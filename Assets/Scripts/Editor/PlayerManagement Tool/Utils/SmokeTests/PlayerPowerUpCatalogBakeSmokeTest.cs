using System;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Forces the player SubScene import and verifies that long power-up descriptions fit the runtime unlock catalog.
/// </summary>
public static class PlayerPowerUpCatalogBakeSmokeTest
{
    #region Constants
    private const string PlayerSubScenePath = "Assets/Scenes/Testing/Main Scenes/SCN_MainScene/SUB_Player.unity";
    private const string ReportedReturningProjectileDescription = "When projectiles reach the end of their Range, they travel back towards their trajectory to disappear when reaching them (or if another destruction condition takes place)";
    #endregion

    #region Methods

    #region Entry Point
    // [MenuItem("Tools/Player/Run Power-Up Catalog Bake Smoke Test")]
    /// <summary>
    /// Reimports the player SubScene synchronously so PlayerAuthoringBaker rebuilds its complete ECS catalog.
    /// </summary>
    public static void Run()
    {
        FixedString4096Bytes description = default;
        CopyError copyError = description.CopyFromTruncated(ReportedReturningProjectileDescription);

        if (copyError != CopyError.None || description.ToString() != ReportedReturningProjectileDescription)
            throw new InvalidOperationException("The reported Returning Projectiles description does not survive runtime catalog storage.");

        AssetDatabase.ImportAsset(PlayerSubScenePath,
                                  ImportAssetOptions.ForceSynchronousImport |
                                  ImportAssetOptions.ForceUpdate);
        Debug.Log("[PlayerPowerUpCatalogBakeSmokeTest] Player SubScene and long catalog descriptions baked successfully.");
    }
    #endregion

    #endregion
}
