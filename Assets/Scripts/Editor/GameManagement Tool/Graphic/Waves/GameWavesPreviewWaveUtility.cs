using UnityEditor;
using UnityEngine;

/// <summary>
/// Resolves serialized wave-cell data shared by preview drawing and direct paint interaction.
/// </summary>
internal static class GameWavesPreviewWaveUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the painted-cell array for one visible wave after refreshing its serialized view.
    /// </summary>
    /// <param name="wavePresetObject">Serialized wave preset.</param>
    /// <param name="waveIndex">Requested visible wave index.</param>
    /// <returns>Painted-cell array, or null when the index is invalid.</returns>
    public static SerializedProperty FindPaintedCells(SerializedObject wavePresetObject, int waveIndex)
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
    public static int FindCellIndex(SerializedProperty cells, Vector2Int coordinate)
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

    #endregion
}
