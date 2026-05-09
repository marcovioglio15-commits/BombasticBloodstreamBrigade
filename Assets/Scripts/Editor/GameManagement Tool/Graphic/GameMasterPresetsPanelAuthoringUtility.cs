using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Handles prefab discovery and active GameAudioManagerAuthoring assignment for Game Management presets.
/// /params None.
/// /returns None.
/// </summary>
internal static class GameMasterPresetsPanelAuthoringUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Finds the first prefab containing GameAudioManagerAuthoring and stores it on the panel.
    /// /params panel Owning panel that receives the selected prefab.
    /// /returns None.
    /// </summary>
    public static void FindAudioManagerPrefab(GameMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        List<GameObject> prefabs = ManagementToolPrefabUtility.FindPrefabsWithComponentInHierarchy<GameAudioManagerAuthoring>(new string[] { "Assets" });

        if (prefabs.Count <= 0)
            return;

        panel.SelectedAudioPrefab = prefabs[0];
        GameMasterPresetsPanelSidePanelUtility.SaveSelectedAudioPrefabState(panel);

        if (panel.AudioPrefabField != null)
            panel.AudioPrefabField.SetValueWithoutNotify(panel.SelectedAudioPrefab);

        RefreshActiveStatus(panel);
    }

    /// <summary>
    /// Finds the first prefab containing GameSceneManagerAuthoring and stores it on the panel.
    /// /params panel Owning panel that receives the selected prefab.
    /// /returns None.
    /// </summary>
    public static void FindSceneManagerPrefab(GameMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        List<GameObject> prefabs = ManagementToolPrefabUtility.FindPrefabsWithComponentInHierarchy<GameSceneManagerAuthoring>(new string[] { "Assets" });

        if (prefabs.Count <= 0)
            return;

        panel.SelectedScenePrefab = prefabs[0];
        GameMasterPresetsPanelSidePanelUtility.SaveSelectedScenePrefabState(panel);

        if (panel.ScenePrefabField != null)
            panel.ScenePrefabField.SetValueWithoutNotify(panel.SelectedScenePrefab);

        RefreshActiveStatus(panel);
    }

    /// <summary>
    /// Assigns the selected master preset to the selected GameAudioManagerAuthoring prefab.
    /// /params panel Owning panel with selected preset and prefab context.
    /// /returns None.
    /// </summary>
    public static void AssignPresetToAuthoringPrefab(GameMasterPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null || panel.SelectedAudioPrefab == null)
            return;

        GameAudioManagerAuthoring authoring = panel.SelectedAudioPrefab.GetComponentInChildren<GameAudioManagerAuthoring>(true);

        if (authoring == null)
            return;

        AssignMasterPresetToAuthoring(panel, authoring, panel.SelectedAudioPrefab);
        RefreshActiveStatus(panel);
    }

    /// <summary>
    /// Assigns the selected master preset to the selected GameSceneManagerAuthoring prefab.
    /// /params panel Owning panel with selected preset and prefab context.
    /// /returns None.
    /// </summary>
    public static void AssignPresetToSceneAuthoringPrefab(GameMasterPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null || panel.SelectedScenePrefab == null)
            return;

        GameSceneManagerAuthoring authoring = panel.SelectedScenePrefab.GetComponentInChildren<GameSceneManagerAuthoring>(true);

        if (authoring == null)
            return;

        AssignMasterPresetToAuthoring(panel, authoring, panel.SelectedScenePrefab);
        RefreshActiveStatus(panel);
    }

    /// <summary>
    /// Updates the active authoring status label for the selected prefab.
    /// /params panel Owning panel with selected preset and status label.
    /// /returns None.
    /// </summary>
    public static void RefreshActiveStatus(GameMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        RefreshAudioActiveStatus(panel);
        RefreshSceneActiveStatus(panel);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Assigns the selected master preset to a serialized authoring component masterPreset field.
    /// /params panel Owning panel with selected master preset context.
    /// /params authoring Authoring component containing a masterPreset field.
    /// /params prefab Prefab asset that owns the authoring component.
    /// /returns None.
    /// </summary>
    private static void AssignMasterPresetToAuthoring(GameMasterPresetsPanel panel, UnityEngine.Object authoring, GameObject prefab)
    {
        Undo.RecordObject(authoring, "Set Active Game Master Preset");
        SerializedObject serializedAuthoring = new SerializedObject(authoring);
        SerializedProperty masterPresetProperty = serializedAuthoring.FindProperty("masterPreset");

        if (masterPresetProperty == null)
            return;

        serializedAuthoring.Update();
        masterPresetProperty.objectReferenceValue = panel.SelectedPreset;
        serializedAuthoring.ApplyModifiedProperties();
        EditorUtility.SetDirty(authoring);
        EditorUtility.SetDirty(prefab);
        PrefabUtility.RecordPrefabInstancePropertyModifications(authoring);
        GameManagementDraftSession.MarkDirty();
    }

    /// <summary>
    /// Updates the Audio Manager active authoring status label.
    /// /params panel Owning panel with selected audio prefab context.
    /// /returns None.
    /// </summary>
    private static void RefreshAudioActiveStatus(GameMasterPresetsPanel panel)
    {
        if (panel.ActiveStatusLabel == null)
            return;

        if (panel.SelectedAudioPrefab == null)
        {
            panel.ActiveStatusLabel.text = "No Audio Manager prefab selected.";
            return;
        }

        GameAudioManagerAuthoring authoring = panel.SelectedAudioPrefab.GetComponentInChildren<GameAudioManagerAuthoring>(true);

        if (authoring == null)
        {
            panel.ActiveStatusLabel.text = "Selected prefab has no GameAudioManagerAuthoring.";
            return;
        }

        panel.ActiveStatusLabel.text = authoring.MasterPreset == panel.SelectedPreset ? "This preset is active on the selected prefab." : "Selected prefab uses a different preset.";
    }

    /// <summary>
    /// Updates the Scene Manager active authoring status label.
    /// /params panel Owning panel with selected scene prefab context.
    /// /returns None.
    /// </summary>
    private static void RefreshSceneActiveStatus(GameMasterPresetsPanel panel)
    {
        if (panel.SceneActiveStatusLabel == null)
            return;

        if (panel.SelectedScenePrefab == null)
        {
            panel.SceneActiveStatusLabel.text = "No Scene Manager prefab selected.";
            return;
        }

        GameSceneManagerAuthoring authoring = panel.SelectedScenePrefab.GetComponentInChildren<GameSceneManagerAuthoring>(true);

        if (authoring == null)
        {
            panel.SceneActiveStatusLabel.text = "Selected prefab has no GameSceneManagerAuthoring.";
            return;
        }

        panel.SceneActiveStatusLabel.text = authoring.MasterPreset == panel.SelectedPreset ? "This preset is active on the selected prefab." : "Selected prefab uses a different preset.";
    }
    #endregion

    #endregion
}
