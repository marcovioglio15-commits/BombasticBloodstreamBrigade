using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Cached runtime state for one managed power-up VFX instance.
/// /params None.
/// /returns None.
/// </summary>
internal sealed class PlayerPowerUpManagedVfxInstance
{
    #region Fields
    public GameObject SourcePrefab;
    public GameObject InstanceObject;
    public Transform InstanceTransform;
    public ParticleSystem[] ParticleSystems;
    public TrailRenderer[] TrailRenderers;
    public Entity PrefabEntity;
    public float RemainingSeconds;
    public Entity FollowTargetEntity;
    public float3 FollowPositionOffset;
    public Entity FollowValidationEntity;
    public uint FollowValidationSpawnVersion;
    public float3 Velocity;
    public float3 Position;
    public bool HasFollowTarget;
    public bool HasVelocity;
    #endregion
}
