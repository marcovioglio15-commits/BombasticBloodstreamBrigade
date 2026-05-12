using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Contains execution helpers for active power-up runtime effects such as projectiles, bombs, dash and bullet time.
/// </summary>
public static class PlayerPowerUpActivationExecutionUtility
{
    #region Methods

    #region Execute
    public static void ExecuteTool(in PlayerPowerUpSlotConfig slotConfig,
                                   in LocalTransform localTransform,
                                   in PlayerLookState lookState,
                                   in PlayerMovementState movementState,
                                   in PlayerRuntimeMovementConfig runtimeMovementConfig,
                                   in PlayerRuntimeShootingConfig runtimeShootingConfig,
                                   DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> appliedElementSlots,
                                   in PlayerPassiveToolsState passiveToolsState,
                                   in ComponentLookup<ShooterMuzzleAnchor> muzzleLookup,
                                   in ComponentLookup<LocalTransform> transformLookup,
                                   in ComponentLookup<LocalToWorld> localToWorldLookup,
                                   float2 moveInput,
                                   float3 lastValidMovementDirection,
                                   Entity playerEntity,
                                   ref PlayerLaserBeamState laserBeamState,
                                   ref PlayerDashState dashState,
                                   ref PlayerBulletTimeState bulletTimeState,
                                   DynamicBuffer<PlayerBombSpawnRequest> bombRequests,
                                   DynamicBuffer<ShootRequest> shootRequests)
    {
        switch (slotConfig.ToolKind)
        {
            case ActiveToolKind.Bomb:
                ExecuteBomb(in slotConfig, in localTransform, in lookState, in movementState, playerEntity, bombRequests);
                return;
            case ActiveToolKind.Dash:
                ExecuteDash(in slotConfig,
                            in lookState,
                            in movementState,
                            in runtimeMovementConfig,
                            in localTransform,
                            moveInput,
                            lastValidMovementDirection,
                            ref dashState);
                return;
            case ActiveToolKind.BulletTime:
                ExecuteBulletTime(in slotConfig, ref bulletTimeState);
                return;
            case ActiveToolKind.Shotgun:
                ExecuteShotgun(in slotConfig,
                               in localTransform,
                               in lookState,
                               in runtimeShootingConfig,
                               appliedElementSlots,
                               in passiveToolsState,
                               playerEntity,
                               in muzzleLookup,
                               in transformLookup,
                               in localToWorldLookup,
                               ref laserBeamState,
                               shootRequests);
                return;
            case ActiveToolKind.PortableHealthPack:
                return;
            case ActiveToolKind.PassiveToggle:
                return;
        }
    }

