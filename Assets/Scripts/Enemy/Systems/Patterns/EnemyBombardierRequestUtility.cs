using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Provides deterministic Bombardier request helpers shared by the runtime request system.
/// </summary>
internal static class EnemyBombardierRequestUtility
{
    #region Constants
    private static readonly float3 ForwardAxis = new float3(0f, 0f, 1f);
    private const float DirectionEpsilon = 1e-6f;
    private const uint RandomSeedMultiplier = 747796405u;
    #endregion

    #region Methods

    #region Runtime
    /// <summary>
    /// Synchronizes Bombardier runtime buffer length with the active config buffer.
    /// </summary>
    /// <param name="bombardierRuntime">Mutable runtime buffer to rebuild.</param>
    /// <param name="count">Required runtime element count.</param>
    /// <param name="enemyEntity">Enemy entity used to seed deterministic module random states.</param>
    internal static void SynchronizeBombardierRuntime(DynamicBuffer<EnemyBombardierRuntimeElement> bombardierRuntime,
                                                      int count,
                                                      Entity enemyEntity)
    {
        bombardierRuntime.Clear();

        for (int index = 0; index < count; index++)
            bombardierRuntime.Add(CreateDefaultRuntime(enemyEntity, index));
    }

    /// <summary>
    /// Creates a clean runtime state for one Bombardier module.
    /// </summary>
    /// <param name="enemyEntity">Enemy entity used to derive the random seed.</param>
    /// <param name="moduleIndex">Bombardier module index inside the active buffer.</param>
    /// <returns>Default Bombardier runtime element.</returns>
    internal static EnemyBombardierRuntimeElement CreateDefaultRuntime(Entity enemyEntity, int moduleIndex)
    {
        return new EnemyBombardierRuntimeElement
        {
            NextBurstTimer = 0f,
            NextBombInBurstTimer = 0f,
            PostLaunchStopTimer = 0f,
            RemainingBurstLaunches = 0,
            LaunchesCompletedInCurrentBurst = 0,
            BurstWindupDurationSeconds = 0f,
            IsPlayerInReach = 0,
            IsLaunchAllowed = 0,
            LockedTargetPosition = float3.zero,
            HasLockedTargetPosition = 0,
            RandomState = ResolveSeed(enemyEntity, moduleIndex)
        };
    }

    /// <summary>
    /// Advances mutable cadence timers for one Bombardier module.
    /// </summary>
    /// <param name="runtime">Mutable Bombardier runtime state.</param>
    /// <param name="deltaTime">Scaled enemy delta time.</param>
    internal static void AdvanceRuntimeTimers(ref EnemyBombardierRuntimeElement runtime, float deltaTime)
    {
        runtime.NextBurstTimer = math.max(0f, runtime.NextBurstTimer - deltaTime);
        runtime.NextBombInBurstTimer = math.max(0f, runtime.NextBombInBurstTimer - deltaTime);
        runtime.PostLaunchStopTimer = math.max(0f, runtime.PostLaunchStopTimer - deltaTime);
    }

    /// <summary>
    /// Clears any currently committed burst when targeting or gates are no longer valid.
    /// </summary>
    /// <param name="runtime">Mutable Bombardier runtime state.</param>
    internal static void CancelActiveBurst(ref EnemyBombardierRuntimeElement runtime)
    {
        runtime.RemainingBurstLaunches = 0;
        runtime.LaunchesCompletedInCurrentBurst = 0;
        runtime.BurstWindupDurationSeconds = 0f;
        runtime.NextBombInBurstTimer = 0f;
        runtime.PostLaunchStopTimer = 0f;
        runtime.HasLockedTargetPosition = 0;
    }

