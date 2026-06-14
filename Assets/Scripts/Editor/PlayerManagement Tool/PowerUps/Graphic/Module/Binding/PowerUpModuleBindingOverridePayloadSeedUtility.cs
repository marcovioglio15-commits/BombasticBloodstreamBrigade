using UnityEditor;

/// <summary>
/// Seeds power-up binding override payloads from existing authored payload edits.
/// </summary>
public static class PowerUpModuleBindingOverridePayloadSeedUtility
{
    #region Constants
    private const string ActivePowerUpsPropertyName = "activePowerUps";
    private const string PassivePowerUpsPropertyName = "passivePowerUps";
    private const string ModuleBindingsPropertyName = "moduleBindings";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Copies the best existing payload source into a newly enabled override payload.
    /// </summary>
    /// <param name="serializedObject">Preset serialized object that owns module definitions and power-ups.</param>
    /// <param name="bindingProperty">Binding whose override payload is being enabled.</param>
    /// <param name="moduleId">Referenced module id.</param>
    /// <param name="moduleKind">Resolved module kind used to find the payload property path.</param>
    /// <param name="overridePayloadProperty">Override payload root that receives the seed data.</param>
    /// <param name="moduleDefaultPayloadProperty">Module-management payload root used as fallback seed data.</param>
    /// <returns>True when a payload source was copied.</returns>
    public static bool SeedOverridePayload(SerializedObject serializedObject,
                                           SerializedProperty bindingProperty,
                                           string moduleId,
                                           PowerUpModuleKind moduleKind,
                                           SerializedProperty overridePayloadProperty,
                                           SerializedProperty moduleDefaultPayloadProperty)
    {
        if (serializedObject == null ||
            bindingProperty == null ||
            overridePayloadProperty == null ||
            string.IsNullOrWhiteSpace(moduleId))
        {
            return false;
        }

        if (!PowerUpModuleEnumDescriptions.TryGetPayloadProperty(moduleKind,
                                                                 out string payloadRelativePath,
                                                                 out string _))
        {
            return false;
        }

        SerializedProperty targetPayloadProperty = overridePayloadProperty.FindPropertyRelative(payloadRelativePath);

        if (targetPayloadProperty == null)
            return false;

        SerializedProperty sourcePayloadProperty = FindMatchingOverridePayload(serializedObject,
                                                                               bindingProperty,
                                                                               moduleId,
                                                                               payloadRelativePath);

        if (sourcePayloadProperty == null && moduleDefaultPayloadProperty != null)
            sourcePayloadProperty = moduleDefaultPayloadProperty.FindPropertyRelative(payloadRelativePath);

        if (sourcePayloadProperty == null)
            return false;

        CopySerializedPropertyTree(targetPayloadProperty, sourcePayloadProperty);
        overridePayloadProperty.serializedObject.ApplyModifiedProperties();
        return true;
    }
    #endregion

    #region Source Resolution
    /// <summary>
    /// Finds another override payload that targets the same module id.
    /// </summary>
    /// <param name="serializedObject">Preset serialized object to scan.</param>
    /// <param name="currentBindingProperty">Binding currently being edited and therefore excluded from the scan.</param>
    /// <param name="moduleId">Module id to match.</param>
    /// <param name="payloadRelativePath">Payload path inside PowerUpModuleData.</param>
    /// <returns>Matching override payload when another power-up already edited this module; otherwise null.</returns>
    private static SerializedProperty FindMatchingOverridePayload(SerializedObject serializedObject,
                                                                  SerializedProperty currentBindingProperty,
                                                                  string moduleId,
                                                                  string payloadRelativePath)
    {
        SerializedProperty payloadProperty = FindMatchingOverridePayloadInPowerUps(serializedObject,
                                                                                   currentBindingProperty,
                                                                                   ActivePowerUpsPropertyName,
                                                                                   moduleId,
                                                                                   payloadRelativePath);

        if (payloadProperty != null)
            return payloadProperty;

        return FindMatchingOverridePayloadInPowerUps(serializedObject,
                                                     currentBindingProperty,
                                                     PassivePowerUpsPropertyName,
                                                     moduleId,
                                                     payloadRelativePath);
    }

