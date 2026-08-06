using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Scenes;
using UnityEngine;

/// <summary>
/// Rebuilds preauthored portal logs when graph edge assignments expose a rewarded destination room.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class GameRoomPortalRewardPresentationSystem : SystemBase
{
    #region Fields
    private readonly List<GameRoomRewardPresentationItem> formattedItems =
        new List<GameRoomRewardPresentationItem>(16);
    private EntityQuery managerQuery;
    private EntityQuery playerQuery;
    private EntityQuery portalQuery;
    private uint lastAnchorRevision;
    private uint lastGenerationVersion;
    private int lastCurrentNodeIndex = -1;
    private byte lastRoomCleared;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares graph, reward and managed portal view dependencies and enables change-filtered portal updates.
    /// </summary>
    protected override void OnCreate()
    {
        managerQuery = GetEntityQuery(typeof(GameRoomRewardConfig),
                                      typeof(GameRoomRewardModuleElement),
                                      typeof(GameRoomRewardDefinitionElement),
                                      typeof(GameRoomRewardModuleBindingElement),
                                      typeof(GameRoomRewardTileBindingElement),
                                      typeof(GameRoomRewardPresentationElement),
                                      typeof(GameProceduralLevelRuntimeState),
                                      typeof(GameProceduralRoomNodeElement),
                                      typeof(GameProceduralRoomEdgeElement));
        playerQuery = GetEntityQuery(typeof(PlayerHealth),
                                     typeof(PlayerExperience),
                                     typeof(PlayerPowerUpsState),
                                     typeof(PlayerScalableStatElement));
        portalQuery = GetEntityQuery(typeof(GameRoomPortal),
                                     typeof(GameRoomPortalRuntimeState),
                                     typeof(SceneTag));
        portalQuery.SetChangedVersionFilter(typeof(GameRoomPortalRuntimeState));
    }

    /// <summary>
    /// Rebuilds only portal chunks whose graph assignment changed since the previous system update.
    /// </summary>
    protected override void OnUpdate()
    {
        if (managerQuery.CalculateEntityCount() != 1 ||
            playerQuery.CalculateEntityCount() != 1)
            return;

        Entity managerEntity = managerQuery.GetSingletonEntity();
        Entity playerEntity = playerQuery.GetSingletonEntity();
        GameRoomRewardConfig config =
            EntityManager.GetComponentData<GameRoomRewardConfig>(managerEntity);
        GameProceduralLevelRuntimeState runtimeState =
            EntityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);
        uint anchorRevision = GameRoomPortalRewardLogAnchor.Revision;
        bool graphChanged = lastGenerationVersion != runtimeState.GenerationVersion;
        bool roomChanged = lastCurrentNodeIndex != runtimeState.CurrentNodeIndex;
        bool clearStateChanged = lastRoomCleared != runtimeState.CurrentRoomCleared;
        bool requiresFullRefresh = graphChanged ||
                                   roomChanged ||
                                   clearStateChanged ||
                                   lastAnchorRevision != anchorRevision;

        lastAnchorRevision = anchorRevision;
        lastGenerationVersion = runtimeState.GenerationVersion;
        lastCurrentNodeIndex = runtimeState.CurrentNodeIndex;
        lastRoomCleared = runtimeState.CurrentRoomCleared;

        if (runtimeState.CurrentRoomCleared == 0)
        {
            if (requiresFullRefresh)
                GameRoomPortalRewardLogAnchor.HideAll();

            return;
        }

        if (requiresFullRefresh)
            portalQuery.ResetFilter();

        NativeList<Entity> portalEntities = new NativeList<Entity>(Allocator.Temp);

        try
        {
            try
            {
                // Restrict presentation ownership to the exact active room while staged and retired instances remain resident.
                GameProceduralRoomInstanceQueryUtility.CollectActiveRoomEntities(portalQuery,
                                                                                  ref portalEntities);
            }
            finally
            {
                // The shared-component collector resets query filters after visiting the active instance sections.
                portalQuery.SetChangedVersionFilter(typeof(GameRoomPortalRuntimeState));
            }

            if (graphChanged || roomChanged)
                GameRoomPortalRewardLogAnchor.HideAll();

            if (portalEntities.Length == 0)
                return;

            DynamicBuffer<GameProceduralRoomNodeElement> nodes =
                EntityManager.GetBuffer<GameProceduralRoomNodeElement>(managerEntity, true);
            DynamicBuffer<GameProceduralRoomEdgeElement> edges =
                EntityManager.GetBuffer<GameProceduralRoomEdgeElement>(managerEntity, true);
            DynamicBuffer<GameRoomRewardTileBindingElement> tileBindings =
                EntityManager.GetBuffer<GameRoomRewardTileBindingElement>(managerEntity, true);
            DynamicBuffer<GameRoomRewardDefinitionElement> rewards =
                EntityManager.GetBuffer<GameRoomRewardDefinitionElement>(managerEntity, true);
            DynamicBuffer<GameRoomRewardModuleBindingElement> moduleBindings =
                EntityManager.GetBuffer<GameRoomRewardModuleBindingElement>(managerEntity, true);
            DynamicBuffer<GameRoomRewardModuleElement> modules =
                EntityManager.GetBuffer<GameRoomRewardModuleElement>(managerEntity, true);
            DynamicBuffer<GameRoomRewardPresentationElement> mappings =
                EntityManager.GetBuffer<GameRoomRewardPresentationElement>(managerEntity, true);
            DynamicBuffer<PlayerScalableStatElement> scalableStats =
                EntityManager.GetBuffer<PlayerScalableStatElement>(playerEntity, true);
            PlayerHealth health =
                EntityManager.GetComponentData<PlayerHealth>(playerEntity);
            PlayerExperience experience =
                EntityManager.GetComponentData<PlayerExperience>(playerEntity);
            PlayerPowerUpsState powerUpsState =
                EntityManager.GetComponentData<PlayerPowerUpsState>(playerEntity);

            // Each changed active portal resolves its assigned edge once and rebuilds only when its signature differs.
            for (int portalIndex = 0; portalIndex < portalEntities.Length; portalIndex++)
            {
                Entity portalEntity = portalEntities[portalIndex];
                GameRoomPortal portal = EntityManager.GetComponentData<GameRoomPortal>(portalEntity);
                GameRoomPortalRuntimeState portalState =
                    EntityManager.GetComponentData<GameRoomPortalRuntimeState>(portalEntity);

                if (!GameRoomPortalRewardLogAnchor.TryResolve(portal.PortalId,
                                                               portal.Center,
                                                               out GameRoomPortalRewardLogView view))
                {
                    continue;
                }

                if (view == null)
                    continue;

                if (portalState.AssignedEdgeIndex < 0 ||
                    portalState.TraversalEnabled == 0)
                {
                    view.Hide();
                    continue;
                }

                int signature = BuildSignature(runtimeState.GenerationVersion,
                                               portalState.AssignedEdgeIndex,
                                               portal.PortalId.GetHashCode());

                if (!view.NeedsRebuild(signature))
                    continue;

                if (!TryResolveDestinationTileIndex(portalState.AssignedEdgeIndex,
                                                    edges,
                                                    nodes,
                                                    out int tileIndex))
                {
                    view.Hide();
                    continue;
                }

                BuildDestinationItems(tileIndex,
                                      tileBindings,
                                      rewards,
                                      moduleBindings,
                                      modules,
                                      mappings,
                                      scalableStats,
                                      in health,
                                      in experience,
                                      in powerUpsState);

                if (formattedItems.Count == 0)
                {
                    view.Hide();
                    continue;
                }

                float3 center = portal.Center;
                view.Rebuild(signature,
                             formattedItems,
                             new Vector3(center.x, center.y, center.z),
                             in config);
            }
        }
        finally
        {
            portalEntities.Dispose();
        }
    }
    #endregion

    #region Destination Resolution
    /// <summary>
    /// Resolves the target graph node and flattened procedural tile for one assigned portal edge.
    /// </summary>
    /// <param name="assignedEdgeIndex">Edge identity assigned to the physical portal.</param>
    /// <param name="edges">Generated graph edges.</param>
    /// <param name="nodes">Generated graph nodes.</param>
    /// <param name="tileIndex">Resolved flattened procedural tile index.</param>
    /// <returns>True when the edge targets a valid generated room node.</returns>
    private static bool TryResolveDestinationTileIndex(
        int assignedEdgeIndex,
        DynamicBuffer<GameProceduralRoomEdgeElement> edges,
        DynamicBuffer<GameProceduralRoomNodeElement> nodes,
        out int tileIndex)
    {
        tileIndex = -1;

        if (assignedEdgeIndex < 0)
            return false;

        for (int edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
        {
            GameProceduralRoomEdgeElement edge = edges[edgeIndex];

            if (edge.EdgeIndex != assignedEdgeIndex)
                continue;

            if (edge.TargetNodeIndex < 0 || edge.TargetNodeIndex >= nodes.Length)
                return false;

            tileIndex = nodes[edge.TargetNodeIndex].TileIndex;
            return tileIndex >= 0;
        }

        return false;
    }

    /// <summary>
    /// Formats every ordered module assigned to one destination tile while collapsing multiplicative quantities.
    /// </summary>
    /// <param name="tileIndex">Flattened destination tile index.</param>
    /// <param name="tileBindings">All tile-to-reward assignments.</param>
    /// <param name="rewards">All flattened composed rewards.</param>
    /// <param name="moduleBindings">All reward-to-module bindings.</param>
    /// <param name="modules">All flattened atomic modules.</param>
    /// <param name="mappings">All shared presentation mappings.</param>
    /// <param name="scalableStats">Current authoritative player stats exposed to formula variables.</param>
    /// <param name="health">Current player health used by resource formulas.</param>
    /// <param name="experience">Current player experience used by resource formulas.</param>
    /// <param name="powerUpsState">Current player active power-up energy used by resource formulas.</param>
    private void BuildDestinationItems(
        int tileIndex,
        DynamicBuffer<GameRoomRewardTileBindingElement> tileBindings,
        DynamicBuffer<GameRoomRewardDefinitionElement> rewards,
        DynamicBuffer<GameRoomRewardModuleBindingElement> moduleBindings,
        DynamicBuffer<GameRoomRewardModuleElement> modules,
        DynamicBuffer<GameRoomRewardPresentationElement> mappings,
        DynamicBuffer<PlayerScalableStatElement> scalableStats,
        in PlayerHealth health,
        in PlayerExperience experience,
        in PlayerPowerUpsState powerUpsState)
    {
        formattedItems.Clear();
        IReadOnlyList<int> orderedTileBindings =
            GameRoomRewardRuntimeBufferUtility.BuildOrderedTileBindingIndices(tileBindings, tileIndex);

        for (int tileOrderIndex = 0; tileOrderIndex < orderedTileBindings.Count; tileOrderIndex++)
        {
            GameRoomRewardTileBindingElement tileBinding =
                tileBindings[orderedTileBindings[tileOrderIndex]];

            if (tileBinding.RewardIndex < 0 || tileBinding.RewardIndex >= rewards.Length)
                continue;

            GameRoomRewardDefinitionElement reward = rewards[tileBinding.RewardIndex];
            IReadOnlyList<int> orderedModuleBindings =
                GameRoomRewardRuntimeBufferUtility.BuildOrderedModuleBindingIndices(moduleBindings,
                                                                                    reward.ModuleBindingStartIndex,
                                                                                    reward.ModuleBindingCount);

            for (int moduleOrderIndex = 0;
                 moduleOrderIndex < orderedModuleBindings.Count;
                 moduleOrderIndex++)
            {
                GameRoomRewardModuleBindingElement moduleBinding =
                    moduleBindings[orderedModuleBindings[moduleOrderIndex]];

                if (moduleBinding.ModuleIndex < 0 || moduleBinding.ModuleIndex >= modules.Length)
                    continue;

                int combinedQuantity = math.max(1,
                                                tileBinding.Quantity *
                                                moduleBinding.Quantity);
                GameRoomRewardModuleElement module = modules[moduleBinding.ModuleIndex];
                bool hasFormulaResult =
                    PlayerRoomRewardValueUtility.TryEvaluateFormulaPreview(
                        in module,
                        scalableStats,
                        in health,
                        in experience,
                        in powerUpsState,
                        out PlayerFormulaValue formulaBaseValue,
                        out PlayerFormulaValue formulaResult);
                formattedItems.Add(
                    GameRoomRewardPresentationFormatter.FormatPortalModule(in module,
                                                                           combinedQuantity,
                                                                           mappings,
                                                                           in formulaBaseValue,
                                                                           in formulaResult,
                                                                           hasFormulaResult));
            }
        }
    }

    /// <summary>
    /// Builds a stable view-local signature from graph generation, assignment and portal identity.
    /// </summary>
    /// <param name="generationVersion">Current generated graph version.</param>
    /// <param name="assignedEdgeIndex">Portal edge assignment.</param>
    /// <param name="portalHash">Stable portal identifier hash for the current process.</param>
    /// <returns>Combined signature used to avoid redundant content rebuilds.</returns>
    private static int BuildSignature(uint generationVersion,
                                      int assignedEdgeIndex,
                                      int portalHash)
    {
        unchecked
        {
            int signature = (int)generationVersion;
            signature = signature * 397 ^ assignedEdgeIndex;
            signature = signature * 397 ^ portalHash;
            return signature;
        }
    }
    #endregion

    #endregion
}
