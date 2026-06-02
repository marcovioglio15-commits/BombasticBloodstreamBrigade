using UnityEditor;
using UnityEngine;

#region Utilities
/// <summary>
/// Authors the Acid Puddle hierarchy used by PF_VFX_AcidPuddle. The effect is split into a mesh-based
/// posterized liquid surface, a drifting gas plume, and a rising bubble/pop particle layer that grows,
/// ascends and collapses to read as bubbles bursting at the surface.
/// </summary>
internal static class AcidPuddleVfxBuilderUtility
{
    #region Constants

    #region Material GUIDs
    // GUIDs are resolved through AssetDatabase so material renames remain safe while missing assets report warnings.
    private const string PuddleSurfaceMaterialGuid = "b1e5d4f3a72c4f08b0d7a6e1c2f3a401";
    private const string GasMaterialGuid = "e4b8a7c6d05f7231e3a0d9b4f5c6d704";
    private const string BubblePopMaterialGuid = "d3a7f6b5c94e6120d2f9c8a3e4b5c603";
    #endregion

    #region Child Names
    private const string LiquidSurfaceChildName = "FX_AcidLiquidSurface";
    private const string GasChildName = "FX_AcidRisingGas";
    private const string BubblePopsChildName = "FX_AcidBubblePops";
    #endregion

    #region Surface Transform
    private static readonly Vector3 LiquidSurfaceLocalScale = new Vector3(1.22f, 1.04f, 1.00f);
    private static readonly Vector3 LiquidSurfaceLocalPosition = new Vector3(0f, 0.012f, 0f);
    private static readonly Quaternion LiquidSurfaceLocalRotation = Quaternion.Euler(90f, 0f, 0f);
    private const string BuiltInQuadMeshResource = "Quad.fbx";
    #endregion

    #region Palette
    private static readonly Color GasTint = new Color(0.72f, 1.00f, 0.28f, 0.62f);
    private static readonly Color GasTintFade = new Color(0.78f, 1.00f, 0.42f, 0.12f);
    private static readonly Color BubbleTint = new Color(0.78f, 1.00f, 0.36f, 0.90f);
    private static readonly Color BubbleTintFade = new Color(0.42f, 0.88f, 0.16f, 0.00f);
    #endregion

    #endregion

    #region Methods

    #region Public Builder Entry
    /// <summary>
    /// Composes the full Acid Puddle hierarchy ready to be saved as a prefab. The caller owns the returned
    /// authored root and is responsible for persisting it as a prefab.
    /// </summary>
    /// <param name="rootObjectName">Name to assign to the authored root GameObject.</param>
    /// <returns>Authored root GameObject hosting the liquid, gas and bubble/pop children.</returns>
    public static GameObject BuildHierarchy(string rootObjectName)
    {
        GameObject rootObject = new GameObject(rootObjectName);
        rootObject.transform.localScale = Vector3.one;

        // Children are authored in the same visual order as the prefab: ground surface, gas, bubble pops.
        BuildLiquidSurfaceChild(rootObject);
        BuildGasChild(rootObject);
        BuildBubblePopsChild(rootObject);

        return rootObject;
    }
    #endregion

    #region Liquid Surface
    /// <summary>
    /// Builds the flat liquid quad child. The shared acid shader handles the puddle silhouette, cel ripples,
    /// dark border and posterized highlight bands through M_VFX_AcidPuddleBody.
    /// </summary>
    /// <param name="parent">Parent GameObject owning the new child.</param>
    private static void BuildLiquidSurfaceChild(GameObject parent)
    {
        GameObject child = new GameObject(LiquidSurfaceChildName);
        child.transform.SetParent(parent.transform, false);
        child.transform.localPosition = LiquidSurfaceLocalPosition;
        child.transform.localRotation = LiquidSurfaceLocalRotation;
        child.transform.localScale = LiquidSurfaceLocalScale;

        // Resolve Unity's built-in quad mesh; warn instead of throwing so the authoring menu remains recoverable.
        MeshFilter meshFilter = child.AddComponent<MeshFilter>();
        Mesh quadMesh = Resources.GetBuiltinResource<Mesh>(BuiltInQuadMeshResource);

        if (quadMesh != null)
            meshFilter.sharedMesh = quadMesh;
        else
            Debug.LogWarning("[AcidPuddleVfxBuilder] Missing built-in Quad mesh; liquid surface will be empty.");

        // Transparent unlit VFX should not cast/receive shadows or write motion vectors.
        MeshRenderer meshRenderer = child.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        meshRenderer.allowOcclusionWhenDynamic = true;
        meshRenderer.sharedMaterial = ResolveMaterialByGuid(PuddleSurfaceMaterialGuid);
        meshRenderer.sortingOrder = 0;
    }
    #endregion

