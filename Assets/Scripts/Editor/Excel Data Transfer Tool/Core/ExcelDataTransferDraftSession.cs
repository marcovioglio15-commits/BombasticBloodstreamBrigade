using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tracks draft edits for the Excel Data Transfer Tool and restores the baseline when changes are discarded.
/// </summary>
public static class ExcelDataTransferDraftSession
{
    #region Fields
    private static readonly Dictionary<string, string> baselineJsonByPath = new Dictionary<string, string>();
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
    /// <summary>
    /// Creates missing default tool presets and captures the current clean baseline.
    /// </summary>
    public static void BeginSession()
    {
        pendingChangesVerifier.Reset();
        ExcelDataTransferAssetUtility.GetOrCreateDefaultMasterPreset();
        CaptureBaseline();
        isInitialized = true;
        hasPendingChanges = false;
    }

    /// <summary>
    /// Clears draft session state when the editor window closes without pending changes.
    /// </summary>
    public static void EndSession()
    {
        pendingChangesVerifier.Reset();
        isInitialized = false;
        hasPendingChanges = false;
        baselineJsonByPath.Clear();
    }

    /// <summary>
    /// Performs one Unity Undo step and refreshes the pending-change flag.
    /// </summary>
    public static void PerformUndo()
    {
        Undo.PerformUndo();
        RecomputePendingChanges();
    }

    /// <summary>
    /// Performs one Unity Redo step and refreshes the pending-change flag.
    /// </summary>
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

    /// <summary>
    /// Rebuilds the current asset snapshot and compares it with the captured baseline.
    /// </summary>
    public static void RecomputePendingChanges()
    {
        if (!isInitialized)
        {
            hasPendingChanges = false;
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
    /// Saves all accepted editor-only transfer presets and captures a new clean baseline.
    /// </summary>
    public static void Apply()
    {
        pendingChangesVerifier.Reset();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        CaptureBaseline();
        hasPendingChanges = false;
    }

    /// <summary>
    /// Restores baseline JSON and deletes tool assets created after the captured baseline.
    /// </summary>
    public static void Discard()
    {
        if (!isInitialized)
            return;

        pendingChangesVerifier.Reset();
        RestoreBaselineAssets();
        DeleteAssetsCreatedAfterBaseline();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        CaptureBaseline();
        hasPendingChanges = false;
    }
    #endregion

    #region Session Helpers
    /// <summary>
    /// Captures every tracked transfer preset as JSON for future discard comparisons.
    /// </summary>
    private static void CaptureBaseline()
    {
        baselineJsonByPath.Clear();
        Dictionary<string, string> currentState = BuildStateDictionary();

        foreach (KeyValuePair<string, string> stateEntry in currentState)
            baselineJsonByPath[stateEntry.Key] = stateEntry.Value;
    }

    /// <summary>
    /// Builds a path-to-json dictionary for all tracked editor-only transfer presets.
    /// </summary>
    /// <returns>Current serialized state dictionary.</returns>
    private static Dictionary<string, string> BuildStateDictionary()
    {
        Dictionary<string, string> stateByPath = new Dictionary<string, string>();
        List<string> assetPaths = ExcelDataTransferAssetUtility.CollectTrackedAssetPaths();

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
    /// Restores tracked assets that existed when the draft baseline was captured.
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
    /// Deletes editor-only transfer assets created after the current baseline was captured.
    /// </summary>
    private static void DeleteAssetsCreatedAfterBaseline()
    {
        List<string> currentPaths = ExcelDataTransferAssetUtility.CollectTrackedAssetPaths();

        for (int pathIndex = 0; pathIndex < currentPaths.Count; pathIndex++)
        {
            string assetPath = currentPaths[pathIndex];

            if (baselineJsonByPath.ContainsKey(assetPath))
                continue;

            AssetDatabase.DeleteAsset(assetPath);
        }
    }
    #endregion

    #endregion
}
