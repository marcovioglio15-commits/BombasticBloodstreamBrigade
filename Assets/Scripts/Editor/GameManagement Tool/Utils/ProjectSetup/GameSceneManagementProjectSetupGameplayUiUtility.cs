using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static GameSceneManagementProjectSetupSceneUtility;
using static GameSceneManagementProjectSetupSerializedUtility;

/// <summary>
/// Builds and maintains the additive gameplay UI scene used by the default Scene Manager setup.
/// </summary>
internal static class GameSceneManagementProjectSetupGameplayUiUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the separate gameplay UI scene and moves authored HUD/menu roots out of the gameplay scene.
    /// </summary>
    public static void EnsureGameplayUiScene()
    {
        Scene gameplayScene = EditorSceneManager.OpenScene(GameSceneManagementProjectSetupUtility.GameplayScenePath, OpenSceneMode.Single);
        Scene gameplayUiScene = OpenOrCreateGameplayUiScene();
        MoveGameplayUiRoots(gameplayScene, gameplayUiScene);
        Canvas uiCanvas = EnsureGameplayUiCanvas(gameplayScene, gameplayUiScene);
        EnsureGameplayUiHudManager(gameplayScene, gameplayUiScene);
        EnsureGameplayUiHudSections(gameplayUiScene);
        EnsureGameplayUiEventSystem(gameplayScene, gameplayUiScene);
        EnsureGameplayUiCameraStackBridge(gameplayUiScene);
        EnsureGameplayUiCameraReferences(gameplayUiScene);
        ConfigureGameplayUiCanvas(uiCanvas);
        CleanCameraStacks(gameplayScene);
        CleanCameraStacks(gameplayUiScene);
        EditorSceneManager.MarkSceneDirty(gameplayScene);
        EditorSceneManager.MarkSceneDirty(gameplayUiScene);
        EditorSceneManager.SaveScene(gameplayScene, GameSceneManagementProjectSetupUtility.GameplayScenePath);
        EditorSceneManager.SaveScene(gameplayUiScene, GameSceneManagementProjectSetupUtility.GameplayUiScenePath);
    }
    #endregion

    #region Scene Opening
    /// <summary>
    /// Opens the existing gameplay UI scene or creates an empty additive scene at the expected path.
    /// </summary>
    /// <returns>Open gameplay UI scene.</returns>
    private static Scene OpenOrCreateGameplayUiScene()
    {
        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameSceneManagementProjectSetupUtility.GameplayUiScenePath);

        if (sceneAsset != null)
            return EditorSceneManager.OpenScene(GameSceneManagementProjectSetupUtility.GameplayUiScenePath, OpenSceneMode.Additive);

        return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
    }
    #endregion

    #region Root Migration
    /// <summary>
    /// Moves every top-level UI root from gameplay to the additive UI scene before references are serialized.
    /// </summary>
    /// <param name="gameplayScene">Scene currently holding simulation and possibly authored UI roots.</param>
    /// <param name="gameplayUiScene">Scene that should own gameplay UI roots.</param>
    private static void MoveGameplayUiRoots(Scene gameplayScene, Scene gameplayUiScene)
    {
        GameObject[] rootObjects = gameplayScene.GetRootGameObjects();

        for (int index = 0; index < rootObjects.Length; index++)
        {
            GameObject rootObject = rootObjects[index];

            if (!ShouldMoveGameplayUiRoot(rootObject))
                continue;

            SceneManager.MoveGameObjectToScene(rootObject, gameplayUiScene);
        }
    }

    /// <summary>
    /// Resolves whether one gameplay root belongs to authored UI and should be separated.
    /// </summary>
    /// <param name="rootObject">Gameplay scene root inspected for UI components.</param>
    /// <returns>True when the root should move into the gameplay UI scene.</returns>
    private static bool ShouldMoveGameplayUiRoot(GameObject rootObject)
    {
        if (rootObject == null)
            return false;

        if (rootObject.GetComponentInChildren<Canvas>(true) != null)
            return true;

        if (rootObject.GetComponentInChildren<HUDManager>(true) != null)
            return true;

        if (rootObject.GetComponentInChildren<EventSystem>(true) != null)
            return true;

        if (rootObject.GetComponentInChildren<GameplayMenuController>(true) != null)
            return true;

        if (rootObject.GetComponentInChildren<MenuSelectionController>(true) != null)
            return true;

        if (IsNamedUiCameraRoot(rootObject))
            return true;

        return rootObject.GetComponent<RectTransform>() != null &&
               rootObject.GetComponentInChildren<Selectable>(true) != null;
    }

    /// <summary>
    /// Resolves whether one root is the authored camera used by screen-space camera UI canvases.
    /// </summary>
    /// <param name="rootObject">Gameplay scene root inspected for camera ownership.</param>
    /// <returns>True when this root is the authored UI camera.</returns>
    private static bool IsNamedUiCameraRoot(GameObject rootObject)
    {
        if (!string.Equals(rootObject.name, "UI Camera", StringComparison.Ordinal))
            return false;

        return rootObject.GetComponentInChildren<Camera>(true) != null;
    }
    #endregion

    #region Canvas
    /// <summary>
    /// Ensures the gameplay UI scene owns the gameplay canvas and removes duplicate gameplay copies.
    /// </summary>
    /// <param name="gameplayScene">Scene currently holding gameplay simulation content.</param>
    /// <param name="gameplayUiScene">Scene that should own gameplay UI roots.</param>
    /// <returns>Gameplay UI canvas or null when no authored canvas exists yet.</returns>
    private static Canvas EnsureGameplayUiCanvas(Scene gameplayScene, Scene gameplayUiScene)
    {
        Canvas uiSceneCanvas = FindGameplayCanvas(gameplayUiScene);
        Canvas gameplaySceneCanvas = FindGameplayCanvas(gameplayScene);

        if (uiSceneCanvas == null && gameplaySceneCanvas != null)
        {
            SceneManager.MoveGameObjectToScene(gameplaySceneCanvas.transform.root.gameObject, gameplayUiScene);
            return gameplaySceneCanvas;
        }

        if (uiSceneCanvas != null && gameplaySceneCanvas != null)
            UnityEngine.Object.DestroyImmediate(gameplaySceneCanvas.transform.root.gameObject);

        return uiSceneCanvas;
    }

    /// <summary>
    /// Applies stable screen-space settings to the separated gameplay UI canvas.
    /// </summary>
    /// <param name="canvas">Gameplay UI canvas to configure.</param>
    private static void ConfigureGameplayUiCanvas(Canvas canvas)
    {
        if (canvas == null)
            return;

        CanvasScaler canvasScaler = EnsureComponent<CanvasScaler>(canvas.gameObject);
        GraphicRaycaster graphicRaycaster = EnsureComponent<GraphicRaycaster>(canvas.gameObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = 0.5f;
        graphicRaycaster.enabled = true;
        EditorUtility.SetDirty(canvas);
    }

    /// <summary>
    /// Finds the authored gameplay canvas by HUDManager first, CanvasMain second and any root Canvas last.
    /// </summary>
    /// <param name="scene">Scene searched by root hierarchy.</param>
    /// <returns>Gameplay canvas when available.</returns>
    private static Canvas FindGameplayCanvas(Scene scene)
    {
        HUDManager hudManager = FindFirstComponentInScene<HUDManager>(scene);

        if (hudManager != null)
        {
            Canvas parentCanvas = hudManager.GetComponentInParent<Canvas>(true);

            if (parentCanvas != null)
                return parentCanvas;
        }

        Canvas fallbackCanvas = null;
        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int index = 0; index < rootObjects.Length; index++)
        {
            Canvas rootCanvas = rootObjects[index].GetComponent<Canvas>();

            if (rootCanvas == null)
                continue;

            if (string.Equals(rootObjects[index].name, "CanvasMain", StringComparison.Ordinal))
                return rootCanvas;

            if (fallbackCanvas == null)
                fallbackCanvas = rootCanvas;
        }

        return fallbackCanvas;
    }
    #endregion

    #region HUD
    /// <summary>
    /// Ensures the gameplay UI scene owns the authored HUD manager root and removes duplicate gameplay copies.
    /// </summary>
    /// <param name="gameplayScene">Scene currently holding gameplay simulation content.</param>
    /// <param name="gameplayUiScene">Scene that should own gameplay UI roots.</param>
    private static void EnsureGameplayUiHudManager(Scene gameplayScene, Scene gameplayUiScene)
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
    private static void EnsureGameplayUiHudSections(Scene gameplayUiScene)
    {
        HUDManager hudManager = FindFirstComponentInScene<HUDManager>(gameplayUiScene);

        if (hudManager == null)
            return;

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
        SetObjectReference(serializedSection, "rankText", FindFirstChildText(host.transform, "ComboLabel"));
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

    /// <summary>
    /// Finds one active power-up slot view whose object name contains the provided token.
    /// </summary>
    /// <param name="scene">Scene searched for slot views.</param>
    /// <param name="nameToken">Name token used to distinguish primary and secondary slots.</param>
    /// <returns>Matching slot view or null when unavailable.</returns>
    private static PlayerActivePowerUpSlotHudView FindSlotView(Scene scene, string nameToken)
    {
        System.Collections.Generic.List<PlayerActivePowerUpSlotHudView> slotViews = FindComponentsInScene<PlayerActivePowerUpSlotHudView>(scene);

        for (int index = 0; index < slotViews.Count; index++)
        {
            PlayerActivePowerUpSlotHudView slotView = slotViews[index];

            if (slotView == null)
                continue;

            if (ContainsOrdinalIgnoreCase(slotView.gameObject.name, nameToken))
                return slotView;
        }

        if (slotViews.Count > 0 && string.Equals(nameToken, "Primary", StringComparison.OrdinalIgnoreCase))
            return slotViews[0];

        if (slotViews.Count > 1 && string.Equals(nameToken, "Secondary", StringComparison.OrdinalIgnoreCase))
            return slotViews[1];

        return null;
    }

    /// <summary>
    /// Finds the first TMP text whose GameObject name contains a token.
    /// </summary>
    /// <param name="scene">Scene searched for text components.</param>
    /// <param name="nameToken">Name token used to match the text.</param>
    /// <returns>Matching TMP text or null when unavailable.</returns>
    private static TMP_Text FindTextByName(Scene scene, string nameToken)
    {
        System.Collections.Generic.List<TMP_Text> texts = FindComponentsInScene<TMP_Text>(scene);

        for (int index = 0; index < texts.Count; index++)
        {
            TMP_Text text = texts[index];

            if (text != null && ContainsOrdinalIgnoreCase(text.gameObject.name, nameToken))
                return text;
        }

        return null;
    }

    /// <summary>
    /// Finds the first Image whose GameObject name contains a token.
    /// </summary>
    /// <param name="scene">Scene searched for image components.</param>
    /// <param name="nameToken">Name token used to match the image.</param>
    /// <returns>Matching image or null when unavailable.</returns>
    private static Image FindImageByName(Scene scene, string nameToken)
    {
        System.Collections.Generic.List<Image> images = FindComponentsInScene<Image>(scene);

        for (int index = 0; index < images.Count; index++)
        {
            Image image = images[index];

            if (image != null && ContainsOrdinalIgnoreCase(image.gameObject.name, nameToken))
                return image;
        }

        return null;
    }

    /// <summary>
    /// Finds the first transform whose GameObject name contains a token.
    /// </summary>
    /// <param name="scene">Scene searched for transforms.</param>
    /// <param name="nameToken">Name token used to match the transform.</param>
    /// <returns>Matching transform or null when unavailable.</returns>
    private static Transform FindTransformByNameContains(Scene scene, string nameToken)
    {
        System.Collections.Generic.List<Transform> transforms = FindComponentsInScene<Transform>(scene);

        for (int index = 0; index < transforms.Count; index++)
        {
            Transform transform = transforms[index];

            if (transform != null && ContainsOrdinalIgnoreCase(transform.gameObject.name, nameToken))
                return transform;
        }

        return null;
    }

    /// <summary>
    /// Finds the first child TMP text whose object name contains a token.
    /// </summary>
    /// <param name="root">Hierarchy root searched for text components.</param>
    /// <param name="nameToken">Name token used to match the text.</param>
    /// <returns>Matching TMP text or null when unavailable.</returns>
    private static TMP_Text FindFirstChildText(Transform root, string nameToken)
    {
        return FindFirstChildComponent<TMP_Text>(root, nameToken);
    }

    /// <summary>
    /// Finds the first child Image whose object name contains a token.
    /// </summary>
    /// <param name="root">Hierarchy root searched for image components.</param>
    /// <param name="nameToken">Name token used to match the image.</param>
    /// <returns>Matching image or null when unavailable.</returns>
    private static Image FindFirstChildImage(Transform root, string nameToken)
    {
        return FindFirstChildComponent<Image>(root, nameToken);
    }

    /// <summary>
    /// Finds the first child component whose object name contains a token.
    /// </summary>
    /// <param name="root">Hierarchy root searched for components.</param>
    /// <param name="nameToken">Name token used to match the component object.</param>
    /// <returns>Matching component or null when unavailable.</returns>
    private static TComponent FindFirstChildComponent<TComponent>(Transform root, string nameToken) where TComponent : Component
    {
        if (root == null)
            return null;

        TComponent[] components = root.GetComponentsInChildren<TComponent>(true);

        for (int index = 0; index < components.Length; index++)
        {
            TComponent component = components[index];

            if (component != null && ContainsOrdinalIgnoreCase(component.gameObject.name, nameToken))
                return component;
        }

        return components.Length > 0 ? components[0] : null;
    }

    /// <summary>
    /// Resolves the nearest parent root whose name contains the requested token.
    /// </summary>
    /// <param name="start">Transform where the search starts.</param>
    /// <param name="nameToken">Name token used to match the parent root.</param>
    /// <returns>Resolved root object, or the start object when no parent matches.</returns>
    private static GameObject ResolveSectionRoot(Transform start, string nameToken)
    {
        if (start == null)
            return null;

        Transform current = start;

        while (current != null)
        {
            if (ContainsOrdinalIgnoreCase(current.gameObject.name, nameToken))
                return current.gameObject;

            current = current.parent;
        }

        return start.gameObject;
    }

    /// <summary>
    /// Resolves a common parent host for two optional section transforms.
    /// </summary>
    /// <param name="first">First transform.</param>
    /// <param name="second">Second transform.</param>
    /// <param name="fallback">Fallback object returned when no transform is assigned.</param>
    /// <returns>Common parent object, first parent object, or fallback.</returns>
    private static GameObject ResolveCommonSectionHost(Transform first, Transform second, GameObject fallback)
    {
        if (first == null && second == null)
            return fallback;

        if (first == null)
            return second.parent != null ? second.parent.gameObject : second.gameObject;

        if (second == null)
            return first.parent != null ? first.parent.gameObject : first.gameObject;

        Transform firstParent = first.parent;

        while (firstParent != null)
        {
            if (second.IsChildOf(firstParent))
                return firstParent.gameObject;

            firstParent = firstParent.parent;
        }

        return first.parent != null ? first.parent.gameObject : first.gameObject;
    }

    /// <summary>
    /// Checks whether a string contains a token using ordinal case-insensitive comparison.
    /// </summary>
    /// <param name="value">String value to inspect.</param>
    /// <param name="token">Token to find.</param>
    /// <returns>True when the token is present.</returns>
    private static bool ContainsOrdinalIgnoreCase(string value, string token)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(token))
            return false;

        return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }
    #endregion

    #region Camera References
    /// <summary>
    /// Ensures the additive UI camera can rebuild URP camera stacking at runtime without cross-scene references.
    /// </summary>
    /// <param name="gameplayUiScene">Scene that owns gameplay UI roots.</param>
    private static void EnsureGameplayUiCameraStackBridge(Scene gameplayUiScene)
    {
        Camera uiCamera = FindCameraByName(gameplayUiScene, "UI Camera");

        if (uiCamera == null)
            return;

        ConfigureGameplayUiCamera(uiCamera);

        GameSceneUiCameraStackBridge bridge = EnsureComponent<GameSceneUiCameraStackBridge>(uiCamera.gameObject);
        SerializedObject serializedBridge = new SerializedObject(bridge);
        serializedBridge.Update();
        SetObjectReference(serializedBridge, "uiCamera", uiCamera);
        SetString(serializedBridge, "baseCameraTag", "MainCamera");
        SetBool(serializedBridge, "removeFromStackOnDisable", true);
        serializedBridge.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bridge);
    }

    /// <summary>
    /// Configures the additive UI camera as a post-process-free URP overlay camera.
    /// </summary>
    /// <param name="uiCamera">Camera owned by the gameplay UI scene.</param>
    private static void ConfigureGameplayUiCamera(Camera uiCamera)
    {
        // UI cameras must be overlays and must not contribute post-processing to the base stack.
        UniversalAdditionalCameraData uiCameraData = EnsureComponent<UniversalAdditionalCameraData>(uiCamera.gameObject);
        uiCameraData.renderType = CameraRenderType.Overlay;
        uiCameraData.renderPostProcessing = false;

        // Keep the setup aligned with the shared camera layer contract when the UI layer exists.
        if (GameSceneCameraLayerUtility.TryResolveLayerMask(GameSceneCameraLayerUtility.UiLayerName, out int uiLayerMask))
            uiCamera.cullingMask = uiLayerMask;

        // Persist both camera and URP metadata changes in the edited scene.
        EditorUtility.SetDirty(uiCamera);
        EditorUtility.SetDirty(uiCameraData);
    }

    /// <summary>
    /// Restores screen-space camera canvas references after UI roots have moved into the companion scene.
    /// </summary>
    /// <param name="gameplayUiScene">Scene that owns gameplay UI roots.</param>
    private static void EnsureGameplayUiCameraReferences(Scene gameplayUiScene)
    {
        Camera uiCamera = FindCameraByName(gameplayUiScene, "UI Camera");

        if (uiCamera == null)
            return;

        System.Collections.Generic.List<Canvas> canvases = FindComponentsInScene<Canvas>(gameplayUiScene);

        for (int index = 0; index < canvases.Count; index++)
        {
            Canvas canvas = canvases[index];

            if (canvas == null)
                continue;

            if (canvas.renderMode != RenderMode.ScreenSpaceCamera)
                continue;

            if (canvas.worldCamera != null)
                continue;

            canvas.worldCamera = uiCamera;
            EditorUtility.SetDirty(canvas);
        }
    }

    /// <summary>
    /// Finds one camera by GameObject name inside an opened scene.
    /// </summary>
    /// <param name="scene">Scene searched for the camera.</param>
    /// <param name="cameraName">Exact GameObject name expected for the camera.</param>
    /// <returns>Matching camera or null when missing.</returns>
    private static Camera FindCameraByName(Scene scene, string cameraName)
    {
        System.Collections.Generic.List<Camera> cameras = FindComponentsInScene<Camera>(scene);

        for (int index = 0; index < cameras.Count; index++)
        {
            Camera camera = cameras[index];

            if (camera == null)
                continue;

            if (string.Equals(camera.gameObject.name, cameraName, StringComparison.Ordinal))
                return camera;
        }

        return null;
    }
    #endregion

    #region Camera Stack Cleanup
    /// <summary>
    /// Removes null or cross-scene entries from URP camera stacks after the UI camera has been separated.
    /// </summary>
    /// <param name="scene">Scene whose camera stack references should be normalized before saving.</param>
    private static void CleanCameraStacks(Scene scene)
    {
        System.Collections.Generic.List<UniversalAdditionalCameraData> cameraDataList = FindComponentsInScene<UniversalAdditionalCameraData>(scene);

        for (int dataIndex = 0; dataIndex < cameraDataList.Count; dataIndex++)
        {
            UniversalAdditionalCameraData cameraData = cameraDataList[dataIndex];

            if (cameraData == null)
                continue;

            if (cameraData.renderType != CameraRenderType.Base)
                continue;

            System.Collections.Generic.List<Camera> cameraStack = cameraData.cameraStack;

            if (cameraStack == null)
                continue;

            for (int stackIndex = cameraStack.Count - 1; stackIndex >= 0; stackIndex--)
            {
                Camera stackedCamera = cameraStack[stackIndex];

                if (stackedCamera == null || stackedCamera.gameObject.scene != scene)
                    cameraStack.RemoveAt(stackIndex);
            }

            EditorUtility.SetDirty(cameraData);
        }
    }
    #endregion

    #region Event System
    /// <summary>
    /// Ensures the gameplay UI scene owns one EventSystem and removes the gameplay-scene duplicate.
    /// </summary>
    /// <param name="gameplayScene">Scene currently holding gameplay simulation content.</param>
    /// <param name="gameplayUiScene">Scene that should own gameplay UI roots.</param>
    private static void EnsureGameplayUiEventSystem(Scene gameplayScene, Scene gameplayUiScene)
    {
        EventSystem uiEventSystem = FindFirstComponentInScene<EventSystem>(gameplayUiScene);
        EventSystem gameplayEventSystem = FindFirstComponentInScene<EventSystem>(gameplayScene);

        if (uiEventSystem == null && gameplayEventSystem != null)
        {
            SceneManager.MoveGameObjectToScene(gameplayEventSystem.transform.root.gameObject, gameplayUiScene);
            EnsureEventSystemCoordinator(gameplayEventSystem);
            return;
        }

        if (uiEventSystem == null)
        {
            CreateGameplayUiEventSystem(gameplayUiScene);
            return;
        }

        EnsureEventSystemCoordinator(uiEventSystem);

        if (gameplayEventSystem != null)
            UnityEngine.Object.DestroyImmediate(gameplayEventSystem.transform.root.gameObject);
    }

    /// <summary>
    /// Creates an Input System backed EventSystem when the migrated UI scene had none.
    /// </summary>
    /// <param name="gameplayUiScene">Scene receiving the generated EventSystem.</param>
    private static void CreateGameplayUiEventSystem(Scene gameplayUiScene)
    {
        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        InputSystemUIInputModule inputModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();
        EnsureEventSystemCoordinator(eventSystemObject.GetComponent<EventSystem>());
        SceneManager.MoveGameObjectToScene(eventSystemObject, gameplayUiScene);
    }

    /// <summary>
    /// Ensures one EventSystem has the additive-transition coordinator attached and wired.
    /// </summary>
    /// <param name="eventSystem">EventSystem that should be coordinated.</param>
    private static void EnsureEventSystemCoordinator(EventSystem eventSystem)
    {
        if (eventSystem == null)
            return;

        GameSceneEventSystemCoordinator coordinator = EnsureComponent<GameSceneEventSystemCoordinator>(eventSystem.gameObject);
        SerializedObject serializedCoordinator = new SerializedObject(coordinator);
        serializedCoordinator.Update();
        SetObjectReference(serializedCoordinator, "eventSystem", eventSystem);
        serializedCoordinator.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(coordinator);
    }
    #endregion

    #endregion
}
