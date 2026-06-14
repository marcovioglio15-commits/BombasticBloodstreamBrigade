using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Drives the cinematic camera zoom-in (FOV pulse plus optional camera-to-player dolly) and the optional despawn VFX
/// authored on <see cref="PlayerVisualPreset"/> while the run-outcome state is in its dying playback window. The
/// animation parametric time is <c>DyingElapsedSeconds / PlaybackDurationSeconds</c>, so the visual preset configures
/// both the SHAPE of the death moment (target FOV, dolly amount, easing, VFX spawn-time) and the duration (shared with
/// <see cref="PlayerRunOutcomeSystem"/>). The system runs in <see cref="PresentationSystemGroup"/>
/// after <see cref="PlayerCameraFollowSystem"/> so it can layer its writes on top of the camera shake output without
/// fighting the shake utility's per-frame previous-applied bookkeeping.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(PlayerCameraFollowSystem))]
public partial struct PlayerDeathAnimationSystem : ISystem
{
    #region Constants
    private const float MinimumDurationSeconds = 0.0001f;
    private const float FovEpsilon = 0.00001f;
    private const float PositionEpsilon = 0.00001f;
    private const float CompletionEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime data required by the animation pass so it never runs against incomplete baked entities.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerDeathAnimationConfig>();
        state.RequireForUpdate<PlayerDeathAnimationState>();
        state.RequireForUpdate<PlayerRunOutcomeState>();
    }

    /// <summary>
    /// Resolves the active dying window for the local player entity, updates the animation state, layers the camera
    /// position/FOV deltas and triggers the despawn VFX plus complete player presentation hide once the normalized
    /// time threshold passes.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        if (!PlayerRuntimeCameraUtility.TryResolveGameplayCamera(out Camera camera))
            return;

        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer commandBuffer = default;
        state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();
        ComponentLookup<LocalToWorld> localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        ComponentLookup<PlayerCameraShakeState> shakeStateLookup = SystemAPI.GetComponentLookup<PlayerCameraShakeState>(true);

        foreach ((RefRO<PlayerDeathAnimationConfig> deathConfig,
                  RefRW<PlayerDeathAnimationState> deathState,
                  RefRO<PlayerRunOutcomeState> runOutcomeState,
                  RefRO<LocalTransform> localTransform,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRO<PlayerDeathAnimationConfig>,
                                    RefRW<PlayerDeathAnimationState>,
                                    RefRO<PlayerRunOutcomeState>,
                                    RefRO<LocalTransform>>()
                             .WithAll<PlayerControllerConfig>()
                             .WithEntityAccess())
        {
            bool isDying = runOutcomeState.ValueRO.IsDying != 0 && runOutcomeState.ValueRO.IsFinalized == 0;

            // Idle path: not dying and never started → leave the camera untouched, no work to do.
            if (!isDying && deathState.ValueRO.Active == 0)
                continue;

            float3 playerPosition = ResolvePlayerWorldPosition(playerEntity, localTransform.ValueRO.Position, localToWorldLookup);
            ResolveShakeOutput(playerEntity, shakeStateLookup, out float3 shakePositionOffset, out float shakeFovDelta);

            ProcessAnimation(camera,
                             entityManager,
                             ref commandBuffer,
                             playerEntity,
                             in deathConfig.ValueRO,
                             ref deathState.ValueRW,
                             in runOutcomeState.ValueRO,
                             playerPosition,
                             shakePositionOffset,
                             shakeFovDelta);
        }

        if (commandBuffer.IsCreated)
        {
            commandBuffer.Playback(entityManager);
            commandBuffer.Dispose();
        }
    }
    #endregion

    #region Processing
    /// <summary>
    /// Routes one frame of the animation through the proper phase: capture the base camera pose on the first dying
    /// frame, apply the FOV/position layer while dying, hold the final pose while finalized, then clear bookkeeping
    /// without moving the camera when a fresh idle run starts.
    /// </summary>
    /// <param name="camera">Resolved gameplay camera written by both the shake utility and this system.</param>
    /// <param name="entityManager">Entity manager used for the optional managed despawn VFX and projection visibility.</param>
    /// <param name="commandBuffer">Command buffer receiving orbital projection visibility changes.</param>
    /// <param name="playerEntity">Player entity owning the dying state.</param>
    /// <param name="deathConfig">Resolved death animation config baked from the visual preset.</param>
    /// <param name="deathState">Mutable animation state for feedback-safe layering and one-shot tracking.</param>
    /// <param name="runOutcomeState">Current run outcome state used to detect dying vs. finalized vs. idle.</param>
    /// <param name="playerPosition">Resolved player world position used by the dolly lerp.</param>
    /// <param name="shakePositionOffset">Camera shake position offset for this frame, re-layered on top of the dolly.</param>
    /// <param name="shakeFovDelta">Camera shake FOV delta for this frame, re-layered on top of the zoom.</param>
    private static void ProcessAnimation(Camera camera,
                                          EntityManager entityManager,
                                          ref EntityCommandBuffer commandBuffer,
                                          Entity playerEntity,
                                          in PlayerDeathAnimationConfig deathConfig,
                                          ref PlayerDeathAnimationState deathState,
                                          in PlayerRunOutcomeState runOutcomeState,
                                          float3 playerPosition,
                                          float3 shakePositionOffset,
                                          float shakeFovDelta)
    {
        // Carry the previous frame's applied deltas before recomputing so feedback-safe layering subtracts last frame's
        // own contribution before re-applying this frame's: exact same pattern used by the camera-shake utility.
        deathState.PreviousAppliedFovDelta = deathState.CurrentFovDelta;
        deathState.PreviousAppliedPositionOffset = deathState.CurrentPositionOffset;

        bool isDying = runOutcomeState.IsDying != 0 && runOutcomeState.IsFinalized == 0;

        // Finalized defeats must keep the last death camera pose until the next run replaces it.
        if (!isDying)
        {
            if (runOutcomeState.IsFinalized != 0)
                return;

            ResetAnimationStateForFreshIdle(entityManager,
                                            ref commandBuffer,
                                            playerEntity,
                                            ref deathState);
            return;
        }

        // Disabled animations skip camera, bridge hide and VFX completely.
        if (deathConfig.Enabled == 0)
        {
            ResetAnimationStateForFreshIdle(entityManager,
                                            ref commandBuffer,
                                            playerEntity,
                                            ref deathState);
            return;
        }

        // First dying frame: latch the base camera pose so subsequent frames can lerp from a stable reference. The baseline is
        // captured with the current shake offset subtracted so it reflects the un-shaken un-tweened pose: lerping toward
        // playerPosition then becomes a clean absolute lerp without a hidden shake bias built into the starting point.
        if (deathState.Active == 0)
        {
            CaptureCameraBaseline(camera, ref deathState, shakePositionOffset, shakeFovDelta);
            deathState.Active = 1;
            deathState.VfxSpawned = 0;
            deathState.VisualBridgeHidden = 0;
        }

        float playbackDuration = math.max(MinimumDurationSeconds, deathConfig.PlaybackDurationSeconds);
        float normalizedTime = math.saturate(runOutcomeState.DyingElapsedSeconds / playbackDuration);
        float cameraNormalizedTime = ResolveCameraNormalizedTime(normalizedTime, deathConfig.CameraCompletionNormalizedTime);
        float easedCameraTime = ResolveEasedTime(cameraNormalizedTime, deathConfig.EasingMode);

        ApplyCameraLayering(camera, in deathConfig, ref deathState, easedCameraTime, playerPosition, shakePositionOffset, shakeFovDelta);
        TrySpawnDespawnVfx(entityManager,
                           ref commandBuffer,
                           playerEntity,
                           in deathConfig,
                           ref deathState,
                           normalizedTime,
                           playerPosition);
    }

    /// <summary>
    /// Captures the un-shaken un-tweened camera pose to use as the baseline for the dying-window absolute lerp. The
    /// shake offset/FOV delta resolved this frame is subtracted so the lerp toward the player position is clean and a
    /// lerp amount of 1 actually reaches playerPosition (instead of playerPosition + lingering shake bias). Called
    /// exactly once per active run so a re-spawn after the next run start gets a fresh capture.
    /// </summary>
    /// <param name="camera">Resolved gameplay camera.</param>
    /// <param name="deathState">Mutable animation state receiving the baseline.</param>
    /// <param name="shakePositionOffset">Camera shake position offset to subtract from the captured baseline.</param>
    /// <param name="shakeFovDelta">Camera shake FOV delta to subtract from the captured baseline.</param>
    private static void CaptureCameraBaseline(Camera camera,
                                               ref PlayerDeathAnimationState deathState,
                                               float3 shakePositionOffset,
                                               float shakeFovDelta)
    {
        deathState.BaseCameraFov = camera.fieldOfView - shakeFovDelta;
        Vector3 cameraPosition = camera.transform.position;
        float3 capturedPosition = new float3(cameraPosition.x, cameraPosition.y, cameraPosition.z) - shakePositionOffset;
        deathState.BaseCameraPosition = capturedPosition;
        deathState.CurrentFovDelta = 0f;
        deathState.CurrentPositionOffset = float3.zero;
        deathState.PreviousAppliedFovDelta = 0f;
        deathState.PreviousAppliedPositionOffset = float3.zero;
    }

    /// <summary>
    /// Writes the camera FOV and world position for the current frame as an absolute override: the death animation owns
    /// the camera pose during dying so a lerp amount of 1 at the end of the window actually reaches the player position
    /// instead of just nudging the camera by a per-frame delta on top of the follow system. The shake offset/FOV delta
    /// (already resolved this frame by the camera shake utility) are re-layered on top so the lethal hit's final beat
    /// keeps shaking the death pose. PreviousAppliedFovDelta / PreviousAppliedPositionOffset stay tracked so the camera
    /// pose can be held cleanly while the finalized run keeps the death camera frame.
    /// </summary>
    /// <param name="camera">Resolved gameplay camera.</param>
    /// <param name="deathConfig">Resolved death animation config.</param>
    /// <param name="deathState">Mutable animation state holding the baseline and previous-applied deltas.</param>
    /// <param name="easedTime">Eased animation parametric time in the [0..1] range.</param>
    /// <param name="playerPosition">Resolved player world position used by the dolly lerp.</param>
    /// <param name="shakePositionOffset">Camera shake position offset for this frame, re-layered on top of the dolly.</param>
    /// <param name="shakeFovDelta">Camera shake FOV delta for this frame, re-layered on top of the zoom.</param>
    private static void ApplyCameraLayering(Camera camera,
                                             in PlayerDeathAnimationConfig deathConfig,
                                             ref PlayerDeathAnimationState deathState,
                                             float easedTime,
                                             float3 playerPosition,
                                             float3 shakePositionOffset,
                                             float shakeFovDelta)
    {
        // FOV: absolute target = baseFov + zoom delta; re-layer the shake on top so the lethal-hit pulse stays felt.
        float deathFovDelta = deathConfig.CameraZoomEnabled != 0 ? deathConfig.CameraTargetFovDelta * easedTime : 0f;
        float targetFov = deathState.BaseCameraFov + deathFovDelta + shakeFovDelta;
        bool fovActive = math.abs(deathFovDelta) > FovEpsilon || math.abs(deathState.PreviousAppliedFovDelta) > FovEpsilon;

        if (fovActive)
            camera.fieldOfView = math.max(MinimumDurationSeconds, targetFov);

        // The "previous-applied" slot tracks just the death animation's own contribution so the next frame can be
        // layered without dragging the shake's contribution along.
        deathState.CurrentFovDelta = deathFovDelta;

        // Position: absolute lerp from the captured baseline toward the live player position. At amount=1 and the end of
        // the window the camera lands exactly on the player. The shake offset is re-layered on top of the lerp result.
        float3 targetPosition = deathState.BaseCameraPosition;
        bool positionLerpActive = deathConfig.CameraPositionLerpEnabled != 0;
        float3 deathPositionDelta = float3.zero;

        if (positionLerpActive)
        {
            float lerpFactor = math.saturate(math.saturate(deathConfig.CameraPositionLerpAmount) * easedTime);
            targetPosition = math.lerp(deathState.BaseCameraPosition, playerPosition, lerpFactor);
            deathPositionDelta = targetPosition - deathState.BaseCameraPosition;
        }

        bool positionActive = math.lengthsq(deathPositionDelta) > PositionEpsilon || math.lengthsq(deathState.PreviousAppliedPositionOffset) > PositionEpsilon;

        if (positionActive)
        {
            float3 finalPosition = targetPosition + shakePositionOffset;
            camera.transform.position = new Vector3(finalPosition.x, finalPosition.y, finalPosition.z);
        }

        deathState.CurrentPositionOffset = deathPositionDelta;
    }

    /// <summary>
    /// Clears death-animation bookkeeping once a fresh idle run starts and restores a managed visual bridge hidden by
    /// the VFX handoff. The camera transform is intentionally left untouched so finalized defeats hold the last death
    /// pose until the next run owns the camera again.
    /// </summary>
    /// <param name="entityManager">Entity manager used to restore player-owned orbital projection rendering.</param>
    /// <param name="commandBuffer">Command buffer receiving orbital projection visibility restoration.</param>
    /// <param name="playerEntity">Player entity whose visual presentation may need to be shown again.</param>
    /// <param name="deathState">Mutable animation state cleared for the next run.</param>
    private static void ResetAnimationStateForFreshIdle(EntityManager entityManager,
                                                        ref EntityCommandBuffer commandBuffer,
                                                        Entity playerEntity,
                                                        ref PlayerDeathAnimationState deathState)
    {
        if (deathState.Active == 0)
        {
            if (deathState.VisualBridgeHidden != 0)
                RestoreVisualPresentationForEntity(entityManager,
                                                   ref commandBuffer,
                                                   playerEntity,
                                                   ref deathState);

            deathState.CurrentFovDelta = 0f;
            deathState.CurrentPositionOffset = float3.zero;
            deathState.PreviousAppliedFovDelta = 0f;
            deathState.PreviousAppliedPositionOffset = float3.zero;
            return;
        }

        deathState.Active = 0;
        deathState.VfxSpawned = 0;

        if (deathState.VisualBridgeHidden != 0)
            RestoreVisualPresentationForEntity(entityManager,
                                               ref commandBuffer,
                                               playerEntity,
                                               ref deathState);

        deathState.CurrentFovDelta = 0f;
        deathState.CurrentPositionOffset = float3.zero;
        deathState.PreviousAppliedFovDelta = 0f;
        deathState.PreviousAppliedPositionOffset = float3.zero;
    }

    /// <summary>
    /// Re-enables the runtime visual bridge, player-owned orbital projections, and every player-attached VFX/beam the
    /// death animation hid. Used when the run-outcome state returns to idle without finalizing so a respawned player
    /// keeps its visual presentation intact.
    /// </summary>
    /// <param name="entityManager">Entity manager used to restore player-owned orbital projection rendering.</param>
    /// <param name="commandBuffer">Command buffer receiving orbital projection visibility restoration.</param>
    /// <param name="playerEntity">Player entity whose visual presentation should be restored.</param>
    /// <param name="deathState">Mutable animation state whose visual-bridge-hidden flag must be cleared.</param>
    private static void RestoreVisualPresentationForEntity(EntityManager entityManager,
                                                           ref EntityCommandBuffer commandBuffer,
                                                           Entity playerEntity,
                                                           ref PlayerDeathAnimationState deathState)
    {
        PlayerManagedVisualAnimatorBridgeSystem.TryShowRuntimeBridgeInstance(playerEntity);
        PlayerPowerUpManagedVfxRuntimeUtility.ShowPlayerAttachedInstances(playerEntity);
        PlayerOrbitalProjectionDeathVisibilityRuntimeUtility.SetPlayerOwnedRenderingHidden(entityManager,
                                                                                           ref commandBuffer,
                                                                                           playerEntity,
                                                                                           false);
        deathState.VisualBridgeHidden = 0;
    }
    #endregion

    #region Despawn VFX
    /// <summary>
    /// Spawns the optional despawn VFX once the normalized animation time reaches the configured threshold, and hides
    /// the runtime visual bridge on the same frame when the preset asks for it. Both operations are guarded by one-shot
    /// flags so they only fire once per run even though the system runs every frame.
    /// </summary>
    /// <param name="entityManager">Entity manager used for the optional managed despawn VFX and projection visibility.</param>
    /// <param name="commandBuffer">Command buffer receiving orbital projection visibility suppression.</param>
    /// <param name="playerEntity">Player entity owning the death animation state.</param>
    /// <param name="deathConfig">Resolved death animation config providing the spawn threshold and VFX tuning.</param>
    /// <param name="deathState">Mutable animation state tracking the one-shot spawn and bridge-hide flags.</param>
    /// <param name="normalizedTime">Animation parametric time in the [0..1] range.</param>
    /// <param name="playerPosition">Resolved player world position used as the spawn anchor.</param>
    private static void TrySpawnDespawnVfx(EntityManager entityManager,
                                           ref EntityCommandBuffer commandBuffer,
                                           Entity playerEntity,
                                           in PlayerDeathAnimationConfig deathConfig,
                                           ref PlayerDeathAnimationState deathState,
                                           float normalizedTime,
                                           float3 playerPosition)
    {
        if (deathState.VfxSpawned != 0)
            return;

        if (normalizedTime < deathConfig.DespawnVfxSpawnNormalizedTime)
            return;

        // Visual bridge hide can happen even when no VFX is authored, as long as the preset asks for it. Treat it as a
        // sibling one-shot so designers can hide the player rig at the spawn-time threshold regardless of VFX presence.
        // Same frame also hides every player-attached VFX (Charge Shot, Level-Up, Health/Shield Increase, Muzzle Flash
        // follow-pose, Elemental Trail attached, Laser Beam managed visual), player-owned orbital projection hierarchy,
        // and aiming pointer so the despawn effect plays against a clean stage instead of a halo around the invisible
        // rig. This suppression is gated by VisualBridgeHidden so it fires once per run even when no managed bridge
        // instance exists (e.g. Animator companion mode): the toggle is what matters, not whether the bridge call
        // succeeded.
        if (deathConfig.HidePlayerVisualOnVfxSpawn != 0 && deathState.VisualBridgeHidden == 0)
        {
            PlayerManagedVisualAnimatorBridgeSystem.TryHideRuntimeBridgeInstance(playerEntity);
            PlayerPowerUpManagedVfxRuntimeUtility.HidePlayerAttachedInstances(playerEntity);
            PlayerLaserBeamPresentationSystem.TryHideManagedInstance(playerEntity);
            PlayerOrbitalProjectionDeathVisibilityRuntimeUtility.SetPlayerOwnedRenderingHidden(entityManager,
                                                                                               ref commandBuffer,
                                                                                               playerEntity,
                                                                                               true);
            deathState.VisualBridgeHidden = 1;
        }

        if (deathConfig.HasDespawnVfxPrefab == 0)
        {
            // No VFX authored: mark spawned so the threshold check stops firing for the rest of the run.
            deathState.VfxSpawned = 1;
            return;
        }

        if (!entityManager.HasComponent<PlayerDeathAnimationManagedConfig>(playerEntity))
        {
            deathState.VfxSpawned = 1;
            return;
        }

        PlayerDeathAnimationManagedConfig managedConfig = entityManager.GetComponentObject<PlayerDeathAnimationManagedConfig>(playerEntity);

        if (managedConfig == null || managedConfig.DespawnVfxPrefab == null)
        {
            deathState.VfxSpawned = 1;
            return;
        }

        Vector3 spawnPosition = new Vector3(playerPosition.x + deathConfig.DespawnVfxSpawnOffset.x,
                                            playerPosition.y + deathConfig.DespawnVfxSpawnOffset.y,
                                            playerPosition.z + deathConfig.DespawnVfxSpawnOffset.z);
        GameObject spawnedInstance = Object.Instantiate(managedConfig.DespawnVfxPrefab, spawnPosition, Quaternion.identity);

        if (spawnedInstance != null)
        {
            float scale = math.max(0f, deathConfig.DespawnVfxScaleMultiplier);

            if (math.abs(scale - 1f) > FovEpsilon)
                spawnedInstance.transform.localScale = spawnedInstance.transform.localScale * scale;

            float lifetime = math.max(0f, deathConfig.DespawnVfxLifetimeSeconds);
            PrepareDespawnVfxForUnscaledPlayback(spawnedInstance, lifetime);
        }

        deathState.VfxSpawned = 1;
    }

    /// <summary>
    /// Converts common VFX playback components to unscaled time and installs an unscaled lifetime driver so the despawn
    /// effect remains visible while the run-outcome freeze pins <see cref="Time.timeScale"/> to zero.
    /// </summary>
    /// <param name="spawnedInstance">Runtime VFX instance spawned for the death animation.</param>
    /// <param name="lifetimeSeconds">Unscaled lifetime before the instance is destroyed.</param>
    private static void PrepareDespawnVfxForUnscaledPlayback(GameObject spawnedInstance, float lifetimeSeconds)
    {
        if (spawnedInstance == null)
            return;

        ParticleSystem[] particleSystems = spawnedInstance.GetComponentsInChildren<ParticleSystem>(true);

        for (int particleSystemIndex = 0; particleSystemIndex < particleSystems.Length; particleSystemIndex++)
        {
            ParticleSystem particleSystem = particleSystems[particleSystemIndex];

            if (particleSystem == null)
                continue;

            ParticleSystem.MainModule mainModule = particleSystem.main;
            mainModule.useUnscaledTime = true;
            particleSystem.Play(true);
        }

        Animator[] animators = spawnedInstance.GetComponentsInChildren<Animator>(true);

        for (int animatorIndex = 0; animatorIndex < animators.Length; animatorIndex++)
        {
            Animator animator = animators[animatorIndex];

            if (animator == null)
                continue;

            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        if (lifetimeSeconds <= 0f)
        {
            Object.Destroy(spawnedInstance);
            return;
        }

        PlayerDeathAnimationUnscaledVfxLifetime.Attach(spawnedInstance, lifetimeSeconds);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Reads the current frame's camera shake position offset and FOV delta resolved by the camera shake utility so the
    /// death animation can subtract them from the captured baseline and re-layer them on top of the absolute dolly/zoom.
    /// Returns zeroed output when the player has no shake state component (the shake feedback was simply not authored).
    /// </summary>
    /// <param name="playerEntity">Player entity whose shake state should be read.</param>
    /// <param name="shakeStateLookup">Read-only lookup into the PlayerCameraShakeState component.</param>
    /// <param name="shakePositionOffset">Resolved shake position offset for this frame, or float3.zero when absent.</param>
    /// <param name="shakeFovDelta">Resolved shake FOV delta for this frame, or 0 when absent.</param>
    private static void ResolveShakeOutput(Entity playerEntity,
                                            ComponentLookup<PlayerCameraShakeState> shakeStateLookup,
                                            out float3 shakePositionOffset,
                                            out float shakeFovDelta)
    {
        if (!shakeStateLookup.HasComponent(playerEntity))
        {
            shakePositionOffset = float3.zero;
            shakeFovDelta = 0f;
            return;
        }

        PlayerCameraShakeState shakeState = shakeStateLookup[playerEntity];
        shakePositionOffset = shakeState.PositionOffset;
        shakeFovDelta = shakeState.FovDelta;
    }

    /// <summary>
    /// Resolves the player world position from the cached <see cref="LocalToWorld"/> matrix when available, falling
    /// back to the local transform position otherwise. The local-to-world path is preferred so the dolly lerp follows
    /// the rendered transform exactly instead of a one-frame-stale local position.
    /// </summary>
    /// <param name="playerEntity">Player entity whose world position is needed.</param>
    /// <param name="fallbackPosition">Local transform position used when no LocalToWorld is available.</param>
    /// <param name="localToWorldLookup">Read-only lookup into the LocalToWorld component.</param>
    /// <returns>Resolved player world position.</returns>
    private static float3 ResolvePlayerWorldPosition(Entity playerEntity,
                                                      float3 fallbackPosition,
                                                      ComponentLookup<LocalToWorld> localToWorldLookup)
    {
        if (localToWorldLookup.HasComponent(playerEntity))
            return localToWorldLookup[playerEntity].Position;

        return fallbackPosition;
    }

    /// <summary>
    /// Maps the linear animation parametric time through the selected easing curve. Linear keeps the input as-is, Smooth
    /// uses smoothstep, EaseIn squares the input (slow start, fast finish) and EaseOut mirrors EaseIn for a fast start
    /// and slow finish.
    /// </summary>
    /// <param name="normalizedTime">Linear animation parametric time in [0..1].</param>
    /// <param name="easingMode">Selected easing curve.</param>
    /// <returns>Eased parametric time in [0..1].</returns>
    private static float ResolveEasedTime(float normalizedTime, PlayerDeathAnimationEasing easingMode)
    {
        float clamped = math.saturate(normalizedTime);

        switch (easingMode)
        {
            case PlayerDeathAnimationEasing.Smooth:
                return math.smoothstep(0f, 1f, clamped);
            case PlayerDeathAnimationEasing.EaseIn:
                return clamped * clamped;
            case PlayerDeathAnimationEasing.EaseOut:
                float inverse = 1f - clamped;
                return 1f - inverse * inverse;
            default:
                return clamped;
        }
    }

    /// <summary>
    /// Converts full payback normalized time into camera-only normalized time using the authored completion point.
    /// Values below or equal to zero complete the camera move on the lethal frame; values below one make the camera
    /// hold its final zoom and dolly for the remaining payback time.
    /// </summary>
    /// <param name="normalizedTime">Full payback normalized time in [0..1].</param>
    /// <param name="cameraCompletionNormalizedTime">Authored payback fraction where camera motion should be complete.</param>
    /// <returns>Camera tween normalized time in [0..1].</returns>
    private static float ResolveCameraNormalizedTime(float normalizedTime, float cameraCompletionNormalizedTime)
    {
        float completion = math.saturate(cameraCompletionNormalizedTime);

        if (completion <= CompletionEpsilon)
            return 1f;

        return math.saturate(normalizedTime / completion);
    }
    #endregion

    #endregion
}
