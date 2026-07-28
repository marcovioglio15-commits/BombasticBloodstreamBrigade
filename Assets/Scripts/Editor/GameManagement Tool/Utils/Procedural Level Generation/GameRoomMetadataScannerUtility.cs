using System;
using System.Collections.Generic;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scans room root scenes and recursively referenced SubScenes without changing the designer's open-scene setup.
/// </summary>
public static class GameRoomMetadataScannerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Refreshes every unique room scene referenced by tiles in one procedural level preset.
    /// </summary>
    /// <param name="preset">Procedural preset whose referenced rooms should be scanned.</param>
    /// <returns>Refresh report containing updated count, warnings and blocking errors.</returns>
    public static GameRoomMetadataRefreshReport RefreshReferencedRooms(GameProceduralLevelPreset preset)
    {
        GameRoomMetadataRefreshReport report = new GameRoomMetadataRefreshReport();

        if (!ValidatePresetCatalog(preset, report))
            return report;

        List<string> sceneIds = CollectReferencedSceneIds(preset);

        RefreshSceneIds(preset, sceneIds, report, "No room tile with a Scene ID is available to refresh.");
        return report;
    }

    /// <summary>
    /// Refreshes every unique room referenced by one selected procedural level.
    /// </summary>
    /// <param name="preset">Procedural preset receiving refreshed cache entries.</param>
    /// <param name="level">Selected level whose tile scenes should be scanned.</param>
    /// <returns>Refresh report containing updated count, warnings and blocking errors.</returns>
    public static GameRoomMetadataRefreshReport RefreshLevelRooms(GameProceduralLevelPreset preset,
                                                                  GameProceduralLevelDefinition level)
    {
        GameRoomMetadataRefreshReport report = new GameRoomMetadataRefreshReport();

        if (!ValidatePresetCatalog(preset, report))
            return report;

        if (level == null)
        {
            report.AddError("A procedural level is required to refresh level room metadata.");
            return report;
        }

        List<string> sceneIds = CollectReferencedSceneIds(level);
        RefreshSceneIds(preset, sceneIds, report, "The selected level has no room tile with a Scene ID to refresh.");
        return report;
    }

    /// <summary>
    /// Refreshes an exact set of canonical room Scene IDs through one shared deterministic pipeline.
    /// </summary>
    /// <param name="preset">Procedural preset receiving refreshed cache entries.</param>
    /// <param name="sceneIds">Unique Scene IDs to scan in ordinal order.</param>
    /// <returns>Refresh report containing updated count, warnings and blocking errors.</returns>
    public static GameRoomMetadataRefreshReport RefreshRooms(GameProceduralLevelPreset preset,
                                                              IReadOnlyCollection<string> sceneIds)
    {
        GameRoomMetadataRefreshReport report = new GameRoomMetadataRefreshReport();

        if (!ValidatePresetCatalog(preset, report))
            return report;

        List<string> orderedSceneIds = BuildOrderedUniqueSceneIds(sceneIds);
        RefreshSceneIds(preset, orderedSceneIds, report, string.Empty);
        return report;
    }

    /// <summary>
    /// Collects unique room Scene IDs referenced by all authored levels in one preset.
    /// </summary>
    /// <param name="preset">Procedural preset whose room references should be collected.</param>
    /// <returns>Ordinally sorted unique Scene IDs.</returns>
    public static List<string> CollectReferencedSceneIds(GameProceduralLevelPreset preset)
    {
        List<string> sceneIds = new List<string>();
        HashSet<string> uniqueSceneIds = new HashSet<string>(StringComparer.Ordinal);

        if (preset == null || preset.Levels == null)
            return sceneIds;

        // Include disabled levels so their metadata is ready before designers enable them.
        for (int levelIndex = 0; levelIndex < preset.Levels.Count; levelIndex++)
            AppendReferencedSceneIds(preset.Levels[levelIndex], sceneIds, uniqueSceneIds);

        sceneIds.Sort(StringComparer.Ordinal);
        return sceneIds;
    }

    /// <summary>
    /// Collects unique room Scene IDs referenced by one authored level.
    /// </summary>
    /// <param name="level">Procedural level whose room references should be collected.</param>
    /// <returns>Ordinally sorted unique Scene IDs.</returns>
    public static List<string> CollectReferencedSceneIds(GameProceduralLevelDefinition level)
    {
        List<string> sceneIds = new List<string>();
        HashSet<string> uniqueSceneIds = new HashSet<string>(StringComparer.Ordinal);
        AppendReferencedSceneIds(level, sceneIds, uniqueSceneIds);
        sceneIds.Sort(StringComparer.Ordinal);
        return sceneIds;
    }

    /// <summary>
    /// Scans ordered room IDs and appends every result to one shared refresh report.
    /// </summary>
    /// <param name="preset">Procedural preset receiving refreshed snapshots.</param>
    /// <param name="sceneIds">Ordered unique Scene IDs to scan.</param>
    /// <param name="report">Shared refresh report.</param>
    /// <param name="emptyWarning">Optional warning emitted when the collection is empty.</param>
    private static void RefreshSceneIds(GameProceduralLevelPreset preset,
                                        IReadOnlyList<string> sceneIds,
                                        GameRoomMetadataRefreshReport report,
                                        string emptyWarning)
    {

        if (sceneIds.Count == 0)
        {
            report.AddWarning(emptyWarning);
            return;
        }

        // Refresh scene snapshots in ordinal order so asset diffs remain deterministic.
        for (int index = 0; index < sceneIds.Count; index++)
            RefreshRoomInternal(preset, sceneIds[index], report);
    }

    /// <summary>
    /// Refreshes one room metadata snapshot selected by its canonical Scene Manager scene ID.
    /// </summary>
    /// <param name="preset">Procedural preset receiving the updated cache entry.</param>
    /// <param name="sceneId">Canonical room Scene ID selected by a tile.</param>
    /// <returns>Refresh report containing scan warnings or blocking errors.</returns>
    public static GameRoomMetadataRefreshReport RefreshRoom(GameProceduralLevelPreset preset, string sceneId)
    {
        GameRoomMetadataRefreshReport report = new GameRoomMetadataRefreshReport();

        if (!ValidatePresetCatalog(preset, report))
            return report;

        if (string.IsNullOrWhiteSpace(sceneId))
        {
            report.AddError("A non-empty Scene ID is required to refresh room metadata.");
            return report;
        }

        RefreshRoomInternal(preset, sceneId, report);
        return report;
    }
    #endregion

    #region Refresh Pipeline
    /// <summary>
    /// Resolves, scans and serializes one room scene while appending diagnostics to a shared report.
    /// </summary>
    /// <param name="preset">Procedural preset receiving the snapshot.</param>
    /// <param name="sceneId">Canonical Scene Manager scene ID.</param>
    /// <param name="report">Shared refresh report.</param>
    private static void RefreshRoomInternal(GameProceduralLevelPreset preset,
                                            string sceneId,
                                            GameRoomMetadataRefreshReport report)
    {
        GameSceneDefinition sceneDefinition;

        if (!preset.SceneCatalogPreset.TryFindScene(sceneId, out sceneDefinition) || sceneDefinition == null)
        {
            report.AddError("Room Scene ID '" + sceneId + "' is missing from the selected Scene Manager preset.");
            return;
        }

        string scenePath = GameRoomMetadataDependencyUtility.NormalizeAssetPath(sceneDefinition.ScenePath);

        if (string.IsNullOrWhiteSpace(scenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
        {
            report.AddError("Room Scene ID '" + sceneId + "' does not resolve to a readable Unity scene asset.");
            return;
        }

        GameRoomMetadataScanSnapshot snapshot = new GameRoomMetadataScanSnapshot
        {
            SceneId = sceneId,
            SceneGuid = AssetDatabase.AssetPathToGUID(scenePath)
        };

        if (!ScanSceneHierarchy(scenePath, snapshot, report))
            return;

        snapshot.SourceScenePaths.Sort(StringComparer.Ordinal);
        snapshot.Portals.Sort(ComparePortalSnapshots);
        snapshot.DependencyHash = GameRoomMetadataDependencyUtility.ComputeCombinedDependencyHash(snapshot.SourceScenePaths);

        if (!GameRoomMetadataSerializedWriteUtility.Write(preset, snapshot))
        {
            report.AddError("Room metadata for Scene ID '" + sceneId + "' could not be written through Unity serialization.");
            return;
        }

        report.RecordRefreshedRoom();
    }

    /// <summary>
    /// Opens only missing scenes, scans the root and nested SubScenes, then closes only scenes opened by this operation.
    /// </summary>
    /// <param name="rootScenePath">Root room scene asset path.</param>
    /// <param name="snapshot">Mutable scan output.</param>
    /// <param name="report">Refresh report receiving blocking scan errors.</param>
    /// <returns>True when every reachable source scene was scanned successfully.</returns>
    private static bool ScanSceneHierarchy(string rootScenePath,
                                           GameRoomMetadataScanSnapshot snapshot,
                                           GameRoomMetadataRefreshReport report)
    {
        Queue<string> pendingPaths = new Queue<string>();
        HashSet<string> visitedPaths = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> portalIds = new HashSet<string>(StringComparer.Ordinal);
        List<Scene> openedByScanner = new List<Scene>();
        Scene originalActiveScene = SceneManager.GetActiveScene();
        pendingPaths.Enqueue(rootScenePath);

        try
        {
            // Traverse referenced SubScenes breadth-first so cycles and duplicate references are handled safely.
            while (pendingPaths.Count > 0)
            {
                string scenePath = pendingPaths.Dequeue();

                if (!visitedPaths.Add(scenePath))
                    continue;

                Scene scene;

                if (!TryOpenSceneForRead(scenePath, openedByScanner, out scene))
                {
                    report.AddError("Room metadata scan could not open scene '" + scenePath + "'.");
                    return false;
                }

                snapshot.SourceScenePaths.Add(scenePath);

                if (scene.isDirty)
                {
                    snapshot.CacheStale = true;
                    snapshot.AuthoringWarnings.Add("Source scene '" + scenePath + "' has unsaved changes. The scan preserved them, but this cache remains stale until the scene is saved and refreshed.");
                }

                ScanAuthoringComponents(scene,
                                        string.Equals(scenePath, rootScenePath, StringComparison.Ordinal),
                                        snapshot,
                                        portalIds);
                EnqueueReferencedSubScenes(scene, pendingPaths, snapshot.AuthoringWarnings);
            }

            return true;
        }
        catch (Exception exception)
        {
            report.AddError("Room metadata scan failed for '" + rootScenePath + "': " + exception.Message);
            return false;
        }
        finally
        {
            // Close only scenes opened by this scan; already-open and dirty scenes remain untouched.
            for (int index = openedByScanner.Count - 1; index >= 0; index--)
            {
                Scene scene = openedByScanner[index];

                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
                SceneManager.SetActiveScene(originalActiveScene);
        }
    }
    #endregion

    #region Scene Scanning
    /// <summary>
    /// Reuses one loaded scene or opens it additively for read-only scanning.
    /// </summary>
    /// <param name="scenePath">Project-relative scene asset path.</param>
    /// <param name="openedByScanner">Scenes that must be closed after scanning.</param>
    /// <param name="scene">Resolved loaded scene.</param>
    /// <returns>True when the scene is valid and loaded.</returns>
    private static bool TryOpenSceneForRead(string scenePath, List<Scene> openedByScanner, out Scene scene)
    {
        scene = SceneManager.GetSceneByPath(scenePath);

        if (scene.IsValid() && scene.isLoaded)
            return true;

        scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        openedByScanner.Add(scene);
        return true;
    }

    /// <summary>
    /// Collects portals and center anchors from one loaded root scene or SubScene.
    /// </summary>
    /// <param name="scene">Loaded source scene.</param>
    /// <param name="isManagedRootScene">True when this is the loadable root scene rather than a baked SubScene.</param>
    /// <param name="snapshot">Mutable room snapshot.</param>
    /// <param name="portalIds">Cross-scene uniqueness guard for physical Portal IDs.</param>
    private static void ScanAuthoringComponents(Scene scene,
                                                bool isManagedRootScene,
                                                GameRoomMetadataScanSnapshot snapshot,
                                                HashSet<string> portalIds)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        bool foundRootAuthoring = false;

        // Include inactive authoring objects in diagnostics even though inactive hierarchy state may prevent expected baking.
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            GameRoomPortalAuthoring[] portals = roots[rootIndex].GetComponentsInChildren<GameRoomPortalAuthoring>(true);
            GameRoomCenterAnchorAuthoring[] centerAnchors = roots[rootIndex].GetComponentsInChildren<GameRoomCenterAnchorAuthoring>(true);
            EnemySpawnerAuthoring[] spawners = roots[rootIndex].GetComponentsInChildren<EnemySpawnerAuthoring>(true);
            snapshot.CenterAnchorCount += centerAnchors.Length;
            foundRootAuthoring |= isManagedRootScene &&
                                  (portals.Length > 0 || centerAnchors.Length > 0 || spawners.Length > 0);

            for (int portalIndex = 0; portalIndex < portals.Length; portalIndex++)
                AppendPortal(portals[portalIndex], scene.path, snapshot, portalIds);

            for (int anchorIndex = 0; anchorIndex < centerAnchors.Length; anchorIndex++)
            {
                GameRoomCenterAnchorAuthoring anchor = centerAnchors[anchorIndex];

                if (anchor != null && !anchor.gameObject.activeInHierarchy)
                    snapshot.AuthoringWarnings.Add("Center anchor '" + BuildHierarchyPath(anchor.transform) + "' in '" + scene.path + "' is inactive and may not bake as an available arrival pose.");
            }

            for (int spawnerIndex = 0; spawnerIndex < spawners.Length; spawnerIndex++)
                AppendSpawner(spawners[spawnerIndex], scene.path, isManagedRootScene, snapshot);
        }

        if (!foundRootAuthoring)
            return;

        snapshot.CacheStale = true;
        snapshot.AuthoringWarnings.Add("Portal, center-anchor or enemy-spawner authoring found in the managed root scene cannot produce runtime ECS entities. Move these components into a referenced SubScene, save it and refresh room metadata.");
    }

    /// <summary>
    /// Counts one active bakeable enemy spawner and records why it cannot make a room reward-eligible.
    /// </summary>
    /// <param name="spawner">Enemy spawner authoring component to inspect.</param>
    /// <param name="scenePath">Scene or SubScene owning the component.</param>
    /// <param name="isManagedRootScene">True when the component cannot bake as a room runtime entity.</param>
    /// <param name="snapshot">Mutable room metadata snapshot.</param>
    private static void AppendSpawner(EnemySpawnerAuthoring spawner,
                                      string scenePath,
                                      bool isManagedRootScene,
                                      GameRoomMetadataScanSnapshot snapshot)
    {
        if (spawner == null)
            return;

        string context = "Enemy spawner '" + BuildHierarchyPath(spawner.transform) + "' in '" + scenePath + "'";
        bool active = spawner.enabled &&
                      spawner.gameObject.activeInHierarchy &&
                      spawner.RuntimeEnabledByDefault;

        if (!active)
            return;

        if (isManagedRootScene)
        {
            snapshot.AuthoringWarnings.Add(context + " is active but cannot contribute to Room Clear Reward eligibility because it is outside a bakeable SubScene.");
            return;
        }

        snapshot.ActiveSpawnerCount++;
        bool hasNonemptyWave = false;

        if (spawner.Waves != null)
        {
            for (int waveIndex = 0; waveIndex < spawner.Waves.Count; waveIndex++)
            {
                EnemySpawnWaveAuthoring wave = spawner.Waves[waveIndex];

                if (wave == null ||
                    EnemySpawnerWaveBakeUtility.CountWaveEnemies(wave) <= 0)
                {
                    continue;
                }

                hasNonemptyWave = true;
                break;
            }
        }

        if (hasNonemptyWave)
        {
            snapshot.ActiveSpawnerWithWavesCount++;
            return;
        }

        snapshot.AuthoringWarnings.Add(context + " is active but has no wave containing at least one enemy, so its room cannot receive Room Clear Rewards.");
    }

    /// <summary>
    /// Validates and appends one individual portal while allowing arbitrary same-side multiplicity.
    /// </summary>
    /// <param name="portal">Portal authoring component to inspect.</param>
    /// <param name="scenePath">Scene or SubScene owning the component.</param>
    /// <param name="snapshot">Mutable room snapshot.</param>
    /// <param name="portalIds">Cross-scene Portal ID uniqueness guard.</param>
    private static void AppendPortal(GameRoomPortalAuthoring portal,
                                     string scenePath,
                                     GameRoomMetadataScanSnapshot snapshot,
                                     HashSet<string> portalIds)
    {
        if (portal == null)
            return;

        string context = "Portal '" + BuildHierarchyPath(portal.transform) + "' in '" + scenePath + "'";
        string portalId = portal.PortalId ?? string.Empty;

        // Keep the cache structurally stale whenever the Baker would omit either runtime half of this portal.
        if (!portal.TryValidateBakeReadiness(out string bakeFailureMessage))
        {
            snapshot.CacheStale = true;
            snapshot.AuthoringWarnings.Add(context + " cannot produce complete runtime portal data because " +
                                           bakeFailureMessage + ". Fix the authoring and refresh this room.");
        }

        if (!string.IsNullOrWhiteSpace(portalId) && !portalIds.Add(portalId))
            snapshot.AuthoringWarnings.Add(context + " duplicates Portal ID '" + portalId + "'. Portal IDs must be unique across the room root and nested SubScenes.");

        if (!string.Equals(portalId, portalId.Trim(), StringComparison.Ordinal))
            snapshot.AuthoringWarnings.Add(context + " contains leading or trailing whitespace in its Portal ID.");

        if (portal.PortalVolume != null && !portal.PortalVolume.isTrigger)
            snapshot.AuthoringWarnings.Add(context + " uses a non-trigger BoxCollider; ECS can evaluate it, but the collider may physically obstruct traversal.");

        if (portal.ArrivalAnchor == null)
            snapshot.AuthoringWarnings.Add(context + " has no arrival anchor; bake fallback uses the portal transform but designer-facing validation remains unresolved.");

        if (portal.InwardOffset < 0f)
            snapshot.AuthoringWarnings.Add(context + " has a negative inward offset and may place the player back inside the entry volume.");

        if (portal.PortalVolume != null && portal.IsArrivalInsidePortalVolume())
            snapshot.AuthoringWarnings.Add(context + " resolves its arrival pose inside the closed blocker volume. Move the anchor or increase Inward Offset before generation.");

        if (portal.Capability == GameRoomPortalCapability.Entrance &&
            portal.ConnectionPolicy == GameRoomPortalConnectionPolicy.LevelExit)
            snapshot.AuthoringWarnings.Add(context + " is Entrance-only but uses LevelExit policy, which requires an outgoing capability.");

        snapshot.Portals.Add(new GameRoomPortalScanSnapshot(portalId,
                                                            portal.Side,
                                                            portal.Capability,
                                                            portal.ConnectionPolicy));
    }

    /// <summary>
    /// Finds SubScene components and queues their referenced scene assets for recursive scanning.
    /// </summary>
    /// <param name="scene">Loaded parent scene.</param>
    /// <param name="pendingPaths">Breadth-first scene work queue.</param>
    /// <param name="warnings">Target authoring warning list.</param>
    private static void EnqueueReferencedSubScenes(Scene scene,
                                                   Queue<string> pendingPaths,
                                                   List<string> warnings)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        // Read every nested SubScene reference without opening inspector state or changing auto-load settings.
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            SubScene[] subScenes = roots[rootIndex].GetComponentsInChildren<SubScene>(true);

            for (int subSceneIndex = 0; subSceneIndex < subScenes.Length; subSceneIndex++)
            {
                SubScene subScene = subScenes[subSceneIndex];
                string subScenePath = ResolveSubSceneAssetPath(subScene);

                if (subScene != null && subScene.AutoLoadScene)
                    warnings.Add("SubScene component '" + BuildHierarchyPath(subScene.transform) + "' in '" + scene.path + "' must disable Auto Load Scene for duplicate-capable transactional room streaming.");

                if (string.IsNullOrWhiteSpace(subScenePath))
                {
                    warnings.Add("SubScene component '" + BuildHierarchyPath(subScenes[subSceneIndex].transform) + "' in '" + scene.path + "' has no readable scene asset.");
                    continue;
                }

                pendingPaths.Enqueue(subScenePath);
            }
        }
    }

    /// <summary>
    /// Resolves the scene asset referenced by one DOTS SubScene component through its serialized editor field.
    /// </summary>
    /// <param name="subScene">SubScene component to inspect.</param>
    /// <returns>Project-relative SubScene path, or an empty string when missing.</returns>
    private static string ResolveSubSceneAssetPath(SubScene subScene)
    {
        if (subScene == null)
            return string.Empty;

        SerializedObject serializedSubScene = new SerializedObject(subScene);
        SerializedProperty sceneAssetProperty = serializedSubScene.FindProperty("_SceneAsset");
        SceneAsset sceneAsset = sceneAssetProperty != null ? sceneAssetProperty.objectReferenceValue as SceneAsset : null;

        if (sceneAsset == null)
            return string.Empty;

        return GameRoomMetadataDependencyUtility.NormalizeAssetPath(AssetDatabase.GetAssetPath(sceneAsset));
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Validates only references required before scanning, without correcting preset data.
    /// </summary>
    /// <param name="preset">Procedural preset to validate.</param>
    /// <param name="report">Report receiving blocking errors.</param>
    /// <returns>True when a Scene Manager catalog is available.</returns>
    private static bool ValidatePresetCatalog(GameProceduralLevelPreset preset, GameRoomMetadataRefreshReport report)
    {
        if (preset == null)
        {
            report.AddError("A Procedural Level preset is required to refresh room metadata.");
            return false;
        }

        if (preset.SceneCatalogPreset != null)
            return true;

        report.AddError("Procedural Level preset '" + preset.name + "' has no Scene Manager catalog reference.");
        return false;
    }

    /// <summary>
    /// Appends unique room Scene IDs used by one enabled or disabled authored level.
    /// </summary>
    /// <param name="level">Procedural level to inspect.</param>
    /// <param name="sceneIds">Ordered output list.</param>
    /// <param name="uniqueSceneIds">Uniqueness guard shared across levels.</param>
    private static void AppendReferencedSceneIds(GameProceduralLevelDefinition level,
                                                 List<string> sceneIds,
                                                 HashSet<string> uniqueSceneIds)
    {
        if (level == null || level.RoomTiles == null)
            return;

        // Include every authored tile so disabled levels remain ready when enabled later.
        for (int tileIndex = 0; tileIndex < level.RoomTiles.Count; tileIndex++)
        {
            GameProceduralRoomTileDefinition tile = level.RoomTiles[tileIndex];

            if (tile == null || string.IsNullOrWhiteSpace(tile.SceneId))
                continue;

            if (uniqueSceneIds.Add(tile.SceneId))
                sceneIds.Add(tile.SceneId);
        }
    }

    /// <summary>
    /// Normalizes an arbitrary Scene ID collection into deterministic unique scan order.
    /// </summary>
    /// <param name="sceneIds">Raw Scene IDs requested by a caller.</param>
    /// <returns>Ordinally sorted non-empty unique Scene IDs.</returns>
    private static List<string> BuildOrderedUniqueSceneIds(IReadOnlyCollection<string> sceneIds)
    {
        List<string> orderedSceneIds = new List<string>();
        HashSet<string> uniqueSceneIds = new HashSet<string>(StringComparer.Ordinal);

        if (sceneIds == null)
            return orderedSceneIds;

        foreach (string sceneId in sceneIds)
        {
            if (!string.IsNullOrWhiteSpace(sceneId) && uniqueSceneIds.Add(sceneId))
                orderedSceneIds.Add(sceneId);
        }

        orderedSceneIds.Sort(StringComparer.Ordinal);
        return orderedSceneIds;
    }

    /// <summary>
    /// Builds a stable hierarchy path used by scanner diagnostics.
    /// </summary>
    /// <param name="transform">Authoring transform to describe.</param>
    /// <returns>Slash-separated hierarchy path.</returns>
    private static string BuildHierarchyPath(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        string path = transform.name;
        Transform parent = transform.parent;

        // Walk toward the source-scene root so duplicate names remain diagnosable.
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    /// <summary>
    /// Orders portal snapshots by side, capability, ID and policy for deterministic serialized diffs.
    /// </summary>
    /// <param name="left">First portal signature.</param>
    /// <param name="right">Second portal signature.</param>
    /// <returns>Negative, zero or positive ordering value.</returns>
    private static int ComparePortalSnapshots(GameRoomPortalScanSnapshot left, GameRoomPortalScanSnapshot right)
    {
        int sideComparison = left.Side.CompareTo(right.Side);

        if (sideComparison != 0)
            return sideComparison;

        int capabilityComparison = left.Capability.CompareTo(right.Capability);

        if (capabilityComparison != 0)
            return capabilityComparison;

        int idComparison = string.Compare(left.PortalId, right.PortalId, StringComparison.Ordinal);

        if (idComparison != 0)
            return idComparison;

        return left.ConnectionPolicy.CompareTo(right.ConnectionPolicy);
    }
    #endregion

    #endregion
}
