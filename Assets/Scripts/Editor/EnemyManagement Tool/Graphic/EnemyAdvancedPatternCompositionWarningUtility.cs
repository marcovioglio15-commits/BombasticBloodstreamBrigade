using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds warning messages for unsupported or ambiguous advanced-pattern compositions without mutating authoring data.
/// </summary>
internal static class EnemyAdvancedPatternCompositionWarningUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Appends composition warnings for the selected preset to the provided container.
    /// </summary>
    /// <param name="panel">Owning panel that provides the selected preset.</param>
    /// <param name="container">Target UI container that receives generated warning boxes.</param>
    public static void AddWarnings(EnemyAdvancedPatternPresetsPanel panel, VisualElement container)
    {
        if (panel == null || container == null)
            return;

        EnemyAdvancedPatternPreset preset = panel.SelectedPreset;

        if (preset == null)
            return;

        HashSet<string> warnings = CollectWarnings(preset);

        foreach (string warning in warnings)
        {
            HelpBox warningBox = new HelpBox(warning, HelpBoxMessageType.Warning);
            warningBox.style.marginTop = 4f;
            container.Add(warningBox);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Collects all warnings implied by the current preset loadout source.
    /// </summary>
    /// <param name="preset">Selected advanced-pattern preset.</param>
    /// <returns>The distinct warning set.</returns>
    private static HashSet<string> CollectWarnings(EnemyAdvancedPatternPreset preset)
    {
        HashSet<string> warnings = new HashSet<string>();

        if (preset == null)
            return warnings;

        if (preset.ActivePatternIds != null && preset.ActivePatternIds.Count > 1)
        {
            warnings.Add("Pattern Loadout currently contains multiple active pattern IDs. The current workflow expects one active shared pattern at a time.");
        }

        if (preset.ModulesAndPatternsPreset != null)
        {
            IReadOnlyList<EnemyModulesPatternDefinition> sharedPatterns = preset.ModulesAndPatternsPreset.Patterns;

            for (int index = 0; index < sharedPatterns.Count; index++)
                AnalyzeSharedPattern(preset, sharedPatterns[index], warnings);

            return warnings;
        }

        IReadOnlyList<EnemyPatternDefinition> legacyPatterns = preset.LegacyPatterns;

        for (int index = 0; index < legacyPatterns.Count; index++)
            AnalyzeLegacyPattern(preset, legacyPatterns[index], warnings);

        return warnings;
    }

    /// <summary>
    /// Analyzes one shared assembled pattern against the new category model.
    /// </summary>
    /// <param name="preset">Selected advanced-pattern preset.</param>
    /// <param name="pattern">Shared assembled pattern to inspect.</param>
    /// <param name="warnings">Mutable warning set.</param>
    private static void AnalyzeSharedPattern(EnemyAdvancedPatternPreset preset,
                                             EnemyModulesPatternDefinition pattern,
                                             HashSet<string> warnings)
    {
        if (preset == null || pattern == null || warnings == null)
            return;

        string patternName = string.IsNullOrWhiteSpace(pattern.DisplayName) ? pattern.PatternId : pattern.DisplayName;
        EnemyPatternCoreMovementAssembly coreMovement = pattern.CoreMovement;

        if (coreMovement == null || coreMovement.Binding == null || !coreMovement.Binding.IsEnabled)
        {
            warnings.Add(string.Format("Shared pattern '{0}' is missing an enabled Core Movement module.", patternName));
        }
        else
        {
            EnemyPatternModuleDefinition coreDefinition = preset.ResolveModuleDefinitionById(coreMovement.Binding.ModuleId);

            if (coreDefinition == null)
            {
                warnings.Add(string.Format("Shared pattern '{0}' references a Core Movement module ID that cannot be resolved.", patternName));
            }
            else if (!EnemyAdvancedPatternDrawerUtility.IsModuleKindAllowedInCatalogSection(coreDefinition.ModuleKind, EnemyPatternModuleCatalogSection.CoreMovement))
            {
                warnings.Add(string.Format("Shared pattern '{0}' assigns '{1}' to Core Movement, but that module kind does not belong to the Core Movement category.", patternName, coreDefinition.ModuleKind));
            }
        }

        AnalyzeOptionalCategory(patternName,
                                pattern.ShortRangeInteraction != null && pattern.ShortRangeInteraction.IsEnabled,
                                pattern.ShortRangeInteraction != null ? pattern.ShortRangeInteraction.Binding : null,
                                EnemyPatternModuleCatalogSection.ShortRangeInteraction,
                                preset,
                                warnings,
                                "Short-Range Interaction");
        AnalyzeOffensiveEngagementTrigger(patternName,
                                          pattern.ShortRangeInteraction != null && pattern.ShortRangeInteraction.IsEnabled,
                                          pattern.ShortRangeInteraction != null && pattern.ShortRangeInteraction.DisplayBehaviourEngagementTrigger,
                                          pattern.ShortRangeInteraction != null ? pattern.ShortRangeInteraction.Binding : null,
                                          EnemyPatternModuleCatalogSection.ShortRangeInteraction,
                                          preset,
                                          warnings,
                                          "Short-Range Interaction");
        AnalyzeOptionalCategory(patternName,
                                pattern.WeaponInteraction != null && pattern.WeaponInteraction.IsEnabled,
                                pattern.WeaponInteraction != null ? pattern.WeaponInteraction.Binding : null,
                                EnemyPatternModuleCatalogSection.WeaponInteraction,
                                preset,
                                warnings,
                                "Weapon Interaction");
        AnalyzeOffensiveEngagementTrigger(patternName,
                                          pattern.WeaponInteraction != null && pattern.WeaponInteraction.IsEnabled,
                                          pattern.WeaponInteraction != null && pattern.WeaponInteraction.DisplayBehaviourEngagementTrigger,
                                          pattern.WeaponInteraction != null ? pattern.WeaponInteraction.Binding : null,
                                          EnemyPatternModuleCatalogSection.WeaponInteraction,
                                          preset,
                                          warnings,
                                          "Weapon Interaction");
        AnalyzeDropItemsCategory(patternName,
                                 pattern.DropItems,
                                 preset,
                                 warnings);
    }

    /// <summary>
    /// Adds warnings for one optional shared pattern category.
    /// </summary>
    /// <param name="patternName">Display name of the analyzed pattern.</param>
    /// <param name="categoryEnabled">Whether the category is enabled.</param>
    /// <param name="binding">Optional category binding.</param>
    /// <param name="section">Expected catalog section for the category.</param>
    /// <param name="preset">Selected advanced-pattern preset.</param>
    /// <param name="warnings">Mutable warning set.</param>
    /// <param name="categoryLabel">Display label of the category.</param>
    private static void AnalyzeOptionalCategory(string patternName,
                                                bool categoryEnabled,
                                                EnemyPatternModuleBinding binding,
                                                EnemyPatternModuleCatalogSection section,
                                                EnemyAdvancedPatternPreset preset,
                                                HashSet<string> warnings,
                                                string categoryLabel)
    {
        if (!categoryEnabled)
            return;

        if (binding == null || !binding.IsEnabled)
        {
            warnings.Add(string.Format("Shared pattern '{0}' enables {1} but no enabled module binding is assigned.", patternName, categoryLabel));
            return;
        }

        EnemyPatternModuleDefinition definition = preset.ResolveModuleDefinitionById(binding.ModuleId);

        if (definition == null)
        {
            warnings.Add(string.Format("Shared pattern '{0}' enables {1} but its module ID cannot be resolved.", patternName, categoryLabel));
            return;
        }

        if (!EnemyAdvancedPatternDrawerUtility.IsModuleKindAllowedInCatalogSection(definition.ModuleKind, section))
        {
            warnings.Add(string.Format("Shared pattern '{0}' enables {1} with module kind '{2}', which does not belong to that category.", patternName, categoryLabel, definition.ModuleKind));
        }
    }

    /// <summary>
    /// Adds warnings for enabled engagement triggers that currently point to unsupported predictive module kinds.
    /// </summary>
    /// <param name="patternName">Display name of the analyzed pattern.</param>
    /// <param name="categoryEnabled">Whether the owning category is enabled.</param>
    /// <param name="triggerEnabled">Whether the engagement trigger is enabled for that category.</param>
    /// <param name="binding">Optional category binding.</param>
    /// <param name="section">Expected catalog section for the category.</param>
    /// <param name="preset">Selected advanced-pattern preset.</param>
    /// <param name="warnings">Mutable warning set.</param>
    /// <param name="categoryLabel">Display label of the category.</param>
    private static void AnalyzeOffensiveEngagementTrigger(string patternName,
                                                          bool categoryEnabled,
                                                          bool triggerEnabled,
                                                          EnemyPatternModuleBinding binding,
                                                          EnemyPatternModuleCatalogSection section,
                                                          EnemyAdvancedPatternPreset preset,
                                                          HashSet<string> warnings,
                                                          string categoryLabel)
    {
        if (!categoryEnabled || !triggerEnabled || binding == null || !binding.IsEnabled || preset == null)
        {
            return;
        }

        EnemyPatternModuleDefinition definition = preset.ResolveModuleDefinitionById(binding.ModuleId);

        if (definition == null)
        {
            return;
        }

        if (EnemyOffensiveEngagementSupportUtility.SupportsTimingMode(section, definition.ModuleKind))
        {
            return;
        }

        warnings.Add(string.Format("Shared pattern '{0}' enables Display Behaviour Engagement Trigger on {1}, but module kind '{2}' does not currently expose a predictive offensive timing hook in that slot.",
                                   patternName,
                                   categoryLabel,
                                   definition.ModuleKind));
    }

    /// <summary>
    /// Adds warnings for the shared Drop Items category, which now supports multiple module bindings.
    /// </summary>
    /// <param name="patternName">Display name of the analyzed pattern.</param>
    /// <param name="dropItemsAssembly">Shared Drop Items assembly to inspect.</param>
    /// <param name="preset">Selected advanced-pattern preset.</param>
    /// <param name="warnings">Mutable warning set.</param>
    private static void AnalyzeDropItemsCategory(string patternName,
                                                 EnemyPatternDropItemsAssembly dropItemsAssembly,
                                                 EnemyAdvancedPatternPreset preset,
                                                 HashSet<string> warnings)
    {
        if (dropItemsAssembly == null || !dropItemsAssembly.IsEnabled)
            return;

        IReadOnlyList<EnemyPatternModuleBinding> moduleBindings = dropItemsAssembly.Modules;
        bool hasEnabledModule = false;

        if (moduleBindings == null)
        {
            warnings.Add(string.Format("Shared pattern '{0}' enables Drop Items but no modules list is assigned.", patternName));
            return;
        }

        for (int moduleIndex = 0; moduleIndex < moduleBindings.Count; moduleIndex++)
        {
            EnemyPatternModuleBinding binding = moduleBindings[moduleIndex];

            if (binding == null || !binding.IsEnabled)
                continue;

            hasEnabledModule = true;
            EnemyPatternModuleDefinition definition = preset.ResolveModuleDefinitionById(binding.ModuleId);

            if (definition == null)
            {
                warnings.Add(string.Format("Shared pattern '{0}' enables Drop Items module #{1}, but its module ID cannot be resolved.", patternName, moduleIndex + 1));
                continue;
            }

            if (!EnemyAdvancedPatternDrawerUtility.IsModuleKindAllowedInCatalogSection(definition.ModuleKind, EnemyPatternModuleCatalogSection.DropItems))
            {
                warnings.Add(string.Format("Shared pattern '{0}' enables Drop Items module #{1} with module kind '{2}', which does not belong to that category.", patternName, moduleIndex + 1, definition.ModuleKind));
            }
        }

        if (!hasEnabledModule)
            warnings.Add(string.Format("Shared pattern '{0}' enables Drop Items but no enabled module binding is assigned.", patternName));
    }

    /// <summary>
    /// Analyzes one hidden legacy pattern for backward-compatible warnings.
    /// </summary>
    /// <param name="preset">Selected advanced-pattern preset.</param>
    /// <param name="pattern">Legacy pattern to inspect.</param>
    /// <param name="warnings">Mutable warning set.</param>
    private static void AnalyzeLegacyPattern(EnemyAdvancedPatternPreset preset,
                                             EnemyPatternDefinition pattern,
                                             HashSet<string> warnings)
    {
        if (preset == null || pattern == null || warnings == null)
            return;

        int baseMovementCount = 0;
        int shooterCount = 0;
        int dropItemsCount = 0;
        List<string> movementLabels = new List<string>(4);
        IReadOnlyList<EnemyPatternModuleBinding> bindings = pattern.ModuleBindings;

        if (bindings == null)
            return;

        for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
        {
            EnemyPatternModuleBinding binding = bindings[bindingIndex];

            if (binding == null || !binding.IsEnabled)
                continue;

            EnemyPatternModuleDefinition definition = preset.ResolveLegacyModuleDefinitionById(binding.ModuleId);

            if (definition == null)
                continue;

            EnemyPatternModulePayloadData payload = ResolvePayload(definition, binding);

            switch (definition.ModuleKind)
            {
                case EnemyPatternModuleKind.Stationary:
                    baseMovementCount++;
                    movementLabels.Add("Stationary");
                    break;

                case EnemyPatternModuleKind.Grunt:
                    baseMovementCount++;
                    movementLabels.Add("Grunt");
                    break;

                case EnemyPatternModuleKind.Wanderer:
                    baseMovementCount++;
                    movementLabels.Add(ResolveWandererLabel(payload));
                    break;

                case EnemyPatternModuleKind.Coward:
                    baseMovementCount++;
                    movementLabels.Add("Coward");
                    break;

                case EnemyPatternModuleKind.Shooter:
                    shooterCount++;
                    break;

                case EnemyPatternModuleKind.DropItems:
                    dropItemsCount++;
                    break;
            }
        }

        string patternName = string.IsNullOrWhiteSpace(pattern.DisplayName) ? pattern.PatternId : pattern.DisplayName;

        if (baseMovementCount <= 0)
        {
            warnings.Add(string.Format("Legacy pattern '{0}' does not contain any movement module. Runtime falls back to Grunt.", patternName));
        }

        if (baseMovementCount > 1)
        {
            warnings.Add(string.Format("Legacy pattern '{0}' still combines multiple movement modules ({1}). Rebuild it with one Core Movement plus optional Short-Range / Weapon categories.", patternName, string.Join(", ", movementLabels.ToArray())));
        }

        if (shooterCount > 1)
        {
            warnings.Add(string.Format("Legacy pattern '{0}' contains multiple Shooter modules. Shared Weapon Interaction expects one module slot.", patternName));
        }

    }

    /// <summary>
    /// Resolves the payload block used by one legacy binding after considering its optional override payload.
    /// </summary>
    /// <param name="definition">Source module definition.</param>
    /// <param name="binding">Source binding.</param>
    /// <returns>The effective payload block.</returns>
    private static EnemyPatternModulePayloadData ResolvePayload(EnemyPatternModuleDefinition definition, EnemyPatternModuleBinding binding)
    {
        if (binding != null && binding.UseOverridePayload && binding.OverridePayload != null)
            return binding.OverridePayload;

        if (definition != null)
            return definition.Data;

        return null;
    }

    /// <summary>
    /// Resolves a user-facing Wanderer label from the provided payload.
    /// </summary>
    /// <param name="payload">Effective payload block of the module.</param>
    /// <returns>The resolved Wanderer display label.</returns>
    private static string ResolveWandererLabel(EnemyPatternModulePayloadData payload)
    {
        if (payload == null || payload.Wanderer == null)
            return "Wanderer";

        if (payload.Wanderer.Mode == EnemyWandererMode.Dvd)
            return "DVD";

        return "Wanderer";
    }
    #endregion

    #endregion
}
