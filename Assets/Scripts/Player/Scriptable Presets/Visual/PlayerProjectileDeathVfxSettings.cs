using System;
using UnityEngine;

/// <summary>
/// Stores one projectile-death VFX event configuration shared by range/lifetime expiry and terminal wall impacts.
/// </summary>
[Serializable]
public sealed class PlayerProjectileDeathVfxEventSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("When enabled, this VFX event can spawn when its projectile despawn condition is reached without any previous valid enemy hit.")]
    [SerializeField] private bool enabled;

    [Tooltip("One-shot VFX prefab spawned for this projectile despawn event. Terminal Wall Hit can leave this empty to reuse the Range Or Lifetime prefab.")]
    [SerializeField] private GameObject vfxPrefab;

    [Tooltip("Projectile-local offset applied at the final projectile pose. The offset scales with the current projectile size.")]
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;

    [Tooltip("Uniform VFX scale multiplier applied on top of the current projectile size.")]
    [SerializeField] private float scaleMultiplier = 1f;

    [Tooltip("Lifetime in seconds before the spawned one-shot VFX returns to the managed VFX pool.")]
    [SerializeField] private float lifetimeSeconds = 0.5f;
    #endregion

    #endregion

    #region Properties
    public bool Enabled
    {
        get
        {
            return enabled;
        }
    }

    public GameObject VfxPrefab
    {
        get
        {
            return vfxPrefab;
        }
    }

    public Vector3 SpawnOffset
    {
        get
        {
            return spawnOffset;
        }
    }

    public float ScaleMultiplier
    {
        get
        {
            return scaleMultiplier;
        }
    }

    public float LifetimeSeconds
    {
        get
        {
            return lifetimeSeconds;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Reports invalid authored values without mutating the visual preset.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name used in warning messages.</param>
    /// <param name="eventLabel">User-facing projectile despawn event label.</param>
    /// <param name="fallbackPrefab">Optional fallback prefab accepted when this event has no direct assignment.</param>
    public void Validate(string ownerAssetName, string eventLabel, GameObject fallbackPrefab)
    {
        if (!enabled)
            return;

        if (vfxPrefab == null && fallbackPrefab == null)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Projectile Death VFX / {1}: event is enabled but no VFX prefab is assigned.", ownerAssetName, eventLabel));

        if (!IsFinite(spawnOffset))
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Projectile Death VFX / {1}: spawn offset contains an invalid numeric value.", ownerAssetName, eventLabel));

        if (!IsFinite(scaleMultiplier) || scaleMultiplier <= 0f)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Projectile Death VFX / {1}: scale multiplier should be finite and greater than zero.", ownerAssetName, eventLabel));

        if (!IsFinite(lifetimeSeconds) || lifetimeSeconds <= 0f)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Projectile Death VFX / {1}: lifetime should be finite and greater than zero.", ownerAssetName, eventLabel));
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Checks whether every vector component is finite.
    /// </summary>
    /// <param name="value">Vector value to inspect.</param>
    /// <returns>True when every component is finite.</returns>
    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    /// <summary>
    /// Checks whether one floating-point value is finite.
    /// </summary>
    /// <param name="value">Value to inspect.</param>
    /// <returns>True when the value is neither NaN nor infinity.</returns>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores player projectile-death VFX settings for natural expiry and non-bouncing terminal wall impacts.
/// </summary>
[Serializable]
public sealed class PlayerProjectileDeathVfxSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("VFX event spawned when a projectile reaches its configured range or lifetime without any previous valid enemy hit.")]
    [SerializeField] private PlayerProjectileDeathVfxEventSettings rangeOrLifetime = new PlayerProjectileDeathVfxEventSettings();

    [Tooltip("VFX event spawned when a projectile despawns on a wall after all configured projectile bounces are unavailable, provided it has not previously hit an enemy.")]
    [SerializeField] private PlayerProjectileDeathVfxEventSettings terminalWallHit = new PlayerProjectileDeathVfxEventSettings();
    #endregion

    #endregion

    #region Properties
    public PlayerProjectileDeathVfxEventSettings RangeOrLifetime
    {
        get
        {
            return rangeOrLifetime;
        }
    }

    public PlayerProjectileDeathVfxEventSettings TerminalWallHit
    {
        get
        {
            return terminalWallHit;
        }
    }

    public bool HasAnyPrefab
    {
        get
        {
            return rangeOrLifetime != null && rangeOrLifetime.VfxPrefab != null ||
                   terminalWallHit != null && terminalWallHit.VfxPrefab != null;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Reports missing nested event settings and invalid authored values without snapping them.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name used in warning messages.</param>
    public void Validate(string ownerAssetName)
    {
        if (rangeOrLifetime == null)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Projectile Death VFX: Range Or Lifetime settings are missing.", ownerAssetName));
        else
            rangeOrLifetime.Validate(ownerAssetName, "Range Or Lifetime", null);

        if (terminalWallHit == null)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Projectile Death VFX: Terminal Wall Hit settings are missing.", ownerAssetName));
        else
            terminalWallHit.Validate(ownerAssetName,
                                     "Terminal Wall Hit",
                                     rangeOrLifetime != null ? rangeOrLifetime.VfxPrefab : null);
    }
    #endregion

    #endregion
}
