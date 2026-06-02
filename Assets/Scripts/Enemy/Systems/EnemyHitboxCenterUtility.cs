using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Resolves the world-space center shared by enemy gameplay hitboxes and ground-footprint presentation.
/// </summary>
public static class EnemyHitboxCenterUtility
{
    #region Constants
    private const float OffsetEpsilonSquared = 0.000001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the planar hit center for one enemy from its ECS transform and baked enemy data.
    /// Rotation of the baked offset is disabled for continuous self-spinning enemies so visual-only
    /// spin does not make a small bounds-derived offset orbit around the root.
    /// </summary>
    /// <param name="entityTransform">World transform of the enemy entity root.</param>
    /// <param name="enemyData">Baked enemy data containing the local planar hit-center offset.</param>
    /// <returns>World-space hit center used by gameplay radius checks.</returns>
    public static float3 ResolveWorldCenter(in LocalTransform entityTransform, in EnemyData enemyData)
    {
        quaternion offsetRotation = enemyData.RotateHitCenterOffset != 0 ? entityTransform.Rotation : quaternion.identity;
        return ResolveWorldCenter(entityTransform.Position,
                                  offsetRotation,
                                  entityTransform.Scale,
                                  enemyData.HitCenterOffsetXZ);
    }

    /// <summary>
    /// Resolves the planar hit center for one enemy from its entity position, rotation, scale and baked offset.
    /// </summary>
    /// <param name="entityPosition">World-space enemy entity root position.</param>
    /// <param name="entityRotation">World-space enemy entity root rotation.</param>
    /// <param name="entityScale">Uniform enemy entity root scale.</param>
    /// <param name="hitCenterOffsetXZ">Local root-space XZ offset from entity root to hit center.</param>
    /// <returns>World-space hit center used by gameplay radius checks.</returns>
    public static float3 ResolveWorldCenter(float3 entityPosition,
                                            quaternion entityRotation,
                                            float entityScale,
                                            float2 hitCenterOffsetXZ)
    {
        float3 worldOffset = ResolveWorldPlanarOffset(entityRotation, entityScale, hitCenterOffsetXZ);
        return new float3(entityPosition.x + worldOffset.x,
                          entityPosition.y,
                          entityPosition.z + worldOffset.z);
    }

    /// <summary>
    /// Resolves the planar hit center for one enemy from its entity position and baked enemy data.
    /// This overload keeps identity-rotation call sites explicit for tooling that has no transform yet.
    /// </summary>
    /// <param name="entityPosition">World-space enemy entity root position.</param>
    /// <param name="enemyData">Baked enemy data containing the local planar hit-center offset.</param>
    /// <returns>World-space hit center used by gameplay radius checks.</returns>
    public static float3 ResolveWorldCenter(float3 entityPosition, in EnemyData enemyData)
    {
        return ResolveWorldCenter(entityPosition, quaternion.identity, 1f, enemyData.HitCenterOffsetXZ);
    }

    /// <summary>
    /// Resolves a managed Transform position for the ground indicator from one root transform snapshot and baked offsets.
    /// </summary>
    /// <param name="entityPosition">World-space enemy entity root position.</param>
    /// <param name="entityRotation">World-space enemy entity root rotation.</param>
    /// <param name="entityScale">Uniform enemy entity root scale.</param>
    /// <param name="hitCenterOffsetXZ">Local root-space XZ offset from entity root to hit center.</param>
    /// <param name="rotateHitCenterOffset">True when the local hit-center offset should rotate with the entity root.</param>
    /// <param name="heightOffset">World-space height offset applied to the returned position.</param>
    /// <returns>World-space ground indicator position.</returns>
    public static Vector3 ResolveWorldCenter(Vector3 entityPosition,
                                             Quaternion entityRotation,
                                             float entityScale,
                                             float2 hitCenterOffsetXZ,
                                             bool rotateHitCenterOffset,
                                             float heightOffset)
    {
        Vector3 scaledLocalOffset = new Vector3(hitCenterOffsetXZ.x * entityScale,
                                                0f,
                                                hitCenterOffsetXZ.y * entityScale);
        return ResolveManagedWorldCenter(entityPosition, entityRotation, scaledLocalOffset, rotateHitCenterOffset, heightOffset);
    }

