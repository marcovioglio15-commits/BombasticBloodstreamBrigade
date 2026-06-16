using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Provides transition loading-progress calculations and status text assembly for the Scene Manager presentation bridge.
/// </summary>
internal static class GameSceneLoadingProgressRuntimeUtility
{
    #region Methods

    #region State
    /// <summary>
    /// Applies hidden loading-progress state while preserving baked visual settings.
    /// </summary>
    /// <param name="state">Mutable loading-progress presentation state.</param>
    /// <param name="config">Runtime scene manager config.</param>
    public static void Hide(ref GameSceneLoadingProgressPresentationState state, GameSceneManagerConfig config)
    {
        ApplySettings(ref state, config);
        state.Visible = 0;
        state.ProgressNormalized = 0f;
        state.StatusText = default;
    }

    /// <summary>
    /// Applies active loading-progress state with an operation-specific status label.
    /// </summary>
    /// <param name="state">Mutable loading-progress presentation state.</param>
    /// <param name="config">Runtime scene manager config.</param>
    /// <param name="progressNormalized">Aggregate loading progress in the 0..1 range.</param>
    /// <param name="operationKind">Current transition operation kind.</param>
    /// <param name="sceneDefinition">Scene definition currently being processed.</param>
    public static void ApplyProgress(ref GameSceneLoadingProgressPresentationState state,
                                     GameSceneManagerConfig config,
                                     float progressNormalized,
                                     GameSceneLoadingProgressOperationKind operationKind,
                                     GameSceneDefinitionElement sceneDefinition)
    {
        ApplySettings(ref state, config);

        if (config.ShowLoadingProgress == 0)
        {
            state.Visible = 0;
            return;
        }

        state.Visible = 1;
        state.ProgressNormalized = math.saturate(progressNormalized);
        state.StatusText = BuildStatusText(config, operationKind, sceneDefinition);
    }

    /// <summary>
    /// Applies the final ready state used while optional black-screen hold time elapses.
    /// </summary>
    /// <param name="state">Mutable loading-progress presentation state.</param>
    /// <param name="config">Runtime scene manager config.</param>
    public static void ApplyReady(ref GameSceneLoadingProgressPresentationState state, GameSceneManagerConfig config)
    {
        ApplySettings(ref state, config);

        if (config.ShowLoadingProgress == 0)
        {
            state.Visible = 0;
            return;
        }

        state.Visible = 1;
        state.ProgressNormalized = 1f;
        state.StatusText = config.LoadingProgressReadyStatusText;
    }
    #endregion

