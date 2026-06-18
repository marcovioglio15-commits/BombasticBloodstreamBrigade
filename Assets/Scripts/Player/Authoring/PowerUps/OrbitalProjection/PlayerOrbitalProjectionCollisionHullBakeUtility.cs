using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Computes the XZ convex-hull silhouette of an orbital projection prefab at bake time. The hull is
/// expressed in prefab-root local space (counter-clockwise) and capped to a fixed vertex budget so
/// runtime narrow-phase checks stay cheap and Burst-friendly. Used by the player baker to fill the
/// per-binding hull table consumed by Adapt Collision To Model projections.
/// </summary>
public static class PlayerOrbitalProjectionCollisionHullBakeUtility
{
    #region Constants
    public const int MaximumHullVertices = 16;
    private const float DegenerateAreaEpsilon = 0.000001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the XZ convex hull for one projection prefab from every MeshFilter and
    /// SkinnedMeshRenderer found under its root, including inactive children.
    /// </summary>
    /// <param name="prefab">Projection prefab asset whose silhouette is baked.</param>
    /// <param name="hullVertices">Output counter-clockwise hull vertices in prefab-local XZ space.</param>
    /// <returns>True when a usable hull (3+ vertices) was produced.</returns>
    public static bool TryBuildHull(GameObject prefab, List<float2> hullVertices)
    {
        if (hullVertices == null)
            return false;

        hullVertices.Clear();

        if (prefab == null)
            return false;

        // Project every render vertex into prefab-root local XZ space.
        List<float2> projectedVertices = new List<float2>(256);
        Matrix4x4 rootWorldToLocal = prefab.transform.worldToLocalMatrix;

        CollectMeshFilterVertices(prefab, rootWorldToLocal, projectedVertices);
        CollectSkinnedMeshVertices(prefab, rootWorldToLocal, projectedVertices);

        if (projectedVertices.Count < 3)
            return false;

        BuildConvexHull(projectedVertices, hullVertices);

        if (hullVertices.Count < 3)
        {
            hullVertices.Clear();
            return false;
        }

        DecimateHull(hullVertices, MaximumHullVertices);
        return hullVertices.Count >= 3;
    }
    #endregion

    #region Vertex Collection
    /// <summary>
    /// Appends XZ-projected shared-mesh vertices from every MeshFilter under the prefab root.
    /// </summary>
    /// <param name="prefab">Prefab asset being scanned.</param>
    /// <param name="rootWorldToLocal">Prefab root inverse transform used to express children locally.</param>
    /// <param name="projectedVertices">Output vertex list updated in place.</param>
    private static void CollectMeshFilterVertices(GameObject prefab,
                                                  Matrix4x4 rootWorldToLocal,
                                                  List<float2> projectedVertices)
    {
        MeshFilter[] meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);

