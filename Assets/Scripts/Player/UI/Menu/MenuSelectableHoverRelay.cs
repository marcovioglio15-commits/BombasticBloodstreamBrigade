using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Forwards pointer enter and exit events from one menu button to a shared MenuSelectionController.
/// </summary>
[RequireComponent(typeof(Selectable))]
[DisallowMultipleComponent]
public sealed class MenuSelectableHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IDeselectHandler
{
    #region Fields

    #region Serialized Fields
    [Header("Selection")]
    [Tooltip("Optional selection controller override used instead of the first parent MenuSelectionController.")]
    [SerializeField] private MenuSelectionController selectionControllerOverride;
    #endregion

    #region Runtime
    private Selectable selectable;
    private MenuSelectionController selectionController;
    #endregion

    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Caches the required selectable and resolves the shared menu selection controller.
    /// </summary>
    private void Awake()
    {
        // Cache the required runtime references once.
        selectable = GetComponent<Selectable>();
        ResolveSelectionController();
    }

    /// <summary>
    /// Re-resolves the selection controller when the relay is re-enabled after hierarchy changes.
    /// </summary>
    private void OnEnable()
    {
        // Re-resolve the controller in case the hierarchy changed.
        ResolveSelectionController();
    }

    /// <summary>
    /// Releases hover ownership if this selectable is disabled while the pointer owns it.
    /// </summary>
    private void OnDisable()
    {
        // Release hover ownership cleanly when the button gets disabled mid-hover.
        if (selectionController == null || selectable == null)
            return;

        selectionController.RegisterPointerExit(selectable);
    }
    #endregion

    #region Event Methods
    /// <summary>
    /// Transfers active selection to this button while the pointer is hovering it.
    /// </summary>
    /// <param name="eventData">Pointer event reported by the Unity EventSystem.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        // Ignore missing selection infrastructure.
        if (selectionController == null || selectable == null)
            return;

        selectionController.RegisterPointerEnter(selectable);
    }

    /// <summary>
    /// Restores the previous menu selection when the pointer leaves this button.
    /// </summary>
    /// <param name="eventData">Pointer event reported by the Unity EventSystem.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        // Ignore missing selection infrastructure.
        if (selectionController == null || selectable == null)
            return;

        selectionController.RegisterPointerExit(selectable);
    }

    /// <summary>
    /// Reports focus loss so the shared menu controller can recover from pointer clicks outside buttons.
    /// </summary>
    /// <param name="eventData">Deselection event reported by the Unity EventSystem.</param>
    public void OnDeselect(BaseEventData eventData)
    {
        // Ignore missing selection infrastructure.
        if (selectionController == null || selectable == null)
            return;

        selectionController.RegisterSelectableDeselected(selectable);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the shared MenuSelectionController used by this button.
    /// </summary>
    private void ResolveSelectionController()
    {
        // Prefer the explicit serialized override when present.
        if (selectionControllerOverride != null)
        {
            selectionController = selectionControllerOverride;
            return;
        }

        // Fall back to the closest menu-level controller in the hierarchy.
        selectionController = GetComponentInParent<MenuSelectionController>(true);
    }
    #endregion

    #endregion
}
