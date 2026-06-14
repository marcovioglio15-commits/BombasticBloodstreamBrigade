using System;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Builds runtime ECS configs for player visual-preset VFX that are independent from power-up payload assets.
/// </summary>
public static class PlayerVisualVfxBakeUtility
{
    #region Constants
    private const float DefaultLevelUpVfxLifetimeSeconds = 1f;
    private const float DefaultStatIncreaseVfxLifetimeSeconds = 1f;
    private const float DefaultChargeShotVfxLifetimeSeconds = 1f;
    private const float DefaultProjectileAttachedVfxLifetimeSeconds = 12f;
    private const float MinimumScale = 0.01f;
    private const float MinimumProjectileDeathVfxLifetimeSeconds = 0.05f;
    private const float MinimumMuzzleFlashLifetimeSeconds = 0.05f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the optional level-up VFX config from the resolved visual preset.
    /// </summary>
    /// <param name="visualPreset">Resolved visual preset, already scaled when Add Scaling is enabled.</param>
    /// <param name="resolveDynamicVfxPrefabEntity">Prefab resolver that also registers managed VFX bindings.</param>
    /// <param name="config">Built ECS config when a prefab is assigned.</param>
    /// <returns>True when the preset contains a Level-Up VFX prefab.</returns>
    public static bool TryBuildLevelUpVfxConfig(PlayerVisualPreset visualPreset,
                                                Func<GameObject, Entity> resolveDynamicVfxPrefabEntity,
                                                out PlayerLevelUpVfxConfig config)
    {
        config = default;

        if (visualPreset == null || visualPreset.LevelUpVfxPrefab == null)
            return false;

        GameObject prefab = visualPreset.LevelUpVfxPrefab;
        Entity prefabEntity = resolveDynamicVfxPrefabEntity != null ? resolveDynamicVfxPrefabEntity(prefab) : Entity.Null;
        Vector3 spawnOffset = visualPreset.LevelUpVfxSpawnOffset;

        config = new PlayerLevelUpVfxConfig
        {
            PrefabEntity = prefabEntity,
            SourcePrefab = prefab,
            SpawnOffset = new float3(spawnOffset.x, spawnOffset.y, spawnOffset.z),
            UniformScale = math.max(MinimumScale, visualPreset.LevelUpVfxScaleMultiplier),
            LifetimeSeconds = ManagedVfxPrefabLifetimeUtility.ResolvePrefabLifetimeSeconds(prefab, DefaultLevelUpVfxLifetimeSeconds),
            TriggerMode = ResolveLevelUpTriggerMode(visualPreset.LevelUpVfxTriggerMode)
        };
        return true;
    }

    /// <summary>
    /// Builds the optional muzzle-flash VFX config from the resolved visual preset. Unlike the other one-shot VFX, its lifetime is authored explicitly instead of being read from the prefab.
    /// </summary>
    /// <param name="visualPreset">Resolved visual preset, already scaled when Add Scaling is enabled.</param>
    /// <param name="resolveDynamicVfxPrefabEntity">Prefab resolver that also registers managed VFX bindings.</param>
    /// <param name="config">Built ECS config when a prefab is assigned.</param>
    /// <returns>True when the preset contains a Muzzle Flash VFX prefab.</returns>
    public static bool TryBuildMuzzleFlashVfxConfig(PlayerVisualPreset visualPreset,
                                                    Func<GameObject, Entity> resolveDynamicVfxPrefabEntity,
                                                    out PlayerMuzzleFlashVfxConfig config)
    {
        config = default;

        if (visualPreset == null || visualPreset.MuzzleFlashVfxPrefab == null)
            return false;

        GameObject prefab = visualPreset.MuzzleFlashVfxPrefab;
        Entity prefabEntity = resolveDynamicVfxPrefabEntity != null ? resolveDynamicVfxPrefabEntity(prefab) : Entity.Null;
        Vector3 spawnOffset = visualPreset.MuzzleFlashVfxSpawnOffset;

        config = new PlayerMuzzleFlashVfxConfig
        {
            PrefabEntity = prefabEntity,
            SourcePrefab = prefab,
            SpawnOffset = new float3(spawnOffset.x, spawnOffset.y, spawnOffset.z),
            UniformScale = math.max(MinimumScale, visualPreset.MuzzleFlashVfxScaleMultiplier),
            LifetimeSeconds = math.max(MinimumMuzzleFlashLifetimeSeconds, visualPreset.MuzzleFlashVfxLifetimeSeconds)
        };
        return true;
    }

