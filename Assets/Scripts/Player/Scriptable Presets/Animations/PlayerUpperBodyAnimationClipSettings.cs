using System;
using UnityEngine;

/// <summary>
/// Stores concrete upper-body action clips selected by scalable modular power-up payload enums.
/// </summary>
[Serializable]
public sealed class PlayerUpperBodyAnimationClipSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Upper-body shooting clip used when a Switch Weapon payload selects the Cannon animation slot.")]
    [SerializeField]
    private AnimationClip cannonShootClip;

    [Tooltip("Upper-body shooting clip used when a Switch Weapon payload selects the Gatling animation slot.")]
    [SerializeField]
    private AnimationClip gatlingShootClip;

    [Tooltip("Upper-body shooting clip used when a Switch Weapon payload selects the Railgun animation slot.")]
    [SerializeField]
    private AnimationClip railgunShootClip;

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
    public AnimationClip CannonShootClip
    {
        get
        {
            return cannonShootClip;
        }
    }

    public AnimationClip GatlingShootClip
    {
        get
        {
            return gatlingShootClip;
        }
    }

    public AnimationClip RailgunShootClip
    {
        get
        {
            return railgunShootClip;
        }
    }

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
