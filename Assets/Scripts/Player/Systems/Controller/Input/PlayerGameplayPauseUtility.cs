using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Centralizes the hard-pause check used by gameplay ECS systems that must freeze their mutable runtime state while UI owns the simulation.
/// /params None.
/// /returns None.
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
    /// /params None.
    /// /returns True when simulation-facing gameplay state must remain frozen for the current frame.
    /// </summary>
    public static bool IsHardGameplayPauseActive()
    {
        return IsTimeScaleHardPaused() || GameSceneTransitionRuntimeGuardUtility.ShouldBlockDefaultWorldGameplay();
    }

    /// <summary>
    /// Resolves whether Unity's scaled time is paused, without treating scene transitions as a pause by itself.
    /// /params None.
    /// /returns True when Time.timeScale is effectively zero.
    /// </summary>
    public static bool IsTimeScaleHardPaused()
    {
        return Time.timeScale <= HardPauseTimeScaleThreshold;
    }

    /// <summary>
    /// Resolves whether a finalized run outcome should freeze presentation systems that would otherwise move during transition fade-out.
    /// /params runOutcomeQuery Query selecting local player run outcome state.
    /// /returns True when at least one player run outcome is finalized.
    /// </summary>
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
    #endregion

    #endregion
}
