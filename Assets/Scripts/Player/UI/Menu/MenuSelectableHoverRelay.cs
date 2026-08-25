using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Preserves pointer-to-navigation focus synchronization and applies the baked interaction profile for one authored menu button.
/// </summary>
[RequireComponent(typeof(Selectable))]
[DisallowMultipleComponent]
public sealed class MenuSelectableHoverRelay : MonoBehaviour,
                                               IPointerEnterHandler,
                                               IPointerExitHandler,
                                               IPointerDownHandler,
                                               IPointerUpHandler,
                                               ISelectHandler,
                                               IDeselectHandler
{
    #region Fields

    #region Serialized Fields
    [Header("Selection")]
    [Tooltip("Optional selection controller override used instead of the first parent MenuSelectionController.")]
    [SerializeField] private MenuSelectionController selectionControllerOverride;

    [Header("Interaction Profile")]
    [Tooltip("Explicit menu profile used by this preauthored button.")]
    [SerializeField] private GameUiMenuKind menuKind = GameUiMenuKind.MainMenu;

    [Tooltip("Optional target graphic override. The Selectable target graphic is used when this is empty.")]
    [SerializeField] private Graphic targetGraphicOverride;

    [Tooltip("Optional TMP label override. The first child TMP text is used when this is empty.")]
    [SerializeField] private TMP_Text targetTextOverride;

    [Tooltip("Optional child transform isolating position, rotation, scale, and clip feedback from the layout-driven button root. Manual transform feedback works without this override by using the current post-layout baseline.")]
    [SerializeField] private Transform transformTargetOverride;
    #endregion

    #region Runtime
    private Selectable selectable;
    private MenuSelectionController selectionController;
    private Graphic targetGraphic;
    private TMP_Text targetText;
    private Transform presentationTransform;
    private Sprite originalSprite;
    private Color originalGraphicColor;
    private TMP_FontAsset originalFont;
    private float originalFontSize;
    private FontStyles originalFontStyle;
    private Color originalTextColor;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalLocalScale;
    private GameUiMenuButtonInteractionElement interaction;
    private Coroutine transitionCoroutine;
    private GameUiButtonPresentationState currentState;
    private bool interactionResolved;
    private bool isHovered;
    private bool isPressed;
    private bool isSelected;
    private bool rootPositionModified;
    private bool stateInitialized;
    #endregion

    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Caches authored presentation baselines and resolves the shared menu selection controller.
    /// </summary>
    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        targetGraphic = targetGraphicOverride != null ? targetGraphicOverride : selectable.targetGraphic;
        targetText = targetTextOverride != null ? targetTextOverride : GetComponentInChildren<TMP_Text>(true);
        presentationTransform = transformTargetOverride != null ? transformTargetOverride : transform;
        CacheOriginalPresentation();
        ResolveSelectionController();
        TryResolveInteraction();
    }

    /// <summary>
    /// Re-resolves dependencies and applies current button state when the authored menu becomes visible.
    /// </summary>
    private void OnEnable()
    {
        ResolveSelectionController();
        TryResolveInteraction();
        ApplyCurrentState(false);
    }

    /// <summary>
    /// Performs one deferred ECS-config lookup after scene initialization has completed.
    /// </summary>
    private void Start()
    {
        if (!interactionResolved && TryResolveInteraction())
            ApplyCurrentState(false);
    }

    /// <summary>
    /// Releases hover ownership, stops transitions, and restores the authored baseline when disabled.
    /// </summary>
    private void OnDisable()
    {
        if (selectionController != null && selectable != null)
            selectionController.RegisterPointerExit(selectable);

        isHovered = false;
        isPressed = false;
        isSelected = false;
        StopTransition();

        if (transformTargetOverride == null &&
            (!stateInitialized || currentState == GameUiButtonPresentationState.Normal))
            rootPositionModified = false;

        RestoreOriginalPresentation();
        stateInitialized = false;
    }
    #endregion

    #region Event Methods
    /// <summary>
    /// Transfers active selection to this button and applies pointer-hover presentation.
    /// </summary>
    /// <param name="eventData">Pointer event reported by the Unity EventSystem.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        EnsureInteractionAndApply();

        if (selectionController != null && selectable != null)
            selectionController.RegisterPointerEnter(selectable);
    }

    /// <summary>
    /// Restores previous menu selection and removes pointer-hover presentation.
    /// </summary>
    /// <param name="eventData">Pointer event reported by the Unity EventSystem.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isPressed = false;
        EnsureInteractionAndApply();

        if (selectionController != null && selectable != null)
            selectionController.RegisterPointerExit(selectable);
    }

    /// <summary>
    /// Applies the pressed interaction state for a valid left-pointer submission.
    /// </summary>
    /// <param name="eventData">Pointer event reported by the Unity EventSystem.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        isPressed = true;
        EnsureInteractionAndApply();
    }

    /// <summary>
    /// Releases the pressed state and returns to hover, selected, or normal presentation.
    /// </summary>
    /// <param name="eventData">Pointer event reported by the Unity EventSystem.</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        isPressed = false;
        EnsureInteractionAndApply();
    }

    /// <summary>
    /// Applies the focused state used by keyboard and gamepad navigation.
    /// </summary>
    /// <param name="eventData">Selection event reported by the Unity EventSystem.</param>
    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
        EnsureInteractionAndApply();
    }

    /// <summary>
    /// Reports focus loss for selection recovery and updates the interaction presentation.
    /// </summary>
    /// <param name="eventData">Deselection event reported by the Unity EventSystem.</param>
    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        isPressed = false;
        EnsureInteractionAndApply();

        if (selectionController != null && selectable != null)
            selectionController.RegisterSelectableDeselected(selectable);
    }
    #endregion

    #region Interaction Config
    /// <summary>
    /// Resolves the menu kind and retrieves its baked ECS interaction profile once.
    /// </summary>
    /// <returns>True when a matching enabled or disabled profile was found.</returns>
    private bool TryResolveInteraction()
    {
        if (interactionResolved)
            return true;

        if (!GameMenuButtonInteractionRuntimeUtility.TryResolve(menuKind, out interaction))
            return false;

        interactionResolved = true;
        return true;
    }

    /// <summary>
    /// Retries unresolved ECS config on actual interaction and applies the highest-priority current state.
    /// </summary>
    private void EnsureInteractionAndApply()
    {
        TryResolveInteraction();
        ApplyCurrentState(true);
    }

    /// <summary>
    /// Resolves disabled, pressed, hover, selected, or normal presentation in priority order.
    /// </summary>
    /// <param name="animate">True to interpolate motion and sample clips over their transition duration.</param>
    private void ApplyCurrentState(bool animate)
    {
        if (!interactionResolved || interaction.Enabled == 0)
        {
            RestoreOriginalPresentation();
            return;
        }

        GameUiButtonPresentationState state;

        if (selectable == null || !selectable.IsInteractable())
            state = GameUiButtonPresentationState.Disabled;
        else if (isPressed)
            state = GameUiButtonPresentationState.Pressed;
        else if (isHovered)
            state = GameUiButtonPresentationState.Hovered;
        else if (isSelected)
            state = GameUiButtonPresentationState.Selected;
        else
            state = GameUiButtonPresentationState.Normal;

        ApplyState(state, animate);
    }

    /// <summary>
    /// Applies sprite and text state immediately, then schedules optional transform and clip interpolation.
    /// </summary>
    /// <param name="state">Resolved presentation state.</param>
    /// <param name="animate">True to use the authored transition duration.</param>
    private void ApplyState(GameUiButtonPresentationState state, bool animate)
    {
        PreparePositionBaseline(state);
        currentState = state;
        stateInitialized = true;
        ApplySpriteAndGraphic(state);
        ApplyTextStyle(state);
        StopTransition();
        float durationSeconds = animate ? interaction.TransitionDurationSeconds : 0f;
        bool usesManualMotion = GameMenuButtonPresentationUtility.UsesManualMotion(in interaction);
        bool usesClip = GameMenuButtonPresentationUtility.UsesClips(in interaction);
        Vector3 targetPosition = GameMenuButtonPresentationUtility.ResolvePosition(in interaction,
                                                                                  state,
                                                                                  originalLocalPosition);
        Quaternion targetRotation = GameMenuButtonPresentationUtility.ResolveRotation(in interaction,
                                                                                      state,
                                                                                      originalLocalRotation);
        Vector3 targetScale = GameMenuButtonPresentationUtility.ResolveScale(in interaction,
                                                                            state,
                                                                            originalLocalScale);
        AnimationClip targetClip = usesClip
            ? GameMenuButtonPresentationUtility.ResolveClip(in interaction, state)
            : null;

        if ((state == GameUiButtonPresentationState.Hovered ||
             state == GameUiButtonPresentationState.Selected) &&
            usesManualMotion &&
            interaction.HoverTransformMode == GameUiButtonHoverTransformMode.Pulse)
        {
            transitionCoroutine = StartCoroutine(PulsePresentation(targetPosition,
                                                                   targetRotation,
                                                                   targetScale,
                                                                   targetClip));
            return;
        }

        if (durationSeconds <= 0f)
        {
            if (targetClip != null)
                SampleClip(targetClip, targetClip.length);

            if (usesManualMotion)
                ApplyTransform(targetPosition, targetRotation, targetScale);

            return;
        }

        transitionCoroutine = StartCoroutine(TransitionPresentation(presentationTransform.localPosition,
                                                                    presentationTransform.localRotation,
                                                                    presentationTransform.localScale,
                                                                    targetPosition,
                                                                    targetRotation,
                                                                    targetScale,
                                                                    usesManualMotion,
                                                                    targetClip,
                                                                    durationSeconds));
    }
    #endregion

    #region Presentation
    /// <summary>
    /// Interpolates optional transform motion and animation-clip sampling without an Animator dependency.
    /// </summary>
    /// <param name="startPosition">Local position at transition start.</param>
    /// <param name="startRotation">Local rotation at transition start.</param>
    /// <param name="startScale">Local scale at transition start.</param>
    /// <param name="targetPosition">Target local position.</param>
    /// <param name="targetRotation">Target local rotation.</param>
    /// <param name="targetScale">Target local scale.</param>
    /// <param name="usesManualMotion">True when transform interpolation is enabled.</param>
    /// <param name="clip">Optional authored clip sampled across the transition.</param>
    /// <param name="durationSeconds">Transition duration in seconds.</param>
    /// <returns>Coroutine enumerator scheduled by Unity.</returns>
    private IEnumerator TransitionPresentation(Vector3 startPosition,
                                               Quaternion startRotation,
                                               Vector3 startScale,
                                               Vector3 targetPosition,
                                               Quaternion targetRotation,
                                               Vector3 targetScale,
                                               bool usesManualMotion,
                                               AnimationClip clip,
                                               float durationSeconds)
    {
        float elapsedSeconds = 0f;

        while (elapsedSeconds < durationSeconds)
        {
            elapsedSeconds += interaction.UseUnscaledTime != 0 ? Time.unscaledDeltaTime : Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedSeconds / durationSeconds);
            float easedTime = normalizedTime * normalizedTime * (3f - 2f * normalizedTime);

            if (clip != null)
                SampleClip(clip, clip.length * normalizedTime);

            if (usesManualMotion)
            {
                ApplyTransform(Vector3.LerpUnclamped(startPosition, targetPosition, easedTime),
                               Quaternion.SlerpUnclamped(startRotation, targetRotation, easedTime),
                               Vector3.LerpUnclamped(startScale, targetScale, easedTime));
            }

            yield return null;
        }

        if (clip != null)
            SampleClip(clip, clip.length);

        if (usesManualMotion)
            ApplyTransform(targetPosition, targetRotation, targetScale);

        transitionCoroutine = null;
    }

    /// <summary>
    /// Plays complete baseline-to-peak-to-baseline transform cycles while optionally sampling the hover clip.
    /// </summary>
    /// <param name="peakPosition">Peak local position reached at the middle of each cycle.</param>
    /// <param name="peakRotation">Peak local rotation reached at the middle of each cycle.</param>
    /// <param name="peakScale">Peak local scale reached at the middle of each cycle.</param>
    /// <param name="clip">Optional hover clip sampled once per pulse cycle.</param>
    /// <returns>Coroutine enumerator scheduled by Unity.</returns>
    private IEnumerator PulsePresentation(Vector3 peakPosition,
                                          Quaternion peakRotation,
                                          Vector3 peakScale,
                                          AnimationClip clip)
    {
        float cycleDuration = Mathf.Max(0.02f, interaction.HoverPulseCycleSeconds);
        int completedCycles = 0;

        while (interaction.LoopHoverPulse != 0 || completedCycles < Mathf.Max(1, interaction.HoverPulseCycles))
        {
            float elapsedSeconds = 0f;

            while (elapsedSeconds < cycleDuration)
            {
                elapsedSeconds += interaction.UseUnscaledTime != 0 ? Time.unscaledDeltaTime : Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsedSeconds / cycleDuration);
                float pulseWeight = 0.5f - 0.5f * Mathf.Cos(normalizedTime * Mathf.PI * 2f);

                if (clip != null)
                    SampleClip(clip, clip.length * normalizedTime);

                ApplyTransform(Vector3.LerpUnclamped(originalLocalPosition, peakPosition, pulseWeight),
                               Quaternion.SlerpUnclamped(originalLocalRotation, peakRotation, pulseWeight),
                               Vector3.LerpUnclamped(originalLocalScale, peakScale, pulseWeight));
                yield return null;
            }

            completedCycles += 1;
        }

        ApplyTransform(originalLocalPosition, originalLocalRotation, originalLocalScale);
        transitionCoroutine = null;
    }

    /// <summary>
    /// Applies the state sprite and target-graphic tint when those overrides are enabled.
    /// </summary>
    /// <param name="state">Current button presentation state.</param>
    private void ApplySpriteAndGraphic(GameUiButtonPresentationState state)
    {
        if (targetGraphic == null)
            return;

        if (interaction.OverrideGraphicColors != 0)
            targetGraphic.color = GameMenuButtonPresentationUtility.ResolveGraphicColor(in interaction, state);
        else
            targetGraphic.color = originalGraphicColor;

        Image image = targetGraphic as Image;

        if (image == null)
            return;

        if (interaction.OverrideSprites == 0)
        {
            image.sprite = originalSprite;
            return;
        }

        Sprite stateSprite = GameMenuButtonPresentationUtility.ResolveSprite(in interaction, state);
        image.sprite = stateSprite != null ? stateSprite : originalSprite;
    }

    /// <summary>
    /// Applies normal, emphasized, or disabled TMP text style from the current menu profile.
    /// </summary>
    /// <param name="state">Current button presentation state.</param>
    private void ApplyTextStyle(GameUiButtonPresentationState state)
    {
        if (targetText == null)
            return;

        if (interaction.OverrideTextStyle == 0)
        {
            RestoreOriginalTextStyle();
            return;
        }

        bool emphasized = state == GameUiButtonPresentationState.Hovered ||
                          state == GameUiButtonPresentationState.Selected ||
                          state == GameUiButtonPresentationState.Pressed;
        TMP_FontAsset font = emphasized ? interaction.EmphasizedFont.Value : interaction.NormalFont.Value;

        if (font != null)
            targetText.font = font;
        else if (originalFont != null)
            targetText.font = originalFont;

        targetText.fontSize = emphasized ? interaction.EmphasizedFontSize : interaction.NormalFontSize;
        targetText.fontStyle = (FontStyles)(emphasized ? interaction.EmphasizedFontStyle : interaction.NormalFontStyle);
        targetText.color = GameMenuButtonPresentationUtility.ResolveTextColor(in interaction, state);
    }

    /// <summary>
    /// Applies one local transform state without writing layout-owned root position.
    /// </summary>
    /// <param name="position">Target local position.</param>
    /// <param name="rotation">Target local rotation.</param>
    /// <param name="scale">Target local scale.</param>
    private void ApplyTransform(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (presentationTransform == null)
            return;

        if (transformTargetOverride != null || rootPositionModified)
            presentationTransform.localPosition = position;

        presentationTransform.localRotation = rotation;
        presentationTransform.localScale = scale;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Caches authored baseline values once before preset overrides are applied.
    /// </summary>
    private void CacheOriginalPresentation()
    {
        originalLocalPosition = presentationTransform.localPosition;
        originalLocalRotation = presentationTransform.localRotation;
        originalLocalScale = presentationTransform.localScale;
        originalGraphicColor = targetGraphic != null ? targetGraphic.color : Color.white;
        Image image = targetGraphic as Image;
        originalSprite = image != null ? image.sprite : null;

        if (targetText == null)
            return;

        originalFont = targetText.font;
        originalFontSize = targetText.fontSize;
        originalFontStyle = targetText.fontStyle;
        originalTextColor = targetText.color;
    }

    /// <summary>
    /// Captures the current layout-owned root position immediately before the first non-normal transform state.
    /// </summary>
    /// <param name="targetState">Presentation state about to be applied.</param>
    private void PreparePositionBaseline(GameUiButtonPresentationState targetState)
    {
        if (transformTargetOverride != null || targetState == GameUiButtonPresentationState.Normal)
            return;

        if (!stateInitialized || currentState == GameUiButtonPresentationState.Normal)
        {
            originalLocalPosition = presentationTransform.localPosition;
            rootPositionModified = true;
        }
    }

    /// <summary>
    /// Restores authored transform, sprite, graphic, and label values when no enabled profile applies.
    /// </summary>
    private void RestoreOriginalPresentation()
    {
        ApplyTransform(originalLocalPosition, originalLocalRotation, originalLocalScale);
        rootPositionModified = false;
        currentState = GameUiButtonPresentationState.Normal;
        stateInitialized = true;

        if (targetGraphic != null)
            targetGraphic.color = originalGraphicColor;

        Image image = targetGraphic as Image;

        if (image != null)
            image.sprite = originalSprite;

        RestoreOriginalTextStyle();
    }

    /// <summary>
    /// Restores authored TMP font, size, style, and color values.
    /// </summary>
    private void RestoreOriginalTextStyle()
    {
        if (targetText == null)
            return;

        if (originalFont != null)
            targetText.font = originalFont;

        targetText.fontSize = originalFontSize;
        targetText.fontStyle = originalFontStyle;
        targetText.color = originalTextColor;
    }

    /// <summary>
    /// Stops one active transform or clip transition before applying a new state.
    /// </summary>
    private void StopTransition()
    {
        if (transitionCoroutine == null)
            return;

        StopCoroutine(transitionCoroutine);
        transitionCoroutine = null;
    }

    /// <summary>
    /// Resolves the shared menu selection controller used by this button.
    /// </summary>
    private void ResolveSelectionController()
    {
        selectionController = selectionControllerOverride != null
            ? selectionControllerOverride
            : GetComponentInParent<MenuSelectionController>(true);
    }

    /// <summary>
    /// Samples one authored clip while protecting the layout-owned button-root position.
    /// </summary>
    /// <param name="clip">Clip sampled on the configured presentation object.</param>
    /// <param name="timeSeconds">Clip-local sample time.</param>
    private void SampleClip(AnimationClip clip, float timeSeconds)
    {
        if (clip == null || presentationTransform == null)
            return;

        Vector3 layoutPosition = presentationTransform.localPosition;
        clip.SampleAnimation(presentationTransform.gameObject, timeSeconds);

        if (transformTargetOverride == null)
            presentationTransform.localPosition = layoutPosition;
    }
    #endregion

    #endregion
}

/// <summary>
/// Internal priority states used by menu-button presentation.
/// </summary>
internal enum GameUiButtonPresentationState : byte
{
    Normal = 0,
    Hovered = 1,
    Selected = 2,
    Pressed = 3,
    Disabled = 4
}
