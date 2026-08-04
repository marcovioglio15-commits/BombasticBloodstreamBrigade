using UnityEditor;
using UnityEngine;

/// <summary>
/// Removes painted wave cells that no longer belong to a valid resized spawner grid.
/// </summary>
internal static class GameWavesGridResizeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Removes every out-of-bounds cell across all waves while preserving Undo and draft-session restoration.
    /// </summary>
    /// <param name="waveSerializedObject">Serialized Enemy Wave preset containing painted cells.</param>
    /// <param name="gridSizeX">New valid horizontal grid size.</param>
    /// <param name="gridSizeZ">New valid vertical grid size.</param>
    /// <returns>Number of removed painted cells.</returns>
    public static int RemoveOutOfBoundsCells(SerializedObject waveSerializedObject,
                                             int gridSizeX,
                                             int gridSizeZ)
    {
        if (waveSerializedObject == null || gridSizeX <= 0 || gridSizeZ <= 0)
            return 0;

        waveSerializedObject.UpdateIfRequiredOrScript();
        SerializedProperty waves = waveSerializedObject.FindProperty("waves");

        if (waves == null)
            return 0;

        int removedCellCount = CountOutOfBoundsCells(waves, gridSizeX, gridSizeZ);

        if (removedCellCount == 0)
            return 0;

        Undo.RecordObjects(waveSerializedObject.targetObjects, "Resize Enemy Spawn Grid");

        // Delete backwards so serialized array indices stay valid throughout the cleanup.
        for (int waveIndex = 0; waveIndex < waves.arraySize; waveIndex++)
        {
            SerializedProperty cells = waves.GetArrayElementAtIndex(waveIndex)
                                                .FindPropertyRelative("paintedCells");

            for (int cellIndex = cells.arraySize - 1; cellIndex >= 0; cellIndex--)
            {
                Vector2Int coordinate = cells.GetArrayElementAtIndex(cellIndex)
                                             .FindPropertyRelative("cellCoordinate")
                                             .vector2IntValue;

                if (!EnemySpawnerWaveBakeUtility.IsCellInsideGrid(coordinate, gridSizeX, gridSizeZ))
                    cells.DeleteArrayElementAtIndex(cellIndex);
            }
        }

        waveSerializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(waveSerializedObject.targetObject);
        GameManagementDraftSession.MarkDirty();
        return removedCellCount;
    }
    #endregion

    #region Counting Methods
    /// <summary>
    /// Counts cells invalidated by one proposed grid size without mutating the wave asset.
    /// </summary>
    /// <param name="waves">Serialized wave array to inspect.</param>
    /// <param name="gridSizeX">Proposed horizontal grid size.</param>
    /// <param name="gridSizeZ">Proposed vertical grid size.</param>
    /// <returns>Number of coordinates outside the proposed grid.</returns>
    private static int CountOutOfBoundsCells(SerializedProperty waves,
                                             int gridSizeX,
                                             int gridSizeZ)
    {
        int count = 0;

        // Inspect every parallel and sequential wave because all of them share the spawner grid.
        for (int waveIndex = 0; waveIndex < waves.arraySize; waveIndex++)
        {
            SerializedProperty cells = waves.GetArrayElementAtIndex(waveIndex)
                                                .FindPropertyRelative("paintedCells");

            for (int cellIndex = 0; cellIndex < cells.arraySize; cellIndex++)
            {
                Vector2Int coordinate = cells.GetArrayElementAtIndex(cellIndex)
                                             .FindPropertyRelative("cellCoordinate")
                                             .vector2IntValue;

                if (!EnemySpawnerWaveBakeUtility.IsCellInsideGrid(coordinate, gridSizeX, gridSizeZ))
                    count++;
            }
        }

        return count;
    }
    #endregion

    #endregion
}
