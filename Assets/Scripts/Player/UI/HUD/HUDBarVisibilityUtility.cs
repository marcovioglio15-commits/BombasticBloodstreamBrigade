using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Provides small shared helpers for resolving and toggling HUD bar roots.
/// </summary>
internal static class HUDBarVisibilityUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Shows or hides a complete HUD bar hierarchy only when the active state changes.
    /// </summary>
    /// <param name="barRootObject">Root GameObject that owns the bar visuals.</param>
    /// <param name="isVisible">Desired visibility state.</param>
    public static void SetVisible(GameObject barRootObject, bool isVisible)
    {
        if (barRootObject == null)
            return;

        if (barRootObject.activeSelf == isVisible)
            return;

        barRootObject.SetActive(isVisible);
    }

    /// <summary>
    /// Resolves the root object that owns a HUD bar background and fill image.
    /// </summary>
    /// <param name="fillImage">Fill image used by the managed bar runtime.</param>
    /// <returns>Root object that can be toggled for the full bar, or null when unavailable.</returns>
    public static GameObject ResolveRootObject(Image fillImage)
    {
        if (fillImage == null)
            return null;

        Transform parentTransform = fillImage.transform.parent;

        if (parentTransform != null)
            return parentTransform.gameObject;

        return fillImage.gameObject;
    }

    /// <summary>
    /// Resolves a HUD bar root, preferring an explicit background object when one exists.
    /// </summary>
    /// <param name="backgroundImage">Optional background image that can represent the full bar root.</param>
    /// <param name="fillImage">Fill image used as fallback when no background is available.</param>
    /// <returns>Root object that can be toggled for the full bar, or null when unavailable.</returns>
    public static GameObject ResolveRootObject(Image backgroundImage, Image fillImage)
    {
        if (backgroundImage != null)
            return backgroundImage.gameObject;

        return ResolveRootObject(fillImage);
    }
    #endregion

    #endregion
}
