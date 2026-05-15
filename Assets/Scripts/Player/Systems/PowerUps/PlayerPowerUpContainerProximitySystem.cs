using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Resolves the nearest dropped power-up container within interaction range for each player.
/// none.
/// returns none.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpContainerSwapResolveSystem))]
public partial struct PlayerPowerUpContainerProximitySystem : ISystem
{
    #region Methods

    #region Lifecycle

    /// <summary>
    /// Registers the runtime components required to track dropped-container proximity.
    /// state: Current ECS system state.
    /// returns void.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpContainerInteractionConfig>();
        state.RequireForUpdate<PlayerPowerUpContainerProximityState>();
        state.RequireForUpdate<PlayerPowerUpContainerInteractionLock>();
        state.RequireForUpdate<LocalTransform>();
    }

    /// <summary>
    /// Updates the nearest dropped container for each player using squared-distance comparisons only.
    /// state: Current ECS system state.
    /// returns void.
    /// </summary>
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRO<PlayerPowerUpContainerInteractionConfig> interactionConfig,
                  RefRO<LocalTransform> playerTransform,
                  RefRW<PlayerPowerUpContainerProximityState> proximityState,
                  RefRW<PlayerPowerUpContainerInteractionLock> interactionLock)
                 in SystemAPI.Query<RefRO<PlayerPowerUpContainerInteractionConfig>,
                                    RefRO<LocalTransform>,
                                    RefRW<PlayerPowerUpContainerProximityState>,
                                    RefRW<PlayerPowerUpContainerInteractionLock>>()
                             .WithAll<PlayerControllerConfig>())
        {
            if (interactionLock.ValueRO.LockedContainerEntity != Entity.Null)
            {
                interactionLock.ValueRW.RemainingLockTime -= deltaTime;

                if (interactionLock.ValueRO.RemainingLockTime <= 0f)
                {
                    interactionLock.ValueRW.LockedContainerEntity = Entity.Null;
                    interactionLock.ValueRW.RemainingLockTime = 0f;
                }
            }

            float interactionRadius = math.max(0f, interactionConfig.ValueRO.InteractionRadius);
            float interactionRadiusSquared = interactionRadius * interactionRadius;
            Entity nearestContainerEntity = Entity.Null;
            float nearestDistanceSquared = float.MaxValue;
            Entity lockedContainerEntity = interactionLock.ValueRO.LockedContainerEntity;

            if (interactionRadiusSquared > 0f)
            {
                float3 playerPosition = playerTransform.ValueRO.Position;

                foreach ((RefRO<PlayerDroppedPowerUpContainerContent> droppedContainerContent,
                          RefRO<LocalTransform> containerTransform,
                          Entity containerEntity)
                         in SystemAPI.Query<RefRO<PlayerDroppedPowerUpContainerContent>,
                                            RefRO<LocalTransform>>().WithEntityAccess())
                {
                    if (droppedContainerContent.ValueRO.StoredPowerUp.SlotConfig.IsDefined == 0)
                        continue;

                    if (containerEntity == lockedContainerEntity)
                        continue;

                    float distanceSquared = math.distancesq(playerPosition, containerTransform.ValueRO.Position);

                    if (distanceSquared > interactionRadiusSquared)
                        continue;

                    if (distanceSquared >= nearestDistanceSquared)
                        continue;

                    nearestDistanceSquared = distanceSquared;
                    nearestContainerEntity = containerEntity;
                }
            }

            proximityState.ValueRW = new PlayerPowerUpContainerProximityState
            {
                NearestContainerEntity = nearestContainerEntity,
                NearestDistanceSquared = nearestContainerEntity != Entity.Null ? nearestDistanceSquared : 0f,
                HasContainerInRange = nearestContainerEntity != Entity.Null ? (byte)1 : (byte)0
            };
        }
    }

    #endregion

    #endregion
}