    /// <summary>
    /// Starts a new Bombardier burst and optionally locks the first target position.
    /// </summary>
    /// <param name="runtime">Mutable Bombardier runtime state.</param>
    /// <param name="bombardierConfig">Bombardier config that controls cadence and aim policy.</param>
    /// <param name="targetingMode">Resolved targeting mode for the current reach state.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="random">Mutable random state used for random target modes.</param>
    internal static void StartBurst(ref EnemyBombardierRuntimeElement runtime,
                                    in EnemyBombardierConfigElement bombardierConfig,
                                    EnemyBombardierTargetingMode targetingMode,
                                    float3 enemyPosition,
                                    float3 playerPosition,
                                    ref Random random)
    {
        runtime.RemainingBurstLaunches = math.max(1, bombardierConfig.BurstCount);
        runtime.LaunchesCompletedInCurrentBurst = 0;
        runtime.BurstWindupDurationSeconds = ResolveBurstWindupDuration(in bombardierConfig);
        runtime.NextBombInBurstTimer = runtime.BurstWindupDurationSeconds;
        runtime.NextBurstTimer = math.max(0.01f, bombardierConfig.FireInterval);

        if (bombardierConfig.AimPolicy != EnemyShooterAimPolicy.LockOnFireStart)
            return;

        runtime.LockedTargetPosition = ResolveTargetPosition(targetingMode,
                                                             enemyPosition,
                                                             playerPosition,
                                                             in bombardierConfig,
                                                             ref random);
        runtime.HasLockedTargetPosition = 1;
    }

    /// <summary>
    /// Advances burst counters after one launch group was emitted.
    /// </summary>
    /// <param name="runtime">Mutable Bombardier runtime state.</param>
    /// <param name="bombardierConfig">Bombardier config that controls intra-burst delay and post-launch stop.</param>
    internal static void CompleteLaunch(ref EnemyBombardierRuntimeElement runtime,
                                        in EnemyBombardierConfigElement bombardierConfig)
    {
        runtime.RemainingBurstLaunches -= 1;
        runtime.LaunchesCompletedInCurrentBurst += 1;

        if (runtime.RemainingBurstLaunches > 0)
        {
            runtime.NextBombInBurstTimer = math.max(0f, bombardierConfig.IntraBurstDelay);
            return;
        }

        runtime.NextBombInBurstTimer = 0f;
        runtime.LaunchesCompletedInCurrentBurst = 0;
        runtime.BurstWindupDurationSeconds = 0f;
        runtime.PostLaunchStopTimer = ResolvePostLaunchStopDuration(in bombardierConfig);
        runtime.HasLockedTargetPosition = 0;
    }
    #endregion

    #region Targeting
    /// <summary>
    /// Resolves whether the player is inside Bombardier reach using only distance gates.
    /// </summary>
    /// <param name="playerDistance">Current planar player distance.</param>
    /// <param name="bombardierConfig">Bombardier config containing distance gates.</param>
    /// <returns>True when distance gates classify the player as in reach.</returns>
    internal static bool IsPlayerInReach(float playerDistance, in EnemyBombardierConfigElement bombardierConfig)
    {
        if (bombardierConfig.UseMinimumRange != 0 && playerDistance < math.max(0f, bombardierConfig.MinimumRange))
            return false;

        if (bombardierConfig.UseMaximumRange != 0 && playerDistance > math.max(0f, bombardierConfig.MaximumRange))
            return false;

        return true;
    }

    /// <summary>
    /// Resolves the targeting mode to use for the current reach state.
    /// </summary>
    /// <param name="bombardierConfig">Bombardier config containing in-reach and out-of-reach targeting modes.</param>
    /// <param name="runtime">Current Bombardier runtime state.</param>
    /// <returns>Targeting mode selected for this frame.</returns>
    internal static EnemyBombardierTargetingMode ResolveRuntimeTargetingMode(in EnemyBombardierConfigElement bombardierConfig,
                                                                             in EnemyBombardierRuntimeElement runtime)
    {
        return runtime.IsPlayerInReach != 0
            ? bombardierConfig.InReachTargetingMode
            : bombardierConfig.OutOfReachTargetingMode;
    }

