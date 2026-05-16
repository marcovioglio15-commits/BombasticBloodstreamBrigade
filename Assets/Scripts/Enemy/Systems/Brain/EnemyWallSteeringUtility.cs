using Unity.Mathematics;
using Unity.Physics;

/// <summary>
/// Provides tangent-based wall circumnavigation helpers for blocked enemy steering movement.
/// </summary>
public static class EnemyWallSteeringUtility
{
    #region Constants
    private static readonly float3 UpAxis = new float3(0f, 1f, 0f);
    private const float DirectionEpsilon = 1e-6f;
    private const float MinimumDeltaTime = 1e-4f;
    private const float MinimumBlockedRatioForCircumnavigation = 0.2f;
    private const float MinimumCandidateTravelDistance = 0.01f;
    private const float MinimumCircumnavigationDistanceGain = 0.005f;
    private const float TravelDistanceScoreWeight = 1f;
    private const float PlayerProgressScoreWeight = 0.38f;
    #endregion

    #region Methods
    /// <summary>
    /// Attempts to convert a blocked pursuit displacement into a valid tangent bypass displacement around wall geometry.
    /// Use this after a blocking sweep so enemies side-step circumnavigable obstacles instead of repeatedly pushing into them.
    /// </summary>
    /// <param name="physicsWorldSingleton">Physics world used for wall sweep checks.</param>
    /// <param name="enemyStableIndex">Stable enemy index used for deterministic tie-break decisions.</param>
    /// <param name="startPosition">Current enemy world position.</param>
    /// <param name="playerPosition">Current player world position.</param>
    /// <param name="currentVelocity">Current enemy velocity before wall response.</param>
    /// <param name="desiredDisplacement">Requested displacement for this frame before collision resolution.</param>
    /// <param name="blockedDisplacement">Displacement returned by the initial blocking sweep.</param>
    /// <param name="blockingNormal">Wall normal returned by the initial blocking sweep.</param>
    /// <param name="collisionRadius">Enemy body radius used for sweep checks.</param>
    /// <param name="wallsLayerMask">Layer mask used for wall collision queries.</param>
    /// <param name="deltaTime">Simulation delta time for the frame.</param>
    /// <param name="resolvedDisplacement">Best resolved displacement produced by the bypass evaluation.</param>
    /// <param name="resolvedVelocity">Velocity corresponding to the resolved displacement.</param>
    /// <param name="resolvedHitNormal">Wall normal of the resolved bypass candidate when still partially blocked.</param>
    /// <returns>True when a better bypass candidate is found and should replace the default blocked response.</returns>
    public static bool TryResolveCircumnavigationDisplacement(in PhysicsWorldSingleton physicsWorldSingleton,
                                                              int enemyStableIndex,
                                                              float3 startPosition,
                                                              float3 playerPosition,
                                                              float3 currentVelocity,
                                                              float3 desiredDisplacement,
                                                              float3 blockedDisplacement,
                                                              float3 blockingNormal,
                                                              float collisionRadius,
                                                              int wallsLayerMask,
                                                              float deltaTime,
                                                              out float3 resolvedDisplacement,
                                                              out float3 resolvedVelocity,
                                                              out float3 resolvedHitNormal)
    {
        resolvedDisplacement = blockedDisplacement;
        resolvedVelocity = currentVelocity;
        resolvedHitNormal = blockingNormal;

        if (wallsLayerMask == 0)
            return false;

        CollisionFilter wallsCollisionFilter = WorldWallCollisionUtility.BuildWallsCollisionFilter(wallsLayerMask);

        if (wallsCollisionFilter.CollidesWith == 0u)
            return false;

        if (deltaTime <= MinimumDeltaTime)
            return false;

        float requestedDistance = math.length(desiredDisplacement);

        if (requestedDistance <= DirectionEpsilon)
            return false;

        float blockedDistance = math.length(blockedDisplacement);
        float blockedRatio = 1f - math.saturate(blockedDistance / math.max(0.0001f, requestedDistance));

        if (blockedRatio < MinimumBlockedRatioForCircumnavigation)
            return false;

        float3 planarNormal = math.normalizesafe(new float3(blockingNormal.x, 0f, blockingNormal.z), float3.zero);

        if (math.lengthsq(planarNormal) <= DirectionEpsilon)
            return false;

        float3 tangent = math.normalizesafe(math.cross(UpAxis, planarNormal), float3.zero);

        if (math.lengthsq(tangent) <= DirectionEpsilon)
            return false;

        float3 planarCurrentVelocity = new float3(currentVelocity.x, 0f, currentVelocity.z);
        float speed = math.length(planarCurrentVelocity);

        if (speed <= DirectionEpsilon)
            speed = requestedDistance / math.max(deltaTime, MinimumDeltaTime);

        if (speed <= DirectionEpsilon)
            return false;

        float3 toPlayer = playerPosition - startPosition;
        toPlayer.y = 0f;
        float3 toPlayerDirection = math.normalizesafe(toPlayer, tangent);
        float preferredSign = ResolvePreferredSign(tangent, toPlayerDirection, planarCurrentVelocity, enemyStableIndex);
        float3 firstCandidateDirection = tangent * preferredSign;
        float3 secondCandidateDirection = -firstCandidateDirection;
        bool hasFirstCandidate = EvaluateCircumnavigationCandidate(physicsWorldSingleton,
                                                                   startPosition,
                                                                   firstCandidateDirection,
                                                                   speed,
                                                                   deltaTime,
                                                                   toPlayerDirection,
                                                                   collisionRadius,
                                                                   wallsCollisionFilter,
                                                                   out float firstCandidateScore,
                                                                   out float firstCandidateDistance,
                                                                   out float3 firstCandidateDisplacement,
                                                                   out float3 firstCandidateVelocity,
                                                                   out float3 firstCandidateHitNormal);
        bool hasSecondCandidate = EvaluateCircumnavigationCandidate(physicsWorldSingleton,
                                                                    startPosition,
                                                                    secondCandidateDirection,
                                                                    speed,
                                                                    deltaTime,
                                                                    toPlayerDirection,
                                                                    collisionRadius,
                                                                    wallsCollisionFilter,
                                                                    out float secondCandidateScore,
                                                                    out float secondCandidateDistance,
                                                                    out float3 secondCandidateDisplacement,
                                                                    out float3 secondCandidateVelocity,
                                                                    out float3 secondCandidateHitNormal);

        if (hasFirstCandidate == false && hasSecondCandidate == false)
            return false;

        bool useSecondCandidate = false;

        if (hasSecondCandidate)
        {
            if (hasFirstCandidate == false)
                useSecondCandidate = true;
            else if (secondCandidateScore > firstCandidateScore)
                useSecondCandidate = true;
        }

        float bestDistance = useSecondCandidate ? secondCandidateDistance : firstCandidateDistance;

        if (bestDistance <= blockedDistance + MinimumCircumnavigationDistanceGain)
            return false;

        if (useSecondCandidate)
        {
            resolvedDisplacement = secondCandidateDisplacement;
            resolvedVelocity = secondCandidateVelocity;
            resolvedHitNormal = secondCandidateHitNormal;
            return true;
        }

        resolvedDisplacement = firstCandidateDisplacement;
        resolvedVelocity = firstCandidateVelocity;
        resolvedHitNormal = firstCandidateHitNormal;
        return true;
    }

