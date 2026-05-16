using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Centralizes Laser Beam renderer property-block assignment and shared endpoint/body-role resolution helpers.
/// </summary>
internal static class PlayerLaserBeamPresentationRuntimeRenderPropertyUtility
{
    #region Constants
    private const float SourceAnchorBlend = 0.26f;
    private const float TerminalCapEmbedDistanceBias = 0.04f;
    private const float TerminalCapEmbedDistanceFactor = 0.22f;
    private const float ContactFlareEmbedDistanceBias = 0.02f;
    private const float ContactFlareEmbedDistanceFactor = 0.1f;
    private static readonly int CoreColorPropertyId = Shader.PropertyToID("_CoreColor");
    private static readonly int FlowColorPropertyId = Shader.PropertyToID("_FlowColor");
    private static readonly int StormColorPropertyId = Shader.PropertyToID("_StormColor");
    private static readonly int ContactColorPropertyId = Shader.PropertyToID("_ContactColor");
    private static readonly int OpacityPropertyId = Shader.PropertyToID("_Opacity");
    private static readonly int CoreBrightnessPropertyId = Shader.PropertyToID("_CoreBrightness");
    private static readonly int RimBrightnessPropertyId = Shader.PropertyToID("_RimBrightness");
    private static readonly int FlowScrollSpeedPropertyId = Shader.PropertyToID("_FlowScrollSpeed");
    private static readonly int FlowPulseFrequencyPropertyId = Shader.PropertyToID("_FlowPulseFrequency");
    private static readonly int WobbleAmplitudePropertyId = Shader.PropertyToID("_WobbleAmplitude");
    private static readonly int BubbleDriftSpeedPropertyId = Shader.PropertyToID("_BubbleDriftSpeed");
    private static readonly int BeamRolePropertyId = Shader.PropertyToID("_BeamRole");
    private static readonly int BodyLayerRolePropertyId = Shader.PropertyToID("_BodyLayerRole");
    private static readonly int CapShapePropertyId = Shader.PropertyToID("_CapShape");
    private static readonly int SegmentLengthPropertyId = Shader.PropertyToID("_SegmentLength");
    private static readonly int WidthScalePropertyId = Shader.PropertyToID("_WidthScale");
    private static readonly int CoreWidthMultiplierPropertyId = Shader.PropertyToID("_CoreWidthMultiplier");
    private static readonly int StormTwistSpeedPropertyId = Shader.PropertyToID("_StormTwistSpeed");
    private static readonly int StormIdleIntensityPropertyId = Shader.PropertyToID("_StormIdleIntensity");
    private static readonly int StormBurstIntensityPropertyId = Shader.PropertyToID("_StormBurstIntensity");
    private static readonly int StormBurstNormalizedPropertyId = Shader.PropertyToID("_StormBurstNormalized");
    private static readonly int StormShellWidthMultiplierPropertyId = Shader.PropertyToID("_StormShellWidthMultiplier");
    private static readonly int StormShellSeparationPropertyId = Shader.PropertyToID("_StormShellSeparation");
    private static readonly int StormRingFrequencyPropertyId = Shader.PropertyToID("_StormRingFrequency");
    private static readonly int StormRingThicknessPropertyId = Shader.PropertyToID("_StormRingThickness");
    private static readonly int StormTickProgressAPropertyId = Shader.PropertyToID("_StormTickProgressA");
    private static readonly int StormTickProgressBPropertyId = Shader.PropertyToID("_StormTickProgressB");
    private static readonly int StormTickActiveAPropertyId = Shader.PropertyToID("_StormTickActiveA");
    private static readonly int StormTickActiveBPropertyId = Shader.PropertyToID("_StormTickActiveB");
    private static readonly int SourceDischargeIntensityPropertyId = Shader.PropertyToID("_SourceDischargeIntensity");
    private static readonly int TerminalCapIntensityPropertyId = Shader.PropertyToID("_TerminalCapIntensity");
    private static readonly int ContactFlareIntensityPropertyId = Shader.PropertyToID("_ContactFlareIntensity");
    private static readonly int TerminalBlockedByWallPropertyId = Shader.PropertyToID("_TerminalBlockedByWall");
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies the shared body and endpoint shader properties to the provided property block.
    /// </summary>
    /// <param name="propertyBlock">Property block to populate.</param>
    /// <param name="palette">Resolved beam palette.</param>
    /// <param name="laserBeamConfig">Runtime passive config.</param>
    /// <param name="stormBurstNormalized">Normalized storm burst currently active on the beam.</param>
    /// <param name="stormTickProgressA">Progress vector of the first four active traveling damage packets.</param>
    /// <param name="stormTickProgressB">Progress vector of the next four active traveling damage packets.</param>
    /// <param name="stormTickActiveA">Active-state vector of the first four traveling damage packets.</param>
    /// <param name="stormTickActiveB">Active-state vector of the next four traveling damage packets.</param>
    /// <param name="segmentLength">Segment length value consumed by the shader.</param>
    /// <param name="widthScale">Width scale value consumed by the shader.</param>
    /// <param name="beamRole">Shader role selector used for body/source/terminal/contact rendering.</param>
    /// <param name="bodyLayerRole">Body-layer selector used only by body renderers.</param>
    /// <param name="capShape">Cap-shape selector consumed by endpoint rendering.</param>
    /// <param name="terminalBlockedByWall">True when the terminal point is a wall hit.</param>
    /// <param name="opacity">Final opacity multiplier.</param>
    /// <param name="coreBrightness">Final core brightness multiplier.</param>
    /// <param name="rimBrightness">Final rim brightness multiplier.</param>
    /// <param name="stormIdleIntensity">Final idle-storm intensity.</param>
    /// <param name="stormBurstIntensity">Final burst-storm intensity.</param>
    /// <param name="sourceDischargeIntensity">Final source-discharge intensity.</param>
    /// <param name="terminalCapIntensity">Final terminal-cap intensity.</param>
    /// <param name="contactFlareIntensity">Final contact-flare intensity.</param>
    public static void ApplySharedPaletteAndBeamProperties(MaterialPropertyBlock propertyBlock,
                                                           in PlayerLaserBeamResolvedPalette palette,
                                                           in LaserBeamPassiveConfig laserBeamConfig,
                                                           float stormBurstNormalized,
                                                           Vector4 stormTickProgressA,
                                                           Vector4 stormTickProgressB,
                                                           Vector4 stormTickActiveA,
                                                           Vector4 stormTickActiveB,
                                                           float segmentLength,
                                                           float widthScale,
                                                           float beamRole,
                                                           float bodyLayerRole,
                                                           float capShape,
                                                           bool terminalBlockedByWall,
                                                           float opacity,
                                                           float coreBrightness,
                                                           float rimBrightness,
                                                           float stormIdleIntensity,
                                                           float stormBurstIntensity,
                                                           float sourceDischargeIntensity,
                                                           float terminalCapIntensity,
                                                           float contactFlareIntensity)
    {
        if (propertyBlock == null)
            return;

        propertyBlock.SetColor(CoreColorPropertyId, palette.CoreColor);
        propertyBlock.SetColor(FlowColorPropertyId, palette.FlowColor);
        propertyBlock.SetColor(StormColorPropertyId, palette.StormColor);
        propertyBlock.SetColor(ContactColorPropertyId, palette.ContactColor);
        propertyBlock.SetFloat(OpacityPropertyId, math.saturate(opacity));
        propertyBlock.SetFloat(CoreBrightnessPropertyId, math.max(0f, coreBrightness));
        propertyBlock.SetFloat(RimBrightnessPropertyId, math.max(0f, rimBrightness));
        propertyBlock.SetFloat(FlowScrollSpeedPropertyId, math.max(0f, laserBeamConfig.FlowScrollSpeed));
        propertyBlock.SetFloat(FlowPulseFrequencyPropertyId, math.max(0f, laserBeamConfig.FlowPulseFrequency));
        propertyBlock.SetFloat(WobbleAmplitudePropertyId, math.max(0f, laserBeamConfig.WobbleAmplitude));
        propertyBlock.SetFloat(BubbleDriftSpeedPropertyId, math.max(0f, laserBeamConfig.BubbleDriftSpeed));
        propertyBlock.SetFloat(BeamRolePropertyId, beamRole);
        propertyBlock.SetFloat(BodyLayerRolePropertyId, bodyLayerRole);
        propertyBlock.SetFloat(CapShapePropertyId, capShape);
        propertyBlock.SetFloat(SegmentLengthPropertyId, math.max(0.05f, segmentLength));
        propertyBlock.SetFloat(WidthScalePropertyId, math.max(0.01f, widthScale));
        propertyBlock.SetFloat(CoreWidthMultiplierPropertyId, math.max(0.05f, laserBeamConfig.CoreWidthMultiplier));
        propertyBlock.SetFloat(StormTwistSpeedPropertyId, math.max(0f, laserBeamConfig.StormTwistSpeed));
        propertyBlock.SetFloat(StormIdleIntensityPropertyId, math.max(0f, stormIdleIntensity));
        propertyBlock.SetFloat(StormBurstIntensityPropertyId, math.max(0f, stormBurstIntensity));
        propertyBlock.SetFloat(StormBurstNormalizedPropertyId, math.saturate(stormBurstNormalized));
        propertyBlock.SetFloat(StormShellWidthMultiplierPropertyId, math.max(0.01f, laserBeamConfig.StormShellWidthMultiplier));
        propertyBlock.SetFloat(StormShellSeparationPropertyId, math.max(0f, laserBeamConfig.StormShellSeparation));
        propertyBlock.SetFloat(StormRingFrequencyPropertyId, math.max(0f, laserBeamConfig.StormRingFrequency));
        propertyBlock.SetFloat(StormRingThicknessPropertyId, math.max(0.01f, laserBeamConfig.StormRingThickness));
        propertyBlock.SetVector(StormTickProgressAPropertyId, stormTickProgressA);
        propertyBlock.SetVector(StormTickProgressBPropertyId, stormTickProgressB);
        propertyBlock.SetVector(StormTickActiveAPropertyId, stormTickActiveA);
        propertyBlock.SetVector(StormTickActiveBPropertyId, stormTickActiveB);
        propertyBlock.SetFloat(SourceDischargeIntensityPropertyId, math.max(0f, sourceDischargeIntensity));
        propertyBlock.SetFloat(TerminalCapIntensityPropertyId, math.max(0f, terminalCapIntensity));
        propertyBlock.SetFloat(ContactFlareIntensityPropertyId, math.max(0f, contactFlareIntensity));
        propertyBlock.SetFloat(TerminalBlockedByWallPropertyId, terminalBlockedByWall ? 1f : 0f);
    }

