using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tracks Game Management Tool draft edits, staged deletes, apply/rename operations and discard restoration.
/// /params None.
/// /returns None.
/// </summary>
public static class GameManagementDraftSession
{
    #region Constants
    private const string TrackedGameAssetsRoot = "Assets/Scriptable Objects/Game";
    private const string TrackedProjectRoot = "Assets";
    #endregion

    #region Fields
    private static readonly Dictionary<string, string> baselineJsonByPath = new Dictionary<string, string>();
    private static readonly HashSet<string> stagedDeletePaths = new HashSet<string>();

    private static bool isInitialized;
    private static bool hasPendingChanges;
    #endregion

    #region Properties
    public static bool IsInitialized
    {
        get
        {
            return isInitialized;
        }
    }

    public static bool HasPendingChanges
    {
        get
        {
            return hasPendingChanges;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Captures the current editable asset baseline for the tool session.
    /// /params None.
    /// /returns None.
    /// </summary>
    public static void BeginSession()
    {
        EnsureTrackedDefaults();
        CaptureBaseline();
        stagedDeletePaths.Clear();
        isInitialized = true;
        hasPendingChanges = false;
    }

    /// <summary>
    /// Clears all draft session state when the tool closes without pending changes.
    /// /params None.
    /// /returns None.
    /// </summary>
    public static void EndSession()
    {
        isInitialized = false;
        hasPendingChanges = false;
        baselineJsonByPath.Clear();
        stagedDeletePaths.Clear();
    }

    /// <summary>
    /// Stages one asset for deletion on Apply while hiding it from tool lists.
    /// /params asset Asset object to stage.
    /// /returns None.
    /// </summary>
    public static void StageDeleteAsset(UnityEngine.Object asset)
    {
        if (asset == null)
            return;

        string assetPath = AssetDatabase.GetAssetPath(asset);

        if (string.IsNullOrWhiteSpace(assetPath))
            return;

        stagedDeletePaths.Add(assetPath);
        hasPendingChanges = true;
    }

    /// <summary>
    /// Checks whether an asset is currently staged for deletion.
    /// /params asset Asset object to inspect.
    /// /returns True when the asset path is staged.
    /// </summary>
    public static bool IsAssetStagedForDeletion(UnityEngine.Object asset)
    {
        if (asset == null)
            return false;

        string assetPath = AssetDatabase.GetAssetPath(asset);

        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        return stagedDeletePaths.Contains(assetPath);
    }

    /// <summary>
    /// Performs one Unity Undo step and refreshes the pending-change flag.
    /// /params None.
    /// /returns None.
    /// </summary>
    public static void PerformUndo()
    {
        Undo.PerformUndo();
        RecomputePendingChanges();
    }

    /// <summary>
    /// Performs one Unity Redo step and refreshes the pending-change flag.
    /// /params None.
    /// /returns None.
    /// </summary>
    public static void PerformRedo()
    {
        Undo.PerformRedo();
        RecomputePendingChanges();
    }

    /// <summary>
    /// Marks the session dirty after a tool-side asset mutation.
    /// /params None.
    /// /returns None.
    /// </summary>
    public static void MarkDirty()
    {
        hasPendingChanges = true;
    }

    /// <summary>
    /// Rebuilds the current state snapshot and compares it against the captured baseline.
    /// /params None.
    /// /returns None.
    /// </summary>
    public static void RecomputePendingChanges()
    {
        if (!isInitialized)
        {
            hasPendingChanges = false;
            return;
        }

        SyncStagedDeletePaths();

        if (stagedDeletePaths.Count > 0)
        {
            hasPendingChanges = true;
            return;
        }

        Dictionary<string, string> currentState = BuildStateDictionary();

        if (currentState.Count != baselineJsonByPath.Count)
        {
            hasPendingChanges = true;
            return;
        }

        foreach (KeyValuePair<string, string> baselineEntry in baselineJsonByPath)
        {
            if (!currentState.TryGetValue(baselineEntry.Key, out string currentJson))
            {
                hasPendingChanges = true;
                return;
            }

            if (!string.Equals(baselineEntry.Value, currentJson, StringComparison.Ordinal))
            {
                hasPendingChanges = true;
                return;
            }
        }

        hasPendingChanges = false;
    }

    /// <summary>
    /// Applies staged deletes, asset renames and saves the accepted baseline.
    /// /params None.
    /// /returns None.
    /// </summary>
    public static void Apply()
    {
        ExecuteStagedDeletions();
        ExecuteRenames();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        CaptureBaseline();
        stagedDeletePaths.Clear();
        hasPendingChanges = false;
    }

    /// <summary>
    /// Restores baseline JSON and removes newly created game assets from the draft session.
    /// /params None.
    /// /returns None.
    /// </summary>
    public static void Discard()
    {
        if (!isInitialized)
            return;

        RestoreBaselineAssets();
        DeleteAssetsCreatedAfterBaseline();
        stagedDeletePaths.Clear();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        CaptureBaseline();
        hasPendingChanges = false;
    }

    /// <summary>
    /// Normalizes text into a file-safe asset name.
    /// /params rawName Raw user-authored name.
    /// /returns Safe filename text.
    /// </summary>
    public static string NormalizeAssetName(string rawName)
    {
        return GameManagementAssetUtility.NormalizeAssetName(rawName);
    }
    #endregion

    #region Session Helpers
    /// <summary>
    /// Captures the current tracked state as the clean draft baseline.
    /// /params None.
    /// /returns None.
    /// </summary>
    private static void CaptureBaseline()
    {
        baselineJsonByPath.Clear();
        Dictionary<string, string> currentState = BuildStateDictionary();

        foreach (KeyValuePair<string, string> stateEntry in currentState)
            baselineJsonByPath[stateEntry.Key] = stateEntry.Value;
    }

    /// <summary>
    /// Builds a path-to-json dictionary for all tracked Game Management assets and authoring prefabs.
    /// /params None.
    /// /returns Current serialized state dictionary.
    /// </summary>
    private static Dictionary<string, string> BuildStateDictionary()
    {
        Dictionary<string, string> stateByPath = new Dictionary<string, string>();
        List<string> assetPaths = CollectTrackedAssetPaths();

        for (int pathIndex = 0; pathIndex < assetPaths.Count; pathIndex++)
        {
            string assetPath = assetPaths[pathIndex];

            if (string.IsNullOrWhiteSpace(assetPath))
                continue;

            UnityEngine.Object assetObject = AssetDatabase.LoadMainAssetAtPath(assetPath);

            if (assetObject == null)
                continue;

            stateByPath[assetPath] = EditorJsonUtility.ToJson(assetObject, true);
        }

        return stateByPath;
    }

    /// <summary>
    /// Collects all assets that must participate in apply/discard tracking.
    /// /params None.
    /// /returns Sorted list of tracked asset paths.
    /// </summary>
    private static List<string> CollectTrackedAssetPaths()
    {
        HashSet<string> uniquePaths = new HashSet<string>();
        AddAssetPathsOfType<GameMasterPresetLibrary>(uniquePaths, TrackedGameAssetsRoot);
        AddAssetPathsOfType<GameMasterPreset>(uniquePaths, TrackedGameAssetsRoot);
        AddAssetPathsOfType<GameAudioManagerPresetLibrary>(uniquePaths, TrackedGameAssetsRoot);
        AddAssetPathsOfType<GameAudioManagerPreset>(uniquePaths, TrackedGameAssetsRoot);
        AddAssetPathsOfType<GameSceneManagerPresetLibrary>(uniquePaths, TrackedGameAssetsRoot);
        AddAssetPathsOfType<GameSceneManagerPreset>(uniquePaths, TrackedGameAssetsRoot);
        AddAudioManagerPrefabPaths(uniquePaths);
        AddSceneManagerPrefabPaths(uniquePaths);

        List<string> paths = new List<string>(uniquePaths);
        paths.Sort(StringComparer.Ordinal);
        return paths;
    }

    /// <summary>
    /// Adds all assets of one Unity type below a search root.
    /// /params uniquePaths Output set receiving project-relative paths.
    /// /params searchRoot Search folder.
    /// /returns None.
    /// </summary>
    private static void AddAssetPathsOfType<TAsset>(HashSet<string> uniquePaths, string searchRoot) where TAsset : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(searchRoot))
            return;

        if (!AssetDatabase.IsValidFolder(searchRoot))
            return;

        string[] guids = AssetDatabase.FindAssets("t:" + typeof(TAsset).Name, new string[] { searchRoot });

        for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);

