using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.Collections;

/// <summary>
/// Builds and refreshes one shared static navigation flow field used by enemy pursuit around wall obstacles.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(GameSceneManagementSystemGroup))]
[UpdateAfter(typeof(EnemyPoolInitializeSystem))]
[UpdateBefore(typeof(EnemySystemGroup))]
public partial struct EnemyNavigationFlowFieldSystem : ISystem
{
    #region Fields
    private Entity navigationEntity;
    private EntityQuery enemyDataQuery;
    private EntityQuery playerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the shared navigation singleton and required queries.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        navigationEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponent<EnemyNavigationGridTag>(navigationEntity);
        state.EntityManager.AddComponentData(navigationEntity, new EnemyNavigationGridState
        {
            Origin = float2.zero,
            CellSize = 0f,
            InverseCellSize = 0f,
            AgentRadius = 0f,
            Width = 0,
            Height = 0,
            PlayerCellIndex = EnemyNavigationFlowFieldUtility.InvalidCellIndex,
            NextFlowRefreshTime = 0f,
            StaticLayoutHash = 0u,
            Initialized = 0,
            FlowReady = 0
        });
        state.EntityManager.AddBuffer<EnemyNavigationCellElement>(navigationEntity);

        enemyDataQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyData>()
            .Build();

        playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig, LocalTransform>()
            .Build();

        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate(playerQuery);
        state.RequireForUpdate(enemyDataQuery);
    }

    /// <summary>
    /// Rebuilds the static navigation grid when wall geometry changes and refreshes the flow field as the player moves across cells.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        if (!state.EntityManager.Exists(navigationEntity))
            return;

        if (GameSceneTransitionRuntimeGuardUtility.IsDefaultWorldTransitioning() &&
            (!SystemAPI.TryGetSingleton(out GameSceneTransitionPhysicsStepState physicsStepState) ||
             physicsStepState.NavigationWarmupAllowed == 0))
        {
            return;
        }

        if (!SystemAPI.TryGetSingletonEntity<PlayerControllerConfig>(out Entity playerEntity))
            return;

        PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        EnemyNavigationGridState navigationGridState = state.EntityManager.GetComponentData<EnemyNavigationGridState>(navigationEntity);
        DynamicBuffer<EnemyNavigationCellElement> navigationCells = state.EntityManager.GetBuffer<EnemyNavigationCellElement>(navigationEntity);
        float3 playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
        int wallsLayerMask = WorldWallCollisionUtility.ResolveWallsLayerMask();

        if (SystemAPI.TryGetSingleton<PlayerWorldLayersConfig>(out PlayerWorldLayersConfig worldLayersConfig) && worldLayersConfig.WallsLayerMask != 0)
            wallsLayerMask = worldLayersConfig.WallsLayerMask;

        if (wallsLayerMask == 0)
        {
            navigationGridState.FlowReady = 0;
            state.EntityManager.SetComponentData(navigationEntity, navigationGridState);
            return;
        }

        if (!EnemyNavigationFlowFieldUtility.TryCollectStaticWallBounds(in physicsWorldSingleton,
                                                                        wallsLayerMask,
                                                                        out Aabb wallBounds,
                                                                        out uint staticLayoutHash))
        {
            navigationGridState.Initialized = 0;
            navigationGridState.FlowReady = 0;
            navigationGridState.StaticLayoutHash = 0u;
            navigationGridState.PlayerCellIndex = EnemyNavigationFlowFieldUtility.InvalidCellIndex;
            state.EntityManager.SetComponentData(navigationEntity, navigationGridState);
            return;
        }

        bool requiresGridRebuild = navigationGridState.Initialized == 0 ||
                                   navigationGridState.StaticLayoutHash != staticLayoutHash;

        if (requiresGridRebuild)
        {
            float maximumNavigationRadius = ResolveMaximumNavigationRadius();
            EnemyNavigationFlowFieldUtility.RebuildGrid(ref navigationCells,
                                                       ref navigationGridState,
                                                       in physicsWorldSingleton,
                                                       in wallBounds,
                                                       wallsLayerMask,
                                                       maximumNavigationRadius,
                                                       staticLayoutHash);
        }

        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        bool requiresFlowRefresh = EnemyNavigationFlowFieldUtility.ShouldRefreshFlowField(in navigationGridState,
                                                                                          playerPosition,
                                                                                          elapsedTime);

        if (requiresFlowRefresh &&
            EnemyNavigationFlowFieldUtility.TryRefreshFlowField(ref navigationCells,
                                                                ref navigationGridState,
                                                                playerPosition))
        {
            navigationGridState.NextFlowRefreshTime = elapsedTime + EnemyNavigationFlowFieldUtility.FlowRefreshIntervalSeconds;
        }

        state.EntityManager.SetComponentData(navigationEntity, navigationGridState);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the largest baked enemy wall-navigation radius currently available in the world.
    /// </summary>
    /// <returns>Largest body-plus-wall-distance radius used to size the shared navigation clearance.</returns>
    private float ResolveMaximumNavigationRadius()
    {
        float maximumNavigationRadius = 0.55f;
        NativeArray<EnemyData> enemyDataArray = enemyDataQuery.ToComponentDataArray<EnemyData>(Allocator.Temp);

        // Scan the currently baked enemy data only when the navigation grid must be rebuilt.
        for (int enemyIndex = 0; enemyIndex < enemyDataArray.Length; enemyIndex++)
        {
            EnemyData enemyData = enemyDataArray[enemyIndex];
            float navigationRadius = math.max(0.05f, enemyData.BodyRadius + math.max(0f, enemyData.MinimumWallDistance));

            if (navigationRadius > maximumNavigationRadius)
                maximumNavigationRadius = navigationRadius;
        }

        enemyDataArray.Dispose();
        return maximumNavigationRadius;
    }
    #endregion

    #endregion
}