    /// <summary>
    /// Scans one power-up array for another enabled override payload referencing the same module.
    /// </summary>
    /// <param name="serializedObject">Preset serialized object to scan.</param>
    /// <param name="currentBindingProperty">Binding currently being edited and therefore excluded from the scan.</param>
    /// <param name="powerUpsPropertyName">Serialized power-up list property name.</param>
    /// <param name="moduleId">Module id to match.</param>
    /// <param name="payloadRelativePath">Payload path inside PowerUpModuleData.</param>
    /// <returns>Matching override payload when found; otherwise null.</returns>
    private static SerializedProperty FindMatchingOverridePayloadInPowerUps(SerializedObject serializedObject,
                                                                           SerializedProperty currentBindingProperty,
                                                                           string powerUpsPropertyName,
                                                                           string moduleId,
                                                                           string payloadRelativePath)
    {
        SerializedProperty powerUpsProperty = serializedObject.FindProperty(powerUpsPropertyName);

        if (powerUpsProperty == null || !powerUpsProperty.isArray)
            return null;

        for (int powerUpIndex = 0; powerUpIndex < powerUpsProperty.arraySize; powerUpIndex++)
        {
            SerializedProperty powerUpProperty = powerUpsProperty.GetArrayElementAtIndex(powerUpIndex);
            SerializedProperty bindingsProperty = powerUpProperty != null
                ? powerUpProperty.FindPropertyRelative(ModuleBindingsPropertyName)
                : null;

            if (bindingsProperty == null || !bindingsProperty.isArray)
                continue;

            SerializedProperty payloadProperty = FindMatchingOverridePayloadInBindings(bindingsProperty,
                                                                                       currentBindingProperty,
                                                                                       moduleId,
                                                                                       payloadRelativePath);

            if (payloadProperty != null)
                return payloadProperty;
        }

        return null;
    }

    /// <summary>
    /// Scans one module-binding list for another enabled override payload referencing the same module.
    /// </summary>
    /// <param name="bindingsProperty">Serialized binding list to scan.</param>
    /// <param name="currentBindingProperty">Binding currently being edited and therefore excluded from the scan.</param>
    /// <param name="moduleId">Module id to match.</param>
    /// <param name="payloadRelativePath">Payload path inside PowerUpModuleData.</param>
    /// <returns>Matching override payload when found; otherwise null.</returns>
    private static SerializedProperty FindMatchingOverridePayloadInBindings(SerializedProperty bindingsProperty,
                                                                           SerializedProperty currentBindingProperty,
                                                                           string moduleId,
                                                                           string payloadRelativePath)
    {
        for (int bindingIndex = 0; bindingIndex < bindingsProperty.arraySize; bindingIndex++)
        {
            SerializedProperty bindingProperty = bindingsProperty.GetArrayElementAtIndex(bindingIndex);

            if (bindingProperty == null)
                continue;

            if (string.Equals(bindingProperty.propertyPath, currentBindingProperty.propertyPath, System.StringComparison.Ordinal))
                continue;

            SerializedProperty moduleIdProperty = bindingProperty.FindPropertyRelative("moduleId");
            SerializedProperty useOverrideProperty = bindingProperty.FindPropertyRelative("useOverridePayload");
            SerializedProperty overridePayloadProperty = bindingProperty.FindPropertyRelative("overridePayload");

            if (moduleIdProperty == null ||
                useOverrideProperty == null ||
                overridePayloadProperty == null)
            {
                continue;
            }

            if (!useOverrideProperty.boolValue)
                continue;

            if (!string.Equals(moduleIdProperty.stringValue, moduleId, System.StringComparison.OrdinalIgnoreCase))
                continue;

            SerializedProperty payloadProperty = overridePayloadProperty.FindPropertyRelative(payloadRelativePath);

            if (payloadProperty != null)
                return payloadProperty;
        }

        return null;
    }
    #endregion

    #region Copy
    /// <summary>
    /// Copies one serialized property tree while preserving nested arrays and Unity object references.
    /// </summary>
    /// <param name="targetProperty">Serialized property that receives the value tree.</param>
    /// <param name="sourceProperty">Serialized property that provides the value tree.</param>
    private static void CopySerializedPropertyTree(SerializedProperty targetProperty, SerializedProperty sourceProperty)
    {
        if (targetProperty == null ||
            sourceProperty == null ||
            targetProperty.propertyType != sourceProperty.propertyType)
        {
            return;
        }

        if (targetProperty.isArray && sourceProperty.isArray && targetProperty.propertyType != SerializedPropertyType.String)
        {
            CopyArrayProperty(targetProperty, sourceProperty);
            return;
        }

        if (targetProperty.propertyType == SerializedPropertyType.Generic)
        {
            CopyGenericProperty(targetProperty, sourceProperty);
            return;
        }

        CopyValueProperty(targetProperty, sourceProperty);
    }

