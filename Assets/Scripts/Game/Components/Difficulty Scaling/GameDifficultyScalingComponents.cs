using Unity.Collections;
using Unity.Entities;

#region Baked Configuration
/// <summary>
/// Stores immutable metadata and context requirements for the active difficulty graph.
/// </summary>
public struct GameDifficultyScalingConfig : IComponentData
{
    public FixedString64Bytes PresetId;
    public int CoefficientCount;
    public byte UsesElapsedRunTime;
}

/// <summary>
/// Stores one flattened dependency-ordered coefficient definition.
/// </summary>
[InternalBufferCapacity(0)]
public struct GameDifficultyCoefficientDefinitionElement : IBufferElementData
{
    public FixedString64Bytes CoefficientId;
    public FixedString128Bytes DisplayName;
    public FixedString512Bytes Formula;
    public FixedString64Bytes CurveInputVariable;
    public GameDifficultyScalingMode ScalingMode;
    public float DefaultValue;
    public float MinimumValue;
    public float MaximumValue;
    public int FirstCurveSampleIndex;
    public int CurveSampleCount;
    public int FirstStepIndex;
    public int StepCount;
    public byte DebugInConsole;
}

/// <summary>
/// Stores one sampled curve point belonging to a flattened difficulty coefficient.
/// </summary>
[InternalBufferCapacity(0)]
public struct GameDifficultyCurveSampleElement : IBufferElementData
{
    public float Input;
    public float Output;
}

/// <summary>
/// Stores one flattened quantized step and its contiguous condition slice.
/// </summary>
[InternalBufferCapacity(0)]
public struct GameDifficultyStepElement : IBufferElementData
{
    public GameDifficultyConditionCombination ConditionCombination;
    public float OutputValue;
    public int FirstConditionIndex;
    public int ConditionCount;
}

/// <summary>
/// Stores one flattened numeric comparison used by a quantized difficulty step.
/// </summary>
[InternalBufferCapacity(0)]
public struct GameDifficultyStepConditionElement : IBufferElementData
{
    public FixedString64Bytes VariableName;
    public GameDifficultyComparison Comparison;
    public float Threshold;
}
#endregion

#region Runtime State
/// <summary>
/// Tracks the last evaluated source context and monotonic coefficient version.
/// </summary>
public struct GameDifficultyRuntimeState : IComponentData
{
    public uint SourceHash;
    public uint Version;
    public float RunStartTime;
    public byte Initialized;
}

/// <summary>
/// Stores one current authoritative difficulty coefficient value.
/// </summary>
[InternalBufferCapacity(0)]
public struct GameDifficultyCoefficientValueElement : IBufferElementData
{
    public FixedString64Bytes CoefficientId;
    public float Value;
}
#endregion
