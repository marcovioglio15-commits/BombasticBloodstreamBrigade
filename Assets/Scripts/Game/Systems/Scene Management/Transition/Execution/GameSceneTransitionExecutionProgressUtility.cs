using System.Collections.Generic;
using Unity.Mathematics;

/// <summary>
/// Applies aggregate loading-progress presentation for the managed scene transition executor.
/// </summary>
internal static class GameSceneTransitionExecutionProgressUtility
{
    #region Methods

    #region Presentation
    /// <summary>
    /// Applies the appropriate loading-progress visibility and status when a transition phase starts.
    /// </summary>
    /// <param name="phase">Transition phase being entered.</param>
    /// <param name="loadingProgressState">Mutable loading-progress presentation component.</param>
    /// <param name="config">Scene manager runtime config.</param>
    /// <param name="suppressLoadingProgress">True when this transition exposes only the fade overlay.</param>
    /// <param name="snapshot">Immutable snapshot of the executor progress counters.</param>
    public static void ApplyForPhase(GameSceneTransitionPhase phase,
                                     ref GameSceneLoadingProgressPresentationState loadingProgressState,
                                     GameSceneManagerConfig config,
                                     bool suppressLoadingProgress,
                                     GameSceneTransitionProgressSnapshot snapshot)
    {
        if (suppressLoadingProgress)
        {
            GameSceneLoadingProgressRuntimeUtility.Hide(ref loadingProgressState, config);
            return;
        }

        switch (phase)
        {
            case GameSceneTransitionPhase.PreUnload:
                ApplyCurrent(ref loadingProgressState,
                             config,
                             GameSceneLoadingProgressOperationKind.Unloading,
                             snapshot.SourceScene,
                             false,
                             snapshot);
                break;
            case GameSceneTransitionPhase.Loading:
                ApplyCurrent(ref loadingProgressState,
                             config,
                             GameSceneLoadingProgressOperationKind.Loading,
                             snapshot.TargetScene,
                             false,
                             snapshot);
                break;
            case GameSceneTransitionPhase.PostUnload:
                ApplyCurrent(ref loadingProgressState,
                             config,
                             GameSceneLoadingProgressOperationKind.Unloading,
                             snapshot.SourceScene,
                             false,
                             snapshot);
                break;
            case GameSceneTransitionPhase.HoldBlack:
                GameSceneLoadingProgressRuntimeUtility.ApplyReady(ref loadingProgressState, config);
                break;
            default:
                GameSceneLoadingProgressRuntimeUtility.Hide(ref loadingProgressState, config);
                break;
        }
    }

    /// <summary>
    /// Applies aggregate loading progress for the current operation step.
    /// </summary>
    /// <param name="loadingProgressState">Mutable loading-progress presentation component.</param>
    /// <param name="config">Scene manager runtime config.</param>
    /// <param name="operationKind">Current operation kind used for status text.</param>
    /// <param name="sceneDefinition">Scene definition currently being processed.</param>
    /// <param name="suppressLoadingProgress">True when this transition exposes only the fade overlay.</param>
    /// <param name="snapshot">Immutable snapshot of the executor progress counters.</param>
    public static void ApplyCurrent(ref GameSceneLoadingProgressPresentationState loadingProgressState,
                                    GameSceneManagerConfig config,
                                    GameSceneLoadingProgressOperationKind operationKind,
                                    GameSceneDefinitionElement sceneDefinition,
                                    bool suppressLoadingProgress,
                                    GameSceneTransitionProgressSnapshot snapshot)
    {
        if (suppressLoadingProgress)
        {
            GameSceneLoadingProgressRuntimeUtility.Hide(ref loadingProgressState, config);
            return;
        }

        int completedSteps = ResolveCompletedSteps(snapshot);
        float progress = GameSceneLoadingProgressRuntimeUtility.ResolveAggregateProgress(completedSteps,
                                                                                        snapshot.LoadingProgressTotalSteps,
                                                                                        snapshot.ActiveOperation);
        GameSceneLoadingProgressRuntimeUtility.ApplyProgress(ref loadingProgressState, config, progress, operationKind, sceneDefinition);
    }
    #endregion

