using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Consumes confirmed enemy death puddle requests and acquires pooled ECS render entities.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyKilledEventsSystem))]
[UpdateBefore(typeof(EnemyFinalizeDespawnSystem))]
public partial struct EnemyDeathPuddleSpawnSystem : ISystem
{
    #region Constants
#if UNITY_ANDROID || UNITY_SWITCH
    private const int MaximumActivePuddles = 256;
    private const int MaximumPuddlesPerCell = 6;
#else
    private const int MaximumActivePuddles = 768;
    private const int MaximumPuddlesPerCell = 10;
#endif
    private const float CellSize = 1.75f;
    #endregion

    #region Fields
    private EntityQuery activePuddleQuery;
    private EntityQuery inactivePuddleQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the global puddle request buffer and caches active and inactive pool queries.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        EntityQuery requestQuery = state.GetEntityQuery(ComponentType.ReadWrite<EnemyDeathPuddleSpawnRequest>());

        if (requestQuery.IsEmptyIgnoreFilter)
        {
            Entity requestEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddBuffer<EnemyDeathPuddleSpawnRequest>(requestEntity);
        }

        activePuddleQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyDeathPuddleRuntimeState, EnemyDeathPuddleActive, LocalTransform>()
            .WithNone<Prefab>()
            .Build();
        inactivePuddleQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyDeathPuddleRuntimeState, LocalTransform>()
            .WithDisabled<EnemyDeathPuddleActive>()
            .WithNone<Prefab>()
            .Build();
        state.RequireForUpdate<EnemyDeathPuddleSpawnRequest>();
    }

    /// <summary>
    /// Acquires inactive matching puddles or instantiates new pooled entities while respecting visual density caps.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonBuffer(out DynamicBuffer<EnemyDeathPuddleSpawnRequest> requests))
            return;

        if (requests.Length <= 0)
            return;

        EntityManager entityManager = state.EntityManager;
        Allocator frameAllocator = state.WorldUpdateAllocator;
        NativeArray<EnemyDeathPuddleSpawnRequest> pendingRequests = requests.ToNativeArray(frameAllocator);
        NativeArray<Entity> inactiveEntities = inactivePuddleQuery.ToEntityArray(frameAllocator);
        NativeArray<EnemyDeathPuddleRuntimeState> inactiveStates = inactivePuddleQuery.ToComponentDataArray<EnemyDeathPuddleRuntimeState>(frameAllocator);
        NativeList<byte> inactiveConsumed = new NativeList<byte>(inactiveEntities.Length, frameAllocator);
        NativeParallelHashMap<int, int> activeCountsByCell = BuildActiveCountsByCell(frameAllocator);
        int activePuddleCount = activePuddleQuery.CalculateEntityCount();
        float shaderTime = Time.time;

        requests.Clear();
        inactiveConsumed.Resize(inactiveEntities.Length, NativeArrayOptions.ClearMemory);

        for (int requestIndex = 0; requestIndex < pendingRequests.Length; requestIndex++)
        {
            EnemyDeathPuddleSpawnRequest request = pendingRequests[requestIndex];

            if (request.PrefabEntity == Entity.Null)
                continue;

            int cellKey = ResolveCellKey(request.Position);
            int cellCount = activeCountsByCell.TryGetValue(cellKey, out int existingCellCount) ? existingCellCount : 0;

            if (cellCount >= MaximumPuddlesPerCell)
                continue;

            Entity puddleEntity = AcquireInactivePuddle(request.PrefabEntity,
                                                        inactiveEntities,
                                                        inactiveStates,
                                                        ref inactiveConsumed);

            if (puddleEntity == Entity.Null)
            {
                if (activePuddleCount >= MaximumActivePuddles)
                    continue;

                puddleEntity = entityManager.Instantiate(request.PrefabEntity);
            }

            ActivatePuddle(entityManager, puddleEntity, in request, shaderTime);
            activePuddleCount++;
            activeCountsByCell[cellKey] = cellCount + 1;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Builds active puddle counts by spatial cell once for the current spawn pass.
    /// </summary>
    /// <param name="allocator">Frame allocator used by the temporary map.</param>
    /// <returns>Active puddle counts indexed by deterministic spatial cell key.</returns>
    private NativeParallelHashMap<int, int> BuildActiveCountsByCell(Allocator allocator)
    {
        int activeCount = activePuddleQuery.CalculateEntityCount();
        NativeParallelHashMap<int, int> countsByCell = new NativeParallelHashMap<int, int>(math.max(1, activeCount), allocator);
        NativeArray<LocalTransform> activeTransforms = activePuddleQuery.ToComponentDataArray<LocalTransform>(allocator);

        for (int transformIndex = 0; transformIndex < activeTransforms.Length; transformIndex++)
        {
            int cellKey = ResolveCellKey(activeTransforms[transformIndex].Position);
            int currentCount = countsByCell.TryGetValue(cellKey, out int existingCount) ? existingCount : 0;
            countsByCell[cellKey] = currentCount + 1;
        }

        return countsByCell;
    }

    /// <summary>
    /// Acquires one inactive puddle matching the requested source prefab.
    /// </summary>
    /// <param name="prefabEntity">Requested source prefab entity.</param>
    /// <param name="inactiveEntities">Inactive pooled entity candidates.</param>
    /// <param name="inactiveStates">Runtime states matching inactive candidates.</param>
    /// <param name="inactiveConsumed">Per-candidate flags preventing duplicate acquisition in the same frame.</param>
    /// <returns>Matching pooled entity, or Entity.Null when no candidate exists.</returns>
    private static Entity AcquireInactivePuddle(Entity prefabEntity,
                                                NativeArray<Entity> inactiveEntities,
                                                NativeArray<EnemyDeathPuddleRuntimeState> inactiveStates,
                                                ref NativeList<byte> inactiveConsumed)
    {
        for (int entityIndex = 0; entityIndex < inactiveEntities.Length; entityIndex++)
        {
            if (inactiveConsumed[entityIndex] != 0)
                continue;

            if (inactiveStates[entityIndex].PrefabEntity != prefabEntity)
                continue;

            inactiveConsumed[entityIndex] = 1;
            return inactiveEntities[entityIndex];
        }

        return Entity.Null;
    }

    /// <summary>
    /// Writes all transform, lifetime and material values required by one acquired puddle. Every per-instance
    /// material component is upserted with HasComponent-checked fallback so a legacy or partially baked pooled
    /// entity cannot silently fall back to the material default (which mirrors the field defaults and produced
    /// the "puddle ignores Visual Preset settings" symptom for at least one missing override).
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate the pooled puddle.</param>
    /// <param name="puddleEntity">Acquired puddle entity.</param>
    /// <param name="request">Spawn request supplying resolved instance data.</param>
    /// <param name="shaderTime">Current Unity shader time used as the animation origin.</param>
    private static void ActivatePuddle(EntityManager entityManager,
                                       Entity puddleEntity,
                                       in EnemyDeathPuddleSpawnRequest request,
                                       float shaderTime)
    {
        // Random world-Y spin is consumed by the X +90 plane orientation so puddles always lie flat on the floor.
        float rotationRadians = request.RandomRotation != 0
            ? HashToUnitFloat(request.Seed ^ 0x9E3779B9u) * math.PI * 2f
            : 0f;

        entityManager.SetComponentData(puddleEntity,
                                       LocalTransform.FromPositionRotationScale(request.Position,
                                                                                math.mul(quaternion.RotateY(rotationRadians),
                                                                                         quaternion.RotateX(math.radians(90f))),
                                                                                1f));
        UpsertComponentData(entityManager, puddleEntity, new PostTransformMatrix
        {
            Value = float4x4.Scale(new float3(request.WorldSize.x, request.WorldSize.y, 1f))
        });
        UpsertComponentData(entityManager, puddleEntity, new EnemyDeathPuddleRuntimeState
        {
            PrefabEntity = request.PrefabEntity,
            RemainingSeconds = request.LifetimeSeconds
        });
        UpsertComponentData(entityManager, puddleEntity, new MaterialDeathPuddlePrimaryColor
        {
            Value = request.PrimaryColor
        });
        UpsertComponentData(entityManager, puddleEntity, new MaterialDeathPuddleSecondaryColor
        {
            Value = request.SecondaryColor
        });
        UpsertComponentData(entityManager, puddleEntity, new MaterialDeathPuddleTiming
        {
            Value = new float4(shaderTime,
                               request.LifetimeSeconds,
                               request.StableFraction,
                               request.FinalScaleRatio)
        });
        UpsertComponentData(entityManager, puddleEntity, new MaterialDeathPuddleShape
        {
            Value = new float4(request.EdgeIrregularity,
                               request.BorderWidth,
                               request.EdgeFeather,
                               HashToUnitFloat(request.Seed))
        });
        UpsertComponentData(entityManager, puddleEntity, new MaterialDeathPuddleStyle
        {
            Value = new float4(request.SecondaryPaletteBlend,
                               (float)request.EvaporationCurve,
                               0f,
                               0f)
        });
        UpsertComponentData(entityManager, puddleEntity, new MaterialDeathPuddleFluid
        {
            Value = new float4(request.FlowSpeed,
                               request.Viscosity,
                               request.SurfaceDistortion,
                               request.HighlightStrength)
        });
        entityManager.SetComponentEnabled<EnemyDeathPuddleActive>(puddleEntity, true);
    }

    /// <summary>
    /// Writes one IComponentData value, recovering the component archetype-side when missing. Required because
    /// the shader uses UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED which silently falls back to the material
    /// default when the per-instance component is absent on a pooled entity, ignoring the Visual Preset value.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate the pooled puddle.</param>
    /// <param name="puddleEntity">Pooled puddle entity receiving the value.</param>
    /// <param name="value">Per-instance component value resolved from the spawn request.</param>
    private static void UpsertComponentData<TComponent>(EntityManager entityManager,
                                                        Entity puddleEntity,
                                                        TComponent value) where TComponent : unmanaged, IComponentData
    {
        if (entityManager.HasComponent<TComponent>(puddleEntity))
            entityManager.SetComponentData(puddleEntity, value);
        else
            entityManager.AddComponentData(puddleEntity, value);
    }

    /// <summary>
    /// Encodes one XZ spatial cell into a deterministic integer key.
    /// </summary>
    /// <param name="position">World-space puddle position.</param>
    /// <returns>Encoded spatial cell key.</returns>
    private static int ResolveCellKey(float3 position)
    {
        int cellX = (int)math.floor(position.x / CellSize);
        int cellZ = (int)math.floor(position.z / CellSize);
        return (cellX * 73856093) ^ (cellZ * 19349663);
    }

    /// <summary>
    /// Converts a deterministic uint seed into a normalized float.
    /// </summary>
    /// <param name="seed">Source deterministic seed.</param>
    /// <returns>Normalized value in the [0, 1] range.</returns>
    private static float HashToUnitFloat(uint seed)
    {
        return (math.hash(new uint2(seed, 0x85EBCA6Bu)) & 0x00FFFFFFu) / 16777215f;
    }
    #endregion

    #endregion
}

