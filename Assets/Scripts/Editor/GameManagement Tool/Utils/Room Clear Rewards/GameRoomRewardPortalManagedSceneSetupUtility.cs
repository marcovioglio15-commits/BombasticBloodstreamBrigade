#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Installs fixed portal reward logs in managed room scenes while keeping SubScene artifacts unmanaged.
/// </summary>
internal static class GameRoomRewardPortalManagedSceneSetupUtility
{
    #region Constants
    private const string AnchorNamePrefix = "Portal Reward - ";
    #endregion

    #region Methods

    #region Entry Point
    /// <summary>
    /// Rebuilds managed portal presentation anchors for every room referenced by a procedural level preset.
    /// </summary>
    /// <param name="portalAnchorPrefab">Shared anchor and fixed-capacity log prefab installed for each portal.</param>
    public static void Configure(GameObject portalAnchorPrefab)
    {
        if (portalAnchorPrefab == null)
            throw new InvalidOperationException("The shared portal anchor prefab is missing.");

        HashSet<string> roomScenePaths = CollectProceduralRoomScenePaths();
        Scene previouslyActiveScene = SceneManager.GetActiveScene();

        // Rebuild each referenced room independently so duplicate preset references never duplicate anchors.
        foreach (string roomScenePath in roomScenePaths)
            ConfigureRoomScene(roomScenePath, portalAnchorPrefab);

        if (previouslyActiveScene.IsValid() && previouslyActiveScene.isLoaded)
            SceneManager.SetActiveScene(previouslyActiveScene);
    }
    #endregion

