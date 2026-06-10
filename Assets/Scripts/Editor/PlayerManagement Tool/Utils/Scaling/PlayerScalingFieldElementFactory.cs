using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds reusable UI Toolkit controls for fields that support Add Scaling with typed formula validation.
/// </summary>
public static class PlayerScalingFieldElementFactory
{
    #region Constants
    private const float FormulaIndent = 16f;
    private const float VectorComponentIndent = 12f;
    private const float ToggleMinWidth = 100f;
    private const float DebugToggleMinWidth = 150f;
    private const float DebugColorFieldWidth = 60f;
    private const float AvailableVariablesBoxHeight = 46f;
    #endregion

    #region Fields
    private static readonly Dictionary<string, Action> formulaFieldRefreshByKey = new Dictionary<string, Action>(StringComparer.Ordinal);
    private static string activeFormulaFieldKey = string.Empty;
    private static int contextGeneration;
    #endregion

    #region Constructors
    /// <summary>
    /// Tracks a monotonically increasing context generation so formula elements can skip redundant
    /// revalidation on attach when the cross-panel selection context did not change while detached.
    /// </summary>
    static PlayerScalingFieldElementFactory()
    {
        PlayerManagementSelectionContext.ContextChanged += () => contextGeneration++;
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates a property editor for the provided property, adding scaling controls when the property supports formula scaling.
    /// </summary>
    /// <param name="targetProperty">Target serialized property to render.</param>
    /// <param name="scalingRulesProperty">Serialized List&lt;PlayerStatScalingRule&gt; used for Add Scaling state.</param>
    /// <param name="labelOverride">Optional field label override.</param>
    /// <param name="allowedVariables">Optional formula variable whitelist for validation.</param>
    /// <param name="allowTokenScaling">True when string token properties should expose Add Scaling.</param>
    /// <returns>Configured VisualElement ready to add into editor layout.</returns>
    public static VisualElement CreateField(SerializedProperty targetProperty,
                                            SerializedProperty scalingRulesProperty,
                                            string labelOverride = null,
                                            ISet<string> allowedVariables = null,
                                            bool allowTokenScaling = false)
    {
        if (targetProperty == null)
            return CreateMissingLabel("Missing serialized field.");

        if (scalingRulesProperty == null)
        {
            PropertyField fallbackField = string.IsNullOrWhiteSpace(labelOverride)
                ? new PropertyField(targetProperty)
                : new PropertyField(targetProperty, labelOverride);
            fallbackField.BindProperty(targetProperty);
            fallbackField.RegisterCallback<SerializedPropertyChangeEvent>(evt => PlayerManagementDraftSession.MarkDirty());
            return fallbackField;
        }

        if (IsVectorProperty(targetProperty))
            return CreateVectorField(targetProperty, scalingRulesProperty, labelOverride, allowedVariables);

        if (!PlayerScalingFormulaEditorUtility.SupportsScalingTarget(targetProperty) ||
            targetProperty.propertyType == SerializedPropertyType.String && !allowTokenScaling)
        {
            PropertyField fallbackField = string.IsNullOrWhiteSpace(labelOverride)
                ? new PropertyField(targetProperty)
                : new PropertyField(targetProperty, labelOverride);
            fallbackField.BindProperty(targetProperty);
            fallbackField.RegisterCallback<SerializedPropertyChangeEvent>(evt => PlayerManagementDraftSession.MarkDirty());
            return fallbackField;
        }

        string statKey = PlayerScalingStatKeyUtility.BuildStatKey(targetProperty);
        string fieldKey = BuildFormulaFieldKey(targetProperty);
        bool supportsRuntimeDebug = targetProperty.propertyType == SerializedPropertyType.Integer ||
                                    targetProperty.propertyType == SerializedPropertyType.Float;
        ISet<string> cachedAllowedVariables = null;
        IReadOnlyDictionary<string, PlayerFormulaValueType> cachedVariableTypes = null;
        IReadOnlyDictionary<string, PlayerScalableStatType> cachedDisplayTypes = null;
        string cachedHelperText = string.Empty;
        bool formulaMetadataResolved = false;
        bool helperTextResolved = false;
        int resolvedContextGeneration = -1;
        VisualElement root = new VisualElement();
        root.style.flexDirection = FlexDirection.Column;

        VisualElement firstRow = new VisualElement();
        firstRow.style.flexDirection = FlexDirection.Row;
        firstRow.style.alignItems = Align.Center;
        root.Add(firstRow);

        PropertyField valueField = string.IsNullOrWhiteSpace(labelOverride)
            ? new PropertyField(targetProperty)
            : new PropertyField(targetProperty, labelOverride);
        valueField.style.flexGrow = 1f;
        valueField.BindProperty(targetProperty);
        valueField.RegisterCallback<SerializedPropertyChangeEvent>(evt => PlayerManagementDraftSession.MarkDirty());
        firstRow.Add(valueField);

        Toggle addScalingToggle = new Toggle("Add Scaling");
        addScalingToggle.style.marginLeft = 6f;
        addScalingToggle.style.minWidth = ToggleMinWidth;
        firstRow.Add(addScalingToggle);

        VisualElement formulaContainer = new VisualElement();
        formulaContainer.style.marginLeft = FormulaIndent;
        formulaContainer.style.marginTop = 2f;
        formulaContainer.style.marginBottom = 2f;
        root.Add(formulaContainer);

        VisualElement formulaRow = new VisualElement();
        formulaRow.style.flexDirection = FlexDirection.Row;
        formulaRow.style.alignItems = Align.Center;
        formulaContainer.Add(formulaRow);

        TextField formulaField = new TextField("Formula");
        formulaField.isDelayed = false;
        formulaField.tooltip = string.Format("Use [this], [ScalableStatName], true/false, quoted token literals such as \"Fire\", and, on enum targets, bracketed enum members such as [FixedHits]. Supported operators: + - * / ^, < <= > >=, == !=, && ||, !, ?:. Supported functions: {0}. switch() syntax: switch(condition, case:value, ..., fallback).",
                                            PlayerFormulaFunctionUtility.SupportedFunctionsLabel);
        formulaField.style.flexGrow = 1f;
        formulaRow.Add(formulaField);

        Toggle debugInConsoleToggle = new Toggle("Debug in Console");
        debugInConsoleToggle.tooltip = "Editor-only runtime logs for scalable stat variables referenced by this formula.";
        debugInConsoleToggle.style.marginLeft = 8f;
        debugInConsoleToggle.style.minWidth = DebugToggleMinWidth;
        formulaRow.Add(debugInConsoleToggle);

        ColorField debugColorField = new ColorField();
        debugColorField.tooltip = "Editor-only color used for this specific runtime scaling debug line.";
        debugColorField.showAlpha = false;
        debugColorField.style.marginLeft = 6f;
        debugColorField.style.minWidth = DebugColorFieldWidth;
        debugColorField.style.maxWidth = DebugColorFieldWidth;
        debugColorField.style.flexShrink = 0f;
        formulaRow.Add(debugColorField);

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        warningBox.style.display = DisplayStyle.None;
        formulaContainer.Add(warningBox);

        ScrollView availableVariablesScrollView = new ScrollView(ScrollViewMode.Vertical);
        availableVariablesScrollView.style.marginTop = 2f;
        availableVariablesScrollView.style.height = AvailableVariablesBoxHeight;
        availableVariablesScrollView.style.maxHeight = AvailableVariablesBoxHeight;
        availableVariablesScrollView.style.flexShrink = 0f;
        formulaContainer.Add(availableVariablesScrollView);

        Label availableVariablesLabel = new Label();
        availableVariablesLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        availableVariablesLabel.style.whiteSpace = WhiteSpace.Normal;
        availableVariablesLabel.style.flexShrink = 0f;
        availableVariablesScrollView.Add(availableVariablesLabel);

        RegisterFormulaFieldRefresher(fieldKey, RefreshFromSerializedState);
        root.RegisterCallback<DetachFromPanelEvent>(evt => UnregisterFormulaFieldRefresher(fieldKey));
        root.RegisterCallback<MouseDownEvent>(evt =>
        {
            SetActiveFormulaField(fieldKey);
        });
        root.RegisterCallback<FocusOutEvent>(evt =>
        {
            if (evt.relatedTarget is VisualElement nextFocusedElement && root.Contains(nextFocusedElement))
                return;

            ClearActiveFormulaField(fieldKey);
        });

        // Revalidate against the refreshed master scope whenever the cross-panel selection context
        // changes: side panels are no longer rebuilt on master selection, so a stale metadata cache
        // would otherwise keep false "unknown scalable stat" warnings (or stale helper text) alive
        // until the section is manually switched. The attach handler re-syncs only when the context
        // generation actually moved while the element was detached (window reopen, inactive tabs):
        // plain tab switches and the initial build therefore never pay a redundant revalidation.
        Action contextChangedHandler = () =>
        {
            formulaMetadataResolved = false;
            helperTextResolved = false;
            RefreshFromSerializedState();
        };
        root.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            PlayerManagementSelectionContext.ContextChanged += contextChangedHandler;

            if (resolvedContextGeneration != contextGeneration)
                contextChangedHandler();
        });
        root.RegisterCallback<DetachFromPanelEvent>(evt =>
        {
            PlayerManagementSelectionContext.ContextChanged -= contextChangedHandler;
        });

