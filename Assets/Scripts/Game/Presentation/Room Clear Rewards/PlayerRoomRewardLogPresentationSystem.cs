using Unity.Entities;
using UnityEngine;

/// <summary>
/// Transfers authoritative player reward events into the preauthored log attached to the runtime visual prefab.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(PlayerManagedVisualAnimatorBridgeSystem))]
public partial class PlayerRoomRewardLogPresentationSystem : SystemBase
{
    #region Fields
    private EntityQuery managerQuery;
    private EntityQuery playerQuery;
    private Entity cachedPlayerEntity;
    private PlayerRoomRewardLogView cachedView;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the unique config and player presentation queues required by this bridge.
    /// </summary>
    protected override void OnCreate()
    {
        managerQuery = GetEntityQuery(typeof(GameRoomRewardConfig),
                                      typeof(GameRoomRewardPresentationElement));
        playerQuery = GetEntityQuery(typeof(PlayerControllerConfig),
                                     typeof(PlayerRoomRewardPresentationEvent));
    }

    /// <summary>
    /// Formats all pending reward events and enqueues them only after the preauthored visual view is available.
    /// </summary>
    protected override void OnUpdate()
    {
        if (managerQuery.CalculateEntityCount() != 1 || playerQuery.CalculateEntityCount() != 1)
            return;

        Entity playerEntity = playerQuery.GetSingletonEntity();
        DynamicBuffer<PlayerRoomRewardPresentationEvent> events =
            EntityManager.GetBuffer<PlayerRoomRewardPresentationEvent>(playerEntity);

        if (events.Length == 0)
            return;

        if (!TryResolveView(playerEntity, out PlayerRoomRewardLogView view, out Transform visualRoot))
            return;

        Entity managerEntity = managerQuery.GetSingletonEntity();
        GameRoomRewardConfig config =
            EntityManager.GetComponentData<GameRoomRewardConfig>(managerEntity);
        DynamicBuffer<GameRoomRewardPresentationElement> mappings =
            EntityManager.GetBuffer<GameRoomRewardPresentationElement>(managerEntity, true);
        view.ConfigureRuntime(visualRoot, in config);

        // Preserve authoritative event order while moving every entry into the bounded managed view queue.
        for (int eventIndex = 0; eventIndex < events.Length; eventIndex++)
        {
            PlayerRoomRewardPresentationEvent rewardEvent = events[eventIndex];
            GameRoomRewardPresentationItem item =
                GameRoomRewardPresentationFormatter.FormatPlayerEvent(in rewardEvent, mappings);
            view.Enqueue(in item);
        }

        events.Clear();
    }
    #endregion

    #region View Resolution
    /// <summary>
    /// Resolves and caches the preauthored log below the current managed player visual root.
    /// </summary>
    /// <param name="playerEntity">Player entity owning the managed visual bridge.</param>
    /// <param name="view">Resolved preauthored reward log.</param>
    /// <param name="visualRoot">Runtime visual root followed by the view.</param>
    /// <returns>True when the runtime player visual contains a valid preauthored view.</returns>
    private bool TryResolveView(Entity playerEntity,
                                out PlayerRoomRewardLogView view,
                                out Transform visualRoot)
    {
        view = null;
        visualRoot = null;

        if (!PlayerManagedVisualAnimatorBridgeSystem.TryGetRuntimeBridgeRoot(playerEntity,
                                                                             out visualRoot))
        {
            return false;
        }

        if (cachedPlayerEntity == playerEntity && cachedView != null)
        {
            view = cachedView;
            return true;
        }

        cachedPlayerEntity = playerEntity;
        cachedView = visualRoot.GetComponentInChildren<PlayerRoomRewardLogView>(true);
        view = cachedView;
        return view != null;
    }
    #endregion

    #endregion
}
