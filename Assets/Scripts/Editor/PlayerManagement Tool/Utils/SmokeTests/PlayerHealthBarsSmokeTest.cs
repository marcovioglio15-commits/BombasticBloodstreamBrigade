using System;
using System.Text;
using TMPro;
using UnityEditor;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Validates the preauthored player health/shield syringe hierarchy, technical assets, and target-scene binding.
/// </summary>
public static class PlayerHealthBarsSmokeTest
{
    #region Constants
    private const string PrefabPath = "Assets/Prefabs/UI/PlayerBars VerticalBox.prefab";
    private const string MaterialPath = "Assets/2D/Materials/M_UI_PlayerSyringeBar.mat";
    private const string ShieldMaterialPath = "Assets/2D/Materials/M_UI_PlayerShieldSyringeBar.mat";
    private const string FontPath = "Assets/2D/Fonts/NoctraDrip-Solid SDF.asset";
    private const string VisualPresetPath = "Assets/Scriptable Objects/Player/Visual/PlayerVisualPreset_A.asset";
    private const string MasterPresetPath = "Assets/Scriptable Objects/Player/Master Presets/PlayerMasterPreset_A.asset";
    private const int TransparentRenderQueue = 3000;
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
        PlayerHealthBarsLabelDistributionSmokeTestUtility.Validate();
        ValidateLabelRenderQueueOrdering();
        ValidateGraduationAlignmentAndMotionReset();
        PlayerHealthBarsDecorationScaleSmokeTestUtility.Validate();
        ValidateFirstRuntimeValueSnap();
        PlayerHealthBarsRuntimeSmokeTestUtility.ValidateShieldVisibilityPolicy();
        PlayerHealthBarsRuntimeSmokeTestUtility.ValidateScene();
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
            SerializedProperty experience = healthBars.FindPropertyRelative("experience");
            SerializedProperty motion = health.FindPropertyRelative("motion");
            SerializedProperty palette = health.FindPropertyRelative("palette");
            SerializedProperty experiencePalette = experience.FindPropertyRelative("palette");
            SerializedProperty outlineStyle = health.FindPropertyRelative("outlineStyle");
            SerializedProperty experienceShape = healthBars.FindPropertyRelative("experienceShape");
            SerializedProperty colorChannel = palette.FindPropertyRelative("liquid").FindPropertyRelative("r");
            SerializedProperty experienceLiquidColorChannel = experiencePalette.FindPropertyRelative("liquid").FindPropertyRelative("r");
            SerializedProperty healthEnabled = health.FindPropertyRelative("enabled");
            SerializedProperty outlineStyleEnabled = outlineStyle.FindPropertyRelative("enabled");
            SerializedProperty outlineEdgeWobbleStrength = outlineStyle.FindPropertyRelative("edgeWobbleStrength");
            SerializedProperty experienceShapeBodyStyle = experienceShape.FindPropertyRelative("bodyStyle");
            SerializedProperty experienceShapeUniformLabelCount = experienceShape.FindPropertyRelative("uniformLabelCount");
            SerializedProperty experienceShapeTerminationEnabled = experienceShape.FindPropertyRelative("terminationEnabled");
            SerializedProperty terminationStyle = healthBars.FindPropertyRelative("terminationStyle");
            SerializedProperty terminationEnabled = healthBars.FindPropertyRelative("terminationEnabled");
            SerializedProperty bodyStyle = healthBars.FindPropertyRelative("bodyStyle");
            SerializedProperty labelPlacement = healthBars.FindPropertyRelative("labelPlacement");
            SerializedProperty graduationMode = healthBars.FindPropertyRelative("graduationMode");
            SerializedProperty uniformLabelCount = healthBars.FindPropertyRelative("uniformLabelCount");
            SerializedProperty labelMinimumSpacing = healthBars.FindPropertyRelative("labelMinimumSpacing");
            SerializedProperty graduationEndPadding = healthBars.FindPropertyRelative("graduationEndPadding");
            SerializedProperty terminationOffset = healthBars.FindPropertyRelative("terminationOffset");
            SerializedProperty clampPlungerStartInsideBody = healthBars.FindPropertyRelative("clampPlungerStartInsideBody");
            SerializedProperty clampPlungerEndInsideBody = healthBars.FindPropertyRelative("clampPlungerEndInsideBody");
            SerializedProperty stopLiquidAtPlunger = healthBars.FindPropertyRelative("stopLiquidAtPlunger");
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
            SerializedProperty sloshAffectsBubblesOnly = health.FindPropertyRelative("sloshAffectsBubblesOnly");
            SerializedProperty graduationVerticalOffset = healthBars.FindPropertyRelative("graduationVerticalOffset");
            SerializedProperty fontAsset = healthBars.FindPropertyRelative("fontAsset");
            TMP_FontAsset expectedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            fontAsset.objectReferenceValue = expectedFont;

