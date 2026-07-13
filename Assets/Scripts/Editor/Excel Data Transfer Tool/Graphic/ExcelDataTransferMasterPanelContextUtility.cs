/// <summary>
/// Resolves lightweight display metadata for the Excel transfer master panel.
/// </summary>
internal static class ExcelDataTransferMasterPanelContextUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves display text for one transfer master preset.
    /// </summary>
    /// <param name="preset">Preset to display.</param>
    /// <returns>Display name for sidebar rows.</returns>
    public static string ResolvePresetDisplayName(ExcelDataTransferMasterPreset preset)
    {
        if (preset == null)
            return "<Missing Preset>";

        string presetName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        return presetName;
    }
    #endregion

    #endregion
}

/// <summary>
/// Local detail sections available for one Excel transfer master preset.
/// </summary>
internal enum ExcelDataTransferDetailsSectionType
{
    Metadata = 0,
    SubPresets = 1,
    Import = 2,
    Export = 3,
    LayoutBrush = 4,
    FieldCatalog = 5,
    BrushPalette = 6
}

/// <summary>
/// Top-level side panels available inside the Excel transfer master preset tool.
/// </summary>
internal enum ExcelDataTransferPanelType
{
    TransferMasterPresets = 0,
    ImportPreset = 1,
    ExportPreset = 2,
    WorkbookLayout = 3,
    BrushPalette = 4
}