    #region Anchor Alignment
    /// <summary>
    /// Resolves one unique SubScene portal volume center for an anchor edited in a loaded managed room scene.
    /// </summary>
    /// <param name="roomScene">Loaded managed room scene containing the anchor and its SubScene references.</param>
    /// <param name="portalId">Exact stable portal identifier already assigned to the managed anchor.</param>
    /// <param name="worldCenter">Resolved authoritative portal volume center when lookup succeeds.</param>
    /// <param name="failure">Actionable explanation when the identifier cannot resolve exactly one portal.</param>
    /// <returns>True when exactly one valid SubScene portal matches the requested identifier.</returns>
    internal static bool TryResolvePortalWorldCenter(Scene roomScene,
                                                     string portalId,
                                                     out Vector3 worldCenter,
                                                     out string failure)
    {
        worldCenter = Vector3.zero;
        failure = string.Empty;

        if (!roomScene.IsValid() || !roomScene.isLoaded)
        {
            failure = "The managed room scene is not loaded.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(portalId))
        {
            failure = "Assign a non-empty Portal ID before aligning the anchor.";
            return false;
        }

        List<PortalPresentationSource> sources = CollectPortalSources(roomScene);
        bool found = false;

        // Require one exact identity so duplicate portal authoring can never move an anchor ambiguously.
        for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
        {
            PortalPresentationSource source = sources[sourceIndex];

            if (!string.Equals(source.PortalId, portalId, StringComparison.Ordinal))
                continue;

            if (found)
            {
                worldCenter = Vector3.zero;
                failure = "Portal ID '" + portalId +
                          "' is duplicated across the referenced SubScenes. Keep the ID unique before aligning.";
                return false;
            }

            found = true;
            worldCenter = source.WorldCenter;
        }

        if (found)
            return true;

        failure = "Portal ID '" + portalId +
                  "' does not exist on a valid Portal Volume in the referenced SubScenes.";
        return false;
    }
    #endregion

    #region Scene Discovery
    /// <summary>
    /// Collects unique managed room scene paths from every authored procedural level preset.
    /// </summary>
    /// <returns>Unique project-relative room scene paths.</returns>
    private static HashSet<string> CollectProceduralRoomScenePaths()
    {
        HashSet<string> scenePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] presetGuids = AssetDatabase.FindAssets("t:GameProceduralLevelPreset");

        for (int presetIndex = 0; presetIndex < presetGuids.Length; presetIndex++)
        {
            string presetPath = AssetDatabase.GUIDToAssetPath(presetGuids[presetIndex]);
            GameProceduralLevelPreset preset =
                AssetDatabase.LoadAssetAtPath<GameProceduralLevelPreset>(presetPath);

            if (preset == null)
                continue;

            IReadOnlyList<GameProceduralLevelDefinition> levels = preset.Levels;

            for (int levelIndex = 0; levelIndex < levels.Count; levelIndex++)
            {
                GameProceduralLevelDefinition level = levels[levelIndex];

                if (level == null)
                    continue;

                IReadOnlyList<GameProceduralRoomTileDefinition> tiles = level.RoomTiles;

                for (int tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
                {
                    GameProceduralRoomTileDefinition tile = tiles[tileIndex];

                    if (tile == null || string.IsNullOrWhiteSpace(tile.SceneGuid))
                        continue;

                    string scenePath = AssetDatabase.GUIDToAssetPath(tile.SceneGuid);

                    if (!string.IsNullOrWhiteSpace(scenePath))
                        scenePaths.Add(scenePath);
                }
            }
        }

        return scenePaths;
    }
    #endregion

    #region Managed Scene Configuration
    /// <summary>
    /// Replaces setup-owned presentation anchors in one managed room scene from its referenced SubScene portals.
    /// </summary>
    /// <param name="roomScenePath">Project-relative managed room scene path.</param>
    /// <param name="portalAnchorPrefab">Shared anchor and log prefab installed for each portal.</param>
    private static void ConfigureRoomScene(string roomScenePath,
                                           GameObject portalAnchorPrefab)
    {
        Scene roomScene = SceneManager.GetSceneByPath(roomScenePath);
        bool wasLoaded = roomScene.IsValid() && roomScene.isLoaded;

        if (!wasLoaded)
            roomScene = EditorSceneManager.OpenScene(roomScenePath, OpenSceneMode.Additive);

        try
        {
            List<PortalPresentationSource> sources = CollectPortalSources(roomScene);
            SynchronizePresentationAnchors(roomScene,
                                           sources,
                                           portalAnchorPrefab);

            EditorSceneManager.SaveScene(roomScene);
        }
        finally
        {
            if (!wasLoaded && roomScene.IsValid() && roomScene.isLoaded)
                EditorSceneManager.CloseScene(roomScene, true);
        }
    }

    /// <summary>
    /// Synchronizes setup-owned anchors while preserving linked objects and freely authored log placement.
    /// </summary>
    /// <param name="roomScene">Managed room scene receiving synchronized anchors.</param>
    /// <param name="sources">Current authoritative portal identities and centers.</param>
    /// <param name="portalAnchorPrefab">Shared anchor prefab used only for missing portals.</param>
    private static void SynchronizePresentationAnchors(
        Scene roomScene,
        IReadOnlyList<PortalPresentationSource> sources,
        GameObject portalAnchorPrefab)
    {
        GameObject[] roots = roomScene.GetRootGameObjects();
        Dictionary<string, GameRoomPortalRewardLogAnchor> anchorsByPortalId =
            new Dictionary<string, GameRoomPortalRewardLogAnchor>(StringComparer.Ordinal);
        HashSet<string> sourceIds = new HashSet<string>(StringComparer.Ordinal);

        for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
            sourceIds.Add(sources[sourceIndex].PortalId);

        // Remove obsolete containers and retain at most one valid anchor for each current portal identity.
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            GameObject root = roots[rootIndex];
            GameRoomPortalRewardLogAnchor anchor =
                root.GetComponent<GameRoomPortalRewardLogAnchor>();

            if (string.Equals(root.name,
                              "Room Reward Portal Presentations",
                              StringComparison.Ordinal))
            {
                UnityEngine.Object.DestroyImmediate(root);
                continue;
            }

            if (anchor == null)
                continue;

            if (string.IsNullOrWhiteSpace(anchor.PortalId) ||
                !sourceIds.Contains(anchor.PortalId) ||
                anchorsByPortalId.ContainsKey(anchor.PortalId))
            {
                UnityEngine.Object.DestroyImmediate(root);
                continue;
            }

            anchorsByPortalId.Add(anchor.PortalId, anchor);
        }

        // Reuse valid anchors so scene object links and Static Rows placement survive every setup pass.
        for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
        {
            PortalPresentationSource source = sources[sourceIndex];

            if (!anchorsByPortalId.TryGetValue(source.PortalId,
                                                out GameRoomPortalRewardLogAnchor anchor))
            {
                anchor = CreatePresentationAnchor(roomScene,
                                                  source,
                                                  portalAnchorPrefab);
            }

            ConfigurePresentationAnchor(anchor, source);
        }
    }

    /// <summary>
    /// Creates one shared prefab instance for a missing portal anchor.
    /// </summary>
    /// <param name="roomScene">Managed room scene receiving the presentation hierarchy.</param>
    /// <param name="source">Portal identity and authored world center.</param>
    /// <param name="portalAnchorPrefab">Shared anchor and log prefab.</param>
    /// <returns>Created managed portal anchor.</returns>
    private static GameRoomPortalRewardLogAnchor CreatePresentationAnchor(
        Scene roomScene,
        PortalPresentationSource source,
        GameObject portalAnchorPrefab)
    {
        GameObject anchorObject =
            PrefabUtility.InstantiatePrefab(portalAnchorPrefab,
                                            roomScene) as GameObject;

        if (anchorObject == null)
            throw new InvalidOperationException(
                "Unity could not instantiate a managed portal reward anchor.");

        GameRoomPortalRewardLogAnchor anchor =
            anchorObject.GetComponent<GameRoomPortalRewardLogAnchor>();

        if (anchor == null)
            throw new InvalidOperationException(
                "The shared portal anchor prefab has no anchor component.");

        return anchor;
    }

