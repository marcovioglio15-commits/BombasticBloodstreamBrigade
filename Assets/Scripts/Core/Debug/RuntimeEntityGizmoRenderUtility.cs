using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Collects live ECS runtime data and emits a shared set of gameplay debug gizmos through an abstract primitive drawer.
/// none.
/// </summary>
public static class RuntimeEntityGizmoRenderUtility
{
    #region Constants
    private const float DirectionMagnitudeEpsilon = 0.0001f;
    private const float PlayerDirectionLength = 1.55f;
    private const float BombVelocityLength = 1.2f;
    private const float PlayerMarkerRadius = 0.12f;
    private const float WanderTargetMarkerRadius = 0.1f;
    private const float EnemyDrawDistance = 60f;
    private const float SpawnerDrawDistance = 140f;
    private const float BombDrawDistance = 40f;
    private const int MaxEnemyDrawCount = 160;
    private const int MaxSpawnerDrawCount = 32;
    private const int MaxBombDrawCount = 32;
    private const int MaxEnemyLabelCount = 12;

    private static readonly Color PlayerPickupRadiusColor = new Color(0.1f, 0.92f, 0.68f, 0.96f);
    private static readonly Color PlayerMoveVectorColor = new Color(0.24f, 0.82f, 1f, 0.96f);
    private static readonly Color PlayerLookDirectionColor = new Color(1f, 0.92f, 0.2f, 0.96f);
    private static readonly Color EnemyBodyRadiusColor = new Color(1f, 0.84f, 0.18f, 0.94f);
    private static readonly Color EnemyContactRadiusColor = new Color(1f, 0.28f, 0.28f, 0.94f);
    private static readonly Color EnemyAreaRadiusColor = new Color(1f, 0.52f, 0.18f, 0.94f);
    private static readonly Color EnemySeparationRadiusColor = new Color(0.24f, 0.72f, 1f, 0.94f);
    private static readonly Color EnemyWanderTargetColor = new Color(0.36f, 1f, 0.82f, 0.94f);
    private static readonly Color EnemyShortRangeDashTargetColor = new Color(1f, 0.42f, 0.78f, 0.94f);
    private static readonly Color EnemyAcidTrailSegmentColor = new Color(0.58f, 1f, 0.18f, 0.82f);
    private static readonly Color SpawnerSpawnRadiusColor = new Color(0.2f, 0.9f, 0.42f, 0.94f);
    private static readonly Color SpawnerDespawnRadiusColor = new Color(1f, 0.66f, 0.24f, 0.94f);
    private static readonly Color BombRadiusColor = new Color(1f, 0.4f, 0.12f, 0.94f);
    private static readonly Color BombVelocityColor = new Color(1f, 0.86f, 0.28f, 0.94f);
    #endregion

    #region Fields
    private static World cachedWorld;
    private static EntityManager cachedEntityManager;
    private static EntityQuery playerQuery;
    private static EntityQuery enemyQuery;
    private static EntityQuery spawnerQuery;
    private static EntityQuery bombQuery;
    private static EntityQuery projectileQuery;
    #endregion

