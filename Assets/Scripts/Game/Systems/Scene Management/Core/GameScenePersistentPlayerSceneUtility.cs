using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;
using Hash128 = Unity.Entities.Hash128;

/// <summary>
/// Handles direct DOTS scene operations for the persistent player scene outside Unity scene loading.
/// </summary>
internal static class GameScenePersistentPlayerSceneUtility
{
    #region Methods

    #region Operation Collection
    /// <summary>
    /// Builds persistent player load and unload work for the active transition.
    /// </summary>
    /// <param name="scenes">Runtime scene definitions.</param>
    /// <param name="targetScene">Transition target scene definition.</param>
    /// <param name="reloadPersistentPlayer">True when restart should recreate the player entity scene.</param>
    /// <param name="preLoadUnloadScenes">Output scenes unloaded before persistent player reload.</param>
    /// <param name="loadScenes">Output persistent player scenes required by the target.</param>
    /// <param name="postLoadUnloadScenes">Output persistent player scenes unloaded after leaving gameplay.</param>
    public static void CollectOperations(DynamicBuffer<GameSceneDefinitionElement> scenes,
                                         GameSceneDefinitionElement targetScene,
                                         bool reloadPersistentPlayer,
                                         List<GameSceneDefinitionElement> preLoadUnloadScenes,
                                         List<GameSceneDefinitionElement> loadScenes,
                                         List<GameSceneDefinitionElement> postLoadUnloadScenes)
    {
        ClearOperationLists(preLoadUnloadScenes, loadScenes, postLoadUnloadScenes);

        bool targetNeedsPlayer = IsGameplayLikeScene(targetScene);

        for (int index = 0; index < scenes.Length; index++)
        {
            GameSceneDefinitionElement sceneDefinition = scenes[index];

            if (sceneDefinition.SceneKind != GameSceneKind.PersistentPlayer)
                continue;

            if (reloadPersistentPlayer)
                preLoadUnloadScenes.Add(sceneDefinition);

            if (targetNeedsPlayer)
            {
                loadScenes.Add(sceneDefinition);
                continue;
            }

            postLoadUnloadScenes.Add(sceneDefinition);
        }
    }

    /// <summary>
    /// Resolves whether one scene kind should have the persistent player available.
    /// </summary>
    /// <param name="sceneDefinition">Scene definition being inspected.</param>
    /// <returns>True when the scene represents playable gameplay space.</returns>
    public static bool IsGameplayLikeScene(GameSceneDefinitionElement sceneDefinition)
    {
        switch (sceneDefinition.SceneKind)
        {
            case GameSceneKind.Gameplay:
            case GameSceneKind.Test:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Clears operation lists while tolerating missing optional containers.
    /// </summary>
    /// <param name="preLoadUnloadScenes">Persistent player scenes unloaded before reload.</param>
    /// <param name="loadScenes">Persistent player scenes loaded for gameplay.</param>
    /// <param name="postLoadUnloadScenes">Persistent player scenes unloaded after gameplay.</param>
    private static void ClearOperationLists(List<GameSceneDefinitionElement> preLoadUnloadScenes,
                                            List<GameSceneDefinitionElement> loadScenes,
                                            List<GameSceneDefinitionElement> postLoadUnloadScenes)
    {
        preLoadUnloadScenes.Clear();
        loadScenes.Clear();
        postLoadUnloadScenes.Clear();
    }
    #endregion

    #region Load
    /// <summary>
    /// Advances direct DOTS scene loads one scene at a time.
    /// </summary>
    /// <param name="scenes">Ordered persistent player scenes to load.</param>
    /// <param name="operationIndex">Mutable index of the scene currently being processed.</param>
    /// <returns>True while a scene load is still in flight.</returns>
    public static bool TickLoadSteps(List<GameSceneDefinitionElement> scenes, ref int operationIndex)
    {
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
            return false;

        while (operationIndex < scenes.Count)
        {
            GameSceneDefinitionElement sceneDefinition = scenes[operationIndex];

            if (!TryResolveSceneGuid(sceneDefinition, out Hash128 sceneGuid))
            {
                operationIndex++;
                continue;
            }

            Entity sceneEntity = SceneSystem.GetSceneEntity(world.Unmanaged, sceneGuid);

            if (sceneEntity == Entity.Null)
            {
                SceneSystem.LoadSceneAsync(world.Unmanaged, sceneGuid, BuildLoadParameters());
                return true;
            }

            if (!SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity))
            {
                if (!world.EntityManager.HasComponent<RequestSceneLoaded>(sceneEntity))
                    SceneSystem.LoadSceneAsync(world.Unmanaged, sceneEntity, BuildLoadParameters());

                return true;
            }

            operationIndex++;
        }

        return false;
    }
    #endregion

    #region Unload
    /// <summary>
    /// Unloads direct DOTS scene content for persistent player scenes.
    /// </summary>
    /// <param name="scenes">Ordered persistent player scenes to unload.</param>
    /// <param name="operationIndex">Mutable index of the scene currently being processed.</param>
    /// <returns>False because SceneSystem unload is immediate while the active transition keeps gameplay paused.</returns>
    public static bool TickUnloadSteps(List<GameSceneDefinitionElement> scenes, ref int operationIndex)
    {
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
            return false;

        while (operationIndex < scenes.Count)
        {
            GameSceneDefinitionElement sceneDefinition = scenes[operationIndex];

            if (TryResolveSceneGuid(sceneDefinition, out Hash128 sceneGuid))
                SceneSystem.UnloadScene(world.Unmanaged, sceneGuid, SceneSystem.UnloadParameters.DestroyMetaEntities);

            operationIndex++;
        }

        return false;
    }
    #endregion

    #region State
    /// <summary>
    /// Checks whether one direct DOTS scene definition is loaded.
    /// </summary>
    /// <param name="sceneDefinition">Persistent player scene definition.</param>
    /// <returns>True when SceneSystem reports the entity scene as loaded.</returns>
    public static bool IsSceneLoaded(GameSceneDefinitionElement sceneDefinition)
    {
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
            return false;

        if (!TryResolveSceneGuid(sceneDefinition, out Hash128 sceneGuid))
            return false;

        Entity sceneEntity = SceneSystem.GetSceneEntity(world.Unmanaged, sceneGuid);

        if (sceneEntity == Entity.Null)
            return false;

        return SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Builds load parameters shared by transition-managed direct DOTS scenes.
    /// </summary>
    /// <returns>SceneSystem load parameters.</returns>
    private static SceneSystem.LoadParameters BuildLoadParameters()
    {
        return new SceneSystem.LoadParameters
        {
            Flags = SceneLoadFlags.BlockOnImport
        };
    }

    /// <summary>
    /// Converts the scene definition GUID string to an Entities Hash128.
    /// </summary>
    /// <param name="sceneDefinition">Scene definition with serialized asset GUID.</param>
    /// <param name="sceneGuid">Parsed scene GUID when valid.</param>
    /// <returns>True when the GUID can be used by SceneSystem.</returns>
    private static bool TryResolveSceneGuid(GameSceneDefinitionElement sceneDefinition, out Hash128 sceneGuid)
    {
        sceneGuid = default;
        FixedString64Bytes fixedGuid = sceneDefinition.SceneGuid;

        if (fixedGuid.Length <= 0)
            return false;

        sceneGuid = new Hash128(fixedGuid.ToString());
        return sceneGuid.IsValid;
    }
    #endregion

    #endregion
}
