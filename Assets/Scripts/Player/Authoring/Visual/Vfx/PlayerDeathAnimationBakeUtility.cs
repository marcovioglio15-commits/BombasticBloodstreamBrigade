using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Builds the baked death-animation config and the matching managed prefab reference from the resolved visual preset.
/// Invoked by <see cref="PlayerAuthoringBaker"/> during the visual feedback bake pass so runtime systems find ready-to-use
/// data on the player entity. Disabled configs bake a zero payback duration so defeat finalizes immediately.
/// </summary>
public static class PlayerDeathAnimationBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the death animation config from the resolved visual preset, or a defaults instance when no preset is
    /// available so the dying playback timing always lands on the player entity regardless of authoring. The defaults
    /// keep the master toggle on and the playback duration at 1 second, matching the authored default in
    /// <see cref="PlayerDeathAnimationSettings"/>.
    /// </summary>
    /// <param name="visualPreset">Resolved visual preset, already scaled when Add Scaling is enabled.</param>
    /// <param name="config">Built ECS config populated from authored or default values.</param>
    /// <param name="despawnVfxPrefab">Optional managed prefab reference paired with the config.</param>
    public static void BuildConfig(PlayerVisualPreset visualPreset,
                                    out PlayerDeathAnimationConfig config,
                                    out GameObject despawnVfxPrefab)
    {
        PlayerDeathAnimationSettings settings = visualPreset != null ? visualPreset.DeathAnimation : null;

        if (settings == null)
            settings = new PlayerDeathAnimationSettings();

        despawnVfxPrefab = settings.DespawnVfxPrefab;
        Vector3 vfxOffset = settings.DespawnVfxSpawnOffset;
        bool animationEnabled = settings.Enabled;
        float playbackDurationSeconds = animationEnabled ? math.max(0f, settings.PlaybackDurationSeconds) : 0f;
        ImpactFramePowerUpConfig impactFrameConfig = default;
        bool hasValidImpactFrame = PlayerPowerUpImpactFrameBakeUtility.TryBuildConfig(settings.ImpactFrame,
                                                                                      out impactFrameConfig);
        bool hasImpactFrame = animationEnabled && settings.ImpactFrameEnabled && hasValidImpactFrame;
        config = new PlayerDeathAnimationConfig
        {
            Enabled = animationEnabled ? (byte)1 : (byte)0,
            PlaybackDurationSeconds = playbackDurationSeconds,
            CameraZoomEnabled = settings.CameraZoomEnabled ? (byte)1 : (byte)0,
            CameraTargetFovDelta = settings.CameraTargetFovDelta,
            CameraPositionLerpEnabled = settings.CameraPositionLerpEnabled ? (byte)1 : (byte)0,
            CameraPositionLerpAmount = math.saturate(settings.CameraPositionLerpAmount),
            CameraCompletionNormalizedTime = math.saturate(settings.CameraCompletionNormalizedTime),
            EasingMode = settings.EasingMode,
            HasDespawnVfxPrefab = despawnVfxPrefab != null ? (byte)1 : (byte)0,
            DespawnVfxSpawnOffset = new float3(vfxOffset.x, vfxOffset.y, vfxOffset.z),
            DespawnVfxScaleMultiplier = math.max(0f, settings.DespawnVfxScaleMultiplier),
            DespawnVfxSpawnNormalizedTime = math.saturate(settings.DespawnVfxSpawnNormalizedTime),
            DespawnVfxLifetimeSeconds = math.max(0f, settings.DespawnVfxLifetimeSeconds),
            HidePlayerVisualOnVfxSpawn = settings.HidePlayerVisualOnVfxSpawn ? (byte)1 : (byte)0,
            ImpactFrameEnabled = hasImpactFrame ? (byte)1 : (byte)0,
            ImpactFrameBuildInStartNormalizedTime = math.saturate(settings.ImpactFrameBuildInStartNormalizedTime),
            ImpactFrameApplyNormalizedTime = math.saturate(settings.ImpactFrameApplyNormalizedTime),
            ImpactFrameEndNormalizedTime = math.saturate(settings.ImpactFrameEndNormalizedTime),
            ImpactFrame = impactFrameConfig
        };
    }

    /// <summary>
    /// Builds the immutable baseline config used by runtime Add Scaling rebuilds.
    /// </summary>
    /// <param name="visualPreset">Source visual preset used as the baseline for scalable formulas.</param>
    /// <returns>Baseline config component copied into runtime rebuilds before formula application.</returns>
    public static PlayerBaseDeathAnimationConfig BuildBaseConfig(PlayerVisualPreset visualPreset)
    {
        BuildConfig(visualPreset,
                    out PlayerDeathAnimationConfig config,
                    out GameObject unusedDespawnVfxPrefab);
        return new PlayerBaseDeathAnimationConfig
        {
            Config = config
        };
    }

    /// <summary>
    /// Builds the initial death animation state used the first time the runtime presentation system observes the player
    /// entity. All flags start cleared so the first dying frame triggers the baseline capture.
    /// </summary>
    /// <returns>Default-initialized state suitable to add alongside the config.</returns>
    public static PlayerDeathAnimationState BuildInitialState()
    {
        return new PlayerDeathAnimationState
        {
            Active = 0,
            VfxSpawned = 0,
            VisualBridgeHidden = 0,
            ImpactFrameApplied = 0,
            ImpactFrameCompleted = 0,
            BaseCameraFov = 0f,
            BaseCameraPosition = float3.zero,
            CurrentFovDelta = 0f,
            CurrentPositionOffset = float3.zero,
            PreviousAppliedFovDelta = 0f,
            PreviousAppliedPositionOffset = float3.zero
        };
    }
    #endregion

    #endregion
}
