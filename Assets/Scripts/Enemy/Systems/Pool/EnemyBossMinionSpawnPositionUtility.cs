using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

/// <summary>
/// Resolves boss-minion spawn positions against the shared enemy navigation grid and wall collision queries.
/// /params None.
/// /returns None.
/// </summary>
internal static class EnemyBossMinionSpawnPositionUtility
{
    #region Constants
    private const float TwoPi = 6.283185307179586f;
    private const float DirectionEpsilon = 1e-6f;
    private const float MinimumClearance = 0.05f;
    private const int CandidateProbeCount = 12;
    private const int MaximumCellSearchRadius = 16;
    private static readonly float3 MinionSpawnOffset = new float3(0f, 0.5f, 0f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves a deterministic spawn point near the boss and projects unsafe candidates back into reachable room space.
    /// /params bossPosition Current boss world position.
    /// /params spawnPlaneHeight World-space height used for minion placement and wall probes.
    /// /params radius Requested spawn radius around the boss.
    /// /params bossEntity Boss entity used for deterministic candidate rotation.
    /// /params ruleIndex Source minion rule index.
    /// /params spawnIndex Current minion spawn index within the trigger batch.
    /// /params minionData Minion body data used to preserve wall clearance.
    /// /params hasPhysicsWorld True when wall queries can be evaluated.
    /// /params physicsWorldSingleton Physics world used for wall clearance and direct path checks.
    /// /params wallsLayerMask Wall layer mask used by DOTS physics queries.
    /// /params navigationReady True when the shared navigation flow field is available.
    /// /params navigationGridState Shared navigation grid state.
    /// /params navigationCells Stable navigation cell snapshot safe across structural changes.
    /// /returns Resolved world spawn position.
    /// </summary>
    public static float3 ResolveSpawnPosition(float3 bossPosition,
                                              float spawnPlaneHeight,
                                              float radius,
                                              Entity bossEntity,
                                              int ruleIndex,
                                              int spawnIndex,
                                              in EnemyData minionData,
                                              bool hasPhysicsWorld,
                                              in PhysicsWorldSingleton physicsWorldSingleton,
                                              int wallsLayerMask,
                                              bool navigationReady,
                                              in EnemyNavigationGridState navigationGridState,
                                              NativeArray<EnemyNavigationCellElement> navigationCells)
    {
        float resolvedRadius = math.max(0f, radius);
        float clearance = math.max(MinimumClearance, minionData.BodyRadius + math.max(0f, minionData.MinimumWallDistance));
        float3 spawnCenterPosition = bossPosition;
        spawnCenterPosition.y = spawnPlaneHeight;
        uint baseSeed = math.hash(new int4(bossEntity.Index,
                                           bossEntity.Version,
                                           ruleIndex + 17,
                                           spawnIndex + 113));

        // Probe the authored ring first, then rotate through nearby deterministic alternatives.
        for (int candidateIndex = 0; candidateIndex < CandidateProbeCount; candidateIndex++)
        {
            float normalizedAngle = ResolveNormalizedCandidateAngle(baseSeed, candidateIndex);
            float radiusScale = ResolveCandidateRadiusScale(candidateIndex);
            float3 candidatePosition = ResolveRadialCandidate(spawnCenterPosition + MinionSpawnOffset, resolvedRadius * radiusScale, normalizedAngle);

            if (TryResolveSafeCandidate(candidatePosition,
                                        spawnCenterPosition,
                                        clearance,
                                        resolvedRadius,
                                        hasPhysicsWorld,
                                        in physicsWorldSingleton,
                                        wallsLayerMask,
                                        navigationReady,
                                        in navigationGridState,
                                        navigationCells,
                                        out float3 resolvedPosition))
            {
                return resolvedPosition;
            }
        }

        // Fallback keeps spawning deterministic even before physics/navigation have warmed up.
        return ResolveRadialCandidate(spawnCenterPosition + MinionSpawnOffset, resolvedRadius, ResolveNormalizedCandidateAngle(baseSeed, 0));
    }
    #endregion

    #region Candidate Resolution
    /// <summary>
    /// Resolves whether a candidate can be used directly or after projection to a reachable navigation cell.
    /// /params candidatePosition Candidate world position before safety correction.
    /// /params bossPosition Current boss world position used to reject wall-separated candidates.
    /// /params clearance Minimum minion wall clearance.
    /// /params searchDistance Distance budget used to find reachable fallback cells.
    /// /params hasPhysicsWorld True when wall queries can be evaluated.
    /// /params physicsWorldSingleton Physics world used for wall clearance and direct path checks.
    /// /params wallsLayerMask Wall layer mask used by DOTS physics queries.
    /// /params navigationReady True when the shared navigation flow field is available.
    /// /params navigationGridState Shared navigation grid state.
    /// /params navigationCells Stable navigation cell snapshot safe across structural changes.
    /// /params resolvedPosition Output safe candidate.
    /// /returns True when a safe candidate was resolved.
    /// </summary>
    private static bool TryResolveSafeCandidate(float3 candidatePosition,
                                                float3 bossPosition,
                                                float clearance,
                                                float searchDistance,
                                                bool hasPhysicsWorld,
                                                in PhysicsWorldSingleton physicsWorldSingleton,
                                                int wallsLayerMask,
                                                bool navigationReady,
                                                in EnemyNavigationGridState navigationGridState,
                                                NativeArray<EnemyNavigationCellElement> navigationCells,
                                                out float3 resolvedPosition)
    {
        resolvedPosition = candidatePosition;

        if (navigationReady &&
            !TryProjectToReachablePosition(candidatePosition,
                                           searchDistance,
                                           in navigationGridState,
                                           navigationCells,
                                           out resolvedPosition))
        {
            return false;
        }

        if (!hasPhysicsWorld || wallsLayerMask == 0)
            return true;

        if (WorldWallCollisionUtility.TryResolveMinimumClearance(physicsWorldSingleton,
                                                                 resolvedPosition,
                                                                 clearance,
                                                                 wallsLayerMask,
                                                                 out float3 correctionDisplacement,
                                                                 out float3 _))
        {
            resolvedPosition += correctionDisplacement;
        }

        if (navigationReady && !IsReachablePosition(resolvedPosition, in navigationGridState, navigationCells))
            return false;

        float3 bossToCandidate = resolvedPosition - bossPosition;
        bossToCandidate.y = 0f;

        if (math.lengthsq(bossToCandidate) <= DirectionEpsilon)
            return true;

        return !WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                        bossPosition,
                                                                        bossToCandidate,
                                                                        clearance,
                                                                        wallsLayerMask,
                                                                        out float3 _,
                                                                        out float3 _);
    }

