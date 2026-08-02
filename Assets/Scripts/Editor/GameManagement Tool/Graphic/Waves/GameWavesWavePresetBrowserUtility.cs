using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Provides direct Enemy Wave asset selection for sequence editing, including presets not currently mapped to a room.
/// </summary>
internal static class GameWavesWavePresetBrowserUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the direct wave-preset selector and the ordered sequence editor beneath it.
    /// </summary>
    /// <param name="root">Wave Sequence tab receiving the controls.</param>
    /// <param name="wavesPresetObject">Serialized Game Waves preset used to resolve the selected room fallback.</param>
    /// <param name="selectedSceneIndex">Currently selected room mapping index.</param>
    /// <param name="requestedPreset">Previously selected independent wave asset.</param>
    /// <param name="selectionChanged">Callback receiving a newly selected asset.</param>
    /// <param name="rebuild">Callback rebuilding the sequence after mutations.</param>
    /// <returns>Wave preset currently displayed by the sequence editor.</returns>
    public static EnemyWavePreset Build(VisualElement root,
                                        SerializedObject wavesPresetObject,
                                        int selectedSceneIndex,
                                        EnemyWavePreset requestedPreset,
                                        Action<EnemyWavePreset> selectionChanged,
                                        Action rebuild)
    {
        EnemyWavePreset resolvedPreset = requestedPreset != null
            ? requestedPreset
            : ResolveMappedPreset(wavesPresetObject, selectedSceneIndex);
        ObjectField presetField = new ObjectField("Enemy Wave Preset")
        {
            objectType = typeof(EnemyWavePreset),
            allowSceneObjects = false,
            value = resolvedPreset
        };
        presetField.tooltip = "Enemy Wave asset whose ordered and parallel sequence is edited. Any project asset can be selected, including preserved scene setups.";
        presetField.style.flexShrink = 0f;
        presetField.RegisterValueChangedCallback(evt => selectionChanged(evt.newValue as EnemyWavePreset));
        root.Add(presetField);

        if (resolvedPreset == null)
        {
            root.Add(new HelpBox("Select an Enemy Wave preset to define its ordered sequence.",
                                 HelpBoxMessageType.Info));
            return null;
        }

        SerializedObject wavePresetObject = new SerializedObject(resolvedPreset);
        GameWavesSequenceEditorUtility.Build(root, wavePresetObject, rebuild);
        return resolvedPreset;
    }
    #endregion

    #region Lookup Methods
    /// <summary>
    /// Resolves the wave asset assigned to the selected room mapping as the initial browser choice.
    /// </summary>
    /// <param name="wavesPresetObject">Serialized Game Waves preset containing scene mappings.</param>
    /// <param name="selectedSceneIndex">Requested scene mapping index.</param>
    /// <returns>Mapped wave preset, or null when no valid mapping exists.</returns>
    private static EnemyWavePreset ResolveMappedPreset(SerializedObject wavesPresetObject,
                                                       int selectedSceneIndex)
    {
        SerializedProperty sceneMappings = wavesPresetObject.FindProperty("sceneMappings");

        if (sceneMappings == null || sceneMappings.arraySize == 0)
            return null;

        int mappingIndex = GameWavesPanelUiUtility.ClampIndex(selectedSceneIndex, sceneMappings.arraySize);
        return sceneMappings.GetArrayElementAtIndex(mappingIndex)
                            .FindPropertyRelative("wavePreset")
                            .objectReferenceValue as EnemyWavePreset;
    }
    #endregion

    #endregion
}
