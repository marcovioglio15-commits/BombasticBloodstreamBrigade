using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Finalizes scene-transition state consistently across managed execution fields and ECS presentation components.
/// </summary>
internal static class GameSceneTransitionExecutionCompletionUtility
{
    #region Methods

    #region Completion Methods
    /// <summary>
    /// Clears the active operation, restores time scale and commits the idle transition state to ECS when a manager exists.
    /// </summary>
    /// <param name="entityManager">Entity manager used to persist the completed component values.</param>
    /// <param name="managerEntity">Scene manager entity that may receive the completed values.</param>
    /// <param name="activeOperation">Mutable asynchronous scene operation state.</param>
    /// <param name="activePhase">Managed phase mirror reset to Idle.</param>
    /// <param name="activePurpose">Managed transition purpose mirror reset to Standard.</param>
    /// <param name="suppressLoadingProgress">Purpose-specific loading suppression flag reset for the next request.</param>
    /// <param name="targetSceneId">Scene identifier committed as the new active scene.</param>
    /// <param name="transitionState">Mutable authoritative transition state.</param>
    /// <param name="fadeState">Mutable fade presentation state.</param>
    /// <param name="loadingProgressState">Mutable loading-progress presentation state.</param>
    /// <param name="config">Runtime scene manager configuration used to hide loading presentation.</param>
    /// <param name="timeScaleChanged">Mutable flag tracking transition-owned time-scale changes.</param>
    /// <param name="previousTimeScale">Time scale restored when transition execution completes.</param>
    internal static void Complete(EntityManager entityManager,
                                  Entity managerEntity,
                                  ref GameSceneSceneOperationState activeOperation,
                                  ref GameSceneTransitionPhase activePhase,
                                  ref GameSceneTransitionPurpose activePurpose,
                                  ref bool suppressLoadingProgress,
                                  FixedString64Bytes targetSceneId,
                                  ref GameSceneTransitionState transitionState,
                                  ref GameSceneFadePresentationState fadeState,
                                  ref GameSceneLoadingProgressPresentationState loadingProgressState,
                                  GameSceneManagerConfig config,
                                  ref bool timeScaleChanged,
                                  float previousTimeScale)
    {
        // Reset managed and ECS state before exposing the completed values to subsequent systems.
        activeOperation.Clear();
        activePhase = GameSceneTransitionPhase.Idle;
        activePurpose = GameSceneTransitionPurpose.Standard;
        suppressLoadingProgress = false;
        transitionState.ActiveSceneId = targetSceneId;
        transitionState.SourceSceneId = default;
        transitionState.TargetSceneId = default;
        transitionState.Phase = GameSceneTransitionPhase.Idle;
        transitionState.Purpose = GameSceneTransitionPurpose.Standard;
        transitionState.CameraPreparation = GameSceneTransitionCameraPreparation.NotRequired;
        transitionState.IsTransitioning = 0;
        transitionState.Initialized = 1;
        fadeState.Alpha = 0f;
        fadeState.Visible = 0;
        GameSceneLoadingProgressRuntimeUtility.Hide(ref loadingProgressState, config);
        GameSceneTransitionTimeScaleUtility.Restore(ref timeScaleChanged, previousTimeScale);

        // Persist only when completion originated from an update owning a live manager singleton.
        if (managerEntity == Entity.Null || !entityManager.Exists(managerEntity))
            return;

        entityManager.SetComponentData(managerEntity, transitionState);
        entityManager.SetComponentData(managerEntity, fadeState);
        entityManager.SetComponentData(managerEntity, loadingProgressState);
    }
    #endregion

    #endregion
}
