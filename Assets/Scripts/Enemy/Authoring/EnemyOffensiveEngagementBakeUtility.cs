using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Compiles offensive engagement feedback settings from the active shared pattern and visual preset into ECS runtime buffers.
/// </summary>
internal static class EnemyOffensiveEngagementBakeUtility
{
    #region Fields
    private static readonly EnemyOffensiveEngagementFeedbackSettings DefaultSettings = new EnemyOffensiveEngagementFeedbackSettings();
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Appends every supported offensive engagement feedback configuration for the currently selected shared pattern.
    /// </summary>
    /// <param name="authoring">Enemy authoring component that resolves visual and advanced-pattern presets.</param>
    /// <param name="configs">Target dynamic buffer populated during bake.</param>
    public static void AppendConfigs(EnemyAuthoring authoring, DynamicBuffer<EnemyOffensiveEngagementConfigElement> configs)
    {
        if (authoring == null)
            return;

        EnemyAdvancedPatternPreset advancedPatternPreset = authoring.AdvancedPatternPreset;

        if (advancedPatternPreset == null)
            return;

        EnemyModulesAndPatternsPreset sharedPreset = advancedPatternPreset.ModulesAndPatternsPreset;

        if (sharedPreset == null)
            return;

        EnemyModulesPatternDefinition selectedPattern = EnemyModulesAndPatternsSelectionUtility.ResolveSelectedPattern(advancedPatternPreset);

        if (selectedPattern == null)
            return;

        EnemyOffensiveEngagementFeedbackSettings globalSettings = authoring.OffensiveEngagementFeedbackSettings;

        if (globalSettings == null)
            globalSettings = DefaultSettings;

        AppendShortRangeConfig(selectedPattern, sharedPreset, globalSettings, configs);
        AppendWeaponConfig(selectedPattern, sharedPreset, globalSettings, configs);
    }

    /// <summary>
    /// Converts boss pattern-change feedback settings into an ECS config that uses lead-time fields as post-extraction display durations.
    /// </summary>
    /// <param name="settings">Resolved visual preset settings block.</param>
    /// <returns>Baked boss pattern-change feedback config.</returns>
    public static EnemyBossPatternChangeFeedbackConfig CreateBossPatternChangeFeedbackConfig(EnemyOffensiveEngagementFeedbackSettings settings)
    {
        if (settings == null)
        {
            settings = DefaultSettings;
        }

        Vector3 billboardOffset = settings.BillboardLocalOffset;
        bool hasVisibleChannel = settings.EnableColorBlend || settings.EnableBillboard;

        return new EnemyBossPatternChangeFeedbackConfig
        {
            Enabled = hasVisibleChannel ? (byte)1 : (byte)0,
            EnableColorBlend = settings.EnableColorBlend ? (byte)1 : (byte)0,
            ColorBlendColor = DamageFlashRuntimeUtility.ToLinearFloat4(settings.ColorBlendColor),
            ColorBlendDurationSeconds = math.max(0f, settings.ColorBlendLeadTimeSeconds),
            ColorBlendFadeOutSeconds = math.max(0f, settings.ColorBlendFadeOutSeconds),
            ColorBlendMaximumBlend = math.saturate(settings.ColorBlendMaximumBlend),
            EnableBillboard = settings.EnableBillboard ? (byte)1 : (byte)0,
            BillboardColor = DamageFlashRuntimeUtility.ToLinearFloat4(settings.BillboardColor),
            BillboardOffset = new float3(billboardOffset.x, billboardOffset.y, billboardOffset.z),
            BillboardDurationSeconds = math.max(0f, settings.BillboardLeadTimeSeconds),
            BillboardBaseScale = math.max(0f, settings.BillboardBaseScale),
            BillboardPulseScaleMultiplier = math.max(0f, settings.BillboardPulseScaleMultiplier),
            BillboardPulseExpandDurationSeconds = math.max(0f, settings.BillboardPulseExpandDurationSeconds),
            BillboardPulseContractDurationSeconds = math.max(0f, settings.BillboardPulseContractDurationSeconds)
        };
    }

