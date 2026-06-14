using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Entry point used to refresh the authored player gameplay integration from the Editor or Unity batch mode.
/// None.
/// </summary>
public static class PlayerGameplayIntegrationSetupUtility
{
    #region Menu
    /// <summary>
    /// Runs the authored gameplay integration setup from the Unity Editor menu.
    /// None.
    /// </summary>
    //[MenuItem("Tools/Player/Setup Gameplay Integration")]
    public static void SetupFromMenu()
    {
        ExecuteSetup(exitOnCompletion : false);
    }

    /// <summary>
    /// Runs the authored gameplay integration setup from Unity batch mode.
    /// None.
    /// </summary>
    public static void ExecuteBatchSetup()
    {
        ExecuteSetup(exitOnCompletion : true);
    }
    #endregion

    #region Methods
    /// <summary>
    /// Runs the full authored setup pipeline and optionally exits the Unity process for batch workflows.
    /// </summary>
    /// <param name="exitOnCompletion">True when the method should terminate Unity with a success or failure exit code.</param>
    private static void ExecuteSetup(bool exitOnCompletion)
    {
        try
        {
            PlayerInputActionsAssetUtility.LoadOrCreateAsset();
            PlayerGameplayVisualSetupUtility.ExecuteSetup();
            PlayerGameplayMenuSetupUtility.ExecuteSetup();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PlayerGameplayIntegrationSetupUtility] Gameplay integration setup completed.");

            if (exitOnCompletion)
                EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError(string.Format("[PlayerGameplayIntegrationSetupUtility] Setup failed: {0}", exception));

            if (exitOnCompletion)
            {
                EditorApplication.Exit(1);
                return;
            }

            throw;
        }
    }
    #endregion
}
