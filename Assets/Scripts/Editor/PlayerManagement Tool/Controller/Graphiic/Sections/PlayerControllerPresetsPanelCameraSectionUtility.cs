using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the camera detail section for the player controller preset panel: follow behavior, follow-spring values
/// and the two trauma-based shake dropdowns (Damage + Fire). Each shake exposes the master toggle, the envelope,
/// per-axis enables (Right/Up/Forward), zoom FOV pulse, single-impulse motion mode and a Controller Rumble sub-foldout
/// that can switch between Continuous (envelope-driven) and Single Impulse (clean fixed-duration burst) modes. Fields
/// that depend on other settings are hidden until those settings make them relevant (rule 23) so s only see the
/// knobs that actually affect the current configuration.
/// </summary>
internal static class PlayerControllerPresetsPanelCameraSectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the camera section content into the provided container.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context.</param>
    /// <param name="section">Pre-created section container that receives the camera controls.</param>
    public static void BuildCameraSection(PlayerControllerPresetsPanel panel, VisualElement section)
    {
        if (panel == null || section == null)
            return;

        SerializedProperty cameraProperty = panel.PresetSerializedObject.FindProperty("cameraSettings");

        if (cameraProperty == null)
            return;

        SerializedProperty behaviorProperty = cameraProperty.FindPropertyRelative("behavior");
        SerializedProperty offsetProperty = cameraProperty.FindPropertyRelative("followOffset");
        SerializedProperty anchorProperty = cameraProperty.FindPropertyRelative("roomAnchor");
        SerializedProperty valuesProperty = cameraProperty.FindPropertyRelative("values");
        SerializedProperty scalingRulesProperty = panel.PresetSerializedObject.FindProperty("scalingRules");

        VisualElement behaviorField = PlayerScalingFieldElementFactory.CreateField(behaviorProperty, scalingRulesProperty, "Camera Behavior");
        section.Add(behaviorField);

        VisualElement offsetField = PlayerScalingFieldElementFactory.CreateField(offsetProperty, scalingRulesProperty, "Follow Offset");
        offsetField.tooltip = "Local offset applied by the follow camera relative to the tracked player position.";
        section.Add(offsetField);

        ObjectField anchorField = new ObjectField("Room Anchor");
        anchorField.objectType = typeof(Transform);
        anchorField.BindProperty(anchorProperty);
        section.Add(anchorField);

        Foldout valuesFoldout = PlayerControllerPresetsPanelFieldUtility.BuildValuesFoldout(valuesProperty,
                                                                                            scalingRulesProperty,
                                                                                            new string[]
        {
            "smoothTime",
            "maxFollowDistance",
            "deadZoneRadius"
        });
        section.Add(valuesFoldout);

        SerializedProperty smoothTimeProperty = valuesProperty != null ? valuesProperty.FindPropertyRelative("smoothTime") : null;
        SerializedProperty maxFollowDistanceProperty = valuesProperty != null ? valuesProperty.FindPropertyRelative("maxFollowDistance") : null;
        SerializedProperty deadZoneRadiusProperty = valuesProperty != null ? valuesProperty.FindPropertyRelative("deadZoneRadius") : null;

        VisualElement cameraWarningsRoot = new VisualElement();
        cameraWarningsRoot.style.marginTop = 4f;
        section.Add(cameraWarningsRoot);

        BuildDamageShakeFoldout(section, cameraProperty, scalingRulesProperty);
        BuildFireShakeFoldout(section, cameraProperty, scalingRulesProperty);

        System.Action updateView = () =>
        {
            CameraBehavior behavior = (CameraBehavior)behaviorProperty.enumValueIndex;
            offsetField.style.display = behavior == CameraBehavior.FollowWithOffset ? DisplayStyle.Flex : DisplayStyle.None;
            anchorField.style.display = behavior == CameraBehavior.RoomFixed ? DisplayStyle.Flex : DisplayStyle.None;

            // ChildOfPlayer parents the camera and bypasses the follow spring, so its smoothing values are inert.
            bool usesFollowSpring = behavior != CameraBehavior.ChildOfPlayer;
            valuesFoldout.style.display = usesFollowSpring ? DisplayStyle.Flex : DisplayStyle.None;
            PlayerControllerCameraWarningUtility.RefreshCameraValueWarnings(cameraWarningsRoot,
                                                                           usesFollowSpring,
                                                                           smoothTimeProperty,
                                                                           maxFollowDistanceProperty,
                                                                           deadZoneRadiusProperty);
        };

        behaviorField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            updateView();
        });

        // Camera value edits bubble up from the foldout's bound fields and must refresh the coherence warnings.
        valuesFoldout.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            updateView();
        });

        updateView();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds the Damage Shake dropdown with scaling-aware fields and conditional visibility. The envelope detail block
    /// only shows while the shake is enabled. The damage-reference field only appears while Scale With Damage is on.
    /// Frequency only appears while Motion Mode is Continuous. Forward Amplitude follows the Axis Forward Enabled toggle
    /// so s can see the depth knob only when it can actually push the camera along its forward axis. Zoom FOV
    /// Delta only appears while Zoom is enabled. The Controller Rumble sub-foldout follows the same rule for its impulse
    /// duration: visible only while the rumble itself uses Single Impulse.
    /// </summary>
    /// <param name="section">Section container that receives the shake foldout.</param>
    /// <param name="cameraProperty">Serialized camera settings block owning the damage-shake sub-block.</param>
    /// <param name="scalingRulesProperty">Scaling-rules array used by the scaling-aware shake fields.</param>
    private static void BuildDamageShakeFoldout(VisualElement section,
                                                SerializedProperty cameraProperty,
                                                SerializedProperty scalingRulesProperty)
    {
        SerializedProperty shakeProperty = cameraProperty.FindPropertyRelative("damageShake");

        if (shakeProperty == null)
            return;

        DamageShakeProperties properties = ResolveDamageShakeProperties(shakeProperty);

        if (!properties.IsComplete)
            return;

        Foldout shakeFoldout = new Foldout();
        shakeFoldout.text = "Damage Shake";
        shakeFoldout.value = false;
        section.Add(shakeFoldout);

        VisualElement enabledField = PlayerScalingFieldElementFactory.CreateField(properties.Enabled, scalingRulesProperty, "Enabled");
        shakeFoldout.Add(enabledField);

        // Body holds every detail knob and collapses entirely while the shake is disabled.
        VisualElement shakeBody = new VisualElement();
        shakeBody.style.flexDirection = FlexDirection.Column;
        shakeFoldout.Add(shakeBody);

        Label envelopeHeader = BuildSubHeader("Envelope");
        shakeBody.Add(envelopeHeader);
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(properties.Duration, scalingRulesProperty, "Duration (s)"));
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(properties.Falloff, scalingRulesProperty, "Falloff"));
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(properties.MotionMode, scalingRulesProperty, "Motion Mode"));
        VisualElement frequencyField = PlayerScalingFieldElementFactory.CreateField(properties.Frequency, scalingRulesProperty, "Frequency (Hz)");
        shakeBody.Add(frequencyField);

        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(properties.ScaleWithDamage, scalingRulesProperty, "Scale With Damage"));
        VisualElement damageForFullStrengthField = PlayerScalingFieldElementFactory.CreateField(properties.DamageForFullStrength,
                                                                                                 scalingRulesProperty,
                                                                                                 "Damage For Full Strength");
        shakeBody.Add(damageForFullStrengthField);

        Label axesHeader = BuildSubHeader("Axes & Amplitudes");
        shakeBody.Add(axesHeader);
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(properties.AxisRightEnabled, scalingRulesProperty, "Axis Right (Left-Right)"));
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(properties.AxisUpEnabled, scalingRulesProperty, "Axis Up (Vertical)"));
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(properties.AxisForwardEnabled, scalingRulesProperty, "Axis Forward (Push/Pull)"));
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(properties.PositionalAmplitude, scalingRulesProperty, "Positional Amplitude (Right/Up)"));
        VisualElement forwardAmplitudeField = PlayerScalingFieldElementFactory.CreateField(properties.ForwardAmplitude, scalingRulesProperty, "Forward Amplitude");
        shakeBody.Add(forwardAmplitudeField);
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(properties.RotationalAmplitude, scalingRulesProperty, "Rotational Amplitude (deg)"));

        Label zoomHeader = BuildSubHeader("Zoom");
        shakeBody.Add(zoomHeader);
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(properties.ZoomEnabled, scalingRulesProperty, "Zoom Enabled"));
        VisualElement zoomFovDeltaField = PlayerScalingFieldElementFactory.CreateField(properties.ZoomFovDelta, scalingRulesProperty, "Zoom FOV Delta (deg)");
        shakeBody.Add(zoomFovDeltaField);

        VisualElement shakeWarningsRoot = new VisualElement();
        shakeWarningsRoot.style.marginTop = 4f;
        shakeBody.Add(shakeWarningsRoot);

        Foldout rumbleFoldout = BuildRumbleFoldout(shakeBody,
                                                    properties.RumbleEnabled,
                                                    properties.RumbleMotionMode,
                                                    properties.RumbleImpulseDuration,
                                                    properties.RumbleLowFrequency,
                                                    properties.RumbleHighFrequency,
                                                    scalingRulesProperty,
                                                    out VisualElement rumbleBody,
                                                    out VisualElement rumbleImpulseDurationField,
                                                    out VisualElement rumbleWarningsRoot);

        System.Action updateShakeView = () =>
        {
            bool shakeEnabled = properties.Enabled.boolValue;
            shakeBody.style.display = shakeEnabled ? DisplayStyle.Flex : DisplayStyle.None;

            // Continuous noise frequency is meaningless for SingleImpulse; collapse to avoid misleading authoring.
            bool isContinuous = properties.MotionMode.enumValueIndex == (int)CameraShakeMotionMode.Continuous;
            frequencyField.style.display = isContinuous ? DisplayStyle.Flex : DisplayStyle.None;

            // The damage reference only matters when the added trauma is scaled by the hit's damage.
            damageForFullStrengthField.style.display = properties.ScaleWithDamage.boolValue ? DisplayStyle.Flex : DisplayStyle.None;

            // Forward Amplitude only matters while the forward axis is enabled; same logic for the zoom delta.
            forwardAmplitudeField.style.display = properties.AxisForwardEnabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            zoomFovDeltaField.style.display = properties.ZoomEnabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;

            PlayerControllerCameraWarningUtility.RefreshShakeValueWarnings(shakeWarningsRoot,
                                                                          shakeEnabled,
                                                                          properties.Duration,
                                                                          properties.PositionalAmplitude,
                                                                          properties.RotationalAmplitude,
                                                                          properties.Frequency,
                                                                          properties.ScaleWithDamage,
                                                                          properties.DamageForFullStrength,
                                                                          properties.MotionMode,
                                                                          properties.AxisRightEnabled,
                                                                          properties.AxisUpEnabled,
                                                                          properties.AxisForwardEnabled,
                                                                          properties.ForwardAmplitude,
                                                                          properties.ZoomEnabled,
                                                                          properties.ZoomFovDelta);

            bool rumbleEnabled = properties.RumbleEnabled.boolValue;
            rumbleBody.style.display = rumbleEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            bool rumbleIsImpulse = properties.RumbleMotionMode.enumValueIndex == (int)CameraShakeRumbleMotionMode.SingleImpulse;
            rumbleImpulseDurationField.style.display = rumbleIsImpulse ? DisplayStyle.Flex : DisplayStyle.None;
            PlayerControllerCameraWarningUtility.RefreshRumbleValueWarnings(rumbleWarningsRoot,
                                                                           rumbleEnabled,
                                                                           properties.RumbleLowFrequency,
                                                                           properties.RumbleHighFrequency,
                                                                           properties.RumbleMotionMode,
                                                                           properties.RumbleImpulseDuration);
        };

        shakeFoldout.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            updateShakeView();
        });

        updateShakeView();
    }

    /// <summary>
    /// Builds the Fire Shake dropdown rendered right under Damage Shake. Mirrors the Damage Shake authoring surface
    /// minus the damage-scaling block, keeping the same conditional-display rules so the two channels read as a
    /// matched pair.
    /// </summary>
    /// <param name="section">Section container that receives the fire shake foldout.</param>
    /// <param name="cameraProperty">Serialized camera settings block owning the fire-shake sub-block.</param>
    /// <param name="scalingRulesProperty">Scaling-rules array used by the scaling-aware fire shake fields.</param>
    private static void BuildFireShakeFoldout(VisualElement section,
                                                SerializedProperty cameraProperty,
                                                SerializedProperty scalingRulesProperty)
    {
        SerializedProperty shakeProperty = cameraProperty.FindPropertyRelative("fireShake");

        if (shakeProperty == null)
            return;

        FireShakeProperties properties = ResolveFireShakeProperties(shakeProperty);

        if (!properties.IsComplete)
            return;

        Foldout shakeFoldout = new Foldout();
        shakeFoldout.text = "Fire Shake";
        shakeFoldout.value = false;
        section.Add(shakeFoldout);

        VisualElement enabledField = PlayerScalingFieldElementFactory.CreateField(properties.Enabled, scalingRulesProperty, "Enabled");
        shakeFoldout.Add(enabledField);

        VisualElement shakeBody = new VisualElement();
        shakeBody.style.flexDirection = FlexDirection.Column;
        shakeFoldout.Add(shakeBody);

        Label envelopeHeader = BuildSubHeader("Envelope");
        shakeBody.Add(envelopeHeader);
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(properties.Duration, scalingRulesProperty, "Duration (s)"));
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(properties.Falloff, scalingRulesProperty, "Falloff"));
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(properties.MotionMode, scalingRulesProperty, "Motion Mode"));
        VisualElement frequencyField = PlayerScalingFieldElementFactory.CreateField(properties.Frequency, scalingRulesProperty, "Frequency (Hz)");
        shakeBody.Add(frequencyField);
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(properties.SuppressOnLaserBeam, scalingRulesProperty, "Suppress While Laser Firing"));

        Label axesHeader = BuildSubHeader("Axes & Amplitudes");
        shakeBody.Add(axesHeader);
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(properties.AxisRightEnabled, scalingRulesProperty, "Axis Right (Left-Right)"));
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(properties.AxisUpEnabled, scalingRulesProperty, "Axis Up (Vertical)"));
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(properties.AxisForwardEnabled, scalingRulesProperty, "Axis Forward (Push/Pull)"));
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(properties.PositionalAmplitude, scalingRulesProperty, "Positional Amplitude (Right/Up)"));
        VisualElement forwardAmplitudeField = PlayerScalingFieldElementFactory.CreateField(properties.ForwardAmplitude, scalingRulesProperty, "Forward Amplitude");
        shakeBody.Add(forwardAmplitudeField);
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(properties.RotationalAmplitude, scalingRulesProperty, "Rotational Amplitude (deg)"));

        Label zoomHeader = BuildSubHeader("Zoom");
        shakeBody.Add(zoomHeader);
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(properties.ZoomEnabled, scalingRulesProperty, "Zoom Enabled"));
        VisualElement zoomFovDeltaField = PlayerScalingFieldElementFactory.CreateField(properties.ZoomFovDelta, scalingRulesProperty, "Zoom FOV Delta (deg)");
        shakeBody.Add(zoomFovDeltaField);

        VisualElement shakeWarningsRoot = new VisualElement();
        shakeWarningsRoot.style.marginTop = 4f;
        shakeBody.Add(shakeWarningsRoot);

        Foldout rumbleFoldout = BuildRumbleFoldout(shakeBody,
                                                    properties.RumbleEnabled,
                                                    properties.RumbleMotionMode,
                                                    properties.RumbleImpulseDuration,
                                                    properties.RumbleLowFrequency,
                                                    properties.RumbleHighFrequency,
                                                    scalingRulesProperty,
                                                    out VisualElement rumbleBody,
                                                    out VisualElement rumbleImpulseDurationField,
                                                    out VisualElement rumbleWarningsRoot);

        System.Action updateShakeView = () =>
        {
            bool shakeEnabled = properties.Enabled.boolValue;
            shakeBody.style.display = shakeEnabled ? DisplayStyle.Flex : DisplayStyle.None;

            bool isContinuous = properties.MotionMode.enumValueIndex == (int)CameraShakeMotionMode.Continuous;
            frequencyField.style.display = isContinuous ? DisplayStyle.Flex : DisplayStyle.None;

            forwardAmplitudeField.style.display = properties.AxisForwardEnabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            zoomFovDeltaField.style.display = properties.ZoomEnabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;

            PlayerControllerCameraWarningUtility.RefreshFireShakeValueWarnings(shakeWarningsRoot,
                                                                                shakeEnabled,
                                                                                properties.Duration,
                                                                                properties.PositionalAmplitude,
                                                                                properties.RotationalAmplitude,
                                                                                properties.Frequency,
                                                                                properties.MotionMode,
                                                                                properties.AxisRightEnabled,
                                                                                properties.AxisUpEnabled,
                                                                                properties.AxisForwardEnabled,
                                                                                properties.ForwardAmplitude,
                                                                                properties.ZoomEnabled,
                                                                                properties.ZoomFovDelta);

            bool rumbleEnabled = properties.RumbleEnabled.boolValue;
            rumbleBody.style.display = rumbleEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            bool rumbleIsImpulse = properties.RumbleMotionMode.enumValueIndex == (int)CameraShakeRumbleMotionMode.SingleImpulse;
            rumbleImpulseDurationField.style.display = rumbleIsImpulse ? DisplayStyle.Flex : DisplayStyle.None;
            PlayerControllerCameraWarningUtility.RefreshRumbleValueWarnings(rumbleWarningsRoot,
                                                                            rumbleEnabled,
                                                                            properties.RumbleLowFrequency,
                                                                            properties.RumbleHighFrequency,
                                                                            properties.RumbleMotionMode,
                                                                            properties.RumbleImpulseDuration);
        };

        shakeFoldout.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            updateShakeView();
        });

        updateShakeView();
    }

    /// <summary>
    /// Builds the Controller Rumble sub-foldout shared by both shake channels. Returns the inner body and the
    /// impulse-duration field so the parent foldout can drive the conditional visibility from its own callbacks.
    /// </summary>
    /// <param name="parentBody">Parent shake body that hosts the rumble foldout.</param>
    /// <param name="rumbleEnabledProperty">Serialized master rumble toggle.</param>
    /// <param name="rumbleMotionModeProperty">Serialized rumble motion-mode enum.</param>
    /// <param name="rumbleImpulseDurationProperty">Serialized SingleImpulse rumble duration.</param>
    /// <param name="rumbleLowFrequencyProperty">Serialized low-frequency motor amplitude.</param>
    /// <param name="rumbleHighFrequencyProperty">Serialized high-frequency motor amplitude.</param>
    /// <param name="scalingRulesProperty">Scaling-rules list used by the scaling-aware rumble fields.</param>
    /// <param name="rumbleBody">Outputs the body container so the parent can toggle it with the master enable.</param>
    /// <param name="rumbleImpulseDurationField">Outputs the impulse duration field so the parent can show/hide it.</param>
    /// <param name="rumbleWarningsRoot">Outputs the warnings container so the parent can refresh it.</param>
    /// <returns>Configured rumble foldout already added to the parent body.</returns>
    private static Foldout BuildRumbleFoldout(VisualElement parentBody,
                                               SerializedProperty rumbleEnabledProperty,
                                               SerializedProperty rumbleMotionModeProperty,
                                               SerializedProperty rumbleImpulseDurationProperty,
                                               SerializedProperty rumbleLowFrequencyProperty,
                                               SerializedProperty rumbleHighFrequencyProperty,
                                               SerializedProperty scalingRulesProperty,
                                               out VisualElement rumbleBody,
                                               out VisualElement rumbleImpulseDurationField,
                                               out VisualElement rumbleWarningsRoot)
    {
        Foldout rumbleFoldout = new Foldout();
        rumbleFoldout.text = "Controller Rumble";
        rumbleFoldout.value = false;
        parentBody.Add(rumbleFoldout);

        VisualElement rumbleEnabledField = PlayerScalingFieldElementFactory.CreateField(rumbleEnabledProperty, scalingRulesProperty, "Enabled");
        rumbleFoldout.Add(rumbleEnabledField);

        rumbleBody = new VisualElement();
        rumbleBody.style.flexDirection = FlexDirection.Column;
        rumbleFoldout.Add(rumbleBody);

        rumbleBody.Add(PlayerScalingFieldElementFactory.CreateField(rumbleMotionModeProperty, scalingRulesProperty, "Rumble Motion Mode"));
        rumbleImpulseDurationField = PlayerScalingFieldElementFactory.CreateField(rumbleImpulseDurationProperty, scalingRulesProperty, "Impulse Duration (s)");
        rumbleBody.Add(rumbleImpulseDurationField);
        rumbleBody.Add(PlayerScalingFieldElementFactory.CreateField(rumbleLowFrequencyProperty, scalingRulesProperty, "Low-Frequency Motor"));
        rumbleBody.Add(PlayerScalingFieldElementFactory.CreateField(rumbleHighFrequencyProperty, scalingRulesProperty, "High-Frequency Motor"));

        rumbleWarningsRoot = new VisualElement();
        rumbleWarningsRoot.style.marginTop = 4f;
        rumbleBody.Add(rumbleWarningsRoot);
        return rumbleFoldout;
    }

    /// <summary>
    /// Resolves the damage-shake serialized properties into a single struct so the foldout body and the update callback
    /// can both reuse them without re-walking the SerializedProperty tree.
    /// </summary>
    /// <param name="shakeProperty">Serialized damage-shake settings block.</param>
    /// <returns>Resolved property bundle; <see cref="DamageShakeProperties.IsComplete"/> is true when every required relative was found.</returns>
    private static DamageShakeProperties ResolveDamageShakeProperties(SerializedProperty shakeProperty)
    {
        DamageShakeProperties bundle = default;
        bundle.Enabled = shakeProperty.FindPropertyRelative("enabled");
        bundle.Duration = shakeProperty.FindPropertyRelative("durationSeconds");
        bundle.Falloff = shakeProperty.FindPropertyRelative("falloff");
        bundle.MotionMode = shakeProperty.FindPropertyRelative("motionMode");
        bundle.Frequency = shakeProperty.FindPropertyRelative("frequency");
        bundle.ScaleWithDamage = shakeProperty.FindPropertyRelative("scaleWithDamage");
        bundle.DamageForFullStrength = shakeProperty.FindPropertyRelative("damageForFullStrength");
        bundle.AxisRightEnabled = shakeProperty.FindPropertyRelative("axisRightEnabled");
        bundle.AxisUpEnabled = shakeProperty.FindPropertyRelative("axisUpEnabled");
        bundle.AxisForwardEnabled = shakeProperty.FindPropertyRelative("axisForwardEnabled");
        bundle.PositionalAmplitude = shakeProperty.FindPropertyRelative("positionalAmplitude");
        bundle.ForwardAmplitude = shakeProperty.FindPropertyRelative("forwardAmplitude");
        bundle.RotationalAmplitude = shakeProperty.FindPropertyRelative("rotationalAmplitude");
        bundle.ZoomEnabled = shakeProperty.FindPropertyRelative("zoomEnabled");
        bundle.ZoomFovDelta = shakeProperty.FindPropertyRelative("zoomFovDelta");
        bundle.RumbleEnabled = shakeProperty.FindPropertyRelative("rumbleEnabled");
        bundle.RumbleMotionMode = shakeProperty.FindPropertyRelative("rumbleMotionMode");
        bundle.RumbleImpulseDuration = shakeProperty.FindPropertyRelative("rumbleImpulseDurationSeconds");
        bundle.RumbleLowFrequency = shakeProperty.FindPropertyRelative("rumbleLowFrequency");
        bundle.RumbleHighFrequency = shakeProperty.FindPropertyRelative("rumbleHighFrequency");
        return bundle;
    }

    /// <summary>
    /// Resolves the fire-shake serialized properties into a single struct so the foldout body and the update callback
    /// can both reuse them without re-walking the SerializedProperty tree.
    /// </summary>
    /// <param name="shakeProperty">Serialized fire-shake settings block.</param>
    /// <returns>Resolved property bundle; <see cref="FireShakeProperties.IsComplete"/> is true when every required relative was found.</returns>
    private static FireShakeProperties ResolveFireShakeProperties(SerializedProperty shakeProperty)
    {
        FireShakeProperties bundle = default;
        bundle.Enabled = shakeProperty.FindPropertyRelative("enabled");
        bundle.Duration = shakeProperty.FindPropertyRelative("durationSeconds");
        bundle.Falloff = shakeProperty.FindPropertyRelative("falloff");
        bundle.MotionMode = shakeProperty.FindPropertyRelative("motionMode");
        bundle.Frequency = shakeProperty.FindPropertyRelative("frequency");
        bundle.SuppressOnLaserBeam = shakeProperty.FindPropertyRelative("suppressOnLaserBeam");
        bundle.AxisRightEnabled = shakeProperty.FindPropertyRelative("axisRightEnabled");
        bundle.AxisUpEnabled = shakeProperty.FindPropertyRelative("axisUpEnabled");
        bundle.AxisForwardEnabled = shakeProperty.FindPropertyRelative("axisForwardEnabled");
        bundle.PositionalAmplitude = shakeProperty.FindPropertyRelative("positionalAmplitude");
        bundle.ForwardAmplitude = shakeProperty.FindPropertyRelative("forwardAmplitude");
        bundle.RotationalAmplitude = shakeProperty.FindPropertyRelative("rotationalAmplitude");
        bundle.ZoomEnabled = shakeProperty.FindPropertyRelative("zoomEnabled");
        bundle.ZoomFovDelta = shakeProperty.FindPropertyRelative("zoomFovDelta");
        bundle.RumbleEnabled = shakeProperty.FindPropertyRelative("rumbleEnabled");
        bundle.RumbleMotionMode = shakeProperty.FindPropertyRelative("rumbleMotionMode");
        bundle.RumbleImpulseDuration = shakeProperty.FindPropertyRelative("rumbleImpulseDurationSeconds");
        bundle.RumbleLowFrequency = shakeProperty.FindPropertyRelative("rumbleLowFrequency");
        bundle.RumbleHighFrequency = shakeProperty.FindPropertyRelative("rumbleHighFrequency");
        return bundle;
    }

    /// <summary>
    /// Builds one bold subsection header label used to thematically separate Envelope, Axes &amp; Amplitudes and Zoom blocks.
    /// </summary>
    /// <param name="title">Header text.</param>
    /// <returns>Configured Label ready to insert into the parent container.</returns>
    private static Label BuildSubHeader(string title)
    {
        Label header = new Label(title);
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginTop = 6f;
        header.style.marginBottom = 2f;
        return header;
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Bundles the damage-shake serialized properties so the body builder and the update callback share one resolution.
    /// </summary>
    private struct DamageShakeProperties
    {
        public SerializedProperty Enabled;
        public SerializedProperty Duration;
        public SerializedProperty Falloff;
        public SerializedProperty MotionMode;
        public SerializedProperty Frequency;
        public SerializedProperty ScaleWithDamage;
        public SerializedProperty DamageForFullStrength;
        public SerializedProperty AxisRightEnabled;
        public SerializedProperty AxisUpEnabled;
        public SerializedProperty AxisForwardEnabled;
        public SerializedProperty PositionalAmplitude;
        public SerializedProperty ForwardAmplitude;
        public SerializedProperty RotationalAmplitude;
        public SerializedProperty ZoomEnabled;
        public SerializedProperty ZoomFovDelta;
        public SerializedProperty RumbleEnabled;
        public SerializedProperty RumbleMotionMode;
        public SerializedProperty RumbleImpulseDuration;
        public SerializedProperty RumbleLowFrequency;
        public SerializedProperty RumbleHighFrequency;

        public bool IsComplete
        {
            get
            {
                return Enabled != null &&
                       Duration != null &&
                       Falloff != null &&
                       MotionMode != null &&
                       Frequency != null &&
                       ScaleWithDamage != null &&
                       DamageForFullStrength != null &&
                       AxisRightEnabled != null &&
                       AxisUpEnabled != null &&
                       AxisForwardEnabled != null &&
                       PositionalAmplitude != null &&
                       ForwardAmplitude != null &&
                       RotationalAmplitude != null &&
                       ZoomEnabled != null &&
                       ZoomFovDelta != null &&
                       RumbleEnabled != null &&
                       RumbleMotionMode != null &&
                       RumbleImpulseDuration != null &&
                       RumbleLowFrequency != null &&
                       RumbleHighFrequency != null;
            }
        }
    }

    /// <summary>
    /// Bundles the fire-shake serialized properties so the body builder and the update callback share one resolution.
    /// </summary>
    private struct FireShakeProperties
    {
        public SerializedProperty Enabled;
        public SerializedProperty Duration;
        public SerializedProperty Falloff;
        public SerializedProperty MotionMode;
        public SerializedProperty Frequency;
        public SerializedProperty SuppressOnLaserBeam;
        public SerializedProperty AxisRightEnabled;
        public SerializedProperty AxisUpEnabled;
        public SerializedProperty AxisForwardEnabled;
        public SerializedProperty PositionalAmplitude;
        public SerializedProperty ForwardAmplitude;
        public SerializedProperty RotationalAmplitude;
        public SerializedProperty ZoomEnabled;
        public SerializedProperty ZoomFovDelta;
        public SerializedProperty RumbleEnabled;
        public SerializedProperty RumbleMotionMode;
        public SerializedProperty RumbleImpulseDuration;
        public SerializedProperty RumbleLowFrequency;
        public SerializedProperty RumbleHighFrequency;

        public bool IsComplete
        {
            get
            {
                return Enabled != null &&
                       Duration != null &&
                       Falloff != null &&
                       MotionMode != null &&
                       Frequency != null &&
                       SuppressOnLaserBeam != null &&
                       AxisRightEnabled != null &&
                       AxisUpEnabled != null &&
                       AxisForwardEnabled != null &&
                       PositionalAmplitude != null &&
                       ForwardAmplitude != null &&
                       RotationalAmplitude != null &&
                       ZoomEnabled != null &&
                       ZoomFovDelta != null &&
                       RumbleEnabled != null &&
                       RumbleMotionMode != null &&
                       RumbleImpulseDuration != null &&
                       RumbleLowFrequency != null &&
                       RumbleHighFrequency != null;
            }
        }
    }
    #endregion
}
