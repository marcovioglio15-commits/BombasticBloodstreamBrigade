using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

            if (sourceHealthSyringe == null || sourceShieldSyringe == null)
                throw new InvalidOperationException("PlayerBars VerticalBox is missing source syringe views.");

            UpdatePowerUpSlotPrefab(sourceHealthSyringe, chargeRingMaterial, cooldownIconMaterial);
            UpdateBossHudPrefab(sourceHealthSyringe, sourceShieldSyringe);
            UpdateMainUiScene(sourceHealthSyringe, chargeRingMaterial, cooldownIconMaterial);
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
    private static void UpdatePowerUpSlotPrefab(PlayerSyringeBarView sourceEnergySyringe,
                                                Material chargeRingMaterial,
                                                Material cooldownIconMaterial)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PowerUpSlotPrefabPath);

        try
        {
            ConfigurePowerUpSlotHierarchy(prefabRoot, sourceEnergySyringe, chargeRingMaterial, cooldownIconMaterial);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PowerUpSlotPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    /// <summary>
    /// Updates the boss HUD prefab with preauthored health and shield syringe views.
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
    /// Updates the main gameplay UI scene so HUDManager points to the redesigned active power-up slot views.
    /// </summary>
    /// <param name="sourceEnergySyringe">Preauthored player syringe used as the clone source for scene slot instances.</param>
    /// <param name="chargeRingMaterial">Material template assigned to scene charge semiring graphics.</param>
    /// <param name="cooldownIconMaterial">Material template assigned to scene cooldown icon views.</param>
    private static void UpdateMainUiScene(PlayerSyringeBarView sourceEnergySyringe,
                                          Material chargeRingMaterial,
                                          Material cooldownIconMaterial)
    {
        Scene scene = EditorSceneManager.OpenScene(MainUiScenePath, OpenSceneMode.Single);
        HUDManager hudManager = FindComponentInScene<HUDManager>(scene);

        if (hudManager == null)
            throw new InvalidOperationException("SCN_MainScene_UI is missing HUDManager.");

        SerializedObject hudObject = new SerializedObject(hudManager);
        PlayerActivePowerUpSlotHudView primaryView = ConfigureScenePowerUpSlot(hudObject,
                                                                               "primaryPowerUpSlotRootObject",
                                                                               sourceEnergySyringe,
                                                                               chargeRingMaterial,
                                                                               cooldownIconMaterial);
        PlayerActivePowerUpSlotHudView secondaryView = ConfigureScenePowerUpSlot(hudObject,
                                                                                 "secondaryPowerUpSlotRootObject",
                                                                                 sourceEnergySyringe,
                                                                                 chargeRingMaterial,
                                                                                 cooldownIconMaterial);

        SetObjectReference(hudObject, "primaryPowerUpSlotView", primaryView);
        SetObjectReference(hudObject, "secondaryPowerUpSlotView", secondaryView);
        hudObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hudManager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>
    /// Configures one scene slot referenced by HUDManager and returns its redesigned view.
    /// </summary>
    /// <param name="hudObject">Serialized HUDManager object containing the slot root reference.</param>
    /// <param name="slotRootPropertyName">Serialized property name for the slot root GameObject.</param>
    /// <param name="sourceEnergySyringe">Preauthored syringe used when the scene instance still needs generated children.</param>
    /// <param name="chargeRingMaterial">Material template for charge semiring generation.</param>
    /// <param name="cooldownIconMaterial">Material template for cooldown icon generation.</param>
    /// <returns>The configured slot view, or null when the slot root is missing.</returns>
    private static PlayerActivePowerUpSlotHudView ConfigureScenePowerUpSlot(SerializedObject hudObject,
                                                                            string slotRootPropertyName,
                                                                            PlayerSyringeBarView sourceEnergySyringe,
                                                                            Material chargeRingMaterial,
                                                                            Material cooldownIconMaterial)
    {
        SerializedProperty rootProperty = hudObject.FindProperty(slotRootPropertyName);
        GameObject slotRoot = rootProperty != null ? rootProperty.objectReferenceValue as GameObject : null;

        if (slotRoot == null)
            return null;

        PlayerActivePowerUpSlotHudView slotView = ConfigurePowerUpSlotHierarchy(slotRoot,
                                                                                sourceEnergySyringe,
                                                                                chargeRingMaterial,
                                                                                cooldownIconMaterial);
        KeepRightAnchoredSceneSlotInsideScreen(slotRoot);
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
    /// <returns>The slot view component configured on the slot root.</returns>
    private static PlayerActivePowerUpSlotHudView ConfigurePowerUpSlotHierarchy(GameObject slotRoot,
                                                                                PlayerSyringeBarView sourceEnergySyringe,
                                                                                Material chargeRingMaterial,
                                                                                Material cooldownIconMaterial)
    {
        RectTransform rootRect = EnsureRectTransform(slotRoot);
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
        Transform parent = panel != null ? panel : bossRoot.transform;
        RemoveLegacyBossBarHierarchy(parent);
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

    #region Shared Hierarchy
    /// <summary>
    /// Clones one authored syringe view from the player bars prefab when the target hierarchy does not already contain it.
    /// </summary>
    /// <param name="sourceView">Source syringe view whose child hierarchy and serialized references are copied.</param>
    /// <param name="parent">Target parent that should own the cloned syringe.</param>
    /// <param name="targetName">Stable generated child name.</param>
    /// <param name="layer">Layer assigned recursively to the clone.</param>
    /// <returns>The existing or newly cloned syringe view.</returns>
    private static PlayerSyringeBarView EnsureClonedSyringe(PlayerSyringeBarView sourceView,
                                                            Transform parent,
                                                            string targetName,
                                                            int layer)
    {
        PlayerSyringeBarView existingView = FindComponentByName<PlayerSyringeBarView>(parent, targetName);

        if (existingView != null)
        {
            existingView.transform.SetParent(parent, false);
            SetLayerRecursively(existingView.gameObject, layer);
            return existingView;
        }

        PlayerSyringeBarView clonedView = UnityEngine.Object.Instantiate(sourceView, parent);
        clonedView.name = targetName;
        SetLayerRecursively(clonedView.gameObject, layer);
        EditorUtility.SetDirty(clonedView);
        return clonedView;
    }

    /// <summary>
    /// Destroys one generated or legacy child when it exists under the target hierarchy.
    /// </summary>
    /// <param name="parent">Root searched recursively.</param>
    /// <param name="childName">Child name to remove.</param>
    private static void DestroyChildIfFound(Transform parent, string childName)
    {
        Transform child = FindChild(parent, childName);

        if (child == null)
            return;

        UnityEngine.Object.DestroyImmediate(child.gameObject);
    }

    /// <summary>
    /// Ensures a GameObject has a RectTransform and returns it.
    /// </summary>
    /// <param name="targetObject">GameObject expected to be a UI node.</param>
    /// <returns>The existing or newly added RectTransform.</returns>
    private static RectTransform EnsureRectTransform(GameObject targetObject)
    {
        RectTransform rectTransform = targetObject.GetComponent<RectTransform>();

        if (rectTransform != null)
            return rectTransform;

        return targetObject.AddComponent<RectTransform>();
    }

    /// <summary>
    /// Configures one RectTransform with explicit anchors, pivot, size, and anchored position.
    /// </summary>
    /// <param name="rectTransform">RectTransform receiving the layout values.</param>
    /// <param name="size">Size delta in local UI units.</param>
    /// <param name="pivot">Pivot normalized in the rect.</param>
    /// <param name="anchorMin">Minimum anchor normalized in the parent.</param>
    /// <param name="anchorMax">Maximum anchor normalized in the parent.</param>
    /// <param name="anchoredPosition">Anchored local UI position.</param>
    private static void ConfigureRectTransform(RectTransform rectTransform,
                                               Vector2 size,
                                               Vector2 pivot,
                                               Vector2 anchorMin,
                                               Vector2 anchorMax,
                                               Vector2 anchoredPosition)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// Ensures one layout element has stable minimum and preferred dimensions.
    /// </summary>
    /// <param name="targetObject">GameObject receiving the LayoutElement.</param>
    /// <param name="minimumWidth">Minimum layout width.</param>
    /// <param name="minimumHeight">Minimum layout height.</param>
    /// <param name="preferredWidth">Preferred layout width.</param>
    /// <param name="preferredHeight">Preferred layout height.</param>
    private static void ConfigureLayoutElement(GameObject targetObject,
                                               float minimumWidth,
                                               float minimumHeight,
                                               float preferredWidth,
                                               float preferredHeight)
    {
        LayoutElement layoutElement = EnsureComponent<LayoutElement>(targetObject);
        layoutElement.ignoreLayout = false;
        layoutElement.minWidth = minimumWidth;
        layoutElement.minHeight = minimumHeight;
        layoutElement.preferredWidth = preferredWidth;
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;
        layoutElement.layoutPriority = 2;
        EditorUtility.SetDirty(layoutElement);
    }

    /// <summary>
    /// Assigns one layer to a GameObject and every child transform.
    /// </summary>
    /// <param name="targetObject">Root object receiving the layer.</param>
    /// <param name="layer">Unity layer index to assign.</param>
    private static void SetLayerRecursively(GameObject targetObject, int layer)
    {
        Transform[] transforms = targetObject.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < transforms.Length; index++)
            transforms[index].gameObject.layer = layer;
    }

    /// <summary>
    /// Ensures one component exists on the provided GameObject.
    /// </summary>
    /// <param name="targetObject">GameObject receiving the component when missing.</param>
    /// <typeparam name="T">Component type to resolve or add.</typeparam>
    /// <returns>The existing or newly added component.</returns>
    private static T EnsureComponent<T>(GameObject targetObject) where T : Component
    {
        T component = targetObject.GetComponent<T>();

        if (component != null)
            return component;

        component = targetObject.AddComponent<T>();
        EditorUtility.SetDirty(component);
        return component;
    }

    /// <summary>
    /// Finds the first component of a given type whose GameObject has the requested name.
    /// </summary>
    /// <param name="root">Hierarchy root used for the search.</param>
    /// <param name="targetName">GameObject name to match.</param>
    /// <typeparam name="T">Component type to resolve.</typeparam>
    /// <returns>The matching component, or null when no matching child exists.</returns>
    private static T FindComponentByName<T>(Transform root, string targetName) where T : Component
    {
        if (root == null)
            return null;

        T[] components = root.GetComponentsInChildren<T>(true);

        for (int index = 0; index < components.Length; index++)
        {
            if (components[index].name == targetName)
                return components[index];
        }

        return null;
    }

    /// <summary>
    /// Finds one component of the requested type in a loaded scene.
    /// </summary>
    /// <param name="scene">Loaded scene to inspect.</param>
    /// <typeparam name="T">Component type to resolve.</typeparam>
    /// <returns>The first matching component, or null when the scene does not contain it.</returns>
    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int index = 0; index < rootObjects.Length; index++)
        {
            T component = rootObjects[index].GetComponentInChildren<T>(true);

            if (component != null)
                return component;
        }

        return null;
    }

    /// <summary>
    /// Finds the first child transform with a specific GameObject name.
    /// </summary>
    /// <param name="root">Hierarchy root used for the search.</param>
    /// <param name="targetName">Child GameObject name to match.</param>
    /// <returns>The matching child transform, or null when none exists.</returns>
    private static Transform FindChild(Transform root, string targetName)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < children.Length; index++)
        {
            if (children[index].name == targetName)
                return children[index];
        }

        return null;
    }

    /// <summary>
    /// Writes one object reference into a serialized object when the target property exists.
    /// </summary>
    /// <param name="serializedObject">Serialized object containing the target property.</param>
    /// <param name="propertyName">Serialized object-reference property name.</param>
    /// <param name="value">Object reference value assigned to the property.</param>
    private static void SetObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        property.objectReferenceValue = value;
    }
    #endregion

    #endregion
}
