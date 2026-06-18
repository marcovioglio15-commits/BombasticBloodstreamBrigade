using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Highlights an authored Settings menu selectable while it owns keyboard or controller focus.
/// </summary>
[DisallowMultipleComponent]
public sealed class SettingsSelectableFocusIndicator : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    #region Fields

    #region Serialized Fields
    [Header("Target")]
    [Tooltip("Graphic that receives the focus tint. Leave empty to use the selectable target graphic.")]
    [SerializeField] private Graphic targetGraphic;

    [Tooltip("Outline enabled while this setting is focused by keyboard or controller navigation.")]
    [SerializeField] private Outline focusOutline;

    [Tooltip("When enabled, focus changes also tint the target graphic. Disable for controls that manage their own selected color.")]
    [SerializeField] private bool tintGraphic = true;

    [Header("Colors")]
    [Tooltip("Graphic color used when this setting does not own focus.")]
    [SerializeField] private Color normalColor = new Color(0.09f, 0.13f, 0.16f, 1f);

    [Tooltip("Graphic color used while this setting owns direct controller or keyboard focus.")]
    [SerializeField] private Color focusedColor = new Color(0.18f, 0.31f, 0.39f, 1f);

    [Tooltip("Outline color used while this setting owns direct controller or keyboard focus.")]
    [SerializeField] private Color outlineColor = new Color(0.98f, 0.78f, 0.15f, 1f);
    #endregion

    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Resolves optional references once and applies the unfocused state.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        ApplyFocusedState(false);
    }

    /// <summary>
    /// Reapplies the unfocused state when pooled or prefab-instantiated menu controls become active.
    /// </summary>
    private void OnEnable()
    {
        ResolveReferences();
        ApplyFocusedState(false);
    }

    /// <summary>
    /// Clears the highlight when the selectable is disabled while focused.
    /// </summary>
    private void OnDisable()
    {
        ApplyFocusedState(false);
    }
    #endregion

    #region Event Methods
    /// <summary>
    /// Enables the authored focus highlight.
    /// </summary>
    /// <param name="eventData">Selection event data from the active EventSystem.</param>
    public void OnSelect(BaseEventData eventData)
    {
        ApplyFocusedState(true);
    }

    /// <summary>
    /// Disables the authored focus highlight.
    /// </summary>
    /// <param name="eventData">Deselection event data from the active EventSystem.</param>
    public void OnDeselect(BaseEventData eventData)
    {
        ApplyFocusedState(false);
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Configures the highlight target and colors from the editor prefab setup path.
    /// </summary>
    /// <param name="graphic">Graphic that receives the focus color.</param>
    /// <param name="outline">Outline enabled only while focused.</param>
    /// <param name="normalColor">Unfocused graphic color.</param>
    /// <param name="focusedColor">Focused graphic color.</param>
    /// <param name="outlineColor">Focused outline color.</param>
    /// <param name="tintGraphic">True when focus should tint the target graphic in addition to enabling the outline.</param>
    public void Configure(Graphic graphic,
                          Outline outline,
                          Color normalColor,
                          Color focusedColor,
                          Color outlineColor,
                          bool tintGraphic)
    {
        targetGraphic = graphic;
        focusOutline = outline;
        this.normalColor = normalColor;
        this.focusedColor = focusedColor;
        this.outlineColor = outlineColor;
        this.tintGraphic = tintGraphic;
        ApplyFocusedState(false);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves missing optional references from the local selectable.
    /// </summary>
    private void ResolveReferences()
    {
        if (targetGraphic != null)
            return;

        Selectable selectable = GetComponent<Selectable>();

        if (selectable != null)
            targetGraphic = selectable.targetGraphic;
    }

    /// <summary>
    /// Applies the requested highlight state without allocating or polling each frame.
    /// </summary>
    /// <param name="focused">True when this selectable owns focus.</param>
    private void ApplyFocusedState(bool focused)
    {
        if (targetGraphic != null && tintGraphic)
            targetGraphic.color = focused ? focusedColor : normalColor;

        if (focusOutline == null)
            return;

        focusOutline.effectColor = outlineColor;
        focusOutline.enabled = focused;
    }
    #endregion

    #endregion
}
