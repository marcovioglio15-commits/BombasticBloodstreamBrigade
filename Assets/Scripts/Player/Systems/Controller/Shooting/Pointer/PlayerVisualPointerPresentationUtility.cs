using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Provides the shared straight beam mesh, material property assignment, and geometry resolution helpers used by the aiming laser pointer presentation.
/// The pointer reuses the Laser Beam body material with all motion disabled so it renders as a steady precision sight line.
/// </summary>
internal static class PlayerVisualPointerPresentationUtility
{
    #region Constants
    private const int TubeSideCount = 12;
    private const float TubeRadius = 0.5f;
    private const float DefaultBrightness = 1f;

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

    #region Fields
    private static readonly MaterialPropertyBlock sharedPropertyBlock = new MaterialPropertyBlock();
    private static readonly Vector4 idlePacketProgress = new Vector4(1f, 1f, 1f, 1f);
    private static Mesh beamMesh;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the cached straight beam mesh, building it once as a unit-length tube aligned with the local forward axis.
    /// </summary>
    /// <returns>Shared straight beam mesh reused by every pointer draw.</returns>
    public static Mesh GetOrCreateBeamMesh()
    {
        if (beamMesh != null)
            return beamMesh;

        beamMesh = BuildBeamMesh();
        return beamMesh;
    }

    /// <summary>
    /// Resolves the final rendered pointer length from the current shooting reach and the orbital-shot freeze rule.
    /// </summary>
    /// <param name="config">Baked pointer config.</param>
    /// <param name="passiveToolsState">Current aggregated passive-tool state used to read range modifiers and orbital activation.</param>
    /// <param name="shootingValues">Current runtime shooting values providing speed, range and lifetime.</param>
    /// <returns>Clamped rendered pointer length in world units.</returns>
    public static float ResolveLength(in PlayerVisualPointerConfig config,
                                      in PlayerPassiveToolsState passiveToolsState,
                                      in ShootingValuesBlob shootingValues)
    {
        bool freezeForOrbital = config.FreezeWithOrbitalProjectiles != 0 && passiveToolsState.HasPerfectCircle != 0;

        // Orbital shots no longer travel straight, so the pointer length is held at a fixed authored value instead of tracking the range.
        if (freezeForOrbital)
        {
            float frozenLength = config.OrbitalFrozenLength > 0f ? config.OrbitalFrozenLength : config.BaseStraightLength;
            return math.clamp(frozenLength, 0f, PlayerLaserBeamUtility.MaximumSupportedTravelDistance);
        }

        // Reuse the Laser Beam travel-distance math so the pointer reach matches where a straight projectile would despawn.
        float speed = math.max(0f, shootingValues.ShootSpeed * math.max(0f, passiveToolsState.ProjectileSpeedMultiplier));
        float range = math.max(0f, shootingValues.Range * math.max(0f, passiveToolsState.ProjectileLifetimeRangeMultiplier));
        float lifetime = math.max(0f, shootingValues.Lifetime * math.max(0f, passiveToolsState.ProjectileLifetimeSecondsMultiplier));
        float resolvedLength = PlayerLaserBeamUtility.ResolveMaximumTravelDistance(speed, range, lifetime) * config.LengthMultiplier;

        if (config.MaxLength > 0f)
            resolvedLength = math.min(resolvedLength, config.MaxLength);

        return math.clamp(resolvedLength, 0f, PlayerLaserBeamUtility.MaximumSupportedTravelDistance);
    }

    /// <summary>
    /// Resolves the world-space pointer origin through the same baked muzzle anchor path used by projectile requests.
    /// </summary>
    /// <param name="playerEntity">Player entity that owns the pointer and shooting components.</param>
    /// <param name="localTransform">Current player transform used as origin fallback.</param>
    /// <param name="runtimeShootingConfig">Runtime shooting config containing the scalable shoot offset.</param>
    /// <param name="muzzleLookup">Read-only lookup for the baked shooter muzzle anchor.</param>
    /// <param name="transformLookup">Read-only transform lookup used by the spawn-position resolver.</param>
    /// <param name="localToWorldLookup">Read-only world transform lookup used by the spawn-position resolver.</param>
    /// <param name="verticalLift">Vertical lift applied to avoid floor z-fighting.</param>
    /// <returns>World-space pointer origin.</returns>
    public static float3 ResolveOrigin(Entity playerEntity,
                                       in LocalTransform localTransform,
                                       in PlayerRuntimeShootingConfig runtimeShootingConfig,
                                       in ComponentLookup<ShooterMuzzleAnchor> muzzleLookup,
                                       in ComponentLookup<LocalTransform> transformLookup,
                                       in ComponentLookup<LocalToWorld> localToWorldLookup,
                                       float verticalLift)
    {
        float3 origin = PlayerProjectileRequestUtility.ResolveShootSpawnPosition(playerEntity,
                                                                                 in localTransform,
                                                                                 in runtimeShootingConfig,
                                                                                 in muzzleLookup,
                                                                                 in transformLookup,
                                                                                 in localToWorldLookup);
        origin.y += verticalLift;
        return origin;
    }

