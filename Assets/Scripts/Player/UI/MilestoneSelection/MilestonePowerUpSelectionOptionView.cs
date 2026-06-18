using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Stores milestone power-up card references and forwards pointer interaction to the HUD selection controller.
/// </summary>
[DisallowMultipleComponent]
public sealed class MilestonePowerUpSelectionOptionView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Text label used to show the power-up title on this card.")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("Text label used to show the power-up description on this card.")]
    [SerializeField] private TMP_Text descriptionText;

    [Tooltip("Optional text label used to show the unlock kind when the prefab exposes one.")]
    [SerializeField] private TMP_Text typeText;

    [Tooltip("Optional image reserved for a future power-up icon on this card.")]
    [SerializeField] private Image iconImage;

    [Tooltip("Graphic enabled only when this card is the currently selected milestone option.")]
    [SerializeField] private Graphic selectionHighlightGraphic;

    [Tooltip("Disables the whole card GameObject when no offer is assigned to this slot.")]
    [SerializeField] private bool hideWhenUnused = true;

    [Tooltip("Updates the current keyboard/gamepad selection when the mouse hovers this card.")]
    [SerializeField] private bool selectOnPointerEnter = true;
    #endregion

    private Action<MilestonePowerUpSelectionOptionView> clickCallback;
    private Action<MilestonePowerUpSelectionOptionView> hoverCallback;
    private bool isInteractable = true;
    #endregion

    #region Properties
    public TMP_Text NameText
    {
        get
        {
            return nameText;
        }
    }

    public TMP_Text DescriptionText
    {
        get
        {
            return descriptionText;
        }
    }

    public TMP_Text TypeText
    {
        get
        {
            return typeText;
        }
    }

    public Image IconImage
    {
        get
        {
            return iconImage;
        }
    }

    public bool HideWhenUnused
    {
        get
        {
            return hideWhenUnused;
        }
    }

    public bool SelectOnPointerEnter
    {
        get
        {
            return selectOnPointerEnter;
        }
    }
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Resolves missing child references the first time the prefab instance becomes active.
    /// </summary>

    private void Awake()
    {
        CacheReferences();
    }

    /// <summary>
    /// Forwards left-click selection requests to the owning milestone HUD section.
    /// </summary>
    /// <param name="eventData">Pointer event raised by the current EventSystem.</param>

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!CanForwardPointerInput(eventData))
            return;

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        clickCallback.Invoke(this);
    }

    /// <summary>
    /// Moves the current selection highlight to this card when hover-based sync is enabled.
    /// </summary>
    /// <param name="eventData">Pointer event raised by the current EventSystem.</param>

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!CanForwardPointerInput(eventData))
            return;

        if (!selectOnPointerEnter)
            return;

        hoverCallback.Invoke(this);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Refreshes auto-resolved references while editing the prefab or scene instance.
    /// </summary>

    private void OnValidate()
    {
        CacheReferences();
    }

    /// <summary>
    /// Populates default child references when the component is first added from the Inspector.
    /// </summary>

    private void Reset()
    {
        CacheReferences();
    }
