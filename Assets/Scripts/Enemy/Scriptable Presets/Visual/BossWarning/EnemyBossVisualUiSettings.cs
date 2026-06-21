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

    [Tooltip("Procedural syringe settings used by the mirrored boss health and shield bars. The Health channel drives boss health and the Shield channel drives boss shield.")]
    [SerializeField] private PlayerHealthBarsVisualSettings syringeBars = new PlayerHealthBarsVisualSettings(PlayerSyringePalettePreset.BossHealth,
                                                                                                             PlayerSyringePalettePreset.BossShield);

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

    public PlayerHealthBarsVisualSettings SyringeBars
    {
        get
        {
            return syringeBars;
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
    /// Reports invalid boss UI values while leaving prefab-authored boss bar layout untouched.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name used by warning messages.</param>
    public void Validate(string ownerAssetName)
    {
        if (!IsFinite(offscreenIndicatorColor))
            LogWarning(ownerAssetName, "Offscreen Indicator Color channels should be finite.");

        if (offscreenIndicatorSizePixels <= 0f || !IsFinite(offscreenIndicatorSizePixels))
            LogWarning(ownerAssetName, "Offscreen Indicator Size should be finite and greater than zero.");

        if (edgePaddingPixels < 0f || !IsFinite(edgePaddingPixels))
            LogWarning(ownerAssetName, "Screen Margin should be finite and zero or positive.");

        if (syringeBars == null)
            LogWarning(ownerAssetName, "Syringe Bars settings are missing.");
        else
            syringeBars.Validate(ownerAssetName);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Writes one boss UI preset warning.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name.</param>
    /// <param name="message">Warning message.</param>
    private static void LogWarning(string ownerAssetName, string message)
    {
        Debug.LogWarning(string.Format("[EnemyVisualPreset] '{0}' - Boss UI: {1}",
                                       ownerAssetName,
                                       message));
    }

    /// <summary>
    /// Checks whether every color channel is finite.
    /// </summary>
    /// <param name="value">Color to inspect.</param>
    /// <returns>True when all RGBA channels are finite.</returns>
    private static bool IsFinite(Color value)
    {
        return IsFinite(value.r) &&
               IsFinite(value.g) &&
               IsFinite(value.b) &&
               IsFinite(value.a);
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
