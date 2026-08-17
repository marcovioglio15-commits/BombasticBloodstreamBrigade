using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Builds the single projectile request emitted by a standalone Returning Projectiles active power-up.
/// </summary>
public static class PlayerReturningProjectileActivationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Emits one returning projectile while preserving compatible ordinary projectile-stat passives.
    /// </summary>
    /// <param name="slotConfig">Active slot containing the mandatory return config.</param>
    /// <param name="localTransform">Player transform used for fallback aim.</param>
    /// <param name="lookState">Player look state used for aim.</param>
    /// <param name="runtimeShootingConfig">Current projectile shooting values.</param>
    /// <param name="appliedElementSlots">Current default elemental payload slots.</param>
    /// <param name="passiveToolsState">Aggregated passives applied to ordinary projectile statistics.</param>
    /// <param name="playerEntity">Player entity that owns the request.</param>
    /// <param name="muzzleLookup">Read-only muzzle anchor lookup.</param>
    /// <param name="transformLookup">Read-only transform lookup.</param>
    /// <param name="localToWorldLookup">Read-only world-transform lookup.</param>
    /// <param name="slotIndex">Stable active slot index used for live-projectile accounting.</param>
    /// <param name="shootRequests">Mutable projectile request buffer.</param>
    public static void Execute(in PlayerPowerUpSlotConfig slotConfig,
                               in LocalTransform localTransform,
                               in PlayerLookState lookState,
                               in PlayerRuntimeShootingConfig runtimeShootingConfig,
                               DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> appliedElementSlots,
                               in PlayerPassiveToolsState passiveToolsState,
                               Entity playerEntity,
                               in ComponentLookup<ShooterMuzzleAnchor> muzzleLookup,
                               in ComponentLookup<LocalTransform> transformLookup,
                               in ComponentLookup<LocalToWorld> localToWorldLookup,
                               byte slotIndex,
                               DynamicBuffer<ShootRequest> shootRequests)
    {
        PlayerProjectileRequestUtility.ResolvePenetrationSettings(in runtimeShootingConfig.Values,
                                                                  ProjectilePenetrationMode.None,
                                                                  0,
                                                                  out ProjectilePenetrationMode penetrationMode,
                                                                  out int maxPenetrations);
        float3 shootDirection = PlayerProjectileRequestUtility.ResolveShootDirection(in lookState, in localTransform);
        float3 spawnPosition = PlayerProjectileRequestUtility.ResolveShootSpawnPosition(playerEntity,
                                                                                        in localTransform,
                                                                                        in runtimeShootingConfig,
                                                                                        in muzzleLookup,
                                                                                        in transformLookup,
                                                                                        in localToWorldLookup);
        PlayerProjectileRequestTemplate template = PlayerProjectileRequestUtility.BuildProjectileTemplate(in runtimeShootingConfig,
                                                                                                          appliedElementSlots,
                                                                                                          in passiveToolsState,
                                                                                                          1f,
                                                                                                          1f,
                                                                                                          1f,
                                                                                                          1f,
                                                                                                          1f,
                                                                                                          false,
                                                                                                          default,
                                                                                                          0f);
        PlayerProjectileRequestUtility.AddSpreadRequests(ref shootRequests,
                                                         1,
                                                         0f,
                                                         spawnPosition,
                                                         shootDirection,
                                                         in template,
                                                         penetrationMode,
                                                         maxPenetrations,
                                                         0,
                                                         ProjectileSpawnSource.ActivePowerUp,
                                                         slotIndex,
                                                         1,
                                                         slotConfig.ReturningProjectiles);
    }
    #endregion

    #endregion
}
