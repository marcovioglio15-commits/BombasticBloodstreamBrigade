using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Builds ECS interaction settings for dropped active power-up containers from progression authoring data.
/// none.
/// </summary>
internal static class PlayerPowerUpContainerBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the player-side interaction config baked from the current progression preset.
    /// </summary>
    /// <param name="progressionPreset">Progression preset that owns the dropped-container settings.</param>
    /// <param name="resolveDynamicPrefabEntity">Prefab-to-entity resolver provided by the baker.</param>
    /// <returns>Baked ECS interaction config.</returns>
    public static PlayerPowerUpContainerInteractionConfig BuildInteractionConfig(PlayerProgressionPreset progressionPreset,
                                                                                 System.Func<GameObject, Entity> resolveDynamicPrefabEntity)
    {
        PlayerPowerUpContainerInteractionSettings settings = progressionPreset != null
            ? progressionPreset.PowerUpContainerSettings
            : null;
        Entity containerPrefabEntity = Entity.Null;
        float containerGroundClearanceOffset = 0f;
        float baseInteractionLockDuration = settings != null
            ? settings.InteractionLockDuration
            : PlayerPowerUpContainerInteractionSettings.DefaultInteractionLockDurationSeconds;
        string interactionLockDurationFormula = string.Empty;

        if (PlayerRuntimeScalingBakeMetadataUtility.TryResolvePowerUpContainerInteractionLockDurationScalingData(progressionPreset,
                                                                                                                 out float resolvedBaseInteractionLockDuration,
                                                                                                                 out string resolvedInteractionLockDurationFormula))
        {
            baseInteractionLockDuration = resolvedBaseInteractionLockDuration;
            interactionLockDurationFormula = resolvedInteractionLockDurationFormula;
        }

        if (settings != null &&
            settings.ContainerPrefab != null &&
            resolveDynamicPrefabEntity != null)
        {
            containerPrefabEntity = resolveDynamicPrefabEntity.Invoke(settings.ContainerPrefab);
            containerGroundClearanceOffset = ResolveContainerGroundClearanceOffset(settings.ContainerPrefab);
        }

        return new PlayerPowerUpContainerInteractionConfig
        {
            ContainerPrefabEntity = containerPrefabEntity,
            InteractionRadius = settings != null ? math.max(0f, settings.InteractionRadius) : 0f,
            OverlayPanelTimeScaleResumeDurationSeconds = settings != null ? math.max(0f, settings.OverlayPanelTimeScaleResumeDurationSeconds) : 0f,
            ContainerGroundClearanceOffset = math.max(0f, containerGroundClearanceOffset),
            InteractionMode = settings != null ? settings.InteractionMode : PlayerPowerUpContainerInteractionMode.OverlayPanel,
            StoredStateMode = settings != null ? settings.StoredStateMode : PlayerPowerUpContainerStoredStateMode.PreserveEnergyAndCooldown,
            InteractionLockDuration = settings != null ? math.max(0f, settings.InteractionLockDuration) : PlayerPowerUpContainerInteractionSettings.DefaultInteractionLockDurationSeconds,
            BaseInteractionLockDuration = math.max(0f, baseInteractionLockDuration),
            InteractionLockDurationScalingFormula = new Unity.Collections.FixedString512Bytes(interactionLockDurationFormula)
        };
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Estimates the distance between the prefab pivot and the lowest rendered point so spawned containers can rest on the ground.
    /// </summary>
    /// <param name="containerPrefab">Prefab asset used to spawn dropped containers.</param>
    /// <returns>Ground-clearance offset measured from the prefab pivot to the lowest renderer bound.</returns>
    private static float ResolveContainerGroundClearanceOffset(GameObject containerPrefab)
    {
        if (containerPrefab == null)
            return 0f;

        Renderer[] renderers = containerPrefab.GetComponentsInChildren<Renderer>(true);

        if (renderers == null || renderers.Length <= 0)
            return 0f;

        float rootPositionY = containerPrefab.transform.position.y;
        float lowestRendererY = float.MaxValue;

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];

            if (renderer == null || !renderer.enabled)
                continue;

            float rendererMinY = renderer.bounds.min.y;

            if (rendererMinY < lowestRendererY)
                lowestRendererY = rendererMinY;
        }

        if (lowestRendererY == float.MaxValue)
            return 0f;

        return math.max(0f, rootPositionY - lowestRendererY);
    }
    #endregion

    #endregion
}
