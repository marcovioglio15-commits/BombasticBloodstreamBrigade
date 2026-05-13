using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Applies shared enemy-hit payloads such as elemental stacks, knockback, and hit VFX for projectile-like damage sources.
/// /params None.
/// /returns None.
/// </summary>
public static class EnemyHitPayloadRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies all secondary hit payloads for one enemy impact and returns whether knockback state changed.
    /// /params enemyIndex Index of the impacted enemy inside the projected enemy arrays.
    /// /params shooterEntity Shooter entity used to resolve player-authored elemental VFX definitions.
    /// /params impactPosition World-space position used to spawn elemental and hit-react VFX for this impact.
    /// /params projectileData Projectile payload data used for knockback and explosion-derived metadata.
    /// /params projectileTransform Projectile transform used to resolve knockback direction.
    /// /params elementalPayload Elemental payload applied on hit.
    /// /params enemyEntities Enemy entity array indexed by the enemy query order.
    /// /params enemyPositions Enemy world positions indexed by the enemy query order.
    /// /params enemyRuntimeArray Enemy runtime state array indexed by the enemy query order.
    /// /params enemyDataArray Enemy immutable data array indexed by the enemy query order.
    /// /params projectedEnemyKnockback Mutable projected knockback state array.
    /// /params elementalVfxConfigLookup Lookup used to resolve shooter-authored elemental VFX presets.
    /// /params elementalVfxAnchorLookup Lookup used to resolve optional enemy follow anchors for elemental VFX.
    /// /params enemyHitVfxConfigLookup Lookup used to resolve one-shot enemy hit-react VFX.
    /// /params spawnInactivityLockLookup Lookup used to block knockback while enemies are spawn-locked.
    /// /params canEnqueueVfxRequests True when the shooter has a writable VFX request buffer.
    /// /params vfxRequests Writable shooter-side VFX request buffer.
    /// /params elementalStackLookup Writable enemy elemental-stack lookup.
    /// /returns True when the projected knockback state changed, otherwise false.
    /// </summary>
    public static bool ApplyEnemyHitPayloads(int enemyIndex,
                                             Entity shooterEntity,
                                             float3 impactPosition,
                                             in Projectile projectileData,
                                             in LocalTransform projectileTransform,
                                             in ProjectileElementalPayload elementalPayload,
                                             NativeArray<Entity> enemyEntities,
                                             NativeArray<float3> enemyPositions,
                                             NativeArray<EnemyRuntimeState> enemyRuntimeArray,
                                             NativeArray<EnemyData> enemyDataArray,
                                             ref NativeArray<EnemyKnockbackState> projectedEnemyKnockback,
                                             in ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup,
                                             in ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup,
                                             in ComponentLookup<EnemyHitVfxConfig> enemyHitVfxConfigLookup,
                                             in ComponentLookup<EnemySpawnInactivityLock> spawnInactivityLockLookup,
                                             bool canEnqueueVfxRequests,
                                             ref DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests,
                                             ref BufferLookup<EnemyElementStackElement> elementalStackLookup)
    {
        if (enemyIndex < 0 ||
            enemyIndex >= enemyEntities.Length ||
            enemyIndex >= enemyPositions.Length ||
            enemyIndex >= enemyRuntimeArray.Length ||
            enemyIndex >= enemyDataArray.Length)
        {
            return false;
        }

        Entity enemyEntity = enemyEntities[enemyIndex];
        float3 enemyPosition = enemyPositions[enemyIndex];
        EnemyRuntimeState enemyRuntimeState = enemyRuntimeArray[enemyIndex];
        EnemyData enemyData = enemyDataArray[enemyIndex];
        TryApplyElementalPayloads(enemyEntity,
                                  impactPosition,
                                  shooterEntity,
                                  in elementalPayload,
                                  in enemyRuntimeState,
                                  in elementalVfxConfigLookup,
                                  in elementalVfxAnchorLookup,
                                  canEnqueueVfxRequests,
                                  ref vfxRequests,
                                  ref elementalStackLookup);
        bool knockbackChanged = TryApplyKnockbackPayload(enemyIndex,
                                                         enemyEntity,
                                                         enemyPosition,
                                                         in projectileData,
                                                         in projectileTransform,
                                                         in enemyData,
                                                         ref projectedEnemyKnockback,
                                                         in spawnInactivityLockLookup);
        TryEnqueueEnemyHitVfx(enemyEntity,
                              impactPosition,
                              in enemyRuntimeState,
                              in enemyHitVfxConfigLookup,
                              canEnqueueVfxRequests,
                              ref vfxRequests);
        return knockbackChanged;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies elemental stacks and queues any related stack or proc VFX for one enemy impact.
    /// /params enemyEntity Impacted enemy entity.
    /// /params enemyPosition World-space impact position used for elemental VFX requests.
    /// /params shooterEntity Shooter entity used to resolve elemental VFX definitions.
    /// /params elementalPayload Elemental payload applied on hit.
    /// /params enemyRuntimeState Enemy runtime state used for follow-validation metadata.
    /// /params elementalVfxConfigLookup Lookup used to resolve shooter-authored elemental VFX presets.
    /// /params elementalVfxAnchorLookup Lookup used to resolve optional enemy follow anchors for elemental VFX.
    /// /params canEnqueueVfxRequests True when the shooter has a writable VFX request buffer.
    /// /params vfxRequests Writable shooter-side VFX request buffer.
    /// /params elementalStackLookup Writable enemy elemental-stack lookup.
    /// /returns None.
    /// </summary>
    private static void TryApplyElementalPayloads(Entity enemyEntity,
                                                  float3 enemyPosition,
                                                  Entity shooterEntity,
                                                  in ProjectileElementalPayload elementalPayload,
                                                  in EnemyRuntimeState enemyRuntimeState,
                                                  in ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup,
                                                  in ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup,
                                                  bool canEnqueueVfxRequests,
                                                  ref DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests,
                                                  ref BufferLookup<EnemyElementStackElement> elementalStackLookup)
    {
        if (!ProjectileElementalPayloadUtility.HasAnyPayload(in elementalPayload))
            return;

        Entity followTargetEntity = enemyEntity;

        if (elementalVfxAnchorLookup.HasComponent(enemyEntity))
        {
            Entity anchorEntity = elementalVfxAnchorLookup[enemyEntity].AnchorEntity;

            if (anchorEntity != Entity.Null)
                followTargetEntity = anchorEntity;
        }

        int elementalPayloadEntryCount = ProjectileElementalPayloadUtility.GetEntryCount(in elementalPayload);

        for (int payloadIndex = 0; payloadIndex < elementalPayloadEntryCount; payloadIndex++)
        {
            ProjectileElementalPayloadEntry payloadEntry = ProjectileElementalPayloadUtility.GetEntry(in elementalPayload,
                                                                                                     payloadIndex);

            if (payloadEntry.StacksPerHit <= 0f)
                continue;

            ElementalEffectConfig payloadEffect = payloadEntry.Effect;
            bool procTriggered;
            bool applied = EnemyElementalStackUtility.TryApplyStacks(enemyEntity,
                                                                     math.max(0f, payloadEntry.StacksPerHit),
                                                                     payloadEffect,
                                                                     ref elementalStackLookup,
                                                                     out procTriggered);

            if (!applied || !canEnqueueVfxRequests)
                continue;

            ElementalVfxDefinitionConfig elementalVfxConfig = ResolveElementalVfxDefinition(shooterEntity,
                                                                                            payloadEffect.ElementType,
                                                                                            in elementalVfxConfigLookup);

            if (elementalVfxConfig.SpawnStackVfx != 0)
                EnqueueElementalVfx(ref vfxRequests,
                                    elementalVfxConfig.StackVfxPrefabEntity,
                                    enemyPosition,
                                    elementalVfxConfig.StackVfxScaleMultiplier,
                                    followTargetEntity,
                                    enemyEntity,
                                    enemyRuntimeState.SpawnVersion,
                                    0.35f);

            if (!procTriggered || elementalVfxConfig.SpawnProcVfx == 0)
                continue;

            EnqueueElementalVfx(ref vfxRequests,
                                elementalVfxConfig.ProcVfxPrefabEntity,
                                enemyPosition,
                                elementalVfxConfig.ProcVfxScaleMultiplier,
                                followTargetEntity,
                                enemyEntity,
                                enemyRuntimeState.SpawnVersion,
                                ResolveProcVfxLifetimeSeconds(in payloadEffect));
        }
    }

    /// <summary>
    /// Applies projectile-derived knockback to one projected enemy state when the enemy is eligible.
    /// /params enemyIndex Index of the impacted enemy inside the projected knockback array.
    /// /params enemyEntity Impacted enemy entity.
    /// /params enemyPosition Enemy world position used by the knockback solver.
    /// /params projectileData Projectile payload data used by the knockback solver.
    /// /params projectileTransform Projectile transform used by the knockback solver.
    /// /params enemyData Immutable target enemy data used to inspect knockback immunity.
    /// /params projectedEnemyKnockback Mutable projected knockback state array.
    /// /params spawnInactivityLockLookup Lookup used to block knockback while enemies are spawn-locked.
    /// /returns True when the projected knockback state changed, otherwise false.
    /// </summary>
    private static bool TryApplyKnockbackPayload(int enemyIndex,
                                                 Entity enemyEntity,
                                                 float3 enemyPosition,
                                                 in Projectile projectileData,
                                                 in LocalTransform projectileTransform,
                                                 in EnemyData enemyData,
                                                 ref NativeArray<EnemyKnockbackState> projectedEnemyKnockback,
                                                 in ComponentLookup<EnemySpawnInactivityLock> spawnInactivityLockLookup)
    {
        if (enemyIndex < 0 || enemyIndex >= projectedEnemyKnockback.Length)
            return false;

        if (enemyData.DisablePlayerKnockback != 0)
            return false;

        if (spawnInactivityLockLookup.HasComponent(enemyEntity) &&
            spawnInactivityLockLookup.IsComponentEnabled(enemyEntity))
        {
            return false;
        }

        EnemyKnockbackState previousState = projectedEnemyKnockback[enemyIndex];
        EnemyKnockbackState updatedState = previousState;

        if (!EnemyKnockbackRuntimeUtility.TryApplyFromProjectile(in projectileData,
                                                                 in projectileTransform,
                                                                 enemyPosition,
                                                                 ref updatedState))
        {
            return false;
        }

        projectedEnemyKnockback[enemyIndex] = updatedState;
        return DidKnockbackStateChange(previousState, updatedState);
    }

    /// <summary>
    /// Compares two knockback states to determine whether the runtime payload path produced a meaningful change.
    /// /params leftValue Previous projected knockback state.
    /// /params rightValue Updated projected knockback state.
    /// /returns True when any tracked field differs, otherwise false.
    /// </summary>
    private static bool DidKnockbackStateChange(EnemyKnockbackState leftValue, EnemyKnockbackState rightValue)
    {
        return leftValue.RemainingTime != rightValue.RemainingTime ||
               leftValue.Velocity.x != rightValue.Velocity.x ||
               leftValue.Velocity.y != rightValue.Velocity.y ||
               leftValue.Velocity.z != rightValue.Velocity.z;
    }

    /// <summary>
    /// Queues the one-shot enemy hit-react VFX when the target enemy exposes a valid VFX configuration.
    /// /params enemyEntity Impacted enemy entity.
    /// /params enemyPosition World-space impact position used by the one-shot VFX.
    /// /params enemyRuntimeState Enemy runtime state used for follow-validation metadata.
    /// /params enemyHitVfxConfigLookup Lookup used to resolve baked hit-react VFX settings.
    /// /params canEnqueueVfxRequests True when the shooter has a writable VFX request buffer.
    /// /params vfxRequests Writable shooter-side VFX request buffer.
    /// /returns None.
    /// </summary>
    private static void TryEnqueueEnemyHitVfx(Entity enemyEntity,
                                              float3 enemyPosition,
                                              in EnemyRuntimeState enemyRuntimeState,
                                              in ComponentLookup<EnemyHitVfxConfig> enemyHitVfxConfigLookup,
                                              bool canEnqueueVfxRequests,
                                              ref DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests)
    {
        if (!canEnqueueVfxRequests || !enemyHitVfxConfigLookup.HasComponent(enemyEntity))
            return;

        EnemyHitVfxConfig hitVfxConfig = enemyHitVfxConfigLookup[enemyEntity];

        if (hitVfxConfig.PrefabEntity == Entity.Null)
            return;

        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = hitVfxConfig.PrefabEntity,
            Position = enemyPosition,
            Rotation = quaternion.identity,
            UniformScale = math.max(0.01f, hitVfxConfig.ScaleMultiplier),
            LifetimeSeconds = math.max(0.05f, hitVfxConfig.LifetimeSeconds),
            FollowTargetEntity = Entity.Null,
            FollowPositionOffset = float3.zero,
            FollowValidationEntity = enemyEntity,
            FollowValidationSpawnVersion = enemyRuntimeState.SpawnVersion,
            Velocity = float3.zero
        });
    }

    /// <summary>
    /// Resolves the elemental VFX definition authored on the shooter for one elemental type.
    /// /params shooterEntity Shooter entity used to resolve the authored elemental VFX config.
    /// /params elementType Elemental type whose VFX definition should be resolved.
    /// /params elementalVfxConfigLookup Lookup used to resolve shooter-authored elemental VFX presets.
    /// /returns Resolved elemental VFX definition, or default when unavailable.
    /// </summary>
    private static ElementalVfxDefinitionConfig ResolveElementalVfxDefinition(Entity shooterEntity,
                                                                              ElementType elementType,
                                                                              in ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup)
    {
        if (shooterEntity == Entity.Null || shooterEntity.Index < 0)
            return default;

        if (!elementalVfxConfigLookup.HasComponent(shooterEntity))
            return default;

        PlayerElementalVfxConfig elementalVfxConfig = elementalVfxConfigLookup[shooterEntity];

        switch (elementType)
        {
            case ElementType.Fire:
                return elementalVfxConfig.Fire;
            case ElementType.Ice:
                return elementalVfxConfig.Ice;
            case ElementType.Poison:
                return elementalVfxConfig.Poison;
            default:
                return elementalVfxConfig.Custom;
        }
    }

    /// <summary>
    /// Queues one elemental VFX spawn request when the prefab entity is valid.
    /// /params vfxRequests Writable shooter-side VFX request buffer.
    /// /params prefabEntity Prefab entity to spawn.
    /// /params position World-space spawn position.
    /// /params scaleMultiplier Uniform scale multiplier applied to the spawned VFX.
    /// /params followTargetEntity Optional follow target used by looping elemental VFX.
    /// /params followValidationEntity Entity used to validate the follow target.
    /// /params followValidationSpawnVersion Spawn version used for follow validation.
    /// /params lifetimeSeconds Requested VFX lifetime.
    /// /returns None.
    /// </summary>
    private static void EnqueueElementalVfx(ref DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests,
                                            Entity prefabEntity,
                                            float3 position,
                                            float scaleMultiplier,
                                            Entity followTargetEntity,
                                            Entity followValidationEntity,
                                            uint followValidationSpawnVersion,
                                            float lifetimeSeconds)
    {
        if (prefabEntity == Entity.Null)
            return;

        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = prefabEntity,
            Position = position,
            Rotation = quaternion.identity,
            UniformScale = math.max(0.01f, scaleMultiplier),
            LifetimeSeconds = math.max(0.05f, lifetimeSeconds),
            FollowTargetEntity = followTargetEntity,
            FollowPositionOffset = float3.zero,
            FollowValidationEntity = followValidationEntity,
            FollowValidationSpawnVersion = followValidationSpawnVersion,
            Velocity = float3.zero
        });
    }

    /// <summary>
    /// Resolves a stable lifetime for proc VFX based on the authored elemental effect kind.
    /// /params effectConfig Authored elemental effect config.
    /// /returns Stable proc VFX lifetime in seconds.
    /// </summary>
    private static float ResolveProcVfxLifetimeSeconds(in ElementalEffectConfig effectConfig)
    {
        switch (effectConfig.EffectKind)
        {
            case ElementalEffectKind.Dots:
                return math.max(0.05f, effectConfig.DotDurationSeconds);
            case ElementalEffectKind.Impediment:
                return math.max(0.05f, effectConfig.ImpedimentDurationSeconds);
            default:
                return 0.5f;
        }
    }
    #endregion

    #endregion
}
