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
    /// Registers one endpoint-stationary projectile so its active slot can reject invalid early recall taps without charging a cost.
    /// </summary>
    /// <param name="shooterEntity">Player entity that owns the active projectile.</param>
    /// <param name="returnState">Mutable projectile state entering activation-recall waiting.</param>
    /// <param name="powerUpsStateLookup">Mutable player state lookup receiving the ready count.</param>
    public static void RegisterReady(Entity shooterEntity,
                                     ref ProjectileReturnState returnState,
                                     ref ComponentLookup<PlayerPowerUpsState> powerUpsStateLookup)
    {
        if (returnState.Config.ReturnStartMode != ProjectileReturnStartMode.ActivationTap ||
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
            returnState.Config.ReturnStartMode != ProjectileReturnStartMode.ActivationTap ||
            returnState.ConcurrencyRegistered == 0 ||
            !powerUpsStateLookup.HasComponent(owner.ShooterEntity))
        {
            return false;
        }

        PlayerPowerUpsState powerUpsState = powerUpsStateLookup[owner.ShooterEntity];
        uint recallVersion = ResolveRecallVersion(in returnState, in powerUpsState);

        if (recallVersion == returnState.LastObservedActivationRecallVersion)
            return false;

        returnState.LastObservedActivationRecallVersion = recallVersion;

        switch (returnState.Phase)
        {
            case ProjectileReturnPhase.Outbound:
                if (returnState.Config.AllowEarlyActivationRecall == 0)
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

            if (returnState.ActivationRecallReadyRegistered != 0)
                powerUpsState.PrimaryReturningProjectileRecallReadyCount = math.max(0, powerUpsState.PrimaryReturningProjectileRecallReadyCount - 1);
        }
        else if (returnState.ConcurrencyGeneration == powerUpsState.SecondaryReturningProjectileGeneration)
        {
            powerUpsState.SecondaryReturningProjectileCount = math.max(0, powerUpsState.SecondaryReturningProjectileCount - 1);

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
    private static uint ResolveRecallVersion(in ProjectileReturnState returnState,
                                             in PlayerPowerUpsState powerUpsState)
    {
        if (returnState.ConcurrencyGeneration == powerUpsState.PrimaryReturningProjectileGeneration)
            return powerUpsState.PrimaryReturningProjectileRecallVersion;

        if (returnState.ConcurrencyGeneration == powerUpsState.SecondaryReturningProjectileGeneration)
            return powerUpsState.SecondaryReturningProjectileRecallVersion;

        return returnState.LastObservedActivationRecallVersion;
    }
    #endregion

    #endregion
}
