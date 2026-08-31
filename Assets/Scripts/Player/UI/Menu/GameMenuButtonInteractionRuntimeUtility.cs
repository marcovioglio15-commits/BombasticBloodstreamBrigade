using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Resolves baked menu interaction profiles for preauthored button relays with explicit menu categories.
/// </summary>
public static class GameMenuButtonInteractionRuntimeUtility
{
    #region Fields
    private static readonly GameUiMenuButtonInteractionElement[] CachedInteractions =
        new GameUiMenuButtonInteractionElement[(int)GameUiMenuKind.RuntimeTools + 1];
    private static readonly bool[] CachedInteractionStates = new bool[(int)GameUiMenuKind.RuntimeTools + 1];
    private static readonly List<GameUiButtonImageContentElement> CachedImageContents =
        new List<GameUiButtonImageContentElement>();

    private static World cachedWorld;
    private static bool cacheInitialized;
    private static int lastCacheBuildAttemptFrame = -1;
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

        if (!TryEnsureCache(world))
            return false;

        int menuIndex = (int)menuKind;

        if (menuIndex < 0 || menuIndex >= CachedInteractions.Length || !CachedInteractionStates[menuIndex])
            return false;

        interaction = CachedInteractions[menuIndex];
        return true;
    }

    /// <summary>
    /// Finds one baked image-content mapping after the shared HUD cache has been initialized.
    /// </summary>
    /// <param name="menuKind">Menu category owning the button.</param>
    /// <param name="buttonId">Stable ID authored on the preauthored relay.</param>
    /// <param name="content">Matching state sprites and tints when available.</param>
    /// <returns>True when one exact menu and button ID mapping exists.</returns>
    public static bool TryResolveImageContent(GameUiMenuKind menuKind,
                                              string buttonId,
                                              out GameUiButtonImageContentElement content)
    {
        content = default;

        if (string.IsNullOrWhiteSpace(buttonId))
            return false;

        World world = World.DefaultGameObjectInjectionWorld;

        if (!TryEnsureCache(world))
            return false;

        string normalizedButtonId = buttonId.Trim();

        for (int contentIndex = 0; contentIndex < CachedImageContents.Count; contentIndex++)
        {
            GameUiButtonImageContentElement candidate = CachedImageContents[contentIndex];

            if (candidate.MenuKind == menuKind &&
                string.Equals(candidate.ButtonId.ToString(), normalizedButtonId, StringComparison.Ordinal))
            {
                content = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves or rebuilds the current world's immutable menu cache at most once per rendered frame.
    /// </summary>
    /// <param name="world">Current default ECS world, or null before DOTS initialization.</param>
    /// <returns>True when the interaction and image-content buffers are ready for lookup.</returns>
    private static bool TryEnsureCache(World world)
    {
        if (world == null || !world.IsCreated)
            return false;

        if (!ReferenceEquals(cachedWorld, world))
            ResetCache(world);

        if (cacheInitialized)
            return true;

        int currentFrame = Time.frameCount;

        if (lastCacheBuildAttemptFrame == currentFrame)
            return false;

        lastCacheBuildAttemptFrame = currentFrame;
        return TryBuildCache(world.EntityManager);
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

        CachedImageContents.Clear();

        if (entityManager.HasBuffer<GameUiButtonImageContentElement>(configEntity))
        {
            DynamicBuffer<GameUiButtonImageContentElement> imageContents =
                entityManager.GetBuffer<GameUiButtonImageContentElement>(configEntity, true);

            for (int contentIndex = 0; contentIndex < imageContents.Length; contentIndex++)
                CachedImageContents.Add(imageContents[contentIndex]);
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
        lastCacheBuildAttemptFrame = -1;
        CachedImageContents.Clear();

        for (int menuIndex = 0; menuIndex < CachedInteractionStates.Length; menuIndex++)
        {
            CachedInteractionStates[menuIndex] = false;
            CachedInteractions[menuIndex] = default;
        }
    }
    #endregion

    #endregion
}
