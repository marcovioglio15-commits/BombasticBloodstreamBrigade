using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "PlayerAnimationBindingsPreset", menuName = "Player/Animation Bindings Preset", order = 14)]
public sealed class PlayerAnimationBindingsPreset : ScriptableObject
{
    #region Constants
    private const float DefaultFloatDampTime = 0.08f;
    private const float DefaultMovingSpeedThreshold = 0.02f;
    private const string DefaultMoveXParameter = "MoveX";
    private const string DefaultMoveYParameter = "MoveY";
    private const string DefaultMoveSpeedParameter = "MoveSpeed";
    private const string DefaultAimXParameter = "AimX";
    private const string DefaultAimYParameter = "AimY";
    private const string DefaultIsMovingParameter = "IsMoving";
    private const string DefaultIsShootingParameter = "IsShooting";
    private const string DefaultIsDashingParameter = "IsDashing";
    private const string DefaultShotPulseParameter = "ShotPulse";
    private const string DefaultProceduralRecoilParameter = "ProcRecoil";
    private const string DefaultProceduralAimWeightParameter = "ProcAimWeight";
    private const string DefaultProceduralLeanParameter = "ProcLean";
    #endregion

    #region Fields

    #region Serialized Fields

    #region Metadata
    [Header("Metadata")]
    [Tooltip("Unique ID for this animation bindings preset, used for stable references.")]
    [FormerlySerializedAs("m_PresetId")]
    [SerializeField]
    private string presetId;

    [Tooltip("Animation bindings preset name.")]
    [FormerlySerializedAs("m_PresetName")]
    [SerializeField]
    private string presetName = "New Animation Bindings Preset";

    [Tooltip("Short description of the animation bindings preset use case.")]
    [FormerlySerializedAs("m_Description")]
    [SerializeField]
    private string description;

    [Tooltip("Optional semantic version string for this animation bindings preset.")]
    [FormerlySerializedAs("m_Version")]
    [SerializeField]
    private string version = "1.0.0";

    [Header("Scaling")]
    [Tooltip("Optional formula-based scaling rules applied to numeric animation binding properties during bake.")]
    [SerializeField]
    private List<PlayerStatScalingRule> scalingRules = new List<PlayerStatScalingRule>();
    #endregion

    #region Animator Setup
    [Header("Animator Setup")]
    [Tooltip("Runtime Animator Controller assigned to the visual Animator.")]
    [FormerlySerializedAs("m_AnimatorController")]
    [SerializeField]
    private RuntimeAnimatorController animatorController;

    [Tooltip("Optional upper-body avatar mask reference used by the animator setup.")]
    [FormerlySerializedAs("m_UpperBodyAvatarMask")]
    [SerializeField]
    private AvatarMask upperBodyAvatarMask;

    [Tooltip("Optional lower-body avatar mask reference used by the animator setup.")]
    [FormerlySerializedAs("m_LowerBodyAvatarMask")]
    [SerializeField]
    private AvatarMask lowerBodyAvatarMask;

    [Tooltip("Force-disable root motion from runtime sync to keep ECS movement authoritative.")]
    [FormerlySerializedAs("m_DisableRootMotion")]
    [SerializeField]
    private bool disableRootMotion = true;

    [Tooltip("Use Animator float damping when writing directional and speed parameters.")]
    [FormerlySerializedAs("m_UseFloatDamping")]
    [SerializeField]
    private bool useFloatDamping = true;

    [Tooltip("Damping time used by Animator.SetFloat(..., dampTime, deltaTime).")]
    [FormerlySerializedAs("m_FloatDampTime")]
    [SerializeField]
    private float floatDampTime = DefaultFloatDampTime;

    [Tooltip("Speed threshold used to set IsMoving parameter.")]
    [FormerlySerializedAs("m_MovingSpeedThreshold")]
    [SerializeField]
    private float movingSpeedThreshold = DefaultMovingSpeedThreshold;
    #endregion

    #region Animator Parameters
    [Header("Animator Parameters")]
    [Tooltip("Animator float parameter name for local movement right-axis value.")]
    [FormerlySerializedAs("m_MoveXParameter")]
    [SerializeField]
    private string moveXParameter = DefaultMoveXParameter;

    [Tooltip("Animator float parameter name for local movement forward-axis value.")]
    [FormerlySerializedAs("m_MoveYParameter")]
    [SerializeField]
    private string moveYParameter = DefaultMoveYParameter;

    [Tooltip("Animator float parameter name for current movement speed.")]
    [FormerlySerializedAs("m_MoveSpeedParameter")]
    [SerializeField]
    private string moveSpeedParameter = DefaultMoveSpeedParameter;

