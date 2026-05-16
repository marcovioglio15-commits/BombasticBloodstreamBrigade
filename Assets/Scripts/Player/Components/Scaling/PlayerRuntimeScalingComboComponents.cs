using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Stores immutable combo runtime rules used to rebuild the active combo config whenever scalable stats change.
/// none.
/// </summary>
public struct PlayerBaseComboCounterConfig : IComponentData
{
    public byte Enabled;
    public int ComboGainPerKill;
    public PlayerComboDamageBreakMode DamageBreakMode;
    public byte ShieldDamageBreaksCombo;
    public byte PreventDecayIntoNonDecayingRanks;
}

/// <summary>
/// Stores the current combo runtime rules after progression Add Scaling formulas are resolved.
/// none.
/// </summary>
public struct PlayerRuntimeComboCounterConfig : IComponentData
{
    public byte Enabled;
    public int ComboGainPerKill;
    public PlayerComboDamageBreakMode DamageBreakMode;
    public byte ShieldDamageBreaksCombo;
    public byte PreventDecayIntoNonDecayingRanks;
}

/// <summary>
/// Stores one immutable combo-rank milestone, point-decay rate, progressive boost data, passive unlock range, and flattened Character Tuning formula range used by that rank.
/// none.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerBaseComboRankElement : IBufferElementData
{
    public FixedString64Bytes RankId;
    public int RequiredComboValue;
    public float PointsDecayPerSecond;
    public float ProgressiveBoostPercent;
    public int BonusFormulaStartIndex;
    public int BonusFormulaCount;
    public int PassiveUnlockStartIndex;
    public int PassiveUnlockCount;
}

/// <summary>
/// Stores one current combo-rank milestone, point-decay rate, progressive boost data, and passive unlock range after progression Add Scaling formulas are resolved.
/// none.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRuntimeComboRankElement : IBufferElementData
{
    public FixedString64Bytes RankId;
    public int RequiredComboValue;
    public float PointsDecayPerSecond;
    public float ProgressiveBoostPercent;
    public int BonusFormulaStartIndex;
    public int BonusFormulaCount;
    public int PassiveUnlockStartIndex;
    public int PassiveUnlockCount;
}

/// <summary>
/// Stores one immutable passive power-up unlock authored under a combo rank.
/// none.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerBaseComboPassiveUnlockElement : IBufferElementData
{
    public FixedString64Bytes PassivePowerUpId;
    public byte IsEnabled;
}

/// <summary>
/// Stores one current passive power-up unlock after progression Add Scaling formulas are resolved.
/// none.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRuntimeComboPassiveUnlockElement : IBufferElementData
{
    public FixedString64Bytes PassivePowerUpId;
    public byte IsEnabled;
}

/// <summary>
/// Identifies one combo runtime field that can be rebuilt from a progression Add Scaling rule.
/// none.
/// </summary>
public enum PlayerRuntimeComboCounterFieldId : byte
{
    Enabled = 0,
    ComboGainPerKill = 1,
    ShieldDamageBreaksCombo = 2,
    DamageBreakMode = 3,
    RankRequiredComboValue = 4,
    RankPointsDecayPerSecond = 5,
    PreventDecayIntoNonDecayingRanks = 6,
    RankProgressiveBoostPercent = 7,
    RankPassiveUnlockEnabled = 8,
    RankPassiveUnlockPowerUpId = 9
}

/// <summary>
/// Stores one combo scaling metadata entry baked from progression Add Scaling authoring data.
/// none.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRuntimeComboCounterScalingElement : IBufferElementData
{
    public PlayerRuntimeComboCounterFieldId FieldId;
    public int RankIndex;
    public int PassiveUnlockIndex;
    public byte ValueType;
    public float BaseValue;
    public byte BaseBooleanValue;
    public byte IsInteger;
    public FixedString64Bytes BaseTokenValue;
    public FixedString512Bytes Formula;
}
