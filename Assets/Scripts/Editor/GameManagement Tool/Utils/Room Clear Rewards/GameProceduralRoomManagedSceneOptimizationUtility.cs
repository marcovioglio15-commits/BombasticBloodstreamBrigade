#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Consolidates duplicated managed procedural-room environment roots into theme-specific prefabs and removes empty separators.
/// </summary>
internal static class GameProceduralRoomManagedSceneOptimizationUtility
{
    #region Constants
    private const string MetroSceneFolder =
        "Assets/Scenes/LevelGenerationSceneSetTest/MetroConcourse";
    private const string MaintenanceSceneFolder =
        "Assets/Scenes/LevelGenerationSceneSetTest/MaintenanceTunnels";
    private const string EnvironmentPrefabFolder =
        "Assets/Prefabs/RoomAuthoring/Managed";
    private const string MetroEnvironmentPrefabPath =
        EnvironmentPrefabFolder + "/PF_LGTEST_MetroManagedEnvironment.prefab";
    private const string MaintenanceEnvironmentPrefabPath =
        EnvironmentPrefabFolder + "/PF_LGTEST_MaintenanceManagedEnvironment.prefab";
    private const string MetroEnvironmentInstanceName = "Metro Managed Environment";
    private const string MaintenanceEnvironmentInstanceName = "Maintenance Managed Environment";
    private const string CollisionSourceName = "JOhn Sex";
    private const string MetroCollisionDisplayName = "Metro Collision Mesh";
    private const string MaintenanceCollisionDisplayName = "Maintenance Collision Mesh";

    private static readonly string[] ReusableRootNames =
    {
        "Directional Light",
        "Global Volume",
        "metro_blockout",
        CollisionSourceName,
        "Environment_modules",
        "Decals"
    };

    private static readonly string[] EmptyRootNames =
    {
        "Camera-----------------------------",
        "------------------------------------------------------",
        "SubScenes-------------------------------------------",
        "----------------------------------------------------------",
        "Managers------------------------------------------",
        "Canvas------------------------------------------------------",
        "Env------------------------------------------------------ "
    };
    #endregion

    #region Methods

    #region Entry Point
    /// <summary>
    /// Creates or reuses one common managed environment prefab per authored room theme and normalizes every scene.
    /// </summary>
    public static void Configure()
    {
        ConfigureProfile(MetroSceneFolder,
                         "SCN_LGTEST_METRO_",
                         MetroEnvironmentPrefabPath,
                         MetroEnvironmentInstanceName,
                         MetroCollisionDisplayName);
        ConfigureProfile(MaintenanceSceneFolder,
                         "SCN_LGTEST_MAINT_",
                         MaintenanceEnvironmentPrefabPath,
                         MaintenanceEnvironmentInstanceName,
                         MaintenanceCollisionDisplayName);
    }

    /// <summary>
    /// Validates and consolidates every managed scene belonging to one visual room theme.
    /// </summary>
    /// <param name="sceneFolder">Project folder containing managed room scenes.</param>
    /// <param name="scenePrefix">Managed scene-name prefix excluding ECS SubScenes.</param>
    /// <param name="prefabPath">Theme-specific shared environment prefab path.</param>
    /// <param name="instanceName">Stable scene-root name used by the prefab instance.</param>
    /// <param name="collisionDisplayName">Readable collision mesh name stored in the shared prefab.</param>
    private static void ConfigureProfile(string sceneFolder,
                                         string scenePrefix,
                                         string prefabPath,
                                         string instanceName,
                                         string collisionDisplayName)
    {
        List<string> scenePaths = CollectManagedScenePaths(sceneFolder,
                                                           scenePrefix);

        if (scenePaths.Count == 0)
            return;

        GameObject environmentPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (environmentPrefab == null)
        {
            ValidateReusableRoots(scenePaths);
            environmentPrefab = CreateEnvironmentPrefab(scenePaths[0],
                                                        prefabPath,
                                                        instanceName,
                                                        collisionDisplayName);
        }

        for (int index = 0; index < scenePaths.Count; index++)
            OptimizeScene(scenePaths[index],
                          environmentPrefab,
                          instanceName);
    }
    #endregion

