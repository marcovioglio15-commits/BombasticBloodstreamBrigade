using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates and locates editor-only assets used by the Excel Data Transfer Tool.
/// </summary>
public static class ExcelDataTransferAssetUtility
{
    #region Constants
    public const string PresetRoot = "Assets/Scriptable Objects/Editor/Excel Data Transfer";
    public const string SelectedMasterPresetStateKey = "NashCore.ExcelDataTransfer.SelectedMasterPreset";

    private const string DefaultMasterPresetPath = PresetRoot + "/DefaultExcelDataTransferMasterPreset.asset";
    private const string DefaultImportPresetPath = PresetRoot + "/DefaultExcelDataImportPreset.asset";
    private const string DefaultExportPresetPath = PresetRoot + "/DefaultExcelDataExportPreset.asset";
    private const string DefaultLayoutPresetPath = PresetRoot + "/DefaultExcelDataWorkbookLayoutPreset.asset";
    private const string DefaultBrushPalettePresetPath = PresetRoot + "/DefaultExcelDataBrushPalettePreset.asset";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads the default master preset or creates the complete preset graph when missing.
    /// </summary>
    /// <returns>Default master preset with import, export, layout and brush palette references assigned.</returns>
    public static ExcelDataTransferMasterPreset GetOrCreateDefaultMasterPreset()
    {
        GameManagementAssetUtility.EnsureFolder(PresetRoot);

        ExcelDataWorkbookLayoutPreset layoutPreset =
            GetOrCreateAsset<ExcelDataWorkbookLayoutPreset>(DefaultLayoutPresetPath, "Default Workbook Layout");
        ExcelDataBrushPalettePreset brushPalettePreset =
            GetOrCreateAsset<ExcelDataBrushPalettePreset>(DefaultBrushPalettePresetPath, "Default Brush Palette");
        ExcelDataImportPreset importPreset =
            GetOrCreateAsset<ExcelDataImportPreset>(DefaultImportPresetPath, "Default Import");
        ExcelDataExportPreset exportPreset =
            GetOrCreateAsset<ExcelDataExportPreset>(DefaultExportPresetPath, "Default Export");
        ExcelDataTransferMasterPreset masterPreset =
            GetOrCreateAsset<ExcelDataTransferMasterPreset>(DefaultMasterPresetPath, "Default Excel Data Transfer");

        EnsureDefaultBrushes(brushPalettePreset);
        ExcelDataTransferDefaultPresetUtility.EnsureTransferGraphDefaults(layoutPreset, importPreset, exportPreset);
        masterPreset.AssignLinkedPresets(layoutPreset, brushPalettePreset, importPreset, exportPreset);
        masterPreset.ValidateValues();

        EditorUtility.SetDirty(masterPreset);
        EditorUtility.SetDirty(layoutPreset);
        EditorUtility.SetDirty(importPreset);
        EditorUtility.SetDirty(exportPreset);
        EditorUtility.SetDirty(brushPalettePreset);
        AssetDatabase.SaveAssets();
        return masterPreset;
    }

    /// <summary>
    /// Loads every Excel transfer master preset under the tool preset root.
    /// </summary>
    /// <returns>Master presets sorted by asset name.</returns>
    public static List<ExcelDataTransferMasterPreset> LoadMasterPresets()
    {
        GameManagementAssetUtility.EnsureFolder(PresetRoot);
        string[] guids = AssetDatabase.FindAssets("t:" + nameof(ExcelDataTransferMasterPreset), new string[] { PresetRoot });
        List<ExcelDataTransferMasterPreset> presets = new List<ExcelDataTransferMasterPreset>();

        for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);

            if (string.IsNullOrWhiteSpace(assetPath))
                continue;

            ExcelDataTransferMasterPreset preset = AssetDatabase.LoadAssetAtPath<ExcelDataTransferMasterPreset>(assetPath);

            if (preset == null)
                continue;