            if (experienceLiquidColorChannel != null)
                experienceLiquidColorChannel.floatValue = 0.33f;

            if (colorChannel == null ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(colorChannel) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(experienceLiquidColorChannel) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(healthEnabled) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(outlineStyleEnabled) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(outlineEdgeWobbleStrength) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(experienceShapeBodyStyle) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(experienceShapeUniformLabelCount) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(experienceShapeTerminationEnabled) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(terminationStyle) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(terminationEnabled) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(bodyStyle) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(labelPlacement) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(graduationMode) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(uniformLabelCount) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(labelMinimumSpacing) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(graduationEndPadding) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(terminationOffset) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(clampPlungerStartInsideBody) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(clampPlungerEndInsideBody) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(stopLiquidAtPlunger) ||
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
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(horizontalSloshStrength) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(sloshAffectsBubblesOnly) ||
                !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(graduationVerticalOffset))
            {
                throw new InvalidOperationException("Health Bars Add Scaling target support is incomplete.");
            }

            SerializedProperty scalingRules = presetObject.FindProperty("scalingRules");
            scalingRules.arraySize = 33;
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
            ConfigureRule(scalingRules.GetArrayElementAtIndex(19), PlayerScalingStatKeyUtility.BuildStatKey(sloshAffectsBubblesOnly), "![this]");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(20), PlayerScalingStatKeyUtility.BuildStatKey(graduationVerticalOffset), "[this] + 0.1");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(21), PlayerScalingStatKeyUtility.BuildStatKey(graduationMode), "[this] + ([UniformLabels] - [this])");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(22), PlayerScalingStatKeyUtility.BuildStatKey(uniformLabelCount), "[this] + 2");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(23), PlayerScalingStatKeyUtility.BuildStatKey(clampPlungerStartInsideBody), "![this]");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(24), PlayerScalingStatKeyUtility.BuildStatKey(clampPlungerEndInsideBody), "![this]");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(25), PlayerScalingStatKeyUtility.BuildStatKey(stopLiquidAtPlunger), "![this]");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(26), PlayerScalingStatKeyUtility.BuildStatKey(outlineStyleEnabled), "![this]");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(27), PlayerScalingStatKeyUtility.BuildStatKey(outlineEdgeWobbleStrength), "[this] + 0.2");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(28), PlayerScalingStatKeyUtility.BuildStatKey(experienceShapeBodyStyle), "[this] + ([DetailedSyringe] - [this])");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(29), PlayerScalingStatKeyUtility.BuildStatKey(experienceShapeUniformLabelCount), "[this] + 1");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(30), PlayerScalingStatKeyUtility.BuildStatKey(terminationEnabled), "![this]");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(31), PlayerScalingStatKeyUtility.BuildStatKey(experienceShapeTerminationEnabled), "![this]");
            ConfigureRule(scalingRules.GetArrayElementAtIndex(32), PlayerScalingStatKeyUtility.BuildStatKey(experienceLiquidColorChannel), "[this] + 0.07");
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

            if (!Mathf.Approximately(baseConfig.Experience.Palette.Liquid.x, 0.33f))
                throw new InvalidOperationException("Experience syringe direct palette was overwritten before runtime scaling.");

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

            if (metadata.Length != 33)
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
                runtimeConfig.GraduationMode != PlayerSyringeGraduationMode.UniformLabels ||
                runtimeConfig.UniformLabelCount != baseConfig.UniformLabelCount + 2 ||
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
                runtimeConfig.Health.SloshAffectsBubblesOnly == 0 ||
                runtimeConfig.ClampPlungerStartInsideBody != 0 ||
                runtimeConfig.ClampPlungerEndInsideBody == 0 ||
                runtimeConfig.StopLiquidAtPlunger != 0 ||
                runtimeConfig.Health.OutlineStyle.Enabled == 0 ||
                !Mathf.Approximately(runtimeConfig.Health.OutlineStyle.EdgeWobbleStrength, baseConfig.Health.OutlineStyle.EdgeWobbleStrength + 0.2f) ||
                runtimeConfig.ExperienceShape.BodyStyle != PlayerSyringeBodyStyle.DetailedSyringe ||
                runtimeConfig.ExperienceShape.UniformLabelCount != baseConfig.ExperienceShape.UniformLabelCount + 1 ||
                runtimeConfig.TerminationEnabled != 0 ||
                runtimeConfig.ExperienceShape.TerminationEnabled != 0 ||
                !Mathf.Approximately(runtimeConfig.Experience.Palette.Liquid.x, baseConfig.Experience.Palette.Liquid.x + 0.07f) ||
                !Mathf.Approximately(runtimeConfig.GraduationVerticalOffset, baseConfig.GraduationVerticalOffset + 0.1f) ||
                runtimeConfig.FontAsset.Value != expectedFont ||
                scalingState.Initialized == 0 ||
                scalingState.LastScalableStatsHash != 123u)
            {
                throw new InvalidOperationException(string.Format("Health Bars runtime scaling rebuild mismatch. Color={0}/{1}, Enabled={2}, Termination={3}, Body={4}, Placement={5}, Spacing={6}, Padding={7}, OutlineWidth={8}, Drips={9}/{10}, Label={11}/{12}, Horizontal={13}/{14}, Font='{15}', State={16}/{17}, Metadata={18}, SloshBubbles={19}, GradOffset={20}, GraduationMode={21}, UniformLabels={22}, ClampStart={23}, ClampEnd={24}, StopLiquid={25}, OutlineStyle={26}/{27}, ExperienceShape={28}/{29}, TerminationEnabled={30}/{31}, ExperienceColor={32}/{33}.",
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
                                                                  metadataDetails,
                                                                  runtimeConfig.Health.SloshAffectsBubblesOnly,
                                                                  runtimeConfig.GraduationVerticalOffset,
                                                                  runtimeConfig.GraduationMode,
                                                                  runtimeConfig.UniformLabelCount,
                                                                  runtimeConfig.ClampPlungerStartInsideBody,
                                                                  runtimeConfig.ClampPlungerEndInsideBody,
                                                                  runtimeConfig.StopLiquidAtPlunger,
                                                                  runtimeConfig.Health.OutlineStyle.Enabled,
                                                                  runtimeConfig.Health.OutlineStyle.EdgeWobbleStrength,
                                                                  runtimeConfig.ExperienceShape.BodyStyle,
                                                                  runtimeConfig.ExperienceShape.UniformLabelCount,
                                                                  runtimeConfig.TerminationEnabled,
                                                                  runtimeConfig.ExperienceShape.TerminationEnabled,
                                                                  runtimeConfig.Experience.Palette.Liquid.x,
                                                                  baseConfig.Experience.Palette.Liquid.x + 0.07f));
            }
        }
        finally
        {
            world.Dispose();
            UnityEngine.Object.DestroyImmediate(preset);
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

            PlayerHealthBarVisualConfig config = PlayerHealthBarVisualBakeUtility.BuildConfig((PlayerVisualPreset)null);
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

        if (prefab.transform.Find("PlayerHealthSyringe") == null ||
            prefab.transform.Find("PlayerShieldSyringe") == null ||
            prefab.transform.Find("PlayerExperienceSyringe") == null)
        {
            throw new InvalidOperationException("Preauthored health, shield, or experience syringe root is missing.");
        }

        PlayerHealthBarsHudView hudView = prefab.GetComponent<PlayerHealthBarsHudView>();
        VerticalLayoutGroup layoutGroup = prefab.GetComponent<VerticalLayoutGroup>();
        PlayerSyringeBarView[] syringeViews = prefab.GetComponentsInChildren<PlayerSyringeBarView>(true);
        PlayerSyringeBarGraphic[] graphics = prefab.GetComponentsInChildren<PlayerSyringeBarGraphic>(true);
        PlayerSyringeBarLabelPool[] labelPools = prefab.GetComponentsInChildren<PlayerSyringeBarLabelPool>(true);
        TMP_Text[] labels = prefab.GetComponentsInChildren<TMP_Text>(true);

        if (hudView == null || syringeViews.Length != 3 || graphics.Length != 3 || labelPools.Length != 3)
            throw new InvalidOperationException("Player bars prefab does not contain the expected one HUD view and three complete syringe views.");

        if (layoutGroup == null || layoutGroup.childForceExpandHeight)
            throw new InvalidOperationException("Player bars prefab must use one non-expanding VerticalLayoutGroup as the exclusive vertical-position authority.");

        if (prefab.transform.childCount < 3 ||
            prefab.transform.GetChild(0).name != "PlayerHealthSyringe" ||
            prefab.transform.GetChild(1).name != "PlayerShieldSyringe" ||
            prefab.transform.GetChild(2).name != "PlayerExperienceSyringe")
        {
            throw new InvalidOperationException("Player bars prefab children are not ordered Health, Shield, Experience for deterministic vertical layout.");
        }

        SerializedObject hudObject = new SerializedObject(hudView);
        PlayerMasterPreset expectedMasterPreset = AssetDatabase.LoadAssetAtPath<PlayerMasterPreset>(MasterPresetPath);

        if (hudObject.FindProperty("editorPreviewMasterPreset").objectReferenceValue != expectedMasterPreset)
            throw new InvalidOperationException("Player bars prefab is missing the direct Player Master Preset reference required for runtime-equivalent Edit Mode health length.");

        if (hudObject.FindProperty("editorPreviewPreset").objectReferenceValue == null)
            throw new InvalidOperationException("Player bars prefab is missing the direct Player Visual Preset reference required by its Edit Mode preview.");

        for (int index = 0; index < graphics.Length; index++)
        {
            if (graphics[index].material == null || graphics[index].material.shader == null)
                throw new InvalidOperationException("Player syringe graphic is missing its Edit Mode preview material.");
        }

        int requiredSyringeLabels = PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity * 3;

        if (labels.Length < requiredSyringeLabels)
            throw new InvalidOperationException("Player bars prefab does not contain the required preauthored numeric label capacity.");

        PlayerSyringeBarSmokeTestLayoutUtility.ValidatePlayerBarsLabelCounterRotation(prefab.transform);
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
        PlayerMasterPreset masterPreset = hudObject.FindProperty("editorPreviewMasterPreset").objectReferenceValue as PlayerMasterPreset;
        PlayerVisualPreset previewPreset = masterPreset != null && masterPreset.VisualPreset != null
            ? masterPreset.VisualPreset
            : hudObject.FindProperty("editorPreviewPreset").objectReferenceValue as PlayerVisualPreset;
        PlayerHealthBarVisualConfig previewConfig = PlayerHealthBarVisualBakeUtility.BuildConfig(previewPreset);
        PlayerControllerPreset controllerPreset = masterPreset != null
            ? masterPreset.ControllerPreset
            : null;
        float expectedHealthMaximum = controllerPreset != null && controllerPreset.HealthStatistics != null
            ? Mathf.Max(1f,
                        PlayerSyringeBarPreviewLengthTestUtility.ResolveExpectedScaledControllerValue(masterPreset,
                                                                                                      controllerPreset,
                                                                                                      "healthStatistics.maxHealth",
                                                                                                      controllerPreset.HealthStatistics.MaxHealth))
            : Mathf.Max(0.0001f, hudObject.FindProperty("editorPreviewHealthMaximum").floatValue);
        float expectedHealthLength = PlayerSyringeBarPreviewLengthTestUtility.ResolveExpectedLength(previewConfig, expectedHealthMaximum);

        try
        {
            hudView.RefreshEditorPreview();

            if (!Mathf.Approximately(healthRoot.sizeDelta.y, previewConfig.BarHeight) ||
                !Mathf.Approximately(healthRoot.sizeDelta.x, expectedHealthLength))
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
    /// Validates that shield labels render after the procedural syringe even when the portrait UI rotates the bar root in 3D.
    /// </summary>
    private static void ValidateLabelRenderQueueOrdering()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        PlayerVisualPreset visualPreset = AssetDatabase.LoadAssetAtPath<PlayerVisualPreset>(VisualPresetPath);
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        Transform shieldRoot = instance.transform.Find("PlayerShieldSyringe");
        PlayerSyringeBarView shieldView = shieldRoot != null
            ? shieldRoot.GetComponent<PlayerSyringeBarView>()
            : null;
        PlayerSyringeBarGraphic shieldGraphic = shieldRoot != null
            ? shieldRoot.GetComponentInChildren<PlayerSyringeBarGraphic>(true)
            : null;

        try
        {
            if (visualPreset == null || shieldView == null || shieldGraphic == null)
                throw new InvalidOperationException("Shield syringe render-queue validation is missing its preset, view, or graphic.");

            PlayerHealthBarVisualConfig config = PlayerHealthBarVisualBakeUtility.BuildConfig(visualPreset);
            TMP_FontAsset font = config.FontAsset.Value;
            shieldView.ApplyConfiguration(in config, in config.Shield, font);
            shieldView.UpdateValue(5f, 10f, 0f, true);
            Canvas.ForceUpdateCanvases();

            int syringeQueue = ResolveMaterialRenderQueue(shieldGraphic.material);
            TMP_Text[] labels = shieldRoot.GetComponentsInChildren<TMP_Text>(true);
            int activeCount = 0;

            for (int index = 0; index < labels.Length; index++)
            {
                if (!labels[index].gameObject.activeSelf)
                    continue;

                activeCount++;
                Material labelMaterial = labels[index].fontSharedMaterial;

                if (labelMaterial == null || ResolveMaterialRenderQueue(labelMaterial) <= syringeQueue)
                {
                    throw new InvalidOperationException(string.Format("Shield label '{0}' is not forced above the syringe graphic render queue.",
                                                                      labels[index].name));
                }
            }

            if (activeCount != 10)
                throw new InvalidOperationException(string.Format("Shield render-queue validation expected 10 active labels but found {0}.",
                                                                  activeCount));
        }
        finally
        {
            if (shieldView != null)
                shieldView.Dispose();

            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    /// <summary>
    /// Validates that the first runtime value initializes immediately even when smoothing is enabled.
    /// </summary>
    private static void ValidateFirstRuntimeValueSnap()
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
                throw new InvalidOperationException("Health syringe view is missing during first runtime value validation.");

            PlayerHealthBarVisualConfig config = PlayerHealthBarVisualBakeUtility.BuildConfig((PlayerVisualPreset)null);
            config.Health.SmoothingSeconds = 0.35f;
            view.ApplyConfiguration(in config, in config.Health, null);
            view.UpdateValue(5f, 5f, 0f, false);
            PlayerSyringeBarGraphic graphic = healthRoot.GetComponentInChildren<PlayerSyringeBarGraphic>(true);

            if (graphic == null || graphic.material == null)
                throw new InvalidOperationException("First runtime value validation is missing the health syringe runtime material.");

            float shaderFill = graphic.material.GetFloat("_FillNormalized");

            if (!Mathf.Approximately(shaderFill, 1f))
            {
                throw new InvalidOperationException(string.Format("First runtime health syringe value did not initialize immediately with smoothing enabled. Fill={0}.",
                                                                  shaderFill));
            }

            view.HandleMissing(true);
            view.HandleMissing(false);
            view.UpdateValue(2f, 5f, 0f, false);
            shaderFill = graphic.material.GetFloat("_FillNormalized");

            if (!Mathf.Approximately(shaderFill, 0.4f))
            {
                throw new InvalidOperationException(string.Format("Health syringe value did not reinitialize after visibility was restored. Fill={0}.",
                                                                  shaderFill));
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
    /// Resolves the effective transparent render queue used by a UI material.
    /// </summary>
    /// <param name="material">Material rendered by a UGUI graphic or TMP label.</param>
    /// <returns>Explicit material queue, shader queue, or Unity transparent queue fallback.</returns>
    private static int ResolveMaterialRenderQueue(Material material)
    {
        if (material == null)
            return TransparentRenderQueue;

        if (material.renderQueue >= 0)
            return material.renderQueue;

        if (material.shader != null)
            return material.shader.renderQueue;

        return TransparentRenderQueue;
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
