using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Selects active managed VFX instances used by refresh and bounded cap-restart policies.
/// </summary>
internal static class PlayerPowerUpManagedVfxCapSelectionUtility
{
    #region Fields
    private static ulong activationSequence;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resets the activation order when the managed VFX runtime pool is destroyed.
    /// </summary>
    public static void ResetActivationSequence()
    {
        activationSequence = 0ul;
    }

    /// <summary>
    /// Allocates a monotonic sequence used to select the oldest restart-on-cap instance.
    /// </summary>
    /// <returns>Non-zero activation sequence for the instance being configured.</returns>
    public static ulong ResolveNextActivationSequence()
    {
        activationSequence++;

        if (activationSequence == 0ul)
            activationSequence = 1ul;

        return activationSequence;
    }

    /// <summary>
    /// Finds an active instance matching one continuous-effect refresh request.
    /// </summary>
    /// <param name="activeInstances">Current active managed VFX instances.</param>
    /// <param name="request">Request carrying the refresh key and target identity.</param>
    /// <returns>Matching active instance, or null when no continuous effect can be refreshed.</returns>
    public static PlayerPowerUpManagedVfxInstance FindKeyedInstance(IReadOnlyList<PlayerPowerUpManagedVfxInstance> activeInstances,
                                                                    in PlayerPowerUpVfxSpawnRequest request)
    {
        for (int instanceIndex = 0; instanceIndex < activeInstances.Count; instanceIndex++)
        {
            PlayerPowerUpManagedVfxInstance instance = activeInstances[instanceIndex];

            if (!PlayerPowerUpManagedVfxInstanceUtility.IsInstanceUsable(instance))
                continue;

            if (instance.RefreshKey != request.RefreshKey)
                continue;

            if (!MatchesRequestedPrefab(instance, in request))
                continue;

            if (instance.FollowTargetEntity != request.FollowTargetEntity)
                continue;

            return instance;
        }

        return null;
    }

    /// <summary>
    /// Counts active attached instances matching the same prefab and enemy spawn-version key.
    /// </summary>
    /// <param name="activeInstances">Current active managed VFX instances.</param>
    /// <param name="request">VFX request being evaluated.</param>
    /// <param name="existingInstance">First matching active instance found during the scan.</param>
    /// <returns>Number of active matching attached instances.</returns>
    public static int CountAttachedInstances(IReadOnlyList<PlayerPowerUpManagedVfxInstance> activeInstances,
                                             in PlayerPowerUpVfxSpawnRequest request,
                                             out PlayerPowerUpManagedVfxInstance existingInstance)
    {
        existingInstance = null;

        if (request.FollowValidationEntity == Entity.Null || request.FollowValidationSpawnVersion == 0u)
            return 0;

        int count = 0;

        for (int instanceIndex = 0; instanceIndex < activeInstances.Count; instanceIndex++)
        {
            PlayerPowerUpManagedVfxInstance instance = activeInstances[instanceIndex];

            if (!PlayerPowerUpManagedVfxInstanceUtility.IsInstanceUsable(instance))
                continue;

            if (!instance.HasFollowTarget)
                continue;

            if (!MatchesRequestedPrefab(instance, in request))
                continue;

            if (instance.FollowValidationEntity != request.FollowValidationEntity)
                continue;

            if (instance.FollowValidationSpawnVersion != request.FollowValidationSpawnVersion)
                continue;

            existingInstance = existingInstance == null ? instance : existingInstance;
            count++;
        }

        return count;
    }

    /// <summary>
    /// Counts matching one-shot instances in one area-cap cell and selects its oldest restartable entry.
    /// </summary>
    /// <param name="activeInstances">Current active managed VFX instances.</param>
    /// <param name="prefabEntity">VFX prefab entity used as the cap key.</param>
    /// <param name="sourcePrefab">Direct source prefab used when no baked prefab entity is available.</param>
    /// <param name="position">Requested spawn position.</param>
    /// <param name="cellSize">Sanitized cap cell size.</param>
    /// <param name="oldestRestartableInstance">Oldest matching opt-in instance that can be restarted under the cap.</param>
    /// <returns>Number of active matching one-shot instances in the same cell.</returns>
    public static int CountAreaInstances(IReadOnlyList<PlayerPowerUpManagedVfxInstance> activeInstances,
                                         Entity prefabEntity,
                                         GameObject sourcePrefab,
                                         float3 position,
                                         float cellSize,
                                         out PlayerPowerUpManagedVfxInstance oldestRestartableInstance)
    {
        int requestedCellX = (int)math.floor(position.x / cellSize);
        int requestedCellY = (int)math.floor(position.z / cellSize);
        int count = 0;
        oldestRestartableInstance = null;

        for (int instanceIndex = 0; instanceIndex < activeInstances.Count; instanceIndex++)
        {
            PlayerPowerUpManagedVfxInstance instance = activeInstances[instanceIndex];

            if (!PlayerPowerUpManagedVfxInstanceUtility.IsInstanceUsable(instance))
                continue;

            if (instance.HasFollowTarget)
                continue;

            if (!MatchesRequestedPrefab(instance, prefabEntity, sourcePrefab))
                continue;

            int instanceCellX = (int)math.floor(instance.Position.x / cellSize);
            int instanceCellY = (int)math.floor(instance.Position.z / cellSize);

            if (instanceCellX != requestedCellX || instanceCellY != requestedCellY)
                continue;

            if (instance.RestartOldestOnCap &&
                (oldestRestartableInstance == null ||
                 instance.ActivationSequence < oldestRestartableInstance.ActivationSequence))
            {
                oldestRestartableInstance = instance;
            }

            count++;
        }

        return count;
    }

