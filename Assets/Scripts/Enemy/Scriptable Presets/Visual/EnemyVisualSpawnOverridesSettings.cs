using System;
using UnityEngine;

/// <summary>
/// Stores enemy-type overrides for spawner-dependent spawn offset and spawn warning settings.
/// </summary>
[Serializable]
public sealed class EnemyVisualSpawnOverridesSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("When enabled, this enemy type adds its own local-space offset to every spawner-authored spawn position.")]
    [SerializeField] private bool overrideSpawnOffset;

    [Tooltip("Local-space offset added after the spawner grid and cell placement have resolved the spawn position.")]
    [SerializeField] private Vector3 spawnOffset;

    [Tooltip("When enabled, this enemy type replaces the owning spawner spawn warning settings for its own spawn events.")]
    [SerializeField] private bool overrideSpawnWarning;

    [Tooltip("Enables warning rings for this enemy type when Spawn Warning override is active.")]
    [SerializeField] private bool enableSpawnWarning = true;

    [Tooltip("Seconds of anticipation shown before this enemy type becomes active.")]
    [Range(0f, 3f)]
    [SerializeField] private float spawnWarningLeadTimeSeconds = 0.7f;

    [Tooltip("Ring world radius resolved as the spawner Cell Size multiplied by this scale.")]
    [Range(0.1f, 2f)]
    [SerializeField] private float spawnWarningRadiusScale = 0.45f;

    [Tooltip("World-space line width used by this enemy type warning ring.")]
    [Range(0.02f, 1f)]
    [SerializeField] private float spawnWarningRingWidth = 0.15f;

    [Tooltip("Extra vertical lift applied to this enemy type warning ring above the spawn plane.")]
    [Range(0f, 1f)]
    [SerializeField] private float spawnWarningHeightOffset = 0.06f;

    [Tooltip("Maximum opacity reached by this enemy type warning ring right before spawning.")]
    [Range(0f, 1f)]
    [SerializeField] private float spawnWarningMaximumAlpha = 0.95f;

    [Tooltip("Seconds used to softly fade this enemy type warning ring after the enemy has spawned.")]
    [Range(0f, 1f)]
    [SerializeField] private float spawnWarningFadeOutSeconds = 0.18f;

    [Tooltip("Tint color used by this enemy type spawn warning ring.")]
    [SerializeField] private Color spawnWarningColor = new Color(1f, 0.72f, 0.18f, 1f);
    #endregion

    #endregion

    #region Properties
    public bool OverrideSpawnOffset
    {
        get
        {
            return overrideSpawnOffset;
        }
    }

    public Vector3 SpawnOffset
    {
        get
        {
            return spawnOffset;
        }
    }

    public bool OverrideSpawnWarning
    {
        get
        {
            return overrideSpawnWarning;
        }
    }

    public bool EnableSpawnWarning
    {
        get
        {
            return enableSpawnWarning;
        }
    }

    public float SpawnWarningLeadTimeSeconds
    {
        get
        {
            return spawnWarningLeadTimeSeconds;
        }
    }

    public float SpawnWarningRadiusScale
    {
        get
        {
            return spawnWarningRadiusScale;
        }
    }

    public float SpawnWarningRingWidth
    {
        get
        {
            return spawnWarningRingWidth;
        }
    }

    public float SpawnWarningHeightOffset
    {
        get
        {
            return spawnWarningHeightOffset;
        }
    }

    public float SpawnWarningMaximumAlpha
    {
        get
        {
            return spawnWarningMaximumAlpha;
        }
    }

    public float SpawnWarningFadeOutSeconds
    {
        get
        {
            return spawnWarningFadeOutSeconds;
        }
    }

    public Color SpawnWarningColor
    {
        get
        {
            return spawnWarningColor;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Keeps the settings object structurally valid without snapping authored override values.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}
