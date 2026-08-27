using System;
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
[UpdateAfter(typeof(GameSceneFadePresentationSystem))]
[UpdateBefore(typeof(GameAudioPlaybackSystem))]
public partial class GameRoomPortalRewardPresentationSystem : SystemBase
{
    #region Fields
    private readonly List<GameRoomRewardPresentationItem> formattedItems =
        new List<GameRoomRewardPresentationItem>(16);
    private readonly List<PlayerScalableStatElement> effectiveScalableStats =
        new List<PlayerScalableStatElement>(64);
    private readonly Dictionary<string, PlayerFormulaValue> effectiveVariableContext =
        new Dictionary<string, PlayerFormulaValue>(StringComparer.OrdinalIgnoreCase);
    private EntityQuery managerQuery;
    private EntityQuery audioQuery;
    private EntityQuery playerQuery;
    private EntityQuery portalQuery;
    private uint lastAnchorRevision;
    private uint lastFormulaContextHash;
    private uint lastGenerationVersion;
    private int lastCurrentNodeIndex = -1;
    private byte lastRoomCleared;
    private bool hasPendingActivationEffects;
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
                                      typeof(GameRoomPortalActivationAnimationElement),
                                      typeof(GameRoomPortalPrefabReplacementElement),
                                      typeof(GameRoomPortalUnlockAudioRuntimeState),
                                      typeof(GameSceneTransitionState),
                                      typeof(GameSceneFadePresentationState),
                                      typeof(GameProceduralLevelRuntimeState),
                                      typeof(GameProceduralRoomNodeElement),
                                      typeof(GameProceduralRoomEdgeElement));
        playerQuery = GetEntityQuery(typeof(PlayerHealth),
                                     typeof(PlayerExperience),
                                     typeof(PlayerPowerUpsState),
                                     typeof(PlayerRuntimeScalingState),
                                     typeof(PlayerScalableStatElement));
        audioQuery = GetEntityQuery(typeof(GameAudioEventRequest));
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
        if (managerQuery.CalculateEntityCount() != 1)
            return;

        Entity managerEntity = managerQuery.GetSingletonEntity();
        GameProceduralLevelRuntimeState runtimeState =
            EntityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);
        GameSceneTransitionState transitionState =
            EntityManager.GetComponentData<GameSceneTransitionState>(managerEntity);
        GameSceneFadePresentationState fadeState =
            EntityManager.GetComponentData<GameSceneFadePresentationState>(managerEntity);

        if (ShouldPreserveOutgoingEffects(in runtimeState,
                                          in transitionState,
                                          in fadeState))
            return;

        if (playerQuery.CalculateEntityCount() != 1)
            return;

        Entity playerEntity = playerQuery.GetSingletonEntity();
        uint formulaContextHash =
            EntityManager.GetComponentData<PlayerRuntimeScalingState>(playerEntity).LastScalableStatsHash;
        GameRoomRewardConfig config =
            EntityManager.GetComponentData<GameRoomRewardConfig>(managerEntity);
        uint anchorRevision = GameRoomPortalRewardLogAnchor.Revision;
        bool graphChanged = lastGenerationVersion != runtimeState.GenerationVersion;
        bool roomChanged = lastCurrentNodeIndex != runtimeState.CurrentNodeIndex;
        bool clearStateChanged = lastRoomCleared != runtimeState.CurrentRoomCleared;
        bool formulaContextChanged = lastFormulaContextHash != formulaContextHash;
        bool requiresFullRefresh = graphChanged ||
                                   roomChanged ||
                                   clearStateChanged ||
                                   formulaContextChanged ||
                                   lastAnchorRevision != anchorRevision ||
                                   hasPendingActivationEffects;

        lastAnchorRevision = anchorRevision;
        lastFormulaContextHash = formulaContextHash;
        lastGenerationVersion = runtimeState.GenerationVersion;
        lastCurrentNodeIndex = runtimeState.CurrentNodeIndex;
        lastRoomCleared = runtimeState.CurrentRoomCleared;

        if (runtimeState.CurrentRoomCleared == 0)
        {
            hasPendingActivationEffects = false;

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
            DynamicBuffer<GameRoomPortalActivationAnimationElement> portalAnimations =
                EntityManager.GetBuffer<GameRoomPortalActivationAnimationElement>(managerEntity, true);
            DynamicBuffer<GameRoomPortalPrefabReplacementElement> portalReplacements =
                EntityManager.GetBuffer<GameRoomPortalPrefabReplacementElement>(managerEntity, true);
            DynamicBuffer<PlayerScalableStatElement> scalableStats =
                EntityManager.GetBuffer<PlayerScalableStatElement>(playerEntity, true);
            PlayerHealth health =
                EntityManager.GetComponentData<PlayerHealth>(playerEntity);
            PlayerExperience experience =
                EntityManager.GetComponentData<PlayerExperience>(playerEntity);
            PlayerPowerUpsState powerUpsState =
                EntityManager.GetComponentData<PlayerPowerUpsState>(playerEntity);
            PlayerRuntimeScalingFormulaContextUtility.Fill(EntityManager,
                                                            playerEntity,
                                                            effectiveScalableStats,
                                                            effectiveVariableContext);
            bool activationReadinessChanged = false;
            bool pendingActivationEffects = false;

            // Start or poll linked effects before exposing traversal and destination reward presentation.
            for (int portalIndex = 0; portalIndex < portalEntities.Length; portalIndex++)
            {
                Entity portalEntity = portalEntities[portalIndex];
                GameRoomPortal portal = EntityManager.GetComponentData<GameRoomPortal>(portalEntity);
                GameRoomPortalRuntimeState portalState =
                    EntityManager.GetComponentData<GameRoomPortalRuntimeState>(portalEntity);

                if (!GameRoomPortalRewardLogAnchor.TryResolve(
                        portal.PortalId,
                        portal.Center,
                        out GameRoomPortalRewardLogAnchor anchor))
                {
                    if (portalState.AssignedEdgeIndex !=
                            GameProceduralRoomTraversalConstants.UnassignedEdgeIndex &&
                        portalState.ActivationEffectsReady == 0)
                    {
                        pendingActivationEffects = true;
                    }

                    continue;
                }

                GameRoomPortalRewardLogView view = anchor.LogView;

                if (portalState.AssignedEdgeIndex ==
                    GameProceduralRoomTraversalConstants.UnassignedEdgeIndex)
                {
                    view.Hide();

                    if (anchor.EffectView != null)
                        anchor.EffectView.Deactivate();

                    continue;
                }

                int signature = BuildSignature(runtimeState.GenerationVersion,
                                               portalState.AssignedEdgeIndex,
                                               portal.PortalId.GetHashCode());

                if (portalState.ActivationEffectsReady == 0)
                {
                    view.Hide();
                    anchor.ActivateEffects(signature,
                                           portalAnimations,
                                           portalReplacements);

                    if (anchor.EffectView != null &&
                        !anchor.EffectView.IsActivationReady)
                    {
                        pendingActivationEffects = true;
                        continue;
                    }

                    portalState.ActivationEffectsReady = 1;
                    EntityManager.SetComponentData(portalEntity, portalState);
                    activationReadinessChanged = true;
                    continue;
                }

                if (portalState.TraversalEnabled == 0)
                {
                    view.Hide();
                    continue;
                }

                int presentationSignature = BuildPresentationSignature(signature,
                                                                       formulaContextHash);

                if (!view.NeedsRebuild(presentationSignature))
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
                                      effectiveVariableContext,
                                      in health,
                                      in experience,
                                      in powerUpsState,
                                      config.PortalValueDisplayMode);

                if (formattedItems.Count == 0)
                {
                    view.Hide();
                    continue;
                }

                float3 center = portal.Center;
                view.Rebuild(presentationSignature,
                             formattedItems,
                             new Vector3(center.x, center.y, center.z),
                             in config);
            }

            hasPendingActivationEffects = pendingActivationEffects;

            if (activationReadinessChanged)
            {
                GameProceduralRoomPortalBlockingUtility.SynchronizeTraversalAvailability(
                    EntityManager,
                    managerEntity);
            }

            DispatchPortalUnlockAudio(managerEntity,
                                      in config,
                                      in runtimeState,
                                      portalEntities);
        }
        finally
        {
            portalEntities.Dispose();
        }
    }
    #endregion

    #region Transition Coverage
    /// <summary>
    /// Keeps outgoing prefab replacements intact until the authored Canvas has rendered complete transition coverage.
    /// </summary>
    /// <param name="runtimeState">Procedural lifecycle state that changes before the Scene Manager consumes its request.</param>
    /// <param name="transitionState">Scene transition lifecycle and purpose.</param>
    /// <param name="fadeState">Fade state carrying the render acknowledgement.</param>
    /// <returns>True while presentation teardown would still be visible in the outgoing room.</returns>
    private static bool ShouldPreserveOutgoingEffects(
        in GameProceduralLevelRuntimeState runtimeState,
        in GameSceneTransitionState transitionState,
        in GameSceneFadePresentationState fadeState)
    {
        bool transitionPending;

        switch (runtimeState.Phase)
        {
            case GameProceduralLevelRuntimePhase.Traversing:
            case GameProceduralLevelRuntimePhase.Generating:
            case GameProceduralLevelRuntimePhase.LoadingInitialRoom:
                transitionPending = true;
                break;
            default:
                transitionPending = transitionState.IsTransitioning != 0;
                break;
        }

        if (!transitionPending || !GameSceneFadeCanvasView.HasActiveView)
            return false;

        return fadeState.OpaquePresented == 0;
    }
    #endregion

    #region Portal Unlock Audio
    /// <summary>
    /// Emits the shared portal-unlock event once for the room or once for each newly traversable exit.
    /// </summary>
    /// <param name="managerEntity">Room reward manager owning the ECS dispatch checkpoint.</param>
    /// <param name="config">Baked room reward presentation configuration.</param>
    /// <param name="runtimeState">Current procedural room lifecycle state.</param>
    /// <param name="portalEntities">Active-room portals evaluated during the current presentation refresh.</param>
    private void DispatchPortalUnlockAudio(Entity managerEntity,
                                           in GameRoomRewardConfig config,
                                           in GameProceduralLevelRuntimeState runtimeState,
                                           NativeList<Entity> portalEntities)
    {
        GameRoomPortalUnlockAudioRuntimeState audioState =
            EntityManager.GetComponentData<GameRoomPortalUnlockAudioRuntimeState>(managerEntity);
        bool roomChanged = audioState.GenerationVersion != runtimeState.GenerationVersion ||
                           audioState.NodeIndex != runtimeState.CurrentNodeIndex;

        // A new room or an uncleared room resets the checkpoint without emitting presentation requests.
        if (roomChanged || runtimeState.CurrentRoomCleared == 0)
        {
            audioState.GenerationVersion = runtimeState.GenerationVersion;
            audioState.NodeIndex = runtimeState.CurrentNodeIndex;
            audioState.Dispatched = 0;
            EntityManager.SetComponentData(managerEntity, audioState);
        }

        if (runtimeState.CurrentRoomCleared == 0 ||
            audioState.Dispatched != 0 ||
            config.PortalUnlockAudioEnabled == 0 ||
            audioQuery.CalculateEntityCount() != 1)
            return;

        DynamicBuffer<GameAudioEventRequest> audioRequests =
            EntityManager.GetBuffer<GameAudioEventRequest>(audioQuery.GetSingletonEntity());
        bool dispatched = false;

        // Resolve current ECS portal positions independently from managed animation or replacement bindings.
        for (int portalIndex = 0; portalIndex < portalEntities.Length; portalIndex++)
        {
            Entity portalEntity = portalEntities[portalIndex];
            GameRoomPortalRuntimeState portalState =
                EntityManager.GetComponentData<GameRoomPortalRuntimeState>(portalEntity);

            if (portalState.TraversalEnabled == 0 || portalState.AssignedEdgeIndex < 0)
                continue;

            GameRoomPortal portal = EntityManager.GetComponentData<GameRoomPortal>(portalEntity);

            GameAudioEventRequestUtility.EnqueuePositioned(
                audioRequests,
                GameAudioEventId.RoomRewardPortalUnlock,
                portal.Center);
            dispatched = true;

            if (config.PortalUnlockAudioPlaybackMode == GameRoomPortalUnlockAudioPlaybackMode.OncePerRoom)
                break;
        }

        if (!dispatched)
            return;

        audioState.Dispatched = 1;
        EntityManager.SetComponentData(managerEntity, audioState);
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
    /// <param name="scalableStats">Current authoritative player stats used by stat-target previews.</param>
    /// <param name="formulaContext">Effective scalable-stat context used by resource formula previews.</param>
    /// <param name="health">Current player health used by resource formulas.</param>
    /// <param name="experience">Current player experience used by resource formulas.</param>
    /// <param name="powerUpsState">Current player active power-up energy used by resource formulas.</param>
    /// <param name="displayMode">Detailed or sign-only portal value presentation.</param>
    private void BuildDestinationItems(
        int tileIndex,
        DynamicBuffer<GameRoomRewardTileBindingElement> tileBindings,
        DynamicBuffer<GameRoomRewardDefinitionElement> rewards,
        DynamicBuffer<GameRoomRewardModuleBindingElement> moduleBindings,
        DynamicBuffer<GameRoomRewardModuleElement> modules,
        DynamicBuffer<GameRoomRewardPresentationElement> mappings,
        DynamicBuffer<PlayerScalableStatElement> scalableStats,
        IReadOnlyDictionary<string, PlayerFormulaValue> formulaContext,
        in PlayerHealth health,
        in PlayerExperience experience,
        in PlayerPowerUpsState powerUpsState,
        GameRoomRewardValueDisplayMode displayMode)
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
                        formulaContext,
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
                                                                           hasFormulaResult,
                                                                           displayMode));
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

    /// <summary>
    /// Extends the stable portal signature with the current effective scalable-stat context.
    /// </summary>
    /// <param name="portalSignature">Stable graph, assignment and portal identity signature.</param>
    /// <param name="formulaContextHash">Current runtime scaling hash including temporary and combo inputs.</param>
    /// <returns>Combined signature used to invalidate formula-backed portal content.</returns>
    private static int BuildPresentationSignature(int portalSignature,
                                                  uint formulaContextHash)
    {
        unchecked
        {
            return portalSignature * 397 ^ (int)formulaContextHash;
        }
    }
    #endregion

    #endregion
}
