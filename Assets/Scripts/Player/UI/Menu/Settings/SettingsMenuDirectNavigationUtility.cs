using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Resolves deterministic Settings focus movement while excluding macro tabs and optionally expandable headers.
/// </summary>
internal static class SettingsMenuDirectNavigationUtility
{
    #region Constants
    private const float MinimumDirectionalDistance = 0.5f;
    private const float SameRowTolerance = 12f;
    private const float PrimaryDistanceWeight = 10f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Moves focus through currently visible Settings options or adjusts a focused slider horizontally.
    /// </summary>
    /// <param name="eventSystem">EventSystem that owns current focus.</param>
    /// <param name="menuRoot">Settings root constraining all candidates.</param>
    /// <param name="defaultSelectable">Preferred fallback when no option currently owns focus.</param>
    /// <param name="candidates">Precached authored Selectables from active and inactive macro panels.</param>
    /// <param name="audioTabButton">Audio macro-tab button excluded from content navigation.</param>
    /// <param name="gameplayTabButton">Gameplay macro-tab button excluded from content navigation.</param>
    /// <param name="includeDropdownHeaders">True when expandable section headers may receive focus.</param>
    /// <param name="direction">Requested cardinal navigation direction.</param>
    public static void Navigate(EventSystem eventSystem,
                                GameObject menuRoot,
                                Selectable defaultSelectable,
                                Selectable[] candidates,
                                Selectable audioTabButton,
                                Selectable gameplayTabButton,
                                bool includeDropdownHeaders,
                                RuntimeMenuNavigationDirection direction)
    {
        Selectable current = RuntimeMenuDirectNavigationUtility.ResolveCurrentSelectable(eventSystem, menuRoot);

        // Recover focus from the active panel before attempting a directional step.
        if (!IsAllowed(defaultSelectable,
                       menuRoot,
                       audioTabButton,
                       gameplayTabButton,
                       includeDropdownHeaders))
            defaultSelectable = ResolveFirstAllowed(candidates,
                                                    menuRoot,
                                                    audioTabButton,
                                                    gameplayTabButton,
                                                    includeDropdownHeaders);

        if (current == null)
        {
            RuntimeMenuDirectNavigationUtility.SelectSelectable(eventSystem, defaultSelectable);
            return;
        }

        if (RuntimeMenuDirectNavigationUtility.TryAdjustSelectedSlider(current, direction))
            return;

        Selectable next = ResolveDirectionalCandidate(current,
                                                      candidates,
                                                      menuRoot,
                                                      audioTabButton,
                                                      gameplayTabButton,
                                                      includeDropdownHeaders,
                                                      direction);

        if (next != null)
            RuntimeMenuDirectNavigationUtility.SelectSelectable(eventSystem, next);
    }
    #endregion

    #region Candidate Resolution
    /// <summary>
    /// Finds the nearest valid option in the requested direction using current post-layout positions.
    /// </summary>
    /// <param name="current">Selectable that currently owns focus.</param>
    /// <param name="candidates">Precached Settings Selectables.</param>
    /// <param name="menuRoot">Settings root constraining candidates.</param>
    /// <param name="audioTabButton">Audio macro tab excluded from content navigation.</param>
    /// <param name="gameplayTabButton">Gameplay macro tab excluded from content navigation.</param>
    /// <param name="includeDropdownHeaders">True when expandable section headers may receive focus.</param>
    /// <param name="direction">Requested cardinal navigation direction.</param>
    /// <returns>Nearest valid candidate, or null when the requested direction has no option.</returns>
    private static Selectable ResolveDirectionalCandidate(Selectable current,
                                                          Selectable[] candidates,
                                                          GameObject menuRoot,
                                                          Selectable audioTabButton,
                                                          Selectable gameplayTabButton,
                                                          bool includeDropdownHeaders,
                                                          RuntimeMenuNavigationDirection direction)
    {
        if (candidates == null)
            return null;

        Vector3 currentPosition = current.transform.position;
        Selectable bestCandidate = null;
        float bestScore = float.PositiveInfinity;

        // Compare only visible, interactable controls that belong to the active macro panel or shared footer.
        for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
        {
            Selectable candidate = candidates[candidateIndex];

            if (candidate == current ||
                !IsAllowed(candidate,
                           menuRoot,
                           audioTabButton,
                           gameplayTabButton,
                           includeDropdownHeaders))
                continue;

            if (!TryCalculateDirectionalScore(currentPosition,
                                              candidate.transform.position,
                                              direction,
                                              out float score) ||
                score >= bestScore)
                continue;

            bestScore = score;
            bestCandidate = candidate;
        }

        return bestCandidate;
    }

