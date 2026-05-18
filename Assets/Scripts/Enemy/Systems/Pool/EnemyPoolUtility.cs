using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

#region Utilities
/// <summary>
/// Provides shared helper methods for enemy pool expansion and pooled enemy state resets.
/// </summary>
public static class EnemyPoolUtility
{
    #region Constants
    private static readonly float3 ParkingPosition = new float3(0f, -10000f, 0f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Expands one concrete enemy pool by instantiating the provided prefab entity.
    /// Called by pool initialization and by runtime fallback expansion when a wave needs more instances.
    /// </summary>
    /// <param name="entityManager">Entity manager used to create and initialize pooled instances.</param>
    /// <param name="poolEntity">Pool entity that receives the new pooled enemies.</param>
    /// <param name="spawnerEntity">Spawner that owns the pool and spawned instances.</param>
    /// <param name="enemyPrefab">Enemy prefab entity to instantiate.</param>
    /// <param name="count">Number of pooled enemies to create.</param>
    public static void ExpandPool(EntityManager entityManager,
                                  Entity poolEntity,
                                  Entity spawnerEntity,
                                  Entity enemyPrefab,
                                  int count)
    {
        if (count <= 0)
            return;

        if (!entityManager.Exists(poolEntity))
            return;

        if (!entityManager.Exists(enemyPrefab))
            return;

        if (!entityManager.HasBuffer<EnemyPoolElement>(poolEntity))
            return;

        NativeArray<Entity> spawnedEnemies = new NativeArray<Entity>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        entityManager.Instantiate(enemyPrefab, spawnedEnemies);

        try
        {
            for (int index = 0; index < spawnedEnemies.Length; index++)
            {
                Entity enemyEntity = spawnedEnemies[index];
                PrepareEnemyForPool(entityManager, enemyEntity, spawnerEntity, poolEntity);
            }

            DynamicBuffer<EnemyPoolElement> poolBuffer = entityManager.GetBuffer<EnemyPoolElement>(poolEntity);

            for (int index = 0; index < spawnedEnemies.Length; index++)
            {
                poolBuffer.Add(new EnemyPoolElement
                {
                    EnemyEntity = spawnedEnemies[index]
                });
            }
        }
        finally
        {
            if (spawnedEnemies.IsCreated)
                spawnedEnemies.Dispose();
        }
    }

    /// <summary>
    /// Ensures core pooled-enemy components exist on the provided entity.
    /// Mostly acts as a defensive safety net for prefab authoring inconsistencies.
    /// </summary>
    /// <param name="entityManager">Entity manager used to add missing components.</param>
    /// <param name="enemyEntity">Enemy instance that must expose the expected pooled runtime components.</param>
    public static void EnsureEnemyComponents(EntityManager entityManager, Entity enemyEntity)
    {
        if (!entityManager.HasComponent<EnemyPoolValidated>(enemyEntity))
        {
            if (!entityManager.HasComponent<LocalTransform>(enemyEntity))
                entityManager.AddComponentData(enemyEntity, LocalTransform.Identity);

            if (!entityManager.HasComponent<EnemyData>(enemyEntity))
                entityManager.AddComponentData(enemyEntity, default(EnemyData));

            if (!entityManager.HasComponent<EnemyRuntimeState>(enemyEntity))
                entityManager.AddComponentData(enemyEntity, default(EnemyRuntimeState));

            if (!entityManager.HasComponent<EnemyKnockbackState>(enemyEntity))
                entityManager.AddComponentData(enemyEntity, default(EnemyKnockbackState));

            if (!entityManager.HasComponent<EnemyPatternConfig>(enemyEntity))
                entityManager.AddComponentData(enemyEntity, CreateDefaultPatternConfig());

            if (!entityManager.HasComponent<EnemyPatternRuntimeState>(enemyEntity))
                entityManager.AddComponentData(enemyEntity, CreateDefaultPatternRuntimeState());

            if (!entityManager.HasComponent<EnemyShooterControlState>(enemyEntity))
                entityManager.AddComponentData(enemyEntity, default(EnemyShooterControlState));

            if (!entityManager.HasBuffer<EnemyShooterConfigElement>(enemyEntity))
                entityManager.AddBuffer<EnemyShooterConfigElement>(enemyEntity);

            if (!entityManager.HasBuffer<EnemyShooterRuntimeElement>(enemyEntity))
                entityManager.AddBuffer<EnemyShooterRuntimeElement>(enemyEntity);

            if (!entityManager.HasBuffer<EnemyOffensiveEngagementConfigElement>(enemyEntity))
                entityManager.AddBuffer<EnemyOffensiveEngagementConfigElement>(enemyEntity);

            if (!entityManager.HasComponent<EnemyElementalRuntimeState>(enemyEntity))
                entityManager.AddComponentData(enemyEntity, new EnemyElementalRuntimeState
                {
                    SlowPercent = 0f
                });

            if (!entityManager.HasBuffer<EnemyElementStackElement>(enemyEntity))
                entityManager.AddBuffer<EnemyElementStackElement>(enemyEntity);

            if (!entityManager.HasComponent<EnemyHealth>(enemyEntity))
                entityManager.AddComponentData(enemyEntity, new EnemyHealth
                {
                    Current = 1f,
                    Max = 1f,
                    CurrentShield = 0f,
                    MaxShield = 0f
                });

            if (!entityManager.HasComponent<EnemyOwnerSpawner>(enemyEntity))
                entityManager.AddComponentData(enemyEntity, new EnemyOwnerSpawner
                {
                    SpawnerEntity = Entity.Null
                });

            if (!entityManager.HasComponent<EnemyOwnerPool>(enemyEntity))
                entityManager.AddComponentData(enemyEntity, new EnemyOwnerPool
                {
                    PoolEntity = Entity.Null
                });

            if (!entityManager.HasComponent<EnemyWaveOwner>(enemyEntity))
                entityManager.AddComponentData(enemyEntity, new EnemyWaveOwner
                {
                    WaveIndex = -1
                });

            if (!entityManager.HasComponent<EnemyWorldSpaceStatusBarsRuntimeLink>(enemyEntity))
                entityManager.AddComponentData(enemyEntity, new EnemyWorldSpaceStatusBarsRuntimeLink
                {
                    ViewEntity = Entity.Null
                });

            EnemyPoolVisualUtility.EnsureVisualComponents(entityManager, enemyEntity);

            if (!entityManager.HasComponent<EnemyActive>(enemyEntity))
                entityManager.AddComponent<EnemyActive>(enemyEntity);

            if (!entityManager.HasComponent<EnemySpawnInactivityLock>(enemyEntity))
                entityManager.AddComponent<EnemySpawnInactivityLock>(enemyEntity);

            if (!entityManager.HasComponent<EnemySpawnWarningState>(enemyEntity))
                entityManager.AddComponent<EnemySpawnWarningState>(enemyEntity);

            entityManager.AddComponent<EnemyPoolValidated>(enemyEntity);
        }

        EnsureCustomMovementTag(entityManager, enemyEntity);
        EnemyPoolVisualUtility.EnsureVisualModeTags(entityManager, enemyEntity);
    }

    /// <summary>
    /// Resets one pooled enemy for activation and places it at the provided world position.
    /// Called immediately before the instance is enabled as part of a baked wave event.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate runtime state.</param>
    /// <param name="enemyEntity">Enemy instance being activated.</param>
    /// <param name="spawnerEntity">Spawner that owns the enemy.</param>
    /// <param name="poolEntity">Pool entity that must receive the enemy back on despawn.</param>
    /// <param name="waveIndex">Wave index that emitted the enemy.</param>
    /// <param name="worldPosition">Resolved world position for the spawn event.</param>
    public static void ActivateEnemy(EntityManager entityManager,
                                     Entity enemyEntity,
                                     Entity spawnerEntity,
                                     Entity poolEntity,
                                     int waveIndex,
                                     float3 worldPosition)
    {
        EnsureEnemyComponents(entityManager, enemyEntity);
        ResetEnemySimulationState(entityManager, enemyEntity);
        SetEnemyTransformPosition(entityManager, enemyEntity, worldPosition);
        SetEnemyOwnership(entityManager, enemyEntity, spawnerEntity, poolEntity, waveIndex);
        EnemyPoolVisualUtility.ResetVisualRuntimeState(entityManager, enemyEntity, 1);
        ApplySpawnInactivityState(entityManager, enemyEntity);
        ClearSpawnWarningState(entityManager, enemyEntity);

        if (entityManager.HasComponent<EnemyDespawnRequest>(enemyEntity))
            entityManager.RemoveComponent<EnemyDespawnRequest>(enemyEntity);

        entityManager.SetComponentEnabled<EnemyActive>(enemyEntity, true);
    }

    /// <summary>
    /// Resets one pooled enemy for inactive parking inside its owning pool.
    /// Called during prewarm and on final despawn.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate runtime state.</param>
    /// <param name="enemyEntity">Enemy instance being returned to pool.</param>
    /// <param name="spawnerEntity">Spawner that owns the enemy pool.</param>
    /// <param name="poolEntity">Pool that receives the enemy.</param>
    public static void PrepareEnemyForPool(EntityManager entityManager,
                                           Entity enemyEntity,
                                           Entity spawnerEntity,
                                           Entity poolEntity)
    {
        EnsureEnemyComponents(entityManager, enemyEntity);
        ResetEnemySimulationState(entityManager, enemyEntity);
        SetEnemyOwnership(entityManager, enemyEntity, spawnerEntity, poolEntity, -1);
        ParkEnemy(entityManager, enemyEntity);
        EnemyPoolVisualUtility.ResetVisualRuntimeState(entityManager, enemyEntity, 0);
        ClearSpawnWarningState(entityManager, enemyEntity);
        SetSpawnInactivityLock(entityManager, enemyEntity, false);
        entityManager.SetComponentEnabled<EnemyActive>(enemyEntity, false);
    }

    /// <summary>
    /// Reserves one pooled enemy at its exact future spawn position before activation time is reached.
    /// Called by EnemySpawnSystem as soon as the warning lead window opens for one wave event.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate runtime state.</param>
    /// <param name="enemyEntity">Enemy instance being reserved for an upcoming spawn.</param>
    /// <param name="spawnerEntity">Spawner that owns the enemy.</param>
    /// <param name="poolEntity">Pool that provided the enemy instance.</param>
    /// <param name="waveIndex">Wave index that will own the enemy once activated.</param>
    /// <param name="worldPosition">Final world-space spawn position that must match the warning ring.</param>
    /// <param name="warningState">Fully resolved warning state used by presentation until fade-out completion.</param>
    public static void ReserveEnemyForSpawn(EntityManager entityManager,
                                            Entity enemyEntity,
                                            Entity spawnerEntity,
                                            Entity poolEntity,
                                            int waveIndex,
                                            float3 worldPosition,
                                            EnemySpawnWarningState warningState)
    {
        EnsureEnemyComponents(entityManager, enemyEntity);
        ResetEnemySimulationState(entityManager, enemyEntity);
        SetEnemyOwnership(entityManager, enemyEntity, spawnerEntity, poolEntity, waveIndex);
        ParkEnemy(entityManager, enemyEntity);
        EnemyPoolVisualUtility.ResetVisualRuntimeState(entityManager, enemyEntity, 0);
        SetSpawnInactivityLock(entityManager, enemyEntity, false);
        ArmSpawnWarningState(entityManager, enemyEntity, warningState);

        if (entityManager.HasComponent<EnemyDespawnRequest>(enemyEntity))
            entityManager.RemoveComponent<EnemyDespawnRequest>(enemyEntity);

        entityManager.SetComponentEnabled<EnemyActive>(enemyEntity, false);
    }

    /// <summary>
    /// Activates an enemy that was already reserved at its exact spawn point during the warning phase.
    /// Called by EnemySpawnSystem when the reserved spawn time becomes due.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate runtime state.</param>
    /// <param name="enemyEntity">Reserved enemy instance being activated.</param>
    /// <param name="worldPosition">Final world-space spawn position enforced again at activation time.</param>
    public static void ActivateReservedEnemy(EntityManager entityManager,
                                             Entity enemyEntity,
                                             float3 worldPosition)
    {
        EnsureEnemyComponents(entityManager, enemyEntity);
        ResetEnemySimulationState(entityManager, enemyEntity);
        SetEnemyTransformPosition(entityManager, enemyEntity, worldPosition);
        EnemyPoolVisualUtility.ResetVisualRuntimeState(entityManager, enemyEntity, 1);
        ApplySpawnInactivityState(entityManager, enemyEntity);

        if (entityManager.HasComponent<EnemyDespawnRequest>(enemyEntity))
            entityManager.RemoveComponent<EnemyDespawnRequest>(enemyEntity);

        entityManager.SetComponentEnabled<EnemyActive>(enemyEntity, true);
    }

    /// <summary>
    /// Parks the enemy far below the playable area so inactive pooled instances stay hidden.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate LocalTransform.</param>
    /// <param name="enemyEntity">Enemy instance to park.</param>
    public static void ParkEnemy(EntityManager entityManager, Entity enemyEntity)
    {
        if (!entityManager.HasComponent<LocalTransform>(enemyEntity))
            return;

        LocalTransform parkedTransform = entityManager.GetComponentData<LocalTransform>(enemyEntity);
        parkedTransform.Position = ParkingPosition;
        entityManager.SetComponentData(enemyEntity, parkedTransform);

        if (entityManager.HasComponent<LocalToWorld>(enemyEntity))
        {
            entityManager.SetComponentData(enemyEntity, new LocalToWorld
            {
                Value = parkedTransform.ToMatrix()
            });
        }
    }

    #endregion

    #region Private Methods
    /// <summary>
    /// Creates the default fallback pattern config used when instantiated prefabs miss it.
    /// </summary>
    /// <returns>Default EnemyPatternConfig value.</returns>
    private static EnemyPatternConfig CreateDefaultPatternConfig()
    {
        return EnemyPatternDefaultsUtility.CreatePatternConfig();
    }

    /// <summary>
    /// Ensures the custom movement tag matches the baked movement pattern kind.
    /// </summary>
    /// <param name="entityManager">Entity manager used to add or remove the tag.</param>
    /// <param name="enemyEntity">Enemy instance to inspect.</param>
    private static void EnsureCustomMovementTag(EntityManager entityManager, Entity enemyEntity)
    {
        bool shouldUseCustomMovement = false;

        if (entityManager.HasComponent<EnemyPatternConfig>(enemyEntity))
        {
            EnemyPatternConfig patternConfig = entityManager.GetComponentData<EnemyPatternConfig>(enemyEntity);
            shouldUseCustomMovement = patternConfig.MovementKind != EnemyCompiledMovementPatternKind.Grunt;

            if (!shouldUseCustomMovement &&
                patternConfig.HasShortRangeInteraction != 0 &&
                patternConfig.ShortRangeMovementKind != EnemyCompiledMovementPatternKind.Grunt)
            {
                shouldUseCustomMovement = true;
            }
        }

        bool hasCustomPatternMovementTag = entityManager.HasComponent<EnemyCustomPatternMovementTag>(enemyEntity);

        if (shouldUseCustomMovement && !hasCustomPatternMovementTag)
            entityManager.AddComponent<EnemyCustomPatternMovementTag>(enemyEntity);

        if (!shouldUseCustomMovement && hasCustomPatternMovementTag)
            entityManager.RemoveComponent<EnemyCustomPatternMovementTag>(enemyEntity);
    }

    /// <summary>
    /// Resets gameplay runtime state that must start clean every time an enemy is reused.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate components and buffers.</param>
    /// <param name="enemyEntity">Enemy instance to reset.</param>
    private static void ResetEnemySimulationState(EntityManager entityManager, Entity enemyEntity)
    {
        if (entityManager.HasComponent<EnemyRuntimeState>(enemyEntity))
        {
            EnemyRuntimeState runtimeState = entityManager.GetComponentData<EnemyRuntimeState>(enemyEntity);
            runtimeState.Velocity = float3.zero;
            runtimeState.ContactDamageCooldown = 0f;
            runtimeState.AreaDamageCooldown = 0f;
            runtimeState.SpawnInactivityTimer = 0f;
            runtimeState.LifetimeSeconds = 0f;
            runtimeState.FirstDamageLifetimeSeconds = 0f;
            runtimeState.LastDamageLifetimeSeconds = 0f;
            runtimeState.HasTakenDamage = 0;

            unchecked
            {
                runtimeState.SpawnVersion++;
            }

            if (runtimeState.SpawnVersion == 0u)
                runtimeState.SpawnVersion = 1u;

            entityManager.SetComponentData(enemyEntity, runtimeState);
        }

        if (entityManager.HasComponent<EnemyPatternRuntimeState>(enemyEntity))
            entityManager.SetComponentData(enemyEntity, CreateDefaultPatternRuntimeState());

        if (entityManager.HasComponent<EnemyBossPatternRuntimeState>(enemyEntity))
            entityManager.SetComponentData(enemyEntity, CreateDefaultBossPatternRuntimeState());

        if (entityManager.HasBuffer<EnemyBossPatternSlotRuntimeElement>(enemyEntity))
            ResetBossPatternSlotRuntime(entityManager, enemyEntity);

        if (entityManager.HasComponent<EnemyBossDropRuntimeState>(enemyEntity))
            ResetBossDropRuntime(entityManager, enemyEntity);

        if (entityManager.HasBuffer<EnemyBossMinionSpawnElement>(enemyEntity))
            ResetBossMinionRuntime(entityManager, enemyEntity);

        if (entityManager.HasComponent<EnemyKnockbackState>(enemyEntity))
        {
            EnemyKnockbackState knockbackState = entityManager.GetComponentData<EnemyKnockbackState>(enemyEntity);
            EnemyKnockbackRuntimeUtility.Clear(ref knockbackState);
            entityManager.SetComponentData(enemyEntity, knockbackState);
        }

        if (entityManager.HasComponent<EnemyShooterControlState>(enemyEntity))
        {
            EnemyShooterControlState shooterControlState = entityManager.GetComponentData<EnemyShooterControlState>(enemyEntity);
            shooterControlState.MovementLocked = 0;
            shooterControlState.AimDirection = float3.zero;
            shooterControlState.HasAimDirection = 0;
            entityManager.SetComponentData(enemyEntity, shooterControlState);
        }

        if (entityManager.HasBuffer<EnemyShooterRuntimeElement>(enemyEntity))
            ResetShooterRuntime(entityManager, enemyEntity);

        if (entityManager.HasComponent<EnemyHealth>(enemyEntity))
            ResetHealth(entityManager, enemyEntity);

        EnemyPoolVisualUtility.ResetPresentationRuntimeState(entityManager, enemyEntity);

        if (entityManager.HasBuffer<EnemyElementStackElement>(enemyEntity))
            entityManager.GetBuffer<EnemyElementStackElement>(enemyEntity).Clear();

        if (entityManager.HasComponent<EnemyElementalRuntimeState>(enemyEntity))
        {
            EnemyElementalRuntimeState elementalRuntimeState = entityManager.GetComponentData<EnemyElementalRuntimeState>(enemyEntity);
            elementalRuntimeState.SlowPercent = 0f;
            entityManager.SetComponentData(enemyEntity, elementalRuntimeState);
        }
    }

    /// <summary>
    /// Returns the default runtime pattern state for a freshly activated pooled enemy.
    /// </summary>
    /// <returns>Default EnemyPatternRuntimeState value.</returns>
    private static EnemyPatternRuntimeState CreateDefaultPatternRuntimeState()
    {
        return EnemyPatternDefaultsUtility.CreatePatternRuntimeState();
    }

    /// <summary>
    /// Returns the default boss pattern runtime state for a freshly activated pooled boss.
    /// </summary>
    /// <returns>Default boss pattern runtime state value.</returns>
    private static EnemyBossPatternRuntimeState CreateDefaultBossPatternRuntimeState()
    {
        return new EnemyBossPatternRuntimeState
        {
            ActiveInteractionIndex = -2,
            ElapsedSeconds = 0f,
            ActiveInteractionElapsedSeconds = 0f,
            ExtractionElapsedSeconds = 0f,
            TravelledDistance = 0f,
            DistanceSinceLastExtraction = 0f,
            LastExtractionMissingHealthPercent = 0f,
            PlayerDistanceHoldSeconds = 0f,
            DamageWindowElapsedSeconds = 0f,
            DamageWindowAccumulated = 0f,
            PreviousObservedDurability = 0f,
            LastPosition = float3.zero,
            LastObservedDamageLifetimeSeconds = 0f,
            Initialized = 0
        };
    }

    /// <summary>
    /// Resets internal boss pattern slot extraction state for a freshly activated pooled boss.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate the slot runtime buffer.</param>
    /// <param name="enemyEntity">Boss entity whose internal slot states are reset.</param>
    private static void ResetBossPatternSlotRuntime(EntityManager entityManager, Entity enemyEntity)
    {
        DynamicBuffer<EnemyBossPatternSlotRuntimeElement> slotRuntimes = entityManager.GetBuffer<EnemyBossPatternSlotRuntimeElement>(enemyEntity);

        for (int index = 0; index < slotRuntimes.Length; index++)
        {
            EnemyBossPatternSlotRuntimeElement slotRuntime = slotRuntimes[index];
            slotRuntime.ActivePatternIndex = -2;
            slotRuntime.ActiveCandidateIndex = -2;
            slotRuntime.ActiveCandidateElapsedSeconds = 0f;
            slotRuntime.ExtractionElapsedSeconds = 0f;
            slotRuntime.DistanceSinceLastExtraction = 0f;
            slotRuntime.LastExtractionMissingHealthPercent = 0f;
            slotRuntime.PlayerDistanceHoldSeconds = 0f;
            slotRuntime.DamageWindowElapsedSeconds = 0f;
            slotRuntime.DamageWindowAccumulated = 0f;
            slotRuntime.PreviousObservedDurability = 0f;
            slotRuntimes[index] = slotRuntime;
        }
    }

    /// <summary>
    /// Clears boss drop selection state so the next death can extract fresh drop candidates.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate boss drop runtime data.</param>
    /// <param name="enemyEntity">Boss entity whose drop state is reset.</param>
    private static void ResetBossDropRuntime(EntityManager entityManager, Entity enemyEntity)
    {
        entityManager.SetComponentData(enemyEntity, new EnemyBossDropRuntimeState
        {
            SelectionResolved = 0
        });

        if (entityManager.HasBuffer<EnemyBossSelectedDropCandidateElement>(enemyEntity))
            entityManager.GetBuffer<EnemyBossSelectedDropCandidateElement>(enemyEntity).Clear();
    }

    /// <summary>
    /// Resets per-activation boss minion spawn counters while preserving any already created pools.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate the minion spawn buffer.</param>
    /// <param name="enemyEntity">Boss entity whose minion rules are reset.</param>
    private static void ResetBossMinionRuntime(EntityManager entityManager, Entity enemyEntity)
    {
        EnemyBossMinionPendingSpawnUtility.RecycleAndClearPendingSpawns(entityManager, enemyEntity);

        DynamicBuffer<EnemyBossMinionSpawnElement> minionSpawns = entityManager.GetBuffer<EnemyBossMinionSpawnElement>(enemyEntity);

        for (int index = 0; index < minionSpawns.Length; index++)
        {
            EnemyBossMinionSpawnElement minionSpawn = minionSpawns[index];
            minionSpawn.NextSpawnTime = 0f;
            minionSpawn.LastObservedDamageLifetimeSeconds = 0f;
            minionSpawn.Triggered = 0;
            minionSpawn.Initialized = 0;
            minionSpawns[index] = minionSpawn;
        }
    }

    /// <summary>
    /// Resets shooter runtime elements from the baked shooter config count.
    /// </summary>
    /// <param name="entityManager">Entity manager used to access shooter buffers.</param>
    /// <param name="enemyEntity">Enemy instance whose shooter runtime must be rebuilt.</param>
    private static void ResetShooterRuntime(EntityManager entityManager, Entity enemyEntity)
    {
        DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime = entityManager.GetBuffer<EnemyShooterRuntimeElement>(enemyEntity);
        int shooterCount = 0;

        if (entityManager.HasBuffer<EnemyShooterConfigElement>(enemyEntity))
            shooterCount = entityManager.GetBuffer<EnemyShooterConfigElement>(enemyEntity).Length;

        shooterRuntime.Clear();

        for (int shooterIndex = 0; shooterIndex < shooterCount; shooterIndex++)
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

    /// <summary>
    /// Restores health and shield values to their baked maxima.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate EnemyHealth.</param>
    /// <param name="enemyEntity">Enemy instance whose health must be reset.</param>
    private static void ResetHealth(EntityManager entityManager, Entity enemyEntity)
    {
        EnemyHealth enemyHealth = entityManager.GetComponentData<EnemyHealth>(enemyEntity);

        if (enemyHealth.Max < 1f)
            enemyHealth.Max = 1f;

        if (enemyHealth.MaxShield < 0f)
            enemyHealth.MaxShield = 0f;

        enemyHealth.Current = enemyHealth.Max;
        enemyHealth.CurrentShield = enemyHealth.MaxShield;
        entityManager.SetComponentData(enemyEntity, enemyHealth);
    }

    /// <summary>
    /// Writes current ownership metadata onto the pooled enemy instance.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate owner components.</param>
    /// <param name="enemyEntity">Enemy instance whose ownership must be updated.</param>
    /// <param name="spawnerEntity">Owning spawner entity.</param>
    /// <param name="poolEntity">Owning concrete pool entity.</param>
    /// <param name="waveIndex">Wave index that currently owns the enemy, or -1 when pooled.</param>
    private static void SetEnemyOwnership(EntityManager entityManager,
                                          Entity enemyEntity,
                                          Entity spawnerEntity,
                                          Entity poolEntity,
                                          int waveIndex)
    {
        if (entityManager.HasComponent<EnemyOwnerSpawner>(enemyEntity))
        {
            EnemyOwnerSpawner ownerSpawner = entityManager.GetComponentData<EnemyOwnerSpawner>(enemyEntity);
            ownerSpawner.SpawnerEntity = spawnerEntity;
            entityManager.SetComponentData(enemyEntity, ownerSpawner);
        }

        if (entityManager.HasComponent<EnemyOwnerPool>(enemyEntity))
        {
            EnemyOwnerPool ownerPool = entityManager.GetComponentData<EnemyOwnerPool>(enemyEntity);
            ownerPool.PoolEntity = poolEntity;
            entityManager.SetComponentData(enemyEntity, ownerPool);
        }

        if (entityManager.HasComponent<EnemyWaveOwner>(enemyEntity))
        {
            EnemyWaveOwner waveOwner = entityManager.GetComponentData<EnemyWaveOwner>(enemyEntity);
            waveOwner.WaveIndex = waveIndex;
            entityManager.SetComponentData(enemyEntity, waveOwner);
        }
    }

    /// <summary>
    /// Updates LocalTransform position without altering existing rotation or scale.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate LocalTransform.</param>
    /// <param name="enemyEntity">Enemy instance to move.</param>
    /// <param name="worldPosition">Target world position.</param>
    private static void SetEnemyTransformPosition(EntityManager entityManager, Entity enemyEntity, float3 worldPosition)
    {
        if (!entityManager.HasComponent<LocalTransform>(enemyEntity))
            return;

        LocalTransform enemyTransform = entityManager.GetComponentData<LocalTransform>(enemyEntity);
        enemyTransform.Position = worldPosition;
        entityManager.SetComponentData(enemyEntity, enemyTransform);
    }

    /// <summary>
    /// Applies the authored inactivity timer and corresponding runtime lock after a spawn becomes active.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate EnemyRuntimeState and EnemySpawnInactivityLock.</param>
    /// <param name="enemyEntity">Enemy instance that has just become active.</param>
    private static void ApplySpawnInactivityState(EntityManager entityManager, Entity enemyEntity)
    {
        if (!entityManager.HasComponent<EnemyRuntimeState>(enemyEntity))
            return;

        float inactivityTime = 0f;

        if (entityManager.HasComponent<EnemyData>(enemyEntity))
            inactivityTime = math.max(0f, entityManager.GetComponentData<EnemyData>(enemyEntity).SpawnInactivityTime);

        EnemyRuntimeState runtimeState = entityManager.GetComponentData<EnemyRuntimeState>(enemyEntity);
        runtimeState.SpawnInactivityTimer = inactivityTime;
        entityManager.SetComponentData(enemyEntity, runtimeState);
        SetSpawnInactivityLock(entityManager, enemyEntity, inactivityTime > 0f);
    }

    /// <summary>
    /// Enables or disables the spawn inactivity lock without assuming the component already exists.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate the enableable component state.</param>
    /// <param name="enemyEntity">Enemy instance whose post-spawn lock must be updated.</param>
    /// <param name="enabled">True to lock behaviour modules, false to release them.</param>
    private static void SetSpawnInactivityLock(EntityManager entityManager,
                                               Entity enemyEntity,
                                               bool enabled)
    {
        if (!entityManager.HasComponent<EnemySpawnInactivityLock>(enemyEntity))
            return;

        entityManager.SetComponentEnabled<EnemySpawnInactivityLock>(enemyEntity, enabled);
    }

    /// <summary>
    /// Writes the active warning state used by presentation for one reserved enemy.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate EnemySpawnWarningState.</param>
    /// <param name="enemyEntity">Enemy instance that owns the warning.</param>
    /// <param name="warningState">Fully resolved warning payload.</param>
    private static void ArmSpawnWarningState(EntityManager entityManager,
                                             Entity enemyEntity,
                                             EnemySpawnWarningState warningState)
    {
        if (!entityManager.HasComponent<EnemySpawnWarningState>(enemyEntity))
            return;

        entityManager.SetComponentData(enemyEntity, warningState);
        entityManager.SetComponentEnabled<EnemySpawnWarningState>(enemyEntity, true);
    }

    /// <summary>
    /// Clears the warning state so pooled enemies never keep stale ring data across reuses.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate EnemySpawnWarningState.</param>
    /// <param name="enemyEntity">Enemy instance whose warning state must be cleared.</param>
    private static void ClearSpawnWarningState(EntityManager entityManager, Entity enemyEntity)
    {
        if (!entityManager.HasComponent<EnemySpawnWarningState>(enemyEntity))
            return;

        entityManager.SetComponentData(enemyEntity, default(EnemySpawnWarningState));
        entityManager.SetComponentEnabled<EnemySpawnWarningState>(enemyEntity, false);
    }
    #endregion

    #endregion
}
#endregion
