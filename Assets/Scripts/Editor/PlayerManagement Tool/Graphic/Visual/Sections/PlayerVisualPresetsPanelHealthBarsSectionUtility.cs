using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds scalable and conditionally visible Player Visual Preset controls for the health and shield syringe HUD.
/// </summary>
internal static class PlayerVisualPresetsPanelHealthBarsSectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the complete Health Bars visual-preset subsection.
    /// </summary>
    /// <param name="panel">Owning visual preset panel providing serialized authoring data.</param>
    /// <returns>Configured Health Bars subsection.</returns>
    public static VisualElement Build(PlayerVisualPresetsPanel panel)
    {
        Foldout root = ManagementToolFoldoutStateUtility.CreateFoldout("Health Bars",
                                                                        "NashCore.PlayerManagement.Visual.HealthBars",
                                                                        true);
        root.tooltip = "Configures the ECS-authoritative procedural health and shield syringe HUD.";

        if (panel == null || panel.PresetSerializedObject == null)
            return root;

        SerializedObject serializedObject = panel.PresetSerializedObject;
        SerializedProperty settings = serializedObject.FindProperty("healthBars");
        SerializedProperty scalingRules = serializedObject.FindProperty("scalingRules");

        if (settings == null)
        {
            root.Add(new HelpBox("Health Bars settings are missing from the selected Player Visual Preset.",
                                 HelpBoxMessageType.Warning));
            return root;
        }

        SerializedProperty enabled = settings.FindPropertyRelative("enabled");
        VisualElement details = new VisualElement();
        VisualElement warnings = new VisualElement();
        AddField(root, enabled, scalingRules, "Enabled", "Enables both ECS-authoritative syringe HUD views.");
        BuildGeneral(details, settings, scalingRules);
        BuildGeometryAndGraduation(details, settings, scalingRules);
        BuildChannel(details, settings.FindPropertyRelative("health"), scalingRules, "Health Syringe", "Health");
        BuildChannel(details, settings.FindPropertyRelative("shield"), scalingRules, "Shield Syringe", "Shield");
        root.Add(details);
        root.Add(warnings);

        Refresh();
        TrackRefresh(root, enabled, Refresh);
        TrackRefresh(root, settings.FindPropertyRelative("unitsPerMajorDivision"), Refresh);
        TrackRefresh(root, settings.FindPropertyRelative("pixelsPerMajorDivision"), Refresh);
        TrackRefresh(root, settings.FindPropertyRelative("minimumLength"), Refresh);
        TrackRefresh(root, settings.FindPropertyRelative("maximumLength"), Refresh);
        TrackRefresh(root, settings.FindPropertyRelative("maximumLabelCount"), Refresh);
        TrackRefresh(root, settings.FindPropertyRelative("labelMinimumSpacing"), Refresh);
        TrackRefresh(root, settings.FindPropertyRelative("graduationEndPadding"), Refresh);
        TrackRefresh(root, settings.FindPropertyRelative("terminationOffset"), Refresh);
        TrackRefresh(root, settings.FindPropertyRelative("fontAsset"), Refresh);
        SerializedProperty paintDrips = settings.FindPropertyRelative("paintDrips");
        TrackRefresh(root, paintDrips.FindPropertyRelative("enabled"), Refresh);
        TrackRefresh(root, paintDrips.FindPropertyRelative("density"), Refresh);
        TrackRefresh(root, paintDrips.FindPropertyRelative("length"), Refresh);
        TrackRefresh(root, paintDrips.FindPropertyRelative("width"), Refresh);
        TrackRefresh(root, paintDrips.FindPropertyRelative("irregularity"), Refresh);
        return root;

        void Refresh()
        {
            details.style.display = enabled != null && enabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshWarnings(warnings, settings);
        }
    }
    #endregion

    #region Section Construction
    /// <summary>
    /// Builds shared health-bar visibility and layout controls.
    /// </summary>
    /// <param name="parent">Parent container receiving the foldout.</param>
    /// <param name="settings">Serialized Health Bars settings root.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    private static void BuildGeneral(VisualElement parent,
                                     SerializedProperty settings,
                                     SerializedProperty scalingRules)
    {
        Foldout foldout = CreateFoldout("General", "General");
        AddField(foldout, settings.FindPropertyRelative("hideWhenPlayerMissing"), scalingRules, "Hide When Player Missing", "Hides both syringe views while no valid player entity is available.");
        AddField(foldout, settings.FindPropertyRelative("verticalSpacing"), scalingRules, "Vertical Spacing", "Vertical pixel spacing between health and shield syringe roots.");
        parent.Add(foldout);
    }

    /// <summary>
    /// Builds shared fixed-unit graduation, label, and procedural geometry controls.
    /// </summary>
    /// <param name="parent">Parent container receiving the foldout.</param>
    /// <param name="settings">Serialized Health Bars settings root.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    private static void BuildGeometryAndGraduation(VisualElement parent,
                                                   SerializedProperty settings,
                                                   SerializedProperty scalingRules)
    {
        Foldout foldout = CreateFoldout("Geometry and Fixed-Unit Graduation", "GeometryGraduation");
        BuildSilhouette(foldout, settings, scalingRules);
        BuildGraduation(foldout, settings, scalingRules);
        BuildPaintDrips(foldout, settings.FindPropertyRelative("paintDrips"), scalingRules);
        parent.Add(foldout);
    }

    /// <summary>
    /// Builds shared silhouette controls and exposes detailed-only end-cap options intelligently.
    /// </summary>
    /// <param name="parent">Parent foldout receiving the silhouette controls.</param>
    /// <param name="settings">Serialized Health Bars settings root.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    private static void BuildSilhouette(VisualElement parent,
                                        SerializedProperty settings,
                                        SerializedProperty scalingRules)
    {
        Foldout foldout = CreateFoldout("Silhouette", "GeometryGraduation.Silhouette");
        SerializedProperty bodyStyle = settings.FindPropertyRelative("bodyStyle");
        VisualElement simplifiedDetails = new VisualElement();
        AddField(foldout, bodyStyle, scalingRules, "Body Style", "Selects a simple painted container close to the reference sketch or the detailed syringe silhouette.");
        AddField(foldout, settings.FindPropertyRelative("barHeight"), scalingRules, "Bar Height", "Complete procedural syringe height in pixels.");
        AddField(foldout, settings.FindPropertyRelative("outlineThickness"), scalingRules, "Outline Thickness", "Normalized outline thickness relative to complete syringe height.");
        AddField(foldout, settings.FindPropertyRelative("chamberInset"), scalingRules, "Chamber Inset", "Normalized inset separating liquid chamber from outer body.");
        AddField(foldout, settings.FindPropertyRelative("plungerWidth"), scalingRules, "Plunger Width", "Reference-length normalized width of the moving plunger head. Runtime compensation preserves its pixel footprint across short and long syringes.");
        AddField(foldout, settings.FindPropertyRelative("endCapWidth"), scalingRules, "End Cap Width", "Horizontal width of each non-scaling end cap; the simplified right termination starts at the final graduated value.");
        AddField(simplifiedDetails, settings.FindPropertyRelative("terminationOffset"), scalingRules, "Termination Offset", "Horizontal pixel gap between the final graduated value and the simplified right termination.");
        AddField(foldout, settings.FindPropertyRelative("terminationStyle"), scalingRules, "Termination Style", "Procedural silhouette used by the simplified terminal section and detailed syringe end caps.");
        foldout.Add(simplifiedDetails);
        parent.Add(foldout);

        Refresh();
        TrackRefresh(foldout, bodyStyle, Refresh);

        void Refresh()
        {
            simplifiedDetails.style.display = bodyStyle != null &&
                                              bodyStyle.enumValueIndex == (int)PlayerSyringeBodyStyle.SimplePaintedContainer
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }

    /// <summary>
    /// Builds fixed-unit graduation and readable numeric-label controls.
    /// </summary>
    /// <param name="parent">Parent foldout receiving graduation controls.</param>
    /// <param name="settings">Serialized Health Bars settings root.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    private static void BuildGraduation(VisualElement parent,
                                        SerializedProperty settings,
                                        SerializedProperty scalingRules)
    {
        Foldout foldout = CreateFoldout("Graduation and Numeric Labels", "GeometryGraduation.Graduation");
        AddField(foldout, settings.FindPropertyRelative("unitsPerMajorDivision"), scalingRules, "Units Per Major Division", "Authoritative value represented by every full major graduation interval.");
        AddField(foldout, settings.FindPropertyRelative("pixelsPerMajorDivision"), scalingRules, "Pixels Per Major Division", "Horizontal pixels assigned to every full major graduation interval.");
        AddField(foldout, settings.FindPropertyRelative("minimumLength"), scalingRules, "Minimum Length", "Minimum complete syringe width in pixels.");
        AddField(foldout, settings.FindPropertyRelative("maximumLength"), scalingRules, "Maximum Length", "Maximum complete syringe width before growth is capped.");
        AddField(foldout, settings.FindPropertyRelative("minorDivisionsPerMajor"), scalingRules, "Minor Divisions Per Major", "Number of smaller intervals drawn inside every major interval.");
        AddField(foldout, settings.FindPropertyRelative("labelPlacement"), scalingRules, "Label Placement", "Places ticks and numeric labels directly inside the chamber or on the external graduation plate.");
        AddField(foldout, settings.FindPropertyRelative("labelEveryMajorDivision"), scalingRules, "Label Every Major Division", "Displays one numeric label every N major intervals when sufficient horizontal space is available.");
        AddField(foldout, settings.FindPropertyRelative("maximumLabelCount"), scalingRules, "Maximum Label Count", "Maximum number of preauthored numeric labels activated per syringe.");
        AddField(foldout, settings.FindPropertyRelative("labelMinimumSpacing"), scalingRules, "Label Minimum Spacing", "Minimum horizontal pixel spacing maintained by automatically distributed labels.");
        AddField(foldout, settings.FindPropertyRelative("graduationEndPadding"), scalingRules, "Graduation Start Padding", "Adds space before the first graduated value without adding matching space after the final value.");
        AddField(foldout, settings.FindPropertyRelative("fontAsset"), scalingRules, "Font Asset", "Direct TextMeshPro font asset applied to every numeric graduation label.");
        AddField(foldout, settings.FindPropertyRelative("labelFontSize"), scalingRules, "Label Font Size", "TextMeshPro font size used by numeric graduation labels.");
        AddField(foldout, settings.FindPropertyRelative("labelOffset"), scalingRules, "Label Offset", "Pixel offset applied to every numeric label relative to its major tick.");
        AddField(foldout, settings.FindPropertyRelative("graduationVerticalOffset"), scalingRules, "Graduation Vertical Offset", "Optional upward offset for the entire graduation - ticks and numeric labels - within the syringe. Positive values move it up.");
        AddField(foldout, settings.FindPropertyRelative("labelOutlineWidth"), scalingRules, "Label Outline Width", "TextMeshPro outline width used to preserve label readability over changing liquid colors.");
        parent.Add(foldout);
    }

    /// <summary>
    /// Builds conditionally visible procedural paint-drip controls.
    /// </summary>
    /// <param name="parent">Parent foldout receiving paint-drip controls.</param>
    /// <param name="paintDrips">Serialized paint-drip settings.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    private static void BuildPaintDrips(VisualElement parent,
                                        SerializedProperty paintDrips,
                                        SerializedProperty scalingRules)
    {
        Foldout foldout = CreateFoldout("Paint Drips", "GeometryGraduation.PaintDrips");

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
    /// Builds one health or shield channel with intelligent palette, fluid, and motion sub-foldouts.
    /// </summary>
    /// <param name="parent">Parent container receiving the channel foldout.</param>
    /// <param name="channel">Serialized channel settings.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    /// <param name="title">User-facing channel foldout title.</param>
    /// <param name="stateSuffix">Stable foldout-state suffix.</param>
    private static void BuildChannel(VisualElement parent,
                                     SerializedProperty channel,
                                     SerializedProperty scalingRules,
                                     string title,
                                     string stateSuffix)
    {
        Foldout foldout = CreateFoldout(title, stateSuffix);

        if (channel == null)
        {
            foldout.Add(new HelpBox(title + " settings are missing.", HelpBoxMessageType.Warning));
            parent.Add(foldout);
            return;
        }

        SerializedProperty enabled = channel.FindPropertyRelative("enabled");
        SerializedProperty sloshBubblesOnly = channel.FindPropertyRelative("sloshAffectsBubblesOnly");
        VisualElement details = new VisualElement();
        AddField(foldout, enabled, scalingRules, "Enabled", "Enables this syringe channel.");
        AddField(details, channel.FindPropertyRelative("hideWhenMaximumUnavailable"), scalingRules, "Hide When Maximum Unavailable", "Hides this syringe when its authoritative maximum is zero or negative.");
        AddField(details, channel.FindPropertyRelative("smoothingSeconds"), scalingRules, "Smoothing Seconds", "Seconds used to move the displayed liquid boundary and plunger toward the authoritative current value. Set zero for immediate movement.");
        AddField(details, sloshBubblesOnly, scalingRules, "Slosh Affects Bubbles Only", "Routes reactive slosh to the procedural bubbles only: the liquid fills flat up to the current value and the liquid wave and surface-slosh settings are hidden.");
        BuildPalette(details, channel.FindPropertyRelative("palette"), scalingRules, stateSuffix);
        BuildFluid(details, channel.FindPropertyRelative("fluid"), scalingRules, stateSuffix, sloshBubblesOnly);
        BuildMotion(details, channel.FindPropertyRelative("motion"), scalingRules, stateSuffix, sloshBubblesOnly);
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
    /// Builds one direct-color palette foldout with RGBA Add Scaling support.
    /// </summary>
    /// <param name="parent">Parent container receiving the palette foldout.</param>
    /// <param name="palette">Serialized direct palette.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    /// <param name="stateSuffix">Stable foldout-state suffix.</param>
    private static void BuildPalette(VisualElement parent,
                                     SerializedProperty palette,
                                     SerializedProperty scalingRules,
                                     string stateSuffix)
    {
        Foldout foldout = CreateFoldout("Direct Palette", stateSuffix + ".Palette");

        if (palette != null)
        {
            AddField(foldout, palette.FindPropertyRelative("outline"), scalingRules, "Outline", "Color surrounding the complete syringe silhouette.");
            AddField(foldout, palette.FindPropertyRelative("body"), scalingRules, "Body", "Primary flat-shaded syringe body color.");
            AddField(foldout, palette.FindPropertyRelative("bodyShadow"), scalingRules, "Body Shadow", "Secondary faceted body shade.");
            AddField(foldout, palette.FindPropertyRelative("chamber"), scalingRules, "Chamber", "Color used by the empty chamber.");
            AddField(foldout, palette.FindPropertyRelative("liquid"), scalingRules, "Liquid", "Primary liquid color.");
            AddField(foldout, palette.FindPropertyRelative("liquidHighlight"), scalingRules, "Liquid Highlight", "Secondary liquid wave and depth color.");
            AddField(foldout, palette.FindPropertyRelative("bubbles"), scalingRules, "Bubbles", "Procedural air-bubble color.");
            AddField(foldout, palette.FindPropertyRelative("graduation"), scalingRules, "Graduation", "Procedural graduation-tick color.");
            AddField(foldout, palette.FindPropertyRelative("label"), scalingRules, "Label", "Direct numeric-label text color.");
            AddField(foldout, palette.FindPropertyRelative("labelOutline"), scalingRules, "Label Outline", "Direct numeric-label outline color.");
            AddField(foldout, palette.FindPropertyRelative("plunger"), scalingRules, "Plunger", "Primary color used by the moving-plunger frame.");
            AddField(foldout, palette.FindPropertyRelative("plungerWindow"), scalingRules, "Plunger Window", "Semitransparent center color that keeps covered numeric labels readable.");
            AddField(foldout, palette.FindPropertyRelative("terminationOutline"), scalingRules, "Termination Outline", "Direct outline color used by the simplified square termination.");
            AddField(foldout, palette.FindPropertyRelative("terminationInterior"), scalingRules, "Termination Interior", "Fill color used inside the simplified square termination.");
        }

        parent.Add(foldout);
    }

    /// <summary>
    /// Builds conditionally visible liquid-flow and bubble controls.
    /// </summary>
    /// <param name="parent">Parent container receiving the fluid foldout.</param>
    /// <param name="fluid">Serialized fluid settings.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    /// <param name="stateSuffix">Stable foldout-state suffix.</param>
    /// <param name="sloshBubblesOnly">Channel slosh-on-bubbles-only toggle that hides the liquid wave settings when enabled.</param>
    private static void BuildFluid(VisualElement parent,
                                   SerializedProperty fluid,
                                   SerializedProperty scalingRules,
                                   string stateSuffix,
                                   SerializedProperty sloshBubblesOnly)
    {
        Foldout foldout = CreateFoldout("Fluid", stateSuffix + ".Fluid");

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
            // The liquid surface is flat while slosh is routed to the bubbles, so the wave controls become irrelevant.
            waveDetails.style.display = flowOn && (sloshBubblesOnly == null || !sloshBubblesOnly.boolValue)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            bubbleDetails.style.display = bubblesEnabled != null && bubblesEnabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    /// <summary>
    /// Builds conditionally visible movement, tilt, and value-change impulse controls.
    /// </summary>
    /// <param name="parent">Parent container receiving the motion foldout.</param>
    /// <param name="motion">Serialized motion settings.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    /// <param name="stateSuffix">Stable foldout-state suffix.</param>
    /// <param name="sloshBubblesOnly">Channel slosh-on-bubbles-only toggle that hides the surface-slosh setting when enabled.</param>
    private static void BuildMotion(VisualElement parent,
                                    SerializedProperty motion,
                                    SerializedProperty scalingRules,
                                    string stateSuffix,
                                    SerializedProperty sloshBubblesOnly)
    {
        Foldout foldout = CreateFoldout("Reactive Motion", stateSuffix + ".Motion");

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
        AddField(foldout, valueImpulseEnabled, scalingRules, "Value Impulse Enabled", "Enables an additional liquid impulse when the represented value changes.");
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
            // The liquid surface stays flat while slosh is routed to the bubbles, so its slope control is hidden.
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
                                                                "NashCore.PlayerManagement.Visual.HealthBars." + stateSuffix,
                                                                true);
    }
    #endregion

    #region Warnings
    /// <summary>
    /// Registers a low-cost serialized-property tracker for conditional controls and warnings.
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

    /// <summary>
    /// Rebuilds shared health-bar authoring warnings without modifying serialized values.
    /// </summary>
    /// <param name="warnings">Container receiving warning boxes.</param>
    /// <param name="settings">Serialized Health Bars settings root.</param>
    private static void RefreshWarnings(VisualElement warnings, SerializedProperty settings)
    {
        warnings.Clear();
        SerializedProperty units = settings.FindPropertyRelative("unitsPerMajorDivision");
        SerializedProperty pixels = settings.FindPropertyRelative("pixelsPerMajorDivision");
        SerializedProperty minimumLength = settings.FindPropertyRelative("minimumLength");
        SerializedProperty maximumLength = settings.FindPropertyRelative("maximumLength");
        SerializedProperty maximumLabelCount = settings.FindPropertyRelative("maximumLabelCount");
        SerializedProperty labelMinimumSpacing = settings.FindPropertyRelative("labelMinimumSpacing");
        SerializedProperty graduationEndPadding = settings.FindPropertyRelative("graduationEndPadding");
        SerializedProperty terminationOffset = settings.FindPropertyRelative("terminationOffset");
        SerializedProperty fontAsset = settings.FindPropertyRelative("fontAsset");
        SerializedProperty paintDrips = settings.FindPropertyRelative("paintDrips");

        if (units != null && (!IsFinite(units.floatValue) || units.floatValue < 0.1f || units.floatValue > 100f))
            warnings.Add(new HelpBox("Units Per Major Division should be finite and within 0.1-100.", HelpBoxMessageType.Warning));

        if (pixels != null && (!IsFinite(pixels.floatValue) || pixels.floatValue < 8f || pixels.floatValue > 256f))
            warnings.Add(new HelpBox("Pixels Per Major Division should be finite and within 8-256.", HelpBoxMessageType.Warning));

        if (minimumLength != null &&
            maximumLength != null &&
            (!IsFinite(minimumLength.floatValue) ||
             !IsFinite(maximumLength.floatValue) ||
             minimumLength.floatValue < 64f ||
             maximumLength.floatValue > 2048f ||
             maximumLength.floatValue < minimumLength.floatValue))
        {
            warnings.Add(new HelpBox("Minimum and Maximum Length should be finite, ordered, and within 64-2048.",
                                     HelpBoxMessageType.Warning));
        }

        if (maximumLabelCount != null &&
            (maximumLabelCount.intValue < 2 ||
             maximumLabelCount.intValue > PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity))
        {
            warnings.Add(new HelpBox(string.Format("Maximum Label Count exceeds the preauthored supported range 2-{0}.",
                                                   PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity),
                                     HelpBoxMessageType.Warning));
        }

        if (labelMinimumSpacing != null &&
            (!IsFinite(labelMinimumSpacing.floatValue) ||
             labelMinimumSpacing.floatValue < 8f ||
             labelMinimumSpacing.floatValue > 256f))
        {
            warnings.Add(new HelpBox("Label Minimum Spacing should be finite and within 8-256.",
                                     HelpBoxMessageType.Warning));
        }

        if (graduationEndPadding != null &&
            (!IsFinite(graduationEndPadding.floatValue) ||
             graduationEndPadding.floatValue < 0f ||
             graduationEndPadding.floatValue > 256f))
        {
            warnings.Add(new HelpBox("Graduation End Padding should be finite and within 0-256.",
                                     HelpBoxMessageType.Warning));
        }

        if (terminationOffset != null &&
            (!IsFinite(terminationOffset.floatValue) ||
             terminationOffset.floatValue < 0f ||
             terminationOffset.floatValue > 256f))
        {
            warnings.Add(new HelpBox("Termination Offset should be finite and within 0-256.",
                                     HelpBoxMessageType.Warning));
        }

        if (fontAsset != null && fontAsset.objectReferenceValue == null)
        {
            warnings.Add(new HelpBox("Font Asset is not assigned; preauthored label fonts will remain unchanged.",
                                     HelpBoxMessageType.Warning));
        }

        SerializedProperty paintDripsEnabled = paintDrips != null
            ? paintDrips.FindPropertyRelative("enabled")
            : null;

        if (paintDripsEnabled == null || !paintDripsEnabled.boolValue)
            return;

        SerializedProperty dripDensity = paintDrips.FindPropertyRelative("density");
        SerializedProperty dripLength = paintDrips.FindPropertyRelative("length");
        SerializedProperty dripWidth = paintDrips.FindPropertyRelative("width");
        SerializedProperty dripIrregularity = paintDrips.FindPropertyRelative("irregularity");

        if (dripDensity != null &&
            (!IsFinite(dripDensity.floatValue) || dripDensity.floatValue < 0f || dripDensity.floatValue > 1f))
        {
            warnings.Add(new HelpBox("Paint Drip Density should be finite and within 0-1.",
                                     HelpBoxMessageType.Warning));
        }

        if (dripLength != null &&
            (!IsFinite(dripLength.floatValue) || dripLength.floatValue < 0f || dripLength.floatValue > 0.5f))
        {
            warnings.Add(new HelpBox("Paint Drip Length should be finite and within 0-0.5.",
                                     HelpBoxMessageType.Warning));
        }

        if (dripWidth != null &&
            (!IsFinite(dripWidth.floatValue) || dripWidth.floatValue <= 0f || dripWidth.floatValue > 0.25f))
        {
            warnings.Add(new HelpBox("Paint Drip Width should be finite, greater than zero, and no higher than 0.25.",
                                     HelpBoxMessageType.Warning));
        }

        if (dripIrregularity != null &&
            (!IsFinite(dripIrregularity.floatValue) || dripIrregularity.floatValue < 0f || dripIrregularity.floatValue > 1f))
        {
            warnings.Add(new HelpBox("Paint Drip Irregularity should be finite and within 0-1.",
                                     HelpBoxMessageType.Warning));
        }
    }

    /// <summary>
    /// Checks whether one floating-point value is finite.
    /// </summary>
    /// <param name="value">Value to inspect.</param>
    /// <returns>True when the value is neither NaN nor infinity.</returns>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
    #endregion

    #endregion
}
