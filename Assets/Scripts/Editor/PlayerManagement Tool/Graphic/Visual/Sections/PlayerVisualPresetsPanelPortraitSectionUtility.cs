using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds the Player Visual Preset Portrait subsection with scalable animation timing and closed ID selectors.
/// </summary>
internal static class PlayerVisualPresetsPanelPortraitSectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the complete Portrait visual-preset subsection.
    /// </summary>
    /// <param name="panel">Owning visual preset panel providing serialized authoring data.</param>
    /// <returns>Configured Portrait subsection.</returns>
    public static VisualElement Build(PlayerVisualPresetsPanel panel)
    {
        Foldout root = ManagementToolFoldoutStateUtility.CreateFoldout("Portrait",
                                                                        "NashCore.PlayerManagement.Visual.Portrait",
                                                                        true);
        root.tooltip = "Configures the ECS-driven player HUD portrait animation state machine.";

        if (panel == null || panel.PresetSerializedObject == null)
            return root;

        SerializedObject serializedObject = panel.PresetSerializedObject;
        SerializedProperty settings = serializedObject.FindProperty("portrait");
        SerializedProperty scalingRules = serializedObject.FindProperty("scalingRules");

        if (settings == null)
        {
            root.Add(new HelpBox("Portrait settings are missing from the selected Player Visual Preset.",
                                 HelpBoxMessageType.Warning));
            return root;
        }

        SerializedProperty enabled = settings.FindPropertyRelative("enabled");
        VisualElement details = new VisualElement();
        AddField(root, enabled, scalingRules, "Enabled", "Enables the dynamic portrait HUD.");
        AddField(details, settings.FindPropertyRelative("hideWhenPlayerMissing"), scalingRules, "Hide When Player Missing", "Hides the portrait while no valid player entity is available.");
        BuildAnimation(details, settings.FindPropertyRelative("idleAnimation"), scalingRules, "Idle Animation", "Idle");
        BuildAnimation(details, settings.FindPropertyRelative("damageAnimation"), scalingRules, "Damage Animation", "Damage");
        BuildAnimation(details, settings.FindPropertyRelative("deathAnimation"), scalingRules, "Death Animation", "Death");
        BuildComboRankAnimations(panel, details, settings.FindPropertyRelative("comboRankAnimations"), scalingRules);
        BuildPowerUpAnimations(panel, details, settings.FindPropertyRelative("powerUpAnimations"), scalingRules);
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

    #region Animation Blocks
    /// <summary>
    /// Builds one portrait animation editor block.
    /// </summary>
    /// <param name="parent">Parent container receiving the animation foldout.</param>
    /// <param name="animation">Serialized animation definition.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    /// <param name="title">Foldout title.</param>
    /// <param name="stateSuffix">Stable foldout state suffix.</param>
    private static void BuildAnimation(VisualElement parent,
                                       SerializedProperty animation,
                                       SerializedProperty scalingRules,
                                       string title,
                                       string stateSuffix)
    {
        if (parent == null || animation == null)
            return;

        Foldout foldout = CreateFoldout(title, stateSuffix);
        AttachLazyFoldout(foldout,
                          () =>
                          {
                              AddPlainField(foldout, animation.FindPropertyRelative("animationId"), "Animation Id", "Stable animation ID used by Add Scaling keys and bake diagnostics.");
                              AddPlainField(foldout, animation.FindPropertyRelative("frames"), "Frames", "Ordered sprites played by this portrait animation.");
                              AddField(foldout, animation.FindPropertyRelative("secondsPerFrame"), scalingRules, "Seconds Per Frame", "Base seconds spent on each frame before playback speed is applied.");
                              AddField(foldout, animation.FindPropertyRelative("playbackSpeedMultiplier"), scalingRules, "Playback Speed", "Runtime multiplier applied to frame timing.");
                              AddField(foldout, animation.FindPropertyRelative("playbackMode"), scalingRules, "Playback Mode", "Loop, Once, or PingPong playback behavior.");
                              AddField(foldout, animation.FindPropertyRelative("priority"), scalingRules, "Priority", "Higher priority portrait animations interrupt lower priority states.");
                              AddField(foldout, animation.FindPropertyRelative("restartWhenReentered"), scalingRules, "Restart When Re-entered", "Restarts this animation from the first frame when the same condition fires again.");
                          });
        parent.Add(foldout);
    }

    /// <summary>
    /// Builds combo-rank portrait animation entries with closed rank selectors.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="parent">Parent container receiving the section.</param>
    /// <param name="entries">Serialized combo-rank animation array.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    private static void BuildComboRankAnimations(PlayerVisualPresetsPanel panel,
                                                 VisualElement parent,
                                                 SerializedProperty entries,
                                                 SerializedProperty scalingRules)
    {
        Foldout foldout = CreateFoldout("Combo Rank Idle Animations", "ComboRankAnimations");
        List<string> rankOptions = BuildComboRankOptions();

        if (rankOptions.Count <= 0)
            foldout.Add(new HelpBox("Select a Player Progression Preset in the master context to expose combo-rank selectors.",
                                    HelpBoxMessageType.Info));

        BuildArrayEntries(panel,
                          foldout,
                          entries,
                          "Combo Rank Animation",
                          rankOptions,
                          "rankId",
                          "Add Combo Rank Animation",
                          scalingRules);
        parent.Add(foldout);
    }

    /// <summary>
    /// Builds power-up portrait animation entries with closed power-up selectors.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="parent">Parent container receiving the section.</param>
    /// <param name="entries">Serialized power-up animation array.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    private static void BuildPowerUpAnimations(PlayerVisualPresetsPanel panel,
                                               VisualElement parent,
                                               SerializedProperty entries,
                                               SerializedProperty scalingRules)
    {
        Foldout foldout = CreateFoldout("Power-up Acquisition Animations", "PowerUpAnimations");
        List<string> powerUpOptions = BuildPowerUpOptions();

        if (powerUpOptions.Count <= 0)
            foldout.Add(new HelpBox("Select a Player Power-ups Preset in the master context to expose power-up selectors.",
                                    HelpBoxMessageType.Info));

        if (entries != null)
        {
            for (int entryIndex = 0; entryIndex < entries.arraySize; entryIndex++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(entryIndex);
                Foldout entryFoldout = CreateFoldout(string.Format("Power-up Animation {0}", entryIndex + 1),
                                                     "PowerUpAnimations." + entryIndex);
                BuildPowerUpIdList(panel,
                                   entryFoldout,
                                   entry.FindPropertyRelative("powerUpIds"),
                                   powerUpOptions);
                BuildAnimation(entryFoldout,
                               entry.FindPropertyRelative("animation"),
                               scalingRules,
                               "Animation",
                               "PowerUpAnimations." + entryIndex + ".Animation");
                AddRemoveButton(panel, entryFoldout, entries, entryIndex, "Remove Power-up Animation");
                foldout.Add(entryFoldout);
            }

            Button addButton = new Button(() => AddArrayEntry(panel, entries));
            addButton.text = "Add Power-up Animation";
            foldout.Add(addButton);
        }

        parent.Add(foldout);
    }
    #endregion

    #region Array Helpers
    /// <summary>
    /// Builds an array of portrait animation entries with one selector field.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="parent">Parent container receiving entry foldouts.</param>
    /// <param name="entries">Serialized entry array.</param>
    /// <param name="entryTitle">Entry title prefix.</param>
    /// <param name="options">Closed selector options.</param>
    /// <param name="selectorPropertyName">Relative string property used by the selector.</param>
    /// <param name="addButtonText">Text for the add-entry button.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    private static void BuildArrayEntries(PlayerVisualPresetsPanel panel,
                                          VisualElement parent,
                                          SerializedProperty entries,
                                          string entryTitle,
                                          List<string> options,
                                          string selectorPropertyName,
                                          string addButtonText,
                                          SerializedProperty scalingRules)
    {
        if (entries == null)
            return;

        for (int entryIndex = 0; entryIndex < entries.arraySize; entryIndex++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(entryIndex);
            Foldout entryFoldout = CreateFoldout(string.Format("{0} {1}", entryTitle, entryIndex + 1),
                                                 entryTitle + "." + entryIndex);
            BuildClosedStringSelector(panel,
                                      entryFoldout,
                                      entry.FindPropertyRelative(selectorPropertyName),
                                      options,
                                      selectorPropertyName);
            BuildAnimation(entryFoldout,
                           entry.FindPropertyRelative("animation"),
                           scalingRules,
                           "Animation",
                           entryTitle + "." + entryIndex + ".Animation");
            AddRemoveButton(panel, entryFoldout, entries, entryIndex, "Remove");
            parent.Add(entryFoldout);
        }

        Button addButton = new Button(() => AddArrayEntry(panel, entries));
        addButton.text = addButtonText;
        parent.Add(addButton);
    }

    /// <summary>
    /// Builds the list of power-up ID selectors for one power-up animation entry.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="parent">Parent container receiving selector rows.</param>
    /// <param name="powerUpIds">Serialized string array of power-up IDs.</param>
    /// <param name="options">Closed power-up options.</param>
    private static void BuildPowerUpIdList(PlayerVisualPresetsPanel panel,
                                           VisualElement parent,
                                           SerializedProperty powerUpIds,
                                           List<string> options)
    {
        if (powerUpIds == null)
            return;

        for (int idIndex = 0; idIndex < powerUpIds.arraySize; idIndex++)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            BuildClosedStringSelector(panel,
                                      row,
                                      powerUpIds.GetArrayElementAtIndex(idIndex),
                                      options,
                                      "Power-up");
            Button removeButton = new Button(() =>
            {
                powerUpIds.DeleteArrayElementAtIndex(idIndex);
                Apply(panel);
                panel.RebuildDetails();
            });
            removeButton.text = "-";
            removeButton.tooltip = "Remove this power-up binding.";
            row.Add(removeButton);
            parent.Add(row);
        }

        Button addButton = new Button(() =>
        {
            powerUpIds.arraySize++;
            SerializedProperty inserted = powerUpIds.GetArrayElementAtIndex(powerUpIds.arraySize - 1);
            inserted.stringValue = options.Count > 0 ? options[0] : string.Empty;
            Apply(panel);
            panel.RebuildDetails();
        });
        addButton.text = "Add Power-up";
        parent.Add(addButton);
    }

    /// <summary>
    /// Adds one new serialized array entry and rebuilds the panel.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="entries">Serialized array to mutate.</param>
    private static void AddArrayEntry(PlayerVisualPresetsPanel panel, SerializedProperty entries)
    {
        if (entries == null)
            return;

        entries.arraySize++;
        Apply(panel);
        panel.RebuildDetails();
    }

    /// <summary>
    /// Adds a remove button for one serialized array entry.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="parent">Parent container receiving the button.</param>
    /// <param name="entries">Serialized array to mutate.</param>
    /// <param name="entryIndex">Entry index to remove.</param>
    /// <param name="label">Button text.</param>
    private static void AddRemoveButton(PlayerVisualPresetsPanel panel,
                                        VisualElement parent,
                                        SerializedProperty entries,
                                        int entryIndex,
                                        string label)
    {
        Button removeButton = new Button(() =>
        {
            entries.DeleteArrayElementAtIndex(entryIndex);
            Apply(panel);
            panel.RebuildDetails();
        });
        removeButton.text = label;
        parent.Add(removeButton);
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
    private static void AddField(VisualElement parent,
                                 SerializedProperty property,
                                 SerializedProperty scalingRules,
                                 string label,
                                 string tooltip)
    {
        if (parent == null || property == null)
            return;

        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property,
                                                                           scalingRules,
                                                                           label,
                                                                           null,
                                                                           false);
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

    /// <summary>
    /// Builds a dropdown that writes a serialized string from a closed option list.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="parent">Parent container receiving the dropdown.</param>
    /// <param name="property">Serialized string property to update.</param>
    /// <param name="options">Closed selector options.</param>
    /// <param name="label">Field label.</param>
    private static void BuildClosedStringSelector(PlayerVisualPresetsPanel panel,
                                                  VisualElement parent,
                                                  SerializedProperty property,
                                                  List<string> options,
                                                  string label)
    {
        if (parent == null || property == null)
            return;

        if (options == null || options.Count <= 0)
        {
            AddPlainField(parent, property, label, "No scoped options are currently available.");
            return;
        }

        string currentValue = property.stringValue;

        if (!options.Contains(currentValue))
            currentValue = options[0];

        DropdownField dropdown = new DropdownField(label, options, currentValue);
        dropdown.tooltip = "Closed selector populated from the active Player Management preset context.";
        dropdown.RegisterValueChangedCallback(evt =>
        {
            property.stringValue = evt.newValue;
            Apply(panel);
        });
        parent.Add(dropdown);
    }
    #endregion

    #region Options
    /// <summary>
    /// Builds combo-rank ID options from the active progression preset.
    /// </summary>
    /// <returns>Closed combo-rank option list.</returns>
    private static List<string> BuildComboRankOptions()
    {
        List<string> options = new List<string>();
        PlayerProgressionPreset progressionPreset = PlayerManagementSelectionContext.ActiveProgressionPreset;

        if (progressionPreset == null || progressionPreset.ComboCounter == null || progressionPreset.ComboCounter.RankDefinitions == null)
            return options;

        for (int rankIndex = 0; rankIndex < progressionPreset.ComboCounter.RankDefinitions.Count; rankIndex++)
        {
            PlayerComboRankDefinition rank = progressionPreset.ComboCounter.RankDefinitions[rankIndex];

            if (rank == null || string.IsNullOrWhiteSpace(rank.RankId))
                continue;

            string rankId = rank.RankId.Trim();

            if (!options.Contains(rankId))
                options.Add(rankId);
        }

        return options;
    }

    /// <summary>
    /// Builds power-up ID options from the active power-ups preset.
    /// </summary>
    /// <returns>Closed power-up option list.</returns>
    private static List<string> BuildPowerUpOptions()
    {
        List<string> options = new List<string>();
        PlayerPowerUpsPreset powerUpsPreset = PlayerManagementSelectionContext.ActivePowerUpsPreset;

        if (powerUpsPreset == null)
            return options;

        AddPowerUpOptions(powerUpsPreset.ActivePowerUps, options);
        AddPowerUpOptions(powerUpsPreset.PassivePowerUps, options);
        return options;
    }

    /// <summary>
    /// Appends unique power-up IDs from one definition list.
    /// </summary>
    /// <param name="definitions">Power-up definitions to inspect.</param>
    /// <param name="options">Destination option list.</param>
    private static void AddPowerUpOptions(IReadOnlyList<ModularPowerUpDefinition> definitions, List<string> options)
    {
        if (definitions == null)
            return;

        for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
        {
            ModularPowerUpDefinition definition = definitions[definitionIndex];

            if (definition == null || definition.CommonData == null || string.IsNullOrWhiteSpace(definition.CommonData.PowerUpId))
                continue;

            string powerUpId = definition.CommonData.PowerUpId.Trim();

            if (!options.Contains(powerUpId))
                options.Add(powerUpId);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Applies serialized changes and marks the draft session dirty.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    private static void Apply(PlayerVisualPresetsPanel panel)
    {
        if (panel == null || panel.PresetSerializedObject == null)
            return;

        panel.PresetSerializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
    }

    /// <summary>
    /// Creates one themed nested foldout with a stable state key.
    /// </summary>
    /// <param name="title">User-facing foldout title.</param>
    /// <param name="stateSuffix">Stable state-key suffix.</param>
    /// <returns>Configured nested foldout.</returns>
    private static Foldout CreateFoldout(string title, string stateSuffix)
    {
        return ManagementToolFoldoutStateUtility.CreateFoldout(title,
                                                                "NashCore.PlayerManagement.Visual.Portrait.Lazy." + stateSuffix,
                                                                false);
    }

    /// <summary>
    /// Builds a foldout body only when the user opens it, avoiding heavy nested property construction during tab activation.
    /// </summary>
    /// <param name="foldout">Foldout that owns the lazy body.</param>
    /// <param name="buildContent">Content builder invoked at most once.</param>
    private static void AttachLazyFoldout(Foldout foldout, Action buildContent)
    {
        if (foldout == null || buildContent == null)
            return;

        bool isBuilt = false;

        void EnsureBuilt()
        {
            if (isBuilt)
                return;

            isBuilt = true;
            buildContent.Invoke();
        }

        if (foldout.value)
            EnsureBuilt();

        foldout.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue)
                EnsureBuilt();
        });
    }
    #endregion

    #endregion
}
