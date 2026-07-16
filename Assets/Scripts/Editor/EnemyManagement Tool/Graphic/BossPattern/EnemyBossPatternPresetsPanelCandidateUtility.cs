using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds boss pattern internal module extraction candidates from the source Modules &amp; Patterns preset.
/// </summary>
internal static class EnemyBossPatternPresetsPanelCandidateUtility
{
    #region Constants
    private const float DefaultMaximumConditionSeconds = 180f;
    private const float DefaultMaximumTravelDistance = 300f;
    private const float DefaultMaximumPlayerDistance = 64f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds one pattern-owned internal extraction block with editable module candidates.
    /// </summary>
    /// <param name="panel">Owning boss preset panel.</param>
    /// <param name="parent">Parent receiving the extraction UI.</param>
    /// <param name="extractionProperty">Serialized extraction definition root.</param>
    /// <param name="sourcePreset">Source module catalog used by boss candidates.</param>
    /// <param name="slotKind">Boss slot controlled by this extraction block.</param>
    /// <param name="label">Foldout label.</param>
    public static void BuildInternalExtractionSlot(EnemyBossPatternPresetsPanel panel,
                                                   VisualElement parent,
                                                   SerializedProperty extractionProperty,
                                                   EnemyModulesAndPatternsPreset sourcePreset,
                                                   EnemyBossPatternSlotKind slotKind,
                                                   string label)
    {
        if (panel == null || parent == null || extractionProperty == null)
            return;

        Foldout foldout = ManagementToolFoldoutStateUtility.CreatePropertyFoldout(extractionProperty,
                                                                                  label,
                                                                                  "BossPatternInternalExtraction" + slotKind,
                                                                                  false);
        foldout.tooltip = "Internal extraction rules and module candidates used while this boss pattern remains active.";
        foldout.style.marginTop = 6f;
        parent.Add(foldout);

        if (sourcePreset == null)
            foldout.Add(new HelpBox("Assign a source Modules & Patterns preset before editing this internal extraction slot.", HelpBoxMessageType.Warning));

        EnemyBossPatternPresetsPanelExtractionUtility.BuildExtractionSettingsCard(panel,
                                                                                  extractionProperty.FindPropertyRelative("extractionSettings"),
                                                                                  foldout,
                                                                                  label + " Rules",
                                                                                  "BossPatternInternalExtractionRules" + slotKind,
                                                                                  false);
        BuildCandidateCards(panel,
                            extractionProperty.FindPropertyRelative("candidates"),
                            sourcePreset,
                            slotKind,
                            foldout);
    }
    #endregion

    #region Candidate List
    /// <summary>
    /// Builds the editable candidate list for one internal extraction slot.
    /// </summary>
    /// <param name="panel">Owning boss preset panel.</param>
    /// <param name="candidatesProperty">Serialized candidates array.</param>
    /// <param name="sourcePreset">Source module catalog used by boss candidates.</param>
    /// <param name="slotKind">Boss slot controlled by the candidates.</param>
    /// <param name="parent">Parent receiving the candidate list.</param>
    private static void BuildCandidateCards(EnemyBossPatternPresetsPanel panel,
                                            SerializedProperty candidatesProperty,
                                            EnemyModulesAndPatternsPreset sourcePreset,
                                            EnemyBossPatternSlotKind slotKind,
                                            VisualElement parent)
    {
        if (panel == null || candidatesProperty == null || parent == null)
            return;

        Label header = new Label("Module Candidates");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginTop = 6f;
        parent.Add(header);

        for (int index = 0; index < candidatesProperty.arraySize; index++)
        {
            SerializedProperty candidateProperty = candidatesProperty.GetArrayElementAtIndex(index);

            if (candidateProperty == null)
                continue;

            BuildCandidateCard(panel, candidatesProperty, candidateProperty, sourcePreset, slotKind, index, parent);
        }

        BuildCandidateActions(panel, candidatesProperty, sourcePreset, slotKind, parent);
    }

