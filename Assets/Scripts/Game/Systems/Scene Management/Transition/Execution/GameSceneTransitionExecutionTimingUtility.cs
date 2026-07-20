using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Provides timing and readiness helpers for the managed scene transition executor.
/// </summary>
internal static class GameSceneTransitionExecutionTimingUtility
{
    #region Constants
    private const int MinimumReadyWarmupFrames = 3;
    private const float MinimumReadyWarmupSeconds = 0.05f;
    private const float MaximumFadeStepSeconds = 1f / 30f;
    #endregion

    #region Methods

    #region Fade Timing
    /// <summary>
    /// Resolves active transition fade timings from transition override or preset defaults.
    /// </summary>
    /// <param name="config">Scene manager runtime config.</param>
    /// <param name="transition">Transition override data.</param>
    /// <returns>Fade timings used by the current transition.</returns>
    public static GameSceneTransitionFadeTimings ResolveFadeTimings(GameSceneManagerConfig config, GameSceneTransitionElement transition)
    {
        if (transition.OverrideFadeSettings != 0)
        {
            return new GameSceneTransitionFadeTimings(Mathf.Max(0f, transition.FadeOutSeconds),
                                                     Mathf.Max(0f, transition.PostLoadReadyExtraSeconds),
                                                     Mathf.Max(0f, transition.FadeInSeconds));
        }

        return new GameSceneTransitionFadeTimings(Mathf.Max(0f, config.FadeOutSeconds),
                                                 Mathf.Max(0f, config.PostLoadReadyExtraSeconds),
                                                 Mathf.Max(0f, config.FadeInSeconds));
    }

    /// <summary>
    /// Caps visual transition steps so a loading hitch cannot consume an entire fade-in in one frame.
    /// </summary>
    /// <param name="unscaledDeltaTime">Raw Unity unscaled frame delta.</param>
    /// <returns>Clamped presentation delta for fade phases.</returns>
    public static float ResolveFadeStepDeltaTime(float unscaledDeltaTime)
    {
        return Mathf.Min(Mathf.Max(0f, unscaledDeltaTime), MaximumFadeStepSeconds);
    }
    #endregion

    #region Readiness
    /// <summary>
    /// Waits until loaded scenes, gameplay runtime and a short hidden warm-up have completed before fade-in.
    /// </summary>
    /// <param name="entityManager">EntityManager used to flush LocalToWorld before readiness checks.</param>
    /// <param name="targetScene">Main transition target scene.</param>
    /// <param name="hasTargetCompanionScene">True when a companion scene was loaded with the target.</param>
    /// <param name="targetCompanionScene">Companion scene definition.</param>
    /// <param name="persistentPlayerLoadScenes">Persistent player scenes loaded for the target.</param>
    /// <param name="transitionPurpose">Purpose selecting first-load or persistent-runtime readiness policy.</param>
    /// <param name="readinessWarmupFrames">Mutable warm-up frame counter.</param>
    /// <param name="readinessWarmupSeconds">Mutable warm-up duration counter.</param>
    /// <returns>True when the transition can reveal the target scene.</returns>
    public static bool TryCompleteReadinessWarmup(EntityManager entityManager,
                                                  GameSceneDefinitionElement targetScene,
                                                  bool hasTargetCompanionScene,
                                                  GameSceneDefinitionElement targetCompanionScene,
                                                  List<GameSceneDefinitionElement> persistentPlayerLoadScenes,
                                                  GameSceneTransitionPurpose transitionPurpose,
                                                  ref int readinessWarmupFrames,
                                                  ref float readinessWarmupSeconds)
    {
        entityManager.CompleteDependencyBeforeRO<LocalToWorld>();

        if (!GameSceneTransitionReadinessUtility.AreTransitionScenesReady(targetScene,
                                                                         hasTargetCompanionScene,
                                                                         targetCompanionScene,
                                                                         persistentPlayerLoadScenes,
                                                                         transitionPurpose))
        {
            ResetReadinessWarmup(ref readinessWarmupFrames, ref readinessWarmupSeconds);
            return false;
        }

        if (!GameProceduralRoomArrivalUtility.TryPreparePendingArrival(entityManager))
        {
            ResetReadinessWarmup(ref readinessWarmupFrames, ref readinessWarmupSeconds);
            return false;
        }

        readinessWarmupFrames++;
        readinessWarmupSeconds += Mathf.Max(0f, Time.unscaledDeltaTime);

        if (readinessWarmupFrames < MinimumReadyWarmupFrames)
            return false;

        return readinessWarmupSeconds >= MinimumReadyWarmupSeconds;
    }

    /// <summary>
    /// Clears hidden warm-up progress when a new transition starts or readiness drops.
    /// </summary>
    /// <param name="readinessWarmupFrames">Mutable warm-up frame counter.</param>
    /// <param name="readinessWarmupSeconds">Mutable warm-up duration counter.</param>
    public static void ResetReadinessWarmup(ref int readinessWarmupFrames, ref float readinessWarmupSeconds)
    {
        readinessWarmupFrames = 0;
        readinessWarmupSeconds = 0f;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores resolved fade timings for one transition request.
/// </summary>
internal readonly struct GameSceneTransitionFadeTimings
{
    #region Fields
    public readonly float FadeOutSeconds;
    public readonly float PostLoadReadyExtraSeconds;
    public readonly float FadeInSeconds;
    #endregion

    #region Methods

    #region Constructor
    /// <summary>
    /// Creates immutable fade timing data for the transition executor.
    /// </summary>
    /// <param name="fadeOutSeconds">Seconds used by fade-out.</param>
    /// <param name="postLoadReadyExtraSeconds">Seconds spent fully black after readiness.</param>
    /// <param name="fadeInSeconds">Seconds used by fade-in.</param>
    public GameSceneTransitionFadeTimings(float fadeOutSeconds, float postLoadReadyExtraSeconds, float fadeInSeconds)
    {
        FadeOutSeconds = fadeOutSeconds;
        PostLoadReadyExtraSeconds = postLoadReadyExtraSeconds;
        FadeInSeconds = fadeInSeconds;
    }
    #endregion

    #endregion
}
