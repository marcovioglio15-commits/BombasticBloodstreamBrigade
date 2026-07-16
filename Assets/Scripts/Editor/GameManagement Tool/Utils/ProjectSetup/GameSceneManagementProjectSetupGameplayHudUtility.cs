using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static GameSceneManagementProjectSetupGameplayUiUtility;
using static GameSceneManagementProjectSetupSceneUtility;
using static GameSceneManagementProjectSetupSerializedUtility;

/// <summary>
/// Builds and wires authored HUD section components inside the additive gameplay UI scene.
/// </summary>
internal static class GameSceneManagementProjectSetupGameplayHudUtility
{
    #region Methods

    #region Internal Methods
    /// <summary>
    /// Ensures the gameplay UI scene owns the authored HUD manager root and removes duplicate gameplay copies.
    /// </summary>
    /// <param name="gameplayScene">Scene currently holding gameplay simulation content.</param>
    /// <param name="gameplayUiScene">Scene that should own gameplay UI roots.</param>
    internal static void EnsureGameplayUiHudManager(Scene gameplayScene, Scene gameplayUiScene)
    {
        HUDManager uiSceneHudManager = FindFirstComponentInScene<HUDManager>(gameplayUiScene);
        HUDManager gameplaySceneHudManager = FindFirstComponentInScene<HUDManager>(gameplayScene);

        if (uiSceneHudManager == null && gameplaySceneHudManager != null)
        {
            SceneManager.MoveGameObjectToScene(gameplaySceneHudManager.transform.root.gameObject, gameplayUiScene);
            return;
        }

        if (uiSceneHudManager != null && gameplaySceneHudManager != null)
            UnityEngine.Object.DestroyImmediate(gameplaySceneHudManager.transform.root.gameObject);
    }

    /// <summary>
    /// Ensures HUD section components exist on their authored UI roots and are referenced by HUDManager.
    /// </summary>
    /// <param name="gameplayUiScene">Scene that owns gameplay UI roots.</param>
    internal static void EnsureGameplayUiHudSections(Scene gameplayUiScene)
    {
        HUDManager hudManager = FindFirstComponentInScene<HUDManager>(gameplayUiScene);

        if (hudManager == null)
            return;

        // Resolve every authored HUD host before updating the manager atomically.
        Canvas canvas = FindGameplayCanvas(gameplayUiScene);
        HUDReferenceRootProvider referenceRootProvider = EnsureReferenceRootProvider(hudManager, canvas);
        PlayerHealthBarsHudView healthBarsView = FindFirstComponentInScene<PlayerHealthBarsHudView>(gameplayUiScene);
        HUDLevelExperienceSection levelExperienceSection = EnsureLevelExperienceSection(hudManager, healthBarsView);
        HUDPlayerPortraitSection portraitSection = EnsurePortraitSection(gameplayUiScene, hudManager);
        HUDGrowthSequenceSection growthSequenceSection = EnsureGrowthSequenceSection(gameplayUiScene, hudManager);
        HUDPowerUpOverlaySectionComponent powerUpOverlaySection = EnsurePowerUpOverlaySection(gameplayUiScene, hudManager);
        HUDRunTimerSection runTimerSection = EnsureRunTimerSection(gameplayUiScene, hudManager);
        HUDComboCounterSection comboCounterSection = EnsureComboCounterSection(gameplayUiScene, hudManager);
        HUDMilestoneSelectionSection milestoneSection = EnsureMilestoneSelectionSection(gameplayUiScene, hudManager);
        HUDPowerUpContainerInteractionSection containerSection = EnsurePowerUpContainerInteractionSection(gameplayUiScene, hudManager);
        HUDPlayerDamageVignetteSection damageSection = EnsureDamageVignetteSection(gameplayUiScene, hudManager);

        // Persist all section references through the established serialized setup path.
        SerializedObject serializedHudManager = new SerializedObject(hudManager);
        serializedHudManager.Update();
        SetObjectReference(serializedHudManager, "referenceRootProvider", referenceRootProvider);
        SetObjectReference(serializedHudManager, "playerHealthBarsView", healthBarsView);
        SetObjectReference(serializedHudManager, "levelExperienceSection", levelExperienceSection);
        SetObjectReference(serializedHudManager, "portraitSection", portraitSection);
        SetObjectReference(serializedHudManager, "growthSequenceSection", growthSequenceSection);
        SetObjectReference(serializedHudManager, "powerUpOverlaySection", powerUpOverlaySection);
        SetObjectReference(serializedHudManager, "runTimerSection", runTimerSection);
        SetObjectReference(serializedHudManager, "comboCounterSection", comboCounterSection);
        SetObjectReference(serializedHudManager, "milestoneSelectionSection", milestoneSection);
        SetObjectReference(serializedHudManager, "powerUpContainerInteractionSection", containerSection);
        SetObjectReference(serializedHudManager, "damageVignetteSection", damageSection);
        serializedHudManager.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hudManager);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Ensures the HUD reference root provider exists on the gameplay canvas when possible.
    /// </summary>
    /// <param name="hudManager">HUD manager being wired.</param>
    /// <param name="canvas">Gameplay canvas used as the preferred provider host.</param>
    /// <returns>Configured reference root provider.</returns>
    private static HUDReferenceRootProvider EnsureReferenceRootProvider(HUDManager hudManager, Canvas canvas)
    {
        GameObject providerHost = canvas != null ? canvas.gameObject : hudManager.gameObject;
        HUDReferenceRootProvider provider = EnsureComponent<HUDReferenceRootProvider>(providerHost);
        SerializedObject serializedProvider = new SerializedObject(provider);
        serializedProvider.Update();
        SetObjectReference(serializedProvider, "referenceSearchRoot", canvas != null ? canvas.transform : hudManager.transform);
        SetString(serializedProvider, "referenceSearchRootName", canvas != null ? canvas.gameObject.name : "CanvasStyled");
        serializedProvider.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(provider);
        return provider;
    }

