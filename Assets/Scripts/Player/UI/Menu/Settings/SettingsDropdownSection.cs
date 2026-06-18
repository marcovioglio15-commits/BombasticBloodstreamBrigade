using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives one authored expandable settings section by toggling a prebuilt content root.
/// </summary>
[DisallowMultipleComponent]
public sealed class SettingsDropdownSection : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("References")]
    [Tooltip("Button used as the section header and click target.")]
    [SerializeField] private Button headerButton;

    [Tooltip("Content root shown while the section is expanded.")]
    [SerializeField] private GameObject contentRoot;

    [Tooltip("Optional label that receives the collapsed or expanded arrow glyph.")]
    [SerializeField] private TMP_Text arrowLabel;

    [Header("State")]
    [Tooltip("Initial expanded state applied when the section object starts.")]
    [SerializeField] private bool expanded = true;
    #endregion

    #endregion

    #region Properties
    public Button HeaderButton
    {
        get
        {
            return headerButton;
        }
    }
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Registers the authored header button callback.
    /// </summary>
    private void OnEnable()
    {
        if (headerButton != null)
            headerButton.onClick.AddListener(Toggle);

        ApplyState();
    }

    /// <summary>
    /// Removes the authored header button callback.
    /// </summary>
    private void OnDisable()
    {
        if (headerButton != null)
            headerButton.onClick.RemoveListener(Toggle);
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Sets the section expansion state from prefab setup or menu reset code.
    /// </summary>
    /// <param name="isExpanded">True to show the content root, false to hide it.</param>
    public void SetExpanded(bool isExpanded)
    {
        expanded = isExpanded;
        ApplyState();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Toggles the section expansion state after a header click.
    /// </summary>
    private void Toggle()
    {
        expanded = !expanded;
        ApplyState();
    }

    /// <summary>
    /// Applies the current state to the authored content root and optional arrow label.
    /// </summary>
    private void ApplyState()
    {
        if (contentRoot != null)
            contentRoot.SetActive(expanded);

        if (arrowLabel != null)
            arrowLabel.text = expanded ? "v" : ">";
    }
    #endregion

    #endregion
}
