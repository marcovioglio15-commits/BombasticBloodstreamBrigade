using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Draws projectile-specific runtime gizmos without bloating the shared runtime entity renderer.
/// /params None.
/// /returns None.
/// </summary>
internal static class RuntimeEntityProjectileGizmoUtility
{
    #region Constants
    private const float DirectionMagnitudeEpsilon = 0.0001f;
    private const float ProjectileVelocityLength = 0.85f;
    private const float ProjectileRangeMarkerRadius = 0.08f;
    private const float ProjectileDrawDistance = 48f;
    private const float BaseProjectileHitRadius = 0.05f;
    private const int MaxProjectileDrawCount = 96;
    private const int MaxProjectileLabelCount = 16;

    private static readonly Color PlayerProjectileImpactRadiusColor = new Color(0.22f, 0.9f, 1f, 0.94f);
    private static readonly Color PlayerProjectileVelocityColor = new Color(0.2f, 1f, 0.8f, 0.94f);
    private static readonly Color PlayerProjectileRemainingRangeColor = new Color(0.48f, 0.72f, 1f, 0.94f);
    private static readonly Color EnemyProjectileImpactRadiusColor = new Color(1f, 0.42f, 0.2f, 0.94f);
    private static readonly Color EnemyProjectileVelocityColor = new Color(1f, 0.84f, 0.28f, 0.94f);
    private static readonly Color EnemyProjectileRemainingRangeColor = new Color(1f, 0.6f, 0.32f, 0.94f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Draws live projectile combat envelopes and travel hints near the player while respecting strict debug caps.
    /// /params primitiveDrawer Active rendering backend receiving primitive calls.
    /// /params entityManager Runtime entity manager used to fetch projectile components.
    /// /params projectileQuery Query selecting active projectile entities.
    /// /params playerPosition Runtime player position used for distance filtering.
    /// /returns None.
    /// </summary>
    public static void DrawProjectileGizmos(IRuntimeGizmoPrimitiveDrawer primitiveDrawer,
                                            EntityManager entityManager,
                                            EntityQuery projectileQuery,
                                            float3 playerPosition)
    {
        if (!AnyProjectileGizmoEnabled())
            return;

        if (projectileQuery.IsEmptyIgnoreFilter)
            return;

        NativeArray<Entity> projectileEntities = projectileQuery.ToEntityArray(Allocator.Temp);
        int drawnCount = 0;
        int labeledCount = 0;

        try
        {
            // Keep projectile debug usable in bullet-heavy rooms by capping both distance and count.
            for (int projectileIndex = 0; projectileIndex < projectileEntities.Length; projectileIndex++)
            {
                if (drawnCount >= MaxProjectileDrawCount)
                    break;

                if (!TryDrawProjectile(primitiveDrawer,
                                       entityManager,
                                       projectileEntities[projectileIndex],
                                       playerPosition,
                                       ref drawnCount,
                                       ref labeledCount))
                    continue;
            }
        }
        finally
        {
            if (projectileEntities.IsCreated)
                projectileEntities.Dispose();
        }
    }
    #endregion

    #region Draw
    /// <summary>
    /// Draws one projectile when it is close enough and has at least one enabled projectile debug layer.
    /// /params primitiveDrawer Active rendering backend receiving primitive calls.
    /// /params entityManager Runtime entity manager used to fetch projectile components.
    /// /params projectileEntity Projectile entity being drawn.
    /// /params playerPosition Runtime player position used for distance filtering.
    /// /params drawnCount Mutable draw count used by the caller cap.
    /// /params labeledCount Mutable label count used by the caller cap.
    /// /returns True when a projectile gizmo was drawn.
    /// </summary>
    private static bool TryDrawProjectile(IRuntimeGizmoPrimitiveDrawer primitiveDrawer,
                                          EntityManager entityManager,
                                          Entity projectileEntity,
                                          float3 playerPosition,
                                          ref int drawnCount,
                                          ref int labeledCount)
    {
        LocalTransform projectileTransform = entityManager.GetComponentData<LocalTransform>(projectileEntity);
        float planarDistance = math.distance(playerPosition.xz, projectileTransform.Position.xz);

        if (planarDistance > ProjectileDrawDistance)
            return false;

        Projectile projectile = entityManager.GetComponentData<Projectile>(projectileEntity);
        ProjectileRuntimeState projectileRuntimeState = entityManager.GetComponentData<ProjectileRuntimeState>(projectileEntity);
        ProjectileOwner projectileOwner = entityManager.GetComponentData<ProjectileOwner>(projectileEntity);
        Vector3 projectilePosition = ToVector3(projectileTransform.Position);
        bool isPlayerOwned = entityManager.HasComponent<PlayerControllerConfig>(projectileOwner.ShooterEntity);
        bool drewProjectileGizmo = false;

        if (RuntimeGizmoDebugState.ProjectileImpactRadiusEnabled)
            drewProjectileGizmo = DrawImpactRadius(primitiveDrawer, projectile, in projectileTransform, projectilePosition, isPlayerOwned);

        if (RuntimeGizmoDebugState.ProjectileVelocityEnabled)
            drewProjectileGizmo |= DrawVelocity(primitiveDrawer,
                                                entityManager,
                                                projectileEntity,
                                                projectile,
                                                projectileOwner,
                                                projectilePosition,
                                                isPlayerOwned);

        if (RuntimeGizmoDebugState.ProjectileRemainingRangeEnabled)
            drewProjectileGizmo |= DrawRemainingRange(primitiveDrawer,
                                                      entityManager,
                                                      projectileEntity,
                                                      projectile,
                                                      projectileRuntimeState,
                                                      in projectileTransform,
                                                      projectileOwner,
                                                      projectilePosition,
                                                      isPlayerOwned);

        if (!drewProjectileGizmo)
            return false;

        if (RuntimeGizmoDebugState.ShowLabels && labeledCount < MaxProjectileLabelCount)
        {
            primitiveDrawer.DrawLabel(projectilePosition, isPlayerOwned ? "Player Projectile" : "Enemy Projectile");
            labeledCount++;
        }

        drawnCount++;
        return true;
    }

    /// <summary>
    /// Draws one projectile impact radius disc.
    /// /params primitiveDrawer Active rendering backend receiving primitive calls.
    /// /params projectile Projectile combat data used to resolve impact radius.
    /// /params projectileTransform Projectile transform used for scale.
    /// /params projectilePosition World-space projectile position.
    /// /params isPlayerOwned True when the projectile belongs to the player.
    /// /returns True when the impact radius disc was drawn.
    /// </summary>
    private static bool DrawImpactRadius(IRuntimeGizmoPrimitiveDrawer primitiveDrawer,
                                         Projectile projectile,
                                         in LocalTransform projectileTransform,
                                         Vector3 projectilePosition,
                                         bool isPlayerOwned)
    {
        float impactRadius = ResolveProjectileImpactRadius(projectile, in projectileTransform);

        if (impactRadius <= 0f)
            return false;

        primitiveDrawer.DrawWireDisc(projectilePosition,
                                     impactRadius,
                                     isPlayerOwned ? PlayerProjectileImpactRadiusColor : EnemyProjectileImpactRadiusColor);
        return true;
    }

    /// <summary>
    /// Draws one projectile velocity vector using the same inherited velocity as runtime motion.
    /// /params primitiveDrawer Active rendering backend receiving primitive calls.
    /// /params entityManager Runtime entity manager used to fetch projectile components.
    /// /params projectileEntity Projectile entity used to resolve special trajectory state.
    /// /params projectile Projectile motion data.
    /// /params projectileOwner Projectile owner data.
    /// /params projectilePosition World-space projectile position.
    /// /params isPlayerOwned True when the projectile belongs to the player.
    /// /returns True when the velocity vector was drawn.
    /// </summary>
    private static bool DrawVelocity(IRuntimeGizmoPrimitiveDrawer primitiveDrawer,
                                     EntityManager entityManager,
                                     Entity projectileEntity,
                                     Projectile projectile,
                                     ProjectileOwner projectileOwner,
                                     Vector3 projectilePosition,
                                     bool isPlayerOwned)
    {
        if (!TryResolveProjectileTravelVelocity(entityManager, projectileEntity, projectile, projectileOwner, out float3 projectileVelocity))
            return false;

        primitiveDrawer.DrawDirection(projectilePosition,
                                      ToVector3(projectileVelocity),
                                      ProjectileVelocityLength,
                                      isPlayerOwned ? PlayerProjectileVelocityColor : EnemyProjectileVelocityColor);
        return true;
    }

    /// <summary>
    /// Draws one estimated projectile travel endpoint using inherited velocity and propulsion-only range consumption.
    /// /params primitiveDrawer Active rendering backend receiving primitive calls.
    /// /params entityManager Runtime entity manager used to fetch projectile components.
    /// /params projectileEntity Projectile entity used to resolve special trajectory state.
    /// /params projectile Projectile motion and range data.
    /// /params projectileRuntimeState Projectile runtime counters.
    /// /params projectileTransform Projectile transform used as the endpoint origin.
    /// /params projectileOwner Projectile owner data.
    /// /params projectilePosition World-space projectile position.
    /// /params isPlayerOwned True when the projectile belongs to the player.
    /// /returns True when the remaining-range preview was drawn.
    /// </summary>
    private static bool DrawRemainingRange(IRuntimeGizmoPrimitiveDrawer primitiveDrawer,
                                           EntityManager entityManager,
                                           Entity projectileEntity,
                                           Projectile projectile,
                                           ProjectileRuntimeState projectileRuntimeState,
                                           in LocalTransform projectileTransform,
                                           ProjectileOwner projectileOwner,
                                           Vector3 projectilePosition,
                                           bool isPlayerOwned)
    {
        if (!TryResolveProjectileTravelEnd(entityManager,
                                           projectileEntity,
                                           projectile,
                                           projectileRuntimeState,
                                           in projectileTransform,
                                           projectileOwner,
                                           out float3 projectileTravelEnd))
            return false;

        Vector3 travelEndPosition = ToVector3(projectileTravelEnd);
        Color travelColor = isPlayerOwned ? PlayerProjectileRemainingRangeColor : EnemyProjectileRemainingRangeColor;
        primitiveDrawer.DrawLink(projectilePosition, travelEndPosition, travelColor);
        primitiveDrawer.DrawMarker(travelEndPosition, ProjectileRangeMarkerRadius, travelColor);
        return true;
    }
    #endregion

    #region Resolution
    /// <summary>
    /// Checks whether at least one projectile debug layer is currently enabled.
    /// /params None.
    /// /returns True when projectile gizmos should be evaluated.
    /// </summary>
    private static bool AnyProjectileGizmoEnabled()
    {
        return RuntimeGizmoDebugState.ProjectileImpactRadiusEnabled ||
               RuntimeGizmoDebugState.ProjectileVelocityEnabled ||
               RuntimeGizmoDebugState.ProjectileRemainingRangeEnabled;
    }

    /// <summary>
    /// Resolves the projectile impact radius including explosion payload radius.
    /// /params projectile Projectile combat data.
    /// /params projectileTransform Projectile transform used for current scale.
    /// /returns World-space impact radius.
    /// </summary>
    private static float ResolveProjectileImpactRadius(Projectile projectile,
                                                       in LocalTransform projectileTransform)
    {
        float projectileScale = math.max(0.01f, projectileTransform.Scale);
        float explosionRadius = math.max(0f, projectile.ExplosionRadius);
        return math.max(0.005f, BaseProjectileHitRadius * projectileScale + explosionRadius);
    }

    /// <summary>
    /// Resolves the current travel velocity used by the projectile debug vector.
    /// /params entityManager Runtime entity manager used to inspect special trajectory state.
    /// /params projectileEntity Projectile entity being inspected.
    /// /params projectile Projectile motion data.
    /// /params projectileOwner Projectile owner data.
    /// /params projectileVelocity Resolved world-space velocity when the method succeeds.
    /// /returns True when the projectile has a drawable velocity.
    /// </summary>
    private static bool TryResolveProjectileTravelVelocity(EntityManager entityManager,
                                                           Entity projectileEntity,
                                                           Projectile projectile,
                                                           ProjectileOwner projectileOwner,
                                                           out float3 projectileVelocity)
    {
        if (entityManager.HasComponent<ProjectilePerfectCircleState>(projectileEntity))
        {
            ProjectilePerfectCircleState perfectCircleState = entityManager.GetComponentData<ProjectilePerfectCircleState>(projectileEntity);

            if (perfectCircleState.Enabled != 0)
            {
                projectileVelocity = projectile.Velocity;
                return math.lengthsq(projectileVelocity) > DirectionMagnitudeEpsilon;
            }
        }

        projectileVelocity = ProjectileKinematicsUtility.ResolveLinearVelocity(in projectile,
                                                                               in projectileOwner,
                                                                               entityManager);
        return math.lengthsq(projectileVelocity) > DirectionMagnitudeEpsilon;
    }

    /// <summary>
    /// Resolves an estimated endpoint for the projectile under its current linear velocity and remaining limits.
    /// /params entityManager Runtime entity manager used to inspect special trajectory state.
    /// /params projectileEntity Projectile entity being inspected.
    /// /params projectile Projectile motion and range data.
    /// /params projectileRuntimeState Projectile runtime counters.
    /// /params projectileTransform Projectile transform used as the endpoint origin.
    /// /params projectileOwner Projectile owner data.
    /// /params projectileTravelEnd Resolved endpoint when the method succeeds.
    /// /returns True when an endpoint can be estimated.
    /// </summary>
    private static bool TryResolveProjectileTravelEnd(EntityManager entityManager,
                                                      Entity projectileEntity,
                                                      Projectile projectile,
                                                      ProjectileRuntimeState projectileRuntimeState,
                                                      in LocalTransform projectileTransform,
                                                      ProjectileOwner projectileOwner,
                                                      out float3 projectileTravelEnd)
    {
        projectileTravelEnd = default;

        if (!TryResolveProjectileTravelVelocity(entityManager,
                                                projectileEntity,
                                                projectile,
                                                projectileOwner,
                                                out float3 projectileVelocity))
            return false;

        float remainingRangeSeconds = ProjectileKinematicsUtility.ResolveRemainingRangeSeconds(in projectile,
                                                                                               projectileRuntimeState.TraveledDistance);
        float remainingLifetimeSeconds = projectile.MaxLifetime > 0f
            ? math.max(0f, projectile.MaxLifetime - projectileRuntimeState.ElapsedLifetime)
            : float.PositiveInfinity;
        float remainingTravelSeconds = math.min(remainingRangeSeconds, remainingLifetimeSeconds);

        if (float.IsInfinity(remainingTravelSeconds) || remainingTravelSeconds <= 0f)
            return false;

        projectileTravelEnd = projectileTransform.Position + projectileVelocity * remainingTravelSeconds;
        return true;
    }
    #endregion

    #region Conversion
    /// <summary>
    /// Converts a math vector to a UnityEngine vector.
    /// /params value Source math vector.
    /// /returns Managed Vector3 with matching components.
    /// </summary>
    private static Vector3 ToVector3(float3 value)
    {
        return new Vector3(value.x, value.y, value.z);
    }
    #endregion

    #endregion
}
