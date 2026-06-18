using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

/// <summary>
/// Builds and queries the shared enemy navigation flow field derived from static wall colliders.
/// </summary>
public static class EnemyNavigationFlowFieldUtility
{
    #region Constants
    public const int InvalidCellIndex = -1;
    public const int UnreachableCellCost = int.MaxValue;

    private const float DirectionEpsilon = 1e-6f;
    private const float MinimumNavigationCellSize = 0.45f;
    private const float MaximumNavigationCellSize = 0.85f;
    private const float DefaultNavigationBodyRadius = 0.55f;
    private const float GridPaddingInCells = 2f;
    private const float MinimumWalkableClearance = 0.08f;
    private const float MaximumWalkableClearanceRatio = 0.48f;
    private const float MinimumAgentRadius = 0.12f;
    private const float AgentRadiusPadding = 0.04f;
    public const float FlowRefreshIntervalSeconds = 0.12f;
    private const int SearchRadiusWhenCellBlocked = 3;
    private const int RetreatTopologyLookAheadSteps = 5;
    private const float RetreatCostGainNormalization = 10f;
    private const float RetreatExitCountNormalization = 5f;
    private const float RetreatForwardExitNormalization = 3f;
    private const float RetreatDeadEndPenalty = 0.42f;
    private const float RetreatBacktrackPenalty = 0.24f;
    private const uint WallHashSeed = 2166136261u;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Collects one world-space AABB that encloses all static wall bodies matching the provided layer mask.
    /// </summary>
    /// <param name="physicsWorldSingleton">Physics world used to enumerate static rigid bodies.</param>
    /// <param name="wallsLayerMask">Layer mask that identifies wall colliders.</param>
    /// <param name="wallBounds">Output union bounds of all detected static wall colliders.</param>
    /// <param name="layoutHash">Output hash used to detect static wall-layout changes.</param>
    /// <returns>True when at least one matching static wall body is found; otherwise false.</returns>
    public static bool TryCollectStaticWallBounds(in PhysicsWorldSingleton physicsWorldSingleton,
                                                  int wallsLayerMask,
                                                  out Aabb wallBounds,
                                                  out uint layoutHash)
    {
        wallBounds = default;
        layoutHash = WallHashSeed;

        if (wallsLayerMask == 0)
            return false;

        NativeArray<RigidBody> staticBodies = physicsWorldSingleton.StaticBodies;
        uint wallsMask = (uint)wallsLayerMask;
        bool foundWall = false;

        // Aggregate all static wall AABBs so the navigation grid automatically tracks authored obstacles.
        for (int bodyIndex = 0; bodyIndex < staticBodies.Length; bodyIndex++)
        {
            RigidBody rigidBody = staticBodies[bodyIndex];

            if (!rigidBody.Collider.IsCreated)
                continue;

            CollisionFilter collisionFilter = rigidBody.Collider.Value.GetCollisionFilter();

            if ((collisionFilter.BelongsTo & wallsMask) == 0u)
                continue;

            Aabb bodyAabb = rigidBody.CalculateAabb();

            if (!foundWall)
            {
                wallBounds = bodyAabb;
                foundWall = true;
            }
            else
            {
                wallBounds.Min = math.min(wallBounds.Min, bodyAabb.Min);
                wallBounds.Max = math.max(wallBounds.Max, bodyAabb.Max);
            }

            uint bodyHash = math.hash(new float4(bodyAabb.Min.x,
                                                 bodyAabb.Min.z,
                                                 bodyAabb.Max.x,
                                                 bodyAabb.Max.z));
            layoutHash = math.hash(new uint3(layoutHash, (uint)rigidBody.Entity.Index, bodyHash));
        }

        return foundWall;
    }

    /// <summary>
    /// Rebuilds the navigation grid layout and all cell connectivity from static wall geometry.
    /// </summary>
    /// <param name="navigationCells">Dynamic buffer that stores one navigation cell per grid slot.</param>
    /// <param name="navigationGridState">Mutable grid-state component rebuilt in place.</param>
    /// <param name="physicsWorldSingleton">Physics world used for wall clearance and edge traversal checks.</param>
    /// <param name="wallBounds">Union bounds of all static wall colliders.</param>
    /// <param name="wallsLayerMask">Layer mask that identifies wall colliders.</param>
    /// <param name="maximumNavigationRadius">Largest enemy body-plus-wall-distance radius used to size the shared traversal clearance.</param>
    /// <param name="staticLayoutHash">Hash of the current static wall layout.</param>
    public static void RebuildGrid(ref DynamicBuffer<EnemyNavigationCellElement> navigationCells,
                                   ref EnemyNavigationGridState navigationGridState,
                                   in PhysicsWorldSingleton physicsWorldSingleton,
                                   in Aabb wallBounds,
                                   int wallsLayerMask,
                                   float maximumNavigationRadius,
                                   uint staticLayoutHash)
    {
        float resolvedBodyRadius = math.max(DefaultNavigationBodyRadius, maximumNavigationRadius);
        float cellSize = ResolveCellSize(resolvedBodyRadius);
        float2 origin = wallBounds.Min.xz - cellSize * GridPaddingInCells;
        float2 maximum = wallBounds.Max.xz + cellSize * GridPaddingInCells;
        float2 extents = maximum - origin;
        int width = math.max(1, (int)math.ceil(extents.x / cellSize));
        int height = math.max(1, (int)math.ceil(extents.y / cellSize));
        float agentRadius = ResolveAgentRadius(resolvedBodyRadius);
        float walkableClearance = ResolveWalkableClearance(agentRadius, cellSize);
        int cellCount = width * height;

        navigationCells.Clear();
        navigationCells.ResizeUninitialized(cellCount);

        navigationGridState = new EnemyNavigationGridState
        {
            Origin = origin,
            CellSize = cellSize,
            InverseCellSize = 1f / cellSize,
            AgentRadius = agentRadius,
            Width = width,
            Height = height,
            PlayerCellIndex = InvalidCellIndex,
            NextFlowRefreshTime = 0f,
            StaticLayoutHash = staticLayoutHash,
            Initialized = 1,
            FlowReady = 0
        };

        // First pass marks cells that provide enough static clearance from wall colliders.
        for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
        {
            int2 coordinates = ResolveCoordinatesFromIndex(cellIndex, width);
            float2 center = ResolveCellCenter(origin, cellSize, coordinates.x, coordinates.y);
            bool walkable = IsCellWalkable(in physicsWorldSingleton, center, walkableClearance, wallsLayerMask);
            navigationCells[cellIndex] = new EnemyNavigationCellElement
            {
                Cost = UnreachableCellCost,
                Walkable = walkable ? (byte)1 : (byte)0,
                NeighborMask = 0,
                FlowDirection = float2.zero
            };
        }

        // Second pass evaluates static edge connectivity so runtime flow-field refreshes only touch costs and directions.
        for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
        {
            EnemyNavigationCellElement cell = navigationCells[cellIndex];

            if (cell.Walkable == 0)
            {
                navigationCells[cellIndex] = cell;
                continue;
            }

            int2 coordinates = ResolveCoordinatesFromIndex(cellIndex, width);
            cell.NeighborMask = ResolveNeighborMask(in physicsWorldSingleton,
                                                    navigationCells,
                                                    origin,
                                                    cellSize,
                                                    width,
                                                    height,
                                                    coordinates.x,
                                                    coordinates.y,
                                                    agentRadius,
                                                    wallsLayerMask);
            navigationCells[cellIndex] = cell;
        }
    }

