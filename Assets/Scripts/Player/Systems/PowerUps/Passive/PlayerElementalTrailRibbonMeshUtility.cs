using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds and updates camera-independent managed mesh ribbons for the Elemental Trail passive presentation.
/// /params None.
/// /returns None.
/// </summary>
internal static class PlayerElementalTrailRibbonMeshUtility
{
    #region Constants
    private const int InitialVertexCapacity = 256;
    private const int InitialTriangleCapacity = 768;
    private const int MaximumPointCount = 256;
    private const float MinimumLifetimeSeconds = 0.05f;
    private const float MinimumSampleDistance = 0.03f;
    private const float MinimumWidth = 0.01f;
    private const float TangentEpsilonSquared = 0.000001f;
    private const float BoundsPadding = 2f;
    #endregion

    #region Methods

    #region Creation
    /// <summary>
    /// Creates one managed ribbon mesh instance using the first TrailRenderer found on the authored prefab as visual template.
    /// /params sourcePrefab Authored VFX prefab assigned by the visual preset or player authoring fallback.
    /// /returns Created ribbon instance, or null when the prefab cannot provide a usable TrailRenderer material.
    /// </summary>
    public static PlayerElementalTrailRibbonInstance CreateInstance(GameObject sourcePrefab)
    {
        if (sourcePrefab == null)
            return null;

        PlayerElementalTrailRibbonTemplate template;

        if (!TryBuildTemplate(sourcePrefab, out template))
            return null;

        GameObject instanceObject = new GameObject(string.Format("{0}_ElementalTrailRibbon", sourcePrefab.name));
        Mesh mesh = new Mesh
        {
            name = string.Format("{0}_Mesh", instanceObject.name)
        };
        mesh.MarkDynamic();

        MeshFilter meshFilter = instanceObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = instanceObject.AddComponent<MeshRenderer>();
        Material materialInstance = PlayerElementalTrailRibbonMaterialUtility.CreateRuntimeMaterial(template.SourceMaterial);

        if (materialInstance == null)
        {
            Object.Destroy(mesh);
            Object.Destroy(instanceObject);
            return null;
        }

        meshFilter.sharedMesh = mesh;
        ConfigureRenderer(meshRenderer, materialInstance, in template);
        instanceObject.layer = template.Layer;
        instanceObject.SetActive(false);

        return new PlayerElementalTrailRibbonInstance
        {
            SourcePrefab = sourcePrefab,
            InstanceObject = instanceObject,
            InstanceTransform = instanceObject.transform,
            Mesh = mesh,
            MeshFilter = meshFilter,
            MeshRenderer = meshRenderer,
            MaterialInstance = materialInstance,
            Template = template
        };
    }

    /// <summary>
    /// Destroys one managed ribbon instance and its runtime-only Unity objects.
    /// /params instance Managed ribbon instance to release.
    /// /returns None.
    /// </summary>
    public static void DestroyInstance(PlayerElementalTrailRibbonInstance instance)
    {
        if (instance == null)
            return;

        if (instance.MaterialInstance != null)
            Object.Destroy(instance.MaterialInstance);

        if (instance.Mesh != null)
            Object.Destroy(instance.Mesh);

        if (instance.InstanceObject != null)
            Object.Destroy(instance.InstanceObject);

        ClearManagedReferences(instance);
    }
    #endregion

    #region State
    /// <summary>
    /// Clears all ribbon samples and hides the managed renderer.
    /// /params instance Managed ribbon instance being disabled.
    /// /returns None.
    /// </summary>
    public static void SetInactive(PlayerElementalTrailRibbonInstance instance)
    {
        if (instance == null)
            return;

        instance.WasEmitting = false;
        instance.Points.Clear();
        ClearMesh(instance);

        if (instance.InstanceObject != null && instance.InstanceObject.activeSelf)
            instance.InstanceObject.SetActive(false);
    }

