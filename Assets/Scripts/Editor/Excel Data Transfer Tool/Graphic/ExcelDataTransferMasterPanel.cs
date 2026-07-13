using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Root panel for Excel transfer master presets and their independently opened sub-preset panels.
/// </summary>
public sealed class ExcelDataTransferMasterPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<ExcelDataTransferMasterPreset> filteredPresets = new List<ExcelDataTransferMasterPreset>();
    private readonly List<ExcelDataImportPreviewRow> previewRows = new List<ExcelDataImportPreviewRow>();
    private readonly Dictionary<ExcelDataTransferPanelType, SidePanelEntry> sidePanels =
        new Dictionary<ExcelDataTransferPanelType, SidePanelEntry>();

    private VisualElement mainContentRoot;
    private VisualElement tabBar;
    private VisualElement contentHost;
    private ListView presetListView;
    private ToolbarSearchField searchField;
    private ScrollView detailsRoot;
    private VisualElement sectionContentRoot;
    private ExcelDataTransferMasterPreset selectedMasterPreset;
    private ExcelDataTransferDetailsSectionType activeDetailsSection = ExcelDataTransferDetailsSectionType.Metadata;
    private ExcelDataTransferPanelType activePanel = ExcelDataTransferPanelType.TransferMasterPresets;
    private ExcelDataFieldCatalogPanel fieldCatalogPanel;
    private ExcelDataImportPreviewResult importPreviewResult;
    private string operationStatus;
    private bool scheduledDetailsRefresh;
    #endregion

    #region Properties
    public VisualElement Root
    {
        get
        {
            return root;
        }
    }

    internal Dictionary<ExcelDataTransferPanelType, SidePanelEntry> SidePanels
    {
        get
        {
            return sidePanels;
        }
    }

    internal VisualElement MainContentRoot
    {
        get
        {
            return mainContentRoot;
        }
    }

    internal VisualElement TabBar
    {
        get
        {
            return tabBar;
        }
    }

    internal VisualElement ContentHost
    {
        get
        {
            return contentHost;
        }
    }

    internal ExcelDataTransferPanelType ActivePanel
    {
        get
        {
            return activePanel;
        }
        set
        {
            activePanel = value;
        }
    }

    internal ExcelDataTransferMasterPreset SelectedMasterPreset
    {
        get
        {
            return selectedMasterPreset;
        }
    }

    internal List<ExcelDataImportPreviewRow> PreviewRows
    {
        get
        {
            return previewRows;
        }
    }

    internal ExcelDataImportPreviewResult ImportPreviewResult
    {
        get
        {
            return importPreviewResult;
        }
        set
        {
            importPreviewResult = value;
        }
    }

    internal string OperationStatus
    {
        get
        {
            return operationStatus;
        }
        set
        {
            operationStatus = value;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Builds the transfer preset panel and restores the last selected master preset.
    /// </summary>
    public ExcelDataTransferMasterPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;
        root.style.flexDirection = FlexDirection.Column;

        mainContentRoot = BuildMainContentRoot();
        tabBar = BuildPanelTabBar();
        contentHost = BuildContentHost();
        root.Add(tabBar);
        root.Add(contentHost);

        ExcelDataTransferMasterPanelSidePanelUtility.BuildPanelsContainer(this);
        RefreshPresetList();
        SelectInitialPreset();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Refreshes panel bindings after the draft session applies, discards, undoes or redoes changes.
    /// </summary>
    public void RefreshFromSessionChange()
    {
        ExcelDataTransferMasterPreset previousSelection = selectedMasterPreset;
        RefreshPresetList();

        if (previousSelection != null && filteredPresets.Contains(previousSelection))
            SelectPreset(previousSelection);
        else
            SelectInitialPreset();

    }
    #endregion

    #region Layout
    /// <summary>
    /// Builds the persistent master-preset split view used by the main tab.
    /// </summary>
    /// <returns>Master tab content root.</returns>
    private VisualElement BuildMainContentRoot()
    {
        VisualElement panelRoot = new VisualElement();
        panelRoot.style.flexGrow = 1f;

        TwoPaneSplitView splitView = GameManagementPanelLayoutUtility.CreateHorizontalSplitView(LeftPaneWidth);
        panelRoot.Add(splitView);
        splitView.Add(BuildBrowserPane());
        splitView.Add(BuildDetailsPane());
        return panelRoot;
    }

    /// <summary>
    /// Builds the top-level tab bar shared by the master and sub-preset panels.
    /// </summary>
    /// <returns>Configured tab bar root.</returns>
    private VisualElement BuildPanelTabBar()
    {
        VisualElement newTabBar = new VisualElement();
        newTabBar.style.flexDirection = FlexDirection.Row;
        newTabBar.style.flexWrap = Wrap.Wrap;
        newTabBar.style.marginBottom = 6f;
        return newTabBar;
    }

    /// <summary>
    /// Builds the host that swaps between the master split and open sub-preset panels.
    /// </summary>
    /// <returns>Configured content host.</returns>
    private VisualElement BuildContentHost()
    {
        VisualElement host = new VisualElement();
        host.style.flexGrow = 1f;
        return host;
    }

    /// <summary>
    /// Builds the left master preset browser pane.
    /// </summary>
    /// <returns>Configured browser pane.</returns>
    private VisualElement BuildBrowserPane()
    {
        VisualElement pane = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureBrowserPane(pane);

        Label titleLabel = new Label("Transfer Presets");
        titleLabel.tooltip = "Excel transfer master presets available in the project.";
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 6f;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(titleLabel, "NashCore.ExcelDataTransfer.Master.BrowserTitle");
        pane.Add(titleLabel);
        pane.Add(BuildBrowserToolbar());

        searchField = new ToolbarSearchField();
        searchField.tooltip = "Search transfer presets by asset name or authored preset name.";
        searchField.RegisterValueChangedCallback(evt => RefreshPresetList());
        GameManagementPanelLayoutUtility.ConfigureSearchField(searchField);
        pane.Add(searchField);
        GameManagementPanelLayoutUtility.BindSearchFieldToBrowserPane(pane, searchField);

        presetListView = BuildPresetListView();
        pane.Add(presetListView);
        return pane;
    }

    /// <summary>
    /// Builds create, duplicate and ping actions for the master preset browser.
    /// </summary>
    /// <returns>Toolbar visual element.</returns>
    private Toolbar BuildBrowserToolbar()
    {
        Toolbar toolbar = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(toolbar);

        Button createButton = new Button(() => ExcelDataTransferMasterPanelOperationsUtility.CreatePreset(this));
        createButton.text = "Create";
        createButton.tooltip = "Create a new Excel transfer master preset with import, export, layout and brush presets.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(createButton, 56f);
        toolbar.Add(createButton);

        Button duplicateButton = new Button(() => ExcelDataTransferMasterPanelOperationsUtility.DuplicatePreset(this));
        duplicateButton.text = "Duplicate";
        duplicateButton.tooltip = "Duplicate the selected transfer preset graph without sharing sub-preset assets.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(duplicateButton, 76f);
        toolbar.Add(duplicateButton);

        Button pingButton = new Button(() => ExcelDataTransferMasterPanelOperationsUtility.PingSelectedPreset(this));
        pingButton.text = "Ping";
        pingButton.tooltip = "Ping the selected transfer master preset in the Project window.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(pingButton, 48f);
        toolbar.Add(pingButton);
        return toolbar;
    }

    /// <summary>
    /// Builds the right master details pane that hosts section buttons and content.
    /// </summary>
    /// <returns>Configured details pane.</returns>
    private ScrollView BuildDetailsPane()
    {
        detailsRoot = new ScrollView();
        GameManagementPanelLayoutUtility.ConfigureDetailsPane(detailsRoot);
        return detailsRoot;
    }

    /// <summary>
    /// Builds the master preset list view.
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

    /// <summary>
    /// Creates one reusable master preset row.
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
    /// Binds one master preset row.
    /// </summary>
    /// <param name="element">Row visual element.</param>
    /// <param name="index">Filtered preset index.</param>
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

        ExcelDataTransferMasterPreset preset = filteredPresets[index];
        label.text = ExcelDataTransferMasterPanelContextUtility.ResolvePresetDisplayName(preset);
        label.tooltip = AssetDatabase.GetAssetPath(preset);
    }
    #endregion

    #region Preset List
    /// <summary>
    /// Refreshes the master preset list from project assets and reapplies search filtering.
    /// </summary>
    internal void RefreshPresetList()
    {
        filteredPresets.Clear();
        List<ExcelDataTransferMasterPreset> presets = ExcelDataTransferAssetUtility.LoadMasterPresets();
        string searchText = searchField == null ? string.Empty : searchField.value;

        for (int presetIndex = 0; presetIndex < presets.Count; presetIndex++)
        {
            ExcelDataTransferMasterPreset preset = presets[presetIndex];

            if (!MatchesPresetSearch(preset, searchText))
                continue;

            filteredPresets.Add(preset);
        }

        if (presetListView != null)
            presetListView.Rebuild();
    }

    /// <summary>
    /// Selects the persisted preset, default preset, or first available filtered preset.
    /// </summary>
    private void SelectInitialPreset()
    {
        ExcelDataTransferMasterPreset persistedPreset = ExcelDataTransferAssetUtility.LoadSelectedOrDefaultMasterPreset();

        if (filteredPresets.Contains(persistedPreset))
        {
            SelectPreset(persistedPreset);
            return;
        }

        if (filteredPresets.Count > 0)
            SelectPreset(filteredPresets[0]);
        else
            SelectPreset(null);
    }

    /// <summary>
    /// Checks whether one preset matches the current sidebar search text.
    /// </summary>
    /// <param name="preset">Preset to test.</param>
    /// <param name="searchText">Search text entered by the user.</param>
    /// <returns>True when the preset should be visible.</returns>
    private bool MatchesPresetSearch(ExcelDataTransferMasterPreset preset, string searchText)
    {
        if (preset == null)
            return false;

        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        string normalizedSearch = searchText.ToLowerInvariant();
        string displayName = ExcelDataTransferMasterPanelContextUtility.ResolvePresetDisplayName(preset).ToLowerInvariant();
        return displayName.Contains(normalizedSearch) || preset.name.ToLowerInvariant().Contains(normalizedSearch);
    }
    #endregion

    #region Selection
    /// <summary>
    /// Receives preset selection from the ListView.
    /// </summary>
    /// <param name="selection">Selected ListView payload.</param>
    private void OnPresetSelectionChanged(IEnumerable<object> selection)
    {
        foreach (object selectedObject in selection)
        {
            SelectPreset(selectedObject as ExcelDataTransferMasterPreset);
            return;
        }
    }

    /// <summary>
    /// Selects one master preset and rebuilds the active local section.
    /// </summary>
    /// <param name="preset">Preset to select, or null to clear the details area.</param>
    internal void SelectPreset(ExcelDataTransferMasterPreset preset)
    {
        selectedMasterPreset = preset;
        ExcelDataTransferAssetUtility.SaveSelectedMasterPreset(selectedMasterPreset);

        if (presetListView != null && selectedMasterPreset != null)
        {
            int selectedIndex = filteredPresets.IndexOf(selectedMasterPreset);

            if (selectedIndex >= 0)
                presetListView.SetSelectionWithoutNotify(new int[] { selectedIndex });
        }

        BuildDetailsShell();
        ExcelDataTransferMasterPanelSidePanelUtility.RefreshOpenSidePanels(this);
    }

    /// <summary>
    /// Rebuilds section buttons and active master details for the selected preset.
    /// </summary>
    private void BuildDetailsShell()
    {
        detailsRoot.Clear();

        if (selectedMasterPreset == null)
        {
            detailsRoot.Add(new Label("Select or create an Excel transfer preset to edit."));
            return;
        }

        selectedMasterPreset.ValidateValues();
        VisualElement sectionButtonsRoot =
            ExcelDataTransferMasterPanelSectionUtility.BuildSectionButtons(activeDetailsSection, ActivateMasterSection);
        sectionContentRoot = new VisualElement();
        sectionContentRoot.style.flexGrow = 1f;
        detailsRoot.Add(sectionButtonsRoot);
        detailsRoot.Add(sectionContentRoot);
        BuildActiveDetailsSection();
    }
    #endregion

    #region Sections
    /// <summary>
    /// Rebuilds the currently active master details section.
    /// </summary>
    internal void BuildActiveDetailsSection()
    {
        if (sectionContentRoot == null)
            return;

        sectionContentRoot.Clear();

        switch (activeDetailsSection)
        {
            case ExcelDataTransferDetailsSectionType.SubPresets:
                BuildSubPresetsSection();
                break;
            case ExcelDataTransferDetailsSectionType.FieldCatalog:
                BuildFieldCatalogSection();
                break;
            default:
                BuildMetadataSection();
                break;
        }

        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(sectionContentRoot);
    }

    /// <summary>
    /// Builds master metadata fields for the selected transfer preset.
    /// </summary>
    private void BuildMetadataSection()
    {
        SerializedObject serializedObject = new SerializedObject(selectedMasterPreset);
        VisualElement section = ExcelDataTransferMasterPanelSectionUtility.CreateSection(sectionContentRoot, "Preset Details");
        ExcelDataTransferMasterPanelFieldUtility.AddPropertyField(this, section, serializedObject, "presetName", "Preset Name", "Readable name shown by the Excel Data Transfer Tool.", true);
        ExcelDataTransferMasterPanelFieldUtility.AddPropertyField(this, section, serializedObject, "description", "Description", "Short editor-only description of this workbook workflow.", false);
        ExcelDataTransferMasterPanelFieldUtility.AddPropertyField(this, section, serializedObject, "version", "Version", "Semantic version for the workbook layout contract.", false);
        ExcelDataTransferMasterPanelFieldUtility.AddDisabledPropertyField(section, serializedObject, "presetId", "Preset ID", "Stable identifier written into workbook metadata.");
    }

    /// <summary>
    /// Builds sub-preset object fields and opens each linked preset in its own side panel.
    /// </summary>
    private void BuildSubPresetsSection()
    {
        SerializedObject serializedObject = new SerializedObject(selectedMasterPreset);
        VisualElement section = ExcelDataTransferMasterPanelSectionUtility.CreateSection(sectionContentRoot, "Sub Presets");
        ExcelDataTransferMasterPanelSectionUtility.AddSubPresetControl(section, serializedObject, "layoutPreset", "Layout Preset", typeof(ExcelDataWorkbookLayoutPreset), ExcelDataTransferDetailsSectionType.LayoutBrush, selectedMasterPreset, OpenSubPresetPanel);
        ExcelDataTransferMasterPanelSectionUtility.AddSubPresetControl(section, serializedObject, "brushPalettePreset", "Brush Palette Preset", typeof(ExcelDataBrushPalettePreset), ExcelDataTransferDetailsSectionType.BrushPalette, selectedMasterPreset, OpenSubPresetPanel);
        ExcelDataTransferMasterPanelSectionUtility.AddSubPresetControl(section, serializedObject, "importPreset", "Import Preset", typeof(ExcelDataImportPreset), ExcelDataTransferDetailsSectionType.Import, selectedMasterPreset, OpenSubPresetPanel);
        ExcelDataTransferMasterPanelSectionUtility.AddSubPresetControl(section, serializedObject, "exportPreset", "Export Preset", typeof(ExcelDataExportPreset), ExcelDataTransferDetailsSectionType.Export, selectedMasterPreset, OpenSubPresetPanel);
    }

    /// <summary>
    /// Embeds the field catalog browser for quick source inspection.
    /// </summary>
    private void BuildFieldCatalogSection()
    {
        VisualElement section = ExcelDataTransferMasterPanelSectionUtility.CreateSection(sectionContentRoot, "Field Catalog");

        if (fieldCatalogPanel == null)
            fieldCatalogPanel = new ExcelDataFieldCatalogPanel();
        else
            fieldCatalogPanel.RefreshFromSessionChange();

        section.Add(fieldCatalogPanel.Root);
    }
    #endregion

    #region Panel Helpers
    /// <summary>
    /// Opens or activates the top-level side panel matching a linked sub-preset reference.
    /// </summary>
    /// <param name="sectionType">Linked sub-preset section selected from the master tab.</param>
    private void OpenSubPresetPanel(ExcelDataTransferDetailsSectionType sectionType)
    {
        ExcelDataTransferPanelType panelType = ResolvePanelType(sectionType);

        if (panelType == ExcelDataTransferPanelType.TransferMasterPresets)
            return;

        ExcelDataTransferMasterPanelSidePanelUtility.OpenSidePanel(this, panelType);
    }

    /// <summary>
    /// Activates a master-owned details section.
    /// </summary>
    /// <param name="sectionType">Master section to display.</param>
    private void ActivateMasterSection(ExcelDataTransferDetailsSectionType sectionType)
    {
        activeDetailsSection = sectionType;
        BuildActiveDetailsSection();
    }

    /// <summary>
    /// Refreshes master and side-panel contents after a linked sub-preset assignment.
    /// </summary>
    internal void RefreshAfterLinkedPresetAssignment()
    {
        BuildDetailsShell();
        ExcelDataTransferMasterPanelSidePanelUtility.RefreshOpenSidePanels(this);
    }

    /// <summary>
    /// Refreshes the current operation panel after import/export commands update status or selections.
    /// </summary>
    internal void RefreshAfterOperation()
    {
        BuildActiveDetailsSection();
        ExcelDataTransferMasterPanelSidePanelUtility.RefreshActiveSidePanel(this);
    }

    /// <summary>
    /// Resolves the top-level panel type represented by a master sub-preset section.
    /// </summary>
    /// <param name="sectionType">Master section type selected by the user.</param>
    /// <returns>Top-level panel type, or the master panel for unsupported sections.</returns>
    private ExcelDataTransferPanelType ResolvePanelType(ExcelDataTransferDetailsSectionType sectionType)
    {
        switch (sectionType)
        {
            case ExcelDataTransferDetailsSectionType.Import:
                return ExcelDataTransferPanelType.ImportPreset;
            case ExcelDataTransferDetailsSectionType.Export:
                return ExcelDataTransferPanelType.ExportPreset;
            case ExcelDataTransferDetailsSectionType.LayoutBrush:
                return ExcelDataTransferPanelType.WorkbookLayout;
            case ExcelDataTransferDetailsSectionType.BrushPalette:
                return ExcelDataTransferPanelType.BrushPalette;
            default:
                return ExcelDataTransferPanelType.TransferMasterPresets;
        }
    }
    #endregion

    #region Field Helpers
    /// <summary>
    /// Schedules a details refresh after the current UI event dispatch has completed.
    /// </summary>
    internal void ScheduleActiveDetailsRefresh()
    {
        if (scheduledDetailsRefresh || root == null)
            return;

        scheduledDetailsRefresh = true;
        root.schedule.Execute(() =>
        {
            scheduledDetailsRefresh = false;
            BuildActiveDetailsSection();
            ExcelDataTransferMasterPanelSidePanelUtility.RefreshActiveSidePanel(this);
        });
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Stores visuals and panel instances for one top-level Excel transfer tab.
    /// </summary>
    internal sealed class SidePanelEntry
    {
        #region Fields
        public VisualElement TabContainer;
        public Button TabButton;
        public VisualElement Content;
        public ExcelDataLinkedSubPresetPanel LinkedPresetPanel;
        public ExcelDataWorkbookLayoutPresetPanel WorkbookLayoutPanel;
        #endregion
    }
    #endregion
}
