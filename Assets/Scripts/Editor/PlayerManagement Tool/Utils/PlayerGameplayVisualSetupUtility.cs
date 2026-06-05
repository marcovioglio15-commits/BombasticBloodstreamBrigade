using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Builds and refreshes the authored player visual assets required by gameplay integration.
/// This includes the animated muzzle wrapper prefab, the player prefab references, the shoot clip binding, and the upper-body shoot state.
/// None.
/// </summary>
public static class PlayerGameplayVisualSetupUtility
{
    #region Constants
    private const string PlayerPrefabPath = "Assets/Prefabs/Player/PF_Player.prefab";
    private const string PlayerVisualPrefabPath = "Assets/Prefabs/Player/PF_PlayerVisual.prefab";
    private const string PlayerModelPrefabPath = "Assets/3D/Character/SK_PlayerFinal.fbx";
    private const string FlyAnimationPackPath = "Assets/3D/Character/AN_FlyPack.fbx";
    private const string ShootClipPath = "Assets/3D/Testing/PlayerTest/PlayerTestAnimations/AN_MovementForward-Shoot.fbx";
    private const string AimBackwardClipPath = "Assets/3D/Testing/PlayerTest/PlayerTestAnimations/AN_MovementBackwards-Aim.fbx";
    private const string AimLeftClipPath = "Assets/3D/Testing/PlayerTest/PlayerTestAnimations/AN_MovementLeft-Aim.fbx";
    private const string AimRightClipPath = "Assets/3D/Testing/PlayerTest/PlayerTestAnimations/AN_MovementRight-Aim.fbx";
    private const string AnimationBindingsPresetPath = "Assets/Scriptable Objects/Player/Animation Bindings/PlayerAnimationBindingsPreset.asset";
    private const string AnimatorControllerPath = "Assets/3D/Testing/PlayerTest/Animation Contorller/AC_PlayerTesting.controller";
    private const string LowerBodyMaskPath = "Assets/3D/Testing/PlayerTest/Avatar Masks/AM_PlayerTesting_Lower.mask";
    private const string UpperBodyMaskPath = "Assets/3D/Testing/PlayerTest/Avatar Masks/AM_PlayerTesting_Upper.mask";
    private const string GunBarrelObjectName = "GunBarrel";
    private const string BaseGunObjectName = "base gun";
    private const string CannonObjectName = "cannon";
    private const string GatlingObjectName = "gatling";
    private const string RailgunObjectName = "railgun";
    private const string MuzzleAnchorObjectName = "MuzzleAnchor";
    private const string LowerBodyLayerName = "LowerBody";
    private const string LowerMoveStateName = "BT_Lower_Move";
    private const string UpperBodyLayerName = "UpperBody";
    private const string UpperAimStateName = "BT_Upper_Aim";
    private const string UpperShootStateName = "ST_Upper_Shoot";
    private const int PlayerLayer = 3;
    private const float MuzzleForwardPadding = 0.01f;
    private const float GeneratedForwardShotOffset = 0.14f;
    private const float GeneratedMinimumPlanarDistance = 0.72f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Runs the full authoring setup for the player visual wrapper, player prefab, animation bindings preset, and animator controller.
    /// None.
    /// </summary>
    public static void ExecuteSetup()
    {
        EnsureFlyAnimationPackLooping();
        AnimationClip idleClip = LoadAnimationClip(FlyAnimationPackPath, "AN_FlyIdle");
        AnimationClip moveForwardClip = LoadAnimationClip(FlyAnimationPackPath, "AN_FlyForward");
        AnimationClip moveBackwardClip = LoadAnimationClip(FlyAnimationPackPath, "AN_FlyBackwards");
        AnimationClip moveLeftClip = LoadAnimationClip(FlyAnimationPackPath, "AN_FlyLeft");
        AnimationClip moveRightClip = LoadAnimationClip(FlyAnimationPackPath, "AN_FlyRight");
        AnimationClip shootClip = LoadPrimaryAnimationClip(ShootClipPath);
        AnimationClip aimBackwardClip = LoadPrimaryAnimationClip(AimBackwardClipPath);
        AnimationClip aimLeftClip = LoadPrimaryAnimationClip(AimLeftClipPath);
        AnimationClip aimRightClip = LoadPrimaryAnimationClip(AimRightClipPath);
        EnsureAvatarMasks();
        EnsureAnimationBindingsPreset(idleClip,
                                      moveForwardClip,
                                      moveBackwardClip,
                                      moveLeftClip,
                                      moveRightClip,
                                      shootClip,
                                      aimBackwardClip,
                                      aimLeftClip,
                                      aimRightClip);
        EnsureAnimatorController(idleClip,
                                 moveForwardClip,
                                 moveBackwardClip,
                                 moveLeftClip,
                                 moveRightClip,
                                 shootClip,
                                 aimBackwardClip,
                                 aimLeftClip,
                                 aimRightClip);
        GameObject playerVisualPrefab = EnsurePlayerVisualPrefab();
        EnsurePlayerPrefab(playerVisualPrefab);
        AssetDatabase.SaveAssets();
    }
    #endregion