    public static void ExecuteChargeShot(in PlayerPowerUpSlotConfig slotConfig,
                                         in LocalTransform localTransform,
                                         in PlayerLookState lookState,
                                         in PlayerRuntimeShootingConfig runtimeShootingConfig,
                                         DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> appliedElementSlots,
                                         in PlayerPassiveToolsState passiveToolsState,
                                         Entity playerEntity,
                                         in ComponentLookup<ShooterMuzzleAnchor> muzzleLookup,
                                         in ComponentLookup<LocalTransform> transformLookup,
                                         in ComponentLookup<LocalToWorld> localToWorldLookup,
                                         ref PlayerLaserBeamState laserBeamState,
                                         float normalizedCharge,
                                         DynamicBuffer<ShootRequest> shootRequests)
    {
        float chargeFactor = math.saturate(normalizedCharge);
        float resolvedSizeMultiplier = ResolveChargeScaledMultiplier(slotConfig.ChargeShot.SizeMultiplier, chargeFactor);
        float resolvedDamageMultiplier = ResolveChargeScaledMultiplier(slotConfig.ChargeShot.DamageMultiplier, chargeFactor);
        float resolvedSpeedMultiplier = ResolveChargeScaledMultiplier(slotConfig.ChargeShot.SpeedMultiplier, chargeFactor);
        float resolvedRangeMultiplier = ResolveChargeScaledMultiplier(slotConfig.ChargeShot.RangeMultiplier, chargeFactor);
        float resolvedLifetimeMultiplier = ResolveChargeScaledMultiplier(slotConfig.ChargeShot.LifetimeMultiplier, chargeFactor);

        if (slotConfig.ChargeShot.UseChargedLaserBeam != 0)
        {
            ExecuteIndependentChargedLaser(in slotConfig,
                                           in runtimeShootingConfig,
                                           appliedElementSlots,
                                           ref laserBeamState,
                                           resolvedSizeMultiplier,
                                           resolvedDamageMultiplier,
                                           resolvedSpeedMultiplier,
                                           resolvedRangeMultiplier,
                                           resolvedLifetimeMultiplier);
            return;
        }

        if (TryResolveTriggeredLaserPassiveToolsState(in slotConfig,
                                                      in passiveToolsState,
                                                      out PlayerPassiveToolsState triggeredPassiveToolsState))
        {
            ResolvePenetrationSettings(in runtimeShootingConfig.Values,
                                       slotConfig.ChargeShot.PenetrationMode,
                                       slotConfig.ChargeShot.MaxPenetrations,
                                       out ProjectilePenetrationMode laserPenetrationMode,
                                       out int laserMaxPenetrations);

            PlayerProjectileRequestTemplate triggeredLaserTemplate = PlayerProjectileRequestUtility.BuildProjectileTemplate(in runtimeShootingConfig,
                                                                                                                             appliedElementSlots,
                                                                                                                             in triggeredPassiveToolsState,
                                                                                                                             resolvedSizeMultiplier,
                                                                                                                             resolvedDamageMultiplier,
                                                                                                                             resolvedSpeedMultiplier,
                                                                                                                             resolvedRangeMultiplier,
                                                                                                                             resolvedLifetimeMultiplier,
                                                                                                                             slotConfig.ChargeShot.HasElementalPayload != 0,
                                                                                                                             in slotConfig.ChargeShot.ElementalEffect,
                                                                                                                             slotConfig.ChargeShot.ElementalStacksPerHit);

            PlayerLaserBeamStateUtility.ActivateTriggeredActiveLaser(ref laserBeamState,
                                                                     slotConfig.ChargeShot.LaserDurationSeconds,
                                                                     laserPenetrationMode,
                                                                     laserMaxPenetrations,
                                                                     in triggeredLaserTemplate,
                                                                     in triggeredPassiveToolsState);
            return;
        }

        bool hasPassiveShotgunPayload = passiveToolsState.HasShotgun != 0;
        int projectileCount = hasPassiveShotgunPayload ? math.max(1, passiveToolsState.Shotgun.ProjectileCount) : 1;
        float coneAngleDegrees = hasPassiveShotgunPayload ? math.max(0f, passiveToolsState.Shotgun.ConeAngleDegrees) : 0f;
        ResolvePenetrationSettings(in runtimeShootingConfig.Values,
                                   slotConfig.ChargeShot.PenetrationMode,
                                   slotConfig.ChargeShot.MaxPenetrations,
                                   out ProjectilePenetrationMode penetrationMode,
                                   out int maxPenetrations);
        float3 shootDirection = PlayerProjectileRequestUtility.ResolveShootDirection(in lookState, in localTransform);
        float3 spawnPosition = PlayerProjectileRequestUtility.ResolveShootSpawnPosition(playerEntity,
                                                                                        in localTransform,
                                                                                        in runtimeShootingConfig,
                                                                                        in muzzleLookup,
                                                                                        in transformLookup,
                                                                                        in localToWorldLookup);
        PlayerProjectileRequestTemplate template = PlayerProjectileRequestUtility.BuildProjectileTemplate(in runtimeShootingConfig,
                                                                                                          appliedElementSlots,
                                                                                                          in passiveToolsState,
                                                                                                          resolvedSizeMultiplier,
                                                                                                          resolvedDamageMultiplier,
                                                                                                          resolvedSpeedMultiplier,
                                                                                                          resolvedRangeMultiplier,
                                                                                                          resolvedLifetimeMultiplier,
                                                                                                          slotConfig.ChargeShot.HasElementalPayload != 0,
                                                                                                          in slotConfig.ChargeShot.ElementalEffect,
                                                                                                          slotConfig.ChargeShot.ElementalStacksPerHit);

        PlayerProjectileRequestUtility.AddSpreadRequests(ref shootRequests,
                                                         projectileCount,
                                                         coneAngleDegrees,
                                                         spawnPosition,
                                                         shootDirection,
                                                         in template,
                                                         penetrationMode,
                                                         maxPenetrations,
                                                         0);
    }

