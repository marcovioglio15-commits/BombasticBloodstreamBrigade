using System;
using UnityEngine;

/// <summary>
/// Selects how a death puddle resolves its initial world-space footprint.
/// </summary>
public enum EnemyDeathPuddleSizeMode : byte
{
    EnemyFootprint = 0,
    FixedWorldSize = 1
}

/// <summary>
/// Selects the shader-side evaporation curve used by a death puddle.
/// </summary>
public enum EnemyDeathPuddleEvaporationCurve : byte
{
    SmoothStep = 0,
    Linear = 1,
    EaseIn = 2
}

/// <summary>
/// Stores render-only death puddle settings resolved by an enemy visual preset.
/// </summary>
[Serializable]
public sealed class EnemyVisualDeathPuddleSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables a pooled ECS render-only puddle when this enemy is killed.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Optional ECS-compatible puddle prefab override. Empty uses the shared standard enemy death puddle prefab.")]
    [SerializeField] private GameObject puddlePrefab;

    [Tooltip("Total puddle lifetime in seconds before the pooled entity is released.")]
    [Range(0.1f, 30f)]
    [SerializeField] private float lifetimeSeconds = 4f;

    [Tooltip("Normalized lifetime fraction during which the puddle remains close to its initial size.")]
    [Range(0f, 0.95f)]
    [SerializeField] private float stableFraction = 0.2f;

    [Tooltip("Remaining footprint ratio reached immediately before the puddle disappears.")]
    [Range(0f, 1f)]
    [SerializeField] private float finalScaleRatio = 0.08f;

    [Tooltip("Shader-side curve used while the puddle shrinks and fades.")]
    [SerializeField] private EnemyDeathPuddleEvaporationCurve evaporationCurve = EnemyDeathPuddleEvaporationCurve.SmoothStep;

    [Tooltip("Controls whether the puddle follows the killed enemy footprint or uses a fixed world size.")]
    [SerializeField] private EnemyDeathPuddleSizeMode sizeMode = EnemyDeathPuddleSizeMode.EnemyFootprint;

    [Tooltip("Multiplier applied to the killed enemy XZ body footprint when Enemy Footprint size mode is selected.")]
    [Range(0.1f, 4f)]
    [SerializeField] private float footprintScaleMultiplier = 1.1f;

    [Tooltip("Fixed world-space puddle width and depth used when Fixed World Size mode is selected.")]
    [SerializeField] private Vector2 fixedWorldSize = Vector2.one;

    [Tooltip("Maximum deterministic per-instance size variation applied symmetrically around the configured size.")]
    [Range(0f, 0.75f)]
    [SerializeField] private float randomSizeVariation = 0.12f;

    [Tooltip("Applies a deterministic random world-space rotation to each spawned puddle.")]
    [SerializeField] private bool randomRotation = true;

    [Tooltip("Vertical world-space offset used to keep the puddle above the floor without visible hovering.")]
    [Range(-0.1f, 0.5f)]
    [SerializeField] private float groundOffset = 0.012f;

    [Tooltip("Controls procedural irregularity of the puddle silhouette.")]
    [Range(0f, 1f)]
    [SerializeField] private float edgeIrregularity = 0.28f;

    [Tooltip("Normalized width of the dark outer puddle border.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float borderWidth = 0.1f;

    [Tooltip("Normalized softness applied only to the puddle outer edge.")]
    [Range(0.001f, 0.5f)]
    [SerializeField] private float edgeFeather = 0.04f;

    [Tooltip("Blend strength of the secondary sampled palette color inside the puddle body.")]
    [Range(0f, 1f)]
    [SerializeField] private float secondaryPaletteBlend = 0.55f;

    [Tooltip("Speed of the procedural liquid flow across the puddle surface.")]
    [Range(0f, 3f)]
    [SerializeField] private float flowSpeed = 0.35f;

    [Tooltip("Controls how slowly and broadly the liquid deforms, where one produces the thickest motion.")]
    [Range(0f, 1f)]
    [SerializeField] private float viscosity = 0.7f;

    [Tooltip("Maximum procedural displacement applied to the puddle body and silhouette.")]
    [Range(0f, 0.35f)]
    [SerializeField] private float surfaceDistortion = 0.08f;

    [Tooltip("Strength of the animated glossy highlight moving across the puddle body.")]
    [Range(0f, 1f)]
    [SerializeField] private float highlightStrength = 0.18f;
    #endregion

    #endregion

    #region Properties
    public bool Enabled => enabled;
    public GameObject PuddlePrefab => puddlePrefab;
    public float LifetimeSeconds => lifetimeSeconds;
    public float StableFraction => stableFraction;
    public float FinalScaleRatio => finalScaleRatio;
    public EnemyDeathPuddleEvaporationCurve EvaporationCurve => evaporationCurve;
    public EnemyDeathPuddleSizeMode SizeMode => sizeMode;
    public float FootprintScaleMultiplier => footprintScaleMultiplier;
    public Vector2 FixedWorldSize => fixedWorldSize;
    public float RandomSizeVariation => randomSizeVariation;
    public bool RandomRotation => randomRotation;
    public float GroundOffset => groundOffset;
    public float EdgeIrregularity => edgeIrregularity;
    public float BorderWidth => borderWidth;
    public float EdgeFeather => edgeFeather;
    public float SecondaryPaletteBlend => secondaryPaletteBlend;
    public float FlowSpeed => flowSpeed;
    public float Viscosity => viscosity;
    public float SurfaceDistortion => surfaceDistortion;
    public float HighlightStrength => highlightStrength;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Preserves authored values so the management tool can report inconsistencies without silently snapping data.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}
