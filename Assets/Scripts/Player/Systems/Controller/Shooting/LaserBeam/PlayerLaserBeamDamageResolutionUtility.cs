using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Provides shared hit collection and low-level damage helpers used by the Laser Beam damage system.
/// </summary>
internal static class PlayerLaserBeamDamageResolutionUtility
{
    #region Nested Types
    internal struct LaserBeamHitCandidate
    {
        public int EnemyIndex;
        public float DistanceAlongLane;
        public float3 HitPoint;
        public float3 HitDirection;
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Collects all enemies intersecting the requested lane and sorts them by travel distance from the lane origin.
    /// </summary>
    /// <param name="laserBeamLanes">Resolved lane-segment buffer for the current player.</param>
    /// <param name="segmentStartIndex">First segment index belonging to the lane.</param>
    /// <param name="segmentEndIndex">Exclusive end index of the lane.</param>
    /// <param name="enemyCount">Number of projected enemies.</param>
    /// <param name="enemyPositions">Cached world positions of projected enemies.</param>
    /// <param name="enemyBodyRadii">Cached body radii of projected enemies.</param>
    /// <param name="enemyCellMap">Spatial hash map used to cull enemy lookups.</param>
    /// <param name="inverseCellSize">Reciprocal cell size used by the spatial hash.</param>
    /// <param name="maximumEnemyRadius">Largest enemy radius stored in the current enemy cache.</param>
    /// <param name="hitCandidates">Output list reused across lane evaluations.</param>
    public static void CollectHitCandidates(DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                                            int segmentStartIndex,
                                            int segmentEndIndex,
                                            int enemyCount,
                                            in NativeArray<float3> enemyPositions,
                                            in NativeArray<float> enemyBodyRadii,
                                            in NativeParallelMultiHashMap<int, int> enemyCellMap,
                                            float inverseCellSize,
                                            float maximumEnemyRadius,
                                            ref NativeList<LaserBeamHitCandidate> hitCandidates)
    {
        hitCandidates.Clear();
        float accumulatedDistanceBeforeSegment = 0f;

        for (int segmentIndex = segmentStartIndex; segmentIndex < segmentEndIndex; segmentIndex++)
        {
            PlayerLaserBeamLaneElement segment = laserBeamLanes[segmentIndex];
            float3 midpoint = (segment.StartPoint + segment.EndPoint) * 0.5f;
            float queryRadius = segment.Length * 0.5f + math.max(0.01f, segment.CollisionRadius + maximumEnemyRadius);
            EnemySpatialHashUtility.ResolveCellBounds(midpoint,
                                                      queryRadius,
                                                      inverseCellSize,
                                                      out int minCellX,
                                                      out int maxCellX,
                                                      out int minCellY,
                                                      out int maxCellY);

            for (int cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                for (int cellY = minCellY; cellY <= maxCellY; cellY++)
                {
                    int cellKey = EnemySpatialHashUtility.EncodeCell(cellX, cellY);
                    NativeParallelMultiHashMapIterator<int> iterator;
                    int enemyIndex;

                    if (!enemyCellMap.TryGetFirstValue(cellKey, out enemyIndex, out iterator))
                        continue;

                    do
                    {
                        if (enemyIndex < 0 || enemyIndex >= enemyCount)
                            continue;

                        float3 closestPoint = ClosestPointOnSegment(enemyPositions[enemyIndex], segment.StartPoint, segment.EndPoint);
                        float3 delta = enemyPositions[enemyIndex] - closestPoint;
                        delta.y = 0f;
                        float combinedRadius = math.max(0.01f, segment.CollisionRadius + math.max(0.05f, enemyBodyRadii[enemyIndex]));

                        if (math.lengthsq(delta) > combinedRadius * combinedRadius)
                            continue;

                        float distanceAlongSegment = math.distance(segment.StartPoint, closestPoint);
                        AddHitCandidate(ref hitCandidates,
                                        enemyIndex,
                                        accumulatedDistanceBeforeSegment + distanceAlongSegment,
                                        closestPoint,
                                        segment.Direction);
                    }
                    while (enemyCellMap.TryGetNextValue(out enemyIndex, ref iterator));
                }
            }

            accumulatedDistanceBeforeSegment += math.max(0f, segment.Length);
        }
    }

