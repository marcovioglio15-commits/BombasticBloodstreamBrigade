using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Stores immutable preview data used to draw one grid cell without keeping SerializedProperty handles alive.
/// </summary>
public readonly struct EnemySpawnerGridCellPreviewData
{
    #region Fields
    public readonly int EnemyCount;
    public readonly Color FillColor;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one immutable preview snapshot for a painted grid cell.
    /// </summary>
    /// <param name="enemyCount">Authored enemy count of the cell.</param>
    /// <param name="fillColor">Resolved paint color of the cell.</param>
    public EnemySpawnerGridCellPreviewData(int enemyCount, Color fillColor)
    {
        EnemyCount = enemyCount;
        FillColor = fillColor;
    }
    #endregion
}

/// <summary>
/// Centralizes serialized wave and cell mutations used by EnemySpawnerAuthoringEditor.
/// </summary>
public static class EnemySpawnerAuthoringEditorWaveUtility
{
    #region Methods

    #region Lookup
    /// <summary>
    /// Returns the painted-cells property of one wave.
    /// </summary>
    /// <param name="wavesProperty">Serialized waves array.</param>
    /// <param name="waveIndex">Wave index to inspect.</param>
    /// <returns>Painted cells property, or null when the wave index is invalid.</returns>
    public static SerializedProperty GetPaintedCellsProperty(SerializedProperty wavesProperty, int waveIndex)
    {
        if (wavesProperty == null)
            return null;

        if (waveIndex < 0 || waveIndex >= wavesProperty.arraySize)
            return null;

        SerializedProperty waveProperty = wavesProperty.GetArrayElementAtIndex(waveIndex);
        return waveProperty.FindPropertyRelative("paintedCells");
    }

    /// <summary>
    /// Finds one painted cell property by coordinate.
    /// </summary>
    /// <param name="wavesProperty">Serialized waves array.</param>
    /// <param name="waveIndex">Wave index to inspect.</param>
    /// <param name="coordinate">Target coordinate.</param>
    /// <returns>Serialized property representing the painted cell, or null when it does not exist.</returns>
    public static SerializedProperty FindCellProperty(SerializedProperty wavesProperty, int waveIndex, Vector2Int coordinate)
    {
        SerializedProperty paintedCellsProperty = GetPaintedCellsProperty(wavesProperty, waveIndex);

        if (paintedCellsProperty == null)
            return null;

        int existingCellIndex = FindCellIndex(paintedCellsProperty, coordinate);

        if (existingCellIndex < 0)
            return null;

        return paintedCellsProperty.GetArrayElementAtIndex(existingCellIndex);
    }

    /// <summary>
    /// Finds the array index of one painted cell by grid coordinate.
    /// </summary>
    /// <param name="paintedCellsProperty">Serialized array of painted cells.</param>
    /// <param name="coordinate">Target coordinate.</param>
    /// <returns>Index of the painted cell, or -1 when not found.</returns>
    public static int FindCellIndex(SerializedProperty paintedCellsProperty, Vector2Int coordinate)
    {
        if (paintedCellsProperty == null)
            return -1;

        for (int cellIndex = 0; cellIndex < paintedCellsProperty.arraySize; cellIndex++)
        {
            SerializedProperty cellProperty = paintedCellsProperty.GetArrayElementAtIndex(cellIndex);

            if (cellProperty == null)
                continue;

            if (cellProperty.FindPropertyRelative("cellCoordinate").vector2IntValue == coordinate)
                return cellIndex;
        }

        return -1;
    }

    /// <summary>
    /// Builds a coordinate lookup for existing painted cells of the current wave.
    /// </summary>
    /// <param name="paintedCellsProperty">Serialized array of painted cells.</param>
    /// <param name="wavesPreset">Waves preset used to resolve category colors.</param>
    /// <returns>Coordinate-to-preview-data lookup.</returns>
    public static Dictionary<Vector2Int, EnemySpawnerGridCellPreviewData> BuildCellPreviewMap(SerializedProperty paintedCellsProperty,
                                                                                              GameWavesPreset wavesPreset)
    {
        Dictionary<Vector2Int, EnemySpawnerGridCellPreviewData> cellPreviewByCoordinate = new Dictionary<Vector2Int, EnemySpawnerGridCellPreviewData>();

        if (paintedCellsProperty == null)
            return cellPreviewByCoordinate;

        for (int cellIndex = 0; cellIndex < paintedCellsProperty.arraySize; cellIndex++)
        {
            SerializedProperty cellProperty = paintedCellsProperty.GetArrayElementAtIndex(cellIndex);

            if (cellProperty == null)
                continue;

            Vector2Int coordinate = cellProperty.FindPropertyRelative("cellCoordinate").vector2IntValue;
            int enemyCount = Mathf.Max(0, cellProperty.FindPropertyRelative("enemyCount").intValue);
            string categoryId = cellProperty.FindPropertyRelative("brushCategoryId").stringValue;
            Color color = ResolveCategoryColor(wavesPreset, categoryId);
            color.a = 0.9f;
            cellPreviewByCoordinate[coordinate] = new EnemySpawnerGridCellPreviewData(enemyCount, color);
        }

        return cellPreviewByCoordinate;
    }

