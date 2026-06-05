using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Singleton runtime configuration baked from GameAudioManagerPreset.
/// </summary>
public struct GameAudioRuntimeConfig : IComponentData
{
    public byte Enabled;
    public byte LogMissingEventPaths;
    public byte BackgroundMusicEnabled;
    public byte BackgroundMusicAutoStart;
    public byte BackgroundMusicRestartWhenPathChanges;
    public byte BackgroundMusicStopWhenDisabled;
    public FixedString512Bytes BackgroundMusicEventPath;
    public FixedString64Bytes BackgroundMusicBankName;
    public float MasterVolume;
    public float BackgroundMusicVolume;
    public float DefaultMinimumDistance;
    public float DefaultMaximumDistance;
}

/// <summary>
/// Baked FMOD event binding used by runtime audio request playback.
/// </summary>
public struct GameAudioEventBindingElement : IBufferElementData
{
    public GameAudioEventId EventId;
    public FixedString64Bytes EventCode;
    public FixedString512Bytes EventPath;
    public float Volume;
    public float Pitch;
    public byte Spatialize;
    public float MinimumDistance;
    public float MaximumDistance;
    public byte SingleInstance;
    public byte RateLimitEnabled;
    public int MaxPlaysPerWindow;
    public float WindowSeconds;
}

/// <summary>
/// Runtime request emitted by gameplay systems and consumed by GameAudioPlaybackSystem. When StopRequest is set
/// the playback system stops the previously stored single-instance voice for EventId instead of triggering a new
/// playback, so continuous events (looped or long clips) can be silenced from gameplay state changes.
/// </summary>
public struct GameAudioEventRequest : IBufferElementData
{
    public GameAudioEventId EventId;
    public float3 Position;
    public byte HasPosition;
    public byte StopRequest;
    public float VolumeMultiplier;
    public float PitchMultiplier;
}

/// <summary>
/// Runtime rate-limit state tracked per event ID by the playback system.
/// </summary>
public struct GameAudioRateLimitStateElement : IBufferElementData
{
    public GameAudioEventId EventId;
    public float WindowStartTime;
    public int PlaysInWindow;
}
