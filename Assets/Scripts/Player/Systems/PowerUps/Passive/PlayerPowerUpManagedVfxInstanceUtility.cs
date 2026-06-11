using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Creates, validates, resets and destroys pooled managed power-up VFX instances.
/// </summary>
internal static class PlayerPowerUpManagedVfxInstanceUtility
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Instantiates one managed VFX object and caches presentation components used during reuse.
    /// </summary>
    /// <param name="sourcePrefab">Source prefab asset requested by gameplay.</param>
    /// <returns>Created managed VFX instance, or null when the prefab cannot be instantiated.</returns>
    public static PlayerPowerUpManagedVfxInstance CreateInstance(GameObject sourcePrefab)
    {
        if (sourcePrefab == null)
            return null;

        GameObject instanceObject = Object.Instantiate(sourcePrefab);

        if (instanceObject == null)
            return null;

        ParticleSystem[] particleSystems = instanceObject.GetComponentsInChildren<ParticleSystem>(true);
        TrailRenderer[] trailRenderers = instanceObject.GetComponentsInChildren<TrailRenderer>(true);

        instanceObject.name = string.Format("{0}_PowerUpVfx", sourcePrefab.name);
        return new PlayerPowerUpManagedVfxInstance
        {
            SourcePrefab = sourcePrefab,
            InstanceObject = instanceObject,
            InstanceTransform = instanceObject.transform,
            RootBaseLocalScale = instanceObject.transform.localScale,
            ParticleSystems = particleSystems,
            TrailRenderers = trailRenderers,
            ParticleSystemBaseSimulationSpeeds = BuildParticleSystemBaseSimulationSpeeds(particleSystems),
            ParticleSystemBaseLooping = BuildParticleSystemBaseLooping(particleSystems),
            ParticleSystemBaseStartColors = BuildParticleSystemBaseStartColors(particleSystems),
            ParticleSystemBaseColorOverLifetimeEnabled = BuildParticleSystemBaseColorOverLifetimeEnabled(particleSystems),
            ParticleSystemBaseColorOverLifetimeColors = BuildParticleSystemBaseColorOverLifetimeColors(particleSystems),
            TrailRendererBaseWidths = BuildTrailRendererBaseWidths(trailRenderers),
            TrailRendererBaseTimes = BuildTrailRendererBaseTimes(trailRenderers)
        };
    }

    /// <summary>
    /// Clears runtime-only metadata before a managed VFX instance is pooled.
    /// </summary>
    /// <param name="instance">Managed VFX instance being reset.</param>
    public static void ResetRuntimeState(PlayerPowerUpManagedVfxInstance instance)
    {
        instance.PrefabEntity = Entity.Null;
        instance.RefreshKey = 0;
        instance.RemainingSeconds = 0f;
        instance.FollowTargetEntity = Entity.Null;
        instance.FollowPositionOffset = float3.zero;
        instance.FollowValidationEntity = Entity.Null;
        instance.FollowValidationSpawnVersion = 0u;
        instance.Velocity = float3.zero;
        instance.Position = float3.zero;
        instance.Rotation = quaternion.identity;
        instance.HasFollowTarget = false;
        instance.HasVelocity = false;
        instance.FollowMuzzlePose = false;
        instance.DetachWhenFollowTargetInvalid = false;
        instance.KeepAliveWhileFollowTargetValid = false;
        instance.RestartOldestOnCap = false;
        instance.ActivationSequence = 0ul;
    }

    /// <summary>
    /// Destroys one managed VFX GameObject and clears cached component references.
    /// </summary>
    /// <param name="instance">Managed VFX instance being destroyed.</param>
    public static void DestroyInstance(PlayerPowerUpManagedVfxInstance instance)
    {
        if (instance == null)
            return;

        if (instance.InstanceObject != null)
            DestroyInstanceObject(instance.InstanceObject);

        instance.SourcePrefab = null;
        instance.InstanceObject = null;
        instance.InstanceTransform = null;
        instance.ParticleSystems = null;
        instance.TrailRenderers = null;
        instance.ParticleSystemBaseSimulationSpeeds = null;
        instance.ParticleSystemBaseLooping = null;
        instance.ParticleSystemBaseStartColors = null;
        instance.ParticleSystemBaseColorOverLifetimeEnabled = null;
        instance.ParticleSystemBaseColorOverLifetimeColors = null;
        ResetRuntimeState(instance);
    }

    /// <summary>
    /// Checks whether a managed VFX instance still has a live GameObject and transform.
    /// </summary>
    /// <param name="instance">Managed VFX instance to validate.</param>
    /// <returns>True when the instance can be updated or pooled.</returns>
    public static bool IsInstanceUsable(PlayerPowerUpManagedVfxInstance instance)
    {
        if (instance == null)
            return false;

        if (instance.InstanceObject == null)
            return false;

        if (instance.InstanceTransform == null)
            return false;

        return true;
    }

    /// <summary>
    /// Destroys one managed VFX object immediately during edit-mode smoke tests and normally during play.
    /// </summary>
    /// <param name="instanceObject">Managed VFX GameObject to destroy.</param>
    private static void DestroyInstanceObject(GameObject instanceObject)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object.DestroyImmediate(instanceObject);
            return;
        }
