using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Renders isolated managed-scene and SubScene content and paints wave cells through an embedded preview camera.
/// </summary>
internal sealed class GameWavesPreviewRenderer : IDisposable
{
    #region Constants
    private const float GridPaddingScale = 1.08f;
    private const float CameraClearance = 24f;
    private const float CellInsetScale = 0.47f;
    #endregion

    #region Fields
    private PreviewRenderUtility previewUtility;
    private EnemyWavePreset previewWavePreset;
    private SerializedObject previewWavePresetObject;
    private readonly Dictionary<Vector2Int, int> paintedCellIndexByCoordinate = new Dictionary<Vector2Int, int>();
    private string loadedMainScenePath;
    private string loadedSubScenePath;
    private Matrix4x4 spawnerLocalToWorld = Matrix4x4.identity;
    private Bounds sceneBounds;
    private readonly Vector3[] overlayCorners = new Vector3[4];
    private GUIStyle coordinateLabelStyle;
    private GUIStyle paintedLabelStyle;
    private int gridSizeX;
    private int gridSizeZ;
    private float cellSize;
    private Vector3 originOffset;
    private float spawnHeightOffset;
    private bool hasSpawner;
    private bool hasSceneBounds;
    private string loadWarning;
    #endregion

    #region Properties
    public bool HasSpawner => hasSpawner;
    public string LoadWarning => loadWarning;
    #endregion

    #region Methods

    #region Scene Loading
    /// <summary>
    /// Loads renderable copies of one managed room and its single ECS SubScene into an isolated preview scene.
    /// </summary>
    /// <param name="mainScenePath">Project-relative main scene path.</param>
    /// <param name="subScenePath">Project-relative ECS SubScene path.</param>
    public void Load(string mainScenePath, string subScenePath)
    {
        if (string.Equals(loadedMainScenePath, mainScenePath, StringComparison.Ordinal) &&
            string.Equals(loadedSubScenePath, subScenePath, StringComparison.Ordinal) &&
            previewUtility != null)
        {
            return;
        }

        Cleanup();
        loadedMainScenePath = mainScenePath;
        loadedSubScenePath = subScenePath;
        loadWarning = string.Empty;
        hasSpawner = false;

        if (string.IsNullOrWhiteSpace(mainScenePath) || string.IsNullOrWhiteSpace(subScenePath))
        {
            loadWarning = "Select a mapped main scene with exactly one resolved SubScene before painting.";
            return;
        }

        previewUtility = new PreviewRenderUtility(true);
        previewUtility.ambientColor = new Color(0.32f, 0.32f, 0.34f, 1f);
        previewUtility.lights[0].intensity = 1.15f;
        previewUtility.lights[0].transform.rotation = Quaternion.Euler(45f, 35f, 0f);
        previewUtility.lights[1].intensity = 0.55f;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;

        // Copy both managed and ECS authoring scenes without affecting the user's open-scene setup.
        AppendScene(mainScenePath, subScenePath, false, ref bounds, ref hasBounds);
        AppendScene(subScenePath, string.Empty, true, ref bounds, ref hasBounds);

        if (!hasSpawner)
            AppendLoadWarning("The mapped SubScene must contain exactly one EnemySpawnerAuthoring component.");

        sceneBounds = bounds;
        hasSceneBounds = hasBounds;
    }

    /// <summary>
    /// Copies renderable roots from one loaded or temporary preview scene and optionally captures its spawner settings.
    /// </summary>
    /// <param name="scenePath">Project-relative scene path to inspect.</param>
    /// <param name="referencedSubScenePath">SubScene whose duplicate loaded reference prevents opening a managed preview scene.</param>
    /// <param name="captureSpawner">Whether this scene is expected to own the enemy spawner.</param>
    /// <param name="bounds">Accumulated renderer bounds used to frame the scene.</param>
    /// <param name="hasBounds">Whether accumulated bounds have been initialized.</param>
    private void AppendScene(string scenePath,
                             string referencedSubScenePath,
                             bool captureSpawner,
                             ref Bounds bounds,
                             ref bool hasBounds)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool closeScene = false;

