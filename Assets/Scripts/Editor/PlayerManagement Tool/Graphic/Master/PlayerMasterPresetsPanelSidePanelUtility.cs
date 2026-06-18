using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Manages side panels, persisted tab state and cross-panel selection syncing for player master preset panels.
/// </summary>
internal static class PlayerMasterPresetsPanelSidePanelUtility
{
    #region Constants
    private const string ActivePanelStateKey = "NashCore.PlayerManagement.Master.ActivePanel";
    private const string OpenPanelsStateKey = "NashCore.PlayerManagement.Master.OpenPanels";
    internal const string ActiveDetailsSectionStateKey = "NashCore.PlayerManagement.Master.ActiveDetailsSection";
    private const string SelectedPrefabPathStateKey = "NashCore.PlayerManagement.Master.SelectedPrefabPath";
    internal const string SelectedPresetPathStateKey = "NashCore.PlayerManagement.Master.SelectedPresetPath";
    internal const string DetailsScrollOffsetStateKey = "NashCore.PlayerManagement.Master.DetailsScroll";
    private static readonly Color ActiveTabColor = new Color(0.18f, 0.18f, 0.18f, 0.6f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Opens one side panel and synchronizes its selection with the currently selected master preset.
    /// </summary>
    /// <param name="panel">Owning panel that stores side panel entries and active selection state.</param>
    /// <param name="panelType">Panel type to open or activate.</param>

    public static void OpenSidePanel(PlayerMasterPresetsPanel panel, PlayerManagementWindow.PanelType panelType)
    {
        if (panel == null)
            return;

        // Side panels no longer auto-route to the master preset's referenced sub-preset; they keep
        // whatever the user last navigated to (restored from their own persisted selection key).
        if (panel.SidePanels.ContainsKey(panelType))
        {
            PlayerMasterPresetsPanel.SidePanelEntry existingEntry = panel.SidePanels[panelType];

            if (existingEntry != null)
                SetActivePanel(panel, panelType);

            return;
        }

        PlayerControllerPresetsPanel controllerPanel;
        PlayerProgressionPresetsPanel progressionPanel;
        PlayerPowerUpsPresetsPanel powerUpsPanel;
        PlayerVisualPresetsPanel visualPanel;
        PlayerAnimationBindingsPresetsPanel animationPanel;
        VisualElement content = BuildSidePanelContent(panel,
                                                      panelType,
                                                      out controllerPanel,
                                                      out progressionPanel,
                                                      out powerUpsPanel,
                                                      out visualPanel,
                                                      out animationPanel);

        if (content == null)
            return;

        AddTab(panel, panelType, GetPanelTitle(panelType), content, controllerPanel, progressionPanel, powerUpsPanel, visualPanel, animationPanel);
        SetActivePanel(panel, panelType);
    }

    /// <summary>
    /// Builds the root tab bar and content host, restores persisted side panels and activates the last active tab.
    /// </summary>
    /// <param name="panel">Owning panel that stores tab UI state.</param>

    public static void BuildPanelsContainer(PlayerMasterPresetsPanel panel)
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
        PlayerManagementWindow.PanelType intendedActivePanel = panel.ActivePanel;

        panel.SuppressStateWrite = true;
        AddTab(panel,
               PlayerManagementWindow.PanelType.PlayerMasterPresets,
               "Player Master Presets",
               panel.MainContentRoot,
               null,
               null,
               null,
               null,
               null);
        RestoreOpenSidePanels(panel);

        if (!panel.SidePanels.ContainsKey(intendedActivePanel))
            intendedActivePanel = PlayerManagementWindow.PanelType.PlayerMasterPresets;

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
    /// Refreshes all opened side panels after external asset changes. Side panels keep their own
    /// persisted selection and are no longer rerouted to the master preset's referenced sub-preset.
    /// </summary>
    /// <param name="panel">Owning panel that stores opened side panel controllers.</param>

    public static void RefreshOpenSidePanels(PlayerMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        // Refresh-only: rebind each side panel to its live draft session state. The master preset no
        // longer drives the selected sub-preset of each side panel; persisted side-panel selections
        // are restored independently when the panel reopens.
        foreach (KeyValuePair<PlayerManagementWindow.PanelType, PlayerMasterPresetsPanel.SidePanelEntry> panelEntry in panel.SidePanels)
        {
            PlayerMasterPresetsPanel.SidePanelEntry entry = panelEntry.Value;

            if (entry == null)
                continue;

            if (entry.ControllerPanel != null)
                entry.ControllerPanel.RefreshFromSessionChange();

            if (entry.ProgressionPanel != null)
                entry.ProgressionPanel.RefreshFromSessionChange();

            if (entry.PowerUpsPanel != null)
                entry.PowerUpsPanel.RefreshFromSessionChange();

            if (entry.VisualPanel != null)
                entry.VisualPanel.RefreshFromSessionChange();

            if (entry.AnimationPanel != null)
                entry.AnimationPanel.RefreshFromSessionChange();
        }

        PlayerManagementSelectionContext.NotifyVisualPresetContentChanged();
    }

    /// <summary>
    /// Restores active tab, detail section and selected prefab from persisted editor state.
    /// </summary>
    /// <param name="panel">Owning panel that stores persisted state.</param>

    public static void RestorePersistedState(PlayerMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        panel.ActivePanel = ManagementToolStateUtility.LoadEnumValue(ActivePanelStateKey, PlayerManagementWindow.PanelType.PlayerMasterPresets);
        panel.ActiveDetailsSection = ManagementToolStateUtility.LoadEnumValue(ActiveDetailsSectionStateKey, PlayerMasterPresetsPanel.DetailsSectionType.Metadata);
        panel.SelectedPlayerPrefab = ManagementToolStateUtility.LoadGameObjectAsset(SelectedPrefabPathStateKey);
    }

    /// <summary>
    /// Persists the currently selected player prefab reference.
    /// </summary>
    /// <param name="panel">Owning panel that stores selected prefab state.</param>

    public static void SaveSelectedPrefabState(PlayerMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        ManagementToolStateUtility.SaveAssetPath(SelectedPrefabPathStateKey, panel.SelectedPlayerPrefab);
    }

    /// <summary>
    /// Persists the currently selected master preset reference so the panel can re-select it on reopen.
    /// </summary>
    /// <param name="panel">Owning panel that stores selected preset state.</param>
    public static void SaveSelectedPresetState(PlayerMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        ManagementToolStateUtility.SaveAssetPath(SelectedPresetPathStateKey, panel.SelectedPreset);
    }

    /// <summary>
    /// Loads the last persisted master preset reference, or null when the asset no longer exists.
    /// </summary>
    /// <returns>Returns the resolved master preset asset, or null.</returns>
    public static PlayerMasterPreset LoadPersistedSelectedPreset()
    {
        return ManagementToolStateUtility.LoadAsset<PlayerMasterPreset>(SelectedPresetPathStateKey);
    }

    #endregion

    #region Private Methods
    /// <summary>
    /// Closes one opened side panel and preserves the root panel as always available.
    /// </summary>
    /// <param name="panel">Owning panel that stores side panel entries and active selection state.</param>
    /// <param name="panelType">Panel type to close.</param>

    private static void CloseSidePanel(PlayerMasterPresetsPanel panel, PlayerManagementWindow.PanelType panelType)
    {
        if (panel == null || panelType == PlayerManagementWindow.PanelType.PlayerMasterPresets)
            return;

        PlayerMasterPresetsPanel.SidePanelEntry entry;

        if (!panel.SidePanels.TryGetValue(panelType, out entry))
            return;

        if (entry != null && entry.TabContainer != null)
            entry.TabContainer.RemoveFromHierarchy();

        panel.SidePanels.Remove(panelType);
        SaveOpenPanelsState(panel);

        if (panel.ActivePanel == panelType)
            SetActivePanel(panel, PlayerManagementWindow.PanelType.PlayerMasterPresets);
    }

    /// <summary>
    /// Resolves the visible title for one panel type.
    /// </summary>
    /// <param name="panelType">Panel type to resolve.</param>
    /// <returns>Returns the display title used by tab headers.</returns>
    private static string GetPanelTitle(PlayerManagementWindow.PanelType panelType)
    {
        switch (panelType)
        {
            case PlayerManagementWindow.PanelType.PlayerControllerPresets:
                return "Player Controller Presets";
            case PlayerManagementWindow.PanelType.LevelUpProgression:
                return "Level-Up & Progression";
            case PlayerManagementWindow.PanelType.PowerUps:
                return "Power-Ups";
            case PlayerManagementWindow.PanelType.PlayerVisualPresets:
                return "Visual Presets";
            case PlayerManagementWindow.PanelType.AnimationBindings:
                return "Animation Bindings";
            default:
                return "Player Master Presets";
        }
    }

    /// <summary>
    /// Builds one side panel content root and returns specialized child panel controllers when applicable.
    /// </summary>
    /// <param name="panel">Owning panel used for close callbacks.</param>
    /// <param name="panelType">Panel type to build.</param>
    /// <param name="controllerPanel">Resolved controller panel controller when built.</param>
    /// <param name="progressionPanel">Resolved progression panel controller when built.</param>
    /// <param name="powerUpsPanel">Resolved power-ups panel controller when built.</param>
    /// <param name="animationPanel">Resolved animation panel controller when built.</param>
    /// <returns>Returns the panel root shown in the content host.</returns>
    private static VisualElement BuildSidePanelContent(PlayerMasterPresetsPanel panel,
                                                       PlayerManagementWindow.PanelType panelType,
                                                       out PlayerControllerPresetsPanel controllerPanel,
                                                       out PlayerProgressionPresetsPanel progressionPanel,
                                                       out PlayerPowerUpsPresetsPanel powerUpsPanel,
                                                       out PlayerVisualPresetsPanel visualPanel,
                                                       out PlayerAnimationBindingsPresetsPanel animationPanel)
    {
        controllerPanel = null;
        progressionPanel = null;
        powerUpsPanel = null;
        visualPanel = null;
        animationPanel = null;

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
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(title, "NashCore.PlayerManagement.SidePanel." + GetPanelTitle(panelType));
        headerRow.Add(title);

        Button closeButton = new Button(() => CloseSidePanel(panel, panelType));
        closeButton.text = "X";
        closeButton.tooltip = "Close this section.";
        headerRow.Add(closeButton);

        panelRoot.Add(headerRow);

        switch (panelType)
        {
            case PlayerManagementWindow.PanelType.PlayerControllerPresets:
                controllerPanel = new PlayerControllerPresetsPanel();
                panelRoot.Add(controllerPanel.Root);
                return panelRoot;
            case PlayerManagementWindow.PanelType.LevelUpProgression:
                progressionPanel = new PlayerProgressionPresetsPanel();
                panelRoot.Add(progressionPanel.Root);
                return panelRoot;
            case PlayerManagementWindow.PanelType.PowerUps:
                powerUpsPanel = new PlayerPowerUpsPresetsPanel();
                panelRoot.Add(powerUpsPanel.Root);
                return panelRoot;
            case PlayerManagementWindow.PanelType.PlayerVisualPresets:
                visualPanel = new PlayerVisualPresetsPanel();
                panelRoot.Add(visualPanel.Root);
                return panelRoot;
            case PlayerManagementWindow.PanelType.AnimationBindings:
                animationPanel = new PlayerAnimationBindingsPresetsPanel();
                panelRoot.Add(animationPanel.Root);
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
    /// <param name="controllerPanel">Optional controller panel controller.</param>
    /// <param name="progressionPanel">Optional progression panel controller.</param>
    /// <param name="powerUpsPanel">Optional power-ups panel controller.</param>
    /// <param name="animationPanel">Optional animation panel controller.</param>

    private static void AddTab(PlayerMasterPresetsPanel panel,
                               PlayerManagementWindow.PanelType panelType,
                               string label,
                               VisualElement content,
                               PlayerControllerPresetsPanel controllerPanel,
                               PlayerProgressionPresetsPanel progressionPanel,
                               PlayerPowerUpsPresetsPanel powerUpsPanel,
                               PlayerVisualPresetsPanel visualPanel,
                               PlayerAnimationBindingsPresetsPanel animationPanel)
    {
        if (panel == null || panel.TabBar == null)
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

        PlayerMasterPresetsPanel.SidePanelEntry sidePanelEntry = new PlayerMasterPresetsPanel.SidePanelEntry();
        sidePanelEntry.TabContainer = tabContainer;
        sidePanelEntry.TabButton = tabButton;
        sidePanelEntry.Content = content;
        sidePanelEntry.ControllerPanel = controllerPanel;
        sidePanelEntry.ProgressionPanel = progressionPanel;
        sidePanelEntry.PowerUpsPanel = powerUpsPanel;
        sidePanelEntry.VisualPanel = visualPanel;
        sidePanelEntry.AnimationPanel = animationPanel;
        panel.SidePanels[panelType] = sidePanelEntry;

        SaveOpenPanelsState(panel);
    }

    /// <summary>
    /// Activates one panel tab, persists selection and swaps visible content.
    /// </summary>
    /// <param name="panel">Owning panel that stores active tab state.</param>
    /// <param name="panelType">Panel type to activate.</param>

    private static void SetActivePanel(PlayerMasterPresetsPanel panel, PlayerManagementWindow.PanelType panelType)
    {
        if (panel == null)
            return;

        PlayerMasterPresetsPanel.SidePanelEntry entry;

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
    /// Updates tab button styles so the active panel is visually highlighted.
    /// </summary>
    /// <param name="panel">Owning panel that stores side panel entries.</param>

    private static void UpdateTabStyles(PlayerMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        foreach (KeyValuePair<PlayerManagementWindow.PanelType, PlayerMasterPresetsPanel.SidePanelEntry> entry in panel.SidePanels)
        {
            if (entry.Value == null || entry.Value.TabButton == null)
                continue;

            bool isActive = entry.Key == panel.ActivePanel;
            entry.Value.TabButton.style.unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal;
            entry.Value.TabButton.style.backgroundColor = isActive ? ActiveTabColor : Color.clear;
        }
    }

    /// <summary>
    /// Reopens side panels that were open in the previous editor session.
    /// </summary>
    /// <param name="panel">Owning panel that stores side panel state.</param>

    private static void RestoreOpenSidePanels(PlayerMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        List<PlayerManagementWindow.PanelType> openPanels = ManagementToolStateUtility.LoadEnumList<PlayerManagementWindow.PanelType>(OpenPanelsStateKey);

        for (int index = 0; index < openPanels.Count; index++)
        {
            PlayerManagementWindow.PanelType openPanel = openPanels[index];

            if (openPanel == PlayerManagementWindow.PanelType.PlayerMasterPresets)
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
    /// Persists the set of currently opened tabs to editor state.
    /// </summary>
    /// <param name="panel">Owning panel that stores side panel state.</param>

    private static void SaveOpenPanelsState(PlayerMasterPresetsPanel panel)
    {
        if (panel == null || panel.SuppressStateWrite)
            return;

        List<PlayerManagementWindow.PanelType> openPanels = new List<PlayerManagementWindow.PanelType>();
        openPanels.Add(PlayerManagementWindow.PanelType.PlayerMasterPresets);

        if (panel.SidePanels.ContainsKey(PlayerManagementWindow.PanelType.PlayerControllerPresets))
            openPanels.Add(PlayerManagementWindow.PanelType.PlayerControllerPresets);

        if (panel.SidePanels.ContainsKey(PlayerManagementWindow.PanelType.LevelUpProgression))
            openPanels.Add(PlayerManagementWindow.PanelType.LevelUpProgression);

        if (panel.SidePanels.ContainsKey(PlayerManagementWindow.PanelType.PowerUps))
            openPanels.Add(PlayerManagementWindow.PanelType.PowerUps);

        if (panel.SidePanels.ContainsKey(PlayerManagementWindow.PanelType.PlayerVisualPresets))
            openPanels.Add(PlayerManagementWindow.PanelType.PlayerVisualPresets);

        if (panel.SidePanels.ContainsKey(PlayerManagementWindow.PanelType.AnimationBindings))
            openPanels.Add(PlayerManagementWindow.PanelType.AnimationBindings);

        ManagementToolStateUtility.SaveEnumList(OpenPanelsStateKey, openPanels);
    }
    #endregion

    #endregion
}
