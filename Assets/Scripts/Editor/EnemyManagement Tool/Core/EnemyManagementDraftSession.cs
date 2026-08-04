using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Manages draft edits for enemy presets and related assets in the Enemy Management Tool.
/// </summary>
public static class EnemyManagementDraftSession
{
    #region Constants
    private const string TrackedEnemyAssetsRoot = "Assets/Scriptable Objects/Enemy";
    private const string TrackedProjectRoot = "Assets";
    #endregion

    #region Fields
    private static readonly Dictionary<string, string> baselineJsonByPath = new Dictionary<string, string>();
    private static readonly HashSet<string> stagedDeletePaths = new HashSet<string>();
    private static readonly ManagementToolDraftChangeVerifier pendingChangesVerifier =
        new ManagementToolDraftChangeVerifier(RecomputePendingChanges);

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
    public static void BeginSession()
    {
        pendingChangesVerifier.Reset();
        CaptureBaseline();
        stagedDeletePaths.Clear();
        isInitialized = true;
        hasPendingChanges = false;
    }

    public static void EndSession()
    {
        pendingChangesVerifier.Reset();
        isInitialized = false;
        hasPendingChanges = false;
        baselineJsonByPath.Clear();
        stagedDeletePaths.Clear();
    }

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

    public static bool IsAssetStagedForDeletion(UnityEngine.Object asset)
    {
        if (asset == null)
            return false;

        string assetPath = AssetDatabase.GetAssetPath(asset);

        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        return stagedDeletePaths.Contains(assetPath);
    }

    public static void PerformUndo()
    {
        Undo.PerformUndo();
        RecomputePendingChanges();
    }

    public static void PerformRedo()
    {
        Undo.PerformRedo();
        RecomputePendingChanges();
    }

    /// <summary>
    /// Verifies one tool-side dirty signal against the serialized baseline before changing pending state.
    /// </summary>
    public static void MarkDirty()
    {
        if (!isInitialized || hasPendingChanges)
            return;

        pendingChangesVerifier.VerifySignal();
    }

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

    public static void Apply()
    {
        pendingChangesVerifier.Reset();
        ExecuteStagedDeletions();
        ExecuteRenames();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        CaptureBaseline();
        stagedDeletePaths.Clear();
        hasPendingChanges = false;
    }

    public static void Discard()
    {
        if (!isInitialized)
            return;

        pendingChangesVerifier.Reset();
        RestoreBaselineAssets();
        DeleteAssetsCreatedAfterBaseline();
        stagedDeletePaths.Clear();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        CaptureBaseline();
        hasPendingChanges = false;
    }