        try
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (!captureSpawner &&
                    GameWavesPreviewSceneUtility.IsSubSceneReferencedByLoadedScene(referencedSubScenePath))
                {
                    AppendLoadWarning("Managed room visuals were omitted because its SubScene is already referenced " +
                                      "by an open scene. The ECS room grid remains fully editable.");
                    return;
                }

                scene = EditorSceneManager.OpenPreviewScene(scenePath);
                closeScene = true;
            }

            GameObject[] roots = scene.GetRootGameObjects();

            if (captureSpawner)
                CaptureSpawner(roots);

            AppendRenderableRoots(roots, ref bounds, ref hasBounds);
        }
        catch (Exception exception)
        {
            AppendLoadWarning("Unable to build embedded preview for '" + scenePath + "': " + exception.Message);
        }
        finally
        {
            if (closeScene && scene.IsValid())
                EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    /// <summary>
    /// Clones renderable roots while excluding SubScene authoring roots that would register duplicate scene references.
    /// </summary>
    /// <param name="roots">Loaded source-scene roots.</param>
    /// <param name="bounds">Accumulated renderer bounds used to frame the scene.</param>
    /// <param name="hasBounds">Whether accumulated bounds have been initialized.</param>
    private void AppendRenderableRoots(GameObject[] roots, ref Bounds bounds, ref bool hasBounds)
    {
        // SubScene authoring objects are control roots, while their visual content is appended from the SubScene itself.
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            if (GameWavesPreviewSceneUtility.ContainsSubScene(roots[rootIndex]))
                continue;

            GameObject clone = UnityEngine.Object.Instantiate(roots[rootIndex]);
            clone.name = roots[rootIndex].name;
            clone.hideFlags = HideFlags.HideAndDontSave;
            GameWavesPreviewSceneUtility.DisablePreviewBehaviours(clone);
            previewUtility.AddSingleGO(clone);
            GameWavesPreviewSceneUtility.EncapsulateRenderers(clone, ref bounds, ref hasBounds);
        }
    }

    /// <summary>
    /// Appends one actionable preview warning without discarding an earlier independent loading issue.
    /// </summary>
    /// <param name="warning">Warning text to expose in Scene Brush.</param>
    private void AppendLoadWarning(string warning)
    {
        loadWarning = string.IsNullOrWhiteSpace(loadWarning)
            ? warning
            : loadWarning + "\n" + warning;
    }

    /// <summary>
    /// Captures grid geometry from exactly one spawner in the mapped ECS SubScene.
    /// </summary>
    /// <param name="roots">Loaded SubScene root objects.</param>
    private void CaptureSpawner(GameObject[] roots)
    {
        EnemySpawnerAuthoring resolvedSpawner = null;
        int spawnerCount = 0;

        // Count all nested spawners before accepting the room mapping.
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            EnemySpawnerAuthoring[] spawners = roots[rootIndex].GetComponentsInChildren<EnemySpawnerAuthoring>(true);
            spawnerCount += spawners.Length;

            if (spawners.Length > 0 && resolvedSpawner == null)
                resolvedSpawner = spawners[0];
        }

        if (spawnerCount != 1 || resolvedSpawner == null)
            return;

        hasSpawner = true;
        spawnerLocalToWorld = resolvedSpawner.transform.localToWorldMatrix;
        gridSizeX = resolvedSpawner.GridSizeX;
        gridSizeZ = resolvedSpawner.GridSizeZ;
        cellSize = resolvedSpawner.CellSize;
        originOffset = resolvedSpawner.OriginOffset;
        spawnHeightOffset = resolvedSpawner.SpawnHeightOffset;
    }

    #endregion

    #region Preview Drawing
    /// <summary>
    /// Draws the isolated room, wave overlay and direct paint interaction inside one IMGUI container.
    /// </summary>
    /// <param name="rect">Available IMGUI preview rectangle.</param>
    /// <param name="wavePreset">Wave preset modified through a preview-owned serialization stream.</param>
    /// <param name="waveIndex">Single wave currently visible and editable.</param>
    /// <param name="wavesPreset">Preset used to resolve per-category overlay colors.</param>
    /// <param name="brushCategoryId">Stable selected brush category.</param>
    /// <param name="enemyCount">Enemy amount written into newly painted cells.</param>
    /// <param name="erase">Whether left-click removes a cell instead of painting it.</param>
    /// <param name="zoom">Stable top-down magnification where one displays the complete grid.</param>
    /// <param name="selectedCell">Optional painted cell highlighted for detailed editing.</param>
    /// <param name="selectCell">Callback invoked by right-clicking a painted cell.</param>
    public void Draw(Rect rect,
                     EnemyWavePreset wavePreset,
                     int waveIndex,
                     GameWavesPreset wavesPreset,
                     string brushCategoryId,
                     int enemyCount,
                     bool erase,
                     float zoom,
                     Vector2Int? selectedCell,
                     Action<Vector2Int> selectCell)
    {
        EditorGUI.DrawRect(rect, new Color(0.09f, 0.09f, 0.1f, 1f));

        if (previewUtility == null)
        {
            GUI.Label(new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, 48f), loadWarning);
            return;
        }

        ConfigureCamera(rect, zoom, selectedCell);

        // Render the static room only during repaint events; layout and pointer events reuse its camera geometry.
        if (Event.current.type == EventType.Repaint)
        {
            previewUtility.BeginPreview(rect, GUIStyle.none);
            previewUtility.Render(true);
            Texture texture = previewUtility.EndPreview();

            if (texture != null)
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }

        SerializedObject wavePresetObject = ResolveWavePresetObject(wavePreset);

        if (hasSpawner && wavePresetObject != null)
        {
            if (Event.current.type == EventType.Repaint)
                DrawGridOverlay(rect, wavePresetObject, waveIndex, wavesPreset, selectedCell);

            HandleInteraction(rect,
                              wavePresetObject,
                              waveIndex,
                              brushCategoryId,
                              enemyCount,
                              erase || Event.current.shift,
                              selectCell);
        }

        if (Event.current.type == EventType.Repaint)
            DrawInstructions(rect, erase);
    }

    /// <summary>
    /// Positions a stable orthographic camera above the spawner grid and fits every authored cell.
    /// </summary>
    /// <param name="rect">Preview rectangle whose aspect ratio drives grid framing.</param>
    /// <param name="zoom">Requested centered magnification.</param>
    /// <param name="selectedCell">Optional cell used as the zoom focus.</param>
    private void ConfigureCamera(Rect rect, float zoom, Vector2Int? selectedCell)
    {
        Vector3 gridCenter = spawnerLocalToWorld.MultiplyPoint3x4(originOffset + Vector3.up * spawnHeightOffset);
        Vector3 gridUp = spawnerLocalToWorld.MultiplyVector(Vector3.up).normalized;
        Vector3 gridForward = spawnerLocalToWorld.MultiplyVector(Vector3.forward).normalized;
        Vector3 focusCenter = gridCenter;

        if (selectedCell.HasValue && zoom > 1f)
            focusCenter = spawnerLocalToWorld.MultiplyPoint3x4(ResolveCellLocalCenter(selectedCell.Value));

        float highestSceneOffset = hasSceneBounds
            ? GameWavesPreviewSceneUtility.ResolveMaximumBoundsProjection(sceneBounds, gridCenter, gridUp)
            : 0f;
        float cameraHeight = Mathf.Max(CameraClearance, highestSceneOffset + CameraClearance);
        Camera previewCamera = previewUtility.camera;
        previewCamera.orthographic = true;
        previewCamera.transform.rotation = Quaternion.LookRotation(-gridUp, gridForward);
        previewCamera.transform.position = focusCenter + gridUp * cameraHeight;
        float aspect = Mathf.Max(0.1f, rect.width / Mathf.Max(1f, rect.height));
        float gridWidth = Mathf.Max(1f, gridSizeX * cellSize);
        float gridDepth = Mathf.Max(1f, gridSizeZ * cellSize);
        float fitSize = Mathf.Max(gridDepth * 0.5f, gridWidth * 0.5f / aspect) * GridPaddingScale;
        previewCamera.orthographicSize = fitSize / Mathf.Clamp(zoom, 1f, 4f);
        previewCamera.nearClipPlane = 0.05f;
        previewCamera.farClipPlane = Mathf.Max(500f, cameraHeight * 4f);
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.09f, 0.09f, 0.1f, 1f);
    }

    /// <summary>
    /// Draws the complete colored and numbered grid, including category and count labels for painted cells.
    /// </summary>
    /// <param name="rect">Rendered preview rectangle.</param>
    /// <param name="wavePresetObject">Serialized wave asset supplying painted cells.</param>
    /// <param name="waveIndex">Visible wave index.</param>
    /// <param name="wavesPreset">Waves preset used to resolve category labels and colors.</param>
    /// <param name="selectedCell">Optional painted-cell coordinate highlighted for detailed editing.</param>
    private void DrawGridOverlay(Rect rect,
                                 SerializedObject wavePresetObject,
                                 int waveIndex,
                                 GameWavesPreset wavesPreset,
                                 Vector2Int? selectedCell)
    {
        SerializedProperty cells = FindPaintedCells(wavePresetObject, waveIndex);
        bool hasHoveredCell = TryResolveCellCoordinate(rect,
                                                       Event.current.mousePosition,
                                                       out Vector2Int hoveredCoordinate);
        GameWavesPreviewOverlayUtility.EnsureLabelStyles(ref coordinateLabelStyle, ref paintedLabelStyle);
        paintedCellIndexByCoordinate.Clear();

        // Index sparse authored cells once per repaint instead of scanning them for every grid coordinate.
        if (cells != null)
        {
            for (int cellIndex = 0; cellIndex < cells.arraySize; cellIndex++)
            {
                Vector2Int coordinate = cells.GetArrayElementAtIndex(cellIndex)
                                             .FindPropertyRelative("cellCoordinate")
                                             .vector2IntValue;
                paintedCellIndexByCoordinate[coordinate] = cellIndex;
            }
        }

        // Draw every cell explicitly so the authored grid remains readable over busy room materials.
        for (int zIndex = 0; zIndex < gridSizeZ; zIndex++)
        {
            for (int xIndex = 0; xIndex < gridSizeX; xIndex++)
            {
                Vector2Int coordinate = new Vector2Int(xIndex, zIndex);
                bool isPainted = paintedCellIndexByCoordinate.TryGetValue(coordinate, out int paintedCellIndex);
                bool isHovered = hasHoveredCell && hoveredCoordinate == coordinate;
                bool isSelected = selectedCell.HasValue && selectedCell.Value == coordinate;
                Color fillColor = (xIndex + zIndex) % 2 == 0
                    ? new Color(0.05f, 0.22f, 0.34f, 0.22f)
                    : new Color(0.04f, 0.16f, 0.27f, 0.18f);
                Color outlineColor = isSelected
                    ? new Color(0.25f, 1f, 0.45f, 1f)
                    : isHovered
                    ? new Color(1f, 0.82f, 0.18f, 1f)
                    : new Color(0.28f, 0.78f, 1f, 0.82f);
                string label = coordinate.x + "," + coordinate.y;
                GUIStyle labelStyle = coordinateLabelStyle;

                if (isPainted)
                {
                    SerializedProperty cell = cells.GetArrayElementAtIndex(paintedCellIndex);
                    string categoryId = cell.FindPropertyRelative("brushCategoryId").stringValue;
                    int paintedEnemyCount = cell.FindPropertyRelative("enemyCount").intValue;
                    fillColor = GameWavesPreviewOverlayUtility.ResolveCategoryColor(wavesPreset, categoryId);
                    fillColor.a = isHovered ? 0.78f : 0.58f;
                    outlineColor = isSelected
                        ? new Color(0.25f, 1f, 0.45f, 1f)
                        : isHovered
                        ? new Color(1f, 0.88f, 0.2f, 1f)
                        : new Color(fillColor.r, fillColor.g, fillColor.b, 1f);
                    label = coordinate.x + "," + coordinate.y + "\n" +
                            GameWavesPreviewOverlayUtility.ResolveCategoryLabel(wavesPreset, categoryId) +
                            " x" + paintedEnemyCount;
                    labelStyle = paintedLabelStyle;
                }

                GameWavesPreviewOverlayUtility.DrawCell(rect,
                                                        previewUtility.camera,
                                                        spawnerLocalToWorld,
                                                        ResolveCellLocalCenter(coordinate),
                                                        cellSize * CellInsetScale,
                                                        overlayCorners,
                                                        fillColor,
                                                        outlineColor,
                                                        label,
                                                        labelStyle);
            }
        }

        // Show the complete brush name for the hovered painted cell, or the selected cell as a stable fallback.
        int focusedCellIndex = hasHoveredCell &&
                               paintedCellIndexByCoordinate.TryGetValue(hoveredCoordinate, out int hoveredCellIndex)
            ? hoveredCellIndex
            : selectedCell.HasValue &&
              paintedCellIndexByCoordinate.TryGetValue(selectedCell.Value, out int selectedCellIndex)
                ? selectedCellIndex
                : -1;

        if (focusedCellIndex >= 0)
        {
            SerializedProperty focusedCell = cells.GetArrayElementAtIndex(focusedCellIndex);
            Vector2Int focusedCoordinate = focusedCell.FindPropertyRelative("cellCoordinate").vector2IntValue;
            GameWavesPreviewOverlayUtility.DrawFocusedCellDetails(
                rect,
                wavesPreset,
                focusedCell.FindPropertyRelative("brushCategoryId").stringValue,
                focusedCell.FindPropertyRelative("enemyCount").intValue,
                focusedCoordinate);
        }
    }

    /// <summary>
    /// Handles direct paint, erase and painted-cell selection clicks through the preview-owned serialization stream.
    /// </summary>
    /// <param name="rect">Interactive preview rectangle.</param>
    /// <param name="wavePresetObject">Serialized wave asset being edited.</param>
    /// <param name="waveIndex">Visible wave index.</param>
    /// <param name="brushCategoryId">Selected category written by paint operations.</param>
    /// <param name="enemyCount">Enemy count written by new cells.</param>
    /// <param name="erase">Whether the operation removes an existing cell.</param>
    /// <param name="selectCell">Callback selecting one existing painted cell for detailed editing.</param>
    private void HandleInteraction(Rect rect,
                                   SerializedObject wavePresetObject,
                                   int waveIndex,
                                   string brushCategoryId,
                                   int enemyCount,
                                   bool erase,
                                   Action<Vector2Int> selectCell)
    {
        Event currentEvent = Event.current;

        if (currentEvent.type != EventType.MouseDown ||
            currentEvent.button != 0 && currentEvent.button != 1 ||
            currentEvent.alt || !rect.Contains(currentEvent.mousePosition))
        {
            return;
        }

        if (!TryResolveCellCoordinate(rect, currentEvent.mousePosition, out Vector2Int coordinate))
            return;

        SerializedProperty cells = FindPaintedCells(wavePresetObject, waveIndex);

        if (cells == null)
            return;

        int existingIndex = FindCellIndex(cells, coordinate);

        if (currentEvent.button == 1)
        {
            if (existingIndex >= 0)
                selectCell?.Invoke(coordinate);

            currentEvent.Use();
            return;
        }

        if (!erase && string.IsNullOrWhiteSpace(brushCategoryId))
            return;

        Undo.RecordObject(wavePresetObject.targetObject, erase ? "Erase Enemy Wave Cell" : "Paint Enemy Wave Cell");

        if (erase)
        {
            if (existingIndex >= 0)
                cells.DeleteArrayElementAtIndex(existingIndex);
        }
        else
        {
            if (existingIndex < 0)
            {
                existingIndex = cells.arraySize;
                cells.InsertArrayElementAtIndex(existingIndex);
            }

            SerializedProperty cell = cells.GetArrayElementAtIndex(existingIndex);
            cell.FindPropertyRelative("cellCoordinate").vector2IntValue = coordinate;
            cell.FindPropertyRelative("brushCategoryId").stringValue = brushCategoryId;
            cell.FindPropertyRelative("enemyCount").intValue = enemyCount;
        }

        wavePresetObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(wavePresetObject.targetObject);
        GameManagementDraftSession.MarkDirty();

        if (!erase)
            selectCell?.Invoke(coordinate);

        currentEvent.Use();
    }

    /// <summary>
    /// Draws concise navigation and current paint-mode instructions over the preview.
    /// </summary>
    /// <param name="rect">Rendered preview rectangle.</param>
    /// <param name="erase">Current toolbar erase state.</param>
    private void DrawInstructions(Rect rect, bool erase)
    {
        string interaction = erase
            ? "ERASE | Left click removes | Right click selects a painted cell"
            : "PAINT | Left click paints | Shift + left click erases | Right click selects";
        Rect labelRect = new Rect(rect.x + 8f, rect.yMax - 26f, rect.width - 16f, 20f);
        EditorGUI.DrawRect(labelRect, new Color(0f, 0f, 0f, 0.62f));
        GUI.Label(labelRect, interaction, EditorStyles.miniLabel);

        if (!string.IsNullOrWhiteSpace(loadWarning))
            GUI.Label(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 40f), loadWarning, EditorStyles.helpBox);
    }
    #endregion

    #region Geometry Helpers
    /// <summary>
    /// Resolves a dedicated serialized view for preview drawing so repaint updates cannot interrupt bound tool fields.
    /// </summary>
    /// <param name="wavePreset">Wave asset displayed by the preview.</param>
    /// <returns>Preview-owned serialized object, or null when no wave asset is selected.</returns>
    private SerializedObject ResolveWavePresetObject(EnemyWavePreset wavePreset)
    {
        if (wavePreset == null)
            return null;

        if (previewWavePreset != wavePreset || previewWavePresetObject == null)
        {
            previewWavePreset = wavePreset;
            previewWavePresetObject = new SerializedObject(wavePreset);
        }

        return previewWavePresetObject;
    }

    /// <summary>
    /// Converts one preview pointer position into a valid local spawner-grid coordinate.
    /// </summary>
    /// <param name="rect">Interactive preview rectangle.</param>
    /// <param name="mousePosition">Current IMGUI pointer position.</param>
    /// <param name="coordinate">Resolved grid coordinate when the ray hits the grid plane.</param>
    /// <returns>True when the pointer resolves inside the authored grid.</returns>
    private bool TryResolveCellCoordinate(Rect rect,
                                          Vector2 mousePosition,
                                          out Vector2Int coordinate)
    {
        coordinate = default;
        Vector3 viewportPoint = new Vector3((mousePosition.x - rect.x) / rect.width,
                                            1f - (mousePosition.y - rect.y) / rect.height,
                                            0f);
        Ray ray = previewUtility.camera.ViewportPointToRay(viewportPoint);
        Vector3 planeNormal = spawnerLocalToWorld.MultiplyVector(Vector3.up).normalized;
        Vector3 planeOrigin = spawnerLocalToWorld.MultiplyPoint3x4(originOffset +
                                                                   Vector3.up * spawnHeightOffset);
        Plane gridPlane = new Plane(planeNormal, planeOrigin);

        if (!gridPlane.Raycast(ray, out float hitDistance))
            return false;

        Vector3 localHit = spawnerLocalToWorld.inverse.MultiplyPoint3x4(ray.GetPoint(hitDistance));
        int xCoordinate = Mathf.RoundToInt((localHit.x - originOffset.x) / cellSize + (gridSizeX - 1) * 0.5f);
        int zCoordinate = Mathf.RoundToInt((localHit.z - originOffset.z) / cellSize + (gridSizeZ - 1) * 0.5f);

        if (xCoordinate < 0 || xCoordinate >= gridSizeX || zCoordinate < 0 || zCoordinate >= gridSizeZ)
            return false;

        coordinate = new Vector2Int(xCoordinate, zCoordinate);
        return true;
    }

    /// <summary>
    /// Resolves one cell center in spawner-local space for painting and overlay corners.
    /// </summary>
    /// <param name="coordinate">Grid coordinate to resolve.</param>
    /// <returns>Spawner-local cell center.</returns>
    private Vector3 ResolveCellLocalCenter(Vector2Int coordinate)
    {
        return new Vector3(originOffset.x +
                           (coordinate.x - (gridSizeX - 1) * 0.5f) * cellSize,
                           originOffset.y + spawnHeightOffset,
                           originOffset.z +
                           (coordinate.y - (gridSizeZ - 1) * 0.5f) * cellSize);
    }

    /// <summary>
    /// Resolves the painted-cell array for one visible wave.
    /// </summary>
    /// <param name="wavePresetObject">Serialized wave preset.</param>
    /// <param name="waveIndex">Requested visible wave index.</param>
    /// <returns>Painted-cell array, or null when the index is invalid.</returns>
    private static SerializedProperty FindPaintedCells(SerializedObject wavePresetObject, int waveIndex)
    {
        wavePresetObject.UpdateIfRequiredOrScript();
        SerializedProperty waves = wavePresetObject.FindProperty("waves");

        if (waves == null || waveIndex < 0 || waveIndex >= waves.arraySize)
            return null;

        return waves.GetArrayElementAtIndex(waveIndex).FindPropertyRelative("paintedCells");
    }

    /// <summary>
    /// Finds one sparse painted cell by grid coordinate.
    /// </summary>
    /// <param name="cells">Serialized painted-cell array.</param>
    /// <param name="coordinate">Grid coordinate being searched.</param>
    /// <returns>Array index when found, otherwise -1.</returns>
    private static int FindCellIndex(SerializedProperty cells, Vector2Int coordinate)
    {
        for (int cellIndex = 0; cellIndex < cells.arraySize; cellIndex++)
        {
            if (cells.GetArrayElementAtIndex(cellIndex)
                     .FindPropertyRelative("cellCoordinate")
                     .vector2IntValue == coordinate)
            {
                return cellIndex;
            }
        }

        return -1;
    }

    #endregion

    #region Cleanup
    /// <summary>
    /// Releases cloned objects, preview render textures and the isolated preview scene.
    /// </summary>
    public void Dispose()
    {
        Cleanup();
    }

    /// <summary>
    /// Releases the current preview utility while retaining camera authoring preferences.
    /// </summary>
    private void Cleanup()
    {
        if (previewUtility != null)
        {
            previewUtility.Cleanup();
            previewUtility = null;
        }

        loadedMainScenePath = string.Empty;
        loadedSubScenePath = string.Empty;
        previewWavePreset = null;
        previewWavePresetObject = null;
        hasSpawner = false;
        hasSceneBounds = false;
    }
    #endregion

    #endregion
}
