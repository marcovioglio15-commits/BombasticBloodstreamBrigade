#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Places north/south-only room portals beside arrived trains and keeps authoritative SubScene volumes synchronized.
/// </summary>
internal static class GameRoomTrainPortalPlacementUtility
{
    #region Constants
    private const float BoardingSideClearance = 0.25f;
    private const float RailEndClearance = 0.5f;
    private const float PositionEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves whether a room lacking east/west exits must map south and north exits to west and east trains.
    /// </summary>
    /// <param name="anchors">Managed portal anchors currently authored in one train room.</param>
    /// <returns>True when no horizontal portal exists and at least one vertical portal needs train fallback.</returns>
    internal static bool RequiresVerticalFallback(
        IReadOnlyList<GameRoomPortalRewardLogAnchor> anchors)
    {
        bool hasVerticalPortal = false;

        for (int anchorIndex = 0; anchorIndex < anchors.Count; anchorIndex++)
        {
            string portalId = anchors[anchorIndex] != null
                ? anchors[anchorIndex].PortalId
                : string.Empty;

            if (GameRoomTrainArrivalProjectSetupUtility.ContainsDirection(portalId, "WEST") ||
                GameRoomTrainArrivalProjectSetupUtility.ContainsDirection(portalId, "EAST"))
            {
                return false;
            }

            hasVerticalPortal |= GameRoomTrainArrivalProjectSetupUtility.ContainsDirection(portalId, "SOUTH") ||
                                 GameRoomTrainArrivalProjectSetupUtility.ContainsDirection(portalId, "NORTH");
        }

        return hasVerticalPortal;
    }

    /// <summary>
    /// Moves fallback managed anchors and matching authoritative portal volumes to arrived train boarding positions.
    /// </summary>
    /// <param name="managedScene">Loaded managed room scene containing SubScene references.</param>
    /// <param name="anchors">Managed portal anchors to position.</param>
    /// <param name="westTrain">West train root at its off-map authored start.</param>
    /// <param name="eastTrain">East train root at its off-map authored start.</param>
    /// <returns>True when the managed scene required a serialized anchor change.</returns>
    internal static bool ConfigureVerticalFallback(
        Scene managedScene,
        IReadOnlyList<GameRoomPortalRewardLogAnchor> anchors,
        Transform westTrain,
        Transform eastTrain)
    {
        Dictionary<string, Vector3> targetPositions =
            BuildTargetPositions(anchors, westTrain, eastTrain);

        if (targetPositions.Count == 0)
            return false;

        MoveAuthoritativePortals(managedScene, targetPositions);
        bool changed = false;

        // Keep managed presentation centers identical to the authoritative volume centers changed above.
        for (int anchorIndex = 0; anchorIndex < anchors.Count; anchorIndex++)
        {
            GameRoomPortalRewardLogAnchor anchor = anchors[anchorIndex];

            if (anchor == null ||
                !targetPositions.TryGetValue(anchor.PortalId, out Vector3 targetPosition) ||
                Vector3.SqrMagnitude(anchor.transform.position - targetPosition) <= PositionEpsilon)
            {
                continue;
            }

            anchor.transform.position = targetPosition;
            EditorUtility.SetDirty(anchor.transform);

            if (PrefabUtility.IsPartOfPrefabInstance(anchor.transform))
                PrefabUtility.RecordPrefabInstancePropertyModifications(anchor.transform);

            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Maps one portal identity to a train side, including the vertical fallback used by N/S-only rooms.
    /// </summary>
    /// <param name="portalId">Stable portal identity containing its logical side token.</param>
    /// <param name="useVerticalFallback">True when south and north replace missing west and east exits.</param>
    /// <param name="usesWestTrain">True when this portal should own the west train.</param>
    /// <returns>True when the portal participates in train arrival presentation.</returns>
    internal static bool TryResolveTrainSide(string portalId,
                                             bool useVerticalFallback,
                                             out bool usesWestTrain)
    {
        if (GameRoomTrainArrivalProjectSetupUtility.ContainsDirection(portalId, "WEST") ||
            useVerticalFallback &&
            GameRoomTrainArrivalProjectSetupUtility.ContainsDirection(portalId, "SOUTH"))
        {
            usesWestTrain = true;
            return true;
        }

        if (GameRoomTrainArrivalProjectSetupUtility.ContainsDirection(portalId, "EAST") ||
            useVerticalFallback &&
            GameRoomTrainArrivalProjectSetupUtility.ContainsDirection(portalId, "NORTH"))
        {
            usesWestTrain = false;
            return true;
        }

        usesWestTrain = false;
        return false;
    }
    #endregion

    #region Target Resolution
    /// <summary>
    /// Builds exact boarding targets for every fallback portal from predicted arrived renderer bounds.
    /// </summary>
    /// <param name="anchors">Managed anchors supplying portal identities and established vertical centers.</param>
    /// <param name="westTrain">West train root.</param>
    /// <param name="eastTrain">East train root.</param>
    /// <returns>Portal identity to world-position mapping for vertical fallback portals.</returns>
    private static Dictionary<string, Vector3> BuildTargetPositions(
        IReadOnlyList<GameRoomPortalRewardLogAnchor> anchors,
        Transform westTrain,
        Transform eastTrain)
    {
        Dictionary<string, Vector3> targets =
            new Dictionary<string, Vector3>(StringComparer.Ordinal);
        bool hasWestBounds = TryResolveArrivedBounds(westTrain,
                                                     GameRoomTrainArrivalProjectSetupUtility.WestArrivalOffset,
                                                     out Bounds westBounds);
        bool hasEastBounds = TryResolveArrivedBounds(eastTrain,
                                                     GameRoomTrainArrivalProjectSetupUtility.EastArrivalOffset,
                                                     out Bounds eastBounds);

        for (int anchorIndex = 0; anchorIndex < anchors.Count; anchorIndex++)
        {
            GameRoomPortalRewardLogAnchor anchor = anchors[anchorIndex];

            if (anchor == null ||
                !TryResolveTrainSide(anchor.PortalId, true, out bool usesWestTrain))
            {
                continue;
            }

            Bounds trainBounds = usesWestTrain ? westBounds : eastBounds;

            if (usesWestTrain && !hasWestBounds || !usesWestTrain && !hasEastBounds)
                continue;

            float xPosition = usesWestTrain
                ? trainBounds.max.x + BoardingSideClearance
                : trainBounds.min.x - BoardingSideClearance;
            float minimumZ = trainBounds.min.z + RailEndClearance;
            float maximumZ = trainBounds.max.z - RailEndClearance;
            float zPosition = minimumZ <= maximumZ
                ? Mathf.Clamp(0f, minimumZ, maximumZ)
                : trainBounds.center.z;
            targets[anchor.PortalId] = new Vector3(xPosition,
                                                   anchor.transform.position.y,
                                                   zPosition);
        }

        return targets;
    }

    /// <summary>
    /// Predicts combined renderer bounds after one train reaches its configured local arrival offset.
    /// </summary>
    /// <param name="train">Train root whose renderer hierarchy defines its footprint.</param>
    /// <param name="localOffset">Local arrival displacement configured in the reward preset.</param>
    /// <param name="arrivedBounds">Combined predicted world-space bounds.</param>
    /// <returns>True when the train owns at least one renderer.</returns>
    private static bool TryResolveArrivedBounds(Transform train,
                                                Vector3 localOffset,
                                                out Bounds arrivedBounds)
    {
        if (train == null)
        {
            arrivedBounds = default;
            return false;
        }

        Renderer[] renderers = train.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            arrivedBounds = default;
            return false;
        }

        arrivedBounds = renderers[0].bounds;

        for (int rendererIndex = 1; rendererIndex < renderers.Length; rendererIndex++)
            arrivedBounds.Encapsulate(renderers[rendererIndex].bounds);

        arrivedBounds.center += train.parent != null
            ? train.parent.TransformVector(localOffset)
            : localOffset;
        return true;
    }
    #endregion

    #region Authoritative SubScenes
    /// <summary>
    /// Applies target centers to matching portal volumes in every SubScene referenced by one managed room.
    /// </summary>
    /// <param name="managedScene">Managed scene containing SubScene authoring components.</param>
    /// <param name="targetPositions">Exact portal centers indexed by stable identity.</param>
    private static void MoveAuthoritativePortals(Scene managedScene,
                                                 IReadOnlyDictionary<string, Vector3> targetPositions)
    {
        GameObject[] roots = managedScene.GetRootGameObjects();
        List<SubScene> subScenes = new List<SubScene>(2);

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            subScenes.Clear();
            roots[rootIndex].GetComponentsInChildren(true, subScenes);

            for (int subSceneIndex = 0; subSceneIndex < subScenes.Count; subSceneIndex++)
                MoveSubScenePortals(subScenes[subSceneIndex], targetPositions);
        }
    }