    /// <summary>
    /// Applies one incremental dissipation step to the currently assigned beam property block.
    /// </summary>
    /// <param name="propertyBlock">Property block to modify in place.</param>
    /// <param name="previousFadeNormalized">Previously applied remaining fade amount in the 0-1 range.</param>
    /// <param name="currentFadeNormalized">Current remaining fade amount in the 0-1 range.</param>
    public static void ApplyDissipationFadeStep(MaterialPropertyBlock propertyBlock,
                                                float previousFadeNormalized,
                                                float currentFadeNormalized)
    {
        if (propertyBlock == null)
            return;

        float previousOpacityFade = ResolveOpacityDissipationFactor(previousFadeNormalized);
        float currentOpacityFade = ResolveOpacityDissipationFactor(currentFadeNormalized);
        float previousEnergyFade = ResolveEnergyDissipationFactor(previousFadeNormalized);
        float currentEnergyFade = ResolveEnergyDissipationFactor(currentFadeNormalized);
        float opacityFadeRatio = previousOpacityFade > 1e-5f ? currentOpacityFade / previousOpacityFade : 0f;
        float energyFadeRatio = previousEnergyFade > 1e-5f ? currentEnergyFade / previousEnergyFade : 0f;
        propertyBlock.SetFloat(OpacityPropertyId, propertyBlock.GetFloat(OpacityPropertyId) * opacityFadeRatio);
        propertyBlock.SetFloat(CoreBrightnessPropertyId, propertyBlock.GetFloat(CoreBrightnessPropertyId) * energyFadeRatio);
        propertyBlock.SetFloat(RimBrightnessPropertyId, propertyBlock.GetFloat(RimBrightnessPropertyId) * energyFadeRatio);
        propertyBlock.SetFloat(StormIdleIntensityPropertyId, propertyBlock.GetFloat(StormIdleIntensityPropertyId) * energyFadeRatio);
        propertyBlock.SetFloat(StormBurstIntensityPropertyId, propertyBlock.GetFloat(StormBurstIntensityPropertyId) * energyFadeRatio);
        propertyBlock.SetFloat(SourceDischargeIntensityPropertyId, propertyBlock.GetFloat(SourceDischargeIntensityPropertyId) * energyFadeRatio);
        propertyBlock.SetFloat(TerminalCapIntensityPropertyId, propertyBlock.GetFloat(TerminalCapIntensityPropertyId) * energyFadeRatio);
        propertyBlock.SetFloat(ContactFlareIntensityPropertyId, propertyBlock.GetFloat(ContactFlareIntensityPropertyId) * energyFadeRatio);
    }

