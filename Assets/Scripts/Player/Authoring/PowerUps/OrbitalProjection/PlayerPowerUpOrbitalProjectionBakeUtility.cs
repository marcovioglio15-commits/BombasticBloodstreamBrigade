using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Builds runtime orbital projection configs from modular power-up authoring data.
/// </summary>
public static class PlayerPowerUpOrbitalProjectionBakeUtility
{
    #region Constants
    private const int MaximumFixedString64Utf8Bytes = 61;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Compiles one Orbital Projections module payload into a compact fixed-list runtime config.
    /// </summary>
    /// <param name="authoring">Player authoring component used for prefab validation logs.</param>
    /// <param name="moduleData">Orbital projection module payload being compiled.</param>
    /// <param name="resolveDynamicPrefabEntity">Prefab-to-entity resolver provided by the baker.</param>
    /// <param name="resolveOrbitalProjectionPrefabBindingIndex">Resolver that stores prefab entities in a remappable player-owned binding table.</param>
    /// <returns>Fixed-list projection config used by runtime spawn systems.</returns>
    public static FixedList4096Bytes<OrbitalProjectionConfig> BuildProjectionConfigs(PlayerAuthoring authoring,
                                                                                     PowerUpOrbitalProjectionsModuleData moduleData,
                                                                                     Func<GameObject, Entity> resolveDynamicPrefabEntity,
                                                                                     Func<GameObject, int> resolveOrbitalProjectionPrefabBindingIndex = null)
    {
        FixedList4096Bytes<OrbitalProjectionConfig> configs = default;

        if (moduleData == null)
            return configs;

        IReadOnlyList<PowerUpOrbitalProjectionDefinitionData> projections = moduleData.Projections;

        if (projections == null || projections.Count <= 0)
            return configs;

        for (int projectionIndex = 0; projectionIndex < projections.Count; projectionIndex++)
        {
            PowerUpOrbitalProjectionDefinitionData projection = projections[projectionIndex];

            if (projection == null)
                continue;

            if (configs.Length >= configs.Capacity)
            {
#if UNITY_EDITOR
                if (authoring != null)
                    Debug.LogWarning(string.Format("[PlayerAuthoringBaker] Orbital Projections module on '{0}' exceeds the runtime capacity of {1} entries. Extra entries are ignored.",
                                                   authoring.name,
                                                   configs.Capacity),
                                     authoring);
#endif
                break;
            }

            configs.Add(BuildProjectionConfig(authoring,
                                              moduleData,
                                              projection,
                                              projectionIndex,
                                              resolveDynamicPrefabEntity,
                                              resolveOrbitalProjectionPrefabBindingIndex));
        }

        return configs;
    }

