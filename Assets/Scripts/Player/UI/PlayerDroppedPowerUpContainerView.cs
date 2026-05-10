using System.Collections;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the world-space presentation for one dropped active power-up container companion object.
/// none.
/// returns none.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerDroppedPowerUpContainerView : MonoBehaviour
{
    #region Constants
    private const float CameraResolveRetryIntervalSeconds = 0.5f;
    private const float BillboardMagnitudeEpsilon = 0.0001f;
    private const float PromptHiddenScaleMultiplier = 0.78f;
    private const float PromptOvershootScaleMultiplier = 1.06f;
    private const float PromptVisibleScaleMultiplier = 1f;
    private const float PromptShowRiseDurationSeconds = 0.14f;
    private const float PromptShowSettleDurationSeconds = 0.08f;
    private const float PromptHideDurationSeconds = 0.12f;
    private const int BillboardSortingOrder = 40;
    private static readonly Vector2 PromptHiddenAnchoredPosition = new Vector2(0f, -18f);
    private static readonly Vector2 PromptOvershootAnchoredPosition = new Vector2(0f, 126f);
    private static readonly Vector2 PromptVisibleAnchoredPosition = new Vector2(0f, 112f);
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("References")]
    [Tooltip("Billboard root that rotates toward the active camera while keeping the sphere mesh upright.")]
    [SerializeField] private Transform billboardRoot;

    [Tooltip("World-space canvas used to render the dropped power-up icon and interaction prompts.")]
    [SerializeField] private Canvas billboardCanvas;

    [Tooltip("World-space icon image rendered at the center of the dropped power-up container.")]
    [SerializeField] private Image iconImage;

    [Tooltip("Prompt root used by Overlay Panel mode before the interaction key is pressed.")]
    [SerializeField] private GameObject singlePromptRoot;

    [Tooltip("Text label shown by Overlay Panel mode to display the interaction binding.")]
    [SerializeField] private TMP_Text singlePromptText;

    [Tooltip("Legacy animator reference kept only to disable old generated prompt animators on migrated prefabs.")]
    [SerializeField] private Animator singlePromptAnimator;

    [Tooltip("Prompt root used by 3D Prompt mode to display direct slot-replacement bindings.")]
    [SerializeField] private GameObject swapPromptRoot;

    [Tooltip("Text label shown for the primary-slot replacement binding in 3D Prompt mode.")]
    [SerializeField] private TMP_Text swapPrimaryPromptText;

    [Tooltip("Text label shown for the secondary-slot replacement binding in 3D Prompt mode.")]
    [SerializeField] private TMP_Text swapSecondaryPromptText;

    [Tooltip("Legacy animator reference kept only to disable old generated prompt animators on migrated prefabs.")]
    [SerializeField] private Animator swapPromptAnimator;

    [Tooltip("When enabled, billboarding keeps the UI aligned only around the Y axis.")]
    [SerializeField] private bool billboardYawOnly;
    #endregion

    private static Camera cachedMainCamera;
    private static Transform cachedMainCameraTransform;
    private static float nextCameraResolveTime;
    private Sprite displayedIcon;
    private Coroutine singlePromptAnimationCoroutine;
    private Coroutine swapPromptAnimationCoroutine;
    private PromptPresentationState singlePromptState;
    private PromptPresentationState swapPromptState;

    /// <summary>
    /// Stores the cached references and presentation state for one world-space prompt root.
    /// none.
    /// returns none.
    /// </summary>
    private struct PromptPresentationState
    {
        public GameObject Root;
        public CanvasGroup CanvasGroup;
        public RectTransform RectTransform;
        public bool IsVisible;
    }
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Resolves child references and applies the hidden prompt state when the companion object awakens.
    /// none.
    /// returns void.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
        PreparePromptForRuntime(ref singlePromptState, singlePromptAnimator);
        PreparePromptForRuntime(ref swapPromptState, swapPromptAnimator);
        ApplyPromptStateImmediate(ref singlePromptState, false, true);
        ApplyPromptStateImmediate(ref swapPromptState, false, true);
        SetIcon(null);
    }

    /// <summary>
    /// Reapplies the current prompt state whenever the companion object becomes active again after ECS or pooling lifecycle changes.
    /// none.
    /// returns void.
    /// </summary>
    private void OnEnable()
    {
        CacheReferences();
        ApplyPromptStateImmediate(ref singlePromptState, singlePromptState.IsVisible, true);
        ApplyPromptStateImmediate(ref swapPromptState, swapPromptState.IsVisible, true);
    }

    /// <summary>
    /// Keeps the billboard root facing the active camera without rotating the sphere mesh itself.
    /// none.
    /// returns void.
    /// </summary>
    private void LateUpdate()
    {
        if (!NeedsBillboardUpdate())
            return;

        Transform cameraTransform = ResolveCameraTransform();

        if (billboardRoot == null || cameraTransform == null)
            return;

        if (billboardYawOnly)
        {
            Vector3 flattenedForward = cameraTransform.forward;
            flattenedForward.y = 0f;

            if (flattenedForward.sqrMagnitude <= BillboardMagnitudeEpsilon)
                return;

            billboardRoot.rotation = Quaternion.LookRotation(flattenedForward.normalized, Vector3.up);
            return;
        }

        billboardRoot.rotation = cameraTransform.rotation;
    }

    /// <summary>
    /// Stops active prompt transitions when the companion object is disabled, avoiding stale coroutine handles in pooled or destroyed instances.
    /// none.
    /// returns void.
    /// </summary>
    private void OnDisable()
    {
        StopPromptAnimation(ref singlePromptAnimationCoroutine);
        StopPromptAnimation(ref swapPromptAnimationCoroutine);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Refreshes auto-resolved references while the prefab or scene instance is edited.
    /// none.
    /// returns void.
    /// </summary>
    private void OnValidate()
    {
        CacheReferences();
    }

    /// <summary>
    /// Populates expected child references when the component is first added in the Editor.
    /// none.
    /// returns void.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
    }
