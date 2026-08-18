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

    #endregion

    #region Helper Types
    /// <summary>
    /// Stores per-drawer caches used to avoid repeated module option scans and payload rebuilds.
    /// </summary>
    private sealed class BindingDrawerState
    {
        #region Fields
        public readonly List<string> ModuleIdOptions;
        public string OverridePayloadRebuildKey;
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a binding drawer state from the module option list captured when the drawer opens.
        /// </summary>
        /// <param name="moduleIdOptions">Module IDs available to this binding drawer.</param>
        public BindingDrawerState(List<string> moduleIdOptions)
        {
            ModuleIdOptions = moduleIdOptions;
            OverridePayloadRebuildKey = string.Empty;
        }
        #endregion
    }
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

        BindingDrawerState drawerState = new BindingDrawerState(moduleIdOptions);
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
        overrideContainer.userData = drawerState;
        root.Add(overrideContainer);

        RefreshBindingUi(property,
                         property.serializedObject,
                         moduleIdProperty,
                         stageProperty,
                         useOverrideProperty,
                         overridePayloadProperty,
                         modulePopup,
                         moduleKindInfoBox,
                         overrideContainer,
                         drawerState,
                         false);

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
                             overrideContainer,
                             drawerState,
                             useOverrideProperty.boolValue);
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
                             overrideContainer,
                             drawerState,
                             useOverrideProperty != null && useOverrideProperty.boolValue);
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
                             overrideContainer,
                             drawerState,
                             changedProperty.boolValue);
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
                                         VisualElement overrideContainer,
                                         BindingDrawerState drawerState,
                                         bool seedOverridePayload)
    {
        string moduleId = moduleIdProperty != null ? moduleIdProperty.stringValue : string.Empty;
        List<string> options = ResolveModuleIdOptions(serializedObject, drawerState);

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

        if (moduleResolved &&
            seedOverridePayload &&
            useOverrideProperty != null &&
            useOverrideProperty.boolValue)
        {
            PowerUpModuleBindingOverridePayloadSeedUtility.SeedOverridePayload(serializedObject,
                                                                               bindingProperty,
                                                                               resolvedModuleId,
                                                                               moduleKind,
                                                                               overridePayloadProperty,
                                                                               moduleDefaultPayloadProperty);
        }

        RebuildOverrideContainer(overrideContainer,
                                 useOverrideProperty,
                                 overridePayloadProperty,
                                 moduleDefaultPayloadProperty,
                                 moduleResolved,
                                 moduleKind,
                                 bindingProperty,
                                 drawerState);
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
                                                 SerializedProperty bindingProperty,
                                                 BindingDrawerState drawerState)
    {
        if (overrideContainer == null)
            return;

        bool showOverride = useOverrideProperty != null && useOverrideProperty.boolValue;
        overrideContainer.style.display = showOverride ? DisplayStyle.Flex : DisplayStyle.None;

        bool showToggleDurationOption = ShouldShowToggleDurationOption(bindingProperty, moduleKind);
        string rebuildKey = BuildOverridePayloadRebuildKey(showOverride,
                                                           moduleResolved,
                                                           moduleKind,
                                                           useOverrideProperty,
                                                           overridePayloadProperty,
                                                           moduleDefaultPayloadProperty,
                                                           bindingProperty,
                                                           showToggleDurationOption);
        BindingDrawerState resolvedDrawerState = ResolveDrawerState(overrideContainer, drawerState);

        if (resolvedDrawerState != null &&
            string.Equals(resolvedDrawerState.OverridePayloadRebuildKey, rebuildKey, System.StringComparison.Ordinal))
        {
            return;
        }

        if (resolvedDrawerState != null)
            resolvedDrawerState.OverridePayloadRebuildKey = rebuildKey;

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
        bool showActiveProjectileConcurrencyOption = ShouldShowActiveProjectileConcurrencyOption(bindingProperty, moduleKind);
        bool hasOwningResourceGate = showActiveProjectileConcurrencyOption && HasEnabledOwningResourceGate(bindingProperty);
        PowerUpModuleDefinitionPropertyDrawer.BuildPayloadEditor(overrideContainer,
                                                                 payloadProperty,
                                                                 moduleKind,
                                                                 payloadLabel,
                                                                 showActiveTriggerCharacterTuningOption,
                                                                 showToggleDurationOption,
                                                                 showActiveProjectileConcurrencyOption,
                                                                 hasOwningResourceGate);
    }

    /// <summary>
    /// Resolves whether a Returning Projectiles binding belongs to a non-toggleable active power-up.
    /// </summary>
    /// <param name="bindingProperty">Serialized module binding currently being drawn.</param>
    /// <param name="moduleKind">Resolved module kind for the selected binding.</param>
    /// <returns>True when concurrent live-projectile control affects the owning active.</returns>
    private static bool ShouldShowActiveProjectileConcurrencyOption(SerializedProperty bindingProperty,
                                                                    PowerUpModuleKind moduleKind)
    {
        if (moduleKind != PowerUpModuleKind.ReturningProjectiles ||
            bindingProperty == null ||
            !IsBindingEnabled(bindingProperty))
        {
            return false;
        }

        if (!TryResolveOwningActivePowerUpProperty(bindingProperty, out SerializedProperty powerUpProperty))
            return false;

        return IsNonToggleableActive(powerUpProperty, false, false);
    }

    /// <summary>
    /// Reports whether the active that owns one binding contains an enabled Resource Gate module.
    /// </summary>
    /// <param name="bindingProperty">Serialized binding used to resolve the owning active and module catalog.</param>
    /// <returns>True when an enabled Resource Gate binding belongs to the same Active power-up.</returns>
    private static bool HasEnabledOwningResourceGate(SerializedProperty bindingProperty)
    {
        if (!TryResolveOwningActivePowerUpProperty(bindingProperty, out SerializedProperty powerUpProperty))
            return false;

        SerializedProperty bindingsProperty = powerUpProperty.FindPropertyRelative("moduleBindings");

        if (bindingsProperty == null)
            return false;

        for (int index = 0; index < bindingsProperty.arraySize; index++)
        {
            SerializedProperty candidateBinding = bindingsProperty.GetArrayElementAtIndex(index);

            if (candidateBinding == null || !IsBindingEnabled(candidateBinding))
                continue;

            string moduleId = ModularPowerUpBindingDrawerUtility.ResolveBindingModuleId(candidateBinding);

            if (!TryResolveModuleInfo(bindingProperty.serializedObject,
                                      moduleId,
                                      out PowerUpModuleKind moduleKind,
                                      out PowerUpModuleStage _,
                                      out string _,
                                      out SerializedProperty _))
            {
                continue;
            }

            if (moduleKind == PowerUpModuleKind.GateResource)
                return true;
        }

        return false;
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

        return IsNonToggleableActive(powerUpProperty, true, true);
    }

    /// <summary>
    /// Resolves whether Ghost Trail may match the owning toggleable active lifetime.
    /// </summary>
    /// <param name="bindingProperty">Serialized Ghost Trail binding currently being drawn.</param>
    /// <param name="moduleKind">Resolved module kind for the selected binding.</param>
    /// <returns>True when the binding belongs to an active containing an enabled toggleable Resource Gate.</returns>
    private static bool ShouldShowToggleDurationOption(SerializedProperty bindingProperty, PowerUpModuleKind moduleKind)
    {
        if (moduleKind != PowerUpModuleKind.GhostTrail)
            return false;

        if (bindingProperty == null || !IsBindingEnabled(bindingProperty))
            return false;

        if (!TryResolveOwningActivePowerUpProperty(bindingProperty, out SerializedProperty powerUpProperty))
            return false;

        SerializedProperty moduleBindingsProperty = powerUpProperty.FindPropertyRelative("moduleBindings");

        if (moduleBindingsProperty == null || !moduleBindingsProperty.isArray)
            return false;

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

            if (siblingModuleKind == PowerUpModuleKind.GateResource &&
                ResolveBindingResourceGateToggleable(siblingBindingProperty, moduleDefaultPayloadProperty))
            {
                return true;
            }
        }

        return false;
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
    /// Checks whether the owning active composition is non-toggleable and satisfies the requested trigger constraints.
    /// </summary>
    /// <param name="powerUpProperty">Serialized active power-up definition that owns the binding.</param>
    /// <param name="rejectHoldCharge">Whether Trigger Hold Charge makes the context unsupported.</param>
    /// <param name="requireTriggerableTool">Whether the active must contain a module that emits an immediate effect.</param>
    /// <returns>True when the active is non-toggleable and satisfies the requested constraints.</returns>
    private static bool IsNonToggleableActive(SerializedProperty powerUpProperty,
                                              bool rejectHoldCharge,
                                              bool requireTriggerableTool)
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
                case PowerUpModuleKind.AttractDrops:
                case PowerUpModuleKind.ReturningProjectiles:
                    hasTriggerableTool = true;
                    break;
            }

            if ((rejectHoldCharge && hasHoldCharge) || hasToggleableGate)
                return false;
        }

        return !requireTriggerableTool || hasTriggerableTool;
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

    /// <summary>
    /// Resolves module popup options without rescanning the module catalog on every binding refresh.
    /// </summary>
    /// <param name="serializedObject">Serialized preset used as fallback when no drawer cache exists.</param>
    /// <param name="drawerState">Optional per-drawer state with prebuilt module options.</param>
    /// <returns>Module ID options used by the binding popup.</returns>
    private static List<string> ResolveModuleIdOptions(SerializedObject serializedObject, BindingDrawerState drawerState)
    {
        if (drawerState != null &&
            drawerState.ModuleIdOptions != null &&
            drawerState.ModuleIdOptions.Count > 0)
        {
            return drawerState.ModuleIdOptions;
        }

        return BuildModuleIdOptions(serializedObject);
    }

    /// <summary>
    /// Resolves the cache object attached to the override payload container.
    /// </summary>
    /// <param name="overrideContainer">Container that owns override payload UI.</param>
    /// <param name="drawerState">Drawer state passed by the current refresh path.</param>
    /// <returns>Reusable drawer state, or null when none is available.</returns>
    private static BindingDrawerState ResolveDrawerState(VisualElement overrideContainer, BindingDrawerState drawerState)
    {
        if (drawerState != null)
            return drawerState;

        if (overrideContainer == null)
            return null;

        BindingDrawerState containerState = overrideContainer.userData as BindingDrawerState;
        return containerState;
    }

    /// <summary>
    /// Builds an identity key for the override payload subtree currently required by a binding.
    /// </summary>
    /// <param name="showOverride">True when override payload UI should be visible.</param>
    /// <param name="moduleResolved">True when the selected module resolves in the module catalog.</param>
    /// <param name="moduleKind">Resolved module kind that selects the payload drawer.</param>
    /// <param name="useOverrideProperty">Serialized flag that controls override visibility.</param>
    /// <param name="overridePayloadProperty">Serialized override payload root.</param>
    /// <param name="moduleDefaultPayloadProperty">Serialized default payload root of the selected module.</param>
    /// <param name="bindingProperty">Serialized binding property represented by this drawer.</param>
    /// <param name="showToggleDurationOption">True when contextual Ghost Trail toggle lifetime controls are available.</param>
    /// <returns>Stable key used to skip redundant payload rebuilds.</returns>
    private static string BuildOverridePayloadRebuildKey(bool showOverride,
                                                         bool moduleResolved,
                                                         PowerUpModuleKind moduleKind,
                                                         SerializedProperty useOverrideProperty,
                                                         SerializedProperty overridePayloadProperty,
                                                         SerializedProperty moduleDefaultPayloadProperty,
                                                         SerializedProperty bindingProperty,
                                                         bool showToggleDurationOption)
    {
        string useOverridePath = useOverrideProperty != null ? useOverrideProperty.propertyPath : string.Empty;
        string overridePayloadPath = overridePayloadProperty != null ? overridePayloadProperty.propertyPath : string.Empty;
        string defaultPayloadPath = moduleDefaultPayloadProperty != null ? moduleDefaultPayloadProperty.propertyPath : string.Empty;
        string bindingPath = bindingProperty != null ? bindingProperty.propertyPath : string.Empty;
        return string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}",
                             showOverride,
                             moduleResolved,
                             (int)moduleKind,
                             useOverridePath,
                             overridePayloadPath,
                             defaultPayloadPath,
                             bindingPath,
                             showToggleDurationOption);
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
