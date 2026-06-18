using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Shared helpers used by enemy proximity systems backed by spatial hash lookups.
/// </summary>
public static class EnemySpatialHashUtility
{
    #region Constants
    public const float MinimumCellSize = 0.25f;
    #endregion

    #region Methods

    #region Build
    public static float ResolveCellSize(float maximumRadius)
    {
        return math.max(MinimumCellSize, maximumRadius);
    }

    public static void BuildCellMap(in NativeArray<float3> positions,
                                    float inverseCellSize,
                                    ref NativeParallelMultiHashMap<int, int> cellMap)
    {
        for (int enemyIndex = 0; enemyIndex < positions.Length; enemyIndex++)
        {
            float3 position = positions[enemyIndex];
            int cellX = (int)math.floor(position.x * inverseCellSize);
            int cellY = (int)math.floor(position.z * inverseCellSize);
            int cellKey = EncodeCell(cellX, cellY);
            cellMap.Add(cellKey, enemyIndex);
        }
    }
    #endregion

    #region Query
    public static void ResolveCellBounds(float3 center,
                                         float radius,
                                         float inverseCellSize,
                                         out int minCellX,
                                         out int maxCellX,
                                         out int minCellY,
                                         out int maxCellY)
    {
        float queryRadius = math.max(0f, radius);
        float minX = center.x - queryRadius;
        float maxX = center.x + queryRadius;
        float minY = center.z - queryRadius;
        float maxY = center.z + queryRadius;
        minCellX = (int)math.floor(minX * inverseCellSize);
        maxCellX = (int)math.floor(maxX * inverseCellSize);
        minCellY = (int)math.floor(minY * inverseCellSize);
        maxCellY = (int)math.floor(maxY * inverseCellSize);
    }
    #endregion

    #region Hash
    public static int EncodeCell(int x, int y)
    {
        unchecked
        {
            return (x * 73856093) ^ (y * 19349663);
        }
    }
    #endregion

    #endregion
}
