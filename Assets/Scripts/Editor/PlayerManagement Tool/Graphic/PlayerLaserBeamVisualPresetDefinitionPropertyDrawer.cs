using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Renders one Laser Beam visual preset definition entry with a compact, facing layout.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerLaserBeamVisualPresetDefinition))]
public sealed class PlayerLaserBeamVisualPresetDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit layout for one Laser Beam visual preset definition entry.
    /// </summary>
    /// <param name="property">Serialized preset definition property.</param>
    /// <returns>Configured UI Toolkit element.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        root.style.marginBottom = 4f;

        if (property == null)
            return root;

        SerializedProperty stableIdProperty = property.FindPropertyRelative("stableId");
        SerializedProperty displayNameProperty = property.FindPropertyRelative("displayName");
        SerializedProperty coreColorProperty = property.FindPropertyRelative("coreColor");
        SerializedProperty flowColorProperty = property.FindPropertyRelative("flowColor");
        SerializedProperty stormColorProperty = property.FindPropertyRelative("stormColor");
        SerializedProperty contactColorProperty = property.FindPropertyRelative("contactColor");

        if (stableIdProperty == null ||
            displayNameProperty == null ||
            coreColorProperty == null ||
            flowColorProperty == null ||
            stormColorProperty == null ||
            contactColorProperty == null)
        {
            PropertyField fallbackField = new PropertyField(property);
            fallbackField.BindProperty(property);
            root.Add(fallbackField);
            return root;
        }

        PropertyField displayNameField = new PropertyField(displayNameProperty, "Display Name");
        PropertyField stableIdField = new PropertyField(stableIdProperty, "Stable ID");
        PropertyField coreColorField = new PropertyField(coreColorProperty, "Core Color");
        PropertyField flowColorField = new PropertyField(flowColorProperty, "Flow Color");
        PropertyField stormColorField = new PropertyField(stormColorProperty, "Storm Color");
        PropertyField contactColorField = new PropertyField(contactColorProperty, "Contact Color");
        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        warningBox.style.display = DisplayStyle.None;

        displayNameField.BindProperty(displayNameProperty);
        stableIdField.BindProperty(stableIdProperty);
        coreColorField.BindProperty(coreColorProperty);
        flowColorField.BindProperty(flowColorProperty);
        stormColorField.BindProperty(stormColorProperty);
        contactColorField.BindProperty(contactColorProperty);

        displayNameField.RegisterCallback<SerializedPropertyChangeEvent>(evt => PlayerManagementDraftSession.MarkDirty());
        stableIdField.RegisterCallback<SerializedPropertyChangeEvent>(evt => PlayerManagementDraftSession.MarkDirty());
        coreColorField.RegisterCallback<SerializedPropertyChangeEvent>(evt => PlayerManagementDraftSession.MarkDirty());
        flowColorField.RegisterCallback<SerializedPropertyChangeEvent>(evt => PlayerManagementDraftSession.MarkDirty());
        stormColorField.RegisterCallback<SerializedPropertyChangeEvent>(evt => PlayerManagementDraftSession.MarkDirty());
        contactColorField.RegisterCallback<SerializedPropertyChangeEvent>(evt => PlayerManagementDraftSession.MarkDirty());

        root.Add(displayNameField);
        root.Add(stableIdField);
        root.Add(coreColorField);
        root.Add(flowColorField);
        root.Add(stormColorField);
        root.Add(contactColorField);
        root.Add(warningBox);

        void RefreshWarning()
        {
            if (string.IsNullOrWhiteSpace(displayNameProperty.stringValue))
            {
                warningBox.text = "Display Name is empty. Formula helper labels and popup selectors will become harder to read.";
                warningBox.style.display = DisplayStyle.Flex;
                return;
            }

            if (stableIdProperty.intValue < 0)
            {
                warningBox.text = "Stable ID is negative. Runtime scaling and selector popups expect non-negative IDs.";
                warningBox.style.display = DisplayStyle.Flex;
                return;
            }

            warningBox.style.display = DisplayStyle.None;
        }

        root.TrackPropertyValue(displayNameProperty, changedProperty =>
        {
            RefreshWarning();
        });
        root.TrackPropertyValue(stableIdProperty, changedProperty =>
        {
            RefreshWarning();
        });

        RefreshWarning();
        return root;
    }
    #endregion

    #endregion
}
