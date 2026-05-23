using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Applies progressive movement slow from active charge-shot slots after base look multipliers have been resolved.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerLookMultiplierSystem))]
[UpdateAfter(typeof(PlayerPowerUpActivationSystem))]
[UpdateBefore(typeof(PlayerMovementSpeedSystem))]
public partial struct PlayerPowerUpChargeMovementSlowSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Configures component requirements for charge movement slow application.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpsConfigElement>();
        state.RequireForUpdate<PlayerPowerUpsState>();
        state.RequireForUpdate<PlayerMovementModifiers>();
    }
    #endregion

    #region Update
    /// <summary>
    /// Multiplies movement modifiers by the strongest active charge slow configured on the player's power-up slots.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        foreach ((DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer,
                  RefRO<PlayerPowerUpsState> powerUpsState,
                  RefRW<PlayerMovementModifiers> movementModifiers) in SystemAPI.Query<DynamicBuffer<PlayerPowerUpsConfigElement>,
                                                                                       RefRO<PlayerPowerUpsState>,
                                                                                       RefRW<PlayerMovementModifiers>>())
        {
            PlayerPowerUpsConfig powerUpsConfig = PlayerPowerUpsConfigBufferUtility.Read(powerUpsConfigBuffer);
            float primarySlowPercent = ResolveSlotSlowPercent(in powerUpsConfig.PrimarySlot,
                                                              powerUpsState.ValueRO.PrimaryCharge,
                                                              powerUpsState.ValueRO.PrimaryIsCharging);
            float secondarySlowPercent = ResolveSlotSlowPercent(in powerUpsConfig.SecondarySlot,
                                                                powerUpsState.ValueRO.SecondaryCharge,
                                                                powerUpsState.ValueRO.SecondaryIsCharging);
            float slowPercent = math.max(primarySlowPercent, secondarySlowPercent);

            if (slowPercent <= 0f)
                continue;

            float movementMultiplier = math.saturate(1f - slowPercent * 0.01f);
            movementModifiers.ValueRW.MaxSpeedMultiplier *= movementMultiplier;
            movementModifiers.ValueRW.AccelerationMultiplier *= movementMultiplier;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the slow percentage contributed by one charging slot.
    /// </summary>
    /// <param name="slotConfig">Slot configuration containing charge-shot slow settings.</param>
    /// <param name="charge">Current stored charge for the inspected slot.</param>
    /// <param name="isCharging">Current charging flag for the inspected slot.</param>
    /// <returns>Slow percentage in the 0-100 range.</returns>
    private static float ResolveSlotSlowPercent(in PlayerPowerUpSlotConfig slotConfig,
                                                float charge,
                                                byte isCharging)
    {
        if (isCharging == 0)
            return 0f;

        if (slotConfig.IsDefined == 0)
            return 0f;

        if (slotConfig.ToolKind != ActiveToolKind.ChargeShot)
            return 0f;

        if (slotConfig.ChargeShot.SlowPlayerWhileCharging == 0)
            return 0f;

        float maximumCharge = math.max(slotConfig.ChargeShot.RequiredCharge, slotConfig.ChargeShot.MaximumCharge);

        if (maximumCharge <= 0f)
            return 0f;

        float maximumSlowPercent = math.clamp(slotConfig.ChargeShot.MaximumPlayerSlowPercent, 0f, 100f);

        if (maximumSlowPercent <= 0f)
            return 0f;

        float normalizedCharge = math.saturate(math.max(0f, charge) / maximumCharge);
        float curveValue = SampleNormalizedSlowCurve(in slotConfig.ChargeShot.PlayerSlowCurveSamples, normalizedCharge);
        return maximumSlowPercent * curveValue;
    }

    /// <summary>
    /// Samples the fixed normalized slow curve used by the charge-shot movement penalty.
    /// </summary>
    /// <param name="samples">Fixed normalized curve samples baked from the authoring AnimationCurve.</param>
    /// <param name="normalizedCharge">Current charge progress in the 0-1 range.</param>
    /// <returns>Normalized curve output in the 0-1 range.</returns>
    private static float SampleNormalizedSlowCurve(in FixedList128Bytes<float> samples,
                                                   float normalizedCharge)
    {
        float clampedCharge = math.saturate(normalizedCharge);
        int sampleCount = samples.Length;

        if (sampleCount <= 0)
            return clampedCharge;

        if (sampleCount == 1)
            return math.saturate(samples[0]);

        float scaledSampleIndex = clampedCharge * (sampleCount - 1);
        int lowerSampleIndex = math.clamp((int)math.floor(scaledSampleIndex), 0, sampleCount - 1);
        int upperSampleIndex = math.min(lowerSampleIndex + 1, sampleCount - 1);
        float interpolation = math.saturate(scaledSampleIndex - lowerSampleIndex);
        float lowerSample = math.saturate(samples[lowerSampleIndex]);
        float upperSample = math.saturate(samples[upperSampleIndex]);
        return math.lerp(lowerSample, upperSample, interpolation);
    }
    #endregion

    #endregion
}
