using TMPro;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Owns one preauthored player-stat text row in the lower summary panel.
/// </summary>
[DisallowMultipleComponent]
public sealed class HUDPowerUpSummaryStatisticRowView : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Text component receiving the resolved statistic label and current value.")]
    [SerializeField] private TMP_Text valueText;
    #endregion

    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies one baked row style and activates this preauthored statistic slot.
    /// </summary>
    /// <param name="definition">Baked statistic definition owning the row style.</param>
    public void ApplyStyle(in GamePowerUpSummaryStatisticElement definition)
    {
        gameObject.SetActive(true);

        if (valueText == null)
            return;

        if (definition.Font.Value != null)
            valueText.font = definition.Font.Value;

        valueText.fontSize = definition.FontSize;
        valueText.fontStyle = (FontStyles)definition.FontStyle;
        valueText.color = ToColor(definition.Color);
    }

    /// <summary>
    /// Replaces the current row text only when the formatted value changed.
    /// </summary>
    /// <param name="text">Fully formatted statistic label and value.</param>
    public void SetText(string text)
    {
        if (valueText == null || string.Equals(valueText.text, text, System.StringComparison.Ordinal))
            return;

        valueText.text = text;
    }

    /// <summary>
    /// Hides this unused preauthored row and clears stale text.
    /// </summary>
    public void Hide()
    {
        if (valueText != null)
            valueText.text = string.Empty;

        gameObject.SetActive(false);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Converts an ECS color to the Unity UI representation.
    /// </summary>
    /// <param name="value">RGBA color stored in ECS.</param>
    /// <returns>Unity color used by UI text.</returns>
    private static Color ToColor(float4 value)
    {
        return new Color(value.x, value.y, value.z, value.w);
    }
    #endregion

    #endregion
}
