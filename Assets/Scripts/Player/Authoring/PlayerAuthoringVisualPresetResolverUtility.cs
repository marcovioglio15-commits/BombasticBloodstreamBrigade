using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Resolves player visual settings from the active master visual preset with hidden authoring values as fallback.
/// </summary>
public static class PlayerAuthoringVisualPresetResolverUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the active player visual preset referenced by a master preset.
    /// </summary>
    /// <param name="masterPreset">Master preset assigned to the player authoring.</param>
    /// <returns>Active PlayerVisualPreset when assigned; otherwise null.</returns>
    public static PlayerVisualPreset ResolveVisualPreset(PlayerMasterPreset masterPreset)
    {
        if (masterPreset == null)
            return null;

        return masterPreset.VisualPreset;
    }

    /// <summary>
    /// Resolves the runtime visual bridge prefab using the master visual preset first and the hidden authoring field as fallback.
    /// </summary>
    /// <param name="masterPreset">Master preset assigned to the player authoring.</param>
    /// <param name="fallbackPrefab">Hidden authoring fallback prefab kept for compatibility.</param>
    /// <returns>Resolved visual bridge prefab.</returns>
    public static GameObject ResolveRuntimeVisualBridgePrefab(PlayerMasterPreset masterPreset, GameObject fallbackPrefab)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null && visualPreset.RuntimeVisualBridgePrefab != null)
            return visualPreset.RuntimeVisualBridgePrefab;

        return fallbackPrefab;
    }

    /// <summary>
    /// Resolves whether the runtime bridge should spawn only when no Animator companion is available.
    /// </summary>
    /// <param name="masterPreset">Master preset assigned to the player authoring.</param>
    /// <param name="fallbackValue">Hidden authoring fallback value kept for compatibility.</param>
    /// <returns>Resolved spawn policy.</returns>
    public static bool ResolveSpawnRuntimeVisualBridgeWhenAnimatorMissing(PlayerMasterPreset masterPreset, bool fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.SpawnRuntimeVisualBridgeWhenAnimatorMissing;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves whether the runtime bridge should copy ECS rotation.
    /// </summary>
    /// <param name="masterPreset">Master preset assigned to the player authoring.</param>
    /// <param name="fallbackValue">Hidden authoring fallback value kept for compatibility.</param>
    /// <returns>Resolved sync rotation flag.</returns>
    public static bool ResolveRuntimeVisualBridgeSyncRotation(PlayerMasterPreset masterPreset, bool fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.RuntimeVisualBridgeSyncRotation;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves the local runtime bridge offset.
    /// </summary>
    /// <param name="masterPreset">Master preset assigned to the player authoring.</param>
    /// <param name="fallbackValue">Hidden authoring fallback value kept for compatibility.</param>
    /// <returns>Resolved runtime bridge offset.</returns>
    public static Vector3 ResolveRuntimeVisualBridgeOffset(PlayerMasterPreset masterPreset, Vector3 fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.RuntimeVisualBridgeOffset;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves the authored damage flash color.
    /// </summary>
    /// <param name="masterPreset">Master preset assigned to the player authoring.</param>
    /// <param name="fallbackValue">Hidden authoring fallback value kept for compatibility.</param>
    /// <returns>Resolved damage flash color.</returns>
    public static Color ResolveDamageFlashColor(PlayerMasterPreset masterPreset, Color fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.DamageFlashColor;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves the damage flash duration.
    /// </summary>
    /// <param name="masterPreset">Master preset assigned to the player authoring.</param>
    /// <param name="fallbackValue">Hidden authoring fallback value kept for compatibility.</param>
    /// <returns>Resolved damage flash duration in seconds.</returns>
    public static float ResolveDamageFlashDurationSeconds(PlayerMasterPreset masterPreset, float fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.DamageFlashDurationSeconds;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves the maximum damage flash blend.
    /// </summary>
    /// <param name="masterPreset">Master preset assigned to the player authoring.</param>
    /// <param name="fallbackValue">Hidden authoring fallback value kept for compatibility.</param>
    /// <returns>Resolved maximum flash blend.</returns>
    public static float ResolveDamageFlashMaximumBlend(PlayerMasterPreset masterPreset, float fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.DamageFlashMaximumBlend;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves the Elemental Trail attached VFX prefab.
    /// </summary>
    /// <param name="masterPreset">Master preset assigned to the player authoring.</param>
    /// <param name="fallbackPrefab">Hidden authoring fallback prefab kept for compatibility.</param>
    /// <returns>Resolved Elemental Trail attached VFX prefab.</returns>
    public static GameObject ResolveElementalTrailAttachedVfxPrefab(PlayerMasterPreset masterPreset, GameObject fallbackPrefab)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null && visualPreset.ElementalTrailAttachedVfxPrefab != null)
            return visualPreset.ElementalTrailAttachedVfxPrefab;

        return fallbackPrefab;
    }

    /// <summary>
    /// Resolves the Elemental Trail attached VFX scale multiplier.
    /// </summary>
    /// <param name="masterPreset">Master preset assigned to the player authoring.</param>
    /// <param name="fallbackValue">Hidden authoring fallback value kept for compatibility.</param>
    /// <returns>Resolved attached VFX scale multiplier.</returns>
    public static float ResolveElementalTrailAttachedVfxScaleMultiplier(PlayerMasterPreset masterPreset, float fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.ElementalTrailAttachedVfxScaleMultiplier;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves the one-shot VFX per-cell cap.
    /// </summary>
    /// <param name="masterPreset">Master preset assigned to the player authoring.</param>
    /// <param name="fallbackValue">Hidden authoring fallback value kept for compatibility.</param>
    /// <returns>Resolved one-shot VFX per-cell cap.</returns>
    public static int ResolveMaxIdenticalOneShotVfxPerCell(PlayerMasterPreset masterPreset, int fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.MaxIdenticalOneShotVfxPerCell;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves the one-shot VFX spatial cell size.
    /// </summary>
    /// <param name="masterPreset">Master preset assigned to the player authoring.</param>
    /// <param name="fallbackValue">Hidden authoring fallback value kept for compatibility.</param>
    /// <returns>Resolved one-shot VFX cell size.</returns>
    public static float ResolveOneShotVfxCellSize(PlayerMasterPreset masterPreset, float fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.OneShotVfxCellSize;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves the attached elemental VFX per-target cap.
    /// </summary>
    /// <param name="masterPreset">Master preset assigned to the player authoring.</param>
    /// <param name="fallbackValue">Hidden authoring fallback value kept for compatibility.</param>
    /// <returns>Resolved attached elemental VFX per-target cap.</returns>
    public static int ResolveMaxAttachedElementalVfxPerTarget(PlayerMasterPreset masterPreset, int fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.MaxAttachedElementalVfxPerTarget;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves the total active one-shot VFX cap.
    /// </summary>
    /// <param name="masterPreset">Master preset assigned to the player authoring.</param>
    /// <param name="fallbackValue">Hidden authoring fallback value kept for compatibility.</param>
    /// <returns>Resolved active one-shot VFX cap.</returns>
    public static int ResolveMaxActiveOneShotPowerUpVfx(PlayerMasterPreset masterPreset, int fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.MaxActiveOneShotPowerUpVfx;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves whether the lifetime of capped attached elemental VFX should be refreshed.
    /// </summary>
    /// <param name="masterPreset">Master preset assigned to the player authoring.</param>
    /// <param name="fallbackValue">Hidden authoring fallback value kept for compatibility.</param>
    /// <returns>Resolved refresh-on-cap policy.</returns>
    public static bool ResolveRefreshAttachedElementalVfxLifetimeOnCapHit(PlayerMasterPreset masterPreset, bool fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.RefreshAttachedElementalVfxLifetimeOnCapHit;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves the per-element enemy VFX assignments used by elemental player bullets and trails.
    /// </summary>
    /// <param name="masterPreset">Master preset assigned to the player authoring.</param>
    /// <param name="fallbackPreset">Legacy power-ups preset kept as a fallback source for migrated projects.</param>
    /// <returns>Resolved assignment list, or null when no source is available.</returns>
    public static IReadOnlyList<ElementalVfxByElementData> ResolveElementalEnemyVfxAssignments(PlayerMasterPreset masterPreset,
                                                                                                PlayerPowerUpsPreset fallbackPreset)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null && PlayerElementalVfxAssignmentUtility.HasAnyConfiguredVfx(visualPreset.ElementalEnemyVfxByElement))
            return visualPreset.ElementalEnemyVfxByElement;

        if (fallbackPreset != null)
            return fallbackPreset.ElementalVfxByElement;

        if (visualPreset != null)
            return visualPreset.ElementalEnemyVfxByElement;

        return null;
    }

    /// <summary>
    /// Resolves the player outline settings block authored on the active visual preset.
    /// </summary>
    /// <param name="masterPreset">Master preset assigned to the player authoring.</param>
    /// <returns>Resolved outline settings, or null when no visual preset is available.</returns>
    public static PlayerVisualOutlineSettings ResolveOutlineSettings(PlayerMasterPreset masterPreset)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset == null)
            return null;

        return visualPreset.Outline;
    }

    /// <summary>
    /// Resolves the shared Laser Beam visual settings block authored on the active visual preset.
    /// </summary>
    /// <param name="masterPreset">Master preset assigned to the player authoring.</param>
    /// <returns>Resolved Laser Beam visual settings, or null when no visual preset is available.</returns>
    public static PlayerLaserBeamVisualSettings ResolveLaserBeamVisualSettings(PlayerMasterPreset masterPreset)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset == null)
            return null;

        return visualPreset.LaserBeam;
    }
    #endregion

    #endregion
}
