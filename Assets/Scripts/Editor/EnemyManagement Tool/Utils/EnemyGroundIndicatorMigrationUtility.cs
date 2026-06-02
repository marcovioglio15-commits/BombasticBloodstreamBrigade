using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot Editor utility used to migrate every enemy prefab from the legacy world-space Canvas
/// status bars + tagged plane shadow to the new shader-driven ground indicator. Provides a menu
/// entry under Tools/NashCore/Enemy that scans every PF_Enemy_*.prefab in the project, removes the
/// legacy __EnemyStatusBarsTestUI hierarchy, mounts a dedicated indicator GameObject (Unity Quad
/// primitive + M_EnemyGroundIndicator material + EnemyGroundIndicatorView) directly under the
/// enemy root, disables the legacy SM_FloorShadow renderers so the new shader is the only ground
/// visual, and wires the authoring link.
/// Delete this file (and its menu entry) after the bulk migration succeeds on every enemy prefab.
/// </summary>
public static class EnemyGroundIndicatorMigrationUtility
{
    #region Constants
    private const string MenuPath = "Tools/NashCore/Enemy/Migrate Enemy Prefabs To Ground Indicator";
    private const string LegacyTestUiRootObjectName = "__EnemyStatusBarsTestUI";
    private const string GroundIndicatorMaterialGuid = "7c2af0d51e3b48a98d5f9b7a0e64c391";
    private const string EnemyShadowTag = "EnemyShadow";
    private const string IndicatorGameObjectName = "__EnemyGroundIndicator";
    private const string PrefabSearchFolder = "Assets/Prefabs/Enemies";
    #endregion

    #region Methods

