using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Resolves asset lists, linked references and display metadata for linked Excel transfer sub-preset panels.
/// </summary>
internal static class ExcelDataLinkedSubPresetPanelContextUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads sub-presets for the requested panel family.
    /// </summary>
    /// <param name="panelType">Panel family to load.</param>
    /// <returns>Loaded sub-presets as scriptable objects.</returns>
    public static List<ScriptableObject> LoadPresetsForPanel(ExcelDataTransferPanelType panelType)
    {
        List<ScriptableObject> presets = new List<ScriptableObject>();

        switch (panelType)
        {
            case ExcelDataTransferPanelType.ImportPreset:
                AddTypedPresets(presets, ExcelDataTransferAssetUtility.LoadSubPresets<ExcelDataImportPreset>());
                break;
            case ExcelDataTransferPanelType.ExportPreset:
                AddTypedPresets(presets, ExcelDataTransferAssetUtility.LoadSubPresets<ExcelDataExportPreset>());
                break;
            case ExcelDataTransferPanelType.BrushPalette:
                AddTypedPresets(presets, ExcelDataTransferAssetUtility.LoadSubPresets<ExcelDataBrushPalettePreset>());
                break;
        }

        return presets;
    }

    /// <summary>
    /// Creates one sub-preset asset for the requested panel family.
    /// </summary>
    /// <param name="panelType">Sub-preset panel family.</param>
    /// <returns>Created sub-preset asset, or null for unsupported panels.</returns>
    public static ScriptableObject CreatePresetForPanel(ExcelDataTransferPanelType panelType)
    {
        switch (panelType)
        {
            case ExcelDataTransferPanelType.ImportPreset:
                return ExcelDataTransferSubPresetAssetUtility.CreateSubPreset<ExcelDataImportPreset>("ExcelDataImportPreset", "Excel Import");
            case ExcelDataTransferPanelType.ExportPreset:
                return ExcelDataTransferSubPresetAssetUtility.CreateSubPreset<ExcelDataExportPreset>("ExcelDataExportPreset", "Excel Export");
            case ExcelDataTransferPanelType.BrushPalette:
                return ExcelDataTransferSubPresetAssetUtility.CreateSubPreset<ExcelDataBrushPalettePreset>("ExcelDataBrushPalettePreset", "Excel Brush Palette");
            default:
                return null;
        }
    }

    /// <summary>
    /// Duplicates one selected sub-preset for the requested panel family.
    /// </summary>
    /// <param name="panelType">Sub-preset panel family.</param>
    /// <param name="selectedPreset">Selected source preset.</param>
    /// <returns>Duplicated sub-preset asset, or null when unsupported.</returns>
    public static ScriptableObject DuplicatePresetForPanel(ExcelDataTransferPanelType panelType,
                                                           ScriptableObject selectedPreset)
    {
        switch (panelType)
        {
            case ExcelDataTransferPanelType.ImportPreset:
                return ExcelDataTransferSubPresetAssetUtility.DuplicateSubPreset(selectedPreset as ExcelDataImportPreset);
            case ExcelDataTransferPanelType.ExportPreset:
                return ExcelDataTransferSubPresetAssetUtility.DuplicateSubPreset(selectedPreset as ExcelDataExportPreset);
            case ExcelDataTransferPanelType.BrushPalette:
                return ExcelDataTransferSubPresetAssetUtility.DuplicateSubPreset(selectedPreset as ExcelDataBrushPalettePreset);
            default:
                return null;
        }
    }

    /// <summary>
    /// Deletes one sub-preset when it is not referenced by any transfer master preset.
    /// </summary>
    /// <param name="selectedPreset">Selected preset to delete.</param>
    /// <param name="statusMessage">User-facing delete result.</param>
    /// <returns>True when the preset was deleted.</returns>
    public static bool DeletePreset(ScriptableObject selectedPreset, out string statusMessage)
    {
        statusMessage = string.Empty;

        if (selectedPreset == null)
        {
            statusMessage = "Select a sub-preset before deleting.";
            return false;
        }

        string deletedPresetName = selectedPreset.name;
        string blockingMasterName;

        if (!ExcelDataTransferSubPresetAssetUtility.DeleteSubPresetIfUnreferenced(selectedPreset, out blockingMasterName))
        {
            statusMessage = string.IsNullOrWhiteSpace(blockingMasterName)
                ? "Could not delete the selected sub-preset."
                : "Cannot delete: still referenced by " + blockingMasterName + ".";
            return false;
        }

        statusMessage = "Deleted sub-preset " + deletedPresetName + ".";
        return true;
    }

    /// <summary>
    /// Resolves the sub-preset currently linked by the active master.
    /// </summary>
    /// <param name="panelType">Sub-preset panel family.</param>
    /// <param name="masterPreset">Active transfer master preset.</param>
    /// <returns>Linked sub-preset, or null.</returns>
    public static ScriptableObject ResolveLinkedPreset(ExcelDataTransferPanelType panelType,
                                                       ExcelDataTransferMasterPreset masterPreset)
    {
        if (masterPreset == null)
            return null;

        switch (panelType)
        {
            case ExcelDataTransferPanelType.ImportPreset:
                return masterPreset.ImportPreset;
            case ExcelDataTransferPanelType.ExportPreset:
                return masterPreset.ExportPreset;
            case ExcelDataTransferPanelType.BrushPalette:
                return masterPreset.BrushPalettePreset;
            default:
                return null;
        }
    }

    /// <summary>
    /// Resolves the master object-reference property used by one sub-preset panel family.
    /// </summary>
    /// <param name="panelType">Sub-preset panel family.</param>
    /// <returns>Serialized master property name.</returns>
    public static string ResolveMasterPropertyName(ExcelDataTransferPanelType panelType)
    {
        switch (panelType)
        {
            case ExcelDataTransferPanelType.ImportPreset:
                return "importPreset";
            case ExcelDataTransferPanelType.ExportPreset:
                return "exportPreset";
            case ExcelDataTransferPanelType.BrushPalette:
                return "brushPalettePreset";
            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// Resolves the panel title shown in browsers and empty-state messages.
    /// </summary>
    /// <param name="panelType">Sub-preset panel family.</param>
    /// <returns>Visible panel title.</returns>
    public static string ResolvePanelTitle(ExcelDataTransferPanelType panelType)
    {
        switch (panelType)
        {
            case ExcelDataTransferPanelType.ImportPreset:
                return "Import Presets";
            case ExcelDataTransferPanelType.ExportPreset:
                return "Export Presets";
            case ExcelDataTransferPanelType.BrushPalette:
                return "Brush Palettes";
            default:
                return "Sub Presets";
        }
    }

    /// <summary>
    /// Resolves display text for one sub-preset asset.
    /// </summary>
    /// <param name="preset">Preset asset to display.</param>
    /// <returns>Readable display name.</returns>
    public static string ResolvePresetDisplayName(ScriptableObject preset)
    {
        if (preset == null)
            return "<Missing Preset>";

        SerializedObject serializedObject = new SerializedObject(preset);
        SerializedProperty presetNameProperty = serializedObject.FindProperty("presetName");

        if (presetNameProperty != null && !string.IsNullOrWhiteSpace(presetNameProperty.stringValue))
            return presetNameProperty.stringValue;

        return preset.name;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Adds typed sub-presets to a scriptable object list.
    /// </summary>
    /// <typeparam name="T">ScriptableObject subtype.</typeparam>
    /// <param name="target">Target list.</param>
    /// <param name="source">Typed source list.</param>
    private static void AddTypedPresets<T>(List<ScriptableObject> target, List<T> source) where T : ScriptableObject
    {
        for (int index = 0; index < source.Count; index++)
            target.Add(source[index]);
    }
    #endregion

    #endregion
}
