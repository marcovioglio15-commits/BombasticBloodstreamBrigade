using System.Collections.Generic;

/// <summary>
/// Resolves boss mixed-pattern and module-candidate offensive engagement overrides from the same authored ordering used by baking.
/// </summary>
internal static class EnemyOffensiveEngagementBossAuthoringResolverUtility
{
    #region Methods

    #region Internal Methods
    /// <summary>
    /// Resolves the active boss candidate override first and retains the owning mixed-pattern override as its sprite fallback.
    /// </summary>
    /// <param name="bossInteractions">Authored boss mixed-pattern interactions in compiled candidate order.</param>
    /// <param name="source">Interaction source currently requesting offensive engagement presentation.</param>
    /// <param name="visualSettingsKey">Compiled candidate index baked into the active boss configuration.</param>
    /// <param name="inheritedBossPatternSettings">Owning mixed-pattern settings retained below a candidate-specific override.</param>
    /// <returns>Candidate settings, mixed-pattern settings, or null when global settings remain authoritative.</returns>
    internal static EnemyOffensiveEngagementFeedbackSettings ResolveOverrideSettings(
        IReadOnlyList<EnemyBossPatternInteractionDefinition> bossInteractions,
        EnemyOffensiveEngagementTriggerSource source,
        int visualSettingsKey,
        out EnemyOffensiveEngagementFeedbackSettings inheritedBossPatternSettings)
    {
        inheritedBossPatternSettings = null;

        // Reject keys that cannot map to one compiled boss candidate.
        if (bossInteractions == null || visualSettingsKey < 0)
            return null;

        int candidateCursor = 0;

        // Walk enabled mixed patterns in the same deterministic order used by boss baking.
        for (int interactionIndex = 0; interactionIndex < bossInteractions.Count; interactionIndex++)
        {
            EnemyBossPatternInteractionDefinition interaction = bossInteractions[interactionIndex];

            if (interaction == null || !interaction.Enabled)
                continue;

            if (TryResolveCoreCandidateOverride(interaction.CoreMovementExtraction,
                                                source,
                                                visualSettingsKey,
                                                ref candidateCursor,
                                                out EnemyOffensiveEngagementFeedbackSettings coreSettings))
                return ResolveCandidateOrPatternOverride(coreSettings,
                                                         interaction,
                                                         out inheritedBossPatternSettings);

            if (TryResolveShortRangeCandidateOverride(interaction.ShortRangeExtraction,
                                                      source,
                                                      visualSettingsKey,
                                                      ref candidateCursor,
                                                      out EnemyOffensiveEngagementFeedbackSettings shortRangeSettings))
                return ResolveCandidateOrPatternOverride(shortRangeSettings,
                                                         interaction,
                                                         out inheritedBossPatternSettings);

            if (TryResolveWeaponCandidateOverride(interaction.WeaponExtraction,
                                                  source,
                                                  visualSettingsKey,
                                                  ref candidateCursor,
                                                  out EnemyOffensiveEngagementFeedbackSettings weaponSettings))
                return ResolveCandidateOrPatternOverride(weaponSettings,
                                                         interaction,
                                                         out inheritedBossPatternSettings);
        }

        return null;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Prioritizes candidate-specific settings while preserving the owning mixed-pattern settings as a managed sprite fallback.
    /// </summary>
    /// <param name="candidateSettings">Candidate-specific settings resolved for the active module slot.</param>
    /// <param name="interaction">Owning mixed boss pattern.</param>
    /// <param name="inheritedBossPatternSettings">Pattern settings retained below a candidate-specific override.</param>
    /// <returns>Candidate settings, pattern settings, or null when global settings remain authoritative.</returns>
    private static EnemyOffensiveEngagementFeedbackSettings ResolveCandidateOrPatternOverride(
        EnemyOffensiveEngagementFeedbackSettings candidateSettings,
        EnemyBossPatternInteractionDefinition interaction,
        out EnemyOffensiveEngagementFeedbackSettings inheritedBossPatternSettings)
    {
        inheritedBossPatternSettings = ResolvePatternOverrideSettings(interaction);

        if (candidateSettings != null)
            return candidateSettings;

        EnemyOffensiveEngagementFeedbackSettings resolvedSettings = inheritedBossPatternSettings;
        inheritedBossPatternSettings = null;
        return resolvedSettings;
    }

    /// <summary>
    /// Resolves the boss-only offensive engagement settings inherited by active module candidates in one mixed pattern.
    /// </summary>
    /// <param name="interaction">Mixed boss pattern to inspect.</param>
    /// <returns>Boss-only pattern override settings, or null when global settings remain authoritative.</returns>
    private static EnemyOffensiveEngagementFeedbackSettings ResolvePatternOverrideSettings(
        EnemyBossPatternInteractionDefinition interaction)
    {
        if (interaction == null || !interaction.UseEngagementFeedbackOverride)
            return null;

        return interaction.EngagementFeedbackOverride;
    }

    /// <summary>
    /// Resolves an offensive engagement override from one Core Movement module candidate.
    /// </summary>
    /// <param name="extraction">Core Movement extraction definition to inspect.</param>
    /// <param name="source">Current billboard trigger source.</param>
    /// <param name="visualSettingsKey">Compiled module candidate index baked into the active configuration.</param>
    /// <param name="candidateCursor">Mutable cursor matching the compiled candidate ordering.</param>
    /// <param name="settings">Candidate-specific settings when the key and source match.</param>
    /// <returns>True when the baked key maps to this extraction list.</returns>
    private static bool TryResolveCoreCandidateOverride(
        EnemyBossPatternCoreMovementExtractionDefinition extraction,
        EnemyOffensiveEngagementTriggerSource source,
        int visualSettingsKey,
        ref int candidateCursor,
        out EnemyOffensiveEngagementFeedbackSettings settings)
    {
        settings = null;
        IReadOnlyList<EnemyBossPatternCoreMovementModuleCandidateDefinition> candidates = extraction != null
            ? extraction.Candidates
            : null;

        if (candidates == null)
            return false;

        // Advance only through candidates accepted by the shared compilation contract.
        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            EnemyBossPatternCoreMovementModuleCandidateDefinition candidate = candidates[candidateIndex];

            if (candidate == null ||
                !EnemyBossPatternCandidateCompilationUtility.CanCompile(candidate.Eligibility,
                                                                        candidate.ModuleMode,
                                                                        candidate.Binding != null && candidate.Binding.IsEnabled))
                continue;

            if (candidateCursor == visualSettingsKey)
            {
                settings = source == EnemyOffensiveEngagementTriggerSource.CoreMovement
                    ? ResolveCoreMovementOverrideSettings(candidate)
                    : null;
                return true;
            }

            candidateCursor++;
        }

        return false;
    }

