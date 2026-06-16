using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Drives upper-body shooting, charge, and release clips from ECS presentation state without giving Animator gameplay authority.
/// </summary>
public static class PlayerUpperBodyAnimationPresentationUtility
{
    #region Constants
    private const string UpperBodyLayerName = "UpperBody";
    private const string UpperBodyIdleStatePath = "UpperBody.ST_Idle";
    private const string UpperBodyActionStatePath = "UpperBody.ST_Upper_Shoot";
    private const float MinimumDurationSeconds = 0.0001f;
    private const float IdleTransitionSeconds = 0.05f;
    private static readonly int UpperBodyIdleStateHash = Animator.StringToHash(UpperBodyIdleStatePath);
    private static readonly int UpperBodyActionStateHash = Animator.StringToHash(UpperBodyActionStatePath);
    #endregion

    #region Fields
    private static readonly Dictionary<int, PlayerUpperBodyAnimatorBinding> bindingsByAnimatorId =
        new Dictionary<int, PlayerUpperBodyAnimatorBinding>(2);
#if UNITY_EDITOR
    private static readonly HashSet<int> invalidAnimatorWarnings = new HashSet<int>();
    private static readonly HashSet<int> missingClipWarnings = new HashSet<int>();
    private static readonly HashSet<int> missingIdleOverrideKeyWarnings = new HashSet<int>();
#endif
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Clears managed Animator override bindings when the owning presentation system is destroyed.
    /// </summary>
    public static void ClearCache()
    {
        bindingsByAnimatorId.Clear();
#if UNITY_EDITOR
        invalidAnimatorWarnings.Clear();
        missingClipWarnings.Clear();
        missingIdleOverrideKeyWarnings.Clear();
#endif
    }

    /// <summary>
    /// Suspends upper-body presentation while the managed Animator hierarchy is inactive. Synchronizes consumed
    /// gameplay edges so stale shots or charge releases are not replayed when the visual hierarchy becomes active.
    /// </summary>
    /// <param name="powerUpsState">Current authoritative charge state used to synchronize release-edge history.</param>
    /// <param name="shotPulseVersion">Current authoritative shot-pulse version marked as consumed.</param>
    /// <param name="runtimeState">Mutable Animator presentation state reset for a clean reactivation.</param>
    public static void SuspendForInactiveAnimator(in PlayerPowerUpsState powerUpsState,
                                                  uint shotPulseVersion,
                                                  ref PlayerAnimatorRuntimeState runtimeState)
    {
        ResetActionState(ref runtimeState);
        UpdatePreviousChargeState(in powerUpsState, ref runtimeState);
        runtimeState.PreviousShooting = 0;
        runtimeState.LastShotPulseVersion = shotPulseVersion;
        runtimeState.Initialized = 0;
    }
    #endregion

