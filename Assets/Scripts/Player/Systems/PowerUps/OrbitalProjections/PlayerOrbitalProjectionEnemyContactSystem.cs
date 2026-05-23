using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Applies repeated enemy contact damage from active player orbital projections.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyContactDamageSystem))]
[UpdateBefore(typeof(EnemyDespawnSystem))]
public partial struct PlayerOrbitalProjectionEnemyContactSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers projection and enemy components required by contact damage resolution.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerOrbitalProjectionInstance>();
        state.RequireForUpdate<EnemyHealth>();
    }

    /// <summary>
    /// Resolves projection/enemy overlaps and applies tick-gated contact damage.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = math.max(0f, SystemAPI.Time.DeltaTime);
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        foreach ((RefRW<PlayerOrbitalProjectionInstance> projection,
                  RefRO<LocalTransform> projectionTransform,
                  DynamicBuffer<PlayerOrbitalProjectionEnemyContactElement> contactBuffer)
                 in SystemAPI.Query<RefRW<PlayerOrbitalProjectionInstance>,
                                    RefRO<LocalTransform>,
                                    DynamicBuffer<PlayerOrbitalProjectionEnemyContactElement>>())
        {
            PlayerOrbitalProjectionInstance instance = projection.ValueRO;

            if (!CanDamageEnemies(in instance))
                continue;

            TickContactCooldowns(contactBuffer, deltaTime);

            foreach ((RefRO<EnemyData> enemyData,
                      RefRW<EnemyHealth> enemyHealth,
                      RefRW<EnemyRuntimeState> enemyRuntimeState,
                      RefRO<LocalTransform> enemyTransform,
                      Entity enemyEntity)
                     in SystemAPI.Query<RefRO<EnemyData>,
                                        RefRW<EnemyHealth>,
                                        RefRW<EnemyRuntimeState>,
                                        RefRO<LocalTransform>>()
                                 .WithAll<EnemyActive>()
                                 .WithNone<EnemyDespawnRequest, EnemySpawnInactivityLock>()
                                 .WithEntityAccess())
            {
                if (!IsOverlapping(projectionTransform.ValueRO.Position,
                                   instance.Config.CollisionRadius,
                                   enemyTransform.ValueRO.Position,
                                   enemyData.ValueRO.BodyRadius))
                {
                    continue;
                }

                if (!CanApplyDamageTick(contactBuffer, enemyEntity, instance.Config.DamageTickIntervalSeconds))
                    continue;

                EnemyHealth mutableEnemyHealth = enemyHealth.ValueRO;
                EnemyRuntimeState mutableEnemyRuntimeState = enemyRuntimeState.ValueRO;

                ApplyEnemyDamage(entityManager,
                                 ref commandBuffer,
                                 enemyEntity,
                                 ref mutableEnemyHealth,
                                 ref mutableEnemyRuntimeState,
                                 instance.Config.ContactDamage);

                enemyHealth.ValueRW = mutableEnemyHealth;
                enemyRuntimeState.ValueRW = mutableEnemyRuntimeState;

                ApplyProjectionHealthCost(ref instance,
                                          projectionTransform.ValueRO.Position,
                                          instance.Config.EnemyContactHealthDamage);

                if (instance.Phase == PlayerOrbitalProjectionPhase.Despawning)
                    break;
            }

            projection.ValueRW = instance;
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Damage
    /// <summary>
    /// Checks whether one projection currently supports enemy contact damage.
    /// </summary>
    /// <param name="instance">Projection instance being inspected.</param>
    /// <returns>True when enemy damage should be evaluated.</returns>
    private static bool CanDamageEnemies(in PlayerOrbitalProjectionInstance instance)
    {
        return instance.Phase != PlayerOrbitalProjectionPhase.Despawning &&
               instance.Config.DamageEnemies != 0 &&
               instance.Config.ContactDamage > 0f &&
               instance.Config.CollisionRadius > 0f;
    }

    /// <summary>
    /// Applies one flat damage packet to an enemy and queues despawn when killed.
    /// </summary>
    /// <param name="entityManager">Entity manager used by visual damage feedback.</param>
    /// <param name="commandBuffer">Command buffer receiving optional despawn request.</param>
    /// <param name="enemyEntity">Enemy entity receiving damage.</param>
    /// <param name="enemyHealth">Mutable enemy health.</param>
    /// <param name="enemyRuntimeState">Mutable enemy runtime state used for combo timing.</param>
    /// <param name="damage">Flat damage amount applied to shield then health.</param>
    private static void ApplyEnemyDamage(EntityManager entityManager,
                                         ref EntityCommandBuffer commandBuffer,
                                         Entity enemyEntity,
                                         ref EnemyHealth enemyHealth,
                                         ref EnemyRuntimeState enemyRuntimeState,
                                         float damage)
    {
        if (enemyHealth.Current <= 0f)
            return;

        if (!EnemyDamageUtility.TryApplyFlatShieldDamage(ref enemyHealth, math.max(0f, damage)))
            return;

        EnemyExtraComboPointsRuntimeUtility.MarkEnemyDamaged(ref enemyRuntimeState);
        DamageFlashRuntimeUtility.Trigger(entityManager, enemyEntity);

        if (enemyHealth.Current > 0f)
            return;

        commandBuffer.AddComponent(enemyEntity, new EnemyDespawnRequest
        {
            Reason = EnemyDespawnReason.Killed
        });
    }

    /// <summary>
    /// Applies optional projection health loss from enemy contact.
    /// </summary>
    /// <param name="instance">Projection instance updated in place.</param>
    /// <param name="currentPosition">Current projection position used when despawn starts.</param>
    /// <param name="healthDamage">Health cost applied to the projection.</param>
    private static void ApplyProjectionHealthCost(ref PlayerOrbitalProjectionInstance instance,
                                                  float3 currentPosition,
                                                  float healthDamage)
    {
        if (instance.Config.HasHealth == 0 || healthDamage <= 0f)
            return;

        instance.CurrentHealth -= healthDamage;

        if (instance.CurrentHealth > 0f)
            return;

        instance.Phase = PlayerOrbitalProjectionPhase.Despawning;
        instance.PhaseElapsedSeconds = 0f;
        instance.DespawnStartPosition = currentPosition;
    }
    #endregion

    #region Contact Cooldowns
    /// <summary>
    /// Decrements all stored enemy contact cooldowns.
    /// </summary>
    /// <param name="contactBuffer">Projection contact buffer updated in place.</param>
    /// <param name="deltaTime">Current frame delta time.</param>
    private static void TickContactCooldowns(DynamicBuffer<PlayerOrbitalProjectionEnemyContactElement> contactBuffer, float deltaTime)
    {
        for (int contactIndex = contactBuffer.Length - 1; contactIndex >= 0; contactIndex--)
        {
            PlayerOrbitalProjectionEnemyContactElement contact = contactBuffer[contactIndex];
            contact.CooldownRemainingSeconds -= deltaTime;
            contactBuffer[contactIndex] = contact;
        }
    }

    /// <summary>
    /// Resolves whether contact damage can tick for one enemy and updates its cooldown.
    /// </summary>
    /// <param name="contactBuffer">Projection contact buffer updated in place.</param>
    /// <param name="enemyEntity">Enemy entity being checked.</param>
    /// <param name="tickIntervalSeconds">Cooldown assigned after a successful tick.</param>
    /// <returns>True when damage should be applied this frame.</returns>
    private static bool CanApplyDamageTick(DynamicBuffer<PlayerOrbitalProjectionEnemyContactElement> contactBuffer,
                                           Entity enemyEntity,
                                           float tickIntervalSeconds)
    {
        float cooldown = math.max(0.01f, tickIntervalSeconds);

        for (int contactIndex = 0; contactIndex < contactBuffer.Length; contactIndex++)
        {
            PlayerOrbitalProjectionEnemyContactElement contact = contactBuffer[contactIndex];

            if (contact.EnemyEntity != enemyEntity)
                continue;

            if (contact.CooldownRemainingSeconds > 0f)
                return false;

            contact.CooldownRemainingSeconds = cooldown;
            contactBuffer[contactIndex] = contact;
            return true;
        }

        contactBuffer.Add(new PlayerOrbitalProjectionEnemyContactElement
        {
            EnemyEntity = enemyEntity,
            CooldownRemainingSeconds = cooldown
        });
        return true;
    }
    #endregion

    #region Geometry
    /// <summary>
    /// Checks overlap between a projection circle and an enemy body circle in the XZ plane.
    /// </summary>
    /// <param name="projectionPosition">Projection world position.</param>
    /// <param name="projectionRadius">Projection collision radius.</param>
    /// <param name="enemyPosition">Enemy world position.</param>
    /// <param name="enemyRadius">Enemy body radius.</param>
    /// <returns>True when the circles overlap.</returns>
    private static bool IsOverlapping(float3 projectionPosition,
                                      float projectionRadius,
                                      float3 enemyPosition,
                                      float enemyRadius)
    {
        float3 delta = enemyPosition - projectionPosition;
        delta.y = 0f;
        float radius = math.max(0f, projectionRadius) + math.max(0f, enemyRadius);
        return math.lengthsq(delta) <= radius * radius;
    }
    #endregion

    #endregion
}