    /// <summary>
    /// Fills and returns the shared material property block with steady (motion-free) Laser Beam body properties for the pointer.
    /// </summary>
    /// <param name="config">Baked pointer config providing palette colors, opacity and width.</param>
    /// <param name="length">Final rendered pointer length used by the body gradient.</param>
    /// <returns>Shared material property block ready to render one pointer instance.</returns>
    public static MaterialPropertyBlock BuildPropertyBlock(in PlayerVisualPointerConfig config, float length)
    {
        sharedPropertyBlock.Clear();
        sharedPropertyBlock.SetColor(CoreColorPropertyId, ToColor(config.CoreColor));
        sharedPropertyBlock.SetColor(FlowColorPropertyId, ToColor(config.FlowColor));
        sharedPropertyBlock.SetColor(StormColorPropertyId, ToColor(config.StormColor));
        sharedPropertyBlock.SetColor(ContactColorPropertyId, ToColor(config.ContactColor));
        sharedPropertyBlock.SetFloat(OpacityPropertyId, math.saturate(config.Opacity));
        sharedPropertyBlock.SetFloat(CoreBrightnessPropertyId, DefaultBrightness);
        sharedPropertyBlock.SetFloat(RimBrightnessPropertyId, DefaultBrightness);

        // Disable every animated channel so the aiming pointer stays a still, readable line instead of an electric beam.
        sharedPropertyBlock.SetFloat(FlowScrollSpeedPropertyId, 0f);
        sharedPropertyBlock.SetFloat(FlowPulseFrequencyPropertyId, 0f);
        sharedPropertyBlock.SetFloat(WobbleAmplitudePropertyId, 0f);
        sharedPropertyBlock.SetFloat(BubbleDriftSpeedPropertyId, 0f);
        sharedPropertyBlock.SetFloat(StormTwistSpeedPropertyId, 0f);
        sharedPropertyBlock.SetFloat(StormIdleIntensityPropertyId, 0f);
        sharedPropertyBlock.SetFloat(StormBurstIntensityPropertyId, 0f);
        sharedPropertyBlock.SetFloat(StormBurstNormalizedPropertyId, 0f);
        sharedPropertyBlock.SetFloat(SourceDischargeIntensityPropertyId, 0f);
        sharedPropertyBlock.SetFloat(TerminalCapIntensityPropertyId, 0f);
        sharedPropertyBlock.SetFloat(ContactFlareIntensityPropertyId, 0f);
        sharedPropertyBlock.SetFloat(TerminalBlockedByWallPropertyId, 0f);

        // Mark no active traveling damage packets so the storm helix stays hidden on the pointer body.
        sharedPropertyBlock.SetVector(StormTickProgressAPropertyId, idlePacketProgress);
        sharedPropertyBlock.SetVector(StormTickProgressBPropertyId, idlePacketProgress);
        sharedPropertyBlock.SetVector(StormTickActiveAPropertyId, Vector4.zero);
        sharedPropertyBlock.SetVector(StormTickActiveBPropertyId, Vector4.zero);

        // Neutral storm shell so any residual shell sampling collapses onto the body silhouette.
        sharedPropertyBlock.SetFloat(StormShellWidthMultiplierPropertyId, 1f);
        sharedPropertyBlock.SetFloat(StormShellSeparationPropertyId, 0f);
        sharedPropertyBlock.SetFloat(StormRingFrequencyPropertyId, 0f);
        sharedPropertyBlock.SetFloat(StormRingThicknessPropertyId, 1f);

        // Body role rendering with the flow sheath layer keeps the pointer reading as the main beam volume.
        sharedPropertyBlock.SetFloat(BeamRolePropertyId, 0f);
        sharedPropertyBlock.SetFloat(BodyLayerRolePropertyId, (float)PlayerLaserBeamBodyLayerRole.Flow);
        sharedPropertyBlock.SetFloat(CapShapePropertyId, 0f);
        sharedPropertyBlock.SetFloat(CoreWidthMultiplierPropertyId, 1f);
        sharedPropertyBlock.SetFloat(SegmentLengthPropertyId, math.max(0.05f, length));
        sharedPropertyBlock.SetFloat(WidthScalePropertyId, math.max(0.01f, config.Width));
        return sharedPropertyBlock;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds the unit-length tube mesh spanning local Z from 0 to 1 with a radius of half a unit, so a TRS scale maps directly to width and length.
    /// </summary>
    /// <returns>New straight beam tube mesh with source-to-tip UVs and end caps.</returns>
    private static Mesh BuildBeamMesh()
    {
        int ringVertexCount = TubeSideCount + 1;
        Vector3[] vertices = new Vector3[ringVertexCount * 2 + 2];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uvs = new Vector2[vertices.Length];

        // Two rings (source at z=0, tip at z=1) duplicated at the seam so circumferential UVs stay continuous.
        for (int sideIndex = 0; sideIndex <= TubeSideCount; sideIndex++)
        {
            float angleRadians = math.PI * 2f * sideIndex / TubeSideCount;
            float cosine = math.cos(angleRadians);
            float sine = math.sin(angleRadians);
            Vector3 radialNormal = new Vector3(cosine, sine, 0f);
            Vector3 radialOffset = radialNormal * TubeRadius;
            float circumferentialUv = sideIndex / (float)TubeSideCount;

            vertices[sideIndex] = new Vector3(radialOffset.x, radialOffset.y, 0f);
            normals[sideIndex] = radialNormal;
            uvs[sideIndex] = new Vector2(0f, circumferentialUv);

            vertices[ringVertexCount + sideIndex] = new Vector3(radialOffset.x, radialOffset.y, 1f);
            normals[ringVertexCount + sideIndex] = radialNormal;
            uvs[ringVertexCount + sideIndex] = new Vector2(1f, circumferentialUv);
        }

        int startCapCenterIndex = ringVertexCount * 2;
        int endCapCenterIndex = startCapCenterIndex + 1;
        vertices[startCapCenterIndex] = new Vector3(0f, 0f, 0f);
        normals[startCapCenterIndex] = new Vector3(0f, 0f, -1f);
        uvs[startCapCenterIndex] = new Vector2(0f, 0.5f);
        vertices[endCapCenterIndex] = new Vector3(0f, 0f, 1f);
        normals[endCapCenterIndex] = new Vector3(0f, 0f, 1f);
        uvs[endCapCenterIndex] = new Vector2(1f, 0.5f);

        int[] triangles = new int[TubeSideCount * 6 + TubeSideCount * 6];
        int triangleCursor = 0;

        for (int sideIndex = 0; sideIndex < TubeSideCount; sideIndex++)
        {
            int startA = sideIndex;
            int startB = sideIndex + 1;
            int endA = ringVertexCount + sideIndex;
            int endB = ringVertexCount + sideIndex + 1;

            // Side quad bridging the source ring to the tip ring.
            triangles[triangleCursor++] = startA;
            triangles[triangleCursor++] = endA;
            triangles[triangleCursor++] = startB;
            triangles[triangleCursor++] = startB;
            triangles[triangleCursor++] = endA;
            triangles[triangleCursor++] = endB;

            // Start cap fan facing the muzzle.
            triangles[triangleCursor++] = startCapCenterIndex;
            triangles[triangleCursor++] = startB;
            triangles[triangleCursor++] = startA;

            // Tip cap fan facing forward, wound to match the Laser Beam body tube outward face.
            triangles[triangleCursor++] = endA;
            triangles[triangleCursor++] = endCapCenterIndex;
            triangles[triangleCursor++] = endB;
        }

        Mesh mesh = new Mesh
        {
            name = "PlayerVisualPointerBeam",
            hideFlags = HideFlags.HideAndDontSave
        };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0, true);
        return mesh;
    }

    /// <summary>
    /// Converts one linear float4 color into a managed Unity color consumed by the body shader property block.
    /// </summary>
    /// <param name="value">Linear float4 color baked into the pointer config.</param>
    /// <returns>Managed color reused by the property block.</returns>
    private static Color ToColor(float4 value)
    {
        return new Color(value.x, value.y, value.z, value.w);
    }
    #endregion

    #endregion
}
