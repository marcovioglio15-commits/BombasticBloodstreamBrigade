using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
/// <summary>
/// Owns exact managed and DOTS room instances for single-slot replacement and optional dual-slot preloading.
/// </summary>
internal static class GameProceduralRoomStreamingRuntimeUtility
{
    #region Constants
    private const double MinimumRetirementDelaySeconds = 0.5d;
    #endregion
    #region Fields
    private static readonly List<GameProceduralRoomStreamInstance> instances = new List<GameProceduralRoomStreamInstance>(8);
    private static GameProceduralRoomStreamInstance activeInstance;
    private static ulong currentGenerationKey;
    private static ulong prioritizedGenerationKey;
    private static int nextStagingSlotIndex;
    private static int prioritizedNodeIndex = -1;
    private static bool sceneCallbacksRegistered;
    #endregion
    #region Properties
    public static bool HasActiveInstance
    {
        get
        {
            return activeInstance != null && activeInstance.State == GameProceduralRoomStreamState.Active;
        }
    }
    #endregion
    #region Methods
    #region Loading
    /// <summary>
    /// Starts or reuses the exact managed room instance assigned to one generated node.
    /// </summary>
    /// <param name="generationKey">Stable run and level generation identity.</param>
    /// <param name="nodeIndex">Generated graph node index that owns the instance.</param>
    /// <param name="sceneDefinition">Reusable room scene definition referenced by the node.</param>
    /// <param name="loadBackend">Configured managed scene loading backend.</param>
    /// <param name="usesSpatialStaging">True only when a concurrently resident target must be isolated off-world.</param>
    /// <returns>True when an existing instance is usable or a new asynchronous load started successfully.</returns>
    public static bool EnsureNodeLoading(ulong generationKey,
                                         int nodeIndex,
                                         GameSceneDefinitionElement sceneDefinition,
                                         GameSceneLoadBackend loadBackend,
                                         bool usesSpatialStaging)
    {
        BeginGeneration(generationKey);

        if (TryGetInstance(generationKey, nodeIndex, out GameProceduralRoomStreamInstance existingInstance))
        {
            if (existingInstance.State == GameProceduralRoomStreamState.Retired)
            {
                existingInstance.RetiredAtUnscaledTime = 0d;
                existingInstance.State = GameProceduralRoomStreamState.Ready;
            }

            if (existingInstance.State != GameProceduralRoomStreamState.Unloading)
            {
                existingInstance.RetireWhenReady = false;
                return existingInstance.State != GameProceduralRoomStreamState.Failed &&
                       existingInstance.State != GameProceduralRoomStreamState.Released;
            }
        }

        EnsureSceneCallbacks();
        GameProceduralRoomStreamInstance instance = new GameProceduralRoomStreamInstance(generationKey,
                                                                                          nodeIndex,
                                                                                          AcquireStagingSlotIndex(),
                                                                                          sceneDefinition,
                                                                                          usesSpatialStaging);
        instances.Add(instance);

        return GameProceduralRoomManagedSceneUtility.StartLoad(instance, loadBackend);
    }

