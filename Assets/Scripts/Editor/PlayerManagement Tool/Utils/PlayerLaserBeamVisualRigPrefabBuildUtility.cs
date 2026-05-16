using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds Laser Beam visual rig prefabs and synchronizes the player visual bridge authoring component.
/// </summary>
internal static class PlayerLaserBeamVisualRigPrefabBuildUtility
{
    #region Constants
    private const int LaserBeamVisualLayer = 0;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds or refreshes one mesh-body prefab from the provided deterministic definition.
    /// </summary>
    /// <param name="definition">Deterministic body prefab definition.</param>
    /// <param name="bodyMaterial">Material assigned to every mesh layer of the body prefab.</param>
    /// <param name="sphereMesh">Mesh used by each liquid body blob.</param>
    /// <returns>Built body prefab asset.</returns>
    public static GameObject BuildBodyPrefab(PlayerLaserBeamBodyPrefabDefinition definition, Material bodyMaterial, Mesh sphereMesh)
    {
        bool wasExistingPrefab;
        GameObject prefabContentsRoot = LoadOrCreatePrefabContents(definition.PrefabPath, definition.RootName, out wasExistingPrefab);

        try
        {
            ResetRoot(prefabContentsRoot, definition.RootName);
            SetLayerRecursively(prefabContentsRoot, LaserBeamVisualLayer);
            CreateBodyRenderer(prefabContentsRoot.transform,
                               "OuterBlob",
                               sphereMesh,
                               bodyMaterial,
                               definition.OuterPosition,
                               definition.OuterScale,
                               0);
            CreateBodyRenderer(prefabContentsRoot.transform,
                               "InnerCore",
                               sphereMesh,
                               bodyMaterial,
                               definition.InnerPosition,
                               definition.InnerScale,
                               1);
            PrefabUtility.SaveAsPrefabAsset(prefabContentsRoot, definition.PrefabPath);
        }
        finally
        {
            UnloadOrDestroyPrefabContents(prefabContentsRoot, wasExistingPrefab);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(definition.PrefabPath);
    }

    /// <summary>
    /// Builds or refreshes one particle prefab from the provided deterministic layered-emitter definition.
    /// </summary>
    /// <param name="definition">Deterministic particle prefab definition.</param>
    /// <param name="particleMaterial">Material assigned to every particle renderer in the prefab.</param>
    /// <param name="sphereMesh">Mesh used by the mesh-particle renderers.</param>
    /// <returns>Built particle prefab asset.</returns>
    public static GameObject BuildParticlePrefab(PlayerLaserBeamParticlePrefabDefinition definition, Material particleMaterial, Mesh sphereMesh)
    {
        bool wasExistingPrefab;
        GameObject prefabContentsRoot = LoadOrCreatePrefabContents(definition.PrefabPath, definition.RootName, out wasExistingPrefab);

        try
        {
            ResetRoot(prefabContentsRoot, definition.RootName);
            SetLayerRecursively(prefabContentsRoot, LaserBeamVisualLayer);
            CreateParticleEmitter(prefabContentsRoot.transform,
                                  in definition.PrimaryEmitter,
                                  particleMaterial,
                                  sphereMesh);
            CreateParticleEmitter(prefabContentsRoot.transform,
                                  in definition.SecondaryEmitter,
                                  particleMaterial,
                                  sphereMesh);
            PrefabUtility.SaveAsPrefabAsset(prefabContentsRoot, definition.PrefabPath);
        }
        finally
        {
            UnloadOrDestroyPrefabContents(prefabContentsRoot, wasExistingPrefab);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(definition.PrefabPath);
    }

    /// <summary>
    /// Synchronizes the runtime visual bridge prefab with the newly generated Laser Beam rig assets.
    /// </summary>
    /// <param name="playerVisualPrefabPath">Runtime visual bridge prefab path.</param>
    /// <param name="bubbleBurstSourcePrefab">Bubble-burst source prefab asset.</param>
    /// <param name="starBloomSourcePrefab">Star-bloom source prefab asset.</param>
    /// <param name="softDiscSourcePrefab">Soft-disc source prefab asset.</param>
    /// <param name="bubbleBurstImpactPrefab">Bubble-burst impact prefab asset.</param>
    /// <param name="starBloomImpactPrefab">Star-bloom impact prefab asset.</param>
    /// <param name="softDiscImpactPrefab">Soft-disc impact prefab asset.</param>
    public static void SynchronizePlayerVisualBridge(string playerVisualPrefabPath,
                                                     GameObject bubbleBurstSourcePrefab,
                                                     GameObject starBloomSourcePrefab,
                                                     GameObject softDiscSourcePrefab,
                                                     GameObject bubbleBurstImpactPrefab,
                                                     GameObject starBloomImpactPrefab,
                                                     GameObject softDiscImpactPrefab)
    {
        GameObject prefabContentsRoot = PrefabUtility.LoadPrefabContents(playerVisualPrefabPath);

        try
        {
            PlayerLaserBeamVisualRigAuthoring rigAuthoring = GetOrAddComponent<PlayerLaserBeamVisualRigAuthoring>(prefabContentsRoot);
            SerializedObject serializedRig = new SerializedObject(rigAuthoring);
            serializedRig.Update();
            serializedRig.FindProperty("bubbleBurstSourcePrefab").objectReferenceValue = bubbleBurstSourcePrefab;
            serializedRig.FindProperty("starBloomSourcePrefab").objectReferenceValue = starBloomSourcePrefab;
            serializedRig.FindProperty("softDiscSourcePrefab").objectReferenceValue = softDiscSourcePrefab;
            serializedRig.FindProperty("bubbleBurstImpactPrefab").objectReferenceValue = bubbleBurstImpactPrefab;
            serializedRig.FindProperty("starBloomImpactPrefab").objectReferenceValue = starBloomImpactPrefab;
            serializedRig.FindProperty("softDiscImpactPrefab").objectReferenceValue = softDiscImpactPrefab;
            serializedRig.FindProperty("maximumRibbonSegmentLength").floatValue = 0.18f;
            serializedRig.FindProperty("terminalSplashLengthMultiplier").floatValue = 1.2f;
            serializedRig.FindProperty("terminalSplashWidthMultiplier").floatValue = 1.7f;
            serializedRig.FindProperty("sourceForwardOffset").floatValue = 0.02f;
            serializedRig.FindProperty("impactForwardOffset").floatValue = 0f;
            serializedRig.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rigAuthoring);
            PrefabUtility.SaveAsPrefabAsset(prefabContentsRoot, playerVisualPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContentsRoot);
        }
    }

    /// <summary>
    /// Ensures that one AssetDatabase folder path exists before prefab generation runs.
    /// </summary>
    /// <param name="folderPath">Folder path that must exist inside the AssetDatabase.</param>
    public static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            return;

        string parentFolder = Path.GetDirectoryName(folderPath);
        string folderName = Path.GetFileName(folderPath);

        if (!string.IsNullOrWhiteSpace(parentFolder) && !AssetDatabase.IsValidFolder(parentFolder))
            EnsureFolder(parentFolder);

        AssetDatabase.CreateFolder(parentFolder, folderName);
    }

