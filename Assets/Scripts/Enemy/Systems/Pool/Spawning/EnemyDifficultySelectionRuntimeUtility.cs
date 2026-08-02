using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Resolves deterministic difficulty-gated wave and brush-category choices for enemy spawners.
/// </summary>
public static class EnemyDifficultySelectionRuntimeUtility
{
    #region Methods

    #region Wave Selection
    /// <summary>
    /// Resolves every pending wave selection group once using the authoritative difficulty projection.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate wave runtime entries.</param>
    /// <param name="spawnerEntity">Spawner owning definitions and mutable wave state.</param>
    /// <param name="spawnerState">Spawner clock used for disabled-wave completion timestamps.</param>
    public static void ResolveWaveSelections(EntityManager entityManager,
                                             Entity spawnerEntity,
                                             in EnemySpawnerState spawnerState)
    {
        DynamicBuffer<EnemySpawnerWaveDefinitionElement> definitions =
            entityManager.GetBuffer<EnemySpawnerWaveDefinitionElement>(spawnerEntity, true);
        DynamicBuffer<EnemySpawnerWaveRuntimeElement> runtime =
            entityManager.GetBuffer<EnemySpawnerWaveRuntimeElement>(spawnerEntity);

        // Resolve each authored group only once; resolving one entry resolves all siblings.
        for (int waveIndex = 0; waveIndex < definitions.Length && waveIndex < runtime.Length; waveIndex++)
        {
            EnemySpawnerWaveRuntimeElement waveRuntime = runtime[waveIndex];

            if (waveRuntime.SelectionResolved != 0)
                continue;

            EnemySpawnerWaveDefinitionElement definition = definitions[waveIndex];

            if (definition.DifficultySelectionGroupId.Length == 0)
            {
                waveRuntime.SelectionResolved = 1;
                waveRuntime.Enabled = 1;
                runtime[waveIndex] = waveRuntime;
                continue;
            }

            ResolveWaveSelectionGroup(definitions,
                                      runtime,
                                      definition.DifficultySelectionGroupId,
                                      ResolveSpawnerSelectionSeed(entityManager,
                                                                  spawnerEntity,
                                                                  definition.DifficultySelectionGroupId.GetHashCode()),
                                      spawnerState.StartTime);
        }
    }

    /// <summary>
    /// Selects one eligible weighted wave and marks all sibling candidates resolved.
    /// </summary>
    /// <param name="definitions">Immutable wave definitions.</param>
    /// <param name="runtime">Mutable wave runtime buffer.</param>
    /// <param name="selectionGroupId">Group identifier being resolved.</param>
    /// <param name="selectionSeed">Deterministic spawner and group seed.</param>
    /// <param name="completionTime">Logical completion timestamp assigned to skipped candidates.</param>
    private static void ResolveWaveSelectionGroup(DynamicBuffer<EnemySpawnerWaveDefinitionElement> definitions,
                                                  DynamicBuffer<EnemySpawnerWaveRuntimeElement> runtime,
                                                  FixedString64Bytes selectionGroupId,
                                                  uint selectionSeed,
                                                  float completionTime)
    {
        float totalWeight = 0f;

        // Sum only candidates valid for the current coefficient value.
        for (int waveIndex = 0; waveIndex < definitions.Length && waveIndex < runtime.Length; waveIndex++)
        {
            EnemySpawnerWaveDefinitionElement candidate = definitions[waveIndex];

            if (!candidate.DifficultySelectionGroupId.Equals(selectionGroupId) || !IsDifficultyEligible(candidate))
                continue;

            totalWeight += math.max(0f, candidate.SelectionWeight);
        }

        float selectionPoint = HashToUnitFloat(selectionSeed) * totalWeight;
        int selectedWaveIndex = -1;
        float cumulativeWeight = 0f;

        // Locate the deterministic weighted candidate.
        for (int waveIndex = 0; waveIndex < definitions.Length && waveIndex < runtime.Length; waveIndex++)
        {
            EnemySpawnerWaveDefinitionElement candidate = definitions[waveIndex];

            if (!candidate.DifficultySelectionGroupId.Equals(selectionGroupId) || !IsDifficultyEligible(candidate))
                continue;

            cumulativeWeight += math.max(0f, candidate.SelectionWeight);

            if (selectedWaveIndex < 0 && cumulativeWeight >= selectionPoint && cumulativeWeight > 0f)
                selectedWaveIndex = waveIndex;
        }

        // Mark skipped choices completed so dependency chains cannot deadlock.
        for (int waveIndex = 0; waveIndex < definitions.Length && waveIndex < runtime.Length; waveIndex++)
        {
            if (!definitions[waveIndex].DifficultySelectionGroupId.Equals(selectionGroupId))
                continue;

            EnemySpawnerWaveRuntimeElement candidateRuntime = runtime[waveIndex];
            candidateRuntime.SelectionResolved = 1;
            candidateRuntime.Enabled = waveIndex == selectedWaveIndex ? (byte)1 : (byte)0;

            if (candidateRuntime.Enabled == 0)
            {
                candidateRuntime.SpawnFinished = 1;
                candidateRuntime.Completed = 1;
                candidateRuntime.CompletionTime = completionTime;
            }

            runtime[waveIndex] = candidateRuntime;
        }
    }