    #region Properties
    public static bool AnyRuntimeGizmoEnabled
    {
        get
        {
            return RuntimeGizmoDebugState.AnyRuntimeGizmoEnabled;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Renders the full runtime gizmo set through the provided primitive drawer.
    /// </summary>
    /// <param name="primitiveDrawer">Active rendering backend that receives primitive draw calls.</param>
    /// <returns>True when a valid runtime world and player context were available for rendering.</returns>
    public static bool TryRender(IRuntimeGizmoPrimitiveDrawer primitiveDrawer)
    {
        if (primitiveDrawer == null)
            return false;

        if (!RuntimeGizmoDebugState.AnyRuntimeGizmoEnabled)
            return false;

        if (!TryInitializeQueries())
            return false;

        if (!TryResolvePlayer(out Entity playerEntity, out LocalTransform playerTransform))
            return false;

        DrawPlayerGizmos(primitiveDrawer, cachedEntityManager, playerEntity, in playerTransform);
        DrawEnemyGizmos(primitiveDrawer, cachedEntityManager, playerTransform.Position);
        DrawSpawnerGizmos(primitiveDrawer, cachedEntityManager, playerTransform.Position);
        DrawBombGizmos(primitiveDrawer, cachedEntityManager, playerTransform.Position);
        RuntimeEntityProjectileGizmoUtility.DrawProjectileGizmos(primitiveDrawer,
                                                                 cachedEntityManager,
                                                                 projectileQuery,
                                                                 playerTransform.Position);
        return true;
    }

    /// <summary>
    /// Clears cached ECS queries so a new runtime world can be resolved after domain reloads or play mode transitions.
    /// none.
    /// </summary>
    public static void ResetCachedContext()
    {
        cachedWorld = null;
        cachedEntityManager = default;
        playerQuery = default;
        enemyQuery = default;
        spawnerQuery = default;
        bombQuery = default;
        projectileQuery = default;
    }
    #endregion

    #region Initialization
    private static bool TryInitializeQueries()
    {
        World defaultWorld = World.DefaultGameObjectInjectionWorld;

        if (defaultWorld == null || !defaultWorld.IsCreated)
        {
            ResetCachedContext();
            return false;
        }

        if (ReferenceEquals(cachedWorld, defaultWorld) && cachedWorld.IsCreated)
            return true;

        ResetCachedContext();
        cachedWorld = defaultWorld;
        cachedEntityManager = defaultWorld.EntityManager;
        playerQuery = cachedEntityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>(),
                                                            ComponentType.ReadOnly<LocalTransform>());
        enemyQuery = cachedEntityManager.CreateEntityQuery(ComponentType.ReadOnly<EnemyData>(),
                                                           ComponentType.ReadOnly<LocalTransform>(),
                                                           ComponentType.ReadOnly<EnemyActive>());
        spawnerQuery = cachedEntityManager.CreateEntityQuery(ComponentType.ReadOnly<EnemySpawner>(),
                                                             ComponentType.ReadOnly<LocalTransform>());
        bombQuery = cachedEntityManager.CreateEntityQuery(ComponentType.ReadOnly<BombFuseState>(),
                                                          ComponentType.ReadOnly<LocalTransform>());
        projectileQuery = cachedEntityManager.CreateEntityQuery(ComponentType.ReadOnly<Projectile>(),
                                                                ComponentType.ReadOnly<ProjectileRuntimeState>(),
                                                                ComponentType.ReadOnly<ProjectileOwner>(),
                                                                ComponentType.ReadOnly<LocalTransform>(),
                                                                ComponentType.ReadOnly<ProjectileActive>());
        return true;
    }

    private static bool TryResolvePlayer(out Entity playerEntity, out LocalTransform playerTransform)
    {
        playerEntity = Entity.Null;
        playerTransform = default;

        if (cachedWorld == null || !cachedWorld.IsCreated)
            return false;

        if (playerQuery.CalculateEntityCount() != 1)
            return false;

        Entity resolvedPlayer = playerQuery.GetSingletonEntity();

        if (!cachedEntityManager.Exists(resolvedPlayer))
            return false;

        playerEntity = resolvedPlayer;
        playerTransform = cachedEntityManager.GetComponentData<LocalTransform>(resolvedPlayer);
        return true;
    }
    #endregion

    #region Player
    /// <summary>
    /// Draws the player gameplay gizmos derived from live movement, look and pickup ECS state.
    /// </summary>
    /// <param name="primitiveDrawer">Active rendering backend receiving primitive calls.</param>
    /// <param name="entityManager">Runtime entity manager used to fetch player components.</param>
    /// <param name="playerEntity">Runtime player entity.</param>
    /// <param name="playerTransform">Runtime player transform.</param>
    private static void DrawPlayerGizmos(IRuntimeGizmoPrimitiveDrawer primitiveDrawer,
                                         EntityManager entityManager,
                                         Entity playerEntity,
                                         in LocalTransform playerTransform)
    {
        Vector3 playerPosition = ToVector3(playerTransform.Position);
        bool drewPlayerGizmo = false;

        // Draw the experience pickup radius when the collection component is available.
        if (RuntimeGizmoDebugState.PlayerPickupRadiusEnabled &&
            entityManager.HasComponent<PlayerExperienceCollection>(playerEntity))
        {
            PlayerExperienceCollection pickup = entityManager.GetComponentData<PlayerExperienceCollection>(playerEntity);
            primitiveDrawer.DrawWireDisc(playerPosition, pickup.PickupRadius, PlayerPickupRadiusColor);
            drewPlayerGizmo = true;
        }

        // Draw movement intent using the current velocity first, then desired direction as fallback.
        if (RuntimeGizmoDebugState.PlayerMoveVectorEnabled &&
            entityManager.HasComponent<PlayerMovementState>(playerEntity))
        {
            PlayerMovementState movementState = entityManager.GetComponentData<PlayerMovementState>(playerEntity);
            float3 moveDirection = ResolveMoveDirection(in movementState);

            if (math.lengthsq(moveDirection) > DirectionMagnitudeEpsilon)
            {
                primitiveDrawer.DrawDirection(playerPosition,
                                              ToVector3(moveDirection),
                                              PlayerDirectionLength,
                                              PlayerMoveVectorColor);
                drewPlayerGizmo = true;
            }
        }

        // Draw look direction using the current resolved facing and then the desired aim as fallback.
        if (RuntimeGizmoDebugState.PlayerLookDirectionEnabled &&
            entityManager.HasComponent<PlayerLookState>(playerEntity))
        {
            PlayerLookState lookState = entityManager.GetComponentData<PlayerLookState>(playerEntity);
            float3 lookDirection = math.lengthsq(lookState.CurrentDirection) > DirectionMagnitudeEpsilon
                ? lookState.CurrentDirection
                : lookState.DesiredDirection;

            if (math.lengthsq(lookDirection) > DirectionMagnitudeEpsilon)
            {
                primitiveDrawer.DrawDirection(playerPosition,
                                              ToVector3(lookDirection),
                                              PlayerDirectionLength,
                                              PlayerLookDirectionColor);
                drewPlayerGizmo = true;
            }
        }

        if (!drewPlayerGizmo)
            return;

        primitiveDrawer.DrawMarker(playerPosition, PlayerMarkerRadius, PlayerLookDirectionColor);

        if (RuntimeGizmoDebugState.ShowLabels)
            primitiveDrawer.DrawLabel(playerPosition, "Player");
    }

    private static float3 ResolveMoveDirection(in PlayerMovementState movementState)
    {
        if (math.lengthsq(movementState.Velocity) > DirectionMagnitudeEpsilon)
            return math.normalizesafe(movementState.Velocity);

        return math.normalizesafe(movementState.DesiredDirection);
    }
    #endregion

    #region Enemies
    /// <summary>
    /// Draws enemy combat and navigation gizmos for active enemies near the player to keep the overlay readable.
    /// </summary>
    /// <param name="primitiveDrawer">Active rendering backend receiving primitive calls.</param>
    /// <param name="entityManager">Runtime entity manager used to fetch enemy components.</param>
    /// <param name="playerPosition">Runtime player position used for distance filtering.</param>
    private static void DrawEnemyGizmos(IRuntimeGizmoPrimitiveDrawer primitiveDrawer,
                                        EntityManager entityManager,
                                        float3 playerPosition)
    {
        if (cachedWorld == null || !cachedWorld.IsCreated)
            return;

        if (enemyQuery.IsEmptyIgnoreFilter)
            return;

        NativeArray<Entity> enemyEntities = enemyQuery.ToEntityArray(Allocator.Temp);
        int drawnCount = 0;
        int labeledCount = 0;

        try
        {
            // Filter enemies around the player and stop once the debug cap is reached.
            for (int enemyIndex = 0; enemyIndex < enemyEntities.Length; enemyIndex++)
            {
                if (drawnCount >= MaxEnemyDrawCount)
                    break;

                Entity enemyEntity = enemyEntities[enemyIndex];
                LocalTransform enemyTransform = entityManager.GetComponentData<LocalTransform>(enemyEntity);
                float planarDistance = math.distance(playerPosition.xz, enemyTransform.Position.xz);

                if (planarDistance > EnemyDrawDistance)
                    continue;

                EnemyData enemyData = entityManager.GetComponentData<EnemyData>(enemyEntity);
                Vector3 enemyPosition = ToVector3(enemyTransform.Position);
                bool drewEnemyGizmo = false;

                // Draw the gameplay radii that directly affect collision, damage and steering.
                if (RuntimeGizmoDebugState.EnemyBodyRadiusEnabled)
                {
                    primitiveDrawer.DrawWireDisc(enemyPosition, enemyData.BodyRadius, EnemyBodyRadiusColor);
                    drewEnemyGizmo = true;
                }

                if (RuntimeGizmoDebugState.EnemyContactRadiusEnabled && enemyData.ContactDamageEnabled != 0)
                {
                    primitiveDrawer.DrawWireDisc(enemyPosition, enemyData.ContactRadius, EnemyContactRadiusColor);
                    drewEnemyGizmo = true;
                }

                if (RuntimeGizmoDebugState.EnemyAreaRadiusEnabled && enemyData.AreaDamageEnabled != 0)
                {
                    primitiveDrawer.DrawWireDisc(enemyPosition, enemyData.AreaRadius, EnemyAreaRadiusColor);
                    drewEnemyGizmo = true;
                }

                if (RuntimeGizmoDebugState.EnemySeparationRadiusEnabled)
                {
                    primitiveDrawer.DrawWireDisc(enemyPosition, enemyData.SeparationRadius, EnemySeparationRadiusColor);
                    drewEnemyGizmo = true;
                }

                // Draw the current wander target only for enemies that actively carry that pattern runtime state.
                if (RuntimeGizmoDebugState.EnemyWanderTargetEnabled &&
                    entityManager.HasComponent<EnemyPatternRuntimeState>(enemyEntity))
                {
                    EnemyPatternRuntimeState patternRuntimeState = entityManager.GetComponentData<EnemyPatternRuntimeState>(enemyEntity);

                    if (patternRuntimeState.WanderHasTarget != 0)
                    {
                        Vector3 targetPosition = ToVector3(patternRuntimeState.WanderTargetPosition);
                        primitiveDrawer.DrawLink(enemyPosition, targetPosition, EnemyWanderTargetColor);
                        primitiveDrawer.DrawMarker(targetPosition, WanderTargetMarkerRadius, EnemyWanderTargetColor);
                        drewEnemyGizmo = true;
                    }

                    if (entityManager.HasComponent<EnemyPatternConfig>(enemyEntity) &&
                        patternRuntimeState.ShortRangeDashPhase != EnemyShortRangeDashPhase.Idle)
                    {
                        EnemyPatternConfig patternConfig = entityManager.GetComponentData<EnemyPatternConfig>(enemyEntity);

                        if (patternConfig.ShortRangeMovementKind == EnemyCompiledMovementPatternKind.ShortRangeDash)
                        {
                            float normalizedDashProgress = patternRuntimeState.ShortRangeDashPhase == EnemyShortRangeDashPhase.Dashing
                                ? math.saturate(patternRuntimeState.ShortRangeDashPhaseElapsed /
                                                math.max(0.01f, patternConfig.ShortRangeDashDuration))
                                : 0f;
                            float3 dashCurrentPoint = EnemyPatternShortRangeDashUtility.ResolvePathPoint(in patternConfig,
                                                                                                        in patternRuntimeState,
                                                                                                        normalizedDashProgress);
                            float3 dashEndPoint = EnemyPatternShortRangeDashUtility.ResolvePathPoint(in patternConfig,
                                                                                                    in patternRuntimeState,
                                                                                                    1f);
                            primitiveDrawer.DrawLink(enemyPosition, ToVector3(dashCurrentPoint), EnemyShortRangeDashTargetColor);
                            primitiveDrawer.DrawLink(ToVector3(dashCurrentPoint), ToVector3(dashEndPoint), EnemyShortRangeDashTargetColor);
                            primitiveDrawer.DrawMarker(ToVector3(dashEndPoint), WanderTargetMarkerRadius, EnemyShortRangeDashTargetColor);
                            drewEnemyGizmo = true;
                        }
                    }

                    if (entityManager.HasComponent<EnemyPatternConfig>(enemyEntity) &&
                        entityManager.HasBuffer<EnemyAcidTrailSegmentElement>(enemyEntity))
                    {
                        EnemyPatternConfig patternConfig = entityManager.GetComponentData<EnemyPatternConfig>(enemyEntity);

                        if (patternConfig.AcidTrailEnabled != 0 && patternConfig.AcidTrailDebugDrawSegments != 0)
                            drewEnemyGizmo |= DrawAcidTrailSegments(primitiveDrawer, entityManager.GetBuffer<EnemyAcidTrailSegmentElement>(enemyEntity));
                    }
                }

                if (!drewEnemyGizmo)
                    continue;

                if (RuntimeGizmoDebugState.ShowLabels && labeledCount < MaxEnemyLabelCount)
                {
                    primitiveDrawer.DrawLabel(enemyPosition, "Enemy");
                    labeledCount++;
                }

                drawnCount++;
            }
        }
        finally
        {
            if (enemyEntities.IsCreated)
                enemyEntities.Dispose();
        }
    }

    /// <summary>
    /// Draws active Acid Wanderer hazard segments as planar discs when the enemy wander debug toggle is enabled.
    /// </summary>
    /// <param name="primitiveDrawer">Active rendering backend receiving primitive calls.</param>
    /// <param name="segments">Per-enemy acid trail buffer read from ECS.</param>
    /// <returns>True when at least one active segment was drawn.</returns>
    private static bool DrawAcidTrailSegments(IRuntimeGizmoPrimitiveDrawer primitiveDrawer,
                                              DynamicBuffer<EnemyAcidTrailSegmentElement> segments)
    {
        bool drewSegment = false;

        for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
        {
            EnemyAcidTrailSegmentElement segment = segments[segmentIndex];

            if (segment.Radius <= 0f || segment.RemainingLifetime <= 0f)
                continue;

            primitiveDrawer.DrawWireDisc(ToVector3(segment.Position), segment.Radius, EnemyAcidTrailSegmentColor);
            drewSegment = true;
        }

        return drewSegment;
    }
    #endregion

    #region Spawners
    /// <summary>
    /// Draws pooled enemy spawner radii near the player so spawn and despawn behaviour can be validated in runtime.
    /// </summary>
    /// <param name="primitiveDrawer">Active rendering backend receiving primitive calls.</param>
    /// <param name="entityManager">Runtime entity manager used to fetch spawner components.</param>
    /// <param name="playerPosition">Runtime player position used for distance filtering.</param>
    private static void DrawSpawnerGizmos(IRuntimeGizmoPrimitiveDrawer primitiveDrawer,
                                          EntityManager entityManager,
                                          float3 playerPosition)
    {
        if (cachedWorld == null || !cachedWorld.IsCreated)
            return;

        if (spawnerQuery.IsEmptyIgnoreFilter)
            return;

        NativeArray<Entity> spawnerEntities = spawnerQuery.ToEntityArray(Allocator.Temp);
        int drawnCount = 0;

        try
        {
            // Draw only nearby spawners to avoid filling the room graph with overlapping radii.
            for (int spawnerIndex = 0; spawnerIndex < spawnerEntities.Length; spawnerIndex++)
            {
                if (drawnCount >= MaxSpawnerDrawCount)
                    break;

                Entity spawnerEntity = spawnerEntities[spawnerIndex];
                LocalTransform spawnerTransform = entityManager.GetComponentData<LocalTransform>(spawnerEntity);
                float planarDistance = math.distance(playerPosition.xz, spawnerTransform.Position.xz);

                if (planarDistance > SpawnerDrawDistance)
                    continue;

                EnemySpawner spawner = entityManager.GetComponentData<EnemySpawner>(spawnerEntity);
                Vector3 spawnerPosition = ToVector3(spawnerTransform.Position);
                bool drewSpawnerGizmo = false;

                if (RuntimeGizmoDebugState.SpawnerSpawnRadiusEnabled)
                {
                    primitiveDrawer.DrawWireDisc(spawnerPosition, 0.75f, SpawnerSpawnRadiusColor);
                    drewSpawnerGizmo = true;
                }

                if (RuntimeGizmoDebugState.SpawnerDespawnRadiusEnabled)
                {
                    float effectiveDespawnDistance = EnemySpawnerRuntimeUtility.ResolveEffectiveDespawnDistance(in spawner);
                    primitiveDrawer.DrawWireDisc(spawnerPosition, effectiveDespawnDistance, SpawnerDespawnRadiusColor);
                    drewSpawnerGizmo = true;
                }

                if (!drewSpawnerGizmo)
                    continue;

                if (RuntimeGizmoDebugState.ShowLabels)
                    primitiveDrawer.DrawLabel(spawnerPosition, "Spawner");

                drawnCount++;
            }
        }
        finally
        {
            if (spawnerEntities.IsCreated)
                spawnerEntities.Dispose();
        }
    }
    #endregion

    #region Bombs
    /// <summary>
    /// Draws active bomb explosion radii and motion vectors near the player.
    /// </summary>
    /// <param name="primitiveDrawer">Active rendering backend receiving primitive calls.</param>
    /// <param name="entityManager">Runtime entity manager used to fetch bomb components.</param>
    /// <param name="playerPosition">Runtime player position used for distance filtering.</param>
    private static void DrawBombGizmos(IRuntimeGizmoPrimitiveDrawer primitiveDrawer,
                                       EntityManager entityManager,
                                       float3 playerPosition)
    {
        if (cachedWorld == null || !cachedWorld.IsCreated)
            return;

        if (bombQuery.IsEmptyIgnoreFilter)
            return;

        NativeArray<Entity> bombEntities = bombQuery.ToEntityArray(Allocator.Temp);
        int drawnCount = 0;

        try
        {
            // Draw only bombs close enough to matter for the current combat slice.
            for (int bombIndex = 0; bombIndex < bombEntities.Length; bombIndex++)
            {
                if (drawnCount >= MaxBombDrawCount)
                    break;

                Entity bombEntity = bombEntities[bombIndex];
                LocalTransform bombTransform = entityManager.GetComponentData<LocalTransform>(bombEntity);
                float planarDistance = math.distance(playerPosition.xz, bombTransform.Position.xz);

                if (planarDistance > BombDrawDistance)
                    continue;

                BombFuseState bombFuseState = entityManager.GetComponentData<BombFuseState>(bombEntity);
                Vector3 bombPosition = ToVector3(bombTransform.Position);
                bool drewBombGizmo = false;

                if (RuntimeGizmoDebugState.BombRadiusEnabled)
                {
                    primitiveDrawer.DrawWireDisc(bombPosition, bombFuseState.Radius, BombRadiusColor);
                    drewBombGizmo = true;
                }

                if (RuntimeGizmoDebugState.BombVelocityEnabled &&
                    math.lengthsq(bombFuseState.Velocity) > DirectionMagnitudeEpsilon)
                {
                    primitiveDrawer.DrawDirection(bombPosition,
                                                  ToVector3(math.normalizesafe(bombFuseState.Velocity)),
                                                  BombVelocityLength,
                                                  BombVelocityColor);
                    drewBombGizmo = true;
                }

                if (!drewBombGizmo)
                    continue;

                if (RuntimeGizmoDebugState.ShowLabels)
                    primitiveDrawer.DrawLabel(bombPosition, "Bomb");

                drawnCount++;
            }
        }
        finally
        {
            if (bombEntities.IsCreated)
                bombEntities.Dispose();
        }
    }
    #endregion

    #region Helpers
    private static Vector3 ToVector3(float3 value)
    {
        return new Vector3(value.x, value.y, value.z);
    }
    #endregion

    #endregion
}
