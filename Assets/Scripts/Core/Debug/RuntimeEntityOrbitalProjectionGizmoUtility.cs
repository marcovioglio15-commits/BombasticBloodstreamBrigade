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

                float radius = projection.Config.CollisionRadius;

                if (radius <= 0f)
                    continue;

                LocalTransform transform = entityManager.GetComponentData<LocalTransform>(projectionEntity);

                if (math.distance(playerPosition.xz, transform.Position.xz) > DrawDistance)
                    continue;

                Vector3 projectionPosition = new Vector3(transform.Position.x, transform.Position.y, transform.Position.z);
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

    #endregion
}
