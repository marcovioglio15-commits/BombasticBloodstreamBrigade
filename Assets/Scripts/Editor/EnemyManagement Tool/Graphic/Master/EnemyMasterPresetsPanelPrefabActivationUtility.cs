using UnityEditor;
using UnityEngine;

/// <summary>
/// Handles enemy prefab activation and related status refresh for enemy master preset panels.
/// </summary>
internal static class EnemyMasterPresetsPanelPrefabActivationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Refreshes the list of enemy prefabs that contain EnemyAuthoring and preserves selection when possible.
    /// </summary>
    /// <param name="panel">Owning panel that stores prefab candidates and selected prefab state.</param>

    public static void RefreshAvailableEnemyPrefabs(EnemyMasterPresetsPanel panel)
    {
        string selectedPrefabPath = string.Empty;

        if (panel.SelectedEnemyPrefab != null)
            selectedPrefabPath = AssetDatabase.GetAssetPath(panel.SelectedEnemyPrefab);

        panel.AvailableEnemyPrefabs.Clear();
        panel.AvailableEnemyPrefabs.Add(null);

        string[] searchFolders = new string[] { "Assets" };
        System.Collections.Generic.List<GameObject> prefabAssets = ManagementToolPrefabUtility.FindPrefabsWithComponentInHierarchy<EnemyAuthoring>(searchFolders);

        for (int prefabIndex = 0; prefabIndex < prefabAssets.Count; prefabIndex++)
        {
            GameObject prefabAsset = prefabAssets[prefabIndex];

            if (prefabAsset == null)
                continue;

            panel.AvailableEnemyPrefabs.Add(prefabAsset);
        }

        if (string.IsNullOrWhiteSpace(selectedPrefabPath))
        {
            panel.SelectedEnemyPrefab = null;
            return;
        }

        for (int prefabIndex = 1; prefabIndex < panel.AvailableEnemyPrefabs.Count; prefabIndex++)
        {
            GameObject prefabAsset = panel.AvailableEnemyPrefabs[prefabIndex];
            string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);

            if (!string.Equals(prefabPath, selectedPrefabPath, System.StringComparison.Ordinal))
                continue;

            panel.SelectedEnemyPrefab = prefabAsset;
            return;
        }

        panel.SelectedEnemyPrefab = null;
        EnemyMasterPresetsPanelSidePanelUtility.SaveSelectedPrefabState(panel);
    }

    /// <summary>
    /// Resolves the selected prefab index inside the currently available enemy prefab list.
    /// </summary>
    /// <param name="panel">Owning panel that stores prefab candidates and selected prefab state.</param>
    /// <returns>Returns the popup index for the current selection, or zero when no valid selection exists.</returns>
    public static int ResolveSelectedEnemyPrefabIndex(EnemyMasterPresetsPanel panel)
    {
        if (panel.SelectedEnemyPrefab == null)
            return 0;

        string selectedPrefabPath = AssetDatabase.GetAssetPath(panel.SelectedEnemyPrefab);

        if (string.IsNullOrWhiteSpace(selectedPrefabPath))
            return 0;

        for (int prefabIndex = 1; prefabIndex < panel.AvailableEnemyPrefabs.Count; prefabIndex++)
        {
            GameObject prefabAsset = panel.AvailableEnemyPrefabs[prefabIndex];
            string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);

            if (string.Equals(prefabPath, selectedPrefabPath, System.StringComparison.Ordinal))
                return prefabIndex;
        }

        panel.SelectedEnemyPrefab = null;
        EnemyMasterPresetsPanelSidePanelUtility.SaveSelectedPrefabState(panel);
        return 0;
    }

    /// <summary>
    /// Refreshes prefab selection UI and related status labels after rescanning the project.
    /// </summary>
    /// <param name="panel">Owning panel that stores prefab selection state.</param>

    public static void RefreshEnemyPrefabSelection(EnemyMasterPresetsPanel panel)
    {
        RefreshAvailableEnemyPrefabs(panel);

        if (panel.ActiveDetailsSection == EnemyMasterPresetsPanel.DetailsSectionType.ActivePreset)
        {
            panel.BuildActiveDetailsSection();
            return;
        }

        RefreshActiveStatus(panel);
    }

    /// <summary>
    /// Pings the currently selected enemy prefab asset in the Project window.
    /// </summary>
    /// <param name="panel">Owning panel that stores prefab selection state.</param>

    public static void PingSelectedEnemyPrefab(EnemyMasterPresetsPanel panel)
    {
        if (panel.SelectedEnemyPrefab == null)
            return;

        Selection.activeObject = panel.SelectedEnemyPrefab;
        EditorGUIUtility.PingObject(panel.SelectedEnemyPrefab);
    }

    /// <summary>
    /// Assigns the selected enemy master preset and linked sub presets to the currently selected enemy prefab.
    /// </summary>
    /// <param name="panel">Owning panel that stores selected preset and selected prefab state.</param>

    public static void AssignPresetToPrefab(EnemyMasterPresetsPanel panel)
    {
        if (panel.SelectedPreset == null)
        {
            EditorUtility.DisplayDialog("Set Active Preset", "Select an enemy master preset first.", "OK");
            return;
        }

        if (panel.SelectedEnemyPrefab == null)
        {
            EditorUtility.DisplayDialog("Set Active Preset", "Select an enemy prefab first.", "OK");
            return;
        }

        EnemyAuthoring authoring = EnemyMasterPresetsPanel.ResolveEnemyAuthoringInPrefab(panel.SelectedEnemyPrefab);

        if (authoring == null)
        {
            EditorUtility.DisplayDialog("Set Active Preset", "EnemyAuthoring component not found on the selected prefab.", "OK");
            return;
        }

        SerializedObject serializedAuthoring = new SerializedObject(authoring);
        SerializedProperty masterPresetProperty = serializedAuthoring.FindProperty("masterPreset");
        SerializedProperty brainPresetProperty = serializedAuthoring.FindProperty("brainPreset");
        SerializedProperty visualPresetProperty = serializedAuthoring.FindProperty("visualPreset");
        SerializedProperty advancedPatternPresetProperty = serializedAuthoring.FindProperty("advancedPatternPreset");
        SerializedProperty bossPatternPresetProperty = serializedAuthoring.FindProperty("bossPatternPreset");

        if (masterPresetProperty == null || brainPresetProperty == null || visualPresetProperty == null || advancedPatternPresetProperty == null || bossPatternPresetProperty == null)
        {
            EditorUtility.DisplayDialog("Set Active Preset",
                                        "One or more preset properties are missing on EnemyAuthoring (masterPreset, brainPreset, visualPreset, advancedPatternPreset, bossPatternPreset).",
                                        "OK");
            return;
        }

        Undo.RecordObject(authoring, "Set Active Enemy Master Preset");
        serializedAuthoring.Update();
        masterPresetProperty.objectReferenceValue = panel.SelectedPreset;
        brainPresetProperty.objectReferenceValue = panel.SelectedPreset.BrainPreset;
        visualPresetProperty.objectReferenceValue = panel.SelectedPreset.VisualPreset;
        advancedPatternPresetProperty.objectReferenceValue = panel.SelectedPreset.AdvancedPatternPreset;
        bossPatternPresetProperty.objectReferenceValue = panel.SelectedPreset.BossPatternPreset;
        serializedAuthoring.ApplyModifiedProperties();
        EditorUtility.SetDirty(authoring);

        EnemyVisualPreset visualPreset = panel.SelectedPreset.VisualPreset;

        if (visualPreset != null)
        {
            SerializedObject visualPresetSerializedObject = new SerializedObject(visualPreset);
            SerializedProperty prefabsProperty = visualPresetSerializedObject.FindProperty("prefabs");
            SerializedProperty enemyPrefabProperty = prefabsProperty != null ? prefabsProperty.FindPropertyRelative("enemyPrefab") : null;

            if (enemyPrefabProperty != null && enemyPrefabProperty.objectReferenceValue != panel.SelectedEnemyPrefab)
            {
                Undo.RecordObject(visualPreset, "Assign Enemy Prefab To Visual Preset");
                visualPresetSerializedObject.Update();
                enemyPrefabProperty.objectReferenceValue = panel.SelectedEnemyPrefab;
                visualPresetSerializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(visualPreset);
            }
        }

        if (PrefabUtility.IsPartOfPrefabInstance(authoring))
            PrefabUtility.RecordPrefabInstancePropertyModifications(authoring);

        EnemyManagementDraftSession.MarkDirty();
        RefreshActiveStatus(panel);
    }

    /// <summary>
    /// Reloads the selected prefab asset reference after prefab modifications.
    /// </summary>
    /// <param name="panel">Owning panel that stores selected prefab state and popup UI.</param>

    public static void ReloadSelectedPrefabReference(EnemyMasterPresetsPanel panel)
    {
        if (panel.SelectedEnemyPrefab == null)
            return;

        string prefabPath = AssetDatabase.GetAssetPath(panel.SelectedEnemyPrefab);

        if (string.IsNullOrWhiteSpace(prefabPath))
            return;

        GameObject reloadedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (reloadedPrefab == null)
            return;

        panel.SelectedEnemyPrefab = reloadedPrefab;

        if (panel.EnemyPrefabPopup != null)
            panel.EnemyPrefabPopup.SetValueWithoutNotify(panel.SelectedEnemyPrefab);

        EnemyMasterPresetsPanelSidePanelUtility.SaveSelectedPrefabState(panel);
    }

    /// <summary>
    /// Refreshes the active assignment status label for the selected enemy prefab.
    /// </summary>
    /// <param name="panel">Owning panel that stores selected preset, selected prefab and UI labels.</param>

    public static void RefreshActiveStatus(EnemyMasterPresetsPanel panel)
    {
        if (panel.ActiveStatusLabel == null)
            return;

        if (panel.SelectedPreset == null)
        {
            panel.ActiveStatusLabel.text = "No enemy master preset selected.";
            return;
        }

        if (panel.SelectedEnemyPrefab == null)
        {
            panel.ActiveStatusLabel.text = "No enemy prefab selected.";
            return;
        }

        EnemyAuthoring authoring = EnemyMasterPresetsPanel.ResolveEnemyAuthoringInPrefab(panel.SelectedEnemyPrefab);

        if (authoring == null)
        {
            panel.ActiveStatusLabel.text = "EnemyAuthoring component not found on prefab.";
            return;
        }

        bool isActive = authoring.MasterPreset == panel.SelectedPreset;
        panel.ActiveStatusLabel.text = isActive ? "Active on selected prefab." : "Not active on selected prefab.";
    }
    #endregion

    #endregion
}