    /// <summary>
    /// Ensures the level and legacy experience component exists beside the health-bars view.
    /// </summary>
    /// <param name="hudManager">HUD manager used as fallback host.</param>
    /// <param name="healthBarsView">Preauthored health-bars view when present.</param>
    /// <returns>Configured level and experience section.</returns>
    private static HUDLevelExperienceSection EnsureLevelExperienceSection(HUDManager hudManager, PlayerHealthBarsHudView healthBarsView)
    {
        GameObject host = healthBarsView != null ? healthBarsView.gameObject : hudManager.gameObject;
        HUDLevelExperienceSection section = EnsureComponent<HUDLevelExperienceSection>(host);
        TMP_Text levelText = FindFirstChildText(host.transform, "Level");
        Image experienceFill = FindFirstChildImage(host.transform, "Experience");
        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        SetObjectReference(serializedSection, "playerLevelText", levelText);
        SetObjectReference(serializedSection, "playerExperienceFillImage", experienceFill);
        serializedSection.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(section);
        return section;
    }

    /// <summary>
    /// Ensures the portrait section component exists on the portrait root.
    /// </summary>
    /// <param name="scene">Scene searched for portrait UI.</param>
    /// <param name="hudManager">HUD manager used as fallback host.</param>
    /// <returns>Configured portrait section.</returns>
    private static HUDPlayerPortraitSection EnsurePortraitSection(Scene scene, HUDManager hudManager)
    {
        Image portraitImage = FindImageByName(scene, "Portrait");
        GameObject host = portraitImage != null ? ResolveSectionRoot(portraitImage.transform, "PortraitContainer") : hudManager.gameObject;
        HUDPlayerPortraitSection section = EnsureComponent<HUDPlayerPortraitSection>(host);
        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        SetObjectReference(serializedSection, "rootObject", host);
        SetObjectReference(serializedSection, "portraitImage", portraitImage);
        SetBool(serializedSection, "autoDiscoverReferences", true);
        serializedSection.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(section);
        return section;
    }

