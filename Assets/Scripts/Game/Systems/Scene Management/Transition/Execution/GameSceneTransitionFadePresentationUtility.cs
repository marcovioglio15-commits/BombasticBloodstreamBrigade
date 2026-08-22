/// <summary>
/// Resolves and applies fade presentation policy independently from scene mutation orchestration.
/// </summary>
internal static class GameSceneTransitionFadePresentationUtility
{
    #region Methods
    /// <summary>
    /// Selects portal-directed darkness only for physical room traversal and keeps other transitions uniform.
    /// </summary>
    /// <param name="request">Request whose purpose and portal direction define the presentation.</param>
    /// <param name="fadeMode">Resolved shader fade mode.</param>
    /// <param name="wipeDirection">Resolved directional-gradient orientation.</param>
    internal static void Resolve(in GameSceneTransitionRequest request,
                                 out GameSceneFadeMode fadeMode,
                                 out GameSceneFadeWipeDirection wipeDirection)
    {
        if (request.Purpose == GameSceneTransitionPurpose.ProceduralRoomTraversal)
        {
            fadeMode = GameSceneFadeMode.DirectionalGradient;
            wipeDirection = request.PortalWipeDirection;
            return;
        }

        fadeMode = GameSceneFadeMode.Uniform;
        wipeDirection = GameSceneFadeWipeDirection.LeftToRight;
    }

    /// <summary>
    /// Applies active request presentation to the frame-local config consumed by every fade phase.
    /// </summary>
    /// <param name="config">Mutable frame-local scene configuration.</param>
    /// <param name="fadeMode">Active shader fade mode.</param>
    /// <param name="wipeDirection">Active directional-gradient orientation.</param>
    internal static void Apply(ref GameSceneManagerConfig config,
                               GameSceneFadeMode fadeMode,
                               GameSceneFadeWipeDirection wipeDirection)
    {
        config.FadeMode = fadeMode;
        config.FadeWipeDirection = wipeDirection;
    }

    /// <summary>
    /// Resolves whether scene mutation must wait for one fully covered Canvas render submission.
    /// </summary>
    /// <param name="fadeState">Current ECS fade state carrying its presentation acknowledgement.</param>
    /// <returns>True while an active authored fade view has not rendered complete coverage.</returns>
    internal static bool IsWaitingForRenderedCoverage(in GameSceneFadePresentationState fadeState)
    {
        return GameSceneFadeCanvasView.HasActiveView && fadeState.OpaquePresented == 0;
    }
    #endregion
}
