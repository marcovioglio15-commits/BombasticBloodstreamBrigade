using Unity.Entities;

/// <summary>
/// Validates and queues milestone selection commands requested by the HUD.
/// </summary>
public static class HUDMilestoneSelectionCommandUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Queues one milestone command after validating the current runtime state and optional offer index.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read and write milestone selection buffers.</param>
    /// <param name="playerEntity">Player entity that owns milestone selection state and command buffers.</param>
    /// <param name="commandType">Command kind requested by the HUD.</param>
    /// <param name="offerIndex">Offer index used by selection commands, or -1 for skip.</param>
    /// <returns>True when the command is queued; otherwise false.</returns>
    public static bool TryQueueCommand(EntityManager entityManager,
                                       Entity playerEntity,
                                       PlayerMilestoneSelectionCommandType commandType,
                                       int offerIndex)
    {
        if (playerEntity == Entity.Null)
            return false;

        if (!entityManager.HasComponent<PlayerMilestonePowerUpSelectionState>(playerEntity))
            return false;

        PlayerMilestonePowerUpSelectionState selectionState = entityManager.GetComponentData<PlayerMilestonePowerUpSelectionState>(playerEntity);

        if (selectionState.IsSelectionActive == 0)
            return false;

        if (commandType == PlayerMilestoneSelectionCommandType.SelectOffer)
        {
            if (!entityManager.HasBuffer<PlayerMilestonePowerUpSelectionOfferElement>(playerEntity))
                return false;

            DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> offersBuffer = entityManager.GetBuffer<PlayerMilestonePowerUpSelectionOfferElement>(playerEntity);

            if (offerIndex < 0 || offerIndex >= offersBuffer.Length)
                return false;
        }

        if (!entityManager.HasBuffer<PlayerMilestonePowerUpSelectionCommand>(playerEntity))
            return false;

        DynamicBuffer<PlayerMilestonePowerUpSelectionCommand> selectionCommandsBuffer = entityManager.GetBuffer<PlayerMilestonePowerUpSelectionCommand>(playerEntity);
        selectionCommandsBuffer.Clear();
        selectionCommandsBuffer.Add(new PlayerMilestonePowerUpSelectionCommand
        {
            CommandType = commandType,
            OfferIndex = offerIndex
        });
        return true;
    }
    #endregion

    #endregion
}
