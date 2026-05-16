using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Validates enemy projectile prefab render settings that keep hostile bullets readable over dense enemy crowds.
/// </summary>
public static class EnemyProjectileVisualOrderValidationUtility
{
    #region Constants
    public const int MinimumEnemyProjectileSortingOrder = 16000;
    public const int MinimumEnemyProjectileRenderQueue = (int)RenderQueue.Overlay;
    private const string depthTestPropertyName = "_ZTest";
    private const string depthWritePropertyName = "_ZWrite";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Emits bake-time warnings when an enemy projectile prefab can be hidden behind enemy renderers.
    /// </summary>
    /// <param name="projectilePrefabObject">Projectile prefab resolved by the enemy Shooter payload.</param>
    /// <param name="context">Authoring object used as the warning context.</param>
    public static void ValidateProjectilePrefab(GameObject projectilePrefabObject, Object context)
    {
        if (projectilePrefabObject == null)
            return;

        Renderer[] renderers = projectilePrefabObject.GetComponentsInChildren<Renderer>(true);

        if (renderers == null || renderers.Length <= 0)
        {
            Debug.LogWarning(string.Format("[EnemyProjectileVisualOrder] Projectile prefab '{0}' has no Renderer children. Enemy bullets will be invisible.", projectilePrefabObject.name), context);
            return;
        }

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];

            if (renderer == null)
                continue;

            ValidateRenderer(projectilePrefabObject, renderer, context);
        }
    }
    #endregion

    #region Validation
    /// <summary>
    /// Validates renderer-level ordering and occlusion settings for one enemy projectile visual renderer.
    /// </summary>
    /// <param name="projectilePrefabObject">Projectile prefab that owns the renderer.</param>
    /// <param name="renderer">Renderer inspected for queue, depth, and sorting configuration.</param>
    /// <param name="context">Authoring object used as the warning context.</param>
    private static void ValidateRenderer(GameObject projectilePrefabObject, Renderer renderer, Object context)
    {
        if (renderer.sortingOrder < MinimumEnemyProjectileSortingOrder)
        {
            Debug.LogWarning(string.Format("[EnemyProjectileVisualOrder] Renderer '{0}' on projectile prefab '{1}' has sorting order {2}. Use at least {3} so enemy bullets draw above enemy visuals.",
                                           renderer.name,
                                           projectilePrefabObject.name,
                                           renderer.sortingOrder,
                                           MinimumEnemyProjectileSortingOrder),
                             context);
        }

        if (renderer.allowOcclusionWhenDynamic)
        {
            Debug.LogWarning(string.Format("[EnemyProjectileVisualOrder] Renderer '{0}' on projectile prefab '{1}' allows dynamic occlusion. Disable it for tiny enemy bullets that must stay readable.",
                                           renderer.name,
                                           projectilePrefabObject.name),
                             context);
        }

        Material[] sharedMaterials = renderer.sharedMaterials;

        for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
            ValidateMaterial(projectilePrefabObject, renderer, sharedMaterials[materialIndex], context);
    }

    /// <summary>
    /// Validates material render state required by the selected no-extra-camera enemy projectile overlay path.
    /// </summary>
    /// <param name="projectilePrefabObject">Projectile prefab that owns the renderer material.</param>
    /// <param name="renderer">Renderer that references the material.</param>
    /// <param name="material">Material inspected for render queue and depth state.</param>
    /// <param name="context">Authoring object used as the warning context.</param>
    private static void ValidateMaterial(GameObject projectilePrefabObject, Renderer renderer, Material material, Object context)
    {
        if (material == null)
        {
            Debug.LogWarning(string.Format("[EnemyProjectileVisualOrder] Renderer '{0}' on projectile prefab '{1}' has a missing material.", renderer.name, projectilePrefabObject.name), context);
            return;
        }

        if (material.renderQueue < MinimumEnemyProjectileRenderQueue)
        {
            Debug.LogWarning(string.Format("[EnemyProjectileVisualOrder] Material '{0}' on projectile prefab '{1}' renders at queue {2}. Use queue {3} or higher so hostile bullet overlays draw after enemy and scene transparent passes.",
                                           material.name,
                                           projectilePrefabObject.name,
                                           material.renderQueue,
                                           MinimumEnemyProjectileRenderQueue),
                             context);
        }

        if (!material.HasProperty(depthTestPropertyName))
        {
            Debug.LogWarning(string.Format("[EnemyProjectileVisualOrder] Material '{0}' on projectile prefab '{1}' does not expose {2}. Enemy bullets cannot force visibility over enemy depth.",
                                           material.name,
                                           projectilePrefabObject.name,
                                           depthTestPropertyName),
                             context);
            return;
        }

        if ((CompareFunction)Mathf.RoundToInt(material.GetFloat(depthTestPropertyName)) != CompareFunction.Always)
        {
            Debug.LogWarning(string.Format("[EnemyProjectileVisualOrder] Material '{0}' on projectile prefab '{1}' uses depth test {2}. Use Always so enemy bullets remain visible over enemies.",
                                           material.name,
                                           projectilePrefabObject.name,
                                           (CompareFunction)Mathf.RoundToInt(material.GetFloat(depthTestPropertyName))),
                             context);
        }

        if (material.HasProperty(depthWritePropertyName) &&
            Mathf.RoundToInt(material.GetFloat(depthWritePropertyName)) != 0)
        {
            Debug.LogWarning(string.Format("[EnemyProjectileVisualOrder] Material '{0}' on projectile prefab '{1}' writes depth. Disable depth writes so overlay bullets do not hide each other incorrectly.",
                                           material.name,
                                           projectilePrefabObject.name),
                             context);
        }
    }
    #endregion

    #endregion
}
