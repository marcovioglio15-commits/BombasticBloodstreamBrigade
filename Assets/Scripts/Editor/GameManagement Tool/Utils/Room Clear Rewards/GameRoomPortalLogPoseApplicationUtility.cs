#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies a resolved world pose to a portal log while preserving the serialized coordinates required by nested RectTransforms.
/// </summary>
internal static class GameRoomPortalLogPoseApplicationUtility
{
    #region Constants
    private const float PositionToleranceSquared = 0.00000001f;
    private const float RotationTolerance = 0.0001f;
    private const string UndoName = "Synchronize Room Reward Log Pose";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies one resolved world pose and explicitly persists UI anchored coordinates when the log belongs to a nested prefab.
    /// </summary>
    /// <param name="logTransform">Target log Transform or RectTransform receiving the resolved pose.</param>
    /// <param name="worldPosition">Portal-relative position already resolved in target-scene world space.</param>
    /// <param name="worldRotation">Portal-relative rotation already resolved in target-scene world space.</param>
    /// <param name="useUndo">Whether the target belongs to a scene that was already open before synchronization.</param>
    /// <returns>True when the target pose required a serialized change.</returns>
    internal static bool Apply(Transform logTransform,
                               Vector3 worldPosition,
                               Quaternion worldRotation,
                               bool useUndo)
    {
        if ((logTransform.position - worldPosition).sqrMagnitude <=
                PositionToleranceSquared &&
            Quaternion.Angle(logTransform.rotation, worldRotation) <=
                RotationTolerance)
        {
            return false;
        }

        if (useUndo)
            Undo.RecordObject(logTransform, UndoName);

        // Convert once to parent-local coordinates so nested prefab data does not depend on a temporary world-space solve.
        Transform parent = logTransform.parent;
        Vector3 localPosition = parent != null
            ? parent.InverseTransformPoint(worldPosition)
            : worldPosition;
        Quaternion localRotation = parent != null
            ? Quaternion.Inverse(parent.rotation) * worldRotation
            : worldRotation;

        logTransform.localPosition = localPosition;
        logTransform.localRotation = localRotation;

        // A world-space Canvas root serializes X/Y through anchoredPosition and Z through localPosition.
        if (logTransform is RectTransform rectTransform)
            rectTransform.anchoredPosition3D = localPosition;

        EditorUtility.SetDirty(logTransform);

        if (PrefabUtility.IsPartOfPrefabInstance(logTransform))
            PrefabUtility.RecordPrefabInstancePropertyModifications(logTransform);

        return true;
    }
    #endregion

    #endregion
}
#endif
