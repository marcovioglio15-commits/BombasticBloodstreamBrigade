using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Resolves weighted Drop Items module selection consistently across experience, recovery and Extra Combo Points payloads.
/// </summary>
internal static class EnemyDropItemsModuleSelectionUtility
{
    #region Constants
    private const float MinimumSelectionWeight = 0.0001f;
    private const float RandomMinimum = 0.000001f;
    private const int SelectionCountSeedSalt = 7919;
    private const int SelectionScoreSeedSalt = 297121507;
    #endregion

    #region Methods

    #region NativeArray Selection
    /// <summary>
    /// Resolves whether a module should run for the current kill using a NativeArray selection snapshot.
    /// </summary>
    /// <param name="enemyEntity">Killed enemy entity used to seed deterministic selection.</param>
    /// <param name="killEventIndex">Killed-event index inside the current frame snapshot.</param>
    /// <param name="dropItemsConfig">Baked drop-items selection config.</param>
    /// <param name="selectionModules">Selection entries snapshotted before runtime pool expansion can invalidate buffers.</param>
    /// <param name="payloadKind">Payload kind owned by the module being tested.</param>
    /// <param name="moduleIndex">Type-local module index being tested.</param>
    /// <returns>True when the module should be evaluated for this kill.</returns>
    public static bool ShouldResolveModule(Entity enemyEntity,
                                           int killEventIndex,
                                           in EnemyDropItemsConfig dropItemsConfig,
                                           NativeArray<EnemyDropItemsModuleSelectionElement> selectionModules,
                                           EnemyDropItemsPayloadKind payloadKind,
                                           int moduleIndex)
    {
        if (dropItemsConfig.ModuleCombineMode == EnemyDropItemsModuleCombineMode.AllModules)
            return true;

        if (!selectionModules.IsCreated || selectionModules.Length <= 0)
            return true;

        int selectionIndex = FindSelectionIndex(selectionModules, payloadKind, moduleIndex);

        if (selectionIndex < 0)
            return false;

        int selectedModuleCount = ResolveSelectedModuleCount(enemyEntity,
                                                             killEventIndex,
                                                             in dropItemsConfig,
                                                             selectionModules.Length);

        if (selectedModuleCount <= 0)
            return false;

        if (selectedModuleCount >= selectionModules.Length)
            return true;

        return IsSelectedByWeightedRank(enemyEntity,
                                        killEventIndex,
                                        selectionModules,
                                        selectionIndex,
                                        selectedModuleCount);
    }

    /// <summary>
    /// Finds the selection entry that maps to one type-local payload module.
    /// </summary>
    /// <param name="selectionModules">Selection entries to inspect.</param>
    /// <param name="payloadKind">Payload kind owned by the requested module.</param>
    /// <param name="moduleIndex">Type-local module index to find.</param>
    /// <returns>Selection entry index, or -1 when the module is not selectable.</returns>
    private static int FindSelectionIndex(NativeArray<EnemyDropItemsModuleSelectionElement> selectionModules,
                                          EnemyDropItemsPayloadKind payloadKind,
                                          int moduleIndex)
    {
        for (int selectionIndex = 0; selectionIndex < selectionModules.Length; selectionIndex++)
        {
            EnemyDropItemsModuleSelectionElement selectionModule = selectionModules[selectionIndex];

            if (selectionModule.PayloadKind != payloadKind || selectionModule.ModuleIndex != moduleIndex)
                continue;

            return selectionIndex;
        }

        return -1;
    }

    /// <summary>
    /// Resolves whether one NativeArray selection entry is within the weighted rank cutoff for this kill.
    /// </summary>
    /// <param name="enemyEntity">Killed enemy entity used to seed deterministic selection.</param>
    /// <param name="killEventIndex">Killed-event index inside the current frame snapshot.</param>
    /// <param name="selectionModules">Selection entries to rank.</param>
    /// <param name="targetSelectionIndex">Selection entry being tested.</param>
    /// <param name="selectedModuleCount">Amount of modules selected for this kill.</param>
    /// <returns>True when the target entry is selected.</returns>
    private static bool IsSelectedByWeightedRank(Entity enemyEntity,
                                                 int killEventIndex,
                                                 NativeArray<EnemyDropItemsModuleSelectionElement> selectionModules,
                                                 int targetSelectionIndex,
                                                 int selectedModuleCount)
    {
        float targetScore = ResolveSelectionScore(enemyEntity,
                                                  killEventIndex,
                                                  targetSelectionIndex,
                                                  selectionModules[targetSelectionIndex].SelectionWeight);
        int betterRankCount = 0;

        for (int selectionIndex = 0; selectionIndex < selectionModules.Length; selectionIndex++)
        {
            if (selectionIndex == targetSelectionIndex)
                continue;

            float candidateScore = ResolveSelectionScore(enemyEntity,
                                                         killEventIndex,
                                                         selectionIndex,
                                                         selectionModules[selectionIndex].SelectionWeight);

            if (candidateScore < targetScore || (candidateScore == targetScore && selectionIndex < targetSelectionIndex))
                betterRankCount++;
        }

        return betterRankCount < selectedModuleCount;
    }
    #endregion

