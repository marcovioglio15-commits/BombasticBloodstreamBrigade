using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit drawer that shows loading-progress options only when the parent toggle makes them useful.
/// </summary>
[CustomPropertyDrawer(typeof(GameSceneLoadingProgressSettings))]
public sealed class GameSceneLoadingProgressSettingsPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region UI
    /// <summary>
    /// Builds the Scene Manager loading-progress settings editor UI.
    /// </summary>
    /// <param name="property">Serialized GameSceneLoadingProgressSettings property.</param>
    /// <returns>Configured loading-progress settings visual tree.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        root.style.marginBottom = 6f;

        SerializedProperty showProgressProperty = property.FindPropertyRelative("showLoadingProgress");
        SerializedProperty showStatusTextProperty = property.FindPropertyRelative("showStatusText");
        PropertyField showProgressField = AddProperty(root,
                                                      property,
                                                      "showLoadingProgress",
                                                      "Enable the circular loading-progress indicator during black-screen transition phases.");
        VisualElement progressOptions = BuildProgressOptions(property);
        VisualElement statusOptions = BuildStatusOptions(property);
        root.Add(progressOptions);
        root.Add(statusOptions);
        RefreshVisibility(showProgressProperty, showStatusTextProperty, progressOptions, statusOptions);

        if (showProgressField != null)
            showProgressField.RegisterCallback<SerializedPropertyChangeEvent>(evt => RefreshVisibility(showProgressProperty, showStatusTextProperty, progressOptions, statusOptions));

        PropertyField showStatusTextField = progressOptions.Q<PropertyField>("showStatusText");

        if (showStatusTextField != null)
            showStatusTextField.RegisterCallback<SerializedPropertyChangeEvent>(evt => RefreshVisibility(showProgressProperty, showStatusTextProperty, progressOptions, statusOptions));

        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds settings that are only relevant while the loading-progress indicator is enabled.
    /// </summary>
    /// <param name="property">Serialized loading-progress settings property.</param>
    /// <returns>Container with enabled-state-dependent fields.</returns>
    private static VisualElement BuildProgressOptions(SerializedProperty property)
    {
        VisualElement container = new VisualElement();
        AddProperty(container, property, "showPercentage", "Show a percentage label in the center of the circular indicator.");
        AddProperty(container, property, "showStatusText", "Show current loading or unloading status text next to the circular indicator.");
        AddProperty(container, property, "ringColor", "Color applied to the filled segmented progress ring.");
        AddProperty(container, property, "trackColor", "Color applied to the segmented background track.");
        AddProperty(container, property, "textColor", "Color applied to percentage and status labels.");
        AddProperty(container, property, "ringSegmentCount", "Number of visual segments used by the discontinuous circular ring.");
        AddProperty(container, property, "ringSegmentGapDegrees", "Angular gap in degrees between ring segments.");
        AddProperty(container, property, "ringThickness", "Ring thickness in UI pixels.");
        AddProperty(container, property, "spinnerRotationDegreesPerSecond", "Unscaled rotation speed in degrees per second for the loading spinner root.");
        return container;
    }

    /// <summary>
    /// Builds status-text fields that are only relevant while status text is enabled.
    /// </summary>
    /// <param name="property">Serialized loading-progress settings property.</param>
    /// <returns>Container with status-text fields.</returns>
    private static VisualElement BuildStatusOptions(SerializedProperty property)
    {
        VisualElement container = new VisualElement();
        AddProperty(container, property, "loadingStatusPrefix", "Prefix used before the current scene or Addressables key while loading.");
        AddProperty(container, property, "unloadingStatusPrefix", "Prefix used before the current scene or Addressables key while unloading.");
        AddProperty(container, property, "readinessStatusText", "Text shown while waiting for DOTS and presentation readiness.");
        AddProperty(container, property, "readyStatusText", "Text shown after loading has finished and fade-in is about to start.");
        return container;
    }

    /// <summary>
    /// Adds one relative property field with an explicit tooltip.
    /// </summary>
    /// <param name="root">Parent visual element.</param>
    /// <param name="parentProperty">Serialized loading-progress settings property.</param>
    /// <param name="propertyName">Relative property name.</param>
    /// <param name="tooltip">Field tooltip.</param>
    /// <returns>Created property field, or null when the property is missing.</returns>
    private static PropertyField AddProperty(VisualElement root, SerializedProperty parentProperty, string propertyName, string tooltip)
    {
        SerializedProperty childProperty = parentProperty.FindPropertyRelative(propertyName);

        if (childProperty == null)
            return null;

        PropertyField field = new PropertyField(childProperty);
        field.name = propertyName;
        field.tooltip = tooltip;
        field.BindProperty(childProperty);
        root.Add(field);
        return field;
    }

    /// <summary>
    /// Applies dependent display state for the loading-progress fields.
    /// </summary>
    /// <param name="showProgressProperty">Toggle that controls all progress options.</param>
    /// <param name="showStatusTextProperty">Toggle that controls status text options.</param>
    /// <param name="progressOptions">Container with progress options.</param>
    /// <param name="statusOptions">Container with status text options.</param>
    private static void RefreshVisibility(SerializedProperty showProgressProperty,
                                          SerializedProperty showStatusTextProperty,
                                          VisualElement progressOptions,
                                          VisualElement statusOptions)
    {
        bool showProgress = showProgressProperty != null && showProgressProperty.boolValue;
        bool showStatusText = showProgress &&
                              showStatusTextProperty != null &&
                              showStatusTextProperty.boolValue;
        progressOptions.style.display = showProgress ? DisplayStyle.Flex : DisplayStyle.None;
        statusOptions.style.display = showStatusText ? DisplayStyle.Flex : DisplayStyle.None;
    }
    #endregion

    #endregion
}
