using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Rebuilds nested conditional weapon switch runtime buffers from immutable baselines when scalable stats change.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerRuntimeScalingSyncSystem))]
[UpdateBefore(typeof(PlayerConditionalWeaponSwitchSystem))]
public partial struct PlayerRuntimeConditionalWeaponSwitchScalingSystem : ISystem
{
    #region Fields
    private static readonly Dictionary<string, PlayerFormulaValue> VariableContext = new Dictionary<string, PlayerFormulaValue>(64, System.StringComparer.OrdinalIgnoreCase);
    private static readonly List<PlayerScalableStatElement> EffectiveScalableStats = new List<PlayerScalableStatElement>(64);
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the baseline, runtime and scaling metadata required by conditional weapon switch rebuilds.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerRuntimeScalingState>();
        state.RequireForUpdate<PlayerConditionalWeaponSwitchScalingState>();
        state.RequireForUpdate<PlayerBaseConditionalWeaponSwitchEntryElement>();
        state.RequireForUpdate<PlayerBaseConditionalWeaponSwitchConditionElement>();
        state.RequireForUpdate<PlayerConditionalWeaponSwitchEntryElement>();
        state.RequireForUpdate<PlayerConditionalWeaponSwitchConditionElement>();
        state.RequireForUpdate<PlayerRuntimeConditionalWeaponSwitchScalingElement>();
    }

    /// <summary>
    /// Rebuilds conditional switch buffers and applies nested Add Scaling formulas on scalable-stat hash changes.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        BufferLookup<PlayerScalableStatElement> scalableStatsLookup = SystemAPI.GetBufferLookup<PlayerScalableStatElement>(true);
        BufferLookup<PlayerRoomRewardTemporaryModifierElement> temporaryModifiersLookup = SystemAPI.GetBufferLookup<PlayerRoomRewardTemporaryModifierElement>(true);
        ComponentLookup<PlayerRoomRewardTemporaryState> temporaryStateLookup = SystemAPI.GetComponentLookup<PlayerRoomRewardTemporaryState>(true);
        ComponentLookup<PlayerRuntimeComboCounterConfig> comboConfigLookup = SystemAPI.GetComponentLookup<PlayerRuntimeComboCounterConfig>(true);
        ComponentLookup<PlayerComboCounterState> comboStateLookup = SystemAPI.GetComponentLookup<PlayerComboCounterState>(true);
        BufferLookup<PlayerRuntimeComboRankElement> comboRanksLookup = SystemAPI.GetBufferLookup<PlayerRuntimeComboRankElement>(true);
        BufferLookup<PlayerPowerUpCharacterTuningFormulaElement> characterTuningLookup = SystemAPI.GetBufferLookup<PlayerPowerUpCharacterTuningFormulaElement>(true);
        BufferLookup<PlayerBaseConditionalWeaponSwitchEntryElement> baseEntryLookup = SystemAPI.GetBufferLookup<PlayerBaseConditionalWeaponSwitchEntryElement>(true);
        BufferLookup<PlayerBaseConditionalWeaponSwitchConditionElement> baseConditionLookup = SystemAPI.GetBufferLookup<PlayerBaseConditionalWeaponSwitchConditionElement>(true);
        BufferLookup<PlayerConditionalWeaponSwitchEntryElement> runtimeEntryLookup = SystemAPI.GetBufferLookup<PlayerConditionalWeaponSwitchEntryElement>(false);
        BufferLookup<PlayerConditionalWeaponSwitchConditionElement> runtimeConditionLookup = SystemAPI.GetBufferLookup<PlayerConditionalWeaponSwitchConditionElement>(false);
        BufferLookup<PlayerRuntimeConditionalWeaponSwitchScalingElement> scalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeConditionalWeaponSwitchScalingElement>(true);

        foreach ((RefRO<PlayerRuntimeScalingState> runtimeScalingState,
                  RefRW<PlayerConditionalWeaponSwitchScalingState> conditionalScalingState,
                  Entity entity)
                 in SystemAPI.Query<RefRO<PlayerRuntimeScalingState>,
                                    RefRW<PlayerConditionalWeaponSwitchScalingState>>()
                             .WithAll<PlayerBaseConditionalWeaponSwitchEntryElement>()
                             .WithAll<PlayerBaseConditionalWeaponSwitchConditionElement>()
                             .WithAll<PlayerConditionalWeaponSwitchEntryElement>()
                             .WithAll<PlayerConditionalWeaponSwitchConditionElement>()
                             .WithAll<PlayerRuntimeConditionalWeaponSwitchScalingElement>()
                             .WithEntityAccess())
        {
            if (runtimeScalingState.ValueRO.Initialized == 0)
                continue;

            if (conditionalScalingState.ValueRO.Initialized != 0 &&
                conditionalScalingState.ValueRO.LastScalableStatsHash == runtimeScalingState.ValueRO.LastScalableStatsHash)
            {
                continue;
            }

            RebuildRuntimeBuffers(entity,
                                  in baseEntryLookup,
                                  in baseConditionLookup,
                                  ref runtimeEntryLookup,
                                  ref runtimeConditionLookup);
            PlayerRuntimeScalingFormulaContextUtility.Fill(entity,
                                                            in scalableStatsLookup,
                                                            in temporaryModifiersLookup,
                                                            in temporaryStateLookup,
                                                            in comboConfigLookup,
                                                           in comboStateLookup,
                                                           in comboRanksLookup,
                                                           in characterTuningLookup,
                                                           EffectiveScalableStats,
                                                           VariableContext);
            ApplyScaling(entity,
                         scalingLookup[entity],
                         ref runtimeEntryLookup,
                         ref runtimeConditionLookup);
            conditionalScalingState.ValueRW.Initialized = 1;
            conditionalScalingState.ValueRW.LastScalableStatsHash = runtimeScalingState.ValueRO.LastScalableStatsHash;
        }
    }
    #endregion

    #region Rebuild
    /// <summary>
    /// Restores mutable conditional entry and condition buffers from their immutable baseline counterparts.
    /// </summary>
    /// <param name="entity">Player entity owning all conditional switch buffers.</param>
    /// <param name="baseEntryLookup">Read-only immutable entry lookup.</param>
    /// <param name="baseConditionLookup">Read-only immutable condition lookup.</param>
    /// <param name="runtimeEntryLookup">Mutable runtime entry lookup.</param>
    /// <param name="runtimeConditionLookup">Mutable runtime condition lookup.</param>
    private static void RebuildRuntimeBuffers(Entity entity,
                                              in BufferLookup<PlayerBaseConditionalWeaponSwitchEntryElement> baseEntryLookup,
                                              in BufferLookup<PlayerBaseConditionalWeaponSwitchConditionElement> baseConditionLookup,
                                              ref BufferLookup<PlayerConditionalWeaponSwitchEntryElement> runtimeEntryLookup,
                                              ref BufferLookup<PlayerConditionalWeaponSwitchConditionElement> runtimeConditionLookup)
    {
        DynamicBuffer<PlayerBaseConditionalWeaponSwitchEntryElement> baseEntries = baseEntryLookup[entity];
        DynamicBuffer<PlayerBaseConditionalWeaponSwitchConditionElement> baseConditions = baseConditionLookup[entity];
        DynamicBuffer<PlayerConditionalWeaponSwitchEntryElement> runtimeEntries = runtimeEntryLookup[entity];
        DynamicBuffer<PlayerConditionalWeaponSwitchConditionElement> runtimeConditions = runtimeConditionLookup[entity];
        runtimeEntries.Clear();
        runtimeConditions.Clear();

        for (int entryIndex = 0; entryIndex < baseEntries.Length; entryIndex++)
        {
            PlayerBaseConditionalWeaponSwitchEntryElement entry = baseEntries[entryIndex];
            runtimeEntries.Add(new PlayerConditionalWeaponSwitchEntryElement
            {
                WeaponId = entry.WeaponId,
                Priority = entry.Priority,
                ConditionStartIndex = entry.ConditionStartIndex,
                ConditionCount = entry.ConditionCount,
                OverridePowerUpSwitch = entry.OverridePowerUpSwitch,
                SufficientGroupCount = entry.SufficientGroupCount
            });
        }

        for (int conditionIndex = 0; conditionIndex < baseConditions.Length; conditionIndex++)
        {
            PlayerBaseConditionalWeaponSwitchConditionElement condition = baseConditions[conditionIndex];
            runtimeConditions.Add(new PlayerConditionalWeaponSwitchConditionElement
            {
                StatName = condition.StatName,
                MinimumValue = condition.MinimumValue,
                MaximumValue = condition.MaximumValue,
                Requirement = condition.Requirement
            });
        }
    }
    #endregion

    #region Scaling
    /// <summary>
    /// Applies every conditional weapon switch scaling rule to freshly rebuilt runtime buffers.
    /// </summary>
    /// <param name="entity">Player entity owning the runtime buffers.</param>
    /// <param name="scalingBuffer">Runtime conditional switch scaling metadata.</param>
    /// <param name="entryLookup">Mutable runtime entry lookup.</param>
    /// <param name="conditionLookup">Mutable runtime condition lookup.</param>
    private static void ApplyScaling(Entity entity,
                                     DynamicBuffer<PlayerRuntimeConditionalWeaponSwitchScalingElement> scalingBuffer,
                                     ref BufferLookup<PlayerConditionalWeaponSwitchEntryElement> entryLookup,
                                     ref BufferLookup<PlayerConditionalWeaponSwitchConditionElement> conditionLookup)
    {
        DynamicBuffer<PlayerConditionalWeaponSwitchEntryElement> entries = entryLookup[entity];
        DynamicBuffer<PlayerConditionalWeaponSwitchConditionElement> conditions = conditionLookup[entity];

        for (int scalingIndex = 0; scalingIndex < scalingBuffer.Length; scalingIndex++)
        {
            PlayerRuntimeConditionalWeaponSwitchScalingElement scalingElement = scalingBuffer[scalingIndex];

            switch ((PlayerFormulaValueType)scalingElement.ValueType)
            {
                case PlayerFormulaValueType.Boolean:
                    ApplyBooleanScaling(in scalingElement, entries);
                    break;
                case PlayerFormulaValueType.Token:
                    ApplyTokenScaling(in scalingElement, entries);
                    break;
                case PlayerFormulaValueType.Number:
                    ApplyNumericScaling(in scalingElement, entries, conditions);
                    break;
            }
        }
    }

    /// <summary>
    /// Evaluates and applies one boolean entry-field scaling rule.
    /// </summary>
    /// <param name="scalingElement">Boolean scaling metadata.</param>
    /// <param name="entries">Mutable runtime entry buffer.</param>
    private static void ApplyBooleanScaling(in PlayerRuntimeConditionalWeaponSwitchScalingElement scalingElement,
                                            DynamicBuffer<PlayerConditionalWeaponSwitchEntryElement> entries)
    {
        if (scalingElement.FieldId != PlayerRuntimeConditionalWeaponSwitchFieldId.EntryOverridePowerUpSwitch ||
            !TryResolveEntry(scalingElement.TargetEntryIndex, entries, out PlayerConditionalWeaponSwitchEntryElement entry))
        {
            return;
        }

        if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateBooleanValue(scalingElement.Formula.ToString(),
                                                                                  scalingElement.BaseBooleanValue != 0,
                                                                                  VariableContext,
                                                                                  out bool resolvedValue))
        {
            return;
        }

        entry.OverridePowerUpSwitch = resolvedValue ? (byte)1 : (byte)0;
        entries[scalingElement.TargetEntryIndex] = entry;
    }

    /// <summary>
    /// Evaluates and applies one token-backed entry Weapon Id scaling rule.
    /// </summary>
    /// <param name="scalingElement">Token scaling metadata.</param>
    /// <param name="entries">Mutable runtime entry buffer.</param>
    private static void ApplyTokenScaling(in PlayerRuntimeConditionalWeaponSwitchScalingElement scalingElement,
                                          DynamicBuffer<PlayerConditionalWeaponSwitchEntryElement> entries)
    {
        if (scalingElement.FieldId != PlayerRuntimeConditionalWeaponSwitchFieldId.EntryWeaponId ||
            !TryResolveEntry(scalingElement.TargetEntryIndex, entries, out PlayerConditionalWeaponSwitchEntryElement entry))
        {
            return;
        }

        if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateTokenValue(scalingElement.Formula.ToString(),
                                                                                scalingElement.BaseTokenValue.ToString(),
                                                                                VariableContext,
                                                                                out string resolvedValue))
        {
            return;
        }

        entry.WeaponId = PlayerWeaponVisualBakeUtility.BuildWeaponIdFixedString(resolvedValue);
        entries[scalingElement.TargetEntryIndex] = entry;
    }

    /// <summary>
    /// Evaluates and applies one numeric entry-priority or condition-bound scaling rule.
    /// </summary>
    /// <param name="scalingElement">Numeric scaling metadata.</param>
    /// <param name="entries">Mutable runtime entry buffer.</param>
    /// <param name="conditions">Mutable runtime condition buffer.</param>
    private static void ApplyNumericScaling(in PlayerRuntimeConditionalWeaponSwitchScalingElement scalingElement,
                                            DynamicBuffer<PlayerConditionalWeaponSwitchEntryElement> entries,
                                            DynamicBuffer<PlayerConditionalWeaponSwitchConditionElement> conditions)
    {
        if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateNumericValue(scalingElement.Formula.ToString(),
                                                                                  scalingElement.BaseValue,
                                                                                  scalingElement.IsInteger != 0,
                                                                                  VariableContext,
                                                                                  out float resolvedValue) ||
            !math.isfinite(resolvedValue))
        {
            return;
        }

        if (scalingElement.FieldId == PlayerRuntimeConditionalWeaponSwitchFieldId.EntryPriority)
        {
            if (!TryResolveEntry(scalingElement.TargetEntryIndex, entries, out PlayerConditionalWeaponSwitchEntryElement entry))
                return;

            entry.Priority = ResolvePriority(resolvedValue);
            entries[scalingElement.TargetEntryIndex] = entry;
            return;
        }

        if (!TryResolveCondition(scalingElement.TargetEntryIndex,
                                 scalingElement.TargetConditionIndex,
                                 entries,
                                 conditions,
                                 out int globalConditionIndex,
                                 out PlayerConditionalWeaponSwitchConditionElement condition))
        {
            return;
        }

        switch (scalingElement.FieldId)
        {
            case PlayerRuntimeConditionalWeaponSwitchFieldId.ConditionMinimumValue:
                condition.MinimumValue = resolvedValue;
                break;
            case PlayerRuntimeConditionalWeaponSwitchFieldId.ConditionMaximumValue:
                condition.MaximumValue = resolvedValue;
                break;
            default:
                return;
        }

        conditions[globalConditionIndex] = condition;
    }

    /// <summary>
    /// Resolves one entry buffer element by bake-captured authored index.
    /// </summary>
    /// <param name="entryIndex">Target entry index.</param>
    /// <param name="entries">Runtime entry buffer.</param>
    /// <param name="entry">Resolved entry when the index is valid.</param>
    /// <returns>True when the entry exists.</returns>
    private static bool TryResolveEntry(int entryIndex,
                                        DynamicBuffer<PlayerConditionalWeaponSwitchEntryElement> entries,
                                        out PlayerConditionalWeaponSwitchEntryElement entry)
    {
        entry = default;

        if (entryIndex < 0 || entryIndex >= entries.Length)
            return false;

        entry = entries[entryIndex];
        return true;
    }

    /// <summary>
    /// Resolves one flattened condition through its owning entry and condition-local authored index.
    /// </summary>
    /// <param name="entryIndex">Owning entry index.</param>
    /// <param name="conditionIndex">Condition-local index inside the entry.</param>
    /// <param name="entries">Runtime entry buffer.</param>
    /// <param name="conditions">Runtime flattened condition buffer.</param>
    /// <param name="globalConditionIndex">Resolved flattened condition index.</param>
    /// <param name="condition">Resolved condition when indices are valid.</param>
    /// <returns>True when the condition exists.</returns>
    private static bool TryResolveCondition(int entryIndex,
                                            int conditionIndex,
                                            DynamicBuffer<PlayerConditionalWeaponSwitchEntryElement> entries,
                                            DynamicBuffer<PlayerConditionalWeaponSwitchConditionElement> conditions,
                                            out int globalConditionIndex,
                                            out PlayerConditionalWeaponSwitchConditionElement condition)
    {
        globalConditionIndex = -1;
        condition = default;

        if (!TryResolveEntry(entryIndex, entries, out PlayerConditionalWeaponSwitchEntryElement entry) ||
            conditionIndex < 0 ||
            conditionIndex >= entry.ConditionCount)
        {
            return false;
        }

        globalConditionIndex = entry.ConditionStartIndex + conditionIndex;

        if (globalConditionIndex < 0 || globalConditionIndex >= conditions.Length)
            return false;

        condition = conditions[globalConditionIndex];
        return true;
    }

    /// <summary>
    /// Converts a finite rounded formula result into the full supported priority integer range.
    /// </summary>
    /// <param name="value">Finite formula result.</param>
    /// <returns>Clamped integer priority.</returns>
    private static int ResolvePriority(float value)
    {
        if (value >= int.MaxValue)
            return int.MaxValue;

        if (value <= int.MinValue)
            return int.MinValue;

        return (int)value;
    }
    #endregion

    #endregion
}