#endif
    #endregion

    #region Public Methods
    /// <summary>
    /// Registers delegates invoked by pointer click and hover events on this card.
    /// </summary>
    /// <param name="clickCallbackValue">Callback invoked when the card is clicked.</param>
    /// <param name="hoverCallbackValue">Callback invoked when the card is hovered.</param>

    public void RegisterCallbacks(Action<MilestonePowerUpSelectionOptionView> clickCallbackValue,
                                  Action<MilestonePowerUpSelectionOptionView> hoverCallbackValue)
    {
        clickCallback = clickCallbackValue;
        hoverCallback = hoverCallbackValue;
    }

    /// <summary>
    /// Clears all registered interaction delegates when the HUD section is disposed or rebuilt.
    /// </summary>

    public void ClearCallbacks()
    {
        clickCallback = null;
        hoverCallback = null;
    }

    /// <summary>
    /// Enables or disables the selection highlight associated with this card.
    /// </summary>
    /// <param name="isSelected">True to show the highlight; false to hide it.</param>

    public void SetSelected(bool isSelected)
    {
        if (selectionHighlightGraphic == null)
            return;

        if (selectionHighlightGraphic.enabled == isSelected)
            return;

        selectionHighlightGraphic.enabled = isSelected;
    }

    /// <summary>
    /// Updates the runtime interactable state used to filter pointer input after a command is queued.
    /// </summary>
    /// <param name="interactable">True to accept pointer input; false to ignore it.</param>

    public void SetInteractable(bool interactable)
    {
        isInteractable = interactable;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves missing child references from the expected milestone card hierarchy.
    /// </summary>

    private void CacheReferences()
    {
        Transform rootTransform = transform;

        if (nameText == null)
            nameText = ResolveText(rootTransform, "NamePowerUp");

        if (descriptionText == null)
            descriptionText = ResolveText(rootTransform, "DescriptionPowerUp");

        if (typeText == null)
            typeText = ResolveText(rootTransform, "TypePowerUp");

        if (iconImage == null)
            iconImage = ResolveImage(rootTransform, "Sprite");

        if (selectionHighlightGraphic == null)
            selectionHighlightGraphic = ResolveGraphic(rootTransform, "Corner");
    }

    /// <summary>
    /// Resolves one TMP text child from the expected milestone card hierarchy.
    /// </summary>
    /// <param name="rootTransform">Root transform used for descendant lookup.</param>
    /// <param name="childName">Expected descendant GameObject name.</param>
    /// <returns>Resolved TMP text when found; otherwise null.</returns>
    private static TMP_Text ResolveText(Transform rootTransform, string childName)
    {
        Transform childTransform = HUDMilestoneSelectionOptionUtility.FindDescendantByName(rootTransform, childName);

        if (childTransform == null)
            return null;

        return childTransform.GetComponent<TMP_Text>();
    }

    /// <summary>
    /// Resolves one Image child from the expected milestone card hierarchy.
    /// </summary>
    /// <param name="rootTransform">Root transform used for descendant lookup.</param>
    /// <param name="childName">Expected descendant GameObject name.</param>
    /// <returns>Resolved Image when found; otherwise null.</returns>
    private static Image ResolveImage(Transform rootTransform, string childName)
    {
        Transform childTransform = HUDMilestoneSelectionOptionUtility.FindDescendantByName(rootTransform, childName);

        if (childTransform == null)
            return null;

        return childTransform.GetComponent<Image>();
    }

    /// <summary>
    /// Resolves one Graphic child from the expected milestone card hierarchy.
    /// </summary>
    /// <param name="rootTransform">Root transform used for descendant lookup.</param>
    /// <param name="childName">Expected descendant GameObject name.</param>
    /// <returns>Resolved Graphic when found; otherwise null.</returns>
    private static Graphic ResolveGraphic(Transform rootTransform, string childName)
    {
        Transform childTransform = HUDMilestoneSelectionOptionUtility.FindDescendantByName(rootTransform, childName);

        if (childTransform == null)
            return null;

        return childTransform.GetComponent<Graphic>();
    }

    /// <summary>
    /// Validates whether the current pointer event can be forwarded to the milestone HUD controller.
    /// </summary>
    /// <param name="eventData">Pointer event raised by the current EventSystem.</param>
    /// <returns>True when the callbacks can be invoked; otherwise false.</returns>
    private bool CanForwardPointerInput(PointerEventData eventData)
    {
        if (!isActiveAndEnabled)
            return false;

        if (!isInteractable)
            return false;

        if (eventData == null)
            return false;

        if (clickCallback == null && hoverCallback == null)
            return false;

        return true;
    }
    #endregion

    #endregion
}
