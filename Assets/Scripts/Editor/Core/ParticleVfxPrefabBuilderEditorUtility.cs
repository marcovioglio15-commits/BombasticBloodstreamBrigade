using UnityEditor;
using UnityEngine;

#region Utilities
/// <summary>
/// Editor menu utility that materializes hierarchical particle-system VFX prefabs (PF_VFX_AcidPuddle,
/// PF_VFX_FireZone) directly under Assets/3D/VFX/VFX_remastered. The acid puddle authoring lives in
/// <see cref="AcidPuddleVfxBuilderUtility"/>; fire zone authoring remains inline here. Shared low-level helpers
/// (particle child creation, main module defaults, gradient and size-curve builders, renderer wiring, prefab save)
/// are exposed as <c>internal static</c> so authoring utilities can share one editor-only generation path.
/// </summary>
public static class ParticleVfxPrefabBuilderEditorUtility
{
    #region Constants
    private const string OutputFolderRelative = "Assets/3D/VFX/VFX_remastered";
    private const string AcidPuddlePrefabName = "PF_VFX_AcidPuddle";
    private const string FireZonePrefabName = "PF_VFX_FireZone";
    #endregion

    #region Methods

    #region Menu Entries
    /// <summary>
    /// Builds and saves the Acid Puddle hierarchical particle VFX prefab. Delegates the hierarchy authoring to
    /// <see cref="AcidPuddleVfxBuilderUtility"/> so the layered structure (liquid surface, drifting gas, rising
    /// popping bubbles) and material wiring stay isolated from the menu plumbing.
    /// </summary>
    //[MenuItem("Tools/NashCore/Build Particle VFX/Build Acid Puddle Prefab", priority = 510)]
    public static void BuildAcidPuddlePrefab()
    {
        GameObject rootObject = AcidPuddleVfxBuilderUtility.BuildHierarchy(AcidPuddlePrefabName);
        SaveAsPrefab(rootObject, AcidPuddlePrefabName);
    }

    /// <summary>
    /// Builds and saves the Fire Zone hierarchical particle VFX prefab.
    /// </summary>
    //[MenuItem("Tools/NashCore/Build Particle VFX/Build Fire Zone Prefab", priority = 511)]
    public static void BuildFireZonePrefab()
    {
        GameObject rootObject = BuildFireZoneHierarchy();
        SaveAsPrefab(rootObject, FireZonePrefabName);
    }

    /// <summary>
    /// Builds and saves both prefabs in one click.
    /// </summary>
    //[MenuItem("Tools/NashCore/Build Particle VFX/Build Both Prefabs", priority = 520)]
    public static void BuildBothPrefabs()
    {
        BuildAcidPuddlePrefab();
        BuildFireZonePrefab();
    }
    #endregion

    #region Fire Zone Builder
    /// <summary>
    /// Composes the three-child Fire Zone hierarchy: flame body, sparks/embers, smoke drift.
    /// </summary>
    /// <returns>Authored root GameObject to be saved as prefab.</returns>
    private static GameObject BuildFireZoneHierarchy()
    {
        GameObject rootObject = new GameObject(FireZonePrefabName);
        rootObject.transform.localScale = Vector3.one;

        Color fireOrange = new Color(1f, 0.55f, 0.18f, 1f);
        Color fireYellow = new Color(1f, 0.85f, 0.32f, 0.85f);
        Color fireRedFade = new Color(0.95f, 0.22f, 0.05f, 0f);
        Color smokeColor = new Color(0.18f, 0.18f, 0.2f, 0.5f);
        Color smokeFade = new Color(0.05f, 0.05f, 0.06f, 0f);

        BuildFlameBodyChild(rootObject, "FX_FlameBody", fireOrange, fireRedFade);
        BuildEmbersChild(rootObject, "FX_FireEmbers", fireYellow, fireRedFade);
        BuildSmokeChild(rootObject, "FX_FireSmoke", smokeColor, smokeFade);

        return rootObject;
    }