    /// <summary>
    /// Creates a deep clone of an animation curve while preserving wrap modes.
    /// </summary>
    /// <param name="sourceCurve">Source curve to duplicate.</param>
    /// <returns>Cloned curve, or a default linear curve when the source is null.</returns>
    public static AnimationCurve CloneAnimationCurve(AnimationCurve sourceCurve)
    {
        AnimationCurve clonedCurve = sourceCurve == null
            ? EnemySpawnerWaveBakeUtility.CreateDefaultDistributionCurve()
            : new AnimationCurve(sourceCurve.keys);

        if (sourceCurve != null)
        {
            clonedCurve.preWrapMode = sourceCurve.preWrapMode;
            clonedCurve.postWrapMode = sourceCurve.postWrapMode;
        }

        return clonedCurve;
    }
    #endregion

    #region Cell Mutation
    /// <summary>
    /// Paints or erases one cell depending on the current brush mode.
    /// </summary>
    /// <param name="serializedObject">Serialized object backing the editor.</param>
    /// <param name="targetObject">Unity object marked dirty after mutation.</param>
    /// <param name="wavesProperty">Serialized waves array.</param>
    /// <param name="waveIndex">Wave index receiving the change.</param>
    /// <param name="coordinate">Target grid coordinate.</param>
    /// <param name="eraseMode">True to erase instead of paint.</param>
    /// <param name="brushCategoryId">Stable brush category identifier assigned while painting.</param>
    /// <param name="brushEnemyCount">Enemy count assigned while painting.</param>
    /// <param name="brushDistributionCurve">Default curve copied into new cells.</param>
    /// <param name="selectedWaveIndex">Current selected wave index, updated by the mutation.</param>
    /// <param name="selectedCellCoordinate">Current selected coordinate, updated by the mutation.</param>
    /// <returns>True when the serialized data changed, otherwise false.</returns>
    public static bool PaintCell(SerializedObject serializedObject,
                                 Object targetObject,
                                 SerializedProperty wavesProperty,
                                 int waveIndex,
                                 Vector2Int coordinate,
                                 bool eraseMode,
                                 string brushCategoryId,
                                 int brushEnemyCount,
                                 AnimationCurve brushDistributionCurve,
                                 ref int selectedWaveIndex,
                                 ref Vector2Int selectedCellCoordinate)
    {
        SerializedProperty paintedCellsProperty = GetPaintedCellsProperty(wavesProperty, waveIndex);

        if (paintedCellsProperty == null)
            return false;

        int existingCellIndex = FindCellIndex(paintedCellsProperty, coordinate);

        if (eraseMode)
        {
            if (existingCellIndex < 0)
                return false;

            paintedCellsProperty.DeleteArrayElementAtIndex(existingCellIndex);

            if (selectedWaveIndex == waveIndex && selectedCellCoordinate == coordinate)
            {
                selectedWaveIndex = -1;
                selectedCellCoordinate = new Vector2Int(-1, -1);
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetObject);
            return true;
        }

        if (string.IsNullOrWhiteSpace(brushCategoryId))
            return false;

        SerializedProperty cellProperty;

        if (existingCellIndex >= 0)
            cellProperty = paintedCellsProperty.GetArrayElementAtIndex(existingCellIndex);
        else
        {
            int newIndex = paintedCellsProperty.arraySize;
            paintedCellsProperty.InsertArrayElementAtIndex(newIndex);
            cellProperty = paintedCellsProperty.GetArrayElementAtIndex(newIndex);
        }

        cellProperty.FindPropertyRelative("cellCoordinate").vector2IntValue = coordinate;
        cellProperty.FindPropertyRelative("brushCategoryId").stringValue = brushCategoryId;
        cellProperty.FindPropertyRelative("enemyCount").intValue = Mathf.Max(1, brushEnemyCount);
        cellProperty.FindPropertyRelative("useWaveDefaultDistribution").boolValue = false;
        cellProperty.FindPropertyRelative("distributionCurveOverride").animationCurveValue = CloneAnimationCurve(brushDistributionCurve);

        selectedWaveIndex = waveIndex;
        selectedCellCoordinate = coordinate;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(targetObject);
        return true;
    }

