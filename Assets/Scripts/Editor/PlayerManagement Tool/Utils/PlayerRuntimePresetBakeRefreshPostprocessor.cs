using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the baked player prefab and player SubScene synchronized when player presets or bake scripts change.
/// </summary>
public sealed class PlayerRuntimePresetBakeRefreshPostprocessor : AssetPostprocessor
{
    #region Constants
    private const string PlayerPrefabPath = "Assets/Prefabs/Player/PF_Player.prefab";
    private const string PlayerSubScenePath = "Assets/Scenes/Testing/Main Scenes/SCN_MainScene/SUB_Player.unity";
    private const string PlayerPresetDirectory = "Assets/Scriptable Objects/Player/";
    private const string PlayerAuthoringDirectory = "Assets/Scripts/Player/Authoring/";
    private const string PlayerComponentDirectory = "Assets/Scripts/Player/Components/";
    private const string PlayerSystemDirectory = "Assets/Scripts/Player/Systems/";
    private const string PlayerScriptablePresetDirectory = "Assets/Scripts/Player/Scriptable Presets/";
    private const string PlayerHudRuntimeDirectory = "Assets/Scripts/Player/UI/HUD/";
    private const string PlayerManagementToolDirectory = "Assets/Scripts/Editor/PlayerManagement Tool/";
    private const string StartupRefreshSessionKey = "PlayerRuntimePresetBakeRefreshPostprocessor.StartupRefreshQueued";
    private const int MaxRuntimeReimportRetries = 4;
    #endregion

    #region Fields
    private static bool refreshQueued;
    private static bool isRefreshing;
    private static int runtimeReimportRetryCount;
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Queues one session-scoped refresh after domain reload so newly added bake hooks are materialized without manual reimport.
    /// </summary>
    [InitializeOnLoadMethod]
    private static void QueueStartupRuntimeRefresh()
    {
        if (SessionState.GetBool(StartupRefreshSessionKey, false))
            return;

        SessionState.SetBool(StartupRefreshSessionKey, true);
        QueueRuntimeRefresh();
    }