#endif
    #endregion

    #region Public Methods
    /// <summary>
    /// Updates the icon shown at the center of the dropped container.
    /// icon: Sprite resolved from the dropped power-up id.
    /// returns void.
    /// </summary>
    public void SetIcon(Sprite icon)
    {
        if (iconImage == null)
            return;

        if (displayedIcon == icon && iconImage.enabled == (icon != null))
            return;

        displayedIcon = icon;
        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
    }

    /// <summary>
    /// Synchronizes the runtime scene object pose with the authoritative ECS container transform.
    /// worldPosition: World position resolved from ECS.
    /// worldRotation: World rotation resolved from ECS.
    /// uniformScale: Uniform scale resolved from ECS.
    /// returns void.
    /// </summary>
    public void SyncWorldPose(Vector3 worldPosition, Quaternion worldRotation, float uniformScale)
    {
        Transform currentTransform = transform;
        Vector3 targetScale = new Vector3(uniformScale, uniformScale, uniformScale);

        if (currentTransform.position != worldPosition)
            currentTransform.position = worldPosition;

        if (currentTransform.rotation != worldRotation)
            currentTransform.rotation = worldRotation;

        if (currentTransform.localScale != targetScale)
            currentTransform.localScale = targetScale;
    }

    /// <summary>
    /// Shows the overlay interaction prompt and hides the direct-replacement prompt.
    /// promptText: Text displayed to tell the player which interaction key to press.
    /// returns void.
    /// </summary>
    public void ShowSinglePrompt(string promptText)
    {
        SetText(singlePromptText, promptText);
        SetPromptVisible(ref singlePromptState, ref singlePromptAnimationCoroutine, true);
        SetPromptVisible(ref swapPromptState, ref swapPromptAnimationCoroutine, false);
    }

    /// <summary>
    /// Shows the direct-replacement prompt and hides the overlay interaction prompt.
    /// primaryPromptText: Binding label displayed for the primary-slot replacement action.
    /// secondaryPromptText: Binding label displayed for the secondary-slot replacement action.
    /// returns void.
    /// </summary>
    public void ShowSwapPrompt(string primaryPromptText, string secondaryPromptText)
    {
        SetText(swapPrimaryPromptText, primaryPromptText);
        SetText(swapSecondaryPromptText, secondaryPromptText);
        SetPromptVisible(ref singlePromptState, ref singlePromptAnimationCoroutine, false);
        SetPromptVisible(ref swapPromptState, ref swapPromptAnimationCoroutine, true);
    }

    /// <summary>
    /// Hides every world-space interaction prompt owned by this dropped container.
    /// none.
    /// returns void.
    /// </summary>
    public void HidePrompts()
    {
        SetPromptVisible(ref singlePromptState, ref singlePromptAnimationCoroutine, false);
        SetPromptVisible(ref swapPromptState, ref swapPromptAnimationCoroutine, false);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves missing serialized references from the generated child hierarchy.
    /// none.
    /// returns void.
    /// </summary>
    private void CacheReferences()
    {
        Transform rootTransform = transform;

        if (billboardRoot == null)
            billboardRoot = FindDescendantByName(rootTransform, "BillboardRoot");

        if (billboardCanvas == null && billboardRoot != null)
            billboardCanvas = billboardRoot.GetComponent<Canvas>();

        if (iconImage == null)
            iconImage = ResolveImage(rootTransform, "IconImage");

        if (singlePromptRoot == null)
        {
            Transform singlePromptTransform = FindDescendantByName(rootTransform, "SinglePromptRoot");

            if (singlePromptTransform != null)
                singlePromptRoot = singlePromptTransform.gameObject;
        }

        if (singlePromptText == null)
            singlePromptText = ResolveText(rootTransform, "SinglePromptText");

        if (singlePromptAnimator == null && singlePromptRoot != null)
            singlePromptAnimator = singlePromptRoot.GetComponent<Animator>();

        if (swapPromptRoot == null)
        {
            Transform swapPromptTransform = FindDescendantByName(rootTransform, "SwapPromptRoot");

            if (swapPromptTransform != null)
                swapPromptRoot = swapPromptTransform.gameObject;
        }

        if (swapPrimaryPromptText == null)
            swapPrimaryPromptText = ResolveText(rootTransform, "SwapPrimaryPromptText");

        if (swapSecondaryPromptText == null)
            swapSecondaryPromptText = ResolveText(rootTransform, "SwapSecondaryPromptText");

        if (swapPromptAnimator == null && swapPromptRoot != null)
            swapPromptAnimator = swapPromptRoot.GetComponent<Animator>();

        ConfigureBillboardCanvas();
        CachePromptState(ref singlePromptState, singlePromptRoot);
        CachePromptState(ref swapPromptState, swapPromptRoot);
    }

    /// <summary>
    /// Resolves the active camera transform and caches it for subsequent billboard updates.
    /// none.
    /// returns Active camera transform when available.
    /// </summary>
    private static Transform ResolveCameraTransform()
    {
        if (cachedMainCamera != null && cachedMainCamera.isActiveAndEnabled && cachedMainCameraTransform != null)
            return cachedMainCameraTransform;

        if (Time.unscaledTime < nextCameraResolveTime)
            return null;

        nextCameraResolveTime = Time.unscaledTime + CameraResolveRetryIntervalSeconds;

        if (!PlayerRuntimeCameraUtility.TryResolveGameplayCamera(out cachedMainCamera))
        {
            cachedMainCameraTransform = null;
            return null;
        }

        cachedMainCameraTransform = cachedMainCamera.transform;
        return cachedMainCameraTransform;
    }

    /// <summary>
    /// Returns whether the billboard canvas currently needs camera-facing updates.
    /// none.
    /// returns True when icon or prompts are currently visible enough to require billboarding.
    /// </summary>
    private bool NeedsBillboardUpdate()
    {
        return displayedIcon != null ||
               singlePromptState.IsVisible ||
               swapPromptState.IsVisible ||
               singlePromptAnimationCoroutine != null ||
               swapPromptAnimationCoroutine != null;
    }

    /// <summary>
    /// Ensures the world-space canvas uses explicit sorting so prompt UI renders reliably above the sphere.
    /// none.
    /// returns void.
    /// </summary>
    private void ConfigureBillboardCanvas()
    {
        if (billboardCanvas == null)
            return;

        billboardCanvas.overrideSorting = true;

        if (billboardCanvas.sortingOrder < BillboardSortingOrder)
            billboardCanvas.sortingOrder = BillboardSortingOrder;
    }

    /// <summary>
    /// Caches the components required to animate one prompt root.
    /// promptState: Cached prompt state updated in place.
    /// promptRoot: Prompt root resolved from the generated hierarchy.
    /// returns void.
    /// </summary>
    private static void CachePromptState(ref PromptPresentationState promptState,
                                         GameObject promptRoot)
    {
        promptState.Root = promptRoot;
        promptState.CanvasGroup = promptRoot != null ? promptRoot.GetComponent<CanvasGroup>() : null;
        promptState.RectTransform = promptRoot != null ? promptRoot.GetComponent<RectTransform>() : null;
    }

    /// <summary>
    /// Activates one prompt root during runtime and disables any legacy Animator left on migrated prefabs.
    /// promptState: Cached prompt state updated in place.
    /// promptAnimator: Legacy prompt animator disabled when present.
    /// returns void.
    /// </summary>
    private static void PreparePromptForRuntime(ref PromptPresentationState promptState, Animator promptAnimator)
    {
        if (promptState.Root == null)
            return;

        if (!promptState.Root.activeSelf)
            promptState.Root.SetActive(true);

        if (promptAnimator != null && promptAnimator.enabled)
            promptAnimator.enabled = false;
    }

    /// <summary>
    /// Applies one target visibility state to the prompt by starting one short transition coroutine only when the state changes.
    /// promptState: Cached prompt state updated in place.
    /// animationCoroutine: Running coroutine reference updated in place.
    /// visible: Target prompt visibility.
    /// returns void.
    /// </summary>
    private void SetPromptVisible(ref PromptPresentationState promptState,
                                  ref Coroutine animationCoroutine,
                                  bool visible)
    {
        if (promptState.IsVisible == visible)
            return;

        promptState.IsVisible = visible;

        if (promptState.Root == null)
            return;

        if (!promptState.Root.activeSelf)
            promptState.Root.SetActive(true);

        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);

        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            animationCoroutine = null;
            ApplyPromptStateImmediate(ref promptState, visible, false);
            return;
        }

        GameObject promptRoot = promptState.Root;
        animationCoroutine = StartCoroutine(AnimatePromptVisibility(promptState.Root,
                                                                   promptState.CanvasGroup,
                                                                   promptState.RectTransform,
                                                                   visible,
                                                                   () => ClearPromptAnimationCoroutine(promptRoot)));
    }

    /// <summary>
    /// Applies one prompt visibility state immediately, primarily for initialization, validation, and Animator fallback.
    /// promptState: Cached prompt state updated in place.
    /// visible: Target prompt visibility applied instantly.
    /// activateRoot: True to force the prompt root active before writing UI state.
    /// returns void.
    /// </summary>
    private static void ApplyPromptStateImmediate(ref PromptPresentationState promptState, bool visible, bool activateRoot)
    {
        promptState.IsVisible = visible;

        if (promptState.Root == null)
            return;

        if (activateRoot && !promptState.Root.activeSelf)
            promptState.Root.SetActive(true);

        if (promptState.CanvasGroup != null)
            promptState.CanvasGroup.alpha = visible ? 1f : 0f;

        if (promptState.RectTransform == null)
            return;

        promptState.RectTransform.anchoredPosition = visible ? PromptVisibleAnchoredPosition : PromptHiddenAnchoredPosition;
        promptState.RectTransform.localScale = Vector3.one * (visible ? PromptVisibleScaleMultiplier : PromptHiddenScaleMultiplier);
    }

    /// <summary>
    /// Runs the short prompt transition that rises out of the sphere on show and falls back inside on hide.
    /// promptRoot: Prompt root animated by the transition.
    /// canvasGroup: Canvas group used to animate prompt opacity.
    /// rectTransform: RectTransform used to animate position and scale.
    /// visible: Target prompt visibility.
    /// onCompleted: Callback invoked after the coroutine reference has to be cleared.
    /// returns IEnumerator used by StartCoroutine.
    /// </summary>
    private IEnumerator AnimatePromptVisibility(GameObject promptRoot,
                                               CanvasGroup canvasGroup,
                                               RectTransform rectTransform,
                                               bool visible,
                                               System.Action onCompleted)
    {
        if (promptRoot == null || rectTransform == null)
        {
            onCompleted?.Invoke();
            yield break;
        }

        if (!promptRoot.activeSelf)
            promptRoot.SetActive(true);

        Vector2 startAnchoredPosition = rectTransform.anchoredPosition;
        Vector3 startScale = rectTransform.localScale;
        float startAlpha = canvasGroup != null ? canvasGroup.alpha : (visible ? 0f : 1f);

        if (visible)
        {
            yield return AnimatePromptPhase(canvasGroup,
                                            rectTransform,
                                            startAlpha,
                                            1f,
                                            startAnchoredPosition,
                                            PromptOvershootAnchoredPosition,
                                            startScale,
                                            Vector3.one * PromptOvershootScaleMultiplier,
                                            PromptShowRiseDurationSeconds);
            yield return AnimatePromptPhase(canvasGroup,
                                            rectTransform,
                                            1f,
                                            1f,
                                            PromptOvershootAnchoredPosition,
                                            PromptVisibleAnchoredPosition,
                                            Vector3.one * PromptOvershootScaleMultiplier,
                                            Vector3.one * PromptVisibleScaleMultiplier,
                                            PromptShowSettleDurationSeconds);
        }
        else
        {
            yield return AnimatePromptPhase(canvasGroup,
                                            rectTransform,
                                            startAlpha,
                                            0f,
                                            startAnchoredPosition,
                                            PromptHiddenAnchoredPosition,
                                            startScale,
                                            Vector3.one * PromptHiddenScaleMultiplier,
                                            PromptHideDurationSeconds);
        }

        onCompleted?.Invoke();
    }

    /// <summary>
    /// Interpolates one prompt transition phase using unscaled time so the prompt stays responsive while gameplay is paused.
    /// canvasGroup: Canvas group used to animate prompt opacity.
    /// rectTransform: RectTransform used to animate prompt position and scale.
    /// startAlpha: Phase starting alpha.
    /// targetAlpha: Phase target alpha.
    /// startAnchoredPosition: Phase starting anchored position.
    /// targetAnchoredPosition: Phase target anchored position.
    /// startScale: Phase starting scale.
    /// targetScale: Phase target scale.
    /// durationSeconds: Unscaled duration assigned to the phase.
    /// returns IEnumerator used by the parent coroutine.
    /// </summary>
    private static IEnumerator AnimatePromptPhase(CanvasGroup canvasGroup,
                                                  RectTransform rectTransform,
                                                  float startAlpha,
                                                  float targetAlpha,
                                                  Vector2 startAnchoredPosition,
                                                  Vector2 targetAnchoredPosition,
                                                  Vector3 startScale,
                                                  Vector3 targetScale,
                                                  float durationSeconds)
    {
        if (durationSeconds <= 0f)
        {
            ApplyPromptPresentation(canvasGroup,
                                    rectTransform,
                                    targetAlpha,
                                    targetAnchoredPosition,
                                    targetScale);
            yield break;
        }

        float elapsedSeconds = 0f;

        while (elapsedSeconds < durationSeconds)
        {
            elapsedSeconds += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedSeconds / durationSeconds);
            float easedTime = Mathf.SmoothStep(0f, 1f, normalizedTime);
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, easedTime);
            Vector2 anchoredPosition = Vector2.LerpUnclamped(startAnchoredPosition, targetAnchoredPosition, easedTime);
            Vector3 scale = Vector3.LerpUnclamped(startScale, targetScale, easedTime);
            ApplyPromptPresentation(canvasGroup, rectTransform, alpha, anchoredPosition, scale);
            yield return null;
        }

        ApplyPromptPresentation(canvasGroup,
                                rectTransform,
                                targetAlpha,
                                targetAnchoredPosition,
                                targetScale);
    }

    /// <summary>
    /// Writes one prompt presentation snapshot into the world-space UI components.
    /// canvasGroup: Canvas group receiving the alpha.
    /// rectTransform: RectTransform receiving position and scale.
    /// alpha: Prompt alpha to apply.
    /// anchoredPosition: Prompt anchored position to apply.
    /// scale: Prompt scale to apply.
    /// returns void.
    /// </summary>
    private static void ApplyPromptPresentation(CanvasGroup canvasGroup,
                                                RectTransform rectTransform,
                                                float alpha,
                                                Vector2 anchoredPosition,
                                                Vector3 scale)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = alpha;

        if (rectTransform == null)
            return;

        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.localScale = scale;
    }

    /// <summary>
    /// Clears the coroutine handle associated with the completed prompt transition.
    /// promptRoot: Prompt root whose coroutine handle must be cleared.
    /// returns void.
    /// </summary>
    private void ClearPromptAnimationCoroutine(GameObject promptRoot)
    {
        if (promptRoot == null)
            return;

        if (ReferenceEquals(singlePromptState.Root, promptRoot))
        {
            singlePromptAnimationCoroutine = null;
            return;
        }

        if (ReferenceEquals(swapPromptState.Root, promptRoot))
            swapPromptAnimationCoroutine = null;
    }

    /// <summary>
    /// Stops one active prompt animation coroutine and clears the cached handle.
    /// animationCoroutine: Coroutine handle updated in place.
    /// returns void.
    /// </summary>
    private void StopPromptAnimation(ref Coroutine animationCoroutine)
    {
        if (animationCoroutine == null)
            return;

        StopCoroutine(animationCoroutine);
        animationCoroutine = null;
    }

    /// <summary>
    /// Assigns text to an optional TMP label while tolerating missing references.
    /// label: TMP label updated by the helper.
    /// value: New string assigned to the label.
    /// returns void.
    /// </summary>
    private static void SetText(TMP_Text label, string value)
    {
        if (label == null)
            return;

        label.text = string.IsNullOrWhiteSpace(value) ? string.Empty : value;
    }

    /// <summary>
    /// Finds one descendant transform by exact name using depth-first traversal.
    /// rootTransform: Traversal root.
    /// childName: Exact child object name requested.
    /// returns Matching descendant transform when found; otherwise null.
    /// </summary>
    private static Transform FindDescendantByName(Transform rootTransform, string childName)
    {
        if (rootTransform == null || string.IsNullOrWhiteSpace(childName))
            return null;

        if (string.Equals(rootTransform.name, childName, System.StringComparison.Ordinal))
            return rootTransform;

        int childCount = rootTransform.childCount;

        for (int childIndex = 0; childIndex < childCount; childIndex++)
        {
            Transform foundTransform = FindDescendantByName(rootTransform.GetChild(childIndex), childName);

            if (foundTransform != null)
                return foundTransform;
        }

        return null;
    }

    /// <summary>
    /// Resolves one descendant image from the generated hierarchy.
    /// rootTransform: Search root used to resolve the image.
    /// objectName: Exact child object name.
    /// returns Resolved image when found; otherwise null.
    /// </summary>
    private static Image ResolveImage(Transform rootTransform, string objectName)
    {
        Transform childTransform = FindDescendantByName(rootTransform, objectName);

        if (childTransform == null)
            return null;

        return childTransform.GetComponent<Image>();
    }

    /// <summary>
    /// Resolves one descendant TMP label from the generated hierarchy.
    /// rootTransform: Search root used to resolve the label.
    /// objectName: Exact child object name.
    /// returns Resolved TMP label when found; otherwise null.
    /// </summary>
    private static TMP_Text ResolveText(Transform rootTransform, string objectName)
    {
        Transform childTransform = FindDescendantByName(rootTransform, objectName);

        if (childTransform == null)
            return null;

        return childTransform.GetComponent<TMP_Text>();
    }
    #endregion

    #endregion
}

/// <summary>
/// Bakes dropped-container companion views into managed ECS component objects on their root entity.
/// none.
/// returns none.
/// </summary>
public sealed class PlayerDroppedPowerUpContainerViewBaker : Baker<PlayerDroppedPowerUpContainerView>
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Bakes the dropped-container companion view and keeps the generated child hierarchy available to the companion pipeline.
    /// authoring: Authoring component baked on the dropped-container prefab root.
    /// returns void.
    /// </summary>
    public override void Bake(PlayerDroppedPowerUpContainerView authoring)
    {
        if (authoring == null)
            return;

        Entity rootEntity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponentObject(rootEntity, authoring);
        Transform rootTransform = authoring.transform;

        if (rootTransform == null)
            return;

        Transform[] childTransforms = rootTransform.GetComponentsInChildren<Transform>(true);

        for (int childIndex = 0; childIndex < childTransforms.Length; childIndex++)
        {
            Transform childTransform = childTransforms[childIndex];

            if (childTransform == null || childTransform == rootTransform)
                continue;

            GetEntity(childTransform.gameObject, TransformUsageFlags.Dynamic);
        }
    }
    #endregion

    #endregion
}
