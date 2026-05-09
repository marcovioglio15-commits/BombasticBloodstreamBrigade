using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Manages Game Management side panels, persisted tab state and cross-panel selection sync.
/// /params None.
/// /returns None.
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
    /// /params panel Owning panel that stores persisted state.
    /// /returns None.
    /// </summary>
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
    /// /params panel Owning panel that stores tab UI state.
    /// /returns None.
    /// </summary>
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

        panel.SuppressStateWrite = true;
        AddTab(panel, GameManagementWindow.PanelType.GameMasterPresets, "Game Master Presets", panel.MainContentRoot, null, null);

        if (panel.ActivePanel == GameManagementWindow.PanelType.AudioManager)
            OpenSidePanel(panel, GameManagementWindow.PanelType.AudioManager);

        if (panel.ActivePanel == GameManagementWindow.PanelType.SceneManager)
            OpenSidePanel(panel, GameManagementWindow.PanelType.SceneManager);

        if (!panel.SidePanels.ContainsKey(panel.ActivePanel))
            panel.ActivePanel = GameManagementWindow.PanelType.GameMasterPresets;

        SetActivePanel(panel, panel.ActivePanel);
        panel.SuppressStateWrite = false;
        ManagementToolStateUtility.SaveEnumValue(ActivePanelStateKey, panel.ActivePanel);
    }

    /// <summary>
    /// Opens or activates a side panel and synchronizes it with the selected master preset.
    /// /params panel Owning panel that stores side panel entries.
    /// /params panelType Panel type to open or activate.
    /// /returns None.
    /// </summary>
    public static void OpenSidePanel(GameMasterPresetsPanel panel, GameManagementWindow.PanelType panelType)
    {
        if (panel == null)
            return;

        if (panel.SidePanels.ContainsKey(panelType))
        {
            SetActivePanel(panel, panelType);
            SyncSidePanelSelection(panel, panelType, panel.SidePanels[panelType]);
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
            default:
                return;
        }

        SetActivePanel(panel, panelType);
        SyncSidePanelSelection(panel, panelType, panel.SidePanels[panelType]);
    }

    /// <summary>
    /// Refreshes every open side panel after session changes.
    /// /params panel Owning panel with opened side panel controllers.
    /// /returns None.
    /// </summary>
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
        }

        SyncOpenSidePanels(panel);
    }

    /// <summary>
    /// Synchronizes all open side panel selections with the selected master preset references.
    /// /params panel Owning panel with selected master preset context.
    /// /returns None.
    /// </summary>
    public static void SyncOpenSidePanels(GameMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        foreach (KeyValuePair<GameManagementWindow.PanelType, GameMasterPresetsPanel.SidePanelEntry> entry in panel.SidePanels)
            SyncSidePanelSelection(panel, entry.Key, entry.Value);
    }

    /// <summary>
    /// Persists the active details section.
    /// /params panel Owning panel that stores the active section.
    /// /returns None.
    /// </summary>
    public static void SaveActiveDetailsSection(GameMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        ManagementToolStateUtility.SaveEnumValue(ActiveDetailsSectionStateKey, panel.ActiveDetailsSection);
    }

    /// <summary>
    /// Persists the selected audio manager prefab reference.
    /// /params panel Owning panel that stores selected prefab state.
    /// /returns None.
    /// </summary>
    public static void SaveSelectedAudioPrefabState(GameMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        ManagementToolStateUtility.SaveAssetPath(SelectedAudioPrefabPathStateKey, panel.SelectedAudioPrefab);
    }

    /// <summary>
    /// Persists the selected scene manager prefab reference.
    /// /params panel Owning panel that stores selected prefab state.
    /// /returns None.
    /// </summary>
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
    /// /params panel Owning panel with tab state.
    /// /params panelType Side panel type.
    /// /returns None.
    /// </summary>
    private static void OpenAudioManagerPanel(GameMasterPresetsPanel panel, GameManagementWindow.PanelType panelType)
    {
        GameAudioManagerPresetsPanel audioPanel = new GameAudioManagerPresetsPanel();
        VisualElement panelRoot = BuildSidePanelRoot(panel, "Audio Manager", audioPanel.Root, panelType);
        AddTab(panel, panelType, "Audio Manager", panelRoot, audioPanel, null);
    }

    /// <summary>
    /// Creates and registers the Scene Manager side panel.
    /// /params panel Owning panel with tab state.
    /// /params panelType Side panel type.
    /// /returns None.
    /// </summary>
    private static void OpenSceneManagerPanel(GameMasterPresetsPanel panel, GameManagementWindow.PanelType panelType)
    {
        GameSceneManagerPresetsPanel scenePanel = new GameSceneManagerPresetsPanel();
        VisualElement panelRoot = BuildSidePanelRoot(panel, "Scene Manager", scenePanel.Root, panelType);
        AddTab(panel, panelType, "Scene Manager", panelRoot, null, scenePanel);
    }

    /// <summary>
    /// Creates the side-panel root with a title and close button.
    /// /params panel Owning panel used by the close callback.
    /// /params title Panel title.
    /// /params content Inner panel content.
    /// /params panelType Panel type represented by this root.
    /// /returns Side-panel root element.
    /// </summary>
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
    /// /params panel Owning panel with tab bar and content host.
    /// /params panelType Panel type represented by the tab.
    /// /params label Tab label.
    /// /params content Content shown when active.
    /// /params audioPanel Optional Audio Manager panel controller.
    /// /params scenePanel Optional Scene Manager panel controller.
    /// /returns None.
    /// </summary>
    private static void AddTab(GameMasterPresetsPanel panel,
                               GameManagementWindow.PanelType panelType,
                               string label,
                               VisualElement content,
                               GameAudioManagerPresetsPanel audioPanel,
                               GameSceneManagerPresetsPanel scenePanel)
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
            ScenePanel = scenePanel
        };
    }

    /// <summary>
    /// Activates one panel tab and swaps content.
    /// /params panel Owning panel with tab state.
    /// /params panelType Panel type to activate.
    /// /returns None.
    /// </summary>
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
    /// /params panel Owning panel with tab state.
    /// /params panelType Panel type to close.
    /// /returns None.
    /// </summary>
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
    /// Synchronizes one side panel selection with the selected master preset.
    /// /params panel Owning panel with selected master preset context.
    /// /params panelType Side panel type.
    /// /params entry Side panel entry.
    /// /returns None.
    /// </summary>
    private static void SyncSidePanelSelection(GameMasterPresetsPanel panel,
                                               GameManagementWindow.PanelType panelType,
                                               GameMasterPresetsPanel.SidePanelEntry entry)
    {
        if (panel.SelectedPreset == null || entry == null)
            return;

        if (panelType == GameManagementWindow.PanelType.AudioManager &&
            entry.AudioPanel != null &&
            panel.SelectedPreset.AudioManagerPreset != null)
        {
            entry.AudioPanel.SelectPresetFromExternal(panel.SelectedPreset.AudioManagerPreset);
            return;
        }

        if (panelType == GameManagementWindow.PanelType.SceneManager &&
            entry.ScenePanel != null &&
            panel.SelectedPreset.SceneManagerPreset != null)
        {
            entry.ScenePanel.SelectPresetFromExternal(panel.SelectedPreset.SceneManagerPreset);
        }
    }

    /// <summary>
    /// Updates tab button styling to highlight the active tab.
    /// /params panel Owning panel with side panel entries.
    /// /returns None.
    /// </summary>
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
