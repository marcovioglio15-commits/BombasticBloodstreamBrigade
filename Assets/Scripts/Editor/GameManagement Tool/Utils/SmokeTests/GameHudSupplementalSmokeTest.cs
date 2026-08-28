using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Verifies supplemental HUD baking, authored presentation pools, menu relays, and drop-system ordering.
/// </summary>
public static class GameHudSupplementalSmokeTest
{
    #region Constants
    private const string InvalidDropOrderingWarning =
        "Ignoring invalid [Unity.Entities.UpdateBeforeAttribute] attribute on EnemyExperienceDropSpawnSystem";

    private static readonly string[] MenuPrefabPaths =
    {
        "Assets/Prefabs/UI/PF_GameplayMenus.prefab",
        "Assets/Prefabs/UI/PF_SettingsMenu.prefab",
        "Assets/Prefabs/UI/PF_PowerUpsPanel.prefab"
    };
    #endregion

    #region Fields
    private static bool invalidDropOrderingObserved;
    #endregion

    #region Methods

    #region Entry Point
    /// <summary>
    /// Runs deterministic asset, bake, ECS-resolution, scene-binding, and prefab-relay checks in batch mode.
    /// </summary>
    // [MenuItem("Tools/Game/HUD/Run Supplemental HUD Smoke Test")]
    public static void Run()
    {
        GameHudManagerPreset hudPreset = AssetDatabase.LoadAssetAtPath<GameHudManagerPreset>(
            "Assets/Scriptable Objects/Game/HUD/GameHudManagerPreset.asset");
        Require(hudPreset != null, "Default HUD Manager preset is missing.");
        Require(hudPreset.PowerUpSummarySettings != null, "Default HUD preset does not contain inline Power-Up Summary settings.");
        Require(hudPreset.WaveClearAnnouncementSettings != null,
                "Default HUD preset does not contain Wave Clear Announcement settings.");
        ValidatePreset(hudPreset);
        GameHudWaveClearAnnouncementSmokeTestUtility.ValidateAudioBindings();
        ValidateInputActions(hudPreset);
        ValidateBakeAndRuntime(hudPreset);
        ValidateExperienceDropSystemOrdering();
        ValidateBootstrapProfileSource(hudPreset);
        ValidateGameplayUiScene();
        ValidateMainMenuScene();
        ValidateMenuPrefabs();
        Debug.Log("[GameHudSupplementalSmokeTest] All checks passed.");
    }
    #endregion

    #region ECS Ordering
    /// <summary>
    /// Builds the production group nesting and verifies that sorting no longer reports the cross-group drop-system warning.
    /// </summary>
    private static void ValidateExperienceDropSystemOrdering()
    {
        invalidDropOrderingObserved = false;
        Application.logMessageReceived += HandleOrderingLog;
        World previousDefaultWorld = World.DefaultGameObjectInjectionWorld;
        World orderingWorld = null;

        try
        {
            orderingWorld = DefaultWorldInitialization.Initialize("ExperienceDropOrderingSmokeTest", false);
        }
        finally
        {
            if (orderingWorld != null && orderingWorld.IsCreated)
            {
                ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(orderingWorld);
                orderingWorld.Dispose();
            }

            World.DefaultGameObjectInjectionWorld = previousDefaultWorld;
            Application.logMessageReceived -= HandleOrderingLog;
        }

        Require(!invalidDropOrderingObserved,
                "Experience-drop system sorting still reports an invalid cross-group UpdateBefore attribute.");
    }

