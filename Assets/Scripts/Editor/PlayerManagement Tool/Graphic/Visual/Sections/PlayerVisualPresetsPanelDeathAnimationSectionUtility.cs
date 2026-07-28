using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the "Death Animation" subsection of the player visual preset panel. Exposes the cinematic camera tween (FOV
/// zoom delta and camera-to-player dolly) and the optional despawn VFX (prefab, offset, scale, lifetime and normalized
/// spawn time) authored on <see cref="PlayerDeathAnimationSettings"/>. Numeric fields route through the unified Add
/// Scaling factory; secondary controls collapse out of view when their parent toggle is off so the inspector only
/// surfaces options that actually take effect under the current configuration (project rule 23). Payback Duration is
/// hidden with the dependent fields when the master toggle is off because disabled animations finalize defeat instantly.
/// </summary>
internal static class PlayerVisualPresetsPanelDeathAnimationSectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the Death Animation subsection content for the provided panel.
    /// </summary>
    /// <param name="panel">Owning player visual preset panel providing the serialized preset.</param>
    /// <param name="container">Section container that receives the fields.</param>
    public static void Build(PlayerVisualPresetsPanel panel, VisualElement container)
    {
        if (panel == null || container == null)
            return;

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;

        if (presetSerializedObject == null)
            return;

        SerializedProperty scalingRulesProperty = presetSerializedObject.FindProperty("scalingRules");
        SerializedProperty deathAnimationProperty = presetSerializedObject.FindProperty("deathAnimation");

        if (deathAnimationProperty == null)
        {
            container.Add(new HelpBox("Death Animation block is missing on this preset. Reopen the asset after recompiling.", HelpBoxMessageType.Warning));
            return;
        }

        DeathAnimationProperties properties = ResolveProperties(deathAnimationProperty);

        if (!properties.IsComplete)
        {
            container.Add(new HelpBox("Death Animation fields are missing on this preset. Reopen the asset after recompiling.", HelpBoxMessageType.Warning));
            return;
        }

        AddScalableField(container, properties.Enabled, scalingRulesProperty, "Enabled", "Master toggle for the death camera animation. When disabled the whole payback playback is skipped, the camera is left untouched, the despawn VFX is not spawned and the end-of-run UI appears immediately.");

        VisualElement detailsContainer = new VisualElement();
        detailsContainer.style.flexDirection = FlexDirection.Column;
        container.Add(detailsContainer);
        AddScalableField(detailsContainer, properties.PlaybackDuration, scalingRulesProperty, "Payback Duration (s)", "Seconds the run keeps playing damage feedbacks and this death animation after the lethal hit before the end-of-run UI is shown. Ignored when Enabled is off. 0 shows the end UI on the same frame as the lethal hit.");

        BuildCameraTweenBlock(detailsContainer,
                              scalingRulesProperty,
                              properties,
                              out VisualElement zoomDeltaField,
                              out VisualElement positionLerpField,
                              out VisualElement cameraCompletionField);
        BuildDespawnVfxBlock(detailsContainer,
                              scalingRulesProperty,
                              properties,
                              out VisualElement vfxDetailsContainer);
        BuildVisualBridgeBlock(detailsContainer,
                                scalingRulesProperty,
                                properties);
        BuildImpactFrameBlock(detailsContainer,
                              scalingRulesProperty,
                              properties,
                              out VisualElement impactFrameDetails);

        VisualElement warningsContainer = new VisualElement();
        warningsContainer.style.marginTop = 4f;
        detailsContainer.Add(warningsContainer);

        System.Action updateView = () =>
        {
            bool enabled = properties.Enabled.boolValue;
            detailsContainer.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;

            // FOV delta only matters while Camera Zoom is enabled; same logic for the position lerp amount.
            zoomDeltaField.style.display = properties.CameraZoomEnabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            positionLerpField.style.display = properties.CameraPositionLerpEnabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            bool anyCameraTweenEnabled = properties.CameraZoomEnabled.boolValue || properties.CameraPositionLerpEnabled.boolValue;
            cameraCompletionField.style.display = anyCameraTweenEnabled ? DisplayStyle.Flex : DisplayStyle.None;

            // VFX details only matter when a prefab is assigned.
            vfxDetailsContainer.style.display = properties.DespawnVfxPrefab.objectReferenceValue != null ? DisplayStyle.Flex : DisplayStyle.None;
            impactFrameDetails.style.display = properties.ImpactFrameEnabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;

            RefreshWarnings(warningsContainer, properties);
        };

        deathAnimationProperty.serializedObject.Update();
        detailsContainer.TrackPropertyValue(properties.Enabled, _ => updateView());
        detailsContainer.TrackPropertyValue(properties.CameraZoomEnabled, _ => updateView());
        detailsContainer.TrackPropertyValue(properties.CameraPositionLerpEnabled, _ => updateView());
        detailsContainer.TrackPropertyValue(properties.DespawnVfxPrefab, _ => updateView());
        detailsContainer.TrackPropertyValue(properties.PlaybackDuration, _ => updateView());
        detailsContainer.TrackPropertyValue(properties.CameraTargetFovDelta, _ => updateView());
        detailsContainer.TrackPropertyValue(properties.CameraPositionLerpAmount, _ => updateView());
        detailsContainer.TrackPropertyValue(properties.CameraCompletionNormalizedTime, _ => updateView());
        detailsContainer.TrackPropertyValue(properties.DespawnVfxScaleMultiplier, _ => updateView());
        detailsContainer.TrackPropertyValue(properties.DespawnVfxLifetimeSeconds, _ => updateView());
        detailsContainer.TrackPropertyValue(properties.DespawnVfxSpawnNormalizedTime, _ => updateView());
        detailsContainer.TrackPropertyValue(properties.ImpactFrameEnabled, _ => updateView());
        detailsContainer.TrackPropertyValue(properties.ImpactFrameBuildInStartNormalizedTime, _ => updateView());
        detailsContainer.TrackPropertyValue(properties.ImpactFrameApplyNormalizedTime, _ => updateView());
        detailsContainer.TrackPropertyValue(properties.ImpactFrameEndNormalizedTime, _ => updateView());
        updateView();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds the Camera Tween block: zoom toggle, target FOV delta, dolly toggle, dolly amount, easing.
    /// </summary>
    /// <param name="parent">Parent container that receives the block.</param>
    /// <param name="scalingRulesProperty">Scaling rules list used by the scaling-aware fields.</param>
    /// <param name="properties">Resolved property bundle for the death animation block.</param>
    /// <param name="zoomDeltaField">Outputs the FOV delta field so the parent can toggle its visibility.</param>
    /// <param name="positionLerpField">Outputs the dolly amount field so the parent can toggle its visibility.</param>
    /// <param name="cameraCompletionField">Outputs the camera completion field so the parent can toggle its visibility.</param>
    private static void BuildCameraTweenBlock(VisualElement parent,
                                               SerializedProperty scalingRulesProperty,
                                               DeathAnimationProperties properties,
                                               out VisualElement zoomDeltaField,
                                               out VisualElement positionLerpField,
                                               out VisualElement cameraCompletionField)
    {
        Label header = BuildSubHeader("Camera Tween");
        parent.Add(header);
        AddScalableField(parent, properties.CameraZoomEnabled, scalingRulesProperty, "Camera Zoom Enabled", "When enabled, the camera FOV pulses toward base FOV plus Target FOV Delta over the dying window.");
        zoomDeltaField = PlayerScalingFieldElementFactory.CreateField(properties.CameraTargetFovDelta, scalingRulesProperty, "Target FOV Delta (deg)");
        zoomDeltaField.tooltip = "Peak FOV delta in degrees applied at the end of the dying window. Negative = zoom IN, positive = zoom OUT.";
        parent.Add(zoomDeltaField);

        AddScalableField(parent, properties.CameraPositionLerpEnabled, scalingRulesProperty, "Camera Dolly Enabled", "When enabled, the camera slides toward the player position over the dying window.");
        positionLerpField = PlayerScalingFieldElementFactory.CreateField(properties.CameraPositionLerpAmount, scalingRulesProperty, "Camera Dolly Amount");
        positionLerpField.tooltip = "0 keeps the captured camera position, 1 fully snaps the camera onto the player. Subtle values (0.2-0.5) feel cinematic.";
        parent.Add(positionLerpField);

        cameraCompletionField = PlayerScalingFieldElementFactory.CreateField(properties.CameraCompletionNormalizedTime, scalingRulesProperty, "Camera Completion Time");
        cameraCompletionField.tooltip = "Normalized Payback Duration fraction where camera zoom and dolly complete. 1 uses the full payback; 0 completes immediately and holds the final pose.";
        parent.Add(cameraCompletionField);

        AddScalableField(parent, properties.EasingMode, scalingRulesProperty, "Easing", "Curve applied to the animation parametric time. Linear, Smooth (smoothstep), EaseIn (slow start), EaseOut (slow finish).");
    }

    /// <summary>
    /// Builds the Despawn VFX block: prefab field plus collapsing details (offset, scale, spawn time, lifetime).
    /// </summary>
    /// <param name="parent">Parent container that receives the block.</param>
    /// <param name="scalingRulesProperty">Scaling rules list used by the scaling-aware fields.</param>
    /// <param name="properties">Resolved property bundle for the death animation block.</param>
    /// <param name="vfxDetailsContainer">Outputs the details container so the parent can toggle its visibility.</param>
    private static void BuildDespawnVfxBlock(VisualElement parent,
                                              SerializedProperty scalingRulesProperty,
                                              DeathAnimationProperties properties,
                                              out VisualElement vfxDetailsContainer)
    {
        Label header = BuildSubHeader("Despawn VFX");
        parent.Add(header);

        PropertyField prefabField = new PropertyField(properties.DespawnVfxPrefab, "Despawn VFX Prefab");
        prefabField.BindProperty(properties.DespawnVfxPrefab);
        prefabField.tooltip = "Optional one-shot VFX prefab spawned on the player while the death animation plays.";
        parent.Add(prefabField);

        vfxDetailsContainer = new VisualElement();
        vfxDetailsContainer.style.flexDirection = FlexDirection.Column;
        parent.Add(vfxDetailsContainer);

        AddScalableField(vfxDetailsContainer, properties.DespawnVfxSpawnOffset, scalingRulesProperty, "Spawn Offset", "Local-space offset applied to the VFX instance relative to the player position at spawn time.");
        AddScalableField(vfxDetailsContainer, properties.DespawnVfxScaleMultiplier, scalingRulesProperty, "Scale Multiplier", "Uniform scale multiplier applied to the VFX instance.");
        AddScalableField(vfxDetailsContainer, properties.DespawnVfxSpawnNormalizedTime, scalingRulesProperty, "Spawn Normalized Time", "Normalized animation time (0 = lethal hit frame, 1 = end of dying window) at which the VFX is spawned.");
        AddScalableField(vfxDetailsContainer, properties.DespawnVfxLifetimeSeconds, scalingRulesProperty, "Lifetime (s)", "Seconds before the spawned VFX instance is destroyed.");
    }

    /// <summary>
    /// Builds the Visual Bridge block: the hide-on-VFX toggle plus any future bridge-related options.
    /// </summary>
    /// <param name="parent">Parent container that receives the block.</param>
    /// <param name="scalingRulesProperty">Scaling rules list used by the scaling-aware fields.</param>
    /// <param name="properties">Resolved property bundle for the death animation block.</param>
    private static void BuildVisualBridgeBlock(VisualElement parent,
                                                SerializedProperty scalingRulesProperty,
                                                DeathAnimationProperties properties)
    {
        Label header = BuildSubHeader("Visual Bridge");
        parent.Add(header);
        AddScalableField(parent, properties.HidePlayerVisualOnVfxSpawn, scalingRulesProperty, "Hide Player Visual On VFX Spawn", "When enabled, the runtime visual bridge GameObject is hidden the frame the despawn VFX spawns so the VFX visually replaces the player.");
    }

    /// <summary>
    /// Builds the death-owned Impact Frame timeline markers and the full reusable Impact Frame payload editor.
    /// </summary>
    /// <param name="parent">Parent container receiving the block.</param>
    /// <param name="scalingRulesProperty">Scaling rules list used by marker fields.</param>
    /// <param name="properties">Resolved death animation properties.</param>
    /// <param name="detailsContainer">Outputs the details container toggled by Impact Frame Enabled.</param>
    private static void BuildImpactFrameBlock(VisualElement parent,
                                              SerializedProperty scalingRulesProperty,
                                              DeathAnimationProperties properties,
                                              out VisualElement detailsContainer)
    {
        parent.Add(BuildSubHeader("Impact Frame"));
        AddScalableField(parent,
                         properties.ImpactFrameEnabled,
                         scalingRulesProperty,
                         "Impact Frame Enabled",
                         "Drives build-in and final Impact Frame effects across the death playback timeline.");
        detailsContainer = new VisualElement();
        parent.Add(detailsContainer);
        AddScalableField(detailsContainer,
                         properties.ImpactFrameBuildInStartNormalizedTime,
                         scalingRulesProperty,
                         "Build-In Start Normalized Time",
                         "Normalized death playback point where build-in starts.");
        AddScalableField(detailsContainer,
                         properties.ImpactFrameApplyNormalizedTime,
                         scalingRulesProperty,
                         "Apply Normalized Time",
                         "Normalized death playback point where the final Impact Frame activates.");
        AddScalableField(detailsContainer,
                         properties.ImpactFrameEndNormalizedTime,
                         scalingRulesProperty,
                         "End Normalized Time",
                         "Normalized death playback point where the final Impact Frame is cleared.");
        PowerUpImpactFramePayloadDrawerUtility.BuildImpactFramePayloadUi(detailsContainer, properties.ImpactFrame);
    }

    /// <summary>
    /// Refreshes the warning HelpBoxes for the death animation section. Warnings are emitted instead of snapping per
    /// project rule 20: the validator only clamps hard floors so s see the issues without losing data.
    /// </summary>
    /// <param name="warningsContainer">Container that receives the HelpBoxes.</param>
    /// <param name="properties">Resolved property bundle for the death animation block.</param>
    private static void RefreshWarnings(VisualElement warningsContainer, DeathAnimationProperties properties)
    {
        if (warningsContainer == null)
            return;

        warningsContainer.Clear();

        if (!properties.Enabled.boolValue)
            return;

        bool zoomEnabled = properties.CameraZoomEnabled.boolValue;
        bool positionLerpEnabled = properties.CameraPositionLerpEnabled.boolValue;
        bool hasVfxPrefab = properties.DespawnVfxPrefab.objectReferenceValue != null;
        float playbackDuration = properties.PlaybackDuration.floatValue;
        float fovDelta = properties.CameraTargetFovDelta.floatValue;
        float positionLerpAmount = properties.CameraPositionLerpAmount.floatValue;
        float cameraCompletionTime = properties.CameraCompletionNormalizedTime.floatValue;
        float vfxScale = properties.DespawnVfxScaleMultiplier.floatValue;
        float vfxLifetime = properties.DespawnVfxLifetimeSeconds.floatValue;
        float vfxSpawnNormalizedTime = properties.DespawnVfxSpawnNormalizedTime.floatValue;
        bool impactFrameEnabled = properties.ImpactFrameEnabled.boolValue;
        float impactBuildInStart = properties.ImpactFrameBuildInStartNormalizedTime.floatValue;
        float impactApply = properties.ImpactFrameApplyNormalizedTime.floatValue;
        float impactEnd = properties.ImpactFrameEndNormalizedTime.floatValue;

        if (playbackDuration < 0f)
            warningsContainer.Add(new HelpBox("Payback Duration is negative; runtime clamps it to zero and the end-of-run UI appears immediately.", HelpBoxMessageType.Warning));

        // With every cinematic source disabled the animation is functionally a no-op.
        if (!zoomEnabled && !positionLerpEnabled && !hasVfxPrefab)
            warningsContainer.Add(new HelpBox("Zoom is off, dolly is off and no Despawn VFX is assigned: the death animation will have no visible effect while enabled.", HelpBoxMessageType.Warning));

        if (zoomEnabled && Mathf.Approximately(fovDelta, 0f))
            warningsContainer.Add(new HelpBox("Camera Zoom is enabled but Target FOV Delta is 0; the zoom will have no visible effect.", HelpBoxMessageType.Warning));

        if (positionLerpEnabled && positionLerpAmount <= 0f)
            warningsContainer.Add(new HelpBox("Camera Dolly is enabled but Camera Dolly Amount is 0; the camera will not slide toward the player.", HelpBoxMessageType.Warning));

        if (positionLerpAmount < 0f || positionLerpAmount > 1f)
            warningsContainer.Add(new HelpBox("Camera Dolly Amount is outside the [0..1] range; runtime clamps it before driving the lerp.", HelpBoxMessageType.Warning));

        if ((zoomEnabled || positionLerpEnabled) && (cameraCompletionTime < 0f || cameraCompletionTime > 1f))
            warningsContainer.Add(new HelpBox("Camera Completion Time is outside the [0..1] range; runtime clamps it before resolving the camera tween.", HelpBoxMessageType.Warning));

        if (hasVfxPrefab)
        {
            if (vfxScale <= 0f)
                warningsContainer.Add(new HelpBox("Despawn VFX Scale Multiplier is 0 or negative; the VFX will not be visible.", HelpBoxMessageType.Warning));

            if (vfxLifetime <= 0f)
                warningsContainer.Add(new HelpBox("Despawn VFX Lifetime is 0; the VFX will be destroyed on the same frame it spawns.", HelpBoxMessageType.Info));

            if (vfxSpawnNormalizedTime < 0f || vfxSpawnNormalizedTime > 1f)
                warningsContainer.Add(new HelpBox("Despawn VFX Spawn Normalized Time is outside the [0..1] range; runtime clamps it before evaluating the spawn threshold.", HelpBoxMessageType.Warning));
        }

        if (impactFrameEnabled)
        {
            if (impactBuildInStart < 0f || impactBuildInStart > 1f ||
                impactApply < 0f || impactApply > 1f ||
                impactEnd < 0f || impactEnd > 1f)
            {
                warningsContainer.Add(new HelpBox("Impact Frame death timeline markers should stay inside the [0..1] range.",
                                                  HelpBoxMessageType.Warning));
            }

            if (impactBuildInStart > impactApply || impactApply > impactEnd)
                warningsContainer.Add(new HelpBox("Impact Frame death timeline should satisfy Build-In Start <= Apply <= End.",
                                                  HelpBoxMessageType.Warning));
        }
    }

    /// <summary>
    /// Resolves the death animation serialized properties into a single struct so the body builder and the update
    /// callback can both reuse them without re-walking the SerializedProperty tree.
    /// </summary>
    /// <param name="deathAnimationProperty">Serialized death animation settings block.</param>
    /// <returns>Resolved property bundle; <see cref="DeathAnimationProperties.IsComplete"/> is true when every required relative was found.</returns>
    private static DeathAnimationProperties ResolveProperties(SerializedProperty deathAnimationProperty)
    {
        DeathAnimationProperties bundle = default;
        bundle.Enabled = deathAnimationProperty.FindPropertyRelative("enabled");
        bundle.PlaybackDuration = deathAnimationProperty.FindPropertyRelative("playbackDurationSeconds");
        bundle.CameraZoomEnabled = deathAnimationProperty.FindPropertyRelative("cameraZoomEnabled");
        bundle.CameraTargetFovDelta = deathAnimationProperty.FindPropertyRelative("cameraTargetFovDelta");
        bundle.CameraPositionLerpEnabled = deathAnimationProperty.FindPropertyRelative("cameraPositionLerpEnabled");
        bundle.CameraPositionLerpAmount = deathAnimationProperty.FindPropertyRelative("cameraPositionLerpAmount");
        bundle.CameraCompletionNormalizedTime = deathAnimationProperty.FindPropertyRelative("cameraCompletionNormalizedTime");
        bundle.EasingMode = deathAnimationProperty.FindPropertyRelative("easingMode");
        bundle.DespawnVfxPrefab = deathAnimationProperty.FindPropertyRelative("despawnVfxPrefab");
        bundle.DespawnVfxSpawnOffset = deathAnimationProperty.FindPropertyRelative("despawnVfxSpawnOffset");
        bundle.DespawnVfxScaleMultiplier = deathAnimationProperty.FindPropertyRelative("despawnVfxScaleMultiplier");
        bundle.DespawnVfxSpawnNormalizedTime = deathAnimationProperty.FindPropertyRelative("despawnVfxSpawnNormalizedTime");
        bundle.DespawnVfxLifetimeSeconds = deathAnimationProperty.FindPropertyRelative("despawnVfxLifetimeSeconds");
        bundle.HidePlayerVisualOnVfxSpawn = deathAnimationProperty.FindPropertyRelative("hidePlayerVisualOnVfxSpawn");
        bundle.ImpactFrameEnabled = deathAnimationProperty.FindPropertyRelative("impactFrameEnabled");
        bundle.ImpactFrameBuildInStartNormalizedTime = deathAnimationProperty.FindPropertyRelative("impactFrameBuildInStartNormalizedTime");
        bundle.ImpactFrameApplyNormalizedTime = deathAnimationProperty.FindPropertyRelative("impactFrameApplyNormalizedTime");
        bundle.ImpactFrameEndNormalizedTime = deathAnimationProperty.FindPropertyRelative("impactFrameEndNormalizedTime");
        bundle.ImpactFrame = deathAnimationProperty.FindPropertyRelative("impactFrame");
        return bundle;
    }

    /// <summary>
    /// Adds one Add-Scaling-aware property field to the target container.
    /// </summary>
    /// <param name="target">Container that receives the field.</param>
    /// <param name="property">Serialized property to bind.</param>
    /// <param name="scalingRulesProperty">Scaling rules list backing the preset.</param>
    /// <param name="label">User-facing label override.</param>
    /// <param name="tooltip">Tooltip shown on hover.</param>
    private static void AddScalableField(VisualElement target,
                                          SerializedProperty property,
                                          SerializedProperty scalingRulesProperty,
                                          string label,
                                          string tooltip)
    {
        if (target == null || property == null)
            return;

        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property, scalingRulesProperty, label);
        field.tooltip = tooltip;
        target.Add(field);
    }

    /// <summary>
    /// Builds one bold subsection header label used to thematically separate Camera Tween, Despawn VFX and Visual
    /// Bridge blocks.
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
    /// Bundles the death animation serialized properties so the body builder and the update callback share one resolution.
    /// </summary>
    private struct DeathAnimationProperties
    {
        public SerializedProperty Enabled;
        public SerializedProperty PlaybackDuration;
        public SerializedProperty CameraZoomEnabled;
        public SerializedProperty CameraTargetFovDelta;
        public SerializedProperty CameraPositionLerpEnabled;
        public SerializedProperty CameraPositionLerpAmount;
        public SerializedProperty CameraCompletionNormalizedTime;
        public SerializedProperty EasingMode;
        public SerializedProperty DespawnVfxPrefab;
        public SerializedProperty DespawnVfxSpawnOffset;
        public SerializedProperty DespawnVfxScaleMultiplier;
        public SerializedProperty DespawnVfxSpawnNormalizedTime;
        public SerializedProperty DespawnVfxLifetimeSeconds;
        public SerializedProperty HidePlayerVisualOnVfxSpawn;
        public SerializedProperty ImpactFrameEnabled;
        public SerializedProperty ImpactFrameBuildInStartNormalizedTime;
        public SerializedProperty ImpactFrameApplyNormalizedTime;
        public SerializedProperty ImpactFrameEndNormalizedTime;
        public SerializedProperty ImpactFrame;

        public bool IsComplete
        {
            get
            {
                return Enabled != null &&
                       PlaybackDuration != null &&
                       CameraZoomEnabled != null &&
                       CameraTargetFovDelta != null &&
                       CameraPositionLerpEnabled != null &&
                       CameraPositionLerpAmount != null &&
                       CameraCompletionNormalizedTime != null &&
                       EasingMode != null &&
                       DespawnVfxPrefab != null &&
                       DespawnVfxSpawnOffset != null &&
                       DespawnVfxScaleMultiplier != null &&
                       DespawnVfxSpawnNormalizedTime != null &&
                       DespawnVfxLifetimeSeconds != null &&
                       HidePlayerVisualOnVfxSpawn != null &&
                       ImpactFrameEnabled != null &&
                       ImpactFrameBuildInStartNormalizedTime != null &&
                       ImpactFrameApplyNormalizedTime != null &&
                       ImpactFrameEndNormalizedTime != null &&
                       ImpactFrame != null;
            }
        }
    }
    #endregion
}
