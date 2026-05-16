using UnityEngine;

/// <summary>
/// Defines the primitive drawing operations required by the shared runtime gizmo rendering utility.
/// none.
/// </summary>
public interface IRuntimeGizmoPrimitiveDrawer
{
    #region Methods

    #region Drawing
    /// <summary>
    /// Draws one planar wire disc centered on the supplied world position.
    /// </summary>
    /// <param name="center">World-space center of the disc.</param>
    /// <param name="radius">Radius expressed in gameplay world units.</param>
    /// <param name="color">Final line color used by the active rendering backend.</param>
    void DrawWireDisc(Vector3 center, float radius, Color color);

    /// <summary>
    /// Draws one directional indicator starting from a world-space origin.
    /// </summary>
    /// <param name="origin">World-space starting point of the vector.</param>
    /// <param name="direction">World-space direction expected to be normalized or safely normalizable.</param>
    /// <param name="length">Final vector length expressed in gameplay world units.</param>
    /// <param name="color">Final line color used by the active rendering backend.</param>
    void DrawDirection(Vector3 origin, Vector3 direction, float length, Color color);

    /// <summary>
    /// Draws one straight world-space link between two positions.
    /// </summary>
    /// <param name="start">Link starting point in world space.</param>
    /// <param name="end">Link end point in world space.</param>
    /// <param name="color">Final line color used by the active rendering backend.</param>
    void DrawLink(Vector3 start, Vector3 end, Color color);

    /// <summary>
    /// Draws one compact marker used to highlight a world-space point of interest.
    /// </summary>
    /// <param name="position">World-space marker position.</param>
    /// <param name="radius">Marker size hint expressed in gameplay world units.</param>
    /// <param name="color">Final marker color used by the active rendering backend.</param>
    void DrawMarker(Vector3 position, float radius, Color color);

    /// <summary>
    /// Draws one short text label anchored to a world-space position.
    /// </summary>
    /// <param name="position">World-space label anchor.</param>
    /// <param name="text">Text shown by the active rendering backend.</param>
    void DrawLabel(Vector3 position, string text);
    #endregion

    #endregion
}
