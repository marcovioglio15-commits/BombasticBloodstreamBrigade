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
            settings = DefaultSettings;

        Vector3 billboardOffset = ResolveFiniteVector3(settings.BillboardWorldOffset,
                                                       DefaultSettings.BillboardWorldOffset);
        bool hasVisibleChannel = settings.EnableColorBlend || settings.EnableBillboard;

        return new EnemyBossPatternChangeFeedbackConfig
        {
            Enabled = hasVisibleChannel ? (byte)1 : (byte)0,
            EnableColorBlend = settings.EnableColorBlend ? (byte)1 : (byte)0,
            ColorBlendColor = DamageFlashRuntimeUtility.ToLinearFloat4(ResolveFiniteColor(settings.ColorBlendColor,
                                                                                          DefaultSettings.ColorBlendColor)),
            ColorBlendDurationSeconds = ResolveNonNegativeFinite(settings.ColorBlendLeadTimeSeconds,
                                                                 DefaultSettings.ColorBlendLeadTimeSeconds),
            ColorBlendFadeOutSeconds = ResolveNonNegativeFinite(settings.ColorBlendFadeOutSeconds,
                                                                DefaultSettings.ColorBlendFadeOutSeconds),
            ColorBlendMaximumBlend = ResolveSaturatedFinite(settings.ColorBlendMaximumBlend,
                                                            DefaultSettings.ColorBlendMaximumBlend),
            EnableBillboard = settings.EnableBillboard ? (byte)1 : (byte)0,
            BillboardColor = DamageFlashRuntimeUtility.ToLinearFloat4(ResolveFiniteColor(settings.BillboardColor,
                                                                                         DefaultSettings.BillboardColor)),
            BillboardOffset = new float3(billboardOffset.x, billboardOffset.y, billboardOffset.z),
            BillboardDurationSeconds = ResolveNonNegativeFinite(settings.BillboardLeadTimeSeconds,
                                                                DefaultSettings.BillboardLeadTimeSeconds),
            BillboardBaseScale = ResolveNonNegativeFinite(settings.BillboardBaseScale,
                                                          DefaultSettings.BillboardBaseScale),
            BillboardPulseScaleMultiplier = ResolveNonNegativeFinite(settings.BillboardPulseScaleMultiplier,
                                                                      DefaultSettings.BillboardPulseScaleMultiplier),
            BillboardPulseExpandDurationSeconds = ResolveNonNegativeFinite(settings.BillboardPulseExpandDurationSeconds,
                                                                            DefaultSettings.BillboardPulseExpandDurationSeconds),
            BillboardPulseContractDurationSeconds = ResolveNonNegativeFinite(settings.BillboardPulseContractDurationSeconds,
                                                                              DefaultSettings.BillboardPulseContractDurationSeconds)
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
                              candidate.PreventWarningInterruption,
                              candidate.EngagementFeedbackOverride,
                              EnemyPatternModuleCatalogSection.CoreMovement,
                              EnemyOffensiveEngagementTriggerSource.CoreMovement,
                              EnemyOffensiveEngagementTimingContext.BossMixedPattern,
                              out config);
    }

    /// <summary>
    /// Builds one short-range offensive engagement config from an explicit pattern assemble slot.
    /// </summary>
    /// <param name="interaction">Short-range interaction slot being compiled.</param>
    /// <param name="sharedPreset">Shared source preset used to resolve the selected module kind.</param>
    /// <param name="globalSettings">Generic visual feedback settings resolved from the visual preset.</param>
    /// <param name="timingContext">Runtime pattern owner available to evaluate the warning timing.</param>
    /// <param name="config">Output baked offensive engagement config.</param>
    /// <returns>True when the slot exposes a timing hook supported by the requested runtime context.</returns>
    internal static bool TryBuildShortRangeConfig(EnemyPatternShortRangeInteractionAssembly interaction,
                                                  EnemyModulesAndPatternsPreset sharedPreset,
                                                  EnemyOffensiveEngagementFeedbackSettings globalSettings,
                                                  EnemyOffensiveEngagementTimingContext timingContext,
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
                              interaction.PreventWarningInterruption,
                              interaction.EngagementFeedbackOverride,
                              EnemyPatternModuleCatalogSection.ShortRangeInteraction,
                              EnemyOffensiveEngagementTriggerSource.ShortRangeInteraction,
                              timingContext,
                              out config);
    }

    /// <summary>
    /// Builds one weapon offensive engagement config from an explicit pattern assemble slot.
    /// </summary>
    /// <param name="interaction">Weapon interaction slot being compiled.</param>
    /// <param name="sharedPreset">Shared source preset used to resolve the selected module kind.</param>
    /// <param name="globalSettings">Generic visual feedback settings resolved from the visual preset.</param>
    /// <param name="timingContext">Runtime pattern owner available to evaluate the warning timing.</param>
    /// <param name="config">Output baked offensive engagement config.</param>
    /// <returns>True when the slot exposes a timing hook supported by the requested runtime context.</returns>
    internal static bool TryBuildWeaponConfig(EnemyPatternWeaponInteractionAssembly interaction,
                                              EnemyModulesAndPatternsPreset sharedPreset,
                                              EnemyOffensiveEngagementFeedbackSettings globalSettings,
                                              EnemyOffensiveEngagementTimingContext timingContext,
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
                              interaction.PreventWarningInterruption,
                              interaction.EngagementFeedbackOverride,
                              EnemyPatternModuleCatalogSection.WeaponInteraction,
                              EnemyOffensiveEngagementTriggerSource.WeaponInteraction,
                              timingContext,
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

        if (!TryBuildShortRangeConfig(interaction,
                                      sharedPreset,
                                      globalSettings,
                                      EnemyOffensiveEngagementTimingContext.SharedPattern,
                                      out EnemyOffensiveEngagementConfigElement config))
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

        if (!TryBuildWeaponConfig(interaction,
                                  sharedPreset,
                                  globalSettings,
                                  EnemyOffensiveEngagementTimingContext.SharedPattern,
                                  out EnemyOffensiveEngagementConfigElement config))
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
    /// <param name="preventWarningInterruption">True when this behaviour must retain presentation ownership for its active warning window.</param>
    /// <param name="overrideSettings">Optional interaction-specific override settings.</param>
    /// <param name="section">Source module catalog section.</param>
    /// <param name="source">Interaction source currently being compiled.</param>
    /// <param name="timingContext">Runtime pattern owner available to evaluate the warning timing.</param>
    /// <param name="config">Output baked offensive engagement config.</param>
    /// <returns>True when a supported and visible config was produced.</returns>
    private static bool TryBuildConfig(EnemyPatternModuleBinding binding,
                                       EnemyModulesAndPatternsPreset sharedPreset,
                                       EnemyOffensiveEngagementFeedbackSettings globalSettings,
                                       bool useOverrideSettings,
                                       bool preventWarningInterruption,
                                       EnemyOffensiveEngagementFeedbackSettings overrideSettings,
                                       EnemyPatternModuleCatalogSection section,
                                       EnemyOffensiveEngagementTriggerSource source,
                                       EnemyOffensiveEngagementTimingContext timingContext,
                                       out EnemyOffensiveEngagementConfigElement config)
    {
        config = default;

        if (binding == null || !binding.IsEnabled || sharedPreset == null)
            return false;

        EnemyPatternModuleDefinition moduleDefinition = sharedPreset.ResolveModuleDefinitionById(binding.ModuleId);

        if (moduleDefinition == null)
            return false;

        EnemyOffensiveEngagementTimingMode timingMode = EnemyOffensiveEngagementSupportUtility.ResolveTimingMode(section,
                                                                                                                 moduleDefinition.ModuleKind,
                                                                                                                 timingContext);

        if (timingMode == EnemyOffensiveEngagementTimingMode.None)
            return false;

        EnemyOffensiveEngagementFeedbackSettings settings = ResolveSettings(globalSettings,
                                                                           useOverrideSettings,
                                                                           overrideSettings);
        return TryCreateConfig(source,
                               timingMode,
                               useOverrideSettings,
                               preventWarningInterruption,
                               settings,
                               out config);
    }

    /// <summary>
    /// Converts one authored settings block into a baked offensive engagement buffer entry.
    /// </summary>
    /// <param name="source">Interaction source currently being compiled.</param>
    /// <param name="timingMode">Supported timing model used for a predictive commit or boss-owned activation window.</param>
    /// <param name="useOverrideVisualSettings">True when the interaction-specific override provided the baked settings.</param>
    /// <param name="preventWarningInterruption">True when other module warnings cannot replace this config while its window remains active.</param>
    /// <param name="settings">Resolved authored settings block.</param>
    /// <param name="config">Output baked offensive engagement config.</param>
    /// <returns>True when the settings expose at least one visible feedback channel.</returns>
    private static bool TryCreateConfig(EnemyOffensiveEngagementTriggerSource source,
                                        EnemyOffensiveEngagementTimingMode timingMode,
                                        bool useOverrideVisualSettings,
                                        bool preventWarningInterruption,
                                        EnemyOffensiveEngagementFeedbackSettings settings,
                                        out EnemyOffensiveEngagementConfigElement config)
    {
        config = default;

        if (settings == null)
            settings = DefaultSettings;

        bool hasVisibleChannel = settings.EnableColorBlend || settings.EnableBillboard;

        if (!hasVisibleChannel)
            return false;

        Vector3 billboardOffset = ResolveFiniteVector3(settings.BillboardWorldOffset,
                                                       DefaultSettings.BillboardWorldOffset);

        config = new EnemyOffensiveEngagementConfigElement
        {
            Source = source,
            TimingMode = timingMode,
            VisualSettingsKey = -1,
            UseOverrideVisualSettings = useOverrideVisualSettings ? (byte)1 : (byte)0,
            PreventWarningInterruption = preventWarningInterruption ? (byte)1 : (byte)0,
            EnableColorBlend = settings.EnableColorBlend ? (byte)1 : (byte)0,
            ColorBlendColor = DamageFlashRuntimeUtility.ToLinearFloat4(ResolveFiniteColor(settings.ColorBlendColor,
                                                                                          DefaultSettings.ColorBlendColor)),
            ColorBlendLeadTimeSeconds = ResolveNonNegativeFinite(settings.ColorBlendLeadTimeSeconds,
                                                                 DefaultSettings.ColorBlendLeadTimeSeconds),
            ColorBlendFadeOutSeconds = ResolveNonNegativeFinite(settings.ColorBlendFadeOutSeconds,
                                                                DefaultSettings.ColorBlendFadeOutSeconds),
            ColorBlendMaximumBlend = ResolveSaturatedFinite(settings.ColorBlendMaximumBlend,
                                                            DefaultSettings.ColorBlendMaximumBlend),
            EnableBillboard = settings.EnableBillboard ? (byte)1 : (byte)0,
            BillboardColor = DamageFlashRuntimeUtility.ToLinearFloat4(ResolveFiniteColor(settings.BillboardColor,
                                                                                         DefaultSettings.BillboardColor)),
            BillboardOffset = new float3(billboardOffset.x, billboardOffset.y, billboardOffset.z),
            BillboardLeadTimeSeconds = ResolveNonNegativeFinite(settings.BillboardLeadTimeSeconds,
                                                                DefaultSettings.BillboardLeadTimeSeconds),
            BillboardBaseScale = ResolveNonNegativeFinite(settings.BillboardBaseScale,
                                                          DefaultSettings.BillboardBaseScale),
            BillboardPulseScaleMultiplier = ResolveNonNegativeFinite(settings.BillboardPulseScaleMultiplier,
                                                                      DefaultSettings.BillboardPulseScaleMultiplier),
            BillboardPulseExpandDurationSeconds = ResolveNonNegativeFinite(settings.BillboardPulseExpandDurationSeconds,
                                                                            DefaultSettings.BillboardPulseExpandDurationSeconds),
            BillboardPulseContractDurationSeconds = ResolveNonNegativeFinite(settings.BillboardPulseContractDurationSeconds,
                                                                              DefaultSettings.BillboardPulseContractDurationSeconds)
        };
        return true;
    }

    /// <summary>
    /// Resolves a finite color for ECS conversion without mutating the authored settings object.
    /// </summary>
    /// <param name="value">Authored color to inspect.</param>
    /// <param name="fallback">Canonical color used when any channel is not finite.</param>
    /// <returns>The authored color when finite; otherwise the canonical fallback.</returns>
    private static Color ResolveFiniteColor(Color value, Color fallback)
    {
        if (!float.IsFinite(value.r) ||
            !float.IsFinite(value.g) ||
            !float.IsFinite(value.b) ||
            !float.IsFinite(value.a))
            return fallback;

        return value;
    }

    /// <summary>
    /// Resolves a finite world offset for ECS conversion without allowing invalid components to poison transforms.
    /// </summary>
    /// <param name="value">Authored world-space offset to inspect.</param>
    /// <param name="fallback">Canonical offset used when any component is not finite.</param>
    /// <returns>The authored offset when finite; otherwise the canonical fallback.</returns>
    private static Vector3 ResolveFiniteVector3(Vector3 value, Vector3 fallback)
    {
        if (!float.IsFinite(value.x) ||
            !float.IsFinite(value.y) ||
            !float.IsFinite(value.z))
            return fallback;

        return value;
    }

    /// <summary>
    /// Resolves a non-negative finite runtime value while preserving invalid authored data for editor correction.
    /// </summary>
    /// <param name="value">Authored value to convert.</param>
    /// <param name="fallback">Canonical finite value used when the authored value is NaN or infinity.</param>
    /// <returns>A finite runtime value clamped to zero or above.</returns>
    private static float ResolveNonNegativeFinite(float value, float fallback)
    {
        if (!float.IsFinite(value))
            value = fallback;

        return math.max(0f, value);
    }

    /// <summary>
    /// Resolves a finite normalized runtime value while preserving invalid authored data for editor correction.
    /// </summary>
    /// <param name="value">Authored value to convert.</param>
    /// <param name="fallback">Canonical finite value used when the authored value is NaN or infinity.</param>
    /// <returns>A finite runtime value clamped to the inclusive zero-to-one range.</returns>
    private static float ResolveSaturatedFinite(float value, float fallback)
    {
        if (!float.IsFinite(value))
            value = fallback;

        return math.saturate(value);
    }
    #endregion

    #endregion
}
