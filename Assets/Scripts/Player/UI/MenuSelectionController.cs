using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Centralizes default menu selection and pointer-hover takeover for authored UI menus.
/// It keeps keyboard/controller navigation stable while letting hovered buttons temporarily own submit input.
/// None.
/// </summary>
[DisallowMultipleComponent]
public sealed class MenuSelectionController : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Selection")]
    [Tooltip("Optional EventSystem override used to drive authored menu selection.")]
    [SerializeField] private EventSystem eventSystemOverride;

    [Tooltip("Fallback selectable restored when the current pointer hover ends or when the menu first opens.")]
    [SerializeField] private Selectable defaultSelectable;
    #endregion

    #region Runtime
    private Coroutine deferredSelectionCoroutine;
    private Selectable hoveredSelectable;
    private GameObject selectionBeforeHover;
    #endregion

    #endregion

    #region Properties
    public EventSystem EventSystemOverride
    {
        get
        {
            return eventSystemOverride;
        }
    }

    public Selectable DefaultSelectable
    {
        get
        {
            return defaultSelectable;
        }
    }
    #endregion

    #region Methods

    #region Unity Methods
    private void OnEnable()
    {
        // Reapply the fallback selection whenever the menu becomes active.
        QueueDeferredDefaultSelection();
    }

    private void Start()
    {
        // Run one second selection pass after all scene objects finished enabling.
        QueueDeferredDefaultSelection();
    }

    private void OnDisable()
    {
        // Clear transient hover state when the menu is hidden or destroyed.
        hoveredSelectable = null;
        selectionBeforeHover = null;

        if (deferredSelectionCoroutine == null)
            return;

        StopCoroutine(deferredSelectionCoroutine);
        deferredSelectionCoroutine = null;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Replaces the fallback selectable used by future default-selection restores.
    /// </summary>
    /// <param name="selectable">Selectable that should become the new fallback target.</param>
    public void SetDefaultSelectable(Selectable selectable)
    {
        if (!IsSelectionCandidateValid(selectable))
            return;

        defaultSelectable = selectable;
    }

    /// <summary>
    /// Selects one authored button immediately and optionally stores it as the fallback target.
    /// </summary>
    /// <param name="selectable">Selectable that should own navigation and submit input.</param>
    /// <param name="rememberAsDefault">True when this selectable should become the new fallback target.</param>
    public void SelectSelectable(Selectable selectable, bool rememberAsDefault)
    {
        // Persist the preferred fallback target when requested by the caller.
        if (rememberAsDefault && IsSelectionCandidateValid(selectable))
            defaultSelectable = selectable;

        // Ignore invalid or inactive selection candidates.
        if (!IsSelectionCandidateValid(selectable))
            return;

        ApplySelection(selectable);
    }

    /// <summary>
    /// Handles pointer-entry takeover so the hovered button becomes the active submit target.
    /// </summary>
    /// <param name="selectable">Hovered selectable reported by one menu button relay.</param>
    public void RegisterPointerEnter(Selectable selectable)
    {
        // Ignore invalid hover sources.
        if (!IsSelectionCandidateValid(selectable))
            return;

        EventSystem resolvedEventSystem = ResolveEventSystem();

        if (resolvedEventSystem == null)
            return;

        // Preserve the previous keyboard/controller selection only on the first active hover.
        if (hoveredSelectable == null)
            selectionBeforeHover = resolvedEventSystem.currentSelectedGameObject;

        hoveredSelectable = selectable;
        ApplySelection(selectable);
    }

    /// <summary>
    /// Handles pointer-exit restore so keyboard/controller selection returns to its previous button.
    /// </summary>
    /// <param name="selectable">Selectable whose hover ownership has just ended.</param>
    public void RegisterPointerExit(Selectable selectable)
    {
        // Ignore stale exits coming from buttons that no longer own hover takeover.
        if (hoveredSelectable != selectable)
            return;

        hoveredSelectable = null;
        RestoreSelectionAfterHover();
    }
    #endregion

    #region Selection Helpers
    /// <summary>
    /// Queues one end-of-frame fallback selection pass so menu highlight is restored reliably after activation.
    /// None.
    /// </summary>
    private void QueueDeferredDefaultSelection()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (deferredSelectionCoroutine != null)
            StopCoroutine(deferredSelectionCoroutine);

        deferredSelectionCoroutine = StartCoroutine(DeferredDefaultSelectionCoroutine());
    }

    /// <summary>
    /// Applies the fallback selection on the next frame after layout and EventSystem startup have settled.
    /// None.
    /// </summary>
    /// <returns>Enumerator used by Unity coroutine scheduling.</returns>
    private IEnumerator DeferredDefaultSelectionCoroutine()
    {
        // Wait one frame so EventSystem and layout state are fully initialized.
        yield return null;
        deferredSelectionCoroutine = null;

        // Do not override an active pointer-hover takeover.
        if (hoveredSelectable != null)
            yield break;

        SelectSelectable(defaultSelectable, rememberAsDefault : false);
    }

    /// <summary>
    /// Restores either the pre-hover selection or the configured fallback selectable after hover ends.
    /// None.
    /// </summary>
    private void RestoreSelectionAfterHover()
    {
        // Restore the last keyboard/controller selection when it is still valid.
        Selectable previousSelectable = ResolveSelectable(selectionBeforeHover);
        selectionBeforeHover = null;

        if (IsSelectionCandidateValid(previousSelectable))
        {
            ApplySelection(previousSelectable);
            return;
        }

        // Fall back to the configured default selection when no previous button can be restored.
        SelectSelectable(defaultSelectable, rememberAsDefault : false);
    }

    /// <summary>
    /// Applies one selection to the resolved EventSystem using both Button.Select and SetSelectedGameObject.
    /// </summary>
    /// <param name="selectable">Selectable that should own the current UI focus.</param>
    private void ApplySelection(Selectable selectable)
    {
        EventSystem resolvedEventSystem = ResolveEventSystem();

        if (resolvedEventSystem == null)
            return;

        // Force an up-to-date layout before changing focus state.
        Canvas.ForceUpdateCanvases();
        resolvedEventSystem.SetSelectedGameObject(null);
        selectable.Select();
        resolvedEventSystem.SetSelectedGameObject(selectable.gameObject);
    }

    /// <summary>
    /// Resolves the usable EventSystem instance for this menu.
    /// None.
    /// </summary>
    /// <returns>EventSystem used by this menu, or null when none is available.</returns>
    private EventSystem ResolveEventSystem()
    {
        if (eventSystemOverride != null)
            return eventSystemOverride;

        return EventSystem.current;
    }

    /// <summary>
    /// Resolves one Selectable component from the provided GameObject when present.
    /// </summary>
    /// <param name="targetObject">GameObject that may carry a Selectable component.</param>
    /// <returns>Resolved selectable or null when none is available.</returns>
    private static Selectable ResolveSelectable(GameObject targetObject)
    {
        if (targetObject == null)
            return null;

        return targetObject.GetComponent<Selectable>();
    }

    /// <summary>
    /// Checks whether a selectable can safely receive focus at the current moment.
    /// </summary>
    /// <param name="selectable">Selectable to validate.</param>
    /// <returns>True when the selectable is active, interactable and usable, otherwise false.</returns>
    private static bool IsSelectionCandidateValid(Selectable selectable)
    {
        if (selectable == null)
            return false;

        if (!selectable.gameObject.activeInHierarchy)
            return false;

        return selectable.IsInteractable();
    }
    #endregion

    #endregion
}
