using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MageCast.EditorTools
{
    /// <summary>
    /// Builds the character's AnimatorController from whatever clips the project currently has.
    ///
    /// Written as a re-runnable tool rather than a one-off because the locomotion clips are still
    /// missing: the Vexa pack ships idles, jumps, dashes and a lot of spellcasting, but no walk and no
    /// run at all. The rest of the machine -- the blend tree, the Speed parameter, the wiring into the
    /// motor -- does not depend on those clips existing, so it is built now. Drop any humanoid walk and
    /// run into the project, run this again, and they slot in.
    ///
    /// Clips are found by NAME, so anything called "Walk"/"Run" (Mixamo's default output, for one)
    /// is picked up without further setup. Anything humanoid retargets onto Vexa automatically.
    /// </summary>
    public static class CharacterAnimatorBuilder
    {
        const string ControllerPath = "Assets/Animation/Character.controller";

        /// <summary>
        /// The animation library, which is the part of this project that is meant to outlive the
        /// character wearing it. Drop clips in the folder for their role and this tool wires them up.
        ///
        /// It matters that these live here and not inside a character pack. The model is a skin and is
        /// expected to be replaced; if the idle, the jumps and the casting clips sit inside its folder
        /// then throwing the model away throws the animation away with it, which is backwards.
        /// </summary>
        public const string LocomotionFolder = "Assets/Animation/Locomotion";
        const string IdleFolder = "Assets/Animation/Idle";
        const string JumpFolder = "Assets/Animation/Jump";
        const string CastFolder = "Assets/Animation/Cast";

        /// <summary>
        /// Mixamo's Pro Magic Pack, used by name for a few roles: the barrier's block, the walk with a
        /// spell in hand, and the running jump. Not searched by keyword -- it holds a whole library,
        /// and a keyword search there would hand a role whatever came first.
        /// </summary>
        const string PackFolder = "Assets/Animation/Pro Magic Pack";

        /// <summary>
        /// How fast she moves with a spell in hand, as a share of the jog. Must match
        /// GestureCaster.HoldSpeed, which applies it to the motor; here it sets the walk's playback.
        /// </summary>
        const float HoldPace = 0.52f;

        /// <summary>
        /// A character pack whose clips may be borrowed for roles the library has not filled yet.
        ///
        /// Empty on purpose. There used to be one here, and borrowing from it turned out to cost more
        /// than the gap it filled: a bought character's clips are built on ITS skeleton, so every one of
        /// them arrives retargeted -- measured, 6% off this character's proportions -- while everything
        /// in the library proper wears the character's own avatar and needs no retargeting at all.
        /// Point it at a folder if you ever want that trade again.
        /// </summary>
        const string OnLoanFrom = "";

        /// <summary>Speeds the blend tree is authored against -- these match PlayerMotor.</summary>
        // These must mirror PlayerMotor, including its backward and strafe multipliers -- a blend tree
        // authored at speeds the character never actually reaches would blend two clips forever and
        // never settle on either.
        const float JogSpeed = 4.3f;    // plain WASD

        /// <summary>
        /// Every locomotion clip plays at this share of the rate that would match its stride to the
        /// ground. A matter of taste, asked for after playing: at the matched rate the legs looked
        /// wound up. Below 1 the feet slide a little further -- on top of the pace the motor already
        /// adds over the animation -- so this and PlayerMotor.paceOverAnimation are the two knobs.
        /// </summary>
        const float StridePlayback = 0.85f;
        const float RunSpeed = 6.2f;    // held Shift
        const float BackMultiplier = 0.6f;
        const float StrafeMultiplier = 0.85f;

        /// <summary>
        /// Every movement clip the tree uses, two per direction: one for the jog, one for the run.
        ///
        /// Each is the clip whose OWN stride fits that speed best, so the day somebody drops a "Jog
        /// Backward" into the folder it takes over the slow backpedal without anything being renamed.
        /// Until then a direction with only one clip uses it for both, played slower for the jog.
        /// </summary>
        class LocomotionSet
        {
            public AnimationClip JogFwd, JogBack, JogLeft, JogRight;
            public AnimationClip RunFwd, RunBack, RunLeft, RunRight;

            public static LocomotionSet Resolve()
            {
                var fwd = new List<AnimationClip>();
                var back = new List<AnimationClip>();
                var left = new List<AnimationClip>();
                var right = new List<AnimationClip>();

                if (AssetDatabase.IsValidFolder(LocomotionFolder))
                {
                    var all = new List<AnimationClip>();
                    foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { LocomotionFolder }))
                        Collect(AssetDatabase.GUIDToAssetPath(guid), "", all);

                    // sorted by the FILE name, since Mixamo calls every clip inside "mixamo.com"
                    foreach (AnimationClip c in all)
                    {
                        string file = System.IO.Path.GetFileNameWithoutExtension(
                            AssetDatabase.GetAssetPath(c)).ToLowerInvariant();
                        if (file.Contains("idle") || file.Contains("jump")) continue;
                        if (file.Contains("back")) back.Add(c);
                        else if (file.Contains("left")) left.Add(c);
                        else if (file.Contains("right")) right.Add(c);
                        else if (file.Contains("run") || file.Contains("jog") || file.Contains("walk")
                                 || file.Contains("sprint") || file.Contains("forward")) fwd.Add(c);
                    }
                }

                var s = new LocomotionSet();
                // A clip actually called a jog wins the jog, when there is one. Fit alone cannot tell
                // them apart -- "Run" and "Jog Forward" both travel about 2.5 m/s -- and the two are
                // not equal: "Run" holds the pelvis 10 degrees off to one side for the whole
                // cycle, which reads as walking sideways. Measured in game, not assumed from the name.
                var jogs = fwd.FindAll(c => System.IO.Path.GetFileNameWithoutExtension(
                    AssetDatabase.GetAssetPath(c)).ToLowerInvariant().Contains("jog"));
                // ...but only while it actually fits. A jog played half again as fast is a cartoon, and
                // at W's pace the best-fitting clip is simply the run.
                jogs = jogs.FindAll(c => Mathf.Abs(Mathf.Log(JogSpeed / Mathf.Max(0.1f, MeasureAuthoredSpeed(c)))) < 0.2f);
                s.JogFwd = BestFit(jogs.Count > 0 ? jogs : fwd, JogSpeed);
                s.RunFwd = BestFit(fwd, RunSpeed);
                s.JogBack = BestFit(back, JogSpeed * BackMultiplier);
                s.RunBack = BestFit(back, RunSpeed * BackMultiplier);
                s.JogLeft = BestFit(left, JogSpeed * StrafeMultiplier);
                s.RunLeft = BestFit(left, RunSpeed * StrafeMultiplier);
                s.JogRight = BestFit(right, JogSpeed * StrafeMultiplier);
                s.RunRight = BestFit(right, RunSpeed * StrafeMultiplier);
                return s;
            }
        }

        /// <summary>One direction tree -- four clips on a unit circle, each played at its own stride.</summary>
        static BlendTree BuildMoveTree(AnimatorController ctrl, string name, float speed, AnimationClip idle,
                                       AnimationClip fwd, AnimationClip back, AnimationClip left,
                                       AnimationClip right)
        {
            BlendTree move = new BlendTree();
            move.name = name;
            move.hideFlags = HideFlags.HideInHierarchy;
            move.blendType = BlendTreeType.FreeformDirectional2D;
            move.blendParameter = "DirX";
            move.blendParameterY = "DirZ";
            AssetDatabase.AddObjectToAsset(move, ctrl);

            move.AddChild(fwd != null ? fwd : idle, new Vector2(0f, 1f));
            move.AddChild(back != null ? back : idle, new Vector2(0f, -1f));
            move.AddChild(left != null ? left : idle, new Vector2(-1f, 0f));
            move.AddChild(right != null ? right : idle, new Vector2(1f, 0f));
            SetStride(move, 0, speed);
            SetStride(move, 1, speed * BackMultiplier);
            SetStride(move, 2, speed * StrafeMultiplier);
            SetStride(move, 3, speed * StrafeMultiplier);
            return move;
        }

        /// <summary>
        /// Shifts every clip in a tree so the left foot lands at the same moment of the shared cycle.
        ///
        /// A blend tree plays all its clips at one normalised time. Five separate downloads plant their
        /// feet wherever their animator happened to start the loop -- measured, the jog lands its left
        /// foot 48% of a cycle away from the run -- so blending the two mixes a left step into a right
        /// one and the legs scissor for the length of the blend. Pressing Shift is exactly that blend.
        /// </summary>
        static void AlignFootfall(BlendTree tree, float reference)
        {
            ChildMotion[] children = tree.children;
            for (int i = 0; i < children.Length; i++)
            {
                AnimationClip clip = children[i].motion as AnimationClip;
                if (clip == null) continue;
                float phase = FootDownPhase(clip);
                if (phase < 0f) continue;
                children[i].cycleOffset = Mathf.Repeat(phase - reference, 1f);
            }
            tree.children = children;
        }

        /// <summary>
        /// A running human covers roughly this per step. Used only to sanity-check the measured
        /// authored speeds, not to derive them.
        /// </summary>
        const float PlausibleStride = 1.4f;

        [MenuItem("Tools/Arena/Rebuild Character Animator")]
        public static void Build()
        {
            EnsureFolder();
            PrepareLibrary();

            AnimationClip idle = Role(IdleFolder, "Idle", "idle");
            AnimationClip draw = Role(CastFolder, "HandsSpell", "draw", "channel", "handsspell");
            // Two releases, because putting a barrier up and throwing a bolt are not the same motion.
            // Keywords before the generic ones, so "1h magic attack" claims the file before "attack"
            // could hand it whichever of the two came back first.
            AnimationClip sendAttack = Role(CastFolder, "SnappySpell",
                                            "1h magic attack", "1h", "send", "release", "snappyspell");
            AnimationClip sendShield = Role(CastFolder, "SnappySpell",
                                            "2h magic attack", "2h", "shield", "barrier");

            SetLooping(idle, true);   // stood in for as long as nobody touches a key
            SetLooping(draw, true);   // held while the glyph is being drawn, however long that takes
            sendAttack = TrimToStrike(sendAttack, "SendAttack");

            // The barrier goes up with a block -- raise, then lower -- rather than a thrown spell.
            AnimationClip blockStart = PackClip("Standing Block Start");
            AnimationClip blockEnd = PackClip("Standing Block End");
            SetLooping(blockStart, false);
            SetLooping(blockEnd, false);
            if (blockStart != null) sendShield = blockStart;
            else sendShield = TrimToStrike(sendShield, "SendShield");

            // A spell in hand: she walks instead of jogging.
            AnimationClip walkF = PackClip("Standing Walk Forward"), walkB = PackClip("Standing Walk Back");
            AnimationClip walkL = PackClip("Standing Walk Left"), walkR = PackClip("Standing Walk Right");
            foreach (AnimationClip w in new[] { walkF, walkB, walkL, walkR }) SetLooping(w, true);
            bool canWalk = walkF != null && walkB != null && walkL != null && walkR != null;

            // Only the idle is genuinely required -- it is what every other role falls back to. The rest
            // degrade instead of aborting, so the project still builds and runs while the library is
            // being filled in, and the console says exactly what is missing.
            if (idle == null)
            {
                Debug.LogError("[Animator] no idle clip anywhere. Put one in " + IdleFolder
                               + " -- nothing can be built without a pose to stand in.");
                return;
            }

            if (draw == null)
            {
                draw = idle;
                Debug.LogWarning("[Animator] no channelling clip, so the arms will idle while a glyph is "
                                 + "drawn. Anything with 'draw' or 'channel' in its name in " + CastFolder
                                 + " takes over.");
            }

            bool canCast = sendAttack != null && sendShield != null;
            if (!canCast)
                Debug.LogWarning("[Animator] no cast release clips, so the casting layer is left out "
                                 + "entirely. Spells still fire, the arms just do not throw them.");

            // Searched project-wide and required to be humanoid: a generic clip would silently fail to
            // retarget and the character would just stand still, which is a maddening thing to debug.
            // "backward" is matched before "run", or "Run Backward" would answer a search for "run".
            LocomotionSet loco = LocomotionSet.Resolve();

            if (!AssetDatabase.IsValidFolder("Assets/Animation"))
                AssetDatabase.CreateFolder("Assets", "Animation");

            AssetDatabase.DeleteAsset(ControllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("DirX", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("DirZ", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Airborne", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("JumpBack", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Drawing", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Send", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Defensive", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("LeftHand", AnimatorControllerParameterType.Bool);   // mirrors the send
            ctrl.AddParameter("Hold", AnimatorControllerParameterType.Float);      // 1 = a spell in hand: walk
            ctrl.AddParameter("JumpRun", AnimatorControllerParameterType.Bool);    // took off sprinting

            var sm = ctrl.layers[0].stateMachine;

            // Two trees, not one, and the reason is measured. It used to be a single 2D tree with idle
            // sitting at the origin and the four directions out on the axes. Running diagonally -- which
            // is most of how anybody moves in a fight -- landed at (2.4, 2.8), and the tree resolved that
            // to Idle 0.23, Run 0.41, Right 0.36. Nearly a quarter of the character was standing still
            // while she crossed the arena at full speed, which is what made her look like she was
            // shuffling sideways rather than running.
            //
            // It is not a tuning mistake, it is the shape of the thing: four samples on the axes make a
            // diamond, and every diagonal falls outside it. No arrangement of four clips fixes that.
            //
            // So direction and speed are separated. The inner tree holds only the four movement clips on
            // a unit circle and answers "which way", with no idle in it to bleed through. The outer tree
            // answers "how fast" and is the only place idle appears.
            //
            // Two of those direction trees now, one for the jog on WASD and one for the run on Shift.
            BlendTree jog = BuildMoveTree(ctrl, "Jog", JogSpeed, idle,
                                          loco.JogFwd, loco.JogBack, loco.JogLeft, loco.JogRight);
            BlendTree run = BuildMoveTree(ctrl, "Run", RunSpeed, idle,
                                          loco.RunFwd, loco.RunBack, loco.RunLeft, loco.RunRight);

            // Speed is a FRACTION of what the character can do in the direction she is going, not m/s:
            // 1 is a full jog whether that is 2.6 forward or 1.6 backwards, and 1.54 is the run.
            // Feeding it raw m/s would put a full-speed backpedal at 2.4 and drag idle back in at 40%,
            // which is the same bug in a different costume.
            //
            // And an outer blend on Hold on top of that: with a spell in hand she walks (the pack's walk,
            // at HoldPace of the jog), and letting go of the spell blends back into the free tree.
            BlendTree outer;
            var locomotion = ctrl.CreateBlendTreeInController("Locomotion", out outer, 0);
            outer.blendType = BlendTreeType.Simple1D;
            outer.blendParameter = "Hold";
            outer.useAutomaticThresholds = false;

            BlendTree tree = outer.CreateBlendTreeChild(0f);
            tree.name = "Free";
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;

            if (canWalk)
            {
                BlendTree walk = BuildMoveTree(ctrl, "Walk", JogSpeed * HoldPace, idle, walkF, walkB, walkL, walkR);
                BlendTree held = outer.CreateBlendTreeChild(1f);
                held.name = "Holding";
                held.blendType = BlendTreeType.Simple1D;
                held.blendParameter = "Speed";
                held.useAutomaticThresholds = false;
                held.AddChild(idle, 0f);
                held.AddChild(walk, HoldPace);
            }
            else Debug.LogWarning("[Animator] no walk clips in " + PackFolder + " - holding a spell keeps the jog");

            float footfall = loco.RunFwd != null ? FootDownPhase(loco.RunFwd) : -1f;
            if (footfall >= 0f)
            {
                AlignFootfall(jog, footfall);
                AlignFootfall(run, footfall);
            }

            tree.AddChild(idle, 0f);
            tree.AddChild(jog, 1f);
            tree.AddChild(run, RunSpeed / JogSpeed);

            // Foot IK deliberately left OFF. It looks like the obvious cure for feet that do not sit on
            // the floor, and measured here it made things worse -- the sole went from 9.9 cm through the
            // floor to 12.9 cm. It reproduces where the SOURCE skeleton put the foot, which is no help
            // when the source skeleton is the wrong shape in the first place.
            locomotion.iKOnFeet = false;

            // The IK pass is what lets FootGrounder run at all -- without it Unity never calls
            // OnAnimatorIK and the component sits there doing nothing, silently, which is the failure
            // mode this project keeps meeting.
            AnimatorControllerLayer[] layers = ctrl.layers;
            layers[0].iKPass = true;
            ctrl.layers = layers;

            sm.defaultState = locomotion;

            // --- airborne ---
            //
            // Driven by the motor's own grounded flag rather than by the jump input, so it covers every
            // way of leaving the ground: jumping, walking off a platform, and being blown off your feet
            // by the air spell. A trigger on the jump key would miss the last two entirely.
            // A jump is three separate things and it has to be built as three states. Measured from the
            // clips' own root height, Jump01 goes 1.00 -> 0.72 -> 1.08 (crouch and push off), Jump02 sits
            // flat at 1.03 (hanging in the air) and Jump03 goes 1.03 -> 0.62 -> 0.98 (land and recover).
            //
            // It used to be Jump01 alone, on loop, for the whole flight -- so the character crouched and
            // pushed off over and over while airborne. At 0.30s a clip and roughly 0.70s of hang time
            // that is a bit over two takeoffs per jump, which is exactly what it looked like.
            RepairJumpFiles();
            JumpPhases forward = ResolveJump("Jump", false);
            JumpPhases backward = ResolveJump("JumpBack", true);
            JumpPhases running = ResolveRunningJump();

            if (forward.Complete)
            {
                // Sprinting (Shift) takes off into the pack's running jump. Checked first: the forward
                // chain would otherwise take a sprinting take-off too, since it only asks "not backward".
                if (running.Complete) BuildJumpChain(sm, locomotion, "JumpRun", running, 0, "JumpRun");
                else Debug.LogWarning("[Animator] no running jump - a sprinting take-off uses the plain one");
                // Two chains, because leaping backwards is a different shape from leaping forwards and
                // this character faces the camera while she does both. Which chain runs is latched at
                // the moment she leaves the ground; it must not switch mid-flight.
                // Gate -1 means "whenever airborne": with only one chain there is nothing to choose
                // between, and a condition on JumpBack would strand her running in mid-air.
                BuildJumpChain(sm, locomotion, "Jump", forward, backward.Complete ? 0 : -1);
                if (backward.Complete) BuildJumpChain(sm, locomotion, "JumpBack", backward, 1);
                else Debug.LogWarning("[Animator] no backward jump found - every jump uses the forward one");
            }
            else
            {
                Debug.LogWarning("[Animator] no usable jump clips - the character will keep running in mid-air");
            }

            // --- casting, as an upper-body layer over the top ---
            //
            // It was a full-body state before, which meant the legs stopped dead the moment you started
            // drawing: you would slide across the arena in a standing pose, during the one phase of the
            // game you spend the most time in. Masking it to the upper body lets the run keep running
            // underneath while the arms do the spell.
            if (canCast)
            {
            AvatarMask mask = BuildUpperBodyMask();

            var castSm = new AnimatorStateMachine();
            castSm.name = "Cast";
            castSm.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(castSm, ctrl);

            var sDraw = castSm.AddState("Draw"); sDraw.motion = draw;
            castSm.defaultState = sDraw;

            BuildSend(castSm, sDraw, "SendAttack", sendAttack, false);
            AnimatorState shield = BuildSend(castSm, sDraw, "SendShield", sendShield, true);
            if (blockEnd != null && blockStart != null)
            {
                // the block goes up, then comes down: the raise hands over to the lowering, which then
                // goes back under the drawing pose
                var lower = castSm.AddState("BlockEnd");
                lower.motion = blockEnd;
                lower.mirrorParameterActive = true;
                lower.mirrorParameter = "LeftHand";
                foreach (AnimatorStateTransition t in shield.transitions) shield.RemoveTransition(t);
                var up = shield.AddTransition(lower);
                up.hasExitTime = true; up.exitTime = 0.95f; up.duration = 0.08f;
                var down = lower.AddTransition(sDraw);
                down.hasExitTime = true; down.exitTime = 0.85f; down.duration = 0.15f;
            }

            ctrl.AddLayer(new AnimatorControllerLayer
            {
                name = "Cast",
                stateMachine = castSm,
                avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 0f,      // PlayerAnimation fades it in; an always-on layer would freeze the arms
            });
            }

            AssetDatabase.SaveAssets();
            Reconnect(ctrl);

            // Shouted at build time and not only from the audit menu, because a proportion mismatch does
            // not look like a rig problem when you play it -- it looks like bad animation, and you go
            // hunting through blend trees and playback rates instead. Which is exactly what happened.
            var shape = new System.Text.StringBuilder();
            if (AuditProportions(shape) > 0) Debug.LogWarning("[Animator] " + shape);

            Debug.Log(string.Format(
                "[Animator] rebuilt on skin '{0}'.\n  forward: {1}\n  back:    {2}\n  left:    {3}\n"
                + "  right:   {4}\n  (each: jog clip | run clip)\n  idle:    {6}{5}\n"
                + "  jump fwd:  {7} / {8} / {9}\n  jump back: {10} / {11} / {12}\n"
                + "  cast:    {13}\n           attack {14}\n           shield {15}",
                CharacterSkin.Describe(CharacterSkin.Prefab()),
                Describe(loco.JogFwd) + " | " + Describe(loco.RunFwd),
                Describe(loco.JogBack) + " | " + Describe(loco.RunBack),
                Describe(loco.JogLeft) + " | " + Describe(loco.RunLeft),
                Describe(loco.JogRight) + " | " + Describe(loco.RunRight),
                "",
                Describe(idle),
                Describe(forward.Up), Describe(forward.Air), Describe(forward.Land),
                Describe(backward.Up), Describe(backward.Air), Describe(backward.Land),
                Describe(draw), Describe(sendAttack), Describe(sendShield)));

            WarnAboutRootMotion(loco.JogFwd, loco.RunFwd, loco.RunBack, loco.RunLeft, loco.RunRight);
        }

        /// <summary>
        /// Points anything already in the scene at the controller that was just built.
        ///
        /// The rebuild deletes the asset and creates it again, which hands it a new GUID, so every
        /// Animator referencing the old one is left holding nothing. The character then stands in a
        /// bind pose with no error anywhere -- exactly the sort of silent failure this file exists to
        /// stop -- until somebody thinks to respawn the rig.
        /// </summary>
        static void Reconnect(AnimatorController ctrl)
        {
            ReconnectPrefab(ctrl);

            int fixedUp = 0;
            foreach (Animator a in Object.FindObjectsOfType<Animator>())
            {
                if (a.GetComponentInParent<PlayerAnimation>() == null) continue;
                if (a.runtimeAnimatorController == ctrl) continue;
                a.runtimeAnimatorController = ctrl;
                EditorUtility.SetDirty(a);
                fixedUp++;
            }
            if (fixedUp == 0) return;

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[Animator] reconnected " + fixedUp + " animator(s) in the open scene");
        }

        /// <summary>
        /// The same repair for the player prefab, which is where the character actually lives now --
        /// the network spawns every player from it. Fixing only the scene left the prefab pointing at
        /// the deleted controller, and every spawned player stood frozen in its bind pose: found when
        /// a skating test showed the feet not moving at all.
        /// </summary>
        static void ReconnectPrefab(AnimatorController ctrl)
        {
            const string PrefabPath = "Assets/Prefabs/Player.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                int fixedUp = 0;
                foreach (Animator a in root.GetComponentsInChildren<Animator>(true))
                {
                    if (a.runtimeAnimatorController == ctrl) continue;
                    a.runtimeAnimatorController = ctrl;
                    fixedUp++;
                }
                if (fixedUp == 0) return;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[Animator] reconnected the player prefab to the rebuilt controller");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);

                // Forced, even when the file was already right. The controller was deleted and made
                // again under the prefab's feet, and the prefab Unity keeps in memory went on holding
                // the dead one -- the file on disk was correct and play mode still spawned players
                // with no controller at all, until the prefab was reimported.
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            }
        }

        /// <summary>
        /// Measures every clip the animator is about to use and reports the ones that will not behave.
        ///
        /// This exists because clips get added from all over -- a bought pack for the core locomotion,
        /// single downloads for one-off actions -- and the failures are all silent. A clip that is not
        /// humanoid does not retarget and the character just stands there. A cycle with Loop Time off
        /// clamps to its last frame, which for a run is nearly its first, so it looks frozen while the
        /// animator insists it is playing. Neither errors, neither warns. So they get measured instead.
        /// </summary>
        [MenuItem("Tools/Arena/Audit Animation Library")]
        public static void Audit()
        {
            LocomotionSet loco = LocomotionSet.Resolve();

            var report = new System.Text.StringBuilder();
            report.AppendLine("[Animation] library audit -- skin '"
                              + CharacterSkin.Describe(CharacterSkin.Prefab()) + "'");
            report.AppendLine();
            report.AppendLine("BLENDED SET -- these prolong into each other, so they want one source:");

            int problems = 0;
            problems += AuditCycle(report, "jog fwd", loco.JogFwd, JogSpeed);
            problems += AuditCycle(report, "jog back", loco.JogBack, JogSpeed * BackMultiplier);
            problems += AuditCycle(report, "jog L", loco.JogLeft, JogSpeed * StrafeMultiplier);
            problems += AuditCycle(report, "jog R", loco.JogRight, JogSpeed * StrafeMultiplier);
            problems += AuditCycle(report, "run fwd", loco.RunFwd, RunSpeed);
            problems += AuditCycle(report, "run back", loco.RunBack, RunSpeed * BackMultiplier);
            problems += AuditCycle(report, "run L", loco.RunLeft, RunSpeed * StrafeMultiplier);
            problems += AuditCycle(report, "run R", loco.RunRight, RunSpeed * StrafeMultiplier);

            problems += AuditFootPhase(report, loco.RunFwd, loco.JogFwd, loco.RunBack, loco.RunLeft, loco.RunRight);
            problems += AuditProportions(report);

            report.AppendLine();
            report.AppendLine("STANDALONE -- own state, never blended, so any source is fine:");
            problems += AuditAction(report, "idle", Role(IdleFolder, "Idle", "idle"), true);

            JumpPhases fwd = ResolveJump("Jump", false);
            JumpPhases bwd = ResolveJump("JumpBack", true);
            problems += AuditAction(report, "jump up", fwd.Up, false);
            problems += AuditAction(report, "jump air", fwd.Air, true);
            problems += AuditAction(report, "jump land", fwd.Land, false);
            if (bwd.Complete)
            {
                problems += AuditAction(report, "back up", bwd.Up, false);
                problems += AuditAction(report, "back air", bwd.Air, true);
                problems += AuditAction(report, "back land", bwd.Land, false);
            }
            else report.AppendLine("  back jump   none -- backing away will use the forward jump");
            problems += AuditAction(report, "cast draw",
                Role(CastFolder, "HandsSpell", "draw", "channel", "handsspell"), true);
            problems += AuditAction(report, "send atk",
                Role(CastFolder, "SnappySpell", "1h magic attack", "1h", "send", "release", "snappyspell"), false);
            problems += AuditAction(report, "send shield",
                Role(CastFolder, "SnappySpell", "2h magic attack", "2h", "shield", "barrier"), false);

            report.AppendLine();
            report.AppendLine(problems == 0
                ? "nothing to fix"
                : problems + " thing(s) worth looking at, marked <-- above");

            if (problems == 0) Debug.Log(report.ToString());
            else Debug.LogWarning(report.ToString());
        }

        /// <summary>
        /// Whether the skeletons the animations were made on are the same shape as the character.
        ///
        /// This is the check that would have saved a whole evening. Humanoid retargeting copies joint
        /// ANGLES, so it only lands the feet in the right place if the two skeletons have roughly the
        /// same proportions. Measured on the first set through here, the animation rig's legs were 41%
        /// of its height and the character's were 57% -- and the feet went 10 cm through the floor and
        /// lost 18% of their stride, which reads as scuffing and dragging no matter what else is right.
        ///
        /// Mixamo does not make you live with this: pick the character first, then download animations,
        /// and every clip comes baked onto that character's own skeleton with nothing to retarget.
        /// </summary>
        /// <summary>
        /// Where in each clip's cycle the feet actually touch down.
        ///
        /// Unity blends the children of a tree at the SAME normalized time. If one clip plants its left
        /// foot a fifth of the way in and another plants it two thirds of the way in, blending them puts
        /// the character halfway between planted and lifted on both legs at once -- which does not read
        /// as walking, it reads as shuffling.
        ///
        /// A bought locomotion set has these aligned by the animator. Five separate downloads have no
        /// reason to be, and nothing in Unity warns about it. If the numbers below disagree, that is
        /// fixable for free with a per-clip cycle offset -- no clips to re-download, no trade-off.
        /// </summary>
        static int AuditFootPhase(System.Text.StringBuilder report, AnimationClip run, AnimationClip sprint,
                                  AnimationClip back, AnimationClip left, AnimationClip right)
        {
            report.AppendLine();
            report.AppendLine("FOOTFALL TIMING -- blended clips must plant their feet at the same moment:");

            var clips = new[] { run, sprint, back, left, right };
            var labels = new[] { "run fwd", "jog fwd", "backward", "strafe L", "strafe R" };

            float reference = -1f;
            float lowest = 2f, highest = -1f;
            int measured = 0;

            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null) continue;

                float phase = FootDownPhase(clips[i]);
                if (phase < 0f) continue;

                if (reference < 0f) reference = phase;
                if (phase < lowest) lowest = phase;
                if (phase > highest) highest = phase;
                measured++;

                float drift = Mathf.DeltaAngle(reference * 360f, phase * 360f) / 360f;
                report.AppendLine("  " + labels[i].PadRight(11)
                    + "left foot lands at " + phase.ToString("P0").PadLeft(5) + " of the cycle"
                    + (i == 0 ? "   (the reference)"
                              : "   off by " + drift.ToString("P0")
                                + (Mathf.Abs(drift) > 0.10f ? "  <-- shuffles when blended with forward" : "")));
            }

            if (measured < 2) return 0;

            float spread = highest - lowest;
            report.AppendLine("  spread across the set: " + spread.ToString("P0")
                + (spread > 0.15f
                    ? "  <-- worth correcting with a cycle offset per clip"
                    : "  ok, they land together"));
            return spread > 0.15f ? 1 : 0;
        }

        /// <summary>
        /// The fraction of the way through a clip at which the left foot is at its lowest -- the moment
        /// it takes the character's weight.
        /// </summary>
        static float FootDownPhase(AnimationClip clip)
        {
            GameObject probe = SpawnProbe();
            if (probe == null) return -1f;

            Animator animator = probe.GetComponent<Animator>();
            Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (foot == null || hips == null) { Object.DestroyImmediate(probe); return -1f; }

            const int Steps = 60;
            float best = float.MaxValue, bestAt = 0f;
            for (int i = 0; i < Steps; i++)
            {
                float t = i / (float)Steps;
                probe.transform.position = Vector3.zero;
                probe.transform.rotation = Quaternion.identity;
                clip.SampleAnimation(probe, clip.length * t);

                // measured against the hips, so a clip whose whole body sits lower does not skew it
                float height = foot.position.y - hips.position.y;
                if (height < best) { best = height; bestAt = t; }
            }

            Object.DestroyImmediate(probe);
            return bestAt;
        }

        static int AuditProportions(System.Text.StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("SKELETON SHAPE -- retargeting only works between similar proportions:");

            GameObject prefab = CharacterSkin.Prefab();
            if (prefab == null) { report.AppendLine("  no skin installed"); return 1; }

            float skinRatio = LegRatio(prefab);
            if (skinRatio <= 0f) { report.AppendLine("  could not measure the skin"); return 1; }
            report.AppendLine("  " + prefab.name.PadRight(26) + "legs are "
                              + skinRatio.ToString("P0") + " of standing height  (the character)");

            int problems = 0;
            if (!AssetDatabase.IsValidFolder(LocomotionFolder)) return 0;

            Avatar skinAvatar = CharacterSkin.AvatarFor(prefab);

            foreach (string folder in new[] { LocomotionFolder, IdleFolder, JumpFolder, CastFolder })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;

                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                    string name = System.IO.Path.GetFileName(path);

                    // Wearing the character's own avatar means no retargeting happens at all, so what
                    // shape the file's own skeleton is stops mattering entirely.
                    if (imp != null && imp.avatarSetup == ModelImporterAvatarSetup.CopyFromOther
                        && imp.sourceAvatar == skinAvatar)
                    {
                        report.AppendLine("  " + name.PadRight(34)
                                          + "wears the character's own avatar - nothing to retarget");
                        continue;
                    }

                    float ratio = LegRatio(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    if (ratio <= 0f) continue;

                    float off = Mathf.Abs(ratio - skinRatio) / skinRatio;
                    report.AppendLine("  " + name.PadRight(34) + "retargeted, legs are "
                        + ratio.ToString("P0") + " of height"
                        + (off > 0.08f
                            ? "  <-- " + off.ToString("P0") + " off. Its skeleton does not match this "
                              + "character, so the feet will not land where they should."
                            : "  ok"));
                    if (off > 0.08f) problems++;
                }
            }
            return problems;
        }

        /// <summary>
        /// Leg length as a fraction of standing height, measured along the bone chains so the answer
        /// does not depend on what pose the model happens to be in.
        /// </summary>
        static float LegRatio(GameObject prefab)
        {
            if (prefab == null) return -1f;

            GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            probe.hideFlags = HideFlags.HideAndDontSave;

            Animator a = probe.GetComponentInChildren<Animator>();
            if (a == null) a = probe.AddComponent<Animator>();
            if (a.avatar == null || !a.avatar.isHuman) a.avatar = CharacterSkin.AvatarFor(prefab);

            float ratio = -1f;
            if (a.isHuman)
            {
                Transform upper = a.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                Transform lower = a.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                Transform foot = a.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform hips = a.GetBoneTransform(HumanBodyBones.Hips);
                Transform head = a.GetBoneTransform(HumanBodyBones.Head);

                if (upper != null && lower != null && foot != null && hips != null && head != null)
                {
                    float leg = Vector3.Distance(upper.position, lower.position)
                              + Vector3.Distance(lower.position, foot.position);
                    float torso = Vector3.Distance(hips.position, head.position);
                    if (leg + torso > 0.01f) ratio = leg / (leg + torso);
                }
            }
            Object.DestroyImmediate(probe);
            return ratio;
        }

        static int AuditCycle(System.Text.StringBuilder report, string role, AnimationClip clip, float wanted)
        {
            if (clip == null)
            {
                report.AppendLine("  " + role.PadRight(11) + " MISSING  <-- idle stands in for it");
                return 1;
            }

            string file = System.IO.Path.GetFileName(AssetDatabase.GetAssetPath(clip));
            float authored = MeasureAuthoredSpeed(clip);
            float rate = authored > 0.2f ? wanted / authored : 1f;
            float travel = new Vector2(clip.averageSpeed.x, clip.averageSpeed.z).magnitude;

            var note = new System.Text.StringBuilder();
            if (!clip.isHumanMotion) note.Append("NOT HUMANOID, will not retarget. ");
            if (!clip.isLooping) note.Append("Loop Time is off, a cycle needs it on. ");
            if (travel > 0.35f) note.Append("travels " + travel.ToString("F1") + " m/s of its own, "
                                            + "re-download it In Place. ");
            // Outside this band the clip is being pushed to a speed it was never made for and starts to
            // read as fast-forward or slow motion, whichever way it is wrong.
            if (rate < 0.8f || rate > 1.25f)
                note.Append("authored for " + authored.ToString("F1") + " m/s but used at "
                            + wanted.ToString("F1") + ". ");

            // The cycle as it is ACTUALLY played, which is what the eye sees as the rhythm of footfalls.
            // Two clips blended together are synchronised by Unity, so if these differ across the set,
            // the step rhythm changes as the character turns.
            float cycle = rate > 0.01f ? clip.length / rate : clip.length;

            report.AppendLine("  " + role.PadRight(11) + file.PadRight(22)
                              + clip.length.ToString("F2") + "s  "
                              + authored.ToString("F1").PadLeft(4) + " m/s authored  x"
                              + rate.ToString("F2") + " playback   cycle "
                              + cycle.ToString("F2") + "s"
                              + (note.Length > 0 ? "  <-- " + note : ""));
            return note.Length > 0 ? 1 : 0;
        }

        static int AuditAction(System.Text.StringBuilder report, string role, AnimationClip clip, bool shouldLoop)
        {
            if (clip == null)
            {
                report.AppendLine("  " + role.PadRight(11) + " MISSING  <--");
                return 1;
            }

            string file = System.IO.Path.GetFileName(AssetDatabase.GetAssetPath(clip));
            var note = new System.Text.StringBuilder();
            if (!clip.isHumanMotion) note.Append("NOT HUMANOID, will not retarget. ");
            if (clip.isLooping != shouldLoop)
                note.Append(shouldLoop
                    ? "held pose, wants Loop Time on. "
                    : "one-shot, but Loop Time is on -- it will repeat while the state lasts. ");

            report.AppendLine("  " + role.PadRight(11) + file.PadRight(22)
                              + clip.length.ToString("F2") + "s"
                              + (note.Length > 0 ? "  <-- " + note : ""));
            return note.Length > 0 ? 1 : 0;
        }

        static string Describe(AnimationClip c)
        {
            if (c == null) return "MISSING - standing in with Idle";
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                 "{0}  ({1:F2}s, from {2})", c.name, c.length,
                                 System.IO.Path.GetFileName(AssetDatabase.GetAssetPath(c)));
        }

        /// <summary>
        /// Root motion off means the motor owns movement, so a clip that travels on its own will look
        /// like skating -- the legs cycle for a speed the body is not going. Mixamo calls the fix
        /// "In Place", and it is easy to forget on one download out of five.
        /// </summary>
        static void WarnAboutRootMotion(params AnimationClip[] clips)
        {
            foreach (AnimationClip c in clips)
            {
                if (c == null) continue;
                float travel = new Vector2(c.averageSpeed.x, c.averageSpeed.z).magnitude;
                if (travel > 0.35f)
                    Debug.LogWarning(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "[Animator] '{0}' travels at {1:F2} m/s of its own -- it was not downloaded " +
                        "In Place, so the feet will skate. Re-download it with In Place ticked.",
                        c.name, travel));
            }
        }

        /// <summary>
        /// Plays one clip at the rate its own stride implies for the speed it is meant to represent.
        ///
        /// Without this the feet skate, badly. Measured on the downloaded set: Run is authored for
        /// 3.8 m/s but plays at 6, Run Backward for 3.0 but plays at 6 -- the legs were cycling at half
        /// the rate the body was travelling, which is exactly the "something is wrong but I cannot say
        /// what" look. Correcting playback rate fixes it without touching movement speed, which is set
        /// by the map's cover distances and is not the animator's business to override.
        /// </summary>
        /// <summary>
        /// How long after the button the spell should visibly leave the hand.
        ///
        /// The gesture IS the wind-up in this game -- you have already spent a second drawing -- so the
        /// release has to be a snap, not another wind-up. The downloaded attacks are 2.3 and 2.7 seconds
        /// of full mocap swing, most of which is recovery back to an idle this character never returns to.
        /// </summary>
        struct JumpPhases
        {
            public AnimationClip Up, Air, Land;
            public bool Complete { get { return Up != null && Air != null && Land != null; } }
        }

        /// <summary>
        /// The three phases of one jump, sliced out of a single downloaded clip if need be.
        ///
        /// A download is normally one complete jump -- crouch, rise, apex, fall, land, settle -- but the
        /// game needs the three parts separately, because how long the character is off the ground is
        /// decided by the motor and not by whoever exported the animation. Walking off a ledge has to
        /// hang for as long as the fall lasts.
        /// </summary>
        /// <summary>
        /// Puts back any jump file whose split is not a clean Up/Air/Land, so it can be sliced again.
        ///
        /// Without this a bad split is permanent: a half-sliced file no longer holds the single take a
        /// slicer needs to measure, so it stops being recognised as a source and quietly falls back to
        /// the borrowed clips forever. Self-healing beats having to remember to reset it by hand.
        /// </summary>
        static void RepairJumpFiles()
        {
            if (!AssetDatabase.IsValidFolder(JumpFolder)) return;

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { JumpFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;

                ModelImporterClipAnimation[] cas = imp.clipAnimations;
                if (cas == null || cas.Length == 0) continue;    // untouched, still a usable source

                bool valid = cas.Length == 3
                             && cas[0].name.EndsWith("Up")
                             && cas[1].name.EndsWith("Air")
                             && cas[2].name.EndsWith("Land")
                             && cas[0].name != cas[1].name
                             && cas[1].name != cas[2].name;

                // and the slices have to be in place. A clip carrying the leap itself is not usable
                // however it is cut up, because the CharacterController is already doing the leaping.
                if (valid)
                    foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        AnimationClip c = o as AnimationClip;
                        if (c != null && !c.name.StartsWith("__") && RootRangeY(c) > MaxInPlaceRise) valid = false;
                    }
                if (valid) continue;

                imp.clipAnimations = new ModelImporterClipAnimation[0];
                imp.SaveAndReimport();
                Debug.Log("[Animator] reset " + System.IO.Path.GetFileName(path)
                          + " - its split was not a clean Up/Air/Land, so it can be sliced again");
            }
        }

        static JumpPhases ResolveJump(string prefix, bool wantBackward)
        {
            var phases = new JumpPhases();
            phases.Up = FindClipNamed(JumpFolder, prefix + "Up");
            phases.Air = FindClipNamed(JumpFolder, prefix + "Air");
            phases.Land = FindClipNamed(JumpFolder, prefix + "Land");
            if (phases.Complete) return phases;      // sliced on an earlier run

            // Three separate in-place clips, which is the shape a controller-driven jump actually wants
            // and what Mixamo hands over as Jump Up / Falling Idle / Falling To Landing. Tried before
            // slicing, because a set that is already in three parts needs no cutting up.
            if (!wantBackward)
            {
                // "jumping" as well as "jump up", because Mixamo calls it Jumping Up and the two do not
                // share a substring -- a keyword list is only as good as the names it has actually seen.
                AnimationClip up = Role(JumpFolder, null,
                                        "jumping up", "jump up", "jumpup", "jumping", "takeoff", "launch");
                AnimationClip air = Role(JumpFolder, null,
                                         "falling idle", "fallingidle", "hang", "midair", "falling");
                AnimationClip land = Role(JumpFolder, null, "landing", "touchdown", "land");

                // Cut each phase down to the part that happens on the ground.
                //
                // A downloaded "Jumping Up" carries the whole leap -- its root climbs 1.02 m -- but the
                // climb is all in the back half; the front half is a 0.33 m crouch and push, which is
                // real animation and belongs in the pose. Same story reversed for the landing. So the
                // in-place rule is applied to the SLICE rather than the file, and both become usable.
                up = TrimJumpPhase(up, "JumpUp", true);
                land = TrimJumpPhase(land, "JumpLand", false);

                if (up != null && air != null && land != null && up != air && air != land && up != land)
                {
                    // same rule as for a complete jump: the motor owns how high she goes
                    float rise = Mathf.Max(RootRangeY(up), Mathf.Max(RootRangeY(air), RootRangeY(land)));
                    if (rise <= MaxInPlaceRise)
                    {
                        SetLooping(up, false);
                        SetLooping(air, LoopsCleanly(air));
                        SetLooping(land, false);
                        phases.Up = up; phases.Air = air; phases.Land = land;
                        return phases;
                    }
                    Debug.LogWarning(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "[Animator] the jump phases lift their root {0:F2} m between them, so one of them "
                        + "still carries the leap the CharacterController is already doing. Re-download "
                        + "that one with In Place ticked.", rise));
                }
            }

            string source = FindJumpSource(wantBackward);
            if (source != null && SliceJump(source, prefix, ref phases)) return phases;

            // nothing of our own: borrow the three-part jump the character pack happens to ship
            if (wantBackward) return phases;
            phases.Up = FindClip("Jump01", OnLoanFrom);
            phases.Air = FindClip("Jump02", OnLoanFrom);
            phases.Land = FindClip("Jump03", OnLoanFrom);
            SetLooping(phases.Up, false);
            SetLooping(phases.Air, LoopsCleanly(phases.Air));   // asked, not assumed
            SetLooping(phases.Land, false);
            return phases;
        }

        /// <summary>
        /// Which downloaded jump is the backward one, decided by where its root actually goes.
        ///
        /// Measured rather than read off the file name, because the two arrived called "Jump" and
        /// "Jump (1)" -- the browser numbered them, nothing else. The backward one travels -2.4 m.
        /// </summary>
        static string FindJumpSource(bool wantBackward)
        {
            string best = null;
            float bestTravel = wantBackward ? -0.5f : float.MaxValue;

            if (!AssetDatabase.IsValidFolder(JumpFolder)) return null;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { JumpFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AnimationClip clip = SingleClip(path);
                if (clip == null || !clip.isHumanMotion) continue;

                // A jump clip has to be in place, like the run cycles are. One that carries the leap
                // puts the rise into the pose, and no import setting takes it back out -- measured, all
                // four combinations of Bake Into Pose and Based Upon leave it there or break something
                // worse. The motor owns how high she goes; the animation only owns what she looks like.
                float rise = RootRangeY(clip);
                if (rise > MaxInPlaceRise)
                {
                    Debug.LogWarning(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "[Animator] '{0}' lifts its own root {1:F2} m, so it is a complete jump rather "
                        + "than an in-place phase, and using it would jump twice as high as intended. "
                        + "Skipped. What this needs is the three in-place clips -- Mixamo calls them "
                        + "Jump Up, Falling Idle and Falling To Landing -- with In Place ticked.",
                        System.IO.Path.GetFileName(path), rise));
                    continue;
                }

                float travel = RootTravelZ(clip);
                if (wantBackward) { if (travel < bestTravel) { bestTravel = travel; best = path; } }
                else if (Mathf.Abs(travel) < bestTravel) { bestTravel = Mathf.Abs(travel); best = path; }
            }
            return best;
        }

        /// <summary>
        /// The pack's running jump (Jump (2)), sliced into take-off, hang and landing like the others.
        /// It carries its own leap -- the root rises 1.1 m and travels 4 m -- which the slices leave out
        /// of the pose (see Slice): the CharacterController does the leaping.
        /// </summary>
        static JumpPhases ResolveRunningJump()
        {
            var phases = new JumpPhases();
            phases.Up = FindClipNamed(PackFolder, "JumpRunUp");
            phases.Air = FindClipNamed(PackFolder, "JumpRunAir");
            phases.Land = FindClipNamed(PackFolder, "JumpRunLand");
            if (phases.Complete) return phases;

            string path = PackFolder + "/Jump (2).fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return phases;
            SliceJump(path, "JumpRun", ref phases, PackFolder);
            return phases;
        }

        static bool SliceJump(string path, string prefix, ref JumpPhases phases, string folder = JumpFolder)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) return false;

            AnimationClip clip = SingleClip(path);
            if (clip == null)
            {
                Debug.LogWarning("[Animator] " + System.IO.Path.GetFileName(path)
                                 + " does not hold exactly one clip, so it cannot be sliced into phases");
                return false;
            }

            ModelImporterClipAnimation[] existing = imp.defaultClipAnimations;
            if (existing == null || existing.Length == 0) return false;

            AnimationCurve height = RootCurve(clip, "RootT.y");
            if (height == null)
            {
                Debug.LogWarning("[Animator] " + System.IO.Path.GetFileName(path)
                                 + " has no root height curve - is it set to Humanoid?");
                return false;
            }

            float fps = clip.frameRate > 1f ? clip.frameRate : 30f;
            int frames = Mathf.RoundToInt(clip.length * fps);
            int first = Mathf.RoundToInt(existing[0].firstFrame);

            float ground = height.Evaluate(0f);
            int apex = 0; float highest = float.MinValue;
            for (int i = 0; i <= frames; i++)
            {
                float y = height.Evaluate(i / fps);
                if (y > highest) { highest = y; apex = i; }
            }

            // touchdown: the first frame past the apex back down at the height she started from
            int touchdown = frames;
            for (int i = apex; i <= frames; i++)
                if (height.Evaluate(i / fps) <= ground + 0.06f) { touchdown = i; break; }

            if (apex < 2 || touchdown <= apex + 1)
            {
                Debug.LogWarning(string.Format(
                    "[Animator] {0} does not read as a jump - apex at frame {1} of {2}, touchdown {3}. "
                    + "It needs to leave the ground and come back down in one clip.",
                    System.IO.Path.GetFileName(path), apex, frames, touchdown));
                return false;
            }

            var slices = new List<ModelImporterClipAnimation>();
            slices.Add(Slice(existing[0], prefix + "Up", first, first + apex, false));
            // a two-frame hold at the apex, not a slice of the arc: the fall lasts as long as the motor
            // says, and looping any real movement here would show as a twitch every fifth of a second
            slices.Add(Slice(existing[0], prefix + "Air", first + apex, first + apex + 1, true));
            slices.Add(Slice(existing[0], prefix + "Land", first + touchdown, first + frames, false));

            imp.clipAnimations = slices.ToArray();
            imp.SaveAndReimport();

            phases.Up = FindClipNamed(folder, prefix + "Up");
            phases.Air = FindClipNamed(folder, prefix + "Air");
            phases.Land = FindClipNamed(folder, prefix + "Land");

            Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "[Animator] {0}: sliced {1} ({2} frames) -- rise 0-{3}, apex {3}, land {4}-{2}",
                prefix, System.IO.Path.GetFileName(path), frames, apex, touchdown));

            return phases.Complete;
        }

        /// <summary>
        /// A fresh clip definition copied from the file's own take.
        ///
        /// Built new every time rather than copied from the source, because ModelImporterClipAnimation
        /// is a class: assigning it hands out another reference to the same object, so three "copies"
        /// were three views of one clip and only the last name survived.
        /// </summary>
        /// <summary>
        /// Trims a downloaded jump phase to the part where the feet are still on the floor.
        ///
        /// The clip's own root height says where that is. A takeoff dips into a crouch and then climbs
        /// away; everything up to the moment it climbs back past where it started is push-off, and the
        /// rest is the leap the CharacterController is already doing. A landing is the same in reverse:
        /// the useful part starts at the lowest point, which is the impact.
        ///
        /// Left alone if the clip is already in place, and idempotent -- the rename is what marks it done.
        /// </summary>
        static AnimationClip TrimJumpPhase(AnimationClip clip, string roleName, bool takeoff)
        {
            if (clip == null) return null;
            if (clip.name == roleName) return clip;                  // trimmed on an earlier run
            if (RootRangeY(clip) <= MaxInPlaceRise) return clip;     // already in place, nothing to cut

            string path = AssetDatabase.GetAssetPath(clip);
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) return clip;

            ModelImporterClipAnimation[] source = imp.defaultClipAnimations;
            if (source == null || source.Length == 0) return clip;

            AnimationCurve height = RootCurve(clip, "RootT.y");
            if (height == null) return clip;

            float fps = clip.frameRate > 1f ? clip.frameRate : 30f;
            int frames = Mathf.RoundToInt(clip.length * fps);
            int offset = Mathf.RoundToInt(source[0].firstFrame);
            float startHeight = height.Evaluate(0f);
            float wasMoving = RootRangeY(clip);   // read now; the reimport below repoints this reference

            int dip = 0;
            float lowest = float.MaxValue;
            for (int i = 0; i <= frames; i++)
            {
                float y = height.Evaluate(i / fps);
                if (y < lowest) { lowest = y; dip = i; }
            }

            int first, last;
            if (takeoff)
            {
                first = 0;
                last = frames;
                for (int i = dip; i <= frames; i++)
                    if (height.Evaluate(i / fps) >= startHeight) { last = i; break; }
            }
            else
            {
                // a couple of frames before impact, so the foot is seen arriving rather than already down
                first = Mathf.Max(0, dip - 2);
                last = frames;
            }

            if (last - first < 3) return clip;

            // Both trimmed phases happen with the feet on the floor, so the vertical belongs in the pose.
            imp.clipAnimations = new[] { Slice(source[0], roleName, offset + first, offset + last,
                                               false, true) };
            imp.SaveAndReimport();

            AnimationClip result = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                AnimationClip c = o as AnimationClip;
                if (c != null && c.name == roleName) result = c;
            }

            Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "[Animator] {0}: cut {1} to frames {2}-{3} of {4}; root now moves {5:F2} m instead of {6:F2}",
                roleName, System.IO.Path.GetFileName(path), first, last, frames,
                result != null ? RootRangeY(result) : 0f, wasMoving));

            return result != null ? result : clip;
        }

        static ModelImporterClipAnimation Slice(ModelImporterClipAnimation from, string name,
                                                int firstFrame, int lastFrame, bool looping,
                                                bool bakeVerticalIntoPose = false)
        {
            var s = new ModelImporterClipAnimation();
            s.takeName = from.takeName;
            s.wrapMode = from.wrapMode;
            s.lockRootRotation = from.lockRootRotation;
            s.keepOriginalOrientation = from.keepOriginalOrientation;
            s.lockRootPositionXZ = from.lockRootPositionXZ;
            s.keepOriginalPositionXZ = from.keepOriginalPositionXZ;
            s.keepOriginalPositionY = from.keepOriginalPositionY;
            s.name = name;
            s.firstFrame = firstFrame;
            s.lastFrame = lastFrame;
            s.loopTime = looping;
            s.loopPose = looping;
            // Whether the up-and-down belongs to the pose depends on whether the feet are on the floor.
            //
            // For a slice that spans the flight, it does not: baked in, the body would lift a second
            // time on top of the lift the CharacterController is already doing.
            //
            // For a slice that stays on the ground -- a crouch and push-off, or a landing absorb -- it
            // very much does. Left out, the hips hold still while the legs straighten out of the crouch,
            // and the legs have nowhere to go but through the floor. Measured: the sole reached 27 cm
            // under it during a landing, which was reported as sinking in up to the ankle. Baked in, the
            // hips drop instead, which is what a person actually does, and the deepest the sole gets
            // anywhere in the whole jump is 1 cm ABOVE the floor.
            s.lockRootHeightY = bakeVerticalIntoPose;
            s.keepOriginalPositionY = true;
            return s;
        }

        /// <summary>Above this much of a jog (the Speed parameter), a landing counts as a moving one.</summary>
        const float MovingLanding = 0.3f;

        static void BuildJumpChain(AnimatorStateMachine sm, AnimatorState locomotion, string prefix,
                                   JumpPhases phases, int gate, string onlyIf = null)
        {
            var sUp = sm.AddState(prefix + "Up"); sUp.motion = phases.Up;
            var sAir = sm.AddState(prefix + "Air"); sAir.motion = phases.Air;

            // A hang that is not a loop gets frozen on one frame instead of played.
            //
            // This is the bug that survived the first jump fix. The borrowed middle phase looked like a
            // held pose because its root height is flat -- and I concluded from that alone that looping
            // it was invisible. It is not a held pose at all: measured, the foot travels 45 cm and the
            // hand 42 cm inside its 0.30 s, and its last frame sits 0.55 m away from its first. Looped
            // across a 0.7 s hang that plays twice over and snaps back in between, which is exactly the
            // "it repeats several times while I am in the air" it was reported as.
            //
            // Whether a clip can be looped is now asked properly: does it end where it began?
            if (!LoopsCleanly(phases.Air))
            {
                sAir.speed = 0f;
                sAir.cycleOffset = MostAirborneFrame(phases.Air);
                Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "[Animator] {0}Air does not loop (its ends are {1:F2} m apart), so it is held on "
                    + "one frame at {2:P0} through the clip instead of cycling",
                    prefix, EndsApart(phases.Air), sAir.cycleOffset));
            }
            var sLand = sm.AddState(prefix + "Land"); sLand.motion = phases.Land;

            var toAir = locomotion.AddTransition(sUp);
            toAir.AddCondition(AnimatorConditionMode.If, 0f, "Airborne");
            if (gate >= 0)
                toAir.AddCondition(gate == 1 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                                   0f, "JumpBack");
            if (onlyIf != null) toAir.AddCondition(AnimatorConditionMode.If, 0f, onlyIf);
            toAir.duration = 0.06f; toAir.hasExitTime = false;

            var upToAir = sUp.AddTransition(sAir);
            upToAir.hasExitTime = true; upToAir.exitTime = 0.95f; upToAir.duration = 0.08f;

            // Short hops and steps off a low kerb never reach the hang at all, so the rise needs its own
            // way down, or the character would finish a push-off she has already landed from.
            // Landing while moving skips the landing clip and goes straight back into the legs.
            //
            // The landing is a stand-still absorb: knees bend, feet planted. Played while the body
            // carries on at full speed, the feet stood still under a body gliding across the floor for
            // most of a second -- reported as "sliding on ice after a jump". A moving landing is a
            // stride, and the locomotion tree already is one. Transitions are checked in the order they
            // were added, so the moving case is added first.
            foreach (AnimatorState from in new[] { sUp, sAir })
            {
                var runOut = from.AddTransition(locomotion);
                runOut.AddCondition(AnimatorConditionMode.IfNot, 0f, "Airborne");
                runOut.AddCondition(AnimatorConditionMode.Greater, MovingLanding, "Speed");
                runOut.duration = 0.10f; runOut.hasExitTime = false;

                var land = from.AddTransition(sLand);
                land.AddCondition(AnimatorConditionMode.IfNot, 0f, "Airborne");
                land.duration = from == sUp ? 0.08f : 0.10f; land.hasExitTime = false;
            }

            // A standing landing that starts moving is let go of at once, for the same reason.
            var landMove = sLand.AddTransition(locomotion);
            landMove.AddCondition(AnimatorConditionMode.Greater, MovingLanding, "Speed");
            landMove.duration = 0.12f; landMove.hasExitTime = false;

            // Left early, at 80%: the tail of the recovery is a stand-up the locomotion blend does
            // better anyway, and holding it longer makes running away from a landing feel stuck.
            var landDone = sLand.AddTransition(locomotion);
            landDone.hasExitTime = true; landDone.exitTime = 0.8f; landDone.duration = 0.12f;

            var landToUp = sLand.AddTransition(sUp);
            landToUp.AddCondition(AnimatorConditionMode.If, 0f, "Airborne");
            if (gate >= 0)
                landToUp.AddCondition(gate == 1 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                                      0f, "JumpBack");
            if (onlyIf != null) landToUp.AddCondition(AnimatorConditionMode.If, 0f, onlyIf);
            landToUp.duration = 0.06f; landToUp.hasExitTime = false;
        }

        /// <summary>
        /// How far the pose at the end of a clip sits from the pose at its start, in metres.
        ///
        /// The honest test for "can this be looped". A cycle that was authored to loop comes back to
        /// where it started and measures zero -- both idles in this project measure exactly 0.000. A
        /// one-shot does not, and looping it snaps the character back to the beginning every time round.
        /// </summary>
        static float EndsApart(AnimationClip clip)
        {
            if (clip == null) return 0f;

            GameObject probe = SpawnProbe();
            if (probe == null) return 0f;

            Animator animator = probe.GetComponent<Animator>();
            var watched = new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
                                  HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.Head };
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null) { Object.DestroyImmediate(probe); return 0f; }

            var first = new Vector3[watched.Length];
            var last = new Vector3[watched.Length];

            // relative to the hips, so a clip that merely travels is not mistaken for one that changes pose
            probe.transform.position = Vector3.zero;
            clip.SampleAnimation(probe, 0f);
            for (int i = 0; i < watched.Length; i++)
            {
                Transform b = animator.GetBoneTransform(watched[i]);
                first[i] = b != null ? b.position - hips.position : Vector3.zero;
            }

            probe.transform.position = Vector3.zero;
            clip.SampleAnimation(probe, clip.length);
            for (int i = 0; i < watched.Length; i++)
            {
                Transform b = animator.GetBoneTransform(watched[i]);
                last[i] = b != null ? b.position - hips.position : Vector3.zero;
            }

            Object.DestroyImmediate(probe);

            float worst = 0f;
            for (int i = 0; i < watched.Length; i++)
                worst = Mathf.Max(worst, (last[i] - first[i]).magnitude);
            return worst;
        }

        /// <summary>Eight centimetres of drift is about where a loop stops being invisible.</summary>
        const float LoopTolerance = 0.08f;

        static bool LoopsCleanly(AnimationClip clip)
        {
            return clip != null && EndsApart(clip) <= LoopTolerance;
        }

        /// <summary>
        /// The moment in a clip that reads most like being off the ground, as a fraction of its length.
        ///
        /// Taken as the frame where the feet are highest under the hips, which is the tuck at the top of
        /// a jump. Used to choose which single frame to hold when the hang cannot be looped.
        /// </summary>
        static float MostAirborneFrame(AnimationClip clip)
        {
            if (clip == null) return 0.5f;

            GameObject probe = SpawnProbe();
            if (probe == null) return 0.5f;

            Animator animator = probe.GetComponent<Animator>();
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rf = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (hips == null || lf == null || rf == null) { Object.DestroyImmediate(probe); return 0.5f; }

            float best = float.MinValue, bestAt = 0.5f;
            const int Steps = 40;
            for (int i = 0; i <= Steps; i++)
            {
                float t = i / (float)Steps;
                probe.transform.position = Vector3.zero;
                clip.SampleAnimation(probe, clip.length * t);

                float tuck = Mathf.Min(lf.position.y, rf.position.y) - hips.position.y;
                if (tuck > best) { best = tuck; bestAt = t; }
            }

            Object.DestroyImmediate(probe);
            return bestAt;
        }

        static AnimationCurve RootCurve(AnimationClip clip, string property)
        {
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
                if (b.propertyName == property) return AnimationUtility.GetEditorCurve(clip, b);
            return null;
        }

        /// <summary>
        /// How far a clip lifts its own root, in metres.
        ///
        /// The line between "a crouch and a push-off" and "the whole leap": Vexa's in-place jump phases
        /// measure 0.27 m, a complete downloaded jump measures 1.11 m.
        /// </summary>
        const float MaxInPlaceRise = 0.6f;

        static float RootRangeY(AnimationClip clip)
        {
            AnimationCurve c = RootCurve(clip, "RootT.y");
            if (c == null) return 0f;

            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i <= 30; i++)
            {
                float y = c.Evaluate(clip.length * i / 30f);
                if (y < lo) lo = y;
                if (y > hi) hi = y;
            }
            return hi - lo;
        }

        static float RootTravelZ(AnimationClip clip)
        {
            AnimationCurve c = RootCurve(clip, "RootT.z");
            return c == null ? 0f : c.Evaluate(clip.length) - c.Evaluate(0f);
        }

        static AnimationClip SingleClip(string path)
        {
            AnimationClip only = null;
            int count = 0;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                AnimationClip c = o as AnimationClip;
                if (c == null || c.name.StartsWith("__")) continue;
                only = c; count++;
            }
            return count == 1 ? only : null;     // already sliced files have three, and are not sources
        }

        static AnimationClip FindClipNamed(string folder, string name)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                {
                    AnimationClip c = o as AnimationClip;
                    if (c != null && c.name == name) return c;
                }
            return null;
        }

        const float SendRunUp = 0.22f;   // thrust visible before the spell leaves the hand
        const float SendTail = 0.40f;    // follow-through kept after it

        /// <summary>
        /// Cuts a full mocap attack down to the part this game needs, and names it after its role.
        ///
        /// The downloaded attacks are 2.3 and 2.7 seconds: a long wind-up, a strike somewhere in the
        /// middle, and a long settle back to a neutral stance. Only the strike is wanted. Playing the
        /// whole thing fast enough to keep up measured out at 3x, which is the fast-forward look this
        /// project already threw out once on the run cycle -- so the clip is trimmed instead and then
        /// plays at its own speed.
        ///
        /// Skipped for anything already short, and idempotent: the rename is what marks it done.
        /// </summary>
        static AnimationClip TrimToStrike(AnimationClip clip, string roleName)
        {
            if (clip == null) return null;
            if (clip.name == roleName) return clip;                 // already trimmed on an earlier run
            if (clip.length < 1f) { SetLooping(clip, false); return clip; }   // short enough as it is

            string path = AssetDatabase.GetAssetPath(clip);
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) { SetLooping(clip, false); return clip; }

            ModelImporterClipAnimation[] clips = imp.clipAnimations;
            if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
            if (clips.Length == 0) return clip;

            float fps = clip.frameRate > 1f ? clip.frameRate : 30f;
            float originalLength = clip.length;   // read now; the reimport below repoints this reference
            float strikeAt = MeasureStrike(clip) * originalLength;
            float originalLast = clips[0].lastFrame;

            var trimmed = clips[0];
            trimmed.name = roleName;
            trimmed.firstFrame = Mathf.Max(clips[0].firstFrame, Mathf.Round((strikeAt - SendRunUp) * fps));
            trimmed.lastFrame = Mathf.Min(originalLast, Mathf.Round((strikeAt + SendTail) * fps));
            trimmed.loopTime = false;
            trimmed.loopPose = false;

            imp.clipAnimations = new[] { trimmed };
            imp.SaveAndReimport();

            AnimationClip result = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                AnimationClip c = o as AnimationClip;
                if (c != null && c.name == roleName) result = c;
            }

            Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "[Animator] {0}: strike was {1:F2}s into a {2:F2}s clip, trimmed to {3:F2}s",
                roleName, strikeAt, originalLength, result != null ? result.length : 0f));

            return result != null ? result : clip;
        }

        static AnimatorState BuildSend(AnimatorStateMachine sm, AnimatorState back, string name,
                                       AnimationClip clip, bool defensive)
        {
            var s = sm.AddState(name);
            s.motion = clip;
            // Mirrored when the camera sits over the left shoulder: the clips throw with the right hand,
            // and a humanoid clip mirrored throws with the left.
            s.mirrorParameterActive = true;
            s.mirrorParameter = "LeftHand";

            var enter = sm.AddAnyStateTransition(s);
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Send");
            enter.AddCondition(defensive ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                               0f, "Defensive");
            enter.duration = 0.05f; enter.hasExitTime = false; enter.canTransitionToSelf = false;

            // The clip is already only the useful part, so it runs almost to its end and then blends
            // back under the drawing pose.
            var done = s.AddTransition(back);
            done.hasExitTime = true;
            done.exitTime = 0.85f;
            done.duration = 0.15f;
            return s;
        }

        /// <summary>
        /// When the clip actually throws something, as a fraction of its length.
        ///
        /// Taken as the moment a hand reaches furthest from the body -- the extension at the end of a
        /// throw or the push of a barrier going up. Measured because "Standing 1H Magic Attack 01" says
        /// nothing about where in its 2.3 seconds the interesting frame is.
        /// </summary>
        static float MeasureStrike(AnimationClip clip)
        {
            GameObject probe = SpawnProbe();
            if (probe == null) return 0.4f;

            Animator animator = probe.GetComponent<Animator>();
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform lh = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform rh = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hips == null || lh == null || rh == null) { Object.DestroyImmediate(probe); return 0.4f; }

            AnimationMode.StartAnimationMode();
            const int Steps = 60;
            float best = 0f, bestAt = 0.4f;

            for (int i = 0; i <= Steps; i++)
            {
                float t = i / (float)Steps;
                AnimationMode.SampleAnimationClip(probe, clip, clip.length * t);

                float reach = Mathf.Max(Vector3.Distance(hips.position, lh.position),
                                        Vector3.Distance(hips.position, rh.position));
                if (reach > best) { best = reach; bestAt = t; }
            }

            AnimationMode.StopAnimationMode();
            Object.DestroyImmediate(probe);
            return bestAt;
        }

        static void SetStride(BlendTree tree, int index, float wantedSpeed)
        {
            ChildMotion[] children = tree.children;
            if (index < 0 || index >= children.Length) return;

            AnimationClip clip = children[index].motion as AnimationClip;
            if (clip == null) return;

            float authoredSpeed = MeasureAuthoredSpeed(clip);
            if (authoredSpeed < 0.2f) return;

            // clamped: a clip that needs tripling looks frantic, and that is a signal the movement
            // speed is wrong rather than something to paper over
            children[index].timeScale = Mathf.Clamp(wantedSpeed / authoredSpeed * StridePlayback, 0.5f, 2f);
            tree.children = children;
        }

        /// <summary>
        /// How fast an in-place locomotion clip believes the body is moving, taken from how fast the
        /// planted foot slides backwards past the hips. Sampled rather than assumed -- the alternative
        /// is guessing a stride length, and the guess is what produces skating in the first place.
        /// </summary>
        public static float MeasureAuthoredSpeed(AnimationClip clip)
        {
            GameObject probe = SpawnProbe();
            if (probe == null) return -1f;

            Animator animator = probe.GetComponent<Animator>();

            // A clip that was NOT downloaded In Place says outright how fast it goes: its root travels
            // the real distance. That beats any estimate from the feet, and the estimate below turned
            // out to need it -- "Jog Forward" measured 1.9 m/s by step length, but driven by its own
            // root motion in game it covers 2.53 m/s on this character. The step-length figure is a
            // floor, not the speed: a jog carries the body a good way past the widest split.
            Vector3 travel = clip.averageSpeed;
            float rootSpeed = new Vector2(travel.x, travel.z).magnitude;
            if (clip.isHumanMotion && rootSpeed > 0.3f)
            {
                // Humanoid root motion is in normalised human units. humanScale turns it into the
                // avatar's metres, and the model's own scale -- the skin is resized to 2 m -- into the
                // game's. Checked against the game: 2.54 x 0.96 x 1.029 = 2.51, measured 2.52.
                float metres = rootSpeed * animator.humanScale * animator.transform.lossyScale.y;
                Object.DestroyImmediate(probe);
                return metres;
            }

            Transform lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rf = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (lf == null || rf == null) { Object.DestroyImmediate(probe); return -1f; }

            const int Steps = 90;
            float widest = 0f;

            AnimationMode.StartAnimationMode();
            for (int i = 0; i < Steps; i++)
            {
                // reset first: a clip carrying root motion walks the probe away from the origin, and
                // then the foot positions below would be measured against a moving frame
                probe.transform.position = Vector3.zero;
                probe.transform.rotation = Quaternion.identity;

                AnimationMode.SampleAnimationClip(probe, clip, clip.length * i / (float)Steps);

                Vector3 a = lf.position; a.y = 0f;
                Vector3 b = rf.position; b.y = 0f;
                float split = Vector3.Distance(a, b);
                if (split > widest) widest = split;
            }
            AnimationMode.StopAnimationMode();
            Object.DestroyImmediate(probe);

            // Step length, not planted-foot slide. A cycle is two steps and the feet are one step apart
            // at their widest, so the body covers 2 x widest per cycle.
            //
            // The slide method this replaces needed a height threshold to decide when a foot was on the
            // ground, and that turned out to be the whole problem: measured, the two feet of this rig sit
            // 2.7 cm apart at their lowest against a 3 cm threshold, so one foot's window caught swing
            // frames and the other's did not. It reported the run at 3.4 m/s. Two independent checks --
            // step length and the fastest backward ankle -- both put it near 2.5, and the tree was
            // therefore playing the run 35% too slow while the character crossed the arena at 4 m/s.
            return clip.length > 0.01f ? 2f * widest / clip.length : 0f;
        }

        /// <summary>
        /// A throwaway copy of the current skin, used to sample clips on a real rig.
        ///
        /// It has to be the skin actually in use rather than a fixed model: stride length scales with
        /// leg length, so measuring on one character and playing on another would reintroduce exactly
        /// the foot skating this measurement exists to remove.
        /// </summary>
        static GameObject SpawnProbe()
        {
            GameObject prefab = CharacterSkin.Prefab();
            if (prefab == null) return null;

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            CharacterSkin.NormaliseHeight(go);    // measure at the size the game plays at, not the model's

            Animator a = go.GetComponent<Animator>();
            if (a == null) a = go.AddComponent<Animator>();
            if (a.avatar == null || !a.avatar.isHuman) a.avatar = CharacterSkin.AvatarFor(prefab);
            return go;
        }

        static AvatarMask BuildUpperBodyMask()
        {
            const string path = "Assets/Animation/UpperBody.mask";
            var existing = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (existing != null) return existing;

            var mask = new AvatarMask();
            for (AvatarMaskBodyPart part = 0; part < AvatarMaskBodyPart.LastBodyPart; part++)
                mask.SetHumanoidBodyPartActive(part, false);

            // Arms, hands and the torso above the hips. Legs and root stay with the locomotion layer,
            // which is the entire point.
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);

            AssetDatabase.CreateAsset(mask, path);
            return mask;
        }

        static bool AllClipsLoop(string modelPath)
        {
            bool any = false;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                AnimationClip c = o as AnimationClip;
                if (c == null || c.name.StartsWith("__")) continue;
                any = true;
                if (!c.isLooping) return false;
            }
            return any;
        }

        /// <summary>
        /// Turns looping off on the takeoff and the landing.
        ///
        /// The Vexa pack ships every clip with Loop Time ticked, including these two, and a looping
        /// takeoff is what made the character bounce repeatedly in mid-air. Jump02 is left looping on
        /// purpose -- it is a held pose, so its loop is what covers a hang of any length.
        /// </summary>
        /// <summary>
        /// Forces one clip to loop or not, according to the part it plays.
        ///
        /// Set here rather than left to whoever exported the file, because getting it wrong is invisible
        /// in both directions. A one-shot left looping repeats for as long as its state lasts -- that is
        /// what had the character crouching and pushing off over and over in mid-air. A held pose with
        /// looping off freezes on its last frame the moment the state outlives the clip.
        /// </summary>
        static void SetLooping(AnimationClip clip, bool looping)
        {
            if (clip == null || clip.isLooping == looping) return;

            string path = AssetDatabase.GetAssetPath(clip);
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) return;

            ModelImporterClipAnimation[] clips = imp.clipAnimations;
            if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;

            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = looping;
                clips[i].loopPose = looping;
            }

            imp.clipAnimations = clips;
            imp.SaveAndReimport();
            Debug.Log("[Animator] " + System.IO.Path.GetFileName(path)
                      + (looping ? " set to loop" : " set to play once"));
        }

        /// <summary>Every transform name in a model, which for a rig is its bone list.</summary>
        static List<string> BoneNames(GameObject prefab)
        {
            var names = new List<string>();
            if (prefab == null) return names;

            GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            probe.hideFlags = HideFlags.HideAndDontSave;
            foreach (Transform t in probe.GetComponentsInChildren<Transform>()) names.Add(t.name);
            Object.DestroyImmediate(probe);
            return names;
        }

        /// <summary>
        /// Whether an animation file's skeleton is the same one the skin uses.
        ///
        /// Compared by bone name rather than by count, and the file's own root is expected to differ --
        /// Mixamo names it after the animation ("Fast Run") while the character's is named after the
        /// character. Everything below that has to line up.
        /// </summary>
        static bool SkeletonMatches(string modelPath, List<string> skinBones)
        {
            if (skinBones == null || skinBones.Count == 0) return false;

            List<string> bones = BoneNames(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            if (bones.Count < 10) return false;

            int shared = 0;
            foreach (string n in bones) if (skinBones.Contains(n)) shared++;

            // one root allowed to differ, nothing else
            return shared >= bones.Count - 1;
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Animation"))
                AssetDatabase.CreateFolder("Assets", "Animation");

            foreach (string role in new[] { "Locomotion", "Idle", "Jump", "Cast" })
                if (!AssetDatabase.IsValidFolder("Assets/Animation/" + role))
                    AssetDatabase.CreateFolder("Assets/Animation", role);

            if (!AssetDatabase.IsValidFolder(CharacterSkin.SkinFolder))
                AssetDatabase.CreateFolder("Assets", "Characters");
        }

        /// <summary>
        /// Anything dropped in the locomotion folder gets switched to Humanoid automatically.
        ///
        /// A downloaded animation arrives as Generic, and a generic clip does not retarget -- the
        /// character would simply stand still with no error anywhere. Doing it here means the whole
        /// job is "put the file in the folder and press the menu item", instead of a rig setting
        /// somebody has to know about.
        /// </summary>
        /// <summary>
        /// Every folder the game takes art from, switched to Humanoid.
        ///
        /// The skin is in here too, and deliberately: a character imported as Generic looks completely
        /// normal in the project window and then stands motionless in the scene, because a generic rig
        /// has nothing for a humanoid clip to retarget onto.
        ///
        /// Only the locomotion folder gets Loop Time forced on. The other roles are one-shots and held
        /// poses in roughly equal measure, so they are set individually once the builder knows which
        /// clip is playing which part.
        /// </summary>
        static void PrepareLibrary()
        {
            MakeHumanoid(CharacterSkin.SkinFolder, false, false);   // the skin builds its own avatar
            MakeHumanoid(LocomotionFolder, true, true);
            MakeHumanoid(IdleFolder, false, true);
            MakeHumanoid(JumpFolder, false, true);
            MakeHumanoid(CastFolder, false, true);
            MakeHumanoid(PackFolder, false, true);
        }

        /// <summary>The one clip in PackFolder/name.fbx, or null if the pack is not there.</summary>
        static AnimationClip PackClip(string name)
        {
            string path = PackFolder + "/" + name + ".fbx";
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                AnimationClip c = o as AnimationClip;
                if (c != null && !c.name.StartsWith("__")) return c;
            }
            return null;
        }

        static void MakeHumanoid(string folder, bool forceLooping, bool wearSkinAvatar)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;

            // Animation files are given the SKIN'S avatar rather than one built from their own skeleton,
            // whenever the two skeletons are the same shape.
            //
            // This is the single biggest thing in this file. With its own avatar, a clip is retargeted:
            // Unity copies joint ANGLES onto a differently proportioned body and the feet land somewhere
            // else. Measured here, on a set whose rig has legs 62% of its height against this character's
            // 57%, the soles went 11.8 cm through the floor on every stride -- which is what "drags its
            // ankles" was. Handing the clip the character's own avatar skips retargeting entirely: same
            // bone names, same skeleton, nothing to convert. The same measurement then reads +4 cm, and
            // backing away and strafing land within 6 mm of the floor.
            GameObject skinPrefab = wearSkinAvatar ? CharacterSkin.Prefab() : null;
            Avatar skinAvatar = skinPrefab != null ? CharacterSkin.AvatarFor(skinPrefab) : null;
            List<string> skinBones = skinAvatar != null ? BoneNames(skinPrefab) : null;

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;

                // Only worth borrowing the avatar if the skeletons really do match. Unity refuses the
                // copy outright when the hierarchy differs, and a near-match is worse than a refusal --
                // the clip imports and simply plays wrong.
                bool canWear = skinAvatar != null && SkeletonMatches(path, skinBones);

                bool needsHumanoid = imp.animationType != ModelImporterAnimationType.Human;
                bool needsAvatar = canWear
                    ? imp.sourceAvatar != skinAvatar
                    : imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel;
                bool needsLoop = forceLooping && !AllClipsLoop(path);
                if (!needsHumanoid && !needsAvatar && !needsLoop) continue;

                imp.animationType = ModelImporterAnimationType.Human;
                if (canWear)
                {
                    imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    imp.sourceAvatar = skinAvatar;
                }
                else
                {
                    imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    imp.sourceAvatar = null;
                }

                if (needsLoop)
                {
                    // Loop Time, forced on. This is the one that cost hours: a downloaded cycle arrives
                    // with looping OFF, so once the state's time passes 1.0 the clip clamps to its final
                    // frame -- and the last frame of a run cycle is nearly the first, so the character
                    // appears frozen in a standing pose while the animator insists it is playing. Nothing
                    // errors, nothing warns, and the state time keeps counting up quite happily.
                    ModelImporterClipAnimation[] clips = imp.defaultClipAnimations;
                    for (int i = 0; i < clips.Length; i++)
                    {
                        clips[i].loopTime = true;
                        clips[i].loopPose = true;      // kills the jolt where the cycle wraps
                    }
                    imp.clipAnimations = clips;
                }

                imp.SaveAndReimport();
                Debug.Log("[Animator] prepared " + System.IO.Path.GetFileName(path)
                          + (canWear ? " (wearing the character's own avatar" : " (own avatar")
                          + (needsLoop ? ", +loop)" : ")"));
            }
        }

        /// <summary>
        /// One role, filled from the library if it can be, and borrowed from the character pack if not.
        ///
        /// Keywords rather than exact names, because a downloaded jump is called whatever the person
        /// who uploaded it felt like. The borrowed name is exact, since that one is known.
        /// </summary>
        static AnimationClip Role(string folder, string onLoanName, params string[] keywords)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                var found = new List<AnimationClip>();
                foreach (string keyword in keywords)
                    foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
                        Collect(AssetDatabase.GUIDToAssetPath(guid), keyword, found);

                if (found.Count > 0) return found[0];
            }
            return FindClip(onLoanName, OnLoanFrom);
        }

        static AnimationClip FindClip(string clipName, string folder)
        {
            // Searching a folder that is not there makes Unity log its own warning on every build, so
            // the "no pack to borrow from" case is answered here instead of being reported as a fault.
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder)) return null;

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    AnimationClip c = o as AnimationClip;
                    if (c != null && c.name == clipName) return c;
                }
            }
            return null;
        }

        // Clips are picked by which one actually fits the speed, not by name, and the reason is
        // measured: the downloaded "Run" is a 2.4 m/s jog and "Fast Run" is a 3.9 m/s run. Going by the
        // names once put the jog on a 4 m/s character at 1.67x -- "runs like a cripple". The jog now
        // gets the jog, on WASD, and the run gets Shift. LocomotionSet.Resolve does the picking.

        /// <summary>The clip whose own stride is closest to the speed it has to represent.</summary>
        static AnimationClip BestFit(List<AnimationClip> candidates, float wantedSpeed)
        {
            AnimationClip best = null;
            float bestError = float.MaxValue;

            foreach (AnimationClip c in candidates)
            {
                float authored = MeasureAuthoredSpeed(c);
                if (authored < 0.2f) continue;

                // compared as a ratio, not a difference: being 1 m/s out matters far more at walking
                // pace than at a sprint, and it is the playback rate that shows on screen
                float error = Mathf.Abs(Mathf.Log(wantedSpeed / authored));
                if (error < bestError) { bestError = error; best = c; }
            }
            return best;
        }

        /// <summary>Any humanoid clip in the locomotion folder matching a keyword, shortest name wins.</summary>
        static AnimationClip FindHumanoidClipAnywhere(params string[] keywords)
        {
            return FindHumanoidClipExcluding(keywords, new AnimationClip[0]);
        }

        /// <summary>
        /// The same search, but refusing clips already claimed by a more specific one.
        ///
        /// Needed because these names overlap: "Run Backward", "Fast Run" and the strafes all contain
        /// "run", so a plain search for the forward run would happily return whichever of them came
        /// first and the character would sprint backwards while moving forwards.
        /// </summary>
        static AnimationClip FindHumanoidClipAnywhere(string keyword, params AnimationClip[] alreadyTaken)
        {
            return FindHumanoidClipExcluding(new[] { keyword }, alreadyTaken);
        }

        static AnimationClip FindHumanoidClipExcluding(string[] keywords, AnimationClip[] taken)
        {
            var found = new List<AnimationClip>();
            if (!AssetDatabase.IsValidFolder(LocomotionFolder)) return null;

            // Scoped to the locomotion folder rather than the whole project, because these keywords are
            // broad enough to be dangerous. "run" would happily match a Running Jump sitting in the jump
            // folder, and the character would then run forwards by leaping.
            string[] scope = { LocomotionFolder };

            foreach (string raw in keywords)
            {
                string keyword = raw.ToLowerInvariant();
                foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", scope))
                    Collect(AssetDatabase.GUIDToAssetPath(guid), keyword, found);
                foreach (string guid in AssetDatabase.FindAssets("t:Model", scope))
                    Collect(AssetDatabase.GUIDToAssetPath(guid), keyword, found);
            }

            AnimationClip best = null;
            foreach (AnimationClip c in found)
            {
                bool claimed = false;
                foreach (AnimationClip t in taken) if (t == c) claimed = true;
                if (claimed) continue;

                string name = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(c));
                string bestName = best == null ? null : System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(best));
                if (best == null || name.Length < bestName.Length) best = c;
            }
            return best;
        }

        static void Collect(string path, string keyword, List<AnimationClip> into)
        {
            // The FILE name counts as well as the clip name, and that is not a nicety: Mixamo names the
            // clip inside every download "mixamo.com" regardless of what you asked for, so matching on
            // the clip name alone would find nothing at all and look like the search was broken.
            string file = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            bool fileMatches = file.Contains(keyword);

            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                AnimationClip c = o as AnimationClip;
                if (c == null || c.name.StartsWith("__")) continue;
                if (!c.isHumanMotion) continue;
                if (!fileMatches && !c.name.ToLowerInvariant().Contains(keyword)) continue;
                if (!into.Contains(c)) into.Add(c);
            }
        }
    }
}
