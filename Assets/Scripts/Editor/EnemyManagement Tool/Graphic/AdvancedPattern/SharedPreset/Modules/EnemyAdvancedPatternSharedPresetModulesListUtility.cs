using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds the card-based shared module catalog editors used by the enemy Modules and Patterns preset flow.
/// </summary>
internal static class EnemyAdvancedPatternSharedPresetModulesListUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds every shared module catalog subsection with filter and card controls.
    /// </summary>
    /// <param name="panel">Owning panel that stores transient filter state and rebuild callbacks.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="sharedPreset">Shared preset asset being edited.</param>
    /// <param name="parent">Parent foldout that receives the catalog subsections.</param>
    public static void BuildModuleCatalogSections(EnemyAdvancedPatternPresetsPanel panel,
                                                  SerializedObject sharedPresetSerializedObject,
                                                  EnemyModulesAndPatternsPreset sharedPreset,
                                                  VisualElement parent)
    {
        if (panel == null || sharedPresetSerializedObject == null || sharedPreset == null || parent == null)
            return;

        BuildModuleCatalogSection(panel,
                                  sharedPresetSerializedObject,
                                  sharedPreset,
                                  parent,
                                  EnemyPatternModuleCatalogSection.CoreMovement);
        BuildModuleCatalogSection(panel,
                                  sharedPresetSerializedObject,
                                  sharedPreset,
                                  parent,
                                  EnemyPatternModuleCatalogSection.ShortRangeInteraction);
        BuildModuleCatalogSection(panel,
                                  sharedPresetSerializedObject,
                                  sharedPreset,
                                  parent,
                                  EnemyPatternModuleCatalogSection.WeaponInteraction);
        BuildModuleCatalogSection(panel,
                                  sharedPresetSerializedObject,
                                  sharedPreset,
                                  parent,
                                  EnemyPatternModuleCatalogSection.DropItems);
    }

    /// <summary>
    /// Returns whether one module matches the current catalog filters.
    /// </summary>
    /// <param name="panel">Owning panel that stores filter text.</param>
    /// <param name="section">Catalog section that owns the filters.</param>
    /// <param name="moduleId">Candidate module ID.</param>
    /// <param name="displayName">Candidate module display name.</param>
    /// <returns>True when the module should remain visible.</returns>
    internal static bool IsMatchingModuleFilters(EnemyAdvancedPatternPresetsPanel panel,
                                                 EnemyPatternModuleCatalogSection section,
                                                 string moduleId,
                                                 string displayName)
    {
        if (panel == null)
            return true;

        string moduleIdFilterText = panel.SharedPresetViewState.GetModuleIdFilterText(section);

        if (!string.IsNullOrWhiteSpace(moduleIdFilterText))
        {
            string resolvedModuleId = string.IsNullOrWhiteSpace(moduleId) ? string.Empty : moduleId;

            if (resolvedModuleId.IndexOf(moduleIdFilterText, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        string displayNameFilterText = panel.SharedPresetViewState.GetModuleDisplayNameFilterText(section);

        if (!string.IsNullOrWhiteSpace(displayNameFilterText))
        {
            string resolvedDisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName;

            if (resolvedDisplayName.IndexOf(displayNameFilterText, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds one shared module catalog subsection.
    /// </summary>
    /// <param name="panel">Owning panel that stores transient filter state and rebuild callbacks.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="sharedPreset">Shared preset asset being edited.</param>
    /// <param name="parent">Parent foldout that receives the subsection.</param>
    /// <param name="section">Catalog section being drawn.</param>
    private static void BuildModuleCatalogSection(EnemyAdvancedPatternPresetsPanel panel,
                                                  SerializedObject sharedPresetSerializedObject,
                                                  EnemyModulesAndPatternsPreset sharedPreset,
                                                  VisualElement parent,
                                                  EnemyPatternModuleCatalogSection section)
    {
        string definitionsPropertyName = EnemyAdvancedPatternSharedPresetModuleCatalogUtility.ResolveDefinitionsPropertyName(section);
        SerializedProperty definitionsProperty = sharedPresetSerializedObject.FindProperty(definitionsPropertyName);

        if (definitionsProperty == null)
            return;

        string sectionTitle = EnemyAdvancedPatternSharedPresetModuleCatalogUtility.ResolveSectionTitle(section);
        string sectionStateKey = ManagementToolFoldoutStateUtility.BuildSerializedObjectStateKey(sharedPresetSerializedObject) +
                                 "|SharedPresetModulesCatalog|" +
                                 definitionsPropertyName;
        Foldout sectionFoldout = ManagementToolFoldoutStateUtility.CreateFoldout(sectionTitle,
                                                                                 sectionStateKey,
                                                                                 true);
        sectionFoldout.tooltip = EnemyAdvancedPatternSharedPresetModuleCatalogUtility.ResolveSectionTooltip(section);
        sectionFoldout.style.marginTop = 4f;
        parent.Add(sectionFoldout);
        RegisterSectionFoldoutColorContextMenu(sectionFoldout,
                                               "NashCore.EnemyManagement.AdvancedPattern.SharedPreset.Modules." +
                                               sectionTitle.Replace(" ", string.Empty));

        sectionFoldout.Add(EnemyAdvancedPatternSharedPresetCardUtility.CreateDescriptionLabel(EnemyAdvancedPatternSharedPresetModuleCatalogUtility.ResolveSectionDescription(section)));

        VisualElement filtersRow = EnemyAdvancedPatternSharedPresetCardUtility.CreateHorizontalRow(true);
        TextField moduleIdFilterField = EnemyAdvancedPatternSharedPresetCardUtility.CreateDelayedFilterField("Filter Module ID",
                                                                                                              "Show only modules whose Module ID contains this text.",
                                                                                                              panel.SharedPresetViewState.GetModuleIdFilterText(section),
                                                                                                              6f);
        TextField displayNameFilterField = EnemyAdvancedPatternSharedPresetCardUtility.CreateDelayedFilterField("Filter Display Name",
                                                                                                                 "Show only modules whose Display Name contains this text.",
                                                                                                                 panel.SharedPresetViewState.GetModuleDisplayNameFilterText(section),
                                                                                                                 0f);
        filtersRow.Add(moduleIdFilterField);
        filtersRow.Add(displayNameFilterField);
        sectionFoldout.Add(filtersRow);

        VisualElement actionsRow = EnemyAdvancedPatternSharedPresetCardUtility.CreateHorizontalRow(false);
        Label countLabel = EnemyAdvancedPatternSharedPresetCardUtility.CreateCountLabel();
        ScrollView cardsContainer = EnemyAdvancedPatternSharedPresetCardUtility.CreateCardsScrollView();
        ManagementToolScrollStateUtility.BindScrollView(cardsContainer, sectionStateKey + "|Cards");

        Button addButton = EnemyAdvancedPatternSharedPresetCardUtility.CreateActionButton("Add Module",
                                                                                          "Create a new shared module definition inside this catalog section.",
                                                                                          () =>
                                                                                          {
                                                                                              EnemyAdvancedPatternSharedPresetModulesMutationUtility.AddModuleDefinition(panel,
                                                                                                                                                                  sharedPresetSerializedObject,
                                                                                                                                                                  sharedPreset,
                                                                                                                                                                  section);
                                                                                          },
                                                                                          0f);
        actionsRow.Add(addButton);

        Button expandAllButton = EnemyAdvancedPatternSharedPresetCardUtility.CreateActionButton("Expand All",
                                                                                                "Expand every visible module card in this catalog section.",
                                                                                                () =>
                                                                                                {
                                                                                                    EnemyAdvancedPatternSharedPresetModulesMutationUtility.SetAllModuleFoldoutStates(panel,
                                                                                                                                                      sharedPresetSerializedObject,
                                                                                                                                                      section,
                                                                                                                                                      true);
                                                                                                    RebuildModuleCards(panel,
                                                                                                                       sharedPresetSerializedObject,
                                                                                                                       sharedPreset,
                                                                                                                       section,
                                                                                                                       cardsContainer,
                                                                                                                       countLabel);
                                                                                                },
                                                                                                4f);
        actionsRow.Add(expandAllButton);

        Button collapseAllButton = EnemyAdvancedPatternSharedPresetCardUtility.CreateActionButton("Collapse All",
                                                                                                  "Collapse every visible module card in this catalog section.",
                                                                                                  () =>
                                                                                                  {
                                                                                                      EnemyAdvancedPatternSharedPresetModulesMutationUtility.SetAllModuleFoldoutStates(panel,
                                                                                                                                                        sharedPresetSerializedObject,
                                                                                                                                                        section,
                                                                                                                                                        false);
                                                                                                      RebuildModuleCards(panel,
                                                                                                                         sharedPresetSerializedObject,
                                                                                                                         sharedPreset,
                                                                                                                         section,
                                                                                                                         cardsContainer,
                                                                                                                         countLabel);
                                                                                                  },
                                                                                                  4f);
        actionsRow.Add(collapseAllButton);

        Button clearFiltersButton = EnemyAdvancedPatternSharedPresetCardUtility.CreateActionButton("Clear Filters",
                                                                                                   "Clear both module filters for this catalog section.",
                                                                                                   () =>
                                                                                                   {
                                                                                                       panel.SharedPresetViewState.ClearModuleFilters(section);
                                                                                                       moduleIdFilterField.SetValueWithoutNotify(string.Empty);
                                                                                                       displayNameFilterField.SetValueWithoutNotify(string.Empty);
                                                                                                       RebuildModuleCards(panel,
                                                                                                                          sharedPresetSerializedObject,
                                                                                                                          sharedPreset,
                                                                                                                          section,
                                                                                                                          cardsContainer,
                                                                                                                          countLabel);
                                                                                                   },
                                                                                                   4f);
        actionsRow.Add(clearFiltersButton);

        sectionFoldout.Add(actionsRow);
        sectionFoldout.Add(countLabel);
        sectionFoldout.Add(cardsContainer);

        moduleIdFilterField.RegisterValueChangedCallback(evt =>
        {
            panel.SharedPresetViewState.SetModuleIdFilterText(section, evt.newValue);
            RebuildModuleCards(panel,
                               sharedPresetSerializedObject,
                               sharedPreset,
                               section,
                               cardsContainer,
                               countLabel);
        });
        displayNameFilterField.RegisterValueChangedCallback(evt =>
        {
            panel.SharedPresetViewState.SetModuleDisplayNameFilterText(section, evt.newValue);
            RebuildModuleCards(panel,
                               sharedPresetSerializedObject,
                               sharedPreset,
                               section,
                               cardsContainer,
                               countLabel);
        });

        RebuildModuleCards(panel,
                           sharedPresetSerializedObject,
                           sharedPreset,
                           section,
                           cardsContainer,
                           countLabel);
    }

    /// <summary>
    /// Rebuilds the visible module cards for one catalog section.
    /// </summary>
    /// <param name="panel">Owning panel that stores filter state.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="sharedPreset">Shared preset asset being edited.</param>
    /// <param name="section">Catalog section being rebuilt.</param>
    /// <param name="cardsContainer">Container that receives the generated cards.</param>
    /// <param name="countLabel">Count label updated with visible and total entries.</param>
    private static void RebuildModuleCards(EnemyAdvancedPatternPresetsPanel panel,
                                           SerializedObject sharedPresetSerializedObject,
                                           EnemyModulesAndPatternsPreset sharedPreset,
                                           EnemyPatternModuleCatalogSection section,
                                           VisualElement cardsContainer,
                                           Label countLabel)
    {
        if (panel == null || sharedPresetSerializedObject == null || sharedPreset == null || cardsContainer == null || countLabel == null)
            return;

        ScrollView cardsScrollView = cardsContainer as ScrollView;
        ManagementToolFoldoutStateUtility.CaptureFoldoutStates(cardsContainer);
        ManagementToolScrollStateUtility.CaptureScrollOffset(cardsScrollView);
        cardsContainer.Clear();
        SerializedProperty definitionsProperty = sharedPresetSerializedObject.FindProperty(EnemyAdvancedPatternSharedPresetModuleCatalogUtility.ResolveDefinitionsPropertyName(section));

        if (definitionsProperty == null)
        {
            countLabel.text = "Visible Modules: 0 / 0";
            cardsContainer.Add(EnemyAdvancedPatternSharedPresetCardUtility.CreateStatusLabel("Module definitions are unavailable for this catalog section."));
            ManagementToolScrollStateUtility.RestoreScrollOffset(cardsScrollView);
            return;
        }

        int totalModules = definitionsProperty.arraySize;
        int visibleModules = 0;

        for (int moduleIndex = 0; moduleIndex < definitionsProperty.arraySize; moduleIndex++)
        {
            SerializedProperty moduleProperty = definitionsProperty.GetArrayElementAtIndex(moduleIndex);

            if (moduleProperty == null)
                continue;

            string moduleId = EnemyAdvancedPatternSharedPresetModuleDefinitionUtility.ResolveModuleDefinitionId(moduleProperty);
            string displayName = EnemyAdvancedPatternSharedPresetModuleDefinitionUtility.ResolveModuleDefinitionDisplayName(moduleProperty);

            if (!IsMatchingModuleFilters(panel, section, moduleId, displayName))
                continue;

            EnemyPatternModuleKind moduleKind = EnemyAdvancedPatternSharedPresetModuleDefinitionUtility.ResolveModuleDefinitionKind(moduleProperty);
            cardsContainer.Add(CreateModuleCard(panel,
                                                sharedPresetSerializedObject,
                                                sharedPreset,
                                                definitionsProperty,
                                                moduleProperty,
                                                section,
                                                moduleIndex,
                                                moduleId,
                                                displayName,
                                                moduleKind));
            visibleModules += 1;
        }

        countLabel.text = string.Format("Visible Modules: {0} / {1}", visibleModules, totalModules);

        if (visibleModules > 0)
        {
            ManagementToolScrollStateUtility.RestoreScrollOffset(cardsScrollView);
            return;
        }

        cardsContainer.Add(EnemyAdvancedPatternSharedPresetCardUtility.CreateStatusLabel("No modules match current filters."));
        ManagementToolScrollStateUtility.RestoreScrollOffset(cardsScrollView);
    }

    /// <summary>
    /// Creates one module card with foldout, actions and bound property field.
    /// </summary>
    /// <param name="panel">Owning panel that provides callbacks.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="sharedPreset">Shared preset asset being edited.</param>
    /// <param name="definitionsProperty">Serialized definitions array that owns the card.</param>
    /// <param name="moduleProperty">Serialized module property displayed by the card.</param>
    /// <param name="section">Catalog section being edited.</param>
    /// <param name="moduleIndex">Current module index.</param>
    /// <param name="moduleId">Resolved module ID.</param>
    /// <param name="displayName">Resolved display name.</param>
    /// <param name="moduleKind">Resolved module kind.</param>
    /// <returns>Created card element.</returns>
    private static VisualElement CreateModuleCard(EnemyAdvancedPatternPresetsPanel panel,
                                                  SerializedObject sharedPresetSerializedObject,
                                                  EnemyModulesAndPatternsPreset sharedPreset,
                                                  SerializedProperty definitionsProperty,
                                                  SerializedProperty moduleProperty,
                                                  EnemyPatternModuleCatalogSection section,
                                                  int moduleIndex,
                                                  string moduleId,
                                                  string displayName,
                                                  EnemyPatternModuleKind moduleKind)
    {
        VisualElement card = EnemyAdvancedPatternSharedPresetCardUtility.CreateCardContainer();
        string foldoutStateKey = EnemyAdvancedPatternSharedPresetModuleDefinitionUtility.BuildModuleCardStateKey(moduleProperty);
        Foldout foldout = ManagementToolFoldoutStateUtility.CreateFoldout(EnemyAdvancedPatternSharedPresetModuleDefinitionUtility.BuildModuleCardTitle(moduleIndex,
                                                                                                                                                      moduleId,
                                                                                                                                                      displayName,
                                                                                                                                                      moduleKind),
                                                                          foldoutStateKey,
                                                                          ManagementToolFoldoutStateUtility.ResolveFoldoutState(foldoutStateKey, false));
        card.Add(foldout);

        VisualElement actionsRow = EnemyAdvancedPatternSharedPresetCardUtility.CreateCardActionsRow();
        foldout.Add(actionsRow);

        Button duplicateButton = EnemyAdvancedPatternSharedPresetCardUtility.CreateActionButton("Duplicate",
                                                                                                "Duplicate this shared module definition.",
                                                                                                () =>
                                                                                                {
                                                                                                    EnemyAdvancedPatternSharedPresetModulesMutationUtility.DuplicateModuleDefinition(panel,
                                                                                                                                                            sharedPresetSerializedObject,
                                                                                                                                                            sharedPreset,
                                                                                                                                                            section,
                                                                                                                                                            moduleIndex);
                                                                                                },
                                                                                                0f);
        actionsRow.Add(duplicateButton);

        Button moveUpButton = EnemyAdvancedPatternSharedPresetCardUtility.CreateActionButton("Up",
                                                                                             "Move this module one position up inside the current catalog section.",
                                                                                             () =>
                                                                                             {
                                                                                                 EnemyAdvancedPatternSharedPresetModulesMutationUtility.MoveModuleDefinition(panel,
                                                                                                                                                     sharedPresetSerializedObject,
                                                                                                                                                     sharedPreset,
                                                                                                                                                     section,
                                                                                                                                                     moduleIndex,
                                                                                                                                                     moduleIndex - 1);
                                                                                             },
                                                                                             4f);
        moveUpButton.SetEnabled(moduleIndex > 0);
        actionsRow.Add(moveUpButton);

        Button moveDownButton = EnemyAdvancedPatternSharedPresetCardUtility.CreateActionButton("Down",
                                                                                               "Move this module one position down inside the current catalog section.",
                                                                                               () =>
                                                                                               {
                                                                                                   EnemyAdvancedPatternSharedPresetModulesMutationUtility.MoveModuleDefinition(panel,
                                                                                                                                                       sharedPresetSerializedObject,
                                                                                                                                                       sharedPreset,
                                                                                                                                                       section,
                                                                                                                                                       moduleIndex,
                                                                                                                                                       moduleIndex + 1);
                                                                                               },
                                                                                               4f);
        moveDownButton.SetEnabled(moduleIndex < definitionsProperty.arraySize - 1);
        actionsRow.Add(moveDownButton);

        Button deleteButton = EnemyAdvancedPatternSharedPresetCardUtility.CreateActionButton("Delete",
                                                                                             "Delete this shared module definition.",
                                                                                             () =>
                                                                                             {
                                                                                                 EnemyAdvancedPatternSharedPresetModulesMutationUtility.DeleteModuleDefinition(panel,
                                                                                                                                                       sharedPresetSerializedObject,
                                                                                                                                                       sharedPreset,
                                                                                                                                                       section,
                                                                                                                                                       moduleIndex);
                                                                                             },
                                                                                             4f);
        actionsRow.Add(deleteButton);

        PropertyField moduleField = new PropertyField(moduleProperty);
        moduleField.BindProperty(moduleProperty);
        moduleField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            EnemyAdvancedPatternSharedPresetEditorUtility.CommitSharedPresetSerializedChanges(panel,
                                                                                             sharedPresetSerializedObject,
                                                                                             sharedPreset,
                                                                                             "Edit Enemy Shared Module Definition",
                                                                                             false,
                                                                                             false);
        });
        foldout.Add(moduleField);

        SerializedProperty moduleIdProperty = moduleProperty.FindPropertyRelative("moduleId");
        SerializedProperty displayNameProperty = moduleProperty.FindPropertyRelative("displayName");
        SerializedProperty moduleKindProperty = moduleProperty.FindPropertyRelative("moduleKind");

        if (moduleIdProperty != null)
        {
            card.TrackPropertyValue(moduleIdProperty, changedProperty =>
            {
                UpdateModuleCardTitle(foldout, moduleIndex, moduleProperty);
            });
        }

        if (displayNameProperty != null)
        {
            card.TrackPropertyValue(displayNameProperty, changedProperty =>
            {
                UpdateModuleCardTitle(foldout, moduleIndex, moduleProperty);
            });
        }

        if (moduleKindProperty != null)
        {
            card.TrackPropertyValue(moduleKindProperty, changedProperty =>
            {
                UpdateModuleCardTitle(foldout, moduleIndex, moduleProperty);
            });
        }

        return card;
    }

    /// <summary>
    /// Registers the foldout title label of one shared module subsection for contextual recoloring.
    /// </summary>
    /// <param name="sectionFoldout">Foldout whose header label should expose the recolor menu.</param>
    /// <param name="stateKey">Stable persistence key used by the label.</param>
    private static void RegisterSectionFoldoutColorContextMenu(Foldout sectionFoldout, string stateKey)
    {
        if (sectionFoldout == null || string.IsNullOrWhiteSpace(stateKey))
            return;

        sectionFoldout.schedule.Execute(() =>
        {
            Label foldoutHeaderLabel = sectionFoldout.Q<Label>();

            if (foldoutHeaderLabel == null)
                return;

            ManagementToolCategoryLabelUtility.RegisterColorContextMenu(foldoutHeaderLabel, stateKey);
        });
    }

    /// <summary>
    /// Updates the foldout title of one module card after an identity field changes.
    /// </summary>
    /// <param name="foldout">Foldout whose title must be refreshed.</param>
    /// <param name="moduleIndex">Current module index.</param>
    /// <param name="moduleProperty">Serialized module definition property.</param>
    private static void UpdateModuleCardTitle(Foldout foldout,
                                              int moduleIndex,
                                              SerializedProperty moduleProperty)
    {
        if (foldout == null || moduleProperty == null)
            return;

        foldout.text = EnemyAdvancedPatternSharedPresetModuleDefinitionUtility.BuildModuleCardTitle(moduleIndex,
                                                                                                    EnemyAdvancedPatternSharedPresetModuleDefinitionUtility.ResolveModuleDefinitionId(moduleProperty),
                                                                                                    EnemyAdvancedPatternSharedPresetModuleDefinitionUtility.ResolveModuleDefinitionDisplayName(moduleProperty),
                                                                                                    EnemyAdvancedPatternSharedPresetModuleDefinitionUtility.ResolveModuleDefinitionKind(moduleProperty));
    }
    #endregion

    #endregion
}
