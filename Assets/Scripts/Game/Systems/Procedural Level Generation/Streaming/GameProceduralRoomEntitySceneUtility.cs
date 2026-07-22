using System.Collections.Generic;
using Unity.Entities;
using Unity.Scenes;
using UnityEngine;

/// <summary>
/// Owns exact duplicate-capable DOTS scene operations and section discovery for transactional room instances.
/// </summary>
internal static class GameProceduralRoomEntitySceneUtility
{
    #region Methods

    #region Load
    /// <summary>
    /// Starts explicit NewInstance loads and advances them until every exact section instance is available.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager.</param>
    /// <param name="instance">Logical room instance whose entity scenes are streamed.</param>
    /// <returns>True when every section is loaded and its exact handle has been collected.</returns>
    public static bool TickLoad(EntityManager entityManager, GameProceduralRoomStreamInstance instance)
    {
        World world = entityManager.World;

        if (!instance.EntityLoadStarted)
        {
            if (!TryStartLoads(world, instance))
                return false;

            instance.EntityLoadStarted = true;
        }

        for (int sceneIndex = 0; sceneIndex < instance.EntitySceneHandles.Count; sceneIndex++)
        {
            Entity sceneEntity = instance.EntitySceneHandles[sceneIndex];

            if (!entityManager.Exists(sceneEntity) || !SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity))
                return false;
        }

        CollectSectionEntities(entityManager, instance);
        return true;
    }

    /// <summary>
    /// Creates independent DOTS scene entities so duplicate managed templates never share streaming ownership.
    /// </summary>
    /// <param name="world">Default ECS world receiving scene entities.</param>
    /// <param name="instance">Logical room instance whose managed SubScene components are inspected.</param>
    /// <returns>True when every explicit entity scene load was issued.</returns>
    private static bool TryStartLoads(World world, GameProceduralRoomStreamInstance instance)
    {
        if (world == null || !world.IsCreated || !instance.ManagedScene.IsValid())
            return false;

        GameObject[] roots = instance.ManagedScene.GetRootGameObjects();
        List<SubScene> subScenes = new List<SubScene>(2);
        int requestedSceneCount = 0;

        // Unity fills the caller list per hierarchy root, so consume each root before the next query replaces its contents.
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            subScenes.Clear();
            roots[rootIndex].GetComponentsInChildren(true, subScenes);

            for (int subSceneIndex = 0; subSceneIndex < subScenes.Count; subSceneIndex++)
            {
                SubScene subScene = subScenes[subSceneIndex];

                if (subScene == null || !subScene.SceneGUID.IsValid)
                    continue;

                if (subScene.AutoLoadScene)
                    return MarkLoadFailed(instance,
                                          "Room SubScenes must disable Auto Load Scene so each logical node can own a NewInstance handle.");

                SceneSystem.LoadParameters parameters = new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.NewInstance
                };
                instance.EntitySceneHandles.Add(SceneSystem.LoadSceneAsync(world.Unmanaged, subScene.SceneGUID, parameters));
                requestedSceneCount++;
            }
        }

        if (requestedSceneCount <= 0)
            return MarkLoadFailed(instance, "The managed room contains no valid explicit SubScene to stream.");

        return true;
    }

    /// <summary>
    /// Collects exact section entities used for spatial placement and instance-filtered gameplay queries.
    /// </summary>
    /// <param name="entityManager">Entity manager owning resolved section buffers.</param>
    /// <param name="instance">Logical room instance receiving exact section handles.</param>
    private static void CollectSectionEntities(EntityManager entityManager, GameProceduralRoomStreamInstance instance)
    {
        instance.SectionEntities.Clear();

        for (int sceneIndex = 0; sceneIndex < instance.EntitySceneHandles.Count; sceneIndex++)
        {
            Entity sceneEntity = instance.EntitySceneHandles[sceneIndex];

            if (!entityManager.Exists(sceneEntity) || !entityManager.HasBuffer<ResolvedSectionEntity>(sceneEntity))
                continue;

            DynamicBuffer<ResolvedSectionEntity> sections = entityManager.GetBuffer<ResolvedSectionEntity>(sceneEntity, true);

            for (int sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
                instance.SectionEntities.Add(sections[sectionIndex].SectionEntity);
        }
    }
    #endregion

    #region Unload
    /// <summary>
    /// Starts unloading every exact DOTS scene handle owned by one retired logical room.
    /// </summary>
    /// <param name="entityManager">Entity manager owning DOTS scene handles.</param>
    /// <param name="instance">Retired logical room instance.</param>
    public static void StartUnload(EntityManager entityManager, GameProceduralRoomStreamInstance instance)
    {
        if (instance.EntityUnloadStarted)
            return;

        for (int sceneIndex = 0; sceneIndex < instance.EntitySceneHandles.Count; sceneIndex++)
        {
            Entity sceneEntity = instance.EntitySceneHandles[sceneIndex];

            if (entityManager.Exists(sceneEntity))
                SceneSystem.UnloadScene(entityManager.World.Unmanaged,
                                        sceneEntity,
                                        SceneSystem.UnloadParameters.DestroyMetaEntities);
        }

        instance.EntityUnloadStarted = true;
    }

    /// <summary>
    /// Checks whether every exact DOTS scene entity owned by an unloading room has been destroyed.
    /// </summary>
    /// <param name="entityManager">Entity manager owning DOTS scene handles.</param>
    /// <param name="instance">Unloading logical room instance.</param>
    /// <returns>True when no DOTS scene handle remains alive.</returns>
    public static bool IsUnloadComplete(EntityManager entityManager, GameProceduralRoomStreamInstance instance)
    {
        for (int sceneIndex = 0; sceneIndex < instance.EntitySceneHandles.Count; sceneIndex++)
        {
            if (entityManager.Exists(instance.EntitySceneHandles[sceneIndex]))
                return false;
        }

        return true;
    }
    #endregion

    #region Diagnostics
    /// <summary>
    /// Records a failed entity-scene instance and emits one actionable runtime diagnostic.
    /// </summary>
    /// <param name="instance">Logical room instance that failed.</param>
    /// <param name="message">Actionable failure detail.</param>
    /// <returns>Always false for direct propagation by load helpers.</returns>
    private static bool MarkLoadFailed(GameProceduralRoomStreamInstance instance, string message)
    {
        instance.State = GameProceduralRoomStreamState.Failed;
        Debug.LogError("[ProceduralRoomStreaming] Node " + instance.NodeIndex + " failed: " + message);
        return false;
    }
    #endregion

    #endregion
}