    /// <summary>
    /// Advances sample age, appends new movement samples and rebuilds the visible ribbon mesh.
    /// /params instance Managed ribbon instance owned by one player.
    /// /params worldPosition Current world-space emission position.
    /// /params desiredWidth Full ribbon width resolved from Elemental Trail gameplay radius and visual multiplier.
    /// /params isEmitting True while the player is moving and the passive should add new samples.
    /// /params deltaTime Frame delta time used to age and fade samples.
    /// /returns None.
    /// </summary>
    public static void UpdateInstance(PlayerElementalTrailRibbonInstance instance,
                                      float3 worldPosition,
                                      float desiredWidth,
                                      bool isEmitting,
                                      float deltaTime)
    {
        if (instance == null || instance.InstanceObject == null || instance.Mesh == null)
            return;

        AgeAndPruneSamples(instance, deltaTime);

        if (isEmitting)
        {
            AddOrMoveNewestSample(instance, worldPosition);
        }

        instance.WasEmitting = isEmitting;
        RebuildMesh(instance, math.max(MinimumWidth, desiredWidth));

        bool shouldBeVisible = instance.Points.Count > 1;

        if (instance.InstanceObject.activeSelf != shouldBeVisible)
            instance.InstanceObject.SetActive(shouldBeVisible);
    }
    #endregion

    #region Template
    /// <summary>
    /// Reads the first authored TrailRenderer as a data template for the managed ribbon mesh.
    /// /params sourcePrefab Source prefab containing a TrailRenderer template.
    /// /params template Output template containing material, gradient and sampling settings.
    /// /returns True when a usable template was found.
    /// </summary>
    private static bool TryBuildTemplate(GameObject sourcePrefab, out PlayerElementalTrailRibbonTemplate template)
    {
        template = default;

        TrailRenderer templateRenderer = ResolveTemplateRenderer(sourcePrefab);

        if (templateRenderer == null || templateRenderer.sharedMaterial == null)
            return false;

        template = new PlayerElementalTrailRibbonTemplate
        {
            SourceMaterial = templateRenderer.sharedMaterial,
            ColorGradient = CloneGradient(templateRenderer.colorGradient),
            WidthCurve = CloneWidthCurve(templateRenderer.widthCurve),
            LifetimeSeconds = math.max(MinimumLifetimeSeconds, templateRenderer.time),
            MinimumSampleDistance = math.max(MinimumSampleDistance, templateRenderer.minVertexDistance),
            TextureScale = templateRenderer.textureScale,
            SortingLayerId = templateRenderer.sortingLayerID,
            SortingOrder = templateRenderer.sortingOrder,
            Layer = templateRenderer.gameObject.layer
        };

        return true;
    }

    /// <summary>
    /// Finds the first valid TrailRenderer template inside a prefab hierarchy.
    /// /params sourcePrefab Source prefab object used only for reading authored component settings.
    /// /returns First TrailRenderer found in children, or null when none exists.
    /// </summary>
    private static TrailRenderer ResolveTemplateRenderer(GameObject sourcePrefab)
    {
        if (sourcePrefab == null)
            return null;

        TrailRenderer[] trailRenderers = sourcePrefab.GetComponentsInChildren<TrailRenderer>(true);

        if (trailRenderers == null || trailRenderers.Length <= 0)
            return null;

        for (int rendererIndex = 0; rendererIndex < trailRenderers.Length; rendererIndex++)
        {
            TrailRenderer trailRenderer = trailRenderers[rendererIndex];

            if (trailRenderer != null)
                return trailRenderer;
        }

        return null;
    }

    /// <summary>
    /// Applies renderer settings that keep the ribbon lightweight and independent from scene lighting.
    /// /params meshRenderer Runtime MeshRenderer used by the ribbon instance.
    /// /params materialInstance Runtime material instance assigned to the renderer.
    /// /params template Authored sorting data copied from the TrailRenderer template.
    /// /returns None.
    /// </summary>
    private static void ConfigureRenderer(MeshRenderer meshRenderer,
                                          Material materialInstance,
                                          in PlayerElementalTrailRibbonTemplate template)
    {
        meshRenderer.sharedMaterial = materialInstance;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.sortingLayerID = template.SortingLayerId;
        meshRenderer.sortingOrder = template.SortingOrder;
        meshRenderer.allowOcclusionWhenDynamic = false;
    }
    #endregion

