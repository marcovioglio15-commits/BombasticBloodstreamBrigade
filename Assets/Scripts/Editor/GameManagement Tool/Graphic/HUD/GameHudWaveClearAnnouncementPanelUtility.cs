using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using static GameHudManagerSupplementalPanelUtility;

/// <summary>
/// Builds the conditional Game Management Tool controls for room-clear announcement presentation.
/// </summary>
public static class GameHudWaveClearAnnouncementPanelUtility
{
    #region Methods

    #region Build Methods
    /// <summary>
    /// Builds standard and terminal-Boss room-clear presentation with context-sensitive timing and audio controls.
    /// </summary>
    /// <param name="root">HUD details root receiving announcement controls.</param>
    /// <param name="serializedObject">Serialized HUD preset containing announcement settings.</param>
    public static void Build(VisualElement root, SerializedObject serializedObject)
    {
        string prefix = "waveClearAnnouncementSettings.";
        Foldout availability = CreateFoldout("Availability And Content",
                                             "Controls whether authoritative room completions present an announcement and which standard text is displayed.");
        PropertyField enabledField = AddProperty(availability, serializedObject, prefix + "isEnabled", "Enabled");
        AddProperty(availability, serializedObject, prefix + "content", "Content");
        root.Add(availability);

        VisualElement enabledOptions = new VisualElement();
        Foldout motion = CreateFoldout("Motion",
                                       "Selects the presentation family and its direction, timing, and time source.");
        PropertyField presentationModeField = AddProperty(motion,
                                                          serializedObject,
                                                          prefix + "presentationMode",
                                                          "Presentation Mode");
        AddProperty(motion, serializedObject, prefix + "direction", "Direction");
        VisualElement traversalOptions = new VisualElement();
        AddProperty(traversalOptions, serializedObject, prefix + "traversalDurationSeconds", "Traversal Duration");
        AddProperty(traversalOptions, serializedObject, prefix + "easing", "Easing");
        PropertyField pauseField = AddProperty(traversalOptions,
                                               serializedObject,
                                               prefix + "pauseAtCenter",
                                               "Pause At Center");
        PropertyField holdField = AddProperty(traversalOptions,
                                              serializedObject,
                                              prefix + "centerHoldDurationSeconds",
                                              "Center Hold Duration");
        GameHudManagerPresetsPanelUtility.TrackConditionalVisibility(pauseField,
                                                                    holdField,
                                                                    serializedObject,
                                                                    prefix + "pauseAtCenter",
                                                                    true);
        motion.Add(traversalOptions);
        VisualElement paintTimingOptions = new VisualElement();
        AddProperty(paintTimingOptions, serializedObject, prefix + "paintExitDirection", "Removal Direction");
        AddProperty(paintTimingOptions, serializedObject, prefix + "paintRevealDurationSeconds", "Reveal Duration");
        AddProperty(paintTimingOptions, serializedObject, prefix + "paintHoldDurationSeconds", "Hold Duration");
        AddProperty(paintTimingOptions, serializedObject, prefix + "paintFadeOutDurationSeconds", "Removal Duration");
        motion.Add(paintTimingOptions);
        AddProperty(motion, serializedObject, prefix + "useUnscaledTime", "Use Unscaled Time");
        TrackPresentationModeVisibility(presentationModeField,
                                        traversalOptions,
                                        paintTimingOptions,
                                        serializedObject,
                                        prefix + "presentationMode");
        enabledOptions.Add(motion);

        Foldout standardAudio = CreateFoldout("Standard Audio",
                                              "Optionally requests one stable Audio Manager event with standard room-clear messages.");
        PropertyField playAudioField = AddProperty(standardAudio,
                                                   serializedObject,
                                                   prefix + "playAudioEvent",
                                                   "Play Audio Event");
        PropertyField audioEventField = AddProperty(standardAudio,
                                                    serializedObject,
                                                    prefix + "audioEventId",
                                                    "Audio Event");
        GameHudManagerPresetsPanelUtility.TrackConditionalVisibility(playAudioField,
                                                                    audioEventField,
                                                                    serializedObject,
                                                                    prefix + "playAudioEvent",
                                                                    true);
        enabledOptions.Add(standardAudio);

        Foldout finalWave = CreateFoldout("Terminal Boss Room",
                                          "Overrides content, motion timing, and audio when the final Boss room is cleared.");
        PropertyField finalOverrideField = AddProperty(finalWave,
                                                       serializedObject,
                                                       prefix + "useFinalWaveOverride",
                                                       "Use Final Wave Override");
        VisualElement finalOptions = new VisualElement();
        AddProperty(finalOptions, serializedObject, prefix + "finalWaveContent", "Content");
        PropertyField finalPresentationModeField = AddProperty(finalOptions,
                                                               serializedObject,
                                                               prefix + "finalWavePresentationMode",
                                                               "Presentation Mode");
        AddProperty(finalOptions, serializedObject, prefix + "finalWaveDirection", "Direction");
        VisualElement finalTraversalOptions = new VisualElement();
        AddProperty(finalTraversalOptions,
                    serializedObject,
                    prefix + "finalWaveTraversalDurationSeconds",
                    "Traversal Duration");
        AddProperty(finalTraversalOptions, serializedObject, prefix + "finalWaveEasing", "Easing");
        PropertyField finalPauseField = AddProperty(finalTraversalOptions,
                                                    serializedObject,
                                                    prefix + "finalWavePauseAtCenter",
                                                    "Pause At Center");
        PropertyField finalHoldField = AddProperty(finalTraversalOptions,
                                                   serializedObject,
                                                   prefix + "finalWaveCenterHoldDurationSeconds",
                                                   "Center Hold Duration");
        GameHudManagerPresetsPanelUtility.TrackConditionalVisibility(finalPauseField,
                                                                    finalHoldField,
                                                                    serializedObject,
                                                                    prefix + "finalWavePauseAtCenter",
                                                                    true);
        finalOptions.Add(finalTraversalOptions);
        VisualElement finalPaintTimingOptions = new VisualElement();
        AddProperty(finalPaintTimingOptions,
                    serializedObject,
                    prefix + "finalWavePaintExitDirection",
                    "Removal Direction");
        AddProperty(finalPaintTimingOptions,
                    serializedObject,
                    prefix + "finalWavePaintRevealDurationSeconds",
                    "Reveal Duration");
        AddProperty(finalPaintTimingOptions,
                    serializedObject,
                    prefix + "finalWavePaintHoldDurationSeconds",
                    "Hold Duration");
        AddProperty(finalPaintTimingOptions,
                    serializedObject,
                    prefix + "finalWavePaintFadeOutDurationSeconds",
                    "Removal Duration");
        finalOptions.Add(finalPaintTimingOptions);
        TrackPresentationModeVisibility(finalPresentationModeField,
                                        finalTraversalOptions,
                                        finalPaintTimingOptions,
                                        serializedObject,
                                        prefix + "finalWavePresentationMode");
        PropertyField finalAudioField = AddProperty(finalOptions,
                                                    serializedObject,
                                                    prefix + "playFinalWaveAudioEvent",
                                                    "Play Audio Event");
        PropertyField finalAudioEventField = AddProperty(finalOptions,
                                                         serializedObject,
                                                         prefix + "finalWaveAudioEventId",
                                                         "Audio Event");
        GameHudManagerPresetsPanelUtility.TrackConditionalVisibility(finalAudioField,
                                                                    finalAudioEventField,
                                                                    serializedObject,
                                                                    prefix + "playFinalWaveAudioEvent",
                                                                    true);
        finalWave.Add(finalOptions);
        GameHudManagerPresetsPanelUtility.TrackConditionalVisibility(finalOverrideField,
                                                                    finalOptions,
                                                                    serializedObject,
                                                                    prefix + "useFinalWaveOverride",
                                                                    true);
        enabledOptions.Add(finalWave);

        Foldout placement = CreateFoldout("Placement", "Controls the vertical path and fully off-screen travel margin.");
        AddProperty(placement,
                    serializedObject,
                    prefix + "verticalPositionNormalized",
                    "Vertical Screen Position");
        PropertyField offscreenPaddingField = AddProperty(placement,
                                                          serializedObject,
                                                          prefix + "horizontalOffscreenPadding",
                                                          "Off-Screen Padding");
        TrackModeUsageVisibility(presentationModeField,
                                 finalPresentationModeField,
                                 finalOverrideField,
                                 offscreenPaddingField,
                                 serializedObject,
                                 prefix,
                                 GameHudWaveClearAnnouncementPresentationMode.Traversal);
        enabledOptions.Add(placement);

        Foldout paintStyle = CreateFoldout("Aerosol Paint Style",
                                           "Controls the preauthored splatter silhouette and timed aerosol deposition.");
        AddProperty(paintStyle, serializedObject, prefix + "paintBackgroundSprite", "Background Sprite");
        AddProperty(paintStyle, serializedObject, prefix + "paintBackgroundColor", "Background Color");
        AddProperty(paintStyle, serializedObject, prefix + "paintBackgroundPadding", "Background Padding");
        AddProperty(paintStyle, serializedObject, prefix + "paintEdgeSoftness", "Deposit Softness");
        AddProperty(paintStyle, serializedObject, prefix + "paintNoiseStrength", "Deposit Variation");
        AddProperty(paintStyle, serializedObject, prefix + "paintNoiseScale", "Deposit Scale");
        AddProperty(paintStyle, serializedObject, prefix + "paintBristleStrength", "Mist Strength");
        AddProperty(paintStyle, serializedObject, prefix + "paintBristleScale", "Mist Density");
        TrackModeUsageVisibility(presentationModeField,
                                 finalPresentationModeField,
                                 finalOverrideField,
                                 paintStyle,
                                 serializedObject,
                                 prefix,
                                 GameHudWaveClearAnnouncementPresentationMode.PaintReveal);
        enabledOptions.Add(paintStyle);

        Foldout typography = CreateFoldout("Style", "Controls the preauthored announcement text presentation.");
        AddProperty(typography, serializedObject, prefix + "font", "Font");
        AddProperty(typography, serializedObject, prefix + "fontSize", "Font Size");
        AddProperty(typography, serializedObject, prefix + "fontStyle", "Font Style");
        AddProperty(typography, serializedObject, prefix + "color", "Color");
        enabledOptions.Add(typography);
        root.Add(enabledOptions);
        GameHudManagerPresetsPanelUtility.TrackConditionalVisibility(enabledField,
                                                                    enabledOptions,
                                                                    serializedObject,
                                                                    prefix + "isEnabled",
                                                                    true);
    }
    #endregion