    /// <summary>
    /// Fires the hold-charge-owned Laser Beam using a neutral passive snapshot so equipped passives and other power-up hooks do not leak into the shot.
    /// /params slotConfig Active slot that owns the charge-shot payload.
    /// /params runtimeShootingConfig Current shooting config used as the base projectile template source.
    /// /params appliedElementSlots Runtime default elemental slots used only when the charge shot has no override payload.
    /// /params laserBeamState Mutable Laser Beam state receiving the timed active snapshot.
    /// /params resolvedSizeMultiplier Charge-scaled size multiplier.
    /// /params resolvedDamageMultiplier Charge-scaled damage multiplier.
    /// /params resolvedSpeedMultiplier Charge-scaled speed multiplier.
    /// /params resolvedRangeMultiplier Charge-scaled range multiplier.
    /// /params resolvedLifetimeMultiplier Charge-scaled lifetime multiplier.
    /// /returns None.
    /// </summary>
    private static void ExecuteIndependentChargedLaser(in PlayerPowerUpSlotConfig slotConfig,
                                                       in PlayerRuntimeShootingConfig runtimeShootingConfig,
                                                       DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> appliedElementSlots,
                                                       ref PlayerLaserBeamState laserBeamState,
                                                       float resolvedSizeMultiplier,
                                                       float resolvedDamageMultiplier,
                                                       float resolvedSpeedMultiplier,
                                                       float resolvedRangeMultiplier,
                                                       float resolvedLifetimeMultiplier)
    {
        PlayerPassiveToolsState chargedLaserPassiveToolsState =
            PlayerPassiveToolsAggregationUtility.CreateStandaloneLaserBeamState(in slotConfig.ChargeShot.ChargedLaserBeam);

        ResolvePenetrationSettings(in runtimeShootingConfig.Values,
                                   slotConfig.ChargeShot.PenetrationMode,
                                   slotConfig.ChargeShot.MaxPenetrations,
                                   out ProjectilePenetrationMode laserPenetrationMode,
                                   out int laserMaxPenetrations);

        PlayerProjectileRequestTemplate chargedLaserTemplate = PlayerProjectileRequestUtility.BuildProjectileTemplate(in runtimeShootingConfig,
                                                                                                                       appliedElementSlots,
                                                                                                                       in chargedLaserPassiveToolsState,
                                                                                                                       resolvedSizeMultiplier,
                                                                                                                       resolvedDamageMultiplier,
                                                                                                                       resolvedSpeedMultiplier,
                                                                                                                       resolvedRangeMultiplier,
                                                                                                                       resolvedLifetimeMultiplier,
                                                                                                                       slotConfig.ChargeShot.HasElementalPayload != 0,
                                                                                                                       in slotConfig.ChargeShot.ElementalEffect,
                                                                                                                       slotConfig.ChargeShot.ElementalStacksPerHit);

        PlayerLaserBeamStateUtility.ActivateTriggeredActiveLaser(ref laserBeamState,
                                                                 slotConfig.ChargeShot.ChargedLaserDurationSeconds,
                                                                 laserPenetrationMode,
                                                                 laserMaxPenetrations,
                                                                 in chargedLaserTemplate,
                                                                 in chargedLaserPassiveToolsState);
    }

    /// <summary>
    /// Resolves one charge-scaled projectile multiplier so charge-shot projectiles and triggered lasers share the same growth curve.
    /// /params authoredMultiplier Authored multiplier resolved from the active slot config.
    /// /params chargeFactor Normalized charge ratio in the 0-1 range.
    /// /returns Charge-scaled multiplier applied to the emitted projectile template.
    /// </summary>
    private static float ResolveChargeScaledMultiplier(float authoredMultiplier, float chargeFactor)
    {
        return math.lerp(1f, math.max(1f, authoredMultiplier), math.saturate(chargeFactor));
    }

