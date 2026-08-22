using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;

/// <summary>
/// Validates reusable and binding-local room reward module payloads before ECS flattening.
/// </summary>
public static class GameRoomRewardModuleValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates module identities, targets, formulas, typed flat values and temporary durations.
    /// </summary>
    /// <param name="preset">Reward preset being validated.</param>
    /// <param name="failureMessage">First reusable-module validation failure.</param>
    /// <returns>True when every reusable module satisfies the runtime contract.</returns>
    public static bool ValidateModules(GameRoomClearRewardsPreset preset,
                                       out string failureMessage)
    {
        HashSet<string> identifiers = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < preset.Modules.Count; index++)
        {
            GameRoomRewardModuleDefinition module = preset.Modules[index];

            if (module == null)
            {
                failureMessage = string.Format(
                    "Reward module at index {0} is null.",
                    index);
                return false;
            }

            if (!ValidateIdentity(module.TechnicalId,
                                  identifiers,
                                  "reward module",
                                  out failureMessage))
            {
                return false;
            }

            if (!ValidatePayload(preset,
                                 module.TargetDomain,
                                 module.ValueSource,
                                 module.Duration,
                                 module.TargetStatName,
                                 module.Formula,
                                 module.FlatNumericValue,
                                 module.DurationRooms,
                                 module.DisplayName,
                                 out failureMessage))
            {
                return false;
            }

            if (Encoding.UTF8.GetByteCount(module.DisplayName ?? string.Empty) >
                FixedString128Bytes.UTF8MaxLengthInBytes ||
                Encoding.UTF8.GetByteCount(module.Description ?? string.Empty) >
                FixedString128Bytes.UTF8MaxLengthInBytes ||
                !ValidatePayloadCapacity(module.TargetStatName,
                                         module.Formula,
                                         module.FlatTokenValue))
            {
                failureMessage = string.Format(
                    "Reward module '{0}' contains text that exceeds its baked UTF-8 capacity.",
                    module.DisplayName);
                return false;
            }
        }

        failureMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// Validates composed reward identities, references and optional binding-local module payloads.
    /// </summary>
    /// <param name="preset">Reward preset being validated.</param>
    /// <param name="failureMessage">First composed-reward validation failure.</param>
    /// <returns>True when every composed reward has valid ordered bindings and overrides.</returns>
    public static bool ValidateRewards(GameRoomClearRewardsPreset preset,
                                       out string failureMessage)
    {
        HashSet<string> rewardIdentifiers =
            new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> bindingIdentifiers =
            new HashSet<string>(StringComparer.Ordinal);

        for (int rewardIndex = 0;
             rewardIndex < preset.Rewards.Count;
             rewardIndex++)
        {
            GameRoomRewardDefinition reward = preset.Rewards[rewardIndex];

            if (reward == null)
            {
                failureMessage = string.Format(
                    "Room reward at index {0} is null.",
                    rewardIndex);
                return false;
            }

            if (!ValidateIdentity(reward.TechnicalId,
                                  rewardIdentifiers,
                                  "room reward",
                                  out failureMessage))
            {
                return false;
            }

            for (int bindingIndex = 0;
                 bindingIndex < reward.Modules.Count;
                 bindingIndex++)
            {
                GameRoomRewardModuleBinding binding =
                    reward.Modules[bindingIndex];

                if (binding == null ||
                    !preset.TryFindModule(
                        binding.ModuleTechnicalId,
                        out GameRoomRewardModuleDefinition sourceModule))
                {
                    failureMessage = string.Format(
                        "Room reward '{0}' contains a missing module reference at index {1}.",
                        reward.DisplayName,
                        bindingIndex);
                    return false;
                }

                if (binding.Quantity <= 0)
                {
                    failureMessage = string.Format(
                        "Room reward '{0}' contains a module quantity that is not greater than zero.",
                        reward.DisplayName);
                    return false;
                }

                if (!ValidateIdentity(binding.BindingId,
                                      bindingIdentifiers,
                                      "room reward module binding",
                                      out failureMessage))
                {
                    return false;
                }

                if (binding.UseOverridePayload &&
                    !ValidateOverride(preset,
                                      sourceModule,
                                      binding,
                                      reward.DisplayName,
                                      out failureMessage))
                {
                    return false;
                }
            }
        }

        failureMessage = string.Empty;
        return true;
    }
    #endregion

    #region Payload Validation
    /// <summary>
    /// Validates one binding-local payload against its referenced module category.
    /// </summary>
    /// <param name="preset">Reward preset supplying stat definitions and formula variables.</param>
    /// <param name="sourceModule">Referenced module supplying fixed category axes.</param>
    /// <param name="binding">Binding supplying the local payload.</param>
    /// <param name="rewardName">Owning reward name used in diagnostics.</param>
    /// <param name="failureMessage">First local-payload validation failure.</param>
    /// <returns>True when the payload can be flattened without correction.</returns>
    private static bool ValidateOverride(GameRoomClearRewardsPreset preset,
                                         GameRoomRewardModuleDefinition sourceModule,
                                         GameRoomRewardModuleBinding binding,
                                         string rewardName,
                                         out string failureMessage)
    {
        GameRoomRewardModuleOverridePayload payload = binding.OverridePayload;

        if (payload == null)
        {
            failureMessage = string.Format(
                "Room reward '{0}' enables a module override without payload storage.",
                rewardName);
            return false;
        }

        string context = string.Format(
            "Room reward '{0}' override for module '{1}'",
            rewardName,
            sourceModule.DisplayName);

        if (!ValidatePayload(preset,
                             sourceModule.TargetDomain,
                             sourceModule.ValueSource,
                             sourceModule.Duration,
                             payload.TargetStatName,
                             payload.Formula,
                             payload.FlatNumericValue,
                             payload.DurationRooms,
                             context,
                             out failureMessage))
        {
            return false;
        }

        if (!ValidatePayloadCapacity(payload.TargetStatName,
                                     payload.Formula,
                                     payload.FlatTokenValue))
        {
            failureMessage = context + " exceeds an ECS text capacity.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates one resolved module payload shared by defaults and binding-local overrides.
    /// </summary>
    /// <param name="preset">Reward preset supplying stat definitions and formula variables.</param>
    /// <param name="targetDomain">Resolved player data domain.</param>
    /// <param name="valueSource">Resolved Flat or Formula source.</param>
    /// <param name="duration">Resolved permanent or temporary lifetime.</param>
    /// <param name="targetStatName">Resolved scalable-stat target.</param>
    /// <param name="formula">Resolved unified formula.</param>
    /// <param name="flatNumericValue">Resolved flat numeric value.</param>
    /// <param name="durationRooms">Resolved future-room duration.</param>
    /// <param name="context">Module or override identity shown in validation.</param>
    /// <param name="failureMessage">First payload validation failure.</param>
    /// <returns>True when the resolved payload satisfies its inherited category.</returns>
    private static bool ValidatePayload(GameRoomClearRewardsPreset preset,
                                        GameRoomRewardTargetDomain targetDomain,
                                        GameRoomRewardValueSource valueSource,
                                        GameRoomRewardDuration duration,
                                        string targetStatName,
                                        string formula,
                                        float flatNumericValue,
                                        int durationRooms,
                                        string context,
                                        out string failureMessage)
    {
        PlayerScalableStatDefinition targetStat = null;

        if (targetDomain == GameRoomRewardTargetDomain.ScalableStat &&
            !TryResolveStat(preset, targetStatName, out targetStat))
        {
            failureMessage = string.Format(
                "{0} targets unknown scalable stat '{1}'.",
                context,
                targetStatName);
            return false;
        }

        if (RequiresFiniteFlatNumeric(valueSource,
                                      targetDomain,
                                      targetStat) &&
            (float.IsNaN(flatNumericValue) ||
             float.IsInfinity(flatNumericValue)))
        {
            failureMessage = context +
                             " requires a finite flat numeric value.";
            return false;
        }

        if (!GameRoomRewardFormulaValidationUtility.TryValidate(
                preset,
                targetDomain,
                valueSource,
                targetStatName,
                formula,
                out string formulaFailure))
        {
            failureMessage = context +
                             " has an invalid formula: " +
                             formulaFailure;
            return false;
        }

        if (duration == GameRoomRewardDuration.Temporary &&
            durationRooms <= 0)
        {
            failureMessage = context +
                             " requires Future Rooms greater than zero.";
            return false;
        }

        failureMessage = string.Empty;
        return true;
    }
    #endregion

    #region Resolution
    /// <summary>
    /// Resolves whether one Flat payload consumes its numeric value.
    /// </summary>
    /// <param name="valueSource">Resolved value source.</param>
    /// <param name="targetDomain">Resolved player data domain.</param>
    /// <param name="targetStat">Resolved scalable-stat definition when applicable.</param>
    /// <returns>True for resources and numeric scalable-stat types.</returns>
    private static bool RequiresFiniteFlatNumeric(
        GameRoomRewardValueSource valueSource,
        GameRoomRewardTargetDomain targetDomain,
        PlayerScalableStatDefinition targetStat)
    {
        if (valueSource != GameRoomRewardValueSource.Flat)
            return false;

        if (targetDomain == GameRoomRewardTargetDomain.Resource)
            return true;

        if (targetStat == null)
            return false;

        switch (targetStat.StatType)
        {
            case PlayerScalableStatType.Float:
            case PlayerScalableStatType.Integer:
            case PlayerScalableStatType.Unsigned:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Finds one scalable stat using unified-formula name semantics.
    /// </summary>
    /// <param name="preset">Reward preset containing the linked Player Context.</param>
    /// <param name="statName">Scalable-stat name to resolve.</param>
    /// <param name="definition">Matching stat definition when available.</param>
    /// <returns>True when a matching non-null stat exists.</returns>
    private static bool TryResolveStat(GameRoomClearRewardsPreset preset,
                                       string statName,
                                       out PlayerScalableStatDefinition definition)
    {
        definition = null;
        IReadOnlyList<PlayerScalableStatDefinition> stats =
            preset.PlayerContextPreset.ProgressionPreset.ScalableStats;

        for (int index = 0; index < stats.Count; index++)
        {
            PlayerScalableStatDefinition candidate = stats[index];

            if (candidate == null ||
                !string.Equals(candidate.StatName,
                               statName,
                               StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            definition = candidate;
            return true;
        }

        return false;
    }
    #endregion

    #region Fixed Strings
    /// <summary>
    /// Validates one stable identifier for presence, uniqueness and ECS capacity.
    /// </summary>
    /// <param name="identifier">Authored technical identifier.</param>
    /// <param name="identifiers">Previously visited identifiers.</param>
    /// <param name="context">Definition kind shown in validation.</param>
    /// <param name="failureMessage">Validation failure when invalid.</param>
    /// <returns>True when the identifier is nonempty, unique and runtime-safe.</returns>
    private static bool ValidateIdentity(string identifier,
                                         HashSet<string> identifiers,
                                         string context,
                                         out string failureMessage)
    {
        if (string.IsNullOrWhiteSpace(identifier) ||
            Encoding.UTF8.GetByteCount(identifier) >
            FixedString64Bytes.UTF8MaxLengthInBytes)
        {
            failureMessage = string.Format(
                "A {0} has an empty or oversized technical identifier.",
                context);
            return false;
        }

        if (!identifiers.Add(identifier))
        {
            failureMessage = string.Format(
                "Duplicate {0} technical identifier '{1}'.",
                context,
                identifier);
            return false;
        }

        failureMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// Validates the three payload strings stored by one flattened ECS module.
    /// </summary>
    /// <param name="targetStatName">Optional scalable-stat target.</param>
    /// <param name="formula">Optional unified formula.</param>
    /// <param name="flatTokenValue">Optional flat Token value.</param>
    /// <returns>True when every payload fits its destination fixed string.</returns>
    private static bool ValidatePayloadCapacity(string targetStatName,
                                                string formula,
                                                string flatTokenValue)
    {
        return Encoding.UTF8.GetByteCount(targetStatName ?? string.Empty) <=
               FixedString64Bytes.UTF8MaxLengthInBytes &&
               Encoding.UTF8.GetByteCount(formula ?? string.Empty) <=
               FixedString512Bytes.UTF8MaxLengthInBytes &&
               Encoding.UTF8.GetByteCount(flatTokenValue ?? string.Empty) <=
               FixedString64Bytes.UTF8MaxLengthInBytes;
    }
    #endregion

    #endregion
}
