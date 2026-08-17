using System;
using UnityEngine;

#region Returning Projectile Payload
/// <summary>
/// Defines the outbound presentation, return path, hit policy, and module interaction rules for player projectiles.
/// </summary>
[Serializable]
public sealed class PowerUpReturningProjectilesModuleData
{
    #region Fields

    #region Serialized Fields
    [Header("Projectile Override")]
    [Tooltip("Optional projectile prefab used while this module modifies a shot. Leave empty to preserve the shooter's configured projectile prefab.")]
    [SerializeField]
    private GameObject replacementProjectilePrefab;

    [Tooltip("Keeps the Player Projectile VFX attached by the Player Visual preset when the replacement projectile prefab is used. Disable it when the replacement prefab already provides its own flight presentation. Muzzle flash, hit, and death VFX are unaffected.")]
    [SerializeField]
    private bool keepProjectileVfx = true;

    [Tooltip("Keeps the standard muzzle-flash VFX when the replacement projectile prefab is used. Disable it when the replacement presentation includes its own launch effect.")]
    [SerializeField]
    private bool keepMuzzleFlashVfx = true;

    [Tooltip("Keeps hit-react and elemental hit VFX when the replacement projectile prefab damages an enemy. Gameplay payloads and damage remain unaffected.")]
    [SerializeField]
    private bool keepHitVfx = true;

    [Tooltip("Keeps range, lifetime, and terminal-wall projectile death VFX when the replacement projectile prefab despawns. Enemy-hit VFX are controlled separately.")]
    [SerializeField]
    private bool keepDeathVfx = true;

    [Tooltip("Selects whether the projectile retraces recorded outbound positions or travels directly toward the current player position.")]
    [SerializeField]
    private ProjectileReturnPathMode returnPathMode = ProjectileReturnPathMode.RetraceOutboundPath;

    [Tooltip("Multiplier applied to projectile speed during the return phase.")]
    [SerializeField]
    private float returnSpeedMultiplier = 1f;

    [Header("Outbound Trajectory")]
    [Tooltip("Multiplier applied to the projectile's normal maximum travel distance before return begins. Values below one shorten the outbound route, while values above one extend it.")]
    [SerializeField]
    private float outboundRangeMultiplier = 1f;

    [Tooltip("Multiplier applied to the projectile's normal maximum lifetime before return begins. This is independent from range so time-limited and distance-limited shots remain configurable.")]
    [SerializeField]
    private float outboundLifetimeMultiplier = 1f;

    [Header("Outbound Hits")]
    [Tooltip("Controls enemy-impact termination during outbound travel. Natural Penetration preserves the projectile's current behavior, Complete Outbound Travel continues after natural penetration is exhausted, and Limited Additional Hits consumes a separate extra hit budget. Physical walls and obstacles always remain authoritative.")]
    [SerializeField]
    private ProjectileOutboundHitPolicy outboundHitPolicy = ProjectileOutboundHitPolicy.NaturalPenetration;

    [Tooltip("Enemy hits allowed during outbound travel after the projectile's natural penetration capacity is exhausted. Used only by Limited Additional Hits.")]
    [SerializeField]
    private int additionalOutboundHits = 1;

    [Header("Return Transition")]
    [Tooltip("Seconds the projectile remains positionally stationary at its outbound endpoint before turnaround or direct return begins. Set to zero to disable the delay.")]
    [SerializeField]
    private float returnDelaySeconds;

    [Tooltip("Additional return-start controller vibration as a multiplier of the configured firing rumble. Set to zero to disable this haptic pulse without affecting camera shake.")]
    [SerializeField]
    private float returnRumbleMultiplier = 0.5f;

    [Tooltip("Additional return-start camera shake as a multiplier of the configured firing shake. Set to zero to disable this visual pulse without affecting controller rumble.")]
    [SerializeField]
    private float returnCameraShakeMultiplier = 0.5f;

    [Header("Projectile Scale")]
    [Tooltip("Multiplier applied to projectile scale during outbound travel.")]
    [SerializeField]
    private float outboundSizeMultiplier = 1f;

    [Tooltip("Multiplier applied to projectile scale when return travel begins.")]
    [SerializeField]
    private float returnSizeMultiplier = 1f;

    [Header("Rotation")]
    [Tooltip("Continuously rotates the projectile around its selected local axis during both outbound and return travel.")]
    [SerializeField]
    private bool spinDuringFlight = true;

    [Tooltip("Continuous flight-spin speed in degrees per second. Used only when Spin During Flight is enabled.")]
    [SerializeField]
    private float spinSpeedDegreesPerSecond = 540f;

