using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Draws runtime collision-radius gizmos for live player orbital projections near the active player.
/// </summary>
public static class RuntimeEntityOrbitalProjectionGizmoUtility
{
    #region Constants
    private const float DrawDistance = 45f;
    private const int MaximumDrawCount = 96;
    private const int MaximumLabelCount = 12;
    private static readonly Color CollisionRadiusColor = new Color(0.16f, 0.94f, 0.82f, 0.96f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Draws collision discs for live orbital projections when their runtime debug toggle is active.
    /// </summary>
    /// <param name="primitiveDrawer">Active rendering backend receiving primitive calls.</param>
    /// <param name="entityManager">Runtime entity manager used to read projection components.</param>
    /// <param name="projectionQuery">Cached query for projection instances and transforms.</param>
    /// <param name="playerPosition">Runtime player position used for distance filtering.</param>
    public static void DrawCollisionRadiusGizmos(IRuntimeGizmoPrimitiveDrawer primitiveDrawer,
                                                 EntityManager entityManager,
                                                 EntityQuery projectionQuery,
                                                 float3 playerPosition)
    {
        if (!RuntimeGizmoDebugState.OrbitalProjectionCollisionRadiusEnabled)
            return;

        if (projectionQuery.IsEmptyIgnoreFilter)
            return;

        NativeArray<Entity> projectionEntities = projectionQuery.ToEntityArray(Allocator.Temp);
        int drawnCount = 0;
        int labelCount = 0;

        try
        {
            // Keep the overlay scoped to projections that matter in the current combat slice.
            for (int projectionIndex = 0; projectionIndex < projectionEntities.Length; projectionIndex++)
            {
                if (drawnCount >= MaximumDrawCount)
                    break;

                Entity projectionEntity = projectionEntities[projectionIndex];
                PlayerOrbitalProjectionInstance projection = entityManager.GetComponentData<PlayerOrbitalProjectionInstance>(projectionEntity);

                if (projection.Phase == PlayerOrbitalProjectionPhase.Despawning)
                    continue;

                float radius = PlayerOrbitalProjectionCollisionShapeRuntimeUtility.ResolveBroadPhaseRadius(in projection.Config);

                if (radius <= 0f)
                    continue;

                LocalTransform transform = entityManager.GetComponentData<LocalTransform>(projectionEntity);

                if (math.distance(playerPosition.xz, transform.Position.xz) > DrawDistance)
                    continue;

                Vector3 projectionPosition = new Vector3(transform.Position.x, transform.Position.y, transform.Position.z);

                // Model-shaped projections draw their actual silhouette; the rest keep the disc.
                if (!TryDrawCollisionHull(primitiveDrawer, entityManager, projectionEntity, in projection, in transform))
                    primitiveDrawer.DrawWireDisc(projectionPosition, radius, CollisionRadiusColor);

                if (RuntimeGizmoDebugState.ShowLabels && labelCount < MaximumLabelCount)
                {
                    primitiveDrawer.DrawLabel(projectionPosition, "Orbital Projection");
                    labelCount++;
                }

                drawnCount++;
            }
        }
        finally
        {
            if (projectionEntities.IsCreated)
                projectionEntities.Dispose();
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Draws the model-shaped collision silhouette for one projection when its hull is active.
    /// </summary>
    /// <param name="primitiveDrawer">Active rendering backend receiving primitive calls.</param>
    /// <param name="entityManager">Runtime entity manager used to read the hull buffer.</param>
    /// <param name="projectionEntity">Projection entity being drawn.</param>
    /// <param name="projection">Projection instance carrying collision settings.</param>
    /// <param name="transform">Projection world transform applied to the local hull.</param>
    /// <returns>True when the silhouette was drawn instead of the fallback disc.</returns>
    private static bool TryDrawCollisionHull(IRuntimeGizmoPrimitiveDrawer primitiveDrawer,
                                             EntityManager entityManager,
                                             Entity projectionEntity,
                                             in PlayerOrbitalProjectionInstance projection,
                                             in LocalTransform transform)
    {
        if (projection.Config.AdaptCollisionToModel == 0)
            return false;

        if (!entityManager.HasBuffer<PlayerOrbitalProjectionCollisionVertexElement>(projectionEntity))
            return false;

        DynamicBuffer<PlayerOrbitalProjectionCollisionVertexElement> hullVertices = entityManager.GetBuffer<PlayerOrbitalProjectionCollisionVertexElement>(projectionEntity, true);

        if (hullVertices.Length < 3)
            return false;

        // Walk the hull edges in world space, applying the projection yaw and scale.
        float scale = math.max(0.0001f, transform.Scale);
        Vector3 previousPoint = ResolveHullWorldPoint(hullVertices[hullVertices.Length - 1].LocalPositionXZ, in transform, scale);

        for (int vertexIndex = 0; vertexIndex < hullVertices.Length; vertexIndex++)
        {
            Vector3 currentPoint = ResolveHullWorldPoint(hullVertices[vertexIndex].LocalPositionXZ, in transform, scale);
            primitiveDrawer.DrawLink(previousPoint, currentPoint, CollisionRadiusColor);
            previousPoint = currentPoint;
        }

        return true;
    }

    /// <summary>
    /// Transforms one local hull vertex into world space at the projection's height.
    /// </summary>
    /// <param name="localPositionXZ">Hull vertex in prefab-local XZ space.</param>
    /// <param name="transform">Projection world transform.</param>
    /// <param name="scale">Sanitized projection scale.</param>
    /// <returns>World-space vertex used for gizmo lines.</returns>
    private static Vector3 ResolveHullWorldPoint(float2 localPositionXZ, in LocalTransform transform, float scale)
    {
        float3 worldOffset = math.mul(transform.Rotation, new float3(localPositionXZ.x, 0f, localPositionXZ.y)) * scale;
        float3 worldPoint = transform.Position + worldOffset;
        return new Vector3(worldPoint.x, worldPoint.y, worldPoint.z);
    }
    #endregion

    #endregion
}
