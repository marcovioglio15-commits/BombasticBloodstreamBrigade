using UnityEngine;

/// <summary>
/// Immutable visual style used by runtime menu overlays that expose a software cursor for gamepad users.
/// </summary>
public readonly struct RuntimeMenuGamepadCursorStyle
{
    #region Fields
    public readonly Sprite Sprite;
    public readonly Color Tint;
    public readonly float Size;
    #endregion

    #region Construction
    /// <summary>
    /// Creates a cursor style from values authored on the owning runtime menu controller.
    /// </summary>
    /// <param name="sprite">Optional custom cursor sprite; null selects the generated fallback reticle.</param>
    /// <param name="tint">Tint applied to the custom sprite or to the generated reticle bars.</param>
    /// <param name="size">On-screen cursor size in pixels.</param>
    public RuntimeMenuGamepadCursorStyle(Sprite sprite, Color tint, float size)
    {
        Sprite = sprite;
        Tint = tint;
        Size = size;
    }
    #endregion
}
