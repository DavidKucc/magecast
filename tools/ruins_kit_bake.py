"""The Ruins' building set, from the models as ComfyUI wrote them to the ones the game loads.

    blender -b --factory-startup --python tools/ruins_kit_bake.py -- <ComfyUI output/3d folder> <out folder> [all | Name,Name]

A source model is 50-100 thousand triangles with three 2048 px textures. For each one this writes
<Name>.fbx (1.2-9 thousand triangles), <Name>_albedo.png and <Name>_normal.png, two pictures of the
result beside the original (pv_*.png), and kit.json with the measurements the solids in
Assets/Editor/ArenaRuinsKit.cs were worked out from (rays down and up over the footprint, the outline
per band of height). The .fbx go to Assets/Meshes/Ruins, the textures to Assets/Textures/Ruins.

How a model is brought down, and what went wrong on the way (Nov 2026):
  weld      the source is split along every texture seam; unwelded it is a pile of open patches
  seal      five of the models are open underneath and hollow (HOLLOW): a sheet under the footprint closes them
  strip     faces no ray gets out from (pockets inside the stone) are dropped
  envelope  a voxel remesh, so the grooves between the stones end up in the normal map and not as slivers of
            mesh; without the two steps above it came out as a torn shell or with a second surface inside
  decimate  down to the triangle count in MODELS; the house skips the envelope (its beams are thinner than a voxel)
  bake      new UVs, then normal and colour from the original onto the game mesh (colour as emission, so
            metal and lighting cannot darken it)

Model axes: x along, y across, z up, about 1 unit big, standing on z = 0. Unity turns them on import:
X = -x, Y = z, Z = -y.
"""
import bpy, math, sys, os, json, time
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
ROOT = argv[0]
out_dir = argv[1]
only = set(argv[2].split(",")) if len(argv) > 2 and argv[2] != "all" else None
# for trying things out: one envelope size and one triangle count for every model named
VOXEL = float(argv[3]) if len(argv) > 3 else None
TARGET = int(argv[4]) if len(argv) > 4 else None
# name: (file, triangles, albedo px, normal px)
MODELS = {
    "House":         ("ComfyUI_00005.glb", 9000, 2048, 1024),
    "HouseFar":      ("ComfyUI_00005.glb", 2600, 1024, 512),
    "Bridge":        ("ComfyUI_00006.glb", 5000, 2048, 1024),
    "ColumnBanded":  ("ComfyUI_00007.glb", 2000, 1024, 1024),
    "ColumnCarved":  ("ComfyUI_00008.glb", 2000, 1024, 1024),
    "WallLow":       ("ComfyUI_00009.glb", 1200, 1024, 1024),
    "WallTall":      ("ComfyUI_00010.glb", 1200, 1024, 1024),
    "WallBrick":     ("ComfyUI_00011.glb", 1200, 1024, 1024),
    "StairsMoss":    ("ComfyUI_00012.glb", 2000, 1024, 1024),
    "StairsStone":   ("ComfyUI_00013.glb", 1800, 1024, 1024),
    "PlatformSand": ("exp/best_110_final_00001_.glb", 1500, 1024, 1024),
    "PlatformBrick": ("exp/flush3_final_00001_.glb", 1500, 1024, 1024),
    "PlatformMoss": ("exp/full_c90_final_00001_.glb", 1500, 1024, 1024),
}
VOXELS = {n: 0.012 for n in MODELS}
VOXELS['House'] = 0.0
# open underneath and hollow. Measured with rays from below: on these the first thing hit is the inside of
# the top (57-97 % of the footprint), on the others the model's own underside. Unsealed, their envelopes
# came out as torn shells, or (the brick wall, thick enough to hold) with a second surface inside.
HOLLOW = {'ColumnCarved', 'StairsMoss', 'PlatformSand', 'PlatformMoss', 'WallBrick'}
kit_path = os.path.join(out_dir, "kit.json")
kit = json.load(open(kit_path)) if os.path.exists(kit_path) else {}


