using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// This component represents the shooting state of a player entity, 
/// including whether automatic shooting is enabled,
/// </summary>
public struct PlayerShootingState : IComponentData
{
    public byte AutomaticEnabled;
    public byte PreviousShootPressed;
    public byte VisualShootingActive;
    public uint ShotPulseVersion;
    public float NextShotTime;
    public float VisualShootingUntilTime;
}

/// <summary>
/// This component holds a reference to the projectile prefab entity 
/// that the shooter will instantiate when shooting.
/// </summary>
public struct ShooterProjectilePrefab : IComponentData
{
    public Entity PrefabEntity;
}

/// <summary>
/// This component holds a reference to the muzzle anchor entity,
/// which is the point from which projectiles will be spawned when the shooter fires.
/// </summary>
public struct ShooterMuzzleAnchor : IComponentData
{
    public Entity AnchorEntity;
}

/// <summary>
/// This component represents the state of the projectile pool for a shooter entity,
/// which is used to manage a pool of projectile entities for efficient shooting 
/// without runtime instantiation overhead.
/// </summary>
public struct ProjectilePoolState : IComponentData
{
    public int InitialCapacity;
    public int ExpandBatch;
    public byte Initialized;
}

/// <summary>
/// This component represents a shoot request, which is created when a player entity initiates a shooting action.
/// </summary>
[InternalBufferCapacity(0)]
public struct ShootRequest : IBufferElementData
{
    public float3 Position;
    public float3 Direction;
    public float Speed;
    public float ExplosionRadius;
    public float Range;
    public float Lifetime;
    public float Damage;
    public float ProjectileScaleMultiplier;
    public ProjectilePenetrationMode PenetrationMode;
    public int MaxPenetrations;
    public byte KnockbackEnabled;
    public float KnockbackStrength;
    public float KnockbackDurationSeconds;
    public ProjectileKnockbackDirectionMode KnockbackDirectionMode;
    public ProjectileKnockbackStackingMode KnockbackStackingMode;
    public byte InheritPlayerSpeed;
    public byte IsSplitChild;
    public int OrbitLayerIndex;
    public int OrbitLayerCount;
    public ProjectileElementalPayload ElementalPayloadOverride;
}

[InternalBufferCapacity(0)]
public struct ProjectilePoolElement : IBufferElementData
{
    public Entity ProjectileEntity;
}

/// <summary>
/// This component represents a projectile entity, 
/// which includes its velocity, damage, maximum range, lifetime, 
/// additional impact radius, and whether it inherits the player's speed when spawned (Hoctagon style).
/// </summary>
public struct Projectile : IComponentData
{
    public float3 Velocity;
    public float Damage;
    public float ExplosionRadius;
    public float MaxRange;
    public float MaxLifetime;
    public ProjectilePenetrationMode PenetrationMode;
    public int RemainingPenetrations;
    public byte KnockbackEnabled;
    public float KnockbackStrength;
    public float KnockbackDurationSeconds;
    public ProjectileKnockbackDirectionMode KnockbackDirectionMode;
    public ProjectileKnockbackStackingMode KnockbackStackingMode;
    public byte InheritPlayerSpeed;
}

/// <summary>
/// This component represents the runtime state of a projectile entity,
/// which includes the distance it has traveled and the elapsed time since it was spawned.
/// </summary>
public struct ProjectileRuntimeState : IComponentData
{
    public float TraveledDistance;
    public float ElapsedLifetime;
}

/// <summary>
/// This component holds a reference to the shooter entity that owns a projectile,
/// which is used to associate projectiles with their source shooter for 
/// applying player modifiers to projectile behavior 
/// or handling interactions between projectiles and the shooter (e.g., avoiding self-collision).
/// </summary>
public struct ProjectileOwner : IComponentData
{
    public Entity ShooterEntity;
}

/// <summary>
/// Tracks whether one pooled projectile still needs an offscreen warning before it first becomes visible.
/// </summary>
public struct ProjectileOffscreenWarningState : IComponentData
{
    public byte Enabled;
    public byte HasBeenVisible;
}

/// <summary>
/// Stores enemies already hit during the projectile's current overlap contact so penetration cannot damage them every frame until the projectile exits.
/// </summary>
[InternalBufferCapacity(8)]
public struct ProjectileHitHistoryElement : IBufferElementData
{
    public Entity EnemyEntity;
}

/// <summary>
/// This component is used to mark a projectile entity as active, indicating that it is currently 
/// in flight and should be processed by the projectile movement and collision systems.
/// </summary>
public struct ProjectileActive : IComponentData, IEnableableComponent
{
}

/// <summary>
/// Stores the projectile prefab base scale so runtime modifiers can be reapplied without cumulative drift.
/// </summary>
public struct ProjectileBaseScale : IComponentData
{
    public float Value;
}

