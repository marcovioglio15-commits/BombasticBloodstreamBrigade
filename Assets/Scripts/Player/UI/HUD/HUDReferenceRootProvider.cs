using UnityEngine;

/// <summary>
/// Scene component that owns the optional HUD hierarchy root used by sections for one-time reference discovery.
/// </summary>
[DisallowMultipleComponent]
public sealed class HUDReferenceRootProvider : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Reference Discovery")]
    [Tooltip("Optional scene root used by HUD sections to auto-discover portrait and growth sequence containers.")]
    [SerializeField] private Transform referenceSearchRoot;

    [Tooltip("Fallback GameObject name used to resolve the HUD reference search root when no explicit root is assigned.")]
    [SerializeField] private string referenceSearchRootName = "CanvasStyled";
    #endregion

    private Transform resolvedReferenceSearchRoot;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the hierarchy root used by nested HUD sections for one-time reference discovery.
    /// </summary>
    /// <param name="ownerTransform">Transform used as the final fallback when no canvas can be resolved.</param>
    /// <returns>Configured root, resolved canvas root, or owner transform as a final fallback.</returns>
    public Transform Resolve(Transform ownerTransform)
    {
        if (resolvedReferenceSearchRoot != null)
            return resolvedReferenceSearchRoot;

        if (referenceSearchRoot != null)
        {
            resolvedReferenceSearchRoot = referenceSearchRoot;
            return resolvedReferenceSearchRoot;
        }

        Transform namedRoot = ResolveNamedHudReferenceSearchRoot();

        if (namedRoot != null)
        {
            resolvedReferenceSearchRoot = namedRoot;
            return resolvedReferenceSearchRoot;
        }

        Canvas parentCanvas = GetComponentInParent<Canvas>(true);

        if (parentCanvas != null)
        {
            resolvedReferenceSearchRoot = parentCanvas.transform;
            return resolvedReferenceSearchRoot;
        }

        Canvas sceneCanvas = ResolveSceneCanvasReferenceRoot();

        if (sceneCanvas != null)
        {
            resolvedReferenceSearchRoot = sceneCanvas.transform;
            return resolvedReferenceSearchRoot;
        }

        resolvedReferenceSearchRoot = ownerTransform;
        return resolvedReferenceSearchRoot;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the configured HUD root name from active scene objects.
    /// </summary>
    /// <returns>Named root transform, or null when no matching object is active.</returns>
    private Transform ResolveNamedHudReferenceSearchRoot()
    {
        if (string.IsNullOrWhiteSpace(referenceSearchRootName))
            return null;

        GameObject namedRootObject = GameObject.Find(referenceSearchRootName);

        if (namedRootObject == null)
            return null;

        return namedRootObject.transform;
    }

    /// <summary>
    /// Resolves a scene canvas fallback when the HUD manager is authored outside the canvas hierarchy.
    /// </summary>
    /// <returns>First active canvas in the scene, or null when none is available.</returns>
    private static Canvas ResolveSceneCanvasReferenceRoot()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int canvasIndex = 0; canvasIndex < canvases.Length; canvasIndex++)
        {
            Canvas canvas = canvases[canvasIndex];

            if (canvas != null && canvas.gameObject.activeInHierarchy)
                return canvas;
        }

        return canvases.Length > 0 ? canvases[0] : null;
    }
    #endregion

    #endregion
}
