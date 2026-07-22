using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates and links the default Procedural Level preset without replacing designer-authored levels or tuning.
/// </summary>
internal static class GameProceduralLevelProjectSetupUtility
{
    #region Constants
    public const string DefaultPresetPath = "Assets/Scriptable Objects/Game/Procedural Level Generation/GameProceduralLevelPreset.asset";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads or creates the default Procedural Level preset, registers it and assigns a missing scene catalog reference.
    /// </summary>
    /// <param name="sceneCatalogPreset">Canonical Scene Manager preset used by room scene selectors and validation.</param>
    /// <returns>Default registered Procedural Level preset asset.</returns>
    public static GameProceduralLevelPreset EnsureDefaultPreset(GameSceneManagerPreset sceneCatalogPreset)
    {
        GameProceduralLevelPreset preset = AssetDatabase.LoadAssetAtPath<GameProceduralLevelPreset>(DefaultPresetPath);

        if (preset == null)
            preset = GameProceduralLevelPresetLibraryUtility.CreatePresetAsset("GameProceduralLevelPreset");

        if (preset == null)
            throw new InvalidOperationException("Unable to create the default GameProceduralLevelPreset asset.");

        return EnsurePreset(preset, sceneCatalogPreset);
    }

    /// <summary>
    /// Initializes and registers an authored Procedural Level preset while preserving its designer-authored content.
    /// </summary>
    /// <param name="preset">Authored preset already selected by the Game Master configuration.</param>
    /// <param name="sceneCatalogPreset">Canonical Scene Manager preset assigned only when the authored link is missing.</param>
    /// <returns>The initialized authored preset, or a newly created default when no preset was supplied.</returns>
    public static GameProceduralLevelPreset EnsurePreset(GameProceduralLevelPreset preset,
                                                         GameSceneManagerPreset sceneCatalogPreset)
    {
        if (preset == null)
            return EnsureDefaultPreset(sceneCatalogPreset);

        preset.EnsureInitialized();
        AssignMissingSceneCatalog(preset, sceneCatalogPreset);
        GameProceduralRoomTransactionalSetupUtility.EnsureExplicitSubSceneOwnership(preset);

        GameProceduralLevelPresetLibrary library = GameProceduralLevelPresetLibraryUtility.GetOrCreateLibrary();
        library.AddPreset(preset);
        EditorUtility.SetDirty(library);
        EditorUtility.SetDirty(preset);
        return preset;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Assigns the canonical scene catalog only when the procedural preset has no authored catalog reference.
    /// </summary>
    /// <param name="preset">Procedural Level preset being initialized.</param>
    /// <param name="sceneCatalogPreset">Canonical Scene Manager preset available to the default setup.</param>
    private static void AssignMissingSceneCatalog(GameProceduralLevelPreset preset, GameSceneManagerPreset sceneCatalogPreset)
    {
        if (preset == null || sceneCatalogPreset == null)
            return;

        SerializedObject serializedPreset = new SerializedObject(preset);
        SerializedProperty catalogProperty = serializedPreset.FindProperty("sceneCatalogPreset");

        if (catalogProperty == null || catalogProperty.objectReferenceValue != null)
            return;

        serializedPreset.Update();
        catalogProperty.objectReferenceValue = sceneCatalogPreset;
        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
    }
    #endregion

    #endregion
}
