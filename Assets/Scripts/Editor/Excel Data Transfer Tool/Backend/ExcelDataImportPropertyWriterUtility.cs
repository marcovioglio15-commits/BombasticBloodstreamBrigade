using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Parses coordinate-exact workbook values and writes supported SerializedProperty types during import.
/// </summary>
internal static class ExcelDataImportPropertyWriterUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Writes one raw workbook cell into a supported SerializedProperty using the active reference policy.
    /// </summary>
    /// <param name="property">Target serialized property.</param>
    /// <param name="cellValue">Coordinate-exact incoming value and hidden reference metadata.</param>
    /// <param name="importPreset">Import preset controlling reference resolution.</param>
    /// <param name="warning">Warning generated when the value cannot be written safely.</param>
    /// <returns>True when the pending SerializedObject value was written.</returns>
    public static bool TryWriteProperty(SerializedProperty property,
                                        ExcelDataImportCellValue cellValue,
                                        ExcelDataImportPreset importPreset,
                                        out string warning)
    {
        warning = string.Empty;

        if (property == null)
        {
            warning = "Missing target SerializedProperty.";
            return false;
        }

        if (cellValue == null)
        {
            warning = "Missing incoming workbook cell value.";
            return false;
        }

        if (property.propertyPath.EndsWith(".Array.size", StringComparison.Ordinal))
        {
            warning = "List Size import requires the explicit list policy introduced in the list-semantics tranche.";
            return false;
        }

        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                return TryWriteInteger(property, cellValue.ValueText, out warning);
            case SerializedPropertyType.Boolean:
                return TryWriteBoolean(property, cellValue.ValueText, out warning);
            case SerializedPropertyType.Float:
                return TryWriteFloat(property, cellValue.ValueText, out warning);
            case SerializedPropertyType.String:
            case SerializedPropertyType.Character:
                property.stringValue = cellValue.ValueText;
                return true;
            case SerializedPropertyType.Enum:
                return TryWriteEnum(property, cellValue.ValueText, out warning);
            case SerializedPropertyType.ObjectReference:
                return TryWriteObjectReference(property, cellValue, importPreset, out warning);
            case SerializedPropertyType.Color:
                return TryWriteColor(property, cellValue.ValueText, out warning);
            case SerializedPropertyType.Vector2:
                return TryWriteVector2(property, cellValue.ValueText, out warning);
            case SerializedPropertyType.Vector3:
                return TryWriteVector3(property, cellValue.ValueText, out warning);
            case SerializedPropertyType.Vector4:
                return TryWriteVector4(property, cellValue.ValueText, out warning);
            case SerializedPropertyType.Vector2Int:
                return TryWriteVector2Int(property, cellValue.ValueText, out warning);
            case SerializedPropertyType.Vector3Int:
                return TryWriteVector3Int(property, cellValue.ValueText, out warning);
            default:
                warning = "Unsupported import property type: " + property.propertyType;
                return false;
        }
    }
    #endregion

    #region Scalar Writers
    /// <summary>
    /// Writes a signed 64-bit integer so Unity long fields do not lose range during import.
    /// </summary>
    /// <param name="property">Target integer property.</param>
    /// <param name="value">Invariant workbook value.</param>
    /// <param name="warning">Warning generated when parsing fails.</param>
    /// <returns>True when the value was written.</returns>
    private static bool TryWriteInteger(SerializedProperty property, string value, out string warning)
    {
        long parsedValue;
        warning = string.Empty;

        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue))
        {
            warning = "Invalid integer value: " + value;
            return false;
        }

        property.longValue = parsedValue;
        return true;
    }

    /// <summary>
    /// Writes a boolean value using invariant Boolean text accepted by Excel export.
    /// </summary>
    /// <param name="property">Target boolean property.</param>
    /// <param name="value">Workbook value.</param>
    /// <param name="warning">Warning generated when parsing fails.</param>
    /// <returns>True when the value was written.</returns>
    private static bool TryWriteBoolean(SerializedProperty property, string value, out string warning)
    {
        bool parsedValue;
        warning = string.Empty;

        if (!bool.TryParse(value, out parsedValue))
        {
            warning = "Invalid boolean value: " + value;
            return false;
        }

        property.boolValue = parsedValue;
        return true;
    }

    /// <summary>
    /// Writes a double-precision serialized float value using invariant workbook text.
    /// </summary>
    /// <param name="property">Target float property.</param>
    /// <param name="value">Invariant workbook value.</param>
    /// <param name="warning">Warning generated when parsing fails.</param>
    /// <returns>True when the value was written.</returns>
    private static bool TryWriteFloat(SerializedProperty property, string value, out string warning)
    {
        double parsedValue;
        warning = string.Empty;

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue))
        {
            warning = "Invalid numeric value: " + value;
            return false;
        }

        property.doubleValue = parsedValue;
        return true;
    }

    /// <summary>
    /// Writes an enum by readable display name, serialized name, or controlled numeric index fallback.
    /// </summary>
    /// <param name="property">Target enum property.</param>
    /// <param name="value">Workbook enum value.</param>
    /// <param name="warning">Warning generated when no enum value matches.</param>
    /// <returns>True when the enum was written.</returns>
    private static bool TryWriteEnum(SerializedProperty property, string value, out string warning)
    {
        warning = string.Empty;

        for (int enumIndex = 0; enumIndex < property.enumDisplayNames.Length; enumIndex++)
        {
            if (!string.Equals(property.enumDisplayNames[enumIndex], value, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(property.enumNames[enumIndex], value, StringComparison.OrdinalIgnoreCase))
                continue;

            property.enumValueIndex = enumIndex;
            return true;
        }

        int parsedIndex;

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedIndex) &&
            parsedIndex >= 0 &&
            parsedIndex < property.enumDisplayNames.Length)
        {
            property.enumValueIndex = parsedIndex;
            return true;
        }

        warning = "Invalid enum value: " + value;
        return false;
    }
    #endregion

    #region Reference Writer
    /// <summary>
    /// Resolves and writes an object reference while treating an empty visible cell as an explicit clear.
    /// </summary>
    /// <param name="property">Target object-reference property.</param>
    /// <param name="cellValue">Visible name plus optional hidden GUID and path metadata.</param>
    /// <param name="importPreset">Import preset controlling resolver order and ambiguity policy.</param>
    /// <param name="warning">Warning generated when resolution fails or remains ambiguous.</param>
    /// <returns>True when the reference was written or cleared.</returns>
    private static bool TryWriteObjectReference(SerializedProperty property,
                                                ExcelDataImportCellValue cellValue,
                                                ExcelDataImportPreset importPreset,
                                                out string warning)
    {
        Object resolvedObject = ResolveReference(cellValue, importPreset, out warning);

        if (resolvedObject == null && !string.IsNullOrWhiteSpace(warning))
            return false;

        property.objectReferenceValue = resolvedObject;

        if (resolvedObject != null && property.objectReferenceValue != resolvedObject)
        {
            warning = "Resolved asset is incompatible with target reference type: " + resolvedObject.GetType().Name + ".";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves one visible reference value according to the configured deterministic fallback order.
    /// </summary>
    /// <param name="cellValue">Visible name plus optional hidden GUID and path metadata.</param>
    /// <param name="importPreset">Import preset controlling resolution order.</param>
    /// <param name="warning">Warning generated when resolution fails or is ambiguous.</param>
    /// <returns>Resolved project asset, or null for an explicit empty-cell clear.</returns>
    private static Object ResolveReference(ExcelDataImportCellValue cellValue,
                                           ExcelDataImportPreset importPreset,
                                           out string warning)
    {
        warning = string.Empty;

        if (string.IsNullOrWhiteSpace(cellValue.ValueText))
            return null;

        switch (importPreset.ReferenceResolutionMode)
        {
            case ExcelDataReferenceResolutionMode.AssetPath:
                return ResolveReferenceByPath(cellValue.ReferencePath, out warning);
            case ExcelDataReferenceResolutionMode.GuidThenAssetName:
                return ResolveReferenceByGuidThenName(cellValue, importPreset, out warning);
            case ExcelDataReferenceResolutionMode.AssetNameThenGuid:
                return ResolveReferenceByNameThenGuid(cellValue, importPreset, out warning);
            default:
                return ResolveReferenceByName(cellValue.ValueText, importPreset.BlockAmbiguousReferences, out warning);
        }
    }

    /// <summary>
    /// Resolves a hidden GUID first and falls back to the visible asset name.
    /// </summary>
    /// <param name="cellValue">Incoming reference value.</param>
    /// <param name="importPreset">Import preset controlling ambiguity policy.</param>
    /// <param name="warning">Warning generated when both identities fail.</param>
    /// <returns>Resolved asset, or null.</returns>
    private static Object ResolveReferenceByGuidThenName(ExcelDataImportCellValue cellValue,
                                                         ExcelDataImportPreset importPreset,
                                                         out string warning)
    {
        Object resolvedObject = ResolveReferenceByGuid(cellValue.ReferenceGuid);

        if (resolvedObject != null)
        {
            warning = string.Empty;
            return resolvedObject;
        }

        return ResolveReferenceByName(cellValue.ValueText, importPreset.BlockAmbiguousReferences, out warning);
    }

    /// <summary>
    /// Resolves the visible asset name first and uses the hidden GUID only when the name is missing.
    /// </summary>
    /// <param name="cellValue">Incoming reference value.</param>
    /// <param name="importPreset">Import preset controlling ambiguity policy.</param>
    /// <param name="warning">Warning generated when identities fail or the name is ambiguous.</param>
    /// <returns>Resolved asset, or null.</returns>
    private static Object ResolveReferenceByNameThenGuid(ExcelDataImportCellValue cellValue,
                                                         ExcelDataImportPreset importPreset,
                                                         out string warning)
    {
        Object resolvedObject = ResolveReferenceByName(cellValue.ValueText,
                                                       importPreset.BlockAmbiguousReferences,
                                                       out warning);

        if (resolvedObject != null || warning.StartsWith("Ambiguous", StringComparison.Ordinal))
            return resolvedObject;

        resolvedObject = ResolveReferenceByGuid(cellValue.ReferenceGuid);

        if (resolvedObject != null)
            warning = string.Empty;

        return resolvedObject;
    }

    /// <summary>
    /// Resolves a project asset by exact visible name and reports duplicate-name ambiguity.
    /// </summary>
    /// <param name="referenceName">Exact visible asset name.</param>
    /// <param name="blockAmbiguousReferences">True when multiple exact matches block import.</param>
    /// <param name="warning">Warning generated for missing or ambiguous names.</param>
    /// <returns>Resolved asset, or null.</returns>
    private static Object ResolveReferenceByName(string referenceName,
                                                 bool blockAmbiguousReferences,
                                                 out string warning)
    {
        warning = string.Empty;
        string[] guids = AssetDatabase.FindAssets(referenceName);
        Object matchedObject = null;
        int exactMatches = 0;

        // Count exact object-name matches because AssetDatabase.FindAssets also returns fuzzy matches.
        for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);
            Object candidate = AssetDatabase.LoadAssetAtPath<Object>(assetPath);

            if (candidate == null || candidate.name != referenceName)
                continue;

            matchedObject = candidate;
            exactMatches++;
        }

        if (exactMatches == 1)
            return matchedObject;

        if (exactMatches > 1 && blockAmbiguousReferences)
        {
            warning = "Ambiguous asset reference name: " + referenceName + ".";
            return null;
        }

        warning = exactMatches > 1 ? string.Empty : "Asset reference name not found: " + referenceName + ".";
        return matchedObject;
    }

    /// <summary>
    /// Resolves a project asset by GUID.
    /// </summary>
    /// <param name="referenceGuid">Project asset GUID.</param>
    /// <returns>Resolved asset, or null.</returns>
    private static Object ResolveReferenceByGuid(string referenceGuid)
    {
        if (string.IsNullOrWhiteSpace(referenceGuid))
            return null;

        string assetPath = AssetDatabase.GUIDToAssetPath(referenceGuid);
        return string.IsNullOrWhiteSpace(assetPath) ? null : AssetDatabase.LoadAssetAtPath<Object>(assetPath);
    }

    /// <summary>
    /// Resolves a project asset by the hidden project-relative path.
    /// </summary>
    /// <param name="referencePath">Project-relative asset path.</param>
    /// <param name="warning">Warning generated when the path is empty or missing.</param>
    /// <returns>Resolved asset, or null.</returns>
    private static Object ResolveReferenceByPath(string referencePath, out string warning)
    {
        warning = string.Empty;

        if (string.IsNullOrWhiteSpace(referencePath))
        {
            warning = "Reference path metadata is empty.";
            return null;
        }

        Object resolvedObject = AssetDatabase.LoadAssetAtPath<Object>(referencePath);
        warning = resolvedObject == null ? "Reference path not found: " + referencePath + "." : string.Empty;
        return resolvedObject;
    }
    #endregion

    #region Structured Writers
    /// <summary>
    /// Writes a color formatted as comma-separated RGBA components.
    /// </summary>
    /// <param name="property">Target color property.</param>
    /// <param name="value">Workbook component text.</param>
    /// <param name="warning">Warning generated when parsing fails.</param>
    /// <returns>True when the value was written.</returns>
    private static bool TryWriteColor(SerializedProperty property, string value, out string warning)
    {
        float[] components = ParseFloatComponents(value, 4, out warning);

        if (components == null)
            return false;

        property.colorValue = new Color(components[0], components[1], components[2], components[3]);
        return true;
    }

    /// <summary>
    /// Writes a Vector2 formatted as comma-separated components.
    /// </summary>
    /// <param name="property">Target vector property.</param>
    /// <param name="value">Workbook component text.</param>
    /// <param name="warning">Warning generated when parsing fails.</param>
    /// <returns>True when the value was written.</returns>
    private static bool TryWriteVector2(SerializedProperty property, string value, out string warning)
    {
        float[] components = ParseFloatComponents(value, 2, out warning);

        if (components == null)
            return false;

        property.vector2Value = new Vector2(components[0], components[1]);
        return true;
    }

    /// <summary>
    /// Writes a Vector3 formatted as comma-separated components.
    /// </summary>
    /// <param name="property">Target vector property.</param>
    /// <param name="value">Workbook component text.</param>
    /// <param name="warning">Warning generated when parsing fails.</param>
    /// <returns>True when the value was written.</returns>
    private static bool TryWriteVector3(SerializedProperty property, string value, out string warning)
    {
        float[] components = ParseFloatComponents(value, 3, out warning);

        if (components == null)
            return false;

        property.vector3Value = new Vector3(components[0], components[1], components[2]);
        return true;
    }

    /// <summary>
    /// Writes a Vector4 formatted as comma-separated components.
    /// </summary>
    /// <param name="property">Target vector property.</param>
    /// <param name="value">Workbook component text.</param>
    /// <param name="warning">Warning generated when parsing fails.</param>
    /// <returns>True when the value was written.</returns>
    private static bool TryWriteVector4(SerializedProperty property, string value, out string warning)
    {
        float[] components = ParseFloatComponents(value, 4, out warning);

        if (components == null)
            return false;

        property.vector4Value = new Vector4(components[0], components[1], components[2], components[3]);
        return true;
    }

    /// <summary>
    /// Writes a Vector2Int formatted as comma-separated components.
    /// </summary>
    /// <param name="property">Target integer vector property.</param>
    /// <param name="value">Workbook component text.</param>
    /// <param name="warning">Warning generated when parsing fails.</param>
    /// <returns>True when the value was written.</returns>
    private static bool TryWriteVector2Int(SerializedProperty property, string value, out string warning)
    {
        int[] components = ParseIntComponents(value, 2, out warning);

        if (components == null)
            return false;

        property.vector2IntValue = new Vector2Int(components[0], components[1]);
        return true;
    }

    /// <summary>
    /// Writes a Vector3Int formatted as comma-separated components.
    /// </summary>
    /// <param name="property">Target integer vector property.</param>
    /// <param name="value">Workbook component text.</param>
    /// <param name="warning">Warning generated when parsing fails.</param>
    /// <returns>True when the value was written.</returns>
    private static bool TryWriteVector3Int(SerializedProperty property, string value, out string warning)
    {
        int[] components = ParseIntComponents(value, 3, out warning);

        if (components == null)
            return false;

        property.vector3IntValue = new Vector3Int(components[0], components[1], components[2]);
        return true;
    }
    #endregion

    #region Parsing
    /// <summary>
    /// Parses a fixed number of comma-separated invariant float components.
    /// </summary>
    /// <param name="value">Workbook component text.</param>
    /// <param name="expectedCount">Required component count.</param>
    /// <param name="warning">Warning generated when parsing fails.</param>
    /// <returns>Parsed components, or null.</returns>
    private static float[] ParseFloatComponents(string value, int expectedCount, out string warning)
    {
        string[] parts = SplitComponents(value, expectedCount, out warning);

        if (parts == null)
            return null;

        float[] components = new float[expectedCount];

        for (int componentIndex = 0; componentIndex < expectedCount; componentIndex++)
        {
            if (float.TryParse(parts[componentIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out components[componentIndex]))
                continue;

            warning = "Invalid numeric component: " + parts[componentIndex] + ".";
            return null;
        }

        return components;
    }

    /// <summary>
    /// Parses a fixed number of comma-separated invariant integer components.
    /// </summary>
    /// <param name="value">Workbook component text.</param>
    /// <param name="expectedCount">Required component count.</param>
    /// <param name="warning">Warning generated when parsing fails.</param>
    /// <returns>Parsed components, or null.</returns>
    private static int[] ParseIntComponents(string value, int expectedCount, out string warning)
    {
        string[] parts = SplitComponents(value, expectedCount, out warning);

        if (parts == null)
            return null;

        int[] components = new int[expectedCount];

        for (int componentIndex = 0; componentIndex < expectedCount; componentIndex++)
        {
            if (int.TryParse(parts[componentIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out components[componentIndex]))
                continue;

            warning = "Invalid integer component: " + parts[componentIndex] + ".";
            return null;
        }

        return components;
    }

    /// <summary>
    /// Splits comma-separated component text and validates its exact length.
    /// </summary>
    /// <param name="value">Workbook component text.</param>
    /// <param name="expectedCount">Required component count.</param>
    /// <param name="warning">Warning generated when validation fails.</param>
    /// <returns>Trimmed components, or null.</returns>
    private static string[] SplitComponents(string value, int expectedCount, out string warning)
    {
        warning = string.Empty;
        string[] parts = (value ?? string.Empty).Split(',');

        if (parts.Length != expectedCount)
        {
            warning = "Expected " + expectedCount.ToString(CultureInfo.InvariantCulture) +
                      " components, got " + parts.Length.ToString(CultureInfo.InvariantCulture) + ".";
            return null;
        }

        for (int componentIndex = 0; componentIndex < parts.Length; componentIndex++)
            parts[componentIndex] = parts[componentIndex].Trim();

        return parts;
    }
    #endregion

    #endregion
}