    /// <summary>
    /// Resolves the active target position for the current launch group.
    /// </summary>
    /// <param name="runtime">Current Bombardier runtime state.</param>
    /// <param name="bombardierConfig">Bombardier config used by random target modes.</param>
    /// <param name="targetingMode">Resolved target mode for the current reach state.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="random">Mutable random state used for random target modes.</param>
    /// <returns>World-space landing target position.</returns>
    internal static float3 ResolveCurrentTargetPosition(in EnemyBombardierRuntimeElement runtime,
                                                        in EnemyBombardierConfigElement bombardierConfig,
                                                        EnemyBombardierTargetingMode targetingMode,
                                                        float3 enemyPosition,
                                                        float3 playerPosition,
                                                        ref Random random)
    {
        if (runtime.HasLockedTargetPosition != 0)
            return runtime.LockedTargetPosition;

        return ResolveTargetPosition(targetingMode,
                                     enemyPosition,
                                     playerPosition,
                                     in bombardierConfig,
                                     ref random);
    }

    /// <summary>
    /// Resolves one target point for a Bombardier target mode.
    /// </summary>
    /// <param name="targetingMode">Targeting mode to evaluate.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="bombardierConfig">Bombardier config containing random distance limits.</param>
    /// <param name="random">Mutable random state used for random target modes.</param>
    /// <returns>World-space target position.</returns>
    internal static float3 ResolveTargetPosition(EnemyBombardierTargetingMode targetingMode,
                                                 float3 enemyPosition,
                                                 float3 playerPosition,
                                                 in EnemyBombardierConfigElement bombardierConfig,
                                                 ref Random random)
    {
        switch (targetingMode)
        {
            case EnemyBombardierTargetingMode.RandomAroundEnemy:
                return enemyPosition + ResolveRandomPlanarOffset(in bombardierConfig, ref random);

            case EnemyBombardierTargetingMode.RandomAroundPlayer:
                return playerPosition + ResolveRandomPlanarOffset(in bombardierConfig, ref random);

            case EnemyBombardierTargetingMode.Player:
                return playerPosition;

            default:
                return enemyPosition;
        }
    }

    /// <summary>
    /// Resolves a random planar offset using authored minimum and maximum random distances.
    /// </summary>
    /// <param name="bombardierConfig">Bombardier config containing random distance limits.</param>
    /// <param name="random">Mutable random state.</param>
    /// <returns>Random planar offset.</returns>
    private static float3 ResolveRandomPlanarOffset(in EnemyBombardierConfigElement bombardierConfig, ref Random random)
    {
        float minimumDistance = math.max(0f, bombardierConfig.RandomMinimumDistance);
        float maximumDistance = math.max(minimumDistance, bombardierConfig.RandomMaximumDistance);
        float angle = random.NextFloat(0f, math.PI * 2f);
        float distance = random.NextFloat(minimumDistance, maximumDistance);
        return new float3(math.cos(angle) * distance, 0f, math.sin(angle) * distance);
    }
    #endregion

    #region Launch Requests
    /// <summary>
    /// Enqueues every bomb request required by one committed launch group.
    /// </summary>
    /// <param name="launchRequests">Mutable request buffer receiving bomb launches.</param>
    /// <param name="ownerEntity">Enemy entity that owns the launch.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="targetPosition">Resolved base landing target position.</param>
    /// <param name="bombardierConfig">Bombardier config containing launch pattern and damage settings.</param>
    /// <param name="random">Mutable random state used by scatter patterns.</param>
    internal static void EnqueueLaunchRequests(DynamicBuffer<EnemyBombardierLaunchRequest> launchRequests,
                                               Entity ownerEntity,
                                               float3 enemyPosition,
                                               float3 targetPosition,
                                               in EnemyBombardierConfigElement bombardierConfig,
                                               ref Random random)
    {
        switch (bombardierConfig.LaunchPattern)
        {
            case EnemyBombardierLaunchPattern.Radial:
                EnqueueRadialLaunchRequests(launchRequests,
                                            ownerEntity,
                                            enemyPosition,
                                            targetPosition,
                                            in bombardierConfig);
                return;

            default:
                EnqueueClusterLaunchRequests(launchRequests,
                                             ownerEntity,
                                             enemyPosition,
                                             targetPosition,
                                             in bombardierConfig,
                                             ref random);
                return;
        }
    }

