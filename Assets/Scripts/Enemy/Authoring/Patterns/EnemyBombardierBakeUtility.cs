using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Converts Bombardier authoring payloads into ECS runtime configs.
/// </summary>
internal static class EnemyBombardierBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Appends one Bombardier module config from a resolved payload.
    /// </summary>
    /// <param name="payload">Resolved module payload.</param>
    /// <param name="bombardierConfigs">Target config list.</param>
    /// <param name="result">Mutable compiled pattern result receiving runtime prefab settings.</param>
    public static void TryAddBombardierModule(EnemyPatternModulePayloadData payload,
                                              List<EnemyBombardierConfigElement> bombardierConfigs,
                                              ref EnemyCompiledPatternBakeResult result)
    {
        if (bombardierConfigs == null)
            return;

        if (payload == null || payload.Bombardier == null)
            return;

        EnemyBombardierModuleData bombardierData = payload.Bombardier;
        TryAssignBombardierRuntimeSettings(bombardierData, ref result);

        float minimumRange = math.max(0f, bombardierData.RandomMinimumDistance);
        float maximumRange = math.max(minimumRange, bombardierData.RandomMaximumDistance);
        EnemyBombardierLandingWarningPayload warning = bombardierData.LandingWarning;
        Color warningColor = warning != null ? warning.Color : Color.white;

        bombardierConfigs.Add(new EnemyBombardierConfigElement
        {
            AimPolicy = EnemyAdvancedPatternBakeUtility.ResolveShooterAimPolicy(bombardierData.AimPolicy),
            MovementPolicy = EnemyAdvancedPatternBakeUtility.ResolveShooterMovementPolicy(bombardierData.MovementPolicy),
            InReachTargetingMode = ResolveTargetingMode(bombardierData.InReachTargetingMode),
            OutOfReachTargetingMode = ResolveTargetingMode(bombardierData.OutOfReachTargetingMode),
            LaunchPattern = ResolveLaunchPattern(bombardierData.LaunchPattern),
            TrajectoryMode = ResolveTrajectoryMode(bombardierData.TrajectoryMode),
            FireInterval = math.max(0.01f, bombardierData.FireInterval),
            BurstCount = math.clamp(math.max(1, bombardierData.BurstCount), 1, 64),
            AimWindupSeconds = math.max(0f, bombardierData.AimWindupSeconds),
            PreLaunchStopSeconds = math.max(0f, bombardierData.PreLaunchStopSeconds),
            PostLaunchStopSeconds = math.max(0f, bombardierData.PostLaunchStopSeconds),
            IntraBurstDelay = math.max(0f, bombardierData.IntraBurstDelay),
            UseMinimumRange = 0,
            MinimumRange = 0f,
            UseMaximumRange = 0,
            MaximumRange = 0f,
            ExclusiveLookDirectionControl = 0,
            ActivationGates = EnemyWeaponInteractionActivationGate.Always,
            MaximumActivationSpeed = 0f,
            RecentlyDamagedWindowSeconds = 0f,
            BombsPerLaunch = math.clamp(math.max(1, bombardierData.BombsPerLaunch), 1, 64),
            LandingSpreadRadius = math.max(0f, bombardierData.LandingSpreadRadius),
            RadialPatternRadius = math.max(0f, bombardierData.RadialPatternRadius),
            RandomMinimumDistance = minimumRange,
            RandomMaximumDistance = maximumRange,
            FlightDurationSeconds = math.max(0.05f, bombardierData.FlightDurationSeconds),
            Gravity = math.max(0.01f, bombardierData.Gravity),
            ApexHeight = math.max(0.05f, bombardierData.ApexHeight),
            LaunchHeightOffset = bombardierData.LaunchHeightOffset,
            LandingHeightOffset = bombardierData.LandingHeightOffset,
            Damage = math.max(0f, bombardierData.Damage),
            DamageRadius = math.max(0f, bombardierData.DamageRadius),
            ImpactExplosionDelaySeconds = math.max(0f, bombardierData.ImpactExplosionDelaySeconds),
            BombScaleMultiplier = math.max(0.01f, bombardierData.BombScaleMultiplier),
            PreventMidAirInterception = bombardierData.PreventMidAirInterception ? (byte)1 : (byte)0,
            EnableLandingWarning = warning != null && warning.EnableLandingWarning ? (byte)1 : (byte)0,
            WarningLeadTimeSeconds = warning != null ? math.max(0f, warning.WarningLeadTimeSeconds) : 0f,
            WarningRadiusScale = warning != null ? math.max(0f, warning.WarningRadiusScale) : 0f,
            WarningRingWidth = warning != null ? math.max(0f, warning.RingWidth) : 0f,
            WarningHeightOffset = warning != null ? warning.HeightOffset : 0f,
            WarningMaximumAlpha = warning != null ? math.saturate(warning.MaximumAlpha) : 0f,
            WarningFadeOutSeconds = warning != null ? math.max(0f, warning.FadeOutSeconds) : 0f,
            WarningColor = new float4(warningColor.r, warningColor.g, warningColor.b, warningColor.a)
        });
    }

    /// <summary>
    /// Copies the first available Bombardier runtime prefab setting into the compiled result.
    /// </summary>
    /// <param name="bombardierData">Bombardier module data that may contain a runtime prefab.</param>
    /// <param name="result">Mutable compiled pattern result receiving runtime prefab settings.</param>
    public static void TryAssignBombardierRuntimeSettings(EnemyBombardierModuleData bombardierData,
                                                          ref EnemyCompiledPatternBakeResult result)
    {
        if (result.HasBombardierRuntimeSettings)
            return;

        if (bombardierData == null)
            return;

        EnemyBombardierRuntimeBombPayload runtimePayload = bombardierData.RuntimeBomb;

        if (runtimePayload == null)
            return;

        GameObject bombPrefab = runtimePayload.BombPrefab;

        if (bombPrefab == null)
            return;

        result.BombardierBombPrefab = bombPrefab;
        result.BombardierExplosionVfxPrefab = runtimePayload.ExplosionVfxPrefab;
        result.BombardierScaleExplosionVfxToDamageRadius = runtimePayload.ScaleExplosionVfxToDamageRadius;
        result.BombardierExplosionVfxScaleMultiplier = math.max(0.01f, runtimePayload.ExplosionVfxScaleMultiplier);
        result.HasBombardierRuntimeSettings = true;
    }

    /// <summary>
    /// Resolves one valid Bombardier targeting mode enum value.
    /// </summary>
    /// <param name="targetingMode">Authored targeting mode.</param>
    /// <returns>Supported targeting mode.</returns>
    public static EnemyBombardierTargetingMode ResolveTargetingMode(EnemyBombardierTargetingMode targetingMode)
    {
        switch (targetingMode)
        {
            case EnemyBombardierTargetingMode.Disabled:
            case EnemyBombardierTargetingMode.Player:
            case EnemyBombardierTargetingMode.RandomAroundEnemy:
            case EnemyBombardierTargetingMode.RandomAroundPlayer:
                return targetingMode;

            default:
                return EnemyBombardierTargetingMode.Disabled;
        }
    }

    /// <summary>
    /// Resolves one valid Bombardier launch pattern enum value.
    /// </summary>
    /// <param name="launchPattern">Authored launch pattern.</param>
    /// <returns>Supported launch pattern.</returns>
    public static EnemyBombardierLaunchPattern ResolveLaunchPattern(EnemyBombardierLaunchPattern launchPattern)
    {
        switch (launchPattern)
        {
            case EnemyBombardierLaunchPattern.Cluster:
            case EnemyBombardierLaunchPattern.Radial:
                return launchPattern;

            default:
                return EnemyBombardierLaunchPattern.Cluster;
        }
    }

    /// <summary>
    /// Resolves one valid Bombardier trajectory solver enum value.
    /// </summary>
    /// <param name="trajectoryMode">Authored trajectory mode.</param>
    /// <returns>Supported trajectory mode.</returns>
    public static EnemyBombardierTrajectoryMode ResolveTrajectoryMode(EnemyBombardierTrajectoryMode trajectoryMode)
    {
        switch (trajectoryMode)
        {
            case EnemyBombardierTrajectoryMode.FixedFlightTimeAndGravity:
            case EnemyBombardierTrajectoryMode.FixedApexHeight:
                return trajectoryMode;

            default:
                return EnemyBombardierTrajectoryMode.FixedFlightTimeAndGravity;
        }
    }
    #endregion

    #endregion
}
