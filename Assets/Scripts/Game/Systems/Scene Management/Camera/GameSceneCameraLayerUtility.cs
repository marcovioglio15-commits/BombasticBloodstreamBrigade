using UnityEngine;

/// <summary>
/// Centralizes Unity layer names and masks used by gameplay camera routing.
/// </summary>
public static class GameSceneCameraLayerUtility
{
    #region Constants
    public const string EnvironmentLayerName = "Environment";
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
        return ~(environmentMask | additionalExcludedMask);
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
