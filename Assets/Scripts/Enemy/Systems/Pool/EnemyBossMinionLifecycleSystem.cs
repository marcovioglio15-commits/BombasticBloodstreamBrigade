using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Applies boss-owned minion lifecycle policy when the owning boss dies.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyDespawnSystem))]
[UpdateAfter(typeof(PlayerPassiveExplosionResolveSystem))]
[UpdateAfter(typeof(PlayerLaserBeamDamageSystem))]
[UpdateAfter(typeof(PlayerElementalTrailResolveSystem))]
[UpdateAfter(typeof(EnemyElementalEffectsSystem))]
[UpdateBefore(typeof(EnemyKilledEventsSystem))]
public partial struct EnemyBossMinionLifecycleSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime dependencies required to react to boss death requests.
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyBossTag>();
        state.RequireForUpdate<EnemyBossMinionOwner>();
        state.RequireForUpdate<EnemyDespawnRequest>();
    }

    /// <summary>
    /// Kills active minions whose owner boss has just received a killed despawn request.
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        NativeList<Entity> dyingBosses = new NativeList<Entity>(state.WorldUpdateAllocator);
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        try
        {
            bool hasDyingBosses = CollectDyingBosses(ref state, ref dyingBosses);

            if (!hasDyingBosses)
                return;

            QueueOwnedMinionDeaths(ref state, ref commandBuffer, in dyingBosses);
            commandBuffer.Playback(state.EntityManager);
        }
        finally
        {
            commandBuffer.Dispose();
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Collects boss entities that have been killed during the current enemy pipeline pass.
    /// </summary>
    /// <param name="state">Mutable system state used by SystemAPI query generation.</param>
    /// <param name="dyingBosses">Target set receiving boss entities.</param>
    /// <returns>True when at least one killed boss was collected.</returns>
    private bool CollectDyingBosses(ref SystemState state, ref NativeList<Entity> dyingBosses)
    {
        bool hasDyingBosses = false;

        foreach ((RefRO<EnemyDespawnRequest> despawnRequest, Entity bossEntity)
                 in SystemAPI.Query<RefRO<EnemyDespawnRequest>>()
                             .WithAll<EnemyBossTag>()
                             .WithEntityAccess())
        {
            if (despawnRequest.ValueRO.Reason != EnemyDespawnReason.Killed)
                continue;

            dyingBosses.Add(bossEntity);
            hasDyingBosses = true;
        }

        return hasDyingBosses;
    }

    /// <summary>
    /// Queues killed despawn requests for active minions configured to die with their boss.
    /// </summary>
    /// <param name="state">Mutable system state used by SystemAPI query generation.</param>
    /// <param name="commandBuffer">Command buffer used for structural changes after iteration.</param>
    /// <param name="dyingBosses">Set of bosses killed during the current pass.</param>
    private void QueueOwnedMinionDeaths(ref SystemState state, ref EntityCommandBuffer commandBuffer, in NativeList<Entity> dyingBosses)
    {
        foreach ((RefRO<EnemyBossMinionOwner> owner, Entity minionEntity)
                 in SystemAPI.Query<RefRO<EnemyBossMinionOwner>>()
                             .WithAll<EnemyActive>()
                             .WithNone<EnemyDespawnRequest>()
                             .WithEntityAccess())
        {
            if (owner.ValueRO.KillOnBossDeath == 0)
                continue;

            if (!ContainsBoss(in dyingBosses, owner.ValueRO.BossEntity))
                continue;

            commandBuffer.AddComponent(minionEntity, new EnemyDespawnRequest
            {
                Reason = EnemyDespawnReason.Killed
            });
        }
    }

    /// <summary>
    /// Resolves whether the current pass collected the requested boss entity.
    /// </summary>
    /// <param name="dyingBosses">Boss entities killed during this pass.</param>
    /// <param name="bossEntity">Boss entity to find.</param>
    /// <returns>True when the boss entity exists in the collected list.</returns>
    private static bool ContainsBoss(in NativeList<Entity> dyingBosses, Entity bossEntity)
    {
        for (int index = 0; index < dyingBosses.Length; index++)
        {
            if (dyingBosses[index] == bossEntity)
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
