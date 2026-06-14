using System.IO;
using UnityEditor;

/// <summary>
/// Centralizes the canonical asset path used by EnemyWavePreset authoring tools.
/// None.
/// </summary>
public static class EnemyWavePresetAssetUtility
{
    #region Constants
    public const string DefaultPresetsFolder = "Assets/Scriptable Objects/Enemy/Wave Preset";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves a unique asset path inside the canonical EnemyWavePreset folder.
    /// </summary>
    /// <param name="presetName">Desired asset name before uniqueness suffixes are applied.</param>
    /// <returns>Unique asset path inside the canonical EnemyWavePreset folder.</returns>
    public static string CreateUniquePresetAssetPath(string presetName)
    {
        EnsureFolder(DefaultPresetsFolder);

        string normalizedName = string.IsNullOrWhiteSpace(presetName)
            ? "EnemyWavePreset"
            : EnemyManagementDraftSession.NormalizeAssetName(presetName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "EnemyWavePreset";

        string assetPath = DefaultPresetsFolder + "/" + normalizedName + ".asset";
        return AssetDatabase.GenerateUniqueAssetPath(assetPath);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates the requested folder path recursively when any segment is still missing.
    /// </summary>
    /// <param name="folderPath">Unity project-relative folder path that must exist.</param>
    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parentFolder = Path.GetDirectoryName(folderPath);
        string folderName = Path.GetFileName(folderPath);

        if (!string.IsNullOrWhiteSpace(parentFolder) && !AssetDatabase.IsValidFolder(parentFolder))
            EnsureFolder(parentFolder);

        AssetDatabase.CreateFolder(parentFolder, folderName);
    }
    #endregion

    #endregion
}
