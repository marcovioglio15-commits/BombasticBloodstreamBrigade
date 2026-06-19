using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Captures pointer press, release, and hover events for the milestone skip button hold-confirmation flow.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class MilestoneSkipHoldButtonView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler
{
    #region Fields
    private Button cachedButton;
    private Action pressedCallback;
    private Action releasedCallback;
    private Action hoveredCallback;
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Caches the required button reference used to gate pointer events.
    /// </summary>
    private void Awake()
    {
        cachedButton = GetComponent<Button>();
    }

    /// <summary>
    /// Cancels any active hold when the button is disabled mid-interaction.
    /// </summary>
    private void OnDisable()
    {
        InvokeReleased();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Registers callbacks consumed by the owning milestone HUD section.
    /// </summary>
    /// <param name="pressedCallbackValue">Callback invoked when the left pointer button starts pressing Skip.</param>
    /// <param name="releasedCallbackValue">Callback invoked when the pointer releases or leaves the Skip button.</param>
    /// <param name="hoveredCallbackValue">Callback invoked when the pointer enters the Skip button.</param>
    public void RegisterCallbacks(Action pressedCallbackValue,
                                  Action releasedCallbackValue,
                                  Action hoveredCallbackValue)
    {
        pressedCallback = pressedCallbackValue;
        releasedCallback = releasedCallbackValue;
        hoveredCallback = hoveredCallbackValue;
    }

    /// <summary>
    /// Clears callbacks registered by the owning milestone HUD section.
    /// </summary>
    public void ClearCallbacks()
    {
        pressedCallback = null;
        releasedCallback = null;
        hoveredCallback = null;
    }
    #endregion

    #region Pointer Events
    /// <summary>
    /// Starts skip hold confirmation when the player presses the left pointer button.
    /// </summary>
    /// <param name="eventData">Pointer event raised by the active EventSystem.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanForwardPointerInput(eventData, true))
            return;

        Action callback = pressedCallback;

        if (callback != null)
            callback.Invoke();

        eventData.Use();
    }

    /// <summary>
    /// Cancels skip hold confirmation when the player releases the pointer button.
    /// </summary>
    /// <param name="eventData">Pointer event raised by the active EventSystem.</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        InvokeReleased();
    }

    /// <summary>
    /// Cancels skip hold confirmation when the pointer leaves the Skip button bounds.
    /// </summary>
    /// <param name="eventData">Pointer event raised by the active EventSystem.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        InvokeReleased();
    }

    /// <summary>
    /// Selects the skip navigation item when pointer-hover selection is enabled by the owning HUD section.
    /// </summary>
    /// <param name="eventData">Pointer event raised by the active EventSystem.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!CanForwardPointerInput(eventData, false))
            return;

        Action callback = hoveredCallback;

        if (callback != null)
            callback.Invoke();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Invokes the release callback if one is registered.
    /// </summary>
    private void InvokeReleased()
    {
        Action callback = releasedCallback;

        if (callback != null)
            callback.Invoke();
    }

    /// <summary>
    /// Checks whether the current pointer event can drive skip-button interactions.
    /// </summary>
    /// <param name="eventData">Pointer event raised by the active EventSystem.</param>
    /// <param name="requireLeftButton">True when only left-click input should be accepted.</param>
    /// <returns>True when the event can be forwarded to the owner; otherwise false.</returns>
    private bool CanForwardPointerInput(PointerEventData eventData, bool requireLeftButton)
    {
        if (eventData == null)
            return false;

        if (requireLeftButton && eventData.button != PointerEventData.InputButton.Left)
            return false;

        if (cachedButton == null)
            cachedButton = GetComponent<Button>();

        if (cachedButton == null)
            return false;

        if (!cachedButton.IsActive())
            return false;

        return cachedButton.IsInteractable();
    }
    #endregion

    #endregion
}