    #region DynamicBuffer Selection
    /// <summary>
    /// Resolves whether a module should run for the current kill using a dynamic buffer selection view.
    /// </summary>
    /// <param name="enemyEntity">Killed enemy entity used to seed deterministic selection.</param>
    /// <param name="killEventIndex">Killed-event index inside the current frame snapshot.</param>
    /// <param name="dropItemsConfig">Baked drop-items selection config.</param>
    /// <param name="selectionModules">Selection entries stored on the enemy.</param>
    /// <param name="payloadKind">Payload kind owned by the module being tested.</param>
    /// <param name="moduleIndex">Type-local module index being tested.</param>
    /// <returns>True when the module should be evaluated for this kill.</returns>
    public static bool ShouldResolveModule(Entity enemyEntity,
                                           int killEventIndex,
                                           in EnemyDropItemsConfig dropItemsConfig,
                                           DynamicBuffer<EnemyDropItemsModuleSelectionElement> selectionModules,
                                           EnemyDropItemsPayloadKind payloadKind,
                                           int moduleIndex)
    {
        if (dropItemsConfig.ModuleCombineMode == EnemyDropItemsModuleCombineMode.AllModules)
            return true;

        if (!selectionModules.IsCreated || selectionModules.Length <= 0)
            return true;

        int selectionIndex = FindSelectionIndex(selectionModules, payloadKind, moduleIndex);

        if (selectionIndex < 0)
            return false;

        int selectedModuleCount = ResolveSelectedModuleCount(enemyEntity,
                                                             killEventIndex,
                                                             in dropItemsConfig,
                                                             selectionModules.Length);

        if (selectedModuleCount <= 0)
            return false;

        if (selectedModuleCount >= selectionModules.Length)
            return true;

        return IsSelectedByWeightedRank(enemyEntity,
                                        killEventIndex,
                                        selectionModules,
                                        selectionIndex,
                                        selectedModuleCount);
    }

    /// <summary>
    /// Finds the dynamic-buffer selection entry that maps to one type-local payload module.
    /// </summary>
    /// <param name="selectionModules">Selection entries to inspect.</param>
    /// <param name="payloadKind">Payload kind owned by the requested module.</param>
    /// <param name="moduleIndex">Type-local module index to find.</param>
    /// <returns>Selection entry index, or -1 when the module is not selectable.</returns>
    private static int FindSelectionIndex(DynamicBuffer<EnemyDropItemsModuleSelectionElement> selectionModules,
                                          EnemyDropItemsPayloadKind payloadKind,
                                          int moduleIndex)
    {
        for (int selectionIndex = 0; selectionIndex < selectionModules.Length; selectionIndex++)
        {
            EnemyDropItemsModuleSelectionElement selectionModule = selectionModules[selectionIndex];

            if (selectionModule.PayloadKind != payloadKind || selectionModule.ModuleIndex != moduleIndex)
                continue;

            return selectionIndex;
        }

        return -1;
    }

    /// <summary>
    /// Resolves whether one dynamic-buffer selection entry is within the weighted rank cutoff for this kill.
    /// </summary>
    /// <param name="enemyEntity">Killed enemy entity used to seed deterministic selection.</param>
    /// <param name="killEventIndex">Killed-event index inside the current frame snapshot.</param>
    /// <param name="selectionModules">Selection entries to rank.</param>
    /// <param name="targetSelectionIndex">Selection entry being tested.</param>
    /// <param name="selectedModuleCount">Amount of modules selected for this kill.</param>
    /// <returns>True when the target entry is selected.</returns>
    private static bool IsSelectedByWeightedRank(Entity enemyEntity,
                                                 int killEventIndex,
                                                 DynamicBuffer<EnemyDropItemsModuleSelectionElement> selectionModules,
                                                 int targetSelectionIndex,
                                                 int selectedModuleCount)
    {
        float targetScore = ResolveSelectionScore(enemyEntity,
                                                  killEventIndex,
                                                  targetSelectionIndex,
                                                  selectionModules[targetSelectionIndex].SelectionWeight);
        int betterRankCount = 0;

        for (int selectionIndex = 0; selectionIndex < selectionModules.Length; selectionIndex++)
        {
            if (selectionIndex == targetSelectionIndex)
                continue;

            float candidateScore = ResolveSelectionScore(enemyEntity,
                                                         killEventIndex,
                                                         selectionIndex,
                                                         selectionModules[selectionIndex].SelectionWeight);

            if (candidateScore < targetScore || (candidateScore == targetScore && selectionIndex < targetSelectionIndex))
                betterRankCount++;
        }

        return betterRankCount < selectedModuleCount;
    }
    #endregion