    #region Sampling
    /// <summary>
    /// Ages samples and removes points that have exceeded the template lifetime.
    /// /params instance Managed ribbon instance whose point list is being updated.
    /// /params deltaTime Frame delta time in seconds.
    /// /returns None.
    /// </summary>
    private static void AgeAndPruneSamples(PlayerElementalTrailRibbonInstance instance,
                                           float deltaTime)
    {
        float lifetimeSeconds = math.max(MinimumLifetimeSeconds, instance.Template.LifetimeSeconds);

        for (int pointIndex = instance.Points.Count - 1; pointIndex >= 0; pointIndex--)
        {
            PlayerElementalTrailRibbonPoint point = instance.Points[pointIndex];
            point.AgeSeconds += math.max(0f, deltaTime);

            if (point.AgeSeconds >= lifetimeSeconds)
            {
                instance.Points.RemoveAt(pointIndex);
                continue;
            }

            instance.Points[pointIndex] = point;
        }

        while (instance.Points.Count > MaximumPointCount)
            instance.Points.RemoveAt(0);
    }

    /// <summary>
    /// Adds a new sample when the emitter moved far enough, otherwise keeps the newest sample locked to the player.
    /// /params instance Managed ribbon instance receiving the movement sample.
    /// /params worldPosition Current world-space emission position.
    /// /returns None.
    /// </summary>
    private static void AddOrMoveNewestSample(PlayerElementalTrailRibbonInstance instance,
                                              float3 worldPosition)
    {
        if (instance.Points.Count <= 0)
        {
            instance.Points.Add(new PlayerElementalTrailRibbonPoint
            {
                Position = worldPosition,
                AgeSeconds = 0f
            });
            return;
        }

        int newestIndex = instance.Points.Count - 1;
        PlayerElementalTrailRibbonPoint newestPoint = instance.Points[newestIndex];
        float3 planarDelta = worldPosition - newestPoint.Position;
        planarDelta.y = 0f;
        float minimumDistance = math.max(MinimumSampleDistance, instance.Template.MinimumSampleDistance);

        if (math.lengthsq(planarDelta) < minimumDistance * minimumDistance)
        {
            newestPoint.Position = worldPosition;
            newestPoint.AgeSeconds = 0f;
            instance.Points[newestIndex] = newestPoint;
            return;
        }

        instance.Points.Add(new PlayerElementalTrailRibbonPoint
        {
            Position = worldPosition,
            AgeSeconds = 0f
        });
    }
    #endregion

    #region Mesh
    /// <summary>
    /// Rebuilds the ribbon mesh from current samples using a stable horizontal normal and explicit bounds.
    /// /params instance Managed ribbon instance whose mesh buffers should be rebuilt.
    /// /params desiredWidth Full ribbon width in world units.
    /// /returns None.
    /// </summary>
    private static void RebuildMesh(PlayerElementalTrailRibbonInstance instance,
                                    float desiredWidth)
    {
        if (instance.Points.Count <= 1)
        {
            ClearMesh(instance);
            return;
        }

        ClearMeshBuffers(instance);
        FillMeshBuffers(instance, desiredWidth);

        instance.Mesh.Clear(false);
        instance.Mesh.SetVertices(instance.Vertices);
        instance.Mesh.SetTriangles(instance.Triangles, 0, false);
        instance.Mesh.SetColors(instance.Colors);
        instance.Mesh.SetUVs(0, instance.Uvs);
        instance.Mesh.bounds = ResolveBounds(instance.Vertices);
    }

