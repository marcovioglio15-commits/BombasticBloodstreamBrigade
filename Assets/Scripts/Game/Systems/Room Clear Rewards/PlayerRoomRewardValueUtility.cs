using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Applies typed scalable-stat and clamped resource changes shared by permanent grants and temporary stipends.
/// </summary>
public static class PlayerRoomRewardValueUtility
{
    #region Fields
    private static readonly Dictionary<string, PlayerFormulaValue> variableContext =
        new Dictionary<string, PlayerFormulaValue>(StringComparer.OrdinalIgnoreCase);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies one permanent room reward module directly to the authoritative scalable-stat buffer.
    /// </summary>
    /// <param name="module">Baked module being applied.</param>
    /// <param name="scalableStats">Mutable player scalable-stat buffer.</param>
    /// <param name="previousValue">Typed value stored before the operation.</param>
    /// <param name="appliedValue">Typed value stored after the operation.</param>
    /// <returns>True when the target exists and receives a valid typed value.</returns>
    public static bool TryApplyScalableStat(in GameRoomRewardModuleElement module,
                                            DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                            out PlayerFormulaValue previousValue,
                                            out PlayerFormulaValue appliedValue)
    {
        previousValue = PlayerFormulaValue.CreateInvalid();
        appliedValue = PlayerFormulaValue.CreateInvalid();
        int statIndex = PlayerPowerUpCharacterTuningRuntimeUtility.FindScalableStatIndex(scalableStats,
                                                                                        module.TargetStatName.ToString());

        if (statIndex < 0)
            return false;

        PlayerScalableStatElement scalableStat = scalableStats[statIndex];
        previousValue = PlayerScalableStatValueUtility.ResolveRuntimeValue(in scalableStat);

        if (!TryResolveScalableStatValue(in module,
                                         scalableStats,
                                         in scalableStat,
                                         out PlayerFormulaValue resolvedValue))
        {
            return false;
        }

        if (!PlayerScalableStatValueUtility.TryWriteRuntimeValue(ref scalableStat,
                                                                 resolvedValue,
                                                                 out string _))
        {
            return false;
        }

        scalableStats[statIndex] = scalableStat;
        appliedValue = PlayerScalableStatValueUtility.ResolveRuntimeValue(in scalableStat);
        return true;
    }

    /// <summary>
    /// Applies one resource module and returns the post-clamp delta used by presentation.
    /// </summary>
    /// <param name="resource">Target player resource.</param>
    /// <param name="valueSource">Flat or formula value source.</param>
    /// <param name="flatValue">Flat delta when no formula is used.</param>
    /// <param name="formula">Unified formula expression that resolves a resource delta.</param>
    /// <param name="scalableStats">Authoritative scalable stats updated when experience changes.</param>
    /// <param name="formulaScalableStats">Optional effective stat projection exposed to formula variables.</param>
    /// <param name="health">Mutable player health.</param>
    /// <param name="experience">Mutable player experience.</param>
    /// <param name="powerUpsState">Mutable active power-up energy state.</param>
    /// <param name="powerUpsConfig">Current active power-up slot configuration.</param>
    /// <returns>Actual post-clamp delta applied to the selected resource.</returns>
    public static float ApplyResource(GameRoomRewardResource resource,
                                      GameRoomRewardValueSource valueSource,
                                      float flatValue,
                                      string formula,
                                      DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                      IReadOnlyList<PlayerScalableStatElement> formulaScalableStats,
                                      ref PlayerHealth health,
                                      ref PlayerExperience experience,
                                      ref PlayerPowerUpsState powerUpsState,
                                      in PlayerPowerUpsConfig powerUpsConfig)
    {
        float currentValue = ResolveResourceValue(resource,
                                                  in health,
                                                  in experience,
                                                  in powerUpsState);
        float delta = flatValue;

        if (valueSource == GameRoomRewardValueSource.Formula)
        {
            if (formulaScalableStats == null)
                PlayerScalingRuntimeFormulaUtility.FillVariableContext(scalableStats, variableContext);
            else
                PlayerScalingRuntimeFormulaUtility.FillVariableContext(formulaScalableStats, variableContext);

            if (!PlayerScalingRuntimeFormulaUtility.TryEvaluateFormula(formula,
                                                                        currentValue,
                                                                        variableContext,
                                                                        out delta,
                                                                        out string _,
                                                                        false))
            {
                return 0f;
            }
        }

        switch (resource)
        {
            case GameRoomRewardResource.Health:
                health.Current = math.clamp(currentValue + delta, 0f, math.max(0f, health.Max));
                break;
            case GameRoomRewardResource.PrimaryPowerUpEnergy:
                powerUpsState.PrimaryEnergy = math.clamp(currentValue + delta,
                                                        0f,
                                                        math.max(0f, powerUpsConfig.PrimarySlot.MaximumEnergy));
                break;
            case GameRoomRewardResource.SecondaryPowerUpEnergy:
                powerUpsState.SecondaryEnergy = math.clamp(currentValue + delta,
                                                          0f,
                                                          math.max(0f, powerUpsConfig.SecondarySlot.MaximumEnergy));
                break;
            case GameRoomRewardResource.Experience:
                experience.Current = math.max(0f, currentValue + delta);
                TryWriteExperienceStat(scalableStats, experience.Current);
                break;
        }

        return ResolveResourceValue(resource,
                                    in health,
                                    in experience,
                                    in powerUpsState) - currentValue;
    }

