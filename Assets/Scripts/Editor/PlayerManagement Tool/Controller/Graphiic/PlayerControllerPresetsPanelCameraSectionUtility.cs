using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the camera detail section for the player controller preset panel: follow behavior, follow-spring values
/// and the separate trauma-based damage-shake dropdown. Kept out of the shared sections utility so the camera UI,
/// its conditional visibility and its coherence warnings live in one focused file.
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
    /// Builds the separate damage-shake dropdown with scaling-aware fields, conditional visibility and warnings.
    /// The detail fields only appear while the shake is enabled, and the damage-reference field only appears while
    /// the shake scales with damage, so designers see exactly the knobs that affect the current configuration. A
    /// nested Controller Rumble sub-foldout exposes the connected-gamepad motor amplitudes, which collapse while the
    /// rumble is disabled since they share the shake's trauma envelope.
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

        SerializedProperty enabledProperty = shakeProperty.FindPropertyRelative("enabled");
        SerializedProperty durationProperty = shakeProperty.FindPropertyRelative("durationSeconds");
        SerializedProperty positionalAmplitudeProperty = shakeProperty.FindPropertyRelative("positionalAmplitude");
        SerializedProperty rotationalAmplitudeProperty = shakeProperty.FindPropertyRelative("rotationalAmplitude");
        SerializedProperty frequencyProperty = shakeProperty.FindPropertyRelative("frequency");
        SerializedProperty falloffProperty = shakeProperty.FindPropertyRelative("falloff");
        SerializedProperty scaleWithDamageProperty = shakeProperty.FindPropertyRelative("scaleWithDamage");
        SerializedProperty damageForFullStrengthProperty = shakeProperty.FindPropertyRelative("damageForFullStrength");
        SerializedProperty rumbleEnabledProperty = shakeProperty.FindPropertyRelative("rumbleEnabled");
        SerializedProperty rumbleLowFrequencyProperty = shakeProperty.FindPropertyRelative("rumbleLowFrequency");
        SerializedProperty rumbleHighFrequencyProperty = shakeProperty.FindPropertyRelative("rumbleHighFrequency");

        if (enabledProperty == null ||
            durationProperty == null ||
            positionalAmplitudeProperty == null ||
            rotationalAmplitudeProperty == null ||
            frequencyProperty == null ||
            falloffProperty == null ||
            scaleWithDamageProperty == null ||
            damageForFullStrengthProperty == null ||
            rumbleEnabledProperty == null ||
            rumbleLowFrequencyProperty == null ||
            rumbleHighFrequencyProperty == null)
        {
            return;
        }

        Foldout shakeFoldout = new Foldout();
        shakeFoldout.text = "Damage Shake";
        shakeFoldout.value = false;
        section.Add(shakeFoldout);

        VisualElement enabledField = PlayerScalingFieldElementFactory.CreateField(enabledProperty, scalingRulesProperty, "Enabled");
        shakeFoldout.Add(enabledField);

        // Body holds every detail knob and collapses entirely while the shake is disabled.
        VisualElement shakeBody = new VisualElement();
        shakeBody.style.flexDirection = FlexDirection.Column;
        shakeFoldout.Add(shakeBody);

        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(durationProperty, scalingRulesProperty, "Duration (s)"));
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(positionalAmplitudeProperty, scalingRulesProperty, "Positional Amplitude"));
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(rotationalAmplitudeProperty, scalingRulesProperty, "Rotational Amplitude (deg)"));
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(frequencyProperty, scalingRulesProperty, "Frequency (Hz)"));
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(falloffProperty, scalingRulesProperty, "Falloff"));
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(scaleWithDamageProperty, scalingRulesProperty, "Scale With Damage"));

        VisualElement damageForFullStrengthField = PlayerScalingFieldElementFactory.CreateField(damageForFullStrengthProperty,
                                                                                                scalingRulesProperty,
                                                                                                "Damage For Full Strength");
        shakeBody.Add(damageForFullStrengthField);

        VisualElement shakeWarningsRoot = new VisualElement();
        shakeWarningsRoot.style.marginTop = 4f;
        shakeBody.Add(shakeWarningsRoot);

        // Controller rumble lives inside the shake body so it inherits the master enable: vibrating only makes sense
        // while the damage shake itself accumulates trauma. Its motor knobs collapse while the rumble is disabled.
        Foldout rumbleFoldout = new Foldout();
        rumbleFoldout.text = "Controller Rumble";
        rumbleFoldout.value = false;
        shakeBody.Add(rumbleFoldout);

        VisualElement rumbleEnabledField = PlayerScalingFieldElementFactory.CreateField(rumbleEnabledProperty, scalingRulesProperty, "Enabled");
        rumbleFoldout.Add(rumbleEnabledField);

        VisualElement rumbleBody = new VisualElement();
        rumbleBody.style.flexDirection = FlexDirection.Column;
        rumbleFoldout.Add(rumbleBody);

        rumbleBody.Add(PlayerScalingFieldElementFactory.CreateField(rumbleLowFrequencyProperty, scalingRulesProperty, "Low-Frequency Motor"));
        rumbleBody.Add(PlayerScalingFieldElementFactory.CreateField(rumbleHighFrequencyProperty, scalingRulesProperty, "High-Frequency Motor"));

        VisualElement rumbleWarningsRoot = new VisualElement();
        rumbleWarningsRoot.style.marginTop = 4f;
        rumbleBody.Add(rumbleWarningsRoot);

        System.Action updateShakeView = () =>
        {
            bool shakeEnabled = enabledProperty.boolValue;
            shakeBody.style.display = shakeEnabled ? DisplayStyle.Flex : DisplayStyle.None;

            // The damage reference only matters when the added trauma is scaled by the hit's damage.
            damageForFullStrengthField.style.display = scaleWithDamageProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            PlayerControllerCameraWarningUtility.RefreshShakeValueWarnings(shakeWarningsRoot,
                                                                          shakeEnabled,
                                                                          durationProperty,
                                                                          positionalAmplitudeProperty,
                                                                          rotationalAmplitudeProperty,
                                                                          frequencyProperty,
                                                                          scaleWithDamageProperty,
                                                                          damageForFullStrengthProperty);

            // The motor amplitudes only matter while the rumble itself is enabled.
            bool rumbleEnabled = rumbleEnabledProperty.boolValue;
            rumbleBody.style.display = rumbleEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            PlayerControllerCameraWarningUtility.RefreshRumbleValueWarnings(rumbleWarningsRoot,
                                                                           rumbleEnabled,
                                                                           rumbleLowFrequencyProperty,
                                                                           rumbleHighFrequencyProperty);
        };

        // Toggling enabled/scaleWithDamage or editing a value bubbles a change event that refreshes the view.
        shakeFoldout.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            updateShakeView();
        });

        updateShakeView();
    }

    /// <summary>
    /// Builds the separate fire-shake dropdown rendered right under Damage Shake. The detail fields only appear while
    /// the shake is enabled, and the nested Controller Rumble sub-foldout collapses its motor knobs while the rumble
    /// itself is disabled, mirroring the Damage Shake UI so the two channels read as a matched pair.
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

        SerializedProperty enabledProperty = shakeProperty.FindPropertyRelative("enabled");
        SerializedProperty durationProperty = shakeProperty.FindPropertyRelative("durationSeconds");
        SerializedProperty positionalAmplitudeProperty = shakeProperty.FindPropertyRelative("positionalAmplitude");
        SerializedProperty rotationalAmplitudeProperty = shakeProperty.FindPropertyRelative("rotationalAmplitude");
        SerializedProperty frequencyProperty = shakeProperty.FindPropertyRelative("frequency");
        SerializedProperty falloffProperty = shakeProperty.FindPropertyRelative("falloff");
        SerializedProperty suppressOnLaserBeamProperty = shakeProperty.FindPropertyRelative("suppressOnLaserBeam");
        SerializedProperty rumbleEnabledProperty = shakeProperty.FindPropertyRelative("rumbleEnabled");
        SerializedProperty rumbleLowFrequencyProperty = shakeProperty.FindPropertyRelative("rumbleLowFrequency");
        SerializedProperty rumbleHighFrequencyProperty = shakeProperty.FindPropertyRelative("rumbleHighFrequency");

        if (enabledProperty == null ||
            durationProperty == null ||
            positionalAmplitudeProperty == null ||
            rotationalAmplitudeProperty == null ||
            frequencyProperty == null ||
            falloffProperty == null ||
            suppressOnLaserBeamProperty == null ||
            rumbleEnabledProperty == null ||
            rumbleLowFrequencyProperty == null ||
            rumbleHighFrequencyProperty == null)
        {
            return;
        }

        Foldout shakeFoldout = new Foldout();
        shakeFoldout.text = "Fire Shake";
        shakeFoldout.value = false;
        section.Add(shakeFoldout);

        VisualElement enabledField = PlayerScalingFieldElementFactory.CreateField(enabledProperty, scalingRulesProperty, "Enabled");
        shakeFoldout.Add(enabledField);

        // Body holds every detail knob and collapses entirely while the fire shake is disabled.
        VisualElement shakeBody = new VisualElement();
        shakeBody.style.flexDirection = FlexDirection.Column;
        shakeFoldout.Add(shakeBody);

        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(durationProperty, scalingRulesProperty, "Duration (s)"));
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(positionalAmplitudeProperty, scalingRulesProperty, "Positional Amplitude"));
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(rotationalAmplitudeProperty, scalingRulesProperty, "Rotational Amplitude (deg)"));
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(frequencyProperty, scalingRulesProperty, "Frequency (Hz)"));
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(falloffProperty, scalingRulesProperty, "Falloff"));
        shakeBody.Add(PlayerScalingFieldElementFactory.CreateField(suppressOnLaserBeamProperty, scalingRulesProperty, "Suppress While Laser Firing"));

        VisualElement shakeWarningsRoot = new VisualElement();
        shakeWarningsRoot.style.marginTop = 4f;
        shakeBody.Add(shakeWarningsRoot);

        // Controller rumble lives inside the shake body so it inherits the master enable: vibrating only makes sense
        // while the fire shake itself accumulates trauma. Its motor knobs collapse while the rumble is disabled.
        Foldout rumbleFoldout = new Foldout();
        rumbleFoldout.text = "Controller Rumble";
        rumbleFoldout.value = false;
        shakeBody.Add(rumbleFoldout);

        VisualElement rumbleEnabledField = PlayerScalingFieldElementFactory.CreateField(rumbleEnabledProperty, scalingRulesProperty, "Enabled");
        rumbleFoldout.Add(rumbleEnabledField);

        VisualElement rumbleBody = new VisualElement();
        rumbleBody.style.flexDirection = FlexDirection.Column;
        rumbleFoldout.Add(rumbleBody);

        rumbleBody.Add(PlayerScalingFieldElementFactory.CreateField(rumbleLowFrequencyProperty, scalingRulesProperty, "Low-Frequency Motor"));
        rumbleBody.Add(PlayerScalingFieldElementFactory.CreateField(rumbleHighFrequencyProperty, scalingRulesProperty, "High-Frequency Motor"));

        VisualElement rumbleWarningsRoot = new VisualElement();
        rumbleWarningsRoot.style.marginTop = 4f;
        rumbleBody.Add(rumbleWarningsRoot);

        System.Action updateShakeView = () =>
        {
            bool shakeEnabled = enabledProperty.boolValue;
            shakeBody.style.display = shakeEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            PlayerControllerCameraWarningUtility.RefreshFireShakeValueWarnings(shakeWarningsRoot,
                                                                                shakeEnabled,
                                                                                durationProperty,
                                                                                positionalAmplitudeProperty,
                                                                                rotationalAmplitudeProperty,
                                                                                frequencyProperty);

            // The motor amplitudes only matter while the rumble itself is enabled.
            bool rumbleEnabled = rumbleEnabledProperty.boolValue;
            rumbleBody.style.display = rumbleEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            PlayerControllerCameraWarningUtility.RefreshRumbleValueWarnings(rumbleWarningsRoot,
                                                                            rumbleEnabled,
                                                                            rumbleLowFrequencyProperty,
                                                                            rumbleHighFrequencyProperty);
        };

        shakeFoldout.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            updateShakeView();
        });

        updateShakeView();
    }
    #endregion

    #endregion
}
