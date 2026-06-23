using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the enemy visual preset face flipbook subsection.
/// </summary>
internal static class EnemyVisualPresetsPanelFaceFlipbookSectionUtility
{
    #region Constants
    private const string FaceFlipbookMaterialPath = "Assets/3D/Materials/M_EnemiesFaces.mat";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the dedicated face flipbook tab with dependent controls and warnings.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the serialized preset context.</param>
    /// <returns>Face flipbook subsection content.</returns>
    public static VisualElement BuildFaceFlipbookSubSection(EnemyVisualPresetsPanel panel)
    {
        SerializedProperty faceProperty = panel.PresetSerializedObject.FindProperty("faceFlipbook");
        VisualElement container = EnemyVisualPresetsPanelSectionsUtility.CreateSubSectionContainer("Face Flipbook");

        if (faceProperty == null)
            return container;

        SerializedProperty enabledProperty = faceProperty.FindPropertyRelative("enabled");
        EnemyVisualPresetsPanelSectionsUtility.AddReactiveToggleField(panel,
                                                                      container,
                                                                      enabledProperty,
                                                                      "Enabled",
                                                                      "Enables shader-driven idle, attack and damage face flipbook playback.",
                                                                      "Edit Enemy Face Flipbook Settings");

        if (enabledProperty != null && !enabledProperty.boolValue)
        {
            container.Add(new HelpBox("Face flipbook playback is disabled for this visual preset.", HelpBoxMessageType.Info));
            return container;
        }

        AddStateControls(panel, container, faceProperty.FindPropertyRelative("idle"), "Idle", false);
        AddStateControls(panel, container, faceProperty.FindPropertyRelative("attack"), "Attack", true);
        AddStateControls(panel, container, faceProperty.FindPropertyRelative("damage"), "Damage", true);
        return container;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds one state foldout with atlas, grid, playback and optional duration controls.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the serialized preset context.</param>
    /// <param name="container">Parent element receiving the state foldout.</param>
    /// <param name="stateProperty">Serialized state settings property.</param>
    /// <param name="stateLabel">State label shown in the tool.</param>
    /// <param name="showDuration">Whether this state exposes temporary playback duration.</param>
    private static void AddStateControls(EnemyVisualPresetsPanel panel,
                                         VisualElement container,
                                         SerializedProperty stateProperty,
                                         string stateLabel,
                                         bool showDuration)
    {
        if (stateProperty == null)
            return;

        Foldout foldout = ManagementToolFoldoutStateUtility.CreateFoldout(stateLabel,
                                                                          "NashCore.EnemyManagement.Visual.FaceFlipbook." + stateLabel,
                                                                          true);
        foldout.style.marginTop = 4f;
        SerializedProperty enabledProperty = stateProperty.FindPropertyRelative("enabled");
        EnemyVisualPresetsPanelSectionsUtility.AddReactiveToggleField(panel,
                                                                      foldout,
                                                                      enabledProperty,
                                                                      "Enabled",
                                                                      "Enables the " + stateLabel + " face state. Disabled temporary states fall back to Idle.",
                                                                      "Edit Enemy Face Flipbook Settings");

        if (enabledProperty != null && !enabledProperty.boolValue)
        {
            foldout.Add(new HelpBox(stateLabel + " face playback is disabled.", HelpBoxMessageType.Info));
            container.Add(foldout);
            return;
        }

        AddPropertyField(panel, foldout, stateProperty, "atlas", "Atlas", "Optional atlas reference applied to the shared enemy-face material slot for this state.", stateLabel);
        AddPropertyField(panel, foldout, stateProperty, "columns", "Columns", "Number of atlas columns for this face state.");
        AddPropertyField(panel, foldout, stateProperty, "rows", "Rows", "Number of atlas rows for this face state.");
        AddPropertyField(panel, foldout, stateProperty, "frameCount", "Frame Count", "Valid frames read left-to-right and top-to-bottom.");
        AddPropertyField(panel, foldout, stateProperty, "framesPerSecond", "Frames Per Second", "Playback speed used by this face state.");
        AddPropertyField(panel, foldout, stateProperty, "startFrame", "Start Frame", "Frame offset used when playback starts from a cell other than zero.");

        if (showDuration)
            AddPropertyField(panel, foldout, stateProperty, "durationSeconds", "Duration Seconds", "Independent temporary playback duration after this state is triggered.");

        AddStateWarnings(stateProperty, foldout, stateLabel, showDuration);
        container.Add(foldout);
    }

    /// <summary>
    /// Adds one serialized property field and marks the draft session dirty when edited.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the serialized preset context.</param>
    /// <param name="target">Parent element receiving the property field.</param>
    /// <param name="parentProperty">Serialized parent object.</param>
    /// <param name="relativePropertyName">Relative property name.</param>
    /// <param name="label">Visible control label.</param>
    /// <param name="tooltip">Tooltip explaining the setting.</param>
    /// <param name="faceStateLabel">Optional face state label used for material atlas slot synchronization.</param>
    private static void AddPropertyField(EnemyVisualPresetsPanel panel,
                                         VisualElement target,
                                         SerializedProperty parentProperty,
                                         string relativePropertyName,
                                         string label,
                                         string tooltip,
                                         string faceStateLabel = null)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(relativePropertyName);

        if (property == null)
            return;

        PropertyField propertyField = new PropertyField(property, label);
        propertyField.BindProperty(property);
        propertyField.tooltip = tooltip;
        propertyField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            EnemyManagementDraftSession.MarkDirty();

            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                string.Equals(relativePropertyName, "atlas", StringComparison.Ordinal))
                TryApplyAtlasToSharedMaterial(faceStateLabel, property.objectReferenceValue);