    private static void ExecuteBomb(in PlayerPowerUpSlotConfig slotConfig,
                                    in LocalTransform localTransform,
                                    in PlayerLookState lookState,
                                    in PlayerMovementState movementState,
                                    Entity playerEntity,
                                    DynamicBuffer<PlayerBombSpawnRequest> bombRequests)
    {
        float3 bombDirection = ResolveBombActivationDirection(in movementState, in localTransform);
        quaternion spawnOffsetRotation = ResolveSpawnOffsetRotation(in slotConfig.Bomb, in localTransform, in lookState);
        float3 worldSpawnOffset = math.rotate(spawnOffsetRotation, slotConfig.Bomb.SpawnOffset);
        float3 spawnPosition = localTransform.Position + worldSpawnOffset;
        float deploySpeed = math.max(0f, slotConfig.Bomb.DeploySpeed);
        float3 initialVelocity = bombDirection * deploySpeed;
        byte enableDamagePayload = slotConfig.Bomb.EnableDamagePayload;
        float radius = enableDamagePayload != 0 ? math.max(0.1f, slotConfig.Bomb.Radius) : 0f;
        float damage = enableDamagePayload != 0 ? math.max(0f, slotConfig.Bomb.Damage) : 0f;
        byte affectAll = enableDamagePayload != 0 ? slotConfig.Bomb.AffectAllEnemiesInRadius : (byte)0;
        Entity explosionVfxPrefabEntity = enableDamagePayload != 0 ? slotConfig.Bomb.ExplosionVfxPrefabEntity : Entity.Null;
        byte scaleVfxToRadius = enableDamagePayload != 0 ? slotConfig.Bomb.ScaleVfxToRadius : (byte)0;
        float vfxScaleMultiplier = enableDamagePayload != 0 ? math.max(0.01f, slotConfig.Bomb.VfxScaleMultiplier) : 1f;

        bombRequests.Add(new PlayerBombSpawnRequest
        {
            OwnerEntity = playerEntity,
            BombPrefabEntity = slotConfig.BombPrefabEntity,
            Position = spawnPosition,
            Rotation = quaternion.LookRotationSafe(bombDirection, new float3(0f, 1f, 0f)),
            Velocity = initialVelocity,
            CollisionRadius = math.max(0.01f, slotConfig.Bomb.CollisionRadius),
            BounceOnWalls = slotConfig.Bomb.BounceOnWalls,
            BounceDamping = math.clamp(slotConfig.Bomb.BounceDamping, 0f, 1f),
            LinearDampingPerSecond = math.max(0f, slotConfig.Bomb.LinearDampingPerSecond),
            FuseSeconds = math.max(0.05f, slotConfig.Bomb.FuseSeconds),
            Radius = radius,
            Damage = damage,
            AffectAllEnemiesInRadius = affectAll,
            ExplosionVfxPrefabEntity = explosionVfxPrefabEntity,
            ScaleVfxToRadius = scaleVfxToRadius,
            VfxScaleMultiplier = vfxScaleMultiplier
        });
    }

    /// <summary>
    /// Starts a fixed-distance dash using the configured direction source and speed profile.
    /// /params slotConfig Active slot that owns the dash payload.
    /// /params lookState Current player look direction state.
    /// /params movementState Current player movement state.
    /// /params runtimeMovementConfig Movement config used when resolving movement-input dash directions.
    /// /params localTransform Player transform used as fallback direction basis.
    /// /params moveInput Raw movement input used as final movement-direction fallback.
    /// /params lastValidMovementDirection Cached movement direction from previous frames.
    /// /params dashState Mutable dash runtime state receiving the started dash.
    /// /returns None.
    /// </summary>
    public static void ExecuteDash(in PlayerPowerUpSlotConfig slotConfig,
                                   in PlayerLookState lookState,
                                   in PlayerMovementState movementState,
                                   in PlayerRuntimeMovementConfig runtimeMovementConfig,
                                   in LocalTransform localTransform,
                                   float2 moveInput,
                                   float3 lastValidMovementDirection,
                                   ref PlayerDashState dashState)
    {
        if (!TryResolveDashActivationDirection(slotConfig.Dash.DirectionMode,
                                               in lookState,
                                               in movementState,
                                               in runtimeMovementConfig,
                                               in localTransform,
                                               moveInput,
                                               lastValidMovementDirection,
                                               out float3 dashDirection))
            return;

        float dashDuration = math.max(0.01f, slotConfig.Dash.Duration);
        float dashDistance = math.max(0f, slotConfig.Dash.Distance);
        float dashTransitionIn = math.clamp(math.max(0f, slotConfig.Dash.SpeedTransitionInSeconds), 0f, dashDuration);
        float dashRemainingDuration = dashDuration - dashTransitionIn;
        float dashTransitionOut = math.clamp(math.max(0f, slotConfig.Dash.SpeedTransitionOutSeconds), 0f, dashRemainingDuration);
        float dashHoldDuration = dashDuration - dashTransitionIn - dashTransitionOut;
        float dashProfileDuration = ResolveDashProfileDuration(dashTransitionIn, dashHoldDuration, dashTransitionOut);
        float dashSpeed = dashProfileDuration > 0f ? dashDistance / dashProfileDuration : 0f;

        dashState.IsDashing = 1;
        dashState.ClearVelocityAfterApply = 0;
        dashState.Direction = dashDirection;
        dashState.EntryVelocity = float3.zero;
        dashState.Speed = dashSpeed;
        dashState.Duration = dashDuration;
        dashState.Distance = dashDistance;
        dashState.ElapsedDuration = 0f;
        dashState.TransitionInDuration = dashTransitionIn;
        dashState.TransitionOutDuration = dashTransitionOut;
        dashState.WallBounceIntensity = math.clamp(slotConfig.Dash.WallBounceIntensity, 0f, 1f);
        dashState.HoldDuration = dashHoldDuration;

        if (dashTransitionIn > 0f)
        {
            dashState.Phase = 1;
            dashState.PhaseRemaining = dashTransitionIn;
        }
        else if (dashHoldDuration > 0f)
        {
            dashState.Phase = 2;
            dashState.PhaseRemaining = dashHoldDuration;
        }
        else
        {
            dashState.Phase = 3;
            dashState.PhaseRemaining = dashTransitionOut;
        }

        if (slotConfig.Dash.GrantsInvulnerability != 0)
        {
            float invulnerabilityDuration = dashDuration + math.max(0f, slotConfig.Dash.InvulnerabilityExtraTime);
            dashState.RemainingInvulnerability = invulnerabilityDuration;
        }
    }

