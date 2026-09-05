using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds the independent music contexts with shared conditional controls in the Game Management Tool.
/// </summary>
internal static class GameAudioMusicPresetsDrawerUtility
{
    #region Methods

    #region Layout
    /// <summary>
    /// Displays crossfade tuning and the three editable FMOD music events.
    /// </summary>
    /// <param name="panel">Audio preset panel owning the current draft.</param>
    public static void Build(GameAudioManagerPresetsPanel panel)
    {
        // Every context uses the same serialized model and editor path.
        AddField(panel, panel.SectionContentRoot, panel.PresetSerializedObject.FindProperty("musicCrossfadeSeconds"), "Crossfade Seconds");
        BuildTrack(panel, "backgroundMusicSettings", "Background Music");
        BuildTrack(panel, "bossMusicSettings", "Boss Music");
        BuildTrack(panel, "mainMenuMusicSettings", "Main Menu Music");
    }

    /// <summary>
    /// Shows event and playback controls only when the associated music context is enabled.
    /// </summary>
    /// <param name="panel">Owning preset panel.</param>
    /// <param name="propertyName">Serialized track root.</param>
    /// <param name="title">Context label shown above the track controls.</param>
    private static void BuildTrack(GameAudioManagerPresetsPanel panel, string propertyName, string title)
    {
        SerializedProperty track = panel.PresetSerializedObject.FindProperty(propertyName);
        SerializedProperty enabled = track.FindPropertyRelative("enabled");
        SerializedProperty autoStart = track.FindPropertyRelative("autoStart");
        Foldout section = new Foldout { text = title, value = true, tooltip = track.tooltip };
        panel.SectionContentRoot.Add(section);
        AddField(panel, section, enabled, "Enabled");
        AddField(panel, section, track.FindPropertyRelative("stopWhenDisabled"), "Stop When Disabled");
        VisualElement controls = new VisualElement();
        section.Add(controls);
        AddField(panel, controls, track.FindPropertyRelative("eventPath"), "FMOD Event Path");
        AddField(panel, controls, track.FindPropertyRelative("bankName"), "FMOD Bank Name");
        AddField(panel, controls, track.FindPropertyRelative("volume"), "Volume");
        AddField(panel, controls, autoStart, "Auto Start");
        PropertyField restart = AddField(panel, controls, track.FindPropertyRelative("restartWhenPathChanges"), "Restart When Path Changes");

        // Binding callbacks update the dependent form without rebuilding unrelated sections.
        controls.style.display = enabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
        restart.style.display = autoStart.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
        section.TrackPropertyValue(enabled, changed => controls.style.display = changed.boolValue ? DisplayStyle.Flex : DisplayStyle.None);
        section.TrackPropertyValue(autoStart, changed => restart.style.display = changed.boolValue ? DisplayStyle.Flex : DisplayStyle.None);
    }

    /// <summary>
    /// Binds a music field to the existing draft save/undo workflow using its serialized tooltip.
    /// </summary>
    /// <param name="panel">Owning draft panel.</param>
    /// <param name="parent">Container receiving the field.</param>
    /// <param name="property">Serialized music setting.</param>
    /// <param name="label">Field label.</param>
    /// <returns>Bound field for optional dependent visibility.</returns>
    private static PropertyField AddField(GameAudioManagerPresetsPanel panel, VisualElement parent, SerializedProperty property, string label)
    {
        PropertyField field = new PropertyField(property, label) { tooltip = property.tooltip };
        field.BindProperty(property);
        field.RegisterCallback<SerializedPropertyChangeEvent>(change => panel.MarkSelectedPresetDirty());
        parent.Add(field);
        return field;
    }
    #endregion

    #endregion
}
