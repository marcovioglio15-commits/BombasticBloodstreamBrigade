using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Provides milestone power-up HUD helpers for card discovery, data rendering, and custom navigation resolution.
/// </summary>
public static class HUDMilestoneSelectionOptionUtility
{
    #region Fields
    private const string PowerUpListRootName = "PowerUpList";
    #endregion

    #region Methods

    #region Discovery
    /// <summary>
    /// Returns whether the milestone selection panel exposes at least one usable UI control.
    /// </summary>
    /// <param name="panelRoot">Panel root potentially shown while the selection is active.</param>
    /// <param name="skipButton">Optional skip button configured for the panel.</param>
    /// <param name="optionViews">Auto-discovered card views under the panel hierarchy.</param>
    /// <returns>True when any valid UI control exists; otherwise false.</returns>
    public static bool HasUiConfigured(GameObject panelRoot,
                                       Button skipButton,
                                       IReadOnlyList<MilestonePowerUpSelectionOptionView> optionViews)
    {
        if (panelRoot != null)
            return true;

        if (skipButton != null)
            return true;

        return HasDiscoveredOptionView(optionViews);
    }

    /// <summary>
    /// Returns whether the current panel exposes at least one auto-discovered card view.
    /// </summary>
    /// <param name="optionViews">Auto-discovered card views under the panel hierarchy.</param>
    /// <returns>True when at least one valid card view exists; otherwise false.</returns>
    public static bool HasDiscoveredOptionView(IReadOnlyList<MilestonePowerUpSelectionOptionView> optionViews)
    {
        if (optionViews == null || optionViews.Count <= 0)
            return false;

        for (int optionIndex = 0; optionIndex < optionViews.Count; optionIndex++)
        {
            if (optionViews[optionIndex] != null)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Collects milestone power-up card views from the configured panel hierarchy in display order.
    /// </summary>
    /// <param name="panelRoot">Panel root that owns the milestone power-up cards.</param>
    /// <param name="optionViews">Destination list populated with resolved card views.</param>
    /// <param name="maxOptionCount">Maximum number of card views to collect.</param>

    public static void DiscoverOptionViews(GameObject panelRoot,
                                           List<MilestonePowerUpSelectionOptionView> optionViews,
                                           int maxOptionCount)
    {
        optionViews.Clear();

        if (panelRoot == null)
            return;

        if (maxOptionCount <= 0)
            return;

        Transform panelTransform = panelRoot.transform;
        Transform listRootTransform = FindDescendantByName(panelTransform, PowerUpListRootName);

        if (listRootTransform == null)
            listRootTransform = panelTransform;

        int childCount = listRootTransform.childCount;

        for (int childIndex = 0; childIndex < childCount; childIndex++)
        {
            Transform childTransform = listRootTransform.GetChild(childIndex);

            if (childTransform == null)
                continue;

            MilestonePowerUpSelectionOptionView optionView = childTransform.GetComponent<MilestonePowerUpSelectionOptionView>();

            if (optionView == null)
                continue;

            optionViews.Add(optionView);

            if (optionViews.Count >= maxOptionCount)
                return;
        }
    }

    /// <summary>
    /// Finds the first descendant transform matching the requested name using depth-first traversal.
    /// </summary>
    /// <param name="rootTransform">Root transform used as traversal start point.</param>
    /// <param name="targetName">Exact GameObject name requested.</param>
    /// <returns>Resolved descendant transform when found; otherwise null.</returns>
    public static Transform FindDescendantByName(Transform rootTransform, string targetName)
    {
        if (rootTransform == null)
            return null;

        if (string.IsNullOrWhiteSpace(targetName))
            return null;

        if (string.Equals(rootTransform.name, targetName))
            return rootTransform;

        int childCount = rootTransform.childCount;

        for (int childIndex = 0; childIndex < childCount; childIndex++)
        {
            Transform childTransform = rootTransform.GetChild(childIndex);
            Transform resolvedTransform = FindDescendantByName(childTransform, targetName);

            if (resolvedTransform != null)
                return resolvedTransform;
        }

        return null;
    }
    #endregion

    #region Rendering
    /// <summary>
    /// Clears every discovered card view currently owned by the milestone selection panel.
    /// </summary>
    /// <param name="optionViews">Auto-discovered card views under the panel hierarchy.</param>

    public static void ResetOptionViews(IReadOnlyList<MilestonePowerUpSelectionOptionView> optionViews)
    {
        if (optionViews == null)
            return;

        for (int optionIndex = 0; optionIndex < optionViews.Count; optionIndex++)
        {
            MilestonePowerUpSelectionOptionView optionView = optionViews[optionIndex];
            SetOptionViewUnused(optionView);
        }
    }

    /// Applies current ECS offer data to every discovered card view.
    /// </summary>
    /// <param name="optionViews">Auto-discovered card views under the panel hierarchy.</param>
    /// <param name="selectionOffers">Current buffer of rolled milestone offers.</param>
    /// <param name="activeOfferCount">Number of rolled offers currently shown to the player.</param>

    public static void RenderOptionViews(IReadOnlyList<MilestonePowerUpSelectionOptionView> optionViews,
                                         DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffers,
                                         int activeOfferCount)
    {
        if (optionViews == null)
            return;

        for (int optionIndex = 0; optionIndex < optionViews.Count; optionIndex++)
        {
            MilestonePowerUpSelectionOptionView optionView = optionViews[optionIndex];

            if (optionIndex >= activeOfferCount)
            {
                SetOptionViewUnused(optionView);
                continue;
            }

            PlayerMilestonePowerUpSelectionOfferElement offer = selectionOffers[optionIndex];
            SetOptionViewOffer(optionView, optionIndex, in offer);
        }
    }

    /// Applies one rolled milestone offer to a card view resolved from the panel prefab.
    /// </summary>
    /// <param name="optionView">Card view receiving the offer data.</param>
    /// <param name="optionIndex">Display order index used for numbering.</param>
    /// <param name="offer">Rolled milestone offer bound to the card.</param>

    public static void SetOptionViewOffer(MilestonePowerUpSelectionOptionView optionView,
                                          int optionIndex,
                                          in PlayerMilestonePowerUpSelectionOfferElement offer)
    {
        if (optionView == null)
            return;

        GameObject optionObject = optionView.gameObject;

        if (!optionObject.activeSelf)
            optionObject.SetActive(true);

        optionView.SetInteractable(true);
        optionView.SetSelected(false);
        SetText(optionView.NameText, BuildDisplayName(optionIndex, in offer));
        SetText(optionView.TypeText, ResolveUnlockKindLabel(offer.UnlockKind));
        SetIcon(optionView.IconImage, offer.PowerUpId.ToString());

        string description = optionView.TypeText != null
            ? BuildDescription(in offer)
            : BuildDescriptionWithEmbeddedType(in offer);
        SetText(optionView.DescriptionText, description);
    }

    /// <summary>
    /// Clears one card view when the current milestone rolled fewer offers than available slots.
    /// </summary>
    /// <param name="optionView">Card view reset to an unused state.</param>

    public static void SetOptionViewUnused(MilestonePowerUpSelectionOptionView optionView)
    {
        if (optionView == null)
            return;

        optionView.SetSelected(false);
        optionView.SetInteractable(false);

        if (optionView.HideWhenUnused)
        {
            if (optionView.gameObject.activeSelf)
                optionView.gameObject.SetActive(false);

            return;
        }

        if (!optionView.gameObject.activeSelf)
            optionView.gameObject.SetActive(true);

        SetText(optionView.NameText, string.Empty);
        SetText(optionView.DescriptionText, string.Empty);
        SetText(optionView.TypeText, string.Empty);
        SetIcon(optionView.IconImage, string.Empty);
    }

    /// Updates the selected highlight state for all discovered card views.
    /// </summary>
    /// <param name="optionViews">Resolved card views currently owned by the milestone panel.</param>
    /// <param name="selectedOfferIndex">Offer index currently selected by keyboard or gamepad navigation.</param>
    /// <param name="activeOfferCount">Number of rolled offers currently displayed.</param>

    public static void ApplyOptionViewSelection(IReadOnlyList<MilestonePowerUpSelectionOptionView> optionViews,
                                                int selectedOfferIndex,
                                                int activeOfferCount)
    {
        if (optionViews == null)
            return;

        for (int optionIndex = 0; optionIndex < optionViews.Count; optionIndex++)
        {
            MilestonePowerUpSelectionOptionView optionView = optionViews[optionIndex];

            if (optionView == null)
                continue;

            bool isSelected = optionIndex < activeOfferCount && optionIndex == selectedOfferIndex;
            optionView.SetSelected(isSelected);
        }
    }

    /// <summary>
    /// Updates card and skip-button interactability after a selection command is queued.
    /// </summary>
    /// <param name="optionViews">Auto-discovered card views under the panel hierarchy.</param>
    /// <param name="skipButton">Optional skip button configured for the panel.</param>
    /// <param name="interactable">True to allow input; false to block it.</param>

    public static void SetOptionInputsInteractable(IReadOnlyList<MilestonePowerUpSelectionOptionView> optionViews,
                                                   Button skipButton,
                                                   bool interactable)
    {
        if (optionViews != null)
        {
            for (int optionIndex = 0; optionIndex < optionViews.Count; optionIndex++)
            {
                MilestonePowerUpSelectionOptionView optionView = optionViews[optionIndex];

                if (optionView == null)
                    continue;

                optionView.SetInteractable(interactable);
            }
        }

        SetSkipButtonInteractable(skipButton, interactable);
    }

    /// <summary>
    /// Synchronizes card highlights with the current selected offer index.
    /// </summary>
    /// <param name="optionViews">Auto-discovered card views under the panel hierarchy.</param>
    /// <param name="selectedOfferIndex">Offer index currently selected by keyboard or gamepad navigation.</param>
    /// <param name="activeOfferCount">Number of rolled offers currently shown to the player.</param>

    public static void ApplySelectionVisuals(IReadOnlyList<MilestonePowerUpSelectionOptionView> optionViews,
                                             int selectedOfferIndex,
                                             int activeOfferCount)
    {
        ApplyOptionViewSelection(optionViews, selectedOfferIndex, activeOfferCount);
    }

    /// <summary>
    /// Shows or hides the optional skip button and aligns its interactable state with the current lock flag.
    /// </summary>
    /// <param name="skipButton">Optional skip button configured for the panel.</param>
    /// <param name="isVisible">True to show the skip button; false to hide it.</param>
    /// <param name="allowInteraction">True to keep the button interactable while visible; false to disable it.</param>

    public static void SetSkipButtonVisible(Button skipButton, bool isVisible, bool allowInteraction)
    {
        if (skipButton == null)
            return;

        GameObject skipButtonObject = skipButton.gameObject;

        if (skipButtonObject.activeSelf != isVisible)
            skipButtonObject.SetActive(isVisible);

        skipButton.interactable = isVisible && allowInteraction;
    }

    /// <summary>
    /// Enables or disables interaction on the optional skip button.
    /// </summary>
    /// <param name="skipButton">Optional skip button configured for the panel.</param>
    /// <param name="interactable">True to allow input; false to block it.</param>

    public static void SetSkipButtonInteractable(Button skipButton, bool interactable)
    {
        if (skipButton == null)
            return;

        skipButton.interactable = interactable && skipButton.gameObject.activeSelf;
    }
    #endregion

    #region Formatting
    /// <summary>
    /// Builds the milestone panel header shown while the selection is active.
    /// </summary>
    /// <param name="milestoneLevel">Milestone level that triggered the current selection panel.</param>
    /// <param name="offerCount">Number of rolled offers currently shown to the player.</param>
    /// <returns>Formatted header string for the milestone selection panel.</returns>
    public static string BuildHeaderText(int milestoneLevel, int offerCount)
    {
        return string.Format("Milestone Lv {0} - Choose 1 of {1} Power-Ups", milestoneLevel, offerCount);
    }

    /// <summary>
    /// Returns the user-facing unlock kind label for one milestone offer.
    /// </summary>
    /// <param name="unlockKind">Unlock kind stored in the rolled offer payload.</param>
    /// <returns>User-facing label for the unlock kind.</returns>
    public static string ResolveUnlockKindLabel(PlayerPowerUpUnlockKind unlockKind)
    {
        switch (unlockKind)
        {
            case PlayerPowerUpUnlockKind.Active:
                return "Active";

            case PlayerPowerUpUnlockKind.Passive:
                return "Passive";

            default:
                return "Unknown";
        }
    }

    /// <summary>
    /// Builds the numbered title shown for one offer inside the milestone selection UI.
    /// </summary>
    /// <param name="optionIndex">Display order index used for numbering.</param>
    /// <param name="offer">Rolled milestone offer bound to the UI slot.</param>
    /// <returns>Formatted display title for the option.</returns>
    private static string BuildDisplayName(int optionIndex, in PlayerMilestonePowerUpSelectionOfferElement offer)
    {
        string displayName = offer.DisplayName.ToString();

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = offer.PowerUpId.ToString();

        return string.Format("{0}. {1}", optionIndex + 1, displayName);
    }

    /// <summary>
    /// Builds the plain description shown when the prefab already exposes a dedicated unlock-kind label.
    /// </summary>
    /// <param name="offer">Rolled milestone offer bound to the UI slot.</param>
    /// <returns>Description text shown for the option.</returns>
    private static string BuildDescription(in PlayerMilestonePowerUpSelectionOfferElement offer)
    {
        string description = offer.Description.ToString();

        if (!string.IsNullOrWhiteSpace(description))
            return description;

        return "No description available.";
    }

    /// <summary>
    /// Builds the fallback description shown when the prefab does not expose a dedicated unlock-kind label.
    /// </summary>
    /// <param name="offer">Rolled milestone offer bound to the UI slot.</param>
    /// <returns>Description text with embedded unlock-kind prefix.</returns>
    private static string BuildDescriptionWithEmbeddedType(in PlayerMilestonePowerUpSelectionOfferElement offer)
    {
        string description = BuildDescription(in offer);
        string unlockKindLabel = ResolveUnlockKindLabel(offer.UnlockKind);
        return string.Format("{0} Power-Up\n{1}", unlockKindLabel, description);
    }

    /// <summary>
    /// Assigns text to an optional UI label while tolerating missing references.
    /// </summary>
    /// <param name="textLabel">UI label updated by the helper.</param>
    /// <param name="value">New string assigned to the label.</param>

    private static void SetText(TMP_Text textLabel, string value)
    {
        if (textLabel == null)
            return;

        textLabel.text = value;
    }

    /// <summary>
    /// Assigns one runtime icon to an optional UI image while tolerating missing presentation data.
    /// </summary>
    /// <param name="iconImage">UI image updated by the helper.</param>
    /// <param name="powerUpId">Stable power-up identifier used to resolve the cached sprite.</param>
    private static void SetIcon(Image iconImage, string powerUpId)
    {
        if (iconImage == null)
            return;

        if (!PlayerPowerUpPresentationRuntime.TryResolveIcon(powerUpId, out Sprite icon))
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
            return;
        }

        if (iconImage.sprite != icon)
            iconImage.sprite = icon;

        if (!iconImage.enabled)
            iconImage.enabled = true;
    }
    #endregion

    #endregion
}
