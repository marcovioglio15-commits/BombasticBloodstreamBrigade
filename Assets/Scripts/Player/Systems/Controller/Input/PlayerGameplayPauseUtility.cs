using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Centralizes the hard-pause checks used by gameplay ECS systems that must freeze their mutable runtime state while UI
/// owns the simulation. Also exposes the dying-aware helpers used by the feedback presentation systems that must keep
/// evolving during the lethal-hit playback window even though Time.timeScale is pinned to zero from the moment defeat
/// is detected (so the camera shake, damage flash, vignette, rumble and death animation can play their final beat while
/// the rest of the gameplay simulation stays frozen).
/// </summary>
internal static class PlayerGameplayPauseUtility
{
    #region Constants
    private const float HardPauseTimeScaleThreshold = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves whether gameplay is currently under a hard pause driven by UI or end-of-run flows.
    /// </summary>
    /// <returns>True when simulation-facing gameplay state must remain frozen for the current frame.</returns>
    public static bool IsHardGameplayPauseActive()
    {
        return IsTimeScaleHardPaused() || GameSceneTransitionRuntimeGuardUtility.ShouldBlockDefaultWorldPlayerGameplay();
    }

    /// <summary>
    /// Resolves whether player position and facing must freeze while a destination is unsafe. Motion resumes for a
    /// ready procedural FadeIn and remains live throughout optional spatial dual-slot traversal.
    /// </summary>
    /// <returns>True for time-scale pauses and destructive scene-replacement phases.</returns>
    public static bool IsPlayerMotionHardPauseActive()
    {
        if (IsTimeScaleHardPaused())
            return true;

        GameSceneTransitionRuntimeGuardUtility.ResolveDefaultWorldPlayerPolicy(out bool _,
                                                                               out bool shouldBlockGameplay,
                                                                               out bool allowsLiveMotion,
                                                                               out bool _,
                                                                               out bool _);
        return shouldBlockGameplay && !allowsLiveMotion;
    }

    /// <summary>
    /// Resolves whether player shooting must freeze while scene management owns an unsafe destination. Combat resumes
    /// for a ready room-traversal FadeIn and stays live throughout spatially aligned dual-slot traversal.
    /// </summary>
    /// <returns>True for time-scale pauses and transition phases that cannot safely simulate player combat.</returns>
    public static bool IsPlayerCombatHardPauseActive()
    {
        if (IsTimeScaleHardPaused())
            return true;

        GameSceneTransitionRuntimeGuardUtility.ResolveDefaultWorldPlayerPolicy(out bool _,
                                                                               out bool shouldBlockGameplay,
                                                                               out bool _,
                                                                               out bool allowsLiveCombat,
                                                                               out bool _);
        return shouldBlockGameplay && !allowsLiveCombat;
    }

    /// <summary>
    /// Resolves whether Unity's scaled time is paused, without treating scene transitions as a pause by itself.
    /// </summary>
    /// <returns>True when Time.timeScale is effectively zero.</returns>
    public static bool IsTimeScaleHardPaused()
    {
        return Time.timeScale <= HardPauseTimeScaleThreshold;
    }

    /// <summary>
    /// Resolves whether a finalized run outcome should freeze presentation systems that would otherwise move during transition fade-out.
    /// </summary>
    /// <param name="runOutcomeQuery">Query selecting local player run outcome state.</param>
    /// <returns>True when at least one player run outcome is finalized.</returns>
    public static bool IsFinalizedRunOutcomeActive(EntityQuery runOutcomeQuery)
    {
        if (runOutcomeQuery.IsEmptyIgnoreFilter)
            return false;

        NativeArray<PlayerRunOutcomeState> runOutcomeStates = runOutcomeQuery.ToComponentDataArray<PlayerRunOutcomeState>(Allocator.Temp);

        try
        {
            for (int index = 0; index < runOutcomeStates.Length; index++)
            {
                if (runOutcomeStates[index].IsFinalized == 0)
                    continue;

                return true;
            }
        }
        finally
        {
            if (runOutcomeStates.IsCreated)
                runOutcomeStates.Dispose();
        }

        return false;
    }

    /// <summary>
    /// Resolves whether the local player run outcome is in its transient dying playback window. Used by the feedback
    /// presentation systems to bypass the standard hard-pause gate so they can keep evolving even though the freeze
    /// system pinned Time.timeScale to zero from the moment defeat was detected.
    /// </summary>
    /// <param name="runOutcomeQuery">Query selecting local player run outcome state.</param>
    /// <returns>True when at least one player run outcome is dying but not yet finalized.</returns>
    public static bool IsDyingRunOutcomeActive(EntityQuery runOutcomeQuery)
    {
        if (runOutcomeQuery.IsEmptyIgnoreFilter)
            return false;

        NativeArray<PlayerRunOutcomeState> runOutcomeStates = runOutcomeQuery.ToComponentDataArray<PlayerRunOutcomeState>(Allocator.Temp);

        try
        {
            for (int index = 0; index < runOutcomeStates.Length; index++)
            {
                PlayerRunOutcomeState runOutcomeState = runOutcomeStates[index];

                if (runOutcomeState.IsDying == 0)
                    continue;

                if (runOutcomeState.IsFinalized != 0)
                    continue;

                return true;
            }
        }
        finally
        {
            if (runOutcomeStates.IsCreated)
                runOutcomeStates.Dispose();
        }

        return false;
    }

    /// <summary>
    /// Resolves the delta time the feedback presentation systems (camera shake, damage flash, vignette, death animation)
    /// should use this frame so they keep evolving during the dying playback window despite the hard pause. Returns the
    /// scaled delta when gameplay time is running normally, the unscaled delta when the run is dying so the feedbacks
    /// can settle even though Time.timeScale was pinned to zero on defeat, and the unscaled delta during scene
    /// transitions so the presentation does not jitter while the simulation settles.
    /// </summary>
    /// <param name="scaledDeltaTime">Current frame scaled delta time from SystemAPI.Time.DeltaTime.</param>
    /// <param name="runOutcomeQuery">Query selecting local player run outcome state.</param>
    /// <param name="isSceneTransitioning">True while the scene manager is loading or fading between scenes.</param>
    /// <returns>Delta time suitable for the feedback presentation systems on the current frame.</returns>
    public static float ResolveFeedbackDeltaTime(float scaledDeltaTime,
                                                  EntityQuery runOutcomeQuery,
                                                  bool isSceneTransitioning)
    {
        if (scaledDeltaTime > 0f)
            return scaledDeltaTime;

        if (IsDyingRunOutcomeActive(runOutcomeQuery))
            return Time.unscaledDeltaTime;

        if (isSceneTransitioning)
            return Time.unscaledDeltaTime;

        return 0f;
    }
    #endregion

    #endregion
}
