#if UNITY_EDITOR
using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Verifies that repeated scene commands collapse inside the authoritative ECS request buffer without suppressing
/// requests whose target or operation differs.
/// </summary>
public static class GameSceneTransitionRequestDeduplicationSmokeTest
{
    #region Methods

    #region Public Methods
    // [UnityEditor.MenuItem("Tools/Game/Run Scene Transition Request Deduplication Smoke Test")]
    /// <summary>
    /// Executes deterministic request-buffer checks from Unity batch mode through -executeMethod.
    /// </summary>
    public static void Run()
    {
        World testWorld = new World("Scene Transition Request Deduplication Smoke Test");

        try
        {
            Entity requestEntity = testWorld.EntityManager.CreateEntity();
            DynamicBuffer<GameSceneTransitionRequest> requests =
                testWorld.EntityManager.AddBuffer<GameSceneTransitionRequest>(requestEntity);
            GameSceneTransitionRequest playRequest = new GameSceneTransitionRequest
            {
                RequestType = GameSceneTransitionRequestType.LoadDefaultGameplay,
                Purpose = GameSceneTransitionPurpose.Standard
            };
            GameSceneTransitionRequest mainMenuRequest = new GameSceneTransitionRequest
            {
                RequestType = GameSceneTransitionRequestType.LoadMainMenu,
                Purpose = GameSceneTransitionPurpose.Standard,
                TargetSceneId = new FixedString64Bytes("MainMenu")
            };

            if (!GameSceneTransitionRequestUtility.TryAddUnique(requests, playRequest))
                throw new Exception("The first Play request was not appended.");

            if (GameSceneTransitionRequestUtility.TryAddUnique(requests, playRequest))
                throw new Exception("A duplicate Play request was appended.");

            if (!GameSceneTransitionRequestUtility.TryAddUnique(requests, mainMenuRequest) || requests.Length != 2)
                throw new Exception("A distinct scene request was suppressed by deduplication.");
        }
        finally
        {
            testWorld.Dispose();
        }

        Debug.Log("[GameSceneTransitionRequestDeduplicationSmokeTest] Request deduplication checks passed.");
    }
    #endregion

    #endregion
}
#endif
