using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Validates authored active power-up and boss syringe UI assets after setup utilities run.
/// </summary>
public static class PlayerActiveHudBossSyringeUiSmokeTest
{
    #region Constants
    private const string PowerUpSlotPrefabPath = "Assets/Prefabs/UI/PF_UI_PowerUpsSlot.prefab";
    private const string BossHudPrefabPath = "Assets/Prefabs/UI/PF_BossHUD.prefab";
    private const string BossVisualPresetPath = "Assets/Scriptable Objects/Enemy/Visual/EnemyVisualPreset_BOSS.asset";
    private const string MainUiScenePath = "Assets/Scenes/Testing/Main Scenes/SCN_MainScene/SCN_MainScene_UI.unity";
    private const string ChargeRingMaterialPath = "Assets/2D/Materials/M_UI_PowerUpChargeSemiRing.mat";
    private const string CooldownIconMaterialPath = "Assets/2D/Materials/M_UI_PowerUpCooldownIcon.mat";
    private const string ChargeRingShaderName = "Custom/UI/PowerUpChargeSemiRing";
    private const string CooldownIconShaderName = "Custom/UI/PowerUpCooldownIcon";
    #endregion

    #region Methods

    #region Entry Point
    /// <summary>
    /// Runs authored asset validation for active power-up and boss syringe UI.
    /// </summary>
    public static void Run()
    {
        ValidateMaterials();
        ValidatePowerUpSlotPrefab();
        ValidateBossHudPrefab();
        ValidateMainUiSceneBindings();
        Debug.Log("[PlayerActiveHudBossSyringeUiSmokeTest] Passed active power-up and boss syringe UI asset validation.");
    }
    #endregion

    #region Materials
    /// <summary>
    /// Validates that new procedural UI material templates resolve to the expected shaders.
    /// </summary>
    private static void ValidateMaterials()
    {
        ValidateMaterialShader(ChargeRingMaterialPath, ChargeRingShaderName);
        ValidateMaterialShader(CooldownIconMaterialPath, CooldownIconShaderName);
    }

    /// <summary>
    /// Validates one material asset against the expected shader name.
    /// </summary>
    /// <param name="assetPath">Material asset path.</param>
    /// <param name="shaderName">Expected shader name.</param>
    private static void ValidateMaterialShader(string assetPath, string shaderName)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

        if (material == null)
            throw new InvalidOperationException("Missing UI material asset: " + assetPath);

