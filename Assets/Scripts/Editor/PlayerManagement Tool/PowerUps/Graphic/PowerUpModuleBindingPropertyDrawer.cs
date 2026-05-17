using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(PowerUpModuleBinding))]
public sealed class PowerUpModuleBindingPropertyDrawer : PropertyDrawer
{
    #region Fields
    private const string ActivePowerUpsRoot = "activePowerUps.Array.data[";
    private const string ModuleBindingsPath = ".moduleBindings.Array.data[";

    private static readonly string[] spawnObjectPayloadPropertyNames =
    {
        "bombPrefab",
        "spawnOffset",
        "spawnOffsetOrientation",
        "deploySpeed",
        "velocityDirection",
        "collisionRadius",
        "bounceOnWalls",
        "bounceDamping",
        "linearDampingPerSecond",
        "fuseSeconds",
        "radius",
        "enableDamagePayload",
        "damage",
        "affectAllEnemiesInRadius",
        "explosionVfxPrefab",
        "scaleVfxToRadius",
        "vfxScaleMultiplier"
    };
    #endregion

    #region Methods
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty moduleIdProperty = property.FindPropertyRelative("moduleId");
        SerializedProperty stageProperty = property.FindPropertyRelative("stage");
        SerializedProperty enabledProperty = property.FindPropertyRelative("isEnabled");
        SerializedProperty useOverrideProperty = property.FindPropertyRelative("useOverridePayload");
        SerializedProperty overridePayloadProperty = property.FindPropertyRelative("overridePayload");

