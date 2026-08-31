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
    #region Constants
    private const int InteractionResolutionMaxFrames = 300;
    #endregion

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

    [Tooltip("Stable ID matched to the button image configured in the HUD preset. Project setup uses this GameObject name by default.")]
    [SerializeField]
    private string buttonContentId;

    [Tooltip("Preauthored image-content target used when the active menu profile selects Image content.")]
    [SerializeField]
    private Image targetImageOverride;

    [Tooltip("Optional whole-button motion target used instead of the layout-driven button root. Content Only motion ignores this override and uses the active text or image content.")]
    [SerializeField] private Transform transformTargetOverride;
    #endregion

    #region Runtime
    private Selectable selectable;
    private MenuSelectionController selectionController;
    private Graphic targetGraphic;
    private TMP_Text targetText;
    private Image targetImage;
    private Transform presentationTransform;
    private Sprite originalSprite;
    private Color originalGraphicColor;
    private TMP_FontAsset originalFont;
    private float originalFontSize;
    private FontStyles originalFontStyle;
    private Color originalTextColor;
    private Sprite originalImageSprite;
    private Color originalImageColor;
    private bool originalImagePreserveAspect;
    private bool originalImageEnabled;
    private bool originalTextEnabled;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalLocalScale;
    private GameUiMenuButtonInteractionElement interaction;
    private GameUiButtonImageContentElement imageContent;
    private Coroutine interactionResolutionCoroutine;
    private Coroutine transitionCoroutine;
    private GameUiButtonPresentationState currentState;
    private bool interactionResolved;
    private bool usesImageContent;
    private bool isHovered;
    private bool isPressed;
    private bool isSelected;
    private bool protectLayoutPosition;
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
        targetImage = targetImageOverride != null ? targetImageOverride : FindImageContentTarget();
        presentationTransform = transformTargetOverride != null ? transformTargetOverride : transform;
        protectLayoutPosition = transformTargetOverride == null;
        CacheOriginalPresentation();
        ResolveSelectionController();
    }

    /// <summary>
    /// Re-resolves dependencies and applies current button state when the authored menu becomes visible.
    /// </summary>
    private void OnEnable()
    {
        ResolveSelectionController();

        if (TryResolveInteraction())
            ApplyCurrentState(false);
        else
            BeginInteractionResolution();
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
        StopInteractionResolution();
        StopTransition();

        if (protectLayoutPosition &&
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

        usesImageContent = interaction.ContentMode == GameUiButtonContentMode.Image &&
                           targetImage != null &&
                           GameMenuButtonInteractionRuntimeUtility.TryResolveImageContent(menuKind,
                                                                                          buttonContentId,
                                                                                          out imageContent) &&
                           imageContent.NormalSprite.Value != null;
        GameMenuButtonContentPresentationUtility.ApplyVisibility(targetText, targetImage, usesImageContent);
        ConfigureMotionTarget(interaction.MotionTarget);
        interactionResolved = true;
        return true;
    }

    /// <summary>
    /// Retries unresolved ECS config on actual interaction and applies the highest-priority current state.
    /// </summary>
    private void EnsureInteractionAndApply()
    {
        if (!TryResolveInteraction())
            BeginInteractionResolution();

        ApplyCurrentState(true);
    }

    /// <summary>
    /// Starts one transient initialization retry while the DOTS HUD singleton is still being created.
    /// </summary>
    private void BeginInteractionResolution()
    {
        if (interactionResolved || interactionResolutionCoroutine != null || !isActiveAndEnabled)
            return;

        interactionResolutionCoroutine = StartCoroutine(ResolveInteractionWhenReady());
    }

    /// <summary>
    /// Retries the shared ECS lookup only during startup and applies the idle state as soon as it succeeds.
    /// </summary>
    /// <returns>Coroutine enumerator that completes immediately after the HUD interaction buffer becomes available.</returns>
    private IEnumerator ResolveInteractionWhenReady()
    {
        int attemptedFrames = 0;

        while (isActiveAndEnabled &&
               !interactionResolved &&
               attemptedFrames < InteractionResolutionMaxFrames)
        {
            if (TryResolveInteraction())
            {
                ApplyCurrentState(false);
                break;
            }

            attemptedFrames++;
            yield return null;
        }

        interactionResolutionCoroutine = null;
    }

    /// <summary>
    /// Stops the transient ECS initialization retry when the authored button hierarchy is hidden.
    /// </summary>
    private void StopInteractionResolution()
    {
        if (interactionResolutionCoroutine == null)
            return;

        StopCoroutine(interactionResolutionCoroutine);
        interactionResolutionCoroutine = null;
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

        GameMenuButtonContentPresentationUtility.ApplyVisibility(targetText, targetImage, usesImageContent);

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
        GameMenuButtonContentPresentationUtility.ApplyTargetGraphic(targetGraphic,
                                                                    originalGraphicColor,
                                                                    originalSprite,
                                                                    in interaction,
                                                                    state);
        GameMenuButtonContentPresentationUtility.ApplyText(targetText,
                                                           usesImageContent,
                                                           originalFont,
                                                           originalFontSize,
                                                           originalFontStyle,
                                                           originalTextColor,
                                                           originalTextEnabled,
                                                           in interaction,
                                                           state);
        GameMenuButtonContentPresentationUtility.ApplyImage(targetImage,
                                                            usesImageContent,
                                                            in imageContent,
                                                            state);
        StopTransition();

        // Resolve motion independently from sprite, graphic-color, and text-style state changes.
        bool hasMotionTarget = presentationTransform != null;
        bool usesManualMotion = hasMotionTarget && GameMenuButtonPresentationUtility.UsesManualMotion(in interaction);
        bool usesClip = hasMotionTarget && GameMenuButtonPresentationUtility.UsesClips(in interaction);
        AnimationClip targetClip = usesClip
            ? GameMenuButtonPresentationUtility.ResolveClip(in interaction, state)
            : null;

        if (!usesManualMotion && targetClip == null)
            return;

        float durationSeconds = animate ? interaction.TransitionDurationSeconds : 0f;
        Vector3 targetPosition = GameMenuButtonPresentationUtility.ResolvePosition(in interaction,
                                                                                  state,
                                                                                  originalLocalPosition);
        Quaternion targetRotation = GameMenuButtonPresentationUtility.ResolveRotation(in interaction,
                                                                                      state,
                                                                                      originalLocalRotation);
        Vector3 targetScale = GameMenuButtonPresentationUtility.ResolveScale(in interaction,
                                                                            state,
                                                                            originalLocalScale);

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
    /// Applies one local transform state without writing layout-owned root position.
    /// </summary>
    /// <param name="position">Target local position.</param>
    /// <param name="rotation">Target local rotation.</param>
    /// <param name="scale">Target local scale.</param>
    private void ApplyTransform(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (presentationTransform == null)
            return;

        if (!protectLayoutPosition || rootPositionModified)
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
        CacheOriginalTransform();
        originalGraphicColor = targetGraphic != null ? targetGraphic.color : Color.white;
        Image image = targetGraphic as Image;
        originalSprite = image != null ? image.sprite : null;
        originalImageSprite = targetImage != null ? targetImage.sprite : null;
        originalImageColor = targetImage != null ? targetImage.color : Color.white;
        originalImagePreserveAspect = targetImage != null && targetImage.preserveAspect;
        originalImageEnabled = targetImage != null && targetImage.enabled;
        originalTextEnabled = targetText != null && targetText.enabled;

        if (targetText == null)
            return;

        originalFont = targetText.font;
        originalFontSize = targetText.fontSize;
        originalFontStyle = targetText.fontStyle;
        originalTextColor = targetText.color;
    }

    /// <summary>
    /// Caches the authored transform baseline after the baked motion target has been resolved.
    /// </summary>
    private void CacheOriginalTransform()
    {
        if (presentationTransform == null)
            return;

        originalLocalPosition = presentationTransform.localPosition;
        originalLocalRotation = presentationTransform.localRotation;
        originalLocalScale = presentationTransform.localScale;
    }

    /// <summary>
    /// Selects the whole-button or active content motion target once when the ECS profile becomes available.
    /// </summary>
    /// <param name="motionTarget">Baked target policy selected by the active menu profile.</param>
    private void ConfigureMotionTarget(GameUiButtonMotionTarget motionTarget)
    {
        Transform resolvedTarget;
        bool resolvedLayoutProtection;

        switch (motionTarget)
        {
            case GameUiButtonMotionTarget.ContentOnly:
                resolvedTarget = usesImageContent
                    ? targetImage.transform
                    : targetText != null ? targetText.transform : null;
                resolvedLayoutProtection = false;
                break;
            default:
                resolvedTarget = transformTargetOverride != null ? transformTargetOverride : transform;
                resolvedLayoutProtection = transformTargetOverride == null;
                break;
        }

        protectLayoutPosition = resolvedLayoutProtection;

        if (presentationTransform == resolvedTarget)
            return;

        presentationTransform = resolvedTarget;
        rootPositionModified = false;
        CacheOriginalTransform();
    }

    /// <summary>
    /// Captures the current layout-owned root position immediately before the first non-normal transform state.
    /// </summary>
    /// <param name="targetState">Presentation state about to be applied.</param>
    private void PreparePositionBaseline(GameUiButtonPresentationState targetState)
    {
        if (presentationTransform == null ||
            !protectLayoutPosition ||
            targetState == GameUiButtonPresentationState.Normal)
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

        GameMenuButtonContentPresentationUtility.RestoreImage(targetImage,
                                                              originalImageSprite,
                                                              originalImageColor,
                                                              originalImagePreserveAspect,
                                                              originalImageEnabled);
        GameMenuButtonContentPresentationUtility.RestoreText(targetText,
                                                             originalFont,
                                                             originalFontSize,
                                                             originalFontStyle,
                                                             originalTextColor,
                                                             originalTextEnabled);
    }

    /// <summary>
    /// Finds the preauthored image-content child while excluding the selectable target graphic.
    /// </summary>
    /// <returns>First eligible child Image, or null when project setup has not authored one.</returns>
    private Image FindImageContentTarget()
    {
        Image[] images = GetComponentsInChildren<Image>(true);

        for (int imageIndex = 0; imageIndex < images.Length; imageIndex++)
        {
            if (images[imageIndex] != targetGraphic && images[imageIndex].name == "ImageContent")
                return images[imageIndex];
        }

        return null;
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

        if (protectLayoutPosition)
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
