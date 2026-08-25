using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds and wires the fixed-capacity authored hierarchy used by the ECS power-up summary presentation.
/// </summary>
internal static class GameHudPowerUpSummaryProjectSetupUtility
{
    #region Constants
    private const string SummaryInstanceName = "PF_PowerUpSummary";
    private const string LegacySummaryRootName = "PowerUpSummaryPanel";
    private const string SummaryPrefabFolder = "Assets/Prefabs/UI/Power-Up Summary";
    public const string SummaryPrefabPath = SummaryPrefabFolder + "/PF_PowerUpSummary.prefab";
    private const string DefaultFontPath = "Assets/2D/UI/Fonts/NoctraDrip-Solid SDF.asset";
    #endregion

    #region Methods

    #region Entry Point
    /// <summary>
    /// Ensures one complete summary prefab instance exists below the gameplay canvas without runtime UI creation.
    /// </summary>
    /// <param name="canvas">Gameplay canvas receiving the authored panel.</param>
    /// <returns>Configured summary section component.</returns>
    public static HUDPowerUpSummarySection EnsureSection(Canvas canvas)
    {
        if (canvas == null)
            return null;

        GameObject prefabAsset = EnsurePrefabAsset();

        if (prefabAsset == null)
            return null;

        GameObject instance = ResolveUniqueInstance(canvas, prefabAsset);

        if (instance == null)
            instance = PrefabUtility.InstantiatePrefab(prefabAsset, canvas.transform) as GameObject;

        if (instance == null)
            return null;

        RectTransform panelRoot = instance.transform as RectTransform;
        panelRoot.SetParent(canvas.transform, false);
        panelRoot.anchorMin = new Vector2(1f, 0.5f);
        panelRoot.anchorMax = panelRoot.anchorMin;
        panelRoot.pivot = new Vector2(1f, 0.5f);
        panelRoot.sizeDelta = new Vector2(520f, 960f);
        panelRoot.anchoredPosition = Vector2.zero;
        return instance.GetComponent<HUDPowerUpSummarySection>();
    }

    /// <summary>
    /// Resolves one authored summary prefab instance and removes duplicates left by older name-based setup runs.
    /// </summary>
    /// <param name="canvas">Gameplay canvas searched for summary sections.</param>
    /// <param name="prefabAsset">Summary prefab asset expected from every valid instance.</param>
    /// <returns>Single retained prefab instance root, or null when none exists.</returns>
    private static GameObject ResolveUniqueInstance(Canvas canvas, GameObject prefabAsset)
    {
        HUDPowerUpSummarySection[] sections = canvas.GetComponentsInChildren<HUDPowerUpSummarySection>(true);
        GameObject retainedInstance = null;

        // Keep one matching prefab root and remove every duplicate as a complete prefab instance.
        for (int sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
        {
            HUDPowerUpSummarySection section = sections[sectionIndex];

            if (section == null)
                continue;

            GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(section.gameObject);
            GameObject sourceRoot = instanceRoot != null
                ? PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot)
                : null;

            if (sourceRoot != prefabAsset)
                continue;

            if (retainedInstance == null)
            {
                retainedInstance = instanceRoot;
                continue;
            }

            Object.DestroyImmediate(instanceRoot);
        }

        // Remove an obsolete non-prefab root that would otherwise collide with the stable authored name.
        Transform obsoleteRoot = canvas.transform.Find(LegacySummaryRootName);

        if (obsoleteRoot != null &&
            obsoleteRoot.gameObject != retainedInstance &&
            PrefabUtility.GetOutermostPrefabInstanceRoot(obsoleteRoot.gameObject) == null)
            Object.DestroyImmediate(obsoleteRoot.gameObject);

        return retainedInstance;
    }

