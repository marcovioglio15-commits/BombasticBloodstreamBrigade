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
    /// <returns>Coordinate-to-preview-data lookup.</returns>
    public static Dictionary<Vector2Int, EnemySpawnerGridCellPreviewData> BuildCellPreviewMap(SerializedProperty paintedCellsProperty)
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
            EnemyMasterPreset masterPreset = cellProperty.FindPropertyRelative("masterPreset").objectReferenceValue as EnemyMasterPreset;
            Color color = EnemySpawnerWaveBakeUtility.ResolvePaintColor(masterPreset);
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
    /// <param name="brushMasterPreset">Master preset assigned while painting.</param>
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
                                 EnemyMasterPreset brushMasterPreset,
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

        if (brushMasterPreset == null)
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
        cellProperty.FindPropertyRelative("masterPreset").objectReferenceValue = brushMasterPreset;
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
    /// <param name="waveFoldoutState">Foldout-state cache updated for the new wave.</param>
    public static void AddWave(SerializedObject serializedObject,
                               Object targetObject,
                               SerializedProperty wavesProperty,
                               Dictionary<int, bool> waveFoldoutState)
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
        waveFoldoutState[newWaveIndex] = true;
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
