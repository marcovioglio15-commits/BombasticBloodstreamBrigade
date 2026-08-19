using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Registers one preauthored managed-scene portal log against its stable ECS portal identity.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameRoomPortalRewardLogAnchor : MonoBehaviour
{
    #region Constants
    /// <summary>
    /// Gets the maximum world-space distance allowed between a managed anchor and its matching ECS portal center.
    /// </summary>
    public const float MaximumPositionError = 0.25f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Stable portal identifier mirrored from the matching portal authoring component in the room SubScene.")]
    [SerializeField]
    private string portalId;

    [Tooltip("Preauthored world-space log controlled by the ECS presentation bridge for this portal.")]
    [SerializeField]
    private GameRoomPortalRewardLogView logView;

    [Tooltip("Preauthored bridge that maps baked portal effects to linked managed scene objects.")]
    [SerializeField]
    private GameRoomPortalRewardEffectView effectView;
    #endregion

    #region Runtime Fields
    private static readonly List<GameRoomPortalRewardLogAnchor> registeredAnchors =
        new List<GameRoomPortalRewardLogAnchor>(32);
    private static uint revision;
    #endregion

    #endregion

    #region Properties
    /// <summary>
    /// Gets the stable portal identifier mirrored from the bakeable SubScene authoring.
    /// </summary>
    public string PortalId => portalId;

    /// <summary>
    /// Gets the fixed managed log controlled by the ECS presentation bridge.
    /// </summary>
    public GameRoomPortalRewardLogView LogView => logView;

    /// <summary>
    /// Gets the managed Transform and prefab-replacement bridge owned by this anchor.
    /// </summary>
    public GameRoomPortalRewardEffectView EffectView => effectView;

    public static uint Revision => revision;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns the stable portal identity and fixed view during the explicit managed-scene setup workflow.
    /// </summary>
    /// <param name="resolvedPortalId">Stable identifier copied from the matching ECS portal authoring.</param>
    /// <param name="resolvedLogView">Preauthored world-space log owned by this anchor.</param>
    /// <param name="resolvedEffectView">Preauthored linked-object effect bridge owned by this anchor.</param>
    public void ConfigureAuthoring(string resolvedPortalId,
                                   GameRoomPortalRewardLogView resolvedLogView,
                                   GameRoomPortalRewardEffectView resolvedEffectView)
    {
        portalId = resolvedPortalId;
        logView = resolvedLogView;
        effectView = resolvedEffectView;
    }

    /// <summary>
    /// Starts baked portal effects once for a new authoritative portal assignment.
    /// </summary>
    /// <param name="signature">Generation and edge signature preventing duplicate activation.</param>
    /// <param name="animations">Baked Transform animation definitions.</param>
    /// <param name="replacements">Baked prefab replacement definitions.</param>
    /// <param name="hasAudioCue">True when one resolved animation requests the dedicated audio event.</param>
    /// <param name="audioDelay">Delay shared with the audio-owning animation.</param>
    /// <param name="audioPosition">World position used by the positioned audio request.</param>
    /// <returns>True when a new signature was accepted by the effect bridge.</returns>
    public bool ActivateEffects(
        int signature,
        DynamicBuffer<GameRoomPortalTransformAnimationElement> animations,
        DynamicBuffer<GameRoomPortalPrefabReplacementElement> replacements,
        out bool hasAudioCue,
        out float audioDelay,
        out Vector3 audioPosition)
    {
        if (effectView != null)
        {
            return effectView.Activate(signature,
                                       animations,
                                       replacements,
                                       out hasAudioCue,
                                       out audioDelay,
                                       out audioPosition);
        }

        hasAudioCue = false;
        audioDelay = 0f;
        audioPosition = transform.position;
        return false;
    }

    /// <summary>
    /// Resolves the closest loaded managed-scene log matching one ECS portal identity and world position.
    /// </summary>
    /// <param name="resolvedPortalId">Stable ECS portal identifier.</param>
    /// <param name="portalCenter">Current portal center after room-instance placement.</param>
    /// <param name="resolvedAnchor">Closest valid preauthored anchor when resolution succeeds.</param>
    /// <returns>True when one loaded anchor matches both identity and placed world position.</returns>
    public static bool TryResolve(FixedString64Bytes resolvedPortalId,
                                  float3 portalCenter,
                                  out GameRoomPortalRewardLogAnchor resolvedAnchor)
    {
        resolvedAnchor = null;

        if (resolvedPortalId.Length <= 0)
            return false;

        string resolvedPortalIdString = resolvedPortalId.ToString();
        float nearestDistanceSquared = float.MaxValue;

        // Match identity first, then position to disambiguate duplicate staged instances of one room template.
        for (int anchorIndex = registeredAnchors.Count - 1; anchorIndex >= 0; anchorIndex--)
        {
            GameRoomPortalRewardLogAnchor candidate = registeredAnchors[anchorIndex];

            if (candidate == null)
            {
                registeredAnchors.RemoveAt(anchorIndex);
                IncrementRevision();
                continue;
            }

            if (!candidate.isActiveAndEnabled ||
                candidate.logView == null ||
                !string.Equals(candidate.portalId,
                               resolvedPortalIdString,
                               StringComparison.Ordinal))
            {
                continue;
            }

            Vector3 offset = candidate.transform.position -
                             new Vector3(portalCenter.x, portalCenter.y, portalCenter.z);
            float distanceSquared = offset.sqrMagnitude;

            if (distanceSquared >= nearestDistanceSquared)
                continue;

            nearestDistanceSquared = distanceSquared;
            resolvedAnchor = candidate;
        }

        if (nearestDistanceSquared <= MaximumPositionError * MaximumPositionError)
            return true;

        resolvedAnchor = null;
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

            if (anchor == null)
                continue;

            if (anchor.logView != null)
                anchor.logView.Hide();

            if (anchor.effectView != null)
                anchor.effectView.Deactivate();
        }
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Registers this preauthored anchor when its exact managed room scene becomes active.
    /// </summary>
    private void OnEnable()
    {
        if (effectView == null)
            effectView = GetComponent<GameRoomPortalRewardEffectView>();

        if (!registeredAnchors.Contains(this))
        {
            registeredAnchors.Add(this);
            IncrementRevision();
        }

        if (logView != null)
            logView.Hide();

        if (effectView != null)
            effectView.Deactivate();
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

        if (effectView != null)
            effectView.Deactivate();
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