    private static void ExecuteBulletTime(in PlayerPowerUpSlotConfig slotConfig, ref PlayerBulletTimeState bulletTimeState)
    {
        PlayerBulletTimeRuntimeUtility.ActivateTimedEffect(ref bulletTimeState,
                                                           slotConfig.BulletTime.Duration,
                                                           slotConfig.BulletTime.EnemySlowPercent,
                                                           slotConfig.BulletTime.TransitionTimeSeconds);
    }

    private static void ExecuteShotgun(in PlayerPowerUpSlotConfig slotConfig,
                                       in LocalTransform localTransform,
                                       in PlayerLookState lookState,
                                       in PlayerRuntimeShootingConfig runtimeShootingConfig,
                                       DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> appliedElementSlots,
                                       in PlayerPassiveToolsState passiveToolsState,
                                       Entity playerEntity,
                                       in ComponentLookup<ShooterMuzzleAnchor> muzzleLookup,
                                       in ComponentLookup<LocalTransform> transformLookup,
                                       in ComponentLookup<LocalToWorld> localToWorldLookup,
                                       ref PlayerLaserBeamState laserBeamState,
                                       DynamicBuffer<ShootRequest> shootRequests)
    {
        if (TryResolveTriggeredLaserPassiveToolsState(in slotConfig,
                                                      in passiveToolsState,
                                                      out PlayerPassiveToolsState triggeredPassiveToolsState))
        {
            ResolvePenetrationSettings(in runtimeShootingConfig.Values,
                                       slotConfig.Shotgun.PenetrationMode,
                                       slotConfig.Shotgun.MaxPenetrations,
                                       out ProjectilePenetrationMode laserPenetrationMode,
                                       out int laserMaxPenetrations);

            PlayerProjectileRequestTemplate triggeredLaserTemplate = PlayerProjectileRequestUtility.BuildProjectileTemplate(in runtimeShootingConfig,
                                                                                                                             appliedElementSlots,
                                                                                                                             in triggeredPassiveToolsState,
                                                                                                                             slotConfig.Shotgun.SizeMultiplier,
                                                                                                                             slotConfig.Shotgun.DamageMultiplier,
                                                                                                                             slotConfig.Shotgun.SpeedMultiplier,
                                                                                                                             slotConfig.Shotgun.RangeMultiplier,
                                                                                                                             slotConfig.Shotgun.LifetimeMultiplier,
                                                                                                                             slotConfig.Shotgun.HasElementalPayload != 0,
                                                                                                                             in slotConfig.Shotgun.ElementalEffect,
                                                                                                                             slotConfig.Shotgun.ElementalStacksPerHit);

            PlayerLaserBeamStateUtility.ActivateTriggeredActiveLaser(ref laserBeamState,
                                                                     slotConfig.Shotgun.LaserDurationSeconds,
                                                                     laserPenetrationMode,
                                                                     laserMaxPenetrations,
                                                                     in triggeredLaserTemplate,
                                                                     in triggeredPassiveToolsState);
            return;
        }

        int projectileCount = math.max(1, slotConfig.Shotgun.ProjectileCount);
        float coneAngleDegrees = math.max(0f, slotConfig.Shotgun.ConeAngleDegrees);
        ResolvePenetrationSettings(in runtimeShootingConfig.Values,
                                   slotConfig.Shotgun.PenetrationMode,
                                   slotConfig.Shotgun.MaxPenetrations,
                                   out ProjectilePenetrationMode penetrationMode,
                                   out int maxPenetrations);
        float3 shootDirection = PlayerProjectileRequestUtility.ResolveShootDirection(in lookState, in localTransform);
        float3 spawnPosition = PlayerProjectileRequestUtility.ResolveShootSpawnPosition(playerEntity,
                                                                                        in localTransform,
                                                                                        in runtimeShootingConfig,
                                                                                        in muzzleLookup,
                                                                                        in transformLookup,
                                                                                        in localToWorldLookup);
        PlayerProjectileRequestTemplate template = PlayerProjectileRequestUtility.BuildProjectileTemplate(in runtimeShootingConfig,
                                                                                                          appliedElementSlots,
                                                                                                          in passiveToolsState,
                                                                                                          slotConfig.Shotgun.SizeMultiplier,
                                                                                                          slotConfig.Shotgun.DamageMultiplier,
                                                                                                          slotConfig.Shotgun.SpeedMultiplier,
                                                                                                          slotConfig.Shotgun.RangeMultiplier,
                                                                                                          slotConfig.Shotgun.LifetimeMultiplier,
                                                                                                          slotConfig.Shotgun.HasElementalPayload != 0,
                                                                                                          in slotConfig.Shotgun.ElementalEffect,
                                                                                                          slotConfig.Shotgun.ElementalStacksPerHit);

        PlayerProjectileRequestUtility.AddSpreadRequests(ref shootRequests,
                                                         projectileCount,
                                                         coneAngleDegrees,
                                                         spawnPosition,
                                                         shootDirection,
                                                         in template,
                                                         penetrationMode,
                                                         maxPenetrations,
                                                         0);
    }

