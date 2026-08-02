using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Verifies difficulty, categories, scene ownership and ordered parallel wave authoring end to end.
/// </summary>
public static class GameWaveDifficultyIntegrationSmokeTest
{
    #region Constants
    private const string PreservedSceneWavesFolder =
        "Assets/Scriptable Objects/Enemy/Wave Preset/Scene Waves";
    private const int ExpectedPreservedSceneWavePresetCount = 23;
    private const int ExpectedPreservedSceneWaveCellCount = 253;
    #endregion

    #region Methods

    #region Entry Point
    // [MenuItem("Tools/Tests/Game/Wave Difficulty Integration Smoke Test")]
    /// <summary>
    /// Runs the wave and difficulty integration audit and fails batch execution when any invariant is unresolved.
    /// </summary>
    public static void Run()
    {
        List<string> failures = new List<string>();
        GameMasterPreset gameMasterPreset = FindFirstAsset<GameMasterPreset>();

        if (gameMasterPreset == null)
        {
            failures.Add("No Game Master preset exists.");
            Complete(failures);
            return;
        }

        GameDifficultyScalingPreset difficultyPreset = gameMasterPreset.DifficultyScalingPreset;
        GameWavesPreset wavesPreset = gameMasterPreset.WavesPreset;
        ValidateDifficulty(difficultyPreset, failures);
        ValidateWaves(wavesPreset, failures);
        ValidateWaveAssets(wavesPreset, failures);
        ValidatePreservedSceneWaveAssets(failures);
        ValidateEmbeddedPreviewLoading(wavesPreset, failures);
        Complete(failures);
    }
    #endregion

    #region Difficulty Validation
    /// <summary>
    /// Verifies that the default difficulty graph is valid, ordered and connected to Player context.
    /// </summary>
    /// <param name="preset">Difficulty Scaling preset assigned by Game Master.</param>
    /// <param name="failures">Output failure list.</param>
    private static void ValidateDifficulty(GameDifficultyScalingPreset preset, List<string> failures)
    {
        if (preset == null)
        {
            failures.Add("Game Master has no Difficulty Scaling preset.");
            return;
        }

        List<string> warnings = GameDifficultyScalingValidationUtility.BuildWarnings(preset);

        for (int warningIndex = 0; warningIndex < warnings.Count; warningIndex++)
            failures.Add("Difficulty validation: " + warnings[warningIndex]);

        if (preset.PlayerContextPreset == null)
            failures.Add("Difficulty Scaling preset has no Player context preset.");

        if (!preset.TryFindCoefficient("enemyIntensity", out GameDifficultyCoefficientDefinition enemyIntensity) ||
            enemyIntensity == null)
        {
            failures.Add("Default enemyIntensity coefficient is missing.");
        }

        if (!preset.TryFindCoefficient("rewardTier", out GameDifficultyCoefficientDefinition rewardTier) ||
            rewardTier == null)
        {
            failures.Add("Default rewardTier coefficient is missing.");
        }

        if (!GameDifficultyScalingValidationUtility.TryBuildEvaluationOrder(preset,
                                                                             out List<GameDifficultyCoefficientDefinition> order,
                                                                             out string orderError))
        {
            failures.Add("Difficulty evaluation order failed: " + orderError);
        }
        else if (order.Count != preset.Coefficients.Count)
        {
            failures.Add("Difficulty evaluation order omits one or more coefficients.");
        }

        if (preset.PlayerContextPreset != null && preset.PlayerContextPreset.ProgressionPreset != null)
        {
            SerializedObject progressionObject = new SerializedObject(preset.PlayerContextPreset.ProgressionPreset);
            List<string> cycleWarnings =
                PlayerScalingDependencyValidationUtility.BuildDifficultyCrossDependencyWarnings(
                    progressionObject.FindProperty("scalableStats"),
                    progressionObject.FindProperty("scalingRules"));

            for (int warningIndex = 0; warningIndex < cycleWarnings.Count; warningIndex++)
                failures.Add(cycleWarnings[warningIndex]);
        }
    }
    #endregion

