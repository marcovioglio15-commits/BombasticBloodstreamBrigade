using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Provides managed helper methods for UI and MonoBehaviour scripts that need to submit scene transition requests.
/// /params None.
/// /returns None.
/// </summary>
public static class GameSceneTransitionRequestUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Enqueues a request to load the default gameplay scene configured by the active Scene Manager.
    /// /params None.
    /// /returns True when the request was submitted.
    /// </summary>
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
    /// /params None.
    /// /returns True when the request was submitted.
    /// </summary>
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
    /// /params None.
    /// /returns True when the request was submitted.
    /// </summary>
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
    /// /params sceneId Target scene ID.
    /// /returns True when the request was submitted.
    /// </summary>
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
    /// /params transitionId Target transition ID.
    /// /returns True when the request was submitted.
    /// </summary>
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
    /// /params request Request to enqueue.
    /// /returns True when a valid request buffer was found.
    /// </summary>
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
