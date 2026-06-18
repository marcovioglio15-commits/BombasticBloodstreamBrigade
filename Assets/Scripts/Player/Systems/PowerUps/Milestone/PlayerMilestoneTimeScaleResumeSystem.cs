using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Restores Time.timeScale smoothly after a milestone power-up selection closes.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerMilestonePowerUpSelectionResolveSystem))]
public partial struct PlayerMilestoneTimeScaleResumeSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers the runtime components required to restore Time.timeScale after milestone selections.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerMilestonePowerUpSelectionState>();
        state.RequireForUpdate<PlayerMilestoneTimeScaleResumeState>();
    }

    /// <summary>
    /// Advances the smooth Time.timeScale resume using unscaled time so the ramp progresses even from a full pause.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>

    public void OnUpdate(ref SystemState state)
    {
        float unscaledDeltaTime = math.max(0f, Time.unscaledDeltaTime);
        ComponentLookup<PlayerRunOutcomeState> runOutcomeLookup = SystemAPI.GetComponentLookup<PlayerRunOutcomeState>(true);

        foreach ((RefRO<PlayerMilestonePowerUpSelectionState> selectionState,
                  RefRW<PlayerMilestoneTimeScaleResumeState> resumeState,
                  Entity entity)
                 in SystemAPI.Query<RefRO<PlayerMilestonePowerUpSelectionState>,
                                    RefRW<PlayerMilestoneTimeScaleResumeState>>()
                             .WithEntityAccess())
        {
            if (PlayerRunOutcomeRuntimeUtility.IsFinalized(entity, in runOutcomeLookup))
            {
                if (resumeState.ValueRO.IsResuming != 0)
                    resumeState.ValueRW = PlayerMilestoneSelectionOutcomeUtility.CreateInactiveResumeState();

                Time.timeScale = 0f;
                continue;
            }

            // Any new milestone pause interrupts a previous resume immediately.
            if (selectionState.ValueRO.IsSelectionActive != 0)
            {
                if (resumeState.ValueRO.IsResuming != 0)
                    resumeState.ValueRW = PlayerMilestoneSelectionOutcomeUtility.CreateInactiveResumeState();

                if (Time.timeScale > 0f)
                    Time.timeScale = 0f;

                continue;
            }

            if (resumeState.ValueRO.IsResuming == 0)
                continue;

            float durationSeconds = math.max(0f, resumeState.ValueRO.DurationSeconds);

            if (durationSeconds <= 0f)
            {
                Time.timeScale = math.clamp(resumeState.ValueRO.TargetTimeScale, 0f, 1f);
                resumeState.ValueRW = PlayerMilestoneSelectionOutcomeUtility.CreateInactiveResumeState();
                continue;
            }

            float elapsedUnscaledSeconds = resumeState.ValueRO.ElapsedUnscaledSeconds + unscaledDeltaTime;
            float normalizedProgress = math.saturate(elapsedUnscaledSeconds / durationSeconds);
            float currentTimeScale = math.lerp(resumeState.ValueRO.StartTimeScale,
                                               resumeState.ValueRO.TargetTimeScale,
                                               normalizedProgress);

            Time.timeScale = math.clamp(currentTimeScale, 0f, 1f);

            if (normalizedProgress < 1f)
            {
                resumeState.ValueRW.ElapsedUnscaledSeconds = elapsedUnscaledSeconds;
                continue;
            }

            Time.timeScale = math.clamp(resumeState.ValueRO.TargetTimeScale, 0f, 1f);
            resumeState.ValueRW = PlayerMilestoneSelectionOutcomeUtility.CreateInactiveResumeState();
        }
    }
    #endregion

    #endregion
}
