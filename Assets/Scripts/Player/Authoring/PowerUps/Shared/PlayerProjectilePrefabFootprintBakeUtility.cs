using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Resolves a rotation-invariant planar projectile footprint from prefab render geometry during baking.
/// The resulting radius keeps replacement meshes outside physical walls without runtime renderer queries.
/// </summary>
public static class PlayerProjectilePrefabFootprintBakeUtility
{
    #region Constants
    private const float MinimumUsableRadius = 0.001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Computes the greatest prefab-local XZ distance from the projectile pivot to its baked render silhouette.
    /// </summary>
    /// <param name="prefab">Projectile prefab or model asset inspected at bake time.</param>
    /// <returns>Positive rotation-invariant planar radius, or zero when no usable render geometry exists.</returns>
    public static float ResolvePlanarRadius(GameObject prefab)
    {
        if (prefab == null)
            return 0f;

        List<float2> hullVertices = new List<float2>(PlayerOrbitalProjectionCollisionHullBakeUtility.MaximumHullVertices);

        if (!PlayerOrbitalProjectionCollisionHullBakeUtility.TryBuildHull(prefab, hullVertices))
            return 0f;

        float maximumRadiusSquared = 0f;

        // The furthest silhouette point provides a safe sphere sweep for every runtime spin angle.
        for (int vertexIndex = 0; vertexIndex < hullVertices.Count; vertexIndex++)
            maximumRadiusSquared = math.max(maximumRadiusSquared, math.lengthsq(hullVertices[vertexIndex]));

        if (maximumRadiusSquared <= MinimumUsableRadius * MinimumUsableRadius)
            return 0f;

        return math.sqrt(maximumRadiusSquared);
    }
    #endregion

    #endregion
}