    /// <summary>
    /// Schedules a focused player prefab and SubScene reimport after relevant player assets change.
    /// </summary>
    /// <param name="importedAssets">Assets imported by the editor refresh.</param>
    /// <param name="deletedAssets">Assets deleted by the editor refresh.</param>
    /// <param name="movedAssets">New asset paths after move operations.</param>
    /// <param name="movedFromAssetPaths">Previous asset paths before move operations.</param>
    private static void OnPostprocessAllAssets(string[] importedAssets,
                                               string[] deletedAssets,
                                               string[] movedAssets,
                                               string[] movedFromAssetPaths)
    {
        if (isRefreshing || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        bool hasRelevantPlayerChange = ContainsRelevantPlayerChange(importedAssets) ||
                                       ContainsRelevantPlayerChange(deletedAssets) ||
                                       ContainsRelevantPlayerChange(movedAssets) ||
                                       ContainsRelevantPlayerChange(movedFromAssetPaths);

        if (!hasRelevantPlayerChange)
            return;

        runtimeReimportRetryCount = 0;
        QueueRuntimeRefresh();
    }
    #endregion

    #region Refresh
    /// <summary>
    /// Queues one delayed refresh so bursts of imports collapse into a single player runtime bake update.
    /// </summary>
    private static void QueueRuntimeRefresh()
    {
        if (refreshQueued)
            return;

        refreshQueued = true;
        EditorApplication.delayCall += RefreshPlayerRuntimeAssets;
    }

    /// <summary>
    /// Reimports the player prefab and player SubScene outside the asset import callback.
    /// </summary>
    private static void RefreshPlayerRuntimeAssets()
    {
        EditorApplication.delayCall -= RefreshPlayerRuntimeAssets;
        refreshQueued = false;

        if (isRefreshing || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        isRefreshing = true;

        try
        {
            TryReimportPlayerRuntimeAssets();
        }
        finally
        {
            isRefreshing = false;
        }
    }

    /// <summary>
    /// Reimports player runtime assets and converts transient Entities cache locks into a bounded delayed retry.
    /// </summary>
    private static void TryReimportPlayerRuntimeAssets()
    {
        try
        {
            ImportIfPresent(PlayerPrefabPath);
            ImportIfPresent(PlayerSubScenePath);
            runtimeReimportRetryCount = 0;
        }
        catch (IOException exception)
        {
            if (!IsTransientImportLock(exception))
                throw;

            QueueRuntimeReimportRetry(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            QueueRuntimeReimportRetry(exception);
        }
    }

    /// <summary>
    /// Requeues player runtime reimport after Unity temporarily locks DOTS scene dependency cache files.
    /// </summary>
    /// <param name="exception">Transient file-system exception thrown by the Unity asset or Entities importer.</param>
    private static void QueueRuntimeReimportRetry(Exception exception)
    {
        if (runtimeReimportRetryCount >= MaxRuntimeReimportRetries)
        {
            Debug.LogWarning(string.Format("[PlayerRuntimePresetBakeRefresh] Player prefab/SubScene reimport skipped after {0} retries because Unity kept the Entities scene dependency cache locked. Last error: {1}",
                                           MaxRuntimeReimportRetries,
                                           exception.Message));
            return;
        }

        runtimeReimportRetryCount++;
        QueueRuntimeRefresh();
    }
    #endregion

    #region Filtering
    /// <summary>
    /// Checks whether an asset batch contains player preset, authoring, runtime HUD or bake-script changes.
    /// </summary>
    /// <param name="assetPaths">Asset paths from one postprocessor batch category.</param>
    /// <returns>True when at least one path requires a player runtime refresh.</returns>
    private static bool ContainsRelevantPlayerChange(string[] assetPaths)
    {
        if (assetPaths == null)
            return false;

        for (int assetIndex = 0; assetIndex < assetPaths.Length; assetIndex++)
        {
            if (IsRelevantPlayerAsset(assetPaths[assetIndex]))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks one asset path for player runtime bake relevance.
    /// </summary>
    /// <param name="assetPath">Unity project-relative asset path.</param>
    /// <returns>True when the asset can affect player baked HUD visual data.</returns>
    private static bool IsRelevantPlayerAsset(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        string normalizedPath = assetPath.Replace('\\', '/');

        if (string.Equals(normalizedPath, PlayerPrefabPath, StringComparison.Ordinal))
            return true;

        if (string.Equals(normalizedPath, PlayerSubScenePath, StringComparison.Ordinal))
            return true;

        if (IsPlayerPresetAsset(normalizedPath))
            return true;

        if (IsPlayerRuntimeScriptAsset(normalizedPath))
            return true;

        return false;
    }

    /// <summary>
    /// Checks whether an asset path points to a player preset asset that can feed PlayerAuthoringBaker.
    /// </summary>
    /// <param name="assetPath">Unity project-relative asset path.</param>
    /// <returns>True when the path is a player preset asset.</returns>
    private static bool IsPlayerPresetAsset(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        return assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) &&
               assetPath.StartsWith(PlayerPresetDirectory, StringComparison.Ordinal);
    }

    /// <summary>
    /// Checks whether a script change can alter player bake output or HUD runtime interpretation.
    /// </summary>
    /// <param name="assetPath">Unity project-relative asset path.</param>
    /// <returns>True when the script should trigger player prefab and SubScene reimport.</returns>
    private static bool IsPlayerRuntimeScriptAsset(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        if (!assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            return false;

        if (assetPath.StartsWith(PlayerAuthoringDirectory, StringComparison.Ordinal))
            return true;

        if (assetPath.StartsWith(PlayerComponentDirectory, StringComparison.Ordinal))
            return assetPath.IndexOf("Hud", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   assetPath.IndexOf("HUD", StringComparison.Ordinal) >= 0 ||
                   assetPath.IndexOf("Scaling", StringComparison.Ordinal) >= 0;

        if (assetPath.StartsWith(PlayerSystemDirectory, StringComparison.Ordinal))
            return assetPath.IndexOf("Hud", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   assetPath.IndexOf("Scaling", StringComparison.Ordinal) >= 0;

        if (assetPath.StartsWith(PlayerScriptablePresetDirectory, StringComparison.Ordinal))
            return assetPath.IndexOf("/Visual/HUD/", StringComparison.Ordinal) >= 0 ||
                   assetPath.IndexOf("/Progression/", StringComparison.Ordinal) >= 0 ||
                   assetPath.IndexOf("/Master/", StringComparison.Ordinal) >= 0;

        if (assetPath.StartsWith(PlayerHudRuntimeDirectory, StringComparison.Ordinal))
            return true;

        if (assetPath.StartsWith(PlayerManagementToolDirectory, StringComparison.Ordinal))
            return assetPath.IndexOf("PlayerRuntimePresetBakeRefresh", StringComparison.Ordinal) >= 0 ||
                   assetPath.IndexOf("/Graphic/Visual/", StringComparison.Ordinal) >= 0;

        return false;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Imports one asset only when it still exists in the project database.
    /// </summary>
    /// <param name="assetPath">Unity project-relative asset path.</param>
    private static void ImportIfPresent(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(assetPath)))
            return;

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
    }

    /// <summary>
    /// Checks whether an IO failure matches the temporary lock errors produced by Unity scene import artifacts.
    /// </summary>
    /// <param name="exception">IO exception thrown by the asset or Entities importer.</param>
    /// <returns>True when retrying on the next editor delay is safer than surfacing the current attempt.</returns>
    private static bool IsTransientImportLock(IOException exception)
    {
        if (exception == null || string.IsNullOrWhiteSpace(exception.Message))
            return false;

        return exception.Message.IndexOf("Sharing violation", StringComparison.OrdinalIgnoreCase) >= 0 ||
               exception.Message.IndexOf("being used by another process", StringComparison.OrdinalIgnoreCase) >= 0;
    }
    #endregion

    #endregion
}
