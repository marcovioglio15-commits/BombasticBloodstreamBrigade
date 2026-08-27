#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Verifies transform-based train arrivals, off-map authored starts, and directional portal links in managed rooms.
/// </summary>
public static class GameRoomTrainArrivalSmokeTest
{
    #region Constants
    private const string defaultPresetPath =
        "Assets/Scriptable Objects/Game/Room Clear Rewards/GameRoomClearRewardsPreset.asset";
    private const string startSceneId = "SCN_MAIN_METRO_START";
    private const string trainSceneId = "SCN_LGTEST_METRO_NS";
    private const string bossSceneId = "SCN_LGTEST_METRO_BOSS";
    private const uint testingPresetSeed = 41867u;
    #endregion

    #region Methods

    #region Entry Point
    // [MenuItem("Tools/Game Management/Room Clear Rewards/Run Train Arrival Smoke Test")]
    /// <summary>
    /// Runs deterministic preset, prefab, and managed-scene checks after train project setup.
    /// </summary>
    public static void Run()
    {
        ValidatePreset();
        ValidateTestingPreset();
        ValidatePrefabs();
        ValidateScenes();
        Debug.Log("[GameRoomTrainArrivalSmokeTest] All checks passed.");
    }
    #endregion

    #region Testing Preset Validation
    /// <summary>
    /// Verifies the registered testing preset always generates Start, N/S train, and Boss at depths zero through two.
    /// </summary>
    private static void ValidateTestingPreset()
    {
        GameProceduralLevelPreset preset =
            AssetDatabase.LoadAssetAtPath<GameProceduralLevelPreset>(
                GameTrainSceneTestingPresetProjectSetupUtility.TargetPresetPath);
        Require(preset != null, "TestingTrainScenes preset is missing.");
        Require(string.Equals(preset.PresetName, "TestingTrainScenes", StringComparison.Ordinal),
                "TestingTrainScenes does not retain its expected display name.");
        Require(preset.GenerationSettings.MaximumNodeCount == 5,
                "TestingTrainScenes maximum node count is not deterministic.");
        Require(preset.GenerationSettings.MaximumDepth == 2,
                "TestingTrainScenes maximum depth is not deterministic.");
        Require(preset.Levels.Count == 1 && preset.Levels[0] != null,
                "TestingTrainScenes must contain exactly one configured level.");
        GameProceduralLevelDefinition level = preset.Levels[0];
        Require(level.Enabled, "TestingTrainScenes level is disabled.");
        Require(level.TargetNodeCountRange == new Vector2Int(5, 5),
                "TestingTrainScenes target node count is not fixed to five.");
        Require(level.UseCenterArrival,
                "TestingTrainScenes must use center arrival so every required Start exit can reach a train copy.");
        Require(level.RoomTiles.Count == 3,
                "TestingTrainScenes must contain exactly three room tiles.");
        ValidateTestingTile(level.RoomTiles[0], startSceneId, GameProceduralRoomRole.Start, 0, 1);
        ValidateTestingTile(level.RoomTiles[1], trainSceneId, GameProceduralRoomRole.Regular, 1, 3);
        ValidateTestingTile(level.RoomTiles[2], bossSceneId, GameProceduralRoomRole.Boss, 2, 1);

        // Run the production solver so exact-depth authoring is proven by the generated graph.
        GameProceduralLevelGenerationResult result =
            GameProceduralLevelSolver.Generate(preset, level, testingPresetSeed);
        Require(result.Success,
                "TestingTrainScenes generation failed: " + result.FailureCode + " — " + result.Diagnostic);
        Require(result.Nodes.Count == 5,
                "TestingTrainScenes generated an unexpected node count.");
        ValidateGeneratedNode(result.Nodes[0], startSceneId, GameProceduralRoomRole.Start, 0);

        // Every possible first transition after Start must enter an N/S train-room copy.
        for (int nodeIndex = 1; nodeIndex < result.Nodes.Count - 1; nodeIndex++)
            ValidateGeneratedNode(result.Nodes[nodeIndex],
                                  trainSceneId,
                                  GameProceduralRoomRole.Regular,
                                  1);

        ValidateGeneratedNode(result.Nodes[4], bossSceneId, GameProceduralRoomRole.Boss, 2);

        GameProceduralLevelPresetLibrary library =
            AssetDatabase.LoadAssetAtPath<GameProceduralLevelPresetLibrary>(
                GameProceduralLevelPresetLibraryUtility.DefaultLibraryPath);
        Require(library != null && IsPresetRegistered(library, preset),
                "TestingTrainScenes is not registered in the Procedural Level preset library.");
    }