    /// <summary>
    /// Creates or refreshes the reusable summary prefab from its fixed-capacity authored hierarchy.
    /// </summary>
    /// <returns>Saved summary prefab asset.</returns>
    private static GameObject EnsurePrefabAsset()
    {
        GameManagementAssetUtility.EnsureFolder(SummaryPrefabFolder);
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SummaryPrefabPath);
        GameObject prefabRoot = prefabAsset != null
            ? PrefabUtility.LoadPrefabContents(SummaryPrefabPath)
            : new GameObject(SummaryInstanceName, typeof(RectTransform));
        ConfigurePrefabRoot(prefabRoot.transform as RectTransform);
        PrefabUtility.SaveAsPrefabAsset(prefabRoot, SummaryPrefabPath);

        if (prefabAsset != null)
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        else
            Object.DestroyImmediate(prefabRoot);

        return AssetDatabase.LoadAssetAtPath<GameObject>(SummaryPrefabPath);
    }

    /// <summary>
    /// Builds and wires every fixed summary child below one prefab root.
    /// </summary>
    /// <param name="panelRoot">Prefab root receiving the complete hierarchy.</param>
    private static void ConfigurePrefabRoot(RectTransform panelRoot)
    {
        panelRoot.gameObject.name = SummaryInstanceName;
        panelRoot.anchorMin = new Vector2(1f, 0.5f);
        panelRoot.anchorMax = panelRoot.anchorMin;
        panelRoot.pivot = new Vector2(1f, 0.5f);
        panelRoot.sizeDelta = new Vector2(520f, 960f);
        panelRoot.anchoredPosition = Vector2.zero;

        Image backgroundImage = EnsureComponent<Image>(panelRoot.gameObject);
        backgroundImage.raycastTarget = false;
        HUDPowerUpSummarySection section = EnsureComponent<HUDPowerUpSummarySection>(panelRoot.gameObject);
        RectTransform contentRoot = EnsureRect("Content", panelRoot);
        Stretch(contentRoot);
        RectTransform powerUpAreaRoot = EnsureRect("PowerUpArea", contentRoot);
        RectTransform statisticsAreaRoot = EnsureRect("StatisticsArea", contentRoot);
        HorizontalLayoutGroup powerUpLayout = ConfigurePowerUpArea(powerUpAreaRoot);

        SummaryColumn activeColumn = EnsurePowerUpColumn(powerUpAreaRoot,
                                                        "ActiveColumn",
                                                        "ACTIVE",
                                                        GameHudPowerUpSummarySettings.AuthoredActiveSlotCapacity);
        Image columnSeparator = EnsureSeparator(powerUpAreaRoot, "PowerUpColumnSeparator", false);
        SummaryColumn passiveColumn = EnsurePowerUpColumn(powerUpAreaRoot,
                                                         "PassiveColumn",
                                                         "PASSIVE",
                                                         GameHudPowerUpSummarySettings.AuthoredPassiveSlotCapacity);
        SummaryStatistics statistics = EnsureStatisticsArea(statisticsAreaRoot);
        Button toggleButton = EnsureToggle(panelRoot, out Image toggleImage);
        WireSection(section,
                    panelRoot,
                    contentRoot,
                    powerUpAreaRoot,
                    statisticsAreaRoot,
                    backgroundImage,
                    toggleButton,
                    toggleImage,
                    powerUpLayout,
                    in activeColumn,
                    in passiveColumn,
                    columnSeparator,
                    in statistics);
    }
    #endregion

    #region Power-Up Areas
    /// <summary>
    /// Configures the upper active/passive row and its flexible column sizing.
    /// </summary>
    /// <param name="root">Upper content RectTransform.</param>
    /// <returns>Horizontal layout assigned to the upper area.</returns>
    private static HorizontalLayoutGroup ConfigurePowerUpArea(RectTransform root)
    {
        Stretch(root);
        HorizontalLayoutGroup layout = EnsureComponent<HorizontalLayoutGroup>(root.gameObject);
        layout.padding = new RectOffset();
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        return layout;
    }

    /// <summary>
    /// Ensures one titled, independently scrollable icon column and its fixed preauthored slot pool.
    /// </summary>
    /// <param name="parent">Upper area receiving the column.</param>
    /// <param name="name">Stable hierarchy name.</param>
    /// <param name="title">Initial title shown before ECS config is applied.</param>
    /// <param name="capacity">Fixed icon-slot capacity.</param>
    /// <returns>Column references required by the runtime section.</returns>
    private static SummaryColumn EnsurePowerUpColumn(RectTransform parent,
                                                     string name,
                                                     string title,
                                                     int capacity)
    {
        RectTransform columnRoot = EnsureRect(name, parent);
        LayoutElement columnElement = EnsureComponent<LayoutElement>(columnRoot.gameObject);
        columnElement.flexibleWidth = 1f;
        columnElement.flexibleHeight = 1f;
        VerticalLayoutGroup columnLayout = EnsureComponent<VerticalLayoutGroup>(columnRoot.gameObject);
        columnLayout.spacing = 6f;
        columnLayout.childControlWidth = true;
        columnLayout.childControlHeight = true;
        columnLayout.childForceExpandWidth = true;
        columnLayout.childForceExpandHeight = false;
        TMP_Text titleText = EnsureText(columnRoot, "Title", title, 20f, TextAlignmentOptions.Center);
        ConfigurePreferredHeight(titleText.gameObject, 30f);
        RectTransform viewport = EnsureRect("Viewport", columnRoot);
        LayoutElement viewportElement = EnsureComponent<LayoutElement>(viewport.gameObject);
        viewportElement.flexibleHeight = 1f;
        EnsureComponent<RectMask2D>(viewport.gameObject);
        ScrollRect scrollRect = EnsureComponent<ScrollRect>(viewport.gameObject);
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 28f;
        RectTransform gridRoot = EnsureRect("Grid", viewport);
        gridRoot.anchorMin = new Vector2(0f, 1f);
        gridRoot.anchorMax = new Vector2(1f, 1f);
        gridRoot.pivot = new Vector2(0.5f, 1f);
        gridRoot.anchoredPosition = Vector2.zero;
        gridRoot.sizeDelta = Vector2.zero;
        GridLayoutGroup grid = EnsureComponent<GridLayoutGroup>(gridRoot.gameObject);
        grid.cellSize = new Vector2(52f, 52f);
        grid.spacing = new Vector2(8f, 8f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        ContentSizeFitter fitter = EnsureComponent<ContentSizeFitter>(gridRoot.gameObject);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.viewport = viewport;
        scrollRect.content = gridRoot;
        HUDPowerUpSummaryIconView[] iconViews = EnsureIconViews(gridRoot, capacity);
        return new SummaryColumn(columnRoot, grid, titleText, iconViews);
    }

    /// <summary>
    /// Ensures every icon pool entry owns background, icon, and lower-edge counter references.
    /// </summary>
    /// <param name="gridRoot">Grid receiving icon entries.</param>
    /// <param name="capacity">Fixed number of preauthored entries.</param>
    /// <returns>Ordered icon view array.</returns>
    private static HUDPowerUpSummaryIconView[] EnsureIconViews(RectTransform gridRoot, int capacity)
    {
        HUDPowerUpSummaryIconView[] views = new HUDPowerUpSummaryIconView[capacity];

        for (int slotIndex = 0; slotIndex < capacity; slotIndex++)
        {
            RectTransform slotRoot = EnsureRect(string.Format("IconSlot_{0:D2}", slotIndex + 1), gridRoot);
            slotRoot.sizeDelta = new Vector2(52f, 52f);
            Image background = EnsureComponent<Image>(slotRoot.gameObject);
            background.raycastTarget = false;
            RectTransform iconRoot = EnsureRect("Icon", slotRoot);
            Stretch(iconRoot);
            Image icon = EnsureComponent<Image>(iconRoot.gameObject);
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            TMP_Text countText = EnsureText(slotRoot, "Count", string.Empty, 16f, TextAlignmentOptions.BottomRight);
            RectTransform countRect = countText.rectTransform;
            Stretch(countRect);
            countRect.offsetMin = new Vector2(2f, 0f);
            countRect.offsetMax = new Vector2(-2f, 0f);
            countText.raycastTarget = false;
            HUDPowerUpSummaryIconView view = EnsureComponent<HUDPowerUpSummaryIconView>(slotRoot.gameObject);
            SerializedObject serializedView = new SerializedObject(view);
            serializedView.Update();
            SetReference(serializedView, "backgroundImage", background);
            SetReference(serializedView, "iconImage", icon);
            SetReference(serializedView, "countText", countText);
            serializedView.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            slotRoot.gameObject.SetActive(false);
            views[slotIndex] = view;
        }

        return views;
    }
    #endregion

    #region Statistics Area
    /// <summary>
    /// Ensures the lower titled statistic area, separator, scroll viewport, and fixed row pool.
    /// </summary>
    /// <param name="root">Lower content RectTransform.</param>
    /// <returns>Statistic-area references required by the runtime section.</returns>
    private static SummaryStatistics EnsureStatisticsArea(RectTransform root)
    {
        Stretch(root);
        VerticalLayoutGroup layout = EnsureComponent<VerticalLayoutGroup>(root.gameObject);
        layout.spacing = 5f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        TMP_Text titleText = EnsureText(root, "Title", "PLAYER STATS", 20f, TextAlignmentOptions.Center);
        ConfigurePreferredHeight(titleText.gameObject, 30f);
        Image separator = EnsureSeparator(root, "StatisticsSeparator", true);
        ConfigurePreferredHeight(separator.gameObject, 1f);
        RectTransform viewport = EnsureRect("Viewport", root);
        LayoutElement viewportElement = EnsureComponent<LayoutElement>(viewport.gameObject);
        viewportElement.flexibleHeight = 1f;
        EnsureComponent<RectMask2D>(viewport.gameObject);
        ScrollRect scrollRect = EnsureComponent<ScrollRect>(viewport.gameObject);
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;
        RectTransform rowsRoot = EnsureRect("Rows", viewport);
        rowsRoot.anchorMin = new Vector2(0f, 1f);
        rowsRoot.anchorMax = new Vector2(1f, 1f);
        rowsRoot.pivot = new Vector2(0.5f, 1f);
        rowsRoot.anchoredPosition = Vector2.zero;
        rowsRoot.sizeDelta = Vector2.zero;
        VerticalLayoutGroup rowsLayout = EnsureComponent<VerticalLayoutGroup>(rowsRoot.gameObject);
        rowsLayout.spacing = 3f;
        rowsLayout.childControlWidth = true;
        rowsLayout.childControlHeight = true;
        rowsLayout.childForceExpandWidth = true;
        rowsLayout.childForceExpandHeight = false;
        ContentSizeFitter fitter = EnsureComponent<ContentSizeFitter>(rowsRoot.gameObject);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.viewport = viewport;
        scrollRect.content = rowsRoot;
        HUDPowerUpSummaryStatisticRowView[] rows = EnsureStatisticRows(rowsRoot);
        return new SummaryStatistics(titleText, separator, rows);
    }

    /// <summary>
    /// Ensures all fixed statistic rows and their TMP references are preauthored.
    /// </summary>
    /// <param name="rowsRoot">Scrollable content receiving rows.</param>
    /// <returns>Ordered statistic row array.</returns>
    private static HUDPowerUpSummaryStatisticRowView[] EnsureStatisticRows(RectTransform rowsRoot)
    {
        int capacity = GameHudPowerUpSummarySettings.AuthoredStatisticRowCapacity;
        HUDPowerUpSummaryStatisticRowView[] rows = new HUDPowerUpSummaryStatisticRowView[capacity];

        for (int rowIndex = 0; rowIndex < capacity; rowIndex++)
        {
            RectTransform rowRoot = EnsureRect(string.Format("StatisticRow_{0:D2}", rowIndex + 1), rowsRoot);
            ConfigurePreferredHeight(rowRoot.gameObject, 24f);
            TMP_Text valueText = EnsureText(rowRoot, "Value", string.Empty, 18f, TextAlignmentOptions.Left);
            Stretch(valueText.rectTransform);
            valueText.raycastTarget = false;
            HUDPowerUpSummaryStatisticRowView row = EnsureComponent<HUDPowerUpSummaryStatisticRowView>(rowRoot.gameObject);
            SerializedObject serializedRow = new SerializedObject(row);
            serializedRow.Update();
            SetReference(serializedRow, "valueText", valueText);
            serializedRow.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(row);
            rowRoot.gameObject.SetActive(false);
            rows[rowIndex] = row;
        }

        return rows;
    }
    #endregion

    #region Toggle and Wiring
    /// <summary>
    /// Ensures the persistent panel handle, arrow label, Button, and preauthored interaction relay.
    /// </summary>
    /// <param name="panelRoot">Panel receiving the handle.</param>
    /// <param name="toggleImage">Resolved target image.</param>
    /// <returns>Configured toggle button.</returns>
    private static Button EnsureToggle(RectTransform panelRoot, out Image toggleImage)
    {
        RectTransform toggleRoot = EnsureRect("Toggle", panelRoot);
        toggleRoot.anchorMin = new Vector2(0f, 0.5f);
        toggleRoot.anchorMax = toggleRoot.anchorMin;
        toggleRoot.pivot = new Vector2(0f, 0.5f);
        toggleRoot.sizeDelta = new Vector2(42f, 96f);
        toggleRoot.anchoredPosition = Vector2.zero;
        toggleImage = EnsureComponent<Image>(toggleRoot.gameObject);
        Button button = EnsureComponent<Button>(toggleRoot.gameObject);
        button.targetGraphic = toggleImage;
        TMP_Text arrowText = EnsureText(toggleRoot, "Arrow", "‹", 28f, TextAlignmentOptions.Center);
        Stretch(arrowText.rectTransform);
        arrowText.raycastTarget = false;
        MenuSelectableHoverRelay relay = EnsureComponent<MenuSelectableHoverRelay>(toggleRoot.gameObject);
        SerializedObject serializedRelay = new SerializedObject(relay);
        serializedRelay.Update();
        SerializedProperty menuKindProperty = serializedRelay.FindProperty("menuKind");

        if (menuKindProperty != null)
            menuKindProperty.enumValueIndex = (int)GameUiMenuKind.PowerUpSummary;

        serializedRelay.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(relay);
        return button;
    }

    /// <summary>
    /// Writes every authored UI reference and fixed array into the runtime summary section.
    /// </summary>
    /// <param name="section">Summary component being configured.</param>
    /// <param name="panelRoot">Sliding panel root.</param>
    /// <param name="contentRoot">Inset content root.</param>
    /// <param name="powerUpAreaRoot">Upper power-up area.</param>
    /// <param name="statisticsAreaRoot">Lower statistic area.</param>
    /// <param name="backgroundImage">Panel background.</param>
    /// <param name="toggleButton">Expand/collapse button.</param>
    /// <param name="toggleImage">Toggle target image.</param>
    /// <param name="powerUpLayout">Upper horizontal layout.</param>
    /// <param name="activeColumn">Active power-up column references.</param>
    /// <param name="passiveColumn">Passive power-up column references.</param>
    /// <param name="columnSeparator">Vertical column separator.</param>
    /// <param name="statistics">Lower statistic references.</param>
    private static void WireSection(HUDPowerUpSummarySection section,
                                    RectTransform panelRoot,
                                    RectTransform contentRoot,
                                    RectTransform powerUpAreaRoot,
                                    RectTransform statisticsAreaRoot,
                                    Image backgroundImage,
                                    Button toggleButton,
                                    Image toggleImage,
                                    HorizontalLayoutGroup powerUpLayout,
                                    in SummaryColumn activeColumn,
                                    in SummaryColumn passiveColumn,
                                    Image columnSeparator,
                                    in SummaryStatistics statistics)
    {
        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        SetReference(serializedSection, "panelRoot", panelRoot);
        SetReference(serializedSection, "contentRoot", contentRoot);
        SetReference(serializedSection, "powerUpAreaRoot", powerUpAreaRoot);
        SetReference(serializedSection, "statisticsAreaRoot", statisticsAreaRoot);
        SetReference(serializedSection, "backgroundImage", backgroundImage);
        SetReference(serializedSection, "toggleButton", toggleButton);
        SetReference(serializedSection, "toggleImage", toggleImage);
        SetReference(serializedSection, "activeColumnRoot", activeColumn.Root);
        SetReference(serializedSection, "passiveColumnRoot", passiveColumn.Root);
        SetReference(serializedSection, "activeGrid", activeColumn.Grid);
        SetReference(serializedSection, "passiveGrid", passiveColumn.Grid);
        SetReference(serializedSection, "powerUpColumnsLayout", powerUpLayout);
        SetReference(serializedSection, "activeTitleText", activeColumn.Title);
        SetReference(serializedSection, "passiveTitleText", passiveColumn.Title);
        SetReference(serializedSection, "powerUpColumnSeparator", columnSeparator);
        SetArray(serializedSection, "activeIconViews", activeColumn.Views);
        SetArray(serializedSection, "passiveIconViews", passiveColumn.Views);
        SetReference(serializedSection, "statisticsTitleText", statistics.Title);
        SetReference(serializedSection, "statisticsSeparator", statistics.Separator);
        SetArray(serializedSection, "statisticRows", statistics.Rows);
        serializedSection.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(section);
    }
    #endregion

    #region UI Helpers
    /// <summary>
    /// Creates one RectTransform child using the parent's layer.
    /// </summary>
    /// <param name="name">Stable child name.</param>
    /// <param name="parent">Parent transform.</param>
    /// <returns>Created RectTransform.</returns>
    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = parent.gameObject.layer;
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        return rectTransform;
    }

    /// <summary>
    /// Finds or creates one named RectTransform child.
    /// </summary>
    /// <param name="name">Stable child name.</param>
    /// <param name="parent">Parent RectTransform.</param>
    /// <returns>Existing or created child RectTransform.</returns>
    private static RectTransform EnsureRect(string name, RectTransform parent)
    {
        Transform existing = parent.Find(name);

        if (existing != null)
            return existing as RectTransform;

        return CreateRect(name, parent);
    }

    /// <summary>
    /// Stretches one RectTransform across its parent while clearing offsets.
    /// </summary>
    /// <param name="rectTransform">RectTransform to configure.</param>
    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Finds or adds one required component on an authored GameObject.
    /// </summary>
    /// <typeparam name="TComponent">Component type to ensure.</typeparam>
    /// <param name="gameObject">Authored GameObject receiving the component.</param>
    /// <returns>Existing or added component.</returns>
    private static TComponent EnsureComponent<TComponent>(GameObject gameObject) where TComponent : Component
    {
        TComponent component = gameObject.GetComponent<TComponent>();

        if (component == null)
            component = gameObject.AddComponent<TComponent>();

        return component;
    }

    /// <summary>
    /// Ensures one TMP child and applies safe initial text presentation values.
    /// </summary>
    /// <param name="parent">Parent RectTransform.</param>
    /// <param name="name">Stable child name.</param>
    /// <param name="value">Initial visible text.</param>
    /// <param name="fontSize">Initial font size.</param>
    /// <param name="alignment">Initial TMP alignment.</param>
    /// <returns>Configured TMP text.</returns>
    private static TMP_Text EnsureText(RectTransform parent,
                                       string name,
                                       string value,
                                       float fontSize,
                                       TextAlignmentOptions alignment)
    {
        RectTransform textRoot = EnsureRect(name, parent);
        TextMeshProUGUI text = EnsureComponent<TextMeshProUGUI>(textRoot.gameObject);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFontPath);
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        if (font != null)
            text.font = font;

        return text;
    }

    /// <summary>
    /// Ensures one separator image with stable transparent-raycast behavior.
    /// </summary>
    /// <param name="parent">Parent receiving the separator.</param>
    /// <param name="name">Stable separator name.</param>
    /// <param name="horizontal">True for a horizontal line, false for a vertical line.</param>
    /// <returns>Configured separator image.</returns>
    private static Image EnsureSeparator(RectTransform parent, string name, bool horizontal)
    {
        RectTransform separatorRoot = EnsureRect(name, parent);
        Image separator = EnsureComponent<Image>(separatorRoot.gameObject);
        separator.color = new Color(1f, 1f, 1f, 0.28f);
        separator.raycastTarget = false;
        LayoutElement element = EnsureComponent<LayoutElement>(separatorRoot.gameObject);

        if (horizontal)
            element.preferredHeight = 1f;
        else
            element.preferredWidth = 1f;

        return separator;
    }

    /// <summary>
    /// Applies one preferred height without forcing flexible growth.
    /// </summary>
    /// <param name="gameObject">Authored UI object receiving layout metadata.</param>
    /// <param name="height">Preferred layout height.</param>
    private static void ConfigurePreferredHeight(GameObject gameObject, float height)
    {
        LayoutElement element = EnsureComponent<LayoutElement>(gameObject);
        element.preferredHeight = height;
        element.flexibleHeight = 0f;
    }

    /// <summary>
    /// Assigns one serialized object reference when its field exists.
    /// </summary>
    /// <param name="serializedObject">Serialized component owning the field.</param>
    /// <param name="propertyName">Serialized field name.</param>
    /// <param name="value">Object reference to assign.</param>
    private static void SetReference(SerializedObject serializedObject,
                                     string propertyName,
                                     Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.objectReferenceValue = value;
    }

    /// <summary>
    /// Replaces one serialized component-reference array with an ordered fixed pool.
    /// </summary>
    /// <typeparam name="TComponent">Component reference type.</typeparam>
    /// <param name="serializedObject">Serialized component owning the array.</param>
    /// <param name="propertyName">Serialized array field name.</param>
    /// <param name="values">Ordered preauthored references.</param>
    private static void SetArray<TComponent>(SerializedObject serializedObject,
                                             string propertyName,
                                             IReadOnlyList<TComponent> values) where TComponent : Component
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        property.arraySize = values.Count;

        for (int valueIndex = 0; valueIndex < values.Count; valueIndex++)
            property.GetArrayElementAtIndex(valueIndex).objectReferenceValue = values[valueIndex];
    }
    #endregion

    #endregion

    #region Data Structures
    /// <summary>
    /// Groups references belonging to one independently scrollable power-up column.
    /// </summary>
    private readonly struct SummaryColumn
    {
        public readonly RectTransform Root;
        public readonly GridLayoutGroup Grid;
        public readonly TMP_Text Title;
        public readonly HUDPowerUpSummaryIconView[] Views;

        /// <summary>
        /// Creates one immutable column reference group.
        /// </summary>
        /// <param name="root">Column root.</param>
        /// <param name="grid">Scrollable icon grid.</param>
        /// <param name="title">Column title.</param>
        /// <param name="views">Fixed icon view pool.</param>
        public SummaryColumn(RectTransform root,
                             GridLayoutGroup grid,
                             TMP_Text title,
                             HUDPowerUpSummaryIconView[] views)
        {
            Root = root;
            Grid = grid;
            Title = title;
            Views = views;
        }
    }

    /// <summary>
    /// Groups references belonging to the independently scrollable statistic area.
    /// </summary>
    private readonly struct SummaryStatistics
    {
        public readonly TMP_Text Title;
        public readonly Image Separator;
        public readonly HUDPowerUpSummaryStatisticRowView[] Rows;

        /// <summary>
        /// Creates one immutable statistic-area reference group.
        /// </summary>
        /// <param name="title">Statistic section title.</param>
        /// <param name="separator">Horizontal section separator.</param>
        /// <param name="rows">Fixed statistic row pool.</param>
        public SummaryStatistics(TMP_Text title,
                                 Image separator,
                                 HUDPowerUpSummaryStatisticRowView[] rows)
        {
            Title = title;
            Separator = separator;
            Rows = rows;
        }
    }
    #endregion
}