    #region Shared Selection
    /// <summary>
    /// Resolves how many modules should be selected by the current combine mode.
    /// </summary>
    /// <param name="enemyEntity">Killed enemy entity used to seed deterministic selection.</param>
    /// <param name="killEventIndex">Killed-event index inside the current frame snapshot.</param>
    /// <param name="dropItemsConfig">Baked drop-items selection config.</param>
    /// <param name="selectionModuleCount">Amount of valid selectable module entries.</param>
    /// <returns>Number of modules selected for this kill.</returns>
    private static int ResolveSelectedModuleCount(Entity enemyEntity,
                                                  int killEventIndex,
                                                  in EnemyDropItemsConfig dropItemsConfig,
                                                  int selectionModuleCount)
    {
        int sanitizedSelectionModuleCount = math.max(0, selectionModuleCount);

        if (sanitizedSelectionModuleCount <= 0)
            return 0;

        if (dropItemsConfig.ModuleCombineMode == EnemyDropItemsModuleCombineMode.SingleWeightedModule)
            return 1;

        if (dropItemsConfig.ModuleCombineMode != EnemyDropItemsModuleCombineMode.WeightedSubset)
            return sanitizedSelectionModuleCount;

        int minimumSelectedModules = math.clamp(dropItemsConfig.MinimumSelectedModules,
                                                0,
                                                sanitizedSelectionModuleCount);
        int maximumSelectedModules = math.clamp(math.max(minimumSelectedModules,
                                                         dropItemsConfig.MaximumSelectedModules),
                                                0,
                                                sanitizedSelectionModuleCount);

        if (maximumSelectedModules <= minimumSelectedModules)
            return minimumSelectedModules;

        Unity.Mathematics.Random random = new Unity.Mathematics.Random(ResolveSelectionSeed(enemyEntity,
                                                                                            killEventIndex,
                                                                                            SelectionCountSeedSalt,
                                                                                            sanitizedSelectionModuleCount));
        return random.NextInt(minimumSelectedModules, maximumSelectedModules + 1);
    }

    /// <summary>
    /// Produces the weighted random rank score for one selection entry.
    /// </summary>
    /// <param name="enemyEntity">Killed enemy entity used to seed deterministic selection.</param>
    /// <param name="killEventIndex">Killed-event index inside the current frame snapshot.</param>
    /// <param name="selectionIndex">Selection entry index being scored.</param>
    /// <param name="selectionWeight">Relative authored selection weight.</param>
    /// <returns>Weighted rank score where lower values are selected first.</returns>
    private static float ResolveSelectionScore(Entity enemyEntity,
                                               int killEventIndex,
                                               int selectionIndex,
                                               float selectionWeight)
    {
        Unity.Mathematics.Random random = new Unity.Mathematics.Random(ResolveSelectionSeed(enemyEntity,
                                                                                            killEventIndex,
                                                                                            SelectionScoreSeedSalt,
                                                                                            selectionIndex));
        float randomValue = random.NextFloat(RandomMinimum, 1f);
        return -math.log(randomValue) / math.max(MinimumSelectionWeight, selectionWeight);
    }

    /// <summary>
    /// Builds a deterministic non-zero seed for module selection from kill identity and a caller-provided salt.
    /// </summary>
    /// <param name="enemyEntity">Killed enemy entity used to seed deterministic selection.</param>
    /// <param name="killEventIndex">Killed-event index inside the current frame snapshot.</param>
    /// <param name="salt">Salt separating independent random streams.</param>
    /// <param name="value">Additional stream-specific value.</param>
    /// <returns>Non-zero deterministic random seed.</returns>
    private static uint ResolveSelectionSeed(Entity enemyEntity, int killEventIndex, int salt, int value)
    {
        uint seed = math.hash(new int4(enemyEntity.Index,
                                       enemyEntity.Version,
                                       math.max(0, killEventIndex) + salt,
                                       math.max(0, value)));

        if (seed == 0u)
            return 1u;

        return seed;
    }
    #endregion

    #endregion
}
