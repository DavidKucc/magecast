using MageCast.Combat;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// While a spell is held, shows what it will do where the crosshair is pointing -- BEFORE you send it.
    ///
    /// In every game: ice or fire aimed at the floor at your feet shows the ground the wall or the line
    /// will take (Footprint). That is about where YOUR spell goes, not about reading anyone.
    ///
    /// In training only, and only what the spell in hand does AT ITS TIER (SpellTiers) -- a tier I
    /// leaves no patch, so it shows none:
    ///
    ///   floor   fire, ice from II their patch (fire III: and the blast round it); lightning from II
    ///           its shock patch; air from II its updraft, at III into a patch the reach of the air bomb
    ///   wall    fire from II the bounce and the line it takes; air the burst, every tier
    ///   a person, or no patch, burst or bounce there: nothing
    ///
    /// A tier I's splash is not drawn: a ring on the floor reads as a patch, and a tier I lays none
    /// (15 Oct, the player took it for one).
    /// </summary>
    public class AimSurfaceMarker : MonoBehaviour
    {
        [SerializeField] float lineWidth = 0.05f;
        [SerializeField] float bounceRayLength = 5f;

        const int Segments = 40;

        GestureCaster caster;
        ThirdPersonCamera cam;
        LineRenderer ring;
        LineRenderer ring2;          // a second reach at once: fire III's blast round its patch
        LineRenderer bounceRay;

        void Awake()
        {
            caster = GetComponent<GestureCaster>();
            cam = FindObjectOfType<ThirdPersonCamera>();
            ring = MakeLine("AimRing", true);
            ring2 = MakeLine("AimRing2", true);
            bounceRay = MakeLine("AimBounce", false);
            footprint = MakeLine("AimFootprint", true);
        }

        LineRenderer footprint;

        // the outline is a few dozen raycasts, worked out again only when the spot or the size moved
        Vector3 footAt = new Vector3(float.NaN, 0f, 0f);
        Vector3 footFacing;
        float footLength = -1f, footDepth = -1f;
        Vector3[] footPoints;

        /// <summary>Points along each long side of the outline, and along each short one.</summary>
        const int FootLong = 14, FootShort = 3;

        /// <summary>
        /// Ice or fire aimed at the floor close in front: the ground the wall or the line will cover, as
        /// an outline lying on the floor -- following its rises and dips, nothing drawn upward. How tall
        /// the thing is does not read from a frame in the air, and it is not what you are choosing; where
        /// it goes is. Shown in every game, not only training -- it is about what YOUR spell is going to
        /// do, and a wall or a line you did not mean to lay is a wasted spell.
        /// </summary>
        bool Footprint()
        {
            Vector3 spot, facing;
            float length, depth;
            if (caster == null || !caster.PlacePreview(out spot, out facing, out length, out depth)) return false;

            facing = facing.normalized;
            if (footPoints == null || (spot - footAt).sqrMagnitude > 0.0004f || Vector3.Dot(facing, footFacing) < 0.9995f
                || Mathf.Abs(length - footLength) > 0.01f || Mathf.Abs(depth - footDepth) > 0.01f)
            {
                footPoints = Outline(spot, facing, length, depth);
                footAt = spot;
                footFacing = facing;
                footLength = length;
                footDepth = depth;
            }

            footprint.positionCount = footPoints.Length;
            footprint.SetPositions(footPoints);
            Color c = caster.HeldSpell != null ? caster.HeldSpell.colour : Color.cyan;
            footprint.startColor = footprint.endColor = new Color(c.r, c.g, c.b, 0.9f);
            footprint.enabled = true;
            return true;
        }

        /// <summary>
        /// The rectangle <paramref name="length"/> across the aim by <paramref name="depth"/> along it,
        /// centred on <paramref name="spot"/>, each point dropped onto the map (through people and spells,
        /// the way the wall and the line find their floor) and lifted a hair so it does not z-fight.
        /// </summary>
        static Vector3[] Outline(Vector3 spot, Vector3 facing, float length, float depth)
        {
            Vector3 right = Vector3.Cross(Vector3.up, facing).normalized;
            float hl = length * 0.5f, hd = depth * 0.5f;
            Vector3[] corners =
            {
                spot - right * hl - facing * hd,
                spot + right * hl - facing * hd,
                spot + right * hl + facing * hd,
                spot - right * hl + facing * hd,
            };
            int[] steps = { FootLong, FootShort, FootLong, FootShort };
            var points = new Vector3[FootLong * 2 + FootShort * 2];
            int k = 0;
            for (int side = 0; side < 4; side++)
            {
                Vector3 a = corners[side], b = corners[(side + 1) % 4];
                for (int i = 0; i < steps[side]; i++)
                {
                    Vector3 p = Vector3.Lerp(a, b, i / (float)steps[side]);
                    RaycastHit h;
                    if (FireLine.Ground(p + Vector3.up * 1.5f, 3.5f, out h) && Mathf.Abs(h.point.y - spot.y) < 1.5f)
                        p.y = h.point.y;
                    points[k++] = p + Vector3.up * 0.05f;
                }
            }
            return points;
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
            ring2.enabled = false;
            bounceRay.enabled = false;
            footprint.enabled = false;

            if (Footprint()) return;

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
            int tier = caster.HeldTier;
            bool floor = hit.normal.y > Projectile.FloorNormal;
            bool fire = spell.groundEffect == GroundEffect.Burn, ice = spell.groundEffect == GroundEffect.Ice;
            bool air = spell.groundEffect == GroundEffect.Updraft, lightning = spell.chargeDamage > 0f;
            Color blast = new Color(spell.colour.r, spell.colour.g, spell.colour.b, 0.55f);

            if (floor)
            {
                if ((fire || ice) && tier >= 2)
                {
                    DrawReach(hit.point, spell.zoneRadius * scale, spell.colour);
                    if (fire && tier >= 3)
                        DrawRing(ring2, hit.point, Vector3.up, SpellTiers.ExplosionRadius, blast);
                }
                else if (lightning && tier >= 2)
                    DrawReach(hit.point, SpellTiers.ShockRadius * scale, spell.colour);
                else if (air && tier >= 3 && SpellZone.AnyAt(hit.point, spell.radius) != null)
                {
                    // into a patch: the bomb, which pulls in everyone in it and this far round it
                    SpellZone patch = SpellZone.AnyAt(hit.point, spell.radius);
                    DrawRing(ring, patch.transform.position, Vector3.up, patch.Radius + SpellTiers.BombReachBeyondPatch, spell.colour);
                }
                else if (air && tier >= 2)
                    DrawReach(hit.point, spell.zoneRadius * scale, spell.colour);
                return;
            }

            if (spell.wallBounces > 0 && tier >= 2)
            {
                DrawRing(ring, hit.point, hit.normal, 0.35f, spell.colour);

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
                DrawRing(ring, hit.point, hit.normal, spell.wallBurstRadius * scale, spell.colour);
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
        void DrawRing(LineRenderer ring, Vector3 centre, Vector3 normal, float radius, Color colour)
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
