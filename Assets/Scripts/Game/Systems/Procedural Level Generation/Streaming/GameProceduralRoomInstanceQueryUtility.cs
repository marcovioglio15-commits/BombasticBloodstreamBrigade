using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;

/// <summary>
/// Filters gameplay queries through exact active room section handles so duplicate templates remain unambiguous.
/// </summary>
public static class GameProceduralRoomInstanceQueryUtility
{
    #region Methods

    #region Query
    /// <summary>
    /// Collects query matches from the exact active room sections, with a global fallback for compatibility streaming.
    /// </summary>
    /// <param name="query">Query that includes SceneTag and the required gameplay components.</param>
    /// <param name="destination">Temporary entity list receiving exact active-room matches.</param>
    public static void CollectActiveRoomEntities(EntityQuery query, ref NativeList<Entity> destination)
    {
        destination.Clear();

        if (!GameProceduralRoomStreamingRuntimeUtility.TryGetActiveInstance(out GameProceduralRoomStreamInstance instance))
        {
            using NativeArray<Entity> globalEntities = query.ToEntityArray(Allocator.Temp);
            destination.AddRange(globalEntities);
            return;
        }

        CollectRoomInstanceEntities(instance, query, ref destination);
    }

    /// <summary>
    /// Collects query matches from one exact room instance regardless of whether it is staged, active or retired.
    /// </summary>
    /// <param name="instance">Exact room instance whose section handles constrain the query.</param>
    /// <param name="query">Query that includes SceneTag and the required room components.</param>
    /// <param name="destination">Temporary entity list receiving exact instance matches.</param>
    internal static void CollectRoomInstanceEntities(GameProceduralRoomStreamInstance instance,
                                                      EntityQuery query,
                                                      ref NativeList<Entity> destination)
    {
        destination.Clear();

        if (instance == null)
            return;

        // Apply every exact section handle independently because SceneTag is a shared component filter.
        for (int sectionIndex = 0; sectionIndex < instance.SectionEntities.Count; sectionIndex++)
        {
            query.SetSharedComponentFilter(new SceneTag
            {
                SceneEntity = instance.SectionEntities[sectionIndex]
            });
            using NativeArray<Entity> sectionEntities = query.ToEntityArray(Allocator.Temp);
            destination.AddRange(sectionEntities);
        }

        query.ResetFilter();
    }

    /// <summary>
    /// Checks whether one scene-owned entity belongs to an exact active room section.
    /// </summary>
    /// <param name="entityManager">Entity manager owning SceneTag data.</param>
    /// <param name="entity">Scene-owned entity to inspect.</param>
    /// <returns>True for compatibility mode or when the entity belongs to the transactional active instance.</returns>
    public static bool IsEntityInActiveRoom(EntityManager entityManager, Entity entity)
    {
        if (!GameProceduralRoomStreamingRuntimeUtility.TryGetActiveInstance(out GameProceduralRoomStreamInstance instance))
            return true;

        if (!entityManager.Exists(entity) || !entityManager.HasComponent<SceneTag>(entity))
            return false;

        SceneTag sceneTag = entityManager.GetSharedComponent<SceneTag>(entity);

        for (int sectionIndex = 0; sectionIndex < instance.SectionEntities.Count; sectionIndex++)
        {
            if (instance.SectionEntities[sectionIndex] == sceneTag.SceneEntity)
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
