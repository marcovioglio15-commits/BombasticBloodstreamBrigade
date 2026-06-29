using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds conditionally visible Active Power-Up HUD energy syringe controls from the shared syringe settings block.
/// </summary>
internal static class PlayerVisualPresetsPanelActivePowerUpHudEnergySyringeSectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the Active Power-Up HUD energy syringe subsection with context-aware labels and visibility.
    /// </summary>
    /// <param name="parent">Parent container receiving the foldout.</param>
    /// <param name="settings">Serialized shared syringe settings used by active energy bars.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    public static void Build(VisualElement parent,
                             SerializedProperty settings,
                             SerializedProperty scalingRules)
    {
        Foldout foldout = CreateFoldout("Energy Syringe", "EnergySyringe");

        if (settings == null)
        {
            foldout.Add(new HelpBox("Energy Syringe settings are missing.", HelpBoxMessageType.Warning));
            parent.Add(foldout);
            return;
        }

        SerializedProperty enabled = settings.FindPropertyRelative("enabled");
        VisualElement details = new VisualElement();
        AddField(foldout, enabled, scalingRules, "Enabled", "Enables the active power-up energy syringe view.");
        BuildGeometryAndGraduation(details, settings, scalingRules);
        BuildEnergyChannel(details, settings.FindPropertyRelative("health"), scalingRules);
        foldout.Add(details);
        parent.Add(foldout);

        Refresh();
        TrackRefresh(foldout, enabled, Refresh);

        void Refresh()
        {
            details.style.display = enabled != null && enabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
    #endregion

    #region Section Construction
    /// <summary>
    /// Builds shared active-energy syringe geometry, graduation, and paint-drip controls.
    /// </summary>
    /// <param name="parent">Parent container receiving the foldout.</param>
    /// <param name="settings">Serialized energy syringe settings root.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    private static void BuildGeometryAndGraduation(VisualElement parent,
                                                   SerializedProperty settings,
                                                   SerializedProperty scalingRules)
    {
        Foldout foldout = CreateFoldout("Geometry and Graduation", "EnergySyringe.GeometryGraduation");
        BuildSilhouette(foldout, settings, scalingRules);
        BuildGraduation(foldout, settings, scalingRules);
        BuildPaintDrips(foldout, settings.FindPropertyRelative("paintDrips"), scalingRules);
        parent.Add(foldout);
    }

    /// <summary>
    /// Builds shared silhouette controls and exposes simplified-only termination spacing intelligently.
    /// </summary>
    /// <param name="parent">Parent foldout receiving the silhouette controls.</param>
    /// <param name="settings">Serialized energy syringe settings root.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    private static void BuildSilhouette(VisualElement parent,
                                        SerializedProperty settings,
                                        SerializedProperty scalingRules)
    {
        Foldout foldout = CreateFoldout("Silhouette", "EnergySyringe.GeometryGraduation.Silhouette");
        SerializedProperty bodyStyle = settings.FindPropertyRelative("bodyStyle");
        SerializedProperty plungerWidth = settings.FindPropertyRelative("plungerWidth");
        SerializedProperty terminationEnabled = settings.FindPropertyRelative("terminationEnabled");
        VisualElement simplifiedDetails = new VisualElement();
        VisualElement terminationDetails = new VisualElement();
        VisualElement plungerDetails = new VisualElement();
        AddField(foldout, bodyStyle, scalingRules, "Body Style", "Selects a simple painted container close to the reference sketch or the detailed syringe silhouette.");
        AddField(foldout, settings.FindPropertyRelative("barHeight"), scalingRules, "Bar Height", "Complete procedural syringe height in pixels.");
        AddField(foldout, settings.FindPropertyRelative("outlineThickness"), scalingRules, "Outline Thickness", "Normalized outline thickness relative to complete syringe height.");
        AddField(foldout, settings.FindPropertyRelative("chamberInset"), scalingRules, "Chamber Inset", "Normalized inset separating liquid chamber from outer body.");
        AddField(foldout, plungerWidth, scalingRules, "Plunger Width", "Reference-length normalized width of the moving plunger head. Runtime compensation preserves its pixel footprint across short and long syringes.");
        AddField(plungerDetails, settings.FindPropertyRelative("clampPlungerStartInsideBody"), scalingRules, "Clamp Plunger At Start", "Keeps the plunger head inside the syringe body when the represented energy is at the first graduated position.");
        AddField(plungerDetails, settings.FindPropertyRelative("clampPlungerEndInsideBody"), scalingRules, "Clamp Plunger At End", "Keeps the plunger head inside the syringe body when the represented energy is at the final graduated position.");
        AddField(plungerDetails, settings.FindPropertyRelative("stopLiquidAtPlunger"), scalingRules, "Stop Liquid At Plunger", "Stops the liquid boundary at the plunger's leading edge so the energy liquid never renders underneath the plunger head.");
        AddField(foldout, terminationEnabled, scalingRules, "Enable Termination", "Draws the right-side energy syringe termination and reserves its dedicated layout spacing.");
        AddField(foldout, settings.FindPropertyRelative("endCapWidth"), scalingRules, "End Cap Width", "Horizontal width of each non-scaling end cap; the simplified right termination starts at the final graduated value.");
        AddField(simplifiedDetails, settings.FindPropertyRelative("terminationOffset"), scalingRules, "Termination Offset", "Horizontal pixel gap between the final graduated value and the simplified right termination.");
        AddField(terminationDetails, settings.FindPropertyRelative("terminationStyle"), scalingRules, "Termination Style", "Procedural silhouette used by the simplified terminal section and detailed syringe end caps.");
        terminationDetails.Add(simplifiedDetails);
        foldout.Add(plungerDetails);
        foldout.Add(terminationDetails);
        parent.Add(foldout);

        Refresh();
        TrackRefresh(foldout, bodyStyle, Refresh);
        TrackRefresh(foldout, plungerWidth, Refresh);
        TrackRefresh(foldout, terminationEnabled, Refresh);

        void Refresh()
        {
            bool terminationOn = terminationEnabled == null || terminationEnabled.boolValue;
            plungerDetails.style.display = plungerWidth != null && plungerWidth.floatValue > 0f
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            terminationDetails.style.display = terminationOn ? DisplayStyle.Flex : DisplayStyle.None;
            simplifiedDetails.style.display = terminationOn &&
                                              bodyStyle != null &&
                                              bodyStyle.enumValueIndex == (int)PlayerSyringeBodyStyle.SimplePaintedContainer
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }

    /// <summary>
    /// Builds graduation and label controls with mode-specific option visibility.
    /// </summary>
    /// <param name="parent">Parent foldout receiving graduation controls.</param>
    /// <param name="settings">Serialized energy syringe settings root.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    private static void BuildGraduation(VisualElement parent,
                                        SerializedProperty settings,
                                        SerializedProperty scalingRules)
    {
        Foldout foldout = CreateFoldout("Graduation and Numeric Labels", "EnergySyringe.GeometryGraduation.Graduation");
        SerializedProperty graduationMode = settings.FindPropertyRelative("graduationMode");
        VisualElement fixedDetails = new VisualElement();
        VisualElement uniformDetails = new VisualElement();
        VisualElement visibleGraduationDetails = new VisualElement();
        AddField(foldout, graduationMode, scalingRules, "Graduation Mode", "Chooses fixed value units, uniformly distributed labels, or a completely hidden graduation.");
        AddField(foldout, settings.FindPropertyRelative("pixelsPerMajorDivision"), scalingRules, "Pixels Per Major Division", "Horizontal pixels assigned to every full major graduation interval.");
        AddField(foldout, settings.FindPropertyRelative("minimumLength"), scalingRules, "Minimum Length", "Minimum complete syringe width in pixels.");
        AddField(foldout, settings.FindPropertyRelative("maximumLength"), scalingRules, "Maximum Length", "Maximum complete syringe width before growth is capped.");
        AddField(fixedDetails, settings.FindPropertyRelative("unitsPerMajorDivision"), scalingRules, "Units Per Major Division", "Authoritative energy represented by every full major graduation interval in Fixed Units mode.");
        AddField(fixedDetails, settings.FindPropertyRelative("labelEveryMajorDivision"), scalingRules, "Label Every Major Division", "Displays one numeric label every N fixed major intervals when sufficient horizontal space is available.");
        AddField(uniformDetails, settings.FindPropertyRelative("uniformLabelCount"), scalingRules, "Uniform Label Count", "Total labels distributed evenly from zero to the represented energy maximum in Uniform Labels mode.");
        AddField(visibleGraduationDetails, settings.FindPropertyRelative("minorDivisionsPerMajor"), scalingRules, "Minor Divisions Per Major", "Number of smaller intervals drawn inside every visible major interval.");
        AddField(visibleGraduationDetails, settings.FindPropertyRelative("labelPlacement"), scalingRules, "Label Placement", "Places ticks and numeric labels directly inside the chamber or on the external graduation plate.");
        AddField(visibleGraduationDetails, settings.FindPropertyRelative("maximumLabelCount"), scalingRules, "Maximum Label Count", "Maximum number of preauthored numeric labels activated per syringe.");
        AddField(visibleGraduationDetails, settings.FindPropertyRelative("labelMinimumSpacing"), scalingRules, "Label Minimum Spacing", "Minimum horizontal pixel spacing maintained by automatically distributed labels.");
        AddField(foldout, settings.FindPropertyRelative("graduationEndPadding"), scalingRules, "Graduation Start Padding", "Adds space before the first graduated value without adding matching space after the final value.");
        AddField(visibleGraduationDetails, settings.FindPropertyRelative("fontAsset"), scalingRules, "Font Asset", "Direct TextMeshPro font asset applied to every numeric graduation label.");
        AddField(visibleGraduationDetails, settings.FindPropertyRelative("labelFontSize"), scalingRules, "Label Font Size", "TextMeshPro font size used by numeric graduation labels.");
        AddField(visibleGraduationDetails, settings.FindPropertyRelative("labelOffset"), scalingRules, "Label Offset", "Pixel offset applied to every numeric label relative to its major tick.");
        AddField(visibleGraduationDetails, settings.FindPropertyRelative("graduationVerticalOffset"), scalingRules, "Graduation Vertical Offset", "Optional upward offset for ticks and numeric labels within the syringe.");
        AddField(visibleGraduationDetails, settings.FindPropertyRelative("labelOutlineWidth"), scalingRules, "Label Outline Width", "TextMeshPro outline width used to preserve label readability over changing liquid colors.");
        foldout.Add(fixedDetails);
        foldout.Add(uniformDetails);
        foldout.Add(visibleGraduationDetails);
        parent.Add(foldout);

        Refresh();
        TrackRefresh(foldout, graduationMode, Refresh);

        void Refresh()
        {
            int mode = graduationMode != null ? graduationMode.enumValueIndex : (int)PlayerSyringeGraduationMode.FixedUnits;
            bool visibleGraduation = mode != (int)PlayerSyringeGraduationMode.Hidden;
            fixedDetails.style.display = mode == (int)PlayerSyringeGraduationMode.FixedUnits
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            uniformDetails.style.display = mode == (int)PlayerSyringeGraduationMode.UniformLabels
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            visibleGraduationDetails.style.display = visibleGraduation ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    /// <summary>
    /// Builds paint-drip controls and hides shape tuning while paint drips are disabled.
    /// </summary>
    /// <param name="parent">Parent foldout receiving paint-drip controls.</param>
    /// <param name="paintDrips">Serialized paint-drip settings.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    private static void BuildPaintDrips(VisualElement parent,
                                        SerializedProperty paintDrips,
                                        SerializedProperty scalingRules)
    {
        Foldout foldout = CreateFoldout("Paint Drips", "EnergySyringe.GeometryGraduation.PaintDrips");

        if (paintDrips == null)
        {
            parent.Add(foldout);
            return;
        }

        SerializedProperty enabled = paintDrips.FindPropertyRelative("enabled");
        VisualElement details = new VisualElement();
        AddField(foldout, enabled, scalingRules, "Enabled", "Enables procedural paint-like drips extending from the body borders.");
        AddField(details, paintDrips.FindPropertyRelative("density"), scalingRules, "Density", "Normalized probability that each procedural border cell produces a drip.");
        AddField(details, paintDrips.FindPropertyRelative("length"), scalingRules, "Length", "Maximum normalized length reached by procedural paint drips.");
        AddField(details, paintDrips.FindPropertyRelative("width"), scalingRules, "Width", "Reference-length normalized horizontal width of each procedural paint drip. Runtime compensation preserves its pixel footprint across short and long syringes.");
        AddField(details, paintDrips.FindPropertyRelative("irregularity"), scalingRules, "Irregularity", "Deterministic variation between neighboring drip lengths and widths.");
        foldout.Add(details);
        parent.Add(foldout);

        Refresh();
        TrackRefresh(foldout, enabled, Refresh);

        void Refresh()
        {
            details.style.display = enabled != null && enabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    /// <summary>
    /// Builds active-energy channel controls with palette, fluid, and reactive-motion sub-foldouts.
    /// </summary>
    /// <param name="parent">Parent container receiving the channel foldout.</param>
    /// <param name="channel">Serialized active-energy channel settings.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    private static void BuildEnergyChannel(VisualElement parent,
                                           SerializedProperty channel,
                                           SerializedProperty scalingRules)
    {
        Foldout foldout = CreateFoldout("Energy Channel", "EnergySyringe.EnergyChannel");

        if (channel == null)
        {
            foldout.Add(new HelpBox("Energy Channel settings are missing.", HelpBoxMessageType.Warning));
            parent.Add(foldout);
            return;
        }

        SerializedProperty enabled = channel.FindPropertyRelative("enabled");
        SerializedProperty sloshBubblesOnly = channel.FindPropertyRelative("sloshAffectsBubblesOnly");
        VisualElement details = new VisualElement();
        AddField(foldout, enabled, scalingRules, "Enabled", "Enables the active energy syringe channel.");
        AddField(details, channel.FindPropertyRelative("hideWhenMaximumUnavailable"), scalingRules, "Hide When Maximum Unavailable", "Hides this energy syringe when the equipped power-up maximum energy is zero or negative.");
        AddField(details, channel.FindPropertyRelative("smoothingSeconds"), scalingRules, "Smoothing Seconds", "Seconds used to move the displayed liquid boundary and plunger toward the authoritative current energy. Set zero for immediate movement.");
        AddField(details, sloshBubblesOnly, scalingRules, "Slosh Affects Bubbles Only", "Routes reactive slosh to the procedural bubbles only: the liquid fills flat up to the current energy and the liquid wave and surface-slosh settings are hidden.");
        BuildPalette(details, channel.FindPropertyRelative("palette"), scalingRules);
        BuildOutlineStyle(details, channel.FindPropertyRelative("outlineStyle"), scalingRules);
        BuildFluid(details, channel.FindPropertyRelative("fluid"), scalingRules, sloshBubblesOnly);
        BuildMotion(details, channel.FindPropertyRelative("motion"), scalingRules, sloshBubblesOnly);
        foldout.Add(details);
        parent.Add(foldout);

        Refresh();
        TrackRefresh(foldout, enabled, Refresh);

        void Refresh()
        {
            details.style.display = enabled != null && enabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    /// <summary>
    /// Builds painted-outline controls and hides tuning while the stylized outline is disabled.
    /// </summary>
    /// <param name="parent">Parent container receiving the outline-style foldout.</param>
    /// <param name="outlineStyle">Serialized outline-style settings.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    private static void BuildOutlineStyle(VisualElement parent,
                                          SerializedProperty outlineStyle,
                                          SerializedProperty scalingRules)
    {
        Foldout foldout = CreateFoldout("Painted Outline", "EnergySyringe.EnergyChannel.PaintedOutline");

        if (outlineStyle == null)
        {
            parent.Add(foldout);
            return;
        }

        SerializedProperty enabled = outlineStyle.FindPropertyRelative("enabled");
        VisualElement details = new VisualElement();
        AddField(foldout, enabled, scalingRules, "Enabled", "Enables non-uniform painted outline variation and optional internal streaks for this energy syringe.");
        AddField(details, outlineStyle.FindPropertyRelative("edgeWobbleStrength"), scalingRules, "Edge Wobble Strength", "Normalized strength of deterministic edge wobble applied to outline and frame masks.");
        AddField(details, outlineStyle.FindPropertyRelative("edgeWobbleFrequency"), scalingRules, "Edge Wobble Frequency", "Number of deterministic edge-wobble cells sampled along the syringe length.");
        AddField(details, outlineStyle.FindPropertyRelative("innerStreakStrength"), scalingRules, "Inner Streak Strength", "Normalized opacity of thin internal painted streaks blended inside the chamber and liquid.");
        AddField(details, outlineStyle.FindPropertyRelative("innerStreakDensity"), scalingRules, "Inner Streak Density", "Approximate normalized density of internal painted streak columns.");
        AddField(details, outlineStyle.FindPropertyRelative("innerStreakLength"), scalingRules, "Inner Streak Length", "Maximum normalized vertical length of internal paint streaks descending from the chamber top.");
        foldout.Add(details);
        parent.Add(foldout);

        Refresh();
        TrackRefresh(foldout, enabled, Refresh);

        void Refresh()
        {
            details.style.display = enabled != null && enabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    /// <summary>
    /// Builds one direct-color palette foldout with Add Scaling support for each color channel.
    /// </summary>
    /// <param name="parent">Parent container receiving the palette foldout.</param>
    /// <param name="palette">Serialized direct-color palette.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    private static void BuildPalette(VisualElement parent,
                                     SerializedProperty palette,
                                     SerializedProperty scalingRules)
    {
        Foldout foldout = CreateFoldout("Palette", "EnergySyringe.EnergyChannel.Palette");

        if (palette == null)
        {
            parent.Add(foldout);
            return;
        }

        AddField(foldout, palette.FindPropertyRelative("outline"), scalingRules, "Outline", "Near-black or colored line surrounding the complete syringe silhouette.");
        AddField(foldout, palette.FindPropertyRelative("body"), scalingRules, "Body", "Primary flat-shaded color used by the syringe body and graduation plate.");
        AddField(foldout, palette.FindPropertyRelative("bodyShadow"), scalingRules, "Body Shadow", "Secondary faceted shade used to separate body planes.");
        AddField(foldout, palette.FindPropertyRelative("chamber"), scalingRules, "Chamber", "Color used by the internal empty chamber.");
        AddField(foldout, palette.FindPropertyRelative("liquid"), scalingRules, "Liquid", "Primary color of the energy liquid.");
        AddField(foldout, palette.FindPropertyRelative("liquidHighlight"), scalingRules, "Liquid Highlight", "Secondary flat-shaded liquid color used by waves and depth layers.");
        AddField(foldout, palette.FindPropertyRelative("bubbles"), scalingRules, "Bubbles", "Color used by procedural air bubbles.");
        AddField(foldout, palette.FindPropertyRelative("graduation"), scalingRules, "Graduation", "Color used by procedural graduation ticks.");
        AddField(foldout, palette.FindPropertyRelative("label"), scalingRules, "Label", "Direct TextMeshPro color used by numeric graduation labels.");
        AddField(foldout, palette.FindPropertyRelative("labelOutline"), scalingRules, "Label Outline", "Direct TextMeshPro outline color used to keep numeric graduation labels readable.");
        AddField(foldout, palette.FindPropertyRelative("plunger"), scalingRules, "Plunger", "Primary color used by the moving-plunger frame; its outer edge uses the outline color.");
        AddField(foldout, palette.FindPropertyRelative("plungerWindow"), scalingRules, "Plunger Window", "Semitransparent color used by the readable central window inside the simplified moving plunger.");
        AddField(foldout, palette.FindPropertyRelative("terminationOutline"), scalingRules, "Termination Outline", "Direct color used by the simplified square syringe termination outline.");
        AddField(foldout, palette.FindPropertyRelative("terminationInterior"), scalingRules, "Termination Interior", "Color used inside the simplified square syringe termination.");
        parent.Add(foldout);
    }

    /// <summary>
    /// Builds fluid animation controls and hides movement details when their feature toggles are off.
    /// </summary>
    /// <param name="parent">Parent container receiving the fluid foldout.</param>
    /// <param name="fluid">Serialized fluid settings.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    /// <param name="sloshBubblesOnly">Channel slosh-on-bubbles-only toggle that hides liquid wave settings.</param>
    private static void BuildFluid(VisualElement parent,
                                   SerializedProperty fluid,
                                   SerializedProperty scalingRules,
                                   SerializedProperty sloshBubblesOnly)
    {
        Foldout foldout = CreateFoldout("Fluid", "EnergySyringe.EnergyChannel.Fluid");

        if (fluid == null)
        {
            parent.Add(foldout);
            return;
        }

        SerializedProperty flowEnabled = fluid.FindPropertyRelative("flowEnabled");
        SerializedProperty bubblesEnabled = fluid.FindPropertyRelative("bubblesEnabled");
        VisualElement flowDetails = new VisualElement();
        VisualElement waveDetails = new VisualElement();
        VisualElement bubbleDetails = new VisualElement();
        AddField(foldout, flowEnabled, scalingRules, "Flow Enabled", "Enables continuous procedural movement inside the liquid.");
        AddField(flowDetails, fluid.FindPropertyRelative("flowSpeed"), scalingRules, "Flow Speed", "Base horizontal flow speed used by liquid layers.");
        AddField(waveDetails, fluid.FindPropertyRelative("waveAmplitude"), scalingRules, "Wave Amplitude", "Maximum normalized liquid-surface displacement.");
        AddField(waveDetails, fluid.FindPropertyRelative("waveFrequency"), scalingRules, "Wave Frequency", "Number of liquid-surface waves along the chamber.");
        flowDetails.Add(waveDetails);
        AddField(flowDetails, fluid.FindPropertyRelative("viscosity"), scalingRules, "Viscosity", "Controls how slowly liquid settles after movement impulses.");
        AddField(foldout, bubblesEnabled, scalingRules, "Bubbles Enabled", "Enables deterministic procedural air bubbles.");
        AddField(bubbleDetails, fluid.FindPropertyRelative("bubbleDensity"), scalingRules, "Bubble Density", "Approximate normalized density of visible bubbles.");
        AddField(bubbleDetails, fluid.FindPropertyRelative("bubbleMinimumSize"), scalingRules, "Bubble Minimum Size", "Minimum normalized bubble radius.");
        AddField(bubbleDetails, fluid.FindPropertyRelative("bubbleMaximumSize"), scalingRules, "Bubble Maximum Size", "Maximum normalized bubble radius.");
        AddField(bubbleDetails, fluid.FindPropertyRelative("bubbleRiseSpeed"), scalingRules, "Bubble Rise Speed", "Vertical procedural bubble travel speed.");
        AddField(bubbleDetails, fluid.FindPropertyRelative("bubbleDrift"), scalingRules, "Bubble Drift", "Horizontal procedural drift while bubbles rise.");
        foldout.Add(flowDetails);
        foldout.Add(bubbleDetails);
        parent.Add(foldout);

        Refresh();
        TrackRefresh(foldout, flowEnabled, Refresh);
        TrackRefresh(foldout, bubblesEnabled, Refresh);
        TrackRefresh(foldout, sloshBubblesOnly, Refresh);

        void Refresh()
        {
            bool flowOn = flowEnabled != null && flowEnabled.boolValue;
            flowDetails.style.display = flowOn ? DisplayStyle.Flex : DisplayStyle.None;
            waveDetails.style.display = flowOn && (sloshBubblesOnly == null || !sloshBubblesOnly.boolValue)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            bubbleDetails.style.display = bubblesEnabled != null && bubblesEnabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    /// <summary>
    /// Builds reactive motion controls and hides dependent tuning while each reaction is disabled.
    /// </summary>
    /// <param name="parent">Parent container receiving the motion foldout.</param>
    /// <param name="motion">Serialized reactive-motion settings.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    /// <param name="sloshBubblesOnly">Channel slosh-on-bubbles-only toggle that hides liquid-surface slosh settings.</param>
    private static void BuildMotion(VisualElement parent,
                                    SerializedProperty motion,
                                    SerializedProperty scalingRules,
                                    SerializedProperty sloshBubblesOnly)
    {
        Foldout foldout = CreateFoldout("Reactive Motion", "EnergySyringe.EnergyChannel.Motion");

        if (motion == null)
        {
            parent.Add(foldout);
            return;
        }

        SerializedProperty movementEnabled = motion.FindPropertyRelative("movementReactionEnabled");
        SerializedProperty horizontalSloshEnabled = motion.FindPropertyRelative("horizontalSloshEnabled");
        SerializedProperty tiltEnabled = motion.FindPropertyRelative("tiltEnabled");
        SerializedProperty valueImpulseEnabled = motion.FindPropertyRelative("valueImpulseEnabled");
        VisualElement movementDetails = new VisualElement();
        VisualElement surfaceSloshDetails = new VisualElement();
        VisualElement horizontalSloshDetails = new VisualElement();
        VisualElement tiltDetails = new VisualElement();
        VisualElement valueImpulseDetails = new VisualElement();
        AddField(foldout, movementEnabled, scalingRules, "Movement Reaction Enabled", "Enables inertial liquid movement opposite to player acceleration.");
        AddField(movementDetails, motion.FindPropertyRelative("sloshStrength"), scalingRules, "Slosh Strength", "Converts player acceleration into normalized liquid displacement.");
        AddField(surfaceSloshDetails, motion.FindPropertyRelative("surfaceSloshStrength"), scalingRules, "Surface Slosh Strength", "Converts normalized slosh displacement into a visible liquid-surface slope.");
        movementDetails.Add(surfaceSloshDetails);
        AddField(movementDetails, horizontalSloshEnabled, scalingRules, "Horizontal Slosh Enabled", "Enables horizontal inertial displacement of the liquid boundary and procedural bubbles.");
        AddField(horizontalSloshDetails, motion.FindPropertyRelative("horizontalSloshStrength"), scalingRules, "Horizontal Slosh Strength", "Controls horizontal liquid and bubble displacement along the graduated value track.");
        AddField(movementDetails, motion.FindPropertyRelative("sloshSpring"), scalingRules, "Slosh Spring", "Spring force returning liquid displacement to rest.");
        AddField(movementDetails, motion.FindPropertyRelative("sloshDamping"), scalingRules, "Slosh Damping", "Damping applied while liquid returns to rest.");
        AddField(movementDetails, motion.FindPropertyRelative("maximumSlosh"), scalingRules, "Maximum Slosh", "Maximum normalized inertial liquid displacement.");
        AddField(foldout, tiltEnabled, scalingRules, "Tilt Enabled", "Enables small Z-axis syringe inclination driven by player movement.");
        AddField(tiltDetails, motion.FindPropertyRelative("maximumTiltDegrees"), scalingRules, "Maximum Tilt Degrees", "Maximum absolute Z-axis inclination.");
        AddField(tiltDetails, motion.FindPropertyRelative("tiltSpring"), scalingRules, "Tilt Spring", "Spring force returning syringe inclination to rest.");
        AddField(tiltDetails, motion.FindPropertyRelative("tiltDamping"), scalingRules, "Tilt Damping", "Damping applied while syringe inclination returns to rest.");
        AddField(foldout, valueImpulseEnabled, scalingRules, "Value Impulse Enabled", "Enables an additional liquid impulse when the represented energy changes.");
        AddField(valueImpulseDetails, motion.FindPropertyRelative("valueImpulseStrength"), scalingRules, "Value Impulse Strength", "Converts normalized value delta into a liquid impulse.");
        AddField(valueImpulseDetails, motion.FindPropertyRelative("valueImpulseDecay"), scalingRules, "Value Impulse Decay", "Exponential decay speed of value-change impulses.");
        movementDetails.Add(horizontalSloshDetails);
        foldout.Add(movementDetails);
        foldout.Add(tiltDetails);
        foldout.Add(valueImpulseDetails);
        parent.Add(foldout);

        Refresh();
        TrackRefresh(foldout, movementEnabled, Refresh);
        TrackRefresh(foldout, horizontalSloshEnabled, Refresh);
        TrackRefresh(foldout, tiltEnabled, Refresh);
        TrackRefresh(foldout, valueImpulseEnabled, Refresh);
        TrackRefresh(foldout, sloshBubblesOnly, Refresh);

        void Refresh()
        {
            bool movementOn = movementEnabled != null && movementEnabled.boolValue;
            movementDetails.style.display = movementOn ? DisplayStyle.Flex : DisplayStyle.None;
            surfaceSloshDetails.style.display = movementOn && (sloshBubblesOnly == null || !sloshBubblesOnly.boolValue)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            horizontalSloshDetails.style.display = movementOn &&
                                                   horizontalSloshEnabled != null &&
                                                   horizontalSloshEnabled.boolValue
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            tiltDetails.style.display = tiltEnabled != null && tiltEnabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            valueImpulseDetails.style.display = valueImpulseEnabled != null && valueImpulseEnabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
    #endregion

    #region Field Construction
    /// <summary>
    /// Adds one unified Add Scaling field with direct authoring and an explanatory tooltip.
    /// </summary>
    /// <param name="parent">Parent container receiving the field.</param>
    /// <param name="property">Serialized target property.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    /// <param name="label">User-facing field label.</param>
    /// <param name="tooltip">Field behavior description.</param>
    private static void AddField(VisualElement parent,
                                 SerializedProperty property,
                                 SerializedProperty scalingRules,
                                 string label,
                                 string tooltip)
    {
        if (parent == null || property == null)
            return;

        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property,
                                                                           scalingRules,
                                                                           label,
                                                                           null);
        field.tooltip = tooltip;
        parent.Add(field);
    }

    /// <summary>
    /// Creates one themed nested foldout with a stable state key.
    /// </summary>
    /// <param name="title">User-facing foldout title.</param>
    /// <param name="stateSuffix">Stable state-key suffix.</param>
    /// <returns>Configured nested foldout.</returns>
    private static Foldout CreateFoldout(string title, string stateSuffix)
    {
        return ManagementToolFoldoutStateUtility.CreateFoldout(title,
                                                               "NashCore.PlayerManagement.Visual.ActivePowerUpHud." + stateSuffix,
                                                               true);
    }
    #endregion

    #region Visibility
    /// <summary>
    /// Registers a serialized-property tracker for conditional Active HUD energy-syringe controls.
    /// </summary>
    /// <param name="root">Element owning the property tracker.</param>
    /// <param name="property">Property whose changes trigger a refresh.</param>
    /// <param name="refresh">Refresh callback.</param>
    private static void TrackRefresh(VisualElement root,
                                     SerializedProperty property,
                                     System.Action refresh)
    {
        if (root == null || property == null || refresh == null)
            return;

        root.TrackPropertyValue(property, changedProperty => refresh());
    }
    #endregion

    #endregion
}