            panel.RefreshPresetList();
        });
        target.Add(propertyField);
    }

    /// <summary>
    /// Applies atlas property edits to the shared material slot used by the face shader.
    /// </summary>
    /// <param name="faceStateLabel">Face state label used to resolve the material texture slot.</param>
    /// <param name="atlasObject">Assigned atlas object from the property field.</param>
    private static void TryApplyAtlasToSharedMaterial(string faceStateLabel,
                                                      UnityEngine.Object atlasObject)
    {
        Texture texture = atlasObject as Texture;

        if (texture == null)
            return;

        string materialPropertyName = ResolveMaterialAtlasPropertyName(faceStateLabel);

        if (string.IsNullOrEmpty(materialPropertyName))
            return;

        Material material = AssetDatabase.LoadAssetAtPath<Material>(FaceFlipbookMaterialPath);

        if (material == null || !material.HasProperty(materialPropertyName))
            return;

        Undo.RecordObject(material, "Apply Enemy Face Atlas");
        material.SetTexture(materialPropertyName, texture);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
    }

    /// <summary>
    /// Resolves the shared material texture slot for a face state label.
    /// </summary>
    /// <param name="faceStateLabel">Face state label from the tool foldout.</param>
    /// <returns>Material texture property name, or null when the state is unsupported.</returns>
    private static string ResolveMaterialAtlasPropertyName(string faceStateLabel)
    {
        switch (faceStateLabel)
        {
            case "Idle":
                return "_MainTex";

            case "Attack":
                return "_FaceAttackTex";

            case "Damage":
                return "_FaceDamageTex";

            default:
                return null;
        }
    }

    /// <summary>
    /// Adds warning boxes for invalid face state values without mutating the serialized preset.
    /// </summary>
    /// <param name="stateProperty">Serialized state settings property.</param>
    /// <param name="container">Parent element receiving warning boxes.</param>
    /// <param name="stateLabel">State label used in messages.</param>
    /// <param name="requiresDuration">Whether duration warnings apply.</param>
    private static void AddStateWarnings(SerializedProperty stateProperty,
                                         VisualElement container,
                                         string stateLabel,
                                         bool requiresDuration)
    {
        SerializedProperty columnsProperty = stateProperty.FindPropertyRelative("columns");
        SerializedProperty rowsProperty = stateProperty.FindPropertyRelative("rows");
        SerializedProperty frameCountProperty = stateProperty.FindPropertyRelative("frameCount");
        SerializedProperty framesPerSecondProperty = stateProperty.FindPropertyRelative("framesPerSecond");
        SerializedProperty startFrameProperty = stateProperty.FindPropertyRelative("startFrame");
        SerializedProperty durationProperty = stateProperty.FindPropertyRelative("durationSeconds");

        if (columnsProperty != null && columnsProperty.intValue <= 0)
            container.Add(new HelpBox(stateLabel + " columns should be greater than zero.", HelpBoxMessageType.Warning));

        if (rowsProperty != null && rowsProperty.intValue <= 0)
            container.Add(new HelpBox(stateLabel + " rows should be greater than zero.", HelpBoxMessageType.Warning));

        if (frameCountProperty != null && frameCountProperty.intValue <= 0)
            container.Add(new HelpBox(stateLabel + " frame count should be greater than zero.", HelpBoxMessageType.Warning));

        if (columnsProperty != null &&
            rowsProperty != null &&
            frameCountProperty != null &&
            columnsProperty.intValue > 0 &&
            rowsProperty.intValue > 0 &&
            frameCountProperty.intValue > columnsProperty.intValue * rowsProperty.intValue)
        {
            container.Add(new HelpBox(stateLabel + " frame count exceeds available atlas cells.", HelpBoxMessageType.Warning));
        }

        if (framesPerSecondProperty != null && framesPerSecondProperty.floatValue <= 0f)
            container.Add(new HelpBox(stateLabel + " frames per second should be greater than zero.", HelpBoxMessageType.Warning));

        if (startFrameProperty != null && startFrameProperty.floatValue < 0f)
            container.Add(new HelpBox(stateLabel + " start frame should be zero or positive.", HelpBoxMessageType.Warning));

        if (requiresDuration && durationProperty != null && durationProperty.floatValue <= 0f)
            container.Add(new HelpBox(stateLabel + " duration should be greater than zero.", HelpBoxMessageType.Warning));

        AddAtlasGridWarning(stateProperty, container, stateLabel);
    }

    /// <summary>
    /// Adds a warning when the assigned atlas dimensions do not divide cleanly by the authored grid.
    /// </summary>
    /// <param name="stateProperty">Serialized state settings property.</param>
    /// <param name="container">Parent element receiving warning boxes.</param>
    /// <param name="stateLabel">State label used in messages.</param>
    private static void AddAtlasGridWarning(SerializedProperty stateProperty,
                                            VisualElement container,
                                            string stateLabel)
    {
        SerializedProperty atlasProperty = stateProperty.FindPropertyRelative("atlas");
        SerializedProperty columnsProperty = stateProperty.FindPropertyRelative("columns");
        SerializedProperty rowsProperty = stateProperty.FindPropertyRelative("rows");

        if (atlasProperty == null ||
            columnsProperty == null ||
            rowsProperty == null ||
            atlasProperty.objectReferenceValue == null ||
            columnsProperty.intValue <= 0 ||
            rowsProperty.intValue <= 0)
        {
            return;
        }

        Texture2D atlas = atlasProperty.objectReferenceValue as Texture2D;

        if (atlas == null)
            return;

        if (atlas.width % columnsProperty.intValue != 0 ||
            atlas.height % rowsProperty.intValue != 0)
        {
            container.Add(new HelpBox(stateLabel + " atlas dimensions should divide evenly by Columns and Rows.", HelpBoxMessageType.Warning));
        }
    }
    #endregion

    #endregion
}
