using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Applies traveling Laser Beam tick packets using the projectile hit-payload rules inherited from the current shooting config.
/// </summary>
internal static class PlayerLaserBeamDamagePacketHitUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies one traveling tick packet against the filtered lane candidates using the projectile penetration rules inherited by the beam.
    /// </summary>
    /// <param name="shooterEntity">Player entity owning the beam.</param>
    /// <param name="laneDamagePerTick">Damage budget carried by the packet before lane multipliers.</param>
    /// <param name="pulseId">Unique id of the pulse being resolved.</param>
    /// <param name="pulseHits">Mutable pulse-hit history used to prevent duplicate enemy hits by the same pulse.</param>
    /// <param name="pulseHitSet">Mutable frame-local pulse-hit lookup synchronized with the persistent hit buffer.</param>
    /// <param name="penetrationMode">Projectile penetration mode inherited from the current shooting config.</param>
    /// <param name="maximumPenetrations">Maximum penetration budget inherited from the current shooting config.</param>
    /// <param name="projectileTemplate">Projectile template used to resolve knockback, elemental and VFX payloads.</param>
    /// <param name="laserBeamLanes">Resolved lane buffer of the current player.</param>
    /// <param name="segmentStartIndex">First segment index belonging to the lane.</param>
    /// <param name="hitCandidates">Filtered lane hit candidates covered by the pulse span.</param>
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
                                       int pulseId,
                                       DynamicBuffer<PlayerLaserBeamPulseHitElement> pulseHits,
                                       ref NativeParallelHashSet<PlayerLaserBeamPulseHitUtility.PulseHitKey> pulseHitSet,
                                       ProjectilePenetrationMode penetrationMode,
                                       int maximumPenetrations,
                                       PlayerProjectileRequestTemplate projectileTemplate,
                                       in DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                                       int segmentStartIndex,
                                       in NativeList<PlayerLaserBeamDamageResolutionUtility.LaserBeamHitCandidate> hitCandidates,
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
        PlayerLaserBeamLaneElement referenceSegment = laserBeamLanes[segmentStartIndex];
        float effectiveLaneDamagePerTick = math.max(0f, laneDamagePerTick * math.max(0f, referenceSegment.DamageMultiplier));

        switch (penetrationMode)
        {
            case ProjectilePenetrationMode.FixedHits:
                ResolveFixedHitMode(shooterEntity,
                                    effectiveLaneDamagePerTick,
                                    pulseId,
                                    pulseHits,
                                    ref pulseHitSet,
                                    maximumPenetrations,
                                    projectileTemplate,
                                    in referenceSegment,
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
                return;
            case ProjectilePenetrationMode.Infinite:
                ResolveInfiniteHitMode(shooterEntity,
                                       effectiveLaneDamagePerTick,
                                       pulseId,
                                       pulseHits,
                                       ref pulseHitSet,
                                       projectileTemplate,
                                       in referenceSegment,
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
                return;
            case ProjectilePenetrationMode.DamageBased:
                ResolveDamageBasedMode(shooterEntity,
                                       effectiveLaneDamagePerTick,
                                       pulseId,
                                       pulseHits,
                                       ref pulseHitSet,
                                       maximumPenetrations,
                                       projectileTemplate,
                                       in referenceSegment,
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
                return;
            default:
                ResolveSingleHitMode(shooterEntity,
                                     effectiveLaneDamagePerTick,
                                     pulseId,
                                     pulseHits,
                                     ref pulseHitSet,
                                     projectileTemplate,
                                     in referenceSegment,
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
                return;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies one single-hit packet to the nearest valid enemy.
    /// </summary>
    /// <param name="shooterEntity">Player entity owning the beam.</param>
    /// <param name="laneDamagePerTick">Effective lane damage carried by the packet.</param>
    /// <param name="pulseId">Unique id of the pulse being resolved.</param>
    /// <param name="pulseHits">Mutable pulse-hit history used to prevent duplicate enemy hits by the same pulse.</param>
    /// <param name="pulseHitSet">Mutable frame-local pulse-hit lookup synchronized with the persistent hit buffer.</param>
    /// <param name="projectileTemplate">Projectile template used to resolve hit payloads.</param>
    /// <param name="referenceSegment">Lane segment used to inherit direction and radius data.</param>
    /// <param name="hitCandidates">Filtered lane hit candidates covered by the pulse span.</param>
    /// <param name="enemyEntities">Projected enemy entities.</param>
    /// <param name="projectedEnemyHealth">Mutable projected enemy health buffer.</param>
    /// <param name="enemyPositions">Cached world positions of projected enemies.</param>
    /// <param name="enemyRuntimeArray">Cached runtime states of projected enemies.</param>
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
    private static void ResolveSingleHitMode(Entity shooterEntity,
                                             float laneDamagePerTick,
                                             int pulseId,
                                             DynamicBuffer<PlayerLaserBeamPulseHitElement> pulseHits,
                                             ref NativeParallelHashSet<PlayerLaserBeamPulseHitUtility.PulseHitKey> pulseHitSet,
                                             PlayerProjectileRequestTemplate projectileTemplate,
                                             in PlayerLaserBeamLaneElement referenceSegment,
                                             in NativeList<PlayerLaserBeamDamageResolutionUtility.LaserBeamHitCandidate> hitCandidates,
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
        for (int candidateIndex = 0; candidateIndex < hitCandidates.Length; candidateIndex++)
        {
            PlayerLaserBeamDamageResolutionUtility.LaserBeamHitCandidate hitCandidate = hitCandidates[candidateIndex];
            Entity enemyEntity = enemyEntities[hitCandidate.EnemyIndex];

            if (PlayerLaserBeamPulseHitUtility.HasPulseHit(in pulseHitSet, pulseId, enemyEntity))
                continue;

            if (!PlayerLaserBeamDamageResolutionUtility.TryApplyFlatDamageHit(ref projectedEnemyHealth,
                                                                              hitCandidate.EnemyIndex,
                                                                              laneDamagePerTick,
                                                                              out bool _))
            {
                continue;
            }

            PlayerLaserBeamPulseHitUtility.RegisterPulseHit(pulseHits, ref pulseHitSet, pulseId, enemyEntity);
            enemyDirtyFlags[hitCandidate.EnemyIndex] = 1;
            enemyFlashDirtyFlags[hitCandidate.EnemyIndex] = 1;
            ApplyHitPayloads(shooterEntity,
                             hitCandidate.EnemyIndex,
                             hitCandidate.HitPoint,
                             hitCandidate.HitDirection,
                             laneDamagePerTick,
                             projectileTemplate,
                             in referenceSegment,
                             enemyEntities,
                             in enemyPositions,
                             in enemyRuntimeArray,
                             in enemyDataArray,
                             ref projectedEnemyKnockback,
                             ref enemyKnockbackDirtyFlags,
                             in elementalVfxConfigLookup,
                             in elementalVfxAnchorLookup,
                             in enemyHitVfxConfigLookup,
                             in spawnInactivityLockLookup,
                             canEnqueueVfxRequests,
                             ref shooterVfxRequests,
                             ref elementalStackLookup);
            PlayerLaserBeamDamageResolutionUtility.TryScheduleDespawn(enemyEntity,
                                                                      projectedEnemyHealth[hitCandidate.EnemyIndex],
                                                                      in despawnRequestLookup,
                                                                      ref commandBuffer);
            return;
        }
    }

    /// <summary>
    /// Applies one fixed-hit packet to the ordered hit list until the penetration budget is exhausted.
    /// </summary>
    /// <param name="shooterEntity">Player entity owning the beam.</param>
    /// <param name="laneDamagePerTick">Effective lane damage carried by the packet.</param>
    /// <param name="pulseId">Unique id of the pulse being resolved.</param>
    /// <param name="pulseHits">Mutable pulse-hit history used to prevent duplicate enemy hits by the same pulse.</param>
    /// <param name="pulseHitSet">Mutable frame-local pulse-hit lookup synchronized with the persistent hit buffer.</param>
    /// <param name="maximumPenetrations">Maximum penetration budget inherited from the current shooting config.</param>
    /// <param name="projectileTemplate">Projectile template used to resolve hit payloads.</param>
    /// <param name="referenceSegment">Lane segment used to inherit direction and radius data.</param>
    /// <param name="hitCandidates">Filtered lane hit candidates covered by the pulse span.</param>
    /// <param name="enemyEntities">Projected enemy entities.</param>
    /// <param name="projectedEnemyHealth">Mutable projected enemy health buffer.</param>
    /// <param name="enemyPositions">Cached world positions of projected enemies.</param>
    /// <param name="enemyRuntimeArray">Cached runtime states of projected enemies.</param>
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
    private static void ResolveFixedHitMode(Entity shooterEntity,
                                            float laneDamagePerTick,
                                            int pulseId,
                                            DynamicBuffer<PlayerLaserBeamPulseHitElement> pulseHits,
                                            ref NativeParallelHashSet<PlayerLaserBeamPulseHitUtility.PulseHitKey> pulseHitSet,
                                            int maximumPenetrations,
                                            PlayerProjectileRequestTemplate projectileTemplate,
                                            in PlayerLaserBeamLaneElement referenceSegment,
                                            in NativeList<PlayerLaserBeamDamageResolutionUtility.LaserBeamHitCandidate> hitCandidates,
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
        int maximumHitCount = 1 + math.max(0, maximumPenetrations);
        int appliedHitCount = 0;

        for (int candidateIndex = 0; candidateIndex < hitCandidates.Length; candidateIndex++)
        {
            if (appliedHitCount >= maximumHitCount)
                return;

            PlayerLaserBeamDamageResolutionUtility.LaserBeamHitCandidate hitCandidate = hitCandidates[candidateIndex];
            Entity enemyEntity = enemyEntities[hitCandidate.EnemyIndex];

            if (PlayerLaserBeamPulseHitUtility.HasPulseHit(in pulseHitSet, pulseId, enemyEntity))
                continue;

            if (!PlayerLaserBeamDamageResolutionUtility.TryApplyFlatDamageHit(ref projectedEnemyHealth,
                                                                              hitCandidate.EnemyIndex,
                                                                              laneDamagePerTick,
                                                                              out bool _))
            {
                continue;
            }

            PlayerLaserBeamPulseHitUtility.RegisterPulseHit(pulseHits, ref pulseHitSet, pulseId, enemyEntity);
            appliedHitCount++;
            enemyDirtyFlags[hitCandidate.EnemyIndex] = 1;
            enemyFlashDirtyFlags[hitCandidate.EnemyIndex] = 1;
            ApplyHitPayloads(shooterEntity,
                             hitCandidate.EnemyIndex,
                             hitCandidate.HitPoint,
                             hitCandidate.HitDirection,
                             laneDamagePerTick,
                             projectileTemplate,
                             in referenceSegment,
                             enemyEntities,
                             in enemyPositions,
                             in enemyRuntimeArray,
                             in enemyDataArray,
                             ref projectedEnemyKnockback,
                             ref enemyKnockbackDirtyFlags,
                             in elementalVfxConfigLookup,
                             in elementalVfxAnchorLookup,
                             in enemyHitVfxConfigLookup,
                             in spawnInactivityLockLookup,
                             canEnqueueVfxRequests,
                             ref shooterVfxRequests,
                             ref elementalStackLookup);
            PlayerLaserBeamDamageResolutionUtility.TryScheduleDespawn(enemyEntity,
                                                                      projectedEnemyHealth[hitCandidate.EnemyIndex],
                                                                      in despawnRequestLookup,
                                                                      ref commandBuffer);
        }
    }

    /// <summary>
    /// Applies one infinite-penetration packet to every crossed enemy.
    /// </summary>
    /// <param name="shooterEntity">Player entity owning the beam.</param>
    /// <param name="laneDamagePerTick">Effective lane damage carried by the packet.</param>
    /// <param name="pulseId">Unique id of the pulse being resolved.</param>
    /// <param name="pulseHits">Mutable pulse-hit history used to prevent duplicate enemy hits by the same pulse.</param>
    /// <param name="pulseHitSet">Mutable frame-local pulse-hit lookup synchronized with the persistent hit buffer.</param>
    /// <param name="projectileTemplate">Projectile template used to resolve hit payloads.</param>
    /// <param name="referenceSegment">Lane segment used to inherit direction and radius data.</param>
    /// <param name="hitCandidates">Filtered lane hit candidates covered by the pulse span.</param>
    /// <param name="enemyEntities">Projected enemy entities.</param>
    /// <param name="projectedEnemyHealth">Mutable projected enemy health buffer.</param>
    /// <param name="enemyPositions">Cached world positions of projected enemies.</param>
    /// <param name="enemyRuntimeArray">Cached runtime states of projected enemies.</param>
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
    private static void ResolveInfiniteHitMode(Entity shooterEntity,
                                               float laneDamagePerTick,
                                               int pulseId,
                                               DynamicBuffer<PlayerLaserBeamPulseHitElement> pulseHits,
                                               ref NativeParallelHashSet<PlayerLaserBeamPulseHitUtility.PulseHitKey> pulseHitSet,
                                               PlayerProjectileRequestTemplate projectileTemplate,
                                               in PlayerLaserBeamLaneElement referenceSegment,
                                               in NativeList<PlayerLaserBeamDamageResolutionUtility.LaserBeamHitCandidate> hitCandidates,
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
        for (int candidateIndex = 0; candidateIndex < hitCandidates.Length; candidateIndex++)
        {
            PlayerLaserBeamDamageResolutionUtility.LaserBeamHitCandidate hitCandidate = hitCandidates[candidateIndex];
            Entity enemyEntity = enemyEntities[hitCandidate.EnemyIndex];

            if (PlayerLaserBeamPulseHitUtility.HasPulseHit(in pulseHitSet, pulseId, enemyEntity))
                continue;

            if (!PlayerLaserBeamDamageResolutionUtility.TryApplyFlatDamageHit(ref projectedEnemyHealth,
                                                                              hitCandidate.EnemyIndex,
                                                                              laneDamagePerTick,
                                                                              out bool _))
            {
                continue;
            }

            PlayerLaserBeamPulseHitUtility.RegisterPulseHit(pulseHits, ref pulseHitSet, pulseId, enemyEntity);
            enemyDirtyFlags[hitCandidate.EnemyIndex] = 1;
            enemyFlashDirtyFlags[hitCandidate.EnemyIndex] = 1;
            ApplyHitPayloads(shooterEntity,
                             hitCandidate.EnemyIndex,
                             hitCandidate.HitPoint,
                             hitCandidate.HitDirection,
                             laneDamagePerTick,
                             projectileTemplate,
                             in referenceSegment,
                             enemyEntities,
                             in enemyPositions,
                             in enemyRuntimeArray,
                             in enemyDataArray,
                             ref projectedEnemyKnockback,
                             ref enemyKnockbackDirtyFlags,
                             in elementalVfxConfigLookup,
                             in elementalVfxAnchorLookup,
                             in enemyHitVfxConfigLookup,
                             in spawnInactivityLockLookup,
                             canEnqueueVfxRequests,
                             ref shooterVfxRequests,
                             ref elementalStackLookup);
            PlayerLaserBeamDamageResolutionUtility.TryScheduleDespawn(enemyEntity,
                                                                      projectedEnemyHealth[hitCandidate.EnemyIndex],
                                                                      in despawnRequestLookup,
                                                                      ref commandBuffer);
        }
    }

    /// <summary>
    /// Applies one damage-based packet that spends remaining damage budget while enemies are killed.
    /// </summary>
    /// <param name="shooterEntity">Player entity owning the beam.</param>
    /// <param name="laneDamagePerTick">Effective lane damage carried by the packet.</param>
    /// <param name="pulseId">Unique id of the pulse being resolved.</param>
    /// <param name="pulseHits">Mutable pulse-hit history used to prevent duplicate enemy hits by the same pulse.</param>
    /// <param name="pulseHitSet">Mutable frame-local pulse-hit lookup synchronized with the persistent hit buffer.</param>
    /// <param name="maximumPenetrations">Maximum kill-based penetration budget inherited from the current shooting config.</param>
    /// <param name="projectileTemplate">Projectile template used to resolve hit payloads.</param>
    /// <param name="referenceSegment">Lane segment used to inherit direction and radius data.</param>
    /// <param name="hitCandidates">Filtered lane hit candidates covered by the pulse span.</param>
    /// <param name="enemyEntities">Projected enemy entities.</param>
    /// <param name="projectedEnemyHealth">Mutable projected enemy health buffer.</param>
    /// <param name="enemyPositions">Cached world positions of projected enemies.</param>
    /// <param name="enemyRuntimeArray">Cached runtime states of projected enemies.</param>
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
    private static void ResolveDamageBasedMode(Entity shooterEntity,
                                               float laneDamagePerTick,
                                               int pulseId,
                                               DynamicBuffer<PlayerLaserBeamPulseHitElement> pulseHits,
                                               ref NativeParallelHashSet<PlayerLaserBeamPulseHitUtility.PulseHitKey> pulseHitSet,
                                               int maximumPenetrations,
                                               PlayerProjectileRequestTemplate projectileTemplate,
                                               in PlayerLaserBeamLaneElement referenceSegment,
                                               in NativeList<PlayerLaserBeamDamageResolutionUtility.LaserBeamHitCandidate> hitCandidates,
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
        float remainingDamage = math.max(0f, laneDamagePerTick);
        int consumedPenetrations = 0;

        for (int candidateIndex = 0; candidateIndex < hitCandidates.Length; candidateIndex++)
        {
            if (remainingDamage <= 0f)
                return;

            PlayerLaserBeamDamageResolutionUtility.LaserBeamHitCandidate hitCandidate = hitCandidates[candidateIndex];
            Entity enemyEntity = enemyEntities[hitCandidate.EnemyIndex];

            if (PlayerLaserBeamPulseHitUtility.HasPulseHit(in pulseHitSet, pulseId, enemyEntity))
                continue;

            bool enemyKilled;
            float leftoverDamage = PlayerLaserBeamDamageResolutionUtility.ApplyDamageBasedHit(ref projectedEnemyHealth,
                                                                                              hitCandidate.EnemyIndex,
                                                                                              remainingDamage,
                                                                                              out enemyKilled);

            if (leftoverDamage == remainingDamage)
                continue;

            PlayerLaserBeamPulseHitUtility.RegisterPulseHit(pulseHits, ref pulseHitSet, pulseId, enemyEntity);
            enemyDirtyFlags[hitCandidate.EnemyIndex] = 1;
            enemyFlashDirtyFlags[hitCandidate.EnemyIndex] = 1;
            ApplyHitPayloads(shooterEntity,
                             hitCandidate.EnemyIndex,
                             hitCandidate.HitPoint,
                             hitCandidate.HitDirection,
                             remainingDamage - leftoverDamage,
                             projectileTemplate,
                             in referenceSegment,
                             enemyEntities,
                             in enemyPositions,
                             in enemyRuntimeArray,
                             in enemyDataArray,
                             ref projectedEnemyKnockback,
                             ref enemyKnockbackDirtyFlags,
                             in elementalVfxConfigLookup,
                             in elementalVfxAnchorLookup,
                             in enemyHitVfxConfigLookup,
                             in spawnInactivityLockLookup,
                             canEnqueueVfxRequests,
                             ref shooterVfxRequests,
                             ref elementalStackLookup);
            PlayerLaserBeamDamageResolutionUtility.TryScheduleDespawn(enemyEntity,
                                                                      projectedEnemyHealth[hitCandidate.EnemyIndex],
                                                                      in despawnRequestLookup,
                                                                      ref commandBuffer);

            if (!enemyKilled)
                return;

            consumedPenetrations++;
            remainingDamage = leftoverDamage;

            if (consumedPenetrations > maximumPenetrations)
                return;
        }
    }

    /// <summary>
    /// Applies projectile-derived elemental, knockback and hit-VFX payloads to one enemy already damaged by the beam.
    /// </summary>
    /// <param name="shooterEntity">Player entity owning the beam.</param>
    /// <param name="enemyIndex">Enemy index receiving payloads.</param>
    /// <param name="hitPoint">World-space hit point used by the payload helpers.</param>
    /// <param name="hitDirection">World-space impact direction used by the payload helpers.</param>
    /// <param name="appliedDamage">Damage already applied to the enemy during this packet evaluation.</param>
    /// <param name="projectileTemplate">Projectile template used to resolve payload details.</param>
    /// <param name="referenceSegment">Lane segment used to inherit collision radius and fallback direction.</param>
    /// <param name="enemyEntities">Projected enemy entities.</param>
    /// <param name="enemyPositions">Cached world positions of projected enemies.</param>
    /// <param name="enemyRuntimeArray">Cached runtime states of projected enemies.</param>
    /// <param name="projectedEnemyKnockback">Mutable projected knockback buffer.</param>
    /// <param name="enemyKnockbackDirtyFlags">Per-enemy dirty flags tracking knockback updates.</param>
    /// <param name="elementalVfxConfigLookup">Lookup of player-owned elemental VFX config.</param>
    /// <param name="elementalVfxAnchorLookup">Lookup of enemy-owned elemental VFX anchors.</param>
    /// <param name="enemyHitVfxConfigLookup">Lookup of enemy hit VFX config.</param>
    /// <param name="spawnInactivityLockLookup">Lookup used by hit VFX payload spawning.</param>
    /// <param name="canEnqueueVfxRequests">True when the shooter can enqueue VFX requests this frame.</param>
    /// <param name="shooterVfxRequests">Mutable shooter VFX buffer.</param>
    /// <param name="elementalStackLookup">Mutable elemental stack lookup on enemies.</param>
    private static void ApplyHitPayloads(Entity shooterEntity,
                                         int enemyIndex,
                                         float3 hitPoint,
                                         float3 hitDirection,
                                         float appliedDamage,
                                         PlayerProjectileRequestTemplate projectileTemplate,
                                         in PlayerLaserBeamLaneElement referenceSegment,
                                         NativeArray<Entity> enemyEntities,
                                         in NativeArray<float3> enemyPositions,
                                         in NativeArray<EnemyRuntimeState> enemyRuntimeArray,
                                         in NativeArray<EnemyData> enemyDataArray,
                                         ref NativeArray<EnemyKnockbackState> projectedEnemyKnockback,
                                         ref NativeArray<byte> enemyKnockbackDirtyFlags,
                                         in ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup,
                                         in ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup,
                                         in ComponentLookup<EnemyHitVfxConfig> enemyHitVfxConfigLookup,
                                         in ComponentLookup<EnemySpawnInactivityLock> spawnInactivityLockLookup,
                                         bool canEnqueueVfxRequests,
                                         ref DynamicBuffer<PlayerPowerUpVfxSpawnRequest> shooterVfxRequests,
                                         ref BufferLookup<EnemyElementStackElement> elementalStackLookup)
    {
        Projectile projectileForPayloads = new Projectile
        {
            Velocity = math.normalizesafe(hitDirection, referenceSegment.Direction) * math.max(0f, projectileTemplate.Speed),
            Damage = math.max(0f, appliedDamage),
            ExplosionRadius = math.max(0f, projectileTemplate.ExplosionRadius),
            MaxRange = 0f,
            MaxLifetime = 0f,
            PenetrationMode = ProjectilePenetrationMode.None,
            RemainingPenetrations = 0,
            KnockbackEnabled = projectileTemplate.Knockback.Enabled,
            KnockbackStrength = math.max(0f, projectileTemplate.Knockback.Strength),
            KnockbackDurationSeconds = math.max(0f, projectileTemplate.Knockback.DurationSeconds),
            KnockbackDirectionMode = projectileTemplate.Knockback.DirectionMode,
            KnockbackStackingMode = projectileTemplate.Knockback.StackingMode,
            InheritPlayerSpeed = projectileTemplate.InheritPlayerSpeed,
            IgnoreInheritedPlayerVelocityX = projectileTemplate.IgnoreInheritedPlayerVelocityX,
            IgnoreInheritedPlayerVelocityZ = projectileTemplate.IgnoreInheritedPlayerVelocityZ
        };
        LocalTransform projectileTransform = LocalTransform.FromPositionRotationScale(hitPoint,
                                                                                     quaternion.identity,
                                                                                     math.max(0.01f, referenceSegment.CollisionRadius / PlayerLaserBeamUtility.BaseProjectileRadius));

        if (EnemyHitPayloadRuntimeUtility.ApplyEnemyHitPayloads(enemyIndex,
                                                                shooterEntity,
                                                                hitPoint,
                                                                in projectileForPayloads,
                                                                in projectileTransform,
                                                                in projectileTemplate.ElementalPayloadOverride,
                                                                enemyEntities,
                                                                enemyPositions,
                                                                enemyRuntimeArray,
                                                                enemyDataArray,
                                                                ref projectedEnemyKnockback,
                                                                in elementalVfxConfigLookup,
                                                                in elementalVfxAnchorLookup,
                                                                in enemyHitVfxConfigLookup,
                                                                in spawnInactivityLockLookup,
                                                                canEnqueueVfxRequests,
                                                                ref shooterVfxRequests,
                                                                ref elementalStackLookup))
        {
            enemyKnockbackDirtyFlags[enemyIndex] = 1;
        }
    }
    #endregion

    #endregion
}
