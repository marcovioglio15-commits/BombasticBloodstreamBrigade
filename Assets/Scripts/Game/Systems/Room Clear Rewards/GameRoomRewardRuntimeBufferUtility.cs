using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Centralizes stable reward-buffer ordering, expiration cleanup and grant idempotency checks.
/// </summary>
public static class GameRoomRewardRuntimeBufferUtility
{
    #region Fields
    private static readonly List<int> OrderedTileIndices = new List<int>(16);
    private static readonly List<int> ResolvedTileIndices = new List<int>(16);
    private static readonly List<FixedString64Bytes> ResolvedSelectionGroups = new List<FixedString64Bytes>(8);
    private static readonly List<int> OrderedModuleIndices = new List<int>(32);
    private static readonly List<int> OrderedResourceIndices = new List<int>(16);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds stable ordered indices for tile bindings targeting one flattened tile.
    /// </summary>
    /// <param name="bindings">All tile-to-reward bindings.</param>
    /// <param name="tileIndex">Cleared flattened tile index.</param>
    /// <returns>Reusable ordered index view valid until the next tile ordering request.</returns>
    public static IReadOnlyList<int> BuildOrderedTileBindingIndices(
        DynamicBuffer<GameRoomRewardTileBindingElement> bindings,
        int tileIndex)
    {
        OrderedTileIndices.Clear();

        for (int index = 0; index < bindings.Length; index++)
        {
            if (bindings[index].TileIndex == tileIndex)
                OrderedTileIndices.Add(index);
        }

        // Stable insertion sort avoids delegate allocations on the event-driven grant path.
        for (int index = 1; index < OrderedTileIndices.Count; index++)
        {
            int candidate = OrderedTileIndices[index];
            int insertionIndex = index - 1;

            while (insertionIndex >= 0 &&
                   IsTileBindingAfter(bindings, OrderedTileIndices[insertionIndex], candidate))
            {
                OrderedTileIndices[insertionIndex + 1] = OrderedTileIndices[insertionIndex];
                insertionIndex--;
            }

            OrderedTileIndices[insertionIndex + 1] = candidate;
        }

        return OrderedTileIndices;
    }

    /// <summary>
    /// Resolves deterministic weighted difficulty groups after stable tile binding ordering.
    /// </summary>
    /// <param name="bindings">All tile-to-reward bindings.</param>
    /// <param name="tileIndex">Cleared flattened tile index.</param>
    /// <param name="runSeed">Authoritative run seed contributing to deterministic selection.</param>
    /// <param name="clearVersion">Monotonic room-clear version contributing to deterministic selection.</param>
    /// <returns>Reusable ordered indices containing unconditional bindings and one candidate per resolved group.</returns>
    public static IReadOnlyList<int> BuildResolvedTileBindingIndices(
        DynamicBuffer<GameRoomRewardTileBindingElement> bindings,
        int tileIndex,
        uint runSeed,
        uint clearVersion)
    {
        IReadOnlyList<int> orderedIndices = BuildOrderedTileBindingIndices(bindings, tileIndex);
        ResolvedTileIndices.Clear();
        ResolvedSelectionGroups.Clear();

        for (int orderedIndex = 0; orderedIndex < orderedIndices.Count; orderedIndex++)
        {
            int bindingIndex = orderedIndices[orderedIndex];
            GameRoomRewardTileBindingElement binding = bindings[bindingIndex];

            if (binding.UseDifficultySelection == 0)
            {
                ResolvedTileIndices.Add(bindingIndex);
                continue;
            }

            if (ContainsGroup(binding.SelectionGroupId))
                continue;

            ResolvedSelectionGroups.Add(binding.SelectionGroupId);
            int selectedBindingIndex = ResolveDifficultyGroup(bindings,
                                                              orderedIndices,
                                                              binding.SelectionGroupId,
                                                              runSeed,
                                                              clearVersion,
                                                              tileIndex);

            if (selectedBindingIndex >= 0)
                ResolvedTileIndices.Add(selectedBindingIndex);
        }

        // Restore explicit order after grouped candidates are selected from potentially separated source rows.
        for (int index = 1; index < ResolvedTileIndices.Count; index++)
        {
            int candidate = ResolvedTileIndices[index];
            int insertionIndex = index - 1;

            while (insertionIndex >= 0 &&
                   IsTileBindingAfter(bindings, ResolvedTileIndices[insertionIndex], candidate))
            {
                ResolvedTileIndices[insertionIndex + 1] = ResolvedTileIndices[insertionIndex];
                insertionIndex--;
            }

            ResolvedTileIndices[insertionIndex + 1] = candidate;
        }

        return ResolvedTileIndices;
    }