    /// <summary>
    /// Applies per-layer opacity and intensity adjustments so the body layers stay separated.
    /// </summary>
    /// <param name="layerRole">Layer role currently being rendered.</param>
    /// <param name="opacity">Mutable opacity multiplier.</param>
    /// <param name="coreBrightness">Mutable core brightness multiplier.</param>
    /// <param name="rimBrightness">Mutable rim brightness multiplier.</param>
    /// <param name="stormIdleIntensity">Mutable idle-storm multiplier.</param>
    /// <param name="stormBurstIntensity">Mutable burst-storm multiplier.</param>
    public static void ApplyBodyLayerOverrides(PlayerLaserBeamBodyLayerRole layerRole,
                                               ref float opacity,
                                               ref float coreBrightness,
                                               ref float rimBrightness,
                                               ref float stormIdleIntensity,
                                               ref float stormBurstIntensity)
    {
        switch (layerRole)
        {
            case PlayerLaserBeamBodyLayerRole.Core:
                opacity *= 0.62f;
                coreBrightness *= 0.96f;
                rimBrightness *= 0.2f;
                stormIdleIntensity *= 0.05f;
                stormBurstIntensity *= 0.08f;
                return;
            case PlayerLaserBeamBodyLayerRole.Storm:
                opacity *= 0.94f;
                coreBrightness *= 0.22f;
                rimBrightness *= 1.58f;
                stormIdleIntensity *= 1.46f;
                stormBurstIntensity *= 1.58f;
                return;
            default:
                opacity *= 1.04f;
                coreBrightness *= 0.82f;
                rimBrightness *= 1.06f;
                stormIdleIntensity *= 0.86f;
                stormBurstIntensity *= 0.98f;
                return;
        }
    }

