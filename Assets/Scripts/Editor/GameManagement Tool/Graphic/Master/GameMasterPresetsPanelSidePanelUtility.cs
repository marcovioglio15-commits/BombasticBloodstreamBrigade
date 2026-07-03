using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Manages Game Management side panels, persisted tab state and cross-panel selection sync.
/// </summary>
internal static class GameMasterPresetsPanelSidePanelUtility
{
    #region Constants
    private const string ActivePanelStateKey = "NashCore.GameManagement.Master.ActivePanel";
    private const string ActiveDetailsSectionStateKey = "NashCore.GameManagement.Master.ActiveDetailsSection";
    private const string SelectedAudioPrefabPathStateKey = "NashCore.GameManagement.Master.SelectedAudioPrefabPath";
    private const string SelectedScenePrefabPathStateKey = "NashCore.GameManagement.Master.SelectedScenePrefabPath";
    private static readonly Color ActiveTabColor = new Color(0.18f, 0.18f, 0.18f, 0.6f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Restores active tab, details section and selected audio prefab from editor state.
    /// </summary>
    /// <param name="panel">Owning panel that stores persisted state.</param>
    public static void RestorePersistedState(GameMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        panel.ActivePanel = ManagementToolStateUtility.LoadEnumValue(ActivePanelStateKey, GameManagementWindow.PanelType.GameMasterPresets);
        panel.ActiveDetailsSection = ManagementToolStateUtility.LoadEnumValue(ActiveDetailsSectionStateKey, GameMasterPresetsPanel.DetailsSectionType.Metadata);
        panel.SelectedAudioPrefab = ManagementToolStateUtility.LoadGameObjectAsset(SelectedAudioPrefabPathStateKey);
        panel.SelectedScenePrefab = ManagementToolStateUtility.LoadGameObjectAsset(SelectedScenePrefabPathStateKey);
    }

    /// <summary>
    /// Builds the root tab bar, content host and initially restored side panels.
    /// </summary>
    /// <param name="panel">Owning panel that stores tab UI state.</param>
    public static void BuildPanelsContainer(GameMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement tabBar = new VisualElement();
        tabBar.style.flexDirection = FlexDirection.Row;
        tabBar.style.flexWrap = Wrap.Wrap;
        tabBar.style.paddingLeft = 6f;
        tabBar.style.paddingRight = 6f;
        tabBar.style.paddingTop = 4f;
        tabBar.style.paddingBottom = 4f;

        VisualElement contentHost = new VisualElement();
        contentHost.style.flexGrow = 1f;
        contentHost.style.flexShrink = 1f;
        contentHost.style.minWidth = 0f;

        panel.TabBar = tabBar;
        panel.ContentHost = contentHost;
        panel.Root.Add(tabBar);
        panel.Root.Add(contentHost);

        // The intended active panel may be reassigned by the restore calls below, so capture it up
        // front and re-apply at the end.
        GameManagementWindow.PanelType intendedActivePanel = panel.ActivePanel;

        panel.SuppressStateWrite = true;
        AddTab(panel, GameManagementWindow.PanelType.GameMasterPresets, "Game Master Presets", panel.MainContentRoot, null, null, null, null);

        // Isolate per-panel failures: one panel that throws while constructing must not stop the
        // master tab from displaying.
        if (intendedActivePanel == GameManagementWindow.PanelType.AudioManager)
            TryOpenSidePanelSafe(panel, GameManagementWindow.PanelType.AudioManager);

        if (intendedActivePanel == GameManagementWindow.PanelType.SceneManager)
            TryOpenSidePanelSafe(panel, GameManagementWindow.PanelType.SceneManager);

        if (intendedActivePanel == GameManagementWindow.PanelType.SettingsManager)
            TryOpenSidePanelSafe(panel, GameManagementWindow.PanelType.SettingsManager);

        if (intendedActivePanel == GameManagementWindow.PanelType.HudManager)
            TryOpenSidePanelSafe(panel, GameManagementWindow.PanelType.HudManager);

        if (!panel.SidePanels.ContainsKey(intendedActivePanel))
            intendedActivePanel = GameManagementWindow.PanelType.GameMasterPresets;

        panel.ActivePanel = intendedActivePanel;
        SetActivePanel(panel, intendedActivePanel);
        panel.SuppressStateWrite = false;
        ManagementToolStateUtility.SaveEnumValue(ActivePanelStateKey, panel.ActivePanel);
    }

    /// <summary>
    /// Calls OpenSidePanel inside a try/catch so a transient asset failure for one side panel does
    /// not prevent the rest of the master tab from coming up.
    /// </summary>
    /// <param name="panel">Owning panel with tab state.</param>
    /// <param name="panelType">Side panel type to open.</param>
    private static void TryOpenSidePanelSafe(GameMasterPresetsPanel panel, GameManagementWindow.PanelType panelType)
    {
        try
        {
            OpenSidePanel(panel, panelType);
        }
        catch (System.Exception sidePanelException)
        {
            UnityEngine.Debug.LogException(sidePanelException);
        }
    }

    /// <summary>
    /// Opens or activates a side panel and synchronizes it with the selected master preset.
    /// </summary>
    /// <param name="panel">Owning panel that stores side panel entries.</param>
    /// <param name="panelType">Panel type to open or activate.</param>
    public static void OpenSidePanel(GameMasterPresetsPanel panel, GameManagementWindow.PanelType panelType)
    {
        if (panel == null)
            return;

        // Side panels no longer auto-route to the master preset's referenced sub-preset; they keep
        // whatever the user last navigated to (restored from their own persisted selection key).
        if (panel.SidePanels.ContainsKey(panelType))
        {
            SetActivePanel(panel, panelType);
            return;
        }

        switch (panelType)
        {
            case GameManagementWindow.PanelType.AudioManager:
                OpenAudioManagerPanel(panel, panelType);
                break;
            case GameManagementWindow.PanelType.SceneManager:
                OpenSceneManagerPanel(panel, panelType);
                break;
            case GameManagementWindow.PanelType.SettingsManager:
                OpenSettingsManagerPanel(panel, panelType);
                break;
            case GameManagementWindow.PanelType.HudManager:
                OpenHudManagerPanel(panel, panelType);
                break;
            default:
                return;
        }

        SetActivePanel(panel, panelType);
    }

    /// <summary>
    /// Refreshes every open side panel after session changes. Side panels keep their own persisted
    /// selection and are no longer rerouted to the master preset's referenced sub-preset.
    /// </summary>
    /// <param name="panel">Owning panel with opened side panel controllers.</param>
    public static void RefreshOpenSidePanels(GameMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        foreach (KeyValuePair<GameManagementWindow.PanelType, GameMasterPresetsPanel.SidePanelEntry> panelEntry in panel.SidePanels)
        {
            GameMasterPresetsPanel.SidePanelEntry entry = panelEntry.Value;

            if (entry == null)
                continue;

            if (entry.AudioPanel != null)
                entry.AudioPanel.RefreshFromSessionChange();

            if (entry.ScenePanel != null)
                entry.ScenePanel.RefreshFromSessionChange();

            if (entry.SettingsPanel != null)
                entry.SettingsPanel.RefreshFromSessionChange();

            if (entry.HudPanel != null)
                entry.HudPanel.RefreshFromSessionChange();
        }
    }

    /// <summary>
    /// Persists the active details section.
    /// </summary>
    /// <param name="panel">Owning panel that stores the active section.</param>
    public static void SaveActiveDetailsSection(GameMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        ManagementToolStateUtility.SaveEnumValue(ActiveDetailsSectionStateKey, panel.ActiveDetailsSection);
    }

    /// <summary>
    /// Persists the selected audio manager prefab reference.
    /// </summary>
    /// <param name="panel">Owning panel that stores selected prefab state.</param>
    public static void SaveSelectedAudioPrefabState(GameMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        ManagementToolStateUtility.SaveAssetPath(SelectedAudioPrefabPathStateKey, panel.SelectedAudioPrefab);
    }

    /// <summary>
    /// Persists the selected scene manager prefab reference.
    /// </summary>
    /// <param name="panel">Owning panel that stores selected prefab state.</param>
    public static void SaveSelectedScenePrefabState(GameMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        ManagementToolStateUtility.SaveAssetPath(SelectedScenePrefabPathStateKey, panel.SelectedScenePrefab);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates and registers the Audio Manager side panel.
    /// </summary>
    /// <param name="panel">Owning panel with tab state.</param>
    /// <param name="panelType">Side panel type.</param>
    private static void OpenAudioManagerPanel(GameMasterPresetsPanel panel, GameManagementWindow.PanelType panelType)
    {
        GameAudioManagerPresetsPanel audioPanel = new GameAudioManagerPresetsPanel();
        VisualElement panelRoot = BuildSidePanelRoot(panel, "Audio Manager", audioPanel.Root, panelType);
        AddTab(panel, panelType, "Audio Manager", panelRoot, audioPanel, null, null, null);
    }

    /// <summary>
    /// Creates and registers the Scene Manager side panel.
    /// </summary>
    /// <param name="panel">Owning panel with tab state.</param>
    /// <param name="panelType">Side panel type.</param>
    private static void OpenSceneManagerPanel(GameMasterPresetsPanel panel, GameManagementWindow.PanelType panelType)
    {
        GameSceneManagerPresetsPanel scenePanel = new GameSceneManagerPresetsPanel();
        VisualElement panelRoot = BuildSidePanelRoot(panel, "Scene Manager", scenePanel.Root, panelType);
        AddTab(panel, panelType, "Scene Manager", panelRoot, null, scenePanel, null, null);
    }

    /// <summary>
    /// Creates and registers the Settings Manager side panel.
    /// </summary>
    /// <param name="panel">Owning panel with tab state.</param>
    /// <param name="panelType">Side panel type.</param>
    private static void OpenSettingsManagerPanel(GameMasterPresetsPanel panel, GameManagementWindow.PanelType panelType)
    {
        GameSettingsManagerPresetsPanel settingsPanel = new GameSettingsManagerPresetsPanel();
        VisualElement panelRoot = BuildSidePanelRoot(panel, "Settings Manager", settingsPanel.Root, panelType);
        AddTab(panel, panelType, "Settings Manager", panelRoot, null, null, settingsPanel, null);
    }

    /// <summary>
    /// Creates and registers the HUD Manager side panel.
    /// </summary>
    /// <param name="panel">Owning panel with tab state.</param>
    /// <param name="panelType">Side panel type.</param>
    private static void OpenHudManagerPanel(GameMasterPresetsPanel panel, GameManagementWindow.PanelType panelType)
    {
        GameHudManagerPresetsPanel hudPanel = new GameHudManagerPresetsPanel();
        VisualElement panelRoot = BuildSidePanelRoot(panel, "HUD Manager", hudPanel.Root, panelType);
        AddTab(panel, panelType, "HUD Manager", panelRoot, null, null, null, hudPanel);
    }

    /// <summary>
    /// Creates the side-panel root with a title and close button.
    /// </summary>
    /// <param name="panel">Owning panel used by the close callback.</param>
    /// <param name="title">Panel title.</param>
    /// <param name="content">Inner panel content.</param>
    /// <param name="panelType">Panel type represented by this root.</param>
    /// <returns>Side-panel root element.</returns>
    private static VisualElement BuildSidePanelRoot(GameMasterPresetsPanel panel, string title, VisualElement content, GameManagementWindow.PanelType panelType)
    {
        VisualElement panelRoot = new VisualElement();
        panelRoot.style.flexGrow = 1f;
        panelRoot.style.flexShrink = 1f;
        panelRoot.style.minWidth = 0f;

        VisualElement header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;

        Label label = new Label(title);
        label.tooltip = "Open Game Management section: " + title + ".";
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.Add(label);

        Button closeButton = new Button(() => CloseSidePanel(panel, panelType));
        closeButton.text = "X";
        closeButton.tooltip = "Close this section.";
        header.Add(closeButton);

        panelRoot.Add(header);
        content.style.flexGrow = 1f;
        content.style.flexShrink = 1f;
        content.style.minWidth = 0f;
        panelRoot.Add(content);
        return panelRoot;
    }

    /// <summary>
    /// Adds one tab entry to the tab host.
    /// </summary>
    /// <param name="panel">Owning panel with tab bar and content host.</param>
    /// <param name="panelType">Panel type represented by the tab.</param>
    /// <param name="label">Tab label.</param>
    /// <param name="content">Content shown when active.</param>
    /// <param name="audioPanel">Optional Audio Manager panel controller.</param>
    /// <param name="scenePanel">Optional Scene Manager panel controller.</param>
    /// <param name="settingsPanel">Optional Settings Manager panel controller.</param>
    /// <param name="hudPanel">Optional HUD Manager panel controller.</param>
    private static void AddTab(GameMasterPresetsPanel panel,
                               GameManagementWindow.PanelType panelType,
                               string label,
                               VisualElement content,
                               GameAudioManagerPresetsPanel audioPanel,
                               GameSceneManagerPresetsPanel scenePanel,
                               GameSettingsManagerPresetsPanel settingsPanel,
                               GameHudManagerPresetsPanel hudPanel)
    {
        VisualElement tabContainer = new VisualElement();
        tabContainer.style.flexDirection = FlexDirection.Row;
        tabContainer.style.marginRight = 6f;

        Button tabButton = new Button(() => SetActivePanel(panel, panelType));
        tabButton.text = label;
        tabButton.tooltip = "Show " + label + ".";
        tabButton.style.flexShrink = 1f;
        tabButton.style.minWidth = 0f;
        tabContainer.Add(tabButton);
        panel.TabBar.Add(tabContainer);

        panel.SidePanels[panelType] = new GameMasterPresetsPanel.SidePanelEntry
        {
            TabContainer = tabContainer,
            TabButton = tabButton,
            Content = content,
            AudioPanel = audioPanel,
            ScenePanel = scenePanel,
            SettingsPanel = settingsPanel,
            HudPanel = hudPanel
        };
    }

    /// <summary>
    /// Activates one panel tab and swaps content.
    /// </summary>
    /// <param name="panel">Owning panel with tab state.</param>
    /// <param name="panelType">Panel type to activate.</param>
    private static void SetActivePanel(GameMasterPresetsPanel panel, GameManagementWindow.PanelType panelType)
    {
        if (!panel.SidePanels.TryGetValue(panelType, out GameMasterPresetsPanel.SidePanelEntry entry))
            return;

        panel.ActivePanel = panelType;

        if (!panel.SuppressStateWrite)
            ManagementToolStateUtility.SaveEnumValue(ActivePanelStateKey, panel.ActivePanel);

        panel.ContentHost.Clear();
        panel.ContentHost.Add(entry.Content);
        UpdateTabStyles(panel);
        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(panel.ContentHost);
    }

    /// <summary>
    /// Closes one side panel while keeping the master panel available.
    /// </summary>
    /// <param name="panel">Owning panel with tab state.</param>
    /// <param name="panelType">Panel type to close.</param>
    private static void CloseSidePanel(GameMasterPresetsPanel panel, GameManagementWindow.PanelType panelType)
    {
        if (panel == null || panelType == GameManagementWindow.PanelType.GameMasterPresets)
            return;

        if (!panel.SidePanels.TryGetValue(panelType, out GameMasterPresetsPanel.SidePanelEntry entry))
            return;

        if (entry.TabContainer != null)
            entry.TabContainer.RemoveFromHierarchy();

        panel.SidePanels.Remove(panelType);

        if (panel.ActivePanel == panelType)
            SetActivePanel(panel, GameManagementWindow.PanelType.GameMasterPresets);
    }

    /// <summary>
    /// Updates tab button styling to highlight the active tab.
    /// </summary>
    /// <param name="panel">Owning panel with side panel entries.</param>
    private static void UpdateTabStyles(GameMasterPresetsPanel panel)
    {
        foreach (KeyValuePair<GameManagementWindow.PanelType, GameMasterPresetsPanel.SidePanelEntry> entry in panel.SidePanels)
        {
            bool isActive = entry.Key == panel.ActivePanel;
            entry.Value.TabButton.style.unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal;
            entry.Value.TabButton.style.backgroundColor = isActive ? ActiveTabColor : Color.clear;
        }
    }
    #endregion

    #endregion
}
