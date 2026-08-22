#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds room-scoped portal-unlock audio controls independently from individual visual effects.
/// </summary>
internal static class GameRoomPortalUnlockAudioEditorUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds the shared FMOD portal-unlock policy and conditionally exposes its playback cardinality.
    /// </summary>
    /// <param name="root">Portal tab root receiving the audio section.</param>
    /// <param name="settings">Serialized portal settings containing the room-scoped audio policy.</param>
    public static void Build(VisualElement root, SerializedProperty settings)
    {
        if (root == null || settings == null)
            return;

        SerializedProperty enabled = settings.FindPropertyRelative("playUnlockAudio");
        SerializedProperty playbackMode = settings.FindPropertyRelative("unlockAudioPlaybackMode");
        VisualElement section = CreateSection();

        if (enabled == null || playbackMode == null)
        {
            section.Add(new HelpBox("The portal unlock audio settings could not be serialized.",
                                    HelpBoxMessageType.Warning));
            root.Add(section);
            return;
        }

        section.Add(new HelpBox(
            "The shared Room Reward Portal Unlock event is independent from individual animation entries and uses each unlocked portal position.",
            HelpBoxMessageType.Info));
        Toggle enabledField = AddToggleField(section, enabled);
        EnumField playbackField = AddEnumField(section, playbackMode);
        GameRoomRewardEditorElementUtility.SetVisible(playbackField, enabled.boolValue);
        enabledField.RegisterValueChangedCallback(evt =>
        {
            GameRoomRewardEditorElementUtility.SetVisible(playbackField, evt.newValue);
        });
        root.Add(section);
    }
    #endregion

    #region Field Construction
    /// <summary>
    /// Creates the compact audio section root and title.
    /// </summary>
    /// <returns>Configured audio section.</returns>
    private static VisualElement CreateSection()
    {
        VisualElement section = new VisualElement();
        section.style.marginTop = 8f;
        Label title = new Label("Portal Unlock Audio");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        section.Add(title);
        return section;
    }

    /// <summary>
    /// Adds a Boolean selector that commits before dependent controls refresh.
    /// </summary>
    /// <param name="parent">Visual parent receiving the toggle.</param>
    /// <param name="property">Serialized Boolean property.</param>
    /// <returns>Created toggle.</returns>
    private static Toggle AddToggleField(VisualElement parent,
                                         SerializedProperty property)
    {
        Toggle field = new Toggle("Play Unlock Event");
        field.tooltip = property.tooltip;
        field.SetValueWithoutNotify(property.boolValue);
        field.RegisterValueChangedCallback(evt =>
        {
            if (property.boolValue == evt.newValue)
                return;

            property.boolValue = evt.newValue;
            property.serializedObject.ApplyModifiedProperties();
            GameManagementDraftSession.MarkDirty();
        });
        parent.Add(field);
        return field;
    }

    /// <summary>
    /// Adds the enum selector controlling whether the shared event plays once per room or portal.
    /// </summary>
    /// <param name="parent">Visual parent receiving the selector.</param>
    /// <param name="property">Serialized enum property.</param>
    /// <returns>Created enum selector.</returns>
    private static EnumField AddEnumField(VisualElement parent,
                                          SerializedProperty property)
    {
        Enum currentValue = (Enum)Enum.ToObject(
            typeof(GameRoomPortalUnlockAudioPlaybackMode),
            property.intValue);
        EnumField field = new EnumField("Playback", currentValue);
        field.tooltip = property.tooltip;
        field.RegisterValueChangedCallback(evt =>
        {
            int nextValue = Convert.ToInt32(evt.newValue);

            if (property.intValue == nextValue)
                return;

            property.intValue = nextValue;
            property.serializedObject.ApplyModifiedProperties();
            GameManagementDraftSession.MarkDirty();
        });
        parent.Add(field);
        return field;
    }
    #endregion

    #endregion
}
#endif
