using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Adds elemental runtime components and buffers to enemy entities when missing.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup), OrderFirst = true)]
public partial struct EnemyElementalInitializeSystem : ISystem
{
    #region Fields
    private EntityQuery missingRuntimeStateQuery;
    private EntityQuery missingStacksBufferQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the missing-component queries used as a defensive fallback for authored enemy prefabs.
    /// </summary>
    /// <param name="state">System state used to build entity queries and declare update requirements.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyData>();

        missingRuntimeStateQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyData>()
            .WithNone<EnemyElementalRuntimeState>()
            .Build();

        missingStacksBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyData>()
            .WithNone<EnemyElementStackElement>()
            .Build();
    }

    /// <summary>
    /// Adds missing elemental runtime state to enemies that were not prepared by pooling validation.
    /// </summary>
    /// <param name="state">System state used to create and play back structural changes.</param>
    public void OnUpdate(ref SystemState state)
    {
        bool hasMissingRuntimeState = !missingRuntimeStateQuery.IsEmptyIgnoreFilter;
        bool hasMissingStacksBuffer = !missingStacksBufferQuery.IsEmptyIgnoreFilter;

        if (!hasMissingRuntimeState && !hasMissingStacksBuffer)
            return;

        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        if (hasMissingRuntimeState)
            AddMissingRuntimeStates(ref commandBuffer);

        if (hasMissingStacksBuffer)
            AddMissingStackBuffers(ref commandBuffer);

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Adds default elemental runtime state to all enemies currently missing it.
    /// </summary>
    /// <param name="commandBuffer">Command buffer receiving structural changes for playback after query iteration.</param>
    private void AddMissingRuntimeStates(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingRuntimeStateQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            commandBuffer.AddComponent(entities[index], new EnemyElementalRuntimeState
            {
                SlowPercent = 0f
            });
        }

        entities.Dispose();
    }

    /// <summary>
    /// Adds the elemental stack buffer to all enemies currently missing it.
    /// </summary>
    /// <param name="commandBuffer">Command buffer receiving structural changes for playback after query iteration.</param>
    private void AddMissingStackBuffers(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingStacksBufferQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
            commandBuffer.AddBuffer<EnemyElementStackElement>(entities[index]);

        entities.Dispose();
    }
    #endregion

    #endregion
}