    /// <summary>
    /// Builds the upward flame body child for Fire Zone.
    /// </summary>
    /// <param name="parent">Parent GameObject owning the hierarchy.</param>
    /// <param name="childName">Name to assign to the new child GameObject.</param>
    /// <param name="flameColor">Bright flame color sampled at particle birth.</param>
    /// <param name="flameFade">Faded smoke-like color used at particle death.</param>
    private static void BuildFlameBodyChild(GameObject parent,
                                            string childName,
                                            Color flameColor,
                                            Color flameFade)
    {
        GameObject child = CreateParticleChild(parent, childName, Vector3.zero, Quaternion.identity);
        ParticleSystem particleSystem = child.GetComponent<ParticleSystem>();

        ConfigureMainModule(particleSystem,
                            duration: 1.2f,
                            looping: true,
                            startLifetime: 0.65f,
                            startSpeed: 1.1f,
                            startSize: 0.85f,
                            startColor: flameColor,
                            maxParticles: 64);

        ParticleSystem.EmissionModule emissionModule = particleSystem.emission;
        emissionModule.rateOverTime = 35f;

        ParticleSystem.ShapeModule shapeModule = particleSystem.shape;
        shapeModule.shapeType = ParticleSystemShapeType.Cone;
        shapeModule.angle = 18f;
        shapeModule.radius = 0.32f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetimeModule = particleSystem.colorOverLifetime;
        colorOverLifetimeModule.enabled = true;
        colorOverLifetimeModule.color = BuildColorGradient(flameColor, flameFade);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetimeModule = particleSystem.sizeOverLifetime;
        sizeOverLifetimeModule.enabled = true;
        sizeOverLifetimeModule.size = new ParticleSystem.MinMaxCurve(1f, BuildSizeCurve(0.8f, 1.2f, 0.35f));

        ParticleSystem.NoiseModule noiseModule = particleSystem.noise;
        noiseModule.enabled = true;
        noiseModule.strength = 0.4f;
        noiseModule.frequency = 1.1f;
        noiseModule.scrollSpeed = 1.6f;

        ApplyRenderer(child, ParticleSystemRenderMode.Billboard, null);
    }

    /// <summary>
    /// Builds the bright spark/ember child for Fire Zone.
    /// </summary>
    /// <param name="parent">Parent GameObject owning the hierarchy.</param>
    /// <param name="childName">Name to assign to the new child GameObject.</param>
    /// <param name="emberColor">Bright ember color sampled at particle birth.</param>
    /// <param name="emberFade">Faded ember color used at particle death.</param>
    private static void BuildEmbersChild(GameObject parent,
                                         string childName,
                                         Color emberColor,
                                         Color emberFade)
    {
        GameObject child = CreateParticleChild(parent, childName, new Vector3(0f, 0.15f, 0f), Quaternion.identity);
        ParticleSystem particleSystem = child.GetComponent<ParticleSystem>();

        ConfigureMainModule(particleSystem,
                            duration: 1.2f,
                            looping: true,
                            startLifetime: 1.1f,
                            startSpeed: 1.7f,
                            startSize: 0.08f,
                            startColor: emberColor,
                            maxParticles: 96);

        ParticleSystem.EmissionModule emissionModule = particleSystem.emission;
        emissionModule.rateOverTime = 40f;

        ParticleSystem.ShapeModule shapeModule = particleSystem.shape;
        shapeModule.shapeType = ParticleSystemShapeType.Cone;
        shapeModule.angle = 28f;
        shapeModule.radius = 0.18f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetimeModule = particleSystem.colorOverLifetime;
        colorOverLifetimeModule.enabled = true;
        colorOverLifetimeModule.color = BuildColorGradient(emberColor, emberFade);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetimeModule = particleSystem.sizeOverLifetime;
        sizeOverLifetimeModule.enabled = true;
        sizeOverLifetimeModule.size = new ParticleSystem.MinMaxCurve(1f, BuildSizeCurve(1f, 0.6f, 0.1f));

        ApplyRenderer(child, ParticleSystemRenderMode.Billboard, null);
    }

