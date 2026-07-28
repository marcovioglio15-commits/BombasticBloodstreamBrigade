/// <summary>
/// Identifies the gameplay domain modified by a room-clear reward module.
/// </summary>
public enum GameRoomRewardTargetDomain : byte
{
    ScalableStat = 0,
    Resource = 1
}

/// <summary>
/// Identifies how a room-clear reward module obtains its applied value.
/// </summary>
public enum GameRoomRewardValueSource : byte
{
    Formula = 0,
    Flat = 1
}

/// <summary>
/// Identifies whether a room-clear reward is permanent or scoped to future room visits.
/// </summary>
public enum GameRoomRewardDuration : byte
{
    Permanent = 0,
    Temporary = 1
}

/// <summary>
/// Identifies a player resource supported by room-clear rewards.
/// </summary>
public enum GameRoomRewardResource : byte
{
    Health = 0,
    PrimaryPowerUpEnergy = 1,
    SecondaryPowerUpEnergy = 2,
    Experience = 3
}

/// <summary>
/// Identifies the eight -facing reward categories produced by the three module axes.
/// </summary>
public enum GameRoomRewardModuleCategory : byte
{
    PermanentStatFormula = 0,
    PermanentStatFlat = 1,
    PermanentResourceFormula = 2,
    PermanentResourceFlat = 3,
    TemporaryStatFormula = 4,
    TemporaryStatFlat = 5,
    TemporaryResourceFormula = 6,
    TemporaryResourceFlat = 7
}

/// <summary>
/// Identifies the ordered menu group used to organize composed room rewards.
/// </summary>
public enum GameRoomRewardMenuGroup : byte
{
    PermanentStats = 0,
    PermanentResources = 1,
    TemporarySubtype1 = 2,
    TemporarySubtype2 = 3,
    TemporarySubtype3 = 4,
    TemporarySubtype4 = 5
}

/// <summary>
/// Identifies how one reward target is represented by player and portal presentation views.
/// </summary>
public enum GameRoomRewardPresentationMode : byte
{
    ColoredText = 0,
    Sprite = 1
}