    /// <summary>
    /// Ensures the growth sequence component exists on its authored root.
    /// </summary>
    /// <param name="scene">Scene searched for growth UI.</param>
    /// <param name="hudManager">HUD manager used as fallback host.</param>
    /// <returns>Configured growth sequence section.</returns>
    private static HUDGrowthSequenceSection EnsureGrowthSequenceSection(Scene scene, HUDManager hudManager)
    {
        Transform root = FindTransformByNameContains(scene, "GrowthSequence");
        GameObject host = root != null ? root.gameObject : hudManager.gameObject;
        HUDGrowthSequenceSection section = EnsureComponent<HUDGrowthSequenceSection>(host);
        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        SetObjectReference(serializedSection, "rootObject", host);
        SetBool(serializedSection, "autoDiscoverReferences", true);
        serializedSection.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(section);
        return section;
    }

    /// <summary>
    /// Ensures the active power-up overlay component exists on the active-slot container.
    /// </summary>
    /// <param name="scene">Scene searched for active-slot views.</param>
    /// <param name="hudManager">HUD manager used as fallback host.</param>
    /// <returns>Configured active power-up overlay section.</returns>
    private static HUDPowerUpOverlaySectionComponent EnsurePowerUpOverlaySection(Scene scene, HUDManager hudManager)
    {
        PlayerActivePowerUpSlotHudView primarySlot = FindSlotView(scene, "Primary");
        PlayerActivePowerUpSlotHudView secondarySlot = FindSlotView(scene, "Secondary");
        GameObject host = ResolveCommonSectionHost(primarySlot != null ? primarySlot.transform : null,
                                                   secondarySlot != null ? secondarySlot.transform : null,
                                                   hudManager.gameObject);
        HUDPowerUpOverlaySectionComponent section = EnsureComponent<HUDPowerUpOverlaySectionComponent>(host);
        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        SetObjectReference(serializedSection, "primaryPowerUpSlotView", primarySlot);
        SetObjectReference(serializedSection, "secondaryPowerUpSlotView", secondarySlot);
        SetObjectReference(serializedSection, "primaryPowerUpIconImage", primarySlot != null ? primarySlot.IconImage : null);
        SetObjectReference(serializedSection, "secondaryPowerUpIconImage", secondarySlot != null ? secondarySlot.IconImage : null);
        SetObjectReference(serializedSection, "primaryPowerUpSlotRootObject", primarySlot != null ? primarySlot.gameObject : null);
        SetObjectReference(serializedSection, "secondaryPowerUpSlotRootObject", secondarySlot != null ? secondarySlot.gameObject : null);
        serializedSection.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(section);
        return section;
    }

    /// <summary>
    /// Ensures the run timer component exists on the authored timer text object.
    /// </summary>
    /// <param name="scene">Scene searched for timer text.</param>
    /// <param name="hudManager">HUD manager used as fallback host.</param>
    /// <returns>Configured run timer section.</returns>
    private static HUDRunTimerSection EnsureRunTimerSection(Scene scene, HUDManager hudManager)
    {
        TMP_Text timerText = FindTextByName(scene, "Timer");
        GameObject host = timerText != null ? timerText.gameObject : hudManager.gameObject;
        HUDRunTimerSection section = EnsureComponent<HUDRunTimerSection>(host);
        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        SetObjectReference(serializedSection, "timerText", timerText);
        serializedSection.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(section);
        return section;
    }

    /// <summary>
    /// Ensures the combo counter component exists on the combo panel root.
    /// </summary>
    /// <param name="scene">Scene searched for combo UI.</param>
    /// <param name="hudManager">HUD manager used as fallback host.</param>
    /// <returns>Configured combo counter section.</returns>
    private static HUDComboCounterSection EnsureComboCounterSection(Scene scene, HUDManager hudManager)
    {
        Transform root = FindTransformByNameContains(scene, "ComboCounter");
        GameObject host = root != null ? root.gameObject : hudManager.gameObject;
        HUDComboCounterSection section = EnsureComponent<HUDComboCounterSection>(host);
        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        SetObjectReference(serializedSection, "rootObject", host);
        SetObjectReference(serializedSection, "rankText", FindFirstChildText(host.transform, "RankText"));
        SetObjectReference(serializedSection, "comboValueText", FindFirstChildText(host.transform, "ComboValue"));
        SetObjectReference(serializedSection, "rankBadgeImage", FindFirstChildImage(host.transform, "Badge"));
        SetObjectReference(serializedSection, "progressFillImage", FindFirstChildImage(host.transform, "Fill"));
        SetObjectReference(serializedSection, "progressBackgroundImage", FindFirstChildImage(host.transform, "Background"));
        serializedSection.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(section);
        return section;
    }

