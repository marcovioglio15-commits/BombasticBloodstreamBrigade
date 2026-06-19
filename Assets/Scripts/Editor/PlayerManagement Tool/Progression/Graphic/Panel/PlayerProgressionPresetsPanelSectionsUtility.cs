using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the editable detail sections for progression presets and centralizes section-specific validation helpers.
/// </summary>
public static class PlayerProgressionPresetsPanelSectionsUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the metadata section of the progression preset panel.
    /// </summary>
    /// <param name="panel">Panel instance that owns the serialized preset and target UI container.</param>
    public static void BuildMetadataSection(PlayerProgressionPresetsPanel panel)
    {
        if (panel == null || panel.sectionContentRoot == null || panel.presetSerializedObject == null)
            return;

        Label header = new Label("Preset Details");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(header, "NashCore.PlayerManagement.Progression.Metadata.PresetDetails");
        panel.sectionContentRoot.Add(header);

        SerializedProperty idProperty = panel.presetSerializedObject.FindProperty("presetId");
        SerializedProperty nameProperty = panel.presetSerializedObject.FindProperty("presetName");
        SerializedProperty descriptionProperty = panel.presetSerializedObject.FindProperty("description");
        SerializedProperty versionProperty = panel.presetSerializedObject.FindProperty("version");

        TextField nameField = new TextField("Preset Name");
        nameField.isDelayed = true;
        nameField.BindProperty(nameProperty);
        nameField.RegisterValueChangedCallback(evt =>
        {
            panel.RenamePreset(panel.selectedPreset, evt.newValue);
        });
        panel.sectionContentRoot.Add(nameField);

        TextField versionField = new TextField("Version");
        versionField.isDelayed = true;
        versionField.BindProperty(versionProperty);
        versionField.RegisterValueChangedCallback(evt =>
        {
            panel.RefreshPresetList();
        });
        panel.sectionContentRoot.Add(versionField);

        TextField descriptionField = new TextField("Description");
        descriptionField.multiline = true;
        descriptionField.isDelayed = true;
        descriptionField.style.height = 60f;
        descriptionField.BindProperty(descriptionProperty);
        descriptionField.RegisterValueChangedCallback(evt =>
        {
            panel.RefreshPresetList();
        });
        panel.sectionContentRoot.Add(descriptionField);

        VisualElement idRow = new VisualElement();
        idRow.style.flexDirection = FlexDirection.Row;
        idRow.style.alignItems = Align.Center;

        TextField idField = new TextField("Preset ID");
        idField.isReadOnly = true;
        idField.SetEnabled(false);
        idField.style.flexGrow = 1f;
        idField.BindProperty(idProperty);
        idRow.Add(idField);

        Button regenerateButton = new Button();
        regenerateButton.text = "Regenerate";
        regenerateButton.clicked += panel.RegeneratePresetId;
        regenerateButton.style.marginLeft = 6f;
        idRow.Add(regenerateButton);

        panel.sectionContentRoot.Add(idRow);
    }

    /// <summary>
    /// Builds the scalable stats section and live validation warnings.
    /// </summary>
    /// <param name="panel">Panel instance that owns the serialized preset and target UI container.</param>
    public static void BuildScalableStatsSection(PlayerProgressionPresetsPanel panel)
    {
        if (panel == null || panel.presetSerializedObject == null)
            return;

        VisualElement scalableStatsContainer = panel.CreateSectionContainer("Scalable Stats");
        SerializedProperty scalableStatsProperty = panel.presetSerializedObject.FindProperty("scalableStats");
        SerializedProperty scalingRulesProperty = panel.presetSerializedObject.FindProperty("scalingRules");

        if (scalableStatsProperty == null || scalingRulesProperty == null)
        {
            Label missingLabel = new Label("Scalable stats data is missing on this preset.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            scalableStatsContainer.Add(missingLabel);
            return;
        }

        Label infoLabel = new Label("Manage custom variables used by formulas. Use [this] to reference current field value.");
        infoLabel.style.marginBottom = 4f;
        scalableStatsContainer.Add(infoLabel);

        VisualElement warningsRoot = new VisualElement();
        warningsRoot.style.marginBottom = 4f;
        scalableStatsContainer.Add(warningsRoot);
        RefreshScalableStatsWarnings(warningsRoot, scalableStatsProperty, scalingRulesProperty);

        PropertyField scalableStatsField = new PropertyField(scalableStatsProperty, "Scalable Stats");
        scalableStatsField.BindProperty(scalableStatsProperty);
        scalableStatsField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            RefreshScalableStatsWarnings(warningsRoot, scalableStatsProperty, scalingRulesProperty);
        });
        scalableStatsContainer.Add(scalableStatsField);

        scalableStatsContainer.RegisterCallback<ChangeEvent<string>>(evt =>
        {
            RefreshScalableStatsWarnings(warningsRoot, scalableStatsProperty, scalingRulesProperty);
        });

        scalableStatsContainer.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            RefreshScalableStatsWarnings(warningsRoot, scalableStatsProperty, scalingRulesProperty);
        });
    }

    /// <summary>
    /// Builds the milestones section, including all visible root properties related to phase progression.
    /// </summary>
    /// <param name="panel">Panel instance that owns the serialized preset and target UI container.</param>
    /// <param name="excludedRootPropertyNames">Root-level property names intentionally hidden from the milestones view.</param>
    public static void BuildMilestonesSection(PlayerProgressionPresetsPanel panel, HashSet<string> excludedRootPropertyNames)
    {
        if (panel == null || panel.presetSerializedObject == null)
            return;

        VisualElement milestonesContainer = panel.CreateSectionContainer("Milestones");
        SerializedProperty gamePhasesDefinitionProperty = panel.presetSerializedObject.FindProperty("gamePhasesDefinition");
        SerializedProperty experiencePickupRadiusProperty = panel.presetSerializedObject.FindProperty("experiencePickupRadius");
        SerializedProperty milestoneSkipHoldConfirmationProperty = panel.presetSerializedObject.FindProperty("milestoneSkipHoldConfirmationSeconds");
        SerializedProperty milestoneSkipHoldFillColorProperty = panel.presetSerializedObject.FindProperty("milestoneSkipHoldFillColor");
        SerializedProperty scalingRulesProperty = panel.presetSerializedObject.FindProperty("scalingRules");

        if (gamePhasesDefinitionProperty == null || experiencePickupRadiusProperty == null)
        {
            Label missingLabel = new Label("Milestones data is missing on this preset.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            milestonesContainer.Add(missingLabel);
            return;
        }

        Label infoLabel = new Label("Define game phases, linear growth, and milestone spikes used by runtime level-up.");
        infoLabel.style.marginBottom = 4f;
        milestonesContainer.Add(infoLabel);

        VisualElement runtimeWarningsRoot = new VisualElement();
        runtimeWarningsRoot.style.marginBottom = 4f;
        milestonesContainer.Add(runtimeWarningsRoot);
        RefreshMilestoneRuntimeWarnings(runtimeWarningsRoot,
                                        milestoneSkipHoldConfirmationProperty,
                                        milestoneSkipHoldFillColorProperty);
        milestonesContainer.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            RefreshMilestoneRuntimeWarnings(runtimeWarningsRoot,
                                            milestoneSkipHoldConfirmationProperty,
                                            milestoneSkipHoldFillColorProperty);
        });

        SerializedProperty iterator = panel.presetSerializedObject.GetIterator();
        bool enterChildren = true;
        bool hasVisibleMilestonesField = false;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.depth != 0)
                continue;

            SerializedProperty rootProperty = iterator.Copy();

            if (ShouldSkipMilestonesRootProperty(rootProperty, excludedRootPropertyNames))
                continue;

            hasVisibleMilestonesField = true;

            if (string.Equals(rootProperty.name, "gamePhasesDefinition", StringComparison.Ordinal))
            {
                Foldout gamePhasesFoldout = new Foldout();
                gamePhasesFoldout.text = "Game Phases Definition";
                gamePhasesFoldout.value = true;
                gamePhasesFoldout.style.marginBottom = 4f;
                milestonesContainer.Add(gamePhasesFoldout);

                VisualElement scalableGamePhasesField = PlayerScalingFieldElementFactory.CreateField(rootProperty,
                                                                                                      scalingRulesProperty,
                                                                                                      "Phases");
                gamePhasesFoldout.Add(scalableGamePhasesField);
                continue;
            }

            string labelOverride = rootProperty.displayName;

            if (string.Equals(rootProperty.name, "experiencePickupRadius", StringComparison.Ordinal))
                labelOverride = "Experience Pickup Radius";

            VisualElement scalableField = PlayerScalingFieldElementFactory.CreateField(rootProperty,
                                                                                        scalingRulesProperty,
                                                                                        labelOverride);
            milestonesContainer.Add(scalableField);
        }

        if (hasVisibleMilestonesField)
            return;

        Label noFieldsLabel = new Label("No milestone fields available on this preset.");
        noFieldsLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        milestonesContainer.Add(noFieldsLabel);
    }

    /// <summary>
    /// Builds the schedules section and the popup used to select the runtime-equipped schedule.
    /// </summary>
    /// <param name="panel">Panel instance that owns the serialized preset and target UI container.</param>
    public static void BuildSchedulesSection(PlayerProgressionPresetsPanel panel)
    {
        if (panel == null || panel.presetSerializedObject == null)
            return;

        VisualElement schedulesContainer = panel.CreateSectionContainer("Schedules");
        SerializedProperty schedulesProperty = panel.presetSerializedObject.FindProperty("schedules");
        SerializedProperty equippedScheduleIdProperty = panel.presetSerializedObject.FindProperty("equippedScheduleId");

        if (schedulesProperty == null || equippedScheduleIdProperty == null)
        {
            Label missingLabel = new Label("Schedule data is missing on this preset.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            schedulesContainer.Add(missingLabel);
            return;
        }

        Label infoLabel = new Label("Define repeating level-up stat sequences and select the schedule equipped at runtime.");
        infoLabel.style.marginBottom = 4f;
        schedulesContainer.Add(infoLabel);

        List<string> scheduleIdOptions = BuildScheduleIdOptions(schedulesProperty);
        List<string> popupOptions = new List<string>();
        popupOptions.Add("<None>");

        for (int optionIndex = 0; optionIndex < scheduleIdOptions.Count; optionIndex++)
            popupOptions.Add(scheduleIdOptions[optionIndex]);

        string selectedLabel = ResolveSchedulePopupLabel(popupOptions, equippedScheduleIdProperty.stringValue);
        PopupField<string> equippedSchedulePopup = new PopupField<string>("Equipped Schedule", popupOptions, selectedLabel);
        equippedSchedulePopup.RegisterValueChangedCallback(evt =>
        {
            panel.presetSerializedObject.Update();
            equippedScheduleIdProperty.stringValue = string.Equals(evt.newValue, "<None>", StringComparison.Ordinal) ? string.Empty : evt.newValue;
            panel.presetSerializedObject.ApplyModifiedProperties();
            PlayerManagementDraftSession.MarkDirty();
        });
        schedulesContainer.Add(equippedSchedulePopup);

        if (scheduleIdOptions.Count <= 0)
        {
            HelpBox warningBox = new HelpBox("No schedules available. Create at least one schedule to enable runtime level-up sequencing.", HelpBoxMessageType.Warning);
            warningBox.style.marginBottom = 4f;
            schedulesContainer.Add(warningBox);
        }

        PropertyField schedulesField = new PropertyField(schedulesProperty, "Schedule Definitions");
        schedulesField.BindProperty(schedulesProperty);
        schedulesContainer.Add(schedulesField);
    }

    /// <summary>
    /// Builds the combo-counter section, including scalable runtime toggles, thresholds, and rank bonus formulas.
    /// </summary>
    /// <param name="panel">Panel instance that owns the serialized preset and target UI container.</param>
    public static void BuildComboCounterSection(PlayerProgressionPresetsPanel panel)
    {
        if (panel == null || panel.presetSerializedObject == null)
            return;

        VisualElement comboContainer = panel.CreateSectionContainer("Combo Counter");
        SerializedProperty comboCounterProperty = panel.presetSerializedObject.FindProperty("comboCounter");

        if (comboCounterProperty == null)
        {
            Label missingLabel = new Label("Combo counter data is missing on this preset.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            comboContainer.Add(missingLabel);
            return;
        }

        PropertyField comboCounterField = new PropertyField(comboCounterProperty, "Combo Counter Settings");
        comboCounterField.BindProperty(comboCounterProperty);
        comboCounterField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
        });
        comboContainer.Add(comboCounterField);
    }
    #endregion

    #region Private Methods
    private static bool ShouldSkipMilestonesRootProperty(SerializedProperty property, HashSet<string> excludedRootPropertyNames)
    {
        if (property == null)
            return true;

        if (string.Equals(property.name, "m_Script", StringComparison.Ordinal))
            return true;

        return excludedRootPropertyNames != null && excludedRootPropertyNames.Contains(property.name);
    }

    private static List<string> BuildScheduleIdOptions(SerializedProperty schedulesProperty)
    {
        List<string> options = new List<string>();

        if (schedulesProperty == null)
            return options;

        HashSet<string> visitedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int scheduleIndex = 0; scheduleIndex < schedulesProperty.arraySize; scheduleIndex++)
        {
            SerializedProperty scheduleProperty = schedulesProperty.GetArrayElementAtIndex(scheduleIndex);

            if (scheduleProperty == null)
                continue;

            SerializedProperty scheduleIdProperty = scheduleProperty.FindPropertyRelative("scheduleId");

            if (scheduleIdProperty == null || string.IsNullOrWhiteSpace(scheduleIdProperty.stringValue))
                continue;

            string scheduleId = scheduleIdProperty.stringValue.Trim();

            if (!visitedIds.Add(scheduleId))
                continue;

            options.Add(scheduleId);
        }

        options.Sort(StringComparer.OrdinalIgnoreCase);
        return options;
    }

    private static string ResolveSchedulePopupLabel(List<string> popupOptions, string equippedScheduleId)
    {
        if (popupOptions == null || popupOptions.Count <= 0)
            return "<None>";

        if (string.IsNullOrWhiteSpace(equippedScheduleId))
            return "<None>";

        for (int optionIndex = 0; optionIndex < popupOptions.Count; optionIndex++)
        {
            string option = popupOptions[optionIndex];

            if (string.Equals(option, equippedScheduleId, StringComparison.OrdinalIgnoreCase))
                return option;
        }

        return "<None>";
    }

    /// <summary>
    /// Refreshes warnings for milestone runtime presentation fields that are sanitized only at runtime.
    /// </summary>
    /// <param name="warningsRoot">Container receiving generated warning boxes.</param>
    /// <param name="skipHoldConfirmationProperty">Serialized skip hold confirmation duration.</param>
    /// <param name="skipHoldFillColorProperty">Serialized skip hold fill color.</param>
    private static void RefreshMilestoneRuntimeWarnings(VisualElement warningsRoot,
                                                        SerializedProperty skipHoldConfirmationProperty,
                                                        SerializedProperty skipHoldFillColorProperty)
    {
        if (warningsRoot == null)
            return;

        warningsRoot.Clear();

        if (skipHoldConfirmationProperty != null)
        {
            float holdSeconds = skipHoldConfirmationProperty.floatValue;

            if (float.IsNaN(holdSeconds) || float.IsInfinity(holdSeconds))
                AddWarning(warningsRoot, "Milestone Skip Hold Confirmation Seconds is not finite. Runtime falls back to a safe value without changing the asset.");
            else if (holdSeconds < 0f)
                AddWarning(warningsRoot, "Milestone Skip Hold Confirmation Seconds is negative. Runtime treats it as instant confirmation without changing the asset.");
        }

        if (skipHoldFillColorProperty == null)
            return;

        Color fillColor = skipHoldFillColorProperty.colorValue;

        if (fillColor.a <= 0f)
            AddWarning(warningsRoot, "Milestone Skip Hold Fill Color alpha is zero or negative, so the hold fill will be invisible.");

        if (IsColorOutsideUnitRange(fillColor))
            AddWarning(warningsRoot, "Milestone Skip Hold Fill Color contains channels outside 0..1. Runtime clamps presentation channels without changing the asset.");
    }

    /// <summary>
    /// Adds one warning box to the target container.
    /// </summary>
    /// <param name="warningsRoot">Container receiving the warning.</param>
    /// <param name="warningText">Warning message shown in the tool.</param>
    private static void AddWarning(VisualElement warningsRoot, string warningText)
    {
        if (warningsRoot == null || string.IsNullOrWhiteSpace(warningText))
            return;

        HelpBox warningBox = new HelpBox(warningText, HelpBoxMessageType.Warning);
        warningBox.style.marginBottom = 2f;
        warningsRoot.Add(warningBox);
    }

    /// <summary>
    /// Checks whether any color channel is outside the normalized presentation range.
    /// </summary>
    /// <param name="color">Color value inspected by the tool.</param>
    /// <returns>True when any channel is outside 0..1; otherwise false.</returns>
    private static bool IsColorOutsideUnitRange(Color color)
    {
        if (color.r < 0f || color.r > 1f)
            return true;

        if (color.g < 0f || color.g > 1f)
            return true;

        if (color.b < 0f || color.b > 1f)
            return true;

        return color.a < 0f || color.a > 1f;
    }

    private static void RefreshScalableStatsWarnings(VisualElement warningsRoot,
                                                     SerializedProperty scalableStatsProperty,
                                                     SerializedProperty scalingRulesProperty)
    {
        if (warningsRoot == null || scalableStatsProperty == null)
            return;

        warningsRoot.Clear();

        for (int statIndex = 0; statIndex < scalableStatsProperty.arraySize; statIndex++)
        {
            SerializedProperty statElementProperty = scalableStatsProperty.GetArrayElementAtIndex(statIndex);
            SerializedProperty statNameProperty = statElementProperty != null ? statElementProperty.FindPropertyRelative("statName") : null;
            string statName = statNameProperty != null ? statNameProperty.stringValue : string.Empty;
            string warningText = ValidateScalableStatEntry(statName, statIndex, scalableStatsProperty);

            if (string.IsNullOrWhiteSpace(warningText))
                continue;

            HelpBox warningBox = new HelpBox(string.Format("Stat {0}: {1}", statIndex + 1, warningText), HelpBoxMessageType.Warning);
            warningBox.style.marginBottom = 2f;
            warningsRoot.Add(warningBox);
        }

        List<string> dependencyWarnings = PlayerScalingDependencyValidationUtility.BuildScalableStatsDependencyWarnings(scalableStatsProperty,
                                                                                                                         scalingRulesProperty);

        for (int warningIndex = 0; warningIndex < dependencyWarnings.Count; warningIndex++)
        {
            string dependencyWarning = dependencyWarnings[warningIndex];

            if (string.IsNullOrWhiteSpace(dependencyWarning))
                continue;

            HelpBox warningBox = new HelpBox(dependencyWarning, HelpBoxMessageType.Warning);
            warningBox.style.marginBottom = 2f;
            warningsRoot.Add(warningBox);
        }
    }

    private static string ValidateScalableStatEntry(string statName, int statIndex, SerializedProperty scalableStatsProperty)
    {
        List<string> warnings = new List<string>();

        if (!PlayerScalableStatNameUtility.IsValid(statName))
            warnings.Add("Invalid name. Use letters/digits/underscore, start with letter or underscore, and avoid 'this'.");

        SerializedProperty statElementProperty = scalableStatsProperty.GetArrayElementAtIndex(statIndex);
        SerializedProperty statTypeProperty = statElementProperty != null ? statElementProperty.FindPropertyRelative("statType") : null;
        SerializedProperty defaultValueProperty = statElementProperty != null ? statElementProperty.FindPropertyRelative("defaultValue") : null;
        SerializedProperty minimumValueProperty = statElementProperty != null ? statElementProperty.FindPropertyRelative("minimumValue") : null;
        SerializedProperty maximumValueProperty = statElementProperty != null ? statElementProperty.FindPropertyRelative("maximumValue") : null;
        SerializedProperty defaultTokenValueProperty = statElementProperty != null ? statElementProperty.FindPropertyRelative("defaultTokenValue") : null;
        PlayerScalableStatType statType = statTypeProperty != null
            ? (PlayerScalableStatType)statTypeProperty.enumValueIndex
            : PlayerScalableStatType.Float;

        if ((statType == PlayerScalableStatType.Float ||
             statType == PlayerScalableStatType.Integer ||
             statType == PlayerScalableStatType.Unsigned) &&
            minimumValueProperty != null &&
            maximumValueProperty != null)
        {
            float minimumValue = minimumValueProperty.floatValue;
            float maximumValue = maximumValueProperty.floatValue;

            if (minimumValue > maximumValue)
            {
                warnings.Add("Min is above Max. Runtime uses the ordered pair without snapping authoring values.");
            }

            if (defaultValueProperty != null)
            {
                PlayerScalableStatClampUtility.ResolveOrderedRange(minimumValue,
                                                                   maximumValue,
                                                                   out float resolvedMinimumValue,
                                                                   out float resolvedMaximumValue);
                float defaultValue = defaultValueProperty.floatValue;

                if (defaultValue < resolvedMinimumValue || defaultValue > resolvedMaximumValue)
                {
                    warnings.Add("Default Value is outside the configured clamp range and will be clamped only at runtime.");
                }
            }
        }

        if (statType == PlayerScalableStatType.Integer)
        {
            if (defaultValueProperty != null && HasFractionalPart(defaultValueProperty.floatValue))
                warnings.Add("Default Value has decimals on an Integer stat and will be rounded only at runtime.");

            if (minimumValueProperty != null && HasFractionalPart(minimumValueProperty.floatValue))
                warnings.Add("Min has decimals on an Integer stat and may produce ambiguous runtime bounds.");

            if (maximumValueProperty != null && HasFractionalPart(maximumValueProperty.floatValue))
                warnings.Add("Max has decimals on an Integer stat and may produce ambiguous runtime bounds.");
        }

        if (statType == PlayerScalableStatType.Unsigned)
        {
            if (defaultValueProperty != null && defaultValueProperty.floatValue < 0f)
                warnings.Add("Default Value is negative on an Unsigned stat and will be clamped only at runtime.");

            if (minimumValueProperty != null && minimumValueProperty.floatValue < 0f)
                warnings.Add("Min is negative on an Unsigned stat. Runtime will still enforce a zero lower bound.");

            if (maximumValueProperty != null && maximumValueProperty.floatValue < 0f)
                warnings.Add("Max is negative on an Unsigned stat and will collapse to zero at runtime.");

            if (defaultValueProperty != null && HasFractionalPart(defaultValueProperty.floatValue))
                warnings.Add("Default Value has decimals on an Unsigned stat and will be rounded only at runtime.");

            if (minimumValueProperty != null && HasFractionalPart(minimumValueProperty.floatValue))
                warnings.Add("Min has decimals on an Unsigned stat and may produce ambiguous runtime bounds.");

            if (maximumValueProperty != null && HasFractionalPart(maximumValueProperty.floatValue))
                warnings.Add("Max has decimals on an Unsigned stat and may produce ambiguous runtime bounds.");
        }

        if (statType == PlayerScalableStatType.Token && defaultTokenValueProperty != null)
        {
            string tokenValue = string.IsNullOrWhiteSpace(defaultTokenValueProperty.stringValue)
                ? string.Empty
                : defaultTokenValueProperty.stringValue.Trim();

            if (Encoding.UTF8.GetByteCount(tokenValue) > 61)
                warnings.Add("Default token value exceeds runtime FixedString64Bytes capacity and will not be writable at runtime.");
        }

        for (int index = 0; index < scalableStatsProperty.arraySize; index++)
        {
            if (index == statIndex)
                continue;

            SerializedProperty otherStatElement = scalableStatsProperty.GetArrayElementAtIndex(index);
            SerializedProperty otherStatNameProperty = otherStatElement != null ? otherStatElement.FindPropertyRelative("statName") : null;

            if (otherStatNameProperty == null)
                continue;

            if (!string.Equals(otherStatNameProperty.stringValue, statName, StringComparison.OrdinalIgnoreCase))
                continue;

            warnings.Add("Duplicate name.");
            break;
        }

        return string.Join(Environment.NewLine, warnings);
    }

    private static bool HasFractionalPart(float value)
    {
        return Mathf.Abs(value - Mathf.Round(value)) > 0.0001f;
    }
    #endregion

    #endregion
}
