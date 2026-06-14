using Unity.Entities;

#region Timed Power-Up State
/// <summary>
/// Holds runtime state for the Bullet Time active tool.
/// </summary>
public struct PlayerBulletTimeState : IComponentData
{
    #region Fields
    public float TimedRemainingDuration;
    public float TimedSlowPercent;
    public float TimedTransitionTimeSeconds;
    public float ToggleSlowPercent;
    public float ToggleTransitionTimeSeconds;
    public float CurrentSlowPercent;
    public float TransitionStartSlowPercent;
    public float TransitionTargetSlowPercent;
    public float TransitionDurationSeconds;
    public float TransitionElapsedSeconds;
    #endregion
}

/// <summary>
/// Holds runtime state for heal-over-time effects triggered by power ups.
/// </summary>
public struct PlayerHealOverTimeState : IComponentData
{
    #region Fields
    public byte IsActive;
    public float HealPerSecond;
    public float RemainingTotalHeal;
    public float RemainingDuration;
    public float TickIntervalSeconds;
    public float TickTimer;
    #endregion
}
#endregion
