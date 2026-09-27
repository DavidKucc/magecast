using MageCast;
using UnityEditor;
using UnityEngine;

namespace MageCast.EditorTools
{
    /// <summary>
    /// Builds the player as a prefab. Same principle as the arena builder: nothing is hand-assembled,
    /// so it can be regenerated after any change -- a new skin, a new component, a rebuilt animator.
    ///
    /// It used to build the player straight into the scene. Players are spawned by the network now,
    /// one per person in the game, so the thing being built is the template they are spawned from.
    /// </summary>
    public static class PlayerRigBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Player.prefab";

        [MenuItem("Tools/Arena/Build Player Prefab")]
        public static GameObject BuildPrefab()
        {
            GameObject player = BuildPlayer();

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(player, PrefabPath);
            Object.DestroyImmediate(player);

            Debug.Log("[Player] prefab saved to " + PrefabPath + ". Skin: "
                      + CharacterSkin.Describe(CharacterSkin.Prefab()));
            return prefab;
        }

        static GameObject BuildPlayer()
        {
            GameObject player = new GameObject("Player");

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 2f;                       // the scale contract: a player is 2 m
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 1f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;
            cc.skinWidth = 0.03f;

            Material skin = Mat("BO_Player", 0.85f, 0.78f, 0.35f);
            Material trim = Mat("BO_PlayerTrim", 0.15f, 0.15f, 0.18f);

            BuildBody(player.transform, skin, trim);

            player.AddComponent<PlayerMotor>();
            player.AddComponent<MageCast.Gestures.GestureCaster>();

            // The rune crib sheet, top right. Temporary by design -- F6 hides it, and deleting the
            // component is the whole removal.
            player.AddComponent<MageCast.Gestures.GestureLegend>();

            // Shows, while a spell is held, what it will do where you are pointing -- floor, wall or
            // person now give different effects from one gesture, and that must be visible beforehand.
            player.AddComponent<MageCast.Gestures.AimSurfaceMarker>();

            // The player can be hurt too -- by their own misfire, mostly, which is exactly the kind of
            // thing that should be findable by accident rather than described in a patch note.
            MageCast.Combat.Health playerHealth = player.AddComponent<MageCast.Combat.Health>();
            SerializedObject healthSo = new SerializedObject(playerHealth);
            healthSo.FindProperty("maxHealth").floatValue = 100f;
            healthSo.ApplyModifiedProperties();

            player.AddComponent<MageCast.PlayerHud>();

            // --- network ---
            // The camera is no longer wired here: a prefab cannot point at something in a scene, and
            // which player the camera follows is only known once the network says which one is yours.
            player.AddComponent<Unity.Netcode.NetworkObject>();

            var sync = player.AddComponent<MageCast.OwnerNetworkTransform>();
            // A body that only ever turns about the vertical: pitch lives in the camera and the upper-body
            // aim, and scale never changes. Sending those would be a third of the traffic for nothing.
            sync.SyncRotAngleX = false;
            sync.SyncRotAngleZ = false;
            sync.SyncScaleX = false;
            sync.SyncScaleY = false;
            sync.SyncScaleZ = false;
            sync.Interpolate = true;

            player.AddComponent<MageCast.PlayerNet>();
            return player;
        }

        /// <summary>
        /// Makes sure the scene camera is the game camera: third person, crosshair, near plane close
        /// enough for the spring arm. The player finds it at spawn.
        /// </summary>
        public static void EnsureGameCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.nearClipPlane = 0.05f;   // the spring arm can pull in to 0.9 m; a 0.3 m near plane clips the body
            cam.farClipPlane = 300f;

            ThirdPersonCamera tpc = cam.GetComponent<ThirdPersonCamera>();
            if (tpc == null) tpc = cam.gameObject.AddComponent<ThirdPersonCamera>();
            if (cam.GetComponent<Crosshair>() == null) cam.gameObject.AddComponent<Crosshair>();