    /// <summary>
    /// Builds the optional charge-shot VFX config from the resolved visual preset.
    /// </summary>
    /// <param name="visualPreset">Resolved visual preset, already scaled when Add Scaling is enabled.</param>
    /// <param name="resolveDynamicVfxPrefabEntity">Prefab resolver that also registers managed VFX bindings.</param>
    /// <param name="config">Built ECS config when a prefab is assigned.</param>
    /// <returns>True when the preset contains a Charge Shot VFX prefab.</returns>
    public static bool TryBuildChargeShotVfxConfig(PlayerVisualPreset visualPreset,
                                                   Func<GameObject, Entity> resolveDynamicVfxPrefabEntity,
                                                   out PlayerChargeShotVfxConfig config)
    {
        config = default;

        if (visualPreset == null || visualPreset.ChargeShotVfxPrefab == null)
            return false;

        GameObject prefab = visualPreset.ChargeShotVfxPrefab;
        Entity prefabEntity = resolveDynamicVfxPrefabEntity != null ? resolveDynamicVfxPrefabEntity(prefab) : Entity.Null;
        Vector3 spawnOffset = visualPreset.ChargeShotVfxSpawnOffset;

        config = new PlayerChargeShotVfxConfig
        {
            PrefabEntity = prefabEntity,
            SourcePrefab = prefab,
            SpawnOffset = new float3(spawnOffset.x, spawnOffset.y, spawnOffset.z),
            UniformScale = math.max(MinimumScale, visualPreset.ChargeShotVfxScaleMultiplier),
            LifetimeSeconds = ManagedVfxPrefabLifetimeUtility.ResolvePrefabLifetimeSeconds(prefab, DefaultChargeShotVfxLifetimeSeconds),
            PlaybackMode = ResolveChargeShotPlaybackMode(visualPreset.ChargeShotVfxPlaybackMode),
            AppliesToAllHoldChargePowerUps = visualPreset.ChargeShotVfxAppliesToAllHoldChargePowerUps ? (byte)1 : (byte)0
        };
        return true;
    }

    /// <summary>
    /// Builds the optional health-increase VFX config from the resolved visual preset.
    /// </summary>
    /// <param name="visualPreset">Resolved visual preset, already scaled when Add Scaling is enabled.</param>
    /// <param name="resolveDynamicVfxPrefabEntity">Prefab resolver that also registers managed VFX bindings.</param>
    /// <param name="config">Built ECS config when a prefab is assigned.</param>
    /// <returns>True when the preset contains a Health Increase VFX prefab.</returns>
    public static bool TryBuildHealthIncreaseVfxConfig(PlayerVisualPreset visualPreset,
                                                       Func<GameObject, Entity> resolveDynamicVfxPrefabEntity,
                                                       out PlayerHealthIncreaseVfxConfig config)
    {
        config = default;

        if (visualPreset == null || visualPreset.HealthIncreaseVfxPrefab == null)
            return false;

        GameObject prefab = visualPreset.HealthIncreaseVfxPrefab;
        Entity prefabEntity = resolveDynamicVfxPrefabEntity != null ? resolveDynamicVfxPrefabEntity(prefab) : Entity.Null;
        Vector3 spawnOffset = visualPreset.HealthIncreaseVfxSpawnOffset;

        config = new PlayerHealthIncreaseVfxConfig
        {
            PrefabEntity = prefabEntity,
            SourcePrefab = prefab,
            SpawnOffset = new float3(spawnOffset.x, spawnOffset.y, spawnOffset.z),
            UniformScale = math.max(MinimumScale, visualPreset.HealthIncreaseVfxScaleMultiplier),
            LifetimeSeconds = ManagedVfxPrefabLifetimeUtility.ResolvePrefabLifetimeSeconds(prefab, DefaultStatIncreaseVfxLifetimeSeconds),
            TriggerMode = ResolveStatIncreaseTriggerMode(visualPreset.HealthIncreaseVfxTriggerMode)
        };
        return true;
    }

