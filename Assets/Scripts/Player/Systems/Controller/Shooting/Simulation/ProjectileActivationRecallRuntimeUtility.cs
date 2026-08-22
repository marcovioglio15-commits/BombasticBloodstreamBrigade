using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Tracks active-slot ownership, endpoint readiness, and versioned input recalls for returning projectiles.
/// </summary>
public static class ProjectileActivationRecallRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies one pending stolen-ownership command and keeps a reconnected projectile's active slot index current.
    /// </summary>
    /// <param name="returnState">Mutable projectile return state carrying its ownership generation.</param>
    /// <param name="projectile">Mutable projectile stopped while ownership is suspended or invalidated.</param>
    /// <param name="powerUpsState">Owner state carrying stolen generation commands and current slot generations.</param>
    /// <returns>True when ordinary projectile simulation must stop for the current frame.</returns>
    public static bool ApplyStolenOwnershipPolicy(ref ProjectileReturnState returnState,
                                                  ref Projectile projectile,
                                                  in PlayerPowerUpsState powerUpsState)
    {
        if (returnState.ConcurrencyRegistered == 0)
            return false;

        if (returnState.ConcurrencyGeneration == powerUpsState.PrimaryReturningProjectileGeneration)
            returnState.ActiveSlotIndex = 0;
        else if (returnState.ConcurrencyGeneration == powerUpsState.SecondaryReturningProjectileGeneration)
            returnState.ActiveSlotIndex = 1;

        ProjectileStolenOwnershipPolicy policy;

        if (returnState.ConcurrencyGeneration == powerUpsState.PrimaryStolenReturningProjectileGeneration)
            policy = powerUpsState.PrimaryStolenReturningProjectilePolicy;
        else if (returnState.ConcurrencyGeneration == powerUpsState.SecondaryStolenReturningProjectileGeneration)
            policy = powerUpsState.SecondaryStolenReturningProjectilePolicy;
        else
            return false;

        projectile.Velocity = float3.zero;

        if (policy == ProjectileStolenOwnershipPolicy.Despawn)
            returnState.Phase = ProjectileReturnPhase.Completed;

        return true;
    }

    /// <summary>
    /// Registers one endpoint-stationary projectile so its active slot can reject invalid early recall taps without charging a cost.
    /// </summary>
    /// <param name="shooterEntity">Player entity that owns the active projectile.</param>
    /// <param name="returnState">Mutable projectile state entering activation-recall waiting.</param>
    /// <param name="powerUpsStateLookup">Mutable player state lookup receiving the ready count.</param>
    public static void RegisterReady(Entity shooterEntity,
                                     ref ProjectileReturnState returnState,
                                     ref ComponentLookup<PlayerPowerUpsState> powerUpsStateLookup)
    {
        if (!ProjectileReturnStartModeUtility.UsesActivationTap(returnState.Config.ReturnStartMode) ||
            returnState.Phase != ProjectileReturnPhase.Delaying ||
            returnState.ConcurrencyRegistered == 0 ||
            returnState.ActivationRecallReadyRegistered != 0 ||
            !powerUpsStateLookup.HasComponent(shooterEntity))
        {
            return;
        }

        PlayerPowerUpsState powerUpsState = powerUpsStateLookup[shooterEntity];

        if (returnState.ConcurrencyGeneration == powerUpsState.PrimaryReturningProjectileGeneration)
            powerUpsState.PrimaryReturningProjectileRecallReadyCount++;
        else if (returnState.ConcurrencyGeneration == powerUpsState.SecondaryReturningProjectileGeneration)
            powerUpsState.SecondaryReturningProjectileRecallReadyCount++;
        else
            return;

        returnState.ActivationRecallReadyRegistered = 1;
        powerUpsStateLookup[shooterEntity] = powerUpsState;
    }

    /// <summary>
    /// Consumes a new recall version for one owned projectile and starts return immediately when its phase is eligible.
    /// </summary>
    /// <param name="returnState">Mutable projectile return state observing its active slot recall version.</param>
    /// <param name="projectile">Mutable projectile behavior stopped or redirected by the transition.</param>
    /// <param name="perfectCircleState">Mutable orbital trajectory state disabled when recall starts.</param>
    /// <param name="projectileTransform">Mutable projectile transform receiving endpoint and scale transition data.</param>
    /// <param name="returnPath">Mutable recorded outbound path receiving the early recall endpoint.</param>
    /// <param name="owner">Projectile owner used to resolve active slot state.</param>
    /// <param name="powerUpsStateLookup">Read-only player state lookup carrying recall versions.</param>
    /// <returns>True when this request started or released return travel.</returns>
    public static bool TryConsume(ref ProjectileReturnState returnState,
                                  ref Projectile projectile,
                                  ref ProjectilePerfectCircleState perfectCircleState,
                                  ref LocalTransform projectileTransform,
                                  DynamicBuffer<ProjectileReturnPathPoint> returnPath,
                                  in ProjectileOwner owner,
                                  in ComponentLookup<PlayerPowerUpsState> powerUpsStateLookup)
    {
        if (returnState.Enabled == 0 ||
            (!ProjectileReturnStartModeUtility.UsesActivationTap(returnState.Config.ReturnStartMode) &&
             !ProjectileReturnStartModeUtility.UsesResourceDrain(returnState.Config.ReturnStartMode)) ||
            returnState.ConcurrencyRegistered == 0 ||
            !powerUpsStateLookup.HasComponent(owner.ShooterEntity))
        {
            return false;
        }

        PlayerPowerUpsState powerUpsState = powerUpsStateLookup[owner.ShooterEntity];
        uint activationRecallVersion = ResolveActivationRecallVersion(in returnState, in powerUpsState);
        uint resourceRecallVersion = ResolveResourceRecallVersion(in returnState, in powerUpsState);
        bool activationRecallRequested = activationRecallVersion != returnState.LastObservedActivationRecallVersion;
        bool resourceRecallRequested = resourceRecallVersion != returnState.LastObservedResourceRecallVersion;

        if (!activationRecallRequested && !resourceRecallRequested)
            return false;

        returnState.LastObservedActivationRecallVersion = activationRecallVersion;
        returnState.LastObservedResourceRecallVersion = resourceRecallVersion;

        switch (returnState.Phase)
        {
            case ProjectileReturnPhase.Outbound:
                if (!resourceRecallRequested && returnState.Config.AllowEarlyActivationRecall == 0)
                    return false;

                ProjectileReturnRuntimeUtility.BeginReturn(ref returnState,
                                                            ref projectile,
                                                            ref perfectCircleState,
                                                            ref projectileTransform,
                                                            returnPath,
                                                            false,
                                                            true);
                return true;
            case ProjectileReturnPhase.Delaying:
                returnState.ActivationRecallReadyRegistered = 0;
                returnState.Phase = ProjectileReturnRuntimeUtility.ResolvePostDelayPhase(in returnState.Config);

                if (returnState.Phase == ProjectileReturnPhase.Returning)
                    ProjectileReturnRuntimeUtility.MarkReturnTravelStarted(ref returnState);

                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Releases active-slot live ownership and endpoint readiness exactly once before final pooling.
    /// </summary>
    /// <param name="shooterEntity">Shooter that owns the active slot.</param>
    /// <param name="returnState">Mutable return state whose ownership registration is cleared.</param>
    /// <param name="powerUpsStateLookup">Mutable player power-up state lookup.</param>
    public static void ReleaseOwnership(Entity shooterEntity,
                                        ref ProjectileReturnState returnState,
                                        ref ComponentLookup<PlayerPowerUpsState> powerUpsStateLookup)
    {
        if (returnState.ConcurrencyRegistered == 0 || !powerUpsStateLookup.HasComponent(shooterEntity))
            return;

        PlayerPowerUpsState powerUpsState = powerUpsStateLookup[shooterEntity];

        if (returnState.ConcurrencyGeneration == powerUpsState.PrimaryReturningProjectileGeneration)
        {
            powerUpsState.PrimaryReturningProjectileCount = math.max(0, powerUpsState.PrimaryReturningProjectileCount - 1);

            if (powerUpsState.PrimaryReturningProjectileCount == 0)
            {
                powerUpsState.PrimaryReturningProjectileResourceDrainActive = 0;
                powerUpsState.PrimaryReturningProjectileReconnectPending = 0;
            }

            if (returnState.ActivationRecallReadyRegistered != 0)
                powerUpsState.PrimaryReturningProjectileRecallReadyCount = math.max(0, powerUpsState.PrimaryReturningProjectileRecallReadyCount - 1);
        }
        else if (returnState.ConcurrencyGeneration == powerUpsState.SecondaryReturningProjectileGeneration)
        {
            powerUpsState.SecondaryReturningProjectileCount = math.max(0, powerUpsState.SecondaryReturningProjectileCount - 1);

            if (powerUpsState.SecondaryReturningProjectileCount == 0)
            {
                powerUpsState.SecondaryReturningProjectileResourceDrainActive = 0;
                powerUpsState.SecondaryReturningProjectileReconnectPending = 0;
            }

            if (returnState.ActivationRecallReadyRegistered != 0)
                powerUpsState.SecondaryReturningProjectileRecallReadyCount = math.max(0, powerUpsState.SecondaryReturningProjectileRecallReadyCount - 1);
        }

        powerUpsStateLookup[shooterEntity] = powerUpsState;
        returnState.ConcurrencyRegistered = 0;
        returnState.ActivationRecallReadyRegistered = 0;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the recall version owned by the active slot generation registered on one projectile.
    /// </summary>
    /// <param name="returnState">Projectile state carrying its active ownership generation.</param>
    /// <param name="powerUpsState">Owner state carrying primary and secondary recall versions.</param>
    /// <returns>Matching slot recall version, or the already observed value when ownership is stale.</returns>
    private static uint ResolveActivationRecallVersion(in ProjectileReturnState returnState,
                                                       in PlayerPowerUpsState powerUpsState)
    {
        if (returnState.ConcurrencyGeneration == powerUpsState.PrimaryReturningProjectileGeneration)
            return powerUpsState.PrimaryReturningProjectileRecallVersion;

        if (returnState.ConcurrencyGeneration == powerUpsState.SecondaryReturningProjectileGeneration)
            return powerUpsState.SecondaryReturningProjectileRecallVersion;

        return returnState.LastObservedActivationRecallVersion;
    }

    /// <summary>
    /// Resolves the resource recall version owned by the active slot generation registered on one projectile.
    /// </summary>
    /// <param name="returnState">Projectile state carrying its active ownership generation.</param>
    /// <param name="powerUpsState">Owner state carrying primary and secondary resource recall versions.</param>
    /// <returns>Matching resource recall version, or the already observed value when ownership is stale.</returns>
    private static uint ResolveResourceRecallVersion(in ProjectileReturnState returnState,
                                                     in PlayerPowerUpsState powerUpsState)
    {
        if (returnState.ConcurrencyGeneration == powerUpsState.PrimaryReturningProjectileGeneration)
            return powerUpsState.PrimaryReturningProjectileResourceRecallVersion;

        if (returnState.ConcurrencyGeneration == powerUpsState.SecondaryReturningProjectileGeneration)
            return powerUpsState.SecondaryReturningProjectileResourceRecallVersion;

        return returnState.LastObservedResourceRecallVersion;
    }
    #endregion

    #endregion
}
