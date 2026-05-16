using Unity.Entities;

/// <summary>
/// Enumerates the terminal outcome of the current player run.
/// None.
/// </summary>
public enum PlayerRunOutcome : byte
{
    None = 0,
    Victory = 1,
    Defeat = 2
}

/// <summary>
/// Stores the authoritative end-of-run result for the local player entity.
/// Runtime UI reads this state to display ending screens without reloading the scene immediately.
/// None.
/// </summary>
public struct PlayerRunOutcomeState : IComponentData
{
    #region Fields
    public PlayerRunOutcome Outcome;
    public byte IsFinalized;
    public byte RuntimeFreezeApplied;
    #endregion
}
