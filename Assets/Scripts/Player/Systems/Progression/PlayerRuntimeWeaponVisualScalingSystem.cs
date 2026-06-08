using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Rebuilds the scalable weapon visual references, the Default Additional Weapon Id, and the per-weapon runtime
/// buffer when the unified runtime scaling hash changes. Combo-rank-dependent Character Tuning re-applications
/// flow through this system; per-frame weapon toggles consume the rebuilt state allocation-free.
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
    /// Declares the runtime data required to rebuild scalable weapon visual configuration. The system runs only
    /// on player entities that own both the bridge config and the additional-weapons buffer.
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
        BufferLookup<PlayerBaseAdditionalWeaponVisualElement> baseAdditionalWeaponsLookup = SystemAPI.GetBufferLookup<PlayerBaseAdditionalWeaponVisualElement>(true);
        BufferLookup<PlayerAdditionalWeaponVisualElement> additionalWeaponsLookup = SystemAPI.GetBufferLookup<PlayerAdditionalWeaponVisualElement>(false);
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
            RebuildAdditionalWeaponsBuffer(entity, in baseAdditionalWeaponsLookup, ref additionalWeaponsLookup);
            FillVariableContext(entity,
                                in scalableStatsLookup,
                                in comboConfigLookup,
                                in comboStateLookup,
                                in comboRanksLookup,
                                in characterTuningLookup);

            if (scalingLookup.HasBuffer(entity))
                ApplyScaling(scalingLookup[entity],
                              ref runtimeConfig.ValueRW,
                              ref additionalWeaponsLookup,
                              entity);

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
    /// Applies all weapon visual Add Scaling formulas to the freshly rebuilt runtime configuration. Bridge-level
    /// fields are written directly; per-entry rules look up the matching runtime buffer slot by Weapon Id.
    /// </summary>
    /// <param name="scalingBuffer">Runtime weapon visual scaling metadata.</param>
    /// <param name="runtimeConfig">Mutable runtime visual bridge configuration.</param>
    /// <param name="additionalWeaponsLookup">Mutable lookup used to write per-entry token results.</param>
    /// <param name="entity">Player entity owning the additional-weapons buffer.</param>
    private static void ApplyScaling(DynamicBuffer<PlayerRuntimeWeaponVisualScalingElement> scalingBuffer,
                                     ref PlayerVisualRuntimeBridgeConfig runtimeConfig,
                                     ref BufferLookup<PlayerAdditionalWeaponVisualElement> additionalWeaponsLookup,
                                     Entity entity)
    {
        for (int scalingIndex = 0; scalingIndex < scalingBuffer.Length; scalingIndex++)
        {
            PlayerRuntimeWeaponVisualScalingElement scalingElement = scalingBuffer[scalingIndex];

            // Every weapon-visual scalable field is a string token; numeric paths are no longer used.
            if ((PlayerFormulaValueType)scalingElement.ValueType != PlayerFormulaValueType.Token)
                continue;

            if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateTokenValue(scalingElement.Formula.ToString(),
                                                                                    scalingElement.BaseTokenValue.ToString(),
                                                                                    VariableContext,
                                                                                    out string resolvedToken))
                continue;

            ApplyTokenToField(scalingElement.FieldId,
                              scalingElement.TargetEntryIndex,
                              resolvedToken,
                              ref runtimeConfig,
                              ref additionalWeaponsLookup,
                              entity);
        }
    }

    /// <summary>
    /// Applies one resolved token to the field identified by <paramref name="fieldId"/>. Per-entry fields write
    /// into the runtime buffer element at <paramref name="targetEntryIndex"/>.
    /// </summary>
    /// <param name="fieldId">Target runtime field identifier.</param>
    /// <param name="targetEntryIndex">Per-entry array index captured at bake time.</param>
    /// <param name="resolvedToken">Formula-resolved token text.</param>
    /// <param name="runtimeConfig">Mutable runtime visual bridge configuration.</param>
    /// <param name="additionalWeaponsLookup">Mutable lookup used to write per-entry token results.</param>
    /// <param name="entity">Player entity owning the additional-weapons buffer.</param>
    private static void ApplyTokenToField(PlayerRuntimeWeaponVisualFieldId fieldId,
                                           int targetEntryIndex,
                                           string resolvedToken,
                                           ref PlayerVisualRuntimeBridgeConfig runtimeConfig,
                                           ref BufferLookup<PlayerAdditionalWeaponVisualElement> additionalWeaponsLookup,
                                           Entity entity)
    {
        string normalizedToken = string.IsNullOrWhiteSpace(resolvedToken) ? string.Empty : resolvedToken.Trim();

        switch (fieldId)
        {
            case PlayerRuntimeWeaponVisualFieldId.BaseGunReference:
                if (Encoding.UTF8.GetByteCount(normalizedToken) <= PlayerWeaponVisualSettings.MaximumReferenceSelectorUtf8Bytes)
                    runtimeConfig.BaseGunReference = new FixedString128Bytes(normalizedToken);
                return;
            case PlayerRuntimeWeaponVisualFieldId.DefaultAdditionalWeaponId:
                if (Encoding.UTF8.GetByteCount(normalizedToken) <= PlayerWeaponVisualSettings.MaximumWeaponIdUtf8Bytes)
                    runtimeConfig.DefaultAdditionalWeaponId = new FixedString64Bytes(normalizedToken);
                return;
            case PlayerRuntimeWeaponVisualFieldId.AdditionalWeaponRuntimeReference:
            case PlayerRuntimeWeaponVisualFieldId.AdditionalWeaponWeaponId:
                ApplyTokenToAdditionalWeapon(fieldId,
                                              targetEntryIndex,
                                              normalizedToken,
                                              ref additionalWeaponsLookup,
                                              entity);
                return;
        }
    }

    /// <summary>
    /// Writes one resolved token into the runtime additional-weapons buffer element at the bake-resolved array
    /// index. This keeps all rules targeting the entry valid even when one rule changes its Weapon Id.
    /// </summary>
    /// <param name="fieldId">Per-entry field identifier (runtime reference or Weapon Id).</param>
    /// <param name="targetEntryIndex">Bake-resolved target array index.</param>
    /// <param name="normalizedToken">Trimmed token result ready for capacity checks.</param>
    /// <param name="additionalWeaponsLookup">Mutable lookup used to write per-entry token results.</param>
    /// <param name="entity">Player entity owning the additional-weapons buffer.</param>
    private static void ApplyTokenToAdditionalWeapon(PlayerRuntimeWeaponVisualFieldId fieldId,
                                                      int targetEntryIndex,
                                                      string normalizedToken,
                                                      ref BufferLookup<PlayerAdditionalWeaponVisualElement> additionalWeaponsLookup,
                                                      Entity entity)
    {
        if (!additionalWeaponsLookup.HasBuffer(entity))
            return;

        DynamicBuffer<PlayerAdditionalWeaponVisualElement> buffer = additionalWeaponsLookup[entity];

        if (targetEntryIndex < 0 || targetEntryIndex >= buffer.Length)
            return;

        PlayerAdditionalWeaponVisualElement element = buffer[targetEntryIndex];

        switch (fieldId)
        {
            case PlayerRuntimeWeaponVisualFieldId.AdditionalWeaponRuntimeReference:
                if (Encoding.UTF8.GetByteCount(normalizedToken) <= PlayerWeaponVisualSettings.MaximumReferenceSelectorUtf8Bytes)
                    element.RuntimeReference = new FixedString128Bytes(normalizedToken);
                break;
            case PlayerRuntimeWeaponVisualFieldId.AdditionalWeaponWeaponId:
                if (Encoding.UTF8.GetByteCount(normalizedToken) <= PlayerWeaponVisualSettings.MaximumWeaponIdUtf8Bytes)
                    element.WeaponId = new FixedString64Bytes(normalizedToken);
                break;
        }

        buffer[targetEntryIndex] = element;
    }

    /// <summary>
    /// Restores all scalable weapon visual bridge fields from the immutable baseline before formulas are applied.
    /// </summary>
    /// <param name="baseConfig">Immutable weapon visual baseline.</param>
    /// <param name="runtimeConfig">Mutable runtime visual bridge configuration.</param>
    private static void ApplyBaseConfig(in PlayerBaseWeaponVisualConfig baseConfig,
                                        ref PlayerVisualRuntimeBridgeConfig runtimeConfig)
    {
        runtimeConfig.BaseGunReference = baseConfig.BaseGunReference;
        runtimeConfig.DefaultAdditionalWeaponId = baseConfig.DefaultAdditionalWeaponId;
    }

    /// <summary>
    /// Rebuilds the runtime additional-weapons buffer from the immutable baseline buffer. Called once per scaling
    /// hash change so the next rule pass writes into deterministic per-entry slots.
    /// </summary>
    /// <param name="entity">Player entity owning both buffers.</param>
    /// <param name="baseLookup">Read-only baseline buffer lookup.</param>
    /// <param name="runtimeLookup">Mutable runtime buffer lookup rebuilt in place.</param>
    private static void RebuildAdditionalWeaponsBuffer(Entity entity,
                                                       in BufferLookup<PlayerBaseAdditionalWeaponVisualElement> baseLookup,
                                                       ref BufferLookup<PlayerAdditionalWeaponVisualElement> runtimeLookup)
    {
        if (!baseLookup.HasBuffer(entity) || !runtimeLookup.HasBuffer(entity))
            return;

        DynamicBuffer<PlayerBaseAdditionalWeaponVisualElement> baseBuffer = baseLookup[entity];
        DynamicBuffer<PlayerAdditionalWeaponVisualElement> runtimeBuffer = runtimeLookup[entity];
        runtimeBuffer.Clear();

        for (int entryIndex = 0; entryIndex < baseBuffer.Length; entryIndex++)
        {
            PlayerBaseAdditionalWeaponVisualElement baseElement = baseBuffer[entryIndex];
            runtimeBuffer.Add(new PlayerAdditionalWeaponVisualElement
            {
                WeaponId = baseElement.WeaponId,
                RuntimeReference = baseElement.RuntimeReference,
                ShootAnimationClip = baseElement.ShootAnimationClip
            });
        }
    }
    #endregion

    #endregion
}