    /// <summary>
    /// Resolves an offensive engagement override from one Short-Range module candidate.
    /// </summary>
    /// <param name="extraction">Short-Range extraction definition to inspect.</param>
    /// <param name="source">Current billboard trigger source.</param>
    /// <param name="visualSettingsKey">Compiled module candidate index baked into the active configuration.</param>
    /// <param name="candidateCursor">Mutable cursor matching the compiled candidate ordering.</param>
    /// <param name="settings">Candidate-specific settings when the key and source match.</param>
    /// <returns>True when the baked key maps to this extraction list.</returns>
    private static bool TryResolveShortRangeCandidateOverride(
        EnemyBossPatternShortRangeExtractionDefinition extraction,
        EnemyOffensiveEngagementTriggerSource source,
        int visualSettingsKey,
        ref int candidateCursor,
        out EnemyOffensiveEngagementFeedbackSettings settings)
    {
        settings = null;
        IReadOnlyList<EnemyBossPatternShortRangeModuleCandidateDefinition> candidates = extraction != null
            ? extraction.Candidates
            : null;

        if (candidates == null)
            return false;

        // Advance only through candidates accepted by the shared compilation contract.
        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            EnemyBossPatternShortRangeModuleCandidateDefinition candidate = candidates[candidateIndex];

            if (candidate == null ||
                !EnemyBossPatternCandidateCompilationUtility.CanCompile(candidate.Eligibility,
                                                                        candidate.ModuleMode,
                                                                        candidate.Interaction != null &&
                                                                        candidate.Interaction.IsEnabled &&
                                                                        candidate.Interaction.Binding != null &&
                                                                        candidate.Interaction.Binding.IsEnabled))
                continue;

            if (candidateCursor == visualSettingsKey)
            {
                settings = source == EnemyOffensiveEngagementTriggerSource.ShortRangeInteraction
                    ? ResolveShortRangeOverrideSettings(candidate.Interaction)
                    : null;
                return true;
            }

            candidateCursor++;
        }