def gpu():
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        for kind in ("OPTIX", "CUDA"):
            try:
                prefs.compute_device_type = kind
                prefs.get_devices()
                devs = [d for d in prefs.devices if d.type == kind]
                if devs:
                    for d in prefs.devices:
                        d.use = d.type == kind
                    bpy.context.scene.cycles.device = "GPU"
                    return kind
            except Exception:
                pass
    except Exception:
        pass
    return "CPU"


def image_node(mat, img):
    n = mat.node_tree.nodes.new("ShaderNodeTexImage")
    n.image = img
    return n


def render_views(name, kind, obj_show, obj_hide, lo, hi):
    scene = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            scene.render.engine = eng
            break
        except TypeError:
            pass
    obj_show.hide_render = False
    obj_hide.hide_render = True
    scene.render.resolution_x = scene.render.resolution_y = 512
    c = (lo + hi) / 2
    d = (hi - lo).length
    cam = scene.camera
    for k, (yaw, pitch) in enumerate(((-35, 20), (145, 24))):
        y, p = math.radians(yaw), math.radians(pitch)
        cam.location = c + Vector((math.sin(y) * math.cos(p), -math.cos(y) * math.cos(p), math.sin(p))) * d * 1.15
        cam.rotation_euler = (c - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(out_dir, "pv_%s_%s%d.png" % (name, kind, k))
        bpy.ops.render.render(write_still=True)


for name, (rel, target, a_px, n_px) in MODELS.items():
    if only and name not in only:
        continue
    t0 = time.time()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(ROOT, rel))
    bpy.context.view_layer.update()
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for o in bpy.context.scene.objects:
        o.select_set(o in meshes)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    high = bpy.context.view_layer.objects.active
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    high.data.transform(high.matrix_world)
    high.matrix_world = Matrix.Identity(4)
    co = np.empty(len(high.data.vertices) * 3, dtype=np.float32)
    high.data.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    lo = co.min(0)
    hi = co.max(0)
    # kit space: centred, standing on z = 0
    shift = Vector((-(float(lo[0]) + float(hi[0])) / 2, -(float(lo[1]) + float(hi[1])) / 2, -float(lo[2])))
    high.data.transform(Matrix.Translation(shift))
    high.data.update()
    co = co + np.array(shift, dtype=np.float32)
    lo = Vector(co.min(0).tolist())
    hi = Vector(co.max(0).tolist())
    high.data.calc_loop_triangles()
    src_tris = len(high.data.loop_triangles)

    # ---- measurements on the original surface
    dg = bpy.context.evaluated_depsgraph_get()
    bvh = BVHTree.FromObject(high, dg)
    N = 81
    top = [[None] * N for _ in range(N)]
    under = [[None] * N for _ in range(N)]
    for i in range(N):
        for j in range(N):
            x = lo.x + (hi.x - lo.x) * i / (N - 1)
            y = lo.y + (hi.y - lo.y) * j / (N - 1)
            h = bvh.ray_cast(Vector((x, y, hi.z + 1)), Vector((0, 0, -1)))
            if h[0] is not None:
                top[i][j] = round(h[0].z, 4)
            h = bvh.ray_cast(Vector((x, y, -1)), Vector((0, 0, 1)))
            if h[0] is not None:
                under[i][j] = round(h[0].z, 4)
    NB = 40
    bands = []
    for b in range(NB):
        z0 = hi.z * b / NB
        z1 = hi.z * (b + 1) / NB
        S = co[(co[:, 2] >= z0) & (co[:, 2] <= z1)]
        if len(S) == 0:
            bands.append(None)
            continue
        x0, y0 = float(S[:, 0].min()), float(S[:, 1].min())
        x1, y1 = float(S[:, 0].max()), float(S[:, 1].max())
        r = float(np.sqrt(S[:, 0] ** 2 + S[:, 1] ** 2).max())
        bands.append([round(v, 4) for v in (z0, z1, x0, x1, y0, y1, r)])
    kit[name] = {"file": rel, "size": [round(hi.x - lo.x, 4), round(hi.y - lo.y, 4), round(hi.z, 4)], "src_tris": src_tris,
                 "top": top, "under": under, "bands": bands}

    # ---- the game mesh
    low = high.copy()
    low.data = high.data.copy()
    low.name = name
    bpy.context.scene.collection.objects.link(low)
    for o in bpy.context.scene.objects:
        o.select_set(o is low)
    bpy.context.view_layer.objects.active = low
    low.data.materials.clear()
    # the source is split along every texture seam: weld it, or it is a pile of open patches
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=0.00002)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    # ... and it carries surfaces nobody can see (pockets inside the stone); with them in, the envelope below
    # comes out as a thin torn shell. A face stays if a ray from it gets out in any direction.
    import bmesh
    bm = bmesh.new(); bm.from_mesh(low.data)
    # some of the models are open underneath and hollow: a sheet under the whole footprint seals them, so the
    # inside counts as inside
    cap = None
    if name in HOLLOW:
        cv = [bm.verts.new((x, y, 0.0003)) for x, y in ((lo.x, lo.y), (hi.x, lo.y), (hi.x, hi.y), (lo.x, hi.y))]
        cap = bm.faces.new(cv)
    bm.faces.ensure_lookup_table()
    tree = BVHTree.FromBMesh(bm)
    dirs = []
    for k in range(48):
        zz = 1 - 2 * (k + 0.5) / 48; rr = math.sqrt(max(0.0, 1 - zz * zz)); aa = k * 2.399963
        dirs.append(Vector((rr * math.cos(aa), rr * math.sin(aa), zz)))
    hidden = []
    bm.normal_update()
    for f in bm.faces:
        if f is cap:
            continue
        c = f.calc_center_median(); nrm = f.normal
        free = False
        for dv in dirs:
            if dv.dot(nrm) > 0.05 and tree.ray_cast(c + nrm * 0.0005, dv)[0] is None:
                free = True
                break
        if not free:
            hidden.append(f)
    kit[name]["hidden_faces"] = len(hidden)
    bmesh.ops.delete(bm, geom=hidden, context="FACES")
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    if cap is not None and (VOXEL if VOXEL is not None else VOXELS.get(name, 0.0)) <= 0:
        bmesh.ops.delete(bm, geom=[cap], context="FACES")       # only the envelope wants the sheet
    open_edges = sum(1 for e in bm.edges if len(e.link_faces) != 2)
    bm.to_mesh(low.data); bm.free()
    low.data.update()
    low.data.calc_loop_triangles()
    kit[name]["open_edges"] = open_edges
    voxel = VOXEL if VOXEL is not None else VOXELS.get(name, 0.0)
    if TARGET is not None:
        target = TARGET
    if voxel > 0:
        # an envelope first: the grooves between the stones go into the normal map, not into slivers of mesh
        low.data.remesh_voxel_size = voxel
        low.data.remesh_voxel_adaptivity = 0.0
        bpy.ops.object.voxel_remesh()
        # the envelope of a model with pockets in it comes with shells inside: closed surfaces nobody sees, which
        # took half the triangles of a wall (measured in Unity: 669 of 1320 points lay inside the stone).
        # A shell goes if none of a sample of its faces can see out.
        bm = bmesh.new(); bm.from_mesh(low.data)
        bm.faces.ensure_lookup_table(); bm.normal_update()
        tree = BVHTree.FromBMesh(bm)
        seen = set(); doomed = []; shells = 0; shells_in = 0
        for f0 in bm.faces:
            if f0.index in seen:
                continue
            part = [f0]; seen.add(f0.index); k = 0
            while k < len(part):
                for e in part[k].edges:
                    for g in e.link_faces:
                        if g.index not in seen:
                            seen.add(g.index); part.append(g)
                k += 1
            shells += 1
            step = max(1, len(part) // 300)
            visible = False
            for f in part[::step]:
                c = f.calc_center_median(); nrm = f.normal
                for dv in dirs:
                    if dv.dot(nrm) > 0.05 and tree.ray_cast(c + nrm * 0.0005, dv)[0] is None:
                        visible = True
                        break
                if visible:
                    break
            if not visible:
                doomed.extend(part); shells_in += 1
        kit[name]["shells"] = [shells, shells_in, len(doomed), len(bm.faces)]
        if doomed:
            bmesh.ops.delete(bm, geom=doomed, context="FACES")
            bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
        bm.to_mesh(low.data); bm.free()
        low.data.update()
        low.data.calc_loop_triangles()
    now_tris = len(low.data.loop_triangles) if voxel > 0 else src_tris
    mod = low.modifiers.new("dec", "DECIMATE")
    mod.ratio = min(1.0, target / max(1, now_tris))
    mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier="dec")
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=0.0005)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    try:
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(50))
    except Exception:
        bpy.ops.object.shade_smooth()
    while low.data.uv_layers:
        low.data.uv_layers.remove(low.data.uv_layers[0])
    low.data.uv_layers.new(name="UVMap")
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.004)
    bpy.ops.object.mode_set(mode="OBJECT")
    low.data.calc_loop_triangles()
    kit[name]["tris"] = len(low.data.loop_triangles)

    albedo = bpy.data.images.new(name + "_albedo", a_px, a_px, alpha=False)
    normal = bpy.data.images.new(name + "_normal", n_px, n_px, alpha=False)
    normal.colorspace_settings.name = "Non-Color"
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    low.data.materials.append(mat)
    na = image_node(mat, albedo)
    nn = image_node(mat, normal)

    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 4
    device = gpu()
    bake = scene.render.bake
    bake.use_selected_to_active = True
    bake.cage_extrusion = 0.02
    bake.max_ray_distance = 0.05
    bake.margin = 12
    high.select_set(True)
    low.select_set(True)
    bpy.context.view_layer.objects.active = low

    mat.node_tree.nodes.active = nn
    bpy.ops.object.bake(type="NORMAL")
    normal.filepath_raw = os.path.join(out_dir, name + "_normal.png")
    normal.file_format = "PNG"
    normal.save()

    # colour straight from the texture: as emission, so metal and lighting cannot darken it
    restore = []
    for hm in high.data.materials:
        nt = hm.node_tree
        out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL" and n.is_active_output)
        bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
        src = bsdf.inputs["Base Color"].links[0].from_socket if bsdf and bsdf.inputs["Base Color"].links else None
        em = nt.nodes.new("ShaderNodeEmission")
        if src:
            nt.links.new(src, em.inputs["Color"])
        nt.links.new(em.outputs[0], out.inputs["Surface"])
        restore.append((nt, bsdf, out))
    mat.node_tree.nodes.active = na
    bpy.ops.object.bake(type="EMIT")
    albedo.filepath_raw = os.path.join(out_dir, name + "_albedo.png")
    albedo.file_format = "PNG"
    albedo.save()
    for nt, bsdf, out in restore:           # back to the lit material, for the comparison picture
        if bsdf:
            nt.links.new(bsdf.outputs[0], out.inputs["Surface"])

    # preview material for the game mesh
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    mat.node_tree.links.new(na.outputs["Color"], bsdf.inputs["Base Color"])
    nm = mat.node_tree.nodes.new("ShaderNodeNormalMap")
    mat.node_tree.links.new(nn.outputs["Color"], nm.inputs["Color"])
    mat.node_tree.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = 0.85

    for o in bpy.context.scene.objects:
        o.select_set(o is low)
    bpy.context.view_layer.objects.active = low
    bpy.ops.export_scene.fbx(filepath=os.path.join(out_dir, name + ".fbx"), use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             mesh_smooth_type="OFF", use_tspace=False, add_leaf_bones=False, path_mode="STRIP", embed_textures=False)

    world = bpy.data.worlds.new("w")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.55, 0.62, 0.72, 1)
    bg.inputs[1].default_value = 0.8
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(50), 0, math.radians(35))
    scene.collection.objects.link(sun)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    cam.data.lens = 35
    cam.data.clip_start = 0.01
    scene.collection.objects.link(cam)
    scene.camera = cam
    render_views(name, "low", low, high, lo, hi)
    render_views(name, "high", high, low, lo, hi)
    json.dump(kit, open(kit_path, "w"))
    print("BAKED %s: %d -> %d tris, hidden %d, shells %s, %s, %.0fs" % (name, src_tris, kit[name]["tris"], kit[name]["hidden_faces"], kit[name].get("shells"), device, time.time() - t0), flush=True)
