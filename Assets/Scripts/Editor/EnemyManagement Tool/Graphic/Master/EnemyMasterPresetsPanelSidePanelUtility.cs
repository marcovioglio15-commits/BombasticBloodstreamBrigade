using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Manages side panels, tab state persistence and cross-panel selection syncing for enemy master preset panels.
/// </summary>
internal static class EnemyMasterPresetsPanelSidePanelUtility
{
    #region Constants
    private const string ActivePanelStateKey = "NashCore.EnemyManagement.Master.ActivePanel";
    private const string OpenPanelsStateKey = "NashCore.EnemyManagement.Master.OpenPanels";
    private const string ActiveDetailsSectionStateKey = "NashCore.EnemyManagement.Master.ActiveDetailsSection";
    private const string SelectedPrefabPathStateKey = "NashCore.EnemyManagement.Master.SelectedPrefabPath";
    internal const string SelectedPresetPathStateKey = "NashCore.EnemyManagement.Master.SelectedPresetPath";
    internal const string DetailsScrollOffsetStateKey = "NashCore.EnemyManagement.Master.DetailsScroll";
    private static readonly Color ActiveTabColor = new Color(0.18f, 0.18f, 0.18f, 0.6f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Opens one side panel and synchronizes its selection with the currently selected master preset.
    /// </summary>
    /// <param name="panel">Owning panel that stores side panel entries and active selection state.</param>
    /// <param name="panelType">Panel type to open or activate.</param>

    public static void OpenSidePanel(EnemyMasterPresetsPanel panel, EnemyManagementWindow.PanelType panelType)
    {
        if (panel == null)
            return;

        // Side panels no longer auto-route to the master preset's referenced sub-preset; they keep
        // whatever the user last navigated to (restored from their own persisted selection key).
        if (panel.SidePanels.ContainsKey(panelType))
        {
            EnemyMasterPresetsPanel.SidePanelEntry existingEntry = panel.SidePanels[panelType];

            if (existingEntry != null)
                SetActivePanel(panel, panelType);

            return;
        }

        EnemyBrainPresetsPanel brainPanel;
        EnemyVisualPresetsPanel visualPanel;
        EnemyUiVisualPresetsPanel uiVisualPanel;
        EnemyAdvancedPatternPresetsPanel advancedPatternPanel;
        EnemyBossPatternPresetsPanel bossPatternPanel;
        VisualElement content = BuildSidePanelContent(panel,
                                                      panelType,
                                                      out brainPanel,
                                                      out visualPanel,
                                                      out uiVisualPanel,
                                                      out advancedPatternPanel,
                                                      out bossPatternPanel);

        if (content == null)
            return;

        AddTab(panel, panelType, GetPanelTitle(panelType), content, brainPanel, visualPanel, uiVisualPanel, advancedPatternPanel, bossPatternPanel);
        SetActivePanel(panel, panelType);
    }

    /// <summary>
    /// Closes one opened side panel and preserves the root panel as always available.
    /// </summary>
    /// <param name="panel">Owning panel that stores side panel entries and active selection state.</param>
    /// <param name="panelType">Panel type to close.</param>

    public static void CloseSidePanel(EnemyMasterPresetsPanel panel, EnemyManagementWindow.PanelType panelType)
    {
        if (panel == null)
            return;

        if (panelType == EnemyManagementWindow.PanelType.EnemyMasterPresets)
            return;

        EnemyMasterPresetsPanel.SidePanelEntry entry;

        if (!panel.SidePanels.TryGetValue(panelType, out entry))
            return;

        if (entry != null && entry.TabContainer != null)
            entry.TabContainer.RemoveFromHierarchy();

        panel.SidePanels.Remove(panelType);
        SaveOpenPanelsState(panel);

        if (panel.ActivePanel == panelType)
            SetActivePanel(panel, EnemyManagementWindow.PanelType.EnemyMasterPresets);
    }

    /// <summary>
    /// Resolves the visible title for one panel type.
    /// </summary>
    /// <param name="panelType">Panel type to resolve.</param>
    /// <returns>Returns the display title used by tab headers.</returns>
    public static string GetPanelTitle(EnemyManagementWindow.PanelType panelType)
    {
        switch (panelType)
        {
            case EnemyManagementWindow.PanelType.EnemyBrainPresets:
                return "Brain Presets";
            case EnemyManagementWindow.PanelType.EnemyVisualPresets:
                return "Gameplay Visual Presets";
            case EnemyManagementWindow.PanelType.EnemyUiVisualPresets:
                return "UI Visual Presets";
            case EnemyManagementWindow.PanelType.EnemyAdvancedPatternPresets:
                return "Advanced Pattern Presets";
            case EnemyManagementWindow.PanelType.EnemyBossPatternPresets:
                return "Boss Pattern Presets";
            default:
                return "Master Presets";
        }
    }

    /// <summary>
    /// Builds the root tab bar and content host, restores persisted side panels and activates the last active tab.
    /// </summary>
    /// <param name="panel">Owning panel that stores tab UI state.</param>

    public static void BuildPanelsContainer(EnemyMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement tabBar = new VisualElement();
        tabBar.style.flexDirection = FlexDirection.Row;
        tabBar.style.flexWrap = Wrap.Wrap;
        tabBar.style.marginBottom = 4f;
        tabBar.style.paddingLeft = 6f;
        tabBar.style.paddingRight = 6f;
        tabBar.style.paddingTop = 4f;
        tabBar.style.paddingBottom = 4f;

        VisualElement contentHost = new VisualElement();
        contentHost.style.flexGrow = 1f;

        panel.TabBar = tabBar;
        panel.ContentHost = contentHost;
        panel.Root.Add(tabBar);
        panel.Root.Add(contentHost);

        // The intended active panel may be reassigned by RestoreOpenSidePanels (each restored side
        // panel calls SetActivePanel), so capture it up front and re-apply at the end.
        EnemyManagementWindow.PanelType intendedActivePanel = panel.ActivePanel;

        panel.SuppressStateWrite = true;
        AddTab(panel,
               EnemyManagementWindow.PanelType.EnemyMasterPresets,
               "Master Presets",
               panel.MainContentRoot,
               null,
               null,
               null,
               null,
               null);
        RestoreOpenSidePanels(panel);

        if (!panel.SidePanels.ContainsKey(intendedActivePanel))
            intendedActivePanel = EnemyManagementWindow.PanelType.EnemyMasterPresets;

        panel.ActivePanel = intendedActivePanel;
        SetActivePanel(panel, intendedActivePanel);
        panel.SuppressStateWrite = false;
        // Do NOT re-save OpenPanelsStateKey here. The persisted set was the source of truth for
        // restoration; if a transient failure dropped a panel, we want the original set preserved
        // so the next reopen can retry that panel cleanly. The user-driven AddTab/CloseSidePanel
        // calls are the only legitimate writers to that key.
        ManagementToolStateUtility.SaveEnumValue(ActivePanelStateKey, panel.ActivePanel);
    }

    /// <summary>
    /// Builds one side panel content root and returns specialized child panel controllers when applicable.
    /// </summary>
    /// <param name="panel">Owning panel used for close callbacks.</param>
    /// <param name="panelType">Panel type to build.</param>
    /// <param name="brainPanel">Resolved brain panel controller when built.</param>
    /// <param name="visualPanel">Resolved gameplay visual panel controller when built.</param>
    /// <param name="uiVisualPanel">Resolved UI visual panel controller when built.</param>
    /// <param name="advancedPatternPanel">Resolved advanced pattern panel controller when built.</param>
    /// <param name="bossPatternPanel">Resolved boss pattern panel controller when built.</param>
    /// <returns>Returns the panel root shown in the content host.</returns>
    public static VisualElement BuildSidePanelContent(EnemyMasterPresetsPanel panel,
                                                      EnemyManagementWindow.PanelType panelType,
                                                      out EnemyBrainPresetsPanel brainPanel,
                                                      out EnemyVisualPresetsPanel visualPanel,
                                                      out EnemyUiVisualPresetsPanel uiVisualPanel,
                                                      out EnemyAdvancedPatternPresetsPanel advancedPatternPanel,
                                                      out EnemyBossPatternPresetsPanel bossPatternPanel)
    {
        brainPanel = null;
        visualPanel = null;
        uiVisualPanel = null;
        advancedPatternPanel = null;
        bossPatternPanel = null;

        VisualElement panelRoot = new VisualElement();
        panelRoot.style.flexDirection = FlexDirection.Column;
        panelRoot.style.flexGrow = 1f;

        VisualElement headerRow = new VisualElement();
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.justifyContent = Justify.SpaceBetween;
        headerRow.style.alignItems = Align.Center;
        headerRow.style.marginBottom = 4f;

        Label title = new Label(GetPanelTitle(panelType));
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(title, "NashCore.EnemyManagement.SidePanel." + GetPanelTitle(panelType));
        headerRow.Add(title);

        Button closeButton = new Button(() => CloseSidePanel(panel, panelType));
        closeButton.text = "X";
        closeButton.tooltip = "Close this section.";
        headerRow.Add(closeButton);

        panelRoot.Add(headerRow);

        if (panelType == EnemyManagementWindow.PanelType.EnemyBrainPresets)
        {
            brainPanel = new EnemyBrainPresetsPanel();
            panelRoot.Add(brainPanel.Root);
            return panelRoot;
        }

        if (panelType == EnemyManagementWindow.PanelType.EnemyVisualPresets)
        {
            visualPanel = new EnemyVisualPresetsPanel();
            panelRoot.Add(visualPanel.Root);
            return panelRoot;
        }

        if (panelType == EnemyManagementWindow.PanelType.EnemyUiVisualPresets)
        {
            uiVisualPanel = new EnemyUiVisualPresetsPanel();
            panelRoot.Add(uiVisualPanel.Root);
            return panelRoot;
        }

        if (panelType == EnemyManagementWindow.PanelType.EnemyAdvancedPatternPresets)
        {
            advancedPatternPanel = new EnemyAdvancedPatternPresetsPanel();
            panelRoot.Add(advancedPatternPanel.Root);
            return panelRoot;
        }

        if (panelType == EnemyManagementWindow.PanelType.EnemyBossPatternPresets)
        {
            bossPatternPanel = new EnemyBossPatternPresetsPanel();
            panelRoot.Add(bossPatternPanel.Root);
            return panelRoot;
        }

        VisualElement placeholder = new VisualElement();
        placeholder.style.flexGrow = 1f;
        placeholder.style.minHeight = 220f;
        placeholder.style.justifyContent = Justify.Center;
        placeholder.style.alignItems = Align.Center;
        Label placeholderLabel = new Label("Section not implemented yet.");
        placeholderLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        placeholder.Add(placeholderLabel);
        panelRoot.Add(placeholder);
        return panelRoot;
    }

    /// <summary>
    /// Adds one panel tab entry and persists the updated open panel set.
    /// </summary>
    /// <param name="panel">Owning panel that stores side panel entries.</param>
    /// <param name="panelType">Panel type represented by the new tab.</param>
    /// <param name="label">Tab label.</param>
    /// <param name="content">Content root shown when the tab is active.</param>
    /// <param name="brainPanel">Optional brain panel controller.</param>
    /// <param name="visualPanel">Optional gameplay visual panel controller.</param>
    /// <param name="uiVisualPanel">Optional UI visual panel controller.</param>
    /// <param name="advancedPatternPanel">Optional advanced pattern panel controller.</param>
    /// <param name="bossPatternPanel">Optional boss pattern panel controller.</param>

    public static void AddTab(EnemyMasterPresetsPanel panel,
                              EnemyManagementWindow.PanelType panelType,
                              string label,
                              VisualElement content,
                              EnemyBrainPresetsPanel brainPanel,
                              EnemyVisualPresetsPanel visualPanel,
                              EnemyUiVisualPresetsPanel uiVisualPanel,
                              EnemyAdvancedPatternPresetsPanel advancedPatternPanel,
                              EnemyBossPatternPresetsPanel bossPatternPanel)
    {
        if (panel == null)
            return;

        if (panel.TabBar == null)
            return;

        VisualElement tabContainer = new VisualElement();
        tabContainer.style.flexDirection = FlexDirection.Row;
        tabContainer.style.alignItems = Align.Center;
        tabContainer.style.marginRight = 6f;

        Button tabButton = new Button(() => SetActivePanel(panel, panelType));
        tabButton.text = label;
        tabButton.style.unityTextAlign = TextAnchor.MiddleLeft;
        tabContainer.Add(tabButton);

        panel.TabBar.Add(tabContainer);

        EnemyMasterPresetsPanel.SidePanelEntry sidePanelEntry = new EnemyMasterPresetsPanel.SidePanelEntry();
        sidePanelEntry.TabContainer = tabContainer;
        sidePanelEntry.TabButton = tabButton;
        sidePanelEntry.Content = content;
        sidePanelEntry.BrainPanel = brainPanel;
        sidePanelEntry.VisualPanel = visualPanel;
        sidePanelEntry.UiVisualPanel = uiVisualPanel;
        sidePanelEntry.AdvancedPatternPanel = advancedPatternPanel;
        sidePanelEntry.BossPatternPanel = bossPatternPanel;
        panel.SidePanels[panelType] = sidePanelEntry;

        SaveOpenPanelsState(panel);
    }

    /// <summary>
    /// Refreshes all open side panels after library/session changes. Side panels keep their own
    /// persisted selection and are no longer rerouted to the master preset's referenced sub-preset.
    /// </summary>
    /// <param name="panel">Owning panel that stores side panel entries.</param>

    public static void RefreshOpenSidePanels(EnemyMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        foreach (KeyValuePair<EnemyManagementWindow.PanelType, EnemyMasterPresetsPanel.SidePanelEntry> sidePanelEntry in panel.SidePanels)
        {
            EnemyMasterPresetsPanel.SidePanelEntry entry = sidePanelEntry.Value;

            if (entry == null)
                continue;

            if (entry.BrainPanel != null)
                entry.BrainPanel.RefreshFromSessionChange();

            if (entry.VisualPanel != null)
                entry.VisualPanel.RefreshFromSessionChange();

            if (entry.UiVisualPanel != null)
                entry.UiVisualPanel.RefreshFromSessionChange();

            if (entry.AdvancedPatternPanel != null)
                entry.AdvancedPatternPanel.RefreshFromSessionChange();

            if (entry.BossPatternPanel != null)
                entry.BossPatternPanel.RefreshFromSessionChange();
        }
    }

    /// <summary>
    /// Activates one panel tab, swaps visible content and persists the active panel state.
    /// </summary>
    /// <param name="panel">Owning panel that stores tab state and content host.</param>
    /// <param name="panelType">Panel type to activate.</param>

    public static void SetActivePanel(EnemyMasterPresetsPanel panel, EnemyManagementWindow.PanelType panelType)
    {
        if (panel == null)
            return;

        EnemyMasterPresetsPanel.SidePanelEntry entry;

        if (!panel.SidePanels.TryGetValue(panelType, out entry))
            return;

        if (panel.ContentHost == null)
            return;

        panel.ActivePanel = panelType;

        if (!panel.SuppressStateWrite)
            ManagementToolStateUtility.SaveEnumValue(ActivePanelStateKey, panel.ActivePanel);

        panel.ContentHost.Clear();
        panel.ContentHost.Add(entry.Content);
        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(panel.ContentHost);
        UpdateTabStyles(panel);
    }

    /// <summary>
    /// Refreshes tab button styles to reflect the currently active panel.
    /// </summary>
    /// <param name="panel">Owning panel that stores side panel entries and active panel state.</param>

    public static void UpdateTabStyles(EnemyMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        foreach (KeyValuePair<EnemyManagementWindow.PanelType, EnemyMasterPresetsPanel.SidePanelEntry> sidePanelEntry in panel.SidePanels)
        {
            if (sidePanelEntry.Value == null || sidePanelEntry.Value.TabButton == null)
                continue;

            bool isActive = sidePanelEntry.Key == panel.ActivePanel;
            sidePanelEntry.Value.TabButton.style.unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal;
            sidePanelEntry.Value.TabButton.style.backgroundColor = isActive ? ActiveTabColor : Color.clear;
        }
    }

    /// <summary>
    /// Restores persisted panel, detail section and selected prefab state.
    /// </summary>
    /// <param name="panel">Owning panel that receives restored state.</param>

    public static void RestorePersistedState(EnemyMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        panel.ActivePanel = ManagementToolStateUtility.LoadEnumValue(ActivePanelStateKey,
                                                                     EnemyManagementWindow.PanelType.EnemyMasterPresets);
        panel.ActiveDetailsSection = ManagementToolStateUtility.LoadEnumValue(ActiveDetailsSectionStateKey,
                                                                              EnemyMasterPresetsPanel.DetailsSectionType.Metadata);
        panel.SelectedEnemyPrefab = ManagementToolStateUtility.LoadGameObjectAsset(SelectedPrefabPathStateKey);
    }

    /// <summary>
    /// Reopens all side panels that were persisted as open in the previous session.
    /// </summary>
    /// <param name="panel">Owning panel that receives the restored tabs.</param>

    public static void RestoreOpenSidePanels(EnemyMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        List<EnemyManagementWindow.PanelType> openPanels = ManagementToolStateUtility.LoadEnumList<EnemyManagementWindow.PanelType>(OpenPanelsStateKey);

        for (int index = 0; index < openPanels.Count; index++)
        {
            EnemyManagementWindow.PanelType openPanel = openPanels[index];

            if (openPanel == EnemyManagementWindow.PanelType.EnemyMasterPresets)
                continue;

            // Isolate per-panel failures: one panel that throws while constructing must not stop
            // the rest of the persisted tabs from coming back.
            try
            {
                OpenSidePanel(panel, openPanel);
            }
            catch (System.Exception sidePanelException)
            {
                UnityEngine.Debug.LogException(sidePanelException);
            }
        }
    }

    /// <summary>
    /// Persists the current set of open panels in deterministic order.
    /// </summary>
    /// <param name="panel">Owning panel that stores side panel entries.</param>

    public static void SaveOpenPanelsState(EnemyMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        if (panel.SuppressStateWrite)
            return;

        List<EnemyManagementWindow.PanelType> openPanels = new List<EnemyManagementWindow.PanelType>();
        openPanels.Add(EnemyManagementWindow.PanelType.EnemyMasterPresets);

        if (panel.SidePanels.ContainsKey(EnemyManagementWindow.PanelType.EnemyBrainPresets))
            openPanels.Add(EnemyManagementWindow.PanelType.EnemyBrainPresets);

        if (panel.SidePanels.ContainsKey(EnemyManagementWindow.PanelType.EnemyVisualPresets))
            openPanels.Add(EnemyManagementWindow.PanelType.EnemyVisualPresets);

        if (panel.SidePanels.ContainsKey(EnemyManagementWindow.PanelType.EnemyUiVisualPresets))
            openPanels.Add(EnemyManagementWindow.PanelType.EnemyUiVisualPresets);

        if (panel.SidePanels.ContainsKey(EnemyManagementWindow.PanelType.EnemyAdvancedPatternPresets))
            openPanels.Add(EnemyManagementWindow.PanelType.EnemyAdvancedPatternPresets);

        if (panel.SidePanels.ContainsKey(EnemyManagementWindow.PanelType.EnemyBossPatternPresets))
            openPanels.Add(EnemyManagementWindow.PanelType.EnemyBossPatternPresets);

        ManagementToolStateUtility.SaveEnumList(OpenPanelsStateKey, openPanels);
    }

    /// <summary>
    /// Persists the currently selected enemy prefab asset path.
    /// </summary>
    /// <param name="panel">Owning panel that provides the selected prefab reference.</param>

    public static void SaveSelectedPrefabState(EnemyMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        ManagementToolStateUtility.SaveAssetPath(SelectedPrefabPathStateKey, panel.SelectedEnemyPrefab);
    }

    /// <summary>
    /// Persists the currently selected enemy master preset reference so the panel can re-select it on reopen.
    /// </summary>
    /// <param name="panel">Owning panel that stores selected preset state.</param>
    public static void SaveSelectedPresetState(EnemyMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        ManagementToolStateUtility.SaveAssetPath(SelectedPresetPathStateKey, panel.SelectedPreset);
    }

    /// <summary>
    /// Loads the last persisted enemy master preset reference, or null when the asset no longer exists.
    /// </summary>
    /// <returns>Returns the resolved master preset asset, or null.</returns>
    public static EnemyMasterPreset LoadPersistedSelectedPreset()
    {
        return ManagementToolStateUtility.LoadAsset<EnemyMasterPreset>(SelectedPresetPathStateKey);
    }

    #endregion

    #endregion
}