    /// <summary>
    /// Finds the oldest compatible opt-in one-shot for global-cap replacement.
    /// </summary>
    /// <param name="activeInstances">Current active managed VFX instances.</param>
    /// <param name="request">Incoming request that opted into restart-on-cap behavior.</param>
    /// <returns>Oldest compatible active instance, or null when none can be restarted.</returns>
    public static PlayerPowerUpManagedVfxInstance FindOldestMatchingRestartableInstance(IReadOnlyList<PlayerPowerUpManagedVfxInstance> activeInstances,
                                                                                        in PlayerPowerUpVfxSpawnRequest request)
    {
        return FindOldestRestartableInstance(activeInstances, true, in request);
    }

    /// <summary>
    /// Finds the oldest opt-in one-shot regardless of prefab identity for bounded global-cap replacement.
    /// </summary>
    /// <param name="activeInstances">Current active managed VFX instances.</param>
    /// <returns>Oldest restartable active instance, or null when no opt-in one-shot exists.</returns>
    public static PlayerPowerUpManagedVfxInstance FindOldestRestartableInstance(IReadOnlyList<PlayerPowerUpManagedVfxInstance> activeInstances)
    {
        PlayerPowerUpVfxSpawnRequest request = default;
        return FindOldestRestartableInstance(activeInstances, false, in request);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Finds the oldest restartable one-shot, optionally requiring prefab identity compatibility.
    /// </summary>
    /// <param name="activeInstances">Current active managed VFX instances.</param>
    /// <param name="requireMatchingPrefab">True when the selected instance must use the requested prefab.</param>
    /// <param name="request">Incoming request used when prefab compatibility is required.</param>
    /// <returns>Oldest qualifying active instance, or null when none exists.</returns>
    private static PlayerPowerUpManagedVfxInstance FindOldestRestartableInstance(IReadOnlyList<PlayerPowerUpManagedVfxInstance> activeInstances,
                                                                                 bool requireMatchingPrefab,
                                                                                 in PlayerPowerUpVfxSpawnRequest request)
    {
        PlayerPowerUpManagedVfxInstance oldestRestartableInstance = null;

        for (int instanceIndex = 0; instanceIndex < activeInstances.Count; instanceIndex++)
        {
            PlayerPowerUpManagedVfxInstance instance = activeInstances[instanceIndex];

            if (!PlayerPowerUpManagedVfxInstanceUtility.IsInstanceUsable(instance))
                continue;

            if (instance.HasFollowTarget || !instance.RestartOldestOnCap)
                continue;

            if (requireMatchingPrefab && !MatchesRequestedPrefab(instance, in request))
                continue;

            if (oldestRestartableInstance != null &&
                instance.ActivationSequence >= oldestRestartableInstance.ActivationSequence)
            {
                continue;
            }

            oldestRestartableInstance = instance;
        }

        return oldestRestartableInstance;
    }
    /// <summary>
    /// Checks whether a managed instance matches the prefab identity carried by a request.
    /// </summary>
    /// <param name="instance">Active managed VFX instance to inspect.</param>
    /// <param name="request">Spawn request carrying prefab identity.</param>
    /// <returns>True when the instance and request reference the same prefab.</returns>
    private static bool MatchesRequestedPrefab(PlayerPowerUpManagedVfxInstance instance,
                                               in PlayerPowerUpVfxSpawnRequest request)
    {
        return MatchesRequestedPrefab(instance, request.PrefabEntity, request.SourcePrefab.Value);
    }

    /// <summary>
    /// Checks whether a managed instance matches a prefab entity or direct source prefab identity.
    /// </summary>
    /// <param name="instance">Active managed VFX instance to inspect.</param>
    /// <param name="prefabEntity">Optional baked prefab entity.</param>
    /// <param name="sourcePrefab">Optional direct source prefab reference.</param>
    /// <returns>True when the instance and source identify the same prefab.</returns>
    private static bool MatchesRequestedPrefab(PlayerPowerUpManagedVfxInstance instance,
                                               Entity prefabEntity,
                                               GameObject sourcePrefab)
    {
        if (prefabEntity != Entity.Null)
            return instance.PrefabEntity == prefabEntity;

        return sourcePrefab != null && instance.SourcePrefab == sourcePrefab;
    }
    #endregion

    #endregion
}
