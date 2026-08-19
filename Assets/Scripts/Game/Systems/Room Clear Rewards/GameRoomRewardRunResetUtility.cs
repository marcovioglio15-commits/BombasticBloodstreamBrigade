using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Clears room-reward transaction and future-room state when the authoritative procedural run resets.
/// </summary>
public static class GameRoomRewardRunResetUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Clears grant checkpoints, temporary schedules, presentation entries and pending portal audio cues.
    /// </summary>
    /// <param name="entityManager">Entity manager owning persistent player entities.</param>
    public static void ResetPlayers(EntityManager entityManager)
    {
        EntityQuery playerQuery = entityManager.CreateEntityQuery(
            ComponentType.ReadWrite<PlayerRoomRewardGrantState>(),
            ComponentType.ReadWrite<PlayerRoomRewardTemporaryState>(),
            ComponentType.ReadWrite<PlayerRoomRewardTemporaryModifierElement>(),
            ComponentType.ReadWrite<PlayerRoomRewardTemporaryResourceElement>(),
            ComponentType.ReadWrite<PlayerRoomRewardPresentationEvent>());

        using NativeArray<Entity> players =
            playerQuery.ToEntityArray(Allocator.Temp);

        for (int playerIndex = 0; playerIndex < players.Length; playerIndex++)
        {
            Entity playerEntity = players[playerIndex];
            entityManager.SetComponentData(playerEntity, new PlayerRoomRewardGrantState
            {
                LastNodeIndex = -1
            });
            entityManager.SetComponentData(playerEntity,
                                           new PlayerRoomRewardTemporaryState());
            entityManager.GetBuffer<PlayerRoomRewardTemporaryModifierElement>(
                playerEntity).Clear();
            entityManager.GetBuffer<PlayerRoomRewardTemporaryResourceElement>(
                playerEntity).Clear();
            entityManager.GetBuffer<PlayerRoomRewardPresentationEvent>(
                playerEntity).Clear();

            if (!entityManager.HasComponent<PlayerRuntimeScalingState>(playerEntity))
                continue;

            PlayerRuntimeScalingState scalingState =
                entityManager.GetComponentData<PlayerRuntimeScalingState>(playerEntity);
            scalingState.Initialized = 0;
            scalingState.LastScalableStatsHash = 0u;
            entityManager.SetComponentData(playerEntity, scalingState);
        }

        playerQuery.Dispose();

        EntityQuery portalAudioQuery = entityManager.CreateEntityQuery(
            ComponentType.ReadWrite<GameRoomPortalAnimationAudioCue>());

        if (portalAudioQuery.CalculateEntityCount() == 1)
        {
            entityManager.GetBuffer<GameRoomPortalAnimationAudioCue>(
                portalAudioQuery.GetSingletonEntity()).Clear();
        }

        portalAudioQuery.Dispose();
    }
    #endregion

    #endregion
}
