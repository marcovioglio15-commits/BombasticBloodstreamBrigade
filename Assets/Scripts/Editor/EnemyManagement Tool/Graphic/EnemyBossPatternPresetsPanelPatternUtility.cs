using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds the boss Pattern Assemble subsection using the same Core, Short-Range and Weapon slots as normal enemies.
/// </summary>
internal static class EnemyBossPatternPresetsPanelPatternUtility
{
    #region Constants
    private const float DefaultMaximumConditionSeconds = 180f;
    private const float DefaultMaximumTravelDistance = 300f;
    private const float DefaultMaximumPlayerDistance = 64f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the boss pattern assemble section with base slots and ordered boss interactions.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized preset context.</param>
    public static void BuildPatternAssembleSection(EnemyBossPatternPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = EnemyBossPatternPresetsPanelSharedUtility.CreateDetailsSectionContainer(panel, "Pattern Assemble");

        if (sectionContainer == null)
            return;

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        SerializedProperty basePatternProperty = presetSerializedObject.FindProperty("basePattern");
        SerializedProperty interactionsProperty = presetSerializedObject.FindProperty("interactions");
        SerializedProperty sourcePatternsProperty = presetSerializedObject.FindProperty("sourcePatternsPreset");
        EnemyModulesAndPatternsPreset sourcePreset = sourcePatternsProperty != null
            ? sourcePatternsProperty.objectReferenceValue as EnemyModulesAndPatternsPreset
            : null;

        if (sourcePreset == null)
            sectionContainer.Add(new HelpBox("Assign a source Modules & Patterns preset before configuring boss Pattern Assemble slots.", HelpBoxMessageType.Warning));
        else
            sectionContainer.Add(new HelpBox("Bosses use the normal Core Movement, Short-Range Interaction and Weapon Interaction slots. Boss Interactions are ordered override layers such as Missing Health Interaction.", HelpBoxMessageType.Info));

        BuildBasePatternCard(panel, basePatternProperty, sourcePreset, sectionContainer);
        BuildInteractionCards(panel, interactionsProperty, sourcePreset, sectionContainer);
        EnemyBossPatternPresetsPanelWarningUtility.AddPatternWarnings(basePatternProperty, interactionsProperty, sourcePreset, sectionContainer);
    }
    #endregion

    #region Base Pattern
    /// <summary>
    /// Builds the always-available base pattern assemble card.
    /// </summary>
    /// <param name="panel">Owning panel used for rebuild callbacks.</param>
    /// <param name="basePatternProperty">Serialized base pattern root.</param>
    /// <param name="sourcePreset">Source module catalog.</param>
    /// <param name="parent">Parent receiving the card.</param>
    private static void BuildBasePatternCard(EnemyBossPatternPresetsPanel panel,
                                             SerializedProperty basePatternProperty,
                                             EnemyModulesAndPatternsPreset sourcePreset,
                                             VisualElement parent)
    {
        if (basePatternProperty == null || parent == null)
            return;

        VisualElement card = EnemyBossPatternPresetsPanelSharedUtility.CreateCard();
        Foldout foldout = ManagementToolFoldoutStateUtility.CreatePropertyFoldout(basePatternProperty,
                                                                                  "Base Pattern Assemble",
                                                                                  "BossBasePatternAssemble",
                                                                                  true);
        card.Add(foldout);
        BuildBaseCoreMovementSlot(panel, foldout, basePatternProperty.FindPropertyRelative("coreMovement"), sourcePreset);
        BuildShortRangeSlot(panel,
                            foldout,
                            basePatternProperty.FindPropertyRelative("shortRangeInteraction"),
                            sourcePreset,
                            "Short-Range Interaction",
                            "Enable Short-Range Interaction",
                            "Optional base short-range interaction inherited by boss interactions unless they override it.");
        BuildWeaponSlot(panel,
                        foldout,
                        basePatternProperty.FindPropertyRelative("weaponInteraction"),
                        sourcePreset,
                        "Weapon Interaction",
                        "Enable Weapon Interaction",
                        "Optional base weapon interaction inherited by boss interactions unless they override it.");
        parent.Add(card);
    }

