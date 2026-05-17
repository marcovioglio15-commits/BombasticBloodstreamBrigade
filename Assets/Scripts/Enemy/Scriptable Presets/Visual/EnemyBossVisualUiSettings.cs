using System;
using UnityEngine;

/// <summary>
/// Stores boss-specific screen-space UI presentation settings.
/// </summary>
[Serializable]
public sealed class EnemyBossVisualUiSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables the dedicated boss HUD for enemies using a Boss Pattern Preset.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Shows the mirrored boss health and shield bars when the dedicated boss HUD is enabled.")]
    [SerializeField] private bool showHealthBar = true;

    [Tooltip("Shows the screen-edge indicator for an active boss that is outside the camera view.")]
    [SerializeField] private bool showOffscreenIndicator = true;

    [Tooltip("Optional boss display name shown near the mirrored top-right boss bars. Empty falls back to the visual preset name.")]
    [SerializeField] private string bossDisplayName;

    [Tooltip("Screen-space health fill color used by the mirrored boss health syringe bar.")]
    [SerializeField] private Color healthFillColor = new Color(0.9f, 0.12f, 0.08f, 1f);

    [Tooltip("Sprite tint used behind the mirrored boss health syringe bar. Keep white to preserve the player bar background silhouette.")]
    [SerializeField] private Color healthBackgroundColor = Color.white;

    [Tooltip("Screen-space shield fill color used by the mirrored boss shield syringe bar.")]
    [SerializeField] private Color shieldFillColor = new Color(0.2f, 0.85f, 1f, 1f);

    [Tooltip("Sprite tint used behind the mirrored boss shield syringe bar. Keep white to preserve the player bar background silhouette.")]
    [SerializeField] private Color shieldBackgroundColor = Color.white;

    [Tooltip("Sprite used by the off-screen indicator that slides along screen edges.")]
    [SerializeField] private Sprite offscreenIndicatorSprite;

    [Tooltip("Tint color applied to the off-screen boss indicator.")]
    [SerializeField] private Color offscreenIndicatorColor = new Color(1f, 0.2f, 0.1f, 0.95f);

    [Tooltip("Square size in pixels used by the off-screen boss indicator image.")]
    [Range(16f, 192f)]
    [SerializeField] private float offscreenIndicatorSizePixels = 56f;

    [Tooltip("Extra screen-edge margin in pixels kept outside the off-screen indicator half size.")]
    [Range(0f, 160f)]
    [SerializeField] private float edgePaddingPixels = 30f;
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

    public bool ShowHealthBar
    {
        get
        {
            return showHealthBar;
        }
    }

    public bool ShowOffscreenIndicator
    {
        get
        {
            return showOffscreenIndicator;
        }
    }

    public string BossDisplayName
    {
        get
        {
            return bossDisplayName;
        }
    }

    public Color HealthFillColor
    {
        get
        {
            return healthFillColor;
        }
    }

    public Color HealthBackgroundColor
    {
        get
        {
            return healthBackgroundColor;
        }
    }

    public Color ShieldFillColor
    {
        get
        {
            return shieldFillColor;
        }
    }

    public Color ShieldBackgroundColor
    {
        get
        {
            return shieldBackgroundColor;
        }
    }

    public Sprite OffscreenIndicatorSprite
    {
        get
        {
            return offscreenIndicatorSprite;
        }
    }

    public Color OffscreenIndicatorColor
    {
        get
        {
            return offscreenIndicatorColor;
        }
    }

    public float OffscreenIndicatorSizePixels
    {
        get
        {
            return offscreenIndicatorSizePixels;
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
    /// Validates color alpha channels while leaving prefab-authored boss bar layout untouched.
    /// </summary>
    public void Validate()
    {
        healthFillColor.a = Mathf.Clamp01(healthFillColor.a);
        healthBackgroundColor.a = Mathf.Clamp01(healthBackgroundColor.a);
        shieldFillColor.a = Mathf.Clamp01(shieldFillColor.a);
        shieldBackgroundColor.a = Mathf.Clamp01(shieldBackgroundColor.a);
        offscreenIndicatorColor.a = Mathf.Clamp01(offscreenIndicatorColor.a);
    }
    #endregion

    #endregion
}
