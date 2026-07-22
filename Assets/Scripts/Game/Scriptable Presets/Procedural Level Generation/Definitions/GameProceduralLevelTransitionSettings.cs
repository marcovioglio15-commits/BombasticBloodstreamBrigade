using System;
using UnityEngine;

/// <summary>
/// Selects the ownership model used while moving between generated rooms.
/// </summary>
public enum GameProceduralRoomStreamingMode : byte
{
    AuthoredSingleSlot = 0,
    TransactionalDualSlot = 1,
    SerialSceneReplacement = 2
}

/// <summary>
/// Selects which graph-adjacent rooms are staged before their portals are crossed.
/// </summary>
public enum GameProceduralAdjacentPreloadPolicy : byte
{
    AllOutgoingUpToBudget = 0,
    FirstOutgoingOnly = 1,
    Disabled = 2
}

/// <summary>
/// Stores presentation and relocation settings used only for room-to-room transitions inside one level.
/// </summary>
[Serializable]
public sealed class GameProceduralLevelTransitionSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Selects authored-coordinate single-slot streaming, optional spatial dual-slot preloading, or the compatibility serial path. Authored Single Slot unloads the source completely before loading the destination and guarantees one resident room instance.")]
    [SerializeField]
    private GameProceduralRoomStreamingMode roomStreamingMode = GameProceduralRoomStreamingMode.AuthoredSingleSlot;

    [Tooltip("Selects which outgoing graph rooms are staged while the current room remains active.")]
    [SerializeField]
    private GameProceduralAdjacentPreloadPolicy adjacentPreloadPolicy = GameProceduralAdjacentPreloadPolicy.Disabled;

    [Tooltip("Maximum number of fully loaded inactive room instances retained as traversal candidates. One is recommended because staged DOTS rooms remain part of world updates.")]
    [SerializeField]
    private int maximumStagedRooms;

    [Tooltip("Prevents portal traversal until its exact target room instance has completed managed and ECS streaming.")]
    [SerializeField]
    private bool requireReadyBeforePortalCommit = true;

    [Tooltip("Maximum number of previously active room instances kept resident before deferred retirement begins. Zero retires the previous room after the protected transition delay.")]
    [SerializeField]
    private int retiredRoomBudget;

    [Tooltip("Main-thread time budget in milliseconds reserved for starting deferred room retirement work outside the fade transaction.")]
    [SerializeField]
    private float retirementWorkBudgetMilliseconds = 1.5f;

    [Tooltip("Keeps the persistent player presentation visible above the black environment fade during intra-level room transitions.")]
    [SerializeField]
    private bool keepPlayerVisible = true;

    [Tooltip("Hides the percentage, progress ring and loading status text only during room-to-room traversal, while preserving fade and player presentation.")]
    [SerializeField]
    private bool hideLoadingProgressDuringRoomTransitions = true;

    [Tooltip("Optional in-place, root-curve-free one-shot animation played by the persistent player presentation during an intra-level transition.")]
    [SerializeField]
    private AnimationClip playerTransitionAnimation;

    [Tooltip("Normalized transition-animation time at which the ready authored-coordinate room is committed and the player is placed at the graph-selected entrance behind black.")]
    [SerializeField]
    private float relocationNormalizedTime = 0.5f;

    [Tooltip("Clears player motion when the authored single-slot or compatibility serial path relocates the player. Optional spatial dual-slot traversal preserves continuous motion.")]
    [SerializeField]
    private bool clearPlayerVelocity = true;
    #endregion

    #endregion

    #region Properties
    public GameProceduralRoomStreamingMode RoomStreamingMode
    {
        get
        {
            return roomStreamingMode;
        }
    }

    public GameProceduralAdjacentPreloadPolicy AdjacentPreloadPolicy
    {
        get
        {
            return adjacentPreloadPolicy;
        }
    }

    public int MaximumStagedRooms
    {
        get
        {
            return maximumStagedRooms;
        }
    }

    public bool RequireReadyBeforePortalCommit
    {
        get
        {
            return requireReadyBeforePortalCommit;
        }
    }

    public int RetiredRoomBudget
    {
        get
        {
            return retiredRoomBudget;
        }
    }

    public float RetirementWorkBudgetMilliseconds
    {
        get
        {
            return retirementWorkBudgetMilliseconds;
        }
    }

    public bool KeepPlayerVisible
    {
        get
        {
            return keepPlayerVisible;
        }
    }

    public AnimationClip PlayerTransitionAnimation
    {
        get
        {
            return playerTransitionAnimation;
        }
    }

    public bool HideLoadingProgressDuringRoomTransitions
    {
        get
        {
            return hideLoadingProgressDuringRoomTransitions;
        }
    }

    public float RelocationNormalizedTime
    {
        get
        {
            return relocationNormalizedTime;
        }
    }

    public bool ClearPlayerVelocity
    {
        get
        {
            return clearPlayerVelocity;
        }
    }

    #endregion
}
