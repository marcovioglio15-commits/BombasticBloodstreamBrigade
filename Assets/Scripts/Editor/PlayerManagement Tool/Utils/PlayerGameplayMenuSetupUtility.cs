using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static PlayerGameplayMenuSetupSharedUtility;

/// <summary>
/// Builds the authored menu prefab, refreshes the menu scenes, and keeps gameplay UI in its additive companion scene.
/// /params None.
/// /returns None.
/// </summary>
public static class PlayerGameplayMenuSetupUtility
{
    #region Constants
    private const string GameplayMenusPrefabPath = "Assets/Prefabs/UI/PF_GameplayMenus.prefab";
    private const string MainMenuScenePath = "Assets/Scenes/Testing/Main Scenes/UI/SCN_MainMenu.unity";
    private const string GameplayScenePath = "Assets/Scenes/Testing/Main Scenes/SCN_PlayerControllerTesting/SCN_PlayerControllerTesting.unity";
    private const string BootstrapScenePath = GameSceneManagementProjectSetupUtility.BootstrapScenePath;
    private const string GameplayUiScenePath = GameSceneManagementProjectSetupUtility.GameplayUiScenePath;
    private static readonly Color BackgroundColor = new Color(0.06f, 0.08f, 0.11f, 1f);
    private static readonly Color OverlayColor = new Color(0.03f, 0.05f, 0.07f, 0.78f);
    private static readonly Color PanelColor = new Color(0.1f, 0.14f, 0.18f, 0.96f);
    private static readonly Color ButtonColor = new Color(0.2f, 0.28f, 0.35f, 1f);
    private static readonly Color ButtonHighlightColor = new Color(0.28f, 0.38f, 0.47f, 1f);
    private static readonly Color ButtonPressedColor = new Color(0.16f, 0.22f, 0.27f, 1f);
    private static readonly Color TextColor = new Color(0.95f, 0.97f, 1f, 1f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Runs the full authored UI setup and applies the project Scene Manager defaults it depends on.
    /// /params None.
    /// /returns None.
    /// </summary>
    public static void ExecuteSetup()
    {
        GameObject gameplayMenusPrefab = EnsureGameplayMenusPrefab();
        EnsureMainMenuScene();
        GameSceneManagementProjectSetupUtility.ApplyDefaultProjectSetup(false);
        EnsureGameplayUiScene(gameplayMenusPrefab);
        EnsureBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    #endregion

    #region Prefab Setup
    /// <summary>
    /// Creates or refreshes the authored gameplay-menu prefab used by the additive gameplay UI scene.
    /// /params None.
    /// /returns Gameplay menu prefab asset.
    /// </summary>
    private static GameObject EnsureGameplayMenusPrefab()
    {
        EnsureFolder(Path.GetDirectoryName(GameplayMenusPrefabPath));
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayMenusPrefabPath);
        GameObject prefabContentsRoot = existingPrefab != null
            ? PrefabUtility.LoadPrefabContents(GameplayMenusPrefabPath)
            : new GameObject("PF_GameplayMenus", typeof(RectTransform), typeof(GameplayMenuController));

        try
        {
            prefabContentsRoot.name = "PF_GameplayMenus";
            RectTransform rootRect = EnsureRectTransform(prefabContentsRoot);
            StretchToParent(rootRect);
            DestroyAllChildren(prefabContentsRoot.transform);

            GameObject pauseOverlay = CreateOverlayRoot("PauseMenu", prefabContentsRoot.transform);
            GameObject pausePanel = CreatePanel("PausePanel", pauseOverlay.transform, new Vector2(420f, 430f));
            TMP_Text pauseTitle = CreateTitleText("PauseTitle", "Paused", pausePanel.transform);
            Button resumeButton = CreateMenuButton("ResumeButton", "Resume", pausePanel.transform);
            Button restartButton = CreateMenuButton("RestartButton", "Restart", pausePanel.transform);
            Button mainMenuButton = CreateMenuButton("MainMenuButton", "Main Menu", pausePanel.transform);
            Button quitButton = CreateMenuButton("QuitButton", "Quit", pausePanel.transform);

            GameObject endingOverlay = CreateOverlayRoot("EndingMenu", prefabContentsRoot.transform);
            GameObject endingPanel = CreatePanel("EndingPanel", endingOverlay.transform, new Vector2(460f, 450f));
            TMP_Text endingTitle = CreateTitleText("EndingTitle", "Run Complete", endingPanel.transform);
            TMP_Text endingMessage = CreateBodyText("EndingMessage", "Victory", endingPanel.transform, 30f);
            Button playAgainButton = CreateMenuButton("PlayAgainButton", "Play Again", endingPanel.transform);
            Button endingMainMenuButton = CreateMenuButton("EndingMainMenuButton", "Main Menu", endingPanel.transform);
            Button endingQuitButton = CreateMenuButton("EndingQuitButton", "Quit", endingPanel.transform);

            pauseOverlay.SetActive(false);
            endingOverlay.SetActive(false);
            EnsureLayout(pausePanel.transform);
            EnsureLayout(endingPanel.transform);
            EnsureLayoutElement(pauseTitle.rectTransform.gameObject, 56f);
            EnsureLayoutElement(endingTitle.rectTransform.gameObject, 56f);
            EnsureLayoutElement(endingMessage.rectTransform.gameObject, 64f);
            ConfigureVerticalNavigation(resumeButton, restartButton, mainMenuButton, quitButton);
            ConfigureVerticalNavigation(playAgainButton, endingMainMenuButton, endingQuitButton);

            GameplayMenuController gameplayMenuController = GetOrAddComponent<GameplayMenuController>(prefabContentsRoot);
            MenuSelectionController selectionController = GetOrAddComponent<MenuSelectionController>(prefabContentsRoot);
            SerializedObject serializedController = new SerializedObject(gameplayMenuController);
            serializedController.Update();
            serializedController.FindProperty("pauseMenuRoot").objectReferenceValue = pauseOverlay;
            serializedController.FindProperty("resumeButton").objectReferenceValue = resumeButton;
            serializedController.FindProperty("pauseRestartButton").objectReferenceValue = restartButton;
            serializedController.FindProperty("pauseMainMenuButton").objectReferenceValue = mainMenuButton;
            serializedController.FindProperty("pauseQuitButton").objectReferenceValue = quitButton;
            serializedController.FindProperty("endingMenuRoot").objectReferenceValue = endingOverlay;
            serializedController.FindProperty("endingMessageText").objectReferenceValue = endingMessage;
            serializedController.FindProperty("endingPlayAgainButton").objectReferenceValue = playAgainButton;
            serializedController.FindProperty("endingMainMenuButton").objectReferenceValue = endingMainMenuButton;
            serializedController.FindProperty("endingQuitButton").objectReferenceValue = endingQuitButton;
            serializedController.FindProperty("victoryMessage").stringValue = "Victory";
            serializedController.FindProperty("defeatMessage").stringValue = "Defeat";
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedSelectionController = new SerializedObject(selectionController);
            serializedSelectionController.Update();
            serializedSelectionController.FindProperty("defaultSelectable").objectReferenceValue = resumeButton;
            serializedSelectionController.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(prefabContentsRoot, GameplayMenusPrefabPath);
        }
        finally
        {
            if (existingPrefab != null)
                PrefabUtility.UnloadPrefabContents(prefabContentsRoot);
            else
                UnityEngine.Object.DestroyImmediate(prefabContentsRoot);
        }

        GameObject generatedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayMenusPrefabPath);

        if (generatedPrefab == null)
            throw new InvalidOperationException(string.Format("Failed to create gameplay menus prefab at '{0}'.", GameplayMenusPrefabPath));

        return generatedPrefab;
    }
    #endregion

