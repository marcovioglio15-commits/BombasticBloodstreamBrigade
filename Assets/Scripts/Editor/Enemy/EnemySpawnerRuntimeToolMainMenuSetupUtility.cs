using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Installs and refreshes the runtime enemy spawner tool UI inside the main menu scene.
/// </summary>
public static class EnemySpawnerRuntimeToolMainMenuSetupUtility
{
    #region Constants
    private const string MenuPath = "Tools/Enemy/Runtime Spawner Tool/Apply Main Menu Setup";
    private const string MainMenuScenePath = "Assets/Scenes/Testing/Main Scenes/UI/SCN_MainMenu.unity";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds the runtime catalog and installs the main-menu button and panel.
    /// </summary>
    //[MenuItem(MenuPath)]
    public static void ApplyDefaultSetup()
    {
        EnemySpawnerRuntimeCatalog catalog = EnemySpawnerRuntimeCatalogBuildUtility.RebuildCatalogAsset();
        EnemySpawnerRuntimeCatalog loadedCatalog = AssetDatabase.LoadAssetAtPath<EnemySpawnerRuntimeCatalog>(EnemySpawnerRuntimeCatalogBuildUtility.CatalogAssetPath);

        if (loadedCatalog != null)
            catalog = loadedCatalog;

        Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        MainMenuController menuController = Object.FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);

        if (menuController == null)
        {
            Debug.LogWarning("[EnemySpawnerRuntimeToolMainMenuSetupUtility] MainMenuController not found in SCN_MainMenu.");
            return;
        }

        Button playButton = ResolveButton(menuController, "playButton");
        Button quitButton = ResolveButton(menuController, "quitButton");

        if (playButton == null || quitButton == null)
        {
            Debug.LogWarning("[EnemySpawnerRuntimeToolMainMenuSetupUtility] Play or Quit button is missing from MainMenuController.");
            return;
        }

