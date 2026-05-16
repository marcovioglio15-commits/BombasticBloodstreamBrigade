using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rebuilds the authored Laser Beam endpoint particle prefabs, then synchronizes the runtime visual bridge rig.
/// </summary>
public static class PlayerLaserBeamVisualRigPrefabUtility
{
    #region Constants
    private const string PlayerVisualPrefabPath = "Assets/Prefabs/Player/PF_PlayerVisual.prefab";
    private const string BubbleBurstSourcePrefabPath = "Assets/Prefabs/Player/PF_PlayerLaserBeamSource_BubbleBurst.prefab";
    private const string StarBloomSourcePrefabPath = "Assets/Prefabs/Player/PF_PlayerLaserBeamSource_StarBloom.prefab";
    private const string SoftDiscSourcePrefabPath = "Assets/Prefabs/Player/PF_PlayerLaserBeamSource_SoftDisc.prefab";
    private const string BubbleBurstImpactPrefabPath = "Assets/Prefabs/Player/PF_PlayerLaserBeamImpact_BubbleBurst.prefab";
    private const string StarBloomImpactPrefabPath = "Assets/Prefabs/Player/PF_PlayerLaserBeamImpact_StarBloom.prefab";
    private const string SoftDiscImpactPrefabPath = "Assets/Prefabs/Player/PF_PlayerLaserBeamImpact_SoftDisc.prefab";
    private const string ParticleSphereMeshAssetPath = "Assets/3D/Meshes/Player/ME_PlayerLaserBeamParticleSphere.asset";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds the full authored Laser Beam visual rig and updates the player visual bridge prefab.
    /// </summary>
    //[MenuItem("Tools/Player/Rebuild Laser Beam Visual Rig Prefabs")]
    public static void RebuildLaserBeamVisualRigPrefabs()
    {
        ExecuteRebuild();
    }

    /// <summary>
    /// Executes the rebuild entry point from Unity batch mode via -executeMethod.
    /// </summary>
    public static void ExecuteRebuild()
    {
        Material bubbleMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerLaserBeamVisualDefaultsUtility.DefaultSourceEffectMaterialPath);
        Material splashMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerLaserBeamVisualDefaultsUtility.DefaultTerminalCapMaterialPath);
        Mesh particleMesh = PlayerLaserBeamVisualRigPrefabBuildUtility.RebuildPrimitiveMeshAsset(ParticleSphereMeshAssetPath,
                                                                                                  PrimitiveType.Sphere,
                                                                                                  Quaternion.identity);

        if (bubbleMaterial == null)
            throw new InvalidOperationException(string.Format("Laser Beam source material not found at '{0}'.", PlayerLaserBeamVisualDefaultsUtility.DefaultSourceEffectMaterialPath));

        if (splashMaterial == null)
            throw new InvalidOperationException(string.Format("Laser Beam terminal material not found at '{0}'.", PlayerLaserBeamVisualDefaultsUtility.DefaultTerminalCapMaterialPath));

        if (particleMesh == null)
            throw new InvalidOperationException(string.Format("Laser Beam particle mesh could not be built at '{0}'.", ParticleSphereMeshAssetPath));

        PlayerLaserBeamVisualRigPrefabBuildUtility.EnsureFolder(Path.GetDirectoryName(BubbleBurstSourcePrefabPath));
        GameObject bubbleBurstSourcePrefab = PlayerLaserBeamVisualRigPrefabBuildUtility.BuildParticlePrefab(PlayerLaserBeamVisualRigPrefabDefinitions.CreateBubbleBurstSourceDefinition(BubbleBurstSourcePrefabPath),
                                                                                                             bubbleMaterial,
                                                                                                             particleMesh);
        GameObject starBloomSourcePrefab = PlayerLaserBeamVisualRigPrefabBuildUtility.BuildParticlePrefab(PlayerLaserBeamVisualRigPrefabDefinitions.CreateStarBloomSourceDefinition(StarBloomSourcePrefabPath),
                                                                                                           bubbleMaterial,
                                                                                                           particleMesh);
        GameObject softDiscSourcePrefab = PlayerLaserBeamVisualRigPrefabBuildUtility.BuildParticlePrefab(PlayerLaserBeamVisualRigPrefabDefinitions.CreateSoftDiscSourceDefinition(SoftDiscSourcePrefabPath),
                                                                                                          bubbleMaterial,
                                                                                                          particleMesh);
        GameObject bubbleBurstImpactPrefab = PlayerLaserBeamVisualRigPrefabBuildUtility.BuildParticlePrefab(PlayerLaserBeamVisualRigPrefabDefinitions.CreateBubbleBurstImpactDefinition(BubbleBurstImpactPrefabPath),
                                                                                                             splashMaterial,
                                                                                                             particleMesh);
        GameObject starBloomImpactPrefab = PlayerLaserBeamVisualRigPrefabBuildUtility.BuildParticlePrefab(PlayerLaserBeamVisualRigPrefabDefinitions.CreateStarBloomImpactDefinition(StarBloomImpactPrefabPath),
                                                                                                           splashMaterial,
                                                                                                           particleMesh);
        GameObject softDiscImpactPrefab = PlayerLaserBeamVisualRigPrefabBuildUtility.BuildParticlePrefab(PlayerLaserBeamVisualRigPrefabDefinitions.CreateSoftDiscImpactDefinition(SoftDiscImpactPrefabPath),
                                                                                                             splashMaterial,
                                                                                                             particleMesh);
        PlayerLaserBeamVisualRigPrefabBuildUtility.SynchronizePlayerVisualBridge(PlayerVisualPrefabPath,
                                                                                 bubbleBurstSourcePrefab,
                                                                                 starBloomSourcePrefab,
                                                                                 softDiscSourcePrefab,
                                                                                 bubbleBurstImpactPrefab,
                                                                                 starBloomImpactPrefab,
                                                                                 softDiscImpactPrefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PlayerLaserBeamVisualRigPrefabUtility] Laser Beam visual rig prefabs rebuilt successfully.");
    }
    #endregion

    #endregion
}
