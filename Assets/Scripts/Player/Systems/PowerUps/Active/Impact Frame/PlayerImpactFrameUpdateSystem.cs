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
        state.RequireForUpdate<PlayerGhostTrailState>();
        state.RequireForUpdate<PlayerPowerUpsConfigElement>();
        state.RequireForUpdate<PlayerPowerUpsState>();
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
        bool hasActiveFeedback = false;
        PlayerImpactFramePresentationSnapshot strongestSnapshot = default;

        foreach ((RefRW<PlayerImpactFrameState> impactFrameState,
                  RefRW<PlayerImpactFrameBuildInState> buildInState,
                  RefRW<PlayerGhostTrailState> ghostTrailState,
                  RefRO<PlayerPowerUpsState> powerUpsState,
                  DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer)
                 in SystemAPI.Query<RefRW<PlayerImpactFrameState>,
                                    RefRW<PlayerImpactFrameBuildInState>,
                                    RefRW<PlayerGhostTrailState>,
                                    RefRO<PlayerPowerUpsState>,
                                    DynamicBuffer<PlayerPowerUpsConfigElement>>())
        {
            ReconcileMatchedToggle(ref ghostTrailState.ValueRW,
                                   in powerUpsState.ValueRO,
                                   powerUpsConfigBuffer);
            bool impactActive = PlayerImpactFrameRuntimeUtility.Tick(ref impactFrameState.ValueRW, unscaledDeltaTime);
            bool buildInActive = PlayerImpactFrameBuildInRuntimeUtility.Tick(ref buildInState.ValueRW, unscaledDeltaTime);
            bool ghostTrailActive = PlayerGhostTrailRuntimeUtility.Tick(ref ghostTrailState.ValueRW, unscaledDeltaTime);

            if (!impactActive &&
                !buildInActive &&
                (!ghostTrailActive || ghostTrailState.ValueRO.Config.ScreenFeedbackEnabled == 0))
                continue;

            hasActiveFeedback = true;

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

            if (ghostTrailActive && ghostTrailState.ValueRO.Config.ScreenFeedbackEnabled != 0)
                AccumulateEffect(in ghostTrailState.ValueRO.Config.ScreenFeedback,
                                 math.saturate(ghostTrailState.ValueRO.CurrentBlend),
                                 ghostTrailState.ValueRO.EffectElapsedUnscaledSeconds,
                                 ghostTrailState.ValueRO.TotalDurationUnscaledSeconds,
                                 float3.zero,
                                 0,
                                 ref strongestSlowPercent,
                                 ref strongestPresentationScore,
                                 ref strongestSnapshot);
        }

        if (hasActiveFeedback)
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
    /// Stops matched-toggle Ghost Trail emission after loadout resets, replacements, or maintenance deactivation.
    /// </summary>
    /// <param name="ghostTrailState">Mutable Ghost Trail state.</param>
    /// <param name="powerUpsState">Current active-slot runtime flags.</param>
    /// <param name="powerUpsConfigBuffer">Current active-slot configuration buffer.</param>
    private static void ReconcileMatchedToggle(ref PlayerGhostTrailState ghostTrailState,
                                               in PlayerPowerUpsState powerUpsState,
                                               DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer)
    {
        if (ghostTrailState.MatchToggleActivationDuration == 0)
            return;

        PlayerPowerUpsConfig powerUpsConfig;
        PlayerPowerUpsConfigBufferUtility.Read(powerUpsConfigBuffer, out powerUpsConfig);
        PlayerPowerUpSlotConfig slotConfig;
        byte slotActive;

        switch (ghostTrailState.MatchedToggleSlotIndex)
        {
            case 0:
                slotConfig = powerUpsConfig.PrimarySlot;
                slotActive = powerUpsState.PrimaryIsActive;
                break;
            case 1:
                slotConfig = powerUpsConfig.SecondarySlot;
                slotActive = powerUpsState.SecondaryIsActive;
                break;
            default:
                PlayerGhostTrailRuntimeUtility.Clear(ref ghostTrailState);
                return;
        }

        if (slotActive != 0 &&
            slotConfig.IsDefined != 0 &&
            slotConfig.ToolKind == ActiveToolKind.PassiveToggle &&
            slotConfig.HasGhostTrail != 0)
        {
            return;
        }

        PlayerGhostTrailRuntimeUtility.StopMatchedToggle(ref ghostTrailState,
                                                         ghostTrailState.MatchedToggleSlotIndex);
    }

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

        float presentationScore = currentBlend *
                                  math.max(math.saturate(effect.OverlayIntensity),
                                           ResolveCameraFeedbackScore(in effect.CameraFeedback));

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

    /// <summary>
    /// Resolves a comparable presentation score so camera-only profiles can publish a runtime snapshot.
    /// </summary>
    /// <param name="cameraFeedback">Camera feedback profile to score.</param>
    /// <returns>Non-negative score derived from enabled motion and zoom amplitudes.</returns>
    private static float ResolveCameraFeedbackScore(in ImpactFrameCameraFeedbackConfig cameraFeedback)
    {
        if (cameraFeedback.Enabled == 0)
            return 0f;

        float score = math.max(math.max(math.max(0f, cameraFeedback.PositionalAmplitude),
                                        math.max(0f, cameraFeedback.ForwardAmplitude)),
                               math.max(0f, cameraFeedback.RotationalAmplitude));

        if (cameraFeedback.ZoomEnabled != 0)
            score = math.max(score, math.abs(cameraFeedback.ZoomFovDelta));

        return score;
    }
    #endregion

    #endregion
}
