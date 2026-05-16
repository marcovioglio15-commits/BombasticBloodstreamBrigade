using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Stores enemy-specific advanced-pattern metadata, active loadout selection and the shared Modules & Patterns preset reference.
/// Legacy local catalogs remain serialized only as a compatibility fallback for already-authored assets.
/// </summary>
[CreateAssetMenu(fileName = "EnemyAdvancedPatternPreset", menuName = "Enemy/Advanced Pattern Preset", order = 12)]
public sealed class EnemyAdvancedPatternPreset : ScriptableObject
{
    #region Constants
    private const string DefaultLegacyPatternId = "Pattern_DefaultGrunt";
    private const string DefaultLegacyModuleIdStationary = "Module_Stationary";
    private const string DefaultLegacyModuleIdGrunt = "Module_Grunt";
    private const string DefaultLegacyModuleIdWanderer = "Module_Wanderer";
    private const string DefaultLegacyModuleIdCoward = "Module_Coward";
    private const string DefaultLegacyModuleIdShooter = "Module_Shooter";
    private const string DefaultLegacyModuleIdDropItems = "Module_DropItems";
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this advanced pattern preset.")]
    [SerializeField] private string presetId;

    [Tooltip("Human-readable name shown in Enemy Management Tool.")]
    [SerializeField] private string presetName = "New Enemy Advanced Pattern Preset";

    [Tooltip("Short description of this advanced pattern preset.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this preset.")]
    [SerializeField] private string version = "1.0.0";

    [Header("Modules & Patterns Preset")]
    [Tooltip("Shared modules and patterns preset referenced by this enemy-specific loadout.")]
    [SerializeField] private EnemyModulesAndPatternsPreset modulesAndPatternsPreset;

    [Header("Pattern Loadout")]
    [Tooltip("Active shared pattern IDs compiled at bake time. The current tool flow expects one selected pattern.")]
    [SerializeField] private List<string> activePatternIds = new List<string>();

    [Tooltip("Legacy shooter projectile prefab fallback kept for backward compatibility with older presets.")]
    [FormerlySerializedAs("shooterProjectilePrefab")]
    [SerializeField]
    [HideInInspector] private GameObject legacyShooterProjectilePrefab;

    [Tooltip("Legacy shooter pool initial capacity fallback kept for backward compatibility with older presets.")]
    [FormerlySerializedAs("shooterProjectilePoolInitialCapacity")]
    [SerializeField]
    [HideInInspector] private int legacyShooterProjectilePoolInitialCapacity = 24;

    [Tooltip("Legacy shooter pool expand batch fallback kept for backward compatibility with older presets.")]
    [FormerlySerializedAs("shooterProjectilePoolExpandBatch")]
    [SerializeField]
    [HideInInspector] private int legacyShooterProjectilePoolExpandBatch = 8;

    [Tooltip("Legacy reusable module catalog kept hidden so pre-refactor assets can still be migrated and baked safely.")]
    [SerializeField]
    [HideInInspector] private List<EnemyPatternModuleDefinition> moduleDefinitions = new List<EnemyPatternModuleDefinition>();

    [Tooltip("Legacy assembled patterns kept hidden so pre-refactor assets can still be migrated and baked safely.")]
    [SerializeField]
    [HideInInspector] private List<EnemyPatternDefinition> patterns = new List<EnemyPatternDefinition>();
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

    public EnemyModulesAndPatternsPreset ModulesAndPatternsPreset
    {
        get
        {
            return modulesAndPatternsPreset;
        }
    }

    public IReadOnlyList<string> ActivePatternIds
    {
        get
        {
            return activePatternIds;
        }
    }

    public IReadOnlyList<EnemyPatternModuleDefinition> LegacyModuleDefinitions
    {
        get
        {
            return moduleDefinitions;
        }
    }

    public IReadOnlyList<EnemyPatternDefinition> LegacyPatterns
    {
        get
        {
            return patterns;
        }
    }

    public GameObject LegacyShooterProjectilePrefab
    {
        get
        {
            return legacyShooterProjectilePrefab;
        }
    }

    public int LegacyShooterProjectilePoolInitialCapacity
    {
        get
        {
            return legacyShooterProjectilePoolInitialCapacity;
        }
    }

    public int LegacyShooterProjectilePoolExpandBatch
    {
        get
        {
            return legacyShooterProjectilePoolExpandBatch;
        }
    }

