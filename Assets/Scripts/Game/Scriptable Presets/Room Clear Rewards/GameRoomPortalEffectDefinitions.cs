using System;
using UnityEngine;

/// <summary>
/// Defines one local-space Transform animation started by an authoritative portal enable transition.
/// </summary>
[Serializable]
public sealed class GameRoomPortalTransformAnimationDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Scene-object slot resolved by each portal reward anchor when this animation starts.")]
    [SerializeField]
    private GameRoomPortalLinkedObjectSlot targetSlot = GameRoomPortalLinkedObjectSlot.Object01;

    [Tooltip("Local Transform channels animated without using an Animator component.")]
    [SerializeField]
    private GameRoomPortalTransformAnimationMode mode;

    [Tooltip("Controls whether the animation stops at its target, repeats, or alternates direction.")]
    [SerializeField]
    private GameRoomPortalTransformAnimationPlayback playback;

    [Tooltip("Interpolation curve applied to normalized animation progress.")]
    [SerializeField]
    private GameRoomPortalTransformAnimationEase easing = GameRoomPortalTransformAnimationEase.EaseInOut;

    [Tooltip("Delay in seconds after the portal becomes a traversable exit before this animation starts.")]
    [SerializeField]
    private float startDelay;

    [Tooltip("Seconds required to travel from the captured local Transform to the configured target.")]
    [SerializeField]
    private float duration = 0.5f;

    [Tooltip("Local position offset reached at normalized progress one when Position is included.")]
    [SerializeField]
    private Vector3 positionOffset;

    [Tooltip("Local Euler-angle offset reached at normalized progress one when Rotation is included.")]
    [SerializeField]
    private Vector3 rotationOffset;

    [Tooltip("Component-wise local scale multiplier reached at normalized progress one when Scale is included.")]
    [SerializeField]
    private Vector3 scaleMultiplier = Vector3.one;

    [Tooltip("Requests the dedicated portal-animation FMOD event when this animation exits its delay.")]
    [SerializeField]
    private bool playAudioEvent;
    #endregion

    #endregion

    #region Properties
    public GameRoomPortalLinkedObjectSlot TargetSlot => targetSlot;
    public GameRoomPortalTransformAnimationMode Mode => mode;
    public GameRoomPortalTransformAnimationPlayback Playback => playback;
    public GameRoomPortalTransformAnimationEase Easing => easing;
    public float StartDelay => startDelay;
    public float Duration => duration;
    public Vector3 PositionOffset => positionOffset;
    public Vector3 RotationOffset => rotationOffset;
    public Vector3 ScaleMultiplier => scaleMultiplier;
    public bool PlayAudioEvent => playAudioEvent;
    #endregion
}

/// <summary>
/// Defines one scene-object prefab replacement applied before portal activation animations are resolved.
/// </summary>
[Serializable]
public sealed class GameRoomPortalPrefabReplacementDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Slot of the existing 3D scene GameObject disabled when the portal becomes traversable.")]
    [SerializeField]
    private GameRoomPortalLinkedObjectSlot targetSlot = GameRoomPortalLinkedObjectSlot.Object01;

    [Tooltip("Prefab asset instantiated only on activation as a sibling with the 3D scene object's local position, rotation and scale.")]
    [SerializeField]
    private GameObject replacementPrefab;
    #endregion

    #endregion

    #region Properties
    public GameRoomPortalLinkedObjectSlot TargetSlot => targetSlot;
    public GameObject ReplacementPrefab => replacementPrefab;
    #endregion
}

/// <summary>
/// Provides shared channel tests for runtime composition and conditional editor presentation.
/// </summary>
public static class GameRoomPortalTransformAnimationModeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Returns whether an animation mode writes local position.
    /// </summary>
    /// <param name="mode">Animation mode to inspect.</param>
    /// <returns>True when local position participates in the animation.</returns>
    public static bool IncludesPosition(GameRoomPortalTransformAnimationMode mode)
    {
        switch (mode)
        {
            case GameRoomPortalTransformAnimationMode.Position:
            case GameRoomPortalTransformAnimationMode.PositionAndRotation:
            case GameRoomPortalTransformAnimationMode.PositionAndScale:
            case GameRoomPortalTransformAnimationMode.PositionRotationAndScale:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Returns whether an animation mode writes local rotation.
    /// </summary>
    /// <param name="mode">Animation mode to inspect.</param>
    /// <returns>True when local rotation participates in the animation.</returns>
    public static bool IncludesRotation(GameRoomPortalTransformAnimationMode mode)
    {
        switch (mode)
        {
            case GameRoomPortalTransformAnimationMode.Rotation:
            case GameRoomPortalTransformAnimationMode.PositionAndRotation:
            case GameRoomPortalTransformAnimationMode.RotationAndScale:
            case GameRoomPortalTransformAnimationMode.PositionRotationAndScale:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Returns whether an animation mode writes local scale.
    /// </summary>
    /// <param name="mode">Animation mode to inspect.</param>
    /// <returns>True when local scale participates in the animation.</returns>
    public static bool IncludesScale(GameRoomPortalTransformAnimationMode mode)
    {
        switch (mode)
        {
            case GameRoomPortalTransformAnimationMode.Scale:
            case GameRoomPortalTransformAnimationMode.PositionAndScale:
            case GameRoomPortalTransformAnimationMode.RotationAndScale:
            case GameRoomPortalTransformAnimationMode.PositionRotationAndScale:
                return true;
            default:
                return false;
        }
    }
    #endregion

    #endregion
}
