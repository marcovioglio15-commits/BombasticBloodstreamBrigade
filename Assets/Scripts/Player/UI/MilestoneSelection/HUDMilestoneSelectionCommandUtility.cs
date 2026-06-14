using Unity.Entities;

/// <summary>
/// Validates and stores milestone selection commands requested by the HUD.
/// </summary>
public static class HUDMilestoneSelectionCommandUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Stores one milestone command after validating the current runtime state and optional offer index.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read and write milestone selection state.</param>
    /// <param name="playerEntity">Player entity that owns milestone selection state.</param>
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

        selectionState.HasPendingCommand = 1;
        selectionState.PendingCommandType = commandType;
        selectionState.PendingOfferIndex = offerIndex;
        entityManager.SetComponentData(playerEntity, selectionState);
        return true;
    }
    #endregion

    #endregion
}
