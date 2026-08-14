using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Reflects the selected HUD preset directly on authored Synchro Meter objects in loaded editor scenes.
/// </summary>
internal static class GameHudManagerSynchroMeterScenePreviewUtility
{
    #region Constants
    private const int AllScenesHandle = -1;
    #endregion

    #region Fields
    private static readonly List<GameHudSynchroMeterScenePresentationState> presentationStates = new List<GameHudSynchroMeterScenePresentationState>();
    private static readonly StringBuilder progressionTextBuilder = new StringBuilder(512);
    private static object activeOwner;
    private static GameHudManagerPreset activePreset;
    private static object scheduledOwner;
    private static GameHudManagerPreset scheduledPreset;
    private static bool lifecycleHooksRegistered;
    #endregion

    #region Methods

    #region Internal Methods
    /// <summary>
    /// Reflects one selected preset immediately on authored Synchro Meters in loaded editor scenes.
    /// </summary>
    /// <param name="owner">HUD panel requesting and owning the editor presentation lifecycle.</param>
    /// <param name="preset">Currently selected HUD preset, or null to restore authored presentation values.</param>
    internal static void Refresh(object owner, GameHudManagerPreset preset)
    {
        if (owner == null)
            return;

        EnsureLifecycleHooks();
        EditorApplication.delayCall -= ApplyScheduledPresentation;
        scheduledOwner = null;
        scheduledPreset = null;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Clear(owner);
            return;
        }

