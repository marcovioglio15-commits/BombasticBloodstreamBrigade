using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Contains shared reference discovery and development diagnostics for the Player portrait HUD section.
/// </summary>
public static class HUDPlayerPortraitSectionUtility
{
    #region Methods

    #region Reference Discovery
    /// <summary>
    /// Resolves missing portrait container and Image references from the HUD hierarchy.
    /// </summary>
    /// <param name="searchRoot">HUD hierarchy root used for optional reference discovery.</param>
    /// <param name="rootObject">Current or resolved portrait section root object.</param>
    /// <param name="portraitImage">Current or resolved portrait frame Image.</param>
    /// <param name="portraitContainerName">Configured portrait container object name.</param>
    /// <param name="portraitImageName">Configured portrait Image object name.</param>
    /// <param name="defaultContainerName">Fallback portrait container object name.</param>
    /// <param name="defaultPortraitImageName">Fallback portrait Image object name.</param>
    public static void ResolveReferences(Transform searchRoot,
                                         ref GameObject rootObject,
                                         ref Image portraitImage,
                                         string portraitContainerName,
                                         string portraitImageName,
                                         string defaultContainerName,
                                         string defaultPortraitImageName)
    {
        if (searchRoot == null)
            return;

        string resolvedContainerName = string.IsNullOrWhiteSpace(portraitContainerName)
            ? defaultContainerName
            : portraitContainerName;
        string resolvedImageName = string.IsNullOrWhiteSpace(portraitImageName)
            ? defaultPortraitImageName
            : portraitImageName;

        if (rootObject == null)
        {
            Transform container = FindChildByName(searchRoot, resolvedContainerName);

            if (container != null)
                rootObject = container.gameObject;
        }

        if (portraitImage != null)
            return;

        Transform imageRoot = rootObject != null
            ? FindChildByName(rootObject.transform, resolvedImageName)
            : FindChildByName(searchRoot, resolvedImageName);

        if (imageRoot != null)
            portraitImage = imageRoot.GetComponent<Image>();
    }

    /// <summary>
    /// Finds the first child Transform with a matching name in the provided hierarchy.
    /// </summary>
    /// <param name="root">Hierarchy root to scan.</param>
    /// <param name="targetName">Child object name to match.</param>
    /// <returns>Matching Transform, or null when no object with the requested name exists.</returns>
    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        for (int childIndex = 0; childIndex < children.Length; childIndex++)
        {
            Transform child = children[childIndex];

            if (child != null && string.Equals(child.name, targetName, StringComparison.Ordinal))
                return child;
        }

        return null;
    }
    #endregion

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    #region Diagnostics
    /// <summary>
    /// Logs why the portrait runtime binding cannot use the player entity.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager used to inspect the player entity.</param>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    /// <param name="loggedMissingPlayerReference">Mutable guard flag for this diagnostic.</param>
    public static void LogMissingPlayerReference(EntityManager runtimeEntityManager,
                                                 Entity playerEntity,
                                                 ref bool loggedMissingPlayerReference)
    {
        if (loggedMissingPlayerReference)
            return;

        if (!runtimeEntityManager.Exists(playerEntity))
            return;

        if (!runtimeEntityManager.HasComponent<PlayerPortraitHudVisualReference>(playerEntity))
            LogDiagnosticOnce(ref loggedMissingPlayerReference,
                              "[HUDPlayerPortraitSection] Player entity is missing PlayerPortraitHudVisualReference. The active player bake does not include the new Portrait HUD config yet; reimport/rebake the player prefab or owner scene.");
    }

    /// <summary>
    /// Logs one diagnostic message once per HUD section instance.
    /// </summary>
    /// <param name="logged">Mutable guard flag for this diagnostic.</param>
    /// <param name="message">Diagnostic message.</param>
    public static void LogDiagnosticOnce(ref bool logged, string message)
    {
        if (logged)
            return;

        logged = true;
        Debug.LogWarning(message);
    }
    #endregion
#endif

    #endregion
}