#endif
        Object.Destroy(instanceObject);
    }
    #endregion

    #region Prefab Resolution
    /// <summary>
    /// Resolves the GameObject prefab mapped to one baked VFX prefab entity.
    /// </summary>
    /// <param name="prefabBindings">Player-owned prefab entity to GameObject source bindings.</param>
    /// <param name="request">Baked VFX request carrying either a prefab entity or direct source prefab.</param>
    /// <returns>Source GameObject prefab, or null when no binding exists.</returns>
    public static GameObject ResolveSourcePrefab(DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> prefabBindings,
                                                 in PlayerPowerUpVfxSpawnRequest request)
    {
        if (request.PrefabEntity != Entity.Null)
        {
            for (int bindingIndex = 0; bindingIndex < prefabBindings.Length; bindingIndex++)
            {
                PlayerPowerUpVfxPrefabBindingElement binding = prefabBindings[bindingIndex];

                if (binding.PrefabEntity != request.PrefabEntity)
                    continue;

                return binding.Prefab.Value;
            }
        }

        return request.SourcePrefab.Value;
    }
    #endregion

    #region Cached Presentation State
    /// <summary>
    /// Caches authored particle simulation speeds so pooled VFX can restore timing after charge-shot stretch requests.
    /// </summary>
    /// <param name="particleSystems">Particle systems collected from the spawned VFX instance.</param>
    /// <returns>Simulation speeds matching the particle-system array order.</returns>
    private static float[] BuildParticleSystemBaseSimulationSpeeds(ParticleSystem[] particleSystems)
    {
        if (particleSystems == null || particleSystems.Length <= 0)
            return null;

        float[] baseSimulationSpeeds = new float[particleSystems.Length];

        for (int particleIndex = 0; particleIndex < particleSystems.Length; particleIndex++)
        {
            ParticleSystem particleSystem = particleSystems[particleIndex];
            baseSimulationSpeeds[particleIndex] = particleSystem != null ? particleSystem.main.simulationSpeed : 1f;
        }

        return baseSimulationSpeeds;
    }

    /// <summary>
    /// Caches authored particle loop flags so forced-loop VFX requests do not leak into pooled reuse.
    /// </summary>
    /// <param name="particleSystems">Particle systems collected from the spawned VFX instance.</param>
    /// <returns>Loop flags matching the particle-system array order.</returns>
    private static bool[] BuildParticleSystemBaseLooping(ParticleSystem[] particleSystems)
    {
        if (particleSystems == null || particleSystems.Length <= 0)
            return null;

        bool[] baseLooping = new bool[particleSystems.Length];

        for (int particleIndex = 0; particleIndex < particleSystems.Length; particleIndex++)
        {
            ParticleSystem particleSystem = particleSystems[particleIndex];
            baseLooping[particleIndex] = particleSystem != null && particleSystem.main.loop;
        }

        return baseLooping;
    }

    /// <summary>
    /// Caches authored particle start colors so color override requests can be reset during pooled reuse.
    /// </summary>
    /// <param name="particleSystems">Particle systems collected from the spawned VFX instance.</param>
    /// <returns>Start colors matching the particle-system array order.</returns>
    private static ParticleSystem.MinMaxGradient[] BuildParticleSystemBaseStartColors(ParticleSystem[] particleSystems)
    {
        if (particleSystems == null || particleSystems.Length <= 0)
            return null;

        ParticleSystem.MinMaxGradient[] baseStartColors = new ParticleSystem.MinMaxGradient[particleSystems.Length];

        for (int particleIndex = 0; particleIndex < particleSystems.Length; particleIndex++)
        {
            ParticleSystem particleSystem = particleSystems[particleIndex];
            baseStartColors[particleIndex] = particleSystem != null ? particleSystem.main.startColor : new ParticleSystem.MinMaxGradient(Color.white);
        }

        return baseStartColors;
    }

    /// <summary>
    /// Caches authored Color over Lifetime enabled states so color override requests can restore pooled particles.
    /// </summary>
    /// <param name="particleSystems">Particle systems collected from the spawned VFX instance.</param>
    /// <returns>Enabled states matching the particle-system array order.</returns>
    private static bool[] BuildParticleSystemBaseColorOverLifetimeEnabled(ParticleSystem[] particleSystems)
    {
        if (particleSystems == null || particleSystems.Length <= 0)
            return null;

        bool[] baseEnabledStates = new bool[particleSystems.Length];

        for (int particleIndex = 0; particleIndex < particleSystems.Length; particleIndex++)
        {
            ParticleSystem particleSystem = particleSystems[particleIndex];
            baseEnabledStates[particleIndex] = particleSystem != null && particleSystem.colorOverLifetime.enabled;
        }

        return baseEnabledStates;
    }

    /// <summary>
    /// Caches authored Color over Lifetime gradients so pooled VFX can restore source prefab color animation.
    /// </summary>
    /// <param name="particleSystems">Particle systems collected from the spawned VFX instance.</param>
    /// <returns>Color over Lifetime gradients matching the particle-system array order.</returns>
    private static ParticleSystem.MinMaxGradient[] BuildParticleSystemBaseColorOverLifetimeColors(ParticleSystem[] particleSystems)
    {
        if (particleSystems == null || particleSystems.Length <= 0)
            return null;

        ParticleSystem.MinMaxGradient[] baseColors = new ParticleSystem.MinMaxGradient[particleSystems.Length];

        for (int particleIndex = 0; particleIndex < particleSystems.Length; particleIndex++)
        {
            ParticleSystem particleSystem = particleSystems[particleIndex];
            baseColors[particleIndex] = particleSystem != null ? particleSystem.colorOverLifetime.color : new ParticleSystem.MinMaxGradient(Color.white);
        }

        return baseColors;
    }

    /// <summary>
    /// Caches authored trail widths so pooled VFX can be rescaled from stable prefab values.
    /// </summary>
    /// <param name="trailRenderers">Trail renderers collected from the spawned VFX instance.</param>
    /// <returns>Width multipliers matching the renderer array order.</returns>
    private static float[] BuildTrailRendererBaseWidths(TrailRenderer[] trailRenderers)
    {
        if (trailRenderers == null || trailRenderers.Length <= 0)
            return null;

        float[] baseWidths = new float[trailRenderers.Length];

        for (int trailIndex = 0; trailIndex < trailRenderers.Length; trailIndex++)
        {
            TrailRenderer trailRenderer = trailRenderers[trailIndex];
            baseWidths[trailIndex] = trailRenderer != null ? trailRenderer.widthMultiplier : 1f;
        }

        return baseWidths;
    }

    /// <summary>
    /// Caches authored trail retention times so pooled VFX can restore source prefab history settings.
    /// </summary>
    /// <param name="trailRenderers">Trail renderers collected from the spawned VFX instance.</param>
    /// <returns>Retention times matching the renderer array order.</returns>
    private static float[] BuildTrailRendererBaseTimes(TrailRenderer[] trailRenderers)
    {
        if (trailRenderers == null || trailRenderers.Length <= 0)
            return null;

        float[] baseTimes = new float[trailRenderers.Length];

        for (int trailIndex = 0; trailIndex < trailRenderers.Length; trailIndex++)
        {
            TrailRenderer trailRenderer = trailRenderers[trailIndex];
            baseTimes[trailIndex] = trailRenderer != null ? trailRenderer.time : 0f;
        }

        return baseTimes;
    }
    #endregion

    #endregion
}
