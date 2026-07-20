using UnityEngine;

/// <summary>
/// Advances the presentation-only fade phases used by managed scene transitions.
/// </summary>
internal static class GameSceneTransitionFadePhaseUtility
{
    #region Methods

    #region Phase Ticks
    /// <summary>
    /// Advances fade-out timing and keeps the overlay fully opaque once the phase completes.
    /// </summary>
    /// <param name="phaseTimer">Mutable elapsed time for the active phase.</param>
    /// <param name="fadeOutSeconds">Configured fade-out duration in seconds.</param>
    /// <param name="deltaTime">Clamped unscaled frame delta.</param>
    /// <param name="fadeState">Mutable fade presentation state.</param>
    /// <param name="config">Runtime scene manager configuration that provides the fade color.</param>
    /// <returns>True when fade-out has reached full opacity.</returns>
    public static bool TickFadeOut(ref float phaseTimer,
                                   float fadeOutSeconds,
                                   float deltaTime,
                                   ref GameSceneFadePresentationState fadeState,
                                   GameSceneManagerConfig config)
    {
        // Advance the timer and publish interpolated opacity for this presentation frame.
        phaseTimer += deltaTime;
        float alpha = fadeOutSeconds > 0f ? Mathf.Clamp01(phaseTimer / fadeOutSeconds) : 1f;
        GameSceneTransitionExecutionUtility.SetFade(ref fadeState, alpha, true, config);

        // Keep the target hidden at exact full opacity once the fade completes.
        if (phaseTimer < fadeOutSeconds)
            return false;

        GameSceneTransitionExecutionUtility.SetFade(ref fadeState, 1f, true, config);
        return true;
    }

    /// <summary>
    /// Advances the optional fully opaque hold while publishing ready loading progress.
    /// </summary>
    /// <param name="phaseTimer">Mutable elapsed time for the active phase.</param>
    /// <param name="holdSeconds">Configured fully opaque hold duration in seconds.</param>
    /// <param name="deltaTime">Clamped unscaled frame delta.</param>
    /// <param name="fadeState">Mutable fade presentation state.</param>
    /// <param name="loadingProgressState">Mutable loading-progress presentation state.</param>
    /// <param name="config">Runtime scene manager configuration.</param>
    /// <returns>True when the configured hold duration has elapsed.</returns>
    public static bool TickHoldBlack(ref float phaseTimer,
                                     float holdSeconds,
                                     float deltaTime,
                                     ref GameSceneFadePresentationState fadeState,
                                     ref GameSceneLoadingProgressPresentationState loadingProgressState,
                                     GameSceneManagerConfig config)
    {
        // Keep the loaded scene hidden and expose its ready status throughout the hold.
        GameSceneTransitionExecutionUtility.SetFade(ref fadeState, 1f, true, config);
        GameSceneLoadingProgressRuntimeUtility.ApplyReady(ref loadingProgressState, config);
        phaseTimer += deltaTime;
        return phaseTimer >= holdSeconds;
    }

    /// <summary>
    /// Advances fade-in timing and updates the overlay visibility threshold.
    /// </summary>
    /// <param name="phaseTimer">Mutable elapsed time for the active phase.</param>
    /// <param name="fadeInSeconds">Configured fade-in duration in seconds.</param>
    /// <param name="deltaTime">Clamped unscaled frame delta.</param>
    /// <param name="fadeState">Mutable fade presentation state.</param>
    /// <returns>True when fade-in has reached transparent alpha.</returns>
    public static bool TickFadeIn(ref float phaseTimer,
                                  float fadeInSeconds,
                                  float deltaTime,
                                  ref GameSceneFadePresentationState fadeState)
    {
        // Advance the timer and reveal the ready target scene through the shared overlay state.
        phaseTimer += deltaTime;
        float alpha = fadeInSeconds > 0f ? 1f - Mathf.Clamp01(phaseTimer / fadeInSeconds) : 0f;
        fadeState.Alpha = alpha;
        fadeState.Visible = alpha > 0.001f ? (byte)1 : (byte)0;
        return phaseTimer >= fadeInSeconds;
    }
    #endregion

    #endregion
}