    /// <summary>
    /// Projects a desired spawn point to the nearest reachable navigation cell when the desired cell is outside the room.
    /// /params desiredPosition Desired world position.
    /// /params searchDistance Distance budget used to scale local cell search.
    /// /params navigationGridState Shared navigation grid state.
    /// /params navigationCells Stable navigation cell snapshot safe across structural changes.
    /// /params projectedPosition Output desired position or nearest reachable cell center.
    /// /returns True when a reachable position was found.
    /// </summary>
    private static bool TryProjectToReachablePosition(float3 desiredPosition,
                                                      float searchDistance,
                                                      in EnemyNavigationGridState navigationGridState,
                                                      NativeArray<EnemyNavigationCellElement> navigationCells,
                                                      out float3 projectedPosition)
    {
        projectedPosition = desiredPosition;

        if (!TryResolveClampedCellCoordinates(desiredPosition, in navigationGridState, out int cellX, out int cellY))
            return false;

        int cellIndex = ResolveCellIndex(cellX, cellY, navigationGridState.Width);

        if (IsReachableCell(cellIndex, navigationCells))
            return true;

        int searchRadius = ResolveCellSearchRadius(searchDistance, navigationGridState.CellSize);
        float bestDistanceSquared = float.MaxValue;
        bool foundReachableCell = false;

        // Scan a bounded local square to recover candidates pushed just outside the enclosed room.
        for (int offsetY = -searchRadius; offsetY <= searchRadius; offsetY++)
        {
            for (int offsetX = -searchRadius; offsetX <= searchRadius; offsetX++)
            {
                int candidateX = cellX + offsetX;
                int candidateY = cellY + offsetY;

                if (!IsInsideGrid(candidateX, candidateY, navigationGridState.Width, navigationGridState.Height))
                    continue;

                int candidateIndex = ResolveCellIndex(candidateX, candidateY, navigationGridState.Width);

                if (!IsReachableCell(candidateIndex, navigationCells))
                    continue;

                float2 candidateCenter = ResolveCellCenter(navigationGridState.Origin,
                                                           navigationGridState.CellSize,
                                                           candidateX,
                                                           candidateY);
                float2 desiredPlanar = new float2(desiredPosition.x, desiredPosition.z);
                float distanceSquared = math.lengthsq(candidateCenter - desiredPlanar);

                if (distanceSquared >= bestDistanceSquared)
                    continue;

                bestDistanceSquared = distanceSquared;
                projectedPosition = new float3(candidateCenter.x, desiredPosition.y, candidateCenter.y);
                foundReachableCell = true;
            }
        }

        return foundReachableCell;
    }
    #endregion

