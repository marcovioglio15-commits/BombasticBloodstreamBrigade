using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Identifies how one offensive engagement settings block is used so labels, fallbacks and warnings stay context-accurate.
/// </summary>
internal enum EnemyOffensiveEngagementFeedbackEditorUsage
{
    GlobalHybrid = 0,
    PredictiveOverride = 1,
    BossPatternChange = 2,
    BossMixedPattern = 3,
    BossCandidate = 4
}

/// <summary>
/// Builds reusable UI Toolkit editors and warnings for offensive engagement feedback settings.
/// </summary>
internal static class EnemyOffensiveEngagementFeedbackDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the editor UI for one offensive engagement feedback settings block.
    /// </summary>
    /// <param name="settingsProperty">Serialized settings block to draw.</param>
    /// <param name="onValueChanged">Optional callback invoked after any serialized value changes.</param>
    /// <param name="usage">Runtime usage that controls timing labels, sprite fallback guidance and disabled-channel messaging.</param>
    /// <returns>The built visual element tree.</returns>
    public static VisualElement BuildSettingsEditor(SerializedProperty settingsProperty,
                                                    Action onValueChanged = null,
                                                    EnemyOffensiveEngagementFeedbackEditorUsage usage = EnemyOffensiveEngagementFeedbackEditorUsage.GlobalHybrid)
    {
        VisualElement root = new VisualElement();

        if (settingsProperty == null)
        {
            HelpBox missingSettingsBox = new HelpBox("Offensive engagement feedback settings are missing.", HelpBoxMessageType.Warning);
            root.Add(missingSettingsBox);
            return root;
        }

        SerializedProperty enableColorBlendProperty = settingsProperty.FindPropertyRelative("enableColorBlend");
        SerializedProperty colorBlendColorProperty = settingsProperty.FindPropertyRelative("colorBlendColor");
        SerializedProperty colorBlendLeadTimeSecondsProperty = settingsProperty.FindPropertyRelative("colorBlendLeadTimeSeconds");
        SerializedProperty colorBlendFadeOutSecondsProperty = settingsProperty.FindPropertyRelative("colorBlendFadeOutSeconds");
        SerializedProperty colorBlendMaximumBlendProperty = settingsProperty.FindPropertyRelative("colorBlendMaximumBlend");
        SerializedProperty enableBillboardProperty = settingsProperty.FindPropertyRelative("enableBillboard");
        SerializedProperty billboardSpriteProperty = settingsProperty.FindPropertyRelative("billboardSprite");
        SerializedProperty billboardColorProperty = settingsProperty.FindPropertyRelative("billboardColor");
        SerializedProperty billboardWorldOffsetProperty = settingsProperty.FindPropertyRelative("billboardWorldOffset");
        SerializedProperty billboardLeadTimeSecondsProperty = settingsProperty.FindPropertyRelative("billboardLeadTimeSeconds");
        SerializedProperty billboardBaseScaleProperty = settingsProperty.FindPropertyRelative("billboardBaseScale");
        SerializedProperty billboardPulseScaleMultiplierProperty = settingsProperty.FindPropertyRelative("billboardPulseScaleMultiplier");
        SerializedProperty billboardPulseExpandDurationSecondsProperty = settingsProperty.FindPropertyRelative("billboardPulseExpandDurationSeconds");
        SerializedProperty billboardPulseContractDurationSecondsProperty = settingsProperty.FindPropertyRelative("billboardPulseContractDurationSeconds");

        if (enableColorBlendProperty == null ||
            colorBlendColorProperty == null ||
            colorBlendLeadTimeSecondsProperty == null ||
            colorBlendFadeOutSecondsProperty == null ||
            colorBlendMaximumBlendProperty == null ||
            enableBillboardProperty == null ||
            billboardSpriteProperty == null ||
            billboardColorProperty == null ||
            billboardWorldOffsetProperty == null ||
            billboardLeadTimeSecondsProperty == null ||
            billboardBaseScaleProperty == null ||
            billboardPulseScaleMultiplierProperty == null ||
            billboardPulseExpandDurationSecondsProperty == null ||
            billboardPulseContractDurationSecondsProperty == null)
        {
            HelpBox invalidSettingsBox = new HelpBox("Offensive engagement feedback fields are incomplete.", HelpBoxMessageType.Warning);
            root.Add(invalidSettingsBox);
            return root;
        }

        bool usesPostTriggerDurations = usage == EnemyOffensiveEngagementFeedbackEditorUsage.BossPatternChange;
        bool usesHybridWarningWindows = usage == EnemyOffensiveEngagementFeedbackEditorUsage.GlobalHybrid ||
                                        usage == EnemyOffensiveEngagementFeedbackEditorUsage.BossMixedPattern ||
                                        usage == EnemyOffensiveEngagementFeedbackEditorUsage.BossCandidate;
        string colorTimingLabel = usesPostTriggerDurations
            ? "Color Blend Duration Seconds"
            : usesHybridWarningWindows
                ? "Color Warning Window Seconds"
                : "Color Blend Lead Time Seconds";
        string billboardTimingLabel = usesPostTriggerDurations
            ? "Billboard Duration Seconds"
            : usesHybridWarningWindows
                ? "Billboard Warning Window Seconds"
                : "Billboard Lead Time Seconds";

        if (usesHybridWarningWindows)
            root.Add(new HelpBox("Predictive modules use each warning window before their commit. Activation-only boss modules use the same value as a post-selection display duration.", HelpBoxMessageType.Info));

        VisualElement colorBlendGroup = CreateGroupContainer("Color Blend");
        VisualElement colorBlendSettings = new VisualElement();
        AddField(colorBlendGroup, enableColorBlendProperty, "Enable Color Blend", onValueChanged);
        AddField(colorBlendSettings, colorBlendColorProperty, "Color Blend Color", onValueChanged);
        AddField(colorBlendSettings, colorBlendLeadTimeSecondsProperty, colorTimingLabel, onValueChanged);
        AddField(colorBlendSettings, colorBlendFadeOutSecondsProperty, "Color Blend Fade Out Seconds", onValueChanged);
        AddField(colorBlendSettings, colorBlendMaximumBlendProperty, "Maximum Color Blend", onValueChanged);
        colorBlendGroup.Add(colorBlendSettings);
        RegisterDependentVisibility(colorBlendGroup,
                                    enableColorBlendProperty,
                                    colorBlendSettings);
        root.Add(colorBlendGroup);

        VisualElement billboardGroup = CreateGroupContainer("Billboard");
        VisualElement billboardSettings = new VisualElement();
        AddField(billboardGroup, enableBillboardProperty, "Enable Billboard", onValueChanged);
        AddField(billboardSettings, billboardSpriteProperty, "Billboard Sprite", onValueChanged);
        AddField(billboardSettings, billboardColorProperty, "Billboard Color", onValueChanged);
        AddField(billboardSettings, billboardWorldOffsetProperty, "Billboard World Offset", onValueChanged);
        AddField(billboardSettings, billboardLeadTimeSecondsProperty, billboardTimingLabel, onValueChanged);
        AddField(billboardSettings, billboardBaseScaleProperty, "Billboard Base Scale", onValueChanged);
        AddField(billboardSettings, billboardPulseScaleMultiplierProperty, "Billboard Pulse Scale Multiplier", onValueChanged);
        AddField(billboardSettings, billboardPulseExpandDurationSecondsProperty, "Billboard Pulse Expand Duration Seconds", onValueChanged);
        AddField(billboardSettings, billboardPulseContractDurationSecondsProperty, "Billboard Pulse Contract Duration Seconds", onValueChanged);
        billboardGroup.Add(billboardSettings);
        RegisterDependentVisibility(billboardGroup,
                                    enableBillboardProperty,
                                    billboardSettings);
        root.Add(billboardGroup);

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        warningBox.style.marginTop = 6f;
        root.Add(warningBox);
        RefreshWarnings(warningBox,
                        enableColorBlendProperty,
                        colorBlendColorProperty,
                        colorBlendLeadTimeSecondsProperty,
                        colorBlendFadeOutSecondsProperty,
                        colorBlendMaximumBlendProperty,
                        enableBillboardProperty,
                        billboardSpriteProperty,
                        billboardColorProperty,
                        billboardWorldOffsetProperty,
                        billboardLeadTimeSecondsProperty,
                        billboardBaseScaleProperty,
                        billboardPulseScaleMultiplierProperty,
                        billboardPulseExpandDurationSecondsProperty,
                        billboardPulseContractDurationSecondsProperty,
                        usage);
        RegisterWarningRefresh(root,
                               enableColorBlendProperty,
                               colorBlendColorProperty,
                               colorBlendLeadTimeSecondsProperty,
                               colorBlendFadeOutSecondsProperty,
                               colorBlendMaximumBlendProperty,
                               enableBillboardProperty,
                               billboardSpriteProperty,
                               billboardColorProperty,
                               billboardWorldOffsetProperty,
                               billboardLeadTimeSecondsProperty,
                               billboardBaseScaleProperty,
                               billboardPulseScaleMultiplierProperty,
                               billboardPulseExpandDurationSecondsProperty,
                               billboardPulseContractDurationSecondsProperty,
                               warningBox,
                               usage);
        return root;
    }

    /// <summary>
    /// Writes the runtime settings type's canonical defaults into a newly inserted serialized feedback block.
    /// </summary>
    /// <param name="settingsProperty">Serialized feedback block that must not inherit values cloned by Unity array insertion.</param>
    public static void ApplyDefaultValues(SerializedProperty settingsProperty)
    {
        if (settingsProperty == null)
            return;

        EnemyOffensiveEngagementFeedbackSettings defaults = new EnemyOffensiveEngagementFeedbackSettings();
        SetBoolean(settingsProperty, "enableColorBlend", defaults.EnableColorBlend);
        SetColor(settingsProperty, "colorBlendColor", defaults.ColorBlendColor);
        SetFloat(settingsProperty, "colorBlendLeadTimeSeconds", defaults.ColorBlendLeadTimeSeconds);
        SetFloat(settingsProperty, "colorBlendFadeOutSeconds", defaults.ColorBlendFadeOutSeconds);
        SetFloat(settingsProperty, "colorBlendMaximumBlend", defaults.ColorBlendMaximumBlend);
        SetBoolean(settingsProperty, "enableBillboard", defaults.EnableBillboard);
        SetObjectReference(settingsProperty, "billboardSprite", defaults.BillboardSprite);
        SetColor(settingsProperty, "billboardColor", defaults.BillboardColor);
        SetVector3(settingsProperty, "billboardWorldOffset", defaults.BillboardWorldOffset);
        SetFloat(settingsProperty, "billboardLeadTimeSeconds", defaults.BillboardLeadTimeSeconds);
        SetFloat(settingsProperty, "billboardBaseScale", defaults.BillboardBaseScale);
        SetFloat(settingsProperty, "billboardPulseScaleMultiplier", defaults.BillboardPulseScaleMultiplier);
        SetFloat(settingsProperty, "billboardPulseExpandDurationSeconds", defaults.BillboardPulseExpandDurationSeconds);
        SetFloat(settingsProperty, "billboardPulseContractDurationSeconds", defaults.BillboardPulseContractDurationSeconds);
    }

    /// <summary>
    /// Returns whether the currently selected module binding supports predictive engagement feedback in the provided catalog section.
    /// </summary>
    /// <param name="bindingProperty">Serialized module binding.</param>
    /// <param name="section">Catalog section used to interpret the binding.</param>
    /// <returns>True when the currently selected module kind maps to a supported engagement timing mode.</returns>
    public static bool SupportsDisplayTrigger(SerializedProperty bindingProperty, EnemyPatternModuleCatalogSection section)
    {
        if (bindingProperty == null)
            return false;

        SerializedProperty bindingEnabledProperty = bindingProperty.FindPropertyRelative("isEnabled");

        if (bindingEnabledProperty == null || !bindingEnabledProperty.boolValue)
            return false;

        SerializedProperty moduleIdProperty = bindingProperty.FindPropertyRelative("moduleId");

        if (moduleIdProperty == null)
            return false;

        string moduleId = moduleIdProperty.stringValue;

        if (string.IsNullOrWhiteSpace(moduleId))
            return false;

        bool resolvedModuleInfo = EnemyAdvancedPatternDrawerUtility.TryResolveModuleInfo(bindingProperty,
                                                                                         moduleId,
                                                                                         out EnemyPatternModuleKind moduleKind,
                                                                                         out string _);

        if (!resolvedModuleInfo)
            return false;

        return EnemyOffensiveEngagementSupportUtility.SupportsTimingMode(section, moduleKind);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Writes one nested serialized Boolean when the target field exists.
    /// </summary>
    /// <param name="parent">Serialized settings root.</param>
    /// <param name="relativeName">Nested field name.</param>
    /// <param name="value">Default Boolean value.</param>
    private static void SetBoolean(SerializedProperty parent, string relativeName, bool value)
    {
        SerializedProperty property = parent.FindPropertyRelative(relativeName);

        if (property != null)
            property.boolValue = value;
    }

    /// <summary>
    /// Writes one nested serialized float when the target field exists.
    /// </summary>
    /// <param name="parent">Serialized settings root.</param>
    /// <param name="relativeName">Nested field name.</param>
    /// <param name="value">Default float value.</param>
    private static void SetFloat(SerializedProperty parent, string relativeName, float value)
    {
        SerializedProperty property = parent.FindPropertyRelative(relativeName);

        if (property != null)
            property.floatValue = value;
    }

    /// <summary>
    /// Writes one nested serialized color when the target field exists.
    /// </summary>
    /// <param name="parent">Serialized settings root.</param>
    /// <param name="relativeName">Nested field name.</param>
    /// <param name="value">Default color value.</param>
    private static void SetColor(SerializedProperty parent, string relativeName, Color value)
    {
        SerializedProperty property = parent.FindPropertyRelative(relativeName);

        if (property != null)
            property.colorValue = value;
    }

    /// <summary>
    /// Writes one nested serialized vector when the target field exists.
    /// </summary>
    /// <param name="parent">Serialized settings root.</param>
    /// <param name="relativeName">Nested field name.</param>
    /// <param name="value">Default vector value.</param>
    private static void SetVector3(SerializedProperty parent, string relativeName, Vector3 value)
    {
        SerializedProperty property = parent.FindPropertyRelative(relativeName);

        if (property != null)
            property.vector3Value = value;
    }

    /// <summary>
    /// Writes one nested serialized object reference when the target field exists.
    /// </summary>
    /// <param name="parent">Serialized settings root.</param>
    /// <param name="relativeName">Nested field name.</param>
    /// <param name="value">Default object reference.</param>
    private static void SetObjectReference(SerializedProperty parent, string relativeName, UnityEngine.Object value)
    {
        SerializedProperty property = parent.FindPropertyRelative(relativeName);

        if (property != null)
            property.objectReferenceValue = value;
    }

    /// <summary>
    /// Creates one titled container used to visually separate major settings groups.
    /// </summary>
    /// <param name="title">Group title shown above the contained fields.</param>
    /// <returns>The created group container.</returns>
    private static VisualElement CreateGroupContainer(string title)
    {
        VisualElement container = new VisualElement();
        container.style.marginTop = 4f;

        Label header = new Label(title);
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        container.Add(header);
        return container;
    }

    /// <summary>
    /// Adds one bound property field and routes local change notifications through the optional callback.
    /// </summary>
    /// <param name="parent">Parent container that receives the property field.</param>
    /// <param name="property">Serialized property to bind.</param>
    /// <param name="label">UI label for the property field.</param>
    /// <param name="onValueChanged">Optional callback invoked after the field changes.</param>
    private static void AddField(VisualElement parent,
                                 SerializedProperty property,
                                 string label,
                                 Action onValueChanged)
    {
        if (parent == null || property == null)
            return;

        PropertyField field = new PropertyField(property, label);
        field.BindProperty(property);
        field.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            if (onValueChanged != null)
                onValueChanged();
        });
        parent.Add(field);
    }

    /// <summary>
    /// Keeps settings that depend on an enable toggle hidden until they can affect runtime presentation.
    /// </summary>
    /// <param name="root">Root that tracks serialized changes.</param>
    /// <param name="enabledProperty">Serialized channel toggle.</param>
    /// <param name="dependentSettings">Settings container whose visibility follows the toggle.</param>
    private static void RegisterDependentVisibility(VisualElement root,
                                                    SerializedProperty enabledProperty,
                                                    VisualElement dependentSettings)
    {
        if (root == null || enabledProperty == null || dependentSettings == null)
            return;

        dependentSettings.style.display = enabledProperty.boolValue
            ? DisplayStyle.Flex
            : DisplayStyle.None;
        root.TrackPropertyValue(enabledProperty, changedProperty =>
        {
            dependentSettings.style.display = changedProperty.boolValue
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        });
    }

    /// <summary>
    /// Registers warning refresh callbacks for every property that affects warning generation.
    /// </summary>
    /// <param name="root">Root visual element that tracks property changes.</param>
    /// <param name="enableColorBlendProperty">Serialized color-blend enable toggle.</param>
    /// <param name="colorBlendColorProperty">Serialized color-blend tint.</param>
    /// <param name="colorBlendLeadTimeSecondsProperty">Serialized predictive lead time or post-trigger duration.</param>
    /// <param name="colorBlendFadeOutSecondsProperty">Serialized color-blend fade-out duration.</param>
    /// <param name="colorBlendMaximumBlendProperty">Serialized maximum color blend.</param>
    /// <param name="enableBillboardProperty">Serialized billboard enable toggle.</param>
    /// <param name="billboardSpriteProperty">Serialized billboard sprite reference.</param>
    /// <param name="billboardColorProperty">Serialized billboard tint.</param>
    /// <param name="billboardWorldOffsetProperty">Serialized billboard world-space offset.</param>
    /// <param name="billboardLeadTimeSecondsProperty">Serialized predictive lead time or post-trigger duration.</param>
    /// <param name="billboardBaseScaleProperty">Serialized billboard base scale.</param>
    /// <param name="billboardPulseScaleMultiplierProperty">Serialized pulse scale multiplier.</param>
    /// <param name="billboardPulseExpandDurationSecondsProperty">Serialized pulse expansion duration.</param>
    /// <param name="billboardPulseContractDurationSecondsProperty">Serialized pulse contraction duration.</param>
    /// <param name="warningBox">Warning box refreshed after any tracked property changes.</param>
    /// <param name="usage">Settings usage that controls context-specific warning wording.</param>
    private static void RegisterWarningRefresh(VisualElement root,
                                               SerializedProperty enableColorBlendProperty,
                                               SerializedProperty colorBlendColorProperty,
                                               SerializedProperty colorBlendLeadTimeSecondsProperty,
                                               SerializedProperty colorBlendFadeOutSecondsProperty,
                                               SerializedProperty colorBlendMaximumBlendProperty,
                                               SerializedProperty enableBillboardProperty,
                                               SerializedProperty billboardSpriteProperty,
                                               SerializedProperty billboardColorProperty,
                                               SerializedProperty billboardWorldOffsetProperty,
                                               SerializedProperty billboardLeadTimeSecondsProperty,
                                               SerializedProperty billboardBaseScaleProperty,
                                               SerializedProperty billboardPulseScaleMultiplierProperty,
                                               SerializedProperty billboardPulseExpandDurationSecondsProperty,
                                               SerializedProperty billboardPulseContractDurationSecondsProperty,
                                               HelpBox warningBox,
                                               EnemyOffensiveEngagementFeedbackEditorUsage usage)
    {
        if (root == null || warningBox == null)
            return;

        SerializedProperty[] warningProperties = new SerializedProperty[]
        {
            enableColorBlendProperty,
            colorBlendColorProperty,
            colorBlendLeadTimeSecondsProperty,
            colorBlendFadeOutSecondsProperty,
            colorBlendMaximumBlendProperty,
            enableBillboardProperty,
            billboardSpriteProperty,
            billboardColorProperty,
            billboardWorldOffsetProperty,
            billboardLeadTimeSecondsProperty,
            billboardBaseScaleProperty,
            billboardPulseScaleMultiplierProperty,
            billboardPulseExpandDurationSecondsProperty,
            billboardPulseContractDurationSecondsProperty
        };

        for (int propertyIndex = 0; propertyIndex < warningProperties.Length; propertyIndex++)
        {
            SerializedProperty trackedProperty = warningProperties[propertyIndex];

            if (trackedProperty == null)
                continue;

            root.TrackPropertyValue(trackedProperty, changedProperty =>
            {
                RefreshWarnings(warningBox,
                                enableColorBlendProperty,
                                colorBlendColorProperty,
                                colorBlendLeadTimeSecondsProperty,
                                colorBlendFadeOutSecondsProperty,
                                colorBlendMaximumBlendProperty,
                                enableBillboardProperty,
                                billboardSpriteProperty,
                                billboardColorProperty,
                                billboardWorldOffsetProperty,
                                billboardLeadTimeSecondsProperty,
                                billboardBaseScaleProperty,
                                billboardPulseScaleMultiplierProperty,
                                billboardPulseExpandDurationSecondsProperty,
                                billboardPulseContractDurationSecondsProperty,
                                usage);
            });
        }
    }

    /// <summary>
    /// Rebuilds the consolidated warning text for the current settings block.
    /// </summary>
    /// <param name="warningBox">Warning box updated in place.</param>
    /// <param name="enableColorBlendProperty">Serialized color-blend enable toggle.</param>
    /// <param name="colorBlendColorProperty">Serialized color-blend tint.</param>
    /// <param name="colorBlendLeadTimeSecondsProperty">Serialized color-blend lead time.</param>
    /// <param name="colorBlendFadeOutSecondsProperty">Serialized color-blend fade-out duration.</param>
    /// <param name="colorBlendMaximumBlendProperty">Serialized color-blend maximum blend.</param>
    /// <param name="enableBillboardProperty">Serialized billboard enable toggle.</param>
    /// <param name="billboardSpriteProperty">Serialized billboard sprite reference.</param>
    /// <param name="billboardColorProperty">Serialized billboard tint.</param>
    /// <param name="billboardWorldOffsetProperty">Serialized billboard world-space offset.</param>
    /// <param name="billboardLeadTimeSecondsProperty">Serialized billboard lead time.</param>
    /// <param name="billboardBaseScaleProperty">Serialized billboard base scale.</param>
    /// <param name="billboardPulseScaleMultiplierProperty">Serialized billboard pulse multiplier.</param>
    /// <param name="billboardPulseExpandDurationSecondsProperty">Serialized billboard expand duration.</param>
    /// <param name="billboardPulseContractDurationSecondsProperty">Serialized billboard contract duration.</param>
    /// <param name="usage">Settings usage that controls context-specific warning wording and fallback rules.</param>
    private static void RefreshWarnings(HelpBox warningBox,
                                        SerializedProperty enableColorBlendProperty,
                                        SerializedProperty colorBlendColorProperty,
                                        SerializedProperty colorBlendLeadTimeSecondsProperty,
                                        SerializedProperty colorBlendFadeOutSecondsProperty,
                                        SerializedProperty colorBlendMaximumBlendProperty,
                                        SerializedProperty enableBillboardProperty,
                                        SerializedProperty billboardSpriteProperty,
                                        SerializedProperty billboardColorProperty,
                                        SerializedProperty billboardWorldOffsetProperty,
                                        SerializedProperty billboardLeadTimeSecondsProperty,
                                        SerializedProperty billboardBaseScaleProperty,
                                        SerializedProperty billboardPulseScaleMultiplierProperty,
                                        SerializedProperty billboardPulseExpandDurationSecondsProperty,
                                        SerializedProperty billboardPulseContractDurationSecondsProperty,
                                        EnemyOffensiveEngagementFeedbackEditorUsage usage)
    {
        if (warningBox == null)
            return;

        List<string> warningLines = new List<string>();
        bool colorBlendEnabled = enableColorBlendProperty.boolValue;
        bool billboardEnabled = enableBillboardProperty.boolValue;

        if (!colorBlendEnabled && !billboardEnabled)
            warningLines.Add(ResolveDisabledChannelsWarning(usage));

        if (colorBlendEnabled && !IsFinite(colorBlendColorProperty.colorValue))
            warningLines.Add("Color Blend Color contains a NaN or infinity channel. Bake uses the canonical default color until the authored value is corrected.");

        if (colorBlendEnabled &&
            (!IsFinite(colorBlendLeadTimeSecondsProperty.floatValue) ||
             !IsFinite(colorBlendFadeOutSecondsProperty.floatValue) ||
             !IsFinite(colorBlendMaximumBlendProperty.floatValue)))
            warningLines.Add("Color Blend timing and strength values must be finite. Bake uses canonical defaults for invalid values.");

        if (colorBlendEnabled &&
            IsFinite(colorBlendMaximumBlendProperty.floatValue) &&
            colorBlendMaximumBlendProperty.floatValue <= 0f)
            warningLines.Add("Maximum Color Blend is 0 or below, so the color warning will stay invisible.");

        if (colorBlendEnabled &&
            IsFinite(colorBlendMaximumBlendProperty.floatValue) &&
            colorBlendMaximumBlendProperty.floatValue > 1f)
            warningLines.Add("Maximum Color Blend is above 1. Bake clamps it to 1.");

        if (colorBlendEnabled &&
            IsFinite(colorBlendLeadTimeSecondsProperty.floatValue) &&
            colorBlendLeadTimeSecondsProperty.floatValue <= 0f)
            warningLines.Add(ResolveTimingLabel("Color Blend", usage) + " is 0 or below, so no color warning window can open.");

        if (colorBlendEnabled &&
            IsFinite(colorBlendFadeOutSecondsProperty.floatValue) &&
            colorBlendFadeOutSecondsProperty.floatValue < 0f)
            warningLines.Add("Negative Color Blend Fade Out Seconds values are treated as 0 at bake/runtime.");

        if (billboardEnabled &&
            billboardSpriteProperty.objectReferenceValue == null &&
            (usage == EnemyOffensiveEngagementFeedbackEditorUsage.GlobalHybrid ||
             usage == EnemyOffensiveEngagementFeedbackEditorUsage.BossPatternChange))
            warningLines.Add("Billboard is enabled but no sprite is assigned, so this settings source cannot render a billboard.");

        if (billboardEnabled && !IsFinite(billboardColorProperty.colorValue))
            warningLines.Add("Billboard Color contains a NaN or infinity channel. Bake uses the canonical default color until the authored value is corrected.");

        if (billboardEnabled &&
            IsFinite(billboardColorProperty.colorValue) &&
            billboardColorProperty.colorValue.a <= 0f)
            warningLines.Add("Billboard Color alpha is 0 or below, so the billboard will stay invisible.");

        if (billboardEnabled && !IsFinite(billboardWorldOffsetProperty.vector3Value))
            warningLines.Add("Billboard World Offset contains NaN or infinity. Bake uses the canonical default offset until the authored value is corrected.");

        if (billboardEnabled &&
            (!IsFinite(billboardLeadTimeSecondsProperty.floatValue) ||
             !IsFinite(billboardBaseScaleProperty.floatValue) ||
             !IsFinite(billboardPulseScaleMultiplierProperty.floatValue) ||
             !IsFinite(billboardPulseExpandDurationSecondsProperty.floatValue) ||
             !IsFinite(billboardPulseContractDurationSecondsProperty.floatValue)))
            warningLines.Add("Billboard timing and scale values must be finite. Bake uses canonical defaults for invalid values.");

        if (billboardEnabled &&
            IsFinite(billboardLeadTimeSecondsProperty.floatValue) &&
            billboardLeadTimeSecondsProperty.floatValue <= 0f)
            warningLines.Add(ResolveTimingLabel("Billboard", usage) + " is 0 or below, so no billboard warning window can open.");

        if (billboardEnabled &&
            IsFinite(billboardBaseScaleProperty.floatValue) &&
            billboardBaseScaleProperty.floatValue <= 0f)
            warningLines.Add("Billboard Base Scale is 0 or below, so the billboard will stay invisible.");

        if (billboardEnabled &&
            IsFinite(billboardPulseScaleMultiplierProperty.floatValue) &&
            billboardPulseScaleMultiplierProperty.floatValue < 0f)
            warningLines.Add("Negative Billboard Pulse Scale Multiplier values are treated as 0 at bake/runtime.");

        if (billboardEnabled &&
            IsFinite(billboardPulseExpandDurationSecondsProperty.floatValue) &&
            billboardPulseExpandDurationSecondsProperty.floatValue < 0f)
            warningLines.Add("Negative Billboard Pulse Expand Duration Seconds values are treated as 0 at bake/runtime.");

        if (billboardEnabled &&
            IsFinite(billboardPulseContractDurationSecondsProperty.floatValue) &&
            billboardPulseContractDurationSecondsProperty.floatValue < 0f)
            warningLines.Add("Negative Billboard Pulse Contract Duration Seconds values are treated as 0 at bake/runtime.");

        warningBox.style.display = warningLines.Count > 0
            ? DisplayStyle.Flex
            : DisplayStyle.None;
        warningBox.text = string.Join("\n", warningLines);
    }

    /// <summary>
    /// Resolves the warning emitted when a settings source intentionally or accidentally disables every presentation channel.
    /// </summary>
    /// <param name="usage">Settings usage that determines whether disabling both channels also suppresses inherited feedback.</param>
    /// <returns>Context-accurate warning text for the disabled settings block.</returns>
    private static string ResolveDisabledChannelsWarning(EnemyOffensiveEngagementFeedbackEditorUsage usage)
    {
        switch (usage)
        {
            case EnemyOffensiveEngagementFeedbackEditorUsage.PredictiveOverride:
                return "Both visual channels are disabled. This override intentionally suppresses inherited behaviour engagement feedback for this normal-pattern interaction.";

            case EnemyOffensiveEngagementFeedbackEditorUsage.BossCandidate:
                return "Both visual channels are disabled. This candidate override intentionally suppresses its mixed-pattern and enemy visual preset feedback.";

            case EnemyOffensiveEngagementFeedbackEditorUsage.BossMixedPattern:
                return "Both visual channels are disabled. This mixed-pattern override intentionally suppresses the enemy visual preset feedback unless a candidate supplies its own override.";

            default:
                return "Both visual channels are disabled, so this feedback block will bake no visible result.";
        }
    }

    /// <summary>
    /// Resolves the serialized timing field's user-facing label for warnings in each runtime context.
    /// </summary>
    /// <param name="channelLabel">Visual channel prefix shown before the timing role.</param>
    /// <param name="usage">Settings usage that determines whether the value is a duration, lead time or hybrid warning window.</param>
    /// <returns>Context-accurate timing field label.</returns>
    private static string ResolveTimingLabel(string channelLabel,
                                             EnemyOffensiveEngagementFeedbackEditorUsage usage)
    {
        switch (usage)
        {
            case EnemyOffensiveEngagementFeedbackEditorUsage.BossPatternChange:
                return channelLabel + " Duration Seconds";

            case EnemyOffensiveEngagementFeedbackEditorUsage.GlobalHybrid:
            case EnemyOffensiveEngagementFeedbackEditorUsage.BossMixedPattern:
            case EnemyOffensiveEngagementFeedbackEditorUsage.BossCandidate:
                return channelLabel + " Warning Window Seconds";

            default:
                return channelLabel + " Lead Time Seconds";
        }
    }

    /// <summary>
    /// Checks whether every color channel is finite before editor data can reach ECS presentation.
    /// </summary>
    /// <param name="value">Color value to inspect.</param>
    /// <returns>True when every channel is neither NaN nor infinity.</returns>
    private static bool IsFinite(Color value)
    {
        return IsFinite(value.r) &&
               IsFinite(value.g) &&
               IsFinite(value.b) &&
               IsFinite(value.a);
    }

    /// <summary>
    /// Checks whether every vector component is finite before it can affect a world transform.
    /// </summary>
    /// <param name="value">World-space vector to inspect.</param>
    /// <returns>True when every component is neither NaN nor infinity.</returns>
    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) &&
               IsFinite(value.y) &&
               IsFinite(value.z);
    }

    /// <summary>
    /// Checks whether one floating-point value is finite.
    /// </summary>
    /// <param name="value">Floating-point value to inspect.</param>
    /// <returns>True when the value is neither NaN nor infinity.</returns>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
    #endregion

    #endregion
}