    public bool HasLegacyCatalogData
    {
        get
        {
            if (moduleDefinitions != null && moduleDefinitions.Count > 0)
                return true;

            return patterns != null && patterns.Count > 0;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves one shared or legacy module definition by module ID.
    /// Shared catalog data has priority when a Modules & Patterns preset is assigned.
    /// </summary>
    /// <param name="moduleId">Target module identifier.</param>
    /// <returns>The resolved module definition, or null when not found.</returns>
    public EnemyPatternModuleDefinition ResolveModuleDefinitionById(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            return null;

        if (modulesAndPatternsPreset != null)
        {
            EnemyPatternModuleDefinition sharedDefinition = modulesAndPatternsPreset.ResolveModuleDefinitionById(moduleId);

            if (sharedDefinition != null)
                return sharedDefinition;
        }

        return ResolveLegacyModuleDefinitionById(moduleId);
    }

    /// <summary>
    /// Resolves one pattern definition by pattern ID using the hidden legacy local catalog.
    /// This compatibility wrapper keeps legacy bake/editor code working after the shared-preset split.
    /// </summary>
    /// <param name="patternId">Target pattern identifier.</param>
    /// <returns>The resolved legacy pattern definition, or null when not found.</returns>
    public EnemyPatternDefinition ResolvePatternById(string patternId)
    {
        return ResolveLegacyPatternById(patternId);
    }

    /// <summary>
    /// Resolves one shared pattern definition by pattern ID.
    /// </summary>
    /// <param name="patternId">Target shared pattern identifier.</param>
    /// <returns>The resolved shared pattern definition, or null when no shared preset is assigned or no match exists.</returns>
    public EnemyModulesPatternDefinition ResolveSharedPatternById(string patternId)
    {
        if (modulesAndPatternsPreset == null)
            return null;

        return modulesAndPatternsPreset.ResolvePatternById(patternId);
    }

    /// <summary>
    /// Resolves one legacy local module definition by module ID.
    /// </summary>
    /// <param name="moduleId">Target legacy module identifier.</param>
    /// <returns>The resolved legacy module definition, or null when not found.</returns>
    public EnemyPatternModuleDefinition ResolveLegacyModuleDefinitionById(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            return null;

        string normalizedModuleId = moduleId.Trim();

        for (int index = 0; index < moduleDefinitions.Count; index++)
        {
            EnemyPatternModuleDefinition definition = moduleDefinitions[index];

            if (definition == null)
                continue;

            if (!string.Equals(definition.ModuleId, normalizedModuleId, StringComparison.OrdinalIgnoreCase))
                continue;

            return definition;
        }

        return null;
    }

    /// <summary>
    /// Resolves one legacy local pattern definition by pattern ID.
    /// </summary>
    /// <param name="patternId">Target legacy pattern identifier.</param>
    /// <returns>The resolved legacy pattern definition, or null when not found.</returns>
    public EnemyPatternDefinition ResolveLegacyPatternById(string patternId)
    {
        if (string.IsNullOrWhiteSpace(patternId))
            return null;

        string normalizedPatternId = patternId.Trim();

        for (int index = 0; index < patterns.Count; index++)
        {
            EnemyPatternDefinition pattern = patterns[index];

            if (pattern == null)
                continue;

            if (!string.Equals(pattern.PatternId, normalizedPatternId, StringComparison.OrdinalIgnoreCase))
                continue;

            return pattern;
        }

        return null;
    }

    /// <summary>
    /// Validates metadata, active loadout data, referenced shared preset and hidden legacy compatibility data.
    /// </summary>
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(presetName))
            presetName = "New Enemy Advanced Pattern Preset";

        if (activePatternIds == null)
            activePatternIds = new List<string>();

        if (modulesAndPatternsPreset != null)
            modulesAndPatternsPreset.ValidateValues();

        ValidateLegacyCompatibilityData();
        NormalizeLoadoutIds();

        if (activePatternIds.Count <= 0)
            AddDefaultLoadoutEntry();

        if (legacyShooterProjectilePoolInitialCapacity < 0)
            legacyShooterProjectilePoolInitialCapacity = 0;

        if (legacyShooterProjectilePoolExpandBatch < 1)
            legacyShooterProjectilePoolExpandBatch = 1;
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
    /// Ensures hidden legacy data remains structurally valid whenever older assets are still loaded.
    /// </summary>
    private void ValidateLegacyCompatibilityData()
    {
        if (moduleDefinitions == null)
            moduleDefinitions = new List<EnemyPatternModuleDefinition>();

        if (patterns == null)
            patterns = new List<EnemyPatternDefinition>();

        if (moduleDefinitions.Count <= 0 && patterns.Count <= 0)
            return;

        if (moduleDefinitions.Count <= 0)
            moduleDefinitions = BuildDefaultLegacyModuleDefinitions();

        if (patterns.Count <= 0)
            patterns = BuildDefaultLegacyPatternDefinitions();

        ValidateLegacyModuleDefinitions();
        ValidateLegacyPatternDefinitions();
        MigrateLegacyRuntimeProjectileToShooterPayload();
    }

    /// <summary>
    /// Adds a default pattern loadout entry from the shared preset when available, otherwise from hidden legacy data.
    /// </summary>
    private void AddDefaultLoadoutEntry()
    {
        if (modulesAndPatternsPreset != null && modulesAndPatternsPreset.Patterns.Count > 0)
        {
            activePatternIds.Add(modulesAndPatternsPreset.Patterns[0].PatternId);
            return;
        }

        if (patterns.Count > 0)
            activePatternIds.Add(patterns[0].PatternId);
    }

    /// <summary>
    /// Normalizes active loadout IDs against the currently assigned shared preset, or against legacy data when no shared preset exists.
    /// </summary>
    private void NormalizeLoadoutIds()
    {
        HashSet<string> validPatternIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (modulesAndPatternsPreset != null)
        {
            IReadOnlyList<EnemyModulesPatternDefinition> sharedPatterns = modulesAndPatternsPreset.Patterns;

            for (int index = 0; index < sharedPatterns.Count; index++)
            {
                EnemyModulesPatternDefinition pattern = sharedPatterns[index];

                if (pattern == null || string.IsNullOrWhiteSpace(pattern.PatternId))
                    continue;

                validPatternIds.Add(pattern.PatternId);
            }
        }
        else
        {
            for (int index = 0; index < patterns.Count; index++)
            {
                EnemyPatternDefinition pattern = patterns[index];

                if (pattern == null || string.IsNullOrWhiteSpace(pattern.PatternId))
                    continue;

                validPatternIds.Add(pattern.PatternId);
            }
        }

        HashSet<string> visitedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < activePatternIds.Count; index++)
        {
            string loadoutId = NormalizeId(activePatternIds[index], string.Empty);

            if (string.IsNullOrWhiteSpace(loadoutId))
            {
                activePatternIds.RemoveAt(index);
                index--;
                continue;
            }

            if (!validPatternIds.Contains(loadoutId))
            {
                activePatternIds.RemoveAt(index);
                index--;
                continue;
            }

            if (!visitedIds.Add(loadoutId))
            {
                activePatternIds.RemoveAt(index);
                index--;
                continue;
            }

            activePatternIds[index] = loadoutId;
        }
    }

