using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds metadata and UI-only visual subsections for enemy UI visual preset panels.
/// </summary>
internal static class EnemyUiVisualPresetsPanelSectionsUtility
{
    #region Constants
    private static readonly Color ActiveTabColor = new Color(0.18f, 0.18f, 0.18f, 0.6f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds metadata fields for the selected enemy UI visual preset.
    /// </summary>
    /// <param name="panel">Owning UI visual panel.</param>
    public static void BuildMetadataSection(EnemyUiVisualPresetsPanel panel)
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
            EnemyManagementDraftSession.MarkDirty();
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
            EnemyManagementDraftSession.MarkDirty();
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
    /// Builds the UI visual tab container and all enemy UI subsections.
    /// </summary>
    /// <param name="panel">Owning UI visual panel.</param>
    public static void BuildVisualSection(EnemyUiVisualPresetsPanel panel)
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
                                 EnemyUiVisualPresetsPanel.UiVisualSubSectionType.Footprint,
                                 "Footprint",
                                 EnemyVisualPresetsPanelFootprintSectionUtility.BuildFootprintSubSection(panel));
        AddUiVisualSubSectionTab(panel,
                                 EnemyUiVisualPresetsPanel.UiVisualSubSectionType.BossUi,
                                 "Boss UI",
                                 EnemyVisualPresetsPanelSectionsUtility.BuildBossUiSubSection(panel));
        AddUiVisualSubSectionTab(panel,
                                 EnemyUiVisualPresetsPanel.UiVisualSubSectionType.ProjectileOffscreenWarning,
                                 "Projectile Warnings",
                                 EnemyVisualPresetsPanelSectionsUtility.BuildProjectileOffscreenWarningSubSection(panel));

        if (!panel.UiVisualSubSectionTabs.ContainsKey(panel.ActiveUiVisualSubSection))
            panel.ActiveUiVisualSubSection = EnemyUiVisualPresetsPanel.UiVisualSubSectionType.Footprint;

        panel.SetActiveUiVisualSubSection(panel.ActiveUiVisualSubSection);
    }

    /// <summary>
    /// Shows the active UI visual subsection and refreshes tab styles.
    /// </summary>
    /// <param name="panel">Owning UI visual panel.</param>
    public static void ShowActiveUiVisualSubSection(EnemyUiVisualPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement contentHost = panel.UiVisualSubSectionContentHost;

        if (contentHost == null)
            return;

        EnemyUiVisualPresetsPanel.UiVisualSubSectionTabEntry tabEntry;

        if (!panel.UiVisualSubSectionTabs.TryGetValue(panel.ActiveUiVisualSubSection, out tabEntry))
            return;

        if (tabEntry == null || tabEntry.Content == null)
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
    private static VisualElement CreateDetailsSectionContainer(EnemyUiVisualPresetsPanel panel, string sectionTitle)
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
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(header, "NashCore.EnemyManagement.UiVisual.Section." + sectionTitle);
        container.Add(header);
        detailsSectionContentRoot.Add(container);
        return container;
    }

    /// <summary>
    /// Adds one already-built enemy UI visual subsection tab.
    /// </summary>
    /// <param name="panel">Owning UI visual panel.</param>
    /// <param name="subSectionType">Subsection represented by the tab.</param>
    /// <param name="tabLabel">Visible tab label.</param>
    /// <param name="content">Content shown when the tab is active.</param>
    private static void AddUiVisualSubSectionTab(EnemyUiVisualPresetsPanel panel,
                                                 EnemyUiVisualPresetsPanel.UiVisualSubSectionType subSectionType,
                                                 string tabLabel,
                                                 VisualElement content)
    {
        if (panel == null)
            return;

        if (panel.UiVisualSubSectionTabBar == null || content == null)
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

        EnemyUiVisualPresetsPanel.UiVisualSubSectionTabEntry tabEntry = new EnemyUiVisualPresetsPanel.UiVisualSubSectionTabEntry();
        tabEntry.TabContainer = tabContainer;
        tabEntry.TabButton = tabButton;
        tabEntry.Content = content;
        panel.UiVisualSubSectionTabs[subSectionType] = tabEntry;
    }

    /// <summary>
    /// Refreshes bold and background style on UI visual subsection tabs.
    /// </summary>
    /// <param name="panel">Owning UI visual panel.</param>
    private static void UpdateUiVisualSubSectionTabStyles(EnemyUiVisualPresetsPanel panel)
    {
        if (panel == null)
            return;

        foreach (KeyValuePair<EnemyUiVisualPresetsPanel.UiVisualSubSectionType, EnemyUiVisualPresetsPanel.UiVisualSubSectionTabEntry> tabEntry in panel.UiVisualSubSectionTabs)
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
