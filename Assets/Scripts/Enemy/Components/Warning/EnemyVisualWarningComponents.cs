using Unity.Entities;
using Unity.Mathematics;

#region Enemy Visual Warning Components
/// <summary>
/// Identifies the offensive interaction source that currently requests engagement feedback.
/// </summary>
public enum EnemyOffensiveEngagementTriggerSource : byte
{
    CoreMovement = 0,
    ShortRangeInteraction = 1,
    WeaponInteraction = 2,
    BossPatternChange = 3
}

/// <summary>
/// Identifies the runtime timing model used to predict one offensive engagement commit.
/// </summary>
public enum EnemyOffensiveEngagementTimingMode : byte
{
    None = 0,
    ShortRangeDashRelease = 1,
    WeaponShot = 2,
    ModuleActivation = 3
}

/// <summary>
/// Stores immutable offensive engagement feedback settings compiled for one active interaction slot.
/// </summary>
public struct EnemyOffensiveEngagementConfigElement : IBufferElementData
{
    public EnemyOffensiveEngagementTriggerSource Source;
    public EnemyOffensiveEngagementTimingMode TimingMode;
    public int VisualSettingsKey;
    public byte UseOverrideVisualSettings;
    public byte PreventWarningInterruption;
    public byte EnableColorBlend;
    public float4 ColorBlendColor;
    public float ColorBlendLeadTimeSeconds;
    public float ColorBlendFadeOutSeconds;
    public float ColorBlendMaximumBlend;
    public byte EnableBillboard;
    public float4 BillboardColor;
    public float3 BillboardOffset;
    public float BillboardLeadTimeSeconds;
    public float BillboardBaseScale;
    public float BillboardPulseScaleMultiplier;
    public float BillboardPulseExpandDurationSeconds;
    public float BillboardPulseContractDurationSeconds;
}

/// <summary>
/// Stores the last composed flash values applied to enemy renderers.
/// </summary>
public struct EnemyVisualFlashPresentationState : IComponentData
{
    public float AppliedBlend;
    public float4 AppliedColor;
    public float4 OffensiveEngagementColor;
    public float OffensiveEngagementBlend;
    public float OffensiveEngagementFadeOutSeconds;
    public byte HasProtectedEngagementSource;
    public EnemyOffensiveEngagementTriggerSource ProtectedEngagementSource;
}

/// <summary>
/// Stores immutable boss pattern-change preannounce feedback settings baked from the visual preset.
/// </summary>
public struct EnemyBossPatternChangeFeedbackConfig : IComponentData
{
    public byte Enabled;
    public byte EnableColorBlend;
    public float4 ColorBlendColor;
    public float ColorBlendDurationSeconds;
    public float ColorBlendFadeOutSeconds;
    public float ColorBlendMaximumBlend;
    public byte EnableBillboard;
    public float4 BillboardColor;
    public float3 BillboardOffset;
    public float BillboardDurationSeconds;
    public float BillboardBaseScale;
    public float BillboardPulseScaleMultiplier;
    public float BillboardPulseExpandDurationSeconds;
    public float BillboardPulseContractDurationSeconds;
}

/// <summary>
/// Tracks the currently visible boss pattern-change preannounce window.
/// </summary>
public struct EnemyBossPatternChangeFeedbackState : IComponentData
{
    public float ElapsedSeconds;
    public float RemainingSeconds;
    public float DisplayedBlend;
    public float4 DisplayedColor;
    public float FadeOutSeconds;
}
#endregion
