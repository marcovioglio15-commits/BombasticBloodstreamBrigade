using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Detects player entry into baked scene transition volumes and submits scene manager requests.
/// </summary>
[UpdateInGroup(typeof(GameSceneManagementSystemGroup))]
public partial class GameSceneTransitionTriggerSystem : SystemBase
{
    #region Fields
    private EntityQuery managerQuery;
    private EntityQuery playerQuery;
    private EntityQuery triggerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the entity queries used by the trigger system.
    /// </summary>
    protected override void OnCreate()
    {
        managerQuery = GetEntityQuery(typeof(GameSceneManagerConfig),
                                      typeof(GameSceneTransitionState),
                                      typeof(GameSceneTransitionRequest),
                                      typeof(GameSceneTransitionElement));
        playerQuery = GetEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>(),
                                     ComponentType.ReadOnly<LocalTransform>());
        triggerQuery = GetEntityQuery(typeof(GameSceneTransitionTrigger),
                                      typeof(GameSceneTransitionTriggerRuntimeState));
    }

    /// <summary>
    /// Processes trigger entry and cooldown state only when a manager, player and trigger exist.
    /// </summary>
    protected override void OnUpdate()
    {
        if (managerQuery.CalculateEntityCount() != 1)
            return;

        if (triggerQuery.IsEmptyIgnoreFilter)
            return;

        if (playerQuery.IsEmptyIgnoreFilter)
            return;

        Entity managerEntity = managerQuery.GetSingletonEntity();
        GameSceneTransitionState transitionState = EntityManager.GetComponentData<GameSceneTransitionState>(managerEntity);

        if (transitionState.IsTransitioning != 0)
            return;

        float3 playerPosition = ResolvePlayerPosition();
        DynamicBuffer<GameSceneTransitionRequest> requestBuffer = EntityManager.GetBuffer<GameSceneTransitionRequest>(managerEntity);
        DynamicBuffer<GameSceneTransitionElement> transitionBuffer = EntityManager.GetBuffer<GameSceneTransitionElement>(managerEntity);
        GameSceneManagerConfig config = EntityManager.GetComponentData<GameSceneManagerConfig>(managerEntity);
        NativeArray<Entity> triggerEntities = triggerQuery.ToEntityArray(Allocator.Temp);
        NativeArray<GameSceneTransitionTrigger> triggers = triggerQuery.ToComponentDataArray<GameSceneTransitionTrigger>(Allocator.Temp);
        NativeArray<GameSceneTransitionTriggerRuntimeState> runtimeStates = triggerQuery.ToComponentDataArray<GameSceneTransitionTriggerRuntimeState>(Allocator.Temp);
        float deltaTime = SystemAPI.Time.DeltaTime;

        for (int index = 0; index < triggers.Length; index++)
        {
            GameSceneTransitionTrigger trigger = triggers[index];
            GameSceneTransitionTriggerRuntimeState runtimeState = runtimeStates[index];
            bool submittedRequest = ProcessTrigger(playerPosition,
                                                   trigger,
                                                   transitionBuffer,
                                                   requestBuffer,
                                                   config,
                                                   deltaTime,
                                                   ref runtimeState);
            EntityManager.SetComponentData(triggerEntities[index], runtimeState);

            if (submittedRequest)
                break;
        }

        triggerEntities.Dispose();
        triggers.Dispose();
        runtimeStates.Dispose();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the first player position available to transition triggers.
    /// </summary>
    /// <returns>Player world position.</returns>
    private float3 ResolvePlayerPosition()
    {
        NativeArray<LocalTransform> transforms = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        LocalTransform playerTransform = transforms[0];
        transforms.Dispose();
        return playerTransform.Position;
    }

    /// <summary>
    /// Updates one trigger and submits a request on first player entry.
    /// </summary>
    /// <param name="playerPosition">Current player world position.</param>
    /// <param name="trigger">Baked trigger data.</param>
    /// <param name="transitions">Available transition definitions.</param>
    /// <param name="requests">Request buffer that receives a transition request.</param>
    /// <param name="config">Scene manager config containing trigger defaults.</param>
    /// <param name="deltaTime">Frame delta time used to update cooldown.</param>
    /// <param name="runtimeState">Mutable runtime state for this trigger.</param>
    /// <returns>True when this trigger submitted a request.</returns>
    private static bool ProcessTrigger(float3 playerPosition,
                                       GameSceneTransitionTrigger trigger,
                                       DynamicBuffer<GameSceneTransitionElement> transitions,
                                       DynamicBuffer<GameSceneTransitionRequest> requests,
                                       GameSceneManagerConfig config,
                                       float deltaTime,
                                       ref GameSceneTransitionTriggerRuntimeState runtimeState)
    {
        if (runtimeState.CooldownRemainingSeconds > 0f)
            runtimeState.CooldownRemainingSeconds = math.max(0f, runtimeState.CooldownRemainingSeconds - deltaTime);

        if (trigger.OneShot != 0 && runtimeState.HasTriggered != 0)
            return false;

        bool isInside = IsPointInside(playerPosition, trigger.Center, trigger.HalfExtents);
        bool wasInside = runtimeState.WasPlayerInside != 0;
        runtimeState.WasPlayerInside = isInside ? (byte)1 : (byte)0;

        if (!isInside || wasInside)
            return false;

        if (runtimeState.CooldownRemainingSeconds > 0f)
            return false;

        if (!TryBuildRequest(trigger, transitions, out GameSceneTransitionRequest request))
            return false;

        requests.Add(request);
        runtimeState.CooldownRemainingSeconds = ResolveCooldown(trigger, config);

        if (trigger.OneShot != 0)
            runtimeState.HasTriggered = 1;

        return true;
    }

    /// <summary>
    /// Checks whether one point lies inside an axis-aligned trigger box.
    /// </summary>
    /// <param name="point">World-space point.</param>
    /// <param name="center">World-space box center.</param>
    /// <param name="halfExtents">World-space half extents.</param>
    /// <returns>True when the point is inside the box.</returns>
    private static bool IsPointInside(float3 point, float3 center, float3 halfExtents)
    {
        float3 delta = math.abs(point - center);
        return delta.x <= halfExtents.x &&
               delta.y <= halfExtents.y &&
               delta.z <= halfExtents.z;
    }

    /// <summary>
    /// Resolves the transition request represented by a trigger or matching transition definition.
    /// </summary>
    /// <param name="trigger">Baked trigger data.</param>
    /// <param name="transitions">Available transition definitions.</param>
    /// <param name="request">Output request when one can be built.</param>
    /// <returns>True when a request target was resolved.</returns>
    private static bool TryBuildRequest(GameSceneTransitionTrigger trigger,
                                        DynamicBuffer<GameSceneTransitionElement> transitions,
                                        out GameSceneTransitionRequest request)
    {
        request = default;

        if (trigger.TransitionId.Length > 0)
        {
            request = new GameSceneTransitionRequest
            {
                RequestType = GameSceneTransitionRequestType.LoadScene,
                TargetSceneId = default,
                TransitionId = trigger.TransitionId
            };
            return true;
        }

        if (GameSceneLoadBackendUtility.TryFindTransitionForTrigger(transitions, trigger.TriggerId, out GameSceneTransitionElement transition))
        {
            request = new GameSceneTransitionRequest
            {
                RequestType = GameSceneTransitionRequestType.LoadScene,
                TargetSceneId = transition.ToSceneId,
                TransitionId = transition.TransitionId
            };
            return true;
        }

        if (trigger.TargetSceneId.Length <= 0)
            return false;

        request = new GameSceneTransitionRequest
        {
            RequestType = GameSceneTransitionRequestType.LoadScene,
            TargetSceneId = trigger.TargetSceneId,
            TransitionId = default
        };
        return true;
    }

    /// <summary>
    /// Resolves cooldown from trigger override or scene manager defaults.
    /// </summary>
    /// <param name="trigger">Baked trigger data.</param>
    /// <param name="config">Scene manager singleton config.</param>
    /// <returns>Non-negative cooldown seconds.</returns>
    private static float ResolveCooldown(GameSceneTransitionTrigger trigger, GameSceneManagerConfig config)
    {
        if (trigger.CooldownSeconds >= 0f)
            return trigger.CooldownSeconds;

        return math.max(0f, config.DefaultTriggerCooldownSeconds);
    }
    #endregion

    #endregion
}
