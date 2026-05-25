using Unity.Entities;

/// <summary>
/// Reads and writes compact active-slot snapshots used by power-up cheat presets.
/// </summary>
internal static class PlayerPowerUpCheatPresetSlotBufferUtility
{
    #region Constants
    private const byte PrimarySlotIndex = 0;
    private const byte SecondarySlotIndex = 1;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Appends active slots directly into the flattened slot buffer without constructing the large two-slot wrapper.
    /// </summary>
    /// <param name="slotBuffer">Destination buffer receiving compact active-slot entries.</param>
    /// <param name="primarySlot">Primary active slot payload.</param>
    /// <param name="secondarySlot">Secondary active slot payload.</param>
    /// <returns>Number of slot entries appended.</returns>
    public static int AppendSlots(DynamicBuffer<PlayerPowerUpCheatPresetSlotElement> slotBuffer,
                                  in PlayerPowerUpSlotConfig primarySlot,
                                  in PlayerPowerUpSlotConfig secondarySlot)
    {
        int startIndex = slotBuffer.Length;
        slotBuffer.ResizeUninitialized(startIndex + 2);

        ref PlayerPowerUpCheatPresetSlotElement primaryElement = ref slotBuffer.ElementAt(startIndex);
        primaryElement.SlotIndex = PrimarySlotIndex;
        primaryElement.Slot = primarySlot;

        ref PlayerPowerUpCheatPresetSlotElement secondaryElement = ref slotBuffer.ElementAt(startIndex + 1);
        secondaryElement.SlotIndex = SecondarySlotIndex;
        secondaryElement.Slot = secondarySlot;

        return 2;
    }

    /// <summary>
    /// Rebuilds one logical power-up config from the flattened active-slot range referenced by a cheat preset entry.
    /// </summary>
    /// <param name="entry">Cheat preset metadata containing the slot range to read.</param>
    /// <param name="slotBuffer">Flattened active-slot payload buffer.</param>
    /// <param name="powerUpsConfig">Reconstructed active power-up config.</param>
    public static void Read(in PlayerPowerUpCheatPresetEntry entry,
                            DynamicBuffer<PlayerPowerUpCheatPresetSlotElement> slotBuffer,
                            out PlayerPowerUpsConfig powerUpsConfig)
    {
        powerUpsConfig = default;

        if (slotBuffer.Length <= 0 || entry.SlotCount <= 0)
            return;

        int safeStartIndex = Unity.Mathematics.math.clamp(entry.SlotStartIndex, 0, slotBuffer.Length);
        int availableCount = slotBuffer.Length - safeStartIndex;
        int safeCount = Unity.Mathematics.math.clamp(entry.SlotCount, 0, availableCount);

        // Rehydrate the two runtime slots from the flattened cheat preset slice.
        for (int slotOffset = 0; slotOffset < safeCount; slotOffset++)
        {
            ref PlayerPowerUpCheatPresetSlotElement slotElement = ref slotBuffer.ElementAt(safeStartIndex + slotOffset);

            switch (slotElement.SlotIndex)
            {
                case PrimarySlotIndex:
                    powerUpsConfig.PrimarySlot = slotElement.Slot;
                    break;
                case SecondarySlotIndex:
                    powerUpsConfig.SecondarySlot = slotElement.Slot;
                    break;
            }
        }
    }
    #endregion

    #endregion
}
