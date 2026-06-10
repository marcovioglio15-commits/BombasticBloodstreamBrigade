using Unity.Entities;
using Unity.Mathematics;

#region Ghost Trail State
/// <summary>
/// Holds the active Ghost Trail timeline and the resolved snapshot/presentation configuration.
/// </summary>
public struct PlayerGhostTrailState : IComponentData
{
    #region Fields
    public byte IsActive;
    public byte Phase;
    public byte MatchToggleActivationDuration;
    public byte MatchedToggleSlotIndex;
    public float RemainingDurationSeconds;
    public float EaseInUnscaledSeconds;
    public float EaseOutUnscaledSeconds;
    public float PhaseElapsedUnscaledSeconds;
    public float EffectElapsedUnscaledSeconds;
    public float TotalDurationUnscaledSeconds;
    public float CurrentBlend;
    public GhostTrailPowerUpConfig Config;
    #endregion
}
#endregion
