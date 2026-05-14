using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Provides timing and readiness helpers for the managed scene transition executor.
/// /params None.
/// /returns None.
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
    /// /params config Scene manager runtime config.
    /// /params transition Transition override data.
    /// /returns Fade timings used by the current transition.
    /// </summary>
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
    /// /params unscaledDeltaTime Raw Unity unscaled frame delta.
    /// /returns Clamped presentation delta for fade phases.
    /// </summary>
    public static float ResolveFadeStepDeltaTime(float unscaledDeltaTime)
    {
        return Mathf.Min(Mathf.Max(0f, unscaledDeltaTime), MaximumFadeStepSeconds);
    }
    #endregion

    #region Readiness
    /// <summary>
    /// Waits until loaded scenes, gameplay runtime and a short hidden warm-up have completed before fade-in.
    /// /params entityManager EntityManager used to flush LocalToWorld before readiness checks.
    /// /params targetScene Main transition target scene.
    /// /params hasTargetCompanionScene True when a companion scene was loaded with the target.
    /// /params targetCompanionScene Companion scene definition.
    /// /params persistentPlayerLoadScenes Persistent player scenes loaded for the target.
    /// /params readinessWarmupFrames Mutable warm-up frame counter.
    /// /params readinessWarmupSeconds Mutable warm-up duration counter.
    /// /returns True when the transition can reveal the target scene.
    /// </summary>
    public static bool TryCompleteReadinessWarmup(EntityManager entityManager,
                                                  GameSceneDefinitionElement targetScene,
                                                  bool hasTargetCompanionScene,
                                                  GameSceneDefinitionElement targetCompanionScene,
                                                  List<GameSceneDefinitionElement> persistentPlayerLoadScenes,
                                                  ref int readinessWarmupFrames,
                                                  ref float readinessWarmupSeconds)
    {
        entityManager.CompleteDependencyBeforeRO<LocalToWorld>();

        if (!GameSceneTransitionReadinessUtility.AreTransitionScenesReady(targetScene,
                                                                         hasTargetCompanionScene,
                                                                         targetCompanionScene,
                                                                         persistentPlayerLoadScenes))
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
    /// /params readinessWarmupFrames Mutable warm-up frame counter.
    /// /params readinessWarmupSeconds Mutable warm-up duration counter.
    /// /returns None.
    /// </summary>
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
/// /params None.
/// /returns None.
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
    /// /params fadeOutSeconds Seconds used by fade-out.
    /// /params postLoadReadyExtraSeconds Seconds spent fully black after readiness.
    /// /params fadeInSeconds Seconds used by fade-in.
    /// /returns None.
    /// </summary>
    public GameSceneTransitionFadeTimings(float fadeOutSeconds, float postLoadReadyExtraSeconds, float fadeInSeconds)
    {
        FadeOutSeconds = fadeOutSeconds;
        PostLoadReadyExtraSeconds = postLoadReadyExtraSeconds;
        FadeInSeconds = fadeInSeconds;
    }
    #endregion

    #endregion
}
