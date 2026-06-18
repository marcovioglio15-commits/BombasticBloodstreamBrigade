using Unity.Entities;
using Unity.Transforms;

/// <summary>
/// Synchronizes dropped-container world-space icons from cached power-up presentation metadata.
/// none.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct PlayerDroppedPowerUpContainerIconSyncSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers the runtime components required to update dropped-container companion views.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerDroppedPowerUpContainerContent>();
    }

    /// <summary>
    /// Releases pooled runtime fallback views when the world is destroyed.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnDestroy(ref SystemState state)
    {
        PlayerDroppedPowerUpContainerViewRuntimeUtility.Shutdown();
    }

    /// <summary>
    /// Pushes the current icon sprite and transform pose into each dropped-container runtime view when available.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;

        foreach ((RefRO<PlayerDroppedPowerUpContainerContent> droppedContainerContent,
                  DynamicBuffer<PlayerDroppedPowerUpContainerSlotElement> containerSlotBuffer,
                  RefRO<LocalTransform> containerTransform,
                  Entity containerEntity)
                 in SystemAPI.Query<RefRO<PlayerDroppedPowerUpContainerContent>,
                                    DynamicBuffer<PlayerDroppedPowerUpContainerSlotElement>,
                                    RefRO<LocalTransform>>()
                             .WithEntityAccess())
        {
            if (!PlayerDroppedPowerUpContainerViewRuntimeUtility.TryResolveRuntimeView(entityManager,
                                                                                       containerEntity,
                                                                                       out PlayerDroppedPowerUpContainerView containerView))
            {
                continue;
            }

            PlayerDroppedPowerUpContainerViewRuntimeUtility.SyncViewPose(containerView,
                                                                        in containerTransform.ValueRO);

            if (!PlayerDroppedPowerUpContainerPayloadUtility.HasValidPayload(in droppedContainerContent.ValueRO, containerSlotBuffer) ||
                !PlayerDroppedPowerUpContainerPayloadUtility.TryResolvePowerUpId(containerSlotBuffer, out string powerUpId))
                powerUpId = string.Empty;

            if (!PlayerPowerUpPresentationRuntime.TryResolveIcon(powerUpId, out UnityEngine.Sprite icon))
                icon = null;

            containerView.SetIcon(icon);
        }

        PlayerDroppedPowerUpContainerViewRuntimeUtility.ReleaseInactiveViews(entityManager);
    }
    #endregion

    #endregion
}
