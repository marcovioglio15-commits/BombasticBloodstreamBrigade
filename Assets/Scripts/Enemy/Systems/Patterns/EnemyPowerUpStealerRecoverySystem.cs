using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// Recovers power-ups held by Power-Up Stealer enemies before pooled despawn clears their runtime state.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyKilledEventsSystem))]
[UpdateBefore(typeof(EnemyFinalizeDespawnSystem))]
public partial struct EnemyPowerUpStealerRecoverySystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the despawn and Stealer runtime data required to recover stolen power-ups.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyPowerUpStealerRuntimeElement>();
    }

    /// <summary>
    /// Restores or drops stolen power-ups before enemies are returned to their pools.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        PhysicsWorldSingleton physicsWorldSingleton = default;
        bool hasPhysicsWorld = SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out physicsWorldSingleton);
        EnemyPowerUpStealerPlayerAccess playerAccess = new EnemyPowerUpStealerPlayerAccess
        {
            PowerUpsConfigLookup = SystemAPI.GetComponentLookup<PlayerPowerUpsConfig>(false),
            PowerUpsStateLookup = SystemAPI.GetComponentLookup<PlayerPowerUpsState>(false),
            EquippedPassiveToolsLookup = SystemAPI.GetBufferLookup<EquippedPassiveToolElement>(false),
            PassiveToolsStateLookup = SystemAPI.GetComponentLookup<PlayerPassiveToolsState>(false),
            UnlockCatalogLookup = SystemAPI.GetBufferLookup<PlayerPowerUpUnlockCatalogElement>(false),
            ContainerConfigLookup = SystemAPI.GetComponentLookup<PlayerPowerUpContainerInteractionConfig>(true)
        };
        ComponentLookup<EnemyPowerUpStealerVisualState> visualStateLookup = SystemAPI.GetComponentLookup<EnemyPowerUpStealerVisualState>(false);
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((DynamicBuffer<EnemyPowerUpStealerRuntimeElement> stealerRuntime,
                  RefRO<EnemyHealth> enemyHealth,
                  RefRO<LocalTransform> enemyTransform,
                  Entity enemyEntity)
                 in SystemAPI.Query<DynamicBuffer<EnemyPowerUpStealerRuntimeElement>,
                                    RefRO<EnemyHealth>,
                                    RefRO<LocalTransform>>()
                             .WithAll<EnemyActive>()
                             .WithNone<EnemyDespawnRequest, EnemySpawnInactivityLock>()
                             .WithEntityAccess())
        {
            if (!EnemyPowerUpStealerRuntimeUtility.HasAnyStolenPowerUp(stealerRuntime))
                continue;

            EnemyPowerUpStealerRecoveryRuntimeUtility.TryRecoverStolenPowerUpsAfterDamage(enemyEntity,
                                                                                          enemyTransform.ValueRO.Position,
                                                                                          in enemyHealth.ValueRO,
                                                                                          deltaTime,
                                                                                          in physicsWorldSingleton,
                                                                                          hasPhysicsWorld,
                                                                                          stealerRuntime,
                                                                                          ref visualStateLookup,
                                                                                          ref playerAccess,
                                                                                          ref commandBuffer);
        }

        foreach ((DynamicBuffer<EnemyPowerUpStealerRuntimeElement> stealerRuntime,
                  RefRO<EnemyDespawnRequest> despawnRequest,
                  RefRO<LocalTransform> enemyTransform,
                  Entity enemyEntity)
                 in SystemAPI.Query<DynamicBuffer<EnemyPowerUpStealerRuntimeElement>,
                                    RefRO<EnemyDespawnRequest>,
                                    RefRO<LocalTransform>>()
                             .WithAll<EnemyDespawnRequest>()
                             .WithEntityAccess())
        {
            bool forceActiveContainerDrop = despawnRequest.ValueRO.Reason == EnemyDespawnReason.Killed;

            EnemyPowerUpStealerRecoveryRuntimeUtility.TryRecoverStolenPowerUps(enemyEntity,
                                                                               enemyTransform.ValueRO.Position,
                                                                               in physicsWorldSingleton,
                                                                               hasPhysicsWorld,
                                                                               forceActiveContainerDrop,
                                                                               stealerRuntime,
                                                                               ref visualStateLookup,
                                                                               ref playerAccess,
                                                                               ref commandBuffer);
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #endregion
}