    /// <summary>
    /// Filters one sorted hit-candidate list down to the candidates crossed by the traveling tick packet during the current frame.
    /// </summary>
    /// <param name="hitCandidates">Sorted lane hit candidates.</param>
    /// <param name="startDistance">Packet center distance at the start of the frame.</param>
    /// <param name="endDistance">Packet center distance at the end of the frame.</param>
    /// <param name="traversedHitCandidates">Output list reused by the caller.</param>
    public static void CollectTraversedHitCandidates(in NativeList<LaserBeamHitCandidate> hitCandidates,
                                                     float startDistance,
                                                     float endDistance,
                                                     ref NativeList<LaserBeamHitCandidate> traversedHitCandidates)
    {
        traversedHitCandidates.Clear();
        float minimumDistance = math.min(startDistance, endDistance);
        float maximumDistance = math.max(startDistance, endDistance);

        if (maximumDistance <= minimumDistance)
            return;

        for (int candidateIndex = 0; candidateIndex < hitCandidates.Length; candidateIndex++)
        {
            LaserBeamHitCandidate hitCandidate = hitCandidates[candidateIndex];

            if (hitCandidate.DistanceAlongLane < minimumDistance)
                continue;

            if (hitCandidate.DistanceAlongLane > maximumDistance)
                break;

            traversedHitCandidates.Add(hitCandidate);
        }
    }

    /// <summary>
    /// Applies one continuous-damage budget slice to every enemy currently intersecting one lane.
    /// </summary>
    /// <param name="laneDamageBudget">Damage budget emitted by the lane for the current application slice before lane multipliers.</param>
    /// <param name="referenceSegment">First segment of the lane, used to inherit lane-local damage scaling.</param>
    /// <param name="hitCandidates">Sorted lane hit candidates.</param>
    /// <param name="enemyEntities">Projected enemy entities.</param>
    /// <param name="projectedEnemyHealth">Mutable projected enemy health buffer.</param>
    /// <param name="enemyDirtyFlags">Per-enemy dirty flags tracking health updates.</param>
    /// <param name="despawnRequestLookup">Lookup used to avoid duplicate despawn requests.</param>
    /// <param name="commandBuffer">ECB used to enqueue despawn requests.</param>
    public static void ApplyContinuousLaneDamageBudget(float laneDamageBudget,
                                                       in PlayerLaserBeamLaneElement referenceSegment,
                                                       in NativeList<LaserBeamHitCandidate> hitCandidates,
                                                       NativeArray<Entity> enemyEntities,
                                                       ref NativeArray<EnemyHealth> projectedEnemyHealth,
                                                       ref NativeArray<byte> enemyDirtyFlags,
                                                       in ComponentLookup<EnemyDespawnRequest> despawnRequestLookup,
                                                       ref EntityCommandBuffer commandBuffer)
    {
        float effectiveLaneDamageBudget = math.max(0f, laneDamageBudget * math.max(0f, referenceSegment.DamageMultiplier));

        if (effectiveLaneDamageBudget <= 0f)
            return;

        for (int candidateIndex = 0; candidateIndex < hitCandidates.Length; candidateIndex++)
        {
            LaserBeamHitCandidate hitCandidate = hitCandidates[candidateIndex];

            if (!TryApplyFlatDamageHit(ref projectedEnemyHealth,
                                       hitCandidate.EnemyIndex,
                                       effectiveLaneDamageBudget,
                                       out bool _))
            {
                continue;
            }

            enemyDirtyFlags[hitCandidate.EnemyIndex] = 1;
            TryScheduleDespawn(enemyEntities[hitCandidate.EnemyIndex],
                               projectedEnemyHealth[hitCandidate.EnemyIndex],
                               in despawnRequestLookup,
                               ref commandBuffer);
        }
    }

