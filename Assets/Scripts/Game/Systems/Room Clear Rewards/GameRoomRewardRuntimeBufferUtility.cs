using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Centralizes stable reward-buffer ordering, expiration cleanup and grant idempotency checks.
/// </summary>
public static class GameRoomRewardRuntimeBufferUtility
{
    #region Fields
    private static readonly List<int> OrderedTileIndices = new List<int>(16);
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
