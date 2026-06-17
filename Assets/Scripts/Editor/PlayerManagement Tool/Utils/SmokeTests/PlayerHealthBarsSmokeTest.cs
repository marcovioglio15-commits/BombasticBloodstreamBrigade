using System;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Validates the preauthored player health/shield syringe hierarchy, technical assets, and target-scene binding.
/// </summary>
public static class PlayerHealthBarsSmokeTest
{
    #region Constants
    private const string PrefabPath = "Assets/Prefabs/UI/PlayerBars VerticalBox.prefab";
    private const string ScenePath = "Assets/Scenes/Testing/Main Scenes/SCN_PlayerControllerTesting/SCN_PlayerControllerTesting_UI.unity";
    private const string MaterialPath = "Assets/2D/Materials/M_UI_PlayerSyringeBar.mat";
    private const string ShieldMaterialPath = "Assets/2D/Materials/M_UI_PlayerShieldSyringeBar.mat";
    private const string FontPath = "Assets/2D/Fonts/NoctraDrip-Solid SDF.asset";
    #endregion

    #region Methods

    #region Public Methods
    // [MenuItem("Tools/Player/Tests/Run Health Bars Smoke Test")]
    /// <summary>
    /// Runs editor validation for the complete preauthored player health-bars presentation surface.
    /// </summary>
    public static void Run()
    {
        ValidateTechnicalAssets();
        ValidateScalingEndToEnd();
        ValidatePrefab();
        ValidateEditorPreview();
        ValidateLabelDistribution();
        ValidateGraduationAlignmentAndMotionReset();
        ValidateShortSyringeDecorationScale();
        ValidateShieldVisibilityPolicy();
        ValidateScene();
        Debug.Log("[PlayerHealthBarsSmokeTest] Passed prefab, scene, shader, material, direct-font, value-track, and preauthored-label validation.");
    }
    #endregion

    #region Validation
    /// <summary>
    /// Validates the dedicated shader, materials, and directly selectable font asset.
    /// </summary>
    private static void ValidateTechnicalAssets()
    {
        Shader shader = Shader.Find("Custom/UI/PlayerSyringeBar");

        if (shader == null)
            throw new InvalidOperationException("Custom/UI/PlayerSyringeBar shader is missing.");

        if (ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("Custom/UI/PlayerSyringeBar contains shader compiler errors.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Material shieldMaterial = AssetDatabase.LoadAssetAtPath<Material>(ShieldMaterialPath);

        if (material == null || material.shader != shader)
            throw new InvalidOperationException("Shared player syringe material is missing or uses the wrong shader.");

        if (shieldMaterial == null || shieldMaterial.shader != shader)
            throw new InvalidOperationException("Shared player shield-syringe preview material is missing or uses the wrong shader.");

        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) == null)
            throw new InvalidOperationException("Player Health Bar direct font test asset is missing.");
    }

