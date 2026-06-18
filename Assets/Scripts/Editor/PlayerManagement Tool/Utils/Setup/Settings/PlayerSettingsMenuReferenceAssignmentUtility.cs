using UnityEditor;
using UnityEngine;

/// <summary>
/// Assigns generated Settings menu prefab references to runtime controllers through serialized fields.
/// </summary>
internal static class PlayerSettingsMenuReferenceAssignmentUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Copies every generated Settings menu reference into the runtime SettingsMenuController.
    /// </summary>
    /// <param name="controller">Controller receiving the generated UI references.</param>
    /// <param name="references">Reference set collected during prefab hierarchy generation.</param>
    public static void AssignControllerReferences(SettingsMenuController controller, PlayerSettingsMenuReferences references)
    {
        AssignObject(controller, "panelRoot", references.PanelRoot);
        AssignObject(controller, "audioTabButton", references.AudioTabButton);
        AssignObject(controller, "gameplayTabButton", references.GameplayTabButton);
        AssignObject(controller, "confirmButton", references.ConfirmButton);
        AssignObject(controller, "resetDefaultsButton", references.ResetDefaultsButton);
        AssignObject(controller, "closeButton", references.CloseButton);
        AssignObject(controller, "audioPanelRoot", references.AudioPanelRoot);
        AssignObject(controller, "gameplayPanelRoot", references.GameplayPanelRoot);
        AssignObject(controller, "masterVolumeSlider", references.MasterVolumeSlider);
        AssignObject(controller, "sfxVolumeSlider", references.SfxVolumeSlider);
        AssignObject(controller, "musicVolumeSlider", references.MusicVolumeSlider);
        AssignObject(controller, "masterVolumeValueLabel", references.MasterVolumeValueLabel);
        AssignObject(controller, "sfxVolumeValueLabel", references.SfxVolumeValueLabel);
        AssignObject(controller, "musicVolumeValueLabel", references.MusicVolumeValueLabel);
        AssignObject(controller, "visualPointerToggle", references.VisualPointerToggle);
        AssignObject(controller, "fullscreenToggle", references.FullscreenToggle);
        AssignObject(controller, "frameRateSelector", references.FrameRateSelector);
        AssignObject(controller, "damageRumbleMultiplierSlider", references.DamageRumbleMultiplierSlider);
        AssignObject(controller, "fireRumbleMultiplierSlider", references.FireRumbleMultiplierSlider);
        AssignObject(controller, "damageRumbleValueLabel", references.DamageRumbleValueLabel);
        AssignObject(controller, "fireRumbleValueLabel", references.FireRumbleValueLabel);
    }
    #endregion

    #region Serialized Helpers
    /// <summary>
    /// Assigns one object reference to a serialized field when the target field exists.
    /// </summary>
    /// <param name="target">Object receiving the assignment.</param>
    /// <param name="fieldName">Serialized field name.</param>
    /// <param name="value">Object reference value.</param>
    private static void AssignObject(Object target, string fieldName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty(fieldName);

        if (property == null)
            return;

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
    #endregion

    #endregion
}
