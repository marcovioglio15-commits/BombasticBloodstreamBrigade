using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Applies transform and component playback state for managed power-up VFX instances.
/// /params None.
/// /returns None.
/// </summary>
internal static class PlayerPowerUpManagedVfxPresentationUtility
{
    #region Methods

    #region Transform
    /// <summary>
    /// Applies full transform data to one managed VFX root.
    /// /params instance Managed VFX instance whose transform is being updated.
    /// /params position World position.
    /// /params rotation World rotation.
    /// /params uniformScale Uniform local scale.
    /// /returns None.
    /// </summary>
    public static void ApplyTransform(PlayerPowerUpManagedVfxInstance instance,
                                      float3 position,
                                      quaternion rotation,
                                      float uniformScale)
    {
        Transform instanceTransform = instance.InstanceTransform;
        instanceTransform.position = ToVector3(position);
        instanceTransform.rotation = ToQuaternion(rotation);
        instanceTransform.localScale = new Vector3(uniformScale, uniformScale, uniformScale);
    }

    /// <summary>
    /// Applies a position-only update to one managed VFX root.
    /// /params instance Managed VFX instance whose transform is being updated.
    /// /params position World position.
    /// /returns None.
    /// </summary>
    public static void ApplyPosition(PlayerPowerUpManagedVfxInstance instance,
                                     float3 position)
    {
        instance.InstanceTransform.position = ToVector3(position);
    }
    #endregion

    #region Playback
    /// <summary>
    /// Restarts particle systems and clears trail renderers when a pooled instance becomes active.
    /// /params instance Managed VFX instance being restarted.
    /// /returns None.
    /// </summary>
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
    /// /params instance Managed VFX instance being stopped.
    /// /returns None.
    /// </summary>
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
    /// /params value Source math vector.
    /// /returns Managed Vector3 with matching components.
    /// </summary>
    private static Vector3 ToVector3(float3 value)
    {
        return new Vector3(value.x, value.y, value.z);
    }

    /// <summary>
    /// Converts a Unity.Mathematics quaternion to a managed Unity Quaternion.
    /// /params value Source math quaternion.
    /// /returns Managed Quaternion with matching components.
    /// </summary>
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
