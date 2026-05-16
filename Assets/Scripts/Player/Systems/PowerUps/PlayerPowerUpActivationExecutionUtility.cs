using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Contains execution helpers for active power-up runtime effects such as projectiles, bombs, dash and bullet time.
/// </summary>
public static class PlayerPowerUpActivationExecutionUtility
{
    #region Methods

    #region Execute
    /// <summary>
    /// Executes one active slot's primary tool and any non-primary Dash payload chained to the same activation.
    /// </summary>
    /// <param name="slotConfig">Runtime active slot configuration.</param>
    /// <param name="localTransform">Player transform used by projectiles, bombs and dash fallback direction.</param>
    /// <param name="lookState">Player look state used by projectile and dash direction resolution.</param>
    /// <param name="movementState">Player movement state used by dash direction resolution.</param>
    /// <param name="runtimeMovementConfig">Runtime movement config used by movement-relative dash direction resolution.</param>
    /// <param name="runtimeShootingConfig">Runtime shooting config used by projectile request creation.</param>
    /// <param name="appliedElementSlots">Runtime elemental slots applied to emitted projectiles.</param>
    /// <param name="passiveToolsState">Aggregated passive tool state applied to projectile-style tools.</param>
    /// <param name="muzzleLookup">Shooter muzzle lookup used to resolve projectile spawn positions.</param>
    /// <param name="transformLookup">Transform lookup used to resolve projectile spawn positions.</param>
    /// <param name="localToWorldLookup">LocalToWorld lookup used to resolve projectile spawn positions.</param>
    /// <param name="moveInput">Raw movement input used as dash direction fallback.</param>
    /// <param name="lastValidMovementDirection">Cached movement direction used as dash direction fallback.</param>
    /// <param name="playerEntity">Player entity that owns spawned requests.</param>
    /// <param name="laserBeamState">Mutable laser-beam state for triggered active beams.</param>
    /// <param name="dashState">Mutable dash state for primary or chained dash execution.</param>
    /// <param name="bulletTimeState">Mutable bullet-time state for timed slow effects.</param>
    /// <param name="bombRequests">Output bomb-spawn request buffer.</param>
    /// <param name="shootRequests">Output projectile-spawn request buffer.</param>
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
        bool executeDashAfterPrimaryTool = slotConfig.ToolKind != ActiveToolKind.Dash &&
                                           PlayerPowerUpDashActivationUtility.HasDashPayload(in slotConfig);

        switch (slotConfig.ToolKind)
        {
            case ActiveToolKind.Bomb:
                ExecuteBomb(in slotConfig, in localTransform, in lookState, playerEntity, bombRequests);
                break;
            case ActiveToolKind.Dash:
                PlayerPowerUpDashActivationUtility.ExecuteDash(in slotConfig,
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
                break;
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
                break;
            case ActiveToolKind.PortableHealthPack:
                break;
            case ActiveToolKind.PassiveToggle:
                return;
            default:
                return;
        }

        if (!executeDashAfterPrimaryTool)
            return;

        PlayerPowerUpDashActivationUtility.ExecuteDashIfConfigured(in slotConfig,
                                                                    in lookState,
                                                                    in movementState,
                                                                    in runtimeMovementConfig,
                                                                    in localTransform,
                                                                    moveInput,
                                                                    lastValidMovementDirection,
                                                                    ref dashState);
    }

