using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Stores baked player-side settings used to spawn and interact with dropped active power-up containers.
/// none.
/// returns none.
/// </summary>
public struct PlayerPowerUpContainerInteractionConfig : IComponentData
{
    public Entity ContainerPrefabEntity;
    public float InteractionRadius;
    public float OverlayPanelTimeScaleResumeDurationSeconds;
    public float ContainerGroundClearanceOffset;
    public PlayerPowerUpContainerInteractionMode InteractionMode;
    public PlayerPowerUpContainerStoredStateMode StoredStateMode;
    public float InteractionLockDuration;
    public float BaseInteractionLockDuration;
    public FixedString512Bytes InteractionLockDurationScalingFormula;
}

/// <summary>
/// Stores one active-slot payload serialized into a dropped world container.
/// none.
/// returns none.
/// </summary>
public struct PlayerStoredActivePowerUpData
{
    public PlayerPowerUpSlotConfig SlotConfig;
    public float StoredEnergy;
    public float StoredCooldownRemaining;
}

/// <summary>
/// Marks one dropped world entity and stores the active power-up currently available inside it.
/// none.
/// returns none.
/// </summary>
public struct PlayerDroppedPowerUpContainerContent : IComponentData
{
    public PlayerStoredActivePowerUpData StoredPowerUp;
}

/// <summary>
/// Queues one authoritative request to swap a dropped power-up container with one player active slot.
/// none.
/// returns none.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerPowerUpContainerSwapCommand : IBufferElementData
{
    public Entity ContainerEntity;
    public int TargetSlotIndex;
}

/// <summary>
/// Stores the nearest dropped power-up container currently available to one player.
/// none.
/// returns none.
/// </summary>
public struct PlayerPowerUpContainerProximityState : IComponentData
{
    public Entity NearestContainerEntity;
    public float NearestDistanceSquared;
    public byte HasContainerInRange;
}

/// <summary>
/// Stores a temporary interaction lock against a specific dropped power-up container.
/// </summary>
public struct PlayerPowerUpContainerInteractionLock : IComponentData
{
    // The dropped container temporarily ignored by the player.
    public Entity LockedContainerEntity;
    // Remaining lock duration in seconds.
    public float RemainingLockTime;
}
