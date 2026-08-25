using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns one preauthored power-up icon slot and its collected-quantity label.
/// </summary>
[DisallowMultipleComponent]
public sealed class HUDPowerUpSummaryIconView : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Optional background image drawn behind the power-up icon.")]
    [SerializeField] private Image backgroundImage;

    [Tooltip("Image receiving the resolved power-up sprite.")]
    [SerializeField] private Image iconImage;

    [Tooltip("Text rendered along the lower edge of the icon to show collected quantity.")]
    [SerializeField] private TMP_Text countText;
    #endregion

    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies shared icon presentation settings without changing slot activation.
    /// </summary>
    /// <param name="config">Baked summary presentation config.</param>
    public void ApplyStyle(in GamePowerUpSummaryRuntimeConfig config)
    {
        RectTransform rectTransform = transform as RectTransform;

        if (rectTransform != null)
            rectTransform.sizeDelta = new Vector2(config.IconSize, config.IconSize);

        if (backgroundImage != null)
        {
            backgroundImage.sprite = config.IconBackgroundSprite.Value;
            backgroundImage.color = ToColor(config.IconBackgroundTint);
            backgroundImage.enabled = backgroundImage.sprite != null || config.IconBackgroundTint.w > 0f;
        }

        if (iconImage != null)
        {
            iconImage.color = ToColor(config.IconTint);
            iconImage.preserveAspect = true;
        }

        if (countText == null)
            return;

        if (config.CounterFont.Value != null)
            countText.font = config.CounterFont.Value;

        countText.fontSize = config.CounterFontSize;
        countText.color = ToColor(config.CounterColor);
    }

    /// <summary>
    /// Shows one collected power-up using presentation metadata already cached for the active player preset.
    /// </summary>
    /// <param name="powerUpId">Stable power-up identifier resolved from the ECS catalog.</param>
    /// <param name="count">Current collected quantity stored by the authoritative catalog.</param>
    /// <param name="counterPrefix">Text placed before the quantity.</param>
    /// <param name="showSingleCount">True when a quantity of one remains visible.</param>
    public void Show(string powerUpId, int count, string counterPrefix, bool showSingleCount)
    {
        gameObject.SetActive(true);

        if (iconImage != null)
        {
            bool hasIcon = PlayerPowerUpPresentationRuntime.TryResolveIcon(powerUpId, out Sprite icon);
            iconImage.sprite = hasIcon ? icon : null;
            iconImage.enabled = hasIcon;
        }

        if (countText == null)
            return;

        bool showCount = showSingleCount || count > 1;
        countText.gameObject.SetActive(showCount);

        if (showCount)
            countText.text = string.Concat(counterPrefix, math.max(0, count).ToString());
    }

    /// <summary>
    /// Hides this preauthored slot and clears object references held by its UI graphics.
    /// </summary>
    public void Hide()
    {
        if (iconImage != null)
            iconImage.sprite = null;

        if (countText != null)
            countText.text = string.Empty;

        gameObject.SetActive(false);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Converts an ECS color to the Unity UI representation.
    /// </summary>
    /// <param name="value">RGBA color stored in ECS.</param>
    /// <returns>Unity color used by UI graphics.</returns>
    private static Color ToColor(float4 value)
    {
        return new Color(value.x, value.y, value.z, value.w);
    }
    #endregion

    #endregion
}