        RefreshFromSerializedState();

        addScalingToggle.RegisterValueChangedCallback(evt =>
        {
            SerializedProperty ruleProperty = EnsureRuleProperty(scalingRulesProperty, targetProperty, statKey);

            if (ruleProperty == null)
                return;

            SerializedProperty addScalingProperty = ruleProperty.FindPropertyRelative("addScaling");
            SerializedProperty formulaProperty = ruleProperty.FindPropertyRelative("formula");
            SerializedProperty debugInConsoleProperty = ruleProperty.FindPropertyRelative("debugInConsole");

            if (addScalingProperty == null || formulaProperty == null || debugInConsoleProperty == null)
                return;

            ruleProperty.serializedObject.Update();
            addScalingProperty.boolValue = evt.newValue;

            if (!evt.newValue)
                debugInConsoleProperty.boolValue = false;

            ruleProperty.serializedObject.ApplyModifiedProperties();
            PlayerManagementDraftSession.MarkDirty();

            if (evt.newValue)
                SetActiveFormulaField(fieldKey);
            else
                RefreshFormulaField(fieldKey);
        });

        formulaField.RegisterValueChangedCallback(evt =>
        {
            SerializedProperty ruleProperty = EnsureRuleProperty(scalingRulesProperty, targetProperty, statKey);

            if (ruleProperty == null)
                return;

            SerializedProperty formulaProperty = ruleProperty.FindPropertyRelative("formula");
            SerializedProperty addScalingProperty = ruleProperty.FindPropertyRelative("addScaling");

            if (formulaProperty == null || addScalingProperty == null)
                return;

            ruleProperty.serializedObject.Update();
            addScalingProperty.boolValue = true;
            formulaProperty.stringValue = evt.newValue;
            ruleProperty.serializedObject.ApplyModifiedProperties();
            PlayerManagementDraftSession.MarkDirty();

            if (IsActiveFormulaField(fieldKey))
                RefreshFormulaField(fieldKey);
            else
                SetActiveFormulaField(fieldKey);
        });