    /// <summary>
    /// Evaluates one tangent movement candidate and scores it by free travel distance plus progress toward player.
    /// </summary>
    /// <param name="physicsWorldSingleton">Physics world used for wall sweep checks.</param>
    /// <param name="startPosition">Current enemy world position.</param>
    /// <param name="candidateDirection">Planar tangent direction to evaluate.</param>
    /// <param name="speed">Planar speed used to build candidate displacement.</param>
    /// <param name="deltaTime">Simulation delta time for the frame.</param>
    /// <param name="toPlayerDirection">Normalized planar direction from enemy to player.</param>
    /// <param name="collisionRadius">Enemy body radius used for sweep checks.</param>
    /// <param name="wallsLayerMask">Layer mask used for wall collision queries.</param>
    /// <param name="candidateScore">Score used to compare candidate quality.</param>
    /// <param name="candidateDistance">Travel distance produced by this candidate.</param>
    /// <param name="candidateDisplacement">Resolved displacement of this candidate.</param>
    /// <param name="candidateVelocity">Velocity corresponding to the candidate displacement.</param>
    /// <param name="candidateHitNormal">Wall normal for this candidate when still partially blocked.</param>
    /// <returns>True when candidate is valid and produces usable movement.</returns>
    private static bool EvaluateCircumnavigationCandidate(in PhysicsWorldSingleton physicsWorldSingleton,
                                                          float3 startPosition,
                                                          float3 candidateDirection,
                                                          float speed,
                                                          float deltaTime,
                                                          float3 toPlayerDirection,
                                                          float collisionRadius,
                                                          in CollisionFilter wallsCollisionFilter,
                                                          out float candidateScore,
                                                          out float candidateDistance,
                                                          out float3 candidateDisplacement,
                                                          out float3 candidateVelocity,
                                                          out float3 candidateHitNormal)
    {
        candidateScore = 0f;
        candidateDistance = 0f;
        candidateDisplacement = float3.zero;
        candidateVelocity = float3.zero;
        candidateHitNormal = float3.zero;

        float3 planarCandidateDirection = math.normalizesafe(new float3(candidateDirection.x, 0f, candidateDirection.z), float3.zero);

        if (math.lengthsq(planarCandidateDirection) <= DirectionEpsilon)
            return false;

        float candidateRequestedDistance = speed * deltaTime;

        if (candidateRequestedDistance <= DirectionEpsilon)
            return false;

        float3 candidateRequestedDisplacement = planarCandidateDirection * candidateRequestedDistance;
        bool hitWall = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                               startPosition,
                                                                               candidateRequestedDisplacement,
                                                                               collisionRadius,
                                                                               wallsCollisionFilter,
                                                                               out float3 allowedDisplacement,
                                                                               out float3 hitNormal);
        float distance = math.length(allowedDisplacement);

