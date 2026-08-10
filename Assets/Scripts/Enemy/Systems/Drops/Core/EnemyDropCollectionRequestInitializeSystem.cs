using Unity.Entities;

/// <summary>
/// Creates the unique runtime drop-collection request queue before gameplay systems begin updating.
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
public partial struct EnemyDropCollectionRequestInitializeSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the request singleton when the current ECS world does not already own one.
    /// </summary>
    /// <param name="state">Current ECS system state used for singleton creation.</param>
    public void OnCreate(ref SystemState state)
    {
        EntityQuery requestQueueQuery = state.GetEntityQuery(ComponentType.ReadOnly<EnemyDropCollectionRequestQueue>());
        int requestQueueCount = requestQueueQuery.CalculateEntityCount();

        if (requestQueueCount <= 0)
        {
            Entity requestQueueEntity = state.EntityManager.CreateEntity(typeof(EnemyDropCollectionRequestQueue));
            state.EntityManager.AddBuffer<EnemyDropCollectionRequest>(requestQueueEntity);
        }
        else if (requestQueueCount == 1)
        {
            Entity requestQueueEntity = requestQueueQuery.GetSingletonEntity();

            if (!state.EntityManager.HasBuffer<EnemyDropCollectionRequest>(requestQueueEntity))
                state.EntityManager.AddBuffer<EnemyDropCollectionRequest>(requestQueueEntity);
        }

        state.Enabled = false;
    }

    /// <summary>
    /// Remains disabled because queue creation is completed synchronously during system creation.
    /// </summary>
    /// <param name="state">Current ECS system state, unused after initialization.</param>
    public void OnUpdate(ref SystemState state)
    {
    }
    #endregion

    #endregion
}
