using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Provides editor-only render optimization diagnostics for open scenes without mutating scene or prefab data.
/// </summary>
public static class SceneRenderOptimizationAuditUtility
{
    #region Constants
    private const int MaxLoggedExamplesPerCategory = 24;
    private const string MainGameplayScenePath = "Assets/Scenes/Testing/Main Scenes/SCN_PlayerControllerTesting/SCN_PlayerControllerTesting.unity";
    private const string EnvironmentPrefabFolder = "Assets/3D/3D prefabs";
    private const string EnvironmentModulesRootName = "Environment_modules";
    #endregion

    #region Methods

    #region Menu
    /// <summary>
    /// Audits currently open scenes for common render cost risks such as missing LODGroups and multi-material renderers.
    /// </summary>
    //[MenuItem("Tools/Optimization/Audit Open Scenes Rendering")]
    private static void AuditOpenScenesRendering()
    {
        string report = BuildOpenScenesRenderingAuditReport();
        Debug.Log(report);
    }

    /// <summary>
    /// Applies batching-static flags to known immobile environment modules in the open scenes.
    /// </summary>
    //[MenuItem("Tools/Optimization/Apply Open Scenes Environment Static Batching")]
    private static void ApplyOpenScenesEnvironmentStaticBatchingMenu()
    {
        int changedObjects = ApplyOpenScenesEnvironmentStaticBatching();
        Debug.Log(string.Format("[Scene Render Optimization] Applied batching-static flags to {0} scene environment objects.", changedObjects));
    }

    /// <summary>
    /// Applies batching-static flags to reusable environment prefabs.
    /// </summary>
    //[MenuItem("Tools/Optimization/Apply Environment Prefab Static Batching")]
    private static void ApplyEnvironmentPrefabStaticBatchingMenu()
    {
        int changedObjects = ApplyEnvironmentPrefabStaticBatching();
        Debug.Log(string.Format("[Scene Render Optimization] Applied batching-static flags to {0} prefab environment objects.", changedObjects));
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Builds a text report for currently loaded scenes that can be copied from the Unity Console.
    /// </summary>
    /// <returns>Human-readable render optimization report.</returns>
    public static string BuildOpenScenesRenderingAuditReport()
    {
        MeshRenderer[] meshRenderers = Resources.FindObjectsOfTypeAll<MeshRenderer>();
        SkinnedMeshRenderer[] skinnedMeshRenderers = Resources.FindObjectsOfTypeAll<SkinnedMeshRenderer>();
        SceneRenderAuditStats stats = new SceneRenderAuditStats();
        stats.UniqueMaterials = new HashSet<Material>();
        StringBuilder reportBuilder = new StringBuilder(4096);

        reportBuilder.AppendLine("[Scene Render Optimization Audit]");
        reportBuilder.AppendLine("Scope: loaded scenes only. No scene or prefab data was modified.");
        reportBuilder.AppendLine();

        AuditMeshRenderers(meshRenderers, ref stats, reportBuilder);
        AuditSkinnedMeshRenderers(skinnedMeshRenderers, ref stats, reportBuilder);
        AppendSummary(in stats, reportBuilder);

        return reportBuilder.ToString();
    }

    /// <summary>
    /// Batchmode entry point that optimizes reusable environment prefabs and the current gameplay scene.
    /// </summary>
    public static void ApplyMainGameplayEnvironmentStaticBatching()
    {
        int changedPrefabObjects = ApplyEnvironmentPrefabStaticBatching();
        Scene scene = EditorSceneManager.OpenScene(MainGameplayScenePath, OpenSceneMode.Single);
        int changedSceneObjects = ApplySceneEnvironmentStaticBatching(scene);

        if (changedSceneObjects > 0)
            EditorSceneManager.SaveScene(scene);

        Debug.Log(string.Format("[Scene Render Optimization] Static batching pass complete. Prefab objects changed: {0}. Scene objects changed: {1}.", changedPrefabObjects, changedSceneObjects));
        Debug.Log(BuildOpenScenesRenderingAuditReport());
    }

    /// <summary>
    /// Applies batching-static flags to known immobile environment renderers in all loaded scenes.
    /// </summary>
    /// <returns>Number of GameObjects changed.</returns>
    public static int ApplyOpenScenesEnvironmentStaticBatching()
    {
        int changedObjects = 0;

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            changedObjects += ApplySceneEnvironmentStaticBatching(scene);

            if (scene.IsValid() && scene.isLoaded && scene.isDirty)
                EditorSceneManager.SaveScene(scene);
        }

        return changedObjects;
    }
    #endregion

