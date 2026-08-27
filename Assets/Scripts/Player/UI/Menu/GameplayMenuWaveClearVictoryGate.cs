using Unity.Entities;

/// <summary>
/// Caches the compact ECS query that keeps the victory menu behind the terminal wave-clear announcement.
/// </summary>
public sealed class GameplayMenuWaveClearVictoryGate
{
    #region Fields
    private World boundWorld;
    private EntityQuery presentationQuery;
    private bool initialized;
    #endregion

    #region Methods

    /// <summary>
    /// Resolves the current terminal-announcement gate and rebuilds its query only when the default world changes.
    /// </summary>
    /// <param name="world">Current gameplay ECS world.</param>
    /// <param name="entityManager">Entity manager belonging to the supplied world.</param>
    /// <returns>True while the victory menu must remain hidden.</returns>
    public bool IsBlocked(World world, EntityManager entityManager)
    {
        if (world == null || !world.IsCreated)
        {
            Invalidate();
            return false;
        }

        if (!initialized || !ReferenceEquals(boundWorld, world))
        {
            boundWorld = world;
            presentationQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GameHudWaveClearAnnouncementPresentationState>());
            initialized = true;
        }

        if (presentationQuery.CalculateEntityCount() != 1)
            return false;

        GameHudWaveClearAnnouncementPresentationState presentationState =
            entityManager.GetComponentData<GameHudWaveClearAnnouncementPresentationState>(
                presentationQuery.GetSingletonEntity());
        return presentationState.BlocksVictoryMenu != 0;
    }

    /// <summary>
    /// Invalidates cached world state when the owning menu leaves the active gameplay scene.
    /// </summary>
    public void Invalidate()
    {
        boundWorld = null;
        presentationQuery = default;
        initialized = false;
    }
    #endregion
}