    #region Scene Setup
    /// <summary>
    /// Injects the authored gameplay-menu prefab into the additive gameplay UI scene and ensures UI input is ready there.
    /// /params gameplayMenusPrefab Prefab asset instantiated under the gameplay UI canvas.
    /// /returns None.
    /// </summary>
    private static void EnsureGameplayUiScene(GameObject gameplayMenusPrefab)
    {
        SceneAsset gameplayUiSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameplayUiScenePath);

        if (gameplayUiSceneAsset == null)
            throw new InvalidOperationException("Gameplay UI scene was not created by the Scene Manager project setup.");

        Scene gameplayUiScene = EditorSceneManager.OpenScene(GameplayUiScenePath, OpenSceneMode.Single);
        HUDManager hudManager = FindComponentInScene<HUDManager>(gameplayUiScene);

        if (hudManager == null)
            throw new InvalidOperationException("HUDManager not found in gameplay UI scene. Unable to resolve the gameplay canvas.");

        Canvas targetCanvas = hudManager.GetComponentInParent<Canvas>();

        if (targetCanvas == null)
            targetCanvas = ResolveGameplayCanvas(gameplayUiScene);

        if (targetCanvas == null)
            throw new InvalidOperationException("Gameplay canvas not found in gameplay UI scene.");

