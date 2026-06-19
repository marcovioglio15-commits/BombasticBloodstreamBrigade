using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides milestone power-up HUD helpers for input gating, selection indexing, and custom navigation.
/// </summary>
public static class HUDMilestoneSelectionNavigationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Returns whether custom milestone selection input should currently be processed.
    /// </summary>
    /// <param name="hasRuntimeContext">True when the HUD owns a valid runtime EntityManager and player entity.</param>
    /// <param name="isPanelVisible">True when the milestone panel is currently shown.</param>
    /// <param name="interactionLocked">True when a command was already queued and input must be ignored.</param>
    /// <param name="activeSelectableCount">Number of rolled offers plus any optional skip control currently shown to the player.</param>
    /// <returns>True when custom input should be processed; otherwise false.</returns>
    public static bool CanHandleSelectionInput(bool hasRuntimeContext,
                                               bool isPanelVisible,
                                               bool interactionLocked,
                                               int activeSelectableCount)
    {
        if (!hasRuntimeContext)
            return false;

        if (!isPanelVisible)
            return false;

        if (interactionLocked)
            return false;

        if (activeSelectableCount <= 0)
            return false;

        return true;
    }

    /// <summary>
    /// Resolves the number of navigable milestone controls, including the optional skip button.
    /// </summary>
    /// <param name="activeOfferCount">Number of rolled offers currently shown to the player.</param>
    /// <param name="hasSkipButton">True when the milestone panel exposes a skip button.</param>
    /// <returns>Total number of controls reachable through custom navigation.</returns>
    public static int ResolveSelectableCount(int activeOfferCount, bool hasSkipButton)
    {
        int selectableCount = Mathf.Max(0, activeOfferCount);
        return hasSkipButton ? selectableCount + 1 : selectableCount;
    }

    /// <summary>
    /// Normalizes the selected offer index so it always points to a valid rolled offer.
    /// </summary>
    /// <param name="selectedOfferIndex">Current selected offer index cached by the HUD section.</param>
    /// <param name="activeOfferCount">Number of rolled offers currently shown to the player.</param>
    /// <returns>Valid selected offer index, or -1 when no offers are available.</returns>
    public static int NormalizeSelectedOfferIndex(int selectedOfferIndex, int activeOfferCount)
    {
        if (activeOfferCount <= 0)
            return -1;

        if (selectedOfferIndex >= 0 && selectedOfferIndex < activeOfferCount)
            return selectedOfferIndex;

        return 0;
    }

    /// <summary>
    /// Normalizes the current navigation index so it points to either a rolled offer or the optional skip button.
    /// </summary>
    /// <param name="selectedIndex">Current navigation index cached by the HUD section.</param>
    /// <param name="activeOfferCount">Number of rolled offers currently shown to the player.</param>
    /// <param name="hasSkipButton">True when the milestone panel exposes a skip button.</param>
    /// <returns>Valid navigation index, or -1 when no selectable controls exist.</returns>
    public static int NormalizeSelectedIndex(int selectedIndex, int activeOfferCount, bool hasSkipButton)
    {
        int selectableCount = ResolveSelectableCount(activeOfferCount, hasSkipButton);

        if (selectableCount <= 0)
            return -1;

        if (selectedIndex >= 0 && selectedIndex < selectableCount)
            return selectedIndex;

        return 0;
    }

    /// <summary>
    /// Resolves whether one navigation index points to the skip button.
    /// </summary>
    /// <param name="selectedIndex">Current navigation index cached by the HUD section.</param>
    /// <param name="activeOfferCount">Number of rolled offers currently shown to the player.</param>
    /// <param name="hasSkipButton">True when the milestone panel exposes a skip button.</param>
    /// <returns>True when the index points at Skip; otherwise false.</returns>
    public static bool IsSkipSelectionIndex(int selectedIndex, int activeOfferCount, bool hasSkipButton)
    {
        return hasSkipButton && selectedIndex == activeOfferCount;
    }

    /// <summary>
    /// Resolves the navigation index used by the optional skip button.
    /// </summary>
    /// <param name="activeOfferCount">Number of rolled offers currently shown to the player.</param>
    /// <param name="hasSkipButton">True when the milestone panel exposes a skip button.</param>
    /// <returns>Skip navigation index, or -1 when no skip button is available.</returns>
    public static int ResolveSkipSelectionIndex(int activeOfferCount, bool hasSkipButton)
    {
        return hasSkipButton ? Mathf.Max(0, activeOfferCount) : -1;
    }

    /// <summary>
    /// Calculates the next selected offer index after one discrete navigation step.
    /// </summary>
    /// <param name="selectedOfferIndex">Current selected offer index cached by the HUD section.</param>
    /// <param name="activeSelectableCount">Number of rolled offers plus any optional skip control currently shown to the player.</param>
    /// <param name="navigationStep">Discrete navigation step resolved from the current input value.</param>
    /// <param name="wrapNavigation">True to loop from end to start and vice versa.</param>
    /// <returns>Next selected offer index after the requested navigation step.</returns>
    public static int MoveSelection(int selectedOfferIndex,
                                    int activeSelectableCount,
                                    int navigationStep,
                                    bool wrapNavigation)
    {
        if (activeSelectableCount <= 0)
            return -1;

        int nextOptionIndex = selectedOfferIndex + navigationStep;

        if (wrapNavigation)
        {
            if (nextOptionIndex < 0)
                return activeSelectableCount - 1;

            if (nextOptionIndex >= activeSelectableCount)
                return 0;

            return nextOptionIndex;
        }

        return Mathf.Clamp(nextOptionIndex, 0, activeSelectableCount - 1);
    }

    /// <summary>
    /// Converts a UI Navigate vector into one discrete selection step for the milestone card list.
    /// </summary>
    /// <param name="navigateValue">Navigate action value read from the New Input System.</param>
    /// <param name="deadzone">Minimum axis magnitude required before a navigation step is accepted.</param>
    /// <returns>-1 for previous, 1 for next, or 0 when the input should be ignored.</returns>
    public static int ResolveNavigationStep(Vector2 navigateValue, float deadzone)
    {
        float absoluteX = Mathf.Abs(navigateValue.x);
        float absoluteY = Mathf.Abs(navigateValue.y);

        if (absoluteX < deadzone && absoluteY < deadzone)
            return 0;

        if (absoluteY >= absoluteX)
            return navigateValue.y > 0f ? -1 : 1;

        return navigateValue.x > 0f ? 1 : -1;
    }

    /// <summary>
    /// Resolves the display-order index associated with one auto-discovered milestone card view.
    /// </summary>
    /// <param name="optionViews">Auto-discovered card views under the panel hierarchy.</param>
    /// <param name="optionView">Card view searched inside the discovered collection.</param>
    /// <param name="activeOfferCount">Number of rolled offers currently shown to the player.</param>
    /// <param name="optionIndex">Resolved display-order index when found.</param>
    /// <returns>True when the card view belongs to the current panel and maps to a valid offer; otherwise false.</returns>
    public static bool TryGetOptionViewIndex(IReadOnlyList<MilestonePowerUpSelectionOptionView> optionViews,
                                             MilestonePowerUpSelectionOptionView optionView,
                                             int activeOfferCount,
                                             out int optionIndex)
    {
        optionIndex = -1;

        if (optionViews == null || optionView == null)
            return false;

        for (int discoveredIndex = 0; discoveredIndex < optionViews.Count; discoveredIndex++)
        {
            if (!ReferenceEquals(optionViews[discoveredIndex], optionView))
                continue;

            if (discoveredIndex >= activeOfferCount)
                return false;

            optionIndex = discoveredIndex;
            return true;
        }

        return false;
    }
    #endregion

    #endregion
}