            if (string.IsNullOrWhiteSpace(path))
                continue;

            uniquePaths.Add(path);
        }
    }

    /// <summary>
    /// Creates the default Game Management asset roots and libraries before baseline tracking starts.
    /// /params None.
    /// /returns None.
    /// </summary>
    private static void EnsureTrackedDefaults()
    {
        GameManagementAssetUtility.EnsureFolder(TrackedGameAssetsRoot);
        GameMasterPresetLibraryUtility.GetOrCreateLibrary();
        GameAudioManagerPresetLibraryUtility.GetOrCreateLibrary();
        GameSceneManagerPresetLibraryUtility.GetOrCreateLibrary();
    }

    /// <summary>
    /// Adds prefab assets that contain a GameAudioManagerAuthoring component.
    /// /params uniquePaths Output set receiving project-relative paths.
    /// /returns None.
    /// </summary>
    private static void AddAudioManagerPrefabPaths(HashSet<string> uniquePaths)
    {
        AddPrefabPathsWithComponent<GameAudioManagerAuthoring>(uniquePaths);
    }

    /// <summary>
    /// Adds prefab assets that contain a GameSceneManagerAuthoring component.
    /// /params uniquePaths Output set receiving project-relative paths.
    /// /returns None.
    /// </summary>
    private static void AddSceneManagerPrefabPaths(HashSet<string> uniquePaths)
    {
        AddPrefabPathsWithComponent<GameSceneManagerAuthoring>(uniquePaths);
    }

    /// <summary>
    /// Adds prefab assets that contain one component type to the tracked path set.
    /// /params uniquePaths Output set receiving project-relative paths.
    /// /returns None.
    /// </summary>
    private static void AddPrefabPathsWithComponent<TComponent>(HashSet<string> uniquePaths) where TComponent : Component
    {
        List<GameObject> prefabs = ManagementToolPrefabUtility.FindPrefabsWithComponentInHierarchy<TComponent>(new string[] { TrackedProjectRoot });

        for (int index = 0; index < prefabs.Count; index++)
        {
            GameObject prefab = prefabs[index];

            if (prefab == null)
                continue;

            string prefabPath = AssetDatabase.GetAssetPath(prefab);

            if (string.IsNullOrWhiteSpace(prefabPath))
                continue;

            uniquePaths.Add(prefabPath);
        }
    }

    /// <summary>
    /// Restores all baseline assets through Unity JSON overwrite.
    /// /params None.
    /// /returns None.
    /// </summary>
    private static void RestoreBaselineAssets()
    {
        foreach (KeyValuePair<string, string> baselineEntry in baselineJsonByPath)
        {
            UnityEngine.Object assetObject = AssetDatabase.LoadMainAssetAtPath(baselineEntry.Key);

            if (assetObject == null)
                continue;

            EditorJsonUtility.FromJsonOverwrite(baselineEntry.Value, assetObject);
            EditorUtility.SetDirty(assetObject);
        }
    }

    /// <summary>
    /// Deletes newly created Game ScriptableObject assets that were not part of the baseline.
    /// /params None.
    /// /returns None.
    /// </summary>
    private static void DeleteAssetsCreatedAfterBaseline()
    {
        List<string> currentPaths = CollectTrackedAssetPaths();

        for (int pathIndex = 0; pathIndex < currentPaths.Count; pathIndex++)
        {
            string currentPath = currentPaths[pathIndex];

            if (!currentPath.StartsWith(TrackedGameAssetsRoot, StringComparison.Ordinal))
                continue;

            if (baselineJsonByPath.ContainsKey(currentPath))
                continue;

            AssetDatabase.DeleteAsset(currentPath);
        }
    }

    /// <summary>
    /// Deletes assets that were staged and no longer referenced by libraries.
    /// /params None.
    /// /returns None.
    /// </summary>
    private static void ExecuteStagedDeletions()
    {
        List<string> stagedPaths = new List<string>(stagedDeletePaths);

        for (int pathIndex = 0; pathIndex < stagedPaths.Count; pathIndex++)
        {
            string stagedPath = stagedPaths[pathIndex];

            if (string.IsNullOrWhiteSpace(stagedPath))
                continue;

            if (AssetDatabase.LoadMainAssetAtPath(stagedPath) == null)
                continue;

            AssetDatabase.DeleteAsset(stagedPath);
        }
    }

    /// <summary>
    /// Renames preset assets according to their serialized presetName fields during Apply.
    /// /params None.
    /// /returns None.
    /// </summary>
    private static void ExecuteRenames()
    {
        List<string> assetPaths = CollectTrackedAssetPaths();

        for (int pathIndex = 0; pathIndex < assetPaths.Count; pathIndex++)
        {
            string assetPath = assetPaths[pathIndex];

            if (stagedDeletePaths.Contains(assetPath))
                continue;

            UnityEngine.Object assetObject = AssetDatabase.LoadMainAssetAtPath(assetPath);

            if (!IsRenamablePresetAsset(assetObject))
                continue;

            RenamePresetAssetIfNeeded(assetObject, assetPath);
        }
    }

    /// <summary>
    /// Checks whether an asset should be filename-synchronized on Apply.
    /// /params assetObject Candidate asset object.
    /// /returns True for Game Management preset asset types.
    /// </summary>
    private static bool IsRenamablePresetAsset(UnityEngine.Object assetObject)
    {
        return assetObject is GameMasterPreset ||
               assetObject is GameAudioManagerPreset ||
               assetObject is GameSceneManagerPreset;
    }

    /// <summary>
    /// Applies one asset rename when the serialized preset name differs from the current filename.
    /// /params assetObject Preset asset being renamed.
    /// /params assetPath Current asset path.
    /// /returns None.
    /// </summary>
    private static void RenamePresetAssetIfNeeded(UnityEngine.Object assetObject, string assetPath)
    {
        string currentFileName = Path.GetFileNameWithoutExtension(assetPath);
        string targetFileName = ResolveRequestedPresetFileName(assetObject);

        if (string.IsNullOrWhiteSpace(targetFileName))
            return;

        if (string.Equals(currentFileName, targetFileName, StringComparison.Ordinal))
        {
            SyncPresetAssetNameToFileName(assetObject, currentFileName);
            return;
        }

        string directoryPath = Path.GetDirectoryName(assetPath);

        if (string.IsNullOrWhiteSpace(directoryPath))
            return;

        string requestedPath = Path.Combine(directoryPath.Replace('\\', '/'), targetFileName + Path.GetExtension(assetPath)).Replace('\\', '/');
        string uniquePath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);
        string renameError = AssetDatabase.MoveAsset(assetPath, uniquePath);

        if (!string.IsNullOrWhiteSpace(renameError))
        {
            Debug.LogWarning(string.Format("GameManagementDraftSession: failed to rename asset '{0}' to '{1}'. Error: {2}",
                                           assetPath,
                                           targetFileName,
                                           renameError));
            return;
        }

        UnityEngine.Object movedAsset = AssetDatabase.LoadMainAssetAtPath(uniquePath);

        if (movedAsset == null)
            return;

        SyncPresetAssetNameToFileName(movedAsset, Path.GetFileNameWithoutExtension(uniquePath));
    }

    /// <summary>
    /// Resolves the requested filename from presetName, falling back to asset object name.
    /// /params assetObject Preset asset being inspected.
    /// /returns Normalized requested filename.
    /// </summary>
    private static string ResolveRequestedPresetFileName(UnityEngine.Object assetObject)
    {
        SerializedObject serializedObject = new SerializedObject(assetObject);
        SerializedProperty presetNameProperty = serializedObject.FindProperty("presetName");

        if (presetNameProperty != null)
        {
            serializedObject.Update();
            string requestedName = NormalizeAssetName(presetNameProperty.stringValue);

            if (!string.IsNullOrWhiteSpace(requestedName))
                return requestedName;
        }

        return NormalizeAssetName(assetObject.name);
    }

    /// <summary>
    /// Synchronizes asset object name and serialized presetName after an Apply rename.
    /// /params assetObject Preset asset to synchronize.
    /// /params fileName Filename without extension.
    /// /returns None.
    /// </summary>
    private static void SyncPresetAssetNameToFileName(UnityEngine.Object assetObject, string fileName)
    {
        if (assetObject == null || string.IsNullOrWhiteSpace(fileName))
            return;

        assetObject.name = fileName;
        SerializedObject serializedObject = new SerializedObject(assetObject);
        SerializedProperty presetNameProperty = serializedObject.FindProperty("presetName");

        if (presetNameProperty != null)
        {
            serializedObject.Update();
            presetNameProperty.stringValue = fileName;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(assetObject);
    }

    /// <summary>
    /// Removes staged deletes that became referenced again by a library.
    /// /params None.
    /// /returns None.
    /// </summary>
    private static void SyncStagedDeletePaths()
    {
        if (stagedDeletePaths.Count == 0)
            return;

        List<string> stagedPaths = new List<string>(stagedDeletePaths);

        for (int index = 0; index < stagedPaths.Count; index++)
        {
            if (!IsPathReferencedByLibraries(stagedPaths[index]))
                continue;

            stagedDeletePaths.Remove(stagedPaths[index]);
        }
    }

    /// <summary>
    /// Checks whether one asset path is still referenced by a Game Management library.
    /// /params assetPath Project-relative asset path.
    /// /returns True when the path is referenced.
    /// </summary>
    private static bool IsPathReferencedByLibraries(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        GameMasterPresetLibrary masterLibrary = GameMasterPresetLibraryUtility.GetOrCreateLibrary();

        if (LibraryContainsPath(masterLibrary.Presets, assetPath))
            return true;

        GameAudioManagerPresetLibrary audioLibrary = GameAudioManagerPresetLibraryUtility.GetOrCreateLibrary();

        if (LibraryContainsPath(audioLibrary.Presets, assetPath))
            return true;

        GameSceneManagerPresetLibrary sceneLibrary = GameSceneManagerPresetLibraryUtility.GetOrCreateLibrary();
        return LibraryContainsPath(sceneLibrary.Presets, assetPath);
    }

    /// <summary>
    /// Checks whether a typed library list contains an asset at one path.
    /// /params presets Preset list to inspect.
    /// /params assetPath Project-relative asset path.
    /// /returns True when any listed preset resolves to the path.
    /// </summary>
    private static bool LibraryContainsPath<TAsset>(IReadOnlyList<TAsset> presets, string assetPath) where TAsset : UnityEngine.Object
    {
        for (int index = 0; index < presets.Count; index++)
        {
            TAsset preset = presets[index];

            if (preset == null)
                continue;

            string presetPath = AssetDatabase.GetAssetPath(preset);

            if (!string.Equals(presetPath, assetPath, StringComparison.Ordinal))
                continue;

            return true;
        }

        return false;
    }
    #endregion

    #endregion
}