    [Tooltip("Animator float parameter name for local aim right-axis value.")]
    [FormerlySerializedAs("m_AimXParameter")]
    [SerializeField]
    private string aimXParameter = DefaultAimXParameter;

    [Tooltip("Animator float parameter name for local aim forward-axis value.")]
    [FormerlySerializedAs("m_AimYParameter")]
    [SerializeField]
    private string aimYParameter = DefaultAimYParameter;

    [Tooltip("Animator bool parameter name for movement state.")]
    [FormerlySerializedAs("m_IsMovingParameter")]
    [SerializeField]
    private string isMovingParameter = DefaultIsMovingParameter;

    [Tooltip("Animator bool parameter name for shooting hold state.")]
    [FormerlySerializedAs("m_IsShootingParameter")]
    [SerializeField]
    private string isShootingParameter = DefaultIsShootingParameter;

    [Tooltip("Animator bool parameter name for dash state.")]
    [FormerlySerializedAs("m_IsDashingParameter")]
    [SerializeField]
    private string isDashingParameter = DefaultIsDashingParameter;

    [Tooltip("Animator trigger parameter name used for one-shot firing pulse.")]
    [FormerlySerializedAs("m_ShotPulseParameter")]
    [SerializeField]
    private string shotPulseParameter = DefaultShotPulseParameter;
    #endregion

    #region Procedural Animation
    [Header("Procedural Animation")]
    [Tooltip("Enable procedural recoil driver values from ECS into Animator parameters.")]
    [SerializeField]
    private bool proceduralRecoilEnabled = true;

    [Tooltip("Recoil kick added on each shoot edge. Recommended range 0.05 - 0.35.")]
    [SerializeField]
    private float proceduralRecoilKick = 0.2f;

    [Tooltip("Recoil recovery speed per second. Higher values recover faster.")]
    [SerializeField]
    private float proceduralRecoilRecoveryPerSecond = 7f;

    [Tooltip("Animator float parameter name that receives procedural recoil value.")]
    [SerializeField]
    private string proceduralRecoilParameter = DefaultProceduralRecoilParameter;

    [Tooltip("Enable procedural aim rig weight smoothing from ECS look state.")]
    [SerializeField]
    private bool proceduralAimWeightEnabled;

    [Tooltip("Smoothing speed used for procedural aim rig weight changes.")]
    [SerializeField]
    private float proceduralAimWeightSmoothing = 12f;

    [Tooltip("Animator float parameter name that receives procedural aim rig weight.")]
    [SerializeField]
    private string proceduralAimWeightParameter = DefaultProceduralAimWeightParameter;

    [Tooltip("Enable procedural lean from local movement X.")]
    [SerializeField]
    private bool proceduralLeanEnabled;

    [Tooltip("Smoothing speed used for procedural lean value changes.")]
    [SerializeField]
    private float proceduralLeanSmoothing = 10f;

    [Tooltip("Animator float parameter name that receives procedural lean value.")]
    [SerializeField]
    private string proceduralLeanParameter = DefaultProceduralLeanParameter;
    #endregion

    #region Clip Slots
    [Header("Clip Slots")]
    [Tooltip("Default idle clip used by locomotion state machine.")]
    [FormerlySerializedAs("m_IdleClip")]
    [SerializeField]
    private AnimationClip idleClip;

    [Tooltip("Forward movement clip mapped by tooling for locomotion blend trees.")]
    [FormerlySerializedAs("m_MoveForwardClip")]
    [SerializeField]
    private AnimationClip moveForwardClip;

    [Tooltip("Backward movement clip mapped by tooling for locomotion blend trees.")]
    [FormerlySerializedAs("m_MoveBackwardClip")]
    [SerializeField]
    private AnimationClip moveBackwardClip;

    [Tooltip("Left movement clip mapped by tooling for locomotion blend trees.")]
    [FormerlySerializedAs("m_MoveLeftClip")]
    [SerializeField]
    private AnimationClip moveLeftClip;

    [Tooltip("Right movement clip mapped by tooling for locomotion blend trees.")]
    [FormerlySerializedAs("m_MoveRightClip")]
    [SerializeField]
    private AnimationClip moveRightClip;

    [Tooltip("Forward aim clip mapped by tooling for upper-body blend trees.")]
    [FormerlySerializedAs("m_AimForwardClip")]
    [SerializeField]
    private AnimationClip aimForwardClip;

    [Tooltip("Backward aim clip mapped by tooling for upper-body blend trees.")]
    [FormerlySerializedAs("m_AimBackwardClip")]
    [SerializeField]
    private AnimationClip aimBackwardClip;

    [Tooltip("Left aim clip mapped by tooling for upper-body blend trees.")]
    [FormerlySerializedAs("m_AimLeftClip")]
    [SerializeField]
    private AnimationClip aimLeftClip;

