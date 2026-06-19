using Unity.Entities;
using Unity.Collections;

#region Components
/// <summary>
/// Stores runtime player health values.
/// </summary>
public struct PlayerHealth : IComponentData
{
    public float Current;
    public float Max;
}

/// <summary>
/// Stores runtime player shield values.
/// </summary>
public struct PlayerShield : IComponentData
{
    public float Current;
    public float Max;
}

/// <summary>
/// Stores the current player damage-grace window end time.
/// </summary>
public struct PlayerDamageGraceState : IComponentData
{
    public float IgnoreDamageUntilTime;
}

/// <summary>
/// Stores runtime player experience value.
/// </summary>
public struct PlayerExperience : IComponentData
{
    public float Current;
}

/// <summary>
/// Stores runtime level progression state for the player.
/// </summary>
public struct PlayerLevel : IComponentData
{
    public int Current;
    public int ActiveGamePhaseIndex;
    public float RequiredExperienceForNextLevel;
}

/// <summary>
/// Stores player experience drop attraction radius used for collection.
/// </summary>
public struct PlayerExperienceCollection : IComponentData
{
    public float PickupRadius;
}

/// <summary>
/// Marks a newly initialized player whose spawn audio must wait for scene transition loading to finish.
/// </summary>
public struct PlayerSpawnAudioPending : IComponentData
{
}

/// <summary>
/// Stores one runtime scalable stat value keyed by name.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerScalableStatElement : IBufferElementData
{
    public FixedString64Bytes Name;
    public byte Type;
    public float MinimumValue;
    public float MaximumValue;
    public float Value;
    public byte BooleanValue;
    public FixedString64Bytes TokenValue;
}
#endregion