        formulaField.RegisterCallback<FocusInEvent>(evt =>
        {
            SetActiveFormulaField(fieldKey);
        });

        debugInConsoleToggle.RegisterValueChangedCallback(evt =>
        {
            if (!supportsRuntimeDebug)
                return;

            SerializedProperty ruleProperty = EnsureRuleProperty(scalingRulesProperty, targetProperty, statKey);

            if (ruleProperty == null)
                return;

            SerializedProperty debugInConsoleProperty = ruleProperty.FindPropertyRelative("debugInConsole");
            SerializedProperty addScalingProperty = ruleProperty.FindPropertyRelative("addScaling");
            SerializedProperty debugColorProperty = ruleProperty.FindPropertyRelative("debugColor");

            if (debugInConsoleProperty == null || addScalingProperty == null)
                return;

            ruleProperty.serializedObject.Update();
            addScalingProperty.boolValue = true;
            debugInConsoleProperty.boolValue = evt.newValue;

            if (evt.newValue && debugColorProperty != null && debugColorProperty.colorValue.a <= 0.0001f)
                debugColorProperty.colorValue = PlayerStatScalingRule.GetDefaultDebugColor();

            ruleProperty.serializedObject.ApplyModifiedProperties();
            PlayerManagementDraftSession.MarkDirty();
            RefreshFromSerializedState();
        });

        debugColorField.RegisterValueChangedCallback(evt =>
        {
            if (!supportsRuntimeDebug)
                return;

            SerializedProperty ruleProperty = EnsureRuleProperty(scalingRulesProperty, targetProperty, statKey);

            if (ruleProperty == null)
                return;

            SerializedProperty debugColorProperty = ruleProperty.FindPropertyRelative("debugColor");
            SerializedProperty addScalingProperty = ruleProperty.FindPropertyRelative("addScaling");
            SerializedProperty debugInConsoleProperty = ruleProperty.FindPropertyRelative("debugInConsole");

            if (debugColorProperty == null || addScalingProperty == null || debugInConsoleProperty == null)
                return;

            Color selectedColor = evt.newValue;
            selectedColor.a = 1f;

            ruleProperty.serializedObject.Update();
            addScalingProperty.boolValue = true;
            debugInConsoleProperty.boolValue = true;
            debugColorProperty.colorValue = selectedColor;
            ruleProperty.serializedObject.ApplyModifiedProperties();
            PlayerManagementDraftSession.MarkDirty();
            RefreshFromSerializedState();
        });

