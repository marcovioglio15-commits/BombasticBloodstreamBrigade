using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Advances active Impact Frame effects, applies global time scale, and publishes the fullscreen filter snapshot.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpActivationSystem))]
public partial struct PlayerImpactFrameUpdateSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers Impact Frame state as the required runtime data for this presentation/update system.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerImpactFrameState>();
        state.RequireForUpdate<PlayerImpactFrameBuildInState>();
    }

    /// <summary>
    /// Ticks every active Impact Frame state using unscaled time and applies the strongest global result.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        float unscaledDeltaTime = math.max(0f, Time.unscaledDeltaTime);
        float strongestSlowPercent = 0f;
        float strongestPresentationScore = 0f;
        bool hasActiveImpactFrame = false;
        PlayerImpactFramePresentationSnapshot strongestSnapshot = default;

        foreach ((RefRW<PlayerImpactFrameState> impactFrameState,
                  RefRW<PlayerImpactFrameBuildInState> buildInState)
                 in SystemAPI.Query<RefRW<PlayerImpactFrameState>, RefRW<PlayerImpactFrameBuildInState>>())
        {
            bool impactActive = PlayerImpactFrameRuntimeUtility.Tick(ref impactFrameState.ValueRW, unscaledDeltaTime);
            bool buildInActive = PlayerImpactFrameBuildInRuntimeUtility.Tick(ref buildInState.ValueRW, unscaledDeltaTime);

            if (!impactActive && !buildInActive)
                continue;

            hasActiveImpactFrame = true;

            if (impactActive)
                AccumulateEffect(in impactFrameState.ValueRO.Effect,
                                 math.saturate(impactFrameState.ValueRO.CurrentBlend),
                                 impactFrameState.ValueRO.EffectElapsedUnscaledSeconds,
                                 impactFrameState.ValueRO.TotalDurationUnscaledSeconds,
                                 impactFrameState.ValueRO.EffectOriginWorldPosition,
                                 impactFrameState.ValueRO.HasWorldOrigin,
                                 ref strongestSlowPercent,
                                 ref strongestPresentationScore,
                                 ref strongestSnapshot);

            if (buildInActive)
                AccumulateEffect(in buildInState.ValueRO.Effect,
                                 math.saturate(buildInState.ValueRO.CurrentBlend),
                                 0f,
                                 1f,
                                 float3.zero,
                                 0,
                                 ref strongestSlowPercent,
                                 ref strongestPresentationScore,
                                 ref strongestSnapshot);
        }

        if (hasActiveImpactFrame)
        {
            if (strongestSlowPercent > 0f)
                PlayerImpactFrameTimeScaleUtility.ApplySlowPercent(strongestSlowPercent);
            else
                PlayerImpactFrameTimeScaleUtility.Clear();

            PlayerImpactFramePresentationRuntime.SetSnapshot(in strongestSnapshot);
            return;
        }

        PlayerImpactFrameTimeScaleUtility.Clear();
        PlayerImpactFramePresentationRuntime.ClearSnapshot();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Builds the presentation snapshot consumed by the render callback.
    /// </summary>
    /// <param name="impactFrameState">Current Impact Frame ECS state.</param>
    /// <param name="currentBlend">Current normalized blend already clamped to 0-1.</param>
    /// <returns>Presentation snapshot for the active fullscreen filter.</returns>
    private static void AccumulateEffect(in ImpactFrameEffectConfig effect,
                                         float currentBlend,
                                         float effectElapsedUnscaledSeconds,
                                         float totalDurationUnscaledSeconds,
                                         float3 effectOriginWorldPosition,
                                         byte hasWorldOrigin,
                                         ref float strongestSlowPercent,
                                         ref float strongestPresentationScore,
                                         ref PlayerImpactFramePresentationSnapshot strongestSnapshot)
    {
        float slowPercent = math.clamp(effect.TimeSlowdownPercent * currentBlend, 0f, 100f);

        if (slowPercent > strongestSlowPercent)
            strongestSlowPercent = slowPercent;

        float presentationScore = currentBlend * math.saturate(effect.OverlayIntensity);

        if (presentationScore <= strongestPresentationScore)
            return;

        strongestPresentationScore = presentationScore;
        float lifetimeProgress = math.saturate(effectElapsedUnscaledSeconds /
                                               math.max(0.0001f, totalDurationUnscaledSeconds));
        strongestSnapshot = new PlayerImpactFramePresentationSnapshot(currentBlend,
                                                                      effect.PresentationScope,
                                                                      effect.OverlayIntensity,
                                                                      effect.FilterTintRgba,
                                                                      effect.DesaturationAmount,
                                                                      effect.VignetteIntensity,
                                                                      effect.VignetteSoftness,
                                                                      effect.VignetteExtent,
                                                                      effect.VignetteTintRgba,
                                                                      effect.RadialVignetteIntensity,
                                                                      effect.RadialVignetteRadius,
                                                                      effect.RadialVignetteSoftness,
                                                                      effect.RadialVignetteTintRgba,
                                                                      effect.ChromaticAberration,
                                                                      effect.ScanlineIntensity,
                                                                      effect.ScanlineFrequency,
                                                                      effect.FlashIntensity,
                                                                      effect.RadialDistortion,
                                                                      effect.ShockwaveIntensity,
                                                                      effect.ShockwaveRadius,
                                                                      effect.ShockwaveThickness,
                                                                      effect.ZoomPunchIntensity,
                                                                      effect.InvertIntensity,
                                                                      effect.PosterizeIntensity,
                                                                      effect.PosterizeSteps,
                                                                      effect.EdgeInkIntensity,
                                                                      effect.ScreenTearIntensity,
                                                                      effect.ScreenTearFrequency,
                                                                      effect.PaletteFlashIntensity,
                                                                      effect.PaletteFlashTintRgba,
                                                                      lifetimeProgress,
                                                                      effectOriginWorldPosition,
                                                                      hasWorldOrigin);
    }
    #endregion

    #endregion
}
