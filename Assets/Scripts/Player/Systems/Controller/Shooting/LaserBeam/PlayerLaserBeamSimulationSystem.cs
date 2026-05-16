using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// Resolves activation, cooldown and bounced lane geometry for the player Laser Beam passive override.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerShootingIntentSystem))]
[UpdateAfter(typeof(PlayerMovementApplySystem))]
public partial struct PlayerLaserBeamSimulationSystem : ISystem
{
    #region Constants
    private const int MaximumSupportedSplitChildLanes = 24;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers all required runtime dependencies for Laser Beam simulation.
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerLaserBeamState>();
        state.RequireForUpdate<PlayerLaserBeamLaneElement>();
        state.RequireForUpdate<PlayerInputState>();
        state.RequireForUpdate<PlayerLookState>();
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<PlayerShootingState>();
        state.RequireForUpdate<PlayerRuntimeShootingConfig>();
        state.RequireForUpdate<PlayerRuntimeShootingAppliedElementSlot>();
        state.RequireForUpdate<PlayerPassiveToolsState>();
        state.RequireForUpdate<LocalTransform>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    /// <summary>
    /// Updates beam activation timers and rebuilds the current segment buffer for every active player beam.
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        if (PlayerGameplayPauseUtility.IsHardGameplayPauseActive())
            return;

        float deltaTime = SystemAPI.Time.DeltaTime;
        float globalTime = (float)SystemAPI.Time.ElapsedTime;
        PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        int wallsLayerMask = WorldWallCollisionUtility.ResolveWallsLayerMask();

        if (SystemAPI.TryGetSingleton<PlayerWorldLayersConfig>(out PlayerWorldLayersConfig worldLayersConfig) &&
            worldLayersConfig.WallsLayerMask != 0)
        {
            wallsLayerMask = worldLayersConfig.WallsLayerMask;
        }

        bool wallsEnabled = wallsLayerMask != 0;
        CollisionFilter wallsCollisionFilter = wallsEnabled
            ? WorldWallCollisionUtility.BuildWallsCollisionFilter(wallsLayerMask)
            : default;

        state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