    /// <summary>
    /// Resolves the walkable player cell and rebuilds the flow field when a valid target cell is available.
    /// </summary>
    /// <param name="navigationCells">Dynamic buffer that stores navigation cells.</param>
    /// <param name="navigationGridState">Mutable grid-state component updated in place.</param>
    /// <param name="playerPosition">Current player world position.</param>
    /// <returns>True when the flow field is ready for runtime navigation queries; otherwise false.</returns>
    public static bool TryRefreshFlowField(ref DynamicBuffer<EnemyNavigationCellElement> navigationCells,
                                           ref EnemyNavigationGridState navigationGridState,
                                           float3 playerPosition)
    {
        if (navigationGridState.Initialized == 0)
            return false;

        if (!TryResolveBestCellIndex(playerPosition, in navigationGridState, navigationCells, out int playerCellIndex))
        {
            navigationGridState.PlayerCellIndex = InvalidCellIndex;
            navigationGridState.FlowReady = 0;
            return false;
        }

        navigationGridState.PlayerCellIndex = playerCellIndex;
        RebuildFlowField(ref navigationCells, in navigationGridState, playerCellIndex);
        navigationGridState.FlowReady = navigationCells.Length > 0 ? (byte)1 : (byte)0;
        return navigationGridState.FlowReady != 0;
    }

    /// <summary>
    /// Returns whether the shared flow field should be refreshed at the current time.
    /// </summary>
    /// <param name="navigationGridState">Current grid-state component.</param>
    /// <param name="playerPosition">Current player world position.</param>
    /// <param name="elapsedTime">Current world elapsed time.</param>
    /// <returns>True when the flow field should be rebuilt this frame; otherwise false.</returns>
    public static bool ShouldRefreshFlowField(in EnemyNavigationGridState navigationGridState,
                                              float3 playerPosition,
                                              float elapsedTime)
    {
        if (navigationGridState.Initialized == 0)
            return false;

        if (navigationGridState.FlowReady == 0)
            return true;

        if (elapsedTime >= navigationGridState.NextFlowRefreshTime)
            return true;

        if (!TryResolveCellCoordinates(playerPosition, in navigationGridState, out int playerCellX, out int playerCellY))
            return true;

        int playerCellIndex = ResolveCellIndex(playerCellX, playerCellY, navigationGridState.Width);
        return playerCellIndex != navigationGridState.PlayerCellIndex;
    }

    /// <summary>
    /// Resolves one navigation-aware desired velocity toward the player by preferring direct line of sight and falling back to the shared flow field.
    /// </summary>
    /// <param name="currentPosition">Current enemy world position.</param>
    /// <param name="targetPosition">Current player world position.</param>
    /// <param name="collisionRadius">Current enemy navigation collision radius used for direct-path wall checks.</param>
    /// <param name="desiredSpeed">Desired movement speed before acceleration integration.</param>
    /// <param name="physicsWorldSingleton">Physics world used for direct-path wall checks.</param>
    /// <param name="wallsLayerMask">Layer mask that identifies wall colliders.</param>
    /// <param name="navigationGridState">Current shared navigation-grid state.</param>
    /// <param name="navigationCells">Current shared navigation cells buffer.</param>
    /// <param name="desiredVelocity">Output navigation-aware desired velocity.</param>
    /// <returns>True when a valid navigation velocity is produced; otherwise false.</returns>
    public static bool TryResolveNavigationVelocity(float3 currentPosition,
                                                    float3 targetPosition,
                                                    float collisionRadius,
                                                    float desiredSpeed,
                                                    in PhysicsWorldSingleton physicsWorldSingleton,
                                                    int wallsLayerMask,
                                                    in EnemyNavigationGridState navigationGridState,
                                                    DynamicBuffer<EnemyNavigationCellElement> navigationCells,
                                                    out float3 desiredVelocity)
    {
        desiredVelocity = float3.zero;

        if (navigationGridState.FlowReady == 0)
            return false;

        if (desiredSpeed <= DirectionEpsilon)
            return false;

        float3 toTarget = targetPosition - currentPosition;
        toTarget.y = 0f;
        float targetDistance = math.length(toTarget);

        if (targetDistance <= DirectionEpsilon)
            return false;

        float navigationRadius = math.max(math.max(collisionRadius, navigationGridState.AgentRadius), MinimumAgentRadius);
        bool directPathBlocked = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                                         currentPosition,
                                                                                         toTarget,
                                                                                         navigationRadius,
                                                                                         wallsLayerMask,
                                                                                         out float3 allowedDisplacement,
                                                                                         out float3 _);