        if (distance <= MinimumCandidateTravelDistance)
            return false;

        float3 travelDirection = math.normalizesafe(allowedDisplacement, planarCandidateDirection);
        float progressTowardPlayer = math.max(0f, math.dot(travelDirection, toPlayerDirection));
        float score = distance * TravelDistanceScoreWeight + progressTowardPlayer * candidateRequestedDistance * PlayerProgressScoreWeight;

        if (hitWall)
            score *= 0.92f;

        candidateScore = score;
        candidateDistance = distance;
        candidateDisplacement = allowedDisplacement;
        candidateVelocity = allowedDisplacement / math.max(deltaTime, MinimumDeltaTime);
        candidateHitNormal = hitWall ? hitNormal : float3.zero;
        return true;
    }

    /// <summary>
    /// Resolves the preferred tangent sign by combining player-side bias, current velocity bias, and deterministic fallback.
    /// </summary>
    /// <param name="tangent">Planar tangent direction derived from wall normal.</param>
    /// <param name="toPlayerDirection">Normalized planar direction from enemy to player.</param>
    /// <param name="planarCurrentVelocity">Current planar velocity.</param>
    /// <param name="enemyStableIndex">Stable enemy index used for deterministic fallback.</param>
    /// <returns>+1 or -1 sign used to orient the first tangent candidate.</returns>
    private static float ResolvePreferredSign(float3 tangent,
                                              float3 toPlayerDirection,
                                              float3 planarCurrentVelocity,
                                              int enemyStableIndex)
    {
        float playerSide = math.dot(tangent, toPlayerDirection);

        if (math.abs(playerSide) > 0.05f)
            return playerSide >= 0f ? 1f : -1f;

        float3 velocityDirection = math.normalizesafe(planarCurrentVelocity, float3.zero);
        float velocitySide = math.dot(tangent, velocityDirection);

        if (math.abs(velocitySide) > 0.05f)
            return velocitySide >= 0f ? 1f : -1f;

        return ResolveDeterministicSign(enemyStableIndex);
    }

    /// <summary>
    /// Returns a deterministic sign based on enemy index to avoid random oscillation between tangent sides.
    /// </summary>
    /// <param name="enemyStableIndex">Stable enemy index.</param>
    /// <returns>+1 when hash bit is even, otherwise -1.</returns>
    private static float ResolveDeterministicSign(int enemyStableIndex)
    {
        uint hash = math.hash(new int2(enemyStableIndex * 17 + 3, enemyStableIndex * 31 + 7));

        if ((hash & 1u) == 0u)
            return 1f;

        return -1f;
    }
    #endregion
}
