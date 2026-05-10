using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit drawer that presents fade timing as load coverage plus a small post-readiness bonus.
/// /params None.
/// /returns None.
/// </summary>
[CustomPropertyDrawer(typeof(GameSceneFadeSettings))]
public sealed class GameSceneFadeSettingsPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region UI
    /// <summary>
    /// Builds the Scene Manager fade settings editor UI.
    /// /params property Serialized GameSceneFadeSettings property.
    /// /returns Configured fade settings visual tree.
    /// </summary>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        root.style.marginBottom = 6f;
        AddProperty(root, property, "fadeColor", "Color used by the full-screen transition overlay.");
        AddProperty(root, property, "fadeOutSeconds", "Seconds used to fade from transparent to fully opaque before loading starts.");
        AddProperty(root, property, "postLoadReadyExtraSeconds", "Small extra black time after Unity scene loading, DOTS SubScene streaming and presentation readiness have completed.");
        AddProperty(root, property, "fadeInSeconds", "Seconds used to fade from fully opaque back to transparent after readiness and extra black time.");
        AddProperty(root, property, "lockGameplayInput", "Blocks gameplay by using the transition time-scale lock path while the fade is active.");
        AddProperty(root, property, "setTimeScaleDuringTransition", "Sets Time.timeScale to zero while a transition is active, then restores the previous value.");
        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds one relative property field with an explicit tooltip.
    /// /params root Parent visual element.
    /// /params parentProperty Serialized fade settings property.
    /// /params propertyName Relative property name.
    /// /params tooltip Field tooltip.
    /// /returns None.
    /// </summary>
    private static void AddProperty(VisualElement root, SerializedProperty parentProperty, string propertyName, string tooltip)
    {
        SerializedProperty childProperty = parentProperty.FindPropertyRelative(propertyName);

        if (childProperty == null)
            return;

        PropertyField field = new PropertyField(childProperty);
        field.tooltip = tooltip;
        field.BindProperty(childProperty);
        root.Add(field);
    }
    #endregion

    #endregion
}