    /// <summary>
    /// Resolves the particle gradient colors used by the requested endpoint role.
    /// </summary>
    /// <param name="palette">Resolved beam palette.</param>
    /// <param name="visualRole">Endpoint visual role.</param>
    /// <param name="minimumColor">Gradient minimum color.</param>
    /// <param name="maximumColor">Gradient maximum color.</param>
    public static void ResolveParticleGradientColors(in PlayerLaserBeamResolvedPalette palette,
                                                     PlayerLaserBeamEndpointVisualRole visualRole,
                                                     out Color minimumColor,
                                                     out Color maximumColor)
    {
        switch (visualRole)
        {
            case PlayerLaserBeamEndpointVisualRole.Source:
                minimumColor = Color.Lerp(palette.FlowColor, palette.CoreColor, 0.5f);
                maximumColor = Color.Lerp(palette.StormColor, palette.CoreColor, 0.42f);
                return;
            case PlayerLaserBeamEndpointVisualRole.ContactFlare:
                minimumColor = Color.Lerp(palette.ContactColor, palette.StormColor, 0.25f);
                maximumColor = Color.Lerp(palette.ContactColor, Color.white, 0.38f);
                return;
            default:
                minimumColor = Color.Lerp(palette.FlowColor, palette.ContactColor, 0.4f);
                maximumColor = Color.Lerp(palette.ContactColor, Color.white, 0.34f);
                return;
        }
    }

    /// <summary>
    /// Resolves the role-specific opacity multiplier used by endpoint effects.
    /// </summary>
    /// <param name="laserBeamConfig">Runtime passive config.</param>
    /// <param name="visualRole">Endpoint visual role.</param>
    /// <returns>Endpoint opacity multiplier.</returns>
    public static float ResolveEndpointOpacity(in LaserBeamPassiveConfig laserBeamConfig,
                                               PlayerLaserBeamEndpointVisualRole visualRole)
    {
        switch (visualRole)
        {
            case PlayerLaserBeamEndpointVisualRole.Source:
                return laserBeamConfig.BodyOpacity * 0.98f;
            case PlayerLaserBeamEndpointVisualRole.ContactFlare:
                return laserBeamConfig.BodyOpacity;
            default:
                return laserBeamConfig.BodyOpacity * 0.99f;
        }
    }