    /// <summary>
    /// Builds one Core Movement offensive engagement config from a boss module candidate.
    /// </summary>
    /// <param name="candidate">Core Movement candidate being compiled.</param>
    /// <param name="sharedPreset">Shared source preset used to resolve the selected module kind.</param>
    /// <param name="globalSettings">Generic visual feedback settings resolved from the visual preset.</param>
    /// <param name="config">Output baked offensive engagement config.</param>
    /// <returns>True when the candidate exposes a visible activation feedback config.</returns>
    internal static bool TryBuildCoreMovementConfig(EnemyBossPatternCoreMovementModuleCandidateDefinition candidate,
                                                    EnemyModulesAndPatternsPreset sharedPreset,
                                                    EnemyOffensiveEngagementFeedbackSettings globalSettings,
                                                    out EnemyOffensiveEngagementConfigElement config)
    {
        config = default;

        if (candidate == null ||
            candidate.ModuleMode == EnemyBossPatternModuleMode.NullModule ||
            !candidate.DisplayBehaviourEngagementTrigger ||
            candidate.Binding == null)
        {
            return false;
        }

        return TryBuildConfig(candidate.Binding,
                              sharedPreset,
                              globalSettings,
                              candidate.UseEngagementFeedbackOverride,
                              candidate.EngagementFeedbackOverride,
                              EnemyPatternModuleCatalogSection.CoreMovement,
                              EnemyOffensiveEngagementTriggerSource.CoreMovement,
                              out config);
    }

    /// <summary>
    /// Builds one short-range offensive engagement config from an explicit pattern assemble slot.
    /// </summary>
    /// <param name="interaction">Short-range interaction slot being compiled.</param>
    /// <param name="sharedPreset">Shared source preset used to resolve the selected module kind.</param>
    /// <param name="globalSettings">Generic visual feedback settings resolved from the visual preset.</param>
    /// <param name="config">Output baked offensive engagement config.</param>
    /// <returns>True when the slot exposes a supported predictive warning config.</returns>
    internal static bool TryBuildShortRangeConfig(EnemyPatternShortRangeInteractionAssembly interaction,
                                                  EnemyModulesAndPatternsPreset sharedPreset,
                                                  EnemyOffensiveEngagementFeedbackSettings globalSettings,
                                                  out EnemyOffensiveEngagementConfigElement config)
    {
        config = default;

        if (interaction == null ||
            !interaction.IsEnabled ||
            !interaction.DisplayBehaviourEngagementTrigger ||
            interaction.Binding == null)
        {
            return false;
        }

        return TryBuildConfig(interaction.Binding,
                              sharedPreset,
                              globalSettings,
                              interaction.UseEngagementFeedbackOverride,
                              interaction.EngagementFeedbackOverride,
                              EnemyPatternModuleCatalogSection.ShortRangeInteraction,
                              EnemyOffensiveEngagementTriggerSource.ShortRangeInteraction,
                              out config);
    }