    /// <summary>
    /// Validates Add Scaling property paths, metadata bake, base reset, and runtime application for color, bool, enum, and numeric fields.
    /// </summary>
    private static void ValidateScalingEndToEnd()
    {
        PlayerVisualPreset preset = ScriptableObject.CreateInstance<PlayerVisualPreset>();
        World world = new World("PlayerHealthBarsSmokeTestWorld");

        try
        {
            SerializedObject presetObject = new SerializedObject(preset);
            SerializedProperty healthBars = presetObject.FindProperty("healthBars");
            SerializedProperty health = healthBars.FindPropertyRelative("health");
            SerializedProperty motion = health.FindPropertyRelative("motion");
            SerializedProperty palette = health.FindPropertyRelative("palette");
            SerializedProperty colorChannel = palette.FindPropertyRelative("liquid").FindPropertyRelative("r");
            SerializedProperty healthEnabled = health.FindPropertyRelative("enabled");
            SerializedProperty terminationStyle = healthBars.FindPropertyRelative("terminationStyle");
            SerializedProperty bodyStyle = healthBars.FindPropertyRelative("bodyStyle");
            SerializedProperty labelPlacement = healthBars.FindPropertyRelative("labelPlacement");
            SerializedProperty labelMinimumSpacing = healthBars.FindPropertyRelative("labelMinimumSpacing");
            SerializedProperty graduationEndPadding = healthBars.FindPropertyRelative("graduationEndPadding");
            SerializedProperty terminationOffset = healthBars.FindPropertyRelative("terminationOffset");
            SerializedProperty labelOutlineWidth = healthBars.FindPropertyRelative("labelOutlineWidth");
            SerializedProperty paintDripsEnabled = healthBars.FindPropertyRelative("paintDrips").FindPropertyRelative("enabled");
            SerializedProperty paintDripLength = healthBars.FindPropertyRelative("paintDrips").FindPropertyRelative("length");
            SerializedProperty labelColorChannel = palette.FindPropertyRelative("label").FindPropertyRelative("r");
            SerializedProperty labelOutlineColorChannel = palette.FindPropertyRelative("labelOutline").FindPropertyRelative("a");
            SerializedProperty terminationInteriorColorChannel = palette.FindPropertyRelative("terminationInterior").FindPropertyRelative("r");
            SerializedProperty terminationOutlineColorChannel = palette.FindPropertyRelative("terminationOutline").FindPropertyRelative("r");
            SerializedProperty plungerWindowColorChannel = palette.FindPropertyRelative("plungerWindow").FindPropertyRelative("a");
            SerializedProperty horizontalSloshEnabled = motion.FindPropertyRelative("horizontalSloshEnabled");
            SerializedProperty surfaceSloshStrength = motion.FindPropertyRelative("surfaceSloshStrength");
            SerializedProperty horizontalSloshStrength = motion.FindPropertyRelative("horizontalSloshStrength");
            SerializedProperty fontAsset = healthBars.FindPropertyRelative("fontAsset");
            TMP_FontAsset expectedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            fontAsset.objectReferenceValue = expectedFont;

            if (colorChannel == null ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(colorChannel) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(healthEnabled) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(terminationStyle) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(bodyStyle) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(labelPlacement) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(labelMinimumSpacing) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(graduationEndPadding) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(terminationOffset) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(labelOutlineWidth) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(paintDripsEnabled) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(paintDripLength) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(labelColorChannel) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(labelOutlineColorChannel) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(terminationInteriorColorChannel) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(terminationOutlineColorChannel) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(plungerWindowColorChannel) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(horizontalSloshEnabled) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(surfaceSloshStrength) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(horizontalSloshStrength))
            {
                throw new InvalidOperationException("Health Bars Add Scaling target support is incomplete.");
            }

            SerializedProperty scalingRules = presetObject.FindProperty("scalingRules");
            scalingRules.arraySize = 19;
            ConfigureRule(scalingRules.GetArrayElementAtIndex(0), PlayerScalingStatKeyUtility.BuildStatKey(colorChannel), "[this] * 0.5");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(1), PlayerScalingStatKeyUtility.BuildStatKey(healthEnabled), "![this]");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(2), PlayerScalingStatKeyUtility.BuildStatKey(terminationStyle), "[this] + ([Needle] - [this])");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(3), PlayerScalingStatKeyUtility.BuildStatKey(horizontalSloshEnabled), "![this]");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(4), PlayerScalingStatKeyUtility.BuildStatKey(bodyStyle), "[this] + ([DetailedSyringe] - [this])");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(5), PlayerScalingStatKeyUtility.BuildStatKey(labelPlacement), "[this] + ([GraduationPlate] - [this])");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(6), PlayerScalingStatKeyUtility.BuildStatKey(labelMinimumSpacing), "[this] + 10");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(7), PlayerScalingStatKeyUtility.BuildStatKey(paintDripsEnabled), "![this]");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(8), PlayerScalingStatKeyUtility.BuildStatKey(labelColorChannel), "[this] + 0.1");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(9), PlayerScalingStatKeyUtility.BuildStatKey(labelOutlineWidth), "[this] + 0.05");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(10), PlayerScalingStatKeyUtility.BuildStatKey(paintDripLength), "[this] + 0.02");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(11), PlayerScalingStatKeyUtility.BuildStatKey(labelOutlineColorChannel), "[this] - 0.1");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(12), PlayerScalingStatKeyUtility.BuildStatKey(graduationEndPadding), "[this] + 4");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(13), PlayerScalingStatKeyUtility.BuildStatKey(surfaceSloshStrength), "[this] + 0.1");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(14), PlayerScalingStatKeyUtility.BuildStatKey(horizontalSloshStrength), "[this] + 0.05");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(15), PlayerScalingStatKeyUtility.BuildStatKey(terminationInteriorColorChannel), "[this] + 0.08");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(16), PlayerScalingStatKeyUtility.BuildStatKey(terminationOffset), "[this] + 6");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(17), PlayerScalingStatKeyUtility.BuildStatKey(terminationOutlineColorChannel), "[this] + 0.12");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(18), PlayerScalingStatKeyUtility.BuildStatKey(plungerWindowColorChannel), "[this] - 0.08");
            presetObject.ApplyModifiedPropertiesWithoutUndo();

            EntityManager entityManager = world.EntityManager;
            Entity playerEntity = entityManager.CreateEntity();
            Entity configEntity = entityManager.CreateEntity();
            PlayerHealthBarVisualConfig baseConfig = PlayerHealthBarVisualBakeUtility.BuildConfig(preset);

            if (baseConfig.Shield.HideWhenMaximumUnavailable == 0)
                throw new InvalidOperationException("Default shield syringe must stay hidden while its authoritative maximum is unavailable.");

            if (Mathf.Approximately(baseConfig.Health.Palette.Liquid.x, baseConfig.Shield.Palette.Liquid.x) &&
                Mathf.Approximately(baseConfig.Health.Palette.Liquid.z, baseConfig.Shield.Palette.Liquid.z))
            {
                throw new InvalidOperationException("Default health and shield syringe palettes must remain visually distinct.");
            }

            entityManager.AddComponentData(playerEntity, new PlayerRuntimeScalingState
            {
                Initialized = 1,
                LastScalableStatsHash = 123u
            });
            entityManager.AddComponentData(playerEntity, new PlayerHealthBarVisualReference
            {
                ConfigEntity = configEntity
            });
            entityManager.AddComponentData(configEntity, new PlayerHealthBarVisualOwner
            {
                PlayerEntity = playerEntity
            });
            entityManager.AddComponentData(configEntity, new PlayerHealthBarVisualScalingState());
            entityManager.AddComponentData(configEntity, new PlayerBaseHealthBarVisualConfig
            {
                Config = baseConfig
            });
            entityManager.AddComponentData(configEntity, baseConfig);
            entityManager.AddComponentData(playerEntity, new PlayerRuntimeComboCounterConfig());
            entityManager.AddComponentData(playerEntity, new PlayerComboCounterState());
            entityManager.AddBuffer<PlayerScalableStatElement>(playerEntity);
            entityManager.AddBuffer<PlayerRuntimeComboRankElement>(playerEntity);
            entityManager.AddBuffer<PlayerPowerUpCharacterTuningFormulaElement>(playerEntity);
            DynamicBuffer<PlayerRuntimeHealthBarVisualScalingElement> metadata = entityManager.AddBuffer<PlayerRuntimeHealthBarVisualScalingElement>(configEntity);
            PlayerRuntimeScalingVisualBakeUtility.PopulateHealthBarVisualScalingMetadata(preset, metadata);

            if (metadata.Length != 19)
                throw new InvalidOperationException("Health Bars runtime scaling metadata did not include palette, bool, enum, numeric, and nested-block rules.");

            StringBuilder metadataDetails = new StringBuilder();

            for (int index = 0; index < metadata.Length; index++)
            {
                PlayerRuntimeHealthBarVisualScalingElement element = metadata[index];
                metadataDetails.Append(element.PayloadPath);
                metadataDetails.Append(':');
                metadataDetails.Append(element.ValueType);
                metadataDetails.Append(':');
                metadataDetails.Append(element.Formula);
                metadataDetails.Append(';');
            }

            SystemHandle system = world.GetOrCreateSystem<PlayerRuntimeHealthBarVisualScalingSystem>();
            system.Update(world.Unmanaged);
            PlayerHealthBarVisualConfig runtimeConfig = entityManager.GetComponentData<PlayerHealthBarVisualConfig>(configEntity);
            PlayerHealthBarVisualScalingState scalingState = entityManager.GetComponentData<PlayerHealthBarVisualScalingState>(configEntity);

            if (entityManager.HasComponent<PlayerHealthBarVisualConfig>(playerEntity) ||
                entityManager.HasComponent<PlayerBaseHealthBarVisualConfig>(playerEntity) ||
                entityManager.HasBuffer<PlayerRuntimeHealthBarVisualScalingElement>(playerEntity))
            {
                throw new InvalidOperationException("Large Health Bars visual payload is still stored on the player archetype.");
            }

            if (!Mathf.Approximately(runtimeConfig.Health.Palette.Liquid.x, baseConfig.Health.Palette.Liquid.x * 0.5f) ||
                runtimeConfig.Health.Enabled != 0 ||
                runtimeConfig.TerminationStyle != PlayerSyringeTerminationStyle.Needle ||
                runtimeConfig.BodyStyle != PlayerSyringeBodyStyle.DetailedSyringe ||
                runtimeConfig.LabelPlacement != PlayerSyringeLabelPlacement.GraduationPlate ||
                !Mathf.Approximately(runtimeConfig.LabelMinimumSpacing, baseConfig.LabelMinimumSpacing + 10f) ||
                !Mathf.Approximately(runtimeConfig.GraduationEndPadding, baseConfig.GraduationEndPadding + 4f) ||
                !Mathf.Approximately(runtimeConfig.LabelOutlineWidth, baseConfig.LabelOutlineWidth + 0.05f) ||
                runtimeConfig.PaintDrips.Enabled == 0 ||
                !Mathf.Approximately(runtimeConfig.PaintDrips.Length, baseConfig.PaintDrips.Length + 0.02f) ||
                !Mathf.Approximately(runtimeConfig.Health.Palette.Label.x, baseConfig.Health.Palette.Label.x + 0.1f) ||
                !Mathf.Approximately(runtimeConfig.Health.Palette.LabelOutline.w, baseConfig.Health.Palette.LabelOutline.w - 0.1f) ||
                !Mathf.Approximately(runtimeConfig.Health.Palette.TerminationInterior.x, baseConfig.Health.Palette.TerminationInterior.x + 0.08f) ||
                !Mathf.Approximately(runtimeConfig.TerminationOffset, baseConfig.TerminationOffset + 6f) ||
                !Mathf.Approximately(runtimeConfig.Health.Palette.TerminationOutline.x, baseConfig.Health.Palette.TerminationOutline.x + 0.12f) ||
                !Mathf.Approximately(runtimeConfig.Health.Palette.PlungerWindow.w, baseConfig.Health.Palette.PlungerWindow.w - 0.08f) ||
                runtimeConfig.Health.Motion.HorizontalSloshEnabled != 0 ||
                !Mathf.Approximately(runtimeConfig.Health.Motion.SurfaceSloshStrength, baseConfig.Health.Motion.SurfaceSloshStrength + 0.1f) ||
                !Mathf.Approximately(runtimeConfig.Health.Motion.HorizontalSloshStrength, baseConfig.Health.Motion.HorizontalSloshStrength + 0.05f) ||
                runtimeConfig.FontAsset.Value != expectedFont ||
                scalingState.Initialized == 0 ||
                scalingState.LastScalableStatsHash != 123u)
            {
                throw new InvalidOperationException(string.Format("Health Bars runtime scaling rebuild mismatch. Color={0}/{1}, Enabled={2}, Termination={3}, Body={4}, Placement={5}, Spacing={6}, Padding={7}, OutlineWidth={8}, Drips={9}/{10}, Label={11}/{12}, Horizontal={13}/{14}, Font='{15}', State={16}/{17}, Metadata={18}.",
                                                                  runtimeConfig.Health.Palette.Liquid.x,
                                                                  baseConfig.Health.Palette.Liquid.x * 0.5f,
                                                                  runtimeConfig.Health.Enabled,
                                                                  runtimeConfig.TerminationStyle,
                                                                  runtimeConfig.BodyStyle,
                                                                  runtimeConfig.LabelPlacement,
                                                                  runtimeConfig.LabelMinimumSpacing,
                                                                  runtimeConfig.GraduationEndPadding,
                                                                  runtimeConfig.LabelOutlineWidth,
                                                                  runtimeConfig.PaintDrips.Enabled,
                                                                  runtimeConfig.PaintDrips.Length,
                                                                  runtimeConfig.Health.Palette.Label.x,
                                                                  runtimeConfig.Health.Palette.LabelOutline.w,
                                                                  runtimeConfig.Health.Motion.SurfaceSloshStrength,
                                                                  runtimeConfig.Health.Motion.HorizontalSloshStrength,
                                                                  runtimeConfig.FontAsset.Value,
                                                                  scalingState.Initialized,
                                                                  scalingState.LastScalableStatsHash,
                                                                  metadataDetails));
            }
        }
        finally
        {
            world.Dispose();
            UnityEngine.Object.DestroyImmediate(preset);
        }
    }

    /// <summary>
    /// Validates that constrained label pools distribute values across the complete syringe range.
    /// </summary>
    private static void ValidateLabelDistribution()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = UnityEngine.Object.Instantiate(prefab);

        try
        {
            PlayerSyringeBarLabelPool labelPool = instance.GetComponentInChildren<PlayerSyringeBarLabelPool>(true);

            if (labelPool == null)
                throw new InvalidOperationException("Preauthored syringe label pool is missing.");

            labelPool.Rebuild(5f,
                              1f,
                              1,
                              PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity,
                              260f,
                              46f,
                              PlayerSyringeLabelPlacement.InsideChamber,
                              15f,
                              new Unity.Mathematics.float2(0f, 0f),
                              new Unity.Mathematics.float4(0f, 0f, 0f, 1f),
                              new Unity.Mathematics.float4(1f, 1f, 1f, 1f),
                              0.1f,
                              null);
            TMP_Text[] labels = labelPool.GetComponentsInChildren<TMP_Text>(true);
            bool foundMaximum = false;
            bool foundOne = false;
            bool foundTwo = false;
            bool foundThree = false;
            bool foundFour = false;
            bool foundZero = false;
            bool anchorsAligned = true;
            int activeCount = 0;

            for (int index = 0; index < labels.Length; index++)
            {
                if (!labels[index].gameObject.activeSelf)
                    continue;

                activeCount++;
                foundMaximum |= string.Equals(labels[index].text, "5", StringComparison.Ordinal);
                foundOne |= string.Equals(labels[index].text, "1", StringComparison.Ordinal);
                foundTwo |= string.Equals(labels[index].text, "2", StringComparison.Ordinal);
                foundThree |= string.Equals(labels[index].text, "3", StringComparison.Ordinal);
                foundFour |= string.Equals(labels[index].text, "4", StringComparison.Ordinal);
                foundZero |= string.Equals(labels[index].text, "0", StringComparison.Ordinal);

                if (!int.TryParse(labels[index].text, out int representedValue) ||
                    !Mathf.Approximately(labels[index].rectTransform.anchorMin.x, representedValue / 5f))
                {
                    anchorsAligned = false;
                }
            }

            if (activeCount != 5 ||
                !foundMaximum ||
                !foundOne ||
                !foundTwo ||
                !foundThree ||
                !foundFour ||
                foundZero ||
                !anchorsAligned)
            {
                throw new InvalidOperationException("Fixed-unit label pool did not preserve every aligned 1-5 graduation label or incorrectly displayed zero.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    /// <summary>
    /// Validates exact simplified-style label-to-tick alignment and reactive-motion reset behavior.
    /// </summary>
    private static void ValidateGraduationAlignmentAndMotionReset()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        Transform healthRoot = instance.transform.Find("PlayerHealthSyringe");
        PlayerSyringeBarView view = healthRoot != null
            ? healthRoot.GetComponent<PlayerSyringeBarView>()
            : null;

        try
        {
            if (view == null)
                throw new InvalidOperationException("Health syringe view is missing during alignment validation.");

            PlayerHealthBarVisualConfig config = PlayerHealthBarVisualBakeUtility.BuildConfig(null);
            view.ApplyConfiguration(in config, in config.Health, null);
            view.UpdateValue(4f, 5f, 0f, true);
            Transform motionRoot = healthRoot.Find("MotionRoot");
            RectTransform labelsRoot = healthRoot.Find("MotionRoot/GraduationLabels") as RectTransform;
            PlayerSyringeBarGraphic graphic = healthRoot.GetComponentInChildren<PlayerSyringeBarGraphic>(true);

            if (motionRoot == null || labelsRoot == null || graphic == null || graphic.material == null)
                throw new InvalidOperationException("Health syringe alignment-validation hierarchy is incomplete.");

            float expectedNormalizedInset = labelsRoot.offsetMin.x / view.Root.rect.width;
            float expectedNormalizedEnd = 1f + labelsRoot.offsetMax.x / view.Root.rect.width;
            float expectedGraduationInset = config.EndCapWidth * 0.5f + config.GraduationEndPadding;
            float expectedTerminationInset = config.EndCapWidth + config.TerminationOffset;
            float shaderNormalizedInset = graphic.material.GetFloat("_GraduationInsetNormalized");
            float shaderNormalizedEnd = graphic.material.GetFloat("_GraduationEndNormalized");
            float shaderTerminationOffset = graphic.material.GetFloat("_TerminationOffsetNormalized");
            float shaderFill = graphic.material.GetFloat("_FillNormalized");

            if (!Mathf.Approximately(expectedNormalizedInset, shaderNormalizedInset) ||
                !Mathf.Approximately(expectedNormalizedEnd, shaderNormalizedEnd) ||
                !Mathf.Approximately(labelsRoot.offsetMin.x, expectedGraduationInset) ||
                !Mathf.Approximately(-labelsRoot.offsetMax.x, expectedTerminationInset) ||
                !Mathf.Approximately(shaderTerminationOffset, config.TerminationOffset / view.Root.rect.width) ||
                !Mathf.Approximately(shaderFill, 0.8f))
            {
                throw new InvalidOperationException(string.Format("Simplified syringe labels, graduation ticks, value track, and termination mismatch: start={0}/{1}, end={2}/{3}, start inset={4}/{5}, termination inset={6}/{7}, fill={8}.",
                                                                  expectedNormalizedInset,
                                                                  shaderNormalizedInset,
                                                                  expectedNormalizedEnd,
                                                                  shaderNormalizedEnd,
                                                                  labelsRoot.offsetMin.x,
                                                                  expectedGraduationInset,
                                                                  -labelsRoot.offsetMax.x,
                                                                  expectedTerminationInset,
                                                                  shaderFill));
            }

            motionRoot.localRotation = Quaternion.Euler(0f, 0f, 137f);
            view.ResetReactiveMotion();

            if (Quaternion.Angle(motionRoot.localRotation, Quaternion.identity) > 0.001f)
                throw new InvalidOperationException("Reactive-motion reset did not restore the syringe rotation after a focus discontinuity.");
        }
        finally
        {
            if (view != null)
                view.Dispose();

            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    /// <summary>
    /// Validates the rebuilt preauthored syringe prefab hierarchy and fixed-capacity label pools.
    /// </summary>
    private static void ValidatePrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        if (prefab == null)
            throw new InvalidOperationException("Player bars prefab is missing.");

        if (prefab.transform.Find("PlayerHealthBar") != null || prefab.transform.Find("PlayerShieldBar") != null)
            throw new InvalidOperationException("Legacy health or shield prefab roots are still present.");

        if (prefab.transform.Find("PlayerHealthSyringe") == null || prefab.transform.Find("PlayerShieldSyringe") == null)
            throw new InvalidOperationException("Preauthored health or shield syringe root is missing.");

        PlayerHealthBarsHudView hudView = prefab.GetComponent<PlayerHealthBarsHudView>();
        VerticalLayoutGroup layoutGroup = prefab.GetComponent<VerticalLayoutGroup>();
        PlayerSyringeBarView[] syringeViews = prefab.GetComponentsInChildren<PlayerSyringeBarView>(true);
        PlayerSyringeBarGraphic[] graphics = prefab.GetComponentsInChildren<PlayerSyringeBarGraphic>(true);
        PlayerSyringeBarLabelPool[] labelPools = prefab.GetComponentsInChildren<PlayerSyringeBarLabelPool>(true);
        TMP_Text[] labels = prefab.GetComponentsInChildren<TMP_Text>(true);

        if (hudView == null || syringeViews.Length != 2 || graphics.Length != 2 || labelPools.Length != 2)
            throw new InvalidOperationException("Player bars prefab does not contain the expected one HUD view and two complete syringe views.");

        if (layoutGroup == null || layoutGroup.childForceExpandHeight)
            throw new InvalidOperationException("Player bars prefab must use one non-expanding VerticalLayoutGroup as the exclusive vertical-position authority.");

        if (prefab.transform.childCount < 3 ||
            prefab.transform.GetChild(0).name != "PlayerHealthSyringe" ||
            prefab.transform.GetChild(1).name != "PlayerShieldSyringe" ||
            prefab.transform.GetChild(2).name != "PlayerExperienceBar")
        {
            throw new InvalidOperationException("Player bars prefab children are not ordered Health, Shield, Experience for deterministic vertical layout.");
        }

        SerializedObject hudObject = new SerializedObject(hudView);

        if (hudObject.FindProperty("editorPreviewPreset").objectReferenceValue == null)
            throw new InvalidOperationException("Player bars prefab is missing the direct Player Visual Preset reference required by its Edit Mode preview.");

        for (int index = 0; index < graphics.Length; index++)
        {
            if (graphics[index].material == null || graphics[index].material.shader == null)
                throw new InvalidOperationException("Player syringe graphic is missing its Edit Mode preview material.");
        }

        int requiredSyringeLabels = PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity * 2;

        if (labels.Length < requiredSyringeLabels)
            throw new InvalidOperationException("Player bars prefab does not contain the required preauthored numeric label capacity.");
    }

    /// <summary>
    /// Validates that Edit Mode preview geometry, spacing, and shield visibility resolve through the runtime configuration builder.
    /// </summary>
    private static void ValidateEditorPreview()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        PlayerHealthBarsHudView hudView = instance.GetComponent<PlayerHealthBarsHudView>();
        RectTransform healthRoot = instance.transform.Find("PlayerHealthSyringe") as RectTransform;
        Transform shieldRoot = instance.transform.Find("PlayerShieldSyringe");
        VerticalLayoutGroup layoutGroup = instance.GetComponent<VerticalLayoutGroup>();
        SerializedObject hudObject = new SerializedObject(hudView);
        PlayerVisualPreset previewPreset = hudObject.FindProperty("editorPreviewPreset").objectReferenceValue as PlayerVisualPreset;
        PlayerHealthBarVisualConfig previewConfig = PlayerHealthBarVisualBakeUtility.BuildConfig(previewPreset);

        try
        {
            hudView.RefreshEditorPreview();

            if (!Mathf.Approximately(healthRoot.sizeDelta.y, previewConfig.BarHeight) ||
                healthRoot.sizeDelta.x < previewConfig.MinimumLength ||
                healthRoot.sizeDelta.x > previewConfig.MaximumLength)
            {
                throw new InvalidOperationException("Edit Mode preview did not rebuild health syringe geometry through the selected Player Visual Preset.");
            }

            if (!Mathf.Approximately(layoutGroup.spacing, previewConfig.VerticalSpacing))
                throw new InvalidOperationException("Edit Mode preview did not apply Player Visual Preset vertical spacing.");

            if (shieldRoot.gameObject.activeSelf)
                throw new InvalidOperationException("Edit Mode preview did not apply the zero-maximum shield visibility policy.");
        }
        finally
        {
            hudView.Dispose();
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    /// <summary>
    /// Validates that short one-division syringes preserve stable pixel-sized decorations.
    /// </summary>
    private static void ValidateShortSyringeDecorationScale()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        Transform shieldRoot = instance.transform.Find("PlayerShieldSyringe");
        PlayerSyringeBarView view = shieldRoot != null
            ? shieldRoot.GetComponent<PlayerSyringeBarView>()
            : null;

        try
        {
            if (view == null)
                throw new InvalidOperationException("Shield syringe view is missing during short-decoration validation.");

            PlayerHealthBarVisualConfig config = PlayerHealthBarVisualBakeUtility.BuildConfig(null);
            config.MinimumLength = 114f;
            config.MaximumLength = 200f;
            config.PaintDrips.Enabled = 1;
            config.PaintDrips.Width = 0.026f;
            view.ApplyConfiguration(in config, in config.Shield, null);
            view.UpdateValue(1f, 1f, 0f, true);
            PlayerSyringeBarGraphic graphic = shieldRoot.GetComponentInChildren<PlayerSyringeBarGraphic>(true);

            if (graphic == null || graphic.material == null)
                throw new InvalidOperationException("Short shield syringe graphic is missing its runtime material.");

            float resolvedLength = view.Root.rect.width;
            float expectedPlungerWidth = Mathf.Clamp(config.PlungerWidth * 340f / resolvedLength, 0f, 0.2f);
            float expectedPaintDripWidth = Mathf.Clamp(config.PaintDrips.Width * 340f / resolvedLength, 0f, 0.25f);
            float expectedLengthScale = Mathf.Clamp(resolvedLength / 340f, 0.25f, 4f);
            float shaderPlungerWidth = graphic.material.GetFloat("_PlungerWidth");
            float shaderPaintDripWidth = graphic.material.GetFloat("_PaintDripWidth");
            float shaderLengthScale = graphic.material.GetFloat("_LengthPixelScale");

            if (!Mathf.Approximately(shaderPlungerWidth, expectedPlungerWidth) ||
                !Mathf.Approximately(shaderPaintDripWidth, expectedPaintDripWidth) ||
                !Mathf.Approximately(shaderLengthScale, expectedLengthScale) ||
                shaderPlungerWidth <= config.PlungerWidth ||
                shaderPaintDripWidth <= config.PaintDrips.Width)
            {
                throw new InvalidOperationException(string.Format("Short syringe decoration compensation failed. Plunger={0}/{1}, Drip={2}/{3}, LengthScale={4}/{5}.",
                                                                  shaderPlungerWidth,
                                                                  expectedPlungerWidth,
                                                                  shaderPaintDripWidth,
                                                                  expectedPaintDripWidth,
                                                                  shaderLengthScale,
                                                                  expectedLengthScale));
            }
        }
        finally
        {
            if (view != null)
                view.Dispose();

            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    /// <summary>
    /// Validates that a zero-maximum shield stays hidden and becomes visible after its authoritative maximum increases.
    /// </summary>
    private static void ValidateShieldVisibilityPolicy()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        PlayerHealthBarsHudView hudView = instance.GetComponent<PlayerHealthBarsHudView>();
        Transform shieldRoot = instance.transform.Find("PlayerShieldSyringe");
        RectTransform shieldRect = shieldRoot as RectTransform;
        RectTransform experienceRect = instance.transform.Find("PlayerExperienceBar") as RectTransform;
        RectTransform layoutRoot = instance.transform as RectTransform;
        World world = new World("PlayerHealthBarsShieldVisibilitySmokeTestWorld");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity playerEntity = entityManager.CreateEntity();
            Entity configEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(playerEntity, new PlayerHealth
            {
                Current = 100f,
                Max = 100f
            });
            entityManager.AddComponentData(playerEntity, new PlayerShield());
            entityManager.AddComponentData(playerEntity, new PlayerHealthBarVisualReference
            {
                ConfigEntity = configEntity
            });
            entityManager.AddComponentData(configEntity, PlayerHealthBarVisualBakeUtility.BuildConfig(null));
            entityManager.AddComponentData(configEntity, new PlayerHealthBarVisualScalingState
            {
                LastScalableStatsHash = 1
            });
            hudView.Initialize();
            hudView.UpdateView(entityManager, playerEntity, true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);

            if (shieldRoot.gameObject.activeSelf)
                throw new InvalidOperationException("Shield syringe remained visible while PlayerShield.Max was zero.");

            float experiencePositionWithoutShield = experienceRect.anchoredPosition.y;
            entityManager.SetComponentData(playerEntity, new PlayerShield
            {
                Current = 20f,
                Max = 20f
            });
            hudView.UpdateView(entityManager, playerEntity, true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);

            if (!shieldRoot.gameObject.activeSelf)
                throw new InvalidOperationException("Shield syringe did not become visible after PlayerShield.Max increased.");

            if (experienceRect.anchoredPosition.y >= experiencePositionWithoutShield)
                throw new InvalidOperationException("Experience bar did not move below the newly visible shield syringe.");

            float shieldPositionBeforeConfigRefresh = shieldRect.anchoredPosition.y;
            entityManager.SetComponentData(configEntity, new PlayerHealthBarVisualScalingState
            {
                LastScalableStatsHash = 2
            });
            hudView.UpdateView(entityManager, playerEntity, true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);

            if (!Mathf.Approximately(shieldRect.anchoredPosition.y, shieldPositionBeforeConfigRefresh))
                throw new InvalidOperationException("Shield syringe changed vertical position after a level-up-style visual configuration refresh.");
        }
        finally
        {
            hudView.Dispose();
            world.Dispose();
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    /// <summary>
    /// Validates the target UI scene binding and confirms health/shield presentation settings no longer belong to HUDManager.
    /// </summary>
    private static void ValidateScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        HUDManager hudManager = UnityEngine.Object.FindFirstObjectByType<HUDManager>(FindObjectsInactive.Include);
        PlayerHealthBarsHudView hudView = UnityEngine.Object.FindFirstObjectByType<PlayerHealthBarsHudView>(FindObjectsInactive.Include);

        if (hudManager == null || hudView == null)
            throw new InvalidOperationException("Target UI scene is missing HUDManager or PlayerHealthBarsHudView.");

        SerializedObject hudManagerObject = new SerializedObject(hudManager);
        SerializedProperty binding = hudManagerObject.FindProperty("playerHealthBarsView");

        if (binding == null || binding.objectReferenceValue != hudView)
            throw new InvalidOperationException("HUDManager is not bound to the preauthored PlayerHealthBarsHudView.");

        if (hudManagerObject.FindProperty("healthBarPresentation") != null ||
            hudManagerObject.FindProperty("shieldBarPresentation") != null ||
            hudManagerObject.FindProperty("healthBarSmoothingSeconds") != null ||
            hudManagerObject.FindProperty("shieldBarSmoothingSeconds") != null)
        {
            throw new InvalidOperationException("Legacy player health or shield visual settings are still serialized by HUDManager.");
        }

        if (!scene.isLoaded)
            throw new InvalidOperationException("Target UI scene failed to load during validation.");
    }

    /// <summary>
    /// Configures one serialized Add Scaling row for smoke-test metadata generation.
    /// </summary>
    /// <param name="rule">Serialized scaling rule row.</param>
    /// <param name="statKey">Stable target stat key.</param>
    /// <param name="formula">Unified formula text.</param>
    private static void ConfigureRule(SerializedProperty rule, string statKey, string formula)
    {
        rule.FindPropertyRelative("statKey").stringValue = statKey;
        rule.FindPropertyRelative("addScaling").boolValue = true;
        rule.FindPropertyRelative("formula").stringValue = formula;
    }
    #endregion

    #endregion
}
