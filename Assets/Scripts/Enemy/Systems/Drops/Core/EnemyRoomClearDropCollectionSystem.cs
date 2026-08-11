using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Marks all active drops for persistent attraction once per authoritative procedural room-clear event.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(GameProceduralRoomCompletionSystem))]
[UpdateBefore(typeof(GameRoomRewardGrantSystem))]
public partial struct EnemyRoomClearDropCollectionSystem : ISystem
{
    #region Fields
    private EntityQuery managerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Caches the procedural manager query and requires the shared collection queue.
    /// </summary>
    /// <param name="state">Current ECS system state used to build runtime queries.</param>
    public void OnCreate(ref SystemState state)
    {
        managerQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<GameProceduralRoomClearedEvent>()
            .Build(ref state);
        state.RequireForUpdate<EnemyDropCollectionRequestQueue>();
        state.RequireForUpdate(managerQuery);
    }

    /// <summary>
    /// Starts distance-independent attraction for every drop belonging to the latest room-clear transaction.
    /// </summary>
    /// <param name="state">Current ECS system state providing manager events and request-queue access.</param>
    public void OnUpdate(ref SystemState state)
    {
        if (managerQuery.CalculateEntityCount() != 1)
            return;

        Entity requestQueueEntity = SystemAPI.GetSingletonEntity<EnemyDropCollectionRequestQueue>();
        DynamicBuffer<GameProceduralRoomClearedEvent> clearedEvents =
            state.EntityManager.GetBuffer<GameProceduralRoomClearedEvent>(managerQuery.GetSingletonEntity(), true);

        if (clearedEvents.Length <= 0)
            return;

        GameProceduralRoomClearedEvent clearedEvent = clearedEvents[clearedEvents.Length - 1];
        EnemyDropCollectionRequestQueue requestQueue =
            state.EntityManager.GetComponentData<EnemyDropCollectionRequestQueue>(requestQueueEntity);

        if (requestQueue.HasQueuedRoomClear != 0 &&
            requestQueue.LastQueuedRunSeed == clearedEvent.RunSeed &&
            requestQueue.LastQueuedGenerationVersion == clearedEvent.GenerationVersion &&
            requestQueue.LastQueuedRoomClearVersion == clearedEvent.ClearVersion)
            return;

        EnemyDropRoomClearAttractionUtility.MarkActiveDrops(state.EntityManager);
        requestQueue.LastQueuedRunSeed = clearedEvent.RunSeed;
        requestQueue.LastQueuedGenerationVersion = clearedEvent.GenerationVersion;
        requestQueue.LastQueuedRoomClearVersion = clearedEvent.ClearVersion;
        requestQueue.HasQueuedRoomClear = 1;
        state.EntityManager.SetComponentData(requestQueueEntity, requestQueue);
    }
    #endregion

    #endregion
}
