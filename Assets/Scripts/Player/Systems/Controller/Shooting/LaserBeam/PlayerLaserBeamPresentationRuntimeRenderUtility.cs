using Unity.Mathematics;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Applies runtime transforms, materials, and shader properties to pooled Laser Beam visuals.
/// </summary>
internal static class PlayerLaserBeamPresentationRuntimeRenderUtility
{
    #region Fields
    private static readonly MaterialPropertyBlock sharedPropertyBlock = new MaterialPropertyBlock();
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Updates one body visual instance to match the requested lane metadata and body material properties.
    /// </summary>
    /// <param name="visual">Pooled body visual to update.</param>
    /// <param name="laneVisual">Render-time lane metadata.</param>
    /// <param name="visualConfig">Shared visual config.</param>
    /// <param name="laserBeamConfig">Runtime passive config used for material properties.</param>
    /// <param name="laserBeamState">Runtime state used to resolve the current storm response.</param>
    /// <param name="stormTickPulses">Active storm pulses used to drive packet shader vectors.</param>
    /// <param name="palette">Resolved beam palette.</param>
    /// <param name="bodyMaterial">Optional shared body material override.</param>
    public static void ApplyBodyVisual(PlayerLaserBeamManagedBodyVisual visual,
                                       in PlayerLaserBeamLaneVisual laneVisual,
                                       in PlayerLaserBeamVisualConfig visualConfig,
                                       in LaserBeamPassiveConfig laserBeamConfig,
                                       in PlayerLaserBeamState laserBeamState,
                                       in DynamicBuffer<PlayerLaserBeamStormTickPulse> stormTickPulses,
                                       in PlayerLaserBeamResolvedPalette palette,
                                       Material bodyMaterial)
    {
        if (visual == null || visual.InstanceObject == null || visual.RootTransform == null)
            return;

        if (!visual.InstanceObject.activeSelf)
            visual.InstanceObject.SetActive(true);

        visual.RootTransform.localPosition = Vector3.zero;
        visual.RootTransform.localRotation = Quaternion.identity;
        visual.RootTransform.localScale = Vector3.one;
        float laneLength = math.max(visualConfig.MinimumSegmentLength, laneVisual.TotalLength);
        float maximumWidth = PlayerLaserBeamPresentationRuntimeMeshUtility.ResolveBodyVisualWidth(math.max(laneVisual.StartWidth, laneVisual.EndWidth));
        float stormBurstNormalized = PlayerLaserBeamPresentationRuntimeMeshUtility.ResolveStormBurstNormalized(in laserBeamConfig, in laserBeamState);
        PlayerLaserBeamPresentationRuntimeRenderPropertyUtility.ResolveStormTickPulseVectors(in laserBeamConfig,
                                                                                             in stormTickPulses,
                                                                                             out Vector4 stormTickProgressA,
                                                                                             out Vector4 stormTickProgressB,
                                                                                             out Vector4 stormTickActiveA,
                                                                                             out Vector4 stormTickActiveB);

        // Drive the shared body mesh through three layered renderers so core, sheath and storm remain visually separated.
        for (int layerIndex = 0; layerIndex < visual.LayerVisuals.Count; layerIndex++)
        {
            PlayerLaserBeamManagedBodyLayerVisual layerVisual = visual.LayerVisuals[layerIndex];

            if (layerVisual == null || layerVisual.MeshRenderer == null)
                continue;

            if (layerVisual.InstanceObject != null && !layerVisual.InstanceObject.activeSelf)
                layerVisual.InstanceObject.SetActive(true);

            if (bodyMaterial != null && layerVisual.MeshRenderer.sharedMaterial != bodyMaterial)
                layerVisual.MeshRenderer.sharedMaterial = bodyMaterial;

            float layerOpacity = laserBeamConfig.BodyOpacity;
            float layerCoreBrightness = laserBeamConfig.CoreBrightness;
            float layerRimBrightness = laserBeamConfig.RimBrightness;
            float layerStormIdleIntensity = laserBeamConfig.StormIdleIntensity;
            float layerStormBurstIntensity = laserBeamConfig.StormBurstIntensity;
            PlayerLaserBeamPresentationRuntimeRenderPropertyUtility.ApplyBodyLayerOverrides(layerVisual.LayerRole,
                                                                                            ref layerOpacity,
                                                                                            ref layerCoreBrightness,
                                                                                            ref layerRimBrightness,
                                                                                            ref layerStormIdleIntensity,
                                                                                            ref layerStormBurstIntensity);
            sharedPropertyBlock.Clear();
            PlayerLaserBeamPresentationRuntimeRenderPropertyUtility.ApplySharedPaletteAndBeamProperties(sharedPropertyBlock,
                                                                                                        in palette,
                                                                                                        in laserBeamConfig,
                                                                                                        stormBurstNormalized,
                                                                                                        stormTickProgressA,
                                                                                                        stormTickProgressB,
                                                                                                        stormTickActiveA,
                                                                                                        stormTickActiveB,
                                                                                                        laneLength,
                                                                                                        maximumWidth,
                                                                                                        0f,
                                                                                                        (float)layerVisual.LayerRole,
                                                                                                        0f,
                                                                                                        laneVisual.TerminalBlockedByWall != 0,
                                                                                                        layerOpacity,
                                                                                                        layerCoreBrightness,
                                                                                                        layerRimBrightness,
                                                                                                        layerStormIdleIntensity,
                                                                                                        layerStormBurstIntensity,
                                                                                                        laserBeamConfig.SourceDischargeIntensity,
                                                                                                        laserBeamConfig.TerminalCapIntensity,
                                                                                                        laserBeamConfig.ContactFlareIntensity);
            layerVisual.MeshRenderer.SetPropertyBlock(sharedPropertyBlock);
        }
    }

