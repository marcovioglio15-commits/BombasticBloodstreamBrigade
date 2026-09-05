using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Enqueues player shots after movement and look rotation, using the current player heading and muzzle hierarchy.
/// Analog projectile direction stays aligned with the aiming pointer even while rotation is damped or released.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerLookRotationSystem))]
[UpdateAfter(typeof(PlayerMovementApplySystem))]
public partial struct PlayerShootingIntentSystem : ISystem
{
    #region Constants
    private const int MaxAutomaticShotsPerFrame = 4;
    #endregion

    #region Lifecycle
    /// <summary>
    /// Configures the system to require updates for player entities that have 
    /// the necessary components for processing shooting logic,
    /// as well as the ShootRequest buffer to ensure that the system only runs when 
    /// there are relevant entities to process.
    /// </summary>
    /// <param name="state"></param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerControllerConfig>();
        state.RequireForUpdate<PlayerInputState>();
        state.RequireForUpdate<PlayerLookState>();
        state.RequireForUpdate<PlayerShootingState>();
        state.RequireForUpdate<PlayerRuntimeShootingConfig>();
        state.RequireForUpdate<PlayerRuntimeShootingAppliedElementSlot>();
        state.RequireForUpdate<ShooterProjectilePrefab>();
        state.RequireForUpdate<ShootRequest>();
        state.RequireForUpdate<EquippedPassiveToolElement>();
        state.RequireForUpdate<PlayerPowerUpsConfigElement>();
        state.RequireForUpdate<PlayerBombSpawnRequest>();
        state.RequireForUpdate<PlayerLaserBeamState>();
        state.RequireForUpdate<PlayerPowerUpUnlockCatalogElement>();
        state.RequireForUpdate<PlayerPowerUpCharacterTuningFormulaElement>();
        state.RequireForUpdate<PlayerScalableStatElement>();
        state.RequireForUpdate<PlayerRuntimeControllerScalingElement>();
    }

    /// <summary>
    /// Processes player input and shooting state to enqueue shoot requests for each player entity based on their
    /// shooting configuration and current input.
    /// </summary>
    /// <param name="state">The current system state for the update.</param>
    public void OnUpdate(ref SystemState state)
    {
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        ComponentLookup<ShooterMuzzleAnchor> muzzleLookup = SystemAPI.GetComponentLookup<ShooterMuzzleAnchor>(true);
        ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<Parent> parentLookup = SystemAPI.GetComponentLookup<Parent>(true);
        ComponentLookup<PlayerPowerUpsState> powerUpsStateLookup = SystemAPI.GetComponentLookup<PlayerPowerUpsState>(false);
        BufferLookup<PlayerPassiveToolsStateElement> passiveToolsLookup = SystemAPI.GetBufferLookup<PlayerPassiveToolsStateElement>(true);
        BufferLookup<PlayerPowerUpsConfigElement> powerUpsConfigLookup = SystemAPI.GetBufferLookup<PlayerPowerUpsConfigElement>(true);
        BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup = SystemAPI.GetBufferLookup<EquippedPassiveToolElement>(false);
        BufferLookup<PlayerBombSpawnRequest> bombRequestsLookup = SystemAPI.GetBufferLookup<PlayerBombSpawnRequest>(false);
        BufferLookup<PlayerPowerUpUnlockCatalogElement> unlockCatalogLookup = SystemAPI.GetBufferLookup<PlayerPowerUpUnlockCatalogElement>(true);
        BufferLookup<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulasLookup = SystemAPI.GetBufferLookup<PlayerPowerUpCharacterTuningFormulaElement>(true);
        BufferLookup<PlayerScalableStatElement> scalableStatsLookup = SystemAPI.GetBufferLookup<PlayerScalableStatElement>(true);
        BufferLookup<PlayerRuntimeControllerScalingElement> controllerScalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeControllerScalingElement>(true);
        BufferLookup<PlayerRoomRewardTemporaryModifierElement> temporaryModifiersLookup = SystemAPI.GetBufferLookup<PlayerRoomRewardTemporaryModifierElement>(true);
        BufferLookup<PlayerRuntimeComboRankElement> runtimeComboRanksLookup = SystemAPI.GetBufferLookup<PlayerRuntimeComboRankElement>(true);
        ComponentLookup<PlayerRoomRewardTemporaryState> temporaryStateLookup = SystemAPI.GetComponentLookup<PlayerRoomRewardTemporaryState>(true);
        ComponentLookup<PlayerRuntimeComboCounterConfig> runtimeComboConfigLookup = SystemAPI.GetComponentLookup<PlayerRuntimeComboCounterConfig>(true);
        ComponentLookup<PlayerComboCounterState> comboStateLookup = SystemAPI.GetComponentLookup<PlayerComboCounterState>(true);
        ComponentLookup<PlayerLaserBeamState> laserBeamStateLookup = SystemAPI.GetComponentLookup<PlayerLaserBeamState>(false);
        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);

        // for each player,
        // determine if they should shoot based on their input and shooting mode,
        // and if so, enqueue shoot requests with the appropriate parameters for projectile spawning
        foreach ((RefRO<PlayerInputState> inputState,
                  RefRO<PlayerLookState> lookState,
                  RefRO<PlayerRuntimeShootingConfig> runtimeShootingConfig,
                  DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> appliedElementSlots,
                  RefRO<LocalTransform> localTransform,
                  RefRW<PlayerShootingState> shootingState,
                  DynamicBuffer<ShootRequest> shootRequests,
                  Entity entity) in SystemAPI.Query<RefRO<PlayerInputState>,
                                                   RefRO<PlayerLookState>,
                                                   RefRO<PlayerRuntimeShootingConfig>,
                                                   DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot>,
                                                   RefRO<LocalTransform>,
                                                   RefRW<PlayerShootingState>,
                                                   DynamicBuffer<ShootRequest>>().WithEntityAccess())
        {
            if (!powerUpsStateLookup.HasComponent(entity))
                continue;

            DynamicBuffer<ShootRequest> mutableShootRequests = shootRequests;
            PlayerPowerUpsState powerUpsState = powerUpsStateLookup[entity];

            // if shooting is disabled in the config, skip processing shooting logic for this player
            PlayerRuntimeShootingConfig shootingConfig = runtimeShootingConfig.ValueRO;
            ShootingValuesBlob values = shootingConfig.Values;
            PlayerPassiveToolsState passiveToolsState;
            ResolvePassiveToolsState(entity,
                                     in passiveToolsLookup,
                                     out passiveToolsState);
            bool isShootingSuppressed = powerUpsState.IsShootingSuppressed != 0;
            bool isShootPressed = inputState.ValueRO.Shoot > 0.5f;
            bool usesAutomaticLatch = shootingConfig.TriggerMode == ShootingTriggerMode.AutomaticToggle;

            if (!usesAutomaticLatch && shootingState.ValueRO.AutomaticEnabled != 0)
                shootingState.ValueRW.AutomaticEnabled = 0;

            if (isShootingSuppressed)
            {
                shootingState.ValueRW.PreviousShootPressed = isShootPressed ? (byte)1 : (byte)0;
                RefreshVisualShootingState(ref shootingState.ValueRW, false, elapsedTime);

                if (values.RateOfFire > 0f)
                    ResetShotSchedule(ref shootingState.ValueRW, elapsedTime);

                continue;
            }

            // if rate of fire or shoot speed is zero or negative, treat as shooting disabled and skip shooting logic
            if (values.RateOfFire <= 0f || values.ShootSpeed <= 0f)
            {
                shootingState.ValueRW.PreviousShootPressed = isShootPressed ? (byte)1 : (byte)0;
                RefreshVisualShootingState(ref shootingState.ValueRW, false, elapsedTime);
                ResetShotSchedule(ref shootingState.ValueRW, elapsedTime);
                continue;
            }

            // determine if the shoot button is currently pressed and if it was just pressed this frame
            bool shootPressedThisFrame = isShootPressed && shootingState.ValueRO.PreviousShootPressed == 0;
            shootingState.ValueRW.PreviousShootPressed = isShootPressed ? (byte)1 : (byte)0;

            if (passiveToolsState.HasLaserBeam != 0)
            {
                shootingState.ValueRW.AutomaticEnabled = 0;
                RefreshVisualShootingState(ref shootingState.ValueRW, isShootPressed, elapsedTime);
                ResetShotSchedule(ref shootingState.ValueRW, elapsedTime);
                continue;
            }

            bool automaticWasEnabled = usesAutomaticLatch && shootingState.ValueRO.AutomaticEnabled != 0;
            float shotInterval = 1f / values.RateOfFire;

            // Re-anchor manual continuous fire after idle time while preserving active cooldowns across rapid re-presses.
            if (shootingConfig.TriggerMode == ShootingTriggerMode.ManualContinousShot && shootPressedThisFrame)
                RefreshManualContinuousShotScheduleOnPress(ref shootingState.ValueRW, elapsedTime);

            // based on the shooting trigger mode, determine if the player should shoot this frame
            bool shouldShoot = ResolveShootingTrigger(ref shootingState.ValueRW,
                                                      shootingConfig.TriggerMode,
                                                      isShootPressed,
                                                      shootPressedThisFrame);
            bool automaticIsEnabled = usesAutomaticLatch && shootingState.ValueRW.AutomaticEnabled != 0;

            if (shootingConfig.TriggerMode == ShootingTriggerMode.AutomaticToggle)
            {
                bool automaticEnabledThisFrame = !automaticWasEnabled && automaticIsEnabled;
                bool automaticDisabledThisFrame = automaticWasEnabled && !automaticIsEnabled;

                if (automaticDisabledThisFrame)
                {
                    RefreshVisualShootingState(ref shootingState.ValueRW, false, elapsedTime);
                    shootingState.ValueRW.NextShotTime = elapsedTime + shotInterval;
                    continue;
                }

                if (automaticEnabledThisFrame)
                    ResetShotSchedule(ref shootingState.ValueRW, elapsedTime);
            }

            RefreshVisualShootingState(ref shootingState.ValueRW, false, elapsedTime);

            if (!shouldShoot)
                continue;

            // compute how many shots to fire this frame based on the elapsed time and the player's rate of fire,
            // ensuring don't exceed the maximum allowed shots per frame for automatic fire
            int shotsToFire = ComputeShotsToFire(ref shootingState.ValueRW, shootingConfig.TriggerMode, elapsedTime, shotInterval);

            if (shotsToFire <= 0)
                continue;

            if (!powerUpsConfigLookup.HasBuffer(entity) ||
                !equippedPassiveToolsLookup.HasBuffer(entity) ||
                !bombRequestsLookup.HasBuffer(entity) ||
                !unlockCatalogLookup.HasBuffer(entity) ||
                !characterTuningFormulasLookup.HasBuffer(entity) ||
                !scalableStatsLookup.HasBuffer(entity) ||
                !controllerScalingLookup.HasBuffer(entity) ||
                !laserBeamStateLookup.HasComponent(entity))
                continue;

            // Analog rotation can stop between allowed directions when the stick is released. Match the pointer's
            // actual heading, while keeping the existing target-based aiming behavior for pointer and digital input.
            float3 shootDirection = inputState.ValueRO.LookUsesAnalogSource != 0
                ? PlayerLaserBeamUtility.ResolveCurrentForwardDirection(in localTransform.ValueRO)
                : PlayerProjectileRequestUtility.ResolveShootDirection(in lookState.ValueRO, in localTransform.ValueRO);
            PlayerPowerUpsConfig powerUpsConfig;
            PlayerPowerUpsConfigBufferUtility.Read(powerUpsConfigLookup[entity], out powerUpsConfig);
            DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools = equippedPassiveToolsLookup[entity];
            DynamicBuffer<PlayerBombSpawnRequest> bombRequests = bombRequestsLookup[entity];
            DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog = unlockCatalogLookup[entity];
            DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas = characterTuningFormulasLookup[entity];
            DynamicBuffer<PlayerScalableStatElement> scalableStats = scalableStatsLookup[entity];
            DynamicBuffer<PlayerRuntimeControllerScalingElement> controllerScaling = controllerScalingLookup[entity];
            DynamicBuffer<PlayerRoomRewardTemporaryModifierElement> temporaryModifiers = temporaryModifiersLookup.HasBuffer(entity)
                ? temporaryModifiersLookup[entity]
                : default;
            DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks = runtimeComboRanksLookup.HasBuffer(entity)
                ? runtimeComboRanksLookup[entity]
                : default;
            PlayerRoomRewardTemporaryState temporaryState = temporaryStateLookup.HasComponent(entity)
                ? temporaryStateLookup[entity]
                : default;
            PlayerRuntimeComboCounterConfig runtimeComboConfig = runtimeComboConfigLookup.HasComponent(entity)
                ? runtimeComboConfigLookup[entity]
                : default;
            PlayerComboCounterState comboState = comboStateLookup.HasComponent(entity)
                ? comboStateLookup[entity]
                : default;
            PlayerConditionalCharacterTuningContext conditionalCharacterTuningContext = new PlayerConditionalCharacterTuningContext(unlockCatalog,
                                                                                                                                    characterTuningFormulas,
                                                                                                                                    scalableStats,
                                                                                                                                    controllerScaling,
                                                                                                                                    temporaryModifiers,
                                                                                                                                    in temporaryState,
                                                                                                                                    runtimeComboRanks,
                                                                                                                                    in runtimeComboConfig,
                                                                                                                                    in comboState);
            PlayerLaserBeamState laserBeamState = laserBeamStateLookup[entity];
            ElementalEffectConfig unusedElementalEffect = default;

            // enqueue the appropriate number of shoot requests with the resolved spawn position,
            // shoot direction, and shooting parameters from the config
            for (int shotIndex = 0; shotIndex < shotsToFire; shotIndex++)
            {
                PlayerPassiveToolsState shotPassiveToolsState = passiveToolsState;
                PlayerRuntimeShootingConfig shotShootingConfig = shootingConfig;
                bool conditionalShotContextInitialized = false;
                PlayerConditionalShotEffectRuntimeUtility.AccumulateQualifiedEffects(equippedPassiveTools,
                                                                                     in powerUpsConfig,
                                                                                     ref powerUpsState,
                                                                                     in localTransform.ValueRO,
                                                                                     in lookState.ValueRO,
                                                                                     in shootingConfig,
                                                                                     in conditionalCharacterTuningContext,
                                                                                     ref conditionalShotContextInitialized,
                                                                                     ref shotShootingConfig,
                                                                                     appliedElementSlots,
                                                                                     entity,
                                                                                     bombRequests,
                                                                                     ref laserBeamState,
                                                                                     ref shotPassiveToolsState);
                // The controller runs before TransformSystemGroup, so child LocalToWorld still holds the old pose.
                // Share the beam's hierarchy composition to apply this frame's movement and rotation to the muzzle.
                float3 spawnPosition = PlayerLaserBeamUtility.ResolveCurrentFrameSpawnPosition(entity,
                                                                                              in localTransform.ValueRO,
                                                                                              in shotShootingConfig,
                                                                                              in muzzleLookup,
                                                                                              in transformLookup,
                                                                                              in parentLookup);
                bool hasPassiveShotgunPayload = shotPassiveToolsState.HasShotgun != 0;
                int passiveShotgunProjectileCount = hasPassiveShotgunPayload
                    ? math.max(1, shotPassiveToolsState.Shotgun.ProjectileCount)
                    : 1;
                float passiveShotgunConeAngle = hasPassiveShotgunPayload
                    ? math.max(0f, shotPassiveToolsState.Shotgun.ConeAngleDegrees)
                    : 0f;
                PlayerProjectileRequestUtility.ResolvePenetrationSettings(in shotShootingConfig.Values,
                                                                          hasPassiveShotgunPayload
                                                                              ? shotPassiveToolsState.Shotgun.PenetrationMode
                                                                              : ProjectilePenetrationMode.None,
                                                                          hasPassiveShotgunPayload
                                                                              ? shotPassiveToolsState.Shotgun.MaxPenetrations
                                                                              : 0,
                                                                          out ProjectilePenetrationMode penetrationMode,
                                                                          out int maxPenetrations);
                PlayerProjectileRequestTemplate projectileTemplate = PlayerProjectileRequestUtility.BuildProjectileTemplate(in shotShootingConfig,
                                                                                                                             appliedElementSlots,
                                                                                                                             in shotPassiveToolsState,
                                                                                                                             1f,
                                                                                                                             1f,
                                                                                                                             1f,
                                                                                                                             1f,
                                                                                                                             1f,
                                                                                                                             false,
                                                                                                                             in unusedElementalEffect,
                                                                                                                             0f);
                ProjectileShotModifierConfig shotModifiers = PlayerProjectileRequestUtility.BuildShotModifierConfig(in shotPassiveToolsState);
                PlayerProjectileRequestUtility.AddSpreadRequests(ref mutableShootRequests,
                                                                 passiveShotgunProjectileCount,
                                                                 passiveShotgunConeAngle,
                                                                 spawnPosition,
                                                                 shootDirection,
                                                                 in projectileTemplate,
                                                                 penetrationMode,
                                                                 maxPenetrations,
                                                                 0,
                                                                 ProjectileSpawnSource.BaseShot,
                                                                 shotPassiveToolsState.HasReturningProjectilesActiveSlotOwner != 0
                                                                     ? shotPassiveToolsState.ReturningProjectilesActiveSlotIndex
                                                                     : byte.MaxValue,
                                                                 shotPassiveToolsState.HasReturningProjectiles,
                                                                 shotPassiveToolsState.ReturningProjectiles,
                                                                 shotModifiers);

                if (canEnqueueAudioRequests)
                    GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.PlayerShootProjectile, spawnPosition);
            }

            powerUpsStateLookup[entity] = powerUpsState;
            laserBeamStateLookup[entity] = laserBeamState;
        }
    }

    #endregion


    #region Helpers
    /// <summary>
    /// This method determines whether the player should shoot 
    /// based on their shooting trigger mode and current input state.
    /// </summary>
    /// <param name="shootingState"></param>
    /// <param name="triggerMode"></param>
    /// <param name="isShootPressed"></param>
    /// <param name="shootPressedThisFrame"></param>
    /// <returns></returns>
    private static bool ResolveShootingTrigger(ref PlayerShootingState shootingState,
                                               ShootingTriggerMode triggerMode,
                                               bool isShootPressed,
                                               bool shootPressedThisFrame)
    {
        switch (triggerMode)
        {
            case ShootingTriggerMode.AutomaticToggle:
                if (shootPressedThisFrame)
                    shootingState.AutomaticEnabled = shootingState.AutomaticEnabled == 0 ? (byte)1 : (byte)0;

                return shootingState.AutomaticEnabled != 0;
            case ShootingTriggerMode.ManualSingleShot:
                return shootPressedThisFrame;
            case ShootingTriggerMode.ManualContinousShot:
                shootingState.AutomaticEnabled = 0;
                return isShootPressed;
            default:
                shootingState.AutomaticEnabled = 0;
                return false;
        }
    }

    /// <summary>
    /// This method computes how many shots the player should fire 
    /// in the current frame based on their shooting state,
    /// and the elapsed time since the last shot, ensuring that the number of shots fired 
    /// does not exceed the maximum allowed for automatic fire.
    /// </summary>
    /// <param name="shootingState"></param>
    /// <param name="triggerMode"></param>
    /// <param name="elapsedTime"></param>
    /// <param name="shotInterval"></param>
    /// <returns></returns>
    private static int ComputeShotsToFire(ref PlayerShootingState shootingState, ShootingTriggerMode triggerMode, float elapsedTime, float shotInterval)
    {
        float nextShotTime = shootingState.NextShotTime;

        if (nextShotTime <= 0f)
            nextShotTime = elapsedTime;

        int shotsToFire = 0;

        switch (triggerMode)
        {
            case ShootingTriggerMode.ManualContinousShot:
            case ShootingTriggerMode.AutomaticToggle:
                if (elapsedTime < nextShotTime)
                    break;

                float lag = elapsedTime - nextShotTime;
                shotsToFire = 1 + (int)math.floor(lag / shotInterval);
                shotsToFire = math.clamp(shotsToFire, 1, MaxAutomaticShotsPerFrame);
                nextShotTime += shotInterval * shotsToFire;
                break;
            case ShootingTriggerMode.ManualSingleShot:
                if (elapsedTime < nextShotTime)
                    break;

                shotsToFire = 1;
                nextShotTime = elapsedTime + shotInterval;
                break;
            default:
                shotsToFire = 0;
                nextShotTime = elapsedTime + shotInterval;
                break;
        }

        shootingState.NextShotTime = nextShotTime;
        return shotsToFire;
    }

    /// <summary>
    /// Resets the next-shot schedule to the current frame so idle or temporarily disabled fire does not accumulate
    /// deferred automatic shots.
    /// </summary>
    /// <param name="shootingState">Mutable firing state that stores the next scheduled shot time.</param>
    /// <param name="elapsedTime">Current world elapsed time used as the new schedule anchor.</param>
    private static void ResetShotSchedule(ref PlayerShootingState shootingState,
                                          float elapsedTime)
    {
        shootingState.NextShotTime = elapsedTime;
    }

    /// <summary>
    /// Prevents released manual continuous fire from accumulating catch-up shots while keeping a future cooldown intact
    /// when the player repeatedly presses faster than the configured fire rate.
    /// </summary>
    /// <param name="shootingState">Mutable player shooting state that stores the next eligible shot time.</param>
    /// <param name="elapsedTime">Current world elapsed time used to clear stale idle schedules.</param>
    private static void RefreshManualContinuousShotScheduleOnPress(ref PlayerShootingState shootingState,
                                                                   float elapsedTime)
    {
        if (shootingState.NextShotTime >= elapsedTime)
            return;

        shootingState.NextShotTime = elapsedTime;
    }

    /// <summary>
    /// Refreshes the animator-facing shooting flag from input intent and short-lived projectile spawn pulses.
    /// </summary>
    /// <param name="shootingState">Mutable player shooting state that stores visual pulse timing.</param>
    /// <param name="inputRequestsShootingVisual">True when current input or automatic latch should keep shooting visuals active.</param>
    /// <param name="elapsedTime">Current world elapsed time used to expire short projectile pulses.</param>
    private static void RefreshVisualShootingState(ref PlayerShootingState shootingState,
                                                   bool inputRequestsShootingVisual,
                                                   float elapsedTime)
    {
        bool pulseStillActive = shootingState.VisualShootingUntilTime > elapsedTime;

        if (!pulseStillActive)
            shootingState.VisualShootingUntilTime = 0f;

        shootingState.VisualShootingActive = inputRequestsShootingVisual || pulseStillActive ? (byte)1 : (byte)0;
    }

    private static void ResolvePassiveToolsState(Entity shooterEntity,
                                                 in BufferLookup<PlayerPassiveToolsStateElement> passiveToolsLookup,
                                                 out PlayerPassiveToolsState passiveToolsState)
    {
        PlayerPassiveToolsStateBufferUtility.Read(shooterEntity,
                                                  in passiveToolsLookup,
                                                  out passiveToolsState);
    }

    #endregion
}
