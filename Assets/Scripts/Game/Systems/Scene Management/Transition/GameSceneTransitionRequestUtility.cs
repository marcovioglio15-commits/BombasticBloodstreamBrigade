using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Provides managed helper methods for UI and MonoBehaviour scripts that need to submit scene transition requests.
/// </summary>
public static class GameSceneTransitionRequestUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Enqueues a request to load the default gameplay scene configured by the active Scene Manager.
    /// </summary>
    /// <returns>True when the request was submitted.</returns>
    public static bool EnqueueLoadDefaultGameplay()
    {
        return Enqueue(new GameSceneTransitionRequest
        {
            RequestType = GameSceneTransitionRequestType.LoadDefaultGameplay,
            TargetSceneId = default,
            TransitionId = default
        });
    }

    /// <summary>
    /// Enqueues a request to load the configured main menu scene.
    /// </summary>
    /// <returns>True when the request was submitted.</returns>
    public static bool EnqueueLoadMainMenu()
    {
        return Enqueue(new GameSceneTransitionRequest
        {
            RequestType = GameSceneTransitionRequestType.LoadMainMenu,
            TargetSceneId = default,
            TransitionId = default
        });
    }

    /// <summary>
    /// Enqueues a request to reload the currently active managed scene.
    /// </summary>
    /// <returns>True when the request was submitted.</returns>
    public static bool EnqueueRestartActiveScene()
    {
        return Enqueue(new GameSceneTransitionRequest
        {
            RequestType = GameSceneTransitionRequestType.RestartActiveScene,
            TargetSceneId = default,
            TransitionId = default
        });
    }

    /// <summary>
    /// Enqueues a request to load a scene by stable Scene Manager scene ID.
    /// </summary>
    /// <param name="sceneId">Target scene ID.</param>
    /// <returns>True when the request was submitted.</returns>
    public static bool EnqueueLoadScene(string sceneId)
    {
        if (string.IsNullOrWhiteSpace(sceneId))
            return false;

        return Enqueue(new GameSceneTransitionRequest
        {
            RequestType = GameSceneTransitionRequestType.LoadScene,
            TargetSceneId = new FixedString64Bytes(sceneId),
            TransitionId = default
        });
    }

    /// <summary>
    /// Enqueues a request to run a transition by stable transition ID.
    /// </summary>
    /// <param name="transitionId">Target transition ID.</param>
    /// <returns>True when the request was submitted.</returns>
    public static bool EnqueueTransition(string transitionId)
    {
        if (string.IsNullOrWhiteSpace(transitionId))
            return false;

        return Enqueue(new GameSceneTransitionRequest
        {
            RequestType = GameSceneTransitionRequestType.LoadScene,
            TargetSceneId = default,
            TransitionId = new FixedString64Bytes(transitionId)
        });
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds one request to the active scene manager singleton buffer.
    /// </summary>
    /// <param name="request">Request to enqueue.</param>
    /// <returns>True when a valid request buffer was found.</returns>
    private static bool Enqueue(GameSceneTransitionRequest request)
    {
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
        {
            Debug.LogWarning("[GameSceneTransitionRequestUtility] No default ECS world is available for scene transition requests.");
            return false;
        }

        EntityManager entityManager = world.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameSceneManagerConfig>(),
                                                            ComponentType.ReadWrite<GameSceneTransitionRequest>());
        int entityCount = query.CalculateEntityCount();

        if (entityCount != 1)
        {
            query.Dispose();
            Debug.LogWarning("[GameSceneTransitionRequestUtility] Expected one Game Scene Manager singleton, found " + entityCount + ".");
            return false;
        }

        Entity managerEntity = query.GetSingletonEntity();
        DynamicBuffer<GameSceneTransitionRequest> requestBuffer = entityManager.GetBuffer<GameSceneTransitionRequest>(managerEntity);
        requestBuffer.Add(request);
        query.Dispose();
        return true;
    }
    #endregion

    #endregion
}
