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
    public static void ApplyTransform(PlayerPowerUpManagedVfxInstance instance,
                                      float3 position,
                                      quaternion rotation,
                                      float uniformScale)
    {
        Transform instanceTransform = instance.InstanceTransform;
        instanceTransform.position = ToVector3(position);
        instanceTransform.rotation = ToQuaternion(rotation);
        instanceTransform.localScale = ScaleVector(instance.RootBaseLocalScale, uniformScale);

        ApplyParticleSystemScaling(instance);
        ApplyTrailRendererScaling(instance, uniformScale);
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
    /// Applies request scale to trail widths because TrailRenderer width is authored in world units.
    /// </summary>
    /// <param name="instance">Managed VFX instance whose trail renderers are being prepared.</param>
    /// <param name="uniformScale">Uniform scale requested by gameplay.</param>
    private static void ApplyTrailRendererScaling(PlayerPowerUpManagedVfxInstance instance, float uniformScale)
    {
        if (instance.TrailRenderers == null)
            return;

        for (int trailIndex = 0; trailIndex < instance.TrailRenderers.Length; trailIndex++)
        {
            TrailRenderer trailRenderer = instance.TrailRenderers[trailIndex];

            if (trailRenderer == null)
                continue;

            float baseWidth = ResolveTrailRendererBaseWidth(instance, trailIndex, trailRenderer);
            trailRenderer.widthMultiplier = Mathf.Max(0.0001f, baseWidth * uniformScale);
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