    /// <summary>
    /// Rebuilds one normalized primitive mesh asset used by the Laser Beam visual rig.
    /// </summary>
    /// <param name="meshAssetPath">Asset path where the generated mesh should live.</param>
    /// <param name="primitiveType">Unity primitive type used as the source mesh.</param>
    /// <param name="meshRotation">Local-space rotation baked directly into the saved mesh vertices.</param>
    /// <returns>Generated mesh asset ready to be referenced by prefabs.</returns>
    public static Mesh RebuildPrimitiveMeshAsset(string meshAssetPath,
                                                 PrimitiveType primitiveType,
                                                 Quaternion meshRotation)
    {
        if (string.IsNullOrWhiteSpace(meshAssetPath))
            throw new ArgumentException("Mesh asset path cannot be null or empty.", nameof(meshAssetPath));

        string folderPath = Path.GetDirectoryName(meshAssetPath);
        EnsureFolder(folderPath);
        GameObject primitiveObject = GameObject.CreatePrimitive(primitiveType);

        try
        {
            MeshFilter meshFilter = primitiveObject.GetComponent<MeshFilter>();

            if (meshFilter == null || meshFilter.sharedMesh == null)
                throw new InvalidOperationException(string.Format("Could not extract source mesh from primitive type '{0}'.", primitiveType));

            Mesh generatedMesh = UnityEngine.Object.Instantiate(meshFilter.sharedMesh);
            generatedMesh.name = Path.GetFileNameWithoutExtension(meshAssetPath);
            BakeMeshRotation(generatedMesh, meshRotation);
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);

            if (existingMesh == null)
            {
                AssetDatabase.CreateAsset(generatedMesh, meshAssetPath);
                return AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);
            }

