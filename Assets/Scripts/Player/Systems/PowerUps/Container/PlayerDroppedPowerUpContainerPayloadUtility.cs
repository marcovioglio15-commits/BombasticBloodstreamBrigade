using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Reads and writes dropped power-up container payloads stored as compact metadata plus a large slot buffer.
/// </summary>
internal static class PlayerDroppedPowerUpContainerPayloadUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Checks whether a container entity still exists and stores one valid active payload.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect container components.</param>
    /// <param name="containerEntity">Dropped container entity inspected for usability.</param>
    /// <returns>True when the container can still be interacted with.</returns>
    public static bool IsContainerUsable(EntityManager entityManager, Entity containerEntity)
    {
        if (containerEntity == Entity.Null || !entityManager.Exists(containerEntity))
            return false;

        if (!entityManager.HasComponent<PlayerDroppedPowerUpContainerContent>(containerEntity))
            return false;

        if (!entityManager.HasBuffer<PlayerDroppedPowerUpContainerSlotElement>(containerEntity))
            return false;

        PlayerDroppedPowerUpContainerContent containerContent = entityManager.GetComponentData<PlayerDroppedPowerUpContainerContent>(containerEntity);
        DynamicBuffer<PlayerDroppedPowerUpContainerSlotElement> containerSlotBuffer = entityManager.GetBuffer<PlayerDroppedPowerUpContainerSlotElement>(containerEntity);
        return HasValidPayload(in containerContent, containerSlotBuffer);
    }

    /// <summary>
    /// Resolves the active power-up id currently stored in a dropped container entity.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect container components.</param>
    /// <param name="containerEntity">Dropped container entity inspected for UI metadata.</param>
    /// <param name="powerUpId">Resolved power-up id string.</param>
    /// <returns>True when a valid stored payload exists.</returns>
    public static bool TryResolvePowerUpId(EntityManager entityManager, Entity containerEntity, out string powerUpId)
    {
        powerUpId = string.Empty;

        if (!IsContainerUsable(entityManager, containerEntity))
            return false;

        DynamicBuffer<PlayerDroppedPowerUpContainerSlotElement> containerSlotBuffer = entityManager.GetBuffer<PlayerDroppedPowerUpContainerSlotElement>(containerEntity);
        return TryResolvePowerUpId(containerSlotBuffer, out powerUpId);
    }

    /// <summary>
    /// Resolves the active power-up id from a container slot buffer without copying the large slot payload.
    /// </summary>
    /// <param name="containerSlotBuffer">Buffer containing the large active slot payload.</param>
    /// <param name="powerUpId">Resolved power-up id string.</param>
    /// <returns>True when a valid slot payload exists.</returns>
    public static bool TryResolvePowerUpId(DynamicBuffer<PlayerDroppedPowerUpContainerSlotElement> containerSlotBuffer, out string powerUpId)
    {
        powerUpId = string.Empty;

        if (containerSlotBuffer.Length <= 0)
            return false;

        ref PlayerDroppedPowerUpContainerSlotElement slotElement = ref containerSlotBuffer.ElementAt(0);

        if (slotElement.SlotConfig.IsDefined == 0)
            return false;

        powerUpId = slotElement.SlotConfig.PowerUpId.ToString();
        return true;
    }

    /// <summary>
    /// Checks whether compact container metadata and its slot buffer describe one valid active payload.
    /// </summary>
    /// <param name="containerContent">Small container state component.</param>
    /// <param name="containerSlotBuffer">Buffer containing the large active slot payload.</param>
    /// <returns>True when the container stores a usable active payload.</returns>
    public static bool HasValidPayload(in PlayerDroppedPowerUpContainerContent containerContent,
                                       DynamicBuffer<PlayerDroppedPowerUpContainerSlotElement> containerSlotBuffer)
    {
        if (containerContent.HasStoredPowerUp == 0)
            return false;

        if (containerSlotBuffer.Length <= 0)
            return false;

        ref PlayerDroppedPowerUpContainerSlotElement slotElement = ref containerSlotBuffer.ElementAt(0);
        return slotElement.SlotConfig.IsDefined != 0;
    }

    /// <summary>
    /// Rebuilds the active payload snapshot from compact container metadata and its slot buffer.
    /// </summary>
    /// <param name="containerContent">Small container state component.</param>
    /// <param name="containerSlotBuffer">Buffer containing the large active slot payload.</param>
    /// <param name="storedPowerUp">Rebuilt active payload snapshot.</param>
    /// <returns>True when the container currently stores a valid active payload.</returns>
    public static bool TryReadStoredPowerUp(in PlayerDroppedPowerUpContainerContent containerContent,
                                            DynamicBuffer<PlayerDroppedPowerUpContainerSlotElement> containerSlotBuffer,
                                            out PlayerStoredActivePowerUpData storedPowerUp)
    {
        storedPowerUp = default;

        if (!HasValidPayload(in containerContent, containerSlotBuffer))
            return false;

        ref PlayerDroppedPowerUpContainerSlotElement slotElement = ref containerSlotBuffer.ElementAt(0);
        storedPowerUp.SlotConfig = slotElement.SlotConfig;
        storedPowerUp.StoredEnergy = containerContent.StoredEnergy;
        storedPowerUp.StoredCooldownRemaining = containerContent.StoredCooldownRemaining;
        storedPowerUp.ReturningProjectileCount = containerContent.ReturningProjectileCount;
        storedPowerUp.ReturningProjectileRecallReadyCount = containerContent.ReturningProjectileRecallReadyCount;
        storedPowerUp.ReturningProjectileGeneration = containerContent.ReturningProjectileGeneration;
        storedPowerUp.ReturningProjectileRecallVersion = containerContent.ReturningProjectileRecallVersion;
        storedPowerUp.ReturningProjectileResourceRecallVersion = containerContent.ReturningProjectileResourceRecallVersion;
        storedPowerUp.ReturningProjectileResourceDrainActive = containerContent.ReturningProjectileResourceDrainActive;
        storedPowerUp.PreserveReturningProjectileOwnership = containerContent.PreserveReturningProjectileOwnership;
        return true;
    }

    /// <summary>
    /// Writes an active payload back into a dropped container without passing the large slot config by value.
    /// </summary>
    /// <param name="containerEntity">Dropped container entity receiving the updated payload.</param>
    /// <param name="storedPowerUp">Updated active payload snapshot.</param>
    /// <param name="droppedContainerContentLookup">Mutable lookup for compact container metadata.</param>
    /// <param name="droppedContainerSlotLookup">Mutable lookup for the large slot payload buffer.</param>
    public static void WriteStoredPowerUp(Entity containerEntity,
                                          in PlayerStoredActivePowerUpData storedPowerUp,
                                          ref ComponentLookup<PlayerDroppedPowerUpContainerContent> droppedContainerContentLookup,
                                          ref BufferLookup<PlayerDroppedPowerUpContainerSlotElement> droppedContainerSlotLookup)
    {
        PlayerDroppedPowerUpContainerContent containerContent = droppedContainerContentLookup[containerEntity];
        containerContent.StoredEnergy = storedPowerUp.StoredEnergy;
        containerContent.StoredCooldownRemaining = storedPowerUp.StoredCooldownRemaining;
        containerContent.ReturningProjectileCount = storedPowerUp.ReturningProjectileCount;
        containerContent.ReturningProjectileRecallReadyCount = storedPowerUp.ReturningProjectileRecallReadyCount;
        containerContent.ReturningProjectileGeneration = storedPowerUp.ReturningProjectileGeneration;
        containerContent.ReturningProjectileRecallVersion = storedPowerUp.ReturningProjectileRecallVersion;
        containerContent.ReturningProjectileResourceRecallVersion = storedPowerUp.ReturningProjectileResourceRecallVersion;
        containerContent.ReturningProjectileResourceDrainActive = storedPowerUp.ReturningProjectileResourceDrainActive;
        containerContent.PreserveReturningProjectileOwnership = storedPowerUp.PreserveReturningProjectileOwnership;
        containerContent.HasStoredPowerUp = storedPowerUp.SlotConfig.IsDefined != 0 ? (byte)1 : (byte)0;
        droppedContainerContentLookup[containerEntity] = containerContent;

        DynamicBuffer<PlayerDroppedPowerUpContainerSlotElement> containerSlotBuffer = droppedContainerSlotLookup[containerEntity];

        if (containerSlotBuffer.Length <= 0)
            containerSlotBuffer.ResizeUninitialized(1);

        ref PlayerDroppedPowerUpContainerSlotElement slotElement = ref containerSlotBuffer.ElementAt(0);
        slotElement.SlotConfig = storedPowerUp.SlotConfig;
    }
    #endregion

    #endregion
}
