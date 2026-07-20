using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralizes Unity layer names and masks used by gameplay camera routing.
/// </summary>
public static class GameSceneCameraLayerUtility
{
    #region Constants
    public const string EnvironmentLayerName = "Environment";
    public const string FadeTransitionLayerName = "SceneTransitionFade";
    public const string OutlineLayerName = "Outline";
    public const string PlayerTransitionLayerName = "PlayerTransition";
    public const string WallsLayerName = "Walls";
    public const string UiLayerName = "UI";
    public const int EnvironmentLayerIndex = 11;
    public const int WallsLayerIndex = 6;
    public const int UiLayerIndex = 5;
    public const int DefaultEnvironmentCullingMask = (1 << EnvironmentLayerIndex) | (1 << WallsLayerIndex);
    public const int DefaultUiCullingMask = 1 << UiLayerIndex;
    public const int DefaultGameplayCullingMask = ~((1 << EnvironmentLayerIndex) | (1 << WallsLayerIndex) | (1 << UiLayerIndex));
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the gameplay overlay mask by removing environment and configured excluded layers from all renderable layers.
    /// </summary>
    /// <param name="environmentMask">Layers already rendered by the post-processed environment base camera.</param>
    /// <param name="additionalExcludedMask">Extra layers that should not render in the gameplay overlay camera.</param>
    /// <returns>Derived culling mask for the gameplay overlay camera.</returns>
    public static int BuildGameplayCullingMask(int environmentMask, int additionalExcludedMask)
    {
        return ExcludeTransitionLayers(~(environmentMask | additionalExcludedMask));
    }

    /// <summary>
    /// Removes dedicated fade and player-only transition layers from a normal camera pass.
    /// </summary>
    /// <param name="cullingMask">Source camera culling mask.</param>
    /// <returns>Mask that cannot render either authored transition-only layer.</returns>
    public static int ExcludeTransitionLayers(int cullingMask)
    {
        if (TryResolveLayerMask(FadeTransitionLayerName, out int fadeMask))
            cullingMask &= ~fadeMask;

        if (TryResolveLayerMask(PlayerTransitionLayerName, out int playerMask))
            cullingMask &= ~playerMask;

        return cullingMask;
    }

    /// <summary>
    /// Applies one resolved Unity layer to an existing authored hierarchy without allocating temporary collections.
    /// </summary>
    /// <param name="root">Hierarchy root receiving the layer.</param>
    /// <param name="layerIndex">Resolved Unity layer index.</param>
    public static void ApplyLayerRecursively(Transform root, int layerIndex)
    {
        if (root == null || layerIndex < 0)
            return;

        root.gameObject.layer = layerIndex;

        for (int childIndex = 0; childIndex < root.childCount; childIndex++)
            ApplyLayerRecursively(root.GetChild(childIndex), layerIndex);
    }

    /// <summary>
    /// Moves each unique renderer GameObject under an Animator to one layer while preserving one exact original snapshot.
    /// </summary>
    /// <param name="animator">Animator hierarchy containing persistent player renderers.</param>
    /// <param name="targetLayerIndex">Resolved transition-only Unity layer index.</param>
    /// <param name="originalLayers">Snapshot map retained until transition teardown.</param>
    /// <returns>Number of unique GameObjects moved during this call.</returns>
    public static int MoveRendererObjectsToLayer(Animator animator,
                                                 int targetLayerIndex,
                                                 Dictionary<GameObject, int> originalLayers)
    {
        if (animator == null || targetLayerIndex < 0 || originalLayers == null)
            return 0;

        Renderer[] renderers = animator.GetComponentsInChildren<Renderer>(true);
        int movedCount = 0;

        // Deduplicate by GameObject because one object may own several Renderer components.
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];

            if (renderer == null)
                continue;

            GameObject rendererObject = renderer.gameObject;

            if (originalLayers.ContainsKey(rendererObject))
                continue;

            originalLayers.Add(rendererObject, rendererObject.layer);
            rendererObject.layer = targetLayerIndex;
            movedCount++;
        }

        return movedCount;
    }

    /// <summary>
    /// Restores every surviving renderer GameObject from one layer snapshot and clears the completed transition state.
    /// </summary>
    /// <param name="originalLayers">Snapshot map produced before renderer isolation.</param>
    /// <returns>Number of surviving GameObjects restored during this call.</returns>
    public static int RestoreRendererObjectLayers(Dictionary<GameObject, int> originalLayers)
    {
        if (originalLayers == null)
            return 0;

        int restoredCount = 0;

        // Unity destroyed-object semantics are handled before assigning the original layer.
        foreach (KeyValuePair<GameObject, int> rendererLayer in originalLayers)
        {
            if (rendererLayer.Key == null)
                continue;

            rendererLayer.Key.layer = rendererLayer.Value;
            restoredCount++;
        }

        originalLayers.Clear();
        return restoredCount;
    }

    /// <summary>
    /// Resolves whether two masks share at least one layer.
    /// </summary>
    /// <param name="firstMask">First Unity layer mask.</param>
    /// <param name="secondMask">Second Unity layer mask.</param>
    /// <returns>True when the masks overlap.</returns>
    public static bool HasLayerOverlap(int firstMask, int secondMask)
    {
        return (firstMask & secondMask) != 0;
    }

    /// <summary>
    /// Resolves a Unity layer mask from a layer name without allocating intermediate arrays.
    /// </summary>
    /// <param name="layerName">Unity layer name to resolve.</param>
    /// <param name="layerMask">Mask containing the resolved layer when available.</param>
    /// <returns>True when the layer name exists in Project Settings.</returns>
    public static bool TryResolveLayerMask(string layerName, out int layerMask)
    {
        layerMask = 0;

        if (string.IsNullOrWhiteSpace(layerName))
            return false;

        int layerIndex = LayerMask.NameToLayer(layerName);

        if (layerIndex < 0)
            return false;

        layerMask = 1 << layerIndex;
        return true;
    }
    #endregion

    #endregion
}