    [Tooltip("Right aim clip mapped by tooling for upper-body blend trees.")]
    [FormerlySerializedAs("m_AimRightClip")]
    [SerializeField]
    private AnimationClip aimRightClip;

    [Tooltip("Concrete upper-body shooting, charge, and release clips selected by scalable modular power-up payload enums.")]
    [SerializeField]
    private PlayerUpperBodyAnimationClipSettings upperBodyActionClips = new PlayerUpperBodyAnimationClipSettings();

    [Tooltip("Dash clip slot used for dash transitions.")]
    [FormerlySerializedAs("m_DashClip")]
    [SerializeField]
    private AnimationClip dashClip;
    #endregion

    #endregion

    #endregion

    #region Properties

    #region Metadata
    public string PresetId
    {
        get
        {
            return presetId;
        }
    }

    public string PresetName
    {
        get
        {
            return presetName;
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public string Version
    {
        get
        {
            return version;
        }
    }

    public IReadOnlyList<PlayerStatScalingRule> ScalingRules
    {
        get
        {
            return scalingRules;
        }
    }
    #endregion

    #region Animator Setup
    public RuntimeAnimatorController AnimatorController
    {
        get
        {
            return animatorController;
        }
    }

    public AvatarMask UpperBodyAvatarMask
    {
        get
        {
            return upperBodyAvatarMask;
        }
    }

    public AvatarMask LowerBodyAvatarMask
    {
        get
        {
            return lowerBodyAvatarMask;
        }
    }

    public bool DisableRootMotion
    {
        get
        {
            return disableRootMotion;
        }
    }

    public bool UseFloatDamping
    {
        get
        {
            return useFloatDamping;
        }
    }

    public float FloatDampTime
    {
        get
        {
            return floatDampTime;
        }
    }

    public float MovingSpeedThreshold
    {
        get
        {
            return movingSpeedThreshold;
        }
    }
    #endregion

    #region Animator Parameters
    public string MoveXParameter
    {
        get
        {
            return moveXParameter;
        }
    }

    public string MoveYParameter
    {
        get
        {
            return moveYParameter;
        }
    }

    public string MoveSpeedParameter
    {
        get
        {
            return moveSpeedParameter;
        }
    }

    public string AimXParameter
    {
        get
        {
            return aimXParameter;
        }
    }

    public string AimYParameter
    {
        get
        {
            return aimYParameter;
        }
    }

    public string IsMovingParameter
    {
        get
        {
            return isMovingParameter;
        }
    }

    public string IsShootingParameter
    {
        get
        {
            return isShootingParameter;
        }
    }

    public string IsDashingParameter
    {
        get
        {
            return isDashingParameter;
        }
    }

    public string ShotPulseParameter
    {
        get
        {
            return shotPulseParameter;
        }
    }
    #endregion

    #region Procedural Animation
    public bool ProceduralRecoilEnabled
    {
        get
        {
            return proceduralRecoilEnabled;
        }
    }

    public float ProceduralRecoilKick
    {
        get
        {
            return proceduralRecoilKick;
        }
    }

    public float ProceduralRecoilRecoveryPerSecond
    {
        get
        {
            return proceduralRecoilRecoveryPerSecond;
        }
    }

    public string ProceduralRecoilParameter
    {
        get
        {
            return proceduralRecoilParameter;
        }
    }

    public bool ProceduralAimWeightEnabled
    {
        get
        {
            return proceduralAimWeightEnabled;
        }
    }

    public float ProceduralAimWeightSmoothing
    {
        get
        {
            return proceduralAimWeightSmoothing;
        }
    }

    public string ProceduralAimWeightParameter
    {
        get
        {
            return proceduralAimWeightParameter;
        }
    }

    public bool ProceduralLeanEnabled
    {
        get
        {
            return proceduralLeanEnabled;
        }
    }

    public float ProceduralLeanSmoothing
    {
        get
        {
            return proceduralLeanSmoothing;
        }
    }

    public string ProceduralLeanParameter
    {
        get
        {
            return proceduralLeanParameter;
        }
    }
    #endregion

    #region Clip Slots
    public AnimationClip IdleClip
    {
        get
        {
            return idleClip;
        }
    }

    public AnimationClip MoveForwardClip
    {
        get
        {
            return moveForwardClip;
        }
    }

    public AnimationClip MoveBackwardClip
    {
        get
        {
            return moveBackwardClip;
        }
    }

    public AnimationClip MoveLeftClip
    {
        get
        {
            return moveLeftClip;
        }
    }

    public AnimationClip MoveRightClip
    {
        get
        {
            return moveRightClip;
        }
    }

    public AnimationClip AimForwardClip
    {
        get
        {
            return aimForwardClip;
        }
    }

    public AnimationClip AimBackwardClip
    {
        get
        {
            return aimBackwardClip;
        }
    }

    public AnimationClip AimLeftClip
    {
        get
        {
            return aimLeftClip;
        }
    }

    public AnimationClip AimRightClip
    {
        get
        {
            return aimRightClip;
        }
    }

    public PlayerUpperBodyAnimationClipSettings UpperBodyActionClips
    {
        get
        {
            return upperBodyActionClips;
        }
    }

    public AnimationClip DashClip
    {
        get
        {
            return dashClip;
        }
    }
    #endregion

    #endregion

    #region Methods

    #region Clip Slots
    public void SetClip(PlayerAnimationClipSlot slot, AnimationClip clip)
    {
        switch (slot)
        {
            case PlayerAnimationClipSlot.Idle:
                idleClip = clip;
                return;
            case PlayerAnimationClipSlot.MoveForward:
                moveForwardClip = clip;
                return;
            case PlayerAnimationClipSlot.MoveBackward:
                moveBackwardClip = clip;
                return;
            case PlayerAnimationClipSlot.MoveLeft:
                moveLeftClip = clip;
                return;
            case PlayerAnimationClipSlot.MoveRight:
                moveRightClip = clip;
                return;
            case PlayerAnimationClipSlot.AimForward:
                aimForwardClip = clip;
                return;
            case PlayerAnimationClipSlot.AimBackward:
                aimBackwardClip = clip;
                return;
            case PlayerAnimationClipSlot.AimLeft:
                aimLeftClip = clip;
                return;
            case PlayerAnimationClipSlot.AimRight:
                aimRightClip = clip;
                return;
            case PlayerAnimationClipSlot.Dash:
                dashClip = clip;
                return;
        }
    }
    #endregion

    #endregion

    #region Unity Methods
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (scalingRules == null)
            scalingRules = new List<PlayerStatScalingRule>();

        if (upperBodyActionClips == null)
            upperBodyActionClips = new PlayerUpperBodyAnimationClipSettings();

        floatDampTime = Mathf.Max(0f, floatDampTime);
        movingSpeedThreshold = Mathf.Max(0f, movingSpeedThreshold);

        proceduralRecoilKick = Mathf.Max(0f, proceduralRecoilKick);
        proceduralRecoilRecoveryPerSecond = Mathf.Max(0f, proceduralRecoilRecoveryPerSecond);
        proceduralAimWeightSmoothing = Mathf.Max(0f, proceduralAimWeightSmoothing);
        proceduralLeanSmoothing = Mathf.Max(0f, proceduralLeanSmoothing);

        moveXParameter = PlayerAnimationBindingsPresetValidationUtility.TrimParameter(moveXParameter, DefaultMoveXParameter);
        moveYParameter = PlayerAnimationBindingsPresetValidationUtility.TrimParameter(moveYParameter, DefaultMoveYParameter);
        moveSpeedParameter = PlayerAnimationBindingsPresetValidationUtility.TrimParameter(moveSpeedParameter, DefaultMoveSpeedParameter);
        aimXParameter = PlayerAnimationBindingsPresetValidationUtility.TrimParameter(aimXParameter, DefaultAimXParameter);
        aimYParameter = PlayerAnimationBindingsPresetValidationUtility.TrimParameter(aimYParameter, DefaultAimYParameter);
        isMovingParameter = PlayerAnimationBindingsPresetValidationUtility.TrimParameter(isMovingParameter, DefaultIsMovingParameter);
        isShootingParameter = PlayerAnimationBindingsPresetValidationUtility.TrimParameter(isShootingParameter, DefaultIsShootingParameter);
        isDashingParameter = PlayerAnimationBindingsPresetValidationUtility.TrimParameter(isDashingParameter, DefaultIsDashingParameter);
        shotPulseParameter = PlayerAnimationBindingsPresetValidationUtility.TrimParameter(shotPulseParameter, DefaultShotPulseParameter);
        proceduralRecoilParameter = PlayerAnimationBindingsPresetValidationUtility.TrimParameter(proceduralRecoilParameter, DefaultProceduralRecoilParameter);
        proceduralAimWeightParameter = PlayerAnimationBindingsPresetValidationUtility.TrimParameter(proceduralAimWeightParameter, DefaultProceduralAimWeightParameter);
        proceduralLeanParameter = PlayerAnimationBindingsPresetValidationUtility.TrimParameter(proceduralLeanParameter, DefaultProceduralLeanParameter);
        PlayerAnimationBindingsPresetValidationUtility.ValidateScalingRules(scalingRules);
    }
    #endregion
}