    /// <summary>
    /// Builds the drifting smoke child for Fire Zone.
    /// </summary>
    /// <param name="parent">Parent GameObject owning the hierarchy.</param>
    /// <param name="childName">Name to assign to the new child GameObject.</param>
    /// <param name="smokeColor">Smoke body color sampled at particle birth.</param>
    /// <param name="smokeFade">Faded smoke color used at particle death.</param>
    private static void BuildSmokeChild(GameObject parent,
                                        string childName,
                                        Color smokeColor,
                                        Color smokeFade)
    {
        GameObject child = CreateParticleChild(parent, childName, new Vector3(0f, 0.65f, 0f), Quaternion.identity);
        ParticleSystem particleSystem = child.GetComponent<ParticleSystem>();

        ConfigureMainModule(particleSystem,
                            duration: 1.2f,
                            looping: true,
                            startLifetime: 1.8f,
                            startSpeed: 0.75f,
                            startSize: 1.1f,
                            startColor: smokeColor,
                            maxParticles: 48);

        ParticleSystem.EmissionModule emissionModule = particleSystem.emission;
        emissionModule.rateOverTime = 14f;

        ParticleSystem.ShapeModule shapeModule = particleSystem.shape;
        shapeModule.shapeType = ParticleSystemShapeType.Cone;
        shapeModule.angle = 12f;
        shapeModule.radius = 0.25f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetimeModule = particleSystem.colorOverLifetime;
        colorOverLifetimeModule.enabled = true;
        colorOverLifetimeModule.color = BuildColorGradient(smokeColor, smokeFade);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetimeModule = particleSystem.sizeOverLifetime;
        sizeOverLifetimeModule.enabled = true;
        sizeOverLifetimeModule.size = new ParticleSystem.MinMaxCurve(1f, BuildSizeCurve(0.6f, 1.6f, 1.9f));

        ParticleSystem.NoiseModule noiseModule = particleSystem.noise;
        noiseModule.enabled = true;
        noiseModule.strength = 0.3f;
        noiseModule.frequency = 0.45f;
        noiseModule.scrollSpeed = 0.6f;

        ApplyRenderer(child, ParticleSystemRenderMode.Billboard, null);
    }
    #endregion

    #region Shared Builders
    /// <summary>
    /// Creates one child GameObject parented to <paramref name="parent"/> already carrying a ParticleSystem
    /// component. Exposed as <c>internal</c> so authoring utilities (e.g. <see cref="AcidPuddleVfxBuilderUtility"/>)
    /// can share the same child-creation contract.
    /// </summary>
    /// <param name="parent">Parent GameObject owning the new child.</param>
    /// <param name="childName">Name to assign to the new child.</param>
    /// <param name="localPosition">Local position to apply on the child.</param>
    /// <param name="localRotation">Local rotation to apply on the child.</param>
    /// <returns>Newly created child GameObject.</returns>
    internal static GameObject CreateParticleChild(GameObject parent,
                                                   string childName,
                                                   Vector3 localPosition,
                                                   Quaternion localRotation)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(parent.transform, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = localRotation;
        child.transform.localScale = Vector3.one;
        child.AddComponent<ParticleSystem>();
        return child;
    }

    /// <summary>
    /// Configures the ParticleSystem main module with the most common authored values used by these prefabs.
    /// Exposed as <c>internal</c> so authoring utilities share a single contract for main-module defaults.
    /// </summary>
    /// <param name="particleSystem">ParticleSystem owning the main module.</param>
    /// <param name="duration">Loop length in seconds.</param>
    /// <param name="looping">When true, system re-emits indefinitely.</param>
    /// <param name="startLifetime">Per-particle lifetime in seconds.</param>
    /// <param name="startSpeed">Per-particle starting speed in meters per second.</param>
    /// <param name="startSize">Per-particle starting uniform size.</param>
    /// <param name="startColor">Per-particle starting color.</param>
    /// <param name="maxParticles">Cap on simultaneous particles.</param>
    internal static void ConfigureMainModule(ParticleSystem particleSystem,
                                             float duration,
                                             bool looping,
                                             float startLifetime,
                                             float startSpeed,
                                             float startSize,
                                             Color startColor,
                                             int maxParticles)
    {
        ParticleSystem.MainModule mainModule = particleSystem.main;
        mainModule.duration = duration;
        mainModule.loop = looping;
        mainModule.startLifetime = startLifetime;
        mainModule.startSpeed = startSpeed;
        mainModule.startSize = startSize;
        mainModule.startColor = startColor;
        mainModule.maxParticles = maxParticles;
        mainModule.scalingMode = ParticleSystemScalingMode.Local;
        mainModule.playOnAwake = true;
    }

