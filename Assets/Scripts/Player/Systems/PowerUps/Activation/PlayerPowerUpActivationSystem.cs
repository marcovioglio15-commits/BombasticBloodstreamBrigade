using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Handles active-tool button presses, charge workflows and emits runtime actions/requests.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpRechargeSystem))]
[UpdateAfter(typeof(PlayerMovementDirectionSystem))]
[UpdateAfter(typeof(PlayerLookDirectionSystem))]
[UpdateBefore(typeof(PlayerMovementSpeedSystem))]
[UpdateBefore(typeof(PlayerDashMovementSystem))]
public partial struct PlayerPowerUpActivationSystem : ISystem
{
    #region Constants
    private const float InputPressThreshold = 0.5f;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers every player state, request buffer and shared queue required by active power-up dispatch.
    /// </summary>
    /// <param name="state">Current ECS system state used to register update requirements.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpsConfigElement>();
        state.RequireForUpdate<PlayerPowerUpsState>();
        state.RequireForUpdate<PlayerChargeCharacterTuningState>();
        state.RequireForUpdate<PlayerChargeCharacterTuningBaseStatElement>();
        state.RequireForUpdate<PlayerProjectileSizePowerUpMultiplierElement>();
        state.RequireForUpdate<PlayerPowerUpUnlockCatalogElement>();
        state.RequireForUpdate<PlayerPowerUpCharacterTuningFormulaElement>();
        state.RequireForUpdate<PlayerInputState>();
        state.RequireForUpdate<PlayerLookState>();
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<PlayerBaseMovementConfig>();
        state.RequireForUpdate<PlayerRuntimeMovementConfig>();
        state.RequireForUpdate<PlayerBaseLookConfig>();
        state.RequireForUpdate<PlayerRuntimeLookConfig>();
        state.RequireForUpdate<PlayerBaseCameraConfig>();
        state.RequireForUpdate<PlayerRuntimeCameraConfig>();
        state.RequireForUpdate<PlayerBaseShootingConfig>();
        state.RequireForUpdate<PlayerRuntimeShootingConfig>();
        state.RequireForUpdate<PlayerBaseShootingAppliedElementSlot>();
        state.RequireForUpdate<PlayerRuntimeShootingAppliedElementSlot>();
        state.RequireForUpdate<PlayerBaseHealthStatisticsConfig>();
        state.RequireForUpdate<PlayerRuntimeHealthStatisticsConfig>();
        state.RequireForUpdate<PlayerBaseDeathAnimationConfig>();
        state.RequireForUpdate<PlayerDeathAnimationConfig>();
        state.RequireForUpdate<PlayerProgressionConfig>();
        state.RequireForUpdate<PlayerRuntimeScalingState>();
        state.RequireForUpdate<PlayerRuntimeControllerScalingElement>();
        state.RequireForUpdate<PlayerRuntimeDeathAnimationScalingElement>();
        state.RequireForUpdate<PlayerRuntimeProgressionScalingElement>();
        state.RequireForUpdate<PlayerBaseGamePhaseElement>();
        state.RequireForUpdate<PlayerRuntimeGamePhaseElement>();
        state.RequireForUpdate<PlayerBaseComboCounterConfig>();
        state.RequireForUpdate<PlayerRuntimeComboCounterConfig>();
        state.RequireForUpdate<PlayerBaseComboRankElement>();
        state.RequireForUpdate<PlayerRuntimeComboRankElement>();
        state.RequireForUpdate<PlayerBaseComboPassiveUnlockElement>();
        state.RequireForUpdate<PlayerRuntimeComboPassiveUnlockElement>();
        state.RequireForUpdate<PlayerRuntimeComboCounterScalingElement>();
        state.RequireForUpdate<PlayerComboCounterState>();
        state.RequireForUpdate<PlayerPowerUpBaseConfigElement>();
        state.RequireForUpdate<PlayerRuntimePowerUpScalingElement>();
        state.RequireForUpdate<EquippedPassiveToolElement>();
        state.RequireForUpdate<PlayerExperience>();
        state.RequireForUpdate<PlayerLevel>();
        state.RequireForUpdate<PlayerExperienceCollection>();
        state.RequireForUpdate<PlayerScalableStatElement>();
        state.RequireForUpdate<PlayerHealth>();
        state.RequireForUpdate<PlayerShield>();
        state.RequireForUpdate<LocalTransform>();
        state.RequireForUpdate<PlayerBombSpawnRequest>();
        state.RequireForUpdate<PlayerOrbitalProjectionSpawnRequest>();
        state.RequireForUpdate<ShootRequest>();
        state.RequireForUpdate<PlayerBulletTimeState>();
        state.RequireForUpdate<PlayerImpactFrameState>();
        state.RequireForUpdate<PlayerGhostTrailState>();
        state.RequireForUpdate<PlayerHealOverTimeState>();
        state.RequireForUpdate<PlayerPassiveToolsStateElement>();
        state.RequireForUpdate<PlayerLaserBeamState>();
        state.RequireForUpdate<EnemyDropCollectionRequestQueue>();
    }

    /// <summary>
    /// Processes both active slots and routes successful Drop Attraction activations into the shared collection queue.
    /// </summary>
    /// <param name="state">Current ECS system state providing player queries and mutable runtime lookups.</param>
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();
        BufferLookup<PlayerPowerUpsConfigElement> powerUpsConfigLookup = SystemAPI.GetBufferLookup<PlayerPowerUpsConfigElement>(false);
        ComponentLookup<PlayerHealth> healthLookup = SystemAPI.GetComponentLookup<PlayerHealth>(false);
        ComponentLookup<PlayerShield> shieldLookup = SystemAPI.GetComponentLookup<PlayerShield>(false);
        ComponentLookup<PlayerLookState> lookLookup = SystemAPI.GetComponentLookup<PlayerLookState>(true);
        ComponentLookup<PlayerMovementState> movementLookup = SystemAPI.GetComponentLookup<PlayerMovementState>(true);
        ComponentLookup<PlayerBaseMovementConfig> baseMovementLookup = SystemAPI.GetComponentLookup<PlayerBaseMovementConfig>(true);
        ComponentLookup<PlayerRuntimeMovementConfig> runtimeMovementLookup = SystemAPI.GetComponentLookup<PlayerRuntimeMovementConfig>(false);
        ComponentLookup<PlayerBaseLookConfig> baseLookLookup = SystemAPI.GetComponentLookup<PlayerBaseLookConfig>(true);
        ComponentLookup<PlayerRuntimeLookConfig> runtimeLookLookup = SystemAPI.GetComponentLookup<PlayerRuntimeLookConfig>(false);
        ComponentLookup<PlayerBaseCameraConfig> baseCameraLookup = SystemAPI.GetComponentLookup<PlayerBaseCameraConfig>(true);
        ComponentLookup<PlayerRuntimeCameraConfig> runtimeCameraLookup = SystemAPI.GetComponentLookup<PlayerRuntimeCameraConfig>(false);
        ComponentLookup<PlayerBaseShootingConfig> baseShootingLookup = SystemAPI.GetComponentLookup<PlayerBaseShootingConfig>(true);
        ComponentLookup<PlayerRuntimeShootingConfig> runtimeShootingLookup = SystemAPI.GetComponentLookup<PlayerRuntimeShootingConfig>(false);
        BufferLookup<PlayerBaseShootingAppliedElementSlot> baseAppliedElementSlotsLookup = SystemAPI.GetBufferLookup<PlayerBaseShootingAppliedElementSlot>(true);
        BufferLookup<PlayerRuntimeShootingAppliedElementSlot> runtimeAppliedElementSlotsLookup = SystemAPI.GetBufferLookup<PlayerRuntimeShootingAppliedElementSlot>(false);
        ComponentLookup<PlayerBaseHealthStatisticsConfig> baseHealthLookup = SystemAPI.GetComponentLookup<PlayerBaseHealthStatisticsConfig>(true);
        ComponentLookup<PlayerRuntimeHealthStatisticsConfig> runtimeHealthLookup = SystemAPI.GetComponentLookup<PlayerRuntimeHealthStatisticsConfig>(false);
        ComponentLookup<PlayerBaseDeathAnimationConfig> baseDeathAnimationLookup = SystemAPI.GetComponentLookup<PlayerBaseDeathAnimationConfig>(true);
        ComponentLookup<PlayerDeathAnimationConfig> runtimeDeathAnimationLookup = SystemAPI.GetComponentLookup<PlayerDeathAnimationConfig>(false);
        BufferLookup<PlayerPassiveToolsStateElement> passiveToolsLookup = SystemAPI.GetBufferLookup<PlayerPassiveToolsStateElement>(false);
        ComponentLookup<ShooterMuzzleAnchor> muzzleLookup = SystemAPI.GetComponentLookup<ShooterMuzzleAnchor>(true);
        ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<LocalToWorld> localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        ComponentLookup<PlayerBulletTimeState> bulletTimeLookup = SystemAPI.GetComponentLookup<PlayerBulletTimeState>(false);
        ComponentLookup<PlayerImpactFrameState> impactFrameLookup = SystemAPI.GetComponentLookup<PlayerImpactFrameState>(false);
        ComponentLookup<PlayerGhostTrailState> ghostTrailLookup = SystemAPI.GetComponentLookup<PlayerGhostTrailState>(false);
        ComponentLookup<PlayerHealOverTimeState> healOverTimeLookup = SystemAPI.GetComponentLookup<PlayerHealOverTimeState>(false);
        ComponentLookup<PlayerChargeCharacterTuningState> chargeCharacterTuningStateLookup = SystemAPI.GetComponentLookup<PlayerChargeCharacterTuningState>(false);
        ComponentLookup<PlayerProgressionConfig> progressionConfigLookup = SystemAPI.GetComponentLookup<PlayerProgressionConfig>(true);
        ComponentLookup<PlayerExperience> playerExperienceLookup = SystemAPI.GetComponentLookup<PlayerExperience>(false);
        ComponentLookup<PlayerLevel> playerLevelLookup = SystemAPI.GetComponentLookup<PlayerLevel>(false);
        ComponentLookup<PlayerExperienceCollection> playerExperienceCollectionLookup = SystemAPI.GetComponentLookup<PlayerExperienceCollection>(false);
        ComponentLookup<PlayerRuntimeScalingState> runtimeScalingStateLookup = SystemAPI.GetComponentLookup<PlayerRuntimeScalingState>(false);
        BufferLookup<PlayerChargeCharacterTuningBaseStatElement> chargeCharacterTuningBaseStatsLookup = SystemAPI.GetBufferLookup<PlayerChargeCharacterTuningBaseStatElement>(false);
        BufferLookup<PlayerProjectileSizePowerUpMultiplierElement> projectileSizePowerUpMultipliersLookup = SystemAPI.GetBufferLookup<PlayerProjectileSizePowerUpMultiplierElement>(false);
        BufferLookup<PlayerRuntimeControllerScalingElement> controllerScalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeControllerScalingElement>(true);
        BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup = SystemAPI.GetBufferLookup<EquippedPassiveToolElement>(false);
        BufferLookup<PlayerPowerUpUnlockCatalogElement> unlockCatalogLookup = SystemAPI.GetBufferLookup<PlayerPowerUpUnlockCatalogElement>(false);
        BufferLookup<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulaLookup = SystemAPI.GetBufferLookup<PlayerPowerUpCharacterTuningFormulaElement>(true);
        BufferLookup<PlayerScalableStatElement> scalableStatsLookup = SystemAPI.GetBufferLookup<PlayerScalableStatElement>(false);
        BufferLookup<PlayerRoomRewardTemporaryModifierElement> temporaryModifiersLookup =
            SystemAPI.GetBufferLookup<PlayerRoomRewardTemporaryModifierElement>(true);
        ComponentLookup<PlayerRoomRewardTemporaryState> temporaryStateLookup =
            SystemAPI.GetComponentLookup<PlayerRoomRewardTemporaryState>(true);
        BufferLookup<PlayerRuntimeProgressionScalingElement> progressionScalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeProgressionScalingElement>(true);
        BufferLookup<PlayerRuntimeDeathAnimationScalingElement> deathAnimationScalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeDeathAnimationScalingElement>(true);
        BufferLookup<PlayerBaseGamePhaseElement> baseGamePhasesLookup = SystemAPI.GetBufferLookup<PlayerBaseGamePhaseElement>(true);
        BufferLookup<PlayerRuntimeGamePhaseElement> runtimeGamePhasesLookup = SystemAPI.GetBufferLookup<PlayerRuntimeGamePhaseElement>(false);
        ComponentLookup<PlayerBaseComboCounterConfig> baseComboConfigLookup = SystemAPI.GetComponentLookup<PlayerBaseComboCounterConfig>(true);
        ComponentLookup<PlayerRuntimeComboCounterConfig> runtimeComboConfigLookup = SystemAPI.GetComponentLookup<PlayerRuntimeComboCounterConfig>(false);
        BufferLookup<PlayerBaseComboRankElement> baseComboRanksLookup = SystemAPI.GetBufferLookup<PlayerBaseComboRankElement>(true);
        BufferLookup<PlayerRuntimeComboRankElement> runtimeComboRanksLookup = SystemAPI.GetBufferLookup<PlayerRuntimeComboRankElement>(false);
        BufferLookup<PlayerBaseComboPassiveUnlockElement> baseComboPassiveUnlocksLookup = SystemAPI.GetBufferLookup<PlayerBaseComboPassiveUnlockElement>(true);
        BufferLookup<PlayerRuntimeComboPassiveUnlockElement> runtimeComboPassiveUnlocksLookup = SystemAPI.GetBufferLookup<PlayerRuntimeComboPassiveUnlockElement>(false);
        BufferLookup<PlayerRuntimeComboCounterScalingElement> comboScalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeComboCounterScalingElement>(true);
        ComponentLookup<PlayerComboCounterState> comboCounterStateLookup = SystemAPI.GetComponentLookup<PlayerComboCounterState>(false);
        BufferLookup<PlayerPowerUpBaseConfigElement> basePowerUpConfigsLookup = SystemAPI.GetBufferLookup<PlayerPowerUpBaseConfigElement>(false);
        BufferLookup<PlayerRuntimePowerUpScalingElement> powerUpScalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimePowerUpScalingElement>(true);
        BufferLookup<PlayerOrbitalProjectionSpawnRequest> orbitalProjectionRequestsLookup = SystemAPI.GetBufferLookup<PlayerOrbitalProjectionSpawnRequest>(false);
        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);
        DynamicBuffer<EnemyDropCollectionRequest> dropCollectionRequests =
            SystemAPI.GetSingletonBuffer<EnemyDropCollectionRequest>();

        foreach ((RefRO<PlayerInputState> inputState,
                  DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer,
                  RefRW<PlayerPowerUpsState> powerUpsState,
                  RefRW<PlayerDashState> dashState,
                  RefRW<PlayerLaserBeamState> laserBeamState,
                  DynamicBuffer<PlayerBombSpawnRequest> bombRequests,
                  DynamicBuffer<ShootRequest> shootRequests,
                  Entity entity)
                 in SystemAPI.Query<RefRO<PlayerInputState>,
                                    DynamicBuffer<PlayerPowerUpsConfigElement>,
                                    RefRW<PlayerPowerUpsState>,
                                    RefRW<PlayerDashState>,
                                    RefRW<PlayerLaserBeamState>,
                                    DynamicBuffer<PlayerBombSpawnRequest>,
                                    DynamicBuffer<ShootRequest>>().WithEntityAccess())
        {
            if (!lookLookup.HasComponent(entity))
                continue;

            if (!movementLookup.HasComponent(entity))
                continue;

            if (!runtimeMovementLookup.HasComponent(entity))
                continue;

            if (!runtimeShootingLookup.HasComponent(entity))
                continue;

            if (!passiveToolsLookup.HasBuffer(entity))
                continue;

            if (!transformLookup.HasComponent(entity))
                continue;

            if (!bulletTimeLookup.HasComponent(entity))
                continue;

            if (!impactFrameLookup.HasComponent(entity))
                continue;

            if (!ghostTrailLookup.HasComponent(entity))
                continue;

            if (!healOverTimeLookup.HasComponent(entity))
                continue;

            if (!chargeCharacterTuningStateLookup.HasComponent(entity))
                continue;

            if (!progressionConfigLookup.HasComponent(entity))
                continue;

            if (!playerExperienceLookup.HasComponent(entity))
                continue;

            if (!playerLevelLookup.HasComponent(entity))
                continue;

            if (!playerExperienceCollectionLookup.HasComponent(entity))
                continue;

            if (!chargeCharacterTuningBaseStatsLookup.HasBuffer(entity))
                continue;

            if (!projectileSizePowerUpMultipliersLookup.HasBuffer(entity))
                continue;

            if (!unlockCatalogLookup.HasBuffer(entity))
                continue;

            if (!characterTuningFormulaLookup.HasBuffer(entity))
                continue;

            if (!scalableStatsLookup.HasBuffer(entity))
                continue;

            if (!runtimeGamePhasesLookup.HasBuffer(entity))
                continue;

            if (!orbitalProjectionRequestsLookup.HasBuffer(entity))
                continue;

            PlayerLookState lookState = lookLookup[entity];
            PlayerMovementState movementState = movementLookup[entity];
            PlayerPowerUpsConfig powerUpsConfig;
            PlayerPowerUpsConfigBufferUtility.Read(powerUpsConfigBuffer,
                                                   out powerUpsConfig);
            PlayerRuntimeMovementConfig runtimeMovementConfig = runtimeMovementLookup[entity];
            PlayerRuntimeShootingConfig runtimeShootingConfig = runtimeShootingLookup[entity];
            DynamicBuffer<PlayerPassiveToolsStateElement> passiveToolsStateBuffer = passiveToolsLookup[entity];
            ref PlayerPassiveToolsState passiveToolsState = ref PlayerPassiveToolsStateBufferUtility.GetStateRef(passiveToolsStateBuffer);
            LocalTransform localTransform = transformLookup[entity];
            PlayerBulletTimeState bulletTimeState = bulletTimeLookup[entity];
            PlayerImpactFrameState impactFrameState = impactFrameLookup[entity];
            PlayerGhostTrailState ghostTrailState = ghostTrailLookup[entity];
            PlayerHealOverTimeState healOverTimeState = healOverTimeLookup[entity];
            PlayerChargeCharacterTuningState chargeCharacterTuningState = chargeCharacterTuningStateLookup[entity];
            PlayerProgressionConfig progressionConfig = progressionConfigLookup[entity];
            PlayerExperience playerExperience = playerExperienceLookup[entity];
            PlayerLevel playerLevel = playerLevelLookup[entity];
            PlayerExperienceCollection playerExperienceCollection = playerExperienceCollectionLookup[entity];
            DynamicBuffer<PlayerChargeCharacterTuningBaseStatElement> chargeCharacterTuningBaseStats = chargeCharacterTuningBaseStatsLookup[entity];
            DynamicBuffer<PlayerProjectileSizePowerUpMultiplierElement> projectileSizePowerUpMultipliers = projectileSizePowerUpMultipliersLookup[entity];
            DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> runtimeAppliedElementSlots = runtimeAppliedElementSlotsLookup[entity];
            DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog = unlockCatalogLookup[entity];
            DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas = characterTuningFormulaLookup[entity];
            DynamicBuffer<PlayerScalableStatElement> scalableStats = scalableStatsLookup[entity];
            DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases = runtimeGamePhasesLookup[entity];
            DynamicBuffer<PlayerOrbitalProjectionSpawnRequest> orbitalProjectionRequests = orbitalProjectionRequestsLookup[entity];
            PlayerLaserBeamState mutableLaserBeamState = laserBeamState.ValueRO;
            bool primaryPressed = inputState.ValueRO.PowerUpPrimary > InputPressThreshold;
            bool secondaryPressed = inputState.ValueRO.PowerUpSecondary > InputPressThreshold;
            bool primaryPressedThisFrame = primaryPressed && powerUpsState.ValueRO.PreviousPrimaryPressed == 0;
            bool secondaryPressedThisFrame = secondaryPressed && powerUpsState.ValueRO.PreviousSecondaryPressed == 0;
            bool primaryReleasedThisFrame = !primaryPressed && powerUpsState.ValueRO.PreviousPrimaryPressed != 0;
            bool secondaryReleasedThisFrame = !secondaryPressed && powerUpsState.ValueRO.PreviousSecondaryPressed != 0;
            float3 desiredDirection = movementState.DesiredDirection;

            if (math.lengthsq(desiredDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
                powerUpsState.ValueRW.LastValidMovementDirection = math.normalizesafe(desiredDirection, new float3(0f, 0f, 1f));

            powerUpsState.ValueRW.PreviousPrimaryPressed = primaryPressed ? (byte)1 : (byte)0;
            powerUpsState.ValueRW.PreviousSecondaryPressed = secondaryPressed ? (byte)1 : (byte)0;

            PlayerPowerUpSlotConfig primarySlotConfig = powerUpsConfig.PrimarySlot;
            PlayerPowerUpSlotConfig secondarySlotConfig = powerUpsConfig.SecondarySlot;
            float primaryEnergy = powerUpsState.ValueRO.PrimaryEnergy;
            float secondaryEnergy = powerUpsState.ValueRO.SecondaryEnergy;
            float primaryCooldownRemaining = powerUpsState.ValueRO.PrimaryCooldownRemaining;
            float secondaryCooldownRemaining = powerUpsState.ValueRO.SecondaryCooldownRemaining;
            float primaryCharge = powerUpsState.ValueRO.PrimaryCharge;
            float secondaryCharge = powerUpsState.ValueRO.SecondaryCharge;
            float primaryMaintenanceTickTimer = powerUpsState.ValueRO.PrimaryMaintenanceTickTimer;
            float secondaryMaintenanceTickTimer = powerUpsState.ValueRO.SecondaryMaintenanceTickTimer;
            int primaryReturningProjectileRecallReadyCount = powerUpsState.ValueRO.PrimaryReturningProjectileRecallReadyCount;
            int secondaryReturningProjectileRecallReadyCount = powerUpsState.ValueRO.SecondaryReturningProjectileRecallReadyCount;
            uint primaryReturningProjectileRecallVersion = powerUpsState.ValueRO.PrimaryReturningProjectileRecallVersion;
            uint secondaryReturningProjectileRecallVersion = powerUpsState.ValueRO.SecondaryReturningProjectileRecallVersion;
            byte primaryIsCharging = powerUpsState.ValueRO.PrimaryIsCharging;
            byte secondaryIsCharging = powerUpsState.ValueRO.SecondaryIsCharging;
            byte primaryIsActive = powerUpsState.ValueRO.PrimaryIsActive;
            byte secondaryIsActive = powerUpsState.ValueRO.SecondaryIsActive;
            byte isShootingSuppressed = 0;
            bool healthChanged = false;
            PlayerHealth updatedHealth = default;
            bool shieldChanged = false;
            PlayerShield updatedShield = default;
            bool primaryScopedCharacterTuningShouldBeActiveBeforePrimary = ShouldScopedCharacterTuningBeActiveBeforeSlotProcessing(in primarySlotConfig,
                                                                                                                                     primaryPressedThisFrame,
                                                                                                                                     primaryReleasedThisFrame,
                                                                                                                                     primaryIsCharging,
                                                                                                                                     primaryIsActive,
                                                                                                                                     primaryCooldownRemaining);
            bool secondaryScopedCharacterTuningShouldBeActiveBeforePrimary = ShouldScopedCharacterTuningRemainActiveOutsideCurrentSlot(in secondarySlotConfig,
                                                                                                                                       secondaryIsActive);

            bool scopedCharacterTuningChangedBeforePrimary = PlayerPowerUpChargeCharacterTuningRuntimeUtility.ReconcileScopedCharacterTuning(in primarySlotConfig,
                                                                                                                                               in secondarySlotConfig,
                                                                                                                                               primaryScopedCharacterTuningShouldBeActiveBeforePrimary,
                                                                                                                                               secondaryScopedCharacterTuningShouldBeActiveBeforePrimary,
                                                                                                                                               unlockCatalog,
                                                                                                                                               characterTuningFormulas,
                                                                                                                                               scalableStats,
                                                                                                                                               progressionConfig,
                                                                                                                                               runtimeGamePhases,
                                                                                                                                               ref chargeCharacterTuningState,
                                                                                                                                               chargeCharacterTuningBaseStats,
                                                                                                                                               projectileSizePowerUpMultipliers,
                                                                                                                                               ref passiveToolsState,
                                                                                                                                               ref playerExperience,
                                                                                                                                               ref playerLevel,
                                                                                                                                               ref playerExperienceCollection);

            if (scopedCharacterTuningChangedBeforePrimary)
            {
                RefreshRuntimeScaledState(entity,
                                          scalableStatsLookup,
                                          temporaryModifiersLookup,
                                          temporaryStateLookup,
                                          controllerScalingLookup,
                                          baseMovementLookup,
                                          runtimeMovementLookup,
                                          baseLookLookup,
                                          runtimeLookLookup,
                                          baseCameraLookup,
                                          runtimeCameraLookup,
                                          baseShootingLookup,
                                          runtimeShootingLookup,
                                          baseAppliedElementSlotsLookup,
                                          runtimeAppliedElementSlotsLookup,
                                          baseHealthLookup,
                                          runtimeHealthLookup,
                                          baseDeathAnimationLookup,
                                          runtimeDeathAnimationLookup,
                                          deathAnimationScalingLookup,
                                          progressionScalingLookup,
                                          baseGamePhasesLookup,
                                          runtimeGamePhasesLookup,
                                          baseComboConfigLookup,
                                          runtimeComboConfigLookup,
                                          baseComboRanksLookup,
                                          runtimeComboRanksLookup,
                                          baseComboPassiveUnlocksLookup,
                                          runtimeComboPassiveUnlocksLookup,
                                          comboScalingLookup,
                                          comboCounterStateLookup,
                                          characterTuningFormulaLookup,
                                          basePowerUpConfigsLookup,
                                          powerUpScalingLookup,
                                          powerUpsConfigLookup,
                                          unlockCatalogLookup,
                                          equippedPassiveToolsLookup,
                                          passiveToolsLookup,
                                          healthLookup,
                                          shieldLookup,
                                          progressionConfigLookup,
                                          playerExperienceLookup,
                                          playerLevelLookup,
                                          playerExperienceCollectionLookup,
                                          runtimeScalingStateLookup,
                                          ref primarySlotConfig,
                                          ref secondarySlotConfig,
                                          ref runtimeMovementConfig,
                                          ref runtimeShootingConfig,
                                          ref passiveToolsState,
                                          ref playerExperience,
                                          ref playerLevel,
                                          ref playerExperienceCollection);
            }

            PlayerPowerUpActivationSlotUtility.ProcessSlotInput(in primarySlotConfig,
                                                                in secondarySlotConfig,
                                                                0,
                                                                powerUpsState.ValueRO.PrimaryReturningProjectileCount,
                                                                ref primaryReturningProjectileRecallReadyCount,
                                                                ref primaryReturningProjectileRecallVersion,
                                                                primaryPressed,
                                                                primaryPressedThisFrame,
                                                                primaryReleasedThisFrame,
                                                                deltaTime,
                                                                in localTransform,
                                                       in lookState,
                                                       in movementState,
                                                       in runtimeMovementConfig,
                                                                in runtimeShootingConfig,
                                                                runtimeAppliedElementSlots,
                                                                in passiveToolsState,
                                                                in muzzleLookup,
                                                                in transformLookup,
                                                                in localToWorldLookup,
                                                                inputState.ValueRO.Move,
                                                                powerUpsState.ValueRO.LastValidMovementDirection,
                                                                ref mutableLaserBeamState,
                                                                ref primaryEnergy,
                                                                ref primaryCooldownRemaining,
                                                                ref primaryCharge,
                                                                ref primaryIsCharging,
                                                                ref primaryIsActive,
                                                                ref primaryMaintenanceTickTimer,
                                                                ref secondaryCharge,
                                                                ref secondaryCooldownRemaining,
                                                                ref secondaryIsCharging,
                                                                ref secondaryIsActive,
                                                                ref secondaryMaintenanceTickTimer,
                                                                ref isShootingSuppressed,
                                                                ref dashState.ValueRW,
                                                                ref bulletTimeState,
                                                                ref impactFrameState,
                                                                ref ghostTrailState,
                                                                ref healOverTimeState,
                                                                bombRequests,
                                                                orbitalProjectionRequests,
                                                                shootRequests,
                                                                dropCollectionRequests,
                                                                audioRequests,
                                                                canEnqueueAudioRequests,
                                                                entity,
                                                                ref healthLookup,
                                                                ref updatedHealth,
                                                                ref healthChanged,
                                                                ref shieldLookup,
                                                                ref updatedShield,
                                                                ref shieldChanged);

            bool primaryScopedCharacterTuningShouldBeActiveBeforeSecondary = ShouldScopedCharacterTuningRemainActiveOutsideCurrentSlot(in primarySlotConfig,
                                                                                                                                       primaryIsActive);
            bool secondaryScopedCharacterTuningShouldBeActiveBeforeSecondary = ShouldScopedCharacterTuningBeActiveBeforeSlotProcessing(in secondarySlotConfig,
                                                                                                                                         secondaryPressedThisFrame,
                                                                                                                                         secondaryReleasedThisFrame,
                                                                                                                                         secondaryIsCharging,
                                                                                                                                         secondaryIsActive,
                                                                                                                                         secondaryCooldownRemaining);

            bool scopedCharacterTuningChangedBeforeSecondary = PlayerPowerUpChargeCharacterTuningRuntimeUtility.ReconcileScopedCharacterTuning(in primarySlotConfig,
                                                                                                                                                 in secondarySlotConfig,
                                                                                                                                                 primaryScopedCharacterTuningShouldBeActiveBeforeSecondary,
                                                                                                                                                 secondaryScopedCharacterTuningShouldBeActiveBeforeSecondary,
                                                                                                                                                 unlockCatalog,
                                                                                                                                                 characterTuningFormulas,
                                                                                                                                                 scalableStats,
                                                                                                                                                 progressionConfig,
                                                                                                                                                 runtimeGamePhases,
                                                                                                                                                 ref chargeCharacterTuningState,
                                                                                                                                                 chargeCharacterTuningBaseStats,
                                                                                                                                                 projectileSizePowerUpMultipliers,
                                                                                                                                                 ref passiveToolsState,
                                                                                                                                                 ref playerExperience,
                                                                                                                                                 ref playerLevel,
                                                                                                                                                 ref playerExperienceCollection);

            if (scopedCharacterTuningChangedBeforeSecondary)
            {
                RefreshRuntimeScaledState(entity,
                                          scalableStatsLookup,
                                          temporaryModifiersLookup,
                                          temporaryStateLookup,
                                          controllerScalingLookup,
                                          baseMovementLookup,
                                          runtimeMovementLookup,
                                          baseLookLookup,
                                          runtimeLookLookup,
                                          baseCameraLookup,
                                          runtimeCameraLookup,
                                          baseShootingLookup,
                                          runtimeShootingLookup,
                                          baseAppliedElementSlotsLookup,
                                          runtimeAppliedElementSlotsLookup,
                                          baseHealthLookup,
                                          runtimeHealthLookup,
                                          baseDeathAnimationLookup,
                                          runtimeDeathAnimationLookup,
                                          deathAnimationScalingLookup,
                                          progressionScalingLookup,
                                          baseGamePhasesLookup,
                                          runtimeGamePhasesLookup,
                                          baseComboConfigLookup,
                                          runtimeComboConfigLookup,
                                          baseComboRanksLookup,
                                          runtimeComboRanksLookup,
                                          baseComboPassiveUnlocksLookup,
                                          runtimeComboPassiveUnlocksLookup,
                                          comboScalingLookup,
                                          comboCounterStateLookup,
                                          characterTuningFormulaLookup,
                                          basePowerUpConfigsLookup,
                                          powerUpScalingLookup,
                                          powerUpsConfigLookup,
                                          unlockCatalogLookup,
                                          equippedPassiveToolsLookup,
                                          passiveToolsLookup,
                                          healthLookup,
                                          shieldLookup,
                                          progressionConfigLookup,
                                          playerExperienceLookup,
                                          playerLevelLookup,
                                          playerExperienceCollectionLookup,
                                          runtimeScalingStateLookup,
                                          ref primarySlotConfig,
                                          ref secondarySlotConfig,
                                          ref runtimeMovementConfig,
                                          ref runtimeShootingConfig,
                                          ref passiveToolsState,
                                          ref playerExperience,
                                          ref playerLevel,
                                          ref playerExperienceCollection);
            }

            PlayerPowerUpActivationSlotUtility.ProcessSlotInput(in secondarySlotConfig,
                                                                in primarySlotConfig,
                                                                1,
                                                                powerUpsState.ValueRO.SecondaryReturningProjectileCount,
                                                                ref secondaryReturningProjectileRecallReadyCount,
                                                                ref secondaryReturningProjectileRecallVersion,
                                                                secondaryPressed,
                                                                secondaryPressedThisFrame,
                                                                secondaryReleasedThisFrame,
                                                                deltaTime,
                                                                in localTransform,
                                                       in lookState,
                                                       in movementState,
                                                       in runtimeMovementConfig,
                                                                in runtimeShootingConfig,
                                                                runtimeAppliedElementSlots,
                                                                in passiveToolsState,
                                                                in muzzleLookup,
                                                                in transformLookup,
                                                                in localToWorldLookup,
                                                                inputState.ValueRO.Move,
                                                                powerUpsState.ValueRO.LastValidMovementDirection,
                                                                ref mutableLaserBeamState,
                                                                ref secondaryEnergy,
                                                                ref secondaryCooldownRemaining,
                                                                ref secondaryCharge,
                                                                ref secondaryIsCharging,
                                                                ref secondaryIsActive,
                                                                ref secondaryMaintenanceTickTimer,
                                                                ref primaryCharge,
                                                                ref primaryCooldownRemaining,
                                                                ref primaryIsCharging,
                                                                ref primaryIsActive,
                                                                ref primaryMaintenanceTickTimer,
                                                                ref isShootingSuppressed,
                                                                ref dashState.ValueRW,
                                                                ref bulletTimeState,
                                                                ref impactFrameState,
                                                                ref ghostTrailState,
                                                                ref healOverTimeState,
                                                                bombRequests,
                                                                orbitalProjectionRequests,
                                                                shootRequests,
                                                                dropCollectionRequests,
                                                                audioRequests,
                                                                canEnqueueAudioRequests,
                                                                entity,
                                                                ref healthLookup,
                                                                ref updatedHealth,
                                                                ref healthChanged,
                                                                ref shieldLookup,
                                                                ref updatedShield,
                                                                ref shieldChanged);

            bool primaryScopedCharacterTuningShouldBeActiveFinal = ShouldScopedCharacterTuningRemainActive(in primarySlotConfig,
                                                                                                              primaryIsCharging,
                                                                                                              primaryIsActive);
            bool secondaryScopedCharacterTuningShouldBeActiveFinal = ShouldScopedCharacterTuningRemainActive(in secondarySlotConfig,
                                                                                                                secondaryIsCharging,
                                                                                                                secondaryIsActive);

            bool scopedCharacterTuningChangedFinal = PlayerPowerUpChargeCharacterTuningRuntimeUtility.ReconcileScopedCharacterTuning(in primarySlotConfig,
                                                                                                                                       in secondarySlotConfig,
                                                                                                                                       primaryScopedCharacterTuningShouldBeActiveFinal,
                                                                                                                                       secondaryScopedCharacterTuningShouldBeActiveFinal,
                                                                                                                                       unlockCatalog,
                                                                                                                                       characterTuningFormulas,
                                                                                                                                       scalableStats,
                                                                                                                                       progressionConfig,
                                                                                                                                       runtimeGamePhases,
                                                                                                                                       ref chargeCharacterTuningState,
                                                                                                                                       chargeCharacterTuningBaseStats,
                                                                                                                                       projectileSizePowerUpMultipliers,
                                                                                                                                       ref passiveToolsState,
                                                                                                                                       ref playerExperience,
                                                                                                                                       ref playerLevel,
                                                                                                                                       ref playerExperienceCollection);

            if (scopedCharacterTuningChangedFinal)
            {
                RefreshRuntimeScaledState(entity,
                                          scalableStatsLookup,
                                          temporaryModifiersLookup,
                                          temporaryStateLookup,
                                          controllerScalingLookup,
                                          baseMovementLookup,
                                          runtimeMovementLookup,
                                          baseLookLookup,
                                          runtimeLookLookup,
                                          baseCameraLookup,
                                          runtimeCameraLookup,
                                          baseShootingLookup,
                                          runtimeShootingLookup,
                                          baseAppliedElementSlotsLookup,
                                          runtimeAppliedElementSlotsLookup,
                                          baseHealthLookup,
                                          runtimeHealthLookup,
                                          baseDeathAnimationLookup,
                                          runtimeDeathAnimationLookup,
                                          deathAnimationScalingLookup,
                                          progressionScalingLookup,
                                          baseGamePhasesLookup,
                                          runtimeGamePhasesLookup,
                                          baseComboConfigLookup,
                                          runtimeComboConfigLookup,
                                          baseComboRanksLookup,
                                          runtimeComboRanksLookup,
                                          baseComboPassiveUnlocksLookup,
                                          runtimeComboPassiveUnlocksLookup,
                                          comboScalingLookup,
                                          comboCounterStateLookup,
                                          characterTuningFormulaLookup,
                                          basePowerUpConfigsLookup,
                                          powerUpScalingLookup,
                                          powerUpsConfigLookup,
                                          unlockCatalogLookup,
                                          equippedPassiveToolsLookup,
                                          passiveToolsLookup,
                                          healthLookup,
                                          shieldLookup,
                                          progressionConfigLookup,
                                          playerExperienceLookup,
                                          playerLevelLookup,
                                          playerExperienceCollectionLookup,
                                          runtimeScalingStateLookup,
                                          ref primarySlotConfig,
                                          ref secondarySlotConfig,
                                          ref runtimeMovementConfig,
                                          ref runtimeShootingConfig,
                                          ref passiveToolsState,
                                          ref playerExperience,
                                          ref playerLevel,
                                          ref playerExperienceCollection);
            }

            if (healthChanged)
                healthLookup[entity] = updatedHealth;

            if (shieldChanged)
                shieldLookup[entity] = updatedShield;

            powerUpsState.ValueRW.PrimaryEnergy = primaryEnergy;
            powerUpsState.ValueRW.SecondaryEnergy = secondaryEnergy;
            powerUpsState.ValueRW.PrimaryCooldownRemaining = primaryCooldownRemaining;
            powerUpsState.ValueRW.SecondaryCooldownRemaining = secondaryCooldownRemaining;
            powerUpsState.ValueRW.PrimaryCharge = primaryCharge;
            powerUpsState.ValueRW.SecondaryCharge = secondaryCharge;
            powerUpsState.ValueRW.PrimaryMaintenanceTickTimer = primaryMaintenanceTickTimer;
            powerUpsState.ValueRW.SecondaryMaintenanceTickTimer = secondaryMaintenanceTickTimer;
            powerUpsState.ValueRW.PrimaryReturningProjectileRecallReadyCount = primaryReturningProjectileRecallReadyCount;
            powerUpsState.ValueRW.SecondaryReturningProjectileRecallReadyCount = secondaryReturningProjectileRecallReadyCount;
            powerUpsState.ValueRW.PrimaryReturningProjectileRecallVersion = primaryReturningProjectileRecallVersion;
            powerUpsState.ValueRW.SecondaryReturningProjectileRecallVersion = secondaryReturningProjectileRecallVersion;
            powerUpsState.ValueRW.PrimaryIsCharging = primaryIsCharging;
            powerUpsState.ValueRW.SecondaryIsCharging = secondaryIsCharging;
            powerUpsState.ValueRW.PrimaryIsActive = primaryIsActive;
            powerUpsState.ValueRW.SecondaryIsActive = secondaryIsActive;
            powerUpsState.ValueRW.IsShootingSuppressed = isShootingSuppressed;
            laserBeamState.ValueRW = mutableLaserBeamState;
            bulletTimeLookup[entity] = bulletTimeState;
            impactFrameLookup[entity] = impactFrameState;
            ghostTrailLookup[entity] = ghostTrailState;
            healOverTimeLookup[entity] = healOverTimeState;
            chargeCharacterTuningStateLookup[entity] = chargeCharacterTuningState;
            playerExperienceLookup[entity] = playerExperience;
            playerLevelLookup[entity] = playerLevel;
            playerExperienceCollectionLookup[entity] = playerExperienceCollection;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves whether one runtime-scoped Character Tuning overlay must already be active before the current slot starts processing this frame.
    /// </summary>
    /// <param name="slotConfig">Slot config inspected for temporary Character Tuning semantics.</param>
    /// <param name="pressedThisFrame">True when the slot input was freshly pressed during the current frame.</param>
    /// <param name="releasedThisFrame">True when the slot input was released during the current frame.</param>
    /// <param name="isCharging">Current charging flag before slot processing mutates it.</param>
    /// <param name="isActive">Current toggle-active flag before slot processing mutates it.</param>
    /// <param name="cooldownRemaining">Current cooldown or startup-lock value before slot processing mutates it.</param>
    /// <returns>True when the temporary Character Tuning overlay should be active while the current slot is processed.</returns>
    private static bool ShouldScopedCharacterTuningBeActiveBeforeSlotProcessing(in PlayerPowerUpSlotConfig slotConfig,
                                                                                bool pressedThisFrame,
                                                                                bool releasedThisFrame,
                                                                                byte isCharging,
                                                                                byte isActive,
                                                                                float cooldownRemaining)
    {
        if (slotConfig.IsDefined == 0)
            return false;

        if (slotConfig.ToolKind == ActiveToolKind.ChargeShot)
        {
            if (isCharging != 0)
                return true;

            if (!pressedThisFrame)
                return false;

            if (cooldownRemaining > 0f)
                return false;

            if (slotConfig.ChargeShot.RequiredCharge <= 0f)
                return false;

            return slotConfig.ChargeShot.MaximumCharge > 0f;
        }

        if (PlayerPowerUpCharacterTuningRuntimeUtility.IsActiveTriggerScopedCharacterTuning(in slotConfig))
        {
            if (cooldownRemaining > 0f)
                return false;

            switch (slotConfig.ActivationInputMode)
            {
                case PowerUpActivationInputMode.OnRelease:
                    return releasedThisFrame;
                default:
                    return pressedThisFrame;
            }
        }

        if (slotConfig.Toggleable == 0)
            return false;

        return isActive != 0;
    }

    /// <summary>
    /// Rebuilds runtime-scaled configs immediately after scalable-stat changes triggered inside the activation flow and refreshes cached local copies.
    /// </summary>
    /// <param name="entity">Player entity being refreshed.</param>
    /// <param name="scalableStatsLookup">Runtime scalable-stat buffer lookup.</param>
    /// <param name="temporaryModifiersLookup">Room-scoped scalable-stat modifier lookup.</param>
    /// <param name="temporaryStateLookup">Versioned room-visit state lookup.</param>
    /// <param name="controllerScalingLookup">Controller scaling metadata lookup.</param>
    /// <param name="baseMovementLookup">Immutable movement baseline lookup.</param>
    /// <param name="runtimeMovementLookup">Mutable runtime movement config lookup.</param>
    /// <param name="baseLookLookup">Immutable look baseline lookup.</param>
    /// <param name="runtimeLookLookup">Mutable runtime look config lookup.</param>
    /// <param name="baseCameraLookup">Immutable camera baseline lookup.</param>
    /// <param name="runtimeCameraLookup">Mutable runtime camera config lookup.</param>
    /// <param name="baseShootingLookup">Immutable shooting baseline lookup.</param>
    /// <param name="runtimeShootingLookup">Mutable runtime shooting config lookup.</param>
    /// <param name="baseHealthLookup">Immutable health baseline lookup.</param>
    /// <param name="runtimeHealthLookup">Mutable runtime health config lookup.</param>
    /// <param name="baseDeathAnimationLookup">Immutable death-animation baseline lookup.</param>
    /// <param name="runtimeDeathAnimationLookup">Mutable runtime death-animation config lookup.</param>
    /// <param name="deathAnimationScalingLookup">Death-animation visual scaling metadata lookup.</param>
    /// <param name="progressionScalingLookup">Progression scaling metadata lookup.</param>
    /// <param name="baseGamePhasesLookup">Immutable runtime-phase baseline lookup.</param>
    /// <param name="runtimeGamePhasesLookup">Mutable runtime-phase buffer lookup.</param>
    /// <param name="basePowerUpConfigsLookup">Immutable modular power-up baseline lookup.</param>
    /// <param name="powerUpScalingLookup">Runtime power-up scaling metadata lookup.</param>
    /// <param name="powerUpsConfigLookup">Mutable external power-up slot config snapshot lookup.</param>
    /// <param name="unlockCatalogLookup">Mutable unlock catalog lookup.</param>
    /// <param name="equippedPassiveToolsLookup">Mutable equipped-passive buffer lookup.</param>
    /// <param name="passiveToolsLookup">Mutable passive aggregate lookup.</param>
    /// <param name="healthLookup">Mutable health lookup.</param>
    /// <param name="shieldLookup">Mutable shield lookup.</param>
    /// <param name="progressionConfigLookup">Runtime progression config lookup.</param>
    /// <param name="experienceLookup">Mutable experience lookup.</param>
    /// <param name="levelLookup">Mutable level lookup.</param>
    /// <param name="experienceCollectionLookup">Mutable experience-collection lookup.</param>
    /// <param name="runtimeScalingStateLookup">Mutable runtime-scaling sync state lookup.</param>
    /// <param name="primarySlotConfig">Cached primary slot config refreshed from runtime state.</param>
    /// <param name="secondarySlotConfig">Cached secondary slot config refreshed from runtime state.</param>
    /// <param name="runtimeMovementConfig">Cached runtime movement config refreshed from runtime state.</param>
    /// <param name="runtimeShootingConfig">Cached runtime shooting config refreshed from runtime state.</param>
    /// <param name="passiveToolsState">Cached passive aggregate refreshed from runtime state.</param>
    /// <param name="playerExperience">Cached experience component refreshed from runtime state.</param>
    /// <param name="playerLevel">Cached level component refreshed from runtime state.</param>
    /// <param name="playerExperienceCollection">Cached experience-collection component refreshed from runtime state.</param>
    private static void RefreshRuntimeScaledState(Entity entity,
                                                  BufferLookup<PlayerScalableStatElement> scalableStatsLookup,
                                                  BufferLookup<PlayerRoomRewardTemporaryModifierElement> temporaryModifiersLookup,
                                                  ComponentLookup<PlayerRoomRewardTemporaryState> temporaryStateLookup,
                                                  BufferLookup<PlayerRuntimeControllerScalingElement> controllerScalingLookup,
                                                  ComponentLookup<PlayerBaseMovementConfig> baseMovementLookup,
                                                  ComponentLookup<PlayerRuntimeMovementConfig> runtimeMovementLookup,
                                                  ComponentLookup<PlayerBaseLookConfig> baseLookLookup,
                                                  ComponentLookup<PlayerRuntimeLookConfig> runtimeLookLookup,
                                                  ComponentLookup<PlayerBaseCameraConfig> baseCameraLookup,
                                                  ComponentLookup<PlayerRuntimeCameraConfig> runtimeCameraLookup,
                                                  ComponentLookup<PlayerBaseShootingConfig> baseShootingLookup,
                                                  ComponentLookup<PlayerRuntimeShootingConfig> runtimeShootingLookup,
                                                  BufferLookup<PlayerBaseShootingAppliedElementSlot> baseAppliedElementSlotsLookup,
                                                  BufferLookup<PlayerRuntimeShootingAppliedElementSlot> runtimeAppliedElementSlotsLookup,
                                                  ComponentLookup<PlayerBaseHealthStatisticsConfig> baseHealthLookup,
                                                  ComponentLookup<PlayerRuntimeHealthStatisticsConfig> runtimeHealthLookup,
                                                  ComponentLookup<PlayerBaseDeathAnimationConfig> baseDeathAnimationLookup,
                                                  ComponentLookup<PlayerDeathAnimationConfig> runtimeDeathAnimationLookup,
                                                  BufferLookup<PlayerRuntimeDeathAnimationScalingElement> deathAnimationScalingLookup,
                                                  BufferLookup<PlayerRuntimeProgressionScalingElement> progressionScalingLookup,
                                                  BufferLookup<PlayerBaseGamePhaseElement> baseGamePhasesLookup,
                                                  BufferLookup<PlayerRuntimeGamePhaseElement> runtimeGamePhasesLookup,
                                                  ComponentLookup<PlayerBaseComboCounterConfig> baseComboConfigLookup,
                                                  ComponentLookup<PlayerRuntimeComboCounterConfig> runtimeComboConfigLookup,
                                                  BufferLookup<PlayerBaseComboRankElement> baseComboRanksLookup,
                                                  BufferLookup<PlayerRuntimeComboRankElement> runtimeComboRanksLookup,
                                                  BufferLookup<PlayerBaseComboPassiveUnlockElement> baseComboPassiveUnlocksLookup,
                                                  BufferLookup<PlayerRuntimeComboPassiveUnlockElement> runtimeComboPassiveUnlocksLookup,
                                                  BufferLookup<PlayerRuntimeComboCounterScalingElement> comboScalingLookup,
                                                  ComponentLookup<PlayerComboCounterState> comboCounterStateLookup,
                                                  BufferLookup<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulaLookup,
                                                  BufferLookup<PlayerPowerUpBaseConfigElement> basePowerUpConfigsLookup,
                                                  BufferLookup<PlayerRuntimePowerUpScalingElement> powerUpScalingLookup,
                                                  BufferLookup<PlayerPowerUpsConfigElement> powerUpsConfigLookup,
                                                  BufferLookup<PlayerPowerUpUnlockCatalogElement> unlockCatalogLookup,
                                                  BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup,
                                                  BufferLookup<PlayerPassiveToolsStateElement> passiveToolsLookup,
                                                  ComponentLookup<PlayerHealth> healthLookup,
                                                  ComponentLookup<PlayerShield> shieldLookup,
                                                  ComponentLookup<PlayerProgressionConfig> progressionConfigLookup,
                                                  ComponentLookup<PlayerExperience> experienceLookup,
                                                  ComponentLookup<PlayerLevel> levelLookup,
                                                  ComponentLookup<PlayerExperienceCollection> experienceCollectionLookup,
                                                  ComponentLookup<PlayerRuntimeScalingState> runtimeScalingStateLookup,
                                                  ref PlayerPowerUpSlotConfig primarySlotConfig,
                                                  ref PlayerPowerUpSlotConfig secondarySlotConfig,
                                                  ref PlayerRuntimeMovementConfig runtimeMovementConfig,
                                                  ref PlayerRuntimeShootingConfig runtimeShootingConfig,
                                                  ref PlayerPassiveToolsState passiveToolsState,
                                                  ref PlayerExperience playerExperience,
                                                  ref PlayerLevel playerLevel,
                                                  ref PlayerExperienceCollection playerExperienceCollection)
    {
        PlayerRuntimeScalingRefreshUtility.TryApplyForEntity(entity,
                                                             scalableStatsLookup,
                                                             temporaryModifiersLookup,
                                                             temporaryStateLookup,
                                                             controllerScalingLookup,
                                                             baseMovementLookup,
                                                             runtimeMovementLookup,
                                                             baseLookLookup,
                                                             runtimeLookLookup,
                                                             baseCameraLookup,
                                                             runtimeCameraLookup,
                                                             baseShootingLookup,
                                                             runtimeShootingLookup,
                                                             baseAppliedElementSlotsLookup,
                                                             runtimeAppliedElementSlotsLookup,
                                                             baseHealthLookup,
                                                             runtimeHealthLookup,
                                                             baseDeathAnimationLookup,
                                                             runtimeDeathAnimationLookup,
                                                             deathAnimationScalingLookup,
                                                             progressionScalingLookup,
                                                             baseGamePhasesLookup,
                                                             runtimeGamePhasesLookup,
                                                             baseComboConfigLookup,
                                                             runtimeComboConfigLookup,
                                                             baseComboRanksLookup,
                                                             runtimeComboRanksLookup,
                                                             baseComboPassiveUnlocksLookup,
                                                             runtimeComboPassiveUnlocksLookup,
                                                             comboScalingLookup,
                                                             comboCounterStateLookup,
                                                             characterTuningFormulaLookup,
                                                             basePowerUpConfigsLookup,
                                                             powerUpScalingLookup,
                                                             powerUpsConfigLookup,
                                                             unlockCatalogLookup,
                                                             equippedPassiveToolsLookup,
                                                             passiveToolsLookup,
                                                             healthLookup,
                                                             shieldLookup,
                                                             progressionConfigLookup,
                                                             experienceLookup,
                                                             levelLookup,
                                                             experienceCollectionLookup,
                                                             runtimeScalingStateLookup,
                                                             false);

        if (!powerUpsConfigLookup.HasBuffer(entity))
            return;

        PlayerPowerUpsConfig powerUpsConfig;
        PlayerPowerUpsConfigBufferUtility.Read(entity,
                                               in powerUpsConfigLookup,
                                               out powerUpsConfig);
        primarySlotConfig = powerUpsConfig.PrimarySlot;
        secondarySlotConfig = powerUpsConfig.SecondarySlot;

        if (runtimeMovementLookup.HasComponent(entity))
            runtimeMovementConfig = runtimeMovementLookup[entity];

        if (runtimeShootingLookup.HasComponent(entity))
            runtimeShootingConfig = runtimeShootingLookup[entity];

        if (experienceLookup.HasComponent(entity))
            playerExperience = experienceLookup[entity];

        if (levelLookup.HasComponent(entity))
            playerLevel = levelLookup[entity];

        if (experienceCollectionLookup.HasComponent(entity))
            playerExperienceCollection = experienceCollectionLookup[entity];
    }

    /// <summary>
    /// Resolves whether one runtime-scoped Character Tuning overlay must remain applied outside the slot currently being processed.
    /// </summary>
    /// <param name="slotConfig">Slot config inspected for temporary Character Tuning semantics.</param>
    /// <param name="isCharging">Current charging flag after the latest slot mutation.</param>
    /// <param name="isActive">Current toggle-active flag after the latest slot mutation.</param>
    /// <returns>True when the temporary Character Tuning overlay should remain applied.</returns>
    private static bool ShouldScopedCharacterTuningRemainActive(in PlayerPowerUpSlotConfig slotConfig,
                                                                byte isCharging,
                                                                byte isActive)
    {
        if (slotConfig.IsDefined == 0)
            return false;

        if (slotConfig.ToolKind == ActiveToolKind.ChargeShot)
            return isCharging != 0;

        if (slotConfig.Toggleable == 0)
            return false;

        return isActive != 0;
    }

    /// <summary>
    /// Resolves scoped tuning for the slot that is not currently executing so charge-shot projectile tuning cannot leak into another tool's shot.
    /// </summary>
    /// <param name="slotConfig">Opposite slot config inspected for scoped Character Tuning semantics.</param>
    /// <param name="isActive">Current toggle-active flag after the latest slot mutation.</param>
    /// <returns>True when non-charge scoped tuning should stay active while another slot processes.</returns>
    private static bool ShouldScopedCharacterTuningRemainActiveOutsideCurrentSlot(in PlayerPowerUpSlotConfig slotConfig,
                                                                                  byte isActive)
    {
        if (slotConfig.IsDefined == 0)
            return false;

        if (slotConfig.ToolKind == ActiveToolKind.ChargeShot)
            return false;

        if (slotConfig.Toggleable == 0)
            return false;

        return isActive != 0;
    }
    #endregion

    #endregion
}