    /// <summary>
    /// Evaluates one formula module against the current authoritative player snapshot without mutating ECS state.
    /// Scalable-stat formulas return their typed requested value, while resource formulas return their numeric delta,
    /// matching the semantics consumed by the production grant path.
    /// </summary>
    /// <param name="module">Baked formula module to preview.</param>
    /// <param name="scalableStats">Current authoritative scalable-stat buffer exposed to named variables.</param>
    /// <param name="health">Current player health used for a Health resource formula's reserved [this] value.</param>
    /// <param name="experience">Current player experience used for an Experience formula's reserved [this] value.</param>
    /// <param name="powerUpsState">Current energy state used by active power-up resource formulas.</param>
    /// <param name="baseValue">Current typed target value mapped to the reserved [this] token.</param>
    /// <param name="formulaResult">Typed formula result using the same evaluation rules as an authoritative grant.</param>
    /// <returns>True when the formula and its target resolve successfully against the player snapshot.</returns>
    public static bool TryEvaluateFormulaPreview(
        in GameRoomRewardModuleElement module,
        DynamicBuffer<PlayerScalableStatElement> scalableStats,
        in PlayerHealth health,
        in PlayerExperience experience,
        in PlayerPowerUpsState powerUpsState,
        out PlayerFormulaValue baseValue,
        out PlayerFormulaValue formulaResult)
    {
        baseValue = PlayerFormulaValue.CreateInvalid();
        formulaResult = PlayerFormulaValue.CreateInvalid();

        if (module.ValueSource != GameRoomRewardValueSource.Formula)
            return false;

        if (module.TargetDomain == GameRoomRewardTargetDomain.ScalableStat)
        {
            int statIndex =
                PlayerPowerUpCharacterTuningRuntimeUtility.FindScalableStatIndex(
                    scalableStats,
                    module.TargetStatName.ToString());

            if (statIndex < 0)
                return false;

            PlayerScalableStatElement scalableStat = scalableStats[statIndex];
            baseValue =
                PlayerScalableStatValueUtility.ResolveRuntimeValue(in scalableStat);

            if (!TryResolveScalableStatValue(in module,
                                             scalableStats,
                                             in scalableStat,
                                             out PlayerFormulaValue requestedValue))
            {
                return false;
            }

            PlayerScalableStatElement previewStat = scalableStat;

            if (!PlayerScalableStatValueUtility.TryWriteRuntimeValue(
                    ref previewStat,
                    requestedValue,
                    out string _))
            {
                return false;
            }

            formulaResult =
                PlayerScalableStatValueUtility.ResolveRuntimeValue(in previewStat);
            return true;
        }

        float currentResourceValue = ResolveResourceValue(module.Resource,
                                                          in health,
                                                          in experience,
                                                          in powerUpsState);
        baseValue = PlayerFormulaValue.CreateNumber(currentResourceValue);
        PlayerScalingRuntimeFormulaUtility.FillVariableContext(scalableStats,
                                                               variableContext);

        if (!PlayerScalingRuntimeFormulaUtility.TryEvaluateFormula(
                module.Formula.ToString(),
                currentResourceValue,
                variableContext,
                out float numericResult,
                out string _,
                false))
        {
            return false;
        }

        formulaResult = PlayerFormulaValue.CreateNumber(numericResult);
        return true;
    }