    #region Gas Mist
    /// <summary>
    /// Builds the rising acid gas child. The plume stays narrow and low-density so it communicates toxicity
    /// without competing with gameplay bullets and enemy silhouettes.
    /// </summary>
    /// <param name="parent">Parent GameObject owning the new child.</param>
    private static void BuildGasChild(GameObject parent)
    {
        GameObject child = ParticleVfxPrefabBuilderEditorUtility.CreateParticleChild(parent,
                                                                                     GasChildName,
                                                                                     new Vector3(0f, 0.035f, 0f),
                                                                                     Quaternion.Euler(-90f, 0f, 0f));
        ParticleSystem particleSystem = child.GetComponent<ParticleSystem>();

        // Slow upward emission creates a toxic vapor column above the liquid surface.
        ParticleVfxPrefabBuilderEditorUtility.ConfigureMainModule(particleSystem,
                                                                  duration: 2.0f,
                                                                  looping: true,
                                                                  startLifetime: 1.6f,
                                                                  startSpeed: 0.18f,
                                                                  startSize: 0.34f,
                                                                  startColor: GasTint,
                                                                  maxParticles: 32);

        ParticleSystem.EmissionModule emissionModule = particleSystem.emission;
        emissionModule.rateOverTime = 6f;

        // A broad hemisphere keeps the mist grounded while still allowing particles to drift upward.
        ParticleSystem.ShapeModule shapeModule = particleSystem.shape;
        shapeModule.shapeType = ParticleSystemShapeType.Hemisphere;
        shapeModule.radius = 0.40f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetimeModule = particleSystem.colorOverLifetime;
        colorOverLifetimeModule.enabled = true;
        colorOverLifetimeModule.color = ParticleVfxPrefabBuilderEditorUtility.BuildColorGradient(GasTint,
                                                                                                 GasTintFade);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetimeModule = particleSystem.sizeOverLifetime;
        sizeOverLifetimeModule.enabled = true;
        sizeOverLifetimeModule.size = new ParticleSystem.MinMaxCurve(1f,
                                                                     ParticleVfxPrefabBuilderEditorUtility
                                                                         .BuildSizeCurve(0.45f, 1.15f, 1.35f));

        // Low-frequency noise gives the gas a drifting chemical feel without a high particle count.
        ParticleSystem.NoiseModule noiseModule = particleSystem.noise;
        noiseModule.enabled = true;
        noiseModule.strength = 0.18f;
        noiseModule.frequency = 0.45f;
        noiseModule.scrollSpeed = 0.55f;

        ApplyAcidRenderer(child, ParticleSystemRenderMode.Billboard, GasMaterialGuid, 1);
    }
    #endregion

