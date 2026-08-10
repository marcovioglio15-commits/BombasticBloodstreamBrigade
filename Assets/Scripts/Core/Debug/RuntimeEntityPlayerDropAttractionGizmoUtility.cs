using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Draws the passive drop-attraction radius only when it extends the player's normal pickup footprint.
/// </summary>
public static class RuntimeEntityPlayerDropAttractionGizmoUtility
{
    #region Constants
    private const float RadiusDifferenceEpsilon = 0.01f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Draws a distinct effective attraction ring from the current aggregated passive ECS state.
    /// </summary>
    /// <param name="primitiveDrawer">Active rendering backend receiving the radius primitive.</param>
    /// <param name="entityManager">Runtime entity manager used to read the passive-tools snapshot.</param>
    /// <param name="playerEntity">Player entity owning the aggregated passive state.</param>
    /// <param name="playerPosition">Current player world position used as ring center.</param>
    /// <param name="basePickupRadius">Normal pickup radius already rendered by the caller.</param>
    /// <param name="attractionColor">Color distinguishing the extended attraction footprint.</param>
    /// <returns>True when a passive attraction ring was drawn.</returns>
    public static bool DrawEffectiveRadius(IRuntimeGizmoPrimitiveDrawer primitiveDrawer,
                                           EntityManager entityManager,
                                           Entity playerEntity,
                                           Vector3 playerPosition,
                                           float basePickupRadius,
                                           Color attractionColor)
    {
        if (!entityManager.HasBuffer<PlayerPassiveToolsStateElement>(playerEntity))
            return false;

        PlayerPassiveToolsStateBufferUtility.Read(entityManager.GetBuffer<PlayerPassiveToolsStateElement>(playerEntity, true),
                                                  out PlayerPassiveToolsState passiveToolsState);

        if (passiveToolsState.HasDropAttraction == 0)
            return false;

        float attractionRadius = math.max(0f, passiveToolsState.DropAttraction.AttractionRadius);

        if (attractionRadius <= math.max(0f, basePickupRadius) + RadiusDifferenceEpsilon)
            return false;

        primitiveDrawer.DrawWireDisc(playerPosition, attractionRadius, attractionColor);
        return true;
    }
    #endregion

    #endregion
}