    #region Progress
    /// <summary>
    /// Counts authored transition operations that contribute to the aggregate loading progress.
    /// </summary>
    /// <param name="reloadActiveScene">True when the source scene is unloaded before loading the replacement.</param>
    /// <param name="hasSourceScene">True when a managed source scene definition is available.</param>
    /// <param name="sourceScene">Source scene definition.</param>
    /// <param name="hasSourceCompanionScene">True when a companion UI scene is attached to the source scene.</param>
    /// <param name="sourceCompanionScene">Source companion UI scene definition.</param>
    /// <param name="sourceSceneId">Runtime source scene ID.</param>
    /// <param name="targetSceneId">Runtime target scene ID.</param>
    /// <param name="hasTargetCompanionScene">True when the target scene has a companion UI scene.</param>
    /// <param name="targetCompanionScene">Target companion UI scene definition.</param>
    /// <param name="persistentPlayerPreLoadUnloadScenes">Persistent player scenes unloaded before target loading.</param>
    /// <param name="persistentPlayerLoadScenes">Persistent player scenes loaded for the target.</param>
    /// <param name="persistentPlayerPostLoadUnloadScenes">Persistent player scenes unloaded after target loading.</param>
    /// <returns>Operation count used as the aggregate progress denominator.</returns>
    public static int CountTransitionSteps(bool reloadActiveScene,
                                           bool hasSourceScene,
                                           GameSceneDefinitionElement sourceScene,
                                           bool hasSourceCompanionScene,
                                           GameSceneDefinitionElement sourceCompanionScene,
                                           FixedString64Bytes sourceSceneId,
                                           FixedString64Bytes targetSceneId,
                                           bool hasTargetCompanionScene,
                                           GameSceneDefinitionElement targetCompanionScene,
                                           List<GameSceneDefinitionElement> persistentPlayerPreLoadUnloadScenes,
                                           List<GameSceneDefinitionElement> persistentPlayerLoadScenes,
                                           List<GameSceneDefinitionElement> persistentPlayerPostLoadUnloadScenes)
    {
        int stepCount = 1;

        if (reloadActiveScene)
        {
            stepCount += CountUnloadStep(hasSourceScene, sourceScene);
            stepCount += CountUnloadStep(hasSourceCompanionScene, sourceCompanionScene);
        }

        stepCount += CountList(persistentPlayerPreLoadUnloadScenes);
        stepCount += CountList(persistentPlayerLoadScenes);

        if (hasTargetCompanionScene)
            stepCount += 1;

        if (GameSceneTransitionUnloadPolicyUtility.ShouldUnloadSourceAfterLoad(hasSourceScene,
                                                                               reloadActiveScene,
                                                                               sourceSceneId,
                                                                               targetSceneId,
                                                                               sourceScene))
        {
            stepCount += 1;
        }

        if (GameSceneTransitionUnloadPolicyUtility.ShouldUnloadSourceCompanionAfterLoad(hasSourceCompanionScene,
                                                                                       reloadActiveScene,
                                                                                       hasTargetCompanionScene,
                                                                                       sourceCompanionScene,
                                                                                       targetCompanionScene))
        {
            stepCount += 1;
        }

        stepCount += CountList(persistentPlayerPostLoadUnloadScenes);
        return math.max(1, stepCount);
    }

    /// <summary>
    /// Converts completed operation count plus the active Unity async operation into aggregate progress.
    /// </summary>
    /// <param name="completedSteps">Number of transition operations that have finished.</param>
    /// <param name="totalSteps">Total operation denominator for the active transition.</param>
    /// <param name="activeOperation">Current managed Unity async operation.</param>
    /// <returns>Aggregate progress in the 0..1 range.</returns>
    public static float ResolveAggregateProgress(int completedSteps, int totalSteps, GameSceneSceneOperationState activeOperation)
    {
        int safeTotalSteps = math.max(1, totalSteps);
        float activeStepProgress = activeOperation.IsRunning ? activeOperation.Progress : 0f;
        return math.saturate((completedSteps + activeStepProgress) / safeTotalSteps);
    }

    /// <summary>
    /// Counts one unload step only when the scene can actually be unloaded by transition policy.
    /// </summary>
    /// <param name="hasScene">True when the scene definition is valid.</param>
    /// <param name="sceneDefinition">Scene definition being inspected.</param>
    /// <returns>One when the unload operation should be counted, otherwise zero.</returns>
    public static int CountUnloadStep(bool hasScene, GameSceneDefinitionElement sceneDefinition)
    {
        if (!hasScene)
            return 0;

        return sceneDefinition.UnloadPolicy == GameSceneUnloadPolicy.UnloadOnTransition ? 1 : 0;
    }
    #endregion