    [Tooltip("Local axis used by continuous flight spin.")]
    [SerializeField]
    private ProjectileReturnRotationAxis spinAxis = ProjectileReturnRotationAxis.Vertical;

    [Tooltip("Turnaround rotation speed in degrees per second. Used for the mandatory 180-degree turn when continuous flight spin is disabled.")]
    [SerializeField]
    private float turnaroundRotationSpeedDegreesPerSecond = 720f;

    [Tooltip("Local axis used by the 180-degree turnaround when continuous flight spin is disabled.")]
    [SerializeField]
    private ProjectileReturnRotationAxis turnaroundAxis = ProjectileReturnRotationAxis.Vertical;

    [Header("Return Hits")]
    [Tooltip("Controls whether the projectile must finish its return regardless of hits or can despawn after a separate return hit budget is consumed.")]
    [SerializeField]
    private ProjectileReturnHitPolicy returnHitPolicy = ProjectileReturnHitPolicy.CompleteReturn;

    [Tooltip("Enemy hits allowed during return after the projectile's natural penetration capacity has been exhausted. Used only by Limited Additional Hits.")]
    [SerializeField]
    private int additionalReturnHits = 1;

    [Header("Trajectory Precision")]
    [Tooltip("Minimum world-space distance between recorded outbound path points. Smaller values improve retrace precision and increase buffer usage.")]
    [SerializeField]
    private float pathSampleDistance = 0.25f;

    [Tooltip("Distance from the spawn point or current player position at which return travel is considered complete.")]
    [SerializeField]
    private float returnCompletionDistance = 0.2f;

    [Header("Power-Up Interactions")]
    [Tooltip("Allows Returning Projectiles to use interaction policies for compatible modules supplied by other power-ups. Modules composed inside this same power-up remain compatible when disabled.")]
    [SerializeField]
    private bool allowOtherPowerUpInteractions = true;

    [Tooltip("Allows Projectile Split to generate child shots from a projectile modified by this module. When external interactions are disabled, this setting still applies to Projectile Split composed inside this power-up.")]
    [SerializeField]
    private bool enableProjectileSplitting = true;

    [Tooltip("Applies this module to child projectiles generated by Projectile Split. When external interactions are disabled, this setting still applies to Projectile Split composed inside this power-up.")]
    [SerializeField]
    private bool applyToSplitProjectiles = true;

    [Tooltip("Consumes available wall bounces before a wall can start return travel. Range, lifetime, and hit limits remain authoritative. When external interactions are disabled, this setting still applies to Bouncing Projectiles composed inside this power-up.")]
    [SerializeField]
    private bool completeBouncesBeforeReturn = true;

    [Tooltip("Waits for an Orbital Projectiles path to complete before return begins. When external interactions are disabled, this setting still applies to Orbital Projectiles composed inside this power-up.")]
    [SerializeField]
    private bool completeOrbitalPathBeforeReturn = true;

    [Tooltip("Applies projectile-size Character Tuning supplied by Tiny Projectiles, Mega Projectiles, or compatible power-ups. With external interactions disabled, only size tuning composed inside this power-up can apply.")]
    [SerializeField]
    private bool applyTinyMegaProjectileScaling = true;

    [Tooltip("Applies this module to projectile shots emitted by another active power-up, including Shotgun and non-laser Charge Shot releases. Projectile emitters composed inside this power-up carry their Returning Projectiles override directly.")]
    [SerializeField]
    private bool applyToActivePowerUpProjectiles;

    [Tooltip("Allows another projectile from the same non-toggleable active slot to spawn before its previous returning projectile despawns.")]
    [SerializeField]
    private bool allowConcurrentActiveProjectiles;
    #endregion

    #endregion