    /// <summary>
    /// Updates one particle visual instance to match the requested lane endpoint and visual role.
    /// </summary>
    /// <param name="visual">Pooled particle visual to update.</param>
    /// <param name="endpoint">Per-lane endpoint metadata.</param>
    /// <param name="visualConfig">Shared visual config.</param>
    /// <param name="laserBeamConfig">Runtime passive config used for scale and material properties.</param>
    /// <param name="laserBeamState">Runtime state used to resolve the current storm response.</param>
    /// <param name="stormTickPulses">Active storm pulses used to drive packet shader vectors.</param>
    /// <param name="palette">Resolved beam palette.</param>
    /// <param name="materialOverride">Optional shared material override.</param>
    /// <param name="capShape">Shape selector applied to the shader.</param>
    /// <param name="visualRole">Endpoint visual role rendered by the pooled particle prefab.</param>
    public static void ApplyParticleVisual(PlayerLaserBeamManagedParticleVisual visual,
                                           in PlayerLaserBeamLaneEndpoint endpoint,
                                           in PlayerLaserBeamVisualConfig visualConfig,
                                           in LaserBeamPassiveConfig laserBeamConfig,
                                           in PlayerLaserBeamState laserBeamState,
                                           in DynamicBuffer<PlayerLaserBeamStormTickPulse> stormTickPulses,
                                           in PlayerLaserBeamResolvedPalette palette,
                                           Material materialOverride,
                                           LaserBeamCapShape capShape,
                                           PlayerLaserBeamEndpointVisualRole visualRole)
    {
        if (visual == null || visual.InstanceObject == null || visual.RootTransform == null)
            return;

        if (visualRole == PlayerLaserBeamEndpointVisualRole.ContactFlare && endpoint.TerminalBlockedByWall == 0)
        {
            DisableParticleVisual(visual);
            return;
        }

        float3 direction = PlayerLaserBeamPresentationRuntimeRenderPropertyUtility.ResolveEndpointDirection(in endpoint, visualRole);
        quaternion rotation = PlayerLaserBeamPresentationRuntimeRenderPropertyUtility.ResolveEndpointRotation(in endpoint, direction, visualRole);
        float width = math.max(0.05f, PlayerLaserBeamPresentationRuntimeRenderPropertyUtility.ResolveEndpointWidth(in endpoint, visualRole));
        float forwardOffset = PlayerLaserBeamPresentationRuntimeRenderPropertyUtility.ResolveEndpointForwardOffset(in visualConfig, visualRole);
        float authoredScaleMultiplier = PlayerLaserBeamPresentationRuntimeRenderPropertyUtility.ResolveEndpointScaleMultiplier(in laserBeamConfig, visualRole);
        float uniformScale = PlayerLaserBeamPresentationRuntimeRenderPropertyUtility.ResolveEndpointVisualScale(width, authoredScaleMultiplier, visualRole);
        float3 anchorPoint = PlayerLaserBeamPresentationRuntimeRenderPropertyUtility.ResolveEndpointAnchorPoint(in endpoint, visualRole);
        float3 worldPosition = anchorPoint + direction * forwardOffset;

        if (!visual.InstanceObject.activeSelf)
        {
            visual.InstanceObject.SetActive(true);
            PlayerLaserBeamPresentationRuntimeUtility.RestartParticleVisual(visual);
        }

        visual.RootTransform.position = ToVector3(worldPosition);
        visual.RootTransform.rotation = ToQuaternion(rotation);
        visual.RootTransform.localScale = new Vector3(uniformScale, uniformScale, uniformScale);
        ApplyParticleMaterials(visual, materialOverride);
        ApplyParticlePalette(visual,
                             in palette,
                             in laserBeamConfig,
                             in laserBeamState,
                             in stormTickPulses,
                             capShape,
                             width,
                             endpoint.TerminalBlockedByWall != 0,
                             visualRole);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies the shared material override to all particle renderers of one pooled visual.
    /// </summary>
    /// <param name="visual">Pooled particle visual that owns the renderers.</param>
    /// <param name="materialOverride">Shared material override to assign.</param>
    private static void ApplyParticleMaterials(PlayerLaserBeamManagedParticleVisual visual,
                                               Material materialOverride)
    {
        if (visual == null || visual.Renderers == null || materialOverride == null)
            return;

        for (int rendererIndex = 0; rendererIndex < visual.Renderers.Length; rendererIndex++)
        {
            ParticleSystemRenderer renderer = visual.Renderers[rendererIndex];

            if (renderer == null)
                continue;

            if (renderer.sharedMaterial != materialOverride)
                renderer.sharedMaterial = materialOverride;
        }
    }

    /// <summary>
    /// Pushes palette colors and electric-beam shader properties into one pooled particle visual.
    /// </summary>
    /// <param name="visual">Pooled particle visual to update.</param>
    /// <param name="palette">Resolved beam palette.</param>
    /// <param name="laserBeamConfig">Runtime passive config that drives the shader response.</param>
    /// <param name="laserBeamState">Runtime state used to resolve the current storm response.</param>
    /// <param name="stormTickPulses">Active storm pulses used to drive packet shader vectors.</param>
    /// <param name="capShape">Shape selector applied to the shader.</param>
    /// <param name="width">Beam width at the endpoint.</param>
    /// <param name="terminalBlockedByWall">True when the terminal point is a wall hit.</param>
    /// <param name="visualRole">Endpoint visual role rendered by the pooled particle prefab.</param>
    private static void ApplyParticlePalette(PlayerLaserBeamManagedParticleVisual visual,
                                             in PlayerLaserBeamResolvedPalette palette,
                                             in LaserBeamPassiveConfig laserBeamConfig,
                                             in PlayerLaserBeamState laserBeamState,
                                             in DynamicBuffer<PlayerLaserBeamStormTickPulse> stormTickPulses,
                                             LaserBeamCapShape capShape,
                                             float width,
                                             bool terminalBlockedByWall,
                                             PlayerLaserBeamEndpointVisualRole visualRole)
    {
        if (visual == null)
            return;

        PlayerLaserBeamPresentationRuntimeRenderPropertyUtility.ResolveParticleGradientColors(in palette,
                                                                                              visualRole,
                                                                                              out Color minimumColor,
                                                                                              out Color maximumColor);
        ParticleSystem.MinMaxGradient startGradient = new ParticleSystem.MinMaxGradient(minimumColor, maximumColor);
        float resolvedEndpointWidth = PlayerLaserBeamPresentationRuntimeMeshUtility.ResolveBodyVisualWidth(width);
        float stormBurstNormalized = PlayerLaserBeamPresentationRuntimeMeshUtility.ResolveStormBurstNormalized(in laserBeamConfig, in laserBeamState);
        PlayerLaserBeamPresentationRuntimeRenderPropertyUtility.ResolveStormTickPulseVectors(in laserBeamConfig,
                                                                                             in stormTickPulses,
                                                                                             out Vector4 stormTickProgressA,
                                                                                             out Vector4 stormTickProgressB,
                                                                                             out Vector4 stormTickActiveA,
                                                                                             out Vector4 stormTickActiveB);
        float endpointOpacity = PlayerLaserBeamPresentationRuntimeRenderPropertyUtility.ResolveEndpointOpacity(in laserBeamConfig, visualRole);
        float endpointCoreBrightness = PlayerLaserBeamPresentationRuntimeRenderPropertyUtility.ResolveEndpointCoreBrightness(in laserBeamConfig, visualRole);
        float endpointRimBrightness = PlayerLaserBeamPresentationRuntimeRenderPropertyUtility.ResolveEndpointRimBrightness(in laserBeamConfig, visualRole);
        float endpointStormIdleIntensity = PlayerLaserBeamPresentationRuntimeRenderPropertyUtility.ResolveEndpointStormIdleIntensity(in laserBeamConfig, visualRole);
        float endpointStormBurstIntensity = PlayerLaserBeamPresentationRuntimeRenderPropertyUtility.ResolveEndpointStormBurstIntensity(in laserBeamConfig, visualRole);
        float sourceDischargeIntensity = visualRole == PlayerLaserBeamEndpointVisualRole.Source
            ? laserBeamConfig.SourceDischargeIntensity
            : 0f;
        float terminalCapIntensity = visualRole == PlayerLaserBeamEndpointVisualRole.TerminalCap
            ? laserBeamConfig.TerminalCapIntensity
            : 0f;
        float contactFlareIntensity = visualRole == PlayerLaserBeamEndpointVisualRole.ContactFlare
            ? laserBeamConfig.ContactFlareIntensity
            : 0f;

        // Keep particle tinting in sync with the shader so the mesh-particle silhouettes remain coherent when materials switch.
        if (visual.ParticleSystems != null)
        {
            for (int particleIndex = 0; particleIndex < visual.ParticleSystems.Length; particleIndex++)
            {
                ParticleSystem particleSystem = visual.ParticleSystems[particleIndex];

                if (particleSystem == null)
                    continue;

                ParticleSystem.MainModule mainModule = particleSystem.main;
                mainModule.startColor = startGradient;
            }
        }

        if (visual.Renderers == null)
            return;

        for (int rendererIndex = 0; rendererIndex < visual.Renderers.Length; rendererIndex++)
        {
            ParticleSystemRenderer renderer = visual.Renderers[rendererIndex];

            if (renderer == null)
                continue;

            sharedPropertyBlock.Clear();
            PlayerLaserBeamPresentationRuntimeRenderPropertyUtility.ApplySharedPaletteAndBeamProperties(sharedPropertyBlock,
                                                                                                        in palette,
                                                                                                        in laserBeamConfig,
                                                                                                        stormBurstNormalized,
                                                                                                        stormTickProgressA,
                                                                                                        stormTickProgressB,
                                                                                                        stormTickActiveA,
                                                                                                        stormTickActiveB,
                                                                                                        math.max(0.05f, resolvedEndpointWidth),
                                                                                                        math.max(0.01f, resolvedEndpointWidth),
                                                                                                        (float)visualRole,
                                                                                                        (float)PlayerLaserBeamBodyLayerRole.Flow,
                                                                                                        (float)capShape,
                                                                                                        terminalBlockedByWall,
                                                                                                        endpointOpacity,
                                                                                                        endpointCoreBrightness,
                                                                                                        endpointRimBrightness,
                                                                                                        endpointStormIdleIntensity,
                                                                                                        endpointStormBurstIntensity,
                                                                                                        sourceDischargeIntensity,
                                                                                                        terminalCapIntensity,
                                                                                                        contactFlareIntensity);
            renderer.SetPropertyBlock(sharedPropertyBlock);
        }
    }

    /// <summary>
    /// Disables one pooled particle visual when its role is temporarily not visible.
    /// </summary>
    /// <param name="visual">Pooled particle visual to hide.</param>
    private static void DisableParticleVisual(PlayerLaserBeamManagedParticleVisual visual)
    {
        if (visual == null || visual.InstanceObject == null)
            return;

        if (visual.InstanceObject.activeSelf)
            visual.InstanceObject.SetActive(false);
    }

    /// <summary>
    /// Converts one ECS float3 into a managed Unity Vector3.
    /// </summary>
    /// <param name="value">ECS float3 value.</param>
    /// <returns>Managed Unity Vector3.</returns>
    private static Vector3 ToVector3(float3 value)
    {
        return new Vector3(value.x, value.y, value.z);
    }

    /// <summary>
    /// Converts one ECS quaternion into a managed Unity Quaternion.
    /// </summary>
    /// <param name="value">ECS quaternion value.</param>
    /// <returns>Managed Unity Quaternion.</returns>
    private static Quaternion ToQuaternion(quaternion value)
    {
        return new Quaternion(value.value.x, value.value.y, value.value.z, value.value.w);
    }
    #endregion

    #endregion
}