        Canvas canvas = playButton.GetComponentInParent<Canvas>(true);
        TMP_FontAsset fontAsset = ResolveMenuFont(playButton);
        Button toolButton = EnsureToolButton(playButton, quitButton, fontAsset);
        EnemySpawnerRuntimeToolPanelController toolPanel = EnsureToolPanel(canvas, fontAsset, catalog);
        AssignObject(menuController, "enemySpawnerToolButton", toolButton);
        AssignObject(menuController, "enemySpawnerToolPanel", toolPanel);
        RefreshButtonNavigation(playButton, toolButton, quitButton);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }
    #endregion

    #region Button Setup
    /// <summary>
    /// Creates or refreshes the main-menu button that opens the runtime spawner tool.
    /// </summary>
    /// <param name="playButton">Existing Play button used as visual template.</param>
    /// <param name="quitButton">Existing Quit button used for sibling placement.</param>
    /// <param name="fontAsset">Font asset copied into new labels.</param>
    /// <returns>Configured tool button.</returns>
    private static Button EnsureToolButton(Button playButton, Button quitButton, TMP_FontAsset fontAsset)
    {
        Transform parent = playButton.transform.parent;
        Transform existing = parent.Find("EnemySpawnerToolButton");
        GameObject buttonObject = existing != null
            ? existing.gameObject
            : Object.Instantiate(playButton.gameObject, parent);
        buttonObject.name = "EnemySpawnerToolButton";
        buttonObject.SetActive(true);
        buttonObject.transform.SetSiblingIndex(Mathf.Min(quitButton.transform.GetSiblingIndex(), playButton.transform.GetSiblingIndex() + 1));
        Button button = buttonObject.GetComponent<Button>();
        SetButtonLabel(buttonObject, "Spawner Tool", fontAsset);
        return button;
    }

    /// <summary>
    /// Sets the text label on a button hierarchy.
    /// </summary>
    /// <param name="buttonObject">Button GameObject to update.</param>
    /// <param name="label">Label text.</param>
    /// <param name="fontAsset">Font asset assigned when available.</param>
    private static void SetButtonLabel(GameObject buttonObject, string label, TMP_FontAsset fontAsset)
    {
        TMP_Text text = buttonObject.GetComponentInChildren<TMP_Text>(true);

        if (text == null)
            return;

        text.text = label;

        if (fontAsset != null)
            text.font = fontAsset;
    }

    /// <summary>
    /// Rebuilds explicit menu navigation after inserting the tool button.
    /// </summary>
    /// <param name="playButton">Play button.</param>
    /// <param name="toolButton">Runtime spawner tool button.</param>
    /// <param name="quitButton">Quit button.</param>
    private static void RefreshButtonNavigation(Button playButton, Button toolButton, Button quitButton)
    {
        Navigation playNavigation = playButton.navigation;
        playNavigation.mode = Navigation.Mode.Explicit;
        playNavigation.selectOnUp = quitButton;
        playNavigation.selectOnDown = toolButton;
        playButton.navigation = playNavigation;

        Navigation toolNavigation = toolButton.navigation;
        toolNavigation.mode = Navigation.Mode.Explicit;
        toolNavigation.selectOnUp = playButton;
        toolNavigation.selectOnDown = quitButton;
        toolButton.navigation = toolNavigation;

        Navigation quitNavigation = quitButton.navigation;
        quitNavigation.mode = Navigation.Mode.Explicit;
        quitNavigation.selectOnUp = toolButton;
        quitNavigation.selectOnDown = playButton;
        quitButton.navigation = quitNavigation;
    }
    #endregion

    #region Panel Setup
    /// <summary>
    /// Creates or refreshes the runtime spawner tool panel hierarchy under the main menu canvas.
    /// </summary>
    /// <param name="canvas">Main menu canvas.</param>
    /// <param name="fontAsset">Font asset used by generated labels.</param>
    /// <param name="catalog">Runtime catalog asset assigned to the panel.</param>
    /// <returns>Configured panel controller.</returns>
    private static EnemySpawnerRuntimeToolPanelController EnsureToolPanel(Canvas canvas,
                                                                          TMP_FontAsset fontAsset,
                                                                          EnemySpawnerRuntimeCatalog catalog)
    {
        Transform existing = canvas.transform.Find("EnemySpawnerRuntimeToolController");
        GameObject controllerObject = existing != null
            ? existing.gameObject
            : CreateRectObject("EnemySpawnerRuntimeToolController", canvas.transform);
        EnemySpawnerRuntimeToolPanelController controller = controllerObject.GetComponent<EnemySpawnerRuntimeToolPanelController>();

        if (controller == null)
            controller = controllerObject.AddComponent<EnemySpawnerRuntimeToolPanelController>();

        Transform panelTransform = controllerObject.transform.Find("PanelRoot");
        GameObject panelRoot = panelTransform != null
            ? panelTransform.gameObject
            : BuildPanelRoot(controllerObject.transform, fontAsset);
        EnemySpawnerRuntimeToolPanelReferences references = ResolvePanelReferences(panelRoot, fontAsset);
        EnemySpawnerRuntimeToolMainMenuTableUtility.EnsureRowsHeader(panelRoot, fontAsset);
        EnemySpawnerRuntimeToolMainMenuLayoutUtility.RefreshGeneratedPanelLayout(panelRoot);
        AssignObject(controller, "runtimeCatalog", catalog);
        AssignObject(controller, "panelRoot", panelRoot);
        AssignObject(controller, "statusLabel", references.StatusLabel);
        AssignObject(controller, "sceneSummaryLabel", references.SceneSummaryLabel);
        AssignObject(controller, "sceneDropdown", references.SceneDropdown);
        AssignObject(controller, "presetFolderDropdown", references.PresetFolderDropdown);
        AssignObject(controller, "searchInput", references.SearchInput);
        AssignObject(controller, "rowsContentRoot", references.RowsContentRoot);
        AssignObject(controller, "rowTemplate", references.RowTemplate);
        AssignObject(controller, "closeButton", references.CloseButton);
        AssignObject(controller, "resetSceneButton", references.ResetSceneButton);
        AssignObject(controller, "enableAllButton", references.EnableAllButton);
        AssignObject(controller, "disableAllButton", references.DisableAllButton);
        panelRoot.SetActive(false);
        return controller;
    }

    /// <summary>
    /// Builds the root visual hierarchy for the runtime tool panel.
    /// </summary>
    /// <param name="parent">Parent transform under the canvas.</param>
    /// <param name="fontAsset">Font asset used by generated labels.</param>
    /// <returns>Panel root GameObject.</returns>
    private static GameObject BuildPanelRoot(Transform parent, TMP_FontAsset fontAsset)
    {
        GameObject panelRoot = CreateRectObject("PanelRoot", parent);
        Stretch(panelRoot.GetComponent<RectTransform>());
        Image background = panelRoot.AddComponent<Image>();
        background.color = new Color(0.02f, 0.03f, 0.04f, 0.94f);

        GameObject dialog = CreateRectObject("Dialog", panelRoot.transform);
        RectTransform dialogRect = dialog.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.sizeDelta = new Vector2(1180f, 760f);
        dialogRect.anchoredPosition = Vector2.zero;
        Image dialogImage = dialog.AddComponent<Image>();
        dialogImage.color = new Color(0.08f, 0.11f, 0.14f, 0.98f);
        VerticalLayoutGroup layout = dialog.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 20, 20);
        layout.spacing = 10f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        CreateText("Title", dialog.transform, "Enemy Spawner Runtime Tool", 30, fontAsset, TextAlignmentOptions.Left);
        CreateSelectors(dialog.transform, fontAsset);
        CreateRowsArea(dialog.transform, fontAsset);
        CreateFooter(dialog.transform, fontAsset);
        return panelRoot;
    }

    /// <summary>
    /// Creates selector controls for scene, folder and search.
    /// </summary>
    /// <param name="parent">Parent transform.</param>
    /// <param name="fontAsset">Font asset used by generated labels.</param>
    private static void CreateSelectors(Transform parent, TMP_FontAsset fontAsset)
    {
        GameObject selectorRow = CreateRectObject("Selectors", parent);
        HorizontalLayoutGroup layout = selectorRow.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        CreateDropdown("SceneDropdown", selectorRow.transform, fontAsset);
        CreateDropdown("PresetFolderDropdown", selectorRow.transform, fontAsset);
        CreateInputField("SearchInput", selectorRow.transform, fontAsset);
        SetPreferredHeight(selectorRow, 48f);
    }

    /// <summary>
    /// Creates the scrollable row area and inactive row template.
    /// </summary>
    /// <param name="parent">Parent transform.</param>
    /// <param name="fontAsset">Font asset used by generated labels.</param>
    private static void CreateRowsArea(Transform parent, TMP_FontAsset fontAsset)
    {
        GameObject scrollObject = CreateRectObject("RowsScroll", parent);
        Image scrollImage = scrollObject.AddComponent<Image>();
        scrollImage.color = new Color(0.04f, 0.06f, 0.08f, 0.96f);
        ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        SetFlexibleHeight(scrollObject, 1f);
        EnemySpawnerRuntimeToolMainMenuLayoutUtility.ApplyRowsScrollLayout(scrollObject);

        GameObject viewport = CreateRectObject("Viewport", scrollObject.transform);
        Stretch(viewport.GetComponent<RectTransform>());
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        viewportImage.raycastTarget = false;
        viewportImage.canvasRenderer.cullTransparentMesh = false;
        viewport.AddComponent<RectMask2D>();

        GameObject content = CreateRectObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 0f);
        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 6f;
        contentLayout.padding = new RectOffset(8, 8, 8, 8);
        contentLayout.childControlHeight = true;
        contentLayout.childControlWidth = true;
        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRect;
        BuildRowTemplate(content.transform, fontAsset);
    }

    /// <summary>
    /// Creates footer status labels and action buttons.
    /// </summary>
    /// <param name="parent">Parent transform.</param>
    /// <param name="fontAsset">Font asset used by generated labels.</param>
    private static void CreateFooter(Transform parent, TMP_FontAsset fontAsset)
    {
        CreateText("SceneSummaryLabel", parent, "No scene selected.", 18, fontAsset, TextAlignmentOptions.Left);
        GameObject footer = CreateRectObject("Footer", parent);
        HorizontalLayoutGroup layout = footer.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        CreateText("StatusLabel", footer.transform, "Overrides: 0", 18, fontAsset, TextAlignmentOptions.Left);
        CreateButton("EnableAllButton", footer.transform, "Enable All", fontAsset);
        CreateButton("DisableAllButton", footer.transform, "Disable All", fontAsset);
        CreateButton("ResetSceneButton", footer.transform, "Reset Scene", fontAsset);
        CreateButton("CloseButton", footer.transform, "Close", fontAsset);
        SetPreferredHeight(footer, 46f);
    }
    #endregion

    #region Row Template
    /// <summary>
    /// Creates an inactive row template used by the runtime panel controller.
    /// </summary>
    /// <param name="parent">Rows content transform.</param>
    /// <param name="fontAsset">Font asset used by generated labels.</param>
    private static void BuildRowTemplate(Transform parent, TMP_FontAsset fontAsset)
    {
        GameObject row = CreateRectObject("RowTemplate", parent);
        Image rowImage = row.AddComponent<Image>();
        rowImage.color = new Color(0.11f, 0.15f, 0.19f, 1f);
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = false;
        EnemySpawnerRuntimeToolRowView rowView = row.AddComponent<EnemySpawnerRuntimeToolRowView>();
        Toggle enabledToggle = CreateToggle("EnabledToggle", row.transform);
        TMP_Text nameLabel = CreateText("NameLabel", row.transform, "Spawner", 18, fontAsset, TextAlignmentOptions.Left);
        CreateDivider("SpawnerPresetDivider", row.transform);
        TMP_Dropdown dropdown = CreateDropdown("WavePresetDropdown", row.transform, fontAsset);
        Button resetButton = CreateButton("ResetButton", row.transform, "Reset", fontAsset);
        AssignObject(rowView, "enabledToggle", enabledToggle);
        AssignObject(rowView, "wavePresetDropdown", dropdown);
        AssignObject(rowView, "resetButton", resetButton);
        AssignObject(rowView, "nameLabel", nameLabel);
        SetPreferredHeight(row, 58f);
        EnemySpawnerRuntimeToolMainMenuLayoutUtility.ApplyRowTemplateLayout(row);
        row.SetActive(false);
    }
    #endregion

    #region Control Creation
    /// <summary>
    /// Creates a TextMeshPro label.
    /// </summary>
    /// <param name="name">GameObject name.</param>
    /// <param name="parent">Parent transform.</param>
    /// <param name="text">Displayed text.</param>
    /// <param name="fontSize">Font size.</param>
    /// <param name="fontAsset">Font asset used when available.</param>
    /// <param name="alignment">Text alignment.</param>
    /// <returns>Created text component.</returns>
    private static TMP_Text CreateText(string name,
                                       Transform parent,
                                       string text,
                                       int fontSize,
                                       TMP_FontAsset fontAsset,
                                       TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateRectObject(name, parent);
        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = new Color(0.92f, 0.96f, 1f, 1f);
        label.alignment = alignment;

        if (fontAsset != null)
            label.font = fontAsset;

        return label;
    }

    /// <summary>
    /// Creates a styled button with a TextMeshPro label.
    /// </summary>
    /// <param name="name">GameObject name.</param>
    /// <param name="parent">Parent transform.</param>
    /// <param name="text">Button text.</param>
    /// <param name="fontAsset">Font asset used by label.</param>
    /// <returns>Created button.</returns>
    private static Button CreateButton(string name, Transform parent, string text, TMP_FontAsset fontAsset)
    {
        GameObject buttonObject = CreateRectObject(name, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.19f, 0.27f, 0.34f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text label = CreateText("Label", buttonObject.transform, text, 18, fontAsset, TextAlignmentOptions.Center);
        Stretch(label.GetComponent<RectTransform>());
        SetPreferredSize(buttonObject, 132f, 40f);
        return button;
    }

    /// <summary>
    /// Creates a basic TextMeshPro dropdown with a valid template.
    /// </summary>
    /// <param name="name">GameObject name.</param>
    /// <param name="parent">Parent transform.</param>
    /// <param name="fontAsset">Font asset used by labels.</param>
    /// <returns>Created dropdown.</returns>
    private static TMP_Dropdown CreateDropdown(string name, Transform parent, TMP_FontAsset fontAsset)
    {
        GameObject dropdownObject = CreateRectObject(name, parent);
        Image image = dropdownObject.AddComponent<Image>();
        image.color = new Color(0.15f, 0.2f, 0.25f, 1f);
        TMP_Dropdown dropdown = dropdownObject.AddComponent<TMP_Dropdown>();
        TMP_Text caption = CreateText("Label", dropdownObject.transform, string.Empty, 16, fontAsset, TextAlignmentOptions.Left);
        Stretch(caption.GetComponent<RectTransform>());
        RectTransform template = BuildDropdownTemplate(dropdownObject.transform, fontAsset);
        dropdown.targetGraphic = image;
        dropdown.captionText = caption;
        dropdown.template = template;
        dropdown.itemText = template.GetComponentInChildren<TMP_Text>(true);
        SetPreferredSize(dropdownObject, 280f, 40f);
        return dropdown;
    }

    /// <summary>
    /// Builds a minimal TMP_Dropdown template hierarchy.
    /// </summary>
    /// <param name="parent">Dropdown transform.</param>
    /// <param name="fontAsset">Font asset used by item labels.</param>
    /// <returns>Template RectTransform.</returns>
    private static RectTransform BuildDropdownTemplate(Transform parent, TMP_FontAsset fontAsset)
    {
        GameObject template = CreateRectObject("Template", parent);
        RectTransform templateRect = template.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.anchoredPosition = new Vector2(0f, 2f);
        templateRect.sizeDelta = new Vector2(0f, 220f);
        template.AddComponent<Image>().color = new Color(0.07f, 0.1f, 0.13f, 1f);
        ScrollRect scrollRect = template.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        GameObject viewport = CreateRectObject("Viewport", template.transform);
        Stretch(viewport.GetComponent<RectTransform>());
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        viewportImage.raycastTarget = false;
        viewportImage.canvasRenderer.cullTransparentMesh = false;
        viewport.AddComponent<RectMask2D>();
        GameObject content = CreateRectObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 0f);
        content.AddComponent<VerticalLayoutGroup>().childControlHeight = true;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Toggle itemToggle = CreateToggle("Item", content.transform);
        TMP_Text itemLabel = CreateText("Item Label", itemToggle.transform, "Option", 16, fontAsset, TextAlignmentOptions.Left);
        Stretch(itemLabel.GetComponent<RectTransform>());
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRect;
        template.SetActive(false);
        return templateRect;
    }

    /// <summary>
    /// Creates a basic TextMeshPro input field.
    /// </summary>
    /// <param name="name">GameObject name.</param>
    /// <param name="parent">Parent transform.</param>
    /// <param name="fontAsset">Font asset used by text.</param>
    /// <returns>Created input field.</returns>
    private static TMP_InputField CreateInputField(string name, Transform parent, TMP_FontAsset fontAsset)
    {
        GameObject inputObject = CreateRectObject(name, parent);
        Image image = inputObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.16f, 0.2f, 1f);
        TMP_InputField inputField = inputObject.AddComponent<TMP_InputField>();
        TMP_Text text = CreateText("Text", inputObject.transform, string.Empty, 16, fontAsset, TextAlignmentOptions.Left);
        TMP_Text placeholder = CreateText("Placeholder", inputObject.transform, "Search spawners", 16, fontAsset, TextAlignmentOptions.Left);
        placeholder.color = new Color(0.62f, 0.68f, 0.74f, 1f);
        Stretch(text.GetComponent<RectTransform>());
        Stretch(placeholder.GetComponent<RectTransform>());
        inputField.textComponent = text;
        inputField.placeholder = placeholder;
        inputField.targetGraphic = image;
        SetPreferredSize(inputObject, 240f, 40f);
        return inputField;
    }

    /// <summary>
    /// Creates a basic toggle with background and checkmark graphics.
    /// </summary>
    /// <param name="name">GameObject name.</param>
    /// <param name="parent">Parent transform.</param>
    /// <returns>Created toggle.</returns>
    private static Toggle CreateToggle(string name, Transform parent)
    {
        GameObject toggleObject = CreateRectObject(name, parent);
        Toggle toggle = toggleObject.AddComponent<Toggle>();
        GameObject background = CreateRectObject("Background", toggleObject.transform);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.sizeDelta = new Vector2(24f, 24f);
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = new Color(0.08f, 0.1f, 0.13f, 1f);
        GameObject checkmark = CreateRectObject("Checkmark", background.transform);
        Stretch(checkmark.GetComponent<RectTransform>());
        Image checkmarkImage = checkmark.AddComponent<Image>();
        checkmarkImage.color = new Color(0.34f, 0.78f, 0.52f, 1f);
        toggle.targetGraphic = backgroundImage;
        toggle.graphic = checkmarkImage;
        SetPreferredSize(toggleObject, 34f, 34f);
        return toggle;
    }

    /// <summary>
    /// Creates a thin Image separator used to divide the runtime spawner table columns.
    /// </summary>
    /// <param name="name">GameObject name.</param>
    /// <param name="parent">Parent transform.</param>
    /// <returns>Created divider image.</returns>
    private static Image CreateDivider(string name, Transform parent)
    {
        GameObject dividerObject = CreateRectObject(name, parent);
        Image divider = dividerObject.AddComponent<Image>();
        divider.color = new Color(0.23f, 0.31f, 0.38f, 1f);
        divider.raycastTarget = false;
        SetPreferredSize(dividerObject, 1f, 40f);
        return divider;
    }
    #endregion

    #region Reference Resolution
    /// <summary>
    /// Resolves all generated panel references from the panel root.
    /// </summary>
    /// <param name="panelRoot">Panel root GameObject.</param>
    /// <param name="fontAsset">Font asset assigned to generated labels.</param>
    /// <returns>Resolved panel references.</returns>
    private static EnemySpawnerRuntimeToolPanelReferences ResolvePanelReferences(GameObject panelRoot, TMP_FontAsset fontAsset)
    {
        EnemySpawnerRuntimeToolPanelReferences references = new EnemySpawnerRuntimeToolPanelReferences();
        references.StatusLabel = EnemySpawnerRuntimeToolMainMenuReferenceUtility.FindChildComponent<TMP_Text>(panelRoot.transform, "StatusLabel");
        references.SceneSummaryLabel = EnemySpawnerRuntimeToolMainMenuReferenceUtility.FindChildComponent<TMP_Text>(panelRoot.transform, "SceneSummaryLabel");
        references.SceneDropdown = EnemySpawnerRuntimeToolMainMenuReferenceUtility.FindChildComponent<TMP_Dropdown>(panelRoot.transform, "SceneDropdown");
        references.PresetFolderDropdown = EnemySpawnerRuntimeToolMainMenuReferenceUtility.FindChildComponent<TMP_Dropdown>(panelRoot.transform, "PresetFolderDropdown");
        references.SearchInput = EnemySpawnerRuntimeToolMainMenuReferenceUtility.FindChildComponent<TMP_InputField>(panelRoot.transform, "SearchInput");
        references.RowsContentRoot = EnemySpawnerRuntimeToolMainMenuReferenceUtility.ResolveRowsContentRoot(panelRoot.transform);
        references.RowTemplate = EnemySpawnerRuntimeToolMainMenuReferenceUtility.FindChildComponent<EnemySpawnerRuntimeToolRowView>(panelRoot.transform, "RowTemplate");
        references.CloseButton = EnemySpawnerRuntimeToolMainMenuReferenceUtility.FindChildComponent<Button>(panelRoot.transform, "CloseButton");
        references.ResetSceneButton = EnemySpawnerRuntimeToolMainMenuReferenceUtility.FindChildComponent<Button>(panelRoot.transform, "ResetSceneButton");
        references.EnableAllButton = EnemySpawnerRuntimeToolMainMenuReferenceUtility.FindChildComponent<Button>(panelRoot.transform, "EnableAllButton");
        references.DisableAllButton = EnemySpawnerRuntimeToolMainMenuReferenceUtility.FindChildComponent<Button>(panelRoot.transform, "DisableAllButton");
        EnemySpawnerRuntimeToolMainMenuReferenceUtility.AssignFont(panelRoot.transform, fontAsset);
        return references;
    }
    #endregion

    #region Serialized Helpers
    /// <summary>
    /// Assigns an object reference to one serialized field.
    /// </summary>
    /// <param name="target">Target object.</param>
    /// <param name="fieldName">Serialized field name.</param>
    /// <param name="value">Object reference to assign.</param>
    private static void AssignObject(Object target, string fieldName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty(fieldName);

        if (property == null)
            return;

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    /// <summary>
    /// Resolves a serialized Button reference from MainMenuController.
    /// </summary>
    /// <param name="controller">Controller to inspect.</param>
    /// <param name="fieldName">Serialized field name.</param>
    /// <returns>Resolved button or null.</returns>
    private static Button ResolveButton(MainMenuController controller, string fieldName)
    {
        SerializedObject serializedObject = new SerializedObject(controller);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        return property != null ? property.objectReferenceValue as Button : null;
    }
    #endregion

    #region Layout Helpers
    /// <summary>
    /// Creates a RectTransform GameObject.
    /// </summary>
    /// <param name="name">GameObject name.</param>
    /// <param name="parent">Parent transform.</param>
    /// <returns>Created GameObject.</returns>
    private static GameObject CreateRectObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    /// <summary>
    /// Stretches a RectTransform to fill its parent.
    /// </summary>
    /// <param name="rectTransform">RectTransform to stretch.</param>
    private static void Stretch(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Adds or updates a LayoutElement preferred size.
    /// </summary>
    /// <param name="gameObject">Target GameObject.</param>
    /// <param name="width">Preferred width.</param>
    /// <param name="height">Preferred height.</param>
    private static void SetPreferredSize(GameObject gameObject, float width, float height)
    {
        LayoutElement layoutElement = gameObject.GetComponent<LayoutElement>();

        if (layoutElement == null)
            layoutElement = gameObject.AddComponent<LayoutElement>();

        layoutElement.preferredWidth = width;
        layoutElement.preferredHeight = height;
    }

    /// <summary>
    /// Adds or updates a LayoutElement preferred height.
    /// </summary>
    /// <param name="gameObject">Target GameObject.</param>
    /// <param name="height">Preferred height.</param>
    private static void SetPreferredHeight(GameObject gameObject, float height)
    {
        LayoutElement layoutElement = gameObject.GetComponent<LayoutElement>();

        if (layoutElement == null)
            layoutElement = gameObject.AddComponent<LayoutElement>();

        layoutElement.preferredHeight = height;
    }

    /// <summary>
    /// Adds or updates a LayoutElement flexible height.
    /// </summary>
    /// <param name="gameObject">Target GameObject.</param>
    /// <param name="height">Flexible height.</param>
    private static void SetFlexibleHeight(GameObject gameObject, float height)
    {
        LayoutElement layoutElement = gameObject.GetComponent<LayoutElement>();

        if (layoutElement == null)
            layoutElement = gameObject.AddComponent<LayoutElement>();

        layoutElement.flexibleHeight = height;
    }

    /// <summary>
    /// Resolves the existing main-menu TextMeshPro font asset.
    /// </summary>
    /// <param name="templateButton">Button whose label provides the font.</param>
    /// <returns>Resolved font asset or null.</returns>
    private static TMP_FontAsset ResolveMenuFont(Button templateButton)
    {
        TMP_Text text = templateButton.GetComponentInChildren<TMP_Text>(true);
        return text != null ? text.font : null;
    }

    #endregion

    #endregion
}
