using System;
using UnityEngine;

/// <summary>
/// Stores authored fade-in / fade-out tuning for one full-screen damage vignette channel.
/// One instance drives the shield-only damage overlay, another drives the health damage overlay.
/// Sprite, peak alpha and durations are read at bake time and pushed into the runtime ECS config consumed by the scene UI binder.
/// </summary>
[Serializable]
public sealed class PlayerVisualDamageVignetteSettings
{
    #region Constants
    private const float MinimumDurationSeconds = 0f;
    private const float MaximumDurationSeconds = 5f;
    private const float MinimumAlpha = 0f;
    private const float MaximumAlpha = 1f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Optional full-screen sprite displayed during the vignette burst. Leave empty to disable this channel even when the other fields are configured.")]
    [SerializeField] private Sprite sprite;

    [Tooltip("Peak overlay alpha reached at the end of the fade-in. Set 0 to mute the vignette without removing the sprite.")]
    [Range(MinimumAlpha, MaximumAlpha)]
    [SerializeField] private float maxAlpha = 0.65f;

    [Tooltip("Optional tint multiplied with the sprite color while the vignette is visible. Alpha component is ignored - the runtime alpha comes from maxAlpha.")]
    [SerializeField] private Color tint = Color.white;

    [Tooltip("Seconds used to ramp the overlay from transparent to maxAlpha right after damage is detected. Use very small values for a punchy reaction.")]
    [SerializeField] private float fadeInSeconds = 0.06f;

    [Tooltip("Seconds used to ramp the overlay from maxAlpha back to transparent after the fade-in finishes.")]
    [SerializeField] private float fadeOutSeconds = 0.35f;
    #endregion

    #endregion

    #region Properties
    public Sprite Sprite
    {
        get
        {
            return sprite;
        }
    }

    public float MaxAlpha
    {
        get
        {
            return maxAlpha;
        }
    }

    public Color Tint
    {
        get
        {
            return tint;
        }
    }

    public float FadeInSeconds
    {
        get
        {
            return fadeInSeconds;
        }
    }

    public float FadeOutSeconds
    {
        get
        {
            return fadeOutSeconds;
        }
    }

    public bool HasSprite
    {
        get
        {
            return sprite != null;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates the vignette numeric values and surfaces warnings without snapping the authored data.
    /// Called from the owning preset's OnValidate so the inspector reflects user-visible warnings.
    /// </summary>
    /// <param name="ownerAssetName">Friendly name of the owning preset asset used in warning messages.</param>
    /// <param name="channelLabel">Channel label such as "Health Damage Vignette" used to disambiguate warning messages.</param>
    public void Validate(string ownerAssetName, string channelLabel)
    {
        // Warn instead of snapping so the user actively fixes incoherent authored values per project rule 20.
        if (maxAlpha < MinimumAlpha || maxAlpha > MaximumAlpha)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - {1}: Max Alpha {2} is outside the expected 0..1 range.", ownerAssetName, channelLabel, maxAlpha));

        if (fadeInSeconds < MinimumDurationSeconds || fadeInSeconds > MaximumDurationSeconds)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - {1}: Fade In Seconds {2} is outside the expected 0..{3} range.", ownerAssetName, channelLabel, fadeInSeconds, MaximumDurationSeconds));

        if (fadeOutSeconds < MinimumDurationSeconds || fadeOutSeconds > MaximumDurationSeconds)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - {1}: Fade Out Seconds {2} is outside the expected 0..{3} range.", ownerAssetName, channelLabel, fadeOutSeconds, MaximumDurationSeconds));

        // Tint alpha is ignored at runtime; warn so authors notice if they assumed otherwise.
        if (tint.a < 0.999f)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - {1}: Tint alpha {2:0.###} is ignored at runtime - vignette opacity is driven by Max Alpha and the fade durations.", ownerAssetName, channelLabel, tint.a));
    }
    #endregion

    #endregion
}