    /// <summary>
    /// Ensures the milestone selection component exists on the power-up panel root.
    /// </summary>
    /// <param name="scene">Scene searched for milestone UI.</param>
    /// <param name="hudManager">HUD manager used as fallback host.</param>
    /// <returns>Configured milestone selection section.</returns>
    private static HUDMilestoneSelectionSection EnsureMilestoneSelectionSection(Scene scene, HUDManager hudManager)
    {
        Transform root = FindTransformByNameContains(scene, "PowerUpsPanel");
        GameObject host = root != null ? root.gameObject : hudManager.gameObject;
        HUDMilestoneSelectionSection section = EnsureComponent<HUDMilestoneSelectionSection>(host);
        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        SetObjectReference(serializedSection, "panelRoot", host);
        SetObjectReference(serializedSection, "headerText", FindFirstChildText(host.transform, "Header"));
        SetObjectReference(serializedSection, "skipButton", FindFirstChildComponent<Button>(host.transform, "Skip"));
        SetObjectReference(serializedSection, "skipHoldFillImage", FindFirstChildImage(host.transform, "SkipHoldFill"));
        serializedSection.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(section);
        return section;
    }

    /// <summary>
    /// Ensures the dropped power-up container overlay component exists on its overlay root.
    /// </summary>
    /// <param name="scene">Scene searched for overlay UI.</param>
    /// <param name="hudManager">HUD manager used as fallback host.</param>
    /// <returns>Configured dropped-container interaction section.</returns>
    private static HUDPowerUpContainerInteractionSection EnsurePowerUpContainerInteractionSection(Scene scene, HUDManager hudManager)
    {
        Transform root = FindTransformByNameContains(scene, "PowerUpContainerOverlay");
        GameObject host = root != null ? root.gameObject : hudManager.gameObject;
        HUDPowerUpContainerInteractionSection section = EnsureComponent<HUDPowerUpContainerInteractionSection>(host);
        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        SetObjectReference(serializedSection, "overlayPanelRoot", host);
        SetObjectReference(serializedSection, "overlayTitleText", FindFirstChildText(host.transform, "Title"));
        SetObjectReference(serializedSection, "overlayDescriptionText", FindFirstChildText(host.transform, "Description"));
        SetObjectReference(serializedSection, "overlayIconImage", FindFirstChildImage(host.transform, "Icon"));
        SetObjectReference(serializedSection, "replacePrimaryButton", FindFirstChildComponent<Button>(host.transform, "Primary"));
        SetObjectReference(serializedSection, "replaceSecondaryButton", FindFirstChildComponent<Button>(host.transform, "Secondary"));
        serializedSection.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(section);
        return section;
    }

    /// <summary>
    /// Ensures the damage vignette component exists on the vignette root.
    /// </summary>
    /// <param name="scene">Scene searched for vignette UI.</param>
    /// <param name="hudManager">HUD manager used as fallback host.</param>
    /// <returns>Configured damage vignette section.</returns>
    private static HUDPlayerDamageVignetteSection EnsureDamageVignetteSection(Scene scene, HUDManager hudManager)
    {
        Image shieldImage = FindImageByName(scene, "Shield");
        Image healthImage = FindImageByName(scene, "Health");
        GameObject host = ResolveCommonSectionHost(shieldImage != null ? shieldImage.transform : null,
                                                   healthImage != null ? healthImage.transform : null,
                                                   hudManager.gameObject);
        HUDPlayerDamageVignetteSection section = EnsureComponent<HUDPlayerDamageVignetteSection>(host);
        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        SetObjectReference(serializedSection, "shieldVignetteImage", shieldImage);
        SetObjectReference(serializedSection, "shieldVignetteRootObject", shieldImage != null ? shieldImage.gameObject : null);
        SetObjectReference(serializedSection, "healthVignetteImage", healthImage);
        SetObjectReference(serializedSection, "healthVignetteRootObject", healthImage != null ? healthImage.gameObject : null);
        serializedSection.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(section);
        return section;
    }
    #endregion

    #endregion
}