    /// <summary>
    /// Opens one referenced SubScene, moves matching portal roots by center delta, and saves only when changed.
    /// </summary>
    /// <param name="subScene">SubScene reference owned by the managed room.</param>
    /// <param name="targetPositions">Exact portal centers indexed by stable identity.</param>
    private static void MoveSubScenePortals(SubScene subScene,
                                            IReadOnlyDictionary<string, Vector3> targetPositions)
    {
        if (subScene == null || subScene.SceneAsset == null)
            return;

        string scenePath = AssetDatabase.GetAssetPath(subScene.SceneAsset);
        Scene sourceScene = SceneManager.GetSceneByPath(scenePath);
        bool wasLoaded = sourceScene.IsValid() && sourceScene.isLoaded;

        if (!wasLoaded)
            sourceScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        try
        {
            bool changed = false;
            GameObject[] roots = sourceScene.GetRootGameObjects();
            List<GameRoomPortalAuthoring> portals = new List<GameRoomPortalAuthoring>(8);

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                portals.Clear();
                roots[rootIndex].GetComponentsInChildren(true, portals);

                for (int portalIndex = 0; portalIndex < portals.Count; portalIndex++)
                {
                    GameRoomPortalAuthoring portal = portals[portalIndex];

                    if (portal == null ||
                        portal.PortalVolume == null ||
                        !targetPositions.TryGetValue(portal.PortalId, out Vector3 targetPosition))
                    {
                        continue;
                    }

                    Vector3 currentCenter = portal.PortalVolume.transform.TransformPoint(
                        portal.PortalVolume.center);

                    if (Vector3.SqrMagnitude(currentCenter - targetPosition) <= PositionEpsilon)
                        continue;

                    portal.transform.position += targetPosition - currentCenter;
                    EditorUtility.SetDirty(portal.transform);

                    if (PrefabUtility.IsPartOfPrefabInstance(portal.transform))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(portal.transform);

                    changed = true;
                }
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(sourceScene);
                EditorSceneManager.SaveScene(sourceScene);
            }
        }
        finally
        {
            if (!wasLoaded && sourceScene.IsValid() && sourceScene.isLoaded)
                EditorSceneManager.CloseScene(sourceScene, true);
        }
    }
    #endregion

    #endregion
}
#endif
