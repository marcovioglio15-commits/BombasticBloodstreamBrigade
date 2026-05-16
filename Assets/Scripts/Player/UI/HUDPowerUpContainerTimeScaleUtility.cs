using UnityEngine;
using Unity.Entities;

/// <summary>
/// Centralizes unscaled Time.timeScale resume state updates used by dropped-container overlays.
/// </summary>
internal static class HUDPowerUpContainerTimeScaleUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Starts the unscaled Time.timeScale resume state for the overlay.
    /// isResuming: Mutable flag tracking whether a resume is currently active.
    /// startTimeScale: Mutable cached start Time.timeScale used for interpolation.
    /// targetTimeScale: Mutable cached target Time.timeScale used for interpolation.
    /// durationSeconds: Mutable total resume duration in seconds.
    /// elapsedSeconds: Mutable elapsed unscaled time since the resume started.
    /// configuredDurationSeconds: Resume duration requested by the runtime interaction config.
    /// returns void.
    /// </summary>
    public static void BeginResume(ref bool isResuming,
                                   ref float startTimeScale,
                                   ref float targetTimeScale,
                                   ref float durationSeconds,
                                   ref float elapsedSeconds,
                                   float configuredDurationSeconds)
    {
        durationSeconds = Mathf.Max(0f, configuredDurationSeconds);

        if (durationSeconds <= 0f)
        {
            Time.timeScale = 1f;
            StopResume(ref isResuming,
                       ref startTimeScale,
                       ref targetTimeScale,
                       ref durationSeconds,
                       ref elapsedSeconds);
            return;
        }

        startTimeScale = Mathf.Clamp01(Time.timeScale);
        targetTimeScale = 1f;
        elapsedSeconds = 0f;
        isResuming = true;
    }

    /// <summary>
    /// Starts the configured unscaled Time.timeScale resume by reading the player container-interaction config.
    /// /params entityManager Entity manager used to read the runtime player config.
    /// /params playerEntity Player entity that owns the dropped-container interaction config.
    /// /params isResuming Mutable flag tracking whether a resume is currently active.
    /// /params startTimeScale Mutable cached start Time.timeScale used for interpolation.
    /// /params targetTimeScale Mutable cached target Time.timeScale used for interpolation.
    /// /params durationSeconds Mutable total resume duration in seconds.
    /// /params elapsedSeconds Mutable elapsed unscaled time since the resume started.
    /// /returns void.
    /// </summary>
    public static void BeginResume(EntityManager entityManager,
                                   Entity playerEntity,
                                   ref bool isResuming,
                                   ref float startTimeScale,
                                   ref float targetTimeScale,
                                   ref float durationSeconds,
                                   ref float elapsedSeconds)
    {
        if (playerEntity == Entity.Null ||
            !entityManager.Exists(playerEntity) ||
            !entityManager.HasComponent<PlayerPowerUpContainerInteractionConfig>(playerEntity))
        {
            Time.timeScale = 1f;
            StopResume(ref isResuming,
                       ref startTimeScale,
                       ref targetTimeScale,
                       ref durationSeconds,
                       ref elapsedSeconds);
            return;
        }

        PlayerPowerUpContainerInteractionConfig interactionConfig = entityManager.GetComponentData<PlayerPowerUpContainerInteractionConfig>(playerEntity);
        BeginResume(ref isResuming,
                    ref startTimeScale,
                    ref targetTimeScale,
                    ref durationSeconds,
                    ref elapsedSeconds,
                    interactionConfig.OverlayPanelTimeScaleResumeDurationSeconds);
    }

    /// <summary>
    /// Advances the active Time.timeScale resume and reports whether the interpolation completed.
    /// isResuming: Mutable flag tracking whether a resume is currently active.
    /// startTimeScale: Mutable cached start Time.timeScale used for interpolation.
    /// targetTimeScale: Mutable cached target Time.timeScale used for interpolation.
    /// durationSeconds: Mutable total resume duration in seconds.
    /// elapsedSeconds: Mutable elapsed unscaled time since the resume started.
    /// milestoneSelectionActive: True when another HUD flow must keep the game paused.
    /// returns True when the resume has fully completed or was already inactive.
    /// </summary>
    public static bool UpdateResume(ref bool isResuming,
                                    ref float startTimeScale,
                                    ref float targetTimeScale,
                                    ref float durationSeconds,
                                    ref float elapsedSeconds,
                                    bool milestoneSelectionActive)
    {
        if (!isResuming || milestoneSelectionActive)
            return !isResuming;

        if (durationSeconds <= 0f)
        {
            Time.timeScale = targetTimeScale;
            StopResume(ref isResuming,
                       ref startTimeScale,
                       ref targetTimeScale,
                       ref durationSeconds,
                       ref elapsedSeconds);
            return true;
        }

        elapsedSeconds += Time.unscaledDeltaTime;
        float normalizedProgress = Mathf.Clamp01(elapsedSeconds / durationSeconds);
        Time.timeScale = Mathf.Lerp(startTimeScale, targetTimeScale, normalizedProgress);

        if (normalizedProgress < 1f)
            return false;

        Time.timeScale = targetTimeScale;
        StopResume(ref isResuming,
                   ref startTimeScale,
                   ref targetTimeScale,
                   ref durationSeconds,
                   ref elapsedSeconds);
        return true;
    }

    /// <summary>
    /// Cancels milestone-driven resume state so another overlay can keep gameplay paused until it closes.
    /// /params entityManager Entity manager used to write the milestone resume component.
    /// /params playerEntity Player entity that may own milestone resume state.
    /// /returns void.
    /// </summary>
    public static void CancelMilestoneResume(EntityManager entityManager, Entity playerEntity)
    {
        if (playerEntity == Entity.Null)
            return;

        if (!entityManager.Exists(playerEntity))
            return;

        if (!entityManager.HasComponent<PlayerMilestoneTimeScaleResumeState>(playerEntity))
            return;

        entityManager.SetComponentData(playerEntity,
                                       PlayerMilestoneSelectionOutcomeUtility.CreateInactiveResumeState());
    }

    /// <summary>
    /// Clears the active Time.timeScale resume state.
    /// isResuming: Mutable flag tracking whether a resume is currently active.
    /// startTimeScale: Mutable cached start Time.timeScale used for interpolation.
    /// targetTimeScale: Mutable cached target Time.timeScale used for interpolation.
    /// durationSeconds: Mutable total resume duration in seconds.
    /// elapsedSeconds: Mutable elapsed unscaled time since the resume started.
    /// returns void.
    /// </summary>
    public static void StopResume(ref bool isResuming,
                                  ref float startTimeScale,
                                  ref float targetTimeScale,
                                  ref float durationSeconds,
                                  ref float elapsedSeconds)
    {
        isResuming = false;
        startTimeScale = 0f;
        targetTimeScale = 1f;
        durationSeconds = 0f;
        elapsedSeconds = 0f;
    }
    #endregion

    #endregion
}
