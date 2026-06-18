using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides utility methods for creating and loading player visual preset assets and their library.
/// </summary>
public static class PlayerVisualPresetLibraryUtility
{
    #region Constants
    public const string DefaultLibraryPath = "Assets/Scriptable Objects/Player/PlayerVisualPresetLibrary.asset";
    public const string DefaultPresetsFolder = "Assets/Scriptable Objects/Player/Visual";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads the shared player visual preset library or creates it when missing.
    /// None.
    /// </summary>
    /// <returns>Resolved PlayerVisualPresetLibrary asset.</returns>
    public static PlayerVisualPresetLibrary GetOrCreateLibrary()
    {
        PlayerVisualPresetLibrary library = AssetDatabase.LoadAssetAtPath<PlayerVisualPresetLibrary>(DefaultLibraryPath);

        if (library != null)
            return library;

        EnsureFolder(Path.GetDirectoryName(DefaultLibraryPath));
        PlayerVisualPresetLibrary createdLibrary = ScriptableObject.CreateInstance<PlayerVisualPresetLibrary>();
        AssetDatabase.CreateAsset(createdLibrary, DefaultLibraryPath);
        return createdLibrary;
    }

    /// <summary>
    /// Creates one new player visual preset asset inside the default preset folder.
    /// </summary>
    /// <param name="presetName">Requested asset name before normalization.</param>
    /// <returns>Newly created PlayerVisualPreset asset.</returns>
    public static PlayerVisualPreset CreatePresetAsset(string presetName)
    {
        EnsureFolder(DefaultPresetsFolder);

        string normalizedName = PlayerManagementDraftSession.NormalizeAssetName(presetName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "PlayerVisualPreset";

        PlayerVisualPreset preset = ScriptableObject.CreateInstance<PlayerVisualPreset>();
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(DefaultPresetsFolder, normalizedName + ".asset"));
        AssetDatabase.CreateAsset(preset, assetPath);
        string finalName = Path.GetFileNameWithoutExtension(assetPath);
        preset.name = finalName;

        SerializedObject serializedPreset = new SerializedObject(preset);
        SerializedProperty nameProperty = serializedPreset.FindProperty("presetName");
        SerializedProperty laserBeamProperty = serializedPreset.FindProperty("laserBeam");

        if (nameProperty != null)
            nameProperty.stringValue = finalName;

        if (laserBeamProperty != null)
        {
            SerializedProperty bodyMaterialProperty = laserBeamProperty.FindPropertyRelative("bodyMaterial");
            SerializedProperty sourceEffectMaterialProperty = laserBeamProperty.FindPropertyRelative("sourceEffectMaterial");
            SerializedProperty terminalCapMaterialProperty = laserBeamProperty.FindPropertyRelative("terminalCapMaterial");
            Material laserBeamMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerLaserBeamVisualDefaultsUtility.DefaultBodyMaterialPath);
            Material sourceEffectMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerLaserBeamVisualDefaultsUtility.DefaultSourceEffectMaterialPath);
            Material terminalCapMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerLaserBeamVisualDefaultsUtility.DefaultTerminalCapMaterialPath);

            if (bodyMaterialProperty != null && laserBeamMaterial != null)
                bodyMaterialProperty.objectReferenceValue = laserBeamMaterial;

            if (sourceEffectMaterialProperty != null && sourceEffectMaterial != null)
                sourceEffectMaterialProperty.objectReferenceValue = sourceEffectMaterial;

            if (terminalCapMaterialProperty != null && terminalCapMaterial != null)
                terminalCapMaterialProperty.objectReferenceValue = terminalCapMaterial;
        }

        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        preset.LaserBeam.Validate();
        EditorUtility.SetDirty(preset);
        return preset;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Ensures that the target folder hierarchy exists inside the project.
    /// </summary>
    /// <param name="folderPath">Folder path to create when missing.</param>
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
