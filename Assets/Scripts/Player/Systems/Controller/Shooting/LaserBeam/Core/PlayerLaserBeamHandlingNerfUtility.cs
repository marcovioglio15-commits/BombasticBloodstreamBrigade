using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Resolves Laser Beam player-handling multipliers for movement and look systems without duplicating activation checks.
/// </summary>
internal static class PlayerLaserBeamHandlingNerfUtility
{
    #region Constants
    private const float ShootInputThreshold = 0.5f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves movement and rotation multipliers when the current Laser Beam configuration is firing and handling nerf is enabled.
    /// </summary>
    /// <param name="playerEntity">Player entity being evaluated by movement or look systems.</param>
    /// <param name="inputStateLookup">Read-only input lookup used to detect held Shoot input.</param>
    /// <param name="powerUpsStateLookup">Read-only power-up state lookup used to respect shooting suppression.</param>
    /// <param name="passiveToolsStateLookup">Read-only passive tool state buffer lookup containing the always-on Laser Beam config.</param>
    /// <param name="laserBeamStateLookup">Read-only Laser Beam state lookup containing triggered active snapshots and transient flags.</param>
    /// <param name="moveSpeedMultiplier">Resolved movement speed multiplier. Defaults to 1 when no nerf applies.</param>
    /// <param name="rotationSpeedMultiplier">Resolved look rotation speed multiplier. Defaults to 1 when no nerf applies.</param>
    /// <returns>True when an enabled Laser Beam handling nerf should affect the current frame.</returns>
    public static bool TryResolveFiringHandlingMultipliers(Entity playerEntity,
                                                           in ComponentLookup<PlayerInputState> inputStateLookup,
                                                           in ComponentLookup<PlayerPowerUpsState> powerUpsStateLookup,
                                                           in BufferLookup<PlayerPassiveToolsStateElement> passiveToolsStateLookup,
                                                           in ComponentLookup<PlayerLaserBeamState> laserBeamStateLookup,
                                                           out float moveSpeedMultiplier,
                                                           out float rotationSpeedMultiplier)
    {
        moveSpeedMultiplier = 1f;
        rotationSpeedMultiplier = 1f;

        if (!passiveToolsStateLookup.HasBuffer(playerEntity) ||
            !laserBeamStateLookup.HasComponent(playerEntity))
            return false;

        PlayerLaserBeamState laserBeamState = laserBeamStateLookup[playerEntity];
        PlayerPassiveToolsState passiveToolsState;
        PlayerPassiveToolsStateBufferUtility.Read(playerEntity,
                                                  in passiveToolsStateLookup,
                                                  out passiveToolsState);
        PlayerPassiveToolsState effectivePassiveToolsState;
        PlayerLaserBeamStateUtility.ResolveEffectivePassiveToolsState(in passiveToolsState,
                                                                      in laserBeamState,
                                                                      out effectivePassiveToolsState);

        if (effectivePassiveToolsState.HasLaserBeam == 0)
            return false;

        LaserBeamPassiveConfig laserBeamConfig = effectivePassiveToolsState.LaserBeam;

        if (laserBeamConfig.ApplyPlayerHandlingNerfWhileFiring == 0)
            return false;

        if (!IsLaserBeamFiring(playerEntity,
                               in inputStateLookup,
                               in powerUpsStateLookup,
                               in laserBeamState))
            return false;

        moveSpeedMultiplier = math.max(0f, laserBeamConfig.FiringMoveSpeedMultiplier);
        rotationSpeedMultiplier = math.max(0f, laserBeamConfig.FiringRotationSpeedMultiplier);
        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Mirrors the Laser Beam activation gates needed by pre-movement systems without rebuilding beam geometry.
    /// </summary>
    /// <param name="playerEntity">Player entity being evaluated.</param>
    /// <param name="inputStateLookup">Read-only input lookup used to detect held Shoot input.</param>
    /// <param name="powerUpsStateLookup">Read-only power-up state lookup used to respect shooting suppression.</param>
    /// <param name="laserBeamState">Current Laser Beam state.</param>
    /// <returns>True when the beam should be considered firing for handling nerf purposes.</returns>
    private static bool IsLaserBeamFiring(Entity playerEntity,
                                          in ComponentLookup<PlayerInputState> inputStateLookup,
                                          in ComponentLookup<PlayerPowerUpsState> powerUpsStateLookup,
                                          in PlayerLaserBeamState laserBeamState)
    {
        bool hasTriggeredActiveLaser = PlayerLaserBeamStateUtility.HasTriggeredActiveLaser(in laserBeamState);
        bool hasChargeImpulse = laserBeamState.ChargeImpulseRemainingSeconds > 0f;
        bool isShootPressed = inputStateLookup.HasComponent(playerEntity) &&
                              inputStateLookup[playerEntity].Shoot > ShootInputThreshold;
        bool isShootingSuppressed = powerUpsStateLookup.HasComponent(playerEntity) &&
                                    powerUpsStateLookup[playerEntity].IsShootingSuppressed != 0;

        if (isShootingSuppressed && !hasTriggeredActiveLaser)
            return false;

        if (!hasTriggeredActiveLaser && laserBeamState.IsOverheated != 0)
            return false;

        return hasTriggeredActiveLaser || hasChargeImpulse || isShootPressed;
    }
    #endregion

    #endregion
}
