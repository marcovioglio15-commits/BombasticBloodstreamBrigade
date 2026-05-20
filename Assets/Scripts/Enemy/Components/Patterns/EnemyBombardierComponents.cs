using Unity.Entities;
using Unity.Mathematics;

#region Enemy Bombardier Components
/// <summary>
/// Runtime Bombardier module configuration compiled from active Weapon Interaction patterns.
/// </summary>
public struct EnemyBombardierConfigElement : IBufferElementData
{
    public EnemyShooterAimPolicy AimPolicy;
    public EnemyShooterMovementPolicy MovementPolicy;
    public EnemyBombardierTargetingMode InReachTargetingMode;
    public EnemyBombardierTargetingMode OutOfReachTargetingMode;
    public EnemyBombardierLaunchPattern LaunchPattern;
    public EnemyBombardierTrajectoryMode TrajectoryMode;
    public float FireInterval;
    public int BurstCount;
    public float AimWindupSeconds;
    public float PreLaunchStopSeconds;
    public float PostLaunchStopSeconds;
    public float IntraBurstDelay;
    public byte UseMinimumRange;
    public float MinimumRange;
    public byte UseMaximumRange;
    public float MaximumRange;
    public byte ExclusiveLookDirectionControl;
    public EnemyWeaponInteractionActivationGate ActivationGates;
    public float MaximumActivationSpeed;
    public float RecentlyDamagedWindowSeconds;
    public int BombsPerLaunch;
    public float LandingSpreadRadius;
    public float RadialPatternRadius;
    public float RandomMinimumDistance;
    public float RandomMaximumDistance;
    public float FlightDurationSeconds;
    public float Gravity;
    public float ApexHeight;
    public float LaunchHeightOffset;
    public float LandingHeightOffset;
    public float Damage;
    public float DamageRadius;
    public float ImpactExplosionDelaySeconds;
    public float BombScaleMultiplier;
    public byte EnableLandingWarning;
    public float WarningLeadTimeSeconds;
    public float WarningRadiusScale;
    public float WarningRingWidth;
    public float WarningHeightOffset;
    public float WarningMaximumAlpha;
    public float WarningFadeOutSeconds;
    public float4 WarningColor;
}

/// <summary>
/// Mutable runtime state for one Bombardier module instance.
/// </summary>
public struct EnemyBombardierRuntimeElement : IBufferElementData
{
    public float NextBurstTimer;
    public float NextBombInBurstTimer;
    public float PostLaunchStopTimer;
    public int RemainingBurstLaunches;
    public int LaunchesCompletedInCurrentBurst;
    public float BurstWindupDurationSeconds;
    public byte IsPlayerInReach;
    public byte IsLaunchAllowed;
    public float3 LockedTargetPosition;
    public byte HasLockedTargetPosition;
    public uint RandomState;
}

/// <summary>
/// Enqueued request to spawn one enemy bomb and simulate it toward a predicted landing point.
/// </summary>
[InternalBufferCapacity(0)]
public struct EnemyBombardierLaunchRequest : IBufferElementData
{
    public Entity OwnerEntity;
    public float3 LaunchPosition;
    public float3 LandingPosition;
    public EnemyBombardierTrajectoryMode TrajectoryMode;
    public float FlightDurationSeconds;
    public float Gravity;
    public float ApexHeight;
    public float Damage;
    public float DamageRadius;
    public float ImpactExplosionDelaySeconds;
    public float BombScaleMultiplier;
    public byte EnableLandingWarning;
    public float WarningLeadTimeSeconds;
    public float WarningRadius;
    public float WarningRingWidth;
    public float WarningHeightOffset;
    public float WarningMaximumAlpha;
    public float WarningFadeOutSeconds;
    public float4 WarningColor;
}

/// <summary>
/// Holds the bomb prefab entity used by Bombardier runtime spawning.
/// </summary>
public struct EnemyBombardierBombPrefab : IComponentData
{
    public Entity PrefabEntity;
}

/// <summary>
/// Runtime state carried by one Bombardier bomb entity until its warning fade-out completes.
/// </summary>
public struct EnemyBombardierBomb : IComponentData
{
    public Entity OwnerEntity;
    public float3 LaunchPosition;
    public float3 LandingPosition;
    public float3 Velocity;
    public float Gravity;
    public float ElapsedSeconds;
    public float FlightDurationSeconds;
    public float ImpactExplosionDelaySeconds;
    public float ExplosionDelayElapsedSeconds;
    public float Damage;
    public float DamageRadius;
    public byte HasImpacted;
    public byte HasExploded;
    public float WarningFadeOutEndTime;
}

/// <summary>
/// Stores a Bombardier landing warning ring state rendered by the shared enemy warning presentation pool.
/// </summary>
public struct EnemyBombardierWarningState : IComponentData, IEnableableComponent
{
    public float3 WorldPosition;
    public float WarningStartTime;
    public float ImpactTime;
    public float FadeOutEndTime;
    public float LeadTimeSeconds;
    public float FadeOutSeconds;
    public float Radius;
    public float RingWidth;
    public float HeightOffset;
    public float MaximumAlpha;
    public float4 Color;
}

/// <summary>
/// Stores Bombardier configs referenced by boss module candidates.
/// </summary>
public struct EnemyBossPatternBombardierConfigElement : IBufferElementData
{
    public EnemyBombardierConfigElement BombardierConfig;
}
#endregion