    #region Menu Entry
    //[MenuItem(MenuPath)]
    public static void MigrateAllEnemyPrefabs()
    {
        // Resolve the migration material and the built-in Quad mesh once so each prefab reuses them.
        Material indicatorMaterial = ResolveGroundIndicatorMaterial();

        if (indicatorMaterial == null)
        {
            EditorUtility.DisplayDialog("Enemy Ground Indicator Migration",
                                         "Could not load M_EnemyGroundIndicator material at guid " + GroundIndicatorMaterialGuid + ". Import the new shader/material first.",
                                         "OK");
            return;
        }

        Mesh quadMesh = ResolveBuiltinQuadMesh();

        if (quadMesh == null)
        {
            EditorUtility.DisplayDialog("Enemy Ground Indicator Migration",
                                         "Could not load Unity built-in Quad mesh. Aborting migration.",
                                         "OK");
            return;
        }

        string[] enemyPrefabPaths = ResolveEnemyPrefabPaths();

        if (enemyPrefabPaths.Length == 0)
        {
            EditorUtility.DisplayDialog("Enemy Ground Indicator Migration",
                                         "No PF_Enemy_*.prefab files were found under " + PrefabSearchFolder + ".",
                                         "OK");
            return;
        }

        List<string> migratedPrefabs = new List<string>(enemyPrefabPaths.Length);
        List<string> skippedPrefabs = new List<string>(enemyPrefabPaths.Length);

        for (int prefabIndex = 0; prefabIndex < enemyPrefabPaths.Length; prefabIndex++)
        {
            string prefabPath = enemyPrefabPaths[prefabIndex];

            if (TryMigratePrefab(prefabPath, indicatorMaterial, quadMesh))
                migratedPrefabs.Add(prefabPath);
            else
                skippedPrefabs.Add(prefabPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary = string.Format("Migrated {0} prefabs. Skipped {1}.\n\nMigrated:\n{2}\n\nSkipped:\n{3}",
                                       migratedPrefabs.Count,
                                       skippedPrefabs.Count,
                                       string.Join("\n", migratedPrefabs),
                                       string.Join("\n", skippedPrefabs));
        EditorUtility.DisplayDialog("Enemy Ground Indicator Migration", summary, "OK");
    }
    #endregion

    #region Migration
    /// <summary>
    /// Migrates a single prefab to the new ground indicator pipeline. Loads the prefab contents,
    /// rewires the authoring reference, removes the legacy Canvas and saves the prefab in place.
    /// </summary>
    /// <param name="prefabPath">Project-relative path of the prefab to migrate.</param>
    /// <param name="indicatorMaterial">Shared M_EnemyGroundIndicator material applied to the new indicator renderer.</param>
    /// <param name="quadMesh">Unity built-in Quad mesh used as the indicator geometry.</param>
    /// <returns>True when the prefab was modified and saved, false otherwise.</returns>
    private static bool TryMigratePrefab(string prefabPath, Material indicatorMaterial, Mesh quadMesh)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        if (prefabRoot == null)
            return false;

        bool prefabChanged = false;

        try
        {
            EnemyAuthoring authoring = prefabRoot.GetComponentInChildren<EnemyAuthoring>(true);

            if (authoring == null)
                return false;

            Transform authoringTransform = authoring.transform;

            // Resolve or create the dedicated indicator GameObject directly under the enemy authoring root.
            EnemyGroundIndicatorView indicatorView = FindIndicatorView(authoringTransform);

            if (indicatorView == null)
            {
                indicatorView = CreateIndicatorGameObject(authoringTransform, quadMesh, indicatorMaterial);
                prefabChanged = true;
            }
            else
            {
                ApplyIndicatorRenderer(indicatorView, quadMesh, indicatorMaterial);
                prefabChanged = true;
            }

            WireIndicatorViewSerializedFields(indicatorView);
            WireAuthoringGroundIndicator(authoring, indicatorView, ref prefabChanged);

            // Disable the legacy SM_FloorShadow renderers so the new shader is the only ground visual.
            if (DisableLegacyShadowRenderers(prefabRoot.transform))
                prefabChanged = true;

            // Remove the legacy Canvas-based test UI hierarchy if present.
            if (RemoveLegacyTestUi(prefabRoot.transform))
                prefabChanged = true;

            if (prefabChanged)
            {
                EditorUtility.SetDirty(authoring);
                EditorUtility.SetDirty(indicatorView);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        return prefabChanged;
    }

    /// <summary>
    /// Creates the dedicated ground indicator GameObject under the enemy authoring root and configures
    /// it with a Unity Quad mesh, the shared indicator material and the view component. Positions the
    /// quad at a small height offset above the ground plane so it never z-fights with floor geometry.
    /// </summary>
    /// <param name="authoringTransform">Enemy authoring root used as the indicator parent.</param>
    /// <param name="quadMesh">Unity built-in Quad mesh assigned to the indicator MeshFilter.</param>
    /// <param name="indicatorMaterial">Shared indicator material assigned to the indicator MeshRenderer.</param>
    /// <returns>The view component on the newly created indicator GameObject.</returns>
    private static EnemyGroundIndicatorView CreateIndicatorGameObject(Transform authoringTransform, Mesh quadMesh, Material indicatorMaterial)
    {
        // The GameObject is created inactive and the renderer is left disabled so the bake pipeline
        // does NOT register an Entities Graphics renderer entity for it. At runtime the managed view
        // enables both and the mesh renders via Unity's standard scene rendering pipeline, where
        // MaterialPropertyBlock overrides work as expected. This mirrors the existing pattern used by
        // __EnemyOffensiveEngagementBillboard so the project's renderer setup stays consistent.
        GameObject indicatorObject = new GameObject(IndicatorGameObjectName);
        Transform indicatorTransform = indicatorObject.transform;
        indicatorTransform.SetParent(authoringTransform, false);
        indicatorTransform.localPosition = new Vector3(0f, 0.035f, 0f);
        indicatorTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        indicatorTransform.localScale = Vector3.one;

        MeshFilter meshFilter = indicatorObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = quadMesh;

        MeshRenderer meshRenderer = indicatorObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterials = new Material[] { indicatorMaterial };
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        meshRenderer.allowOcclusionWhenDynamic = false;
        meshRenderer.enabled = false;

        EnemyGroundIndicatorView indicatorView = indicatorObject.AddComponent<EnemyGroundIndicatorView>();
        indicatorObject.SetActive(false);
        return indicatorView;
    }

    /// <summary>
    /// Applies the canonical Unity Quad mesh and indicator material to an existing indicator view
    /// when re-running the migration over an already-migrated prefab.
    /// </summary>
    /// <param name="indicatorView">Existing indicator view to refresh.</param>
    /// <param name="quadMesh">Unity built-in Quad mesh.</param>
    /// <param name="indicatorMaterial">Shared indicator material.</param>
    private static void ApplyIndicatorRenderer(EnemyGroundIndicatorView indicatorView, Mesh quadMesh, Material indicatorMaterial)
    {
        GameObject indicatorObject = indicatorView.gameObject;
        MeshFilter meshFilter = indicatorObject.GetComponent<MeshFilter>();

        if (meshFilter == null)
            meshFilter = indicatorObject.AddComponent<MeshFilter>();

        meshFilter.sharedMesh = quadMesh;

        MeshRenderer meshRenderer = indicatorObject.GetComponent<MeshRenderer>();

        if (meshRenderer == null)
            meshRenderer = indicatorObject.AddComponent<MeshRenderer>();

        meshRenderer.sharedMaterials = new Material[] { indicatorMaterial };
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        // Force the inactive/disabled bake state so re-running the migration over a prefab that was
        // previously baked with an active GameObject also skips the Entities Graphics renderer entity.
        meshRenderer.enabled = false;
        indicatorObject.SetActive(false);
    }

    /// <summary>
    /// Wires the indicator view serialized references to the renderer/mesh on the same GameObject.
    /// </summary>
    /// <param name="indicatorView">Indicator view that owns the serialized renderer references.</param>
    private static void WireIndicatorViewSerializedFields(EnemyGroundIndicatorView indicatorView)
    {
        GameObject indicatorObject = indicatorView.gameObject;
        MeshFilter meshFilter = indicatorObject.GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = indicatorObject.GetComponent<MeshRenderer>();
        SerializedObject viewSerializedObject = new SerializedObject(indicatorView);
        SerializedProperty meshRendererProperty = viewSerializedObject.FindProperty("meshRenderer");
        SerializedProperty meshFilterProperty = viewSerializedObject.FindProperty("meshFilter");
        SerializedProperty visibilityRootProperty = viewSerializedObject.FindProperty("visibilityRoot");
        viewSerializedObject.Update();

        if (meshRendererProperty != null)
            meshRendererProperty.objectReferenceValue = meshRenderer;

        if (meshFilterProperty != null)
            meshFilterProperty.objectReferenceValue = meshFilter;

        if (visibilityRootProperty != null)
            visibilityRootProperty.objectReferenceValue = indicatorObject;

        viewSerializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Wires EnemyAuthoring.groundIndicatorView to the new view component so the baker picks up the link.
    /// </summary>
    /// <param name="authoring">Authoring component to update.</param>
    /// <param name="indicatorView">Indicator view assigned to the authoring serialized field.</param>
    /// <param name="prefabChanged">Set to true when the field had to be updated.</param>
    private static void WireAuthoringGroundIndicator(EnemyAuthoring authoring, EnemyGroundIndicatorView indicatorView, ref bool prefabChanged)
    {
        SerializedObject authoringSerializedObject = new SerializedObject(authoring);
        SerializedProperty groundIndicatorViewProperty = authoringSerializedObject.FindProperty("groundIndicatorView");
        authoringSerializedObject.Update();

        if (groundIndicatorViewProperty != null && groundIndicatorViewProperty.objectReferenceValue != indicatorView)
        {
            groundIndicatorViewProperty.objectReferenceValue = indicatorView;
            prefabChanged = true;
        }

        authoringSerializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Finds the dedicated indicator view (created by a previous migration run) under the enemy root.
    /// </summary>
    /// <param name="rootTransform">Enemy authoring root transform.</param>
    /// <returns>Existing view or null when no migration has run yet.</returns>
    private static EnemyGroundIndicatorView FindIndicatorView(Transform rootTransform)
    {
        if (rootTransform == null)
            return null;

        return rootTransform.GetComponentInChildren<EnemyGroundIndicatorView>(true);
    }

    /// <summary>
    /// Disables every MeshRenderer that lives under a transform tagged EnemyShadow so the new shader
    /// is the only visible ground footprint. Preserves the GameObject (other code reads the tag).
    /// </summary>
    /// <param name="rootTransform">Prefab root transform to scan.</param>
    /// <returns>True when at least one renderer was disabled.</returns>
    private static bool DisableLegacyShadowRenderers(Transform rootTransform)
    {
        if (rootTransform == null)
            return false;

        bool disabledAny = false;
        Transform[] childTransforms = rootTransform.GetComponentsInChildren<Transform>(true);

        for (int childIndex = 0; childIndex < childTransforms.Length; childIndex++)
        {
            Transform candidate = childTransforms[childIndex];

            if (candidate == null)
                continue;

            if (!candidate.CompareTag(EnemyShadowTag))
                continue;

            MeshRenderer[] legacyRenderers = candidate.GetComponentsInChildren<MeshRenderer>(true);

            for (int rendererIndex = 0; rendererIndex < legacyRenderers.Length; rendererIndex++)
            {
                MeshRenderer legacyRenderer = legacyRenderers[rendererIndex];

                if (legacyRenderer == null)
                    continue;

                if (!legacyRenderer.enabled)
                    continue;

                legacyRenderer.enabled = false;
                disabledAny = true;
            }
        }

        return disabledAny;
    }

    /// <summary>
    /// Removes the legacy world-space status bars Canvas hierarchy from a prefab, including all of its
    /// fillable Image children. Used as a one-shot cleanup during migration.
    /// </summary>
    /// <param name="rootTransform">Prefab root transform searched for the legacy hierarchy.</param>
    /// <returns>True when at least one hierarchy was removed.</returns>
    private static bool RemoveLegacyTestUi(Transform rootTransform)
    {
        if (rootTransform == null)
            return false;

        bool removedAny = false;
        Transform[] childTransforms = rootTransform.GetComponentsInChildren<Transform>(true);

        for (int childIndex = 0; childIndex < childTransforms.Length; childIndex++)
        {
            Transform candidate = childTransforms[childIndex];

            if (candidate == null)
                continue;

            if (!string.Equals(candidate.name, LegacyTestUiRootObjectName, System.StringComparison.Ordinal))
                continue;

            Object.DestroyImmediate(candidate.gameObject);
            removedAny = true;
        }

        return removedAny;
    }

    /// <summary>
    /// Resolves the shared M_EnemyGroundIndicator material by GUID so the migration stays valid
    /// even when the project root path differs from the working tree.
    /// </summary>
    /// <returns>The loaded material asset, or null when the material cannot be resolved.</returns>
    private static Material ResolveGroundIndicatorMaterial()
    {
        string materialPath = AssetDatabase.GUIDToAssetPath(GroundIndicatorMaterialGuid);

        if (string.IsNullOrWhiteSpace(materialPath))
            return null;

        return AssetDatabase.LoadAssetAtPath<Material>(materialPath);
    }

    /// <summary>
    /// Resolves the Unity built-in Quad mesh used as the indicator geometry. The Quad is preferred
    /// over Plane because its 1×1 footprint and predictable UV mapping make shader math trivial.
    /// </summary>
    /// <returns>Unity built-in Quad mesh, or null when the asset cannot be resolved.</returns>
    private static Mesh ResolveBuiltinQuadMesh()
    {
        // GameObject.CreatePrimitive yields the same mesh asset and is more robust than relying on
        // hardcoded fileIDs when the editor swaps the built-in resource map between Unity versions.
        GameObject scratchObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Mesh resolvedMesh = null;

        try
        {
            MeshFilter scratchMeshFilter = scratchObject.GetComponent<MeshFilter>();

            if (scratchMeshFilter != null)
                resolvedMesh = scratchMeshFilter.sharedMesh;
        }
        finally
        {
            Object.DestroyImmediate(scratchObject);
        }

        return resolvedMesh;
    }

    /// <summary>
    /// Returns every PF_Enemy_*.prefab path discovered under the canonical enemy prefab folder.
    /// </summary>
    /// <returns>Array of project-relative prefab paths.</returns>
    private static string[] ResolveEnemyPrefabPaths()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab PF_Enemy_", new string[] { PrefabSearchFolder });
        List<string> results = new List<string>(guids.Length);

        for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);

            if (string.IsNullOrWhiteSpace(assetPath))
                continue;

            results.Add(assetPath);
        }

        return results.ToArray();
    }
    #endregion

    #endregion
}