    #region Navigation Helpers
    /// <summary>
    /// Resolves whether one world position belongs to a reachable navigation cell.
    /// /params worldPosition World position to inspect.
    /// /params navigationGridState Shared navigation grid state.
    /// /params navigationCells Stable navigation cell snapshot safe across structural changes.
    /// /returns True when the position maps to a walkable and reachable cell.
    /// </summary>
    private static bool IsReachablePosition(float3 worldPosition,
                                            in EnemyNavigationGridState navigationGridState,
                                            NativeArray<EnemyNavigationCellElement> navigationCells)
    {
        if (!TryResolveCellCoordinates(worldPosition, in navigationGridState, out int cellX, out int cellY))
            return false;

        int cellIndex = ResolveCellIndex(cellX, cellY, navigationGridState.Width);
        return IsReachableCell(cellIndex, navigationCells);
    }

    /// <summary>
    /// Resolves whether one navigation cell is walkable and connected to the player-side flow field.
    /// /params cellIndex Navigation cell index.
    /// /params navigationCells Stable navigation cell snapshot safe across structural changes.
    /// /returns True when the cell is currently reachable.
    /// </summary>
    private static bool IsReachableCell(int cellIndex, NativeArray<EnemyNavigationCellElement> navigationCells)
    {
        if (cellIndex < 0 || cellIndex >= navigationCells.Length)
            return false;

        EnemyNavigationCellElement cell = navigationCells[cellIndex];
        return cell.Walkable != 0 && cell.Cost != EnemyNavigationFlowFieldUtility.UnreachableCellCost;
    }

    /// <summary>
    /// Resolves grid coordinates and clamps out-of-grid positions to the nearest border cell for fallback searches.
    /// /params worldPosition World position to map.
    /// /params navigationGridState Shared navigation grid state.
    /// /params cellX Output cell X coordinate.
    /// /params cellY Output cell Y coordinate.
    /// /returns True when the grid has valid dimensions.
    /// </summary>
    private static bool TryResolveClampedCellCoordinates(float3 worldPosition,
                                                         in EnemyNavigationGridState navigationGridState,
                                                         out int cellX,
                                                         out int cellY)
    {
        cellX = 0;
        cellY = 0;

        if (navigationGridState.Width <= 0 || navigationGridState.Height <= 0)
            return false;

        float rawCellX = math.floor((worldPosition.x - navigationGridState.Origin.x) * navigationGridState.InverseCellSize);
        float rawCellY = math.floor((worldPosition.z - navigationGridState.Origin.y) * navigationGridState.InverseCellSize);
        cellX = math.clamp((int)rawCellX, 0, navigationGridState.Width - 1);
        cellY = math.clamp((int)rawCellY, 0, navigationGridState.Height - 1);
        return true;
    }

