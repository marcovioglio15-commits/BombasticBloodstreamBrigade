using System;
using UnityEngine;

/// <summary>
/// Defines one Transform or Animator-clip animation started by an authoritative portal enable transition.
/// </summary>
[Serializable]
public sealed class GameRoomPortalActivationAnimationDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable identifier of the freely authored linked scene object animated when this portal opens.")]
    [SerializeField]
    private string targetBindingId;

    [Tooltip("Former fixed slot retained only to preserve existing serialized associations during automatic migration.")]
    [HideInInspector]
    [SerializeField]
    private int targetSlot;

    [Tooltip("Selects local Transform interpolation or direct playback of a clip found on an Animator below the linked object.")]
    [SerializeField]
    private GameRoomPortalActivationAnimationSource source;

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

    [Tooltip("Animation clip played directly on the selected Animator without requiring a matching controller state.")]
    [SerializeField]
    private AnimationClip animatorClip;

    [Tooltip("Relative hierarchy path from the linked object to the Animator that owns the selected clip.")]
    [SerializeField]
    private string animatorPath;

    [Tooltip("Positive playback speed multiplier applied to the selected Animator clip.")]
    [SerializeField]
    private float animatorSpeed = 1f;
    #endregion

    #endregion

    #region Properties
    public string TargetBindingId => string.IsNullOrWhiteSpace(targetBindingId)
        ? GameRoomPortalLinkedObjectBindingIdUtility.FromLegacySlot(targetSlot)
        : targetBindingId;
    public GameRoomPortalActivationAnimationSource Source => source;
    public GameRoomPortalTransformAnimationMode Mode => mode;
    public GameRoomPortalTransformAnimationPlayback Playback => playback;
    public GameRoomPortalTransformAnimationEase Easing => easing;
    public float StartDelay => startDelay;
    public float Duration => duration;
    public Vector3 PositionOffset => positionOffset;
    public Vector3 RotationOffset => rotationOffset;
    public Vector3 ScaleMultiplier => scaleMultiplier;
    public AnimationClip AnimatorClip => animatorClip;
    public string AnimatorPath => animatorPath;
    public float AnimatorSpeed => animatorSpeed;
    #endregion
    #region Methods

    #region Public Methods
    /// <summary>
    /// Migrates the former fixed slot value into a stable freely authored binding identifier.
    /// </summary>
    public void EnsureInitialized()
    {
        if (!string.IsNullOrWhiteSpace(targetBindingId))
            return;

        targetBindingId = GameRoomPortalLinkedObjectBindingIdUtility.FromLegacySlot(targetSlot);
    }
    #endregion

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
    [Tooltip("Stable identifier of the existing 3D scene GameObject disabled when the portal becomes traversable.")]
    [SerializeField]
    private string targetBindingId;

    [Tooltip("Former fixed slot retained only to preserve existing serialized associations during automatic migration.")]
    [HideInInspector]
    [SerializeField]
    private int targetSlot;

    [Tooltip("Prefab asset instantiated only on activation as a sibling with the 3D scene object's local position, rotation and scale.")]
    [SerializeField]
    private GameObject replacementPrefab;
    #endregion

    #endregion

    #region Properties
    public string TargetBindingId => string.IsNullOrWhiteSpace(targetBindingId)
        ? GameRoomPortalLinkedObjectBindingIdUtility.FromLegacySlot(targetSlot)
        : targetBindingId;
    public GameObject ReplacementPrefab => replacementPrefab;
    #endregion
    #region Methods

    #region Public Methods
    /// <summary>
    /// Migrates the former fixed slot value into a stable freely authored binding identifier.
    /// </summary>
    public void EnsureInitialized()
    {
        if (!string.IsNullOrWhiteSpace(targetBindingId))
            return;

        targetBindingId = GameRoomPortalLinkedObjectBindingIdUtility.FromLegacySlot(targetSlot);
    }
    #endregion

    #endregion
}

/// <summary>
/// Converts legacy numeric bindings and generates stable identifiers for freely sized scene-object lists.
/// </summary>
public static class GameRoomPortalLinkedObjectBindingIdUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Converts one former enum value without altering existing scene-to-preset associations.
    /// </summary>
    /// <param name="legacySlot">Former serialized slot number.</param>
    /// <returns>Stable migrated identifier, or an empty string when no slot was authored.</returns>
    public static string FromLegacySlot(int legacySlot)
    {
        return legacySlot > 0 ? "Object" + legacySlot.ToString("00") : string.Empty;
    }

    /// <summary>
    /// Creates a compact stable identifier for a newly added linked object.
    /// </summary>
    /// <returns>Unique identifier suitable for serialization and ECS fixed strings.</returns>
    public static string Create()
    {
        return Guid.NewGuid().ToString("N");
    }
    #endregion

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
