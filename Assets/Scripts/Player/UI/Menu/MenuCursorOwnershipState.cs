using UnityEngine;

/// <summary>
/// Shared ownership flag raised while a runtime menu overlay (the Settings menu or the enemy spawner tool) drives the
/// hardware cursor through its own software pointer. Cooperating gameplay systems such as <see cref="InputAuthoring"/>
/// read it to stop forcing the cursor lock, because the active UI input module uses CursorLockBehavior.OutsideScreen:
/// a locked OS cursor would make it discard every mouse pointer (including the gamepad VirtualMouse) as off-screen,
/// which is exactly what kept the in-game Settings virtual cursor from clicking anything.
/// </summary>
public static class MenuCursorOwnershipState
{
    #region Fields
    private static int activeOwnerCount;
    #endregion

    #region Properties
    /// <summary>
    /// True while at least one runtime menu overlay currently owns the hardware cursor.
    /// </summary>
    public static bool OverlayOwnsCursor
    {
        get
        {
            return activeOwnerCount > 0;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Registers one overlay as the current cursor owner. Balanced by <see cref="Release"/> when the overlay closes.
    /// </summary>
    public static void Acquire()
    {
        activeOwnerCount++;
    }

    /// <summary>
    /// Releases one overlay's cursor ownership, never dropping the shared counter below zero.
    /// </summary>
    public static void Release()
    {
        if (activeOwnerCount <= 0)
            return;

        activeOwnerCount--;
    }
    #endregion

    #region Lifecycle
    /// <summary>
    /// Clears any leaked ownership when entering play mode, so a domain-reload-free session never starts with the
    /// cursor stuck in overlay-owned state from a previous run.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnEnterPlayMode()
    {
        activeOwnerCount = 0;
    }
    #endregion

    #endregion
}
