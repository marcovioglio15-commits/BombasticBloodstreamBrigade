using System;
using UnityEngine;

/// <summary>
/// Stores enemy face flipbook atlas settings, playback timings and temporary reaction durations.
/// </summary>
[Serializable]
public sealed class EnemyVisualFaceFlipbookSettings
{
    #region Fields

    #region Serialized Fields
    [Header("Runtime")]
    [Tooltip("Enables shader-driven enemy face state playback for enemies using the shared face material.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Idle face atlas playback used whenever no temporary attack or damage face is active.")]
    [SerializeField] private EnemyFaceFlipbookStateSettings idle = EnemyFaceFlipbookStateSettings.CreateIdle();

    [Tooltip("Attack face atlas playback triggered by offensive engagement behaviour with its own independent duration.")]
    [SerializeField] private EnemyFaceFlipbookStateSettings attack = EnemyFaceFlipbookStateSettings.CreateAttack();

    [Tooltip("Damage face atlas playback triggered when the enemy receives damage with its own independent duration.")]
    [SerializeField] private EnemyFaceFlipbookStateSettings damage = EnemyFaceFlipbookStateSettings.CreateDamage();
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

    public EnemyFaceFlipbookStateSettings Idle
    {
        get
        {
            return idle;
        }
    }

    public EnemyFaceFlipbookStateSettings Attack
    {
        get
        {
            return attack;
        }
    }

