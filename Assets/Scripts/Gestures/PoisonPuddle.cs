using System.Collections.Generic;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// The patch of poison the slime lob leaves where it lands. Area denial: it does not stop anyone,
    /// it makes a piece of ground expensive to stand on -- which is worth more in this arena than
    /// damage, because the map is built around running to cover during a blind cast.
    ///
    /// Occupancy is polled with an OverlapSphere rather than a trigger collider. A flat cylinder
    /// primitive comes with a CapsuleCollider that does not match the visual once it is squashed, and
    /// a physical collider here would also block projectiles, which a puddle has no business doing.
    ///
    /// There is no health system yet, so it only reports who is standing in it. Ticks are the hook to
    /// hang damage on the moment there is something to damage.
    /// </summary>
    public class PoisonPuddle : MonoBehaviour
    {
        public float TickInterval = 0.5f;

        float radius;
        float remaining;
        float nextTick;
        Material material;
        Color colour;

        readonly List<Transform> occupants = new List<Transform>();
        public IList<Transform> Occupants { get { return occupants; } }

        public static PoisonPuddle Spawn(Vector3 at, float radius, float lifetime, Color colour)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "PoisonPuddle";
            Destroy(go.GetComponent<Collider>());

            // Drop to the floor under the impact. The glob can hit a wall, a barrier or a pillar, and
            // a flat disc pasted onto a vertical surface looks like a bug -- it is a puddle, it belongs
            // on the ground beneath whatever stopped it.
            Vector3 ground = at;
            RaycastHit hit;
            if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out hit, 12f, ~0, QueryTriggerInteraction.Ignore))
                ground = hit.point;

            // lifted a hair off the floor: coplanar surfaces z-fight and the puddle flickers
            go.transform.position = ground + Vector3.up * 0.03f;
            go.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);

            Material m = RuntimeMaterials.Emissive(colour, 0.9f);
            go.GetComponent<Renderer>().material = m;

            PoisonPuddle p = go.AddComponent<PoisonPuddle>();
            p.radius = radius;
            p.remaining = lifetime;
            p.material = m;
            p.colour = colour;
            return p;
        }

        void Update()
        {
            remaining -= Time.deltaTime;

            // fade out over the last second so it does not vanish mid-fight without warning
            float alpha = Mathf.Clamp01(remaining);
            material.SetColor("_EmissionColor", colour * (0.4f + 0.5f * alpha));

            if (remaining <= 0f) { Destroy(gameObject); return; }

            if (Time.time < nextTick) return;
            nextTick = Time.time + TickInterval;

            occupants.Clear();
            Collider[] inside = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < inside.Length; i++)
            {
                // anything that can be hurt will have a motor on it; scenery will not
                PlayerMotor motor = inside[i].GetComponentInParent<PlayerMotor>();
                if (motor != null && !occupants.Contains(motor.transform)) occupants.Add(motor.transform);
            }

            // TODO: apply damage here once there is health to take it from.
        }
    }
}
