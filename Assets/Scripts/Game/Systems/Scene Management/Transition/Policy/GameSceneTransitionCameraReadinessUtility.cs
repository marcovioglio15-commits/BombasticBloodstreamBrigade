/// <summary>
/// Coordinates the hidden camera-containment pass that must complete before a loaded gameplay scene is revealed.
/// </summary>
public static class GameSceneTransitionCameraReadinessUtility
{
    #region Methods

    #region State Methods
    /// <summary>
    /// Initializes reveal preparation only for gameplay scenes using containment boundaries.
    /// </summary>
    /// <param name="transitionState">Mutable transition state entering its ready reveal phase.</param>
    /// <param name="config">Runtime Scene Manager configuration selecting camera-boundary behavior.</param>
    /// <param name="targetScene">Loaded target whose kind determines whether a gameplay camera exists.</param>
    public static void InitializeForReveal(ref GameSceneTransitionState transitionState,
                                           GameSceneManagerConfig config,
                                           GameSceneDefinitionElement targetScene)
    {
        transitionState.CameraPreparation = RequiresPreparation(config, targetScene)
            ? GameSceneTransitionCameraPreparation.Pending
            : GameSceneTransitionCameraPreparation.NotRequired;
    }

    /// <summary>
    /// Reports whether camera presentation still owes the transition one hidden containment pass.
    /// </summary>
    /// <param name="transitionState">Current authoritative transition state.</param>
    /// <returns>True while fade-in must remain fully opaque for camera preparation.</returns>
    public static bool IsPreparationPending(in GameSceneTransitionState transitionState)
    {
        return transitionState.IsTransitioning != 0 &&
               transitionState.CameraPreparation == GameSceneTransitionCameraPreparation.Pending;
    }

    /// <summary>
    /// Reports whether procedural traversal framing must yield to the prepared destination framing.
    /// </summary>
    /// <param name="transitionState">Current authoritative transition state.</param>
    /// <returns>True throughout the opaque hold and fade-in after containment preparation was requested.</returns>
    public static bool UsesPreparedFraming(in GameSceneTransitionState transitionState)
    {
        if (transitionState.IsTransitioning == 0 ||
            transitionState.CameraPreparation == GameSceneTransitionCameraPreparation.NotRequired)
            return false;

        switch (transitionState.Phase)
        {
            case GameSceneTransitionPhase.HoldBlack:
            case GameSceneTransitionPhase.FadeIn:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Reports whether presentation may switch from source continuity to the acknowledged destination framing.
    /// </summary>
    /// <param name="transitionState">Current authoritative transition state.</param>
    /// <returns>True after camera preparation completed and while the destination reveal framing remains active.</returns>
    public static bool UsesPreparedRevealFraming(in GameSceneTransitionState transitionState)
    {
        return UsesPreparedFraming(in transitionState) && CanReveal(in transitionState);
    }

    /// <summary>
    /// Resolves whether fade-in may advance without exposing an unresolved destination boundary.
    /// </summary>
    /// <param name="transitionState">Current authoritative transition state.</param>
    /// <returns>True when preparation was unnecessary or has been acknowledged by the active camera writer.</returns>
    public static bool CanReveal(in GameSceneTransitionState transitionState)
    {
        return transitionState.CameraPreparation != GameSceneTransitionCameraPreparation.Pending;
    }

    /// <summary>
    /// Acknowledges that the active camera writer applied its destination pose while the overlay was opaque.
    /// </summary>
    /// <param name="transitionState">Mutable authoritative transition state written by camera presentation.</param>
    public static void MarkPrepared(ref GameSceneTransitionState transitionState)
    {
        if (transitionState.CameraPreparation == GameSceneTransitionCameraPreparation.Pending)
            transitionState.CameraPreparation = GameSceneTransitionCameraPreparation.Prepared;
    }
    #endregion

    #region Policy Methods
    /// <summary>
    /// Resolves whether the target needs an explicit camera pass before reveal.
    /// </summary>
    /// <param name="config">Runtime Scene Manager configuration.</param>
    /// <param name="targetScene">Loaded target scene definition.</param>
    /// <returns>True only for gameplay-like targets using containment boundaries.</returns>
    private static bool RequiresPreparation(GameSceneManagerConfig config,
                                            GameSceneDefinitionElement targetScene)
    {
        return config.EnableCameraBoundaries != 0 &&
               config.CameraBoundaryMode == GameCameraBoundaryMode.ContainmentVolume &&
               GameScenePersistentPlayerSceneUtility.IsGameplayLikeScene(targetScene);
    }
    #endregion

    #endregion
}
