using System;
using UnityEngine;

#region Enums
/// <summary>
/// Easing applied to the death-animation parametric time (elapsed/duration) before driving the camera FOV pulse and the
/// camera-to-player position lerp. Linear keeps the animation strictly proportional, Smooth produces a smoothstep curve,
/// EaseIn starts slow and accelerates toward the end, EaseOut starts fast and decelerates toward the end.
/// </summary>
[Serializable]
public enum PlayerDeathAnimationEasing : byte
{
    Linear = 0,
    Smooth = 1,
    EaseIn = 2,
    EaseOut = 3
}
#endregion

#region Settings
/// <summary>
/// Stores the customizable camera zoom-in animation, the playback duration of the whole dying window and the optional
/// despawn VFX played while the run-outcome state is in its dying playback window. The animation runs in parallel with
/// the damage feedbacks (camera shake, flash, vignette, rumble) so the lethal hit reads as one cinematic beat before
/// the end-of-run screen appears. Authored fields are fully scalable end-to-end via the Add Scaling pipeline and the
/// runtime rebuild path; numeric ranges stay defensively clamped at point of use by runtime systems.
/// </summary>
[Serializable]
public sealed class PlayerDeathAnimationSettings
{
    #region Fields

    #region Serialized Fields - Master
    [Tooltip("Master toggle for the death camera animation. When disabled the whole payback playback is skipped, the camera is left untouched, the despawn VFX is not spawned and the end-of-run UI appears immediately.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Seconds the run keeps playing the damage feedbacks (camera shake, flash, vignette, rumble) and this death animation after the lethal hit before the end-of-run UI is shown. Ignored when Enabled is off. 0 collapses the playback to one frame and shows the end UI immediately.")]
    [SerializeField] private float playbackDurationSeconds = 1f;
    #endregion

    #region Serialized Fields - Camera Tween
    [Tooltip("When enabled, the camera field of view interpolates toward base FOV plus Target FOV Delta over the dying window. Negative deltas zoom IN, positive deltas zoom OUT.")]
    [SerializeField] private bool cameraZoomEnabled = true;

    [Tooltip("Peak field-of-view delta in degrees added to the base FOV at the end of the dying window. Negative values zoom IN (FOV decreases), positive values zoom OUT (FOV increases). The final death camera pose is held until the next run takes camera ownership.")]
    [SerializeField] private float cameraTargetFovDelta = -10f;

    [Tooltip("When enabled, the camera world position slides toward the player position over the dying window, blending between its captured base position and the player position by Camera Position Lerp.")]
    [SerializeField] private bool cameraPositionLerpEnabled = true;

    [Tooltip("How far the camera world position slides toward the player at the end of the dying window. 0 keeps the captured camera position, 1 fully snaps the camera onto the player. Use small values (~0.2-0.5) for a subtle dolly toward the death pose.")]
    [Range(0f, 1f)]
    [SerializeField] private float cameraPositionLerpAmount = 0.4f;

    [Tooltip("Normalized payback time at which the camera zoom and dolly should have reached their final value. 1 uses the full Payback Duration; 0 completes the camera move immediately on the lethal frame and holds it for the rest of the payback.")]
    [Range(0f, 1f)]
    [SerializeField] private float cameraCompletionNormalizedTime = 1f;

    [Tooltip("Easing applied to the animation parametric time before driving the FOV pulse and the position lerp. Linear is strictly proportional, Smooth uses smoothstep, EaseIn starts slow and accelerates, EaseOut starts fast and decelerates.")]
    [SerializeField] private PlayerDeathAnimationEasing easingMode = PlayerDeathAnimationEasing.Smooth;
    #endregion

    #region Serialized Fields - Despawn VFX
    [Tooltip("Optional one-shot VFX prefab spawned on the player while the death animation plays. Leave empty to skip the despawn VFX entirely.")]
    [SerializeField] private GameObject despawnVfxPrefab;

    [Tooltip("Local-space offset applied to the despawn VFX instance relative to the player entity position at spawn time.")]
    [SerializeField] private Vector3 despawnVfxSpawnOffset = Vector3.zero;

    [Tooltip("Uniform scale multiplier applied to the despawn VFX instance. Use values above 1 to make the burst more prominent than the player rig.")]
    [SerializeField] private float despawnVfxScaleMultiplier = 1f;

    [Tooltip("Normalized animation time (0 = start of the dying window, 1 = end) at which the despawn VFX is spawned. Use 0 to spawn the VFX immediately on the lethal hit, larger values to delay the burst.")]
    [Range(0f, 1f)]
    [SerializeField] private float despawnVfxSpawnNormalizedTime = 0.6f;

    [Tooltip("Unscaled lifetime in seconds after which the spawned VFX instance is destroyed. Use small values for a brief burst, larger values for sustained effects that continue while gameplay time is frozen.")]
    [SerializeField] private float despawnVfxLifetimeSeconds = 1.5f;
    #endregion

    #region Serialized Fields - Visual Bridge
    [Tooltip("When enabled, the runtime visual bridge GameObject (the visible player rig) is hidden the first frame the despawn VFX spawns so the VFX visually replaces the player. The bridge is restored when a new run starts.")]
    [SerializeField] private bool hidePlayerVisualOnVfxSpawn = true;
    #endregion

    #endregion

    #region Properties
    public bool Enabled
    {
        get
        {
            return enabled;
        }
    }

    public float PlaybackDurationSeconds
    {
        get
        {
            return playbackDurationSeconds;
        }
    }

    public bool CameraZoomEnabled
    {
        get
        {
            return cameraZoomEnabled;
        }
    }

    public float CameraTargetFovDelta
    {
        get
        {
            return cameraTargetFovDelta;
        }
    }

    public bool CameraPositionLerpEnabled
    {
        get
        {
            return cameraPositionLerpEnabled;
        }
    }

    public float CameraPositionLerpAmount
    {
        get
        {
            return cameraPositionLerpAmount;
        }
    }

    public PlayerDeathAnimationEasing EasingMode
    {
        get
        {
            return easingMode;
        }
    }

    public float CameraCompletionNormalizedTime
    {
        get
        {
            return cameraCompletionNormalizedTime;
        }
    }

    public GameObject DespawnVfxPrefab
    {
        get
        {
            return despawnVfxPrefab;
        }
    }

    public Vector3 DespawnVfxSpawnOffset
    {
        get
        {
            return despawnVfxSpawnOffset;
        }
    }

    public float DespawnVfxScaleMultiplier
    {
        get
        {
            return despawnVfxScaleMultiplier;
        }
    }

    public float DespawnVfxSpawnNormalizedTime
    {
        get
        {
            return despawnVfxSpawnNormalizedTime;
        }
    }

    public float DespawnVfxLifetimeSeconds
    {
        get
        {
            return despawnVfxLifetimeSeconds;
        }
    }

    public bool HidePlayerVisualOnVfxSpawn
    {
        get
        {
            return hidePlayerVisualOnVfxSpawn;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Keeps this settings block aligned with the non-destructive validation policy. Authored range issues are surfaced
    /// as management-tool warnings while runtime systems clamp their local copies before use.
    /// </summary>
    public void Validate()
    {
        // Intentionally no-op: warnings report invalid authored values without mutating preset data.
    }
    #endregion

    #endregion
}
#endregion