    /// <summary>
    /// Resolves the role-specific core brightness used by endpoint effects.
    /// </summary>
    /// <param name="laserBeamConfig">Runtime passive config.</param>
    /// <param name="visualRole">Endpoint visual role.</param>
    /// <returns>Endpoint core brightness multiplier.</returns>
    public static float ResolveEndpointCoreBrightness(in LaserBeamPassiveConfig laserBeamConfig,
                                                      PlayerLaserBeamEndpointVisualRole visualRole)
    {
        switch (visualRole)
        {
            case PlayerLaserBeamEndpointVisualRole.Source:
                return laserBeamConfig.CoreBrightness * 1.14f;
            case PlayerLaserBeamEndpointVisualRole.ContactFlare:
                return laserBeamConfig.CoreBrightness * 1.22f;
            default:
                return laserBeamConfig.CoreBrightness * 1.2f;
        }
    }

    /// <summary>
    /// Resolves the role-specific rim brightness used by endpoint effects.
    /// </summary>
    /// <param name="laserBeamConfig">Runtime passive config.</param>
    /// <param name="visualRole">Endpoint visual role.</param>
    /// <returns>Endpoint rim brightness multiplier.</returns>
    public static float ResolveEndpointRimBrightness(in LaserBeamPassiveConfig laserBeamConfig,
                                                     PlayerLaserBeamEndpointVisualRole visualRole)
    {
        switch (visualRole)
        {
            case PlayerLaserBeamEndpointVisualRole.Source:
                return laserBeamConfig.RimBrightness * 0.96f;
            case PlayerLaserBeamEndpointVisualRole.ContactFlare:
                return laserBeamConfig.RimBrightness * 1.36f;
            default:
                return laserBeamConfig.RimBrightness * 1.12f;
        }
    }

    /// <summary>
    /// Resolves the role-specific idle-storm intensity used by endpoint effects.
    /// </summary>
    /// <param name="laserBeamConfig">Runtime passive config.</param>
    /// <param name="visualRole">Endpoint visual role.</param>
    /// <returns>Endpoint idle-storm intensity.</returns>
    public static float ResolveEndpointStormIdleIntensity(in LaserBeamPassiveConfig laserBeamConfig,
                                                          PlayerLaserBeamEndpointVisualRole visualRole)
    {
        switch (visualRole)
        {
            case PlayerLaserBeamEndpointVisualRole.Source:
                return laserBeamConfig.StormIdleIntensity * 0.9f;
            case PlayerLaserBeamEndpointVisualRole.ContactFlare:
                return laserBeamConfig.StormIdleIntensity * 1.18f;
            default:
                return laserBeamConfig.StormIdleIntensity * 1.02f;
        }
    }

    /// <summary>
    /// Resolves the role-specific burst-storm intensity used by endpoint effects.
    /// </summary>
    /// <param name="laserBeamConfig">Runtime passive config.</param>
    /// <param name="visualRole">Endpoint visual role.</param>
    /// <returns>Endpoint burst-storm intensity.</returns>
    public static float ResolveEndpointStormBurstIntensity(in LaserBeamPassiveConfig laserBeamConfig,
                                                           PlayerLaserBeamEndpointVisualRole visualRole)
    {
        switch (visualRole)
        {
            case PlayerLaserBeamEndpointVisualRole.Source:
                return laserBeamConfig.StormBurstIntensity * 0.86f;
            case PlayerLaserBeamEndpointVisualRole.ContactFlare:
                return laserBeamConfig.StormBurstIntensity * 1.22f;
            default:
                return laserBeamConfig.StormBurstIntensity * 1.08f;
        }
    }