    /// <summary>
    /// Applies one traveling tick packet against the filtered lane candidates using the projectile penetration rules inherited by the beam.
    /// </summary>
    /// <param name="shooterEntity">Player entity owning the beam.</param>
    /// <param name="laneDamagePerTick">Damage budget carried by the packet before lane multipliers.</param>
    /// <param name="penetrationMode">Projectile penetration mode inherited from the current shooting config.</param>
    /// <param name="maximumPenetrations">Maximum penetration budget inherited from the current shooting config.</param>
    /// <param name="projectileTemplate">Projectile template used to resolve knockback, elemental and VFX payloads.</param>
    /// <param name="laserBeamLanes">Resolved lane buffer of the current player.</param>
    /// <param name="segmentStartIndex">First segment index belonging to the lane.</param>
    /// <param name="hitCandidates">Filtered lane hit candidates crossed by the packet during the current frame.</param>
    /// <param name="enemyEntities">Projected enemy entities.</param>
    /// <param name="projectedEnemyHealth">Mutable projected enemy health buffer.</param>
    /// <param name="enemyPositions">Cached world positions of projected enemies.</param>
    /// <param name="enemyRuntimeArray">Cached runtime states of projected enemies.</param>
    /// <param name="enemyDataArray">Cached immutable data of projected enemies.</param>
    /// <param name="projectedEnemyKnockback">Mutable projected knockback buffer.</param>
    /// <param name="enemyDirtyFlags">Per-enemy dirty flags tracking health updates.</param>
    /// <param name="enemyKnockbackDirtyFlags">Per-enemy dirty flags tracking knockback updates.</param>
    /// <param name="elementalVfxConfigLookup">Lookup of player-owned elemental VFX config.</param>
    /// <param name="elementalVfxAnchorLookup">Lookup of enemy-owned elemental VFX anchors.</param>
    /// <param name="enemyHitVfxConfigLookup">Lookup of enemy hit VFX config.</param>
    /// <param name="spawnInactivityLockLookup">Lookup used by hit VFX payload spawning.</param>
    /// <param name="canEnqueueVfxRequests">True when the shooter can enqueue VFX requests this frame.</param>
    /// <param name="shooterVfxRequests">Mutable shooter VFX buffer.</param>
    /// <param name="elementalStackLookup">Mutable elemental stack lookup on enemies.</param>
    /// <param name="despawnRequestLookup">Lookup used to avoid duplicate despawn requests.</param>
    /// <param name="commandBuffer">ECB used to enqueue despawn requests.</param>
    public static void ResolveLaneHits(Entity shooterEntity,
                                       float laneDamagePerTick,
                                       ProjectilePenetrationMode penetrationMode,
                                       int maximumPenetrations,
                                       PlayerProjectileRequestTemplate projectileTemplate,
                                       in DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                                       int segmentStartIndex,
                                       in NativeList<LaserBeamHitCandidate> hitCandidates,
                                       NativeArray<Entity> enemyEntities,
                                       ref NativeArray<EnemyHealth> projectedEnemyHealth,
                                       in NativeArray<float3> enemyPositions,
                                       in NativeArray<EnemyRuntimeState> enemyRuntimeArray,
                                       in NativeArray<EnemyData> enemyDataArray,
                                       ref NativeArray<EnemyKnockbackState> projectedEnemyKnockback,
                                       ref NativeArray<byte> enemyDirtyFlags,
                                       ref NativeArray<byte> enemyFlashDirtyFlags,
                                       ref NativeArray<byte> enemyKnockbackDirtyFlags,
                                       in ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup,
                                       in ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup,
                                       in ComponentLookup<EnemyHitVfxConfig> enemyHitVfxConfigLookup,
                                       in ComponentLookup<EnemySpawnInactivityLock> spawnInactivityLockLookup,
                                       bool canEnqueueVfxRequests,
                                       ref DynamicBuffer<PlayerPowerUpVfxSpawnRequest> shooterVfxRequests,
                                       ref BufferLookup<EnemyElementStackElement> elementalStackLookup,
                                       in ComponentLookup<EnemyDespawnRequest> despawnRequestLookup,
                                       ref EntityCommandBuffer commandBuffer)
    {
        PlayerLaserBeamDamagePacketHitUtility.ResolveLaneHits(shooterEntity,
                                                              laneDamagePerTick,
                                                              penetrationMode,
                                                              maximumPenetrations,
                                                              projectileTemplate,
                                                              in laserBeamLanes,
                                                              segmentStartIndex,
                                                              in hitCandidates,
                                                              enemyEntities,
                                                              ref projectedEnemyHealth,
                                                              in enemyPositions,
                                                              in enemyRuntimeArray,
                                                              in enemyDataArray,
                                                              ref projectedEnemyKnockback,
                                                              ref enemyDirtyFlags,
                                                              ref enemyFlashDirtyFlags,
                                                              ref enemyKnockbackDirtyFlags,
                                                              in elementalVfxConfigLookup,
                                                              in elementalVfxAnchorLookup,
                                                              in enemyHitVfxConfigLookup,
                                                              in spawnInactivityLockLookup,
                                                              canEnqueueVfxRequests,
                                                              ref shooterVfxRequests,
                                                              ref elementalStackLookup,
                                                              in despawnRequestLookup,
                                                              ref commandBuffer);
    }

