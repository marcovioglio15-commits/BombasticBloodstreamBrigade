using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Writes workbook values into supported SerializedProperty types during Excel import apply.
/// </summary>
internal static class ExcelDataImportPropertyWriterUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Writes one workbook value into a supported SerializedProperty type.
    /// </summary>
    /// <param name="property">Target serialized property.</param>
    /// <param name="workbookRow">Workbook row carrying the incoming value.</param>
    /// <param name="importPreset">Import preset controlling reference resolution.</param>
    /// <param name="warning">Warning generated when the value cannot be written.</param>
    /// <returns>True when the value was written.</returns>
    public static bool TryWriteProperty(SerializedProperty property,
                                        ExcelDataWorkbookRow workbookRow,
                                        ExcelDataImportPreset importPreset,
                                        out string warning)
    {
        warning = string.Empty;

        if (property.propertyPath.EndsWith(".Array.size", StringComparison.Ordinal))
            return TryWriteArraySize(property, workbookRow.Value, out warning);

        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                return TryWriteInteger(property, workbookRow.Value, out warning);
            case SerializedPropertyType.Boolean:
                return TryWriteBoolean(property, workbookRow.Value, out warning);
            case SerializedPropertyType.Float:
                return TryWriteFloat(property, workbookRow.Value, out warning);
            case SerializedPropertyType.String:
            case SerializedPropertyType.Character:
                property.stringValue = workbookRow.Value ?? string.Empty;
                return true;
            case SerializedPropertyType.Enum:
                return TryWriteEnum(property, workbookRow.Value, out warning);
            case SerializedPropertyType.ObjectReference:
                return TryWriteObjectReference(property, workbookRow, importPreset, out warning);
            case SerializedPropertyType.Color:
                return TryWriteColor(property, workbookRow.Value, out warning);
            case SerializedPropertyType.Vector2:
                return TryWriteVector2(property, workbookRow.Value, out warning);
            case SerializedPropertyType.Vector3:
                return TryWriteVector3(property, workbookRow.Value, out warning);
            case SerializedPropertyType.Vector4:
                return TryWriteVector4(property, workbookRow.Value, out warning);
            case SerializedPropertyType.Vector2Int:
                return TryWriteVector2Int(property, workbookRow.Value, out warning);
            case SerializedPropertyType.Vector3Int:
                return TryWriteVector3Int(property, workbookRow.Value, out warning);
            default:
                warning = "Unsupported import property type: " + property.propertyType;
                return false;
        }
    }
    #endregion

    #region Value Writers
    /// <summary>
    /// Writes an integer value to a serialized property.
    /// </summary>
    /// <param name="property">Target property.</param>
    /// <param name="value">Workbook value.</param>
    /// <param name="warning">Warning generated when parsing fails.</param>
    /// <returns>True when the value was written.</returns>
    private static bool TryWriteInteger(SerializedProperty property, string value, out string warning)
    {
        int parsedValue;
        warning = string.Empty;

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue))
        {
            warning = "Invalid integer value: " + value;
            return false;
        }

        property.intValue = parsedValue;
        return true;
    }

    /// <summary>
    /// Writes a boolean value to a serialized property.
    /// </summary>
    /// <param name="property">Target property.</param>
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
    /// Writes a float value to a serialized property.
    /// </summary>
    /// <param name="property">Target property.</param>
    /// <param name="value">Workbook value.</param>
    /// <param name="warning">Warning generated when parsing fails.</param>
    /// <returns>True when the value was written.</returns>
    private static bool TryWriteFloat(SerializedProperty property, string value, out string warning)
    {
        float parsedValue;
        warning = string.Empty;

        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue))
        {
            warning = "Invalid float value: " + value;
            return false;
        }

        property.floatValue = parsedValue;
        return true;
    }

    /// <summary>
    /// Writes an enum value using display name, enum name, or numeric index.
    /// </summary>
    /// <param name="property">Target enum property.</param>
    /// <param name="value">Workbook value.</param>
    /// <param name="warning">Warning generated when no enum value matches.</param>
    /// <returns>True when the enum was written.</returns>
    private static bool TryWriteEnum(SerializedProperty property, string value, out string warning)
    {
        warning = string.Empty;

        for (int enumIndex = 0; enumIndex < property.enumDisplayNames.Length; enumIndex++)
        {
            if (string.Equals(property.enumDisplayNames[enumIndex], value, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(property.enumNames[enumIndex], value, StringComparison.OrdinalIgnoreCase))
            {
                property.enumValueIndex = enumIndex;
                return true;
            }
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

    /// <summary>
    /// Writes an object reference value after resolving workbook reference metadata.
    /// </summary>
    /// <param name="property">Target object-reference property.</param>
    /// <param name="workbookRow">Workbook row containing reference metadata.</param>
    /// <param name="importPreset">Import preset controlling reference resolution.</param>
    /// <param name="warning">Warning generated when the reference cannot be resolved safely.</param>
    /// <returns>True when the reference was written.</returns>
    private static bool TryWriteObjectReference(SerializedProperty property,
                                                ExcelDataWorkbookRow workbookRow,
                                                ExcelDataImportPreset importPreset,
                                                out string warning)
    {
        Object resolvedObject = ResolveReference(workbookRow, importPreset, out warning);

        if (resolvedObject == null && !string.IsNullOrWhiteSpace(warning))
            return false;

        property.objectReferenceValue = resolvedObject;
        return true;
    }

    /// <summary>
    /// Writes a color value formatted as comma-separated RGBA components.
    /// </summary>
    /// <param name="property">Target color property.</param>
    /// <param name="value">Workbook value.</param>
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
    /// Writes a Vector2 value formatted as comma-separated components.
    /// </summary>
    /// <param name="property">Target vector property.</param>
    /// <param name="value">Workbook value.</param>
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
    /// Writes a Vector3 value formatted as comma-separated components.
    /// </summary>
    /// <param name="property">Target vector property.</param>
    /// <param name="value">Workbook value.</param>
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
    /// Writes a Vector4 value formatted as comma-separated components.
    /// </summary>
    /// <param name="property">Target vector property.</param>
    /// <param name="value">Workbook value.</param>
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
    /// Writes a Vector2Int value formatted as comma-separated components.
    /// </summary>
    /// <param name="property">Target vector property.</param>
    /// <param name="value">Workbook value.</param>
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
    /// Writes a Vector3Int value formatted as comma-separated components.
    /// </summary>
    /// <param name="property">Target vector property.</param>
    /// <param name="value">Workbook value.</param>
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

    /// <summary>
    /// Writes an array size value.
    /// </summary>
    /// <param name="property">Target array-size property.</param>
    /// <param name="value">Workbook value.</param>
    /// <param name="warning">Warning generated when parsing fails.</param>
    /// <returns>True when the value was written.</returns>
    private static bool TryWriteArraySize(SerializedProperty property, string value, out string warning)
    {
        int parsedValue;
        warning = string.Empty;

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue) || parsedValue < 0)
        {
            warning = "Invalid array size value: " + value;
            return false;
        }

        property.intValue = parsedValue;
        return true;
    }
    #endregion

    #region Reference Resolution
    /// <summary>
    /// Resolves a workbook object reference using the import preset policy.
    /// </summary>
    /// <param name="workbookRow">Workbook row containing reference metadata.</param>
    /// <param name="importPreset">Import preset controlling resolution order.</param>
    /// <param name="warning">Warning generated when resolution fails or is ambiguous.</param>
    /// <returns>Resolved project asset, or null for empty references.</returns>
    private static Object ResolveReference(ExcelDataWorkbookRow workbookRow,
                                           ExcelDataImportPreset importPreset,
                                           out string warning)
    {
        warning = string.Empty;

        switch (importPreset.ReferenceResolutionMode)
        {
            case ExcelDataReferenceResolutionMode.AssetPath:
                return ResolveReferenceByPath(workbookRow.ReferencePath, out warning);
            case ExcelDataReferenceResolutionMode.GuidThenAssetName:
                return ResolveReferenceByGuidThenName(workbookRow, importPreset, out warning);
            case ExcelDataReferenceResolutionMode.AssetNameThenGuid:
                return ResolveReferenceByNameThenGuid(workbookRow, importPreset, out warning);
            default:
                return ResolveReferenceByName(GetReferenceName(workbookRow), importPreset.BlockAmbiguousReferences, out warning);
        }
    }

    /// <summary>
    /// Resolves a reference by GUID before falling back to asset name.
    /// </summary>
    /// <param name="workbookRow">Workbook row containing reference metadata.</param>
    /// <param name="importPreset">Import preset controlling ambiguity policy.</param>
    /// <param name="warning">Warning generated when resolution fails.</param>
    /// <returns>Resolved asset, or null for empty references.</returns>
    private static Object ResolveReferenceByGuidThenName(ExcelDataWorkbookRow workbookRow,
                                                         ExcelDataImportPreset importPreset,
                                                         out string warning)
    {
        Object resolvedObject = ResolveReferenceByGuid(workbookRow.ReferenceGuid);

        if (resolvedObject != null)
        {
            warning = string.Empty;
            return resolvedObject;
        }

        return ResolveReferenceByName(GetReferenceName(workbookRow), importPreset.BlockAmbiguousReferences, out warning);
    }

    /// <summary>
    /// Resolves a reference by asset name before falling back to GUID.
    /// </summary>
    /// <param name="workbookRow">Workbook row containing reference metadata.</param>
    /// <param name="importPreset">Import preset controlling ambiguity policy.</param>
    /// <param name="warning">Warning generated when resolution fails.</param>
    /// <returns>Resolved asset, or null for empty references.</returns>
    private static Object ResolveReferenceByNameThenGuid(ExcelDataWorkbookRow workbookRow,
                                                         ExcelDataImportPreset importPreset,
                                                         out string warning)
    {
        Object resolvedObject = ResolveReferenceByName(GetReferenceName(workbookRow), importPreset.BlockAmbiguousReferences, out warning);

        if (resolvedObject != null || !string.IsNullOrWhiteSpace(warning))
            return resolvedObject;

        resolvedObject = ResolveReferenceByGuid(workbookRow.ReferenceGuid);
        warning = resolvedObject == null && !string.IsNullOrWhiteSpace(workbookRow.ReferenceGuid) ? "Reference GUID not found." : string.Empty;
        return resolvedObject;
    }

    /// <summary>
    /// Resolves a reference by exact project asset name.
    /// </summary>
    /// <param name="referenceName">Reference asset name.</param>
    /// <param name="blockAmbiguousReferences">True when multiple exact name matches should block import.</param>
    /// <param name="warning">Warning generated when resolution fails or is ambiguous.</param>
    /// <returns>Resolved asset, or null for empty references.</returns>
    private static Object ResolveReferenceByName(string referenceName, bool blockAmbiguousReferences, out string warning)
    {
        warning = string.Empty;

        if (string.IsNullOrWhiteSpace(referenceName))
            return null;

        string[] guids = AssetDatabase.FindAssets(referenceName);
        Object matchedObject = null;
        int exactMatches = 0;

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
            warning = "Ambiguous asset reference name: " + referenceName;
            return null;
        }

        warning = exactMatches > 1 ? string.Empty : "Asset reference name not found: " + referenceName;
        return matchedObject;
    }

    /// <summary>
    /// Resolves a reference by GUID.
    /// </summary>
    /// <param name="referenceGuid">Reference asset GUID.</param>
    /// <returns>Resolved asset, or null when not found.</returns>
    private static Object ResolveReferenceByGuid(string referenceGuid)
    {
        if (string.IsNullOrWhiteSpace(referenceGuid))
            return null;

        string assetPath = AssetDatabase.GUIDToAssetPath(referenceGuid);
        return string.IsNullOrWhiteSpace(assetPath) ? null : AssetDatabase.LoadAssetAtPath<Object>(assetPath);
    }

    /// <summary>
    /// Resolves a reference by project-relative asset path.
    /// </summary>
    /// <param name="referencePath">Reference asset path.</param>
    /// <param name="warning">Warning generated when the path does not resolve.</param>
    /// <returns>Resolved asset, or null for empty references.</returns>
    private static Object ResolveReferenceByPath(string referencePath, out string warning)
    {
        warning = string.Empty;

        if (string.IsNullOrWhiteSpace(referencePath))
            return null;

        Object resolvedObject = AssetDatabase.LoadAssetAtPath<Object>(referencePath);
        warning = resolvedObject == null ? "Reference path not found: " + referencePath : string.Empty;
        return resolvedObject;
    }

    /// <summary>
    /// Gets the best available asset name from workbook reference columns.
    /// </summary>
    /// <param name="workbookRow">Workbook row containing reference metadata.</param>
    /// <returns>Reference asset name, or empty.</returns>
    private static string GetReferenceName(ExcelDataWorkbookRow workbookRow)
    {
        if (!string.IsNullOrWhiteSpace(workbookRow.ReferenceName))
            return workbookRow.ReferenceName;

        return workbookRow.Value;
    }
    #endregion

    #region Parsing
    /// <summary>
    /// Parses comma-separated float components.
    /// </summary>
    /// <param name="value">Workbook value.</param>
    /// <param name="expectedCount">Expected component count.</param>
    /// <param name="warning">Warning generated when parsing fails.</param>
    /// <returns>Parsed component array, or null.</returns>
    private static float[] ParseFloatComponents(string value, int expectedCount, out string warning)
    {
        warning = string.Empty;
        string[] parts = SplitComponents(value, expectedCount, out warning);

        if (parts == null)
            return null;

        float[] components = new float[expectedCount];

        for (int componentIndex = 0; componentIndex < expectedCount; componentIndex++)
        {
            if (float.TryParse(parts[componentIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out components[componentIndex]))
                continue;

            warning = "Invalid numeric component: " + parts[componentIndex];
            return null;
        }

        return components;
    }

    /// <summary>
    /// Parses comma-separated integer components.
    /// </summary>
    /// <param name="value">Workbook value.</param>
    /// <param name="expectedCount">Expected component count.</param>
    /// <param name="warning">Warning generated when parsing fails.</param>
    /// <returns>Parsed component array, or null.</returns>
    private static int[] ParseIntComponents(string value, int expectedCount, out string warning)
    {
        warning = string.Empty;
        string[] parts = SplitComponents(value, expectedCount, out warning);

        if (parts == null)
            return null;

        int[] components = new int[expectedCount];

        for (int componentIndex = 0; componentIndex < expectedCount; componentIndex++)
        {
            if (int.TryParse(parts[componentIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out components[componentIndex]))
                continue;

            warning = "Invalid integer component: " + parts[componentIndex];
            return null;
        }

        return components;
    }

    /// <summary>
    /// Splits a comma-separated component value and validates its length.
    /// </summary>
    /// <param name="value">Workbook value.</param>
    /// <param name="expectedCount">Expected component count.</param>
    /// <param name="warning">Warning generated when validation fails.</param>
    /// <returns>Trimmed components, or null.</returns>
    private static string[] SplitComponents(string value, int expectedCount, out string warning)
    {
        warning = string.Empty;
        string[] parts = (value ?? string.Empty).Split(',');

        if (parts.Length != expectedCount)
        {
            warning = "Expected " + expectedCount.ToString(CultureInfo.InvariantCulture) + " components, got " + parts.Length.ToString(CultureInfo.InvariantCulture) + ".";
            return null;
        }

        for (int componentIndex = 0; componentIndex < parts.Length; componentIndex++)
            parts[componentIndex] = parts[componentIndex].Trim();

        return parts;
    }
    #endregion

    #endregion
}
