using System.Collections.Generic;
using MageCast.Combat;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// The barrier: a slab of cover planted in the aimed direction. Unlike the other spells it is not
    /// aimed AT anything -- it is placed, so where it goes is a positioning decision rather than a shot.
    ///
    /// It is a real collider, so it blocks projectiles from both sides, including the caster's own.
    /// That is deliberate: a barrier you can shoot through but your opponent cannot is a much bigger
    /// design commitment (teams, ownership, filtering) and it is worth finding out first whether the
    /// honest version is more interesting. Planting a wall you then have to move around is a real
    /// decision; a one-way window is just free value.
    ///
    /// It wears down. Every spell that hits it takes durability by how hard it hit and what it is --
    /// fire chews through it, lightning barely scratches it, air hardly at all -- so a barrier is a
    /// moment of safety, not a place to live. Shots stop against it; they do not bounce back at the
    /// shooter, which would make a barrier a reward for being shot at rather than a cost.
    ///
    /// One per player: putting up a new one takes the old one down.
    /// </summary>
    public class CastShield : MonoBehaviour
    {
        /// <summary>Durability of a cleanly drawn barrier; the draw scales it 0.6-1.4x.</summary>
        public const float BaseDurability = 60f;

        static readonly Dictionary<ulong, CastShield> byOwner = new Dictionary<ulong, CastShield>();

        float remaining;
        float total;
        float durability;
        float maxDurability;
        Material material;
        Color colour;
        ulong owner = Health.NoAttacker;
        PlayerNet ownerNet;           // the caster's network side, for telling everyone about wear
        bool authoritative = true;

        // The energy wall: drawn on a quad in front of the (hidden) collider box. Null when the shader's
        // template material is missing, and the plain translucent slab is used instead.
        Material wall;
        float age;
        SpellFx.Entry fx;
        AudioSource hum;

        /// <summary>The last few hits, for the ripples: where (UV), when, how hard.</summary>
        readonly Vector4[] hits = { new Vector4(0, 0, -100, 0), new Vector4(0, 0, -100, 0),
                                    new Vector4(0, 0, -100, 0), new Vector4(0, 0, -100, 0) };
        int nextHit;

        /// <summary>How long it takes to rise out of the floor.</summary>
        const float RiseTime = 0.35f;

        /// <summary>The AoE pack's bursts wind up this long before their bang.</summary>
        const float BurstLeadIn = 0.5f;

        // the tier III dome: a sphere around its caster, half in the floor, following them
        bool isDome;
        Transform follows;
        Vector3[] domeVertices;
        Vector2[] domeUvs;
        const float DomeLift = 0.1f;

        public bool IsDome { get { return isDome; } }

        /// <summary>Who a dome goes around -- their own spells pass through it. Null for a wall.</summary>
        public Transform Follows { get { return follows; } }

        public float DurabilityFraction { get { return maxDurability > 0f ? durability / maxDurability : 0f; } }

        public static CastShield Spawn(Vector3 at, Vector3 facing, float width, float height,
                                       float lifetime, Color colour, float durability = BaseDurability,
                                       ulong owner = Health.NoAttacker, PlayerNet ownerNet = null,
                                       bool authoritative = true)
        {
            // one per player, on every machine alike
            CastShield previous;
            if (byOwner.TryGetValue(owner, out previous) && previous != null) Destroy(previous.gameObject);

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "CastShield";
            go.transform.position = at + Vector3.up * (height * 0.5f);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(facing.x, 0f, facing.z).normalized, Vector3.up);
            go.transform.localScale = new Vector3(width, height, 0.15f);

            Material m = RuntimeMaterials.Fade(colour, 0.42f, 0.8f);
            go.GetComponent<Renderer>().material = m;

            CastShield s = go.AddComponent<CastShield>();
            s.BuildWall(width, height, colour);
            s.Init(lifetime, durability, m, colour, owner, ownerNet, authoritative);
            return s;
        }

        /// <summary>
        /// Tier III: a dome around <paramref name="around"/> for a few seconds, going where they go. It
        /// stops everybody else's spells and lets its caster's own out (Projectile skips it for them),
        /// and it does not push its caster around -- their body ignores it.
        /// </summary>
        public static CastShield SpawnDome(Transform around, float radius, float lifetime, Color colour,
                                           float durability, ulong owner, PlayerNet ownerNet, bool authoritative)
        {
            CastShield previous;
            if (byOwner.TryGetValue(owner, out previous) && previous != null) Destroy(previous.gameObject);

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "CastDome";
            go.transform.position = around.position + Vector3.up * DomeLift;
            go.transform.localScale = Vector3.one * (radius * 2f);
            Collider shell = go.GetComponent<Collider>();
            foreach (Collider c in around.GetComponentsInChildren<Collider>(true)) Physics.IgnoreCollision(shell, c);

            Material m = RuntimeMaterials.Fade(colour, 0.3f, 0.8f);
            go.GetComponent<Renderer>().material = m;

            CastShield s = go.AddComponent<CastShield>();
            s.isDome = true;
            s.follows = around;
            s.BuildDome(radius, colour);
            s.Init(lifetime, durability, m, colour, owner, ownerNet, authoritative);
            return s;
        }

        void Init(float lifetime, float durability, Material m, Color colour, ulong owner, PlayerNet ownerNet,
                  bool authoritative)
        {
            CastShield s = this;
            s.remaining = lifetime;
            s.total = lifetime;
            s.durability = durability;
            s.maxDurability = durability;
            s.material = m;
            s.colour = colour;
            s.owner = owner;
            s.ownerNet = ownerNet;
            s.authoritative = authoritative;
            byOwner[owner] = s;
        }

        void BuildDome(float radius, Color c)
        {
            Sounds();
            Material template = Resources.Load<Material>("RuntimeMaterials/EnergyWall");
            if (template == null) return;

            Renderer r = GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            wall = new Material(template);
            wall.SetColor("_Color", c);
            wall.SetVector("_Size", new Vector4(Mathf.PI * 2f * radius, radius * 2f, 0f, 0f));
            wall.SetFloat("_Frame", 0f);
            wall.SetFloat("_GroundAt", 0.5f - DomeLift / (radius * 2f));
            wall.SetFloat("_Rise", 0f);
            wall.SetVectorArray("_Hits", hits);
            r.material = wall;

            Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
            domeVertices = mesh.vertices;
            domeUvs = mesh.uv;

            // a burst of light as it closes over you, and a glowing ring on the floor under it that
            // goes where you go
            Vector3 feet = follows.position;
            if (fx != null && fx.special != null)
                SpellFx.PlayBurst(fx.special, feet, fx.specialScale * radius / 3f * 1.2f, fx.specialLeadIn);
            if (fx != null && fx.zone != null)
            {
                var reach = new float[SpellZone.Spokes];
                for (int i = 0; i < reach.Length; i++) reach[i] = radius;
                floorRing = ZoneFx.Play(fx, feet, radius, SpellTiers.DomeSeconds, reach);
                if (floorRing != null) floorRing.transform.SetParent(follows, true);
            }
        }

        ZoneFx floorRing;

        void BuildWall(float width, float height, Color c)
        {
            Sounds();

            Material template = Resources.Load<Material>("RuntimeMaterials/EnergyWall");
            if (template == null) return;

            GetComponent<Renderer>().enabled = false;
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.name = "EnergyWall";
            quad.transform.SetParent(transform, false);   // the box is scaled to the wall; the quad follows
            Renderer r = quad.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            wall = new Material(template);
            wall.SetColor("_Color", c);
            wall.SetVector("_Size", new Vector4(width, height, 0f, 0f));
            wall.SetFloat("_Rise", 0f);
            wall.SetVectorArray("_Hits", hits);
            r.material = wall;

            // the light burst along its foot as it goes up
            if (fx != null && fx.special != null)
                SpellFx.PlayBurst(fx.special, transform.position - Vector3.up * (height * 0.5f),
                                  fx.specialScale * width * 0.3f, fx.specialLeadIn);
        }

        void Sounds()
        {
            fx = SpellFx.ByName("BARRIER");
            Vector3 middle = transform.position;
            if (fx != null)
            {
                SpellFx.OneShot(fx.raise, middle, 0.9f);
                // the dome's floor ring brings its own loop; a wall hums
                if (fx.hum != null && !isDome)
                {
                    hum = gameObject.AddComponent<AudioSource>();
                    SpellFx.MakeSpatial(hum);
                    hum.clip = fx.hum;
                    hum.loop = true;
                    hum.volume = HumVolume * SpellFx.Volume;
                    hum.Play();
                }
            }
        }

        /// <summary>
        /// Tiles of the wall, in its own material, thrown out both ways and falling as they fade.
        /// Made here rather than taken from a pack: nothing there breaks like a pane of energy.
        /// </summary>
        void Shatter()
        {
            Vector3 size = transform.localScale;
            var go = new GameObject("BarrierShards");
            go.transform.SetPositionAndRotation(transform.position, transform.rotation);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.1f;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.34f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.7f;
            main.maxParticles = 80;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(size.x * size.y * 9f, 30f, 70f)) });

            var shape = ps.shape;
            if (isDome)
            {
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = size.x * 0.5f;
                shape.rotation = new Vector3(-90f, 0f, 0f);
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(size.x, size.y, 0.05f);
            }
            shape.randomDirectionAmount = 1f;

            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
            spin.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
            spin.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            renderer.alignment = ParticleSystemRenderSpace.World;
            var m = new Material(wall);
            m.SetVector("_Size", new Vector4(0.3f, 0.3f, 0f, 0f));
            m.SetFloat("_Strength", 1f);
            m.SetFloat("_Rise", 1f);
            m.SetFloat("_UseVertexColor", 1f);
            renderer.material = m;

            ps.Play();
            Destroy(go, 1.6f);
            Destroy(m, 1.7f);
        }

        /// <summary>Under the fight, not over it: a barrier stands for seconds.</summary>
        const float HumVolume = 0.25f;

        /// <summary>
        /// A spell has hit it here -- on every machine, the copies of shots included, since this is only
        /// the picture: a ripple from the point and a crack of sound.
        /// </summary>
        public void Struck(Vector3 point, float howHard)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            Vector2 uv = new Vector2(Mathf.Clamp01(local.x + 0.5f), Mathf.Clamp01(local.y + 0.5f));
            if (isDome && domeVertices != null)
            {
                // the sphere's own mapping, read off its nearest vertex
                Vector3 onShell = local.normalized * 0.5f;
                float nearest = float.MaxValue;
                for (int i = 0; i < domeVertices.Length; i++)
                {
                    float d = (domeVertices[i] - onShell).sqrMagnitude;
                    if (d < nearest) { nearest = d; uv = domeUvs[i]; }
                }
            }
            hits[nextHit] = new Vector4(uv.x, uv.y, Time.timeSinceLevelLoad, Mathf.Clamp(howHard, 0.3f, 1f));
            nextHit = (nextHit + 1) % hits.Length;
            if (wall != null) wall.SetVectorArray("_Hits", hits);
            if (fx != null) SpellFx.OneShot(fx.struck, point, 0.55f);
        }

        /// <summary>
        /// A ray for aiming from <paramref name="self"/>: the first thing it meets, looking through their
        /// own dome -- they are inside it, and it must not catch their crosshair.
        /// </summary>
        public static bool AimRay(Vector3 origin, Vector3 direction, float distance, Transform self, out RaycastHit hit)
        {
            hit = new RaycastHit();
            RaycastHit[] all = Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            foreach (RaycastHit h in all)
            {
                CastShield s = h.collider.GetComponentInParent<CastShield>();
                if (s != null && s.isDome && s.follows == self) continue;
                if (h.distance < nearest) { nearest = h.distance; hit = h; found = true; }
            }
            return found;
        }

        /// <summary>The current barrier of a player on this machine, or null.</summary>
        public static CastShield Of(ulong owner)
        {
            CastShield s;
            return byOwner.TryGetValue(owner, out s) ? s : null;
        }

        /// <summary>
        /// A hit takes this much durability. Only the copy that decides -- the server's, or offline -- does
        /// the arithmetic; everybody else is told the result, so a barrier breaks at the same moment for
        /// everyone rather than whenever each machine's copy of the shots happened to wear it through.
        /// </summary>
        public void Wear(float amount)
        {
            if (!authoritative || amount <= 0f) return;
            durability = Mathf.Max(0f, durability - amount);

            if (ownerNet != null && ownerNet.IsSpawned) ownerNet.BroadcastBarrierWear(DurabilityFraction);
            if (durability <= 0f) Break();
        }

        /// <summary>Everybody else's copy, set to what the server said is left.</summary>
        public void SetWear(float fraction)
        {
            durability = maxDurability * Mathf.Clamp01(fraction);
            if (durability <= 0f) Break();
        }

        void Break()
        {
            // shattered: the wall flies apart into pieces of itself, with a flash and the sound of it
            if (wall != null) Shatter();
            if (fx != null && fx.impact != null)
                SpellFx.PlayBurst(fx.impact,
                                  isDome && follows != null ? follows.position
                                                            : transform.position - Vector3.up * (transform.localScale.y * 0.5f),
                                  fx.impactScale * transform.localScale.x, BurstLeadIn);
            else
                Projectile.FlashAt(transform.position, colour, 2.4f, 2.2f);
            if (fx != null) SpellFx.OneShot(fx.shatter, transform.position, 1f);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (wall != null) Destroy(wall);
            if (floorRing != null && remaining > 0.3f) floorRing.Fade();
            CastShield current;
            if (byOwner.TryGetValue(owner, out current) && current == this) byOwner.Remove(owner);
        }

        /// <summary>
        /// The Standard shader needs all of this set by hand to blend -- setting only the alpha on the
        /// colour leaves it fully opaque, and an opaque shield you cannot see past is worse than none.
        /// </summary>
        public static void MakeTransparent(Material m)
        {
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
        }

        void Update()
        {
            if (isDome)
            {
                if (follows == null) { Destroy(gameObject); return; }
                transform.position = follows.position + Vector3.up * DomeLift;
            }

            remaining -= Time.deltaTime;
            age += Time.deltaTime;
            if (remaining <= 0f) { Destroy(gameObject); return; }

            // Visibly weakening -- by time AND by damage, whichever is further gone -- so the opponent
            // can see one more fireball will do it, and the caster knows when to move.
            float t = Mathf.Min(remaining / Mathf.Max(0.01f, total), DurabilityFraction);

            if (hum != null) hum.volume = HumVolume * SpellFx.Volume * Mathf.Clamp01(remaining / 0.5f);
            if (wall != null)
            {
                wall.SetFloat("_Strength", t);
                wall.SetFloat("_Rise", Mathf.Clamp01(age / RiseTime));
                return;
            }

            Color c = material.color;
            c.a = Mathf.Lerp(0.1f, 0.42f, t);
            material.color = c;
            material.SetColor("_EmissionColor", colour * Mathf.Lerp(0.15f, 0.8f, t));
        }
    }
}
