#region Generation Enums
/// <summary>
/// Selects how the procedural level generator obtains the authoritative run seed.
/// </summary>
public enum GameProceduralLevelSeedMode : byte
{
    RandomPerRun = 0,
    Fixed = 1,
    External = 2
}

/// <summary>
/// Defines the structural role assigned to one reusable room tile.
/// </summary>
public enum GameProceduralRoomRole : byte
{
    Start = 0,
    Regular = 1,
    Boss = 2
}
#endregion

#region Portal Enums
/// <summary>
/// Identifies the logical side containing one authored room portal.
/// </summary>
public enum GameRoomPortalSide : byte
{
    North = 0,
    South = 1,
    East = 2,
    West = 3
}

/// <summary>
/// Defines whether an authored room portal can receive, emit or support either graph edge role.
/// </summary>
public enum GameRoomPortalCapability : byte
{
    Entrance = 0,
    Exit = 1,
    Both = 2
}

/// <summary>
/// Defines whether an exit must be connected, may be sealed or advances to the next level.
/// </summary>
public enum GameRoomPortalConnectionPolicy : byte
{
    Required = 0,
    Optional = 1,
    LevelExit = 2
}
#endregion
