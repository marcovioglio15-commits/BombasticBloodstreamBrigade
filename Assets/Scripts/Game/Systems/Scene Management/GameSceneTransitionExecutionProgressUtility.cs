using System.Collections.Generic;
using Unity.Mathematics;

/// <summary>
/// Applies aggregate loading-progress presentation for the managed scene transition executor.
/// /params None.
/// /returns None.
/// </summary>
internal static class GameSceneTransitionExecutionProgressUtility
{
    #region Methods

    #region Presentation
    /// <summary>
    /// Applies the appropriate loading-progress visibility and status when a transition phase starts.
    /// /params phase Transition phase being entered.
    /// /params loadingProgressState Mutable loading-progress presentation component.
    /// /params config Scene manager runtime config.
    /// /params snapshot Immutable snapshot of the executor progress counters.
    /// /returns None.
    /// </summary>
    public static void ApplyForPhase(GameSceneTransitionPhase phase,
                                     ref GameSceneLoadingProgressPresentationState loadingProgressState,
                                     GameSceneManagerConfig config,
                                     GameSceneTransitionProgressSnapshot snapshot)
    {
        switch (phase)
        {
            case GameSceneTransitionPhase.PreUnload:
                ApplyCurrent(ref loadingProgressState, config, GameSceneLoadingProgressOperationKind.Unloading, snapshot.SourceScene, snapshot);
                break;
            case GameSceneTransitionPhase.Loading:
                ApplyCurrent(ref loadingProgressState, config, GameSceneLoadingProgressOperationKind.Loading, snapshot.TargetScene, snapshot);
                break;
            case GameSceneTransitionPhase.PostUnload:
                ApplyCurrent(ref loadingProgressState, config, GameSceneLoadingProgressOperationKind.Unloading, snapshot.SourceScene, snapshot);
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
    /// /params loadingProgressState Mutable loading-progress presentation component.
    /// /params config Scene manager runtime config.
    /// /params operationKind Current operation kind used for status text.
    /// /params sceneDefinition Scene definition currently being processed.
    /// /params snapshot Immutable snapshot of the executor progress counters.
    /// /returns None.
    /// </summary>
    public static void ApplyCurrent(ref GameSceneLoadingProgressPresentationState loadingProgressState,
                                    GameSceneManagerConfig config,
                                    GameSceneLoadingProgressOperationKind operationKind,
                                    GameSceneDefinitionElement sceneDefinition,
                                    GameSceneTransitionProgressSnapshot snapshot)
    {
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
    /// /params snapshot Immutable snapshot of the executor progress counters.
    /// /returns Completed loading-progress operation count.
    /// </summary>
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
    /// /params scenes Operation scene list.
    /// /params index Current operation index.
    /// /params fallback Fallback scene definition.
    /// /returns Current scene definition or fallback.
    /// </summary>
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
/// /params None.
/// /returns None.
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
    /// /params reloadActiveScene True when the source scene is unloaded before loading the replacement.
    /// /params hasSourceScene True when a source scene definition is available.
    /// /params sourceScene Source scene definition.
    /// /params hasSourceCompanionScene True when a source companion scene definition is available.
    /// /params sourceCompanionScene Source companion scene definition.
    /// /params targetScene Main transition target scene.
    /// /params hasTargetCompanionScene True when a target companion scene definition is available.
    /// /params targetSceneLoaded True when the target scene load step has completed.
    /// /params targetCompanionSceneLoaded True when the companion scene load step has completed.
    /// /params sourceSceneUnloadComplete True when the source scene unload step has completed.
    /// /params sourceCompanionSceneUnloadComplete True when the source companion scene unload step has completed.
    /// /params persistentPlayerPreLoadUnloadScenes Persistent player scenes unloaded before target loading.
    /// /params persistentPlayerLoadScenes Persistent player scenes loaded for the target.
    /// /params persistentPlayerPostLoadUnloadScenes Persistent player scenes unloaded after target loading.
    /// /params persistentPlayerPreLoadUnloadIndex Current pre-load unload operation index.
    /// /params persistentPlayerLoadIndex Current persistent player load operation index.
    /// /params persistentPlayerPostLoadUnloadIndex Current post-load unload operation index.
    /// /params loadingProgressTotalSteps Aggregate progress denominator for this transition.
    /// /params activeOperation Current managed Unity async operation.
    /// /returns None.
    /// </summary>
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
