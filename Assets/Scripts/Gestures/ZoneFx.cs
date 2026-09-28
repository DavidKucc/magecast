using System.Collections.Generic;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// The look of a patch on the floor: one of the pack's area effects, fitted to the patch.
    ///
    /// The area effects are made for a spell that is announced first and lands later -- two seconds of
    /// warning circle, a burst, five seconds of loop, then a fade -- on open ground. A patch is none of
    /// that: it is there the moment the spell lands, it lasts as long as the spell says, and it ends
    /// where the platform ends. So the effect is fitted when it is placed:
    ///
    ///  - retimed: the warning is dropped, the loop is stretched or shortened to the patch's lifetime,
    ///    and the fade-out is moved to its end;
    ///  - cut to the ground: flat decals are redrawn on a copy of the patch's own outline, and anything
    ///    born out over a drop is removed as it appears -- a patch hangs over nothing, and neither may
    ///    its sparks.
    ///
    /// Only looks: the patch (SpellZone) keeps its own solid outline and does all the gameplay.
    /// </summary>
    public class ZoneFx : MonoBehaviour
    {
        /// <summary>How long the pack's area effects loop for between the burst and the fade.</summary>
        const float AuthoredLoop = 5f;

        /// <summary>The loop ends this long before the patch does, so its fade plays while it dims.</summary>
        const float FadeLead = 1f;

        Vector3 centre;
        float[] reach;
        float radius;
        bool cut;
        float lifetime;
        float age;
        bool fading;

        AudioSource loop;
        AudioClip endClip;
        bool endPlayed;

        readonly List<ParticleSystem> trimmed = new List<ParticleSystem>();
        readonly List<Mesh> meshes = new List<Mesh>();
        static ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[256];

        public static ZoneFx Play(SpellFx.Entry e, Vector3 at, float radius, float lifetime, float[] reach)
        {
            if (e == null || e.zone == null) return null;

            GameObject go = Instantiate(e.zone, at, Quaternion.identity);
            float scale = radius / Mathf.Max(0.1f, e.zoneAuthoredRadius);
            go.transform.localScale = e.zone.transform.localScale * scale;
            SpellFx.Tame(go);

            ZoneFx fx = go.AddComponent<ZoneFx>();
            fx.centre = at;
            fx.reach = reach;
            fx.radius = radius;
            fx.lifetime = lifetime;
            foreach (float r in reach) if (r < radius - 0.02f) { fx.cut = true; break; }

            // Everything stopped first: timings can only be changed on a system that is not running, and
            // it all starts again together once it fits.
            ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in systems) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (ParticleSystem ps in systems) fx.Fit(ps, e.zoneLeadIn, scale);
            foreach (ParticleSystem ps in systems) ps.Play(false);

            fx.Sounds(e);
            return fx;
        }

        void Fit(ParticleSystem ps, float leadIn, float scale)
        {
            var main = ps.main;
            float delay = main.startDelay.constantMax;
            float life = main.startLifetime.constantMax;
            float loopEnd = Mathf.Max(0.5f, lifetime - FadeLead);

            // the warning before the burst: gone, it would announce a spell that has already landed
            float shifted = delay - leadIn;
            if (shifted < -0.01f)
            {
                if (delay + life <= leadIn + 0.05f)
                {
                    var emission = ps.emission;
                    emission.enabled = false;
                    return;
                }
                shifted = 0f;
            }

            if (shifted >= AuthoredLoop - 0.1f)
            {
                // the fade-out: moved to wherever this patch's loop ends
                shifted += loopEnd - AuthoredLoop;
            }
            else
            {
                // the loop: emitters run until it ends, a single long-lived piece lives until then
                if (main.duration >= AuthoredLoop - 0.1f && !main.loop)
                    main.duration = Mathf.Max(0.1f, loopEnd - shifted);
                if (life >= AuthoredLoop - 0.1f)
                    main.startLifetime = Stretch(main.startLifetime, loopEnd - AuthoredLoop);
            }
            main.startDelay = Mathf.Max(0f, shifted);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;

            // The soft floor glows are drawn many times the area's size for the pack's dark demo stage;
            // on a lit arena they wash out everything around. Kept to about the patch.
            if (renderer.renderMode == ParticleSystemRenderMode.HorizontalBillboard && !main.startSize3D)
            {
                float most = GlowAcross * radius / scale;
                if (main.startSize.constantMax > most)
                    main.startSize = Shrink(main.startSize, most / main.startSize.constantMax);
            }

            // A flat decal lying on the floor: redrawn on the patch's outline, so it ends at the edge.
            if (renderer.renderMode == ParticleSystemRenderMode.Mesh && renderer.mesh != null
                && renderer.mesh.name == "Quad" && main.startRotation3D
                && Mathf.Abs(Mathf.DeltaAngle(main.startRotationX.constantMax * Mathf.Rad2Deg, 270f)) < 1f)
            {
                if (cut)
                {
                    float size = main.startSize.constantMax * scale;
                    Mesh m = OutlineQuad(size);
                    meshes.Add(m);
                    renderer.mesh = m;
                    // a spun decal would turn the outline with it
                    main.startRotationY = 0f;
                    main.startRotationZ = 0f;
                    var spin = ps.rotationOverLifetime;
                    spin.enabled = false;
                }
                return;
            }

            var shape = ps.shape;
            bool spread = shape.enabled && shape.shapeType != ParticleSystemShapeType.Mesh && shape.radius * scale > 0.5f;

            // A ring or wall born at the centre and reaching out to the rim -- the boundary marker, the
            // floor discs: pulled in to follow the outline, so the boundary stands where the ground ends.
            if (cut && !spread && renderer.renderMode == ParticleSystemRenderMode.Mesh && renderer.mesh != null
                && renderer.mesh.isReadable && main.startRotation3D)
            {
                Quaternion turn = Quaternion.Euler(main.startRotationX.constantMax * Mathf.Rad2Deg, 0f, 0f);
                Mesh m = FollowOutline(renderer.mesh, turn);
                meshes.Add(m);
                renderer.mesh = m;
                main.startRotationY = 0f;
                main.startRotationZ = 0f;
                var spin = ps.rotationOverLifetime;
                spin.enabled = false;
                return;
            }

            // Anything spread across the area rather than rising from the centre is checked as it is born.
            bool flat = renderer.renderMode == ParticleSystemRenderMode.HorizontalBillboard
                     || renderer.renderMode == ParticleSystemRenderMode.None;
            if (cut && spread && !flat) trimmed.Add(ps);
        }

        /// <summary>Widest a floor glow may be, in patch radii (2 = exactly the patch; a glow is soft at its rim).</summary>
        const float GlowAcross = 2.6f;

        static ParticleSystem.MinMaxCurve Shrink(ParticleSystem.MinMaxCurve c, float by)
        {
            if (c.mode == ParticleSystemCurveMode.TwoConstants)
                return new ParticleSystem.MinMaxCurve(c.constantMin * by, c.constantMax * by);
            if (c.mode == ParticleSystemCurveMode.Constant)
                return new ParticleSystem.MinMaxCurve(c.constantMax * by);
            c.curveMultiplier *= by;
            return c;
        }

        static ParticleSystem.MinMaxCurve Stretch(ParticleSystem.MinMaxCurve c, float by)
        {
            if (c.mode == ParticleSystemCurveMode.TwoConstants)
                return new ParticleSystem.MinMaxCurve(Mathf.Max(0.1f, c.constantMin + by), Mathf.Max(0.1f, c.constantMax + by));
            return new ParticleSystem.MinMaxCurve(Mathf.Max(0.1f, c.constantMax + by));
        }

        /// <summary>
        /// The pack's unit quad, cut to the patch. It lies in the quad's own plane -- the decal particle
        /// turns it flat (270 degrees about X, which takes quad y to world -z) and sizes it -- and keeps the
        /// quad's texture mapping, so the decal looks exactly as before wherever there is ground.
        /// </summary>
        Mesh OutlineQuad(float size)
        {
            int n = reach.Length;
            var vertices = new Vector3[n + 1];
            var uvs = new Vector2[n + 1];
            var normals = new Vector3[n + 1];
            var tangents = new Vector4[n + 1];
            var triangles = new int[n * 3];
            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);

                // on open ground the decal keeps its full square; towards an edge it stops at the edge
                float square = 0.5f / Mathf.Max(Mathf.Abs(c), Mathf.Abs(s));
                float r = reach[i] >= radius - 0.02f ? square : Mathf.Min(square, reach[i] / size);

                var p = new Vector3(c * r, -s * r, 0f);
                vertices[i + 1] = p;
                uvs[i + 1] = new Vector2(p.x + 0.5f, p.y + 0.5f);

                // wound the way the quad is (its y runs opposite to the angle here)
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % n + 1;
            }

            // The quad's own normal and tangent, set outright: the decal shaders read them to project the
            // decal, and a normal left to be worked out from the triangles would not be the quad's.
            for (int i = 0; i <= n; i++)
            {
                normals[i] = new Vector3(0f, 0f, -1f);
                tangents[i] = new Vector4(1f, 0f, 0f, -1f);
            }

            var mesh = new Mesh { name = "ZoneDecal" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A copy of a centred mesh drawn in towards the middle wherever the patch is cut: every vertex
        /// keeps its height and its direction and is moved in by how much shorter the patch is that way.
        /// Worked out in the particle's turned frame, which is the one it lies in on the floor.
        /// </summary>
        Mesh FollowOutline(Mesh source, Quaternion turn)
        {
            Mesh m = Instantiate(source);
            m.name = source.name + "_Outline";
            Quaternion back = Quaternion.Inverse(turn);
            Vector3[] v = m.vertices;
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 w = turn * v[i];
                if (w.x * w.x + w.z * w.z < 1e-6f) continue;
                float factor = Mathf.Clamp01(SpellZone.ReachTowards(reach, w) / radius);
                w.x *= factor;
                w.z *= factor;
                v[i] = back * w;
            }
            m.vertices = v;
            m.RecalculateBounds();
            return m;
        }

        void Sounds(SpellFx.Entry e)
        {
            // the pack's area effects carry an empty looping source; its sounds come separately
            loop = GetComponent<AudioSource>();
            if (loop != null) { loop.Stop(); loop.clip = null; }

            if (e.zoneStart != null) OneShot(e.zoneStart);
            if (e.zoneLoop != null)
            {
                if (loop == null) { loop = gameObject.AddComponent<AudioSource>(); SpellFx.MakeSpatial(loop); }
                loop.clip = e.zoneLoop;
                loop.loop = true;
                loop.volume = 0.7f;
                loop.Play();
            }
            endClip = e.zoneEnd;
        }

        void OneShot(AudioClip clip)
        {
            var go = new GameObject("ZoneSound");
            go.transform.position = centre;
            AudioSource a = go.AddComponent<AudioSource>();
            SpellFx.MakeSpatial(a);
            a.clip = clip;
            a.Play();
            Destroy(go, clip.length + 0.1f);
        }

        /// <summary>The patch went early -- replaced, or over the limit: let go of it where it is.</summary>
        public void Fade()
        {
            if (fading) return;
            fading = true;
            foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            Destroy(gameObject, 2f);
        }

        void Update()
        {
            age += Time.deltaTime;

            if (loop != null && loop.isPlaying)
            {
                float left = (fading ? 0f : lifetime) - age;
                if (fading) loop.volume = Mathf.MoveTowards(loop.volume, 0f, Time.deltaTime * 2f);
                else if (left < 0.6f) loop.volume = 0.7f * Mathf.Clamp01(left / 0.6f);
                if (loop.volume <= 0.001f) loop.Stop();
            }
            if (!fading && !endPlayed && endClip != null && age >= lifetime - 0.3f)
            {
                endPlayed = true;
                OneShot(endClip);
            }

            // everything has had time to fade by now
            if (!fading && age > lifetime + 3f) Destroy(gameObject);
        }

        void LateUpdate()
        {
            if (!cut) return;
            foreach (ParticleSystem ps in trimmed)
            {
                if (ps == null || ps.particleCount == 0) continue;
                if (buffer.Length < ps.main.maxParticles) buffer = new ParticleSystem.Particle[ps.main.maxParticles];
                int count = ps.GetParticles(buffer);
                bool local = ps.main.simulationSpace == ParticleSystemSimulationSpace.Local;
                bool changed = false;
                for (int i = 0; i < count; i++)
                {
                    Vector3 world = local ? ps.transform.TransformPoint(buffer[i].position) : buffer[i].position;
                    if (OverTheEdge(world)) { buffer[i].remainingLifetime = 0f; changed = true; }
                }
                if (changed) ps.SetParticles(buffer, count);
            }
        }

        bool OverTheEdge(Vector3 world)
        {
            Vector3 offset = world - centre;
            float edge = SpellZone.ReachTowards(reach, offset);
            if (edge >= radius - 0.02f) return false;                  // open ground that way
            return new Vector2(offset.x, offset.z).magnitude > edge + 0.1f;
        }

        void OnDestroy()
        {
            foreach (Mesh m in meshes) if (m != null) Destroy(m);
        }
    }
}
