using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Stateless helpers for direct gamepad navigation across authored Unity UI Selectables.
/// </summary>
internal static class RuntimeMenuDirectNavigationUtility
{
    #region Constants
    private const float SliderStepFraction = 0.05f;
    #endregion

    #region Methods

    #region Navigation
    /// <summary>
    /// Moves direct-selection focus or edits the selected slider according to the requested direction.
    /// </summary>
    /// <param name="eventSystem">EventSystem that owns current focus.</param>
    /// <param name="menuRoot">Menu root used to keep focus inside the active overlay.</param>
    /// <param name="defaultSelectable">Fallback selectable used when no graph target exists.</param>
    /// <param name="direction">Resolved navigation direction.</param>
    public static void Navigate(EventSystem eventSystem,
                                GameObject menuRoot,
                                Selectable defaultSelectable,
                                RuntimeMenuNavigationDirection direction)
    {
        Selectable currentSelectable = ResolveCurrentSelectable(eventSystem, menuRoot);

        if (TryAdjustSelectedSlider(currentSelectable, direction))
            return;

        Selectable nextSelectable = ResolveNextSelectable(currentSelectable, direction);

        if (nextSelectable == null)
            nextSelectable = defaultSelectable;

        SelectSelectable(eventSystem, nextSelectable);
    }

    /// <summary>
    /// Submits the currently focused selectable through Unity's standard submit event.
    /// </summary>
    /// <param name="eventSystem">EventSystem that owns current focus.</param>
    /// <param name="menuRoot">Menu root used to keep focus inside the active overlay.</param>
    /// <param name="defaultSelectable">Fallback selectable used when no control is focused.</param>
    public static void SubmitSelection(EventSystem eventSystem, GameObject menuRoot, Selectable defaultSelectable)
    {
        Selectable selectable = ResolveCurrentSelectable(eventSystem, menuRoot);

        if (selectable == null)
        {
            SelectDefaultIfNeeded(eventSystem, menuRoot, defaultSelectable);
            return;
        }

        BaseEventData eventData = new BaseEventData(eventSystem);
        ExecuteEvents.Execute(selectable.gameObject, eventData, ExecuteEvents.submitHandler);
    }

    /// <summary>
    /// Applies default focus when direct navigation starts without a selected control.
    /// </summary>
    /// <param name="eventSystem">EventSystem that owns current focus.</param>
    /// <param name="menuRoot">Menu root used to keep focus inside the active overlay.</param>
    /// <param name="defaultSelectable">Selectable to focus when nothing valid is selected.</param>
    public static void SelectDefaultIfNeeded(EventSystem eventSystem, GameObject menuRoot, Selectable defaultSelectable)
    {
        if (ResolveCurrentSelectable(eventSystem, menuRoot) != null)
            return;

        SelectSelectable(eventSystem, defaultSelectable);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Adjusts a focused slider horizontally so controller users can edit numeric values without pointer emulation.
    /// </summary>
    /// <param name="selectable">Current selectable focus.</param>
    /// <param name="direction">Requested navigation direction.</param>
    /// <returns>True when a slider consumed the navigation input.</returns>
    public static bool TryAdjustSelectedSlider(Selectable selectable, RuntimeMenuNavigationDirection direction)
    {
        if (direction != RuntimeMenuNavigationDirection.Left && direction != RuntimeMenuNavigationDirection.Right)
            return false;

        Slider slider = selectable as Slider;

        if (slider == null || !slider.IsInteractable())
            return false;

        float sign = direction == RuntimeMenuNavigationDirection.Right ? 1f : -1f;
        float range = Mathf.Max(0.0001f, slider.maxValue - slider.minValue);
        float step = slider.wholeNumbers ? 1f : range * SliderStepFraction;
        slider.value = Mathf.Clamp(slider.value + step * sign, slider.minValue, slider.maxValue);
        return true;
    }

    /// <summary>
    /// Resolves the next selectable by using the authored Unity navigation graph.
    /// </summary>
    /// <param name="currentSelectable">Selectable that currently owns focus.</param>
    /// <param name="direction">Requested navigation direction.</param>
    /// <returns>Next selectable, or null when navigation has no explicit target.</returns>
    private static Selectable ResolveNextSelectable(Selectable currentSelectable, RuntimeMenuNavigationDirection direction)
    {
        if (currentSelectable == null)
            return null;

        switch (direction)
        {
            case RuntimeMenuNavigationDirection.Up:
                return currentSelectable.FindSelectableOnUp();
            case RuntimeMenuNavigationDirection.Down:
                return currentSelectable.FindSelectableOnDown();
            case RuntimeMenuNavigationDirection.Left:
                return currentSelectable.FindSelectableOnLeft();
            case RuntimeMenuNavigationDirection.Right:
                return currentSelectable.FindSelectableOnRight();
            default:
                return null;
        }
    }

    /// <summary>
    /// Resolves the current selectable from the EventSystem when it belongs to this menu root.
    /// </summary>
    /// <param name="eventSystem">EventSystem that owns current focus.</param>
    /// <param name="menuRoot">Menu root used to keep focus inside the active overlay.</param>
    /// <returns>Current selectable, or null when none is valid.</returns>
    public static Selectable ResolveCurrentSelectable(EventSystem eventSystem, GameObject menuRoot)
    {
        if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
            return null;

        if (menuRoot != null && !eventSystem.currentSelectedGameObject.transform.IsChildOf(menuRoot.transform))
            return null;

        Selectable selectable = eventSystem.currentSelectedGameObject.GetComponent<Selectable>();

        if (!IsSelectionCandidateValid(selectable))
            return null;

        return selectable;
    }

    /// <summary>
    /// Applies focus to one selectable through the active EventSystem.
    /// </summary>
    /// <param name="eventSystem">EventSystem that receives selected GameObject updates.</param>
    /// <param name="selectable">Selectable that should own menu focus.</param>
    public static void SelectSelectable(EventSystem eventSystem, Selectable selectable)
    {
        if (!IsSelectionCandidateValid(selectable))
            return;

        if (eventSystem == null)
            return;

        Canvas.ForceUpdateCanvases();
        eventSystem.SetSelectedGameObject(null);
        selectable.Select();
        eventSystem.SetSelectedGameObject(selectable.gameObject);
    }

    /// <summary>
    /// Checks whether a selectable can safely receive focus at the current moment.
    /// </summary>
    /// <param name="selectable">Selectable to inspect.</param>
    /// <returns>True when the selectable is active and interactable.</returns>
    public static bool IsSelectionCandidateValid(Selectable selectable)
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

/// <summary>
/// Cardinal directions used by direct runtime menu navigation.
/// </summary>
internal enum RuntimeMenuNavigationDirection
{
    None = 0,
    Up = 1,
    Down = 2,
    Left = 3,
    Right = 4
}