        ComponentLookup<PlayerPowerUpsState> powerUpsStateLookup = SystemAPI.GetComponentLookup<PlayerPowerUpsState>(true);
        ComponentLookup<PlayerInputState> inputStateLookup = SystemAPI.GetComponentLookup<PlayerInputState>(true);
        ComponentLookup<PlayerLookState> lookStateLookup = SystemAPI.GetComponentLookup<PlayerLookState>(true);
        ComponentLookup<PlayerMovementState> movementStateLookup = SystemAPI.GetComponentLookup<PlayerMovementState>(true);
        ComponentLookup<PlayerRuntimeShootingConfig> runtimeShootingConfigLookup = SystemAPI.GetComponentLookup<PlayerRuntimeShootingConfig>(true);
        ComponentLookup<PlayerPassiveToolsState> passiveToolsStateLookup = SystemAPI.GetComponentLookup<PlayerPassiveToolsState>(true);
        ComponentLookup<ShooterMuzzleAnchor> muzzleLookup = SystemAPI.GetComponentLookup<ShooterMuzzleAnchor>(true);
        ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<LocalToWorld> localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        BufferLookup<PlayerRuntimeShootingAppliedElementSlot> appliedElementSlotsLookup = SystemAPI.GetBufferLookup<PlayerRuntimeShootingAppliedElementSlot>(true);
        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);

        foreach ((RefRO<LocalTransform> localTransform,
                  RefRW<PlayerShootingState> shootingState,
                  RefRW<PlayerLaserBeamState> laserBeamState,
                  DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRO<LocalTransform>,
                                    RefRW<PlayerShootingState>,
                                    RefRW<PlayerLaserBeamState>,
                                    DynamicBuffer<PlayerLaserBeamLaneElement>>()
                             .WithEntityAccess())
        {
            if (!inputStateLookup.HasComponent(playerEntity) ||
                !lookStateLookup.HasComponent(playerEntity) ||
                !movementStateLookup.HasComponent(playerEntity) ||
                !runtimeShootingConfigLookup.HasComponent(playerEntity) ||
                !passiveToolsStateLookup.HasComponent(playerEntity) ||
                !appliedElementSlotsLookup.HasBuffer(playerEntity))
            {
                continue;
            }

            DynamicBuffer<PlayerLaserBeamLaneElement> mutableLaserBeamLanes = laserBeamLanes;
            mutableLaserBeamLanes.Clear();

            PlayerLaserBeamState currentLaserBeamState = laserBeamState.ValueRO;
            PlayerPassiveToolsState currentPassiveToolsState = passiveToolsStateLookup[playerEntity];
            PlayerLaserBeamStateUtility.UpdateTriggeredActiveLaser(ref currentLaserBeamState, deltaTime);
            bool hasTriggeredActiveLaser = PlayerLaserBeamStateUtility.HasTriggeredActiveLaser(in currentLaserBeamState);
            PlayerPassiveToolsState effectivePassiveToolsState = PlayerLaserBeamStateUtility.ResolveEffectivePassiveToolsState(in currentPassiveToolsState,
                                                                                                                                in currentLaserBeamState);
            PlayerInputState currentInputState = inputStateLookup[playerEntity];
            PlayerLookState currentLookState = lookStateLookup[playerEntity];
            PlayerMovementState currentMovementState = movementStateLookup[playerEntity];
            PlayerRuntimeShootingConfig currentRuntimeShootingConfig = runtimeShootingConfigLookup[playerEntity];
            DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> appliedElementSlots = appliedElementSlotsLookup[playerEntity];
            ElementalEffectConfig unusedElementalEffect = default;
            bool hasLaserBeam = effectivePassiveToolsState.HasLaserBeam != 0;

            if (!hasLaserBeam)
            {
                PlayerLaserBeamStateUtility.ResetBeamState(ref currentLaserBeamState);
                shootingState.ValueRW.VisualShootingActive = 0;
                laserBeamState.ValueRW = currentLaserBeamState;
                continue;
            }

            LaserBeamPassiveConfig laserBeamConfig = effectivePassiveToolsState.LaserBeam;
            PlayerLaserBeamStateUtility.UpdateCooldown(ref currentLaserBeamState, in laserBeamConfig, deltaTime);
            PlayerLaserBeamStateUtility.UpdateChargeImpulse(ref currentLaserBeamState, deltaTime);
            PlayerLaserBeamStateUtility.AdvanceStormTickPulses(ref currentLaserBeamState, in laserBeamConfig, deltaTime);
            PlayerLaserBeamStateUtility.UpdateStormBurstTimer(ref currentLaserBeamState, in laserBeamConfig, deltaTime);
            bool isShootPressed = currentInputState.Shoot > 0.5f;
            bool hasChargeImpulse = currentLaserBeamState.ChargeImpulseRemainingSeconds > 0f;
            bool isShootingSuppressed = powerUpsStateLookup.HasComponent(playerEntity) &&
                                        powerUpsStateLookup[playerEntity].IsShootingSuppressed != 0;
            bool shouldKeepBeamAlive = hasTriggeredActiveLaser || isShootPressed || hasChargeImpulse;

            if (!shouldKeepBeamAlive || (isShootingSuppressed && !hasTriggeredActiveLaser))
            {
                currentLaserBeamState.IsActive = 0;
                currentLaserBeamState.IsTickReady = 0;
                currentLaserBeamState.LastResolvedPrimaryLaneCount = 0;
                currentLaserBeamState.ConsecutiveActiveElapsed = 0f;
                currentLaserBeamState.DamageTickTimer = 0f;
                currentLaserBeamState.ContinuousDamageAccumulatorSeconds = 0f;
                PlayerLaserBeamStateUtility.ClearStormBurst(ref currentLaserBeamState);
                PlayerLaserBeamStateUtility.ClearStormTickPulses(ref currentLaserBeamState);

                if (isShootingSuppressed)
                    PlayerLaserBeamStateUtility.ClearChargeImpulse(ref currentLaserBeamState);

                shootingState.ValueRW.VisualShootingActive = 0;
                laserBeamState.ValueRW = currentLaserBeamState;
                continue;
            }

            if (!hasTriggeredActiveLaser && currentLaserBeamState.IsOverheated != 0)
            {
                currentLaserBeamState.IsActive = 0;
                currentLaserBeamState.IsTickReady = 0;
                currentLaserBeamState.LastResolvedPrimaryLaneCount = 0;
                currentLaserBeamState.ContinuousDamageAccumulatorSeconds = 0f;
                PlayerLaserBeamStateUtility.ClearStormBurst(ref currentLaserBeamState);
                PlayerLaserBeamStateUtility.ClearStormTickPulses(ref currentLaserBeamState);
                PlayerLaserBeamStateUtility.ClearChargeImpulse(ref currentLaserBeamState);
                shootingState.ValueRW.VisualShootingActive = 0;
                laserBeamState.ValueRW = currentLaserBeamState;
                continue;
            }

            bool wasActive = currentLaserBeamState.IsActive != 0;
            currentLaserBeamState.IsActive = 1;
            bool beamStartedThisFrame = !wasActive;

            if (hasTriggeredActiveLaser || isShootPressed)
                currentLaserBeamState.ConsecutiveActiveElapsed += math.max(0f, deltaTime);
            else
                currentLaserBeamState.ConsecutiveActiveElapsed = 0f;

            if (!wasActive)
                currentLaserBeamState.DamageTickTimer = 0f;
            else
                currentLaserBeamState.DamageTickTimer -= math.max(0f, deltaTime);

            if (!hasTriggeredActiveLaser &&
                isShootPressed &&
                PlayerLaserBeamStateUtility.ShouldOverheat(in laserBeamConfig, currentLaserBeamState.ConsecutiveActiveElapsed))
            {
                currentLaserBeamState.IsActive = 0;
                currentLaserBeamState.IsOverheated = 1;
                currentLaserBeamState.IsTickReady = 0;
                currentLaserBeamState.LastResolvedPrimaryLaneCount = 0;
                currentLaserBeamState.CooldownRemaining = math.max(0f, laserBeamConfig.CooldownSeconds);
                currentLaserBeamState.ConsecutiveActiveElapsed = 0f;
                currentLaserBeamState.DamageTickTimer = math.max(0.0001f, laserBeamConfig.DamageTickIntervalSeconds);
                currentLaserBeamState.ContinuousDamageAccumulatorSeconds = 0f;
                PlayerLaserBeamStateUtility.ClearStormBurst(ref currentLaserBeamState);
                PlayerLaserBeamStateUtility.ClearStormTickPulses(ref currentLaserBeamState);
                PlayerLaserBeamStateUtility.ClearChargeImpulse(ref currentLaserBeamState);
                shootingState.ValueRW.VisualShootingActive = 0;
                laserBeamState.ValueRW = currentLaserBeamState;
                continue;
            }

            currentLaserBeamState.IsTickReady = currentLaserBeamState.DamageTickTimer <= 0f ? (byte)1 : (byte)0;
            bool beamTickReadyThisFrame = currentLaserBeamState.IsTickReady != 0;
            shootingState.ValueRW.VisualShootingActive = 1;

            PlayerProjectileRequestTemplate projectileTemplate = hasTriggeredActiveLaser
                ? currentLaserBeamState.TriggeredActiveProjectileTemplate
                : PlayerProjectileRequestUtility.BuildProjectileTemplate(in currentRuntimeShootingConfig,
                                                                         appliedElementSlots,
                                                                         in effectivePassiveToolsState,
                                                                         1f,
                                                                         1f,
                                                                         1f,
                                                                         1f,
                                                                         1f,
                                                                         false,
                                                                         in unusedElementalEffect,
                                                                         0f);
            bool hasPerfectCircle = effectivePassiveToolsState.HasPerfectCircle != 0;
            float virtualProjectileSpeedMultiplier = math.max(0f, laserBeamConfig.VirtualProjectileSpeedMultiplier);
            float projectileSpeed = math.max(0f,
                                             projectileTemplate.Speed * virtualProjectileSpeedMultiplier);

            if (!hasPerfectCircle && projectileSpeed <= 0f)
            {
                currentLaserBeamState.IsActive = 0;
                currentLaserBeamState.IsTickReady = 0;
                currentLaserBeamState.LastResolvedPrimaryLaneCount = 0;
                currentLaserBeamState.ContinuousDamageAccumulatorSeconds = 0f;
                PlayerLaserBeamStateUtility.ClearStormBurst(ref currentLaserBeamState);
                PlayerLaserBeamStateUtility.ClearStormTickPulses(ref currentLaserBeamState);
                shootingState.ValueRW.VisualShootingActive = 0;
                laserBeamState.ValueRW = currentLaserBeamState;
                continue;
            }

            int primaryLaneCount = effectivePassiveToolsState.HasShotgun != 0
                ? math.max(1, effectivePassiveToolsState.Shotgun.ProjectileCount)
                : 1;
            float coneAngleDegrees = effectivePassiveToolsState.HasShotgun != 0
                ? math.max(0f, effectivePassiveToolsState.Shotgun.ConeAngleDegrees)
                : 0f;
            float3 spawnPosition = PlayerProjectileRequestUtility.ResolveShootSpawnPosition(playerEntity,
                                                                                            in localTransform.ValueRO,
                                                                                            in currentRuntimeShootingConfig,
                                                                                            in muzzleLookup,
                                                                                            in transformLookup,
                                                                                            in localToWorldLookup);
            if (canEnqueueAudioRequests)
            {
                if (beamStartedThisFrame)
                    GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.PlayerShootLaserContinuous, spawnPosition);

                if (beamTickReadyThisFrame)
                    GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.PlayerShootLaserTick, spawnPosition);
            }

            float3 baseDirection = PlayerProjectileRequestUtility.ResolveShootDirection(in currentLookState, in localTransform.ValueRO);
            float travelDistance = hasPerfectCircle
                ? 0f
                : hasTriggeredActiveLaser
                    ? PlayerLaserBeamUtility.ResolveMaximumTravelDistance(projectileSpeed,
                                                                         projectileTemplate.Range,
                                                                         projectileTemplate.Lifetime)
                    : PlayerLaserBeamUtility.ResolveTravelDistance(currentLaserBeamState.ConsecutiveActiveElapsed,
                                                                   projectileSpeed,
                                                                   projectileTemplate.Range,
                                                                   projectileTemplate.Lifetime);
            float chargeImpulseWidthMultiplier = !hasTriggeredActiveLaser && hasChargeImpulse
                ? math.max(1f, currentLaserBeamState.ChargeImpulseWidthMultiplier)
                : 1f;

            if (!hasTriggeredActiveLaser && !hasPerfectCircle && hasChargeImpulse)
                travelDistance = math.max(travelDistance, currentLaserBeamState.ChargeImpulseTravelDistance);

            float collisionRadius = PlayerLaserBeamUtility.ResolveCollisionRadius(projectileTemplate.ScaleMultiplier,
                                                                                 laserBeamConfig.CollisionWidthMultiplier) * chargeImpulseWidthMultiplier;
            collisionRadius += math.max(0f, projectileTemplate.ExplosionRadius);
            float bodyWidth = PlayerLaserBeamUtility.ResolveBodyWidth(projectileTemplate.ScaleMultiplier,
                                                                     laserBeamConfig.BodyWidthMultiplier) * chargeImpulseWidthMultiplier;
            int maximumBounceSegments = PlayerLaserBeamStateUtility.ResolveMaximumBounceSegments(in effectivePassiveToolsState, in laserBeamConfig);

            currentLaserBeamState.LastResolvedPrimaryLaneCount = primaryLaneCount;
            FixedList512Bytes<byte> primaryLaneReachedVirtualDespawnFlags = default;

            for (int laneIndex = 0; laneIndex < primaryLaneCount; laneIndex++)
            {
                float3 laneSpawnPosition = spawnPosition;
                float3 laneDirection = PlayerLaserBeamUtility.ResolveSpreadDirection(baseDirection,
                                                                                    laneIndex,
                                                                                    primaryLaneCount,
                                                                                    coneAngleDegrees);
                bool reachedVirtualDespawn;
                TryAppendLane(ref mutableLaserBeamLanes,
                              laneIndex,
                              false,
                              playerEntity,
                              localTransform.ValueRO.Position,
                              currentMovementState.Velocity,
                              laneSpawnPosition,
                              laneDirection,
                              currentLaserBeamState.ConsecutiveActiveElapsed,
                              globalTime,
                              travelDistance,
                              projectileTemplate.Range,
                              projectileTemplate.Lifetime,
                              virtualProjectileSpeedMultiplier,
                              collisionRadius,
                              bodyWidth,
                              1f,
                              maximumBounceSegments,
                              in effectivePassiveToolsState.PerfectCircle,
                              hasPerfectCircle,
                              in physicsWorldSingleton,
                              in wallsCollisionFilter,
                              out reachedVirtualDespawn,
                              wallsEnabled);
                primaryLaneReachedVirtualDespawnFlags.Add(reachedVirtualDespawn ? (byte)1 : (byte)0);
            }

            if (effectivePassiveToolsState.HasSplittingProjectiles != 0 &&
                effectivePassiveToolsState.SplittingProjectiles.TriggerMode == ProjectileSplitTriggerMode.OnProjectileDespawn)
            {
                AppendSplitChildLanes(ref mutableLaserBeamLanes,
                                      playerEntity,
                                      localTransform.ValueRO.Position,
                                      currentMovementState.Velocity,
                                      primaryLaneCount,
                                      currentLaserBeamState.ConsecutiveActiveElapsed,
                                      globalTime,
                                      travelDistance,
                                      projectileTemplate.Range,
                                      projectileTemplate.Lifetime,
                                      virtualProjectileSpeedMultiplier,
                                      collisionRadius,
                                      bodyWidth,
                                      maximumBounceSegments,
                                      in primaryLaneReachedVirtualDespawnFlags,
                                      in effectivePassiveToolsState.PerfectCircle,
                                      hasPerfectCircle,
                                      in effectivePassiveToolsState.SplittingProjectiles,
                                      in physicsWorldSingleton,
                                      in wallsCollisionFilter,
                                      wallsEnabled);
            }

            laserBeamState.ValueRW = currentLaserBeamState;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Appends one beam lane using either the straight-line builder or the Perfect Circle sampler, depending on passive state.
    /// </summary>
    /// <param name="laserBeamLanes">Output lane buffer for the current player.</param>
    /// <param name="laneIndex">Stable lane index assigned to the lane.</param>
    /// <param name="isSplitChild">True when the lane belongs to one split branch.</param>
    /// <param name="shooterEntity">Player entity owning the beam.</param>
    /// <param name="shooterPosition">Current player position.</param>
    /// <param name="shooterVelocity">Current player velocity.</param>
    /// <param name="spawnPosition">World-space origin of the lane.</param>
    /// <param name="direction">World-space forward direction of the lane.</param>
    /// <param name="activeSeconds">Current uninterrupted active time.</param>
    /// <param name="globalTime">Current elapsed world time.</param>
    /// <param name="travelDistance">Straight-line travel budget used when Perfect Circle is disabled.</param>
    /// <param name="rangeLimit">Effective projectile range inherited by the beam.</param>
    /// <param name="lifetimeLimit">Effective projectile lifetime inherited by the beam.</param>
    /// <param name="speedMultiplier">Beam-local speed multiplier applied to motion simulation.</param>
    /// <param name="collisionRadius">Effective gameplay width of the lane.</param>
    /// <param name="bodyWidth">Effective visual width of the lane.</param>
    /// <param name="damageMultiplier">Lane-local damage multiplier.</param>
    /// <param name="maximumBounceSegments">Maximum reflected wall segments supported by straight-line mode.</param>
    /// <param name="perfectCircleConfig">Aggregated Perfect Circle passive configuration.</param>
    /// <param name="hasPerfectCircle">True when the current lane must follow Perfect Circle sampling.</param>
    /// <param name="physicsWorldSingleton">Physics world used for wall clipping.</param>
    /// <param name="wallsCollisionFilter">Collision filter used for world walls.</param>
    /// <param name="reachedVirtualDespawn">True when the simulated lane has reached its despawn condition and can emit split-on-despawn branches.</param>
    /// <param name="wallsEnabled">True when wall clipping should be evaluated.</param>
    /// <returns>True when at least one lane segment was appended.</returns>
    private static bool TryAppendLane(ref DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                                      int laneIndex,
                                      bool isSplitChild,
                                      Entity shooterEntity,
                                      float3 shooterPosition,
                                      float3 shooterVelocity,
                                      float3 spawnPosition,
                                      float3 direction,
                                      float activeSeconds,
                                      float globalTime,
                                      float travelDistance,
                                      float rangeLimit,
                                      float lifetimeLimit,
                                      float speedMultiplier,
                                      float collisionRadius,
                                      float bodyWidth,
                                      float damageMultiplier,
                                      int maximumBounceSegments,
                                      in PerfectCirclePassiveConfig perfectCircleConfig,
                                      bool hasPerfectCircle,
                                      in PhysicsWorldSingleton physicsWorldSingleton,
                                      in CollisionFilter wallsCollisionFilter,
                                      out bool reachedVirtualDespawn,
                                      bool wallsEnabled)
    {
        reachedVirtualDespawn = false;
        float safeCollisionRadius = PlayerLaserBeamUtility.ClampCollisionRadius(collisionRadius);
        float safeBodyWidth = PlayerLaserBeamUtility.ClampBodyWidth(bodyWidth);
        float safeDamageMultiplier = math.max(0f, damageMultiplier);

        if (hasPerfectCircle)
        {
            return PlayerLaserBeamPerfectCircleUtility.TryAppendPerfectCircleLaneSegments(ref laserBeamLanes,
                                                                                          laneIndex,
                                                                                          isSplitChild,
                                                                                          shooterEntity,
                                                                                          shooterPosition,
                                                                                          shooterVelocity,
                                                                                          spawnPosition,
                                                                                          direction,
                                                                                          activeSeconds,
                                                                                          globalTime,
                                                                                          rangeLimit,
                                                                                          lifetimeLimit,
                                                                                          speedMultiplier,
                                                                                          safeCollisionRadius,
                                                                                          safeBodyWidth,
                                                                                          safeDamageMultiplier,
                                                                                          in perfectCircleConfig,
                                                                                          in physicsWorldSingleton,
                                                                                          in wallsCollisionFilter,
                                                                                          out reachedVirtualDespawn,
                                                                                          wallsEnabled);
        }

        float requestedTravelDistance = PlayerLaserBeamUtility.ClampRequestedTravelDistance(travelDistance);

        if (requestedTravelDistance < PlayerLaserBeamUtility.MinimumTravelDistance)
            return false;

        int laneStartIndex = laserBeamLanes.Length;
        bool appended = PlayerLaserBeamUtility.TryAppendLaneSegments(ref laserBeamLanes,
                                                                     laneIndex,
                                                                     isSplitChild,
                                                                     spawnPosition,
                                                                     direction,
                                                                     requestedTravelDistance,
                                                                     safeCollisionRadius,
                                                                     safeBodyWidth,
                                                                     safeDamageMultiplier,
                                                                     maximumBounceSegments,
                                                                     in physicsWorldSingleton,
                                                                     in wallsCollisionFilter,
                                                                     wallsEnabled);

        if (!appended)
            return false;

        bool reachedLifetimeCap = lifetimeLimit > 0f && activeSeconds >= lifetimeLimit;
        bool reachedRangeCap = rangeLimit > 0f &&
                               requestedTravelDistance + PlayerLaserBeamUtility.MinimumTravelDistance >=
                               PlayerLaserBeamUtility.ClampRequestedTravelDistance(rangeLimit);
        bool blockedByWall = laserBeamLanes.Length > laneStartIndex &&
                             laserBeamLanes[laserBeamLanes.Length - 1].TerminalBlockedByWall != 0;
        reachedVirtualDespawn = blockedByWall || reachedLifetimeCap || reachedRangeCap;
        return true;
    }

    /// <summary>
    /// Appends all split-child lanes emitted from currently resolved primary terminal segments.
    /// </summary>
    /// <param name="laserBeamLanes">Output lane buffer containing the already-built primary lanes.</param>
    /// <param name="shooterEntity">Player entity owning the beam.</param>
    /// <param name="shooterPosition">Current player position.</param>
    /// <param name="shooterVelocity">Current player velocity.</param>
    /// <param name="primaryLaneCount">Number of primary lanes already present in the buffer.</param>
    /// <param name="activeSeconds">Current uninterrupted active time.</param>
    /// <param name="globalTime">Current elapsed world time.</param>
    /// <param name="travelDistance">Straight-line travel budget used when Perfect Circle is disabled.</param>
    /// <param name="rangeLimit">Effective projectile range inherited by the parent lanes.</param>
    /// <param name="lifetimeLimit">Effective projectile lifetime inherited by the parent lanes.</param>
    /// <param name="speedMultiplier">Beam-local speed multiplier inherited by the parent lanes.</param>
    /// <param name="collisionRadius">Effective gameplay width inherited by the parent lanes.</param>
    /// <param name="bodyWidth">Effective visual width inherited by the parent lanes.</param>
    /// <param name="maximumBounceSegments">Maximum reflected wall segments supported by straight-line mode.</param>
    /// <param name="primaryLaneReachedVirtualDespawnFlags">Per-lane flags telling whether each primary lane reached a virtual despawn condition.</param>
    /// <param name="perfectCircleConfig">Aggregated Perfect Circle passive configuration.</param>
    /// <param name="hasPerfectCircle">True when split children must also sample Perfect Circle.</param>
    /// <param name="splittingProjectilesConfig">Aggregated split-projectile passive configuration.</param>
    /// <param name="physicsWorldSingleton">Physics world used for wall clipping.</param>
    /// <param name="wallsCollisionFilter">Collision filter used for world walls.</param>
    /// <param name="wallsEnabled">True when wall clipping should be evaluated.</param>
    private static void AppendSplitChildLanes(ref DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                                              Entity shooterEntity,
                                              float3 shooterPosition,
                                              float3 shooterVelocity,
                                              int primaryLaneCount,
                                              float activeSeconds,
                                              float globalTime,
                                              float travelDistance,
                                              float rangeLimit,
                                              float lifetimeLimit,
                                              float speedMultiplier,
                                              float collisionRadius,
                                              float bodyWidth,
                                              int maximumBounceSegments,
                                              in FixedList512Bytes<byte> primaryLaneReachedVirtualDespawnFlags,
                                              in PerfectCirclePassiveConfig perfectCircleConfig,
                                              bool hasPerfectCircle,
                                              in SplittingProjectilesPassiveConfig splittingProjectilesConfig,
                                              in PhysicsWorldSingleton physicsWorldSingleton,
                                              in CollisionFilter wallsCollisionFilter,
                                              bool wallsEnabled)
    {
        int nextLaneIndex = primaryLaneCount;
        int maximumChildLaneCount = math.min(MaximumSupportedSplitChildLanes,
                                             math.max(0, primaryLaneCount * math.max(1, splittingProjectilesConfig.SplitProjectileCount)));

        for (int primaryLaneIndex = 0; primaryLaneIndex < primaryLaneCount; primaryLaneIndex++)
        {
            if (primaryLaneIndex >= primaryLaneReachedVirtualDespawnFlags.Length ||
                primaryLaneReachedVirtualDespawnFlags[primaryLaneIndex] == 0)
            {
                continue;
            }

            if (!PlayerLaserBeamStateUtility.TryResolveTerminalSegment(laserBeamLanes, primaryLaneIndex, out PlayerLaserBeamLaneElement terminalSegment))
                continue;

            switch (splittingProjectilesConfig.DirectionMode)
            {
                case ProjectileSplitDirectionMode.CustomAngles:
                    for (int customAngleIndex = 0;
                         customAngleIndex < splittingProjectilesConfig.CustomAnglesDegrees.Length && nextLaneIndex - primaryLaneCount < maximumChildLaneCount;
                         customAngleIndex++)
                    {
                        float angleDegrees = splittingProjectilesConfig.CustomAnglesDegrees[customAngleIndex] + splittingProjectilesConfig.SplitOffsetDegrees;
                        float3 childDirection = PlayerLaserBeamStateUtility.RotatePlanarDirection(terminalSegment.Direction, angleDegrees);
                        AppendSplitChildLane(ref laserBeamLanes,
                                             nextLaneIndex,
                                             shooterEntity,
                                             shooterPosition,
                                             shooterVelocity,
                                             terminalSegment.EndPoint,
                                             childDirection,
                                             activeSeconds,
                                             globalTime,
                                             travelDistance,
                                             rangeLimit,
                                             lifetimeLimit,
                                             speedMultiplier,
                                             collisionRadius,
                                             bodyWidth,
                                             maximumBounceSegments,
                                             in perfectCircleConfig,
                                             hasPerfectCircle,
                                             in splittingProjectilesConfig,
                                             in physicsWorldSingleton,
                                             in wallsCollisionFilter,
                                             wallsEnabled);
                        nextLaneIndex++;
                    }

                    break;
                default:
                    int splitCount = math.max(1, splittingProjectilesConfig.SplitProjectileCount);
                    float stepDegrees = 360f / splitCount;

                    for (int splitIndex = 0; splitIndex < splitCount && nextLaneIndex - primaryLaneCount < maximumChildLaneCount; splitIndex++)
                    {
                        float angleDegrees = splittingProjectilesConfig.SplitOffsetDegrees + stepDegrees * splitIndex;
                        float3 childDirection = PlayerLaserBeamStateUtility.RotatePlanarDirection(terminalSegment.Direction, angleDegrees);
                        AppendSplitChildLane(ref laserBeamLanes,
                                             nextLaneIndex,
                                             shooterEntity,
                                             shooterPosition,
                                             shooterVelocity,
                                             terminalSegment.EndPoint,
                                             childDirection,
                                             activeSeconds,
                                             globalTime,
                                             travelDistance,
                                             rangeLimit,
                                             lifetimeLimit,
                                             speedMultiplier,
                                             collisionRadius,
                                             bodyWidth,
                                             maximumBounceSegments,
                                             in perfectCircleConfig,
                                             hasPerfectCircle,
                                             in splittingProjectilesConfig,
                                             in physicsWorldSingleton,
                                             in wallsCollisionFilter,
                                             wallsEnabled);
                        nextLaneIndex++;
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Appends one split-child lane with inherited size, lifetime and speed modifiers from the split passive.
    /// </summary>
    /// <param name="laserBeamLanes">Output lane buffer.</param>
    /// <param name="laneIndex">Stable lane index assigned to the split branch.</param>
    /// <param name="shooterEntity">Player entity owning the beam.</param>
    /// <param name="shooterPosition">Current player position.</param>
    /// <param name="shooterVelocity">Current player velocity.</param>
    /// <param name="spawnPosition">World-space origin of the split branch.</param>
    /// <param name="direction">World-space forward direction of the split branch.</param>
    /// <param name="activeSeconds">Current uninterrupted active time.</param>
    /// <param name="globalTime">Current elapsed world time.</param>
    /// <param name="parentTravelDistance">Straight-line travel budget inherited from the parent lane.</param>
    /// <param name="parentRangeLimit">Effective projectile range inherited from the parent lane.</param>
    /// <param name="parentLifetimeLimit">Effective projectile lifetime inherited from the parent lane.</param>
    /// <param name="speedMultiplier">Beam-local speed multiplier inherited from the parent lane.</param>
    /// <param name="parentCollisionRadius">Effective gameplay width inherited from the parent lane.</param>
    /// <param name="parentBodyWidth">Effective visual width inherited from the parent lane.</param>
    /// <param name="maximumBounceSegments">Maximum reflected wall segments supported by straight-line mode.</param>
    /// <param name="perfectCircleConfig">Aggregated Perfect Circle passive configuration.</param>
    /// <param name="hasPerfectCircle">True when the split branch must also sample Perfect Circle.</param>
    /// <param name="splittingProjectilesConfig">Aggregated split-projectile passive configuration.</param>
    /// <param name="physicsWorldSingleton">Physics world used for wall clipping.</param>
    /// <param name="wallsCollisionFilter">Collision filter used for world walls.</param>
    /// <param name="wallsEnabled">True when wall clipping should be evaluated.</param>
    private static void AppendSplitChildLane(ref DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                                             int laneIndex,
                                             Entity shooterEntity,
                                             float3 shooterPosition,
                                             float3 shooterVelocity,
                                             float3 spawnPosition,
                                             float3 direction,
                                             float activeSeconds,
                                             float globalTime,
                                             float parentTravelDistance,
                                             float parentRangeLimit,
                                             float parentLifetimeLimit,
                                             float speedMultiplier,
                                             float parentCollisionRadius,
                                             float parentBodyWidth,
                                             int maximumBounceSegments,
                                             in PerfectCirclePassiveConfig perfectCircleConfig,
                                             bool hasPerfectCircle,
                                             in SplittingProjectilesPassiveConfig splittingProjectilesConfig,
                                             in PhysicsWorldSingleton physicsWorldSingleton,
                                             in CollisionFilter wallsCollisionFilter,
                                             bool wallsEnabled)
    {
        float splitLifetimeMultiplier = math.max(0f, splittingProjectilesConfig.SplitLifetimeMultiplier);
        float splitTravelDistance = math.max(0f,
                                             parentTravelDistance *
                                             math.max(0f, splittingProjectilesConfig.SplitSpeedMultiplier) *
                                             splitLifetimeMultiplier);
        float splitCollisionRadius = math.max(PlayerLaserBeamUtility.MinimumCollisionRadius,
                                              parentCollisionRadius * math.max(0.01f, splittingProjectilesConfig.SplitSizeMultiplier));
        float splitBodyWidth = math.max(0.02f,
                                        parentBodyWidth * math.max(0.01f, splittingProjectilesConfig.SplitSizeMultiplier));
        float splitRangeLimit = parentRangeLimit > 0f ? parentRangeLimit * splitLifetimeMultiplier : 0f;
        float splitLifetimeLimit = parentLifetimeLimit > 0f ? parentLifetimeLimit * splitLifetimeMultiplier : 0f;
        float splitSpeedMultiplier = math.max(0f, speedMultiplier) * math.max(0f, splittingProjectilesConfig.SplitSpeedMultiplier);
        TryAppendLane(ref laserBeamLanes,
                      laneIndex,
                      true,
                      shooterEntity,
                      shooterPosition,
                      shooterVelocity,
                      spawnPosition,
                      direction,
                      activeSeconds,
                      globalTime,
                      splitTravelDistance,
                      splitRangeLimit,
                      splitLifetimeLimit,
                      splitSpeedMultiplier,
                      splitCollisionRadius,
                      splitBodyWidth,
                      math.max(0f, splittingProjectilesConfig.SplitDamageMultiplier),
                      maximumBounceSegments,
                      in perfectCircleConfig,
                      hasPerfectCircle,
                      in physicsWorldSingleton,
                      in wallsCollisionFilter,
                      out _,
                      wallsEnabled);
    }

    #endregion

    #endregion
}
