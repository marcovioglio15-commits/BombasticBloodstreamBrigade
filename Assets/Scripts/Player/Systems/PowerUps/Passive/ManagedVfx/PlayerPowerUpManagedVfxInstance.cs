using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Cached runtime state for one managed power-up VFX instance.
/// </summary>
internal sealed class PlayerPowerUpManagedVfxInstance
{
    #region Fields
    public GameObject SourcePrefab;
    public GameObject InstanceObject;
    public Transform InstanceTransform;
    public Vector3 RootBaseLocalScale;
    public ParticleSystem[] ParticleSystems;
    public TrailRenderer[] TrailRenderers;
    public float[] ParticleSystemBaseSimulationSpeeds;
    public bool[] ParticleSystemBaseLooping;
    public ParticleSystem.MinMaxGradient[] ParticleSystemBaseStartColors;
    public bool[] ParticleSystemBaseColorOverLifetimeEnabled;
    public ParticleSystem.MinMaxGradient[] ParticleSystemBaseColorOverLifetimeColors;
    public float[] TrailRendererBaseWidths;
    public float[] TrailRendererBaseTimes;
    public Entity PrefabEntity;
    public int RefreshKey;
    public float RemainingSeconds;
    public Entity FollowTargetEntity;
    public float3 FollowPositionOffset;
    public Entity FollowValidationEntity;
    public uint FollowValidationSpawnVersion;
    public float3 Velocity;
    public float3 Position;
    public quaternion Rotation;
    public bool HasFollowTarget;
    public bool HasVelocity;
    public bool FollowMuzzlePose;
    public bool FollowTargetRotation;
    public bool DetachWhenFollowTargetInvalid;
    public bool KeepAliveWhileFollowTargetValid;
    public bool RestartOldestOnCap;
    public ulong ActivationSequence;
    #endregion
}