    #region Waves Validation
    /// <summary>
    /// Verifies brush categories, scene ownership and unique SubScene spawners for every mapped room.
    /// </summary>
    /// <param name="preset">Waves preset assigned by Game Master.</param>
    /// <param name="failures">Output failure list.</param>
    private static void ValidateWaves(GameWavesPreset preset, List<string> failures)
    {
        if (preset == null)
        {
            failures.Add("Game Master has no Waves preset.");
            return;
        }

        List<string> warnings = GameWavesValidationUtility.BuildWarnings(preset);

        for (int warningIndex = 0; warningIndex < warnings.Count; warningIndex++)
            failures.Add("Waves validation: " + warnings[warningIndex]);

        if (preset.BrushCategories.Count == 0)
            failures.Add("Waves preset contains no brush categories.");

        if (preset.SceneMappings.Count == 0)
            failures.Add("Waves preset contains no room scene mappings.");

        // Reopen only isolated preview scenes to verify one-to-one ownership without changing editor scene state.
        for (int mappingIndex = 0; mappingIndex < preset.SceneMappings.Count; mappingIndex++)
        {
            GameWaveSceneDefinition mapping = preset.SceneMappings[mappingIndex];

            if (mapping == null)
                continue;

            if (!GameWaveSceneEditorUtility.TryResolveSingleSubScene(mapping.MainScenePath,
                                                                              out string resolvedSubScenePath,
                                                                              out string subSceneWarning))
            {
                failures.Add(subSceneWarning);
                continue;
            }

            if (!string.Equals(resolvedSubScenePath, mapping.SubScenePath, StringComparison.OrdinalIgnoreCase))
                failures.Add("Mapped SubScene path is stale for '" + mapping.MainScenePath + "'.");

            if (!GameWaveSceneEditorUtility.TryResolveSingleSpawner(mapping.SubScenePath,
                                                                             out EnemyWavePreset wavePreset,
                                                                             out string spawnerWarning))
            {
                failures.Add(spawnerWarning);
                continue;
            }

            if (wavePreset != mapping.WavePreset)
                failures.Add("Mapped wave preset differs from the unique spawner in '" + mapping.SubScenePath + "'.");
        }
    }
    #endregion

