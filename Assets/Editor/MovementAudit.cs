using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MageCast.EditorTools
{
    /// <summary>
    /// Times how the body moves: starting, stopping, sprinting, reversing a dodge, turning, jumping,
    /// steering in the air, and how far behind the legs' animation follows. Run it in play mode with a
    /// player in the arena (training is enough): Tools > Arena > Audit Movement.
    ///
    /// Why it matters: a target that changes direction faster than a spell flies cannot be predicted,
    /// and then nothing hits. These numbers say how much "reading" a dodge allows.
    ///
    /// It drives the motor through its scripted-input hooks, frame by frame at 50 Hz, on a stretch of
    /// open floor it finds round the player, and puts the player back where it found them.
    /// </summary>
    public static class MovementAudit
    {
        const float Dt = 1f / 50f;

        [MenuItem("Tools/Arena/Audit Movement")]
        public static void Run() { Debug.Log(Report()); }

        static PlayerMotor motor;
        static ThirdPersonCamera cam;
        static Transform body;
        static Animator animator;
        static Vector3 origin;
        static float yaw;

        public static string Report()
        {
            if (!EditorApplication.isPlaying) return "[Move] enter play mode with a player in the arena first";
            PlayerNet p = PlayerNet.Local;
            if (p == null) return "[Move] no local player yet";
            motor = p.GetComponent<PlayerMotor>();
            cam = Object.FindObjectOfType<ThirdPersonCamera>();
            body = p.transform;
            animator = p.GetComponentInChildren<Animator>();
            Vector3 home = body.position;
            float homeYaw = body.eulerAngles.y;

            bool wasPaused = EditorApplication.isPaused;
            float wasCapture = Time.captureDeltaTime;
            EditorApplication.isPaused = true;
            Time.captureDeltaTime = Dt;
            var sb = new StringBuilder("[Move] movement audit (body speeds, 50 Hz)\n");
            try
            {
                if (!FindLane(home, out origin, out yaw)) return "[Move] no 14 m of open floor near the player -- stand somewhere open";

                float jogTop = motor.WalkSpeedTowards(Vector2.up);
                float strafeTop = motor.WalkSpeedTowards(Vector2.right);

                // 1. start: rest to jog, and how far behind the legs follow
                Reset();
                var speedCurve = new List<float>(); var animCurve = new List<float>();
                Drive(new Vector2(0f, 1f), false, 50, v => speedCurve.Add(v.z), () => animCurve.Add(animator != null ? animator.GetFloat("Speed") : 0f));
                float jogEnd = speedCurve[speedCurve.Count - 1];
                sb.AppendFormat("  start to jog ({0:F1} m/s):     50% in {1:F2} s, 90% in {2:F2} s; {3:F2} m covered in the first 0.5 s\n",
                    jogEnd, Reach(speedCurve, 0.5f * jogEnd), Reach(speedCurve, 0.9f * jogEnd), Distance(speedCurve, 25));
                float animEnd = animCurve[animCurve.Count - 1];
                sb.AppendFormat("  legs follow (Speed param):  90% in {0:F2} s (the body: {1:F2} s)\n",
                    Reach(animCurve, 0.9f * animEnd), Reach(speedCurve, 0.9f * jogEnd));

                // 2. start: rest to sprint
                Reset();
                var sprint = new List<float>();
                Drive(new Vector2(0f, 1f), true, 60, v => sprint.Add(v.z));
                float sprintEnd = sprint[sprint.Count - 1];
                sb.AppendFormat("  start to sprint ({0:F1} m/s):  90% in {1:F2} s\n", sprintEnd, Reach(sprint, 0.9f * sprintEnd));

                // 3. stop from a jog
                Reset();
                Drive(new Vector2(0f, 1f), false, 50, null);
                var stop = new List<float>();
                Vector3 before = body.position;
                Drive(Vector2.zero, false, 40, v => stop.Add(v.z));
                float stopped = Reach(stop, 0.1f * jogEnd, below: true);
                sb.AppendFormat("  stop from a jog:             under 10% in {0:F2} s, slid {1:F2} m\n", stopped,
                    Vector3.Distance(Flat(before), Flat(body.position)));

                // 4. dodge reversal: strafing right flat out, then left
                Reset();
                Drive(new Vector2(1f, 0f), false, 50, null);
                var rev = new List<float>();
                Vector3 flip = body.position; float overshoot = 0f;
                Drive(new Vector2(-1f, 0f), false, 50, v => rev.Add(v.x), () =>
                {
                    float side = Vector3.Dot(body.position - flip, Quaternion.Euler(0f, yaw, 0f) * Vector3.right);
                    overshoot = Mathf.Max(overshoot, side);
                });
                sb.AppendFormat("  dodge reversal ({0:F1} m/s side): stops in {1:F2} s, 90% the other way in {2:F2} s, carried on {3:F2} m first\n",
                    strafeTop, Reach(rev, 0f, below: true), Reach(rev, -0.9f * strafeTop, below: true), overshoot);

                // 5. turn: running forward, then straight to the right
                Reset();
                Drive(new Vector2(0f, 1f), false, 50, null);
                var turnRight = new List<float>();
                Drive(new Vector2(1f, 0f), false, 50, v => turnRight.Add(v.x));
                sb.AppendFormat("  90 degree turn:              90% of the new direction in {0:F2} s\n", Reach(turnRight, 0.9f * strafeTop));

                // 6. jumps: standing, jogging, sprinting
                Reset();
                float air, up, dist;
                Jump(Vector2.zero, false, out air, out up, out dist);
                sb.AppendFormat("  standing jump:               {0:F2} s in the air, {1:F2} m up\n", air, up);
                Reset();
                Drive(new Vector2(0f, 1f), false, 50, null);
                Jump(new Vector2(0f, 1f), false, out air, out up, out dist);
                sb.AppendFormat("  jogging jump:                {0:F2} s in the air, {1:F2} m long\n", air, dist);
                Reset();
                Drive(new Vector2(0f, 1f), true, 60, null);
                Jump(new Vector2(0f, 1f), true, out air, out up, out dist);
                sb.AppendFormat("  sprinting jump:              {0:F2} s in the air, {1:F2} m long\n", air, dist);

                // 7. steering in the air: a jogging jump, pulling back the whole flight
                Reset();
                Drive(new Vector2(0f, 1f), false, 50, null);
                float takeoff = jogEnd, landing = 0f;
                motor.ScriptedJump = true;
                var airSpeeds = new List<float>();
                Drive(new Vector2(0f, -1f), false, 3, v => airSpeeds.Add(v.z));
                int guard = 0;
                while (!motor.IsGrounded && guard++ < 100) Drive(new Vector2(0f, -1f), false, 1, v => landing = v.z);
                sb.AppendFormat("  air steering:                pulling back through a jogging jump: {0:F1} m/s at take-off, {1:F1} m/s on landing\n",
                    takeoff, landing);
            }
            finally
            {
                motor.ScriptedMove = null;
                motor.ScriptedSprint = false;
                motor.TeleportTo(home, homeYaw);
                if (cam != null) cam.SetTarget(body, homeYaw);
                Time.captureDeltaTime = wasCapture;
                EditorApplication.isPaused = wasPaused;
            }
            return sb.ToString().TrimEnd();
        }

        // ------------------------------------------------------------------ helpers

        static void Reset()
        {
            motor.ScriptedMove = Vector2.zero;
            motor.ScriptedSprint = false;
            motor.TeleportTo(origin, yaw);
            if (cam != null) cam.SetTarget(body, yaw);
            for (int i = 0; i < 20; i++) EditorApplication.Step();
        }

        /// <summary>Steps with an input held, reporting the body's velocity in the camera's own axes.</summary>
        static void Drive(Vector2 input, bool sprint, int frames, System.Action<Vector3> velocity, System.Action each = null)
        {
            motor.ScriptedMove = input;
            motor.ScriptedSprint = sprint;
            Quaternion toLocal = Quaternion.Euler(0f, -yaw, 0f);
            for (int i = 0; i < frames; i++)
            {
                Vector3 last = body.position;
                EditorApplication.Step();
                Vector3 v = (body.position - last) / Dt;
                v.y = 0f;
                if (velocity != null) velocity(toLocal * v);
                if (each != null) each();
            }
        }

        static void Jump(Vector2 input, bool sprint, out float air, out float up, out float distance)
        {
            Vector3 start = body.position;
            float top = start.y;
            motor.ScriptedJump = true;
            int frames = 0;
            Drive(input, sprint, 3, null);
            frames += 3;
            while (!motor.IsGrounded && frames < 150)
            {
                Drive(input, sprint, 1, null);
                top = Mathf.Max(top, body.position.y);
                frames++;
            }
            air = frames * Dt;
            up = top - start.y;
            distance = Vector3.Distance(Flat(start), Flat(body.position));
        }

        /// <summary>The time the curve first gets to <paramref name="level"/> (or under it, going down).</summary>
        static float Reach(List<float> curve, float level, bool below = false)
        {
            for (int i = 0; i < curve.Count; i++)
                if (below ? curve[i] <= level : curve[i] >= level) return (i + 1) * Dt;
            return float.PositiveInfinity;
        }

        static float Distance(List<float> curve, int frames)
        {
            float d = 0f;
            for (int i = 0; i < frames && i < curve.Count; i++) d += curve[i] * Dt;
            return d;
        }

        static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }

        /// <summary>A direction with 14 m clear ahead and 6 m clear to each side, from the player or near them.</summary>
        static bool FindLane(Vector3 near, out Vector3 start, out float direction)
        {
            // the player's own spot first, then rings round it; every start stands on a floor, in the open
            var tries = new List<Vector3> { near };
            foreach (float ring in new[] { 5f, 10f, 15f })
                for (float a = 0f; a < 360f; a += 30f)
                    tries.Add(near + Quaternion.Euler(0f, a, 0f) * Vector3.forward * ring);
            foreach (Vector3 guess in tries)
            {
                RaycastHit floor;
                if (!Physics.Raycast(guess + Vector3.up * 6f, Vector3.down, out floor, 12f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (floor.normal.y < 0.8f) continue;
                Vector3 from = floor.point;
                bool inside = false;
                foreach (Collider c in Physics.OverlapCapsule(from + Vector3.up * 0.5f, from + Vector3.up * 1.6f, 0.45f, ~0, QueryTriggerInteraction.Ignore))
                    if (!c.transform.IsChildOf(body)) { inside = true; break; }
                if (inside) continue;
                for (float a = 0f; a < 360f; a += 15f)
                {
                    Quaternion r = Quaternion.Euler(0f, a, 0f);
                    if (Clear(from, r * Vector3.forward, 14f) && Clear(from, r * Vector3.right, 6f) && Clear(from, r * Vector3.left, 6f))
                    {
                        start = from; direction = a;
                        return true;
                    }
                }
            }
            start = near; direction = 0f;
            return false;
        }

        static bool Clear(Vector3 from, Vector3 dir, float length)
        {
            Vector3 a = from + Vector3.up * 0.5f, b = from + Vector3.up * 1.6f;
            foreach (RaycastHit h in Physics.CapsuleCastAll(a, b, 0.45f, dir, length, ~0, QueryTriggerInteraction.Ignore))
                if (!h.transform.IsChildOf(body)) return false;
            return true;
        }
    }
}