    /// <summary>
    /// Builds the optional shield-increase VFX config from the resolved visual preset.
    /// </summary>
    /// <param name="visualPreset">Resolved visual preset, already scaled when Add Scaling is enabled.</param>
    /// <param name="resolveDynamicVfxPrefabEntity">Prefab resolver that also registers managed VFX bindings.</param>
    /// <param name="config">Built ECS config when a prefab is assigned.</param>
    /// <returns>True when the preset contains a Shield Increase VFX prefab.</returns>
    public static bool TryBuildShieldIncreaseVfxConfig(PlayerVisualPreset visualPreset,
                                                       Func<GameObject, Entity> resolveDynamicVfxPrefabEntity,
                                                       out PlayerShieldIncreaseVfxConfig config)
    {
        config = default;

        if (visualPreset == null || visualPreset.ShieldIncreaseVfxPrefab == null)
            return false;

        GameObject prefab = visualPreset.ShieldIncreaseVfxPrefab;
        Entity prefabEntity = resolveDynamicVfxPrefabEntity != null ? resolveDynamicVfxPrefabEntity(prefab) : Entity.Null;
        Vector3 spawnOffset = visualPreset.ShieldIncreaseVfxSpawnOffset;

        config = new PlayerShieldIncreaseVfxConfig
        {
            PrefabEntity = prefabEntity,
            SourcePrefab = prefab,
            SpawnOffset = new float3(spawnOffset.x, spawnOffset.y, spawnOffset.z),
            UniformScale = math.max(MinimumScale, visualPreset.ShieldIncreaseVfxScaleMultiplier),
            LifetimeSeconds = ManagedVfxPrefabLifetimeUtility.ResolvePrefabLifetimeSeconds(prefab, DefaultStatIncreaseVfxLifetimeSeconds),
            TriggerMode = ResolveStatIncreaseTriggerMode(visualPreset.ShieldIncreaseVfxTriggerMode)
        };
        return true;
    }

    /// <summary>
    /// Builds the optional projectile-attached VFX config from the resolved visual preset.
    /// </summary>
    /// <param name="visualPreset">Resolved visual preset, already scaled when Add Scaling is enabled.</param>
    /// <param name="resolveDynamicVfxPrefabEntity">Prefab resolver that also registers managed VFX bindings.</param>
    /// <param name="config">Built ECS config when a prefab is assigned.</param>
    /// <returns>True when the preset contains a projectile-attached VFX prefab.</returns>
    public static bool TryBuildProjectileAttachedVfxConfig(PlayerVisualPreset visualPreset,
                                                           Func<GameObject, Entity> resolveDynamicVfxPrefabEntity,
                                                           out PlayerProjectileAttachedVfxConfig config)
    {
        config = default;

        if (visualPreset == null || visualPreset.PlayerProjectileVfxPrefab == null)
            return false;

        GameObject prefab = visualPreset.PlayerProjectileVfxPrefab;
        Entity prefabEntity = resolveDynamicVfxPrefabEntity != null ? resolveDynamicVfxPrefabEntity(prefab) : Entity.Null;
        Vector3 spawnOffset = visualPreset.PlayerProjectileVfxSpawnOffset;

        config = new PlayerProjectileAttachedVfxConfig
        {
            PrefabEntity = prefabEntity,
            SourcePrefab = prefab,
            SpawnOffset = new float3(spawnOffset.x, spawnOffset.y, spawnOffset.z),
            UniformScale = math.max(MinimumScale, visualPreset.PlayerProjectileVfxScaleMultiplier),
            LifetimeSeconds = ManagedVfxPrefabLifetimeUtility.ResolvePrefabLifetimeSeconds(prefab, DefaultProjectileAttachedVfxLifetimeSeconds)
        };
        return true;
    }

    /// <summary>
    /// Builds projectile-death VFX runtime settings from the resolved visual preset.
    /// </summary>
    /// <param name="visualPreset">Resolved visual preset, already scaled when Add Scaling is enabled.</param>
    /// <param name="resolveDynamicVfxPrefabEntity">Prefab resolver that also registers managed VFX bindings.</param>
    /// <param name="config">Built ECS config when at least one projectile-death VFX prefab is assigned.</param>
    /// <returns>True when the preset contains at least one projectile-death VFX prefab.</returns>
    public static bool TryBuildProjectileDeathVfxConfig(PlayerVisualPreset visualPreset,
                                                        Func<GameObject, Entity> resolveDynamicVfxPrefabEntity,
                                                        out PlayerProjectileDeathVfxConfig config)
    {
        config = default;
        PlayerProjectileDeathVfxSettings settings = visualPreset != null ? visualPreset.ProjectileDeathVfx : null;

        if (settings == null || !settings.HasAnyPrefab)
            return false;

        config = new PlayerProjectileDeathVfxConfig
        {
            RangeOrLifetime = BuildProjectileDeathVfxEventConfig(settings.RangeOrLifetime,
                                                                 null,
                                                                 resolveDynamicVfxPrefabEntity),
            TerminalWallHit = BuildProjectileDeathVfxEventConfig(settings.TerminalWallHit,
                                                                 settings.RangeOrLifetime != null ? settings.RangeOrLifetime.VfxPrefab : null,
                                                                 resolveDynamicVfxPrefabEntity)
        };
        return true;
    }

