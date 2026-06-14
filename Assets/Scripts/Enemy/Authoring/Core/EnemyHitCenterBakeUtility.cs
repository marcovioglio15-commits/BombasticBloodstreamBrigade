using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Resolves the local hit-center offset baked for enemies by measuring the authored visual body bounds.
/// This keeps collision, contact damage, runtime gizmos and ground indicators aligned with imported
/// meshes whose pivot is not horizontally centered under the visible body.
/// </summary>
public static class EnemyHitCenterBakeUtility
{
    #region Constants
    private const float BoundsEpsilon = 0.0001f;
    private const float SelfRotationSpeedEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves whether a hit-center offset should rotate with the enemy root.
    /// Continuous self-rotation is treated as visual spin, so the gameplay center should not orbit with it.
    /// </summary>
    /// <param name="rotationSpeedDegreesPerSecond">Authored self-rotation speed from movement settings.</param>
    /// <returns>True when the hit-center offset should rotate with LocalTransform.Rotation.</returns>
    public static bool ShouldRotateHitCenterOffset(float rotationSpeedDegreesPerSecond)
    {
        return math.abs(rotationSpeedDegreesPerSecond) <= SelfRotationSpeedEpsilon;
    }

    /// <summary>
    /// Resolves the final local root-space XZ hit-center offset used by ECS runtime systems.
    /// The visual body center is detected automatically and the authored preset offset remains an additive fine-tune.
    /// </summary>
    /// <param name="authoring">Enemy authoring component whose renderer hierarchy is inspected during bake.</param>
    /// <param name="manualOffsetXZ">Manual local XZ fine-tune read from the visual footprint preset.</param>
    /// <returns>Local root-space XZ offset from entity root to the resolved visual hit center.</returns>
    public static float2 ResolveLocalHitCenterOffsetXZ(EnemyAuthoring authoring, float2 manualOffsetXZ)
    {
        if (authoring == null)
            return manualOffsetXZ;

        if (!TryResolveVisualBoundsCenterOffsetXZ(authoring, out float2 visualOffsetXZ))
            return manualOffsetXZ;

        return manualOffsetXZ + visualOffsetXZ;
    }

    /// <summary>
    /// Computes the local XZ center of renderers that belong to the enemy body, excluding helper visuals.
    /// Used by both baking and editor gizmo previews so authored debugging matches runtime data.
    /// </summary>
    /// <param name="authoring">Enemy authoring component whose renderer hierarchy is inspected.</param>
    /// <param name="visualOffsetXZ">Local root-space XZ offset from the root pivot to the visual body center.</param>
    /// <returns>True when at least one usable body renderer contributed bounds.</returns>
    public static bool TryResolveVisualBoundsCenterOffsetXZ(EnemyAuthoring authoring, out float2 visualOffsetXZ)
    {
        visualOffsetXZ = float2.zero;

        if (authoring == null)
            return false;

        Transform rootTransform = authoring.transform;

        if (rootTransform == null)
            return false;

        Renderer[] renderers = authoring.GetComponentsInChildren<Renderer>(true);

        if (renderers == null || renderers.Length <= 0)
            return false;

        Vector2 min = Vector2.zero;
        Vector2 max = Vector2.zero;
        bool hasBounds = false;

        // Inspect only visual body renderers; indicators, billboards and VFX do not define gameplay center.
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer candidateRenderer = renderers[rendererIndex];

            if (!ShouldIncludeRenderer(candidateRenderer, rootTransform))
                continue;

            if (!TryResolveRendererFootprint(candidateRenderer, rootTransform, out Vector2 rendererMin, out Vector2 rendererMax))
                continue;

            EncapsulateBounds(rendererMin, rendererMax, ref min, ref max, ref hasBounds);
        }

        if (!hasBounds)
            return false;