/// <summary>
/// Advances active death puddle lifetimes and returns expired entities to the dedicated ECS pool.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyDeathPuddleSpawnSystem))]
public partial struct EnemyDeathPuddleLifetimeSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Requires at least one active puddle before updating.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyDeathPuddleActive>();
    }

    /// <summary>
    /// Consumes active lifetimes and parks expired puddles without destroying pooled entities.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = math.max(0f, SystemAPI.Time.DeltaTime);
        EntityManager entityManager = state.EntityManager;

        foreach ((RefRW<EnemyDeathPuddleRuntimeState> runtimeState,
                  RefRW<LocalTransform> puddleTransform,
                  RefRW<PostTransformMatrix> postTransform,
                  Entity puddleEntity)
                 in SystemAPI.Query<RefRW<EnemyDeathPuddleRuntimeState>,
                                    RefRW<LocalTransform>,
                                    RefRW<PostTransformMatrix>>()
                             .WithAll<EnemyDeathPuddleActive>()
                             .WithEntityAccess())
        {
            EnemyDeathPuddleRuntimeState nextState = runtimeState.ValueRO;
            nextState.RemainingSeconds -= deltaTime;

            if (nextState.RemainingSeconds > 0f)
            {
                runtimeState.ValueRW = nextState;
                continue;
            }

            LocalTransform parkedTransform = puddleTransform.ValueRO;
            parkedTransform.Position = new float3(0f, -10000f, 0f);
            puddleTransform.ValueRW = parkedTransform;
            postTransform.ValueRW = new PostTransformMatrix
            {
                Value = float4x4.Scale(float3.zero)
            };
            nextState.RemainingSeconds = 0f;
            runtimeState.ValueRW = nextState;
            entityManager.SetComponentEnabled<EnemyDeathPuddleActive>(puddleEntity, false);
        }
    }
    #endregion

    #endregion
}
