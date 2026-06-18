using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Renders the Laser Beam visual preset selector as a popup backed by stable numeric IDs resolved from authored visual presets.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerLaserBeamVisualPresetSelectorAttribute))]
public sealed class PlayerLaserBeamVisualPresetSelectorAttributeDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the popup-based editor UI for the Laser Beam visual preset selector field.
    /// </summary>
    /// <param name="property">Serialized selector property.</param>
    /// <returns>Configured UI Toolkit element.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();

        if (property == null || property.propertyType != SerializedPropertyType.Integer)
        {
            PropertyField fallbackField = new PropertyField(property);
            fallbackField.BindProperty(property);
            root.Add(fallbackField);
            return root;
        }

        PopupField<string> popupField = new PopupField<string>();
        popupField.label = property.displayName;
        popupField.tooltip = "Stable Laser Beam visual preset ID resolved against the currently authored Player Visual Preset definitions.";
        root.Add(popupField);

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        warningBox.style.display = DisplayStyle.None;
        root.Add(warningBox);

        void RefreshState()
        {
            List<PlayerLaserBeamVisualPresetEditorOption> options = PlayerLaserBeamVisualPresetEditorUtility.BuildOptions();

            if (options.Count <= 0)
            {
                popupField.choices = new List<string> { string.Format("Visual Preset [{0}]", property.intValue) };
                popupField.index = 0;
                warningBox.text = "No authored Laser Beam visual presets were found. Runtime will use the default fallback colors for this ID.";
                warningBox.style.display = DisplayStyle.Flex;
                return;
            }

            List<string> popupLabels = new List<string>(options.Count + 1);
            List<int> popupIds = new List<int>(options.Count + 1);
            int selectedIndex = PlayerLaserBeamVisualPresetEditorUtility.ResolveSelectedIndex(options, property.intValue);

            if (selectedIndex < 0)
            {
                popupLabels.Add(string.Format("Missing Preset [{0}]", property.intValue));
                popupIds.Add(property.intValue);
                selectedIndex = 0;
                warningBox.text = "The selected Laser Beam visual preset ID does not exist in the currently authored Player Visual Presets. Runtime will fall back to the default beam colors until a matching ID is added.";
                warningBox.style.display = DisplayStyle.Flex;
            }
            else
            {
                warningBox.style.display = DisplayStyle.None;
            }

            for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
            {
                popupLabels.Add(options[optionIndex].BuildDisplayLabel());
                popupIds.Add(options[optionIndex].StableId);
            }

            popupField.choices = popupLabels;
            popupField.index = selectedIndex;

            popupField.userData = popupIds;
        }

        popupField.RegisterValueChangedCallback(evt =>
        {
            List<int> popupIds = popupField.userData as List<int>;

            if (popupIds == null || popupField.index < 0 || popupField.index >= popupIds.Count)
                return;

            int selectedStableId = popupIds[popupField.index];

            if (property.intValue == selectedStableId)
                return;

            property.serializedObject.Update();
            property.intValue = selectedStableId;
            property.serializedObject.ApplyModifiedProperties();
            PlayerManagementDraftSession.MarkDirty();
            RefreshState();
        });

        void HandleExternalRefresh()
        {
            RefreshState();
        }

        root.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            EditorApplication.projectChanged += HandleExternalRefresh;
            Undo.undoRedoPerformed += HandleExternalRefresh;
        });
        root.RegisterCallback<DetachFromPanelEvent>(evt =>
        {
            EditorApplication.projectChanged -= HandleExternalRefresh;
            Undo.undoRedoPerformed -= HandleExternalRefresh;
        });
        root.TrackPropertyValue(property, changedProperty =>
        {
            RefreshState();
        });

        RefreshState();
        return root;
    }
    #endregion

    #endregion
}
