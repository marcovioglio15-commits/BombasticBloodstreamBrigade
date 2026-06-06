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
/// Stores the concrete upper-body clip assets referenced by scalable power-up payload selectors.
/// </summary>
public struct PlayerUpperBodyAnimationClipConfig : IComponentData
{
    #region Fields
    public UnityObjectRef<AnimationClip> DefaultShoot;
    public UnityObjectRef<AnimationClip> CannonShoot;
    public UnityObjectRef<AnimationClip> GatlingShoot;
    public UnityObjectRef<AnimationClip> RailgunShoot;
    public UnityObjectRef<AnimationClip> PrimaryCharge;
    public UnityObjectRef<AnimationClip> SecondaryCharge;
    public UnityObjectRef<AnimationClip> PrimaryRelease;
    public UnityObjectRef<AnimationClip> SecondaryRelease;
    #endregion
}

/// <summary>
/// Runtime configuration used to spawn and sync an external GameObject visual bridge when no companion Animator is available.
/// </summary>
public struct PlayerVisualRuntimeBridgeConfig : IComponentData
{
    #region Fields
    public UnityObjectRef<GameObject> VisualPrefab;
    public float3 PositionOffset;
    public FixedString128Bytes BaseGunReference;
    public FixedString128Bytes CannonReference;
    public FixedString128Bytes GatlingReference;
    public FixedString128Bytes RailgunReference;
    public PlayerWeaponVisualSlot DefaultAdditionalWeaponVisual;
    public byte SyncRotation;
    public byte SpawnWhenAnimatorMissing;
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