    /// <summary>
    /// Fills reusable mesh buffers with one camera-independent quad strip.
    /// /params instance Managed ribbon instance whose buffers receive generated geometry.
    /// /params desiredWidth Full ribbon width in world units.
    /// /returns None.
    /// </summary>
    private static void FillMeshBuffers(PlayerElementalTrailRibbonInstance instance,
                                        float desiredWidth)
    {
        float cumulativeDistance = 0f;

        for (int pointIndex = 0; pointIndex < instance.Points.Count; pointIndex++)
        {
            PlayerElementalTrailRibbonPoint point = instance.Points[pointIndex];

            if (pointIndex > 0)
                cumulativeDistance += math.distance(point.Position, instance.Points[pointIndex - 1].Position);

            float normalizedAge = math.saturate(point.AgeSeconds / math.max(MinimumLifetimeSeconds, instance.Template.LifetimeSeconds));
            float widthMultiplier = math.max(0f, instance.Template.WidthCurve.Evaluate(normalizedAge));
            float halfWidth = desiredWidth * widthMultiplier * 0.5f;
            float3 tangent = ResolvePlanarTangent(instance, pointIndex);
            float3 side = math.normalize(math.cross(new float3(0f, 1f, 0f), tangent));
            Color32 color = instance.Template.ColorGradient.Evaluate(normalizedAge);
            float u = cumulativeDistance * math.max(0.01f, instance.Template.TextureScale.x);

            instance.Vertices.Add(ToVector3(point.Position - side * halfWidth));
            instance.Vertices.Add(ToVector3(point.Position + side * halfWidth));
            instance.Colors.Add(color);
            instance.Colors.Add(color);
            instance.Uvs.Add(new Vector2(u, 0f));
            instance.Uvs.Add(new Vector2(u, 1f));
        }

        for (int pointIndex = 0; pointIndex < instance.Points.Count - 1; pointIndex++)
        {
            int vertexIndex = pointIndex * 2;
            instance.Triangles.Add(vertexIndex);
            instance.Triangles.Add(vertexIndex + 2);
            instance.Triangles.Add(vertexIndex + 1);
            instance.Triangles.Add(vertexIndex + 1);
            instance.Triangles.Add(vertexIndex + 2);
            instance.Triangles.Add(vertexIndex + 3);
        }
    }

    /// <summary>
    /// Resolves the planar tangent used to orient the ribbon width at one sample.
    /// /params instance Managed ribbon instance containing the point list.
    /// /params pointIndex Index of the point whose tangent is required.
    /// /returns Normalized XZ tangent, or world forward when samples overlap.
    /// </summary>
    private static float3 ResolvePlanarTangent(PlayerElementalTrailRibbonInstance instance,
                                               int pointIndex)
    {
        int lastIndex = instance.Points.Count - 1;
        float3 tangent;

        if (pointIndex <= 0)
            tangent = instance.Points[1].Position - instance.Points[0].Position;
        else if (pointIndex >= lastIndex)
            tangent = instance.Points[lastIndex].Position - instance.Points[lastIndex - 1].Position;
        else
            tangent = instance.Points[pointIndex + 1].Position - instance.Points[pointIndex - 1].Position;

        tangent.y = 0f;

        if (math.lengthsq(tangent) <= TangentEpsilonSquared)
            return new float3(0f, 0f, 1f);

        return math.normalize(tangent);
    }

    /// <summary>
    /// Calculates padded mesh bounds from the generated vertex buffer.
    /// /params vertices Generated mesh vertex buffer.
    /// /returns Padded bounds used by Unity frustum culling.
    /// </summary>
    private static Bounds ResolveBounds(System.Collections.Generic.List<Vector3> vertices)
    {
        Vector3 minimum = vertices[0];
        Vector3 maximum = vertices[0];

        for (int vertexIndex = 1; vertexIndex < vertices.Count; vertexIndex++)
        {
            Vector3 vertex = vertices[vertexIndex];
            minimum = Vector3.Min(minimum, vertex);
            maximum = Vector3.Max(maximum, vertex);
        }

        Bounds bounds = new Bounds((minimum + maximum) * 0.5f, maximum - minimum);
        bounds.Expand(BoundsPadding);
        return bounds;
    }

