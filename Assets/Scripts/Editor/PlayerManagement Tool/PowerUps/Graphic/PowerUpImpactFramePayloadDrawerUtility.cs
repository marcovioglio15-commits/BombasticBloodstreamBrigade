using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds the scaling-aware Player Management Tool UI for Impact Frame power-up payloads.
/// </summary>
public static class PowerUpImpactFramePayloadDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the Impact Frame payload editor with context-sensitive duration fields and coherent validation warnings.
    /// </summary>
    /// <param name="payloadContainer">Container receiving the payload controls.</param>
    /// <param name="impactFramePayloadProperty">Serialized Impact Frame payload property.</param>
    public static void BuildImpactFramePayloadUi(VisualElement payloadContainer, SerializedProperty impactFramePayloadProperty)
    {
        if (payloadContainer == null || impactFramePayloadProperty == null)
            return;

        SerializedProperty durationModeProperty = impactFramePayloadProperty.FindPropertyRelative("durationMode");
        SerializedProperty durationFramesProperty = impactFramePayloadProperty.FindPropertyRelative("durationFrames");
        SerializedProperty referenceFrameRateProperty = impactFramePayloadProperty.FindPropertyRelative("referenceFrameRate");
        SerializedProperty maximumUnscaledDurationSecondsProperty = impactFramePayloadProperty.FindPropertyRelative("maximumUnscaledDurationSeconds");
        SerializedProperty easeInUnscaledSecondsProperty = impactFramePayloadProperty.FindPropertyRelative("easeInUnscaledSeconds");
        SerializedProperty easeOutUnscaledSecondsProperty = impactFramePayloadProperty.FindPropertyRelative("easeOutUnscaledSeconds");
        SerializedProperty easingModeProperty = impactFramePayloadProperty.FindPropertyRelative("easingMode");
        SerializedProperty timeSlowdownPercentProperty = impactFramePayloadProperty.FindPropertyRelative("timeSlowdownPercent");
        SerializedProperty refreshOnShorterRequestProperty = impactFramePayloadProperty.FindPropertyRelative("refreshOnShorterRequest");
        SerializedProperty overlayIntensityProperty = impactFramePayloadProperty.FindPropertyRelative("overlayIntensity");
        SerializedProperty filterTintProperty = impactFramePayloadProperty.FindPropertyRelative("filterTint");
        SerializedProperty desaturationAmountProperty = impactFramePayloadProperty.FindPropertyRelative("desaturationAmount");
        SerializedProperty vignetteIntensityProperty = impactFramePayloadProperty.FindPropertyRelative("vignetteIntensity");
        SerializedProperty vignetteSoftnessProperty = impactFramePayloadProperty.FindPropertyRelative("vignetteSoftness");
        SerializedProperty vignetteExtentProperty = impactFramePayloadProperty.FindPropertyRelative("vignetteExtent");
        SerializedProperty vignetteTintProperty = impactFramePayloadProperty.FindPropertyRelative("vignetteTint");
        SerializedProperty chromaticAberrationProperty = impactFramePayloadProperty.FindPropertyRelative("chromaticAberration");
        SerializedProperty scanlineIntensityProperty = impactFramePayloadProperty.FindPropertyRelative("scanlineIntensity");
        SerializedProperty scanlineFrequencyProperty = impactFramePayloadProperty.FindPropertyRelative("scanlineFrequency");
        SerializedProperty flashIntensityProperty = impactFramePayloadProperty.FindPropertyRelative("flashIntensity");
        SerializedProperty radialDistortionProperty = impactFramePayloadProperty.FindPropertyRelative("radialDistortion");
        SerializedProperty shockwaveIntensityProperty = impactFramePayloadProperty.FindPropertyRelative("shockwaveIntensity");
        SerializedProperty shockwaveRadiusProperty = impactFramePayloadProperty.FindPropertyRelative("shockwaveRadius");
        SerializedProperty shockwaveThicknessProperty = impactFramePayloadProperty.FindPropertyRelative("shockwaveThickness");
        SerializedProperty zoomPunchIntensityProperty = impactFramePayloadProperty.FindPropertyRelative("zoomPunchIntensity");
        SerializedProperty invertIntensityProperty = impactFramePayloadProperty.FindPropertyRelative("invertIntensity");
        SerializedProperty posterizeIntensityProperty = impactFramePayloadProperty.FindPropertyRelative("posterizeIntensity");
        SerializedProperty posterizeStepsProperty = impactFramePayloadProperty.FindPropertyRelative("posterizeSteps");
        SerializedProperty edgeInkIntensityProperty = impactFramePayloadProperty.FindPropertyRelative("edgeInkIntensity");
        SerializedProperty screenTearIntensityProperty = impactFramePayloadProperty.FindPropertyRelative("screenTearIntensity");
        SerializedProperty screenTearFrequencyProperty = impactFramePayloadProperty.FindPropertyRelative("screenTearFrequency");
        SerializedProperty paletteFlashIntensityProperty = impactFramePayloadProperty.FindPropertyRelative("paletteFlashIntensity");
        SerializedProperty paletteFlashTintProperty = impactFramePayloadProperty.FindPropertyRelative("paletteFlashTint");

        if (durationModeProperty == null ||
            durationFramesProperty == null ||
            referenceFrameRateProperty == null ||
            maximumUnscaledDurationSecondsProperty == null ||
            easeInUnscaledSecondsProperty == null ||
            easeOutUnscaledSecondsProperty == null ||
            easingModeProperty == null ||
            timeSlowdownPercentProperty == null ||
            refreshOnShorterRequestProperty == null ||
            overlayIntensityProperty == null ||
            filterTintProperty == null ||
            desaturationAmountProperty == null ||
            vignetteIntensityProperty == null ||
            vignetteSoftnessProperty == null ||
            vignetteExtentProperty == null ||
            vignetteTintProperty == null ||
            chromaticAberrationProperty == null ||
            scanlineIntensityProperty == null ||
            scanlineFrequencyProperty == null ||
            flashIntensityProperty == null ||
            radialDistortionProperty == null ||
            shockwaveIntensityProperty == null ||
            shockwaveRadiusProperty == null ||
            shockwaveThicknessProperty == null ||
            zoomPunchIntensityProperty == null ||
            invertIntensityProperty == null ||
            posterizeIntensityProperty == null ||
            posterizeStepsProperty == null ||
            edgeInkIntensityProperty == null ||
            screenTearIntensityProperty == null ||
            screenTearFrequencyProperty == null ||
            paletteFlashIntensityProperty == null ||
            paletteFlashTintProperty == null)
        {
            HelpBox errorBox = new HelpBox("Impact Frame payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        Foldout timingFoldout = CreatePayloadFoldout("Timing", true);
        Foldout timeScaleFoldout = CreatePayloadFoldout("Time Scale", true);
        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        payloadContainer.Add(timingFoldout);
        payloadContainer.Add(timeScaleFoldout);
        PowerUpImpactFrameScreenEffectsDrawerUtility.Build(payloadContainer, impactFramePayloadProperty, true);
        PowerUpImpactFrameExtendedPayloadDrawerUtility.Build(payloadContainer, impactFramePayloadProperty);
        payloadContainer.Add(warningBox);

        VisualElement durationModeField = PowerUpModuleDefinitionPayloadDrawerUtility.AddField(timingFoldout, durationModeProperty, "Duration Mode");
        VisualElement durationFramesField = PowerUpModuleDefinitionPayloadDrawerUtility.AddField(timingFoldout, durationFramesProperty, "Duration Frames");
        VisualElement referenceFrameRateField = PowerUpModuleDefinitionPayloadDrawerUtility.AddField(timingFoldout, referenceFrameRateProperty, "Reference Frame Rate");
        VisualElement maximumUnscaledDurationSecondsField = PowerUpModuleDefinitionPayloadDrawerUtility.AddField(timingFoldout, maximumUnscaledDurationSecondsProperty, "Maximum Unscaled Duration Seconds");
        VisualElement easeInUnscaledSecondsField = PowerUpModuleDefinitionPayloadDrawerUtility.AddField(timingFoldout, easeInUnscaledSecondsProperty, "Ease In Unscaled Seconds");
        VisualElement easeOutUnscaledSecondsField = PowerUpModuleDefinitionPayloadDrawerUtility.AddField(timingFoldout, easeOutUnscaledSecondsProperty, "Ease Out Unscaled Seconds");
        VisualElement easingModeField = PowerUpModuleDefinitionPayloadDrawerUtility.AddField(timingFoldout, easingModeProperty, "Easing Mode");

        VisualElement timeSlowdownPercentField = PowerUpModuleDefinitionPayloadDrawerUtility.AddField(timeScaleFoldout, timeSlowdownPercentProperty, "Time Slowdown Percent");
        VisualElement refreshOnShorterRequestField = PowerUpModuleDefinitionPayloadDrawerUtility.AddField(timeScaleFoldout, refreshOnShorterRequestProperty, "Refresh On Shorter Request");

        Action refreshView = () =>
        {
            ImpactFrameDurationMode durationMode = ResolveDurationMode(durationModeProperty);
            bool showFrameFields = durationMode != ImpactFrameDurationMode.UnscaledSecondsOnly;
            bool showDurationSeconds = durationMode != ImpactFrameDurationMode.FramesOnly;
            SetDisplay(durationFramesField, showFrameFields);
            SetDisplay(referenceFrameRateField, showFrameFields);
            SetDisplay(maximumUnscaledDurationSecondsField, showDurationSeconds);
            RefreshWarnings(durationModeProperty,
                            durationFramesProperty,
                            referenceFrameRateProperty,
                            maximumUnscaledDurationSecondsProperty,
                            easeInUnscaledSecondsProperty,
                            easeOutUnscaledSecondsProperty,
                            timeSlowdownPercentProperty,
                            overlayIntensityProperty,
                            desaturationAmountProperty,
                            vignetteIntensityProperty,
                            vignetteSoftnessProperty,
                            vignetteExtentProperty,
                            vignetteTintProperty,
                            chromaticAberrationProperty,
                            scanlineIntensityProperty,
                            scanlineFrequencyProperty,
                            flashIntensityProperty,
                            radialDistortionProperty,
                            shockwaveIntensityProperty,
                            shockwaveRadiusProperty,
                            shockwaveThicknessProperty,
                            zoomPunchIntensityProperty,
                            invertIntensityProperty,
                            posterizeIntensityProperty,
                            posterizeStepsProperty,
                            edgeInkIntensityProperty,
                            screenTearIntensityProperty,
                            screenTearFrequencyProperty,
                            paletteFlashIntensityProperty,
                            warningBox);
        };

        RegisterRefreshCallback(durationModeField, refreshView);
        RegisterRefreshCallback(durationFramesField, refreshView);
        RegisterRefreshCallback(referenceFrameRateField, refreshView);
        RegisterRefreshCallback(maximumUnscaledDurationSecondsField, refreshView);
        RegisterRefreshCallback(easeInUnscaledSecondsField, refreshView);
        RegisterRefreshCallback(easeOutUnscaledSecondsField, refreshView);
        RegisterRefreshCallback(easingModeField, refreshView);
        RegisterRefreshCallback(timeSlowdownPercentField, refreshView);
        RegisterRefreshCallback(refreshOnShorterRequestField, refreshView);
        payloadContainer.RegisterCallback<SerializedPropertyChangeEvent>(_ => refreshView());
        refreshView();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates a compact foldout matching the other modular payload drawers.
    /// </summary>
    /// <param name="title">Visible foldout title.</param>
    /// <param name="expanded">Initial expanded state.</param>
    /// <returns>Configured foldout ready to receive field rows.</returns>
    private static Foldout CreatePayloadFoldout(string title, bool expanded)
    {
        Foldout foldout = new Foldout();
        foldout.text = title;
        foldout.value = expanded;
        foldout.style.marginLeft = 8f;
        return foldout;
    }

    /// <summary>
    /// Registers a refresh callback on one scaling-aware field root.
    /// </summary>
    /// <param name="field">Field root that can emit serialized property change events.</param>
    /// <param name="refreshAction">Callback used to refresh dependent visibility and warnings.</param>
    private static void RegisterRefreshCallback(VisualElement field, Action refreshAction)
    {
        if (field == null || refreshAction == null)
            return;

        field.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            refreshAction();
        });
    }

    /// <summary>
    /// Applies UIElements display state without rebuilding the payload tree.
    /// </summary>
    /// <param name="element">Visual element to show or hide.</param>
    /// <param name="visible">True when the element should be visible.</param>
    private static void SetDisplay(VisualElement element, bool visible)
    {
        if (element == null)
            return;

        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Reads the authored duration mode from one serialized enum property.
    /// </summary>
    /// <param name="durationModeProperty">Serialized duration mode property.</param>
    /// <returns>Resolved duration mode, defaulting to UseEarliestLimit for invalid serialized data.</returns>
    private static ImpactFrameDurationMode ResolveDurationMode(SerializedProperty durationModeProperty)
    {
        if (durationModeProperty == null)
            return ImpactFrameDurationMode.UseEarliestLimit;

        switch (durationModeProperty.enumValueIndex)
        {
            case 1:
                return ImpactFrameDurationMode.FramesOnly;
            case 2:
                return ImpactFrameDurationMode.UnscaledSecondsOnly;
            default:
                return ImpactFrameDurationMode.UseEarliestLimit;
        }
    }

    /// <summary>
    /// Refreshes authoring warnings for Impact Frame values without snapping serialized data.
    /// </summary>
    /// <param name="durationModeProperty">Serialized duration mode property.</param>
    /// <param name="durationFramesProperty">Serialized duration frames property.</param>
    /// <param name="referenceFrameRateProperty">Serialized reference frame rate property.</param>
    /// <param name="maximumUnscaledDurationSecondsProperty">Serialized maximum unscaled duration property.</param>
    /// <param name="easeInUnscaledSecondsProperty">Serialized ease-in duration property.</param>
    /// <param name="easeOutUnscaledSecondsProperty">Serialized ease-out duration property.</param>
    /// <param name="timeSlowdownPercentProperty">Serialized slowdown percentage property.</param>
    /// <param name="overlayIntensityProperty">Serialized overlay intensity property.</param>
    /// <param name="desaturationAmountProperty">Serialized desaturation property.</param>
    /// <param name="vignetteIntensityProperty">Serialized vignette intensity property.</param>
    /// <param name="vignetteSoftnessProperty">Serialized vignette softness property.</param>
    /// <param name="vignetteExtentProperty">Serialized screen-border vignette extent property.</param>
    /// <param name="vignetteTintProperty">Serialized scalable screen-border vignette RGBA property.</param>
    /// <param name="chromaticAberrationProperty">Serialized chromatic aberration property.</param>
    /// <param name="scanlineIntensityProperty">Serialized scanline intensity property.</param>
    /// <param name="scanlineFrequencyProperty">Serialized scanline frequency property.</param>
    /// <param name="flashIntensityProperty">Serialized flash intensity property.</param>
    /// <param name="radialDistortionProperty">Serialized radial distortion property.</param>
    /// <param name="shockwaveIntensityProperty">Serialized shockwave intensity property.</param>
    /// <param name="shockwaveRadiusProperty">Serialized shockwave radius property.</param>
    /// <param name="shockwaveThicknessProperty">Serialized shockwave thickness property.</param>
    /// <param name="zoomPunchIntensityProperty">Serialized zoom punch intensity property.</param>
    /// <param name="invertIntensityProperty">Serialized invert intensity property.</param>
    /// <param name="posterizeIntensityProperty">Serialized posterize intensity property.</param>
    /// <param name="posterizeStepsProperty">Serialized posterize step count property.</param>
    /// <param name="edgeInkIntensityProperty">Serialized edge ink intensity property.</param>
    /// <param name="screenTearIntensityProperty">Serialized screen tear intensity property.</param>
    /// <param name="screenTearFrequencyProperty">Serialized screen tear frequency property.</param>
    /// <param name="paletteFlashIntensityProperty">Serialized palette flash intensity property.</param>
    /// <param name="warningBox">HelpBox receiving the generated warning text.</param>
    private static void RefreshWarnings(SerializedProperty durationModeProperty,
                                        SerializedProperty durationFramesProperty,
                                        SerializedProperty referenceFrameRateProperty,
                                        SerializedProperty maximumUnscaledDurationSecondsProperty,
                                        SerializedProperty easeInUnscaledSecondsProperty,
                                        SerializedProperty easeOutUnscaledSecondsProperty,
                                        SerializedProperty timeSlowdownPercentProperty,
                                        SerializedProperty overlayIntensityProperty,
                                        SerializedProperty desaturationAmountProperty,
                                        SerializedProperty vignetteIntensityProperty,
                                        SerializedProperty vignetteSoftnessProperty,
                                        SerializedProperty vignetteExtentProperty,
                                        SerializedProperty vignetteTintProperty,
                                        SerializedProperty chromaticAberrationProperty,
                                        SerializedProperty scanlineIntensityProperty,
                                        SerializedProperty scanlineFrequencyProperty,
                                        SerializedProperty flashIntensityProperty,
                                        SerializedProperty radialDistortionProperty,
                                        SerializedProperty shockwaveIntensityProperty,
                                        SerializedProperty shockwaveRadiusProperty,
                                        SerializedProperty shockwaveThicknessProperty,
                                        SerializedProperty zoomPunchIntensityProperty,
                                        SerializedProperty invertIntensityProperty,
                                        SerializedProperty posterizeIntensityProperty,
                                        SerializedProperty posterizeStepsProperty,
                                        SerializedProperty edgeInkIntensityProperty,
                                        SerializedProperty screenTearIntensityProperty,
                                        SerializedProperty screenTearFrequencyProperty,
                                        SerializedProperty paletteFlashIntensityProperty,
                                        HelpBox warningBox)
    {
        if (warningBox == null)
            return;

        List<string> warnings = new List<string>();
        ImpactFrameDurationMode durationMode = ResolveDurationMode(durationModeProperty);
        bool usesFrames = durationMode != ImpactFrameDurationMode.UnscaledSecondsOnly;
        bool usesSeconds = durationMode != ImpactFrameDurationMode.FramesOnly;

        if (usesFrames && durationFramesProperty.intValue <= 0)
            warnings.Add("Duration Frames must be greater than 0 for frame-limited modes.");

        if (usesFrames && referenceFrameRateProperty.floatValue <= 0f)
            warnings.Add("Reference Frame Rate must be greater than 0 when frame duration is used.");

        if (usesSeconds && maximumUnscaledDurationSecondsProperty.floatValue <= 0f)
            warnings.Add("Maximum Unscaled Duration Seconds must be greater than 0 for second-limited modes.");

        if (easeInUnscaledSecondsProperty.floatValue < 0f || easeOutUnscaledSecondsProperty.floatValue < 0f)
            warnings.Add("Ease In and Ease Out durations should not be negative.");

        if (timeSlowdownPercentProperty.floatValue < 0f || timeSlowdownPercentProperty.floatValue > 100f)
            warnings.Add("Time Slowdown Percent is clamped at runtime to the 0-100 range.");

        AddUnitRangeWarning(warnings, overlayIntensityProperty, "Overlay Intensity");
        AddUnitRangeWarning(warnings, desaturationAmountProperty, "Desaturation Amount");
        AddUnitRangeWarning(warnings, vignetteIntensityProperty, "Screen Border Vignette Intensity");
        AddUnitRangeWarning(warnings, vignetteSoftnessProperty, "Screen Border Vignette Softness");
        AddUnitRangeWarning(warnings, vignetteExtentProperty, "Screen Border Vignette Extent");
        AddUnitRangeWarning(warnings, vignetteTintProperty.FindPropertyRelative("x"), "Screen Border Vignette Tint R");
        AddUnitRangeWarning(warnings, vignetteTintProperty.FindPropertyRelative("y"), "Screen Border Vignette Tint G");
        AddUnitRangeWarning(warnings, vignetteTintProperty.FindPropertyRelative("z"), "Screen Border Vignette Tint B");
        AddUnitRangeWarning(warnings, vignetteTintProperty.FindPropertyRelative("w"), "Screen Border Vignette Tint A");
        AddUnitRangeWarning(warnings, scanlineIntensityProperty, "Scanline Intensity");
        AddUnitRangeWarning(warnings, flashIntensityProperty, "Flash Intensity");
        AddUnitRangeWarning(warnings, radialDistortionProperty, "Radial Distortion");
        AddUnitRangeWarning(warnings, shockwaveIntensityProperty, "Shockwave Intensity");
        AddUnitRangeWarning(warnings, shockwaveRadiusProperty, "Shockwave Radius");
        AddUnitRangeWarning(warnings, shockwaveThicknessProperty, "Shockwave Thickness");
        AddUnitRangeWarning(warnings, zoomPunchIntensityProperty, "Zoom Punch Intensity");
        AddUnitRangeWarning(warnings, invertIntensityProperty, "Invert Intensity");
        AddUnitRangeWarning(warnings, posterizeIntensityProperty, "Posterize Intensity");
        AddUnitRangeWarning(warnings, edgeInkIntensityProperty, "Edge Ink Intensity");
        AddUnitRangeWarning(warnings, screenTearIntensityProperty, "Screen Tear Intensity");
        AddUnitRangeWarning(warnings, paletteFlashIntensityProperty, "Palette Flash Intensity");

        if (chromaticAberrationProperty.floatValue < 0f)
            warnings.Add("Chromatic Aberration should not be negative.");

        if (scanlineIntensityProperty.floatValue > 0f && scanlineFrequencyProperty.floatValue <= 0f)
            warnings.Add("Scanline Frequency must be greater than 0 when scanlines are enabled.");

        if (posterizeIntensityProperty.floatValue > 0f && posterizeStepsProperty.floatValue < 2f)
            warnings.Add("Posterize Steps must be at least 2 when posterization is enabled.");

        if (screenTearIntensityProperty.floatValue > 0f && screenTearFrequencyProperty.floatValue <= 0f)
            warnings.Add("Screen Tear Frequency must be greater than 0 when screen tear is enabled.");

        warningBox.text = string.Join("\n", warnings);
        warningBox.style.display = warnings.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Adds a standard 0-1 warning for authored filter fields.
    /// </summary>
    /// <param name="warnings">Mutable warning list receiving the message.</param>
    /// <param name="property">Serialized property to inspect.</param>
    /// <param name="label">User-facing field label.</param>
    private static void AddUnitRangeWarning(List<string> warnings, SerializedProperty property, string label)
    {
        if (warnings == null || property == null)
            return;

        if (property.floatValue < 0f || property.floatValue > 1f)
            warnings.Add(label + " is clamped at runtime to the 0-1 range.");
    }
    #endregion

    #endregion
}