        return false;
    }

    /// <summary>
    /// Resolves an offensive engagement override from one Weapon module candidate.
    /// </summary>
    /// <param name="extraction">Weapon extraction definition to inspect.</param>
    /// <param name="source">Current billboard trigger source.</param>
    /// <param name="visualSettingsKey">Compiled module candidate index baked into the active configuration.</param>
    /// <param name="candidateCursor">Mutable cursor matching the compiled candidate ordering.</param>
    /// <param name="settings">Candidate-specific settings when the key and source match.</param>
    /// <returns>True when the baked key maps to this extraction list.</returns>
    private static bool TryResolveWeaponCandidateOverride(
        EnemyBossPatternWeaponExtractionDefinition extraction,
        EnemyOffensiveEngagementTriggerSource source,
        int visualSettingsKey,
        ref int candidateCursor,
        out EnemyOffensiveEngagementFeedbackSettings settings)
    {
        settings = null;
        IReadOnlyList<EnemyBossPatternWeaponModuleCandidateDefinition> candidates = extraction != null
            ? extraction.Candidates
            : null;

        if (candidates == null)
            return false;

        // Advance only through candidates accepted by the shared compilation contract.
        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            EnemyBossPatternWeaponModuleCandidateDefinition candidate = candidates[candidateIndex];

            if (candidate == null ||
                !EnemyBossPatternCandidateCompilationUtility.CanCompile(candidate.Eligibility,
                                                                        candidate.ModuleMode,
                                                                        candidate.Interaction != null &&
                                                                        candidate.Interaction.IsEnabled &&
                                                                        candidate.Interaction.Binding != null &&
                                                                        candidate.Interaction.Binding.IsEnabled))
                continue;

            if (candidateCursor == visualSettingsKey)
            {
                settings = source == EnemyOffensiveEngagementTriggerSource.WeaponInteraction
                    ? ResolveWeaponOverrideSettings(candidate.Interaction)
                    : null;
                return true;
            }

            candidateCursor++;
        }

        return false;
    }

    /// <summary>
    /// Resolves an authored Short-Range candidate override when its interaction is active and override-enabled.
    /// </summary>
    /// <param name="interaction">Short-Range interaction to inspect.</param>
    /// <returns>Override settings, or null when the candidate should inherit its mixed-pattern or global settings.</returns>
    private static EnemyOffensiveEngagementFeedbackSettings ResolveShortRangeOverrideSettings(
        EnemyPatternShortRangeInteractionAssembly interaction)
    {
        if (interaction == null || !interaction.IsEnabled || !interaction.UseEngagementFeedbackOverride)
            return null;

        return interaction.EngagementFeedbackOverride;
    }

    /// <summary>
    /// Resolves an authored Core Movement candidate override when the candidate represents an active module and enables it.
    /// </summary>
    /// <param name="candidate">Core Movement candidate to inspect.</param>
    /// <returns>Override settings, or null when the candidate should inherit its mixed-pattern or global settings.</returns>
    private static EnemyOffensiveEngagementFeedbackSettings ResolveCoreMovementOverrideSettings(
        EnemyBossPatternCoreMovementModuleCandidateDefinition candidate)
    {
        if (candidate == null ||
            candidate.ModuleMode == EnemyBossPatternModuleMode.NullModule ||
            !candidate.UseEngagementFeedbackOverride)
            return null;

        return candidate.EngagementFeedbackOverride;
    }

    /// <summary>
    /// Resolves an authored Weapon candidate override when its interaction is active and override-enabled.
    /// </summary>
    /// <param name="interaction">Weapon interaction to inspect.</param>
    /// <returns>Override settings, or null when the candidate should inherit its mixed-pattern or global settings.</returns>
    private static EnemyOffensiveEngagementFeedbackSettings ResolveWeaponOverrideSettings(
        EnemyPatternWeaponInteractionAssembly interaction)
    {
        if (interaction == null || !interaction.IsEnabled || !interaction.UseEngagementFeedbackOverride)
            return null;

        return interaction.EngagementFeedbackOverride;
    }
    #endregion

    #endregion
}