    /// <summary>
    /// Builds one module candidate card with eligibility and slot-specific module controls.
    /// </summary>
    /// <param name="panel">Owning boss preset panel.</param>
    /// <param name="candidatesProperty">Serialized array containing this candidate.</param>
    /// <param name="candidateProperty">Serialized candidate root.</param>
    /// <param name="sourcePreset">Source module catalog used by boss candidates.</param>
    /// <param name="slotKind">Boss slot controlled by the candidate.</param>
    /// <param name="index">Candidate index inside the slot list.</param>
    /// <param name="parent">Parent receiving the card.</param>
    private static void BuildCandidateCard(EnemyBossPatternPresetsPanel panel,
                                           SerializedProperty candidatesProperty,
                                           SerializedProperty candidateProperty,
                                           EnemyModulesAndPatternsPreset sourcePreset,
                                           EnemyBossPatternSlotKind slotKind,
                                           int index,
                                           VisualElement parent)
    {
        VisualElement card = EnemyBossPatternPresetsPanelSharedUtility.CreateCard();
        Foldout foldout = ManagementToolFoldoutStateUtility.CreatePropertyFoldout(candidateProperty,
                                                                                  BuildCandidateTitle(candidateProperty, slotKind, index),
                                                                                  "BossPatternModuleCandidate" + slotKind,
                                                                                  index == 0);
        card.Add(foldout);
        foldout.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateArrayActionsRow(panel, candidatesProperty, index, "Boss Module Candidate"));

