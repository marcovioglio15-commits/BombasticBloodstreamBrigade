using Unity.Collections;

/// <summary>
/// Identifies the scene or encounter that currently owns the music mix.
/// </summary>
public enum GameAudioMusicContext : byte
{
    None,
    Gameplay,
    Boss,
    MainMenu
}

/// <summary>
/// Stores an independent music event compiled into the persistent ECS audio singleton.
/// </summary>
public struct GameAudioMusicTrackConfig
{
    #region Fields
    public byte Enabled;
    public byte AutoStart;
    public byte RestartWhenPathChanges;
    public byte StopWhenDisabled;
    public FixedString512Bytes EventPath;
    public FixedString64Bytes BankName;
    public float Volume;
    #endregion
}
