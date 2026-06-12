using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Selects when stat-increase VFX should be spawned for player health or shield changes.
/// </summary>
public enum PlayerStatIncreaseVfxTriggerMode : byte
{
    EveryIncrease = 0,
    MaximumValueIncreaseOnly = 1
}

/// <summary>
/// Runtime VFX settings spawned when player health increases.
/// </summary>
public struct PlayerHealthIncreaseVfxConfig : IComponentData
{
    public Entity PrefabEntity;
    public UnityObjectRef<GameObject> SourcePrefab;
    public float3 SpawnOffset;
    public float UniformScale;
    public float LifetimeSeconds;
    public PlayerStatIncreaseVfxTriggerMode TriggerMode;
}

/// <summary>
/// Runtime VFX settings spawned when player shield increases.
/// </summary>
public struct PlayerShieldIncreaseVfxConfig : IComponentData
{
    public Entity PrefabEntity;
    public UnityObjectRef<GameObject> SourcePrefab;
    public float3 SpawnOffset;
    public float UniformScale;
    public float LifetimeSeconds;
    public PlayerStatIncreaseVfxTriggerMode TriggerMode;
}

/// <summary>
/// Tracks previous health and shield values so increase VFX can be edge-triggered.
/// </summary>
public struct PlayerStatIncreaseVfxRuntimeState : IComponentData
{
    public float PreviousHealth;
    public float PreviousMaxHealth;
    public float PreviousShield;
    public float PreviousMaxShield;
    public byte Initialized;
}

/// <summary>
/// Runtime VFX settings attached to player projectiles from spawn until despawn.
/// </summary>
public struct PlayerProjectileAttachedVfxConfig : IComponentData
{
    public Entity PrefabEntity;
    public UnityObjectRef<GameObject> SourcePrefab;
    public float3 SpawnOffset;
    public float UniformScale;
    public float LifetimeSeconds;
}

/// <summary>
/// Runtime settings for one projectile-death VFX event.
/// </summary>
public struct PlayerProjectileDeathVfxEventConfig
{
    public Entity PrefabEntity;
    public UnityObjectRef<GameObject> SourcePrefab;
    public float3 SpawnOffset;
    public float UniformScale;
    public float LifetimeSeconds;
    public byte Enabled;
}

/// <summary>
/// Runtime VFX settings used when player projectiles expire without previous enemy hits.
/// </summary>
public struct PlayerProjectileDeathVfxConfig : IComponentData
{
    public PlayerProjectileDeathVfxEventConfig RangeOrLifetime;
    public PlayerProjectileDeathVfxEventConfig TerminalWallHit;
}

/// <summary>
/// Immutable projectile-death VFX baseline used to rebuild runtime-scaled settings.
/// </summary>
public struct PlayerBaseProjectileDeathVfxConfig : IComponentData
{
    public PlayerProjectileDeathVfxConfig Config;
}

/// <summary>
/// Tracks the shared runtime scaling hash last applied to projectile-death VFX settings.
/// </summary>
public struct PlayerProjectileDeathVfxScalingState : IComponentData
{
    public uint LastScalableStatsHash;
    public byte Initialized;
}

/// <summary>
/// Runtime settings controlling a designer-authored Jetpack VFX inside the Visual Player hierarchy.
/// </summary>
public struct PlayerJetpackVfxConfig : IComponentData
{
    public FixedString128Bytes RuntimeReference;
    public float MovementSpeedThreshold;
    public float RotationSpeedThresholdDegrees;
    public float SpeedForMaximumScale;
    public float NormalScaleSpeedPercent;
    public float ScaleVariationPercent;
    public PlayerJetpackVfxActivationMode ActivationMode;
    public byte ScaleWithMovementSpeed;
}

/// <summary>
/// Immutable Jetpack VFX baseline used to rebuild runtime-scaled settings.
/// </summary>
public struct PlayerBaseJetpackVfxConfig : IComponentData
{
    public PlayerJetpackVfxConfig Config;
}

/// <summary>
/// Tracks the shared runtime scaling hash last applied to Jetpack VFX settings.
/// </summary>
public struct PlayerJetpackVfxScalingState : IComponentData
{
    public uint LastScalableStatsHash;
    public byte Initialized;
}

/// <summary>
/// Stores previous rotation plus the desired visibility and authored-scale multiplier consumed by Jetpack VFX presentation.
/// </summary>
public struct PlayerJetpackVfxRuntimeState : IComponentData
{
    public quaternion PreviousRotation;
    public float DesiredScaleMultiplier;
    public byte Initialized;
    public byte DesiredVisible;
}
