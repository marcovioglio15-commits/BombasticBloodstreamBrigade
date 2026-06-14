using Unity.Collections;
using Unity.Entities;

#region Components
/// <summary>
/// Stores the baked conditional weapon switch table headline on one player entity. The buffers
/// <see cref="PlayerConditionalWeaponSwitchEntryElement"/> and <see cref="PlayerConditionalWeaponSwitchConditionElement"/>
/// hold the actual entries and conditions; this component just gates evaluation when no entries are authored.
/// </summary>
public struct PlayerConditionalWeaponSwitchConfig : IComponentData
{
    public byte EntryCount;
}

/// <summary>
/// Stores the resolved winning entry produced by the conditional weapon switch evaluator. The animator
/// presentation utility consumes this state to decide whether the conditional pipeline overrides the equipped
/// Switch Weapon power-up selection.
/// </summary>
public struct PlayerConditionalWeaponSwitchState : IComponentData
{
    public FixedString64Bytes WeaponId;
    public int MatchedPriority;
    public uint LastEvaluatedScalableStatsHash;
    public byte HasMatch;
    public byte OverridesPowerUpSwitch;
    public byte Initialized;
}

/// <summary>
/// Tracks the scalable-stat hash last applied to conditional weapon switch runtime buffers.
/// </summary>
public struct PlayerConditionalWeaponSwitchScalingState : IComponentData
{
    public uint LastScalableStatsHash;
    public byte Initialized;
}
#endregion

#region Buffer Elements
/// <summary>
/// Stores one immutable baseline conditional weapon switch entry used for formula-driven runtime rebuilds.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerBaseConditionalWeaponSwitchEntryElement : IBufferElementData
{
    public FixedString64Bytes WeaponId;
    public int Priority;
    public int ConditionStartIndex;
    public int ConditionCount;
    public byte OverridePowerUpSwitch;
    public byte SufficientGroupCount;
}

/// <summary>
/// Stores one immutable baseline condition used for formula-driven runtime rebuilds.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerBaseConditionalWeaponSwitchConditionElement : IBufferElementData
{
    public FixedString64Bytes StatName;
    public float MinimumValue;
    public float MaximumValue;
    public byte Requirement;
}

/// <summary>
/// Stores one baked conditional weapon switch entry. Conditions live in a separate dynamic buffer indexed by
/// <see cref="ConditionStartIndex"/>+<see cref="ConditionCount"/> so the entry archetype stays blittable.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerConditionalWeaponSwitchEntryElement : IBufferElementData
{
    public FixedString64Bytes WeaponId;
    public int Priority;
    public int ConditionStartIndex;
    public int ConditionCount;
    public byte OverridePowerUpSwitch;
    public byte SufficientGroupCount;
}

/// <summary>
/// Stores one baked condition referenced by zero or more <see cref="PlayerConditionalWeaponSwitchEntryElement"/>
/// entries. The condition stat name is matched against <see cref="PlayerScalableStatElement"/> at runtime; the
/// inclusive numeric range honors both numeric and boolean stat projections.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerConditionalWeaponSwitchConditionElement : IBufferElementData
{
    public FixedString64Bytes StatName;
    public float MinimumValue;
    public float MaximumValue;
    public byte Requirement;
}

/// <summary>
/// Identifies one nested conditional weapon switch field that can be rewritten by an Add Scaling formula.
/// </summary>
public enum PlayerRuntimeConditionalWeaponSwitchFieldId : byte
{
    EntryWeaponId = 0,
    EntryPriority = 1,
    EntryOverridePowerUpSwitch = 2,
    ConditionMinimumValue = 3,
    ConditionMaximumValue = 4
}

/// <summary>
/// Stores one conditional weapon switch Add Scaling rule with its bake-resolved entry and condition indices.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRuntimeConditionalWeaponSwitchScalingElement : IBufferElementData
{
    public PlayerRuntimeConditionalWeaponSwitchFieldId FieldId;
    public int TargetEntryIndex;
    public int TargetConditionIndex;
    public byte ValueType;
    public float BaseValue;
    public byte BaseBooleanValue;
    public byte IsInteger;
    public FixedString64Bytes BaseTokenValue;
    public FixedString512Bytes Formula;
}
#endregion
