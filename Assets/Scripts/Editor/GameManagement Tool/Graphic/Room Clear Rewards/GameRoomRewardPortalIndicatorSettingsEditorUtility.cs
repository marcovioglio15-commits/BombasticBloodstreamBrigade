#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds conditional open-portal screen-edge indicator controls for one Room Clear Rewards preset.
/// </summary>
internal static class GameRoomRewardPortalIndicatorSettingsEditorUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the dedicated Portal Indicators tab and exposes visual settings only while the feature is enabled.
    /// </summary>
    /// <param name="root">Tab root receiving the portal indicator controls.</param>
    /// <param name="serializedPreset">Current Room Clear Rewards serialization context.</param>
    public static void Build(VisualElement root, SerializedObject serializedPreset)
    {
        if (root == null || serializedPreset == null)
            return;

        serializedPreset.Update();
        SerializedProperty settings = serializedPreset.FindProperty("portalIndicatorSettings");

        if (settings == null)
            return;

        Label title = new Label("Open Portal Indicators");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        root.Add(title);
        //root.Add(new HelpBox(
            //"A preauthored screen-space view points toward every traversable portal outside the camera view. No indicator UI is instantiated during gameplay.",
            //HelpBoxMessageType.Info));
        SerializedProperty enabled = settings.FindPropertyRelative("enabled");
        PropertyField enabledField = AddProperty(root,
                                                 enabled,
                                                 "Enable Portal Indicators");
        VisualElement enabledGroup = new VisualElement();
        AddProperty(enabledGroup,
                    settings.FindPropertyRelative("indicatorSprite"),
                    "Indicator Sprite");
        AddProperty(enabledGroup,
                    settings.FindPropertyRelative("indicatorColor"),
                    "Indicator Color");
        AddProperty(enabledGroup,
                    settings.FindPropertyRelative("indicatorSizePixels"),
                    "Indicator Size Pixels");
        AddProperty(enabledGroup,
                    settings.FindPropertyRelative("edgePaddingPixels"),
                    "Edge Padding Pixels");
        AddProperty(enabledGroup,
                    settings.FindPropertyRelative("sortingOrder"),
                    "HUD Sorting Order");
        AddProperty(enabledGroup,
                    settings.FindPropertyRelative("worldOffset"),
                    "Portal World Offset");
        root.Add(enabledGroup);
        UpdateVisibility(enabled, enabledGroup);

        if (enabledField != null)
        {
            enabledField.RegisterValueChangeCallback(evt =>
            {
                serializedPreset.ApplyModifiedProperties();
                UpdateVisibility(enabled, enabledGroup);
                GameManagementDraftSession.MarkDirty();
            });
        }

        GameRoomClearRewardsPreset preset =
            serializedPreset.targetObject as GameRoomClearRewardsPreset;

        if (preset != null &&
            !GameRoomRewardPresentationValidationUtility.TryValidate(
                preset,
                out string warningMessage))
        {
            root.Add(new HelpBox(warningMessage, HelpBoxMessageType.Warning));
        }
    }
    #endregion

    #region Fields
    /// <summary>
    /// Adds one bound property and marks the shared draft session dirty after changes.
    /// </summary>
    /// <param name="parent">Visual container receiving the field.</param>
    /// <param name="property">Serialized value bound to the field.</param>
    /// <param name="label">Visible field label.</param>
    /// <returns>Created property field, or null when the serialized value is unavailable.</returns>
    private static PropertyField AddProperty(VisualElement parent,
                                             SerializedProperty property,
                                             string label)
    {
        if (parent == null || property == null)
            return null;

        PropertyField field = new PropertyField(property, label);
        field.tooltip = property.tooltip;
        field.BindProperty(property);
        field.RegisterValueChangeCallback(evt => GameManagementDraftSession.MarkDirty());
        parent.Add(field);
        return field;
    }
    #endregion

    #region Visibility
    /// <summary>
    /// Shows visual and projection settings only when portal indicators are enabled.
    /// </summary>
    /// <param name="enabled">Serialized feature toggle.</param>
    /// <param name="enabledGroup">Container holding enabled-only controls.</param>
    private static void UpdateVisibility(SerializedProperty enabled,
                                         VisualElement enabledGroup)
    {
        if (enabled == null || enabledGroup == null)
            return;

        GameRoomRewardEditorElementUtility.SetVisible(enabledGroup,
                                                      enabled.boolValue);
    }
    #endregion

    #endregion
}
#endif