    /// <summary>
    /// Enqueues cluster launch requests around the target point.
    /// </summary>
    /// <param name="launchRequests">Mutable request buffer receiving bomb launches.</param>
    /// <param name="ownerEntity">Enemy entity that owns the launch.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="targetPosition">Resolved base landing target position.</param>
    /// <param name="bombardierConfig">Bombardier config containing cluster settings.</param>
    /// <param name="random">Mutable random state used by scatter.</param>
    private static void EnqueueClusterLaunchRequests(DynamicBuffer<EnemyBombardierLaunchRequest> launchRequests,
                                                     Entity ownerEntity,
                                                     float3 enemyPosition,
                                                     float3 targetPosition,
                                                     in EnemyBombardierConfigElement bombardierConfig,
                                                     ref Random random)
    {
        int bombsPerLaunch = math.max(1, bombardierConfig.BombsPerLaunch);
        float spreadRadius = math.max(0f, bombardierConfig.LandingSpreadRadius);

        for (int bombIndex = 0; bombIndex < bombsPerLaunch; bombIndex++)
        {
            float3 landingPosition = targetPosition;

            if (bombsPerLaunch > 1 && spreadRadius > 0f)
                landingPosition += ResolveRandomDiscOffset(spreadRadius, ref random);

            AddLaunchRequest(launchRequests, ownerEntity, enemyPosition, landingPosition, in bombardierConfig);
        }
    }

    /// <summary>
    /// Enqueues radial launch requests around the target point.
    /// </summary>
    /// <param name="launchRequests">Mutable request buffer receiving bomb launches.</param>
    /// <param name="ownerEntity">Enemy entity that owns the launch.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="targetPosition">Resolved base landing target position.</param>
    /// <param name="bombardierConfig">Bombardier config containing radial settings.</param>
    private static void EnqueueRadialLaunchRequests(DynamicBuffer<EnemyBombardierLaunchRequest> launchRequests,
                                                    Entity ownerEntity,
                                                    float3 enemyPosition,
                                                    float3 targetPosition,
                                                    in EnemyBombardierConfigElement bombardierConfig)
    {
        int bombsPerLaunch = math.max(1, bombardierConfig.BombsPerLaunch);
        float radius = math.max(0f, bombardierConfig.RadialPatternRadius);

        if (bombsPerLaunch <= 1 || radius <= 0f)
        {
            AddLaunchRequest(launchRequests, ownerEntity, enemyPosition, targetPosition, in bombardierConfig);
            return;
        }

        float angleStep = math.PI * 2f / bombsPerLaunch;
        float3 baseDirection = targetPosition - enemyPosition;
        baseDirection.y = 0f;
        float baseAngle = math.atan2(baseDirection.z, baseDirection.x);

        for (int bombIndex = 0; bombIndex < bombsPerLaunch; bombIndex++)
        {
            float angle = baseAngle + angleStep * bombIndex;
            float3 offset = new float3(math.cos(angle) * radius, 0f, math.sin(angle) * radius);
            AddLaunchRequest(launchRequests, ownerEntity, enemyPosition, targetPosition + offset, in bombardierConfig);
        }
    }

