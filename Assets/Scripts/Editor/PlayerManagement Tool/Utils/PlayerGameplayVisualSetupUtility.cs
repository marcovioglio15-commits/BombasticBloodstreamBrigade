using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds and refreshes the authored player visual assets required by gameplay integration.
/// This setup deliberately preserves authored Animator states, transitions, and animation clip assignments.
/// None.
/// </summary>
public static class PlayerGameplayVisualSetupUtility
{
    #region Constants
    private const string PlayerPrefabPath = "Assets/Prefabs/Player/PF_Player.prefab";
    private const string PlayerVisualPrefabPath = "Assets/Prefabs/Player/PF_PlayerVisual.prefab";
    private const string PlayerModelPrefabPath = "Assets/3D/Character/SK_PlayerFinal.fbx";
    private const string AnimatorControllerPath = "Assets/3D/Testing/PlayerTest/Animation Contorller/AC_PlayerTesting.controller";
    private const string LowerBodyMaskPath = "Assets/3D/Testing/PlayerTest/Avatar Masks/AM_PlayerTesting_Lower.mask";
    private const string UpperBodyMaskPath = "Assets/3D/Testing/PlayerTest/Avatar Masks/AM_PlayerTesting_Upper.mask";
    private const string GunBarrelObjectName = "GunBarrel";
    private const string BaseGunObjectName = "base gun";
    private const string CannonObjectName = "cannon";
    private const string GatlingObjectName = "gatling";
    private const string RailgunObjectName = "railgun";
    private const string MuzzleAnchorObjectName = "MuzzleAnchor";
    private const int PlayerLayer = 3;
    private const float MuzzleForwardPadding = 0.01f;
    private const float GeneratedForwardShotOffset = 0.14f;
    private const float GeneratedMinimumPlanarDistance = 0.72f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Runs the visual authoring setup without mutating Animation Bindings or Animator controller state machines.
    /// None.
    /// </summary>
    public static void ExecuteSetup()
    {
        EnsureAvatarMasks();
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
            weaponVisualSet.Configure(baseGunTransform.gameObject);

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

    #endregion

    #endregion
}
