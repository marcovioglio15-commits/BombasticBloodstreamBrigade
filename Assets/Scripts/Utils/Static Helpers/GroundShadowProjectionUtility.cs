using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

/// <summary>
/// Selects how a shader-driven ground shadow is placed in the world.
/// </summary>
public enum GroundShadowProjectionMode : byte
{
    RaisedQuad = 0,
    ProjectOntoGround = 1
}

/// <summary>
/// Resolves world-space ground-shadow poses shared by enemy and player presentation systems.
/// </summary>
public static class GroundShadowProjectionUtility
{
    #region Constants
    private const float MinimumProbeLift = 0.05f;
    private const float MinimumProjectionDistance = 0.001f;
    private static readonly Quaternion GroundPlaneRotation = Quaternion.Euler(90f, 0f, 0f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves a ground-shadow pose from an entity pivot, optional planar hit-center offset and projection settings.
    /// </summary>
    /// <param name="entityPosition">World-space entity pivot position.</param>
    /// <param name="entityRotation">World-space entity rotation used when the local planar offset follows the root.</param>
    /// <param name="entityScale">Uniform entity scale applied to the local planar offset.</param>
    /// <param name="positionOffsetXZ">Local planar offset from the entity pivot to the represented hit center.</param>
    /// <param name="rotateHitCenterOffset">True when the local planar offset should rotate with the entity root.</param>
    /// <param name="heightOffset">Clearance applied above the resolved ground point or raised quad source.</param>
    /// <param name="projectionMode">Placement mode selected by the visual preset.</param>
    /// <param name="projectionMaxDistance">Maximum downward projection distance from the hit-center source.</param>
    /// <param name="hasPhysicsWorld">True when a valid DOTS physics world is available for projection.</param>
    /// <param name="groundProbeMask">Physics category mask restricting which colliders the downward probe may hit.</param>
    /// <param name="physicsWorldSingleton">Physics world used for the optional ground projection ray.</param>
    /// <param name="position">Resolved world-space shadow position.</param>
    /// <param name="rotation">Resolved world-space shadow rotation.</param>
    public static void ResolvePose(Vector3 entityPosition,
                                   Quaternion entityRotation,
                                   float entityScale,
                                   float2 positionOffsetXZ,
                                   bool rotateHitCenterOffset,
                                   float heightOffset,
                                   GroundShadowProjectionMode projectionMode,
                                   float projectionMaxDistance,
                                   bool hasPhysicsWorld,
                                   uint groundProbeMask,
                                   in PhysicsWorldSingleton physicsWorldSingleton,
                                   out Vector3 position,
                                   out Quaternion rotation)
    {
        Vector3 basePosition = EnemyHitboxCenterUtility.ResolveWorldCenter(entityPosition,
                                                                           entityRotation,
                                                                           entityScale,
                                                                           positionOffsetXZ,
                                                                           rotateHitCenterOffset,
                                                                           0f);

        if (projectionMode == GroundShadowProjectionMode.ProjectOntoGround &&
            hasPhysicsWorld &&
            projectionMaxDistance > MinimumProjectionDistance &&
            TryProjectOntoGround(basePosition,
                                 heightOffset,
                                 projectionMaxDistance,
                                 groundProbeMask,
                                 in physicsWorldSingleton,
                                 out position,
                                 out rotation))
        {
            return;
        }

        position = basePosition + Vector3.up * heightOffset;
        rotation = GroundPlaneRotation;
    }

    /// <summary>
    /// Returns the default flat-ground rotation used by ground-indicator quad meshes.
    /// </summary>
    /// <returns>World rotation that places a standard Unity quad on the XZ plane.</returns>
    public static Quaternion ResolveGroundPlaneRotation()
    {
        return GroundPlaneRotation;
    }

    /// <summary>
    /// Builds the physics category mask used by the downward ground probe. Excluding the Walls layer keeps
    /// the shadow from projecting onto vertical wall colliders next to the entity, which would tilt the quad
    /// edge-on and make it appear to flicker on and off near map borders or while moving.
    /// </summary>
    /// <param name="excludedLayerBits">Physics category bits to remove from the probe, or zero to probe every collider.</param>
    /// <returns>Collision mask consumed as the ground probe CollidesWith value.</returns>
    public static uint ResolveGroundProbeMask(uint excludedLayerBits)
    {
        if (excludedLayerBits == 0u)
            return uint.MaxValue;

        return uint.MaxValue & ~excludedLayerBits;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Projects one shadow source onto the closest collider below it and aligns the quad to the hit normal.
    /// </summary>
    /// <param name="basePosition">World-space source position before vertical clearance is applied.</param>
    /// <param name="heightOffset">Clearance applied along the ground normal after projection.</param>
    /// <param name="projectionMaxDistance">Maximum downward distance accepted for the projection.</param>
    /// <param name="groundProbeMask">Physics category mask restricting which colliders the probe may hit.</param>
    /// <param name="physicsWorldSingleton">Physics world used for the raycast.</param>
    /// <param name="position">Resolved projected world position.</param>
    /// <param name="rotation">Resolved projected world rotation.</param>
    /// <returns>True when a ground collider was hit within the configured distance.</returns>
    private static bool TryProjectOntoGround(Vector3 basePosition,
                                             float heightOffset,
                                             float projectionMaxDistance,
                                             uint groundProbeMask,
                                             in PhysicsWorldSingleton physicsWorldSingleton,
                                             out Vector3 position,
                                             out Quaternion rotation)
    {
        float resolvedProbeLift = math.max(MinimumProbeLift, math.max(0f, heightOffset) + MinimumProbeLift);
        float3 start = ToFloat3(basePosition) + new float3(0f, resolvedProbeLift, 0f);
        float3 end = ToFloat3(basePosition) - new float3(0f, math.max(MinimumProjectionDistance, projectionMaxDistance), 0f);
        RaycastInput groundProbe = new RaycastInput
        {
            Start = start,
            End = end,
            Filter = new CollisionFilter
            {
                BelongsTo = uint.MaxValue,
                CollidesWith = groundProbeMask,
                GroupIndex = 0
            }
        };

        if (!physicsWorldSingleton.CastRay(groundProbe, out Unity.Physics.RaycastHit groundHit))
        {
            position = default;
            rotation = default;
            return false;
        }

        float3 normal = math.normalizesafe(groundHit.SurfaceNormal, new float3(0f, 1f, 0f));
        position = ToVector3(groundHit.Position + normal * heightOffset);
        rotation = Quaternion.FromToRotation(Vector3.up, ToVector3(normal)) * GroundPlaneRotation;
        return true;
    }

    /// <summary>
    /// Converts a Unity vector to a DOTS float3.
    /// </summary>
    /// <param name="value">Unity vector value.</param>
    /// <returns>Equivalent float3 value.</returns>
    private static float3 ToFloat3(Vector3 value)
    {
        return new float3(value.x, value.y, value.z);
    }

    /// <summary>
    /// Converts a DOTS float3 to a Unity vector.
    /// </summary>
    /// <param name="value">DOTS vector value.</param>
    /// <returns>Equivalent Unity vector value.</returns>
    private static Vector3 ToVector3(float3 value)
    {
        return new Vector3(value.x, value.y, value.z);
    }
    #endregion

    #endregion
}
