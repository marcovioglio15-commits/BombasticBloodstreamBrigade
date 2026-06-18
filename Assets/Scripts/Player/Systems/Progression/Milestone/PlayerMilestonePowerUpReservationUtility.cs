using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Collects temporary power-up reservations created by enemies that currently hold stolen player power-ups.
/// </summary>
internal static class PlayerMilestonePowerUpReservationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Collects power-ups currently held by Stealer enemies so milestone rolls can keep duplicate exclusions coherent.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read Stealer runtime buffers.</param>
    /// <param name="playerEntity">Player entity whose stolen power-ups should be reserved.</param>
    /// <param name="stealerRuntimeQuery">Query containing enemies with Stealer runtime buffers.</param>
    /// <param name="reservedUnlockCountsByPowerUpId">Output map of stolen power-up ids to their effective unlock counts.</param>
    /// <param name="reservedPassiveKinds">Output set of stolen passive kinds used for same-kind first-unlock exclusions.</param>
    public static void BuildStolenPowerUpRollReservations(EntityManager entityManager,
                                                          Entity playerEntity,
                                                          EntityQuery stealerRuntimeQuery,
                                                          out Dictionary<string, int> reservedUnlockCountsByPowerUpId,
                                                          out HashSet<PassiveToolKind> reservedPassiveKinds)
    {
        reservedUnlockCountsByPowerUpId = null;
        reservedPassiveKinds = null;

        if (playerEntity == Entity.Null)
            return;

        if (stealerRuntimeQuery.IsEmptyIgnoreFilter)
            return;

        NativeArray<Entity> stealerEntities = stealerRuntimeQuery.ToEntityArray(Allocator.Temp);

        try
        {
            // Scan only when a milestone is opening, so global Stealer state is not polled every frame.
            for (int stealerEntityIndex = 0; stealerEntityIndex < stealerEntities.Length; stealerEntityIndex++)
            {
                Entity stealerEntity = stealerEntities[stealerEntityIndex];

                if (!entityManager.HasBuffer<EnemyPowerUpStealerRuntimeElement>(stealerEntity))
                    continue;

                DynamicBuffer<EnemyPowerUpStealerRuntimeElement> stealerRuntime = entityManager.GetBuffer<EnemyPowerUpStealerRuntimeElement>(stealerEntity);
                AddStealerRuntimeReservations(stealerRuntime,
                                              playerEntity,
                                              ref reservedUnlockCountsByPowerUpId,
                                              ref reservedPassiveKinds);
            }
        }
        finally
        {
            if (stealerEntities.IsCreated)
                stealerEntities.Dispose();
        }
    }
    #endregion

    #region Reservation Helpers
    /// <summary>
    /// Adds reservations from one enemy Stealer runtime buffer to the milestone exclusion accumulators.
    /// </summary>
    /// <param name="stealerRuntime">Runtime buffer owned by one enemy.</param>
    /// <param name="playerEntity">Player entity whose stolen payloads should be counted.</param>
    /// <param name="reservedUnlockCountsByPowerUpId">Mutable map of stolen power-up ids to their effective unlock counts.</param>
    /// <param name="reservedPassiveKinds">Mutable set of stolen passive kinds.</param>
    private static void AddStealerRuntimeReservations(DynamicBuffer<EnemyPowerUpStealerRuntimeElement> stealerRuntime,
                                                      Entity playerEntity,
                                                      ref Dictionary<string, int> reservedUnlockCountsByPowerUpId,
                                                      ref HashSet<PassiveToolKind> reservedPassiveKinds)
    {
        for (int runtimeIndex = 0; runtimeIndex < stealerRuntime.Length; runtimeIndex++)
        {
            ref EnemyPowerUpStealerRuntimeElement runtime = ref stealerRuntime.ElementAt(runtimeIndex);

            if (runtime.HasStolenPowerUp == 0)
                continue;

            if (runtime.PlayerEntity != playerEntity)
                continue;

            AddReservedPassiveKind(in runtime, ref reservedPassiveKinds);

            if (runtime.PowerUpId.Length <= 0)
                continue;

            string powerUpId = runtime.PowerUpId.ToString();

            if (string.IsNullOrWhiteSpace(powerUpId))
                continue;

            if (reservedUnlockCountsByPowerUpId == null)
                reservedUnlockCountsByPowerUpId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int reservedUnlockCount = ResolveStolenReservationUnlockCount(in runtime);
            string trimmedPowerUpId = powerUpId.Trim();

            if (reservedUnlockCountsByPowerUpId.TryGetValue(trimmedPowerUpId, out int existingUnlockCount) &&
                existingUnlockCount >= reservedUnlockCount)
                continue;

            reservedUnlockCountsByPowerUpId[trimmedPowerUpId] = reservedUnlockCount;
        }
    }

    /// <summary>
    /// Adds the stolen passive kind to the reserved-kind set when the Stealer payload contains a passive config.
    /// </summary>
    /// <param name="runtime">Stealer runtime entry being inspected.</param>
    /// <param name="reservedPassiveKinds">Mutable set of stolen passive kinds.</param>
    private static void AddReservedPassiveKind(in EnemyPowerUpStealerRuntimeElement runtime,
                                               ref HashSet<PassiveToolKind> reservedPassiveKinds)
    {
        if (runtime.StolenKind != PlayerPowerUpUnlockKind.Passive)
            return;

        if (runtime.StoredPassiveTool.IsDefined == 0)
            return;

        if (reservedPassiveKinds == null)
            reservedPassiveKinds = new HashSet<PassiveToolKind>();

        reservedPassiveKinds.Add(runtime.StoredPassiveTool.ToolKind);
    }

    /// <summary>
    /// Resolves the unlock count that a stolen power-up should contribute to milestone duplicate exclusion.
    /// </summary>
    /// <param name="runtime">Stealer runtime entry holding the stolen payload.</param>
    /// <returns>Effective unlock count contributed by the stolen payload.</returns>
    private static int ResolveStolenReservationUnlockCount(in EnemyPowerUpStealerRuntimeElement runtime)
    {
        if (runtime.StolenKind == PlayerPowerUpUnlockKind.Passive)
            return mathMax(1, runtime.OriginalPassiveUnlockCount);

        return 1;
    }
    #endregion

    #region Math
    /// <summary>
    /// Returns the greater integer without taking an external math dependency for this small utility.
    /// </summary>
    /// <param name="left">First integer value.</param>
    /// <param name="right">Second integer value.</param>
    /// <returns>The greater integer value.</returns>
    private static int mathMax(int left, int right)
    {
        return left > right ? left : right;
    }
    #endregion

    #endregion
}
