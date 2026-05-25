using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Applies transform and component playback state for managed power-up VFX instances.
/// </summary>
internal static class PlayerPowerUpManagedVfxPresentationUtility
{
    #region Methods

    #region Transform
    /// <summary>
    /// Applies full transform data to one managed VFX root.
    /// </summary>
    /// <param name="instance">Managed VFX instance whose transform is being updated.</param>
    /// <param name="position">World position.</param>
    /// <param name="rotation">World rotation.</param>
    /// <param name="uniformScale">Uniform local scale.</param>
    /// <param name="particleSimulationSpeedMultiplier">Multiplier applied to authored particle simulation speeds.</param>
    /// <param name="forceLooping">True when particle systems should loop for the request lifetime.</param>
    /// <param name="hasColorOverride">True when matching particle systems should use request colors.</param>
    /// <param name="colorOverride">Primary linear color override applied to matching particle systems.</param>
    /// <param name="secondaryColorOverride">Secondary linear color override used for randomized debris palettes.</param>
    /// <param name="colorOverrideCount">Number of valid request colors, clamped by the caller-side palette producer.</param>
    /// <param name="colorOverrideChildName">Optional child object name used to limit the color override.</param>
    /// <param name="trailRendererWidthOverride">Positive world-space trail width override, or zero to preserve authored scaling.</param>
    /// <param name="trailRendererTimeOverrideSeconds">Positive trail history time override, or zero to preserve authored retention.</param>
    public static void ApplyTransform(PlayerPowerUpManagedVfxInstance instance,
                                      float3 position,
                                      quaternion rotation,
                                      float uniformScale,
                                      float particleSimulationSpeedMultiplier,
                                      bool forceLooping,
                                      bool hasColorOverride,
                                      float4 colorOverride,
                                      float4 secondaryColorOverride,
                                      byte colorOverrideCount,
                                      string colorOverrideChildName,
                                      float trailRendererWidthOverride,
                                      float trailRendererTimeOverrideSeconds)
    {
        Transform instanceTransform = instance.InstanceTransform;
        instanceTransform.position = ToVector3(position);
        instanceTransform.rotation = ToQuaternion(rotation);
        ApplyParticleSystemScaling(instance);
        ApplyParticleSystemRuntimeSettings(instance,
                                           particleSimulationSpeedMultiplier,
                                           forceLooping,
                                           hasColorOverride,
                                           colorOverride,
                                           secondaryColorOverride,
                                           colorOverrideCount,
                                           colorOverrideChildName);
        instanceTransform.localScale = ScaleVector(instance.RootBaseLocalScale, uniformScale);
        ApplyTrailRendererSettings(instance,
                                   uniformScale,
                                   trailRendererWidthOverride,
                                   trailRendererTimeOverrideSeconds);
    }

    /// <summary>
    /// Applies a position-only update to one managed VFX root.
    /// </summary>
    /// <param name="instance">Managed VFX instance whose transform is being updated.</param>
    /// <param name="position">World position.</param>
    public static void ApplyPosition(PlayerPowerUpManagedVfxInstance instance,
                                     float3 position)
    {
        instance.InstanceTransform.position = ToVector3(position);
    }

