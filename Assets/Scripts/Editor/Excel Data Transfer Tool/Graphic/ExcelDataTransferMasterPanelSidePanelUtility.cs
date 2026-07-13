using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Manages top-level Excel transfer tabs so master and sub-preset panels use the same structure as the other management tools.
/// </summary>
internal static class ExcelDataTransferMasterPanelSidePanelUtility
{
    #region Constants
    private static readonly Color ActiveTabColor = new Color(0.18f, 0.18f, 0.18f, 0.6f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Initializes the tab container with the persistent master preset tab.
    /// </summary>
    /// <param name="panel">Owning Excel transfer master panel.</param>
    public static void BuildPanelsContainer(ExcelDataTransferMasterPanel panel)
    {
        if (panel == null || panel.TabBar == null || panel.ContentHost == null)
            return;

        panel.TabBar.Clear();
        panel.ContentHost.Clear();
        panel.SidePanels.Clear();
        AddMasterPanel(panel);
        SetActivePanel(panel, ExcelDataTransferPanelType.TransferMasterPresets);
    }

    /// <summary>
    /// Opens or activates a top-level sub-preset panel.
    /// </summary>
    /// <param name="panel">Owning Excel transfer master panel.</param>
    /// <param name="panelType">Panel type to open.</param>
    public static void OpenSidePanel(ExcelDataTransferMasterPanel panel, ExcelDataTransferPanelType panelType)
    {
        if (panel == null)
            return;

        if (panelType == ExcelDataTransferPanelType.TransferMasterPresets)
        {
            SetActivePanel(panel, panelType);
            return;
        }

        if (!panel.SidePanels.ContainsKey(panelType))
            AddSubPresetPanel(panel, panelType);

        RefreshPanelContent(panel, panelType);
        SetActivePanel(panel, panelType);
    }

    /// <summary>
    /// Refreshes every open sub-preset panel after master selection or draft state changes.
    /// </summary>
    /// <param name="panel">Owning Excel transfer master panel.</param>
    public static void RefreshOpenSidePanels(ExcelDataTransferMasterPanel panel)
    {
        if (panel == null)
            return;

        foreach (ExcelDataTransferPanelType panelType in panel.SidePanels.Keys)
            RefreshPanelContent(panel, panelType);
    }

    /// <summary>
    /// Refreshes the currently active sub-preset panel when an operation changes visible status.
    /// </summary>
    /// <param name="panel">Owning Excel transfer master panel.</param>
    public static void RefreshActiveSidePanel(ExcelDataTransferMasterPanel panel)
    {
        if (panel == null)
            return;

        RefreshPanelContent(panel, panel.ActivePanel);
    }
    #endregion

    #region Panel Building
    /// <summary>
    /// Adds the persistent master preset tab and content entry.
    /// </summary>
    /// <param name="panel">Owning Excel transfer master panel.</param>
    private static void AddMasterPanel(ExcelDataTransferMasterPanel panel)
    {
        ExcelDataTransferMasterPanel.SidePanelEntry entry = new ExcelDataTransferMasterPanel.SidePanelEntry();
        entry.Content = panel.MainContentRoot;
        CreateTab(panel, entry, ExcelDataTransferPanelType.TransferMasterPresets, "Transfer Master Presets", false);
        panel.SidePanels.Add(ExcelDataTransferPanelType.TransferMasterPresets, entry);
    }

    /// <summary>
    /// Adds a closeable sub-preset panel with a browser split or layout brush content.
    /// </summary>
    /// <param name="panel">Owning Excel transfer master panel.</param>
    /// <param name="panelType">Sub-preset panel type.</param>
    private static void AddSubPresetPanel(ExcelDataTransferMasterPanel panel, ExcelDataTransferPanelType panelType)
    {
        ExcelDataTransferMasterPanel.SidePanelEntry entry = new ExcelDataTransferMasterPanel.SidePanelEntry();
        entry.Content = new VisualElement();
        entry.Content.style.flexGrow = 1f;

        if (panelType == ExcelDataTransferPanelType.WorkbookLayout)
        {
            entry.WorkbookLayoutPanel = new ExcelDataWorkbookLayoutPresetPanel(panel);
            entry.Content.Add(entry.WorkbookLayoutPanel.Root);
        }
        else
        {
            entry.LinkedPresetPanel = new ExcelDataLinkedSubPresetPanel(panel, panelType);
            entry.Content.Add(entry.LinkedPresetPanel.Root);
        }

        CreateTab(panel, entry, panelType, ResolvePanelTitle(panelType), true);
        panel.SidePanels.Add(panelType, entry);
    }

    /// <summary>
    /// Creates one top-level tab button and optional close button.
    /// </summary>
    /// <param name="panel">Owning Excel transfer master panel.</param>
    /// <param name="entry">Panel entry receiving tab references.</param>
    /// <param name="panelType">Panel represented by the tab.</param>
    /// <param name="label">Visible tab label.</param>
    /// <param name="allowClose">True when the tab can be closed.</param>
    private static void CreateTab(ExcelDataTransferMasterPanel panel,
                                  ExcelDataTransferMasterPanel.SidePanelEntry entry,
                                  ExcelDataTransferPanelType panelType,
                                  string label,
                                  bool allowClose)
    {
        VisualElement tabContainer = new VisualElement();
        tabContainer.style.flexDirection = FlexDirection.Row;
        tabContainer.style.alignItems = Align.Center;
        tabContainer.style.marginRight = 6f;
        tabContainer.style.marginBottom = 4f;

        Button tabButton = new Button(() => SetActivePanel(panel, panelType));
        tabButton.text = label;
        tabButton.tooltip = "Open the " + label + " panel.";
        tabButton.style.unityTextAlign = TextAnchor.MiddleLeft;
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(tabButton, ResolveTabWidth(panelType));
        tabContainer.Add(tabButton);

        if (allowClose)
            AddCloseButton(panel, tabContainer, panelType);

        entry.TabContainer = tabContainer;
        entry.TabButton = tabButton;
        panel.TabBar.Add(tabContainer);
    }

    /// <summary>
    /// Adds a close button to a top-level sub-preset tab.
    /// </summary>
    /// <param name="panel">Owning Excel transfer master panel.</param>
    /// <param name="tabContainer">Tab container receiving the button.</param>
    /// <param name="panelType">Panel closed by the button.</param>
    private static void AddCloseButton(ExcelDataTransferMasterPanel panel,
                                       VisualElement tabContainer,
                                       ExcelDataTransferPanelType panelType)
    {
        Button closeButton = new Button(() => CloseSidePanel(panel, panelType));
        closeButton.text = "X";
        closeButton.tooltip = "Close this sub-preset panel.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(closeButton, 24f);
        tabContainer.Add(closeButton);
    }
    #endregion

    #region Activation
    /// <summary>
    /// Switches the visible content host to the requested panel.
    /// </summary>
    /// <param name="panel">Owning Excel transfer master panel.</param>
    /// <param name="panelType">Panel to show.</param>
    private static void SetActivePanel(ExcelDataTransferMasterPanel panel, ExcelDataTransferPanelType panelType)
    {
        if (panel == null || !panel.SidePanels.ContainsKey(panelType))
            return;

        panel.ActivePanel = panelType;
        panel.ContentHost.Clear();
        panel.ContentHost.Add(panel.SidePanels[panelType].Content);
        UpdateTabStyles(panel);
    }

    /// <summary>
    /// Closes one sub-preset panel and returns to the master panel when needed.
    /// </summary>
    /// <param name="panel">Owning Excel transfer master panel.</param>
    /// <param name="panelType">Panel to close.</param>
    private static void CloseSidePanel(ExcelDataTransferMasterPanel panel, ExcelDataTransferPanelType panelType)
    {
        if (panel == null || panelType == ExcelDataTransferPanelType.TransferMasterPresets)
            return;

        if (!panel.SidePanels.ContainsKey(panelType))
            return;

        ExcelDataTransferMasterPanel.SidePanelEntry entry = panel.SidePanels[panelType];

        if (entry.TabContainer != null)
            panel.TabBar.Remove(entry.TabContainer);

        panel.SidePanels.Remove(panelType);

        if (panel.ActivePanel == panelType)
            SetActivePanel(panel, ExcelDataTransferPanelType.TransferMasterPresets);
        else
            UpdateTabStyles(panel);
    }

    /// <summary>
    /// Updates active/inactive tab visuals.
    /// </summary>
    /// <param name="panel">Owning Excel transfer master panel.</param>
    private static void UpdateTabStyles(ExcelDataTransferMasterPanel panel)
    {
        foreach (KeyValuePair<ExcelDataTransferPanelType, ExcelDataTransferMasterPanel.SidePanelEntry> pair in panel.SidePanels)
        {
            Button tabButton = pair.Value.TabButton;

            if (tabButton == null)
                continue;

            bool active = pair.Key == panel.ActivePanel;
            tabButton.style.backgroundColor = active ? ActiveTabColor : Color.clear;
            tabButton.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
        }
    }
    #endregion

    #region Refresh
    /// <summary>
    /// Refreshes the content instance owned by one open panel.
    /// </summary>
    /// <param name="panel">Owning Excel transfer master panel.</param>
    /// <param name="panelType">Panel whose content should refresh.</param>
    private static void RefreshPanelContent(ExcelDataTransferMasterPanel panel, ExcelDataTransferPanelType panelType)
    {
        if (panel == null || !panel.SidePanels.ContainsKey(panelType))
            return;

        ExcelDataTransferMasterPanel.SidePanelEntry entry = panel.SidePanels[panelType];

        if (entry.WorkbookLayoutPanel != null)
            entry.WorkbookLayoutPanel.RefreshFromSessionChange();

        if (entry.LinkedPresetPanel != null)
            entry.LinkedPresetPanel.RefreshFromSessionChange();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the visible title for a top-level panel.
    /// </summary>
    /// <param name="panelType">Panel type to label.</param>
    /// <returns>Visible panel title.</returns>
    private static string ResolvePanelTitle(ExcelDataTransferPanelType panelType)
    {
        switch (panelType)
        {
            case ExcelDataTransferPanelType.ImportPreset:
                return "Import Presets";
            case ExcelDataTransferPanelType.ExportPreset:
                return "Export Presets";
            case ExcelDataTransferPanelType.WorkbookLayout:
                return "Workbook Layout";
            case ExcelDataTransferPanelType.BrushPalette:
                return "Brush Palettes";
            default:
                return "Transfer Master Presets";
        }
    }

    /// <summary>
    /// Resolves a stable tab width for the requested panel.
    /// </summary>
    /// <param name="panelType">Panel type represented by the tab.</param>
    /// <returns>Button width in pixels.</returns>
    private static float ResolveTabWidth(ExcelDataTransferPanelType panelType)
    {
        switch (panelType)
        {
            case ExcelDataTransferPanelType.TransferMasterPresets:
                return 156f;
            case ExcelDataTransferPanelType.WorkbookLayout:
                return 128f;
            case ExcelDataTransferPanelType.BrushPalette:
                return 112f;
            default:
                return 104f;
        }
    }
    #endregion

    #endregion
}
