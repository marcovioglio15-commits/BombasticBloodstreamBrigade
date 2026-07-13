using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Refreshes Player authoring dependencies after any Player-domain workbook cells are committed.
/// </summary>
internal static class ExcelDataPlayerScalingBakeRefreshUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Force-reimports changed Player assets so PlayerAuthoring baker dependencies observe new base data and formulas.
    /// </summary>
    /// <param name="affectedAssets">Player assets changed by the completed import transaction.</param>
    /// <returns>User-facing authoring and bake refresh status.</returns>
    public static string Refresh(IReadOnlyList<Object> affectedAssets)
    {
        if (affectedAssets == null || affectedAssets.Count <= 0)
            return "Authoring Updated.";

        int importedAssetCount = 0;

        // Reimport each changed project asset once; PlayerAuthoring.DependsOn handles dependent ECS rebakes.
        for (int assetIndex = 0; assetIndex < affectedAssets.Count; assetIndex++)
        {
            Object asset = affectedAssets[assetIndex];
            string assetPath = asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset);

            if (string.IsNullOrWhiteSpace(assetPath))
                continue;

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            importedAssetCount++;
        }

        if (importedAssetCount <= 0)
            return "Authoring Updated - Bake Required. Changed Player presets have no importable project asset path.";

        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
        return "Authoring Updated - Player Bake Queued for " + importedAssetCount +
               (importedAssetCount == 1 ? " preset." : " presets.");
    }
    #endregion

    #endregion
}
