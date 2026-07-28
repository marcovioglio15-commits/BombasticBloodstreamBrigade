using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Scrolls destination reward summaries through a fixed pool of preauthored world-space portal cells.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameRoomPortalRewardLogView : MonoBehaviour
{
    #region Constants
    public const int PreauthoredCellCapacity = 12;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("World-space canvas containing the fixed portal Log cells.")]
    [SerializeField]
    private Canvas worldCanvas;

    [Tooltip("Fixed reusable Log cells. Runtime code never creates additional UI objects.")]
    [SerializeField]
    private GameRoomRewardPresentationCellView[] cells =
        Array.Empty<GameRoomRewardPresentationCellView>();

    [Tooltip("Keeps the portal Log facing the active gameplay camera.")]
    [SerializeField]
    private bool faceCamera = true;
    #endregion

    #region Runtime Fields
    private readonly List<GameRoomRewardPresentationItem> items =
        new List<GameRoomRewardPresentationItem>(PreauthoredCellCapacity);
    private Transform cameraTransform;
    private Vector3 worldPosition;
    private TMP_FontAsset font;
    private float fontSize;
    private float cellSpacing;
    private float scrollSpeed;
    private float pauseRemaining;
    private float loopPause;
    private int activeCellCount;
    private int nextItemIndex;
    private int signature = int.MinValue;
    #endregion

    #endregion

    #region Properties
    /// <summary>
    /// Gets whether this Log currently owns rebuilt content on an enabled world-space canvas.
    /// </summary>
    public bool HasVisibleContent =>
        signature != int.MinValue &&
        activeCellCount > 0 &&
        worldCanvas != null &&
        worldCanvas.enabled;

    /// <summary>
    /// Gets the graph and portal assignment signature currently presented by this view.
    /// </summary>
    public int Signature => signature;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns the fixed world-space canvas and reusable cell pool during editor setup.
    /// </summary>
    /// <param name="resolvedCanvas">Preauthored world-space canvas.</param>
    /// <param name="resolvedCells">Preauthored reusable Log cells.</param>
    public void ConfigureAuthoring(Canvas resolvedCanvas,
                                   GameRoomRewardPresentationCellView[] resolvedCells)
    {
        worldCanvas = resolvedCanvas;
        cells = resolvedCells ?? Array.Empty<GameRoomRewardPresentationCellView>();
        Hide();
    }

    /// <summary>
    /// Returns whether this view needs content rebuilt for a new portal assignment signature.
    /// </summary>
    /// <param name="candidateSignature">Generation and edge-derived signature.</param>
    /// <returns>True when the view has not yet consumed the signature.</returns>
    public bool NeedsRebuild(int candidateSignature)
    {
        return signature != candidateSignature;
    }

    /// <summary>
    /// Rebuilds the allocation-free scrolling cell state from formatted destination rewards.
    /// </summary>
    /// <param name="candidateSignature">Generation and edge-derived signature owned by this content.</param>
    /// <param name="sourceItems">All formatted destination reward descriptors.</param>
    /// <param name="portalCenter">World-space portal center used as the Log anchor.</param>
    /// <param name="config">Baked portal presentation settings.</param>
    public void Rebuild(int candidateSignature,
                        IReadOnlyList<GameRoomRewardPresentationItem> sourceItems,
                        Vector3 portalCenter,
                        in GameRoomRewardConfig config)
    {
        signature = candidateSignature;
        items.Clear();

        for (int itemIndex = 0; itemIndex < sourceItems.Count; itemIndex++)
            items.Add(sourceItems[itemIndex]);

        worldPosition = portalCenter + new Vector3(config.PortalWorldOffset.x,
                                                   config.PortalWorldOffset.y,
                                                   config.PortalWorldOffset.z);
        font = config.PortalFont.Value;
        fontSize = config.PortalFontSize;
        cellSpacing = Mathf.Max(
            0.01f,
            GameRoomRewardWorldCanvasLayoutUtility.ToLocalHorizontalDistance(
                worldCanvas,
                config.PortalCellSpacing));
        scrollSpeed =
            GameRoomRewardWorldCanvasLayoutUtility.ToLocalHorizontalDistance(
                worldCanvas,
                config.PortalScrollSpeed);
        pauseRemaining = Mathf.Max(0f, config.PortalInitialPause);
        loopPause = Mathf.Max(0f, config.PortalLoopPause);
        int desiredCellCount = Mathf.Max(1, config.PortalVisibleCells + 1);
        activeCellCount = Mathf.Min(cells.Length, Mathf.Min(items.Count, desiredCellCount));
        nextItemIndex = activeCellCount % Mathf.Max(1, items.Count);
        transform.position = worldPosition;

        for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
        {
            GameRoomRewardPresentationCellView cell = cells[cellIndex];

            if (cell == null)
                continue;

            bool shouldShow = cellIndex < activeCellCount;
            cell.SetVisible(shouldShow);

            if (!shouldShow)
                continue;

            GameRoomRewardPresentationItem item = items[cellIndex];
            cell.Apply(in item, font, fontSize);
            Vector2 preferredTextSize = cell.GetPreferredTextSize();
            cellSpacing = Mathf.Max(cellSpacing,
                                    preferredTextSize.x + Mathf.Max(0.5f,
                                                                   fontSize * 0.25f));
            cell.SetOpacity(1f);
        }

        // Position only after resolving the widest active label so neighboring cells cannot overlap.
        for (int cellIndex = 0; cellIndex < activeCellCount; cellIndex++)
        {
            if (cells[cellIndex] != null)
                cells[cellIndex].SetAnchoredPosition(new Vector2(cellIndex * cellSpacing, 0f));
        }

        if (worldCanvas != null)
            worldCanvas.enabled = activeCellCount > 0;

        enabled = activeCellCount > 0;
    }

    /// <summary>
    /// Hides all preauthored cells and invalidates the current presentation signature.
    /// </summary>
    public void Hide()
    {
        for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
        {
            if (cells[cellIndex] != null)
                cells[cellIndex].SetVisible(false);
        }

        if (worldCanvas != null)
            worldCanvas.enabled = false;

        items.Clear();
        activeCellCount = 0;
        signature = int.MinValue;
        enabled = false;
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Starts hidden until ECS assigns a destination with configured rewards.
    /// </summary>
    private void Awake()
    {
        Hide();
    }

    /// <summary>
    /// Advances the horizontal pharmacy-sign loop and camera-facing pose only while content is visible.
    /// </summary>
    private void LateUpdate()
    {
        transform.position = worldPosition;
        FaceCamera();

        if (activeCellCount <= 1 || scrollSpeed <= 0f)
            return;

        float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);

        if (pauseRemaining > 0f)
        {
            pauseRemaining = Mathf.Max(0f, pauseRemaining - deltaTime);
            return;
        }

        AdvanceCells(deltaTime);
    }
    #endregion

    #region Scrolling
    /// <summary>
    /// Moves active cells left and recycles each wrapped cell with the next logical descriptor.
    /// </summary>
    /// <param name="deltaTime">Unscaled presentation delta time.</param>
    private void AdvanceCells(float deltaTime)
    {
        float travel = scrollSpeed * deltaTime;
        float loopWidth = activeCellCount * cellSpacing;

        for (int cellIndex = 0; cellIndex < activeCellCount; cellIndex++)
        {
            GameRoomRewardPresentationCellView cell = cells[cellIndex];

            if (cell == null || cell.CellTransform == null)
                continue;

            Vector2 position = cell.CellTransform.anchoredPosition;
            position.x -= travel;

            while (position.x <= -cellSpacing)
            {
                position.x += loopWidth;
                GameRoomRewardPresentationItem item = items[nextItemIndex];
                cell.Apply(in item, font, fontSize);
                nextItemIndex++;

                if (nextItemIndex >= items.Count)
                {
                    nextItemIndex = 0;
                    pauseRemaining = loopPause;
                }
            }

            cell.SetAnchoredPosition(position);
        }
    }

    /// <summary>
    /// Rotates the Log toward the active camera using a cached transform.
    /// </summary>
    private void FaceCamera()
    {
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
    #endregion

    #endregion
}