            presets.Add(preset);
        }

        presets.Sort((left, right) => string.Compare(left.name, right.name, System.StringComparison.Ordinal));
        return presets;
    }

    /// <summary>
    /// Loads every Excel transfer sub-preset asset of the requested type under the tool preset root.
    /// </summary>
    /// <typeparam name="T">ScriptableObject sub-preset type to load.</typeparam>
    /// <returns>Sub-preset assets sorted by asset name.</returns>
    public static List<T> LoadSubPresets<T>() where T : ScriptableObject
    {
        GameManagementAssetUtility.EnsureFolder(PresetRoot);
        string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new string[] { PresetRoot });
        List<T> presets = new List<T>();

        for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);

            if (string.IsNullOrWhiteSpace(assetPath))
                continue;

            T preset = AssetDatabase.LoadAssetAtPath<T>(assetPath);

            if (preset == null)
                continue;

            presets.Add(preset);
        }

        presets.Sort((left, right) => string.Compare(left.name, right.name, System.StringComparison.Ordinal));
        return presets;
    }

    /// <summary>
    /// Creates a complete master/import/export/layout/brush preset graph.
    /// </summary>
    /// <param name="rawPresetName">Readable base name used for generated assets.</param>
    /// <returns>Created master preset with linked sub-presets.</returns>
    public static ExcelDataTransferMasterPreset CreatePresetGraph(string rawPresetName)
    {
        GameManagementAssetUtility.EnsureFolder(PresetRoot);
        string normalizedName = NormalizeAssetName(string.IsNullOrWhiteSpace(rawPresetName) ? "ExcelDataTransferPreset" : rawPresetName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "ExcelDataTransferPreset";

        ExcelDataWorkbookLayoutPreset layoutPreset =
            CreateAsset<ExcelDataWorkbookLayoutPreset>(normalizedName + "LayoutPreset", normalizedName + " Layout");
        ExcelDataBrushPalettePreset brushPalettePreset =
            CreateAsset<ExcelDataBrushPalettePreset>(normalizedName + "BrushPalettePreset", normalizedName + " Brush Palette");
        ExcelDataImportPreset importPreset =
            CreateAsset<ExcelDataImportPreset>(normalizedName + "ImportPreset", normalizedName + " Import");
        ExcelDataExportPreset exportPreset =
            CreateAsset<ExcelDataExportPreset>(normalizedName + "ExportPreset", normalizedName + " Export");
        ExcelDataTransferMasterPreset masterPreset =
            CreateAsset<ExcelDataTransferMasterPreset>(normalizedName + "MasterPreset", normalizedName);

        EnsureDefaultBrushes(brushPalettePreset);
        ExcelDataTransferDefaultPresetUtility.EnsureTransferGraphDefaults(layoutPreset, importPreset, exportPreset);
        masterPreset.AssignLinkedPresets(layoutPreset, brushPalettePreset, importPreset, exportPreset);
        masterPreset.ValidateValues();
        EditorUtility.SetDirty(masterPreset);
        return masterPreset;
    }

    /// <summary>
    /// Duplicates one complete preset graph without sharing linked sub-preset assets.
    /// </summary>
    /// <param name="sourcePreset">Source preset graph to duplicate.</param>
    /// <returns>Duplicated master preset, or null when the source is missing.</returns>
    public static ExcelDataTransferMasterPreset DuplicatePresetGraph(ExcelDataTransferMasterPreset sourcePreset)
    {
        if (sourcePreset == null)
            return null;

        ExcelDataTransferMasterPreset duplicatedPreset = CreatePresetGraph(sourcePreset.name + " Copy");
        CopyPresetData(sourcePreset.LayoutPreset, duplicatedPreset.LayoutPreset);
        CopyPresetData(sourcePreset.BrushPalettePreset, duplicatedPreset.BrushPalettePreset);
        CopyPresetData(sourcePreset.ImportPreset, duplicatedPreset.ImportPreset);
        CopyPresetData(sourcePreset.ExportPreset, duplicatedPreset.ExportPreset);
        CopyPresetData(sourcePreset, duplicatedPreset);
        RefreshPresetId(duplicatedPreset.LayoutPreset);
        RefreshPresetId(duplicatedPreset.BrushPalettePreset);
        RefreshPresetId(duplicatedPreset.ImportPreset);
        RefreshPresetId(duplicatedPreset.ExportPreset);
        RefreshPresetId(duplicatedPreset);
        duplicatedPreset.AssignLinkedPresets(duplicatedPreset.LayoutPreset,
                                             duplicatedPreset.BrushPalettePreset,
                                             duplicatedPreset.ImportPreset,
                                             duplicatedPreset.ExportPreset);
        duplicatedPreset.ValidateValues();
        EditorUtility.SetDirty(duplicatedPreset);
        return duplicatedPreset;
    }

    /// <summary>
    /// Deletes one master preset graph, including linked sub-presets only when no other master references them.
    /// </summary>
    /// <param name="masterPreset">Master preset graph to delete.</param>
    public static void DeletePresetGraph(ExcelDataTransferMasterPreset masterPreset)
    {
        if (masterPreset == null)
            return;

        List<ExcelDataTransferMasterPreset> allMasters = LoadMasterPresets();
        DeleteLinkedPresetIfUnshared(masterPreset.LayoutPreset, masterPreset, allMasters);
        DeleteLinkedPresetIfUnshared(masterPreset.BrushPalettePreset, masterPreset, allMasters);
        DeleteLinkedPresetIfUnshared(masterPreset.ImportPreset, masterPreset, allMasters);
        DeleteLinkedPresetIfUnshared(masterPreset.ExportPreset, masterPreset, allMasters);
        DeleteAsset(masterPreset);
    }

    /// <summary>
    /// Finds all tool assets that must participate in draft apply and discard tracking.
    /// </summary>
    /// <returns>Project-relative asset paths tracked by the Excel Data Transfer draft session.</returns>
    public static List<string> CollectTrackedAssetPaths()
    {
        HashSet<string> uniquePaths = new HashSet<string>();
        AddAssetPathsOfType<ExcelDataTransferMasterPreset>(uniquePaths);
        AddAssetPathsOfType<ExcelDataImportPreset>(uniquePaths);
        AddAssetPathsOfType<ExcelDataExportPreset>(uniquePaths);
        AddAssetPathsOfType<ExcelDataWorkbookLayoutPreset>(uniquePaths);
        AddAssetPathsOfType<ExcelDataBrushPalettePreset>(uniquePaths);
        return new List<string>(uniquePaths);
    }

    /// <summary>
    /// Loads the last selected master preset or falls back to the generated default preset graph.
    /// </summary>
    /// <returns>Selected or default master preset ready for editor operations.</returns>
    public static ExcelDataTransferMasterPreset LoadSelectedOrDefaultMasterPreset()
    {
        ExcelDataTransferMasterPreset masterPreset =
            ManagementToolStateUtility.LoadAsset<ExcelDataTransferMasterPreset>(SelectedMasterPresetStateKey);

        if (masterPreset != null)
            return masterPreset;

        return GetOrCreateDefaultMasterPreset();
    }

    /// <summary>
    /// Stores the selected master preset path in the shared tool state.
    /// </summary>
    /// <param name="masterPreset">Master preset selected by one Excel Data Transfer panel.</param>
    public static void SaveSelectedMasterPreset(ExcelDataTransferMasterPreset masterPreset)
    {
        ManagementToolStateUtility.SaveAssetPath(SelectedMasterPresetStateKey, masterPreset);
    }

    /// <summary>
    /// Converts display text into a safe Unity asset filename using the shared management utility.
    /// </summary>
    /// <param name="rawName">Raw user-authored name.</param>
    /// <returns>File-safe asset name, or an empty string if no valid characters remain.</returns>
    public static string NormalizeAssetName(string rawName)
    {
        return GameManagementAssetUtility.NormalizeAssetName(rawName);
    }
    #endregion

    #region Asset Creation
    /// <summary>
    /// Loads an asset at the provided path or creates a new ScriptableObject asset there.
    /// </summary>
    /// <typeparam name="T">ScriptableObject asset type to load or create.</typeparam>
    /// <param name="assetPath">Project-relative asset path.</param>
    /// <param name="displayName">Display name assigned to newly created assets.</param>
    /// <returns>Loaded or newly created asset instance.</returns>
    private static T GetOrCreateAsset<T>(string assetPath, string displayName) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);

        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<T>();
        asset.name = displayName;
        AssetDatabase.CreateAsset(asset, assetPath);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    /// <summary>
    /// Creates a ScriptableObject asset under the transfer preset root with a unique path.
    /// </summary>
    /// <typeparam name="T">ScriptableObject type to create.</typeparam>
    /// <param name="assetName">Base asset filename without extension.</param>
    /// <param name="displayName">Unity object name assigned to the new asset.</param>
    /// <returns>Created asset instance.</returns>
    internal static T CreateAsset<T>(string assetName, string displayName) where T : ScriptableObject
    {
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(PresetRoot + "/" + NormalizeAssetName(assetName) + ".asset");
        T asset = ScriptableObject.CreateInstance<T>();
        asset.name = displayName;
        AssetDatabase.CreateAsset(asset, assetPath);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    /// <summary>
    /// Copies serialized data between matching preset assets.
    /// </summary>
    /// <param name="source">Source asset.</param>
    /// <param name="target">Target asset.</param>
    internal static void CopyPresetData(ScriptableObject source, ScriptableObject target)
    {
        if (source == null || target == null)
            return;

        EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), target);
        EditorUtility.SetDirty(target);
    }

    /// <summary>
    /// Assigns a fresh editor preset identifier after duplicating serialized data from another asset.
    /// </summary>
    /// <param name="presetAsset">Preset asset that must receive a unique identifier.</param>
    internal static void RefreshPresetId(ScriptableObject presetAsset)
    {
        if (presetAsset == null)
            return;

        SerializedObject serializedObject = new SerializedObject(presetAsset);
        SerializedProperty presetIdProperty = serializedObject.FindProperty("presetId");

        if (presetIdProperty == null)
            return;

        presetIdProperty.stringValue = System.Guid.NewGuid().ToString("N");
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(presetAsset);
    }

    /// <summary>
    /// Deletes a linked preset only when it is not referenced by another master preset.
    /// </summary>
    /// <param name="linkedPreset">Linked preset candidate.</param>
    /// <param name="deletedMaster">Master preset being deleted.</param>
    /// <param name="allMasters">All current master presets.</param>
    private static void DeleteLinkedPresetIfUnshared(ScriptableObject linkedPreset,
                                                     ExcelDataTransferMasterPreset deletedMaster,
                                                     List<ExcelDataTransferMasterPreset> allMasters)
    {
        if (linkedPreset == null)
            return;

        for (int masterIndex = 0; masterIndex < allMasters.Count; masterIndex++)
        {
            ExcelDataTransferMasterPreset masterPreset = allMasters[masterIndex];

            if (masterPreset == null || masterPreset == deletedMaster)
                continue;

            if (masterPreset.LayoutPreset == linkedPreset ||
                masterPreset.BrushPalettePreset == linkedPreset ||
                masterPreset.ImportPreset == linkedPreset ||
                masterPreset.ExportPreset == linkedPreset)
                return;
        }

        DeleteAsset(linkedPreset);
    }

    /// <summary>
    /// Deletes one asset from the project if it has a valid asset path.
    /// </summary>
    /// <param name="asset">Asset to delete.</param>
    internal static void DeleteAsset(Object asset)
    {
        if (asset == null)
            return;

        string assetPath = AssetDatabase.GetAssetPath(asset);

        if (string.IsNullOrWhiteSpace(assetPath))
            return;

        AssetDatabase.DeleteAsset(assetPath);
    }

    /// <summary>
    /// Adds all asset paths of the provided type from the tool preset root.
    /// </summary>
    /// <typeparam name="T">Asset type to search.</typeparam>
    /// <param name="uniquePaths">Target set that receives project-relative asset paths.</param>
    private static void AddAssetPathsOfType<T>(HashSet<string> uniquePaths) where T : UnityEngine.Object
    {
        if (uniquePaths == null)
            return;

        string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new string[] { PresetRoot });

        for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);

            if (string.IsNullOrWhiteSpace(assetPath))
                continue;

            uniquePaths.Add(assetPath);
        }
    }
    #endregion

    #region Brush Defaults
    /// <summary>
    /// Creates an initial palette focused on fast filtering without overwriting authored brushes.
    /// </summary>
    /// <param name="brushPalettePreset">Palette that should receive default brushes when empty.</param>
    internal static void EnsureDefaultBrushes(ExcelDataBrushPalettePreset brushPalettePreset)
    {
        if (brushPalettePreset == null)
            return;

        List<ExcelDataBrushDefinition> brushes = brushPalettePreset.Brushes;

        AddBrushIfMissing(brushes,
                          "Any Field",
                          ExcelDataTransferDomain.All,
                          ExcelDataFieldCategory.All,
                          ExcelDataBrushDataKind.All,
                          ExcelDataListElementFilterMode.HideConcreteListElements,
                          string.Empty,
                          new Color(0.85f, 0.85f, 0.85f, 1f),
                          "all any field",
                          "Generic brush that opens the full field catalog.");
        AddBrushIfMissing(brushes,
                          "Numbers",
                          ExcelDataTransferDomain.All,
                          ExcelDataFieldCategory.All,
                          ExcelDataBrushDataKind.Number,
                          ExcelDataListElementFilterMode.HideConcreteListElements,
                          string.Empty,
                          new Color(0.45f, 0.68f, 1f, 1f),
                          "number float int stat scaling",
                          "Brush for numeric tuning values, stats and scaling-friendly fields.");
        AddBrushIfMissing(brushes,
                          "Booleans",
                          ExcelDataTransferDomain.All,
                          ExcelDataFieldCategory.All,
                          ExcelDataBrushDataKind.Boolean,
                          ExcelDataListElementFilterMode.HideConcreteListElements,
                          string.Empty,
                          new Color(0.48f, 0.86f, 0.58f, 1f),
                          "bool toggle enabled disabled",
                          "Brush for boolean toggle fields.");
        AddBrushIfMissing(brushes,
                          "Enums",
                          ExcelDataTransferDomain.All,
                          ExcelDataFieldCategory.All,
                          ExcelDataBrushDataKind.Enum,
                          ExcelDataListElementFilterMode.HideConcreteListElements,
                          string.Empty,
                          new Color(0.88f, 0.68f, 1f, 1f),
                          "enum mode type policy",
                          "Brush for enum mode and policy fields.");
        AddBrushIfMissing(brushes,
                          "Strings",
                          ExcelDataTransferDomain.All,
                          ExcelDataFieldCategory.All,
                          ExcelDataBrushDataKind.String,
                          ExcelDataListElementFilterMode.HideConcreteListElements,
                          string.Empty,
                          new Color(0.9f, 0.82f, 0.48f, 1f),
                          "string label id name description",
                          "Brush for text identifiers, labels and descriptions.");
        AddBrushIfMissing(brushes,
                          "References",
                          ExcelDataTransferDomain.All,
                          ExcelDataFieldCategory.Reference,
                          ExcelDataBrushDataKind.ObjectReference,
                          ExcelDataListElementFilterMode.HideConcreteListElements,
                          string.Empty,
                          new Color(0.45f, 0.7f, 1f, 1f),
                          "asset reference prefab material guid name",
                          "Brush for asset reference fields resolved through the project AssetDatabase.");
        AddBrushIfMissing(brushes,
                          "Lists",
                          ExcelDataTransferDomain.All,
                          ExcelDataFieldCategory.All,
                          ExcelDataBrushDataKind.ListElement,
                          ExcelDataListElementFilterMode.ListsOnly,
                          string.Empty,
                          new Color(0.45f, 1f, 0.65f, 1f),
                          "list array element concrete item wave cell",
                          "Brush for concrete list elements and nested list data.");
        AddBrushIfMissing(brushes,
                          "Waves",
                          ExcelDataTransferDomain.Waves,
                          ExcelDataFieldCategory.Wave,
                          ExcelDataBrushDataKind.All,
                          ExcelDataListElementFilterMode.All,
                          string.Empty,
                          new Color(1f, 0.74f, 0.36f, 1f),
                          "enemy wave painted cell spawn",
                          "Brush for EnemyWavePreset wave and painted-cell data.");
        AddBrushIfMissing(brushes,
                          "Export - Player Numbers",
                          ExcelDataTransferDomain.Player,
                          ExcelDataFieldCategory.All,
                          ExcelDataBrushDataKind.Number,
                          ExcelDataListElementFilterMode.HideConcreteListElements,
                          string.Empty,
                          new Color(0.34f, 0.62f, 1f, 1f),
                          "export player number tuning speed damage cooldown",
                          "Test brush for exporting readable player numeric tuning fields.");
        AddBrushIfMissing(brushes,
                          "Export - Enemy Waves",
                          ExcelDataTransferDomain.Waves,
                          ExcelDataFieldCategory.Wave,
                          ExcelDataBrushDataKind.All,
                          ExcelDataListElementFilterMode.All,
                          "Wave",
                          new Color(1f, 0.62f, 0.28f, 1f),
                          "export wave enemy spawn timing group",
                          "Test brush for exporting enemy wave and spawn-related data.");
        AddBrushIfMissing(brushes,
                          "Import - References",
                          ExcelDataTransferDomain.All,
                          ExcelDataFieldCategory.Reference,
                          ExcelDataBrushDataKind.ObjectReference,
                          ExcelDataListElementFilterMode.HideConcreteListElements,
                          string.Empty,
                          new Color(0.42f, 0.82f, 0.95f, 1f),
                          "import reference asset prefab guid name path",
                          "Test brush for importing asset references by readable asset name with ambiguity checks.");
        AddBrushIfMissing(brushes,
                          "Import - Lists",
                          ExcelDataTransferDomain.All,
                          ExcelDataFieldCategory.All,
                          ExcelDataBrushDataKind.ListElement,
                          ExcelDataListElementFilterMode.All,
                          string.Empty,
                          new Color(0.5f, 0.95f, 0.52f, 1f),
                          "import list concrete array element item",
                          "Test brush for importing concrete list elements and list-backed values.");
        brushPalettePreset.ValidateValues();
    }

    /// <summary>
    /// Creates one configured brush definition for the default palette.
    /// </summary>
    /// <param name="brushName">Readable brush name shown in the palette.</param>
    /// <param name="domain">Domain filter applied by this brush.</param>
    /// <param name="dataKind">Data-kind filter applied by this brush.</param>
    /// <param name="color">Grid overlay color used by this brush.</param>
    /// <param name="searchTokens">Search tokens used by smart filtering.</param>
    /// <param name="description">Short editor-only brush description.</param>
    /// <returns>Configured brush definition.</returns>
    private static void AddBrushIfMissing(List<ExcelDataBrushDefinition> brushes,
                                          string brushName,
                                          ExcelDataTransferDomain domain,
                                          ExcelDataFieldCategory category,
                                          ExcelDataBrushDataKind dataKind,
                                          ExcelDataListElementFilterMode listFilter,
                                          string sourceFilter,
                                          Color color,
                                          string searchTokens,
                                          string description)
    {
        if (BrushExists(brushes, brushName))
            return;

        brushes.Add(CreateBrush(brushName,
                                domain,
                                category,
                                dataKind,
                                listFilter,
                                sourceFilter,
                                color,
                                searchTokens,
                                description));
    }

    /// <summary>
    /// Checks whether a brush with the same readable name is already authored.
    /// </summary>
    /// <param name="brushes">Brush list to scan.</param>
    /// <param name="brushName">Brush name to find.</param>
    /// <returns>True when an existing brush uses the provided name.</returns>
    private static bool BrushExists(List<ExcelDataBrushDefinition> brushes, string brushName)
    {
        if (brushes == null)
            return false;

        for (int brushIndex = 0; brushIndex < brushes.Count; brushIndex++)
        {
            ExcelDataBrushDefinition brush = brushes[brushIndex];

            if (brush != null && brush.BrushName == brushName)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Creates one configured brush definition for the default palette.
    /// </summary>
    /// <param name="brushName">Readable brush name shown in the palette.</param>
    /// <param name="domain">Domain filter applied by this brush.</param>
    /// <param name="category">Category filter applied by this brush.</param>
    /// <param name="dataKind">Data-kind filter applied by this brush.</param>
    /// <param name="listFilter">List filter applied by this brush.</param>
    /// <param name="sourceFilter">Source type filter applied by this brush.</param>
    /// <param name="color">Grid overlay color used by this brush.</param>
    /// <param name="searchTokens">Search tokens used by smart filtering.</param>
    /// <param name="description">Short editor-only brush description.</param>
    /// <returns>Configured brush definition.</returns>
    private static ExcelDataBrushDefinition CreateBrush(string brushName,
                                                        ExcelDataTransferDomain domain,
                                                        ExcelDataFieldCategory category,
                                                        ExcelDataBrushDataKind dataKind,
                                                        ExcelDataListElementFilterMode listFilter,
                                                        string sourceFilter,
                                                        Color color,
                                                        string searchTokens,
                                                        string description)
    {
        ExcelDataBrushDefinition brush = new ExcelDataBrushDefinition();
        brush.Configure(brushName, domain, category, dataKind, listFilter, sourceFilter, color, searchTokens, description);
        return brush;
    }
    #endregion

    #endregion
}
