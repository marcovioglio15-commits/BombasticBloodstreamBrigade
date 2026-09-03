using UnityEngine;

/// <summary>
/// Tracks the camera transform writer that owns procedural traversal framing for the current rendered frame.
/// </summary>
internal static class PlayerCameraTraversalFramingUtility
{
    #region State
    private static int cameraInstanceId;
    private static int writeFrame = -1;
    #endregion

    #region Methods

    #region Ownership Methods
    /// <summary>
    /// Records that traversal continuity wrote the supplied camera transform during the current rendered frame.
    /// </summary>
    /// <param name="camera">Gameplay camera whose transform was written by traversal continuity.</param>
    internal static void MarkWritten(Camera camera)
    {
        cameraInstanceId = camera.GetInstanceID();
        writeFrame = Time.frameCount;
    }

    /// <summary>
    /// Checks whether traversal continuity already wrote the supplied camera during the current rendered frame.
    /// </summary>
    /// <param name="camera">Gameplay camera whose transform ownership is being queried.</param>
    /// <returns>True when traversal continuity owns this camera for the current rendered frame.</returns>
    internal static bool Owns(Camera camera)
    {
        return camera != null &&
               writeFrame == Time.frameCount &&
               cameraInstanceId == camera.GetInstanceID();
    }
    #endregion

    #endregion
}