    /// <summary>
    /// Checks whether one grouped wave range contains the current requested coefficient value.
    /// </summary>
    /// <param name="definition">Wave candidate being tested.</param>
    /// <returns>True when its coefficient exists and lies in the inclusive authored range.</returns>
    private static bool IsDifficultyEligible(EnemySpawnerWaveDefinitionElement definition)
    {
        float coefficientValue = 0f;

        if (definition.DifficultyCoefficientId.Length > 0 &&
            !GameDifficultyRuntimeValueStore.TryGetValue(definition.DifficultyCoefficientId.ToString(), out coefficientValue))
        {
            return false;
        }

        return coefficientValue >= definition.MinimumDifficulty && coefficientValue <= definition.MaximumDifficulty;
    }
    #endregion

    #region Category Selection
    /// <summary>
    /// Resolves all weighted prefab candidates belonging to one logical category spawn key.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read and update the spawner event buffer.</param>
    /// <param name="spawnerEntity">Spawner owning the event candidates.</param>
    /// <param name="eventIndex">Current event index requesting resolution.</param>
    /// <param name="waveEvent">Current event value updated after group resolution.</param>
    /// <returns>True when this event is the selected candidate and should continue through spawn processing.</returns>
    public static bool ResolveCategoryEventSelection(EntityManager entityManager,
                                                     Entity spawnerEntity,
                                                     int eventIndex,
                                                     ref EnemySpawnerWaveEventElement waveEvent)
    {
        if (waveEvent.CategorySelectionKey <= 0)
            return true;

        if (waveEvent.CategorySelectionState != 0)
            return waveEvent.CategorySelectionState == 1;

        DynamicBuffer<EnemySpawnerWaveEventElement> events =
            entityManager.GetBuffer<EnemySpawnerWaveEventElement>(spawnerEntity);
        float coefficientValue = ResolveCoefficientValue(waveEvent.DifficultyCoefficientId);
        float totalWeight = 0f;

        // Sum eligible preset variants that represent this logical brushed spawn.
        for (int candidateIndex = 0; candidateIndex < events.Length; candidateIndex++)
        {
            EnemySpawnerWaveEventElement candidate = events[candidateIndex];

            if (!IsCategoryCandidateEligible(candidate, waveEvent.CategorySelectionKey, coefficientValue))
                continue;

            totalWeight += math.max(0f, candidate.SelectionWeight);
        }

        uint seed = ResolveSpawnerSelectionSeed(entityManager, spawnerEntity, waveEvent.CategorySelectionKey);
        float selectionPoint = HashToUnitFloat(seed) * totalWeight;
        float cumulativeWeight = 0f;
        int selectedEventIndex = -1;

        // Locate one stable weighted candidate without allocating or mutating authoring data.
        for (int candidateIndex = 0; candidateIndex < events.Length; candidateIndex++)
        {
            EnemySpawnerWaveEventElement candidate = events[candidateIndex];

            if (!IsCategoryCandidateEligible(candidate, waveEvent.CategorySelectionKey, coefficientValue))
                continue;

            cumulativeWeight += math.max(0f, candidate.SelectionWeight);

            if (selectedEventIndex < 0 && cumulativeWeight >= selectionPoint && cumulativeWeight > 0f)
                selectedEventIndex = candidateIndex;
        }

        // Persist both the winner and skipped candidates for all later spawn passes.
        for (int candidateIndex = 0; candidateIndex < events.Length; candidateIndex++)
        {
            EnemySpawnerWaveEventElement candidate = events[candidateIndex];

            if (candidate.CategorySelectionKey != waveEvent.CategorySelectionKey)
                continue;

            candidate.CategorySelectionState = candidateIndex == selectedEventIndex ? (byte)1 : (byte)2;
            events[candidateIndex] = candidate;
        }

        waveEvent = events[eventIndex];
        return waveEvent.CategorySelectionState == 1;
    }

