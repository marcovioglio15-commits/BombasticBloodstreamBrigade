using System;
using UnityEngine;

/// <summary>
/// Stores screen-edge warning settings for enemy projectiles that start outside camera view.
/// </summary>
[Serializable]
public sealed class EnemyProjectileOffscreenWarningSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Shows a screen-edge warning for enemy-owned projectiles that are fired while still outside camera view.")]
    [SerializeField] private bool enabled;

    [Tooltip("Optional sprite used by offscreen projectile warnings. Empty uses the built-in triangular warning sprite.")]
    [SerializeField] private Sprite indicatorSprite;

    [Tooltip("Tint applied to offscreen projectile warning indicators.")]
    [SerializeField] private Color indicatorColor = new Color(1f, 0.48f, 0.05f, 0.95f);

    [Tooltip("Square size in pixels used by each projectile warning indicator.")]
    [Range(12f, 128f)]
    [SerializeField] private float indicatorSizePixels = 42f;

    [Tooltip("Extra screen-edge margin in pixels kept outside the projectile warning half size.")]
    [Range(0f, 160f)]
    [SerializeField] private float edgePaddingPixels = 28f;
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

    public Sprite IndicatorSprite
    {
        get
        {
            return indicatorSprite;
        }
    }

    public Color IndicatorColor
    {
        get
        {
            return indicatorColor;
        }
    }

    public float IndicatorSizePixels
    {
        get
        {
            return indicatorSizePixels;
        }
    }

    public float EdgePaddingPixels
    {
        get
        {
            return edgePaddingPixels;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Leaves authored warning values untouched so the management tool can report invalid ranges without silently snapping data.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}