            SerializedObject camSo = new SerializedObject(tpc);
            camSo.FindProperty("target").objectReferenceValue = null;
            camSo.ApplyModifiedProperties();
        }

        const string CharacterController_ = "Assets/Animation/Character.controller";

        /// <summary>
        /// The character, or the old capsule if the art is missing. Falling back rather than failing is
        /// deliberate: everything here worked on a yellow capsule until the model arrived, and a rig
        /// builder that throws because an asset folder moved is worse than one that looks wrong.
        /// </summary>
        static void BuildBody(Transform player, Material skin, Material trim)
        {
            GameObject prefab = CharacterSkin.Prefab();
            if (prefab != null && BuildCharacter(player, prefab)) return;

            Debug.LogWarning("[Player] character prefab not found, falling back to the capsule.");

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(player, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            body.GetComponent<Renderer>().sharedMaterial = skin;
            Object.DestroyImmediate(body.GetComponent<Collider>());

            GameObject facing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            facing.name = "FacingMarker";
            facing.transform.SetParent(player, false);
            facing.transform.localPosition = new Vector3(0f, 1.5f, 0.42f);
            facing.transform.localScale = new Vector3(0.16f, 0.16f, 0.35f);
            facing.GetComponent<Renderer>().sharedMaterial = trim;
            Object.DestroyImmediate(facing.GetComponent<Collider>());
        }

        static bool BuildCharacter(Transform player, GameObject prefab)
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.name = "Body";
            model.transform.SetParent(player, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // Scaled to the 2 m in the scale contract, whatever the skin happens to model at -- leaving
            // the mismatch would put the head below the top of its own collider, so a shot clipping the
            // capsule would visibly pass over it. The animator builder scales its measuring probes the
            // same way, which is why this lives in CharacterSkin rather than here.
            CharacterSkin.NormaliseHeight(model);

            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();

            Avatar avatar = CharacterSkin.AvatarFor(prefab);
            if (avatar == null)
            {
                Debug.LogError("[Player] '" + prefab.name + "' has no humanoid avatar. A skin has to be "
                             + "imported with Rig > Animation Type set to Humanoid, or nothing retargets "
                             + "onto it and it will just stand there.");
                return false;
            }
            animator.avatar = avatar;
            animator.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CharacterController_);
            animator.applyRootMotion = false;   // the motor owns movement; root motion would fight it

            // Damps the twitch retargeting leaves in the feet between frames. Costs a little, and the
            // feet are the part of a third-person character the eye is most unforgiving about.
            animator.stabilizeFeet = true;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var anim = player.GetComponent<MageCast.PlayerAnimation>();
            if (anim == null) anim = player.gameObject.AddComponent<MageCast.PlayerAnimation>();
            SerializedObject so = new SerializedObject(anim);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.ApplyModifiedProperties();

            BuildAimRig(player, model, animator);
            return true;
        }

        /// <summary>
        /// The Animation Rigging setup that lets the chest and head turn towards the crosshair.
        ///
        /// Built here rather than by hand in the scene for the usual reason -- the body is a skin and
        /// gets replaced, and a rig wired to one character's bones by hand would die with it. The bones
        /// are asked for by humanoid role, so this works on whatever is worn.
        /// </summary>
        static void BuildAimRig(Transform player, GameObject model, Animator animator)
        {
            // Lives on the player, not the model: it is a place in the world, not part of the body, and
            // parenting it under an animated hierarchy would have the animation drag it around.
            GameObject targetGo = new GameObject("AimTarget");
            targetGo.transform.SetParent(player, false);
            targetGo.transform.localPosition = new Vector3(0f, 1.5f, 10f);

            var rigBuilder = model.GetComponent<UnityEngine.Animations.Rigging.RigBuilder>();
            if (rigBuilder == null) rigBuilder = model.AddComponent<UnityEngine.Animations.Rigging.RigBuilder>();

            GameObject rigGo = new GameObject("AimRig");
            rigGo.transform.SetParent(model.transform, false);
            var rig = rigGo.AddComponent<UnityEngine.Animations.Rigging.Rig>();
            rig.weight = 1f;

            rigBuilder.layers.Clear();
            rigBuilder.layers.Add(new UnityEngine.Animations.Rigging.RigLayer(rig, true));

            // The chest takes about a third of what the head does. All of it in the neck reads as a
            // gawping owl; all of it in the chest drags the arms off the run cycle.
            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Spine);
            AimConstraint(rigGo.transform, "ChestAim", chest, targetGo.transform, 0.3f, 45f);
            AimConstraint(rigGo.transform, "HeadAim", animator.GetBoneTransform(HumanBodyBones.Head),
                          targetGo.transform, 0.85f, 70f);

            rigBuilder.Build();

            var aim = player.GetComponent<MageCast.UpperBodyAim>();
            if (aim == null) aim = player.gameObject.AddComponent<MageCast.UpperBodyAim>();
            SerializedObject aimSo = new SerializedObject(aim);
            aimSo.FindProperty("target").objectReferenceValue = targetGo.transform;
            aimSo.FindProperty("rig").objectReferenceValue = rig;
            aimSo.FindProperty("cam").objectReferenceValue = null;     // found at runtime
            aimSo.ApplyModifiedProperties();
        }

        static void AimConstraint(Transform parent, string name, Transform bone, Transform target,
                                  float weight, float limit)
        {
            if (bone == null) return;

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var constraint = go.AddComponent<UnityEngine.Animations.Rigging.MultiAimConstraint>();
            constraint.weight = weight;

            var data = constraint.data;
            data.constrainedObject = bone;

            var sources = new UnityEngine.Animations.Rigging.WeightedTransformArray();
            sources.Add(new UnityEngine.Animations.Rigging.WeightedTransform(target, 1f));
            data.sourceObjects = sources;

            // Measured on the rig rather than assumed: every spine and head bone on this character has
            // its forward on +Z and its up on +Y, to a dot product of 1.00. Guessing this wrong is the
            // failure where the head snaps sideways the moment the rig switches on.
            data.aimAxis = UnityEngine.Animations.Rigging.MultiAimConstraintData.Axis.Z;
            data.upAxis = UnityEngine.Animations.Rigging.MultiAimConstraintData.Axis.Y;
            data.worldUpType = UnityEngine.Animations.Rigging.MultiAimConstraintData.WorldUpType.SceneUp;
            data.maintainOffset = false;

            // Yaw and pitch only. Letting it roll tips the head sideways when you aim past a shoulder.
            data.constrainedXAxis = true;
            data.constrainedYAxis = true;
            data.constrainedZAxis = false;
            data.limits = new Vector2(-limit, limit);

            constraint.data = data;
        }

        static Material Mat(string name, float r, float g, float b)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");

            string path = "Assets/Materials/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = new Color(r, g, b);
            m.SetFloat("_Glossiness", 0.1f);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
