using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Detects player entry into baked scene transition volumes and submits scene manager requests.
/// /params None.
/// /returns None.
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
    /// /params None.
    /// /returns None.
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
    /// /params None.
    /// /returns None.
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
    /// /params None.
    /// /returns Player world position.
    /// </summary>
    private float3 ResolvePlayerPosition()
    {
        NativeArray<LocalTransform> transforms = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        LocalTransform playerTransform = transforms[0];
        transforms.Dispose();
        return playerTransform.Position;
    }

    /// <summary>
    /// Updates one trigger and submits a request on first player entry.
    /// /params playerPosition Current player world position.
    /// /params trigger Baked trigger data.
    /// /params transitions Available transition definitions.
    /// /params requests Request buffer that receives a transition request.
    /// /params config Scene manager config containing trigger defaults.
    /// /params deltaTime Frame delta time used to update cooldown.
    /// /params runtimeState Mutable runtime state for this trigger.
    /// /returns True when this trigger submitted a request.
    /// </summary>
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
    /// /params point World-space point.
    /// /params center World-space box center.
    /// /params halfExtents World-space half extents.
    /// /returns True when the point is inside the box.
    /// </summary>
    private static bool IsPointInside(float3 point, float3 center, float3 halfExtents)
    {
        float3 delta = math.abs(point - center);
        return delta.x <= halfExtents.x &&
               delta.y <= halfExtents.y &&
               delta.z <= halfExtents.z;
    }

    /// <summary>
    /// Resolves the transition request represented by a trigger or matching transition definition.
    /// /params trigger Baked trigger data.
    /// /params transitions Available transition definitions.
    /// /params request Output request when one can be built.
    /// /returns True when a request target was resolved.
    /// </summary>
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
    /// /params trigger Baked trigger data.
    /// /params config Scene manager singleton config.
    /// /returns Non-negative cooldown seconds.
    /// </summary>
    private static float ResolveCooldown(GameSceneTransitionTrigger trigger, GameSceneManagerConfig config)
    {
        if (trigger.CooldownSeconds >= 0f)
            return trigger.CooldownSeconds;

        return math.max(0f, config.DefaultTriggerCooldownSeconds);
    }
    #endregion

    #endregion
}