    /// <summary>
    /// Selects one eligible weighted reward binding from a shared difficulty selection group.
    /// </summary>
    /// <param name="bindings">All flattened tile bindings.</param>
    /// <param name="orderedIndices">Stable indices targeting the cleared tile.</param>
    /// <param name="selectionGroupId">Group identifier being resolved.</param>
    /// <param name="runSeed">Authoritative run seed.</param>
    /// <param name="clearVersion">Monotonic clear version.</param>
    /// <param name="tileIndex">Cleared flattened tile index.</param>
    /// <returns>Selected binding index, or negative one when no candidate is eligible.</returns>
    private static int ResolveDifficultyGroup(DynamicBuffer<GameRoomRewardTileBindingElement> bindings,
                                              IReadOnlyList<int> orderedIndices,
                                              FixedString64Bytes selectionGroupId,
                                              uint runSeed,
                                              uint clearVersion,
                                              int tileIndex)
    {
        float totalWeight = 0f;

        for (int index = 0; index < orderedIndices.Count; index++)
        {
            GameRoomRewardTileBindingElement candidate = bindings[orderedIndices[index]];

            if (candidate.UseDifficultySelection == 0 ||
                !candidate.SelectionGroupId.Equals(selectionGroupId) ||
                !IsDifficultyEligible(in candidate))
            {
                continue;
            }

            totalWeight += math.max(0f, candidate.SelectionWeight);
        }

        if (totalWeight <= 0f)
            return -1;

        uint selectionHash = math.hash(new uint4(runSeed,
                                                 clearVersion,
                                                 unchecked((uint)tileIndex),
                                                 unchecked((uint)selectionGroupId.GetHashCode())));
        float selectionPoint = (selectionHash & 0x00FFFFFFu) / 16777216f * totalWeight;
        float cumulativeWeight = 0f;

        for (int index = 0; index < orderedIndices.Count; index++)
        {
            int candidateIndex = orderedIndices[index];
            GameRoomRewardTileBindingElement candidate = bindings[candidateIndex];

            if (candidate.UseDifficultySelection == 0 ||
                !candidate.SelectionGroupId.Equals(selectionGroupId) ||
                !IsDifficultyEligible(in candidate))
            {
                continue;
            }

            cumulativeWeight += math.max(0f, candidate.SelectionWeight);

            if (cumulativeWeight >= selectionPoint)
                return candidateIndex;
        }

        return -1;
    }

    /// <summary>
    /// Checks whether one reward candidate contains the current shared difficulty coefficient value.
    /// </summary>
    /// <param name="binding">Difficulty-aware reward binding being tested.</param>
    /// <returns>True when the coefficient exists and lies inside the inclusive authored range.</returns>
    private static bool IsDifficultyEligible(in GameRoomRewardTileBindingElement binding)
    {
        if (!GameDifficultyRuntimeValueStore.TryGetValue(binding.DifficultyCoefficientId.ToString(),
                                                         out float coefficientValue))
        {
            return false;
        }

        return coefficientValue >= binding.MinimumDifficulty && coefficientValue <= binding.MaximumDifficulty;
    }

