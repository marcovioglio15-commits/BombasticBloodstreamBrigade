using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Provides transition loading-progress calculations and status text assembly for the Scene Manager presentation bridge.
/// /params None.
/// /returns None.
/// </summary>
internal static class GameSceneLoadingProgressRuntimeUtility
{
    #region Methods

    #region State
    /// <summary>
    /// Applies hidden loading-progress state while preserving baked visual settings.
    /// /params state Mutable loading-progress presentation state.
    /// /params config Runtime scene manager config.
    /// /returns None.
    /// </summary>
    public static void Hide(ref GameSceneLoadingProgressPresentationState state, GameSceneManagerConfig config)
    {
        ApplySettings(ref state, config);
        state.Visible = 0;
        state.ProgressNormalized = 0f;
        state.StatusText = default;
    }

    /// <summary>
    /// Applies active loading-progress state with an operation-specific status label.
    /// /params state Mutable loading-progress presentation state.
    /// /params config Runtime scene manager config.
    /// /params progressNormalized Aggregate loading progress in the 0..1 range.
    /// /params operationKind Current transition operation kind.
    /// /params sceneDefinition Scene definition currently being processed.
    /// /returns None.
    /// </summary>
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
    /// /params state Mutable loading-progress presentation state.
    /// /params config Runtime scene manager config.
    /// /returns None.
    /// </summary>
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
    /// /params reloadActiveScene True when the source scene is unloaded before loading the replacement.
    /// /params hasSourceScene True when a managed source scene definition is available.
    /// /params sourceScene Source scene definition.
    /// /params hasSourceCompanionScene True when a companion UI scene is attached to the source scene.
    /// /params sourceCompanionScene Source companion UI scene definition.
    /// /params sourceSceneId Runtime source scene ID.
    /// /params targetSceneId Runtime target scene ID.
    /// /params hasTargetCompanionScene True when the target scene has a companion UI scene.
    /// /params targetCompanionScene Target companion UI scene definition.
    /// /params persistentPlayerPreLoadUnloadScenes Persistent player scenes unloaded before target loading.
    /// /params persistentPlayerLoadScenes Persistent player scenes loaded for the target.
    /// /params persistentPlayerPostLoadUnloadScenes Persistent player scenes unloaded after target loading.
    /// /returns Operation count used as the aggregate progress denominator.
    /// </summary>
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
    /// /params completedSteps Number of transition operations that have finished.
    /// /params totalSteps Total operation denominator for the active transition.
    /// /params activeOperation Current managed Unity async operation.
    /// /returns Aggregate progress in the 0..1 range.
    /// </summary>
    public static float ResolveAggregateProgress(int completedSteps, int totalSteps, GameSceneSceneOperationState activeOperation)
    {
        int safeTotalSteps = math.max(1, totalSteps);
        float activeStepProgress = activeOperation.IsRunning ? activeOperation.Progress : 0f;
        return math.saturate((completedSteps + activeStepProgress) / safeTotalSteps);
    }

    /// <summary>
    /// Counts one unload step only when the scene can actually be unloaded by transition policy.
    /// /params hasScene True when the scene definition is valid.
    /// /params sceneDefinition Scene definition being inspected.
    /// /returns One when the unload operation should be counted, otherwise zero.
    /// </summary>
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
    /// /params config Runtime scene manager config.
    /// /params sceneDefinition Scene definition being displayed.
    /// /returns Stable human-readable loading label.
    /// </summary>
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
    /// /params state Mutable loading-progress presentation state.
    /// /params config Runtime scene manager config.
    /// /returns None.
    /// </summary>
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
    /// /params config Runtime scene manager config.
    /// /params operationKind Current transition operation kind.
    /// /params sceneDefinition Scene definition currently being processed.
    /// /returns Fixed-string status text for UI presentation.
    /// </summary>
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
    /// /params prefix Authored operation prefix.
    /// /params sceneLabel Scene or Addressables label.
    /// /returns Combined status text.
    /// </summary>
    private static FixedString128Bytes BuildPrefixStatus(FixedString64Bytes prefix, string sceneLabel)
    {
        string prefixText = prefix.ToString();

        if (string.IsNullOrWhiteSpace(prefixText))
            return new FixedString128Bytes(sceneLabel);

        return new FixedString128Bytes(prefixText + " " + sceneLabel);
    }

    /// <summary>
    /// Counts a nullable operation list without requiring callers to allocate defensive empty lists.
    /// /params scenes Scene operation list.
    /// /returns Scene count or zero when the list is missing.
    /// </summary>
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
/// /params None.
/// /returns None.
/// </summary>
internal enum GameSceneLoadingProgressOperationKind : byte
{
    Loading = 0,
    Unloading = 1,
    Readiness = 2,
    Ready = 3
}
