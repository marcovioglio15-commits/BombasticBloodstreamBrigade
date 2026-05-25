using Unity.Entities;

/// <summary>
/// Reads and writes the active two-slot power-up loadout snapshot stored in the player-owned external buffer.
/// </summary>
internal static class PlayerPowerUpsConfigBufferUtility
{
    #region Constants
    private const byte PrimarySlotIndex = 0;
    private const byte SecondarySlotIndex = 1;
    private const int SlotCount = 2;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the current active power-up loadout from one snapshot buffer.
    /// </summary>
    /// <param name="powerUpsConfigBuffer">Buffer owning the external loadout snapshot.</param>
    /// <param name="powerUpsConfig">Stored loadout, or the default loadout when the buffer has not been initialized yet.</param>
    public static void Read(DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer,
                            out PlayerPowerUpsConfig powerUpsConfig)
    {
        PlayerPowerUpSlotConfig primarySlot;
        PlayerPowerUpSlotConfig secondarySlot;
        ReadSlots(powerUpsConfigBuffer,
                  out primarySlot,
                  out secondarySlot);
        powerUpsConfig = new PlayerPowerUpsConfig
        {
            PrimarySlot = primarySlot,
            SecondarySlot = secondarySlot
        };
    }

    /// <summary>
    /// Resolves the current active power-up loadout from a player entity when its snapshot buffer exists.
    /// </summary>
    /// <param name="playerEntity">Player entity owning the loadout snapshot buffer.</param>
    /// <param name="powerUpsConfigLookup">Lookup used to read player loadout snapshot buffers.</param>
    /// <param name="powerUpsConfig">Stored loadout, or the default loadout when the player has no initialized buffer.</param>
    public static void Read(Entity playerEntity,
                            in BufferLookup<PlayerPowerUpsConfigElement> powerUpsConfigLookup,
                            out PlayerPowerUpsConfig powerUpsConfig)
    {
        powerUpsConfig = default;

        if (!powerUpsConfigLookup.HasBuffer(playerEntity))
            return;

        Read(powerUpsConfigLookup[playerEntity], out powerUpsConfig);
    }

    /// <summary>
    /// Resolves the current active power-up slots directly from one snapshot buffer.
    /// </summary>
    /// <param name="powerUpsConfigBuffer">Buffer owning the external loadout snapshot.</param>
    /// <param name="primarySlot">Stored primary slot, or default when the buffer has no primary entry.</param>
    /// <param name="secondarySlot">Stored secondary slot, or default when the buffer has no secondary entry.</param>
    public static void ReadSlots(DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer,
                                 out PlayerPowerUpSlotConfig primarySlot,
                                 out PlayerPowerUpSlotConfig secondarySlot)
    {
        primarySlot = default;
        secondarySlot = default;

        if (powerUpsConfigBuffer.Length <= 0)
            return;

        // Use the readonly-safe indexer here: ElementAt requires write access even when the caller only reads.
        for (int elementIndex = 0; elementIndex < powerUpsConfigBuffer.Length; elementIndex++)
        {
            PlayerPowerUpsConfigElement configElement = powerUpsConfigBuffer[elementIndex];

            switch (configElement.SlotIndex)
            {
                case PrimarySlotIndex:
                    primarySlot = configElement.Slot;
                    break;
                case SecondarySlotIndex:
                    secondarySlot = configElement.Slot;
                    break;
            }
        }
    }

    /// <summary>
    /// Resolves the current active power-up slots from a player entity when its snapshot buffer exists.
    /// </summary>
    /// <param name="playerEntity">Player entity owning the loadout snapshot buffer.</param>
    /// <param name="powerUpsConfigLookup">Lookup used to read player loadout snapshot buffers.</param>
    /// <param name="primarySlot">Stored primary slot, or default when the player has no initialized buffer.</param>
    /// <param name="secondarySlot">Stored secondary slot, or default when the player has no initialized buffer.</param>
    public static void ReadSlots(Entity playerEntity,
                                 in BufferLookup<PlayerPowerUpsConfigElement> powerUpsConfigLookup,
                                 out PlayerPowerUpSlotConfig primarySlot,
                                 out PlayerPowerUpSlotConfig secondarySlot)
    {
        primarySlot = default;
        secondarySlot = default;

        if (!powerUpsConfigLookup.HasBuffer(playerEntity))
            return;

        ReadSlots(powerUpsConfigLookup[playerEntity],
                  out primarySlot,
                  out secondarySlot);
    }

    /// <summary>
    /// Writes one active power-up loadout while preserving a compact per-slot buffer contract.
    /// </summary>
    /// <param name="powerUpsConfigBuffer">Buffer receiving the loadout snapshot.</param>
    /// <param name="powerUpsConfig">Mutable runtime loadout produced by power-up systems.</param>
    public static void Write(DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer,
                             in PlayerPowerUpsConfig powerUpsConfig)
    {
        WriteSlots(powerUpsConfigBuffer,
                   in powerUpsConfig.PrimarySlot,
                   in powerUpsConfig.SecondarySlot);
    }

    /// <summary>
    /// Writes active power-up slots directly, avoiding construction or passing of the large two-slot wrapper during bake and runtime paths.
    /// </summary>
    /// <param name="powerUpsConfigBuffer">Buffer receiving the loadout snapshot.</param>
    /// <param name="primarySlot">Primary active slot payload.</param>
    /// <param name="secondarySlot">Secondary active slot payload.</param>
    public static void WriteSlots(DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer,
                                  in PlayerPowerUpSlotConfig primarySlot,
                                  in PlayerPowerUpSlotConfig secondarySlot)
    {
        powerUpsConfigBuffer.ResizeUninitialized(SlotCount);

        ref PlayerPowerUpsConfigElement primaryElement = ref powerUpsConfigBuffer.ElementAt(0);
        primaryElement.SlotIndex = PrimarySlotIndex;
        primaryElement.Slot = primarySlot;

        ref PlayerPowerUpsConfigElement secondaryElement = ref powerUpsConfigBuffer.ElementAt(1);
        secondaryElement.SlotIndex = SecondarySlotIndex;
        secondaryElement.Slot = secondarySlot;
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

    /// <summary>
    /// Writes active power-up slots when a player entity owns the external config buffer.
    /// </summary>
    /// <param name="playerEntity">Player entity receiving the loadout snapshot.</param>
    /// <param name="powerUpsConfigLookup">Lookup used to access player loadout snapshot buffers.</param>
    /// <param name="primarySlot">Primary active slot payload.</param>
    /// <param name="secondarySlot">Secondary active slot payload.</param>
    /// <returns>True when the entity owns the config buffer and the loadout was written.</returns>
    public static bool TryWriteSlots(Entity playerEntity,
                                     in BufferLookup<PlayerPowerUpsConfigElement> powerUpsConfigLookup,
                                     in PlayerPowerUpSlotConfig primarySlot,
                                     in PlayerPowerUpSlotConfig secondarySlot)
    {
        if (!powerUpsConfigLookup.HasBuffer(playerEntity))
            return false;

        WriteSlots(powerUpsConfigLookup[playerEntity],
                   in primarySlot,
                   in secondarySlot);
        return true;
    }
    #endregion

    #endregion
}