    #region Properties
    public GameObject ReplacementProjectilePrefab => replacementProjectilePrefab;
    public bool KeepProjectileVfx => keepProjectileVfx;
    public bool KeepMuzzleFlashVfx => keepMuzzleFlashVfx;
    public bool KeepHitVfx => keepHitVfx;
    public bool KeepDeathVfx => keepDeathVfx;
    public ProjectileReturnPathMode ReturnPathMode => returnPathMode;
    public float ReturnSpeedMultiplier => returnSpeedMultiplier;
    public float OutboundRangeMultiplier => outboundRangeMultiplier;
    public float OutboundLifetimeMultiplier => outboundLifetimeMultiplier;
    public ProjectileOutboundHitPolicy OutboundHitPolicy => outboundHitPolicy;
    public int AdditionalOutboundHits => additionalOutboundHits;
    public float ReturnDelaySeconds => returnDelaySeconds;
    public float ReturnRumbleMultiplier => returnRumbleMultiplier;
    public float ReturnCameraShakeMultiplier => returnCameraShakeMultiplier;
    public float OutboundSizeMultiplier => outboundSizeMultiplier;
    public float ReturnSizeMultiplier => returnSizeMultiplier;
    public bool SpinDuringFlight => spinDuringFlight;
    public float SpinSpeedDegreesPerSecond => spinSpeedDegreesPerSecond;
    public ProjectileReturnRotationAxis SpinAxis => spinAxis;
    public float TurnaroundRotationSpeedDegreesPerSecond => turnaroundRotationSpeedDegreesPerSecond;
    public ProjectileReturnRotationAxis TurnaroundAxis => turnaroundAxis;
    public ProjectileReturnHitPolicy ReturnHitPolicy => returnHitPolicy;
    public int AdditionalReturnHits => additionalReturnHits;
    public float PathSampleDistance => pathSampleDistance;
    public float ReturnCompletionDistance => returnCompletionDistance;
    public bool AllowOtherPowerUpInteractions => allowOtherPowerUpInteractions;
    public bool EnableProjectileSplitting => enableProjectileSplitting;
    public bool ApplyToSplitProjectiles => applyToSplitProjectiles;
    public bool CompleteBouncesBeforeReturn => completeBouncesBeforeReturn;
    public bool CompleteOrbitalPathBeforeReturn => completeOrbitalPathBeforeReturn;
    public bool ApplyTinyMegaProjectileScaling => applyTinyMegaProjectileScaling;
    public bool ApplyToActivePowerUpProjectiles => applyToActivePowerUpProjectiles;
    public bool AllowConcurrentActiveProjectiles => allowConcurrentActiveProjectiles;
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Replaces every authored value, allowing preset defaults and focused tests to build the payload without serialized-property mutation.
    /// </summary>
    /// <param name="replacementProjectilePrefabValue">Optional projectile prefab override.</param>
    /// <param name="keepProjectileVfxValue">Whether replacement projectiles retain the Player Visual preset's attached projectile VFX.</param>
    /// <param name="keepMuzzleFlashVfxValue">Whether replacement projectiles retain standard muzzle-flash VFX.</param>
    /// <param name="keepHitVfxValue">Whether replacement projectiles retain enemy hit-react and elemental hit VFX.</param>
    /// <param name="keepDeathVfxValue">Whether replacement projectiles retain range, lifetime, and terminal-wall death VFX.</param>
    /// <param name="returnPathModeValue">Return path strategy.</param>
    /// <param name="returnSpeedMultiplierValue">Return speed multiplier.</param>
    /// <param name="outboundRangeMultiplierValue">Outbound maximum-range multiplier.</param>
    /// <param name="outboundLifetimeMultiplierValue">Outbound maximum-lifetime multiplier.</param>
    /// <param name="outboundHitPolicyValue">Enemy-impact termination policy used during outbound travel.</param>
    /// <param name="additionalOutboundHitsValue">Additional outbound hit budget used after natural penetration is exhausted.</param>
    /// <param name="returnDelaySecondsValue">Stationary delay before turnaround or return.</param>
    /// <param name="returnRumbleMultiplierValue">Return-start rumble multiplier relative to firing rumble.</param>
    /// <param name="returnCameraShakeMultiplierValue">Return-start camera-shake multiplier relative to firing shake.</param>
    /// <param name="outboundSizeMultiplierValue">Outbound scale multiplier.</param>
    /// <param name="returnSizeMultiplierValue">Return scale multiplier.</param>
    /// <param name="spinDuringFlightValue">Whether continuous spin is enabled.</param>
    /// <param name="spinSpeedDegreesPerSecondValue">Continuous spin speed.</param>
    /// <param name="spinAxisValue">Continuous spin axis.</param>
    /// <param name="turnaroundRotationSpeedDegreesPerSecondValue">Turnaround rotation speed.</param>
    /// <param name="turnaroundAxisValue">Turnaround rotation axis.</param>
    /// <param name="returnHitPolicyValue">Return hit policy.</param>
    /// <param name="additionalReturnHitsValue">Additional return hit budget.</param>
    /// <param name="pathSampleDistanceValue">Path sampling distance.</param>
    /// <param name="returnCompletionDistanceValue">Return completion distance.</param>
    /// <param name="allowOtherPowerUpInteractionsValue">Whether interaction policies may include modules owned by other power-ups.</param>
    /// <param name="enableProjectileSplittingValue">Whether compatible Projectile Split modules may generate children.</param>
    /// <param name="applyToSplitProjectilesValue">Whether split children from other power-ups inherit the module.</param>
    /// <param name="completeBouncesBeforeReturnValue">Whether bounces from other power-ups complete first.</param>
    /// <param name="completeOrbitalPathBeforeReturnValue">Whether orbital paths from other power-ups complete first.</param>
    /// <param name="applyTinyMegaProjectileScalingValue">Whether compatible projectile-size Character Tuning applies.</param>
    /// <param name="applyToActivePowerUpProjectilesValue">Whether other active power-up projectile tools inherit the module.</param>
    /// <param name="allowConcurrentActiveProjectilesValue">Whether the owning non-toggleable active can overlap projectiles.</param>
    public void Configure(GameObject replacementProjectilePrefabValue,
                          bool keepProjectileVfxValue,
                          bool keepMuzzleFlashVfxValue,
                          bool keepHitVfxValue,
                          bool keepDeathVfxValue,
                          ProjectileReturnPathMode returnPathModeValue,
                          float returnSpeedMultiplierValue,
                          float outboundRangeMultiplierValue,
                          float outboundLifetimeMultiplierValue,
                          ProjectileOutboundHitPolicy outboundHitPolicyValue,
                          int additionalOutboundHitsValue,
                          float returnDelaySecondsValue,
                          float returnRumbleMultiplierValue,
                          float returnCameraShakeMultiplierValue,
                          float outboundSizeMultiplierValue,
                          float returnSizeMultiplierValue,
                          bool spinDuringFlightValue,
                          float spinSpeedDegreesPerSecondValue,
                          ProjectileReturnRotationAxis spinAxisValue,
                          float turnaroundRotationSpeedDegreesPerSecondValue,
                          ProjectileReturnRotationAxis turnaroundAxisValue,
                          ProjectileReturnHitPolicy returnHitPolicyValue,
                          int additionalReturnHitsValue,
                          float pathSampleDistanceValue,
                          float returnCompletionDistanceValue,
                          bool allowOtherPowerUpInteractionsValue,
                          bool enableProjectileSplittingValue,
                          bool applyToSplitProjectilesValue,
                          bool completeBouncesBeforeReturnValue,
                          bool completeOrbitalPathBeforeReturnValue,
                          bool applyTinyMegaProjectileScalingValue,
                          bool applyToActivePowerUpProjectilesValue,
                          bool allowConcurrentActiveProjectilesValue)
    {
        replacementProjectilePrefab = replacementProjectilePrefabValue;
        keepProjectileVfx = keepProjectileVfxValue;
        keepMuzzleFlashVfx = keepMuzzleFlashVfxValue;
        keepHitVfx = keepHitVfxValue;
        keepDeathVfx = keepDeathVfxValue;
        returnPathMode = returnPathModeValue;
        returnSpeedMultiplier = returnSpeedMultiplierValue;
        outboundRangeMultiplier = outboundRangeMultiplierValue;
        outboundLifetimeMultiplier = outboundLifetimeMultiplierValue;
        outboundHitPolicy = outboundHitPolicyValue;
        additionalOutboundHits = additionalOutboundHitsValue;
        returnDelaySeconds = returnDelaySecondsValue;
        returnRumbleMultiplier = returnRumbleMultiplierValue;
        returnCameraShakeMultiplier = returnCameraShakeMultiplierValue;
        outboundSizeMultiplier = outboundSizeMultiplierValue;
        returnSizeMultiplier = returnSizeMultiplierValue;
        spinDuringFlight = spinDuringFlightValue;
        spinSpeedDegreesPerSecond = spinSpeedDegreesPerSecondValue;
        spinAxis = spinAxisValue;
        turnaroundRotationSpeedDegreesPerSecond = turnaroundRotationSpeedDegreesPerSecondValue;
        turnaroundAxis = turnaroundAxisValue;
        returnHitPolicy = returnHitPolicyValue;
        additionalReturnHits = additionalReturnHitsValue;
        pathSampleDistance = pathSampleDistanceValue;
        returnCompletionDistance = returnCompletionDistanceValue;
        allowOtherPowerUpInteractions = allowOtherPowerUpInteractionsValue;
        enableProjectileSplitting = enableProjectileSplittingValue;
        applyToSplitProjectiles = applyToSplitProjectilesValue;
        completeBouncesBeforeReturn = completeBouncesBeforeReturnValue;
        completeOrbitalPathBeforeReturn = completeOrbitalPathBeforeReturnValue;
        applyTinyMegaProjectileScaling = applyTinyMegaProjectileScalingValue;
        applyToActivePowerUpProjectiles = applyToActivePowerUpProjectilesValue;
        allowConcurrentActiveProjectiles = allowConcurrentActiveProjectilesValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Preserves authored values so the Player Management Tool can report invalid combinations without silently snapping them.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}
#endregion
