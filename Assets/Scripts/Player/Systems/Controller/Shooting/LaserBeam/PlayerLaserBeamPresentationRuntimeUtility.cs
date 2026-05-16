using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Owns pooled managed Laser Beam body and particle instances for the 3D presentation path.
/// </summary>
internal static class PlayerLaserBeamPresentationRuntimeUtility
{
    #region Constants
    private const int LaserBeamVisualSortingOrder = 12;
    private const float ShutdownTailDurationSeconds = 0.12f;
    #endregion

    #region Fields
    private static readonly MaterialPropertyBlock sharedFadePropertyBlock = new MaterialPropertyBlock();
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Removes pooled managed instances owned by destroyed ECS entities.
    /// </summary>
    /// <param name="entityManager">World entity manager used to validate owner entities.</param>
    /// <param name="managedInstances">Mutable runtime instance dictionary.</param>
    /// <param name="invalidOwnerEntities">Reusable list that receives dead owners before cleanup.</param>
    public static void CleanupInvalidOwnerInstances(EntityManager entityManager,
                                                    Dictionary<Entity, PlayerLaserBeamManagedInstance> managedInstances,
                                                    List<Entity> invalidOwnerEntities)
    {
        if (managedInstances.Count <= 0)
            return;

        invalidOwnerEntities.Clear();
        Dictionary<Entity, PlayerLaserBeamManagedInstance>.Enumerator enumerator = managedInstances.GetEnumerator();

        while (enumerator.MoveNext())
        {
            Entity ownerEntity = enumerator.Current.Key;
            PlayerLaserBeamManagedInstance managedInstance = enumerator.Current.Value;

            if (entityManager.Exists(ownerEntity) &&
                managedInstance != null &&
                managedInstance.RootObject != null)
            {
                continue;
            }

            invalidOwnerEntities.Add(ownerEntity);
        }

        enumerator.Dispose();

        for (int invalidIndex = 0; invalidIndex < invalidOwnerEntities.Count; invalidIndex++)
        {
            Entity invalidOwnerEntity = invalidOwnerEntities[invalidIndex];
            PlayerLaserBeamManagedInstance managedInstance;

            if (!managedInstances.TryGetValue(invalidOwnerEntity, out managedInstance))
                continue;

            DestroyManagedInstance(managedInstance);
            managedInstances.Remove(invalidOwnerEntity);
        }
    }

    /// <summary>
    /// Creates or reuses the pooled managed instance that owns all visuals for one player beam.
    /// </summary>
    /// <param name="playerEntity">Owner entity used to key the pooled dictionary.</param>
    /// <param name="managedInstances">Mutable runtime instance dictionary.</param>
    /// <returns>Managed instance ready for rendering.</returns>
    public static PlayerLaserBeamManagedInstance GetOrCreateManagedInstance(Entity playerEntity,
                                                                            Dictionary<Entity, PlayerLaserBeamManagedInstance> managedInstances)
    {
        PlayerLaserBeamManagedInstance managedInstance;

        if (managedInstances.TryGetValue(playerEntity, out managedInstance))
        {
            if (managedInstance != null && managedInstance.RootObject != null)
                return managedInstance;

            DestroyManagedInstance(managedInstance);
            managedInstances.Remove(playerEntity);
        }

        GameObject rootObject = new GameObject(string.Format("PlayerLaserBeam3D_{0}", playerEntity.Index));
        PlayerLaserBeamManagedInstance createdInstance = new PlayerLaserBeamManagedInstance
        {
            RootObject = rootObject,
            RootTransform = rootObject.transform
        };
        managedInstances[playerEntity] = createdInstance;
        return createdInstance;
    }