    /// <summary>
    /// Resolves the progress and active-state vectors used by the shader to render the currently started tick packets.
    /// </summary>
    /// <param name="laserBeamConfig">Runtime passive config.</param>
    /// <param name="laserBeamState">Runtime beam state.</param>
    /// <param name="stormTickProgressA">Progress vector of the first four active packets.</param>
    /// <param name="stormTickProgressB">Progress vector of the next four active packets.</param>
    /// <param name="stormTickActiveA">Active-state vector of the first four packets.</param>
    /// <param name="stormTickActiveB">Active-state vector of the next four packets.</param>
    public static void ResolveStormTickPulseVectors(in LaserBeamPassiveConfig laserBeamConfig,
                                                    in PlayerLaserBeamState laserBeamState,
                                                    out Vector4 stormTickProgressA,
                                                    out Vector4 stormTickProgressB,
                                                    out Vector4 stormTickActiveA,
                                                    out Vector4 stormTickActiveB)
    {
        float travelSpeed = math.max(0f, laserBeamConfig.StormTickTravelSpeed);
        stormTickProgressA = new Vector4(1f, 1f, 1f, 1f);
        stormTickProgressB = new Vector4(1f, 1f, 1f, 1f);
        stormTickActiveA = Vector4.zero;
        stormTickActiveB = Vector4.zero;

        if (travelSpeed <= 0f)
            return;

        float travelDurationSeconds = PlayerLaserBeamStateUtility.ResolveStormTickTravelDurationSeconds(travelSpeed);
        float totalDurationSeconds = PlayerLaserBeamStateUtility.ResolveStormTickTotalDurationSeconds(in laserBeamConfig);
        int renderedPulseCount = 0;

        for (int pulseIndex = 0;
             pulseIndex < laserBeamState.StormTickPulses.Length && renderedPulseCount < 8;
             pulseIndex++)
        {
            PlayerLaserBeamStormTickPulse pulse = laserBeamState.StormTickPulses[pulseIndex];

            if (pulse.CurrentElapsedSeconds < 0f || pulse.CurrentElapsedSeconds >= totalDurationSeconds)
                continue;

            float pulseProgress = pulse.CurrentElapsedSeconds >= travelDurationSeconds
                ? 2f
                : PlayerLaserBeamStateUtility.ResolveNormalizedStormTickProgress(pulse.CurrentElapsedSeconds, travelSpeed);
            AssignStormTickPulse(ref stormTickProgressA,
                                 ref stormTickProgressB,
                                 ref stormTickActiveA,
                                 ref stormTickActiveB,
                                 renderedPulseCount,
                                 pulseProgress);
            renderedPulseCount++;
        }
    }

    /// <summary>
    /// Resolves the endpoint direction used by the requested role.
    /// </summary>
    /// <param name="endpoint">Per-lane endpoint metadata.</param>
    /// <param name="visualRole">Endpoint visual role.</param>
    /// <returns>Forward direction used by the visual.</returns>
    public static float3 ResolveEndpointDirection(in PlayerLaserBeamLaneEndpoint endpoint,
                                                  PlayerLaserBeamEndpointVisualRole visualRole)
    {
        switch (visualRole)
        {
            case PlayerLaserBeamEndpointVisualRole.Source:
                return endpoint.StartDirection;
            default:
                return endpoint.EndDirection;
        }
    }

    /// <summary>
    /// Resolves the anchor point used by the requested endpoint role.
    /// </summary>
    /// <param name="endpoint">Per-lane endpoint metadata.</param>
    /// <param name="visualRole">Endpoint visual role.</param>
    /// <returns>World-space anchor point.</returns>
    public static float3 ResolveEndpointAnchorPoint(in PlayerLaserBeamLaneEndpoint endpoint,
                                                    PlayerLaserBeamEndpointVisualRole visualRole)
    {
        switch (visualRole)
        {
            case PlayerLaserBeamEndpointVisualRole.Source:
                return math.lerp(endpoint.MuzzlePoint, endpoint.VisibleStartPoint, SourceAnchorBlend);
            case PlayerLaserBeamEndpointVisualRole.ContactFlare:
                return endpoint.EndPoint -
                       math.normalizesafe(endpoint.EndDirection, new float3(0f, 0f, 1f)) *
                       ResolveEndpointEmbedDistance(endpoint.EndWidth,
                                                    ContactFlareEmbedDistanceBias,
                                                    ContactFlareEmbedDistanceFactor,
                                                    0.08f);
            default:
                return endpoint.EndPoint -
                       math.normalizesafe(endpoint.EndDirection, new float3(0f, 0f, 1f)) *
                       ResolveEndpointEmbedDistance(endpoint.EndWidth,
                                                    TerminalCapEmbedDistanceBias,
                                                    TerminalCapEmbedDistanceFactor,
                                                    0.12f);
        }
    }