    /// <summary>
    /// Builds the required base Core Movement slot.
    /// </summary>
    /// <param name="panel">Owning panel used for serialized context.</param>
    /// <param name="parent">Parent receiving controls.</param>
    /// <param name="coreMovementProperty">Serialized core movement root.</param>
    /// <param name="sourcePreset">Source module catalog.</param>
    private static void BuildBaseCoreMovementSlot(EnemyBossPatternPresetsPanel panel,
                                                  VisualElement parent,
                                                  SerializedProperty coreMovementProperty,
                                                  EnemyModulesAndPatternsPreset sourcePreset)
    {
        Foldout foldout = CreateSlotFoldout(coreMovementProperty,
                                            "Core Movement",
                                            "Always-active base movement used when no boss interaction overrides Core Movement.",
                                            true);

        if (coreMovementProperty != null)
        {
            EnemyBossPatternPresetsPanelModuleUtility.AddModuleBindingSelector(panel,
                                                                               foldout,
                                                                               coreMovementProperty.FindPropertyRelative("binding"),
                                                                               sourcePreset,
                                                                               EnemyPatternModuleCatalogSection.CoreMovement,
                                                                               "Core Movement Module",
                                                                               "Select the base Core Movement module from the source preset.");
        }

        parent.Add(foldout);
    }
    #endregion

    #region Interactions
    /// <summary>
    /// Builds ordered boss interaction cards and list actions.
    /// </summary>
    /// <param name="panel">Owning panel used for rebuild callbacks.</param>
    /// <param name="interactionsProperty">Serialized interactions array.</param>
    /// <param name="sourcePreset">Source module catalog.</param>
    /// <param name="parent">Parent receiving interaction UI.</param>
    private static void BuildInteractionCards(EnemyBossPatternPresetsPanel panel,
                                              SerializedProperty interactionsProperty,
                                              EnemyModulesAndPatternsPreset sourcePreset,
                                              VisualElement parent)
    {
        if (interactionsProperty == null || parent == null)
            return;

        Label header = new Label("Boss Interactions");
        header.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
        header.style.marginTop = 8f;
        parent.Add(header);

        for (int index = 0; index < interactionsProperty.arraySize; index++)
        {
            SerializedProperty interactionProperty = interactionsProperty.GetArrayElementAtIndex(index);

            if (interactionProperty == null)
                continue;

            BuildInteractionCard(panel, interactionsProperty, interactionProperty, sourcePreset, index, parent);
        }

        Button addButton = new Button(() =>
        {
            AddInteraction(panel, interactionsProperty, sourcePreset);
        });
        addButton.text = "Add Boss Interaction";
        addButton.tooltip = "Add one ordered boss interaction such as Missing Health Interaction.";
        addButton.style.marginTop = 4f;
        addButton.SetEnabled(sourcePreset != null && EnemyBossPatternPresetsPanelModuleUtility.HasAnySelectableModule(sourcePreset));
        parent.Add(addButton);
    }

    /// <summary>
    /// Builds one ordered boss interaction card.
    /// </summary>
    /// <param name="panel">Owning panel used for rebuild callbacks.</param>
    /// <param name="interactionsProperty">Serialized array that owns the interaction.</param>
    /// <param name="interactionProperty">Serialized interaction being drawn.</param>
    /// <param name="sourcePreset">Source module catalog.</param>
    /// <param name="index">Interaction index in the array.</param>
    /// <param name="parent">Parent receiving the card.</param>
    private static void BuildInteractionCard(EnemyBossPatternPresetsPanel panel,
                                             SerializedProperty interactionsProperty,
                                             SerializedProperty interactionProperty,
                                             EnemyModulesAndPatternsPreset sourcePreset,
                                             int index,
                                             VisualElement parent)
    {
        VisualElement card = EnemyBossPatternPresetsPanelSharedUtility.CreateCard();
        Foldout foldout = ManagementToolFoldoutStateUtility.CreatePropertyFoldout(interactionProperty,
                                                                                  BuildInteractionTitle(interactionProperty, index),
                                                                                  "BossInteraction",
                                                                                  index == 0);
        card.Add(foldout);
        foldout.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateArrayActionsRow(panel, interactionsProperty, index, "Boss Interaction"));

