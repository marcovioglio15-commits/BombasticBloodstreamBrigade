using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Verifies transactional spawner settings, finite-grid cleanup and bake bounds without modifying game assets.
/// </summary>
public static class GameWavesSceneBrushSettingsSmokeTest
{
    #region Constants
    private const string TemporaryFolder =
        "Assets/Scripts/Editor/GameManagement Tool/Tests/Temporary Scene Brush Settings";
    private const string TemporaryScenePath = TemporaryFolder + "/SpawnerSettings.unity";
    private const string ColoredFolderSettingsPath =
        "Assets/Scriptable Objects/Editor/ColoredFolders/ColoredFolderSettings_Scripts.asset";
    #endregion

    #region Methods

    #region Entry Point
    // [MenuItem("Tools/Tests/Game/Waves Scene Brush Settings Smoke Test")]
    /// <summary>
    /// Runs isolated Scene Brush settings and grid-resize checks and fails batch execution on any regression.
    /// </summary>
    public static void Run()
    {
        List<string> failures = new List<string>();

        try
        {
            CreateTemporarySpawnerScene();
            ValidateTransactionalSettings(failures);
            ValidateGridCleanup(failures);
            ValidateBakeBounds(failures);
        }
        catch (Exception exception)
        {
            failures.Add("Smoke test threw " + exception.GetType().Name + ": " + exception.Message);
        }
        finally
        {
            GameWavesSpawnerSettingsDraftSession.EndSession();
            CloseTemporaryScene();
            AssetDatabase.DeleteAsset(TemporaryFolder);
            RemoveTemporaryColoredFolderEntry();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Complete(failures);
    }
    #endregion

    #region Scene Methods
    /// <summary>
    /// Creates one isolated saved scene containing exactly one default enemy spawner.
    /// </summary>
    private static void CreateTemporarySpawnerScene()
    {
        EnsureFolder(TemporaryFolder);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject spawnerObject = new GameObject("Smoke Test Enemy Spawner");
        spawnerObject.AddComponent<EnemySpawnerAuthoring>();

        if (!EditorSceneManager.SaveScene(scene, TemporaryScenePath))
            throw new InvalidOperationException("Unable to save the temporary spawner scene.");

        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Closes the temporary scene only when a failed assertion left it loaded.
    /// </summary>
    private static void CloseTemporaryScene()
    {
        Scene scene = SceneManager.GetSceneByPath(TemporaryScenePath);

        if (scene.IsValid() && scene.isLoaded)
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    /// <summary>
    /// Creates one project-relative folder hierarchy without touching pre-existing folders.
    /// </summary>
    /// <param name="folderPath">Project-relative folder path to ensure.</param>
    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        int separatorIndex = folderPath.LastIndexOf('/');
        string parentPath = folderPath.Substring(0, separatorIndex);
        string folderName = folderPath.Substring(separatorIndex + 1);
        EnsureFolder(parentPath);
        AssetDatabase.CreateFolder(parentPath, folderName);
    }

    /// <summary>
    /// Removes only the deleted temporary folder from the editor color index and its parallel serialized arrays.
    /// </summary>
    private static void RemoveTemporaryColoredFolderEntry()
    {
        UnityEngine.Object settingsAsset = AssetDatabase.LoadMainAssetAtPath(ColoredFolderSettingsPath);

        if (settingsAsset == null)
            return;

        SerializedObject serializedSettings = new SerializedObject(settingsAsset);
        SerializedProperty folderPaths = serializedSettings.FindProperty("folderPaths");
        SerializedProperty folderColors = serializedSettings.FindProperty("folderColors");
        SerializedProperty folderApplyModes = serializedSettings.FindProperty("folderApplyModes");

        if (folderPaths == null)
            return;

        // Remove backwards in case a failed earlier run left more than one identical index entry.
        for (int folderIndex = folderPaths.arraySize - 1; folderIndex >= 0; folderIndex--)
        {
            if (!string.Equals(folderPaths.GetArrayElementAtIndex(folderIndex).stringValue,
                               TemporaryFolder,
                               StringComparison.Ordinal))
            {
                continue;
            }

            folderPaths.DeleteArrayElementAtIndex(folderIndex);

            if (folderColors != null && folderIndex < folderColors.arraySize)
                folderColors.DeleteArrayElementAtIndex(folderIndex);

            if (folderApplyModes != null && folderIndex < folderApplyModes.arraySize)
                folderApplyModes.DeleteArrayElementAtIndex(folderIndex);
        }

        serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settingsAsset);
    }
    #endregion

    #region Transaction Methods
    /// <summary>
    /// Verifies clean loading, pending detection, Discard restoration and Apply persistence for a SubScene draft.
    /// </summary>
    /// <param name="failures">Output list receiving actionable failures.</param>
    private static void ValidateTransactionalSettings(List<string> failures)
    {
        GameWavesSpawnerSettingsDraftSession.BeginSession();

        if (!GameWavesSpawnerSettingsDraftSession.TryGetOrCreate(TemporaryScenePath,
                                                                 out GameWavesSpawnerSettingsDraft draft,
                                                                 out string warning))
        {
            failures.Add("Unable to create the spawner-settings draft: " + warning);
            return;
        }

        ValidateDraftEditability(draft, failures);
        draft.hideFlags = HideFlags.HideAndDontSave;

        if (!GameWavesSpawnerSettingsDraftSession.TryGetOrCreate(TemporaryScenePath,
                                                                 out draft,
                                                                 out warning))
        {
            failures.Add("Unable to recover the cached spawner-settings draft: " + warning);
            return;
        }

        ValidateDraftEditability(draft, failures);
        int baselineGridSizeX = draft.GridSizeX;
        SetDraftInteger(draft, "gridSizeX", baselineGridSizeX + 2);

        if (!GameWavesSpawnerSettingsDraftSession.HasPendingChanges)
            failures.Add("Changing a grid setting did not create pending changes.");

        GameWavesSpawnerSettingsDraftSession.Discard();

        if (GameWavesSpawnerSettingsDraftSession.HasPendingChanges || draft.GridSizeX != baselineGridSizeX)
            failures.Add("Discard did not restore the clean grid-settings baseline.");

        SetDraftInteger(draft, "gridSizeX", baselineGridSizeX + 1);
        GameWavesSpawnerSettingsDraftSession.Apply();

        if (GameWavesSpawnerSettingsDraftSession.HasPendingChanges)
            failures.Add("Apply did not accept the spawner-settings baseline.");

        ValidateSavedGridSize(baselineGridSizeX + 1, failures);
    }

    /// <summary>
    /// Verifies the non-persistent settings object remains editable through UI Toolkit serialized bindings.
    /// </summary>
    /// <param name="draft">Transactional settings object displayed by Scene Brush.</param>
    /// <param name="failures">Output list receiving editability failures.</param>
    private static void ValidateDraftEditability(GameWavesSpawnerSettingsDraft draft,
                                                 List<string> failures)
    {
        SerializedObject serializedDraft = new SerializedObject(draft);
        SerializedProperty gridSizeProperty = serializedDraft.FindProperty("gridSizeX");

        if ((draft.hideFlags & HideFlags.NotEditable) != 0 ||
            gridSizeProperty == null ||
            !gridSizeProperty.editable)
        {
            failures.Add("The transient spawner-settings draft is read-only for UI Toolkit bindings.");
        }
    }

    /// <summary>
    /// Writes one integer draft property through the same serialized path used by Scene Brush fields.
    /// </summary>
    /// <param name="draft">Transactional settings object receiving the change.</param>
    /// <param name="propertyName">Private serialized integer field name.</param>
    /// <param name="value">Integer value to author.</param>
    private static void SetDraftInteger(GameWavesSpawnerSettingsDraft draft,
                                        string propertyName,
                                        int value)
    {
        SerializedObject serializedDraft = new SerializedObject(draft);
        serializedDraft.FindProperty(propertyName).intValue = value;
        serializedDraft.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Reopens the isolated scene and verifies the unique spawner received an applied grid value.
    /// </summary>
    /// <param name="expectedGridSizeX">Grid width expected after Apply.</param>
    /// <param name="failures">Output list receiving persistence failures.</param>
    private static void ValidateSavedGridSize(int expectedGridSizeX, List<string> failures)
    {
        Scene scene = SceneManager.GetSceneByPath(TemporaryScenePath);
        bool closeWhenComplete = false;

        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(TemporaryScenePath, OpenSceneMode.Single);
            closeWhenComplete = true;
        }

        EnemySpawnerAuthoring spawner = null;
        GameObject[] roots = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < roots.Length && spawner == null; rootIndex++)
            spawner = roots[rootIndex].GetComponentInChildren<EnemySpawnerAuthoring>(true);

        if (spawner == null || spawner.GridSizeX != expectedGridSizeX)
            failures.Add("Applied grid settings were not persisted to the unique scene spawner.");

        if (closeWhenComplete)
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }
    #endregion