            EditorUtility.CopySerialized(generatedMesh, existingMesh);
            AssetDatabase.SaveAssetIfDirty(existingMesh);
            UnityEngine.Object.DestroyImmediate(generatedMesh);
            return existingMesh;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(primitiveObject);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates one mesh child used by the deterministic beam body prefabs.
    /// </summary>
    /// <param name="parent">Parent transform that receives the mesh child.</param>
    /// <param name="childName">Child GameObject name.</param>
    /// <param name="sphereMesh">Mesh used by the child renderer.</param>
    /// <param name="material">Shared liquid material assigned to the mesh renderer.</param>
    /// <param name="localPosition">Local position of the child.</param>
    /// <param name="localScale">Local scale of the child.</param>
    /// <param name="sortingOrder">Renderer sorting order used for layered shells.</param>
    private static void CreateBodyRenderer(Transform parent,
                                           string childName,
                                           Mesh sphereMesh,
                                           Material material,
                                           Vector3 localPosition,
                                           Vector3 localScale,
                                           int sortingOrder)
    {
        GameObject childObject = new GameObject(childName);
        childObject.transform.SetParent(parent, false);
        childObject.transform.localPosition = localPosition;
        childObject.transform.localRotation = Quaternion.identity;
        childObject.transform.localScale = localScale;
        childObject.layer = LaserBeamVisualLayer;
        MeshFilter meshFilter = childObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = sphereMesh;
        MeshRenderer meshRenderer = childObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.sortingOrder = sortingOrder;
    }

    /// <summary>
    /// Creates one particle emitter child used by the deterministic source and impact prefabs.
    /// </summary>
    /// <param name="parent">Parent transform that receives the emitter child.</param>
    /// <param name="definition">Deterministic emitter definition.</param>
    /// <param name="particleMaterial">Shared particle material assigned to the renderer.</param>
    /// <param name="sphereMesh">Mesh used by the mesh-particle renderer.</param>
    private static void CreateParticleEmitter(Transform parent,
                                              in PlayerLaserBeamParticleEmitterDefinition definition,
                                              Material particleMaterial,
                                              Mesh sphereMesh)
    {
        GameObject childObject = new GameObject(definition.ChildName);
        childObject.transform.SetParent(parent, false);
        childObject.transform.localPosition = definition.LocalPosition;
        childObject.transform.localRotation = Quaternion.Euler(definition.LocalEulerAngles);
        childObject.transform.localScale = definition.LocalScale;
        childObject.layer = LaserBeamVisualLayer;
        ParticleSystem particleSystem = childObject.AddComponent<ParticleSystem>();
        ParticleSystemRenderer particleRenderer = childObject.GetComponent<ParticleSystemRenderer>();

        // Configure the core particle lifetime, motion, and pooling-friendly playback settings.
        ParticleSystem.MainModule mainModule = particleSystem.main;
        mainModule.duration = definition.Duration;
        mainModule.loop = definition.Looping;
        mainModule.playOnAwake = true;
        mainModule.simulationSpace = ParticleSystemSimulationSpace.Local;
        mainModule.scalingMode = ParticleSystemScalingMode.Local;
        mainModule.maxParticles = definition.MaxParticles;
        mainModule.startLifetime = new ParticleSystem.MinMaxCurve(definition.LifetimeRange.x, definition.LifetimeRange.y);
        mainModule.startSpeed = new ParticleSystem.MinMaxCurve(definition.SpeedRange.x, definition.SpeedRange.y);
        mainModule.startSize = new ParticleSystem.MinMaxCurve(definition.SizeRange.x, definition.SizeRange.y);
        mainModule.startColor = Color.white;
        mainModule.stopAction = ParticleSystemStopAction.None;

        // Configure continuous emission plus an activation burst so the effect reads immediately when the beam starts.
        ParticleSystem.EmissionModule emissionModule = particleSystem.emission;
        emissionModule.enabled = true;
        emissionModule.rateOverTime = definition.EmissionRate;

        if (definition.BurstMaximum > 0)
        {
            ParticleSystem.Burst[] bursts = new ParticleSystem.Burst[1];
            bursts[0] = new ParticleSystem.Burst(0f, definition.BurstMinimum, definition.BurstMaximum);
            emissionModule.SetBursts(bursts, 1);
        }

        // Sculpt the volumetric silhouette of the particle cloud around the emitter origin.
        ParticleSystem.ShapeModule shapeModule = particleSystem.shape;
        shapeModule.enabled = true;
        shapeModule.shapeType = definition.ShapeType;
        shapeModule.radius = definition.ShapeRadius;
        shapeModule.angle = definition.ShapeAngle;
        shapeModule.length = definition.ShapeLength;
        shapeModule.randomDirectionAmount = definition.RandomDirectionAmount;

        // Fade particles in and out cleanly so the beam endpoints feel liquid instead of dusty.
        ParticleSystem.ColorOverLifetimeModule colorOverLifetimeModule = particleSystem.colorOverLifetime;
        colorOverLifetimeModule.enabled = true;
        colorOverLifetimeModule.color = new ParticleSystem.MinMaxGradient(CreateFadeGradient());

        // Expand bubbles slightly before they die to create a softer, denser bloom.
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetimeModule = particleSystem.sizeOverLifetime;
        sizeOverLifetimeModule.enabled = true;
        sizeOverLifetimeModule.size = new ParticleSystem.MinMaxCurve(1f, CreateBubbleSizeCurve());

        // Add small turbulence so the beam keeps a wet organic motion without large particle counts.
        ParticleSystem.NoiseModule noiseModule = particleSystem.noise;
        noiseModule.enabled = definition.NoiseStrength > 0f;
        noiseModule.strength = definition.NoiseStrength;
        noiseModule.frequency = definition.NoiseFrequency;
        noiseModule.scrollSpeed = 0.2f;
        noiseModule.damping = true;
        noiseModule.quality = ParticleSystemNoiseQuality.High;

        // Bias the layered emitters either forward or sideways to shape the muzzle bloom and the terminal splash.
        ParticleSystem.VelocityOverLifetimeModule velocityOverLifetimeModule = particleSystem.velocityOverLifetime;
        velocityOverLifetimeModule.enabled = Mathf.Abs(definition.VelocityLinearZ) > 0.0001f || Mathf.Abs(definition.VelocityOrbitalY) > 0.0001f;
        velocityOverLifetimeModule.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetimeModule.z = definition.VelocityLinearZ;
        velocityOverLifetimeModule.orbitalY = definition.VelocityOrbitalY;

        particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        particleRenderer.mesh = sphereMesh;
        particleRenderer.sharedMaterial = particleMaterial;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        particleRenderer.minParticleSize = 0.0001f;
        particleRenderer.maxParticleSize = 0.5f;
        particleRenderer.enableGPUInstancing = true;
    }