        SerializedProperty enabledProperty = interactionProperty.FindPropertyRelative("enabled");
        SerializedProperty interactionTypeProperty = interactionProperty.FindPropertyRelative("interactionType");
        SerializedProperty displayNameProperty = interactionProperty.FindPropertyRelative("displayName");
        SerializedProperty minimumActiveSecondsProperty = interactionProperty.FindPropertyRelative("minimumActiveSeconds");
        EnemyBossPatternInteractionType interactionType = ResolveInteractionType(interactionTypeProperty);

        foldout.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel, enabledProperty, "Enabled", "Enables this boss interaction during bake and runtime selection."));
        foldout.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel, interactionTypeProperty, "Interaction Type", "Boss-only trigger that decides when this interaction can override the base pattern."));
        EnemyBossPatternPresetsPanelSharedUtility.AddTrackedTextField(panel, foldout, displayNameProperty, "Interaction Name", "Readable interaction name shown by the Boss Pattern Assemble section.", false);
        AddInteractionTypeFields(panel, foldout, interactionProperty, interactionType);
        EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, foldout, minimumActiveSecondsProperty, "Minimum Active Seconds", 0f, 20f, "Minimum seconds this interaction remains active before another valid interaction can replace it.");
        BuildCoreOverrideSlot(panel, foldout, interactionProperty.FindPropertyRelative("coreMovement"), sourcePreset);
        BuildShortRangeSlot(panel,
                            foldout,
                            interactionProperty.FindPropertyRelative("shortRangeInteraction"),
                            sourcePreset,
                            "Short-Range Interaction Override",
                            "Override Short-Range Interaction",
                            "When enabled, this boss interaction replaces the inherited Short-Range Interaction slot.");
        BuildWeaponSlot(panel,
                        foldout,
                        interactionProperty.FindPropertyRelative("weaponInteraction"),
                        sourcePreset,
                        "Weapon Interaction Override",
                        "Override Weapon Interaction",
                        "When enabled, this boss interaction replaces the inherited Weapon Interaction slot.");
        parent.Add(card);
    }

    /// <summary>
    /// Adds the trigger-specific threshold fields for one boss interaction.
    /// </summary>
    /// <param name="panel">Owning panel.</param>
    /// <param name="parent">Parent receiving controls.</param>
    /// <param name="interactionProperty">Serialized interaction root.</param>
    /// <param name="interactionType">Selected interaction type.</param>
    private static void AddInteractionTypeFields(EnemyBossPatternPresetsPanel panel,
                                                 VisualElement parent,
                                                 SerializedProperty interactionProperty,
                                                 EnemyBossPatternInteractionType interactionType)
    {
        switch (interactionType)
        {
            case EnemyBossPatternInteractionType.ElapsedTime:
                EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, interactionProperty.FindPropertyRelative("minimumElapsedSeconds"), "Minimum Elapsed Seconds", 0f, DefaultMaximumConditionSeconds, "Minimum seconds since boss spawn required by this interaction.");
                EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, interactionProperty.FindPropertyRelative("maximumElapsedSeconds"), "Maximum Elapsed Seconds", 0f, DefaultMaximumConditionSeconds, "Maximum seconds since boss spawn allowed by this interaction. Zero disables the upper bound.");
                break;

            case EnemyBossPatternInteractionType.TravelledDistance:
                EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, interactionProperty.FindPropertyRelative("minimumTravelledDistance"), "Minimum Travelled Distance", 0f, DefaultMaximumTravelDistance, "Minimum planar distance travelled by the boss before this interaction can activate.");
                EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, interactionProperty.FindPropertyRelative("maximumTravelledDistance"), "Maximum Travelled Distance", 0f, DefaultMaximumTravelDistance, "Maximum planar distance travelled by the boss while this interaction can activate. Zero disables the upper bound.");
                break;

            case EnemyBossPatternInteractionType.PlayerDistance:
                EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, interactionProperty.FindPropertyRelative("minimumPlayerDistance"), "Minimum Player Distance", 0f, DefaultMaximumPlayerDistance, "Minimum planar player distance required by this interaction.");
                EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, interactionProperty.FindPropertyRelative("maximumPlayerDistance"), "Maximum Player Distance", 0f, DefaultMaximumPlayerDistance, "Maximum planar player distance allowed by this interaction. Zero disables the upper bound.");
                break;

            case EnemyBossPatternInteractionType.RecentlyDamaged:
                EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, interactionProperty.FindPropertyRelative("recentlyDamagedWindowSeconds"), "Recently Damaged Window", 0.05f, 10f, "Seconds after receiving damage for which this interaction is valid.");
                break;

            default:
                EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, interactionProperty.FindPropertyRelative("minimumMissingHealthPercent"), "Minimum Missing Health", 0f, 1f, "Minimum normalized missing health required by this interaction.");
                EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, interactionProperty.FindPropertyRelative("maximumMissingHealthPercent"), "Maximum Missing Health", 0f, 1f, "Maximum normalized missing health allowed by this interaction. Zero disables the upper bound.");
                break;
        }
    }
    #endregion

    #region Slots
    /// <summary>
    /// Builds an optional Core Movement override slot.
    /// </summary>
    /// <param name="panel">Owning panel used for serialized context.</param>
    /// <param name="parent">Parent receiving controls.</param>
    /// <param name="coreMovementProperty">Serialized core override root.</param>
    /// <param name="sourcePreset">Source module catalog.</param>
    private static void BuildCoreOverrideSlot(EnemyBossPatternPresetsPanel panel,
                                              VisualElement parent,
                                              SerializedProperty coreMovementProperty,
                                              EnemyModulesAndPatternsPreset sourcePreset)
    {
        Foldout foldout = CreateSlotFoldout(coreMovementProperty,
                                            "Core Movement Override",
                                            "Optional Core Movement override applied while this boss interaction is active.",
                                            false);

        if (coreMovementProperty != null)
        {
            SerializedProperty enabledProperty = coreMovementProperty.FindPropertyRelative("isEnabled");
            foldout.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel, enabledProperty, "Override Core Movement", "When enabled, this boss interaction replaces the base Core Movement slot."));

            if (enabledProperty != null && enabledProperty.boolValue)
            {
                EnemyBossPatternPresetsPanelModuleUtility.AddModuleBindingSelector(panel,
                                                                                   foldout,
                                                                                   coreMovementProperty.FindPropertyRelative("binding"),
                                                                                   sourcePreset,
                                                                                   EnemyPatternModuleCatalogSection.CoreMovement,
                                                                                   "Core Movement Module",
                                                                                   "Select the Core Movement override module from the source preset.");
            }
        }

        parent.Add(foldout);
    }

    /// <summary>
    /// Builds a short-range slot with dependent controls.
    /// </summary>
    /// <param name="panel">Owning panel used for serialized context.</param>
    /// <param name="parent">Parent receiving controls.</param>
    /// <param name="shortRangeProperty">Serialized short-range slot root.</param>
    /// <param name="sourcePreset">Source module catalog.</param>
    /// <param name="title">Foldout title.</param>
    /// <param name="enabledLabel">Enabled toggle label.</param>
    /// <param name="tooltip">Foldout tooltip.</param>
    private static void BuildShortRangeSlot(EnemyBossPatternPresetsPanel panel,
                                            VisualElement parent,
                                            SerializedProperty shortRangeProperty,
                                            EnemyModulesAndPatternsPreset sourcePreset,
                                            string title,
                                            string enabledLabel,
                                            string tooltip)
    {
        Foldout foldout = CreateSlotFoldout(shortRangeProperty, title, tooltip, false);

        if (shortRangeProperty != null)
        {
            SerializedProperty enabledProperty = shortRangeProperty.FindPropertyRelative("isEnabled");
            foldout.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel, enabledProperty, enabledLabel, tooltip));

            if (enabledProperty != null && enabledProperty.boolValue)
            {
                EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, foldout, shortRangeProperty.FindPropertyRelative("activationRange"), "Activation Range", 0f, DefaultMaximumPlayerDistance, "Distance at which this short-range slot starts overriding core movement.");
                EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, foldout, shortRangeProperty.FindPropertyRelative("releaseDistanceBuffer"), "Release Buffer", 0f, 16f, "Extra distance added after activation before this short-range slot releases back to core movement.");
                EnemyBossPatternPresetsPanelModuleUtility.AddModuleBindingSelector(panel,
                                                                                   foldout,
                                                                                   shortRangeProperty.FindPropertyRelative("binding"),
                                                                                   sourcePreset,
                                                                                   EnemyPatternModuleCatalogSection.ShortRangeInteraction,
                                                                                   "Short-Range Module",
                                                                                   "Select the Short-Range Interaction module from the source preset.");
                BuildEngagementFeedbackFields(panel,
                                              foldout,
                                              shortRangeProperty,
                                              sourcePreset,
                                              EnemyPatternModuleCatalogSection.ShortRangeInteraction,
                                              "Short-Range");
            }
        }

        parent.Add(foldout);
    }

    /// <summary>
    /// Builds a weapon slot with dependent range and activation-gate controls.
    /// </summary>
    /// <param name="panel">Owning panel used for serialized context.</param>
    /// <param name="parent">Parent receiving controls.</param>
    /// <param name="weaponProperty">Serialized weapon slot root.</param>
    /// <param name="sourcePreset">Source module catalog.</param>
    /// <param name="title">Foldout title.</param>
    /// <param name="enabledLabel">Enabled toggle label.</param>
    /// <param name="tooltip">Foldout tooltip.</param>
    private static void BuildWeaponSlot(EnemyBossPatternPresetsPanel panel,
                                        VisualElement parent,
                                        SerializedProperty weaponProperty,
                                        EnemyModulesAndPatternsPreset sourcePreset,
                                        string title,
                                        string enabledLabel,
                                        string tooltip)
    {
        Foldout foldout = CreateSlotFoldout(weaponProperty, title, tooltip, false);

        if (weaponProperty != null)
        {
            SerializedProperty enabledProperty = weaponProperty.FindPropertyRelative("isEnabled");
            SerializedProperty useMinimumRangeProperty = weaponProperty.FindPropertyRelative("useMinimumRange");
            SerializedProperty useMaximumRangeProperty = weaponProperty.FindPropertyRelative("useMaximumRange");
            SerializedProperty activationGatesProperty = weaponProperty.FindPropertyRelative("activationGates");
            EnemyWeaponInteractionActivationGate activationGates = EnemyBossPatternPresetsPanelModuleUtility.ResolveWeaponActivationGates(activationGatesProperty);
            foldout.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel, enabledProperty, enabledLabel, tooltip));

            if (enabledProperty != null && enabledProperty.boolValue)
            {
                foldout.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel, useMinimumRangeProperty, "Use Minimum Range", "Require the player to be farther than the minimum range before this weapon slot can fire."));

                if (useMinimumRangeProperty != null && useMinimumRangeProperty.boolValue)
                    EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, foldout, weaponProperty.FindPropertyRelative("minimumRange"), "Minimum Range", 0f, DefaultMaximumPlayerDistance, "Minimum player distance required by this weapon slot.");

                foldout.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel, useMaximumRangeProperty, "Use Maximum Range", "Require the player to stay within the maximum range before this weapon slot can fire."));

                if (useMaximumRangeProperty != null && useMaximumRangeProperty.boolValue)
                    EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, foldout, weaponProperty.FindPropertyRelative("maximumRange"), "Maximum Range", 0f, DefaultMaximumPlayerDistance, "Maximum player distance allowed by this weapon slot.");

                foldout.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel, weaponProperty.FindPropertyRelative("exclusiveLookDirectionControl"), "Exclusive Look Direction", "Let this weapon slot own enemy look direction while its activation gates are valid."));
                foldout.Add(CreateReactiveWeaponGateField(panel, activationGatesProperty, "Activation Gates", "Optional non-range gates evaluated by the shooter runtime."));

                if (activationGates.HasFlag(EnemyWeaponInteractionActivationGate.RequireBelowSpeed))
                    EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, foldout, weaponProperty.FindPropertyRelative("maximumActivationSpeed"), "Maximum Activation Speed", 0f, 12f, "Maximum planar enemy speed allowed by the Require Below Speed gate.");

                if (activationGates.HasFlag(EnemyWeaponInteractionActivationGate.RequireRecentlyDamaged))
                    EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, foldout, weaponProperty.FindPropertyRelative("recentlyDamagedWindowSeconds"), "Weapon Damage Window", 0.05f, 10f, "Seconds after receiving damage during which the weapon gate remains valid.");

                EnemyBossPatternPresetsPanelModuleUtility.AddModuleBindingSelector(panel,
                                                                                   foldout,
                                                                                   weaponProperty.FindPropertyRelative("binding"),
                                                                                   sourcePreset,
                                                                                   EnemyPatternModuleCatalogSection.WeaponInteraction,
                                                                                   "Weapon Module",
                                                                                   "Select the Weapon Interaction module from the source preset.");
                BuildEngagementFeedbackFields(panel,
                                              foldout,
                                              weaponProperty,
                                              sourcePreset,
                                              EnemyPatternModuleCatalogSection.WeaponInteraction,
                                              "Weapon");
            }
        }

        parent.Add(foldout);
    }

    /// <summary>
    /// Adds optional offensive engagement feedback fields for a slot.
    /// </summary>
    /// <param name="panel">Owning panel.</param>
    /// <param name="parent">Parent receiving controls.</param>
    /// <param name="slotProperty">Serialized slot root.</param>
    /// <param name="labelPrefix">Slot label prefix.</param>
    private static void BuildEngagementFeedbackFields(EnemyBossPatternPresetsPanel panel,
                                                      VisualElement parent,
                                                      SerializedProperty slotProperty,
                                                      EnemyModulesAndPatternsPreset sourcePreset,
                                                      EnemyPatternModuleCatalogSection section,
                                                      string labelPrefix)
    {
        SerializedProperty displayTriggerProperty = slotProperty.FindPropertyRelative("displayBehaviourEngagementTrigger");
        SerializedProperty useOverrideProperty = slotProperty.FindPropertyRelative("useEngagementFeedbackOverride");
        SerializedProperty overrideProperty = slotProperty.FindPropertyRelative("engagementFeedbackOverride");
        SerializedProperty bindingProperty = slotProperty.FindPropertyRelative("binding");
        bool supportsFeedback = SupportsEngagementFeedback(sourcePreset, bindingProperty, section);

        if (!supportsFeedback && (displayTriggerProperty == null || !displayTriggerProperty.boolValue))
            return;

        parent.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel, displayTriggerProperty, "Display Behaviour Engagement Trigger", "When enabled, this " + labelPrefix + " slot emits offensive engagement feedback before supported behaviour commits."));

        if (!supportsFeedback)
        {
            parent.Add(new HelpBox(labelPrefix + " offensive engagement feedback is enabled, but the selected module does not expose a predictive timing hook for this slot.", HelpBoxMessageType.Warning));
            return;
        }

        if (displayTriggerProperty == null || !displayTriggerProperty.boolValue)
            return;

        parent.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel, useOverrideProperty, "Use Engagement Feedback Override", "Override the generic offensive engagement feedback settings for this " + labelPrefix + " slot."));

        if (useOverrideProperty != null && useOverrideProperty.boolValue)
            parent.Add(EnemyOffensiveEngagementFeedbackDrawerUtility.BuildSettingsEditor(overrideProperty));
    }

    /// <summary>
    /// Resolves whether a boss pattern slot currently supports offensive engagement feedback.
    /// </summary>
    /// <param name="sourcePreset">Source module catalog used by boss assemble slots.</param>
    /// <param name="bindingProperty">Serialized module binding to inspect.</param>
    /// <param name="section">Catalog section used by the slot.</param>
    /// <returns>True when the selected source module exposes a supported timing hook.</returns>
    private static bool SupportsEngagementFeedback(EnemyModulesAndPatternsPreset sourcePreset,
                                                   SerializedProperty bindingProperty,
                                                   EnemyPatternModuleCatalogSection section)
    {
        if (sourcePreset == null || bindingProperty == null)
            return false;

        SerializedProperty moduleIdProperty = bindingProperty.FindPropertyRelative("moduleId");
        string moduleId = moduleIdProperty != null ? moduleIdProperty.stringValue : string.Empty;

        if (string.IsNullOrWhiteSpace(moduleId))
            return false;

        EnemyPatternModuleDefinition moduleDefinition = sourcePreset.ResolveModuleDefinitionById(moduleId);

        if (moduleDefinition == null)
            return false;

        return EnemyOffensiveEngagementSupportUtility.SupportsTimingMode(section, moduleDefinition.ModuleKind);
    }
    #endregion

    #region Mutations
    /// <summary>
    /// Adds one boss interaction initialized from the first available Core Movement module.
    /// </summary>
    /// <param name="panel">Owning panel used for serialized context and rebuild callbacks.</param>
    /// <param name="interactionsProperty">Serialized interactions array.</param>
    /// <param name="sourcePreset">Source module catalog.</param>
    private static void AddInteraction(EnemyBossPatternPresetsPanel panel,
                                       SerializedProperty interactionsProperty,
                                       EnemyModulesAndPatternsPreset sourcePreset)
    {
        if (panel == null || interactionsProperty == null || sourcePreset == null)
            return;

        EnemyBossPatternPresetsPanelSharedUtility.RecordSelectedPreset(panel, "Add Boss Interaction");
        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        presetSerializedObject.Update();
        int insertIndex = interactionsProperty.arraySize;
        interactionsProperty.InsertArrayElementAtIndex(insertIndex);
        SerializedProperty insertedInteraction = interactionsProperty.GetArrayElementAtIndex(insertIndex);

        if (insertedInteraction != null)
            ConfigureInsertedInteraction(insertedInteraction, sourcePreset, insertIndex);

        presetSerializedObject.ApplyModifiedProperties();
        EnemyBossPatternPresetsPanelSharedUtility.MarkDirtyAndRebuild(panel);
    }

    /// <summary>
    /// Configures serialized defaults for a newly inserted boss interaction.
    /// </summary>
    /// <param name="insertedInteraction">Serialized interaction created by the array insertion.</param>
    /// <param name="sourcePreset">Source module catalog.</param>
    /// <param name="insertIndex">Interaction index used for readable labels.</param>
    private static void ConfigureInsertedInteraction(SerializedProperty insertedInteraction,
                                                     EnemyModulesAndPatternsPreset sourcePreset,
                                                     int insertIndex)
    {
        EnemyBossPatternPresetsPanelModuleUtility.SetBoolean(insertedInteraction.FindPropertyRelative("enabled"), true);
        EnemyBossPatternPresetsPanelModuleUtility.SetEnumIndex(insertedInteraction.FindPropertyRelative("interactionType"), Convert.ToInt32(EnemyBossPatternInteractionType.MissingHealth));
        EnemyBossPatternPresetsPanelModuleUtility.SetString(insertedInteraction.FindPropertyRelative("displayName"), "Missing Health Interaction " + (insertIndex + 1));
        ConfigureDefaultCoreOverride(insertedInteraction.FindPropertyRelative("coreMovement"), sourcePreset);
    }

    /// <summary>
    /// Enables the core override slot on a new interaction when a Core Movement module exists.
    /// </summary>
    /// <param name="coreMovementProperty">Serialized core override root.</param>
    /// <param name="sourcePreset">Source module catalog.</param>
    private static void ConfigureDefaultCoreOverride(SerializedProperty coreMovementProperty, EnemyModulesAndPatternsPreset sourcePreset)
    {
        if (coreMovementProperty == null)
            return;

        if (!EnemyBossPatternPresetsPanelModuleUtility.TryResolveFirstModuleId(sourcePreset, EnemyPatternModuleCatalogSection.CoreMovement, out string moduleId))
            return;

        EnemyBossPatternPresetsPanelModuleUtility.SetBoolean(coreMovementProperty.FindPropertyRelative("isEnabled"), true);
        EnemyBossPatternPresetsPanelModuleUtility.ConfigureBinding(coreMovementProperty.FindPropertyRelative("binding"), moduleId);
    }
    #endregion

    #region Formatting
    /// <summary>
    /// Builds the foldout title for one boss interaction.
    /// </summary>
    /// <param name="interactionProperty">Serialized interaction property.</param>
    /// <param name="index">Interaction index.</param>
    /// <returns>Human-readable title.</returns>
    private static string BuildInteractionTitle(SerializedProperty interactionProperty, int index)
    {
        SerializedProperty displayNameProperty = interactionProperty.FindPropertyRelative("displayName");
        SerializedProperty interactionTypeProperty = interactionProperty.FindPropertyRelative("interactionType");
        string displayName = displayNameProperty != null ? displayNameProperty.stringValue : string.Empty;

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = "Interaction " + (index + 1);

        return "#" + (index + 1).ToString("D2") + " " + FormatInteractionType(ResolveInteractionType(interactionTypeProperty)) + " - " + displayName;
    }

    /// <summary>
    /// Resolves one serialized interaction type property to a typed enum.
    /// </summary>
    /// <param name="interactionTypeProperty">Serialized interaction type property.</param>
    /// <returns>Typed interaction type.</returns>
    private static EnemyBossPatternInteractionType ResolveInteractionType(SerializedProperty interactionTypeProperty)
    {
        if (interactionTypeProperty == null)
            return EnemyBossPatternInteractionType.MissingHealth;

        return (EnemyBossPatternInteractionType)interactionTypeProperty.enumValueIndex;
    }

    /// <summary>
    /// Converts an interaction type into user-facing text.
    /// </summary>
    /// <param name="interactionType">Interaction type to format.</param>
    /// <returns>Human-readable interaction type.</returns>
    private static string FormatInteractionType(EnemyBossPatternInteractionType interactionType)
    {
        return EnemyBossPatternInteractionDefinition.FormatInteractionType(interactionType);
    }

    /// <summary>
    /// Creates a slot foldout with consistent state keys and tooltip.
    /// </summary>
    /// <param name="property">Serialized slot property.</param>
    /// <param name="title">Foldout title.</param>
    /// <param name="tooltip">Foldout tooltip.</param>
    /// <param name="expanded">Initial expanded state.</param>
    /// <returns>Configured foldout.</returns>
    private static Foldout CreateSlotFoldout(SerializedProperty property, string title, string tooltip, bool expanded)
    {
        Foldout foldout = ManagementToolFoldoutStateUtility.CreatePropertyFoldout(property,
                                                                                  title,
                                                                                  title.Replace(" ", string.Empty),
                                                                                  expanded);
        foldout.tooltip = tooltip;
        foldout.style.marginTop = 4f;
        return foldout;
    }

    /// <summary>
    /// Creates the reactive enum-flags field used by weapon interaction gates.
    /// </summary>
    /// <param name="panel">Owning panel used for rebuild callbacks.</param>
    /// <param name="property">Serialized activation gate flags.</param>
    /// <param name="label">Visible field label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    /// <returns>Configured enum-flags field.</returns>
    private static EnumFlagsField CreateReactiveWeaponGateField(EnemyBossPatternPresetsPanel panel,
                                                                SerializedProperty property,
                                                                string label,
                                                                string tooltip)
    {
        EnumFlagsField field = new EnumFlagsField(label, EnemyBossPatternPresetsPanelModuleUtility.ResolveWeaponActivationGates(property));
        field.tooltip = tooltip;
        field.RegisterValueChangedCallback(evt =>
        {
            EnemyWeaponInteractionActivationGate gates = (EnemyWeaponInteractionActivationGate)evt.newValue;
            EnemyBossPatternPresetsPanelSharedUtility.RecordSelectedPreset(panel, "Edit Boss Weapon Interaction");
            panel.PresetSerializedObject.Update();

            if (property != null)
                property.enumValueFlag = Convert.ToInt32(gates);

            panel.PresetSerializedObject.ApplyModifiedProperties();
            EnemyBossPatternPresetsPanelSharedUtility.MarkDirtyAndRebuild(panel);
        });
        return field;
    }
    #endregion

    #endregion
}
