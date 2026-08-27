#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Authors off-map train starts, center arrivals, and portal links through the Room Clear Rewards transform pipeline.
/// </summary>
public static class GameRoomTrainArrivalProjectSetupUtility
{
    #region Constants
    public const string WestTrainBindingId = "TrainWestArrival";
    public const string EastTrainBindingId = "TrainEastArrival";
    public const float ArrivalDurationSeconds = 3.5f;
    public static readonly Vector3 WestArrivalOffset = new Vector3(0f, 0f, 56.68f);
    public static readonly Vector3 EastArrivalOffset = new Vector3(0f, 0f, -70.7f);

    private const string defaultPresetPath =
        "Assets/Scriptable Objects/Game/Room Clear Rewards/GameRoomClearRewardsPreset.asset";
    private const string legacyTrainBindingId = "Train01";
    private const string westTrainName = "SM_Train";
    private const string eastTrainName = "SM_Train (1)";
    internal const float WestInitialLocalZ = -80.7f;
    internal const float EastInitialLocalZ = 81.8f;

    private static readonly string[] managedTrainPrefabPaths =
    {
        "Assets/Prefabs/RoomAuthoring/Managed/PF_LGTEST_MetroManagedEnvironment.prefab",
        "Assets/Prefabs/RoomAuthoring/Managed/PF_LGTEST_MaintenanceManagedEnvironment.prefab"
    };
    #endregion

    #region Properties
    internal static IReadOnlyList<string> ManagedTrainPrefabPaths => managedTrainPrefabPaths;
    #endregion

    #region Methods

    #region Entry Points
    // [MenuItem("Tools/Game Management/Room Clear Rewards/Apply Train Arrivals")]
    /// <summary>
    /// Executes the idempotent train setup independently for batch verification or project maintenance.
    /// </summary>
    public static void ExecuteBatchSetup()
    {
        Configure();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("[GameRoomTrainArrivalProjectSetupUtility] Train arrivals and portal links configured.");
    }

    /// <summary>
    /// Configures the default reward preset, managed train prefabs, and every scene depending on those prefabs.
    /// </summary>
    public static void Configure()
    {
        ConfigureArrivalPreset();
        ConfigureManagedTrainPrefabs();
        ConfigureDependentScenes();
        GameTrainSceneTestingPresetProjectSetupUtility.Configure();
    }
    #endregion

    #region Preset Configuration
    /// <summary>
    /// Replaces the former direct train clip with two directional, center-decelerating transform arrivals.
    /// </summary>
    private static void ConfigureArrivalPreset()
    {
        GameRoomClearRewardsPreset preset =
            AssetDatabase.LoadAssetAtPath<GameRoomClearRewardsPreset>(defaultPresetPath);

        if (preset == null)
            throw new InvalidOperationException("The default Room Clear Rewards preset is missing.");

        preset.EnsureInitialized();
        SerializedObject serializedPreset = new SerializedObject(preset);
        serializedPreset.Update();
        SerializedProperty animations = serializedPreset.FindProperty("portalLogSettings.activationAnimations");

        if (animations == null)
            throw new InvalidOperationException("The default Room Clear Rewards preset has no portal animation list.");

        // Remove only train-owned definitions while preserving every unrelated portal animation in authored order.
        for (int animationIndex = animations.arraySize - 1; animationIndex >= 0; animationIndex--)
        {
            SerializedProperty animation = animations.GetArrayElementAtIndex(animationIndex);
            string bindingId = animation.FindPropertyRelative("targetBindingId").stringValue;

            if (IsTrainBinding(bindingId))
                animations.DeleteArrayElementAtIndex(animationIndex);
        }

        AppendArrivalAnimation(animations, WestTrainBindingId, WestArrivalOffset);
        AppendArrivalAnimation(animations, EastTrainBindingId, EastArrivalOffset);
        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(preset);
    }

