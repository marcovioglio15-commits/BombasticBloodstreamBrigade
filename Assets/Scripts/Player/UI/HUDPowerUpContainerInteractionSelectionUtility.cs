using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Centralizes EventSystem focus management for the dropped-container overlay buttons.
/// </summary>
internal static class HUDPowerUpContainerInteractionSelectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures the EventSystem keeps one valid overlay button selected while the overlay is visible.
    /// primaryButton: Primary-slot replacement button.
    /// secondaryButton: Secondary-slot replacement button.
    /// returns void.
    /// </summary>
    public static void EnsureOverlaySelection(Button primaryButton, Button secondaryButton)
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null)
            return;

        if (IsOverlaySelection(eventSystem.currentSelectedGameObject, primaryButton, secondaryButton))
            return;

        SelectFirstOverlayButton(primaryButton, secondaryButton);
    }

    /// <summary>
    /// Selects the first currently valid overlay button.
    /// primaryButton: Primary-slot replacement button.
    /// secondaryButton: Secondary-slot replacement button.
    /// returns void.
    /// </summary>
    public static void SelectFirstOverlayButton(Button primaryButton, Button secondaryButton)
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null)
            return;

        Button defaultButton = ResolveFirstSelectableOverlayButton(primaryButton, secondaryButton);

        if (defaultButton == null)
            return;

        eventSystem.SetSelectedGameObject(null);
        eventSystem.SetSelectedGameObject(defaultButton.gameObject);
    }

    /// <summary>
    /// Clears EventSystem focus when it currently points to one of the overlay buttons being hidden.
    /// primaryButton: Primary-slot replacement button.
    /// secondaryButton: Secondary-slot replacement button.
    /// returns void.
    /// </summary>
    public static void ClearOverlaySelection(Button primaryButton, Button secondaryButton)
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null)
            return;

        if (!IsOverlaySelection(eventSystem.currentSelectedGameObject, primaryButton, secondaryButton))
            return;

        eventSystem.SetSelectedGameObject(null);
    }

    /// <summary>
    /// Applies the interactable state to both overlay replacement buttons.
    /// /params primaryButton Primary-slot replacement button.
    /// /params secondaryButton Secondary-slot replacement button.
    /// /params isInteractable True when UI submit should be able to confirm a slot replacement.
    /// /returns void.
    /// </summary>
    public static void SetOverlayButtonsInteractable(Button primaryButton, Button secondaryButton, bool isInteractable)
    {
        if (primaryButton != null)
            primaryButton.interactable = isInteractable;

        if (secondaryButton != null)
            secondaryButton.interactable = isInteractable;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Returns the first overlay button that can currently receive navigation focus.
    /// primaryButton: Primary-slot replacement button.
    /// secondaryButton: Secondary-slot replacement button.
    /// returns First valid button or null when no overlay button is selectable.
    /// </summary>
    private static Button ResolveFirstSelectableOverlayButton(Button primaryButton, Button secondaryButton)
    {
        if (primaryButton != null && primaryButton.IsActive() && primaryButton.interactable)
            return primaryButton;

        if (secondaryButton != null && secondaryButton.IsActive() && secondaryButton.interactable)
            return secondaryButton;

        return null;
    }

    /// <summary>
    /// Returns whether the provided GameObject belongs to the overlay slot-selection button set.
    /// selectedObject: EventSystem-selected object inspected for overlay ownership.
    /// primaryButton: Primary-slot replacement button.
    /// secondaryButton: Secondary-slot replacement button.
    /// returns True when the selection belongs to the overlay buttons.
    /// </summary>
    private static bool IsOverlaySelection(GameObject selectedObject, Button primaryButton, Button secondaryButton)
    {
        if (selectedObject == null)
            return false;

        if (primaryButton != null && selectedObject == primaryButton.gameObject)
            return true;

        if (secondaryButton != null && selectedObject == secondaryButton.gameObject)
            return true;

        return false;
    }
    #endregion

    #endregion
}
