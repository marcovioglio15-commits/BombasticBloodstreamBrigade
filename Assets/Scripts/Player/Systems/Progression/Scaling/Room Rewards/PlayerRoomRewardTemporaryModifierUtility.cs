using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Projects active room-scoped modifiers over base scalable stats without mutating their permanent ECS storage.
/// </summary>
public static class PlayerRoomRewardTemporaryModifierUtility
{
    #region Constants
    private const uint FnvOffsetBasis = 2166136261u;
    private const uint FnvPrime = 16777619u;
    #endregion

    #region Fields
    private static readonly Dictionary<string, PlayerFormulaValue> VariableContext =
        new Dictionary<string, PlayerFormulaValue>(64, StringComparer.OrdinalIgnoreCase);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies every active modifier in deterministic acquisition order to an already copied effective stat list.
    /// </summary>
    /// <param name="modifiers">Pending and active room reward modifier buffer.</param>
    /// <param name="visitOrdinal">Current distinct room visit ordinal.</param>
    /// <param name="effectiveScalableStats">Mutable effective stat list copied from permanent base storage.</param>
    public static void ApplyActiveModifiers(
        DynamicBuffer<PlayerRoomRewardTemporaryModifierElement> modifiers,
        uint visitOrdinal,
        List<PlayerScalableStatElement> effectiveScalableStats)
    {
        if (!modifiers.IsCreated || modifiers.Length == 0 || effectiveScalableStats == null)
            return;

        // Buffer append order is authoritative acquisition order and is preserved by expiration cleanup.
        for (int index = 0; index < modifiers.Length; index++)
        {
            PlayerRoomRewardTemporaryModifierElement modifier = modifiers[index];

            if (visitOrdinal < modifier.ActiveFromVisitOrdinal ||
                visitOrdinal >= modifier.ExpireAtVisitOrdinal)
            {
                continue;
            }

            ApplyModifier(in modifier, effectiveScalableStats);
        }
    }

