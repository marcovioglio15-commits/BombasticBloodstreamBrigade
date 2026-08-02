using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Scenes;

/// <summary>
/// Centralizes safe scene-clone preparation for the embedded Waves preview renderer.
/// </summary>
internal static class GameWavesPreviewSceneUtility
{
    #region Methods

    #region Scene Clone Methods
    /// <summary>
    /// Reports whether one root owns any SubScene authoring component that must stay outside render-only clones.
    /// </summary>
    /// <param name="root">Source scene root inspected before cloning.</param>
    /// <returns>True when the root or one of its descendants owns a SubScene component.</returns>
    public static bool ContainsSubScene(GameObject root)
    {
        return root.GetComponentInChildren<SubScene>(true) != null;
    }

    /// <summary>
    /// Reports whether any currently loaded scene already owns a SubScene component for one scene asset.
    /// </summary>
    /// <param name="subScenePath">Project-relative SubScene asset path.</param>
    /// <returns>True when opening another managed scene would register a duplicate SubScene reference.</returns>
    public static bool IsSubSceneReferencedByLoadedScene(string subScenePath)
    {
        if (string.IsNullOrWhiteSpace(subScenePath))
            return false;

        // Inspect every loaded scene because the duplicate can originate from any open room or preview.
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);

            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                SubScene[] subScenes = roots[rootIndex].GetComponentsInChildren<SubScene>(true);

                for (int subSceneIndex = 0; subSceneIndex < subScenes.Length; subSceneIndex++)
                {
                    if (subScenes[subSceneIndex].SceneAsset != null &&
                        string.Equals(AssetDatabase.GetAssetPath(subScenes[subSceneIndex].SceneAsset),
                                      subScenePath,
                                      StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Disables cloned scene behaviors so preview rendering cannot execute room gameplay or camera logic.
    /// </summary>
    /// <param name="root">Cloned scene root moved into the isolated preview scene.</param>
    public static void DisablePreviewBehaviours(GameObject root)
    {
        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);

        for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
            behaviours[behaviourIndex].enabled = false;

        AudioListener[] listeners = root.GetComponentsInChildren<AudioListener>(true);

        for (int listenerIndex = 0; listenerIndex < listeners.Length; listenerIndex++)
            listeners[listenerIndex].enabled = false;
    }
    #endregion

    #region Bounds Methods
    /// <summary>
    /// Resolves the highest scene-bound projection above the grid plane without allocating corner arrays.
    /// </summary>
    /// <param name="bounds">Aggregate renderer bounds for the previewed room.</param>
    /// <param name="gridCenter">World-space center of the spawner grid.</param>
    /// <param name="gridUp">World-space normal of the spawner grid.</param>
    /// <returns>Largest projected scene extent above the grid center.</returns>
    public static float ResolveMaximumBoundsProjection(Bounds bounds,
                                                       Vector3 gridCenter,
                                                       Vector3 gridUp)
    {
        Vector3 extents = bounds.extents;
        float centerProjection = Vector3.Dot(bounds.center - gridCenter, gridUp);
        float projectedExtents = Mathf.Abs(extents.x * gridUp.x) +
                                 Mathf.Abs(extents.y * gridUp.y) +
                                 Mathf.Abs(extents.z * gridUp.z);
        return Mathf.Max(0f, centerProjection + projectedExtents);
    }

    /// <summary>
    /// Expands aggregate bounds with every renderer owned by one cloned root.
    /// </summary>
    /// <param name="root">Cloned root whose renderers are inspected.</param>
    /// <param name="bounds">Aggregate scene bounds.</param>
    /// <param name="hasBounds">Whether aggregate bounds have been initialized.</param>
    public static void EncapsulateRenderers(GameObject root, ref Bounds bounds, ref bool hasBounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            if (!hasBounds)
            {
                bounds = renderers[rendererIndex].bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(renderers[rendererIndex].bounds);
        }
    }
    #endregion

    #endregion
}
