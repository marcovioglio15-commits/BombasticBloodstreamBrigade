using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds common serialized fields used by Excel transfer master panel sections.
/// </summary>
internal static class ExcelDataTransferMasterPanelFieldUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds one bound property field and marks the draft session dirty when edited.
    /// </summary>
    /// <param name="panel">Owning master panel that may need its preset list refreshed.</param>
    /// <param name="parent">Parent section.</param>
    /// <param name="serializedObject">Serialized object that owns the property.</param>
    /// <param name="propertyName">Serialized property name.</param>
    /// <param name="label">Field label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    /// <param name="refreshPresetList">True when sidebar labels should refresh after edits.</param>
    public static void AddPropertyField(ExcelDataTransferMasterPanel panel,
                                        VisualElement parent,
                                        SerializedObject serializedObject,
                                        string propertyName,
                                        string label,
                                        string tooltip,
                                        bool refreshPresetList)
    {
        if (parent == null || serializedObject == null)
            return;

        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        PropertyField propertyField = new PropertyField(property, label);
        propertyField.tooltip = tooltip;
        propertyField.BindProperty(property);
        propertyField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            ExcelDataTransferDraftSession.MarkDirty();

            if (refreshPresetList && panel != null)
                panel.RefreshPresetList();
        });
        parent.Add(propertyField);
    }

    /// <summary>
    /// Adds one disabled property field for read-only metadata.
    /// </summary>
    /// <param name="parent">Parent section.</param>
    /// <param name="serializedObject">Serialized object that owns the property.</param>
    /// <param name="propertyName">Serialized property name.</param>
    /// <param name="label">Field label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    public static void AddDisabledPropertyField(VisualElement parent,
                                                SerializedObject serializedObject,
                                                string propertyName,
                                                string label,
                                                string tooltip)
    {
        if (parent == null || serializedObject == null)
            return;

        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        PropertyField propertyField = new PropertyField(property, label);
        propertyField.tooltip = tooltip;
        propertyField.BindProperty(property);
        propertyField.SetEnabled(false);
        parent.Add(propertyField);
    }

    /// <summary>
    /// Adds shared domain toggle fields when present on import or export presets.
    /// </summary>
    /// <param name="panel">Owning master panel.</param>
    /// <param name="parent">Parent section.</param>
    /// <param name="serializedObject">Serialized preset object.</param>
    public static void AddDomainFields(ExcelDataTransferMasterPanel panel,
                                       VisualElement parent,
                                       SerializedObject serializedObject)
    {
        AddPropertyField(panel, parent, serializedObject, "includePlayerData", "Include Player Data", "Allow Player Management Tool data.", false);
        AddPropertyField(panel, parent, serializedObject, "includeEnemyData", "Include Enemy Data", "Allow Enemy Management Tool data.", false);
        AddPropertyField(panel, parent, serializedObject, "includeGameData", "Include Game Data", "Allow Game Management Tool data.", false);
        AddPropertyField(panel, parent, serializedObject, "includeWaveData", "Include Wave Data", "Allow EnemyWavePreset wave data.", false);
        AddPropertyField(panel, parent, serializedObject, "includeConcreteListElements", "Include Concrete List Elements", "Allow individual list elements to be imported.", false);
    }

    /// <summary>
    /// Adds a workbook path profile field and refreshes the owning section when Custom Path visibility changes.
    /// </summary>
    /// <param name="panel">Owning master panel refreshed after profile edits.</param>
    /// <param name="parent">Parent section.</param>
    /// <param name="serializedObject">Serialized import or export preset.</param>
    /// <param name="propertyName">Workbook path profile property name.</param>
    /// <param name="label">Visible field label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    public static void AddWorkbookProfileField(ExcelDataTransferMasterPanel panel,
                                               VisualElement parent,
                                               SerializedObject serializedObject,
                                               string propertyName,
                                               string label,
                                               string tooltip)
    {
        if (parent == null || serializedObject == null)
            return;

        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        PropertyField propertyField = new PropertyField(property, label);
        propertyField.tooltip = tooltip;
        propertyField.BindProperty(property);
        propertyField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            ExcelDataTransferDraftSession.MarkDirty();

            if (panel != null)
                panel.ScheduleActiveDetailsRefresh();
        });
        parent.Add(propertyField);
    }

    /// <summary>
    /// Adds a custom workbook path field only when the selected path profile requires a manual override.
    /// </summary>
    /// <param name="panel">Owning master panel used to mark the draft when the field changes.</param>
    /// <param name="parent">Parent section.</param>
    /// <param name="serializedObject">Serialized import or export preset.</param>
    /// <param name="profilePropertyName">Workbook profile property name.</param>
    /// <param name="pathPropertyName">Custom path property name.</param>
    /// <param name="label">Custom path field label.</param>
    /// <param name="tooltip">Custom path field tooltip.</param>
    public static void AddCustomWorkbookPathFieldIfNeeded(ExcelDataTransferMasterPanel panel,
                                                          VisualElement parent,
                                                          SerializedObject serializedObject,
                                                          string profilePropertyName,
                                                          string pathPropertyName,
                                                          string label,
                                                          string tooltip)
    {
        if (parent == null || serializedObject == null)
            return;

        SerializedProperty profileProperty = serializedObject.FindProperty(profilePropertyName);

        if (profileProperty == null ||
            profileProperty.enumValueIndex != (int)ExcelDataWorkbookPathProfile.CustomPath)
            return;

        AddPropertyField(panel, parent, serializedObject, pathPropertyName, label, tooltip, false);
    }
    #endregion

    #endregion
}
