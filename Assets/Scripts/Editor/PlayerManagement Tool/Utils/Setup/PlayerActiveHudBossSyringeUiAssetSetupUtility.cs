using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static PlayerHudUiAssetSetupSharedUtility;

/// <summary>
/// Updates authored UI assets that host active power-up and boss syringe HUD widgets.
/// </summary>
public static class PlayerActiveHudBossSyringeUiAssetSetupUtility
{
    #region Constants
    private const string PlayerBarsPrefabPath = "Assets/Prefabs/UI/PlayerBars VerticalBox.prefab";
    private const string PowerUpSlotPrefabPath = "Assets/Prefabs/UI/PF_UI_PowerUpsSlot.prefab";
    private const string BossHudPrefabPath = "Assets/Prefabs/UI/PF_BossHUD.prefab";
    private const string MainUiScenePath = "Assets/Scenes/Testing/Main Scenes/SCN_MainScene/SCN_MainScene_UI.unity";
    private const string ChargeRingMaterialPath = "Assets/2D/Materials/M_UI_PowerUpChargeSemiRing.mat";
    private const string CooldownIconMaterialPath = "Assets/2D/Materials/M_UI_PowerUpCooldownIcon.mat";
    private const string ChargeRingShaderName = "Custom/UI/PowerUpChargeSemiRing";
    private const string CooldownIconShaderName = "Custom/UI/PowerUpCooldownIcon";
    private const string SourceHealthSyringeName = "PlayerHealthSyringe";
    private const string SourceShieldSyringeName = "PlayerShieldSyringe";
    private const string ActiveEnergySyringeName = "ActiveEnergySyringe";
    private const string ActiveChargeRingName = "ActiveChargeSemiRing";
    private const string BossHealthSyringeName = "BossHealthSyringe";
    private const string BossShieldSyringeName = "BossShieldSyringe";
    private const float SceneSlotScreenMargin = 32f;
    #endregion

    #region Methods

    #region Entry Point
    /// <summary>
    /// Batchmode entry point used to update UI prefabs and the gameplay UI scene after code changes compile.
    /// </summary>
    public static void Run()
    {
        Material chargeRingMaterial = EnsureMaterial(ChargeRingMaterialPath, ChargeRingShaderName);
        Material cooldownIconMaterial = EnsureMaterial(CooldownIconMaterialPath, CooldownIconShaderName);
        GameObject sourceRoot = PrefabUtility.LoadPrefabContents(PlayerBarsPrefabPath);

        try
        {
            PlayerSyringeBarView sourceHealthSyringe = FindComponentByName<PlayerSyringeBarView>(sourceRoot.transform,
                                                                                                  SourceHealthSyringeName);
            PlayerSyringeBarView sourceShieldSyringe = FindComponentByName<PlayerSyringeBarView>(sourceRoot.transform,
                                                                                                  SourceShieldSyringeName);
            PlayerUiVisualPreset editorPreviewUiPreset = PlayerActiveHudBossSyringeUiAssetSetupPreviewUtility.ResolveEditorPreviewUiPreset(sourceRoot);
            PlayerVisualPreset editorPreviewPreset = PlayerActiveHudBossSyringeUiAssetSetupPreviewUtility.ResolveEditorPreviewPreset(sourceRoot);

            if (sourceHealthSyringe == null || sourceShieldSyringe == null)
                throw new InvalidOperationException("PlayerBars VerticalBox is missing source syringe views.");

            PlayerHudExperienceBossPortraitAssetSetupUtility.ConfigurePlayerExperienceSyringe(sourceRoot, sourceHealthSyringe);
            PrefabUtility.SaveAsPrefabAsset(sourceRoot, PlayerBarsPrefabPath);
            UpdatePowerUpSlotPrefab(sourceHealthSyringe, chargeRingMaterial, cooldownIconMaterial, editorPreviewUiPreset, editorPreviewPreset);
            UpdateBossHudPrefab(sourceHealthSyringe, sourceShieldSyringe);
            UpdateMainUiScene(sourceHealthSyringe,
                              sourceShieldSyringe,
                              chargeRingMaterial,
                              cooldownIconMaterial,
                              editorPreviewUiPreset,
                              editorPreviewPreset);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(sourceRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PlayerActiveHudBossSyringeUiAssetSetupUtility] UI assets updated.");
    }
    #endregion

    #region Prefabs
    /// <summary>
    /// Updates the active power-up slot prefab with the redesigned energy syringe, charge semiring, and cooldown icon view.
    /// </summary>
    /// <param name="sourceEnergySyringe">Preauthored player syringe used as the clone source for active energy.</param>
    /// <param name="chargeRingMaterial">Material template assigned to the charge semiring graphic.</param>
    /// <param name="cooldownIconMaterial">Material template assigned to the cooldown icon view.</param>
    /// <param name="editorPreviewUiPreset">Player UI Visual Preset used by Edit Mode active-slot previews.</param>
    /// <param name="editorPreviewPreset">Legacy Player Visual Preset fallback used by Edit Mode active-slot previews.</param>
    private static void UpdatePowerUpSlotPrefab(PlayerSyringeBarView sourceEnergySyringe,
                                                Material chargeRingMaterial,
                                                Material cooldownIconMaterial,
                                                PlayerUiVisualPreset editorPreviewUiPreset,
                                                PlayerVisualPreset editorPreviewPreset)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PowerUpSlotPrefabPath);

