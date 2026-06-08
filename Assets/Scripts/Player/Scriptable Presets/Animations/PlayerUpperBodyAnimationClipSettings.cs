using System;
using UnityEngine;

/// <summary>
/// Stores concrete upper-body charge and release clips selected by scalable modular power-up payload enums.
/// Per-weapon shooting clips moved to <see cref="PlayerWeaponVisualSettings"/>; the implicit Base Gun
/// shooting clip is derived from the visual preset Default Additional Weapon Visual.
/// </summary>
[Serializable]
public sealed class PlayerUpperBodyAnimationClipSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Primary optional upper-body animation played while a Trigger Hold Charge module is charging.")]
    [SerializeField]
    private AnimationClip primaryChargeClip;

    [Tooltip("Secondary optional upper-body animation played while a Trigger Hold Charge module is charging.")]
    [SerializeField]
    private AnimationClip secondaryChargeClip;

    [Tooltip("Primary optional upper-body animation played when a Trigger Hold Charge input is released.")]
    [SerializeField]
    private AnimationClip primaryReleaseClip;

    [Tooltip("Secondary optional upper-body animation played when a Trigger Hold Charge input is released.")]
    [SerializeField]
    private AnimationClip secondaryReleaseClip;
    #endregion

    #endregion

    #region Properties
    public AnimationClip PrimaryChargeClip
    {
        get
        {
            return primaryChargeClip;
        }
    }

    public AnimationClip SecondaryChargeClip
    {
        get
        {
            return secondaryChargeClip;
        }
    }

    public AnimationClip PrimaryReleaseClip
    {
        get
        {
            return primaryReleaseClip;
        }
    }

    public AnimationClip SecondaryReleaseClip
    {
        get
        {
            return secondaryReleaseClip;
        }
    }
    #endregion
}