    /// <summary>
    /// Appends one safe once-only transform arrival to the serialized portal animation list.
    /// </summary>
    /// <param name="animations">Serialized portal animation list receiving the definition.</param>
    /// <param name="bindingId">Directional train binding consumed by managed portal anchors.</param>
    /// <param name="positionOffset">Local parent-space displacement ending at the playable rail center.</param>
    private static void AppendArrivalAnimation(SerializedProperty animations,
                                               string bindingId,
                                               Vector3 positionOffset)
    {
        int animationIndex = animations.arraySize;
        animations.InsertArrayElementAtIndex(animationIndex);
        SerializedProperty animation = animations.GetArrayElementAtIndex(animationIndex);
        animation.FindPropertyRelative("targetBindingId").stringValue = bindingId;
        animation.FindPropertyRelative("targetSlot").intValue = 0;
        animation.FindPropertyRelative("source").enumValueIndex =
            (int)GameRoomPortalActivationAnimationSource.Transform;
        animation.FindPropertyRelative("mode").enumValueIndex =
            (int)GameRoomPortalTransformAnimationMode.Position;
        animation.FindPropertyRelative("playback").enumValueIndex =
            (int)GameRoomPortalTransformAnimationPlayback.Once;
        animation.FindPropertyRelative("easing").enumValueIndex =
            (int)GameRoomPortalTransformAnimationEase.SmootherStep;
        animation.FindPropertyRelative("startDelay").floatValue = 0.15f;
        animation.FindPropertyRelative("duration").floatValue = ArrivalDurationSeconds;
        animation.FindPropertyRelative("positionOffset").vector3Value = positionOffset;
        animation.FindPropertyRelative("rotationOffset").vector3Value = Vector3.zero;
        animation.FindPropertyRelative("scaleMultiplier").vector3Value = Vector3.one;
        animation.FindPropertyRelative("animatorClip").objectReferenceValue = null;
        animation.FindPropertyRelative("animatorPath").stringValue = string.Empty;
        animation.FindPropertyRelative("animatorSpeed").floatValue = 1f;
    }
    #endregion

    #region Prefab Configuration
    /// <summary>
    /// Moves both managed train roots beyond opposite rail extremes and disables obsolete controller playback.
    /// </summary>
    private static void ConfigureManagedTrainPrefabs()
    {
        for (int prefabIndex = 0; prefabIndex < managedTrainPrefabPaths.Length; prefabIndex++)
        {
            string prefabPath = managedTrainPrefabPaths[prefabIndex];

            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                continue;

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                Transform westTrain = FindDescendant(prefabRoot.transform, westTrainName);
                Transform eastTrain = FindDescendant(prefabRoot.transform, eastTrainName);
                bool changed = ConfigureTrainRoot(westTrain, WestInitialLocalZ);
                changed |= ConfigureTrainRoot(eastTrain, EastInitialLocalZ);

                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }

    /// <summary>
    /// Applies one off-map local position and removes the legacy Animator controller from a train root.
    /// </summary>
    /// <param name="train">Managed train transform to configure.</param>
    /// <param name="initialLocalZ">Local rail coordinate beyond the playable room bounds.</param>
    /// <returns>True when the prefab required a serialized change.</returns>
    private static bool ConfigureTrainRoot(Transform train, float initialLocalZ)
    {
        if (train == null)
            return false;

        bool changed = false;
        Animator animator = train.GetComponent<Animator>();

        // Remove controller ownership before authoring the pose so Animator teardown cannot restore its clip value.
        if (animator != null &&
            (animator.enabled || animator.runtimeAnimatorController != null || animator.applyRootMotion))
        {
            animator.enabled = false;
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            changed = true;
        }

        Vector3 position = train.localPosition;

        if (!Mathf.Approximately(position.z, initialLocalZ))
        {
            position.z = initialLocalZ;
            train.localPosition = position;
            changed = true;
        }

        return changed;
    }
    #endregion

    #region Scene Configuration
    /// <summary>
    /// Opens only scenes that depend on a managed train prefab and assigns directional portal effect bindings.
    /// </summary>
    private static void ConfigureDependentScenes()
    {
        IReadOnlyList<string> scenePaths = CollectDependentScenePaths();
        Scene previouslyActiveScene = SceneManager.GetActiveScene();

        for (int sceneIndex = 0; sceneIndex < scenePaths.Count; sceneIndex++)
            ConfigureScene(scenePaths[sceneIndex]);

        if (previouslyActiveScene.IsValid() && previouslyActiveScene.isLoaded)
            SceneManager.SetActiveScene(previouslyActiveScene);
    }

    /// <summary>
    /// Collects project-relative scene paths whose dependency graph contains a managed train environment prefab.
    /// </summary>
    /// <returns>Stable list of managed scenes containing the configured train hierarchy.</returns>
    internal static List<string> CollectDependentScenePaths()
    {
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        List<string> scenePaths = new List<string>();

        for (int sceneIndex = 0; sceneIndex < sceneGuids.Length; sceneIndex++)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[sceneIndex]);

            if (DependsOnManagedTrainPrefab(scenePath))
                scenePaths.Add(scenePath);
        }