    /// <summary>
    /// Applies flat damage to one enemy when it is still alive.
    /// </summary>
    /// <param name="projectedEnemyHealth">Mutable projected enemy health buffer.</param>
    /// <param name="enemyIndex">Enemy index receiving damage.</param>
    /// <param name="damage">Flat damage amount to apply.</param>
    /// <param name="enemyKilled">True when the damage reduced the enemy health to zero.</param>
    /// <returns>True when damage was applied.</returns>
    internal static bool TryApplyFlatDamageHit(ref NativeArray<EnemyHealth> projectedEnemyHealth,
                                               int enemyIndex,
                                               float damage,
                                               out bool enemyKilled)
    {
        enemyKilled = false;

        if (enemyIndex < 0 || enemyIndex >= projectedEnemyHealth.Length)
            return false;

        EnemyHealth enemyHealth = projectedEnemyHealth[enemyIndex];

        if (enemyHealth.Current <= 0f)
            return false;

        EnemyDamageUtility.ApplyFlatShieldDamage(ref enemyHealth, damage);
        enemyKilled = enemyHealth.Current <= 0f;
        projectedEnemyHealth[enemyIndex] = enemyHealth;
        return true;
    }

    /// <summary>
    /// Consumes flat damage against one enemy and returns the leftover budget that can continue through the lane.
    /// </summary>
    /// <param name="projectedEnemyHealth">Mutable projected enemy health buffer.</param>
    /// <param name="enemyIndex">Enemy index receiving damage.</param>
    /// <param name="damage">Incoming flat damage amount to consume.</param>
    /// <param name="enemyKilled">True when the damage reduced the enemy health to zero.</param>
    /// <returns>Remaining unspent damage.</returns>
    internal static float ApplyDamageBasedHit(ref NativeArray<EnemyHealth> projectedEnemyHealth,
                                              int enemyIndex,
                                              float damage,
                                              out bool enemyKilled)
    {
        enemyKilled = false;

        if (enemyIndex < 0 || enemyIndex >= projectedEnemyHealth.Length)
            return damage;

        EnemyHealth enemyHealth = projectedEnemyHealth[enemyIndex];

        if (enemyHealth.Current <= 0f)
            return damage;

        float leftoverDamage = EnemyDamageUtility.ConsumeFlatShieldDamage(ref enemyHealth, damage);
        enemyKilled = enemyHealth.Current <= 0f;
        projectedEnemyHealth[enemyIndex] = enemyHealth;
        return leftoverDamage;
    }