    /// <summary>
    /// Resolves the width inherited by the requested endpoint role.
    /// </summary>
    /// <param name="endpoint">Per-lane endpoint metadata.</param>
    /// <param name="visualRole">Endpoint visual role.</param>
    /// <returns>Beam width consumed by endpoint scaling.</returns>
    public static float ResolveEndpointWidth(in PlayerLaserBeamLaneEndpoint endpoint,
                                             PlayerLaserBeamEndpointVisualRole visualRole)
    {
        switch (visualRole)
        {
            case PlayerLaserBeamEndpointVisualRole.Source:
                return endpoint.StartWidth;
            default:
                return endpoint.EndWidth;
        }
    }

    /// <summary>
    /// Resolves the shared visual-config forward offset used by the requested endpoint role.
    /// </summary>
    /// <param name="visualConfig">Shared visual config.</param>
    /// <param name="visualRole">Endpoint visual role.</param>
    /// <returns>Forward offset in world units.</returns>
    public static float ResolveEndpointForwardOffset(in PlayerLaserBeamVisualConfig visualConfig,
                                                     PlayerLaserBeamEndpointVisualRole visualRole)
    {
        switch (visualRole)
        {
            case PlayerLaserBeamEndpointVisualRole.Source:
                return visualConfig.SourceForwardOffset;
            default:
                return visualConfig.ImpactForwardOffset;
        }
    }

    /// <summary>
    /// Resolves the authored endpoint scale multiplier used by the requested role.
    /// </summary>
    /// <param name="laserBeamConfig">Runtime passive config.</param>
    /// <param name="visualRole">Endpoint visual role.</param>
    /// <returns>Authored scale multiplier.</returns>
    public static float ResolveEndpointScaleMultiplier(in LaserBeamPassiveConfig laserBeamConfig,
                                                       PlayerLaserBeamEndpointVisualRole visualRole)
    {
        switch (visualRole)
        {
            case PlayerLaserBeamEndpointVisualRole.Source:
                return math.max(0.01f, laserBeamConfig.SourceScaleMultiplier);
            case PlayerLaserBeamEndpointVisualRole.ContactFlare:
                return math.max(0.01f, laserBeamConfig.ContactFlareScaleMultiplier);
            default:
                return math.max(0.01f, laserBeamConfig.TerminalCapScaleMultiplier);
        }
    }

    /// <summary>
    /// Resolves the endpoint rotation used by source, terminal-cap and contact-flare visuals.
    /// </summary>
    /// <param name="endpoint">Per-lane endpoint metadata.</param>
    /// <param name="direction">Forward direction used by the current endpoint.</param>
    /// <param name="visualRole">Endpoint visual role.</param>
    /// <returns>Endpoint world rotation.</returns>
    public static quaternion ResolveEndpointRotation(in PlayerLaserBeamLaneEndpoint endpoint,
                                                     float3 direction,
                                                     PlayerLaserBeamEndpointVisualRole visualRole)
    {
        if (visualRole == PlayerLaserBeamEndpointVisualRole.Source)
            return quaternion.LookRotationSafe(direction, new float3(0f, 1f, 0f));

        if (endpoint.TerminalBlockedByWall != 0 && math.lengthsq(endpoint.TerminalNormal) > 1e-5f)
        {
            return quaternion.LookRotationSafe(-math.normalizesafe(endpoint.TerminalNormal, direction),
                                               math.normalizesafe(direction, new float3(0f, 1f, 0f)));
        }

        return quaternion.LookRotationSafe(direction, new float3(0f, 1f, 0f));
    }