    /// <summary>
    /// Forces managed particle systems to honor the root transform scale applied by gameplay VFX requests.
    /// </summary>
    /// <param name="instance">Managed VFX instance whose particle systems are being prepared.</param>
    private static void ApplyParticleSystemScaling(PlayerPowerUpManagedVfxInstance instance)
    {
        if (instance.ParticleSystems == null)
            return;

        for (int particleIndex = 0; particleIndex < instance.ParticleSystems.Length; particleIndex++)
        {
            ParticleSystem particleSystem = instance.ParticleSystems[particleIndex];

            if (particleSystem == null)
                continue;

            ParticleSystem.MainModule mainModule = particleSystem.main;

            if (mainModule.scalingMode == ParticleSystemScalingMode.Hierarchy)
                continue;

            mainModule.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
    }

    /// <summary>
    /// Applies per-request particle timing, looping and optional start-color overrides without clearing playback.
    /// </summary>
    /// <param name="instance">Managed VFX instance whose particle systems are being prepared.</param>
    /// <param name="simulationSpeedMultiplier">Multiplier applied to cached authored simulation speeds. Values at or below zero resolve to 1.</param>
    /// <param name="forceLooping">True when particle systems should loop for the request lifetime.</param>
    /// <param name="hasColorOverride">True when matching particle systems should use request colors.</param>
    /// <param name="colorOverride">Primary linear color override applied to matching particle systems.</param>
    /// <param name="secondaryColorOverride">Secondary linear color override used for randomized debris palettes.</param>
    /// <param name="colorOverrideCount">Number of valid request colors, with values below two treated as a single color.</param>
    /// <param name="colorOverrideChildName">Optional child object name used to limit the color override.</param>
    public static void ApplyParticleSystemRuntimeSettings(PlayerPowerUpManagedVfxInstance instance,
                                                          float simulationSpeedMultiplier,
                                                          bool forceLooping,
                                                          bool hasColorOverride,
                                                          float4 colorOverride,
                                                          float4 secondaryColorOverride,
                                                          byte colorOverrideCount,
                                                          string colorOverrideChildName)
    {
        if (instance.ParticleSystems == null)
            return;

        float sanitizedSimulationSpeedMultiplier = simulationSpeedMultiplier > 0f ? simulationSpeedMultiplier : 1f;
        ParticleSystem.MinMaxGradient colorOverrideGradient = ResolveColorOverrideGradient(colorOverride,
                                                                                           secondaryColorOverride,
                                                                                           colorOverrideCount);

        for (int particleIndex = 0; particleIndex < instance.ParticleSystems.Length; particleIndex++)
        {
            ParticleSystem particleSystem = instance.ParticleSystems[particleIndex];

            if (particleSystem == null)
                continue;

            ParticleSystem.MainModule mainModule = particleSystem.main;
            mainModule.simulationSpeed = ResolveParticleBaseSimulationSpeed(instance, particleIndex, mainModule) * sanitizedSimulationSpeedMultiplier;
            mainModule.loop = forceLooping || ResolveParticleBaseLooping(instance, particleIndex, mainModule);
            mainModule.startColor = ShouldApplyColorOverride(particleSystem, hasColorOverride, colorOverrideChildName)
                ? colorOverrideGradient
                : ResolveParticleBaseStartColor(instance, particleIndex, mainModule);
        }
    }

    /// <summary>
    /// Applies width and retention overrides to trail renderers without clearing their current history.
    /// </summary>
    /// <param name="instance">Managed VFX instance whose trail renderers are being prepared.</param>
    /// <param name="uniformScale">Uniform scale requested by gameplay.</param>
    /// <param name="trailRendererWidthOverride">Positive world-space trail width override, or zero to scale authored widths.</param>
    /// <param name="trailRendererTimeOverrideSeconds">Positive trail history time override, or zero to restore authored retention.</param>
    public static void ApplyTrailRendererSettings(PlayerPowerUpManagedVfxInstance instance,
                                                  float uniformScale,
                                                  float trailRendererWidthOverride,
                                                  float trailRendererTimeOverrideSeconds)
    {
        if (instance.TrailRenderers == null)
            return;

        for (int trailIndex = 0; trailIndex < instance.TrailRenderers.Length; trailIndex++)
        {
            TrailRenderer trailRenderer = instance.TrailRenderers[trailIndex];

            if (trailRenderer == null)
                continue;

            if (trailRendererWidthOverride > 0f)
            {
                trailRenderer.widthMultiplier = Mathf.Max(0.0001f, trailRendererWidthOverride);
            }
            else
            {
                float baseWidth = ResolveTrailRendererBaseWidth(instance, trailIndex, trailRenderer);
                trailRenderer.widthMultiplier = Mathf.Max(0.0001f, baseWidth * uniformScale);
            }

            trailRenderer.time = ResolveTrailRendererTime(instance,
                                                          trailIndex,
                                                          trailRenderer,
                                                          trailRendererTimeOverrideSeconds);
        }
    }
    #endregion

    #region Playback
    /// <summary>
    /// Restarts particle systems and clears trail renderers when a pooled instance becomes active.
    /// </summary>
    /// <param name="instance">Managed VFX instance being restarted.</param>
    public static void RestartVisualPlayback(PlayerPowerUpManagedVfxInstance instance)
    {
        if (instance.ParticleSystems != null)
        {
            for (int particleIndex = 0; particleIndex < instance.ParticleSystems.Length; particleIndex++)
            {
                ParticleSystem particleSystem = instance.ParticleSystems[particleIndex];

                if (particleSystem == null)
                    continue;

                particleSystem.Clear(true);
                particleSystem.Play(true);
            }
        }

        if (instance.TrailRenderers == null)
            return;

        for (int trailIndex = 0; trailIndex < instance.TrailRenderers.Length; trailIndex++)
        {
            TrailRenderer trailRenderer = instance.TrailRenderers[trailIndex];

            if (trailRenderer == null)
                continue;

            trailRenderer.Clear();
            trailRenderer.emitting = true;
        }
    }

    /// <summary>
    /// Stops visual playback before returning an instance to the managed pool.
    /// </summary>
    /// <param name="instance">Managed VFX instance being stopped.</param>
    public static void StopVisualPlayback(PlayerPowerUpManagedVfxInstance instance)
    {
        if (instance.ParticleSystems != null)
        {
            for (int particleIndex = 0; particleIndex < instance.ParticleSystems.Length; particleIndex++)
            {
                ParticleSystem particleSystem = instance.ParticleSystems[particleIndex];

                if (particleSystem == null)
                    continue;

                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        if (instance.TrailRenderers == null)
            return;

        for (int trailIndex = 0; trailIndex < instance.TrailRenderers.Length; trailIndex++)
        {
            TrailRenderer trailRenderer = instance.TrailRenderers[trailIndex];

            if (trailRenderer == null)
                continue;

            trailRenderer.emitting = false;
            trailRenderer.Clear();
        }
    }
    #endregion

    #region Conversion
    /// <summary>
    /// Converts a float3 to a managed Unity Vector3.
    /// </summary>
    /// <param name="value">Source math vector.</param>
    /// <returns>Managed Vector3 with matching components.</returns>
    private static Vector3 ToVector3(float3 value)
    {
        return new Vector3(value.x, value.y, value.z);
    }

    /// <summary>
    /// Multiplies an authored local scale by one gameplay scale factor.
    /// </summary>
    /// <param name="value">Authored local scale value.</param>
    /// <param name="uniformScale">Uniform gameplay scale multiplier.</param>
    /// <returns>Scaled local scale value.</returns>
    private static Vector3 ScaleVector(Vector3 value, float uniformScale)
    {
        return new Vector3(value.x * uniformScale,
                           value.y * uniformScale,
                           value.z * uniformScale);
    }

    /// <summary>
    /// Resolves the cached authored trail width for one renderer.
    /// </summary>
    /// <param name="instance">Managed VFX instance that owns cached trail widths.</param>
    /// <param name="trailIndex">Trail renderer index inside the cached renderer array.</param>
    /// <param name="trailRenderer">Trail renderer used as fallback when cache data is missing.</param>
    /// <returns>Positive authored width multiplier.</returns>
    private static float ResolveTrailRendererBaseWidth(PlayerPowerUpManagedVfxInstance instance,
                                                       int trailIndex,
                                                       TrailRenderer trailRenderer)
    {
        if (instance.TrailRendererBaseWidths == null ||
            trailIndex < 0 ||
            trailIndex >= instance.TrailRendererBaseWidths.Length)
        {
            return trailRenderer.widthMultiplier;
        }

        return Mathf.Max(0.0001f, instance.TrailRendererBaseWidths[trailIndex]);
    }

    /// <summary>
    /// Resolves cached authored particle simulation speed for one particle system.
    /// </summary>
    /// <param name="instance">Managed VFX instance that owns cached particle timing values.</param>
    /// <param name="particleIndex">Particle-system index inside the cached array.</param>
    /// <param name="mainModule">Current particle main module used as fallback.</param>
    /// <returns>Positive authored simulation speed.</returns>
    private static float ResolveParticleBaseSimulationSpeed(PlayerPowerUpManagedVfxInstance instance,
                                                            int particleIndex,
                                                            ParticleSystem.MainModule mainModule)
    {
        if (instance.ParticleSystemBaseSimulationSpeeds == null ||
            particleIndex < 0 ||
            particleIndex >= instance.ParticleSystemBaseSimulationSpeeds.Length)
        {
            return Mathf.Max(0.0001f, mainModule.simulationSpeed);
        }

        return Mathf.Max(0.0001f, instance.ParticleSystemBaseSimulationSpeeds[particleIndex]);
    }

    /// <summary>
    /// Resolves cached authored particle looping state for one particle system.
    /// </summary>
    /// <param name="instance">Managed VFX instance that owns cached looping flags.</param>
    /// <param name="particleIndex">Particle-system index inside the cached array.</param>
    /// <param name="mainModule">Current particle main module used as fallback.</param>
    /// <returns>Authored loop flag.</returns>
    private static bool ResolveParticleBaseLooping(PlayerPowerUpManagedVfxInstance instance,
                                                   int particleIndex,
                                                   ParticleSystem.MainModule mainModule)
    {
        if (instance.ParticleSystemBaseLooping == null ||
            particleIndex < 0 ||
            particleIndex >= instance.ParticleSystemBaseLooping.Length)
        {
            return mainModule.loop;
        }

        return instance.ParticleSystemBaseLooping[particleIndex];
    }

    /// <summary>
    /// Resolves cached authored particle start color for one particle system.
    /// </summary>
    /// <param name="instance">Managed VFX instance that owns cached start colors.</param>
    /// <param name="particleIndex">Particle-system index inside the cached array.</param>
    /// <param name="mainModule">Current particle main module used as fallback.</param>
    /// <returns>Authored start color gradient.</returns>
    private static ParticleSystem.MinMaxGradient ResolveParticleBaseStartColor(PlayerPowerUpManagedVfxInstance instance,
                                                                               int particleIndex,
                                                                               ParticleSystem.MainModule mainModule)
    {
        if (instance.ParticleSystemBaseStartColors == null ||
            particleIndex < 0 ||
            particleIndex >= instance.ParticleSystemBaseStartColors.Length)
        {
            return mainModule.startColor;
        }

        return instance.ParticleSystemBaseStartColors[particleIndex];
    }

    /// <summary>
    /// Builds the particle-system start color override used by single-color and two-color debris requests.
    /// </summary>
    /// <param name="primaryColor">Primary linear request color.</param>
    /// <param name="secondaryColor">Secondary linear request color used when at least two colors are valid.</param>
    /// <param name="colorCount">Number of valid request colors.</param>
    /// <returns>Particle gradient that randomizes between two colors when a palette is available.</returns>
    private static ParticleSystem.MinMaxGradient ResolveColorOverrideGradient(float4 primaryColor,
                                                                              float4 secondaryColor,
                                                                              byte colorCount)
    {
        Color managedPrimaryColor = new Color(primaryColor.x,
                                              primaryColor.y,
                                              primaryColor.z,
                                              primaryColor.w);

        if (colorCount < 2)
            return new ParticleSystem.MinMaxGradient(managedPrimaryColor);

        Color managedSecondaryColor = new Color(secondaryColor.x,
                                                secondaryColor.y,
                                                secondaryColor.z,
                                                secondaryColor.w);

        return new ParticleSystem.MinMaxGradient(managedPrimaryColor, managedSecondaryColor);
    }

    /// <summary>
    /// Resolves whether a color override should affect one particle system.
    /// </summary>
    /// <param name="particleSystem">Particle system being inspected.</param>
    /// <param name="hasColorOverride">True when the request carries a color override.</param>
    /// <param name="colorOverrideChildName">Optional child object name used as a filter.</param>
    /// <returns>True when the particle start color should be overridden.</returns>
    private static bool ShouldApplyColorOverride(ParticleSystem particleSystem,
                                                 bool hasColorOverride,
                                                 string colorOverrideChildName)
    {
        if (!hasColorOverride)
            return false;

        if (string.IsNullOrWhiteSpace(colorOverrideChildName))
            return true;

        Transform particleTransform = particleSystem.transform;

        while (particleTransform != null)
        {
            if (string.Equals(particleTransform.name, colorOverrideChildName, System.StringComparison.Ordinal))
                return true;

            particleTransform = particleTransform.parent;
        }

        return false;
    }

    /// <summary>
    /// Resolves requested or authored trail history retention for one renderer.
    /// </summary>
    /// <param name="instance">Managed VFX instance that owns cached trail retention values.</param>
    /// <param name="trailIndex">Trail renderer index inside the cached renderer array.</param>
    /// <param name="trailRenderer">Trail renderer used as fallback when cache data is missing.</param>
    /// <param name="trailRendererTimeOverrideSeconds">Positive runtime trail retention override.</param>
    /// <returns>Trail history retention in seconds.</returns>
    private static float ResolveTrailRendererTime(PlayerPowerUpManagedVfxInstance instance,
                                                  int trailIndex,
                                                  TrailRenderer trailRenderer,
                                                  float trailRendererTimeOverrideSeconds)
    {
        if (trailRendererTimeOverrideSeconds > 0f)
            return trailRendererTimeOverrideSeconds;

        if (instance.TrailRendererBaseTimes == null ||
            trailIndex < 0 ||
            trailIndex >= instance.TrailRendererBaseTimes.Length)
        {
            return trailRenderer.time;
        }

        return Mathf.Max(0f, instance.TrailRendererBaseTimes[trailIndex]);
    }

    /// <summary>
    /// Converts a Unity.Mathematics quaternion to a managed Unity Quaternion.
    /// </summary>
    /// <param name="value">Source math quaternion.</param>
    /// <returns>Managed Quaternion with matching components.</returns>
    private static Quaternion ToQuaternion(quaternion value)
    {
        return new Quaternion(value.value.x,
                              value.value.y,
                              value.value.z,
                              value.value.w);
    }
    #endregion

    #endregion
}