    /// <summary>
    /// Creates the shared fade gradient used by the deterministic particle prefabs.
    /// </summary>
    /// <returns>Shared fade gradient.</returns>
    private static Gradient CreateFadeGradient()
    {
        Gradient gradient = new Gradient();
        GradientColorKey[] colorKeys = new GradientColorKey[2];
        colorKeys[0] = new GradientColorKey(Color.white, 0f);
        colorKeys[1] = new GradientColorKey(Color.white, 1f);
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[4];
        alphaKeys[0] = new GradientAlphaKey(0f, 0f);
        alphaKeys[1] = new GradientAlphaKey(0.95f, 0.12f);
        alphaKeys[2] = new GradientAlphaKey(0.9f, 0.72f);
        alphaKeys[3] = new GradientAlphaKey(0f, 1f);
        gradient.SetKeys(colorKeys, alphaKeys);
        return gradient;
    }

    /// <summary>
    /// Creates the shared size curve used by the deterministic particle prefabs.
    /// </summary>
    /// <returns>Shared size-over-lifetime curve.</returns>
    private static AnimationCurve CreateBubbleSizeCurve()
    {
        return new AnimationCurve(new Keyframe(0f, 0.28f),
                                  new Keyframe(0.2f, 0.92f),
                                  new Keyframe(0.58f, 1.08f),
                                  new Keyframe(1f, 0.12f));
    }

    /// <summary>
    /// Loads prefab contents when the prefab already exists, otherwise creates a new root object.
    /// </summary>
    /// <param name="prefabPath">Prefab asset path.</param>
    /// <param name="rootName">Root name used for new prefabs.</param>
    /// <param name="wasExistingPrefab">True when the prefab already existed on disk.</param>
    /// <returns>Loaded prefab contents root or a new unsaved root object.</returns>
    private static GameObject LoadOrCreatePrefabContents(string prefabPath, string rootName, out bool wasExistingPrefab)
    {
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (existingPrefab != null)
        {
            wasExistingPrefab = true;
            return PrefabUtility.LoadPrefabContents(prefabPath);
        }

        wasExistingPrefab = false;
        GameObject createdRoot = new GameObject(rootName);
        createdRoot.layer = 0;
        return createdRoot;
    }

    /// <summary>
    /// Releases prefab contents after a save operation, unloading loaded contents or destroying unsaved roots.
    /// </summary>
    /// <param name="prefabContentsRoot">Prefab contents root to release.</param>
    /// <param name="wasExistingPrefab">True when the contents were loaded from an existing prefab asset.</param>
    private static void UnloadOrDestroyPrefabContents(GameObject prefabContentsRoot, bool wasExistingPrefab)
    {
        if (wasExistingPrefab)
        {
            PrefabUtility.UnloadPrefabContents(prefabContentsRoot);
            return;
        }

        UnityEngine.Object.DestroyImmediate(prefabContentsRoot);
    }

