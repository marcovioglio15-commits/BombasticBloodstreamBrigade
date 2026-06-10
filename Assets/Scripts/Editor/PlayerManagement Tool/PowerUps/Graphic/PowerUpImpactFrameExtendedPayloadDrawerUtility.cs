using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

/// <summary>
/// Draws reusable scope, camera and charge build-in extensions shared by Impact Frame authoring.
/// </summary>
internal static class PowerUpImpactFrameExtendedPayloadDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds the extended Impact Frame authoring blocks to an existing payload editor.
    /// </summary>
    /// <param name="container">Container receiving the extension blocks.</param>
    /// <param name="impactFrameProperty">Serialized Impact Frame payload.</param>
    public static void Build(VisualElement container, SerializedProperty impactFrameProperty)
    {
        if (container == null || impactFrameProperty == null)
            return;

        SerializedProperty presentationScope = impactFrameProperty.FindPropertyRelative("presentationScope");
        SerializedProperty cameraFeedback = impactFrameProperty.FindPropertyRelative("cameraFeedback");
        SerializedProperty radialIntensity = impactFrameProperty.FindPropertyRelative("radialVignetteIntensity");
        SerializedProperty radialRadius = impactFrameProperty.FindPropertyRelative("radialVignetteRadius");
        SerializedProperty radialSoftness = impactFrameProperty.FindPropertyRelative("radialVignetteSoftness");
        SerializedProperty radialTint = impactFrameProperty.FindPropertyRelative("radialVignetteTint");
        SerializedProperty buildIn = impactFrameProperty.FindPropertyRelative("buildIn");

        if (presentationScope == null ||
            cameraFeedback == null ||
            radialIntensity == null ||
            radialRadius == null ||
            radialSoftness == null ||
            radialTint == null ||
            buildIn == null)
        {
            container.Add(new HelpBox("Extended Impact Frame fields are missing. Reopen the asset after recompiling.",
                                      HelpBoxMessageType.Warning));
            return;
        }

        Foldout scopeAndCameraFoldout = CreateFoldout("Scope And Camera", true);
        Foldout buildInFoldout = CreateFoldout("Trigger Hold Charge Build-In", false);
        container.Add(scopeAndCameraFoldout);
        container.Add(buildInFoldout);

        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(scopeAndCameraFoldout, presentationScope, "Presentation Scope");
        BuildCameraFeedback(scopeAndCameraFoldout, cameraFeedback);

        SerializedProperty buildInEnabled = buildIn.FindPropertyRelative("enabled");
        SerializedProperty releaseSeconds = buildIn.FindPropertyRelative("releaseUnscaledSeconds");
        SerializedProperty easingMode = buildIn.FindPropertyRelative("easingMode");
        SerializedProperty effect = buildIn.FindPropertyRelative("effect");
        VisualElement buildInEnabledField = PowerUpModuleDefinitionPayloadDrawerUtility.AddField(buildInFoldout,
                                                                                                 buildInEnabled,
                                                                                                 "Enabled");
        VisualElement buildInDetails = new VisualElement();
        buildInFoldout.Add(buildInDetails);
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(buildInDetails, releaseSeconds, "Release Unscaled Seconds");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(buildInDetails, easingMode, "Easing Mode");
        BuildEffect(buildInDetails, effect);
        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        container.Add(warningBox);

        System.Action refresh = () =>
        {
            buildInDetails.style.display = buildInEnabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshWarnings(radialIntensity,
                            radialRadius,
                            radialSoftness,
                            buildInEnabled,
                            releaseSeconds,
                            cameraFeedback,
                            effect,
                            warningBox);
        };
        buildInEnabledField.RegisterCallback<SerializedPropertyChangeEvent>(_ => refresh());
        container.RegisterCallback<SerializedPropertyChangeEvent>(_ => refresh());
        refresh();
    }

    /// <summary>
    /// Draws a reusable Impact Frame effect profile for modules that need optional screen and camera feedback.
    /// </summary>
    /// <param name="container">Container receiving the reusable effect hierarchy.</param>
    /// <param name="effectProperty">Serialized reusable effect profile.</param>
    /// <param name="label">Visible root foldout label.</param>
    public static void BuildStandaloneEffect(VisualElement container,
                                             SerializedProperty effectProperty,
                                             string label)
    {
        if (container == null || effectProperty == null)
            return;

        Foldout foldout = CreateFoldout(label, false);
        container.Add(foldout);
        Foldout scopeAndTime = CreateFoldout("Scope And Time", true);
        foldout.Add(scopeAndTime);
        AddEffectField(scopeAndTime, effectProperty, "presentationScope", "Presentation Scope");
        AddEffectField(scopeAndTime, effectProperty, "timeSlowdownPercent", "Time Slowdown Percent");
        BuildCameraFeedback(scopeAndTime, effectProperty.FindPropertyRelative("cameraFeedback"));
        PowerUpImpactFrameScreenEffectsDrawerUtility.Build(foldout, effectProperty, false);
    }

    /// <summary>
    /// Appends coherent reusable effect warnings without changing authored values.
    /// </summary>
    /// <param name="warnings">Mutable warning list.</param>
    /// <param name="effectProperty">Serialized reusable effect profile.</param>
    /// <param name="label">User-facing profile label.</param>
    public static void AddStandaloneEffectWarnings(List<string> warnings,
                                                   SerializedProperty effectProperty,
                                                   string label)
    {
        if (warnings == null || effectProperty == null)
            return;

        AddCameraWarnings(warnings, effectProperty.FindPropertyRelative("cameraFeedback"), label);
        AddEffectWarnings(warnings, effectProperty, label);
    }
    #endregion

    #region Builders
    /// <summary>
    /// Refreshes warnings for extended Impact Frame settings without mutating authored values.
    /// </summary>
    /// <param name="radialIntensity">Authored radial-vignette intensity.</param>
    /// <param name="radialRadius">Authored radial-vignette radius.</param>
    /// <param name="radialSoftness">Authored radial-vignette softness.</param>
    /// <param name="buildInEnabled">Authored build-in toggle.</param>
    /// <param name="releaseSeconds">Authored build-in release duration.</param>
    /// <param name="cameraFeedback">Main Impact Frame camera feedback block.</param>
    /// <param name="buildInEffect">Build-in effect profile.</param>
    /// <param name="warningBox">Warning box receiving coherent messages.</param>
    private static void RefreshWarnings(SerializedProperty radialIntensity,
                                        SerializedProperty radialRadius,
                                        SerializedProperty radialSoftness,
                                        SerializedProperty buildInEnabled,
                                        SerializedProperty releaseSeconds,
                                        SerializedProperty cameraFeedback,
                                        SerializedProperty buildInEffect,
                                        HelpBox warningBox)
    {
        List<string> warnings = new List<string>();
        AddUnitRangeWarning(warnings, radialIntensity, "Radial Vignette Intensity");
        AddUnitRangeWarning(warnings, radialRadius, "Radial Vignette Radius");
        AddUnitRangeWarning(warnings, radialSoftness, "Radial Vignette Softness");
        AddCameraWarnings(warnings, cameraFeedback, "Impact Frame");

        if (buildInEnabled.boolValue)
        {
            if (releaseSeconds.floatValue < 0f)
                warnings.Add("Build-In Release Unscaled Seconds should not be negative.");

            AddCameraWarnings(warnings,
                              buildInEffect.FindPropertyRelative("cameraFeedback"),
                              "Build-In");
            AddEffectWarnings(warnings, buildInEffect, "Build-In");
        }

        PowerUpPayloadWarningBoxUtility.ApplyWarnings(warningBox, warnings);
    }

    /// <summary>
    /// Adds range and dependent-value warnings for one reusable effect profile.
    /// </summary>
    /// <param name="warnings">Mutable warning list.</param>
    /// <param name="effectProperty">Serialized reusable effect profile.</param>
    /// <param name="label">User-facing profile label.</param>
    private static void AddEffectWarnings(List<string> warnings, SerializedProperty effectProperty, string label)
    {
        if (effectProperty == null)
            return;

        SerializedProperty timeSlowdownPercent = effectProperty.FindPropertyRelative("timeSlowdownPercent");
        SerializedProperty chromaticAberration = effectProperty.FindPropertyRelative("chromaticAberration");
        SerializedProperty scanlineIntensity = effectProperty.FindPropertyRelative("scanlineIntensity");
        SerializedProperty scanlineFrequency = effectProperty.FindPropertyRelative("scanlineFrequency");
        SerializedProperty posterizeIntensity = effectProperty.FindPropertyRelative("posterizeIntensity");
        SerializedProperty posterizeSteps = effectProperty.FindPropertyRelative("posterizeSteps");
        SerializedProperty screenTearIntensity = effectProperty.FindPropertyRelative("screenTearIntensity");
        SerializedProperty screenTearFrequency = effectProperty.FindPropertyRelative("screenTearFrequency");

        if (timeSlowdownPercent.floatValue < 0f || timeSlowdownPercent.floatValue > 100f)
            warnings.Add(label + " Time Slowdown Percent is clamped at runtime to the 0-100 range.");

        AddEffectUnitRangeWarnings(warnings, effectProperty, label);

        if (chromaticAberration.floatValue < 0f)
            warnings.Add(label + " Chromatic Aberration should not be negative.");

        if (scanlineIntensity.floatValue > 0f && scanlineFrequency.floatValue <= 0f)
            warnings.Add(label + " Scanline Frequency must be greater than 0 when scanlines are enabled.");

        if (posterizeIntensity.floatValue > 0f && posterizeSteps.floatValue < 2f)
            warnings.Add(label + " Posterize Steps must be at least 2 when posterization is enabled.");

        if (screenTearIntensity.floatValue > 0f && screenTearFrequency.floatValue <= 0f)
            warnings.Add(label + " Screen Tear Frequency must be greater than 0 when screen tear is enabled.");
    }

    /// <summary>
    /// Adds standard 0-1 warnings for every clamped reusable effect field.
    /// </summary>
    /// <param name="warnings">Mutable warning list.</param>
    /// <param name="effectProperty">Serialized reusable effect profile.</param>
    /// <param name="label">User-facing profile label.</param>
    private static void AddEffectUnitRangeWarnings(List<string> warnings, SerializedProperty effectProperty, string label)
    {
        AddUnitRangeWarning(warnings, effectProperty.FindPropertyRelative("overlayIntensity"), label + " Overlay Intensity");
        AddUnitRangeWarning(warnings, effectProperty.FindPropertyRelative("desaturationAmount"), label + " Desaturation Amount");
        AddUnitRangeWarning(warnings, effectProperty.FindPropertyRelative("vignetteIntensity"), label + " Vignette Intensity");
        AddUnitRangeWarning(warnings, effectProperty.FindPropertyRelative("vignetteSoftness"), label + " Vignette Softness");
        AddUnitRangeWarning(warnings, effectProperty.FindPropertyRelative("vignetteExtent"), label + " Vignette Extent");
        SerializedProperty vignetteTint = effectProperty.FindPropertyRelative("vignetteTint");
        AddUnitRangeWarning(warnings, vignetteTint.FindPropertyRelative("x"), label + " Vignette Tint R");
        AddUnitRangeWarning(warnings, vignetteTint.FindPropertyRelative("y"), label + " Vignette Tint G");
        AddUnitRangeWarning(warnings, vignetteTint.FindPropertyRelative("z"), label + " Vignette Tint B");
        AddUnitRangeWarning(warnings, vignetteTint.FindPropertyRelative("w"), label + " Vignette Tint A");
        AddUnitRangeWarning(warnings, effectProperty.FindPropertyRelative("radialVignetteIntensity"), label + " Radial Vignette Intensity");
        AddUnitRangeWarning(warnings, effectProperty.FindPropertyRelative("radialVignetteRadius"), label + " Radial Vignette Radius");
        AddUnitRangeWarning(warnings, effectProperty.FindPropertyRelative("radialVignetteSoftness"), label + " Radial Vignette Softness");
        AddUnitRangeWarning(warnings, effectProperty.FindPropertyRelative("scanlineIntensity"), label + " Scanline Intensity");
        AddUnitRangeWarning(warnings, effectProperty.FindPropertyRelative("flashIntensity"), label + " Flash Intensity");
        AddUnitRangeWarning(warnings, effectProperty.FindPropertyRelative("radialDistortion"), label + " Radial Distortion");
        AddUnitRangeWarning(warnings, effectProperty.FindPropertyRelative("shockwaveIntensity"), label + " Shockwave Intensity");
        AddUnitRangeWarning(warnings, effectProperty.FindPropertyRelative("shockwaveRadius"), label + " Shockwave Radius");
        AddUnitRangeWarning(warnings, effectProperty.FindPropertyRelative("shockwaveThickness"), label + " Shockwave Thickness");
        AddUnitRangeWarning(warnings, effectProperty.FindPropertyRelative("zoomPunchIntensity"), label + " Zoom Punch Intensity");
        AddUnitRangeWarning(warnings, effectProperty.FindPropertyRelative("invertIntensity"), label + " Invert Intensity");
        AddUnitRangeWarning(warnings, effectProperty.FindPropertyRelative("posterizeIntensity"), label + " Posterize Intensity");
        AddUnitRangeWarning(warnings, effectProperty.FindPropertyRelative("edgeInkIntensity"), label + " Edge Ink Intensity");
        AddUnitRangeWarning(warnings, effectProperty.FindPropertyRelative("screenTearIntensity"), label + " Screen Tear Intensity");
        AddUnitRangeWarning(warnings, effectProperty.FindPropertyRelative("paletteFlashIntensity"), label + " Palette Flash Intensity");
    }

    /// <summary>
    /// Adds standard camera-amplitude and frequency warnings for one feedback block.
    /// </summary>
    /// <param name="warnings">Mutable warning list.</param>
    /// <param name="cameraProperty">Serialized camera feedback block.</param>
    /// <param name="label">User-facing profile label.</param>
    private static void AddCameraWarnings(List<string> warnings, SerializedProperty cameraProperty, string label)
    {
        if (cameraProperty == null || !cameraProperty.FindPropertyRelative("enabled").boolValue)
            return;

        if (cameraProperty.FindPropertyRelative("positionalAmplitude").floatValue < 0f ||
            cameraProperty.FindPropertyRelative("forwardAmplitude").floatValue < 0f ||
            cameraProperty.FindPropertyRelative("rotationalAmplitude").floatValue < 0f)
        {
            warnings.Add(label + " camera amplitudes should not be negative.");
        }

        if (cameraProperty.FindPropertyRelative("frequency").floatValue < 0f)
            warnings.Add(label + " camera frequency should not be negative.");
    }

    /// <summary>
    /// Adds a standard 0-1 range warning for one extended numeric property.
    /// </summary>
    /// <param name="warnings">Mutable warning list.</param>
    /// <param name="property">Serialized numeric property.</param>
    /// <param name="label">User-facing property label.</param>
    private static void AddUnitRangeWarning(List<string> warnings, SerializedProperty property, string label)
    {
        if (property == null)
            return;

        if (property.floatValue < 0f || property.floatValue > 1f)
            warnings.Add(label + " is clamped at runtime to the 0-1 range.");
    }

    /// <summary>
    /// Draws one camera feedback block with dependent controls hidden while disabled.
    /// </summary>
    /// <param name="parent">Parent receiving the camera fields.</param>
    /// <param name="cameraProperty">Serialized camera feedback block.</param>
    private static void BuildCameraFeedback(VisualElement parent, SerializedProperty cameraProperty)
    {
        if (cameraProperty == null)
            return;

        Foldout foldout = CreateFoldout("Camera Feedback", false);
        parent.Add(foldout);
        SerializedProperty enabled = cameraProperty.FindPropertyRelative("enabled");
        VisualElement enabledField = PowerUpModuleDefinitionPayloadDrawerUtility.AddField(foldout, enabled, "Enabled");
        VisualElement details = new VisualElement();
        foldout.Add(details);
        SerializedProperty motionMode = cameraProperty.FindPropertyRelative("motionMode");
        SerializedProperty axisRightEnabled = cameraProperty.FindPropertyRelative("axisRightEnabled");
        SerializedProperty axisUpEnabled = cameraProperty.FindPropertyRelative("axisUpEnabled");
        SerializedProperty axisForwardEnabled = cameraProperty.FindPropertyRelative("axisForwardEnabled");
        SerializedProperty zoomEnabled = cameraProperty.FindPropertyRelative("zoomEnabled");
        AddEffectField(details, cameraProperty, "motionMode", "Motion Mode");
        AddEffectField(details, cameraProperty, "axisRightEnabled", "Axis Right Enabled");
        AddEffectField(details, cameraProperty, "axisUpEnabled", "Axis Up Enabled");
        AddEffectField(details, cameraProperty, "axisForwardEnabled", "Axis Forward Enabled");
        VisualElement positionalAmplitude = AddEffectField(details, cameraProperty, "positionalAmplitude", "Positional Amplitude");
        VisualElement forwardAmplitude = AddEffectField(details, cameraProperty, "forwardAmplitude", "Forward Amplitude");
        AddEffectField(details, cameraProperty, "rotationalAmplitude", "Rotational Amplitude");
        VisualElement frequency = AddEffectField(details, cameraProperty, "frequency", "Frequency");
        AddEffectField(details, cameraProperty, "zoomEnabled", "Zoom Enabled");
        VisualElement zoomFovDelta = AddEffectField(details, cameraProperty, "zoomFovDelta", "Zoom FOV Delta");

        System.Action refresh = () =>
        {
            details.style.display = enabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            SetDisplay(positionalAmplitude, axisRightEnabled.boolValue || axisUpEnabled.boolValue);
            SetDisplay(forwardAmplitude, axisForwardEnabled.boolValue);
            SetDisplay(frequency, motionMode.enumValueIndex == (int)CameraShakeMotionMode.Continuous);
            SetDisplay(zoomFovDelta, zoomEnabled.boolValue);
        };
        enabledField.RegisterCallback<SerializedPropertyChangeEvent>(_ => refresh());
        details.RegisterCallback<SerializedPropertyChangeEvent>(_ => refresh());
        refresh();
    }

    /// <summary>
    /// Draws every reusable build-in effect field while preserving camera-specific conditional presentation.
    /// </summary>
    /// <param name="parent">Parent receiving the effect profile.</param>
    /// <param name="effectProperty">Serialized reusable effect profile.</param>
    private static void BuildEffect(VisualElement parent, SerializedProperty effectProperty)
    {
        if (effectProperty == null)
            return;

        Foldout foldout = CreateFoldout("Build-In Effect", false);
        parent.Add(foldout);
        Foldout scopeAndTime = CreateFoldout("Scope And Time", true);
        foldout.Add(scopeAndTime);

        SerializedProperty cameraFeedback = effectProperty.FindPropertyRelative("cameraFeedback");
        AddEffectField(scopeAndTime, effectProperty, "presentationScope", "Presentation Scope");
        AddEffectField(scopeAndTime, effectProperty, "timeSlowdownPercent", "Time Slowdown Percent");
        BuildCameraFeedback(scopeAndTime, cameraFeedback);
        PowerUpImpactFrameScreenEffectsDrawerUtility.Build(foldout, effectProperty, false);
    }

    /// <summary>
    /// Adds one scaling-aware relative field from an Impact Frame data block.
    /// </summary>
    /// <param name="parent">Parent receiving the field.</param>
    /// <param name="effectProperty">Serialized parent data block.</param>
    /// <param name="relativeName">Immediate child property name.</param>
    /// <param name="label">User-facing field label.</param>
    /// <returns>Created field root, or null when the property is unavailable.</returns>
    private static VisualElement AddEffectField(VisualElement parent,
                                                SerializedProperty effectProperty,
                                                string relativeName,
                                                string label)
    {
        SerializedProperty property = effectProperty.FindPropertyRelative(relativeName);

        if (property == null)
            return null;

        return PowerUpModuleDefinitionPayloadDrawerUtility.AddField(parent, property, label);
    }

    /// <summary>
    /// Applies contextual visibility without rebuilding the editor tree.
    /// </summary>
    /// <param name="element">Visual element to update.</param>
    /// <param name="visible">True when the element should be shown.</param>
    private static void SetDisplay(VisualElement element, bool visible)
    {
        if (element == null)
            return;

        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Creates one compact Impact Frame extension foldout.
    /// </summary>
    /// <param name="title">Visible foldout title.</param>
    /// <param name="expanded">Initial foldout state.</param>
    /// <returns>Configured foldout.</returns>
    private static Foldout CreateFoldout(string title, bool expanded)
    {
        Foldout foldout = new Foldout
        {
            text = title,
            value = expanded
        };
        foldout.style.marginLeft = 8f;
        return foldout;
    }
    #endregion

    #endregion
}

