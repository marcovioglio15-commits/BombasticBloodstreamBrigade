using Unity.Entities;

/// <summary>
/// Reads and writes the single passive-tools snapshot stored in the player-owned external buffer.
/// </summary>
internal static class PlayerPassiveToolsStateBufferUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the current passive-tools snapshot from one player buffer.
    /// </summary>
    /// <param name="passiveToolsStateBuffer">Buffer owning the external passive-tools snapshot.</param>
    /// <returns>The stored snapshot, or the default aggregate when the buffer is not initialized yet.</returns>
    public static PlayerPassiveToolsState Read(DynamicBuffer<PlayerPassiveToolsStateElement> passiveToolsStateBuffer)
    {
        if (passiveToolsStateBuffer.Length <= 0)
            return PlayerPassiveToolsAggregationUtility.CreateDefaultState();

        return passiveToolsStateBuffer[0].Value;
    }

    /// <summary>
    /// Resolves the current passive-tools snapshot from a player entity when its state buffer exists.
    /// </summary>
    /// <param name="playerEntity">Player entity owning the passive-tools snapshot buffer.</param>
    /// <param name="passiveToolsStateLookup">Lookup used to read player snapshot buffers.</param>
    /// <returns>The stored snapshot, or the default aggregate when the player has no initialized buffer.</returns>
    public static PlayerPassiveToolsState Read(Entity playerEntity,
                                               in BufferLookup<PlayerPassiveToolsStateElement> passiveToolsStateLookup)
    {
        if (!passiveToolsStateLookup.HasBuffer(playerEntity))
            return PlayerPassiveToolsAggregationUtility.CreateDefaultState();

        return Read(passiveToolsStateLookup[playerEntity]);
    }

    /// <summary>
    /// Writes one aggregate snapshot while preserving the single-slot passive-tools buffer contract.
    /// </summary>
    /// <param name="passiveToolsStateBuffer">Buffer receiving the aggregate snapshot.</param>
    /// <param name="passiveToolsState">Aggregate snapshot produced by passive runtime systems.</param>
    public static void Write(DynamicBuffer<PlayerPassiveToolsStateElement> passiveToolsStateBuffer,
                             in PlayerPassiveToolsState passiveToolsState)
    {
        PlayerPassiveToolsStateElement stateElement = new PlayerPassiveToolsStateElement
        {
            Value = passiveToolsState
        };

        if (passiveToolsStateBuffer.Length <= 0)
        {
            passiveToolsStateBuffer.Add(stateElement);
            return;
        }

        passiveToolsStateBuffer[0] = stateElement;
    }

    /// <summary>
    /// Writes one aggregate snapshot when a player entity owns a passive-tools state buffer.
    /// </summary>
    /// <param name="playerEntity">Player entity receiving the aggregate snapshot.</param>
    /// <param name="passiveToolsStateLookup">Lookup used to access player snapshot buffers.</param>
    /// <param name="passiveToolsState">Aggregate snapshot produced by passive runtime systems.</param>
    /// <returns>True when the entity owns a passive-tools state buffer and the snapshot was written.</returns>
    public static bool TryWrite(Entity playerEntity,
                                in BufferLookup<PlayerPassiveToolsStateElement> passiveToolsStateLookup,
                                in PlayerPassiveToolsState passiveToolsState)
    {
        if (!passiveToolsStateLookup.HasBuffer(playerEntity))
            return false;

        Write(passiveToolsStateLookup[playerEntity], in passiveToolsState);
        return true;
    }
    #endregion

    #endregion
}
