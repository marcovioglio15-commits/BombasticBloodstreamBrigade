using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the boss drop extraction section separated from movement and attack pattern controls.
/// </summary>
internal static class EnemyBossPatternPresetsPanelDropUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the boss drop extraction section.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized preset context.</param>
    public static void BuildDropExtractionSection(EnemyBossPatternPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = EnemyBossPatternPresetsPanelSharedUtility.CreateDetailsSectionContainer(panel, "Boss Drop Extraction");

        if (sectionContainer == null)
            return;

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        SerializedProperty dropExtractionProperty = presetSerializedObject.FindProperty("dropExtraction");
        SerializedProperty sourcePatternsProperty = presetSerializedObject.FindProperty("sourcePatternsPreset");
        EnemyModulesAndPatternsPreset sourcePreset = sourcePatternsProperty != null
            ? sourcePatternsProperty.objectReferenceValue as EnemyModulesAndPatternsPreset
            : null;

        if (dropExtractionProperty == null)
        {
            sectionContainer.Add(new HelpBox("Boss Drop Extraction serialized data is missing.", HelpBoxMessageType.Warning));
            return;
        }

        if (sourcePreset == null)
            sectionContainer.Add(new HelpBox("Assign a source Modules & Patterns preset before configuring boss drop candidates.", HelpBoxMessageType.Warning));

        BuildDropExtractionSettings(panel, dropExtractionProperty, sourcePreset, sectionContainer);
    }
    #endregion

    #region Extraction Settings
    /// <summary>
    /// Builds top-level boss drop extraction controls.
    /// </summary>
    /// <param name="panel">Owning boss preset panel.</param>
    /// <param name="dropExtractionProperty">Serialized drop extraction root.</param>
    /// <param name="sourcePreset">Source module catalog used by drop candidates.</param>
    /// <param name="parent">Parent receiving controls.</param>
    private static void BuildDropExtractionSettings(EnemyBossPatternPresetsPanel panel,
                                                    SerializedProperty dropExtractionProperty,
                                                    EnemyModulesAndPatternsPreset sourcePreset,
                                                    VisualElement parent)
    {
        SerializedProperty enabledProperty = dropExtractionProperty.FindPropertyRelative("enabled");
        SerializedProperty extractionModeProperty = dropExtractionProperty.FindPropertyRelative("extractionMode");
        SerializedProperty candidatesProperty = dropExtractionProperty.FindPropertyRelative("candidates");

        VisualElement card = EnemyBossPatternPresetsPanelSharedUtility.CreateCard();
        parent.Add(card);
        card.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel,
                                                                                       enabledProperty,
                                                                                       "Enabled",
                                                                                       "Enables boss-specific death drops built from common enemy Drop Items modules."));

        if (enabledProperty == null || !enabledProperty.boolValue)
        {
            card.Add(new HelpBox("Boss Drops are disabled for this preset.", HelpBoxMessageType.Info));
            return;
        }

        card.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel,
                                                                                       extractionModeProperty,
                                                                                       "Extraction Mode",
                                                                                       "Single Candidate rolls one enabled drop candidate by weight; Sum All Candidates applies every enabled candidate."));

        if (!HasDropModule(sourcePreset))
            card.Add(new HelpBox("The source preset has no Drop Items modules. Add reusable Drop Items modules in the source Modules & Patterns preset before authoring boss drops.", HelpBoxMessageType.Warning));

        BuildDropCandidateCards(panel,
                                candidatesProperty,
                                sourcePreset,
                                ResolveExtractionMode(extractionModeProperty),
                                card);
    }
    #endregion

    #region Candidates
    /// <summary>
    /// Builds the boss drop candidate list.
    /// </summary>
    /// <param name="panel">Owning boss preset panel.</param>
    /// <param name="candidatesProperty">Serialized candidates array.</param>
    /// <param name="sourcePreset">Source module catalog used by drop candidates.</param>
    /// <param name="extractionMode">Current boss drop extraction mode.</param>
    /// <param name="parent">Parent receiving the list.</param>
    private static void BuildDropCandidateCards(EnemyBossPatternPresetsPanel panel,
                                                SerializedProperty candidatesProperty,
                                                EnemyModulesAndPatternsPreset sourcePreset,
                                                EnemyBossDropExtractionMode extractionMode,
                                                VisualElement parent)
    {
        if (candidatesProperty == null || parent == null)
            return;

        Label header = new Label("Drop Candidates");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginTop = 8f;
        parent.Add(header);

        if (candidatesProperty.arraySize <= 0)
            parent.Add(new HelpBox("Add at least one drop candidate to emit boss death rewards.", HelpBoxMessageType.Info));

        for (int index = 0; index < candidatesProperty.arraySize; index++)
        {
            SerializedProperty candidateProperty = candidatesProperty.GetArrayElementAtIndex(index);

            if (candidateProperty == null)
                continue;

            BuildDropCandidateCard(panel,
                                   candidatesProperty,
                                   candidateProperty,
                                   sourcePreset,
                                   extractionMode,
                                   index,
                                   parent);
        }

        Button addButton = new Button(() =>
        {
            AddDropCandidate(panel, candidatesProperty, sourcePreset);
        });
        addButton.text = "Add Drop Candidate";
        addButton.tooltip = "Add one boss drop candidate built from the source Drop Items module catalog.";
        addButton.style.marginTop = 4f;
        addButton.SetEnabled(HasDropModule(sourcePreset));
        parent.Add(addButton);
    }

    /// <summary>
    /// Builds one boss drop candidate card.
    /// </summary>
    /// <param name="panel">Owning boss preset panel.</param>
    /// <param name="candidatesProperty">Serialized array containing this candidate.</param>
    /// <param name="candidateProperty">Serialized candidate root.</param>
    /// <param name="sourcePreset">Source module catalog used by drop candidates.</param>
    /// <param name="extractionMode">Current boss drop extraction mode.</param>
    /// <param name="index">Candidate index inside the array.</param>
    /// <param name="parent">Parent receiving the card.</param>
    private static void BuildDropCandidateCard(EnemyBossPatternPresetsPanel panel,
                                               SerializedProperty candidatesProperty,
                                               SerializedProperty candidateProperty,
                                               EnemyModulesAndPatternsPreset sourcePreset,
                                               EnemyBossDropExtractionMode extractionMode,
                                               int index,
                                               VisualElement parent)
    {
        VisualElement card = EnemyBossPatternPresetsPanelSharedUtility.CreateCard();
        Foldout foldout = ManagementToolFoldoutStateUtility.CreatePropertyFoldout(candidateProperty,
                                                                                  BuildDropCandidateTitle(candidateProperty, index),
                                                                                  "BossDropCandidate",
                                                                                  index == 0);
        card.Add(foldout);
        foldout.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateArrayActionsRow(panel, candidatesProperty, index, "Boss Drop Candidate"));

        SerializedProperty enabledProperty = candidateProperty.FindPropertyRelative("enabled");
        SerializedProperty displayNameProperty = candidateProperty.FindPropertyRelative("displayName");
        SerializedProperty selectionWeightProperty = candidateProperty.FindPropertyRelative("selectionWeight");
        SerializedProperty dropItemsProperty = candidateProperty.FindPropertyRelative("dropItems");

        foldout.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel,
                                                                                          enabledProperty,
                                                                                          "Enabled",
                                                                                          "Enables this boss drop candidate during bake and death-time extraction."));
        EnemyBossPatternPresetsPanelSharedUtility.AddTrackedTextField(panel,
                                                                      foldout,
                                                                      displayNameProperty,
                                                                      "Candidate Name",
                                                                      "Readable drop candidate name shown by the Boss Drops section.",
                                                                      false);

        if (extractionMode == EnemyBossDropExtractionMode.SingleCandidate)
        {
            EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel,
                                                                          foldout,
                                                                          selectionWeightProperty,
                                                                          "Selection Weight",
                                                                          0f,
                                                                          100f,
                                                                          "Relative weight used when Boss Drop Extraction rolls one candidate.");
        }

        BuildDropItemsAssembly(panel, dropItemsProperty, sourcePreset, foldout);
        AddDropCandidateWarning(candidateProperty, foldout);
        parent.Add(card);
    }
    #endregion

    #region Drop Items
    /// <summary>
    /// Builds the Drop Items assembly controls for one boss drop candidate.
    /// </summary>
    /// <param name="panel">Owning boss preset panel.</param>
    /// <param name="dropItemsProperty">Serialized Drop Items assembly root.</param>
    /// <param name="sourcePreset">Source module catalog used by drop candidates.</param>
    /// <param name="parent">Parent receiving controls.</param>
    private static void BuildDropItemsAssembly(EnemyBossPatternPresetsPanel panel,
                                               SerializedProperty dropItemsProperty,
                                               EnemyModulesAndPatternsPreset sourcePreset,
                                               VisualElement parent)
    {
        if (dropItemsProperty == null || parent == null)
            return;

        Foldout foldout = ManagementToolFoldoutStateUtility.CreatePropertyFoldout(dropItemsProperty,
                                                                                  "Drop Items Modules",
                                                                                  "BossDropItemsModules",
                                                                                  true);
        foldout.tooltip = "Drop Items modules copied from the source preset catalog for this boss drop candidate.";
        foldout.style.marginTop = 4f;
        parent.Add(foldout);

        SerializedProperty enabledProperty = dropItemsProperty.FindPropertyRelative("isEnabled");
        SerializedProperty modulesProperty = dropItemsProperty.FindPropertyRelative("modules");
        foldout.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel,
                                                                                          enabledProperty,
                                                                                          "Enable Drop Items",
                                                                                          "Enables the Drop Items module list for this boss drop candidate."));

        if (enabledProperty == null || !enabledProperty.boolValue)
            return;

        BuildDropModuleCards(panel, modulesProperty, sourcePreset, foldout);
    }

    /// <summary>
    /// Builds the Drop Items module binding list for one boss drop candidate.
    /// </summary>
    /// <param name="panel">Owning boss preset panel.</param>
    /// <param name="modulesProperty">Serialized module binding array.</param>
    /// <param name="sourcePreset">Source module catalog used by drop candidates.</param>
    /// <param name="parent">Parent receiving the module list.</param>
    private static void BuildDropModuleCards(EnemyBossPatternPresetsPanel panel,
                                             SerializedProperty modulesProperty,
                                             EnemyModulesAndPatternsPreset sourcePreset,
                                             VisualElement parent)
    {
        if (modulesProperty == null || parent == null)
            return;

        if (modulesProperty.arraySize <= 0)
            parent.Add(new HelpBox("Add at least one Drop Items module binding to make this candidate emit rewards.", HelpBoxMessageType.Warning));

        for (int index = 0; index < modulesProperty.arraySize; index++)
        {
            SerializedProperty moduleProperty = modulesProperty.GetArrayElementAtIndex(index);

            if (moduleProperty == null)
                continue;

            BuildDropModuleCard(panel, modulesProperty, moduleProperty, sourcePreset, index, parent);
        }

        Button addButton = new Button(() =>
        {
            AddDropModule(panel, modulesProperty, sourcePreset);
        });
        addButton.text = "Add Drop Module";
        addButton.tooltip = "Add one Drop Items module binding from the source preset catalog.";
        addButton.style.marginTop = 4f;
        addButton.SetEnabled(HasDropModule(sourcePreset));
        parent.Add(addButton);
    }

    /// <summary>
    /// Builds one Drop Items module binding card.
    /// </summary>
    /// <param name="panel">Owning boss preset panel.</param>
    /// <param name="modulesProperty">Serialized array containing this binding.</param>
    /// <param name="moduleProperty">Serialized module binding root.</param>
    /// <param name="sourcePreset">Source module catalog used by drop candidates.</param>
    /// <param name="index">Module binding index.</param>
    /// <param name="parent">Parent receiving the card.</param>
    private static void BuildDropModuleCard(EnemyBossPatternPresetsPanel panel,
                                            SerializedProperty modulesProperty,
                                            SerializedProperty moduleProperty,
                                            EnemyModulesAndPatternsPreset sourcePreset,
                                            int index,
                                            VisualElement parent)
    {
        VisualElement card = EnemyBossPatternPresetsPanelSharedUtility.CreateCard();
        Foldout foldout = ManagementToolFoldoutStateUtility.CreatePropertyFoldout(moduleProperty,
                                                                                  "Drop Module #" + (index + 1).ToString("D2"),
                                                                                  "BossDropModule",
                                                                                  true);
        card.Add(foldout);
        foldout.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateArrayActionsRow(panel, modulesProperty, index, "Boss Drop Module"));
        EnemyBossPatternPresetsPanelModuleUtility.AddModuleBindingSelector(panel,
                                                                           foldout,
                                                                           moduleProperty,
                                                                           sourcePreset,
                                                                           EnemyPatternModuleCatalogSection.DropItems,
                                                                           "Drop Items Module",
                                                                           "Select the Drop Items module from the source preset.");
        parent.Add(card);
    }
    #endregion

    #region Warnings
    /// <summary>
    /// Adds warnings for a candidate that cannot currently emit any drop module.
    /// </summary>
    /// <param name="candidateProperty">Serialized drop candidate root.</param>
    /// <param name="parent">Parent receiving warnings.</param>
    private static void AddDropCandidateWarning(SerializedProperty candidateProperty, VisualElement parent)
    {
        if (candidateProperty == null || parent == null)
            return;

        SerializedProperty enabledProperty = candidateProperty.FindPropertyRelative("enabled");

        if (enabledProperty != null && !enabledProperty.boolValue)
            return;

        SerializedProperty dropItemsProperty = candidateProperty.FindPropertyRelative("dropItems");
        SerializedProperty dropItemsEnabledProperty = dropItemsProperty != null
            ? dropItemsProperty.FindPropertyRelative("isEnabled")
            : null;
        SerializedProperty modulesProperty = dropItemsProperty != null
            ? dropItemsProperty.FindPropertyRelative("modules")
            : null;

        if (dropItemsEnabledProperty == null || !dropItemsEnabledProperty.boolValue)
        {
            parent.Add(new HelpBox("This enabled drop candidate has Drop Items disabled and will not emit boss rewards.", HelpBoxMessageType.Warning));
            return;
        }

        if (modulesProperty == null || modulesProperty.arraySize <= 0)
            parent.Add(new HelpBox("This enabled drop candidate has no Drop Items modules and will not emit boss rewards.", HelpBoxMessageType.Warning));
    }
    #endregion

    #region Mutations
    /// <summary>
    /// Adds one boss drop candidate initialized from the first source Drop Items module.
    /// </summary>
    /// <param name="panel">Owning boss preset panel.</param>
    /// <param name="candidatesProperty">Serialized drop candidate array.</param>
    /// <param name="sourcePreset">Source module catalog used by drop candidates.</param>
    private static void AddDropCandidate(EnemyBossPatternPresetsPanel panel,
                                         SerializedProperty candidatesProperty,
                                         EnemyModulesAndPatternsPreset sourcePreset)
    {
        if (panel == null || candidatesProperty == null)
            return;

        EnemyBossPatternPresetsPanelSharedUtility.RecordSelectedPreset(panel, "Add Boss Drop Candidate");
        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        presetSerializedObject.Update();
        int insertIndex = candidatesProperty.arraySize;
        candidatesProperty.InsertArrayElementAtIndex(insertIndex);
        SerializedProperty candidateProperty = candidatesProperty.GetArrayElementAtIndex(insertIndex);

        if (candidateProperty != null)
            ConfigureDropCandidate(candidateProperty, sourcePreset, insertIndex);

        presetSerializedObject.ApplyModifiedProperties();
        EnemyBossPatternPresetsPanelSharedUtility.MarkDirtyAndRebuild(panel);
    }

    /// <summary>
    /// Adds one Drop Items module binding to a candidate.
    /// </summary>
    /// <param name="panel">Owning boss preset panel.</param>
    /// <param name="modulesProperty">Serialized module binding array.</param>
    /// <param name="sourcePreset">Source module catalog used by drop candidates.</param>
    private static void AddDropModule(EnemyBossPatternPresetsPanel panel,
                                      SerializedProperty modulesProperty,
                                      EnemyModulesAndPatternsPreset sourcePreset)
    {
        if (panel == null || modulesProperty == null)
            return;

        if (!EnemyBossPatternPresetsPanelModuleUtility.TryResolveFirstModuleId(sourcePreset,
                                                                               EnemyPatternModuleCatalogSection.DropItems,
                                                                               out string moduleId))
        {
            return;
        }

        EnemyBossPatternPresetsPanelSharedUtility.RecordSelectedPreset(panel, "Add Boss Drop Module");
        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        presetSerializedObject.Update();
        int insertIndex = modulesProperty.arraySize;
        modulesProperty.InsertArrayElementAtIndex(insertIndex);
        SerializedProperty moduleProperty = modulesProperty.GetArrayElementAtIndex(insertIndex);

        if (moduleProperty != null)
            EnemyBossPatternPresetsPanelModuleUtility.ConfigureBinding(moduleProperty, moduleId);

        presetSerializedObject.ApplyModifiedProperties();
        EnemyBossPatternPresetsPanelSharedUtility.MarkDirtyAndRebuild(panel);
    }

    /// <summary>
    /// Writes deterministic defaults into a newly inserted drop candidate.
    /// </summary>
    /// <param name="candidateProperty">Serialized candidate root.</param>
    /// <param name="sourcePreset">Source module catalog used by drop candidates.</param>
    /// <param name="candidateIndex">Candidate index used for display naming.</param>
    private static void ConfigureDropCandidate(SerializedProperty candidateProperty,
                                               EnemyModulesAndPatternsPreset sourcePreset,
                                               int candidateIndex)
    {
        EnemyBossPatternPresetsPanelModuleUtility.SetBoolean(candidateProperty.FindPropertyRelative("enabled"), true);
        EnemyBossPatternPresetsPanelModuleUtility.SetString(candidateProperty.FindPropertyRelative("displayName"), "Drop Candidate " + (candidateIndex + 1));
        EnemyBossPatternPresetsPanelModuleUtility.SetFloat(candidateProperty.FindPropertyRelative("selectionWeight"), 1f);

        SerializedProperty dropItemsProperty = candidateProperty.FindPropertyRelative("dropItems");

        if (dropItemsProperty == null)
            return;

        EnemyBossPatternPresetsPanelModuleUtility.SetBoolean(dropItemsProperty.FindPropertyRelative("isEnabled"), true);

        if (!EnemyBossPatternPresetsPanelModuleUtility.TryResolveFirstModuleId(sourcePreset,
                                                                               EnemyPatternModuleCatalogSection.DropItems,
                                                                               out string moduleId))
        {
            return;
        }

        SerializedProperty modulesProperty = dropItemsProperty.FindPropertyRelative("modules");

        if (modulesProperty == null)
            return;

        modulesProperty.arraySize = 0;
        modulesProperty.InsertArrayElementAtIndex(0);
        SerializedProperty moduleProperty = modulesProperty.GetArrayElementAtIndex(0);

        if (moduleProperty != null)
            EnemyBossPatternPresetsPanelModuleUtility.ConfigureBinding(moduleProperty, moduleId);
    }
    #endregion

    #region Formatting
    /// <summary>
    /// Builds one readable drop candidate card title.
    /// </summary>
    /// <param name="candidateProperty">Serialized candidate root.</param>
    /// <param name="index">Candidate index inside the list.</param>
    /// <returns>Readable candidate card title.</returns>
    private static string BuildDropCandidateTitle(SerializedProperty candidateProperty, int index)
    {
        SerializedProperty displayNameProperty = candidateProperty.FindPropertyRelative("displayName");
        string displayName = displayNameProperty != null ? displayNameProperty.stringValue : string.Empty;

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = "Drop Candidate " + (index + 1);

        return "#" + (index + 1).ToString("D2") + " " + displayName;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves whether the source preset exposes at least one Drop Items module.
    /// </summary>
    /// <param name="sourcePreset">Source module catalog used by drop candidates.</param>
    /// <returns>True when a Drop Items module is selectable.</returns>
    private static bool HasDropModule(EnemyModulesAndPatternsPreset sourcePreset)
    {
        return EnemyBossPatternPresetsPanelModuleUtility.TryResolveFirstModuleId(sourcePreset,
                                                                                 EnemyPatternModuleCatalogSection.DropItems,
                                                                                 out string _);
    }

    /// <summary>
    /// Resolves the current drop extraction mode from a serialized enum.
    /// </summary>
    /// <param name="extractionModeProperty">Serialized extraction mode enum.</param>
    /// <returns>Typed extraction mode.</returns>
    private static EnemyBossDropExtractionMode ResolveExtractionMode(SerializedProperty extractionModeProperty)
    {
        if (extractionModeProperty == null)
            return EnemyBossDropExtractionMode.SingleCandidate;

        return (EnemyBossDropExtractionMode)extractionModeProperty.enumValueIndex;
    }
    #endregion

    #endregion
}
