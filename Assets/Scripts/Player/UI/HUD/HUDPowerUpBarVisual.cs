using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stores the visual references used by one HUD bar, including the fill image and its background root.
/// </summary>
internal readonly struct HUDPowerUpBarVisual
{
    #region Fields
    public readonly Image FillImage;
    public readonly GameObject RootObject;
    #endregion

    #region Properties
    public bool HasVisual
    {
        get
        {
            if (FillImage != null)
                return true;

            return RootObject != null;
        }
    }
    #endregion

    #region Methods

    #region Factory
    /// <summary>
    /// Creates one bar-visual descriptor from a fill image and its parent background object.
    /// </summary>
    /// <param name="fillImage">Fill image bound in the HUD manager.</param>
    /// <returns>A bar-visual descriptor ready for updates.</returns>
    public static HUDPowerUpBarVisual Create(Image fillImage)
    {
        GameObject rootObject = null;

        if (fillImage != null)
        {
            Transform parentTransform = fillImage.transform.parent;
            rootObject = parentTransform != null ? parentTransform.gameObject : fillImage.gameObject;
        }

        return new HUDPowerUpBarVisual(fillImage, rootObject);
    }

    /// <summary>
    /// Creates one bar-visual descriptor.
    /// </summary>
    /// <param name="fillImageValue">Fill image driven by runtime values.</param>
    /// <param name="rootObjectValue">Root object that contains the bar background and fill.</param>
    /// <returns>A fully initialized bar-visual descriptor.</returns>
    private HUDPowerUpBarVisual(Image fillImageValue, GameObject rootObjectValue)
    {
        FillImage = fillImageValue;
        RootObject = rootObjectValue;
    }
    #endregion

    #region Runtime Updates
    /// <summary>
    /// Applies one normalized fill value while keeping the full bar hierarchy visible.
    /// </summary>
    /// <param name="normalizedValue">Normalized fill amount written into the fill image.</param>
    public void ApplyFill(float normalizedValue)
    {
        if (!HasVisual)
            return;

        SetRootVisible(true);

        if (FillImage == null)
            return;

        if (!FillImage.enabled)
            FillImage.enabled = true;

        FillImage.fillAmount = Mathf.Clamp01(normalizedValue);
    }

    /// <summary>
    /// Applies the missing-data state to the bar.
    /// </summary>
    /// <param name="displayedValue">Last displayed normalized value used when the bar remains visible.</param>
    /// <param name="hideWhenMissing">Hides the entire bar hierarchy when true.</param>
    public void HandleMissing(float displayedValue, bool hideWhenMissing)
    {
        if (!HasVisual)
            return;

        if (hideWhenMissing)
        {
            SetRootVisible(false);
            return;
        }

        ApplyFill(displayedValue);
    }

    /// <summary>
    /// Applies the missing-module state to the bar.
    /// </summary>
    /// <param name="displayedValue">Last displayed normalized value used when the bar remains visible.</param>
    /// <param name="hideWhenMissing">Hides the entire bar hierarchy when true.</param>
    public void ApplyMissing(float displayedValue, bool hideWhenMissing)
    {
        HandleMissing(displayedValue, hideWhenMissing);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Shows or hides the full bar hierarchy only when a state change is required.
    /// </summary>
    /// <param name="isVisible">Target active state for the bar root.</param>
    private void SetRootVisible(bool isVisible)
    {
        if (RootObject != null)
        {
            if (RootObject.activeSelf != isVisible)
                RootObject.SetActive(isVisible);

            return;
        }

        if (FillImage != null && FillImage.enabled != isVisible)
            FillImage.enabled = isVisible;
    }
    #endregion

    #endregion
}