    #region Playback
    /// <summary>
    /// Advances the current upper-body action, starts charge/release/shoot actions from ECS edges, and samples the full clip.
    /// </summary>
    /// <param name="animator">Managed visual Animator receiving upper-body presentation.</param>
    /// <param name="clipConfig">Concrete animation clips baked from the active Animation Bindings preset.</param>
    /// <param name="shootingConfig">Current runtime shooting values used to fit a complete shooting clip between shots.</param>
    /// <param name="powerUpsConfig">Current active-slot payloads containing charge animation selectors.</param>
    /// <param name="powerUpsState">Current active-slot charge state and release edges.</param>
    /// <param name="passiveToolsState">Aggregated Switch Weapon selection containing the active shooting animation selector.</param>
    /// <param name="additionalWeaponVisuals">Runtime table binding defined weapon IDs to shooting clips.</param>
    /// <param name="defaultAdditionalWeaponId">Scalable default weapon ID used when no Switch Weapon owns the visual.</param>
    /// <param name="shootPulseThisFrame">True when ECS spawned at least one new player shot since the previous presentation update.</param>
    /// <param name="deltaTime">Presentation delta time used to advance sampled upper-body actions.</param>
    /// <param name="runtimeState">Mutable Animator presentation state persisted on the player entity.</param>
    /// <param name="drivesUpperBody">True when the runtime driver owns the upper-body shooting state for this Animator.</param>
    /// <returns>True while an upper-body action is active.</returns>
    public static bool Update(Animator animator,
                              in PlayerUpperBodyAnimationClipConfig clipConfig,
                              in PlayerRuntimeShootingConfig shootingConfig,
                              in PlayerPowerUpsConfig powerUpsConfig,
                              in PlayerPowerUpsState powerUpsState,
                              in PlayerPassiveToolsState passiveToolsState,
                              in DynamicBuffer<PlayerAdditionalWeaponVisualElement> additionalWeaponVisuals,
                              FixedString64Bytes defaultAdditionalWeaponId,
                              bool shootPulseThisFrame,
                              float deltaTime,
                              ref PlayerAnimatorRuntimeState runtimeState,
                              out bool drivesUpperBody)
    {
        drivesUpperBody = TryGetBinding(animator,
                                        in clipConfig,
                                        in additionalWeaponVisuals,
                                        out PlayerUpperBodyAnimatorBinding binding);

        if (!drivesUpperBody)
        {
            ResetActionState(ref runtimeState);
            UpdatePreviousChargeState(in powerUpsState, ref runtimeState);
            return false;
        }

        bool primaryReleased = runtimeState.PreviousPrimaryCharging != 0 && powerUpsState.PrimaryIsCharging == 0;
        bool secondaryReleased = runtimeState.PreviousSecondaryCharging != 0 && powerUpsState.SecondaryIsCharging == 0;
        bool hasChargingSlot = TryResolveChargingSlot(in powerUpsConfig,
                                                      in powerUpsState,
                                                      out PlayerPowerUpSlotConfig chargingSlot);
        bool keepChargeActionActive = hasChargingSlot &&
                                      chargingSlot.ChargeShot.ChargeAnimationClipSlot != PlayerChargeAnimationClipSlot.None;
        bool startedAction = false;

        AdvanceCurrentAction(binding,
                             keepChargeActionActive,
                             math.max(0f, deltaTime),
                             ref runtimeState);

        if (primaryReleased || secondaryReleased)
        {
            PlayerPowerUpSlotConfig releasedSlot = primaryReleased
                ? powerUpsConfig.PrimarySlot
                : powerUpsConfig.SecondarySlot;
            PlayerReleaseAnimationClipSlot releaseSlot = releasedSlot.ChargeShot.ReleaseAnimationClipSlot;

            if (releasedSlot.IsDefined != 0 &&
                releasedSlot.ToolKind == ActiveToolKind.ChargeShot &&
                releaseSlot != PlayerReleaseAnimationClipSlot.None)
            {
                AnimationClip releaseClip = ResolveReleaseClip(in clipConfig, releaseSlot);
                startedAction = TryStartAction(binding,
                                               PlayerUpperBodyAnimationActionKind.Release,
                                               releaseClip,
                                               ResolveNaturalDuration(releaseClip),
                                               ref runtimeState);
            }
        }

        if (!startedAction && hasChargingSlot)
        {
            PlayerChargeAnimationClipSlot chargeSlot = chargingSlot.ChargeShot.ChargeAnimationClipSlot;

            if (chargeSlot != PlayerChargeAnimationClipSlot.None)
            {
                AnimationClip chargeClip = ResolveChargeClip(in clipConfig, chargeSlot);

                if (runtimeState.UpperBodyActionKind != PlayerUpperBodyAnimationActionKind.Charge ||
                    runtimeState.UpperBodyActionActive == 0 ||
                    binding.AppliedClip != chargeClip)
                {
                    startedAction = TryStartAction(binding,
                                                   PlayerUpperBodyAnimationActionKind.Charge,
                                                   chargeClip,
                                                   float.PositiveInfinity,
                                                   ref runtimeState);
                }
            }
        }

        if (!startedAction &&
            !hasChargingSlot &&
            shootPulseThisFrame &&
            PrepareShootPulse(binding, ref runtimeState))
        {
            AnimationClip shootClip = ResolveShootClip(in clipConfig,
                                                       in passiveToolsState,
                                                       in additionalWeaponVisuals,
                                                       defaultAdditionalWeaponId);
            startedAction = TryStartAction(binding,
                                           PlayerUpperBodyAnimationActionKind.Shoot,
                                           shootClip,
                                           ResolveShootDuration(shootClip, shootingConfig.Values.RateOfFire),
                                           ref runtimeState);
        }

        DriveCurrentAction(binding, ref runtimeState);
        UpdatePreviousChargeState(in powerUpsState, ref runtimeState);
        return runtimeState.UpperBodyActionActive != 0;
    }