    /// <summary>
    /// Adds one sanitized Bombardier launch request to the buffer.
    /// </summary>
    /// <param name="launchRequests">Mutable request buffer receiving bomb launches.</param>
    /// <param name="ownerEntity">Enemy entity that owns the launch.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="landingPosition">Target landing position.</param>
    /// <param name="bombardierConfig">Bombardier config copied into the request.</param>
    private static void AddLaunchRequest(DynamicBuffer<EnemyBombardierLaunchRequest> launchRequests,
                                         Entity ownerEntity,
                                         float3 enemyPosition,
                                         float3 landingPosition,
                                         in EnemyBombardierConfigElement bombardierConfig)
    {
        float3 launchPosition = enemyPosition;
        launchPosition.y += bombardierConfig.LaunchHeightOffset;
        landingPosition.y += bombardierConfig.LandingHeightOffset;
        float damageRadius = math.max(0f, bombardierConfig.DamageRadius);

        launchRequests.Add(new EnemyBombardierLaunchRequest
        {
            OwnerEntity = ownerEntity,
            LaunchPosition = launchPosition,
            LandingPosition = landingPosition,
            TrajectoryMode = bombardierConfig.TrajectoryMode,
            FlightDurationSeconds = math.max(0.05f, bombardierConfig.FlightDurationSeconds),
            Gravity = math.max(0.01f, bombardierConfig.Gravity),
            ApexHeight = math.max(0.05f, bombardierConfig.ApexHeight),
            Damage = math.max(0f, bombardierConfig.Damage),
            DamageRadius = damageRadius,
            ImpactExplosionDelaySeconds = math.max(0f, bombardierConfig.ImpactExplosionDelaySeconds),
            BombScaleMultiplier = math.max(0.01f, bombardierConfig.BombScaleMultiplier),
            EnableLandingWarning = bombardierConfig.EnableLandingWarning,
            WarningLeadTimeSeconds = math.max(0f, bombardierConfig.WarningLeadTimeSeconds),
            WarningRadius = damageRadius * math.max(0f, bombardierConfig.WarningRadiusScale),
            WarningRingWidth = math.max(0f, bombardierConfig.WarningRingWidth),
            WarningHeightOffset = bombardierConfig.WarningHeightOffset,
            WarningMaximumAlpha = math.saturate(bombardierConfig.WarningMaximumAlpha),
            WarningFadeOutSeconds = math.max(0f, bombardierConfig.WarningFadeOutSeconds),
            WarningColor = bombardierConfig.WarningColor
        });
    }

    /// <summary>
    /// Resolves one random offset inside a disc.
    /// </summary>
    /// <param name="radius">Disc radius.</param>
    /// <param name="random">Mutable random state.</param>
    /// <returns>Random planar offset.</returns>
    private static float3 ResolveRandomDiscOffset(float radius, ref Random random)
    {
        float angle = random.NextFloat(0f, math.PI * 2f);
        float distance = math.sqrt(random.NextFloat(0f, 1f)) * math.max(0f, radius);
        return new float3(math.cos(angle) * distance, 0f, math.sin(angle) * distance);
    }
    #endregion