    /// <summary>
    /// Starts a short dissipation tail for one pooled beam instance without destroying its owned visuals.
    /// </summary>
    /// <param name="playerEntity">Owner entity used to resolve the pooled instance.</param>
    /// <param name="managedInstances">Runtime instance dictionary.</param>
    public static void DisableManagedInstance(Entity playerEntity,
                                              Dictionary<Entity, PlayerLaserBeamManagedInstance> managedInstances)
    {
        PlayerLaserBeamManagedInstance managedInstance;

        if (!managedInstances.TryGetValue(playerEntity, out managedInstance))
            return;

        if (managedInstance == null || managedInstance.RootObject == null)
            return;

        if (!managedInstance.RootObject.activeSelf)
        {
            managedInstance.ShutdownTailActive = 0;
            managedInstance.ShutdownTailRemainingSeconds = 0f;
            managedInstance.ShutdownTailLastFadeNormalized = 1f;
            return;
        }

        if (managedInstance.ShutdownTailActive != 0)
            return;

        managedInstance.ShutdownTailActive = 1;
        managedInstance.ShutdownTailRemainingSeconds = ShutdownTailDurationSeconds;
        managedInstance.ShutdownTailLastFadeNormalized = 1f;
        StopParticleVisuals(managedInstance.SourceVisuals, false);
        StopParticleVisuals(managedInstance.TerminalCapVisuals, false);
        StopParticleVisuals(managedInstance.ContactFlareVisuals, false);
    }

    /// <summary>
    /// Cancels the dissipation tail of one managed beam instance because the beam became active again.
    /// </summary>
    /// <param name="managedInstance">Managed beam instance that should resume full rendering.</param>
    public static void CancelManagedInstanceShutdown(PlayerLaserBeamManagedInstance managedInstance)
    {
        if (managedInstance == null)
            return;

        managedInstance.ShutdownTailActive = 0;
        managedInstance.ShutdownTailRemainingSeconds = 0f;
        managedInstance.ShutdownTailLastFadeNormalized = 1f;
    }

    /// <summary>
    /// Advances every active dissipation tail and hard-disables pooled instances whose fade reached zero.
    /// </summary>
    /// <param name="managedInstances">Runtime instance dictionary.</param>
    /// <param name="deltaTimeSeconds">Frame delta time used to advance the fade.</param>
    public static void AdvanceManagedInstanceShutdownTails(Dictionary<Entity, PlayerLaserBeamManagedInstance> managedInstances,
                                                           float deltaTimeSeconds)
    {
        if (managedInstances.Count <= 0)
            return;

        float clampedDeltaTimeSeconds = math.max(0f, deltaTimeSeconds);
        Dictionary<Entity, PlayerLaserBeamManagedInstance>.Enumerator enumerator = managedInstances.GetEnumerator();

        while (enumerator.MoveNext())
        {
            PlayerLaserBeamManagedInstance managedInstance = enumerator.Current.Value;

            if (managedInstance == null || managedInstance.ShutdownTailActive == 0)
                continue;

            if (managedInstance.RootObject == null || !managedInstance.RootObject.activeSelf)
            {
                managedInstance.ShutdownTailActive = 0;
                managedInstance.ShutdownTailRemainingSeconds = 0f;
                managedInstance.ShutdownTailLastFadeNormalized = 1f;
                continue;
            }

            managedInstance.ShutdownTailRemainingSeconds = math.max(0f, managedInstance.ShutdownTailRemainingSeconds - clampedDeltaTimeSeconds);

            if (managedInstance.ShutdownTailRemainingSeconds <= 0f)
            {
                HardDisableManagedInstance(managedInstance);
                continue;
            }

            float previousFadeNormalized = math.max(1e-5f, managedInstance.ShutdownTailLastFadeNormalized);
            float fadeNormalized = managedInstance.ShutdownTailRemainingSeconds / ShutdownTailDurationSeconds;
            ApplyManagedInstanceDissipationFade(managedInstance, previousFadeNormalized, fadeNormalized);
            managedInstance.ShutdownTailLastFadeNormalized = fadeNormalized;
        }

        enumerator.Dispose();
    }