/// <summary>
/// Builds the shared scaling-aware Impact Frame screen-effects hierarchy used by final, build-in and death profiles.
/// </summary>
internal static class PowerUpImpactFrameScreenEffectsDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds one compact screen-effects foldout with thematic subsections and context-sensitive dependent fields.
    /// </summary>
    /// <param name="parent">Container receiving the screen-effects hierarchy.</param>
    /// <param name="effectProperty">Serialized object containing the flat Impact Frame screen-effect fields.</param>
    /// <param name="expanded">Initial state of the root Screen Effects foldout.</param>
    public static void Build(VisualElement parent, SerializedProperty effectProperty, bool expanded)
    {
        if (parent == null || effectProperty == null)
            return;

        Foldout screenEffects = CreateFoldout("Screen Effects", expanded);
        parent.Add(screenEffects);
        SerializedProperty overlayIntensity = effectProperty.FindPropertyRelative("overlayIntensity");
        VisualElement overlayIntensityField = AddField(screenEffects, effectProperty, "overlayIntensity", "Overlay Intensity");
        VisualElement details = new VisualElement();
        screenEffects.Add(details);

        Foldout coreFilter = CreateFoldout("Core Filter", true);
        Foldout vignettes = CreateFoldout("Vignettes", false);
        Foldout distortionAndMotion = CreateFoldout("Distortion And Motion", false);
        Foldout stylization = CreateFoldout("Stylization", false);
        Foldout lightAndColorBursts = CreateFoldout("Light And Color Bursts", false);
        details.Add(coreFilter);
        details.Add(vignettes);
        details.Add(distortionAndMotion);
        details.Add(stylization);
        details.Add(lightAndColorBursts);

        AddScalableColorField(coreFilter, effectProperty, "filterTint", "Filter Tint");
        AddField(coreFilter, effectProperty, "desaturationAmount", "Desaturation Amount");

        Foldout screenBorder = CreateFoldout("Screen Border", false);
        Foldout radialRing = CreateFoldout("Radial Ring", false);
        vignettes.Add(screenBorder);
        vignettes.Add(radialRing);
        AddField(screenBorder, effectProperty, "vignetteIntensity", "Intensity");
        VisualElement borderDetails = new VisualElement();
        screenBorder.Add(borderDetails);
        AddField(borderDetails, effectProperty, "vignetteExtent", "Extent");
        AddField(borderDetails, effectProperty, "vignetteSoftness", "Softness");
        AddScalableTintField(borderDetails, effectProperty, "vignetteTint", "Tint");
        AddField(radialRing, effectProperty, "radialVignetteIntensity", "Intensity");
        VisualElement radialDetails = new VisualElement();
        radialRing.Add(radialDetails);
        AddField(radialDetails, effectProperty, "radialVignetteRadius", "Radius");
        AddField(radialDetails, effectProperty, "radialVignetteSoftness", "Softness");
        AddScalableColorField(radialDetails, effectProperty, "radialVignetteTint", "Tint");

        AddField(distortionAndMotion, effectProperty, "chromaticAberration", "Chromatic Aberration");
        AddField(distortionAndMotion, effectProperty, "radialDistortion", "Radial Distortion");
        AddField(distortionAndMotion, effectProperty, "shockwaveIntensity", "Shockwave Intensity");
        VisualElement shockwaveRadius = AddField(distortionAndMotion, effectProperty, "shockwaveRadius", "Shockwave Radius");
        VisualElement shockwaveThickness = AddField(distortionAndMotion, effectProperty, "shockwaveThickness", "Shockwave Thickness");
        AddField(distortionAndMotion, effectProperty, "zoomPunchIntensity", "Zoom Punch Intensity");
        AddField(distortionAndMotion, effectProperty, "screenTearIntensity", "Screen Tear Intensity");
        VisualElement screenTearFrequency = AddField(distortionAndMotion, effectProperty, "screenTearFrequency", "Screen Tear Frequency");

        AddField(stylization, effectProperty, "scanlineIntensity", "Scanline Intensity");
        VisualElement scanlineFrequency = AddField(stylization, effectProperty, "scanlineFrequency", "Scanline Frequency");
        AddField(stylization, effectProperty, "invertIntensity", "Invert Intensity");
        AddField(stylization, effectProperty, "posterizeIntensity", "Posterize Intensity");
        VisualElement posterizeSteps = AddField(stylization, effectProperty, "posterizeSteps", "Posterize Steps");
        AddField(stylization, effectProperty, "edgeInkIntensity", "Edge Ink Intensity");

        AddField(lightAndColorBursts, effectProperty, "flashIntensity", "Flash Intensity");
        AddField(lightAndColorBursts, effectProperty, "paletteFlashIntensity", "Palette Flash Intensity");
        VisualElement paletteFlashTint = AddScalableColorField(lightAndColorBursts,
                                                               effectProperty,
                                                               "paletteFlashTint",
                                                               "Palette Flash Tint");

        System.Action refresh = () =>
        {
            details.style.display = HasPositiveFloat(overlayIntensity) ? DisplayStyle.Flex : DisplayStyle.None;
            borderDetails.style.display = HasPositiveFloat(effectProperty, "vignetteIntensity") ? DisplayStyle.Flex : DisplayStyle.None;
            radialDetails.style.display = HasPositiveFloat(effectProperty, "radialVignetteIntensity") ? DisplayStyle.Flex : DisplayStyle.None;
            bool shockwaveEnabled = HasPositiveFloat(effectProperty, "shockwaveIntensity");
            SetDisplay(shockwaveRadius, shockwaveEnabled);
            SetDisplay(shockwaveThickness, shockwaveEnabled);
            SetDisplay(screenTearFrequency, HasPositiveFloat(effectProperty, "screenTearIntensity"));
            SetDisplay(scanlineFrequency, HasPositiveFloat(effectProperty, "scanlineIntensity"));
            SetDisplay(posterizeSteps, HasPositiveFloat(effectProperty, "posterizeIntensity"));
            SetDisplay(paletteFlashTint, HasPositiveFloat(effectProperty, "paletteFlashIntensity"));
        };
        overlayIntensityField.RegisterCallback<SerializedPropertyChangeEvent>(_ => refresh());
        screenEffects.RegisterCallback<SerializedPropertyChangeEvent>(_ => refresh());
        refresh();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds a color picker backed by a scalable Vector4 and exposes each RGBA channel through the unified formula UI.
    /// </summary>
    /// <param name="parent">Container receiving the color picker and channel-scaling foldout.</param>
    /// <param name="effectProperty">Serialized flat effect profile.</param>
    /// <param name="relativeName">Immediate scalable Vector4 child name.</param>
    /// <param name="label">User-facing color-picker label.</param>
    private static void AddScalableTintField(VisualElement parent,
                                             SerializedProperty effectProperty,
                                             string relativeName,
                                             string label)
    {
        SerializedProperty tintProperty = effectProperty.FindPropertyRelative(relativeName);

        if (tintProperty == null)
            return;

        Vector4 tintVector = tintProperty.vector4Value;
        ColorField colorField = new ColorField(label);
        colorField.showAlpha = true;
        colorField.tooltip = tintProperty.tooltip;
        colorField.SetValueWithoutNotify(new Color(tintVector.x, tintVector.y, tintVector.z, tintVector.w));
        parent.Add(colorField);
        Foldout channelScaling = CreateFoldout("Tint Channel Scaling", false);
        parent.Add(channelScaling);
        VisualElement redField = AddField(channelScaling, tintProperty, "x", "Red");
        VisualElement greenField = AddField(channelScaling, tintProperty, "y", "Green");
        VisualElement blueField = AddField(channelScaling, tintProperty, "z", "Blue");
        VisualElement alphaField = AddField(channelScaling, tintProperty, "w", "Alpha");
        SetTooltip(redField, "Red tint channel. Supports Add Scaling and is clamped to the 0-1 range at bake/runtime.");
        SetTooltip(greenField, "Green tint channel. Supports Add Scaling and is clamped to the 0-1 range at bake/runtime.");
        SetTooltip(blueField, "Blue tint channel. Supports Add Scaling and is clamped to the 0-1 range at bake/runtime.");
        SetTooltip(alphaField, "Maximum tint opacity. Supports Add Scaling and multiplies Screen Border Intensity at runtime.");

        colorField.RegisterValueChangedCallback(evt =>
        {
            tintProperty.serializedObject.Update();
            tintProperty.vector4Value = new Vector4(evt.newValue.r,
                                                    evt.newValue.g,
                                                    evt.newValue.b,
                                                    evt.newValue.a);
            tintProperty.serializedObject.ApplyModifiedProperties();
            PlayerManagementDraftSession.MarkDirty();
        });
        colorField.TrackPropertyValue(tintProperty, property =>
        {
            Vector4 value = property.vector4Value;
            colorField.SetValueWithoutNotify(new Color(value.x, value.y, value.z, value.w));
        });
    }

    /// <summary>
    /// Adds a color picker backed by a scalable Color and exposes each RGBA channel through the unified formula UI.
    /// </summary>
    /// <param name="parent">Container receiving the color picker and channel-scaling foldout.</param>
    /// <param name="effectProperty">Serialized flat effect profile.</param>
    /// <param name="relativeName">Immediate scalable Color child name.</param>
    /// <param name="label">User-facing color-picker label.</param>
    /// <returns>Root container used for contextual visibility.</returns>
    private static VisualElement AddScalableColorField(VisualElement parent,
                                                       SerializedProperty effectProperty,
                                                       string relativeName,
                                                       string label)
    {
        SerializedProperty colorProperty = effectProperty.FindPropertyRelative(relativeName);

        if (colorProperty == null)
            return null;

        VisualElement root = new VisualElement();
        parent.Add(root);
        ColorField colorField = new ColorField(label);
        colorField.showAlpha = true;
        colorField.tooltip = colorProperty.tooltip;
        colorField.SetValueWithoutNotify(colorProperty.colorValue);
        root.Add(colorField);
        Foldout channelScaling = CreateFoldout("Color Channel Scaling", false);
        root.Add(channelScaling);
        SetTooltip(AddField(channelScaling, colorProperty, "r", "Red"),
                   "Red color channel. Supports Add Scaling and is clamped to the 0-1 range at bake/runtime.");
        SetTooltip(AddField(channelScaling, colorProperty, "g", "Green"),
                   "Green color channel. Supports Add Scaling and is clamped to the 0-1 range at bake/runtime.");
        SetTooltip(AddField(channelScaling, colorProperty, "b", "Blue"),
                   "Blue color channel. Supports Add Scaling and is clamped to the 0-1 range at bake/runtime.");
        SetTooltip(AddField(channelScaling, colorProperty, "a", "Alpha"),
                   "Alpha color channel. Supports Add Scaling and is clamped to the 0-1 range at bake/runtime.");

        colorField.RegisterValueChangedCallback(evt =>
        {
            colorProperty.serializedObject.Update();
            colorProperty.colorValue = evt.newValue;
            colorProperty.serializedObject.ApplyModifiedProperties();
            PlayerManagementDraftSession.MarkDirty();
        });
        colorField.TrackPropertyValue(colorProperty, property =>
        {
            colorField.SetValueWithoutNotify(property.colorValue);
        });
        return root;
    }

    /// <summary>
    /// Adds one scaling-aware field from the provided flat effect profile.
    /// </summary>
    /// <param name="parent">Container receiving the field.</param>
    /// <param name="effectProperty">Serialized flat effect profile.</param>
    /// <param name="relativeName">Immediate serialized child name.</param>
    /// <param name="label">User-facing field label.</param>
    /// <returns>Created field root, or null when the serialized child is unavailable.</returns>
    private static VisualElement AddField(VisualElement parent,
                                          SerializedProperty effectProperty,
                                          string relativeName,
                                          string label)
    {
        SerializedProperty property = effectProperty.FindPropertyRelative(relativeName);

        if (property == null)
            return null;

        return PowerUpModuleDefinitionPayloadDrawerUtility.AddField(parent, property, label);
    }

    /// <summary>
    /// Assigns explanatory text to one generated control when it exists.
    /// </summary>
    /// <param name="element">Generated field root receiving the tooltip.</param>
    /// <param name="tooltip">User-facing explanation of the exposed value.</param>
    private static void SetTooltip(VisualElement element, string tooltip)
    {
        if (element == null)
            return;

        element.tooltip = tooltip;
    }

    /// <summary>
    /// Resolves whether one immediate numeric child currently enables its dependent controls.
    /// </summary>
    /// <param name="effectProperty">Serialized flat effect profile.</param>
    /// <param name="relativeName">Immediate numeric child name.</param>
    /// <returns>True when the child exists and is greater than zero.</returns>
    private static bool HasPositiveFloat(SerializedProperty effectProperty, string relativeName)
    {
        return HasPositiveFloat(effectProperty.FindPropertyRelative(relativeName));
    }

    /// <summary>
    /// Resolves whether one numeric property currently enables its dependent controls.
    /// </summary>
    /// <param name="property">Serialized numeric property.</param>
    /// <returns>True when the property exists and is greater than zero.</returns>
    private static bool HasPositiveFloat(SerializedProperty property)
    {
        return property != null && property.floatValue > 0f;
    }

    /// <summary>
    /// Applies contextual visibility without rebuilding the editor hierarchy.
    /// </summary>
    /// <param name="element">Visual element to update.</param>
    /// <param name="visible">True when the element should be shown.</param>
    private static void SetDisplay(VisualElement element, bool visible)
    {
        if (element == null)
            return;

        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Creates one compact thematic foldout.
    /// </summary>
    /// <param name="title">Visible foldout title.</param>
    /// <param name="expanded">Initial foldout state.</param>
    /// <returns>Configured foldout ready to receive fields.</returns>
    private static Foldout CreateFoldout(string title, bool expanded)
    {
        Foldout foldout = new Foldout
        {
            text = title,
            value = expanded
        };
        foldout.style.marginLeft = 8f;
        return foldout;
    }
    #endregion

    #endregion
}
