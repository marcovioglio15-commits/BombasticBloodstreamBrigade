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
    /// <param name="passiveToolsState">Stored snapshot, or the default aggregate when the buffer is not initialized yet.</param>
    public static void Read(DynamicBuffer<PlayerPassiveToolsStateElement> passiveToolsStateBuffer,
                            out PlayerPassiveToolsState passiveToolsState)
    {
        passiveToolsState = default;
        PlayerPassiveToolsAggregationUtility.ResetToDefault(ref passiveToolsState);

        if (passiveToolsStateBuffer.Length <= 0)
            return;

        passiveToolsState = passiveToolsStateBuffer[0].Value;
    }

    /// <summary>
    /// Resolves the mutable aggregate snapshot stored in the single-slot passive-tools buffer.
    /// </summary>
    /// <param name="passiveToolsStateBuffer">Buffer owning the external passive-tools snapshot.</param>
    /// <returns>Mutable aggregate state reference initialized to the neutral default when missing.</returns>
    public static ref PlayerPassiveToolsState GetStateRef(DynamicBuffer<PlayerPassiveToolsStateElement> passiveToolsStateBuffer)
    {
        EnsureInitialized(passiveToolsStateBuffer);
        return ref passiveToolsStateBuffer.ElementAt(0).Value;
    }

    /// <summary>
    /// Resolves the current passive-tools snapshot from a player entity when its state buffer exists.
    /// </summary>
    /// <param name="playerEntity">Player entity owning the passive-tools snapshot buffer.</param>
    /// <param name="passiveToolsStateLookup">Lookup used to read player snapshot buffers.</param>
    /// <param name="passiveToolsState">Stored snapshot, or the default aggregate when the player has no initialized buffer.</param>
    public static void Read(Entity playerEntity,
                            in BufferLookup<PlayerPassiveToolsStateElement> passiveToolsStateLookup,
                            out PlayerPassiveToolsState passiveToolsState)
    {
        passiveToolsState = default;
        PlayerPassiveToolsAggregationUtility.ResetToDefault(ref passiveToolsState);

        if (!passiveToolsStateLookup.HasBuffer(playerEntity))
            return;

        Read(passiveToolsStateLookup[playerEntity], out passiveToolsState);
    }

    /// <summary>
    /// Writes one aggregate snapshot while preserving the single-slot passive-tools buffer contract.
    /// </summary>
    /// <param name="passiveToolsStateBuffer">Buffer receiving the aggregate snapshot.</param>
    /// <param name="passiveToolsState">Aggregate snapshot produced by passive runtime systems.</param>
    public static void Write(DynamicBuffer<PlayerPassiveToolsStateElement> passiveToolsStateBuffer,
                             in PlayerPassiveToolsState passiveToolsState)
    {
        if (passiveToolsStateBuffer.Length <= 0)
            passiveToolsStateBuffer.ResizeUninitialized(1);

        ref PlayerPassiveToolsStateElement stateElement = ref passiveToolsStateBuffer.ElementAt(0);
        stateElement.Value = passiveToolsState;
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

    #region Private Methods
    /// <summary>
    /// Ensures the external passive-tools snapshot buffer contains its single aggregate slot.
    /// </summary>
    /// <param name="passiveToolsStateBuffer">Buffer that must hold exactly one aggregate snapshot slot.</param>
    private static void EnsureInitialized(DynamicBuffer<PlayerPassiveToolsStateElement> passiveToolsStateBuffer)
    {
        if (passiveToolsStateBuffer.Length > 0)
            return;

        passiveToolsStateBuffer.ResizeUninitialized(1);
        ref PlayerPassiveToolsStateElement stateElement = ref passiveToolsStateBuffer.ElementAt(0);
        PlayerPassiveToolsAggregationUtility.ResetToDefault(ref stateElement.Value);
    }
    #endregion

    #endregion
}