    /// <summary>
    /// Advances managed completion, explicit DOTS NewInstance streaming and the selected placement policy.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager that owns DOTS scene handles.</param>
    public static void TickLoading(EntityManager entityManager)
    {
        // Advance every independent node instance without allocating per-frame snapshots.
        for (int instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
        {
            GameProceduralRoomStreamInstance instance = instances[instanceIndex];

            switch (instance.State)
            {
                case GameProceduralRoomStreamState.LoadingManagedScene:
                    GameProceduralRoomManagedSceneUtility.TickLoad(instance);
                    break;

                case GameProceduralRoomStreamState.LoadingEntityScenes:
                    if (GameProceduralRoomEntitySceneUtility.TickLoad(entityManager, instance))
                        instance.State = GameProceduralRoomStreamState.Staging;

                    break;

                case GameProceduralRoomStreamState.Staging:
                    TickPlacement(entityManager, instance);
                    break;
            }

            if (instance.RetireWhenReady && instance.State == GameProceduralRoomStreamState.Ready)
            {
                instance.State = GameProceduralRoomStreamState.Retired;
                instance.RetiredAtUnscaledTime = Time.realtimeSinceStartupAsDouble;
            }
        }
    }

    /// <summary>
    /// Resolves whether one logical node has a fully streamed target instance ready for its ownership policy.
    /// </summary>
    /// <param name="generationKey">Stable run and level generation identity.</param>
    /// <param name="nodeIndex">Generated graph node index.</param>
    /// <returns>True when the exact instance can be committed without loading or structural changes.</returns>
    public static bool IsNodeReady(ulong generationKey, int nodeIndex)
    {
        if (!TryGetInstance(generationKey, nodeIndex, out GameProceduralRoomStreamInstance instance))
            return false;

        return instance.State == GameProceduralRoomStreamState.Ready ||
               instance.State == GameProceduralRoomStreamState.Active;
    }

    /// <summary>
    /// Resolves whether any lifecycle state already owns the requested logical node.
    /// </summary>
    /// <param name="generationKey">Stable run and level generation identity.</param>
    /// <param name="nodeIndex">Generated graph node index.</param>
    /// <returns>True when loading, active, staged or retirement state owns the node.</returns>
    public static bool ContainsNode(ulong generationKey, int nodeIndex)
    {
        return TryGetInstance(generationKey, nodeIndex, out GameProceduralRoomStreamInstance instance) &&
               instance.State != GameProceduralRoomStreamState.Retired &&
               instance.State != GameProceduralRoomStreamState.Unloading &&
               instance.State != GameProceduralRoomStreamState.Released &&
               instance.State != GameProceduralRoomStreamState.Failed;
    }

    /// <summary>
    /// Retires candidates that are no longer adjacent and restores still-resident candidates that became reachable again.
    /// </summary>
    /// <param name="generationKey">Stable run and level generation identity.</param>
    /// <param name="currentNodeIndex">Currently active generated graph node.</param>
    /// <param name="edges">Authoritative generated graph edges.</param>
    public static void ReconcileCandidateReachability(ulong generationKey,
                                                       int currentNodeIndex,
                                                       DynamicBuffer<GameProceduralRoomEdgeElement> edges)
    {
        for (int instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
        {
            GameProceduralRoomStreamInstance instance = instances[instanceIndex];

            if (instance.GenerationKey != generationKey ||
                ReferenceEquals(instance, activeInstance) ||
                instance.NodeIndex == currentNodeIndex)
            {
                continue;
            }

            if (IsOutgoingCandidate(edges, currentNodeIndex, instance.NodeIndex) &&
                !ShouldDeferCandidate(generationKey, instance.NodeIndex))
            {
                instance.RetireWhenReady = false;

                if (instance.State == GameProceduralRoomStreamState.Retired)
                {
                    instance.RetiredAtUnscaledTime = 0d;
                    instance.State = GameProceduralRoomStreamState.Ready;
                }

                continue;
            }

            instance.RetireWhenReady = true;

            if (instance.State == GameProceduralRoomStreamState.Ready)
            {
                instance.State = GameProceduralRoomStreamState.Retired;
                instance.RetiredAtUnscaledTime = Time.realtimeSinceStartupAsDouble;
            }
        }
    }

    /// <summary>
    /// Prioritizes an explicitly requested portal target and releases sibling preload capacity without cancelling Unity operations.
    /// </summary>
    /// <param name="generationKey">Stable run and level generation identity.</param>
    /// <param name="targetNodeIndex">Player-selected target node that must load next.</param>
    /// <param name="maximumStagedRooms">Authored inactive-room capacity used for the on-demand readiness gate.</param>
    /// <returns>True when the target already exists or capacity is available to start its load.</returns>
    public static bool PrioritizeCandidate(ulong generationKey,
                                           int targetNodeIndex,
                                           int maximumStagedRooms)
    {
        prioritizedGenerationKey = generationKey;
        prioritizedNodeIndex = targetNodeIndex;

        if (ContainsNode(generationKey, targetNodeIndex))
            return true;

        // Ready siblings free capacity immediately; in-flight siblings retire as soon as their uncancellable load completes.
        for (int instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
        {
            GameProceduralRoomStreamInstance instance = instances[instanceIndex];

            if (instance.GenerationKey != generationKey ||
                instance.NodeIndex == targetNodeIndex ||
                ReferenceEquals(instance, activeInstance))
            {
                continue;
            }

            instance.RetireWhenReady = true;

            if (instance.State == GameProceduralRoomStreamState.Ready)
            {
                instance.State = GameProceduralRoomStreamState.Retired;
                instance.RetiredAtUnscaledTime = Time.realtimeSinceStartupAsDouble;
            }
        }

        return CountStagedInstances() < math.max(1, maximumStagedRooms);
    }

    /// <summary>
    /// Checks whether opportunistic preload must yield to a player-selected target until its room transaction commits.
    /// </summary>
    /// <param name="generationKey">Stable run and level generation identity.</param>
    /// <param name="candidateNodeIndex">Opportunistic outgoing candidate being evaluated.</param>
    /// <returns>True when the candidate is not the currently prioritized target.</returns>
    public static bool ShouldDeferCandidate(ulong generationKey, int candidateNodeIndex)
    {
        return prioritizedGenerationKey == generationKey &&
               prioritizedNodeIndex >= 0 &&
               prioritizedNodeIndex != candidateNodeIndex;
    }

    /// <summary>
    /// Counts inactive room candidates that consume the configured staged-room budget.
    /// </summary>
    /// <returns>Number of loading, staging or ready target instances.</returns>
    public static int CountStagedInstances()
    {
        int count = 0;

        for (int instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
        {
            if (instances[instanceIndex].GenerationKey != currentGenerationKey)
                continue;

            switch (instances[instanceIndex].State)
            {
                case GameProceduralRoomStreamState.LoadingManagedScene:
                case GameProceduralRoomStreamState.LoadingEntityScenes:
                case GameProceduralRoomStreamState.Staging:
                case GameProceduralRoomStreamState.Ready:
                    count++;
                    break;
            }
        }

        return count;
    }

    /// <summary>
    /// Checks whether the current generation is already advancing one asynchronous candidate through managed, ECS or placement work.
    /// </summary>
    /// <returns>True while at least one staged candidate still has incomplete loading or placement work.</returns>
    public static bool HasInFlightStagingWork()
    {
        for (int instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
        {
            GameProceduralRoomStreamInstance instance = instances[instanceIndex];

            if (instance.GenerationKey != currentGenerationKey)
                continue;

            switch (instance.State)
            {
                case GameProceduralRoomStreamState.LoadingManagedScene:
                case GameProceduralRoomStreamState.LoadingEntityScenes:
                case GameProceduralRoomStreamState.Staging:
                    return true;
            }
        }

        return false;
    }
    #endregion
    #region Commit
    /// <summary>
    /// Atomically swaps the active and staged room slots using exact instance handles after an opaque frame was presented.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager owning room scene sections.</param>
    /// <param name="generationKey">Stable run and level generation identity.</param>
    /// <param name="targetNodeIndex">Logical target node whose instance becomes active.</param>
    /// <param name="targetPlacementOffset">Absolute world translation used to align the target room.</param>
    /// <param name="sourceOwnedByStreaming">True when the previous active room was moved to deferred retirement.</param>
    /// <returns>True when the target was ready and the slot swap completed.</returns>
    public static bool TryCommitNode(EntityManager entityManager,
                                     ulong generationKey,
                                     int targetNodeIndex,
                                     float3 targetPlacementOffset,
                                     out bool sourceOwnedByStreaming)
    {
        sourceOwnedByStreaming = activeInstance != null;

        if (!TryGetInstance(generationKey, targetNodeIndex, out GameProceduralRoomStreamInstance targetInstance) ||
            targetInstance.State != GameProceduralRoomStreamState.Ready)
        {
            return false;
        }

        // Retire the previous active instance without unloading any managed or ECS content on the critical path.
        if (activeInstance != null && !ReferenceEquals(activeInstance, targetInstance))
        {
            GameProceduralRoomPlacementUtility.ApplyPlacement(entityManager, activeInstance, false);
            activeInstance.State = GameProceduralRoomStreamState.Retired;
            activeInstance.RetiredAtUnscaledTime = Time.realtimeSinceStartupAsDouble;
        }

        // Promote the target at its resolved continuous-world offset and switch active-scene ownership in one tick.
        GameProceduralRoomPlacementUtility.ApplyActivePlacement(entityManager,
                                                                targetInstance,
                                                                targetPlacementOffset);
        targetInstance.State = GameProceduralRoomStreamState.Active;
        activeInstance = targetInstance;

        if (prioritizedGenerationKey == generationKey && prioritizedNodeIndex == targetNodeIndex)
        {
            prioritizedGenerationKey = 0ul;
            prioritizedNodeIndex = -1;
        }

        if (targetInstance.ManagedScene.IsValid() && targetInstance.ManagedScene.isLoaded)
            SceneManager.SetActiveScene(targetInstance.ManagedScene);

        return true;
    }

    /// <summary>
    /// Resolves one target portal arrival position from an exact staged or active logical room instance.
    /// </summary>
    /// <param name="entityManager">Entity manager owning exact room sections and portal data.</param>
    /// <param name="generationKey">Stable run and level generation identity.</param>
    /// <param name="nodeIndex">Logical room node whose portal is required.</param>
    /// <param name="portalId">Graph-selected target portal ID.</param>
    /// <param name="arrivalPosition">Resolved current-world arrival position, including prior instance placement.</param>
    /// <param name="activePlacementOffset">Instance translation already represented by the returned portal position.</param>
    /// <returns>True when exactly one portal matches inside the requested exact instance.</returns>
    public static bool TryResolveNodePortalArrival(EntityManager entityManager,
                                                   ulong generationKey,
                                                   int nodeIndex,
                                                   FixedString64Bytes portalId,
                                                   out float3 arrivalPosition,
                                                   out float3 activePlacementOffset)
    {
        arrivalPosition = float3.zero;
        activePlacementOffset = float3.zero;

        if (portalId.Length <= 0 ||
            !TryGetInstance(generationKey, nodeIndex, out GameProceduralRoomStreamInstance instance))
        {
            return false;
        }

        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameRoomPortal>(),
                                                            ComponentType.ReadOnly<SceneTag>());
        NativeList<Entity> portals = new NativeList<Entity>(Allocator.Temp);
        int matchCount = 0;
        activePlacementOffset = instance.ActivePlacementOffset;

        try
        {
            GameProceduralRoomInstanceQueryUtility.CollectRoomInstanceEntities(instance, query, ref portals);

            for (int portalIndex = 0; portalIndex < portals.Length; portalIndex++)
            {
                GameRoomPortal portal = entityManager.GetComponentData<GameRoomPortal>(portals[portalIndex]);

                if (!portal.PortalId.Equals(portalId))
                    continue;

                arrivalPosition = portal.ArrivalPosition;
                matchCount++;
            }

            return matchCount == 1;
        }
        finally
        {
            portals.Dispose();
            query.Dispose();
        }
    }

    /// <summary>
    /// Returns the exact active room instance used by instance-filtered arrival and portal queries.
    /// </summary>
    /// <param name="instance">Active room instance when transactional streaming owns one.</param>
    /// <returns>True when a valid active instance is available.</returns>
    public static bool TryGetActiveInstance(out GameProceduralRoomStreamInstance instance)
    {
        instance = activeInstance;
        return HasActiveInstance;
    }
    #endregion
    #region Retirement
    /// <summary>
    /// Starts and completes deferred exact-instance retirement outside all active scene transitions.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager owning DOTS scene handles.</param>
    /// <param name="retiredRoomBudget">Maximum previous instances retained resident.</param>
    /// <param name="workBudgetMilliseconds">Main-thread bookkeeping budget for this tick.</param>
    public static void TickRetirement(EntityManager entityManager,
                                      int retiredRoomBudget,
                                      float workBudgetMilliseconds)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + math.max(0.001f, workBudgetMilliseconds) * 0.001d;
        CompleteReleasedInstances(entityManager);

        if (CountRetiredInstances() <= math.max(0, retiredRoomBudget))
            return;

        // Start only the oldest eligible retirement so Unity unload operations cannot fan out in one frame.
        for (int instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
        {
            GameProceduralRoomStreamInstance instance = instances[instanceIndex];

            if (instance.State != GameProceduralRoomStreamState.Retired ||
                Time.realtimeSinceStartupAsDouble - instance.RetiredAtUnscaledTime < MinimumRetirementDelaySeconds)
            {
                continue;
            }

            StartUnload(entityManager, instance);
            break;
        }

        if (Time.realtimeSinceStartupAsDouble <= deadline)
            CompleteReleasedInstances(entityManager);
    }

    /// <summary>
    /// Intercepts a normal Scene Manager unload when its source is owned by the transactional room registry.
    /// </summary>
    /// <param name="sceneId">Canonical active room scene ID requested by the external transition.</param>
    /// <param name="completed">True when exact managed and DOTS instance unloading has completed.</param>
    /// <returns>True when transactional streaming owns the requested source scene.</returns>
    public static bool TryTickExternalUnload(FixedString64Bytes sceneId, out bool completed)
    {
        completed = false;

        if (activeInstance == null || !activeInstance.SceneDefinition.SceneId.Equals(sceneId))
            return false;

        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
            return true;

        if (activeInstance.State == GameProceduralRoomStreamState.Active)
        {
            GameProceduralRoomPlacementUtility.ApplyPlacement(world.EntityManager, activeInstance, false);
            activeInstance.State = GameProceduralRoomStreamState.Retired;
            StartUnload(world.EntityManager, activeInstance);
        }

        if (activeInstance.State == GameProceduralRoomStreamState.Retired)
            StartUnload(world.EntityManager, activeInstance);

        completed = IsUnloadComplete(world.EntityManager, activeInstance);

        if (!completed)
            return true;

        CompleteReleasedInstances(world.EntityManager);
        activeInstance = null;
        return true;
    }
    #endregion
    #region Render Kernel
    /// <summary>
    /// Identifies room scene events that must not trigger persistent camera discovery or URP stack reconstruction.
    /// </summary>
    /// <param name="scene">Managed scene received from a Unity scene callback.</param>
    /// <returns>True when the scene belongs to an active, staged, retired or in-flight procedural room instance.</returns>
    public static bool IsOwnedManagedScene(Scene scene)
    {
        int sceneHandle = scene.handle;
        string scenePath = scene.path;

        for (int instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
        {
            GameProceduralRoomStreamInstance instance = instances[instanceIndex];

            if (sceneHandle != 0 && instance.ManagedScene.handle == sceneHandle)
                return true;

            if (!string.IsNullOrEmpty(scenePath) &&
                string.Equals(instance.SceneDefinition.ScenePath.ToString(), scenePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
    #endregion
    #region Placement
    /// <summary>
    /// Applies optional dual-slot staging before declaring the exact instance portal-ready.
    /// </summary>
    /// <param name="entityManager">Entity manager owning room root transforms.</param>
    /// <param name="instance">Logical room instance awaiting placement.</param>
    private static void TickPlacement(EntityManager entityManager, GameProceduralRoomStreamInstance instance)
    {
        GameProceduralRoomPlacementUtility.CaptureAndStageEntityRoots(entityManager, instance);
        instance.State = GameProceduralRoomStreamState.Ready;
    }
    #endregion

    #region Unload
    /// <summary>
    /// Starts exact DOTS and managed unloading for one retired instance after it leaves the critical path.
    /// </summary>
    /// <param name="entityManager">Entity manager owning DOTS scene handles.</param>
    /// <param name="instance">Retired logical room instance.</param>
    private static void StartUnload(EntityManager entityManager, GameProceduralRoomStreamInstance instance)
    {
        if (instance.State == GameProceduralRoomStreamState.Unloading)
            return;

        GameProceduralRoomEntitySceneUtility.StartUnload(entityManager, instance);
        GameProceduralRoomManagedSceneUtility.StartUnload(instance);

        instance.State = GameProceduralRoomStreamState.Unloading;
    }

    /// <summary>
    /// Releases completed instance handles and removes fully retired entries from the registry.
    /// </summary>
    /// <param name="entityManager">Entity manager used to verify DOTS scene destruction.</param>
    private static void CompleteReleasedInstances(EntityManager entityManager)
    {
        for (int instanceIndex = instances.Count - 1; instanceIndex >= 0; instanceIndex--)
        {
            GameProceduralRoomStreamInstance instance = instances[instanceIndex];

            if (instance.State != GameProceduralRoomStreamState.Unloading ||
                !IsUnloadComplete(entityManager, instance))
            {
                continue;
            }

            GameProceduralRoomManagedSceneUtility.ReleaseUnload(instance);

            instance.State = GameProceduralRoomStreamState.Released;
            instances.RemoveAt(instanceIndex);
        }
    }

    /// <summary>
    /// Checks both exact DOTS scene entity destruction and the matching managed unload operation.
    /// </summary>
    /// <param name="entityManager">Entity manager owning DOTS scene handles.</param>
    /// <param name="instance">Unloading logical room instance.</param>
    /// <returns>True when no scene surface owned by the instance remains loaded.</returns>
    private static bool IsUnloadComplete(EntityManager entityManager, GameProceduralRoomStreamInstance instance)
    {
        return GameProceduralRoomEntitySceneUtility.IsUnloadComplete(entityManager, instance) &&
               GameProceduralRoomManagedSceneUtility.IsUnloadComplete(instance);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves an existing logical node instance without relying on scene IDs or reusable asset paths.
    /// </summary>
    /// <param name="generationKey">Stable run and level generation identity.</param>
    /// <param name="nodeIndex">Generated graph node index.</param>
    /// <param name="instance">Matching exact room instance.</param>
    /// <returns>True when the registry contains the logical node.</returns>
    private static bool TryGetInstance(ulong generationKey,
                                       int nodeIndex,
                                       out GameProceduralRoomStreamInstance instance)
    {
        for (int instanceIndex = instances.Count - 1; instanceIndex >= 0; instanceIndex--)
        {
            if (instances[instanceIndex].GenerationKey != generationKey ||
                instances[instanceIndex].NodeIndex != nodeIndex)
                continue;

            instance = instances[instanceIndex];
            return true;
        }

        instance = null;
        return false;
    }

    /// <summary>
    /// Invalidates staged candidates from a previous graph while retaining its active room until the next commit.
    /// </summary>
    /// <param name="generationKey">Stable identity derived from the generation version and level seed.</param>
    private static void BeginGeneration(ulong generationKey)
    {
        if (currentGenerationKey == generationKey)
            return;

        currentGenerationKey = generationKey;
        prioritizedGenerationKey = 0ul;
        prioritizedNodeIndex = -1;

        for (int instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
        {
            GameProceduralRoomStreamInstance instance = instances[instanceIndex];

            if (instance.GenerationKey == generationKey)
                continue;

            instance.RetireWhenReady = true;

            if (!ReferenceEquals(instance, activeInstance) &&
                instance.State == GameProceduralRoomStreamState.Ready)
            {
                instance.State = GameProceduralRoomStreamState.Retired;
                instance.RetiredAtUnscaledTime = Time.realtimeSinceStartupAsDouble;
            }
        }
    }

    /// <summary>
    /// Counts resident previous rooms without including already unloading or active instances.
    /// </summary>
    /// <returns>Number of fully resident retired room instances.</returns>
    private static int CountRetiredInstances()
    {
        int count = 0;

        for (int instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
        {
            if (instances[instanceIndex].State == GameProceduralRoomStreamState.Retired)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Checks whether one resident candidate is reachable through an outgoing edge of the active node.
    /// </summary>
    /// <param name="edges">Authoritative generated graph edges.</param>
    /// <param name="currentNodeIndex">Currently active generated graph node.</param>
    /// <param name="candidateNodeIndex">Candidate room node being evaluated.</param>
    /// <returns>True when the candidate remains directly reachable.</returns>
    private static bool IsOutgoingCandidate(DynamicBuffer<GameProceduralRoomEdgeElement> edges,
                                            int currentNodeIndex,
                                            int candidateNodeIndex)
    {
        for (int edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
        {
            if (edges[edgeIndex].SourceNodeIndex == currentNodeIndex &&
                edges[edgeIndex].TargetNodeIndex == candidateNodeIndex)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Allocates one process-local staging slot that remains unique across graph regenerations and disabled domain reloads.
    /// </summary>
    /// <returns>Unique non-negative staging slot index for one exact room instance.</returns>
    private static int AcquireStagingSlotIndex()
    {
        int stagingSlotIndex = nextStagingSlotIndex;
        nextStagingSlotIndex = nextStagingSlotIndex == int.MaxValue ? 0 : nextStagingSlotIndex + 1;
        return stagingSlotIndex;
    }

    /// <summary>
    /// Registers scene lifecycle callbacks once so camera bridges can identify in-flight room handles immediately.
    /// </summary>
    private static void EnsureSceneCallbacks()
    {
        if (sceneCallbacksRegistered)
            return;

        sceneCallbacksRegistered = true;
        Application.quitting += ClearRuntimeState;
    }

    /// <summary>
    /// Clears static managed references when the player exits, allowing disabled domain reload configurations to restart safely.
    /// </summary>
    private static void ClearRuntimeState()
    {
        instances.Clear();
        activeInstance = null;
        currentGenerationKey = 0ul;
        prioritizedGenerationKey = 0ul;
        nextStagingSlotIndex = 0;
        prioritizedNodeIndex = -1;
        sceneCallbacksRegistered = false;
    }
    #endregion

    #endregion
}
