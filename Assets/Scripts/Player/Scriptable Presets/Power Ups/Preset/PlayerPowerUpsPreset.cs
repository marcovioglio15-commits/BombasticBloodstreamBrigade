using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Root asset that stores player power-up catalogs, modular definitions and loadout defaults.
/// </summary>
[CreateAssetMenu(fileName = "PlayerPowerUpsPreset", menuName = "Player/Power Ups Preset", order = 13)]
public sealed class PlayerPowerUpsPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this power ups preset.")]
    [FormerlySerializedAs("m_PresetId")]
    [SerializeField] private string presetId;

    [Tooltip("Power ups preset name.")]
    [FormerlySerializedAs("m_PresetName")]
    [SerializeField] private string presetName = "New Power Ups Preset";

    [Tooltip("Description of the preset intent and usage.")]
    [FormerlySerializedAs("m_Description")]
    [SerializeField] private string description;

    [Tooltip("Semantic version of this preset.")]
    [FormerlySerializedAs("m_Version")]
    [SerializeField] private string version = "1.0.0";

    [Header("Scaling")]
    [Tooltip("Optional formula-based scaling rules applied to numeric power-up properties during bake.")]
    [SerializeField] private List<PlayerStatScalingRule> scalingRules = new List<PlayerStatScalingRule>();

    [Header("Input")]
    [Tooltip("Input Action ID used for the primary active tool slot.")]
    [SerializeField] private string primaryToolActionId;

    [Tooltip("Input Action ID used for the secondary active tool slot.")]
    [SerializeField] private string secondaryToolActionId;

    [Tooltip("Input Action ID used to swap the current primary and secondary active power-up slots at runtime.")]
    [SerializeField] private string swapSlotsActionId;

    [Header("Drop Pools & Tiers")]
    [Tooltip("Named drop pools used by progression milestones to roll weighted tier candidates.")]
    [SerializeField] private List<PowerUpDropPoolDefinition> dropPools = new List<PowerUpDropPoolDefinition>();

    [Tooltip("Tier catalog used by progression milestones to roll weighted modular power-ups.")]
    [SerializeField] private List<PowerUpTierLevelDefinition> tierLevels = new List<PowerUpTierLevelDefinition>();

    [Tooltip("Legacy drop-pool catalog kept only for migration to the tier model.")]
    [HideInInspector]
    [SerializeField] private List<string> dropPoolCatalog = new List<string>
    {
        "Milestone",
        "Shop",
        "Boss"
    };

    [Header("Modules Management")]
    [Tooltip("Reusable module catalog used to compose active and passive power ups.")]
    [SerializeField] private List<PowerUpModuleDefinition> moduleDefinitions = new List<PowerUpModuleDefinition>();

    [Header("Active Power Ups")]
    [Tooltip("Composable active power up definitions assembled from modules.")]
    [SerializeField] private List<ModularPowerUpDefinition> activePowerUps = new List<ModularPowerUpDefinition>();

    [Header("Passive Power Ups")]
    [Tooltip("Composable passive power up definitions assembled from modules.")]
    [SerializeField] private List<ModularPowerUpDefinition> passivePowerUps = new List<ModularPowerUpDefinition>();

    [Header("Loadout")]
    [Tooltip("PowerUpId assigned to primary slot at runtime initialization.")]
    [SerializeField] private string primaryActivePowerUpId;

    [Tooltip("PowerUpId assigned to secondary slot at runtime initialization.")]
    [SerializeField] private string secondaryActivePowerUpId;

    [Tooltip("PowerUpId list assigned as equipped passive power ups at runtime initialization.")]
    [SerializeField] private List<string> equippedPassivePowerUpIds = new List<string>();

    [Header("Passive Tools")]
    [Tooltip("Passive tools available in this preset.")]
    [FormerlySerializedAs("passiveModifiers")]
    [HideInInspector]
    [SerializeField] private List<PassiveToolDefinition> passiveTools = new List<PassiveToolDefinition>();

    [Header("Elemental VFX Assignments")]
    [Tooltip("Per-element VFX assignments shared by all elemental passives.")]
    [SerializeField] private List<ElementalVfxByElementData> elementalVfxByElement = new List<ElementalVfxByElementData>();

    [Header("Active Tools")]
    [Tooltip("Active tools available in this preset.")]
    [HideInInspector]
    [SerializeField] private List<ActiveToolDefinition> activeTools = new List<ActiveToolDefinition>();

    [Header(" Loadout")]
    [Tooltip("PowerUpId assigned to primary slot at runtime initialization.")]
    [HideInInspector]
    [SerializeField] private string primaryActiveToolId;

    [Tooltip("PowerUpId assigned to secondary slot at runtime initialization.")]
    [HideInInspector]
    [SerializeField] private string secondaryActiveToolId;

    [Tooltip("PowerUpId list assigned as equipped passive tools at runtime initialization.")]
    [HideInInspector]
    [SerializeField] private List<string> equippedPassiveToolIds = new List<string>();

    [Tooltip(" field used to migrate old primary passive loadout data.")]
    [FormerlySerializedAs("primaryPassiveToolId")]
    [HideInInspector]
    [SerializeField] private string PrimaryPassiveToolId;

    [Tooltip(" field used to migrate old secondary passive loadout data.")]
    [FormerlySerializedAs("secondaryPassiveToolId")]
    [HideInInspector]
    [SerializeField] private string SecondaryPassiveToolId;
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

    public IReadOnlyList<PlayerStatScalingRule> ScalingRules
    {
        get
        {
            return scalingRules;
        }
    }

    public string PrimaryToolActionId
    {
        get
        {
            return primaryToolActionId;
        }
    }

    public string SecondaryToolActionId
    {
        get
        {
            return secondaryToolActionId;
        }
    }

    public string SwapSlotsActionId
    {
        get
        {
            return swapSlotsActionId;
        }
    }

    public IReadOnlyList<PowerUpDropPoolDefinition> DropPools
    {
        get
        {
            return dropPools;
        }
    }

    public IReadOnlyList<PowerUpTierLevelDefinition> TierLevels
    {
        get
        {
            return tierLevels;
        }
    }

    public IReadOnlyList<string> DropPoolCatalog
    {
        get
        {
            return dropPoolCatalog;
        }
    }

    public IReadOnlyList<PowerUpModuleDefinition> ModuleDefinitions
    {
        get
        {
            return moduleDefinitions;
        }
    }

    public IReadOnlyList<ModularPowerUpDefinition> ActivePowerUps
    {
        get
        {
            return activePowerUps;
        }
    }

    public IReadOnlyList<ModularPowerUpDefinition> PassivePowerUps
    {
        get
        {
            return passivePowerUps;
        }
    }

    public string PrimaryActivePowerUpId
    {
        get
        {
            return primaryActivePowerUpId;
        }
    }

    public string SecondaryActivePowerUpId
    {
        get
        {
            return secondaryActivePowerUpId;
        }
    }

    public IReadOnlyList<string> EquippedPassivePowerUpIds
    {
        get
        {
            return equippedPassivePowerUpIds;
        }
    }

    public IReadOnlyList<PassiveToolDefinition> PassiveTools
    {
        get
        {
            return passiveTools;
        }
    }

    public IReadOnlyList<ElementalVfxByElementData> ElementalVfxByElement
    {
        get
        {
            return elementalVfxByElement;
        }
    }

    public IReadOnlyList<ActiveToolDefinition> ActiveTools
    {
        get
        {
            return activeTools;
        }
    }

    public string PrimaryActiveToolId
    {
        get
        {
            return primaryActiveToolId;
        }
    }

    public string SecondaryActiveToolId
    {
        get
        {
            return secondaryActiveToolId;
        }
    }

    public IReadOnlyList<string> EquippedPassiveToolIds
    {
        get
        {
            return equippedPassiveToolIds;
        }
    }
    #endregion

    #region Internal Properties
    internal string PresetIdMutable
    {
        get
        {
            return presetId;
        }
        set
        {
            presetId = value;
        }
    }

    internal string PresetNameMutable
    {
        get
        {
            return presetName;
        }
        set
        {
            presetName = value;
        }
    }

    internal string VersionMutable
    {
        get
        {
            return version;
        }
        set
        {
            version = value;
        }
    }

    internal List<PlayerStatScalingRule> ScalingRulesMutable
    {
        get
        {
            return scalingRules;
        }
        set
        {
            scalingRules = value;
        }
    }

    internal string PrimaryToolActionIdMutable
    {
        get
        {
            return primaryToolActionId;
        }
        set
        {
            primaryToolActionId = value;
        }
    }

    internal string SecondaryToolActionIdMutable
    {
        get
        {
            return secondaryToolActionId;
        }
        set
        {
            secondaryToolActionId = value;
        }
    }

    internal string SwapSlotsActionIdMutable
    {
        get
        {
            return swapSlotsActionId;
        }
        set
        {
            swapSlotsActionId = value;
        }
    }

    internal List<PowerUpDropPoolDefinition> DropPoolsMutable
    {
        get
        {
            return dropPools;
        }
        set
        {
            dropPools = value;
        }
    }

    internal List<PowerUpTierLevelDefinition> TierLevelsMutable
    {
        get
        {
            return tierLevels;
        }
        set
        {
            tierLevels = value;
        }
    }

    internal List<string> DropPoolCatalogMutable
    {
        get
        {
            return dropPoolCatalog;
        }
        set
        {
            dropPoolCatalog = value;
        }
    }

    internal List<PowerUpModuleDefinition> ModuleDefinitionsMutable
    {
        get
        {
            return moduleDefinitions;
        }
        set
        {
            moduleDefinitions = value;
        }
    }

    internal List<ModularPowerUpDefinition> ActivePowerUpsMutable
    {
        get
        {
            return activePowerUps;
        }
        set
        {
            activePowerUps = value;
        }
    }

    internal List<ModularPowerUpDefinition> PassivePowerUpsMutable
    {
        get
        {
            return passivePowerUps;
        }
        set
        {
            passivePowerUps = value;
        }
    }

    internal string PrimaryActivePowerUpIdMutable
    {
        get
        {
            return primaryActivePowerUpId;
        }
        set
        {
            primaryActivePowerUpId = value;
        }
    }

    internal string SecondaryActivePowerUpIdMutable
    {
        get
        {
            return secondaryActivePowerUpId;
        }
        set
        {
            secondaryActivePowerUpId = value;
        }
    }

    internal List<string> EquippedPassivePowerUpIdsMutable
    {
        get
        {
            return equippedPassivePowerUpIds;
        }
        set
        {
            equippedPassivePowerUpIds = value;
        }
    }

    internal List<PassiveToolDefinition> PassiveToolsMutable
    {
        get
        {
            return passiveTools;
        }
        set
        {
            passiveTools = value;
        }
    }

    internal List<ElementalVfxByElementData> ElementalVfxByElementMutable
    {
        get
        {
            return elementalVfxByElement;
        }
        set
        {
            elementalVfxByElement = value;
        }
    }

    internal List<ActiveToolDefinition> ActiveToolsMutable
    {
        get
        {
            return activeTools;
        }
        set
        {
            activeTools = value;
        }
    }

    internal string PrimaryActiveToolIdMutable
    {
        get
        {
            return primaryActiveToolId;
        }
        set
        {
            primaryActiveToolId = value;
        }
    }

    internal string SecondaryActiveToolIdMutable
    {
        get
        {
            return secondaryActiveToolId;
        }
        set
        {
            secondaryActiveToolId = value;
        }
    }

    internal List<string> EquippedPassiveToolIdsMutable
    {
        get
        {
            return equippedPassiveToolIds;
        }
        set
        {
            equippedPassiveToolIds = value;
        }
    }

    internal string PrimaryPassiveToolIdLegacy
    {
        get
        {
            return PrimaryPassiveToolId;
        }
        set
        {
            PrimaryPassiveToolId = value;
        }
    }

    internal string SecondaryPassiveToolIdLegacy
    {
        get
        {
            return SecondaryPassiveToolId;
        }
        set
        {
            SecondaryPassiveToolId = value;
        }
    }
    #endregion

    #region Methods

    #region Public API
    public bool EnsureDefaultModularSetup()
    {
        PlayerPowerUpsPresetValidationUtility.ValidateMetadata(this);
        PlayerPowerUpsPresetValidationUtility.ValidateCollections(this);
        int moduleCountBefore = moduleDefinitions.Count;
        int activeCountBefore = activePowerUps.Count;
        int passiveCountBefore = passivePowerUps.Count;
        PlayerPowerUpsPresetDefaultsUtility.GenerateDefaultModularSetupIfEmpty(this);

        if (moduleDefinitions.Count == moduleCountBefore &&
            activePowerUps.Count == activeCountBefore &&
            passivePowerUps.Count == passiveCountBefore)
        {
            return false;
        }

        PlayerPowerUpsPresetValidationUtility.ValidateEntries(this);
        return true;
    }

    /// <summary>
    /// Reorders one modular power-up entry inside the active or passive catalog owned by this preset.
    /// Used by editor tooling to avoid fragile serialized-array moves on large nested payload graphs.
    /// </summary>
    /// <param name="isActiveSection">True when the active power-up list should be reordered, false for the passive list.</param>
    /// <param name="fromIndex">Current index of the entry to move.</param>
    /// <param name="toIndex">Destination index after the move.</param>
    /// <returns>True when the reorder was applied, otherwise false.</returns>
    public bool MovePowerUpDefinition(bool isActiveSection, int fromIndex, int toIndex)
    {
        List<ModularPowerUpDefinition> powerUpDefinitions = isActiveSection
            ? activePowerUps
            : passivePowerUps;

        return MoveListElement(powerUpDefinitions, fromIndex, toIndex);
    }

    /// <summary>
    /// Reorders one reusable module definition inside this preset module catalog.
    /// Used by editor tooling while keeping existing module instances intact.
    /// </summary>
    /// <param name="fromIndex">Current index of the module definition to move.</param>
    /// <param name="toIndex">Destination index after the move.</param>
    /// <returns>True when the reorder was applied, otherwise false.</returns>
    public bool MoveModuleDefinition(int fromIndex, int toIndex)
    {
        return MoveListElement(moduleDefinitions, fromIndex, toIndex);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Moves one list element to a new index without cloning or recreating the referenced payload graph.
    /// </summary>
    /// <typeparam name="T">Element type stored by the list.</typeparam>
    /// <param name="elements">List that owns the element to move.</param>
    /// <param name="fromIndex">Current element index.</param>
    /// <param name="toIndex">Destination element index.</param>
    /// <returns>True when the move was applied, otherwise false.</returns>
    private static bool MoveListElement<T>(List<T> elements, int fromIndex, int toIndex)
    {
        if (elements == null)
            return false;

        if (fromIndex < 0 || fromIndex >= elements.Count)
            return false;

        if (toIndex < 0 || toIndex >= elements.Count)
            return false;

        if (fromIndex == toIndex)
            return false;

        T movedElement = elements[fromIndex];
        elements.RemoveAt(fromIndex);
        elements.Insert(toIndex, movedElement);
        return true;
    }
    #endregion

    #region Unity Methods
    private void OnValidate()
    {
        PlayerPowerUpsPresetValidationUtility.ValidateMetadata(this);
        PlayerPowerUpsPresetValidationUtility.ValidateCollections(this);
        PlayerPowerUpsPresetValidationUtility.ValidateEntries(this);
    }
    #endregion

    #endregion
}
