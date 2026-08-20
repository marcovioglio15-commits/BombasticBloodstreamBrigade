using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Validates managed reward locator identities and positions against authoritative SubScene portals during metadata scans.
/// </summary>
internal sealed class GameRoomRewardPortalAnchorValidationContext
{
    #region Fields
    private readonly List<GameRoomPortalRewardLogAnchor> anchors =
        new List<GameRoomPortalRewardLogAnchor>(8);
    private readonly Dictionary<string, List<Vector3>> portalCenters =
        new Dictionary<string, List<Vector3>>(StringComparer.Ordinal);
    private readonly string rootScenePath;
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one room-scoped validation context retained only for the duration of a metadata scan.
    /// </summary>
    /// <param name="resolvedRootScenePath">Managed room scene expected to own presentation anchors.</param>
    public GameRoomRewardPortalAnchorValidationContext(string resolvedRootScenePath)
    {
        rootScenePath = resolvedRootScenePath;
    }
    #endregion

    #region Collection
    /// <summary>
    /// Collects managed locators from the room root or authoritative portal centers from a bakeable SubScene.
    /// </summary>
    /// <param name="scene">Loaded scene currently visited by the metadata scanner.</param>
    /// <param name="isManagedRootScene">True when the scene owns managed presentation instead of ECS authoring.</param>
    public void ScanScene(Scene scene, bool isManagedRootScene)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        // Managed anchors belong only in the loadable room root.
        if (isManagedRootScene)
        {
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                anchors.AddRange(
                    roots[rootIndex].GetComponentsInChildren<GameRoomPortalRewardLogAnchor>(true));

            return;
        }

        // Bakeable SubScenes provide the identities and volume centers used by runtime resolution.
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            GameRoomPortalAuthoring[] portals =
                roots[rootIndex].GetComponentsInChildren<GameRoomPortalAuthoring>(true);

            for (int portalIndex = 0; portalIndex < portals.Length; portalIndex++)
                AppendPortalCenter(portals[portalIndex]);
        }
    }

    /// <summary>
    /// Stores one valid portal center without hiding duplicate identities already reported by the main scanner.
    /// </summary>
    /// <param name="portal">Bakeable portal authoring whose volume center drives managed locator resolution.</param>
    private void AppendPortalCenter(GameRoomPortalAuthoring portal)
    {
        if (portal == null ||
            string.IsNullOrWhiteSpace(portal.PortalId) ||
            portal.PortalVolume == null)
        {
            return;
        }

        List<Vector3> centers;

        if (!portalCenters.TryGetValue(portal.PortalId, out centers))
        {
            centers = new List<Vector3>(1);
            portalCenters.Add(portal.PortalId, centers);
        }

        centers.Add(portal.PortalVolume.transform.TransformPoint(portal.PortalVolume.center));
    }
    #endregion

    #region Validation
    /// <summary>
    /// Appends actionable locator warnings after the managed root and every reachable SubScene have been scanned.
    /// </summary>
    /// <param name="warnings">Room metadata warning list displayed by the Game Management Tool.</param>
    public void AppendWarnings(List<string> warnings)
    {
        HashSet<string> anchoredPortalIds = new HashSet<string>(StringComparer.Ordinal);

        // Validate every authored locator independently so duplicates and broken references remain visible.
        for (int anchorIndex = 0; anchorIndex < anchors.Count; anchorIndex++)
        {
            GameRoomPortalRewardLogAnchor anchor = anchors[anchorIndex];

            if (anchor == null)
                continue;

            string context = "Managed reward anchor '" +
                             GameRoomMetadataScannerUtility.BuildHierarchyPath(anchor.transform) +
                             "' in '" + rootScenePath + "'";

            if (!anchor.isActiveAndEnabled)
                warnings.Add(context + " is inactive and cannot register its portal reward log at runtime.");

            if (string.IsNullOrWhiteSpace(anchor.PortalId))
            {
                warnings.Add(context + " has no Portal ID and cannot resolve an ECS portal reward log.");
                continue;
            }

            if (!string.Equals(anchor.PortalId, anchor.PortalId.Trim(), StringComparison.Ordinal))
                warnings.Add(context + " contains leading or trailing whitespace in its Portal ID.");

            if (!anchoredPortalIds.Add(anchor.PortalId))
                warnings.Add(context + " duplicates managed Portal ID '" + anchor.PortalId + "'. Keep exactly one reward anchor per SubScene portal.");

            if (anchor.LogView == null)
                warnings.Add(context + " has no fixed log view and cannot present destination rewards.");

            if (anchor.EffectView == null)
            {
                warnings.Add(context + " has no linked-object effect view. Re-run Room Clear Rewards presentation setup.");
            }
            else if (!anchor.EffectView.TryValidateLinkedObjects(out string linkedObjectFailure))
            {
                warnings.Add(context + " has invalid linked objects: " + linkedObjectFailure);
            }

            if (anchor.OffscreenIndicatorView == null)
            {
                warnings.Add(context +
                             " has no preauthored open-portal indicator. Refresh the shared Room Clear Rewards portal indicator prefab.");
            }

            if (!portalCenters.ContainsKey(anchor.PortalId))
            {
                warnings.Add(context + " references Portal ID '" + anchor.PortalId +
                             "', which does not exist in a bakeable SubScene. Re-synchronize anchors after changing a SubScene reference or Portal ID.");
                continue;
            }

        }

        // Every bakeable portal needs one managed presentation locator in the room root.
        foreach (KeyValuePair<string, List<Vector3>> portal in portalCenters)
        {
            if (!anchoredPortalIds.Contains(portal.Key))
                warnings.Add("Portal ID '" + portal.Key + "' has no managed reward anchor in '" +
                             rootScenePath + "'. Re-synchronize managed portal anchors for this room.");
        }
    }

    #endregion

    #endregion
}
