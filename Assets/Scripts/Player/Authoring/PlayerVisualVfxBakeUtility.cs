using System;
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
    private const float DefaultChargeShotVfxLifetimeSeconds = 1f;
    private const float MinimumScale = 0.01f;
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
    #endregion

    #region Private Methods
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
    #endregion

    #endregion
}
