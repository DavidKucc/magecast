using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace MageCast
{
    /// <summary>
    /// A walkable surface for bots, worked out from the map's colliders the first time a bot needs it in
    /// a scene. The maps are generated, so there is nothing baked to load; building it at run time keeps
    /// every map, present and future, playable by bots without a step anybody has to remember.
    ///
    /// Sized for this character: 0.45 m wide, 2 m tall, 0.4 m steps (the controller's step offset),
    /// 45 degree slopes. Ledges that need a jump or a pull-up are not on it -- bots take the stairs.
    /// </summary>
    public static class BotNav
    {
        static NavMeshDataInstance instance;
        static string bakedFor;

        public static void EnsureBaked()
        {
            string scene = SceneManager.GetActiveScene().name;
            if (instance.valid && bakedFor == scene) return;
            if (instance.valid) instance.Remove();

            var bounds = new Bounds(Vector3.zero, new Vector3(240f, 100f, 240f));
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                                          new List<NavMeshBuildMarkup>(), sources);
            // people, dummies and barriers come and go: they are not the map
            sources.RemoveAll(s => s.component != null
                                   && (s.component.GetComponentInParent<PlayerMotor>() != null
                                       || s.component.GetComponentInParent<Combat.TrainingDummy>() != null
                                       || s.component.GetComponentInParent<Gestures.CastShield>() != null));

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.45f;
            settings.agentHeight = 2f;
            settings.agentClimb = 0.4f;
            settings.agentSlope = 45f;
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            instance = NavMesh.AddNavMeshData(data);
            bakedFor = scene;
            Debug.Log("[Bots] walkable surface built for " + scene + " from " + sources.Count + " colliders");
        }

        /// <summary>A path from one point to another, both snapped onto the walkable surface. False if none.</summary>
        public static bool Path(Vector3 from, Vector3 to, NavMeshPath path)
        {
            NavMeshHit a, b;
            if (!NavMesh.SamplePosition(from, out a, 3f, NavMesh.AllAreas)) return false;
            if (!NavMesh.SamplePosition(to, out b, 4f, NavMesh.AllAreas)) return false;
            return NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path)
                   && path.status != NavMeshPathStatus.PathInvalid && path.corners.Length > 0;
        }

        /// <summary>Whether a straight step from here, this far that way, stays on walkable ground.</summary>
        public static bool Open(Vector3 from, Vector3 direction, float distance)
        {
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(from, out hit, 2f, NavMesh.AllAreas)) return false;
            return !NavMesh.Raycast(hit.position, hit.position + direction.normalized * distance, out hit, NavMesh.AllAreas);
        }

        /// <summary>A random walkable point within <paramref name="radius"/> of a centre.</summary>
        public static Vector3 RandomPoint(Vector3 centre, float radius, System.Random rnd)
        {
            for (int i = 0; i < 12; i++)
            {
                Vector3 p = centre + new Vector3(((float)rnd.NextDouble() - 0.5f) * 2f * radius, 0f,
                                                 ((float)rnd.NextDouble() - 0.5f) * 2f * radius);
                NavMeshHit hit;
                if (NavMesh.SamplePosition(p, out hit, 3f, NavMesh.AllAreas)) return hit.position;
            }
            return centre;
        }
    }
}
