using Unity.Physics;
using UnityEngine;

/// <summary>
/// Defines a dedicated closed-portal category that can match only explicit player movement queries.
/// </summary>
public static class WorldPortalBarrierCollisionUtility
{
    #region Constants
    public const string DefaultPortalBarrierLayerName = "PortalBarrier";
    public const uint PlayerMovementQueryCategory = 1u << 31;
    #endregion

    #region Fields
    private static int cachedPortalBarrierLayerMask = int.MinValue;
#if UNITY_EDITOR
    private static bool warnedMissingLayer;
#endif
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the Unity layer bit reserved for independent portal blocker colliders.
    /// </summary>
    /// <returns>PortalBarrier layer mask, or zero when project setup is incomplete.</returns>
    public static int ResolvePortalBarrierLayerMask()
    {
        if (cachedPortalBarrierLayerMask != int.MinValue)
            return cachedPortalBarrierLayerMask;

        int layerIndex = LayerMask.NameToLayer(DefaultPortalBarrierLayerName);
        cachedPortalBarrierLayerMask = layerIndex >= 0 ? 1 << layerIndex : 0;

#if UNITY_EDITOR
        if (cachedPortalBarrierLayerMask == 0 && !warnedMissingLayer)
        {
            warnedMissingLayer = true;
            Debug.LogWarning("[WorldPortalBarrierCollisionUtility] Missing 'PortalBarrier' layer. Closed portal blockers fail closed by not baking until project setup creates the layer.");
        }
#endif

        return cachedPortalBarrierLayerMask;
    }

    /// <summary>
    /// Builds a blocker filter that can match only the reserved player movement query category.
    /// </summary>
    /// <param name="portalBarrierLayerMask">Dedicated PortalBarrier Unity layer mask.</param>
    /// <returns>Player-only blocker filter, or CollisionFilter.Zero when the layer is missing.</returns>
    public static CollisionFilter BuildPortalBarrierFilter(int portalBarrierLayerMask)
    {
        uint barrierCategory = portalBarrierLayerMask > 0 ? (uint)portalBarrierLayerMask : 0u;

        if (barrierCategory == 0u)
            return CollisionFilter.Zero;

        return new CollisionFilter
        {
            BelongsTo = barrierCategory,
            CollidesWith = PlayerMovementQueryCategory,
            GroupIndex = 0
        };
    }

    /// <summary>
    /// Builds the player movement query filter against solid walls and the dedicated closed-portal category.
    /// </summary>
    /// <param name="wallsLayerMask">Solid world wall categories.</param>
    /// <param name="portalBarrierLayerMask">Dedicated closed-portal category.</param>
    /// <returns>Collision filter whose identity is reserved for player movement queries.</returns>
    public static CollisionFilter BuildPlayerMovementFilter(int wallsLayerMask,
                                                            int portalBarrierLayerMask)
    {
        uint collidesWith = 0u;

        if (wallsLayerMask > 0)
            collidesWith |= (uint)wallsLayerMask;

        if (portalBarrierLayerMask > 0)
            collidesWith |= (uint)portalBarrierLayerMask;

        if (collidesWith == 0u)
            return CollisionFilter.Zero;

        return new CollisionFilter
        {
            BelongsTo = PlayerMovementQueryCategory,
            CollidesWith = collidesWith,
            GroupIndex = 0
        };
    }
    #endregion

    #endregion
}
