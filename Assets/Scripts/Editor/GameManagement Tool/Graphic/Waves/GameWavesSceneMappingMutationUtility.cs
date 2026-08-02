using UnityEditor;
using UnityEngine;

/// <summary>
/// Centralizes serialized scene-mapping mutations used by the Waves panel.
/// </summary>
internal static class GameWavesSceneMappingMutationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds and initializes one empty room-scene mapping.
    /// </summary>
    /// <param name="serializedPreset">Serialized Waves preset receiving the mapping.</param>
    /// <param name="preset">Waves preset recorded by Undo.</param>
    /// <returns>Index of the newly inserted mapping.</returns>
    public static int Add(SerializedObject serializedPreset, GameWavesPreset preset)
    {
        SerializedProperty sceneMappings = serializedPreset.FindProperty("sceneMappings");
        Undo.RecordObject(preset, "Add Wave Scene Mapping");
        int insertedIndex = sceneMappings.arraySize;
        sceneMappings.InsertArrayElementAtIndex(insertedIndex);
        SerializedProperty mapping = sceneMappings.GetArrayElementAtIndex(insertedIndex);
        mapping.FindPropertyRelative("displayName").stringValue = "Room Waves";
        mapping.FindPropertyRelative("mainScenePath").stringValue = string.Empty;
        mapping.FindPropertyRelative("mainSceneGuid").stringValue = string.Empty;
        mapping.FindPropertyRelative("subScenePath").stringValue = string.Empty;
        mapping.FindPropertyRelative("subSceneGuid").stringValue = string.Empty;
        mapping.FindPropertyRelative("wavePreset").objectReferenceValue = null;
        mapping.FindPropertyRelative("mainSceneAsset").objectReferenceValue = null;
        serializedPreset.ApplyModifiedProperties();
        GameManagementDraftSession.MarkDirty();
        return insertedIndex;
    }

    /// <summary>
    /// Removes one selected scene mapping without deleting referenced assets.
    /// </summary>
    /// <param name="serializedPreset">Serialized Waves preset losing the mapping.</param>
    /// <param name="preset">Waves preset recorded by Undo.</param>
    /// <param name="selectedSceneIndex">Selected index clamped after removal.</param>
    /// <returns>True when a mapping was removed.</returns>
    public static bool Remove(SerializedObject serializedPreset,
                              GameWavesPreset preset,
                              ref int selectedSceneIndex)
    {
        SerializedProperty sceneMappings = serializedPreset.FindProperty("sceneMappings");

        if (sceneMappings.arraySize == 0)
            return false;

        Undo.RecordObject(preset, "Remove Wave Scene Mapping");
        sceneMappings.DeleteArrayElementAtIndex(
            GameWavesPanelUiUtility.ClampIndex(selectedSceneIndex, sceneMappings.arraySize));
        selectedSceneIndex = Mathf.Max(0, selectedSceneIndex - 1);
        serializedPreset.ApplyModifiedProperties();
        GameManagementDraftSession.MarkDirty();
        return true;
    }

    /// <summary>
    /// Resolves one managed room mapping and links its unique SubScene wave preset to the category source.
    /// </summary>
    /// <param name="serializedPreset">Serialized Waves preset containing mappings.</param>
    /// <param name="preset">Waves preset supplying brush categories.</param>
    /// <param name="selectedSceneIndex">Requested scene mapping index.</param>
    /// <returns>Synchronization warning, or an empty string when the mapping is valid.</returns>
    public static string SynchronizeScene(SerializedObject serializedPreset,
                                          GameWavesPreset preset,
                                          int selectedSceneIndex)
    {
        SerializedProperty sceneMappings = serializedPreset.FindProperty("sceneMappings");

        if (sceneMappings.arraySize == 0)
            return string.Empty;

        SerializedProperty mapping = sceneMappings.GetArrayElementAtIndex(
            GameWavesPanelUiUtility.ClampIndex(selectedSceneIndex, sceneMappings.arraySize));
        string warning = GameWaveSceneEditorUtility.SynchronizeMapping(mapping);
        serializedPreset.ApplyModifiedProperties();
        GameWaveSceneEditorUtility.LinkCategorySource(
            mapping.FindPropertyRelative("wavePreset").objectReferenceValue as EnemyWavePreset,
            preset);
        EditorUtility.SetDirty(preset);
        GameManagementDraftSession.MarkDirty();
        return warning;
    }

    /// <summary>
    /// Applies a manually selected Enemy Wave preset and links it to the active category source.
    /// </summary>
    /// <param name="serializedPreset">Serialized Waves preset containing mappings.</param>
    /// <param name="preset">Waves preset supplying brush categories.</param>
    /// <param name="selectedSceneIndex">Requested scene mapping index.</param>
    public static void SynchronizeWavePreset(SerializedObject serializedPreset,
                                             GameWavesPreset preset,
                                             int selectedSceneIndex)
    {
        serializedPreset.ApplyModifiedProperties();
        SerializedProperty sceneMappings = serializedPreset.FindProperty("sceneMappings");

        if (sceneMappings.arraySize == 0)
            return;

        SerializedProperty mapping = sceneMappings.GetArrayElementAtIndex(
            GameWavesPanelUiUtility.ClampIndex(selectedSceneIndex, sceneMappings.arraySize));
        GameWaveSceneEditorUtility.LinkCategorySource(
            mapping.FindPropertyRelative("wavePreset").objectReferenceValue as EnemyWavePreset,
            preset);
        GameManagementDraftSession.MarkDirty();
    }
    #endregion

    #endregion
}
