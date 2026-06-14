using System;
using UnityEditor;

/// <summary>
/// Copies source module payload data into boss pattern override payloads without using runtime reflection.
/// </summary>
internal static class EnemyBossPatternOverridePayloadSeedUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Copies the selected source module payload into the override payload root.
    /// </summary>
    /// <param name="sourcePreset">Source module catalog.</param>
    /// <param name="section">Catalog section that owns the selected module.</param>
    /// <param name="moduleId">Module ID to resolve.</param>
    /// <param name="overridePayloadProperty">Target override payload root.</param>
    /// <returns>True when a source payload was found and copied.</returns>
    public static bool SeedOverridePayloadFromSourceModule(EnemyModulesAndPatternsPreset sourcePreset,
                                                           EnemyPatternModuleCatalogSection section,
                                                           string moduleId,
                                                           SerializedProperty overridePayloadProperty)
    {
        if (overridePayloadProperty == null)
            return false;

        SerializedObject sourceSerializedObject;
        SerializedProperty sourcePayloadProperty;

        if (!TryFindSourceModulePayloadProperty(sourcePreset, section, moduleId, out sourceSerializedObject, out sourcePayloadProperty))
            return false;

        return sourceSerializedObject != null && CopySerializedPropertyTree(sourcePayloadProperty, overridePayloadProperty);
    }
    #endregion

    #region Source Resolution
    /// <summary>
    /// Finds the serialized payload data for one module in the source preset catalog.
    /// </summary>
    /// <param name="sourcePreset">Source module catalog.</param>
    /// <param name="section">Catalog section to inspect.</param>
    /// <param name="moduleId">Module ID to resolve.</param>
    /// <param name="sourceSerializedObject">Serialized object that owns the resolved source property.</param>
    /// <param name="payloadProperty">Resolved payload property.</param>
    /// <returns>True when the module payload property is available.</returns>
    private static bool TryFindSourceModulePayloadProperty(EnemyModulesAndPatternsPreset sourcePreset,
                                                           EnemyPatternModuleCatalogSection section,
                                                           string moduleId,
                                                           out SerializedObject sourceSerializedObject,
                                                           out SerializedProperty payloadProperty)
    {
        sourceSerializedObject = null;
        payloadProperty = null;

        if (sourcePreset == null)
            return false;

        if (string.IsNullOrWhiteSpace(moduleId))
            return false;

        sourceSerializedObject = new SerializedObject(sourcePreset);
        sourceSerializedObject.Update();
        SerializedProperty definitionsProperty = sourceSerializedObject.FindProperty(GetDefinitionsPropertyName(section));

        if (definitionsProperty == null)
            return false;

        for (int index = 0; index < definitionsProperty.arraySize; index++)
        {
            SerializedProperty definitionProperty = definitionsProperty.GetArrayElementAtIndex(index);

            if (definitionProperty == null)
                continue;

            SerializedProperty sourceModuleIdProperty = definitionProperty.FindPropertyRelative("moduleId");

            if (sourceModuleIdProperty == null)
                continue;

            if (!string.Equals(sourceModuleIdProperty.stringValue, moduleId, StringComparison.OrdinalIgnoreCase))
                continue;

            payloadProperty = definitionProperty.FindPropertyRelative("data");
            return payloadProperty != null;
        }

        return false;
    }

    /// <summary>
    /// Returns the serialized property name used by one source module catalog section.
    /// </summary>
    /// <param name="section">Catalog section being inspected.</param>
    /// <returns>Serialized definitions property name.</returns>
    private static string GetDefinitionsPropertyName(EnemyPatternModuleCatalogSection section)
    {
        switch (section)
        {
            case EnemyPatternModuleCatalogSection.CoreMovement:
                return "coreMovementDefinitions";

            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                return "shortRangeInteractionDefinitions";

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                return "weaponInteractionDefinitions";

            default:
                return "dropItemsDefinitions";
        }
    }
    #endregion

    #region Property Copy
    /// <summary>
    /// Copies every compatible child value from one payload root to another.
    /// </summary>
    /// <param name="sourceRoot">Source payload root.</param>
    /// <param name="targetRoot">Target payload root.</param>
    /// <returns>True when at least one serialized value was copied.</returns>
    private static bool CopySerializedPropertyTree(SerializedProperty sourceRoot, SerializedProperty targetRoot)
    {
        if (sourceRoot == null || targetRoot == null)
            return false;

        SerializedProperty sourceIterator = sourceRoot.Copy();
        SerializedProperty sourceEnd = sourceRoot.GetEndProperty();
        bool enterChildren = true;
        bool copiedAny = false;

        while (sourceIterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(sourceIterator, sourceEnd))
        {
            enterChildren = true;

            string relativePath = ResolveRelativePropertyPath(sourceRoot.propertyPath, sourceIterator.propertyPath);

            if (string.IsNullOrWhiteSpace(relativePath))
                continue;

            SerializedProperty targetProperty = targetRoot.FindPropertyRelative(relativePath);

            if (targetProperty == null)
                continue;

            if (CopySerializedPropertyValue(sourceIterator, targetProperty))
                copiedAny = true;
        }

        return copiedAny;
    }

    /// <summary>
    /// Copies one serialized property value when the source and target types are compatible.
    /// </summary>
    /// <param name="sourceProperty">Source property value.</param>
    /// <param name="targetProperty">Target property value.</param>
    /// <returns>True when a compatible value was written.</returns>
    private static bool CopySerializedPropertyValue(SerializedProperty sourceProperty, SerializedProperty targetProperty)
    {
        if (sourceProperty == null || targetProperty == null)
            return false;

        if (sourceProperty.propertyType != targetProperty.propertyType)
            return false;

        if (sourceProperty.isArray && sourceProperty.propertyType != SerializedPropertyType.String)
        {
            if (!targetProperty.isArray)
                return false;

            targetProperty.arraySize = sourceProperty.arraySize;
            return true;
        }

        switch (sourceProperty.propertyType)
        {
            case SerializedPropertyType.Integer:
                targetProperty.intValue = sourceProperty.intValue;
                return true;

            case SerializedPropertyType.Boolean:
                targetProperty.boolValue = sourceProperty.boolValue;
                return true;

            case SerializedPropertyType.Float:
                targetProperty.floatValue = sourceProperty.floatValue;
                return true;

            case SerializedPropertyType.String:
                targetProperty.stringValue = sourceProperty.stringValue;
                return true;

            case SerializedPropertyType.Color:
                targetProperty.colorValue = sourceProperty.colorValue;
                return true;

            case SerializedPropertyType.ObjectReference:
                targetProperty.objectReferenceValue = sourceProperty.objectReferenceValue;
                return true;

            case SerializedPropertyType.LayerMask:
                targetProperty.intValue = sourceProperty.intValue;
                return true;

            case SerializedPropertyType.Enum:
                targetProperty.enumValueIndex = sourceProperty.enumValueIndex;
                return true;

            case SerializedPropertyType.Vector2:
                targetProperty.vector2Value = sourceProperty.vector2Value;
                return true;

            case SerializedPropertyType.Vector3:
                targetProperty.vector3Value = sourceProperty.vector3Value;
                return true;

            case SerializedPropertyType.Vector4:
                targetProperty.vector4Value = sourceProperty.vector4Value;
                return true;

            case SerializedPropertyType.Rect:
                targetProperty.rectValue = sourceProperty.rectValue;
                return true;

            case SerializedPropertyType.Character:
                targetProperty.intValue = sourceProperty.intValue;
                return true;

            case SerializedPropertyType.AnimationCurve:
                targetProperty.animationCurveValue = sourceProperty.animationCurveValue;
                return true;

            case SerializedPropertyType.Bounds:
                targetProperty.boundsValue = sourceProperty.boundsValue;
                return true;

            case SerializedPropertyType.Quaternion:
                targetProperty.quaternionValue = sourceProperty.quaternionValue;
                return true;

            case SerializedPropertyType.Vector2Int:
                targetProperty.vector2IntValue = sourceProperty.vector2IntValue;
                return true;

            case SerializedPropertyType.Vector3Int:
                targetProperty.vector3IntValue = sourceProperty.vector3IntValue;
                return true;

            case SerializedPropertyType.RectInt:
                targetProperty.rectIntValue = sourceProperty.rectIntValue;
                return true;

            case SerializedPropertyType.BoundsInt:
                targetProperty.boundsIntValue = sourceProperty.boundsIntValue;
                return true;
        }

        return false;
    }

    /// <summary>
    /// Converts a child property path into a path relative to its serialized root.
    /// </summary>
    /// <param name="rootPath">Source root property path.</param>
    /// <param name="propertyPath">Child property path.</param>
    /// <returns>Relative child path, or an empty string when the child does not belong to the root.</returns>
    private static string ResolveRelativePropertyPath(string rootPath, string propertyPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(propertyPath))
            return string.Empty;

        string prefix = rootPath + ".";

        if (!propertyPath.StartsWith(prefix, StringComparison.Ordinal))
            return string.Empty;

        return propertyPath.Substring(prefix.Length);
    }
    #endregion

    #endregion
}