    /// <summary>
    /// Resolves the acquisition-time value shown for a temporary schedule without mutating player state.
    /// Formula failures retain their source marker so presentation can expose an explicit unavailable value.
    /// </summary>
    /// <param name="module">Temporary module whose presentation payload is projected.</param>
    /// <param name="scalableStats">Current authoritative stats exposed to formula variables.</param>
    /// <param name="health">Current player health used by resource formulas.</param>
    /// <param name="experience">Current player experience used by resource formulas.</param>
    /// <param name="powerUpsState">Current player energy state used by resource formulas.</param>
    /// <param name="presentationValueSource">Flat when a display value resolved, otherwise Formula.</param>
    /// <param name="numericDelta">Projected numeric delta for resource and numeric-stat targets.</param>
    /// <param name="booleanValue">Projected boolean target value.</param>
    /// <param name="tokenValue">Projected token target value.</param>
    public static void ResolveScheduledPresentationValue(
        in GameRoomRewardModuleElement module,
        DynamicBuffer<PlayerScalableStatElement> scalableStats,
        in PlayerHealth health,
        in PlayerExperience experience,
        in PlayerPowerUpsState powerUpsState,
        out GameRoomRewardValueSource presentationValueSource,
        out float numericDelta,
        out byte booleanValue,
        out FixedString64Bytes tokenValue)
    {
        // Start from the authored flat payload so flat modules avoid formula work.
        presentationValueSource = GameRoomRewardValueSource.Flat;
        numericDelta = module.FlatNumericValue;
        booleanValue = module.FlatBooleanValue;
        tokenValue = module.FlatTokenValue;

        if (module.ValueSource != GameRoomRewardValueSource.Formula)
            return;

        // Evaluate formulas through the same parser, variable context and target normalization as grants.
        if (!TryEvaluateFormulaPreview(in module,
                                       scalableStats,
                                       in health,
                                       in experience,
                                       in powerUpsState,
                                       out PlayerFormulaValue baseValue,
                                       out PlayerFormulaValue formulaResult))
        {
            presentationValueSource = GameRoomRewardValueSource.Formula;
            return;
        }

        // Convert the typed absolute formula result into the delta-oriented presentation payload.
        switch (formulaResult.Type)
        {
            case PlayerFormulaValueType.Boolean:
                booleanValue = formulaResult.BooleanValue ? (byte)1 : (byte)0;
                break;
            case PlayerFormulaValueType.Token:
                tokenValue = new FixedString64Bytes(formulaResult.TokenValue);
                break;
            case PlayerFormulaValueType.Number:
                numericDelta = module.TargetDomain == GameRoomRewardTargetDomain.Resource
                    ? formulaResult.NumberValue
                    : formulaResult.NumberValue - baseValue.NumberValue;
                break;
            default:
                presentationValueSource = GameRoomRewardValueSource.Formula;
                break;
        }
    }

