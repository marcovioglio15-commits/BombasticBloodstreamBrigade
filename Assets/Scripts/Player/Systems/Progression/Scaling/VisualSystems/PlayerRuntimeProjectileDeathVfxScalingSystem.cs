using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Rebuilds projectile-death VFX settings only when the unified runtime scaling hash changes.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerRuntimeScalingSyncSystem))]
public partial struct PlayerRuntimeProjectileDeathVfxScalingSystem : ISystem
{
    #region Constants
    private const float MinimumScale = 0.01f;
    private const float MinimumLifetimeSeconds = 0.05f;
    #endregion

    #region Fields
    private static readonly Dictionary<string, PlayerFormulaValue> VariableContext = new Dictionary<string, PlayerFormulaValue>(64, System.StringComparer.OrdinalIgnoreCase);
    private static readonly List<PlayerScalableStatElement> EffectiveScalableStats = new List<PlayerScalableStatElement>(64);
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime data required to rebuild scalable projectile-death VFX settings.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerRuntimeScalingState>();
        state.RequireForUpdate<PlayerProjectileDeathVfxScalingState>();
        state.RequireForUpdate<PlayerBaseProjectileDeathVfxConfig>();
        state.RequireForUpdate<PlayerProjectileDeathVfxConfig>();
        state.RequireForUpdate<PlayerRuntimeProjectileDeathVfxScalingElement>();
    }

    /// <summary>
    /// Rebuilds projectile-death VFX runtime settings when the shared scalable-stat hash changes.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        BufferLookup<PlayerScalableStatElement> scalableStatsLookup = SystemAPI.GetBufferLookup<PlayerScalableStatElement>(true);
        BufferLookup<PlayerRoomRewardTemporaryModifierElement> temporaryModifiersLookup = SystemAPI.GetBufferLookup<PlayerRoomRewardTemporaryModifierElement>(true);
        ComponentLookup<PlayerRoomRewardTemporaryState> temporaryStateLookup = SystemAPI.GetComponentLookup<PlayerRoomRewardTemporaryState>(true);
        BufferLookup<PlayerRuntimeProjectileDeathVfxScalingElement> scalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeProjectileDeathVfxScalingElement>(true);
        ComponentLookup<PlayerRuntimeComboCounterConfig> comboConfigLookup = SystemAPI.GetComponentLookup<PlayerRuntimeComboCounterConfig>(true);
        ComponentLookup<PlayerComboCounterState> comboStateLookup = SystemAPI.GetComponentLookup<PlayerComboCounterState>(true);
        BufferLookup<PlayerRuntimeComboRankElement> comboRanksLookup = SystemAPI.GetBufferLookup<PlayerRuntimeComboRankElement>(true);
        BufferLookup<PlayerPowerUpCharacterTuningFormulaElement> characterTuningLookup = SystemAPI.GetBufferLookup<PlayerPowerUpCharacterTuningFormulaElement>(true);

        foreach ((RefRO<PlayerRuntimeScalingState> runtimeScalingState,
                  RefRW<PlayerProjectileDeathVfxScalingState> vfxScalingState,
                  RefRO<PlayerBaseProjectileDeathVfxConfig> baseConfig,
                  RefRW<PlayerProjectileDeathVfxConfig> runtimeConfig,
                  Entity entity)
                 in SystemAPI.Query<RefRO<PlayerRuntimeScalingState>,
                                    RefRW<PlayerProjectileDeathVfxScalingState>,
                                    RefRO<PlayerBaseProjectileDeathVfxConfig>,
                                    RefRW<PlayerProjectileDeathVfxConfig>>()
                             .WithAll<PlayerRuntimeProjectileDeathVfxScalingElement>()
                             .WithEntityAccess())
        {
            if (runtimeScalingState.ValueRO.Initialized == 0)
                continue;

            if (vfxScalingState.ValueRO.Initialized != 0 &&
                vfxScalingState.ValueRO.LastScalableStatsHash == runtimeScalingState.ValueRO.LastScalableStatsHash)
                continue;

            runtimeConfig.ValueRW = baseConfig.ValueRO.Config;
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
            ApplyScaling(scalingLookup[entity], ref runtimeConfig.ValueRW);
            Sanitize(ref runtimeConfig.ValueRW);
            vfxScalingState.ValueRW.Initialized = 1;
            vfxScalingState.ValueRW.LastScalableStatsHash = runtimeScalingState.ValueRO.LastScalableStatsHash;
        }
    }
    #endregion

    #region Scaling
    /// <summary>
    /// Applies all projectile-death VFX formulas to a freshly restored runtime config.
    /// </summary>
    /// <param name="scalingBuffer">Runtime scaling metadata.</param>
    /// <param name="runtimeConfig">Mutable runtime projectile-death VFX config.</param>
    private static void ApplyScaling(DynamicBuffer<PlayerRuntimeProjectileDeathVfxScalingElement> scalingBuffer,
                                     ref PlayerProjectileDeathVfxConfig runtimeConfig)
    {
        for (int scalingIndex = 0; scalingIndex < scalingBuffer.Length; scalingIndex++)
        {
            PlayerRuntimeProjectileDeathVfxScalingElement scalingElement = scalingBuffer[scalingIndex];

            if ((PlayerFormulaValueType)scalingElement.ValueType == PlayerFormulaValueType.Boolean)
            {
                if (PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateBooleanValue(scalingElement.Formula.ToString(),
                                                                                          scalingElement.BaseBooleanValue != 0,
                                                                                          VariableContext,
                                                                                          out bool resolvedBoolean))
                    ApplyBooleanValue(scalingElement.PayloadPath.ToString(), resolvedBoolean, ref runtimeConfig);

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

            ApplyNumericValue(scalingElement.PayloadPath.ToString(), resolvedValue, ref runtimeConfig);
        }
    }

    /// <summary>
    /// Applies one boolean formula result to an event-enabled field.
    /// </summary>
    /// <param name="payloadPath">Target field path relative to projectileDeathVfx.</param>
    /// <param name="resolvedValue">Resolved boolean result.</param>
    /// <param name="runtimeConfig">Mutable runtime config.</param>
    private static void ApplyBooleanValue(string payloadPath,
                                          bool resolvedValue,
                                          ref PlayerProjectileDeathVfxConfig runtimeConfig)
    {
        byte value = resolvedValue ? (byte)1 : (byte)0;

        switch (payloadPath)
        {
            case "rangeOrLifetime.enabled":
                runtimeConfig.RangeOrLifetime.Enabled = value;
                break;
            case "terminalWallHit.enabled":
                runtimeConfig.TerminalWallHit.Enabled = value;
                break;
        }
    }

    /// <summary>
    /// Applies one numeric formula result to projectile-death VFX offset, scale, or lifetime.
    /// </summary>
    /// <param name="payloadPath">Target field path relative to projectileDeathVfx.</param>
    /// <param name="resolvedValue">Resolved numeric result.</param>
    /// <param name="runtimeConfig">Mutable runtime config.</param>
    private static void ApplyNumericValue(string payloadPath,
                                          float resolvedValue,
                                          ref PlayerProjectileDeathVfxConfig runtimeConfig)
    {
        switch (payloadPath)
        {
            case "rangeOrLifetime.spawnOffset.x":
                runtimeConfig.RangeOrLifetime.SpawnOffset.x = resolvedValue;
                break;
            case "rangeOrLifetime.spawnOffset.y":
                runtimeConfig.RangeOrLifetime.SpawnOffset.y = resolvedValue;
                break;
            case "rangeOrLifetime.spawnOffset.z":
                runtimeConfig.RangeOrLifetime.SpawnOffset.z = resolvedValue;
                break;
            case "rangeOrLifetime.scaleMultiplier":
                runtimeConfig.RangeOrLifetime.UniformScale = resolvedValue;
                break;
            case "rangeOrLifetime.lifetimeSeconds":
                runtimeConfig.RangeOrLifetime.LifetimeSeconds = resolvedValue;
                break;
            case "terminalWallHit.spawnOffset.x":
                runtimeConfig.TerminalWallHit.SpawnOffset.x = resolvedValue;
                break;
            case "terminalWallHit.spawnOffset.y":
                runtimeConfig.TerminalWallHit.SpawnOffset.y = resolvedValue;
                break;
            case "terminalWallHit.spawnOffset.z":
                runtimeConfig.TerminalWallHit.SpawnOffset.z = resolvedValue;
                break;
            case "terminalWallHit.scaleMultiplier":
                runtimeConfig.TerminalWallHit.UniformScale = resolvedValue;
                break;
            case "terminalWallHit.lifetimeSeconds":
                runtimeConfig.TerminalWallHit.LifetimeSeconds = resolvedValue;
                break;
        }
    }

    /// <summary>
    /// Clamps runtime-only projectile-death VFX copies to safe presentation values.
    /// </summary>
    /// <param name="config">Mutable runtime config.</param>
    private static void Sanitize(ref PlayerProjectileDeathVfxConfig config)
    {
        SanitizeEvent(ref config.RangeOrLifetime);
        SanitizeEvent(ref config.TerminalWallHit);
    }

    /// <summary>
    /// Clamps one runtime-only projectile-death VFX event config.
    /// </summary>
    /// <param name="config">Mutable event config.</param>
    private static void SanitizeEvent(ref PlayerProjectileDeathVfxEventConfig config)
    {
        config.UniformScale = math.max(MinimumScale, config.UniformScale);
        config.LifetimeSeconds = math.max(MinimumLifetimeSeconds, config.LifetimeSeconds);
    }
    #endregion

    #endregion
}
