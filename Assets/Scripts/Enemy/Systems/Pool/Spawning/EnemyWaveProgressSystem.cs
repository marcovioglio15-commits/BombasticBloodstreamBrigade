using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Updates wave runtime counters from enemy despawn requests before pooled release happens.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyDespawnSystem))]
[UpdateAfter(typeof(EnemyKilledEventsSystem))]
[UpdateBefore(typeof(EnemyFinalizeDespawnSystem))]
public partial struct EnemyWaveProgressSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the minimum runtime dependencies required by the system.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyDespawnRequest>();
        state.RequireForUpdate<EnemyOwnerSpawner>();
        state.RequireForUpdate<EnemyWaveOwner>();
    }

    /// <summary>
    /// Decrements alive counters and records first-kill or completion timestamps for authored waves.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

        foreach ((RefRO<EnemyDespawnRequest> despawnRequest,
                  RefRO<EnemyOwnerSpawner> ownerSpawner,
                  RefRO<EnemyWaveOwner> waveOwner)
                 in SystemAPI.Query<RefRO<EnemyDespawnRequest>, RefRO<EnemyOwnerSpawner>, RefRO<EnemyWaveOwner>>())
        {
            Entity spawnerEntity = ownerSpawner.ValueRO.SpawnerEntity;
            int waveIndex = waveOwner.ValueRO.WaveIndex;

            if (spawnerEntity == Entity.Null)
                continue;

            if (!entityManager.Exists(spawnerEntity))
                continue;

            if (waveIndex < 0)
                continue;

            if (!entityManager.HasBuffer<EnemySpawnerWaveRuntimeElement>(spawnerEntity))
                continue;

            DynamicBuffer<EnemySpawnerWaveRuntimeElement> waveRuntimeBuffer = entityManager.GetBuffer<EnemySpawnerWaveRuntimeElement>(spawnerEntity);

            if (waveIndex >= waveRuntimeBuffer.Length)
                continue;

            EnemySpawnerWaveRuntimeElement waveRuntime = waveRuntimeBuffer[waveIndex];

            if (waveRuntime.AliveCount > 0)
                waveRuntime.AliveCount--;

            if (despawnRequest.ValueRO.Reason == EnemyDespawnReason.Killed && waveRuntime.FirstKillRegistered == 0)
            {
                waveRuntime.FirstKillRegistered = 1;
                waveRuntime.FirstKillTime = elapsedTime;
            }

            if (waveRuntime.SpawnFinished != 0 && waveRuntime.Completed == 0 && waveRuntime.AliveCount <= 0)
            {
                waveRuntime.Completed = 1;
                waveRuntime.CompletionTime = math.max(elapsedTime, waveRuntime.SpawnEndTime);
            }

            waveRuntimeBuffer[waveIndex] = waveRuntime;

            if (!entityManager.HasComponent<EnemySpawnerState>(spawnerEntity))
                continue;

            EnemySpawnerState spawnerState = entityManager.GetComponentData<EnemySpawnerState>(spawnerEntity);

            if (spawnerState.AliveCount > 0)
                spawnerState.AliveCount--;

            entityManager.SetComponentData(spawnerEntity, spawnerState);
        }
    }
    #endregion

    #endregion
}
