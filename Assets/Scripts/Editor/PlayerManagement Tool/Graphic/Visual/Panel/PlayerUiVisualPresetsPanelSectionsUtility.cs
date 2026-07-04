using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds metadata and UI-only visual subsections for player UI visual preset panels.
/// </summary>
internal static class PlayerUiVisualPresetsPanelSectionsUtility
{
    #region Constants
    private static readonly Color ActiveTabColor = new Color(0.18f, 0.18f, 0.18f, 0.6f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds metadata fields for the selected player UI visual preset.
    /// </summary>
    /// <param name="panel">Owning UI visual panel.</param>
    public static void BuildMetadataSection(PlayerUiVisualPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = CreateDetailsSectionContainer(panel, "Preset Details");

        if (sectionContainer == null)
            return;

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;

        if (presetSerializedObject == null)
            return;

        SerializedProperty idProperty = presetSerializedObject.FindProperty("presetId");
        SerializedProperty nameProperty = presetSerializedObject.FindProperty("presetName");
        SerializedProperty descriptionProperty = presetSerializedObject.FindProperty("description");
        SerializedProperty versionProperty = presetSerializedObject.FindProperty("version");

        TextField nameField = new TextField("Preset Name");
        nameField.isDelayed = true;
        nameField.BindProperty(nameProperty);
        nameField.RegisterValueChangedCallback(evt =>
        {
            panel.HandlePresetNameChanged(evt.newValue);
        });
        sectionContainer.Add(nameField);

        TextField versionField = new TextField("Version");
        versionField.isDelayed = true;
        versionField.BindProperty(versionProperty);
        versionField.RegisterValueChangedCallback(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        });
        sectionContainer.Add(versionField);

        TextField descriptionField = new TextField("Description");
        descriptionField.multiline = true;
        descriptionField.isDelayed = true;
        descriptionField.style.height = 60f;
        descriptionField.BindProperty(descriptionProperty);
        descriptionField.RegisterValueChangedCallback(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        });
        sectionContainer.Add(descriptionField);

        VisualElement idRow = new VisualElement();
        idRow.style.flexDirection = FlexDirection.Row;
        idRow.style.alignItems = Align.Center;

        TextField idField = new TextField("Preset ID");
        idField.isReadOnly = true;
        idField.SetEnabled(false);
        idField.style.flexGrow = 1f;
        idField.BindProperty(idProperty);
        idRow.Add(idField);

