using System.Collections;
using TMPro;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents interruptible preauthored room-clear messages from versioned ECS presentation requests.
/// </summary>
[DisallowMultipleComponent]
public sealed class HUDWaveClearAnnouncementSection : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Presentation")]
    [Tooltip("Full-screen RectTransform used to resolve off-screen start, center, and end positions.")]
    [SerializeField]
    private RectTransform presentationRoot;

    [Tooltip("Preauthored RectTransform moved horizontally across the gameplay HUD.")]
    [SerializeField]
    private RectTransform textRoot;

    [Tooltip("Preauthored text receiving the baked announcement content and typography.")]
    [SerializeField]
    private TMP_Text announcementText;

    [Tooltip("Canvas group used to hide the announcement without enabling or instantiating UI at runtime.")]
    [SerializeField]
    private CanvasGroup canvasGroup;

    [Header("Paint Reveal")]
    [Tooltip("Preauthored Image whose paint material writes the stencil mask used by the announcement content.")]
    [SerializeField]
    private Image paintMaskImage;

    [Tooltip("Preauthored UI Mask that clips the text and background to the animated paint coverage.")]
    [SerializeField]
    private Mask paintMask;

    [Tooltip("Preauthored Image rendered behind the text and clipped by the animated paint mask.")]
    [SerializeField]
    private Image paintBackgroundImage;

    [Tooltip("Dedicated authored Paint Reveal material used only by the room-clear stencil mask.")]
    [SerializeField]
    private Material paintRevealMaterial;
    #endregion

    #region Runtime Fields
    private World activeWorld;
    private EntityQuery configQuery;
    private EntityQuery progressionQuery;
    private Entity presentationEntity;
    private GameHudWaveClearAnnouncementRuntimeConfig baseConfig;
    private GameHudWaveClearAnnouncementRuntimeConfig presentationConfig;
    private Coroutine presentationCoroutine;
    private uint activeRequestVersion;
    private uint observedGenerationVersion;
    private int observedNodeIndex;
    private bool initialized;
    private bool queriesInitialized;
    private bool configApplied;
    private bool progressionObserved;
    private Material paintMaskRuntimeMaterial;
    #endregion

    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Establishes the hidden initial state while retaining observation across repeated HUD initialization passes.
    /// </summary>
    public void Initialize()
    {
        if (initialized)
            return;

        initialized = true;
        HidePresentation();
    }

    /// <summary>
    /// Releases the active transition and cached ECS queries owned by this managed presentation section.
    /// </summary>
    public void Dispose()
    {
        StopPresentation(true);
        ReleaseQueries();
        initialized = false;
    }

    /// <summary>
    /// Consumes pending ECS room-clear requests and interrupts the active sweep when procedural room identity changes.
    /// </summary>
    /// <param name="entityManager">Entity manager owning HUD presentation config and procedural room state.</param>
    public void UpdateSection(EntityManager entityManager)
    {
        if (!EnsureQueries(entityManager) ||
            progressionQuery.CalculateEntityCount() != 1 ||
            !TryApplyConfig(entityManager))
        {
            return;
        }

        Entity progressionEntity = progressionQuery.GetSingletonEntity();
        GameRoomClearAnnouncementProgressState progressState =
            entityManager.GetComponentData<GameRoomClearAnnouncementProgressState>(progressionEntity);

        if (!progressionObserved)
        {
            progressionObserved = true;
            observedGenerationVersion = progressState.ObservedGenerationVersion;
            observedNodeIndex = progressState.ObservedNodeIndex;
        }

        bool roomChanged = progressState.ObservedGenerationVersion != observedGenerationVersion ||
                           progressState.ObservedNodeIndex != observedNodeIndex;

        if (roomChanged)
        {
            StopPresentation(true);
            observedGenerationVersion = progressState.ObservedGenerationVersion;
            observedNodeIndex = progressState.ObservedNodeIndex;
        }

        GameHudWaveClearAnnouncementPresentationState presentationState =
            entityManager.GetComponentData<GameHudWaveClearAnnouncementPresentationState>(presentationEntity);

        if (presentationState.Pending == 0 ||
            presentationState.RequestedVersion == presentationState.CompletedVersion ||
            presentationState.RequestedVersion == activeRequestVersion)
        {
            return;
        }

        if (presentationState.GenerationVersion != progressState.ObservedGenerationVersion ||
            presentationState.NodeIndex != progressState.ObservedNodeIndex)
        {
            CompleteRequest(entityManager, presentationState.RequestedVersion);
            return;
        }

        StopPresentation(true);
        PreparePresentationConfig(presentationState.IsFinalWave != 0);
        StartPresentation(entityManager, presentationState.RequestedVersion);
    }
    #endregion

    #region ECS Configuration
    /// <summary>
    /// Recreates compact singleton queries only when the active ECS world changes.
    /// </summary>
    /// <param name="entityManager">Entity manager associated with the current HUD update.</param>
    /// <returns>True when queries for the current ECS world are available.</returns>
    private bool EnsureQueries(EntityManager entityManager)
    {
        World currentWorld = World.DefaultGameObjectInjectionWorld;

        if (currentWorld == null || !currentWorld.IsCreated)
            return false;

        if (queriesInitialized && ReferenceEquals(activeWorld, currentWorld))
            return true;

        ReleaseQueries();
        activeWorld = currentWorld;
        configQuery = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<GameHudWaveClearAnnouncementRuntimeConfig>(),
            ComponentType.ReadWrite<GameHudWaveClearAnnouncementPresentationState>());
        progressionQuery = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<GameRoomClearAnnouncementProgressState>());
        queriesInitialized = true;
        configApplied = false;
        progressionObserved = false;
        presentationEntity = Entity.Null;
        return true;
    }

    /// <summary>
    /// Caches the immutable baked announcement config once for the current ECS world.
    /// </summary>
    /// <param name="entityManager">Entity manager containing the announcement singleton.</param>
    /// <returns>True when exactly one announcement config and presentation state were resolved.</returns>
    private bool TryApplyConfig(EntityManager entityManager)
    {
        if (configApplied && presentationEntity != Entity.Null && entityManager.Exists(presentationEntity))
            return true;

        if (configQuery.CalculateEntityCount() != 1)
            return false;

        presentationEntity = configQuery.GetSingletonEntity();
        baseConfig = entityManager.GetComponentData<GameHudWaveClearAnnouncementRuntimeConfig>(presentationEntity);
        presentationConfig = baseConfig;
        configApplied = true;
        return true;
    }

    /// <summary>
    /// Disposes cached queries while their world remains valid and clears all observation state.
    /// </summary>
    private void ReleaseQueries()
    {
        if (queriesInitialized && activeWorld != null && activeWorld.IsCreated)
        {
            configQuery.Dispose();
            progressionQuery.Dispose();
        }

        activeWorld = null;
        presentationEntity = Entity.Null;
        queriesInitialized = false;
        configApplied = false;
        progressionObserved = false;
        activeRequestVersion = 0;
    }

    /// <summary>
    /// Selects standard or terminal-Boss text and motion while retaining shared typography and placement.
    /// </summary>
    /// <param name="useFinalOverride">True when the terminal Boss override owns the request.</param>
    private void PreparePresentationConfig(bool useFinalOverride)
    {
        presentationConfig = baseConfig;

        if (!useFinalOverride)
            return;

        presentationConfig.Content = baseConfig.FinalWaveContent;
        presentationConfig.PresentationMode = baseConfig.FinalWavePresentationMode;
        presentationConfig.Direction = baseConfig.FinalWaveDirection;
        presentationConfig.PaintExitDirection = baseConfig.FinalWavePaintExitDirection;
        presentationConfig.TraversalDurationSeconds = baseConfig.FinalWaveTraversalDurationSeconds;
        presentationConfig.Easing = baseConfig.FinalWaveEasing;
        presentationConfig.PauseAtCenter = baseConfig.FinalWavePauseAtCenter;
        presentationConfig.CenterHoldDurationSeconds = baseConfig.FinalWaveCenterHoldDurationSeconds;
        presentationConfig.PaintRevealDurationSeconds = baseConfig.FinalWavePaintRevealDurationSeconds;
        presentationConfig.PaintHoldDurationSeconds = baseConfig.FinalWavePaintHoldDurationSeconds;
        presentationConfig.PaintFadeOutDurationSeconds = baseConfig.FinalWavePaintFadeOutDurationSeconds;
    }

    /// <summary>
    /// Marks one pending request active so the victory menu can wait for managed presentation completion.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the presentation state.</param>
    /// <param name="requestVersion">Request version being started.</param>
    private void MarkRequestActive(EntityManager entityManager, uint requestVersion)
    {
        if (presentationEntity == Entity.Null || !entityManager.Exists(presentationEntity))
            return;

        GameHudWaveClearAnnouncementPresentationState state =
            entityManager.GetComponentData<GameHudWaveClearAnnouncementPresentationState>(presentationEntity);

        if (state.RequestedVersion != requestVersion)
            return;

        state.Pending = 0;
        state.Active = 1;
        entityManager.SetComponentData(presentationEntity, state);
    }

    /// <summary>
    /// Completes the matching request and releases its optional victory-menu gate.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the presentation state.</param>
    /// <param name="requestVersion">Request version being completed or interrupted.</param>
    private void CompleteRequest(EntityManager entityManager, uint requestVersion)
    {
        if (requestVersion == 0 ||
            presentationEntity == Entity.Null ||
            !entityManager.Exists(presentationEntity))
        {
            return;
        }

        GameHudWaveClearAnnouncementPresentationState state =
            entityManager.GetComponentData<GameHudWaveClearAnnouncementPresentationState>(presentationEntity);

        if (state.RequestedVersion != requestVersion)
            return;

        state.CompletedVersion = requestVersion;
        state.Pending = 0;
        state.Active = 0;
        state.BlocksVictoryMenu = 0;
        entityManager.SetComponentData(presentationEntity, state);
    }
    #endregion

    #region Presentation
    /// <summary>
    /// Applies content, font, size, style, and color to the existing text object without runtime UI creation.
    /// </summary>
    private void ApplyTypography()
    {
        if (announcementText == null)
            return;

        announcementText.text = presentationConfig.Content.ToString();
        announcementText.fontSize = presentationConfig.FontSize;
        announcementText.fontStyle = (FontStyles)presentationConfig.FontStyle;
        announcementText.color = new Color(presentationConfig.Color.x,
                                           presentationConfig.Color.y,
                                           presentationConfig.Color.z,
                                           presentationConfig.Color.w);

        if (presentationConfig.Font.Value != null)
            announcementText.font = presentationConfig.Font.Value;
    }

    /// <summary>
    /// Starts one interruptible traversal or paint reveal using current canvas dimensions.
    /// </summary>
    /// <param name="entityManager">Entity manager receiving active presentation state.</param>
    /// <param name="requestVersion">Version of the request being displayed.</param>
    private void StartPresentation(EntityManager entityManager, uint requestVersion)
    {
        activeRequestVersion = requestVersion;

        if (presentationRoot == null || textRoot == null || announcementText == null || canvasGroup == null)
        {
            CompleteRequest(entityManager, requestVersion);
            activeRequestVersion = 0;
            return;
        }

        ApplyTypography();
        Canvas.ForceUpdateCanvases();
        announcementText.ForceMeshUpdate();
        ConfigurePresentationBounds();
        float verticalPosition = math.lerp(-presentationRoot.rect.height * 0.5f,
                                           presentationRoot.rect.height * 0.5f,
                                           presentationConfig.VerticalPositionNormalized);
        textRoot.anchoredPosition = new Vector2(0f, verticalPosition);
        canvasGroup.alpha = 1f;
        MarkRequestActive(entityManager, requestVersion);

        switch (presentationConfig.PresentationMode)
        {
            case GameHudWaveClearAnnouncementPresentationMode.PaintReveal:
                if (!ConfigurePaintPresentation())
                {
                    CompleteRequest(entityManager, requestVersion);
                    activeRequestVersion = 0;
                    HidePresentation();
                    return;
                }

                presentationCoroutine = StartCoroutine(PresentPaint(requestVersion));
                return;
            default:
                ConfigureTraversalPresentation();
                StartTraversal(verticalPosition, requestVersion);
                return;
        }
    }

    /// <summary>
    /// Sizes the preauthored content root from measured text bounds and configured paint padding.
    /// </summary>
    private void ConfigurePresentationBounds()
    {
        Vector2 padding = new Vector2(math.max(0f, presentationConfig.PaintBackgroundPadding.x),
                                      math.max(0f, presentationConfig.PaintBackgroundPadding.y));
        textRoot.sizeDelta = new Vector2(announcementText.preferredWidth + padding.x * 2f,
                                         announcementText.preferredHeight + padding.y * 2f);
        RectTransform announcementRect = announcementText.rectTransform;
        announcementRect.anchorMin = Vector2.zero;
        announcementRect.anchorMax = Vector2.one;
        announcementRect.offsetMin = padding;
        announcementRect.offsetMax = -padding;
    }

    /// <summary>
    /// Disables stencil clipping and background rendering for the legacy traversal presentation.
    /// </summary>
    private void ConfigureTraversalPresentation()
    {
        paintMaskRuntimeMaterial = null;

        if (paintMask != null)
            paintMask.enabled = false;

        if (paintMaskImage != null)
            paintMaskImage.enabled = false;

        if (paintBackgroundImage != null)
            paintBackgroundImage.enabled = false;
    }

    /// <summary>
    /// Resolves off-screen endpoints and begins the edge-to-edge traversal coroutine.
    /// </summary>
    /// <param name="verticalPosition">Canvas-space vertical position shared by both endpoints.</param>
    /// <param name="requestVersion">Version completed after the outgoing segment.</param>
    private void StartTraversal(float verticalPosition, uint requestVersion)
    {
        float horizontalDistance = presentationRoot.rect.width * 0.5f +
                                   textRoot.rect.width * 0.5f +
                                   presentationConfig.HorizontalOffscreenPadding;
        float direction = presentationConfig.Direction == GameHudWaveClearAnnouncementDirection.LeftToRight
            ? 1f
            : -1f;
        Vector2 startPosition = new Vector2(-horizontalDistance * direction, verticalPosition);
        Vector2 endPosition = new Vector2(horizontalDistance * direction, verticalPosition);
        textRoot.anchoredPosition = startPosition;
        presentationCoroutine = StartCoroutine(PresentTraversal(startPosition, endPosition, requestVersion));
    }

    /// <summary>
    /// Moves through entry, optional center hold, and exit phases before completing the ECS request.
    /// </summary>
    /// <param name="startPosition">Fully off-screen entry position.</param>
    /// <param name="endPosition">Fully off-screen exit position.</param>
    /// <param name="requestVersion">Version completed after the outgoing segment.</param>
    /// <returns>Coroutine enumerator scheduled only while the announcement is visible.</returns>
    private IEnumerator PresentTraversal(Vector2 startPosition, Vector2 endPosition, uint requestVersion)
    {
        float halfDuration = presentationConfig.TraversalDurationSeconds * 0.5f;
        yield return MoveSegment(startPosition, new Vector2(0f, startPosition.y), halfDuration, true);

        if (presentationConfig.PauseAtCenter != 0 && presentationConfig.CenterHoldDurationSeconds > 0f)
            yield return WaitDuration(presentationConfig.CenterHoldDurationSeconds);

        yield return MoveSegment(new Vector2(0f, endPosition.y), endPosition, halfDuration, false);
        presentationCoroutine = null;
        CompleteActiveRequest(requestVersion);
        HidePresentation();
    }

    /// <summary>
    /// Configures the authored stencil hierarchy and dedicated material for stationary paint presentation.
    /// </summary>
    /// <returns>True when all paint references and required shader properties are available.</returns>
    private bool ConfigurePaintPresentation()
    {
        if (paintMaskImage == null ||
            paintMask == null ||
            paintBackgroundImage == null ||
            paintRevealMaterial == null ||
            !GameUiPaintRevealMaterialUtility.IsCompatible(paintRevealMaterial))
            return false;

        Sprite paintSprite = presentationConfig.PaintBackgroundSprite.Value;

        if (paintSprite == null)
            return false;

        paintMaskImage.sprite = paintSprite;
        paintBackgroundImage.sprite = paintSprite;
        paintBackgroundImage.color = new Color(presentationConfig.PaintBackgroundColor.x,
                                               presentationConfig.PaintBackgroundColor.y,
                                               presentationConfig.PaintBackgroundColor.z,
                                               presentationConfig.PaintBackgroundColor.w);
        paintBackgroundImage.enabled = true;
        paintMaskImage.material = paintRevealMaterial;
        paintMaskImage.enabled = true;
        paintMask.enabled = true;
        paintMaskImage.SetMaterialDirty();
        paintMaskRuntimeMaterial = paintMaskImage.materialForRendering;
        ConfigurePaintOperation(presentationConfig.Direction,
                                GameUiPaintRevealOperation.Deposit);
        SetPaintProgress(0f);
        return true;
    }

    /// <summary>
    /// Deposits, holds, and directionally removes the stationary paint announcement before completing its ECS request.
    /// </summary>
    /// <param name="requestVersion">Version completed after the paint presentation.</param>
    /// <returns>Coroutine enumerator for the complete paint sequence.</returns>
    private IEnumerator PresentPaint(uint requestVersion)
    {
        yield return AnimatePaintOperation(presentationConfig.Direction,
                                           GameUiPaintRevealOperation.Deposit,
                                           presentationConfig.PaintRevealDurationSeconds);
        yield return WaitDuration(presentationConfig.PaintHoldDurationSeconds);
        yield return AnimatePaintOperation(presentationConfig.PaintExitDirection,
                                           GameUiPaintRevealOperation.Remove,
                                           presentationConfig.PaintFadeOutDurationSeconds);
        presentationCoroutine = null;
        CompleteActiveRequest(requestVersion);
        HidePresentation();
    }

    /// <summary>
    /// Advances one deposit or removal frontier once per rendered frame for the configured interval.
    /// </summary>
    /// <param name="direction">Screen-space direction followed by the active frontier.</param>
    /// <param name="operation">Deposit for entry or remove for exit.</param>
    /// <param name="durationSeconds">Positive phase duration.</param>
    /// <returns>Coroutine enumerator for the active paint operation.</returns>
    private IEnumerator AnimatePaintOperation(GameHudWaveClearAnnouncementDirection direction,
                                              GameUiPaintRevealOperation operation,
                                              float durationSeconds)
    {
        float elapsedSeconds = 0f;
        ConfigurePaintOperation(direction, operation);
        SetPaintProgress(0f);

        while (elapsedSeconds < durationSeconds)
        {
            elapsedSeconds += ResolveDeltaTime();
            float normalizedTime = math.saturate(elapsedSeconds / durationSeconds);
            SetPaintProgress(normalizedTime * normalizedTime * (3f - 2f * normalizedTime));
            yield return null;
        }

        SetPaintProgress(1f);
    }

    /// <summary>
    /// Applies one phase direction and operation to the generated stencil material without mutating the authored asset.
    /// </summary>
    /// <param name="direction">Horizontal direction followed by the active paint frontier.</param>
    /// <param name="operation">Deposit or removal operation evaluated by the shader.</param>
    private void ConfigurePaintOperation(GameHudWaveClearAnnouncementDirection direction,
                                         GameUiPaintRevealOperation operation)
    {
        float aspectRatio = textRoot.rect.width / math.max(1f, textRoot.rect.height);
        Material targetMaterial = paintMaskRuntimeMaterial != null
            ? paintMaskRuntimeMaterial
            : paintRevealMaterial;
        GameUiPaintRevealMaterialUtility.ConfigureRoomClear(targetMaterial,
                                                            in presentationConfig,
                                                            direction,
                                                            operation,
                                                            aspectRatio);
    }

    /// <summary>
    /// Waits without allocations while respecting the announcement time source.
    /// </summary>
    /// <param name="durationSeconds">Non-negative hold duration.</param>
    /// <returns>Coroutine enumerator for the hold interval.</returns>
    private IEnumerator WaitDuration(float durationSeconds)
    {
        float elapsedSeconds = 0f;

        while (elapsedSeconds < durationSeconds)
        {
            elapsedSeconds += ResolveDeltaTime();
            yield return null;
        }
    }

    /// <summary>
    /// Writes the only per-frame paint value to the generated stencil material.
    /// </summary>
    /// <param name="progress">Normalized paint coverage.</param>
    private void SetPaintProgress(float progress)
    {
        GameUiPaintRevealMaterialUtility.SetProgress(paintMaskRuntimeMaterial != null
                                                         ? paintMaskRuntimeMaterial
                                                         : paintRevealMaterial,
                                                     progress);
    }

    /// <summary>
    /// Interpolates one traversal half using the selected center-aware velocity profile.
    /// </summary>
    /// <param name="startPosition">Segment start position.</param>
    /// <param name="endPosition">Segment target position.</param>
    /// <param name="durationSeconds">Seconds assigned to this traversal half.</param>
    /// <param name="incoming">True for movement toward center; false for movement away from center.</param>
    /// <returns>Coroutine enumerator for one motion segment.</returns>
    private IEnumerator MoveSegment(Vector2 startPosition,
                                    Vector2 endPosition,
                                    float durationSeconds,
                                    bool incoming)
    {
        float elapsedSeconds = 0f;

        while (elapsedSeconds < durationSeconds)
        {
            elapsedSeconds += ResolveDeltaTime();
            float normalizedTime = math.saturate(elapsedSeconds / durationSeconds);
            textRoot.anchoredPosition = Vector2.LerpUnclamped(startPosition,
                                                              endPosition,
                                                              EvaluateMotion(normalizedTime, incoming));
            yield return null;
        }

        textRoot.anchoredPosition = endPosition;
    }

    /// <summary>
    /// Evaluates linear, smooth, or center-decelerating motion without allocating an animation curve.
    /// </summary>
    /// <param name="normalizedTime">Normalized time inside the current traversal half.</param>
    /// <param name="incoming">True while approaching screen center.</param>
    /// <returns>Interpolation factor for the configured velocity profile.</returns>
    private float EvaluateMotion(float normalizedTime, bool incoming)
    {
        switch (presentationConfig.Easing)
        {
            case GameHudWaveClearAnnouncementEasing.SmoothStep:
                return normalizedTime * normalizedTime * (3f - 2f * normalizedTime);
            case GameHudWaveClearAnnouncementEasing.DecelerateAtCenter:
                if (incoming)
                    return 1f - math.pow(1f - normalizedTime, 3f);

                return normalizedTime * normalizedTime * normalizedTime;
            default:
                return normalizedTime;
        }
    }

    /// <summary>
    /// Resolves scaled or unscaled frame time according to the baked motion setting.
    /// </summary>
    /// <returns>Current frame delta used by the active announcement transition.</returns>
    private float ResolveDeltaTime()
    {
        return presentationConfig.UseUnscaledTime != 0 ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    /// <summary>
    /// Cancels an active traversal, optionally completes its ECS request, and hides the preauthored view.
    /// </summary>
    /// <param name="completeRequest">True to finish the matching ECS request and release any victory gate.</param>
    private void StopPresentation(bool completeRequest)
    {
        if (presentationCoroutine != null)
        {
            StopCoroutine(presentationCoroutine);
            presentationCoroutine = null;
        }

        if (completeRequest)
            CompleteActiveRequest(activeRequestVersion);

        activeRequestVersion = 0;
        HidePresentation();
    }

    /// <summary>
    /// Completes the active request through the current world when that world is still valid.
    /// </summary>
    /// <param name="requestVersion">Request version to complete.</param>
    private void CompleteActiveRequest(uint requestVersion)
    {
        if (activeWorld == null || !activeWorld.IsCreated)
            return;

        CompleteRequest(activeWorld.EntityManager, requestVersion);

        if (activeRequestVersion == requestVersion)
            activeRequestVersion = 0;
    }

    /// <summary>
    /// Hides the canvas group and prevents it from intercepting UI input.
    /// </summary>
    private void HidePresentation()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (paintMask != null)
            paintMask.enabled = false;

        if (paintMaskImage != null)
            paintMaskImage.enabled = false;

        if (paintBackgroundImage != null)
            paintBackgroundImage.enabled = false;

        paintMaskRuntimeMaterial = null;
    }
    #endregion

    #endregion
}