    public static string NormalizeAssetName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        string trimmedName = rawName.Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))
            return string.Empty;

        char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
        StringBuilder builder = new StringBuilder(trimmedName.Length);

        for (int charIndex = 0; charIndex < trimmedName.Length; charIndex++)
        {
            char currentChar = trimmedName[charIndex];
            bool isInvalidCharacter = false;

            for (int invalidIndex = 0; invalidIndex < invalidFileNameChars.Length; invalidIndex++)
            {
                if (currentChar != invalidFileNameChars[invalidIndex])
                    continue;

                isInvalidCharacter = true;
                break;
            }

            if (isInvalidCharacter)
            {
                builder.Append('_');
                continue;
            }

            builder.Append(currentChar);
        }

        string normalizedName = builder.ToString().Trim();

        while (normalizedName.EndsWith(".", StringComparison.Ordinal))
            normalizedName = normalizedName.Substring(0, normalizedName.Length - 1).TrimEnd();

        if (string.IsNullOrWhiteSpace(normalizedName))
            return string.Empty;

        return normalizedName;
    }
    #endregion

    #region Session Helpers
    private static void CaptureBaseline()
    {
        baselineJsonByPath.Clear();

        Dictionary<string, string> currentState = BuildStateDictionary();

        foreach (KeyValuePair<string, string> stateEntry in currentState)
            baselineJsonByPath[stateEntry.Key] = stateEntry.Value;
    }

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

            string serializedJson = EditorJsonUtility.ToJson(assetObject, true);
            stateByPath[assetPath] = serializedJson;
        }

        return stateByPath;
    }

    private static List<string> CollectTrackedAssetPaths()
    {
        HashSet<string> uniquePaths = new HashSet<string>();
        AddAssetPathsOfType<EnemyMasterPresetLibrary>(uniquePaths, TrackedEnemyAssetsRoot);
        AddAssetPathsOfType<EnemyMasterPreset>(uniquePaths, TrackedEnemyAssetsRoot);
        AddAssetPathsOfType<EnemyBrainPresetLibrary>(uniquePaths, TrackedEnemyAssetsRoot);
        AddAssetPathsOfType<EnemyBrainPreset>(uniquePaths, TrackedEnemyAssetsRoot);
        AddAssetPathsOfType<EnemyVisualPresetLibrary>(uniquePaths, TrackedEnemyAssetsRoot);
        AddAssetPathsOfType<EnemyVisualPreset>(uniquePaths, TrackedEnemyAssetsRoot);
        AddAssetPathsOfType<EnemyUiVisualPresetLibrary>(uniquePaths, TrackedEnemyAssetsRoot);
        AddAssetPathsOfType<EnemyUiVisualPreset>(uniquePaths, TrackedEnemyAssetsRoot);
        AddAssetPathsOfType<EnemyAdvancedPatternPresetLibrary>(uniquePaths, TrackedEnemyAssetsRoot);
        AddAssetPathsOfType<EnemyAdvancedPatternPreset>(uniquePaths, TrackedEnemyAssetsRoot);
        AddAssetPathsOfType<EnemyModulesAndPatternsPreset>(uniquePaths, TrackedEnemyAssetsRoot);
        AddAssetPathsOfType<EnemyBossPatternPresetLibrary>(uniquePaths, TrackedEnemyAssetsRoot);
        AddAssetPathsOfType<EnemyBossPatternPreset>(uniquePaths, TrackedEnemyAssetsRoot);
        AddEnemyPrefabPaths(uniquePaths);

        List<string> paths = new List<string>(uniquePaths);
        paths.Sort(StringComparer.Ordinal);
        return paths;
    }

    private static void AddAssetPathsOfType<TAsset>(HashSet<string> uniquePaths, string searchRoot) where TAsset : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(searchRoot))
            return;

        string[] searchFolders = new string[] { searchRoot };
        string[] guids = AssetDatabase.FindAssets("t:" + typeof(TAsset).Name, searchFolders);

        for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
        {
            string guid = guids[guidIndex];
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (string.IsNullOrWhiteSpace(path))
                continue;

            uniquePaths.Add(path);
        }
    }

    private static void AddEnemyPrefabPaths(HashSet<string> uniquePaths)
    {
        string[] searchFolders = new string[] { TrackedProjectRoot };
        List<GameObject> enemyPrefabs = ManagementToolPrefabUtility.FindPrefabsWithComponentInHierarchy<EnemyAuthoring>(searchFolders);

        for (int prefabIndex = 0; prefabIndex < enemyPrefabs.Count; prefabIndex++)
        {
            GameObject prefabAsset = enemyPrefabs[prefabIndex];

            if (prefabAsset == null)
                continue;

            string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);

            if (string.IsNullOrWhiteSpace(prefabPath))
                continue;

            uniquePaths.Add(prefabPath);
        }
    }

    private static void RestoreBaselineAssets()
    {
        foreach (KeyValuePair<string, string> baselineEntry in baselineJsonByPath)
        {
            string assetPath = baselineEntry.Key;
            UnityEngine.Object assetObject = AssetDatabase.LoadMainAssetAtPath(assetPath);

            if (assetObject == null)
                continue;

            EditorJsonUtility.FromJsonOverwrite(baselineEntry.Value, assetObject);
            EditorUtility.SetDirty(assetObject);
        }
    }

    private static void DeleteAssetsCreatedAfterBaseline()
    {
        List<string> currentPaths = CollectTrackedAssetPaths();

        for (int pathIndex = 0; pathIndex < currentPaths.Count; pathIndex++)
        {
            string currentPath = currentPaths[pathIndex];

            if (!currentPath.StartsWith(TrackedEnemyAssetsRoot, StringComparison.Ordinal))
                continue;

            if (baselineJsonByPath.ContainsKey(currentPath))
                continue;

            AssetDatabase.DeleteAsset(currentPath);
        }
    }

    private static void ExecuteStagedDeletions()
    {
        if (stagedDeletePaths.Count == 0)
            return;

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

    private static void ExecuteRenames()
    {
        List<string> assetPaths = CollectTrackedAssetPaths();

        for (int pathIndex = 0; pathIndex < assetPaths.Count; pathIndex++)
        {
            string assetPath = assetPaths[pathIndex];

            if (string.IsNullOrWhiteSpace(assetPath))
                continue;

            if (stagedDeletePaths.Contains(assetPath))
                continue;

            UnityEngine.Object assetObject = AssetDatabase.LoadMainAssetAtPath(assetPath);

            if (!IsRenamablePresetAsset(assetObject))
                continue;

            string currentFileName = Path.GetFileNameWithoutExtension(assetPath);
            string targetFileName = ResolveRequestedPresetFileName(assetObject);

            if (string.IsNullOrWhiteSpace(targetFileName))
                continue;

            if (string.Equals(currentFileName, targetFileName, StringComparison.Ordinal))
            {
                SyncPresetAssetNameToFileName(assetObject, currentFileName);
                continue;
            }

            string directoryPath = Path.GetDirectoryName(assetPath);

            if (string.IsNullOrWhiteSpace(directoryPath))
                continue;

            string normalizedDirectoryPath = directoryPath.Replace('\\', '/');
            string extension = Path.GetExtension(assetPath);
            string requestedPath = Path.Combine(normalizedDirectoryPath, targetFileName + extension).Replace('\\', '/');
            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);
            string renameError = AssetDatabase.MoveAsset(assetPath, uniquePath);

            if (!string.IsNullOrWhiteSpace(renameError))
            {
                Debug.LogWarning(string.Format("EnemyManagementDraftSession: failed to rename asset '{0}' to '{1}'. Error: {2}",
                                               assetPath,
                                               targetFileName,
                                               renameError));
                continue;
            }

            UnityEngine.Object movedAssetObject = AssetDatabase.LoadMainAssetAtPath(uniquePath);

            if (movedAssetObject == null)
                continue;

            string movedFileName = Path.GetFileNameWithoutExtension(uniquePath);
            SyncPresetAssetNameToFileName(movedAssetObject, movedFileName);
        }
    }

    private static bool IsRenamablePresetAsset(UnityEngine.Object assetObject)
    {
        if (assetObject == null)
            return false;

        if (assetObject is EnemyMasterPreset)
            return true;

        if (assetObject is EnemyBrainPreset)
            return true;

        if (assetObject is EnemyVisualPreset)
            return true;

        if (assetObject is EnemyUiVisualPreset)
            return true;

        if (assetObject is EnemyAdvancedPatternPreset)
            return true;

        if (assetObject is EnemyModulesAndPatternsPreset)
            return true;

        if (assetObject is EnemyBossPatternPreset)
            return true;

        return false;
    }

    private static string ResolveRequestedPresetFileName(UnityEngine.Object assetObject)
    {
        if (assetObject == null)
            return string.Empty;

        SerializedObject serializedObject = new SerializedObject(assetObject);
        SerializedProperty presetNameProperty = serializedObject.FindProperty("presetName");

        if (presetNameProperty == null)
            presetNameProperty = serializedObject.FindProperty("m_PresetName");

        if (presetNameProperty != null)
        {
            serializedObject.Update();
            string requestedPresetName = NormalizeAssetName(presetNameProperty.stringValue);

            if (!string.IsNullOrWhiteSpace(requestedPresetName))
                return requestedPresetName;
        }

        return NormalizeAssetName(assetObject.name);
    }

    private static void SyncPresetAssetNameToFileName(UnityEngine.Object assetObject, string fileName)
    {
        if (assetObject == null)
            return;

        if (string.IsNullOrWhiteSpace(fileName))
            return;

        assetObject.name = fileName;
        SerializedObject serializedObject = new SerializedObject(assetObject);
        SerializedProperty presetNameProperty = serializedObject.FindProperty("presetName");

        if (presetNameProperty == null)
            presetNameProperty = serializedObject.FindProperty("m_PresetName");

        if (presetNameProperty != null)
        {
            serializedObject.Update();
            presetNameProperty.stringValue = fileName;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(assetObject);
    }

    private static void SyncStagedDeletePaths()
    {
        if (stagedDeletePaths.Count == 0)
            return;

        List<string> stagedPaths = new List<string>(stagedDeletePaths);

        for (int pathIndex = 0; pathIndex < stagedPaths.Count; pathIndex++)
        {
            string stagedPath = stagedPaths[pathIndex];

            if (!IsPathReferencedByLibraries(stagedPath))
                continue;

            stagedDeletePaths.Remove(stagedPath);
        }
    }

    private static bool IsPathReferencedByLibraries(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        EnemyMasterPresetLibrary masterLibrary = EnemyMasterPresetLibraryUtility.GetOrCreateLibrary();

        if (LibraryContainsPath(masterLibrary.Presets, assetPath))
            return true;

        EnemyBrainPresetLibrary brainLibrary = EnemyBrainPresetLibraryUtility.GetOrCreateLibrary();

        if (LibraryContainsPath(brainLibrary.Presets, assetPath))
            return true;

        EnemyVisualPresetLibrary visualLibrary = EnemyVisualPresetLibraryUtility.GetOrCreateLibrary();

        if (LibraryContainsPath(visualLibrary.Presets, assetPath))
            return true;

        EnemyUiVisualPresetLibrary uiVisualLibrary = EnemyUiVisualPresetLibraryUtility.GetOrCreateLibrary();

        if (LibraryContainsPath(uiVisualLibrary.Presets, assetPath))
            return true;

        EnemyAdvancedPatternPresetLibrary advancedPatternLibrary = EnemyAdvancedPatternPresetLibraryUtility.GetOrCreateLibrary();

        if (LibraryContainsPath(advancedPatternLibrary.Presets, assetPath))
            return true;

        EnemyBossPatternPresetLibrary bossPatternLibrary = EnemyBossPatternPresetLibraryUtility.GetOrCreateLibrary();
        return LibraryContainsPath(bossPatternLibrary.Presets, assetPath);
    }

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