    /// <summary>
    /// Destroys one pooled managed instance and all visuals owned by it.
    /// </summary>
    /// <param name="managedInstance">Instance to destroy.</param>
    public static void DestroyManagedInstance(PlayerLaserBeamManagedInstance managedInstance)
    {
        if (managedInstance == null)
            return;

        HardDisableManagedInstance(managedInstance);
        DestroyBodyVisuals(managedInstance.BodyVisuals);
        DestroyParticleVisuals(managedInstance.SourceVisuals);
        DestroyParticleVisuals(managedInstance.TerminalCapVisuals);
        DestroyParticleVisuals(managedInstance.ContactFlareVisuals);

        if (managedInstance.RootObject != null)
            Object.Destroy(managedInstance.RootObject);
    }

    /// <summary>
    /// Ensures the pooled body visual list can render the requested lane count.
    /// </summary>
    /// <param name="managedInstance">Instance owning the pooled visuals.</param>
    /// <param name="requiredCount">Number of lane visuals required this frame.</param>
    public static void EnsureBodyVisualCount(PlayerLaserBeamManagedInstance managedInstance,
                                             int requiredCount)
    {
        if (managedInstance == null || managedInstance.RootTransform == null)
            return;

        if (requiredCount < 0)
            requiredCount = 0;

        while (managedInstance.BodyVisuals.Count < requiredCount)
            managedInstance.BodyVisuals.Add(CreateBodyVisual(managedInstance.RootTransform, managedInstance.BodyVisuals.Count));

        for (int visualIndex = requiredCount; visualIndex < managedInstance.BodyVisuals.Count; visualIndex++)
        {
            PlayerLaserBeamManagedBodyVisual visual = managedInstance.BodyVisuals[visualIndex];

            if (visual == null || visual.InstanceObject == null)
                continue;

            if (visual.InstanceObject.activeSelf)
                visual.InstanceObject.SetActive(false);
        }
    }

    /// <summary>
    /// Ensures the pooled particle visual list can render the requested lane count using the selected prefab.
    /// </summary>
    /// <param name="visuals">Mutable pooled visual list.</param>
    /// <param name="requiredCount">Number of lane endpoint visuals required this frame.</param>
    /// <param name="prefab">Resolved particle prefab that should back the pooled visuals.</param>
    /// <param name="parentTransform">Parent transform that receives new pooled instances.</param>
    /// <param name="label">Prefix used when renaming created instances.</param>
    public static void EnsureParticleVisualCount(List<PlayerLaserBeamManagedParticleVisual> visuals,
                                                 int requiredCount,
                                                 GameObject prefab,
                                                 Transform parentTransform,
                                                 string label)
    {
        if (visuals == null || parentTransform == null)
            return;

        if (requiredCount < 0)
            requiredCount = 0;

        EnsureParticleVisualCapacity(visuals, requiredCount, prefab, parentTransform, label);

        for (int visualIndex = requiredCount; visualIndex < visuals.Count; visualIndex++)
        {
            PlayerLaserBeamManagedParticleVisual visual = visuals[visualIndex];

            if (visual == null || visual.InstanceObject == null)
                continue;

            StopParticleVisual(visual, true);

            if (visual.InstanceObject.activeSelf)
                visual.InstanceObject.SetActive(false);
        }
    }

