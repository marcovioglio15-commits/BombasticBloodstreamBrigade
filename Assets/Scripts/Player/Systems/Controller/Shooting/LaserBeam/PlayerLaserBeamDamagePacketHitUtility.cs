using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Applies traveling Laser Beam tick packets using the projectile hit-payload rules inherited from the current shooting config.
/// /params None.
/// /returns None.
/// </summary>
internal static class PlayerLaserBeamDamagePacketHitUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies one traveling tick packet against the filtered lane candidates using the projectile penetration rules inherited by the beam.
    /// /params shooterEntity Player entity owning the beam.
    /// /params laneDamagePerTick Damage budget carried by the packet before lane multipliers.
    /// /params penetrationMode Projectile penetration mode inherited from the current shooting config.
    /// /params maximumPenetrations Maximum penetration budget inherited from the current shooting config.
    /// /params projectileTemplate Projectile template used to resolve knockback, elemental and VFX payloads.
    /// /params laserBeamLanes Resolved lane buffer of the current player.
    /// /params segmentStartIndex First segment index belonging to the lane.
    /// /params hitCandidates Filtered lane hit candidates crossed by the packet during the current frame.
    /// /params enemyEntities Projected enemy entities.
    /// /params projectedEnemyHealth Mutable projected enemy health buffer.
    /// /params enemyPositions Cached world positions of projected enemies.
    /// /params enemyRuntimeArray Cached runtime states of projected enemies.
    /// /params enemyDataArray Cached immutable data of projected enemies.
    /// /params projectedEnemyKnockback Mutable projected knockback buffer.
    /// /params enemyDirtyFlags Per-enemy dirty flags tracking health updates.
    /// /params enemyKnockbackDirtyFlags Per-enemy dirty flags tracking knockback updates.
    /// /params elementalVfxConfigLookup Lookup of player-owned elemental VFX config.
    /// /params elementalVfxAnchorLookup Lookup of enemy-owned elemental VFX anchors.
    /// /params enemyHitVfxConfigLookup Lookup of enemy hit VFX config.
    /// /params spawnInactivityLockLookup Lookup used by hit VFX payload spawning.
    /// /params canEnqueueVfxRequests True when the shooter can enqueue VFX requests this frame.
    /// /params shooterVfxRequests Mutable shooter VFX buffer.
    /// /params elementalStackLookup Mutable elemental stack lookup on enemies.
    /// /params despawnRequestLookup Lookup used to avoid duplicate despawn requests.
    /// /params commandBuffer ECB used to enqueue despawn requests.
    /// /returns None.
    /// </summary>
    public static void ResolveLaneHits(Entity shooterEntity,
                                       float laneDamagePerTick,
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
    /// /params shooterEntity Player entity owning the beam.
    /// /params laneDamagePerTick Effective lane damage carried by the packet.
    /// /params projectileTemplate Projectile template used to resolve hit payloads.
    /// /params referenceSegment Lane segment used to inherit direction and radius data.
    /// /params hitCandidates Filtered lane hit candidates crossed by the packet during the current frame.
    /// /params enemyEntities Projected enemy entities.
    /// /params projectedEnemyHealth Mutable projected enemy health buffer.
    /// /params enemyPositions Cached world positions of projected enemies.
    /// /params enemyRuntimeArray Cached runtime states of projected enemies.
    /// /params projectedEnemyKnockback Mutable projected knockback buffer.
    /// /params enemyDirtyFlags Per-enemy dirty flags tracking health updates.
    /// /params enemyKnockbackDirtyFlags Per-enemy dirty flags tracking knockback updates.
    /// /params elementalVfxConfigLookup Lookup of player-owned elemental VFX config.
    /// /params elementalVfxAnchorLookup Lookup of enemy-owned elemental VFX anchors.
    /// /params enemyHitVfxConfigLookup Lookup of enemy hit VFX config.
    /// /params spawnInactivityLockLookup Lookup used by hit VFX payload spawning.
    /// /params canEnqueueVfxRequests True when the shooter can enqueue VFX requests this frame.
    /// /params shooterVfxRequests Mutable shooter VFX buffer.
    /// /params elementalStackLookup Mutable elemental stack lookup on enemies.
    /// /params despawnRequestLookup Lookup used to avoid duplicate despawn requests.
    /// /params commandBuffer ECB used to enqueue despawn requests.
    /// /returns None.
    /// </summary>
    private static void ResolveSingleHitMode(Entity shooterEntity,
                                             float laneDamagePerTick,
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

            if (!PlayerLaserBeamDamageResolutionUtility.TryApplyFlatDamageHit(ref projectedEnemyHealth,
                                                                              hitCandidate.EnemyIndex,
                                                                              laneDamagePerTick,
                                                                              out bool _))
            {
                continue;
            }

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
            PlayerLaserBeamDamageResolutionUtility.TryScheduleDespawn(enemyEntities[hitCandidate.EnemyIndex],
                                                                      projectedEnemyHealth[hitCandidate.EnemyIndex],
                                                                      in despawnRequestLookup,
                                                                      ref commandBuffer);
            return;
        }
    }

    /// <summary>
    /// Applies one fixed-hit packet to the ordered hit list until the penetration budget is exhausted.
    /// /params shooterEntity Player entity owning the beam.
    /// /params laneDamagePerTick Effective lane damage carried by the packet.
    /// /params maximumPenetrations Maximum penetration budget inherited from the current shooting config.
    /// /params projectileTemplate Projectile template used to resolve hit payloads.
    /// /params referenceSegment Lane segment used to inherit direction and radius data.
    /// /params hitCandidates Filtered lane hit candidates crossed by the packet during the current frame.
    /// /params enemyEntities Projected enemy entities.
    /// /params projectedEnemyHealth Mutable projected enemy health buffer.
    /// /params enemyPositions Cached world positions of projected enemies.
    /// /params enemyRuntimeArray Cached runtime states of projected enemies.
    /// /params projectedEnemyKnockback Mutable projected knockback buffer.
    /// /params enemyDirtyFlags Per-enemy dirty flags tracking health updates.
    /// /params enemyKnockbackDirtyFlags Per-enemy dirty flags tracking knockback updates.
    /// /params elementalVfxConfigLookup Lookup of player-owned elemental VFX config.
    /// /params elementalVfxAnchorLookup Lookup of enemy-owned elemental VFX anchors.
    /// /params enemyHitVfxConfigLookup Lookup of enemy hit VFX config.
    /// /params spawnInactivityLockLookup Lookup used by hit VFX payload spawning.
    /// /params canEnqueueVfxRequests True when the shooter can enqueue VFX requests this frame.
    /// /params shooterVfxRequests Mutable shooter VFX buffer.
    /// /params elementalStackLookup Mutable elemental stack lookup on enemies.
    /// /params despawnRequestLookup Lookup used to avoid duplicate despawn requests.
    /// /params commandBuffer ECB used to enqueue despawn requests.
    /// /returns None.
    /// </summary>
    private static void ResolveFixedHitMode(Entity shooterEntity,
                                            float laneDamagePerTick,
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

            if (!PlayerLaserBeamDamageResolutionUtility.TryApplyFlatDamageHit(ref projectedEnemyHealth,
                                                                              hitCandidate.EnemyIndex,
                                                                              laneDamagePerTick,
                                                                              out bool _))
            {
                continue;
            }

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
            PlayerLaserBeamDamageResolutionUtility.TryScheduleDespawn(enemyEntities[hitCandidate.EnemyIndex],
                                                                      projectedEnemyHealth[hitCandidate.EnemyIndex],
                                                                      in despawnRequestLookup,
                                                                      ref commandBuffer);
        }
    }

    /// <summary>
    /// Applies one infinite-penetration packet to every crossed enemy.
    /// /params shooterEntity Player entity owning the beam.
    /// /params laneDamagePerTick Effective lane damage carried by the packet.
    /// /params projectileTemplate Projectile template used to resolve hit payloads.
    /// /params referenceSegment Lane segment used to inherit direction and radius data.
    /// /params hitCandidates Filtered lane hit candidates crossed by the packet during the current frame.
    /// /params enemyEntities Projected enemy entities.
    /// /params projectedEnemyHealth Mutable projected enemy health buffer.
    /// /params enemyPositions Cached world positions of projected enemies.
    /// /params enemyRuntimeArray Cached runtime states of projected enemies.
    /// /params projectedEnemyKnockback Mutable projected knockback buffer.
    /// /params enemyDirtyFlags Per-enemy dirty flags tracking health updates.
    /// /params enemyKnockbackDirtyFlags Per-enemy dirty flags tracking knockback updates.
    /// /params elementalVfxConfigLookup Lookup of player-owned elemental VFX config.
    /// /params elementalVfxAnchorLookup Lookup of enemy-owned elemental VFX anchors.
    /// /params enemyHitVfxConfigLookup Lookup of enemy hit VFX config.
    /// /params spawnInactivityLockLookup Lookup used by hit VFX payload spawning.
    /// /params canEnqueueVfxRequests True when the shooter can enqueue VFX requests this frame.
    /// /params shooterVfxRequests Mutable shooter VFX buffer.
    /// /params elementalStackLookup Mutable elemental stack lookup on enemies.
    /// /params despawnRequestLookup Lookup used to avoid duplicate despawn requests.
    /// /params commandBuffer ECB used to enqueue despawn requests.
    /// /returns None.
    /// </summary>
    private static void ResolveInfiniteHitMode(Entity shooterEntity,
                                               float laneDamagePerTick,
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

            if (!PlayerLaserBeamDamageResolutionUtility.TryApplyFlatDamageHit(ref projectedEnemyHealth,
                                                                              hitCandidate.EnemyIndex,
                                                                              laneDamagePerTick,
                                                                              out bool _))
            {
                continue;
            }

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
            PlayerLaserBeamDamageResolutionUtility.TryScheduleDespawn(enemyEntities[hitCandidate.EnemyIndex],
                                                                      projectedEnemyHealth[hitCandidate.EnemyIndex],
                                                                      in despawnRequestLookup,
                                                                      ref commandBuffer);
        }
    }

    /// <summary>
    /// Applies one damage-based packet that spends remaining damage budget while enemies are killed.
    /// /params shooterEntity Player entity owning the beam.
    /// /params laneDamagePerTick Effective lane damage carried by the packet.
    /// /params maximumPenetrations Maximum kill-based penetration budget inherited from the current shooting config.
    /// /params projectileTemplate Projectile template used to resolve hit payloads.
    /// /params referenceSegment Lane segment used to inherit direction and radius data.
    /// /params hitCandidates Filtered lane hit candidates crossed by the packet during the current frame.
    /// /params enemyEntities Projected enemy entities.
    /// /params projectedEnemyHealth Mutable projected enemy health buffer.
    /// /params enemyPositions Cached world positions of projected enemies.
    /// /params enemyRuntimeArray Cached runtime states of projected enemies.
    /// /params projectedEnemyKnockback Mutable projected knockback buffer.
    /// /params enemyDirtyFlags Per-enemy dirty flags tracking health updates.
    /// /params enemyKnockbackDirtyFlags Per-enemy dirty flags tracking knockback updates.
    /// /params elementalVfxConfigLookup Lookup of player-owned elemental VFX config.
    /// /params elementalVfxAnchorLookup Lookup of enemy-owned elemental VFX anchors.
    /// /params enemyHitVfxConfigLookup Lookup of enemy hit VFX config.
    /// /params spawnInactivityLockLookup Lookup used by hit VFX payload spawning.
    /// /params canEnqueueVfxRequests True when the shooter can enqueue VFX requests this frame.
    /// /params shooterVfxRequests Mutable shooter VFX buffer.
    /// /params elementalStackLookup Mutable elemental stack lookup on enemies.
    /// /params despawnRequestLookup Lookup used to avoid duplicate despawn requests.
    /// /params commandBuffer ECB used to enqueue despawn requests.
    /// /returns None.
    /// </summary>
    private static void ResolveDamageBasedMode(Entity shooterEntity,
                                               float laneDamagePerTick,
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
            bool enemyKilled;
            float leftoverDamage = PlayerLaserBeamDamageResolutionUtility.ApplyDamageBasedHit(ref projectedEnemyHealth,
                                                                                              hitCandidate.EnemyIndex,
                                                                                              remainingDamage,
                                                                                              out enemyKilled);

            if (leftoverDamage == remainingDamage)
                continue;

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
            PlayerLaserBeamDamageResolutionUtility.TryScheduleDespawn(enemyEntities[hitCandidate.EnemyIndex],
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
    /// /params shooterEntity Player entity owning the beam.
    /// /params enemyIndex Enemy index receiving payloads.
    /// /params hitPoint World-space hit point used by the payload helpers.
    /// /params hitDirection World-space impact direction used by the payload helpers.
    /// /params appliedDamage Damage already applied to the enemy during this packet evaluation.
    /// /params projectileTemplate Projectile template used to resolve payload details.
    /// /params referenceSegment Lane segment used to inherit collision radius and fallback direction.
    /// /params enemyEntities Projected enemy entities.
    /// /params enemyPositions Cached world positions of projected enemies.
    /// /params enemyRuntimeArray Cached runtime states of projected enemies.
    /// /params projectedEnemyKnockback Mutable projected knockback buffer.
    /// /params enemyKnockbackDirtyFlags Per-enemy dirty flags tracking knockback updates.
    /// /params elementalVfxConfigLookup Lookup of player-owned elemental VFX config.
    /// /params elementalVfxAnchorLookup Lookup of enemy-owned elemental VFX anchors.
    /// /params enemyHitVfxConfigLookup Lookup of enemy hit VFX config.
    /// /params spawnInactivityLockLookup Lookup used by hit VFX payload spawning.
    /// /params canEnqueueVfxRequests True when the shooter can enqueue VFX requests this frame.
    /// /params shooterVfxRequests Mutable shooter VFX buffer.
    /// /params elementalStackLookup Mutable elemental stack lookup on enemies.
    /// /returns None.
    /// </summary>
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
            InheritPlayerSpeed = projectileTemplate.InheritPlayerSpeed
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
