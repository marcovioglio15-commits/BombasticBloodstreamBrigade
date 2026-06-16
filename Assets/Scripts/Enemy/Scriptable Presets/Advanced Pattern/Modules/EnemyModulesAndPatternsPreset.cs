using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores reusable module catalogs and assembled patterns shared by multiple enemy advanced-pattern presets.
/// </summary>
[CreateAssetMenu(fileName = "EnemyModulesAndPatternsPreset", menuName = "Enemy/Modules And Patterns Preset", order = 11)]
public sealed class EnemyModulesAndPatternsPreset : ScriptableObject
{
    #region Constants
    private const string DefaultCoreGruntModuleId = "Module_Core_Grunt";
    private const string DefaultCoreStationaryModuleId = "Module_Core_Stationary";
    private const string DefaultCoreWandererModuleId = "Module_Core_Wanderer";
    private const string DefaultShortRangeGruntModuleId = "Module_ShortRange_Grunt";
    private const string DefaultShortRangeCowardModuleId = "Module_ShortRange_Coward";
    private const string DefaultShortRangeDashModuleId = "Module_ShortRange_Dash";
    private const string DefaultWeaponShooterModuleId = "Module_Weapon_Shooter";
    private const string DefaultWeaponBombardierModuleId = "Module_Weapon_Bombardier";
    private const string DefaultWeaponPowerUpStealerModuleId = "Module_Weapon_PowerUpStealer";
    private const string DefaultDropItemsModuleId = "Module_DropItems";
    private const string DefaultPatternId = "Pattern_DefaultGrunt";
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this shared modules and patterns preset.")]
    [SerializeField] private string presetId;

    [Tooltip(" preset name shown inside Enemy Management Tool.")]
    [SerializeField] private string presetName = "New Enemy Modules And Patterns Preset";

    [Tooltip("Short description of this shared modules and patterns preset.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this shared preset.")]
    [SerializeField] private string version = "1.0.0";

    [Header("Modules Definition")]
    [Tooltip("Reusable core movement modules selectable by shared pattern assemblies.")]
    [SerializeField] private List<EnemyPatternModuleDefinition> coreMovementDefinitions = new List<EnemyPatternModuleDefinition>();

    [Tooltip("Reusable short-range interaction modules selectable by shared pattern assemblies.")]
    [SerializeField] private List<EnemyPatternModuleDefinition> shortRangeInteractionDefinitions = new List<EnemyPatternModuleDefinition>();

    [Tooltip("Reusable weapon interaction modules selectable by shared pattern assemblies.")]
    [SerializeField] private List<EnemyPatternModuleDefinition> weaponInteractionDefinitions = new List<EnemyPatternModuleDefinition>();

    [Tooltip("Optional reusable drop-items modules preserved for loot-driven patterns.")]
    [SerializeField] private List<EnemyPatternModuleDefinition> dropItemsDefinitions = new List<EnemyPatternModuleDefinition>();

    [Header("Pattern Assemble")]
    [Tooltip("Shared patterns assembled from the catalog lists above.")]
    [SerializeField] private List<EnemyModulesPatternDefinition> patterns = new List<EnemyModulesPatternDefinition>();
    #endregion

    #endregion

    #region Properties
    public string PresetId
    {
        get
        {
            return presetId;
        }
    }

