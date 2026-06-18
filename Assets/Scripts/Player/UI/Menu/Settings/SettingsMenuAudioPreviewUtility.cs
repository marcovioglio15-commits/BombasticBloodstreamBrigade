using System;
using UnityEngine;

/// <summary>
/// Stateless audio-preview helpers used by the runtime Settings menu sliders.
/// </summary>
internal static class SettingsMenuAudioPreviewUtility
{
    #region Methods
    /// <summary>
    /// Plays the Master slider preview according to the active Settings Manager preview mode.
    /// </summary>
    /// <param name="audioPreviewSet">Resolved preview set from runtime config.</param>
    /// <param name="volume">Preview volume scalar driven by the Master slider position.</param>
    /// <param name="queuePreviewStop">Callback that schedules preview stop after slider movement settles.</param>
    public static void PlayMasterPreview(in GameAudioSettingsPreviewSet audioPreviewSet,
                                         float volume,
                                         Action queuePreviewStop)
    {
        if (audioPreviewSet.MasterPlaysAllOthers)
        {
            float previewVolume = Mathf.Max(0f, volume);
            GameAudioSettingsFmodRuntimeUtility.PlayPreviewEvents(audioPreviewSet.Sfx.EventPath,
                                                                  audioPreviewSet.Sfx.BankName,
                                                                  previewVolume,
                                                                  audioPreviewSet.Music.EventPath,
                                                                  audioPreviewSet.Music.BankName,
                                                                  previewVolume,
                                                                  true);
            InvokeQueueStop(queuePreviewStop);
            return;
        }

        PlayPreview(audioPreviewSet.Master, volume, queuePreviewStop);
    }

    /// <summary>
    /// Plays or refreshes the preview event for an adjusted audio slider.
    /// </summary>
    /// <param name="previewEvent">FMOD preview event resolved from the Audio Manager preset.</param>
    /// <param name="volume">Preview volume scalar.</param>
    /// <param name="queuePreviewStop">Callback that schedules preview stop after slider movement settles.</param>
    public static void PlayPreview(GameAudioSettingsPreviewEvent previewEvent, float volume, Action queuePreviewStop)
    {
        GameAudioSettingsFmodRuntimeUtility.PlayPreviewEvent(previewEvent.EventPath,
                                                             previewEvent.BankName,
                                                             Mathf.Max(0f, volume),
                                                             true);
        InvokeQueueStop(queuePreviewStop);
    }

    /// <summary>
    /// Invokes the optional delayed-stop scheduler.
    /// </summary>
    /// <param name="queuePreviewStop">Callback that schedules preview stop.</param>
    private static void InvokeQueueStop(Action queuePreviewStop)
    {
        if (queuePreviewStop != null)
            queuePreviewStop.Invoke();
    }
    #endregion
}
