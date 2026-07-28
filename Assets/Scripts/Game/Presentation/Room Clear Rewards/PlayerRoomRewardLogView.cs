using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Animates a fixed pool of preauthored world-space rows above the player without runtime UI creation.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerRoomRewardLogView : MonoBehaviour
{
    #region Constants
    public const int PreauthoredRowCapacity = 12;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("World-space canvas containing every preauthored reward row.")]
    [SerializeField]
    private Canvas worldCanvas;

    [Tooltip("Fixed reusable reward rows. Runtime code never creates additional UI objects.")]
    [SerializeField]
    private GameRoomRewardPresentationCellView[] rows =
        Array.Empty<GameRoomRewardPresentationCellView>();

    [Tooltip("Keeps the world-space log facing the active gameplay camera.")]
    [SerializeField]
    private bool faceCamera = true;
    #endregion

    #region Runtime Fields
    private readonly Queue<GameRoomRewardPresentationItem> pendingItems =
        new Queue<GameRoomRewardPresentationItem>(PreauthoredRowCapacity);
    private RowAnimationState[] rowStates = Array.Empty<RowAnimationState>();
    private Transform followTarget;
    private Transform cameraTransform;
    private Vector3 worldOffset;
    private TMP_FontAsset font;
    private float fontSize;
    private float rowSpacing;
    private float enterDuration;
    private float holdDuration;
    private float exitDuration;
    private float scrollDistance;
    private int visibleRows;
    private int queueCapacity = 1;
    #endregion

    #endregion

    #region Properties
    /// <summary>
    /// Gets the lifetime number of authoritative reward entries accepted by this runtime view instance.
    /// </summary>
    public int TotalEnqueuedItems { get; private set; }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns the fixed canvas and row pool during the one-shot editor setup workflow.
    /// </summary>
    /// <param name="resolvedCanvas">Preauthored world-space canvas.</param>
    /// <param name="resolvedRows">Preauthored reusable row pool.</param>
    public void ConfigureAuthoring(Canvas resolvedCanvas,
                                   GameRoomRewardPresentationCellView[] resolvedRows)
    {
        worldCanvas = resolvedCanvas;
        rows = resolvedRows ?? Array.Empty<GameRoomRewardPresentationCellView>();
        EnsureRuntimeState();
        HideAllRows();
    }

    /// <summary>
    /// Applies baked layout and timing values and binds this view to the current player visual root.
    /// </summary>
    /// <param name="resolvedFollowTarget">Runtime player visual transform followed by the log.</param>
    /// <param name="config">Baked room reward presentation config.</param>
    public void ConfigureRuntime(Transform resolvedFollowTarget,
                                 in GameRoomRewardConfig config)
    {
        followTarget = resolvedFollowTarget;
        worldOffset = new Vector3(config.PlayerLogWorldOffset.x,
                                  config.PlayerLogWorldOffset.y,
                                  config.PlayerLogWorldOffset.z);
        font = config.PlayerLogFont.Value;
        fontSize = config.PlayerLogFontSize;
        rowSpacing =
            GameRoomRewardWorldCanvasLayoutUtility.ToLocalVerticalDistance(
                worldCanvas,
                config.PlayerLogRowSpacing);
        visibleRows = rows.Length > 0
            ? Mathf.Clamp(config.PlayerLogVisibleRows, 1, rows.Length)
            : 0;
        queueCapacity = Mathf.Max(1, config.PlayerLogQueueCapacity);
        enterDuration = Mathf.Max(0f, config.PlayerLogEnterDuration);
        holdDuration = Mathf.Max(0f, config.PlayerLogHoldDuration);
        exitDuration = Mathf.Max(0f, config.PlayerLogExitDuration);
        scrollDistance =
            GameRoomRewardWorldCanvasLayoutUtility.ToLocalVerticalDistance(
                worldCanvas,
                config.PlayerLogScrollDistance);

        if (worldCanvas != null)
            worldCanvas.enabled = true;
    }

    /// <summary>
    /// Queues one formatted room reward while preserving the newest bounded feedback.
    /// </summary>
    /// <param name="item">Formatted reward entry to display.</param>
    public void Enqueue(in GameRoomRewardPresentationItem item)
    {
        while (pendingItems.Count >= queueCapacity)
            pendingItems.Dequeue();

        pendingItems.Enqueue(item);
        TotalEnqueuedItems++;
        enabled = true;
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Initializes reusable row state and hides empty rows.
    /// </summary>
    private void Awake()
    {
        EnsureRuntimeState();
        HideAllRows();
    }

    /// <summary>
    /// Advances only active reward rows and keeps the view aligned with the runtime player visual.
    /// </summary>
    private void LateUpdate()
    {
        FollowPlayerAndCamera();
        StartPendingRows();
        AdvanceActiveRows(Time.unscaledDeltaTime);

        if (pendingItems.Count == 0 && CountActiveRows() == 0)
            enabled = false;
    }
    #endregion

    #region Animation
    /// <summary>
    /// Starts queued entries in free preauthored rows up to the configured visible limit.
    /// </summary>
    private void StartPendingRows()
    {
        int activeCount = CountActiveRows();

        if (pendingItems.Count == 0 || activeCount >= visibleRows)
            return;

        for (int rowIndex = 0;
             rowIndex < rows.Length && pendingItems.Count > 0 && activeCount < visibleRows;
             rowIndex++)
        {
            if (rowStates[rowIndex].Active || rows[rowIndex] == null)
                continue;

            GameRoomRewardPresentationItem item = pendingItems.Dequeue();
            rows[rowIndex].Apply(in item, font, fontSize);
            Vector2 preferredTextSize = rows[rowIndex].GetPreferredTextSize();
            rowSpacing = Mathf.Max(rowSpacing,
                                   preferredTextSize.y + Mathf.Max(0.25f,
                                                                   fontSize * 0.15f));
            rows[rowIndex].SetOpacity(enterDuration <= 0f ? 1f : 0f);
            rowStates[rowIndex] = new RowAnimationState
            {
                Active = true,
                Elapsed = 0f
            };
            activeCount++;
        }
    }

    /// <summary>
    /// Advances enter, hold and exit phases for every currently visible row.
    /// </summary>
    /// <param name="deltaTime">Unscaled presentation delta time.</param>
    private void AdvanceActiveRows(float deltaTime)
    {
        float totalDuration = enterDuration + holdDuration + exitDuration;
        int visibleRank = 0;

        for (int rowIndex = 0; rowIndex < rowStates.Length; rowIndex++)
        {
            if (!rowStates[rowIndex].Active)
                continue;

            RowAnimationState state = rowStates[rowIndex];
            state.Elapsed += Mathf.Max(0f, deltaTime);
            float basePosition = visibleRank * rowSpacing;
            float verticalOffset = ResolveVerticalOffset(state.Elapsed);
            float opacity = ResolveOpacity(state.Elapsed);
            rows[rowIndex].SetAnchoredPosition(new Vector2(0f, basePosition + verticalOffset));
            rows[rowIndex].SetOpacity(opacity);
            visibleRank++;

            if (totalDuration > 0f && state.Elapsed < totalDuration)
            {
                rowStates[rowIndex] = state;
                continue;
            }

            state.Active = false;
            state.Elapsed = 0f;
            rowStates[rowIndex] = state;
            rows[rowIndex].SetVisible(false);
        }
    }

    /// <summary>
    /// Resolves row travel for enter and exit phases.
    /// </summary>
    /// <param name="elapsed">Elapsed row lifetime.</param>
    /// <returns>Local vertical offset relative to the row rank.</returns>
    private float ResolveVerticalOffset(float elapsed)
    {
        if (enterDuration > 0f && elapsed < enterDuration)
            return Mathf.Lerp(-scrollDistance, 0f, elapsed / enterDuration);

        float exitStart = enterDuration + holdDuration;

        if (exitDuration > 0f && elapsed > exitStart)
            return Mathf.Lerp(0f,
                              scrollDistance,
                              Mathf.Clamp01((elapsed - exitStart) / exitDuration));

        return 0f;
    }

    /// <summary>
    /// Resolves row opacity for enter and exit phases.
    /// </summary>
    /// <param name="elapsed">Elapsed row lifetime.</param>
    /// <returns>Normalized opacity.</returns>
    private float ResolveOpacity(float elapsed)
    {
        if (enterDuration > 0f && elapsed < enterDuration)
            return Mathf.Clamp01(elapsed / enterDuration);

        float exitStart = enterDuration + holdDuration;

        if (exitDuration > 0f && elapsed > exitStart)
            return 1f - Mathf.Clamp01((elapsed - exitStart) / exitDuration);

        return 1f;
    }
    #endregion

    #region View State
    /// <summary>
    /// Updates world position and camera-facing rotation from cached runtime transforms.
    /// </summary>
    private void FollowPlayerAndCamera()
    {
        if (followTarget != null)
            transform.position = followTarget.position + worldOffset;

        if (!faceCamera)
            return;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform == null)
            return;

        Vector3 cameraDirection = transform.position - cameraTransform.position;

        if (cameraDirection.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(cameraDirection.normalized, Vector3.up);
    }

    /// <summary>
    /// Ensures the runtime state array matches the preauthored row count.
    /// </summary>
    private void EnsureRuntimeState()
    {
        int rowCount = rows != null ? rows.Length : 0;

        if (rowStates.Length != rowCount)
            rowStates = new RowAnimationState[rowCount];
    }

    /// <summary>
    /// Hides every reusable row and clears its animation state.
    /// </summary>
    private void HideAllRows()
    {
        EnsureRuntimeState();

        for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            if (rows[rowIndex] != null)
                rows[rowIndex].SetVisible(false);

            rowStates[rowIndex] = default;
        }
    }

    /// <summary>
    /// Counts currently animating rows.
    /// </summary>
    /// <returns>Number of active preauthored rows.</returns>
    private int CountActiveRows()
    {
        int activeCount = 0;

        for (int rowIndex = 0; rowIndex < rowStates.Length; rowIndex++)
        {
            if (rowStates[rowIndex].Active)
                activeCount++;
        }

        return activeCount;
    }
    #endregion

    #endregion

    #region Nested Types
    private struct RowAnimationState
    {
        public bool Active;
        public float Elapsed;
    }
    #endregion
}
