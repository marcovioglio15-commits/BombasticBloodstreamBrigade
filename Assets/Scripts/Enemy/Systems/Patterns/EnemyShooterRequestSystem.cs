using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Resolves Shooter module runtime and enqueues ShootRequest entries for enemy entities.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemySpawnSystem))]
[UpdateBefore(typeof(EnemySteeringSystem))]
[UpdateBefore(typeof(EnemyPatternMovementSystem))]
public partial struct EnemyShooterRequestSystem : ISystem
{
    #region Constants
    private static readonly float3 ForwardAxis = new float3(0f, 0f, 1f);
    private const float DirectionEpsilon = 1e-6f;
    #endregion

    #region Fields
    private EntityQuery playerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig, LocalTransform>()
            .Build();

        state.RequireForUpdate(playerQuery);
        state.RequireForUpdate<EnemyShooterConfigElement>();
        state.RequireForUpdate<EnemyShooterRuntimeElement>();
        state.RequireForUpdate<ShootRequest>();
        state.RequireForUpdate<EnemyShooterControlState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        Entity playerEntity = Entity.Null;
        float3 playerPosition = float3.zero;

        foreach ((RefRO<LocalTransform> playerTransform,
                  Entity candidatePlayerEntity) in SystemAPI.Query<RefRO<LocalTransform>>()
                                                           .WithAll<PlayerControllerConfig>()
                                                           .WithEntityAccess())
        {
            playerEntity = candidatePlayerEntity;
            playerPosition = playerTransform.ValueRO.Position;
            break;
        }

        if (playerEntity == Entity.Null)
            return;

        if (!entityManager.Exists(playerEntity))
        {
            return;
        }

        float enemyTimeScale = 1f;

        if (SystemAPI.TryGetSingleton<EnemyGlobalTimeScale>(out EnemyGlobalTimeScale enemyGlobalTimeScale))
            enemyTimeScale = math.clamp(enemyGlobalTimeScale.Scale, 0f, 1f);

        float deltaTime = SystemAPI.Time.DeltaTime * enemyTimeScale;

