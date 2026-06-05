using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Rebuilds scalable weapon visual references and the default optional attachment only when the unified runtime scaling
/// hash changes. Presentation resolves changed references once and keeps all per-frame weapon toggles allocation-free.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerRuntimeScalingSyncSystem))]
public partial struct PlayerRuntimeWeaponVisualScalingSystem : ISystem
{
    #region Fields
    private static readonly Dictionary<string, PlayerFormulaValue> VariableContext = new Dictionary<string, PlayerFormulaValue>(64, System.StringComparer.OrdinalIgnoreCase);
    private static readonly List<PlayerScalableStatElement> EffectiveScalableStats = new List<PlayerScalableStatElement>(64);
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime data required to rebuild scalable weapon visual configuration.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerRuntimeScalingState>();
        state.RequireForUpdate<PlayerWeaponVisualScalingState>();
        state.RequireForUpdate<PlayerBaseWeaponVisualConfig>();
        state.RequireForUpdate<PlayerVisualRuntimeBridgeConfig>();
        state.RequireForUpdate<PlayerRuntimeWeaponVisualScalingElement>();
    }

    /// <summary>
    /// Rebuilds weapon visual runtime configuration when the shared scalable-stat hash changes.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        BufferLookup<PlayerScalableStatElement> scalableStatsLookup = SystemAPI.GetBufferLookup<PlayerScalableStatElement>(true);
        BufferLookup<PlayerRuntimeWeaponVisualScalingElement> scalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeWeaponVisualScalingElement>(true);
        ComponentLookup<PlayerRuntimeComboCounterConfig> comboConfigLookup = SystemAPI.GetComponentLookup<PlayerRuntimeComboCounterConfig>(true);
        ComponentLookup<PlayerComboCounterState> comboStateLookup = SystemAPI.GetComponentLookup<PlayerComboCounterState>(true);
        BufferLookup<PlayerRuntimeComboRankElement> comboRanksLookup = SystemAPI.GetBufferLookup<PlayerRuntimeComboRankElement>(true);
        BufferLookup<PlayerPowerUpCharacterTuningFormulaElement> characterTuningLookup = SystemAPI.GetBufferLookup<PlayerPowerUpCharacterTuningFormulaElement>(true);

        foreach ((RefRO<PlayerRuntimeScalingState> runtimeScalingState,
                  RefRW<PlayerWeaponVisualScalingState> weaponScalingState,
                  RefRO<PlayerBaseWeaponVisualConfig> baseConfig,
                  RefRW<PlayerVisualRuntimeBridgeConfig> runtimeConfig,
                  Entity entity)
                 in SystemAPI.Query<RefRO<PlayerRuntimeScalingState>,
                                    RefRW<PlayerWeaponVisualScalingState>,
                                    RefRO<PlayerBaseWeaponVisualConfig>,
                                    RefRW<PlayerVisualRuntimeBridgeConfig>>()
                             .WithAll<PlayerRuntimeWeaponVisualScalingElement>()
                             .WithEntityAccess())
        {
            if (runtimeScalingState.ValueRO.Initialized == 0)
                continue;

            if (weaponScalingState.ValueRO.Initialized != 0 &&
                weaponScalingState.ValueRO.LastScalableStatsHash == runtimeScalingState.ValueRO.LastScalableStatsHash)
            {
                continue;
            }

            ApplyBaseConfig(in baseConfig.ValueRO, ref runtimeConfig.ValueRW);
            FillVariableContext(entity,
                                in scalableStatsLookup,
                                in comboConfigLookup,
                                in comboStateLookup,
                                in comboRanksLookup,
                                in characterTuningLookup);

            if (scalingLookup.HasBuffer(entity))
                ApplyScaling(scalingLookup[entity], ref runtimeConfig.ValueRW);

            weaponScalingState.ValueRW.Initialized = 1;
            weaponScalingState.ValueRW.LastScalableStatsHash = runtimeScalingState.ValueRO.LastScalableStatsHash;
        }
    }
    #endregion

    #region Scaling
    /// <summary>
    /// Rebuilds the shared typed formula context including active combo-rank Character Tuning bonuses.
    /// </summary>
    /// <param name="entity">Player entity owning the current scaling state.</param>
    /// <param name="scalableStatsLookup">Read-only base scalable-stat lookup.</param>
    /// <param name="comboConfigLookup">Read-only runtime combo config lookup.</param>
    /// <param name="comboStateLookup">Read-only combo state lookup.</param>
    /// <param name="comboRanksLookup">Read-only runtime combo-rank lookup.</param>
    /// <param name="characterTuningLookup">Read-only Character Tuning formula lookup.</param>
    private static void FillVariableContext(Entity entity,
                                            in BufferLookup<PlayerScalableStatElement> scalableStatsLookup,
                                            in ComponentLookup<PlayerRuntimeComboCounterConfig> comboConfigLookup,
                                            in ComponentLookup<PlayerComboCounterState> comboStateLookup,
                                            in BufferLookup<PlayerRuntimeComboRankElement> comboRanksLookup,
                                            in BufferLookup<PlayerPowerUpCharacterTuningFormulaElement> characterTuningLookup)
    {
        VariableContext.Clear();
        EffectiveScalableStats.Clear();

        if (!scalableStatsLookup.HasBuffer(entity))
            return;

        DynamicBuffer<PlayerScalableStatElement> scalableStats = scalableStatsLookup[entity];
        PlayerRuntimeScalingComboApplyUtility.CopyBaseScalableStats(scalableStats, EffectiveScalableStats);

        if (comboConfigLookup.HasComponent(entity) &&
            comboStateLookup.HasComponent(entity) &&
            comboRanksLookup.HasBuffer(entity) &&
            characterTuningLookup.HasBuffer(entity))
        {
            PlayerComboCounterState comboState = comboStateLookup[entity];
            PlayerRuntimeComboCounterConfig comboConfig = comboConfigLookup[entity];
            DynamicBuffer<PlayerRuntimeComboRankElement> comboRanks = comboRanksLookup[entity];
            int activeRankIndex = PlayerComboCounterRuntimeUtility.ResolveActiveRankIndex(comboState.CurrentValue,
                                                                                          in comboConfig,
                                                                                          comboRanks);
            PlayerRuntimeScalingComboApplyUtility.ApplyActiveComboRankBonuses(activeRankIndex,
                                                                              comboState.CurrentValue,
                                                                              comboRanks,
                                                                              characterTuningLookup[entity],
                                                                              EffectiveScalableStats);
        }

        PlayerScalingRuntimeFormulaUtility.FillVariableContext(EffectiveScalableStats, VariableContext);
    }

    /// <summary>
    /// Applies all weapon visual Add Scaling formulas to a freshly rebuilt runtime configuration.
    /// </summary>
    /// <param name="scalingBuffer">Runtime weapon visual scaling metadata.</param>
    /// <param name="runtimeConfig">Mutable runtime visual bridge configuration.</param>
    private static void ApplyScaling(DynamicBuffer<PlayerRuntimeWeaponVisualScalingElement> scalingBuffer,
                                     ref PlayerVisualRuntimeBridgeConfig runtimeConfig)
    {
        for (int scalingIndex = 0; scalingIndex < scalingBuffer.Length; scalingIndex++)
        {
            PlayerRuntimeWeaponVisualScalingElement scalingElement = scalingBuffer[scalingIndex];

            if ((PlayerFormulaValueType)scalingElement.ValueType == PlayerFormulaValueType.Token)
            {
                if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateTokenValue(scalingElement.Formula.ToString(),
                                                                                        scalingElement.BaseTokenValue.ToString(),
                                                                                        VariableContext,
                                                                                        out string resolvedToken))
                {
                    continue;
                }

                ApplyToken(scalingElement.FieldId, resolvedToken, ref runtimeConfig);
                continue;
            }

            if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateNumericValue(scalingElement.Formula.ToString(),
                                                                                      scalingElement.BaseValue,
                                                                                      scalingElement.IsInteger != 0,
                                                                                      VariableContext,
                                                                                      out float resolvedValue))
            {
                continue;
            }

            if (scalingElement.FieldId == PlayerRuntimeWeaponVisualFieldId.DefaultAdditionalWeaponVisual)
                runtimeConfig.DefaultAdditionalWeaponVisual = PlayerRuntimeScalingEnumUtility.ResolvePlayerDefaultAdditionalWeaponVisualSlot(resolvedValue);
        }
    }

    /// <summary>
    /// Applies one token reference selector when it fits the ECS fixed-size storage contract.
    /// </summary>
    /// <param name="fieldId">Target weapon visual field.</param>
    /// <param name="resolvedToken">Resolved token formula result.</param>
    /// <param name="runtimeConfig">Mutable runtime visual bridge configuration.</param>
    private static void ApplyToken(PlayerRuntimeWeaponVisualFieldId fieldId,
                                   string resolvedToken,
                                   ref PlayerVisualRuntimeBridgeConfig runtimeConfig)
    {
        string normalizedToken = string.IsNullOrWhiteSpace(resolvedToken) ? string.Empty : resolvedToken.Trim();

        if (Encoding.UTF8.GetByteCount(normalizedToken) > PlayerWeaponVisualSettings.MaximumReferenceSelectorUtf8Bytes)
            return;

        FixedString128Bytes reference = new FixedString128Bytes(normalizedToken);

        switch (fieldId)
        {
            case PlayerRuntimeWeaponVisualFieldId.BaseGunReference:
                runtimeConfig.BaseGunReference = reference;
                break;
            case PlayerRuntimeWeaponVisualFieldId.CannonReference:
                runtimeConfig.CannonReference = reference;
                break;
            case PlayerRuntimeWeaponVisualFieldId.GatlingReference:
                runtimeConfig.GatlingReference = reference;
                break;
            case PlayerRuntimeWeaponVisualFieldId.RailgunReference:
                runtimeConfig.RailgunReference = reference;
                break;
        }
    }

    /// <summary>
    /// Restores all scalable weapon visual fields from the immutable baseline.
    /// </summary>
    /// <param name="baseConfig">Immutable weapon visual baseline.</param>
    /// <param name="runtimeConfig">Mutable runtime visual bridge configuration.</param>
    private static void ApplyBaseConfig(in PlayerBaseWeaponVisualConfig baseConfig,
                                        ref PlayerVisualRuntimeBridgeConfig runtimeConfig)
    {
        runtimeConfig.BaseGunReference = baseConfig.BaseGunReference;
        runtimeConfig.CannonReference = baseConfig.CannonReference;
        runtimeConfig.GatlingReference = baseConfig.GatlingReference;
        runtimeConfig.RailgunReference = baseConfig.RailgunReference;
        runtimeConfig.DefaultAdditionalWeaponVisual = baseConfig.DefaultAdditionalWeaponVisual;
    }
    #endregion

    #endregion
}
