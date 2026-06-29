using Unity.Entities;
using Unity.Mathematics;

#region Components
/// <summary>
/// Global simulation time scale used by gameplay effects such as Bullet Time.
/// </summary>
public struct EnemyGlobalTimeScale : IComponentData
{
    public float Scale;
    public float PlayerProjectileScale;
}

/// <summary>
/// Runtime aggregated elemental status values applied to one enemy.
/// </summary>
public struct EnemyElementalRuntimeState : IComponentData
{
    public float SlowPercent;
}

/// <summary>
/// Elemental stack payload stored per enemy and per element type.
/// </summary>
public struct EnemyElementStackElement : IBufferElementData
{
    public ElementType ElementType;
    public ElementalEffectKind EffectKind;
    public ElementalProcMode ProcMode;
    public ElementalProcReapplyMode ReapplyMode;
    public float CurrentStacks;
    public float MaximumStacks;
    public float ProcThresholdStacks;
    public float StackDecayPerSecond;
    public byte ConsumeStacksOnProc;
    public float DotDamagePerTick;
    public float DotTickInterval;
    public float DotDurationSeconds;
    public float DotRemainingSeconds;
    public float DotTickTimer;
    public float ImpedimentSlowPercentPerStack;
    public float ImpedimentProcSlowPercent;
    public float ImpedimentMaxSlowPercent;
    public float ImpedimentDurationSeconds;
    public float ImpedimentRemainingSeconds;
    public float CurrentImpedimentSlowPercent;
}

/// <summary>
/// Buffer element representing one enemy killed event during current simulation frame.
/// </summary>
public struct EnemyKilledEventElement : IBufferElementData
{
    public Entity EnemyEntity;
    public float3 Position;
    public float ComboPointMultiplier;
}
#endregion
