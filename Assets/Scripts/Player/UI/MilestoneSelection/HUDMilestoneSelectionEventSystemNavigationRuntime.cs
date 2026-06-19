using UnityEngine.EventSystems;

/// <summary>
/// Temporarily disables default EventSystem navigation while the milestone panel uses custom navigation.
/// </summary>
public sealed class HUDMilestoneSelectionEventSystemNavigationRuntime
{
    #region Fields
    private EventSystem suppressedEventSystem;
    private bool cachedSendNavigationEvents;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies navigation suppression or restoration when the milestone panel visibility changes.
    /// </summary>
    /// <param name="isVisible">True when the milestone panel is visible.</param>
    /// <param name="suspendNavigation">True when default EventSystem navigation should be suspended.</param>
    public void ApplyVisibleState(bool isVisible, bool suspendNavigation)
    {
        if (isVisible)
        {
            SuppressIfNeeded(suspendNavigation);
            return;
        }

        Restore();
    }

    /// <summary>
    /// Restores the EventSystem navigation flag cached when suppression started.
    /// </summary>
    public void Restore()
    {
        if (suppressedEventSystem == null)
            return;

        suppressedEventSystem.sendNavigationEvents = cachedSendNavigationEvents;
        suppressedEventSystem = null;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Disables default EventSystem navigation while the milestone panel owns custom input.
    /// </summary>
    /// <param name="suspendNavigation">True when navigation suppression is requested by HUD settings.</param>
    private void SuppressIfNeeded(bool suspendNavigation)
    {
        if (!suspendNavigation)
            return;

        EventSystem currentEventSystem = EventSystem.current;

        if (currentEventSystem == null)
            return;

        if (ReferenceEquals(suppressedEventSystem, currentEventSystem))
            return;

        Restore();
        suppressedEventSystem = currentEventSystem;
        cachedSendNavigationEvents = currentEventSystem.sendNavigationEvents;
        currentEventSystem.sendNavigationEvents = false;
        currentEventSystem.SetSelectedGameObject(null);
    }
    #endregion

    #endregion
}