    /// <summary>
    /// Resolves a managed Transform position for the ground indicator from one root transform snapshot and baked offsets.
    /// </summary>
    /// <param name="entityPosition">World-space enemy entity root position.</param>
    /// <param name="entityRotation">World-space enemy entity root rotation.</param>
    /// <param name="entityScale">World-space enemy root scale used to preserve authored non-uniform prefab scale in editor previews.</param>
    /// <param name="hitCenterOffsetXZ">Local root-space XZ offset from entity root to hit center.</param>
    /// <param name="rotateHitCenterOffset">True when the local hit-center offset should rotate with the entity root.</param>
    /// <param name="heightOffset">World-space height offset applied to the returned position.</param>
    /// <returns>World-space ground indicator position.</returns>
    public static Vector3 ResolveWorldCenter(Vector3 entityPosition,
                                             Quaternion entityRotation,
                                             Vector3 entityScale,
                                             float2 hitCenterOffsetXZ,
                                             bool rotateHitCenterOffset,
                                             float heightOffset)
    {
        Vector3 scaledLocalOffset = new Vector3(hitCenterOffsetXZ.x * entityScale.x,
                                                0f,
                                                hitCenterOffsetXZ.y * entityScale.z);
        return ResolveManagedWorldCenter(entityPosition, entityRotation, scaledLocalOffset, rotateHitCenterOffset, heightOffset);
    }

    /// <summary>
    /// Checks whether an enemy has a meaningful authored planar hit-center offset.
    /// </summary>
    /// <param name="enemyData">Baked enemy data containing the planar hit-center offset.</param>
    /// <returns>True when the hit center is offset from the entity root.</returns>
    public static bool HasPlanarOffset(in EnemyData enemyData)
    {
        return math.lengthsq(enemyData.HitCenterOffsetXZ) > OffsetEpsilonSquared;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Converts a baked local planar offset into world XZ space while ignoring any vertical contribution.
    /// Hit centers intentionally stay on the enemy movement plane even if a malformed rotation contains pitch or roll.
    /// </summary>
    /// <param name="entityRotation">World-space enemy entity root rotation.</param>
    /// <param name="entityScale">Uniform enemy entity root scale.</param>
    /// <param name="hitCenterOffsetXZ">Local root-space XZ offset from entity root to hit center.</param>
    /// <returns>World-space planar offset from entity root to hit center.</returns>
    private static float3 ResolveWorldPlanarOffset(quaternion entityRotation, float entityScale, float2 hitCenterOffsetXZ)
    {
        if (math.lengthsq(hitCenterOffsetXZ) <= OffsetEpsilonSquared)
            return float3.zero;

        float resolvedScale = math.max(0f, entityScale);
        float3 localOffset = new float3(hitCenterOffsetXZ.x * resolvedScale,
                                        0f,
                                        hitCenterOffsetXZ.y * resolvedScale);
        float3 rotatedOffset = math.mul(entityRotation, localOffset);
        return new float3(rotatedOffset.x, 0f, rotatedOffset.z);
    }

    /// <summary>
    /// Applies the managed hit-center offset rotation policy and height offset to a Transform-space snapshot.
    /// </summary>
    /// <param name="entityPosition">World-space enemy entity root position.</param>
    /// <param name="entityRotation">World-space enemy entity root rotation.</param>
    /// <param name="scaledLocalOffset">Local planar offset after the caller has applied the current entity scale.</param>
    /// <param name="rotateHitCenterOffset">True when the scaled local offset should rotate with the entity root.</param>
    /// <param name="heightOffset">World-space height offset applied to the returned position.</param>
    /// <returns>World-space position resolved for managed presentation objects.</returns>
    private static Vector3 ResolveManagedWorldCenter(Vector3 entityPosition,
                                                     Quaternion entityRotation,
                                                     Vector3 scaledLocalOffset,
                                                     bool rotateHitCenterOffset,
                                                     float heightOffset)
    {
        Quaternion offsetRotation = rotateHitCenterOffset ? entityRotation : Quaternion.identity;
        Vector3 worldOffset = offsetRotation * scaledLocalOffset;
        return new Vector3(entityPosition.x + worldOffset.x,
                           entityPosition.y + heightOffset,
                           entityPosition.z + worldOffset.z);
    }
    #endregion

    #endregion
}
