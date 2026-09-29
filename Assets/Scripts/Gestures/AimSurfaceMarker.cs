using MageCast.Combat;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// While a spell is held, shows what it will do where the crosshair is pointing -- BEFORE you send it.
    ///
    /// Aiming at the floor, at a wall and at a person now produce different spells from one gesture, and
    /// the choice is made by what a ray happens to strike. Without this the player would find out which
    /// one they got by watching it happen, which is the same as the game deciding for them.
    ///
    ///   floor, leaves a patch      a ring the size of the patch, in the spell's colour
    ///   wall, bounces              a small ring and the line the bounce will take
    ///   wall, bursts               a ring the size of the burst
    ///   does nothing there         a small grey ring -- lightning into the ground, say
    ///   a person                   nothing; a direct hit needs no explaining
    /// </summary>
    public class AimSurfaceMarker : MonoBehaviour
    {
        [SerializeField] float lineWidth = 0.05f;
        [SerializeField] float bounceRayLength = 5f;
        [SerializeField] Color nothingColour = new Color(0.6f, 0.6f, 0.65f, 0.8f);

        const int Segments = 40;

        GestureCaster caster;
        ThirdPersonCamera cam;
        LineRenderer ring;
        LineRenderer bounceRay;

        void Awake()
        {
            caster = GetComponent<GestureCaster>();
            cam = FindObjectOfType<ThirdPersonCamera>();
            ring = MakeLine("AimRing", true);
            bounceRay = MakeLine("AimBounce", false);
        }

        LineRenderer MakeLine(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.widthMultiplier = lineWidth;
            lr.numCornerVertices = 2;
            lr.material = RuntimeMaterials.Line();
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;
            return lr;
        }

        void LateUpdate()
        {
            ring.enabled = false;
            bounceRay.enabled = false;

            // A training aid only. Against a person it would be the game telling you where a bounce
            // lands and how far a burst reaches -- reading that off the geometry is part of the skill.
            if (!ShowPredictions) return;

            Spell spell = caster != null ? caster.HeldSpell : null;
            if (spell == null || spell.kind != SpellKind.Projectile || cam == null) return;

            // the same ray the caster aims along, so the marker cannot disagree with the shot
            RaycastHit hit;
            if (!CastShield.AimRay(cam.AimOrigin, cam.AimDirection, 200f, transform, out hit))
                return;

            // skip our own body, which the camera ray can graze at a steep angle
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) return;

            bool person = hit.transform.GetComponentInParent<Health>() != null
                          || hit.transform.GetComponentInParent<PlayerMotor>() != null;
            if (person) return;

            float scale = caster.HeldSizeScale;
            bool floor = hit.normal.y > Projectile.FloorNormal;

            if (floor)
            {
                if (spell.groundEffect != GroundEffect.None)
                    DrawReach(hit.point, spell.zoneRadius * scale, spell.colour);
                else
                    DrawRing(hit.point, Vector3.up, 0.3f, nothingColour);
                return;
            }

            if (spell.wallBounces > 0)
            {
                DrawRing(hit.point, hit.normal, 0.35f, spell.colour);

                Vector3 incoming = (hit.point - caster.MuzzlePoint).normalized;
                Vector3 outgoing = Vector3.Reflect(incoming, hit.normal);
                bounceRay.startColor = spell.colour;
                bounceRay.endColor = new Color(spell.colour.r, spell.colour.g, spell.colour.b, 0f);
                bounceRay.positionCount = 2;
                bounceRay.SetPosition(0, hit.point + hit.normal * 0.02f);
                bounceRay.SetPosition(1, hit.point + outgoing * bounceRayLength);
                bounceRay.enabled = true;
            }
            else if (spell.wallBurstRadius > 0f)
            {
                DrawRing(hit.point, hit.normal, spell.wallBurstRadius * scale, spell.colour);
            }
            else
            {
                DrawRing(hit.point, hit.normal, 0.3f, nothingColour);
            }
        }

        /// <summary>True in training and offline; off in any game with another player in it.</summary>
        static bool ShowPredictions
        {
            get
            {
                NetSession session = NetSession.Instance;
                return session == null || session.Current == NetSession.Mode.Training
                       || session.Current == NetSession.Mode.None;
            }
        }

        // The cut outline is a few hundred raycasts, so it is only worked out again when the aim point
        // or the size has actually moved -- a steady aim costs nothing.
        Vector3 reachAt = new Vector3(float.NaN, 0f, 0f);
        float reachRadius = -1f;
        float[] reach;

        /// <summary>
        /// The patch as it will really lie: cut at the edge of whatever it lands on, exactly the way
        /// SpellZone cuts it, so the marker never promises floor that is not there.
        /// </summary>
        void DrawReach(Vector3 centre, float radius, Color colour)
        {
            if (reach == null || (centre - reachAt).sqrMagnitude > 0.0025f || Mathf.Abs(radius - reachRadius) > 0.01f)
            {
                reach = SpellZone.SupportedReach(centre, radius);
                reachAt = centre;
                reachRadius = radius;
            }

            Vector3 lifted = centre + Vector3.up * 0.04f;
            int n = reach.Length;
            ring.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                ring.SetPosition(i, lifted + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * reach[i]);
            }
            ring.startColor = colour;
            ring.endColor = colour;
            ring.enabled = true;
        }

        /// <summary>A circle lying flat on a surface, lifted a hair off it so it does not z-fight.</summary>
        void DrawRing(Vector3 centre, Vector3 normal, float radius, Color colour)
        {
            Vector3 n = normal.normalized;
            Vector3 tangent = Vector3.Cross(n, Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            Vector3 bitangent = Vector3.Cross(n, tangent);
            Vector3 lifted = centre + n * 0.03f;

            ring.positionCount = Segments;
            for (int i = 0; i < Segments; i++)
            {
                float a = i / (float)Segments * Mathf.PI * 2f;
                ring.SetPosition(i, lifted + (tangent * Mathf.Cos(a) + bitangent * Mathf.Sin(a)) * radius);
            }
            ring.startColor = colour;
            ring.endColor = colour;
            ring.enabled = true;
        }
    }
}