    /// <summary>
    /// Copies a serialized array including all child element values.
    /// </summary>
    /// <param name="targetProperty">Array property that receives element data.</param>
    /// <param name="sourceProperty">Array property that provides element data.</param>
    private static void CopyArrayProperty(SerializedProperty targetProperty, SerializedProperty sourceProperty)
    {
        targetProperty.arraySize = sourceProperty.arraySize;

        for (int elementIndex = 0; elementIndex < sourceProperty.arraySize; elementIndex++)
        {
            CopySerializedPropertyTree(targetProperty.GetArrayElementAtIndex(elementIndex),
                                       sourceProperty.GetArrayElementAtIndex(elementIndex));
        }
    }

    /// <summary>
    /// Copies fields inside a serialized object-like property by matching child names.
    /// </summary>
    /// <param name="targetProperty">Generic property that receives child data.</param>
    /// <param name="sourceProperty">Generic property that provides child data.</param>
    private static void CopyGenericProperty(SerializedProperty targetProperty, SerializedProperty sourceProperty)
    {
        SerializedProperty sourceIterator = sourceProperty.Copy();
        SerializedProperty sourceEndProperty = sourceIterator.GetEndProperty();
        bool enterChildren = true;

        while (sourceIterator.NextVisible(enterChildren) &&
               !SerializedProperty.EqualContents(sourceIterator, sourceEndProperty))
        {
            enterChildren = false;
            string childName = sourceIterator.name;

            if (string.IsNullOrWhiteSpace(childName))
                continue;

            SerializedProperty targetChildProperty = targetProperty.FindPropertyRelative(childName);

            if (targetChildProperty == null)
                continue;

            CopySerializedPropertyTree(targetChildProperty, sourceIterator);
        }
    }

    /// <summary>
    /// Copies one non-generic serialized value between matching property types.
    /// </summary>
    /// <param name="targetProperty">Property that receives the value.</param>
    /// <param name="sourceProperty">Property that provides the value.</param>
    private static void CopyValueProperty(SerializedProperty targetProperty, SerializedProperty sourceProperty)
    {
        switch (targetProperty.propertyType)
        {
            case SerializedPropertyType.Boolean:
                targetProperty.boolValue = sourceProperty.boolValue;
                break;
            case SerializedPropertyType.Bounds:
                targetProperty.boundsValue = sourceProperty.boundsValue;
                break;
            case SerializedPropertyType.BoundsInt:
                targetProperty.boundsIntValue = sourceProperty.boundsIntValue;
                break;
            case SerializedPropertyType.Color:
                targetProperty.colorValue = sourceProperty.colorValue;
                break;
            case SerializedPropertyType.Enum:
                targetProperty.enumValueIndex = sourceProperty.enumValueIndex;
                break;
            case SerializedPropertyType.Float:
                targetProperty.floatValue = sourceProperty.floatValue;
                break;
            case SerializedPropertyType.Integer:
                targetProperty.longValue = sourceProperty.longValue;
                break;
            case SerializedPropertyType.ObjectReference:
                targetProperty.objectReferenceValue = sourceProperty.objectReferenceValue;
                break;
            case SerializedPropertyType.Quaternion:
                targetProperty.quaternionValue = sourceProperty.quaternionValue;
                break;
            case SerializedPropertyType.Rect:
                targetProperty.rectValue = sourceProperty.rectValue;
                break;
            case SerializedPropertyType.RectInt:
                targetProperty.rectIntValue = sourceProperty.rectIntValue;
                break;
            case SerializedPropertyType.String:
                targetProperty.stringValue = sourceProperty.stringValue;
                break;
            case SerializedPropertyType.Vector2:
                targetProperty.vector2Value = sourceProperty.vector2Value;
                break;
            case SerializedPropertyType.Vector2Int:
                targetProperty.vector2IntValue = sourceProperty.vector2IntValue;
                break;
            case SerializedPropertyType.Vector3:
                targetProperty.vector3Value = sourceProperty.vector3Value;
                break;
            case SerializedPropertyType.Vector3Int:
                targetProperty.vector3IntValue = sourceProperty.vector3IntValue;
                break;
            case SerializedPropertyType.Vector4:
                targetProperty.vector4Value = sourceProperty.vector4Value;
                break;
        }
    }
    #endregion

    #endregion
}
