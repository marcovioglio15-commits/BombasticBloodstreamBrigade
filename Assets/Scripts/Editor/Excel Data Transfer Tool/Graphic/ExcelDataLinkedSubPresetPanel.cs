using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Edits one family of Excel transfer sub-presets with its own browser pane and internal sections.
/// </summary>
internal sealed class ExcelDataLinkedSubPresetPanel
{
    #region Constants
    private const float LeftPaneWidth = 340f;
    #endregion

    #region Fields
    private readonly ExcelDataTransferMasterPanel parentPanel;
    private readonly ExcelDataTransferPanelType panelType;
    private readonly VisualElement root;
    private readonly List<ScriptableObject> filteredPresets = new List<ScriptableObject>();

    private ToolbarSearchField searchField;
    private ListView presetListView;
    private Label browserStatusLabel;
    private ScrollView detailsRoot;
    private VisualElement sectionContentRoot;
    private ExcelDataTransferMasterPreset selectedMasterPreset;
    private ScriptableObject selectedPreset;
    private DetailsSectionType activeSection = DetailsSectionType.Metadata;
    #endregion

    #region Properties
    public VisualElement Root
    {
        get
        {
            return root;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Builds a linked sub-preset editor panel for import, export or brush palette assets.
    /// </summary>
    /// <param name="newParentPanel">Master panel that owns the linked preset graph.</param>
    /// <param name="newPanelType">Sub-preset family edited by this panel.</param>
    public ExcelDataLinkedSubPresetPanel(ExcelDataTransferMasterPanel newParentPanel,
                                         ExcelDataTransferPanelType newPanelType)
    {
        parentPanel = newParentPanel;
        panelType = newPanelType;
        selectedMasterPreset = parentPanel == null ? null : parentPanel.SelectedMasterPreset;

        root = new VisualElement();
        root.style.flexGrow = 1f;

        TwoPaneSplitView splitView = GameManagementPanelLayoutUtility.CreateHorizontalSplitView(LeftPaneWidth);
        root.Add(splitView);
        splitView.Add(BuildBrowserPane());
        splitView.Add(BuildDetailsPane());

        RefreshFromSessionChange();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Rebinds this panel after master selection, draft session or linked preset changes.
    /// </summary>
    public void RefreshFromSessionChange()
    {
        selectedMasterPreset = parentPanel == null ? null : parentPanel.SelectedMasterPreset;
        RefreshPresetList();
        SelectLinkedPreset();
    }

    #endregion

    #region Layout
    /// <summary>
    /// Builds the left sub-preset browser pane.
    /// </summary>
    /// <returns>Configured browser pane.</returns>
    private VisualElement BuildBrowserPane()
    {
        VisualElement pane = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureBrowserPane(pane);

        Label titleLabel = new Label(ExcelDataLinkedSubPresetPanelContextUtility.ResolvePanelTitle(panelType));
        titleLabel.tooltip = "Sub-preset assets available for " + ExcelDataLinkedSubPresetPanelContextUtility.ResolvePanelTitle(panelType) + ".";
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 6f;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(titleLabel, "NashCore.ExcelDataTransfer.SubPreset." + panelType);
        pane.Add(titleLabel);
        pane.Add(BuildBrowserToolbar());

        searchField = new ToolbarSearchField();
        searchField.tooltip = "Search sub-preset assets by asset name.";
        searchField.RegisterValueChangedCallback(evt => RefreshPresetList());
        GameManagementPanelLayoutUtility.ConfigureSearchField(searchField);
        pane.Add(searchField);
        GameManagementPanelLayoutUtility.BindSearchFieldToBrowserPane(pane, searchField);

        presetListView = BuildPresetListView();
        pane.Add(presetListView);
        browserStatusLabel = BuildBrowserStatusLabel();
        pane.Add(browserStatusLabel);
        return pane;
    }

    /// <summary>
    /// Builds browser actions for creating, duplicating and deleting sub-presets.
    /// </summary>
    /// <returns>Toolbar visual element.</returns>
    private Toolbar BuildBrowserToolbar()
    {
        Toolbar toolbar = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(toolbar);

        Button createButton = new Button(CreateSubPreset);
        createButton.text = "Create";
        createButton.tooltip = "Create a new standalone sub-preset of this type.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(createButton, 58f);
        toolbar.Add(createButton);

        Button duplicateButton = new Button(DuplicateSubPreset);
        duplicateButton.text = "Duplicate";
        duplicateButton.tooltip = "Duplicate the selected sub-preset without changing master links.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(duplicateButton, 78f);
        toolbar.Add(duplicateButton);

        Button deleteButton = new Button(DeleteSubPreset);
        deleteButton.text = "Delete";
        deleteButton.tooltip = "Delete the selected sub-preset when no transfer master references it.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(deleteButton, 58f);
        toolbar.Add(deleteButton);
        return toolbar;
    }

    /// <summary>
    /// Builds the browser status label shown under the preset list.
    /// </summary>
    /// <returns>Configured status label.</returns>
    private Label BuildBrowserStatusLabel()
    {
        Label label = new Label();
        label.tooltip = "Latest create, duplicate or delete result for this sub-preset browser.";
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginTop = 6f;
        return label;
    }

    /// <summary>
    /// Builds the right details pane.
    /// </summary>
    /// <returns>Configured scroll view.</returns>
    private ScrollView BuildDetailsPane()
    {
        detailsRoot = new ScrollView();
        GameManagementPanelLayoutUtility.ConfigureDetailsPane(detailsRoot);
        return detailsRoot;
    }

    /// <summary>
    /// Builds the sub-preset list view.
    /// </summary>
    /// <returns>Configured list view.</returns>
    private ListView BuildPresetListView()
    {
        ListView listView = new ListView();
        listView.itemsSource = filteredPresets;
        listView.makeItem = MakePresetListItem;
        listView.bindItem = BindPresetListItem;
        listView.selectionChanged += OnPresetSelectionChanged;
        listView.fixedItemHeight = 22f;
        listView.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
        GameManagementPanelLayoutUtility.ConfigureListView(listView);
        return listView;
    }
    #endregion

    #region List Binding
    /// <summary>
    /// Creates one sub-preset list row.
    /// </summary>
    /// <returns>Row label visual element.</returns>
    private VisualElement MakePresetListItem()
    {
        Label label = new Label();
        label.style.whiteSpace = WhiteSpace.NoWrap;
        GameManagementPanelLayoutUtility.ConfigureListRowLabel(label);
        return label;
    }

    /// <summary>
    /// Binds one sub-preset list row.
    /// </summary>
    /// <param name="element">Row element.</param>
    /// <param name="index">Filtered asset index.</param>
    private void BindPresetListItem(VisualElement element, int index)
    {
        Label label = element as Label;

        if (label == null)
            return;

        if (index < 0 || index >= filteredPresets.Count)
        {
            label.text = string.Empty;
            return;
        }

        ScriptableObject preset = filteredPresets[index];
        label.text = ExcelDataLinkedSubPresetPanelContextUtility.ResolvePresetDisplayName(preset);
        label.tooltip = AssetDatabase.GetAssetPath(preset);
    }

    /// <summary>
    /// Receives sub-preset selection changes from the browser list.
    /// </summary>
    /// <param name="selection">Selected ListView payload.</param>
    private void OnPresetSelectionChanged(IEnumerable<object> selection)
    {
        foreach (object selectedObject in selection)
        {
            selectedPreset = selectedObject as ScriptableObject;
            BuildDetailsShell();
            return;
        }
    }
    #endregion

    #region Preset List
    /// <summary>
    /// Refreshes the browser list for the current sub-preset family.
    /// </summary>
    private void RefreshPresetList()
    {
        filteredPresets.Clear();
        string searchText = searchField == null ? string.Empty : searchField.value;
        List<ScriptableObject> loadedPresets = ExcelDataLinkedSubPresetPanelContextUtility.LoadPresetsForPanel(panelType);

        for (int presetIndex = 0; presetIndex < loadedPresets.Count; presetIndex++)
        {
            ScriptableObject preset = loadedPresets[presetIndex];

            if (!MatchesSearch(preset, searchText))
                continue;

            filteredPresets.Add(preset);
        }

        if (presetListView != null)
            presetListView.Rebuild();
    }

    /// <summary>
    /// Selects the sub-preset currently linked by the active transfer master.
    /// </summary>
    private void SelectLinkedPreset()
    {
        ScriptableObject linkedPreset =
            ExcelDataLinkedSubPresetPanelContextUtility.ResolveLinkedPreset(panelType, selectedMasterPreset);

        if (linkedPreset != null && filteredPresets.Contains(linkedPreset))
            selectedPreset = linkedPreset;
        else if (filteredPresets.Count > 0 && selectedPreset == null)
            selectedPreset = filteredPresets[0];

        if (presetListView != null && selectedPreset != null)
        {
            int selectedIndex = filteredPresets.IndexOf(selectedPreset);

            if (selectedIndex >= 0)
                presetListView.SetSelectionWithoutNotify(new int[] { selectedIndex });
        }

        BuildDetailsShell();
    }
    #endregion

    #region Details
    /// <summary>
    /// Rebuilds section buttons and content for the selected sub-preset.
    /// </summary>
    private void BuildDetailsShell()
    {
        detailsRoot.Clear();

        if (selectedPreset == null)
        {
            detailsRoot.Add(new Label("Select a " + ExcelDataLinkedSubPresetPanelContextUtility.ResolvePanelTitle(panelType) + " asset to edit."));
            return;
        }

        detailsRoot.Add(BuildSectionButtons());
        sectionContentRoot = new VisualElement();
        sectionContentRoot.style.flexGrow = 1f;
        detailsRoot.Add(sectionContentRoot);
        BuildActiveDetailsSection();
    }

    /// <summary>
    /// Builds the currently selected details section.
    /// </summary>
    private void BuildActiveDetailsSection()
    {
        if (sectionContentRoot == null)
            return;

        sectionContentRoot.Clear();

        switch (activeSection)
        {
            case DetailsSectionType.Workbook:
                BuildWorkbookSection();
                break;
            case DetailsSectionType.Policies:
                BuildPoliciesSection();
                break;
            case DetailsSectionType.FieldSelection:
                BuildFieldSelectionSection();
                break;
            case DetailsSectionType.Actions:
                BuildActionsSection();
                break;
            case DetailsSectionType.Brushes:
                BuildBrushesSection();
                break;
            default:
                BuildMetadataSection();
                break;
        }

        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(sectionContentRoot);
    }

    /// <summary>
    /// Builds the section selector for the current sub-preset family.
    /// </summary>
    /// <returns>Section button row.</returns>
    private VisualElement BuildSectionButtons()
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;

        AddSectionButton(buttonsRoot, DetailsSectionType.Metadata, "Metadata");

        if (panelType == ExcelDataTransferPanelType.BrushPalette)
        {
            AddSectionButton(buttonsRoot, DetailsSectionType.Brushes, "Brushes");
            return buttonsRoot;
        }

        AddSectionButton(buttonsRoot, DetailsSectionType.Workbook, "Workbook");
        AddSectionButton(buttonsRoot, DetailsSectionType.Policies, panelType == ExcelDataTransferPanelType.ImportPreset ? "Policies" : "Filters");
        AddSectionButton(buttonsRoot, DetailsSectionType.FieldSelection, "Field Selection");
        AddSectionButton(buttonsRoot, DetailsSectionType.Actions, "Actions");
        return buttonsRoot;
    }

    /// <summary>
    /// Adds one section selector button.
    /// </summary>
    /// <param name="parent">Parent button row.</param>
    /// <param name="sectionType">Section to activate.</param>
    /// <param name="label">Visible button label.</param>
    private void AddSectionButton(VisualElement parent, DetailsSectionType sectionType, string label)
    {
        Button button = new Button(() =>
        {
            activeSection = sectionType;
            BuildActiveDetailsSection();
        });
        button.text = label;
        button.tooltip = "Show " + label + " for this sub-preset.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(button, ResolveSectionButtonWidth(sectionType));
        button.style.marginRight = 4f;
        button.style.marginBottom = 4f;
        parent.Add(button);
    }
    #endregion

    #region Section Builders
    /// <summary>
    /// Builds shared metadata fields.
    /// </summary>
    private void BuildMetadataSection()
    {
        SerializedObject serializedObject = new SerializedObject(selectedPreset);
        VisualElement section = ExcelDataTransferMasterPanelSectionUtility.CreateSection(sectionContentRoot, "Metadata");
        ExcelDataLinkedSubPresetPanelFieldUtility.AddPropertyField(section, serializedObject, "presetName", "Preset Name", "Readable sub-preset name.");
        ExcelDataLinkedSubPresetPanelFieldUtility.AddDisabledPropertyField(section, serializedObject, "presetId", "Preset ID", "Stable editor identifier.");
        AddLinkedStatus(section);
    }

    /// <summary>
    /// Builds workbook path and layout fields for import/export presets.
    /// </summary>
    private void BuildWorkbookSection()
    {
        ExcelDataLinkedSubPresetPanelWorkbookUtility.BuildWorkbookSection(sectionContentRoot, selectedPreset, panelType);
    }

    /// <summary>
    /// Builds domain, policy and reference fields.
    /// </summary>
    private void BuildPoliciesSection()
    {
        SerializedObject serializedObject = new SerializedObject(selectedPreset);
        VisualElement section = ExcelDataTransferMasterPanelSectionUtility.CreateSection(sectionContentRoot, panelType == ExcelDataTransferPanelType.ImportPreset ? "Policies" : "Filters");

        if (panelType == ExcelDataTransferPanelType.ImportPreset)
        {
            ExcelDataLinkedSubPresetPanelFieldUtility.AddPropertyField(section, serializedObject, "conflictPolicy", "Conflict Policy", "Policy used when workbook values target existing Unity data.");
            ExcelDataLinkedSubPresetPanelFieldUtility.AddPropertyField(section, serializedObject, "missingRowPolicy", "Missing Row Policy", "Policy used when workbook rows are absent but Unity data exists.");
            ExcelDataLinkedSubPresetPanelFieldUtility.AddPropertyField(section, serializedObject, "referenceResolutionMode", "Reference Resolution", "Resolver used for asset-name, GUID and path metadata.");
            ExcelDataLinkedSubPresetPanelFieldUtility.AddPropertyField(section, serializedObject, "requirePreviewBeforeApply", "Require Preview Before Apply", "Require preview before import mutates assets.");
            ExcelDataLinkedSubPresetPanelFieldUtility.AddPropertyField(section, serializedObject, "blockAmbiguousReferences", "Block Ambiguous References", "Block import when an asset name is ambiguous.");
        }
        else
        {
            ExcelDataLinkedSubPresetPanelFieldUtility.AddPropertyField(section, serializedObject, "writeAssetNames", "Write Asset Names", "Write readable asset names for object references.");
            ExcelDataLinkedSubPresetPanelFieldUtility.AddPropertyField(section, serializedObject, "writeReferenceGuids", "Write Reference GUIDs", "Write GUID metadata to disambiguate references.");
            ExcelDataLinkedSubPresetPanelFieldUtility.AddPropertyField(section, serializedObject, "writeReferencePaths", "Write Reference Paths", "Write asset paths for diagnostics.");
        }

        ExcelDataTransferMasterPanelFieldUtility.AddDomainFields(parentPanel, section, serializedObject);
    }

    /// <summary>
    /// Builds the selected field list.
    /// </summary>
    private void BuildFieldSelectionSection()
    {
        SerializedObject serializedObject = new SerializedObject(selectedPreset);
        VisualElement section = ExcelDataTransferMasterPanelSectionUtility.CreateSection(sectionContentRoot, "Field Selection");
        ExcelDataLinkedSubPresetPanelFieldUtility.AddPropertyField(section, serializedObject, "selectedFields", "Selected Fields", "Fields explicitly selected for this transfer direction.");
    }

    /// <summary>
    /// Builds import/export action buttons.
    /// </summary>
    private void BuildActionsSection()
    {
        VisualElement section = ExcelDataTransferMasterPanelSectionUtility.CreateSection(sectionContentRoot, "Actions");
        AddLinkedStatus(section);

        if (!IsSelectedPresetLinked())
        {
            HelpBox helpBox = new HelpBox("Link this sub-preset from Transfer Master Presets > Sub Presets before running operations.", HelpBoxMessageType.Info);
            section.Add(helpBox);
            return;
        }

        if (panelType == ExcelDataTransferPanelType.ImportPreset)
            ExcelDataTransferMasterPanelOperationsUtility.BuildImportActions(parentPanel, section);
        else
            ExcelDataTransferMasterPanelOperationsUtility.BuildExportActions(parentPanel, section);
    }

    /// <summary>
    /// Builds editable brush definitions.
    /// </summary>
    private void BuildBrushesSection()
    {
        SerializedObject serializedObject = new SerializedObject(selectedPreset);
        VisualElement section = ExcelDataTransferMasterPanelSectionUtility.CreateSection(sectionContentRoot, "Brushes");
        ExcelDataLinkedSubPresetPanelFieldUtility.AddPropertyField(section, serializedObject, "brushes", "Brushes", "Saved brush configurations available to the layout brush grid.");
    }
    #endregion

    #region Actions
    /// <summary>
    /// Creates one sub-preset for the active browser type and selects it.
    /// </summary>
    private void CreateSubPreset()
    {
        ScriptableObject createdPreset = ExcelDataLinkedSubPresetPanelContextUtility.CreatePresetForPanel(panelType);

        if (createdPreset == null)
        {
            SetBrowserStatus("Could not create a sub-preset for this panel.");
            return;
        }

        ExcelDataTransferDraftSession.MarkDirty();
        SelectPresetAfterListRefresh(createdPreset);
        SetBrowserStatus("Created sub-preset " + createdPreset.name + ".");
    }

    /// <summary>
    /// Duplicates the selected sub-preset and selects the copy.
    /// </summary>
    private void DuplicateSubPreset()
    {
        if (selectedPreset == null)
        {
            SetBrowserStatus("Select a sub-preset before duplicating.");
            return;
        }

        ScriptableObject duplicatedPreset =
            ExcelDataLinkedSubPresetPanelContextUtility.DuplicatePresetForPanel(panelType, selectedPreset);

        if (duplicatedPreset == null)
        {
            SetBrowserStatus("Could not duplicate the selected sub-preset.");
            return;
        }

        ExcelDataTransferDraftSession.MarkDirty();
        SelectPresetAfterListRefresh(duplicatedPreset);
        SetBrowserStatus("Duplicated sub-preset " + duplicatedPreset.name + ".");
    }

    /// <summary>
    /// Deletes the selected sub-preset when it is not linked by a transfer master.
    /// </summary>
    private void DeleteSubPreset()
    {
        string statusMessage;
        bool deleted = ExcelDataLinkedSubPresetPanelContextUtility.DeletePreset(selectedPreset, out statusMessage);
        SetBrowserStatus(statusMessage);

        if (!deleted)
            return;

        selectedPreset = null;
        ExcelDataTransferDraftSession.MarkDirty();
        RefreshPresetList();
        SelectLinkedPreset();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Clears the search field, refreshes the browser and selects one preset.
    /// </summary>
    /// <param name="preset">Preset asset to select after refresh.</param>
    private void SelectPresetAfterListRefresh(ScriptableObject preset)
    {
        if (searchField != null)
            searchField.SetValueWithoutNotify(string.Empty);

        selectedPreset = preset;
        RefreshPresetList();
        BuildDetailsShell();

        if (presetListView == null || selectedPreset == null)
            return;

        int selectedIndex = filteredPresets.IndexOf(selectedPreset);

        if (selectedIndex >= 0)
            presetListView.SetSelectionWithoutNotify(new int[] { selectedIndex });
    }

    /// <summary>
    /// Updates the browser status label when it exists.
    /// </summary>
    /// <param name="message">Status text to show.</param>
    private void SetBrowserStatus(string message)
    {
        if (browserStatusLabel != null)
            browserStatusLabel.text = message;
    }

    /// <summary>
    /// Checks whether the selected browser asset is the one linked by the active master.
    /// </summary>
    /// <returns>True when selected and linked assets match.</returns>
    private bool IsSelectedPresetLinked()
    {
        return selectedPreset != null &&
               selectedPreset == ExcelDataLinkedSubPresetPanelContextUtility.ResolveLinkedPreset(panelType, selectedMasterPreset);
    }

    /// <summary>
    /// Adds a compact linked/unlinked status label.
    /// </summary>
    /// <param name="parent">Parent visual element.</param>
    private void AddLinkedStatus(VisualElement parent)
    {
        Label label = new Label(IsSelectedPresetLinked() ? "Linked to active master preset." : "Not linked to active master preset.");
        label.tooltip = "Shows whether the selected sub-preset is the asset currently referenced by the active master preset.";
        label.style.marginTop = 6f;
        parent.Add(label);
    }

    /// <summary>
    /// Checks whether one preset matches the current search text.
    /// </summary>
    /// <param name="preset">Preset to test.</param>
    /// <param name="searchText">Search text.</param>
    /// <returns>True when the preset remains visible.</returns>
    private bool MatchesSearch(ScriptableObject preset, string searchText)
    {
        if (preset == null)
            return false;

        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        return ExcelDataLinkedSubPresetPanelContextUtility.ResolvePresetDisplayName(preset).ToLowerInvariant().Contains(searchText.ToLowerInvariant()) ||
               preset.name.ToLowerInvariant().Contains(searchText.ToLowerInvariant());
    }

    /// <summary>
    /// Resolves a stable section button width.
    /// </summary>
    /// <param name="sectionType">Section button type.</param>
    /// <returns>Button width in pixels.</returns>
    private float ResolveSectionButtonWidth(DetailsSectionType sectionType)
    {
        switch (sectionType)
        {
            case DetailsSectionType.FieldSelection:
                return 116f;
            case DetailsSectionType.Workbook:
                return 88f;
            default:
                return 76f;
        }
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Internal section tabs used by linked sub-preset panels.
    /// </summary>
    private enum DetailsSectionType
    {
        Metadata = 0,
        Workbook = 1,
        Policies = 2,
        FieldSelection = 3,
        Actions = 4,
        Brushes = 5
    }
    #endregion
}
