using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// Spawns dropped world containers that serialize one replaced active power-up payload.
/// none.
/// </summary>
internal static class PlayerPowerUpContainerSpawnUtility
{
    #region Constants
    private const float ForwardDropDistance = 0.85f;
    private const float ForwardLengthEpsilon = 0.0001f;
    private const float GroundProbeStartHeight = 8f;
    private const float GroundProbeDistance = 24f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Instantiates one dropped power-up container from the baked prefab and stores the provided active-slot snapshot into it.
    /// </summary>
    /// <param name="playerTransform">Current player transform used to resolve the drop position.</param>
    /// <param name="interactionConfig">Player-side container interaction config containing the baked prefab entity.</param>
    /// <param name="storedPowerUp">Active-slot snapshot serialized into the dropped world entity.</param>
    /// <param name="commandBuffer">ECB used to instantiate and configure the container entity.</param>
    /// <returns>True when a container was spawned; otherwise false.</returns>
    public static bool TrySpawnDroppedContainer(in PhysicsWorldSingleton physicsWorldSingleton,
                                                in LocalTransform playerTransform,
                                                in PlayerPowerUpContainerInteractionConfig interactionConfig,
                                                in PlayerStoredActivePowerUpData storedPowerUp,
                                                ref EntityCommandBuffer commandBuffer)
    {
        float3 dropOrigin = ResolveForwardDropOrigin(in playerTransform);
        return TrySpawnDroppedContainerAtPosition(in physicsWorldSingleton,
                                                  dropOrigin,
                                                  in interactionConfig,
                                                  in storedPowerUp,
                                                  ref commandBuffer);
    }

    /// <summary>
    /// Instantiates one dropped power-up container at an authored world position and grounds it through the shared probe.
    /// </summary>
    /// <param name="physicsWorldSingleton">Physics world used to ground the container.</param>
    /// <param name="dropOrigin">World-space origin where the container should be dropped.</param>
    /// <param name="interactionConfig">Player-side container interaction config containing the baked prefab entity.</param>
    /// <param name="storedPowerUp">Active-slot snapshot serialized into the dropped world entity.</param>
    /// <param name="commandBuffer">ECB used to instantiate and configure the container entity.</param>
    /// <returns>True when a container was spawned; otherwise false.</returns>
    public static bool TrySpawnDroppedContainerAtPosition(in PhysicsWorldSingleton physicsWorldSingleton,
                                                          float3 dropOrigin,
                                                          in PlayerPowerUpContainerInteractionConfig interactionConfig,
                                                          in PlayerStoredActivePowerUpData storedPowerUp,
                                                          ref EntityCommandBuffer commandBuffer)
    {
        if (storedPowerUp.SlotConfig.IsDefined == 0)
            return false;

        if (interactionConfig.ContainerPrefabEntity == Entity.Null)
            return false;

        Entity containerEntity = commandBuffer.Instantiate(interactionConfig.ContainerPrefabEntity);
        LocalTransform containerTransform = LocalTransform.FromPositionRotationScale(ResolveGroundedDropPosition(in physicsWorldSingleton,
                                                                                                                 dropOrigin,
                                                                                                                 in interactionConfig),
                                                                                     quaternion.identity,
                                                                                     1f);

        commandBuffer.SetComponent(containerEntity, containerTransform);
        commandBuffer.AddComponent(containerEntity, new PlayerDroppedPowerUpContainerContent
        {
            StoredEnergy = storedPowerUp.StoredEnergy,
            StoredCooldownRemaining = storedPowerUp.StoredCooldownRemaining,
            ReturningProjectileCount = storedPowerUp.ReturningProjectileCount,
            ReturningProjectileRecallReadyCount = storedPowerUp.ReturningProjectileRecallReadyCount,
            ReturningProjectileGeneration = storedPowerUp.ReturningProjectileGeneration,
            ReturningProjectileRecallVersion = storedPowerUp.ReturningProjectileRecallVersion,
            ReturningProjectileResourceRecallVersion = storedPowerUp.ReturningProjectileResourceRecallVersion,
            ReturningProjectileResourceDrainActive = storedPowerUp.ReturningProjectileResourceDrainActive,
            PreserveReturningProjectileOwnership = storedPowerUp.PreserveReturningProjectileOwnership,
            HasStoredPowerUp = 1
        });
        DynamicBuffer<PlayerDroppedPowerUpContainerSlotElement> slotBuffer = commandBuffer.AddBuffer<PlayerDroppedPowerUpContainerSlotElement>(containerEntity);
        slotBuffer.ResizeUninitialized(1);
        ref PlayerDroppedPowerUpContainerSlotElement slotElement = ref slotBuffer.ElementAt(0);
        slotElement.SlotConfig = storedPowerUp.SlotConfig;
        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves a drop origin slightly in front of the player.
    /// </summary>
    /// <param name="playerTransform">Current player transform used to place the dropped container.</param>
    /// <returns>World position before ground projection.</returns>
    private static float3 ResolveForwardDropOrigin(in LocalTransform playerTransform)
    {
        float3 forward = math.mul(playerTransform.Rotation, new float3(0f, 0f, 1f));
        forward.y = 0f;

        if (math.lengthsq(forward) > ForwardLengthEpsilon)
            forward = math.normalize(forward) * ForwardDropDistance;
        else
            forward = new float3(0f, 0f, ForwardDropDistance);

        return playerTransform.Position + forward;
    }

    /// <summary>
    /// Resolves a grounded drop position from the requested origin.
    /// </summary>
    /// <param name="physicsWorldSingleton">Physics world used to ground the container.</param>
    /// <param name="dropOrigin">World-space origin requested by the caller.</param>
    /// <param name="interactionConfig">Container interaction config providing clearance offsets.</param>
    /// <returns>World position used by the container entity.</returns>
    private static float3 ResolveGroundedDropPosition(in PhysicsWorldSingleton physicsWorldSingleton,
                                                      float3 dropOrigin,
                                                      in PlayerPowerUpContainerInteractionConfig interactionConfig)
    {
        float3 dropPosition = dropOrigin;
        float groundClearanceOffset = math.max(0f, interactionConfig.ContainerGroundClearanceOffset);
        RaycastInput groundProbe = new RaycastInput
        {
            Start = dropPosition + new float3(0f, GroundProbeStartHeight, 0f),
            End = dropPosition - new float3(0f, GroundProbeDistance, 0f),
            Filter = new CollisionFilter
            {
                BelongsTo = uint.MaxValue,
                CollidesWith = uint.MaxValue,
                GroupIndex = 0
            }
        };

        if (physicsWorldSingleton.CastRay(groundProbe, out Unity.Physics.RaycastHit groundHit))
            dropPosition.y = groundHit.Position.y + groundClearanceOffset;
        else
            dropPosition.y = dropOrigin.y + groundClearanceOffset;

        return dropPosition;
    }
    #endregion

    #endregion
}
