/// <summary>
/// Resolves and applies fade presentation policy independently from scene mutation orchestration.
/// </summary>
public static class GameSceneTransitionFadePresentationUtility
{
    #region Methods
    /// <summary>
    /// Selects portal-directed coverage only for physical room traversal and keeps other transitions uniform.
    /// </summary>
    /// <param name="request">Request whose purpose and portal direction define the presentation.</param>
    /// <param name="visualStyle">Gradient or paint visual family selected by the Scene Manager preset.</param>
    /// <param name="fadeMode">Resolved shader fade mode.</param>
    /// <param name="wipeDirection">Resolved directional-gradient orientation.</param>
    public static void Resolve(in GameSceneTransitionRequest request,
                               GameSceneFadeVisualStyle visualStyle,
                               out GameSceneFadeMode fadeMode,
                               out GameSceneFadeWipeDirection wipeDirection)
    {
        bool directional = request.Purpose == GameSceneTransitionPurpose.ProceduralRoomTraversal;

        if (visualStyle == GameSceneFadeVisualStyle.Paint)
        {
            fadeMode = directional
                ? GameSceneFadeMode.DirectionalPaint
                : GameSceneFadeMode.UniformPaint;
            wipeDirection = directional
                ? request.PortalWipeDirection
                : GameSceneFadeWipeDirection.LeftToRight;
            return;
        }

        fadeMode = directional
            ? GameSceneFadeMode.DirectionalGradient
            : GameSceneFadeMode.Uniform;
        wipeDirection = directional
            ? request.PortalWipeDirection
            : GameSceneFadeWipeDirection.LeftToRight;
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
