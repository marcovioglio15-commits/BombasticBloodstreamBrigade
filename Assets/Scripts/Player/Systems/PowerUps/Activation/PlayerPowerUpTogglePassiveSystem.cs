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
        state.RequireForUpdate<PlayerGhostTrailState>();
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
                  RefRW<PlayerGhostTrailState> ghostTrailState,
                  Entity playerEntity)
                 in SystemAPI.Query<DynamicBuffer<PlayerPowerUpsConfigElement>,
                                    RefRW<PlayerPowerUpsState>,
                                    DynamicBuffer<EquippedPassiveToolElement>,
                                    DynamicBuffer<PlayerPassiveToolsStateElement>,
                                    RefRW<PlayerBulletTimeState>,
                                    RefRW<PlayerGhostTrailState>>()
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
            PowerUpConditionalApplicationRuntimeState primaryConditionalApplication = powerUpsState.ValueRO.PrimaryConditionalApplication;
            PowerUpConditionalApplicationRuntimeState secondaryConditionalApplication = powerUpsState.ValueRO.SecondaryConditionalApplication;
            float toggleBulletTimeSlowPercent = 0f;
            float toggleBulletTimePlayerProjectileSlowPercent = 0f;
            float toggleBulletTimeTransitionTimeSeconds = 0f;

            ProcessTogglePassiveSlot(in powerUpsConfig.PrimarySlot,
                                     0,
                                     deltaTime,
                                     playerEntity,
                                     ref primaryEnergy,
                                     ref primaryCooldownRemaining,
                                     ref primaryIsActive,
                                     ref primaryMaintenanceTickTimer,
                                     ref primaryConditionalApplication,
                                     ref aggregatedPassiveToolsState,
                                     ref isShootingSuppressed,
                                     ref healthLookup,
                                     ref updatedHealth,
                                     ref healthChanged,
                                     ref shieldLookup,
                                     ref updatedShield,
                                     ref shieldChanged,
                                     ref toggleBulletTimeSlowPercent,
                                     ref toggleBulletTimePlayerProjectileSlowPercent,
                                     ref toggleBulletTimeTransitionTimeSeconds,
                                     ref ghostTrailState.ValueRW);
            ProcessTogglePassiveSlot(in powerUpsConfig.SecondarySlot,
                                     1,
                                     deltaTime,
                                     playerEntity,
                                     ref secondaryEnergy,
                                     ref secondaryCooldownRemaining,
                                     ref secondaryIsActive,
                                     ref secondaryMaintenanceTickTimer,
                                     ref secondaryConditionalApplication,
                                     ref aggregatedPassiveToolsState,
                                     ref isShootingSuppressed,
                                     ref healthLookup,
                                     ref updatedHealth,
                                     ref healthChanged,
                                     ref shieldLookup,
                                     ref updatedShield,
                                     ref shieldChanged,
                                     ref toggleBulletTimeSlowPercent,
                                     ref toggleBulletTimePlayerProjectileSlowPercent,
                                     ref toggleBulletTimeTransitionTimeSeconds,
                                     ref ghostTrailState.ValueRW);
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
            powerUpsState.ValueRW.PrimaryConditionalApplication = primaryConditionalApplication;
            powerUpsState.ValueRW.SecondaryConditionalApplication = secondaryConditionalApplication;
            powerUpsState.ValueRW.IsShootingSuppressed = isShootingSuppressed;

            if (toggleBulletTimeSlowPercent <= 0f && currentBulletTimeState.ToggleSlowPercent > 0f)
                toggleBulletTimeTransitionTimeSeconds = math.max(toggleBulletTimeTransitionTimeSeconds,
                                                                 currentBulletTimeState.ToggleTransitionTimeSeconds);

            if (toggleBulletTimePlayerProjectileSlowPercent <= 0f &&
                currentBulletTimeState.TogglePlayerProjectileSlowPercent > 0f)
                toggleBulletTimeTransitionTimeSeconds = math.max(toggleBulletTimeTransitionTimeSeconds,
                                                                 currentBulletTimeState.ToggleTransitionTimeSeconds);

            currentBulletTimeState.ToggleSlowPercent = toggleBulletTimeSlowPercent;
            currentBulletTimeState.TogglePlayerProjectileSlowPercent = toggleBulletTimePlayerProjectileSlowPercent;
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
    /// <param name="slotIndex">Stable primary or secondary slot index used by slot-bound toggle effects.</param>
    /// <param name="deltaTime">Current frame delta time.</param>
    /// <param name="playerEntity">Player entity used for health and shield resource access.</param>
    /// <param name="slotEnergy">Mutable slot energy state.</param>
    /// <param name="cooldownRemaining">Mutable slot timer used as toggle startup lock while active.</param>
    /// <param name="isActive">Mutable toggle-active flag for the slot.</param>
    /// <param name="maintenanceTickTimer">Mutable accumulated maintenance timer.</param>
    /// <param name="conditionalApplicationState">Mutable slot state retaining finite toggle lifetime alongside optional shot-condition state.</param>
    /// <param name="passiveToolsState">Aggregated passive state updated with the slot payload when active.</param>
    /// <param name="isShootingSuppressed">Mutable shared shooting suppression flag for the current player frame.</param>
    /// <param name="healthLookup">Health lookup used for non-energy maintenance costs.</param>
    /// <param name="updatedHealth">Cached mutable health value reused within the current caller.</param>
    /// <param name="healthChanged">True when updatedHealth already contains a fetched runtime value.</param>
    /// <param name="shieldLookup">Shield lookup used for shield maintenance costs.</param>
    /// <param name="updatedShield">Cached mutable shield value reused within the current caller.</param>
    /// <param name="shieldChanged">True when updatedShield already contains a fetched runtime value.</param>
    /// <param name="toggleBulletTimeSlowPercent">Mutable maximum enemy slow percentage contributed by active toggle slots.</param>
    /// <param name="toggleBulletTimePlayerProjectileSlowPercent">Mutable maximum player projectile slow percentage contributed by active toggle slots.</param>
    /// <param name="toggleBulletTimeTransitionTimeSeconds">Mutable transition duration matching the strongest active bullet-time toggle.</param>
    /// <param name="ghostTrailState">Mutable shared Ghost Trail runtime state updated when this slot deactivates.</param>
    private static void ProcessTogglePassiveSlot(in PlayerPowerUpSlotConfig slotConfig,
                                                 byte slotIndex,
                                                 float deltaTime,
                                                 Entity playerEntity,
                                                 ref float slotEnergy,
                                                 ref float cooldownRemaining,
                                                 ref byte isActive,
                                                 ref float maintenanceTickTimer,
                                                 ref PowerUpConditionalApplicationRuntimeState conditionalApplicationState,
                                                 ref PlayerPassiveToolsState passiveToolsState,
                                                 ref byte isShootingSuppressed,
                                                 ref ComponentLookup<PlayerHealth> healthLookup,
                                                 ref PlayerHealth updatedHealth,
                                                 ref bool healthChanged,
                                                 ref ComponentLookup<PlayerShield> shieldLookup,
                                                 ref PlayerShield updatedShield,
                                                 ref bool shieldChanged,
                                                 ref float toggleBulletTimeSlowPercent,
                                                 ref float toggleBulletTimePlayerProjectileSlowPercent,
                                                 ref float toggleBulletTimeTransitionTimeSeconds,
                                                 ref PlayerGhostTrailState ghostTrailState)
    {
        bool wasActive = isActive != 0;

        if (slotConfig.IsDefined == 0)
        {
            isActive = 0;
            maintenanceTickTimer = 0f;
            conditionalApplicationState.ToggleActiveElapsedSeconds = 0f;
            StopMatchedGhostTrailIfDeactivated(in slotConfig, slotIndex, wasActive, isActive, ref ghostTrailState);
            return;
        }

        if (slotConfig.ToolKind != ActiveToolKind.PassiveToggle || slotConfig.Toggleable == 0)
        {
            isActive = 0;
            maintenanceTickTimer = 0f;
            conditionalApplicationState.ToggleActiveElapsedSeconds = 0f;
            StopMatchedGhostTrailIfDeactivated(in slotConfig, slotIndex, wasActive, isActive, ref ghostTrailState);
            return;
        }

        if (isActive == 0)
        {
            maintenanceTickTimer = 0f;
            conditionalApplicationState.ToggleActiveElapsedSeconds = 0f;
            return;
        }

        if (PlayerPowerUpToggleLifetimeUtility.Tick(slotConfig.MaximumToggleActiveDurationSeconds,
                                                    deltaTime,
                                                    ref conditionalApplicationState))
        {
            isActive = 0;
            maintenanceTickTimer = 0f;
            conditionalApplicationState.ToggleActiveElapsedSeconds = 0f;
            StopMatchedGhostTrailIfDeactivated(in slotConfig, slotIndex, wasActive, isActive, ref ghostTrailState);
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

        StopMatchedGhostTrailIfDeactivated(in slotConfig, slotIndex, wasActive, isActive, ref ghostTrailState);

        if (isActive == 0)
        {
            conditionalApplicationState.ToggleActiveElapsedSeconds = 0f;
            return;
        }

        if (slotConfig.TogglePassiveTool.IsDefined == 0)
            return;

        PlayerPassiveToolConfig togglePassiveTool = slotConfig.TogglePassiveTool;

        if (togglePassiveTool.ConditionalApplication.Mode != PowerUpConditionalApplicationMode.None)
            return;

        if (togglePassiveTool.HasBulletTime != 0 &&
            (togglePassiveTool.BulletTime.EnemySlowPercent > 0f ||
             togglePassiveTool.BulletTime.PlayerProjectileSlowPercent > 0f))
        {
            float slowPercent = math.clamp(togglePassiveTool.BulletTime.EnemySlowPercent, 0f, 100f);
            float playerProjectileSlowPercent = math.clamp(togglePassiveTool.BulletTime.PlayerProjectileSlowPercent, 0f, 100f);
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

            if (playerProjectileSlowPercent > toggleBulletTimePlayerProjectileSlowPercent)
            {
                toggleBulletTimePlayerProjectileSlowPercent = playerProjectileSlowPercent;
                toggleBulletTimeTransitionTimeSeconds = transitionTimeSeconds;
            }
            else if (math.abs(playerProjectileSlowPercent - toggleBulletTimePlayerProjectileSlowPercent) <= 0.0001f)
            {
                toggleBulletTimeTransitionTimeSeconds = math.max(toggleBulletTimeTransitionTimeSeconds, transitionTimeSeconds);
            }

            togglePassiveTool.HasBulletTime = 0;
            togglePassiveTool.BulletTime = default;
        }

        PlayerPassiveToolsAggregationUtility.AccumulateActiveTogglePassiveTool(ref passiveToolsState,
                                                                                in togglePassiveTool,
                                                                                slotIndex);
    }

    /// <summary>
    /// Stops a matched Ghost Trail timeline when toggle maintenance deactivates its owning slot.
    /// </summary>
    /// <param name="slotConfig">Processed toggle slot configuration.</param>
    /// <param name="slotIndex">Stable primary or secondary slot index.</param>
    /// <param name="wasActive">True when the slot was active before maintenance processing.</param>
    /// <param name="isActive">Current slot active flag after maintenance processing.</param>
    /// <param name="ghostTrailState">Mutable shared Ghost Trail state.</param>
    private static void StopMatchedGhostTrailIfDeactivated(in PlayerPowerUpSlotConfig slotConfig,
                                                           byte slotIndex,
                                                           bool wasActive,
                                                           byte isActive,
                                                           ref PlayerGhostTrailState ghostTrailState)
    {
        if (!wasActive || isActive != 0 || slotConfig.HasGhostTrail == 0)
            return;

        PlayerGhostTrailRuntimeUtility.StopMatchedToggle(ref ghostTrailState, slotIndex);
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
