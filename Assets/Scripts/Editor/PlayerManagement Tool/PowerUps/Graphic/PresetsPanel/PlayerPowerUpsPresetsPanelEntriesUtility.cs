using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds active and passive power-up entry sections for the power-ups presets panel.
/// </summary>
public static class PlayerPowerUpsPresetsPanelEntriesUtility
{
    #region Methods

    #region Section Builders
    public static void BuildActivePowerUpsSection(PlayerPowerUpsPresetsPanel panel)
    {
        BuildPowerUpsSection(panel, true);
    }

    public static void BuildPassivePowerUpsSection(PlayerPowerUpsPresetsPanel panel)
    {
        BuildPowerUpsSection(panel, false);
    }

    private static void BuildPowerUpsSection(PlayerPowerUpsPresetsPanel panel, bool isActiveSection)
    {
        string sectionLabel = isActiveSection ? "Active Power Ups" : "Passive Power Ups";
        string propertyName = isActiveSection ? "activePowerUps" : "passivePowerUps";
        string filterIdValue = isActiveSection ? panel.activePowerUpIdFilterText : panel.passivePowerUpIdFilterText;
        string filterDisplayNameValue = isActiveSection ? panel.activePowerUpDisplayNameFilterText : panel.passivePowerUpDisplayNameFilterText;
        string addButtonLabel = isActiveSection ? "Add Active" : "Add Passive";
        string addButtonTooltip = isActiveSection ? "Create a new active power up entry." : "Create a new passive power up entry.";
        string clearButtonTooltip = isActiveSection ? "Clear active power up search filters." : "Clear passive power up search filters.";
        string filterTooltipPrefix = isActiveSection ? "active" : "passive";

        Label header = new Label(sectionLabel);
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(header, "NashCore.PlayerManagement.PowerUps.Entries." + sectionLabel.Replace(" ", string.Empty));
        panel.sectionContentRoot.Add(header);

        SerializedProperty powerUpsProperty = panel.presetSerializedObject.FindProperty(propertyName);

        if (powerUpsProperty == null)
        {
            Label missingLabel = new Label(sectionLabel + " property is missing.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            panel.sectionContentRoot.Add(missingLabel);
            return;
        }

        Label infoLabel = new Label("Composable " + filterTooltipPrefix + " entries assembled from module bindings.");
        infoLabel.style.marginBottom = 4f;
        panel.sectionContentRoot.Add(infoLabel);

        VisualElement filtersRow = new VisualElement();
        filtersRow.style.flexDirection = FlexDirection.Row;
        filtersRow.style.alignItems = Align.FlexEnd;
        filtersRow.style.marginBottom = 4f;

        TextField powerUpIdFilterField = new TextField("Filter PowerUp ID");
        powerUpIdFilterField.isDelayed = true;
        powerUpIdFilterField.value = filterIdValue;
        powerUpIdFilterField.style.flexGrow = 1f;
        powerUpIdFilterField.style.marginRight = 6f;
        powerUpIdFilterField.tooltip = "Show only " + filterTooltipPrefix + " power ups whose PowerUp ID contains this text.";
        filtersRow.Add(powerUpIdFilterField);

        TextField displayNameFilterField = new TextField("Filter Display Name");
        displayNameFilterField.isDelayed = true;
        displayNameFilterField.value = filterDisplayNameValue;
        displayNameFilterField.style.flexGrow = 1f;
        displayNameFilterField.tooltip = "Show only " + filterTooltipPrefix + " power ups whose Display Name contains this text.";
        filtersRow.Add(displayNameFilterField);
        panel.sectionContentRoot.Add(filtersRow);

        Label countLabel = new Label();
        ScrollView cardsContainer = new ScrollView();
        VisualElement actionsRow = new VisualElement();
        actionsRow.style.flexDirection = FlexDirection.Row;
        actionsRow.style.alignItems = Align.Center;
        actionsRow.style.marginBottom = 4f;

        Button addButton = new Button(() =>
        {
            panel.ScheduleDeferredStructuralAction(() =>
            {
                PlayerPowerUpsPresetsPanelEntriesSupportUtility.AddPowerUpDefinition(panel, isActiveSection);
            });
        });
        addButton.text = addButtonLabel;
        addButton.tooltip = addButtonTooltip;
        actionsRow.Add(addButton);

        Button expandAllButton = new Button(() =>
        {
            PlayerPowerUpsPresetsPanelEntriesSupportUtility.SetAllPowerUpFoldoutStates(panel, isActiveSection, true);
            RebuildPowerUpDefinitionCards(panel, cardsContainer, countLabel, isActiveSection);
        });
        expandAllButton.text = "Expand All";
        expandAllButton.tooltip = "Expand every visible " + filterTooltipPrefix + " power up card.";
        expandAllButton.style.marginLeft = 4f;
        actionsRow.Add(expandAllButton);

        Button collapseAllButton = new Button(() =>
        {
            PlayerPowerUpsPresetsPanelEntriesSupportUtility.SetAllPowerUpFoldoutStates(panel, isActiveSection, false);
            RebuildPowerUpDefinitionCards(panel, cardsContainer, countLabel, isActiveSection);
        });
        collapseAllButton.text = "Collapse All";
        collapseAllButton.tooltip = "Collapse every visible " + filterTooltipPrefix + " power up card.";
        collapseAllButton.style.marginLeft = 4f;
        actionsRow.Add(collapseAllButton);

        Button clearFiltersButton = new Button(() =>
        {
            if (isActiveSection)
            {
                panel.activePowerUpIdFilterText = string.Empty;
                panel.activePowerUpDisplayNameFilterText = string.Empty;
            }
            else
            {
                panel.passivePowerUpIdFilterText = string.Empty;
                panel.passivePowerUpDisplayNameFilterText = string.Empty;
            }

            powerUpIdFilterField.SetValueWithoutNotify(string.Empty);
            displayNameFilterField.SetValueWithoutNotify(string.Empty);
            RebuildPowerUpDefinitionCards(panel, cardsContainer, countLabel, isActiveSection);
        });
        clearFiltersButton.text = "Clear Filters";
        clearFiltersButton.tooltip = clearButtonTooltip;
        clearFiltersButton.style.marginLeft = 4f;
        actionsRow.Add(clearFiltersButton);
        panel.sectionContentRoot.Add(actionsRow);

        countLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        countLabel.style.marginBottom = 2f;
        panel.sectionContentRoot.Add(countLabel);

        cardsContainer.style.maxHeight = 620f;
        cardsContainer.style.paddingRight = 2f;
        panel.sectionContentRoot.Add(cardsContainer);

        powerUpIdFilterField.RegisterValueChangedCallback(evt =>
        {
            if (isActiveSection)
                panel.activePowerUpIdFilterText = evt.newValue;
            else
                panel.passivePowerUpIdFilterText = evt.newValue;

            RebuildPowerUpDefinitionCards(panel, cardsContainer, countLabel, isActiveSection);
        });

        displayNameFilterField.RegisterValueChangedCallback(evt =>
        {
            if (isActiveSection)
                panel.activePowerUpDisplayNameFilterText = evt.newValue;
            else
                panel.passivePowerUpDisplayNameFilterText = evt.newValue;

            RebuildPowerUpDefinitionCards(panel, cardsContainer, countLabel, isActiveSection);
        });

        RebuildPowerUpDefinitionCards(panel, cardsContainer, countLabel, isActiveSection);
    }

    private static void RebuildPowerUpDefinitionCards(PlayerPowerUpsPresetsPanel panel,
                                                      VisualElement cardsContainer,
                                                      Label countLabel,
                                                      bool isActiveSection)
    {
        if (cardsContainer == null)
            return;

        cardsContainer.Clear();

        if (panel.presetSerializedObject == null)
        {
            if (countLabel != null)
                countLabel.text = "Visible Entries: 0 / 0";

            return;
        }

        string propertyName = PlayerPowerUpsPresetsPanelEntriesSupportUtility.ResolvePowerUpsPropertyName(isActiveSection);
        SerializedProperty powerUpsProperty = panel.presetSerializedObject.FindProperty(propertyName);

        if (powerUpsProperty == null)
        {
            if (countLabel != null)
                countLabel.text = "Visible Entries: 0 / 0";

            HelpBox missingHelpBox = new HelpBox("Power ups property is missing.", HelpBoxMessageType.Warning);
            cardsContainer.Add(missingHelpBox);
            return;
        }

        int totalCount = powerUpsProperty.arraySize;
        int visibleCount = 0;
        HashSet<string> validFoldoutStateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, PowerUpModuleCatalogEntry> moduleCatalogById = PowerUpModuleCatalogUtility.BuildCatalogById(panel.presetSerializedObject);
        Dictionary<string, bool> foldoutStateByKey = PlayerPowerUpsPresetsPanelEntriesSupportUtility.ResolvePowerUpFoldoutStateMap(panel, isActiveSection);
        string powerUpIdFilterValue = isActiveSection ? panel.activePowerUpIdFilterText : panel.passivePowerUpIdFilterText;
        string displayNameFilterValue = isActiveSection ? panel.activePowerUpDisplayNameFilterText : panel.passivePowerUpDisplayNameFilterText;

        for (int powerUpIndex = 0; powerUpIndex < powerUpsProperty.arraySize; powerUpIndex++)
        {
            SerializedProperty powerUpProperty = powerUpsProperty.GetArrayElementAtIndex(powerUpIndex);

            if (powerUpProperty == null)
                continue;

            string powerUpId = PlayerPowerUpsPresetsPanelEntriesSupportUtility.ResolvePowerUpDefinitionId(powerUpProperty);
            string displayName = PlayerPowerUpsPresetsPanelEntriesSupportUtility.ResolvePowerUpDefinitionDisplayName(powerUpProperty);
            string foldoutStateKey = PlayerPowerUpsPresetsPanelEntriesSupportUtility.BuildPowerUpFoldoutStateKey(isActiveSection, powerUpProperty);
            validFoldoutStateKeys.Add(foldoutStateKey);

            if (!PlayerPowerUpsPresetsPanelEntriesSupportUtility.IsMatchingPowerUpFilters(powerUpId,
                                                                                          displayName,
                                                                                          powerUpIdFilterValue,
                                                                                          displayNameFilterValue))
                continue;

            VisualElement card = CreatePowerUpDefinitionCard(panel,
                                                             powerUpsProperty,
                                                             powerUpProperty,
                                                             powerUpIndex,
                                                             isActiveSection,
                                                             foldoutStateByKey,
                                                             moduleCatalogById);
            cardsContainer.Add(card);
            visibleCount += 1;
        }

        PlayerPowerUpsPresetsPanelEntriesSupportUtility.PruneFoldoutStateMap(foldoutStateByKey, validFoldoutStateKeys);

        if (countLabel != null)
            countLabel.text = string.Format("Visible Entries: {0} / {1}", visibleCount, totalCount);

        if (visibleCount > 0)
            return;

        string emptyMessage = isActiveSection ? "No active power ups match current filters." : "No passive power ups match current filters.";
        HelpBox emptyHelpBox = new HelpBox(emptyMessage, HelpBoxMessageType.Info);
        cardsContainer.Add(emptyHelpBox);
    }

    private static VisualElement CreatePowerUpDefinitionCard(PlayerPowerUpsPresetsPanel panel,
                                                             SerializedProperty powerUpsProperty,
                                                             SerializedProperty powerUpProperty,
                                                             int powerUpIndex,
                                                             bool isActiveSection,
                                                             Dictionary<string, bool> foldoutStateByKey,
                                                             Dictionary<string, PowerUpModuleCatalogEntry> moduleCatalogById)
    {
        VisualElement card = new VisualElement();
        card.style.marginBottom = 10f;
        card.style.paddingLeft = 6f;
        card.style.paddingRight = 6f;
        card.style.paddingTop = 4f;
        card.style.paddingBottom = 4f;
        card.style.backgroundColor = new Color(1f, 1f, 1f, 0.03f);
        card.style.borderBottomWidth = 1f;
        card.style.borderTopWidth = 1f;
        card.style.borderLeftWidth = 1f;
        card.style.borderRightWidth = 1f;
        card.style.borderBottomColor = new Color(1f, 1f, 1f, 0.14f);
        card.style.borderTopColor = new Color(1f, 1f, 1f, 0.14f);
        card.style.borderLeftColor = new Color(1f, 1f, 1f, 0.14f);
        card.style.borderRightColor = new Color(1f, 1f, 1f, 0.14f);

        string powerUpId = PlayerPowerUpsPresetsPanelEntriesSupportUtility.ResolvePowerUpDefinitionId(powerUpProperty);
        string displayName = PlayerPowerUpsPresetsPanelEntriesSupportUtility.ResolvePowerUpDefinitionDisplayName(powerUpProperty);
        int bindingCount = PlayerPowerUpsPresetsPanelEntriesSupportUtility.ResolvePowerUpDefinitionBindingCount(powerUpProperty);
        bool unreplaceable = PlayerPowerUpsPresetsPanelEntriesSupportUtility.ResolvePowerUpDefinitionUnreplaceable(powerUpProperty);
        string foldoutStateKey = PlayerPowerUpsPresetsPanelEntriesSupportUtility.BuildPowerUpFoldoutStateKey(isActiveSection, powerUpProperty);
        Foldout foldout = PlayerManagementFoldoutStateUtility.CreateFoldout(PlayerPowerUpsPresetsPanelEntriesSupportUtility.BuildPowerUpCardTitle(powerUpIndex,
                                                                                                                                                    powerUpId,
                                                                                                                                                    displayName,
                                                                                                                                                    bindingCount,
                                                                                                                                                    unreplaceable),
                                                                            foldoutStateKey,
                                                                            PlayerPowerUpsPresetsPanelEntriesSupportUtility.ResolvePowerUpFoldoutState(foldoutStateByKey,
                                                                                                                                                       foldoutStateKey));
        card.Add(foldout);

        VisualElement lazyContent = new VisualElement();
        foldout.Add(lazyContent);

        EnsurePowerUpDefinitionFoldoutContent(panel,
                                              powerUpsProperty,
                                              powerUpProperty,
                                              powerUpIndex,
                                              isActiveSection,
                                              moduleCatalogById,
                                              foldout,
                                              lazyContent,
                                              foldout.value);
        foldout.RegisterValueChangedCallback(evt =>
        {
            EnsurePowerUpDefinitionFoldoutContent(panel,
                                                  powerUpsProperty,
                                                  powerUpProperty,
                                                  powerUpIndex,
                                                  isActiveSection,
                                                  moduleCatalogById,
                                                  foldout,
                                                  lazyContent,
                                                  evt.newValue);
        });

        return card;
    }

    /// <summary>
    /// Builds one power-up card body on demand so closed entries do not instantiate nested binding payloads.
    /// </summary>
    /// <param name="panel">Power-ups panel that schedules structural mutations.</param>
    /// <param name="powerUpsProperty">Serialized active or passive power-up array owning the card.</param>
    /// <param name="powerUpProperty">Serialized power-up definition represented by the card.</param>
    /// <param name="powerUpIndex">Current array index used by move, duplicate and delete actions.</param>
    /// <param name="isActiveSection">True when the card belongs to Active Power Ups.</param>
    /// <param name="moduleCatalogById">Module catalog used to compute coverage warnings.</param>
    /// <param name="foldout">Foldout whose title is refreshed when the serialized entry changes.</param>
    /// <param name="contentContainer">Foldout child container receiving deferred UI.</param>
    /// <param name="expanded">True when the foldout is open and content is required.</param>
    private static void EnsurePowerUpDefinitionFoldoutContent(PlayerPowerUpsPresetsPanel panel,
                                                              SerializedProperty powerUpsProperty,
                                                              SerializedProperty powerUpProperty,
                                                              int powerUpIndex,
                                                              bool isActiveSection,
                                                              Dictionary<string, PowerUpModuleCatalogEntry> moduleCatalogById,
                                                              Foldout foldout,
                                                              VisualElement contentContainer,
                                                              bool expanded)
    {
        if (contentContainer == null)
            return;

        if (!expanded)
            return;

        if (contentContainer.childCount > 0)
            return;

        VisualElement actionsRow = new VisualElement();
        actionsRow.style.flexDirection = FlexDirection.Row;
        actionsRow.style.marginLeft = 14f;
        actionsRow.style.marginTop = 2f;
        actionsRow.style.marginBottom = 4f;
        contentContainer.Add(actionsRow);

        Button duplicateButton = new Button(() =>
        {
            panel.ScheduleDeferredStructuralAction(() =>
            {
                PlayerPowerUpsPresetsPanelEntriesSupportUtility.DuplicatePowerUpDefinition(panel, isActiveSection, powerUpIndex);
            });
        });
        duplicateButton.text = "Duplicate";
        duplicateButton.tooltip = "Duplicate this power up entry.";
        actionsRow.Add(duplicateButton);

        Button moveUpButton = new Button(() =>
        {
            panel.ScheduleDeferredStructuralAction(() =>
            {
                PlayerPowerUpsPresetsPanelEntriesSupportUtility.MovePowerUpDefinition(panel, isActiveSection, powerUpIndex, powerUpIndex - 1);
            });
        });
        moveUpButton.text = "Up";
        moveUpButton.tooltip = "Move this power up one position up.";
        moveUpButton.style.marginLeft = 4f;
        moveUpButton.SetEnabled(powerUpIndex > 0);
        actionsRow.Add(moveUpButton);

        Button moveDownButton = new Button(() =>
        {
            panel.ScheduleDeferredStructuralAction(() =>
            {
                PlayerPowerUpsPresetsPanelEntriesSupportUtility.MovePowerUpDefinition(panel, isActiveSection, powerUpIndex, powerUpIndex + 1);
            });
        });
        moveDownButton.text = "Down";
        moveDownButton.tooltip = "Move this power up one position down.";
        moveDownButton.style.marginLeft = 4f;
        moveDownButton.SetEnabled(powerUpIndex < powerUpsProperty.arraySize - 1);
        actionsRow.Add(moveDownButton);

        Button deleteButton = new Button(() =>
        {
            panel.ScheduleDeferredStructuralAction(() =>
            {
                PlayerPowerUpsPresetsPanelEntriesSupportUtility.DeletePowerUpDefinition(panel, isActiveSection, powerUpIndex);
            });
        });
        deleteButton.text = "Delete";
        deleteButton.tooltip = "Delete this power up entry.";
        deleteButton.style.marginLeft = 4f;
        actionsRow.Add(deleteButton);

        HelpBox coverageWarningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        coverageWarningBox.style.marginLeft = 14f;
        coverageWarningBox.style.marginBottom = 4f;
        contentContainer.Add(coverageWarningBox);

        PlayerPowerUpsPresetsPanelEntriesSupportUtility.UpdatePowerUpCardPresentation(powerUpProperty,
                                                                                      powerUpIndex,
                                                                                      isActiveSection,
                                                                                      foldout,
                                                                                      coverageWarningBox,
                                                                                      moduleCatalogById);

        PropertyField powerUpField = new PropertyField(powerUpProperty);
        powerUpField.BindProperty(powerUpProperty);
        powerUpField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            if (evt == null)
                return;

            Dictionary<string, PowerUpModuleCatalogEntry> updatedModuleCatalogById = PowerUpModuleCatalogUtility.BuildCatalogById(powerUpProperty.serializedObject);
            PlayerPowerUpsPresetsPanelEntriesSupportUtility.UpdatePowerUpCardPresentation(powerUpProperty,
                                                                                          powerUpIndex,
                                                                                          isActiveSection,
                                                                                          foldout,
                                                                                          coverageWarningBox,
                                                                                          updatedModuleCatalogById);
        });
        contentContainer.Add(powerUpField);
    }
    #endregion

    #endregion
}