    /// <summary>
    /// Compresses endpoint effect scale so source, terminal-cap and contact visuals remain readable.
    /// </summary>
    /// <param name="rawWidth">Raw beam width inherited from gameplay lane generation.</param>
    /// <param name="authoredScaleMultiplier">Authored endpoint scale multiplier from the Laser Beam passive config.</param>
    /// <param name="visualRole">Endpoint visual role.</param>
    /// <returns>Compressed uniform endpoint effect scale.</returns>
    public static float ResolveEndpointVisualScale(float rawWidth,
                                                   float authoredScaleMultiplier,
                                                   PlayerLaserBeamEndpointVisualRole visualRole)
    {
        float baseFactor;
        float maximumScale;

        switch (visualRole)
        {
            case PlayerLaserBeamEndpointVisualRole.Source:
                baseFactor = 0.68f;
                maximumScale = 1.58f;
                break;
            case PlayerLaserBeamEndpointVisualRole.ContactFlare:
                baseFactor = 0.96f;
                maximumScale = 2.1f;
                break;
            default:
                baseFactor = 0.82f;
                maximumScale = 1.84f;
                break;
        }

        float compressedScale = (0.16f + baseFactor * math.pow(math.max(0.01f, rawWidth), 0.56f)) *
                                math.sqrt(math.max(0.01f, authoredScaleMultiplier));
        return math.clamp(compressedScale, 0.12f, maximumScale);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Writes one traveling packet progress into the appropriate shader vector slot.
    /// </summary>
    /// <param name="stormTickProgressA">Progress vector of the first four packets.</param>
    /// <param name="stormTickProgressB">Progress vector of the next four packets.</param>
    /// <param name="stormTickActiveA">Active-state vector of the first four packets.</param>
    /// <param name="stormTickActiveB">Active-state vector of the next four packets.</param>
    /// <param name="renderedPulseIndex">Zero-based rendered pulse slot.</param>
    /// <param name="pulseProgress">Normalized progress of the packet. Values above 1 keep the trail active while hiding the head.</param>
    private static void AssignStormTickPulse(ref Vector4 stormTickProgressA,
                                             ref Vector4 stormTickProgressB,
                                             ref Vector4 stormTickActiveA,
                                             ref Vector4 stormTickActiveB,
                                             int renderedPulseIndex,
                                             float pulseProgress)
    {
        Vector4 progressVector = renderedPulseIndex < 4 ? stormTickProgressA : stormTickProgressB;
        Vector4 activeVector = renderedPulseIndex < 4 ? stormTickActiveA : stormTickActiveB;
        int componentIndex = renderedPulseIndex % 4;

        switch (componentIndex)
        {
            case 0:
                progressVector.x = pulseProgress;
                activeVector.x = 1f;
                break;
            case 1:
                progressVector.y = pulseProgress;
                activeVector.y = 1f;
                break;
            case 2:
                progressVector.z = pulseProgress;
                activeVector.z = 1f;
                break;
            default:
                progressVector.w = pulseProgress;
                activeVector.w = 1f;
                break;
        }

        if (renderedPulseIndex < 4)
        {
            stormTickProgressA = progressVector;
            stormTickActiveA = activeVector;
            return;
        }

        stormTickProgressB = progressVector;
        stormTickActiveB = activeVector;
    }

    /// <summary>
    /// Resolves the amount the endpoint visual should sink into the body so the handoff reads as one continuous beam.
    /// </summary>
    /// <param name="endWidth">Beam width resolved at the lane endpoint.</param>
    /// <param name="distanceBias">Minimum embed distance applied even on thin beams.</param>
    /// <param name="distanceFactor">Additional distance derived from the current endpoint width.</param>
    /// <param name="maximumDistance">Upper bound used to avoid over-sinking the effect.</param>
    /// <returns>Embed distance in world units.</returns>
    private static float ResolveEndpointEmbedDistance(float endWidth,
                                                      float distanceBias,
                                                      float distanceFactor,
                                                      float maximumDistance)
    {
        float resolvedWidth = math.max(0.02f, endWidth);
        float embedDistance = distanceBias + resolvedWidth * distanceFactor;
        return math.min(maximumDistance, embedDistance);
    }

    /// <summary>
    /// Resolves the target opacity multiplier used by the shutdown dissipation curve.
    /// </summary>
    /// <param name="fadeNormalized">Remaining fade amount in the 0-1 range.</param>
    /// <returns>Target opacity multiplier.</returns>
    private static float ResolveOpacityDissipationFactor(float fadeNormalized)
    {
        float clampedFade = math.saturate(fadeNormalized);
        return clampedFade * clampedFade * (3f - 2f * clampedFade);
    }

    /// <summary>
    /// Resolves the target energy multiplier used by brightness and endpoint intensities during shutdown dissipation.
    /// </summary>
    /// <param name="fadeNormalized">Remaining fade amount in the 0-1 range.</param>
    /// <returns>Target energy multiplier.</returns>
    private static float ResolveEnergyDissipationFactor(float fadeNormalized)
    {
        return math.sqrt(ResolveOpacityDissipationFactor(fadeNormalized));
    }
    #endregion

    #endregion
}
