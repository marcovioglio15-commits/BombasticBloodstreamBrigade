using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores player outline presentation settings applied to managed renderers.
/// </summary>
[Serializable]
public sealed class PlayerVisualOutlineSettings
{
    #region Constants
    private const float MinimumOutlineThickness = 0f;
    private const float MaximumOutlineThickness = 10f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("When enabled, compatible player renderers receive outline property overrides from this preset.")]
    [SerializeField] private bool enableOutline = true;

    [Tooltip("Outline thickness written to compatible player materials exposing _OutlineThickness. Matches the shader authoring range 0-10.")]
    [Range(MinimumOutlineThickness, MaximumOutlineThickness)]
    [SerializeField] private float outlineThickness = 1f;

    [Tooltip("Outline color written to compatible player materials exposing _OutlineColor.")]
    [SerializeField] private Color outlineColor = Color.black;
    #endregion

    #endregion

    #region Properties
    public bool EnableOutline
    {
        get
        {
            return enableOutline;
        }
    }

    public float OutlineThickness
    {
        get
        {
            return outlineThickness;
        }
    }

    public Color OutlineColor
    {
        get
        {
            return outlineColor;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates outline authored values after inspector edits.
    /// None.
    /// </summary>
    public void Validate()
    {
        outlineColor.a = Mathf.Clamp01(outlineColor.a);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores runtime bridge, damage feedback and player-facing power-up VFX settings shared by one visual setup.
/// </summary>
[CreateAssetMenu(fileName = "PlayerVisualPreset", menuName = "Player/Visual Preset", order = 10)]
public sealed class PlayerVisualPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this visual preset, used for stable references.")]
    [SerializeField] private string presetId;

    [Tooltip("Visual preset name shown in the Player Management Tool.")]
    [SerializeField] private string presetName = "New Player Visual Preset";

    [Tooltip("Short description of the visual setup handled by this preset.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this visual preset.")]
    [SerializeField] private string version = "1.0.0";

    [Header("Runtime Bridge")]
    [Tooltip("Optional visual-only prefab instantiated at runtime when no valid companion Animator exists on the player entity.")]
    [SerializeField] private GameObject runtimeVisualBridgePrefab;

    [Tooltip("When enabled, the runtime visual bridge is spawned only when the player entity has no valid Animator companion.")]
    [SerializeField] private bool spawnRuntimeVisualBridgeWhenAnimatorMissing = true;

    [Tooltip("When enabled, the runtime visual bridge follows the ECS player rotation.")]
    [SerializeField] private bool runtimeVisualBridgeSyncRotation = true;

    [Tooltip("Local-space position offset applied to the runtime visual bridge relative to the ECS player transform.")]
    [SerializeField] private Vector3 runtimeVisualBridgeOffset = Vector3.zero;

    [Header("Outline")]
    [Tooltip("Outline settings applied to compatible player renderers.")]
    [SerializeField] private PlayerVisualOutlineSettings outline = new PlayerVisualOutlineSettings();

    [Header("Damage Feedback")]
    [Tooltip("Tint color applied during the brief damage flash after the player receives valid damage.")]
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.15f, 0.15f, 1f);

    [Tooltip("Flash duration in seconds. Use very small values for a 1-3 frame reaction.")]
    [SerializeField] private float damageFlashDurationSeconds = 0.06f;

    [Tooltip("Maximum overlay strength reached immediately after a valid hit.")]
    [SerializeField] private float damageFlashMaximumBlend = 0.85f;

    [Header("VFX")]
    [Tooltip("Optional attached VFX prefab activated while Elemental Trail passive is enabled.")]
    [SerializeField] private GameObject elementalTrailAttachedVfxPrefab;

    [Tooltip("Scale multiplier applied to the attached Elemental Trail VFX instance.")]
    [SerializeField] private float elementalTrailAttachedVfxScaleMultiplier = 1f;

    [Tooltip("Per-element enemy VFX assignments used when elemental player bullets or trail effects apply stacks and procs.")]
    [SerializeField] private List<ElementalVfxByElementData> elementalEnemyVfxByElement = new List<ElementalVfxByElementData>();

    [Tooltip("Maximum number of identical one-shot VFX allowed in the same spatial cell. Set 0 to disable this cap.")]
    [SerializeField] private int maxIdenticalOneShotVfxPerCell = 1;

    [Tooltip("Cell size in meters used by the one-shot VFX per-cell cap.")]
    [SerializeField] private float oneShotVfxCellSize = 1.5f;

    [Tooltip("Maximum number of identical attached elemental VFX allowed on the same target. Set 0 to disable this cap.")]
    [SerializeField] private int maxAttachedElementalVfxPerTarget = 1;

    [Tooltip("Maximum number of active one-shot power-up VFX managed by one player. Set 0 to disable this cap.")]
    [SerializeField] private int maxActiveOneShotPowerUpVfx = 300;

    [Tooltip("When enabled, hitting the attached-target cap refreshes lifetime of the existing VFX.")]
    [SerializeField] private bool refreshAttachedElementalVfxLifetimeOnCapHit = true;

    [Tooltip("Shared material and palette settings used by the Laser Beam managed visuals.")]
    [SerializeField] private PlayerLaserBeamVisualSettings laserBeam = new PlayerLaserBeamVisualSettings();

    [Tooltip("Optional one-shot VFX prefab spawned on the player when a level-up is registered.")]
    [SerializeField] private GameObject levelUpVfxPrefab;

    [Tooltip("Local-space offset applied to Level-Up VFX relative to the player entity position.")]
    [SerializeField] private Vector3 levelUpVfxSpawnOffset = Vector3.zero;

    [Tooltip("Uniform scale multiplier applied to the Level-Up VFX instance.")]
    [SerializeField] private float levelUpVfxScaleMultiplier = 1f;

    [Tooltip("Controls whether Level-Up VFX appears on every level-up or only when the level reaches a milestone power-up selection.")]
    [SerializeField] private PlayerLevelUpVfxTriggerMode levelUpVfxTriggerMode = PlayerLevelUpVfxTriggerMode.EveryLevelUp;

    [Tooltip("Optional one-shot VFX prefab spawned on the player when health increases.")]
    [SerializeField] private GameObject healthIncreaseVfxPrefab;

    [Tooltip("Local-space offset applied to Health Increase VFX relative to the player entity position.")]
    [SerializeField] private Vector3 healthIncreaseVfxSpawnOffset = Vector3.zero;

    [Tooltip("Uniform scale multiplier applied to the Health Increase VFX instance.")]
    [SerializeField] private float healthIncreaseVfxScaleMultiplier = 1f;

    [Tooltip("Controls whether Health Increase VFX appears on every health gain or only when maximum health increases.")]
    [SerializeField] private PlayerStatIncreaseVfxTriggerMode healthIncreaseVfxTriggerMode = PlayerStatIncreaseVfxTriggerMode.EveryIncrease;

    [Tooltip("Optional one-shot VFX prefab spawned on the player when shield increases.")]
    [SerializeField] private GameObject shieldIncreaseVfxPrefab;

    [Tooltip("Local-space offset applied to Shield Increase VFX relative to the player entity position.")]
    [SerializeField] private Vector3 shieldIncreaseVfxSpawnOffset = Vector3.zero;

    [Tooltip("Uniform scale multiplier applied to the Shield Increase VFX instance.")]
    [SerializeField] private float shieldIncreaseVfxScaleMultiplier = 1f;

    [Tooltip("Controls whether Shield Increase VFX appears on every shield gain or only when maximum shield increases.")]
    [SerializeField] private PlayerStatIncreaseVfxTriggerMode shieldIncreaseVfxTriggerMode = PlayerStatIncreaseVfxTriggerMode.EveryIncrease;

    [Tooltip("Optional VFX prefab displayed while a Charge Shot active tool is charging.")]
    [SerializeField] private GameObject chargeShotVfxPrefab;

    [Tooltip("Yaw-relative offset applied to Charge Shot VFX from the muzzle pose. Positive Y always moves upward in world space.")]
    [SerializeField] private Vector3 chargeShotVfxSpawnOffset = Vector3.zero;

    [Tooltip("Uniform scale multiplier applied to the Charge Shot VFX instance.")]
    [SerializeField] private float chargeShotVfxScaleMultiplier = 1f;

    [Tooltip("Controls whether Charge Shot VFX plays once near completion, loops while charging, or stretches one playback over the whole charge.")]
    [SerializeField] private PlayerChargeShotVfxPlaybackMode chargeShotVfxPlaybackMode = PlayerChargeShotVfxPlaybackMode.PlayOnceTimedWithChargeCompletion;

    [Tooltip("When enabled, Charge Shot VFX is also displayed while any active power-up built from a hold-charge module is charging.")]
    [SerializeField] private bool chargeShotVfxAppliesToAllHoldChargePowerUps;

    [Tooltip("Optional attached VFX prefab spawned on every player projectile and followed until the projectile despawns.")]
    [SerializeField] private GameObject playerProjectileVfxPrefab;

    [Tooltip("Local-space offset applied to projectile attached VFX relative to each projectile transform.")]
    [SerializeField] private Vector3 playerProjectileVfxSpawnOffset = Vector3.zero;

    [Tooltip("Uniform scale multiplier applied to projectile attached VFX instances.")]
    [SerializeField] private float playerProjectileVfxScaleMultiplier = 1f;

    #region Visual Pointer
    [Header("Visual Pointer")]
    [Tooltip("When enabled, a precision laser aiming pointer is rendered straight out of the weapon muzzle along the current shot direction.")]
    [SerializeField] private bool enablePointer;

    [Tooltip("Laser Beam visual preset ID whose palette colors are reused to tint the aiming pointer. Resolved against the Laser Beam visual presets authored above.")]
    [PlayerLaserBeamVisualPresetSelector]
    [SerializeField] private int pointerVisualPresetId = PlayerLaserBeamVisualDefaultsUtility.DefaultVisualPresetId;

    [Tooltip("Rendered beam diameter of the aiming pointer in world units.")]
    [SerializeField] private float pointerWidth = 0.05f;

    [Tooltip("Multiplier applied to the projectile range-derived length so the pointer can be shortened or extended relative to the real shot reach.")]
    [SerializeField] private float pointerLengthMultiplier = 1f;

    [Tooltip("Optional hard cap on the rendered pointer length in world units. Set 0 to follow the resolved projectile range without a cap.")]
    [SerializeField] private float pointerMaxLength;

    [Tooltip("Opacity multiplier applied to the aiming pointer body. Use lower values for a subtle sight line.")]
    [SerializeField] private float pointerOpacity = 0.7f;

    [Tooltip("Vertical lift in world units applied to the aiming pointer to avoid floor z-fighting.")]
    [SerializeField] private float pointerVerticalLift = PlayerLaserBeamVisualDefaultsUtility.DefaultVerticalLift;

    [Tooltip("When enabled, the pointer length stops adapting to the projectile range and stays fixed while the Orbital Projectiles (Perfect Circle) power-up is active, since orbiting shots no longer travel in a straight line.")]
    [SerializeField] private bool freezePointerWithOrbitalProjectiles = true;

    [Tooltip("Fixed pointer length in world units used while the Orbital Projectiles power-up is active and freezing is enabled. Set 0 to keep the authored base shot range instead.")]
    [SerializeField] private float pointerOrbitalFrozenLength;
    #endregion

    [Header("Scaling")]
    [Tooltip("Add Scaling rules applied to supported visual preset fields at bake time without mutating this asset.")]
    [SerializeField] private List<PlayerStatScalingRule> scalingRules = new List<PlayerStatScalingRule>();
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

    public GameObject RuntimeVisualBridgePrefab
    {
        get
        {
            return runtimeVisualBridgePrefab;
        }
    }

    public bool SpawnRuntimeVisualBridgeWhenAnimatorMissing
    {
        get
        {
            return spawnRuntimeVisualBridgeWhenAnimatorMissing;
        }
    }

    public bool RuntimeVisualBridgeSyncRotation
    {
        get
        {
            return runtimeVisualBridgeSyncRotation;
        }
    }

    public Vector3 RuntimeVisualBridgeOffset
    {
        get
        {
            return runtimeVisualBridgeOffset;
        }
    }

    public PlayerVisualOutlineSettings Outline
    {
        get
        {
            return outline;
        }
    }

    public Color DamageFlashColor
    {
        get
        {
            return damageFlashColor;
        }
    }

    public float DamageFlashDurationSeconds
    {
        get
        {
            return damageFlashDurationSeconds;
        }
    }

    public float DamageFlashMaximumBlend
    {
        get
        {
            return damageFlashMaximumBlend;
        }
    }

    public GameObject ElementalTrailAttachedVfxPrefab
    {
        get
        {
            return elementalTrailAttachedVfxPrefab;
        }
    }

    public float ElementalTrailAttachedVfxScaleMultiplier
    {
        get
        {
            return elementalTrailAttachedVfxScaleMultiplier;
        }
    }

    public IReadOnlyList<ElementalVfxByElementData> ElementalEnemyVfxByElement
    {
        get
        {
            return elementalEnemyVfxByElement;
        }
    }

    public int MaxIdenticalOneShotVfxPerCell
    {
        get
        {
            return maxIdenticalOneShotVfxPerCell;
        }
    }

    public float OneShotVfxCellSize
    {
        get
        {
            return oneShotVfxCellSize;
        }
    }

    public int MaxAttachedElementalVfxPerTarget
    {
        get
        {
            return maxAttachedElementalVfxPerTarget;
        }
    }

    public int MaxActiveOneShotPowerUpVfx
    {
        get
        {
            return maxActiveOneShotPowerUpVfx;
        }
    }

    public bool RefreshAttachedElementalVfxLifetimeOnCapHit
    {
        get
        {
            return refreshAttachedElementalVfxLifetimeOnCapHit;
        }
    }

    public PlayerLaserBeamVisualSettings LaserBeam
    {
        get
        {
            return laserBeam;
        }
    }

    public GameObject LevelUpVfxPrefab
    {
        get
        {
            return levelUpVfxPrefab;
        }
    }

    public Vector3 LevelUpVfxSpawnOffset
    {
        get
        {
            return levelUpVfxSpawnOffset;
        }
    }

    public float LevelUpVfxScaleMultiplier
    {
        get
        {
            return levelUpVfxScaleMultiplier;
        }
    }

    public PlayerLevelUpVfxTriggerMode LevelUpVfxTriggerMode
    {
        get
        {
            return levelUpVfxTriggerMode;
        }
    }

    public GameObject HealthIncreaseVfxPrefab
    {
        get
        {
            return healthIncreaseVfxPrefab;
        }
    }

    public Vector3 HealthIncreaseVfxSpawnOffset
    {
        get
        {
            return healthIncreaseVfxSpawnOffset;
        }
    }

    public float HealthIncreaseVfxScaleMultiplier
    {
        get
        {
            return healthIncreaseVfxScaleMultiplier;
        }
    }

    public PlayerStatIncreaseVfxTriggerMode HealthIncreaseVfxTriggerMode
    {
        get
        {
            return healthIncreaseVfxTriggerMode;
        }
    }

    public GameObject ShieldIncreaseVfxPrefab
    {
        get
        {
            return shieldIncreaseVfxPrefab;
        }
    }

    public Vector3 ShieldIncreaseVfxSpawnOffset
    {
        get
        {
            return shieldIncreaseVfxSpawnOffset;
        }
    }

    public float ShieldIncreaseVfxScaleMultiplier
    {
        get
        {
            return shieldIncreaseVfxScaleMultiplier;
        }
    }

    public PlayerStatIncreaseVfxTriggerMode ShieldIncreaseVfxTriggerMode
    {
        get
        {
            return shieldIncreaseVfxTriggerMode;
        }
    }

    public GameObject ChargeShotVfxPrefab
    {
        get
        {
            return chargeShotVfxPrefab;
        }
    }

    public Vector3 ChargeShotVfxSpawnOffset
    {
        get
        {
            return chargeShotVfxSpawnOffset;
        }
    }

    public float ChargeShotVfxScaleMultiplier
    {
        get
        {
            return chargeShotVfxScaleMultiplier;
        }
    }

    public PlayerChargeShotVfxPlaybackMode ChargeShotVfxPlaybackMode
    {
        get
        {
            return chargeShotVfxPlaybackMode;
        }
    }

    public bool ChargeShotVfxAppliesToAllHoldChargePowerUps
    {
        get
        {
            return chargeShotVfxAppliesToAllHoldChargePowerUps;
        }
    }

    public GameObject PlayerProjectileVfxPrefab
    {
        get
        {
            return playerProjectileVfxPrefab;
        }
    }

    public Vector3 PlayerProjectileVfxSpawnOffset
    {
        get
        {
            return playerProjectileVfxSpawnOffset;
        }
    }

    public float PlayerProjectileVfxScaleMultiplier
    {
        get
        {
            return playerProjectileVfxScaleMultiplier;
        }
    }

    public bool EnablePointer
    {
        get
        {
            return enablePointer;
        }
    }

    public int PointerVisualPresetId
    {
        get
        {
            return pointerVisualPresetId;
        }
    }

    public float PointerWidth
    {
        get
        {
            return pointerWidth;
        }
    }

    public float PointerLengthMultiplier
    {
        get
        {
            return pointerLengthMultiplier;
        }
    }

    public float PointerMaxLength
    {
        get
        {
            return pointerMaxLength;
        }
    }

    public float PointerOpacity
    {
        get
        {
            return pointerOpacity;
        }
    }

    public float PointerVerticalLift
    {
        get
        {
            return pointerVerticalLift;
        }
    }

    public bool FreezePointerWithOrbitalProjectiles
    {
        get
        {
            return freezePointerWithOrbitalProjectiles;
        }
    }

    public float PointerOrbitalFrozenLength
    {
        get
        {
            return pointerOrbitalFrozenLength;
        }
    }

    public IReadOnlyList<PlayerStatScalingRule> ScalingRules
    {
        get
        {
            return scalingRules;
        }
    }
    #endregion

    #region Methods

    #region Internal API
    internal List<ElementalVfxByElementData> ElementalEnemyVfxByElementMutable
    {
        get
        {
            return elementalEnemyVfxByElement;
        }
        set
        {
            elementalEnemyVfxByElement = value;
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

    /// <summary>
    /// Imports legacy elemental enemy VFX assignments from another preset when this visual preset does not already own authored data.
    /// </summary>
    /// <param name="sourceAssignments">Legacy assignment list to copy.</param>
    /// <returns>True when the visual preset was updated.</returns>
    internal bool ImportElementalEnemyVfxAssignments(IReadOnlyList<ElementalVfxByElementData> sourceAssignments)
    {
        if (PlayerElementalVfxAssignmentUtility.HasAnyConfiguredVfx(elementalEnemyVfxByElement))
            return false;

        return PlayerElementalVfxAssignmentUtility.CopyAssignments(sourceAssignments, elementalEnemyVfxByElement);
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Ensures the preset keeps a stable ID after edits and duplications.
    /// None.
    /// </summary>
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (outline == null)
            outline = new PlayerVisualOutlineSettings();

        if (elementalEnemyVfxByElement == null)
            elementalEnemyVfxByElement = new List<ElementalVfxByElementData>();

        if (laserBeam == null)
            laserBeam = new PlayerLaserBeamVisualSettings();

        if (scalingRules == null)
            scalingRules = new List<PlayerStatScalingRule>();

        outline.Validate();
        laserBeam.Validate();
        ValidateScalingRules();
        PlayerElementalVfxAssignmentUtility.ValidateAssignments(elementalEnemyVfxByElement);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Removes empty Add Scaling rows and validates existing rules after inspector edits.
    /// </summary>
    private void ValidateScalingRules()
    {
        for (int index = 0; index < scalingRules.Count; index++)
        {
            PlayerStatScalingRule scalingRule = scalingRules[index];

            if (scalingRule != null)
                continue;

            scalingRule = new PlayerStatScalingRule();
            scalingRule.Configure(string.Empty, false, string.Empty);
            scalingRules[index] = scalingRule;
        }

        for (int index = scalingRules.Count - 1; index >= 0; index--)
        {
            PlayerStatScalingRule scalingRule = scalingRules[index];
            scalingRule.Validate();

            if (!string.IsNullOrWhiteSpace(scalingRule.StatKey))
                continue;

            scalingRules.RemoveAt(index);
        }
    }
    #endregion

    #endregion
}
