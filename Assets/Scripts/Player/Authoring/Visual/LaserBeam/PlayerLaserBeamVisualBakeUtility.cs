using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Builds authored Laser Beam visual runtime data from the active player visual preset.
/// </summary>
public static class PlayerLaserBeamVisualBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the shared Laser Beam visual config baked on the player entity.
    /// </summary>
    /// <param name="authoring">Player authoring used to resolve the active visual preset.</param>
    /// <returns>Baked Laser Beam visual config.</returns>
    public static PlayerLaserBeamVisualConfig BuildConfig(PlayerAuthoring authoring)
    {
        PlayerLaserBeamVisualSettings visualSettings = ResolveVisualSettings(authoring);
        PlayerLaserBeamVisualRigAuthoring rigAuthoring = ResolveRigAuthoring(authoring);

        if (visualSettings == null)
        {
            return new PlayerLaserBeamVisualConfig
            {
                BodyMaterial = default,
                SourceEffectMaterial = default,
                TerminalCapMaterial = default,
                VerticalLift = PlayerLaserBeamVisualDefaultsUtility.DefaultVerticalLift,
                MinimumSegmentLength = PlayerLaserBeamVisualDefaultsUtility.DefaultMinimumSegmentLength,
                MaximumRibbonSegmentLength = 0.18f,
                TerminalSplashLengthMultiplier = 1.2f,
                TerminalSplashWidthMultiplier = 1.7f,
                SourceForwardOffset = 0.02f,
                ImpactForwardOffset = 0f
            };
        }

        return new PlayerLaserBeamVisualConfig
        {
            BodyMaterial = visualSettings.BodyMaterial,
            SourceEffectMaterial = visualSettings.SourceEffectMaterial,
            TerminalCapMaterial = visualSettings.TerminalCapMaterial,
            VerticalLift = math.max(0f, visualSettings.VerticalLift),
            MinimumSegmentLength = math.max(0.001f, visualSettings.MinimumSegmentLength),
            MaximumRibbonSegmentLength = math.max(0.01f, rigAuthoring != null ? rigAuthoring.MaximumRibbonSegmentLength : 0.18f),
            TerminalSplashLengthMultiplier = math.max(0.01f, rigAuthoring != null ? rigAuthoring.TerminalSplashLengthMultiplier : 1.2f),
            TerminalSplashWidthMultiplier = math.max(0.01f, rigAuthoring != null ? rigAuthoring.TerminalSplashWidthMultiplier : 1.7f),
            SourceForwardOffset = rigAuthoring != null ? rigAuthoring.SourceForwardOffset : 0.02f,
            ImpactForwardOffset = rigAuthoring != null ? rigAuthoring.ImpactForwardOffset : 0f
        };
    }

    /// <summary>
    /// Populates the baked source prefab variants resolved from the active visual rig authoring component.
    /// </summary>
    /// <param name="authoring">Player authoring used to resolve the active visual rig.</param>
    /// <param name="variantBuffer">Destination buffer written in-place.</param>
    public static void PopulateSourceVariantBuffer(PlayerAuthoring authoring, DynamicBuffer<PlayerLaserBeamSourceVariantElement> variantBuffer)
    {
        variantBuffer.Clear();
        PlayerLaserBeamVisualRigAuthoring rigAuthoring = ResolveRigAuthoring(authoring);

        AddSourceVariant(variantBuffer,
                         LaserBeamCapShape.BubbleBurst,
                         rigAuthoring != null ? rigAuthoring.BubbleBurstSourcePrefab : null);
        AddSourceVariant(variantBuffer,
                         LaserBeamCapShape.StarBloom,
                         rigAuthoring != null ? rigAuthoring.StarBloomSourcePrefab : null);
        AddSourceVariant(variantBuffer,
                         LaserBeamCapShape.SoftDisc,
                         rigAuthoring != null ? rigAuthoring.SoftDiscSourcePrefab : null);
    }

    /// <summary>
    /// Populates the baked impact prefab variants resolved from the active visual rig authoring component.
    /// </summary>
    /// <param name="authoring">Player authoring used to resolve the active visual rig.</param>
    /// <param name="variantBuffer">Destination buffer written in-place.</param>
    public static void PopulateImpactVariantBuffer(PlayerAuthoring authoring, DynamicBuffer<PlayerLaserBeamImpactVariantElement> variantBuffer)
    {
        variantBuffer.Clear();
        PlayerLaserBeamVisualRigAuthoring rigAuthoring = ResolveRigAuthoring(authoring);

        AddImpactVariant(variantBuffer,
                         LaserBeamCapShape.BubbleBurst,
                         rigAuthoring != null ? rigAuthoring.BubbleBurstImpactPrefab : null);
        AddImpactVariant(variantBuffer,
                         LaserBeamCapShape.StarBloom,
                         rigAuthoring != null ? rigAuthoring.StarBloomImpactPrefab : null);
        AddImpactVariant(variantBuffer,
                         LaserBeamCapShape.SoftDisc,
                         rigAuthoring != null ? rigAuthoring.SoftDiscImpactPrefab : null);
    }

    /// <summary>
    /// Populates the baked visual preset buffer used by the Laser Beam managed renderer path.
    /// </summary>
    /// <param name="authoring">Player authoring used to resolve the active visual preset.</param>
    /// <param name="visualPresetBuffer">Destination buffer written in-place.</param>
    public static void PopulateVisualPresetBuffer(PlayerAuthoring authoring, DynamicBuffer<PlayerLaserBeamVisualPresetElement> visualPresetBuffer)
    {
        visualPresetBuffer.Clear();
        PlayerLaserBeamVisualSettings visualSettings = ResolveVisualSettings(authoring);
        IReadOnlyList<PlayerLaserBeamVisualPresetDefinition> authoredVisualPresets = visualSettings != null
            ? visualSettings.VisualPresets
            : null;
        int minimumCapacity = authoredVisualPresets != null && authoredVisualPresets.Count > 0
            ? authoredVisualPresets.Count
            : 1;

        if (visualPresetBuffer.Capacity < minimumCapacity)
            visualPresetBuffer.Capacity = minimumCapacity;

        if (authoredVisualPresets != null)
        {
            HashSet<int> coveredVisualPresetIds = new HashSet<int>();

            for (int presetIndex = 0; presetIndex < authoredVisualPresets.Count; presetIndex++)
            {
                PlayerLaserBeamVisualPresetDefinition visualPresetDefinition = authoredVisualPresets[presetIndex];

                if (visualPresetDefinition == null)
                    continue;

                if (!coveredVisualPresetIds.Add(visualPresetDefinition.StableId))
                    continue;

                visualPresetBuffer.Add(new PlayerLaserBeamVisualPresetElement
                {
                    VisualPresetId = math.max(0, visualPresetDefinition.StableId),
                    CoreColor = DamageFlashRuntimeUtility.ToLinearFloat4(visualPresetDefinition.CoreColor),
                    FlowColor = DamageFlashRuntimeUtility.ToLinearFloat4(visualPresetDefinition.FlowColor),
                    StormColor = DamageFlashRuntimeUtility.ToLinearFloat4(visualPresetDefinition.StormColor),
                    ContactColor = DamageFlashRuntimeUtility.ToLinearFloat4(visualPresetDefinition.ContactColor)
                });
            }
        }

        if (visualPresetBuffer.Length > 0)
            return;

        PlayerLaserBeamVisualDefaultsUtility.ResolveDefaultPreset(PlayerLaserBeamVisualDefaultsUtility.DefaultVisualPresetId,
                                                                  out string _,
                                                                  out Color coreColor,
                                                                  out Color flowColor,
                                                                  out Color stormColor,
                                                                  out Color contactColor);
        visualPresetBuffer.Add(new PlayerLaserBeamVisualPresetElement
        {
            VisualPresetId = PlayerLaserBeamVisualDefaultsUtility.DefaultVisualPresetId,
            CoreColor = DamageFlashRuntimeUtility.ToLinearFloat4(coreColor),
            FlowColor = DamageFlashRuntimeUtility.ToLinearFloat4(flowColor),
            StormColor = DamageFlashRuntimeUtility.ToLinearFloat4(stormColor),
            ContactColor = DamageFlashRuntimeUtility.ToLinearFloat4(contactColor)
        });
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the active authored Laser Beam visual settings from the current player master preset.
    /// </summary>
    /// <param name="authoring">Player authoring used to resolve the current master preset.</param>
    /// <returns>Resolved visual settings asset, or null when unavailable.</returns>
    private static PlayerLaserBeamVisualSettings ResolveVisualSettings(PlayerAuthoring authoring)
    {
        if (authoring == null)
            return null;

        return PlayerAuthoringVisualPresetResolverUtility.ResolveLaserBeamVisualSettings(authoring.MasterPreset);
    }

    /// <summary>
    /// Resolves the active runtime visual rig authoring component from the player visual bridge prefab.
    /// </summary>
    /// <param name="authoring">Player authoring used to resolve the current runtime visual bridge prefab.</param>
    /// <returns>Resolved rig authoring component, or null when unavailable.</returns>
    private static PlayerLaserBeamVisualRigAuthoring ResolveRigAuthoring(PlayerAuthoring authoring)
    {
        if (authoring == null)
            return null;

        GameObject runtimeVisualBridgePrefab = PlayerAuthoringVisualPresetResolverUtility.ResolveRuntimeVisualBridgePrefab(authoring.MasterPreset,
                                                                                                                            authoring.RuntimeVisualBridgePrefab);

        if (runtimeVisualBridgePrefab == null)
            return null;

        return runtimeVisualBridgePrefab.GetComponent<PlayerLaserBeamVisualRigAuthoring>();
    }

    /// <summary>
    /// Adds one baked source variant entry to the destination buffer.
    /// </summary>
    /// <param name="variantBuffer">Destination buffer written in-place.</param>
    /// <param name="shape">Runtime cap-shape selector stored by the entry.</param>
    /// <param name="prefab">Authored prefab reference stored by the entry.</param>
    private static void AddSourceVariant(DynamicBuffer<PlayerLaserBeamSourceVariantElement> variantBuffer,
                                         LaserBeamCapShape shape,
                                         GameObject prefab)
    {
        variantBuffer.Add(new PlayerLaserBeamSourceVariantElement
        {
            Shape = shape,
            Prefab = prefab
        });
    }

    /// <summary>
    /// Adds one baked impact variant entry to the destination buffer.
    /// </summary>
    /// <param name="variantBuffer">Destination buffer written in-place.</param>
    /// <param name="shape">Runtime cap-shape selector stored by the entry.</param>
    /// <param name="prefab">Authored prefab reference stored by the entry.</param>
    private static void AddImpactVariant(DynamicBuffer<PlayerLaserBeamImpactVariantElement> variantBuffer,
                                         LaserBeamCapShape shape,
                                         GameObject prefab)
    {
        variantBuffer.Add(new PlayerLaserBeamImpactVariantElement
        {
            Shape = shape,
            Prefab = prefab
        });
    }
    #endregion

    #endregion
}
