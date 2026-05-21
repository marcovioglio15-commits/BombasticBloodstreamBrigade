using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// Maintains boss-owned minion pools and activates normal enemy minions from boss spawn rules.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyBossPatternRuntimeSystem))]
[UpdateBefore(typeof(EnemyShooterRequestSystem))]
public partial struct EnemyBossMinionSpawnSystem : ISystem
{
    #region Nested Types
    /// <summary>
    /// Stores one pool initialization request collected while iterating boss entities.
    /// </summary>
    private struct RuleInitializationRequest
    {
        public Entity BossEntity;
        public int RuleIndex;
        public EnemyBossMinionSpawnElement Rule;
    }

    /// <summary>
    /// Stores one minion spawn request collected while iterating boss entities.
    /// </summary>
    private struct MinionSpawnRequest
    {
        public Entity BossEntity;
        public int RuleIndex;
        public float3 BossPosition;
        public EnemyBossMinionSpawnElement Rule;
    }

    /// <summary>
    /// Stores one pending minion activation collected before structural changes are applied.
    /// </summary>
    private struct PendingMinionActivationRequest
    {
        public Entity BossEntity;
        public EnemyBossPendingMinionSpawnElement PendingSpawn;
    }

    /// <summary>
    /// Identifies one boss minion rule for active and warning-pending count throttling.
    /// </summary>
    private struct BossMinionRuleKey : System.IEquatable<BossMinionRuleKey>
    {
        public Entity BossEntity;
        public int RuleIndex;

        /// <summary>
        /// Compares two boss rule keys using their owning boss entity and rule index.
        /// </summary>
        /// <param name="other">Key to compare against.</param>
        /// <returns>True when both keys address the same boss rule.</returns>
        public bool Equals(BossMinionRuleKey other)
        {
            return BossEntity == other.BossEntity && RuleIndex == other.RuleIndex;
        }

        /// <summary>
        /// Builds a stable hash for use in native hash maps during the current frame.
        /// </summary>
        /// <returns>Hash code derived from entity identity and rule index.</returns>
        public override int GetHashCode()
        {
            return (int)math.hash(new int3(BossEntity.Index, BossEntity.Version, RuleIndex));
        }
    }
    #endregion

    #region Fields
    private EntityQuery activeMinionQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares boss minion spawn buffers as runtime dependencies.
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnCreate(ref SystemState state)
    {
        activeMinionQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyBossMinionOwner, EnemyActive>()
            .Build();
        state.RequireForUpdate<EnemyBossMinionSpawnElement>();
    }

