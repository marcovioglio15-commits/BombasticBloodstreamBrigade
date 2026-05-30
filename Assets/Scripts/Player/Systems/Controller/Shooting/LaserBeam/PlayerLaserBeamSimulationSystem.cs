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
[UpdateAfter(typeof(PlayerLookRotationSystem))]
public partial struct PlayerLaserBeamSimulationSystem : ISystem
{
    #region Constants
    private const int MaximumSupportedSplitChildLanes = 24;
    private const float PerfectCircleTrajectorySpeedMultiplier = 1f;
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
        state.RequireForUpdate<PlayerLaserBeamStormTickPulse>();
        state.RequireForUpdate<PlayerLaserBeamLaneElement>();
        state.RequireForUpdate<PlayerInputState>();
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<PlayerShootingState>();
        state.RequireForUpdate<PlayerRuntimeShootingConfig>();
        state.RequireForUpdate<PlayerRuntimeShootingAppliedElementSlot>();
        state.RequireForUpdate<PlayerPassiveToolsStateElement>();
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
        ComponentLookup<PlayerMovementState> movementStateLookup = SystemAPI.GetComponentLookup<PlayerMovementState>(true);
        ComponentLookup<PlayerRuntimeShootingConfig> runtimeShootingConfigLookup = SystemAPI.GetComponentLookup<PlayerRuntimeShootingConfig>(true);
        BufferLookup<PlayerPassiveToolsStateElement> passiveToolsStateLookup = SystemAPI.GetBufferLookup<PlayerPassiveToolsStateElement>(true);
        ComponentLookup<ShooterMuzzleAnchor> muzzleLookup = SystemAPI.GetComponentLookup<ShooterMuzzleAnchor>(true);
        ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<LocalToWorld> localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        ComponentLookup<PlayerCameraShakeState> cameraShakeStateLookup = SystemAPI.GetComponentLookup<PlayerCameraShakeState>(false);
        ComponentLookup<PlayerRuntimeCameraConfig> runtimeCameraConfigLookup = SystemAPI.GetComponentLookup<PlayerRuntimeCameraConfig>(true);
        BufferLookup<PlayerRuntimeShootingAppliedElementSlot> appliedElementSlotsLookup = SystemAPI.GetBufferLookup<PlayerRuntimeShootingAppliedElementSlot>(true);
        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);

            foreach ((RefRO<LocalTransform> localTransform,
                  RefRW<PlayerShootingState> shootingState,
                  RefRW<PlayerLaserBeamState> laserBeamState,
                  DynamicBuffer<PlayerLaserBeamStormTickPulse> stormTickPulses,
                  DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRO<LocalTransform>,
                                    RefRW<PlayerShootingState>,
                                    RefRW<PlayerLaserBeamState>,
                                    DynamicBuffer<PlayerLaserBeamStormTickPulse>,
                                    DynamicBuffer<PlayerLaserBeamLaneElement>>()
                             .WithEntityAccess())
        {
            if (!inputStateLookup.HasComponent(playerEntity) ||
                !movementStateLookup.HasComponent(playerEntity) ||
                !runtimeShootingConfigLookup.HasComponent(playerEntity) ||
                !passiveToolsStateLookup.HasBuffer(playerEntity) ||
                !appliedElementSlotsLookup.HasBuffer(playerEntity))
            {
                continue;
            }

            DynamicBuffer<PlayerLaserBeamLaneElement> mutableLaserBeamLanes = laserBeamLanes;
            mutableLaserBeamLanes.Clear();

            PlayerLaserBeamState currentLaserBeamState = laserBeamState.ValueRO;
            PlayerPassiveToolsState currentPassiveToolsState;
            PlayerPassiveToolsStateBufferUtility.Read(playerEntity,
                                                      in passiveToolsStateLookup,
                                                      out currentPassiveToolsState);
            PlayerLaserBeamStateUtility.UpdateTriggeredActiveLaser(ref currentLaserBeamState, deltaTime);
            bool hasTriggeredActiveLaser = PlayerLaserBeamStateUtility.HasTriggeredActiveLaser(in currentLaserBeamState);
            PlayerPassiveToolsState effectivePassiveToolsState;
            PlayerLaserBeamStateUtility.ResolveEffectivePassiveToolsState(in currentPassiveToolsState,
                                                                          in currentLaserBeamState,
                                                                          out effectivePassiveToolsState);
            PlayerInputState currentInputState = inputStateLookup[playerEntity];
            PlayerMovementState currentMovementState = movementStateLookup[playerEntity];
            PlayerRuntimeShootingConfig currentRuntimeShootingConfig = runtimeShootingConfigLookup[playerEntity];
            DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> appliedElementSlots = appliedElementSlotsLookup[playerEntity];
            ElementalEffectConfig unusedElementalEffect = default;
            bool hasLaserBeam = effectivePassiveToolsState.HasLaserBeam != 0;

            if (!hasLaserBeam)
            {
                PlayerLaserBeamStateUtility.ResetBeamState(ref currentLaserBeamState, stormTickPulses);
                shootingState.ValueRW.VisualShootingActive = 0;
                laserBeamState.ValueRW = currentLaserBeamState;
                continue;
            }

            LaserBeamPassiveConfig laserBeamConfig = effectivePassiveToolsState.LaserBeam;
            PlayerLaserBeamStateUtility.UpdateCooldown(ref currentLaserBeamState, in laserBeamConfig, deltaTime);
            PlayerLaserBeamStateUtility.UpdateChargeImpulse(ref currentLaserBeamState, deltaTime);
            PlayerLaserBeamStateUtility.AdvanceStormTickPulses(stormTickPulses, in laserBeamConfig, deltaTime);
            PlayerLaserBeamStateUtility.UpdateStormBurstTimer(ref currentLaserBeamState,
                                                              in laserBeamConfig,
                                                              in stormTickPulses,
                                                              deltaTime);
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
                PlayerLaserBeamStateUtility.ClearStormTickPulses(stormTickPulses);

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
                PlayerLaserBeamStateUtility.ClearStormTickPulses(stormTickPulses);
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
                PlayerLaserBeamStateUtility.ClearStormTickPulses(stormTickPulses);
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
                PlayerLaserBeamStateUtility.ClearStormTickPulses(stormTickPulses);
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

            // Beam start and each damage tick mark a fresh Fire Shake pulse. Continuous beam frames between ticks would
            // otherwise stack one trauma unit per frame, sustaining the shake at saturation regardless of fire rate.
            // The pulse is also gated by the per-player Suppress On Laser Beam toggle authored on the fire-shake block
            // so designers can keep the regular-shot shake while silencing it during the laser's continuous tick.
            if ((beamStartedThisFrame || beamTickReadyThisFrame) &&
                !IsFireShakeSuppressedByLaserBeam(playerEntity, in runtimeCameraConfigLookup))
                EnqueueFireShakeRequest(playerEntity, ref cameraShakeStateLookup);

            float3 baseDirection = PlayerLaserBeamUtility.ResolveCurrentForwardDirection(in localTransform.ValueRO);
            float travelDistance = hasTriggeredActiveLaser
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

            if (!hasTriggeredActiveLaser && hasChargeImpulse)
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
                              primaryLaneCount,
                              false,
                              playerEntity,
                              localTransform.ValueRO.Position,
                              currentMovementState.Velocity,
                              laneSpawnPosition,
                              laneDirection,
                              currentLaserBeamState.ConsecutiveActiveElapsed,
                              travelDistance,
                              projectileTemplate.Range,
                              projectileTemplate.Lifetime,
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
                                      travelDistance,
                                      projectileTemplate.Range,
                                      projectileTemplate.Lifetime,
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
    /// Resolves whether the per-player Fire Shake configuration asks the Laser Beam to skip enqueuing fire-shake
    /// pulses. Used to keep the laser's continuous tick from stacking a sustained camera kick or rumble while still
    /// letting regular-shot fire-shake play through the normal projectile spawn path.
    /// </summary>
    /// <param name="playerEntity">Player entity currently firing the beam.</param>
    /// <param name="runtimeCameraConfigLookup">Read-only lookup for the runtime camera config carrying the toggle.</param>
    /// <returns>True when the Fire Shake must remain silent for this laser-firing frame.</returns>
    private static bool IsFireShakeSuppressedByLaserBeam(Entity playerEntity,
                                                          in ComponentLookup<PlayerRuntimeCameraConfig> runtimeCameraConfigLookup)
    {
        if (!runtimeCameraConfigLookup.HasComponent(playerEntity))
            return false;

        return runtimeCameraConfigLookup[playerEntity].FireShake.SuppressOnLaserBeam != 0;
    }

    /// <summary>
    /// Marks the player's camera shake state as having pulsed a fire request this frame for the Laser Beam. The
    /// camera follow system consumes the flag when it evolves the fire-shake trauma, so each beam start and damage
    /// tick lands as exactly one unit of added trauma. Players without the shake state are silently skipped so the
    /// utility stays safe for laser-only spawn flows that do not own the camera (testing tools, future split worlds).
    /// </summary>
    /// <param name="playerEntity">Player entity currently firing the beam.</param>
    /// <param name="cameraShakeStateLookup">Mutable lookup used to flag the pending fire request.</param>
    private static void EnqueueFireShakeRequest(Entity playerEntity,
                                                 ref ComponentLookup<PlayerCameraShakeState> cameraShakeStateLookup)
    {
        if (!cameraShakeStateLookup.HasComponent(playerEntity))
            return;

        PlayerCameraShakeState shakeState = cameraShakeStateLookup[playerEntity];
        shakeState.FireRequestPending = 1;
        cameraShakeStateLookup[playerEntity] = shakeState;
    }

    /// <summary>
    /// Appends one beam lane using either the straight-line builder or the Perfect Circle sampler, depending on passive state.
    /// </summary>
    /// <param name="laserBeamLanes">Output lane buffer for the current player.</param>
    /// <param name="laneIndex">Stable lane index assigned to the lane.</param>
    /// <param name="laneCount">Total sibling lane count used by layered orbital paths.</param>
    /// <param name="isSplitChild">True when the lane belongs to one split branch.</param>
    /// <param name="shooterEntity">Player entity owning the beam.</param>
    /// <param name="shooterPosition">Current player position.</param>
    /// <param name="shooterVelocity">Current player velocity.</param>
    /// <param name="spawnPosition">World-space origin of the lane.</param>
    /// <param name="direction">World-space forward direction of the lane.</param>
    /// <param name="activeSeconds">Current uninterrupted active time.</param>
    /// <param name="travelDistance">Current beam travel budget used by straight and Perfect Circle lane builders.</param>
    /// <param name="rangeLimit">Effective projectile range inherited by the beam.</param>
    /// <param name="lifetimeLimit">Effective projectile lifetime inherited by the beam.</param>
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
                                      int laneCount,
                                      bool isSplitChild,
                                      Entity shooterEntity,
                                      float3 shooterPosition,
                                      float3 shooterVelocity,
                                      float3 spawnPosition,
                                      float3 direction,
                                      float activeSeconds,
                                      float travelDistance,
                                      float rangeLimit,
                                      float lifetimeLimit,
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
            // Regular Perfect Circle projectiles always advance with authored orbital speeds. Laser virtual speed grows
            // the visible travel budget only, otherwise Shot Range formulas also deform orbital beam geometry.
            return PlayerLaserBeamPerfectCircleUtility.TryAppendPerfectCircleLaneSegments(ref laserBeamLanes,
                                                                                          laneIndex,
                                                                                          laneCount,
                                                                                          isSplitChild,
                                                                                          shooterEntity,
                                                                                          shooterPosition,
                                                                                          float3.zero,
                                                                                          spawnPosition,
                                                                                          direction,
                                                                                          activeSeconds,
                                                                                          travelDistance,
                                                                                          rangeLimit,
                                                                                          lifetimeLimit,
                                                                                          PerfectCircleTrajectorySpeedMultiplier,
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
    /// <param name="travelDistance">Straight-line travel budget used when Perfect Circle is disabled.</param>
    /// <param name="rangeLimit">Effective projectile range inherited by the parent lanes.</param>
    /// <param name="lifetimeLimit">Effective projectile lifetime inherited by the parent lanes.</param>
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
                                              float travelDistance,
                                              float rangeLimit,
                                              float lifetimeLimit,
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
                                             math.max(1, splittingProjectilesConfig.CustomAnglesDegrees.Length),
                                             shooterEntity,
                                             shooterPosition,
                                             shooterVelocity,
                                             terminalSegment.EndPoint,
                                             childDirection,
                                             activeSeconds,
                                             travelDistance,
                                             rangeLimit,
                                             lifetimeLimit,
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
                                             splitCount,
                                             shooterEntity,
                                             shooterPosition,
                                             shooterVelocity,
                                             terminalSegment.EndPoint,
                                             childDirection,
                                             activeSeconds,
                                             travelDistance,
                                             rangeLimit,
                                             lifetimeLimit,
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
    /// <param name="laneCount">Total sibling split-lane count used by layered orbital paths.</param>
    /// <param name="shooterEntity">Player entity owning the beam.</param>
    /// <param name="shooterPosition">Current player position.</param>
    /// <param name="shooterVelocity">Current player velocity.</param>
    /// <param name="spawnPosition">World-space origin of the split branch.</param>
    /// <param name="direction">World-space forward direction of the split branch.</param>
    /// <param name="activeSeconds">Current uninterrupted active time.</param>
    /// <param name="parentTravelDistance">Straight-line travel budget inherited from the parent lane.</param>
    /// <param name="parentRangeLimit">Effective projectile range inherited from the parent lane.</param>
    /// <param name="parentLifetimeLimit">Effective projectile lifetime inherited from the parent lane.</param>
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
                                             int laneCount,
                                             Entity shooterEntity,
                                             float3 shooterPosition,
                                             float3 shooterVelocity,
                                             float3 spawnPosition,
                                             float3 direction,
                                             float activeSeconds,
                                             float parentTravelDistance,
                                             float parentRangeLimit,
                                             float parentLifetimeLimit,
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
        TryAppendLane(ref laserBeamLanes,
                      laneIndex,
                      laneCount,
                      true,
                      shooterEntity,
                      shooterPosition,
                      shooterVelocity,
                      spawnPosition,
                      direction,
                      activeSeconds,
                      splitTravelDistance,
                      splitRangeLimit,
                      splitLifetimeLimit,
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