    /// <summary>
    /// Builds the hidden legacy module catalog used by old presets that still serialize local module definitions.
    /// </summary>
    /// <returns>The default legacy module-definition list.</returns>
    private static List<EnemyPatternModuleDefinition> BuildDefaultLegacyModuleDefinitions()
    {
        List<EnemyPatternModuleDefinition> definitions = new List<EnemyPatternModuleDefinition>();
        definitions.Add(CreateDefaultLegacyModule(DefaultLegacyModuleIdStationary,
                                                  "Stationary",
                                                  EnemyPatternModuleKind.Stationary,
                                                  "Forces movement speed to zero."));
        definitions.Add(CreateDefaultLegacyModule(DefaultLegacyModuleIdGrunt,
                                                  "Grunt",
                                                  EnemyPatternModuleKind.Grunt,
                                                  "Uses standard chase and separation from Brain settings."));
        definitions.Add(CreateDefaultLegacyModule(DefaultLegacyModuleIdWanderer,
                                                  "Wanderer",
                                                  EnemyPatternModuleKind.Wanderer,
                                                  "Uses autonomous wandering behaviour."));
        definitions.Add(CreateDefaultLegacyModule(DefaultLegacyModuleIdCoward,
                                                  "Coward",
                                                  EnemyPatternModuleKind.Coward,
                                                  "Retreats from the player while still keeping contact and area damage active."));
        definitions.Add(CreateDefaultLegacyModule(DefaultLegacyModuleIdShooter,
                                                  "Shooter",
                                                  EnemyPatternModuleKind.Shooter,
                                                  "Shoots periodic projectiles toward the player."));
        definitions.Add(CreateDefaultLegacyModule(DefaultLegacyModuleIdDropItems,
                                                  "Drop Items",
                                                  EnemyPatternModuleKind.DropItems,
                                                  "Spawns collectible drops on enemy death."));
        return definitions;
    }