/// <summary>
/// Runtime state used by the Perfect Circle passive to move projectiles around the player.
/// </summary>
public struct ProjectilePerfectCircleState : IComponentData
{
    public byte Enabled;
    public byte HasEnteredOrbit;
    public byte CompletedFullOrbit;
    public byte HasOrbitPlaneHeight;
    public float3 EntryOrigin;
    public float OrbitAngle;
    public float OrbitBlendProgress;
    public float CurrentRadius;
    public float AccumulatedOrbitRadians;
    public float3 RadialDirection;
    public float3 EntryVelocity;
    public float OrbitPlaneHeight;
    public int OrbitLayerIndex;
    public int OrbitLayerCount;
}

/// <summary>
/// Runtime state used by bouncing projectiles to keep bounce counters and speed scaling.
/// </summary>
public struct ProjectileBounceState : IComponentData
{
    public int RemainingBounces;
    public float SpeedPercentChangePerBounce;
    public float MinimumSpeedMultiplierAfterBounce;
    public float MaximumSpeedMultiplierAfterBounce;
    public float CurrentSpeedMultiplier;
}

/// <summary>
/// Runtime split payload stored on original projectiles.
/// </summary>
public struct ProjectileSplitState : IComponentData
{
    public byte CanSplit;
    public ProjectileSplitTriggerMode TriggerMode;
    public ProjectileSplitDirectionMode DirectionMode;
    public int SplitProjectileCount;
    public float SplitOffsetDegrees;
    public FixedList128Bytes<float> CustomAnglesDegrees;
    public float SplitDamageMultiplier;
    public float SplitSizeMultiplier;
    public float SplitSpeedMultiplier;
    public float SplitLifetimeMultiplier;
}

/// <summary>
/// Runtime elemental payload carried by projectiles.
/// </summary>
public struct ProjectileElementalPayloadEntry
{
    public byte ElementTypeId;
    public byte EffectKindId;
    public byte ProcModeId;
    public byte ReapplyModeId;
    public byte ConsumeStacksOnProc;
    public float ProcThresholdStacks;
    public float MaximumStacks;
    public float StackDecayPerSecond;
    public float DotDamagePerTick;
    public float DotTickInterval;
    public float DotDurationSeconds;
    public float ImpedimentSlowPercentPerStack;
    public float ImpedimentProcSlowPercent;
    public float ImpedimentMaxSlowPercent;
    public float ImpedimentDurationSeconds;
    public float StacksPerHit;

    public ElementalEffectConfig Effect
    {
        get
        {
            return new ElementalEffectConfig
            {
                ElementType = (ElementType)ElementTypeId,
                EffectKind = (ElementalEffectKind)EffectKindId,
                ProcMode = (ElementalProcMode)ProcModeId,
                ReapplyMode = (ElementalProcReapplyMode)ReapplyModeId,
                ProcThresholdStacks = ProcThresholdStacks,
                MaximumStacks = MaximumStacks,
                StackDecayPerSecond = StackDecayPerSecond,
                ConsumeStacksOnProc = ConsumeStacksOnProc,
                DotDamagePerTick = DotDamagePerTick,
                DotTickInterval = DotTickInterval,
                DotDurationSeconds = DotDurationSeconds,
                ImpedimentSlowPercentPerStack = ImpedimentSlowPercentPerStack,
                ImpedimentProcSlowPercent = ImpedimentProcSlowPercent,
                ImpedimentMaxSlowPercent = ImpedimentMaxSlowPercent,
                ImpedimentDurationSeconds = ImpedimentDurationSeconds
            };
        }

        set
        {
            ElementTypeId = (byte)value.ElementType;
            EffectKindId = (byte)value.EffectKind;
            ProcModeId = (byte)value.ProcMode;
            ReapplyModeId = (byte)value.ReapplyMode;
            ProcThresholdStacks = value.ProcThresholdStacks;
            MaximumStacks = value.MaximumStacks;
            StackDecayPerSecond = value.StackDecayPerSecond;
            ConsumeStacksOnProc = value.ConsumeStacksOnProc;
            DotDamagePerTick = value.DotDamagePerTick;
            DotTickInterval = value.DotTickInterval;
            DotDurationSeconds = value.DotDurationSeconds;
            ImpedimentSlowPercentPerStack = value.ImpedimentSlowPercentPerStack;
            ImpedimentProcSlowPercent = value.ImpedimentProcSlowPercent;
            ImpedimentMaxSlowPercent = value.ImpedimentMaxSlowPercent;
            ImpedimentDurationSeconds = value.ImpedimentDurationSeconds;
        }
    }
}

/// <summary>
/// Runtime elemental payload carried by projectiles.
/// </summary>
public struct ProjectileElementalPayload : IComponentData
{
    public byte EntryCount;
    public ProjectileElementalPayloadEntry Entry0;
    public ProjectileElementalPayloadEntry Entry1;
    public ProjectileElementalPayloadEntry Entry2;
    public ProjectileElementalPayloadEntry Entry3;
}
