using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Dispatches the player spawn audio after scene transition loading has finished.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerProgressionInitializeSystem))]
public partial struct PlayerSpawnAudioDispatchSystem : ISystem
{
    #region Fields
    private EntityQuery pendingSpawnAudioQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the pending spawn-audio query and prevents per-frame work when no initialized player is waiting.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        pendingSpawnAudioQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerSpawnAudioPending>()
            .Build();
        state.RequireForUpdate(pendingSpawnAudioQuery);
    }

    /// <summary>
    /// Plays the deferred player spawn audio once the scene transition has reached FadeIn or returned to Idle.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.TryGetSingleton<GameSceneTransitionState>(out GameSceneTransitionState transitionState) &&
            !IsSceneTransitionReadyForSpawnAudio(in transitionState))
            return;

        if (!SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out DynamicBuffer<GameAudioEventRequest> audioRequests))
            return;

        NativeArray<Entity> pendingEntities = pendingSpawnAudioQuery.ToEntityArray(Allocator.Temp);

        if (pendingEntities.Length <= 0)
        {
            pendingEntities.Dispose();
            return;
        }

        GameAudioEventRequestUtility.EnqueueGlobal(audioRequests, GameAudioEventId.PlayerSpawn);

        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        for (int entityIndex = 0; entityIndex < pendingEntities.Length; entityIndex++)
            commandBuffer.RemoveComponent<PlayerSpawnAudioPending>(pendingEntities[entityIndex]);

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
        pendingEntities.Dispose();
    }
    #endregion

    #region Transition Readiness
    /// <summary>
    /// Checks whether the scene transition is past loading/readiness and can safely play spawn feedback.
    /// </summary>
    /// <returns>True when no transition is active, or the active transition is revealing the loaded scene.</returns>
    /// <param name="transitionState">Current scene transition state.</param>
    /// <returns>True when the transition is idle or revealing the loaded scene.</returns>
    private static bool IsSceneTransitionReadyForSpawnAudio(in GameSceneTransitionState transitionState)
    {
        if (transitionState.IsTransitioning == 0)
            return true;

        return transitionState.Phase == GameSceneTransitionPhase.FadeIn;
    }
    #endregion

    #endregion
}