        SerializedProperty eligibilityProperty = candidateProperty.FindPropertyRelative("eligibility");
        SerializedProperty moduleModeProperty = candidateProperty.FindPropertyRelative("moduleMode");
        BuildEligibilityFields(panel, foldout, eligibilityProperty);
        foldout.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel,
                                                                                          moduleModeProperty,
                                                                                          "Module Mode",
                                                                                          "Module applies a source module candidate. Null Module ignores this slot until the next extraction; for Core Movement it holds the boss stationary."));

        if (ResolveModuleMode(moduleModeProperty) == EnemyBossPatternModuleMode.NullModule)
            foldout.Add(new HelpBox(ResolveNullModuleDescription(slotKind), HelpBoxMessageType.Info));
        else
            BuildModuleFields(panel, foldout, candidateProperty, sourcePreset, slotKind);

        parent.Add(card);
    }

    /// <summary>
    /// Builds add actions for module and null candidates.
    /// </summary>
    /// <param name="panel">Owning boss preset panel.</param>
    /// <param name="candidatesProperty">Serialized candidate array.</param>
    /// <param name="sourcePreset">Source module catalog used by boss candidates.</param>
    /// <param name="slotKind">Boss slot controlled by the candidates.</param>
    /// <param name="parent">Parent receiving the action row.</param>
    private static void BuildCandidateActions(EnemyBossPatternPresetsPanel panel,
                                              SerializedProperty candidatesProperty,
                                              EnemyModulesAndPatternsPreset sourcePreset,
                                              EnemyBossPatternSlotKind slotKind,
                                              VisualElement parent)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.marginTop = 4f;

        Button addModuleButton = new Button(() =>
        {
            AddCandidate(panel, candidatesProperty, sourcePreset, slotKind, false);
        });
        addModuleButton.text = "Add Module Candidate";
        addModuleButton.tooltip = "Add one candidate that applies a module from the source preset catalog.";
        addModuleButton.SetEnabled(HasSourceModule(sourcePreset, slotKind));
        row.Add(addModuleButton);

        Button addNullButton = new Button(() =>
        {
            AddCandidate(panel, candidatesProperty, sourcePreset, slotKind, true);
        });
        addNullButton.text = "Add Null Candidate";
        addNullButton.tooltip = "Add one candidate that clears this slot until another extraction selects a module.";
        addNullButton.style.marginLeft = 4f;
        row.Add(addNullButton);
        parent.Add(row);
    }
    #endregion

    #region Eligibility
    /// <summary>
    /// Builds shared eligibility controls for one module candidate.
    /// </summary>
    /// <param name="panel">Owning boss preset panel.</param>
    /// <param name="parent">Parent receiving fields.</param>
    /// <param name="eligibilityProperty">Serialized eligibility root.</param>
    private static void BuildEligibilityFields(EnemyBossPatternPresetsPanel panel,
                                               VisualElement parent,
                                               SerializedProperty eligibilityProperty)
    {
        if (eligibilityProperty == null || parent == null)
            return;

        SerializedProperty enabledProperty = eligibilityProperty.FindPropertyRelative("enabled");
        SerializedProperty displayNameProperty = eligibilityProperty.FindPropertyRelative("displayName");
        SerializedProperty eligibilityTypeProperty = eligibilityProperty.FindPropertyRelative("eligibilityType");
        SerializedProperty minimumActiveSecondsProperty = eligibilityProperty.FindPropertyRelative("minimumActiveSeconds");
        SerializedProperty selectionWeightProperty = eligibilityProperty.FindPropertyRelative("selectionWeight");
        EnemyBossPatternInteractionType eligibilityType = ResolveEligibilityType(eligibilityTypeProperty);

        parent.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel,
                                                                                         enabledProperty,
                                                                                         "Enabled",
                                                                                         "Enables this module candidate during bake and runtime slot extraction."));
        EnemyBossPatternPresetsPanelSharedUtility.AddTrackedTextField(panel,
                                                                      parent,
                                                                      displayNameProperty,
                                                                      "Candidate Name",
                                                                      "Readable module candidate name shown by the Boss Pattern Assemble section.",
                                                                      false);
        parent.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel,
                                                                                         eligibilityTypeProperty,
                                                                                         "Eligibility Type",
                                                                                         "Criterion that decides when this module candidate can be extracted inside the active pattern."));
        AddEligibilityTypeFields(panel, parent, eligibilityProperty, eligibilityType);
        EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel,
                                                                      parent,
                                                                      minimumActiveSecondsProperty,
                                                                      "Minimum Active Seconds",
                                                                      0f,
                                                                      20f,
                                                                      "Minimum seconds this module candidate remains active before the slot can extract another candidate.");
        EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel,
                                                                      parent,
                                                                      selectionWeightProperty,
                                                                      "Selection Weight",
                                                                      0f,
                                                                      100f,
                                                                      "Relative weight used when this candidate is eligible during an internal slot extraction roll.");
    }

    /// <summary>
    /// Adds threshold fields relevant to the current eligibility type.
    /// </summary>
    /// <param name="panel">Owning boss preset panel.</param>
    /// <param name="parent">Parent receiving fields.</param>
    /// <param name="eligibilityProperty">Serialized eligibility root.</param>
    /// <param name="eligibilityType">Selected eligibility type.</param>
    private static void AddEligibilityTypeFields(EnemyBossPatternPresetsPanel panel,
                                                 VisualElement parent,
                                                 SerializedProperty eligibilityProperty,
                                                 EnemyBossPatternInteractionType eligibilityType)
    {
        switch (eligibilityType)
        {
            case EnemyBossPatternInteractionType.Always:
                parent.Add(new HelpBox("Always candidates are considered during every internal slot extraction roll.", HelpBoxMessageType.Info));
                break;

            case EnemyBossPatternInteractionType.ElapsedTime:
                EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, eligibilityProperty.FindPropertyRelative("minimumElapsedSeconds"), "Minimum Elapsed Seconds", 0f, DefaultMaximumConditionSeconds, "Minimum seconds since boss spawn required by this candidate.");
                EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, eligibilityProperty.FindPropertyRelative("maximumElapsedSeconds"), "Maximum Elapsed Seconds", 0f, DefaultMaximumConditionSeconds, "Maximum seconds since boss spawn allowed by this candidate. Zero disables the upper bound.");
                break;

            case EnemyBossPatternInteractionType.TravelledDistance:
                EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, eligibilityProperty.FindPropertyRelative("minimumTravelledDistance"), "Minimum Travelled Distance", 0f, DefaultMaximumTravelDistance, "Minimum boss movement distance since this slot's previous extraction.");
                EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, eligibilityProperty.FindPropertyRelative("maximumTravelledDistance"), "Maximum Travelled Distance", 0f, DefaultMaximumTravelDistance, "Maximum boss movement distance since this slot's previous extraction. Zero disables the upper bound.");
                break;

            case EnemyBossPatternInteractionType.PlayerDistance:
                EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, eligibilityProperty.FindPropertyRelative("minimumPlayerDistance"), "Minimum Player Distance", 0f, DefaultMaximumPlayerDistance, "Minimum planar player distance required by this candidate.");
                EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, eligibilityProperty.FindPropertyRelative("maximumPlayerDistance"), "Maximum Player Distance", 0f, DefaultMaximumPlayerDistance, "Maximum planar player distance allowed by this candidate. Zero disables the upper bound.");
                break;

            case EnemyBossPatternInteractionType.RecentlyDamaged:
                EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, eligibilityProperty.FindPropertyRelative("recentlyDamagedWindowSeconds"), "Recently Damaged Window", 0.05f, 10f, "Seconds after receiving damage for which this candidate remains eligible.");
                break;

            default:
                EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, eligibilityProperty.FindPropertyRelative("minimumMissingHealthPercent"), "Minimum Missing Health", 0f, 1f, "Minimum normalized missing health required by this candidate.");
                EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, parent, eligibilityProperty.FindPropertyRelative("maximumMissingHealthPercent"), "Maximum Missing Health", 0f, 1f, "Maximum normalized missing health allowed by this candidate. Zero disables the upper bound.");
                break;
        }
    }
    #endregion

    #region Module Fields
    /// <summary>
    /// Builds slot-specific module controls for a non-null module candidate.
    /// </summary>
    /// <param name="panel">Owning boss preset panel.</param>
    /// <param name="parent">Parent receiving controls.</param>
    /// <param name="candidateProperty">Serialized candidate root.</param>
    /// <param name="sourcePreset">Source module catalog used by boss candidates.</param>
    /// <param name="slotKind">Boss slot controlled by the candidate.</param>
    private static void BuildModuleFields(EnemyBossPatternPresetsPanel panel,
                                          VisualElement parent,
                                          SerializedProperty candidateProperty,
                                          EnemyModulesAndPatternsPreset sourcePreset,
                                          EnemyBossPatternSlotKind slotKind)
    {
        if (sourcePreset == null)
            return;

        switch (slotKind)
        {
            case EnemyBossPatternSlotKind.CoreMovement:
                EnemyBossPatternPresetsPanelModuleUtility.AddModuleBindingSelector(panel,
                                                                                   parent,
                                                                                   candidateProperty.FindPropertyRelative("binding"),
                                                                                   sourcePreset,
                                                                                   EnemyPatternModuleCatalogSection.CoreMovement,
                                                                                   "Core Movement Module",
                                                                                   "Select the Core Movement module candidate from the source preset.");
                EnemyBossPatternPresetsPanelPatternUtility.BuildEngagementFeedbackFields(panel,
                                                                                         parent,
                                                                                         candidateProperty,
                                                                                         sourcePreset,
                                                                                         EnemyPatternModuleCatalogSection.CoreMovement,
                                                                                         "Core Movement");
                break;

            case EnemyBossPatternSlotKind.ShortRangeInteraction:
                EnemyBossPatternPresetsPanelPatternUtility.BuildShortRangeSlot(panel,
                                                                               parent,
                                                                               candidateProperty.FindPropertyRelative("interaction"),
                                                                               sourcePreset,
                                                                               "Short-Range Module Settings",
                                                                               "Enable Short-Range Module",
                                                                               "Configures the Short-Range Interaction module applied by this candidate.");
                break;

            case EnemyBossPatternSlotKind.WeaponInteraction:
                EnemyBossPatternPresetsPanelPatternUtility.BuildWeaponSlot(panel,
                                                                           parent,
                                                                           candidateProperty.FindPropertyRelative("interaction"),
                                                                           sourcePreset,
                                                                           "Weapon Module Settings",
                                                                           "Enable Weapon Module",
                                                                           "Configures the Weapon Interaction module applied by this candidate.");
                break;
        }
    }

    /// <summary>
    /// Resolves the explanatory text shown when a candidate intentionally ignores its slot.
    /// </summary>
    /// <param name="slotKind">Boss slot controlled by the candidate.</param>
    /// <returns>User-facing description of the null module behavior for the slot.</returns>
    private static string ResolveNullModuleDescription(EnemyBossPatternSlotKind slotKind)
    {
        switch (slotKind)
        {
            case EnemyBossPatternSlotKind.CoreMovement:
                return "This candidate ignores Core Movement by holding the boss stationary while it is active.";

            case EnemyBossPatternSlotKind.ShortRangeInteraction:
                return "This candidate ignores the Short-Range Interaction slot while it is active.";

            default:
                return "This candidate ignores the Weapon Interaction slot while it is active.";
        }
    }
    #endregion

    #region Mutations
    /// <summary>
    /// Adds a module or null candidate to one internal extraction slot.
    /// </summary>
    /// <param name="panel">Owning boss preset panel.</param>
    /// <param name="candidatesProperty">Serialized candidate array.</param>
    /// <param name="sourcePreset">Source module catalog used by boss candidates.</param>
    /// <param name="slotKind">Boss slot controlled by the candidates.</param>
    /// <param name="isNullCandidate">True when the candidate should clear the slot.</param>
    private static void AddCandidate(EnemyBossPatternPresetsPanel panel,
                                     SerializedProperty candidatesProperty,
                                     EnemyModulesAndPatternsPreset sourcePreset,
                                     EnemyBossPatternSlotKind slotKind,
                                     bool isNullCandidate)
    {
        if (panel == null || candidatesProperty == null)
            return;

        EnemyBossPatternPresetsPanelSharedUtility.RecordSelectedPreset(panel, "Add Boss Module Candidate");
        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        presetSerializedObject.Update();
        int insertIndex = candidatesProperty.arraySize;
        candidatesProperty.InsertArrayElementAtIndex(insertIndex);
        SerializedProperty insertedCandidate = candidatesProperty.GetArrayElementAtIndex(insertIndex);

        if (insertedCandidate != null)
            ConfigureInsertedCandidate(insertedCandidate, sourcePreset, slotKind, insertIndex, isNullCandidate);

        presetSerializedObject.ApplyModifiedProperties();
        EnemyBossPatternPresetsPanelSharedUtility.MarkDirtyAndRebuild(panel);
    }

    /// <summary>
    /// Writes deterministic defaults into a newly inserted candidate.
    /// </summary>
    /// <param name="candidateProperty">Serialized candidate root.</param>
    /// <param name="sourcePreset">Source module catalog used by boss candidates.</param>
    /// <param name="slotKind">Boss slot controlled by the candidate.</param>
    /// <param name="candidateIndex">Candidate index used for display names.</param>
    /// <param name="isNullCandidate">True when the candidate should clear the slot.</param>
    private static void ConfigureInsertedCandidate(SerializedProperty candidateProperty,
                                                   EnemyModulesAndPatternsPreset sourcePreset,
                                                   EnemyBossPatternSlotKind slotKind,
                                                   int candidateIndex,
                                                   bool isNullCandidate)
    {
        ConfigureEligibility(candidateProperty.FindPropertyRelative("eligibility"),
                             BuildDefaultCandidateName(slotKind, candidateIndex, isNullCandidate));
        EnemyBossPatternPresetsPanelDefaultsUtility.ConfigureCandidateWarningDefaults(candidateProperty,
                                                                                       slotKind);
        EnemyBossPatternPresetsPanelModuleUtility.SetEnumIndex(candidateProperty.FindPropertyRelative("moduleMode"),
                                                               Convert.ToInt32(isNullCandidate
                                                                   ? EnemyBossPatternModuleMode.NullModule
                                                                   : EnemyBossPatternModuleMode.Module));

        if (isNullCandidate)
            return;

        if (!EnemyBossPatternPresetsPanelModuleUtility.TryResolveFirstModuleId(sourcePreset,
                                                                               ResolveCatalogSection(slotKind),
                                                                               out string moduleId))
        {
            return;
        }

        ConfigureCandidateModule(candidateProperty, slotKind, moduleId);
    }

    /// <summary>
    /// Writes deterministic defaults into a candidate eligibility block.
    /// </summary>
    /// <param name="eligibilityProperty">Serialized eligibility root.</param>
    /// <param name="displayName">Display name to assign.</param>
    private static void ConfigureEligibility(SerializedProperty eligibilityProperty, string displayName)
    {
        if (eligibilityProperty == null)
            return;

        EnemyBossPatternPresetsPanelModuleUtility.SetBoolean(eligibilityProperty.FindPropertyRelative("enabled"), true);
        EnemyBossPatternPresetsPanelModuleUtility.SetString(eligibilityProperty.FindPropertyRelative("displayName"), displayName);
        EnemyBossPatternPresetsPanelModuleUtility.SetEnumIndex(eligibilityProperty.FindPropertyRelative("eligibilityType"), Convert.ToInt32(EnemyBossPatternInteractionType.Always));
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(eligibilityProperty.FindPropertyRelative("minimumActiveSeconds"), 0.5f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(eligibilityProperty.FindPropertyRelative("selectionWeight"), 1f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(eligibilityProperty.FindPropertyRelative("minimumMissingHealthPercent"), 0f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(eligibilityProperty.FindPropertyRelative("maximumMissingHealthPercent"), 0f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(eligibilityProperty.FindPropertyRelative("minimumElapsedSeconds"), 0f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(eligibilityProperty.FindPropertyRelative("maximumElapsedSeconds"), 0f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(eligibilityProperty.FindPropertyRelative("minimumTravelledDistance"), 0f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(eligibilityProperty.FindPropertyRelative("maximumTravelledDistance"), 0f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(eligibilityProperty.FindPropertyRelative("minimumPlayerDistance"), 0f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(eligibilityProperty.FindPropertyRelative("maximumPlayerDistance"), 12f);
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(eligibilityProperty.FindPropertyRelative("recentlyDamagedWindowSeconds"), 1.25f);
    }

    /// <summary>
    /// Assigns the first resolved source module to the new candidate.
    /// </summary>
    /// <param name="candidateProperty">Serialized candidate root.</param>
    /// <param name="slotKind">Boss slot controlled by the candidate.</param>
    /// <param name="moduleId">Source module ID to assign.</param>
    private static void ConfigureCandidateModule(SerializedProperty candidateProperty,
                                                 EnemyBossPatternSlotKind slotKind,
                                                 string moduleId)
    {
        switch (slotKind)
        {
            case EnemyBossPatternSlotKind.CoreMovement:
                EnemyBossPatternPresetsPanelModuleUtility.ConfigureBinding(candidateProperty.FindPropertyRelative("binding"), moduleId);
                break;

            case EnemyBossPatternSlotKind.ShortRangeInteraction:
                ConfigureInteractionAssembly(candidateProperty.FindPropertyRelative("interaction"), moduleId);
                break;

            case EnemyBossPatternSlotKind.WeaponInteraction:
                ConfigureInteractionAssembly(candidateProperty.FindPropertyRelative("interaction"), moduleId);
                break;
        }
    }

    /// <summary>
    /// Enables an interaction assembly and assigns its nested module binding.
    /// </summary>
    /// <param name="interactionProperty">Serialized interaction assembly root.</param>
    /// <param name="moduleId">Source module ID to assign.</param>
    private static void ConfigureInteractionAssembly(SerializedProperty interactionProperty, string moduleId)
    {
        if (interactionProperty == null)
            return;

        EnemyBossPatternPresetsPanelModuleUtility.SetBoolean(interactionProperty.FindPropertyRelative("isEnabled"), true);
        EnemyBossPatternPresetsPanelModuleUtility.ConfigureBinding(interactionProperty.FindPropertyRelative("binding"), moduleId);
    }
    #endregion

    #region Formatting
    /// <summary>
    /// Builds a readable foldout title for one module candidate.
    /// </summary>
    /// <param name="candidateProperty">Serialized candidate root.</param>
    /// <param name="slotKind">Boss slot controlled by the candidate.</param>
    /// <param name="index">Candidate index inside the slot list.</param>
    /// <returns>Readable candidate title.</returns>
    private static string BuildCandidateTitle(SerializedProperty candidateProperty,
                                              EnemyBossPatternSlotKind slotKind,
                                              int index)
    {
        SerializedProperty eligibilityProperty = candidateProperty.FindPropertyRelative("eligibility");
        SerializedProperty displayNameProperty = eligibilityProperty != null
            ? eligibilityProperty.FindPropertyRelative("displayName")
            : null;
        string displayName = displayNameProperty != null ? displayNameProperty.stringValue : string.Empty;

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = "Module Candidate " + (index + 1);

        return "#" + (index + 1).ToString("D2") + " " + FormatSlotKind(slotKind) + " - " + displayName;
    }

    /// <summary>
    /// Builds the default display name for a newly inserted module candidate.
    /// </summary>
    /// <param name="slotKind">Boss slot controlled by the candidate.</param>
    /// <param name="candidateIndex">Candidate index inside the slot list.</param>
    /// <param name="isNullCandidate">True when the candidate clears the slot.</param>
    /// <returns>Default display name.</returns>
    private static string BuildDefaultCandidateName(EnemyBossPatternSlotKind slotKind, int candidateIndex, bool isNullCandidate)
    {
        string suffix = isNullCandidate ? "Null Candidate" : "Module Candidate";
        return FormatSlotKind(slotKind) + " " + suffix + " " + (candidateIndex + 1);
    }

    /// <summary>
    /// Converts a boss slot kind into UI text.
    /// </summary>
    /// <param name="slotKind">Slot kind to format.</param>
    /// <returns>Readable slot name.</returns>
    private static string FormatSlotKind(EnemyBossPatternSlotKind slotKind)
    {
        switch (slotKind)
        {
            case EnemyBossPatternSlotKind.ShortRangeInteraction:
                return "Short-Range";

            case EnemyBossPatternSlotKind.WeaponInteraction:
                return "Weapon";

            default:
                return "Core Movement";
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves whether a source module exists for one boss slot.
    /// </summary>
    /// <param name="sourcePreset">Source module catalog used by boss candidates.</param>
    /// <param name="slotKind">Boss slot to inspect.</param>
    /// <returns>True when at least one selectable module exists.</returns>
    private static bool HasSourceModule(EnemyModulesAndPatternsPreset sourcePreset, EnemyBossPatternSlotKind slotKind)
    {
        return EnemyBossPatternPresetsPanelModuleUtility.TryResolveFirstModuleId(sourcePreset,
                                                                                 ResolveCatalogSection(slotKind),
                                                                                 out string _);
    }

    /// <summary>
    /// Resolves the source catalog section used by one boss slot.
    /// </summary>
    /// <param name="slotKind">Boss slot to resolve.</param>
    /// <returns>Matching module catalog section.</returns>
    private static EnemyPatternModuleCatalogSection ResolveCatalogSection(EnemyBossPatternSlotKind slotKind)
    {
        switch (slotKind)
        {
            case EnemyBossPatternSlotKind.ShortRangeInteraction:
                return EnemyPatternModuleCatalogSection.ShortRangeInteraction;

            case EnemyBossPatternSlotKind.WeaponInteraction:
                return EnemyPatternModuleCatalogSection.WeaponInteraction;

            default:
                return EnemyPatternModuleCatalogSection.CoreMovement;
        }
    }

    /// <summary>
    /// Resolves candidate module mode from a serialized enum property.
    /// </summary>
    /// <param name="moduleModeProperty">Serialized module mode enum.</param>
    /// <returns>Typed module mode.</returns>
    private static EnemyBossPatternModuleMode ResolveModuleMode(SerializedProperty moduleModeProperty)
    {
        if (moduleModeProperty == null)
            return EnemyBossPatternModuleMode.Module;

        return (EnemyBossPatternModuleMode)moduleModeProperty.enumValueIndex;
    }

    /// <summary>
    /// Resolves candidate eligibility type from a serialized enum property.
    /// </summary>
    /// <param name="eligibilityTypeProperty">Serialized eligibility type enum.</param>
    /// <returns>Typed eligibility type.</returns>
    private static EnemyBossPatternInteractionType ResolveEligibilityType(SerializedProperty eligibilityTypeProperty)
    {
        if (eligibilityTypeProperty == null)
            return EnemyBossPatternInteractionType.MissingHealth;

        return (EnemyBossPatternInteractionType)eligibilityTypeProperty.enumValueIndex;
    }
    #endregion

    #endregion
}