    /// <summary>
    /// Checks whether a difficulty selection group has already been resolved in the current transaction.
    /// </summary>
    /// <param name="selectionGroupId">Group identifier to inspect.</param>
    /// <returns>True when the group exists in the reusable processed-group list.</returns>
    private static bool ContainsGroup(FixedString64Bytes selectionGroupId)
    {
        for (int index = 0; index < ResolvedSelectionGroups.Count; index++)
        {
            if (ResolvedSelectionGroups[index].Equals(selectionGroupId))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Builds stable ordered indices for one composed reward's contiguous module range.
    /// </summary>
    /// <param name="bindings">All flattened module bindings.</param>
    /// <param name="startIndex">Inclusive binding range start.</param>
    /// <param name="count">Binding range length.</param>
    /// <returns>Reusable ordered index view valid until the next module ordering request.</returns>
    public static IReadOnlyList<int> BuildOrderedModuleBindingIndices(
        DynamicBuffer<GameRoomRewardModuleBindingElement> bindings,
        int startIndex,
        int count)
    {
        OrderedModuleIndices.Clear();
        int endIndex = math.min(bindings.Length, startIndex + count);

        for (int index = math.max(0, startIndex); index < endIndex; index++)
            OrderedModuleIndices.Add(index);

        // Stable insertion sort preserves authored buffer order for equal order values.
        for (int index = 1; index < OrderedModuleIndices.Count; index++)
        {
            int candidate = OrderedModuleIndices[index];
            int insertionIndex = index - 1;

            while (insertionIndex >= 0 &&
                   IsModuleBindingAfter(bindings, OrderedModuleIndices[insertionIndex], candidate))
            {
                OrderedModuleIndices[insertionIndex + 1] = OrderedModuleIndices[insertionIndex];
                insertionIndex--;
            }

            OrderedModuleIndices[insertionIndex + 1] = candidate;
        }

        return OrderedModuleIndices;
    }

    /// <summary>
    /// Builds stable acquisition-order indices for active temporary resource stipends.
    /// </summary>
    /// <param name="resources">All pending and active resource schedules.</param>
    /// <param name="visitOrdinal">Current distinct room visit ordinal.</param>
    /// <returns>Reusable ordered index view valid until the next resource ordering request.</returns>
    public static IReadOnlyList<int> BuildOrderedTemporaryResourceIndices(
        DynamicBuffer<PlayerRoomRewardTemporaryResourceElement> resources,
        uint visitOrdinal)
    {
        OrderedResourceIndices.Clear();

        for (int index = 0; index < resources.Length; index++)
        {
            PlayerRoomRewardTemporaryResourceElement resource = resources[index];

            if (visitOrdinal >= resource.ActiveFromVisitOrdinal &&
                visitOrdinal < resource.ExpireAtVisitOrdinal)
            {
                OrderedResourceIndices.Add(index);
            }
        }

        // Stable insertion sort applies older acquisitions before newer acquisitions.
        for (int index = 1; index < OrderedResourceIndices.Count; index++)
        {
            int candidate = OrderedResourceIndices[index];
            int insertionIndex = index - 1;

            while (insertionIndex >= 0 &&
                   IsTemporaryResourceAfter(resources, OrderedResourceIndices[insertionIndex], candidate))
            {
                OrderedResourceIndices[insertionIndex + 1] = OrderedResourceIndices[insertionIndex];
                insertionIndex--;
            }

            OrderedResourceIndices[insertionIndex + 1] = candidate;
        }

        return OrderedResourceIndices;
    }

    /// <summary>
    /// Removes resource schedules whose exclusive expiration visit has been reached.
    /// </summary>
    /// <param name="resources">Mutable temporary resource schedule buffer.</param>
    /// <param name="visitOrdinal">Current distinct room visit ordinal.</param>
    public static void RemoveExpiredTemporaryResources(
        DynamicBuffer<PlayerRoomRewardTemporaryResourceElement> resources,
        uint visitOrdinal)
    {
        for (int index = resources.Length - 1; index >= 0; index--)
        {
            if (visitOrdinal >= resources[index].ExpireAtVisitOrdinal)
                resources.RemoveAt(index);
        }
    }

    /// <summary>
    /// Removes stat modifiers whose exclusive expiration visit has been reached.
    /// </summary>
    /// <param name="modifiers">Mutable temporary stat modifier buffer.</param>
    /// <param name="visitOrdinal">Current distinct room visit ordinal.</param>
    public static void RemoveExpiredTemporaryModifiers(
        DynamicBuffer<PlayerRoomRewardTemporaryModifierElement> modifiers,
        uint visitOrdinal)
    {
        for (int index = modifiers.Length - 1; index >= 0; index--)
        {
            if (visitOrdinal >= modifiers[index].ExpireAtVisitOrdinal)
                modifiers.RemoveAt(index);
        }
    }

    /// <summary>
    /// Checks the per-player idempotency checkpoint against one room-clear event.
    /// </summary>
    /// <param name="state">Last committed player grant state.</param>
    /// <param name="clearedEvent">Candidate room-clear event.</param>
    /// <returns>True when the candidate has already been granted.</returns>
    public static bool IsAlreadyGranted(in PlayerRoomRewardGrantState state,
                                        in GameProceduralRoomClearedEvent clearedEvent)
    {
        return state.LastRunSeed == clearedEvent.RunSeed &&
               state.LastGenerationVersion == clearedEvent.GenerationVersion &&
               state.LastClearVersion >= clearedEvent.ClearVersion &&
               state.LastNodeIndex == clearedEvent.NodeIndex;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Compares two tile bindings by explicit order and stable buffer index.
    /// </summary>
    /// <param name="bindings">Tile binding buffer.</param>
    /// <param name="leftIndex">Existing ordered index.</param>
    /// <param name="rightIndex">Candidate ordered index.</param>
    /// <returns>True when the left entry must move after the right entry.</returns>
    private static bool IsTileBindingAfter(DynamicBuffer<GameRoomRewardTileBindingElement> bindings,
                                           int leftIndex,
                                           int rightIndex)
    {
        int comparison = bindings[leftIndex].Order.CompareTo(bindings[rightIndex].Order);
        return comparison > 0 || comparison == 0 && leftIndex > rightIndex;
    }

    /// <summary>
    /// Compares two module bindings by explicit order and stable buffer index.
    /// </summary>
    /// <param name="bindings">Module binding buffer.</param>
    /// <param name="leftIndex">Existing ordered index.</param>
    /// <param name="rightIndex">Candidate ordered index.</param>
    /// <returns>True when the left entry must move after the right entry.</returns>
    private static bool IsModuleBindingAfter(DynamicBuffer<GameRoomRewardModuleBindingElement> bindings,
                                             int leftIndex,
                                             int rightIndex)
    {
        int comparison = bindings[leftIndex].Order.CompareTo(bindings[rightIndex].Order);
        return comparison > 0 || comparison == 0 && leftIndex > rightIndex;
    }

    /// <summary>
    /// Compares temporary stipends by acquisition sequence and stable buffer index.
    /// </summary>
    /// <param name="resources">Temporary resource schedule buffer.</param>
    /// <param name="leftIndex">Existing ordered index.</param>
    /// <param name="rightIndex">Candidate ordered index.</param>
    /// <returns>True when the left entry must move after the right entry.</returns>
    private static bool IsTemporaryResourceAfter(
        DynamicBuffer<PlayerRoomRewardTemporaryResourceElement> resources,
        int leftIndex,
        int rightIndex)
    {
        int comparison = resources[leftIndex].GrantSequence.CompareTo(resources[rightIndex].GrantSequence);
        return comparison > 0 || comparison == 0 && leftIndex > rightIndex;
    }
    #endregion

    #endregion
}