    /// <summary>
    /// Advances an active action and returns to upper-body idle only after its complete normalized playback.
    /// </summary>
    /// <param name="binding">Cached Animator override binding.</param>
    /// <param name="keepChargeActionActive">True while a hold-charge slot with an enabled animation remains active.</param>
    /// <param name="deltaTime">Non-negative presentation delta time.</param>
    /// <param name="runtimeState">Mutable upper-body runtime state.</param>
    private static void AdvanceCurrentAction(PlayerUpperBodyAnimatorBinding binding,
                                             bool keepChargeActionActive,
                                             float deltaTime,
                                             ref PlayerAnimatorRuntimeState runtimeState)
    {
        if (runtimeState.UpperBodyActionActive == 0)
            return;

        if (runtimeState.UpperBodyActionKind == PlayerUpperBodyAnimationActionKind.Charge)
        {
            if (keepChargeActionActive)
            {
                runtimeState.UpperBodyActionElapsed += deltaTime;
                return;
            }

            StopAction(binding.AnimatorComponent, binding.UpperBodyLayerIndex, ref runtimeState);
            return;
        }

        runtimeState.UpperBodyActionElapsed += deltaTime;

        if (runtimeState.UpperBodyActionElapsed + MinimumDurationSeconds < runtimeState.UpperBodyActionDuration)
            return;

        runtimeState.UpperBodyActionElapsed = runtimeState.UpperBodyActionDuration;
        DriveCurrentAction(binding, ref runtimeState);
        StopAction(binding.AnimatorComponent, binding.UpperBodyLayerIndex, ref runtimeState);
    }

    /// <summary>
    /// Completes the previous shooting cycle at the authoritative next-shot boundary without inserting an idle frame.
    /// </summary>
    /// <param name="binding">Cached Animator override binding.</param>
    /// <param name="runtimeState">Mutable upper-body runtime state completed before the next shooting action.</param>
    /// <returns>True when no charge or release action blocks the incoming shooting pulse.</returns>
    private static bool PrepareShootPulse(PlayerUpperBodyAnimatorBinding binding,
                                          ref PlayerAnimatorRuntimeState runtimeState)
    {
        if (runtimeState.UpperBodyActionActive == 0)
            return true;

        if (runtimeState.UpperBodyActionKind != PlayerUpperBodyAnimationActionKind.Shoot)
            return false;

        runtimeState.UpperBodyActionElapsed = runtimeState.UpperBodyActionDuration;
        DriveCurrentAction(binding, ref runtimeState);
        ResetActionState(ref runtimeState);
        return true;
    }

    /// <summary>
    /// Starts one upper-body action only when its optional concrete clip is available.
    /// </summary>
    /// <param name="binding">Cached Animator override binding.</param>
    /// <param name="actionKind">Action category used for edge and duration handling.</param>
    /// <param name="clip">Concrete clip selected by the payload selector.</param>
    /// <param name="duration">Requested presentation duration; infinity keeps charge playback active until release.</param>
    /// <param name="runtimeState">Mutable upper-body runtime state.</param>
    /// <returns>True when the action started.</returns>
    private static bool TryStartAction(PlayerUpperBodyAnimatorBinding binding,
                                       PlayerUpperBodyAnimationActionKind actionKind,
                                       AnimationClip clip,
                                       float duration,
                                       ref PlayerAnimatorRuntimeState runtimeState)
    {
        if (!CanDriveAnimator(binding))
            return false;

        if (clip == null)
        {
            LogMissingClip(binding, actionKind);
            return false;
        }

        ApplyOverride(binding, clip);
        runtimeState.UpperBodyActionKind = actionKind;
        runtimeState.UpperBodyActionActive = 1;
        runtimeState.UpperBodyActionElapsed = 0f;
        runtimeState.UpperBodyActionDuration = duration;
        binding.AnimatorComponent.Play(UpperBodyActionStateHash, binding.UpperBodyLayerIndex, 0f);
        return true;
    }

