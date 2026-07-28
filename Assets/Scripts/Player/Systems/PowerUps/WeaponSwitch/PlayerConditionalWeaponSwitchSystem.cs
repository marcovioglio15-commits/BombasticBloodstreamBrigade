using Unity.Entities;

/// <summary>
/// Re-evaluates the conditional weapon switch table whenever the unified scalable stats hash changes. Running
/// in <see cref="PlayerControllerSystemGroup"/> after the nested conditional-switch scaling rebuild keeps the
/// evaluated state aligned with the latest formula-derived table, so the animator presentation pass that runs
/// in the presentation phase consumes a fresh selection. The scaling-sync ordering guarantees that a stat
/// change landed during simulation propagates into the conditional state in the same frame.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerRuntimeConditionalWeaponSwitchScalingSystem))]
public partial struct PlayerConditionalWeaponSwitchSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime data required to evaluate conditional weapon switches. The system runs only on
    /// player entities that have both the runtime scaling state and a conditional weapon switch config.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerRuntimeScalingState>();
        state.RequireForUpdate<PlayerConditionalWeaponSwitchConfig>();
        state.RequireForUpdate<PlayerConditionalWeaponSwitchState>();
    }

    /// <summary>
    /// Re-evaluates the conditional weapon switch table when the scalable stats hash changes. Empty tables
    /// short-circuit on the config entry count so  presets without conditional entries cost nothing.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        BufferLookup<PlayerScalableStatElement> scalableStatsLookup = SystemAPI.GetBufferLookup<PlayerScalableStatElement>(true);
        BufferLookup<PlayerConditionalWeaponSwitchEntryElement> entryLookup = SystemAPI.GetBufferLookup<PlayerConditionalWeaponSwitchEntryElement>(true);
        BufferLookup<PlayerConditionalWeaponSwitchConditionElement> conditionLookup = SystemAPI.GetBufferLookup<PlayerConditionalWeaponSwitchConditionElement>(true);

        foreach ((RefRO<PlayerRuntimeScalingState> runtimeScalingState,
                  RefRO<PlayerConditionalWeaponSwitchConfig> conditionalConfig,
                  RefRW<PlayerConditionalWeaponSwitchState> conditionalState,
                  Entity entity)
                 in SystemAPI.Query<RefRO<PlayerRuntimeScalingState>,
                                    RefRO<PlayerConditionalWeaponSwitchConfig>,
                                    RefRW<PlayerConditionalWeaponSwitchState>>()
                             .WithEntityAccess())
        {
            if (runtimeScalingState.ValueRO.Initialized == 0)
                continue;

            // Empty tables stay neutral without scanning state every frame.
            if (conditionalConfig.ValueRO.EntryCount == 0)
            {
                ResetStateIfDirty(ref conditionalState.ValueRW, runtimeScalingState.ValueRO.LastScalableStatsHash);
                continue;
            }

            // Skip re-evaluation when stats have not changed and we already produced an initialized result.
            if (conditionalState.ValueRO.Initialized != 0 &&
                conditionalState.ValueRO.LastEvaluatedScalableStatsHash == runtimeScalingState.ValueRO.LastScalableStatsHash)
            {
                continue;
            }

            if (!entryLookup.HasBuffer(entity) || !conditionLookup.HasBuffer(entity) || !scalableStatsLookup.HasBuffer(entity))
                continue;

            DynamicBuffer<PlayerConditionalWeaponSwitchEntryElement> entryBuffer = entryLookup[entity];
            DynamicBuffer<PlayerConditionalWeaponSwitchConditionElement> conditionBuffer = conditionLookup[entity];
            DynamicBuffer<PlayerScalableStatElement> scalableStatsBuffer = scalableStatsLookup[entity];
            PlayerConditionalWeaponSwitchRuntimeUtility.Evaluate(in entryBuffer,
                                                                  in conditionBuffer,
                                                                  in scalableStatsBuffer,
                                                                  ref conditionalState.ValueRW);
            conditionalState.ValueRW.LastEvaluatedScalableStatsHash = runtimeScalingState.ValueRO.LastScalableStatsHash;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resets the conditional state to its neutral form so an empty table never leaks a stale match into the
    /// animator presentation pipeline. The cached hash is stamped to suppress redundant resets.
    /// </summary>
    /// <param name="state">Conditional state mutated in place.</param>
    /// <param name="scalableStatsHash">Current runtime scalable stats hash.</param>
    private static void ResetStateIfDirty(ref PlayerConditionalWeaponSwitchState state, uint scalableStatsHash)
    {
        if (state.Initialized != 0 && state.HasMatch == 0 && state.LastEvaluatedScalableStatsHash == scalableStatsHash)
            return;

        state.HasMatch = 0;
        state.OverridesPowerUpSwitch = 0;
        state.MatchedPriority = int.MinValue;
        state.WeaponId = default;
        state.Initialized = 1;
        state.LastEvaluatedScalableStatsHash = scalableStatsHash;
    }
    #endregion

    #endregion
}
