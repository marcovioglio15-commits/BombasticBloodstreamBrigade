using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit drawer that presents fade timing as load coverage plus a small post-readiness bonus.
/// </summary>
[CustomPropertyDrawer(typeof(GameSceneFadeSettings))]
public sealed class GameSceneFadeSettingsPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region UI
    /// <summary>
    /// Builds the Scene Manager fade settings editor UI.
    /// </summary>
    /// <param name="property">Serialized GameSceneFadeSettings property.</param>
    /// <returns>Configured fade settings visual tree.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        root.style.marginBottom = 6f;
        AddProperty(root,
                    property,
                    "fadeColor",
                    "Single transition pigment. Covered pixels use this RGB value directly, with black as the default.");
        SerializedProperty visualStyleProperty = AddProperty(root,
                                                              property,
                                                              "visualStyle",
                                                              "Selects gradient coverage or clustered aerosol deposition.");
        VisualElement directionalSettings = new VisualElement();
        directionalSettings.style.marginLeft = 12f;
        root.Add(directionalSettings);
        AddProperty(directionalSettings,
                    property,
                    "wipeDirection",
                    "Fallback direction used before a portal-directed transition supplies its traversal direction.");
        AddProperty(directionalSettings,
                    property,
                    "directionalEdgeSoftness",
                    "Normalized half-width of the soft gradient boundary. Larger values spread the transition over more of the screen.");
        AddProperty(directionalSettings,
                    property,
                    "directionalNoiseStrength",
                    "Maximum procedural displacement of the boundary. Zero keeps the gradient perfectly straight.");
        AddProperty(directionalSettings,
                    property,
                    "directionalNoiseScale",
                    "Spatial frequency of the procedural variation. Larger values create smaller boundary details.");
        VisualElement paintSettings = new VisualElement();
        paintSettings.style.marginLeft = 12f;
        root.Add(paintSettings);
        AddProperty(paintSettings,
                    property,
                    "wipeDirection",
                    "Fallback direction used before a portal-directed transition supplies its traversal direction.");
        AddProperty(paintSettings,
                    property,
                    "paintEdgeSoftness",
                    "Normalized antialiasing width around newly deposited pigment.");
        AddProperty(paintSettings,
                    property,
                    "paintNoiseStrength",
                    "Maximum local arrival-time variation between overlapping aerosol deposits.");
        AddProperty(paintSettings,
                    property,
                    "paintNoiseScale",
                    "Scale of the repeated deposit clusters across the transition surface.");
        AddProperty(paintSettings,
                    property,
                    "paintBristleStrength",
                    "Strength of fine aerosol breakup and sparse satellite droplets.");
        AddProperty(paintSettings,
                    property,
                    "paintBristleScale",
                    "Spatial density of fine aerosol mist around active deposit edges.");
        AddProperty(root, property, "easing", "Interpolation applied to transition progress before shader evaluation.");
        AddProperty(root, property, "fadeOutSeconds", "Seconds used to fade from transparent to fully opaque before loading starts.");
        AddProperty(root, property, "postLoadReadyExtraSeconds", "Small extra complete-coverage time after Unity scene loading, DOTS SubScene streaming and presentation readiness have completed.");
        AddProperty(root, property, "fadeInSeconds", "Seconds used to remove complete paint coverage after readiness and the configured post-load hold.");
        AddProperty(root, property, "lockGameplayInput", "Blocks gameplay by using the transition time-scale lock path while the fade is active.");
        AddProperty(root, property, "setTimeScaleDuringTransition", "Sets Time.timeScale to zero while a transition is active, then restores the previous value.");
        RefreshStyleVisibility();

        if (visualStyleProperty != null)
            root.TrackPropertyValue(visualStyleProperty, changedProperty => RefreshStyleVisibility());

        return root;

        void RefreshStyleVisibility()
        {
            bool usesPaint = visualStyleProperty != null &&
                             visualStyleProperty.enumValueIndex == (int)GameSceneFadeVisualStyle.Paint;
            directionalSettings.style.display = !usesPaint
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            paintSettings.style.display = usesPaint
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds one relative property field with an explicit tooltip.
    /// </summary>
    /// <param name="root">Parent visual element.</param>
    /// <param name="parentProperty">Serialized fade settings property.</param>
    /// <param name="propertyName">Relative property name.</param>
    /// <param name="tooltip">Field tooltip.</param>
    private static SerializedProperty AddProperty(VisualElement root,
                                                  SerializedProperty parentProperty,
                                                  string propertyName,
                                                  string tooltip)
    {
        SerializedProperty childProperty = parentProperty.FindPropertyRelative(propertyName);

        if (childProperty == null)
            return null;

        PropertyField field = new PropertyField(childProperty);
        field.tooltip = tooltip;
        field.BindProperty(childProperty);
        root.Add(field);
        return childProperty;
    }
    #endregion

    #endregion
}