    #region Prefabs
    /// <summary>
    /// Creates or refreshes the generated player-visual wrapper prefab that carries the animated muzzle anchor.
    /// None.
    /// </summary>
    /// <returns>Generated player visual prefab asset.</returns>
    private static GameObject EnsurePlayerVisualPrefab()
    {
        EnsureFolder(Path.GetDirectoryName(PlayerVisualPrefabPath));
        GameObject playerModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPrefabPath);

        if (playerModelPrefab == null)
            throw new InvalidOperationException(string.Format("Player model prefab not found at '{0}'.", PlayerModelPrefabPath));

        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerVisualPrefabPath);
        GameObject prefabContentsRoot = prefabRoot != null
            ? PrefabUtility.LoadPrefabContents(PlayerVisualPrefabPath)
            : new GameObject("PF_PlayerVisual");

        try
        {
            prefabContentsRoot.name = "PF_PlayerVisual";
            DestroyAllChildren(prefabContentsRoot.transform);

            GameObject modelInstance = PrefabUtility.InstantiatePrefab(playerModelPrefab, prefabContentsRoot.scene) as GameObject;

            if (modelInstance == null)
                throw new InvalidOperationException("Unable to instantiate player model prefab into generated visual wrapper.");

            modelInstance.transform.SetParent(prefabContentsRoot.transform, false);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;

            Animator animator = modelInstance.GetComponentInChildren<Animator>(true);
            RuntimeAnimatorController animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);

            if (animator == null)
                throw new InvalidOperationException("Player model prefab does not contain an Animator.");

            if (animatorController == null)
                throw new InvalidOperationException(string.Format("Animator controller not found at '{0}'.", AnimatorControllerPath));

            animator.runtimeAnimatorController = animatorController;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;

            Transform gunBarrelTransform = FindChildRecursive(modelInstance.transform, GunBarrelObjectName);

            if (gunBarrelTransform == null)
                throw new InvalidOperationException(string.Format("Unable to find '{0}' inside the player model hierarchy.", GunBarrelObjectName));

            GameObject muzzleAnchorObject = new GameObject(MuzzleAnchorObjectName);
            Transform muzzleAnchorTransform = muzzleAnchorObject.transform;
            muzzleAnchorTransform.SetParent(gunBarrelTransform, false);
            muzzleAnchorTransform.localPosition = Vector3.zero;
            muzzleAnchorTransform.localRotation = Quaternion.identity;
            muzzleAnchorTransform.localScale = Vector3.one;

            PlayerVisualMuzzleAnchor muzzleAnchorComponent = GetOrAddComponent<PlayerVisualMuzzleAnchor>(prefabContentsRoot);
            SerializedObject serializedMuzzleAnchor = new SerializedObject(muzzleAnchorComponent);
            SerializedProperty muzzleTransformProperty = serializedMuzzleAnchor.FindProperty("muzzleTransform");
            SerializedProperty forwardShotOffsetProperty = serializedMuzzleAnchor.FindProperty("forwardShotOffset");
            SerializedProperty minimumPlanarDistanceProperty = serializedMuzzleAnchor.FindProperty("minimumPlanarDistanceFromPlayer");
            serializedMuzzleAnchor.Update();
            muzzleTransformProperty.objectReferenceValue = muzzleAnchorTransform;
            forwardShotOffsetProperty.floatValue = GeneratedForwardShotOffset;
            minimumPlanarDistanceProperty.floatValue = GeneratedMinimumPlanarDistance;
            serializedMuzzleAnchor.ApplyModifiedPropertiesWithoutUndo();

            PlayerWeaponVisualSet weaponVisualSet = GetOrAddComponent<PlayerWeaponVisualSet>(prefabContentsRoot);
            Transform baseGunTransform = FindRequiredChild(modelInstance.transform, BaseGunObjectName);
            Transform cannonTransform = FindRequiredChild(modelInstance.transform, CannonObjectName);
            Transform gatlingTransform = FindRequiredChild(modelInstance.transform, GatlingObjectName);
            Transform railgunTransform = FindRequiredChild(modelInstance.transform, RailgunObjectName);
            weaponVisualSet.Configure(baseGunTransform.gameObject,
                                      cannonTransform.gameObject,
                                      gatlingTransform.gameObject,
                                      railgunTransform.gameObject);

            SetLayerRecursively(prefabContentsRoot, PlayerLayer);
            SetLayerRecursively(modelInstance, PlayerLayer);
            SetLayerRecursively(muzzleAnchorObject, PlayerLayer);

            PrefabUtility.SaveAsPrefabAsset(prefabContentsRoot, PlayerVisualPrefabPath);
        }
        finally
        {
            if (prefabRoot != null)
                PrefabUtility.UnloadPrefabContents(prefabContentsRoot);
            else
                UnityEngine.Object.DestroyImmediate(prefabContentsRoot);
        }

        GameObject generatedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerVisualPrefabPath);

        if (generatedPrefab == null)
            throw new InvalidOperationException(string.Format("Failed to create generated player visual prefab at '{0}'.", PlayerVisualPrefabPath));

        return generatedPrefab;
    }

    /// <summary>
    /// Updates the authored player prefab so all gameplay shooting references point to the generated animated muzzle wrapper.
    /// </summary>
    /// <param name="playerVisualPrefab">Generated visual wrapper prefab that should be nested under the player prefab.</param>
    private static void EnsurePlayerPrefab(GameObject playerVisualPrefab)
    {
        if (playerVisualPrefab == null)
            throw new ArgumentNullException("playerVisualPrefab");

        GameObject prefabContentsRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        try
        {
            PlayerAuthoring playerAuthoring = prefabContentsRoot.GetComponent<PlayerAuthoring>();

            if (playerAuthoring == null)
                throw new InvalidOperationException(string.Format("PlayerAuthoring not found on '{0}'.", PlayerPrefabPath));

            Transform previousWeaponTransform = playerAuthoring.WeaponReference;
            GameObject visualInstance = EnsurePlayerVisualInstance(prefabContentsRoot, playerVisualPrefab, previousWeaponTransform);
            Animator resolvedAnimator = visualInstance.GetComponentInChildren<Animator>(true);
            PlayerVisualMuzzleAnchor resolvedMuzzleAnchor = visualInstance.GetComponentInChildren<PlayerVisualMuzzleAnchor>(true);

            if (resolvedAnimator == null)
                throw new InvalidOperationException("Generated player visual instance does not contain an Animator.");

            if (resolvedMuzzleAnchor == null || resolvedMuzzleAnchor.MuzzleTransform == null)
                throw new InvalidOperationException("Generated player visual instance does not contain a valid PlayerVisualMuzzleAnchor.");

            Transform muzzleTransform = resolvedMuzzleAnchor.MuzzleTransform;

            if (previousWeaponTransform != null &&
                previousWeaponTransform != muzzleTransform &&
                string.Equals(previousWeaponTransform.name, "Weapon", StringComparison.Ordinal))
            {
                muzzleTransform.position = previousWeaponTransform.position;
                muzzleTransform.rotation = previousWeaponTransform.rotation;
                muzzleTransform.localScale = Vector3.one;
                UnityEngine.Object.DestroyImmediate(previousWeaponTransform.gameObject);
            }

            SerializedObject serializedAuthoring = new SerializedObject(playerAuthoring);
            SerializedProperty weaponReferenceProperty = serializedAuthoring.FindProperty("weaponReference");
            SerializedProperty animatorComponentProperty = serializedAuthoring.FindProperty("animatorComponent");
            SerializedProperty runtimeVisualBridgePrefabProperty = serializedAuthoring.FindProperty("runtimeVisualBridgePrefab");
            serializedAuthoring.Update();
            weaponReferenceProperty.objectReferenceValue = muzzleTransform;
            animatorComponentProperty.objectReferenceValue = resolvedAnimator;
            runtimeVisualBridgePrefabProperty.objectReferenceValue = playerVisualPrefab;
            serializedAuthoring.ApplyModifiedPropertiesWithoutUndo();
            TryAssignGeneratedVisualPrefabToMasterVisualPreset(playerAuthoring.MasterPreset, playerVisualPrefab);

            EditorUtility.SetDirty(playerAuthoring);
            PrefabUtility.SaveAsPrefabAsset(prefabContentsRoot, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContentsRoot);
        }
    }

    /// <summary>
    /// Ensures the player prefab contains exactly one generated visual wrapper instance and returns it.
    /// </summary>
    /// <param name="prefabContentsRoot">Loaded player prefab root.</param>
    /// <param name="playerVisualPrefab">Generated visual wrapper prefab asset.</param>
    /// <param name="previousWeaponTransform">Previously authored weapon transform that must not be mistaken for the visual root.</param>
    /// <returns>Scene instance of the generated player visual wrapper.</returns>
    private static GameObject EnsurePlayerVisualInstance(GameObject prefabContentsRoot,
                                                         GameObject playerVisualPrefab,
                                                         Transform previousWeaponTransform)
    {
        Transform existingVisualRoot = FindPlayerVisualRoot(prefabContentsRoot.transform, previousWeaponTransform);
        GameObject correspondingSource = existingVisualRoot != null
            ? PrefabUtility.GetCorrespondingObjectFromSource(existingVisualRoot.gameObject)
            : null;

        if (correspondingSource == playerVisualPrefab)
            return existingVisualRoot.gameObject;

        if (existingVisualRoot != null)
            UnityEngine.Object.DestroyImmediate(existingVisualRoot.gameObject);

        GameObject instantiatedVisual = PrefabUtility.InstantiatePrefab(playerVisualPrefab, prefabContentsRoot.scene) as GameObject;

        if (instantiatedVisual == null)
            throw new InvalidOperationException("Unable to instantiate generated player visual prefab into the player prefab.");

        instantiatedVisual.transform.SetParent(prefabContentsRoot.transform, false);
        instantiatedVisual.transform.SetSiblingIndex(0);
        instantiatedVisual.transform.localPosition = Vector3.zero;
        instantiatedVisual.transform.localRotation = Quaternion.identity;
        instantiatedVisual.transform.localScale = Vector3.one;
        SetLayerRecursively(instantiatedVisual, prefabContentsRoot.layer);
        return instantiatedVisual;
    }
    #endregion

    #region Visual Preset Synchronization
    /// <summary>
    /// Synchronizes the generated player visual prefab with the visual preset referenced by the active master preset.
    /// </summary>
    /// <param name="masterPreset">Master preset that may own the visual preset to update.</param>
    /// <param name="playerVisualPrefab">Generated visual wrapper prefab asset.</param>
    private static void TryAssignGeneratedVisualPrefabToMasterVisualPreset(PlayerMasterPreset masterPreset, GameObject playerVisualPrefab)
    {
        if (masterPreset == null || playerVisualPrefab == null)
            return;

        PlayerVisualPreset visualPreset = masterPreset.VisualPreset;

        if (visualPreset == null)
            return;

        SerializedObject serializedPreset = new SerializedObject(visualPreset);
        SerializedProperty runtimeVisualBridgePrefabProperty = serializedPreset.FindProperty("runtimeVisualBridgePrefab");

        if (runtimeVisualBridgePrefabProperty == null)
            return;

        serializedPreset.Update();
        runtimeVisualBridgePrefabProperty.objectReferenceValue = playerVisualPrefab;
        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(visualPreset);
    }
    #endregion

    #region Animation Assets
    /// <summary>
    /// Ensures every authored fly-pack clip loops before the setup binds clips into the runtime Animator controller.
    /// </summary>
    private static void EnsureFlyAnimationPackLooping()
    {
        ModelImporter modelImporter = AssetImporter.GetAtPath(FlyAnimationPackPath) as ModelImporter;

        if (modelImporter == null)
            throw new InvalidOperationException(string.Format("Model importer not found at '{0}'.", FlyAnimationPackPath));

        ModelImporterClipAnimation[] clipAnimations = modelImporter.clipAnimations;

        if (clipAnimations == null || clipAnimations.Length == 0)
            clipAnimations = modelImporter.defaultClipAnimations;

        if (clipAnimations == null || clipAnimations.Length == 0)
            throw new InvalidOperationException(string.Format("No imported animation clips found at '{0}'.", FlyAnimationPackPath));

        bool requiresReimport = false;

        for (int clipIndex = 0; clipIndex < clipAnimations.Length; clipIndex++)
        {
            ModelImporterClipAnimation clipAnimation = clipAnimations[clipIndex];

            if (clipAnimation.loopTime)
                continue;

            clipAnimation.loopTime = true;
            requiresReimport = true;
        }

        if (!requiresReimport)
            return;

        modelImporter.clipAnimations = clipAnimations;
        modelImporter.SaveAndReimport();
    }

    /// <summary>
    /// Assigns the dedicated shoot clip into the authored animation bindings preset so tooling reflects the real setup.
    /// </summary>
    /// <param name="shootClip">Clip used by the upper-body shoot state.</param>
    private static void EnsureAnimationBindingsPreset(AnimationClip idleClip,
                                                      AnimationClip moveForwardClip,
                                                      AnimationClip moveBackwardClip,
                                                      AnimationClip moveLeftClip,
                                                      AnimationClip moveRightClip,
                                                      AnimationClip shootClip,
                                                      AnimationClip aimBackwardClip,
                                                      AnimationClip aimLeftClip,
                                                      AnimationClip aimRightClip)
    {
        PlayerAnimationBindingsPreset preset = AssetDatabase.LoadAssetAtPath<PlayerAnimationBindingsPreset>(AnimationBindingsPresetPath);

        if (preset == null)
            throw new InvalidOperationException(string.Format("Animation bindings preset not found at '{0}'.", AnimationBindingsPresetPath));

        preset.SetClip(PlayerAnimationClipSlot.Idle, idleClip);
        preset.SetClip(PlayerAnimationClipSlot.MoveForward, moveForwardClip);
        preset.SetClip(PlayerAnimationClipSlot.MoveBackward, moveBackwardClip);
        preset.SetClip(PlayerAnimationClipSlot.MoveLeft, moveLeftClip);
        preset.SetClip(PlayerAnimationClipSlot.MoveRight, moveRightClip);
        preset.SetClip(PlayerAnimationClipSlot.AimForward, idleClip);
        preset.SetClip(PlayerAnimationClipSlot.AimBackward, aimBackwardClip);
        preset.SetClip(PlayerAnimationClipSlot.AimLeft, aimLeftClip);
        preset.SetClip(PlayerAnimationClipSlot.AimRight, aimRightClip);
        preset.SetClip(PlayerAnimationClipSlot.Shoot, shootClip);
        EditorUtility.SetDirty(preset);
    }

    /// <summary>
    /// Adds or refreshes the upper-body shoot state and its transitions on the player animator controller.
    /// </summary>
    /// <param name="shootClip">Clip used by the upper-body shoot state.</param>
    private static void EnsureAnimatorController(AnimationClip idleClip,
                                                 AnimationClip moveForwardClip,
                                                 AnimationClip moveBackwardClip,
                                                 AnimationClip moveLeftClip,
                                                 AnimationClip moveRightClip,
                                                 AnimationClip shootClip,
                                                 AnimationClip aimBackwardClip,
                                                 AnimationClip aimLeftClip,
                                                 AnimationClip aimRightClip)
    {
        AnimatorController animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);

        if (animatorController == null)
            throw new InvalidOperationException(string.Format("Animator controller not found at '{0}'.", AnimatorControllerPath));

        int lowerBodyLayerIndex = FindLayerIndex(animatorController, LowerBodyLayerName);
        int upperBodyLayerIndex = FindLayerIndex(animatorController, UpperBodyLayerName);

        if (lowerBodyLayerIndex < 0 || upperBodyLayerIndex < 0)
            throw new InvalidOperationException("Animator controller is missing the required LowerBody or UpperBody layer.");

        AnimatorStateMachine lowerBodyStateMachine = animatorController.layers[lowerBodyLayerIndex].stateMachine;
        AnimatorStateMachine upperBodyStateMachine = animatorController.layers[upperBodyLayerIndex].stateMachine;
        AnimatorState lowerMoveState = FindState(lowerBodyStateMachine, LowerMoveStateName);
        AnimatorState upperAimState = FindState(upperBodyStateMachine, UpperAimStateName);

        if (lowerMoveState == null || upperAimState == null)
            throw new InvalidOperationException("Animator controller is missing the required lower-move or upper-aim state.");

        BlendTree lowerBlendTree = lowerMoveState.motion as BlendTree;
        BlendTree upperBlendTree = upperAimState.motion as BlendTree;

        if (lowerBlendTree == null || upperBlendTree == null)
            throw new InvalidOperationException("Animator controller lower-move and upper-aim states must use blend trees.");

        lowerBlendTree.blendType = BlendTreeType.SimpleDirectional2D;
        lowerBlendTree.blendParameter = "MoveX";
        lowerBlendTree.blendParameterY = "MoveY";
        lowerBlendTree.children = BuildDirectionalChildren(idleClip,
                                                           moveForwardClip,
                                                           moveBackwardClip,
                                                           moveLeftClip,
                                                           moveRightClip);

        upperBlendTree.blendType = BlendTreeType.SimpleDirectional2D;
        upperBlendTree.blendParameter = "AimX";
        upperBlendTree.blendParameterY = "AimY";
        upperBlendTree.children = BuildDirectionalChildren(idleClip,
                                                           idleClip,
                                                           aimBackwardClip,
                                                           aimLeftClip,
                                                           aimRightClip);

        AnimatorState upperShootState = FindState(upperBodyStateMachine, UpperShootStateName);

        if (upperShootState == null)
        {
            upperShootState = upperBodyStateMachine.AddState(UpperShootStateName, new Vector3(560f, 110f, 0f));
            upperShootState.writeDefaultValues = true;
        }

        upperShootState.motion = shootClip;
        upperShootState.speed = 1f;
        upperShootState.iKOnFeet = false;
        RemoveTransitions(upperAimState, upperShootState);
        RemoveTransitions(upperShootState, upperAimState);

        AnimatorStateTransition toShootTransition = upperAimState.AddTransition(upperShootState);
        toShootTransition.hasExitTime = false;
        toShootTransition.exitTime = 0f;
        toShootTransition.duration = 0.05f;
        toShootTransition.offset = 0f;
        toShootTransition.interruptionSource = TransitionInterruptionSource.None;
        toShootTransition.AddCondition(AnimatorConditionMode.If, 0f, "IsShooting");

        AnimatorStateTransition toAimTransition = upperShootState.AddTransition(upperAimState);
        toAimTransition.hasExitTime = false;
        toAimTransition.exitTime = 0f;
        toAimTransition.duration = 0.05f;
        toAimTransition.offset = 0f;
        toAimTransition.interruptionSource = TransitionInterruptionSource.None;
        toAimTransition.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsShooting");

        EditorUtility.SetDirty(animatorController);
        EditorUtility.SetDirty(lowerBlendTree);
        EditorUtility.SetDirty(upperBlendTree);
    }

    private static void EnsureAvatarMasks()
    {
        GameObject playerModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPrefabPath);

        if (playerModelPrefab == null)
            throw new InvalidOperationException(string.Format("Player model prefab not found at '{0}'.", PlayerModelPrefabPath));

        AvatarMask lowerBodyMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(LowerBodyMaskPath);
        AvatarMask upperBodyMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);

        if (lowerBodyMask == null || upperBodyMask == null)
            throw new InvalidOperationException("Player upper/lower avatar mask assets are missing.");

        ConfigureAvatarMask(lowerBodyMask, playerModelPrefab.transform, false);
        ConfigureAvatarMask(upperBodyMask, playerModelPrefab.transform, true);
    }

    private static void ConfigureAvatarMask(AvatarMask mask, Transform modelRoot, bool upperBody)
    {
        mask.transformCount = 0;
        mask.AddTransformPath(modelRoot, true);

        for (int transformIndex = 0; transformIndex < mask.transformCount; transformIndex++)
        {
            string transformPath = mask.GetTransformPath(transformIndex);
            bool isSpinePath = transformPath.StartsWith("PlayerRig/ROOT/spine", StringComparison.Ordinal);
            bool isLowerBodyPath = transformPath.StartsWith("PlayerRig/ROOT/spine/pelvis.", StringComparison.Ordinal) ||
                                   transformPath.StartsWith("PlayerRig/ROOT/spine/thigh.", StringComparison.Ordinal);
            bool isRigRootPath = string.Equals(transformPath, "PlayerRig", StringComparison.Ordinal) ||
                                 string.Equals(transformPath, "PlayerRig/ROOT", StringComparison.Ordinal);
            bool isRootPath = string.IsNullOrEmpty(transformPath);
            bool isActive = upperBody
                ? isSpinePath && !isLowerBodyPath
                : isRootPath || isRigRootPath || isLowerBodyPath;

            if (transformPath.StartsWith(BaseGunObjectName, StringComparison.Ordinal) ||
                transformPath.StartsWith(CannonObjectName, StringComparison.Ordinal) ||
                transformPath.StartsWith(GatlingObjectName, StringComparison.Ordinal) ||
                transformPath.StartsWith(RailgunObjectName, StringComparison.Ordinal) ||
                transformPath.StartsWith("CH_Player", StringComparison.Ordinal))
                isActive = false;

            mask.SetTransformActive(transformIndex, isActive);
        }

        for (int bodyPartIndex = 0; bodyPartIndex < (int)AvatarMaskBodyPart.LastBodyPart; bodyPartIndex++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)bodyPartIndex, false);

        if (upperBody)
        {
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
        }
        else
        {
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, true);
        }

        EditorUtility.SetDirty(mask);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Loads the primary authored animation clip stored inside one imported FBX asset.
    /// </summary>
    /// <param name="clipAssetPath">Path of the imported FBX animation asset.</param>
    /// <returns>Primary non-preview animation clip.</returns>
    private static AnimationClip LoadPrimaryAnimationClip(string clipAssetPath)
    {
        string clipName = Path.GetFileNameWithoutExtension(clipAssetPath);
        UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(clipAssetPath);

        for (int assetIndex = 0; assetIndex < subAssets.Length; assetIndex++)
        {
            AnimationClip clip = subAssets[assetIndex] as AnimationClip;

            if (clip == null)
                continue;

            if (string.Equals(clip.name, clipName, StringComparison.Ordinal))
                return clip;
        }

        for (int assetIndex = 0; assetIndex < subAssets.Length; assetIndex++)
        {
            AnimationClip clip = subAssets[assetIndex] as AnimationClip;

            if (clip != null)
                return clip;
        }

        throw new InvalidOperationException(string.Format("No animation clip found at '{0}'.", clipAssetPath));
    }

    private static AnimationClip LoadAnimationClip(string clipAssetPath, string clipName)
    {
        UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(clipAssetPath);

        for (int assetIndex = 0; assetIndex < subAssets.Length; assetIndex++)
        {
            AnimationClip clip = subAssets[assetIndex] as AnimationClip;

            if (clip != null && string.Equals(clip.name, clipName, StringComparison.Ordinal))
                return clip;
        }

        throw new InvalidOperationException(string.Format("Animation clip '{0}' not found at '{1}'.", clipName, clipAssetPath));
    }

    private static ChildMotion[] BuildDirectionalChildren(AnimationClip centerClip,
                                                          AnimationClip forwardClip,
                                                          AnimationClip backwardClip,
                                                          AnimationClip leftClip,
                                                          AnimationClip rightClip)
    {
        return new[]
        {
            new ChildMotion { motion = centerClip, position = Vector2.zero, timeScale = 1f },
            new ChildMotion { motion = forwardClip, position = Vector2.up, timeScale = 1f },
            new ChildMotion { motion = backwardClip, position = Vector2.down, timeScale = 1f },
            new ChildMotion { motion = leftClip, position = Vector2.left, timeScale = 1f },
            new ChildMotion { motion = rightClip, position = Vector2.right, timeScale = 1f }
        };
    }

    /// <summary>
    /// Recursively creates a folder chain inside the Unity project when one or more path segments are missing.
    /// </summary>
    /// <param name="folderPath">Project-relative folder path that must exist.</param>
    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            return;

        string normalizedFolderPath = folderPath.Replace("\\", "/");
        string[] segments = normalizedFolderPath.Split('/');
        string currentPath = segments[0];

        for (int segmentIndex = 1; segmentIndex < segments.Length; segmentIndex++)
        {
            string nextPath = currentPath + "/" + segments[segmentIndex];

            if (!AssetDatabase.IsValidFolder(nextPath))
                AssetDatabase.CreateFolder(currentPath, segments[segmentIndex]);

            currentPath = nextPath;
        }
    }

    /// <summary>
    /// Finds one child transform anywhere in the hierarchy by exact name.
    /// </summary>
    /// <param name="root">Root transform used to start the search.</param>
    /// <param name="targetName">Exact child-object name to resolve.</param>
    /// <returns>Matching transform or null when not found.</returns>
    private static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null)
            return null;

        if (string.Equals(root.name, targetName, StringComparison.Ordinal))
            return root;

        for (int childIndex = 0; childIndex < root.childCount; childIndex++)
        {
            Transform resolvedTransform = FindChildRecursive(root.GetChild(childIndex), targetName);

            if (resolvedTransform != null)
                return resolvedTransform;
        }

        return null;
    }

    private static Transform FindRequiredChild(Transform root, string targetName)
    {
        Transform resolvedTransform = FindChildRecursive(root, targetName);

        if (resolvedTransform == null)
            throw new InvalidOperationException(string.Format("Unable to find '{0}' inside the player model hierarchy.", targetName));

        return resolvedTransform;
    }

    /// <summary>
    /// Finds the direct child under the player prefab root that represents the visual hierarchy.
    /// </summary>
    /// <param name="root">Player prefab root transform.</param>
    /// <param name="previousWeaponTransform">Current authored weapon transform that must be ignored.</param>
    /// <returns>Direct-child visual root or null when no visual hierarchy is present.</returns>
    private static Transform FindPlayerVisualRoot(Transform root, Transform previousWeaponTransform)
    {
        for (int childIndex = 0; childIndex < root.childCount; childIndex++)
        {
            Transform childTransform = root.GetChild(childIndex);

            if (childTransform == previousWeaponTransform)
                continue;

            if (childTransform.GetComponentInChildren<Animator>(true) == null)
                continue;

            return childTransform;
        }

        return null;
    }

    /// <summary>
    /// Recursively applies the same layer value to one object hierarchy.
    /// </summary>
    /// <param name="targetObject">Root object whose hierarchy should receive the layer.</param>
    /// <param name="layer">Layer value applied to the full hierarchy.</param>
    private static void SetLayerRecursively(GameObject targetObject, int layer)
    {
        if (targetObject == null)
            return;

        targetObject.layer = layer;

        for (int childIndex = 0; childIndex < targetObject.transform.childCount; childIndex++)
            SetLayerRecursively(targetObject.transform.GetChild(childIndex).gameObject, layer);
    }

    /// <summary>
    /// Removes all direct children under one transform.
    /// </summary>
    /// <param name="parent">Parent transform whose full child list should be cleared.</param>
    private static void DestroyAllChildren(Transform parent)
    {
        for (int childIndex = parent.childCount - 1; childIndex >= 0; childIndex--)
            UnityEngine.Object.DestroyImmediate(parent.GetChild(childIndex).gameObject);
    }

    /// <summary>
    /// Resolves a stable local muzzle anchor position from the gun mesh bounds so spawned shots align with the weapon instead of the mesh pivot.
    /// </summary>
    /// <param name="gunMeshTransform">Gun hierarchy transform used as the animated local-space reference.</param>
    /// <returns>Local-space muzzle position relative to the gun transform.</returns>
    private static Vector3 ResolveMuzzleAnchorLocalPosition(Transform gunMeshTransform)
    {
        Bounds localBounds;

        if (TryResolveLocalGunBounds(gunMeshTransform, out localBounds))
        {
            Vector3 localCenter = localBounds.center;
            return new Vector3(0f, localCenter.y, localBounds.max.z + MuzzleForwardPadding);
        }

        return Vector3.zero;
    }

    /// <summary>
    /// Returns the existing component on one GameObject or adds it when missing.
    /// </summary>
    /// <param name="targetObject">GameObject receiving the requested component.</param>
    /// <returns>Existing or newly added component instance.</returns>
    private static TComponent GetOrAddComponent<TComponent>(GameObject targetObject) where TComponent : Component
    {
        TComponent component = targetObject.GetComponent<TComponent>();

        if (component != null)
            return component;

        return targetObject.AddComponent<TComponent>();
    }

    /// <summary>
    /// Resolves local-space bounds for the authored gun mesh using the most accurate renderer or mesh source available.
    /// </summary>
    /// <param name="gunMeshTransform">Gun transform whose mesh bounds should be read.</param>
    /// <returns>True when local bounds were resolved successfully, otherwise false.</returns>
    private static bool TryResolveLocalGunBounds(Transform gunMeshTransform, out Bounds localBounds)
    {
        localBounds = default;

        if (gunMeshTransform == null)
            return false;

        SkinnedMeshRenderer skinnedMeshRenderer = gunMeshTransform.GetComponent<SkinnedMeshRenderer>();

        if (skinnedMeshRenderer != null)
        {
            localBounds = skinnedMeshRenderer.localBounds;
            return true;
        }

        MeshFilter meshFilter = gunMeshTransform.GetComponent<MeshFilter>();

        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            localBounds = meshFilter.sharedMesh.bounds;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Finds one animator state by exact name inside one state machine.
    /// </summary>
    /// <param name="stateMachine">State machine that owns the state list.</param>
    /// <param name="stateName">Exact state name to resolve.</param>
    /// <returns>Matching animator state or null when not found.</returns>
    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
    {
        ChildAnimatorState[] states = stateMachine.states;

        for (int stateIndex = 0; stateIndex < states.Length; stateIndex++)
        {
            AnimatorState candidateState = states[stateIndex].state;

            if (candidateState != null && string.Equals(candidateState.name, stateName, StringComparison.Ordinal))
                return candidateState;
        }

        return null;
    }

    /// <summary>
    /// Finds the index of one animator layer by exact name.
    /// </summary>
    /// <param name="animatorController">Controller that owns the layer list.</param>
    /// <param name="layerName">Exact layer name to resolve.</param>
    /// <returns>Layer index or -1 when not found.</returns>
    private static int FindLayerIndex(AnimatorController animatorController, string layerName)
    {
        AnimatorControllerLayer[] layers = animatorController.layers;

        for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
        {
            if (string.Equals(layers[layerIndex].name, layerName, StringComparison.Ordinal))
                return layerIndex;
        }

        return -1;
    }

    /// <summary>
    /// Removes all transitions between one source state and one destination state.
    /// </summary>
    /// <param name="sourceState">Source state whose transition list should be filtered.</param>
    /// <param name="destinationState">Destination state to remove from the transition list.</param>
    private static void RemoveTransitions(AnimatorState sourceState, AnimatorState destinationState)
    {
        AnimatorStateTransition[] transitions = sourceState.transitions;

        for (int transitionIndex = transitions.Length - 1; transitionIndex >= 0; transitionIndex--)
        {
            AnimatorStateTransition transition = transitions[transitionIndex];

            if (transition.destinationState != destinationState)
                continue;

            sourceState.RemoveTransition(transition);
        }
    }
    #endregion

    #endregion
}