    #region Labels
    /// <summary>
    /// Resolves the best label for the current operation from Addressables key, scene name or scene ID.
    /// </summary>
    /// <param name="config">Runtime scene manager config.</param>
    /// <param name="sceneDefinition">Scene definition being displayed.</param>
    /// <returns>Stable  loading label.</returns>
    public static string ResolveSceneLabel(GameSceneManagerConfig config, GameSceneDefinitionElement sceneDefinition)
    {
        if (config.LoadBackend == GameSceneLoadBackend.Addressables && sceneDefinition.AddressableKey.Length > 0)
            return sceneDefinition.AddressableKey.ToString();

        if (sceneDefinition.SceneName.Length > 0)
            return sceneDefinition.SceneName.ToString();

        if (sceneDefinition.SceneId.Length > 0)
            return sceneDefinition.SceneId.ToString();

        return "Scene";
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Copies baked visual settings into the mutable presentation state.
    /// </summary>
    /// <param name="state">Mutable loading-progress presentation state.</param>
    /// <param name="config">Runtime scene manager config.</param>
    private static void ApplySettings(ref GameSceneLoadingProgressPresentationState state, GameSceneManagerConfig config)
    {
        state.SpinnerRotationDegreesPerSecond = config.LoadingProgressSpinnerRotationDegreesPerSecond;
        state.RingColor = config.LoadingProgressRingColor;
        state.TrackColor = config.LoadingProgressTrackColor;
        state.TextColor = config.LoadingProgressTextColor;
        state.RingSegmentCount = config.LoadingProgressRingSegmentCount;
        state.RingSegmentGapDegrees = config.LoadingProgressRingSegmentGapDegrees;
        state.RingThickness = config.LoadingProgressRingThickness;
        state.ShowPercentage = config.ShowLoadingProgressPercentage;
        state.ShowStatusText = config.ShowLoadingProgressStatusText;
    }

    /// <summary>
    /// Builds the current operation status text from the configured prefix and scene label.
    /// </summary>
    /// <param name="config">Runtime scene manager config.</param>
    /// <param name="operationKind">Current transition operation kind.</param>
    /// <param name="sceneDefinition">Scene definition currently being processed.</param>
    /// <returns>Fixed-string status text for UI presentation.</returns>
    private static FixedString128Bytes BuildStatusText(GameSceneManagerConfig config,
                                                       GameSceneLoadingProgressOperationKind operationKind,
                                                       GameSceneDefinitionElement sceneDefinition)
    {
        switch (operationKind)
        {
            case GameSceneLoadingProgressOperationKind.Unloading:
                return BuildPrefixStatus(config.LoadingProgressUnloadingStatusPrefix, ResolveSceneLabel(config, sceneDefinition));
            case GameSceneLoadingProgressOperationKind.Readiness:
                return config.LoadingProgressReadinessStatusText;
            case GameSceneLoadingProgressOperationKind.Ready:
                return config.LoadingProgressReadyStatusText;
            default:
                return BuildPrefixStatus(config.LoadingProgressLoadingStatusPrefix, ResolveSceneLabel(config, sceneDefinition));
        }
    }

    /// <summary>
    /// Combines one status prefix and one scene label without assuming a specific language order beyond prefix-before-label.
    /// </summary>
    /// <param name="prefix">Authored operation prefix.</param>
    /// <param name="sceneLabel">Scene or Addressables label.</param>
    /// <returns>Combined status text.</returns>
    private static FixedString128Bytes BuildPrefixStatus(FixedString64Bytes prefix, string sceneLabel)
    {
        string prefixText = prefix.ToString();

        if (string.IsNullOrWhiteSpace(prefixText))
            return new FixedString128Bytes(sceneLabel);

        return new FixedString128Bytes(prefixText + " " + sceneLabel);
    }

    /// <summary>
    /// Counts a nullable operation list without requiring callers to allocate defensive empty lists.
    /// </summary>
    /// <param name="scenes">Scene operation list.</param>
    /// <returns>Scene count or zero when the list is missing.</returns>
    private static int CountList(List<GameSceneDefinitionElement> scenes)
    {
        if (scenes == null)
            return 0;

        return scenes.Count;
    }
    #endregion

    #endregion
}

/// <summary>
/// Identifies the status text mode used by loading-progress presentation.
/// </summary>
internal enum GameSceneLoadingProgressOperationKind : byte
{
    Loading = 0,
    Unloading = 1,
    Readiness = 2,
    Ready = 3
}