        for (int filterIndex = 0; filterIndex < meshFilters.Length; filterIndex++)
        {
            MeshFilter meshFilter = meshFilters[filterIndex];

            if (meshFilter == null || meshFilter.sharedMesh == null)
                continue;

            AppendMeshVertices(meshFilter.sharedMesh,
                               rootWorldToLocal * meshFilter.transform.localToWorldMatrix,
                               projectedVertices);
        }
    }

    /// <summary>
    /// Appends XZ-projected shared-mesh vertices from every SkinnedMeshRenderer under the prefab root.
    /// </summary>
    /// <param name="prefab">Prefab asset being scanned.</param>
    /// <param name="rootWorldToLocal">Prefab root inverse transform used to express children locally.</param>
    /// <param name="projectedVertices">Output vertex list updated in place.</param>
    private static void CollectSkinnedMeshVertices(GameObject prefab,
                                                   Matrix4x4 rootWorldToLocal,
                                                   List<float2> projectedVertices)
    {
        SkinnedMeshRenderer[] skinnedRenderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        for (int rendererIndex = 0; rendererIndex < skinnedRenderers.Length; rendererIndex++)
        {
            SkinnedMeshRenderer skinnedRenderer = skinnedRenderers[rendererIndex];

            if (skinnedRenderer == null || skinnedRenderer.sharedMesh == null)
                continue;

            AppendMeshVertices(skinnedRenderer.sharedMesh,
                               rootWorldToLocal * skinnedRenderer.transform.localToWorldMatrix,
                               projectedVertices);
        }
    }

    /// <summary>
    /// Transforms one mesh's vertices into prefab-local space and appends their XZ projection.
    /// </summary>
    /// <param name="mesh">Shared mesh providing the bind-pose vertices.</param>
    /// <param name="meshToRootLocal">Transform from mesh space into prefab-root local space.</param>
    /// <param name="projectedVertices">Output vertex list updated in place.</param>
    private static void AppendMeshVertices(Mesh mesh,
                                           Matrix4x4 meshToRootLocal,
                                           List<float2> projectedVertices)
    {
        Vector3[] vertices = mesh.vertices;

        for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
        {
            Vector3 localVertex = meshToRootLocal.MultiplyPoint3x4(vertices[vertexIndex]);
            projectedVertices.Add(new float2(localVertex.x, localVertex.z));
        }
    }
    #endregion

    #region Hull Construction
    /// <summary>
    /// Builds a counter-clockwise convex hull from projected points using the monotone chain algorithm.
    /// </summary>
    /// <param name="points">Input projected points (mutated by sorting).</param>
    /// <param name="hull">Output counter-clockwise hull vertices.</param>
    private static void BuildConvexHull(List<float2> points, List<float2> hull)
    {
        points.Sort(ComparePointsLexicographically);

        // Lower hull sweep.
        for (int pointIndex = 0; pointIndex < points.Count; pointIndex++)
        {
            float2 point = points[pointIndex];

            while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0f)
                hull.RemoveAt(hull.Count - 1);

            hull.Add(point);
        }

        // Upper hull sweep (skips the final duplicate of the first point).
        int lowerHullCount = hull.Count + 1;

        for (int pointIndex = points.Count - 2; pointIndex >= 0; pointIndex--)
        {
            float2 point = points[pointIndex];

            while (hull.Count >= lowerHullCount && Cross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0f)
                hull.RemoveAt(hull.Count - 1);

            hull.Add(point);
        }

        if (hull.Count > 0)
            hull.RemoveAt(hull.Count - 1);
    }

    /// <summary>
    /// Reduces the hull to the requested vertex budget by repeatedly removing the vertex whose
    /// removal loses the least silhouette area, keeping the shape as faithful as possible.
    /// </summary>
    /// <param name="hull">Counter-clockwise hull mutated in place.</param>
    /// <param name="maximumVertices">Maximum number of vertices to keep.</param>
    private static void DecimateHull(List<float2> hull, int maximumVertices)
    {
        while (hull.Count > maximumVertices)
        {
            int cheapestIndex = 0;
            float cheapestArea = float.MaxValue;

            // Find the vertex contributing the smallest triangle area with its neighbors.
            for (int vertexIndex = 0; vertexIndex < hull.Count; vertexIndex++)
            {
                float2 previous = hull[(vertexIndex - 1 + hull.Count) % hull.Count];
                float2 current = hull[vertexIndex];
                float2 next = hull[(vertexIndex + 1) % hull.Count];
                float area = math.abs(Cross(previous, current, next));

                if (area >= cheapestArea)
                    continue;

                cheapestArea = area;
                cheapestIndex = vertexIndex;
            }

            hull.RemoveAt(cheapestIndex);
        }

        // Drop collinear leftovers so runtime edge math never sees zero-length normals.
        for (int vertexIndex = hull.Count - 1; vertexIndex >= 0 && hull.Count > 3; vertexIndex--)
        {
            float2 previous = hull[(vertexIndex - 1 + hull.Count) % hull.Count];
            float2 current = hull[vertexIndex];
            float2 next = hull[(vertexIndex + 1) % hull.Count];

            if (math.abs(Cross(previous, current, next)) <= DegenerateAreaEpsilon)
                hull.RemoveAt(vertexIndex);
        }
    }

    /// <summary>
    /// Compares two points by X, then by Y, for monotone chain sorting.
    /// </summary>
    /// <param name="left">First point.</param>
    /// <param name="right">Second point.</param>
    /// <returns>Standard comparison result.</returns>
    private static int ComparePointsLexicographically(float2 left, float2 right)
    {
        int xComparison = left.x.CompareTo(right.x);
        return xComparison != 0 ? xComparison : left.y.CompareTo(right.y);
    }

    /// <summary>
    /// Computes the 2D cross product of vectors (b - a) and (c - a).
    /// </summary>
    /// <param name="a">Shared origin point.</param>
    /// <param name="b">First vector end point.</param>
    /// <param name="c">Second vector end point.</param>
    /// <returns>Positive when c lies left of the a-b direction.</returns>
    private static float Cross(float2 a, float2 b, float2 c)
    {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }
    #endregion

    #endregion
}
