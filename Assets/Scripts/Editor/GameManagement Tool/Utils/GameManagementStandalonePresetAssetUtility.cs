using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides shared asset discovery and draft-aware lifecycle operations for standalone Game Management presets.
/// </summary>
internal static class GameManagementStandalonePresetAssetUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Finds live assets of one preset type below the Game Management asset root.
    /// </summary>
    /// <typeparam name="TAsset">Scriptable preset type requested by the panel.</typeparam>
    /// <returns>Assets sorted by project path with staged deletions omitted.</returns>
    public static List<TAsset> FindAssets<TAsset>() where TAsset : ScriptableObject
    {
        List<TAsset> assets = new List<TAsset>();
        string[] assetGuids = AssetDatabase.FindAssets("t:" + typeof(TAsset).Name,
                                                        new string[] { "Assets/Scriptable Objects/Game" });

        // Resolve GUIDs through the AssetDatabase so renamed draft assets remain valid.
        for (int guidIndex = 0; guidIndex < assetGuids.Length; guidIndex++)
        {
            TAsset asset = AssetDatabase.LoadAssetAtPath<TAsset>(AssetDatabase.GUIDToAssetPath(assetGuids[guidIndex]));

            if (asset == null || GameManagementDraftSession.IsAssetStagedForDeletion(asset))
                continue;

            assets.Add(asset);
        }

        assets.Sort((left, right) => string.Compare(AssetDatabase.GetAssetPath(left),
                                                    AssetDatabase.GetAssetPath(right),
                                                    StringComparison.Ordinal));
        return assets;
    }

    /// <summary>
    /// Creates one initialized standalone preset inside a dedicated Game Management folder.
    /// </summary>
    /// <typeparam name="TAsset">Scriptable preset type to create.</typeparam>
    /// <param name="folderPath">Project-relative destination folder.</param>
    /// <param name="baseFileName">Default readable asset filename.</param>
    /// <param name="initialize">Optional callback that restores identity and nested collections.</param>
    /// <returns>Created preset asset, or null when creation fails.</returns>
    public static TAsset CreateAsset<TAsset>(string folderPath,
                                             string baseFileName,
                                             Action<TAsset> initialize) where TAsset : ScriptableObject
    {
        GameManagementAssetUtility.EnsureFolder(folderPath);
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(folderPath + "/" + baseFileName + ".asset");
        TAsset asset = ScriptableObject.CreateInstance<TAsset>();

        if (initialize != null)
            initialize(asset);

        AssetDatabase.CreateAsset(asset, assetPath);
        Undo.RegisterCreatedObjectUndo(asset, "Create " + typeof(TAsset).Name);
        EditorUtility.SetDirty(asset);
        GameManagementDraftSession.MarkDirty();
        return asset;
    }

    /// <summary>
    /// Duplicates one standalone preset beside the source asset and retains serialized tuning.
    /// </summary>
    /// <typeparam name="TAsset">Scriptable preset type being duplicated.</typeparam>
    /// <param name="source">Source preset copied through editor serialization.</param>
    /// <param name="initialize">Optional callback used to ensure required nested storage.</param>
    /// <returns>Created duplicate, or null when the source is missing.</returns>
    public static TAsset DuplicateAsset<TAsset>(TAsset source,
                                                Action<TAsset> initialize) where TAsset : ScriptableObject
    {
        if (source == null)
            return null;

        string sourcePath = AssetDatabase.GetAssetPath(source);
        string duplicatePath = AssetDatabase.GenerateUniqueAssetPath(
            System.IO.Path.GetDirectoryName(sourcePath) + "/" + source.name + " Copy.asset");
        TAsset duplicate = ScriptableObject.CreateInstance<TAsset>();
        EditorUtility.CopySerialized(source, duplicate);

        if (initialize != null)
            initialize(duplicate);

        AssetDatabase.CreateAsset(duplicate, duplicatePath);
        Undo.RegisterCreatedObjectUndo(duplicate, "Duplicate " + typeof(TAsset).Name);
        EditorUtility.SetDirty(duplicate);
        GameManagementDraftSession.MarkDirty();
        return duplicate;
    }

    /// <summary>
    /// Stages one preset for deletion so Apply and Discard retain the normal Game Management behavior.
    /// </summary>
    /// <param name="asset">Preset asset requested for deletion.</param>
    public static void StageDelete(UnityEngine.Object asset)
    {
        if (asset == null)
            return;

        GameManagementDraftSession.StageDeleteAsset(asset);
        GameManagementDraftSession.MarkDirty();
    }
    #endregion

    #endregion
}
