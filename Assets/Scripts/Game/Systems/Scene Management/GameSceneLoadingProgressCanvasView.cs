using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authored loading-progress overlay view controlled by the Game Scene Manager presentation system.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameSceneLoadingProgressCanvasView : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("References")]
    [Tooltip("Child root that contains all loading-progress graphics. Keep this object separate from the component object so it can be hidden without disabling the bridge.")]
    [SerializeField] private GameObject progressRoot;

    [Tooltip("CanvasGroup used to show or hide the loading-progress indicator without changing the fade canvas itself.")]
    [SerializeField] private CanvasGroup progressCanvasGroup;

    [Tooltip("RectTransform rotated every unscaled frame while the loading-progress indicator is visible.")]
    [SerializeField] private RectTransform spinnerRoot;

    [Tooltip("Segmented ring used as the filled loading-progress indicator.")]
    [SerializeField] private GameSceneLoadingProgressRingGraphic progressRing;

    [Tooltip("Segmented ring used as the always-full loading-progress track behind the progress ring.")]
    [SerializeField] private GameSceneLoadingProgressRingGraphic trackRing;

    [Tooltip("Text label shown at the center of the ring when percentage display is enabled.")]
    [SerializeField] private TMP_Text percentageText;

    [Tooltip("Text label shown next to the ring when loading status display is enabled.")]
    [SerializeField] private TMP_Text statusText;

    [Header("Visibility")]
    [Tooltip("When enabled, the Progress Root GameObject is toggled while hidden. The bridge object remains active.")]
    [SerializeField] private bool toggleProgressRoot = true;
    #endregion

    #region Static
    private static GameSceneLoadingProgressCanvasView activeView;
    private static int activeViewVersion;
    #endregion

    #region Runtime
    private bool visible;
    private bool lastShowPercentage;
    private bool lastShowStatusText;
    private float spinnerRotationDegreesPerSecond;
    private int lastPercentage = -1;
    private FixedString128Bytes lastStatusText;
    #endregion

    #endregion

    #region Properties
    public static int ActiveViewVersion
    {
        get
        {
            return activeViewVersion;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies the latest loading-progress state to the active authored view when one exists.
    /// </summary>
    /// <param name="state">Loading-progress presentation state produced by ECS.</param>
    /// <returns>True when a loading-progress view received the state.</returns>
    public static bool TryApply(GameSceneLoadingProgressPresentationState state)
    {
        if (activeView == null)
            return false;

        activeView.Apply(state);
        return true;
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Caches references and registers this authored loading-progress view as the active bridge.
    /// </summary>
    private void OnEnable()
    {
        ResolveReferences();
        activeView = this;
        activeViewVersion++;

        if (!TryApplyCurrentRuntimeState())
            ApplyHidden();
    }

    /// <summary>
    /// Clears the active view reference when this overlay is disabled or unloaded.
    /// </summary>
    private void OnDisable()
    {
        if (activeView == this)
            activeView = null;
    }

    /// <summary>
    /// Rotates the spinner root only while the indicator is visible.
    /// </summary>
    private void Update()
    {
        if (!visible)
            return;

        if (spinnerRoot == null)
            return;

        if (spinnerRotationDegreesPerSecond <= 0f)
            return;

        spinnerRoot.Rotate(0f, 0f, -spinnerRotationDegreesPerSecond * Time.unscaledDeltaTime, Space.Self);
    }
    #endregion

    #region Apply
    /// <summary>
    /// Applies all presentation fields from ECS to authored UI references.
    /// </summary>
    /// <param name="state">Loading-progress presentation state produced by ECS.</param>
    private void Apply(GameSceneLoadingProgressPresentationState state)
    {
        bool nextVisible = state.Visible != 0;
        float progress = Mathf.Clamp01(state.ProgressNormalized);
        Color ringColor = ToColor(state.RingColor);
        Color trackColor = ToColor(state.TrackColor);
        Color textColor = ToColor(state.TextColor);
        spinnerRotationDegreesPerSecond = state.SpinnerRotationDegreesPerSecond;
        ApplyVisibility(nextVisible);
        ApplyRings(progress, state, ringColor, trackColor);
        ApplyPercentage(progress, state.ShowPercentage != 0, textColor);
        ApplyStatus(state.StatusText, state.ShowStatusText != 0, textColor);
    }

    /// <summary>
    /// Applies hidden UI state without requiring an ECS singleton.
    /// </summary>
    private void ApplyHidden()
    {
        ApplyVisibility(false);
        spinnerRotationDegreesPerSecond = 0f;
        lastPercentage = -1;
        lastStatusText = default;
    }

    /// <summary>
    /// Applies visibility to the root and CanvasGroup without disabling this bridge component.
    /// </summary>
    /// <param name="nextVisible">True when loading-progress UI should be visible.</param>
    private void ApplyVisibility(bool nextVisible)
    {
        visible = nextVisible;

        if (progressCanvasGroup != null)
        {
            progressCanvasGroup.alpha = nextVisible ? 1f : 0f;
            progressCanvasGroup.blocksRaycasts = false;
            progressCanvasGroup.interactable = false;
        }

        if (!toggleProgressRoot || progressRoot == null || progressRoot == gameObject)
            return;

        if (progressRoot.activeSelf != nextVisible)
            progressRoot.SetActive(nextVisible);
    }

    /// <summary>
    /// Applies segmented ring visuals and geometry settings.
    /// </summary>
    /// <param name="progress">Normalized loading progress.</param>
    /// <param name="state">Loading-progress presentation state.</param>
    /// <param name="ringColor">Color for the progress fill ring.</param>
    /// <param name="trackColor">Color for the background track ring.</param>
    private void ApplyRings(float progress,
                            GameSceneLoadingProgressPresentationState state,
                            Color ringColor,
                            Color trackColor)
    {
        if (trackRing != null)
        {
            trackRing.SetPresentation(1f,
                                      state.RingSegmentCount,
                                      state.RingSegmentGapDegrees,
                                      state.RingThickness,
                                      trackColor);
            trackRing.enabled = visible;
        }

        if (progressRing == null)
            return;

        progressRing.SetPresentation(progress,
                                     state.RingSegmentCount,
                                     state.RingSegmentGapDegrees,
                                     state.RingThickness,
                                     ringColor);
        progressRing.enabled = visible;
    }

    /// <summary>
    /// Applies the optional centered percentage label.
    /// </summary>
    /// <param name="progress">Normalized loading progress.</param>
    /// <param name="showPercentage">True when the percentage text should be visible.</param>
    /// <param name="textColor">Color applied to the label.</param>
    private void ApplyPercentage(float progress, bool showPercentage, Color textColor)
    {
        if (percentageText == null)
            return;

        bool shouldShow = visible && showPercentage;
        percentageText.enabled = shouldShow;

        if (!shouldShow)
        {
            lastShowPercentage = false;
            return;
        }

        int percentage = Mathf.RoundToInt(progress * 100f);

        if (percentage != lastPercentage || !lastShowPercentage)
        {
            percentageText.text = percentage.ToString("0") + "%";
            lastPercentage = percentage;
        }

        percentageText.color = textColor;
        lastShowPercentage = true;
    }

    /// <summary>
    /// Applies the optional operation status label.
    /// </summary>
    /// <param name="status">Status text produced by ECS.</param>
    /// <param name="showStatusText">True when the status text should be visible.</param>
    /// <param name="textColor">Color applied to the label.</param>
    private void ApplyStatus(FixedString128Bytes status, bool showStatusText, Color textColor)
    {
        if (statusText == null)
            return;

        bool shouldShow = visible && showStatusText;
        statusText.enabled = shouldShow;

        if (!shouldShow)
        {
            lastShowStatusText = false;
            return;
        }

        if (!status.Equals(lastStatusText) || !lastShowStatusText)
        {
            statusText.text = status.ToString();
            lastStatusText = status;
        }

        statusText.color = textColor;
        lastShowStatusText = true;
    }
    #endregion

    #region Runtime State
    /// <summary>
    /// Reads the current ECS loading-progress singleton so enabling the view during a transition preserves state.
    /// </summary>
    /// <returns>True when an ECS loading-progress state was applied.</returns>
    private bool TryApplyCurrentRuntimeState()
    {
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
            return false;

        EntityManager entityManager = world.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameSceneLoadingProgressPresentationState>());
        int entityCount = query.CalculateEntityCount();

        if (entityCount != 1)
        {
            query.Dispose();
            return false;
        }

        Entity entity = query.GetSingletonEntity();
        GameSceneLoadingProgressPresentationState state = entityManager.GetComponentData<GameSceneLoadingProgressPresentationState>(entity);
        query.Dispose();
        Apply(state);
        return true;
    }
    #endregion

    #region Reference Resolution
    /// <summary>
    /// Resolves missing authored references from the local hierarchy without creating UI at runtime.
    /// </summary>
    private void ResolveReferences()
    {
        if (progressRoot == null)
            progressRoot = gameObject;

        if (progressCanvasGroup == null && progressRoot != null)
            progressCanvasGroup = progressRoot.GetComponent<CanvasGroup>();

        if (spinnerRoot == null && progressRoot != null)
            spinnerRoot = progressRoot.GetComponentInChildren<RectTransform>(true);

        if (progressRing == null)
            progressRing = GetComponentInChildren<GameSceneLoadingProgressRingGraphic>(true);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Converts a DOTS float4 color into a UnityEngine Color value.
    /// </summary>
    /// <param name="value">DOTS color value.</param>
    /// <returns>Unity color value.</returns>
    private static Color ToColor(float4 value)
    {
        return new Color(value.x, value.y, value.z, value.w);
    }
    #endregion

    #endregion
}
