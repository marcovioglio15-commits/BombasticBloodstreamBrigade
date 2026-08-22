using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents destination rewards through scrolling or fully static preauthored world-space cells.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameRoomPortalRewardLogView : MonoBehaviour
{
    #region Constants
    public const int PreauthoredCellCapacity = 12;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("World-space canvas containing the fixed portal log cells.")]
    [SerializeField]
    private Canvas worldCanvas;

    [Tooltip("Fixed reusable log cells. Runtime code never creates additional UI objects.")]
    [SerializeField]
    private GameRoomRewardPresentationCellView[] cells =
        Array.Empty<GameRoomRewardPresentationCellView>();

    [Tooltip("Preauthored image resized and styled behind rewards only in Static Rows mode.")]
    [SerializeField]
    private Image backgroundPanel;

    [Tooltip("Keeps only the Scrolling layout facing the active gameplay camera; Static Rows always preserves its authored rotation.")]
    [SerializeField]
    private bool faceCamera = true;
    #endregion

    #region Runtime Fields
    private readonly List<GameRoomRewardPresentationItem> items =
        new List<GameRoomRewardPresentationItem>(PreauthoredCellCapacity);
    private RectTransform canvasTransform;
    private Transform cameraTransform;
    private Vector3 authoredLocalPosition;
    private Quaternion authoredLocalRotation;
    private Vector2 authoredCanvasSize;
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
    private GameRoomRewardPortalLogLayoutMode layoutMode;
    #endregion

    #endregion

    #region Properties
    /// <summary>
    /// Gets whether this log currently owns rebuilt content on an enabled world-space canvas.
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
    /// <param name="resolvedCells">Preauthored reusable log cells.</param>
    /// <param name="resolvedBackgroundPanel">Preauthored adaptive background image.</param>
    public void ConfigureAuthoring(Canvas resolvedCanvas,
                                   GameRoomRewardPresentationCellView[] resolvedCells,
                                   Image resolvedBackgroundPanel)
    {
        worldCanvas = resolvedCanvas;
        cells = resolvedCells ?? Array.Empty<GameRoomRewardPresentationCellView>();
        backgroundPanel = resolvedBackgroundPanel;
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
    /// <param name="portalCenter">World-space portal center used as the log anchor.</param>
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

        font = config.PortalFont.Value;
        fontSize = config.PortalFontSize;
        layoutMode = config.PortalLayoutMode;

        if (worldCanvas == null || cells.Length == 0 || items.Count == 0)
        {
            Hide();
            return;
        }

        switch (layoutMode)
        {
            case GameRoomRewardPortalLogLayoutMode.StaticRows:
                RebuildStaticRows(in config);
                break;
            default:
                RebuildScrolling(portalCenter, in config);
                break;
        }

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

        if (backgroundPanel != null)
            backgroundPanel.enabled = false;

        if (worldCanvas != null)
            worldCanvas.enabled = false;

        items.Clear();
        activeCellCount = 0;
        signature = int.MinValue;
        enabled = false;
    }
    #endregion

    #region Layout
    /// <summary>
    /// Rebuilds the existing horizontal recycling layout at a portal-relative world position.
    /// </summary>
    /// <param name="portalCenter">Authoritative ECS portal center.</param>
    /// <param name="config">Baked scrolling layout settings.</param>
    private void RebuildScrolling(Vector3 portalCenter, in GameRoomRewardConfig config)
    {
        worldPosition = portalCenter + new Vector3(config.PortalWorldOffset.x,
                                                   config.PortalWorldOffset.y,
                                                   config.PortalWorldOffset.z);
        transform.position = worldPosition;

        if (canvasTransform != null)
            canvasTransform.sizeDelta = authoredCanvasSize;

        if (backgroundPanel != null)
            backgroundPanel.enabled = false;

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
    }

    /// <summary>
    /// Rebuilds one reward per row and resizes the preauthored background to the resulting content.
    /// </summary>
    /// <param name="config">Baked Static Rows layout and panel settings.</param>
    private void RebuildStaticRows(in GameRoomRewardConfig config)
    {
        transform.localPosition = authoredLocalPosition;
        transform.localRotation = authoredLocalRotation;
        activeCellCount = Mathf.Min(cells.Length, items.Count);
        scrollSpeed = 0f;
        pauseRemaining = 0f;
        float maximumContentWidth = 0f;
        float rowHeight = Mathf.Max(0.1f, fontSize * 1.25f);

        // Apply all rows before measuring so font and sprite mappings contribute to the adaptive panel size.
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
            Vector2 preferredSize = cell.GetPreferredContentSize();
            maximumContentWidth = Mathf.Max(maximumContentWidth, preferredSize.x);
            rowHeight = Mathf.Max(rowHeight, preferredSize.y);
            cell.SetOpacity(1f);
        }

        Vector2 padding = new Vector2(config.PortalStaticPanelPadding.x,
                                      config.PortalStaticPanelPadding.y);
        float rowSpacing = Mathf.Max(0f, config.PortalStaticRowSpacing);
        float contentHeight = activeCellCount * rowHeight +
                              Mathf.Max(0, activeCellCount - 1) * rowSpacing;
        Vector2 panelSize = new Vector2(
            Mathf.Max(config.PortalStaticMinimumPanelSize.x,
                      maximumContentWidth + padding.x * 2f),
            Mathf.Max(config.PortalStaticMinimumPanelSize.y,
                      contentHeight + padding.y * 2f));

        if (canvasTransform != null)
            canvasTransform.sizeDelta = panelSize;

        if (backgroundPanel != null)
        {
            backgroundPanel.sprite = config.PortalStaticBackgroundSprite.Value;
            backgroundPanel.color = new Color(config.PortalStaticBackgroundColor.x,
                                              config.PortalStaticBackgroundColor.y,
                                              config.PortalStaticBackgroundColor.z,
                                              config.PortalStaticBackgroundColor.w);
            backgroundPanel.enabled = true;
        }

        float firstRowPosition = (activeCellCount - 1) * (rowHeight + rowSpacing) * 0.5f;
        Vector2 cellSize = new Vector2(Mathf.Max(0.1f, panelSize.x - padding.x * 2f),
                                       rowHeight);

        // Center authored rows vertically while preserving one reusable object per reward.
        for (int cellIndex = 0; cellIndex < activeCellCount; cellIndex++)
        {
            GameRoomRewardPresentationCellView cell = cells[cellIndex];

            if (cell == null)
                continue;

            cell.SetSize(cellSize);
            cell.SetAnchoredPosition(new Vector2(0f,
                                                 firstRowPosition -
                                                 cellIndex * (rowHeight + rowSpacing)));
        }
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Starts hidden until ECS assigns a destination with configured rewards.
    /// </summary>
    private void Awake()
    {
        canvasTransform = worldCanvas != null ? worldCanvas.transform as RectTransform : null;
        authoredLocalPosition = transform.localPosition;
        authoredLocalRotation = transform.localRotation;
        authoredCanvasSize = canvasTransform != null ? canvasTransform.sizeDelta : Vector2.zero;
        Hide();
    }

    /// <summary>
    /// Advances scrolling placement and billboard rotation only for the scrolling layout.
    /// </summary>
    private void LateUpdate()
    {
        if (layoutMode != GameRoomRewardPortalLogLayoutMode.Scrolling)
            return;

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
    /// Rotates the log toward the active camera using a cached transform.
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