    /// <summary>
    /// Captures only the specific Entities warning that motivated the drop-system ordering fix.
    /// </summary>
    /// <param name="condition">Unity log message.</param>
    /// <param name="stackTrace">Unity stack trace associated with the message.</param>
    /// <param name="type">Unity log severity.</param>
    private static void HandleOrderingLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Warning &&
            condition.IndexOf(InvalidDropOrderingWarning, StringComparison.Ordinal) >= 0)
            invalidDropOrderingObserved = true;
    }
    #endregion

    #region Preset and Bake
    /// <summary>
    /// Validates summary, announcement, button, and Settings navigation through the non-mutating warning path.
    /// </summary>
    /// <param name="hudPreset">Default HUD preset to inspect.</param>
    private static void ValidatePreset(GameHudManagerPreset hudPreset)
    {
        List<string> warnings = new List<string>();
        GameHudSupplementalPresetValidationUtility.ValidatePowerUpSummary(hudPreset.PowerUpSummarySettings, warnings);
        GameHudSupplementalPresetValidationUtility.ValidateWaveClearAnnouncement(
            hudPreset.WaveClearAnnouncementSettings,
            warnings);
        GameHudSupplementalPresetValidationUtility.ValidateButtonInteractions(hudPreset.ButtonInteractionSettings, warnings);
        GameHudSupplementalPresetValidationUtility.ValidateSettingsNavigation(hudPreset.SettingsNavigationSettings, warnings);
        Require(warnings.Count == 0, "Supplemental HUD validation warnings: " + string.Join(" | ", warnings));
        Require(hudPreset.PowerUpSummarySettings.Statistics.Count > 0, "Default summary statistic list is empty.");
        Require(hudPreset.ButtonInteractionSettings.MenuProfiles.Count == (int)GameUiMenuKind.RuntimeTools + 1,
                "Default HUD preset does not contain one profile for every concrete menu group.");
        Require(!hudPreset.SettingsNavigationSettings.IncludeDropdownHeadersInNavigation,
                "Settings dropdown headers should be excluded from content navigation by default.");
        Require(hudPreset.SettingsNavigationSettings.CustomizeSelectionPresentation,
                "Default Settings navigation does not enable its distinct selection presentation.");
    }

    /// <summary>
    /// Verifies every HUD-selected stable action ID resolves to the expected project action.
    /// </summary>
    /// <param name="hudPreset">Default HUD preset containing selected action IDs.</param>
    private static void ValidateInputActions(GameHudManagerPreset hudPreset)
    {
        InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            PlayerInputActionsAssetUtility.DefaultInputAssetPath);
        Require(inputAsset != null, "Shared Input Action asset is missing.");
        RequireAction(inputAsset, hudPreset.PowerUpSummarySettings.ToggleActionId, "Player/PowerUpSummaryToggle");
        GameHudSettingsNavigationSettings navigation = hudPreset.SettingsNavigationSettings;
        RequireAction(inputAsset, navigation.PreviousTabActionId, "UI/SettingsPreviousTab");
        RequireAction(inputAsset, navigation.NextTabActionId, "UI/SettingsNextTab");
        RequireAction(inputAsset, navigation.VerticalNavigationActionId, "UI/SettingsNavigateVertical");
        RequireAction(inputAsset, navigation.HorizontalNavigationActionId, "UI/SettingsNavigateHorizontal");
        RequireAction(inputAsset, navigation.SubmitActionId, "UI/Submit");
        RequireAction(inputAsset, navigation.CancelActionId, "UI/Cancel");
    }

    /// <summary>
    /// Verifies safe config construction, ordered buffer baking, and typed ECS statistic resolution.
    /// </summary>
    /// <param name="hudPreset">Default HUD preset supplying authoring data.</param>
    private static void ValidateBakeAndRuntime(GameHudManagerPreset hudPreset)
    {
        GamePowerUpSummaryRuntimeConfig summaryConfig =
            GameHudSupplementalPresetBakeUtility.BuildSummaryConfig(hudPreset.PowerUpSummarySettings);
        Require(summaryConfig.Enabled != 0, "Baked summary config is disabled.");
        Require(summaryConfig.MaximumVisibleActivePowerUps <= GameHudPowerUpSummarySettings.AuthoredActiveSlotCapacity,
                "Baked active capacity exceeds the authored pool.");
        Require(summaryConfig.MaximumVisiblePassivePowerUps <= GameHudPowerUpSummarySettings.AuthoredPassiveSlotCapacity,
                "Baked passive capacity exceeds the authored pool.");
        Require(summaryConfig.PowerUpVisibility == hudPreset.PowerUpSummarySettings.PowerUpVisibility,
                "Baked summary power-up visibility does not match the HUD preset.");
        GameHudWaveClearAnnouncementRuntimeConfig announcementConfig =
            GameHudSupplementalPresetBakeUtility.BuildWaveClearAnnouncementConfig(
                hudPreset.WaveClearAnnouncementSettings);
        Require(announcementConfig.Enabled != 0, "Baked Wave Clear Announcement config is disabled.");
        Require(announcementConfig.Content.Length > 0,
                "Baked Wave Clear Announcement content is empty.");
        Require(announcementConfig.Direction == hudPreset.WaveClearAnnouncementSettings.Direction,
                "Baked Wave Clear Announcement direction does not match the HUD preset.");
        Require(announcementConfig.PlayAudioEvent ==
                (hudPreset.WaveClearAnnouncementSettings.PlayAudioEvent ? (byte)1 : (byte)0),
                "Baked Wave Clear Announcement audio toggle does not match the HUD preset.");
        Require(announcementConfig.AudioEventId == hudPreset.WaveClearAnnouncementSettings.AudioEventId,
                "Baked Wave Clear Announcement audio event does not match the HUD preset.");
        Require(announcementConfig.UseFinalWaveOverride ==
                (hudPreset.WaveClearAnnouncementSettings.UseFinalWaveOverride ? (byte)1 : (byte)0),
                "Baked terminal-wave override toggle does not match the HUD preset.");
        Require(announcementConfig.FinalWaveContent.ToString() ==
                hudPreset.WaveClearAnnouncementSettings.FinalWaveContent,
                "Baked terminal-wave content does not match the HUD preset.");
        Require(announcementConfig.FinalWaveDirection ==
                hudPreset.WaveClearAnnouncementSettings.FinalWaveDirection,
                "Baked terminal-wave direction does not match the HUD preset.");
        Require(Mathf.Approximately(announcementConfig.FinalWaveTraversalDurationSeconds,
                                    hudPreset.WaveClearAnnouncementSettings.FinalWaveTraversalDurationSeconds),
                "Baked terminal-wave traversal duration does not match the HUD preset.");
        Require(announcementConfig.FinalWaveEasing ==
                hudPreset.WaveClearAnnouncementSettings.FinalWaveEasing,
                "Baked terminal-wave easing does not match the HUD preset.");
        Require(announcementConfig.FinalWavePauseAtCenter ==
                (hudPreset.WaveClearAnnouncementSettings.FinalWavePauseAtCenter ? (byte)1 : (byte)0),
                "Baked terminal-wave pause toggle does not match the HUD preset.");
        Require(Mathf.Approximately(announcementConfig.FinalWaveCenterHoldDurationSeconds,
                                    hudPreset.WaveClearAnnouncementSettings.FinalWaveCenterHoldDurationSeconds),
                "Baked terminal-wave hold duration does not match the HUD preset.");
        Require(announcementConfig.PlayFinalWaveAudioEvent ==
                (hudPreset.WaveClearAnnouncementSettings.PlayFinalWaveAudioEvent ? (byte)1 : (byte)0),
                "Baked terminal-wave audio toggle does not match the HUD preset.");
        Require(announcementConfig.FinalWaveAudioEventId ==
                hudPreset.WaveClearAnnouncementSettings.FinalWaveAudioEventId,
                "Baked terminal-wave audio event does not match the HUD preset.");
        GameHudWaveClearAnnouncementSmokeTestUtility.ValidateRequestRuntime(announcementConfig);
        GameHudSettingsNavigationRuntimeConfig navigationConfig =
            GameHudSupplementalPresetBakeUtility.BuildSettingsNavigationConfig(hudPreset.SettingsNavigationSettings);
        Require(navigationConfig.Enabled != 0, "Baked Settings navigation config is disabled.");
        Require(navigationConfig.IncludeDropdownHeadersInNavigation == 0,
                "Baked Settings navigation includes dropdown headers despite its default policy.");
        Require(navigationConfig.CustomizeSelectionPresentation != 0,
                "Baked Settings selection presentation is disabled.");
        GameHudButtonInteractionSmokeTestUtility.ValidateTextOnlyMotionTargetBake();

        using (World world = new World("GameHudSupplementalSmokeTest", WorldFlags.Game))
        {
            EntityManager entityManager = world.EntityManager;
            Entity configEntity = entityManager.CreateEntity();
            entityManager.AddBuffer<GamePowerUpSummaryStatisticElement>(configEntity);
            entityManager.AddBuffer<GameUiMenuButtonInteractionElement>(configEntity);
            DynamicBuffer<GamePowerUpSummaryStatisticElement> statisticBuffer =
                entityManager.GetBuffer<GamePowerUpSummaryStatisticElement>(configEntity);
            GameHudSupplementalPresetBakeUtility.PopulateStatisticBuffer(hudPreset.PowerUpSummarySettings, statisticBuffer);
            DynamicBuffer<GameUiMenuButtonInteractionElement> buttonBuffer =
                entityManager.GetBuffer<GameUiMenuButtonInteractionElement>(configEntity);
            GameHudSupplementalPresetBakeUtility.PopulateButtonInteractionBuffer(hudPreset.ButtonInteractionSettings, buttonBuffer);
            Require(statisticBuffer.Length == hudPreset.PowerUpSummarySettings.Statistics.Count,
                    "Baked statistic buffer does not preserve the configured row count.");
            Require(buttonBuffer.Length == (int)GameUiMenuKind.RuntimeTools + 1,
                    "Baked button buffer does not contain every concrete menu group.");
            ValidateButtonInteractionBake(hudPreset.ButtonInteractionSettings,
                                          buttonBuffer);
            ValidateTypedStatisticResolution(entityManager);
        }
    }

    /// <summary>
    /// Verifies authored motion-target and empty-sprite choices reach the matching ECS menu-profile element unchanged.
    /// </summary>
    /// <param name="settings">Authored menu-button profiles.</param>
    /// <param name="buttonBuffer">Baked ECS interaction buffer.</param>
    private static void ValidateButtonInteractionBake(
        GameHudButtonInteractionSettings settings,
        DynamicBuffer<GameUiMenuButtonInteractionElement> buttonBuffer)
    {
        IReadOnlyList<GameUiMenuButtonInteractionDefinition> profiles = settings.MenuProfiles;

        // Match by stable menu kind so the test does not depend on list ordering.
        for (int profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
        {
            GameUiMenuButtonInteractionDefinition profile = profiles[profileIndex];

            if (profile == null)
                continue;

            bool found = false;

            for (int elementIndex = 0; elementIndex < buttonBuffer.Length; elementIndex++)
            {
                GameUiMenuButtonInteractionElement element = buttonBuffer[elementIndex];

                if (element.MenuKind != profile.MenuKind)
                    continue;

                Require(element.MotionTarget == profile.MotionTarget,
                        "Baked Motion Target does not match the " + profile.MenuKind + " profile.");
                Require(element.AllowEmptySprites == (profile.AllowEmptySprites ? (byte)1 : (byte)0),
                        "Baked Allow Empty Sprites does not match the " + profile.MenuKind + " profile.");
                found = true;
                break;
            }

            Require(found, "No baked interaction element exists for " + profile.MenuKind + ".");
        }
    }

    /// <summary>
    /// Resolves numeric, Boolean, and token values from ECS components and scalable-stat buffers.
    /// </summary>
    /// <param name="entityManager">Temporary smoke-test entity manager.</param>
    private static void ValidateTypedStatisticResolution(EntityManager entityManager)
    {
        Entity playerEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(playerEntity, new PlayerHealth { Current = 42f, Max = 100f });
        DynamicBuffer<PlayerScalableStatElement> scalableStats = entityManager.AddBuffer<PlayerScalableStatElement>(playerEntity);
        scalableStats.Add(new PlayerScalableStatElement
        {
            Name = new FixedString64Bytes("CriticalEnabled"),
            Type = (byte)PlayerScalableStatType.Boolean,
            BooleanValue = 1
        });
        scalableStats.Add(new PlayerScalableStatElement
        {
            Name = new FixedString64Bytes("DamageElement"),
            Type = (byte)PlayerScalableStatType.Token,
            TokenValue = new FixedString64Bytes("Arcane")
        });

        GamePowerUpSummaryStatisticElement healthDefinition = new GamePowerUpSummaryStatisticElement
        {
            Statistic = GameHudPlayerStatistic.CurrentHealth,
            Label = new FixedString64Bytes("Health"),
            ValueFormat = GameHudStatisticValueFormat.Number,
            DecimalPlaces = 0,
            DisplayMultiplier = 1f,
            ShowLabel = 1
        };
        Require(HUDPowerUpSummaryRuntimeUtility.TryResolveStatistic(entityManager,
                                                                    playerEntity,
                                                                    in healthDefinition,
                                                                    out HUDPowerUpSummaryStatisticValue healthValue),
                "Current health did not resolve from ECS.");
        Require(Mathf.Approximately(healthValue.NumericValue, 42f), "Resolved health value is incorrect.");

        GamePowerUpSummaryStatisticElement booleanDefinition = BuildCustomDefinition("CriticalEnabled",
                                                                                     GameHudStatisticValueFormat.Automatic);
        Require(HUDPowerUpSummaryRuntimeUtility.TryResolveStatistic(entityManager,
                                                                    playerEntity,
                                                                    in booleanDefinition,
                                                                    out HUDPowerUpSummaryStatisticValue booleanValue) &&
                booleanValue.BooleanValue != 0,
                "Boolean scalable stat did not resolve from ECS.");

        GamePowerUpSummaryStatisticElement tokenDefinition = BuildCustomDefinition("DamageElement",
                                                                                   GameHudStatisticValueFormat.Automatic);
        Require(HUDPowerUpSummaryRuntimeUtility.TryResolveStatistic(entityManager,
                                                                    playerEntity,
                                                                    in tokenDefinition,
                                                                    out HUDPowerUpSummaryStatisticValue tokenValue) &&
                tokenValue.TokenValue.Equals(new FixedString64Bytes("Arcane")),
                "Token scalable stat did not resolve from ECS.");
    }

    /// <summary>
    /// Builds one custom scalable-stat definition for deterministic runtime resolution checks.
    /// </summary>
    /// <param name="statName">Stable scalable-stat name.</param>
    /// <param name="format">Requested display format.</param>
    /// <returns>Runtime statistic definition.</returns>
    private static GamePowerUpSummaryStatisticElement BuildCustomDefinition(string statName,
                                                                            GameHudStatisticValueFormat format)
    {
        return new GamePowerUpSummaryStatisticElement
        {
            Statistic = GameHudPlayerStatistic.CustomScalableStat,
            ScalableStatName = new FixedString64Bytes(statName),
            Label = new FixedString64Bytes(statName),
            ValueFormat = format,
            DecimalPlaces = 0,
            DisplayMultiplier = 1f,
            ShowLabel = 1,
            TrueText = new FixedString64Bytes("On"),
            FalseText = new FixedString64Bytes("Off")
        };
    }
    #endregion

    #region Authored UI
    /// <summary>
    /// Verifies the persistent bootstrap authoring resolves the HUD preset that supplies profiles to every menu scene.
    /// </summary>
    /// <param name="hudPreset">Default HUD preset expected from the global Game Master preset.</param>
    private static void ValidateBootstrapProfileSource(GameHudManagerPreset hudPreset)
    {
        Scene scene = EditorSceneManager.OpenScene(GameSceneManagementProjectSetupUtility.BootstrapScenePath,
                                                   OpenSceneMode.Single);
        GameSceneManagerAuthoring authoring =
            GameSceneManagementProjectSetupSceneUtility.FindFirstComponentInScene<GameSceneManagerAuthoring>(scene);
        Require(authoring != null, "Bootstrap scene does not contain GameSceneManagerAuthoring.");
        Require(authoring.ResolveHudManagerPreset() == hudPreset,
                "Bootstrap authoring does not resolve the HUD preset that owns global menu profiles.");
    }

    /// <summary>
    /// Validates HUDManager binding, fixed pool capacities, and the summary toggle relay in the gameplay UI scene.
    /// </summary>
    private static void ValidateGameplayUiScene()
    {
        Scene scene = EditorSceneManager.OpenScene(GameSceneManagementProjectSetupUtility.GameplayUiScenePath,
                                                   OpenSceneMode.Single);
        HUDManager hudManager = GameSceneManagementProjectSetupSceneUtility.FindFirstComponentInScene<HUDManager>(scene);
        List<HUDPowerUpSummarySection> summarySections =
            GameSceneManagementProjectSetupSceneUtility.FindComponentsInScene<HUDPowerUpSummarySection>(scene);
        List<HUDWaveClearAnnouncementSection> announcementSections =
            GameSceneManagementProjectSetupSceneUtility.FindComponentsInScene<HUDWaveClearAnnouncementSection>(scene);
        Require(hudManager != null, "Gameplay UI scene does not contain HUDManager.");
        Require(summarySections.Count == 1,
                "Gameplay UI scene should contain exactly one authored HUDPowerUpSummarySection instance.");
        Require(announcementSections.Count == 1,
                "Gameplay UI scene should contain exactly one authored HUDWaveClearAnnouncementSection instance.");
        HUDPowerUpSummarySection section = summarySections[0];
        Require(PrefabUtility.IsPartOfPrefabInstance(section),
                "Power-Up Summary scene hierarchy is not a prefab instance.");
        GameObject summarySource = PrefabUtility.GetCorrespondingObjectFromSource(section.gameObject);
        Require(summarySource != null &&
                string.Equals(AssetDatabase.GetAssetPath(summarySource),
                              GameHudPowerUpSummaryProjectSetupUtility.SummaryPrefabPath,
                              StringComparison.Ordinal),
                "Power-Up Summary scene instance does not use the dedicated summary prefab.");

        SerializedObject serializedHudManager = new SerializedObject(hudManager);
        serializedHudManager.Update();
        SerializedProperty summaryReferenceProperty = serializedHudManager.FindProperty("powerUpSummarySection");
        UnityEngine.Object linkedSummary = summaryReferenceProperty != null
            ? summaryReferenceProperty.objectReferenceValue
            : null;
        Require(linkedSummary != null, "HUDManager Power-Up Summary reference is missing.");
        Require(linkedSummary == section,
                "HUDManager Power-Up Summary reference mismatch. Linked: " +
                DescribeObject(linkedSummary) + " | Found: " + DescribeObject(section));

        HUDWaveClearAnnouncementSection announcementSection = announcementSections[0];
        Require(PrefabUtility.IsPartOfPrefabInstance(announcementSection),
                "Wave Clear Announcement scene hierarchy is not a prefab instance.");
        GameObject announcementSource =
            PrefabUtility.GetCorrespondingObjectFromSource(announcementSection.gameObject);
        Require(announcementSource != null &&
                string.Equals(AssetDatabase.GetAssetPath(announcementSource),
                              GameHudWaveClearAnnouncementProjectSetupUtility.AnnouncementPrefabPath,
                              StringComparison.Ordinal),
                "Wave Clear Announcement scene instance does not use its dedicated prefab.");
        SerializedProperty announcementReferenceProperty =
            serializedHudManager.FindProperty("waveClearAnnouncementSection");
        UnityEngine.Object linkedAnnouncement = announcementReferenceProperty != null
            ? announcementReferenceProperty.objectReferenceValue
            : null;
        Require(linkedAnnouncement == announcementSection,
                "HUDManager Wave Clear Announcement reference mismatch. Linked: " +
                DescribeObject(linkedAnnouncement) + " | Found: " + DescribeObject(announcementSection));
        SerializedObject serializedAnnouncement = new SerializedObject(announcementSection);
        serializedAnnouncement.Update();
        Require(serializedAnnouncement.FindProperty("presentationRoot").objectReferenceValue != null,
                "Wave Clear Announcement presentation root is missing.");
        Require(serializedAnnouncement.FindProperty("textRoot").objectReferenceValue != null,
                "Wave Clear Announcement text root is missing.");
        Require(serializedAnnouncement.FindProperty("announcementText").objectReferenceValue != null,
                "Wave Clear Announcement TMP text is missing.");
        Require(serializedAnnouncement.FindProperty("canvasGroup").objectReferenceValue != null,
                "Wave Clear Announcement canvas group is missing.");

        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        RequireArray(serializedSection,
                     "activeIconViews",
                     GameHudPowerUpSummarySettings.AuthoredActiveSlotCapacity);
        RequireArray(serializedSection,
                     "passiveIconViews",
                     GameHudPowerUpSummarySettings.AuthoredPassiveSlotCapacity);
        RequireArray(serializedSection,
                     "statisticRows",
                     GameHudPowerUpSummarySettings.AuthoredStatisticRowCapacity);
        Button toggleButton = serializedSection.FindProperty("toggleButton").objectReferenceValue as Button;
        Require(toggleButton != null, "Summary toggle button is missing.");
        Require(toggleButton.GetComponent<MenuSelectableHoverRelay>() != null,
                "Summary toggle does not own a preauthored menu interaction relay.");
    }

    /// <summary>
    /// Builds a compact Editor diagnostic for scene and prefab object-reference mismatches.
    /// </summary>
    /// <param name="target">Unity object to describe.</param>
    /// <returns>Instance, global object, asset, scene, and hierarchy identity.</returns>
    private static string DescribeObject(UnityEngine.Object target)
    {
        if (target == null)
            return "null";

        Component component = target as Component;
        string scenePath = component != null ? component.gameObject.scene.path : string.Empty;
        string hierarchyName = component != null ? component.gameObject.name : target.name;
        GlobalObjectId globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(target);
        return string.Format("name={0}, instance={1}, global={2}, asset={3}, scene={4}",
                             hierarchyName,
                             target.GetInstanceID(),
                             globalObjectId,
                             AssetDatabase.GetAssetPath(target),
                             scenePath);
    }

    /// <summary>
    /// Verifies every direct Main Menu button owns a relay so its global profile applies before gameplay scenes load.
    /// </summary>
    private static void ValidateMainMenuScene()
    {
        ValidateCyclicMenuNavigation();
        Scene scene = EditorSceneManager.OpenScene(GameSceneManagementProjectSetupUtility.MainMenuScenePath,
                                                   OpenSceneMode.Single);
        List<Button> buttons = GameSceneManagementProjectSetupSceneUtility.FindComponentsInScene<Button>(scene);
        List<MainMenuController> controllers =
            GameSceneManagementProjectSetupSceneUtility.FindComponentsInScene<MainMenuController>(scene);
        Require(buttons.Count > 0, "Main Menu scene contains no Buttons.");
        Require(controllers.Count == 1, "Main Menu scene must contain exactly one MainMenuController.");

        SerializedObject serializedController = new SerializedObject(controllers[0]);
        Button playButton = serializedController.FindProperty("playButton").objectReferenceValue as Button;
        Button settingsButton = serializedController.FindProperty("settingsButton").objectReferenceValue as Button;
        Button toolButton = serializedController.FindProperty("enemySpawnerToolButton").objectReferenceValue as Button;
        Button quitButton = serializedController.FindProperty("quitButton").objectReferenceValue as Button;
        Require(playButton != null && settingsButton != null && toolButton != null && quitButton != null,
                "Main Menu controller has an incomplete authored button chain.");
        Require(!toolButton.gameObject.activeSelf,
                "Runtime Spawner Tool button must start inactive until its scripting define is enabled.");
        Require(playButton.navigation.selectOnDown == settingsButton &&
                settingsButton.navigation.selectOnDown == quitButton &&
                quitButton.navigation.selectOnUp == settingsButton &&
                quitButton.navigation.selectOnDown == playButton,
                "Main Menu authored navigation does not bypass the inactive runtime tool button.");

        for (int buttonIndex = 0; buttonIndex < buttons.Count; buttonIndex++)
            Require(buttons[buttonIndex].GetComponent<MenuSelectableHoverRelay>() != null,
                    "Main Menu button " + buttons[buttonIndex].name + " has no interaction relay.");
    }

    /// <summary>
    /// Verifies cyclic menu navigation wraps in both directions while skipping unavailable entries.
    /// </summary>
    private static void ValidateCyclicMenuNavigation()
    {
        GameObject playObject = new GameObject("SmokeTestPlay", typeof(RectTransform), typeof(Button));
        GameObject settingsObject = new GameObject("SmokeTestSettings", typeof(RectTransform), typeof(Button));
        GameObject hiddenObject = new GameObject("SmokeTestHidden", typeof(RectTransform), typeof(Button));
        GameObject quitObject = new GameObject("SmokeTestQuit", typeof(RectTransform), typeof(Button));

        try
        {
            Button playButton = playObject.GetComponent<Button>();
            Button settingsButton = settingsObject.GetComponent<Button>();
            Button hiddenButton = hiddenObject.GetComponent<Button>();
            Button quitButton = quitObject.GetComponent<Button>();
            hiddenObject.SetActive(false);
            MenuVerticalNavigationUtility.ConfigureCyclic(playButton,
                                                          settingsButton,
                                                          hiddenButton,
                                                          quitButton);
            Require(playButton.navigation.selectOnUp == quitButton &&
                    playButton.navigation.selectOnDown == settingsButton,
                    "Main Menu cyclic navigation does not wrap from the first entry.");
            Require(settingsButton.navigation.selectOnUp == playButton &&
                    settingsButton.navigation.selectOnDown == quitButton,
                    "Main Menu cyclic navigation does not skip an unavailable middle entry.");
            Require(quitButton.navigation.selectOnUp == settingsButton &&
                    quitButton.navigation.selectOnDown == playButton,
                    "Main Menu cyclic navigation does not wrap from the last entry.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(playObject);
            UnityEngine.Object.DestroyImmediate(settingsObject);
            UnityEngine.Object.DestroyImmediate(hiddenObject);
            UnityEngine.Object.DestroyImmediate(quitObject);
        }
    }

    /// <summary>
    /// Verifies every Button in the three known source menu prefabs owns a preauthored relay.
    /// </summary>
    private static void ValidateMenuPrefabs()
    {
        for (int prefabIndex = 0; prefabIndex < MenuPrefabPaths.Length; prefabIndex++)
        {
            string prefabPath = MenuPrefabPaths[prefabIndex];
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                Button[] buttons = prefabRoot.GetComponentsInChildren<Button>(true);
                Require(buttons.Length > 0, prefabPath + " contains no Buttons.");

                for (int buttonIndex = 0; buttonIndex < buttons.Length; buttonIndex++)
                    Require(buttons[buttonIndex].GetComponent<MenuSelectableHoverRelay>() != null,
                            prefabPath + " button " + buttons[buttonIndex].name + " has no interaction relay.");

                if (string.Equals(prefabPath,
                                  PlayerSettingsMenuSetupUtility.SettingsMenuPrefabPath,
                                  StringComparison.Ordinal))
                    ValidateSettingsPrefab(prefabRoot);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }

    /// <summary>
    /// Verifies the Settings prefab uses the shared Input Action asset, excludes macro tabs, and preauthors focus indicators.
    /// </summary>
    /// <param name="prefabRoot">Loaded Settings prefab contents.</param>
    private static void ValidateSettingsPrefab(GameObject prefabRoot)
    {
        SettingsMenuController controller = prefabRoot.GetComponent<SettingsMenuController>();
        Require(controller != null, "Settings prefab has no SettingsMenuController.");
        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.Update();
        InputActionAsset navigationAsset =
            serializedController.FindProperty("navigationInputAsset").objectReferenceValue as InputActionAsset;
        Require(navigationAsset != null, "Settings prefab has no shared navigation Input Action asset.");
        Require(string.Equals(AssetDatabase.GetAssetPath(navigationAsset),
                              PlayerInputActionsAssetUtility.DefaultInputAssetPath,
                              StringComparison.Ordinal),
                "Settings prefab navigation does not reference the shared project Input Action asset.");

        Button audioTabButton = serializedController.FindProperty("audioTabButton").objectReferenceValue as Button;
        Button gameplayTabButton = serializedController.FindProperty("gameplayTabButton").objectReferenceValue as Button;
        Require(audioTabButton != null && audioTabButton.navigation.mode == Navigation.Mode.None,
                "Audio macro tab remains in ordinary content navigation.");
        Require(gameplayTabButton != null && gameplayTabButton.navigation.mode == Navigation.Mode.None,
                "Gameplay macro tab remains in ordinary content navigation.");

        Toggle[] toggles = prefabRoot.GetComponentsInChildren<Toggle>(true);
        Slider[] sliders = prefabRoot.GetComponentsInChildren<Slider>(true);
        SettingsDropdownSection[] dropdownSections = prefabRoot.GetComponentsInChildren<SettingsDropdownSection>(true);
        Require(toggles.Length > 0, "Settings prefab contains no navigable Toggle options.");
        Require(sliders.Length > 0, "Settings prefab contains no navigable Slider options.");

        for (int toggleIndex = 0; toggleIndex < toggles.Length; toggleIndex++)
            Require(toggles[toggleIndex].GetComponent<SettingsSelectableFocusIndicator>() != null,
                    "Settings Toggle " + toggles[toggleIndex].name + " has no preauthored selection indicator.");

        for (int sliderIndex = 0; sliderIndex < sliders.Length; sliderIndex++)
            Require(sliders[sliderIndex].GetComponent<SettingsSelectableFocusIndicator>() != null,
                    "Settings Slider " + sliders[sliderIndex].name + " has no preauthored selection indicator.");

        for (int sectionIndex = 0; sectionIndex < dropdownSections.Length; sectionIndex++)
        {
            Button headerButton = dropdownSections[sectionIndex].HeaderButton;
            Require(headerButton != null, "Settings dropdown section has no header button.");
            Require(headerButton.GetComponent<SettingsSelectableFocusIndicator>() != null,
                    "Settings dropdown header " + headerButton.name + " has no preauthored selection indicator.");
        }
    }

    /// <summary>
    /// Verifies one serialized component-reference array has the required size and no missing entry.
    /// </summary>
    /// <param name="serializedObject">Serialized section owning the array.</param>
    /// <param name="propertyName">Serialized field name.</param>
    /// <param name="capacity">Required fixed capacity.</param>
    private static void RequireArray(SerializedObject serializedObject, string propertyName, int capacity)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        Require(property != null && property.isArray, propertyName + " array is missing.");
        Require(property.arraySize == capacity, propertyName + " has an invalid fixed capacity.");

        for (int entryIndex = 0; entryIndex < property.arraySize; entryIndex++)
            Require(property.GetArrayElementAtIndex(entryIndex).objectReferenceValue != null,
                    propertyName + " entry " + entryIndex + " is missing.");
    }
    #endregion

    #region Assertions
    /// <summary>
    /// Verifies one stable action ID resolves to the expected action path.
    /// </summary>
    /// <param name="inputAsset">Shared project Input Action asset.</param>
    /// <param name="actionId">Stable action ID selected in the HUD preset.</param>
    /// <param name="expectedPath">Expected action path.</param>
    private static void RequireAction(InputActionAsset inputAsset, string actionId, string expectedPath)
    {
        InputAction action = inputAsset.FindAction(actionId, false);
        InputAction expectedAction = inputAsset.FindAction(expectedPath, false);
        Require(action != null, expectedPath + " does not resolve from its HUD-selected action ID.");
        Require(action == expectedAction, expectedPath + " HUD-selected action ID targets a different action.");
    }

    /// <summary>
    /// Throws a deterministic failure with one actionable message when a smoke-test condition is unmet.
    /// </summary>
    /// <param name="condition">Condition required for the smoke test to continue.</param>
    /// <param name="message">Failure message.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("GameHudSupplementalSmokeTest: " + message);
    }
    #endregion

    #endregion
}
