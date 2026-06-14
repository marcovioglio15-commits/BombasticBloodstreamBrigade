using UnityEngine;

/// <summary>
/// Immutable visual style for the runtime spawner tool gamepad cursor, authored on
/// <see cref="EnemySpawnerRuntimeToolPanelController"/> and read once when the cursor is built. When
/// <see cref="Sprite"/> is null the navigation controller builds a generated crosshair reticle as a fallback;
/// otherwise the sprite drives the cursor visual.
/// </summary>
public readonly struct EnemySpawnerRuntimeToolCursorStyle
{
    #region Fields
    public readonly Sprite Sprite;
    public readonly Color Tint;
    public readonly float Size;
    #endregion

    #region Construction
    /// <summary>
    /// Creates a cursor style from the values authored on the spawner tool panel controller.
    /// </summary>
    /// <param name="sprite">Optional custom cursor sprite; null selects the generated fallback reticle.</param>
    /// <param name="tint">Tint applied to the custom sprite or to the generated reticle bars.</param>
    /// <param name="size">On-screen cursor size in pixels.</param>
    public EnemySpawnerRuntimeToolCursorStyle(Sprite sprite, Color tint, float size)
    {
        Sprite = sprite;
        Tint = tint;
        Size = size;
    }
    #endregion
}
