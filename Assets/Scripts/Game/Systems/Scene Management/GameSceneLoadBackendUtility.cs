using Unity.Collections;
using Unity.Entities;
using UnityEngine.SceneManagement;

/// <summary>
/// Provides shared helpers for resolving scene definitions and loaded Unity scene instances.
/// /params None.
/// /returns None.
/// </summary>
public static class GameSceneLoadBackendUtility
{
    #region Methods

    #region Scene Definition Lookup
    /// <summary>
    /// Finds a scene definition by stable scene ID.
    /// /params scenes Runtime scene definition buffer.
    /// /params sceneId Stable scene ID to find.
    /// /params sceneDefinition Matching scene definition when available.
    /// /returns True when a matching scene definition exists.
    /// </summary>
    public static bool TryFindScene(DynamicBuffer<GameSceneDefinitionElement> scenes,
                                    FixedString64Bytes sceneId,
                                    out GameSceneDefinitionElement sceneDefinition)
    {
        sceneDefinition = default;

        if (sceneId.Length <= 0)
            return false;

        for (int index = 0; index < scenes.Length; index++)
        {
            GameSceneDefinitionElement candidate = scenes[index];

            if (!candidate.SceneId.Equals(sceneId))
                continue;

            sceneDefinition = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Finds the scene that follows the provided active scene ID in the configured order.
    /// /params scenes Runtime scene definition buffer.
    /// /params activeSceneId Current managed scene ID.
    /// /params sceneDefinition Next scene definition when available.
    /// /returns True when an ordered next scene exists.
    /// </summary>
    public static bool TryFindNextScene(DynamicBuffer<GameSceneDefinitionElement> scenes,
                                        FixedString64Bytes activeSceneId,
                                        out GameSceneDefinitionElement sceneDefinition)
    {
        sceneDefinition = default;

        if (!TryFindScene(scenes, activeSceneId, out GameSceneDefinitionElement activeScene))
            return false;

        int nextOrderIndex = activeScene.OrderIndex + 1;

        for (int index = 0; index < scenes.Length; index++)
        {
            GameSceneDefinitionElement candidate = scenes[index];

            if (candidate.OrderIndex != nextOrderIndex)
                continue;

            sceneDefinition = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Finds the companion UI scene referenced by one scene definition.
    /// /params scenes Runtime scene definition buffer.
    /// /params sceneDefinition Scene definition that may reference a companion UI scene.
    /// /params companionSceneDefinition Matching companion scene when available.
    /// /returns True when a valid companion scene definition exists.
    /// </summary>
    public static bool TryFindCompanionScene(DynamicBuffer<GameSceneDefinitionElement> scenes,
                                             GameSceneDefinitionElement sceneDefinition,
                                             out GameSceneDefinitionElement companionSceneDefinition)
    {
        companionSceneDefinition = default;

        if (sceneDefinition.CompanionUiSceneId.Length <= 0)
            return false;

        return TryFindScene(scenes, sceneDefinition.CompanionUiSceneId, out companionSceneDefinition);
    }
    #endregion

    #region Transition Lookup
    /// <summary>
    /// Finds one transition by stable transition ID.
    /// /params transitions Runtime transition definition buffer.
    /// /params transitionId Stable transition ID to find.
    /// /params transition Matching transition when available.
    /// /returns True when a matching transition exists.
    /// </summary>
    public static bool TryFindTransition(DynamicBuffer<GameSceneTransitionElement> transitions,
                                         FixedString64Bytes transitionId,
                                         out GameSceneTransitionElement transition)
    {
        transition = default;

        if (transitionId.Length <= 0)
            return false;

        for (int index = 0; index < transitions.Length; index++)
        {
            GameSceneTransitionElement candidate = transitions[index];

            if (!candidate.TransitionId.Equals(transitionId))
                continue;

            transition = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Finds the highest-priority transition associated with one trigger ID.
    /// /params transitions Runtime transition definition buffer.
    /// /params triggerId Trigger ID submitted by a transition volume.
    /// /params transition Matching transition when available.
    /// /returns True when a matching trigger transition exists.
    /// </summary>
    public static bool TryFindTransitionForTrigger(DynamicBuffer<GameSceneTransitionElement> transitions,
                                                   FixedString64Bytes triggerId,
                                                   out GameSceneTransitionElement transition)
    {
        transition = default;

        if (triggerId.Length <= 0)
            return false;

        bool found = false;

        for (int index = 0; index < transitions.Length; index++)
        {
            GameSceneTransitionElement candidate = transitions[index];

            if (candidate.TransitionMode != GameSceneTransitionMode.TriggerVolume)
                continue;

            if (!candidate.TriggerId.Equals(triggerId))
                continue;

            if (!found || candidate.Priority > transition.Priority)
                transition = candidate;

            found = true;
        }

        return found;
    }
    #endregion

    #region Unity Scene Lookup
    /// <summary>
    /// Resolves a loaded Unity scene from a runtime scene definition.
    /// /params sceneDefinition Runtime scene definition.
    /// /returns Loaded Unity scene or an invalid scene when not found.
    /// </summary>
    public static Scene ResolveLoadedScene(GameSceneDefinitionElement sceneDefinition)
    {
        string scenePath = sceneDefinition.ScenePath.ToString();

        if (!string.IsNullOrWhiteSpace(scenePath))
        {
            Scene sceneByPath = SceneManager.GetSceneByPath(scenePath);

            if (sceneByPath.IsValid())
                return sceneByPath;
        }

        string sceneName = sceneDefinition.SceneName.ToString();

        if (!string.IsNullOrWhiteSpace(sceneName))
            return SceneManager.GetSceneByName(sceneName);

        return default;
    }
    #endregion

    #endregion
}
