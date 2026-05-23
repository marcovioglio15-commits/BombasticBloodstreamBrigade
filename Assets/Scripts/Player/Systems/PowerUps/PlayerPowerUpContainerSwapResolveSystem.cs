using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Consumes authoritative swap commands issued by HUD or world-space prompts for dropped power-up containers.
/// none.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerMilestonePowerUpSelectionResolveSystem))]
[UpdateBefore(typeof(PlayerPowerUpActivationSystem))]
public partial struct PlayerPowerUpContainerSwapResolveSystem : ISystem
{
    #region Methods

    #region Lifecycle

    /// <summary>
    /// Registers the runtime components required to resolve dropped-container swap commands.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpContainerSwapCommand>();
        state.RequireForUpdate<PlayerDroppedPowerUpContainerContent>();
        state.RequireForUpdate<PlayerPowerUpsConfigElement>();
        state.RequireForUpdate<PlayerPowerUpsState>();
    }

    /// <summary>
    /// Applies the first valid dropped-container swap command queued on each player entity.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        ComponentLookup<PlayerDroppedPowerUpContainerContent> droppedContainerContentLookup = SystemAPI.GetComponentLookup<PlayerDroppedPowerUpContainerContent>(false);
        ComponentLookup<PlayerPowerUpContainerInteractionConfig> interactionConfigLookup = SystemAPI.GetComponentLookup<PlayerPowerUpContainerInteractionConfig>(true);
        ComponentLookup<PlayerPowerUpContainerInteractionLock> interactionLockLookup = SystemAPI.GetComponentLookup<PlayerPowerUpContainerInteractionLock>(false);
        BufferLookup<PlayerScalableStatElement> scalableStatsLookup = SystemAPI.GetBufferLookup<PlayerScalableStatElement>(true);
        BufferLookup<PlayerPowerUpUnlockCatalogElement> unlockCatalogLookup = SystemAPI.GetBufferLookup<PlayerPowerUpUnlockCatalogElement>(false);
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

        foreach ((DynamicBuffer<PlayerPowerUpContainerSwapCommand> swapCommands,
                  DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer,
                  RefRW<PlayerPowerUpsState> powerUpsState,
                  Entity playerEntity)
                 in SystemAPI.Query<DynamicBuffer<PlayerPowerUpContainerSwapCommand>,
                                    DynamicBuffer<PlayerPowerUpsConfigElement>,
                                    RefRW<PlayerPowerUpsState>>().WithEntityAccess())
        {
            if (swapCommands.Length <= 0)
                continue;

            if (!interactionConfigLookup.HasComponent(playerEntity))
            {
                swapCommands.Clear();
                continue;
            }

            PlayerPowerUpContainerInteractionConfig interactionConfig = interactionConfigLookup[playerEntity];
            PlayerPowerUpContainerStoredStateMode storedStateMode = interactionConfig.StoredStateMode;
            DynamicBuffer<PlayerScalableStatElement> scalableStats = scalableStatsLookup.HasBuffer(playerEntity)
                ? scalableStatsLookup[playerEntity]
                : default;
            float interactionLockDuration = PlayerPowerUpContainerInteractionRuntimeUtility.ResolveInteractionLockDuration(in interactionConfig,
                                                                                                                            scalableStats);
            PlayerPowerUpsConfig powerUpsConfig = PlayerPowerUpsConfigBufferUtility.Read(powerUpsConfigBuffer);

            for (int commandIndex = 0; commandIndex < swapCommands.Length; commandIndex++)
            {
                PlayerPowerUpContainerSwapCommand swapCommand = swapCommands[commandIndex];

                if (!droppedContainerContentLookup.HasComponent(swapCommand.ContainerEntity))
                    continue;

                PlayerDroppedPowerUpContainerContent containerContent = droppedContainerContentLookup[swapCommand.ContainerEntity];
                PlayerStoredActivePowerUpData storedPowerUp = containerContent.StoredPowerUp;
                FixedString64Bytes acquiredPowerUpId = storedPowerUp.SlotConfig.PowerUpId;

                if (!PlayerPowerUpLoadoutRuntimeUtility.TrySwapStoredPowerUpWithSlot(ref storedPowerUp,
                                                                                     swapCommand.TargetSlotIndex,
                                                                                     storedStateMode,
                                                                                     ref powerUpsConfig,
                                                                                     ref powerUpsState.ValueRW,
                                                                                     out bool storedPowerUpConsumed))
                    continue;

                PlayerPowerUpsConfigBufferUtility.Write(powerUpsConfigBuffer, in powerUpsConfig);

                if (storedPowerUpConsumed)
                {
                    PlayerDroppedPowerUpContainerViewRuntimeUtility.ReleaseRuntimeView(swapCommand.ContainerEntity);
                    commandBuffer.DestroyEntity(swapCommand.ContainerEntity);
                }
                else
                {
                    containerContent.StoredPowerUp = storedPowerUp;
                    droppedContainerContentLookup[swapCommand.ContainerEntity] = containerContent;
                }

                if (unlockCatalogLookup.HasBuffer(playerEntity))
                {
                    DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog = unlockCatalogLookup[playerEntity];
                    PlayerPowerUpStealCooldownRuntimeUtility.TryMarkPowerUpAcquired(acquiredPowerUpId,
                                                                                    PlayerPowerUpUnlockKind.Active,
                                                                                    unlockCatalog,
                                                                                    elapsedTime);
                }

                ApplyInteractionLock(playerEntity,
                                     swapCommand.ContainerEntity,
                                     storedPowerUpConsumed,
                                     interactionLockDuration,
                                     ref interactionLockLookup);

                powerUpsState.ValueRW.IsShootingSuppressed = 0;
                powerUpsState.ValueRW.PreviousPrimaryPressed = 0;
                powerUpsState.ValueRW.PreviousSecondaryPressed = 0;

                break;
            }

            swapCommands.Clear();
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Stores the post-swap container lock so held or repeated inputs cannot immediately swap the same container again.
    /// </summary>
    /// <param name="playerEntity">Player entity that consumed the swap command.</param>
    /// <param name="containerEntity">Dropped container involved in the swap.</param>
    /// <param name="containerDestroyed">True when the container was consumed because the destination slot was empty.</param>
    /// <param name="interactionLockDuration">Duration of the temporary interaction lock in seconds.</param>
    /// <param name="interactionLockLookup">Mutable lookup used to write the player lock component.</param>
    private static void ApplyInteractionLock(Entity playerEntity,
                                             Entity containerEntity,
                                             bool containerDestroyed,
                                             float interactionLockDuration,
                                             ref ComponentLookup<PlayerPowerUpContainerInteractionLock> interactionLockLookup)
    {
        if (!interactionLockLookup.HasComponent(playerEntity))
            return;

        interactionLockLookup[playerEntity] = new PlayerPowerUpContainerInteractionLock
        {
            LockedContainerEntity = containerDestroyed ? Entity.Null : containerEntity,
            RemainingLockTime = containerDestroyed ? 0f : interactionLockDuration
        };
    }

    #endregion

    #endregion
}
