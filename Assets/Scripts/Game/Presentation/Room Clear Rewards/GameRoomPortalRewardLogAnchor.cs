using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Registers one preauthored managed-scene portal log against its stable ECS portal identity.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameRoomPortalRewardLogAnchor : MonoBehaviour
{
    #region Constants
    private const float MaximumPositionErrorSquared = 0.0625f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Stable portal identifier mirrored from the matching portal authoring component in the room SubScene.")]
    [SerializeField]
    private string portalId;

    [Tooltip("Preauthored world-space log controlled by the ECS presentation bridge for this portal.")]
    [SerializeField]
    private GameRoomPortalRewardLogView logView;
    #endregion

    #region Runtime Fields
    private static readonly List<GameRoomPortalRewardLogAnchor> registeredAnchors =
        new List<GameRoomPortalRewardLogAnchor>(32);
    private static uint revision;
    #endregion

    #endregion

    #region Properties
    public static uint Revision => revision;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns the stable portal identity and fixed view during the explicit managed-scene setup workflow.
    /// </summary>
    /// <param name="resolvedPortalId">Stable identifier copied from the matching ECS portal authoring.</param>
    /// <param name="resolvedLogView">Preauthored world-space log owned by this anchor.</param>
    public void ConfigureAuthoring(string resolvedPortalId,
                                   GameRoomPortalRewardLogView resolvedLogView)
    {
        portalId = resolvedPortalId;
        logView = resolvedLogView;
    }

    /// <summary>
    /// Resolves the closest loaded managed-scene log matching one ECS portal identity and world position.
    /// </summary>
    /// <param name="resolvedPortalId">Stable ECS portal identifier.</param>
    /// <param name="portalCenter">Current portal center after room-instance placement.</param>
    /// <param name="view">Closest valid preauthored log when resolution succeeds.</param>
    /// <returns>True when one loaded anchor matches both identity and placed world position.</returns>
    public static bool TryResolve(FixedString64Bytes resolvedPortalId,
                                  float3 portalCenter,
                                  out GameRoomPortalRewardLogView view)
    {
        view = null;

        if (resolvedPortalId.Length <= 0)
            return false;

        string resolvedPortalIdString = resolvedPortalId.ToString();
        float nearestDistanceSquared = float.MaxValue;

        // Match identity first, then position to disambiguate duplicate staged instances of one room template.
        for (int anchorIndex = registeredAnchors.Count - 1; anchorIndex >= 0; anchorIndex--)
        {
            GameRoomPortalRewardLogAnchor anchor = registeredAnchors[anchorIndex];

            if (anchor == null)
            {
                registeredAnchors.RemoveAt(anchorIndex);
                IncrementRevision();
                continue;
            }

            if (!anchor.isActiveAndEnabled ||
                anchor.logView == null ||
                !string.Equals(anchor.portalId,
                               resolvedPortalIdString,
                               StringComparison.Ordinal))
            {
                continue;
            }

            Vector3 offset = anchor.transform.position -
                             new Vector3(portalCenter.x, portalCenter.y, portalCenter.z);
            float distanceSquared = offset.sqrMagnitude;

            if (distanceSquared >= nearestDistanceSquared)
                continue;

            nearestDistanceSquared = distanceSquared;
            view = anchor.logView;
        }

        if (nearestDistanceSquared <= MaximumPositionErrorSquared)
            return true;

        view = null;
        return false;
    }

    /// <summary>
    /// Hides every registered log before a new procedural graph assignment is presented.
    /// </summary>
    public static void HideAll()
    {
        // Clear only existing views; registration remains stable across graph regeneration in the same room scene.
        for (int anchorIndex = 0; anchorIndex < registeredAnchors.Count; anchorIndex++)
        {
            GameRoomPortalRewardLogAnchor anchor = registeredAnchors[anchorIndex];

            if (anchor != null && anchor.logView != null)
                anchor.logView.Hide();
        }
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Registers this preauthored anchor when its exact managed room scene becomes active.
    /// </summary>
    private void OnEnable()
    {
        if (!registeredAnchors.Contains(this))
        {
            registeredAnchors.Add(this);
            IncrementRevision();
        }

        if (logView != null)
            logView.Hide();
    }

    /// <summary>
    /// Removes this anchor and hides stale content before its managed room instance unloads.
    /// </summary>
    private void OnDisable()
    {
        if (registeredAnchors.Remove(this))
            IncrementRevision();

        if (logView != null)
            logView.Hide();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Advances the registry revision while preserving zero as the initial unregistered state.
    /// </summary>
    private static void IncrementRevision()
    {
        revision++;

        if (revision == 0u)
            revision = 1u;
    }
    #endregion

    #endregion
}
