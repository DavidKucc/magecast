using System.Collections.Generic;
using UnityEngine;

namespace MageCast.Combat
{
    /// <summary>
    /// Where a person can actually be hit: the body as it stands, from its bones -- the trunk, the head,
    /// both arms and both legs as capsules that move with the animation.
    ///
    /// The CharacterController the body walks with is a 0.4 m-radius capsule 2 m tall, made for not
    /// getting stuck on corners, and spells used to hit that: with a fat spell's own radius on top, ice
    /// passing 0.9 m from somebody's spine still hit them, and it was plain on screen that it had missed
    /// (16 Oct). Spells now sweep against these instead (Projectile); areas -- splash, blasts, patches --
    /// still use the controller, since being near is what they are about.
    ///
    /// Without a humanoid rig (the capsule the game started on) it falls back to one upright capsule of
    /// a body's size.
    /// </summary>
    public class Hurtbox : MonoBehaviour
    {
        static readonly List<Hurtbox> all = new List<Hurtbox>();
        public static IList<Hurtbox> All { get { return all; } }

        struct Part
        {
            public Transform From, To;
            public Vector3 Beyond;     // the segment runs past To by this, in To's frame (a hand, the top of the head)
            public float Radius;
        }

        readonly List<Part> parts = new List<Part>();
        Animator animator;
        bool built;

        public Health Health { get; private set; }

        void Awake()
        {
            Health = GetComponent<Health>();
            // The bones are what gets hit, so they must move even when no camera here sees the body. Left
            // to the default (CullUpdateTransforms), a body out of the host's view kept its last pose:
            // somebody who died in view and came back out of it was hit as if still lying on the floor
            // -- head 0.6 m up instead of 1.6 -- until the host looked their way (23 Oct).
            Animator body = GetComponentInChildren<Animator>();
            if (body != null) body.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        void OnEnable() { all.Add(this); }
        void OnDisable() { all.Remove(this); }

        void Build()
        {
            built = true;
            parts.Clear();
            animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return;

            Add(HumanBodyBones.Hips, HumanBodyBones.Neck, 0.17f);
            Add(HumanBodyBones.Neck, HumanBodyBones.Head, 0.12f, new Vector3(0f, 0.16f, 0f));
            foreach (bool left in new[] { true, false })
            {
                Add(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm,
                    left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm, 0.07f);
                Add(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm,
                    left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand, 0.06f, new Vector3(0f, 0.09f, 0f));
                Add(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg,
                    left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg, 0.09f);
                Add(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg,
                    left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot, 0.07f);
                Add(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot,
                    left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes, 0.05f);
            }
        }

        void Add(HumanBodyBones from, HumanBodyBones to, float radius, Vector3 beyond = default(Vector3))
        {
            Transform a = animator.GetBoneTransform(from), b = animator.GetBoneTransform(to);
            if (a == null || b == null) return;
            parts.Add(new Part { From = a, To = b, Beyond = beyond, Radius = radius });
        }

        /// <summary>The middle of the trunk, for anything that aims at a body.</summary>
        public Vector3 Centre
        {
            get
            {
                if (!built) Build();
                if (parts.Count > 0) return Vector3.Lerp(parts[0].From.position, parts[0].To.position, 0.55f);
                return transform.position + Vector3.up * 1.1f;
            }
        }

        /// <summary>The top of the head -- what shows over cover.</summary>
        public Vector3 Head
        {
            get
            {
                if (!built) Build();
                if (parts.Count > 1)
                {
                    Part head = parts[1];
                    return head.To.position + head.To.rotation * head.Beyond * 0.6f;
                }
                return transform.position + Vector3.up * 1.6f;
            }
        }

        /// <summary>The point of the body nearest to <paramref name="p"/>, on its surface.</summary>
        public Vector3 Nearest(Vector3 p)
        {
            if (!built) Build();
            if (parts.Count == 0)
            {
                Vector3 axis = ClosestOnSegment(p, transform.position + Vector3.up * 0.25f, transform.position + Vector3.up * 1.5f);
                Vector3 out0 = p - axis;
                return out0.magnitude > 0.25f ? axis + out0.normalized * 0.25f : p;
            }
            float best = float.MaxValue;
            Vector3 found = p;
            foreach (Part part in parts)
            {
                Vector3 from = part.From.position;
                Vector3 to = part.To.position + (part.Beyond != Vector3.zero ? part.To.rotation * part.Beyond : Vector3.zero);
                Vector3 c = ClosestOnSegment(p, from, to);
                Vector3 away = p - c;
                float gap = away.magnitude - part.Radius;
                if (gap < best)
                {
                    best = gap;
                    found = gap > 0f ? c + away.normalized * part.Radius : p;
                }
            }
            return found;
        }

        /// <summary>
        /// Where a ball of <paramref name="radius"/> moving from <paramref name="a"/> to <paramref name="b"/>
        /// first touches the body: the share of the way (<paramref name="t"/>, 0..1), the point on the body
        /// and the way out of it. False if it passes clear.
        /// </summary>
        public bool Sweep(Vector3 a, Vector3 b, float radius, out float t, out Vector3 point, out Vector3 normal)
        {
            if (!built) Build();
            t = 0f;
            point = normal = Vector3.zero;

            // Quick reject: nowhere near the body at all. Measured from the trunk itself, not from the
            // object's origin -- a player's is at the soles, but a dummy's is at its waist, and from there a
            // shot at its shins counted as nowhere near (25 Oct).
            Vector3 middle = parts.Count > 0 ? Centre : transform.position + Vector3.up * 0.9f;
            if (DistanceToSegment(middle, a, b) > 1.6f + radius) return false;

            // Every part exactly, as a capsule against the ball's whole way: the first point of the way where
            // the ball is within reach of the part's axis. It used to be walked in at most 24 steps, and
            // one long frame -- a hitch with everybody shooting, up to a third of a second, 17 m of
            // lightning -- left gaps wider than an arm between them, so a shot could go clean through
            // somebody between two steps (25 Oct).
            float first = float.MaxValue;
            Vector3 from = Vector3.zero, to = Vector3.zero;
            float partRadius = 0f;
            if (parts.Count == 0)
            {
                Vector3 foot = transform.position + Vector3.up * 0.25f, head = transform.position + Vector3.up * 1.5f;
                float s = FirstContact(a, b, foot, head, 0.25f + radius);
                if (s >= 0f) { first = s; from = foot; to = head; partRadius = 0.25f; }
            }
            foreach (Part part in parts)
            {
                Vector3 p0 = part.From.position;
                Vector3 p1 = part.To.position + (part.Beyond != Vector3.zero ? part.To.rotation * part.Beyond : Vector3.zero);
                float s = FirstContact(a, b, p0, p1, part.Radius + radius);
                if (s < 0f || s >= first) continue;
                first = s;
                from = p0;
                to = p1;
                partRadius = part.Radius;
            }
            if (first == float.MaxValue) return false;

            t = first;
            Vector3 p = Vector3.Lerp(a, b, t);
            Vector3 away = p - ClosestOnSegment(p, from, to);
            normal = away.sqrMagnitude > 1e-6f ? away.normalized : (a - b).normalized;
            point = p - away + normal * partRadius;
            return true;
        }

        /// <summary>
        /// Where along a..b (0..1) a ball first comes within <paramref name="reach"/> of the segment p0..p1, or
        /// -1 if it never does. The distance from a point moving along a line to a segment falls and then
        /// rises, so the first moment within reach lies before the closest approach and is found by halving.
        /// </summary>
        static float FirstContact(Vector3 a, Vector3 b, Vector3 p0, Vector3 p1, float reach)
        {
            float reach2 = reach * reach;
            if ((a - ClosestOnSegment(a, p0, p1)).sqrMagnitude <= reach2) return 0f;
            float closest = ClosestOnPath(a, b, p0, p1);
            Vector3 at = Vector3.Lerp(a, b, closest);
            if ((at - ClosestOnSegment(at, p0, p1)).sqrMagnitude > reach2) return -1f;

            float lo = 0f, hi = closest;
            for (int i = 0; i < 20; i++)
            {
                float mid = (lo + hi) * 0.5f;
                Vector3 m = Vector3.Lerp(a, b, mid);
                if ((m - ClosestOnSegment(m, p0, p1)).sqrMagnitude <= reach2) hi = mid;
                else lo = mid;
            }
            return hi;
        }

        /// <summary>The point of a..b (0..1) closest to the segment p0..p1 (Ericson, Real-Time Collision Detection 5.1.9).</summary>
        static float ClosestOnPath(Vector3 a, Vector3 b, Vector3 p0, Vector3 p1)
        {
            Vector3 d1 = b - a, d2 = p1 - p0, r = a - p0;
            float aa = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            const float Tiny = 1e-8f;
            if (aa <= Tiny) return 0f;
            float c = Vector3.Dot(d1, r);
            if (e <= Tiny) return Mathf.Clamp01(-c / aa);
            float bd = Vector3.Dot(d1, d2);
            float denom = aa * e - bd * bd;
            float s = denom > Tiny ? Mathf.Clamp01((bd * f - c * e) / denom) : 0f;
            float t = (bd * s + f) / e;
            if (t < 0f) s = Mathf.Clamp01(-c / aa);
            else if (t > 1f) s = Mathf.Clamp01((bd - c) / aa);
            return s;
        }

        static Vector3 ClosestOnSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float l = ab.sqrMagnitude;
            if (l < 1e-8f) return a;
            return a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / l);
        }

