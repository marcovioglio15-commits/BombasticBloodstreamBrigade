using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reads editor-only SerializedProperty values as typed workbook values without runtime reflection.
/// </summary>
internal static class ExcelDataSerializedValueReader
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Reads one grid-authoritative field binding directly from its owning Unity asset.
    /// </summary>
    /// <param name="binding">Stable asset and SerializedProperty identity authored by the workbook layout.</param>
    /// <param name="writeAssetNames">True when object-reference cells should show readable asset names.</param>
    /// <param name="writeReferenceGuids">True when reference GUIDs should be retained in technical metadata.</param>
    /// <param name="writeReferencePaths">True when reference paths should be retained in technical metadata.</param>
    /// <returns>Typed value snapshot plus any reference metadata or cell-local warning.</returns>
    public static ExcelDataSerializedValueSnapshot ReadValue(ExcelDataFieldBinding binding,
                                                             bool writeAssetNames,
                                                             bool writeReferenceGuids,
                                                             bool writeReferencePaths)
    {
        if (binding == null || !binding.IsUsable())
            return ExcelDataSerializedValueSnapshot.CreateWarning("Missing or unusable field binding.", string.Empty);

        string resolvedOwnerAssetPath = ResolveOwnerAssetPath(binding);

        if (string.IsNullOrWhiteSpace(resolvedOwnerAssetPath))
            return ExcelDataSerializedValueSnapshot.CreateWarning("Owner asset could not be resolved from GUID or stored path.", string.Empty);

        UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(resolvedOwnerAssetPath);

        if (asset == null)
            return ExcelDataSerializedValueSnapshot.CreateWarning("Missing owner asset at path: " + resolvedOwnerAssetPath, resolvedOwnerAssetPath);

        if (string.IsNullOrWhiteSpace(binding.SerializedPath))
            return ExcelDataSerializedValueSnapshot.CreateWarning("Binding has no concrete serialized property path.", resolvedOwnerAssetPath);

        SerializedObject serializedObject = new SerializedObject(asset);
        SerializedProperty property = serializedObject.FindProperty(binding.SerializedPath);

        if (property == null)
            return ExcelDataSerializedValueSnapshot.CreateWarning("Missing serialized property: " + binding.SerializedPath, resolvedOwnerAssetPath);

        return ReadPropertyValue(property,
                                 resolvedOwnerAssetPath,
                                 writeAssetNames,
                                 writeReferenceGuids,
                                 writeReferencePaths);
    }
    #endregion

    #region Property Reading
    /// <summary>
    /// Converts one SerializedProperty into a typed workbook value plus optional reference metadata.
    /// </summary>
    /// <param name="property">Serialized property resolved from the owner asset.</param>
    /// <param name="resolvedOwnerAssetPath">Current owner asset path recorded in technical metadata.</param>
    /// <param name="writeAssetNames">True when object references should show readable asset names.</param>
    /// <param name="writeReferenceGuids">True when object-reference GUID metadata should be retained.</param>
    /// <param name="writeReferencePaths">True when object-reference path metadata should be retained.</param>
    /// <returns>Typed serialized value snapshot.</returns>
    private static ExcelDataSerializedValueSnapshot ReadPropertyValue(SerializedProperty property,
                                                                      string resolvedOwnerAssetPath,
                                                                      bool writeAssetNames,
                                                                      bool writeReferenceGuids,
                                                                      bool writeReferencePaths)
    {
        if (property.propertyPath.EndsWith(".Array.size", StringComparison.Ordinal))
            return ExcelDataSerializedValueSnapshot.CreateValue(property.intValue, resolvedOwnerAssetPath);

        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                return ExcelDataSerializedValueSnapshot.CreateValue(property.longValue, resolvedOwnerAssetPath);
            case SerializedPropertyType.Boolean:
                return ExcelDataSerializedValueSnapshot.CreateValue(property.boolValue, resolvedOwnerAssetPath);
            case SerializedPropertyType.Float:
                return ExcelDataSerializedValueSnapshot.CreateValue(property.doubleValue, resolvedOwnerAssetPath);
            case SerializedPropertyType.String:
            case SerializedPropertyType.Character:
                return ExcelDataSerializedValueSnapshot.CreateValue(property.stringValue, resolvedOwnerAssetPath);
            case SerializedPropertyType.Enum:
                return ExcelDataSerializedValueSnapshot.CreateValue(ReadEnumValue(property), resolvedOwnerAssetPath);
            case SerializedPropertyType.ObjectReference:
                return ReadReferenceValue(property,
                                          resolvedOwnerAssetPath,
                                          writeAssetNames,
                                          writeReferenceGuids,
                                          writeReferencePaths);
            case SerializedPropertyType.Color:
                return ExcelDataSerializedValueSnapshot.CreateValue(FormatColor(property.colorValue), resolvedOwnerAssetPath);
            case SerializedPropertyType.Vector2:
                return ExcelDataSerializedValueSnapshot.CreateValue(FormatVector2(property.vector2Value), resolvedOwnerAssetPath);
            case SerializedPropertyType.Vector3:
                return ExcelDataSerializedValueSnapshot.CreateValue(FormatVector3(property.vector3Value), resolvedOwnerAssetPath);
            case SerializedPropertyType.Vector4:
                return ExcelDataSerializedValueSnapshot.CreateValue(FormatVector4(property.vector4Value), resolvedOwnerAssetPath);
            case SerializedPropertyType.Vector2Int:
                return ExcelDataSerializedValueSnapshot.CreateValue(FormatVector2Int(property.vector2IntValue), resolvedOwnerAssetPath);
            case SerializedPropertyType.Vector3Int:
                return ExcelDataSerializedValueSnapshot.CreateValue(FormatVector3Int(property.vector3IntValue), resolvedOwnerAssetPath);
            case SerializedPropertyType.AnimationCurve:
                return ExcelDataSerializedValueSnapshot.CreateValue(JsonUtility.ToJson(property.animationCurveValue), resolvedOwnerAssetPath);
            case SerializedPropertyType.Generic:
                return ExcelDataSerializedValueSnapshot.CreateValue(property.isArray ? property.arraySize : "Complex", resolvedOwnerAssetPath);
            default:
                return ExcelDataSerializedValueSnapshot.CreateWarning("Unsupported property type: " + property.propertyType, resolvedOwnerAssetPath);
        }
    }

    /// <summary>
    /// Reads a stable enum display value with index-range protection.
    /// </summary>
    /// <param name="property">Enum serialized property.</param>
    /// <returns>Enum display name or numeric index fallback.</returns>
    private static string ReadEnumValue(SerializedProperty property)
    {
        if (property.enumValueIndex >= 0 && property.enumDisplayNames != null && property.enumValueIndex < property.enumDisplayNames.Length)
            return property.enumDisplayNames[property.enumValueIndex];

        return property.enumValueIndex.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads an object-reference value and captures optional GUID/path metadata for later disambiguation.
    /// </summary>
    /// <param name="property">Object-reference serialized property.</param>
    /// <param name="resolvedOwnerAssetPath">Current owner asset path recorded in technical metadata.</param>
    /// <param name="writeAssetNames">True when the visible cell should show the referenced asset name.</param>
    /// <param name="writeReferenceGuids">True when the technical sheet should retain the reference GUID.</param>
    /// <param name="writeReferencePaths">True when the technical sheet should retain the reference path.</param>
    /// <returns>Typed reference value snapshot.</returns>
    private static ExcelDataSerializedValueSnapshot ReadReferenceValue(SerializedProperty property,
                                                                       string resolvedOwnerAssetPath,
                                                                       bool writeAssetNames,
                                                                       bool writeReferenceGuids,
                                                                       bool writeReferencePaths)
    {
        UnityEngine.Object referenceObject = property.objectReferenceValue;

        if (referenceObject == null)
            return ExcelDataSerializedValueSnapshot.CreateValue(string.Empty, resolvedOwnerAssetPath);

        string referencePath = AssetDatabase.GetAssetPath(referenceObject);
        string referenceGuid = string.IsNullOrWhiteSpace(referencePath) ? string.Empty : AssetDatabase.AssetPathToGUID(referencePath);
        string referenceName = referenceObject.name;
        object value = writeAssetNames || string.IsNullOrWhiteSpace(referencePath) ? referenceName : referencePath;

        return new ExcelDataSerializedValueSnapshot(value,
                                                    resolvedOwnerAssetPath,
                                                    referenceName,
                                                    writeReferenceGuids ? referenceGuid : string.Empty,
                                                    writeReferencePaths ? referencePath : string.Empty,
                                                    string.Empty);
    }
    #endregion

    #region Owner Resolution
    /// <summary>
    /// Resolves the current owner path from the stable GUID before falling back to the recorded path.
    /// </summary>
    /// <param name="binding">Field binding containing GUID and readable path metadata.</param>
    /// <returns>Current project-relative owner path, or an empty string when neither identity resolves.</returns>
    private static string ResolveOwnerAssetPath(ExcelDataFieldBinding binding)
    {
        if (!string.IsNullOrWhiteSpace(binding.OwnerAssetGuid))
        {
            string guidPath = AssetDatabase.GUIDToAssetPath(binding.OwnerAssetGuid);

            if (!string.IsNullOrWhiteSpace(guidPath))
                return guidPath;
        }

        return binding.OwnerAssetPath;
    }
    #endregion

    #region Formatters
    /// <summary>
    /// Formats a Unity color using invariant component values.
    /// </summary>
    /// <param name="value">Color value to format.</param>
    /// <returns>Comma-separated RGBA value.</returns>
    private static string FormatColor(Color value)
    {
        return FormatFloat(value.r) + "," + FormatFloat(value.g) + "," + FormatFloat(value.b) + "," + FormatFloat(value.a);
    }

    /// <summary>
    /// Formats a Vector2 using invariant component values.
    /// </summary>
    /// <param name="value">Vector value to format.</param>
    /// <returns>Comma-separated vector value.</returns>
    private static string FormatVector2(Vector2 value)
    {
        return FormatFloat(value.x) + "," + FormatFloat(value.y);
    }

    /// <summary>
    /// Formats a Vector3 using invariant component values.
    /// </summary>
    /// <param name="value">Vector value to format.</param>
    /// <returns>Comma-separated vector value.</returns>
    private static string FormatVector3(Vector3 value)
    {
        return FormatFloat(value.x) + "," + FormatFloat(value.y) + "," + FormatFloat(value.z);
    }

    /// <summary>
    /// Formats a Vector4 using invariant component values.
    /// </summary>
    /// <param name="value">Vector value to format.</param>
    /// <returns>Comma-separated vector value.</returns>
    private static string FormatVector4(Vector4 value)
    {
        return FormatFloat(value.x) + "," + FormatFloat(value.y) + "," + FormatFloat(value.z) + "," + FormatFloat(value.w);
    }

    /// <summary>
    /// Formats a Vector2Int using invariant component values.
    /// </summary>
    /// <param name="value">Vector value to format.</param>
    /// <returns>Comma-separated vector value.</returns>
    private static string FormatVector2Int(Vector2Int value)
    {
        return value.x.ToString(CultureInfo.InvariantCulture) + "," + value.y.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a Vector3Int using invariant component values.
    /// </summary>
    /// <param name="value">Vector value to format.</param>
    /// <returns>Comma-separated vector value.</returns>
    private static string FormatVector3Int(Vector3Int value)
    {
        return value.x.ToString(CultureInfo.InvariantCulture) + "," +
               value.y.ToString(CultureInfo.InvariantCulture) + "," +
               value.z.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats one float with invariant culture for workbook portability.
    /// </summary>
    /// <param name="value">Float value to format.</param>
    /// <returns>Invariant string representation.</returns>
    private static string FormatFloat(float value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
    #endregion

    #endregion
}
