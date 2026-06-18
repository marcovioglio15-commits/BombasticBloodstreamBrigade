using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Ensures player entities expose elemental runtime state and stack buffers.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerProgressionInitializeSystem))]
public partial struct PlayerElementalInitializeSystem : ISystem
{
    #region Fields
    private EntityQuery missingElementalRuntimeStateQuery;
    private EntityQuery missingElementalStackBufferQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerControllerConfig>();

        missingElementalRuntimeStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig>()
            .WithNone<PlayerElementalRuntimeState>()
            .Build();

        missingElementalStackBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig>()
            .WithNone<PlayerElementStackElement>()
            .Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        bool hasMissingRuntimeState = !missingElementalRuntimeStateQuery.IsEmptyIgnoreFilter;
        bool hasMissingStackBuffer = !missingElementalStackBufferQuery.IsEmptyIgnoreFilter;

        if (!hasMissingRuntimeState && !hasMissingStackBuffer)
            return;

        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        if (hasMissingRuntimeState)
            AddMissingRuntimeStates(ref commandBuffer);

        if (hasMissingStackBuffer)
            AddMissingStackBuffers(ref commandBuffer);

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Helpers
    private void AddMissingRuntimeStates(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingElementalRuntimeStateQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            commandBuffer.AddComponent(entities[index], new PlayerElementalRuntimeState
            {
                SlowPercent = 0f
            });
        }

        entities.Dispose();
    }

    private void AddMissingStackBuffers(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingElementalStackBufferQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
            commandBuffer.AddBuffer<PlayerElementStackElement>(entities[index]);

        entities.Dispose();
    }
    #endregion

    #endregion
}
