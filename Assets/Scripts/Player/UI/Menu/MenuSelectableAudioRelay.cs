using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Converts authored menu-button focus and activation callbacks into shared ECS audio requests.
/// </summary>
[RequireComponent(typeof(Selectable))]
[DisallowMultipleComponent]
public sealed class MenuSelectableAudioRelay : MonoBehaviour,
                                                IPointerEnterHandler,
                                                IPointerClickHandler,
                                                ISelectHandler,
                                                ISubmitHandler
{
    #region Fields
    private Selectable selectable;
    private int lastHoverRequestFrame = -1;
    private int lastSelectRequestFrame = -1;
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Caches the preauthored Selectable used to reject audio from disabled controls.
    /// </summary>
    private void Awake()
    {
        selectable = GetComponent<Selectable>();
    }
    #endregion

    #region Event Methods
    /// <summary>
    /// Requests hover audio when pointer focus reaches this interactable button.
    /// </summary>
    /// <param name="eventData">Pointer event reported by the Unity EventSystem.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        RequestHover();
    }

    /// <summary>
    /// Requests select audio when a valid left-pointer click activates this button.
    /// </summary>
    /// <param name="eventData">Pointer event reported by the Unity EventSystem.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        RequestSelect();
    }

    /// <summary>
    /// Requests hover audio when keyboard or gamepad navigation focuses this button.
    /// </summary>
    /// <param name="eventData">Selection event reported by the Unity EventSystem.</param>
    public void OnSelect(BaseEventData eventData)
    {
        RequestHover();
    }

    /// <summary>
    /// Requests select audio when keyboard or gamepad submit activates this button.
    /// </summary>
    /// <param name="eventData">Submit event reported by the Unity EventSystem.</param>
    public void OnSubmit(BaseEventData eventData)
    {
        RequestSelect();
    }
    #endregion

    #region Audio Requests
    /// <summary>
    /// Enqueues at most one hover request per rendered frame, preventing pointer-driven selection from double playing.
    /// </summary>
    private void RequestHover()
    {
        if (!CanRequestAudio() || lastHoverRequestFrame == Time.frameCount)
            return;

        lastHoverRequestFrame = Time.frameCount;
        GameAudioManagedEventRequestUtility.TryEnqueueGlobal(GameAudioEventId.MenuButtonHover);
    }

    /// <summary>
    /// Enqueues at most one selection request per rendered frame for the accepted activation path.
    /// </summary>
    private void RequestSelect()
    {
        if (!CanRequestAudio() || lastSelectRequestFrame == Time.frameCount)
            return;

        lastSelectRequestFrame = Time.frameCount;
        GameAudioManagedEventRequestUtility.TryEnqueueGlobal(GameAudioEventId.MenuButtonSelect);
    }

    /// <summary>
    /// Checks whether this active relay owns an interactable Selectable.
    /// </summary>
    /// <returns>True when UI audio may be requested.</returns>
    private bool CanRequestAudio()
    {
        return isActiveAndEnabled && selectable != null && selectable.IsInteractable();
    }
    #endregion

    #endregion
}