    /// <summary>
    /// Aligns one anchor root and refreshes setup-owned component references without moving its log child.
    /// </summary>
    /// <param name="anchor">Existing or newly created managed anchor.</param>
    /// <param name="source">Authoritative portal identity and center.</param>
    private static void ConfigurePresentationAnchor(
        GameRoomPortalRewardLogAnchor anchor,
        PortalPresentationSource source)
    {
        GameObject anchorObject = anchor.gameObject;
        anchorObject.name = AnchorNamePrefix + source.PortalId;
        anchorObject.transform.position = source.WorldCenter;
        GameRoomPortalRewardLogView view =
            anchorObject.GetComponentInChildren<GameRoomPortalRewardLogView>(true);
        GameRoomPortalRewardEffectView effectView =
            anchorObject.GetComponent<GameRoomPortalRewardEffectView>();

        if (effectView == null)
            effectView = anchorObject.AddComponent<GameRoomPortalRewardEffectView>();

        if (view == null)
            throw new InvalidOperationException(
                "The shared portal anchor requires a log view component.");

        anchor.ConfigureAuthoring(source.PortalId, view, effectView);
        view.Hide();
    }
    #endregion

    #region SubScene Inspection
    /// <summary>
    /// Reads every valid portal identity and world center from the SubScenes referenced by one managed room.
    /// </summary>
    /// <param name="roomScene">Managed room scene containing SubScene components.</param>
    /// <returns>Stable portal presentation sources ordered by managed hierarchy and SubScene hierarchy.</returns>
    private static List<PortalPresentationSource> CollectPortalSources(Scene roomScene)
    {
        List<PortalPresentationSource> sources = new List<PortalPresentationSource>(8);
        List<SubScene> subScenes = new List<SubScene>(2);
        GameObject[] roots = roomScene.GetRootGameObjects();

        // Unity replaces the caller list per hierarchy query, so consume each root before inspecting the next.
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            subScenes.Clear();
            roots[rootIndex].GetComponentsInChildren(true, subScenes);

            for (int subSceneIndex = 0; subSceneIndex < subScenes.Count; subSceneIndex++)
                CollectSubScenePortalSources(subScenes[subSceneIndex], sources);
        }

        return sources;
    }

    /// <summary>
    /// Opens one referenced SubScene only when needed and appends all bake-ready portal presentation sources.
    /// </summary>
    /// <param name="subScene">Managed SubScene component referencing the authored ECS scene.</param>
    /// <param name="sources">Mutable destination for collected portal sources.</param>
    private static void CollectSubScenePortalSources(SubScene subScene,
                                                     List<PortalPresentationSource> sources)
    {
        if (subScene == null || subScene.SceneAsset == null)
            return;

        string subScenePath = AssetDatabase.GetAssetPath(subScene.SceneAsset);
        Scene sourceScene = SceneManager.GetSceneByPath(subScenePath);
        bool wasLoaded = sourceScene.IsValid() && sourceScene.isLoaded;

        if (!wasLoaded)
            sourceScene = EditorSceneManager.OpenScene(subScenePath, OpenSceneMode.Additive);

        try
        {
            GameObject[] roots = sourceScene.GetRootGameObjects();
            List<GameRoomPortalAuthoring> portals = new List<GameRoomPortalAuthoring>(8);

            // Read authored portals without mutating the ECS source scene.
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                portals.Clear();
                roots[rootIndex].GetComponentsInChildren(true, portals);

                for (int portalIndex = 0; portalIndex < portals.Count; portalIndex++)
                {
                    GameRoomPortalAuthoring portal = portals[portalIndex];

                    if (portal == null ||
                        string.IsNullOrWhiteSpace(portal.PortalId) ||
                        portal.PortalVolume == null)
                    {
                        continue;
                    }

                    sources.Add(new PortalPresentationSource(
                        portal.PortalId,
                        portal.PortalVolume.transform.TransformPoint(portal.PortalVolume.center)));
                }
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

    #region Nested Types
    /// <summary>
    /// Stores immutable managed presentation identity and placement copied from one SubScene portal.
    /// </summary>
    private readonly struct PortalPresentationSource
    {
        #region Fields
        public readonly string PortalId;
        public readonly Vector3 WorldCenter;
        #endregion

        #region Methods

        #region Constructors
        /// <summary>
        /// Captures one stable portal identity and its authored world-space volume center.
        /// </summary>
        /// <param name="portalId">Stable identifier shared with the baked ECS portal.</param>
        /// <param name="worldCenter">Authored world-space center mirrored by the managed anchor.</param>
        public PortalPresentationSource(string portalId, Vector3 worldCenter)
        {
            PortalId = portalId;
            WorldCenter = worldCenter;
        }
        #endregion

        #endregion
    }
    #endregion
}
#endif
