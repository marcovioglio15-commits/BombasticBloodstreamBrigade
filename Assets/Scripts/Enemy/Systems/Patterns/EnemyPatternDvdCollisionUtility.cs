using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Resolves dynamic DVD-to-DVD collisions so intersecting trajectories bounce like equal-mass moving bodies.
/// </summary>
public static class EnemyPatternDvdCollisionUtility
{
    #region Constants
    private const float DirectionEpsilon = 1e-6f;
    private const float MinimumRadius = 0.05f;
    private const float SearchPadding = 0.05f;
    #endregion

    #region Nested Types
    /// <summary>
    /// Immutable occupancy snapshot used to evaluate nearby DVD collisions.
    /// </summary>
    public readonly struct OccupancyContext
    {
        public readonly NativeArray<Entity> Entities;
        public readonly NativeArray<float3> Positions;
        public readonly NativeArray<float3> Velocities;
        public readonly NativeArray<float> Radii;
        public readonly NativeArray<byte> DvdFlags;
        public readonly NativeArray<float> DvdBounceDamping;
        public readonly NativeParallelMultiHashMap<int, int> CellMap;
        public readonly float InverseCellSize;
        public readonly float MaxRadius;
        public readonly float MaxSpeed;

        public OccupancyContext(NativeArray<Entity> entities,
                                NativeArray<float3> positions,
                                NativeArray<float3> velocities,
                                NativeArray<float> radii,
                                NativeArray<byte> dvdFlags,
                                NativeArray<float> dvdBounceDamping,
                                NativeParallelMultiHashMap<int, int> cellMap,
                                float inverseCellSize,
                                float maxRadius,
                                float maxSpeed)
        {
            Entities = entities;
            Positions = positions;
            Velocities = velocities;
            Radii = radii;
            DvdFlags = dvdFlags;
            DvdBounceDamping = dvdBounceDamping;
            CellMap = cellMap;
            InverseCellSize = inverseCellSize;
            MaxRadius = maxRadius;
            MaxSpeed = maxSpeed;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Predicts the earliest DVD-to-DVD collision over the current frame and resolves the bounced velocity for the current enemy only.
    /// </summary>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="currentPosition">Current enemy world position.</param>
    /// <param name="currentVelocity">Current enemy planar velocity before pair collision response.</param>
    /// <param name="collisionRadius">Current enemy collision radius.</param>
    /// <param name="bounceDamping">Current enemy configured bounce damping.</param>
    /// <param name="deltaTime">Current frame simulation delta time.</param>
    /// <param name="occupancyContext">Immutable occupancy snapshot used for neighborhood lookup.</param>
    /// <param name="bouncedVelocity">Output bounced planar velocity.</param>
    /// <param name="collisionTimeSeconds">Output time of impact in seconds from the current frame start.</param>
    /// <returns>True when a valid DVD pair collision is found; otherwise false.</returns>
    public static bool TryResolveBounceVelocity(Entity enemyEntity,
                                                float3 currentPosition,
                                                float3 currentVelocity,
                                                float collisionRadius,
                                                float bounceDamping,
                                                float deltaTime,
                                                in OccupancyContext occupancyContext,
                                                out float3 bouncedVelocity,
                                                out float collisionTimeSeconds)
    {
        bouncedVelocity = currentVelocity;
        collisionTimeSeconds = 0f;

        if (deltaTime <= DirectionEpsilon)
            return false;

        float3 planarVelocity = new float3(currentVelocity.x, 0f, currentVelocity.z);

        if (math.lengthsq(planarVelocity) <= DirectionEpsilon)
            return false;

        if (occupancyContext.Entities.Length <= 1)
            return false;

        float maximumNeighborTravelDistance = math.max(0f, occupancyContext.MaxSpeed) * deltaTime;
        float3 projectedPosition = currentPosition + planarVelocity * deltaTime;
        float searchRadius = math.max(MinimumRadius, collisionRadius) +
                             math.max(MinimumRadius, occupancyContext.MaxRadius) +
                             math.length(planarVelocity) * deltaTime +
                             maximumNeighborTravelDistance +
                             SearchPadding;
        float minimumX = math.min(currentPosition.x, projectedPosition.x) - searchRadius;
        float maximumX = math.max(currentPosition.x, projectedPosition.x) + searchRadius;
        float minimumY = math.min(currentPosition.z, projectedPosition.z) - searchRadius;
        float maximumY = math.max(currentPosition.z, projectedPosition.z) + searchRadius;
        int minimumCellX = (int)math.floor(minimumX * occupancyContext.InverseCellSize);
        int maximumCellX = (int)math.floor(maximumX * occupancyContext.InverseCellSize);
        int minimumCellY = (int)math.floor(minimumY * occupancyContext.InverseCellSize);
        int maximumCellY = (int)math.floor(maximumY * occupancyContext.InverseCellSize);
        bool foundCollision = false;
        float bestCollisionTime = deltaTime;
        float bestApproachSpeed = 0f;
        float3 bestBouncedVelocity = planarVelocity;

        // Search nearby occupancy cells only so DVD collisions remain cheap even with many active enemies.
        for (int cellX = minimumCellX; cellX <= maximumCellX; cellX++)
        {
            for (int cellY = minimumCellY; cellY <= maximumCellY; cellY++)
            {
                int cellKey = EnemyPatternWandererUtility.EncodeCell(cellX, cellY);
                NativeParallelMultiHashMapIterator<int> iterator;
                int occupancyIndex;

                if (!occupancyContext.CellMap.TryGetFirstValue(cellKey, out occupancyIndex, out iterator))
                    continue;

                do
                {
                    Entity otherEntity = occupancyContext.Entities[occupancyIndex];

                    if (otherEntity == enemyEntity)
                        continue;

                    if (occupancyContext.DvdFlags[occupancyIndex] == 0)
                        continue;

                    float3 otherVelocity = occupancyContext.Velocities[occupancyIndex];
                    float otherRadius = math.max(MinimumRadius, occupancyContext.Radii[occupancyIndex]);

                    if (!TryResolveCollisionCandidate(currentPosition,
                                                      planarVelocity,
                                                      math.max(MinimumRadius, collisionRadius),
                                                      occupancyContext.Positions[occupancyIndex],
                                                      otherVelocity,
                                                      otherRadius,
                                                      deltaTime,
                                                      enemyEntity,
                                                      otherEntity,
                                                      out float candidateCollisionTime,
                                                      out float candidateApproachSpeed,
                                                      out float3 candidateCollisionNormal))
                    {
                        continue;
                    }

                    bool improvesCollision = !foundCollision ||
                                             candidateCollisionTime < bestCollisionTime - 0.0001f ||
                                             (math.abs(candidateCollisionTime - bestCollisionTime) <= 0.0001f &&
                                              candidateApproachSpeed > bestApproachSpeed);

                    if (!improvesCollision)
                        continue;

                    float pairBounceDamping = math.min(math.clamp(bounceDamping, 0f, 1f),
                                                       math.clamp(occupancyContext.DvdBounceDamping[occupancyIndex], 0f, 1f));
                    float3 candidateBouncedVelocity = ResolveEqualMassBounceVelocity(planarVelocity,
                                                                                     otherVelocity,
                                                                                     candidateCollisionNormal,
                                                                                     pairBounceDamping);

                    if (math.lengthsq(candidateBouncedVelocity) <= DirectionEpsilon)
                        continue;

                    foundCollision = true;
                    bestCollisionTime = candidateCollisionTime;
                    bestApproachSpeed = candidateApproachSpeed;
                    bestBouncedVelocity = candidateBouncedVelocity;
                }
                while (occupancyContext.CellMap.TryGetNextValue(out occupancyIndex, ref iterator));
            }
        }

        if (!foundCollision)
            return false;

        bouncedVelocity = bestBouncedVelocity;
        collisionTimeSeconds = math.clamp(bestCollisionTime, 0f, deltaTime);
        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Evaluates whether two moving discs collide during the current frame.
    /// </summary>
    /// <param name="selfPosition">Current self position.</param>
    /// <param name="selfVelocity">Current self planar velocity.</param>
    /// <param name="selfRadius">Current self collision radius.</param>
    /// <param name="otherPosition">Current neighbor position.</param>
    /// <param name="otherVelocity">Current neighbor planar velocity.</param>
    /// <param name="otherRadius">Current neighbor collision radius.</param>
    /// <param name="deltaTime">Current frame simulation delta time.</param>
    /// <param name="selfEntity">Current self entity used for deterministic fallbacks.</param>
    /// <param name="otherEntity">Current neighbor entity used for deterministic fallbacks.</param>
    /// <param name="collisionTimeSeconds">Output time of impact in seconds from frame start.</param>
    /// <param name="approachSpeed">Output closing speed along the collision normal.</param>
    /// <param name="collisionNormal">Output collision normal from neighbor to self.</param>
    /// <returns>True when the two discs collide while approaching one another; otherwise false.</returns>
    private static bool TryResolveCollisionCandidate(float3 selfPosition,
                                                     float3 selfVelocity,
                                                     float selfRadius,
                                                     float3 otherPosition,
                                                     float3 otherVelocity,
                                                     float otherRadius,
                                                     float deltaTime,
                                                     Entity selfEntity,
                                                     Entity otherEntity,
                                                     out float collisionTimeSeconds,
                                                     out float approachSpeed,
                                                     out float3 collisionNormal)
    {
        collisionTimeSeconds = 0f;
        approachSpeed = 0f;
        collisionNormal = float3.zero;

        float2 relativePosition = new float2(selfPosition.x - otherPosition.x, selfPosition.z - otherPosition.z);
        float2 relativeVelocity = new float2(selfVelocity.x - otherVelocity.x, selfVelocity.z - otherVelocity.z);
        float combinedRadius = math.max(MinimumRadius, selfRadius + otherRadius);
        float combinedRadiusSquared = combinedRadius * combinedRadius;
        float relativePositionLengthSquared = math.lengthsq(relativePosition);

        if (relativePositionLengthSquared <= combinedRadiusSquared)
        {
            float3 overlapNormal = ResolveCollisionNormal(relativePosition, selfEntity, otherEntity);
            float relativeNormalVelocity = math.dot(new float3(relativeVelocity.x, 0f, relativeVelocity.y), overlapNormal);

            if (relativeNormalVelocity >= -DirectionEpsilon)
                return false;

            collisionTimeSeconds = 0f;
            approachSpeed = -relativeNormalVelocity;
            collisionNormal = overlapNormal;
            return true;
        }

        float a = math.lengthsq(relativeVelocity);

        if (a <= DirectionEpsilon)
            return false;

        float b = 2f * math.dot(relativePosition, relativeVelocity);
        float c = relativePositionLengthSquared - combinedRadiusSquared;
        float discriminant = b * b - 4f * a * c;

        if (discriminant < 0f)
            return false;

        float squareRootDiscriminant = math.sqrt(discriminant);
        float candidateTime = (-b - squareRootDiscriminant) / (2f * a);

        if (candidateTime < 0f || candidateTime > deltaTime)
            return false;

        float2 impactRelativePosition = relativePosition + relativeVelocity * candidateTime;
        float3 candidateNormal = ResolveCollisionNormal(impactRelativePosition, selfEntity, otherEntity);
        float relativeNormalSpeed = math.dot(new float3(relativeVelocity.x, 0f, relativeVelocity.y), candidateNormal);

        if (relativeNormalSpeed >= -DirectionEpsilon)
            return false;

        collisionTimeSeconds = candidateTime;
        approachSpeed = -relativeNormalSpeed;
        collisionNormal = candidateNormal;
        return true;
    }

    /// <summary>
    /// Resolves the bounced self velocity for an equal-mass pair using the provided collision normal.
    /// </summary>
    /// <param name="selfVelocity">Current self velocity.</param>
    /// <param name="otherVelocity">Current other velocity.</param>
    /// <param name="collisionNormal">Collision normal from other to self.</param>
    /// <param name="bounceDamping">Shared restitution coefficient in the [0..1] range.</param>
    /// <returns>Bounced self velocity.</returns>
    private static float3 ResolveEqualMassBounceVelocity(float3 selfVelocity,
                                                         float3 otherVelocity,
                                                         float3 collisionNormal,
                                                         float bounceDamping)
    {
        float3 normalizedCollisionNormal = math.normalizesafe(new float3(collisionNormal.x, 0f, collisionNormal.z), float3.zero);

        if (math.lengthsq(normalizedCollisionNormal) <= DirectionEpsilon)
            return selfVelocity;

        float3 planarSelfVelocity = new float3(selfVelocity.x, 0f, selfVelocity.z);
        float3 planarOtherVelocity = new float3(otherVelocity.x, 0f, otherVelocity.z);
        float relativeNormalVelocity = math.dot(planarSelfVelocity - planarOtherVelocity, normalizedCollisionNormal);

        if (relativeNormalVelocity >= 0f)
            return planarSelfVelocity;

        float clampedBounceDamping = math.clamp(bounceDamping, 0f, 1f);
        float impulseScale = 0.5f * (1f + clampedBounceDamping) * relativeNormalVelocity;
        return planarSelfVelocity - normalizedCollisionNormal * impulseScale;
    }

    /// <summary>
    /// Resolves one stable collision normal from a 2D relative position, falling back to a deterministic direction when necessary.
    /// </summary>
    /// <param name="relativePosition">Relative position from other to self on the XZ plane.</param>
    /// <param name="selfEntity">Current self entity.</param>
    /// <param name="otherEntity">Current other entity.</param>
    /// <returns>Normalized collision normal from other to self.</returns>
    private static float3 ResolveCollisionNormal(float2 relativePosition, Entity selfEntity, Entity otherEntity)
    {
        float2 planarNormal = math.normalizesafe(relativePosition, float2.zero);

        if (math.lengthsq(planarNormal) > DirectionEpsilon)
            return new float3(planarNormal.x, 0f, planarNormal.y);

        uint hash = math.hash(new uint2((uint)selfEntity.Index, (uint)otherEntity.Index));
        int quadrant = (int)(hash % 4u);

        switch (quadrant)
        {
            case 0:
                return new float3(0.70710677f, 0f, 0.70710677f);

            case 1:
                return new float3(-0.70710677f, 0f, 0.70710677f);

            case 2:
                return new float3(0.70710677f, 0f, -0.70710677f);

            default:
                return new float3(-0.70710677f, 0f, -0.70710677f);
        }
    }
    #endregion

    #endregion
}
