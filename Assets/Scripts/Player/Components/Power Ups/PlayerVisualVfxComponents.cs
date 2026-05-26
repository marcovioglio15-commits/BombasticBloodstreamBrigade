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
