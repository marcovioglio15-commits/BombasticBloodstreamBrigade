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

    #region Events
    /// <summary>
    /// Notifies the Excel Data Transfer window only when the pending-change state actually changes.
    /// </summary>
    public static event Action PendingChangesChanged;
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
        SetPendingChanges(false);
    }

    /// <summary>
    /// Clears draft session state when the editor window closes without pending changes.
    /// </summary>
    public static void EndSession()
    {
        pendingChangesVerifier.Reset();
        isInitialized = false;
        SetPendingChanges(false);
        baselineJsonByPath.Clear();
    }

    /// <summary>
    /// Performs one Unity Undo step and refreshes the pending-change flag.
    /// </summary>
    public static void PerformUndo()
    {
        Undo.PerformUndo();
    }

    /// <summary>
    /// Performs one Unity Redo step and refreshes the pending-change flag.
    /// </summary>
    public static void PerformRedo()
    {
        Undo.PerformRedo();
    }

    /// <summary>
    /// Verifies one tool-side dirty signal against the serialized baseline before changing pending state.
    /// </summary>
    public static void MarkDirty()
    {
        if (!isInitialized)
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
            SetPendingChanges(false);
            return;
        }

        Dictionary<string, string> currentState = BuildStateDictionary();

        if (currentState.Count != baselineJsonByPath.Count)
        {
            SetPendingChanges(true);
            return;
        }

        foreach (KeyValuePair<string, string> baselineEntry in baselineJsonByPath)
        {
            if (!currentState.TryGetValue(baselineEntry.Key, out string currentJson))
            {
                SetPendingChanges(true);
                return;
            }

            if (!string.Equals(baselineEntry.Value, currentJson, StringComparison.Ordinal))
            {
                SetPendingChanges(true);
                return;
            }
        }

        SetPendingChanges(false);
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
        SetPendingChanges(false);
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
        SetPendingChanges(false);
    }
    #endregion

    #region Session Helpers
    /// <summary>
    /// Updates pending state and emits one notification only when the visible state changes.
    /// </summary>
    /// <param name="value">New pending-change state.</param>
    private static void SetPendingChanges(bool value)
    {
        if (hasPendingChanges == value)
            return;

        hasPendingChanges = value;
        PendingChangesChanged?.Invoke();
    }

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

            stateByPath[assetPath] = EditorJsonUtility.ToJson(assetObject);
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
