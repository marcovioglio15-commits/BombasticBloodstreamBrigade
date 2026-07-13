using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates, duplicates and deletes standalone Excel transfer sub-preset assets.
/// </summary>
internal static class ExcelDataTransferSubPresetAssetUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates one standalone sub-preset asset under the Excel transfer preset root.
    /// </summary>
    /// <typeparam name="T">Sub-preset type to create.</typeparam>
    /// <param name="rawPresetName">Readable base name used for the generated asset.</param>
    /// <param name="displayName">Readable preset name stored inside the asset.</param>
    /// <returns>Created sub-preset asset.</returns>
    public static T CreateSubPreset<T>(string rawPresetName, string displayName) where T : ScriptableObject
    {
        GameManagementAssetUtility.EnsureFolder(ExcelDataTransferAssetUtility.PresetRoot);
        string fallbackName = string.IsNullOrWhiteSpace(rawPresetName) ? typeof(T).Name : rawPresetName;
        string normalizedName = ExcelDataTransferAssetUtility.NormalizeAssetName(fallbackName);
        string readableName = string.IsNullOrWhiteSpace(displayName) ? typeof(T).Name : displayName;
        T preset = ExcelDataTransferAssetUtility.CreateAsset<T>(normalizedName, readableName);

        SetPresetName(preset, preset.name);
        SeedStandaloneSubPreset(preset);
        EditorUtility.SetDirty(preset);
        AssetDatabase.SaveAssets();
        return preset;
    }

    /// <summary>
    /// Duplicates one standalone sub-preset asset without changing master references.
    /// </summary>
    /// <typeparam name="T">Sub-preset type to duplicate.</typeparam>
    /// <param name="sourcePreset">Source preset asset.</param>
    /// <returns>Duplicated sub-preset asset, or null when the source is missing.</returns>
    public static T DuplicateSubPreset<T>(T sourcePreset) where T : ScriptableObject
    {
        if (sourcePreset == null)
            return null;

        string sourceName = ResolveSerializedPresetName(sourcePreset);
        T duplicatedPreset = CreateSubPreset<T>(sourcePreset.name + " Copy", sourceName + " Copy");

        ExcelDataTransferAssetUtility.CopyPresetData(sourcePreset, duplicatedPreset);
        duplicatedPreset.name = sourcePreset.name + " Copy";
        SetPresetName(duplicatedPreset, sourceName + " Copy");
        ExcelDataTransferAssetUtility.RefreshPresetId(duplicatedPreset);
        SeedStandaloneSubPreset(duplicatedPreset);
        EditorUtility.SetDirty(duplicatedPreset);
        AssetDatabase.SaveAssets();
        return duplicatedPreset;
    }

    /// <summary>
    /// Deletes a standalone sub-preset only when no transfer master references it.
    /// </summary>
    /// <param name="subPreset">Sub-preset asset to delete.</param>
    /// <param name="blockingMasterName">Name of the first master preset that still references the asset.</param>
    /// <returns>True when the preset was deleted.</returns>
    public static bool DeleteSubPresetIfUnreferenced(ScriptableObject subPreset, out string blockingMasterName)
    {
        blockingMasterName = string.Empty;

        if (subPreset == null)
            return false;

        ExcelDataTransferMasterPreset referencingMaster;

        if (TryFindReferencingMasterPreset(subPreset, out referencingMaster))
        {
            blockingMasterName = referencingMaster == null ? string.Empty : referencingMaster.name;
            return false;
        }

        ExcelDataTransferAssetUtility.DeleteAsset(subPreset);
        AssetDatabase.SaveAssets();
        return true;
    }

    /// <summary>
    /// Finds the first transfer master that references a sub-preset asset.
    /// </summary>
    /// <param name="subPreset">Sub-preset asset to search.</param>
    /// <param name="referencingMaster">First master preset referencing the asset.</param>
    /// <returns>True when a master reference exists.</returns>
    public static bool TryFindReferencingMasterPreset(ScriptableObject subPreset,
                                                      out ExcelDataTransferMasterPreset referencingMaster)
    {
        referencingMaster = null;

        if (subPreset == null)
            return false;

        List<ExcelDataTransferMasterPreset> masterPresets = ExcelDataTransferAssetUtility.LoadMasterPresets();

        for (int masterIndex = 0; masterIndex < masterPresets.Count; masterIndex++)
        {
            ExcelDataTransferMasterPreset masterPreset = masterPresets[masterIndex];

            if (masterPreset == null)
                continue;

            if (masterPreset.LayoutPreset == subPreset ||
                masterPreset.BrushPalettePreset == subPreset ||
                masterPreset.ImportPreset == subPreset ||
                masterPreset.ExportPreset == subPreset)
            {
                referencingMaster = masterPreset;
                return true;
            }
        }

        return false;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Writes the shared presetName field when the sub-preset type exposes it.
    /// </summary>
    /// <param name="presetAsset">Preset asset to update.</param>
    /// <param name="presetName">Readable preset name.</param>
    private static void SetPresetName(ScriptableObject presetAsset, string presetName)
    {
        if (presetAsset == null)
            return;

        SerializedObject serializedObject = new SerializedObject(presetAsset);
        SerializedProperty presetNameProperty = serializedObject.FindProperty("presetName");

        if (presetNameProperty == null)
            return;

        presetNameProperty.stringValue = presetName;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(presetAsset);
    }

    /// <summary>
    /// Resolves the readable presetName field for duplication labels.
    /// </summary>
    /// <param name="presetAsset">Preset asset to inspect.</param>
    /// <returns>Readable preset name or Unity object name.</returns>
    private static string ResolveSerializedPresetName(ScriptableObject presetAsset)
    {
        if (presetAsset == null)
            return "Excel Data Preset";

        SerializedObject serializedObject = new SerializedObject(presetAsset);
        SerializedProperty presetNameProperty = serializedObject.FindProperty("presetName");

        if (presetNameProperty != null && !string.IsNullOrWhiteSpace(presetNameProperty.stringValue))
            return presetNameProperty.stringValue;

        return presetAsset.name;
    }

    /// <summary>
    /// Seeds type-specific defaults for a standalone sub-preset.
    /// </summary>
    /// <param name="presetAsset">Preset asset to seed.</param>
    private static void SeedStandaloneSubPreset(ScriptableObject presetAsset)
    {
        ExcelDataWorkbookLayoutPreset layoutPreset = presetAsset as ExcelDataWorkbookLayoutPreset;

        if (layoutPreset != null)
        {
            layoutPreset.ValidateValues();
            ExcelDataTransferDefaultPresetUtility.EnsureLayoutPresetDefaults(layoutPreset);
            return;
        }

        ExcelDataBrushPalettePreset brushPalettePreset = presetAsset as ExcelDataBrushPalettePreset;

        if (brushPalettePreset != null)
        {
            ExcelDataTransferAssetUtility.EnsureDefaultBrushes(brushPalettePreset);
            return;
        }

        ExcelDataImportPreset importPreset = presetAsset as ExcelDataImportPreset;

        if (importPreset != null)
        {
            importPreset.ValidateValues();
            ExcelDataTransferDefaultPresetUtility.EnsureImportPresetDefaults(importPreset);
            return;
        }

        ExcelDataExportPreset exportPreset = presetAsset as ExcelDataExportPreset;

        if (exportPreset != null)
        {
            exportPreset.ValidateValues();
            ExcelDataTransferDefaultPresetUtility.EnsureExportPresetDefaults(exportPreset);
        }
    }
    #endregion

    #endregion
}
