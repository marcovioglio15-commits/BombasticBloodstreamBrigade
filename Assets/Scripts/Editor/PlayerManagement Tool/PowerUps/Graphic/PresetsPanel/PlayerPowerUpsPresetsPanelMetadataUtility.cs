using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds metadata and drop-pool tier sections for the power-ups presets panel.
/// </summary>
public static class PlayerPowerUpsPresetsPanelMetadataUtility
{
    #region Methods

    #region Section Builders
    public static void BuildMetadataSection(PlayerPowerUpsPresetsPanel panel)
    {
        Label header = new Label("Preset Details");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(header, "NashCore.PlayerManagement.PowerUps.Metadata.PresetDetails");
        panel.sectionContentRoot.Add(header);

        SerializedProperty presetIdProperty = panel.presetSerializedObject.FindProperty("presetId");
        SerializedProperty presetNameProperty = panel.presetSerializedObject.FindProperty("presetName");
        SerializedProperty descriptionProperty = panel.presetSerializedObject.FindProperty("description");
        SerializedProperty versionProperty = panel.presetSerializedObject.FindProperty("version");

        if (presetIdProperty == null || presetNameProperty == null || descriptionProperty == null || versionProperty == null)
        {
            Label missingLabel = new Label("Power ups preset metadata properties are missing.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            panel.sectionContentRoot.Add(missingLabel);
            return;
        }

        TextField nameField = new TextField("Preset Name");
        nameField.isDelayed = true;
        nameField.BindProperty(presetNameProperty);
        nameField.RegisterValueChangedCallback(evt =>
        {
            panel.RenamePreset(panel.selectedPreset, evt.newValue);
        });
        panel.sectionContentRoot.Add(nameField);

        TextField versionField = new TextField("Version");
        versionField.isDelayed = true;
        versionField.BindProperty(versionProperty);
        panel.sectionContentRoot.Add(versionField);

        TextField descriptionField = new TextField("Description");
        descriptionField.multiline = true;
        descriptionField.isDelayed = true;
        descriptionField.style.height = 64f;
        descriptionField.BindProperty(descriptionProperty);
        panel.sectionContentRoot.Add(descriptionField);

        VisualElement idRow = new VisualElement();
        idRow.style.flexDirection = FlexDirection.Row;
        idRow.style.alignItems = Align.Center;

        TextField idField = new TextField("Preset ID");
        idField.SetEnabled(false);
        idField.style.flexGrow = 1f;
        idField.BindProperty(presetIdProperty);
        idRow.Add(idField);

        Button regenerateButton = new Button(panel.RegeneratePresetId);
        regenerateButton.text = "Regenerate";
        regenerateButton.style.marginLeft = 6f;
        idRow.Add(regenerateButton);

        panel.sectionContentRoot.Add(idRow);
    }

    public static void BuildDropPoolsAndTiersSection(PlayerPowerUpsPresetsPanel panel)
    {
        Label header = new Label("Drop Pools & Tiers");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(header, "NashCore.PlayerManagement.PowerUps.Metadata.DropPoolsAndTiers");
        panel.sectionContentRoot.Add(header);

        SerializedProperty dropPoolsProperty = panel.presetSerializedObject.FindProperty("dropPools");
        SerializedProperty tierLevelsProperty = panel.presetSerializedObject.FindProperty("tierLevels");

        if (dropPoolsProperty == null || tierLevelsProperty == null)
        {
            Label missingLabel = new Label("Drop-pool or tier properties are missing.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            panel.sectionContentRoot.Add(missingLabel);
            return;
        }

        Label infoLabel = new Label("Define named drop pools first, then map each pool to weighted tier candidates and each tier to weighted Active/Passive power-up IDs.");
        infoLabel.style.marginBottom = 4f;
        panel.sectionContentRoot.Add(infoLabel);

        PropertyField dropPoolsField = new PropertyField(dropPoolsProperty, "Drop Pools");
        dropPoolsField.BindProperty(dropPoolsProperty);
        panel.sectionContentRoot.Add(dropPoolsField);

        PropertyField tierLevelsField = new PropertyField(tierLevelsProperty, "Tier Levels");
        tierLevelsField.BindProperty(tierLevelsProperty);
        panel.sectionContentRoot.Add(tierLevelsField);

        List<string> dropPoolIdOptions = PowerUpTierOptionsUtility.BuildDropPoolIdOptions(panel.presetSerializedObject);
        List<string> tierIdOptions = PowerUpTierOptionsUtility.BuildTierIdOptions(panel.presetSerializedObject);

        if (dropPoolIdOptions.Count <= 0)
        {
            HelpBox warningBox = new HelpBox("No Drop Pool IDs configured. Milestone unlocks will show warnings until at least one drop pool is defined.", HelpBoxMessageType.Warning);
            warningBox.style.marginTop = 4f;
            panel.sectionContentRoot.Add(warningBox);
        }

        if (tierIdOptions.Count <= 0)
        {
            HelpBox warningBox = new HelpBox("No Tier IDs configured. Drop pools and milestone extraction will show warnings until at least one tier is defined.", HelpBoxMessageType.Warning);
            warningBox.style.marginTop = 4f;
            panel.sectionContentRoot.Add(warningBox);
        }
    }
    #endregion

    #endregion
}
