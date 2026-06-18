using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Keeps the short managed Laser Beam shutdown tail attached to the current player muzzle pose while it fades out.
/// </summary>
internal static class PlayerLaserBeamPresentationShutdownTailUtility
{
    #region Constants
    internal const float DurationSeconds = 0.12f;
    private const float DirectionLengthEpsilon = 1e-6f;
    private static readonly Vector3 UpAxis = Vector3.up;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Stores the latest active muzzle anchor and forward direction used as the follow reference for a future shutdown tail.
    /// </summary>
    /// <param name="managedInstance">Managed beam instance that owns the tail state.</param>
    /// <param name="anchorPoint">Current lifted source anchor in world space.</param>
    /// <param name="direction">Current planar beam direction in world space.</param>
    public static void RecordActivePose(PlayerLaserBeamManagedInstance managedInstance,
                                        float3 anchorPoint,
                                        float3 direction)
    {
        if (managedInstance == null)
            return;

        if (!IsFinite(anchorPoint))
            return;

        managedInstance.ShutdownTailAnchorPoint = anchorPoint;
        managedInstance.ShutdownTailDirection = ResolvePlanarDirection(direction, managedInstance.ShutdownTailDirection);
        managedInstance.ShutdownTailAnchorInitialized = 1;
    }

    /// <summary>
    /// Moves and rotates the fading tail so its remembered source anchor follows the current player muzzle pose.
    /// </summary>
    /// <param name="managedInstance">Managed beam instance currently fading out.</param>
    /// <param name="anchorPoint">Current lifted source anchor in world space.</param>
    /// <param name="direction">Current planar player beam direction in world space.</param>
    public static void FollowShutdownTail(PlayerLaserBeamManagedInstance managedInstance,
                                          float3 anchorPoint,
                                          float3 direction)
    {
        if (managedInstance == null ||
            managedInstance.RootTransform == null ||
            managedInstance.ShutdownTailActive == 0)
            return;

        if (!IsFinite(anchorPoint))
            return;

        if (managedInstance.ShutdownTailAnchorInitialized == 0)
        {
            RecordActivePose(managedInstance, anchorPoint, direction);
            return;
        }

        float3 previousAnchorPoint = managedInstance.ShutdownTailAnchorPoint;
        float3 previousDirection = ResolvePlanarDirection(managedInstance.ShutdownTailDirection, new float3(0f, 0f, 1f));
        float3 currentDirection = ResolvePlanarDirection(direction, previousDirection);
        Quaternion rotationDelta = ResolvePlanarRotationDelta(previousDirection, currentDirection);
        Transform rootTransform = managedInstance.RootTransform;
        Vector3 previousAnchor = ToVector3(previousAnchorPoint);
        Vector3 currentAnchor = ToVector3(anchorPoint);
        Vector3 rotatedRootOffset = rotationDelta * (rootTransform.position - previousAnchor);

        // The body mesh stores world-space vertices under a neutral root, so the tail root carries only the follow delta.
        rootTransform.position = currentAnchor + rotatedRootOffset;
        rootTransform.rotation = rotationDelta * rootTransform.rotation;
        rootTransform.localScale = Vector3.one;
        managedInstance.ShutdownTailAnchorPoint = anchorPoint;
        managedInstance.ShutdownTailDirection = currentDirection;
    }

    /// <summary>
    /// Restores the managed root transform to neutral world space before active rendering rebuilds world-space meshes.
    /// </summary>
    /// <param name="managedInstance">Managed beam instance whose root transform should be reset.</param>
    public static void ResetRootTransform(PlayerLaserBeamManagedInstance managedInstance)
    {
        if (managedInstance == null || managedInstance.RootTransform == null)
            return;

        managedInstance.RootTransform.position = Vector3.zero;
        managedInstance.RootTransform.rotation = Quaternion.identity;
        managedInstance.RootTransform.localScale = Vector3.one;
    }

    /// <summary>
    /// Clears the remembered shutdown follow reference after a hard disable or destruction.
    /// </summary>
    /// <param name="managedInstance">Managed beam instance whose shutdown pose should be cleared.</param>
    public static void ClearPose(PlayerLaserBeamManagedInstance managedInstance)
    {
        if (managedInstance == null)
            return;

        managedInstance.ShutdownTailAnchorPoint = float3.zero;
        managedInstance.ShutdownTailDirection = new float3(0f, 0f, 1f);
        managedInstance.ShutdownTailAnchorInitialized = 0;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves a normalized planar direction with a stable fallback when the input is degenerate.
    /// </summary>
    /// <param name="direction">Requested direction.</param>
    /// <param name="fallbackDirection">Fallback direction used when the input is invalid.</param>
    /// <returns>Normalized planar direction.</returns>
    private static float3 ResolvePlanarDirection(float3 direction,
                                                 float3 fallbackDirection)
    {
        direction.y = 0f;

        if (IsFinite(direction) && math.lengthsq(direction) > DirectionLengthEpsilon)
            return math.normalizesafe(direction, new float3(0f, 0f, 1f));

        fallbackDirection.y = 0f;

        if (IsFinite(fallbackDirection) && math.lengthsq(fallbackDirection) > DirectionLengthEpsilon)
            return math.normalizesafe(fallbackDirection, new float3(0f, 0f, 1f));

        return new float3(0f, 0f, 1f);
    }

    /// <summary>
    /// Builds the yaw-only rotation that moves the previous beam direction toward the current direction.
    /// </summary>
    /// <param name="previousDirection">Previous normalized planar direction.</param>
    /// <param name="currentDirection">Current normalized planar direction.</param>
    /// <returns>World-space yaw rotation delta.</returns>
    private static Quaternion ResolvePlanarRotationDelta(float3 previousDirection,
                                                         float3 currentDirection)
    {
        float dot = math.clamp(math.dot(previousDirection, currentDirection), -1f, 1f);
        float crossY = math.cross(previousDirection, currentDirection).y;
        float angleDegrees = math.degrees(math.atan2(crossY, dot));
        return Quaternion.AngleAxis(angleDegrees, UpAxis);
    }

    /// <summary>
    /// Checks whether one point can be safely applied to a managed Transform.
    /// </summary>
    /// <param name="value">Point value to validate.</param>
    /// <returns>True when every component is finite.</returns>
    private static bool IsFinite(float3 value)
    {
        return !float.IsNaN(value.x) &&
               !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) &&
               !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) &&
               !float.IsInfinity(value.z);
    }

    /// <summary>
    /// Converts one float3 to a UnityEngine vector for managed transform operations.
    /// </summary>
    /// <param name="value">Source math vector.</param>
    /// <returns>UnityEngine vector with matching components.</returns>
    private static Vector3 ToVector3(float3 value)
    {
        return new Vector3(value.x, value.y, value.z);
    }
    #endregion

    #endregion
}