    public string PresetName
    {
        get
        {
            return presetName;
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public string Version
    {
        get
        {
            return version;
        }
    }

    public IReadOnlyList<EnemyPatternModuleDefinition> CoreMovementDefinitions
    {
        get
        {
            return coreMovementDefinitions;
        }
    }

    public IReadOnlyList<EnemyPatternModuleDefinition> ShortRangeInteractionDefinitions
    {
        get
        {
            return shortRangeInteractionDefinitions;
        }
    }

    public IReadOnlyList<EnemyPatternModuleDefinition> WeaponInteractionDefinitions
    {
        get
        {
            return weaponInteractionDefinitions;
        }
    }

    public IReadOnlyList<EnemyPatternModuleDefinition> DropItemsDefinitions
    {
        get
        {
            return dropItemsDefinitions;
        }
    }

    public IReadOnlyList<EnemyModulesPatternDefinition> Patterns
    {
        get
        {
            return patterns;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Replaces the editable contents of this shared preset with caller-provided metadata, catalogs and patterns.
    /// </summary>
    /// <param name="presetNameValue">New preset display name.</param>
    /// <param name="descriptionValue">New description text.</param>
    /// <param name="versionValue">New semantic version string.</param>
    /// <param name="coreMovementDefinitionsValue">Replacement core-movement catalog.</param>
    /// <param name="shortRangeInteractionDefinitionsValue">Replacement short-range catalog.</param>
    /// <param name="weaponInteractionDefinitionsValue">Replacement weapon catalog.</param>
    /// <param name="dropItemsDefinitionsValue">Replacement drop-items catalog.</param>
    /// <param name="patternsValue">Replacement shared assembled patterns.</param>
    public void ReplaceContents(string presetNameValue,
                                string descriptionValue,
                                string versionValue,
                                List<EnemyPatternModuleDefinition> coreMovementDefinitionsValue,
                                List<EnemyPatternModuleDefinition> shortRangeInteractionDefinitionsValue,
                                List<EnemyPatternModuleDefinition> weaponInteractionDefinitionsValue,
                                List<EnemyPatternModuleDefinition> dropItemsDefinitionsValue,
                                List<EnemyModulesPatternDefinition> patternsValue)
    {
        presetName = presetNameValue;
        description = descriptionValue;
        version = versionValue;
        coreMovementDefinitions = coreMovementDefinitionsValue ?? new List<EnemyPatternModuleDefinition>();
        shortRangeInteractionDefinitions = shortRangeInteractionDefinitionsValue ?? new List<EnemyPatternModuleDefinition>();
        weaponInteractionDefinitions = weaponInteractionDefinitionsValue ?? new List<EnemyPatternModuleDefinition>();
        dropItemsDefinitions = dropItemsDefinitionsValue ?? new List<EnemyPatternModuleDefinition>();
        patterns = patternsValue ?? new List<EnemyModulesPatternDefinition>();
        ValidateValues();
    }

    /// <summary>
    /// Resolves one shared module definition by module ID across every catalog subsection.
    /// </summary>
    /// <param name="moduleId">Target module identifier.</param>
    /// <returns>The resolved module definition, or null when not found.</returns>
    public EnemyPatternModuleDefinition ResolveModuleDefinitionById(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            return null;

        EnemyPatternModuleDefinition resolvedDefinition = ResolveModuleDefinitionById(moduleId, coreMovementDefinitions);

        if (resolvedDefinition != null)
            return resolvedDefinition;

        resolvedDefinition = ResolveModuleDefinitionById(moduleId, shortRangeInteractionDefinitions);

        if (resolvedDefinition != null)
            return resolvedDefinition;

        resolvedDefinition = ResolveModuleDefinitionById(moduleId, weaponInteractionDefinitions);

        if (resolvedDefinition != null)
            return resolvedDefinition;

        return ResolveModuleDefinitionById(moduleId, dropItemsDefinitions);
    }

    /// <summary>
    /// Resolves one shared pattern definition by pattern ID.
    /// </summary>
    /// <param name="patternId">Target pattern identifier.</param>
    /// <returns>The resolved shared pattern definition, or null when not found.</returns>
    public EnemyModulesPatternDefinition ResolvePatternById(string patternId)
    {
        if (string.IsNullOrWhiteSpace(patternId))
            return null;

        string normalizedPatternId = patternId.Trim();

        for (int index = 0; index < patterns.Count; index++)
        {
            EnemyModulesPatternDefinition pattern = patterns[index];

            if (pattern == null)
                continue;

            if (!string.Equals(pattern.PatternId, normalizedPatternId, StringComparison.OrdinalIgnoreCase))
                continue;

            return pattern;
        }

        return null;
    }

    /// <summary>
    /// Returns the module-definition list that matches one catalog section.
    /// </summary>
    /// <param name="section">Requested catalog section.</param>
    /// <returns>The matching module-definition list.</returns>
    public IReadOnlyList<EnemyPatternModuleDefinition> GetDefinitions(EnemyPatternModuleCatalogSection section)
    {
        switch (section)
        {
            case EnemyPatternModuleCatalogSection.CoreMovement:
                return coreMovementDefinitions;

            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                return shortRangeInteractionDefinitions;

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                return weaponInteractionDefinitions;

            default:
                return dropItemsDefinitions;
        }
    }

    /// <summary>
    /// Validates metadata, shared module catalogs and assembled patterns.
    /// </summary>
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(presetName))
            presetName = "New Enemy Modules And Patterns Preset";

        EnsureLists();

        if (IsCatalogEmpty())
            BuildDefaultCatalog();

        if (patterns.Count <= 0)
            patterns = BuildDefaultPatternDefinitions();

        ValidateDefinitionList(coreMovementDefinitions,
                               EnemyPatternModuleCatalogSection.CoreMovement,
                               new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        ValidateDefinitionList(shortRangeInteractionDefinitions,
                               EnemyPatternModuleCatalogSection.ShortRangeInteraction,
                               CollectUsedModuleIds(coreMovementDefinitions));
        ValidateDefinitionList(weaponInteractionDefinitions,
                               EnemyPatternModuleCatalogSection.WeaponInteraction,
                               CollectUsedModuleIds(coreMovementDefinitions,
                                                    shortRangeInteractionDefinitions));
        ValidateDefinitionList(dropItemsDefinitions,
                               EnemyPatternModuleCatalogSection.DropItems,
                               CollectUsedModuleIds(coreMovementDefinitions,
                                                    shortRangeInteractionDefinitions,
                                                    weaponInteractionDefinitions));
        ValidatePatternDefinitions();
    }
    #endregion

    #region Unity Methods
    private void OnValidate()
    {
        ValidateValues();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Ensures all serialized lists always exist before validation and editor drawing.
    /// </summary>
    private void EnsureLists()
    {
        if (coreMovementDefinitions == null)
            coreMovementDefinitions = new List<EnemyPatternModuleDefinition>();

        if (shortRangeInteractionDefinitions == null)
            shortRangeInteractionDefinitions = new List<EnemyPatternModuleDefinition>();

        if (weaponInteractionDefinitions == null)
            weaponInteractionDefinitions = new List<EnemyPatternModuleDefinition>();

        if (dropItemsDefinitions == null)
            dropItemsDefinitions = new List<EnemyPatternModuleDefinition>();

        if (patterns == null)
            patterns = new List<EnemyModulesPatternDefinition>();
    }

    /// <summary>
    /// Checks whether every catalog subsection is currently empty.
    /// </summary>
    /// <returns>True when no shared module definition exists.</returns>
    private bool IsCatalogEmpty()
    {
        if (coreMovementDefinitions.Count > 0)
            return false;

        if (shortRangeInteractionDefinitions.Count > 0)
            return false;

        if (weaponInteractionDefinitions.Count > 0)
            return false;

        return dropItemsDefinitions.Count <= 0;
    }

    /// <summary>
    /// Fills the shared preset with a baseline module catalog used by freshly created presets.
    /// </summary>
    private void BuildDefaultCatalog()
    {
        coreMovementDefinitions = new List<EnemyPatternModuleDefinition>();
        shortRangeInteractionDefinitions = new List<EnemyPatternModuleDefinition>();
        weaponInteractionDefinitions = new List<EnemyPatternModuleDefinition>();
        dropItemsDefinitions = new List<EnemyPatternModuleDefinition>();

        coreMovementDefinitions.Add(CreateDefaultModule(DefaultCoreStationaryModuleId,
                                                        "Stationary",
                                                        EnemyPatternModuleKind.Stationary,
                                                        "Stops movement and can optionally freeze rotation."));
        coreMovementDefinitions.Add(CreateDefaultModule(DefaultCoreGruntModuleId,
                                                        "Grunt",
                                                        EnemyPatternModuleKind.Grunt,
                                                        "Uses the standard brain-driven chase and separation."));
        coreMovementDefinitions.Add(CreateDefaultModule(DefaultCoreWandererModuleId,
                                                        "Wanderer",
                                                        EnemyPatternModuleKind.Wanderer,
                                                        "Uses autonomous wandering behaviour outside short-range overrides."));

        shortRangeInteractionDefinitions.Add(CreateDefaultModule(DefaultShortRangeGruntModuleId,
                                                                 "Grunt",
                                                                 EnemyPatternModuleKind.Grunt,
                                                                 "Switches to direct chase while the player stays inside the short-range band."));
        shortRangeInteractionDefinitions.Add(CreateDefaultModule(DefaultShortRangeCowardModuleId,
                                                                 "Coward",
                                                                 EnemyPatternModuleKind.Coward,
                                                                 "Retreats from the player while the short-range band stays active."));
        shortRangeInteractionDefinitions.Add(CreateDefaultModule(DefaultShortRangeDashModuleId,
                                                                 "Dash",
                                                                 EnemyPatternModuleKind.ShortRangeDash,
                                                                 "Takes aim, then commits to one authored dash path toward the player."));

        weaponInteractionDefinitions.Add(CreateDefaultModule(DefaultWeaponShooterModuleId,
                                                             "Shooter",
                                                             EnemyPatternModuleKind.Shooter,
                                                             "Fires periodic projectiles while the weapon interaction is in range."));
        weaponInteractionDefinitions.Add(CreateDefaultModule(DefaultWeaponBombardierModuleId,
                                                             "Bombardier",
                                                             EnemyPatternModuleKind.Bombardier,
                                                             "Launches parabolic bombs with optional landing warnings while the weapon interaction is active."));
        weaponInteractionDefinitions.Add(CreateDefaultModule(DefaultWeaponPowerUpStealerModuleId,
                                                             "Power-Up Stealer",
                                                             EnemyPatternModuleKind.PowerUpStealer,
                                                             "Steals one player power-up while the weapon interaction is in range."));

        dropItemsDefinitions.Add(CreateDefaultModule(DefaultDropItemsModuleId,
                                                     "Drop Items",
                                                     EnemyPatternModuleKind.DropItems,
                                                     "Spawns collectible drops on enemy death."));
    }

    /// <summary>
    /// Creates a new default module definition and validates its payload containers.
    /// </summary>
    /// <param name="moduleIdValue">Default module identifier.</param>
    /// <param name="displayNameValue">Default display name.</param>
    /// <param name="moduleKindValue">Default module kind.</param>
    /// <param name="notesValue">Default notes string.</param>
    /// <returns>The created module definition instance.</returns>
    private static EnemyPatternModuleDefinition CreateDefaultModule(string moduleIdValue,
                                                                    string displayNameValue,
                                                                    EnemyPatternModuleKind moduleKindValue,
                                                                    string notesValue)
    {
        EnemyPatternModuleDefinition definition = new EnemyPatternModuleDefinition();
        EnemyPatternModulePayloadData payloadData = new EnemyPatternModulePayloadData();
        definition.Configure(moduleIdValue, displayNameValue, moduleKindValue, notesValue, payloadData);
        definition.Validate();
        return definition;
    }

    /// <summary>
    /// Builds the default shared pattern list used by freshly created presets.
    /// </summary>
    /// <returns>The default shared pattern list.</returns>
    private static List<EnemyModulesPatternDefinition> BuildDefaultPatternDefinitions()
    {
        List<EnemyModulesPatternDefinition> definitions = new List<EnemyModulesPatternDefinition>();
        EnemyModulesPatternDefinition defaultPattern = new EnemyModulesPatternDefinition();
        defaultPattern.Configure(DefaultPatternId,
                                 "Default Grunt",
                                 "Baseline shared pattern with Grunt core movement.",
                                 false);
        defaultPattern.Validate();
        defaultPattern.CoreMovement.Binding.Configure(DefaultCoreGruntModuleId, true);
        definitions.Add(defaultPattern);
        return definitions;
    }

    /// <summary>
    /// Validates one catalog subsection, enforcing unique module IDs and legal module-kind values for that subsection.
    /// </summary>
    /// <param name="definitions">Definition list to validate.</param>
    /// <param name="section">Catalog section that owns the list.</param>
    /// <param name="usedIds">Case-insensitive set of module IDs already consumed by previous sections.</param>
    private static void ValidateDefinitionList(List<EnemyPatternModuleDefinition> definitions,
                                               EnemyPatternModuleCatalogSection section,
                                               HashSet<string> usedIds)
    {
        if (definitions == null)
            return;

        for (int index = 0; index < definitions.Count; index++)
        {
            EnemyPatternModuleDefinition definition = definitions[index];

            if (definition == null)
            {
                definitions[index] = new EnemyPatternModuleDefinition();
                definition = definitions[index];
            }

            definition.Validate();
            EnemyPatternModuleKind resolvedKind = ResolveCatalogModuleKind(section, definition.ModuleKind);
            string normalizedId = NormalizeId(definition.ModuleId, ResolveCatalogFallbackId(section, resolvedKind));
            string uniqueId = ResolveUniqueId(normalizedId, usedIds, ResolveCatalogFallbackId(section, resolvedKind));

            if (!string.Equals(uniqueId, definition.ModuleId, StringComparison.Ordinal) ||
                resolvedKind != definition.ModuleKind)
            {
                definition.Configure(uniqueId,
                                     definition.DisplayName,
                                     resolvedKind,
                                     definition.Notes,
                                     definition.Data);
                definition.Validate();
            }

            if (usedIds != null)
                usedIds.Add(uniqueId);
        }
    }

    /// <summary>
    /// Validates the shared pattern list, ensuring identity stability and nested slot integrity.
    /// </summary>
    private void ValidatePatternDefinitions()
    {
        HashSet<string> usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < patterns.Count; index++)
        {
            EnemyModulesPatternDefinition pattern = patterns[index];

            if (pattern == null)
            {
                patterns.RemoveAt(index);
                index--;
                continue;
            }

            pattern.Validate();
            string normalizedId = NormalizeId(pattern.PatternId, "Pattern_Custom");
            string uniqueId = ResolveUniqueId(normalizedId, usedIds, "Pattern_Custom");

            if (!string.Equals(uniqueId, pattern.PatternId, StringComparison.Ordinal))
            {
                pattern.Configure(uniqueId,
                                  pattern.DisplayName,
                                  pattern.Description,
                                  pattern.Unreplaceable);
                pattern.Validate();
            }

            usedIds.Add(uniqueId);
        }
    }

    /// <summary>
    /// Resolves the legal module kind for one catalog section.
    /// </summary>
    /// <param name="section">Catalog section being validated.</param>
    /// <param name="candidateKind">Authored module kind candidate.</param>
    /// <returns>A legal module kind for the requested section.</returns>
    private static EnemyPatternModuleKind ResolveCatalogModuleKind(EnemyPatternModuleCatalogSection section,
                                                                   EnemyPatternModuleKind candidateKind)
    {
        switch (section)
        {
            case EnemyPatternModuleCatalogSection.CoreMovement:
                if (candidateKind == EnemyPatternModuleKind.Stationary ||
                    candidateKind == EnemyPatternModuleKind.Grunt ||
                    candidateKind == EnemyPatternModuleKind.Wanderer)
                    return candidateKind;

                return EnemyPatternModuleKind.Grunt;

            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                if (candidateKind == EnemyPatternModuleKind.Grunt ||
                    candidateKind == EnemyPatternModuleKind.Coward ||
                    candidateKind == EnemyPatternModuleKind.ShortRangeDash)
                    return candidateKind;

                return EnemyPatternModuleKind.Grunt;

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                if (candidateKind == EnemyPatternModuleKind.Shooter ||
                    candidateKind == EnemyPatternModuleKind.Bombardier ||
                    candidateKind == EnemyPatternModuleKind.PowerUpStealer)
                    return candidateKind;

                return EnemyPatternModuleKind.Shooter;

            default:
                return EnemyPatternModuleKind.DropItems;
        }
    }

    /// <summary>
    /// Resolves the fallback module ID prefix used by one catalog section and module kind.
    /// </summary>
    /// <param name="section">Catalog section being validated.</param>
    /// <param name="moduleKind">Resolved module kind.</param>
    /// <returns>A stable fallback module ID.</returns>
    private static string ResolveCatalogFallbackId(EnemyPatternModuleCatalogSection section, EnemyPatternModuleKind moduleKind)
    {
        switch (section)
        {
            case EnemyPatternModuleCatalogSection.CoreMovement:
                switch (moduleKind)
                {
                    case EnemyPatternModuleKind.Stationary:
                        return DefaultCoreStationaryModuleId;

                    case EnemyPatternModuleKind.Wanderer:
                        return DefaultCoreWandererModuleId;

                    default:
                        return DefaultCoreGruntModuleId;
                }

            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                if (moduleKind == EnemyPatternModuleKind.Coward)
                    return DefaultShortRangeCowardModuleId;

                if (moduleKind == EnemyPatternModuleKind.ShortRangeDash)
                    return DefaultShortRangeDashModuleId;

                return DefaultShortRangeGruntModuleId;

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                if (moduleKind == EnemyPatternModuleKind.Bombardier)
                    return DefaultWeaponBombardierModuleId;

                if (moduleKind == EnemyPatternModuleKind.PowerUpStealer)
                    return DefaultWeaponPowerUpStealerModuleId;

                return DefaultWeaponShooterModuleId;

            default:
                return DefaultDropItemsModuleId;
        }
    }

    /// <summary>
    /// Collects every module ID already used by the provided definition lists.
    /// </summary>
    /// <param name="definitionLists">Any number of definition lists.</param>
    /// <returns>A case-insensitive set containing every non-empty module ID.</returns>
    private static HashSet<string> CollectUsedModuleIds(params List<EnemyPatternModuleDefinition>[] definitionLists)
    {
        HashSet<string> usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (definitionLists == null)
            return usedIds;

        for (int listIndex = 0; listIndex < definitionLists.Length; listIndex++)
        {
            List<EnemyPatternModuleDefinition> definitions = definitionLists[listIndex];

            if (definitions == null)
                continue;

            for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                EnemyPatternModuleDefinition definition = definitions[definitionIndex];

                if (definition == null || string.IsNullOrWhiteSpace(definition.ModuleId))
                    continue;

                usedIds.Add(definition.ModuleId.Trim());
            }
        }

        return usedIds;
    }

    /// <summary>
    /// Resolves one module definition by ID from one specific list.
    /// </summary>
    /// <param name="moduleId">Target module identifier.</param>
    /// <param name="definitions">Definition list to search.</param>
    /// <returns>The matching module definition, or null when not found.</returns>
    private static EnemyPatternModuleDefinition ResolveModuleDefinitionById(string moduleId,
                                                                           List<EnemyPatternModuleDefinition> definitions)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            return null;

        if (definitions == null)
            return null;

        string normalizedModuleId = moduleId.Trim();

        for (int index = 0; index < definitions.Count; index++)
        {
            EnemyPatternModuleDefinition definition = definitions[index];

            if (definition == null)
                continue;

            if (!string.Equals(definition.ModuleId, normalizedModuleId, StringComparison.OrdinalIgnoreCase))
                continue;

            return definition;
        }

        return null;
    }

    /// <summary>
    /// Normalizes one identifier while preserving author intent whenever possible.
    /// </summary>
    /// <param name="rawId">Raw authored identifier string.</param>
    /// <param name="fallback">Fallback identifier used when the authored value is empty.</param>
    /// <returns>The normalized identifier.</returns>
    private static string NormalizeId(string rawId, string fallback)
    {
        if (string.IsNullOrWhiteSpace(rawId))
            return fallback;

        string normalizedId = rawId.Trim();

        if (string.IsNullOrWhiteSpace(normalizedId))
            return fallback;

        return normalizedId;
    }

    /// <summary>
    /// Resolves one unique identifier against a set of already-used values.
    /// </summary>
    /// <param name="baseId">Requested base identifier.</param>
    /// <param name="usedIds">Already-used identifier set.</param>
    /// <param name="fallbackBaseId">Fallback identifier used when the base ID is invalid.</param>
    /// <returns>A unique identifier string.</returns>
    private static string ResolveUniqueId(string baseId,
                                          HashSet<string> usedIds,
                                          string fallbackBaseId)
    {
        string normalizedBaseId = NormalizeId(baseId, fallbackBaseId);

        if (usedIds == null)
            return normalizedBaseId;

        if (!usedIds.Contains(normalizedBaseId))
            return normalizedBaseId;

        int suffix = 2;

        while (true)
        {
            string indexedId = string.Format("{0}_{1}", normalizedBaseId, suffix);

            if (!usedIds.Contains(indexedId))
                return indexedId;

            suffix++;
        }
    }
    #endregion

    #endregion
}