    /// <summary>
    /// Builds the immutable projectile-death VFX baseline from the unscaled visual preset.
    /// </summary>
    /// <param name="visualPreset">Unscaled source visual preset.</param>
    /// <param name="resolveDynamicVfxPrefabEntity">Prefab resolver that also registers managed VFX bindings.</param>
    /// <returns>Baseline config used by runtime scaling rebuilds.</returns>
    public static PlayerBaseProjectileDeathVfxConfig BuildBaseProjectileDeathVfxConfig(PlayerVisualPreset visualPreset,
                                                                                        Func<GameObject, Entity> resolveDynamicVfxPrefabEntity)
    {
        TryBuildProjectileDeathVfxConfig(visualPreset,
                                         resolveDynamicVfxPrefabEntity,
                                         out PlayerProjectileDeathVfxConfig config);
        return new PlayerBaseProjectileDeathVfxConfig
        {
            Config = config
        };
    }

    /// <summary>
    /// Builds runtime visibility and movement-speed scale settings for the designer-authored Jetpack VFX inside the Visual Player.
    /// </summary>
    /// <param name="visualPreset">Resolved visual preset, already scaled when Add Scaling is enabled.</param>
    /// <returns>Runtime config containing a safe prefab-relative visual reference, activity thresholds, and scale controls.</returns>
    public static PlayerJetpackVfxConfig BuildJetpackVfxConfig(PlayerVisualPreset visualPreset)
    {
        PlayerJetpackVfxSettings settings = visualPreset != null ? visualPreset.PlayerJetpackVfx : null;

        if (settings == null)
            return default;

        return new PlayerJetpackVfxConfig
        {
            RuntimeReference = BuildJetpackRuntimeReference(settings.RuntimeReference),
            MovementSpeedThreshold = settings.MovementSpeedThreshold,
            RotationSpeedThresholdDegrees = settings.RotationSpeedThresholdDegrees,
            SpeedForMaximumScale = settings.SpeedForMaximumScale,
            NormalScaleSpeedPercent = settings.NormalScaleSpeedPercent,
            ScaleVariationPercent = settings.ScaleVariationPercent,
            ActivationMode = ResolveJetpackActivationMode(settings.ActivationMode),
            ScaleWithMovementSpeed = settings.ScaleWithMovementSpeed ? (byte)1 : (byte)0
        };
    }

