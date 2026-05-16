using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds the card-based shared pattern editors used by the enemy Modules and Patterns preset flow.
/// </summary>
internal static class EnemyAdvancedPatternSharedPresetPatternsListUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the shared pattern list with filters, card actions and loadout synchronization hooks.
    /// </summary>
    /// <param name="panel">Owning panel that stores transient filter state and rebuild callbacks.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="sharedPreset">Shared preset asset being edited.</param>
    /// <param name="parent">Parent foldout that receives the pattern list UI.</param>
    public static void BuildPatternSection(EnemyAdvancedPatternPresetsPanel panel,
                                           SerializedObject sharedPresetSerializedObject,
                                           EnemyModulesAndPatternsPreset sharedPreset,
                                           VisualElement parent)
    {
        if (panel == null || sharedPresetSerializedObject == null || sharedPreset == null || parent == null)
            return;

        SerializedProperty patternsProperty = sharedPresetSerializedObject.FindProperty("patterns");

        if (patternsProperty == null)
            return;

        parent.Add(EnemyAdvancedPatternSharedPresetCardUtility.CreateDescriptionLabel("Shared assembled patterns built from the catalog lists above."));

        VisualElement filtersRow = EnemyAdvancedPatternSharedPresetCardUtility.CreateHorizontalRow(true);
        TextField patternIdFilterField = EnemyAdvancedPatternSharedPresetCardUtility.CreateDelayedFilterField("Filter Pattern ID",
                                                                                                               "Show only shared patterns whose Pattern ID contains this text.",
                                                                                                               panel.SharedPresetViewState.GetPatternIdFilterText(),
                                                                                                               6f);
        TextField displayNameFilterField = EnemyAdvancedPatternSharedPresetCardUtility.CreateDelayedFilterField("Filter Display Name",
                                                                                                                  "Show only shared patterns whose Display Name contains this text.",
                                                                                                                  panel.SharedPresetViewState.GetPatternDisplayNameFilterText(),
                                                                                                                  0f);
        filtersRow.Add(patternIdFilterField);
        filtersRow.Add(displayNameFilterField);
        parent.Add(filtersRow);

        VisualElement actionsRow = EnemyAdvancedPatternSharedPresetCardUtility.CreateHorizontalRow(false);
        Label countLabel = EnemyAdvancedPatternSharedPresetCardUtility.CreateCountLabel();
        ScrollView cardsContainer = EnemyAdvancedPatternSharedPresetCardUtility.CreateCardsScrollView();

        Button addButton = EnemyAdvancedPatternSharedPresetCardUtility.CreateActionButton("Add Pattern",
                                                                                          "Create a new shared assembled pattern.",
                                                                                          () =>
                                                                                          {
                                                                                              EnemyAdvancedPatternSharedPresetPatternsMutationUtility.AddPatternDefinition(panel,
                                                                                                                                                                  sharedPresetSerializedObject,
                                                                                                                                                                  sharedPreset);
                                                                                          },
                                                                                          0f);
        actionsRow.Add(addButton);

        Button expandAllButton = EnemyAdvancedPatternSharedPresetCardUtility.CreateActionButton("Expand All",
                                                                                                "Expand every visible shared pattern card.",
                                                                                                () =>
                                                                                                {
                                                                                                    EnemyAdvancedPatternSharedPresetPatternsMutationUtility.SetAllPatternFoldoutStates(panel,
                                                                                                                                                       sharedPresetSerializedObject,
                                                                                                                                                       true);
                                                                                                    RebuildPatternCards(panel,
                                                                                                                        sharedPresetSerializedObject,
                                                                                                                        sharedPreset,
                                                                                                                        cardsContainer,
                                                                                                                        countLabel);
                                                                                                },
                                                                                                4f);
        actionsRow.Add(expandAllButton);

        Button collapseAllButton = EnemyAdvancedPatternSharedPresetCardUtility.CreateActionButton("Collapse All",
                                                                                                  "Collapse every visible shared pattern card.",
                                                                                                  () =>
                                                                                                  {
                                                                                                      EnemyAdvancedPatternSharedPresetPatternsMutationUtility.SetAllPatternFoldoutStates(panel,
                                                                                                                                                         sharedPresetSerializedObject,
                                                                                                                                                         false);
                                                                                                      RebuildPatternCards(panel,
                                                                                                                          sharedPresetSerializedObject,
                                                                                                                          sharedPreset,
                                                                                                                          cardsContainer,
                                                                                                                          countLabel);
                                                                                                  },
                                                                                                  4f);
        actionsRow.Add(collapseAllButton);

        Button clearFiltersButton = EnemyAdvancedPatternSharedPresetCardUtility.CreateActionButton("Clear Filters",
                                                                                                   "Clear both shared pattern filters.",
                                                                                                   () =>
                                                                                                   {
                                                                                                       panel.SharedPresetViewState.ClearPatternFilters();
                                                                                                       patternIdFilterField.SetValueWithoutNotify(string.Empty);
                                                                                                       displayNameFilterField.SetValueWithoutNotify(string.Empty);
                                                                                                       RebuildPatternCards(panel,
                                                                                                                           sharedPresetSerializedObject,
                                                                                                                           sharedPreset,
                                                                                                                           cardsContainer,
                                                                                                                           countLabel);
                                                                                                   },
                                                                                                   4f);
        actionsRow.Add(clearFiltersButton);

        parent.Add(actionsRow);
        parent.Add(countLabel);
        parent.Add(cardsContainer);

        patternIdFilterField.RegisterValueChangedCallback(evt =>
        {
            panel.SharedPresetViewState.SetPatternIdFilterText(evt.newValue);
            RebuildPatternCards(panel,
                                sharedPresetSerializedObject,
                                sharedPreset,
                                cardsContainer,
                                countLabel);
        });
        displayNameFilterField.RegisterValueChangedCallback(evt =>
        {
            panel.SharedPresetViewState.SetPatternDisplayNameFilterText(evt.newValue);
            RebuildPatternCards(panel,
                                sharedPresetSerializedObject,
                                sharedPreset,
                                cardsContainer,
                                countLabel);
        });

        RebuildPatternCards(panel,
                            sharedPresetSerializedObject,
                            sharedPreset,
                            cardsContainer,
                            countLabel);
    }

    /// <summary>
    /// Returns whether one shared pattern matches the current pattern filters.
    /// </summary>
    /// <param name="panel">Owning panel that stores filter text.</param>
    /// <param name="patternId">Candidate pattern ID.</param>
    /// <param name="displayName">Candidate pattern display name.</param>
    /// <returns>True when the pattern should remain visible.</returns>
    internal static bool IsMatchingPatternFilters(EnemyAdvancedPatternPresetsPanel panel,
                                                  string patternId,
                                                  string displayName)
    {
        if (panel == null)
            return true;

        string patternIdFilterText = panel.SharedPresetViewState.GetPatternIdFilterText();

        if (!string.IsNullOrWhiteSpace(patternIdFilterText))
        {
            string resolvedPatternId = string.IsNullOrWhiteSpace(patternId) ? string.Empty : patternId;

            if (resolvedPatternId.IndexOf(patternIdFilterText, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        string displayNameFilterText = panel.SharedPresetViewState.GetPatternDisplayNameFilterText();

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
    /// Rebuilds the visible shared pattern cards from the current filter state.
    /// </summary>
    /// <param name="panel">Owning panel that stores filter state.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="sharedPreset">Shared preset asset being edited.</param>
    /// <param name="cardsContainer">Container that receives generated cards.</param>
    /// <param name="countLabel">Count label updated with visible and total entries.</param>
    private static void RebuildPatternCards(EnemyAdvancedPatternPresetsPanel panel,
                                            SerializedObject sharedPresetSerializedObject,
                                            EnemyModulesAndPatternsPreset sharedPreset,
                                            VisualElement cardsContainer,
                                            Label countLabel)
    {
        if (panel == null || sharedPresetSerializedObject == null || sharedPreset == null || cardsContainer == null || countLabel == null)
            return;

        cardsContainer.Clear();
        SerializedProperty patternsProperty = sharedPresetSerializedObject.FindProperty("patterns");

        if (patternsProperty == null)
        {
            countLabel.text = "Visible Patterns: 0 / 0";
            cardsContainer.Add(EnemyAdvancedPatternSharedPresetCardUtility.CreateStatusLabel("Shared pattern definitions are unavailable."));
            return;
        }

        int totalPatterns = patternsProperty.arraySize;
        int visiblePatterns = 0;

        for (int patternIndex = 0; patternIndex < patternsProperty.arraySize; patternIndex++)
        {
            SerializedProperty patternProperty = patternsProperty.GetArrayElementAtIndex(patternIndex);

            if (patternProperty == null)
                continue;

            string patternId = EnemyAdvancedPatternSharedPresetPatternDefinitionUtility.ResolvePatternId(patternProperty);
            string displayName = EnemyAdvancedPatternSharedPresetPatternDefinitionUtility.ResolvePatternDisplayName(patternProperty);

            if (!IsMatchingPatternFilters(panel, patternId, displayName))
                continue;

            bool unreplaceable = EnemyAdvancedPatternSharedPresetPatternDefinitionUtility.ResolvePatternUnreplaceable(patternProperty);
            cardsContainer.Add(CreatePatternCard(panel,
                                                 sharedPresetSerializedObject,
                                                 sharedPreset,
                                                 patternsProperty,
                                                 patternProperty,
                                                 patternIndex,
                                                 patternId,
                                                 displayName,
                                                 unreplaceable));
            visiblePatterns += 1;
        }

        countLabel.text = string.Format("Visible Patterns: {0} / {1}", visiblePatterns, totalPatterns);

        if (visiblePatterns > 0)
            return;

        cardsContainer.Add(EnemyAdvancedPatternSharedPresetCardUtility.CreateStatusLabel("No patterns match current filters."));
    }

    /// <summary>
    /// Creates one shared pattern card with foldout, actions and bound property field.
    /// </summary>
    /// <param name="panel">Owning panel that provides callbacks.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="sharedPreset">Shared preset asset being edited.</param>
    /// <param name="patternsProperty">Serialized patterns array that owns the card.</param>
    /// <param name="patternProperty">Serialized pattern property displayed by the card.</param>
    /// <param name="patternIndex">Current pattern index.</param>
    /// <param name="patternId">Resolved pattern ID.</param>
    /// <param name="displayName">Resolved display name.</param>
    /// <param name="unreplaceable">Resolved unreplaceable flag.</param>
    /// <returns>Created card element.</returns>
    private static VisualElement CreatePatternCard(EnemyAdvancedPatternPresetsPanel panel,
                                                   SerializedObject sharedPresetSerializedObject,
                                                   EnemyModulesAndPatternsPreset sharedPreset,
                                                   SerializedProperty patternsProperty,
                                                   SerializedProperty patternProperty,
                                                   int patternIndex,
                                                   string patternId,
                                                   string displayName,
                                                   bool unreplaceable)
    {
        VisualElement card = EnemyAdvancedPatternSharedPresetCardUtility.CreateCardContainer();
        string foldoutStateKey = EnemyAdvancedPatternSharedPresetPatternDefinitionUtility.BuildPatternCardStateKey(patternProperty);
        Foldout foldout = ManagementToolFoldoutStateUtility.CreateFoldout(EnemyAdvancedPatternSharedPresetPatternDefinitionUtility.BuildPatternCardTitle(patternIndex,
                                                                                                                                                        patternId,
                                                                                                                                                        displayName,
                                                                                                                                                        unreplaceable),
                                                                          foldoutStateKey,
                                                                          ManagementToolFoldoutStateUtility.ResolveFoldoutState(foldoutStateKey, false));
        card.Add(foldout);

        VisualElement actionsRow = EnemyAdvancedPatternSharedPresetCardUtility.CreateCardActionsRow();
        foldout.Add(actionsRow);

        Button duplicateButton = EnemyAdvancedPatternSharedPresetCardUtility.CreateActionButton("Duplicate",
                                                                                                "Duplicate this shared assembled pattern.",
                                                                                                () =>
                                                                                                {
                                                                                                    EnemyAdvancedPatternSharedPresetPatternsMutationUtility.DuplicatePatternDefinition(panel,
                                                                                                                                                             sharedPresetSerializedObject,
                                                                                                                                                             sharedPreset,
                                                                                                                                                             patternIndex);
                                                                                                },
                                                                                                0f);
        actionsRow.Add(duplicateButton);

        Button moveUpButton = EnemyAdvancedPatternSharedPresetCardUtility.CreateActionButton("Up",
                                                                                             "Move this shared pattern one position up.",
                                                                                             () =>
                                                                                             {
                                                                                                 EnemyAdvancedPatternSharedPresetPatternsMutationUtility.MovePatternDefinition(panel,
                                                                                                                                                          sharedPresetSerializedObject,
                                                                                                                                                          sharedPreset,
                                                                                                                                                          patternIndex,
                                                                                                                                                          patternIndex - 1);
                                                                                             },
                                                                                             4f);
        moveUpButton.SetEnabled(patternIndex > 0);
        actionsRow.Add(moveUpButton);

        Button moveDownButton = EnemyAdvancedPatternSharedPresetCardUtility.CreateActionButton("Down",
                                                                                               "Move this shared pattern one position down.",
                                                                                               () =>
                                                                                               {
                                                                                                   EnemyAdvancedPatternSharedPresetPatternsMutationUtility.MovePatternDefinition(panel,
                                                                                                                                                            sharedPresetSerializedObject,
                                                                                                                                                            sharedPreset,
                                                                                                                                                            patternIndex,
                                                                                                                                                            patternIndex + 1);
                                                                                               },
                                                                                               4f);
        moveDownButton.SetEnabled(patternIndex < patternsProperty.arraySize - 1);
        actionsRow.Add(moveDownButton);

        Button deleteButton = EnemyAdvancedPatternSharedPresetCardUtility.CreateActionButton("Delete",
                                                                                             "Delete this shared assembled pattern.",
                                                                                             () =>
                                                                                             {
                                                                                                 EnemyAdvancedPatternSharedPresetPatternsMutationUtility.DeletePatternDefinition(panel,
                                                                                                                                                            sharedPresetSerializedObject,
                                                                                                                                                            sharedPreset,
                                                                                                                                                            patternIndex);
                                                                                             },
                                                                                             4f);
        actionsRow.Add(deleteButton);

        PropertyField patternField = new PropertyField(patternProperty);
        patternField.BindProperty(patternProperty);
        patternField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            EnemyAdvancedPatternSharedPresetEditorUtility.CommitSharedPresetSerializedChanges(panel,
                                                                                             sharedPresetSerializedObject,
                                                                                             sharedPreset,
                                                                                             "Edit Enemy Shared Pattern Definition",
                                                                                             true,
                                                                                             false);
        });
        foldout.Add(patternField);

        SerializedProperty patternIdProperty = patternProperty.FindPropertyRelative("patternId");
        SerializedProperty displayNameProperty = patternProperty.FindPropertyRelative("displayName");
        SerializedProperty unreplaceableProperty = patternProperty.FindPropertyRelative("unreplaceable");

        if (patternIdProperty != null)
        {
            card.TrackPropertyValue(patternIdProperty, changedProperty =>
            {
                UpdatePatternCardTitle(foldout, patternIndex, patternProperty);
            });
        }

        if (displayNameProperty != null)
        {
            card.TrackPropertyValue(displayNameProperty, changedProperty =>
            {
                UpdatePatternCardTitle(foldout, patternIndex, patternProperty);
            });
        }

        if (unreplaceableProperty != null)
        {
            card.TrackPropertyValue(unreplaceableProperty, changedProperty =>
            {
                UpdatePatternCardTitle(foldout, patternIndex, patternProperty);
            });
        }

        return card;
    }

    /// <summary>
    /// Updates the foldout title of one shared pattern card after an identity field changes.
    /// </summary>
    /// <param name="foldout">Foldout whose title must be refreshed.</param>
    /// <param name="patternIndex">Current pattern index.</param>
    /// <param name="patternProperty">Serialized pattern definition property.</param>
    private static void UpdatePatternCardTitle(Foldout foldout,
                                               int patternIndex,
                                               SerializedProperty patternProperty)
    {
        if (foldout == null || patternProperty == null)
            return;

        foldout.text = EnemyAdvancedPatternSharedPresetPatternDefinitionUtility.BuildPatternCardTitle(patternIndex,
                                                                                                      EnemyAdvancedPatternSharedPresetPatternDefinitionUtility.ResolvePatternId(patternProperty),
                                                                                                      EnemyAdvancedPatternSharedPresetPatternDefinitionUtility.ResolvePatternDisplayName(patternProperty),
                                                                                                      EnemyAdvancedPatternSharedPresetPatternDefinitionUtility.ResolvePatternUnreplaceable(patternProperty));
    }
    #endregion

    #endregion
}
