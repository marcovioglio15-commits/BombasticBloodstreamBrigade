/// <summary>
/// Applies stable list reorders directly on preset backing collections to avoid SerializedProperty.MoveArrayElement issues on large nested payloads.
/// </summary>
internal static class PlayerPowerUpsPresetReorderUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Moves one modular power-up definition inside the requested active or passive collection.
    /// </summary>
    /// <param name="preset">Preset that owns the target collection.</param>
    /// <param name="isActiveSection">True for the active list, false for the passive list.</param>
    /// <param name="fromIndex">Current list index of the element to move.</param>
    /// <param name="toIndex">Destination list index after the reorder.</param>
    /// <returns>True when the move was applied, otherwise false.</returns>
    public static bool MovePowerUpDefinition(PlayerPowerUpsPreset preset,
                                             bool isActiveSection,
                                             int fromIndex,
                                             int toIndex)
    {
        if (preset == null)
            return false;

        return preset.MovePowerUpDefinition(isActiveSection, fromIndex, toIndex);
    }

    /// <summary>
    /// Moves one reusable module definition inside the preset module catalog.
    /// </summary>
    /// <param name="preset">Preset that owns the module list.</param>
    /// <param name="fromIndex">Current module index.</param>
    /// <param name="toIndex">Destination module index.</param>
    /// <returns>True when the move was applied, otherwise false.</returns>
    public static bool MoveModuleDefinition(PlayerPowerUpsPreset preset, int fromIndex, int toIndex)
    {
        if (preset == null)
            return false;

        return preset.MoveModuleDefinition(fromIndex, toIndex);
    }
    #endregion

    #endregion
}