    /// <summary>
    /// Samples the active upper-body state explicitly so each action reaches its intended normalized playback time.
    /// </summary>
    /// <param name="binding">Cached Animator override binding.</param>
    /// <param name="runtimeState">Current upper-body runtime state.</param>
    private static void DriveCurrentAction(PlayerUpperBodyAnimatorBinding binding,
                                           ref PlayerAnimatorRuntimeState runtimeState)
    {
        if (!CanDriveAnimator(binding) || runtimeState.UpperBodyActionActive == 0 || binding.AppliedClip == null)
            return;

        float clipLength = math.max(MinimumDurationSeconds, binding.AppliedClip.length);
        float normalizedTime;

        if (runtimeState.UpperBodyActionKind == PlayerUpperBodyAnimationActionKind.Charge)
        {
            float unboundedNormalizedTime = runtimeState.UpperBodyActionElapsed / clipLength;
            normalizedTime = binding.AppliedClip.isLooping
                ? unboundedNormalizedTime
                : math.min(1f, unboundedNormalizedTime);
        }
        else
        {
            normalizedTime = math.saturate(runtimeState.UpperBodyActionElapsed /
                                           math.max(MinimumDurationSeconds, runtimeState.UpperBodyActionDuration));
        }

        binding.AnimatorComponent.Play(UpperBodyActionStateHash, binding.UpperBodyLayerIndex, normalizedTime);
    }

    /// <summary>
    /// Ends one upper-body action and blends back to the authored upper-body idle state.
    /// </summary>
    /// <param name="animator">Animator returning to upper-body idle.</param>
    /// <param name="upperBodyLayerIndex">Resolved upper-body layer index.</param>
    /// <param name="runtimeState">Mutable upper-body runtime state cleared after completion.</param>
    private static void StopAction(Animator animator,
                                   int upperBodyLayerIndex,
                                   ref PlayerAnimatorRuntimeState runtimeState)
    {
        ResetActionState(ref runtimeState);

        if (animator != null && animator.isActiveAndEnabled)
            animator.CrossFadeInFixedTime(UpperBodyIdleStateHash, IdleTransitionSeconds, upperBodyLayerIndex);
    }

    /// <summary>
    /// Clears upper-body action timing while preserving unrelated Animator runtime state.
    /// </summary>
    /// <param name="runtimeState">Mutable Animator runtime state receiving neutral action values.</param>
    private static void ResetActionState(ref PlayerAnimatorRuntimeState runtimeState)
    {
        runtimeState.UpperBodyActionKind = PlayerUpperBodyAnimationActionKind.None;
        runtimeState.UpperBodyActionActive = 0;
        runtimeState.UpperBodyActionElapsed = 0f;
        runtimeState.UpperBodyActionDuration = 0f;
    }
    #endregion

    #region Binding
    /// <summary>
    /// Resolves or creates the AnimatorOverrideController used to replace only the authored upper-body action motion.
    /// </summary>
    /// <param name="animator">Animator requiring an upper-body runtime binding.</param>
    /// <param name="clipConfig">Baked clip table containing every valid upper-body action motion.</param>
    /// <param name="additionalWeaponVisuals">Runtime table contributing defined shooting clips.</param>
    /// <param name="binding">Resolved valid binding.</param>
    /// <returns>True when the Animator contains the required layer, states, controller, and one configured action override key.</returns>
    private static bool TryGetBinding(Animator animator,
                                      in PlayerUpperBodyAnimationClipConfig clipConfig,
                                      in DynamicBuffer<PlayerAdditionalWeaponVisualElement> additionalWeaponVisuals,
                                      out PlayerUpperBodyAnimatorBinding binding)
    {
        binding = null;

        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
            return false;

        int animatorId = animator.GetInstanceID();

        if (bindingsByAnimatorId.TryGetValue(animatorId, out binding) &&
            binding != null &&
            binding.AnimatorComponent == animator &&
            binding.OverrideController == animator.runtimeAnimatorController)
        {
            ApplyIdleOverride(binding, clipConfig.UpperBodyIdle.Value);
            return true;
        }

        int upperBodyLayerIndex = animator.GetLayerIndex(UpperBodyLayerName);
        bool hasRequiredStates = upperBodyLayerIndex >= 0 &&
                                 animator.HasState(upperBodyLayerIndex, UpperBodyIdleStateHash) &&
                                 animator.HasState(upperBodyLayerIndex, UpperBodyActionStateHash);

        if (!hasRequiredStates)
        {
            LogInvalidAnimator(animator);
            return false;
        }

        RuntimeAnimatorController sourceController = animator.runtimeAnimatorController;
        AnimatorOverrideController overrideController = sourceController as AnimatorOverrideController;

        if (overrideController == null)
        {
            overrideController = new AnimatorOverrideController(sourceController);
            animator.runtimeAnimatorController = overrideController;
        }

        if (!TryResolveActionOverrideKey(overrideController,
                                         in clipConfig,
                                         in additionalWeaponVisuals,
                                         out AnimationClip actionOverrideKey))
        {
            LogInvalidAnimator(animator);
            return false;
        }

        AnimationClip upperBodyIdleClip = clipConfig.UpperBodyIdle.Value;
        AnimationClip idleOverrideKey = null;

        if (upperBodyIdleClip != null &&
            !TryResolveIdleOverrideKey(overrideController, upperBodyIdleClip, out idleOverrideKey))
            LogMissingIdleOverrideKey(animator);

        binding = new PlayerUpperBodyAnimatorBinding
        {
            AnimatorComponent = animator,
            OverrideController = overrideController,
            IdleOverrideKey = idleOverrideKey,
            ActionOverrideKey = actionOverrideKey,
            AppliedIdleClip = null,
            AppliedClip = null,
            UpperBodyLayerIndex = upperBodyLayerIndex
        };
        ApplyIdleOverride(binding, upperBodyIdleClip);
        bindingsByAnimatorId[animatorId] = binding;
        return true;
    }

