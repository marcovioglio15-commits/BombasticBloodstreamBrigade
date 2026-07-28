using UnityEngine;

/// <summary>
/// Converts -authored world-space presentation distances into one preauthored Canvas local space.
/// </summary>
public static class GameRoomRewardWorldCanvasLayoutUtility
{
    #region Constants
    private const float MinimumUsableScale = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Converts one horizontal world-space distance into local RectTransform units using the current Canvas scale.
    /// </summary>
    /// <param name="worldCanvas">World-space Canvas whose X scale defines the conversion.</param>
    /// <param name="worldDistance">-authored distance measured in world units.</param>
    /// <returns>Equivalent positive local Canvas distance, or the input magnitude when no usable Canvas exists.</returns>
    public static float ToLocalHorizontalDistance(Canvas worldCanvas, float worldDistance)
    {
        return ToLocalDistance(worldCanvas, worldDistance, true);
    }

    /// <summary>
    /// Converts one vertical world-space distance into local RectTransform units using the current Canvas scale.
    /// </summary>
    /// <param name="worldCanvas">World-space Canvas whose Y scale defines the conversion.</param>
    /// <param name="worldDistance">-authored distance measured in world units.</param>
    /// <returns>Equivalent positive local Canvas distance, or the input magnitude when no usable Canvas exists.</returns>
    public static float ToLocalVerticalDistance(Canvas worldCanvas, float worldDistance)
    {
        return ToLocalDistance(worldCanvas, worldDistance, false);
    }
    #endregion

    #region Conversion
    /// <summary>
    /// Converts one world-space distance through the selected Canvas axis without changing authored values at runtime.
    /// </summary>
    /// <param name="worldCanvas">World-space Canvas supplying its already-authored transform scale.</param>
    /// <param name="worldDistance">Distance measured in world units.</param>
    /// <param name="useHorizontalAxis">Whether to read the Canvas X scale instead of its Y scale.</param>
    /// <returns>Equivalent positive local Canvas distance.</returns>
    private static float ToLocalDistance(Canvas worldCanvas,
                                         float worldDistance,
                                         bool useHorizontalAxis)
    {
        float distance = Mathf.Abs(worldDistance);

        if (worldCanvas == null)
            return distance;

        Vector3 lossyScale = worldCanvas.transform.lossyScale;
        float axisScale = Mathf.Abs(useHorizontalAxis ? lossyScale.x : lossyScale.y);
        return axisScale > MinimumUsableScale ? distance / axisScale : distance;
    }
    #endregion

    #endregion
}