    /// <summary>
    /// Resolves grid coordinates only when the world position is inside the navigation grid.
    /// /params worldPosition World position to map.
    /// /params navigationGridState Shared navigation grid state.
    /// /params cellX Output cell X coordinate.
    /// /params cellY Output cell Y coordinate.
    /// /returns True when the position maps inside the grid.
    /// </summary>
    private static bool TryResolveCellCoordinates(float3 worldPosition,
                                                  in EnemyNavigationGridState navigationGridState,
                                                  out int cellX,
                                                  out int cellY)
    {
        cellX = (int)math.floor((worldPosition.x - navigationGridState.Origin.x) * navigationGridState.InverseCellSize);
        cellY = (int)math.floor((worldPosition.z - navigationGridState.Origin.y) * navigationGridState.InverseCellSize);
        return IsInsideGrid(cellX, cellY, navigationGridState.Width, navigationGridState.Height);
    }

    /// <summary>
    /// Resolves a bounded search radius in navigation cells from a world-space spawn distance.
    /// /params searchDistance World-space distance budget.
    /// /params cellSize Navigation cell size.
    /// /returns Search radius in cells.
    /// </summary>
    private static int ResolveCellSearchRadius(float searchDistance, float cellSize)
    {
        float safeCellSize = math.max(0.01f, cellSize);
        int radius = (int)math.ceil(math.max(0f, searchDistance) / safeCellSize) + 2;
        return math.clamp(radius, 1, MaximumCellSearchRadius);
    }

    /// <summary>
    /// Converts grid coordinates to a linear cell index.
    /// /params cellX Cell X coordinate.
    /// /params cellY Cell Y coordinate.
    /// /params width Grid width.
    /// /returns Linear cell index.
    /// </summary>
    private static int ResolveCellIndex(int cellX, int cellY, int width)
    {
        return cellY * width + cellX;
    }

    /// <summary>
    /// Resolves the world-space center of one navigation cell.
    /// /params origin Grid origin.
    /// /params cellSize Grid cell size.
    /// /params cellX Cell X coordinate.
    /// /params cellY Cell Y coordinate.
    /// /returns Planar cell center.
    /// </summary>
    private static float2 ResolveCellCenter(float2 origin, float cellSize, int cellX, int cellY)
    {
        return origin + new float2((cellX + 0.5f) * cellSize, (cellY + 0.5f) * cellSize);
    }

    /// <summary>
    /// Resolves whether grid coordinates are inside the grid dimensions.
    /// /params cellX Cell X coordinate.
    /// /params cellY Cell Y coordinate.
    /// /params width Grid width.
    /// /params height Grid height.
    /// /returns True when the coordinates are inside the grid.
    /// </summary>
    private static bool IsInsideGrid(int cellX, int cellY, int width, int height)
    {
        return cellX >= 0 && cellY >= 0 && cellX < width && cellY < height;
    }
    #endregion

    #region Candidate Math
    /// <summary>
    /// Resolves one deterministic normalized angle for the requested candidate probe.
    /// /params seed Deterministic base seed.
    /// /params candidateIndex Candidate probe index.
    /// /returns Normalized angle in the [0..1] range.
    /// </summary>
    private static float ResolveNormalizedCandidateAngle(uint seed, int candidateIndex)
    {
        uint candidateSeed = math.hash(new uint2(seed, (uint)math.max(0, candidateIndex + 1)));
        return (candidateSeed & 0x00FFFFFFu) / 16777215f;
    }

    /// <summary>
    /// Resolves a radius scale for deterministic fallback probes around the boss.
    /// /params candidateIndex Candidate probe index.
    /// /returns Radius scale applied to the authored spawn radius.
    /// </summary>
    private static float ResolveCandidateRadiusScale(int candidateIndex)
    {
        switch (candidateIndex % 4)
        {
            case 1:
                return 0.75f;
            case 2:
                return 0.5f;
            case 3:
                return 1.25f;
            default:
                return 1f;
        }
    }

    /// <summary>
    /// Resolves one radial world position around the boss from a normalized angle.
    /// /params bossPosition Current boss world position.
    /// /params radius Candidate radius.
    /// /params normalizedAngle Normalized angle in the [0..1] range.
    /// /returns Candidate world position.
    /// </summary>
    private static float3 ResolveRadialCandidate(float3 bossPosition, float radius, float normalizedAngle)
    {
        float angle = normalizedAngle * TwoPi;
        float3 position = bossPosition;
        position.x += math.cos(angle) * math.max(0f, radius);
        position.z += math.sin(angle) * math.max(0f, radius);
        return position;
    }
    #endregion

    #endregion
}
