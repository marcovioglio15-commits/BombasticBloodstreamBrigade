using UnityEditor;
using UnityEngine;

/// <summary>
/// Handles prefab lookup, active assignment and status refresh for player master preset panels.
/// </summary>
internal static class PlayerMasterPresetsPanelPrefabActivationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Finds the first project prefab containing PlayerAuthoring and stores it as the active target.
    /// </summary>
    /// <param name="panel">Owning panel that stores selected prefab state and UI references.</param>

    public static void FindPlayerPrefab(PlayerMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        GameObject prefab;

        if (!ManagementToolPrefabUtility.TryFindFirstPrefabWithComponent<PlayerAuthoring>(out prefab))
        {
            EditorUtility.DisplayDialog("Find Player Prefab", "No prefab with PlayerAuthoring was found.", "OK");
            return;
        }

        panel.SelectedPlayerPrefab = prefab;
        PlayerMasterPresetsPanelSidePanelUtility.SaveSelectedPrefabState(panel);

        if (panel.PlayerPrefabField != null)
            panel.PlayerPrefabField.SetValueWithoutNotify(prefab);

        RefreshActiveStatus(panel);
    }

    /// <summary>
    /// Assigns the selected player master preset to the currently selected player prefab.
    /// </summary>
    /// <param name="panel">Owning panel that stores selected preset and selected prefab state.</param>

    public static void AssignPresetToPrefab(PlayerMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        if (panel.SelectedPreset == null)
        {
            EditorUtility.DisplayDialog("Set Active Preset", "Select a master preset first.", "OK");
            return;
        }

        if (panel.SelectedPlayerPrefab == null)
        {
            EditorUtility.DisplayDialog("Set Active Preset", "Select a player prefab first.", "OK");
            return;
        }

        PlayerAuthoring authoring = panel.SelectedPlayerPrefab.GetComponent<PlayerAuthoring>();

        if (authoring == null)
        {
            EditorUtility.DisplayDialog("Set Active Preset", "PlayerAuthoring component not found on the selected prefab.", "OK");
            return;
        }

        SerializedObject serializedAuthoring = new SerializedObject(authoring);
        SerializedProperty presetProperty = serializedAuthoring.FindProperty("masterPreset");

        if (presetProperty == null)
            return;

        Undo.RecordObject(authoring, "Set Active Master Preset");
        serializedAuthoring.Update();
        presetProperty.objectReferenceValue = panel.SelectedPreset;
        serializedAuthoring.ApplyModifiedProperties();
        EditorUtility.SetDirty(authoring);

        if (PrefabUtility.IsPartOfPrefabInstance(authoring))
            PrefabUtility.RecordPrefabInstancePropertyModifications(authoring);

        PlayerManagementDraftSession.MarkDirty();
        RefreshActiveStatus(panel);
    }

    /// <summary>
    /// Refreshes the textual status describing whether the selected prefab uses the selected master preset.
    /// </summary>
    /// <param name="panel">Owning panel that stores selected preset, selected prefab and label references.</param>

    public static void RefreshActiveStatus(PlayerMasterPresetsPanel panel)
    {
        if (panel == null || panel.ActiveStatusLabel == null)
            return;

        if (panel.SelectedPreset == null)
        {
            panel.ActiveStatusLabel.text = "No master preset selected.";
            return;
        }

        if (panel.SelectedPlayerPrefab == null)
        {
            panel.ActiveStatusLabel.text = "No player prefab selected.";
            return;
        }

        PlayerAuthoring authoring = panel.SelectedPlayerPrefab.GetComponent<PlayerAuthoring>();

        if (authoring == null)
        {
            panel.ActiveStatusLabel.text = "PlayerAuthoring component not found on prefab.";
            return;
        }

        bool isActive = authoring.MasterPreset == panel.SelectedPreset;
        panel.ActiveStatusLabel.text = isActive ? "Active on selected prefab." : "Not active on selected prefab.";
    }
    #endregion

    #endregion
}