    /// <summary>
    /// Initializes missing pools and evaluates minion spawn triggers.
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        Allocator frameAllocator = state.WorldUpdateAllocator;
        NativeList<RuleInitializationRequest> initializationRequests = new NativeList<RuleInitializationRequest>(frameAllocator);
        NativeList<MinionSpawnRequest> spawnRequests = new NativeList<MinionSpawnRequest>(frameAllocator);
        NativeArray<EnemyNavigationCellElement> navigationCellSnapshot = default;
        PhysicsWorldSingleton physicsWorldSingleton = default;
        bool hasPhysicsWorld = SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out physicsWorldSingleton);
        int wallsLayerMask = WorldWallCollisionUtility.ResolveWallsLayerMask();
        EnemyNavigationGridState navigationGridState = default;
        bool navigationReady = false;
        bool hasReferenceSpawnPlaneHeight = false;
        float referenceSpawnPlaneHeight = 0f;

        if (SystemAPI.TryGetSingletonEntity<PlayerControllerConfig>(out Entity playerEntity) &&
            entityManager.HasComponent<LocalTransform>(playerEntity))
        {
            referenceSpawnPlaneHeight = entityManager.GetComponentData<LocalTransform>(playerEntity).Position.y;
            hasReferenceSpawnPlaneHeight = true;
        }

        ProcessPendingMinionActivations(ref state, elapsedTime);
        int aliveMinionCountCapacity = ResolveAliveMinionCountCapacity(ref state);
        NativeParallelHashMap<BossMinionRuleKey, int> aliveMinionCounts = new NativeParallelHashMap<BossMinionRuleKey, int>(aliveMinionCountCapacity, frameAllocator);

        if (SystemAPI.TryGetSingleton<PlayerWorldLayersConfig>(out PlayerWorldLayersConfig worldLayersConfig) &&
            worldLayersConfig.WallsLayerMask != 0)
        {
            wallsLayerMask = worldLayersConfig.WallsLayerMask;
        }

        BuildAliveMinionCounts(ref state, ref aliveMinionCounts);

        foreach ((RefRO<EnemyHealth> bossHealth,
                  RefRO<EnemyRuntimeState> bossRuntime,
                  RefRO<LocalTransform> bossTransform,
                  Entity bossEntity)
                 in SystemAPI.Query<RefRO<EnemyHealth>,
                                    RefRO<EnemyRuntimeState>,
                                    RefRO<LocalTransform>>()
                             .WithAll<EnemyBossTag, EnemyActive>()
                             .WithNone<EnemyDespawnRequest>()
                             .WithEntityAccess())
        {
            DynamicBuffer<EnemyBossMinionSpawnElement> minionRules = entityManager.GetBuffer<EnemyBossMinionSpawnElement>(bossEntity);

            for (int ruleIndex = 0; ruleIndex < minionRules.Length; ruleIndex++)
            {
                EnemyBossMinionSpawnElement rule = minionRules[ruleIndex];

                if (rule.Initialized == 0)
                {
                    QueueRuleInitialization(initializationRequests,
                                            bossEntity,
                                            ruleIndex,
                                            ref rule,
                                            in bossRuntime.ValueRO,
                                            elapsedTime);
                    minionRules[ruleIndex] = rule;
                    continue;
                }

                int aliveMinionCount = ResolveAliveMinionCount(in aliveMinionCounts, bossEntity, ruleIndex);

                if (EnemyBossMinionSpawnTriggerUtility.ShouldTriggerRule(aliveMinionCount,
                                                                         ref rule,
                                                                         in bossHealth.ValueRO,
                                                                         in bossRuntime.ValueRO,
                                                                         elapsedTime))
                {
                    EnemyBossMinionSpawnTriggerUtility.MarkRuleTriggered(ref rule, in bossRuntime.ValueRO, elapsedTime);
                    QueueMinionSpawn(spawnRequests,
                                     bossEntity,
                                     ruleIndex,
                                     bossTransform.ValueRO.Position,
                                     in rule);
                }

                minionRules[ruleIndex] = rule;
            }
        }

        ProcessRuleInitializationRequests(entityManager, initializationRequests);

        if (spawnRequests.Length > 0 &&
            SystemAPI.TryGetSingleton<EnemyNavigationGridState>(out navigationGridState) &&
            SystemAPI.TryGetSingletonBuffer<EnemyNavigationCellElement>(out DynamicBuffer<EnemyNavigationCellElement> navigationCells) &&
            navigationGridState.Initialized != 0 &&
            navigationGridState.FlowReady != 0 &&
            navigationCells.Length > 0)
        {
            navigationCellSnapshot = CollectionHelper.CreateNativeArray(navigationCells.AsNativeArray(), frameAllocator);
            navigationReady = navigationCellSnapshot.IsCreated && navigationCellSnapshot.Length > 0;
        }

        ProcessMinionSpawnRequests(entityManager,
                                   spawnRequests,
                                   in aliveMinionCounts,
                                   elapsedTime,
                                   hasPhysicsWorld,
                                   in physicsWorldSingleton,
                                   wallsLayerMask,
                                   navigationReady,
                                   in navigationGridState,
                                   navigationCellSnapshot,
                                   hasReferenceSpawnPlaneHeight,
                                   referenceSpawnPlaneHeight);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Activates reserved minions whose warning lead time has completed, or recycles them if the source boss is no longer valid.
    /// </summary>
    /// <param name="state">Mutable system state used to query pending buffers.</param>
    /// <param name="elapsedTime">Current world elapsed time.</param>
    private void ProcessPendingMinionActivations(ref SystemState state, float elapsedTime)
    {
        EntityManager entityManager = state.EntityManager;
        NativeList<PendingMinionActivationRequest> activationRequests = new NativeList<PendingMinionActivationRequest>(state.WorldUpdateAllocator);

        // Collect expired pending entries first so entity structural changes do not invalidate the queried buffers.
        foreach ((DynamicBuffer<EnemyBossPendingMinionSpawnElement> pendingSpawns,
                  Entity bossEntity) in SystemAPI.Query<DynamicBuffer<EnemyBossPendingMinionSpawnElement>>()
                                                 .WithEntityAccess())
        {
            for (int pendingIndex = pendingSpawns.Length - 1; pendingIndex >= 0; pendingIndex--)
            {
                EnemyBossPendingMinionSpawnElement pendingSpawn = pendingSpawns[pendingIndex];

                if (elapsedTime < pendingSpawn.ActivationTime)
                    continue;

                pendingSpawns.RemoveAt(pendingIndex);
                activationRequests.Add(new PendingMinionActivationRequest
                {
                    BossEntity = bossEntity,
                    PendingSpawn = pendingSpawn
                });
            }
        }

        // Apply activations after all buffers have been released.
        for (int requestIndex = 0; requestIndex < activationRequests.Length; requestIndex++)
        {
            PendingMinionActivationRequest request = activationRequests[requestIndex];

            if (!EnemyBossMinionPendingSpawnUtility.CanActivatePendingSpawn(entityManager, request.BossEntity, in request.PendingSpawn))
            {
                EnemyBossMinionPendingSpawnUtility.RecyclePendingSpawn(entityManager, in request.PendingSpawn);
                continue;
            }

            EnemyPoolUtility.ActivateReservedEnemy(entityManager,
                                                   request.PendingSpawn.MinionEntity,
                                                   request.PendingSpawn.SpawnPosition);
        }
    }

    /// <summary>
    /// Resolves a safe hash-map capacity that can hold active and warning-pending minion rule counters.
    /// </summary>
    /// <param name="state">Mutable system state required by SystemAPI query generation.</param>
    /// <returns>Capacity used by the per-rule minion count map.</returns>
    private int ResolveAliveMinionCountCapacity(ref SystemState state)
    {
        int capacity = math.max(1, activeMinionQuery.CalculateEntityCount());

        foreach (DynamicBuffer<EnemyBossPendingMinionSpawnElement> pendingSpawns in SystemAPI.Query<DynamicBuffer<EnemyBossPendingMinionSpawnElement>>())
            capacity += pendingSpawns.Length;

        return math.max(1, capacity);
    }

    /// <summary>
    /// Builds the current per-rule minion counts from active minions and still-pending spawn reservations.
    /// </summary>
    /// <param name="state">Mutable system state required by SystemAPI query generation.</param>
    /// <param name="aliveMinionCounts">Mutable count map filled during the current frame.</param>
    private void BuildAliveMinionCounts(ref SystemState state,
                                        ref NativeParallelHashMap<BossMinionRuleKey, int> aliveMinionCounts)
    {
        foreach ((RefRO<EnemyBossMinionOwner> owner,
                  EnabledRefRO<EnemyActive> enemyActive)
                 in SystemAPI.Query<RefRO<EnemyBossMinionOwner>, EnabledRefRO<EnemyActive>>()
                             .WithAll<EnemyActive>())
        {
            if (!enemyActive.ValueRO)
                continue;

            BossMinionRuleKey key = new BossMinionRuleKey
            {
                BossEntity = owner.ValueRO.BossEntity,
                RuleIndex = owner.ValueRO.RuleIndex
            };

            IncrementAliveMinionCount(ref aliveMinionCounts, in key);
        }

        foreach ((DynamicBuffer<EnemyBossPendingMinionSpawnElement> pendingSpawns,
                  Entity bossEntity) in SystemAPI.Query<DynamicBuffer<EnemyBossPendingMinionSpawnElement>>()
                                                 .WithEntityAccess())
        {
            for (int pendingIndex = 0; pendingIndex < pendingSpawns.Length; pendingIndex++)
            {
                BossMinionRuleKey key = new BossMinionRuleKey
                {
                    BossEntity = bossEntity,
                    RuleIndex = pendingSpawns[pendingIndex].RuleIndex
                };

                IncrementAliveMinionCount(ref aliveMinionCounts, in key);
            }
        }
    }

    /// <summary>
    /// Adds one active or pending minion to the per-rule count map.
    /// </summary>
    /// <param name="aliveMinionCounts">Mutable count map.</param>
    /// <param name="key">Boss and rule key to increment.</param>
    private static void IncrementAliveMinionCount(ref NativeParallelHashMap<BossMinionRuleKey, int> aliveMinionCounts,
                                                 in BossMinionRuleKey key)
    {
        if (aliveMinionCounts.TryGetValue(key, out int count))
        {
            aliveMinionCounts[key] = count + 1;
            return;
        }

        aliveMinionCounts.Add(key, 1);
    }

    /// <summary>
    /// Reads the current active-or-pending minion count for one boss rule.
    /// </summary>
    /// <param name="aliveMinionCounts">Count map built for the current frame.</param>
    /// <param name="bossEntity">Boss that owns the rule.</param>
    /// <param name="ruleIndex">Rule index to inspect.</param>
    /// <returns>Current minion count, or zero when no entry exists.</returns>
    private static int ResolveAliveMinionCount(in NativeParallelHashMap<BossMinionRuleKey, int> aliveMinionCounts,
                                               Entity bossEntity,
                                               int ruleIndex)
    {
        BossMinionRuleKey key = new BossMinionRuleKey
        {
            BossEntity = bossEntity,
            RuleIndex = ruleIndex
        };

        return aliveMinionCounts.TryGetValue(key, out int count) ? count : 0;
    }

    /// <summary>
    /// Queues pool initialization for one boss minion rule without performing structural changes during query iteration.
    /// </summary>
    /// <param name="initializationRequests">Request list filled by the current update.</param>
    /// <param name="bossEntity">Boss that owns the rule.</param>
    /// <param name="ruleIndex">Rule index on the boss buffer.</param>
    /// <param name="rule">Mutable rule state.</param>
    /// <param name="bossRuntime">Boss runtime state used by damage-trigger cooldowns.</param>
    /// <param name="elapsedTime">Current world elapsed time.</param>
    private static void QueueRuleInitialization(NativeList<RuleInitializationRequest> initializationRequests,
                                                Entity bossEntity,
                                                int ruleIndex,
                                                ref EnemyBossMinionSpawnElement rule,
                                                in EnemyRuntimeState bossRuntime,
                                                float elapsedTime)
    {
        rule.Initialized = 1;
        rule.NextSpawnTime = EnemyBossMinionSpawnTriggerUtility.ResolveInitialNextSpawnTime(in rule, in bossRuntime, elapsedTime);
        rule.LastObservedDamageLifetimeSeconds = 0f;

        initializationRequests.Add(new RuleInitializationRequest
        {
            BossEntity = bossEntity,
            RuleIndex = ruleIndex,
            Rule = rule
        });
    }

    /// <summary>
    /// Queues one spawn request so pooled minions are activated after query iteration has completed.
    /// </summary>
    /// <param name="spawnRequests">Request list filled by the current update.</param>
    /// <param name="bossEntity">Boss that owns the minions.</param>
    /// <param name="ruleIndex">Rule index on the boss buffer.</param>
    /// <param name="bossPosition">Current boss world position.</param>
    /// <param name="rule">Rule data used for spawning.</param>
    private static void QueueMinionSpawn(NativeList<MinionSpawnRequest> spawnRequests,
                                         Entity bossEntity,
                                         int ruleIndex,
                                         float3 bossPosition,
                                         in EnemyBossMinionSpawnElement rule)
    {
        spawnRequests.Add(new MinionSpawnRequest
        {
            BossEntity = bossEntity,
            RuleIndex = ruleIndex,
            BossPosition = bossPosition,
            Rule = rule
        });
    }

    /// <summary>
    /// Performs queued pool creation and prewarming after boss entity iteration has completed.
    /// </summary>
    /// <param name="entityManager">Entity manager used for structural changes.</param>
    /// <param name="initializationRequests">Requests collected during the simulation pass.</param>
    private static void ProcessRuleInitializationRequests(EntityManager entityManager,
                                                         NativeList<RuleInitializationRequest> initializationRequests)
    {
        for (int requestIndex = 0; requestIndex < initializationRequests.Length; requestIndex++)
        {
            RuleInitializationRequest request = initializationRequests[requestIndex];

            if (!entityManager.Exists(request.BossEntity))
                continue;

            if (!entityManager.HasBuffer<EnemyBossMinionSpawnElement>(request.BossEntity))
                continue;

            EnemyBossMinionSpawnElement rule = request.Rule;

            if (rule.PrefabEntity != Entity.Null && entityManager.Exists(rule.PrefabEntity))
                rule.PoolEntity = EnemyBossMinionPoolUtility.CreateAndPrewarmRulePool(entityManager, in rule);

            DynamicBuffer<EnemyBossMinionSpawnElement> minionRules = entityManager.GetBuffer<EnemyBossMinionSpawnElement>(request.BossEntity);

            if (request.RuleIndex < 0 || request.RuleIndex >= minionRules.Length)
                continue;

            minionRules[request.RuleIndex] = rule;
        }
    }

    /// <summary>
    /// Performs queued minion reservations after boss entity iteration has completed.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate pooled minions.</param>
    /// <param name="spawnRequests">Requests collected during the simulation pass.</param>
    /// <param name="aliveMinionCounts">Current per-rule alive counts captured before spawning.</param>
    /// <param name="elapsedTime">Current world elapsed time used for spawn feedback.</param>
    /// <param name="hasPhysicsWorld">True when wall queries can be evaluated.</param>
    /// <param name="physicsWorldSingleton">Physics world used for spawn safety checks.</param>
    /// <param name="wallsLayerMask">Wall layer mask used by spawn safety checks.</param>
    /// <param name="navigationReady">True when the shared navigation grid can project spawn positions.</param>
    /// <param name="navigationGridState">Shared navigation grid state.</param>
    /// <param name="navigationCells">Stable navigation cell snapshot safe across structural changes.</param>
    /// <param name="hasReferenceSpawnPlaneHeight">True when the player supplied a reliable room height.</param>
    /// <param name="referenceSpawnPlaneHeight">Player-derived world-space Y coordinate used for minion placement.</param>
    private static void ProcessMinionSpawnRequests(EntityManager entityManager,
                                                   NativeList<MinionSpawnRequest> spawnRequests,
                                                   in NativeParallelHashMap<BossMinionRuleKey, int> aliveMinionCounts,
                                                   float elapsedTime,
                                                   bool hasPhysicsWorld,
                                                   in PhysicsWorldSingleton physicsWorldSingleton,
                                                   int wallsLayerMask,
                                                   bool navigationReady,
                                                   in EnemyNavigationGridState navigationGridState,
                                                   NativeArray<EnemyNavigationCellElement> navigationCells,
                                                   bool hasReferenceSpawnPlaneHeight,
                                                   float referenceSpawnPlaneHeight)
    {
        for (int requestIndex = 0; requestIndex < spawnRequests.Length; requestIndex++)
        {
            MinionSpawnRequest request = spawnRequests[requestIndex];

            if (!entityManager.Exists(request.BossEntity))
                continue;

            if (!entityManager.HasBuffer<EnemyBossMinionSpawnElement>(request.BossEntity))
                continue;

            EnemyBossMinionSpawnElement rule = ResolveCurrentRule(entityManager,
                                                                  request.BossEntity,
                                                                  request.RuleIndex,
                                                                  in request.Rule);

            if (rule.PoolEntity == Entity.Null || !entityManager.Exists(rule.PoolEntity))
                continue;

            int aliveMinionCount = ResolveAliveMinionCount(in aliveMinionCounts, request.BossEntity, request.RuleIndex);

            ReserveMinionsForSpawn(entityManager,
                                   request.BossEntity,
                                   request.RuleIndex,
                                   request.BossPosition,
                                   ref rule,
                                   aliveMinionCount,
                                   elapsedTime,
                                   hasPhysicsWorld,
                                   in physicsWorldSingleton,
                                   wallsLayerMask,
                                   navigationReady,
                                   in navigationGridState,
                                   navigationCells,
                                   hasReferenceSpawnPlaneHeight,
                                   referenceSpawnPlaneHeight);

            WriteCurrentRule(entityManager, request.BossEntity, request.RuleIndex, in rule);
        }
    }

    /// <summary>
    /// Reads the current rule from the boss buffer without keeping the buffer alive across structural changes.
    /// </summary>
    /// <param name="entityManager">Entity manager used to access the boss buffer.</param>
    /// <param name="bossEntity">Boss that owns the rule.</param>
    /// <param name="ruleIndex">Rule index inside the boss buffer.</param>
    /// <param name="fallbackRule">Request-time rule used when the buffer index is no longer valid.</param>
    /// <returns>Current rule data.</returns>
    private static EnemyBossMinionSpawnElement ResolveCurrentRule(EntityManager entityManager,
                                                                  Entity bossEntity,
                                                                  int ruleIndex,
                                                                  in EnemyBossMinionSpawnElement fallbackRule)
    {
        DynamicBuffer<EnemyBossMinionSpawnElement> minionRules = entityManager.GetBuffer<EnemyBossMinionSpawnElement>(bossEntity);

        if (ruleIndex < 0 || ruleIndex >= minionRules.Length)
            return fallbackRule;

        return minionRules[ruleIndex];
    }

    /// <summary>
    /// Writes an updated rule back after structural changes have completed, reacquiring the buffer handle.
    /// </summary>
    /// <param name="entityManager">Entity manager used to access the boss buffer.</param>
    /// <param name="bossEntity">Boss that owns the rule.</param>
    /// <param name="ruleIndex">Rule index inside the boss buffer.</param>
    /// <param name="rule">Updated rule data.</param>
    private static void WriteCurrentRule(EntityManager entityManager,
                                         Entity bossEntity,
                                         int ruleIndex,
                                         in EnemyBossMinionSpawnElement rule)
    {
        if (!entityManager.Exists(bossEntity))
            return;

        if (!entityManager.HasBuffer<EnemyBossMinionSpawnElement>(bossEntity))
            return;

        DynamicBuffer<EnemyBossMinionSpawnElement> minionRules = entityManager.GetBuffer<EnemyBossMinionSpawnElement>(bossEntity);

        if (ruleIndex < 0 || ruleIndex >= minionRules.Length)
            return;

        minionRules[ruleIndex] = rule;
    }

    /// <summary>
    /// Reserves up to the configured spawn count from the rule pool and arms warning rings before activation.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate pooled minions.</param>
    /// <param name="bossEntity">Boss that owns the minions.</param>
    /// <param name="ruleIndex">Rule index being spawned.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="rule">Mutable rule runtime data.</param>
    /// <param name="aliveMinionCount">Current count for active and pending minions owned by this rule.</param>
    /// <param name="elapsedTime">Current world elapsed time used to arm spawn feedback.</param>
    /// <param name="hasPhysicsWorld">True when wall queries can be evaluated.</param>
    /// <param name="physicsWorldSingleton">Physics world used for spawn safety checks.</param>
    /// <param name="wallsLayerMask">Wall layer mask used by spawn safety checks.</param>
    /// <param name="navigationReady">True when the shared navigation grid can project spawn positions.</param>
    /// <param name="navigationGridState">Shared navigation grid state.</param>
    /// <param name="navigationCells">Stable navigation cell snapshot safe across structural changes.</param>
    /// <param name="hasReferenceSpawnPlaneHeight">True when the player supplied a reliable room height.</param>
    /// <param name="referenceSpawnPlaneHeight">Player-derived world-space Y coordinate used for minion placement.</param>
    private static void ReserveMinionsForSpawn(EntityManager entityManager,
                                               Entity bossEntity,
                                               int ruleIndex,
                                               float3 bossPosition,
                                               ref EnemyBossMinionSpawnElement rule,
                                               int aliveMinionCount,
                                               float elapsedTime,
                                               bool hasPhysicsWorld,
                                               in PhysicsWorldSingleton physicsWorldSingleton,
                                               int wallsLayerMask,
                                               bool navigationReady,
                                               in EnemyNavigationGridState navigationGridState,
                                               NativeArray<EnemyNavigationCellElement> navigationCells,
                                               bool hasReferenceSpawnPlaneHeight,
                                               float referenceSpawnPlaneHeight)
    {
        int availableSlots = rule.MaxAliveMinions > 0
            ? math.max(0, rule.MaxAliveMinions - aliveMinionCount)
            : rule.SpawnCount;
        int spawnCount = math.min(math.max(0, rule.SpawnCount), availableSlots);
        EnemySpawnWarningConfig spawnWarningConfig = EnemyBossMinionPendingSpawnUtility.BuildSpawnWarningConfig();

        for (int spawnIndex = 0; spawnIndex < spawnCount; spawnIndex++)
        {
            if (!EnemyBossMinionPoolUtility.TryAcquireMinion(entityManager, rule.PoolEntity, rule.PrefabEntity, out Entity minionEntity))
                return;

            EnemyData minionData = entityManager.HasComponent<EnemyData>(minionEntity)
                ? entityManager.GetComponentData<EnemyData>(minionEntity)
                : default;
            float spawnPlaneHeight = hasReferenceSpawnPlaneHeight ? referenceSpawnPlaneHeight : bossPosition.y;
            float3 spawnPosition = EnemyBossMinionSpawnPositionUtility.ResolveSpawnPosition(bossPosition,
                                                                                            spawnPlaneHeight,
                                                                                            rule.SpawnRadius,
                                                                                            rule.SpawnOffset,
                                                                                            bossEntity,
                                                                                            ruleIndex,
                                                                                            spawnIndex,
                                                                                            in minionData,
                                                                                            hasPhysicsWorld,
                                                                                            in physicsWorldSingleton,
                                                                                            wallsLayerMask,
                                                                                            navigationReady,
                                                                                            in navigationGridState,
                                                                                            navigationCells);
            float activationTime = elapsedTime + EnemySpawnWarningConfigUtility.ResolveEffectiveLeadTimeSeconds(in spawnWarningConfig);
            EnemySpawnWarningState warningState = EnemyBossMinionPoolUtility.CreateSpawnWarningState(entityManager,
                                                                                                     rule.PrefabEntity,
                                                                                                     spawnPosition,
                                                                                                     activationTime,
                                                                                                     in spawnWarningConfig);
            EnemyPoolUtility.ReserveEnemyForSpawn(entityManager,
                                                  minionEntity,
                                                  rule.PoolEntity,
                                                  rule.PoolEntity,
                                                  -1,
                                                  spawnPosition,
                                                  warningState,
                                                  elapsedTime < activationTime);
            EnemyBossMinionPoolUtility.ApplyMinionMetadata(entityManager,
                                                           minionEntity,
                                                           bossEntity,
                                                           ruleIndex,
                                                           in rule);

            EnemyBossPendingMinionSpawnElement pendingSpawn = new EnemyBossPendingMinionSpawnElement
            {
                MinionEntity = minionEntity,
                PoolEntity = rule.PoolEntity,
                RuleIndex = ruleIndex,
                SpawnPosition = spawnPosition,
                ActivationTime = activationTime
            };

            if (EnemyBossMinionPendingSpawnUtility.TryAppendPendingSpawn(entityManager, bossEntity, in pendingSpawn))
                continue;

            EnemyBossMinionPendingSpawnUtility.RecyclePendingSpawn(entityManager, in pendingSpawn);
            return;
        }
    }

    #endregion

    #endregion
}