        return root;

        void RefreshFromSerializedState()
        {
            SerializedProperty ruleProperty = FindRuleProperty(scalingRulesProperty,
                                                               targetProperty,
                                                               statKey,
                                                               false);
            bool addScaling = false;
            string formulaValue = string.Empty;
            bool debugInConsole = false;
            Color debugColor = PlayerStatScalingRule.GetDefaultDebugColor();

            if (ruleProperty != null)
            {
                SerializedProperty addScalingProperty = ruleProperty.FindPropertyRelative("addScaling");
                SerializedProperty formulaProperty = ruleProperty.FindPropertyRelative("formula");
                SerializedProperty debugInConsoleProperty = ruleProperty.FindPropertyRelative("debugInConsole");
                SerializedProperty debugColorProperty = ruleProperty.FindPropertyRelative("debugColor");

                if (addScalingProperty != null)
                    addScaling = addScalingProperty.boolValue;

                if (formulaProperty != null && !string.IsNullOrWhiteSpace(formulaProperty.stringValue))
                    formulaValue = formulaProperty.stringValue;

                if (debugInConsoleProperty != null)
                    debugInConsole = debugInConsoleProperty.boolValue;

                if (debugColorProperty != null)
                    debugColor = debugColorProperty.colorValue;
            }

            if (addScalingToggle.value != addScaling)
                addScalingToggle.SetValueWithoutNotify(addScaling);

            if (!string.Equals(formulaField.value, formulaValue, StringComparison.Ordinal))
                formulaField.SetValueWithoutNotify(formulaValue);

            if (debugInConsoleToggle.value != debugInConsole)
                debugInConsoleToggle.SetValueWithoutNotify(debugInConsole);

            if (!AreColorsApproximatelyEqual(debugColorField.value, debugColor))
            debugColorField.SetValueWithoutNotify(debugColor);

            formulaContainer.style.display = addScaling ? DisplayStyle.Flex : DisplayStyle.None;
            debugInConsoleToggle.style.display = addScaling && supportsRuntimeDebug ? DisplayStyle.Flex : DisplayStyle.None;
            debugColorField.style.display = addScaling && debugInConsole && supportsRuntimeDebug ? DisplayStyle.Flex : DisplayStyle.None;
            EnsureFormulaMetadataCache();
            string normalizedFormulaValue = PlayerScalingFormulaEditorUtility.NormalizeFormulaForTarget(formulaValue,
                                                                                                        targetProperty,
                                                                                                        cachedAllowedVariables);
            bool showAvailableVariables = addScaling && IsActiveFormulaField(fieldKey);

            if (showAvailableVariables)
            {
                EnsureHelperTextCache();
                availableVariablesLabel.text = cachedHelperText;
            }

            availableVariablesScrollView.style.display = showAvailableVariables
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            if (!addScaling)
            {
                warningBox.style.display = DisplayStyle.None;
                warningBox.text = string.Empty;
                return;
            }

            if (string.IsNullOrWhiteSpace(formulaValue))
            {
                warningBox.text = "Formula is required when Add Scaling is enabled.";
                warningBox.style.display = DisplayStyle.Flex;
                return;
            }

            PlayerFormulaValueType requiredResultType = PlayerScalingFormulaEditorUtility.ResolveRequiredResultType(targetProperty);

            if (PlayerScalingFormulaValidationUtility.TryValidateFormula(normalizedFormulaValue,
                                                                        cachedAllowedVariables,
                                                                        cachedVariableTypes,
                                                                        requiredResultType,
                                                                        requiredResultType,
                                                                        out string warningMessage))
            {
                warningBox.style.display = DisplayStyle.None;
                warningBox.text = string.Empty;
                return;
            }

            warningBox.text = warningMessage;
            warningBox.style.display = DisplayStyle.Flex;
        }

        void EnsureFormulaMetadataCache()
        {
            if (formulaMetadataResolved)
                return;

            cachedAllowedVariables = ResolveAllowedVariables(allowedVariables, scalingRulesProperty);
            cachedVariableTypes = ResolveAllowedVariableTypes(scalingRulesProperty);
            resolvedContextGeneration = contextGeneration;
            formulaMetadataResolved = true;
        }