        if (moduleIdProperty == null ||
            stageProperty == null ||
            enabledProperty == null ||
            useOverrideProperty == null ||
            overridePayloadProperty == null)
        {
            Label errorLabel = new Label("PowerUpModuleBinding serialized fields are missing.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        List<string> moduleIdOptions = BuildModuleIdOptions(property.serializedObject);

        if (moduleIdOptions.Count == 0)
            moduleIdOptions.Add(string.Empty);

        string initialValue = ResolveInitialModuleId(moduleIdProperty.stringValue, moduleIdOptions);
        PopupField<string> modulePopup = new PopupField<string>("Module", moduleIdOptions, initialValue);
        modulePopup.tooltip = "Module ID reference from Modules Management.";
        root.Add(modulePopup);

        HelpBox moduleKindInfoBox = new HelpBox(string.Empty, HelpBoxMessageType.Info);
        moduleKindInfoBox.style.marginTop = 2f;
        ApplyFieldAlignedBoxStyle(moduleKindInfoBox);
        root.Add(moduleKindInfoBox);

        AddField(root, enabledProperty, "Enabled");
        AddField(root, useOverrideProperty, "Use Override Payload");

        Label payloadHeader = new Label("Override Payload");
        payloadHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        payloadHeader.style.marginTop = 4f;
        root.Add(payloadHeader);

        VisualElement overrideContainer = new VisualElement();
        overrideContainer.style.marginLeft = 10f;
        root.Add(overrideContainer);

        RefreshBindingUi(property,
                         property.serializedObject,
                         moduleIdProperty,
                         stageProperty,
                         useOverrideProperty,
                         overridePayloadProperty,
                         modulePopup,
                         moduleKindInfoBox,
                         overrideContainer);

        modulePopup.RegisterValueChangedCallback(evt =>
        {
            if (string.Equals(moduleIdProperty.stringValue, evt.newValue, System.StringComparison.Ordinal))
                return;

            moduleIdProperty.serializedObject.Update();
            moduleIdProperty.stringValue = evt.newValue;
            moduleIdProperty.serializedObject.ApplyModifiedProperties();
            RefreshBindingUi(property,
                             property.serializedObject,
                             moduleIdProperty,
                             stageProperty,
                             useOverrideProperty,
                             overridePayloadProperty,
                             modulePopup,
                             moduleKindInfoBox,
                             overrideContainer);
        });

        root.TrackPropertyValue(moduleIdProperty, changedProperty =>
        {
            RefreshBindingUi(property,
                             property.serializedObject,
                             changedProperty,
                             stageProperty,
                             useOverrideProperty,
                             overridePayloadProperty,
                             modulePopup,
                             moduleKindInfoBox,
                             overrideContainer);
        });

        root.TrackPropertyValue(useOverrideProperty, changedProperty =>
        {
            RefreshBindingUi(property,
                             property.serializedObject,
                             moduleIdProperty,
                             stageProperty,
                             changedProperty,
                             overridePayloadProperty,
                             modulePopup,
                             moduleKindInfoBox,
                             overrideContainer);
        });

        return root;
    }

    private static void RefreshBindingUi(SerializedProperty bindingProperty,
                                         SerializedObject serializedObject,
                                         SerializedProperty moduleIdProperty,
                                         SerializedProperty stageProperty,
                                         SerializedProperty useOverrideProperty,
                                         SerializedProperty overridePayloadProperty,
                                         PopupField<string> modulePopup,
                                         HelpBox moduleKindInfoBox,
                                         VisualElement overrideContainer)
    {
        string moduleId = moduleIdProperty != null ? moduleIdProperty.stringValue : string.Empty;
        List<string> options = BuildModuleIdOptions(serializedObject);

        if (options.Count == 0)
            options.Add(string.Empty);

        string resolvedModuleId = ResolveInitialModuleId(moduleId, options);

        if (modulePopup.value != resolvedModuleId)
            modulePopup.SetValueWithoutNotify(resolvedModuleId);

        if (moduleIdProperty != null && moduleIdProperty.stringValue != resolvedModuleId)
        {
            moduleIdProperty.serializedObject.Update();
            moduleIdProperty.stringValue = resolvedModuleId;
            moduleIdProperty.serializedObject.ApplyModifiedProperties();
        }

        PowerUpModuleKind moduleKind;
        PowerUpModuleStage moduleDefaultStage;
        string moduleDisplayName;
        SerializedProperty moduleDefaultPayloadProperty;
        bool moduleResolved = TryResolveModuleInfo(serializedObject,
                                                   resolvedModuleId,
                                                   out moduleKind,
                                                   out moduleDefaultStage,
                                                   out moduleDisplayName,
                                                   out moduleDefaultPayloadProperty);
        PowerUpModuleStage bindingStage = moduleResolved ? moduleDefaultStage : ResolveStage(stageProperty);

        if (stageProperty != null && stageProperty.propertyType == SerializedPropertyType.Enum && stageProperty.enumValueIndex != (int)bindingStage)
        {
            stageProperty.serializedObject.Update();
            stageProperty.enumValueIndex = (int)bindingStage;
            stageProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        UpdateModuleInfoBox(moduleKindInfoBox, moduleResolved, moduleKind, moduleDisplayName);

        if (moduleResolved && useOverrideProperty != null && useOverrideProperty.boolValue)
            EnsureOverridePayloadInheritedReferences(overridePayloadProperty, moduleDefaultPayloadProperty, moduleKind);

        RebuildOverrideContainer(overrideContainer,
                                 useOverrideProperty,
                                 overridePayloadProperty,
                                 moduleDefaultPayloadProperty,
                                 moduleResolved,
                                 moduleKind,
                                 bindingProperty);
    }

    private static void UpdateModuleInfoBox(HelpBox infoBox, bool moduleResolved, PowerUpModuleKind moduleKind, string moduleDisplayName)
    {
        if (infoBox == null)
            return;

        if (!moduleResolved)
        {
            infoBox.text = "Selected module could not be resolved from Modules Management.";
            infoBox.messageType = HelpBoxMessageType.Warning;
            return;
        }

        infoBox.text = string.Format("Selected module: {0} | Kind: {1}\n{2}",
                                     moduleDisplayName,
                                     moduleKind,
                                     PowerUpModuleEnumDescriptions.GetModuleKindDescription(moduleKind));
        infoBox.messageType = HelpBoxMessageType.Info;
    }

    private static void RebuildOverrideContainer(VisualElement overrideContainer,
                                                 SerializedProperty useOverrideProperty,
                                                 SerializedProperty overridePayloadProperty,
                                                 SerializedProperty moduleDefaultPayloadProperty,
                                                 bool moduleResolved,
                                                 PowerUpModuleKind moduleKind,
                                                 SerializedProperty bindingProperty)
    {
        if (overrideContainer == null)
            return;

        bool showOverride = useOverrideProperty != null && useOverrideProperty.boolValue;
        overrideContainer.style.display = showOverride ? DisplayStyle.Flex : DisplayStyle.None;
        overrideContainer.Clear();

        if (!showOverride)
            return;

        if (!moduleResolved)
        {
            HelpBox moduleMissingBox = new HelpBox("Select a valid module to configure override payload.", HelpBoxMessageType.Warning);
            overrideContainer.Add(moduleMissingBox);
            return;
        }

        string relativePath;
        string payloadLabel;
        bool hasPayload = PowerUpModuleEnumDescriptions.TryGetPayloadProperty(moduleKind, out relativePath, out payloadLabel);

        if (!hasPayload)
        {
            HelpBox noPayloadBox = new HelpBox("Selected module kind does not use payload data.", HelpBoxMessageType.Info);
            overrideContainer.Add(noPayloadBox);
            return;
        }

        if (overridePayloadProperty == null)
        {
            HelpBox missingOverrideBox = new HelpBox("Override payload storage is missing.", HelpBoxMessageType.Warning);
            overrideContainer.Add(missingOverrideBox);
            return;
        }

        SerializedProperty payloadProperty = overridePayloadProperty.FindPropertyRelative(relativePath);

        if (payloadProperty == null)
        {
            HelpBox missingPayloadBox = new HelpBox("Override payload property is missing for selected module kind.", HelpBoxMessageType.Warning);
            overrideContainer.Add(missingPayloadBox);
            return;
        }

        AddOverridePayloadWarnings(overrideContainer, overridePayloadProperty, moduleDefaultPayloadProperty, moduleKind);
        bool showActiveTriggerCharacterTuningOption = ShouldShowActiveTriggerCharacterTuningOption(bindingProperty, moduleKind);
        PowerUpModuleDefinitionPropertyDrawer.BuildPayloadEditor(overrideContainer,
                                                                 payloadProperty,
                                                                 moduleKind,
                                                                 payloadLabel,
                                                                 showActiveTriggerCharacterTuningOption);
    }

    private static bool TryResolveModuleInfo(SerializedObject serializedObject,
                                             string moduleId,
                                             out PowerUpModuleKind moduleKind,
                                             out PowerUpModuleStage defaultStage,
                                             out string displayName,
                                             out SerializedProperty payloadProperty)
    {
        moduleKind = default;
        defaultStage = default;
        displayName = string.Empty;
        payloadProperty = null;

        if (serializedObject == null)
            return false;

        if (string.IsNullOrWhiteSpace(moduleId))
            return false;

        SerializedProperty modulesProperty = serializedObject.FindProperty("moduleDefinitions");

        if (modulesProperty == null)
            return false;

        for (int index = 0; index < modulesProperty.arraySize; index++)
        {
            SerializedProperty moduleElement = modulesProperty.GetArrayElementAtIndex(index);

            if (moduleElement == null)
                continue;

            SerializedProperty moduleIdProperty = moduleElement.FindPropertyRelative("moduleId");

            if (moduleIdProperty == null)
                continue;

            if (!string.Equals(moduleIdProperty.stringValue, moduleId, System.StringComparison.OrdinalIgnoreCase))
                continue;

            SerializedProperty moduleKindProperty = moduleElement.FindPropertyRelative("moduleKind");
            SerializedProperty displayNameProperty = moduleElement.FindPropertyRelative("displayName");
            moduleKind = ResolveModuleKindFromEnumProperty(moduleKindProperty);
            defaultStage = PowerUpModuleKindUtility.ResolveStageFromKind(moduleKind);
            displayName = displayNameProperty != null && !string.IsNullOrWhiteSpace(displayNameProperty.stringValue)
                ? displayNameProperty.stringValue
                : moduleId;
            payloadProperty = moduleElement.FindPropertyRelative("data");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves whether the Character Tuning payload should show active-trigger scope controls for this binding.
    /// </summary>
    /// <param name="bindingProperty">Serialized module binding currently being drawn.</param>
    /// <param name="moduleKind">Resolved module kind for the selected binding.</param>
    /// <returns>True when the binding belongs to a triggerable non-toggleable active without Trigger Hold Charge.</returns>
    private static bool ShouldShowActiveTriggerCharacterTuningOption(SerializedProperty bindingProperty, PowerUpModuleKind moduleKind)
    {
        if (moduleKind != PowerUpModuleKind.CharacterTuning)
            return false;

        if (bindingProperty == null)
            return false;

        if (!IsBindingEnabled(bindingProperty))
            return false;

        if (!TryResolveOwningActivePowerUpProperty(bindingProperty, out SerializedProperty powerUpProperty))
            return false;

        return IsNonToggleableActiveWithoutHoldCharge(powerUpProperty);
    }

    /// <summary>
    /// Resolves whether one binding is currently enabled.
    /// </summary>
    /// <param name="bindingProperty">Serialized binding property being inspected.</param>
    /// <returns>True when the binding is enabled or the serialized flag is missing.</returns>
    private static bool IsBindingEnabled(SerializedProperty bindingProperty)
    {
        SerializedProperty enabledProperty = bindingProperty != null
            ? bindingProperty.FindPropertyRelative("isEnabled")
            : null;

        return enabledProperty == null || enabledProperty.boolValue;
    }

    /// <summary>
    /// Resolves the active power-up property that owns the provided binding.
    /// </summary>
    /// <param name="bindingProperty">Serialized binding property being inspected.</param>
    /// <param name="powerUpProperty">Owning active power-up property when resolved.</param>
    /// <returns>True when the binding belongs to the Active Power Ups array.</returns>
    private static bool TryResolveOwningActivePowerUpProperty(SerializedProperty bindingProperty, out SerializedProperty powerUpProperty)
    {
        powerUpProperty = null;

        if (bindingProperty == null || bindingProperty.serializedObject == null)
            return false;

        string propertyPath = bindingProperty.propertyPath;

        if (string.IsNullOrWhiteSpace(propertyPath))
            return false;

        if (!propertyPath.StartsWith(ActivePowerUpsRoot, System.StringComparison.Ordinal))
            return false;

        int moduleBindingsIndex = propertyPath.IndexOf(ModuleBindingsPath, System.StringComparison.Ordinal);

        if (moduleBindingsIndex <= 0)
            return false;

        string powerUpPath = propertyPath.Substring(0, moduleBindingsIndex);
        powerUpProperty = bindingProperty.serializedObject.FindProperty(powerUpPath);
        return powerUpProperty != null;
    }

    /// <summary>
    /// Checks the owning active composition for the exact runtime context that supports trigger-scoped Character Tuning.
    /// </summary>
    /// <param name="powerUpProperty">Serialized active power-up definition that owns the binding.</param>
    /// <returns>True when the active has a triggerable tool, is not toggleable, and has no Trigger Hold Charge module.</returns>
    private static bool IsNonToggleableActiveWithoutHoldCharge(SerializedProperty powerUpProperty)
    {
        if (powerUpProperty == null)
            return false;

        SerializedProperty moduleBindingsProperty = powerUpProperty.FindPropertyRelative("moduleBindings");

        if (moduleBindingsProperty == null || !moduleBindingsProperty.isArray)
            return false;

        bool hasHoldCharge = false;
        bool hasToggleableGate = false;
        bool hasTriggerableTool = false;

        for (int bindingIndex = 0; bindingIndex < moduleBindingsProperty.arraySize; bindingIndex++)
        {
            SerializedProperty siblingBindingProperty = moduleBindingsProperty.GetArrayElementAtIndex(bindingIndex);

            if (!IsBindingEnabled(siblingBindingProperty))
                continue;

            SerializedProperty moduleIdProperty = siblingBindingProperty.FindPropertyRelative("moduleId");

            if (moduleIdProperty == null)
                continue;

            if (!TryResolveModuleInfo(powerUpProperty.serializedObject,
                                      moduleIdProperty.stringValue,
                                      out PowerUpModuleKind siblingModuleKind,
                                      out PowerUpModuleStage _,
                                      out string _,
                                      out SerializedProperty moduleDefaultPayloadProperty))
            {
                continue;
            }

            switch (siblingModuleKind)
            {
                case PowerUpModuleKind.TriggerHoldCharge:
                    hasHoldCharge = true;
                    break;
                case PowerUpModuleKind.GateResource:
                    hasToggleableGate = hasToggleableGate ||
                                        ResolveBindingResourceGateToggleable(siblingBindingProperty,
                                                                              moduleDefaultPayloadProperty);
                    break;
                case PowerUpModuleKind.ProjectilesPatternCone:
                case PowerUpModuleKind.SpawnObject:
                case PowerUpModuleKind.Dash:
                case PowerUpModuleKind.TimeDilationEnemies:
                case PowerUpModuleKind.Heal:
                    hasTriggerableTool = true;
                    break;
            }

            if (hasHoldCharge || hasToggleableGate)
                return false;
        }

        return hasTriggerableTool;
    }

    /// <summary>
    /// Resolves the effective Resource Gate toggleable flag for a binding, honoring override payloads.
    /// </summary>
    /// <param name="bindingProperty">Serialized Resource Gate binding being inspected.</param>
    /// <param name="moduleDefaultPayloadProperty">Referenced module-default payload root.</param>
    /// <returns>True when the effective Resource Gate payload is toggleable.</returns>
    private static bool ResolveBindingResourceGateToggleable(SerializedProperty bindingProperty,
                                                             SerializedProperty moduleDefaultPayloadProperty)
    {
        SerializedProperty payloadRootProperty = ResolveBindingPayloadRoot(bindingProperty, moduleDefaultPayloadProperty);
        SerializedProperty resourceGateProperty = payloadRootProperty != null
            ? payloadRootProperty.FindPropertyRelative("resourceGate")
            : null;
        SerializedProperty isToggleableProperty = resourceGateProperty != null
            ? resourceGateProperty.FindPropertyRelative("isToggleable")
            : null;

        return isToggleableProperty != null && isToggleableProperty.boolValue;
    }

    /// <summary>
    /// Resolves the payload root used by one binding, preferring override payloads when enabled.
    /// </summary>
    /// <param name="bindingProperty">Serialized binding whose payload root is requested.</param>
    /// <param name="moduleDefaultPayloadProperty">Referenced module-default payload root.</param>
    /// <returns>Override payload root when enabled; otherwise the module-default payload root.</returns>
    private static SerializedProperty ResolveBindingPayloadRoot(SerializedProperty bindingProperty,
                                                               SerializedProperty moduleDefaultPayloadProperty)
    {
        if (bindingProperty == null)
            return moduleDefaultPayloadProperty;

        SerializedProperty useOverrideProperty = bindingProperty.FindPropertyRelative("useOverridePayload");

        if (useOverrideProperty == null || !useOverrideProperty.boolValue)
            return moduleDefaultPayloadProperty;

        SerializedProperty overridePayloadProperty = bindingProperty.FindPropertyRelative("overridePayload");

        if (overridePayloadProperty == null)
            return moduleDefaultPayloadProperty;

        return overridePayloadProperty;
    }

    /// <summary>
    /// Copies required SpawnObject object references from the selected module default into a newly enabled override payload.
    /// </summary>
    /// <param name="overridePayloadProperty">Override payload serialized root.</param>
    /// <param name="moduleDefaultPayloadProperty">Selected module default payload serialized root.</param>
    /// <param name="moduleKind">Resolved module kind for the selected binding.</param>
    private static void EnsureOverridePayloadInheritedReferences(SerializedProperty overridePayloadProperty,
                                                                 SerializedProperty moduleDefaultPayloadProperty,
                                                                 PowerUpModuleKind moduleKind)
    {
        if (moduleKind != PowerUpModuleKind.SpawnObject)
            return;

        if (overridePayloadProperty == null || moduleDefaultPayloadProperty == null)
            return;

        SerializedProperty overrideBombProperty = overridePayloadProperty.FindPropertyRelative("bomb");
        SerializedProperty defaultBombProperty = moduleDefaultPayloadProperty.FindPropertyRelative("bomb");

        if (overrideBombProperty == null || defaultBombProperty == null)
            return;

        bool changed = CopySpawnObjectPayloadIfPrefabMissing(overrideBombProperty, defaultBombProperty);

        if (!changed)
        {
            changed = CopyMissingObjectReference(overrideBombProperty, defaultBombProperty, "bombPrefab") || changed;
            bool copiedExplosionVfxPrefab = CopyMissingObjectReference(overrideBombProperty, defaultBombProperty, "explosionVfxPrefab");

            if (copiedExplosionVfxPrefab)
            {
                CopyPropertyValue(overrideBombProperty, defaultBombProperty, "scaleVfxToRadius");
                CopyPropertyValue(overrideBombProperty, defaultBombProperty, "vfxScaleMultiplier");
            }

            changed = copiedExplosionVfxPrefab || changed;
        }

        if (!changed)
            return;

        overridePayloadProperty.serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Initializes an empty SpawnObject override from the selected module default before designers edit individual values.
    /// </summary>
    /// <param name="targetRootProperty">Override Bomb payload root that receives module-default values.</param>
    /// <param name="sourceRootProperty">Module-default Bomb payload root that provides values.</param>
    /// <returns>True when the full SpawnObject payload was copied.</returns>
    private static bool CopySpawnObjectPayloadIfPrefabMissing(SerializedProperty targetRootProperty,
                                                              SerializedProperty sourceRootProperty)
    {
        if (targetRootProperty == null || sourceRootProperty == null)
            return false;

        SerializedProperty targetPrefabProperty = targetRootProperty.FindPropertyRelative("bombPrefab");
        SerializedProperty sourcePrefabProperty = sourceRootProperty.FindPropertyRelative("bombPrefab");

        if (HasObjectReference(targetPrefabProperty) || !HasObjectReference(sourcePrefabProperty))
            return false;

        CopyPayloadProperties(targetRootProperty, sourceRootProperty, spawnObjectPayloadPropertyNames);
        return true;
    }

    /// <summary>
    /// Copies a known set of serialized payload fields between matching roots.
    /// </summary>
    /// <param name="targetRootProperty">Payload root that receives values.</param>
    /// <param name="sourceRootProperty">Payload root that provides values.</param>
    /// <param name="relativePaths">Relative serialized property paths to copy.</param>
    private static void CopyPayloadProperties(SerializedProperty targetRootProperty,
                                              SerializedProperty sourceRootProperty,
                                              IReadOnlyList<string> relativePaths)
    {
        if (relativePaths == null)
            return;

        for (int pathIndex = 0; pathIndex < relativePaths.Count; pathIndex++)
            CopyPropertyValue(targetRootProperty, sourceRootProperty, relativePaths[pathIndex]);
    }

    /// <summary>
    /// Copies one object reference only when the target override reference is empty.
    /// </summary>
    /// <param name="targetRootProperty">Override payload root that receives the reference.</param>
    /// <param name="sourceRootProperty">Module-default payload root that provides the reference.</param>
    /// <param name="relativePath">Relative serialized object-reference path to copy.</param>
    /// <returns>True when a reference was copied.</returns>
    private static bool CopyMissingObjectReference(SerializedProperty targetRootProperty,
                                                   SerializedProperty sourceRootProperty,
                                                   string relativePath)
    {
        if (targetRootProperty == null || sourceRootProperty == null || string.IsNullOrWhiteSpace(relativePath))
            return false;

        SerializedProperty targetProperty = targetRootProperty.FindPropertyRelative(relativePath);
        SerializedProperty sourceProperty = sourceRootProperty.FindPropertyRelative(relativePath);

        if (targetProperty == null || sourceProperty == null)
            return false;

        if (targetProperty.propertyType != SerializedPropertyType.ObjectReference ||
            sourceProperty.propertyType != SerializedPropertyType.ObjectReference)
        {
            return false;
        }

        if (targetProperty.objectReferenceValue != null || sourceProperty.objectReferenceValue == null)
            return false;

        targetProperty.objectReferenceValue = sourceProperty.objectReferenceValue;
        return true;
    }

    /// <summary>
    /// Copies one primitive serialized value between matching payload roots.
    /// </summary>
    /// <param name="targetRootProperty">Override payload root that receives the value.</param>
    /// <param name="sourceRootProperty">Module-default payload root that provides the value.</param>
    /// <param name="relativePath">Relative serialized property path to copy.</param>
    private static void CopyPropertyValue(SerializedProperty targetRootProperty,
                                          SerializedProperty sourceRootProperty,
                                          string relativePath)
    {
        if (targetRootProperty == null || sourceRootProperty == null || string.IsNullOrWhiteSpace(relativePath))
            return;

        SerializedProperty targetProperty = targetRootProperty.FindPropertyRelative(relativePath);
        SerializedProperty sourceProperty = sourceRootProperty.FindPropertyRelative(relativePath);

        if (targetProperty == null || sourceProperty == null || targetProperty.propertyType != sourceProperty.propertyType)
            return;

        switch (targetProperty.propertyType)
        {
            case SerializedPropertyType.Boolean:
                targetProperty.boolValue = sourceProperty.boolValue;
                break;
            case SerializedPropertyType.Enum:
                targetProperty.enumValueIndex = sourceProperty.enumValueIndex;
                break;
            case SerializedPropertyType.Float:
                targetProperty.floatValue = sourceProperty.floatValue;
                break;
            case SerializedPropertyType.ObjectReference:
                targetProperty.objectReferenceValue = sourceProperty.objectReferenceValue;
                break;
            case SerializedPropertyType.Vector3:
                targetProperty.vector3Value = sourceProperty.vector3Value;
                break;
        }
    }

    /// <summary>
    /// Adds contextual warnings for override payloads that would compile into missing runtime behavior.
    /// </summary>
    /// <param name="overrideContainer">UI container that receives warning boxes.</param>
    /// <param name="overridePayloadProperty">Override payload serialized root.</param>
    /// <param name="moduleDefaultPayloadProperty">Selected module default payload serialized root.</param>
    /// <param name="moduleKind">Resolved module kind for the selected binding.</param>
    private static void AddOverridePayloadWarnings(VisualElement overrideContainer,
                                                   SerializedProperty overridePayloadProperty,
                                                   SerializedProperty moduleDefaultPayloadProperty,
                                                   PowerUpModuleKind moduleKind)
    {
        if (overrideContainer == null || moduleKind != PowerUpModuleKind.SpawnObject)
            return;

        SerializedProperty overrideBombProperty = overridePayloadProperty != null
            ? overridePayloadProperty.FindPropertyRelative("bomb")
            : null;
        SerializedProperty defaultBombProperty = moduleDefaultPayloadProperty != null
            ? moduleDefaultPayloadProperty.FindPropertyRelative("bomb")
            : null;
        SerializedProperty overridePrefabProperty = overrideBombProperty != null
            ? overrideBombProperty.FindPropertyRelative("bombPrefab")
            : null;
        SerializedProperty defaultPrefabProperty = defaultBombProperty != null
            ? defaultBombProperty.FindPropertyRelative("bombPrefab")
            : null;

        if (HasObjectReference(overridePrefabProperty) || HasObjectReference(defaultPrefabProperty))
            return;

        HelpBox missingPrefabBox = new HelpBox("SpawnObject override has no Spawn Prefab and the referenced module default has none. Bomb activation will be ignored at runtime.",
                                               HelpBoxMessageType.Warning);
        overrideContainer.Add(missingPrefabBox);
    }

    /// <summary>
    /// Checks whether one serialized object-reference property currently points to an asset or object.
    /// </summary>
    /// <param name="property">Serialized property to inspect.</param>
    /// <returns>True when the property contains a non-null object reference.</returns>
    private static bool HasObjectReference(SerializedProperty property)
    {
        return property != null &&
               property.propertyType == SerializedPropertyType.ObjectReference &&
               property.objectReferenceValue != null;
    }

    private static void AddField(VisualElement parent, SerializedProperty property, string label)
    {
        if (parent == null)
            return;

        if (property == null)
            return;

        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;
        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property, scalingRulesProperty, label);
        parent.Add(field);
    }

    private static void ApplyFieldAlignedBoxStyle(VisualElement boxElement)
    {
        if (boxElement == null)
            return;

        float leftMargin = EditorGUIUtility.labelWidth + 4f;

        if (leftMargin < 130f)
            leftMargin = 130f;

        boxElement.style.marginLeft = leftMargin;
        boxElement.style.marginRight = 2f;
    }

    private static string ResolveInitialModuleId(string currentId, List<string> options)
    {
        if (options == null || options.Count == 0)
            return string.Empty;

        for (int index = 0; index < options.Count; index++)
        {
            if (string.Equals(options[index], currentId, System.StringComparison.OrdinalIgnoreCase))
                return options[index];
        }

        return options[0];
    }

    private static List<string> BuildModuleIdOptions(SerializedObject serializedObject)
    {
        List<string> options = new List<string>();

        if (serializedObject == null)
            return options;

        SerializedProperty modulesProperty = serializedObject.FindProperty("moduleDefinitions");

        if (modulesProperty == null)
            return options;

        for (int index = 0; index < modulesProperty.arraySize; index++)
        {
            SerializedProperty moduleElement = modulesProperty.GetArrayElementAtIndex(index);

            if (moduleElement == null)
                continue;

            SerializedProperty moduleIdProperty = moduleElement.FindPropertyRelative("moduleId");

            if (moduleIdProperty == null)
                continue;

            string moduleId = moduleIdProperty.stringValue;

            if (string.IsNullOrWhiteSpace(moduleId))
                continue;

            if (ContainsOption(options, moduleId))
                continue;

            options.Add(moduleId);
        }

        return options;
    }

    private static bool ContainsOption(List<string> options, string value)
    {
        if (options == null)
            return false;

        for (int index = 0; index < options.Count; index++)
        {
            if (string.Equals(options[index], value, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static PowerUpModuleKind ResolveModuleKindFromEnumProperty(SerializedProperty moduleKindProperty)
    {
        IReadOnlyList<PowerUpModuleKind> options = PowerUpModuleEnumDescriptions.ModuleKindOptions;

        if (moduleKindProperty == null || moduleKindProperty.propertyType != SerializedPropertyType.Enum)
            return options.Count > 0 ? options[0] : default;

        int enumValue = moduleKindProperty.enumValueIndex;

        for (int index = 0; index < options.Count; index++)
        {
            if ((int)options[index] != enumValue)
                continue;

            return options[index];
        }

        return options.Count > 0 ? options[0] : default;
    }

    private static PowerUpModuleStage ResolveStage(SerializedProperty stageProperty)
    {
        IReadOnlyList<PowerUpModuleStage> options = PowerUpModuleEnumDescriptions.StageOptions;

        if (stageProperty == null || stageProperty.propertyType != SerializedPropertyType.Enum)
            return options.Count > 0 ? options[0] : default;

        int enumValue = stageProperty.enumValueIndex;

        for (int index = 0; index < options.Count; index++)
        {
            if ((int)options[index] != enumValue)
                continue;

            return options[index];
        }

        return options.Count > 0 ? options[0] : default;
    }
    #endregion
}