        if (!directPathBlocked || math.lengthsq(allowedDisplacement) >= math.lengthsq(toTarget) * 0.95f)
        {
            float3 directDirection = toTarget / math.max(targetDistance, DirectionEpsilon);
            desiredVelocity = directDirection * desiredSpeed;
            return true;
        }

        if (!TryResolveBestCellIndex(currentPosition, in navigationGridState, navigationCells, out int cellIndex))
            return false;

        EnemyNavigationCellElement navigationCell = navigationCells[cellIndex];

        if (navigationCell.Cost == UnreachableCellCost)
            return false;

        float2 flowDirection = navigationCell.FlowDirection;

        if (math.lengthsq(flowDirection) <= DirectionEpsilon)
            return false;

        desiredVelocity = new float3(flowDirection.x, 0f, flowDirection.y) * desiredSpeed;
        return true;
    }

    #region Job-Facing NativeArray Overloads
    // Burst-job-facing overloads that accept the navigation cells as a NativeArray (obtained via DynamicBuffer.AsNativeArray()),
    // so EnemySteeringSystem can resolve navigation velocity from inside a parallel job. Logic mirrors the DynamicBuffer paths above.

    /// <summary>
    /// NativeArray overload of <see cref="TryResolveNavigationVelocity(float3, float3, float, float, in PhysicsWorldSingleton, int, in EnemyNavigationGridState, DynamicBuffer{EnemyNavigationCellElement}, out float3)"/> usable inside Burst jobs.
    /// </summary>
    public static bool TryResolveNavigationVelocity(float3 currentPosition,
                                                    float3 targetPosition,
                                                    float collisionRadius,
                                                    float desiredSpeed,
                                                    in PhysicsWorldSingleton physicsWorldSingleton,
                                                    int wallsLayerMask,
                                                    in EnemyNavigationGridState navigationGridState,
                                                    NativeArray<EnemyNavigationCellElement> navigationCells,
                                                    out float3 desiredVelocity)
    {
        desiredVelocity = float3.zero;

        if (navigationGridState.FlowReady == 0)
            return false;

        if (desiredSpeed <= DirectionEpsilon)
            return false;

        float3 toTarget = targetPosition - currentPosition;
        toTarget.y = 0f;
        float targetDistance = math.length(toTarget);

        if (targetDistance <= DirectionEpsilon)
            return false;

        float navigationRadius = math.max(math.max(collisionRadius, navigationGridState.AgentRadius), MinimumAgentRadius);
        bool directPathBlocked = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                                         currentPosition,
                                                                                         toTarget,
                                                                                         navigationRadius,
                                                                                         wallsLayerMask,
                                                                                         out float3 allowedDisplacement,
                                                                                         out float3 _);

        if (!directPathBlocked || math.lengthsq(allowedDisplacement) >= math.lengthsq(toTarget) * 0.95f)
        {
            float3 directDirection = toTarget / math.max(targetDistance, DirectionEpsilon);
            desiredVelocity = directDirection * desiredSpeed;
            return true;
        }

        if (!TryResolveBestCellIndex(currentPosition, in navigationGridState, navigationCells, out int cellIndex))
            return false;

        EnemyNavigationCellElement navigationCell = navigationCells[cellIndex];

        if (navigationCell.Cost == UnreachableCellCost)
            return false;

        float2 flowDirection = navigationCell.FlowDirection;

        if (math.lengthsq(flowDirection) <= DirectionEpsilon)
            return false;

        desiredVelocity = new float3(flowDirection.x, 0f, flowDirection.y) * desiredSpeed;
        return true;
    }

    /// <summary>
    /// NativeArray overload of best-walkable-cell resolution usable inside Burst jobs.
    /// </summary>
    private static bool TryResolveBestCellIndex(float3 worldPosition,
                                                in EnemyNavigationGridState navigationGridState,
                                                NativeArray<EnemyNavigationCellElement> navigationCells,
                                                out int cellIndex)
    {
        cellIndex = InvalidCellIndex;

        if (!TryResolveCellCoordinates(worldPosition, in navigationGridState, out int cellX, out int cellY))
            return false;

        int directCellIndex = ResolveCellIndex(cellX, cellY, navigationGridState.Width);

        if (navigationCells[directCellIndex].Walkable != 0)
        {
            cellIndex = directCellIndex;
            return true;
        }

        return TryResolveNearbyWalkableCell(cellX,
                                            cellY,
                                            in navigationGridState,
                                            navigationCells,
                                            out cellIndex);
    }

    /// <summary>
    /// NativeArray overload of nearby-walkable-cell recovery search usable inside Burst jobs.
    /// </summary>
    private static bool TryResolveNearbyWalkableCell(int sourceCellX,
                                                     int sourceCellY,
                                                     in EnemyNavigationGridState navigationGridState,
                                                     NativeArray<EnemyNavigationCellElement> navigationCells,
                                                     out int cellIndex)
    {
        cellIndex = InvalidCellIndex;
        int bestDistanceSquared = int.MaxValue;

        // Search a small local area first to recover from cells near wall boundaries without scanning the whole grid.
        for (int radius = 1; radius <= SearchRadiusWhenCellBlocked; radius++)
        {
            bool foundCandidateAtCurrentRadius = false;

            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    int candidateX = sourceCellX + offsetX;
                    int candidateY = sourceCellY + offsetY;

                    if (!IsInsideGrid(candidateX, candidateY, navigationGridState.Width, navigationGridState.Height))
                        continue;

                    int candidateIndex = ResolveCellIndex(candidateX, candidateY, navigationGridState.Width);

                    if (navigationCells[candidateIndex].Walkable == 0)
                        continue;

                    int distanceSquared = offsetX * offsetX + offsetY * offsetY;

                    if (distanceSquared >= bestDistanceSquared)
                        continue;

                    bestDistanceSquared = distanceSquared;
                    cellIndex = candidateIndex;
                    foundCandidateAtCurrentRadius = true;
                }
            }

            if (foundCandidateAtCurrentRadius)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Detects whether a world position lies outside the walkable navigation area (outside the grid rect, or on a
    /// blocked cell) and, if so, resolves the nearest walkable cell center to pull the agent back in. Burst-job-safe.
    /// </summary>
    /// <param name="currentPosition">Agent world position to test.</param>
    /// <param name="navigationGridState">Current shared navigation-grid state.</param>
    /// <param name="navigationCells">Navigation cells as a NativeArray (e.g. buffer.AsNativeArray()).</param>
    /// <param name="recoveredPosition">Nearest walkable world position when out of bounds; unchanged otherwise.</param>
    /// <returns>True when the position was out of bounds and a walkable recovery target was resolved.</returns>
    public static bool TryResolveOutOfBoundsRecovery(float3 currentPosition,
                                                     in EnemyNavigationGridState navigationGridState,
                                                     NativeArray<EnemyNavigationCellElement> navigationCells,
                                                     out float3 recoveredPosition)
    {
        recoveredPosition = currentPosition;

        if (navigationGridState.FlowReady == 0 || navigationCells.Length == 0)
            return false;

        // Reconstruct the static wall bounding box from the padded grid (grid origin/max sit GridPaddingInCells cells
        // outside the wall AABB; see grid build). Only positions BEYOND the walls are treated as out of bounds, so
        // enemies that merely brush against interior walls (on clearance-blocked cells) are never falsely teleported.
        float2 padding = navigationGridState.CellSize * GridPaddingInCells;
        float2 wallMinimum = navigationGridState.Origin + padding;
        float2 wallMaximum = navigationGridState.Origin
                             + new float2(navigationGridState.Width, navigationGridState.Height) * navigationGridState.CellSize
                             - padding;

        if (wallMaximum.x <= wallMinimum.x || wallMaximum.y <= wallMinimum.y)
            return false;

        float2 planarPosition = new float2(currentPosition.x, currentPosition.z);
        bool beyondWalls = planarPosition.x < wallMinimum.x ||
                           planarPosition.y < wallMinimum.y ||
                           planarPosition.x > wallMaximum.x ||
                           planarPosition.y > wallMaximum.y;

        if (!beyondWalls)
            return false;

        // Escaped beyond the arena: pull back to the nearest walkable cell, searching inward from the clamped edge cell.
        int rawCellX = (int)math.floor((currentPosition.x - navigationGridState.Origin.x) * navigationGridState.InverseCellSize);
        int rawCellY = (int)math.floor((currentPosition.z - navigationGridState.Origin.y) * navigationGridState.InverseCellSize);
        int searchCellX = math.clamp(rawCellX, 0, navigationGridState.Width - 1);
        int searchCellY = math.clamp(rawCellY, 0, navigationGridState.Height - 1);

        if (TryResolveNearbyWalkableCell(searchCellX, searchCellY, in navigationGridState, navigationCells, out int recoveredCellIndex))
        {
            int2 recoveredCoords = ResolveCoordinatesFromIndex(recoveredCellIndex, navigationGridState.Width);
            float2 recoveredCenter = ResolveCellCenter(navigationGridState.Origin,
                                                       navigationGridState.CellSize,
                                                       recoveredCoords.x,
                                                       recoveredCoords.y);
            recoveredPosition = new float3(recoveredCenter.x, currentPosition.y, recoveredCenter.y);
            return true;
        }

        // Fallback when no nearby walkable cell is found: clamp into the wall bounds so the agent is back inside the arena.
        float2 clampedPosition = math.clamp(planarPosition, wallMinimum, wallMaximum);
        recoveredPosition = new float3(clampedPosition.x, currentPosition.y, clampedPosition.y);
        return true;
    }
    #endregion

    /// <summary>
    /// Resolves one navigation-aware retreat velocity by following the opposite direction of the player-targeting flow field.
    /// </summary>
    /// <param name="currentPosition">Current enemy world position.</param>
    /// <param name="desiredSpeed">Desired movement speed before acceleration integration.</param>
    /// <param name="navigationGridState">Current shared navigation-grid state.</param>
    /// <param name="navigationCells">Current shared navigation cells buffer.</param>
    /// <param name="desiredVelocity">Output navigation-aware retreat velocity.</param>
    /// <returns>True when a valid retreat velocity is produced; otherwise false.</returns>
    public static bool TryResolveRetreatNavigationVelocity(float3 currentPosition,
                                                           float desiredSpeed,
                                                           in EnemyNavigationGridState navigationGridState,
                                                           DynamicBuffer<EnemyNavigationCellElement> navigationCells,
                                                           out float3 desiredVelocity)
    {
        desiredVelocity = float3.zero;

        if (navigationGridState.FlowReady == 0)
            return false;

        if (desiredSpeed <= DirectionEpsilon)
            return false;

        if (!TryResolveBestCellIndex(currentPosition, in navigationGridState, navigationCells, out int cellIndex))
            return false;

        EnemyNavigationCellElement navigationCell = navigationCells[cellIndex];

        if (navigationCell.Cost == UnreachableCellCost)
            return false;

        if (TryResolveBestRetreatDirection(cellIndex,
                                           in navigationGridState,
                                           navigationCells,
                                           out float2 smartRetreatDirection))
        {
            desiredVelocity = new float3(smartRetreatDirection.x, 0f, smartRetreatDirection.y) * desiredSpeed;
            return true;
        }

        float2 flowDirection = navigationCell.FlowDirection;
        float2 retreatDirection = -math.normalizesafe(flowDirection, float2.zero);

        if (math.lengthsq(retreatDirection) <= DirectionEpsilon)
            return false;

        desiredVelocity = new float3(retreatDirection.x, 0f, retreatDirection.y) * desiredSpeed;
        return true;
    }

    /// <summary>
    /// Estimates how safe a world position is for retreat by looking ahead through the navigation topology.
    /// </summary>
    /// <param name="worldPosition">World position to evaluate.</param>
    /// <param name="navigationGridState">Current shared navigation-grid state.</param>
    /// <param name="navigationCells">Current shared navigation cells buffer.</param>
    /// <param name="topologyScore">Output normalized safety score that prefers deep corridors with multiple exits.</param>
    /// <returns>True when a valid topology score is produced; otherwise false.</returns>
    public static bool TryResolveRetreatTopologyScore(float3 worldPosition,
                                                      in EnemyNavigationGridState navigationGridState,
                                                      DynamicBuffer<EnemyNavigationCellElement> navigationCells,
                                                      out float topologyScore)
    {
        topologyScore = 0f;

        if (navigationGridState.FlowReady == 0)
            return false;

        if (!TryResolveBestCellIndex(worldPosition, in navigationGridState, navigationCells, out int cellIndex))
            return false;

        EnemyNavigationCellElement navigationCell = navigationCells[cellIndex];

        if (navigationCell.Cost == UnreachableCellCost || navigationCell.Walkable == 0)
            return false;

        topologyScore = EvaluateRetreatTopologyScore(cellIndex,
                                                     InvalidCellIndex,
                                                     navigationCell.Cost,
                                                     in navigationGridState,
                                                     navigationCells);
        return true;
    }

    /// <summary>
    /// Resolves the current navigation cost of one world position using the shared flow field.
    /// </summary>
    /// <param name="worldPosition">World position to evaluate.</param>
    /// <param name="navigationGridState">Current shared navigation-grid state.</param>
    /// <param name="navigationCells">Current shared navigation cells buffer.</param>
    /// <param name="navigationCost">Output navigation cost in cell steps from the player.</param>
    /// <returns>True when a valid reachable navigation cost is resolved; otherwise false.</returns>
    public static bool TryResolveNavigationCost(float3 worldPosition,
                                                in EnemyNavigationGridState navigationGridState,
                                                DynamicBuffer<EnemyNavigationCellElement> navigationCells,
                                                out int navigationCost)
    {
        navigationCost = UnreachableCellCost;

        if (navigationGridState.FlowReady == 0)
            return false;

        if (!TryResolveBestCellIndex(worldPosition, in navigationGridState, navigationCells, out int cellIndex))
            return false;

        navigationCost = navigationCells[cellIndex].Cost;
        return navigationCost != UnreachableCellCost;
    }
    #endregion

    #region Grid Build
    private static float ResolveCellSize(float maximumEnemyBodyRadius)
    {
        float resolvedRadius = math.max(DefaultNavigationBodyRadius, maximumEnemyBodyRadius);
        return math.clamp(resolvedRadius * 1.15f, MinimumNavigationCellSize, MaximumNavigationCellSize);
    }

    private static float ResolveAgentRadius(float maximumEnemyBodyRadius)
    {
        return math.max(MinimumAgentRadius, maximumEnemyBodyRadius + AgentRadiusPadding);
    }

    private static float ResolveWalkableClearance(float agentRadius, float cellSize)
    {
        float maximumClearance = cellSize * MaximumWalkableClearanceRatio;
        return math.max(MinimumWalkableClearance, math.min(maximumClearance, agentRadius));
    }

    private static bool IsCellWalkable(in PhysicsWorldSingleton physicsWorldSingleton,
                                       float2 cellCenter,
                                       float clearanceRadius,
                                       int wallsLayerMask)
    {
        float3 worldPosition = new float3(cellCenter.x, 0f, cellCenter.y);
        return !WorldWallCollisionUtility.TryResolveMinimumClearance(physicsWorldSingleton,
                                                                     worldPosition,
                                                                     clearanceRadius,
                                                                     wallsLayerMask,
                                                                     out float3 _,
                                                                     out float3 _);
    }

    private static byte ResolveNeighborMask(in PhysicsWorldSingleton physicsWorldSingleton,
                                            DynamicBuffer<EnemyNavigationCellElement> navigationCells,
                                            float2 origin,
                                            float cellSize,
                                            int width,
                                            int height,
                                            int cellX,
                                            int cellY,
                                            float agentRadius,
                                            int wallsLayerMask)
    {
        byte neighborMask = 0;
        float2 currentCenter = ResolveCellCenter(origin, cellSize, cellX, cellY);

        // Evaluate all eight directions once so runtime flow refreshes can reuse static connectivity.
        for (int directionIndex = 0; directionIndex < 8; directionIndex++)
        {
            int2 offset = ResolveNeighborOffset(directionIndex);
            int neighborX = cellX + offset.x;
            int neighborY = cellY + offset.y;

            if (!IsInsideGrid(neighborX, neighborY, width, height))
                continue;

            int neighborIndex = ResolveCellIndex(neighborX, neighborY, width);

            if (navigationCells[neighborIndex].Walkable == 0)
                continue;

            if (IsDiagonalOffset(offset))
            {
                int firstCardinalIndex = ResolveCellIndex(cellX + offset.x, cellY, width);
                int secondCardinalIndex = ResolveCellIndex(cellX, cellY + offset.y, width);

                if (navigationCells[firstCardinalIndex].Walkable == 0 ||
                    navigationCells[secondCardinalIndex].Walkable == 0)
                {
                    continue;
                }
            }

            float2 neighborCenter = ResolveCellCenter(origin, cellSize, neighborX, neighborY);

            if (!CanTraverseBetweenCells(in physicsWorldSingleton, currentCenter, neighborCenter, agentRadius, wallsLayerMask))
                continue;

            neighborMask |= (byte)(1 << directionIndex);
        }

        return neighborMask;
    }

    private static bool CanTraverseBetweenCells(in PhysicsWorldSingleton physicsWorldSingleton,
                                                float2 startCenter,
                                                float2 endCenter,
                                                float agentRadius,
                                                int wallsLayerMask)
    {
        float3 startPosition = new float3(startCenter.x, 0f, startCenter.y);
        float3 displacement = new float3(endCenter.x - startCenter.x, 0f, endCenter.y - startCenter.y);
        return !WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                        startPosition,
                                                                        displacement,
                                                                        agentRadius,
                                                                        wallsLayerMask,
                                                                        out float3 _,
                                                                        out float3 _);
    }
    #endregion

    #region Flow Build
    private static void RebuildFlowField(ref DynamicBuffer<EnemyNavigationCellElement> navigationCells,
                                         in EnemyNavigationGridState navigationGridState,
                                         int playerCellIndex)
    {
        int cellCount = navigationCells.Length;

        if (cellCount <= 0)
            return;

        NativeArray<int> queue = new NativeArray<int>(cellCount, Allocator.Temp);
        int queueHead = 0;
        int queueTail = 0;

        // Reset all runtime costs and flow vectors before flood-filling from the player cell.
        for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
        {
            EnemyNavigationCellElement cell = navigationCells[cellIndex];
            cell.Cost = cell.Walkable != 0 ? UnreachableCellCost : UnreachableCellCost;
            cell.FlowDirection = float2.zero;
            navigationCells[cellIndex] = cell;
        }

        if (playerCellIndex < 0 || playerCellIndex >= cellCount || navigationCells[playerCellIndex].Walkable == 0)
        {
            queue.Dispose();
            return;
        }

        EnemyNavigationCellElement playerCell = navigationCells[playerCellIndex];
        playerCell.Cost = 0;
        navigationCells[playerCellIndex] = playerCell;
        queue[queueTail] = playerCellIndex;
        queueTail++;

        // Uniform-cost flood fill is sufficient because all traversable cell edges share the same movement weight.
        while (queueHead < queueTail)
        {
            int currentIndex = queue[queueHead];
            queueHead++;
            EnemyNavigationCellElement currentCell = navigationCells[currentIndex];
            int2 currentCoordinates = ResolveCoordinatesFromIndex(currentIndex, navigationGridState.Width);

            for (int directionIndex = 0; directionIndex < 8; directionIndex++)
            {
                if ((currentCell.NeighborMask & (1 << directionIndex)) == 0)
                    continue;

                int2 offset = ResolveNeighborOffset(directionIndex);
                int neighborX = currentCoordinates.x + offset.x;
                int neighborY = currentCoordinates.y + offset.y;

                if (!IsInsideGrid(neighborX, neighborY, navigationGridState.Width, navigationGridState.Height))
                    continue;

                int neighborIndex = ResolveCellIndex(neighborX, neighborY, navigationGridState.Width);
                EnemyNavigationCellElement neighborCell = navigationCells[neighborIndex];

                if (neighborCell.Walkable == 0)
                    continue;

                int nextCost = currentCell.Cost + 1;

                if (nextCost >= neighborCell.Cost)
                    continue;

                neighborCell.Cost = nextCost;
                navigationCells[neighborIndex] = neighborCell;
                queue[queueTail] = neighborIndex;
                queueTail++;
            }
        }

        // Convert the scalar cost field into one normalized planar direction per cell.
        for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
        {
            EnemyNavigationCellElement currentCell = navigationCells[cellIndex];

            if (currentCell.Walkable == 0 || currentCell.Cost == UnreachableCellCost)
            {
                currentCell.FlowDirection = float2.zero;
                navigationCells[cellIndex] = currentCell;
                continue;
            }

            if (currentCell.Cost == 0)
            {
                currentCell.FlowDirection = float2.zero;
                navigationCells[cellIndex] = currentCell;
                continue;
            }

            int2 currentCoordinates = ResolveCoordinatesFromIndex(cellIndex, navigationGridState.Width);
            int bestNeighborCost = currentCell.Cost;
            float2 currentCenter = ResolveCellCenter(navigationGridState.Origin,
                                                     navigationGridState.CellSize,
                                                     currentCoordinates.x,
                                                     currentCoordinates.y);
            float2 bestDirection = float2.zero;

            for (int directionIndex = 0; directionIndex < 8; directionIndex++)
            {
                if ((currentCell.NeighborMask & (1 << directionIndex)) == 0)
                    continue;

                int2 offset = ResolveNeighborOffset(directionIndex);
                int neighborX = currentCoordinates.x + offset.x;
                int neighborY = currentCoordinates.y + offset.y;

                if (!IsInsideGrid(neighborX, neighborY, navigationGridState.Width, navigationGridState.Height))
                    continue;

                int neighborIndex = ResolveCellIndex(neighborX, neighborY, navigationGridState.Width);
                EnemyNavigationCellElement neighborCell = navigationCells[neighborIndex];

                if (neighborCell.Cost >= bestNeighborCost)
                    continue;

                float2 neighborCenter = ResolveCellCenter(navigationGridState.Origin,
                                                          navigationGridState.CellSize,
                                                          neighborX,
                                                          neighborY);
                bestNeighborCost = neighborCell.Cost;
                bestDirection = math.normalizesafe(neighborCenter - currentCenter, float2.zero);
            }

            currentCell.FlowDirection = bestDirection;
            navigationCells[cellIndex] = currentCell;
        }

        queue.Dispose();
    }
    #endregion

    #region Runtime Query
    private static bool TryResolveBestCellIndex(float3 worldPosition,
                                                in EnemyNavigationGridState navigationGridState,
                                                DynamicBuffer<EnemyNavigationCellElement> navigationCells,
                                                out int cellIndex)
    {
        cellIndex = InvalidCellIndex;

        if (!TryResolveCellCoordinates(worldPosition, in navigationGridState, out int cellX, out int cellY))
            return false;

        int directCellIndex = ResolveCellIndex(cellX, cellY, navigationGridState.Width);

        if (navigationCells[directCellIndex].Walkable != 0)
        {
            cellIndex = directCellIndex;
            return true;
        }

        return TryResolveNearbyWalkableCell(cellX,
                                            cellY,
                                            in navigationGridState,
                                            navigationCells,
                                            out cellIndex);
    }

    private static bool TryResolveNearbyWalkableCell(int sourceCellX,
                                                     int sourceCellY,
                                                     in EnemyNavigationGridState navigationGridState,
                                                     DynamicBuffer<EnemyNavigationCellElement> navigationCells,
                                                     out int cellIndex)
    {
        cellIndex = InvalidCellIndex;
        int bestDistanceSquared = int.MaxValue;

        // Search a small local area first to recover from cells near wall boundaries without scanning the whole grid.
        for (int radius = 1; radius <= SearchRadiusWhenCellBlocked; radius++)
        {
            bool foundCandidateAtCurrentRadius = false;

            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    int candidateX = sourceCellX + offsetX;
                    int candidateY = sourceCellY + offsetY;

                    if (!IsInsideGrid(candidateX, candidateY, navigationGridState.Width, navigationGridState.Height))
                        continue;

                    int candidateIndex = ResolveCellIndex(candidateX, candidateY, navigationGridState.Width);

                    if (navigationCells[candidateIndex].Walkable == 0)
                        continue;

                    int distanceSquared = offsetX * offsetX + offsetY * offsetY;

                    if (distanceSquared >= bestDistanceSquared)
                        continue;

                    bestDistanceSquared = distanceSquared;
                    cellIndex = candidateIndex;
                    foundCandidateAtCurrentRadius = true;
                }
            }

            if (foundCandidateAtCurrentRadius)
                return true;
        }

        return false;
    }

    private static bool TryResolveBestRetreatDirection(int currentCellIndex,
                                                       in EnemyNavigationGridState navigationGridState,
                                                       DynamicBuffer<EnemyNavigationCellElement> navigationCells,
                                                       out float2 retreatDirection)
    {
        retreatDirection = float2.zero;
        EnemyNavigationCellElement currentCell = navigationCells[currentCellIndex];

        if (currentCell.Walkable == 0 || currentCell.Cost == UnreachableCellCost)
            return false;

        int2 currentCoordinates = ResolveCoordinatesFromIndex(currentCellIndex, navigationGridState.Width);
        float2 currentCenter = ResolveCellCenter(navigationGridState.Origin,
                                                 navigationGridState.CellSize,
                                                 currentCoordinates.x,
                                                 currentCoordinates.y);
        float bestScore = float.NegativeInfinity;
        bool foundCandidate = false;

        for (int directionIndex = 0; directionIndex < 8; directionIndex++)
        {
            if ((currentCell.NeighborMask & (1 << directionIndex)) == 0)
                continue;

            int2 offset = ResolveNeighborOffset(directionIndex);
            int neighborX = currentCoordinates.x + offset.x;
            int neighborY = currentCoordinates.y + offset.y;

            if (!IsInsideGrid(neighborX, neighborY, navigationGridState.Width, navigationGridState.Height))
                continue;

            int neighborIndex = ResolveCellIndex(neighborX, neighborY, navigationGridState.Width);
            EnemyNavigationCellElement neighborCell = navigationCells[neighborIndex];

            if (neighborCell.Walkable == 0 || neighborCell.Cost == UnreachableCellCost)
                continue;

            float candidateScore = EvaluateRetreatTopologyScore(neighborIndex,
                                                                currentCellIndex,
                                                                currentCell.Cost,
                                                                in navigationGridState,
                                                                navigationCells);
            int costDelta = neighborCell.Cost - currentCell.Cost;

            if (costDelta > 0)
                candidateScore += math.saturate(costDelta / 4f) * 0.42f;
            else if (costDelta < 0)
                candidateScore -= math.saturate(-costDelta / 3f) * 0.58f;

            if (candidateScore <= bestScore)
                continue;

            float2 neighborCenter = ResolveCellCenter(navigationGridState.Origin,
                                                      navigationGridState.CellSize,
                                                      neighborX,
                                                      neighborY);
            retreatDirection = math.normalizesafe(neighborCenter - currentCenter, float2.zero);
            bestScore = candidateScore;
            foundCandidate = true;
        }

        return foundCandidate && math.lengthsq(retreatDirection) > DirectionEpsilon;
    }

    private static float EvaluateRetreatTopologyScore(int startCellIndex,
                                                      int previousCellIndex,
                                                      int referenceCost,
                                                      in EnemyNavigationGridState navigationGridState,
                                                      DynamicBuffer<EnemyNavigationCellElement> navigationCells)
    {
        if (startCellIndex < 0 || startCellIndex >= navigationCells.Length)
            return 0f;

        int currentCellIndex = startCellIndex;
        int lastCellIndex = previousCellIndex;
        float accumulatedScore = 0f;
        int traversedStepCount = 0;

        for (int stepIndex = 0; stepIndex < RetreatTopologyLookAheadSteps; stepIndex++)
        {
            EnemyNavigationCellElement currentCell = navigationCells[currentCellIndex];

            if (currentCell.Walkable == 0 || currentCell.Cost == UnreachableCellCost)
                break;

            ResolveRetreatCellMetrics(currentCellIndex,
                                      lastCellIndex,
                                      in navigationGridState,
                                      navigationCells,
                                      out int exitCount,
                                      out int forwardExitCount,
                                      out int nextCellIndex,
                                      out int nextCellCost);

            float costGainScore = math.saturate((currentCell.Cost - referenceCost) / RetreatCostGainNormalization);
            float exitScore = math.saturate((exitCount - 1f) / RetreatExitCountNormalization);
            float forwardExitScore = math.saturate(forwardExitCount / RetreatForwardExitNormalization);
            float localScore = costGainScore * 0.5f +
                               exitScore * 0.24f +
                               forwardExitScore * 0.26f;

            if (exitCount <= 1)
                localScore -= RetreatDeadEndPenalty;
            else if (forwardExitCount <= 0)
                localScore -= RetreatDeadEndPenalty * 0.45f;

            accumulatedScore += math.saturate(localScore);
            traversedStepCount++;

            if (nextCellIndex == InvalidCellIndex)
                break;

            if (nextCellCost < currentCell.Cost && lastCellIndex != InvalidCellIndex)
                accumulatedScore = math.max(0f, accumulatedScore - RetreatBacktrackPenalty);

            lastCellIndex = currentCellIndex;
            currentCellIndex = nextCellIndex;
        }

        if (traversedStepCount <= 0)
            return 0f;

        float depthScore = math.saturate(traversedStepCount / (float)RetreatTopologyLookAheadSteps);
        float averageScore = accumulatedScore / traversedStepCount;
        return math.saturate(averageScore * 0.78f + depthScore * 0.22f);
    }

    private static void ResolveRetreatCellMetrics(int cellIndex,
                                                  int previousCellIndex,
                                                  in EnemyNavigationGridState navigationGridState,
                                                  DynamicBuffer<EnemyNavigationCellElement> navigationCells,
                                                  out int exitCount,
                                                  out int forwardExitCount,
                                                  out int nextCellIndex,
                                                  out int nextCellCost)
    {
        exitCount = 0;
        forwardExitCount = 0;
        nextCellIndex = InvalidCellIndex;
        nextCellCost = UnreachableCellCost;
        EnemyNavigationCellElement currentCell = navigationCells[cellIndex];
        int2 currentCoordinates = ResolveCoordinatesFromIndex(cellIndex, navigationGridState.Width);
        float bestNextScore = float.NegativeInfinity;

        for (int directionIndex = 0; directionIndex < 8; directionIndex++)
        {
            if ((currentCell.NeighborMask & (1 << directionIndex)) == 0)
                continue;

            int2 offset = ResolveNeighborOffset(directionIndex);
            int neighborX = currentCoordinates.x + offset.x;
            int neighborY = currentCoordinates.y + offset.y;

            if (!IsInsideGrid(neighborX, neighborY, navigationGridState.Width, navigationGridState.Height))
                continue;

            int neighborIndex = ResolveCellIndex(neighborX, neighborY, navigationGridState.Width);
            EnemyNavigationCellElement neighborCell = navigationCells[neighborIndex];

            if (neighborCell.Walkable == 0 || neighborCell.Cost == UnreachableCellCost)
                continue;

            exitCount++;
            int costDelta = neighborCell.Cost - currentCell.Cost;

            if (costDelta > 0)
                forwardExitCount++;

            int neighborExitCount = CountReachableNeighborCount(neighborIndex,
                                                                in navigationGridState,
                                                                navigationCells);
            float candidateScore = costDelta * 0.55f +
                                   math.saturate((neighborExitCount - 1f) / RetreatExitCountNormalization) * 0.35f;

            if (neighborIndex == previousCellIndex)
                candidateScore -= 0.6f;

            if (candidateScore <= bestNextScore)
                continue;

            bestNextScore = candidateScore;
            nextCellIndex = neighborIndex;
            nextCellCost = neighborCell.Cost;
        }
    }

    private static int CountReachableNeighborCount(int cellIndex,
                                                   in EnemyNavigationGridState navigationGridState,
                                                   DynamicBuffer<EnemyNavigationCellElement> navigationCells)
    {
        EnemyNavigationCellElement currentCell = navigationCells[cellIndex];

        if (currentCell.Walkable == 0 || currentCell.Cost == UnreachableCellCost)
            return 0;

        int2 currentCoordinates = ResolveCoordinatesFromIndex(cellIndex, navigationGridState.Width);
        int reachableNeighborCount = 0;

        for (int directionIndex = 0; directionIndex < 8; directionIndex++)
        {
            if ((currentCell.NeighborMask & (1 << directionIndex)) == 0)
                continue;

            int2 offset = ResolveNeighborOffset(directionIndex);
            int neighborX = currentCoordinates.x + offset.x;
            int neighborY = currentCoordinates.y + offset.y;

            if (!IsInsideGrid(neighborX, neighborY, navigationGridState.Width, navigationGridState.Height))
                continue;

            int neighborIndex = ResolveCellIndex(neighborX, neighborY, navigationGridState.Width);
            EnemyNavigationCellElement neighborCell = navigationCells[neighborIndex];

            if (neighborCell.Walkable == 0 || neighborCell.Cost == UnreachableCellCost)
                continue;

            reachableNeighborCount++;
        }

        return reachableNeighborCount;
    }

    private static bool TryResolveCellCoordinates(float3 worldPosition,
                                                  in EnemyNavigationGridState navigationGridState,
                                                  out int cellX,
                                                  out int cellY)
    {
        cellX = (int)math.floor((worldPosition.x - navigationGridState.Origin.x) * navigationGridState.InverseCellSize);
        cellY = (int)math.floor((worldPosition.z - navigationGridState.Origin.y) * navigationGridState.InverseCellSize);
        return IsInsideGrid(cellX, cellY, navigationGridState.Width, navigationGridState.Height);
    }
    #endregion

    #region Math Helpers
    private static int ResolveCellIndex(int cellX, int cellY, int width)
    {
        return cellY * width + cellX;
    }

    private static int2 ResolveCoordinatesFromIndex(int cellIndex, int width)
    {
        int cellY = cellIndex / width;
        int cellX = cellIndex - cellY * width;
        return new int2(cellX, cellY);
    }

    private static float2 ResolveCellCenter(float2 origin, float cellSize, int cellX, int cellY)
    {
        return origin + new float2((cellX + 0.5f) * cellSize, (cellY + 0.5f) * cellSize);
    }

    private static bool IsInsideGrid(int cellX, int cellY, int width, int height)
    {
        return cellX >= 0 && cellY >= 0 && cellX < width && cellY < height;
    }

    private static bool IsDiagonalOffset(int2 offset)
    {
        return math.abs(offset.x) == 1 && math.abs(offset.y) == 1;
    }

    private static int2 ResolveNeighborOffset(int directionIndex)
    {
        switch (directionIndex)
        {
            case 0:
                return new int2(0, 1);
            case 1:
                return new int2(1, 0);
            case 2:
                return new int2(0, -1);
            case 3:
                return new int2(-1, 0);
            case 4:
                return new int2(1, 1);
            case 5:
                return new int2(-1, 1);
            case 6:
                return new int2(1, -1);
            default:
                return new int2(-1, -1);
        }
    }
    #endregion

    #endregion
}