        ApplyPresentation(owner, preset);
    }

    /// <summary>
    /// Schedules one coalesced editor presentation refresh after serialized property edits are committed.
    /// </summary>
    /// <param name="owner">HUD panel requesting and owning the editor presentation lifecycle.</param>
    /// <param name="preset">Currently selected HUD preset, or null to restore authored presentation values.</param>
    internal static void Schedule(object owner, GameHudManagerPreset preset)
    {
        if (owner == null)
            return;

        EnsureLifecycleHooks();
        scheduledOwner = owner;
        scheduledPreset = preset;
        EditorApplication.delayCall -= ApplyScheduledPresentation;
        EditorApplication.delayCall += ApplyScheduledPresentation;
    }

    /// <summary>
    /// Restores authored values for presentation content owned by one HUD panel.
    /// </summary>
    /// <param name="owner">HUD panel releasing its editor presentation lifecycle.</param>
    internal static void Clear(object owner)
    {
        if (ReferenceEquals(scheduledOwner, owner))
        {
            EditorApplication.delayCall -= ApplyScheduledPresentation;
            scheduledOwner = null;
            scheduledPreset = null;
        }

        if (!ReferenceEquals(activeOwner, owner))
            return;

        activeOwner = null;
        activePreset = null;
        RestorePresentationStates(AllScenesHandle);
    }
    #endregion

    #region Scheduling
    /// <summary>
    /// Applies the latest editor presentation request after UI Toolkit commits its serialized edit.
    /// </summary>
    private static void ApplyScheduledPresentation()
    {
        EditorApplication.delayCall -= ApplyScheduledPresentation;
        object owner = scheduledOwner;
        GameHudManagerPreset preset = scheduledPreset;
        scheduledOwner = null;
        scheduledPreset = null;

        if (owner == null || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            ClearAll();
            return;
        }

        ApplyPresentation(owner, preset);
    }

    /// <summary>
    /// Registers editor lifecycle callbacks that restore authored state before reload, saving, or Play Mode entry.
    /// </summary>
    private static void EnsureLifecycleHooks()
    {
        if (lifecycleHooksRegistered)
            return;

        AssemblyReloadEvents.beforeAssemblyReload += ClearAll;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorSceneManager.sceneOpened += HandleSceneOpened;
        EditorSceneManager.sceneClosed += HandleSceneClosed;
        EditorSceneManager.sceneSaving += HandleSceneSaving;
        EditorSceneManager.sceneSaved += HandleSceneSaved;
        lifecycleHooksRegistered = true;
    }

    /// <summary>
    /// Reapplies the selected preset after a Scene becomes available to the editor hierarchy.
    /// </summary>
    /// <param name="scene">Scene that has completed opening.</param>
    /// <param name="mode">Mode used to open the Scene.</param>
    private static void HandleSceneOpened(Scene scene, OpenSceneMode mode)
    {
        ScheduleActivePresentation();
    }

    /// <summary>
    /// Rebuilds the selected-preset presentation after obsolete Scene objects have been unloaded.
    /// </summary>
    /// <param name="scene">Scene that has completed closing.</param>
    private static void HandleSceneClosed(Scene scene)
    {
        ScheduleActivePresentation();
    }

    /// <summary>
    /// Restores authored values before Unity serializes a Scene containing reflected preset values.
    /// </summary>
    /// <param name="scene">Scene about to be saved.</param>
    /// <param name="path">Destination path used for the Scene save.</param>
    private static void HandleSceneSaving(Scene scene, string path)
    {
        RestorePresentationStates(scene.handle);
    }

    /// <summary>
    /// Reapplies the selected preset after the authored Scene state has been saved safely.
    /// </summary>
    /// <param name="scene">Scene that has completed saving.</param>
    private static void HandleSceneSaved(Scene scene)
    {
        ScheduleActivePresentation();
    }

    /// <summary>
    /// Restores authored presentation before runtime systems take ownership of the HUD hierarchy.
    /// </summary>
    /// <param name="state">Current editor Play Mode transition state.</param>
    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
            case PlayModeStateChange.EnteredPlayMode:
                ClearAll();
                break;
        }
    }

    /// <summary>
    /// Coalesces a rebuild request when a selected preset and its owning HUD panel remain active.
    /// </summary>
    private static void ScheduleActivePresentation()
    {
        if (activeOwner == null)
            return;

        Schedule(activeOwner, activePreset);
    }
    #endregion

    #region Presentation
    /// <summary>
    /// Restores previous editor state, then reflects the selected preset on every authored loaded-scene meter.
    /// </summary>
    /// <param name="owner">HUD panel taking ownership of the reflected presentation.</param>
    /// <param name="preset">Selected HUD preset whose visual values are reflected.</param>
    private static void ApplyPresentation(object owner, GameHudManagerPreset preset)
    {
        RestorePresentationStates(AllScenesHandle);
        activeOwner = owner;
        activePreset = preset;

        if (preset == null)
            return;

        GameHudRuntimeConfig config = GameHudManagerPresetBakeUtility.BuildConfig(preset);
        HUDComboCounterSection[] sections = Resources.FindObjectsOfTypeAll<HUDComboCounterSection>();

        // Reflect the preset only on authored loaded-scene instances; prefab assets remain untouched.
        for (int sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
        {
            HUDComboCounterSection section = sections[sectionIndex];

            if (!IsPresentationSource(section))
                continue;

            ApplySectionPresentation(section, in config);
        }

        RefreshEditorRendering();
    }

    /// <summary>
    /// Checks whether one Synchro Meter belongs to a loaded editable Scene.
    /// </summary>
    /// <param name="section">Candidate Synchro Meter section.</param>
    /// <returns>True when the section is an authored loaded-scene instance.</returns>
    private static bool IsPresentationSource(HUDComboCounterSection section)
    {
        if (section == null || EditorUtility.IsPersistent(section))
            return false;

        if ((section.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0)
            return false;

        Scene scene = section.gameObject.scene;
        return scene.IsValid() && scene.isLoaded;
    }

    /// <summary>
    /// Captures authored UI values and applies one preset directly to the existing Synchro Meter hierarchy.
    /// </summary>
    /// <param name="section">Authored Synchro Meter receiving the editor presentation.</param>
    /// <param name="config">Baked selected-preset configuration being reflected.</param>
    private static void ApplySectionPresentation(HUDComboCounterSection section,
                                                 in GameHudRuntimeConfig config)
    {
        GameHudSynchroMeterSceneBindings bindings = ReadBindings(section);
        CanvasGroup canvasGroup = bindings.RootObject != null
            ? bindings.RootObject.GetComponent<CanvasGroup>()
            : null;
        presentationStates.Add(new GameHudSynchroMeterScenePresentationState(section,
                                                                             bindings.RootObject,
                                                                             canvasGroup,
                                                                             CaptureGraphicStates(in bindings),
                                                                             bindings.ProgressionText));

        bool meterVisible = config.SynchroMeterEnabled != 0;
        bool usesProgressionText = config.SynchroVisualMode == GameHudSynchroMeterVisualMode.ProgressionText;

        if (bindings.RootObject != null)
            bindings.RootObject.SetActive(meterVisible);

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        ApplyGraphic(bindings.BackgroundImage,
                     meterVisible && config.SynchroShowBackground != 0,
                     HUDSynchroMeterPresentationUtility.ToColor(config.SynchroBackgroundTint));
        ApplyGraphic(bindings.CoverImage,
                     meterVisible && config.SynchroShowCover != 0,
                     HUDSynchroMeterPresentationUtility.ToColor(config.SynchroCoverTint));
        ApplyGraphic(bindings.PrimaryWaveLeadingImage,
                     meterVisible,
                     HUDSynchroMeterPresentationUtility.ToColor(config.SynchroPrimaryWaveTint));
        ApplyGraphic(bindings.PrimaryWaveTrailingImage,
                     meterVisible,
                     HUDSynchroMeterPresentationUtility.ToColor(config.SynchroPrimaryWaveTint));
        ApplyGraphic(bindings.SecondaryWaveLeadingImage,
                     meterVisible,
                     HUDSynchroMeterPresentationUtility.ToColor(config.SynchroSecondaryWaveTint));
        ApplyGraphic(bindings.SecondaryWaveTrailingImage,
                     meterVisible,
                     HUDSynchroMeterPresentationUtility.ToColor(config.SynchroSecondaryWaveTint));
        ApplyGraphic(bindings.RankText,
                     meterVisible && !usesProgressionText && config.SynchroShowRankText != 0,
                     HUDSynchroMeterPresentationUtility.ToColor(config.SynchroRankTextColor));
        ApplyGraphic(bindings.ValueText,
                     meterVisible && !usesProgressionText && config.SynchroShowValueText != 0,
                     HUDSynchroMeterPresentationUtility.ToColor(config.SynchroValueTextColor));
        ApplyGraphic(bindings.ProgressFillImage,
                     meterVisible && !usesProgressionText && config.SynchroShowProgressBar != 0,
                     HUDSynchroMeterPresentationUtility.ToColor(config.SynchroProgressFillTint));
        ApplyGraphic(bindings.ProgressBackgroundImage,
                     meterVisible && !usesProgressionText && config.SynchroShowProgressBar != 0,
                     HUDSynchroMeterPresentationUtility.ToColor(config.SynchroProgressBackgroundTint));
        ApplyGraphic(bindings.ProgressionText,
                     meterVisible && usesProgressionText,
                     HUDSynchroMeterPresentationUtility.ToColor(config.SynchroProgressionTextColor));
        ApplyProgressionText(bindings.ProgressionText, in config);
    }

    /// <summary>
    /// Applies token content and configured layout to the authored optional progression label.
    /// </summary>
    /// <param name="text">Authored TMP label updated in place.</param>
    /// <param name="config">Baked selected-preset configuration providing content and layout.</param>
    private static void ApplyProgressionText(TMP_Text text, in GameHudRuntimeConfig config)
    {
        if (text == null)
            return;

        HUDSynchroMeterPresentationUtility.ApplyProgressionText(text,
                                                               progressionTextBuilder,
                                                               config.SynchroProgressionTextFormat.ToString(),
                                                               0);
        HUDSynchroMeterPresentationUtility.ApplyProgressionTextLayout(text,
                                                                     config.SynchroProgressionTextFontSize,
                                                                     config.SynchroProgressionTextAlignment,
                                                                     config.SynchroProgressionTextWaveDistance);
    }

    /// <summary>
    /// Applies one editor-only visibility and color state when its authored graphic exists.
    /// </summary>
    /// <param name="graphic">Authored UI graphic updated in place.</param>
    /// <param name="isVisible">Whether the graphic is visible for the selected preset.</param>
    /// <param name="color">Color resolved from the selected preset.</param>
    private static void ApplyGraphic(Graphic graphic, bool isVisible, Color color)
    {
        if (graphic == null)
            return;

        graphic.color = color;
        graphic.enabled = isVisible;
    }
    #endregion

    #region Binding and State
    /// <summary>
    /// Reads direct UI references from one authored Synchro Meter without runtime reflection.
    /// </summary>
    /// <param name="section">Authored section owning the serialized references.</param>
    /// <returns>Strongly typed bindings required for editor presentation and restoration.</returns>
    private static GameHudSynchroMeterSceneBindings ReadBindings(HUDComboCounterSection section)
    {
        SerializedObject serializedSection = new SerializedObject(section);
        return new GameHudSynchroMeterSceneBindings(GetReference<GameObject>(serializedSection, "rootObject"),
                                                    GetReference<Image>(serializedSection, "backgroundImage"),
                                                    GetReference<Image>(serializedSection, "coverImage"),
                                                    GetReference<Image>(serializedSection, "primaryWaveLeadingImage"),
                                                    GetReference<Image>(serializedSection, "primaryWaveTrailingImage"),
                                                    GetReference<Image>(serializedSection, "secondaryWaveLeadingImage"),
                                                    GetReference<Image>(serializedSection, "secondaryWaveTrailingImage"),
                                                    GetReference<TMP_Text>(serializedSection, "rankText"),
                                                    GetReference<TMP_Text>(serializedSection, "valueText"),
                                                    GetReference<Image>(serializedSection, "progressFillImage"),
                                                    GetReference<Image>(serializedSection, "progressBackgroundImage"),
                                                    GetReference<TMP_Text>(serializedSection, "progressionText"));
    }

    /// <summary>
    /// Resolves one typed object reference from a serialized Synchro Meter field.
    /// </summary>
    /// <typeparam name="T">Expected Unity object type.</typeparam>
    /// <param name="serializedSection">Serialized authored section containing the field.</param>
    /// <param name="propertyName">Exact private serialized field name.</param>
    /// <returns>Assigned object reference, or null when the field is absent or unassigned.</returns>
    private static T GetReference<T>(SerializedObject serializedSection, string propertyName)
        where T : UnityEngine.Object
    {
        SerializedProperty property = serializedSection.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue as T : null;
    }

    /// <summary>
    /// Captures each unique graphic once so shared bindings restore deterministically.
    /// </summary>
    /// <param name="bindings">Authored references whose graphic states are captured.</param>
    /// <returns>Original enabled and color values for all assigned graphics.</returns>
    private static List<GameHudSynchroMeterGraphicPresentationState> CaptureGraphicStates(
        in GameHudSynchroMeterSceneBindings bindings)
    {
        List<GameHudSynchroMeterGraphicPresentationState> states = new List<GameHudSynchroMeterGraphicPresentationState>(11);
        CaptureGraphicState(states, bindings.BackgroundImage);
        CaptureGraphicState(states, bindings.CoverImage);
        CaptureGraphicState(states, bindings.PrimaryWaveLeadingImage);
        CaptureGraphicState(states, bindings.PrimaryWaveTrailingImage);
        CaptureGraphicState(states, bindings.SecondaryWaveLeadingImage);
        CaptureGraphicState(states, bindings.SecondaryWaveTrailingImage);
        CaptureGraphicState(states, bindings.RankText);
        CaptureGraphicState(states, bindings.ValueText);
        CaptureGraphicState(states, bindings.ProgressFillImage);
        CaptureGraphicState(states, bindings.ProgressBackgroundImage);
        CaptureGraphicState(states, bindings.ProgressionText);
        return states;
    }

    /// <summary>
    /// Adds one graphic state only when the assigned object has not already been captured.
    /// </summary>
    /// <param name="states">Mutable state collection receiving the unique graphic.</param>
    /// <param name="graphic">Candidate authored graphic.</param>
    private static void CaptureGraphicState(List<GameHudSynchroMeterGraphicPresentationState> states,
                                            Graphic graphic)
    {
        if (graphic == null)
            return;

        // Guard uncommon shared bindings so restoration order cannot overwrite an earlier state.
        for (int stateIndex = 0; stateIndex < states.Count; stateIndex++)
        {
            if (states[stateIndex].Graphic == graphic)
                return;
        }

        states.Add(new GameHudSynchroMeterGraphicPresentationState(graphic));
    }
    #endregion

    #region Cleanup
    /// <summary>
    /// Clears every scheduled presentation and restores authored values in all loaded scenes.
    /// </summary>
    private static void ClearAll()
    {
        EditorApplication.delayCall -= ApplyScheduledPresentation;
        scheduledOwner = null;
        scheduledPreset = null;
        activeOwner = null;
        activePreset = null;
        RestorePresentationStates(AllScenesHandle);
    }

    /// <summary>
    /// Restores reflected UI values for all scenes or for one Scene before it is serialized.
    /// </summary>
    /// <param name="sceneHandle">Target Scene handle, or -1 to restore every reflected instance.</param>
    private static void RestorePresentationStates(int sceneHandle)
    {
        // Restore in reverse order and remove records immediately so callbacks remain idempotent.
        for (int stateIndex = presentationStates.Count - 1; stateIndex >= 0; stateIndex--)
        {
            GameHudSynchroMeterScenePresentationState state = presentationStates[stateIndex];

            if (sceneHandle != AllScenesHandle && state.SceneHandle != sceneHandle)
                continue;

            state.Restore();
            presentationStates.RemoveAt(stateIndex);
        }

        RefreshEditorRendering();
    }

    /// <summary>
    /// Requests one immediate editor UI layout and Scene view repaint after presentation changes.
    /// </summary>
    private static void RefreshEditorRendering()
    {
        Canvas.ForceUpdateCanvases();
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }
    #endregion

    #endregion
}
