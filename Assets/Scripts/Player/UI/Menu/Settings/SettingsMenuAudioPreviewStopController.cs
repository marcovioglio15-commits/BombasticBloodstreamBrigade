using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Owns delayed Settings preview-stop coroutine state independently from the main menu controller.
/// </summary>
internal sealed class SettingsMenuAudioPreviewStopController : IDisposable
{
    #region Fields
    private readonly MonoBehaviour coroutineOwner;
    private readonly float stopDelaySeconds;
    private Coroutine stopCoroutine;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one reusable delayed-stop controller for a Settings menu instance.
    /// </summary>
    /// <param name="coroutineOwnerValue">Active component used to schedule the delay.</param>
    /// <param name="stopDelaySecondsValue">Unscaled quiet period before a preview stops.</param>
    public SettingsMenuAudioPreviewStopController(MonoBehaviour coroutineOwnerValue,
                                                  float stopDelaySecondsValue)
    {
        coroutineOwner = coroutineOwnerValue;
        stopDelaySeconds = Mathf.Max(0.05f, stopDelaySecondsValue);
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Restarts the quiet-period delay after a slider preview trigger.
    /// </summary>
    public void Queue()
    {
        if (coroutineOwner == null)
            return;

        if (stopCoroutine != null)
            coroutineOwner.StopCoroutine(stopCoroutine);

        stopCoroutine = coroutineOwner.StartCoroutine(StopAfterDelay());
    }

    /// <summary>
    /// Cancels delayed work and stops the current preview immediately.
    /// </summary>
    public void StopNow()
    {
        if (stopCoroutine != null && coroutineOwner != null)
            coroutineOwner.StopCoroutine(stopCoroutine);

        stopCoroutine = null;
        GameAudioSettingsFmodRuntimeUtility.StopPreviewImmediate();
    }

    /// <summary>
    /// Releases coroutine work when the owning Settings controller is destroyed.
    /// </summary>
    public void Dispose()
    {
        StopNow();
    }
    #endregion

    #region Coroutine
    /// <summary>
    /// Stops the tracked preview after the configured unscaled quiet period.
    /// </summary>
    /// <returns>Enumerator used by Unity coroutine scheduling.</returns>
    private IEnumerator StopAfterDelay()
    {
        yield return new WaitForSecondsRealtime(stopDelaySeconds);
        stopCoroutine = null;
        GameAudioSettingsFmodRuntimeUtility.StopPreviewEvent();
    }
    #endregion

    #endregion
}