        if (material.shader == null || material.shader.name != shaderName)
            throw new InvalidOperationException("Material uses an unexpected shader: " + assetPath);
    }
    #endregion

    #region Prefabs
    /// <summary>
    /// Validates the active power-up slot prefab references all redesigned authored views.
    /// </summary>
    private static void ValidatePowerUpSlotPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PowerUpSlotPrefabPath);

        if (prefab == null)
            throw new InvalidOperationException("Missing active power-up slot prefab.");

        PlayerActivePowerUpSlotHudView slotView = prefab.GetComponent<PlayerActivePowerUpSlotHudView>();

        if (slotView == null || !slotView.HasAnyVisuals)
            throw new InvalidOperationException("Power-up slot prefab is missing PlayerActivePowerUpSlotHudView visuals.");

        PlayerSyringeBarView energySyringe = FindComponentByName<PlayerSyringeBarView>(prefab.transform,
                                                                                       "ActiveEnergySyringe");
        PlayerPowerUpChargeRingView chargeRing = FindComponentByName<PlayerPowerUpChargeRingView>(prefab.transform,
                                                                                                  "ActiveChargeSemiRing");
        PlayerPowerUpIconCooldownView cooldownView = prefab.GetComponentInChildren<PlayerPowerUpIconCooldownView>(true);
        Image iconImage = FindComponentByName<Image>(prefab.transform, "IconImage");

        if (energySyringe == null || chargeRing == null || cooldownView == null || iconImage == null)
            throw new InvalidOperationException("Power-up slot prefab redesigned child views are incomplete.");

        ValidateSerializedReference(slotView, "iconImage", iconImage);
        ValidateSerializedReference(slotView, "energySyringe", energySyringe);
        ValidateSerializedReference(slotView, "chargeRing", chargeRing);
        ValidateSerializedReference(slotView, "iconCooldown", cooldownView);
        ValidatePreviewPreset(slotView);
        ValidateActiveSlotEditorPreview(prefab);
        ValidateNoMissingScripts(prefab.transform);
    }

    /// <summary>
    /// Validates the boss HUD prefab references health and shield syringe views.
    /// </summary>
    private static void ValidateBossHudPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossHudPrefabPath);

        if (prefab == null)
            throw new InvalidOperationException("Missing boss HUD prefab.");

        EnemyBossHudPresentation presentation = prefab.GetComponent<EnemyBossHudPresentation>();
        PlayerSyringeBarView healthSyringe = FindComponentByName<PlayerSyringeBarView>(prefab.transform,
                                                                                       "BossHealthSyringe");
        PlayerSyringeBarView shieldSyringe = FindComponentByName<PlayerSyringeBarView>(prefab.transform,
                                                                                       "BossShieldSyringe");
        RectTransform portraitRoot = FindComponentByName<RectTransform>(prefab.transform,
                                                                        "BossPortraitContainer");
        Image portraitImage = FindComponentByName<Image>(prefab.transform,
                                                         "BossPortraitImage");
        RectTransform contentRoot = FindComponentByName<RectTransform>(prefab.transform,
                                                                       "BossHudContentRoot");
        RectTransform panelRoot = FindComponentByName<RectTransform>(prefab.transform,
                                                                     "Panel");

        if (presentation == null ||
            healthSyringe == null ||
            shieldSyringe == null ||
            portraitRoot == null ||
            portraitImage == null ||
            contentRoot == null ||
            panelRoot == null)
        {
            throw new InvalidOperationException("Boss HUD prefab syringe or portrait presentation is incomplete.");
        }

        ValidateSerializedReference(presentation, "healthSyringeBar", healthSyringe);
        ValidateSerializedReference(presentation, "shieldSyringeBar", shieldSyringe);
        ValidateSerializedReference(presentation, "visibilityRoot", contentRoot.gameObject);
        ValidateSerializedReference(presentation, "panelRoot", panelRoot);
        ValidateSerializedReference(presentation, "portraitRoot", portraitRoot);
        ValidateSerializedReference(presentation, "portraitImage", portraitImage);
        PlayerSyringeBarSmokeTestLayoutUtility.ValidateBossHudLayout(contentRoot, panelRoot, portraitRoot);
        PlayerSyringeBarSmokeTestLayoutUtility.ValidateSyringeLabelCounterRotation(healthSyringe,
                                                                                   true,
                                                                                   "Boss Health Syringe");
        PlayerSyringeBarSmokeTestLayoutUtility.ValidateSyringeLabelCounterRotation(shieldSyringe,
                                                                                   true,
                                                                                   "Boss Shield Syringe");
        ValidateNoNegativeScale(prefab.transform);
        ValidateBossEditorPreview(prefab);
        ValidateNoMissingScripts(prefab.transform);
    }

    /// <summary>
    /// Validates that the boss HUD Edit Mode preview rebuilds through the selected Enemy Visual Preset.
    /// </summary>
    /// <param name="prefab">Boss HUD prefab asset.</param>
    private static void ValidateBossEditorPreview(GameObject prefab)
    {
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        EnemyBossHudPresentation presentation = instance.GetComponent<EnemyBossHudPresentation>();
        PlayerSyringeBarView healthSyringe = FindComponentByName<PlayerSyringeBarView>(instance.transform,
                                                                                       "BossHealthSyringe");
        PlayerSyringeBarView shieldSyringe = FindComponentByName<PlayerSyringeBarView>(instance.transform,
                                                                                       "BossShieldSyringe");
        PlayerSyringeBarGraphic healthGraphic = healthSyringe != null
            ? healthSyringe.GetComponentInChildren<PlayerSyringeBarGraphic>(true)
            : null;
        SerializedObject presentationObject = new SerializedObject(presentation);
        EnemyVisualPreset expectedVisualPreset = AssetDatabase.LoadAssetAtPath<EnemyVisualPreset>(BossVisualPresetPath);
        EnemyVisualPreset previewVisualPreset = presentationObject.FindProperty("editorPreviewVisualPreset").objectReferenceValue as EnemyVisualPreset;

        try
        {
            if (previewVisualPreset != expectedVisualPreset)
                throw new InvalidOperationException("Boss HUD prefab is missing the direct Enemy Visual Preset reference required by its Edit Mode preview.");

            PlayerHealthBarsVisualSettings syringeSettings = previewVisualPreset.BossUi.SyringeBars;
            PlayerHealthBarVisualConfig previewConfig = PlayerHealthBarVisualBakeUtility.BuildConfig(syringeSettings);
            float previewMaximum = Mathf.Max(0.0001f, presentationObject.FindProperty("editorPreviewHealthMaximum").floatValue);
            float expectedLength = PlayerSyringeBarPreviewLengthTestUtility.ResolveExpectedLength(previewConfig, previewMaximum);

            presentation.RefreshEditorPreview();

            if (healthSyringe == null ||
                !Mathf.Approximately(healthSyringe.Root.sizeDelta.y, previewConfig.BarHeight) ||
                !Mathf.Approximately(healthSyringe.Root.sizeDelta.x, expectedLength))
            {
                throw new InvalidOperationException("Boss HUD Edit Mode preview did not rebuild health syringe geometry through the selected Enemy Visual Preset.");
            }

            if (healthGraphic == null ||
                healthGraphic.material == null ||
                !healthGraphic.material.HasProperty("_LiquidColor") ||
                !IsColorApproximately(healthGraphic.material.GetColor("_LiquidColor"), previewConfig.Health.Palette.Liquid))
            {
                throw new InvalidOperationException("Boss HUD Edit Mode preview did not apply the boss syringe material palette.");
            }

            if (shieldSyringe != null && shieldSyringe.gameObject.activeSelf)
                throw new InvalidOperationException("Boss HUD Edit Mode preview did not hide the zero-maximum shield syringe.");
        }
        finally
        {
            if (healthSyringe != null)
                healthSyringe.Dispose();

            if (shieldSyringe != null)
                shieldSyringe.Dispose();

            UnityEngine.Object.DestroyImmediate(instance);
        }
    }
    #endregion

    #region Scene
    /// <summary>
    /// Validates scene bindings to the redesigned active power-up slot views.
    /// </summary>
    private static void ValidateMainUiSceneBindings()
    {
        Scene previousScene = EditorSceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.OpenScene(MainUiScenePath, OpenSceneMode.Single);

        try
        {
            HUDManager hudManager = FindComponentInScene<HUDManager>(scene);

            if (hudManager == null)
                throw new InvalidOperationException("SCN_MainScene_UI is missing HUDManager.");

            HUDPowerUpOverlaySectionComponent overlaySection = FindComponentInScene<HUDPowerUpOverlaySectionComponent>(scene);

            if (overlaySection == null)
                throw new InvalidOperationException("SCN_MainScene_UI is missing HUDPowerUpOverlaySectionComponent.");

            SerializedObject overlayObject = new SerializedObject(overlaySection);
            ValidateHudSlotReference(overlayObject, "primaryPowerUpSlotView");
            ValidateHudSlotReference(overlayObject, "secondaryPowerUpSlotView");
            ValidateSceneSlotReferences(overlayObject);
            ValidateBossHudSceneReferences(scene);
            ValidateNoNegativeScale(hudManager.transform.root);
            ValidateNoMissingScripts(hudManager.transform.root);
        }
        finally
        {
            if (previousScene.IsValid() && !string.IsNullOrEmpty(previousScene.path) && previousScene.path != MainUiScenePath)
                EditorSceneManager.OpenScene(previousScene.path, OpenSceneMode.Single);
        }
    }

    /// <summary>
    /// Validates one serialized active power-up overlay slot view reference.
    /// </summary>
    /// <param name="overlayObject">Serialized active power-up overlay section object.</param>
    /// <param name="propertyName">Slot view property name.</param>
    private static void ValidateHudSlotReference(SerializedObject overlayObject, string propertyName)
    {
        SerializedProperty property = overlayObject.FindProperty(propertyName);
        PlayerActivePowerUpSlotHudView slotView = property != null
            ? property.objectReferenceValue as PlayerActivePowerUpSlotHudView
            : null;

        if (slotView == null || !slotView.HasAnyVisuals)
            throw new InvalidOperationException("HUDPowerUpOverlaySectionComponent is missing redesigned slot view binding: " + propertyName);

        ValidatePreviewPreset(slotView);
    }
    #endregion

    #region Boss Scene References
    /// <summary>
    /// Validates that the scene boss presenter keeps authored portrait references.
    /// </summary>
    /// <param name="scene">Loaded UI scene inspected for boss HUD bindings.</param>
    private static void ValidateBossHudSceneReferences(Scene scene)
    {
        EnemyBossHudPresentation presentation = FindComponentInScene<EnemyBossHudPresentation>(scene);

        if (presentation == null)
            return;

        RectTransform portraitRoot = FindComponentByName<RectTransform>(presentation.transform, "BossPortraitContainer");
        Image portraitImage = FindComponentByName<Image>(presentation.transform, "BossPortraitImage");
        RectTransform contentRoot = FindComponentByName<RectTransform>(presentation.transform, "BossHudContentRoot");
        RectTransform panelRoot = FindComponentByName<RectTransform>(presentation.transform, "Panel");

        if (portraitRoot == null || portraitImage == null || contentRoot == null || panelRoot == null)
            throw new InvalidOperationException("Scene boss HUD is missing the authored portrait hierarchy.");

        ValidateSerializedReference(presentation, "visibilityRoot", contentRoot.gameObject);
        ValidateSerializedReference(presentation, "panelRoot", panelRoot);
        ValidateSerializedReference(presentation, "portraitRoot", portraitRoot);
        ValidateSerializedReference(presentation, "portraitImage", portraitImage);
        PlayerSyringeBarSmokeTestLayoutUtility.ValidateBossHudLayout(contentRoot, panelRoot, portraitRoot);
    }
    #endregion

    #region Scene Slot References
    /// <summary>
    /// Validates that scene active slots keep self-contained references without enforcing designer-authored placement.
    /// </summary>
    /// <param name="overlayObject">Serialized active power-up overlay section containing slot view references.</param>
    private static void ValidateSceneSlotReferences(SerializedObject overlayObject)
    {
        ValidateSlotReferencesBelongToSlot(ResolveSlotView(overlayObject, "primaryPowerUpSlotView"),
                                           "Primary Active Slot");
        ValidateSlotReferencesBelongToSlot(ResolveSlotView(overlayObject, "secondaryPowerUpSlotView"),
                                           "Secondary Active Slot");
    }

    /// <summary>
    /// Validates one scene slot view references children from its own hierarchy.
    /// </summary>
    /// <param name="slotView">Scene slot view inspected for child bindings.</param>
    /// <param name="slotLabel">User-facing slot label used by exception messages.</param>
    private static void ValidateSlotReferencesBelongToSlot(PlayerActivePowerUpSlotHudView slotView,
                                                           string slotLabel)
    {
        if (slotView == null)
            return;

        SerializedObject slotObject = new SerializedObject(slotView);
        ValidateChildReference(slotView, slotObject, "iconImage", slotLabel);
        ValidateChildReference(slotView, slotObject, "energySyringe", slotLabel);
        ValidateChildReference(slotView, slotObject, "chargeRing", slotLabel);
        ValidateChildReference(slotView, slotObject, "iconCooldown", slotLabel);
    }

    /// <summary>
    /// Validates one serialized scene-slot reference and its hierarchy ownership.
    /// </summary>
    /// <param name="slotView">Slot view owning the serialized reference.</param>
    /// <param name="slotObject">Serialized slot view object.</param>
    /// <param name="propertyName">Serialized object-reference property name.</param>
    /// <param name="slotLabel">User-facing slot label used by exception messages.</param>
    private static void ValidateChildReference(PlayerActivePowerUpSlotHudView slotView,
                                               SerializedObject slotObject,
                                               string propertyName,
                                               string slotLabel)
    {
        SerializedProperty property = slotObject.FindProperty(propertyName);
        Component component = property != null ? property.objectReferenceValue as Component : null;

        if (component == null)
            throw new InvalidOperationException(slotLabel + " is missing active HUD reference: " + propertyName);

        if (!component.transform.IsChildOf(slotView.transform) && component.transform != slotView.transform)
            throw new InvalidOperationException(slotLabel + " references a component outside its own hierarchy: " + propertyName);
    }
    #endregion

    #region Editor Preview
    /// <summary>
    /// Validates that the active slot Edit Mode preview rebuilds through the Player Visual Preset.
    /// </summary>
    /// <param name="prefab">Active power-up slot prefab asset.</param>
    private static void ValidateActiveSlotEditorPreview(GameObject prefab)
    {
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        PlayerActivePowerUpSlotHudView slotView = instance.GetComponent<PlayerActivePowerUpSlotHudView>();
        PlayerSyringeBarView energySyringe = FindComponentByName<PlayerSyringeBarView>(instance.transform,
                                                                                       "ActiveEnergySyringe");
        PlayerPowerUpChargeRingGraphic chargeRingGraphic = FindComponentByName<PlayerPowerUpChargeRingGraphic>(instance.transform,
                                                                                                               "ActiveChargeSemiRing");
        SerializedObject serializedObject = new SerializedObject(slotView);
        PlayerVisualPreset previewPreset = serializedObject.FindProperty("editorPreviewPreset").objectReferenceValue as PlayerVisualPreset;
        PlayerActivePowerUpHudVisualConfig previewConfig = PlayerActivePowerUpHudVisualBakeUtility.BuildConfig(previewPreset);

        try
        {
            ValidateChargeRingFillDirection(previewPreset, previewConfig);
            ValidateEnergySyringePlungerBehavior(previewPreset, previewConfig);
            slotView.RefreshEditorPreview();

            if (energySyringe == null ||
                !Mathf.Approximately(energySyringe.Root.sizeDelta.y, previewConfig.EnergySyringe.BarHeight) ||
                energySyringe.Root.sizeDelta.x < previewConfig.EnergySyringe.MinimumLength ||
                energySyringe.Root.sizeDelta.x > previewConfig.EnergySyringe.MaximumLength)
            {
                throw new InvalidOperationException("Active slot Edit Mode preview did not rebuild the energy syringe through the selected Player Visual Preset.");
            }

            if (chargeRingGraphic == null ||
                chargeRingGraphic.material == null ||
                !Mathf.Approximately(chargeRingGraphic.material.GetFloat("_FillDirection"), (float)previewConfig.ChargeRing.FillDirection))
            {
                throw new InvalidOperationException("Active slot Edit Mode preview did not apply charge semiring Fill Direction to the runtime material.");
            }
        }
        finally
        {
            if (slotView != null)
                slotView.Dispose();

            UnityEngine.Object.DestroyImmediate(instance);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Validates that charge semiring fill direction is exposed as a scalable enum and reaches the baked config.
    /// </summary>
    /// <param name="previewPreset">Player Visual Preset used by the active slot editor preview.</param>
    /// <param name="previewConfig">Baked active power-up HUD config resolved from the preview preset.</param>
    private static void ValidateChargeRingFillDirection(PlayerVisualPreset previewPreset,
                                                        PlayerActivePowerUpHudVisualConfig previewConfig)
    {
        SerializedObject presetObject = new SerializedObject(previewPreset);
        SerializedProperty fillDirection = presetObject.FindProperty("activePowerUpHud")
                                                       .FindPropertyRelative("chargeRing")
                                                       .FindPropertyRelative("fillDirection");

        if (fillDirection == null)
            throw new InvalidOperationException("Active Power-Up HUD Charge Ring Fill Direction is missing from the Player Visual Preset.");

        string statKey = PlayerScalingStatKeyUtility.BuildStatKey(fillDirection);

        if (statKey != "activePowerUpHud.chargeRing.fillDirection")
            throw new InvalidOperationException("Active Power-Up HUD Charge Ring Fill Direction has an invalid scaling path: " + statKey);

        if (previewConfig.ChargeRing.FillDirection != (PlayerPowerUpChargeRingFillDirection)fillDirection.enumValueIndex)
            throw new InvalidOperationException("Active Power-Up HUD Charge Ring Fill Direction did not bake into the runtime config.");
    }

    /// <summary>
    /// Validates that energy-syringe plunger behavior toggles are scalable and baked into runtime config.
    /// </summary>
    /// <param name="previewPreset">Player Visual Preset used by the active slot editor preview.</param>
    /// <param name="previewConfig">Baked active power-up HUD config resolved from the preview preset.</param>
    private static void ValidateEnergySyringePlungerBehavior(PlayerVisualPreset previewPreset,
                                                             PlayerActivePowerUpHudVisualConfig previewConfig)
    {
        SerializedObject presetObject = new SerializedObject(previewPreset);
        SerializedProperty activePowerUpHud = presetObject.FindProperty("activePowerUpHud");

        if (activePowerUpHud == null)
            throw new InvalidOperationException("Active Power-Up HUD settings are missing from the Player Visual Preset.");

        SerializedProperty energySyringe = activePowerUpHud.FindPropertyRelative("energySyringe");

        if (energySyringe == null)
            throw new InvalidOperationException("Active Power-Up HUD Energy Syringe settings are missing from the Player Visual Preset.");

        SerializedProperty clampPlungerStartInsideBody = energySyringe.FindPropertyRelative("clampPlungerStartInsideBody");
        SerializedProperty clampPlungerEndInsideBody = energySyringe.FindPropertyRelative("clampPlungerEndInsideBody");
        SerializedProperty stopLiquidAtPlunger = energySyringe.FindPropertyRelative("stopLiquidAtPlunger");
        SerializedProperty terminationEnabled = energySyringe.FindPropertyRelative("terminationEnabled");
        SerializedProperty energyChannel = energySyringe.FindPropertyRelative("health");
        SerializedProperty outlineStyle = energyChannel != null
            ? energyChannel.FindPropertyRelative("outlineStyle")
            : null;
        SerializedProperty outlineEnabled = outlineStyle != null
            ? outlineStyle.FindPropertyRelative("enabled")
            : null;
        SerializedProperty edgeWobbleStrength = outlineStyle != null
            ? outlineStyle.FindPropertyRelative("edgeWobbleStrength")
            : null;

        ValidateEnergySyringeBooleanPath(clampPlungerStartInsideBody,
                                         "activePowerUpHud.energySyringe.clampPlungerStartInsideBody",
                                         previewConfig.EnergySyringe.ClampPlungerStartInsideBody,
                                         "Clamp Plunger At Start");
        ValidateEnergySyringeBooleanPath(clampPlungerEndInsideBody,
                                         "activePowerUpHud.energySyringe.clampPlungerEndInsideBody",
                                         previewConfig.EnergySyringe.ClampPlungerEndInsideBody,
                                         "Clamp Plunger At End");
        ValidateEnergySyringeBooleanPath(stopLiquidAtPlunger,
                                         "activePowerUpHud.energySyringe.stopLiquidAtPlunger",
                                         previewConfig.EnergySyringe.StopLiquidAtPlunger,
                                         "Stop Liquid At Plunger");
        ValidateEnergySyringeBooleanPath(terminationEnabled,
                                         "activePowerUpHud.energySyringe.terminationEnabled",
                                         previewConfig.EnergySyringe.TerminationEnabled,
                                         "Enable Termination");
        ValidateEnergySyringeBooleanPath(outlineEnabled,
                                         "activePowerUpHud.energySyringe.health.outlineStyle.enabled",
                                         previewConfig.EnergySyringe.Health.OutlineStyle.Enabled,
                                         "Painted Outline Enabled");
        ValidateEnergySyringeNumericPath(edgeWobbleStrength,
                                         "activePowerUpHud.energySyringe.health.outlineStyle.edgeWobbleStrength",
                                         previewConfig.EnergySyringe.Health.OutlineStyle.EdgeWobbleStrength,
                                         "Edge Wobble Strength");
    }

    /// <summary>
    /// Validates one active energy-syringe boolean path against Add Scaling and baked config output.
    /// </summary>
    /// <param name="property">Serialized boolean field inspected in the Player Visual Preset.</param>
    /// <param name="expectedStatKey">Expected unified scaling stat key.</param>
    /// <param name="bakedValue">Baked runtime byte value.</param>
    /// <param name="label">User-facing setting name used by diagnostics.</param>
    private static void ValidateEnergySyringeBooleanPath(SerializedProperty property,
                                                         string expectedStatKey,
                                                         byte bakedValue,
                                                         string label)
    {
        if (property == null)
            throw new InvalidOperationException("Active Power-Up HUD Energy Syringe " + label + " is missing from the Player Visual Preset.");

        if (!PlayerScalingFormulaEditorUtility.SupportsScalingTarget(property))
            throw new InvalidOperationException("Active Power-Up HUD Energy Syringe " + label + " is not exposed as an Add Scaling target.");

        string statKey = PlayerScalingStatKeyUtility.BuildStatKey(property);

        if (statKey != expectedStatKey)
            throw new InvalidOperationException("Active Power-Up HUD Energy Syringe " + label + " has an invalid scaling path: " + statKey);

        if ((property.boolValue && bakedValue == 0) || (!property.boolValue && bakedValue != 0))
            throw new InvalidOperationException("Active Power-Up HUD Energy Syringe " + label + " did not bake into the runtime config.");
    }

    /// <summary>
    /// Validates one active energy-syringe numeric path against Add Scaling and baked config output.
    /// </summary>
    /// <param name="property">Serialized numeric field inspected in the Player Visual Preset.</param>
    /// <param name="expectedStatKey">Expected unified scaling stat key.</param>
    /// <param name="bakedValue">Baked runtime numeric value.</param>
    /// <param name="label">User-facing setting name used by diagnostics.</param>
    private static void ValidateEnergySyringeNumericPath(SerializedProperty property,
                                                         string expectedStatKey,
                                                         float bakedValue,
                                                         string label)
    {
        if (property == null)
            throw new InvalidOperationException("Active Power-Up HUD Energy Syringe " + label + " is missing from the Player Visual Preset.");

        if (!PlayerScalingFormulaEditorUtility.SupportsScalingTarget(property))
            throw new InvalidOperationException("Active Power-Up HUD Energy Syringe " + label + " is not exposed as an Add Scaling target.");

        string statKey = PlayerScalingStatKeyUtility.BuildStatKey(property);

        if (statKey != expectedStatKey)
            throw new InvalidOperationException("Active Power-Up HUD Energy Syringe " + label + " has an invalid scaling path: " + statKey);

        if (!Mathf.Approximately(property.floatValue, bakedValue))
            throw new InvalidOperationException("Active Power-Up HUD Energy Syringe " + label + " did not bake into the runtime config.");
    }

    /// <summary>
    /// Compares a Unity color with an unmanaged color using a small preview-material tolerance.
    /// </summary>
    /// <param name="actual">Color read from the preview material.</param>
    /// <param name="expected">Expected unmanaged color from the baked config.</param>
    /// <returns>True when the two colors are visually equivalent for preview validation.</returns>
    private static bool IsColorApproximately(Color actual, float4 expected)
    {
        return Mathf.Abs(actual.r - expected.x) <= 0.001f &&
               Mathf.Abs(actual.g - expected.y) <= 0.001f &&
               Mathf.Abs(actual.b - expected.z) <= 0.001f &&
               Mathf.Abs(actual.a - expected.w) <= 0.001f;
    }

    /// <summary>
    /// Resolves one active slot view from a serialized active power-up overlay section.
    /// </summary>
    /// <param name="overlayObject">Serialized active power-up overlay section object.</param>
    /// <param name="propertyName">Serialized slot-view property name.</param>
    /// <returns>Resolved slot view, or null when missing.</returns>
    private static PlayerActivePowerUpSlotHudView ResolveSlotView(SerializedObject overlayObject, string propertyName)
    {
        SerializedProperty property = overlayObject.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue as PlayerActivePowerUpSlotHudView : null;
    }

    /// <summary>
    /// Validates that one active slot view has an Edit Mode preview preset assigned.
    /// </summary>
    /// <param name="slotView">Slot view whose serialized preview reference is inspected.</param>
    private static void ValidatePreviewPreset(PlayerActivePowerUpSlotHudView slotView)
    {
        SerializedObject serializedObject = new SerializedObject(slotView);
        SerializedProperty property = serializedObject.FindProperty("editorPreviewPreset");

        if (property == null || property.objectReferenceValue == null)
            throw new InvalidOperationException("Active power-up slot is missing its Edit Mode preview preset: " + slotView.name);
    }

    /// <summary>
    /// Validates that one private serialized reference points to the expected object.
    /// </summary>
    /// <param name="owner">Serialized owner component.</param>
    /// <param name="propertyName">Serialized object-reference property name.</param>
    /// <param name="expectedValue">Expected object reference.</param>
    private static void ValidateSerializedReference(UnityEngine.Object owner,
                                                    string propertyName,
                                                    UnityEngine.Object expectedValue)
    {
        SerializedObject serializedObject = new SerializedObject(owner);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null || property.objectReferenceValue != expectedValue)
            throw new InvalidOperationException(owner.name + " has an invalid reference: " + propertyName);
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
    /// Validates that a hierarchy does not contain missing script components.
    /// </summary>
    /// <param name="root">Hierarchy root inspected recursively.</param>
    private static void ValidateNoMissingScripts(Transform root)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
        {
            Component[] components = transforms[transformIndex].GetComponents<Component>();

            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                if (components[componentIndex] == null)
                    throw new InvalidOperationException("Missing script under UI hierarchy: " + transforms[transformIndex].name);
            }
        }
    }

    /// <summary>
    /// Validates that mirrored UI objects use rotation rather than negative scale.
    /// </summary>
    /// <param name="root">Hierarchy root inspected recursively.</param>
    private static void ValidateNoNegativeScale(Transform root)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < transforms.Length; index++)
        {
            if (transforms[index].localScale.x < 0f ||
                transforms[index].localScale.y < 0f ||
                transforms[index].localScale.z < 0f)
            {
                throw new InvalidOperationException("Negative UI scale found under hierarchy: " + transforms[index].name);
            }
        }
    }
    #endregion

    #endregion
}
