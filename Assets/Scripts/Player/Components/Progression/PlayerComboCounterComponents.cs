using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Stores the current combo value, damage observations, time-decay carry, and presentation data resolved from the active combo rank.
/// none.
/// </summary>
public struct PlayerComboCounterState : IComponentData
{
    public int CurrentValue;
    public int CurrentRankIndex;
    public int ActivePassiveUnlockRankIndex;
    public int CurrentRankRequiredValue;
    public int NextRankRequiredValue;
    public uint PassiveUnlockSignature;
    public float ProgressNormalized;
    public float DecayPointsCarry;
    public float GainPointsCarry;
    public float PreviousObservedHealth;
    public float PreviousObservedShield;
    public FixedString64Bytes CurrentRankId;
    public byte Initialized;
}

/// <summary>
/// Tracks one passive power-up catalog stack currently granted by a combo rank so derank/reset can remove only combo-owned stacks.
/// none.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerComboPassivePowerUpGrantElement : IBufferElementData
{
    public FixedString64Bytes PowerUpId;
    public int RankIndex;
    public int CatalogIndex;
    public byte EquippedOnGrant;
}
