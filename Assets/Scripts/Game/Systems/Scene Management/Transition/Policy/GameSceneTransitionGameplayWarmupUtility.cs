using Unity.Collections;
using Unity.Entities;
using Unity.Physics;

/// <summary>
/// Resolves expensive gameplay services that must be prepared before a loaded scene becomes visible and interactive.
/// </summary>
internal static class GameSceneTransitionGameplayWarmupUtility
{
    #region Methods

    #region Audio Readiness
    /// <summary>
    /// Prepares the configured FMOD music bank and event while the transition overlay is still opaque, preventing
    /// synchronous bank work from reaching the first visible gameplay frame.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager containing the optional audio singleton.</param>
    /// <returns>True when audio is absent, disabled or prepared for reveal playback.</returns>
    public static bool IsAudioReady(EntityManager entityManager)
    {
        EntityQuery audioQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameAudioRuntimeConfig>());

        try
        {
            if (audioQuery.CalculateEntityCount() != 1)
                return true;

            GameAudioRuntimeConfig audioConfig = audioQuery.GetSingleton<GameAudioRuntimeConfig>();

            if (audioConfig.Enabled == 0 ||
                audioConfig.BackgroundMusicEnabled == 0 ||
                audioConfig.BackgroundMusicAutoStart == 0)
            {
                return true;
            }

            return GameAudioFmodRuntimeUtility.PrepareBackgroundMusic(audioConfig.BackgroundMusicEventPath.ToString(),
                                                                      audioConfig.BackgroundMusicBankName.ToString(),
                                                                      audioConfig.LogMissingEventPaths != 0);
        }
        finally
        {
            audioQuery.Dispose();
        }
    }
    #endregion

    #region Enemy Navigation Readiness
    /// <summary>
    /// Verifies that the shared enemy navigation grid already represents the target room's exported wall layout.
    /// The rebuild system runs outside the gameplay-paused enemy group, so this barrier keeps its full grid build
    /// behind the opaque transition instead of deferring it to the release frame.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager owning target physics, spawners and navigation state.</param>
    /// <returns>True when no navigation layout is needed or the target layout has already been rebuilt.</returns>
    public static bool IsEnemyNavigationReady(EntityManager entityManager)
    {
        EntityQuery spawnerQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EnemySpawnerState>());
        NativeArray<Entity> spawnerEntities = default;

        try
        {
            spawnerEntities = spawnerQuery.ToEntityArray(Allocator.Temp);
            bool hasActiveSpawner = false;

            // A room without active spawners does not need the shared enemy navigation grid before reveal.
            for (int spawnerIndex = 0; spawnerIndex < spawnerEntities.Length; spawnerIndex++)
            {
                if (!GameProceduralRoomInstanceQueryUtility.IsEntityInActiveRoom(entityManager,
                                                                                 spawnerEntities[spawnerIndex]))
                {
                    continue;
                }

                hasActiveSpawner = true;
                break;
            }

            if (!hasActiveSpawner)
                return true;
        }
        finally
        {
            if (spawnerEntities.IsCreated)
                spawnerEntities.Dispose();

            spawnerQuery.Dispose();
        }

        int wallsLayerMask = ResolveWallsLayerMask(entityManager);

        if (wallsLayerMask == 0)
            return true;

        EntityQuery physicsQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());

        try
        {
            if (physicsQuery.CalculateEntityCount() != 1)
                return false;

            PhysicsWorldSingleton physicsWorldSingleton = physicsQuery.GetSingleton<PhysicsWorldSingleton>();

            if (!EnemyNavigationFlowFieldUtility.TryCollectStaticWallBounds(in physicsWorldSingleton,
                                                                             wallsLayerMask,
                                                                             out Aabb _,
                                                                             out uint staticLayoutHash))
            {
                return true;
            }

            return DoesNavigationGridMatch(entityManager, staticLayoutHash);
        }
        finally
        {
            physicsQuery.Dispose();
        }
    }

    /// <summary>
    /// Resolves the authored wall collision mask, preferring the baked player-world configuration when available.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager containing the optional world-layer singleton.</param>
    /// <returns>Collision mask used by navigation wall collection.</returns>
    private static int ResolveWallsLayerMask(EntityManager entityManager)
    {
        int wallsLayerMask = WorldWallCollisionUtility.ResolveWallsLayerMask();
        EntityQuery worldLayersQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerWorldLayersConfig>());

        try
        {
            if (worldLayersQuery.CalculateEntityCount() != 1)
                return wallsLayerMask;

            PlayerWorldLayersConfig worldLayersConfig = worldLayersQuery.GetSingleton<PlayerWorldLayersConfig>();
            return worldLayersConfig.WallsLayerMask != 0
                ? worldLayersConfig.WallsLayerMask
                : wallsLayerMask;
        }
        finally
        {
            worldLayersQuery.Dispose();
        }
    }

    /// <summary>
    /// Compares the initialized navigation singleton with the wall-layout hash collected from target physics.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager containing the navigation-grid singleton.</param>
    /// <param name="staticLayoutHash">Hash of the currently loaded target-room walls.</param>
    /// <returns>True when the navigation grid is initialized for the supplied layout hash.</returns>
    private static bool DoesNavigationGridMatch(EntityManager entityManager, uint staticLayoutHash)
    {
        EntityQuery navigationQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EnemyNavigationGridTag>(),
                                                                       ComponentType.ReadOnly<EnemyNavigationGridState>());

        try
        {
            if (navigationQuery.CalculateEntityCount() != 1)
                return false;

            EnemyNavigationGridState navigationState = navigationQuery.GetSingleton<EnemyNavigationGridState>();
            return navigationState.Initialized != 0 &&
                   navigationState.StaticLayoutHash == staticLayoutHash;
        }
        finally
        {
            navigationQuery.Dispose();
        }
    }
    #endregion

    #endregion
}