    /// <summary>
    /// Builds the immutable Jetpack VFX baseline from the unscaled visual preset.
    /// </summary>
    /// <param name="visualPreset">Unscaled source visual preset.</param>
    /// <returns>Baseline config used by runtime scaling rebuilds.</returns>
    public static PlayerBaseJetpackVfxConfig BuildBaseJetpackVfxConfig(PlayerVisualPreset visualPreset)
    {
        return new PlayerBaseJetpackVfxConfig
        {
            Config = BuildJetpackVfxConfig(visualPreset)
        };
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds one runtime projectile-death VFX event from authored settings.
    /// </summary>
    /// <param name="settings">Authored event settings.</param>
    /// <param name="fallbackPrefab">Optional fallback prefab used when the event has no direct assignment.</param>
    /// <param name="resolveDynamicVfxPrefabEntity">Prefab resolver that also registers managed VFX bindings.</param>
    /// <returns>Runtime event config with safe presentation values.</returns>
    private static PlayerProjectileDeathVfxEventConfig BuildProjectileDeathVfxEventConfig(PlayerProjectileDeathVfxEventSettings settings,
                                                                                           GameObject fallbackPrefab,
                                                                                           Func<GameObject, Entity> resolveDynamicVfxPrefabEntity)
    {
        if (settings == null)
            return default;

        GameObject prefab = settings.VfxPrefab != null ? settings.VfxPrefab : fallbackPrefab;
        Entity prefabEntity = prefab != null && resolveDynamicVfxPrefabEntity != null
            ? resolveDynamicVfxPrefabEntity(prefab)
            : Entity.Null;
        Vector3 spawnOffset = settings.SpawnOffset;

        return new PlayerProjectileDeathVfxEventConfig
        {
            PrefabEntity = prefabEntity,
            SourcePrefab = prefab,
            SpawnOffset = new float3(spawnOffset.x, spawnOffset.y, spawnOffset.z),
            UniformScale = math.max(MinimumScale, settings.ScaleMultiplier),
            LifetimeSeconds = math.max(MinimumProjectileDeathVfxLifetimeSeconds, settings.LifetimeSeconds),
            Enabled = settings.Enabled ? (byte)1 : (byte)0
        };
    }

    /// <summary>
    /// Resolves invalid serialized level-up VFX trigger values to the every-level-up path.
    /// </summary>
    /// <param name="triggerMode">Authored trigger mode.</param>
    /// <returns>Runtime-supported trigger mode.</returns>
    private static PlayerLevelUpVfxTriggerMode ResolveLevelUpTriggerMode(PlayerLevelUpVfxTriggerMode triggerMode)
    {
        switch (triggerMode)
        {
            case PlayerLevelUpVfxTriggerMode.EveryLevelUp:
            case PlayerLevelUpVfxTriggerMode.MilestonePowerUpsOnly:
                return triggerMode;
            default:
                return PlayerLevelUpVfxTriggerMode.EveryLevelUp;
        }
    }

    /// <summary>
    /// Resolves invalid serialized charge-shot VFX playback values to the timed one-shot path.
    /// </summary>
    /// <param name="playbackMode">Authored playback mode.</param>
    /// <returns>Runtime-supported playback mode.</returns>
    private static PlayerChargeShotVfxPlaybackMode ResolveChargeShotPlaybackMode(PlayerChargeShotVfxPlaybackMode playbackMode)
    {
        switch (playbackMode)
        {
            case PlayerChargeShotVfxPlaybackMode.PlayOnceTimedWithChargeCompletion:
            case PlayerChargeShotVfxPlaybackMode.LoopWhileCharging:
            case PlayerChargeShotVfxPlaybackMode.StretchSinglePlaybackToCharge:
                return playbackMode;
            default:
                return PlayerChargeShotVfxPlaybackMode.PlayOnceTimedWithChargeCompletion;
        }
    }

    /// <summary>
    /// Resolves invalid serialized stat-increase VFX trigger values to the every-increase path.
    /// </summary>
    /// <param name="triggerMode">Authored trigger mode.</param>
    /// <returns>Runtime-supported trigger mode.</returns>
    private static PlayerStatIncreaseVfxTriggerMode ResolveStatIncreaseTriggerMode(PlayerStatIncreaseVfxTriggerMode triggerMode)
    {
        switch (triggerMode)
        {
            case PlayerStatIncreaseVfxTriggerMode.EveryIncrease:
            case PlayerStatIncreaseVfxTriggerMode.MaximumValueIncreaseOnly:
                return triggerMode;
            default:
                return PlayerStatIncreaseVfxTriggerMode.EveryIncrease;
        }
    }

    /// <summary>
    /// Resolves invalid serialized Jetpack activation values to movement-based activation.
    /// </summary>
    /// <param name="activationMode">Authored Jetpack VFX activation mode.</param>
    /// <returns>Runtime-supported activation mode.</returns>
    private static PlayerJetpackVfxActivationMode ResolveJetpackActivationMode(PlayerJetpackVfxActivationMode activationMode)
    {
        switch (activationMode)
        {
            case PlayerJetpackVfxActivationMode.Always:
            case PlayerJetpackVfxActivationMode.WhileMoving:
            case PlayerJetpackVfxActivationMode.WhileRotating:
            case PlayerJetpackVfxActivationMode.WhileMovingOrRotating:
                return activationMode;
            default:
                return PlayerJetpackVfxActivationMode.WhileMoving;
        }
    }

    /// <summary>
    /// Builds a safe fixed-string selector for the designer-authored Jetpack VFX object.
    /// </summary>
    /// <param name="runtimeReference">Authored prefab-relative path or unique object name.</param>
    /// <returns>Trimmed selector, or an empty value when the authored reference exceeds runtime capacity.</returns>
    private static FixedString128Bytes BuildJetpackRuntimeReference(string runtimeReference)
    {
        if (string.IsNullOrWhiteSpace(runtimeReference))
            return default;

        string normalizedReference = runtimeReference.Trim();

        if (Encoding.UTF8.GetByteCount(normalizedReference) > PlayerWeaponVisualSettings.MaximumReferenceSelectorUtf8Bytes)
            return default;

        return new FixedString128Bytes(normalizedReference);
    }
    #endregion

    #endregion
}
