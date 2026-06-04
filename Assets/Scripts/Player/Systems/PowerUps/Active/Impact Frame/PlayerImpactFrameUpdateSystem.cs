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

        foreach (RefRW<PlayerImpactFrameState> impactFrameState in SystemAPI.Query<RefRW<PlayerImpactFrameState>>())
        {
            if (!PlayerImpactFrameRuntimeUtility.Tick(ref impactFrameState.ValueRW, unscaledDeltaTime))
                continue;

            hasActiveImpactFrame = true;
            float currentBlend = math.saturate(impactFrameState.ValueRO.CurrentBlend);
            float slowPercent = math.clamp(impactFrameState.ValueRO.TimeSlowdownPercent * currentBlend, 0f, 100f);

            if (slowPercent > strongestSlowPercent)
                strongestSlowPercent = slowPercent;

            float presentationScore = currentBlend * math.saturate(impactFrameState.ValueRO.OverlayIntensity);

            if (presentationScore <= strongestPresentationScore)
                continue;

            PlayerImpactFrameState currentImpactFrameState = impactFrameState.ValueRO;
            strongestPresentationScore = presentationScore;
            strongestSnapshot = BuildSnapshot(in currentImpactFrameState, currentBlend);
        }

        if (hasActiveImpactFrame)
        {
            PlayerImpactFrameTimeScaleUtility.ApplySlowPercent(strongestSlowPercent);
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
    private static PlayerImpactFramePresentationSnapshot BuildSnapshot(in PlayerImpactFrameState impactFrameState, float currentBlend)
    {
        float lifetimeProgress = math.saturate(impactFrameState.EffectElapsedUnscaledSeconds /
                                               math.max(0.0001f, impactFrameState.TotalDurationUnscaledSeconds));
        return new PlayerImpactFramePresentationSnapshot(currentBlend,
                                                         impactFrameState.OverlayIntensity,
                                                         impactFrameState.FilterTintRgba,
                                                         impactFrameState.DesaturationAmount,
                                                         impactFrameState.VignetteIntensity,
                                                         impactFrameState.VignetteSoftness,
                                                         impactFrameState.ChromaticAberration,
                                                         impactFrameState.ScanlineIntensity,
                                                         impactFrameState.ScanlineFrequency,
                                                         impactFrameState.FlashIntensity,
                                                         impactFrameState.RadialDistortion,
                                                         impactFrameState.ShockwaveIntensity,
                                                         impactFrameState.ShockwaveRadius,
                                                         impactFrameState.ShockwaveThickness,
                                                         impactFrameState.ZoomPunchIntensity,
                                                         impactFrameState.InvertIntensity,
                                                         impactFrameState.PosterizeIntensity,
                                                         impactFrameState.PosterizeSteps,
                                                         impactFrameState.EdgeInkIntensity,
                                                         impactFrameState.ScreenTearIntensity,
                                                         impactFrameState.ScreenTearFrequency,
                                                         impactFrameState.PaletteFlashIntensity,
                                                         impactFrameState.PaletteFlashTintRgba,
                                                         lifetimeProgress,
                                                         impactFrameState.EffectOriginWorldPosition,
                                                         impactFrameState.HasWorldOrigin);
    }
    #endregion

    #endregion
}
