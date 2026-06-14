using UnityEngine.UIElements;

/// <summary>
/// Provides shared visibility checks for management-tool browser discovery and editor-only element traversal.
/// </summary>
internal static class ManagementToolVisualElementVisibilityUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Returns whether the provided element is attached and currently visible for browser discovery.
    /// </summary>
    /// <param name="targetElement">Candidate element being inspected.</param>
    /// <returns>True when the element should be traversed by the browser collector.</returns>
    public static bool IsBrowsable(VisualElement targetElement)
    {
        if (targetElement == null)
            return false;

        if (targetElement.panel == null)
            return false;

        if (!targetElement.visible)
            return false;

        if (targetElement.resolvedStyle.display == DisplayStyle.None)
            return false;

        if (targetElement.resolvedStyle.visibility == Visibility.Hidden)
            return false;

        return true;
    }
    #endregion

    #endregion
}
