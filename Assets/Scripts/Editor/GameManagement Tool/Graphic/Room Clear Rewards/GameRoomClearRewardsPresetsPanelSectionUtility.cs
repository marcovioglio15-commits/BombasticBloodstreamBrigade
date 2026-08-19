using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Routes Room Clear Rewards detail tabs and builds simple bound metadata and presentation settings sections.
/// </summary>
internal static class GameRoomClearRewardsPresetsPanelSectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds one active details tab from the current serialized preset.
    /// </summary>
    /// <param name="root">Scroll content root receiving the tab controls.</param>
    /// <param name="serializedPreset">Current Room Clear Rewards preset serialization context.</param>
    /// <param name="tab">Selected details tab.</param>
    public static void Build(VisualElement root,
                             SerializedObject serializedPreset,
                             GameRoomClearRewardsPresetsPanel.DetailsTab tab)
    {
        if (root == null || serializedPreset == null)
            return;

        switch (tab)
        {
            case GameRoomClearRewardsPresetsPanel.DetailsTab.Metadata:
                BuildMetadata(root, serializedPreset);
                break;
            case GameRoomClearRewardsPresetsPanel.DetailsTab.Modules:
                GameRoomRewardModuleEditorUtility.Build(root, serializedPreset);
                break;
            case GameRoomClearRewardsPresetsPanel.DetailsTab.Rewards:
                GameRoomRewardCompositionEditorUtility.BuildRewards(root, serializedPreset);
                break;
            case GameRoomClearRewardsPresetsPanel.DetailsTab.Presentation:
                GameRoomRewardCompositionEditorUtility.BuildPresentation(root, serializedPreset);
                break;
            case GameRoomClearRewardsPresetsPanel.DetailsTab.PlayerLog:
                BuildSettings(root,
                              serializedPreset,
                              "Player Reward Log",
                              "playerLogSettings",
                              "Preauthored vertical rows above the player. Runtime code never instantiates UI.");
                break;
            case GameRoomClearRewardsPresetsPanel.DetailsTab.PortalLog:
                GameRoomRewardPortalSettingsEditorUtility.Build(root, serializedPreset);
                break;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds preset identity and dynamic Player Management context fields.
    /// </summary>
    /// <param name="root">Content root receiving fields.</param>
    /// <param name="serializedPreset">Current serialized preset.</param>
    private static void BuildMetadata(VisualElement root, SerializedObject serializedPreset)
    {
        root.Add(new HelpBox(
            "The Player Context supplies the scalable-stat catalog used by selectors, unified formulas, validation and ECS bake. No free stat IDs are exposed in module authoring.",
            HelpBoxMessageType.Info));
        AddProperty(root, serializedPreset, "presetName", "Preset Name");
        AddProperty(root, serializedPreset, "version", "Version");
        AddProperty(root, serializedPreset, "description", "Description");
        AddProperty(root, serializedPreset, "playerContextPreset", "Player Context Preset");
        PropertyField idField = AddProperty(root, serializedPreset, "presetId", "Preset ID");

        if (idField != null)
            idField.SetEnabled(false);
    }

    /// <summary>
    /// Builds one complete serialized settings block.
    /// </summary>
    /// <param name="root">Content root receiving the settings.</param>
    /// <param name="serializedPreset">Current serialized preset.</param>
    /// <param name="title">Visible section title.</param>
    /// <param name="propertyName">Serialized settings property name.</param>
    /// <param name="message">Architecture note shown above settings.</param>
    private static void BuildSettings(VisualElement root,
                                      SerializedObject serializedPreset,
                                      string title,
                                      string propertyName,
                                      string message)
    {
        Label titleLabel = new Label(title);
        titleLabel.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
        root.Add(titleLabel);
        root.Add(new HelpBox(message, HelpBoxMessageType.Info));
        AddProperty(root, serializedPreset, propertyName, title);
        GameRoomClearRewardsPreset preset =
            serializedPreset.targetObject as GameRoomClearRewardsPreset;

        if (preset != null &&
            !GameRoomRewardPresentationValidationUtility.TryValidate(preset,
                                                                      out string warningMessage))
        {
            root.Add(new HelpBox(warningMessage, HelpBoxMessageType.Warning));
        }
    }

    /// <summary>
    /// Adds one bound property field and marks the shared draft session dirty after changes.
    /// </summary>
    /// <param name="root">Parent visual element.</param>
    /// <param name="serializedPreset">Current serialized preset.</param>
    /// <param name="propertyName">Serialized property name.</param>
    /// <param name="label">Visible field label.</param>
    /// <returns>Created field, or null when the serialized property is unavailable.</returns>
    private static PropertyField AddProperty(VisualElement root,
                                             SerializedObject serializedPreset,
                                             string propertyName,
                                             string label)
    {
        SerializedProperty property = serializedPreset.FindProperty(propertyName);

        if (property == null)
            return null;

        PropertyField field = new PropertyField(property, label);
        field.tooltip = property.tooltip;
        field.BindProperty(property);
        field.RegisterValueChangeCallback(evt => GameManagementDraftSession.MarkDirty());
        root.Add(field);
        return field;
    }
    #endregion

    #endregion
}
