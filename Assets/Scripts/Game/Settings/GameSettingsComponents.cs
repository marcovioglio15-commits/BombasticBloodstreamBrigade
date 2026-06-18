using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Singleton runtime configuration baked from GameSettingsManagerPreset for the runtime Settings menu.
/// </summary>
public struct GameSettingsRuntimeConfig : IComponentData
{
    #region Fields
    public FixedString128Bytes MasterBusPath;
    public FixedString128Bytes SfxBusPath;
    public FixedString128Bytes MusicBusPath;
    public FixedString512Bytes MasterPreviewEventPath;
    public FixedString64Bytes MasterPreviewBankName;
    public FixedString512Bytes SfxPreviewEventPath;
    public FixedString64Bytes SfxPreviewBankName;
    public FixedString512Bytes MusicPreviewEventPath;
    public FixedString64Bytes MusicPreviewBankName;
    public byte MasterPreviewPlaysAllOthers;
    public float DefaultMasterVolume;
    public float DefaultSfxVolume;
    public float DefaultMusicVolume;
    public byte DefaultVisualPointerEnabled;
    public byte DefaultFullscreenEnabled;
    public int DefaultFrameRateLimit;
    public float DefaultDamageRumbleMultiplier;
    public float DefaultFireRumbleMultiplier;
    public int WindowedWidth;
    public int WindowedHeight;
    public RuntimeMenuGamepadNavigationMode GamepadNavigationMode;
    public FixedString64Bytes NavigateActionName;
    public FixedString64Bytes SubmitActionName;
    public FixedString64Bytes CancelActionName;
    public float NavigateDeadzone;
    public float NavigationRepeatDelaySeconds;
    public float NavigationRepeatIntervalSeconds;
    #endregion
}
