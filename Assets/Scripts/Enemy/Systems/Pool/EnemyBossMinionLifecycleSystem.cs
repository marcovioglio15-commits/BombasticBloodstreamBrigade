using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Applies boss-owned minion lifecycle policy when the owning boss dies.
/// /params None.
/// /returns None.
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
    /// /params state Mutable system state.
    /// /returns None.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyBossTag>();
        state.RequireForUpdate<EnemyBossMinionOwner>();
        state.RequireForUpdate<EnemyDespawnRequest>();
    }

    /// <summary>
    /// Kills active minions whose owner boss has just received a killed despawn request.
    /// /params state Mutable system state.
    /// /returns None.
    /// </summary>
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
    /// /params state Mutable system state used by SystemAPI query generation.
    /// /params dyingBosses Target set receiving boss entities.
    /// /returns True when at least one killed boss was collected.
    /// </summary>
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
    /// /params state Mutable system state used by SystemAPI query generation.
    /// /params commandBuffer Command buffer used for structural changes after iteration.
    /// /params dyingBosses Set of bosses killed during the current pass.
    /// /returns None.
    /// </summary>
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
    /// /params dyingBosses Boss entities killed during this pass.
    /// /params bossEntity Boss entity to find.
    /// /returns True when the boss entity exists in the collected list.
    /// </summary>
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
