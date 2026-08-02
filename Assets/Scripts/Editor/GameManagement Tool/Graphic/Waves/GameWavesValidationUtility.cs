using System;
using System.Collections.Generic;

/// <summary>
/// Produces non-mutating authoring warnings for brush categories, scene mappings and ordered wave sequences.
/// </summary>
internal static class GameWavesValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates one Waves preset and every referenced wave asset without changing designer values.
    /// </summary>
    /// <param name="preset">Waves preset to inspect.</param>
    /// <returns>Ordered actionable warnings; an empty collection indicates a structurally valid setup.</returns>
    public static List<string> BuildWarnings(GameWavesPreset preset)
    {
        List<string> warnings = new List<string>();

        if (preset == null)
        {
            warnings.Add("Waves preset is missing.");
            return warnings;
        }

        HashSet<string> categoryIds = ValidateCategories(preset, warnings);
        HashSet<string> mappedMainScenes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int sceneIndex = 0; sceneIndex < preset.SceneMappings.Count; sceneIndex++)
        {
            GameWaveSceneDefinition mapping = preset.SceneMappings[sceneIndex];

            if (mapping == null)
            {
                warnings.Add("Scene mapping " + sceneIndex + " is null.");
                continue;
            }

            string context = string.IsNullOrWhiteSpace(mapping.DisplayName)
                ? "Scene mapping " + sceneIndex
                : "Scene mapping '" + mapping.DisplayName + "'";

            if (string.IsNullOrWhiteSpace(mapping.MainScenePath))
                warnings.Add(context + " has no managed main scene.");
            else if (!mappedMainScenes.Add(mapping.MainScenePath))
                warnings.Add(context + " duplicates main scene '" + mapping.MainScenePath + "'.");

            if (string.IsNullOrWhiteSpace(mapping.SubScenePath))
                warnings.Add(context + " has no resolved single SubScene.");

            if (mapping.WavePreset == null)
            {
                warnings.Add(context + " has no Enemy Wave preset.");
                continue;
            }

            if (mapping.WavePreset.WavesPreset != preset)
                warnings.Add(context + " references a wave asset that is not linked back to this Waves preset.");

            ValidateWaves(mapping.WavePreset, categoryIds, warnings);
        }

        return warnings;
    }
    #endregion

    #region Category Methods
    /// <summary>
    /// Validates stable category identities and weighted enemy candidates.
    /// </summary>
    /// <param name="preset">Preset containing brush categories.</param>
    /// <param name="warnings">Output warning list.</param>
    /// <returns>Unique non-empty category identifiers valid for painted cells.</returns>
    private static HashSet<string> ValidateCategories(GameWavesPreset preset, List<string> warnings)
    {
        HashSet<string> categoryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int categoryIndex = 0; categoryIndex < preset.BrushCategories.Count; categoryIndex++)
        {
            EnemyBrushCategoryDefinition category = preset.BrushCategories[categoryIndex];

            if (category == null)
            {
                warnings.Add("Brush category " + categoryIndex + " is null.");
                continue;
            }

            string context = "Brush category '" + category.DisplayName + "'";

            if (string.IsNullOrWhiteSpace(category.TechnicalId))
                warnings.Add(context + " has no stable technical ID.");
            else if (!categoryIds.Add(category.TechnicalId))
                warnings.Add(context + " duplicates technical ID '" + category.TechnicalId + "'.");

            if (category.Entries == null || category.Entries.Count == 0)
                warnings.Add(context + " has no enemy preset candidates.");

            for (int entryIndex = 0; entryIndex < category.Entries.Count; entryIndex++)
            {
                EnemyBrushCategoryEntry entry = category.Entries[entryIndex];

                if (entry == null || entry.MasterPreset == null)
                    warnings.Add(context + " candidate " + entryIndex + " has no Enemy Master preset.");
                else if (entry.MinimumDifficulty > entry.MaximumDifficulty)
                    warnings.Add(context + " candidate " + entryIndex + " has an inverted difficulty range.");
                else if (entry.SelectionWeight <= 0f)
                    warnings.Add(context + " candidate " + entryIndex + " must use a positive selection weight.");
            }
        }

        return categoryIds;
    }
    #endregion

    #region Wave Methods
    /// <summary>
    /// Validates wave identities, ordered steps, explicit prerequisites and painted category references.
    /// </summary>
    /// <param name="preset">Enemy wave preset being inspected.</param>
    /// <param name="categoryIds">Known reusable brush category identifiers.</param>
    /// <param name="warnings">Output warning list.</param>
    private static void ValidateWaves(EnemyWavePreset preset,
                                      HashSet<string> categoryIds,
                                      List<string> warnings)
    {
        Dictionary<string, EnemySpawnWaveAuthoring> wavesById =
            new Dictionary<string, EnemySpawnWaveAuthoring>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> difficultyGroupSteps = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        HashSet<int> sequenceSteps = new HashSet<int>();

        for (int waveIndex = 0; waveIndex < preset.Waves.Count; waveIndex++)
        {
            EnemySpawnWaveAuthoring wave = preset.Waves[waveIndex];

            if (wave == null)
            {
                warnings.Add("Wave asset '" + preset.name + "' contains a null wave at index " + waveIndex + ".");
                continue;
            }

            string context = "Wave '" + wave.WaveLabel + "' in '" + preset.name + "'";

            if (string.IsNullOrWhiteSpace(wave.WaveId))
                warnings.Add(context + " has no stable wave ID.");
            else if (wavesById.ContainsKey(wave.WaveId))
                warnings.Add(context + " duplicates wave ID '" + wave.WaveId + "'.");
            else
                wavesById.Add(wave.WaveId, wave);

            if (wave.SequenceStepIndex < 0)
                warnings.Add(context + " has a negative sequence step index.");
            else
                sequenceSteps.Add(wave.SequenceStepIndex);

            if (wave.StartDelaySeconds < 0f || wave.SpawnDurationSeconds < 0f)
                warnings.Add(context + " contains negative timing values.");

            ValidateDifficultySelection(wave, context, difficultyGroupSteps, warnings);

            HashSet<UnityEngine.Vector2Int> coordinates = new HashSet<UnityEngine.Vector2Int>();

            for (int cellIndex = 0; cellIndex < wave.PaintedCells.Count; cellIndex++)
            {
                EnemySpawnWaveCellAuthoring cell = wave.PaintedCells[cellIndex];

                if (cell == null)
                {
                    warnings.Add(context + " contains a null painted cell.");
                    continue;
                }

                if (!coordinates.Add(cell.CellCoordinate))
                    warnings.Add(context + " duplicates painted coordinate " + cell.CellCoordinate + ".");

                if (cell.EnemyCount <= 0)
                    warnings.Add(context + " cell " + cell.CellCoordinate + " has a non-positive enemy count.");

                if (!categoryIds.Contains(cell.BrushCategoryId))
                    warnings.Add(context + " cell " + cell.CellCoordinate + " references missing category '" +
                                 cell.BrushCategoryId + "'.");
            }
        }

        ValidateSequenceSteps(preset.name, sequenceSteps, warnings);

        // Resolve explicit references after the complete identity map is known.
        foreach (KeyValuePair<string, EnemySpawnWaveAuthoring> entry in wavesById)
        {
            if (!string.IsNullOrWhiteSpace(entry.Value.ReferenceWaveId) &&
                !wavesById.ContainsKey(entry.Value.ReferenceWaveId))
            {
                warnings.Add("Wave '" + entry.Value.WaveLabel + "' references missing prerequisite wave ID '" +
                             entry.Value.ReferenceWaveId + "'.");
            }

            if (!string.IsNullOrWhiteSpace(entry.Value.ReferenceWaveId) &&
                wavesById.TryGetValue(entry.Value.ReferenceWaveId, out EnemySpawnWaveAuthoring referencedWave) &&
                referencedWave.SequenceStepIndex >= entry.Value.SequenceStepIndex)
            {
                warnings.Add("Wave '" + entry.Value.WaveLabel +
                             "' must reference a wave from an earlier sequence step.");
            }
        }

        ValidateReferenceCycles(preset.name, wavesById, warnings);
    }

    /// <summary>
    /// Validates optional difficulty selection without mutating disabled or incomplete authored values.
    /// </summary>
    /// <param name="wave">Wave containing optional selection settings.</param>
    /// <param name="context">Readable wave context used by warnings.</param>
    /// <param name="difficultyGroupSteps">Known selection-group step ownership.</param>
    /// <param name="warnings">Output warning list.</param>
    private static void ValidateDifficultySelection(EnemySpawnWaveAuthoring wave,
                                                    string context,
                                                    IDictionary<string, int> difficultyGroupSteps,
                                                    List<string> warnings)
    {
        if (!wave.UseDifficultySelection)
            return;

        if (string.IsNullOrWhiteSpace(wave.DifficultySelectionGroupId))
            warnings.Add(context + " enables difficulty selection but has no selection group.");
        else if (difficultyGroupSteps.TryGetValue(wave.DifficultySelectionGroupId, out int groupStepIndex) &&
                 groupStepIndex != wave.SequenceStepIndex)
        {
            warnings.Add(context + " uses a selection group already assigned to a different sequence step.");
        }
        else
            difficultyGroupSteps[wave.DifficultySelectionGroupId] = wave.SequenceStepIndex;

        if (string.IsNullOrWhiteSpace(wave.DifficultyCoefficientId))
            warnings.Add(context + " enables difficulty selection but has no coefficient.");

        if (wave.SelectionWeight <= 0f)
            warnings.Add(context + " enables difficulty selection but has a non-positive weight.");

        if (wave.MinimumDifficulty > wave.MaximumDifficulty)
            warnings.Add(context + " has an inverted difficulty range.");
    }

    /// <summary>
    /// Validates that authored sequence step identifiers form a readable contiguous order beginning at zero.
    /// </summary>
    /// <param name="presetName">Wave asset name included in diagnostics.</param>
    /// <param name="sequenceSteps">Unique authored step identifiers.</param>
    /// <param name="warnings">Output warning list.</param>
    private static void ValidateSequenceSteps(string presetName,
                                              HashSet<int> sequenceSteps,
                                              List<string> warnings)
    {
        if (sequenceSteps.Count == 0)
            return;

        int maximumStepIndex = -1;

        foreach (int stepIndex in sequenceSteps)
            maximumStepIndex = Math.Max(maximumStepIndex, stepIndex);

        for (int stepIndex = 0; stepIndex <= maximumStepIndex; stepIndex++)
        {
            if (!sequenceSteps.Contains(stepIndex))
                warnings.Add("Wave asset '" + presetName + "' skips sequence step " + (stepIndex + 1) + ".");
        }
    }

    /// <summary>
    /// Detects circular explicit prerequisites that would prevent waves from ever scheduling.
    /// </summary>
    /// <param name="presetName">Wave asset name included in the diagnostic.</param>
    /// <param name="wavesById">Complete wave identity map for the asset.</param>
    /// <param name="warnings">Output warning list.</param>
    private static void ValidateReferenceCycles(string presetName,
                                                IReadOnlyDictionary<string, EnemySpawnWaveAuthoring> wavesById,
                                                List<string> warnings)
    {
        Dictionary<string, byte> visitStates = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        List<string> traversal = new List<string>();

        // Start a depth-first traversal from every unvisited wave identity.
        foreach (KeyValuePair<string, EnemySpawnWaveAuthoring> entry in wavesById)
        {
            if (!visitStates.ContainsKey(entry.Key))
                VisitWaveReference(presetName, entry.Key, wavesById, visitStates, traversal, warnings);
        }
    }

    /// <summary>
    /// Visits one explicit prerequisite chain and reports a cycle at the first back edge.
    /// </summary>
    /// <param name="presetName">Wave asset name included in the diagnostic.</param>
    /// <param name="waveId">Current wave identity being visited.</param>
    /// <param name="wavesById">Complete wave identity map for the asset.</param>
    /// <param name="visitStates">Depth-first visit state keyed by wave identity.</param>
    /// <param name="traversal">Current ordered prerequisite traversal.</param>
    /// <param name="warnings">Output warning list.</param>
    private static void VisitWaveReference(string presetName,
                                           string waveId,
                                           IReadOnlyDictionary<string, EnemySpawnWaveAuthoring> wavesById,
                                           IDictionary<string, byte> visitStates,
                                           List<string> traversal,
                                           List<string> warnings)
    {
        visitStates[waveId] = 1;
        traversal.Add(waveId);
        string referenceWaveId = wavesById[waveId].ReferenceWaveId;

        if (!string.IsNullOrWhiteSpace(referenceWaveId) && wavesById.ContainsKey(referenceWaveId))
        {
            if (!visitStates.TryGetValue(referenceWaveId, out byte referenceState))
            {
                VisitWaveReference(presetName,
                                   referenceWaveId,
                                   wavesById,
                                   visitStates,
                                   traversal,
                                   warnings);
            }
            else if (referenceState == 1)
            {
                int cycleStartIndex = traversal.FindIndex(candidateId =>
                    string.Equals(candidateId, referenceWaveId, StringComparison.OrdinalIgnoreCase));
                List<string> cycle = traversal.GetRange(cycleStartIndex, traversal.Count - cycleStartIndex);
                cycle.Add(referenceWaveId);
                warnings.Add("Wave asset '" + presetName + "' contains a circular prerequisite: " +
                             string.Join(" -> ", cycle) + ".");
            }
        }

        traversal.RemoveAt(traversal.Count - 1);
        visitStates[waveId] = 2;
    }
    #endregion

    #endregion
}
