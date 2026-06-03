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
/// Stores the authoritative end-of-run result for the local player entity. Defeat runs go through a transient "dying"
/// phase: input is frozen immediately so the player cannot act from the dead state, but the camera shake, damage flash
/// and vignette feedback systems keep evolving so the lethal hit reads as one final tactile beat before the end-of-run
/// screen appears. Once the configured defeat playback window elapses the runtime sets <see cref="IsFinalized"/> and
/// the existing freeze/end-of-run UI flow picks up exactly like before.
/// </summary>
public struct PlayerRunOutcomeState : IComponentData
{
    #region Fields
    public PlayerRunOutcome Outcome;

    // Set the first frame defeat is detected and stays 1 until the dying playback window has elapsed and IsFinalized
    // takes over. Victory transitions skip the dying phase and set IsFinalized directly, leaving this byte at 0.
    public byte IsDying;

    // Seconds spent in the dying playback window, advanced with scaled delta time so a pause naturally freezes the
    // sequence and resumes from where it stopped.
    public float DyingElapsedSeconds;

    public byte IsFinalized;
    public byte RuntimeFreezeApplied;

    // Set the first frame the dying input/movement freeze runs so the freeze system only resets state once even though
    // it must keep firing every frame to assert the no-input contract for the rest of the run.
    public byte DyingFreezeApplied;
    #endregion
}