    /// <summary>
    /// Resolves the authored upper-body idle motion so the dedicated idle clip can be overridden independently.
    /// </summary>
    /// <param name="overrideController">Animator override controller exposing authored clip keys.</param>
    /// <param name="upperBodyIdleClip">Configured upper-body idle clip from the active Animation Bindings preset.</param>
    /// <param name="idleOverrideKey">Resolved original controller clip that represents the upper-body idle state.</param>
    /// <returns>True when a matching idle override key was found.</returns>
    private static bool TryResolveIdleOverrideKey(AnimatorOverrideController overrideController,
                                                  AnimationClip upperBodyIdleClip,
                                                  out AnimationClip idleOverrideKey)
    {
        idleOverrideKey = null;

        if (overrideController == null || upperBodyIdleClip == null)
            return false;

        List<KeyValuePair<AnimationClip, AnimationClip>> overrides =
            new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
        overrideController.GetOverrides(overrides);

        for (int overrideIndex = 0; overrideIndex < overrides.Count; overrideIndex++)
        {
            AnimationClip key = overrides[overrideIndex].Key;

            if (key == upperBodyIdleClip)
            {
                idleOverrideKey = key;
                return true;
            }

            if (idleOverrideKey == null && overrides[overrideIndex].Value == upperBodyIdleClip)
                idleOverrideKey = key;
        }

        return idleOverrideKey != null;
    }

    /// <summary>
    /// Resolves the authored upper-body action motion from every concrete action clip configured for the player.
    /// </summary>
    /// <param name="overrideController">Animator override controller exposing authored clip keys.</param>
    /// <param name="clipConfig">Baked concrete action clips accepted as the action-state anchor.</param>
    /// <param name="additionalWeaponVisuals">Runtime table contributing defined shooting clips.</param>
    /// <param name="actionOverrideKey">Resolved original controller clip that may be overridden safely.</param>
    /// <returns>True when one configured action clip is an authored controller key.</returns>
    private static bool TryResolveActionOverrideKey(AnimatorOverrideController overrideController,
                                                    in PlayerUpperBodyAnimationClipConfig clipConfig,
                                                    in DynamicBuffer<PlayerAdditionalWeaponVisualElement> additionalWeaponVisuals,
                                                    out AnimationClip actionOverrideKey)
    {
        actionOverrideKey = null;

        if (overrideController == null)
            return false;

        List<KeyValuePair<AnimationClip, AnimationClip>> overrides =
            new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
        overrideController.GetOverrides(overrides);

        for (int overrideIndex = 0; overrideIndex < overrides.Count; overrideIndex++)
        {
            AnimationClip key = overrides[overrideIndex].Key;

            if (key == clipConfig.DefaultShoot.Value)
            {
                actionOverrideKey = key;
                return true;
            }

            if (actionOverrideKey == null &&
                IsConfiguredActionClip(key, in clipConfig, in additionalWeaponVisuals))
                actionOverrideKey = key;
        }

        return actionOverrideKey != null;
    }

