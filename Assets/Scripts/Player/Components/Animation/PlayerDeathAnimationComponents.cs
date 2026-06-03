using Unity.Entities;
using Unity.Mathematics;

#region Config
/// <summary>
/// Stores the immutable death-animation tuning baked from <see cref="PlayerDeathAnimationSettings"/>. Read every frame
/// by <see cref="PlayerDeathAnimationSystem"/> while the run-outcome state is in its dying playback window. The
/// authored playback duration (<see cref="PlaybackDurationSeconds"/>) is shared with <see cref="PlayerRunOutcomeSystem"/>
/// so the run-end UI transition and this presentation never drift apart.
/// </summary>
public struct PlayerDeathAnimationConfig : IComponentData
{
    #region Master
    public byte Enabled;

    // Seconds the dying playback window keeps the damage feedbacks and this animation running before the run is
    // finalized and the end-of-run UI is shown. Consumed by PlayerRunOutcomeSystem to gate the IsFinalized transition,
    // and by PlayerDeathAnimationSystem to normalize the elapsed time into a [0..1] parametric input.
    public float PlaybackDurationSeconds;
    #endregion

    #region Camera Tween
    public byte CameraZoomEnabled;
    public float CameraTargetFovDelta;
    public byte CameraPositionLerpEnabled;
    public float CameraPositionLerpAmount;
    public float CameraCompletionNormalizedTime;
    public PlayerDeathAnimationEasing EasingMode;
    #endregion

    #region Despawn VFX
    public byte HasDespawnVfxPrefab;
    public float3 DespawnVfxSpawnOffset;
    public float DespawnVfxScaleMultiplier;
    public float DespawnVfxSpawnNormalizedTime;
    public float DespawnVfxLifetimeSeconds;
    #endregion

    #region Visual Bridge
    public byte HidePlayerVisualOnVfxSpawn;
    #endregion
}

/// <summary>
/// Holds the managed despawn VFX prefab reference next to the baked config. Kept as a managed component because the
/// runtime system instantiates the prefab directly as a one-shot GameObject (the death sequence happens once per run,
/// so the dedicated VFX-pool path used by power-up VFX is not justified here).
/// </summary>
public sealed class PlayerDeathAnimationManagedConfig : IComponentData
{
    public UnityEngine.GameObject DespawnVfxPrefab;
}

/// <summary>
/// Stores the immutable baseline death-animation config used by runtime scaling rebuilds.
/// </summary>
public struct PlayerBaseDeathAnimationConfig : IComponentData
{
    public PlayerDeathAnimationConfig Config;
}
#endregion

#region State
/// <summary>
/// Tracks the in-flight death animation so the presentation system can write the camera FOV and position deltas in a
/// feedback-safe way (previous-applied slots are removed before the new ones are layered, exactly like the camera-shake
/// utility does) and so the despawn VFX spawn-once contract is preserved across frames.
/// </summary>
public struct PlayerDeathAnimationState : IComponentData
{
    #region Activation
    // 1 once the dying playback window starts and the base camera pose has been captured.
    public byte Active;

    // 1 once the despawn VFX has been instantiated for this run; cleared by reset on a fresh dying window so the spawn
    // contract is exactly once per run.
    public byte VfxSpawned;

    // 1 once the visual bridge has been hidden for this run; cleared by reset.
    public byte VisualBridgeHidden;
    #endregion

    #region Camera Baseline
    // Camera FOV captured the first frame the animation runs. Combined with PreviousAppliedFovDelta to restore the
    // un-tweened FOV before re-layering the current frame's delta.
    public float BaseCameraFov;

    // Camera world position captured the first frame the animation runs. Combined with PreviousAppliedPositionOffset to
    // restore the un-tweened position before re-layering the current frame's offset toward the player.
    public float3 BaseCameraPosition;
    #endregion

    #region Frame Output
    // FOV delta the animation wrote this frame, isolated from the camera-shake utility's delta so the two can layer
    // additively without overwriting each other's per-frame tracking.
    public float CurrentFovDelta;

    // World-space position offset the animation wrote this frame, isolated from the camera-shake utility's offset.
    public float3 CurrentPositionOffset;

    // Mirrors carried into the previous-frame slots before recomputing this frame's output, so the runtime can subtract
    // last frame's contribution before adding the new one (matches the feedback-safe pattern used by the shake utility).
    public float PreviousAppliedFovDelta;
    public float3 PreviousAppliedPositionOffset;
    #endregion
}
#endregion