        scenePaths.Sort(StringComparer.Ordinal);
        return scenePaths;
    }

    /// <summary>
    /// Reports whether one managed scene directly or transitively references a configured train environment prefab.
    /// </summary>
    /// <param name="scenePath">Project-relative scene asset path.</param>
    /// <returns>True when a known managed train prefab occurs in the scene dependency graph.</returns>
    private static bool DependsOnManagedTrainPrefab(string scenePath)
    {
        string[] dependencies = AssetDatabase.GetDependencies(scenePath, true);

        for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
        {
            for (int prefabIndex = 0; prefabIndex < managedTrainPrefabPaths.Length; prefabIndex++)
            {
                if (string.Equals(dependencies[dependencyIndex],
                                  managedTrainPrefabPaths[prefabIndex],
                                  StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Rebinds east and west portal effect views to the corresponding inherited train instance.
    /// </summary>
    /// <param name="scenePath">Project-relative managed scene path to update.</param>
    private static void ConfigureScene(string scenePath)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;

        if (!wasLoaded)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        try
        {
            FindSceneTrains(scene, out Transform westTrain, out Transform eastTrain);

            if (westTrain == null && eastTrain == null)
                return;

            bool changed = ConfigureTrainInstance(westTrain, WestInitialLocalZ);
            changed |= ConfigureTrainInstance(eastTrain, EastInitialLocalZ);
            List<GameRoomPortalRewardLogAnchor> anchors = new List<GameRoomPortalRewardLogAnchor>(8);
            CollectSceneAnchors(scene, anchors);
            bool useVerticalFallback =
                GameRoomTrainPortalPlacementUtility.RequiresVerticalFallback(anchors);

            if (useVerticalFallback)
            {
                changed |= GameRoomTrainPortalPlacementUtility.ConfigureVerticalFallback(
                    scene,
                    anchors,
                    westTrain,
                    eastTrain);
            }

            // Bind each train-facing managed portal after fallback authoritative centers have been synchronized.
            for (int anchorIndex = 0; anchorIndex < anchors.Count; anchorIndex++)
            {
                changed |= ConfigurePortalBindings(anchors[anchorIndex],
                                                   westTrain,
                                                   eastTrain,
                                                   useVerticalFallback);
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally
        {
            if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    /// <summary>
    /// Collects every managed portal anchor across scene roots without relying on scene-global object searches.
    /// </summary>
    /// <param name="scene">Loaded managed room scene to inspect.</param>
    /// <param name="anchors">Destination list replaced with all discovered anchors.</param>
    private static void CollectSceneAnchors(Scene scene,
                                            List<GameRoomPortalRewardLogAnchor> anchors)
    {
        anchors.Clear();
        GameObject[] roots = scene.GetRootGameObjects();
        List<GameRoomPortalRewardLogAnchor> rootAnchors =
            new List<GameRoomPortalRewardLogAnchor>(8);

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            rootAnchors.Clear();
            roots[rootIndex].GetComponentsInChildren(true, rootAnchors);
            anchors.AddRange(rootAnchors);
        }
    }

    /// <summary>
    /// Normalizes one scene train and records prefab-instance overrides needed to preserve its authored start.
    /// </summary>
    /// <param name="train">Scene train root to configure, or null when the direction is absent.</param>
    /// <param name="initialLocalZ">Local rail coordinate beyond the playable room bounds.</param>
    /// <returns>True when the scene instance required a serialized change.</returns>
    private static bool ConfigureTrainInstance(Transform train, float initialLocalZ)
    {
        if (!ConfigureTrainRoot(train, initialLocalZ))
            return false;

        EditorUtility.SetDirty(train);

        if (PrefabUtility.IsPartOfPrefabInstance(train))
            PrefabUtility.RecordPrefabInstancePropertyModifications(train);

        Animator animator = train.GetComponent<Animator>();

        if (animator != null)
        {
            EditorUtility.SetDirty(animator);

            if (PrefabUtility.IsPartOfPrefabInstance(animator))
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        }

        return true;
    }

    /// <summary>
    /// Finds the two directional train roots inherited by one managed room scene.
    /// </summary>
    /// <param name="scene">Loaded managed room scene to inspect.</param>
    /// <param name="westTrain">Resolved west-side train root.</param>
    /// <param name="eastTrain">Resolved east-side train root.</param>
    internal static void FindSceneTrains(Scene scene, out Transform westTrain, out Transform eastTrain)
    {
        westTrain = null;
        eastTrain = null;
        GameObject[] roots = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            if (westTrain == null)
                westTrain = FindDescendant(roots[rootIndex].transform, westTrainName);

            if (eastTrain == null)
                eastTrain = FindDescendant(roots[rootIndex].transform, eastTrainName);
        }
    }

    /// <summary>
    /// Preserves unrelated linked objects while replacing legacy train links with one directional binding.
    /// </summary>
    /// <param name="anchor">Managed portal presentation anchor to update.</param>
    /// <param name="westTrain">West-side train available in the room.</param>
    /// <param name="eastTrain">East-side train available in the room.</param>
    /// <param name="useVerticalFallback">True when south and north portals replace missing west and east exits.</param>
    /// <returns>True when the anchor's serialized bindings changed.</returns>
    private static bool ConfigurePortalBindings(GameRoomPortalRewardLogAnchor anchor,
                                                Transform westTrain,
                                                Transform eastTrain,
                                                bool useVerticalFallback)
    {
        if (anchor == null || anchor.EffectView == null)
            return false;

        bool hasTrainSide = GameRoomTrainPortalPlacementUtility.TryResolveTrainSide(
            anchor.PortalId,
            useVerticalFallback,
            out bool usesWestTrain);
        Transform desiredTrainTransform = hasTrainSide
            ? usesWestTrain
                ? westTrain
                : eastTrain
            : null;
        GameObject desiredTrain = desiredTrainTransform != null
            ? desiredTrainTransform.gameObject
            : null;
        string desiredBindingId = usesWestTrain ? WestTrainBindingId : EastTrainBindingId;
        string desiredDisplayName = usesWestTrain ? "West Train Arrival" : "East Train Arrival";
        IReadOnlyList<GameRoomPortalLinkedObjectBinding> existingBindings = anchor.EffectView.LinkedObjects;
        List<GameRoomPortalLinkedObjectBinding> resolvedBindings =
            new List<GameRoomPortalLinkedObjectBinding>(existingBindings.Count + 1);
        bool trainBound = false;

        // Retain freely authored objects while normalizing or removing only setup-owned train bindings.
        for (int bindingIndex = 0; bindingIndex < existingBindings.Count; bindingIndex++)
        {
            GameRoomPortalLinkedObjectBinding binding = existingBindings[bindingIndex];

            if (binding == null ||
                string.IsNullOrWhiteSpace(binding.BindingId) ||
                binding.TargetObject == null)
            {
                continue;
            }

            bool targetsTrain = westTrain != null && binding.TargetObject == westTrain.gameObject ||
                                eastTrain != null && binding.TargetObject == eastTrain.gameObject;

            if (!targetsTrain && !IsTrainBinding(binding.BindingId))
            {
                resolvedBindings.Add(binding);
                continue;
            }

            if (desiredTrain == null || trainBound)
                continue;

            resolvedBindings.Add(new GameRoomPortalLinkedObjectBinding(desiredBindingId,
                                                                        desiredDisplayName,
                                                                        desiredTrain));
            trainBound = true;
        }

        if (desiredTrain != null && !trainBound)
            resolvedBindings.Add(new GameRoomPortalLinkedObjectBinding(desiredBindingId,
                                                                        desiredDisplayName,
                                                                        desiredTrain));

        if (BindingsMatch(existingBindings, resolvedBindings))
            return false;

        anchor.EffectView.ConfigureAuthoring(resolvedBindings.ToArray());
        PrefabUtility.RecordPrefabInstancePropertyModifications(anchor.EffectView);
        EditorUtility.SetDirty(anchor.EffectView);
        return true;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Finds one exact named transform below a hierarchy without relying on scene-global searches.
    /// </summary>
    /// <param name="root">Hierarchy root to inspect.</param>
    /// <param name="targetName">Exact transform name to resolve.</param>
    /// <returns>First exact named descendant, or null when absent.</returns>
    internal static Transform FindDescendant(Transform root, string targetName)
    {
        if (string.Equals(root.name, targetName, StringComparison.Ordinal))
            return root;

        for (int childIndex = 0; childIndex < root.childCount; childIndex++)
        {
            Transform match = FindDescendant(root.GetChild(childIndex), targetName);

            if (match != null)
                return match;
        }

        return null;
    }

    /// <summary>
    /// Reports whether a stable identifier belongs to the legacy or directional train setup.
    /// </summary>
    /// <param name="bindingId">Binding identifier to inspect.</param>
    /// <returns>True when the identifier is owned by train arrival setup.</returns>
    private static bool IsTrainBinding(string bindingId)
    {
        return string.Equals(bindingId, legacyTrainBindingId, StringComparison.Ordinal) ||
               string.Equals(bindingId, WestTrainBindingId, StringComparison.Ordinal) ||
               string.Equals(bindingId, EastTrainBindingId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Checks whether one portal identity contains a directional token without culture-dependent comparisons.
    /// </summary>
    /// <param name="portalId">Stable portal identity.</param>
    /// <param name="direction">Direction token to locate.</param>
    /// <returns>True when the token occurs in the portal identity.</returns>
    internal static bool ContainsDirection(string portalId, string direction)
    {
        return !string.IsNullOrWhiteSpace(portalId) &&
               portalId.IndexOf(direction, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Compares binding identity, label, and exact scene target order to avoid unnecessary scene serialization.
    /// </summary>
    /// <param name="left">Existing serialized binding sequence.</param>
    /// <param name="right">Resolved binding sequence.</param>
    /// <returns>True when both sequences describe the same portal links.</returns>
    private static bool BindingsMatch(IReadOnlyList<GameRoomPortalLinkedObjectBinding> left,
                                      IReadOnlyList<GameRoomPortalLinkedObjectBinding> right)
    {
        if (left.Count != right.Count)
            return false;

        for (int bindingIndex = 0; bindingIndex < left.Count; bindingIndex++)
        {
            GameRoomPortalLinkedObjectBinding leftBinding = left[bindingIndex];
            GameRoomPortalLinkedObjectBinding rightBinding = right[bindingIndex];

            if (leftBinding == null || rightBinding == null)
            {
                if (leftBinding != rightBinding)
                    return false;

                continue;
            }

            if (!string.Equals(leftBinding.BindingId, rightBinding.BindingId, StringComparison.Ordinal) ||
                !string.Equals(leftBinding.DisplayName, rightBinding.DisplayName, StringComparison.Ordinal) ||
                leftBinding.TargetObject != rightBinding.TargetObject)
                return false;
        }

        return true;
    }
    #endregion

    #endregion
}
#endif
