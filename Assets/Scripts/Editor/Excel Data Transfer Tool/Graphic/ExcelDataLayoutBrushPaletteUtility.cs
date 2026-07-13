using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Handles saved brush palette options and persistence for the Excel layout brush panel.
/// </summary>
internal static class ExcelDataLayoutBrushPaletteUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds stable display options for saved brush configurations.
    /// </summary>
    /// <param name="brushPalettePreset">Brush palette that owns saved configurations.</param>
    /// <returns>Saved brush option labels.</returns>
    public static List<string> BuildSavedBrushOptions(ExcelDataBrushPalettePreset brushPalettePreset)
    {
        List<string> options = new List<string>();

        if (brushPalettePreset == null)
        {
            options.Add("No Brush Palette");
            return options;
        }

        List<ExcelDataBrushDefinition> brushes = brushPalettePreset.Brushes;

        if (brushes.Count <= 0)
        {
            options.Add("No Saved Brushes");
            return options;
        }

        for (int brushIndex = 0; brushIndex < brushes.Count; brushIndex++)
        {
            ExcelDataBrushDefinition brush = brushes[brushIndex];
            string brushName = brush == null ? "Missing Brush" : brush.BrushName;
            options.Add((brushIndex + 1).ToString(CultureInfo.InvariantCulture) + ". " + brushName);
        }

        return options;
    }

    /// <summary>
    /// Rebuilds saved brush dropdown options while preserving the current selection when still valid.
    /// </summary>
    /// <param name="savedBrushField">Dropdown that displays saved brush options.</param>
    /// <param name="brushPalettePreset">Brush palette that owns saved configurations.</param>
    public static void RefreshSavedBrushOptions(PopupField<string> savedBrushField,
                                                ExcelDataBrushPalettePreset brushPalettePreset)
    {
        if (savedBrushField == null)
            return;

        string previousValue = savedBrushField.value;
        List<string> options = BuildSavedBrushOptions(brushPalettePreset);
        savedBrushField.choices = options;

        if (!string.IsNullOrWhiteSpace(previousValue) && options.Contains(previousValue))
            savedBrushField.SetValueWithoutNotify(previousValue);
        else
            savedBrushField.SetValueWithoutNotify(options.Count > 0 ? options[0] : string.Empty);
    }

    /// <summary>
    /// Creates the color field used for newly saved brush configurations.
    /// </summary>
    /// <returns>Configured brush color field.</returns>
    public static ColorField CreateBrushColorField()
    {
        ColorField field = new ColorField("Brush Color");
        field.tooltip = "Color assigned to cells painted by fields that match the selected brush configuration.";
        field.SetValueWithoutNotify(new Color(0.85f, 0.85f, 0.85f, 1f));
        return field;
    }

    /// <summary>
    /// Applies a saved brush configuration to layout filter controls.
    /// </summary>
    /// <param name="brushPalettePreset">Brush palette searched by dropdown option.</param>
    /// <param name="optionLabel">Visible brush option selected by the user.</param>
    /// <param name="domainField">Domain filter control.</param>
    /// <param name="dataKindField">Data kind filter control.</param>
    /// <param name="listModeField">List element filter control.</param>
    /// <param name="sourceTypeSearchField">Source type search control.</param>
    /// <param name="sourceAssetSearchField">Concrete source asset search control.</param>
    /// <param name="fieldSearchField">General field search control.</param>
    /// <param name="brushColorField">Brush color control.</param>
    /// <param name="direction">Saved transfer direction when a brush is resolved.</param>
    /// <returns>True when a saved brush was found and applied.</returns>
    public static bool ApplySavedBrushConfiguration(ExcelDataBrushPalettePreset brushPalettePreset,
                                                    string optionLabel,
                                                    EnumField domainField,
                                                    EnumField dataKindField,
                                                    EnumField listModeField,
                                                    ToolbarSearchField sourceTypeSearchField,
                                                    ToolbarSearchField sourceAssetSearchField,
                                                    ToolbarSearchField fieldSearchField,
                                                    ColorField brushColorField,
                                                    out ExcelDataTransferDirection direction)
    {
        direction = ExcelDataTransferDirection.Both;
        ExcelDataBrushDefinition brush = FindBrushByOption(brushPalettePreset, optionLabel);

        if (brush == null)
            return false;

        if (domainField != null)
            domainField.SetValueWithoutNotify(brush.Domain);

        if (dataKindField != null)
            dataKindField.SetValueWithoutNotify(brush.DataKind);

        if (listModeField != null)
            listModeField.SetValueWithoutNotify(brush.ListFilter);

        if (sourceTypeSearchField != null)
            sourceTypeSearchField.SetValueWithoutNotify(string.IsNullOrWhiteSpace(brush.SourceFilter) ? string.Empty : brush.SourceFilter);

        if (sourceAssetSearchField != null)
            sourceAssetSearchField.SetValueWithoutNotify(string.IsNullOrWhiteSpace(brush.SourceAssetFilter) ? string.Empty : brush.SourceAssetFilter);

        if (fieldSearchField != null)
            fieldSearchField.SetValueWithoutNotify(string.IsNullOrWhiteSpace(brush.FieldSearchFilter) ? string.Empty : brush.FieldSearchFilter);

        if (brushColorField != null)
            brushColorField.SetValueWithoutNotify(brush.Color);

        direction = brush.Direction;
        return true;
    }

    /// <summary>
    /// Saves the current layout filter state as a brush configuration.
    /// </summary>
    /// <param name="brushPalettePreset">Brush palette receiving the saved configuration.</param>
    /// <param name="domainField">Domain filter control.</param>
    /// <param name="dataKindField">Data kind filter control.</param>
    /// <param name="listModeField">List element filter control.</param>
    /// <param name="sourceTypeSearchField">Source type search control.</param>
    /// <param name="sourceAssetSearchField">Concrete source asset search control.</param>
    /// <param name="brushColorField">Brush color control.</param>
    /// <param name="searchField">Main catalog search control.</param>
    /// <param name="direction">Transfer direction saved with the brush.</param>
    /// <param name="selectedOption">Dropdown option for the saved brush.</param>
    /// <param name="statusMessage">User-facing status message.</param>
    /// <returns>True when the brush was saved.</returns>
    public static bool SaveCurrentBrushConfiguration(ExcelDataBrushPalettePreset brushPalettePreset,
                                                     EnumField domainField,
                                                     EnumField dataKindField,
                                                     EnumField listModeField,
                                                     ToolbarSearchField sourceTypeSearchField,
                                                     ToolbarSearchField sourceAssetSearchField,
                                                     ColorField brushColorField,
                                                     ToolbarSearchField searchField,
                                                     ExcelDataTransferDirection direction,
                                                     out string selectedOption,
                                                     out string statusMessage)
    {
        selectedOption = string.Empty;

        if (brushPalettePreset == null)
        {
            statusMessage = "Cannot save brush: missing brush palette preset.";
            return false;
        }

        List<ExcelDataBrushDefinition> brushes = brushPalettePreset.Brushes;
        ExcelDataBrushDefinition brush = new ExcelDataBrushDefinition();
        string brushName = "Brush " + (brushes.Count + 1).ToString(CultureInfo.InvariantCulture);

        brush.Configure(brushName,
                        domainField == null ? ExcelDataTransferDomain.All : (ExcelDataTransferDomain)domainField.value,
                        dataKindField == null ? ExcelDataBrushDataKind.All : (ExcelDataBrushDataKind)dataKindField.value,
                        listModeField == null ? ExcelDataListElementFilterMode.AllBrushableFields : (ExcelDataListElementFilterMode)listModeField.value,
                        sourceTypeSearchField == null ? string.Empty : sourceTypeSearchField.value,
                        sourceAssetSearchField == null ? string.Empty : sourceAssetSearchField.value,
                        searchField == null ? string.Empty : searchField.value,
                        direction,
                        brushColorField == null ? Color.white : brushColorField.value,
                        searchField == null ? string.Empty : searchField.value,
                        "Saved from the Layout Brush panel.");
        brushes.Add(brush);
        EditorUtility.SetDirty(brushPalettePreset);
        ExcelDataTransferDraftSession.MarkDirty();

        selectedOption = brushes.Count.ToString(CultureInfo.InvariantCulture) + ". " + brushName;
        statusMessage = "Saved brush configuration: " + brushName;
        return true;
    }

    /// <summary>
    /// Resolves the stable brush ID represented by one saved-brush dropdown option.
    /// </summary>
    /// <param name="brushPalettePreset">Brush palette searched by dropdown option.</param>
    /// <param name="optionLabel">Displayed saved-brush option.</param>
    /// <returns>Stable brush ID, or an empty string when the option is not a saved brush.</returns>
    public static string ResolveBrushId(ExcelDataBrushPalettePreset brushPalettePreset,
                                        string optionLabel)
    {
        ExcelDataBrushDefinition brush = FindBrushByOption(brushPalettePreset, optionLabel);
        return brush == null ? string.Empty : brush.BrushId;
    }

    /// <summary>
    /// Finds one saved brush by the stable ID stored in an authored workbook cell.
    /// </summary>
    /// <param name="brushPalettePreset">Brush palette to search.</param>
    /// <param name="brushId">Stable authored brush ID.</param>
    /// <returns>Matching brush, or null.</returns>
    public static ExcelDataBrushDefinition FindBrushById(ExcelDataBrushPalettePreset brushPalettePreset,
                                                         string brushId)
    {
        if (brushPalettePreset == null || string.IsNullOrWhiteSpace(brushId))
            return null;

        List<ExcelDataBrushDefinition> brushes = brushPalettePreset.Brushes;

        for (int brushIndex = 0; brushIndex < brushes.Count; brushIndex++)
        {
            ExcelDataBrushDefinition brush = brushes[brushIndex];

            if (brush != null && string.Equals(brush.BrushId, brushId, StringComparison.Ordinal))
                return brush;
        }

        return null;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Finds a saved brush definition from its displayed dropdown option.
    /// </summary>
    /// <param name="brushPalettePreset">Brush palette searched by dropdown option.</param>
    /// <param name="optionLabel">Displayed option label.</param>
    /// <returns>Saved brush definition, or null when no brush matches.</returns>
    private static ExcelDataBrushDefinition FindBrushByOption(ExcelDataBrushPalettePreset brushPalettePreset,
                                                              string optionLabel)
    {
        if (brushPalettePreset == null || string.IsNullOrWhiteSpace(optionLabel))
            return null;

        int separatorIndex = optionLabel.IndexOf('.', StringComparison.Ordinal);

        if (separatorIndex <= 0)
            return null;

        string indexText = optionLabel.Substring(0, separatorIndex);
        int brushIndex = 0;

        if (!int.TryParse(indexText, out brushIndex))
            return null;

        brushIndex--;
        List<ExcelDataBrushDefinition> brushes = brushPalettePreset.Brushes;

        if (brushIndex < 0 || brushIndex >= brushes.Count)
            return null;

        return brushes[brushIndex];
    }
    #endregion

    #endregion
}