        if (deltaTime <= 0f)
            return;

        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);

        foreach ((DynamicBuffer<EnemyShooterConfigElement> shooterConfigs,
                  DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                  DynamicBuffer<ShootRequest> shootRequests,
                  RefRW<EnemyShooterControlState> shooterControlState,
                  RefRO<EnemyRuntimeState> enemyRuntimeState,
                  RefRO<EnemyPatternRuntimeState> patternRuntimeState,
                  RefRO<LocalTransform> enemyTransform)
                 in SystemAPI.Query<DynamicBuffer<EnemyShooterConfigElement>,
                                    DynamicBuffer<EnemyShooterRuntimeElement>,
                                    DynamicBuffer<ShootRequest>,
                                    RefRW<EnemyShooterControlState>,
                                    RefRO<EnemyRuntimeState>,
                                    RefRO<EnemyPatternRuntimeState>,
                                    RefRO<LocalTransform>>()
                             .WithAll<EnemyActive>()
                             .WithNone<EnemyDespawnRequest, EnemySpawnInactivityLock>())
        {
            DynamicBuffer<EnemyShooterRuntimeElement> mutableShooterRuntime = shooterRuntime;
            DynamicBuffer<ShootRequest> mutableShootRequests = shootRequests;

            if (shooterConfigs.Length <= 0)
            {
                shooterControlState.ValueRW = new EnemyShooterControlState
                {
                    MovementLocked = 0
                };
                continue;
            }

            if (mutableShooterRuntime.Length != shooterConfigs.Length)
                SynchronizeShooterRuntime(mutableShooterRuntime, shooterConfigs.Length);

            float3 enemyPosition = enemyTransform.ValueRO.Position;
            float3 toPlayer = playerPosition - enemyPosition;
            toPlayer.y = 0f;
            float playerDistance = math.length(toPlayer);
            bool movementLocked = false;
            float3 resolvedAimDirection = float3.zero;
            bool hasResolvedAimDirection = false;
            int aimPriority = int.MinValue;

            for (int shooterIndex = 0; shooterIndex < shooterConfigs.Length; shooterIndex++)
            {
                EnemyShooterConfigElement shooterConfig = shooterConfigs[shooterIndex];
                EnemyShooterRuntimeElement runtime = mutableShooterRuntime[shooterIndex];

                runtime.NextBurstTimer = math.max(0f, runtime.NextBurstTimer - deltaTime);
                runtime.NextShotInBurstTimer = math.max(0f, runtime.NextShotInBurstTimer - deltaTime);
                runtime.PostFireStopTimer = math.max(0f, runtime.PostFireStopTimer - deltaTime);
                runtime.IsPlayerInRange = IsInRange(playerDistance,
                                                    in shooterConfig,
                                                    in enemyRuntimeState.ValueRO,
                                                    in patternRuntimeState.ValueRO) ? (byte)1 : (byte)0;

                if (runtime.IsPlayerInRange == 0)
                {
                    runtime.RemainingBurstShots = 0;
                    runtime.ShotsFiredInCurrentBurst = 0;
                    runtime.BurstWindupDurationSeconds = 0f;
                    runtime.NextShotInBurstTimer = 0f;
                    runtime.PostFireStopTimer = 0f;
                    runtime.HasLockedAimDirection = 0;
                    mutableShooterRuntime[shooterIndex] = runtime;
                    continue;
                }

                if (runtime.RemainingBurstShots <= 0 &&
                    runtime.NextBurstTimer <= 0f &&
                    runtime.PostFireStopTimer <= 0f)
                {
                    runtime.RemainingBurstShots = math.max(1, shooterConfig.BurstCount);
                    runtime.ShotsFiredInCurrentBurst = 0;
                    runtime.BurstWindupDurationSeconds = ResolveBurstWindupDuration(in shooterConfig);
                    runtime.NextShotInBurstTimer = runtime.BurstWindupDurationSeconds;
                    runtime.NextBurstTimer = math.max(0.01f, shooterConfig.FireInterval);

                    if (shooterConfig.AimPolicy == EnemyShooterAimPolicy.LockOnFireStart)
                    {
                        runtime.LockedAimDirection = math.normalizesafe(toPlayer, ResolveForward(enemyTransform.ValueRO.Rotation));
                        runtime.HasLockedAimDirection = 1;
                    }
                }

                if (ShouldLockShooterMovement(in shooterConfig, in runtime))
                {
                    movementLocked = true;
                }

                if (runtime.IsPlayerInRange != 0 &&
                    shooterConfig.ExclusiveLookDirectionControl != 0)
                {
                    float3 exclusiveAimDirection = ResolveAimDirection(in shooterConfig,
                                                                       in runtime,
                                                                       toPlayer,
                                                                       enemyTransform.ValueRO.Rotation);
                    TryCaptureAimDirection(exclusiveAimDirection,
                                           shooterConfig.MovementPolicy,
                                           true,
                                           ref resolvedAimDirection,
                                           ref hasResolvedAimDirection,
                                           ref aimPriority);
                }

                if (runtime.RemainingBurstShots > 0)
                {
                    float3 activeAimDirection = ResolveAimDirection(in shooterConfig,
                                                                    in runtime,
                                                                    toPlayer,
                                                                    enemyTransform.ValueRO.Rotation);
                    TryCaptureAimDirection(activeAimDirection,
                                           shooterConfig.MovementPolicy,
                                           false,
                                           ref resolvedAimDirection,
                                           ref hasResolvedAimDirection,
                                           ref aimPriority);
                }

                if (runtime.RemainingBurstShots > 0 && runtime.NextShotInBurstTimer <= 0f)
                {
                    float3 aimDirection = ResolveAimDirection(in shooterConfig,
                                                              in runtime,
                                                              toPlayer,
                                                              enemyTransform.ValueRO.Rotation);

                    EnqueueShotRequests(mutableShootRequests,
                                        enemyPosition,
                                        aimDirection,
                                        in shooterConfig);

                    if (canEnqueueAudioRequests)
                        GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.EnemyShootProjectile, enemyPosition);

                    runtime.RemainingBurstShots -= 1;
                    runtime.ShotsFiredInCurrentBurst += 1;

                    if (runtime.RemainingBurstShots > 0)
                    {
                        runtime.NextShotInBurstTimer = math.max(0f, shooterConfig.IntraBurstDelay);
                    }
                    else
                    {
                        runtime.NextShotInBurstTimer = 0f;
                        runtime.ShotsFiredInCurrentBurst = 0;
                        runtime.BurstWindupDurationSeconds = 0f;
                        runtime.PostFireStopTimer = ResolvePostFireStopDuration(in shooterConfig);
                        runtime.HasLockedAimDirection = 0;
                    }
                }

                mutableShooterRuntime[shooterIndex] = runtime;
            }

            shooterControlState.ValueRW = new EnemyShooterControlState
            {
                MovementLocked = movementLocked ? (byte)1 : (byte)0,
                AimDirection = hasResolvedAimDirection ? resolvedAimDirection : float3.zero,
                HasAimDirection = hasResolvedAimDirection ? (byte)1 : (byte)0
            };
        }
    }
    #endregion

    #region Helpers
    private static void SynchronizeShooterRuntime(DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime, int count)
    {
        shooterRuntime.Clear();

        for (int index = 0; index < count; index++)
        {
            shooterRuntime.Add(new EnemyShooterRuntimeElement
            {
                NextBurstTimer = 0f,
                NextShotInBurstTimer = 0f,
                PostFireStopTimer = 0f,
                RemainingBurstShots = 0,
                ShotsFiredInCurrentBurst = 0,
                BurstWindupDurationSeconds = 0f,
                IsPlayerInRange = 0,
                LockedAimDirection = float3.zero,
                HasLockedAimDirection = 0
            });
        }
    }

    private static bool IsInRange(float playerDistance,
                                  in EnemyShooterConfigElement shooterConfig,
                                  in EnemyRuntimeState enemyRuntimeState,
                                  in EnemyPatternRuntimeState patternRuntimeState)
    {
        if (shooterConfig.UseMinimumRange != 0 && playerDistance < math.max(0f, shooterConfig.MinimumRange))
            return false;

        if (shooterConfig.UseMaximumRange != 0 && playerDistance > math.max(0f, shooterConfig.MaximumRange))
            return false;

        if (!AreActivationGatesValid(in shooterConfig, in enemyRuntimeState, in patternRuntimeState))
            return false;

        return true;
    }

    /// <summary>
    /// Resolves whether one Shooter module currently requires movement to remain stopped.
    /// /params shooterConfig Shooter configuration that declares the movement policy.
    /// /params runtime Mutable Shooter timing state for active and post-fire phases.
    /// /returns True when the enemy should hold position for this module.
    /// </summary>
    private static bool ShouldLockShooterMovement(in EnemyShooterConfigElement shooterConfig,
                                                  in EnemyShooterRuntimeElement runtime)
    {
        if (shooterConfig.MovementPolicy != EnemyShooterMovementPolicy.StopWhileAiming)
            return false;

        return runtime.RemainingBurstShots > 0 || runtime.PostFireStopTimer > 0f;
    }

    /// <summary>
    /// Resolves the first-shot delay for a burst, including stop-before-fire timing only when movement locking is enabled.
    /// /params shooterConfig Shooter config containing windup and stop timing values.
    /// /returns Seconds to wait before the first shot in the burst.
    /// </summary>
    private static float ResolveBurstWindupDuration(in EnemyShooterConfigElement shooterConfig)
    {
        float aimWindupSeconds = math.max(0f, shooterConfig.AimWindupSeconds);

        if (shooterConfig.MovementPolicy != EnemyShooterMovementPolicy.StopWhileAiming)
            return aimWindupSeconds;

        return math.max(aimWindupSeconds, math.max(0f, shooterConfig.PreFireStopSeconds));
    }

    /// <summary>
    /// Resolves post-fire stop timing only for Shooter modules that explicitly lock movement while aiming.
    /// /params shooterConfig Shooter config containing movement policy and post-fire stop timing.
    /// /returns Seconds to keep movement locked after the final shot.
    /// </summary>
    private static float ResolvePostFireStopDuration(in EnemyShooterConfigElement shooterConfig)
    {
        if (shooterConfig.MovementPolicy != EnemyShooterMovementPolicy.StopWhileAiming)
            return 0f;

        return math.max(0f, shooterConfig.PostFireStopSeconds);
    }

    /// <summary>
    /// Evaluates optional activation gates beyond distance checks for one Weapon Interaction.
    /// /params shooterConfig Shooter config containing gate flags.
    /// /params enemyRuntimeState Enemy runtime state used for speed and damage checks.
    /// /params patternRuntimeState Pattern runtime state used for Wanderer wait checks.
    /// /returns True when every configured gate is satisfied.
    /// </summary>
    private static bool AreActivationGatesValid(in EnemyShooterConfigElement shooterConfig,
                                                in EnemyRuntimeState enemyRuntimeState,
                                                in EnemyPatternRuntimeState patternRuntimeState)
    {
        EnemyWeaponInteractionActivationGate gates = shooterConfig.ActivationGates;

        if (gates == EnemyWeaponInteractionActivationGate.Always)
            return true;

        if ((gates & EnemyWeaponInteractionActivationGate.RequireBelowSpeed) != 0)
        {
            float3 planarVelocity = enemyRuntimeState.Velocity;
            planarVelocity.y = 0f;

            if (math.length(planarVelocity) > math.max(0f, shooterConfig.MaximumActivationSpeed))
                return false;
        }

        if ((gates & EnemyWeaponInteractionActivationGate.RequireRecentlyDamaged) != 0)
        {
            float damageAge = enemyRuntimeState.LifetimeSeconds - enemyRuntimeState.LastDamageLifetimeSeconds;

            if (enemyRuntimeState.HasTakenDamage == 0 ||
                damageAge > math.max(0f, shooterConfig.RecentlyDamagedWindowSeconds))
                return false;
        }

        if ((gates & EnemyWeaponInteractionActivationGate.RequireWandererWait) != 0 &&
            patternRuntimeState.WanderWaitTimer <= 0f)
            return false;

        return true;
    }

    private static float3 ResolveAimDirection(in EnemyShooterConfigElement shooterConfig,
                                              in EnemyShooterRuntimeElement runtime,
                                              float3 toPlayer,
                                              quaternion enemyRotation)
    {
        float3 fallbackDirection = ResolveForward(enemyRotation);

        switch (shooterConfig.AimPolicy)
        {
            case EnemyShooterAimPolicy.LockOnFireStart:
                if (runtime.HasLockedAimDirection != 0)
                    return math.normalizesafe(runtime.LockedAimDirection, fallbackDirection);

                return math.normalizesafe(toPlayer, fallbackDirection);

            default:
                return math.normalizesafe(toPlayer, fallbackDirection);
        }
    }

    private static float3 ResolveForward(quaternion rotation)
    {
        float3 forward = math.forward(rotation);
        forward.y = 0f;
        return math.normalizesafe(forward, ForwardAxis);
    }

    /// <summary>
    /// Captures the best current aim direction used to orient the shooter visuals before projectile spawn.
    /// </summary>
    /// <param name="candidateDirection">Current shooter aim direction candidate.</param>
    /// <param name="movementPolicy">Movement policy associated with the current shooter module.</param>
    /// <param name="exclusiveLookDirectionControl">Whether the current module must override any movement-facing fallback.</param>
    /// <param name="resolvedAimDirection">Best resolved aim direction retained across modules.</param>
    /// <param name="hasResolvedAimDirection">Whether a valid aim direction has already been captured.</param>
    /// <param name="aimPriority">Priority of the currently captured aim direction.</param>
    /// <returns>None.<returns>
    private static void TryCaptureAimDirection(float3 candidateDirection,
                                               EnemyShooterMovementPolicy movementPolicy,
                                               bool exclusiveLookDirectionControl,
                                               ref float3 resolvedAimDirection,
                                               ref bool hasResolvedAimDirection,
                                               ref int aimPriority)
    {
        if (math.lengthsq(candidateDirection) <= DirectionEpsilon)
            return;

        int candidatePriority = exclusiveLookDirectionControl
            ? 2
            : movementPolicy == EnemyShooterMovementPolicy.StopWhileAiming ? 1 : 0;

        if (hasResolvedAimDirection && candidatePriority < aimPriority)
            return;

        resolvedAimDirection = math.normalizesafe(candidateDirection, ForwardAxis);
        hasResolvedAimDirection = true;
        aimPriority = candidatePriority;
    }

    private static void EnqueueShotRequests(DynamicBuffer<ShootRequest> shootRequests,
                                            float3 shooterPosition,
                                            float3 baseDirection,
                                            in EnemyShooterConfigElement shooterConfig)
    {
        switch (shooterConfig.ShotPattern)
        {
            case EnemyShooterShotPattern.RadialBurst:
                EnqueueRadialBurstRequests(shootRequests,
                                           shooterPosition,
                                           baseDirection,
                                           in shooterConfig);
                return;

            default:
                EnqueueForwardSpreadRequests(shootRequests,
                                             shooterPosition,
                                             baseDirection,
                                             in shooterConfig);
                return;
        }
    }

    /// <summary>
    /// Enqueues the default target-facing spread shot pattern.
    /// /params shootRequests Buffer receiving projectile spawn requests.
    /// /params shooterPosition World position of the shooter.
    /// /params baseDirection Target-facing aim direction.
    /// /params shooterConfig Shooter projectile payload.
    /// /returns None.
    /// </summary>
    private static void EnqueueForwardSpreadRequests(DynamicBuffer<ShootRequest> shootRequests,
                                                     float3 shooterPosition,
                                                     float3 baseDirection,
                                                     in EnemyShooterConfigElement shooterConfig)
    {
        int projectilesPerShot = math.max(1, shooterConfig.ProjectilesPerShot);

        if (projectilesPerShot <= 1)
        {
            AddShootRequest(shootRequests,
                            shooterPosition,
                            baseDirection,
                            in shooterConfig);
            return;
        }

        float spread = math.max(0f, shooterConfig.SpreadAngleDegrees);
        float step = projectilesPerShot > 1 ? spread / (projectilesPerShot - 1) : 0f;
        float startOffset = -spread * 0.5f;

        for (int projectileIndex = 0; projectileIndex < projectilesPerShot; projectileIndex++)
        {
            float angleOffset = startOffset + step * projectileIndex;
            float3 direction = RotateDirectionByDegrees(baseDirection, angleOffset);
            AddShootRequest(shootRequests,
                            shooterPosition,
                            direction,
                            in shooterConfig);
        }
    }

    /// <summary>
    /// Enqueues a full-circle projectile burst evenly distributed around the shooter.
    /// /params shootRequests Buffer receiving projectile spawn requests.
    /// /params shooterPosition World position of the shooter.
    /// /params baseDirection First projectile direction, usually aimed at the player.
    /// /params shooterConfig Shooter projectile payload.
    /// /returns None.
    /// </summary>
    private static void EnqueueRadialBurstRequests(DynamicBuffer<ShootRequest> shootRequests,
                                                   float3 shooterPosition,
                                                   float3 baseDirection,
                                                   in EnemyShooterConfigElement shooterConfig)
    {
        int projectilesPerShot = math.max(1, shooterConfig.ProjectilesPerShot);
        float angleStep = 360f / projectilesPerShot;

        for (int projectileIndex = 0; projectileIndex < projectilesPerShot; projectileIndex++)
        {
            float3 direction = RotateDirectionByDegrees(baseDirection, angleStep * projectileIndex);
            AddShootRequest(shootRequests,
                            shooterPosition,
                            direction,
                            in shooterConfig);
        }
    }

    private static float3 RotateDirectionByDegrees(float3 direction, float angleDegrees)
    {
        float radians = math.radians(angleDegrees);
        float sine = math.sin(radians);
        float cosine = math.cos(radians);
        float3 normalizedDirection = math.normalizesafe(direction, ForwardAxis);
        float x = normalizedDirection.x * cosine - normalizedDirection.z * sine;
        float z = normalizedDirection.x * sine + normalizedDirection.z * cosine;
        return math.normalizesafe(new float3(x, 0f, z), ForwardAxis);
    }

    private static void AddShootRequest(DynamicBuffer<ShootRequest> shootRequests,
                                        float3 shooterPosition,
                                        float3 direction,
                                        in EnemyShooterConfigElement shooterConfig)
    {
        float3 spawnOffset = math.normalizesafe(direction, ForwardAxis) * 0.35f;

        shootRequests.Add(new ShootRequest
        {
            Position = shooterPosition + spawnOffset,
            Direction = math.normalizesafe(direction, ForwardAxis),
            Speed = math.max(0f, shooterConfig.ProjectileSpeed),
            ExplosionRadius = math.max(0f, shooterConfig.ProjectileExplosionRadius),
            Range = math.max(0f, shooterConfig.ProjectileRange),
            Lifetime = math.max(0f, shooterConfig.ProjectileLifetime),
            Damage = math.max(0f, shooterConfig.ProjectileDamage),
            ProjectileScaleMultiplier = math.max(0.01f, shooterConfig.ProjectileScaleMultiplier),
            PenetrationMode = shooterConfig.PenetrationMode,
            MaxPenetrations = math.max(0, shooterConfig.MaxPenetrations),
            InheritPlayerSpeed = shooterConfig.InheritShooterSpeed,
            IsSplitChild = 0,
            ElementalPayloadOverride = shooterConfig.HasElementalPayload != 0
                ? ProjectileElementalPayloadUtility.BuildSingle(in shooterConfig.ElementalEffect,
                                                                math.max(0f, shooterConfig.ElementalStacksPerHit))
                : default
        });
    }
    #endregion

    #endregion
}