        static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            return Vector3.Distance(p, ClosestOnSegment(p, a, b));
        }

        /// <summary>The hurtbox of a body, made on first use.</summary>
        public static Hurtbox Of(Component body)
        {
            if (body == null) return null;
            Hurtbox h = body.GetComponentInParent<Hurtbox>();
            if (h != null) return h;
            PlayerMotor motor = body.GetComponentInParent<PlayerMotor>();
            return motor != null ? motor.gameObject.AddComponent<Hurtbox>() : null;
        }

        // ---------------------------------------------------------------- seeing it

        /// <summary>Training aid: draws every hurtbox's parts as lines (Gizmos are editor-only).</summary>
        public static bool Show;

        static Material lineMaterial;

        void OnRenderObject()
        {
            if (!Show) return;
            if (!built) Build();
            if (lineMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null) return;
                lineMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            }
            lineMaterial.SetPass(0);
            GL.Begin(GL.LINES);
            GL.Color(new Color(1f, 0.3f, 0.3f, 0.9f));
            foreach (Part part in parts)
            {
                Vector3 from = part.From.position;
                Vector3 to = part.To.position + (part.Beyond != Vector3.zero ? part.To.rotation * part.Beyond : Vector3.zero);
                Vector3 axis = (to - from).normalized;
                Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                for (int k = 0; k < 6; k++)
                {
                    Vector3 o = Quaternion.AngleAxis(k * 60f, axis) * side * part.Radius;
                    GL.Vertex(from + o);
                    GL.Vertex(to + o);
                }
            }
            GL.End();
        }
    }
}