        Button regenerateButton = new Button(panel.RegeneratePresetId);
        regenerateButton.text = "Regenerate";
        regenerateButton.style.marginLeft = 6f;
        idRow.Add(regenerateButton);
        sectionContainer.Add(idRow);
    }

    /// <summary>
    /// Builds the UI visual tab container and all supported UI subsections.
    /// </summary>
    /// <param name="panel">Owning UI visual panel.</param>
    public static void BuildVisualSection(PlayerUiVisualPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = CreateDetailsSectionContainer(panel, "UI Visual");

        if (sectionContainer == null)
            return;

        panel.UiVisualSubSectionTabs.Clear();

        VisualElement tabBar = new VisualElement();
        tabBar.style.flexDirection = FlexDirection.Row;
        tabBar.style.flexWrap = Wrap.Wrap;
        tabBar.style.marginBottom = 6f;
        tabBar.style.paddingTop = 4f;
        tabBar.style.paddingBottom = 4f;
        tabBar.style.paddingLeft = 2f;
        panel.UiVisualSubSectionTabBar = tabBar;

        VisualElement contentHost = new VisualElement();
        contentHost.style.flexDirection = FlexDirection.Column;
        contentHost.style.flexGrow = 1f;
        panel.UiVisualSubSectionContentHost = contentHost;

        sectionContainer.Add(tabBar);
        sectionContainer.Add(contentHost);

        AddUiVisualSubSectionTab(panel,
                                 PlayerUiVisualPresetsPanel.UiVisualSubSectionType.HealthBars,
                                 "Health Bars",
                                 () => PlayerVisualPresetsPanelHealthBarsSectionUtility.Build(panel));
        AddUiVisualSubSectionTab(panel,
                                 PlayerUiVisualPresetsPanel.UiVisualSubSectionType.ActivePowerUpHud,
                                 "Active HUD",
                                 () => PlayerVisualPresetsPanelActivePowerUpHudSectionUtility.Build(panel));
        AddUiVisualSubSectionTab(panel,
                                 PlayerUiVisualPresetsPanel.UiVisualSubSectionType.Portrait,
                                 "Portrait",
                                 () => PlayerVisualPresetsPanelPortraitSectionUtility.Build(panel));
        AddUiVisualSubSectionTab(panel,
                                 PlayerUiVisualPresetsPanel.UiVisualSubSectionType.GrowthSequence,
                                 "Growth Sequence",
                                 () => PlayerVisualPresetsPanelGrowthSequenceSectionUtility.Build(panel));

        if (!panel.UiVisualSubSectionTabs.ContainsKey(panel.ActiveUiVisualSubSection))
            panel.ActiveUiVisualSubSection = PlayerUiVisualPresetsPanel.UiVisualSubSectionType.HealthBars;

        panel.SetActiveUiVisualSubSection(panel.ActiveUiVisualSubSection);
    }

    /// <summary>
    /// Shows the active UI visual subsection and refreshes tab styles.
    /// </summary>
    /// <param name="panel">Owning UI visual panel.</param>
    public static void ShowActiveUiVisualSubSection(PlayerUiVisualPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement contentHost = panel.UiVisualSubSectionContentHost;

        if (contentHost == null)
            return;

        PlayerUiVisualPresetsPanel.UiVisualSubSectionTabEntry tabEntry;

        if (!panel.UiVisualSubSectionTabs.TryGetValue(panel.ActiveUiVisualSubSection, out tabEntry))
            return;

        if (tabEntry == null)
            return;

        if (tabEntry.Content == null && tabEntry.ContentFactory != null)
            tabEntry.Content = tabEntry.ContentFactory.Invoke();

        if (tabEntry.Content == null)
            return;

        contentHost.Clear();
        contentHost.Add(tabEntry.Content);
        UpdateUiVisualSubSectionTabStyles(panel);
        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(contentHost);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates one details section container with a themed header.
    /// </summary>
    /// <param name="panel">Owning UI visual panel.</param>
    /// <param name="sectionTitle">Visible section title.</param>
    /// <returns>Created section container, or null when the panel is not ready.</returns>
    private static VisualElement CreateDetailsSectionContainer(PlayerUiVisualPresetsPanel panel, string sectionTitle)
    {
        if (panel == null)
            return null;

        VisualElement detailsSectionContentRoot = panel.DetailsSectionContentRoot;

        if (detailsSectionContentRoot == null)
            return null;

        VisualElement container = new VisualElement();
        container.style.marginTop = 8f;

        Label header = new Label(sectionTitle);
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(header, "NashCore.PlayerManagement.UiVisual.Section." + sectionTitle);
        container.Add(header);
        detailsSectionContentRoot.Add(container);
        return container;
    }

    /// <summary>
    /// Adds one lazily built UI visual subsection tab.
    /// </summary>
    /// <param name="panel">Owning UI visual panel.</param>
    /// <param name="subSectionType">Subsection represented by the tab.</param>
    /// <param name="tabLabel">Visible tab label.</param>
    /// <param name="contentFactory">Factory used to build tab content on demand.</param>
    private static void AddUiVisualSubSectionTab(PlayerUiVisualPresetsPanel panel,
                                                 PlayerUiVisualPresetsPanel.UiVisualSubSectionType subSectionType,
                                                 string tabLabel,
                                                 System.Func<VisualElement> contentFactory)
    {
        if (panel == null)
            return;

        if (panel.UiVisualSubSectionTabBar == null || contentFactory == null)
            return;

        VisualElement tabContainer = new VisualElement();
        tabContainer.style.flexDirection = FlexDirection.Row;
        tabContainer.style.alignItems = Align.Center;
        tabContainer.style.marginRight = 6f;
        tabContainer.style.marginBottom = 4f;

        Button tabButton = new Button(() => panel.SetActiveUiVisualSubSection(subSectionType));
        tabButton.text = tabLabel;
        tabButton.style.unityTextAlign = TextAnchor.MiddleLeft;
        tabContainer.Add(tabButton);
        panel.UiVisualSubSectionTabBar.Add(tabContainer);

        PlayerUiVisualPresetsPanel.UiVisualSubSectionTabEntry tabEntry = new PlayerUiVisualPresetsPanel.UiVisualSubSectionTabEntry();
        tabEntry.TabContainer = tabContainer;
        tabEntry.TabButton = tabButton;
        tabEntry.ContentFactory = contentFactory;
        panel.UiVisualSubSectionTabs[subSectionType] = tabEntry;
    }

    /// <summary>
    /// Refreshes bold and background style on UI visual subsection tabs.
    /// </summary>
    /// <param name="panel">Owning UI visual panel.</param>
    private static void UpdateUiVisualSubSectionTabStyles(PlayerUiVisualPresetsPanel panel)
    {
        if (panel == null)
            return;

        foreach (KeyValuePair<PlayerUiVisualPresetsPanel.UiVisualSubSectionType, PlayerUiVisualPresetsPanel.UiVisualSubSectionTabEntry> tabEntry in panel.UiVisualSubSectionTabs)
        {
            if (tabEntry.Value == null || tabEntry.Value.TabButton == null)
                continue;

            bool isActive = tabEntry.Key == panel.ActiveUiVisualSubSection;
            tabEntry.Value.TabButton.style.unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal;
            tabEntry.Value.TabButton.style.backgroundColor = isActive ? ActiveTabColor : Color.clear;
        }
    }
    #endregion

    #endregion
}