        EnsureSceneEventSystem(gameplayUiScene);
        RemoveExistingGameplayMenuControllers(gameplayUiScene);

        GameObject instantiatedMenus = PrefabUtility.InstantiatePrefab(gameplayMenusPrefab, gameplayUiScene) as GameObject;

        if (instantiatedMenus == null)
            throw new InvalidOperationException("Unable to instantiate gameplay menus prefab into gameplay UI scene.");

        RectTransform instantiatedRect = instantiatedMenus.GetComponent<RectTransform>();
        instantiatedRect.SetParent(targetCanvas.transform, false);
        StretchToParent(instantiatedRect);
        instantiatedRect.SetAsLastSibling();
        EditorSceneManager.MarkSceneDirty(gameplayUiScene);
        EditorSceneManager.SaveScene(gameplayUiScene, GameplayUiScenePath);
    }

    /// <summary>
    /// Creates or refreshes the authored standalone main-menu scene.
    /// /params None.
    /// /returns None.
    /// </summary>
    private static void EnsureMainMenuScene()
    {
        EnsureFolder(Path.GetDirectoryName(MainMenuScenePath));
        Scene mainMenuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateMainMenuCamera(mainMenuScene);
        Canvas canvas = CreateCanvasRoot("Canvas_MainMenu");
        EventSystem eventSystem = CreateEventSystem("EventSystem");
        GameObject menuRoot = new GameObject("MainMenuRoot", typeof(RectTransform), typeof(MainMenuController));
        RectTransform menuRootRect = menuRoot.GetComponent<RectTransform>();
        menuRootRect.SetParent(canvas.transform, false);
        StretchToParent(menuRootRect);

        GameObject backgroundObject = CreateImageObject("Background", menuRoot.transform, BackgroundColor);
        StretchToParent(backgroundObject.GetComponent<RectTransform>());

        GameObject panel = CreatePanel("MainMenuPanel", menuRoot.transform, new Vector2(440f, 340f));
        TMP_Text titleText = CreateTitleText("Title", "NashCore", panel.transform);
        TMP_Text subtitleText = CreateBodyText("Subtitle", "Bullet Heaven / Roguelite", panel.transform, 22f);
        Button playButton = CreateMenuButton("PlayButton", "Play", panel.transform);
        Button quitButton = CreateMenuButton("QuitButton", "Quit", panel.transform);

        EnsureLayout(panel.transform);
        EnsureLayoutElement(titleText.rectTransform.gameObject, 68f);
        EnsureLayoutElement(subtitleText.rectTransform.gameObject, 42f);
        ConfigureVerticalNavigation(playButton, quitButton);

        MainMenuController mainMenuController = menuRoot.GetComponent<MainMenuController>();
        MenuSelectionController selectionController = GetOrAddComponent<MenuSelectionController>(menuRoot);
        SerializedObject serializedController = new SerializedObject(mainMenuController);
        serializedController.Update();
        serializedController.FindProperty("playButton").objectReferenceValue = playButton;
        serializedController.FindProperty("quitButton").objectReferenceValue = quitButton;
        serializedController.FindProperty("eventSystemOverride").objectReferenceValue = eventSystem;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedSelectionController = new SerializedObject(selectionController);
        serializedSelectionController.Update();
        serializedSelectionController.FindProperty("eventSystemOverride").objectReferenceValue = eventSystem;
        serializedSelectionController.FindProperty("defaultSelectable").objectReferenceValue = playButton;
        serializedSelectionController.ApplyModifiedPropertiesWithoutUndo();
        eventSystem.firstSelectedGameObject = playButton.gameObject;

        EditorSceneManager.MarkSceneDirty(mainMenuScene);
        EditorSceneManager.SaveScene(mainMenuScene, MainMenuScenePath);
    }

    /// <summary>
    /// Ensures build settings start from bootstrap, keep menu/gameplay/UI scenes available, and preserve extra entries.
    /// /params None.
    /// /returns None.
    /// </summary>
    private static void EnsureBuildSettings()
    {
        List<EditorBuildSettingsScene> updatedScenes = new List<EditorBuildSettingsScene>();
        AddSceneIfMissing(updatedScenes, BootstrapScenePath);
        AddSceneIfMissing(updatedScenes, MainMenuScenePath);
        AddSceneIfMissing(updatedScenes, GameplayScenePath);
        AddSceneIfMissing(updatedScenes, GameplayUiScenePath);
        EditorBuildSettingsScene[] existingScenes = EditorBuildSettings.scenes;

        for (int sceneIndex = 0; sceneIndex < existingScenes.Length; sceneIndex++)
        {
            string scenePath = existingScenes[sceneIndex].path;

            if (string.Equals(scenePath, BootstrapScenePath, StringComparison.Ordinal))
                continue;

            if (string.Equals(scenePath, MainMenuScenePath, StringComparison.Ordinal))
                continue;

            if (string.Equals(scenePath, GameplayScenePath, StringComparison.Ordinal))
                continue;

            if (string.Equals(scenePath, GameplayUiScenePath, StringComparison.Ordinal))
                continue;

            AddSceneIfMissing(updatedScenes, scenePath);
        }

        EditorBuildSettings.scenes = updatedScenes.ToArray();
    }
    #endregion

    #region UI Creation
    /// <summary>
    /// Creates one full-screen overlay root used by authored gameplay menus.
    /// /params objectName Name assigned to the created overlay object.
    /// /params parent Parent transform that receives the overlay root.
    /// /returns Created overlay GameObject.
    /// </summary>
    private static GameObject CreateOverlayRoot(string objectName, Transform parent)
    {
        GameObject overlayObject = CreateImageObject(objectName, parent, OverlayColor);
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        StretchToParent(overlayRect);
        return overlayObject;
    }

    /// <summary>
    /// Creates one centered panel object used by the main menu and gameplay overlays.
    /// /params objectName Name assigned to the created panel object.
    /// /params parent Parent transform that receives the panel.
    /// /params size Fixed panel size in pixels.
    /// /returns Created panel GameObject.
    /// </summary>
    private static GameObject CreatePanel(string objectName, Transform parent, Vector2 size)
    {
        GameObject panelObject = CreateImageObject(objectName, parent, PanelColor);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = size;
        panelRect.anchoredPosition = Vector2.zero;
        return panelObject;
    }

    /// <summary>
    /// Creates one title text element under the given parent using the shared menu style.
    /// /params objectName Name assigned to the created text object.
    /// /params text Displayed label.
    /// /params parent Parent transform that receives the text.
    /// /returns Created TMP text component.
    /// </summary>
    private static TMP_Text CreateTitleText(string objectName, string text, Transform parent)
    {
        return CreateText(objectName, text, parent, 42f, FontStyles.Bold, TextAlignmentOptions.Center);
    }

    /// <summary>
    /// Creates one body text element under the given parent using the shared menu style.
    /// /params objectName Name assigned to the created text object.
    /// /params text Displayed label.
    /// /params parent Parent transform that receives the text.
    /// /params fontSize Point size used for the created label.
    /// /returns Created TMP text component.
    /// </summary>
    private static TMP_Text CreateBodyText(string objectName, string text, Transform parent, float fontSize)
    {
        return CreateText(objectName, text, parent, fontSize, FontStyles.Normal, TextAlignmentOptions.Center);
    }

    /// <summary>
    /// Creates one TMP text element with the requested visual settings.
    /// /params objectName Name assigned to the created text object.
    /// /params text Displayed label.
    /// /params parent Parent transform that receives the text.
    /// /params fontSize Point size used for the created label.
    /// /params fontStyle Font style used by the created label.
    /// /params alignment Alignment used by the created label.
    /// /returns Created TMP text component.
    /// </summary>
    private static TMP_Text CreateText(string objectName,
                                       string text,
                                       Transform parent,
                                       float fontSize,
                                       FontStyles fontStyle,
                                       TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(parent, false);
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(320f, fontSize + 24f);

        TextMeshProUGUI textComponent = textObject.GetComponent<TextMeshProUGUI>();
        textComponent.font = ResolveFontAsset();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = fontStyle;
        textComponent.alignment = alignment;
        textComponent.color = TextColor;
        textComponent.textWrappingMode = TextWrappingModes.Normal;
        return textComponent;
    }

    /// <summary>
    /// Creates one gameplay-menu button with a centered TMP label and shared navigation styling.
    /// /params objectName Name assigned to the created button object.
    /// /params label Displayed button label.
    /// /params parent Parent transform that receives the button.
    /// /returns Created Button component.
    /// </summary>
    private static Button CreateMenuButton(string objectName, string label, Transform parent)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(MenuSelectableHoverRelay));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(parent, false);
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(260f, 54f);

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = 260f;
        layoutElement.preferredHeight = 54f;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = ButtonColor;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = ButtonHighlightColor;
        colors.selectedColor = ButtonHighlightColor;
        colors.pressedColor = ButtonPressedColor;
        colors.disabledColor = new Color(ButtonColor.r, ButtonColor.g, ButtonColor.b, 0.45f);
        button.colors = colors;

        TMP_Text labelText = CreateBodyText("Label", label, buttonObject.transform, 24f);
        RectTransform labelRect = labelText.rectTransform;
        StretchToParent(labelRect);
        labelText.margin = new Vector4(8f, 4f, 8f, 4f);
        return button;
    }

    /// <summary>
    /// Creates one image object with RectTransform and Image already configured.
    /// /params objectName Name assigned to the created object.
    /// /params parent Parent transform that receives the object.
    /// /params color Image color assigned to the new object.
    /// /returns Created GameObject.
    /// </summary>
    private static GameObject CreateImageObject(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.SetParent(parent, false);
        imageRect.localScale = Vector3.one;
        imageRect.localPosition = Vector3.zero;
        imageObject.GetComponent<Image>().color = color;
        return imageObject;
    }
    #endregion

    #region Scene Helpers
    /// <summary>
    /// Creates the main-menu camera used by the standalone front-end scene.
    /// /params scene Scene that receives the created camera object.
    /// /returns None.
    /// </summary>
    private static void CreateMainMenuCamera(Scene scene)
    {
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        Camera cameraComponent = cameraObject.GetComponent<Camera>();
        cameraComponent.clearFlags = CameraClearFlags.SolidColor;
        cameraComponent.backgroundColor = BackgroundColor;
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
    }

    /// <summary>
    /// Creates one screen-space overlay canvas with scaler and raycaster configured for authored UI.
    /// /params objectName Name assigned to the canvas root.
    /// /returns Created Canvas component.
    /// </summary>
    private static Canvas CreateCanvasRoot(string objectName)
    {
        GameObject canvasObject = new GameObject(objectName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    /// <summary>
    /// Creates one EventSystem configured for Input System UI navigation.
    /// /params objectName Name assigned to the EventSystem object.
    /// /returns Created EventSystem component.
    /// </summary>
    private static EventSystem CreateEventSystem(string objectName)
    {
        GameObject eventSystemObject = new GameObject(objectName, typeof(EventSystem), typeof(InputSystemUIInputModule), typeof(GameSceneEventSystemCoordinator));
        InputSystemUIInputModule inputModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();
        EventSystem eventSystem = eventSystemObject.GetComponent<EventSystem>();
        GameSceneEventSystemCoordinator coordinator = eventSystemObject.GetComponent<GameSceneEventSystemCoordinator>();
        SerializedObject serializedCoordinator = new SerializedObject(coordinator);
        serializedCoordinator.Update();
        serializedCoordinator.FindProperty("eventSystem").objectReferenceValue = eventSystem;
        serializedCoordinator.ApplyModifiedPropertiesWithoutUndo();
        return eventSystem;
    }

    /// <summary>
    /// Ensures the gameplay UI scene has one EventSystem with Input System UI module ready for authored menus.
    /// /params scene Gameplay UI scene whose EventSystem should be validated.
    /// /returns None.
    /// </summary>
    private static void EnsureSceneEventSystem(Scene scene)
    {
        EventSystem eventSystem = FindComponentInScene<EventSystem>(scene);

        if (eventSystem == null)
            throw new InvalidOperationException("EventSystem not found in gameplay UI scene.");

        InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();

        if (inputModule == null)
            throw new InvalidOperationException("Gameplay UI EventSystem is missing InputSystemUIInputModule.");

        if (inputModule.actionsAsset != null)
            return;

        inputModule.AssignDefaultActions();
        EditorUtility.SetDirty(inputModule);
    }

    /// <summary>
    /// Resolves the most appropriate gameplay UI canvas when the HUD manager is not parented directly under it.
    /// /params scene Gameplay UI scene searched for a valid screen-space UI canvas.
    /// /returns Resolved gameplay canvas or null when none is available.
    /// </summary>
    private static Canvas ResolveGameplayCanvas(Scene scene)
    {
        List<Canvas> canvases = FindComponentsInScene<Canvas>(scene);

        for (int canvasIndex = 0; canvasIndex < canvases.Count; canvasIndex++)
        {
            Canvas candidateCanvas = canvases[canvasIndex];

            if (candidateCanvas == null)
                continue;

            if (candidateCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                continue;

            if (candidateCanvas.GetComponent<GraphicRaycaster>() == null)
                continue;

            return candidateCanvas;
        }

        return null;
    }

    /// <summary>
    /// Removes any pre-existing GameplayMenuController roots from the gameplay UI scene before prefab re-instantiation.
    /// /params scene Gameplay UI scene being refreshed.
    /// /returns None.
    /// </summary>
    private static void RemoveExistingGameplayMenuControllers(Scene scene)
    {
        List<GameplayMenuController> controllers = FindComponentsInScene<GameplayMenuController>(scene);

        for (int controllerIndex = 0; controllerIndex < controllers.Count; controllerIndex++)
        {
            GameplayMenuController controller = controllers[controllerIndex];

            if (controller == null)
                continue;

            UnityEngine.Object.DestroyImmediate(controller.gameObject);
        }
    }

    /// <summary>
    /// Configures one vertical loop of explicit menu navigation for the provided buttons.
    /// /params buttons Ordered button list that should navigate up and down consistently.
    /// /returns None.
    /// </summary>
    private static void ConfigureVerticalNavigation(params Button[] buttons)
    {
        for (int buttonIndex = 0; buttonIndex < buttons.Length; buttonIndex++)
        {
            Button button = buttons[buttonIndex];

            if (button == null)
                continue;

            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = ResolvePreviousButton(buttons, buttonIndex);
            navigation.selectOnDown = ResolveNextButton(buttons, buttonIndex);
            navigation.selectOnLeft = null;
            navigation.selectOnRight = null;
            button.navigation = navigation;
        }
    }
    #endregion

    #endregion
}