    /// <summary>
    /// Builds one weapon offensive engagement config from an explicit pattern assemble slot.
    /// </summary>
    /// <param name="interaction">Weapon interaction slot being compiled.</param>
    /// <param name="sharedPreset">Shared source preset used to resolve the selected module kind.</param>
    /// <param name="globalSettings">Generic visual feedback settings resolved from the visual preset.</param>
    /// <param name="config">Output baked offensive engagement config.</param>
    /// <returns>True when the slot exposes a supported predictive warning config.</returns>
    internal static bool TryBuildWeaponConfig(EnemyPatternWeaponInteractionAssembly interaction,
                                              EnemyModulesAndPatternsPreset sharedPreset,
                                              EnemyOffensiveEngagementFeedbackSettings globalSettings,
                                              out EnemyOffensiveEngagementConfigElement config)
    {
        config = default;

        if (interaction == null ||
            !interaction.IsEnabled ||
            !interaction.DisplayBehaviourEngagementTrigger ||
            interaction.Binding == null)
        {
            return false;
        }

        return TryBuildConfig(interaction.Binding,
                              sharedPreset,
                              globalSettings,
                              interaction.UseEngagementFeedbackOverride,
                              interaction.EngagementFeedbackOverride,
                              EnemyPatternModuleCatalogSection.WeaponInteraction,
                              EnemyOffensiveEngagementTriggerSource.WeaponInteraction,
                              out config);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Appends the short-range offensive engagement feedback configuration when the selected module kind supports timing prediction.
    /// </summary>
    /// <param name="pattern">Selected shared pattern definition.</param>
    /// <param name="sharedPreset">Shared preset used to resolve the selected module kind.</param>
    /// <param name="globalSettings">Generic visual feedback settings resolved from the visual preset.</param>
    /// <param name="configs">Target dynamic buffer populated during bake.</param>
    private static void AppendShortRangeConfig(EnemyModulesPatternDefinition pattern,
                                               EnemyModulesAndPatternsPreset sharedPreset,
                                               EnemyOffensiveEngagementFeedbackSettings globalSettings,
                                               DynamicBuffer<EnemyOffensiveEngagementConfigElement> configs)
    {
        if (pattern == null || sharedPreset == null)
            return;

        EnemyPatternShortRangeInteractionAssembly interaction = pattern.ShortRangeInteraction;

        if (!TryBuildShortRangeConfig(interaction, sharedPreset, globalSettings, out EnemyOffensiveEngagementConfigElement config))
            return;

        configs.Add(config);
    }

    /// <summary>
    /// Appends the weapon offensive engagement feedback configuration when the selected module kind supports timing prediction.
    /// </summary>
    /// <param name="pattern">Selected shared pattern definition.</param>
    /// <param name="sharedPreset">Shared preset used to resolve the selected module kind.</param>
    /// <param name="globalSettings">Generic visual feedback settings resolved from the visual preset.</param>
    /// <param name="configs">Target dynamic buffer populated during bake.</param>
    private static void AppendWeaponConfig(EnemyModulesPatternDefinition pattern,
                                           EnemyModulesAndPatternsPreset sharedPreset,
                                           EnemyOffensiveEngagementFeedbackSettings globalSettings,
                                           DynamicBuffer<EnemyOffensiveEngagementConfigElement> configs)
    {
        if (pattern == null || sharedPreset == null)
            return;

        EnemyPatternWeaponInteractionAssembly interaction = pattern.WeaponInteraction;

        if (!TryBuildWeaponConfig(interaction, sharedPreset, globalSettings, out EnemyOffensiveEngagementConfigElement config))
            return;

        configs.Add(config);
    }

    /// <summary>
    /// Resolves the authored feedback settings block that should be baked for the current interaction.
    /// </summary>
    /// <param name="globalSettings">Generic visual feedback settings resolved from the visual preset.</param>
    /// <param name="useOverrideSettings">True when the interaction-specific override is enabled.</param>
    /// <param name="overrideSettings">Optional interaction-specific override settings.</param>
    /// <returns>The settings block that should be baked.</returns>
    private static EnemyOffensiveEngagementFeedbackSettings ResolveSettings(EnemyOffensiveEngagementFeedbackSettings globalSettings,
                                                                            bool useOverrideSettings,
                                                                            EnemyOffensiveEngagementFeedbackSettings overrideSettings)
    {
        if (useOverrideSettings && overrideSettings != null)
            return overrideSettings;

        if (globalSettings != null)
            return globalSettings;

        return DefaultSettings;
    }

    /// <summary>
    /// Resolves one module binding and authored feedback block into a baked offensive engagement config.
    /// </summary>
    /// <param name="binding">Module binding being compiled.</param>
    /// <param name="sharedPreset">Shared source preset used to resolve the selected module kind.</param>
    /// <param name="globalSettings">Generic visual feedback settings resolved from the visual preset.</param>
    /// <param name="useOverrideSettings">True when the interaction-specific override is enabled.</param>
    /// <param name="overrideSettings">Optional interaction-specific override settings.</param>
    /// <param name="section">Source module catalog section.</param>
    /// <param name="source">Interaction source currently being compiled.</param>
    /// <param name="config">Output baked offensive engagement config.</param>
    /// <returns>True when a supported and visible config was produced.</returns>
    private static bool TryBuildConfig(EnemyPatternModuleBinding binding,
                                       EnemyModulesAndPatternsPreset sharedPreset,
                                       EnemyOffensiveEngagementFeedbackSettings globalSettings,
                                       bool useOverrideSettings,
                                       EnemyOffensiveEngagementFeedbackSettings overrideSettings,
                                       EnemyPatternModuleCatalogSection section,
                                       EnemyOffensiveEngagementTriggerSource source,
                                       out EnemyOffensiveEngagementConfigElement config)
    {
        config = default;

        if (binding == null || sharedPreset == null)
            return false;

        EnemyPatternModuleDefinition moduleDefinition = sharedPreset.ResolveModuleDefinitionById(binding.ModuleId);

        if (moduleDefinition == null)
            return false;

        EnemyOffensiveEngagementTimingMode timingMode = EnemyOffensiveEngagementSupportUtility.ResolveTimingMode(section,
                                                                                                                 moduleDefinition.ModuleKind);

        if (timingMode == EnemyOffensiveEngagementTimingMode.None)
            return false;

        EnemyOffensiveEngagementFeedbackSettings settings = ResolveSettings(globalSettings,
                                                                           useOverrideSettings,
                                                                           overrideSettings);
        return TryCreateConfig(source, timingMode, useOverrideSettings, settings, out config);
    }

    /// <summary>
    /// Converts one authored settings block into a baked offensive engagement buffer entry.
    /// </summary>
    /// <param name="source">Interaction source currently being compiled.</param>
    /// <param name="timingMode">Supported timing model used to predict the behaviour commit.</param>
    /// <param name="useOverrideVisualSettings">True when the interaction-specific override provided the baked settings.</param>
    /// <param name="settings">Resolved authored settings block.</param>
    /// <param name="config">Output baked offensive engagement config.</param>
    /// <returns>True when the settings expose at least one visible feedback channel.</returns>
    private static bool TryCreateConfig(EnemyOffensiveEngagementTriggerSource source,
                                        EnemyOffensiveEngagementTimingMode timingMode,
                                        bool useOverrideVisualSettings,
                                        EnemyOffensiveEngagementFeedbackSettings settings,
                                        out EnemyOffensiveEngagementConfigElement config)
    {
        config = default;

        if (settings == null)
            settings = DefaultSettings;

        bool hasVisibleChannel = settings.EnableColorBlend || settings.EnableBillboard;

        if (!hasVisibleChannel)
            return false;

        Vector3 billboardOffset = settings.BillboardLocalOffset;

        config = new EnemyOffensiveEngagementConfigElement
        {
            Source = source,
            TimingMode = timingMode,
            VisualSettingsKey = -1,
            UseOverrideVisualSettings = useOverrideVisualSettings ? (byte)1 : (byte)0,
            EnableColorBlend = settings.EnableColorBlend ? (byte)1 : (byte)0,
            ColorBlendColor = DamageFlashRuntimeUtility.ToLinearFloat4(settings.ColorBlendColor),
            ColorBlendLeadTimeSeconds = math.max(0f, settings.ColorBlendLeadTimeSeconds),
            ColorBlendFadeOutSeconds = math.max(0f, settings.ColorBlendFadeOutSeconds),
            ColorBlendMaximumBlend = math.saturate(settings.ColorBlendMaximumBlend),
            EnableBillboard = settings.EnableBillboard ? (byte)1 : (byte)0,
            BillboardColor = DamageFlashRuntimeUtility.ToLinearFloat4(settings.BillboardColor),
            BillboardOffset = new float3(billboardOffset.x, billboardOffset.y, billboardOffset.z),
            BillboardLeadTimeSeconds = math.max(0f, settings.BillboardLeadTimeSeconds),
            BillboardBaseScale = math.max(0f, settings.BillboardBaseScale),
            BillboardPulseScaleMultiplier = math.max(0f, settings.BillboardPulseScaleMultiplier),
            BillboardPulseExpandDurationSeconds = math.max(0f, settings.BillboardPulseExpandDurationSeconds),
            BillboardPulseContractDurationSeconds = math.max(0f, settings.BillboardPulseContractDurationSeconds)
        };
        return true;
    }
    #endregion

    #endregion
}
