using Unity.Entities;

/// <summary>
/// Bridges managed presentation events into the authoritative ECS audio request buffer without per-frame polling.
/// </summary>
public static class GameAudioManagedEventRequestUtility
{
    #region Fields
    private static World cachedWorld;
    private static Entity cachedAudioEntity;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Enqueues one non-positioned audio event from managed UI or presentation code.
    /// </summary>
    /// <param name="eventId">Stable audio event identifier to enqueue.</param>
    /// <returns>True when the current world contains exactly one writable audio request buffer.</returns>
    public static bool TryEnqueueGlobal(GameAudioEventId eventId)
    {
        if (eventId == GameAudioEventId.None)
            return false;

        World world = World.DefaultGameObjectInjectionWorld;

        if (!TryResolveAudioEntity(world, out Entity audioEntity))
            return false;

        DynamicBuffer<GameAudioEventRequest> requests = world.EntityManager.GetBuffer<GameAudioEventRequest>(audioEntity);
        GameAudioEventRequestUtility.EnqueueGlobal(requests, eventId);
        return true;
    }
    #endregion

    #region Entity Resolution
    /// <summary>
    /// Resolves and caches the single audio entity for the active default world.
    /// </summary>
    /// <param name="world">Current default ECS world.</param>
    /// <param name="audioEntity">Resolved entity that owns the request buffer.</param>
    /// <returns>True when exactly one valid audio request buffer is available.</returns>
    private static bool TryResolveAudioEntity(World world, out Entity audioEntity)
    {
        audioEntity = Entity.Null;

        if (world == null || !world.IsCreated)
        {
            ResetCache(null);
            return false;
        }

        if (!ReferenceEquals(cachedWorld, world))
            ResetCache(world);

        EntityManager entityManager = world.EntityManager;

        if (cachedAudioEntity != Entity.Null &&
            entityManager.Exists(cachedAudioEntity) &&
            entityManager.HasBuffer<GameAudioEventRequest>(cachedAudioEntity))
        {
            audioEntity = cachedAudioEntity;
            return true;
        }

        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<GameAudioEventRequest>());
        int entityCount = query.CalculateEntityCount();

        if (entityCount == 1)
        {
            cachedAudioEntity = query.GetSingletonEntity();
            audioEntity = cachedAudioEntity;
        }

        query.Dispose();
        return entityCount == 1;
    }

    /// <summary>
    /// Clears cached entity state when the default world changes or becomes unavailable.
    /// </summary>
    /// <param name="world">New default world, or null while no world is active.</param>
    private static void ResetCache(World world)
    {
        cachedWorld = world;
        cachedAudioEntity = Entity.Null;
    }
    #endregion

    #endregion
}
