using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Presents one menu-button profile while hiding controls that do not affect its selected behavior.
/// </summary>
[CustomPropertyDrawer(typeof(GameUiMenuButtonInteractionDefinition))]
public sealed class GameUiMenuButtonInteractionDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Unity Methods
    /// <summary>
    /// Creates the per-menu foldout and rebuilds only conditional motion, sprite, graphic, and text sections.
    /// </summary>
    /// <param name="property">Serialized menu-button profile being rendered.</param>
    /// <returns>Bound conditional UI Toolkit hierarchy.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        Foldout root = new Foldout();
        SerializedProperty menuKindProperty = property.FindPropertyRelative("menuKind");
        SerializedProperty enabledProperty = property.FindPropertyRelative("isEnabled");
        SerializedProperty motionModeProperty = property.FindPropertyRelative("motionMode");
        SerializedProperty hoverTransformModeProperty = property.FindPropertyRelative("hoverTransformMode");
        SerializedProperty loopHoverPulseProperty = property.FindPropertyRelative("loopHoverPulse");
        SerializedProperty overrideSpritesProperty = property.FindPropertyRelative("overrideSprites");
        SerializedProperty overrideColorsProperty = property.FindPropertyRelative("overrideGraphicColors");
        SerializedProperty overrideTextProperty = property.FindPropertyRelative("overrideTextStyle");
        VisualElement conditionalRoot = new VisualElement();
        root.text = ResolveLabel(menuKindProperty);
        AddProperty(root, menuKindProperty, "Menu Group");
        AddProperty(root, enabledProperty, "Enabled");
        root.Add(conditionalRoot);

        System.Action rebuild = () => BuildConditionalFields(conditionalRoot,
                                                              property,
                                                              enabledProperty.boolValue,
                                                              (GameUiButtonMotionMode)motionModeProperty.enumValueIndex,
                                                              (GameUiButtonHoverTransformMode)hoverTransformModeProperty.enumValueIndex,
                                                              loopHoverPulseProperty.boolValue,
                                                              overrideSpritesProperty.boolValue,
                                                              overrideColorsProperty.boolValue,
                                                              overrideTextProperty.boolValue);
        root.TrackPropertyValue(menuKindProperty, changedProperty =>
        {
            root.text = ResolveLabel(changedProperty);
            GameManagementDraftSession.MarkDirty();
        });
        root.TrackPropertyValue(enabledProperty, changedProperty =>
        {
            rebuild();
            GameManagementDraftSession.MarkDirty();
        });
        root.TrackPropertyValue(motionModeProperty, changedProperty =>
        {
            rebuild();
            GameManagementDraftSession.MarkDirty();
        });
        root.TrackPropertyValue(hoverTransformModeProperty, changedProperty =>
        {
            rebuild();
            GameManagementDraftSession.MarkDirty();
        });
        root.TrackPropertyValue(loopHoverPulseProperty, changedProperty =>
        {
            rebuild();
            GameManagementDraftSession.MarkDirty();
        });
        root.TrackPropertyValue(overrideSpritesProperty, changedProperty =>
        {
            rebuild();
            GameManagementDraftSession.MarkDirty();
        });
        root.TrackPropertyValue(overrideColorsProperty, changedProperty =>
        {
            rebuild();
            GameManagementDraftSession.MarkDirty();
        });
        root.TrackPropertyValue(overrideTextProperty, changedProperty =>
        {
            rebuild();
            GameManagementDraftSession.MarkDirty();
        });
        root.RegisterCallback<SerializedPropertyChangeEvent>(evt => GameManagementDraftSession.MarkDirty());
        rebuild();
        return root;
    }
    #endregion

    #region Conditional Fields
    /// <summary>
    /// Populates only controls relevant to the enabled profile and its selected override paths.
    /// </summary>
    /// <param name="root">Container receiving conditional fields.</param>
    /// <param name="property">Serialized menu profile.</param>
    /// <param name="isEnabled">Current profile enabled state.</param>
    /// <param name="motionMode">Selected motion path.</param>
    /// <param name="hoverTransformMode">Selected held-target or pulse hover behavior.</param>
    /// <param name="loopHoverPulse">Whether pulse cycles repeat until pointer exit.</param>
    /// <param name="overrideSprites">Whether sprite-state controls affect runtime.</param>
    /// <param name="overrideColors">Whether graphic-color controls affect runtime.</param>
    /// <param name="overrideText">Whether text-style controls affect runtime.</param>
    private static void BuildConditionalFields(VisualElement root,
                                               SerializedProperty property,
                                               bool isEnabled,
                                               GameUiButtonMotionMode motionMode,
                                               GameUiButtonHoverTransformMode hoverTransformMode,
                                               bool loopHoverPulse,
                                               bool overrideSprites,
                                               bool overrideColors,
                                               bool overrideText)
    {
        root.Clear();

        if (!isEnabled)
        {
            root.Add(new HelpBox("This profile is disabled; authored Button behavior remains unchanged.", HelpBoxMessageType.Info));
            return;
        }

        AddProperty(root, property.FindPropertyRelative("motionMode"), "Motion Mode");

        if (motionMode != GameUiButtonMotionMode.None)
        {
            AddProperty(root, property.FindPropertyRelative("transitionDurationSeconds"), "Transition Duration");
            AddProperty(root, property.FindPropertyRelative("useUnscaledTime"), "Use Unscaled Time");
        }

        if (motionMode == GameUiButtonMotionMode.ManualTransform ||
            motionMode == GameUiButtonMotionMode.ManualTransformAndClips)
            AddManualTransformFields(root, property, hoverTransformMode, loopHoverPulse);

        if (motionMode == GameUiButtonMotionMode.AnimationClips ||
            motionMode == GameUiButtonMotionMode.ManualTransformAndClips)
            AddAnimationClipFields(root, property);

        AddProperty(root, property.FindPropertyRelative("overrideSprites"), "Override Sprites");

        if (overrideSprites)
            AddSpriteFields(root, property);

        AddProperty(root, property.FindPropertyRelative("overrideGraphicColors"), "Override Graphic Colors");

        if (overrideColors)
            AddGraphicColorFields(root, property);

        AddProperty(root, property.FindPropertyRelative("overrideTextStyle"), "Override Text Style");

        if (overrideText)
            AddTextFields(root, property);
    }

    /// <summary>
    /// Adds pointer, focus, and pressed manual transform states.
    /// </summary>
    /// <param name="root">Container receiving the fields.</param>
    /// <param name="property">Serialized menu profile.</param>
    /// <param name="hoverTransformMode">Selected held-target or pulse hover behavior.</param>
    /// <param name="loopHoverPulse">Whether pulse cycles repeat until pointer exit.</param>
    private static void AddManualTransformFields(VisualElement root,
                                                 SerializedProperty property,
                                                 GameUiButtonHoverTransformMode hoverTransformMode,
                                                 bool loopHoverPulse)
    {
        //root.Add(new HelpBox("Manual position offsets use each button's current post-layout baseline. A child Transform Target Override is optional when clips must animate independently from future layout rebuilds.",
        //                     HelpBoxMessageType.Info));
        AddProperty(root, property.FindPropertyRelative("hoverTransformMode"), "Hover / Focus Transform Mode");

        if (hoverTransformMode == GameUiButtonHoverTransformMode.Pulse)
        {
            AddProperty(root, property.FindPropertyRelative("hoverPulseCycleSeconds"), "Pulse Cycle Duration");
            AddProperty(root, property.FindPropertyRelative("loopHoverPulse"), "Loop While Hovered or Focused");

            if (!loopHoverPulse)
                AddProperty(root, property.FindPropertyRelative("hoverPulseCycles"), "Pulse Cycles");
        }

        AddProperty(root, property.FindPropertyRelative("hoverScale"), "Hover / Focus Scale");
        AddProperty(root, property.FindPropertyRelative("hoverPositionOffset"), "Hover / Focus Position Offset");
        AddProperty(root, property.FindPropertyRelative("hoverRotationOffset"), "Hover / Focus Rotation Offset");
        AddProperty(root, property.FindPropertyRelative("pressedScale"), "Pressed Scale");
        AddProperty(root, property.FindPropertyRelative("pressedPositionOffset"), "Pressed Position Offset");
        AddProperty(root, property.FindPropertyRelative("pressedRotationOffset"), "Pressed Rotation Offset");
    }

    /// <summary>
    /// Adds the animation clips sampled for every selectable state.
    /// </summary>
    /// <param name="root">Container receiving the fields.</param>
    /// <param name="property">Serialized menu profile.</param>
    private static void AddAnimationClipFields(VisualElement root, SerializedProperty property)
    {
        AddProperty(root, property.FindPropertyRelative("normalClip"), "Normal Clip");
        AddProperty(root, property.FindPropertyRelative("hoverClip"), "Hover / Focus Clip");
        AddProperty(root, property.FindPropertyRelative("pressedClip"), "Pressed Clip");
        AddProperty(root, property.FindPropertyRelative("disabledClip"), "Disabled Clip");
    }

    /// <summary>
    /// Adds sprite state overrides.
    /// </summary>
    /// <param name="root">Container receiving the fields.</param>
    /// <param name="property">Serialized menu profile.</param>
    private static void AddSpriteFields(VisualElement root, SerializedProperty property)
    {
        AddProperty(root, property.FindPropertyRelative("allowEmptySprites"), "Allow Empty Sprites");
        AddProperty(root, property.FindPropertyRelative("normalSprite"), "Normal Sprite");
        AddProperty(root, property.FindPropertyRelative("hoverSprite"), "Hover / Focus Sprite");
        AddProperty(root, property.FindPropertyRelative("pressedSprite"), "Pressed Sprite");
        AddProperty(root, property.FindPropertyRelative("disabledSprite"), "Disabled Sprite");
    }

    /// <summary>
    /// Adds target-graphic color states.
    /// </summary>
    /// <param name="root">Container receiving the fields.</param>
    /// <param name="property">Serialized menu profile.</param>
    private static void AddGraphicColorFields(VisualElement root, SerializedProperty property)
    {
        AddProperty(root, property.FindPropertyRelative("normalGraphicColor"), "Normal Graphic Color");
        AddProperty(root, property.FindPropertyRelative("hoverGraphicColor"), "Hover / Focus Graphic Color");
        AddProperty(root, property.FindPropertyRelative("pressedGraphicColor"), "Pressed Graphic Color");
        AddProperty(root, property.FindPropertyRelative("disabledGraphicColor"), "Disabled Graphic Color");
    }

    /// <summary>
    /// Adds TMP font, size, style, and color states.
    /// </summary>
    /// <param name="root">Container receiving the fields.</param>
    /// <param name="property">Serialized menu profile.</param>
    private static void AddTextFields(VisualElement root, SerializedProperty property)
    {
        AddProperty(root, property.FindPropertyRelative("normalFont"), "Normal Font");
        AddProperty(root, property.FindPropertyRelative("emphasizedFont"), "Emphasized Font");
        AddProperty(root, property.FindPropertyRelative("normalFontSize"), "Normal Font Size");
        AddProperty(root, property.FindPropertyRelative("emphasizedFontSize"), "Emphasized Font Size");
        AddProperty(root, property.FindPropertyRelative("normalFontStyle"), "Normal Font Style");
        AddProperty(root, property.FindPropertyRelative("emphasizedFontStyle"), "Emphasized Font Style");
        AddProperty(root, property.FindPropertyRelative("normalTextColor"), "Normal Text Color");
        AddProperty(root, property.FindPropertyRelative("hoverTextColor"), "Hover / Focus Text Color");
        AddProperty(root, property.FindPropertyRelative("pressedTextColor"), "Pressed Text Color");
        AddProperty(root, property.FindPropertyRelative("disabledTextColor"), "Disabled Text Color");
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Adds one bound property while preserving its serialized tooltip.
    /// </summary>
    /// <param name="root">Container receiving the field.</param>
    /// <param name="property">Serialized property to render.</param>
    /// <param name="label">Visible field label.</param>
    private static void AddProperty(VisualElement root, SerializedProperty property, string label)
    {
        if (property == null)
            return;

        PropertyField field = new PropertyField(property, label);
        field.tooltip = property.tooltip;
        field.BindProperty(property);
        root.Add(field);
    }

    /// <summary>
    /// Resolves the profile foldout title from its concrete menu group.
    /// </summary>
    /// <param name="menuKindProperty">Serialized menu kind enum.</param>
    /// <returns>Readable profile title.</returns>
    private static string ResolveLabel(SerializedProperty menuKindProperty)
    {
        if (menuKindProperty == null)
            return "Menu Button Profile";

        return menuKindProperty.enumDisplayNames[menuKindProperty.enumValueIndex];
    }
    #endregion

    #endregion
}
