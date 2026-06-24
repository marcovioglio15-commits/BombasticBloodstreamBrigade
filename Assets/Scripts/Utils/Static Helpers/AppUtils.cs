using System.Runtime.InteropServices;

public static class AppUtils
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void BombasticWebGLQuit();
#endif

    /// <summary>
    /// Quits the application if built; stops Play Mode if running inside the Editor.
    /// </summary>
    public static void QuitGame()
    {
#if UNITY_EDITOR
        // Stop Play Mode when testing in the Editor
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
        // Browsers cannot close a user-owned tab. End the session and provide a visible browser-safe exit state.
        BombasticWebGLQuit();
        UnityEngine.Application.Quit();
#else
        // Quit the application when built
        UnityEngine.Application.Quit();
#endif
    }
}