    /// <summary>
    /// Resolves a category color without retaining references to serialized editor properties.
    /// </summary>
    /// <param name="wavesPreset">Waves preset containing the brush category.</param>
    /// <param name="categoryId">Stable category identifier stored on the cell.</param>
    /// <returns>Authored category color, or a clear warning color when the category is unresolved.</returns>
    private static Color ResolveCategoryColor(GameWavesPreset wavesPreset, string categoryId)
    {
        if (wavesPreset != null &&
            wavesPreset.TryFindBrushCategory(categoryId, out EnemyBrushCategoryDefinition category))
        {
            return category.BrushColor;
        }

        return new Color(1f, 0.2f, 0.2f, 0.9f);
    }

    /// <summary>
    /// Selects one painted cell for detailed editing.
    /// </summary>
    /// <param name="wavesProperty">Serialized waves array.</param>
    /// <param name="waveIndex">Wave index containing the cell.</param>
    /// <param name="coordinate">Grid coordinate of the selected cell.</param>
    /// <param name="selectedWaveIndex">Current selected wave index, updated by the selection.</param>
    /// <param name="selectedCellCoordinate">Current selected coordinate, updated by the selection.</param>
    /// <returns>True when the requested cell exists, otherwise false.</returns>
    public static bool SelectCell(SerializedProperty wavesProperty,
                                  int waveIndex,
                                  Vector2Int coordinate,
                                  ref int selectedWaveIndex,
                                  ref Vector2Int selectedCellCoordinate)
    {
        SerializedProperty paintedCellsProperty = GetPaintedCellsProperty(wavesProperty, waveIndex);

        if (paintedCellsProperty == null)
            return false;

        int existingCellIndex = FindCellIndex(paintedCellsProperty, coordinate);

        if (existingCellIndex < 0)
        {
            selectedWaveIndex = -1;
            selectedCellCoordinate = new Vector2Int(-1, -1);
            return false;
        }

        selectedWaveIndex = waveIndex;
        selectedCellCoordinate = coordinate;
        return true;
    }

    /// <summary>
    /// Removes one painted cell from the requested wave.
    /// </summary>
    /// <param name="serializedObject">Serialized object backing the editor.</param>
    /// <param name="targetObject">Unity object marked dirty after mutation.</param>
    /// <param name="wavesProperty">Serialized waves array.</param>
    /// <param name="waveIndex">Wave index containing the cell.</param>
    /// <param name="coordinate">Grid coordinate to remove.</param>
    /// <param name="selectedWaveIndex">Current selected wave index, updated by the mutation.</param>
    /// <param name="selectedCellCoordinate">Current selected coordinate, updated by the mutation.</param>
    /// <returns>True when the cell existed and was removed, otherwise false.</returns>
    public static bool RemoveCell(SerializedObject serializedObject,
                                  Object targetObject,
                                  SerializedProperty wavesProperty,
                                  int waveIndex,
                                  Vector2Int coordinate,
                                  ref int selectedWaveIndex,
                                  ref Vector2Int selectedCellCoordinate)
    {
        SerializedProperty paintedCellsProperty = GetPaintedCellsProperty(wavesProperty, waveIndex);

        if (paintedCellsProperty == null)
            return false;

        int existingCellIndex = FindCellIndex(paintedCellsProperty, coordinate);

        if (existingCellIndex < 0)
            return false;

        paintedCellsProperty.DeleteArrayElementAtIndex(existingCellIndex);
        selectedWaveIndex = -1;
        selectedCellCoordinate = new Vector2Int(-1, -1);
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(targetObject);
        return true;
    }
    #endregion

