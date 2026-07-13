using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the editor-only field catalog from real serialized project assets without runtime reflection.
/// </summary>
internal static class ExcelDataFieldCatalogBuilder
{
    #region Constants
    private const string PlayerRoot = "Assets/Scriptable Objects/Player";
    private const string EnemyRoot = "Assets/Scriptable Objects/Enemy";
    private const string GameRoot = "Assets/Scriptable Objects/Game";
    private const string UnityListToken = ".Array.data[";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Scans supported ScriptableObject assets and returns brushable serialized fields.
    /// </summary>
    /// <returns>Catalog entries built from currently authored project assets.</returns>
    public static List<ExcelDataFieldCatalogEntry> BuildCatalog()
    {
        List<ExcelDataFieldCatalogEntry> entries = new List<ExcelDataFieldCatalogEntry>();
        List<ExcelDataFieldCatalogSourceDefinition> sources = BuildSourceDefinitions();

        for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
        {
            ExcelDataFieldCatalogSourceDefinition source = sources[sourceIndex];

            if (source == null || source.AssetType == null)
                continue;

            AddEntriesFromSource(entries, source);
        }

        return entries;
    }
    #endregion

    #region Source Discovery
    /// <summary>
    /// Creates the supported source list for the first implementation tranche.
    /// </summary>
    /// <returns>ScriptableObject type definitions scanned by the field catalog.</returns>
    private static List<ExcelDataFieldCatalogSourceDefinition> BuildSourceDefinitions()
    {
        List<ExcelDataFieldCatalogSourceDefinition> sources = new List<ExcelDataFieldCatalogSourceDefinition>();
        sources.Add(new ExcelDataFieldCatalogSourceDefinition(typeof(PlayerMasterPreset), ExcelDataTransferDomain.Player, PlayerRoot));
        sources.Add(new ExcelDataFieldCatalogSourceDefinition(typeof(PlayerControllerPreset), ExcelDataTransferDomain.Player, PlayerRoot));
        sources.Add(new ExcelDataFieldCatalogSourceDefinition(typeof(PlayerProgressionPreset), ExcelDataTransferDomain.Player, PlayerRoot));
        sources.Add(new ExcelDataFieldCatalogSourceDefinition(typeof(PlayerPowerUpsPreset), ExcelDataTransferDomain.Player, PlayerRoot));
        sources.Add(new ExcelDataFieldCatalogSourceDefinition(typeof(PlayerVisualPreset), ExcelDataTransferDomain.Player, PlayerRoot));
        sources.Add(new ExcelDataFieldCatalogSourceDefinition(typeof(PlayerUiVisualPreset), ExcelDataTransferDomain.Player, PlayerRoot));
        sources.Add(new ExcelDataFieldCatalogSourceDefinition(typeof(PlayerAnimationBindingsPreset), ExcelDataTransferDomain.Player, PlayerRoot));
        sources.Add(new ExcelDataFieldCatalogSourceDefinition(typeof(EnemyMasterPreset), ExcelDataTransferDomain.Enemy, EnemyRoot));
        sources.Add(new ExcelDataFieldCatalogSourceDefinition(typeof(EnemyBrainPreset), ExcelDataTransferDomain.Enemy, EnemyRoot));
        sources.Add(new ExcelDataFieldCatalogSourceDefinition(typeof(EnemyVisualPreset), ExcelDataTransferDomain.Enemy, EnemyRoot));
        sources.Add(new ExcelDataFieldCatalogSourceDefinition(typeof(EnemyUiVisualPreset), ExcelDataTransferDomain.Enemy, EnemyRoot));
        sources.Add(new ExcelDataFieldCatalogSourceDefinition(typeof(EnemyAdvancedPatternPreset), ExcelDataTransferDomain.Enemy, EnemyRoot));
        sources.Add(new ExcelDataFieldCatalogSourceDefinition(typeof(EnemyBossPatternPreset), ExcelDataTransferDomain.Enemy, EnemyRoot));
        sources.Add(new ExcelDataFieldCatalogSourceDefinition(typeof(EnemyModulesAndPatternsPreset), ExcelDataTransferDomain.Enemy, EnemyRoot));
        sources.Add(new ExcelDataFieldCatalogSourceDefinition(typeof(EnemyWavePreset), ExcelDataTransferDomain.Waves, EnemyRoot));
        sources.Add(new ExcelDataFieldCatalogSourceDefinition(typeof(GameMasterPreset), ExcelDataTransferDomain.Game, GameRoot));
        sources.Add(new ExcelDataFieldCatalogSourceDefinition(typeof(GameAudioManagerPreset), ExcelDataTransferDomain.Game, GameRoot));
        sources.Add(new ExcelDataFieldCatalogSourceDefinition(typeof(GameSettingsManagerPreset), ExcelDataTransferDomain.Game, GameRoot));
        sources.Add(new ExcelDataFieldCatalogSourceDefinition(typeof(GameHudManagerPreset), ExcelDataTransferDomain.Game, GameRoot));
        sources.Add(new ExcelDataFieldCatalogSourceDefinition(typeof(GameSceneManagerPreset), ExcelDataTransferDomain.Game, GameRoot));
        return sources;
    }

