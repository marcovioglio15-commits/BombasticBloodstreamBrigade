using Unity.Mathematics;

/// <summary>
/// Stores one currently active predictive warning window resolved for a single offensive config.
/// </summary>
internal struct EnemyOffensiveEngagementWindow
{
    public float NormalizedProgress;
    public float ElapsedSeconds;
}

/// <summary>
/// Stores the strongest currently active offensive color-blend result.
/// </summary>
internal struct EnemyOffensiveEngagementBlendResult
{
    public bool IsActive;
    public float Blend;
    public float4 Color;
    public float FadeOutSeconds;
}

/// <summary>
/// Stores the strongest currently active offensive billboard result.
/// </summary>
internal struct EnemyOffensiveEngagementBillboardResult
{
    public bool IsActive;
    public EnemyOffensiveEngagementTriggerSource Source;
    public int VisualSettingsKey;
    public bool UseOverrideVisualSettings;
    public float4 Color;
    public float3 Offset;
    public float UniformScale;
}