    /// <summary>
    /// Clears mesh data and reusable geometry buffers.
    /// /params instance Managed ribbon instance whose mesh should be emptied.
    /// /returns None.
    /// </summary>
    private static void ClearMesh(PlayerElementalTrailRibbonInstance instance)
    {
        ClearMeshBuffers(instance);

        if (instance.Mesh != null)
            instance.Mesh.Clear(false);
    }

    /// <summary>
    /// Clears reusable geometry buffers without releasing their allocated capacity.
    /// /params instance Managed ribbon instance whose buffers should be cleared.
    /// /returns None.
    /// </summary>
    private static void ClearMeshBuffers(PlayerElementalTrailRibbonInstance instance)
    {
        instance.Vertices.Clear();
        instance.Triangles.Clear();
        instance.Colors.Clear();
        instance.Uvs.Clear();
        EnsureBufferCapacity(instance);
    }

    /// <summary>
    /// Keeps reusable buffer capacity large enough for the common ribbon size.
    /// /params instance Managed ribbon instance whose buffers should be pre-sized.
    /// /returns None.
    /// </summary>
    private static void EnsureBufferCapacity(PlayerElementalTrailRibbonInstance instance)
    {
        if (instance.Vertices.Capacity < InitialVertexCapacity)
            instance.Vertices.Capacity = InitialVertexCapacity;

        if (instance.Colors.Capacity < InitialVertexCapacity)
            instance.Colors.Capacity = InitialVertexCapacity;

        if (instance.Uvs.Capacity < InitialVertexCapacity)
            instance.Uvs.Capacity = InitialVertexCapacity;

        if (instance.Triangles.Capacity < InitialTriangleCapacity)
            instance.Triangles.Capacity = InitialTriangleCapacity;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Clones a Gradient so runtime edits cannot mutate the authored prefab template.
    /// /params source Source Gradient from the TrailRenderer template.
    /// /returns Independent Gradient instance.
    /// </summary>
    private static Gradient CloneGradient(Gradient source)
    {
        Gradient gradient = new Gradient();

        if (source == null)
        {
            gradient.SetKeys(new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
            return gradient;
        }

        gradient.SetKeys(source.colorKeys, source.alphaKeys);
        return gradient;
    }

    /// <summary>
    /// Clones a width curve so the managed mesh can evaluate authored width over trail lifetime.
    /// /params source Source AnimationCurve from the TrailRenderer template.
    /// /returns Independent width curve with a constant fallback.
    /// </summary>
    private static AnimationCurve CloneWidthCurve(AnimationCurve source)
    {
        if (source == null || source.length <= 0)
            return AnimationCurve.Constant(0f, 1f, 1f);

        return new AnimationCurve(source.keys);
    }

    /// <summary>
    /// Clears managed references after their Unity objects have been destroyed.
    /// /params instance Managed ribbon instance being invalidated.
    /// /returns None.
    /// </summary>
    private static void ClearManagedReferences(PlayerElementalTrailRibbonInstance instance)
    {
        instance.SourcePrefab = null;
        instance.InstanceObject = null;
        instance.InstanceTransform = null;
        instance.Mesh = null;
        instance.MeshFilter = null;
        instance.MeshRenderer = null;
        instance.MaterialInstance = null;
        instance.Points.Clear();
        ClearMeshBuffers(instance);
        instance.WasEmitting = false;
    }

    /// <summary>
    /// Converts a DOTS float3 to a managed Vector3.
    /// /params value Source DOTS vector.
    /// /returns Managed Vector3 with matching components.
    /// </summary>
    private static Vector3 ToVector3(float3 value)
    {
        return new Vector3(value.x, value.y, value.z);
    }
    #endregion

    #endregion
}
