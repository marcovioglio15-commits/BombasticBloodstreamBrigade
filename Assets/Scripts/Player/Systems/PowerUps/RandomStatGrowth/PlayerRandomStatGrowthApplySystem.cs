using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Reapplies accumulated native-stat growth after formula rebuilds and newly committed activations.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerRuntimeScalingSyncSystem))]
[UpdateAfter(typeof(PlayerPowerUpActivationSystem))]
[UpdateBefore(typeof(PlayerMovementSpeedSystem))]
[UpdateBefore(typeof(PlayerShootingIntentSystem))]
public partial struct PlayerRandomStatGrowthApplySystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers the versioned growth state required by the change-only query path.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerRandomStatGrowthState>();
        state.RequireForUpdate<PlayerRandomStatGrowthModifierElement>();
    }

    /// <summary>
    /// Applies only unapplied deltas and restores all accumulated deltas after a runtime scaling rebuild.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        BufferLookup<PlayerRandomStatGrowthModifierElement> modifiersLookup =
            SystemAPI.GetBufferLookup<PlayerRandomStatGrowthModifierElement>(false);
        ComponentLookup<PlayerRuntimeMovementConfig> movementConfigLookup =
            SystemAPI.GetComponentLookup<PlayerRuntimeMovementConfig>(false);
        ComponentLookup<PlayerRuntimeLookConfig> lookConfigLookup =
            SystemAPI.GetComponentLookup<PlayerRuntimeLookConfig>(false);
        ComponentLookup<PlayerRuntimeShootingConfig> shootingConfigLookup =
            SystemAPI.GetComponentLookup<PlayerRuntimeShootingConfig>(false);
        ComponentLookup<PlayerRuntimeHealthStatisticsConfig> healthConfigLookup =
            SystemAPI.GetComponentLookup<PlayerRuntimeHealthStatisticsConfig>(false);
        ComponentLookup<PlayerHealth> healthLookup = SystemAPI.GetComponentLookup<PlayerHealth>(false);
        ComponentLookup<PlayerShield> shieldLookup = SystemAPI.GetComponentLookup<PlayerShield>(false);
        ComponentLookup<PlayerExperienceCollection> experienceCollectionLookup =
            SystemAPI.GetComponentLookup<PlayerExperienceCollection>(false);

        foreach ((RefRO<PlayerRuntimeScalingState> scalingState,
                  RefRW<PlayerRandomStatGrowthState> growthState,
                  Entity entity)
                 in SystemAPI.Query<RefRO<PlayerRuntimeScalingState>,
                                    RefRW<PlayerRandomStatGrowthState>>().WithEntityAccess())
        {
            if (!modifiersLookup.HasBuffer(entity) ||
                !movementConfigLookup.HasComponent(entity) ||
                !lookConfigLookup.HasComponent(entity) ||
                !shootingConfigLookup.HasComponent(entity) ||
                !healthConfigLookup.HasComponent(entity) ||
                !healthLookup.HasComponent(entity) ||
                !shieldLookup.HasComponent(entity) ||
                !experienceCollectionLookup.HasComponent(entity))
            {
                continue;
            }

            bool scalingRebuilt = growthState.ValueRO.LastScalingApplyVersion != scalingState.ValueRO.ApplyVersion;

            if (!scalingRebuilt && growthState.ValueRO.LastAppliedVersion == growthState.ValueRO.Version)
                continue;

            DynamicBuffer<PlayerRandomStatGrowthModifierElement> modifiers = modifiersLookup[entity];
            PlayerRuntimeMovementConfig movementConfig = movementConfigLookup[entity];
            PlayerRuntimeLookConfig lookConfig = lookConfigLookup[entity];
            PlayerRuntimeShootingConfig shootingConfig = shootingConfigLookup[entity];
            PlayerRuntimeHealthStatisticsConfig healthConfig = healthConfigLookup[entity];
            PlayerHealth health = healthLookup[entity];
            PlayerShield shield = shieldLookup[entity];
            PlayerExperienceCollection experienceCollection = experienceCollectionLookup[entity];

            if (scalingRebuilt)
            {
                // Runtime configs returned to their formula baseline, so every permanent delta must be replayed.
                for (int modifierIndex = 0; modifierIndex < modifiers.Length; modifierIndex++)
                {
                    PlayerRandomStatGrowthModifierElement modifier = modifiers[modifierIndex];
                    modifier.AppliedIncrease = 0f;
                    modifiers[modifierIndex] = modifier;
                }
            }

            // Apply only the outstanding amount for each native statistic.
            for (int modifierIndex = 0; modifierIndex < modifiers.Length; modifierIndex++)
            {
                PlayerRandomStatGrowthModifierElement modifier = modifiers[modifierIndex];
                float pendingIncrease = math.max(0f, modifier.TotalIncrease - modifier.AppliedIncrease);

                if (pendingIncrease <= 0f)
                    continue;

                ApplyIncrease(modifier.Target,
                              pendingIncrease,
                              ref movementConfig,
                              ref lookConfig,
                              ref shootingConfig,
                              ref healthConfig,
                              ref health,
                              ref shield,
                              ref experienceCollection);
                modifier.AppliedIncrease = modifier.TotalIncrease;
                modifiers[modifierIndex] = modifier;
            }

            growthState.ValueRW.LastAppliedVersion = growthState.ValueRO.Version;
            growthState.ValueRW.LastScalingApplyVersion = scalingState.ValueRO.ApplyVersion;
            movementConfigLookup[entity] = movementConfig;
            lookConfigLookup[entity] = lookConfig;
            shootingConfigLookup[entity] = shootingConfig;
            healthConfigLookup[entity] = healthConfig;
            healthLookup[entity] = health;
            shieldLookup[entity] = shield;
            experienceCollectionLookup[entity] = experienceCollection;
        }
    }
    #endregion

    #region Application
    /// <summary>
    /// Adds one permanent delta to the matching mutable runtime configuration and dependent reserve state.
    /// </summary>
    /// <param name="target">Native player statistic receiving the delta.</param>
    /// <param name="increase">Positive unapplied amount.</param>
    /// <param name="movementConfig">Mutable movement runtime config.</param>
    /// <param name="lookConfig">Mutable look runtime config.</param>
    /// <param name="shootingConfig">Mutable shooting runtime config.</param>
    /// <param name="healthConfig">Mutable health-statistics runtime config.</param>
    /// <param name="health">Mutable health reserve.</param>
    /// <param name="shield">Mutable shield reserve.</param>
    /// <param name="experienceCollection">Mutable experience pickup settings.</param>
    private static void ApplyIncrease(PlayerRandomStatGrowthTarget target,
                                      float increase,
                                      ref PlayerRuntimeMovementConfig movementConfig,
                                      ref PlayerRuntimeLookConfig lookConfig,
                                      ref PlayerRuntimeShootingConfig shootingConfig,
                                      ref PlayerRuntimeHealthStatisticsConfig healthConfig,
                                      ref PlayerHealth health,
                                      ref PlayerShield shield,
                                      ref PlayerExperienceCollection experienceCollection)
    {
        switch (target)
        {
            case PlayerRandomStatGrowthTarget.MaximumHealth:
                float previousHealthMaximum = health.Max;
                healthConfig.MaxHealth = math.max(1f, healthConfig.MaxHealth + increase);
                health.Max = healthConfig.MaxHealth;
                health.Current = PlayerRuntimeScalingApplyUtility.ResolveAdjustedCurrentValue(health.Current,
                                                                                              previousHealthMaximum,
                                                                                              health.Max,
                                                                                              healthConfig.MaxHealthAdjustmentMode);
                return;
            case PlayerRandomStatGrowthTarget.MaximumShield:
                float previousShieldMaximum = shield.Max;
                healthConfig.MaxShield = math.max(0f, healthConfig.MaxShield + increase);
                shield.Max = healthConfig.MaxShield;
                shield.Current = PlayerRuntimeScalingApplyUtility.ResolveAdjustedCurrentValue(shield.Current,
                                                                                              previousShieldMaximum,
                                                                                              shield.Max,
                                                                                              healthConfig.MaxShieldAdjustmentMode);
                return;
            case PlayerRandomStatGrowthTarget.ExperiencePickupRadius:
                experienceCollection.PickupRadius = math.max(0f, experienceCollection.PickupRadius + increase);
                return;
            case PlayerRandomStatGrowthTarget.MovementBaseSpeed:
                movementConfig.Values.BaseSpeed = math.max(0f, movementConfig.Values.BaseSpeed + increase);
                return;
            case PlayerRandomStatGrowthTarget.MovementMaximumSpeed:
                movementConfig.Values.MaxSpeed = math.max(0f, movementConfig.Values.MaxSpeed + increase);
                return;
            case PlayerRandomStatGrowthTarget.MovementAcceleration:
                movementConfig.Values.Acceleration = math.max(0f, movementConfig.Values.Acceleration + increase);
                return;
            case PlayerRandomStatGrowthTarget.MovementDeceleration:
                movementConfig.Values.Deceleration = math.max(0f, movementConfig.Values.Deceleration + increase);
                return;
            case PlayerRandomStatGrowthTarget.LookRotationSpeed:
                lookConfig.RotationSpeed = math.max(0f, lookConfig.RotationSpeed + increase);
                return;
            case PlayerRandomStatGrowthTarget.ProjectileSpeed:
                shootingConfig.Values.ShootSpeed = math.max(0f, shootingConfig.Values.ShootSpeed + increase);
                return;
            case PlayerRandomStatGrowthTarget.RateOfFire:
                shootingConfig.Values.RateOfFire = math.max(0f, shootingConfig.Values.RateOfFire + increase);
                return;
            case PlayerRandomStatGrowthTarget.ProjectileDamage:
                shootingConfig.Values.Damage = math.max(0f, shootingConfig.Values.Damage + increase);
                return;
            case PlayerRandomStatGrowthTarget.ProjectileRange:
                shootingConfig.Values.Range = math.max(0f, shootingConfig.Values.Range + increase);
                return;
            case PlayerRandomStatGrowthTarget.ProjectileLifetime:
                shootingConfig.Values.Lifetime = math.max(0f, shootingConfig.Values.Lifetime + increase);
                return;
            case PlayerRandomStatGrowthTarget.ProjectileSizeMultiplier:
                shootingConfig.Values.ProjectileSizeMultiplier = math.max(0.01f,
                                                                          shootingConfig.Values.ProjectileSizeMultiplier + increase);
                return;
        }
    }
    #endregion

    #endregion
}
