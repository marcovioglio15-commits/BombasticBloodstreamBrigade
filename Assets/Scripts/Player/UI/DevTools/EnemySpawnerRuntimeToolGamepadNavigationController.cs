using System;

/// <summary>
/// Compatibility wrapper that connects the enemy spawner runtime tool to the shared runtime menu gamepad cursor.
/// </summary>
public sealed class EnemySpawnerRuntimeToolGamepadNavigationController : IDisposable
{
    #region Constants
    private const string CursorRootName = "EnemySpawnerRuntimeToolGamepadCursor";
    #endregion

    #region Fields
    private readonly RuntimeMenuGamepadNavigationController controller;
    #endregion

    #region Construction
    /// <summary>
    /// Creates the delegated gamepad cursor controller used by the enemy spawner runtime tool.
    /// </summary>
    /// <param name="closeRequested">Callback invoked when the player presses UI cancel.</param>
    /// <param name="cursorStyle">Authored enemy spawner cursor style.</param>
    public EnemySpawnerRuntimeToolGamepadNavigationController(Action closeRequested, EnemySpawnerRuntimeToolCursorStyle cursorStyle)
    {
        RuntimeMenuGamepadCursorStyle sharedStyle = new RuntimeMenuGamepadCursorStyle(cursorStyle.Sprite,
                                                                                      cursorStyle.Tint,
                                                                                      cursorStyle.Size);
        controller = new RuntimeMenuGamepadNavigationController(CursorRootName, closeRequested, sharedStyle);
    }
    #endregion

    #region Methods

    #region Activation
    /// <summary>
    /// Activates gamepad cursor navigation while the enemy spawner overlay is open.
    /// </summary>
    public void Activate()
    {
        controller.Activate();
    }

    /// <summary>
    /// Deactivates gamepad cursor navigation and restores normal menu pointer behavior.
    /// </summary>
    public void Deactivate()
    {
        controller.Deactivate();
    }
    #endregion

    #region Disposal
    /// <summary>
    /// Releases the delegated shared cursor controller.
    /// </summary>
    public void Dispose()
    {
        controller.Dispose();
    }
    #endregion

    #endregion
}