    #region Wave Mutation
    /// <summary>
    /// Appends one new empty wave to the serialized wave array.
    /// </summary>
    /// <param name="serializedObject">Serialized object backing the editor.</param>
    /// <param name="targetObject">Unity object marked dirty after mutation.</param>
    /// <param name="wavesProperty">Serialized waves array.</param>
    public static void AddWave(SerializedObject serializedObject,
                               Object targetObject,
                               SerializedProperty wavesProperty)
    {
        if (wavesProperty == null)
            return;

        int newWaveIndex = wavesProperty.arraySize;
        wavesProperty.InsertArrayElementAtIndex(newWaveIndex);
        SerializedProperty newWaveProperty = wavesProperty.GetArrayElementAtIndex(newWaveIndex);
        newWaveProperty.FindPropertyRelative("waveLabel").stringValue = "Wave " + (newWaveIndex + 1);
        newWaveProperty.FindPropertyRelative("previewInScene").boolValue = wavesProperty.arraySize == 1;
        newWaveProperty.FindPropertyRelative("startMode").enumValueIndex = newWaveIndex == 0
            ? (int)EnemyWaveStartMode.FromSpawnerStart
            : (int)EnemyWaveStartMode.AfterPreviousWaveCompleted;
        newWaveProperty.FindPropertyRelative("startDelaySeconds").floatValue = 0f;
        newWaveProperty.FindPropertyRelative("spawnDurationSeconds").floatValue = 4f;
        newWaveProperty.FindPropertyRelative("defaultDistributionCurve").animationCurveValue = EnemySpawnerWaveBakeUtility.CreateDefaultDistributionCurve();
        SerializedProperty paintedCellsProperty = newWaveProperty.FindPropertyRelative("paintedCells");
        paintedCellsProperty.ClearArray();
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(targetObject);
    }

    /// <summary>
    /// Deletes one wave from the serialized array.
    /// </summary>
    /// <param name="serializedObject">Serialized object backing the editor.</param>
    /// <param name="targetObject">Unity object marked dirty after mutation.</param>
    /// <param name="wavesProperty">Serialized waves array.</param>
    /// <param name="waveIndex">Index of the wave to delete.</param>
    /// <param name="selectedWaveIndex">Current selected wave index, updated by the mutation.</param>
    /// <param name="selectedCellCoordinate">Current selected coordinate, updated by the mutation.</param>
    /// <returns>True when the wave existed and was removed, otherwise false.</returns>
    public static bool DeleteWave(SerializedObject serializedObject,
                                  Object targetObject,
                                  SerializedProperty wavesProperty,
                                  int waveIndex,
                                  ref int selectedWaveIndex,
                                  ref Vector2Int selectedCellCoordinate)
    {
        if (wavesProperty == null || waveIndex < 0 || waveIndex >= wavesProperty.arraySize)
            return false;

        wavesProperty.DeleteArrayElementAtIndex(waveIndex);

        if (selectedWaveIndex == waveIndex)
        {
            selectedWaveIndex = -1;
            selectedCellCoordinate = new Vector2Int(-1, -1);
        }
        else if (selectedWaveIndex > waveIndex)
            selectedWaveIndex--;

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(targetObject);
        return true;
    }