    #region Apply
    /// <summary>
    /// Applies batching-static flags to reusable prefabs that represent immobile environment modules.
    /// </summary>
    /// <returns>Number of prefab GameObjects changed.</returns>
    private static int ApplyEnvironmentPrefabStaticBatching()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { EnvironmentPrefabFolder });
        int changedObjects = 0;

        for (int prefabIndex = 0; prefabIndex < prefabGuids.Length; prefabIndex++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[prefabIndex]);

            if (!IsBatchableEnvironmentPrefabPath(prefabPath))
                continue;

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                int changedInPrefab = ApplyBatchingStaticToHierarchy(prefabRoot);

                if (changedInPrefab <= 0)
                    continue;

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                changedObjects += changedInPrefab;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        AssetDatabase.SaveAssets();
        return changedObjects;
    }

    /// <summary>
    /// Applies batching-static flags to scene objects under the authored environment modules root.
    /// </summary>
    /// <param name="scene">Scene to mutate.</param>
    /// <returns>Number of scene GameObjects changed.</returns>
    private static int ApplySceneEnvironmentStaticBatching(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return 0;

        GameObject[] rootObjects = scene.GetRootGameObjects();
        int changedObjects = 0;

        for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
        {
            GameObject rootObject = rootObjects[rootIndex];

            if (rootObject == null)
                continue;

            if (rootObject.name == EnvironmentModulesRootName)
            {
                changedObjects += ApplyBatchingStaticToHierarchy(rootObject);
                changedObjects += ClearDynamicBatchingStaticFromHierarchy(rootObject);
                continue;
            }

            if (IsStandaloneStaticSceneMeshRoot(rootObject))
                changedObjects += ApplyBatchingStaticToHierarchy(rootObject);
        }

        if (changedObjects > 0)
            EditorSceneManager.MarkSceneDirty(scene);

        return changedObjects;
    }

    /// <summary>
    /// Sets only the batching-static bit on MeshRenderer GameObjects in one hierarchy.
    /// </summary>
    /// <param name="rootObject">Hierarchy root to inspect.</param>
    /// <returns>Number of GameObjects changed.</returns>
    private static int ApplyBatchingStaticToHierarchy(GameObject rootObject)
    {
        if (rootObject == null)
            return 0;

        MeshRenderer[] meshRenderers = rootObject.GetComponentsInChildren<MeshRenderer>(true);
        int changedObjects = 0;

        for (int rendererIndex = 0; rendererIndex < meshRenderers.Length; rendererIndex++)
        {
            MeshRenderer meshRenderer = meshRenderers[rendererIndex];

            if (!IsBatchingStaticCandidate(meshRenderer))
                continue;

            GameObject rendererObject = meshRenderer.gameObject;
            StaticEditorFlags currentFlags = GameObjectUtility.GetStaticEditorFlags(rendererObject);
            StaticEditorFlags updatedFlags = currentFlags | StaticEditorFlags.BatchingStatic;

            if (updatedFlags == currentFlags)
                continue;

            GameObjectUtility.SetStaticEditorFlags(rendererObject, updatedFlags);
            EditorUtility.SetDirty(rendererObject);
            changedObjects++;
        }

        return changedObjects;
    }

    /// <summary>
    /// Removes batching-static from environment renderers controlled by dynamic transform drivers.
    /// </summary>
    /// <param name="rootObject">Hierarchy root to inspect.</param>
    /// <returns>Number of GameObjects changed.</returns>
    private static int ClearDynamicBatchingStaticFromHierarchy(GameObject rootObject)
    {
        if (rootObject == null)
            return 0;

        MeshRenderer[] meshRenderers = rootObject.GetComponentsInChildren<MeshRenderer>(true);
        int changedObjects = 0;

        for (int rendererIndex = 0; rendererIndex < meshRenderers.Length; rendererIndex++)
        {
            MeshRenderer meshRenderer = meshRenderers[rendererIndex];

            if (meshRenderer == null || !HasDynamicTransformOwner(meshRenderer.transform))
                continue;

            GameObject rendererObject = meshRenderer.gameObject;
            StaticEditorFlags currentFlags = GameObjectUtility.GetStaticEditorFlags(rendererObject);

            if ((currentFlags & StaticEditorFlags.BatchingStatic) == 0)
                continue;

            StaticEditorFlags updatedFlags = currentFlags & ~StaticEditorFlags.BatchingStatic;
            GameObjectUtility.SetStaticEditorFlags(rendererObject, updatedFlags);
            EditorUtility.SetDirty(rendererObject);
            changedObjects++;
        }

        return changedObjects;
    }
    #endregion

    #region Audit
    /// <summary>
    /// Audits MeshRenderer components for static batching, LODGroup coverage and material slot count.
    /// </summary>
    /// <param name="meshRenderers">Renderer array collected from loaded editor resources.</param>
    /// <param name="stats">Mutable aggregate report counters.</param>
    /// <param name="reportBuilder">Text builder receiving detailed examples.</param>
    private static void AuditMeshRenderers(MeshRenderer[] meshRenderers,
                                           ref SceneRenderAuditStats stats,
                                           StringBuilder reportBuilder)
    {
        if (meshRenderers == null)
            return;

        for (int rendererIndex = 0; rendererIndex < meshRenderers.Length; rendererIndex++)
        {
            MeshRenderer meshRenderer = meshRenderers[rendererIndex];

            if (!IsSceneRenderer(meshRenderer))
                continue;

            bool activeInHierarchy = meshRenderer.gameObject.activeInHierarchy;

            stats.MeshRendererCount++;

            if (activeInHierarchy)
                stats.ActiveMeshRendererCount++;

            TrackMaterials(meshRenderer.sharedMaterials, ref stats);

            if (!meshRenderer.gameObject.isStatic)
            {
                stats.NonStaticMeshRendererCount++;

                if (activeInHierarchy)
                {
                    stats.ActiveNonStaticMeshRendererCount++;

                    if (IsBatchingStaticCandidate(meshRenderer))
                    {
                        stats.ActiveStaticBatchingCandidateCount++;
                        AppendExample(reportBuilder,
                                      "Active static batching candidate",
                                      meshRenderer,
                                      stats.ActiveStaticBatchingCandidateCount);
                    }
                    else if (HasDynamicTransformOwner(meshRenderer.transform))
                    {
                        stats.ActiveDynamicNonStaticMeshRendererCount++;
                        AppendExample(reportBuilder,
                                      "Static batching skipped by dynamic owner",
                                      meshRenderer,
                                      stats.ActiveDynamicNonStaticMeshRendererCount);
                    }
                }
            }

            if (meshRenderer.GetComponentInParent<LODGroup>() == null)
            {
                stats.MeshRenderersWithoutLodGroup++;
                AppendExample(reportBuilder,
                              "MeshRenderer without LODGroup",
                              meshRenderer,
                              stats.MeshRenderersWithoutLodGroup);
            }

            AppendMaterialSlotWarning(meshRenderer,
                                      meshRenderer.sharedMaterials,
                                      ref stats,
                                      reportBuilder);
        }
    }

    /// <summary>
    /// Audits SkinnedMeshRenderer components for LODGroup coverage and material slot count.
    /// </summary>
    /// <param name="skinnedMeshRenderers">Renderer array collected from loaded editor resources.</param>
    /// <param name="stats">Mutable aggregate report counters.</param>
    /// <param name="reportBuilder">Text builder receiving detailed examples.</param>
    private static void AuditSkinnedMeshRenderers(SkinnedMeshRenderer[] skinnedMeshRenderers,
                                                  ref SceneRenderAuditStats stats,
                                                  StringBuilder reportBuilder)
    {
        if (skinnedMeshRenderers == null)
            return;

        for (int rendererIndex = 0; rendererIndex < skinnedMeshRenderers.Length; rendererIndex++)
        {
            SkinnedMeshRenderer skinnedMeshRenderer = skinnedMeshRenderers[rendererIndex];

            if (!IsSceneRenderer(skinnedMeshRenderer))
                continue;

            stats.SkinnedMeshRendererCount++;
            TrackMaterials(skinnedMeshRenderer.sharedMaterials, ref stats);

            if (skinnedMeshRenderer.GetComponentInParent<LODGroup>() == null)
            {
                stats.SkinnedRenderersWithoutLodGroup++;
                AppendExample(reportBuilder,
                              "SkinnedMeshRenderer without LODGroup",
                              skinnedMeshRenderer,
                              stats.SkinnedRenderersWithoutLodGroup);
            }

            AppendMaterialSlotWarning(skinnedMeshRenderer,
                                      skinnedMeshRenderer.sharedMaterials,
                                      ref stats,
                                      reportBuilder);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves whether a renderer belongs to a loaded scene rather than an asset or hidden editor object.
    /// </summary>
    /// <param name="renderer">Renderer candidate to inspect.</param>
    /// <returns>True when the renderer is part of one loaded scene.</returns>
    private static bool IsSceneRenderer(Renderer renderer)
    {
        if (renderer == null)
            return false;

        if (EditorUtility.IsPersistent(renderer))
            return false;

        Scene scene = renderer.gameObject.scene;
        return scene.IsValid() && scene.isLoaded;
    }

    /// <summary>
    /// Resolves whether a renderer can safely participate in static batching.
    /// </summary>
    /// <param name="meshRenderer">Renderer candidate.</param>
    /// <returns>True when the renderer belongs to an immobile mesh hierarchy.</returns>
    private static bool IsBatchingStaticCandidate(MeshRenderer meshRenderer)
    {
        if (meshRenderer == null)
            return false;

        if (!meshRenderer.enabled)
            return false;

        if (HasDynamicTransformOwner(meshRenderer.transform))
            return false;

        MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
        return meshFilter != null && meshFilter.sharedMesh != null;
    }

    /// <summary>
    /// Resolves authored one-off static meshes placed directly in the scene root.
    /// </summary>
    /// <param name="rootObject">Root GameObject to inspect.</param>
    /// <returns>True when the root is a standalone immobile mesh.</returns>
    private static bool IsStandaloneStaticSceneMeshRoot(GameObject rootObject)
    {
        if (rootObject == null || !rootObject.activeInHierarchy)
            return false;

        if (rootObject.transform.childCount != 0)
            return false;

        if (rootObject.GetComponents<MonoBehaviour>().Length != 0)
            return false;

        return IsBatchingStaticCandidate(rootObject.GetComponent<MeshRenderer>());
    }

    /// <summary>
    /// Detects transform drivers that make static batching unsafe.
    /// </summary>
    /// <param name="transform">Renderer transform to inspect.</param>
    /// <returns>True when one parent can move the renderer at runtime.</returns>
    private static bool HasDynamicTransformOwner(Transform transform)
    {
        if (transform == null)
            return false;

        return transform.GetComponentInParent<Animator>(true) != null
            || transform.GetComponentInParent<Rigidbody>(true) != null;
    }

    /// <summary>
    /// Filters environment prefabs to immobile modular pieces. Moving set pieces stay opt-in.
    /// </summary>
    /// <param name="prefabPath">Asset path to inspect.</param>
    /// <returns>True when the prefab should default to batching-static.</returns>
    private static bool IsBatchableEnvironmentPrefabPath(string prefabPath)
    {
        if (string.IsNullOrEmpty(prefabPath))
            return false;

        string prefabName = System.IO.Path.GetFileNameWithoutExtension(prefabPath);

        switch (prefabName)
        {
            case "PF_Column":
            case "PF_Floor_A":
            case "PF_Floor_A_Railway":
            case "PF_Gate_Small":
            case "PF_Grate_LevelExit":
            case "PF_Railway_Rails_A":
            case "PF_Railway_Rails_B":
            case "PF_Stairs":
            case "PF_Tunnel_LevelExit":
            case "PF_Tunnel_Railway":
            case "PF_Wall_A":
            case "PF_Wall_A_Railway":
            case "PF_Wall_B":
                return true;
        }

        return false;
    }

    /// <summary>
    /// Tracks unique shared materials used by audited renderers.
    /// </summary>
    /// <param name="materials">Renderer shared material array.</param>
    /// <param name="stats">Mutable aggregate report counters.</param>
    private static void TrackMaterials(Material[] materials, ref SceneRenderAuditStats stats)
    {
        if (materials == null)
            return;

        for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
        {
            Material material = materials[materialIndex];

            if (material == null)
                continue;

            stats.UniqueMaterials.Add(material);
        }
    }

    /// <summary>
    /// Adds a material-slot warning when one renderer contributes more than one draw slot.
    /// </summary>
    /// <param name="renderer">Renderer being inspected.</param>
    /// <param name="materials">Renderer shared material array.</param>
    /// <param name="stats">Mutable aggregate report counters.</param>
    /// <param name="reportBuilder">Text builder receiving detailed examples.</param>
    private static void AppendMaterialSlotWarning(Renderer renderer,
                                                  Material[] materials,
                                                  ref SceneRenderAuditStats stats,
                                                  StringBuilder reportBuilder)
    {
        int materialSlotCount = materials != null ? materials.Length : 0;

        if (materialSlotCount <= 1)
            return;

        stats.MultiMaterialRendererCount++;
        AppendExample(reportBuilder,
                      string.Format("Renderer with {0} material slots", materialSlotCount),
                      renderer,
                      stats.MultiMaterialRendererCount);
    }

    /// <summary>
    /// Appends a bounded example line for one warning category.
    /// </summary>
    /// <param name="reportBuilder">Text builder receiving detailed examples.</param>
    /// <param name="label">Warning category label.</param>
    /// <param name="renderer">Renderer used to resolve scene path context.</param>
    /// <param name="categoryCount">Current category count used to cap example spam.</param>
    private static void AppendExample(StringBuilder reportBuilder,
                                      string label,
                                      Renderer renderer,
                                      int categoryCount)
    {
        if (reportBuilder == null || renderer == null)
            return;

        if (categoryCount > MaxLoggedExamplesPerCategory)
            return;

        reportBuilder.Append(" - ");
        reportBuilder.Append(label);
        reportBuilder.Append(": ");
        reportBuilder.Append(renderer.gameObject.scene.name);
        reportBuilder.Append("/");
        reportBuilder.Append(GetHierarchyPath(renderer.transform));
        reportBuilder.AppendLine();
    }

    /// <summary>
    /// Appends the aggregate report summary after all renderer examples have been collected.
    /// </summary>
    /// <param name="stats">Aggregate audit counters.</param>
    /// <param name="reportBuilder">Text builder receiving summary lines.</param>
    private static void AppendSummary(in SceneRenderAuditStats stats, StringBuilder reportBuilder)
    {
        if (reportBuilder == null)
            return;

        reportBuilder.AppendLine();
        reportBuilder.AppendLine("Summary:");
        reportBuilder.AppendLine(string.Format(" MeshRenderers: {0}", stats.MeshRendererCount));
        reportBuilder.AppendLine(string.Format(" Active MeshRenderers: {0}", stats.ActiveMeshRendererCount));
        reportBuilder.AppendLine(string.Format(" SkinnedMeshRenderers: {0}", stats.SkinnedMeshRendererCount));
        reportBuilder.AppendLine(string.Format(" Unique shared materials: {0}", stats.UniqueMaterials.Count));
        reportBuilder.AppendLine(string.Format(" MeshRenderers without LODGroup: {0}", stats.MeshRenderersWithoutLodGroup));
        reportBuilder.AppendLine(string.Format(" SkinnedMeshRenderers without LODGroup: {0}", stats.SkinnedRenderersWithoutLodGroup));
        reportBuilder.AppendLine(string.Format(" Non-static MeshRenderers: {0}", stats.NonStaticMeshRendererCount));
        reportBuilder.AppendLine(string.Format(" Active non-static MeshRenderers: {0}", stats.ActiveNonStaticMeshRendererCount));
        reportBuilder.AppendLine(string.Format(" Active static batching candidates: {0}", stats.ActiveStaticBatchingCandidateCount));
        reportBuilder.AppendLine(string.Format(" Active dynamic non-static MeshRenderers: {0}", stats.ActiveDynamicNonStaticMeshRendererCount));
        reportBuilder.AppendLine(string.Format(" Multi-material renderers: {0}", stats.MultiMaterialRendererCount));
    }

    /// <summary>
    /// Builds a stable hierarchy path for one transform to make audit examples easy to find.
    /// </summary>
    /// <param name="transform">Transform to describe.</param>
    /// <returns>Slash-separated hierarchy path.</returns>
    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        StringBuilder pathBuilder = new StringBuilder(transform.name);
        Transform currentTransform = transform.parent;

        while (currentTransform != null)
        {
            pathBuilder.Insert(0, "/");
            pathBuilder.Insert(0, currentTransform.name);
            currentTransform = currentTransform.parent;
        }

        return pathBuilder.ToString();
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Stores aggregate counters collected while auditing renderers in loaded scenes.
    /// </summary>
    private struct SceneRenderAuditStats
    {
        public int MeshRendererCount;
        public int ActiveMeshRendererCount;
        public int SkinnedMeshRendererCount;
        public int MeshRenderersWithoutLodGroup;
        public int SkinnedRenderersWithoutLodGroup;
        public int NonStaticMeshRendererCount;
        public int ActiveNonStaticMeshRendererCount;
        public int ActiveStaticBatchingCandidateCount;
        public int ActiveDynamicNonStaticMeshRendererCount;
        public int MultiMaterialRendererCount;
        public HashSet<Material> UniqueMaterials;
    }
    #endregion
}
