using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds the Synchro Meter HUD preset controls and keeps combo-topology-specific wave options synchronized.
/// </summary>
internal static class GameHudManagerSynchroMeterPanelUtility
{
    #region Constants
    private const string ProgressionPresetStateKey = "NashCore.PlayerManagement.Progression.SelectedPreset";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the complete Synchro Meter section for one serialized HUD Manager preset.
    /// </summary>
    /// <param name="section">Section root receiving the authored controls.</param>
    /// <param name="serializedObject">Serialized HUD Manager preset being edited.</param>
    public static void Build(VisualElement section, SerializedObject serializedObject)
    {
        Foldout activationFoldout = CreateFoldout("Activation", "Master Synchro Meter toggle.");
        PropertyField enabledField = AddProperty(activationFoldout, serializedObject, "synchroMeterSettings.isEnabled", "Enabled");
        section.Add(activationFoldout);

        VisualElement meterOptionsRoot = CreateConditionalOptionsRoot();
        BuildLayersAndTheme(meterOptionsRoot,
                            serializedObject,
                            out PropertyField showBackgroundField,
                            out PropertyField showCoverField,
                            out PropertyField showRankTextField,
                            out PropertyField showValueTextField,
                            out PropertyField showProgressBarField,
                            out VisualElement backgroundThemeRoot,
                            out VisualElement coverThemeRoot,
                            out VisualElement rankTextThemeRoot,
                            out VisualElement valueTextThemeRoot,
                            out VisualElement progressThemeRoot);

        HelpBox topologyBox = new HelpBox(string.Empty, HelpBoxMessageType.Info);
        meterOptionsRoot.Add(topologyBox);
        BuildWaveMotion(meterOptionsRoot,
                        serializedObject,
                        out VisualElement rankedWaveOptionsRoot,
                        out VisualElement singleRankWaveOptionsRoot);

        VisualElement progressOptionsRoot = CreateConditionalOptionsRoot();
        Foldout progressFoldout = CreateFoldout("Progression", "Normalized progression shown below the wave display.");
        AddProperty(progressFoldout, serializedObject, "synchroMeterSettings.progressSmoothingSeconds", "Smoothing Seconds");
        progressOptionsRoot.Add(progressFoldout);
        meterOptionsRoot.Add(progressOptionsRoot);

        Foldout visibilityFoldout = CreateFoldout("Visibility", "Rules that hide the Synchro Meter when it is not useful.");
        AddProperty(visibilityFoldout, serializedObject, "synchroMeterSettings.hideWhenPlayerMissing", "Hide When Player Missing");
        AddProperty(visibilityFoldout, serializedObject, "synchroMeterSettings.hideWhenZeroValue", "Hide When Zero Value");
        AddProperty(visibilityFoldout, serializedObject, "synchroMeterSettings.hideWhenNoActiveRank", "Hide When No Active Rank");
        meterOptionsRoot.Add(visibilityFoldout);

        Foldout transitionsFoldout = CreateFoldout("Visibility Transitions", "Fade timings used when the Synchro Meter changes visibility.");
        AddProperty(transitionsFoldout, serializedObject, "synchroMeterSettings.fadeInDuration", "Fade In Duration");
        AddProperty(transitionsFoldout, serializedObject, "synchroMeterSettings.fadeOutDuration", "Fade Out Duration");
        meterOptionsRoot.Add(transitionsFoldout);

        VisualElement rankTextOptionsRoot = CreateConditionalOptionsRoot();
        Foldout textFoldout = CreateFoldout("Text", "Fallback label used before authoritative combo text is available.");
        AddProperty(textFoldout, serializedObject, "synchroMeterSettings.idleRankLabel", "Idle Rank Label");
        rankTextOptionsRoot.Add(textFoldout);
        meterOptionsRoot.Add(rankTextOptionsRoot);
        section.Add(meterOptionsRoot);

        TrackConditionalVisibility(showBackgroundField, backgroundThemeRoot, serializedObject, "synchroMeterSettings.showBackground", true);
        TrackConditionalVisibility(showCoverField, coverThemeRoot, serializedObject, "synchroMeterSettings.showCover", true);
        TrackConditionalVisibility(showRankTextField, rankTextThemeRoot, serializedObject, "synchroMeterSettings.showRankText", true);
        TrackConditionalVisibility(showRankTextField, rankTextOptionsRoot, serializedObject, "synchroMeterSettings.showRankText", true);
        TrackConditionalVisibility(showValueTextField, valueTextThemeRoot, serializedObject, "synchroMeterSettings.showValueText", true);
        TrackConditionalVisibility(showProgressBarField, progressThemeRoot, serializedObject, "synchroMeterSettings.showProgressBar", true);
        TrackConditionalVisibility(showProgressBarField, progressOptionsRoot, serializedObject, "synchroMeterSettings.showProgressBar", true);
        TrackConditionalVisibility(enabledField, meterOptionsRoot, serializedObject, "synchroMeterSettings.isEnabled", true);
        TrackComboTopology(section, rankedWaveOptionsRoot, singleRankWaveOptionsRoot, topologyBox);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds layer toggles and their conditionally visible theme fields.
    /// </summary>
    /// <param name="parent">Meter options root receiving both foldouts.</param>
    /// <param name="serializedObject">Serialized HUD Manager preset being edited.</param>
    /// <param name="showBackgroundField">Outputs the background visibility driver.</param>
    /// <param name="showCoverField">Outputs the cover visibility driver.</param>
    /// <param name="showRankTextField">Outputs the rank-label visibility driver.</param>
    /// <param name="showValueTextField">Outputs the value-label visibility driver.</param>
    /// <param name="showProgressBarField">Outputs the progression visibility driver.</param>
    /// <param name="backgroundThemeRoot">Outputs background-only theme options.</param>
    /// <param name="coverThemeRoot">Outputs cover-only theme options.</param>
    /// <param name="rankTextThemeRoot">Outputs rank-label-only theme options.</param>
    /// <param name="valueTextThemeRoot">Outputs value-label-only theme options.</param>
    /// <param name="progressThemeRoot">Outputs progression-only theme options.</param>
    private static void BuildLayersAndTheme(VisualElement parent,
                                            SerializedObject serializedObject,
                                            out PropertyField showBackgroundField,
                                            out PropertyField showCoverField,
                                            out PropertyField showRankTextField,
                                            out PropertyField showValueTextField,
                                            out PropertyField showProgressBarField,
                                            out VisualElement backgroundThemeRoot,
                                            out VisualElement coverThemeRoot,
                                            out VisualElement rankTextThemeRoot,
                                            out VisualElement valueTextThemeRoot,
                                            out VisualElement progressThemeRoot)
    {
        Foldout layersFoldout = CreateFoldout("Layers", "Optional authored layers and text overlays shown by the meter.");
        showBackgroundField = AddProperty(layersFoldout, serializedObject, "synchroMeterSettings.showBackground", "Show Background");
        showCoverField = AddProperty(layersFoldout, serializedObject, "synchroMeterSettings.showCover", "Show Cover");
        showRankTextField = AddProperty(layersFoldout, serializedObject, "synchroMeterSettings.showRankText", "Show Rank Text");
        showValueTextField = AddProperty(layersFoldout, serializedObject, "synchroMeterSettings.showValueText", "Show Value Text");
        showProgressBarField = AddProperty(layersFoldout, serializedObject, "synchroMeterSettings.showProgressBar", "Show Progress Bar");
        parent.Add(layersFoldout);

        Foldout themeFoldout = CreateFoldout("Theme", "Tints applied to authored background, cover, waves, text, and progression layers.");
        backgroundThemeRoot = CreateConditionalOptionsRoot();
        AddProperty(backgroundThemeRoot, serializedObject, "synchroMeterSettings.backgroundTint", "Background Tint");
        themeFoldout.Add(backgroundThemeRoot);
        coverThemeRoot = CreateConditionalOptionsRoot();
        AddProperty(coverThemeRoot, serializedObject, "synchroMeterSettings.coverTint", "Cover Tint");
        themeFoldout.Add(coverThemeRoot);
        AddProperty(themeFoldout, serializedObject, "synchroMeterSettings.primaryWaveTint", "Primary Wave Tint");
        AddProperty(themeFoldout, serializedObject, "synchroMeterSettings.secondaryWaveTint", "Secondary Wave Tint");
        rankTextThemeRoot = CreateConditionalOptionsRoot();
        AddProperty(rankTextThemeRoot, serializedObject, "synchroMeterSettings.rankTextColor", "Rank Text Color");
        themeFoldout.Add(rankTextThemeRoot);
        valueTextThemeRoot = CreateConditionalOptionsRoot();
        AddProperty(valueTextThemeRoot, serializedObject, "synchroMeterSettings.valueTextColor", "Value Text Color");
        themeFoldout.Add(valueTextThemeRoot);
        progressThemeRoot = CreateConditionalOptionsRoot();
        AddProperty(progressThemeRoot, serializedObject, "synchroMeterSettings.progressFillTint", "Progress Fill Tint");
        AddProperty(progressThemeRoot, serializedObject, "synchroMeterSettings.progressBackgroundTint", "Progress Background Tint");
        themeFoldout.Add(progressThemeRoot);
        parent.Add(themeFoldout);
    }

    /// <summary>
    /// Builds common wave motion controls and the two mutually exclusive topology payloads.
    /// </summary>
    /// <param name="parent">Meter options root receiving wave controls.</param>
    /// <param name="serializedObject">Serialized HUD Manager preset being edited.</param>
    /// <param name="rankedOptionsRoot">Outputs traditional rank-specific wave options.</param>
    /// <param name="singleRankOptionsRoot">Outputs Single Rank Progression-specific wave options.</param>
    private static void BuildWaveMotion(VisualElement parent,
                                        SerializedObject serializedObject,
                                        out VisualElement rankedOptionsRoot,
                                        out VisualElement singleRankOptionsRoot)
    {
        Foldout wavesFoldout = CreateFoldout("Wave Motion", "Seamless scrolling and topology-specific relative phase convergence.");
        AddProperty(wavesFoldout, serializedObject, "synchroMeterSettings.waveScrollCyclesPerSecond", "Base Scroll Cycles Per Second");

        rankedOptionsRoot = CreateConditionalOptionsRoot();
        AddProperty(rankedOptionsRoot, serializedObject, "synchroMeterSettings.lowestRankPhaseOffsetNormalized", "Lowest Rank Phase Offset");
        AddProperty(rankedOptionsRoot, serializedObject, "synchroMeterSettings.highestRankPhaseOffsetNormalized", "Highest Rank Phase Offset");
        AddProperty(rankedOptionsRoot, serializedObject, "synchroMeterSettings.phaseOffsetResponseExponent", "Phase Response Exponent");
        wavesFoldout.Add(rankedOptionsRoot);

        singleRankOptionsRoot = CreateConditionalOptionsRoot();
        PropertyField accelerateField = AddProperty(singleRankOptionsRoot, serializedObject, "synchroMeterSettings.singleRankAccelerateWavesWithProgress", "Accelerate Waves With Progress");
        VisualElement accelerationOptionsRoot = CreateConditionalOptionsRoot();
        AddProperty(accelerationOptionsRoot, serializedObject, "synchroMeterSettings.singleRankMaximumWaveScrollCyclesPerSecond", "Maximum Scroll Cycles Per Second");
        singleRankOptionsRoot.Add(accelerationOptionsRoot);
        TrackConditionalVisibility(accelerateField,
                                   accelerationOptionsRoot,
                                   serializedObject,
                                   "synchroMeterSettings.singleRankAccelerateWavesWithProgress",
                                   true);
        PropertyField convergenceModeField = AddProperty(singleRankOptionsRoot, serializedObject, "synchroMeterSettings.singleRankConvergenceMode", "Convergence Mode");
        AddProperty(singleRankOptionsRoot, serializedObject, "synchroMeterSettings.singleRankInitialPhaseOffsetNormalized", "Initial Phase Offset");
        AddProperty(singleRankOptionsRoot, serializedObject, "synchroMeterSettings.singleRankFinalPhaseOffsetNormalized", "Final Phase Offset");
        AddProperty(singleRankOptionsRoot, serializedObject, "synchroMeterSettings.singleRankConvergenceStartProgressPercent", "Convergence Start Progress Percent");
        AddProperty(singleRankOptionsRoot, serializedObject, "synchroMeterSettings.singleRankConvergenceEndProgressPercent", "Convergence End Progress Percent");
        VisualElement stepOptionsRoot = CreateConditionalOptionsRoot();
        AddProperty(stepOptionsRoot, serializedObject, "synchroMeterSettings.singleRankConvergenceStepCount", "Convergence Step Count");
        singleRankOptionsRoot.Add(stepOptionsRoot);
        TrackStepVisibility(convergenceModeField, stepOptionsRoot, serializedObject);
        wavesFoldout.Add(singleRankOptionsRoot);

        AddProperty(wavesFoldout, serializedObject, "synchroMeterSettings.phaseTransitionDuration", "Phase Transition Duration");
        AddProperty(wavesFoldout, serializedObject, "synchroMeterSettings.useUnscaledTime", "Use Unscaled Time");
        parent.Add(wavesFoldout);
    }

    /// <summary>
    /// Tracks the selected Player Management progression preset and exposes only its active combo topology options.
    /// </summary>
    /// <param name="lifetimeRoot">Element whose panel lifetime owns event subscriptions.</param>
    /// <param name="rankedOptionsRoot">Traditional rank wave options.</param>
    /// <param name="singleRankOptionsRoot">Single Rank Progression wave options.</param>
    /// <param name="topologyBox">Information box identifying the controlling progression preset.</param>
    private static void TrackComboTopology(VisualElement lifetimeRoot,
                                           VisualElement rankedOptionsRoot,
                                           VisualElement singleRankOptionsRoot,
                                           HelpBox topologyBox)
    {
        Action refresh = () => RefreshComboTopology(rankedOptionsRoot, singleRankOptionsRoot, topologyBox);
        refresh.Invoke();
        PlayerManagementSelectionContext.ContextChanged += refresh;
        PlayerManagementSelectionContext.ProgressionPresetContentChanged += refresh;
        lifetimeRoot.RegisterCallback<DetachFromPanelEvent>(_ =>
        {
            PlayerManagementSelectionContext.ContextChanged -= refresh;
            PlayerManagementSelectionContext.ProgressionPresetContentChanged -= refresh;
        });
    }

    /// <summary>
    /// Refreshes wave option visibility from the active or last persisted progression preset.
    /// </summary>
    /// <param name="rankedOptionsRoot">Traditional rank wave options.</param>
    /// <param name="singleRankOptionsRoot">Single Rank Progression wave options.</param>
    /// <param name="topologyBox">Information box identifying the controlling progression preset.</param>
    private static void RefreshComboTopology(VisualElement rankedOptionsRoot,
                                             VisualElement singleRankOptionsRoot,
                                             HelpBox topologyBox)
    {
        PlayerProgressionPreset progressionPreset = PlayerManagementSelectionContext.ActiveProgressionPreset;

        if (progressionPreset == null)
            progressionPreset = ManagementToolStateUtility.LoadAsset<PlayerProgressionPreset>(ProgressionPresetStateKey);

        PlayerComboCounterMode mode = progressionPreset != null && progressionPreset.ComboCounter != null
            ? progressionPreset.ComboCounter.Mode
            : PlayerComboCounterMode.Ranks;
        bool usesSingleRank = mode == PlayerComboCounterMode.SingleRankProgression;
        rankedOptionsRoot.style.display = usesSingleRank ? DisplayStyle.None : DisplayStyle.Flex;
        singleRankOptionsRoot.style.display = usesSingleRank ? DisplayStyle.Flex : DisplayStyle.None;
        topologyBox.text = progressionPreset != null
            ? "Wave options match combo mode '" + mode + "' from Player Progression preset '" + progressionPreset.name + "'."
            : "No Player Progression preset is selected. Traditional rank wave options are shown as the safe authoring fallback.";
    }

    /// <summary>
    /// Tracks whether stepped convergence is selected and hides its interval count otherwise.
    /// </summary>
    /// <param name="driverField">Serialized convergence mode field.</param>
    /// <param name="stepOptionsRoot">Container holding step-only settings.</param>
    /// <param name="serializedObject">Serialized HUD Manager preset being edited.</param>
    private static void TrackStepVisibility(PropertyField driverField,
                                            VisualElement stepOptionsRoot,
                                            SerializedObject serializedObject)
    {
        Action refresh = () =>
        {
            SerializedProperty modeProperty = serializedObject.FindProperty("synchroMeterSettings.singleRankConvergenceMode");
            bool usesSteps = modeProperty != null &&
                             modeProperty.enumValueIndex == (int)GameHudSynchroSingleRankConvergenceMode.Steps;
            stepOptionsRoot.style.display = usesSteps ? DisplayStyle.Flex : DisplayStyle.None;
        };
        refresh.Invoke();

        if (driverField != null)
            driverField.RegisterCallback<SerializedPropertyChangeEvent>(_ => refresh.Invoke());
    }

    /// <summary>
    /// Creates a standard HUD Manager foldout through the shared panel utility.
    /// </summary>
    /// <param name="title">Foldout label.</param>
    /// <param name="tooltip">Foldout explanatory tooltip.</param>
    /// <returns>Configured foldout.</returns>
    private static Foldout CreateFoldout(string title, string tooltip)
    {
        return GameHudManagerPresetsPanelUtility.CreateFoldout(title, tooltip);
    }

    /// <summary>
    /// Creates a conditional layout container through the shared panel utility.
    /// </summary>
    /// <returns>Configured vertical container.</returns>
    private static VisualElement CreateConditionalOptionsRoot()
    {
        return GameHudManagerPresetsPanelUtility.CreateConditionalOptionsRoot();
    }

    /// <summary>
    /// Adds and binds one HUD Manager serialized field through the shared panel utility.
    /// </summary>
    /// <param name="parent">Parent receiving the field.</param>
    /// <param name="serializedObject">Serialized HUD Manager preset being edited.</param>
    /// <param name="propertyPath">Serialized field path.</param>
    /// <param name="label">Displayed field label.</param>
    /// <returns>Bound property field, or null when the path is unavailable.</returns>
    private static PropertyField AddProperty(VisualElement parent,
                                             SerializedObject serializedObject,
                                             string propertyPath,
                                             string label)
    {
        return GameHudManagerPresetsPanelUtility.AddProperty(parent, serializedObject, propertyPath, label);
    }

    /// <summary>
    /// Tracks one Boolean setting through the shared panel utility.
    /// </summary>
    /// <param name="driverField">Field emitting property changes.</param>
    /// <param name="targetRoot">Container controlled by the Boolean value.</param>
    /// <param name="serializedObject">Serialized HUD Manager preset being edited.</param>
    /// <param name="propertyPath">Serialized Boolean field path.</param>
    /// <param name="fallback">Fallback visibility when the property is unavailable.</param>
    private static void TrackConditionalVisibility(PropertyField driverField,
                                                   VisualElement targetRoot,
                                                   SerializedObject serializedObject,
                                                   string propertyPath,
                                                   bool fallback)
    {
        GameHudManagerPresetsPanelUtility.TrackConditionalVisibility(driverField,
                                                                      targetRoot,
                                                                      serializedObject,
                                                                      propertyPath,
                                                                      fallback);
    }
    #endregion

    #endregion
}