    /// <summary>
    /// Checks whether one authored controller key belongs to the baked upper-body action clip table.
    /// </summary>
    /// <param name="clip">Authored controller key being classified.</param>
    /// <param name="clipConfig">Baked concrete action clips accepted by the presentation driver.</param>
    /// <param name="additionalWeaponVisuals">Runtime table contributing defined shooting clips.</param>
    /// <returns>True when the clip is configured for shoot, charge, or release presentation.</returns>
    private static bool IsConfiguredActionClip(AnimationClip clip,
                                               in PlayerUpperBodyAnimationClipConfig clipConfig,
                                               in DynamicBuffer<PlayerAdditionalWeaponVisualElement> additionalWeaponVisuals)
    {
        if (clip == null)
            return false;

        if (clip == clipConfig.PrimaryCharge.Value ||
               clip == clipConfig.SecondaryCharge.Value ||
               clip == clipConfig.PrimaryRelease.Value ||
               clip == clipConfig.SecondaryRelease.Value)
            return true;

        if (!additionalWeaponVisuals.IsCreated)
            return false;

        for (int entryIndex = 0; entryIndex < additionalWeaponVisuals.Length; entryIndex++)
        {
            if (clip == additionalWeaponVisuals[entryIndex].ShootAnimationClip.Value)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Applies one clip override only when the selected clip changes, avoiding per-frame controller rebinding.
    /// </summary>
    /// <param name="binding">Cached Animator override binding.</param>
    /// <param name="clip">Concrete clip replacing the default upper-body shoot motion.</param>
    private static void ApplyOverride(PlayerUpperBodyAnimatorBinding binding, AnimationClip clip)
    {
        if (binding == null || binding.OverrideController == null || clip == null || binding.AppliedClip == clip)
            return;

        binding.OverrideController[binding.ActionOverrideKey] = clip;
        binding.AppliedClip = clip;
    }

    /// <summary>
    /// Applies the configured upper-body idle clip once per binding revision when the Animator controller exposes it.
    /// </summary>
    /// <param name="binding">Cached Animator override binding.</param>
    /// <param name="clip">Concrete upper-body idle clip from the active Animation Bindings preset.</param>
    private static void ApplyIdleOverride(PlayerUpperBodyAnimatorBinding binding, AnimationClip clip)
    {
        if (binding == null ||
            binding.OverrideController == null ||
            binding.IdleOverrideKey == null ||
            clip == null ||
            binding.AppliedIdleClip == clip)
            return;

        binding.OverrideController[binding.IdleOverrideKey] = clip;
        binding.AppliedIdleClip = clip;
    }

    /// <summary>
    /// Checks whether one cached binding can safely receive Animator playback commands.
    /// </summary>
    /// <param name="binding">Cached Animator binding to inspect.</param>
    /// <returns>True when its Animator component and hierarchy are active.</returns>
    private static bool CanDriveAnimator(PlayerUpperBodyAnimatorBinding binding)
    {
        return binding != null &&
               binding.AnimatorComponent != null &&
               binding.AnimatorComponent.isActiveAndEnabled;
    }
    #endregion

    #region Resolution
    /// <summary>
    /// Resolves the currently charging active slot using primary-slot precedence for simultaneous charge states.
    /// </summary>
    /// <param name="powerUpsConfig">Current active-slot configuration.</param>
    /// <param name="powerUpsState">Current active-slot runtime state.</param>
    /// <param name="chargingSlot">Resolved charging slot config.</param>
    /// <returns>True when at least one defined hold-charge slot is charging.</returns>
    private static bool TryResolveChargingSlot(in PlayerPowerUpsConfig powerUpsConfig,
                                               in PlayerPowerUpsState powerUpsState,
                                               out PlayerPowerUpSlotConfig chargingSlot)
    {
        if (powerUpsState.PrimaryIsCharging != 0 &&
            powerUpsConfig.PrimarySlot.IsDefined != 0 &&
            powerUpsConfig.PrimarySlot.ToolKind == ActiveToolKind.ChargeShot)
        {
            chargingSlot = powerUpsConfig.PrimarySlot;
            return true;
        }

        if (powerUpsState.SecondaryIsCharging != 0 &&
            powerUpsConfig.SecondarySlot.IsDefined != 0 &&
            powerUpsConfig.SecondarySlot.ToolKind == ActiveToolKind.ChargeShot)
        {
            chargingSlot = powerUpsConfig.SecondarySlot;
            return true;
        }

        chargingSlot = default;
        return false;
    }

    /// <summary>
    /// Resolves the concrete shooting clip from the aggregated passive-tools snapshot. When an equipped Switch
    /// Weapon module owns the visible attachment, its defined ID drives the matching clip; otherwise
    /// the scalable default ID is used. Missing clips degrade gracefully to the baked default.
    /// </summary>
    /// <param name="clipConfig">Baked concrete clip table sourced from the visual preset entries.</param>
    /// <param name="passiveToolsState">Aggregated weapon visual selection containing the active Switch Weapon hook.</param>
    /// <param name="additionalWeaponVisuals">Runtime table binding defined weapon IDs to shooting clips.</param>
    /// <param name="defaultAdditionalWeaponId">Scalable default attachment ID.</param>
    /// <returns>Selected concrete clip or Default Shoot fallback.</returns>
    private static AnimationClip ResolveShootClip(in PlayerUpperBodyAnimationClipConfig clipConfig,
                                                  in PlayerPassiveToolsState passiveToolsState,
                                                  in DynamicBuffer<PlayerAdditionalWeaponVisualElement> additionalWeaponVisuals,
                                                  FixedString64Bytes defaultAdditionalWeaponId)
    {
        FixedString64Bytes selectedWeaponId = passiveToolsState.HasWeaponSwitch != 0
            ? passiveToolsState.WeaponId
            : defaultAdditionalWeaponId;

        if (selectedWeaponId.Length > 0 && additionalWeaponVisuals.IsCreated)
        {
            for (int entryIndex = 0; entryIndex < additionalWeaponVisuals.Length; entryIndex++)
            {
                PlayerAdditionalWeaponVisualElement entry = additionalWeaponVisuals[entryIndex];

                if (!entry.WeaponId.Equals(selectedWeaponId))
                    continue;

                AnimationClip selectedClip = entry.ShootAnimationClip.Value;
                return selectedClip != null ? selectedClip : clipConfig.DefaultShoot.Value;
            }
        }

        return clipConfig.DefaultShoot.Value;
    }

    /// <summary>
    /// Resolves one optional hold-charge clip.
    /// </summary>
    /// <param name="clipConfig">Baked concrete clip table.</param>
    /// <param name="slot">Charge animation selector from the active payload.</param>
    /// <returns>Selected clip or null when the payload disables charge animation.</returns>
    private static AnimationClip ResolveChargeClip(in PlayerUpperBodyAnimationClipConfig clipConfig,
                                                   PlayerChargeAnimationClipSlot slot)
    {
        switch (slot)
        {
            case PlayerChargeAnimationClipSlot.Primary:
                return clipConfig.PrimaryCharge.Value;
            case PlayerChargeAnimationClipSlot.Secondary:
                return clipConfig.SecondaryCharge.Value;
            default:
                return null;
        }
    }

    /// <summary>
    /// Resolves one optional hold-charge release clip.
    /// </summary>
    /// <param name="clipConfig">Baked concrete clip table.</param>
    /// <param name="slot">Release animation selector from the active payload.</param>
    /// <returns>Selected clip or null when the payload disables release animation.</returns>
    private static AnimationClip ResolveReleaseClip(in PlayerUpperBodyAnimationClipConfig clipConfig,
                                                    PlayerReleaseAnimationClipSlot slot)
    {
        switch (slot)
        {
            case PlayerReleaseAnimationClipSlot.Primary:
                return clipConfig.PrimaryRelease.Value;
            case PlayerReleaseAnimationClipSlot.Secondary:
                return clipConfig.SecondaryRelease.Value;
            default:
                return null;
        }
    }

    /// <summary>
    /// Resolves a shooting action duration that samples the complete clip exactly once before the next configured shot.
    /// </summary>
    /// <param name="clip">Concrete shooting clip.</param>
    /// <param name="rateOfFire">Current ECS-authoritative shots-per-second value.</param>
    /// <returns>Positive complete-playback duration.</returns>
    private static float ResolveShootDuration(AnimationClip clip, float rateOfFire)
    {
        if (rateOfFire > 0f)
            return math.max(MinimumDurationSeconds, 1f / rateOfFire);

        return ResolveNaturalDuration(clip);
    }

    /// <summary>
    /// Resolves the natural positive duration of one optional clip.
    /// </summary>
    /// <param name="clip">Concrete clip whose imported duration is used.</param>
    /// <returns>Positive clip duration or the minimum presentation duration when missing.</returns>
    private static float ResolveNaturalDuration(AnimationClip clip)
    {
        return clip != null
            ? math.max(MinimumDurationSeconds, clip.length)
            : MinimumDurationSeconds;
    }

    /// <summary>
    /// Stores charge state edges after presentation consumed the current frame.
    /// </summary>
    /// <param name="powerUpsState">Current active-slot charge state.</param>
    /// <param name="runtimeState">Mutable Animator runtime state receiving edge history.</param>
    private static void UpdatePreviousChargeState(in PlayerPowerUpsState powerUpsState,
                                                  ref PlayerAnimatorRuntimeState runtimeState)
    {
        runtimeState.PreviousPrimaryCharging = powerUpsState.PrimaryIsCharging;
        runtimeState.PreviousSecondaryCharging = powerUpsState.SecondaryIsCharging;
    }
    #endregion

    #region Diagnostics
    /// <summary>
    /// Reports one invalid Animator setup once per Animator instance in editor sessions.
    /// </summary>
    /// <param name="animator">Animator missing required upper-body states or layer.</param>
    private static void LogInvalidAnimator(Animator animator)
    {
#if UNITY_EDITOR
        if (animator != null && invalidAnimatorWarnings.Add(animator.GetInstanceID()))
            Debug.LogWarning("[PlayerUpperBodyAnimationPresentationUtility] Animator requires UpperBody.ST_Idle, UpperBody.ST_Upper_Shoot, and an action-state motion assigned to one configured upper-body action clip.", animator);
#endif
    }

    /// <summary>
    /// Reports a missing optional concrete clip once per Animator and action kind in editor sessions.
    /// </summary>
    /// <param name="binding">Animator binding attempting to start the action.</param>
    /// <param name="actionKind">Action whose selected concrete clip is missing.</param>
    private static void LogMissingClip(PlayerUpperBodyAnimatorBinding binding,
                                       PlayerUpperBodyAnimationActionKind actionKind)
    {
#if UNITY_EDITOR
        if (binding == null || binding.AnimatorComponent == null || actionKind == PlayerUpperBodyAnimationActionKind.None)
            return;

        int warningKey = (binding.AnimatorComponent.GetInstanceID() * 397) ^ (int)actionKind;

        if (missingClipWarnings.Add(warningKey))
            Debug.LogWarning(string.Format("[PlayerUpperBodyAnimationPresentationUtility] The selected {0} upper-body clip slot is empty. Assign it in the active Animation Bindings preset.", actionKind), binding.AnimatorComponent);
#endif
    }

    /// <summary>
    /// Reports a missing upper-body idle override key once per Animator instance in editor sessions.
    /// </summary>
    /// <param name="animator">Animator whose controller cannot expose the configured upper-body idle clip as an override key.</param>
    private static void LogMissingIdleOverrideKey(Animator animator)
    {
#if UNITY_EDITOR
        if (animator != null && missingIdleOverrideKeyWarnings.Add(animator.GetInstanceID()))
            Debug.LogWarning("[PlayerUpperBodyAnimationPresentationUtility] UpperBody.ST_Idle could not be bound to the configured upper-body idle clip. Assign the clip as the authored state motion or rebuild the controller from the Animation Bindings preset.", animator);
#endif
    }
    #endregion

    #region Nested Types
    /// <summary>
    /// Caches the managed Animator resources required to replace and sample one upper-body action state.
    /// </summary>
    private sealed class PlayerUpperBodyAnimatorBinding
    {
        public Animator AnimatorComponent;
        public AnimatorOverrideController OverrideController;
        public AnimationClip IdleOverrideKey;
        public AnimationClip ActionOverrideKey;
        public AnimationClip AppliedIdleClip;
        public AnimationClip AppliedClip;
        public int UpperBodyLayerIndex;
    }
    #endregion

    #endregion
}
