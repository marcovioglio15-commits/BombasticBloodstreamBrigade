using Unity.Entities;

#region Random Stat Growth Runtime State
/// <summary>
/// Tracks permanent native-stat growth and the rebuild version to which it was applied.
/// </summary>
public struct PlayerRandomStatGrowthState : IComponentData
{
    public uint Version;
    public uint LastAppliedVersion;
    public uint ActivationSequence;
    public uint LastScalingApplyVersion;
}

/// <summary>
/// Stores one accumulated native-stat increase independently from rebuilt scalable configurations.
/// </summary>
[InternalBufferCapacity(4)]
public struct PlayerRandomStatGrowthModifierElement : IBufferElementData
{
    public PlayerRandomStatGrowthTarget Target;
    public float TotalIncrease;
    public float AppliedIncrease;
}
#endregion
