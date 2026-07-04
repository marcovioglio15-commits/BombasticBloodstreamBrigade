using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds the UI Visual Preset Growth Sequence subsection and synchronizes entries from progression schedules.
/// </summary>
internal static class PlayerVisualPresetsPanelGrowthSequenceSectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the complete Growth Sequence UI visual-preset subsection.
    /// </summary>
    /// <param name="panel">Owning visual preset panel providing serialized authoring data.</param>
    /// <returns>Configured Growth Sequence subsection.</returns>
    public static VisualElement Build(IPlayerVisualPresetEditorPanel panel)
    {
        Foldout root = ManagementToolFoldoutStateUtility.CreateFoldout("Growth Sequence",
                                                                        "NashCore.PlayerManagement.Visual.GrowthSequence",
                                                                        true);
        root.tooltip = "Configures HUD elements mapped to Level-up & Progression growth sequence steps.";

        if (panel == null || panel.PresetSerializedObject == null)
            return root;

        SerializedObject serializedObject = panel.PresetSerializedObject;
        SerializedProperty settings = serializedObject.FindProperty("growthSequence");
        SerializedProperty scalingRules = serializedObject.FindProperty("scalingRules");

        if (settings == null)
        {
            root.Add(new HelpBox("Growth Sequence settings are missing from the selected UI Visual Preset.",
                                 HelpBoxMessageType.Warning));
            return root;
        }

        SerializedProperty enabled = settings.FindPropertyRelative("enabled");
        VisualElement details = new VisualElement();
        AddField(root, enabled, scalingRules, "Enabled", "Enables the HUD growth sequence.");
        AddField(details, settings.FindPropertyRelative("hideWhenPlayerMissing"), scalingRules, "Hide When Player Missing", "Hides the growth sequence while no valid player or progression config is available.");
        AddField(details, settings.FindPropertyRelative("maximumVisibleSteps"), scalingRules, "Maximum Visible Steps", "Caps how many preauthored UI slots are used. Set 0 to use the full active schedule.");
        AddSyncButton(panel, details, settings.FindPropertyRelative("schedules"));
        BuildSchedules(details, settings.FindPropertyRelative("schedules"), scalingRules);
        root.Add(details);

        Refresh();
        root.TrackPropertyValue(enabled, changedProperty => Refresh());
        return root;

        void Refresh()
        {
            details.style.display = enabled != null && enabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
    #endregion

    #region Schedule Construction
    /// <summary>
    /// Builds all schedule visual entries.
    /// </summary>
    /// <param name="parent">Parent container receiving schedule foldouts.</param>
    /// <param name="schedules">Serialized schedule visual array.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    private static void BuildSchedules(VisualElement parent,
                                       SerializedProperty schedules,
                                       SerializedProperty scalingRules)
    {
        if (parent == null || schedules == null)
            return;

        for (int scheduleIndex = 0; scheduleIndex < schedules.arraySize; scheduleIndex++)
        {
            SerializedProperty schedule = schedules.GetArrayElementAtIndex(scheduleIndex).Copy();
            SerializedProperty scheduleId = schedule.FindPropertyRelative("scheduleId");
            Foldout foldout = CreateFoldout(ResolveScheduleTitle(scheduleId, scheduleIndex),
                                            "Schedule." + scheduleIndex);
            int capturedScheduleIndex = scheduleIndex;
            PlayerManagementFoldoutStateUtility.AttachLazyFoldout(foldout,
                                                                   () =>
                                                                   {
                                                                       AddPlainField(foldout, scheduleId, "Schedule Id", "Schedule ID selected from Level-up & Progression.");
                                                                       BuildSteps(foldout, schedule.FindPropertyRelative("steps"), scalingRules, capturedScheduleIndex);
                                                                   });
            parent.Add(foldout);
        }
    }

    /// <summary>
    /// Builds all step visual entries for one schedule.
    /// </summary>
    /// <param name="parent">Parent schedule foldout.</param>
    /// <param name="steps">Serialized step visual array.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    /// <param name="scheduleIndex">Schedule index used in foldout state keys.</param>
    private static void BuildSteps(VisualElement parent,
                                   SerializedProperty steps,
                                   SerializedProperty scalingRules,
                                   int scheduleIndex)
    {
        if (parent == null || steps == null)
            return;

        for (int stepIndex = 0; stepIndex < steps.arraySize; stepIndex++)
        {
            SerializedProperty step = steps.GetArrayElementAtIndex(stepIndex).Copy();
            SerializedProperty presentationMode = step.FindPropertyRelative("presentationMode");
            Foldout foldout = CreateFoldout(ResolveStepTitle(step, stepIndex),
                                            string.Format("Schedule.{0}.Step.{1}", scheduleIndex, stepIndex));
            int capturedStepIndex = stepIndex;
            PlayerManagementFoldoutStateUtility.AttachLazyFoldout(foldout,
                                                                   () => BuildStepDetails(foldout,
                                                                                          step,
                                                                                          presentationMode,
                                                                                          scalingRules,
                                                                                          capturedStepIndex));
            parent.Add(foldout);
        }
    }

    /// <summary>
    /// Builds one growth step editor body after its foldout has been opened.
    /// </summary>
    /// <param name="foldout">Step foldout receiving property controls.</param>
    /// <param name="step">Serialized growth step visual entry.</param>
    /// <param name="presentationMode">Serialized presentation mode used to toggle text/image fields.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    /// <param name="stepIndex">Step index used in nested foldout state keys.</param>
    private static void BuildStepDetails(Foldout foldout,
                                         SerializedProperty step,
                                         SerializedProperty presentationMode,
                                         SerializedProperty scalingRules,
                                         int stepIndex)
    {
        if (foldout == null || step == null)
            return;

        AddPlainField(foldout, step.FindPropertyRelative("stepIndex"), "Step Index", "Zero-based step index inside the selected schedule.");
        AddPlainField(foldout, step.FindPropertyRelative("statName"), "Stat Name", "Copied progression stat name used as readable fallback text.");
        AddField(foldout, step.FindPropertyRelative("textOverride"), scalingRules, "Text Override", "Optional text used when Presentation Mode is Text.", true);
        AddField(foldout, presentationMode, scalingRules, "Presentation Mode", "Text uses TMP styling. Image uses Next/Normal sprites.");

        VisualElement imageFields = new VisualElement();
        AddPlainField(imageFields, step.FindPropertyRelative("nextSprite"), "Next Sprite", "Sprite displayed while this is the next growth step.");
        AddPlainField(imageFields, step.FindPropertyRelative("normalSprite"), "Normal Sprite", "Sprite displayed while this is not the next growth step.");
        foldout.Add(imageFields);

        VisualElement textFields = new VisualElement();
        BuildTextState(textFields, step.FindPropertyRelative("nextText"), scalingRules, "Next Text", "Step." + stepIndex + ".NextText");
        BuildTextState(textFields, step.FindPropertyRelative("normalText"), scalingRules, "Normal Text", "Step." + stepIndex + ".NormalText");
        foldout.Add(textFields);

        RefreshGrowthStepMode(presentationMode, imageFields, textFields);
        foldout.TrackPropertyValue(presentationMode,
                                   changedProperty => RefreshGrowthStepMode(presentationMode,
                                                                            imageFields,
                                                                            textFields));
    }

    /// <summary>
    /// Toggles the text/image editor controls for one growth step based on its presentation mode.
    /// </summary>
    /// <param name="presentationMode">Serialized presentation mode property.</param>
    /// <param name="imageFields">Image-mode field container.</param>
    /// <param name="textFields">Text-mode field container.</param>
    private static void RefreshGrowthStepMode(SerializedProperty presentationMode,
                                              VisualElement imageFields,
                                              VisualElement textFields)
    {
        bool imageMode = presentationMode != null &&
                         presentationMode.enumValueIndex == (int)PlayerGrowthSequenceHudPresentationMode.Image;

        if (imageFields != null)
            imageFields.style.display = imageMode ? DisplayStyle.Flex : DisplayStyle.None;

        if (textFields != null)
            textFields.style.display = imageMode ? DisplayStyle.None : DisplayStyle.Flex;
    }

    /// <summary>
    /// Builds one text-state editor block.
    /// </summary>
    /// <param name="parent">Parent container receiving the foldout.</param>
    /// <param name="textState">Serialized text-state settings.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    /// <param name="title">Foldout title.</param>
    /// <param name="stateSuffix">Stable foldout state suffix.</param>
    private static void BuildTextState(VisualElement parent,
                                       SerializedProperty textState,
                                       SerializedProperty scalingRules,
                                       string title,
                                       string stateSuffix)
    {
        if (parent == null || textState == null)
            return;

        Foldout foldout = CreateFoldout(title, stateSuffix);
        AddPlainField(foldout, textState.FindPropertyRelative("fontAsset"), "Font Asset", "Optional TMP font override for this state.");
        AddField(foldout, textState.FindPropertyRelative("fontSize"), scalingRules, "Font Size", "Fixed font size when Auto Size is disabled, or preferred size clamped inside Auto Size Min/Max when Auto Size is enabled.");
        SerializedProperty enableAutoSize = textState.FindPropertyRelative("enableAutoSize");
        AddField(foldout, enableAutoSize, scalingRules, "Enable Auto Size", "Enables TMP auto-size for this text state.");
        VisualElement autoSizeFields = new VisualElement();
        AddField(autoSizeFields, textState.FindPropertyRelative("autoSizeMin"), scalingRules, "Auto Size Min", "Minimum TMP font size allowed when auto-size is enabled.");
        AddField(autoSizeFields, textState.FindPropertyRelative("autoSizeMax"), scalingRules, "Auto Size Max", "Maximum TMP font size allowed when auto-size is enabled.");
        foldout.Add(autoSizeFields);
        AddField(foldout, textState.FindPropertyRelative("color"), scalingRules, "Color", "Text color used by this state.");
        AddField(foldout, textState.FindPropertyRelative("outlineColor"), scalingRules, "Outline Color", "Text outline color used by this state.");
        AddField(foldout, textState.FindPropertyRelative("outlineWidth"), scalingRules, "Outline Width", "TMP outline width used by this state.");
        parent.Add(foldout);

        RefreshAutoSizeFields(enableAutoSize, autoSizeFields);
        foldout.TrackPropertyValue(enableAutoSize,
                                   changedProperty => RefreshAutoSizeFields(enableAutoSize, autoSizeFields));
    }

    /// <summary>
    /// Shows auto-size bounds only when the owning text state has TMP auto-size enabled.
    /// </summary>
    /// <param name="enableAutoSize">Serialized auto-size toggle.</param>
    /// <param name="autoSizeFields">Container with auto-size bounds controls.</param>
    private static void RefreshAutoSizeFields(SerializedProperty enableAutoSize,
                                              VisualElement autoSizeFields)
    {
        if (autoSizeFields == null)
            return;

        autoSizeFields.style.display = enableAutoSize != null && enableAutoSize.boolValue
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }
    #endregion

    #region Synchronization
    /// <summary>
    /// Adds the synchronization button that mirrors progression schedules into the visual preset.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="parent">Parent container receiving the button.</param>
    /// <param name="schedules">Serialized growth schedule visual array.</param>
    private static void AddSyncButton(IPlayerVisualPresetEditorPanel panel,
                                      VisualElement parent,
                                      SerializedProperty schedules)
    {
        Button syncButton = new Button(() =>
        {
            SyncFromProgression(panel, schedules);
            panel.RebuildDetails();
        });
        syncButton.text = "Sync From Level-up & Progression";
        syncButton.tooltip = "Adds missing schedule/step visual entries from the active Player Progression Preset without overwriting existing artwork or text styling.";
        parent.Add(syncButton);

        if (PlayerManagementSelectionContext.ActiveProgressionPreset == null)
        {
            parent.Add(new HelpBox("Select a Player Progression Preset in the master context before syncing growth sequence steps.",
                                   HelpBoxMessageType.Info));
        }
    }

    /// <summary>
    /// Adds missing schedule and step entries from the active progression preset.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="schedules">Serialized growth schedule visual array.</param>
    private static void SyncFromProgression(IPlayerVisualPresetEditorPanel panel, SerializedProperty schedules)
    {
        PlayerProgressionPreset progressionPreset = PlayerManagementSelectionContext.ActiveProgressionPreset;

        if (panel == null || schedules == null || progressionPreset == null || progressionPreset.Schedules == null)
            return;

        for (int progressionScheduleIndex = 0; progressionScheduleIndex < progressionPreset.Schedules.Count; progressionScheduleIndex++)
        {
            PlayerLevelUpScheduleDefinition progressionSchedule = progressionPreset.Schedules[progressionScheduleIndex];

            if (progressionSchedule == null)
                continue;

            string scheduleId = string.IsNullOrWhiteSpace(progressionSchedule.ScheduleId)
                ? string.Format("Schedule{0}", progressionScheduleIndex)
                : progressionSchedule.ScheduleId.Trim();
            SerializedProperty scheduleProperty = FindOrAddSchedule(schedules, scheduleId);

            if (scheduleProperty == null)
                continue;

            SerializedProperty steps = scheduleProperty.FindPropertyRelative("steps");

            if (steps == null || progressionSchedule.Sequence == null)
                continue;

            for (int stepIndex = 0; stepIndex < progressionSchedule.Sequence.Count; stepIndex++)
            {
                PlayerLevelUpScheduleStepDefinition progressionStep = progressionSchedule.Sequence[stepIndex];
                string statName = progressionStep != null && !string.IsNullOrWhiteSpace(progressionStep.StatName)
                    ? progressionStep.StatName.Trim()
                    : string.Empty;
                SerializedProperty stepProperty = FindOrAddStep(steps, stepIndex);
                stepProperty.FindPropertyRelative("stepIndex").intValue = stepIndex;
                stepProperty.FindPropertyRelative("statName").stringValue = statName;
            }
        }

        panel.PresetSerializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
    }

    /// <summary>
    /// Finds an existing schedule visual entry or appends a new one.
    /// </summary>
    /// <param name="schedules">Serialized schedule array.</param>
    /// <param name="scheduleId">Schedule ID to resolve.</param>
    /// <returns>Resolved schedule serialized property.</returns>
    private static SerializedProperty FindOrAddSchedule(SerializedProperty schedules, string scheduleId)
    {
        for (int scheduleIndex = 0; scheduleIndex < schedules.arraySize; scheduleIndex++)
        {
            SerializedProperty schedule = schedules.GetArrayElementAtIndex(scheduleIndex);
            SerializedProperty idProperty = schedule.FindPropertyRelative("scheduleId");

            if (idProperty != null && string.Equals(idProperty.stringValue, scheduleId, StringComparison.OrdinalIgnoreCase))
                return schedule;
        }

        schedules.arraySize++;
        SerializedProperty inserted = schedules.GetArrayElementAtIndex(schedules.arraySize - 1);
        inserted.FindPropertyRelative("scheduleId").stringValue = scheduleId;
        return inserted;
    }

    /// <summary>
    /// Finds an existing step visual entry or appends a new one.
    /// </summary>
    /// <param name="steps">Serialized step array.</param>
    /// <param name="stepIndex">Step index to resolve.</param>
    /// <returns>Resolved step serialized property.</returns>
    private static SerializedProperty FindOrAddStep(SerializedProperty steps, int stepIndex)
    {
        for (int entryIndex = 0; entryIndex < steps.arraySize; entryIndex++)
        {
            SerializedProperty step = steps.GetArrayElementAtIndex(entryIndex);
            SerializedProperty indexProperty = step.FindPropertyRelative("stepIndex");

            if (indexProperty != null && indexProperty.intValue == stepIndex)
                return step;
        }

        steps.arraySize++;
        SerializedProperty inserted = steps.GetArrayElementAtIndex(steps.arraySize - 1);
        inserted.FindPropertyRelative("stepIndex").intValue = stepIndex;
        return inserted;
    }
    #endregion

    #region Fields
    /// <summary>
    /// Adds one scalable field to the UI.
    /// </summary>
    /// <param name="parent">Parent container receiving the field.</param>
    /// <param name="property">Serialized property to render.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    /// <param name="label">Field label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    /// <param name="allowTokenScaling">True when a string field should expose token formulas.</param>
    private static void AddField(VisualElement parent,
                                 SerializedProperty property,
                                 SerializedProperty scalingRules,
                                 string label,
                                 string tooltip,
                                 bool allowTokenScaling = false)
    {
        if (parent == null || property == null)
            return;

        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property,
                                                                           scalingRules,
                                                                           label,
                                                                           null,
                                                                           allowTokenScaling);
        field.tooltip = tooltip;
        parent.Add(field);
    }

    /// <summary>
    /// Adds one non-scalable property field to the UI.
    /// </summary>
    /// <param name="parent">Parent container receiving the field.</param>
    /// <param name="property">Serialized property to render.</param>
    /// <param name="label">Field label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    private static void AddPlainField(VisualElement parent,
                                      SerializedProperty property,
                                      string label,
                                      string tooltip)
    {
        if (parent == null || property == null)
            return;

        if (property.propertyType == SerializedPropertyType.String)
        {
            TextField textField = new TextField(label);
            textField.isDelayed = true;
            textField.tooltip = tooltip;
            textField.BindProperty(property);
            textField.RegisterValueChangedCallback(evt => PlayerManagementDraftSession.MarkDirty());
            parent.Add(textField);
            return;
        }

        PropertyField field = new PropertyField(property, label);
        field.tooltip = tooltip;
        field.BindProperty(property);
        field.RegisterCallback<SerializedPropertyChangeEvent>(evt => PlayerManagementDraftSession.MarkDirty());
        parent.Add(field);
    }
    #endregion

    #region Labels
    /// <summary>
    /// Resolves a user-facing schedule foldout title.
    /// </summary>
    /// <param name="scheduleId">Serialized schedule ID property.</param>
    /// <param name="scheduleIndex">Schedule array index.</param>
    /// <returns>Foldout title.</returns>
    private static string ResolveScheduleTitle(SerializedProperty scheduleId, int scheduleIndex)
    {
        if (scheduleId != null && !string.IsNullOrWhiteSpace(scheduleId.stringValue))
            return "Schedule - " + scheduleId.stringValue;

        return string.Format("Schedule {0}", scheduleIndex + 1);
    }

    /// <summary>
    /// Resolves a user-facing step foldout title.
    /// </summary>
    /// <param name="step">Serialized step visual entry.</param>
    /// <param name="entryIndex">Step array index.</param>
    /// <returns>Foldout title.</returns>
    private static string ResolveStepTitle(SerializedProperty step, int entryIndex)
    {
        SerializedProperty stepIndex = step != null ? step.FindPropertyRelative("stepIndex") : null;
        SerializedProperty statName = step != null ? step.FindPropertyRelative("statName") : null;
        string statLabel = statName != null && !string.IsNullOrWhiteSpace(statName.stringValue)
            ? " - " + statName.stringValue
            : string.Empty;

        if (stepIndex != null)
            return string.Format("Step {0}{1}", stepIndex.intValue + 1, statLabel);

        return string.Format("Step {0}{1}", entryIndex + 1, statLabel);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Creates one themed nested foldout with a stable state key.
    /// </summary>
    /// <param name="title">User-facing foldout title.</param>
    /// <param name="stateSuffix">Stable state-key suffix.</param>
    /// <returns>Configured nested foldout.</returns>
    private static Foldout CreateFoldout(string title, string stateSuffix)
    {
        return ManagementToolFoldoutStateUtility.CreateFoldout(title,
                                                                "NashCore.PlayerManagement.Visual.GrowthSequence.Lazy." + stateSuffix,
                                                                false);
    }

    #endregion

    #endregion
}
