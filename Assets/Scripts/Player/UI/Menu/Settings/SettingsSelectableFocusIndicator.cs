using TMPro;
using Unity.Mathematics;
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

    #region Runtime
    private TMP_Text[] targetTexts;
    private Transform presentationTransform;
    private Vector3 authoredScale;
    private GameHudSettingsNavigationRuntimeConfig runtimeConfig;
    private bool referencesResolved;
    private bool focused;
    private bool useRuntimePresentation;
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
        focused = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        ApplyFocusedState(focused);
    }

    /// <summary>
    /// Clears the highlight when the selectable is disabled while focused.
    /// </summary>
    private void OnDisable()
    {
        focused = false;
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
        focused = true;
        ApplyFocusedState(true);
    }

    /// <summary>
    /// Disables the authored focus highlight.
    /// </summary>
    /// <param name="eventData">Deselection event data from the active EventSystem.</param>
    public void OnDeselect(BaseEventData eventData)
    {
        focused = false;
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
        referencesResolved = false;
        ResolveReferences();
        ApplyFocusedState(false);
    }

    /// <summary>
    /// Applies the baked HUD selection presentation while retaining the prefab-authored fallback when customization is disabled.
    /// </summary>
    /// <param name="config">Baked Settings navigation and selection presentation config.</param>
    public void Configure(in GameHudSettingsNavigationRuntimeConfig config)
    {
        runtimeConfig = config;
        useRuntimePresentation = config.CustomizeSelectionPresentation != 0;
        ResolveReferences();
        ApplyFocusedState(focused);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves missing optional references from the local selectable.
    /// </summary>
    private void ResolveReferences()
    {
        if (referencesResolved)
            return;

        if (targetGraphic == null)
        {
            Selectable selectable = GetComponent<Selectable>();

            if (selectable != null)
                targetGraphic = selectable.targetGraphic;
        }

        if (targetGraphic != null)
        {
            if (focusOutline == null)
                focusOutline = targetGraphic.GetComponent<Outline>();

            targetTexts = targetGraphic.GetComponentsInChildren<TMP_Text>(true);
            presentationTransform = targetGraphic.transform;
            authoredScale = presentationTransform.localScale;
        }

        referencesResolved = true;
    }

    /// <summary>
    /// Applies the requested highlight state without allocating or polling each frame.
    /// </summary>
    /// <param name="focused">True when this selectable owns focus.</param>
    private void ApplyFocusedState(bool focused)
    {
        if (targetGraphic != null && ResolveGraphicTintEnabled())
            targetGraphic.color = focused ? ResolveSelectedGraphicColor() : ResolveUnselectedGraphicColor();

        ApplyTextState(focused);
        ApplyScaleState(focused);

        if (focusOutline == null)
            return;

        focusOutline.effectColor = useRuntimePresentation
            ? ToColor(runtimeConfig.SelectionOutlineColor)
            : outlineColor;

        if (useRuntimePresentation)
            focusOutline.effectDistance = ToVector2(runtimeConfig.SelectionOutlineDistance);

        focusOutline.enabled = focused && (!useRuntimePresentation || runtimeConfig.ShowSelectionOutline != 0);
    }

    /// <summary>
    /// Applies configurable text color and style to all labels contained by the highlighted option row.
    /// </summary>
    /// <param name="selected">True when the option owns navigation focus.</param>
    private void ApplyTextState(bool selected)
    {
        if (!useRuntimePresentation || runtimeConfig.OverrideSelectionTextStyle == 0 || targetTexts == null)
            return;

        Color color = selected ? ToColor(runtimeConfig.SelectedTextColor) : ToColor(runtimeConfig.UnselectedTextColor);
        FontStyles style = (FontStyles)(selected ? runtimeConfig.SelectedFontStyle : runtimeConfig.UnselectedFontStyle);

        for (int textIndex = 0; textIndex < targetTexts.Length; textIndex++)
        {
            TMP_Text text = targetTexts[textIndex];

            if (text == null)
                continue;

            text.color = color;
            text.fontStyle = style;
        }
    }

    /// <summary>
    /// Applies configurable row scale while preserving the preauthored scale when runtime scale overrides are disabled.
    /// </summary>
    /// <param name="selected">True when the option owns navigation focus.</param>
    private void ApplyScaleState(bool selected)
    {
        if (presentationTransform == null)
            return;

        if (!useRuntimePresentation || runtimeConfig.OverrideSelectionScale == 0)
        {
            presentationTransform.localScale = authoredScale;
            return;
        }

        presentationTransform.localScale = selected
            ? ToVector3(runtimeConfig.SelectedScale)
            : ToVector3(runtimeConfig.UnselectedScale);
    }

    /// <summary>
    /// Resolves whether the current authored or runtime presentation may tint the target graphic.
    /// </summary>
    /// <returns>True when graphic tinting is enabled.</returns>
    private bool ResolveGraphicTintEnabled()
    {
        return useRuntimePresentation
            ? runtimeConfig.OverrideSelectionGraphicColors != 0
            : tintGraphic;
    }

    /// <summary>
    /// Resolves the unselected target-graphic color from runtime customization or prefab fallback values.
    /// </summary>
    /// <returns>Color applied while this option does not own focus.</returns>
    private Color ResolveUnselectedGraphicColor()
    {
        return useRuntimePresentation ? ToColor(runtimeConfig.UnselectedGraphicColor) : normalColor;
    }

    /// <summary>
    /// Resolves the selected target-graphic color from runtime customization or prefab fallback values.
    /// </summary>
    /// <returns>Color applied while this option owns focus.</returns>
    private Color ResolveSelectedGraphicColor()
    {
        return useRuntimePresentation ? ToColor(runtimeConfig.SelectedGraphicColor) : focusedColor;
    }

    /// <summary>
    /// Converts an ECS color to its Unity UI representation.
    /// </summary>
    /// <param name="value">Baked RGBA value.</param>
    /// <returns>Matching Unity color.</returns>
    private static Color ToColor(float4 value)
    {
        return new Color(value.x, value.y, value.z, value.w);
    }

    /// <summary>
    /// Converts an ECS two-dimensional value to a Unity vector.
    /// </summary>
    /// <param name="value">Baked vector value.</param>
    /// <returns>Matching Unity vector.</returns>
    private static Vector2 ToVector2(float2 value)
    {
        return new Vector2(value.x, value.y);
    }

    /// <summary>
    /// Converts an ECS three-dimensional value to a Unity vector.
    /// </summary>
    /// <param name="value">Baked vector value.</param>
    /// <returns>Matching Unity vector.</returns>
    private static Vector3 ToVector3(float3 value)
    {
        return new Vector3(value.x, value.y, value.z);
    }
    #endregion

    #endregion
}