    /// <summary>
    /// Builds a two-stop alpha+color gradient used by the color-over-lifetime modules. Exposed as
    /// <c>internal</c> so authoring utilities share the same gradient construction.
    /// </summary>
    /// <param name="startColor">Color sampled at particle birth.</param>
    /// <param name="endColor">Color sampled at particle death.</param>
    /// <returns>Min-max gradient containing the requested ramp.</returns>
    internal static ParticleSystem.MinMaxGradient BuildColorGradient(Color startColor, Color endColor)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(endColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(startColor.a, 0f),
                new GradientAlphaKey(endColor.a, 1f)
            });
        return new ParticleSystem.MinMaxGradient(gradient);
    }

    /// <summary>
    /// Builds a 3-key animation curve sampled by the size-over-lifetime module. Exposed as <c>internal</c> so
    /// authoring utilities share the same shape contract.
    /// </summary>
    /// <param name="startValue">Value sampled at lifetime 0.</param>
    /// <param name="midValue">Value sampled at lifetime 0.5.</param>
    /// <param name="endValue">Value sampled at lifetime 1.</param>
    /// <returns>Animation curve usable by min-max curves.</returns>
    internal static AnimationCurve BuildSizeCurve(float startValue, float midValue, float endValue)
    {
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(new Keyframe(0f, startValue));
        curve.AddKey(new Keyframe(0.5f, midValue));
        curve.AddKey(new Keyframe(1f, endValue));
        return curve;
    }

    /// <summary>
    /// Applies a render mode and material to one ParticleSystemRenderer. When <paramref name="material"/> is
    /// null the renderer falls back to the editor's built-in Default-Particle material so the prefab does not
    /// render magenta in case a custom material is missing. Exposed as <c>internal</c> so authoring utilities
    /// share one renderer-wiring path.
    /// </summary>
    /// <param name="particleObject">GameObject owning the renderer to configure.</param>
    /// <param name="renderMode">Render mode to apply on the renderer.</param>
    /// <param name="material">Optional custom material; null falls back to the default particle material.</param>
    internal static void ApplyRenderer(GameObject particleObject,
                                       ParticleSystemRenderMode renderMode,
                                       Material material)
    {
        ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();

        if (renderer == null)
            return;

        renderer.renderMode = renderMode;
        renderer.sharedMaterial = material != null ? material : ResolveDefaultParticleMaterial();
    }

    /// <summary>
    /// Resolves a usable default particle material, falling back when the editor built-in path is unavailable.
    /// </summary>
    /// <returns>Material to assign on the particle renderer.</returns>
    private static Material ResolveDefaultParticleMaterial()
    {
        Material defaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");

        if (defaultMaterial != null)
            return defaultMaterial;

        Shader fallbackShader = Shader.Find("Particles/Standard Unlit");

        if (fallbackShader == null)
            fallbackShader = Shader.Find("Sprites/Default");

        return new Material(fallbackShader);
    }
    #endregion

    #region Save
    /// <summary>
    /// Persists the authored root GameObject as a prefab asset at the configured VFX folder.
    /// </summary>
    /// <param name="rootObject">Authored root GameObject in the active scene.</param>
    /// <param name="prefabName">File name to use for the new prefab (without extension).</param>
    private static void SaveAsPrefab(GameObject rootObject, string prefabName)
    {
        EnsureOutputFolderExists();

        string prefabPath = string.Format("{0}/{1}.prefab", OutputFolderRelative, prefabName);
        PrefabUtility.SaveAsPrefabAsset(rootObject, prefabPath);
        Object.DestroyImmediate(rootObject);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(string.Format("[ParticleVfxPrefabBuilder] Saved prefab at {0}", prefabPath));
    }

    /// <summary>
    /// Creates the output folder hierarchy when it does not already exist.
    /// </summary>
    private static void EnsureOutputFolderExists()
    {
        if (AssetDatabase.IsValidFolder(OutputFolderRelative))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/3D"))
            AssetDatabase.CreateFolder("Assets", "3D");

        if (!AssetDatabase.IsValidFolder("Assets/3D/VFX"))
            AssetDatabase.CreateFolder("Assets/3D", "VFX");

        if (!AssetDatabase.IsValidFolder(OutputFolderRelative))
            AssetDatabase.CreateFolder("Assets/3D/VFX", "VFX_remastered");
    }
    #endregion

    #endregion
}
#endregion