    /// <summary>
    /// Adds catalog entries for all assets matching one source definition.
    /// </summary>
    /// <param name="entries">Catalog list receiving discovered fields.</param>
    /// <param name="source">Source definition being scanned.</param>
    private static void AddEntriesFromSource(List<ExcelDataFieldCatalogEntry> entries,
                                             ExcelDataFieldCatalogSourceDefinition source)
    {
        string[] guids = AssetDatabase.FindAssets("t:" + source.AssetType.Name, new string[] { source.RootFolder });

        for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);

            if (string.IsNullOrWhiteSpace(assetPath))
                continue;

            UnityEngine.Object assetObject = AssetDatabase.LoadAssetAtPath(assetPath, source.AssetType);

            if (assetObject == null)
                continue;

            AddEntriesFromAsset(entries, source, assetObject, assetPath, guids[guidIndex]);
        }
    }

    /// <summary>
    /// Iterates one asset's SerializedObject and adds brushable properties to the catalog.
    /// </summary>
    /// <param name="entries">Catalog list receiving discovered fields.</param>
    /// <param name="source">Source definition owning the asset.</param>
    /// <param name="assetObject">Asset instance to scan.</param>
    /// <param name="assetPath">Project-relative asset path.</param>
    /// <param name="assetGuid">Asset GUID used by stable field identifiers.</param>
    private static void AddEntriesFromAsset(List<ExcelDataFieldCatalogEntry> entries,
                                            ExcelDataFieldCatalogSourceDefinition source,
                                            UnityEngine.Object assetObject,
                                            string assetPath,
                                            string assetGuid)
    {
        SerializedObject serializedObject = new SerializedObject(assetObject);
        SerializedProperty iterator = serializedObject.GetIterator();
        Dictionary<string, string> stableKeyCache = new Dictionary<string, string>(StringComparer.Ordinal);

        // Enter every visible serialized child so list elements and nested structs are individually discoverable.
        while (iterator.NextVisible(true))
        {
            if (ShouldSkipProperty(iterator))
                continue;

            SerializedProperty propertyCopy = iterator.Copy();

            if (!IsBrushableProperty(propertyCopy))
                continue;

            entries.Add(CreateEntry(source,
                                    assetObject,
                                    assetPath,
                                    assetGuid,
                                    serializedObject,
                                    stableKeyCache,
                                    propertyCopy));
        }
    }
    #endregion

    #region Entry Creation
    /// <summary>
    /// Creates one immutable catalog entry from a serialized property.
    /// </summary>
    /// <param name="source">Source definition owning the property.</param>
    /// <param name="assetObject">Asset instance that owns the property.</param>
    /// <param name="assetPath">Project-relative asset path.</param>
    /// <param name="assetGuid">Asset GUID used by stable field identifiers.</param>
    /// <param name="serializedObject">Serialized owner used to resolve stable list keys.</param>
    /// <param name="stableKeyCache">Per-asset stable list-key cache.</param>
    /// <param name="property">Serialized property copy used to infer metadata.</param>
    /// <returns>Catalog entry for the provided property.</returns>
    private static ExcelDataFieldCatalogEntry CreateEntry(ExcelDataFieldCatalogSourceDefinition source,
                                                          UnityEngine.Object assetObject,
                                                          string assetPath,
                                                          string assetGuid,
                                                          SerializedObject serializedObject,
                                                          IDictionary<string, string> stableKeyCache,
                                                          SerializedProperty property)
    {
        string serializedPath = property.propertyPath;
        string pathTemplate = BuildPathTemplate(serializedPath);
        int listDepth = CountListDepth(serializedPath);
        bool isConcreteListElement = listDepth > 0;
        bool isListContainer = IsListSizeProperty(property);
        ExcelDataBrushDataKind dataKind = ResolveDataKind(property);
        ExcelDataFieldCategory category = ResolveCategory(source.Domain, serializedPath, dataKind);
        string assetTypeName = source.AssetType.Name;
        string fieldId = source.Domain + ":" + assetGuid + ":" + assetTypeName + ":" + serializedPath;
        List<int> concreteListIndices;
        List<string> stableListKeys;
        string readablePath = ExcelDataListIdentityUtility.BuildReadablePath(serializedObject,
                                                                             serializedPath,
                                                                             stableKeyCache,
                                                                             out concreteListIndices,
                                                                             out stableListKeys);
        string displayName = assetObject.name + " / " + readablePath;
        string valueTypeName = ResolveValueTypeName(property);
        string searchText = BuildSearchText(source.Domain,
                                            category,
                                            dataKind,
                                            assetTypeName,
                                            assetObject.name,
                                            assetPath,
                                            serializedPath,
                                            pathTemplate,
                                            readablePath,
                                            valueTypeName,
                                            isConcreteListElement,
                                            listDepth,
                                            stableListKeys);

        return new ExcelDataFieldCatalogEntry(fieldId,
                                              source.Domain,
                                              category,
                                              dataKind,
                                              assetTypeName,
                                              assetObject.name,
                                              assetPath,
                                              serializedPath,
                                              pathTemplate,
                                              readablePath,
                                              displayName,
                                              valueTypeName,
                                              searchText,
                                              isConcreteListElement,
                                              isListContainer,
                                              listDepth,
                                              concreteListIndices,
                                              stableListKeys);
    }

    /// <summary>
    /// Builds compact lower-case search text consumed by the smart filter utility.
    /// </summary>
    /// <param name="domain">Management domain that owns the field.</param>
    /// <param name="category">Inferred field category.</param>
    /// <param name="dataKind">Inferred field data kind.</param>
    /// <param name="assetTypeName">Unity asset type name.</param>
    /// <param name="assetName">Unity asset display name.</param>
    /// <param name="assetPath">Project-relative asset path.</param>
    /// <param name="serializedPath">Concrete serialized property path.</param>
    /// <param name="pathTemplate">Tokenized serialized property path.</param>
    /// <param name="readablePath">Readable one-based serialized path.</param>
    /// <param name="valueTypeName">Readable value type name.</param>
    /// <param name="isConcreteListElement">True when the field belongs to a concrete list item.</param>
    /// <param name="listDepth">Nested list depth of the field path.</param>
    /// <param name="stableListKeys">Stable list keys discovered for the field.</param>
    /// <returns>Searchable text with domain, type, path and smart aliases.</returns>
    private static string BuildSearchText(ExcelDataTransferDomain domain,
                                          ExcelDataFieldCategory category,
                                          ExcelDataBrushDataKind dataKind,
                                          string assetTypeName,
                                          string assetName,
                                          string assetPath,
                                          string serializedPath,
                                          string pathTemplate,
                                          string readablePath,
                                          string valueTypeName,
                                          bool isConcreteListElement,
                                          int listDepth,
                                          IReadOnlyList<string> stableListKeys)
    {
        string aliases = string.Empty;

        if (dataKind == ExcelDataBrushDataKind.ObjectReference)
            aliases += " asset reference guid name";

        if (isConcreteListElement)
            aliases += " list array element concrete item";

        if (listDepth > 1)
            aliases += " nested list";

        string stableKeyText = ExcelDataListIdentityUtility.BuildStableKeySearchText(stableListKeys);

        string rawText = domain + " " + category + " " + dataKind + " " + assetTypeName + " " +
                         assetName + " " + assetPath + " " + serializedPath + " " +
                         pathTemplate + " " + readablePath + " " + stableKeyText + " " +
                         valueTypeName + aliases;
        return rawText.ToLowerInvariant();
    }
    #endregion

    #region Property Classification
    /// <summary>
    /// Checks whether a Unity serialized property should be skipped by the catalog.
    /// </summary>
    /// <param name="property">Serialized property to inspect.</param>
    /// <returns>True when the property is Unity infrastructure or otherwise unusable.</returns>
    private static bool ShouldSkipProperty(SerializedProperty property)
    {
        if (property == null)
            return true;

        if (property.propertyPath == "m_Script")
            return true;

        return string.IsNullOrWhiteSpace(property.propertyPath);
    }

    /// <summary>
    /// Checks whether a property can be exposed to brush filters in this tranche.
    /// </summary>
    /// <param name="property">Serialized property to inspect.</param>
    /// <returns>True when the property is a leaf, list container, list size or concrete list element.</returns>
    private static bool IsBrushableProperty(SerializedProperty property)
    {
        if (IsListSizeProperty(property))
            return true;

        // Generic containers serialize as Complex and are not directly useful to workbook users.
        return property.propertyType != SerializedPropertyType.Generic;
    }

    /// <summary>
    /// Checks whether a property is Unity's synthetic array size row.
    /// </summary>
    /// <param name="property">Serialized property to inspect.</param>
    /// <returns>True when the serialized path points to an array size value.</returns>
    private static bool IsListSizeProperty(SerializedProperty property)
    {
        if (property == null)
            return false;

        return property.propertyPath.EndsWith(".Array.size", StringComparison.Ordinal);
    }
    #endregion

    #region Value Classification
    /// <summary>
    /// Resolves the brush value kind from one serialized property.
    /// </summary>
    /// <param name="property">Serialized property to inspect.</param>
    /// <returns>Brush data kind used by filters and workbook formatting.</returns>
    private static ExcelDataBrushDataKind ResolveDataKind(SerializedProperty property)
    {
        if (IsListSizeProperty(property))
            return ExcelDataBrushDataKind.ListSize;

        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.Float:
                return ExcelDataBrushDataKind.Number;
            case SerializedPropertyType.Boolean:
                return ExcelDataBrushDataKind.Boolean;
            case SerializedPropertyType.Enum:
                return ExcelDataBrushDataKind.Enum;
            case SerializedPropertyType.String:
            case SerializedPropertyType.Character:
                return ExcelDataBrushDataKind.String;
            case SerializedPropertyType.ObjectReference:
                return ExcelDataBrushDataKind.ObjectReference;
            case SerializedPropertyType.Color:
                return ExcelDataBrushDataKind.Color;
            case SerializedPropertyType.Vector2:
            case SerializedPropertyType.Vector3:
            case SerializedPropertyType.Vector4:
            case SerializedPropertyType.Vector2Int:
            case SerializedPropertyType.Vector3Int:
                return ExcelDataBrushDataKind.Vector;
            case SerializedPropertyType.AnimationCurve:
                return ExcelDataBrushDataKind.Curve;
            default:
                return ExcelDataBrushDataKind.Primitive;
        }
    }

    /// <summary>
    /// Resolves a functional field category from domain, path tokens and value kind.
    /// </summary>
    /// <param name="domain">Management domain that owns the field.</param>
    /// <param name="serializedPath">Unity serialized property path.</param>
    /// <param name="dataKind">Brush data kind already inferred for the field.</param>
    /// <returns>Field category used by smart dropdown filters.</returns>
    private static ExcelDataFieldCategory ResolveCategory(ExcelDataTransferDomain domain,
                                                          string serializedPath,
                                                          ExcelDataBrushDataKind dataKind)
    {
        string normalizedPath = serializedPath.ToLowerInvariant().Replace("_", string.Empty);

        if (domain == ExcelDataTransferDomain.Waves || ContainsAny(normalizedPath, "wave", "paintedcells", "cellcoordinate", "spawn"))
            return ExcelDataFieldCategory.Wave;

        if (ContainsAny(normalizedPath, "presetid", "presetname", "description", "version"))
            return ExcelDataFieldCategory.Metadata;

        if (ContainsAny(normalizedPath, "scaling", "formula", "stat", "curve", "rank", "level"))
            return ExcelDataFieldCategory.Scaling;

        if (dataKind == ExcelDataBrushDataKind.ObjectReference)
            return ExcelDataFieldCategory.Reference;

        if (ContainsAny(normalizedPath, "audio", "fmod", "event"))
            return ExcelDataFieldCategory.Audio;

        if (ContainsAny(normalizedPath, "ui", "hud", "label", "widget", "portrait"))
            return ExcelDataFieldCategory.UserInterface;

        if (ContainsAny(normalizedPath, "visual", "material", "color", "prefab", "vfx", "animation", "shadow", "sprite"))
            return ExcelDataFieldCategory.Visual;

        if (ContainsAny(normalizedPath, "input", "action", "binding", "controller"))
            return ExcelDataFieldCategory.Input;

        return ExcelDataFieldCategory.Gameplay;
    }

    /// <summary>
    /// Resolves a readable value type label for catalog details.
    /// </summary>
    /// <param name="property">Serialized property to inspect.</param>
    /// <returns>Readable value type name.</returns>
    private static string ResolveValueTypeName(SerializedProperty property)
    {
        if (property == null)
            return "Unknown";

        if (!string.IsNullOrWhiteSpace(property.type))
            return property.type;

        return property.propertyType.ToString();
    }
    #endregion

    #region Path Helpers
    /// <summary>
    /// Replaces concrete Unity list indexes with reusable empty list tokens.
    /// </summary>
    /// <param name="serializedPath">Concrete serialized property path.</param>
    /// <returns>Tokenized path template used by reusable brush mappings.</returns>
    private static string BuildPathTemplate(string serializedPath)
    {
        if (string.IsNullOrWhiteSpace(serializedPath))
            return string.Empty;

        string template = serializedPath;
        int tokenIndex = template.IndexOf(UnityListToken, StringComparison.Ordinal);

        while (tokenIndex >= 0)
        {
            int numberStartIndex = tokenIndex + UnityListToken.Length;
            int numberEndIndex = template.IndexOf(']', numberStartIndex);

            if (numberEndIndex < 0)
                break;

            template = template.Substring(0, numberStartIndex) + "]" + template.Substring(numberEndIndex + 1);
            tokenIndex = template.IndexOf(UnityListToken, numberStartIndex + 1, StringComparison.Ordinal);
        }

        return template;
    }

    /// <summary>
    /// Counts concrete list scopes in a Unity serialized property path.
    /// </summary>
    /// <param name="serializedPath">Concrete serialized property path.</param>
    /// <returns>Number of concrete list scopes found in the path.</returns>
    private static int CountListDepth(string serializedPath)
    {
        if (string.IsNullOrWhiteSpace(serializedPath))
            return 0;

        int depth = 0;
        int tokenIndex = serializedPath.IndexOf(UnityListToken, StringComparison.Ordinal);

        while (tokenIndex >= 0)
        {
            depth++;
            tokenIndex = serializedPath.IndexOf(UnityListToken, tokenIndex + UnityListToken.Length, StringComparison.Ordinal);
        }

        return depth;
    }

    /// <summary>
    /// Checks whether normalized text contains any expected token.
    /// </summary>
    /// <param name="text">Lower-case normalized text.</param>
    /// <param name="tokens">Tokens to search.</param>
    /// <returns>True when at least one token is present.</returns>
    private static bool ContainsAny(string text, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
        {
            string token = tokens[tokenIndex];

            if (string.IsNullOrWhiteSpace(token))
                continue;

            if (text.Contains(token))
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
