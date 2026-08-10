using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>
/// Synchronizes scene references and procedural room metadata before player builds, including batch-mode CI builds.
/// </summary>
public sealed class GameSceneReferenceMetadataBuildProcessor : IPreprocessBuildWithReport
{
    #region Properties

    int IOrderedCallback.callbackOrder
    {
        get
        {
            return -1000;
        }
    }

    #endregion

    #region Methods

    #region Build Methods

    /// <summary>
    /// Refreshes rename-sensitive scene metadata and blocks the build when a referenced room cannot be scanned safely.
    /// </summary>
    /// <param name="report">Unity build report describing the requested player target.</param>
    public void OnPreprocessBuild(BuildReport report)
    {
        GameRoomMetadataRefreshReport metadataReport =
            GameRoomMetadataAutomaticRefreshUtility.RefreshAllStaleReferencedRooms();

        if (!metadataReport.Succeeded)
        {
            throw new BuildFailedException(
                "Scene reference or procedural room metadata refresh failed before build: " +
                string.Join(" | ", metadataReport.Errors));
        }
    }

    #endregion

    #endregion
}