    #region Counting
    /// <summary>
    /// Resolves how many transition load/unload steps have completed for aggregate progress.
    /// </summary>
    /// <param name="snapshot">Immutable snapshot of the executor progress counters.</param>
    /// <returns>Completed loading-progress operation count.</returns>
    public static int ResolveCompletedSteps(GameSceneTransitionProgressSnapshot snapshot)
    {
        int completedSteps = 0;

        if (snapshot.ReloadActiveScene)
        {
            if (snapshot.SourceSceneUnloadComplete)
                completedSteps += GameSceneLoadingProgressRuntimeUtility.CountUnloadStep(snapshot.HasSourceScene, snapshot.SourceScene);

            if (snapshot.SourceCompanionSceneUnloadComplete)
                completedSteps += GameSceneLoadingProgressRuntimeUtility.CountUnloadStep(snapshot.HasSourceCompanionScene, snapshot.SourceCompanionScene);
        }

        completedSteps += math.clamp(snapshot.PersistentPlayerPreLoadUnloadIndex, 0, snapshot.PersistentPlayerPreLoadUnloadScenes.Count);
        completedSteps += math.clamp(snapshot.PersistentPlayerLoadIndex, 0, snapshot.PersistentPlayerLoadScenes.Count);

        if (snapshot.TargetSceneLoaded)
            completedSteps++;

        if (snapshot.HasTargetCompanionScene && snapshot.TargetCompanionSceneLoaded)
            completedSteps++;

        if (!snapshot.ReloadActiveScene)
        {
            if (snapshot.SourceSceneUnloadComplete)
                completedSteps += GameSceneLoadingProgressRuntimeUtility.CountUnloadStep(snapshot.HasSourceScene, snapshot.SourceScene);

            if (snapshot.SourceCompanionSceneUnloadComplete)
                completedSteps += GameSceneLoadingProgressRuntimeUtility.CountUnloadStep(snapshot.HasSourceCompanionScene, snapshot.SourceCompanionScene);
        }

        completedSteps += math.clamp(snapshot.PersistentPlayerPostLoadUnloadIndex, 0, snapshot.PersistentPlayerPostLoadUnloadScenes.Count);
        return math.min(completedSteps, snapshot.LoadingProgressTotalSteps);
    }

    /// <summary>
    /// Resolves the scene currently represented by a list index, falling back when the index has already completed.
    /// </summary>
    /// <param name="scenes">Operation scene list.</param>
    /// <param name="index">Current operation index.</param>
    /// <param name="fallback">Fallback scene definition.</param>
    /// <returns>Current scene definition or fallback.</returns>
    public static GameSceneDefinitionElement ResolveCurrentListScene(List<GameSceneDefinitionElement> scenes,
                                                                     int index,
                                                                     GameSceneDefinitionElement fallback)
    {
        if (scenes == null)
            return fallback;

        if (index < 0 || index >= scenes.Count)
            return fallback;

        return scenes[index];
    }
    #endregion

    #endregion
}

