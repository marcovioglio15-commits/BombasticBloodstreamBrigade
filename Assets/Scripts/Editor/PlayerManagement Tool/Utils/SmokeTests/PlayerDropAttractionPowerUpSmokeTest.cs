#if UNITY_EDITOR
using System;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Unity.Transforms;

/// <summary>
/// Runs deterministic editor checks for Drop Attraction authoring, scaling, aggregation and collection policies.
/// </summary>
public static class PlayerDropAttractionPowerUpSmokeTest
{
    #region Constants
    private const float PrecisionEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    // [MenuItem("Tools/Player/Run Drop Attraction Power-Up Smoke Test")]
    /// <summary>
    /// Executes the Drop Attraction smoke suite from Unity batch mode through -executeMethod.
    /// </summary>
    public static void Run()
    {
        ValidateAuthoringScalingTargets();
        ValidateRuntimeScalingPaths();
        ValidateRequestMerging();
        ValidatePassiveAggregation();
        ValidateCollectionPolicies();
        Debug.Log("[PlayerDropAttractionPowerUpSmokeTest] All Drop Attraction checks passed.");
    }
    #endregion

    #region Authoring Scaling
    /// <summary>
    /// Verifies that both serialized payload fields are exposed as unified Add Scaling targets.
    /// </summary>
    private static void ValidateAuthoringScalingTargets()
    {
        PlayerPowerUpsPreset preset = ScriptableObject.CreateInstance<PlayerPowerUpsPreset>();

        try
        {
            SerializedObject serializedPreset = new SerializedObject(preset);
            SerializedProperty moduleDefinitions = serializedPreset.FindProperty("moduleDefinitions");
            moduleDefinitions.arraySize = 1;
            SerializedProperty dropAttraction = moduleDefinitions.GetArrayElementAtIndex(0)
                                                                 .FindPropertyRelative("data")
                                                                 .FindPropertyRelative("dropAttraction");

            if (dropAttraction == null)
                throw new Exception("Drop Attraction payload is missing from modular serialized data.");

            SerializedProperty attractionRadius = dropAttraction.FindPropertyRelative("attractionRadius");
            SerializedProperty consumeUnusableDrops = dropAttraction.FindPropertyRelative("consumeUnusableDrops");

            if (!PlayerScalingFormulaEditorUtility.SupportsScalingTarget(attractionRadius) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(consumeUnusableDrops))
            {
                throw new Exception("Drop Attraction fields are not fully exposed through Add Scaling.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(preset);
        }
    }

    /// <summary>
    /// Verifies numeric and boolean formula results propagate to active and passive runtime configs independently.
    /// </summary>
    private static void ValidateRuntimeScalingPaths()
    {
        PlayerPowerUpSlotConfig activeConfig = new PlayerPowerUpSlotConfig
        {
            HasDropAttraction = 1,
            DropAttraction = new DropAttractionPowerUpConfig
            {
                AttractionRadius = 4f
            }
        };
        PlayerPassiveToolConfig passiveConfig = new PlayerPassiveToolConfig
        {
            HasDropAttraction = 1,
            DropAttraction = new DropAttractionPowerUpConfig
            {
                AttractionRadius = 6f
            }
        };

        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("dropAttraction.attractionRadius",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           12f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyBooleanValue("dropAttraction.consumeUnusableDrops",
                                                                  PlayerPowerUpUnlockKind.Active,
                                                                  true,
                                                                  ref activeConfig,
                                                                  ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("dropAttraction.attractionRadius",
                                                           PlayerPowerUpUnlockKind.Passive,
                                                           18f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyBooleanValue("dropAttraction.consumeUnusableDrops",
                                                                  PlayerPowerUpUnlockKind.Passive,
                                                                  true,
                                                                  ref activeConfig,
                                                                  ref passiveConfig);

        if (math.abs(activeConfig.DropAttraction.AttractionRadius - 12f) > PrecisionEpsilon ||
            activeConfig.DropAttraction.ConsumeUnusableDrops == 0 ||
            math.abs(passiveConfig.DropAttraction.AttractionRadius - 18f) > PrecisionEpsilon ||
            passiveConfig.DropAttraction.ConsumeUnusableDrops == 0)
        {
            throw new Exception("Drop Attraction runtime formula paths did not update active and passive configs.");
        }
    }
    #endregion

    #region Request Queue
    /// <summary>
    /// Verifies bounded request merging preserves the widest radius and strictest consume policy.
    /// </summary>
    private static void ValidateRequestMerging()
    {
        World world = new World("DropAttractionRequestMergeSmokeTest");
        Entity requestEntity = world.EntityManager.CreateEntity();

        try
        {
            DynamicBuffer<EnemyDropCollectionRequest> requests =
                world.EntityManager.AddBuffer<EnemyDropCollectionRequest>(requestEntity);
            EnemyDropCollectionRequestUtility.Enqueue(requests, 5f, false, false);
            EnemyDropCollectionRequestUtility.Enqueue(requests, 11f, true, false);
            EnemyDropCollectionRequestUtility.Enqueue(requests, 0f, true, true);

            if (requests.Length != 1 ||
                math.abs(requests[0].AttractionRadius - 11f) > PrecisionEpsilon ||
                requests[0].ConsumeUnusableDrops == 0 ||
                requests[0].CollectAllImmediately == 0)
            {
                throw new Exception("Drop-collection requests did not merge into one strict bounded command.");
            }
        }
        finally
        {
            world.Dispose();
        }
    }
    #endregion

    #region Passive Aggregation
    /// <summary>
    /// Verifies passive aggregation keeps the maximum radius and combines consume policy with logical OR semantics.
    /// </summary>
    private static void ValidatePassiveAggregation()
    {
        PlayerPassiveToolsState aggregate = default;
        PlayerPassiveToolsAggregationUtility.ResetToDefault(ref aggregate);
        PlayerPassiveToolsAggregationUtility.AccumulatePassiveTool(ref aggregate,
                                                                   new PlayerPassiveToolConfig
                                                                   {
                                                                       IsDefined = 1,
                                                                       HasDropAttraction = 1,
                                                                       DropAttraction = new DropAttractionPowerUpConfig
                                                                       {
                                                                           AttractionRadius = 9f
                                                                       }
                                                                   });
        PlayerPassiveToolsAggregationUtility.AccumulatePassiveTool(ref aggregate,
                                                                   new PlayerPassiveToolConfig
                                                                   {
                                                                       IsDefined = 1,
                                                                       HasDropAttraction = 1,
                                                                       DropAttraction = new DropAttractionPowerUpConfig
                                                                       {
                                                                           AttractionRadius = 15f,
                                                                           ConsumeUnusableDrops = 1
                                                                       }
                                                                   });

        if (aggregate.HasDropAttraction == 0 ||
            math.abs(aggregate.DropAttraction.AttractionRadius - 15f) > PrecisionEpsilon ||
            aggregate.DropAttraction.ConsumeUnusableDrops == 0)
        {
            throw new Exception("Passive Drop Attraction aggregation did not preserve its effective radius and policy.");
        }
    }
    #endregion

    #region Runtime Collection
    /// <summary>
    /// Verifies active custom policy, passive collection and finalized-room forced consumption through the shared ECS path.
    /// </summary>
    private static void ValidateCollectionPolicies()
    {
        World world = new World("DropAttractionCollectionSmokeTest");
        EntityManager entityManager = world.EntityManager;

        try
        {
            world.SetTime(new TimeData(1d, 1f / 60f));
            Entity requestEntity = CreateRequestQueue(entityManager);
            Entity playerEntity = CreatePlayer(entityManager);
            Entity poolEntity = entityManager.CreateEntity();
            entityManager.AddBuffer<EnemyExperienceDropPoolElement>(poolEntity);
            Entity dropEntity = CreateFullHealthRecoveryDrop(entityManager,
                                                              poolEntity,
                                                              new float3(0.5f, 0f, 0f));
            SystemHandle collectionSystem = world.GetOrCreateSystem<EnemyExperienceDropCollectSystem>();
            DynamicBuffer<EnemyDropCollectionRequest> requests =
                entityManager.GetBuffer<EnemyDropCollectionRequest>(requestEntity);

            // A pulse without the custom consume policy must leave unusable recovery available.
            EnemyDropCollectionRequestUtility.Enqueue(requests, 5f, false, false);
            collectionSystem.Update(world.Unmanaged);

            if (!entityManager.IsComponentEnabled<EnemyExperienceDropActive>(dropEntity))
                throw new Exception("Drop Attraction consumed an unusable recovery while its custom policy was disabled.");

            // The same pulse consumes the drop once the explicit policy is enabled.
            EnemyDropCollectionRequestUtility.Enqueue(requests, 5f, true, false);
            collectionSystem.Update(world.Unmanaged);

            if (entityManager.IsComponentEnabled<EnemyExperienceDropActive>(dropEntity) ||
                entityManager.GetBuffer<EnemyExperienceDropPoolElement>(poolEntity).Length != 1)
                throw new Exception("Drop Attraction did not consume an unusable recovery while its custom policy was enabled.");

            // Passive aggregation feeds the same collection path without requiring a per-frame input request.
            ResetDrop(entityManager, dropEntity, poolEntity, new float3(0.5f, 0f, 0f));
            entityManager.GetBuffer<EnemyExperienceDropPoolElement>(poolEntity).Clear();
            DynamicBuffer<PlayerPassiveToolsStateElement> passiveState =
                entityManager.GetBuffer<PlayerPassiveToolsStateElement>(playerEntity);
            passiveState.Add(new PlayerPassiveToolsStateElement
            {
                Value = new PlayerPassiveToolsState
                {
                    HasDropAttraction = 1,
                    DropAttraction = new DropAttractionPowerUpConfig
                    {
                        AttractionRadius = 8f,
                        ConsumeUnusableDrops = 1
                    }
                }
            });
            collectionSystem.Update(world.Unmanaged);

            if (entityManager.IsComponentEnabled<EnemyExperienceDropActive>(dropEntity) ||
                entityManager.GetBuffer<EnemyExperienceDropPoolElement>(poolEntity).Length != 1)
                throw new Exception("Passive Drop Attraction did not continuously consume an in-range unusable recovery.");

            // Room clear must collect every drop even after the run outcome was finalized.
            ResetDrop(entityManager, dropEntity, poolEntity, new float3(500f, 0f, 0f));
            entityManager.GetBuffer<EnemyExperienceDropPoolElement>(poolEntity).Clear();
            passiveState.Clear();
            entityManager.SetComponentData(playerEntity, new PlayerRunOutcomeState
            {
                IsFinalized = 1
            });
            Entity managerEntity = entityManager.CreateEntity();
            DynamicBuffer<GameProceduralRoomClearedEvent> clearedEvents =
                entityManager.AddBuffer<GameProceduralRoomClearedEvent>(managerEntity);
            clearedEvents.Add(new GameProceduralRoomClearedEvent
            {
                RunSeed = 19u,
                GenerationVersion = 3u,
                ClearVersion = 7u
            });
            SystemHandle roomClearSystem = world.GetOrCreateSystem<EnemyRoomClearDropCollectionSystem>();
            roomClearSystem.Update(world.Unmanaged);
            collectionSystem.Update(world.Unmanaged);

            if (entityManager.IsComponentEnabled<EnemyExperienceDropActive>(dropEntity) ||
                entityManager.GetBuffer<EnemyExperienceDropPoolElement>(poolEntity).Length != 1)
                throw new Exception("Room clear did not consume a distant unusable drop through the forced collection path.");

            // Reprocessing the retained event must not enqueue the same authoritative transaction twice.
            roomClearSystem.Update(world.Unmanaged);

            if (entityManager.GetBuffer<EnemyDropCollectionRequest>(requestEntity).Length != 0)
                throw new Exception("Room-clear drop collection enqueued the same clear version more than once.");

            // A new run can legitimately reuse the same clear counter and must still enqueue collection.
            DynamicBuffer<GameProceduralRoomClearedEvent> nextRunEvents =
                entityManager.GetBuffer<GameProceduralRoomClearedEvent>(managerEntity);
            nextRunEvents[0] = new GameProceduralRoomClearedEvent
            {
                RunSeed = 20u,
                GenerationVersion = 1u,
                ClearVersion = 7u
            };
            roomClearSystem.Update(world.Unmanaged);

            if (entityManager.GetBuffer<EnemyDropCollectionRequest>(requestEntity).Length != 1)
                throw new Exception("Room-clear drop collection suppressed a valid clear transaction from a new run.");
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Creates the unique collection request singleton used by power-up and room-clear systems.
    /// </summary>
    /// <param name="entityManager">Entity manager receiving the singleton and its bounded request buffer.</param>
    /// <returns>Created request singleton entity.</returns>
    private static Entity CreateRequestQueue(EntityManager entityManager)
    {
        Entity requestEntity = entityManager.CreateEntity(typeof(EnemyDropCollectionRequestQueue));
        entityManager.AddBuffer<EnemyDropCollectionRequest>(requestEntity);
        return requestEntity;
    }

    /// <summary>
    /// Creates the minimal authoritative player state required by drop collection.
    /// </summary>
    /// <param name="entityManager">Entity manager receiving the player components and runtime buffers.</param>
    /// <returns>Created player entity.</returns>
    private static Entity CreatePlayer(EntityManager entityManager)
    {
        Entity playerEntity = entityManager.CreateEntity(typeof(LocalTransform),
                                                          typeof(PlayerMovementState),
                                                          typeof(PlayerExperienceCollection),
                                                          typeof(PlayerExperience),
                                                          typeof(PlayerHealth),
                                                          typeof(PlayerShield),
                                                          typeof(PlayerLevel),
                                                          typeof(PlayerProgressionConfig),
                                                          typeof(PlayerRunOutcomeState));
        entityManager.AddBuffer<PlayerRuntimeGamePhaseElement>(playerEntity);
        entityManager.AddBuffer<PlayerPassiveToolsStateElement>(playerEntity);
        entityManager.SetComponentData(playerEntity, LocalTransform.FromPosition(float3.zero));
        entityManager.SetComponentData(playerEntity, new PlayerExperienceCollection());
        entityManager.SetComponentData(playerEntity, new PlayerHealth
        {
            Current = 100f,
            Max = 100f
        });
        entityManager.SetComponentData(playerEntity, new PlayerShield
        {
            Current = 50f,
            Max = 50f
        });
        return playerEntity;
    }

    /// <summary>
    /// Creates one active recovery drop that cannot affect the full-health player fixture.
    /// </summary>
    /// <param name="entityManager">Entity manager receiving the pooled drop entity.</param>
    /// <param name="poolEntity">Pool owner that must receive the drop after collection.</param>
    /// <param name="position">World position used to test radius and distance-independent behavior.</param>
    /// <returns>Created active drop entity.</returns>
    private static Entity CreateFullHealthRecoveryDrop(EntityManager entityManager,
                                                       Entity poolEntity,
                                                       float3 position)
    {
        Entity dropEntity = entityManager.CreateEntity(typeof(EnemyExperienceDrop),
                                                        typeof(LocalTransform),
                                                        typeof(EnemyExperienceDropActive));
        ResetDrop(entityManager, dropEntity, poolEntity, position);
        return dropEntity;
    }

    /// <summary>
    /// Reactivates one pooled drop with deterministic recovery data for the next collection policy check.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the drop.</param>
    /// <param name="dropEntity">Drop entity being returned to an active test state.</param>
    /// <param name="poolEntity">Pool owner that receives the drop after collection.</param>
    /// <param name="position">World position assigned to the reactivated drop.</param>
    private static void ResetDrop(EntityManager entityManager,
                                  Entity dropEntity,
                                  Entity poolEntity,
                                  float3 position)
    {
        entityManager.SetComponentData(dropEntity, new EnemyExperienceDrop
        {
            RewardKind = EnemyDropPickupRewardKind.Recovery,
            HealthRestoreAmount = 10f,
            AttractionSpeed = 25f,
            CollectDistance = 1f,
            SpawnStartPosition = position,
            SpawnTargetPosition = position,
            PoolEntity = poolEntity
        });
        entityManager.SetComponentData(dropEntity, LocalTransform.FromPosition(position));
        entityManager.SetComponentEnabled<EnemyExperienceDropActive>(dropEntity, true);
    }
    #endregion

    #endregion
}
#endif
