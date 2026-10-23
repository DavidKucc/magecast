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

            // quick reject: nowhere near the body at all
            Vector3 middle = transform.position + Vector3.up * 0.9f;
            if (DistanceToSegment(middle, a, b) > 1.6f + radius) return false;

            // walked along the way in short steps: a spell covers at most ~0.7 m a frame
            float length = Vector3.Distance(a, b);
            int samples = Mathf.Clamp(Mathf.CeilToInt(length / 0.08f), 1, 24);
            for (int i = 0; i <= samples; i++)
            {
                float s = i / (float)samples;
                Vector3 p = Vector3.Lerp(a, b, s);
                Vector3 on;
                float reach;
                if (Touches(p, radius, out on, out reach))
                {
                    t = s;
                    Vector3 away = p - on;
                    normal = away.sqrMagnitude > 1e-6f ? away.normalized : (a - b).normalized;
                    point = on + normal * reach;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether a ball at <paramref name="p"/> overlaps any part; the nearest part's axis point.</summary>
        bool Touches(Vector3 p, float radius, out Vector3 on, out float reach)
        {
            on = p;
            reach = 0f;
            if (parts.Count == 0)
            {
                Vector3 foot = transform.position + Vector3.up * 0.25f, head = transform.position + Vector3.up * 1.5f;
                on = ClosestOnSegment(p, foot, head);
                reach = 0.25f;
                return (p - on).sqrMagnitude <= (reach + radius) * (reach + radius);
            }
            float best = float.MaxValue;
            bool hit = false;
            foreach (Part part in parts)
            {
                Vector3 from = part.From.position;
                Vector3 to = part.To.position + (part.Beyond != Vector3.zero ? part.To.rotation * part.Beyond : Vector3.zero);
                Vector3 c = ClosestOnSegment(p, from, to);
                float gap = (p - c).magnitude - part.Radius;
                if (gap <= radius && gap < best)
                {
                    best = gap;
                    on = c;
                    reach = part.Radius;
                    hit = true;
                }
            }
            return hit;
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