    /// <summary>
    /// Reports whether the Procedural Level library contains the exact testing preset asset reference.
    /// </summary>
    /// <param name="library">Preset library to inspect.</param>
    /// <param name="preset">Expected registered preset.</param>
    /// <returns>True when the exact preset reference is registered.</returns>
    private static bool IsPresetRegistered(GameProceduralLevelPresetLibrary library,
                                           GameProceduralLevelPreset preset)
    {
        for (int presetIndex = 0; presetIndex < library.Presets.Count; presetIndex++)
        {
            if (library.Presets[presetIndex] == preset)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Verifies one testing tile has the expected scene, structural role, and exact graph depth.
    /// </summary>
    /// <param name="tile">Authored testing tile to inspect.</param>
    /// <param name="expectedSceneId">Required canonical scene identifier.</param>
    /// <param name="expectedRole">Required graph role.</param>
    /// <param name="expectedDepth">Required exact graph depth.</param>
    /// <param name="expectedMaximumCopies">Required per-run copy capacity.</param>
    private static void ValidateTestingTile(GameProceduralRoomTileDefinition tile,
                                            string expectedSceneId,
                                            GameProceduralRoomRole expectedRole,
                                            int expectedDepth,
                                            int expectedMaximumCopies)
    {
        Require(tile != null, "TestingTrainScenes contains a missing room tile.");
        Require(string.Equals(tile.SceneId, expectedSceneId, StringComparison.Ordinal),
                "TestingTrainScenes tile order does not match the deterministic route.");
        Require(tile.Role == expectedRole,
                "TestingTrainScenes tile " + expectedSceneId + " has an incorrect role.");
        Require(tile.MaximumCopies == expectedMaximumCopies,
                "TestingTrainScenes tile " + expectedSceneId + " has an incorrect copy capacity.");
        Require(tile.UseExactDepthConstraint && tile.ExactDepth == expectedDepth,
                "TestingTrainScenes tile " + expectedSceneId + " has an incorrect exact depth.");
    }

    /// <summary>
    /// Verifies one generated node matches the deterministic testing route.
    /// </summary>
    /// <param name="node">Generated graph node to inspect.</param>
    /// <param name="expectedSceneId">Required canonical scene identifier.</param>
    /// <param name="expectedRole">Required graph role.</param>
    /// <param name="expectedDepth">Required graph depth.</param>
    private static void ValidateGeneratedNode(GameProceduralLevelGraphNode node,
                                              string expectedSceneId,
                                              GameProceduralRoomRole expectedRole,
                                              int expectedDepth)
    {
        Require(string.Equals(node.SceneId, expectedSceneId, StringComparison.Ordinal),
                "TestingTrainScenes generated an unexpected scene at depth " + expectedDepth + ".");
        Require(node.Role == expectedRole && node.Depth == expectedDepth,
                "TestingTrainScenes generated an incorrect role or depth for " + expectedSceneId + ".");
    }
    #endregion

    #region Preset Validation
    /// <summary>
    /// Verifies the default portal pipeline contains exactly one transform arrival for each rail direction.
    /// </summary>
    private static void ValidatePreset()
    {
        GameRoomClearRewardsPreset preset =
            AssetDatabase.LoadAssetAtPath<GameRoomClearRewardsPreset>(defaultPresetPath);
        Require(preset != null, "Default Room Clear Rewards preset is missing.");
        IReadOnlyList<GameRoomPortalActivationAnimationDefinition> animations =
            preset.PortalLogSettings.ActivationAnimations;
        int westCount = 0;
        int eastCount = 0;

        for (int animationIndex = 0; animationIndex < animations.Count; animationIndex++)
        {
            GameRoomPortalActivationAnimationDefinition animation = animations[animationIndex];
            Require(animation != null, "Portal animation " + animationIndex + " is missing.");
            Require(!string.Equals(animation.TargetBindingId, "Train01", StringComparison.Ordinal),
                    "Legacy Train01 clip animation remains in the default preset.");

            if (string.Equals(animation.TargetBindingId,
                              GameRoomTrainArrivalProjectSetupUtility.WestTrainBindingId,
                              StringComparison.Ordinal))
            {
                ValidateArrival(animation,
                                GameRoomTrainArrivalProjectSetupUtility.WestArrivalOffset,
                                "west");
                westCount++;
            }

            if (string.Equals(animation.TargetBindingId,
                              GameRoomTrainArrivalProjectSetupUtility.EastTrainBindingId,
                              StringComparison.Ordinal))
            {
                ValidateArrival(animation,
                                GameRoomTrainArrivalProjectSetupUtility.EastArrivalOffset,
                                "east");
                eastCount++;
            }
        }

        Require(westCount == 1, "Default preset should contain exactly one west train arrival.");
        Require(eastCount == 1, "Default preset should contain exactly one east train arrival.");
    }

    /// <summary>
    /// Verifies one directional train animation uses the safe once-only SmootherStep transform profile.
    /// </summary>
    /// <param name="animation">Arrival definition to inspect.</param>
    /// <param name="expectedOffset">Directional local position offset expected from the authored rail extremes.</param>
    /// <param name="label">Direction label included in failures.</param>
    private static void ValidateArrival(GameRoomPortalActivationAnimationDefinition animation,
                                        Vector3 expectedOffset,
                                        string label)
    {
        Require(animation.Source == GameRoomPortalActivationAnimationSource.Transform,
                "The " + label + " train arrival does not use Transform animation.");
        Require(animation.Mode == GameRoomPortalTransformAnimationMode.Position,
                "The " + label + " train arrival controls channels other than position.");
        Require(animation.Playback == GameRoomPortalTransformAnimationPlayback.Once,
                "The " + label + " train arrival should stop at the platform.");
        Require(animation.Easing == GameRoomPortalTransformAnimationEase.SmootherStep,
                "The " + label + " train arrival does not use gradual acceleration and braking.");
        Require(Mathf.Approximately(animation.Duration,
                                    GameRoomTrainArrivalProjectSetupUtility.ArrivalDurationSeconds),
                "The " + label + " train arrival duration is incorrect.");
        Require(Vector3.Distance(animation.PositionOffset, expectedOffset) <= 0.001f,
                "The " + label + " train arrival offset does not end at its authored platform pose.");
        Require(animation.AnimatorClip == null,
                "The " + label + " train arrival still references a legacy AnimationClip.");
    }
    #endregion

    #region Prefab Validation
    /// <summary>
    /// Verifies every managed train environment stores both trains beyond the playable rails with Animators disabled.
    /// </summary>
    private static void ValidatePrefabs()
    {
        IReadOnlyList<string> prefabPaths =
            GameRoomTrainArrivalProjectSetupUtility.ManagedTrainPrefabPaths;

        for (int prefabIndex = 0; prefabIndex < prefabPaths.Count; prefabIndex++)
        {
            string prefabPath = prefabPaths[prefabIndex];
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                Transform westTrain = GameRoomTrainArrivalProjectSetupUtility.FindDescendant(
                    prefabRoot.transform,
                    "SM_Train");
                Transform eastTrain = GameRoomTrainArrivalProjectSetupUtility.FindDescendant(
                    prefabRoot.transform,
                    "SM_Train (1)");
                RequireTrainRoot(westTrain,
                                 GameRoomTrainArrivalProjectSetupUtility.WestInitialLocalZ,
                                 prefabPath + " west train");
                RequireTrainRoot(eastTrain,
                                 GameRoomTrainArrivalProjectSetupUtility.EastInitialLocalZ,
                                 prefabPath + " east train");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }

    /// <summary>
    /// Verifies one train's off-map start and removal of automatic legacy controller playback.
    /// </summary>
    /// <param name="train">Train root to inspect.</param>
    /// <param name="expectedLocalZ">Expected rail-extreme local coordinate.</param>
    /// <param name="label">Prefab and direction label included in failures.</param>
    private static void RequireTrainRoot(Transform train, float expectedLocalZ, string label)
    {
        Require(train != null, label + " is missing.");
        Require(Mathf.Approximately(train.localPosition.z, expectedLocalZ),
                label + " does not start at the authored rail extreme.");
        Animator animator = train.GetComponent<Animator>();

        if (animator == null)
            return;

        Require(!animator.enabled, label + " legacy Animator remains enabled.");
        Require(animator.runtimeAnimatorController == null,
                label + " still references a legacy Animator Controller.");
        Require(!animator.applyRootMotion, label + " still applies Animator root motion.");
    }
    #endregion

    #region Scene Validation
    /// <summary>
    /// Verifies every dependent managed room links directional portals to the correct inherited train instance.
    /// </summary>
    private static void ValidateScenes()
    {
        IReadOnlyList<string> scenePaths =
            GameRoomTrainArrivalProjectSetupUtility.CollectDependentScenePaths();
        Require(scenePaths.Count > 0, "No managed train scenes were discovered from prefab dependencies.");
        Scene previouslyActiveScene = SceneManager.GetActiveScene();

        for (int sceneIndex = 0; sceneIndex < scenePaths.Count; sceneIndex++)
            ValidateScene(scenePaths[sceneIndex]);

        if (previouslyActiveScene.IsValid() && previouslyActiveScene.isLoaded)
            SceneManager.SetActiveScene(previouslyActiveScene);
    }

    /// <summary>
    /// Validates directional links and arrived-train coverage for every east or west portal in one room.
    /// </summary>
    /// <param name="scenePath">Project-relative managed room scene path.</param>
    private static void ValidateScene(string scenePath)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;

        if (!wasLoaded)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        try
        {
            GameRoomTrainArrivalProjectSetupUtility.FindSceneTrains(scene,
                                                                    out Transform westTrain,
                                                                    out Transform eastTrain);

            if (westTrain == null && eastTrain == null)
                return;

            RequireTrainRoot(westTrain,
                             GameRoomTrainArrivalProjectSetupUtility.WestInitialLocalZ,
                             scenePath + " west train");
            RequireTrainRoot(eastTrain,
                             GameRoomTrainArrivalProjectSetupUtility.EastInitialLocalZ,
                             scenePath + " east train");
            List<GameRoomPortalRewardLogAnchor> anchors = new List<GameRoomPortalRewardLogAnchor>(8);
            CollectSceneAnchors(scene, anchors);
            bool useVerticalFallback =
                GameRoomTrainPortalPlacementUtility.RequiresVerticalFallback(anchors);

            // Validate all managed train-facing portals and their synchronized authoritative SubScene centers.
            for (int anchorIndex = 0; anchorIndex < anchors.Count; anchorIndex++)
            {
                ValidatePortalAnchor(anchors[anchorIndex],
                                     westTrain,
                                     eastTrain,
                                     useVerticalFallback,
                                     scene,
                                     scenePath);
            }
        }
        finally
        {
            if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    /// <summary>
    /// Collects every managed portal anchor across all roots of one loaded room scene.
    /// </summary>
    /// <param name="scene">Loaded scene to inspect.</param>
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
    /// Verifies one directional portal owns the expected train link and remains within the arrived boarding footprint.
    /// </summary>
    /// <param name="anchor">Managed portal presentation anchor to inspect.</param>
    /// <param name="westTrain">West train root inherited by the room.</param>
    /// <param name="eastTrain">East train root inherited by the room.</param>
    /// <param name="useVerticalFallback">True when south and north portals map to west and east trains.</param>
    /// <param name="scene">Loaded managed room scene containing authoritative SubScene references.</param>
    /// <param name="scenePath">Scene path included in failures.</param>
    private static void ValidatePortalAnchor(GameRoomPortalRewardLogAnchor anchor,
                                             Transform westTrain,
                                             Transform eastTrain,
                                             bool useVerticalFallback,
                                             Scene scene,
                                             string scenePath)
    {
        if (!GameRoomTrainPortalPlacementUtility.TryResolveTrainSide(anchor.PortalId,
                                                                     useVerticalFallback,
                                                                     out bool usesWestTrain))
        {
            return;
        }

        Transform expectedTrain = usesWestTrain ? westTrain : eastTrain;
        Require(expectedTrain != null,
                scenePath + " portal " + anchor.PortalId + " cannot resolve its expected train root.");
        string expectedBindingId = usesWestTrain
            ? GameRoomTrainArrivalProjectSetupUtility.WestTrainBindingId
            : GameRoomTrainArrivalProjectSetupUtility.EastTrainBindingId;
        Vector3 arrivalOffset = usesWestTrain
            ? GameRoomTrainArrivalProjectSetupUtility.WestArrivalOffset
            : GameRoomTrainArrivalProjectSetupUtility.EastArrivalOffset;
        IReadOnlyList<GameRoomPortalLinkedObjectBinding> bindings = anchor.EffectView.LinkedObjects;
        int matchingBindings = 0;

        for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
        {
            GameRoomPortalLinkedObjectBinding binding = bindings[bindingIndex];
            Require(binding != null &&
                    !string.IsNullOrWhiteSpace(binding.BindingId) &&
                    binding.TargetObject != null,
                    scenePath + " portal " + anchor.PortalId +
                    " contains an incomplete linked-object binding at index " + bindingIndex + ".");

            Require(!string.Equals(binding.BindingId, "Train01", StringComparison.Ordinal),
                    scenePath + " portal " + anchor.PortalId + " retains a legacy Train01 binding.");

            if (string.Equals(binding.BindingId, expectedBindingId, StringComparison.Ordinal) &&
                binding.TargetObject == expectedTrain.gameObject)
                matchingBindings++;
        }

        Require(matchingBindings == 1,
                scenePath + " portal " + anchor.PortalId +
                " should contain exactly one directional train binding.");

        if (useVerticalFallback)
        {
            Require(GameRoomRewardPortalManagedSceneSetupUtility.TryResolvePortalWorldCenter(
                        scene,
                        anchor.PortalId,
                        out Vector3 authoritativeCenter,
                        out string failure),
                    scenePath + " portal " + anchor.PortalId +
                    " cannot resolve its authoritative center: " + failure);
            Require(Vector3.Distance(authoritativeCenter, anchor.transform.position) <= 0.001f,
                    scenePath + " portal " + anchor.PortalId +
                    " managed and authoritative centers are not synchronized.");
        }

        bool portalInsideTrain = PortalInsideArrivedTrain(anchor.transform.position,
                                                         expectedTrain,
                                                         arrivalOffset,
                                                         out Bounds arrivedBounds);
        Require(portalInsideTrain,
                scenePath + " portal " + anchor.PortalId +
                " at " + anchor.transform.position +
                " does not lie inside the train's arrived boarding footprint around render bounds " +
                arrivedBounds.min + " to " + arrivedBounds.max + ".");
    }

    /// <summary>
    /// Tests a portal point against the combined arrived bounds plus the accessible boarding-side clearance.
    /// </summary>
    /// <param name="portalPosition">Managed anchor position aligned to the authoritative portal center.</param>
    /// <param name="train">Train root whose child renderers define the arrived vehicle footprint.</param>
    /// <param name="localOffset">Transform animation offset applied at normalized progress one.</param>
    /// <param name="arrivedBounds">Combined world-space renderer bounds after the predicted displacement.</param>
    /// <returns>True when the portal lies inside or immediately beside the predicted XZ render footprint.</returns>
    private static bool PortalInsideArrivedTrain(Vector3 portalPosition,
                                                 Transform train,
                                                 Vector3 localOffset,
                                                 out Bounds arrivedBounds)
    {
        Renderer[] renderers = train.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            arrivedBounds = new Bounds();
            return false;
        }

        Vector3 worldOffset = train.parent != null
            ? train.parent.TransformVector(localOffset)
            : localOffset;
        arrivedBounds = renderers[0].bounds;

        for (int rendererIndex = 1; rendererIndex < renderers.Length; rendererIndex++)
            arrivedBounds.Encapsulate(renderers[rendererIndex].bounds);

        arrivedBounds.center += worldOffset;
        const float boardingSideClearance = 2f;
        const float railEndClearance = 0.5f;
        return portalPosition.x >= arrivedBounds.min.x - boardingSideClearance &&
               portalPosition.x <= arrivedBounds.max.x + boardingSideClearance &&
               portalPosition.z >= arrivedBounds.min.z - railEndClearance &&
               portalPosition.z <= arrivedBounds.max.z + railEndClearance;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Throws one actionable deterministic failure when a train arrival invariant is not satisfied.
    /// </summary>
    /// <param name="condition">Invariant required for the smoke test to continue.</param>
    /// <param name="message">Failure text describing the broken authoring state.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("GameRoomTrainArrivalSmokeTest: " + message);
    }
    #endregion

    #endregion
}
#endif