    /// <summary>
    /// Resolves the first currently valid authored option for focus recovery.
    /// </summary>
    /// <param name="candidates">Precached Settings Selectables.</param>
    /// <param name="menuRoot">Settings root constraining candidates.</param>
    /// <param name="audioTabButton">Audio macro tab excluded from content navigation.</param>
    /// <param name="gameplayTabButton">Gameplay macro tab excluded from content navigation.</param>
    /// <param name="includeDropdownHeaders">True when expandable section headers may receive focus.</param>
    /// <returns>First valid candidate in authored hierarchy order, or null.</returns>
    private static Selectable ResolveFirstAllowed(Selectable[] candidates,
                                                  GameObject menuRoot,
                                                  Selectable audioTabButton,
                                                  Selectable gameplayTabButton,
                                                  bool includeDropdownHeaders)
    {
        if (candidates == null)
            return null;

        // Hierarchy order is deterministic and follows the preauthored Settings row order.
        for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
        {
            Selectable candidate = candidates[candidateIndex];

            if (IsAllowed(candidate,
                          menuRoot,
                          audioTabButton,
                          gameplayTabButton,
                          includeDropdownHeaders))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Checks common focus eligibility plus Settings-specific macro-tab and dropdown-header exclusions.
    /// </summary>
    /// <param name="candidate">Selectable to inspect.</param>
    /// <param name="menuRoot">Settings root constraining candidates.</param>
    /// <param name="audioTabButton">Audio macro tab excluded from content navigation.</param>
    /// <param name="gameplayTabButton">Gameplay macro tab excluded from content navigation.</param>
    /// <param name="includeDropdownHeaders">True when expandable section headers may receive focus.</param>
    /// <returns>True when the candidate may receive direct Settings focus.</returns>
    private static bool IsAllowed(Selectable candidate,
                                  GameObject menuRoot,
                                  Selectable audioTabButton,
                                  Selectable gameplayTabButton,
                                  bool includeDropdownHeaders)
    {
        if (!RuntimeMenuDirectNavigationUtility.IsSelectionCandidateValid(candidate))
            return false;

        if (menuRoot != null && !candidate.transform.IsChildOf(menuRoot.transform))
            return false;

        if (candidate == audioTabButton || candidate == gameplayTabButton)
            return false;

        if (includeDropdownHeaders)
            return true;

        SettingsDropdownSection section = candidate.GetComponentInParent<SettingsDropdownSection>();
        return section == null || section.HeaderButton != candidate;
    }
    #endregion

    #region Spatial Scoring
    /// <summary>
    /// Calculates a stable directional score while keeping horizontal navigation on the current visual row.
    /// </summary>
    /// <param name="currentPosition">World position of the focused control.</param>
    /// <param name="candidatePosition">World position of a candidate control.</param>
    /// <param name="direction">Requested cardinal direction.</param>
    /// <param name="score">Weighted directional distance when the candidate qualifies.</param>
    /// <returns>True when the candidate lies in the requested direction.</returns>
    private static bool TryCalculateDirectionalScore(Vector3 currentPosition,
                                                     Vector3 candidatePosition,
                                                     RuntimeMenuNavigationDirection direction,
                                                     out float score)
    {
        float horizontalDistance = candidatePosition.x - currentPosition.x;
        float verticalDistance = candidatePosition.y - currentPosition.y;
        float primaryDistance;
        float secondaryDistance;

        switch (direction)
        {
            case RuntimeMenuNavigationDirection.Up:
                primaryDistance = verticalDistance;
                secondaryDistance = Mathf.Abs(horizontalDistance);
                break;
            case RuntimeMenuNavigationDirection.Down:
                primaryDistance = -verticalDistance;
                secondaryDistance = Mathf.Abs(horizontalDistance);
                break;
            case RuntimeMenuNavigationDirection.Left:
                primaryDistance = -horizontalDistance;
                secondaryDistance = Mathf.Abs(verticalDistance);
                break;
            case RuntimeMenuNavigationDirection.Right:
                primaryDistance = horizontalDistance;
                secondaryDistance = Mathf.Abs(verticalDistance);
                break;
            default:
                score = 0f;
                return false;
        }

        bool horizontalDirection = direction == RuntimeMenuNavigationDirection.Left ||
                                   direction == RuntimeMenuNavigationDirection.Right;

        if (primaryDistance <= MinimumDirectionalDistance ||
            horizontalDirection && secondaryDistance > SameRowTolerance)
        {
            score = 0f;
            return false;
        }

        score = primaryDistance * PrimaryDistanceWeight + secondaryDistance;
        return true;
    }
    #endregion

    #endregion
}