/// <summary>
/// Captures transition operation counters needed to calculate aggregate loading progress without mutating executor state.
/// </summary>
internal readonly struct GameSceneTransitionProgressSnapshot
{
    #region Fields
    public readonly bool ReloadActiveScene;
    public readonly bool HasSourceScene;
    public readonly GameSceneDefinitionElement SourceScene;
    public readonly bool HasSourceCompanionScene;
    public readonly GameSceneDefinitionElement SourceCompanionScene;
    public readonly GameSceneDefinitionElement TargetScene;
    public readonly bool HasTargetCompanionScene;
    public readonly bool TargetSceneLoaded;
    public readonly bool TargetCompanionSceneLoaded;
    public readonly bool SourceSceneUnloadComplete;
    public readonly bool SourceCompanionSceneUnloadComplete;
    public readonly List<GameSceneDefinitionElement> PersistentPlayerPreLoadUnloadScenes;
    public readonly List<GameSceneDefinitionElement> PersistentPlayerLoadScenes;
    public readonly List<GameSceneDefinitionElement> PersistentPlayerPostLoadUnloadScenes;
    public readonly int PersistentPlayerPreLoadUnloadIndex;
    public readonly int PersistentPlayerLoadIndex;
    public readonly int PersistentPlayerPostLoadUnloadIndex;
    public readonly int LoadingProgressTotalSteps;
    public readonly GameSceneSceneOperationState ActiveOperation;
    #endregion

    #region Methods

    #region Constructor
    /// <summary>
    /// Creates an immutable progress snapshot from the executor's current managed fields.
    /// </summary>
    /// <param name="reloadActiveScene">True when the source scene is unloaded before loading the replacement.</param>
    /// <param name="hasSourceScene">True when a source scene definition is available.</param>
    /// <param name="sourceScene">Source scene definition.</param>
    /// <param name="hasSourceCompanionScene">True when a source companion scene definition is available.</param>
    /// <param name="sourceCompanionScene">Source companion scene definition.</param>
    /// <param name="targetScene">Main transition target scene.</param>
    /// <param name="hasTargetCompanionScene">True when a target companion scene definition is available.</param>
    /// <param name="targetSceneLoaded">True when the target scene load step has completed.</param>
    /// <param name="targetCompanionSceneLoaded">True when the companion scene load step has completed.</param>
    /// <param name="sourceSceneUnloadComplete">True when the source scene unload step has completed.</param>
    /// <param name="sourceCompanionSceneUnloadComplete">True when the source companion scene unload step has completed.</param>
    /// <param name="persistentPlayerPreLoadUnloadScenes">Persistent player scenes unloaded before target loading.</param>
    /// <param name="persistentPlayerLoadScenes">Persistent player scenes loaded for the target.</param>
    /// <param name="persistentPlayerPostLoadUnloadScenes">Persistent player scenes unloaded after target loading.</param>
    /// <param name="persistentPlayerPreLoadUnloadIndex">Current pre-load unload operation index.</param>
    /// <param name="persistentPlayerLoadIndex">Current persistent player load operation index.</param>
    /// <param name="persistentPlayerPostLoadUnloadIndex">Current post-load unload operation index.</param>
    /// <param name="loadingProgressTotalSteps">Aggregate progress denominator for this transition.</param>
    /// <param name="activeOperation">Current managed Unity async operation.</param>
    public GameSceneTransitionProgressSnapshot(bool reloadActiveScene,
                                               bool hasSourceScene,
                                               GameSceneDefinitionElement sourceScene,
                                               bool hasSourceCompanionScene,
                                               GameSceneDefinitionElement sourceCompanionScene,
                                               GameSceneDefinitionElement targetScene,
                                               bool hasTargetCompanionScene,
                                               bool targetSceneLoaded,
                                               bool targetCompanionSceneLoaded,
                                               bool sourceSceneUnloadComplete,
                                               bool sourceCompanionSceneUnloadComplete,
                                               List<GameSceneDefinitionElement> persistentPlayerPreLoadUnloadScenes,
                                               List<GameSceneDefinitionElement> persistentPlayerLoadScenes,
                                               List<GameSceneDefinitionElement> persistentPlayerPostLoadUnloadScenes,
                                               int persistentPlayerPreLoadUnloadIndex,
                                               int persistentPlayerLoadIndex,
                                               int persistentPlayerPostLoadUnloadIndex,
                                               int loadingProgressTotalSteps,
                                               GameSceneSceneOperationState activeOperation)
    {
        ReloadActiveScene = reloadActiveScene;
        HasSourceScene = hasSourceScene;
        SourceScene = sourceScene;
        HasSourceCompanionScene = hasSourceCompanionScene;
        SourceCompanionScene = sourceCompanionScene;
        TargetScene = targetScene;
        HasTargetCompanionScene = hasTargetCompanionScene;
        TargetSceneLoaded = targetSceneLoaded;
        TargetCompanionSceneLoaded = targetCompanionSceneLoaded;
        SourceSceneUnloadComplete = sourceSceneUnloadComplete;
        SourceCompanionSceneUnloadComplete = sourceCompanionSceneUnloadComplete;
        PersistentPlayerPreLoadUnloadScenes = persistentPlayerPreLoadUnloadScenes;
        PersistentPlayerLoadScenes = persistentPlayerLoadScenes;
        PersistentPlayerPostLoadUnloadScenes = persistentPlayerPostLoadUnloadScenes;
        PersistentPlayerPreLoadUnloadIndex = persistentPlayerPreLoadUnloadIndex;
        PersistentPlayerLoadIndex = persistentPlayerLoadIndex;
        PersistentPlayerPostLoadUnloadIndex = persistentPlayerPostLoadUnloadIndex;
        LoadingProgressTotalSteps = loadingProgressTotalSteps;
        ActiveOperation = activeOperation;
    }
    #endregion

    #endregion
}
