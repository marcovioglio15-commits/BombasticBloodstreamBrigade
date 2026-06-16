using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Runtime hash configuration for animator parameters consumed by ECS animation sync.
/// </summary>
public struct PlayerAnimatorParameterConfig : IComponentData
{
    #region Fields
    public int MoveXHash;
    public int MoveYHash;
    public int MoveSpeedHash;
    public int AimXHash;
    public int AimYHash;
    public int IsMovingHash;
    public int IsShootingHash;
    public int IsDashingHash;
    public int ShotPulseHash;
    public int ProceduralRecoilHash;
    public int ProceduralAimWeightHash;
    public int ProceduralLeanHash;

    public float FloatDampTime;
    public float MovingSpeedThreshold;
    public float ProceduralRecoilKick;
    public float ProceduralRecoilRecoveryPerSecond;
    public float ProceduralAimWeightSmoothing;
    public float ProceduralLeanSmoothing;

    public byte UseFloatDamping;
    public byte DisableRootMotion;
    public byte ProceduralRecoilEnabled;
    public byte ProceduralAimWeightEnabled;
    public byte ProceduralLeanEnabled;

    public byte HasMoveX;
    public byte HasMoveY;
    public byte HasMoveSpeed;
    public byte HasAimX;
    public byte HasAimY;
    public byte HasIsMoving;
    public byte HasIsShooting;
    public byte HasIsDashing;
    public byte HasShotPulse;
    public byte HasProceduralRecoil;
    public byte HasProceduralAimWeight;
    public byte HasProceduralLean;
    #endregion
}

/// <summary>
/// Runtime animation bridge state used to detect one-frame transitions (e.g. shoot pulses).
/// </summary>
public struct PlayerAnimatorRuntimeState : IComponentData
{
    #region Fields
    public byte PreviousShooting;
    public byte PreviousPrimaryCharging;
    public byte PreviousSecondaryCharging;
    public PlayerUpperBodyAnimationActionKind UpperBodyActionKind;
    public byte UpperBodyActionActive;
    public byte Initialized;
    public byte ParametersValidated;
    public uint LastShotPulseVersion;
    public int BoundAnimatorInstanceId;
    public float UpperBodyActionElapsed;
    public float UpperBodyActionDuration;
    public float RecoilValue;
    public float AimWeightValue;
    public float LeanValue;
    public float LastMoveX;
    public float LastMoveY;
    #endregion
}

/// <summary>
/// Identifies the upper-body action currently driven by presentation without transferring gameplay authority to Animator.
/// </summary>
public enum PlayerUpperBodyAnimationActionKind : byte
{
    None = 0,
    Shoot = 1,
    Charge = 2,
    Release = 3
}

/// <summary>
/// Stores the upper-body idle clip, the implicit Base Gun shoot clip, and the charge / release clips selected
/// by scalable power-up payload selectors. Per-weapon shoot clips live on
/// <see cref="PlayerAdditionalWeaponVisualElement"/> buffer entries so each weapon ID can carry its own clip end-to-end.
/// </summary>
public struct PlayerUpperBodyAnimationClipConfig : IComponentData
{
    #region Fields
    public UnityObjectRef<AnimationClip> UpperBodyIdle;
    public UnityObjectRef<AnimationClip> DefaultShoot;
    public UnityObjectRef<AnimationClip> PrimaryCharge;
    public UnityObjectRef<AnimationClip> SecondaryCharge;
    public UnityObjectRef<AnimationClip> PrimaryRelease;
    public UnityObjectRef<AnimationClip> SecondaryRelease;
    #endregion
}

/// <summary>
/// Runtime configuration used to spawn and sync an external GameObject visual bridge when no companion Animator
/// is available. Per-weapon references live on <see cref="PlayerAdditionalWeaponVisualElement"/> buffer entries
/// so the bridge layout stays flexible across weapon IDs.
/// </summary>
public struct PlayerVisualRuntimeBridgeConfig : IComponentData
{
    #region Fields
    public UnityObjectRef<GameObject> VisualPrefab;
    public float3 PositionOffset;
    public FixedString128Bytes BaseGunReference;
    public FixedString64Bytes DefaultAdditionalWeaponId;
    public byte SyncRotation;
    public byte SpawnWhenAnimatorMissing;
    #endregion
}

/// <summary>
/// Runtime mountable-weapon entry baked from <see cref="PlayerWeaponVisualSettings.AdditionalWeapons"/>. Each
/// entry binds one Weapon Id to its prefab-relative runtime reference and the upper-body shoot
/// clip used while the weapon is the visible attachment.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerAdditionalWeaponVisualElement : IBufferElementData
{
    #region Fields
    public FixedString64Bytes WeaponId;
    public FixedString128Bytes RuntimeReference;
    public UnityObjectRef<AnimationClip> ShootAnimationClip;
    #endregion
}

/// <summary>
/// Runtime reference to the visual Animator driven by ECS gameplay state.
/// </summary>
public struct PlayerAnimatorObjectReference : IComponentData
{
    #region Fields
    public UnityObjectRef<Animator> Animator;
    #endregion
}

/// <summary>
/// Optional runtime animator controller fallback used to recover companion animators with missing controller bindings.
/// </summary>
public struct PlayerAnimatorControllerReference : IComponentData
{
    #region Fields
    public UnityObjectRef<RuntimeAnimatorController> Controller;
    #endregion
}

/// <summary>
/// Optional humanoid avatar fallback used to recover companion animators with missing avatar bindings.
/// </summary>
public struct PlayerAnimatorAvatarReference : IComponentData
{
    #region Fields
    public UnityObjectRef<Avatar> Avatar;
    #endregion
}