    /// <summary>
    /// Resets one prefab root before deterministic children are rebuilt.
    /// </summary>
    /// <param name="rootObject">Prefab root object to reset.</param>
    /// <param name="rootName">Root name to assign.</param>
    private static void ResetRoot(GameObject rootObject, string rootName)
    {
        if (rootObject == null)
            return;

        rootObject.name = rootName;
        rootObject.layer = 0;
        rootObject.transform.localPosition = Vector3.zero;
        rootObject.transform.localRotation = Quaternion.identity;
        rootObject.transform.localScale = Vector3.one;
        DestroyAllChildren(rootObject.transform);
        DestroyExtraComponents(rootObject);
    }

    /// <summary>
    /// Destroys every child below one transform.
    /// </summary>
    /// <param name="parent">Parent transform whose children should be removed.</param>
    private static void DestroyAllChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int childIndex = parent.childCount - 1; childIndex >= 0; childIndex--)
            UnityEngine.Object.DestroyImmediate(parent.GetChild(childIndex).gameObject);
    }

    /// <summary>
    /// Removes every component from one root except the Transform so prefab rebuilds stay deterministic.
    /// </summary>
    /// <param name="rootObject">Root object whose extra components should be removed.</param>
    private static void DestroyExtraComponents(GameObject rootObject)
    {
        Component[] components = rootObject.GetComponents<Component>();

        for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
        {
            Component component = components[componentIndex];

            if (component is Transform)
                continue;

            UnityEngine.Object.DestroyImmediate(component);
        }
    }

    /// <summary>
    /// Applies one layer recursively to an object hierarchy.
    /// </summary>
    /// <param name="rootObject">Root object of the hierarchy to update.</param>
    /// <param name="layer">Layer index to assign recursively.</param>
    private static void SetLayerRecursively(GameObject rootObject, int layer)
    {
        if (rootObject == null)
            return;

        rootObject.layer = layer;
        Transform rootTransform = rootObject.transform;

        for (int childIndex = 0; childIndex < rootTransform.childCount; childIndex++)
            SetLayerRecursively(rootTransform.GetChild(childIndex).gameObject, layer);
    }

    /// <summary>
    /// Returns one existing component from the target object or adds it when missing.
    /// </summary>
    /// <param name="targetObject">Target object that should own the component.</param>
    /// <returns>Existing or newly added component instance.</returns>
    private static TComponent GetOrAddComponent<TComponent>(GameObject targetObject)
        where TComponent : Component
    {
        TComponent component = targetObject.GetComponent<TComponent>();

        if (component != null)
            return component;

        return targetObject.AddComponent<TComponent>();
    }

    /// <summary>
    /// Rotates mesh vertex data in local space so the generated asset has the required forward axis baked in.
    /// </summary>
    /// <param name="mesh">Mesh asset instance to rotate.</param>
    /// <param name="rotation">Rotation applied to vertices, normals, and tangents.</param>
    private static void BakeMeshRotation(Mesh mesh, Quaternion rotation)
    {
        if (mesh == null || rotation == Quaternion.identity)
            return;

        Vector3[] vertices = mesh.vertices;

        for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
            vertices[vertexIndex] = rotation * vertices[vertexIndex];

        mesh.vertices = vertices;
        Vector3[] normals = mesh.normals;

        if (normals != null && normals.Length == vertices.Length)
        {
            for (int normalIndex = 0; normalIndex < normals.Length; normalIndex++)
                normals[normalIndex] = rotation * normals[normalIndex];

            mesh.normals = normals;
        }

        Vector4[] tangents = mesh.tangents;

        if (tangents != null && tangents.Length == vertices.Length)
        {
            for (int tangentIndex = 0; tangentIndex < tangents.Length; tangentIndex++)
            {
                Vector3 rotatedTangent = rotation * new Vector3(tangents[tangentIndex].x,
                                                                tangents[tangentIndex].y,
                                                                tangents[tangentIndex].z);
                tangents[tangentIndex] = new Vector4(rotatedTangent.x,
                                                     rotatedTangent.y,
                                                     rotatedTangent.z,
                                                     tangents[tangentIndex].w);
            }

            mesh.tangents = tangents;
        }

        mesh.RecalculateBounds();
    }
    #endregion

    #endregion
}
