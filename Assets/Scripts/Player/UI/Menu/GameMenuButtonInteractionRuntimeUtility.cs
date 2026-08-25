using Unity.Entities;

/// <summary>
/// Resolves baked menu interaction profiles for preauthored button relays with explicit menu categories.
/// </summary>
public static class GameMenuButtonInteractionRuntimeUtility
{
    #region Fields
    private static readonly GameUiMenuButtonInteractionElement[] CachedInteractions =
        new GameUiMenuButtonInteractionElement[(int)GameUiMenuKind.RuntimeTools + 1];
    private static readonly bool[] CachedInteractionStates = new bool[(int)GameUiMenuKind.RuntimeTools + 1];

    private static World cachedWorld;
    private static bool cacheInitialized;
    #endregion

    #region Methods

    #region Lookup
    /// <summary>
    /// Finds one menu interaction profile in the current ECS HUD singleton buffer.
    /// </summary>
    /// <param name="menuKind">Concrete menu category requested by the button relay.</param>
    /// <param name="interaction">Matching baked profile when available.</param>
    /// <returns>True when exactly one HUD buffer exists and contains the requested profile.</returns>
    public static bool TryResolve(GameUiMenuKind menuKind, out GameUiMenuButtonInteractionElement interaction)
    {
        interaction = default;

        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
            return false;

        if (!ReferenceEquals(cachedWorld, world))
            ResetCache(world);

        if (!cacheInitialized && !TryBuildCache(world.EntityManager))
            return false;

        int menuIndex = (int)menuKind;

        if (menuIndex < 0 || menuIndex >= CachedInteractions.Length || !CachedInteractionStates[menuIndex])
            return false;

        interaction = CachedInteractions[menuIndex];
        return true;
    }

    /// <summary>
    /// Rebuilds the shared immutable menu-profile cache once for the current ECS world.
    /// </summary>
    /// <param name="entityManager">Entity manager expected to own one menu-profile buffer.</param>
    /// <returns>True when exactly one buffer was found and cached.</returns>
    private static bool TryBuildCache(EntityManager entityManager)
    {
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameUiMenuButtonInteractionElement>());

        if (query.CalculateEntityCount() != 1)
        {
            query.Dispose();
            return false;
        }

        Entity configEntity = query.GetSingletonEntity();
        query.Dispose();
        DynamicBuffer<GameUiMenuButtonInteractionElement> interactions =
            entityManager.GetBuffer<GameUiMenuButtonInteractionElement>(configEntity, true);

        for (int profileIndex = 0; profileIndex < interactions.Length; profileIndex++)
        {
            GameUiMenuButtonInteractionElement candidate = interactions[profileIndex];
            int menuIndex = (int)candidate.MenuKind;

            if (menuIndex < 0 || menuIndex >= CachedInteractions.Length)
                continue;

            CachedInteractions[menuIndex] = candidate;
            CachedInteractionStates[menuIndex] = true;
        }

        cacheInitialized = true;
        return true;
    }

    /// <summary>
    /// Clears cached menu profiles when the default ECS world changes.
    /// </summary>
    /// <param name="world">New default ECS world.</param>
    private static void ResetCache(World world)
    {
        cachedWorld = world;
        cacheInitialized = false;

        for (int menuIndex = 0; menuIndex < CachedInteractionStates.Length; menuIndex++)
        {
            CachedInteractionStates[menuIndex] = false;
            CachedInteractions[menuIndex] = default;
        }
    }
    #endregion

    #endregion
}