    private static bool TryResolveTriggeredLaserPassiveToolsState(in PlayerPowerUpSlotConfig slotConfig,
                                                                  in PlayerPassiveToolsState passiveToolsState,
                                                                  out PlayerPassiveToolsState triggeredPassiveToolsState)
    {
        triggeredPassiveToolsState = passiveToolsState;

        if (slotConfig.TriggeredProjectilePassiveTool.IsDefined != 0)
            PlayerPassiveToolsAggregationUtility.AccumulatePassiveTool(ref triggeredPassiveToolsState, in slotConfig.TriggeredProjectilePassiveTool);

        if (slotConfig.ToolKind == ActiveToolKind.Shotgun)
            ApplyShotgunLaneOverride(in slotConfig.Shotgun, ref triggeredPassiveToolsState);

        return triggeredPassiveToolsState.HasLaserBeam != 0;
    }

    private static void ApplyShotgunLaneOverride(in ShotgunPowerUpConfig shotgunConfig,
                                                 ref PlayerPassiveToolsState passiveToolsState)
    {
        passiveToolsState.HasShotgun = 1;
        passiveToolsState.Shotgun.ProjectileCount = math.max(1, shotgunConfig.ProjectileCount);
        passiveToolsState.Shotgun.ConeAngleDegrees = math.max(0f, shotgunConfig.ConeAngleDegrees);
        passiveToolsState.Shotgun.PenetrationMode = shotgunConfig.PenetrationMode;
        passiveToolsState.Shotgun.MaxPenetrations = math.max(0, shotgunConfig.MaxPenetrations);
    }
    #endregion

    #region Projectile Helpers
    private static void ResolvePenetrationSettings(in ShootingValuesBlob baseShootingValues,
                                                   ProjectilePenetrationMode overrideMode,
                                                   int overrideMaxPenetrations,
                                                   out ProjectilePenetrationMode resolvedMode,
                                                   out int resolvedMaxPenetrations)
    {
        PlayerProjectileRequestUtility.ResolvePenetrationSettings(in baseShootingValues,
                                                                  overrideMode,
                                                                  overrideMaxPenetrations,
                                                                  out resolvedMode,
                                                                  out resolvedMaxPenetrations);
    }
    #endregion