        visualOffsetXZ = new float2((min.x + max.x) * 0.5f,
                                    (min.y + max.y) * 0.5f);
        return true;
    }
    #endregion

    #region Renderer Filtering
    /// <summary>
    /// Returns whether a renderer belongs to the body mesh/sprite instead of helper presentation.
    /// </summary>
    /// <param name="candidateRenderer">Renderer candidate found under the enemy authoring root.</param>
    /// <param name="rootTransform">Enemy authoring root used as the transform-chain stop.</param>
    /// <returns>True when the renderer should contribute to the visual-body bounds.</returns>
    private static bool ShouldIncludeRenderer(Renderer candidateRenderer, Transform rootTransform)
    {
        if (candidateRenderer == null)
            return false;

        if (!candidateRenderer.enabled)
            return false;

        if (!candidateRenderer.gameObject.activeSelf)
            return false;

        if (candidateRenderer is ParticleSystemRenderer || candidateRenderer is TrailRenderer || candidateRenderer is LineRenderer)
            return false;

        if (candidateRenderer.GetComponentInParent<EnemyGroundIndicatorView>(true) != null)
            return false;

        if (candidateRenderer.GetComponentInParent<EnemyOffensiveEngagementBillboardView>(true) != null)
            return false;

        return !HasExcludedHelperName(candidateRenderer.transform, rootTransform);
    }

    /// <summary>
    /// Checks the renderer transform chain for helper names that should not influence body-center detection.
    /// </summary>
    /// <param name="candidateTransform">Renderer transform to inspect.</param>
    /// <param name="rootTransform">Enemy root transform where the search stops.</param>
    /// <returns>True when the transform belongs to status, warning, indicator, billboard or VFX helpers.</returns>
    private static bool HasExcludedHelperName(Transform candidateTransform, Transform rootTransform)
    {
        Transform currentTransform = candidateTransform;

        while (currentTransform != null && currentTransform != rootTransform)
        {
            string objectName = currentTransform.name;

            if (ContainsOrdinalIgnoreCase(objectName, "Status") ||
                ContainsOrdinalIgnoreCase(objectName, "Health") ||
                ContainsOrdinalIgnoreCase(objectName, "Shield") ||
                ContainsOrdinalIgnoreCase(objectName, "Billboard") ||
                ContainsOrdinalIgnoreCase(objectName, "Warning") ||
                ContainsOrdinalIgnoreCase(objectName, "Indicator") ||
                ContainsOrdinalIgnoreCase(objectName, "VFX"))
                return true;

            currentTransform = currentTransform.parent;
        }

        return false;
    }
    #endregion

    #region Bounds Resolution
    /// <summary>
    /// Resolves one renderer footprint in enemy-root local XZ space using the most precise bounds source available.
    /// </summary>
    /// <param name="candidateRenderer">Renderer candidate that passed body filtering.</param>
    /// <param name="rootTransform">Enemy root transform used to convert bounds into local XZ space.</param>
    /// <param name="min">Minimum local XZ corner contributed by the renderer.</param>
    /// <param name="max">Maximum local XZ corner contributed by the renderer.</param>
    /// <returns>True when usable projected bounds were resolved.</returns>
    private static bool TryResolveRendererFootprint(Renderer candidateRenderer,
                                                    Transform rootTransform,
                                                    out Vector2 min,
                                                    out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.zero;

        if (candidateRenderer == null || rootTransform == null)
            return false;

        SkinnedMeshRenderer skinnedMeshRenderer = candidateRenderer as SkinnedMeshRenderer;

        if (skinnedMeshRenderer != null)
        {
            if (TryResolveLocalBoundsFootprint(skinnedMeshRenderer.localBounds,
                                               skinnedMeshRenderer.transform,
                                               rootTransform,
                                               out min,
                                               out max))
                return true;

            if (skinnedMeshRenderer.sharedMesh != null)
                return TryResolveLocalBoundsFootprint(skinnedMeshRenderer.sharedMesh.bounds,
                                                      skinnedMeshRenderer.transform,
                                                      rootTransform,
                                                      out min,
                                                      out max);
        }

        MeshFilter meshFilter = candidateRenderer.GetComponent<MeshFilter>();

        if (meshFilter != null && meshFilter.sharedMesh != null)
            return TryResolveLocalBoundsFootprint(meshFilter.sharedMesh.bounds,
                                                  meshFilter.transform,
                                                  rootTransform,
                                                  out min,
                                                  out max);

        SpriteRenderer spriteRenderer = candidateRenderer as SpriteRenderer;

        if (spriteRenderer != null && spriteRenderer.sprite != null)
            return TryResolveLocalBoundsFootprint(spriteRenderer.sprite.bounds,
                                                  spriteRenderer.transform,
                                                  rootTransform,
                                                  out min,
                                                  out max);

        return TryResolveWorldBoundsFootprint(candidateRenderer.bounds, rootTransform, out min, out max);
    }

    /// <summary>
    /// Converts renderer-local bounds corners into enemy-root local XZ footprint bounds.
    /// </summary>
    /// <param name="localBounds">Bounds expressed in the renderer transform local space.</param>
    /// <param name="sourceTransform">Renderer transform owning the local bounds.</param>
    /// <param name="rootTransform">Enemy root transform receiving projected local XZ bounds.</param>
    /// <param name="min">Minimum projected local XZ corner.</param>
    /// <param name="max">Maximum projected local XZ corner.</param>
    /// <returns>True when bounds are non-degenerate and transforms are valid.</returns>
    private static bool TryResolveLocalBoundsFootprint(Bounds localBounds,
                                                       Transform sourceTransform,
                                                       Transform rootTransform,
                                                       out Vector2 min,
                                                       out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.zero;

        if (sourceTransform == null || rootTransform == null || !IsUsableBounds(localBounds))
            return false;

        bool hasCorner = false;

        // Project all local bounds corners into root space so rotated imports still produce stable centers.
        for (int xSign = -1; xSign <= 1; xSign += 2)
        {
            for (int ySign = -1; ySign <= 1; ySign += 2)
            {
                for (int zSign = -1; zSign <= 1; zSign += 2)
                {
                    Vector3 localCorner = ResolveBoundsCorner(localBounds, xSign, ySign, zSign);
                    Vector3 worldCorner = sourceTransform.TransformPoint(localCorner);
                    Vector3 rootLocalCorner = rootTransform.InverseTransformPoint(worldCorner);
                    EncapsulatePoint(new Vector2(rootLocalCorner.x, rootLocalCorner.z), ref min, ref max, ref hasCorner);
                }
            }
        }

        return hasCorner;
    }

    /// <summary>
    /// Converts world-space renderer bounds into enemy-root local XZ footprint bounds as a fallback path.
    /// </summary>
    /// <param name="worldBounds">World-space renderer bounds.</param>
    /// <param name="rootTransform">Enemy root transform receiving projected local XZ bounds.</param>
    /// <param name="min">Minimum projected local XZ corner.</param>
    /// <param name="max">Maximum projected local XZ corner.</param>
    /// <returns>True when bounds are non-degenerate and the root transform is valid.</returns>
    private static bool TryResolveWorldBoundsFootprint(Bounds worldBounds, Transform rootTransform, out Vector2 min, out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.zero;

        if (rootTransform == null || !IsUsableBounds(worldBounds))
            return false;

        bool hasCorner = false;

        // World bounds are less precise than mesh-local bounds but keep non-mesh renderers supported.
        for (int xSign = -1; xSign <= 1; xSign += 2)
        {
            for (int ySign = -1; ySign <= 1; ySign += 2)
            {
                for (int zSign = -1; zSign <= 1; zSign += 2)
                {
                    Vector3 worldCorner = ResolveBoundsCorner(worldBounds, xSign, ySign, zSign);
                    Vector3 rootLocalCorner = rootTransform.InverseTransformPoint(worldCorner);
                    EncapsulatePoint(new Vector2(rootLocalCorner.x, rootLocalCorner.z), ref min, ref max, ref hasCorner);
                }
            }
        }

        return hasCorner;
    }
    #endregion

    #region Bounds Math
    /// <summary>
    /// Returns whether bounds have enough projected size to contribute a meaningful visual body center.
    /// </summary>
    /// <param name="bounds">Bounds candidate to inspect.</param>
    /// <returns>True when the bounds size is non-degenerate.</returns>
    private static bool IsUsableBounds(Bounds bounds)
    {
        return bounds.size.sqrMagnitude > BoundsEpsilon;
    }

    /// <summary>
    /// Resolves one signed bounds corner without allocating temporary arrays.
    /// </summary>
    /// <param name="bounds">Bounds supplying center and extents.</param>
    /// <param name="xSign">Signed X extent multiplier.</param>
    /// <param name="ySign">Signed Y extent multiplier.</param>
    /// <param name="zSign">Signed Z extent multiplier.</param>
    /// <returns>Bounds corner in the bounds source space.</returns>
    private static Vector3 ResolveBoundsCorner(Bounds bounds, int xSign, int ySign, int zSign)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        return new Vector3(center.x + extents.x * xSign,
                           center.y + extents.y * ySign,
                           center.z + extents.z * zSign);
    }

    /// <summary>
    /// Expands aggregate projected bounds with one renderer's projected bounds.
    /// </summary>
    /// <param name="sourceMin">Renderer minimum projected local XZ corner.</param>
    /// <param name="sourceMax">Renderer maximum projected local XZ corner.</param>
    /// <param name="min">Aggregate minimum projected local XZ corner.</param>
    /// <param name="max">Aggregate maximum projected local XZ corner.</param>
    /// <param name="hasBounds">Whether aggregate bounds have already received at least one renderer.</param>
    private static void EncapsulateBounds(Vector2 sourceMin,
                                          Vector2 sourceMax,
                                          ref Vector2 min,
                                          ref Vector2 max,
                                          ref bool hasBounds)
    {
        EncapsulatePoint(sourceMin, ref min, ref max, ref hasBounds);
        EncapsulatePoint(sourceMax, ref min, ref max, ref hasBounds);
    }

    /// <summary>
    /// Expands projected bounds with one local XZ point.
    /// </summary>
    /// <param name="point">Projected local XZ point to add.</param>
    /// <param name="min">Current minimum projected local XZ corner.</param>
    /// <param name="max">Current maximum projected local XZ corner.</param>
    /// <param name="hasBounds">Whether bounds already contain a point.</param>
    private static void EncapsulatePoint(Vector2 point, ref Vector2 min, ref Vector2 max, ref bool hasBounds)
    {
        if (!hasBounds)
        {
            min = point;
            max = point;
            hasBounds = true;
            return;
        }

        min = Vector2.Min(min, point);
        max = Vector2.Max(max, point);
    }
    #endregion

    #region Text
    /// <summary>
    /// Performs an ordinal case-insensitive containment check without culture-dependent comparisons.
    /// </summary>
    /// <param name="value">Source text to inspect.</param>
    /// <param name="token">Token expected inside the source text.</param>
    /// <returns>True when the token is found in the source text.</returns>
    private static bool ContainsOrdinalIgnoreCase(string value, string token)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(token))
            return false;

        return value.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
    #endregion

    #endregion
}
