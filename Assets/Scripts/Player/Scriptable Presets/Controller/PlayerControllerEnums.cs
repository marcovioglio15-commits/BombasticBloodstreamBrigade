using System;

#region Enums
[Serializable]
public enum ReferenceFrame
{
    PlayerForward = 0,
    WorldForward = 1,
    CameraForward = 2
}

[Serializable]
public enum MovementDirectionsMode
{
    AllDirections = 0,
    DiscreteCount = 1
}

[Serializable]
public enum LookDirectionsMode
{
    AllDirections = 0,
    DiscreteCount = 1,
    Cones = 2,
    FollowMovementDirection = 3
}

[Serializable]
public enum RotationMode
{
    SnapToAllowedDirections = 0,
    Continuous = 1
}

[Serializable]
public enum LookMultiplierSampling
{
    DirectionalBlend = 0,
    ArcConstant = 1
}

[Serializable]
public enum CameraBehavior
{
    ChildOfPlayer = 0,
    FollowWithOffset = 1,
    FollowWithAutoOffset = 2,
    RoomFixed = 3
}

[Serializable]
public enum CameraShakeFalloff : byte
{
    Linear = 0,
    Smooth = 1,
    Quadratic = 2
}

[Serializable]
public enum ShootingTriggerMode
{
    AutomaticToggle = 0,
    ManualSingleShot = 1,
    ManualContinousShot = 2
}

[Serializable]
public enum PlayerProjectileAppliedElement
{
    None = 0,
    Fire = 1,
    Ice = 2,
    Poison = 3,
    Custom = 4
}

[Serializable]
public enum PlayerMaxStatAdjustmentMode : byte
{
    KeepCurrentValue = 0,
    KeepPercentage = 1,
    AddDeltaToCurrent = 2
}

[Serializable]
public enum ProjectileKnockbackDirectionMode : byte
{
    ProjectileTravel = 0,
    HitToTarget = 1
}

[Serializable]
public enum ProjectileKnockbackStackingMode : byte
{
    Replace = 0,
    Add = 1,
    KeepStrongest = 2
}
#endregion