    /// <summary>
    /// Resolves the current coefficient value or a sentinel that rejects every ranged candidate.
    /// </summary>
    /// <param name="coefficientId">Optional coefficient identifier baked with the category event.</param>
    /// <returns>Zero for an unbound category, the current value when found, or a rejecting sentinel.</returns>
    private static float ResolveCoefficientValue(FixedString64Bytes coefficientId)
    {
        if (coefficientId.Length == 0)
            return 0f;

        if (GameDifficultyRuntimeValueStore.TryGetValue(coefficientId.ToString(), out float coefficientValue))
            return coefficientValue;

        return float.MinValue;
    }

    /// <summary>
    /// Checks key and inclusive difficulty range for one brush-category candidate.
    /// </summary>
    /// <param name="candidate">Candidate event being evaluated.</param>
    /// <param name="selectionKey">Logical brushed spawn key.</param>
    /// <param name="coefficientValue">Current category coefficient value.</param>
    /// <returns>True when the candidate participates in weighted selection.</returns>
    private static bool IsCategoryCandidateEligible(EnemySpawnerWaveEventElement candidate,
                                                    int selectionKey,
                                                    float coefficientValue)
    {
        return candidate.CategorySelectionKey == selectionKey &&
               coefficientValue >= candidate.MinimumDifficulty &&
               coefficientValue <= candidate.MaximumDifficulty;
    }
    #endregion

    #region Deterministic Randomness
    /// <summary>
    /// Builds a deterministic seed from baked spawner identity and one local selection discriminator.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read optional spawner identity.</param>
    /// <param name="spawnerEntity">Spawner whose identity contributes to the seed.</param>
    /// <param name="discriminator">Wave-group or logical spawn discriminator.</param>
    /// <returns>Stable non-zero hash suitable for weighted selection.</returns>
    private static uint ResolveSpawnerSelectionSeed(EntityManager entityManager,
                                                    Entity spawnerEntity,
                                                    int discriminator)
    {
        uint identityHash = entityManager.HasComponent<EnemySpawnerRuntimeIdentity>(spawnerEntity)
            ? (uint)entityManager.GetComponentData<EnemySpawnerRuntimeIdentity>(spawnerEntity).SpawnerGuid.GetHashCode()
            : (uint)spawnerEntity.Index;
        return math.hash(new uint2(identityHash, unchecked((uint)discriminator)));
    }

    /// <summary>
    /// Projects one deterministic hash into the half-open unit interval.
    /// </summary>
    /// <param name="hash">Deterministic selection hash.</param>
    /// <returns>Floating-point value in the range [0, 1).</returns>
    private static float HashToUnitFloat(uint hash)
    {
        return (hash & 0x00FFFFFFu) / 16777216f;
    }
    #endregion

    #endregion
}
