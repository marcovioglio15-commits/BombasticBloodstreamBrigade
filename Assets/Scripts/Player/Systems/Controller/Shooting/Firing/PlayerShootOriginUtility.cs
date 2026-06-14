using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Resolves the authoritative world-space projectile origin for player-fired shots.
/// Baked muzzle anchors are preferred, then the player transform fallback.
/// None.
/// </summary>
public static class PlayerShootOriginUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the world-space spawn position for one player-fired projectile.
    /// </summary>
    /// <param name="shooterEntity">Player entity requesting the projectile spawn.</param>
    /// <param name="shooterTransform">Current player transform used as the final fallback reference pose.</param>
    /// <param name="shootOffset">Authored local shoot offset rotated by the resolved muzzle rotation.</param>
    /// <param name="muzzleLookup">Lookup used to read the baked ECS muzzle anchor entity.</param>
    /// <param name="transformLookup">Lookup used to read fallback LocalTransform data from the baked muzzle anchor.</param>
    /// <param name="localToWorldLookup">Lookup used to read the most accurate world pose from the baked muzzle anchor.</param>
    /// <returns>World-space projectile spawn position.</returns>
    public static float3 ResolveSpawnPosition(Entity shooterEntity,
                                              in LocalTransform shooterTransform,
                                              in float3 shootOffset,
                                              in ComponentLookup<ShooterMuzzleAnchor> muzzleLookup,
                                              in ComponentLookup<LocalTransform> transformLookup,
                                              in ComponentLookup<LocalToWorld> localToWorldLookup)
    {
        float3 referencePosition = shooterTransform.Position;
        quaternion referenceRotation = shooterTransform.Rotation;

        if (TryResolveBakedMuzzlePose(shooterEntity,
                                      in muzzleLookup,
                                      in transformLookup,
                                      in localToWorldLookup,
                                      out referencePosition,
                                      out referenceRotation))
        {
            float3 bakedOffset = math.rotate(referenceRotation, shootOffset);
            return referencePosition + bakedOffset;
        }

        float3 fallbackOffset = math.rotate(referenceRotation, shootOffset);
        return referencePosition + fallbackOffset;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Attempts to resolve the baked ECS muzzle anchor pose.
    /// </summary>
    /// <param name="shooterEntity">Player entity requesting the spawn origin.</param>
    /// <param name="muzzleLookup">Lookup used to read the baked muzzle anchor entity.</param>
    /// <param name="transformLookup">Lookup used to read LocalTransform fallback data for the anchor.</param>
    /// <param name="localToWorldLookup">Lookup used to read the world-space anchor transform.</param>
    /// <param name="position">Resolved baked muzzle position.</param>
    /// <param name="rotation">Resolved baked muzzle rotation.</param>
    /// <returns>True when a valid baked muzzle pose exists, otherwise false.</returns>
    private static bool TryResolveBakedMuzzlePose(Entity shooterEntity,
                                                  in ComponentLookup<ShooterMuzzleAnchor> muzzleLookup,
                                                  in ComponentLookup<LocalTransform> transformLookup,
                                                  in ComponentLookup<LocalToWorld> localToWorldLookup,
                                                  out float3 position,
                                                  out quaternion rotation)
    {
        if (muzzleLookup.HasComponent(shooterEntity))
        {
            Entity muzzleEntity = muzzleLookup[shooterEntity].AnchorEntity;

            if (localToWorldLookup.HasComponent(muzzleEntity))
            {
                LocalToWorld localToWorld = localToWorldLookup[muzzleEntity];
                position = localToWorld.Value.c3.xyz;
                rotation = quaternion.LookRotationSafe(localToWorld.Value.c2.xyz, localToWorld.Value.c1.xyz);
                return true;
            }

            if (transformLookup.HasComponent(muzzleEntity))
            {
                LocalTransform muzzleTransform = transformLookup[muzzleEntity];
                position = muzzleTransform.Position;
                rotation = muzzleTransform.Rotation;
                return true;
            }
        }

        position = float3.zero;
        rotation = quaternion.identity;
        return false;
    }
    #endregion

    #endregion
}