    /// <summary>
    /// Appends projection configs into an existing fixed-list while preserving authored order.
    /// </summary>
    /// <param name="authoring">Player authoring component used for overflow warnings.</param>
    /// <param name="targetConfigs">Destination fixed-list receiving appended entries.</param>
    /// <param name="sourceConfigs">Source fixed-list produced from one module payload.</param>
    public static void AppendProjectionConfigs(PlayerAuthoring authoring,
                                               ref FixedList4096Bytes<OrbitalProjectionConfig> targetConfigs,
                                               in FixedList4096Bytes<OrbitalProjectionConfig> sourceConfigs)
    {
        for (int configIndex = 0; configIndex < sourceConfigs.Length; configIndex++)
        {
            if (targetConfigs.Length >= targetConfigs.Capacity)
            {
#if UNITY_EDITOR
                if (authoring != null)
                    Debug.LogWarning(string.Format("[PlayerAuthoringBaker] Orbital Projections aggregation on '{0}' exceeds the runtime capacity of {1} entries. Extra entries are ignored.",
                                                   authoring.name,
                                                   targetConfigs.Capacity),
                                     authoring);
#endif
                return;
            }

            targetConfigs.Add(sourceConfigs[configIndex]);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Compiles one authored projection entry into runtime-safe values.
    /// </summary>
    /// <param name="authoring">Player authoring component used for prefab validation logs.</param>
    /// <param name="moduleData">Owning module payload that provides lifetime and acquisition settings.</param>
    /// <param name="projection">Projection entry being compiled.</param>
    /// <param name="projectionIndex">Stable entry index inside the owning module.</param>
    /// <param name="resolveDynamicPrefabEntity">Prefab-to-entity resolver provided by the baker.</param>
    /// <param name="resolveOrbitalProjectionPrefabBindingIndex">Resolver that returns the remappable prefab binding index.</param>
    /// <returns>Runtime projection config.</returns>
    private static OrbitalProjectionConfig BuildProjectionConfig(PlayerAuthoring authoring,
                                                                 PowerUpOrbitalProjectionsModuleData moduleData,
                                                                 PowerUpOrbitalProjectionDefinitionData projection,
                                                                 int projectionIndex,
                                                                 Func<GameObject, Entity> resolveDynamicPrefabEntity,
                                                                 Func<GameObject, int> resolveOrbitalProjectionPrefabBindingIndex)
    {
        Entity prefabEntity = PlayerPowerUpBakeSharedUtility.ResolveOptionalPowerUpPrefabEntity(authoring,
                                                                                                projection.ProjectionPrefab,
                                                                                                "Orbital Projection",
                                                                                                resolveDynamicPrefabEntity);
        int prefabBindingIndex = prefabEntity != Entity.Null && resolveOrbitalProjectionPrefabBindingIndex != null
            ? resolveOrbitalProjectionPrefabBindingIndex(projection.ProjectionPrefab)
            : -1;

        return new OrbitalProjectionConfig
        {
            ProjectionIndex = projectionIndex,
            CategoryId = NormalizeCategoryId(projection.CategoryId),
            PrefabEntity = prefabEntity,
            PrefabBindingIndex = prefabBindingIndex,
            MotionMode = projection.MotionMode,
            AcquisitionPolicy = moduleData.AcquisitionPolicy,
            UseCategoryIdAsExclusionFilter = moduleData.UseCategoryIdAsExclusionFilter ? (byte)1 : (byte)0,
            ActiveDurationSeconds = math.max(0f, moduleData.ActiveDurationSeconds),
            SpawnAnimationSeconds = math.max(0f, moduleData.SpawnAnimationSeconds),
            DespawnAnimationSeconds = math.max(0f, moduleData.DespawnAnimationSeconds),
            OrbitDistance = math.max(0f, projection.OrbitDistance),
            HeightOffset = projection.HeightOffset,
            AngleOffsetDegrees = projection.MotionMode == OrbitalProjectionMotionMode.IndependentOrbit ? 0f : projection.AngleOffsetDegrees,
            OrbitSpeedDegreesPerSecond = projection.OrbitSpeedDegreesPerSecond,
            BounceInsideOrbitCone = projection.BounceInsideOrbitCone ? (byte)1 : (byte)0,
            OrbitConeCenterAngleDegrees = projection.OrbitConeCenterAngleDegrees,
            OrbitConeAngleDegrees = math.clamp(projection.OrbitConeAngleDegrees, 0f, 360f),
            FullOrbitConeResponse = projection.FullOrbitConeResponse,
            LookFollowDelaySeconds = math.max(0f, projection.LookFollowDelaySeconds),
            MaximumLookFollowSpeedDegreesPerSecond = projection.MaximumLookFollowSpeedDegreesPerSecond,
            FaceOrbitOutward = projection.FaceOrbitOutward ? (byte)1 : (byte)0,
            CollisionRadius = math.max(0.01f, projection.CollisionRadius),
            AdaptCollisionToModel = ResolveAdaptCollisionToModel(authoring, projection, prefabEntity),
            ModelCollisionBoundingRadius = 0f,
            DamageEnemies = projection.DamageEnemies ? (byte)1 : (byte)0,
            ContactDamage = math.max(0f, projection.ContactDamage),
            DamageTickIntervalSeconds = math.max(0.01f, projection.DamageTickIntervalSeconds),
            BlockEnemyProjectiles = projection.BlockEnemyProjectiles ? (byte)1 : (byte)0,
            BlockEnemyBombs = projection.BlockEnemyBombs ? (byte)1 : (byte)0,
            HasHealth = projection.HasHealth ? (byte)1 : (byte)0,
            MaximumHealth = math.max(0f, projection.MaximumHealth),
            EnemyContactHealthDamage = math.max(0f, projection.EnemyContactHealthDamage),
            ProjectileBlockHealthDamage = math.max(0f, projection.ProjectileBlockHealthDamage),
            BombBlockHealthDamage = math.max(0f, projection.BombBlockHealthDamage)
        };
    }

    /// <summary>
    /// Resolves the Adapt Collision To Model flag, downgrading it to the plain radius when no
    /// prefab entity could be baked (the model silhouette cannot exist without a prefab).
    /// </summary>
    /// <param name="authoring">Player authoring component used for validation logs.</param>
    /// <param name="projection">Projection entry being compiled.</param>
    /// <param name="prefabEntity">Resolved prefab entity, or Entity.Null when missing.</param>
    /// <returns>Runtime flag enabling the model-silhouette collision path.</returns>
    private static byte ResolveAdaptCollisionToModel(PlayerAuthoring authoring,
                                                     PowerUpOrbitalProjectionDefinitionData projection,
                                                     Entity prefabEntity)
    {
        if (!projection.AdaptCollisionToModel)
            return 0;

        if (prefabEntity != Entity.Null)
            return 1;

#if UNITY_EDITOR
        if (authoring != null)
            Debug.LogWarning(string.Format("[PlayerAuthoringBaker] Orbital projection '{0}' enables Adapt Collision To Model without a Projection Prefab on '{1}'. Falling back to Collision Radius.",
                                           projection.DisplayName,
                                           authoring.name),
                             authoring);
#endif
        return 0;
    }

    /// <summary>
    /// Converts an authored category string into a trimmed fixed string used by runtime filters.
    /// </summary>
    /// <param name="categoryId">Authored category id from one projection definition.</param>
    /// <returns>Runtime-safe category id, or an empty fixed string when not configured.</returns>
    private static FixedString64Bytes NormalizeCategoryId(string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
            return default;

        string trimmedCategoryId = categoryId.Trim();

        if (Encoding.UTF8.GetByteCount(trimmedCategoryId) > MaximumFixedString64Utf8Bytes)
            return default;

        return new FixedString64Bytes(trimmedCategoryId);
    }
    #endregion

    #endregion
}
