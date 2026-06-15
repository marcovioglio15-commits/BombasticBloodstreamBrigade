using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the shared ECS-compatible death puddle material and prefab used when a visual preset has no override.
/// </summary>
public static class EnemyDeathPuddleStandardAssetUtility
{
    #region Constants
    public const string MaterialAssetPath = "Assets/Resources/M_EnemyDeathPuddle.mat";
    public const string PrefabAssetPath = "Assets/Resources/PF_EnemyDeathPuddle.prefab";
    private const string ShaderName = "BombasticBloodstreamBrigade/Enemy Death Puddle ECS";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds the shared death puddle assets while preserving the material asset identity when it already exists.
    /// </summary>
    //[MenuItem("Tools/Enemy Management/Rebuild Standard Death Puddle Assets")]
    public static void BuildStandardAssets()
    {
        Shader shader = Shader.Find(ShaderName);

        if (shader == null)
            throw new System.InvalidOperationException("Missing required shader: " + ShaderName);

        Material material = BuildMaterial(shader);
        BuildPrefab(material);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        //Debug.Log("[EnemyDeathPuddleStandardAssetUtility] Standard death puddle assets rebuilt.");
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates or updates the shared death puddle material using the dedicated DOTS shader.
    /// </summary>
    /// <param name="shader">Resolved death puddle shader.</param>
    /// <returns>Persistent shared death puddle material.</returns>
    private static Material BuildMaterial(Shader shader)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialAssetPath);

        if (material == null)
        {
            material = new Material(shader)
            {
                name = "M_EnemyDeathPuddle",
                enableInstancing = true
            };
            AssetDatabase.CreateAsset(material, MaterialAssetPath);
            return material;
        }

        material.shader = shader;
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>
    /// Creates the standard render-only prefab with the authoring marker required by the dedicated ECS pool.
    /// </summary>
    /// <param name="material">Shared death puddle material assigned to the prefab renderer.</param>
    private static void BuildPrefab(Material material)
    {
        Mesh quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        if (quadMesh == null)
            throw new System.InvalidOperationException("Unity built-in Quad mesh could not be resolved.");

        GameObject root = new GameObject("PF_EnemyDeathPuddle");

        try
        {
            MeshFilter meshFilter = root.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = root.AddComponent<MeshRenderer>();
            root.AddComponent<EnemyDeathPuddlePrefabAuthoring>();
            meshFilter.sharedMesh = quadMesh;
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabAssetPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
    #endregion

    #endregion
}
