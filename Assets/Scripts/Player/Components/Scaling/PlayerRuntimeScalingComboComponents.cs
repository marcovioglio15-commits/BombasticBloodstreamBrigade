using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Stores immutable combo runtime rules used to rebuild the active combo config whenever scalable stats change.
/// </summary>
public struct PlayerBaseComboCounterConfig : IComponentData
{
    public byte Enabled;
    public PlayerComboCounterMode Mode;
    public int ComboGainPerKill;
    public PlayerComboDamageBreakMode DamageBreakMode;
    public byte ShieldDamageBreaksCombo;
    public byte PreventDecayIntoNonDecayingRanks;
    public FixedString64Bytes SingleRankId;
    public int SingleRankMaximumComboValue;
    public float SingleRankPointsDecayPerSecond;
    public PlayerComboSingleRankValueDisplayMode SingleRankValueDisplayMode;
    public PlayerComboSingleRankFormulaDistributionMode SingleRankFormulaDistributionMode;
}

/// <summary>
/// Stores the current combo runtime rules after progression Add Scaling formulas are resolved.
/// </summary>
public struct PlayerRuntimeComboCounterConfig : IComponentData
{
    public byte Enabled;
    public PlayerComboCounterMode Mode;
    public int ComboGainPerKill;
    public PlayerComboDamageBreakMode DamageBreakMode;
    public byte ShieldDamageBreaksCombo;
    public byte PreventDecayIntoNonDecayingRanks;
    public FixedString64Bytes SingleRankId;
    public int SingleRankMaximumComboValue;
    public float SingleRankPointsDecayPerSecond;
    public PlayerComboSingleRankValueDisplayMode SingleRankValueDisplayMode;
    public PlayerComboSingleRankFormulaDistributionMode SingleRankFormulaDistributionMode;
}

/// <summary>
/// Stores one immutable combo-rank milestone, point-decay rate, progressive boost data, passive unlock range, and flattened Character Tuning formula range used by that rank.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerBaseComboRankElement : IBufferElementData
{
    public PlayerComboCounterMode Mode;
    public FixedString64Bytes RankId;
    public byte Enabled;
    public int RequiredComboValue;
    public float RequiredProgressPercent;
    public float PointsDecayPerSecond;
    public float ProgressiveBoostPercent;
    public int BonusFormulaStartIndex;
    public int BonusFormulaCount;
    public int PassiveUnlockStartIndex;
    public int PassiveUnlockCount;
}

/// <summary>
/// Stores one current combo-rank milestone, point-decay rate, progressive boost data, and passive unlock range after progression Add Scaling formulas are resolved.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRuntimeComboRankElement : IBufferElementData
{
    public PlayerComboCounterMode Mode;
    public FixedString64Bytes RankId;
    public byte Enabled;
    public int RequiredComboValue;
    public float RequiredProgressPercent;
    public float PointsDecayPerSecond;
    public float ProgressiveBoostPercent;
    public int BonusFormulaStartIndex;
    public int BonusFormulaCount;
    public int PassiveUnlockStartIndex;
    public int PassiveUnlockCount;
}

/// <summary>
/// Stores one immutable passive power-up unlock authored under a combo rank.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerBaseComboPassiveUnlockElement : IBufferElementData
{
    public FixedString64Bytes PassivePowerUpId;
    public byte IsEnabled;
}

/// <summary>
/// Stores one current passive power-up unlock after progression Add Scaling formulas are resolved.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRuntimeComboPassiveUnlockElement : IBufferElementData
{
    public FixedString64Bytes PassivePowerUpId;
    public byte IsEnabled;
}

/// <summary>
/// Identifies one combo runtime field that can be rebuilt from a progression Add Scaling rule.
/// </summary>
public enum PlayerRuntimeComboCounterFieldId : byte
{
    Enabled = 0,
    Mode = 1,
    ComboGainPerKill = 2,
    ShieldDamageBreaksCombo = 3,
    DamageBreakMode = 4,
    RankRequiredComboValue = 5,
    RankPointsDecayPerSecond = 6,
    PreventDecayIntoNonDecayingRanks = 7,
    RankProgressiveBoostPercent = 8,
    RankPassiveUnlockEnabled = 9,
    RankPassiveUnlockPowerUpId = 10,
    SingleRankId = 11,
    SingleRankMaximumComboValue = 12,
    SingleRankPointsDecayPerSecond = 13,
    SingleRankValueDisplayMode = 14,
    SingleRankFormulaDistributionMode = 15,
    SingleRankMilestoneId = 16,
    SingleRankMilestoneEnabled = 17,
    SingleRankMilestoneRequiredProgressPercent = 18
}

/// <summary>
/// Stores one combo scaling metadata entry baked from progression Add Scaling authoring data.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRuntimeComboCounterScalingElement : IBufferElementData
{
    public PlayerRuntimeComboCounterFieldId FieldId;
    public PlayerComboCounterMode EntryMode;
    public int RankIndex;
    public int PassiveUnlockIndex;
    public byte ValueType;
    public float BaseValue;
    public byte BaseBooleanValue;
    public byte IsInteger;
    public FixedString64Bytes BaseTokenValue;
    public FixedString512Bytes Formula;
}