    /// <summary>
    /// Restarts all particle systems owned by one pooled particle visual.
    /// </summary>
    /// <param name="visual">Pooled particle visual to restart.</param>
    public static void RestartParticleVisual(PlayerLaserBeamManagedParticleVisual visual)
    {
        if (visual == null || visual.ParticleSystems == null)
            return;

        for (int particleIndex = 0; particleIndex < visual.ParticleSystems.Length; particleIndex++)
        {
            ParticleSystem particleSystem = visual.ParticleSystems[particleIndex];

            if (particleSystem == null)
                continue;

            particleSystem.Clear(true);
            particleSystem.Play(true);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Ensures the particle visual pool matches the required count and prefab source.
    /// </summary>
    /// <param name="visuals">Mutable pooled particle visual list.</param>
    /// <param name="requiredCount">Required number of pooled particle visuals.</param>
    /// <param name="prefab">Prefab that should back every pooled particle visual.</param>
    /// <param name="parentTransform">Parent transform that receives new pooled instances.</param>
    /// <param name="label">Prefix used when renaming created instances.</param>
    private static void EnsureParticleVisualCapacity(List<PlayerLaserBeamManagedParticleVisual> visuals,
                                                     int requiredCount,
                                                     GameObject prefab,
                                                     Transform parentTransform,
                                                     string label)
    {
        while (visuals.Count < requiredCount)
        {
            PlayerLaserBeamManagedParticleVisual createdVisual = CreateParticleVisual(prefab, parentTransform, label);
            visuals.Add(createdVisual);
        }

        for (int visualIndex = 0; visualIndex < visuals.Count; visualIndex++)
        {
            PlayerLaserBeamManagedParticleVisual visual = visuals[visualIndex];

            if (visual != null &&
                visual.InstanceObject != null &&
                visual.SourcePrefab == prefab)
            {
                continue;
            }

            DestroyParticleVisual(visual);
            visuals[visualIndex] = CreateParticleVisual(prefab, parentTransform, label);
        }
    }

    /// <summary>
    /// Creates one pooled ribbon body visual backed by a dedicated dynamic mesh.
    /// </summary>
    /// <param name="parentTransform">Parent transform that receives the pooled instance.</param>
    /// <param name="visualIndex">Stable index used to name the created GameObject.</param>
    /// <returns>Newly created pooled body visual, or null when creation fails.</returns>
    private static PlayerLaserBeamManagedBodyVisual CreateBodyVisual(Transform parentTransform,
                                                                     int visualIndex)
    {
        if (parentTransform == null)
            return null;

        GameObject instanceObject = new GameObject(string.Format("PlayerLaserBeamBody_{0}", visualIndex));
        instanceObject.transform.SetParent(parentTransform, false);
        instanceObject.layer = 0;
        Mesh dynamicMesh = new Mesh
        {
            name = string.Format("PlayerLaserBeamBodyMesh_{0}", visualIndex)
        };
        dynamicMesh.indexFormat = IndexFormat.UInt32;
        dynamicMesh.MarkDynamic();
        PlayerLaserBeamManagedBodyVisual bodyVisual = new PlayerLaserBeamManagedBodyVisual
        {
            InstanceObject = instanceObject,
            RootTransform = instanceObject.transform,
            DynamicMesh = dynamicMesh
        };
        bodyVisual.LayerVisuals.Add(CreateBodyLayerVisual(instanceObject.transform,
                                                          dynamicMesh,
                                                          PlayerLaserBeamBodyLayerRole.Core,
                                                          "Core",
                                                          LaserBeamVisualSortingOrder));
        bodyVisual.LayerVisuals.Add(CreateBodyLayerVisual(instanceObject.transform,
                                                          dynamicMesh,
                                                          PlayerLaserBeamBodyLayerRole.Flow,
                                                          "Flow",
                                                          LaserBeamVisualSortingOrder + 1));
        bodyVisual.LayerVisuals.Add(CreateBodyLayerVisual(instanceObject.transform,
                                                          dynamicMesh,
                                                          PlayerLaserBeamBodyLayerRole.Storm,
                                                          "Storm",
                                                          LaserBeamVisualSortingOrder + 2));
        return bodyVisual;
    }

    /// <summary>
    /// Instantiates one pooled particle visual from the resolved source or impact prefab.
    /// </summary>
    /// <param name="prefab">Resolved particle prefab to instantiate.</param>
    /// <param name="parentTransform">Parent transform that receives the pooled instance.</param>
    /// <param name="label">Prefix used when renaming the created instance.</param>
    /// <returns>Newly created pooled particle visual, or null when creation fails.</returns>
    private static PlayerLaserBeamManagedParticleVisual CreateParticleVisual(GameObject prefab,
                                                                             Transform parentTransform,
                                                                             string label)
    {
        if (prefab == null || parentTransform == null)
            return null;

        GameObject instanceObject = Object.Instantiate(prefab, parentTransform);

        if (instanceObject == null)
            return null;

        instanceObject.name = string.Format("{0}_{1}", prefab.name, label);
        ParticleSystem[] particleSystems = instanceObject.GetComponentsInChildren<ParticleSystem>(true);
        ParticleSystemRenderer[] renderers = instanceObject.GetComponentsInChildren<ParticleSystemRenderer>(true);
        return new PlayerLaserBeamManagedParticleVisual
        {
            SourcePrefab = prefab,
            InstanceObject = instanceObject,
            RootTransform = instanceObject.transform,
            ParticleSystems = particleSystems,
            Renderers = renderers
        };
    }

    /// <summary>
    /// Destroys all pooled body visuals stored in one list.
    /// </summary>
    /// <param name="visuals">Mutable pooled body visual list.</param>
    private static void DestroyBodyVisuals(List<PlayerLaserBeamManagedBodyVisual> visuals)
    {
        if (visuals == null)
            return;

        for (int visualIndex = 0; visualIndex < visuals.Count; visualIndex++)
            DestroyBodyVisual(visuals[visualIndex]);

        visuals.Clear();
    }

    /// <summary>
    /// Destroys all pooled particle visuals stored in one list.
    /// </summary>
    /// <param name="visuals">Mutable pooled particle visual list.</param>
    private static void DestroyParticleVisuals(List<PlayerLaserBeamManagedParticleVisual> visuals)
    {
        if (visuals == null)
            return;

        for (int visualIndex = 0; visualIndex < visuals.Count; visualIndex++)
            DestroyParticleVisual(visuals[visualIndex]);

        visuals.Clear();
    }

    /// <summary>
    /// Destroys one pooled body visual instance and its owned dynamic mesh.
    /// </summary>
    /// <param name="visual">Pooled body visual to destroy.</param>
    private static void DestroyBodyVisual(PlayerLaserBeamManagedBodyVisual visual)
    {
        if (visual == null)
            return;

        DestroyBodyLayerVisuals(visual.LayerVisuals);

        if (visual.DynamicMesh != null)
            Object.Destroy(visual.DynamicMesh);

        if (visual.InstanceObject != null)
            Object.Destroy(visual.InstanceObject);
    }

    /// <summary>
    /// Destroys one pooled particle visual instance.
    /// </summary>
    /// <param name="visual">Pooled particle visual to destroy.</param>
    private static void DestroyParticleVisual(PlayerLaserBeamManagedParticleVisual visual)
    {
        if (visual == null || visual.InstanceObject == null)
            return;

        Object.Destroy(visual.InstanceObject);
    }

    /// <summary>
    /// Stops every pooled particle visual in one list and optionally clears already spawned particles.
    /// </summary>
    /// <param name="visuals">Mutable pooled particle visual list.</param>
    /// <param name="clearParticles">True to clear spawned particles immediately, false to let them fade naturally.</param>
    private static void StopParticleVisuals(List<PlayerLaserBeamManagedParticleVisual> visuals,
                                            bool clearParticles)
    {
        if (visuals == null)
            return;

        for (int visualIndex = 0; visualIndex < visuals.Count; visualIndex++)
        {
            PlayerLaserBeamManagedParticleVisual visual = visuals[visualIndex];

            if (visual == null)
                continue;

            StopParticleVisual(visual, clearParticles);
        }
    }

    /// <summary>
    /// Stops every particle system owned by one pooled particle visual and optionally clears already spawned particles.
    /// </summary>
    /// <param name="visual">Pooled particle visual to stop.</param>
    /// <param name="clearParticles">True to clear spawned particles immediately, false to preserve a short residual tail.</param>
    private static void StopParticleVisual(PlayerLaserBeamManagedParticleVisual visual,
                                           bool clearParticles)
    {
        if (visual == null || visual.ParticleSystems == null)
            return;

        for (int particleIndex = 0; particleIndex < visual.ParticleSystems.Length; particleIndex++)
        {
            ParticleSystem particleSystem = visual.ParticleSystems[particleIndex];

            if (particleSystem == null)
                continue;

            particleSystem.Stop(true,
                                clearParticles
                                    ? ParticleSystemStopBehavior.StopEmittingAndClear
                                    : ParticleSystemStopBehavior.StopEmitting);
        }
    }

    /// <summary>
    /// Hard-disables one managed instance after its dissipation tail finishes or before destruction.
    /// </summary>
    /// <param name="managedInstance">Managed beam instance to disable.</param>
    private static void HardDisableManagedInstance(PlayerLaserBeamManagedInstance managedInstance)
    {
        if (managedInstance == null || managedInstance.RootObject == null)
            return;

        managedInstance.ShutdownTailActive = 0;
        managedInstance.ShutdownTailRemainingSeconds = 0f;
        managedInstance.ShutdownTailLastFadeNormalized = 1f;
        StopParticleVisuals(managedInstance.SourceVisuals, true);
        StopParticleVisuals(managedInstance.TerminalCapVisuals, true);
        StopParticleVisuals(managedInstance.ContactFlareVisuals, true);

        if (managedInstance.RootObject.activeSelf)
            managedInstance.RootObject.SetActive(false);
    }

    /// <summary>
    /// Applies the shutdown fade multiplier to all active renderers owned by one managed instance.
    /// </summary>
    /// <param name="managedInstance">Managed beam instance that owns the active renderers.</param>
    /// <param name="previousFadeNormalized">Previously applied remaining fade amount in the 0-1 range.</param>
    /// <param name="currentFadeNormalized">Current remaining fade amount in the 0-1 range.</param>
    private static void ApplyManagedInstanceDissipationFade(PlayerLaserBeamManagedInstance managedInstance,
                                                            float previousFadeNormalized,
                                                            float currentFadeNormalized)
    {
        ApplyBodyVisualDissipationFade(managedInstance.BodyVisuals, previousFadeNormalized, currentFadeNormalized);
        ApplyParticleVisualDissipationFade(managedInstance.SourceVisuals, previousFadeNormalized, currentFadeNormalized);
        ApplyParticleVisualDissipationFade(managedInstance.TerminalCapVisuals, previousFadeNormalized, currentFadeNormalized);
        ApplyParticleVisualDissipationFade(managedInstance.ContactFlareVisuals, previousFadeNormalized, currentFadeNormalized);
    }

    /// <summary>
    /// Applies the shutdown fade multiplier to every active body renderer in the provided list.
    /// </summary>
    /// <param name="visuals">Body visual list to fade.</param>
    /// <param name="previousFadeNormalized">Previously applied remaining fade amount in the 0-1 range.</param>
    /// <param name="currentFadeNormalized">Current remaining fade amount in the 0-1 range.</param>
    private static void ApplyBodyVisualDissipationFade(List<PlayerLaserBeamManagedBodyVisual> visuals,
                                                       float previousFadeNormalized,
                                                       float currentFadeNormalized)
    {
        if (visuals == null)
            return;

        for (int visualIndex = 0; visualIndex < visuals.Count; visualIndex++)
        {
            PlayerLaserBeamManagedBodyVisual visual = visuals[visualIndex];

            if (visual == null || visual.LayerVisuals == null)
                continue;

            for (int layerIndex = 0; layerIndex < visual.LayerVisuals.Count; layerIndex++)
            {
                PlayerLaserBeamManagedBodyLayerVisual layerVisual = visual.LayerVisuals[layerIndex];

                if (layerVisual == null || layerVisual.MeshRenderer == null)
                    continue;

                ApplyRendererDissipationFade(layerVisual.MeshRenderer, previousFadeNormalized, currentFadeNormalized);
            }
        }
    }

    /// <summary>
    /// Applies the shutdown fade multiplier to every active particle renderer in the provided list.
    /// </summary>
    /// <param name="visuals">Particle visual list to fade.</param>
    /// <param name="previousFadeNormalized">Previously applied remaining fade amount in the 0-1 range.</param>
    /// <param name="currentFadeNormalized">Current remaining fade amount in the 0-1 range.</param>
    private static void ApplyParticleVisualDissipationFade(List<PlayerLaserBeamManagedParticleVisual> visuals,
                                                           float previousFadeNormalized,
                                                           float currentFadeNormalized)
    {
        if (visuals == null)
            return;

        for (int visualIndex = 0; visualIndex < visuals.Count; visualIndex++)
        {
            PlayerLaserBeamManagedParticleVisual visual = visuals[visualIndex];

            if (visual == null || visual.Renderers == null)
                continue;

            for (int rendererIndex = 0; rendererIndex < visual.Renderers.Length; rendererIndex++)
                ApplyRendererDissipationFade(visual.Renderers[rendererIndex], previousFadeNormalized, currentFadeNormalized);
        }
    }

    /// <summary>
    /// Applies the shutdown fade multiplier to one active renderer property block.
    /// </summary>
    /// <param name="renderer">Renderer that owns the property block.</param>
    /// <param name="previousFadeNormalized">Previously applied remaining fade amount in the 0-1 range.</param>
    /// <param name="currentFadeNormalized">Current remaining fade amount in the 0-1 range.</param>
    private static void ApplyRendererDissipationFade(Renderer renderer,
                                                     float previousFadeNormalized,
                                                     float currentFadeNormalized)
    {
        if (renderer == null || !renderer.gameObject.activeInHierarchy)
            return;

        sharedFadePropertyBlock.Clear();
        renderer.GetPropertyBlock(sharedFadePropertyBlock);
        PlayerLaserBeamPresentationRuntimeRenderPropertyUtility.ApplyDissipationFadeStep(sharedFadePropertyBlock,
                                                                                         previousFadeNormalized,
                                                                                         currentFadeNormalized);
        renderer.SetPropertyBlock(sharedFadePropertyBlock);
    }

    /// <summary>
    /// Creates one mesh-renderer child that shares the lane body mesh and renders one visual layer role.
    /// </summary>
    /// <param name="parentTransform">Parent transform that receives the new child.</param>
    /// <param name="sharedMesh">Dynamic lane mesh shared across all body layers.</param>
    /// <param name="layerRole">Visual role rendered by the created child.</param>
    /// <param name="childName">Readable name appended to the child GameObject.</param>
    /// <param name="sortingOrder">Renderer sorting order used for the layer.</param>
    /// <returns>Newly created body-layer visual, or null when creation fails.</returns>
    private static PlayerLaserBeamManagedBodyLayerVisual CreateBodyLayerVisual(Transform parentTransform,
                                                                               Mesh sharedMesh,
                                                                               PlayerLaserBeamBodyLayerRole layerRole,
                                                                               string childName,
                                                                               int sortingOrder)
    {
        if (parentTransform == null || sharedMesh == null)
            return null;

        GameObject childObject = new GameObject(string.Format("LaserBeamBodyLayer_{0}", childName));
        childObject.transform.SetParent(parentTransform, false);
        childObject.layer = 0;
        MeshFilter meshFilter = childObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = sharedMesh;
        MeshRenderer meshRenderer = childObject.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.sortingOrder = sortingOrder;
        return new PlayerLaserBeamManagedBodyLayerVisual
        {
            InstanceObject = childObject,
            RootTransform = childObject.transform,
            MeshFilter = meshFilter,
            MeshRenderer = meshRenderer,
            LayerRole = layerRole
        };
    }

    /// <summary>
    /// Destroys all child mesh-renderer layers owned by one body visual.
    /// </summary>
    /// <param name="layerVisuals">Mutable layer list to destroy and clear.</param>
    private static void DestroyBodyLayerVisuals(List<PlayerLaserBeamManagedBodyLayerVisual> layerVisuals)
    {
        if (layerVisuals == null)
            return;

        for (int layerIndex = 0; layerIndex < layerVisuals.Count; layerIndex++)
            DestroyBodyLayerVisual(layerVisuals[layerIndex]);

        layerVisuals.Clear();
    }

    /// <summary>
    /// Destroys one child mesh-renderer layer owned by a lane body visual.
    /// </summary>
    /// <param name="layerVisual">Layer visual to destroy.</param>
    private static void DestroyBodyLayerVisual(PlayerLaserBeamManagedBodyLayerVisual layerVisual)
    {
        if (layerVisual == null || layerVisual.InstanceObject == null)
            return;

        Object.Destroy(layerVisual.InstanceObject);
    }
    #endregion

    #endregion
}
