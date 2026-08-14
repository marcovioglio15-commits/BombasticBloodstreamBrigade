using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Draws Synchro Meter scene bindings and reports missing authored wave layers without mutating the hierarchy.
/// </summary>
[CustomPropertyDrawer(typeof(HUDComboCounterSection))]
public sealed class HUDComboCounterSectionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for the authored Synchro Meter section.
    /// </summary>
    /// <param name="property">Serialized Synchro Meter section property.</param>
    /// <returns>Root UI element used by the inspector.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        if (HUDSectionPropertyDrawerUtility.TryCreateObjectReferenceField(property,
                                                                         "Scene Synchro Meter component referenced by HUDManager.",
                                                                         out VisualElement referenceField))
            return referenceField;

        VisualElement root = new VisualElement();
        SerializedProperty isEnabledProperty = property.FindPropertyRelative("isEnabled");
        SerializedProperty rootObjectProperty = property.FindPropertyRelative("rootObject");
        SerializedProperty waveViewportProperty = property.FindPropertyRelative("waveViewport");
        SerializedProperty backgroundImageProperty = property.FindPropertyRelative("backgroundImage");
        SerializedProperty coverImageProperty = property.FindPropertyRelative("coverImage");
        SerializedProperty primaryLeadingProperty = property.FindPropertyRelative("primaryWaveLeadingImage");
        SerializedProperty primaryTrailingProperty = property.FindPropertyRelative("primaryWaveTrailingImage");
        SerializedProperty secondaryLeadingProperty = property.FindPropertyRelative("secondaryWaveLeadingImage");
        SerializedProperty secondaryTrailingProperty = property.FindPropertyRelative("secondaryWaveTrailingImage");
        SerializedProperty rankTextProperty = property.FindPropertyRelative("rankText");
        SerializedProperty valueTextProperty = property.FindPropertyRelative("valueText");
        SerializedProperty progressFillProperty = property.FindPropertyRelative("progressFillImage");
        SerializedProperty progressBackgroundProperty = property.FindPropertyRelative("progressBackgroundImage");
        SerializedProperty progressionTextProperty = property.FindPropertyRelative("progressionText");
        SerializedProperty visualModeProperty = property.FindPropertyRelative("visualMode");
        SerializedProperty showBackgroundProperty = property.FindPropertyRelative("showBackground");
        SerializedProperty showCoverProperty = property.FindPropertyRelative("showCover");
        SerializedProperty showRankTextProperty = property.FindPropertyRelative("showRankText");
        SerializedProperty showValueTextProperty = property.FindPropertyRelative("showValueText");
        SerializedProperty showProgressBarProperty = property.FindPropertyRelative("showProgressBar");

        if (isEnabledProperty == null ||
            rootObjectProperty == null ||
            waveViewportProperty == null ||
            primaryLeadingProperty == null ||
            primaryTrailingProperty == null ||
            secondaryLeadingProperty == null ||
            secondaryTrailingProperty == null ||
            progressionTextProperty == null ||
            visualModeProperty == null)
        {
            root.Add(new HelpBox("Synchro Meter section fields are missing.", HelpBoxMessageType.Warning));
            return root;
        }

        root.Add(new HelpBox("The two images in each wave pair must share the same width and touch edge-to-edge inside the masked viewport. Runtime only repositions these authored images; it never creates UI objects.", HelpBoxMessageType.Info));
        root.Add(CreateBoundField(isEnabledProperty, "Enabled"));
        root.Add(CreateBoundField(rootObjectProperty, "Root Object"));
        root.Add(CreateBoundField(waveViewportProperty, "Wave Viewport"));
        root.Add(CreateBoundField(backgroundImageProperty, "Background Image"));
        root.Add(CreateBoundField(primaryLeadingProperty, "Primary Wave Leading"));
        root.Add(CreateBoundField(primaryTrailingProperty, "Primary Wave Trailing"));
        root.Add(CreateBoundField(secondaryLeadingProperty, "Secondary Wave Leading"));
        root.Add(CreateBoundField(secondaryTrailingProperty, "Secondary Wave Trailing"));
        root.Add(CreateBoundField(coverImageProperty, "Cover Image"));
        root.Add(CreateBoundField(rankTextProperty, "Rank Text"));
        root.Add(CreateBoundField(valueTextProperty, "Value Text"));
        root.Add(CreateBoundField(progressFillProperty, "Progress Fill"));
        root.Add(CreateBoundField(progressBackgroundProperty, "Progress Background"));
        root.Add(CreateBoundField(progressionTextProperty, "Progression Text"));

        Foldout fallbackFoldout = new Foldout
        {
            text = "Scene Fallback Settings"
        };
        fallbackFoldout.tooltip = "Fallback values used before the baked HUD Manager preset becomes available.";
        AddFallbackFields(fallbackFoldout, property);
        root.Add(fallbackFoldout);

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        root.Add(warningBox);

        // Refresh warnings whenever a binding or visibility toggle changes.
        root.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshWarnings(rootObjectProperty,
                                                                                  waveViewportProperty,
                                                                                  backgroundImageProperty,
                                                                                  coverImageProperty,
                                                                                  primaryLeadingProperty,
                                                                                  primaryTrailingProperty,
                                                                                  secondaryLeadingProperty,
                                                                                  secondaryTrailingProperty,
                                                                                  rankTextProperty,
                                                                                  valueTextProperty,
                                                                                  progressFillProperty,
                                                                                  progressBackgroundProperty,
                                                                                  progressionTextProperty,
                                                                                  visualModeProperty,
                                                                                  showBackgroundProperty,
                                                                                  showCoverProperty,
                                                                                  showRankTextProperty,
                                                                                  showValueTextProperty,
                                                                                  showProgressBarProperty,
                                                                                  warningBox));
        RefreshWarnings(rootObjectProperty,
                        waveViewportProperty,
                        backgroundImageProperty,
                        coverImageProperty,
                        primaryLeadingProperty,
                        primaryTrailingProperty,
                        secondaryLeadingProperty,
                        secondaryTrailingProperty,
                        rankTextProperty,
                        valueTextProperty,
                        progressFillProperty,
                        progressBackgroundProperty,
                        progressionTextProperty,
                        visualModeProperty,
                        showBackgroundProperty,
                        showCoverProperty,
                        showRankTextProperty,
                        showValueTextProperty,
                        showProgressBarProperty,
                        warningBox);
        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds fallback theme, motion, layer, and visibility fields used before ECS settings are applied.
    /// </summary>
    /// <param name="parent">Foldout receiving fallback fields.</param>
    /// <param name="property">Serialized section property owning the fallback values.</param>
    private static void AddFallbackFields(VisualElement parent, SerializedProperty property)
    {
        string[] fieldNames =
        {
            "visualMode",
            "progressionTextFormat",
            "backgroundTint",
            "coverTint",
            "primaryWaveTint",
            "secondaryWaveTint",
            "rankTextColor",
            "valueTextColor",
            "progressionTextColor",
            "progressFillTint",
            "progressBackgroundTint",
            "showBackground",
            "showCover",
            "showRankText",
            "showValueText",
            "showProgressBar",
            "waveScrollCyclesPerSecond",
            "lowestRankPhaseOffsetNormalized",
            "highestRankPhaseOffsetNormalized",
            "phaseOffsetResponseExponent",
            "singleRankAccelerateWavesWithProgress",
            "singleRankMaximumWaveScrollCyclesPerSecond",
            "singleRankConvergenceMode",
            "singleRankInitialPhaseOffsetNormalized",
            "singleRankFinalPhaseOffsetNormalized",
            "singleRankConvergenceStartProgressPercent",
            "singleRankConvergenceEndProgressPercent",
            "singleRankConvergenceStepCount",
            "phaseTransitionDuration",
            "useUnscaledTime",
            "progressSmoothingSeconds",
            "hideWhenPlayerMissing",
            "hideWhenZeroValue",
            "hideWhenNoActiveRank",
            "fadeInDuration",
            "fadeOutDuration",
            "idleRankLabel"
        };

        // Unity supplies labels and tooltips directly from the serialized field metadata.
        for (int fieldIndex = 0; fieldIndex < fieldNames.Length; fieldIndex++)
        {
            SerializedProperty childProperty = property.FindPropertyRelative(fieldNames[fieldIndex]);

            if (childProperty != null)
                parent.Add(CreateBoundField(childProperty, childProperty.displayName));
        }
    }

    /// <summary>
    /// Creates one bound property field with the requested display label.
    /// </summary>
    /// <param name="property">Serialized property bound to the field.</param>
    /// <param name="label">Inspector label shown for the bound field.</param>
    /// <returns>Configured property field bound to the serialized property.</returns>
    private static PropertyField CreateBoundField(SerializedProperty property, string label)
    {
        PropertyField propertyField = new PropertyField(property, label);
        propertyField.BindProperty(property);
        return propertyField;
    }

    /// <summary>
    /// Rebuilds warnings for mandatory image pairs and currently enabled optional layers.
    /// </summary>
    /// <param name="rootObjectProperty">Serialized meter root object.</param>
    /// <param name="waveViewportProperty">Serialized masked wave viewport.</param>
    /// <param name="backgroundImageProperty">Serialized background image.</param>
    /// <param name="coverImageProperty">Serialized scanline cover image.</param>
    /// <param name="primaryLeadingProperty">Serialized primary leading wave image.</param>
    /// <param name="primaryTrailingProperty">Serialized primary trailing wave image.</param>
    /// <param name="secondaryLeadingProperty">Serialized secondary leading wave image.</param>
    /// <param name="secondaryTrailingProperty">Serialized secondary trailing wave image.</param>
    /// <param name="rankTextProperty">Serialized rank text.</param>
    /// <param name="valueTextProperty">Serialized value text.</param>
    /// <param name="progressFillProperty">Serialized progression fill image.</param>
    /// <param name="progressBackgroundProperty">Serialized progression track image.</param>
    /// <param name="progressionTextProperty">Serialized optional progression TMP label.</param>
    /// <param name="visualModeProperty">Serialized fallback visual-mode selection.</param>
    /// <param name="showBackgroundProperty">Serialized background visibility toggle.</param>
    /// <param name="showCoverProperty">Serialized cover visibility toggle.</param>
    /// <param name="showRankTextProperty">Serialized rank-text visibility toggle.</param>
    /// <param name="showValueTextProperty">Serialized value-text visibility toggle.</param>
    /// <param name="showProgressBarProperty">Serialized progression-bar visibility toggle.</param>
    /// <param name="warningBox">Warning help box refreshed in place.</param>
    private static void RefreshWarnings(SerializedProperty rootObjectProperty,
                                        SerializedProperty waveViewportProperty,
                                        SerializedProperty backgroundImageProperty,
                                        SerializedProperty coverImageProperty,
                                        SerializedProperty primaryLeadingProperty,
                                        SerializedProperty primaryTrailingProperty,
                                        SerializedProperty secondaryLeadingProperty,
                                        SerializedProperty secondaryTrailingProperty,
                                        SerializedProperty rankTextProperty,
                                        SerializedProperty valueTextProperty,
                                        SerializedProperty progressFillProperty,
                                        SerializedProperty progressBackgroundProperty,
                                        SerializedProperty progressionTextProperty,
                                        SerializedProperty visualModeProperty,
                                        SerializedProperty showBackgroundProperty,
                                        SerializedProperty showCoverProperty,
                                        SerializedProperty showRankTextProperty,
                                        SerializedProperty showValueTextProperty,
                                        SerializedProperty showProgressBarProperty,
                                        HelpBox warningBox)
    {
        if (warningBox == null)
            return;

        List<string> warningLines = new List<string>();
        bool usesProgressionText = visualModeProperty != null &&
                                   visualModeProperty.enumValueIndex == (int)GameHudSynchroMeterVisualMode.ProgressionText;

        if (rootObjectProperty.objectReferenceValue == null)
            warningLines.Add("Assign the Synchro Meter root object used for visibility and fading.");

        if (waveViewportProperty.objectReferenceValue == null)
            warningLines.Add("Assign the masked Wave Viewport so both seamless pairs remain clipped to the display.");

        if (primaryLeadingProperty.objectReferenceValue == null || primaryTrailingProperty.objectReferenceValue == null)
            warningLines.Add("Assign both Primary Wave images; one image cannot cover the seamless wrap boundary.");

        if (secondaryLeadingProperty.objectReferenceValue == null || secondaryTrailingProperty.objectReferenceValue == null)
            warningLines.Add("Assign both Secondary Wave images; phase convergence requires a complete seamless pair.");

        if (showBackgroundProperty.boolValue && backgroundImageProperty.objectReferenceValue == null)
            warningLines.Add("Show Background is enabled, but no background image is assigned.");

        if (showCoverProperty.boolValue && coverImageProperty.objectReferenceValue == null)
            warningLines.Add("Show Cover is enabled, but no scanline cover image is assigned.");

        if (!usesProgressionText && showRankTextProperty.boolValue && rankTextProperty.objectReferenceValue == null)
            warningLines.Add("Show Rank Text is enabled, but no TMP rank label is assigned.");

        if (!usesProgressionText && showValueTextProperty.boolValue && valueTextProperty.objectReferenceValue == null)
            warningLines.Add("Show Value Text is enabled, but no TMP value label is assigned.");

        if (!usesProgressionText &&
            showProgressBarProperty.boolValue &&
            (progressFillProperty.objectReferenceValue == null || progressBackgroundProperty.objectReferenceValue == null))
            warningLines.Add("Show Progress Bar is enabled, but its fill or background image is not assigned.");

        if (usesProgressionText && progressionTextProperty.objectReferenceValue == null)
            warningLines.Add("Progression Text mode is selected, but no TMP progression label is assigned.");

        warningBox.text = string.Join("\n", warningLines);
        warningBox.style.display = warningLines.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }
    #endregion

    #endregion
}