    #region Wave Validation
    /// <summary>
    /// Verifies that every current wave cell uses a valid category and combined room assets retain parallel steps.
    /// </summary>
    /// <param name="wavesPreset">Expected category source for every wave asset.</param>
    /// <param name="failures">Output failure list.</param>
    private static void ValidateWaveAssets(GameWavesPreset wavesPreset,
                                           List<string> failures)
    {
        List<EnemyWavePreset> wavePresets = FindAssets<EnemyWavePreset>();

        for (int presetIndex = 0; presetIndex < wavePresets.Count; presetIndex++)
        {
            EnemyWavePreset wavePreset = wavePresets[presetIndex];

            if (wavePreset.WavesPreset != wavesPreset)
                failures.Add("Wave preset '" + wavePreset.name + "' is not linked to the active category source.");

            for (int waveIndex = 0; waveIndex < wavePreset.Waves.Count; waveIndex++)
            {
                EnemySpawnWaveAuthoring wave = wavePreset.Waves[waveIndex];

                if (wave == null)
                    continue;

                for (int cellIndex = 0; cellIndex < wave.PaintedCells.Count; cellIndex++)
                {
                    EnemySpawnWaveCellAuthoring cell = wave.PaintedCells[cellIndex];

                    if (cell == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(cell.BrushCategoryId) ||
                        wavesPreset == null ||
                        !wavesPreset.TryFindBrushCategory(cell.BrushCategoryId,
                                                               out EnemyBrushCategoryDefinition ignoredCategory))
                    {
                        failures.Add("Wave preset '" + wavePreset.name + "' contains an unresolved brush category.");
                    }

                }
            }

            if (AssetDatabase.GetAssetPath(wavePreset).IndexOf("Parallel Rooms",
                                                               StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ValidateParallelSteps(wavePreset, failures);
            }
        }
    }

    /// <summary>
    /// Verifies that one combined room asset contains at least one step with multiple parallel waves.
    /// </summary>
    /// <param name="preset">Combined room wave asset.</param>
    /// <param name="failures">Output failure list.</param>
    private static void ValidateParallelSteps(EnemyWavePreset preset, List<string> failures)
    {
        Dictionary<int, int> waveCountByStep = new Dictionary<int, int>();

        for (int waveIndex = 0; waveIndex < preset.Waves.Count; waveIndex++)
        {
            EnemySpawnWaveAuthoring wave = preset.Waves[waveIndex];

            if (wave == null)
                continue;

            if (waveCountByStep.TryGetValue(wave.SequenceStepIndex, out int waveCount))
                waveCountByStep[wave.SequenceStepIndex] = waveCount + 1;
            else
                waveCountByStep.Add(wave.SequenceStepIndex, 1);
        }

        foreach (KeyValuePair<int, int> entry in waveCountByStep)
        {
            if (entry.Value > 1)
                return;
        }

        failures.Add("Combined wave asset '" + preset.name + "' does not retain a parallel sequence step.");
    }

    /// <summary>
    /// Verifies that every old inline scene setup exists as one independent, sequential wave asset.
    /// </summary>
    /// <param name="failures">Output failure list.</param>
    private static void ValidatePreservedSceneWaveAssets(List<string> failures)
    {
        List<EnemyWavePreset> allWavePresets = FindAssets<EnemyWavePreset>();
        int preservedPresetCount = 0;
        int preservedCellCount = 0;

        // Count and inspect only the dedicated one-to-one scene setup assets.
        for (int presetIndex = 0; presetIndex < allWavePresets.Count; presetIndex++)
        {
            EnemyWavePreset wavePreset = allWavePresets[presetIndex];
            string assetPath = AssetDatabase.GetAssetPath(wavePreset);

            if (!assetPath.StartsWith(PreservedSceneWavesFolder, StringComparison.OrdinalIgnoreCase))
                continue;

            preservedPresetCount++;

            for (int waveIndex = 0; waveIndex < wavePreset.Waves.Count; waveIndex++)
            {
                EnemySpawnWaveAuthoring wave = wavePreset.Waves[waveIndex];

                if (wave == null)
                    continue;

                preservedCellCount += wave.PaintedCells.Count;

                if (wave.SequenceStepIndex != waveIndex)
                    failures.Add("Preserved scene asset '" + wavePreset.name +
                                 "' contains parallel or reordered waves.");

                if (!string.IsNullOrWhiteSpace(wave.ReferenceWaveId) || wave.UseDifficultySelection)
                    failures.Add("Preserved scene asset '" + wavePreset.name +
                                 "' contains a new dependency or difficulty override.");
            }
        }

        if (preservedPresetCount != ExpectedPreservedSceneWavePresetCount)
        {
            failures.Add("Expected " + ExpectedPreservedSceneWavePresetCount +
                         " preserved scene Wave presets, but found " + preservedPresetCount + ".");
        }

        if (preservedCellCount != ExpectedPreservedSceneWaveCellCount)
            failures.Add("Preserved scene Wave assets contain " + preservedCellCount +
                         " cells instead of " + ExpectedPreservedSceneWaveCellCount + ".");
    }

    /// <summary>
    /// Loads every mapped room through the embedded renderer and detects duplicate SubScene warnings.
    /// </summary>
    /// <param name="wavesPreset">Waves preset supplying mapped rooms.</param>
    /// <param name="failures">Output failure list.</param>
    private static void ValidateEmbeddedPreviewLoading(GameWavesPreset wavesPreset,
                                                       List<string> failures)
    {
        if (wavesPreset == null)
            return;

        GameWaveSceneDefinition loadedMapping = FindFirstCompleteMapping(wavesPreset);
        Scene openedScene = default;

        // Reproduce the interactive Editor case where Scene Brush targets the room already open by the designer.
        if (loadedMapping != null)
        {
            Scene existingScene = SceneManager.GetSceneByPath(loadedMapping.MainScenePath);

            if (!existingScene.IsValid() || !existingScene.isLoaded)
                openedScene = EditorSceneManager.OpenScene(loadedMapping.MainScenePath, OpenSceneMode.Additive);
        }

        bool duplicateSubSceneWarning = false;
        Application.LogCallback logCallback = (condition, stackTrace, logType) =>
        {
            if (condition.IndexOf("Sub Scenes can not reference the same scene",
                                  StringComparison.OrdinalIgnoreCase) >= 0)
                duplicateSubSceneWarning = true;
        };
        Application.logMessageReceived += logCallback;
        GameWavesPreviewRenderer renderer = new GameWavesPreviewRenderer();

        try
        {
            if (loadedMapping != null)
                renderer.Load(loadedMapping.MainScenePath, loadedMapping.SubScenePath);

            // Exercise the exact main-scene plus SubScene clone path used by Scene Brush.
            for (int mappingIndex = 0; mappingIndex < wavesPreset.SceneMappings.Count; mappingIndex++)
            {
                GameWaveSceneDefinition mapping = wavesPreset.SceneMappings[mappingIndex];

                if (mapping != null)
                    renderer.Load(mapping.MainScenePath, mapping.SubScenePath);
            }
        }
        finally
        {
            renderer.Dispose();
            Application.logMessageReceived -= logCallback;

            if (openedScene.IsValid())
                EditorSceneManager.CloseScene(openedScene, true);
        }

        if (duplicateSubSceneWarning)
            failures.Add("Embedded preview loading emitted a duplicate SubScene reference warning.");
    }

    /// <summary>
    /// Finds one scene mapping that can exercise both managed-room and ECS SubScene loading paths.
    /// </summary>
    /// <param name="wavesPreset">Waves preset supplying mapped rooms.</param>
    /// <returns>First mapping with both scene paths, or null when none is complete.</returns>
    private static GameWaveSceneDefinition FindFirstCompleteMapping(GameWavesPreset wavesPreset)
    {
        for (int mappingIndex = 0; mappingIndex < wavesPreset.SceneMappings.Count; mappingIndex++)
        {
            GameWaveSceneDefinition mapping = wavesPreset.SceneMappings[mappingIndex];

            if (mapping != null &&
                !string.IsNullOrWhiteSpace(mapping.MainScenePath) &&
                !string.IsNullOrWhiteSpace(mapping.SubScenePath))
            {
                return mapping;
            }
        }

        return null;
    }
    #endregion

    #region Result and Asset Helpers
    /// <summary>
    /// Completes the smoke test with a clean summary or one aggregate exception.
    /// </summary>
    /// <param name="failures">Collected integration failures.</param>
    private static void Complete(List<string> failures)
    {
        if (failures.Count == 0)
        {
            Debug.Log("Game Wave/Difficulty integration smoke test passed.");
            return;
        }

        throw new InvalidOperationException("Game Wave/Difficulty integration smoke test failed:\n- " +
                                            string.Join("\n- ", failures));
    }

    /// <summary>
    /// Finds all project assets of one Unity object type in deterministic path order.
    /// </summary>
    /// <typeparam name="TAsset">Unity asset type requested.</typeparam>
    /// <returns>Sorted loaded assets.</returns>
    private static List<TAsset> FindAssets<TAsset>() where TAsset : UnityEngine.Object
    {
        List<TAsset> assets = new List<TAsset>();
        string[] assetGuids = AssetDatabase.FindAssets("t:" + typeof(TAsset).Name, new string[] { "Assets" });

        for (int assetIndex = 0; assetIndex < assetGuids.Length; assetIndex++)
        {
            TAsset asset = AssetDatabase.LoadAssetAtPath<TAsset>(AssetDatabase.GUIDToAssetPath(assetGuids[assetIndex]));

            if (asset != null)
                assets.Add(asset);
        }

        assets.Sort((left, right) => string.Compare(AssetDatabase.GetAssetPath(left),
                                                    AssetDatabase.GetAssetPath(right),
                                                    StringComparison.Ordinal));
        return assets;
    }

    /// <summary>
    /// Resolves the first deterministic project asset of one type.
    /// </summary>
    /// <typeparam name="TAsset">Unity asset type requested.</typeparam>
    /// <returns>First sorted asset, or null when none exists.</returns>
    private static TAsset FindFirstAsset<TAsset>() where TAsset : UnityEngine.Object
    {
        List<TAsset> assets = FindAssets<TAsset>();
        return assets.Count > 0 ? assets[0] : null;
    }
    #endregion

    #endregion
}
