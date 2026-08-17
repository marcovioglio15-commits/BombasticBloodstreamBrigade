using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Converts serialized returning-projectile payloads into unmanaged ECS runtime configuration.
/// </summary>
public static class PlayerPowerUpReturningProjectileBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds a safe runtime config while preserving the original serialized values for editor warning workflows.
    /// </summary>
    /// <param name="payload">Serialized module payload to convert.</param>
    /// <param name="resolveDynamicPrefabEntity">Optional prefab-to-entity resolver supplied by the player baker.</param>
    /// <returns>Unmanaged runtime configuration, or default when the payload is missing.</returns>
    public static ReturningProjectilesConfig BuildConfig(PowerUpReturningProjectilesModuleData payload,
                                                         Func<GameObject, Entity> resolveDynamicPrefabEntity)
    {
        if (payload == null)
            return default;

        Entity replacementPrefabEntity = payload.ReplacementProjectilePrefab != null && resolveDynamicPrefabEntity != null
            ? resolveDynamicPrefabEntity(payload.ReplacementProjectilePrefab)
            : Entity.Null;
        return new ReturningProjectilesConfig
        {
            ReplacementProjectilePrefabEntity = replacementPrefabEntity,
            ReplacementProjectilePlanarRadius = PlayerProjectilePrefabFootprintBakeUtility.ResolvePlanarRadius(payload.ReplacementProjectilePrefab),
            KeepProjectileVfx = payload.KeepProjectileVfx ? (byte)1 : (byte)0,
            KeepMuzzleFlashVfx = payload.KeepMuzzleFlashVfx ? (byte)1 : (byte)0,
            KeepHitVfx = payload.KeepHitVfx ? (byte)1 : (byte)0,
            KeepDeathVfx = payload.KeepDeathVfx ? (byte)1 : (byte)0,
            ReturnPathMode = payload.ReturnPathMode,
            ReturnSpeedMultiplier = math.max(0.01f, payload.ReturnSpeedMultiplier),
            OutboundRangeMultiplier = math.max(0.01f, payload.OutboundRangeMultiplier),
            OutboundLifetimeMultiplier = math.max(0.01f, payload.OutboundLifetimeMultiplier),
            OutboundHitPolicy = payload.OutboundHitPolicy,
            AdditionalOutboundHits = math.max(1, payload.AdditionalOutboundHits),
            ReturnDelaySeconds = math.max(0f, payload.ReturnDelaySeconds),
            ReturnRumbleMultiplier = math.max(0f, payload.ReturnRumbleMultiplier),
            ReturnCameraShakeMultiplier = math.max(0f, payload.ReturnCameraShakeMultiplier),
            OutboundSizeMultiplier = math.max(0.01f, payload.OutboundSizeMultiplier),
            ReturnSizeMultiplier = math.max(0.01f, payload.ReturnSizeMultiplier),
            SpinDuringFlight = payload.SpinDuringFlight ? (byte)1 : (byte)0,
            SpinSpeedDegreesPerSecond = math.max(0f, payload.SpinSpeedDegreesPerSecond),
            SpinAxis = payload.SpinAxis,
            TurnaroundRotationSpeedDegreesPerSecond = math.max(0.01f, payload.TurnaroundRotationSpeedDegreesPerSecond),
            TurnaroundAxis = payload.TurnaroundAxis,
            ReturnHitPolicy = payload.ReturnHitPolicy,
            AdditionalReturnHits = math.max(1, payload.AdditionalReturnHits),
            PathSampleDistance = math.max(0.01f, payload.PathSampleDistance),
            ReturnCompletionDistance = math.max(0.01f, payload.ReturnCompletionDistance),
            AllowOtherPowerUpInteractions = payload.AllowOtherPowerUpInteractions ? (byte)1 : (byte)0,
            EnableProjectileSplitting = payload.EnableProjectileSplitting ? (byte)1 : (byte)0,
            ApplyToSplitProjectiles = payload.ApplyToSplitProjectiles ? (byte)1 : (byte)0,
            CompleteBouncesBeforeReturn = payload.CompleteBouncesBeforeReturn ? (byte)1 : (byte)0,
            CompleteOrbitalPathBeforeReturn = payload.CompleteOrbitalPathBeforeReturn ? (byte)1 : (byte)0,
            ApplyTinyMegaProjectileScaling = payload.ApplyTinyMegaProjectileScaling ? (byte)1 : (byte)0,
            ApplyToActivePowerUpProjectiles = payload.ApplyToActivePowerUpProjectiles ? (byte)1 : (byte)0,
            AllowConcurrentActiveProjectiles = payload.AllowConcurrentActiveProjectiles ? (byte)1 : (byte)0
        };
    }

    /// <summary>
    /// Stores which compatible projectile modules share the returning module's owning power-up.
    /// These provenance flags keep same-power-up compositions active even when cross-power-up interactions are disabled.
    /// </summary>
    /// <param name="config">Mutable returning-projectile config receiving co-located module provenance.</param>
    /// <param name="owningPowerUpId">Stable identifier of the power-up that owns this returning module.</param>
    /// <param name="hasProjectileSplit">Whether the owning power-up also contains Projectile Split.</param>
    /// <param name="hasBouncingProjectiles">Whether the owning power-up also contains Bouncing Projectiles.</param>
    /// <param name="hasOrbitalProjectiles">Whether the owning power-up also contains Orbital Projectiles.</param>
    public static void ApplySamePowerUpModuleProvenance(ref ReturningProjectilesConfig config,
                                                        string owningPowerUpId,
                                                        bool hasProjectileSplit,
                                                        bool hasBouncingProjectiles,
                                                        bool hasOrbitalProjectiles)
    {
        config.OwningPowerUpId = string.IsNullOrWhiteSpace(owningPowerUpId)
            ? default
            : new FixedString64Bytes(owningPowerUpId);
        config.SamePowerUpHasProjectileSplit = hasProjectileSplit ? (byte)1 : (byte)0;
        config.SamePowerUpHasBouncingProjectiles = hasBouncingProjectiles ? (byte)1 : (byte)0;
        config.SamePowerUpHasOrbitalProjectiles = hasOrbitalProjectiles ? (byte)1 : (byte)0;
    }
    #endregion

    #endregion
}
