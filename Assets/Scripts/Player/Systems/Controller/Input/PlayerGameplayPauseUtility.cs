using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Centralizes the hard-pause check used by gameplay ECS systems that must freeze their mutable runtime state while UI owns the simulation.
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
        return IsTimeScaleHardPaused() || GameSceneTransitionRuntimeGuardUtility.ShouldBlockDefaultWorldGameplay();
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
    #endregion

    #endregion
}
