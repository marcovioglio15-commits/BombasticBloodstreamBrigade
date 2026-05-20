using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Resolves Bombardier module runtime and enqueues bomb launch requests for enemy entities.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyShooterRequestSystem))]
[UpdateBefore(typeof(EnemySteeringSystem))]
[UpdateBefore(typeof(EnemyPatternMovementSystem))]
public partial struct EnemyBombardierRequestSystem : ISystem
{
    #region Fields
    private EntityQuery playerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Caches the player query and declares Bombardier runtime buffers as update requirements.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig, LocalTransform>()
            .Build();

        state.RequireForUpdate(playerQuery);
        state.RequireForUpdate<EnemyBombardierConfigElement>();
        state.RequireForUpdate<EnemyBombardierRuntimeElement>();
        state.RequireForUpdate<EnemyBombardierLaunchRequest>();
        state.RequireForUpdate<EnemyShooterControlState>();
    }

    /// <summary>
    /// Advances Bombardier cadence state and emits launch requests when each module commits a bomb group.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        bool hasPlayer = false;
        Entity playerEntity = Entity.Null;
        float3 playerPosition = float3.zero;

        foreach ((RefRO<LocalTransform> playerTransform,
                  Entity candidatePlayerEntity) in SystemAPI.Query<RefRO<LocalTransform>>()
                                                           .WithAll<PlayerControllerConfig>()
                                                           .WithEntityAccess())
        {
            playerEntity = candidatePlayerEntity;
            playerPosition = playerTransform.ValueRO.Position;
            hasPlayer = true;
            break;
        }

        if (!hasPlayer)
            return;

        if (!state.EntityManager.Exists(playerEntity))
            return;

        float enemyTimeScale = 1f;

        if (SystemAPI.TryGetSingleton<EnemyGlobalTimeScale>(out EnemyGlobalTimeScale enemyGlobalTimeScale))
            enemyTimeScale = math.clamp(enemyGlobalTimeScale.Scale, 0f, 1f);

        float deltaTime = SystemAPI.Time.DeltaTime * enemyTimeScale;

        if (deltaTime <= 0f)
            return;

        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);
        BufferLookup<EnemyShooterConfigElement> shooterConfigLookup = SystemAPI.GetBufferLookup<EnemyShooterConfigElement>(true);

        foreach ((DynamicBuffer<EnemyBombardierConfigElement> bombardierConfigs,
                  DynamicBuffer<EnemyBombardierRuntimeElement> bombardierRuntime,
                  DynamicBuffer<EnemyBombardierLaunchRequest> launchRequests,
                  RefRW<EnemyShooterControlState> shooterControlState,
                  RefRO<EnemyRuntimeState> enemyRuntimeState,
                  RefRO<EnemyPatternRuntimeState> patternRuntimeState,
                  RefRO<LocalTransform> enemyTransform,
                  Entity enemyEntity)
                 in SystemAPI.Query<DynamicBuffer<EnemyBombardierConfigElement>,
                                    DynamicBuffer<EnemyBombardierRuntimeElement>,
                                    DynamicBuffer<EnemyBombardierLaunchRequest>,
                                    RefRW<EnemyShooterControlState>,
                                    RefRO<EnemyRuntimeState>,
                                    RefRO<EnemyPatternRuntimeState>,
                                    RefRO<LocalTransform>>()
                             .WithAll<EnemyActive>()
                             .WithNone<EnemyDespawnRequest, EnemySpawnInactivityLock>()
                             .WithEntityAccess())
        {
            DynamicBuffer<EnemyBombardierRuntimeElement> mutableBombardierRuntime = bombardierRuntime;
            DynamicBuffer<EnemyBombardierLaunchRequest> mutableLaunchRequests = launchRequests;

            if (bombardierConfigs.Length <= 0)
            {
                int shooterConfigCount = shooterConfigLookup.HasBuffer(enemyEntity)
                    ? shooterConfigLookup[enemyEntity].Length
                    : 0;

                if (shooterConfigCount <= 0)
                {
                    shooterControlState.ValueRW = new EnemyShooterControlState
                    {
                        MovementLocked = 0
                    };
                }

                continue;
            }

            if (mutableBombardierRuntime.Length != bombardierConfigs.Length)
                EnemyBombardierRequestUtility.SynchronizeBombardierRuntime(mutableBombardierRuntime, bombardierConfigs.Length, enemyEntity);

            float3 enemyPosition = enemyTransform.ValueRO.Position;
            float3 toPlayer = playerPosition - enemyPosition;
            toPlayer.y = 0f;
            float playerDistance = math.length(toPlayer);
            EnemyShooterControlState previousControlState = shooterControlState.ValueRO;
            bool movementLocked = previousControlState.MovementLocked != 0;
            float3 resolvedAimDirection = previousControlState.HasAimDirection != 0 ? previousControlState.AimDirection : float3.zero;
            bool hasResolvedAimDirection = previousControlState.HasAimDirection != 0;
            int aimPriority = hasResolvedAimDirection ? 0 : int.MinValue;

            for (int bombardierIndex = 0; bombardierIndex < bombardierConfigs.Length; bombardierIndex++)
            {
                EnemyBombardierConfigElement bombardierConfig = bombardierConfigs[bombardierIndex];
                EnemyBombardierRuntimeElement runtime = mutableBombardierRuntime[bombardierIndex];
                Random random = EnemyBombardierRequestUtility.CreateRandom(ref runtime, enemyEntity, bombardierIndex);

                EnemyBombardierRequestUtility.AdvanceRuntimeTimers(ref runtime, deltaTime);
                runtime.IsPlayerInReach = EnemyBombardierRequestUtility.IsPlayerInReach(playerDistance, in bombardierConfig) ? (byte)1 : (byte)0;
                EnemyBombardierTargetingMode targetingMode = EnemyBombardierRequestUtility.ResolveRuntimeTargetingMode(in bombardierConfig, in runtime);
                runtime.IsLaunchAllowed = targetingMode != EnemyBombardierTargetingMode.Disabled &&
                                          EnemyBombardierRequestUtility.AreActivationGatesValid(in bombardierConfig,
                                                                                                in enemyRuntimeState.ValueRO,
                                                                                                in patternRuntimeState.ValueRO)
                    ? (byte)1
                    : (byte)0;

                if (runtime.IsLaunchAllowed == 0)
                {
                    EnemyBombardierRequestUtility.CancelActiveBurst(ref runtime);
                    runtime.RandomState = random.state;
                    mutableBombardierRuntime[bombardierIndex] = runtime;
                    continue;
                }

                if (runtime.RemainingBurstLaunches <= 0 &&
                    runtime.NextBurstTimer <= 0f &&
                    runtime.PostLaunchStopTimer <= 0f)
                {
                    EnemyBombardierRequestUtility.StartBurst(ref runtime,
                                                             in bombardierConfig,
                                                             targetingMode,
                                                             enemyPosition,
                                                             playerPosition,
                                                             ref random);
                }

                if (EnemyBombardierRequestUtility.ShouldLockMovement(in bombardierConfig, in runtime))
                    movementLocked = true;

                if (EnemyBombardierRequestUtility.TryResolveAimDirection(in runtime,
                                                                         enemyPosition,
                                                                         playerPosition,
                                                                         enemyTransform.ValueRO.Rotation,
                                                                         out float3 aimDirection))
                {
                    if (bombardierConfig.ExclusiveLookDirectionControl != 0)
                    {
                        EnemyBombardierRequestUtility.TryCaptureAimDirection(aimDirection,
                                                                             bombardierConfig.MovementPolicy,
                                                                             true,
                                                                             ref resolvedAimDirection,
                                                                             ref hasResolvedAimDirection,
                                                                             ref aimPriority);
                    }

                    if (runtime.RemainingBurstLaunches > 0)
                    {
                        EnemyBombardierRequestUtility.TryCaptureAimDirection(aimDirection,
                                                                             bombardierConfig.MovementPolicy,
                                                                             false,
                                                                             ref resolvedAimDirection,
                                                                             ref hasResolvedAimDirection,
                                                                             ref aimPriority);
                    }
                }

                if (runtime.RemainingBurstLaunches > 0 && runtime.NextBombInBurstTimer <= 0f)
                {
                    float3 targetPosition = EnemyBombardierRequestUtility.ResolveCurrentTargetPosition(in runtime,
                                                                                                       in bombardierConfig,
                                                                                                       targetingMode,
                                                                                                       enemyPosition,
                                                                                                       playerPosition,
                                                                                                       ref random);
                    EnemyBombardierRequestUtility.EnqueueLaunchRequests(mutableLaunchRequests,
                                                                       enemyEntity,
                                                                       enemyPosition,
                                                                       targetPosition,
                                                                       in bombardierConfig,
                                                                       ref random);

                    if (canEnqueueAudioRequests)
                        GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.EnemyShootProjectile, enemyPosition);

                    EnemyBombardierRequestUtility.CompleteLaunch(ref runtime, in bombardierConfig);
                }

                runtime.RandomState = random.state;
                mutableBombardierRuntime[bombardierIndex] = runtime;
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

    #endregion
}
