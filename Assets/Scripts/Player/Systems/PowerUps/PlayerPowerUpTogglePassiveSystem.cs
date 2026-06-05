using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Applies maintenance and passive-state aggregation for active slots that toggle passive-compatible effects.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpActivationSystem))]
[UpdateBefore(typeof(PlayerBulletTimeUpdateSystem))]
[UpdateBefore(typeof(PlayerMovementSpeedSystem))]
public partial struct PlayerPowerUpTogglePassiveSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers the runtime data required by toggleable passive power-ups.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpsConfigElement>();
        state.RequireForUpdate<PlayerPowerUpsState>();
        state.RequireForUpdate<PlayerPassiveToolsStateElement>();
        state.RequireForUpdate<PlayerBulletTimeState>();
        state.RequireForUpdate<EquippedPassiveToolElement>();
    }

    /// <summary>
    /// Updates toggle startup timers, maintenance ticks, and the aggregated passive state snapshot.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        ComponentLookup<PlayerHealth> healthLookup = SystemAPI.GetComponentLookup<PlayerHealth>(false);
        ComponentLookup<PlayerShield> shieldLookup = SystemAPI.GetComponentLookup<PlayerShield>(false);

        foreach ((DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer,
                  RefRW<PlayerPowerUpsState> powerUpsState,
                  DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                  DynamicBuffer<PlayerPassiveToolsStateElement> passiveToolsStateBuffer,
                  RefRW<PlayerBulletTimeState> bulletTimeState,
                  Entity playerEntity)
                 in SystemAPI.Query<DynamicBuffer<PlayerPowerUpsConfigElement>,
                                    RefRW<PlayerPowerUpsState>,
                                    DynamicBuffer<EquippedPassiveToolElement>,
                                    DynamicBuffer<PlayerPassiveToolsStateElement>,
                                    RefRW<PlayerBulletTimeState>>()
                             .WithEntityAccess())
        {
            PlayerPowerUpsConfig powerUpsConfig;
            PlayerPowerUpsConfigBufferUtility.Read(powerUpsConfigBuffer,
                                                   out powerUpsConfig);
            ref PlayerPassiveToolsState aggregatedPassiveToolsState = ref PlayerPassiveToolsStateBufferUtility.GetStateRef(passiveToolsStateBuffer);
            PlayerPassiveToolsAggregationUtility.RebuildPassiveToolsState(equippedPassiveTools,
                                                                          ref aggregatedPassiveToolsState);
            PlayerBulletTimeState currentBulletTimeState = bulletTimeState.ValueRO;
            byte isShootingSuppressed = powerUpsState.ValueRO.IsShootingSuppressed;
            bool healthChanged = false;
            PlayerHealth updatedHealth = default;
            bool shieldChanged = false;
            PlayerShield updatedShield = default;
            float primaryEnergy = powerUpsState.ValueRO.PrimaryEnergy;
            float secondaryEnergy = powerUpsState.ValueRO.SecondaryEnergy;
            float primaryCooldownRemaining = powerUpsState.ValueRO.PrimaryCooldownRemaining;
            float secondaryCooldownRemaining = powerUpsState.ValueRO.SecondaryCooldownRemaining;
            byte primaryIsActive = powerUpsState.ValueRO.PrimaryIsActive;
            byte secondaryIsActive = powerUpsState.ValueRO.SecondaryIsActive;
            float primaryMaintenanceTickTimer = powerUpsState.ValueRO.PrimaryMaintenanceTickTimer;
            float secondaryMaintenanceTickTimer = powerUpsState.ValueRO.SecondaryMaintenanceTickTimer;
            float toggleBulletTimeSlowPercent = 0f;
            float toggleBulletTimeTransitionTimeSeconds = 0f;

            ProcessTogglePassiveSlot(in powerUpsConfig.PrimarySlot,
                                     deltaTime,
                                     playerEntity,
                                     ref primaryEnergy,
                                     ref primaryCooldownRemaining,
                                     ref primaryIsActive,
                                     ref primaryMaintenanceTickTimer,
                                     ref aggregatedPassiveToolsState,
                                     ref isShootingSuppressed,
                                     ref healthLookup,
                                     ref updatedHealth,
                                     ref healthChanged,
                                     ref shieldLookup,
                                     ref updatedShield,
                                     ref shieldChanged,
                                     ref toggleBulletTimeSlowPercent,
                                     ref toggleBulletTimeTransitionTimeSeconds);
            ProcessTogglePassiveSlot(in powerUpsConfig.SecondarySlot,
                                     deltaTime,
                                     playerEntity,
                                     ref secondaryEnergy,
                                     ref secondaryCooldownRemaining,
                                     ref secondaryIsActive,
                                     ref secondaryMaintenanceTickTimer,
                                     ref aggregatedPassiveToolsState,
                                     ref isShootingSuppressed,
                                     ref healthLookup,
                                     ref updatedHealth,
                                     ref healthChanged,
                                     ref shieldLookup,
                                     ref updatedShield,
                                     ref shieldChanged,
                                     ref toggleBulletTimeSlowPercent,
                                     ref toggleBulletTimeTransitionTimeSeconds);
            if (healthChanged)
                healthLookup[playerEntity] = updatedHealth;

            if (shieldChanged)
                shieldLookup[playerEntity] = updatedShield;

            powerUpsState.ValueRW.PrimaryEnergy = primaryEnergy;
            powerUpsState.ValueRW.SecondaryEnergy = secondaryEnergy;
            powerUpsState.ValueRW.PrimaryCooldownRemaining = primaryCooldownRemaining;
            powerUpsState.ValueRW.SecondaryCooldownRemaining = secondaryCooldownRemaining;
            powerUpsState.ValueRW.PrimaryIsActive = primaryIsActive;
            powerUpsState.ValueRW.SecondaryIsActive = secondaryIsActive;
            powerUpsState.ValueRW.PrimaryMaintenanceTickTimer = primaryMaintenanceTickTimer;
            powerUpsState.ValueRW.SecondaryMaintenanceTickTimer = secondaryMaintenanceTickTimer;
            powerUpsState.ValueRW.IsShootingSuppressed = isShootingSuppressed;

            if (toggleBulletTimeSlowPercent <= 0f && currentBulletTimeState.ToggleSlowPercent > 0f)
                toggleBulletTimeTransitionTimeSeconds = math.max(toggleBulletTimeTransitionTimeSeconds,
                                                                 currentBulletTimeState.ToggleTransitionTimeSeconds);

            currentBulletTimeState.ToggleSlowPercent = toggleBulletTimeSlowPercent;
            currentBulletTimeState.ToggleTransitionTimeSeconds = math.max(0f, toggleBulletTimeTransitionTimeSeconds);
            bulletTimeState.ValueRW = currentBulletTimeState;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Applies one slot toggle runtime step including startup timing, maintenance, and passive aggregation.
    /// </summary>
    /// <param name="slotConfig">Slot configuration inspected for toggle maintenance settings.</param>
    /// <param name="deltaTime">Current frame delta time.</param>
    /// <param name="playerEntity">Player entity used for health and shield resource access.</param>
    /// <param name="slotEnergy">Mutable slot energy state.</param>
    /// <param name="cooldownRemaining">Mutable slot timer used as toggle startup lock while active.</param>
    /// <param name="isActive">Mutable toggle-active flag for the slot.</param>
    /// <param name="maintenanceTickTimer">Mutable accumulated maintenance timer.</param>
    /// <param name="passiveToolsState">Aggregated passive state updated with the slot payload when active.</param>
    /// <param name="isShootingSuppressed">Mutable shared shooting suppression flag for the current player frame.</param>
    /// <param name="healthLookup">Health lookup used for non-energy maintenance costs.</param>
    /// <param name="updatedHealth">Cached mutable health value reused within the current caller.</param>
    /// <param name="healthChanged">True when updatedHealth already contains a fetched runtime value.</param>
    /// <param name="shieldLookup">Shield lookup used for shield maintenance costs.</param>
    /// <param name="updatedShield">Cached mutable shield value reused within the current caller.</param>
    /// <param name="shieldChanged">True when updatedShield already contains a fetched runtime value.</param>
    private static void ProcessTogglePassiveSlot(in PlayerPowerUpSlotConfig slotConfig,
                                                 float deltaTime,
                                                 Entity playerEntity,
                                                 ref float slotEnergy,
                                                 ref float cooldownRemaining,
                                                 ref byte isActive,
                                                 ref float maintenanceTickTimer,
                                                 ref PlayerPassiveToolsState passiveToolsState,
                                                 ref byte isShootingSuppressed,
                                                 ref ComponentLookup<PlayerHealth> healthLookup,
                                                 ref PlayerHealth updatedHealth,
                                                 ref bool healthChanged,
                                                 ref ComponentLookup<PlayerShield> shieldLookup,
                                                 ref PlayerShield updatedShield,
                                                 ref bool shieldChanged,
                                                 ref float toggleBulletTimeSlowPercent,
                                                 ref float toggleBulletTimeTransitionTimeSeconds)
    {
        if (slotConfig.IsDefined == 0)
        {
            isActive = 0;
            maintenanceTickTimer = 0f;
            return;
        }

        if (slotConfig.ToolKind != ActiveToolKind.PassiveToggle || slotConfig.Toggleable == 0)
        {
            isActive = 0;
            maintenanceTickTimer = 0f;
            return;
        }

        if (isActive == 0)
        {
            maintenanceTickTimer = 0f;
            return;
        }

        if (slotConfig.SuppressBaseShootingWhileActive != 0)
            isShootingSuppressed = 1;

        if (cooldownRemaining <= 0f)
            ApplyMaintenanceTicks(in slotConfig,
                                  deltaTime,
                                  playerEntity,
                                  ref slotEnergy,
                                  ref cooldownRemaining,
                                  ref isActive,
                                  ref maintenanceTickTimer,
                                  ref healthLookup,
                                  ref updatedHealth,
                                  ref healthChanged,
                                  ref shieldLookup,
                                  ref updatedShield,
                                  ref shieldChanged);

        if (isActive == 0 || slotConfig.TogglePassiveTool.IsDefined == 0)
            return;

        PlayerPassiveToolConfig togglePassiveTool = slotConfig.TogglePassiveTool;

        if (togglePassiveTool.HasBulletTime != 0 && togglePassiveTool.BulletTime.EnemySlowPercent > 0f)
        {
            float slowPercent = math.clamp(togglePassiveTool.BulletTime.EnemySlowPercent, 0f, 100f);
            float transitionTimeSeconds = math.max(0f, togglePassiveTool.BulletTime.TransitionTimeSeconds);

            if (slowPercent > toggleBulletTimeSlowPercent)
            {
                toggleBulletTimeSlowPercent = slowPercent;
                toggleBulletTimeTransitionTimeSeconds = transitionTimeSeconds;
            }
            else if (math.abs(slowPercent - toggleBulletTimeSlowPercent) <= 0.0001f)
            {
                toggleBulletTimeTransitionTimeSeconds = math.max(toggleBulletTimeTransitionTimeSeconds, transitionTimeSeconds);
            }

            togglePassiveTool.HasBulletTime = 0;
            togglePassiveTool.BulletTime = default;
        }

        PlayerPassiveToolsAggregationUtility.AccumulatePassiveTool(ref passiveToolsState, in togglePassiveTool);
    }

    /// <summary>
    /// Applies maintenance ticks after the startup interval has elapsed and deactivates the slot when payment fails.
    /// </summary>
    /// <param name="slotConfig">Slot configuration containing maintenance settings.</param>
    /// <param name="deltaTime">Current frame delta time.</param>
    /// <param name="playerEntity">Player entity used for health and shield resource access.</param>
    /// <param name="slotEnergy">Mutable slot energy state.</param>
    /// <param name="cooldownRemaining">Mutable startup timer reset when the slot deactivates.</param>
    /// <param name="isActive">Mutable toggle-active flag for the slot.</param>
    /// <param name="maintenanceTickTimer">Mutable accumulated maintenance timer.</param>
    /// <param name="healthLookup">Health lookup used for non-energy maintenance costs.</param>
    /// <param name="updatedHealth">Cached mutable health value reused within the current caller.</param>
    /// <param name="healthChanged">True when updatedHealth already contains a fetched runtime value.</param>
    /// <param name="shieldLookup">Shield lookup used for shield maintenance costs.</param>
    /// <param name="updatedShield">Cached mutable shield value reused within the current caller.</param>
    /// <param name="shieldChanged">True when updatedShield already contains a fetched runtime value.</param>
    private static void ApplyMaintenanceTicks(in PlayerPowerUpSlotConfig slotConfig,
                                              float deltaTime,
                                              Entity playerEntity,
                                              ref float slotEnergy,
                                              ref float cooldownRemaining,
                                              ref byte isActive,
                                              ref float maintenanceTickTimer,
                                              ref ComponentLookup<PlayerHealth> healthLookup,
                                              ref PlayerHealth updatedHealth,
                                              ref bool healthChanged,
                                              ref ComponentLookup<PlayerShield> shieldLookup,
                                              ref PlayerShield updatedShield,
                                              ref bool shieldChanged)
    {
        float maintenanceCostPerSecond = math.max(0f, slotConfig.MaintenanceCostPerSecond);
        float maintenanceTicksPerSecond = math.max(0f, slotConfig.MaintenanceTicksPerSecond);

        if (maintenanceCostPerSecond <= 0f || maintenanceTicksPerSecond <= 0f || slotConfig.MaintenanceResource == PowerUpResourceType.None)
            return;

        float tickIntervalSeconds = 1f / maintenanceTicksPerSecond;
        float maintenanceCostPerTick = maintenanceCostPerSecond / maintenanceTicksPerSecond;
        maintenanceTickTimer += math.max(0f, deltaTime);

        while (maintenanceTickTimer + 1e-6f >= tickIntervalSeconds)
        {
            if (!PlayerPowerUpResourceCostUtility.CanPayFlatResourceCost(slotConfig.MaintenanceResource,
                                                                         maintenanceCostPerTick,
                                                                         slotEnergy,
                                                                         playerEntity,
                                                                         ref healthLookup,
                                                                         ref updatedHealth,
                                                                         ref healthChanged,
                                                                         ref shieldLookup,
                                                                         ref updatedShield,
                                                                         ref shieldChanged))
            {
                isActive = 0;
                cooldownRemaining = 0f;
                maintenanceTickTimer = 0f;
                return;
            }

            PlayerPowerUpResourceCostUtility.ConsumeFlatResourceCost(slotConfig.MaintenanceResource,
                                                                     maintenanceCostPerTick,
                                                                     ref slotEnergy,
                                                                     playerEntity,
                                                                     ref healthLookup,
                                                                     ref updatedHealth,
                                                                     ref healthChanged,
                                                                     ref shieldLookup,
                                                                     ref updatedShield,
                                                                     ref shieldChanged);
            maintenanceTickTimer -= tickIntervalSeconds;
        }
    }
    #endregion

    #endregion
}