    #region Gates And Movement
    /// <summary>
    /// Resolves whether optional non-range activation gates allow Bombardier launch attempts.
    /// </summary>
    /// <param name="bombardierConfig">Bombardier config containing gate flags.</param>
    /// <param name="enemyRuntimeState">Enemy runtime state used for speed and damage checks.</param>
    /// <param name="patternRuntimeState">Pattern runtime state used for Wanderer wait checks.</param>
    /// <returns>True when every configured gate is satisfied.</returns>
    internal static bool AreActivationGatesValid(in EnemyBombardierConfigElement bombardierConfig,
                                                 in EnemyRuntimeState enemyRuntimeState,
                                                 in EnemyPatternRuntimeState patternRuntimeState)
    {
        EnemyWeaponInteractionActivationGate gates = bombardierConfig.ActivationGates;

        if (gates == EnemyWeaponInteractionActivationGate.Always)
            return true;

        if ((gates & EnemyWeaponInteractionActivationGate.RequireBelowSpeed) != 0)
        {
            float3 planarVelocity = enemyRuntimeState.Velocity;
            planarVelocity.y = 0f;

            if (math.length(planarVelocity) > math.max(0f, bombardierConfig.MaximumActivationSpeed))
                return false;
        }

        if ((gates & EnemyWeaponInteractionActivationGate.RequireRecentlyDamaged) != 0)
        {
            float damageAge = enemyRuntimeState.LifetimeSeconds - enemyRuntimeState.LastDamageLifetimeSeconds;

            if (enemyRuntimeState.HasTakenDamage == 0 ||
                damageAge > math.max(0f, bombardierConfig.RecentlyDamagedWindowSeconds))
            {
                return false;
            }
        }

        if ((gates & EnemyWeaponInteractionActivationGate.RequireWandererWait) != 0 &&
            patternRuntimeState.WanderWaitTimer <= 0f)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves whether one Bombardier module currently requires movement to remain stopped.
    /// </summary>
    /// <param name="bombardierConfig">Bombardier configuration that declares the movement policy.</param>
    /// <param name="runtime">Mutable Bombardier timing state for active and post-launch phases.</param>
    /// <returns>True when the enemy should hold position for this module.</returns>
    internal static bool ShouldLockMovement(in EnemyBombardierConfigElement bombardierConfig,
                                            in EnemyBombardierRuntimeElement runtime)
    {
        if (bombardierConfig.MovementPolicy != EnemyShooterMovementPolicy.StopWhileAiming)
            return false;

        return runtime.RemainingBurstLaunches > 0 || runtime.PostLaunchStopTimer > 0f;
    }

    /// <summary>
    /// Resolves the first-launch delay for a burst, including stop-before-launch timing only when movement locking is enabled.
    /// </summary>
    /// <param name="bombardierConfig">Bombardier config containing windup and stop timing values.</param>
    /// <returns>Seconds to wait before the first launch in the burst.</returns>
    private static float ResolveBurstWindupDuration(in EnemyBombardierConfigElement bombardierConfig)
    {
        float aimWindupSeconds = math.max(0f, bombardierConfig.AimWindupSeconds);

        if (bombardierConfig.MovementPolicy != EnemyShooterMovementPolicy.StopWhileAiming)
            return aimWindupSeconds;

        return math.max(aimWindupSeconds, math.max(0f, bombardierConfig.PreLaunchStopSeconds));
    }

    /// <summary>
    /// Resolves post-launch stop timing only for Bombardier modules that explicitly lock movement while aiming.
    /// </summary>
    /// <param name="bombardierConfig">Bombardier config containing movement policy and post-launch stop timing.</param>
    /// <returns>Seconds to keep movement locked after the final launch.</returns>
    private static float ResolvePostLaunchStopDuration(in EnemyBombardierConfigElement bombardierConfig)
    {
        if (bombardierConfig.MovementPolicy != EnemyShooterMovementPolicy.StopWhileAiming)
            return 0f;

        return math.max(0f, bombardierConfig.PostLaunchStopSeconds);
    }
    #endregion

    #region Aim
    /// <summary>
    /// Resolves the aim direction used by look control and engagement presentation.
    /// </summary>
    /// <param name="runtime">Current Bombardier runtime state.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="enemyRotation">Current enemy rotation used as fallback.</param>
    /// <param name="aimDirection">Resolved planar aim direction.</param>
    /// <returns>True when a usable direction was resolved.</returns>
    internal static bool TryResolveAimDirection(in EnemyBombardierRuntimeElement runtime,
                                                float3 enemyPosition,
                                                float3 playerPosition,
                                                quaternion enemyRotation,
                                                out float3 aimDirection)
    {
        float3 targetPosition = runtime.HasLockedTargetPosition != 0
            ? runtime.LockedTargetPosition
            : playerPosition;
        aimDirection = targetPosition - enemyPosition;
        aimDirection.y = 0f;

        if (math.lengthsq(aimDirection) <= DirectionEpsilon)
            aimDirection = ResolveForward(enemyRotation);

        aimDirection = math.normalizesafe(aimDirection, ForwardAxis);
        return math.lengthsq(aimDirection) > DirectionEpsilon;
    }

    /// <summary>
    /// Resolves the planar forward direction from one rotation.
    /// </summary>
    /// <param name="rotation">Rotation to inspect.</param>
    /// <returns>Normalized planar forward direction.</returns>
    private static float3 ResolveForward(quaternion rotation)
    {
        float3 forward = math.forward(rotation);
        forward.y = 0f;
        return math.normalizesafe(forward, ForwardAxis);
    }

    /// <summary>
    /// Captures the best current aim direction used to orient enemy visuals before bomb launch.
    /// </summary>
    /// <param name="candidateDirection">Current Bombardier aim direction candidate.</param>
    /// <param name="movementPolicy">Movement policy associated with the current Bombardier module.</param>
    /// <param name="exclusiveLookDirectionControl">Whether the current module must override any movement-facing fallback.</param>
    /// <param name="resolvedAimDirection">Best resolved aim direction retained across modules.</param>
    /// <param name="hasResolvedAimDirection">Whether a valid aim direction has already been captured.</param>
    /// <param name="aimPriority">Priority of the currently captured aim direction.</param>
    internal static void TryCaptureAimDirection(float3 candidateDirection,
                                                EnemyShooterMovementPolicy movementPolicy,
                                                bool exclusiveLookDirectionControl,
                                                ref float3 resolvedAimDirection,
                                                ref bool hasResolvedAimDirection,
                                                ref int aimPriority)
    {
        if (math.lengthsq(candidateDirection) <= DirectionEpsilon)
            return;

        int candidatePriority = exclusiveLookDirectionControl
            ? 3
            : movementPolicy == EnemyShooterMovementPolicy.StopWhileAiming ? 2 : 1;

        if (hasResolvedAimDirection && candidatePriority < aimPriority)
            return;

        resolvedAimDirection = math.normalizesafe(candidateDirection, ForwardAxis);
        hasResolvedAimDirection = true;
        aimPriority = candidatePriority;
    }
    #endregion

    #region Random
    /// <summary>
    /// Creates a mutable random wrapper from the runtime state, repairing zero seeds.
    /// </summary>
    /// <param name="runtime">Runtime state that stores the persistent random state.</param>
    /// <param name="enemyEntity">Enemy entity used to repair missing seeds.</param>
    /// <param name="moduleIndex">Bombardier module index used to repair missing seeds.</param>
    /// <returns>Mutable random state ready for sampling.</returns>
    internal static Random CreateRandom(ref EnemyBombardierRuntimeElement runtime, Entity enemyEntity, int moduleIndex)
    {
        if (runtime.RandomState == 0u)
            runtime.RandomState = ResolveSeed(enemyEntity, moduleIndex);

        return new Random(runtime.RandomState);
    }

    /// <summary>
    /// Resolves a non-zero random seed for one enemy module instance.
    /// </summary>
    /// <param name="enemyEntity">Enemy entity used for deterministic seed derivation.</param>
    /// <param name="moduleIndex">Bombardier module index inside the active buffer.</param>
    /// <returns>Non-zero random seed.</returns>
    internal static uint ResolveSeed(Entity enemyEntity, int moduleIndex)
    {
        uint seed = (uint)(enemyEntity.Index + 1) * RandomSeedMultiplier;
        seed ^= (uint)(enemyEntity.Version + 1) * 2891336453u;
        seed ^= (uint)(moduleIndex + 1) * 277803737u;

        if (seed == 0u)
            return 1u;

        return seed;
    }
    #endregion

    #endregion
}