    /// <summary>
    /// Executes a charged shot after a valid charge release, including charged lasers and projectile bursts.
    /// </summary>
    /// <param name="slotConfig">Runtime active slot configuration.</param>
    /// <param name="localTransform">Player transform used for projectile direction fallback.</param>
    /// <param name="lookState">Player look state used to resolve firing direction.</param>
    /// <param name="runtimeShootingConfig">Runtime shooting config used by projectile request creation.</param>
    /// <param name="appliedElementSlots">Runtime elemental slots applied to emitted projectiles.</param>
    /// <param name="passiveToolsState">Aggregated passive tool state applied to projectile-style tools.</param>
    /// <param name="playerEntity">Player entity that owns spawned requests.</param>
    /// <param name="muzzleLookup">Shooter muzzle lookup used to resolve projectile spawn positions.</param>
    /// <param name="transformLookup">Transform lookup used to resolve projectile spawn positions.</param>
    /// <param name="localToWorldLookup">LocalToWorld lookup used to resolve projectile spawn positions.</param>
    /// <param name="laserBeamState">Mutable laser-beam state for charged active beams.</param>
    /// <param name="normalizedCharge">Charge amount normalized above the required release threshold.</param>
    /// <param name="shootRequests">Output projectile-spawn request buffer.</param>
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
    /// </summary>
    /// <param name="slotConfig">Active slot that owns the charge-shot payload.</param>
    /// <param name="runtimeShootingConfig">Current shooting config used as the base projectile template source.</param>
    /// <param name="appliedElementSlots">Runtime default elemental slots used only when the charge shot has no override payload.</param>
    /// <param name="laserBeamState">Mutable Laser Beam state receiving the timed active snapshot.</param>
    /// <param name="resolvedSizeMultiplier">Charge-scaled size multiplier.</param>
    /// <param name="resolvedDamageMultiplier">Charge-scaled damage multiplier.</param>
    /// <param name="resolvedSpeedMultiplier">Charge-scaled speed multiplier.</param>
    /// <param name="resolvedRangeMultiplier">Charge-scaled range multiplier.</param>
    /// <param name="resolvedLifetimeMultiplier">Charge-scaled lifetime multiplier.</param>
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
    /// </summary>
    /// <param name="authoredMultiplier">Authored multiplier resolved from the active slot config.</param>
    /// <param name="chargeFactor">Normalized charge ratio in the 0-1 range.</param>
    /// <returns>Charge-scaled multiplier applied to the emitted projectile template.</returns>
    private static float ResolveChargeScaledMultiplier(float authoredMultiplier, float chargeFactor)
    {
        return math.lerp(1f, math.max(1f, authoredMultiplier), math.saturate(chargeFactor));
    }

    /// <summary>
    /// Queues one bomb spawn request using a single authored orientation for spawn offset, initial velocity and visual rotation.
    /// </summary>
    /// <param name="slotConfig">Runtime active slot configuration that contains Bomb payload values.</param>
    /// <param name="localTransform">Current player transform used as the spawn origin and forward fallback.</param>
    /// <param name="lookState">Current player look state used when Spawn Offset Orientation is PlayerLookDirection.</param>
    /// <param name="playerEntity">Player entity that owns the spawned bomb and VFX requests.</param>
    /// <param name="bombRequests">Mutable buffer that receives the bomb spawn request.</param>
    private static void ExecuteBomb(in PlayerPowerUpSlotConfig slotConfig,
                                    in LocalTransform localTransform,
                                    in PlayerLookState lookState,
                                    Entity playerEntity,
                                    DynamicBuffer<PlayerBombSpawnRequest> bombRequests)
    {
        float3 bombDirection = ResolveBombActivationDirection(in slotConfig.Bomb, in localTransform, in lookState);
        quaternion spawnOffsetRotation = quaternion.LookRotationSafe(bombDirection, new float3(0f, 1f, 0f));
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
    /// <summary>
    /// Resolves the planar direction used by Bomb spawn offset rotation, deployment velocity and initial bomb rotation.
    /// </summary>
    /// <param name="bombConfig">Runtime Bomb payload that selects the orientation reference.</param>
    /// <param name="localTransform">Current player transform used for PlayerForward and fallback orientation.</param>
    /// <param name="lookState">Current player look state used for PlayerLookDirection.</param>
    /// <returns>Normalized planar bomb activation direction.</returns>
    private static float3 ResolveBombActivationDirection(in BombPowerUpConfig bombConfig,
                                                         in LocalTransform localTransform,
                                                         in PlayerLookState lookState)
    {
        switch (bombConfig.SpawnOffsetOrientation)
        {
            case SpawnOffsetOrientationMode.PlayerLookDirection:
                return PlayerProjectileRequestUtility.ResolveShootDirection(in lookState, in localTransform);
            case SpawnOffsetOrientationMode.WorldForward:
                return new float3(0f, 0f, 1f);
            default:
                float3 forwardDirection = math.forward(localTransform.Rotation);
                forwardDirection.y = 0f;
                return math.normalizesafe(forwardDirection, new float3(0f, 0f, 1f));
        }
    }

    #endregion

    #endregion
}