    /// <summary>
    /// Enqueues a kill-despawn request when the enemy has no remaining health.
    /// </summary>
    /// <param name="enemyEntity">Enemy entity to despawn.</param>
    /// <param name="enemyHealth">Current projected health of the enemy.</param>
    /// <param name="despawnRequestLookup">Lookup used to avoid duplicate despawn requests.</param>
    /// <param name="commandBuffer">ECB used to enqueue despawn requests.</param>
    internal static void TryScheduleDespawn(Entity enemyEntity,
                                            EnemyHealth enemyHealth,
                                            in ComponentLookup<EnemyDespawnRequest> despawnRequestLookup,
                                            ref EntityCommandBuffer commandBuffer)
    {
        if (enemyHealth.Current > 0f)
            return;

        if (despawnRequestLookup.HasComponent(enemyEntity))
            return;

        commandBuffer.AddComponent(enemyEntity, new EnemyDespawnRequest
        {
            Reason = EnemyDespawnReason.Killed
        });
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Inserts or updates one lane hit candidate while keeping the list sorted by travel distance.
    /// </summary>
    /// <param name="hitCandidates">Output list reused across lane evaluations.</param>
    /// <param name="enemyIndex">Enemy index associated with the candidate.</param>
    /// <param name="distanceAlongLane">Distance from the lane origin to the candidate hit point.</param>
    /// <param name="hitPoint">World-space hit point stored for payload application.</param>
    /// <param name="hitDirection">World-space forward direction stored for payload application.</param>
    private static void AddHitCandidate(ref NativeList<LaserBeamHitCandidate> hitCandidates,
                                        int enemyIndex,
                                        float distanceAlongLane,
                                        float3 hitPoint,
                                        float3 hitDirection)
    {
        for (int candidateIndex = 0; candidateIndex < hitCandidates.Length; candidateIndex++)
        {
            if (hitCandidates[candidateIndex].EnemyIndex != enemyIndex)
                continue;

            if (distanceAlongLane >= hitCandidates[candidateIndex].DistanceAlongLane)
                return;

            hitCandidates[candidateIndex] = new LaserBeamHitCandidate
            {
                EnemyIndex = enemyIndex,
                DistanceAlongLane = distanceAlongLane,
                HitPoint = hitPoint,
                HitDirection = hitDirection
            };
            SortCandidates(ref hitCandidates);
            return;
        }

        hitCandidates.Add(new LaserBeamHitCandidate
        {
            EnemyIndex = enemyIndex,
            DistanceAlongLane = distanceAlongLane,
            HitPoint = hitPoint,
            HitDirection = hitDirection
        });
        SortCandidates(ref hitCandidates);
    }

    /// <summary>
    /// Keeps one candidate list sorted by travel distance using insertion-sort semantics on the most recent write.
    /// </summary>
    /// <param name="hitCandidates">Output list reused across lane evaluations.</param>
    private static void SortCandidates(ref NativeList<LaserBeamHitCandidate> hitCandidates)
    {
        for (int candidateIndex = hitCandidates.Length - 1; candidateIndex > 0; candidateIndex--)
        {
            LaserBeamHitCandidate currentCandidate = hitCandidates[candidateIndex];
            LaserBeamHitCandidate previousCandidate = hitCandidates[candidateIndex - 1];

            if (previousCandidate.DistanceAlongLane <= currentCandidate.DistanceAlongLane)
                break;

            hitCandidates[candidateIndex - 1] = currentCandidate;
            hitCandidates[candidateIndex] = previousCandidate;
        }
    }

    /// <summary>
    /// Resolves the closest point on a finite 3D segment to the requested point.
    /// </summary>
    /// <param name="point">Query point.</param>
    /// <param name="segmentStart">Segment start point.</param>
    /// <param name="segmentEnd">Segment end point.</param>
    /// <returns>Closest point on the segment.</returns>
    private static float3 ClosestPointOnSegment(float3 point,
                                                float3 segmentStart,
                                                float3 segmentEnd)
    {
        float3 segment = segmentEnd - segmentStart;
        float segmentLengthSquared = math.lengthsq(segment);

        if (segmentLengthSquared <= 1e-6f)
            return segmentStart;

        float projection = math.dot(point - segmentStart, segment) / segmentLengthSquared;
        float clampedProjection = math.clamp(projection, 0f, 1f);
        return segmentStart + segment * clampedProjection;
    }
    #endregion

    #endregion
}
