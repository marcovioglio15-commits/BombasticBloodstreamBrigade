using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Drives the reusable Impact Frame build-in and final-impact timelines from normalized player death playback.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PlayerRunOutcomeSystem))]
public partial struct PlayerDeathImpactFrameSystem : ISystem
{
    #region Constants
    private const float MinimumDurationSeconds = 0.0001f;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers death playback and Impact Frame state required by the sequence driver.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerDeathAnimationConfig>();
        state.RequireForUpdate<PlayerDeathAnimationState>();
        state.RequireForUpdate<PlayerRunOutcomeState>();
        state.RequireForUpdate<PlayerImpactFrameState>();
        state.RequireForUpdate<PlayerImpactFrameBuildInState>();
    }

    /// <summary>
    /// Maps death playback into build-in progress, final-impact activation, and deterministic completion.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        foreach ((RefRO<PlayerDeathAnimationConfig> deathConfig,
                  RefRW<PlayerDeathAnimationState> deathState,
                  RefRO<PlayerRunOutcomeState> runOutcomeState,
                  RefRW<PlayerImpactFrameState> impactFrameState,
                  RefRW<PlayerImpactFrameBuildInState> buildInState)
                 in SystemAPI.Query<RefRO<PlayerDeathAnimationConfig>,
                                    RefRW<PlayerDeathAnimationState>,
                                    RefRO<PlayerRunOutcomeState>,
                                    RefRW<PlayerImpactFrameState>,
                                    RefRW<PlayerImpactFrameBuildInState>>())
        {
            bool isDying = runOutcomeState.ValueRO.IsDying != 0 && runOutcomeState.ValueRO.IsFinalized == 0;

            if (!isDying)
            {
                if (runOutcomeState.ValueRO.IsFinalized == 0)
                {
                    deathState.ValueRW.ImpactFrameApplied = 0;
                    deathState.ValueRW.ImpactFrameCompleted = 0;
                }

                continue;
            }

            if (deathConfig.ValueRO.ImpactFrameEnabled == 0 || deathState.ValueRO.ImpactFrameCompleted != 0)
                continue;

            float playbackDuration = math.max(MinimumDurationSeconds, deathConfig.ValueRO.PlaybackDurationSeconds);
            float normalizedTime = math.saturate(runOutcomeState.ValueRO.DyingElapsedSeconds / playbackDuration);
            float buildInStart = math.saturate(deathConfig.ValueRO.ImpactFrameBuildInStartNormalizedTime);
            float applyTime = math.max(buildInStart, math.saturate(deathConfig.ValueRO.ImpactFrameApplyNormalizedTime));
            float endTime = math.max(applyTime, math.saturate(deathConfig.ValueRO.ImpactFrameEndNormalizedTime));

            if (normalizedTime < applyTime)
            {
                RequestBuildIn(in deathConfig.ValueRO.ImpactFrame.BuildIn,
                               normalizedTime,
                               buildInStart,
                               applyTime,
                               ref buildInState.ValueRW);
                continue;
            }

            if (deathState.ValueRO.ImpactFrameApplied == 0)
            {
                ImpactFramePowerUpConfig finalConfig = deathConfig.ValueRO.ImpactFrame;
                finalConfig.DurationMode = ImpactFrameDurationMode.UnscaledSecondsOnly;
                finalConfig.DurationFrames = 0;
                finalConfig.MaximumUnscaledDurationSeconds = math.max(MinimumDurationSeconds,
                                                                       (endTime - applyTime) * playbackDuration);
                PlayerImpactFrameRuntimeUtility.Activate(ref impactFrameState.ValueRW, in finalConfig);
                deathState.ValueRW.ImpactFrameApplied = 1;
            }

            if (normalizedTime < endTime)
                continue;

            PlayerImpactFrameRuntimeUtility.Clear(ref impactFrameState.ValueRW);
            buildInState.ValueRW = default;
            deathState.ValueRW.ImpactFrameCompleted = 1;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Requests death build-in progress across the configured normalized interval.
    /// </summary>
    /// <param name="buildInConfig">Reusable build-in profile stored in the death Impact Frame config.</param>
    /// <param name="normalizedTime">Current normalized death playback.</param>
    /// <param name="startTime">Normalized build-in start point.</param>
    /// <param name="applyTime">Normalized final-impact application point.</param>
    /// <param name="buildInState">Mutable shared build-in state receiving the request.</param>
    private static void RequestBuildIn(in ImpactFrameBuildInConfig buildInConfig,
                                       float normalizedTime,
                                       float startTime,
                                       float applyTime,
                                       ref PlayerImpactFrameBuildInState buildInState)
    {
        if (buildInConfig.Enabled == 0 || normalizedTime < startTime)
            return;

        float duration = math.max(MinimumDurationSeconds, applyTime - startTime);
        PlayerImpactFrameBuildInRuntimeUtility.Request(ref buildInState,
                                                       in buildInConfig,
                                                       math.saturate((normalizedTime - startTime) / duration));
    }
    #endregion

    #endregion
}