    #region Grid Methods
    /// <summary>
    /// Verifies a valid grid reduction removes every cell outside the new bounds across all waves.
    /// </summary>
    /// <param name="failures">Output list receiving cleanup failures.</param>
    private static void ValidateGridCleanup(List<string> failures)
    {
        EnemyWavePreset wavePreset = ScriptableObject.CreateInstance<EnemyWavePreset>();

        try
        {
            SerializedObject serializedPreset = new SerializedObject(wavePreset);
            SerializedProperty waves = serializedPreset.FindProperty("waves");
            waves.InsertArrayElementAtIndex(0);
            SerializedProperty cells = waves.GetArrayElementAtIndex(0).FindPropertyRelative("paintedCells");
            InsertCell(cells, new Vector2Int(0, 0));
            InsertCell(cells, new Vector2Int(4, 1));
            InsertCell(cells, new Vector2Int(1, 6));
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
            int removedCount = GameWavesGridResizeUtility.RemoveOutOfBoundsCells(serializedPreset, 2, 2);
            cells = serializedPreset.FindProperty("waves")
                                    .GetArrayElementAtIndex(0)
                                    .FindPropertyRelative("paintedCells");

            if (removedCount != 2 || cells.arraySize != 1)
                failures.Add("Grid reduction did not remove exactly the two out-of-bounds painted cells.");

            if (cells.arraySize == 1 &&
                cells.GetArrayElementAtIndex(0)
                     .FindPropertyRelative("cellCoordinate")
                     .vector2IntValue != Vector2Int.zero)
            {
                failures.Add("Grid reduction removed or replaced the valid painted cell.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(wavePreset);
        }
    }

    /// <summary>
    /// Inserts one painted cell coordinate into a serialized wave cell array.
    /// </summary>
    /// <param name="cells">Serialized painted-cell array.</param>
    /// <param name="coordinate">Coordinate assigned to the inserted cell.</param>
    private static void InsertCell(SerializedProperty cells, Vector2Int coordinate)
    {
        int cellIndex = cells.arraySize;
        cells.InsertArrayElementAtIndex(cellIndex);
        cells.GetArrayElementAtIndex(cellIndex)
             .FindPropertyRelative("cellCoordinate")
             .vector2IntValue = coordinate;
    }

    /// <summary>
    /// Verifies the shared bounds predicate rejects every coordinate the ECS baker must ignore.
    /// </summary>
    /// <param name="failures">Output list receiving bake-bound failures.</param>
    private static void ValidateBakeBounds(List<string> failures)
    {
        if (!EnemySpawnerWaveBakeUtility.IsCellInsideGrid(new Vector2Int(1, 1), 2, 2))
            failures.Add("Bake bounds rejected a valid finite-grid coordinate.");

        if (EnemySpawnerWaveBakeUtility.IsCellInsideGrid(new Vector2Int(2, 1), 2, 2) ||
            EnemySpawnerWaveBakeUtility.IsCellInsideGrid(new Vector2Int(1, -1), 2, 2))
        {
            failures.Add("Bake bounds accepted an out-of-bounds painted coordinate.");
        }
    }
    #endregion

    #region Result Methods
    /// <summary>
    /// Reports success or throws one aggregate exception for batchmode visibility.
    /// </summary>
    /// <param name="failures">Collected smoke-test failures.</param>
    private static void Complete(List<string> failures)
    {
        if (failures.Count == 0)
        {
            Debug.Log("Waves Scene Brush settings smoke test passed.");
            return;
        }

        throw new InvalidOperationException("Waves Scene Brush settings smoke test failed:\n- " +
                                            string.Join("\n- ", failures));
    }
    #endregion

    #endregion
}