    #region Visibility Methods
    /// <summary>
    /// Shows the timing group that matches one standard or terminal announcement presentation mode.
    /// </summary>
    /// <param name="driverField">Enum field that triggers visibility refreshes.</param>
    /// <param name="traversalRoot">Traversal-only timing controls.</param>
    /// <param name="paintRoot">Paint-only timing controls.</param>
    /// <param name="serializedObject">Serialized HUD preset storing the enum.</param>
    /// <param name="propertyPath">Serialized presentation-mode path.</param>
    private static void TrackPresentationModeVisibility(PropertyField driverField,
                                                        VisualElement traversalRoot,
                                                        VisualElement paintRoot,
                                                        SerializedObject serializedObject,
                                                        string propertyPath)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyPath);

        if (driverField == null || property == null)
            return;

        System.Action refresh = () =>
        {
            bool usesPaint = property.enumValueIndex ==
                             (int)GameHudWaveClearAnnouncementPresentationMode.PaintReveal;
            traversalRoot.style.display = usesPaint ? DisplayStyle.None : DisplayStyle.Flex;
            paintRoot.style.display = usesPaint ? DisplayStyle.Flex : DisplayStyle.None;
        };
        driverField.RegisterCallback<SerializedPropertyChangeEvent>(evt => refresh.Invoke());
        refresh.Invoke();
    }

    /// <summary>
    /// Shows shared controls only when the standard or enabled terminal override uses the requested mode.
    /// </summary>
    /// <param name="standardModeField">Standard presentation mode field.</param>
    /// <param name="finalModeField">Terminal override presentation mode field.</param>
    /// <param name="finalOverrideField">Terminal override toggle field.</param>
    /// <param name="targetRoot">Shared control or group whose visibility is updated.</param>
    /// <param name="serializedObject">Serialized HUD preset containing the related fields.</param>
    /// <param name="prefix">Serialized announcement settings prefix.</param>
    /// <param name="mode">Presentation mode that makes the target useful.</param>
    private static void TrackModeUsageVisibility(PropertyField standardModeField,
                                                 PropertyField finalModeField,
                                                 PropertyField finalOverrideField,
                                                 VisualElement targetRoot,
                                                 SerializedObject serializedObject,
                                                 string prefix,
                                                 GameHudWaveClearAnnouncementPresentationMode mode)
    {
        SerializedProperty standardMode = serializedObject.FindProperty(prefix + "presentationMode");
        SerializedProperty finalMode = serializedObject.FindProperty(prefix + "finalWavePresentationMode");
        SerializedProperty finalOverride = serializedObject.FindProperty(prefix + "useFinalWaveOverride");

        if (standardMode == null || finalMode == null || finalOverride == null || targetRoot == null)
            return;

        System.Action refresh = () =>
        {
            bool standardMatches = standardMode.enumValueIndex == (int)mode;
            bool finalMatches = finalOverride.boolValue && finalMode.enumValueIndex == (int)mode;
            targetRoot.style.display = standardMatches || finalMatches
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        };
        standardModeField?.RegisterCallback<SerializedPropertyChangeEvent>(evt => refresh.Invoke());
        finalModeField?.RegisterCallback<SerializedPropertyChangeEvent>(evt => refresh.Invoke());
        finalOverrideField?.RegisterCallback<SerializedPropertyChangeEvent>(evt => refresh.Invoke());
        refresh.Invoke();
    }

    #endregion

    #endregion
}