    #region Movement Helpers
    private static quaternion ResolveSpawnOffsetRotation(in BombPowerUpConfig bombConfig,
                                                         in LocalTransform localTransform,
                                                         in PlayerLookState lookState)
    {
        switch (bombConfig.SpawnOffsetOrientation)
        {
            case SpawnOffsetOrientationMode.PlayerLookDirection:
                float3 lookDirection = lookState.DesiredDirection;
                lookDirection.y = 0f;

                if (math.lengthsq(lookDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
                {
                    float3 normalizedLook = math.normalizesafe(lookDirection, new float3(0f, 0f, 1f));
                    return quaternion.LookRotationSafe(normalizedLook, new float3(0f, 1f, 0f));
                }

                return localTransform.Rotation;
            case SpawnOffsetOrientationMode.WorldForward:
                return quaternion.identity;
            default:
                return localTransform.Rotation;
        }
    }

    private static float3 ResolveBombActivationDirection(in PlayerMovementState movementState, in LocalTransform localTransform)
    {
        float3 movementDirection = movementState.Velocity;
        movementDirection.y = 0f;

        if (math.lengthsq(movementDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
            return math.normalizesafe(-movementDirection, new float3(0f, 0f, -1f));

        movementDirection = movementState.DesiredDirection;
        movementDirection.y = 0f;

        if (math.lengthsq(movementDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
            return math.normalizesafe(-movementDirection, new float3(0f, 0f, -1f));

        float3 backwardDirection = -math.forward(localTransform.Rotation);
        backwardDirection.y = 0f;
        return math.normalizesafe(backwardDirection, new float3(0f, 0f, -1f));
    }

    /// <summary>
    /// Resolves the dash direction requested by the slot config, including movement/look inversion modes.
    /// /params directionMode Designer-selected source and sign for dash direction.
    /// /params lookState Current player look direction state.
    /// /params movementState Current player movement state.
    /// /params runtimeMovementConfig Movement config used when resolving movement-input directions.
    /// /params localTransform Player transform used as fallback direction basis.
    /// /params moveInput Raw movement input used as final movement-direction fallback.
    /// /params lastValidMovementDirection Cached movement direction from previous frames.
    /// /params dashDirection Resolved normalized planar dash direction.
    /// /returns True when a valid direction was resolved.
    /// </summary>
    public static bool TryResolveDashActivationDirection(DashDirectionMode directionMode,
                                                         in PlayerLookState lookState,
                                                         in PlayerMovementState movementState,
                                                         in PlayerRuntimeMovementConfig runtimeMovementConfig,
                                                         in LocalTransform localTransform,
                                                         float2 moveInput,
                                                         float3 lastValidMovementDirection,
                                                         out float3 dashDirection)
    {
        switch (directionMode)
        {
            case DashDirectionMode.OppositePlayerMovement:
                if (!TryResolveMovementDashDirection(in movementState,
                                                     in runtimeMovementConfig,
                                                     in localTransform,
                                                     moveInput,
                                                     lastValidMovementDirection,
                                                     out dashDirection))
                    return false;

                dashDirection = -dashDirection;
                return true;
            case DashDirectionMode.PlayerLook:
                return TryResolveLookDashDirection(in lookState, in localTransform, out dashDirection);
            case DashDirectionMode.OppositePlayerLook:
                if (!TryResolveLookDashDirection(in lookState, in localTransform, out dashDirection))
                    return false;

                dashDirection = -dashDirection;
                return true;
            default:
                return TryResolveMovementDashDirection(in movementState,
                                                       in runtimeMovementConfig,
                                                       in localTransform,
                                                       moveInput,
                                                       lastValidMovementDirection,
                                                       out dashDirection);
        }
    }

    private static bool TryResolveMovementDashDirection(in PlayerMovementState movementState,
                                                        in PlayerRuntimeMovementConfig runtimeMovementConfig,
                                                        in LocalTransform localTransform,
                                                        float2 moveInput,
                                                        float3 lastValidMovementDirection,
                                                        out float3 dashDirection)
    {
        if (TryResolveDashDirectionFromReleaseMask(in movementState,
                                                   in runtimeMovementConfig,
                                                   in localTransform,
                                                   out dashDirection))
            return true;

        float3 desiredDirection = movementState.DesiredDirection;

        if (math.lengthsq(desiredDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
        {
            dashDirection = math.normalizesafe(desiredDirection, new float3(0f, 0f, 1f));
            return true;
        }

        float3 velocityDirection = movementState.Velocity;
        velocityDirection.y = 0f;

        if (math.lengthsq(velocityDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
        {
            dashDirection = math.normalizesafe(velocityDirection, new float3(0f, 0f, 1f));
            return true;
        }

        if (math.lengthsq(lastValidMovementDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
        {
            dashDirection = math.normalizesafe(lastValidMovementDirection, new float3(0f, 0f, 1f));
            return true;
        }

        return TryResolveDashDirectionFromInput(moveInput, in runtimeMovementConfig, in localTransform, out dashDirection);
    }

    private static bool TryResolveLookDashDirection(in PlayerLookState lookState,
                                                    in LocalTransform localTransform,
                                                    out float3 dashDirection)
    {
        float3 lookDirection = lookState.DesiredDirection;
        lookDirection.y = 0f;

        if (math.lengthsq(lookDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
        {
            dashDirection = math.normalizesafe(lookDirection, new float3(0f, 0f, 1f));
            return true;
        }

        lookDirection = lookState.CurrentDirection;
        lookDirection.y = 0f;

        if (math.lengthsq(lookDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
        {
            dashDirection = math.normalizesafe(lookDirection, new float3(0f, 0f, 1f));
            return true;
        }

        lookDirection = math.forward(localTransform.Rotation);
        lookDirection.y = 0f;
        dashDirection = math.normalizesafe(lookDirection, new float3(0f, 0f, 1f));
        return math.lengthsq(dashDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon;
    }

    private static float ResolveDashProfileDuration(float transitionInDuration,
                                                    float holdDuration,
                                                    float transitionOutDuration)
    {
        float transitionArea = (math.max(0f, transitionInDuration) + math.max(0f, transitionOutDuration)) * 0.5f;
        return math.max(0.0001f, transitionArea + math.max(0f, holdDuration));
    }

    private static bool TryResolveDashDirectionFromReleaseMask(in PlayerMovementState movementState,
                                                               in PlayerRuntimeMovementConfig runtimeMovementConfig,
                                                               in LocalTransform localTransform,
                                                               out float3 dashDirection)
    {
        byte previousMask = movementState.PrevMoveMask;
        byte currentMask = movementState.CurrMoveMask;

        if (!PlayerControllerMath.IsDiagonalMask(previousMask))
        {
            dashDirection = float3.zero;
            return false;
        }

        if (!PlayerControllerMath.IsSingleAxisMask(currentMask))
        {
            dashDirection = float3.zero;
            return false;
        }

        if (!PlayerControllerMath.IsReleaseOnly(previousMask, currentMask))
        {
            dashDirection = float3.zero;
            return false;
        }

        float2 preservedInput = PlayerControllerMath.ResolveDigitalMask(previousMask, movementState.MovePressTimes);

        return TryResolveDashDirectionFromInput(preservedInput,
                                                in runtimeMovementConfig,
                                                in localTransform,
                                                out dashDirection);
    }

    private static bool TryResolveDashDirectionFromInput(float2 input,
                                                         in PlayerRuntimeMovementConfig runtimeMovementConfig,
                                                         in LocalTransform localTransform,
                                                         out float3 dashDirection)
    {
        PlayerRuntimeMovementConfig movementConfig = runtimeMovementConfig;
        float deadZone = movementConfig.Values.InputDeadZone;

        if (math.lengthsq(input) <= deadZone * deadZone)
        {
            dashDirection = float3.zero;
            return false;
        }

        bool hasCamera = PlayerRuntimeCameraUtility.TryResolveGameplayCamera(out Camera camera);
        float3 cameraForward = hasCamera ? (float3)camera.transform.forward : new float3(0f, 0f, 1f);
        float3 playerForward = PlayerControllerMath.NormalizePlanar(math.forward(localTransform.Rotation), new float3(0f, 0f, 1f));
        PlayerControllerMath.GetReferenceBasis(movementConfig.MovementReference, playerForward, cameraForward, hasCamera, out float3 forward, out float3 right);
        float2 inputDirection = PlayerControllerMath.NormalizeSafe(input);

        if (math.lengthsq(inputDirection) <= PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
        {
            dashDirection = float3.zero;
            return false;
        }

        switch (movementConfig.DirectionsMode)
        {
            case MovementDirectionsMode.DiscreteCount:
                int count = math.max(1, movementConfig.DiscreteDirectionCount);
                float step = (math.PI * 2f) / count;
                float offset = math.radians(movementConfig.DirectionOffsetDegrees);
                float inputAngle = math.atan2(inputDirection.x, inputDirection.y);
                float snappedAngle = PlayerControllerMath.QuantizeAngle(inputAngle, step, offset);
                float3 snappedLocalDirection = PlayerControllerMath.DirectionFromAngle(snappedAngle);
                float3 snappedWorldDirection = right * snappedLocalDirection.x + forward * snappedLocalDirection.z;
                dashDirection = math.normalizesafe(snappedWorldDirection, forward);
                return math.lengthsq(dashDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon;
            default:
                float3 freeDirection = right * inputDirection.x + forward * inputDirection.y;
                dashDirection = math.normalizesafe(freeDirection, forward);
                return math.lengthsq(dashDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon;
        }
    }
    #endregion

    #endregion
}
