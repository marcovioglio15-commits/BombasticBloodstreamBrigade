#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// Stores a world Transform pose in an authoritative portal reference frame for deterministic editor transfer.
/// </summary>
internal readonly struct GameRoomPortalRelativePose
{
    #region Fields
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one immutable portal-relative position and rotation pair.
    /// </summary>
    /// <param name="position">Position expressed in logical portal coordinates from the caller-selected center or base origin.</param>
    /// <param name="rotation">Rotation expressed relative to the canonical portal-side orientation.</param>
    public GameRoomPortalRelativePose(Vector3 position,
                                      Quaternion rotation)
    {
        Position = position;
        Rotation = rotation;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Captures one Transform relative to a portal center and orientation without involving hierarchy scale.
    /// </summary>
    /// <param name="portalPose">Authoritative source portal reference frame.</param>
    /// <param name="target">Transform whose world position and rotation are captured.</param>
    /// <returns>Immutable pose ready to apply to any other portal reference frame.</returns>
    public static GameRoomPortalRelativePose Capture(
        GameRoomPortalReferencePose portalPose,
        Transform target)
    {
        return CaptureFromOrigin(portalPose,
                                 portalPose.WorldCenter,
                                 target);
    }

    /// <summary>
    /// Captures one Transform relative to the lower center of a portal volume so floor-relative height remains stable.
    /// </summary>
    /// <param name="portalPose">Authoritative source portal reference frame containing the calculated lower extent.</param>
    /// <param name="target">Transform whose world position and rotation are captured.</param>
    /// <returns>Immutable pose ready to apply from another portal's lower center.</returns>
    public static GameRoomPortalRelativePose CaptureFromBase(
        GameRoomPortalReferencePose portalPose,
        Transform target)
    {
        return CaptureFromOrigin(portalPose,
                                 portalPose.WorldBaseCenter,
                                 target);
    }

    /// <summary>
    /// Resolves this relative pose from the center of one target portal volume.
    /// </summary>
    /// <param name="portalPose">Authoritative target portal reference frame.</param>
    /// <param name="worldPosition">Resolved world position.</param>
    /// <param name="worldRotation">Resolved world rotation.</param>
    public void Resolve(GameRoomPortalReferencePose portalPose,
                        out Vector3 worldPosition,
                        out Quaternion worldRotation)
    {
        ResolveFromOrigin(portalPose,
                          portalPose.WorldCenter,
                          out worldPosition,
                          out worldRotation);
    }

    /// <summary>
    /// Resolves this relative pose from the lower center of one target portal volume.
    /// </summary>
    /// <param name="portalPose">Authoritative target portal reference frame containing the calculated lower extent.</param>
    /// <param name="worldPosition">Resolved world position.</param>
    /// <param name="worldRotation">Resolved world rotation.</param>
    public void ResolveFromBase(GameRoomPortalReferencePose portalPose,
                                out Vector3 worldPosition,
                                out Quaternion worldRotation)
    {
        ResolveFromOrigin(portalPose,
                          portalPose.WorldBaseCenter,
                          out worldPosition,
                          out worldRotation);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Captures one Transform from an explicitly selected origin while sharing canonical portal rotation handling.
    /// </summary>
    /// <param name="portalPose">Authoritative source portal reference frame.</param>
    /// <param name="worldOrigin">World-space origin selected for this presentation category.</param>
    /// <param name="target">Transform whose world position and rotation are captured.</param>
    /// <returns>Immutable portal-oriented pose.</returns>
    private static GameRoomPortalRelativePose CaptureFromOrigin(
        GameRoomPortalReferencePose portalPose,
        Vector3 worldOrigin,
        Transform target)
    {
        Quaternion inversePortalRotation = Quaternion.Inverse(portalPose.WorldRotation);
        return new GameRoomPortalRelativePose(
            inversePortalRotation * (target.position - worldOrigin),
            inversePortalRotation * target.rotation);
    }

    /// <summary>
    /// Resolves this relative pose from an explicitly selected origin without duplicating transform mathematics.
    /// </summary>
    /// <param name="portalPose">Authoritative target portal reference frame.</param>
    /// <param name="worldOrigin">World-space origin selected for this presentation category.</param>
    /// <param name="worldPosition">Resolved world position.</param>
    /// <param name="worldRotation">Resolved world rotation.</param>
    private void ResolveFromOrigin(GameRoomPortalReferencePose portalPose,
                                   Vector3 worldOrigin,
                                   out Vector3 worldPosition,
                                   out Quaternion worldRotation)
    {
        worldPosition = worldOrigin + portalPose.WorldRotation * Position;
        worldRotation = portalPose.WorldRotation * Rotation;
    }
    #endregion

    #endregion
}
#endif
