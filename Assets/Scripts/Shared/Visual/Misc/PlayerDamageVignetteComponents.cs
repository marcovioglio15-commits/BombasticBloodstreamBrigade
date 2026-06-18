using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

#region Enums
/// <summary>
/// Identifies which damage vignette channel is currently advancing through its fade state machine.
/// The presentation system selects exactly one active channel per pulse: pure-shield hits drive Shield, hits that reach health drive Health.
/// </summary>
public enum PlayerDamageVignetteChannel : byte
{
    None = 0,
    Shield = 1,
    Health = 2
}

/// <summary>
/// Tracks the lifecycle of one in-flight vignette pulse so the presentation system can advance fade-in then fade-out independently of the trigger source.
/// </summary>
public enum PlayerDamageVignettePhase : byte
{
    Idle = 0,
    FadeIn = 1,
    FadeOut = 2
}
#endregion

#region Components
/// <summary>
/// Stores immutable per-channel tuning baked from the active <see cref="PlayerVisualPreset"/> Damage Feedback section.
/// Holds the sprite reference, peak alpha, tint and fade durations for both the shield-only and the health damage overlays.
/// Consumed by <see cref="PlayerDamageVignettePresentationSystem"/> and by the scene UI binder.
/// </summary>
public struct PlayerDamageVignetteConfig : IComponentData
{
    #region Fields
    public UnityObjectRef<Sprite> ShieldSprite;
    public float4 ShieldTint;
    public float ShieldMaxAlpha;
    public float ShieldFadeInSeconds;
    public float ShieldFadeOutSeconds;

    public UnityObjectRef<Sprite> HealthSprite;
    public float4 HealthTint;
    public float HealthMaxAlpha;
    public float HealthFadeInSeconds;
    public float HealthFadeOutSeconds;
    #endregion
}

/// <summary>
/// Stores mutable per-channel playback state advanced every presentation frame.
/// Tracks the previous health/shield snapshot used by the presentation system to detect drops without modifying any damage call-site.
/// Holds the active channel, current phase, elapsed seconds in the active phase and the current overlay alpha read by the scene UI binder.
/// </summary>
public struct PlayerDamageVignetteState : IComponentData
{
    #region Fields
    public float PreviousHealth;
    public float PreviousShield;
    public byte Initialized;

    public PlayerDamageVignetteChannel ActiveChannel;
    public PlayerDamageVignettePhase ActivePhase;
    public float ActiveElapsedSeconds;
    public float ActiveAlpha;
    public byte ActiveTriggerPulseId;
    #endregion
}
#endregion
