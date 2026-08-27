using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Maintains inline HUD defaults, preauthored supplemental sections, and relays for independently styled menus.
/// </summary>
public static class GameHudSupplementalProjectSetupUtility
{
    #region Constants
    private static readonly string[] MenuPrefabPaths =
    {
        "Assets/Prefabs/UI/PF_GameplayMenus.prefab",
        "Assets/Prefabs/UI/PF_SettingsMenu.prefab",
        "Assets/Prefabs/UI/PF_PowerUpsPanel.prefab"
    };

    private static readonly GameHudPlayerStatistic[] DefaultStatistics =
    {
        GameHudPlayerStatistic.CurrentHealth,
        GameHudPlayerStatistic.MaximumHealth,
        GameHudPlayerStatistic.CurrentShield,
        GameHudPlayerStatistic.MaximumShield,
        GameHudPlayerStatistic.Level,
        GameHudPlayerStatistic.MovementMaximumSpeed,
        GameHudPlayerStatistic.ProjectileDamage,
        GameHudPlayerStatistic.RateOfFire,
        GameHudPlayerStatistic.ExperiencePickupRadius,
        GameHudPlayerStatistic.SynchroValue,
        GameHudPlayerStatistic.RunTimeSeconds
    };
    #endregion

    #region Methods

    #region Entry Points
    /// <summary>
    /// Executes the supplemental setup without exposing a permanent menu command.
    /// </summary>
    // [MenuItem("Tools/Game/HUD/Apply Supplemental HUD Setup")]
    public static void ExecuteBatchSetup()
    {
        GameHudManagerPreset hudPreset = AssetDatabase.LoadAssetAtPath<GameHudManagerPreset>(
            "Assets/Scriptable Objects/Game/HUD/GameHudManagerPreset.asset");
        EnsureDefaultSettings(hudPreset);
        EnsureWaveClearAudioBindings();

        Scene gameplayUiScene = EditorSceneManager.OpenScene(GameSceneManagementProjectSetupUtility.GameplayUiScenePath,
                                                              OpenSceneMode.Single);
        EnsureGameplayUiScene(gameplayUiScene);
        EditorSceneManager.SaveScene(gameplayUiScene);
        EnsureMenuAssets();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GameHudSupplementalProjectSetupUtility] Supplemental HUD sections and menu-button setup completed.");
    }

    /// <summary>
    /// Adds the wave slots and fills missing verified FMOD defaults without replacing authored paths.
    /// </summary>
    private static void EnsureWaveClearAudioBindings()
    {
        GameAudioManagerPreset audioPreset = AssetDatabase.LoadAssetAtPath<GameAudioManagerPreset>(
            "Assets/Scriptable Objects/Game/Audio/GameAudioManagerPreset.asset");

        if (audioPreset == null)
            return;

        int configurationChanges = audioPreset.EnsureDefaultEventBindings();
        configurationChanges += audioPreset.EnsureDefaultEventPaths();

        if (configurationChanges > 0)
            EditorUtility.SetDirty(audioPreset);
    }

    /// <summary>
    /// Initializes inline summary, button, navigation, and Input Action defaults on the HUD preset.
    /// </summary>
    /// <param name="hudPreset">Default HUD preset receiving supplemental defaults.</param>
    public static void EnsureDefaultSettings(GameHudManagerPreset hudPreset)
    {
        if (hudPreset == null)
            return;

        hudPreset.EnsureInitialized();
        SerializedObject serializedHudPreset = new SerializedObject(hudPreset);
        serializedHudPreset.Update();
        EnsureDefaultStatistics(serializedHudPreset);
        EnsureDefaultButtonProfiles(serializedHudPreset.FindProperty("buttonInteractionSettings.menuProfiles"));
        InputActionAsset inputAsset = PlayerInputActionsAssetUtility.LoadOrCreateAsset();
        EnsureDefaultActionReference(serializedHudPreset,
                                     "powerUpSummarySettings.toggleActionId",
                                     inputAsset,
                                     "Player/PowerUpSummaryToggle");
        EnsureDefaultActionReference(serializedHudPreset,
                                     "settingsNavigationSettings.previousTabActionId",
                                     inputAsset,
                                     "UI/SettingsPreviousTab");
        EnsureDefaultActionReference(serializedHudPreset,
                                     "settingsNavigationSettings.nextTabActionId",
                                     inputAsset,
                                     "UI/SettingsNextTab");
        EnsureDefaultActionReference(serializedHudPreset,
                                     "settingsNavigationSettings.verticalNavigationActionId",
                                     inputAsset,
                                     "UI/SettingsNavigateVertical");
        EnsureDefaultActionReference(serializedHudPreset,
                                     "settingsNavigationSettings.horizontalNavigationActionId",
                                     inputAsset,
                                     "UI/SettingsNavigateHorizontal");
        EnsureDefaultActionReference(serializedHudPreset,
                                     "settingsNavigationSettings.submitActionId",
                                     inputAsset,
                                     "UI/Submit");
        EnsureDefaultActionReference(serializedHudPreset,
                                     "settingsNavigationSettings.cancelActionId",
                                     inputAsset,
                                     "UI/Cancel");
        serializedHudPreset.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hudPreset);
    }

    /// <summary>
    /// Ensures preauthored supplemental HUD hierarchies, HUDManager references, and known gameplay-menu relays exist.
    /// </summary>
    /// <param name="gameplayUiScene">Loaded additive gameplay UI scene to update.</param>
    public static void EnsureGameplayUiScene(Scene gameplayUiScene)
    {
        if (!gameplayUiScene.IsValid() || !gameplayUiScene.isLoaded)
            return;

        HUDManager hudManager = GameSceneManagementProjectSetupSceneUtility.FindFirstComponentInScene<HUDManager>(gameplayUiScene);
        Canvas canvas = GameSceneManagementProjectSetupGameplayUiUtility.FindGameplayCanvas(gameplayUiScene);

        if (hudManager == null || canvas == null)
            return;

        HUDPowerUpSummarySection section = GameHudPowerUpSummaryProjectSetupUtility.EnsureSection(canvas);
        HUDWaveClearAnnouncementSection announcementSection =
            GameHudWaveClearAnnouncementProjectSetupUtility.EnsureSection(canvas);
        SerializedObject serializedHudManager = new SerializedObject(hudManager);
        serializedHudManager.Update();
        GameSceneManagementProjectSetupSerializedUtility.SetObjectReference(serializedHudManager,
                                                                            "powerUpSummarySection",
                                                                            section);
        GameSceneManagementProjectSetupSerializedUtility.SetObjectReference(serializedHudManager,
                                                                            "waveClearAnnouncementSection",
                                                                            announcementSection);
        serializedHudManager.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hudManager);
        EnsureRelays(canvas.gameObject);
        EditorSceneManager.MarkSceneDirty(gameplayUiScene);
    }

    /// <summary>
    /// Ensures source menu prefabs and direct Main Menu buttons own preauthored interaction relays.
    /// </summary>
    public static void EnsureMenuAssets()
    {
        EnsureMenuPrefabs();
        EnsureMainMenuScene();
    }

    /// <summary>
    /// Ensures the standard gameplay UI scene is loaded before updating its summary hierarchy and known menu assets.
    /// </summary>
    public static void EnsureLoadedGameplayUiAndMenus()
    {
        GameSceneManagementProjectSetupGameplayUiUtility.EnsureGameplayUiScene();
        Scene gameplayUiScene = SceneManager.GetSceneByPath(GameSceneManagementProjectSetupUtility.GameplayUiScenePath);
        EnsureGameplayUiScene(gameplayUiScene);

        if (gameplayUiScene.IsValid() && gameplayUiScene.isLoaded)
            EditorSceneManager.SaveScene(gameplayUiScene, GameSceneManagementProjectSetupUtility.GameplayUiScenePath);

        EnsureMenuAssets();
    }
    #endregion

    #region Preset Defaults
    /// <summary>
    /// Adds a balanced initial statistic list only when the inline HUD settings contain no authored rows.
    /// </summary>
    /// <param name="serializedHudPreset">Serialized HUD preset receiving initial rows.</param>
    private static void EnsureDefaultStatistics(SerializedObject serializedHudPreset)
    {
        SerializedProperty statisticsProperty = serializedHudPreset.FindProperty("powerUpSummarySettings.statistics");

        if (statisticsProperty == null || statisticsProperty.arraySize > 0)
            return;

        statisticsProperty.arraySize = DefaultStatistics.Length;

        for (int statisticIndex = 0; statisticIndex < DefaultStatistics.Length; statisticIndex++)
        {
            SerializedProperty rowProperty = statisticsProperty.GetArrayElementAtIndex(statisticIndex);
            SetEnum(rowProperty, "statistic", (int)DefaultStatistics[statisticIndex]);
            SetEnum(rowProperty, "valueFormat", (int)GameHudStatisticValueFormat.Automatic);
            SetInt(rowProperty, "decimalPlaces", 1);
            SetFloat(rowProperty, "displayMultiplier", 1f);
            SetBool(rowProperty, "showLabel", true);
            SetString(rowProperty, "trueText", "On");
            SetString(rowProperty, "falseText", "Off");
            SetFloat(rowProperty, "fontSize", 18f);
            SetColor(rowProperty, "color", Color.white);
        }

    }

    /// <summary>
    /// Assigns a stable default Input Action ID only when the inline reference is empty or unresolved.
    /// </summary>
    /// <param name="serializedHudPreset">Serialized HUD preset storing the action ID.</param>
    /// <param name="propertyPath">Serialized string property path.</param>
    /// <param name="inputAsset">Shared project Input Action asset.</param>
    /// <param name="actionPath">Default action path.</param>
    private static void EnsureDefaultActionReference(SerializedObject serializedHudPreset,
                                                     string propertyPath,
                                                     InputActionAsset inputAsset,
                                                     string actionPath)
    {
        if (serializedHudPreset == null || inputAsset == null)
            return;

        SerializedProperty property = serializedHudPreset.FindProperty(propertyPath);

        if (property == null)
            return;

        if (!string.IsNullOrWhiteSpace(property.stringValue) &&
            inputAsset.FindAction(property.stringValue, false) != null)
            return;

        InputAction action = inputAsset.FindAction(actionPath, false);

        if (action != null)
            property.stringValue = action.id.ToString();
    }

    /// <summary>
    /// Adds one independent default profile for every concrete menu group when the list is empty.
    /// </summary>
    /// <param name="profilesProperty">Serialized menu profile array.</param>
    private static void EnsureDefaultButtonProfiles(SerializedProperty profilesProperty)
    {
        if (profilesProperty == null)
            return;

        int previousCount = profilesProperty.arraySize;
        int profileCount = Enum.GetValues(typeof(GameUiMenuKind)).Length;
        profilesProperty.arraySize = profileCount;

        for (int profileIndex = 0; profileIndex < profileCount; profileIndex++)
        {
            SerializedProperty profileProperty = profilesProperty.GetArrayElementAtIndex(profileIndex);
            SetEnum(profileProperty, "menuKind", profileIndex);

            if (profileIndex < previousCount)
                continue;

            SetBool(profileProperty, "isEnabled", true);
            SetEnum(profileProperty, "motionMode", (int)GameUiButtonMotionMode.ManualTransform);
            SetFloat(profileProperty, "transitionDurationSeconds", 0.12f);
            SetBool(profileProperty, "useUnscaledTime", true);
            SetBool(profileProperty, "allowEmptySprites", false);
            SetVector3(profileProperty, "hoverScale", new Vector3(1.04f, 1.04f, 1f));
            SetVector3(profileProperty, "pressedScale", new Vector3(0.98f, 0.98f, 1f));
            SetFloat(profileProperty, "normalFontSize", 24f);
            SetFloat(profileProperty, "emphasizedFontSize", 26f);
            SetColor(profileProperty, "normalGraphicColor", Color.white);
            SetColor(profileProperty, "hoverGraphicColor", Color.white);
            SetColor(profileProperty, "pressedGraphicColor", Color.white);
            SetColor(profileProperty, "disabledGraphicColor", new Color(1f, 1f, 1f, 0.45f));
            SetColor(profileProperty, "normalTextColor", Color.white);
            SetColor(profileProperty, "hoverTextColor", Color.white);
            SetColor(profileProperty, "pressedTextColor", Color.white);
            SetColor(profileProperty, "disabledTextColor", new Color(1f, 1f, 1f, 0.45f));
        }
    }
    #endregion

    #region Menu Relays
    /// <summary>
    /// Ensures known source prefabs own relays so scene instances require no runtime component creation.
    /// </summary>
    private static void EnsureMenuPrefabs()
    {
        for (int prefabIndex = 0; prefabIndex < MenuPrefabPaths.Length; prefabIndex++)
        {
            string prefabPath = MenuPrefabPaths[prefabIndex];
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                if (EnsureRelays(prefabRoot))
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }

    /// <summary>
    /// Ensures direct Main Menu buttons own relays while preserving the caller's loaded scene set.
    /// </summary>
    private static void EnsureMainMenuScene()
    {
        Scene mainMenuScene = EditorSceneManager.OpenScene(GameSceneManagementProjectSetupUtility.MainMenuScenePath,
                                                           OpenSceneMode.Additive);
        GameObject[] roots = mainMenuScene.GetRootGameObjects();
        bool changed = false;

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            changed |= EnsureRelays(roots[rootIndex]);

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(mainMenuScene);
            EditorSceneManager.SaveScene(mainMenuScene);
        }

        EditorSceneManager.CloseScene(mainMenuScene, true);
    }

    /// <summary>
    /// Adds or updates relays for known menu buttons below one authored hierarchy.
    /// </summary>
    /// <param name="root">Authored hierarchy searched for Buttons.</param>
    /// <returns>True when any component or serialized menu assignment changed.</returns>
    private static bool EnsureRelays(GameObject root)
    {
        if (root == null)
            return false;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        bool changed = false;

        for (int buttonIndex = 0; buttonIndex < buttons.Length; buttonIndex++)
        {
            Button button = buttons[buttonIndex];
            if (!TryResolveMenuKind(button.transform, out GameUiMenuKind menuKind))
                continue;

            MenuSelectableHoverRelay relay = button.GetComponent<MenuSelectableHoverRelay>();

            if (relay == null)
            {
                relay = button.gameObject.AddComponent<MenuSelectableHoverRelay>();
                changed = true;
            }

            SerializedObject serializedRelay = new SerializedObject(relay);
            serializedRelay.Update();
            SerializedProperty menuKindProperty = serializedRelay.FindProperty("menuKind");

            if (menuKindProperty != null && menuKindProperty.enumValueIndex != (int)menuKind)
            {
                menuKindProperty.enumValueIndex = (int)menuKind;
                serializedRelay.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(relay);
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Resolves a concrete profile from known parent components and stable authored hierarchy names.
    /// </summary>
    /// <param name="buttonTransform">Button transform whose ancestry is inspected.</param>
    /// <param name="menuKind">Concrete menu group when the hierarchy is recognized.</param>
    /// <returns>True when the button belongs to a supported preauthored menu.</returns>
    private static bool TryResolveMenuKind(Transform buttonTransform, out GameUiMenuKind menuKind)
    {
        if (buttonTransform.GetComponentInParent<MainMenuController>(true) != null)
        {
            menuKind = GameUiMenuKind.MainMenu;
            return true;
        }

        if (buttonTransform.GetComponentInParent<SettingsMenuController>(true) != null)
        {
            menuKind = GameUiMenuKind.SettingsMenu;
            return true;
        }

        if (buttonTransform.GetComponentInParent<HUDPowerUpSummarySection>(true) != null)
        {
            menuKind = GameUiMenuKind.PowerUpSummary;
            return true;
        }

        if (buttonTransform.GetComponentInParent<HUDPowerUpContainerInteractionSection>(true) != null)
        {
            menuKind = GameUiMenuKind.PowerUpContainer;
            return true;
        }

        if (buttonTransform.GetComponentInParent<HUDMilestoneSelectionSection>(true) != null ||
            ContainsInAncestry(buttonTransform, "PowerUpsPanel"))
        {
            menuKind = GameUiMenuKind.MilestoneSelection;
            return true;
        }

        if (buttonTransform.GetComponentInParent<EnemySpawnerRuntimeToolPanelController>(true) != null)
        {
            menuKind = GameUiMenuKind.RuntimeTools;
            return true;
        }

        if (buttonTransform.GetComponentInParent<GameplayMenuController>(true) != null ||
            ContainsInAncestry(buttonTransform, "GameplayMenus"))
        {
            menuKind = ContainsInAncestry(buttonTransform, "Ending")
                ? GameUiMenuKind.EndingMenu
                : GameUiMenuKind.PauseMenu;
            return true;
        }

        menuKind = default;
        return false;
    }

    /// <summary>
    /// Checks whether a transform or ancestor name contains one case-insensitive token.
    /// </summary>
    /// <param name="current">Starting transform.</param>
    /// <param name="token">Hierarchy-name token to find.</param>
    /// <returns>True when one ancestry name contains the token.</returns>
    private static bool ContainsInAncestry(Transform current, string token)
    {
        while (current != null)
        {
            if (current.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            current = current.parent;
        }

        return false;
    }
    #endregion

    #region Serialized Helpers
    /// <summary>
    /// Sets one relative Boolean property when available.
    /// </summary>
    /// <param name="root">Parent serialized property.</param>
    /// <param name="name">Relative field name.</param>
    /// <param name="value">Boolean value to assign.</param>
    private static void SetBool(SerializedProperty root, string name, bool value)
    {
        SerializedProperty property = root.FindPropertyRelative(name);

        if (property != null)
            property.boolValue = value;
    }

    /// <summary>
    /// Sets one relative integer property when available.
    /// </summary>
    /// <param name="root">Parent serialized property.</param>
    /// <param name="name">Relative field name.</param>
    /// <param name="value">Integer value to assign.</param>
    private static void SetInt(SerializedProperty root, string name, int value)
    {
        SerializedProperty property = root.FindPropertyRelative(name);

        if (property != null)
            property.intValue = value;
    }

    /// <summary>
    /// Sets one relative enum property when available.
    /// </summary>
    /// <param name="root">Parent serialized property.</param>
    /// <param name="name">Relative field name.</param>
    /// <param name="value">Enum index to assign.</param>
    private static void SetEnum(SerializedProperty root, string name, int value)
    {
        SerializedProperty property = root.FindPropertyRelative(name);

        if (property != null)
            property.enumValueIndex = value;
    }

    /// <summary>
    /// Sets one relative float property when available.
    /// </summary>
    /// <param name="root">Parent serialized property.</param>
    /// <param name="name">Relative field name.</param>
    /// <param name="value">Float value to assign.</param>
    private static void SetFloat(SerializedProperty root, string name, float value)
    {
        SerializedProperty property = root.FindPropertyRelative(name);

        if (property != null)
            property.floatValue = value;
    }

    /// <summary>
    /// Sets one relative string property when available.
    /// </summary>
    /// <param name="root">Parent serialized property.</param>
    /// <param name="name">Relative field name.</param>
    /// <param name="value">String value to assign.</param>
    private static void SetString(SerializedProperty root, string name, string value)
    {
        SerializedProperty property = root.FindPropertyRelative(name);

        if (property != null)
            property.stringValue = value;
    }

    /// <summary>
    /// Sets one relative Vector3 property when available.
    /// </summary>
    /// <param name="root">Parent serialized property.</param>
    /// <param name="name">Relative field name.</param>
    /// <param name="value">Vector value to assign.</param>
    private static void SetVector3(SerializedProperty root, string name, Vector3 value)
    {
        SerializedProperty property = root.FindPropertyRelative(name);

        if (property != null)
            property.vector3Value = value;
    }

    /// <summary>
    /// Sets one relative color property when available.
    /// </summary>
    /// <param name="root">Parent serialized property.</param>
    /// <param name="name">Relative field name.</param>
    /// <param name="value">Color value to assign.</param>
    private static void SetColor(SerializedProperty root, string name, Color value)
    {
        SerializedProperty property = root.FindPropertyRelative(name);

        if (property != null)
            property.colorValue = value;
    }
    #endregion

    #endregion
}