    /// <summary>
    /// Combines temporary modifier content and visit state into the scalable-config early-out hash.
    /// </summary>
    /// <param name="baseHash">Hash of permanent scalable-stat storage.</param>
    /// <param name="modifiers">Pending and active temporary modifier buffer.</param>
    /// <param name="temporaryState">Versioned distinct-room visit state.</param>
    /// <returns>Stable hash representing permanent and room-scoped effective stat inputs.</returns>
    public static uint ComputeEffectiveHash(
        uint baseHash,
        DynamicBuffer<PlayerRoomRewardTemporaryModifierElement> modifiers,
        in PlayerRoomRewardTemporaryState temporaryState)
    {
        uint rollingHash = (FnvOffsetBasis ^ baseHash) * FnvPrime;
        rollingHash = (rollingHash ^ temporaryState.Version) * FnvPrime;
        rollingHash = (rollingHash ^ temporaryState.LastVisitOrdinal) * FnvPrime;

        if (!modifiers.IsCreated)
            return rollingHash;

        for (int index = 0; index < modifiers.Length; index++)
        {
            PlayerRoomRewardTemporaryModifierElement modifier = modifiers[index];
            rollingHash = (rollingHash ^ (uint)modifier.ModuleTechnicalId.GetHashCode()) * FnvPrime;
            rollingHash = (rollingHash ^ (uint)modifier.TargetStatName.GetHashCode()) * FnvPrime;
            rollingHash = (rollingHash ^ modifier.ActiveFromVisitOrdinal) * FnvPrime;
            rollingHash = (rollingHash ^ modifier.ExpireAtVisitOrdinal) * FnvPrime;
            rollingHash = (rollingHash ^ modifier.GrantSequence) * FnvPrime;
            rollingHash = (rollingHash ^ math.asuint(modifier.FlatNumericValue)) * FnvPrime;
            rollingHash = (rollingHash ^ modifier.FlatBooleanValue) * FnvPrime;
            rollingHash = (rollingHash ^ (uint)modifier.Formula.GetHashCode()) * FnvPrime;
            rollingHash = (rollingHash ^ (uint)modifier.FlatTokenValue.GetHashCode()) * FnvPrime;
        }

        return (rollingHash ^ (uint)modifiers.Length) * FnvPrime;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies one active typed modifier to its target entry in the effective list.
    /// </summary>
    /// <param name="modifier">Active room reward modifier.</param>
    /// <param name="effectiveScalableStats">Mutable effective scalable-stat list.</param>
    private static void ApplyModifier(in PlayerRoomRewardTemporaryModifierElement modifier,
                                      List<PlayerScalableStatElement> effectiveScalableStats)
    {
        int statIndex = PlayerPowerUpCharacterTuningRuntimeUtility.FindScalableStatIndex(effectiveScalableStats,
                                                                                        modifier.TargetStatName.ToString());

        if (statIndex < 0)
            return;

        PlayerScalableStatElement scalableStat = effectiveScalableStats[statIndex];
        PlayerFormulaValue currentValue = PlayerScalableStatValueUtility.ResolveRuntimeValue(in scalableStat);
        PlayerFormulaValue resolvedValue;

        if (modifier.ValueSource == GameRoomRewardValueSource.Flat)
            resolvedValue = ResolveFlatValue(in modifier, in currentValue);
        else if (!TryEvaluateFormula(in modifier, effectiveScalableStats, in currentValue, out resolvedValue))
            return;

        if (!PlayerScalableStatValueUtility.TryWriteRuntimeValue(ref scalableStat,
                                                                 resolvedValue,
                                                                 out string _))
        {
            return;
        }

        effectiveScalableStats[statIndex] = scalableStat;
    }

    /// <summary>
    /// Evaluates one assignment formula against the effective results of prior temporary modifiers.
    /// </summary>
    /// <param name="modifier">Formula-backed temporary modifier.</param>
    /// <param name="effectiveScalableStats">Current effective stat sequence.</param>
    /// <param name="currentValue">Current typed target value mapped to the reserved this token.</param>
    /// <param name="resolvedValue">Typed formula result.</param>
    /// <returns>True when assignment parsing, target validation and formula evaluation succeed.</returns>
    private static bool TryEvaluateFormula(in PlayerRoomRewardTemporaryModifierElement modifier,
                                           List<PlayerScalableStatElement> effectiveScalableStats,
                                           in PlayerFormulaValue currentValue,
                                           out PlayerFormulaValue resolvedValue)
    {
        if (!PlayerCharacterTuningFormulaUtility.TryParseAssignmentFormula(modifier.Formula.ToString(),
                                                                           out string targetName,
                                                                           out string expression,
                                                                           out string _))
        {
            resolvedValue = PlayerFormulaValue.CreateInvalid();
            return false;
        }

        if (!string.Equals(targetName, modifier.TargetStatName.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            resolvedValue = PlayerFormulaValue.CreateInvalid();
            return false;
        }

        PlayerScalingRuntimeFormulaUtility.FillVariableContext(effectiveScalableStats, VariableContext);
        return PlayerScalingRuntimeFormulaUtility.TryEvaluateFormula(expression,
                                                                      currentValue,
                                                                      VariableContext,
                                                                      out resolvedValue,
                                                                      out string _,
                                                                      false);
    }

    /// <summary>
    /// Resolves numeric-delta or typed replacement semantics for one flat temporary modifier.
    /// </summary>
    /// <param name="modifier">Flat temporary modifier.</param>
    /// <param name="currentValue">Current typed target value.</param>
    /// <returns>Typed value requested by the modifier.</returns>
    private static PlayerFormulaValue ResolveFlatValue(in PlayerRoomRewardTemporaryModifierElement modifier,
                                                       in PlayerFormulaValue currentValue)
    {
        switch (modifier.TargetStatType)
        {
            case PlayerScalableStatType.Float:
            case PlayerScalableStatType.Integer:
            case PlayerScalableStatType.Unsigned:
                return PlayerFormulaValue.CreateNumber(currentValue.NumberValue + modifier.FlatNumericValue);
            case PlayerScalableStatType.Boolean:
                return PlayerFormulaValue.CreateBoolean(modifier.FlatBooleanValue != 0);
            case PlayerScalableStatType.Token:
                return PlayerFormulaValue.CreateToken(modifier.FlatTokenValue.ToString());
            default:
                return PlayerFormulaValue.CreateInvalid();
        }
    }
    #endregion

    #endregion
}