    /// <summary>
    /// Creates one default hidden legacy module definition.
    /// </summary>
    /// <param name="moduleIdValue">Module identifier.</param>
    /// <param name="displayNameValue">Display name.</param>
    /// <param name="moduleKindValue">Module kind.</param>
    /// <param name="notesValue">Notes text.</param>
    /// <returns>The created legacy module definition.</returns>
    private static EnemyPatternModuleDefinition CreateDefaultLegacyModule(string moduleIdValue,
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
    /// Builds the hidden default legacy pattern list used by already-authored local presets.
    /// </summary>
    /// <returns>The default legacy pattern list.</returns>
    private static List<EnemyPatternDefinition> BuildDefaultLegacyPatternDefinitions()
    {
        List<EnemyPatternDefinition> definitions = new List<EnemyPatternDefinition>();
        EnemyPatternDefinition defaultPattern = new EnemyPatternDefinition();
        EnemyPatternModuleBinding gruntBinding = new EnemyPatternModuleBinding();
        gruntBinding.Configure(DefaultLegacyModuleIdGrunt, true);
        gruntBinding.ConfigureOverride(false, new EnemyPatternModulePayloadData());
        defaultPattern.Configure(DefaultLegacyPatternId,
                                 "Default Grunt",
                                 "Baseline pattern with Grunt movement.",
                                 false);
        defaultPattern.ClearBindings();
        defaultPattern.AddBinding(gruntBinding);
        defaultPattern.Validate();
        definitions.Add(defaultPattern);
        return definitions;
    }

    /// <summary>
    /// Validates the hidden legacy module-definition list and keeps module IDs unique.
    /// </summary>
    private void ValidateLegacyModuleDefinitions()
    {
        HashSet<string> usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < moduleDefinitions.Count; index++)
        {
            EnemyPatternModuleDefinition definition = moduleDefinitions[index];

            if (definition == null)
            {
                definition = new EnemyPatternModuleDefinition();
                moduleDefinitions[index] = definition;
            }

            definition.Validate();
            string normalizedId = NormalizeId(definition.ModuleId, "Module_Custom");
            string uniqueId = ResolveUniqueId(normalizedId, usedIds, "Module_Custom");

            if (!string.Equals(uniqueId, definition.ModuleId, StringComparison.Ordinal))
            {
                definition.Configure(uniqueId,
                                     definition.DisplayName,
                                     definition.ModuleKind,
                                     definition.Notes,
                                     definition.Data);
                definition.Validate();
            }

            usedIds.Add(uniqueId);
        }
    }

    /// <summary>
    /// Validates the hidden legacy pattern list and keeps pattern IDs unique.
    /// </summary>
    private void ValidateLegacyPatternDefinitions()
    {
        HashSet<string> usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < patterns.Count; index++)
        {
            EnemyPatternDefinition pattern = patterns[index];

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
    /// Migrates hidden legacy shooter runtime values into hidden legacy shooter modules when those modules still miss a runtime projectile payload.
    /// </summary>
    private void MigrateLegacyRuntimeProjectileToShooterPayload()
    {
        if (legacyShooterProjectilePrefab == null)
            return;

        for (int index = 0; index < moduleDefinitions.Count; index++)
        {
            EnemyPatternModuleDefinition moduleDefinition = moduleDefinitions[index];

            if (moduleDefinition == null)
                continue;

            if (moduleDefinition.ModuleKind != EnemyPatternModuleKind.Shooter)
                continue;

            EnemyPatternModulePayloadData payloadData = moduleDefinition.Data;

            if (payloadData == null || payloadData.Shooter == null || payloadData.Shooter.RuntimeProjectile == null)
                continue;

            EnemyShooterRuntimeProjectilePayload runtimePayload = payloadData.Shooter.RuntimeProjectile;

            if (runtimePayload.ProjectilePrefab != null)
                continue;

            runtimePayload.Configure(legacyShooterProjectilePrefab,
                                     legacyShooterProjectilePoolInitialCapacity,
                                     legacyShooterProjectilePoolExpandBatch);
            runtimePayload.Validate();
        }
    }

    /// <summary>
    /// Normalizes one identifier while preserving author intent whenever possible.
    /// </summary>
    /// <param name="rawId">Raw authored identifier.</param>
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
    /// <returns>A unique identifier.</returns>
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
