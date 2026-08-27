using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using static PlayerGameplayMenuSetupSharedUtility;

/// <summary>
/// Installs the shared Settings menu into the existing authored main-menu and gameplay-menu surfaces without rebuilding
/// their visual hierarchy.
/// </summary>
public static class PlayerGameplayMenuSetupUtility
{
    #region Constants
    private const string GameplayMenusPrefabPath = "Assets/Prefabs/UI/PF_GameplayMenus.prefab";
    private const string MainMenuScenePath = "Assets/Scenes/Testing/Main Scenes/UI/SCN_MainMenu.unity";
    private const string GameplayUiScenePath = GameSceneManagementProjectSetupUtility.GameplayUiScenePath;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Refreshes the reusable Settings prefab and injects references into existing menu assets without replacing layout.
    /// </summary>
    public static void ExecuteSetup()
    {
        GameObject settingsMenuPrefab = PlayerSettingsMenuSetupUtility.EnsureSettingsMenuPrefab();
        GameObject gameplayMenusPrefab = EnsureGameplayMenusPrefab();
        EnsureMainMenuScene(settingsMenuPrefab);
        EnsureGameplayUiScene(gameplayMenusPrefab, settingsMenuPrefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    #endregion

    #region Prefab Setup
    /// <summary>
    /// Adds the Settings button to the existing pause-menu prefab and assigns it to the GameplayMenuController.
    /// </summary>
    /// <returns>Patched gameplay-menu prefab asset.</returns>
    private static GameObject EnsureGameplayMenusPrefab()
    {
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayMenusPrefabPath);

        if (existingPrefab == null)
            throw new InvalidOperationException("Gameplay menus prefab is missing at '" + GameplayMenusPrefabPath + "'.");

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(GameplayMenusPrefabPath);

        try
        {
            GameplayMenuController controller = GetOrAddComponent<GameplayMenuController>(prefabRoot);
            Button resumeButton = ResolveButton(controller, "resumeButton") ?? FindButton(prefabRoot.transform, "ResumeButton");
            Button restartButton = ResolveButton(controller, "pauseRestartButton") ?? FindButton(prefabRoot.transform, "RestartButton");
            Button mainMenuButton = ResolveButton(controller, "pauseMainMenuButton") ?? FindButton(prefabRoot.transform, "MainMenuButton");
            Button quitButton = ResolveButton(controller, "pauseQuitButton") ?? FindButton(prefabRoot.transform, "QuitButton");
            Button settingsButton = ResolveButton(controller, "pauseSettingsButton") ?? FindButton(prefabRoot.transform, "SettingsButton");

            if (settingsButton == null)
                settingsButton = CreateSiblingButton(resumeButton,
                                                     restartButton,
                                                     "SettingsButton",
                                                     "Settings");

            AssignObject(controller, "pauseSettingsButton", settingsButton);
            ConfigureVerticalNavigation(resumeButton, settingsButton, restartButton, mainMenuButton, quitButton);
            RefreshSelectionDefault(prefabRoot, resumeButton);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, GameplayMenusPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        GameObject patchedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayMenusPrefabPath);

        if (patchedPrefab == null)
            throw new InvalidOperationException("Failed to reload patched gameplay menus prefab.");

        return patchedPrefab;
    }
    #endregion

    #region Scene Setup
    /// <summary>
    /// Adds the Settings button and shared Settings menu instance to the existing authored main-menu scene.
    /// </summary>
    /// <param name="settingsMenuPrefab">Shared Settings menu prefab created by the Settings setup utility.</param>
    private static void EnsureMainMenuScene(GameObject settingsMenuPrefab)
    {
        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath);

        if (sceneAsset == null)
            throw new InvalidOperationException("Main menu scene is missing at '" + MainMenuScenePath + "'.");

        Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        MainMenuController menuController = FindComponentInScene<MainMenuController>(scene);

        if (menuController == null)
            throw new InvalidOperationException("MainMenuController not found in the authored main-menu scene.");

        Button playButton = ResolveButton(menuController, "playButton") ?? FindButton(menuController.transform, "PlayButton");
        Button spawnerToolButton = ResolveButton(menuController, "enemySpawnerToolButton") ?? FindButton(menuController.transform, "EnemySpawnerToolButton");
        Button quitButton = ResolveButton(menuController, "quitButton") ?? FindButton(menuController.transform, "QuitButton");
        Button settingsButton = ResolveButton(menuController, "settingsButton") ?? FindButton(menuController.transform, "SettingsButton");

        if (playButton == null || quitButton == null)
            throw new InvalidOperationException("Main menu Play/Quit buttons are required before Settings can be injected.");

        if (settingsButton == null)
            settingsButton = CreateSiblingButton(playButton,
                                                 spawnerToolButton != null ? spawnerToolButton : quitButton,
                                                 "SettingsButton",
                                                 "Settings");

        Canvas canvas = playButton.GetComponentInParent<Canvas>(true);

        if (canvas == null)
            throw new InvalidOperationException("Main menu Canvas not found for Settings menu injection.");

        EventSystem eventSystem = EnsureSceneEventSystem(scene);
        SettingsMenuController settingsMenu = PlayerSettingsMenuSetupUtility.InstantiateSettingsMenu(settingsMenuPrefab,
                                                                                                    canvas.transform,
                                                                                                    eventSystem);
        AssignObject(menuController, "settingsButton", settingsButton);
        AssignObject(menuController, "settingsMenu", settingsMenu);
        AssignObject(menuController, "eventSystemOverride", eventSystem);
        RefreshMainMenuNavigation(playButton, settingsButton, spawnerToolButton, quitButton);
        RefreshSelectionController(menuController.gameObject, playButton, eventSystem);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, MainMenuScenePath);
    }

    /// <summary>
    /// Binds the shared Settings menu instance to the existing gameplay UI scene pause-menu controller.
    /// </summary>
    /// <param name="gameplayMenusPrefab">Gameplay menu prefab patched by <see cref="EnsureGameplayMenusPrefab"/>.</param>
    /// <param name="settingsMenuPrefab">Shared Settings menu prefab created by the Settings setup utility.</param>
    private static void EnsureGameplayUiScene(GameObject gameplayMenusPrefab, GameObject settingsMenuPrefab)
    {
        SceneAsset gameplayUiSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameplayUiScenePath);

        if (gameplayUiSceneAsset == null)
            throw new InvalidOperationException("Gameplay UI scene is missing at '" + GameplayUiScenePath + "'.");

        Scene scene = EditorSceneManager.OpenScene(GameplayUiScenePath, OpenSceneMode.Single);
        GameplayMenuController gameplayMenuController = FindComponentInScene<GameplayMenuController>(scene);
        Canvas canvas = ResolveGameplayCanvas(scene, gameplayMenuController);

        if (canvas == null)
            throw new InvalidOperationException("Gameplay UI Canvas not found for Settings menu injection.");

        if (gameplayMenuController == null)
            gameplayMenuController = InstantiateGameplayMenus(gameplayMenusPrefab, canvas.transform, scene);

        EventSystem eventSystem = EnsureSceneEventSystem(scene);
        SettingsMenuController settingsMenu = PlayerSettingsMenuSetupUtility.InstantiateSettingsMenu(settingsMenuPrefab,
                                                                                                    canvas.transform,
                                                                                                    eventSystem);

        if (gameplayMenuController != null)
            AssignObject(gameplayMenuController, "settingsMenu", settingsMenu);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, GameplayUiScenePath);
    }
    #endregion

    #region Button Helpers
    /// <summary>
    /// Creates one button by cloning an existing sibling so authored visual styling, layout and font choices are kept.
    /// </summary>
    /// <param name="templateButton">Button used as the visual template.</param>
    /// <param name="insertBeforeButton">Button that should follow the created button, or null to append.</param>
    /// <param name="objectName">Name assigned to the created button GameObject.</param>
    /// <param name="label">Visible button label.</param>
    /// <returns>Created or updated Button component.</returns>
    private static Button CreateSiblingButton(Button templateButton, Button insertBeforeButton, string objectName, string label)
    {
        if (templateButton == null)
            throw new InvalidOperationException("A template button is required to create '" + objectName + "'.");

        Transform parent = templateButton.transform.parent;
        GameObject buttonObject = Object.Instantiate(templateButton.gameObject, parent);
        buttonObject.name = objectName;
        buttonObject.SetActive(true);

        if (insertBeforeButton != null)
            buttonObject.transform.SetSiblingIndex(insertBeforeButton.transform.GetSiblingIndex());
        else
            buttonObject.transform.SetAsLastSibling();

        Button button = buttonObject.GetComponent<Button>();

        if (button == null)
            button = buttonObject.AddComponent<Button>();

        button.onClick.RemoveAllListeners();
        SetButtonLabel(buttonObject, label);
        return button;
    }

    /// <summary>
    /// Updates every TMP label found under a cloned button so the visible caption matches the desired command.
    /// </summary>
    /// <param name="buttonObject">Button GameObject whose text should be updated.</param>
    /// <param name="label">Visible button label.</param>
    private static void SetButtonLabel(GameObject buttonObject, string label)
    {
        TMP_Text[] texts = buttonObject.GetComponentsInChildren<TMP_Text>(true);

        for (int textIndex = 0; textIndex < texts.Length; textIndex++)
            texts[textIndex].text = label;
    }

    /// <summary>
    /// Applies explicit cyclic vertical navigation to the main menu button chain.
    /// </summary>
    /// <param name="playButton">Play button.</param>
    /// <param name="settingsButton">Settings button.</param>
    /// <param name="spawnerToolButton">Optional runtime spawner tool button.</param>
    /// <param name="quitButton">Quit button.</param>
    private static void RefreshMainMenuNavigation(Button playButton, Button settingsButton, Button spawnerToolButton, Button quitButton)
    {
        if (spawnerToolButton != null)
        {
            ConfigureVerticalNavigation(playButton, settingsButton, spawnerToolButton, quitButton);
            return;
        }

        ConfigureVerticalNavigation(playButton, settingsButton, quitButton);
    }

    /// <summary>
    /// Applies explicit cyclic vertical navigation to any non-null ordered button list.
    /// </summary>
    /// <param name="buttons">Buttons ordered from top to bottom.</param>
    private static void ConfigureVerticalNavigation(params Button[] buttons)
    {
        MenuVerticalNavigationUtility.ConfigureCyclic(buttons);
    }
    #endregion

    #region Scene Helpers
    /// <summary>
    /// Resolves the gameplay UI canvas from the pause menu controller, HUD manager or first canvas in the scene.
    /// </summary>
    /// <param name="scene">Opened gameplay UI scene.</param>
    /// <param name="gameplayMenuController">Optional gameplay menu controller already present in the scene.</param>
    /// <returns>Resolved gameplay UI Canvas, or null when none exists.</returns>
    private static Canvas ResolveGameplayCanvas(Scene scene, GameplayMenuController gameplayMenuController)
    {
        if (gameplayMenuController != null)
        {
            Canvas menuCanvas = gameplayMenuController.GetComponentInParent<Canvas>(true);

            if (menuCanvas != null)
                return menuCanvas;
        }

        HUDManager hudManager = FindComponentInScene<HUDManager>(scene);

        if (hudManager != null)
        {
            Canvas hudCanvas = hudManager.GetComponentInParent<Canvas>(true);

            if (hudCanvas != null)
                return hudCanvas;
        }

        return FindComponentInScene<Canvas>(scene);
    }

    /// <summary>
    /// Instantiates the gameplay menus prefab only when the gameplay UI scene does not already contain one.
    /// </summary>
    /// <param name="gameplayMenusPrefab">Gameplay menu prefab to instantiate.</param>
    /// <param name="canvasTransform">Canvas transform receiving the instance.</param>
    /// <param name="scene">Scene that owns the instance.</param>
    /// <returns>GameplayMenuController on the instantiated prefab instance.</returns>
    private static GameplayMenuController InstantiateGameplayMenus(GameObject gameplayMenusPrefab, Transform canvasTransform, Scene scene)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(gameplayMenusPrefab, scene) as GameObject;

        if (instance == null)
            throw new InvalidOperationException("Unable to instantiate gameplay menus prefab into gameplay UI scene.");

        RectTransform rectTransform = EnsureRectTransform(instance);
        rectTransform.SetParent(canvasTransform, false);
        StretchToParent(rectTransform);
        rectTransform.SetAsLastSibling();
        return instance.GetComponent<GameplayMenuController>();
    }

    /// <summary>
    /// Ensures one EventSystem exists in the scene and has the Input System UI module required by generated menu focus.
    /// </summary>
    /// <param name="scene">Opened scene searched for an EventSystem.</param>
    /// <returns>Existing or created EventSystem.</returns>
    private static EventSystem EnsureSceneEventSystem(Scene scene)
    {
        EventSystem eventSystem = FindComponentInScene<EventSystem>(scene);

        if (eventSystem != null)
        {
            GetOrAddComponent<InputSystemUIInputModule>(eventSystem.gameObject);
            return eventSystem;
        }

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
        return eventSystemObject.GetComponent<EventSystem>();
    }
    #endregion

    #region Serialized Helpers
    /// <summary>
    /// Resolves a Button reference from a serialized object field.
    /// </summary>
    /// <param name="target">Object containing the serialized field.</param>
    /// <param name="fieldName">Serialized field name.</param>
    /// <returns>Button reference assigned to the field, or null when missing.</returns>
    private static Button ResolveButton(Object target, string fieldName)
    {
        return ResolveObject<Button>(target, fieldName);
    }

    /// <summary>
    /// Resolves a typed Unity object reference from one serialized field.
    /// </summary>
    /// <param name="target">Object containing the serialized field.</param>
    /// <param name="fieldName">Serialized field name.</param>
    /// <returns>Object reference assigned to the field, or null when missing.</returns>
    private static TObject ResolveObject<TObject>(Object target, string fieldName) where TObject : Object
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        return property != null ? property.objectReferenceValue as TObject : null;
    }

    /// <summary>
    /// Assigns one object reference to a serialized field when the field exists.
    /// </summary>
    /// <param name="target">Object receiving the assignment.</param>
    /// <param name="fieldName">Serialized field name.</param>
    /// <param name="value">Object reference value.</param>
    private static void AssignObject(Object target, string fieldName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty(fieldName);

        if (property == null)
            return;

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    /// <summary>
    /// Refreshes the MenuSelectionController default selection after a menu button chain changes.
    /// </summary>
    /// <param name="rootObject">Root GameObject containing or receiving the selection controller.</param>
    /// <param name="defaultButton">Button selected by default.</param>
    private static void RefreshSelectionDefault(GameObject rootObject, Button defaultButton)
    {
        MenuSelectionController selectionController = GetOrAddComponent<MenuSelectionController>(rootObject);
        AssignObject(selectionController, "defaultSelectable", defaultButton);
    }

    /// <summary>
    /// Refreshes main-menu selection controller bindings after the Settings overlay is installed.
    /// </summary>
    /// <param name="rootObject">Main menu root object.</param>
    /// <param name="defaultButton">Button selected by default.</param>
    /// <param name="eventSystem">Scene EventSystem used by the menu.</param>
    private static void RefreshSelectionController(GameObject rootObject, Button defaultButton, EventSystem eventSystem)
    {
        MenuSelectionController selectionController = GetOrAddComponent<MenuSelectionController>(rootObject);
        AssignObject(selectionController, "eventSystemOverride", eventSystem);
        AssignObject(selectionController, "defaultSelectable", defaultButton);

        if (eventSystem.firstSelectedGameObject == null && defaultButton != null)
            eventSystem.firstSelectedGameObject = defaultButton.gameObject;
    }
    #endregion

    #region Search Helpers
    /// <summary>
    /// Finds one Button by GameObject name under a root transform.
    /// </summary>
    /// <param name="root">Root transform searched recursively.</param>
    /// <param name="objectName">Button GameObject name.</param>
    /// <returns>Matching Button, or null when not found.</returns>
    private static Button FindButton(Transform root, string objectName)
    {
        Button[] buttons = root.GetComponentsInChildren<Button>(true);

        for (int buttonIndex = 0; buttonIndex < buttons.Length; buttonIndex++)
        {
            if (string.Equals(buttons[buttonIndex].gameObject.name, objectName, StringComparison.Ordinal))
                return buttons[buttonIndex];
        }

        return null;
    }
    #endregion

    #endregion
}
