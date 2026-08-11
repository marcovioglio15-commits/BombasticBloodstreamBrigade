using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Marks active reward drops for persistent room-clear attraction without creating a second collection path.
/// </summary>
public static class EnemyDropRoomClearAttractionUtility
{
    #region Constants
    private const float MaximumTransitionDeltaTime = 0.1f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Marks every currently active non-prefab drop so it remains attracted and consumable across room transitions.
    /// </summary>
    /// <param name="entityManager">Entity manager owning active reward drops.</param>
    /// <returns>The number of active drops marked for persistent attraction.</returns>
    public static int MarkActiveDrops(EntityManager entityManager)
    {
        EntityQuery query = entityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadWrite<EnemyExperienceDrop>(),
                ComponentType.ReadOnly<EnemyExperienceDropActive>()
            },
            None = new ComponentType[]
            {
                ComponentType.ReadOnly<Prefab>()
            }
        });
        NativeArray<Entity> dropEntities = default;

        try
        {
            dropEntities = query.ToEntityArray(Allocator.Temp);

            // Persist the room-clear policy on each drop so collection can outlive the source room and its pools.
            for (int dropIndex = 0; dropIndex < dropEntities.Length; dropIndex++)
            {
                EnemyExperienceDrop dropData = entityManager.GetComponentData<EnemyExperienceDrop>(dropEntities[dropIndex]);
                dropData.IsAttracting = 1;
                dropData.ConsumeWhenUnusable = 1;
                dropData.IsRoomClearAttraction = 1;
                entityManager.SetComponentData(dropEntities[dropIndex], dropData);
            }

            return dropEntities.Length;
        }
        finally
        {
            if (dropEntities.IsCreated)
                dropEntities.Dispose();

            query.Dispose();
        }
    }

    /// <summary>
    /// Resolves drop flight time so persistent room-clear attraction continues through transition-owned time-scale locks.
    /// </summary>
    /// <param name="scaledDeltaTime">Current ECS world delta time.</param>
    /// <param name="unscaledDeltaTime">Current Unity unscaled delta time.</param>
    /// <param name="isSceneTransitioning">True while scene management owns an active transition.</param>
    /// <param name="isRoomClearAttraction">True when the drop is committed to persistent room-clear attraction.</param>
    /// <returns>Safe movement delta for the selected drop policy.</returns>
    public static float ResolveDeltaTime(float scaledDeltaTime,
                                         float unscaledDeltaTime,
                                         bool isSceneTransitioning,
                                         bool isRoomClearAttraction)
    {
        float safeScaledDeltaTime = math.max(0f, scaledDeltaTime);

        if (!isSceneTransitioning || !isRoomClearAttraction)
            return safeScaledDeltaTime;

        return math.min(math.max(0f, unscaledDeltaTime), MaximumTransitionDeltaTime);
    }
    #endregion

    #endregion
}
