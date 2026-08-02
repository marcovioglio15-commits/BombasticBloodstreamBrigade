using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Authoring component that defines a finite wave-based enemy spawn grid.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemySpawnerAuthoring : MonoBehaviour
{
    #region Fields

#if UNITY_EDITOR
    private static GUIStyle sceneCoordinateLabelStyle;
    private static GUIStyle sceneCountLabelStyle;
#endif

    #region Serialized Fields
    [Header("Activation")]
    [Tooltip("Default runtime-enabled state for this spawner. Keep the GameObject active so DOTS baking can create the spawner entity; the main-menu spawner tool can override this value before the owning subscene loads.")]
    [SerializeField] private bool spawnerEnabled = true;

    [Header("Grid")]
    [Tooltip("Grid width in cells on the local X axis.")]
    [SerializeField] private int gridSizeX = 12;

    [Tooltip("Grid depth in cells on the local Z axis.")]
    [SerializeField] private int gridSizeZ = 12;

    [Tooltip("Square cell size in local units.")]
    [SerializeField] private float cellSize = 2f;

    [Tooltip("Local-space offset applied to the full grid origin before cell placement.")]
    [SerializeField] private Vector3 originOffset;

    [Tooltip("Additional local-space height offset applied to all baked spawn positions.")]
    [SerializeField] private float spawnHeightOffset;

    [Header("Pool")]
    [Tooltip("Amount of enemies prewarmed for each unique prefab referenced by this spawner.")]
    [SerializeField] private int initialPoolCapacityPerPrefab = 16;

    [Tooltip("Amount of enemies instantiated whenever a prefab-specific pool needs expansion.")]
    [SerializeField] private int expandBatchPerPrefab = 8;

    [Header("Lifecycle")]
    [Tooltip("Distance from the player beyond which alive enemies are returned to their pool. Set to 0 to disable.")]
    [SerializeField] private float despawnDistance = 85f;

    [Header("Spawn Warning")]
    [Tooltip("When enabled, upcoming spawn events project one warning ring on the ground shortly before enemy activation.")]
    [SerializeField] private bool enableSpawnWarning = true;

    [Tooltip("Seconds of anticipation shown before one spawn event becomes active.")]
    [Range(0f, 3f)]
    [SerializeField] private float spawnWarningLeadTimeSeconds = 0.7f;

    [Tooltip("Ring world radius resolved as Cell Size multiplied by this scale.")]
    [Range(0.1f, 2f)]
    [SerializeField] private float spawnWarningRadiusScale = 0.45f;

    [Tooltip("World-space line width used by the spawn warning ring.")]
    [Range(0.02f, 1f)]
    [SerializeField] private float spawnWarningRingWidth = 0.15f;

    [Tooltip("Extra vertical lift applied to the warning ring above the spawn plane.")]
    [Range(0f, 1f)]
    [SerializeField] private float spawnWarningHeightOffset = 0.06f;

    [Tooltip("Maximum opacity reached by the warning ring right before the spawn happens.")]
    [Range(0f, 1f)]
    [SerializeField] private float spawnWarningMaximumAlpha = 0.95f;

    [Tooltip("Seconds used to softly fade the ring after the enemy has spawned.")]
    [Range(0f, 1f)]
    [SerializeField] private float spawnWarningFadeOutSeconds = 0.18f;

    [Tooltip("Tint color used by the spawn warning ring.")]
    [SerializeField] private Color spawnWarningColor = new Color(1f, 0.72f, 0.18f, 1f);

    [Header("Waves")]
    [Tooltip("Required wave preset asset containing every sequential or parallel lane emitted by this room's single spawner.")]
    [SerializeField] private EnemyWavePreset wavePreset;

    [Header("Debug")]
    [Tooltip("Draw the authored grid and preview wave gizmos when the spawner is selected.")]
    [SerializeField] private bool drawGridGizmos = true;

    [Tooltip("Draw grid coordinates beside painted preview cells.")]
    [SerializeField] private bool drawCellCoordinates = true;

    [Tooltip("Draw authored enemy counts beside painted preview cells.")]
    [SerializeField] private bool drawCellCounts = true;
    #endregion

    #endregion

    #region Properties
    public bool SpawnerEnabled
    {
        get
        {
            return spawnerEnabled;
        }
    }

    /// <summary>
    /// Resolves the effective default state used by baking and the main-menu spawner tool.
    /// </summary>
    public bool RuntimeEnabledByDefault
    {
        get
        {
            return spawnerEnabled;
        }
    }

    public int GridSizeX
    {
        get
        {
            return gridSizeX;
        }
    }

    public int GridSizeZ
    {
        get
        {
            return gridSizeZ;
        }
    }

    public float CellSize
    {
        get
        {
            return cellSize;
        }
    }

    public Vector3 OriginOffset
    {
        get
        {
            return originOffset;
        }
    }

    public float SpawnHeightOffset
    {
        get
        {
            return spawnHeightOffset;
        }
    }

    public int InitialPoolCapacityPerPrefab
    {
        get
        {
            return initialPoolCapacityPerPrefab;
        }
    }

    public int ExpandBatchPerPrefab
    {
        get
        {
            return expandBatchPerPrefab;
        }
    }

    public float DespawnDistance
    {
        get
        {
            return despawnDistance;
        }
    }

    public bool EnableSpawnWarning
    {
        get
        {
            return enableSpawnWarning;
        }
    }

    public float SpawnWarningLeadTimeSeconds
    {
        get
        {
            return spawnWarningLeadTimeSeconds;
        }
    }

    public float SpawnWarningRadiusScale
    {
        get
        {
            return spawnWarningRadiusScale;
        }
    }

    public float SpawnWarningRingWidth
    {
        get
        {
            return spawnWarningRingWidth;
        }
    }

    public float SpawnWarningHeightOffset
    {
        get
        {
            return spawnWarningHeightOffset;
        }
    }

    public float SpawnWarningMaximumAlpha
    {
        get
        {
            return spawnWarningMaximumAlpha;
        }
    }

    public float SpawnWarningFadeOutSeconds
    {
        get
        {
            return spawnWarningFadeOutSeconds;
        }
    }

    public Color SpawnWarningColor
    {
        get
        {
            return spawnWarningColor;
        }
    }

    public EnemyWavePreset WavePreset
    {
        get
        {
            return wavePreset;
        }
    }

    public System.Collections.Generic.List<EnemySpawnWaveAuthoring> Waves
    {
        get
        {
            return wavePreset == null ? null : wavePreset.Waves;
        }
    }

    public bool DrawGridGizmos
    {
        get
        {
            return drawGridGizmos;
        }
    }

    public bool DrawCellCoordinates
    {
        get
        {
            return drawCellCoordinates;
        }
    }

    public bool DrawCellCounts
    {
        get
        {
            return drawCellCounts;
        }
    }
    #endregion

    #region Const
    private const float fillAlpha = 0.95f;
    private const float cellSizePaddingHorizontal = .82f;
    private const float cellSizePaddingVertical = 0.04f;
    private const float cellSizePaddingVertical_Wired = 0.08f;
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Reports invalid spawner values without silently rewriting designer-authored tuning.
    /// </summary>
    private void OnValidate()
    {
        EnemySpawnerAuthoringValidationUtility.WarnInvalidValues(this);

        if (wavePreset == null)
            return;

        wavePreset.ValidateAgainstGrid(gridSizeX, gridSizeZ);
    }

    /// <summary>
    /// Draws selected-scene gizmos for the grid and currently previewed wave.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!drawGridGizmos)
            return;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        DrawGridGizmoLines();
        DrawPreviewWaveGizmos();
#if UNITY_EDITOR
        DrawSceneOverlayLabels();
#endif
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Resolves the local-space center of one authored grid cell.
    /// Used by the baker and by scene preview drawing.
    /// </summary>
    /// <param name="cellCoordinate">Authored grid coordinate to resolve.</param>
    /// <returns>Local-space center of the requested cell.</returns>
    public float3 ResolveCellLocalCenter(Vector2Int cellCoordinate)
    {
        return EnemySpawnerWaveBakeUtility.ResolveCellLocalCenter(gridSizeX,
                                                                  gridSizeZ,
                                                                  cellSize,
                                                                  originOffset,
                                                                  spawnHeightOffset,
                                                                  cellCoordinate);
    }

    /// <summary>
    /// Tries to resolve the single wave currently flagged for scene preview.
    /// Used by gizmos and by the custom editor.
    /// </summary>
    /// <param name="waveIndex">Resolved preview wave index, or -1 when none is active.</param>
    /// <returns>True when a preview wave exists, otherwise false.</returns>
    public bool TryGetPreviewWaveIndex(out int waveIndex)
    {
        System.Collections.Generic.List<EnemySpawnWaveAuthoring> resolvedWaves = Waves;

        if (resolvedWaves != null)
        {
            for (int index = 0; index < resolvedWaves.Count; index++)
            {
                EnemySpawnWaveAuthoring wave = resolvedWaves[index];

                if (wave == null)
                    continue;

                if (!wave.PreviewInScene)
                    continue;

                waveIndex = index;
                return true;
            }
        }

        waveIndex = -1;
        return false;
    }
    #endregion

    #region Gizmos
    /// <summary>
    /// Draws the grid wireframe in local space.
    /// </summary>
    private void DrawGridGizmoLines()
    {
        Gizmos.color = spawnerEnabled
            ? new Color(0.35f, 0.65f, 1f, 0.45f)
            : new Color(1f, 0.24f, 0.22f, 0.42f);

        for (int x = 0; x <= gridSizeX; x++)
        {
            float offsetX = (x - gridSizeX * 0.5f) * cellSize;
            Vector3 start = originOffset + new Vector3(offsetX, spawnHeightOffset, -gridSizeZ * 0.5f * cellSize);
            Vector3 end = originOffset + new Vector3(offsetX, spawnHeightOffset, gridSizeZ * 0.5f * cellSize);
            Gizmos.DrawLine(start, end);
        }

        for (int z = 0; z <= gridSizeZ; z++)
        {
            float offsetZ = (z - gridSizeZ * 0.5f) * cellSize;
            Vector3 start = originOffset + new Vector3(-gridSizeX * 0.5f * cellSize, spawnHeightOffset, offsetZ);
            Vector3 end = originOffset + new Vector3(gridSizeX * 0.5f * cellSize, spawnHeightOffset, offsetZ);
            Gizmos.DrawLine(start, end);
        }
    }

    /// <summary>
    /// Draws painted preview cells for the currently selected wave.
    /// </summary>
    private void DrawPreviewWaveGizmos()
    {
        if (!spawnerEnabled)
            return;

        System.Collections.Generic.List<EnemySpawnWaveAuthoring> resolvedWaves = Waves;
        int previewWaveIndex;

        if (!TryGetPreviewWaveIndex(out previewWaveIndex))
            return;

        if (resolvedWaves == null)
            return;

        if (previewWaveIndex < 0 || previewWaveIndex >= resolvedWaves.Count)
            return;

        EnemySpawnWaveAuthoring previewWave = resolvedWaves[previewWaveIndex];

        if (previewWave == null || previewWave.PaintedCells == null)
            return;

        for (int cellIndex = 0; cellIndex < previewWave.PaintedCells.Count; cellIndex++)
        {
            EnemySpawnWaveCellAuthoring cell = previewWave.PaintedCells[cellIndex];

            if (cell == null)
                continue;

            float3 localCenterValue = ResolveCellLocalCenter(cell.CellCoordinate);
            Vector3 localCenter = new Vector3(localCenterValue.x, localCenterValue.y, localCenterValue.z);
            Color fillColor = ResolveCellPaintColor(cell);
            fillColor.a = 0.35f;
            Gizmos.color = fillColor;
            Gizmos.DrawCube(localCenter, new Vector3(cellSize * cellSizePaddingHorizontal, cellSizePaddingVertical, cellSize * cellSizePaddingHorizontal));
            Gizmos.color = new Color(fillColor.r, fillColor.g, fillColor.b, fillAlpha);
            Gizmos.DrawWireCube(localCenter, new Vector3(cellSize * cellSizePaddingHorizontal, cellSizePaddingVertical_Wired, cellSize * cellSizePaddingHorizontal));
        }
    }

    /// <summary>
    /// Resolves the category-aware color used by scene preview gizmos.
    /// </summary>
    /// <param name="cell">Painted cell whose brush identity is rendered.</param>
    /// <returns>Authored category color or a clear unresolved-category warning color.</returns>
    private Color ResolveCellPaintColor(EnemySpawnWaveCellAuthoring cell)
    {
        GameWavesPreset wavesPreset = wavePreset != null ? wavePreset.WavesPreset : null;

        if (wavesPreset != null &&
            wavesPreset.TryFindBrushCategory(cell.BrushCategoryId, out EnemyBrushCategoryDefinition category))
        {
            return category.BrushColor;
        }

        return new Color(1f, 0.2f, 0.2f, 0.9f);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Draws screen-space overlays for grid coordinates and painted-cell counts.
    /// </summary>
    private void DrawSceneOverlayLabels()
    {
        if (!drawCellCoordinates && !drawCellCounts)
            return;

        Handles.BeginGUI();

        if (drawCellCoordinates)
        {
            DrawGridCoordinateLabels();
        }

        if (drawCellCounts)
        {
            DrawPreviewCellCountLabels();
        }

        Handles.EndGUI();
    }

    /// <summary>
    /// Draws the coordinate label of every authored grid node while the spawner is selected.
    /// </summary>
    private void DrawGridCoordinateLabels()
    {
        GUIStyle coordinateStyle = GetSceneCoordinateLabelStyle();

        for (int z = gridSizeZ - 1; z >= 0; z--)
        {
            for (int x = 0; x < gridSizeX; x++)
            {
                Vector2Int coordinate = new Vector2Int(x, z);
                float3 localCenterValue = ResolveCellLocalCenter(coordinate);
                Vector3 localCenter = new Vector3(localCenterValue.x, localCenterValue.y, localCenterValue.z);
                Vector3 worldCenter = transform.TransformPoint(localCenter);
                Vector3 screenPoint = HandleUtility.WorldToGUIPointWithDepth(worldCenter);

                if (screenPoint.z < 0f)
                    continue;

                DrawSceneBadge(new Vector2(screenPoint.x, screenPoint.y + 16f),
                               "[" + x + "," + z + "]",
                               coordinateStyle,
                               new Color(0.08f, 0.13f, 0.2f, 0.88f),
                               new Color(0.4f, 0.72f, 1f, 0.92f),
                               54f,
                               20f);
            }
        }
    }

    /// <summary>
    /// Draws the optional enemy-count label for every painted preview cell.
    /// </summary>
    private void DrawPreviewCellCountLabels()
    {
        System.Collections.Generic.List<EnemySpawnWaveAuthoring> resolvedWaves = Waves;
        int previewWaveIndex;

        if (!TryGetPreviewWaveIndex(out previewWaveIndex))
            return;

        if (resolvedWaves == null)
            return;

        if (previewWaveIndex < 0 || previewWaveIndex >= resolvedWaves.Count)
            return;

        EnemySpawnWaveAuthoring previewWave = resolvedWaves[previewWaveIndex];

        if (previewWave == null || previewWave.PaintedCells == null)
            return;

        GUIStyle countStyle = GetSceneCountLabelStyle();

        for (int cellIndex = 0; cellIndex < previewWave.PaintedCells.Count; cellIndex++)
        {
            EnemySpawnWaveCellAuthoring cell = previewWave.PaintedCells[cellIndex];

            if (cell == null)
                continue;

            float3 localCenterValue = ResolveCellLocalCenter(cell.CellCoordinate);
            Vector3 localCenter = new Vector3(localCenterValue.x, localCenterValue.y, localCenterValue.z);
            Vector3 worldCenter = transform.TransformPoint(localCenter);
            Vector3 screenPoint = HandleUtility.WorldToGUIPointWithDepth(worldCenter);

            if (screenPoint.z < 0f)
                continue;

            Color badgeColor = ResolveCellPaintColor(cell);
            badgeColor.a = 0.92f;
            DrawSceneBadge(new Vector2(screenPoint.x, screenPoint.y - 20f),
                           "x" + math.max(0, cell.EnemyCount),
                           countStyle,
                           badgeColor,
                           new Color(1f, 1f, 1f, 0.92f),
                           40f,
                           20f);
        }
    }

    /// <summary>
    /// Draws one centered screen-space badge used by scene overlays.
    /// </summary>
    /// <param name="screenCenter">GUI-space center of the badge.</param>
    /// <param name="label">Text displayed inside the badge.</param>
    /// <param name="style">GUI style used to draw the text.</param>
    /// <param name="backgroundColor">Fill color of the badge.</param>
    /// <param name="borderColor">Outline color of the badge.</param>
    /// <param name="minWidth">Minimum badge width in pixels.</param>
    /// <param name="height">Badge height in pixels.</param>
    private static void DrawSceneBadge(Vector2 screenCenter,
                                       string label,
                                       GUIStyle style,
                                       Color backgroundColor,
                                       Color borderColor,
                                       float minWidth,
                                       float height)
    {
        if (string.IsNullOrEmpty(label))
            return;

        GUIContent badgeContent = new GUIContent(label);
        Vector2 textSize = style.CalcSize(badgeContent);
        float width = Mathf.Max(minWidth, textSize.x + 10f);
        Rect badgeRect = new Rect(screenCenter.x - width * 0.5f,
                                  screenCenter.y - height * 0.5f,
                                  width,
                                  height);
        EditorGUI.DrawRect(badgeRect, backgroundColor);
        EditorGUI.DrawRect(new Rect(badgeRect.xMin, badgeRect.yMin, badgeRect.width, 1f), borderColor);
        EditorGUI.DrawRect(new Rect(badgeRect.xMin, badgeRect.yMax - 1f, badgeRect.width, 1f), borderColor);
        EditorGUI.DrawRect(new Rect(badgeRect.xMin, badgeRect.yMin, 1f, badgeRect.height), borderColor);
        EditorGUI.DrawRect(new Rect(badgeRect.xMax - 1f, badgeRect.yMin, 1f, badgeRect.height), borderColor);
        GUI.Label(badgeRect, badgeContent, style);
    }

    /// <summary>
    /// Returns the cached style used for scene coordinate overlays.
    /// </summary>
    /// <returns>GUI style used by grid coordinate labels.</returns>
    private static GUIStyle GetSceneCoordinateLabelStyle()
    {
        if (sceneCoordinateLabelStyle != null)
            return sceneCoordinateLabelStyle;

        sceneCoordinateLabelStyle = new GUIStyle(EditorStyles.whiteMiniLabel);
        sceneCoordinateLabelStyle.fontSize = 12;
        sceneCoordinateLabelStyle.fontStyle = FontStyle.Bold;
        sceneCoordinateLabelStyle.normal.textColor = new Color(0.95f, 0.98f, 1f, 0.98f);
        sceneCoordinateLabelStyle.alignment = TextAnchor.MiddleCenter;
        return sceneCoordinateLabelStyle;
    }

    /// <summary>
    /// Returns the cached style used for painted-cell enemy-count overlays.
    /// </summary>
    /// <returns>GUI style used by painted-cell count labels.</returns>
    private static GUIStyle GetSceneCountLabelStyle()
    {
        if (sceneCountLabelStyle != null)
            return sceneCountLabelStyle;

        sceneCountLabelStyle = new GUIStyle(EditorStyles.whiteMiniLabel);
        sceneCountLabelStyle.fontSize = 13;
        sceneCountLabelStyle.fontStyle = FontStyle.Bold;
        sceneCountLabelStyle.normal.textColor = Color.white;
        sceneCountLabelStyle.alignment = TextAnchor.MiddleCenter;
        return sceneCountLabelStyle;
    }
#endif
    #endregion

    #endregion
}