        void EnsureHelperTextCache()
        {
            if (helperTextResolved)
                return;

            // Helper text enumerates every scoped variable: building it for all fields up front is
            // a measurable cost on payload open, so it resolves only for the focused formula field.
            EnsureFormulaMetadataCache();
            cachedDisplayTypes = ResolveAllowedDisplayTypes(scalingRulesProperty);
            cachedHelperText = PlayerScalingFormulaEditorUtility.BuildHelperText(cachedAllowedVariables,
                                                                                 cachedDisplayTypes,
                                                                                 targetProperty);
            helperTextResolved = true;
        }
    }
    #endregion

    #region Helpers
    private static VisualElement CreateMissingLabel(string message)
    {
        Label label = new Label(message);
        label.style.unityFontStyleAndWeight = FontStyle.Italic;
        return label;
    }

    private static VisualElement CreateVectorField(SerializedProperty vectorProperty,
                                                   SerializedProperty scalingRulesProperty,
                                                   string labelOverride,
                                                   ISet<string> allowedVariables)
    {
        VisualElement root = new VisualElement();
        root.style.flexDirection = FlexDirection.Column;

        PropertyField vectorField = string.IsNullOrWhiteSpace(labelOverride)
            ? new PropertyField(vectorProperty)
            : new PropertyField(vectorProperty, labelOverride);
        vectorField.BindProperty(vectorProperty);
        vectorField.RegisterCallback<SerializedPropertyChangeEvent>(evt => PlayerManagementDraftSession.MarkDirty());
        root.Add(vectorField);

        VisualElement componentsContainer = new VisualElement();
        componentsContainer.style.marginLeft = VectorComponentIndent;
        root.Add(componentsContainer);

        string baseLabel = string.IsNullOrWhiteSpace(labelOverride) ? vectorProperty.displayName : labelOverride;

        switch (vectorProperty.propertyType)
        {
            case SerializedPropertyType.Vector2:
            case SerializedPropertyType.Vector2Int:
                AddVectorComponentField(componentsContainer, vectorProperty, "x", baseLabel, scalingRulesProperty, allowedVariables);
                AddVectorComponentField(componentsContainer, vectorProperty, "y", baseLabel, scalingRulesProperty, allowedVariables);
                return root;
            case SerializedPropertyType.Vector3:
            case SerializedPropertyType.Vector3Int:
                AddVectorComponentField(componentsContainer, vectorProperty, "x", baseLabel, scalingRulesProperty, allowedVariables);
                AddVectorComponentField(componentsContainer, vectorProperty, "y", baseLabel, scalingRulesProperty, allowedVariables);
                AddVectorComponentField(componentsContainer, vectorProperty, "z", baseLabel, scalingRulesProperty, allowedVariables);
                return root;
            case SerializedPropertyType.Vector4:
                AddVectorComponentField(componentsContainer, vectorProperty, "x", baseLabel, scalingRulesProperty, allowedVariables);
                AddVectorComponentField(componentsContainer, vectorProperty, "y", baseLabel, scalingRulesProperty, allowedVariables);
                AddVectorComponentField(componentsContainer, vectorProperty, "z", baseLabel, scalingRulesProperty, allowedVariables);
                AddVectorComponentField(componentsContainer, vectorProperty, "w", baseLabel, scalingRulesProperty, allowedVariables);
                return root;
        }

        return root;
    }

    private static void AddVectorComponentField(VisualElement parent,
                                                SerializedProperty vectorProperty,
                                                string componentName,
                                                string baseLabel,
                                                SerializedProperty scalingRulesProperty,
                                                ISet<string> allowedVariables)
    {
        if (parent == null || vectorProperty == null)
            return;

        if (string.IsNullOrWhiteSpace(componentName))
            return;

        SerializedProperty componentProperty = vectorProperty.FindPropertyRelative(componentName);

        if (componentProperty == null)
            return;

        string componentLabel = string.IsNullOrWhiteSpace(baseLabel)
            ? string.Format(".{0}", componentName)
            : string.Format("{0}.{1}", baseLabel, componentName);
        VisualElement componentField = CreateField(componentProperty,
                                                   scalingRulesProperty,
                                                   componentLabel,
                                                   allowedVariables);
        parent.Add(componentField);
    }

    private static bool IsVectorProperty(SerializedProperty property)
    {
        if (property == null)
            return false;

        switch (property.propertyType)
        {
            case SerializedPropertyType.Vector2:
            case SerializedPropertyType.Vector3:
            case SerializedPropertyType.Vector4:
            case SerializedPropertyType.Vector2Int:
            case SerializedPropertyType.Vector3Int:
                return true;
        }

        return false;
    }

    /// <summary>
    /// Finds one scaling rule by stat key and, when needed, by resolved target property equivalence.
    /// </summary>
    /// <param name="scalingRulesProperty">Serialized scaling-rules array.</param>
    /// <param name="targetProperty">Current numeric property rendered by the field.</param>
    /// <param name="statKey">Current normalized stat key for the field.</param>
    /// <param name="autoRepairStatKey">When true, rewrites legacy rule keys to the current key.</param>
    /// <returns>Resolved scaling-rule property, or null when not found.</returns>
    private static SerializedProperty FindRuleProperty(SerializedProperty scalingRulesProperty,
                                                       SerializedProperty targetProperty,
                                                       string statKey,
                                                       bool autoRepairStatKey)
    {
        if (scalingRulesProperty == null)
            return null;

        if (string.IsNullOrWhiteSpace(statKey))
            return null;

        if (!scalingRulesProperty.isArray)
            return null;

        SerializedObject serializedObject = scalingRulesProperty.serializedObject;

        if (serializedObject == null)
            return null;

        for (int index = 0; index < scalingRulesProperty.arraySize; index++)
        {
            SerializedProperty ruleProperty = scalingRulesProperty.GetArrayElementAtIndex(index);

            if (ruleProperty == null)
                continue;

            SerializedProperty ruleStatKeyProperty = ruleProperty.FindPropertyRelative("statKey");

            if (ruleStatKeyProperty == null)
                continue;

            string ruleStatKey = ruleStatKeyProperty.stringValue;

            if (string.IsNullOrWhiteSpace(ruleStatKey))
                continue;

            if (string.Equals(ruleStatKey, statKey, StringComparison.Ordinal))
                return ruleProperty;

            if (targetProperty == null)
                continue;

            if (!PlayerScalingStatKeyUtility.TryFindPropertyByStatKey(serializedObject, ruleStatKey, out SerializedProperty resolvedProperty))
                continue;

            if (!AreSameSerializedProperty(resolvedProperty, targetProperty))
                continue;

            if (autoRepairStatKey)
                TryRepairRuleStatKey(ruleStatKeyProperty, statKey);

            return ruleProperty;
        }

        return null;
    }

    /// <summary>
    /// Ensures one scaling rule exists for the current target property and stat key.
    /// </summary>
    /// <param name="scalingRulesProperty">Serialized scaling-rules array.</param>
    /// <param name="targetProperty">Current numeric property rendered by the field.</param>
    /// <param name="statKey">Current normalized stat key for the field.</param>
    /// <returns>Existing or newly created scaling-rule property.</returns>
    private static SerializedProperty EnsureRuleProperty(SerializedProperty scalingRulesProperty,
                                                         SerializedProperty targetProperty,
                                                         string statKey)
    {
        SerializedProperty ruleProperty = FindRuleProperty(scalingRulesProperty,
                                                           targetProperty,
                                                           statKey,
                                                           true);

        if (ruleProperty != null)
            return ruleProperty;

        if (scalingRulesProperty == null)
            return null;

        if (!scalingRulesProperty.isArray)
            return null;

        scalingRulesProperty.serializedObject.Update();
        int insertIndex = scalingRulesProperty.arraySize;
        scalingRulesProperty.arraySize = insertIndex + 1;
        SerializedProperty insertedRule = scalingRulesProperty.GetArrayElementAtIndex(insertIndex);

        if (insertedRule == null)
        {
            scalingRulesProperty.serializedObject.ApplyModifiedProperties();
            return null;
        }

        SerializedProperty insertedStatKeyProperty = insertedRule.FindPropertyRelative("statKey");
        SerializedProperty insertedAddScalingProperty = insertedRule.FindPropertyRelative("addScaling");
        SerializedProperty insertedFormulaProperty = insertedRule.FindPropertyRelative("formula");
        SerializedProperty insertedDebugInConsoleProperty = insertedRule.FindPropertyRelative("debugInConsole");
        SerializedProperty insertedDebugColorProperty = insertedRule.FindPropertyRelative("debugColor");

        if (insertedStatKeyProperty != null)
            insertedStatKeyProperty.stringValue = statKey;

        if (insertedAddScalingProperty != null)
            insertedAddScalingProperty.boolValue = false;

        if (insertedFormulaProperty != null)
            insertedFormulaProperty.stringValue = string.Empty;

        if (insertedDebugInConsoleProperty != null)
            insertedDebugInConsoleProperty.boolValue = false;

        if (insertedDebugColorProperty != null)
            insertedDebugColorProperty.colorValue = PlayerStatScalingRule.GetDefaultDebugColor();

        scalingRulesProperty.serializedObject.ApplyModifiedProperties();
        return insertedRule;
    }

    /// <summary>
    /// Compares two serialized properties by owner object and exact property path.
    /// </summary>
    /// <param name="leftProperty">First property to compare.</param>
    /// <param name="rightProperty">Second property to compare.</param>
    /// <returns>True when both references point to the same serialized field.</returns>
    private static bool AreSameSerializedProperty(SerializedProperty leftProperty, SerializedProperty rightProperty)
    {
        if (leftProperty == null || rightProperty == null)
            return false;

        if (leftProperty.serializedObject == null || rightProperty.serializedObject == null)
            return false;

        if (leftProperty.serializedObject.targetObject != rightProperty.serializedObject.targetObject)
            return false;

        return string.Equals(leftProperty.propertyPath, rightProperty.propertyPath, StringComparison.Ordinal);
    }

    /// <summary>
    /// Updates one rule stat key when a legacy key still resolves to the current field.
    /// </summary>
    /// <param name="ruleStatKeyProperty">Serialized statKey property inside the rule element.</param>
    /// <param name="newStatKey">Current normalized stat key to store.</param>

    private static void TryRepairRuleStatKey(SerializedProperty ruleStatKeyProperty, string newStatKey)
    {
        if (ruleStatKeyProperty == null)
            return;

        if (string.IsNullOrWhiteSpace(newStatKey))
            return;

        if (string.Equals(ruleStatKeyProperty.stringValue, newStatKey, StringComparison.Ordinal))
            return;

        SerializedObject serializedObject = ruleStatKeyProperty.serializedObject;

        if (serializedObject == null)
            return;

        serializedObject.Update();
        ruleStatKeyProperty.stringValue = newStatKey;
        serializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
    }

    private static bool AreColorsApproximatelyEqual(Color leftColor, Color rightColor)
    {
        if (Mathf.Abs(leftColor.r - rightColor.r) > 0.0001f)
            return false;

        if (Mathf.Abs(leftColor.g - rightColor.g) > 0.0001f)
            return false;

        if (Mathf.Abs(leftColor.b - rightColor.b) > 0.0001f)
            return false;

        return Mathf.Abs(leftColor.a - rightColor.a) <= 0.0001f;
    }

    private static ISet<string> ResolveAllowedVariables(ISet<string> inputAllowedVariables, SerializedProperty scalingRulesProperty)
    {
        HashSet<string> mergedVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (inputAllowedVariables != null)
        {
            foreach (string variable in inputAllowedVariables)
                mergedVariables.Add(variable);
        }

        if (scalingRulesProperty == null)
            return mergedVariables;

        SerializedObject serializedObject = scalingRulesProperty.serializedObject;

        if (serializedObject == null)
            return mergedVariables;

        HashSet<string> scopedVariables = PlayerScalingFormulaValidationUtility.BuildScopedVariableSet(serializedObject);

        foreach (string variable in scopedVariables)
            mergedVariables.Add(variable);

        return mergedVariables;
    }

    private static IReadOnlyDictionary<string, PlayerFormulaValueType> ResolveAllowedVariableTypes(SerializedProperty scalingRulesProperty)
    {
        Dictionary<string, PlayerFormulaValueType> mergedTypes = new Dictionary<string, PlayerFormulaValueType>(StringComparer.OrdinalIgnoreCase);

        if (scalingRulesProperty == null)
            return mergedTypes;

        SerializedObject serializedObject = scalingRulesProperty.serializedObject;

        if (serializedObject == null)
            return mergedTypes;

        SerializedProperty scalableStatsProperty = serializedObject.FindProperty("scalableStats");

        if (scalableStatsProperty != null)
        {
            Dictionary<string, PlayerFormulaValueType> localTypes = PlayerScalingFormulaValidationUtility.BuildVariableTypeMap(scalableStatsProperty);

            foreach (KeyValuePair<string, PlayerFormulaValueType> entry in localTypes)
                mergedTypes[entry.Key] = entry.Value;
        }

        Dictionary<string, PlayerFormulaValueType> scopedTypes = PlayerScalingFormulaValidationUtility.BuildScopedVariableTypeMap(serializedObject);

        foreach (KeyValuePair<string, PlayerFormulaValueType> entry in scopedTypes)
            mergedTypes[entry.Key] = entry.Value;

        return mergedTypes;
    }

    private static IReadOnlyDictionary<string, PlayerScalableStatType> ResolveAllowedDisplayTypes(SerializedProperty scalingRulesProperty)
    {
        Dictionary<string, PlayerScalableStatType> mergedTypes = new Dictionary<string, PlayerScalableStatType>(StringComparer.OrdinalIgnoreCase);

        if (scalingRulesProperty == null)
            return mergedTypes;

        SerializedObject serializedObject = scalingRulesProperty.serializedObject;

        if (serializedObject == null)
            return mergedTypes;

        SerializedProperty scalableStatsProperty = serializedObject.FindProperty("scalableStats");

        if (scalableStatsProperty != null)
        {
            Dictionary<string, PlayerScalableStatType> localTypes = PlayerScalingFormulaValidationUtility.BuildScalableStatTypeMap(scalableStatsProperty);

            foreach (KeyValuePair<string, PlayerScalableStatType> entry in localTypes)
                mergedTypes[entry.Key] = entry.Value;
        }

        Dictionary<string, PlayerScalableStatType> scopedTypes = PlayerScalingFormulaValidationUtility.BuildScopedScalableStatTypeMap(serializedObject);

        foreach (KeyValuePair<string, PlayerScalableStatType> entry in scopedTypes)
            mergedTypes[entry.Key] = entry.Value;

        return mergedTypes;
    }

    private static string BuildFormulaFieldKey(SerializedProperty property)
    {
        if (property == null || property.serializedObject == null || property.serializedObject.targetObject == null)
            return string.Empty;

        return string.Format("{0}:{1}",
                             property.serializedObject.targetObject.GetInstanceID(),
                             property.propertyPath);
    }

    private static bool IsActiveFormulaField(string fieldKey)
    {
        if (string.IsNullOrWhiteSpace(fieldKey))
            return false;

        return string.Equals(activeFormulaFieldKey, fieldKey, StringComparison.Ordinal);
    }

    private static void SetActiveFormulaField(string fieldKey)
    {
        if (string.IsNullOrWhiteSpace(fieldKey))
            return;

        if (string.Equals(activeFormulaFieldKey, fieldKey, StringComparison.Ordinal))
            return;

        string previousFieldKey = activeFormulaFieldKey;
        activeFormulaFieldKey = fieldKey;

        if (!string.IsNullOrWhiteSpace(previousFieldKey))
            RefreshFormulaField(previousFieldKey);

        RefreshFormulaField(fieldKey);
    }

    private static void ClearActiveFormulaField(string fieldKey)
    {
        if (string.IsNullOrWhiteSpace(fieldKey))
            return;

        if (!string.Equals(activeFormulaFieldKey, fieldKey, StringComparison.Ordinal))
            return;

        activeFormulaFieldKey = string.Empty;
        RefreshFormulaField(fieldKey);
    }

    private static void RegisterFormulaFieldRefresher(string fieldKey, Action refresher)
    {
        if (string.IsNullOrWhiteSpace(fieldKey) || refresher == null)
            return;

        formulaFieldRefreshByKey[fieldKey] = refresher;
    }

    private static void UnregisterFormulaFieldRefresher(string fieldKey)
    {
        if (string.IsNullOrWhiteSpace(fieldKey))
            return;

        formulaFieldRefreshByKey.Remove(fieldKey);

        if (string.Equals(activeFormulaFieldKey, fieldKey, StringComparison.Ordinal))
            activeFormulaFieldKey = string.Empty;
    }

    private static void RefreshFormulaField(string fieldKey)
    {
        if (string.IsNullOrWhiteSpace(fieldKey))
            return;

        if (!formulaFieldRefreshByKey.TryGetValue(fieldKey, out Action refresher))
            return;

        refresher?.Invoke();
    }

    private static void RefreshRegisteredFormulaFields()
    {
        if (formulaFieldRefreshByKey.Count <= 0)
            return;

        List<Action> refreshers = new List<Action>(formulaFieldRefreshByKey.Values);

        for (int index = 0; index < refreshers.Count; index++)
            refreshers[index]?.Invoke();
    }

    #endregion

    #endregion
}
