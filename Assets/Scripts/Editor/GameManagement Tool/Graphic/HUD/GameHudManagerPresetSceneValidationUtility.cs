using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Validates loaded gameplay HUD scene references used by the HUD Manager preset workflow.
/// </summary>
internal static class GameHudManagerPresetSceneValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Appends warnings for missing HUD scene bindings without opening or mutating scenes.
    /// </summary>
    /// <param name="preset">Selected HUD manager preset used to skip disabled setting-dependent checks.</param>
    /// <param name="warnings">Mutable warning list receiving scene-binding diagnostics.</param>
    public static void CollectWarnings(GameHudManagerPreset preset, List<string> warnings)
    {
        if (warnings == null)
            return;

        Scene scene = FindLoadedGameplayUiScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            warnings.Add("HUD scene references were not checked because SCN_MainScene_UI is not loaded. Open the UI scene or run Game Scene setup to validate bindings.");
            return;
        }

        List<HUDManager> hudManagers = GameSceneManagementProjectSetupSceneUtility.FindComponentsInScene<HUDManager>(scene);

        if (hudManagers.Count <= 0)
        {
            warnings.Add("SCN_MainScene_UI is loaded but no HUDManager was found.");
            return;
        }

        if (hudManagers.Count > 1)
            warnings.Add("SCN_MainScene_UI contains more than one HUDManager. Only the first one is validated by the tool.");

        ValidateHudManagerBindings(hudManagers[0], preset, warnings);
    }
    #endregion

    #region HUD Manager
    /// <summary>
    /// Validates the HUDManager section references and dispatches section-specific checks.
    /// </summary>
    /// <param name="hudManager">Loaded HUDManager instance from SCN_MainScene_UI.</param>
    /// <param name="preset">Selected HUD manager preset used by setting-dependent reference checks.</param>
    /// <param name="warnings">Mutable warning list receiving scene-binding diagnostics.</param>
    private static void ValidateHudManagerBindings(HUDManager hudManager, GameHudManagerPreset preset, List<string> warnings)
    {
        SerializedObject serializedHudManager = new SerializedObject(hudManager);
        serializedHudManager.Update();

        HUDReferenceRootProvider referenceRootProvider = CheckRequiredReference<HUDReferenceRootProvider>(serializedHudManager,
                                                                                                         "referenceRootProvider",
                                                                                                         "HUDManager Reference Root Provider",
                                                                                                         warnings);
        PlayerHealthBarsHudView healthBarsView = CheckRequiredReference<PlayerHealthBarsHudView>(serializedHudManager,
                                                                                                 "playerHealthBarsView",
                                                                                                 "HUDManager Player Health Bars View",
                                                                                                 warnings);
        HUDLevelExperienceSection levelExperienceSection = CheckRequiredReference<HUDLevelExperienceSection>(serializedHudManager,
                                                                                                             "levelExperienceSection",
                                                                                                             "HUDManager Level Experience Section",
                                                                                                             warnings);
        HUDPlayerPortraitSection portraitSection = CheckRequiredReference<HUDPlayerPortraitSection>(serializedHudManager,
                                                                                                    "portraitSection",
                                                                                                    "HUDManager Portrait Section",
                                                                                                    warnings);
        HUDGrowthSequenceSection growthSequenceSection = CheckRequiredReference<HUDGrowthSequenceSection>(serializedHudManager,
                                                                                                          "growthSequenceSection",
                                                                                                          "HUDManager Growth Sequence Section",
                                                                                                          warnings);
        HUDPowerUpOverlaySectionComponent powerUpOverlaySection = CheckRequiredReference<HUDPowerUpOverlaySectionComponent>(serializedHudManager,
                                                                                                                            "powerUpOverlaySection",
                                                                                                                            "HUDManager Power-Up Overlay Section",
                                                                                                                            warnings);
        HUDRunTimerSection runTimerSection = CheckRequiredReference<HUDRunTimerSection>(serializedHudManager,
                                                                                       "runTimerSection",
                                                                                       "HUDManager Run Timer Section",
                                                                                       warnings);
        HUDComboCounterSection comboCounterSection = CheckRequiredReference<HUDComboCounterSection>(serializedHudManager,
                                                                                                    "comboCounterSection",
                                                                                                    "HUDManager Synchro Meter Section",
                                                                                                    warnings);
        HUDMilestoneSelectionSection milestoneSelectionSection = CheckRequiredReference<HUDMilestoneSelectionSection>(serializedHudManager,
                                                                                                                      "milestoneSelectionSection",
                                                                                                                      "HUDManager Milestone Selection Section",
                                                                                                                      warnings);
        HUDPowerUpContainerInteractionSection containerInteractionSection = CheckRequiredReference<HUDPowerUpContainerInteractionSection>(serializedHudManager,
                                                                                                                                          "powerUpContainerInteractionSection",
                                                                                                                                          "HUDManager Power-Up Container Interaction Section",
                                                                                                                                          warnings);
        HUDPowerUpSummarySection summarySection = CheckRequiredReference<HUDPowerUpSummarySection>(serializedHudManager,
                                                                                                   "powerUpSummarySection",
                                                                                                   "HUDManager Power-Up Summary Section",
                                                                                                   warnings);
        HUDWaveClearAnnouncementSection announcementSection = null;

        if (preset == null ||
            preset.WaveClearAnnouncementSettings == null ||
            preset.WaveClearAnnouncementSettings.IsEnabled)
            announcementSection = CheckRequiredReference<HUDWaveClearAnnouncementSection>(serializedHudManager,
                                                                                           "waveClearAnnouncementSection",
                                                                                           "HUDManager Room Clear Announcement Section",
                                                                                           warnings);
        HUDPlayerDamageVignetteSection damageVignetteSection = CheckRequiredReference<HUDPlayerDamageVignetteSection>(serializedHudManager,
                                                                                                                       "damageVignetteSection",
                                                                                                                       "HUDManager Damage Vignette Section",
                                                                                                                       warnings);

        ValidateReferenceRootProvider(referenceRootProvider, warnings);
        ValidateLevelExperienceSection(levelExperienceSection, healthBarsView, preset, warnings);
        ValidatePortraitSection(portraitSection, warnings);
        ValidateGrowthSequenceSection(growthSequenceSection, warnings);
        ValidatePowerUpOverlaySection(powerUpOverlaySection, warnings);
        ValidateRunTimerSection(runTimerSection, preset, warnings);
        ValidateSynchroMeterSection(comboCounterSection, preset, warnings);
        ValidateMilestoneSelectionSection(milestoneSelectionSection, preset, warnings);
        ValidateContainerInteractionSection(containerInteractionSection, warnings);
        ValidatePowerUpSummarySection(summarySection, preset, warnings);
        ValidateWaveClearAnnouncementSection(announcementSection, preset, warnings);
        ValidateDamageVignetteSection(damageVignetteSection, preset, warnings);
    }
    #endregion

    #region Section Validators
    /// <summary>
    /// Validates the optional shared reference root provider used by auto-discovery sections.
    /// </summary>
    /// <param name="provider">Reference root provider assigned on HUDManager.</param>
    /// <param name="warnings">Mutable warning list receiving scene-binding diagnostics.</param>
    private static void ValidateReferenceRootProvider(HUDReferenceRootProvider provider, List<string> warnings)
    {
        if (provider == null)
            return;

        SerializedObject serializedProvider = new SerializedObject(provider);
        serializedProvider.Update();
        CheckRequiredReference<Transform>(serializedProvider,
                                          "referenceSearchRoot",
                                          "HUD Reference Root Provider search root",
                                          warnings);
    }

    /// <summary>
    /// Validates level label and legacy experience references according to the active health-bars setup.
    /// </summary>
    /// <param name="section">Level and experience section assigned on HUDManager.</param>
    /// <param name="healthBarsView">Player health-bars view assigned on HUDManager.</param>
    /// <param name="preset">Selected HUD manager preset used to check piston-dependent references.</param>
    /// <param name="warnings">Mutable warning list receiving scene-binding diagnostics.</param>
    private static void ValidateLevelExperienceSection(HUDLevelExperienceSection section,
                                                       PlayerHealthBarsHudView healthBarsView,
                                                       GameHudManagerPreset preset,
                                                       List<string> warnings)
    {
        if (section == null)
            return;

        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        CheckRequiredReference<TMP_Text>(serializedSection,
                                         "playerLevelText",
                                         "Level Experience player level text",
                                         warnings);

        if (healthBarsView == null || !healthBarsView.HasExperienceBar)
            CheckRequiredReference<Image>(serializedSection,
                                          "playerExperienceFillImage",
                                          "Level Experience legacy fill image",
                                          warnings);

        if (preset != null &&
            preset.LevelExperienceSettings != null &&
            preset.LevelExperienceSettings.EnableLegacyExperiencePiston)
            CheckRequiredReference<RectTransform>(serializedSection,
                                                  "experiencePistonRoot",
                                                  "Level Experience piston root",
                                                  warnings);
    }

    /// <summary>
    /// Validates portrait UI references required when the Player Visual preset provides portrait frames.
    /// </summary>
    /// <param name="section">Portrait section assigned on HUDManager.</param>
    /// <param name="warnings">Mutable warning list receiving scene-binding diagnostics.</param>
    private static void ValidatePortraitSection(HUDPlayerPortraitSection section, List<string> warnings)
    {
        if (section == null)
            return;

        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        CheckRequiredReference<GameObject>(serializedSection,
                                           "rootObject",
                                           "Portrait section root object",
                                           warnings);
        CheckRequiredReference<Image>(serializedSection,
                                      "portraitImage",
                                      "Portrait section image",
                                      warnings);
    }

    /// <summary>
    /// Validates growth sequence root and slot bindings without duplicating Player Visual preset controls.
    /// </summary>
    /// <param name="section">Growth sequence section assigned on HUDManager.</param>
    /// <param name="warnings">Mutable warning list receiving scene-binding diagnostics.</param>
    private static void ValidateGrowthSequenceSection(HUDGrowthSequenceSection section, List<string> warnings)
    {
        if (section == null)
            return;

        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        CheckRequiredReference<GameObject>(serializedSection,
                                           "rootObject",
                                           "Growth Sequence root object",
                                           warnings);
    }

    /// <summary>
    /// Validates active power-up slot references used by icon, energy and charge presentation.
    /// </summary>
    /// <param name="section">Power-up overlay section assigned on HUDManager.</param>
    /// <param name="warnings">Mutable warning list receiving scene-binding diagnostics.</param>
    private static void ValidatePowerUpOverlaySection(HUDPowerUpOverlaySectionComponent section, List<string> warnings)
    {
        if (section == null)
            return;

        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        PlayerActivePowerUpSlotHudView primarySlot = CheckRequiredReference<PlayerActivePowerUpSlotHudView>(serializedSection,
                                                                                                            "primaryPowerUpSlotView",
                                                                                                            "Active Power-Up primary slot view",
                                                                                                            warnings);
        PlayerActivePowerUpSlotHudView secondarySlot = CheckRequiredReference<PlayerActivePowerUpSlotHudView>(serializedSection,
                                                                                                              "secondaryPowerUpSlotView",
                                                                                                              "Active Power-Up secondary slot view",
                                                                                                              warnings);

        if (primarySlot == null)
            CheckRequiredReference<Image>(serializedSection, "primaryPowerUpIconImage", "Active Power-Up primary icon image", warnings);

        if (secondarySlot == null)
            CheckRequiredReference<Image>(serializedSection, "secondaryPowerUpIconImage", "Active Power-Up secondary icon image", warnings);
    }

    /// <summary>
    /// Validates run timer text reference when the preset enables the timer section.
    /// </summary>
    /// <param name="section">Run timer section assigned on HUDManager.</param>
    /// <param name="preset">Selected HUD manager preset used to skip disabled timer checks.</param>
    /// <param name="warnings">Mutable warning list receiving scene-binding diagnostics.</param>
    private static void ValidateRunTimerSection(HUDRunTimerSection section, GameHudManagerPreset preset, List<string> warnings)
    {
        if (section == null)
            return;

        if (preset != null && preset.RunTimerSettings != null && !preset.RunTimerSettings.IsEnabled)
            return;

        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        CheckRequiredReference<TMP_Text>(serializedSection,
                                         "timerText",
                                         "Run Timer TMP text",
                                         warnings);
    }

    /// <summary>
    /// Validates authored Synchro Meter references required by enabled visual options.
    /// </summary>
    /// <param name="section">Synchro Meter section assigned on HUDManager.</param>
    /// <param name="preset">Selected HUD manager preset used to skip disabled visual options.</param>
    /// <param name="warnings">Mutable warning list receiving scene-binding diagnostics.</param>
    private static void ValidateSynchroMeterSection(HUDComboCounterSection section,
                                                    GameHudManagerPreset preset,
                                                    List<string> warnings)
    {
        if (section == null)
            return;

        if (preset != null && preset.SynchroMeterSettings != null && !preset.SynchroMeterSettings.IsEnabled)
            return;

        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        CheckRequiredReference<GameObject>(serializedSection, "rootObject", "Synchro Meter root object", warnings);
        CheckRequiredReference<RectTransform>(serializedSection, "waveViewport", "Synchro Meter wave viewport", warnings);
        CheckRequiredReference<Image>(serializedSection, "primaryWaveLeadingImage", "Synchro Meter primary leading wave", warnings);
        CheckRequiredReference<Image>(serializedSection, "primaryWaveTrailingImage", "Synchro Meter primary trailing wave", warnings);
        CheckRequiredReference<Image>(serializedSection, "secondaryWaveLeadingImage", "Synchro Meter secondary leading wave", warnings);
        CheckRequiredReference<Image>(serializedSection, "secondaryWaveTrailingImage", "Synchro Meter secondary trailing wave", warnings);

        GameHudSynchroMeterSettings settings = preset != null ? preset.SynchroMeterSettings : null;

        if (settings == null || settings.ShowBackground)
            CheckRequiredReference<Image>(serializedSection, "backgroundImage", "Synchro Meter background image", warnings);

        if (settings == null || settings.ShowCover)
            CheckRequiredReference<Image>(serializedSection, "coverImage", "Synchro Meter cover image", warnings);

        if (settings == null || settings.ShowRankText)
            CheckRequiredReference<TMP_Text>(serializedSection, "rankText", "Synchro Meter rank text", warnings);

        if (settings == null || settings.ShowValueText)
            CheckRequiredReference<TMP_Text>(serializedSection, "valueText", "Synchro Meter value text", warnings);
    }

    /// <summary>
    /// Validates milestone selection panel references needed for card rendering and skip input.
    /// </summary>
    /// <param name="section">Milestone selection section assigned on HUDManager.</param>
    /// <param name="preset">Selected HUD manager preset used to evaluate fallback behavior.</param>
    /// <param name="warnings">Mutable warning list receiving scene-binding diagnostics.</param>
    private static void ValidateMilestoneSelectionSection(HUDMilestoneSelectionSection section,
                                                          GameHudManagerPreset preset,
                                                          List<string> warnings)
    {
        if (section == null)
            return;

        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        CheckRequiredReference<GameObject>(serializedSection, "panelRoot", "Milestone Selection panel root", warnings);
        CheckRequiredReference<TMP_Text>(serializedSection, "headerText", "Milestone Selection header text", warnings);
        Button skipButton = CheckRequiredReference<Button>(serializedSection,
                                                           "skipButton",
                                                           "Milestone Selection skip button",
                                                           warnings);

        if (skipButton == null &&
            preset != null &&
            preset.MilestoneSelectionSettings != null &&
            preset.MilestoneSelectionSettings.AutoSelectFirstOfferWhenUiMissing)
            warnings.Add("Milestone Selection will auto-pick the first offer if card UI and Skip are unavailable. Verify this is intentional.");
    }

    /// <summary>
    /// Validates the dropped active-power-up overlay references used by Overlay Panel mode.
    /// </summary>
    /// <param name="section">Container interaction section assigned on HUDManager.</param>
    /// <param name="warnings">Mutable warning list receiving scene-binding diagnostics.</param>
    private static void ValidateContainerInteractionSection(HUDPowerUpContainerInteractionSection section, List<string> warnings)
    {
        if (section == null)
            return;

        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        GameObject overlayRoot = CheckRequiredReference<GameObject>(serializedSection,
                                                                    "overlayPanelRoot",
                                                                    "Power-Up Container overlay root",
                                                                    warnings);

        if (overlayRoot == null)
            return;

        CheckRequiredReference<Button>(serializedSection, "replacePrimaryButton", "Power-Up Container primary replace button", warnings);
        CheckRequiredReference<Button>(serializedSection, "replaceSecondaryButton", "Power-Up Container secondary replace button", warnings);
    }

    /// <summary>
    /// Validates fixed summary pools and core authored hierarchy references when the inline settings enable the section.
    /// </summary>
    /// <param name="section">Power-up summary section assigned on HUDManager.</param>
    /// <param name="preset">Selected HUD preset used to skip an explicitly disabled summary.</param>
    /// <param name="warnings">Mutable warning list receiving scene-binding diagnostics.</param>
    private static void ValidatePowerUpSummarySection(HUDPowerUpSummarySection section,
                                                      GameHudManagerPreset preset,
                                                      List<string> warnings)
    {
        if (section == null)
            return;

        if (preset != null &&
            preset.PowerUpSummarySettings != null &&
            !preset.PowerUpSummarySettings.IsEnabled)
            return;

        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        CheckRequiredReference<RectTransform>(serializedSection, "panelRoot", "Power-Up Summary panel root", warnings);
        CheckRequiredReference<RectTransform>(serializedSection, "contentRoot", "Power-Up Summary content root", warnings);
        CheckRequiredReference<RectTransform>(serializedSection, "powerUpAreaRoot", "Power-Up Summary upper area", warnings);
        CheckRequiredReference<RectTransform>(serializedSection, "statisticsAreaRoot", "Power-Up Summary statistic area", warnings);
        CheckRequiredReference<Button>(serializedSection, "toggleButton", "Power-Up Summary toggle button", warnings);
        ValidateArrayCapacity(serializedSection,
                              "activeIconViews",
                              GameHudPowerUpSummarySettings.AuthoredActiveSlotCapacity,
                              "active icon",
                              warnings);
        ValidateArrayCapacity(serializedSection,
                              "passiveIconViews",
                              GameHudPowerUpSummarySettings.AuthoredPassiveSlotCapacity,
                              "passive icon",
                              warnings);
        ValidateArrayCapacity(serializedSection,
                              "statisticRows",
                              GameHudPowerUpSummarySettings.AuthoredStatisticRowCapacity,
                              "statistic row",
                              warnings);
    }

    /// <summary>
    /// Validates the preauthored full-screen root, moving text, and canvas group when announcements are enabled.
    /// </summary>
    /// <param name="section">Room-clear announcement section assigned on HUDManager.</param>
    /// <param name="preset">Selected HUD preset used to skip disabled announcement checks.</param>
    /// <param name="warnings">Mutable warning list receiving scene-binding diagnostics.</param>
    private static void ValidateWaveClearAnnouncementSection(HUDWaveClearAnnouncementSection section,
                                                             GameHudManagerPreset preset,
                                                             List<string> warnings)
    {
        if (section == null)
            return;

        if (preset != null &&
            preset.WaveClearAnnouncementSettings != null &&
            !preset.WaveClearAnnouncementSettings.IsEnabled)
            return;

        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        CheckRequiredReference<RectTransform>(serializedSection,
                                              "presentationRoot",
                                              "Room Clear Announcement presentation root",
                                              warnings);
        CheckRequiredReference<RectTransform>(serializedSection,
                                              "textRoot",
                                              "Room Clear Announcement moving text root",
                                              warnings);
        CheckRequiredReference<TMP_Text>(serializedSection,
                                         "announcementText",
                                         "Room Clear Announcement text",
                                         warnings);
        CheckRequiredReference<CanvasGroup>(serializedSection,
                                            "canvasGroup",
                                            "Room Clear Announcement canvas group",
                                            warnings);
    }

    /// <summary>
    /// Validates damage vignette image references when the preset enables the section.
    /// </summary>
    /// <param name="section">Damage vignette section assigned on HUDManager.</param>
    /// <param name="preset">Selected HUD manager preset used to skip disabled vignette checks.</param>
    /// <param name="warnings">Mutable warning list receiving scene-binding diagnostics.</param>
    private static void ValidateDamageVignetteSection(HUDPlayerDamageVignetteSection section,
                                                      GameHudManagerPreset preset,
                                                      List<string> warnings)
    {
        if (section == null)
            return;

        if (preset != null && preset.DamageVignetteSettings != null && !preset.DamageVignetteSettings.IsEnabled)
            return;

        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        CheckRequiredReference<Image>(serializedSection, "shieldVignetteImage", "Damage Vignette shield image", warnings);
        CheckRequiredReference<Image>(serializedSection, "healthVignetteImage", "Damage Vignette health image", warnings);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Finds SCN_MainScene_UI among currently loaded scenes without changing editor scene state.
    /// </summary>
    /// <returns>Loaded gameplay UI scene, or an invalid scene when not loaded.</returns>
    private static Scene FindLoadedGameplayUiScene()
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);

            if (!scene.isLoaded)
                continue;

            if (string.Equals(scene.path, GameSceneManagementProjectSetupUtility.GameplayUiScenePath, System.StringComparison.Ordinal) ||
                string.Equals(scene.name, GameSceneManagementProjectSetupUtility.GameplayUiSceneId, System.StringComparison.Ordinal))
                return scene;
        }

        return default;
    }

    /// <summary>
    /// Validates one fixed preauthored component-reference pool and reports missing slots.
    /// </summary>
    /// <param name="serializedObject">Serialized section owning the pool.</param>
    /// <param name="propertyName">Serialized array field name.</param>
    /// <param name="expectedCapacity">Required fixed pool capacity.</param>
    /// <param name="label">Entry label included in warnings.</param>
    /// <param name="warnings">Mutable warning list receiving scene-binding diagnostics.</param>
    private static void ValidateArrayCapacity(SerializedObject serializedObject,
                                              string propertyName,
                                              int expectedCapacity,
                                              string label,
                                              List<string> warnings)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null || !property.isArray)
        {
            warnings.Add("Power-Up Summary " + label + " pool was not found.");
            return;
        }

        if (property.arraySize != expectedCapacity)
            warnings.Add(string.Format("Power-Up Summary {0} pool contains {1} entries instead of the required fixed capacity of {2}.",
                                       label,
                                       property.arraySize,
                                       expectedCapacity));

        for (int entryIndex = 0; entryIndex < property.arraySize; entryIndex++)
        {
            if (property.GetArrayElementAtIndex(entryIndex).objectReferenceValue != null)
                continue;

            warnings.Add(string.Format("Power-Up Summary {0} pool entry {1} is missing.", label, entryIndex + 1));
        }
    }

    /// <summary>
    /// Reads one required object reference and reports a warning when it is missing.
    /// </summary>
    /// <param name="serializedObject">Serialized object that owns the reference field.</param>
    /// <param name="propertyName">Serialized property name to inspect.</param>
    /// <param name="label">Clear binding label included in warnings.</param>
    /// <param name="warnings">Mutable warning list receiving scene-binding diagnostics.</param>
    /// <returns>Resolved object reference, or null when missing or incompatible.</returns>
    private static TReference CheckRequiredReference<TReference>(SerializedObject serializedObject,
                                                                 string propertyName,
                                                                 string label,
                                                                 List<string> warnings) where TReference : UnityEngine.Object
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            warnings.Add(label + " serialized field was not found.");
            return null;
        }

        TReference reference = property.objectReferenceValue as TReference;

        if (reference == null)
            warnings.Add(label + " is missing.");

        return reference;
    }

    #endregion

    #endregion
}