    /// <summary>
    /// Clears all painted cells from the requested wave.
    /// </summary>
    /// <param name="serializedObject">Serialized object backing the editor.</param>
    /// <param name="targetObject">Unity object marked dirty after mutation.</param>
    /// <param name="wavesProperty">Serialized waves array.</param>
    /// <param name="waveIndex">Index of the wave to clear.</param>
    /// <param name="selectedWaveIndex">Current selected wave index, updated by the mutation.</param>
    /// <param name="selectedCellCoordinate">Current selected coordinate, updated by the mutation.</param>
    /// <returns>True when the wave existed and was cleared, otherwise false.</returns>
    public static bool ClearWaveCells(SerializedObject serializedObject,
                                      Object targetObject,
                                      SerializedProperty wavesProperty,
                                      int waveIndex,
                                      ref int selectedWaveIndex,
                                      ref Vector2Int selectedCellCoordinate)
    {
        SerializedProperty paintedCellsProperty = GetPaintedCellsProperty(wavesProperty, waveIndex);

        if (paintedCellsProperty == null)
            return false;

        paintedCellsProperty.ClearArray();

        if (selectedWaveIndex == waveIndex)
        {
            selectedWaveIndex = -1;
            selectedCellCoordinate = new Vector2Int(-1, -1);
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(targetObject);
        return true;
    }

    /// <summary>
    /// Enables preview on one wave and disables it on all others.
    /// </summary>
    /// <param name="serializedObject">Serialized object backing the editor.</param>
    /// <param name="targetObject">Unity object marked dirty after mutation.</param>
    /// <param name="wavesProperty">Serialized waves array.</param>
    /// <param name="previewWaveIndex">Wave index that should remain previewed.</param>
    public static void SetWavePreview(SerializedObject serializedObject,
                                      Object targetObject,
                                      SerializedProperty wavesProperty,
                                      int previewWaveIndex)
    {
        if (wavesProperty == null)
            return;

        for (int waveIndex = 0; waveIndex < wavesProperty.arraySize; waveIndex++)
        {
            SerializedProperty waveProperty = wavesProperty.GetArrayElementAtIndex(waveIndex);
            waveProperty.FindPropertyRelative("previewInScene").boolValue = waveIndex == previewWaveIndex;
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(targetObject);
    }

    /// <summary>
    /// Terminates paint-drag mode when the mouse button is released.
    /// </summary>
    /// <param name="currentEvent">Current IMGUI event.</param>
    /// <param name="paintDragActive">Current paint-drag flag, updated by the method.</param>
    /// <param name="paintDragWaveIndex">Current paint-drag wave index, updated by the method.</param>
    /// <param name="lastPaintedCoordinate">Last drag-painted coordinate, updated by the method.</param>
    public static void HandlePaintDragTermination(Event currentEvent,
                                                  ref bool paintDragActive,
                                                  ref int paintDragWaveIndex,
                                                  ref Vector2Int lastPaintedCoordinate)
    {
        if (currentEvent.type != EventType.MouseUp)
            return;

        paintDragActive = false;
        paintDragWaveIndex = -1;
        lastPaintedCoordinate = new Vector2Int(int.MinValue, int.MinValue);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores per-spawner inspector state for wave foldouts, painter visibility and grid scroll positions.
/// </summary>
internal sealed class EnemySpawnerAuthoringEditorState
{
    #region Constants
    private const string WaveScrollSessionPrefix = "NashCore.EnemySpawnerAuthoringEditor.WaveScroll.";
    private const string FoldoutSessionPrefix = "NashCore.EnemySpawnerAuthoringEditor.WaveFoldout.";
    private const string PainterVisibilitySessionPrefix = "NashCore.EnemySpawnerAuthoringEditor.PainterVisibility.";
    #endregion

    #region Fields
    private readonly Dictionary<int, bool> waveFoldoutState = new Dictionary<int, bool>();
    private readonly Dictionary<int, Vector2> waveGridScrollPositions = new Dictionary<int, Vector2>();
    private readonly Dictionary<int, bool> wavePainterVisibilityState = new Dictionary<int, bool>();
    private int spawnerInstanceId;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebinds state to one spawner instance and clears in-memory caches before lazy SessionState hydration.
    /// </summary>
    /// <param name="instanceId">Unity instance identifier of the inspected spawner.</param>
    public void Reset(int instanceId)
    {
        spawnerInstanceId = instanceId;
        waveFoldoutState.Clear();
        waveGridScrollPositions.Clear();
        wavePainterVisibilityState.Clear();
    }

    /// <summary>
    /// Returns the persisted foldout state of one wave, defaulting to expanded when never seen before.
    /// </summary>
    /// <param name="waveIndex">Wave index to inspect.</param>
    /// <returns>True when the foldout is expanded.</returns>
    public bool GetWaveFoldoutState(int waveIndex)
    {
        if (waveFoldoutState.TryGetValue(waveIndex, out bool isExpanded))
            return isExpanded;

        bool sessionExpanded = SessionState.GetBool(BuildFoldoutKey(waveIndex), true);
        waveFoldoutState[waveIndex] = sessionExpanded;
        return sessionExpanded;
    }

    /// <summary>
    /// Stores the foldout state of one wave both in memory and SessionState.
    /// </summary>
    /// <param name="waveIndex">Wave index to update.</param>
    /// <param name="isExpanded">New foldout state.</param>
    public void SetWaveFoldoutState(int waveIndex, bool isExpanded)
    {
        waveFoldoutState[waveIndex] = isExpanded;
        SessionState.SetBool(BuildFoldoutKey(waveIndex), isExpanded);
    }

    /// <summary>
    /// Returns the cached scroll position for one wave grid, hydrating from SessionState on first read.
    /// </summary>
    /// <param name="waveIndex">Wave index owning the scroll position.</param>
    /// <returns>Current scroll position.</returns>
    public Vector2 GetWaveScrollPosition(int waveIndex)
    {
        if (waveGridScrollPositions.TryGetValue(waveIndex, out Vector2 cachedPosition))
            return cachedPosition;

        string sessionKey = BuildScrollKey(waveIndex);
        Vector2 hydratedPosition = new Vector2(SessionState.GetFloat(sessionKey + ".x", 0f),
                                               SessionState.GetFloat(sessionKey + ".y", 0f));
        waveGridScrollPositions[waveIndex] = hydratedPosition;
        return hydratedPosition;
    }

    /// <summary>
    /// Stores the latest scroll position for one wave grid and skips unchanged SessionState writes.
    /// </summary>
    /// <param name="waveIndex">Wave index owning the scroll position.</param>
    /// <param name="scrollPosition">Latest scroll position emitted by BeginScrollView.</param>
    public void SetWaveScrollPosition(int waveIndex, Vector2 scrollPosition)
    {
        bool hadPrevious = waveGridScrollPositions.TryGetValue(waveIndex, out Vector2 previousPosition);
        waveGridScrollPositions[waveIndex] = scrollPosition;

        if (hadPrevious && previousPosition == scrollPosition)
            return;

        string sessionKey = BuildScrollKey(waveIndex);
        SessionState.SetFloat(sessionKey + ".x", scrollPosition.x);
        SessionState.SetFloat(sessionKey + ".y", scrollPosition.y);
    }

    /// <summary>
    /// Returns whether the shared painter controls are visible inside one wave foldout.
    /// </summary>
    /// <param name="waveIndex">Wave index whose painter visibility is requested.</param>
    /// <returns>True when the painter controls should be drawn.</returns>
    public bool GetWavePainterVisibility(int waveIndex)
    {
        if (wavePainterVisibilityState.TryGetValue(waveIndex, out bool isVisible))
            return isVisible;

        bool sessionVisibility = SessionState.GetBool(BuildPainterVisibilityKey(waveIndex), false);
        wavePainterVisibilityState[waveIndex] = sessionVisibility;
        return sessionVisibility;
    }

    /// <summary>
    /// Stores whether the shared painter controls are visible inside one wave foldout.
    /// </summary>
    /// <param name="waveIndex">Wave index whose painter visibility is updated.</param>
    /// <param name="isVisible">New painter visibility.</param>
    public void SetWavePainterVisibility(int waveIndex, bool isVisible)
    {
        wavePainterVisibilityState[waveIndex] = isVisible;
        SessionState.SetBool(BuildPainterVisibilityKey(waveIndex), isVisible);
    }

    /// <summary>
    /// Clears state at and after a deleted wave index so shifted waves cannot inherit unrelated editor state.
    /// </summary>
    /// <param name="firstWaveIndex">First invalidated wave index.</param>
    /// <param name="previousWaveCount">Wave count before deletion.</param>
    public void ClearWaveStateFrom(int firstWaveIndex, int previousWaveCount)
    {
        for (int waveIndex = firstWaveIndex; waveIndex < previousWaveCount; waveIndex++)
        {
            waveGridScrollPositions.Remove(waveIndex);
            waveFoldoutState.Remove(waveIndex);
            wavePainterVisibilityState.Remove(waveIndex);
            string scrollKey = BuildScrollKey(waveIndex);
            SessionState.EraseFloat(scrollKey + ".x");
            SessionState.EraseFloat(scrollKey + ".y");
            SessionState.EraseBool(BuildFoldoutKey(waveIndex));
            SessionState.EraseBool(BuildPainterVisibilityKey(waveIndex));
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds the SessionState key used to persist one wave grid scroll position.
    /// </summary>
    /// <param name="waveIndex">Wave index part of the key.</param>
    /// <returns>SessionState key string.</returns>
    private string BuildScrollKey(int waveIndex)
    {
        return WaveScrollSessionPrefix + spawnerInstanceId.ToString() + "." + waveIndex.ToString();
    }

    /// <summary>
    /// Builds the SessionState key used to persist one wave foldout state.
    /// </summary>
    /// <param name="waveIndex">Wave index part of the key.</param>
    /// <returns>SessionState key string.</returns>
    private string BuildFoldoutKey(int waveIndex)
    {
        return FoldoutSessionPrefix + spawnerInstanceId.ToString() + "." + waveIndex.ToString();
    }

    /// <summary>
    /// Builds the SessionState key used to persist one wave painter visibility toggle.
    /// </summary>
    /// <param name="waveIndex">Wave index part of the key.</param>
    /// <returns>SessionState key string.</returns>
    private string BuildPainterVisibilityKey(int waveIndex)
    {
        return PainterVisibilitySessionPrefix + spawnerInstanceId.ToString() + "." + waveIndex.ToString();
    }
    #endregion

    #endregion
}