    public EnemyFaceFlipbookStateSettings Damage
    {
        get
        {
            return damage;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates nested face flipbook settings without mutating authored values.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name used in warning messages.</param>
    public void Validate(string ownerAssetName)
    {
        string resolvedOwnerName = string.IsNullOrWhiteSpace(ownerAssetName) ? "Unnamed Visual Preset" : ownerAssetName;

        if (idle == null)
            Debug.LogWarning(string.Format("[EnemyVisualPreset] '{0}' - Face Flipbook: Idle settings are missing.", resolvedOwnerName));
        else
            idle.Validate(resolvedOwnerName, "Idle", false);

        if (attack == null)
            Debug.LogWarning(string.Format("[EnemyVisualPreset] '{0}' - Face Flipbook: Attack settings are missing.", resolvedOwnerName));
        else
            attack.Validate(resolvedOwnerName, "Attack", true);

        if (damage == null)
            Debug.LogWarning(string.Format("[EnemyVisualPreset] '{0}' - Face Flipbook: Damage settings are missing.", resolvedOwnerName));
        else
            damage.Validate(resolvedOwnerName, "Damage", true);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one face atlas state grid and playback definition used by the shared enemy face shader.
/// </summary>
[Serializable]
public sealed class EnemyFaceFlipbookStateSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables this face state. Disabled temporary states fall back to the idle face.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Optional source atlas reference documented on the preset. The shared face material still owns the runtime texture slot used by Entities Graphics.")]
    [SerializeField] private Texture2D atlas;

    [Tooltip("Number of atlas columns used by this face state.")]
    [SerializeField] private int columns = 4;

    [Tooltip("Number of atlas rows used by this face state.")]
    [SerializeField] private int rows = 2;

    [Tooltip("Number of valid frames inside this state's grid, read left-to-right then top-to-bottom.")]
    [SerializeField] private int frameCount = 8;

    [Tooltip("Playback speed for this face state, in frames per second.")]
    [SerializeField] private float framesPerSecond = 8f;

    [Tooltip("Frame index offset used when the first authored frame is not the first cell in the grid.")]
    [SerializeField] private float startFrame;

    [Tooltip("Duration in seconds for temporary Attack and Damage face playback. Idle ignores this value.")]
    [SerializeField] private float durationSeconds = 0.16f;
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

    public Texture2D Atlas
    {
        get
        {
            return atlas;
        }
    }

    public int Columns
    {
        get
        {
            return columns;
        }
    }

    public int Rows
    {
        get
        {
            return rows;
        }
    }

    public int FrameCount
    {
        get
        {
            return frameCount;
        }
    }

    public float FramesPerSecond
    {
        get
        {
            return framesPerSecond;
        }
    }

    public float StartFrame
    {
        get
        {
            return startFrame;
        }
    }

    public float DurationSeconds
    {
        get
        {
            return durationSeconds;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates default idle settings for the two-row idle atlas.
    /// </summary>
    /// <returns>Idle face settings with a four-by-two grid and eight frames.</returns>
    public static EnemyFaceFlipbookStateSettings CreateIdle()
    {
        return Create(true, 4, 2, 8, 8f, 0f, 0f);
    }

    /// <summary>
    /// Creates default attack settings for the one-row attack atlas.
    /// </summary>
    /// <returns>Attack face settings with a four-by-one grid and four frames.</returns>
    public static EnemyFaceFlipbookStateSettings CreateAttack()
    {
        return Create(true, 4, 1, 4, 10f, 0f, 0.18f);
    }

    /// <summary>
    /// Creates default damage settings for the one-row damage atlas.
    /// </summary>
    /// <returns>Damage face settings with a four-by-one grid and four frames.</returns>
    public static EnemyFaceFlipbookStateSettings CreateDamage()
    {
        return Create(true, 4, 1, 4, 12f, 0f, 0.14f);
    }

    /// <summary>
    /// Validates one state definition without mutating authored values.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name used in warning messages.</param>
    /// <param name="stateName">State label used in warning messages.</param>
    /// <param name="requiresDuration">Whether this state uses the temporary duration value.</param>
    public void Validate(string ownerAssetName, string stateName, bool requiresDuration)
    {
        if (!enabled)
            return;

        string prefix = string.Format("[EnemyVisualPreset] '{0}' - Face Flipbook {1}: ", ownerAssetName, stateName);

        if (columns <= 0)
            Debug.LogWarning(prefix + "columns should be greater than zero.");

        if (rows <= 0)
            Debug.LogWarning(prefix + "rows should be greater than zero.");

        if (frameCount <= 0)
            Debug.LogWarning(prefix + "frame count should be greater than zero.");

        if (columns > 0 && rows > 0 && frameCount > columns * rows)
            Debug.LogWarning(prefix + "frame count exceeds the available grid cells.");

        if (!IsFinite(framesPerSecond) || framesPerSecond <= 0f)
            Debug.LogWarning(prefix + "frames per second should be finite and greater than zero.");

        if (!IsFinite(startFrame) || startFrame < 0f)
            Debug.LogWarning(prefix + "start frame should be finite and zero or positive.");

        if (requiresDuration && (!IsFinite(durationSeconds) || durationSeconds <= 0f))
            Debug.LogWarning(prefix + "duration should be finite and greater than zero.");
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates a state settings instance with explicit grid and timing defaults.
    /// </summary>
    /// <param name="isEnabled">Whether this state starts enabled.</param>
    /// <param name="columnCount">Default atlas column count.</param>
    /// <param name="rowCount">Default atlas row count.</param>
    /// <param name="validFrameCount">Default valid frame count.</param>
    /// <param name="fps">Default playback frames per second.</param>
    /// <param name="firstFrame">Default start frame offset.</param>
    /// <param name="duration">Default temporary-state duration in seconds.</param>
    /// <returns>Configured face state settings.</returns>
    private static EnemyFaceFlipbookStateSettings Create(bool isEnabled,
                                                         int columnCount,
                                                         int rowCount,
                                                         int validFrameCount,
                                                         float fps,
                                                         float firstFrame,
                                                         float duration)
    {
        return new EnemyFaceFlipbookStateSettings
        {
            enabled = isEnabled,
            columns = columnCount,
            rows = rowCount,
            frameCount = validFrameCount,
            framesPerSecond = fps,
            startFrame = firstFrame,
            durationSeconds = duration
        };
    }

    /// <summary>
    /// Checks whether a float can be safely consumed by bake-time conversion.
    /// </summary>
    /// <param name="value">Authored value to inspect.</param>
    /// <returns>True when the value is finite.</returns>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
    #endregion

    #endregion
}