    #region Scene Discovery
    /// <summary>
    /// Collects one theme's managed room scene paths in deterministic asset-path order.
    /// </summary>
    /// <param name="sceneFolder">Project folder searched for managed scenes.</param>
    /// <param name="scenePrefix">Required scene-name prefix.</param>
    /// <returns>Sorted project-relative scene paths excluding ECS SubScenes.</returns>
    private static List<string> CollectManagedScenePaths(string sceneFolder,
                                                         string scenePrefix)
    {
        List<string> scenePaths = new List<string>();
        string[] sceneGuids =
            AssetDatabase.FindAssets("t:Scene", new string[] { sceneFolder });

        for (int index = 0; index < sceneGuids.Length; index++)
        {
            string scenePath =
                AssetDatabase.GUIDToAssetPath(sceneGuids[index]);

            if (scenePath.Contains("/SubScenes/") ||
                !Path.GetFileNameWithoutExtension(scenePath)
                    .StartsWith(scenePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            scenePaths.Add(scenePath);
        }

        scenePaths.Sort(StringComparer.Ordinal);
        return scenePaths;
    }
    #endregion

    #region Prefab Creation
    /// <summary>
    /// Rejects prefab consolidation when any copied environment hierarchy differs from the source room.
    /// </summary>
    /// <param name="scenePaths">Managed room scenes expected to share identical environment roots.</param>
    private static void ValidateReusableRoots(IReadOnlyList<string> scenePaths)
    {
        Dictionary<string, string> sourceSignatures =
            BuildReusableRootSignatures(scenePaths[0]);

        for (int sceneIndex = 1; sceneIndex < scenePaths.Count; sceneIndex++)
        {
            Dictionary<string, string> candidateSignatures =
                BuildReusableRootSignatures(scenePaths[sceneIndex]);

            for (int rootIndex = 0;
                 rootIndex < ReusableRootNames.Length;
                 rootIndex++)
            {
                string rootName = ReusableRootNames[rootIndex];

                if (!candidateSignatures.TryGetValue(
                        rootName,
                        out string candidateSignature) ||
                    !sourceSignatures.TryGetValue(
                        rootName,
                        out string sourceSignature) ||
                    !string.Equals(sourceSignature,
                                   candidateSignature,
                                   StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Managed room environment root '" +
                        rootName +
                        "' differs in scene '" +
                        scenePaths[sceneIndex] +
                        "' and cannot be consolidated without overrides.");
                }
            }
        }
    }

    /// <summary>
    /// Builds normalized serialized signatures for every reusable root in one scene.
    /// </summary>
    /// <param name="scenePath">Managed room scene to inspect.</param>
    /// <returns>Root-name to normalized hierarchy signature map.</returns>
    private static Dictionary<string, string> BuildReusableRootSignatures(
        string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(
            scenePath,
            OpenSceneMode.Additive);

        try
        {
            Dictionary<string, string> signatures =
                new Dictionary<string, string>(StringComparer.Ordinal);

            for (int index = 0; index < ReusableRootNames.Length; index++)
            {
                string rootName = ReusableRootNames[index];
                GameObject root = FindRoot(scene, rootName);

                if (root == null)
                    throw new InvalidOperationException(
                        "Managed room scene '" +
                        scenePath +
                        "' is missing reusable root '" +
                        rootName +
                        "'.");

                StringBuilder signature = new StringBuilder();
                AppendHierarchySignature(signature, root.transform);
                signatures.Add(rootName, signature.ToString());
            }

            return signatures;
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    /// <summary>
    /// Creates the common environment prefab by cloning validated roots from the first managed room.
    /// </summary>
    /// <param name="sourceScenePath">Managed source scene containing validated reusable roots.</param>
    /// <param name="prefabPath">Theme-specific output prefab path.</param>
    /// <param name="instanceName">Stable prefab-root name.</param>
    /// <param name="collisionDisplayName">Readable replacement name for the legacy collision mesh.</param>
    /// <returns>Created shared environment prefab asset.</returns>
    private static GameObject CreateEnvironmentPrefab(string sourceScenePath,
                                                      string prefabPath,
                                                      string instanceName,
                                                      string collisionDisplayName)
    {
        Directory.CreateDirectory(EnvironmentPrefabFolder);
        Scene scene = EditorSceneManager.OpenScene(
            sourceScenePath,
            OpenSceneMode.Additive);
        GameObject template = new GameObject(instanceName);

        try
        {
            for (int index = 0; index < ReusableRootNames.Length; index++)
            {
                GameObject source = FindRoot(scene, ReusableRootNames[index]);
                GameObject copy = UnityEngine.Object.Instantiate(source);
                copy.name = string.Equals(source.name,
                                          CollisionSourceName,
                                          StringComparison.Ordinal)
                    ? collisionDisplayName
                    : source.name;
                copy.transform.SetParent(template.transform, true);
            }

            GameObject prefab =
                PrefabUtility.SaveAsPrefabAsset(template,
                                                prefabPath);

            if (prefab == null)
                throw new InvalidOperationException(
                    "Unity could not create the shared managed environment prefab '" +
                    prefabPath +
                    "'.");

            return prefab;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(template);
            EditorSceneManager.CloseScene(scene, true);
        }
    }
    #endregion

    #region Scene Optimization
    /// <summary>
    /// Replaces duplicated environment roots and removes known empty hierarchy-only objects in one room scene.
    /// </summary>
    /// <param name="scenePath">Managed room scene to normalize.</param>
    /// <param name="environmentPrefab">Shared environment prefab replacing duplicated content.</param>
    /// <param name="instanceName">Stable shared environment instance name for this theme.</param>
    private static void OptimizeScene(string scenePath,
                                      GameObject environmentPrefab,
                                      string instanceName)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;

        if (!wasLoaded)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        try
        {
            RemoveReusableRoots(scene, instanceName);
            RemoveKnownEmptyRoots(scene);
            GameObject environmentInstance =
                PrefabUtility.InstantiatePrefab(environmentPrefab,
                                                scene) as GameObject;

            if (environmentInstance == null)
                throw new InvalidOperationException(
                    "Unity could not instantiate the shared managed environment in '" +
                    scenePath +
                    "'.");

            environmentInstance.name = instanceName;
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    /// <summary>
    /// Removes source roots and a previous shared environment instance before idempotent recreation.
    /// </summary>
    /// <param name="scene">Managed room scene being normalized.</param>
    /// <param name="instanceName">Theme-specific shared environment root name.</param>
    private static void RemoveReusableRoots(Scene scene,
                                            string instanceName)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int rootIndex = roots.Length - 1; rootIndex >= 0; rootIndex--)
        {
            GameObject root = roots[rootIndex];

            if (string.Equals(root.name,
                              instanceName,
                              StringComparison.Ordinal) ||
                ContainsName(ReusableRootNames, root.name))
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }

    /// <summary>
    /// Removes only known Transform-only separator or label roots, preserving any object that gained real content.
    /// </summary>
    /// <param name="scene">Managed room scene being normalized.</param>
    private static void RemoveKnownEmptyRoots(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int rootIndex = roots.Length - 1; rootIndex >= 0; rootIndex--)
        {
            GameObject root = roots[rootIndex];
            bool isKnownName = ContainsName(EmptyRootNames, root.name) ||
                               root.name.StartsWith(
                                   "[Level Generation Test]",
                                   StringComparison.Ordinal);

            if (!isKnownName ||
                root.transform.childCount > 0 ||
                root.GetComponents<Component>().Length > 1)
            {
                continue;
            }

            UnityEngine.Object.DestroyImmediate(root);
        }
    }
    #endregion

    #region Signature
    /// <summary>
    /// Appends normalized hierarchy, transform, component and asset-reference data for prefab compatibility checks.
    /// </summary>
    /// <param name="signature">Signature text under construction.</param>
    /// <param name="transform">Hierarchy transform being inspected.</param>
    private static void AppendHierarchySignature(StringBuilder signature,
                                                 Transform transform)
    {
        GameObject gameObject = transform.gameObject;
        signature.Append(gameObject.name);
        signature.Append('|');
        signature.Append(gameObject.activeSelf);
        signature.Append('|');
        signature.Append(gameObject.layer);
        signature.Append('|');
        AppendVector3(signature, transform.localPosition);
        AppendQuaternion(signature, transform.localRotation);
        AppendVector3(signature, transform.localScale);
        Component[] components = gameObject.GetComponents<Component>();

        for (int componentIndex = 0;
             componentIndex < components.Length;
             componentIndex++)
        {
            Component component = components[componentIndex];

            if (component == null)
            {
                signature.Append("<Missing>");
                continue;
            }

            signature.Append(component.GetType().FullName);
            string json = EditorJsonUtility.ToJson(component, false);
            signature.Append(Regex.Replace(
                json,
                "\"instanceID\"\\s*:\\s*-?\\d+",
                "\"instanceID\":0"));
            AppendAssetReferences(signature, component);
        }

        signature.Append('{');

        for (int childIndex = 0;
             childIndex < transform.childCount;
             childIndex++)
        {
            AppendHierarchySignature(signature,
                                     transform.GetChild(childIndex));
        }

        signature.Append('}');
    }

    /// <summary>
    /// Appends stable asset paths for serialized object references hidden by normalized instance identifiers.
    /// </summary>
    /// <param name="signature">Signature text under construction.</param>
    /// <param name="component">Component whose serialized object references are inspected.</param>
    private static void AppendAssetReferences(StringBuilder signature,
                                              Component component)
    {
        SerializedObject serializedObject = new SerializedObject(component);
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                continue;

            signature.Append(iterator.propertyPath);
            signature.Append('=');
            signature.Append(
                AssetDatabase.GetAssetPath(iterator.objectReferenceValue));
        }
    }

    /// <summary>
    /// Appends one vector with round-trip float formatting.
    /// </summary>
    /// <param name="signature">Signature text under construction.</param>
    /// <param name="value">Vector value to append.</param>
    private static void AppendVector3(StringBuilder signature,
                                      Vector3 value)
    {
        signature.Append(value.x.ToString("R"));
        signature.Append(',');
        signature.Append(value.y.ToString("R"));
        signature.Append(',');
        signature.Append(value.z.ToString("R"));
        signature.Append('|');
    }

    /// <summary>
    /// Appends one quaternion with round-trip float formatting.
    /// </summary>
    /// <param name="signature">Signature text under construction.</param>
    /// <param name="value">Quaternion value to append.</param>
    private static void AppendQuaternion(StringBuilder signature,
                                         Quaternion value)
    {
        signature.Append(value.x.ToString("R"));
        signature.Append(',');
        signature.Append(value.y.ToString("R"));
        signature.Append(',');
        signature.Append(value.z.ToString("R"));
        signature.Append(',');
        signature.Append(value.w.ToString("R"));
        signature.Append('|');
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Finds one exact root name in a loaded scene.
    /// </summary>
    /// <param name="scene">Loaded managed room scene.</param>
    /// <param name="rootName">Exact root name to resolve.</param>
    /// <returns>Matching root GameObject, or null when absent.</returns>
    private static GameObject FindRoot(Scene scene, string rootName)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int index = 0; index < roots.Length; index++)
        {
            if (string.Equals(roots[index].name,
                              rootName,
                              StringComparison.Ordinal))
            {
                return roots[index];
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves whether one exact name exists in a compact immutable name array.
    /// </summary>
    /// <param name="names">Known names to inspect.</param>
    /// <param name="candidate">Candidate name.</param>
    /// <returns>True when an ordinal match exists.</returns>
    private static bool ContainsName(IReadOnlyList<string> names,
                                     string candidate)
    {
        for (int index = 0; index < names.Count; index++)
        {
            if (string.Equals(names[index],
                              candidate,
                              StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
    #endregion

    #endregion
}
#endif