    /// <summary>
    /// Resolves the current numeric amount of one supported player resource.
    /// </summary>
    /// <param name="resource">Resource whose value is requested.</param>
    /// <param name="health">Current player health.</param>
    /// <param name="experience">Current player experience.</param>
    /// <param name="powerUpsState">Current active power-up energy state.</param>
    /// <returns>Current numeric resource value.</returns>
    public static float ResolveResourceValue(GameRoomRewardResource resource,
                                             in PlayerHealth health,
                                             in PlayerExperience experience,
                                             in PlayerPowerUpsState powerUpsState)
    {
        switch (resource)
        {
            case GameRoomRewardResource.Health:
                return health.Current;
            case GameRoomRewardResource.PrimaryPowerUpEnergy:
                return powerUpsState.PrimaryEnergy;
            case GameRoomRewardResource.SecondaryPowerUpEnergy:
                return powerUpsState.SecondaryEnergy;
            case GameRoomRewardResource.Experience:
                return experience.Current;
            default:
                return 0f;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves a flat alteration or assignment formula into a typed scalable-stat value.
    /// </summary>
    /// <param name="module">Baked module being evaluated.</param>
    /// <param name="scalableStats">Current stat buffer exposed to formula variables.</param>
    /// <param name="scalableStat">Target stat metadata and current value.</param>
    /// <param name="resolvedValue">Typed result ready for normalized storage.</param>
    /// <returns>True when the authored value is compatible with the target stat.</returns>
    private static bool TryResolveScalableStatValue(in GameRoomRewardModuleElement module,
                                                    DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                                    in PlayerScalableStatElement scalableStat,
                                                    out PlayerFormulaValue resolvedValue)
    {
        PlayerFormulaValue currentValue = PlayerScalableStatValueUtility.ResolveRuntimeValue(in scalableStat);

        if (module.ValueSource == GameRoomRewardValueSource.Flat)
        {
            resolvedValue = ResolveFlatScalableStatValue(in module, in currentValue);
            return resolvedValue.IsValid;
        }

        if (!PlayerCharacterTuningFormulaUtility.TryParseAssignmentFormula(module.Formula.ToString(),
                                                                           out string targetName,
                                                                           out string expression,
                                                                           out string _))
        {
            resolvedValue = PlayerFormulaValue.CreateInvalid();
            return false;
        }

        if (!string.Equals(targetName, module.TargetStatName.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            resolvedValue = PlayerFormulaValue.CreateInvalid();
            return false;
        }

        PlayerScalingRuntimeFormulaUtility.FillVariableContext(scalableStats, variableContext);
        return PlayerScalingRuntimeFormulaUtility.TryEvaluateFormula(expression,
                                                                      currentValue,
                                                                      variableContext,
                                                                      out resolvedValue,
                                                                      out string _,
                                                                      false);
    }

    /// <summary>
    /// Resolves flat stat semantics: numeric values are deltas while Boolean and Token values replace the current value.
    /// </summary>
    /// <param name="module">Flat baked module.</param>
    /// <param name="currentValue">Current typed stat value.</param>
    /// <returns>Typed requested value, or an invalid value for unsupported stat types.</returns>
    private static PlayerFormulaValue ResolveFlatScalableStatValue(in GameRoomRewardModuleElement module,
                                                                   in PlayerFormulaValue currentValue)
    {
        switch (module.TargetStatType)
        {
            case PlayerScalableStatType.Float:
            case PlayerScalableStatType.Integer:
            case PlayerScalableStatType.Unsigned:
                return PlayerFormulaValue.CreateNumber(currentValue.NumberValue + module.FlatNumericValue);
            case PlayerScalableStatType.Boolean:
                return PlayerFormulaValue.CreateBoolean(module.FlatBooleanValue != 0);
            case PlayerScalableStatType.Token:
                return PlayerFormulaValue.CreateToken(module.FlatTokenValue.ToString());
            default:
                return PlayerFormulaValue.CreateInvalid();
        }
    }

    /// <summary>
    /// Synchronizes the reserved experience scalable stat after a direct resource grant.
    /// </summary>
    /// <param name="scalableStats">Mutable scalable-stat buffer.</param>
    /// <param name="experienceValue">Post-clamp experience amount.</param>
    private static void TryWriteExperienceStat(DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                               float experienceValue)
    {
        int statIndex = PlayerPowerUpCharacterTuningRuntimeUtility.FindScalableStatIndex(scalableStats, "experience");

        if (statIndex < 0)
            return;

        PlayerScalableStatElement scalableStat = scalableStats[statIndex];

        if (!PlayerScalableStatValueUtility.TryWriteRuntimeValue(ref scalableStat,
                                                                 PlayerFormulaValue.CreateNumber(experienceValue),
                                                                 out string _))
        {
            return;
        }

        scalableStats[statIndex] = scalableStat;
    }
    #endregion

    #endregion
}