        try
        {
            ConfigurePowerUpSlotHierarchy(prefabRoot,
                                          sourceEnergySyringe,
                                          chargeRingMaterial,
                                          cooldownIconMaterial,
                                          editorPreviewUiPreset,
                                          editorPreviewPreset);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PowerUpSlotPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    /// <summary>
    /// Updates the boss HUD prefab with preauthored health, shield, and portrait views.
    /// </summary>
    /// <param name="sourceHealthSyringe">Preauthored player health syringe used as the boss health source.</param>
    /// <param name="sourceShieldSyringe">Preauthored player shield syringe used as the boss shield source.</param>
    private static void UpdateBossHudPrefab(PlayerSyringeBarView sourceHealthSyringe,
                                            PlayerSyringeBarView sourceShieldSyringe)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(BossHudPrefabPath);

        try
        {
            ConfigureBossHudHierarchy(prefabRoot, sourceHealthSyringe, sourceShieldSyringe);
            PlayerHudExperienceBossPortraitAssetSetupUtility.ConfigureBossPortrait(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, BossHudPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
    #endregion

    #region Scene
    /// <summary>
    /// Updates the main gameplay UI scene so HUDManager points to redesigned authored HUD views.
    /// </summary>
    /// <param name="sourceHealthSyringe">Preauthored player health syringe used as the clone source for scene slots and boss health.</param>
    /// <param name="sourceShieldSyringe">Preauthored player shield syringe used as the clone source for boss shield.</param>
    /// <param name="chargeRingMaterial">Material template assigned to scene charge semiring graphics.</param>
    /// <param name="cooldownIconMaterial">Material template assigned to scene cooldown icon views.</param>
    /// <param name="editorPreviewUiPreset">Player UI Visual Preset used by Edit Mode active-slot previews.</param>
    /// <param name="editorPreviewPreset">Legacy Player Visual Preset fallback used by Edit Mode active-slot previews.</param>
    private static void UpdateMainUiScene(PlayerSyringeBarView sourceHealthSyringe,
                                          PlayerSyringeBarView sourceShieldSyringe,
                                          Material chargeRingMaterial,
                                          Material cooldownIconMaterial,
                                          PlayerUiVisualPreset editorPreviewUiPreset,
                                          PlayerVisualPreset editorPreviewPreset)
    {
        Scene scene = EditorSceneManager.OpenScene(MainUiScenePath, OpenSceneMode.Single);
        HUDManager hudManager = FindComponentInScene<HUDManager>(scene);

        if (hudManager == null)
            throw new InvalidOperationException("SCN_MainScene_UI is missing HUDManager.");

        PlayerHealthBarsHudView healthBarsView = FindComponentInScene<PlayerHealthBarsHudView>(scene);
        EnemyBossHudPresentation bossPresentation = FindComponentInScene<EnemyBossHudPresentation>(scene);
        TMP_Text playerLevelText = null;

        if (healthBarsView != null)
        {
            playerLevelText = PlayerHudExperienceBossPortraitAssetSetupUtility.ConfigurePlayerExperienceSyringe(healthBarsView.gameObject,
                                                                                                               sourceHealthSyringe);
        }

        if (bossPresentation != null)
        {
            PlayerHudExperienceBossPortraitAssetSetupUtility.ConfigureBossPortrait(bossPresentation.gameObject);
            ConfigureBossHudHierarchy(bossPresentation.gameObject, sourceHealthSyringe, sourceShieldSyringe);
            PlayerHudExperienceBossPortraitAssetSetupUtility.ConfigureBossPortrait(bossPresentation.gameObject);
        }

        HUDPowerUpOverlaySectionComponent overlaySection = EnsureComponent<HUDPowerUpOverlaySectionComponent>(ResolvePowerUpOverlayHost(hudManager));
        SerializedObject overlayObject = new SerializedObject(overlaySection);
        GameObject primarySlotRoot = ResolveScenePowerUpSlotRoot(overlayObject, "primaryPowerUpSlotRootObject", "Primary");
        GameObject secondarySlotRoot = ResolveScenePowerUpSlotRoot(overlayObject, "secondaryPowerUpSlotRootObject", "Secondary");
        PlayerActivePowerUpSlotHudView primaryView = ConfigureScenePowerUpSlot(primarySlotRoot,
                                                                               sourceHealthSyringe,
                                                                               chargeRingMaterial,
                                                                               cooldownIconMaterial,
                                                                               editorPreviewUiPreset,
                                                                               editorPreviewPreset);
        PlayerActivePowerUpSlotHudView secondaryView = BindExistingScenePowerUpSlot(secondarySlotRoot,
                                                                                    editorPreviewUiPreset,
                                                                                    editorPreviewPreset);

        SetObjectReference(overlayObject, "primaryPowerUpSlotView", primaryView);
        SetObjectReference(overlayObject, "secondaryPowerUpSlotView", secondaryView);
        SetObjectReference(overlayObject, "primaryPowerUpIconImage", primaryView != null ? primaryView.IconImage : null);
        SetObjectReference(overlayObject, "secondaryPowerUpIconImage", secondaryView != null ? secondaryView.IconImage : null);
        SetObjectReference(overlayObject, "primaryPowerUpSlotRootObject", primaryView != null ? primaryView.gameObject : primarySlotRoot);
        SetObjectReference(overlayObject, "secondaryPowerUpSlotRootObject", secondaryView != null ? secondaryView.gameObject : secondarySlotRoot);
        overlayObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(overlaySection);

        if (playerLevelText != null)
            WireLevelExperienceSection(hudManager, healthBarsView, playerLevelText);

        SerializedObject hudObject = new SerializedObject(hudManager);
        SetObjectReference(hudObject, "playerHealthBarsView", healthBarsView);
        SetObjectReference(hudObject, "levelExperienceSection", healthBarsView != null ? healthBarsView.GetComponent<HUDLevelExperienceSection>() : null);
        SetObjectReference(hudObject, "powerUpOverlaySection", overlaySection);
        hudObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hudManager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>
    /// Configures one scene slot and returns its redesigned view.
    /// </summary>
    /// <param name="slotRoot">Scene slot root GameObject.</param>
    /// <param name="sourceEnergySyringe">Preauthored syringe used when the scene instance still needs generated children.</param>
    /// <param name="chargeRingMaterial">Material template for charge semiring generation.</param>
    /// <param name="cooldownIconMaterial">Material template for cooldown icon generation.</param>
    /// <param name="editorPreviewUiPreset">Player UI Visual Preset used by Edit Mode active-slot previews.</param>
    /// <param name="editorPreviewPreset">Legacy Player Visual Preset fallback used by Edit Mode active-slot previews.</param>
    /// <returns>The configured slot view, or null when the slot root is missing.</returns>
    private static PlayerActivePowerUpSlotHudView ConfigureScenePowerUpSlot(GameObject slotRoot,
                                                                            PlayerSyringeBarView sourceEnergySyringe,
                                                                            Material chargeRingMaterial,
                                                                            Material cooldownIconMaterial,
                                                                            PlayerUiVisualPreset editorPreviewUiPreset,
                                                                            PlayerVisualPreset editorPreviewPreset)
    {
        if (slotRoot == null)
            return null;

        PlayerActivePowerUpSlotHudView slotView = ConfigurePowerUpSlotHierarchy(slotRoot,
                                                                                sourceEnergySyringe,
                                                                                chargeRingMaterial,
                                                                                cooldownIconMaterial,
                                                                                editorPreviewUiPreset,
                                                                                editorPreviewPreset,
                                                                                false);
        KeepRightAnchoredSceneSlotInsideScreen(slotRoot);
        return slotView;
    }

    /// <summary>
    /// Binds an existing scene slot view without rewriting its authored child layout.
    /// </summary>
    /// <param name="slotRoot">Scene slot root GameObject.</param>
    /// <param name="editorPreviewUiPreset">Player UI Visual Preset used by Edit Mode active-slot previews.</param>
    /// <param name="editorPreviewPreset">Legacy Player Visual Preset fallback used by Edit Mode active-slot previews.</param>
    /// <returns>The existing or newly attached slot view, or null when the slot root is missing.</returns>
    private static PlayerActivePowerUpSlotHudView BindExistingScenePowerUpSlot(GameObject slotRoot,
                                                                               PlayerUiVisualPreset editorPreviewUiPreset,
                                                                               PlayerVisualPreset editorPreviewPreset)
    {
        if (slotRoot == null)
            return null;

        PlayerActivePowerUpSlotHudView slotView = EnsureComponent<PlayerActivePowerUpSlotHudView>(slotRoot);
        Image iconImage = FindComponentByName<Image>(slotRoot.transform, "IconImage");
        PlayerSyringeBarView energySyringe = FindComponentByName<PlayerSyringeBarView>(slotRoot.transform,
                                                                                       ActiveEnergySyringeName);
        PlayerPowerUpChargeRingView chargeRing = FindComponentByName<PlayerPowerUpChargeRingView>(slotRoot.transform,
                                                                                                  ActiveChargeRingName);
        PlayerPowerUpIconCooldownView cooldownView = iconImage != null
            ? iconImage.GetComponent<PlayerPowerUpIconCooldownView>()
            : null;
        SerializedObject slotObject = new SerializedObject(slotView);
        SetObjectReference(slotObject, "iconImage", iconImage);
        SetObjectReference(slotObject, "energySyringe", energySyringe);
        SetObjectReference(slotObject, "chargeRing", chargeRing);
        SetObjectReference(slotObject, "iconCooldown", cooldownView);
        SetObjectReference(slotObject, "editorPreviewUiPreset", editorPreviewUiPreset);
        SetObjectReference(slotObject, "editorPreviewPreset", editorPreviewPreset);
        slotObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(slotView);
        return slotView;
    }
    #endregion

    #region Active Power-Up Slot
    /// <summary>
    /// Ensures one active power-up slot hierarchy contains the redesigned authored views and serialized references.
    /// </summary>
    /// <param name="slotRoot">Root GameObject of the slot prefab or scene instance.</param>
    /// <param name="sourceEnergySyringe">Preauthored syringe source cloned into the slot.</param>
    /// <param name="chargeRingMaterial">Material template assigned to the charge semiring.</param>
    /// <param name="cooldownIconMaterial">Material template assigned to the icon cooldown view.</param>
    /// <param name="editorPreviewUiPreset">Player UI Visual Preset used by Edit Mode active-slot previews.</param>
    /// <param name="editorPreviewPreset">Legacy Player Visual Preset fallback used by Edit Mode active-slot previews.</param>
    /// <param name="resizeRoot">Whether the slot root should receive the prefab default dimensions.</param>
    /// <returns>The slot view component configured on the slot root.</returns>
    private static PlayerActivePowerUpSlotHudView ConfigurePowerUpSlotHierarchy(GameObject slotRoot,
                                                                                PlayerSyringeBarView sourceEnergySyringe,
                                                                                Material chargeRingMaterial,
                                                                                Material cooldownIconMaterial,
                                                                                PlayerUiVisualPreset editorPreviewUiPreset,
                                                                                PlayerVisualPreset editorPreviewPreset,
                                                                                bool resizeRoot = true)
    {
        RectTransform rootRect = EnsureRectTransform(slotRoot);

        if (resizeRoot)
            rootRect.sizeDelta = new Vector2(540f, 150f);

        Transform verticalBox = FindChild(slotRoot.transform, "VerticalBox");
        Transform energyParent = verticalBox != null ? verticalBox : slotRoot.transform;
        PlayerSyringeBarView energySyringe = EnsureClonedSyringe(sourceEnergySyringe,
                                                                 energyParent,
                                                                 ActiveEnergySyringeName,
                                                                 slotRoot.layer);
        ConfigureRectTransform(energySyringe.Root,
                               new Vector2(320f, 72f),
                               new Vector2(0f, 0.5f),
                               new Vector2(0f, 0.5f),
                               new Vector2(0f, 0.5f),
                               Vector2.zero);
        ConfigureLayoutElement(energySyringe.gameObject, 285f, 62f, 320f, 72f);
        energySyringe.gameObject.SetActive(true);

        Image iconImage = FindComponentByName<Image>(slotRoot.transform, "IconImage");
        PlayerPowerUpChargeRingView chargeRing = EnsureChargeRing(slotRoot.transform, chargeRingMaterial, slotRoot.layer);
        PlayerPowerUpIconCooldownView cooldownView = EnsureIconCooldown(iconImage, cooldownIconMaterial);
        PlayerActivePowerUpSlotHudView slotView = EnsureComponent<PlayerActivePowerUpSlotHudView>(slotRoot);

        SerializedObject slotObject = new SerializedObject(slotView);
        SetObjectReference(slotObject, "iconImage", iconImage);
        SetObjectReference(slotObject, "energySyringe", energySyringe);
        SetObjectReference(slotObject, "chargeRing", chargeRing);
        SetObjectReference(slotObject, "iconCooldown", cooldownView);
        SetObjectReference(slotObject, "editorPreviewUiPreset", editorPreviewUiPreset);
        SetObjectReference(slotObject, "editorPreviewPreset", editorPreviewPreset);
        slotObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(slotView);

        SetLegacySlotBarDefaultVisibility(slotRoot.transform, "EnergyBar", false);
        SetLegacySlotBarDefaultVisibility(slotRoot.transform, "ChargeBar", false);
        return slotView;
    }

    /// <summary>
    /// Ensures the procedural charge semiring exists as an authored child behind the slot icon.
    /// </summary>
    /// <param name="slotRoot">Slot root receiving the semiring child.</param>
    /// <param name="chargeRingMaterial">Material template assigned to the semiring graphic and view.</param>
    /// <param name="layer">Layer inherited from the containing UI root.</param>
    /// <returns>The configured charge-ring view.</returns>
    private static PlayerPowerUpChargeRingView EnsureChargeRing(Transform slotRoot,
                                                                Material chargeRingMaterial,
                                                                int layer)
    {
        PlayerPowerUpChargeRingView chargeRing = FindComponentByName<PlayerPowerUpChargeRingView>(slotRoot,
                                                                                                  ActiveChargeRingName);

        if (chargeRing == null)
        {
            GameObject ringObject = new GameObject(ActiveChargeRingName,
                                                   typeof(RectTransform),
                                                   typeof(CanvasRenderer),
                                                   typeof(PlayerPowerUpChargeRingGraphic),
                                                   typeof(PlayerPowerUpChargeRingView));
            ringObject.transform.SetParent(slotRoot, false);
            chargeRing = ringObject.GetComponent<PlayerPowerUpChargeRingView>();
        }

        SetLayerRecursively(chargeRing.gameObject, layer);
        RectTransform rectTransform = EnsureRectTransform(chargeRing.gameObject);
        ConfigureRectTransform(rectTransform,
                               new Vector2(136f, 136f),
                               new Vector2(0.5f, 0.5f),
                               new Vector2(0f, 0.5f),
                               new Vector2(0f, 0.5f),
                               new Vector2(52f, 0f));
        rectTransform.SetAsFirstSibling();

        PlayerPowerUpChargeRingGraphic graphic = chargeRing.GetComponent<PlayerPowerUpChargeRingGraphic>();
        graphic.raycastTarget = false;
        graphic.color = Color.white;
        graphic.material = chargeRingMaterial;

        SerializedObject viewObject = new SerializedObject(chargeRing);
        SetObjectReference(viewObject, "graphic", graphic);
        SetObjectReference(viewObject, "materialTemplate", chargeRingMaterial);
        viewObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(chargeRing);
        return chargeRing;
    }

    /// <summary>
    /// Ensures the icon image owns the authored cooldown material view.
    /// </summary>
    /// <param name="iconImage">Icon image driven by the HUD runtime.</param>
    /// <param name="cooldownIconMaterial">Material template assigned to the cooldown view.</param>
    /// <returns>The configured cooldown view, or null when the slot has no icon image.</returns>
    private static PlayerPowerUpIconCooldownView EnsureIconCooldown(Image iconImage, Material cooldownIconMaterial)
    {
        if (iconImage == null)
            return null;

        PlayerPowerUpIconCooldownView cooldownView = EnsureComponent<PlayerPowerUpIconCooldownView>(iconImage.gameObject);
        SerializedObject viewObject = new SerializedObject(cooldownView);
        SetObjectReference(viewObject, "iconImage", iconImage);
        SetObjectReference(viewObject, "materialTemplate", cooldownIconMaterial);
        viewObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cooldownView);
        return cooldownView;
    }

    /// <summary>
    /// Sets the default active state for one legacy bar root while keeping it available as a runtime fallback.
    /// </summary>
    /// <param name="slotRoot">Slot hierarchy root.</param>
    /// <param name="barName">Legacy bar GameObject name.</param>
    /// <param name="isVisible">Default prefab or scene active state.</param>
    private static void SetLegacySlotBarDefaultVisibility(Transform slotRoot, string barName, bool isVisible)
    {
        Transform legacyRoot = FindChild(slotRoot, barName);

        if (legacyRoot == null)
            return;

        legacyRoot.gameObject.SetActive(isVisible);
    }

    /// <summary>
    /// Keeps redesigned right-anchored scene slots inside the UI camera canvas after their authored width changes.
    /// </summary>
    /// <param name="slotRoot">Scene slot root updated by the setup utility.</param>
    private static void KeepRightAnchoredSceneSlotInsideScreen(GameObject slotRoot)
    {
        RectTransform rootRect = slotRoot != null ? slotRoot.GetComponent<RectTransform>() : null;

        if (rootRect == null)
            return;

        if (!Mathf.Approximately(rootRect.anchorMin.x, 1f) || !Mathf.Approximately(rootRect.anchorMax.x, 1f))
            return;

        float rightExtent = Mathf.Max(0f, rootRect.sizeDelta.x * (1f - rootRect.pivot.x));
        float maximumAnchoredX = -rightExtent - SceneSlotScreenMargin;

        if (rootRect.anchoredPosition.x <= maximumAnchoredX)
            return;

        rootRect.anchoredPosition = new Vector2(maximumAnchoredX, rootRect.anchoredPosition.y);
        EditorUtility.SetDirty(rootRect);
    }

    /// <summary>
    /// Resolves the scene object that should own the active power-up overlay section.
    /// </summary>
    /// <param name="hudManager">HUD manager used as the final fallback.</param>
    /// <returns>Overlay section host object.</returns>
    private static GameObject ResolvePowerUpOverlayHost(HUDManager hudManager)
    {
        HUDPowerUpOverlaySectionComponent existingSection = UnityEngine.Object.FindFirstObjectByType<HUDPowerUpOverlaySectionComponent>(FindObjectsInactive.Include);

        if (existingSection != null)
            return existingSection.gameObject;

        PlayerActivePowerUpSlotHudView primarySlot = ResolveSlotViewByName("Primary");

        if (primarySlot != null && primarySlot.transform.parent != null)
            return primarySlot.transform.parent.gameObject;

        return hudManager.gameObject;
    }

    /// <summary>
    /// Resolves one active power-up slot root from an overlay section or scene slot name.
    /// </summary>
    /// <param name="overlayObject">Serialized overlay section object.</param>
    /// <param name="rootPropertyName">Serialized root property name.</param>
    /// <param name="slotNameToken">Name token used when the section has no root assigned yet.</param>
    /// <returns>Resolved slot root or null.</returns>
    private static GameObject ResolveScenePowerUpSlotRoot(SerializedObject overlayObject, string rootPropertyName, string slotNameToken)
    {
        SerializedProperty rootProperty = overlayObject.FindProperty(rootPropertyName);
        GameObject slotRoot = rootProperty != null ? rootProperty.objectReferenceValue as GameObject : null;

        if (slotRoot != null)
            return slotRoot;

        PlayerActivePowerUpSlotHudView slotView = ResolveSlotViewByName(slotNameToken);

        if (slotView != null)
            return slotView.gameObject;

        GameObject[] sceneObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int objectIndex = 0; objectIndex < sceneObjects.Length; objectIndex++)
        {
            GameObject sceneObject = sceneObjects[objectIndex];

            if (sceneObject != null && sceneObject.name.IndexOf(slotNameToken, StringComparison.OrdinalIgnoreCase) >= 0)
                return sceneObject;
        }

        return null;
    }

    /// <summary>
    /// Resolves one active power-up slot view by object name token.
    /// </summary>
    /// <param name="slotNameToken">Name token used to distinguish primary and secondary slots.</param>
    /// <returns>Matching slot view or null.</returns>
    private static PlayerActivePowerUpSlotHudView ResolveSlotViewByName(string slotNameToken)
    {
        PlayerActivePowerUpSlotHudView[] slotViews = UnityEngine.Object.FindObjectsByType<PlayerActivePowerUpSlotHudView>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int slotIndex = 0; slotIndex < slotViews.Length; slotIndex++)
        {
            PlayerActivePowerUpSlotHudView slotView = slotViews[slotIndex];

            if (slotView != null && slotView.gameObject.name.IndexOf(slotNameToken, StringComparison.OrdinalIgnoreCase) >= 0)
                return slotView;
        }

        return null;
    }

    /// <summary>
    /// Ensures the level and legacy experience section owns the moved player level reference.
    /// </summary>
    /// <param name="hudManager">HUD manager receiving the section reference.</param>
    /// <param name="healthBarsView">Health-bars view used as section host.</param>
    /// <param name="playerLevelText">Player level text generated by the experience syringe setup.</param>
    private static void WireLevelExperienceSection(HUDManager hudManager, PlayerHealthBarsHudView healthBarsView, TMP_Text playerLevelText)
    {
        if (healthBarsView == null || playerLevelText == null)
            return;

        HUDLevelExperienceSection section = EnsureComponent<HUDLevelExperienceSection>(healthBarsView.gameObject);
        SerializedObject sectionObject = new SerializedObject(section);
        SetObjectReference(sectionObject, "playerLevelText", playerLevelText);
        sectionObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(section);

        SerializedObject hudObject = new SerializedObject(hudManager);
        SetObjectReference(hudObject, "levelExperienceSection", section);
        hudObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hudManager);
    }
    #endregion

