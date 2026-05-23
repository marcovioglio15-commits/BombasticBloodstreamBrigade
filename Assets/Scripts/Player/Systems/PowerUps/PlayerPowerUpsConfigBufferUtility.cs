using Unity.Entities;

/// <summary>
/// Reads and writes the single active power-up loadout snapshot stored in the player-owned external buffer.
/// </summary>
internal static class PlayerPowerUpsConfigBufferUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the current active power-up loadout from one snapshot buffer.
    /// </summary>
    /// <param name="powerUpsConfigBuffer">Buffer owning the external loadout snapshot.</param>
    /// <returns>The stored loadout, or the default loadout when the buffer has not been initialized yet.</returns>
    public static PlayerPowerUpsConfig Read(DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer)
    {
        if (powerUpsConfigBuffer.Length <= 0)
            return default;

        return powerUpsConfigBuffer[0].Value;
    }

    /// <summary>
    /// Resolves the current active power-up loadout from a player entity when its snapshot buffer exists.
    /// </summary>
    /// <param name="playerEntity">Player entity owning the loadout snapshot buffer.</param>
    /// <param name="powerUpsConfigLookup">Lookup used to read player loadout snapshot buffers.</param>
    /// <returns>The stored loadout, or the default loadout when the player has no initialized buffer.</returns>
    public static PlayerPowerUpsConfig Read(Entity playerEntity,
                                            in BufferLookup<PlayerPowerUpsConfigElement> powerUpsConfigLookup)
    {
        if (!powerUpsConfigLookup.HasBuffer(playerEntity))
            return default;

        return Read(powerUpsConfigLookup[playerEntity]);
    }

    /// <summary>
    /// Writes one active power-up loadout while preserving the single-slot buffer contract.
    /// </summary>
    /// <param name="powerUpsConfigBuffer">Buffer receiving the loadout snapshot.</param>
    /// <param name="powerUpsConfig">Mutable runtime loadout produced by power-up systems.</param>
    public static void Write(DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer,
                             in PlayerPowerUpsConfig powerUpsConfig)
    {
        PlayerPowerUpsConfigElement configElement = new PlayerPowerUpsConfigElement
        {
            Value = powerUpsConfig
        };

        if (powerUpsConfigBuffer.Length <= 0)
        {
            powerUpsConfigBuffer.Add(configElement);
            return;
        }

        powerUpsConfigBuffer[0] = configElement;
    }

    /// <summary>
    /// Writes one loadout snapshot when a player entity owns the external config buffer.
    /// </summary>
    /// <param name="playerEntity">Player entity receiving the loadout snapshot.</param>
    /// <param name="powerUpsConfigLookup">Lookup used to access player loadout snapshot buffers.</param>
    /// <param name="powerUpsConfig">Mutable runtime loadout produced by power-up systems.</param>
    /// <returns>True when the entity owns the config buffer and the loadout was written.</returns>
    public static bool TryWrite(Entity playerEntity,
                                in BufferLookup<PlayerPowerUpsConfigElement> powerUpsConfigLookup,
                                in PlayerPowerUpsConfig powerUpsConfig)
    {
        if (!powerUpsConfigLookup.HasBuffer(playerEntity))
            return false;

        Write(powerUpsConfigLookup[playerEntity], in powerUpsConfig);
        return true;
    }
    #endregion

    #endregion
}
