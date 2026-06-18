using Unity.Entities;

/// <summary>
/// Tracks killed enemies by accumulating pending killed-despawn requests.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyDespawnSystem))]
[UpdateAfter(typeof(EnemyKilledEventsSystem))]
[UpdateBefore(typeof(EnemyFinalizeDespawnSystem))]
public partial struct EnemyKillCounterSystem : ISystem
{
    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        EntityQuery singletonQuery = state.GetEntityQuery(ComponentType.ReadOnly<GlobalEnemyKillCounter>());

        if (singletonQuery.IsEmptyIgnoreFilter)
        {
            Entity singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(singletonEntity, new GlobalEnemyKillCounter
            {
                TotalKilled = 0u
            });
        }
    }

    public void OnUpdate(ref SystemState state)
    {
        uint killedThisFrame = 0u;

        foreach (RefRO<EnemyDespawnRequest> despawnRequest in SystemAPI.Query<RefRO<EnemyDespawnRequest>>())
        {
            if (despawnRequest.ValueRO.Reason != EnemyDespawnReason.Killed)
                continue;

            killedThisFrame++;
        }

        if (killedThisFrame == 0u)
            return;

        RefRW<GlobalEnemyKillCounter> killCounter = SystemAPI.GetSingletonRW<GlobalEnemyKillCounter>();
        killCounter.ValueRW.TotalKilled += killedThisFrame;
    }
    #endregion

    #endregion
}