    #region Boss HUD
    /// <summary>
    /// Ensures the boss HUD hierarchy contains serialized health and shield syringe views.
    /// </summary>
    /// <param name="bossRoot">Boss HUD prefab root.</param>
    /// <param name="sourceHealthSyringe">Preauthored syringe source for boss health.</param>
    /// <param name="sourceShieldSyringe">Preauthored syringe source for boss shield.</param>
    private static void ConfigureBossHudHierarchy(GameObject bossRoot,
                                                  PlayerSyringeBarView sourceHealthSyringe,
                                                  PlayerSyringeBarView sourceShieldSyringe)
    {
        Transform panel = FindChild(bossRoot.transform, "Panel");
        Transform mirrorRoot = panel != null ? FindChild(panel, "BossBarsMirrorRoot") : null;
        Transform parent = mirrorRoot != null ? mirrorRoot : panel != null ? panel : bossRoot.transform;
        RemoveLegacyBossBarHierarchy(parent);

        if (panel != null && panel != parent)
            RemoveLegacyBossBarHierarchy(panel);

        PlayerSyringeBarView healthSyringe = EnsureClonedSyringe(sourceHealthSyringe,
                                                                 parent,
                                                                 BossHealthSyringeName,
                                                                 bossRoot.layer);
        PlayerSyringeBarView shieldSyringe = EnsureClonedSyringe(sourceShieldSyringe,
                                                                 parent,
                                                                 BossShieldSyringeName,
                                                                 bossRoot.layer);

        ConfigureRectTransform(healthSyringe.Root,
                               new Vector2(269f, 58f),
                               new Vector2(0f, 0.5f),
                               new Vector2(0f, 0.5f),
                               new Vector2(0f, 0.5f),
                               Vector2.zero);
        ConfigureRectTransform(shieldSyringe.Root,
                               new Vector2(269f, 50f),
                               new Vector2(0f, 0.5f),
                               new Vector2(0f, 0.5f),
                               new Vector2(0f, 0.5f),
                               Vector2.zero);
        ConfigureLayoutElement(healthSyringe.gameObject, 240f, 50f, 269f, 58f);
        ConfigureLayoutElement(shieldSyringe.gameObject, 240f, 46f, 269f, 50f);
        healthSyringe.gameObject.SetActive(true);
        shieldSyringe.gameObject.SetActive(false);

        EnemyBossHudPresentation presentation = bossRoot.GetComponent<EnemyBossHudPresentation>();

        if (presentation == null)
            return;

        SerializedObject presentationObject = new SerializedObject(presentation);
        SetObjectReference(presentationObject, "healthSyringeBar", healthSyringe);
        SetObjectReference(presentationObject, "shieldSyringeBar", shieldSyringe);
        presentationObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(presentation);
    }

