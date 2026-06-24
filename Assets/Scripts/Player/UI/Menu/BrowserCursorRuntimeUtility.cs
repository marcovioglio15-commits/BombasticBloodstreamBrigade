using UnityEngine;

/// <summary>
/// Applies cursor presentation without requesting browser pointer-lock permissions in WebGL iframe builds.
/// </summary>
public static class BrowserCursorRuntimeUtility
{
    #region Methods
    /// <summary>
    /// Applies visibility and lock state. WebGL keeps the pointer unlocked because browser pointer lock can only
    /// be granted from a direct user gesture and is unnecessary while the controller cursor is hidden.
    /// </summary>
    /// <param name="visible">Requested hardware cursor visibility.</param>
    /// <param name="locked">Requested desktop cursor lock state.</param>
    public static void Apply(bool visible, bool locked)
    {
        Cursor.visible = visible;

#if UNITY_WEBGL && !UNITY_EDITOR
        Cursor.lockState = CursorLockMode.None;
#else
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
#endif
    }
    #endregion
}
