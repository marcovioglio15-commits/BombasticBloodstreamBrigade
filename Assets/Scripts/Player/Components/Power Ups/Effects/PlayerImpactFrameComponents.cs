using Unity.Entities;
using Unity.Mathematics;

#region Impact Frame State
/// <summary>
/// Holds runtime state for active Impact Frame time-scale and presentation effects.
/// </summary>
public struct PlayerImpactFrameState : IComponentData
{
    #region Fields
    public byte IsActive;
    public byte Phase;
    public byte HasFrameLimit;
    public byte HasSecondLimit;
    public int RemainingFrames;
    public float RemainingUnscaledSeconds;
    public float ReferenceFrameRate;
    public float EaseInUnscaledSeconds;
    public float EaseOutUnscaledSeconds;
    public ImpactFrameEasingMode EasingMode;
    public ImpactFrameEffectConfig Effect;
    public float TotalDurationUnscaledSeconds;
    public float EffectElapsedUnscaledSeconds;
    public float3 EffectOriginWorldPosition;
    public byte HasWorldOrigin;
    public float PhaseElapsedUnscaledSeconds;
    public float CurrentBlend;
    #endregion
}

/// <summary>
/// Holds charge-driven Impact Frame build-in state independently from the final impact timeline so both profiles can
/// overlap during the rapid release transition.
/// </summary>
public struct PlayerImpactFrameBuildInState : IComponentData
{
    #region Fields
    public byte IsActive;
    public byte RequestedThisFrame;
    public byte IsReleasing;
    public float RequestedBlend;
    public float CurrentBlend;
    public float ReleaseStartBlend;
    public float ReleaseElapsedUnscaledSeconds;
    public float ReleaseUnscaledSeconds;
    public ImpactFrameEasingMode EasingMode;
    public ImpactFrameEffectConfig Effect;
    #endregion
}
#endregion