    #region Bubble Pops
    /// <summary>
    /// Builds the rising bubble/pop child. Particles spawn inside the puddle, rise briefly, expand and collapse
    /// with the pop-ring shader role so the layer reads as bubbles breaking the acid surface.
    /// </summary>
    /// <param name="parent">Parent GameObject owning the new child.</param>
    private static void BuildBubblePopsChild(GameObject parent)
    {
        GameObject child = ParticleVfxPrefabBuilderEditorUtility.CreateParticleChild(parent,
                                                                                     BubblePopsChildName,
                                                                                     new Vector3(0f, 0.08f, 0f),
                                                                                     Quaternion.identity);
        ParticleSystem particleSystem = child.GetComponent<ParticleSystem>();

        // Short lifetime and upward speed make particles read as bubbles that rise and burst quickly.
        ParticleVfxPrefabBuilderEditorUtility.ConfigureMainModule(particleSystem,
                                                                  duration: 1.2f,
                                                                  looping: true,
                                                                  startLifetime: 0.35f,
                                                                  startSpeed: 0.60f,
                                                                  startSize: 0.12f,
                                                                  startColor: BubbleTint,
                                                                  maxParticles: 24);

        ParticleSystem.EmissionModule emissionModule = particleSystem.emission;
        emissionModule.rateOverTime = 14f;

        ParticleSystem.ShapeModule shapeModule = particleSystem.shape;
        shapeModule.shapeType = ParticleSystemShapeType.Hemisphere;
        shapeModule.radius = 0.25f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetimeModule = particleSystem.colorOverLifetime;
        colorOverLifetimeModule.enabled = true;
        colorOverLifetimeModule.color = ParticleVfxPrefabBuilderEditorUtility.BuildColorGradient(BubbleTint,
                                                                                                 BubbleTintFade);

        // Growth then collapse mimics bubbles swelling before they pop.
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetimeModule = particleSystem.sizeOverLifetime;
        sizeOverLifetimeModule.enabled = true;
        sizeOverLifetimeModule.size = new ParticleSystem.MinMaxCurve(1f,
                                                                     ParticleVfxPrefabBuilderEditorUtility
                                                                         .BuildSizeCurve(0.4f, 1.4f, 0.0f));

        ParticleSystem.NoiseModule noiseModule = particleSystem.noise;
        noiseModule.enabled = true;
        noiseModule.strength = 0.22f;
        noiseModule.frequency = 0.55f;
        noiseModule.scrollSpeed = 0.4f;

        ApplyAcidRenderer(child, ParticleSystemRenderMode.Billboard, BubblePopMaterialGuid, 2);
    }
    #endregion

    #region Material Wiring
    /// <summary>
    /// Wraps <see cref="ParticleVfxPrefabBuilderEditorUtility.ApplyRenderer"/> with material resolution and
    /// sorting-order assignment for one acid particle child.
    /// </summary>
    /// <param name="particleObject">GameObject owning the renderer to configure.</param>
    /// <param name="renderMode">Particle render mode to apply.</param>
    /// <param name="materialGuid">GUID of the .mat asset to assign.</param>
    /// <param name="sortingOrder">Transparent sorting order used by the renderer.</param>
    private static void ApplyAcidRenderer(GameObject particleObject,
                                          ParticleSystemRenderMode renderMode,
                                          string materialGuid,
                                          int sortingOrder)
    {
        Material material = ResolveMaterialByGuid(materialGuid);
        ParticleVfxPrefabBuilderEditorUtility.ApplyRenderer(particleObject, renderMode, material);

        // Sorting is set after shared renderer setup so layered transparent VFX stay deterministic.
        ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();

        if (renderer == null)
            return;

        renderer.sortingOrder = sortingOrder;
    }

    /// <summary>
    /// Loads a material asset by GUID through AssetDatabase. Returns null when the asset is missing so renderer
    /// setup can fall back to the default particle material and emit a warning.
    /// </summary>
    /// <param name="materialGuid">GUID of the .mat asset to resolve.</param>
    /// <returns>Loaded material, or null if the GUID is not registered.</returns>
    private static Material ResolveMaterialByGuid(string materialGuid)
    {
        string assetPath = AssetDatabase.GUIDToAssetPath(materialGuid);

        if (string.IsNullOrEmpty(assetPath))
        {
            Debug.LogWarning(string.Format("[AcidPuddleVfxBuilder] Missing material asset for GUID {0}.",
                                           materialGuid));
            return null;
        }

        Material loadedMaterial = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

        if (loadedMaterial == null)
            Debug.LogWarning(string.Format("[AcidPuddleVfxBuilder] Failed to load material at {0}.", assetPath));

        return loadedMaterial;
    }
    #endregion

    #endregion
}
#endregion