    /// <summary>
    /// Removes boss HUD legacy liquid-bar image objects after the syringe redesign becomes authoritative.
    /// </summary>
    /// <param name="parent">Boss panel or root that may still contain legacy children.</param>
    private static void RemoveLegacyBossBarHierarchy(Transform parent)
    {
        DestroyChildIfFound(parent, "HealthBackground");
        DestroyChildIfFound(parent, "HealthFill");
        DestroyChildIfFound(parent, "HealthPointer");
        DestroyChildIfFound(parent, "HealthCover");
        DestroyChildIfFound(parent, "ShieldBackground");
        DestroyChildIfFound(parent, "ShieldFill");
        DestroyChildIfFound(parent, "ShieldPointer");
        DestroyChildIfFound(parent, "ShieldCover");
    }
    #endregion

    #region Materials
    /// <summary>
    /// Creates or loads a UI material template for one procedural shader.
    /// </summary>
    /// <param name="assetPath">Material asset path to load or create.</param>
    /// <param name="shaderName">Shader name required by the material.</param>
    /// <returns>The loaded or newly created material asset.</returns>
    private static Material EnsureMaterial(string assetPath, string shaderName)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

        if (material != null)
            return material;

        Shader shader = Shader.Find(shaderName);

        if (shader == null)
            throw new InvalidOperationException("Shader not found: " + shaderName);

        string directory = Path.GetDirectoryName(assetPath);

        if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            throw new InvalidOperationException("Material directory does not exist: " + directory);

        material = new Material(shader);
        material.name = Path.GetFileNameWithoutExtension(assetPath);
        AssetDatabase.CreateAsset(material, assetPath);
        EditorUtility.SetDirty(material);
        return material;
    }
    #endregion

    #endregion
}
