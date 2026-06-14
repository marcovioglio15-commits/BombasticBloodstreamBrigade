using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Builds the volumetric 3D Laser Beam body mesh from authoritative lane samples.
/// </summary>
internal static class PlayerLaserBeamPresentationRuntimeMeshUtility
{
    #region Constants
    private const int TubeSideCount = 10;
    private const float MinimumTubeRadius = 0.01f;
    private const float SourceInitialApertureScale = 0.42f;
    private const float SourceMidApertureScale = 0.88f;
    private const float SourceCollarBulgeStrength = 0.06f;
    private const float TerminalShoulderBulgeStrength = 0.07f;
    #endregion

    #region Fields
    private static readonly float angleStepRadians = math.PI * 2f / TubeSideCount;
    private static readonly List<Vector3> sharedVertices = new List<Vector3>(768);
    private static readonly List<Vector3> sharedNormals = new List<Vector3>(768);
    private static readonly List<int> sharedTriangles = new List<int>(1536);
    private static readonly List<Vector2> sharedUvs = new List<Vector2>(768);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds one managed body visual as a volumetric prism-tube that follows the authoritative lane path.
    /// </summary>
    /// <param name="visual">Managed body visual that owns the dynamic mesh.</param>
    /// <param name="laneVisual">Render-time lane metadata describing the point range.</param>
    /// <param name="ribbonPoints">Shared point list containing all sampled lane points.</param>
    /// <param name="visualConfig">Shared beam visual config used to shape the terminal closure.</param>
    /// <param name="laserBeamConfig">Runtime passive config driving width and storm response.</param>
    /// <param name="laserBeamState">Runtime state used to resolve the current storm-burst strength.</param>
    /// <param name="elapsedTimeSeconds">Global elapsed time used by the body breathing animation.</param>
    public static void BuildBodyVolumeMesh(PlayerLaserBeamManagedBodyVisual visual,
                                           in PlayerLaserBeamLaneVisual laneVisual,
                                           List<PlayerLaserBeamRibbonPoint> ribbonPoints,
                                           in PlayerLaserBeamVisualConfig visualConfig,
                                           in LaserBeamPassiveConfig laserBeamConfig,
                                           in PlayerLaserBeamState laserBeamState,
                                           float elapsedTimeSeconds)
    {
        if (visual == null || visual.DynamicMesh == null)
            return;

        if (laneVisual.PointCount < 2)
        {
            visual.DynamicMesh.Clear(false);
            return;
        }

        sharedVertices.Clear();
        sharedNormals.Clear();
        sharedTriangles.Clear();
        sharedUvs.Clear();

        float laneLength = math.max(visualConfig.MinimumSegmentLength, laneVisual.TotalLength);
        float stormBurstNormalized = ResolveStormBurstNormalized(in laserBeamConfig, in laserBeamState);
        float3 minimumBounds = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
        float3 maximumBounds = new float3(float.MinValue, float.MinValue, float.MinValue);
        float3 transportedNormal = ResolveInitialFrameNormal(ribbonPoints,
                                                             laneVisual.PointStartIndex,
                                                             laneVisual.PointCount);
        float3 firstTangent = ResolvePointTangent(ribbonPoints,
                                                  laneVisual.PointStartIndex,
                                                  laneVisual.PointCount,
                                                  0);
        float3 lastTangent = firstTangent;
        int firstRingStartIndex = -1;
        int previousRingStartIndex = -1;

        // Build one shaped ring for each sampled point along the authoritative lane.
        for (int pointIndex = 0; pointIndex < laneVisual.PointCount; pointIndex++)
        {
            int absolutePointIndex = laneVisual.PointStartIndex + pointIndex;
            PlayerLaserBeamRibbonPoint point = ribbonPoints[absolutePointIndex];
            float3 tangent = ResolvePointTangent(ribbonPoints,
                                                 laneVisual.PointStartIndex,
                                                 laneVisual.PointCount,
                                                 pointIndex);

            if (pointIndex > 0)
                transportedNormal = TransportFrameNormal(transportedNormal, tangent);

            float3 binormal = ResolveFrameBinormal(transportedNormal, tangent);
            transportedNormal = ResolveFrameNormal(binormal, tangent);
            float normalizedDistance = math.saturate(point.Distance / laneLength);
            float diameter = ResolveTubeDiameter(point,
                                                 normalizedDistance,
                                                 laneLength,
                                                 stormBurstNormalized,
                                                 in visualConfig,
                                                 in laneVisual,
                                                 in laserBeamConfig,
                                                 elapsedTimeSeconds);
            int ringStartIndex = AddTubeRing(point.Position,
                                             tangent,
                                             transportedNormal,
                                             binormal,
                                             diameter * 0.5f,
                                             normalizedDistance,
                                             in laserBeamConfig,
                                             ref minimumBounds,
                                             ref maximumBounds);

            if (pointIndex == 0)
            {
                firstRingStartIndex = ringStartIndex;
                AddStartCap(point.Position,
                            tangent,
                            ringStartIndex,
                            ref minimumBounds,
                            ref maximumBounds);
            }

            if (previousRingStartIndex >= 0)
                AddTubeBridge(previousRingStartIndex, ringStartIndex);

            previousRingStartIndex = ringStartIndex;
            lastTangent = tangent;
        }

        // Close the final ring with a rounded cap instead of a pointed comet tip.
        if (previousRingStartIndex >= 0 && firstRingStartIndex >= 0)
            AddEndCap(ribbonPoints[laneVisual.PointStartIndex + laneVisual.PointCount - 1].Position,
                      lastTangent,
                      previousRingStartIndex,
                      ref minimumBounds,
                      ref maximumBounds);

        Mesh dynamicMesh = visual.DynamicMesh;
        dynamicMesh.Clear(false);
        dynamicMesh.SetVertices(sharedVertices);
        dynamicMesh.SetNormals(sharedNormals);
        dynamicMesh.SetUVs(0, sharedUvs);
        dynamicMesh.SetTriangles(sharedTriangles, 0, false);
        dynamicMesh.bounds = BuildBounds(minimumBounds, maximumBounds);
    }

    /// <summary>
    /// Compresses the raw gameplay width into a readable art width that remains stable in crowded rooms.
    /// </summary>
    /// <param name="rawWidth">Raw body width inherited from gameplay lane generation.</param>
    /// <returns>Compressed art width used by the body mesh.</returns>
    public static float ResolveBodyVisualWidth(float rawWidth)
    {
        float compressedWidth = 0.12f + 0.32f * math.pow(math.max(0.01f, rawWidth), 0.62f);
        return math.clamp(compressedWidth, 0.11f, 1.45f);
    }

    /// <summary>
    /// Resolves the normalized storm-burst amount currently active on the beam.
    /// </summary>
    /// <param name="laserBeamConfig">Runtime passive config that provides the authored burst duration.</param>
    /// <param name="laserBeamState">Runtime state that stores the current burst countdown.</param>
    /// <returns>Normalized burst strength in the 0-1 range.</returns>
    public static float ResolveStormBurstNormalized(in LaserBeamPassiveConfig laserBeamConfig,
                                                    in PlayerLaserBeamState laserBeamState)
    {
        float durationSeconds = PlayerLaserBeamStateUtility.ResolveStormTickTotalDurationSeconds(in laserBeamConfig);

        if (durationSeconds <= 0f)
            return 0f;

        return math.saturate(laserBeamState.StormBurstRemainingSeconds / durationSeconds);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds one shaped cross-section ring to the shared body mesh buffers.
    /// </summary>
    /// <param name="center">Center of the ring.</param>
    /// <param name="tangent">Forward direction at the current ring.</param>
    /// <param name="normal">Vertical frame axis used by the current ring.</param>
    /// <param name="binormal">Horizontal frame axis used by the current ring.</param>
    /// <param name="radius">Radius of the current ring.</param>
    /// <param name="normalizedDistance">Normalized distance of the ring along the lane.</param>
    /// <param name="laserBeamConfig">Runtime passive config used to resolve the active body profile.</param>
    /// <param name="minimumBounds">Current minimum mesh bounds.</param>
    /// <param name="maximumBounds">Current maximum mesh bounds.</param>
    /// <returns>Start vertex index of the created ring.</returns>
    private static int AddTubeRing(float3 center,
                                   float3 tangent,
                                   float3 normal,
                                   float3 binormal,
                                   float radius,
                                   float normalizedDistance,
                                   in LaserBeamPassiveConfig laserBeamConfig,
                                   ref float3 minimumBounds,
                                   ref float3 maximumBounds)
    {
        ResolveBodyProfileShapeScales(laserBeamConfig.BodyProfile,
                                      normalizedDistance,
                                      out float normalAxisScale,
                                      out float binormalAxisScale);
        int ringStartIndex = sharedVertices.Count;

        // Duplicate the seam vertex so the storm helix can scroll cleanly across the circumference.
        for (int sideIndex = 0; sideIndex <= TubeSideCount; sideIndex++)
        {
            float angleRadians = sideIndex * angleStepRadians;
            float cosine = math.cos(angleRadians);
            float sine = math.sin(angleRadians);
            float3 radialOffset = normal * (cosine * radius * normalAxisScale) +
                                  binormal * (sine * radius * binormalAxisScale);
            float3 vertexPosition = center + radialOffset;
            float3 normalDirection = math.normalizesafe(normal * (cosine / math.max(0.001f, normalAxisScale)) +
                                                        binormal * (sine / math.max(0.001f, binormalAxisScale)),
                                                        binormal);
            sharedVertices.Add(ToVector3(vertexPosition));
            sharedNormals.Add(ToVector3(normalDirection));
            sharedUvs.Add(new Vector2(normalizedDistance, sideIndex / (float)TubeSideCount));
            ExpandBounds(vertexPosition, ref minimumBounds, ref maximumBounds);
        }

        return ringStartIndex;
    }

    /// <summary>
    /// Connects two neighboring rings with side-surface triangles.
    /// </summary>
    /// <param name="previousRingStartIndex">Start index of the previous ring.</param>
    /// <param name="currentRingStartIndex">Start index of the current ring.</param>
    private static void AddTubeBridge(int previousRingStartIndex, int currentRingStartIndex)
    {
        for (int sideIndex = 0; sideIndex < TubeSideCount; sideIndex++)
        {
            int previousA = previousRingStartIndex + sideIndex;
            int previousB = previousRingStartIndex + sideIndex + 1;
            int currentA = currentRingStartIndex + sideIndex;
            int currentB = currentRingStartIndex + sideIndex + 1;
            sharedTriangles.Add(previousA);
            sharedTriangles.Add(currentA);
            sharedTriangles.Add(previousB);
            sharedTriangles.Add(previousB);
            sharedTriangles.Add(currentA);
            sharedTriangles.Add(currentB);
        }
    }

    /// <summary>
    /// Closes the beam start with a simple cap so the tube does not appear hollow near the muzzle.
    /// </summary>
    /// <param name="center">Start-point position.</param>
    /// <param name="tangent">Forward tangent at the first point.</param>
    /// <param name="firstRingStartIndex">Start index of the first body ring.</param>
    /// <param name="minimumBounds">Current minimum mesh bounds.</param>
    /// <param name="maximumBounds">Current maximum mesh bounds.</param>
    private static void AddStartCap(float3 center,
                                    float3 tangent,
                                    int firstRingStartIndex,
                                    ref float3 minimumBounds,
                                    ref float3 maximumBounds)
    {
        int centerVertexIndex = sharedVertices.Count;
        sharedVertices.Add(ToVector3(center));
        sharedNormals.Add(ToVector3(-math.normalizesafe(tangent, new float3(0f, 0f, 1f))));
        sharedUvs.Add(new Vector2(0f, 0.5f));
        ExpandBounds(center, ref minimumBounds, ref maximumBounds);

        for (int sideIndex = 0; sideIndex < TubeSideCount; sideIndex++)
        {
            sharedTriangles.Add(centerVertexIndex);
            sharedTriangles.Add(firstRingStartIndex + sideIndex + 1);
            sharedTriangles.Add(firstRingStartIndex + sideIndex);
        }
    }

    /// <summary>
    /// Closes the final ring with a rounded cap so the beam terminates cleanly without a pointed spear tip.
    /// </summary>
    /// <param name="endPosition">Real terminal point of the lane.</param>
    /// <param name="tangent">Forward tangent at the end of the lane.</param>
    /// <param name="finalRingStartIndex">Start index of the final body ring.</param>
    /// <param name="minimumBounds">Current minimum mesh bounds.</param>
    /// <param name="maximumBounds">Current maximum mesh bounds.</param>
    private static void AddEndCap(float3 endPosition,
                                  float3 tangent,
                                  int finalRingStartIndex,
                                  ref float3 minimumBounds,
                                  ref float3 maximumBounds)
    {
        int centerVertexIndex = sharedVertices.Count;
        sharedVertices.Add(ToVector3(endPosition));
        sharedNormals.Add(ToVector3(math.normalizesafe(tangent, new float3(0f, 0f, 1f))));
        sharedUvs.Add(new Vector2(1f, 0.5f));
        ExpandBounds(endPosition, ref minimumBounds, ref maximumBounds);

        for (int sideIndex = 0; sideIndex < TubeSideCount; sideIndex++)
        {
            sharedTriangles.Add(finalRingStartIndex + sideIndex);
            sharedTriangles.Add(centerVertexIndex);
            sharedTriangles.Add(finalRingStartIndex + sideIndex + 1);
        }
    }

    /// <summary>
    /// Resolves the final body diameter at one point after breathing, profile shaping, source opening, and terminal closure are applied.
    /// </summary>
    /// <param name="point">Current sampled point.</param>
    /// <param name="normalizedDistance">Normalized distance along the lane.</param>
    /// <param name="laneLength">Total length of the current lane.</param>
    /// <param name="stormBurstNormalized">Normalized storm burst currently active on the beam.</param>
    /// <param name="visualConfig">Shared visual config used by the terminal closure.</param>
    /// <param name="laneVisual">Render-time lane metadata.</param>
    /// <param name="laserBeamConfig">Runtime passive config.</param>
    /// <param name="elapsedTimeSeconds">Global elapsed time.</param>
    /// <returns>Final full body diameter.</returns>
    private static float ResolveTubeDiameter(PlayerLaserBeamRibbonPoint point,
                                             float normalizedDistance,
                                             float laneLength,
                                             float stormBurstNormalized,
                                             in PlayerLaserBeamVisualConfig visualConfig,
                                             in PlayerLaserBeamLaneVisual laneVisual,
                                             in LaserBeamPassiveConfig laserBeamConfig,
                                             float elapsedTimeSeconds)
    {
        float baseDiameter = ResolveBodyVisualWidth(point.Width);
        float breathingWave = math.sin(elapsedTimeSeconds * 5.4f - point.Distance * 4.8f + laneVisual.LaneIndex * 0.31f);
        float breathingMultiplier = 1f + laserBeamConfig.WobbleAmplitude * 0.32f * breathingWave;
        float stormWidthMultiplier = 1f + math.saturate(stormBurstNormalized * laserBeamConfig.StormBurstIntensity) * 0.08f;
        float bodyProfileMultiplier = ResolveBodyProfileDiameterMultiplier(laserBeamConfig.BodyProfile, normalizedDistance);
        float sourceApertureMultiplier = ResolveSourceApertureDiameterMultiplier(point.Distance,
                                                                                 laneLength,
                                                                                 baseDiameter,
                                                                                 in laserBeamConfig);
        float terminalClosureMultiplier = ResolveTerminalClosureDiameterMultiplier(point.Distance,
                                                                                   laneLength,
                                                                                   baseDiameter,
                                                                                   in visualConfig,
                                                                                   in laserBeamConfig);
        float resolvedDiameter = baseDiameter *
                                 math.max(0.35f, breathingMultiplier) *
                                 stormWidthMultiplier *
                                 bodyProfileMultiplier *
                                 sourceApertureMultiplier *
                                 terminalClosureMultiplier;
        return math.max(MinimumTubeRadius * 2f, resolvedDiameter);
    }

    /// <summary>
    /// Resolves the profile-driven overall diameter multiplier used to preserve authored silhouette variety.
    /// </summary>
    /// <param name="bodyProfile">Active body profile selector.</param>
    /// <param name="normalizedDistance">Normalized distance along the lane.</param>
    /// <returns>Diameter multiplier derived from the active profile.</returns>
    private static float ResolveBodyProfileDiameterMultiplier(LaserBeamBodyProfile bodyProfile,
                                                              float normalizedDistance)
    {
        switch (bodyProfile)
        {
            case LaserBeamBodyProfile.TaperedJet:
                return math.lerp(0.92f, 1.05f, normalizedDistance);
            case LaserBeamBodyProfile.DenseRibbon:
                return 1.08f;
            default:
                return 1f;
        }
    }

    /// <summary>
    /// Resolves the vertical and horizontal ellipse scales used by one ring cross-section.
    /// </summary>
    /// <param name="bodyProfile">Active body profile selector.</param>
    /// <param name="normalizedDistance">Normalized distance along the lane.</param>
    /// <param name="normalAxisScale">Vertical ellipse scale aligned with the frame normal.</param>
    /// <param name="binormalAxisScale">Horizontal ellipse scale aligned with the frame binormal.</param>
    private static void ResolveBodyProfileShapeScales(LaserBeamBodyProfile bodyProfile,
                                                      float normalizedDistance,
                                                      out float normalAxisScale,
                                                      out float binormalAxisScale)
    {
        switch (bodyProfile)
        {
            case LaserBeamBodyProfile.TaperedJet:
                normalAxisScale = math.lerp(0.84f, 0.68f, normalizedDistance);
                binormalAxisScale = math.lerp(0.98f, 1.1f, normalizedDistance);
                return;
            case LaserBeamBodyProfile.DenseRibbon:
                normalAxisScale = 0.62f;
                binormalAxisScale = 1.18f;
                return;
            default:
                normalAxisScale = 0.9f;
                binormalAxisScale = 1f;
                return;
        }
    }

    /// <summary>
    /// Resolves the diameter multiplier applied near the source so the beam starts sealed and opens outward.
    /// </summary>
    /// <param name="distanceAlongLane">Current point distance.</param>
    /// <param name="laneLength">Total lane length.</param>
    /// <param name="baseDiameter">Current base body diameter.</param>
    /// <param name="laserBeamConfig">Runtime passive config used to scale the source offset.</param>
    /// <returns>Diameter multiplier applied near the source aperture.</returns>
    private static float ResolveSourceApertureDiameterMultiplier(float distanceAlongLane,
                                                                 float laneLength,
                                                                 float baseDiameter,
                                                                 in LaserBeamPassiveConfig laserBeamConfig)
    {
        float apertureLength = math.clamp(math.max(baseDiameter * 1.7f, laserBeamConfig.SourceOffset * 2.05f),
                                          0.08f,
                                          laneLength * 0.3f);

        if (apertureLength <= 0f || distanceAlongLane >= apertureLength)
            return 1f;

        float fastOpenLength = apertureLength * 0.2f;

        if (distanceAlongLane <= fastOpenLength)
        {
            float fastOpenInterpolation = math.saturate(distanceAlongLane / math.max(0.0001f, fastOpenLength));
            float smoothFastOpenInterpolation = fastOpenInterpolation * fastOpenInterpolation * (3f - 2f * fastOpenInterpolation);
            return math.lerp(SourceInitialApertureScale, SourceMidApertureScale, smoothFastOpenInterpolation);
        }

        float apertureInterpolation = math.saturate((distanceAlongLane - fastOpenLength) /
                                                    math.max(0.0001f, apertureLength - fastOpenLength));
        float smoothApertureInterpolation = apertureInterpolation * apertureInterpolation * (3f - 2f * apertureInterpolation);
        float collarBulge = math.sin(smoothApertureInterpolation * math.PI) *
                            SourceCollarBulgeStrength *
                            math.saturate(math.sqrt(math.max(0.01f, laserBeamConfig.SourceScaleMultiplier)));
        float collarMultiplier = 1f + collarBulge;
        return math.lerp(SourceMidApertureScale, 1f, smoothApertureInterpolation) * collarMultiplier;
    }

    /// <summary>
    /// Resolves the taper multiplier applied near the end of the lane so the body closes cleanly into the rounded terminal cap.
    /// </summary>
    /// <param name="distanceAlongLane">Current point distance.</param>
    /// <param name="laneLength">Total lane length.</param>
    /// <param name="baseDiameter">Current base body diameter.</param>
    /// <param name="visualConfig">Shared visual config used to shape the terminal closure.</param>
    /// <param name="laserBeamConfig">Runtime passive config used to scale the terminal emphasis.</param>
    /// <returns>Diameter multiplier applied near the terminal section.</returns>
    private static float ResolveTerminalClosureDiameterMultiplier(float distanceAlongLane,
                                                                  float laneLength,
                                                                  float baseDiameter,
                                                                  in PlayerLaserBeamVisualConfig visualConfig,
                                                                  in LaserBeamPassiveConfig laserBeamConfig)
    {
        float tipLength = math.clamp(baseDiameter *
                                     math.sqrt(math.max(0.01f, laserBeamConfig.TerminalCapScaleMultiplier)) *
                                     math.max(1f, visualConfig.TerminalSplashLengthMultiplier) *
                                     2.9f,
                                     0.12f,
                                     laneLength * 0.34f);
        float tipStartDistance = math.max(0f, laneLength - tipLength);

        if (distanceAlongLane <= tipStartDistance)
            return 1f;

        float tipInterpolation = math.saturate((distanceAlongLane - tipStartDistance) / math.max(0.0001f, laneLength - tipStartDistance));
        float smoothInterpolation = tipInterpolation * tipInterpolation * (3f - 2f * tipInterpolation);
        float shoulderBulge = math.sin(smoothInterpolation * math.PI) *
                              TerminalShoulderBulgeStrength *
                              math.saturate(math.sqrt(math.max(0.01f, laserBeamConfig.TerminalCapScaleMultiplier)));
        float closureFloor = math.clamp(0.74f + math.max(0f, visualConfig.TerminalSplashWidthMultiplier - 1f) * 0.05f,
                                        0.68f,
                                        0.86f);
        float closureInterpolation = smoothInterpolation * smoothInterpolation;
        return (1f + shoulderBulge) * math.lerp(1f, closureFloor, closureInterpolation);
    }

    /// <summary>
    /// Resolves the tangent used to orient one sampled point between its neighbors.
    /// </summary>
    /// <param name="ribbonPoints">Shared ribbon point list.</param>
    /// <param name="pointStartIndex">Start index of the current lane inside the shared point list.</param>
    /// <param name="pointCount">Number of points belonging to the current lane.</param>
    /// <param name="localPointIndex">Zero-based point index inside the current lane.</param>
    /// <returns>Normalized tangent.</returns>
    private static float3 ResolvePointTangent(List<PlayerLaserBeamRibbonPoint> ribbonPoints,
                                              int pointStartIndex,
                                              int pointCount,
                                              int localPointIndex)
    {
        int previousLocalIndex = math.max(0, localPointIndex - 1);
        int nextLocalIndex = math.min(pointCount - 1, localPointIndex + 1);
        float3 previousPoint = ribbonPoints[pointStartIndex + previousLocalIndex].Position;
        float3 nextPoint = ribbonPoints[pointStartIndex + nextLocalIndex].Position;
        return math.normalizesafe(nextPoint - previousPoint, new float3(0f, 0f, 1f));
    }

    /// <summary>
    /// Resolves the first frame-normal axis used to seed the ring transport along the lane.
    /// </summary>
    /// <param name="ribbonPoints">Shared ribbon point list.</param>
    /// <param name="pointStartIndex">Start index of the current lane inside the shared point list.</param>
    /// <param name="pointCount">Number of points belonging to the current lane.</param>
    /// <returns>Initial transported normal axis.</returns>
    private static float3 ResolveInitialFrameNormal(List<PlayerLaserBeamRibbonPoint> ribbonPoints,
                                                    int pointStartIndex,
                                                    int pointCount)
    {
        float3 tangent = ResolvePointTangent(ribbonPoints, pointStartIndex, pointCount, 0);
        float3 projectedUp = ProjectOntoPlane(new float3(0f, 1f, 0f), tangent);

        if (math.lengthsq(projectedUp) > 1e-6f)
            return math.normalizesafe(projectedUp, new float3(0f, 1f, 0f));

        float3 projectedRight = ProjectOntoPlane(new float3(1f, 0f, 0f), tangent);
        return math.normalizesafe(projectedRight, new float3(1f, 0f, 0f));
    }

    /// <summary>
    /// Transports the previous frame-normal axis onto the plane orthogonal to the new tangent.
    /// </summary>
    /// <param name="previousNormal">Previous transported frame-normal axis.</param>
    /// <param name="tangent">Current tangent.</param>
    /// <returns>Stabilized transported normal axis.</returns>
    private static float3 TransportFrameNormal(float3 previousNormal, float3 tangent)
    {
        float3 projectedNormal = ProjectOntoPlane(previousNormal, tangent);

        if (math.lengthsq(projectedNormal) > 1e-6f)
            return math.normalizesafe(projectedNormal, previousNormal);

        return math.normalizesafe(ProjectOntoPlane(new float3(0f, 1f, 0f), tangent), new float3(0f, 1f, 0f));
    }

    /// <summary>
    /// Resolves the orthogonal frame binormal from the transported normal axis and tangent.
    /// </summary>
    /// <param name="normal">Current transported normal axis.</param>
    /// <param name="tangent">Current tangent.</param>
    /// <returns>Stabilized frame binormal.</returns>
    private static float3 ResolveFrameBinormal(float3 normal, float3 tangent)
    {
        float3 binormal = math.cross(tangent, normal);
        return math.normalizesafe(binormal, new float3(1f, 0f, 0f));
    }

    /// <summary>
    /// Re-orthonormalizes the transported normal axis after the binormal was resolved.
    /// </summary>
    /// <param name="binormal">Current frame binormal.</param>
    /// <param name="tangent">Current tangent.</param>
    /// <returns>Stabilized frame normal axis.</returns>
    private static float3 ResolveFrameNormal(float3 binormal, float3 tangent)
    {
        float3 normal = math.cross(binormal, tangent);
        return math.normalizesafe(normal, new float3(0f, 1f, 0f));
    }

    /// <summary>
    /// Projects one vector onto the plane orthogonal to the provided normal.
    /// </summary>
    /// <param name="vector">Vector to project.</param>
    /// <param name="planeNormal">Plane normal used for the projection.</param>
    /// <returns>Projected vector.</returns>
    private static float3 ProjectOntoPlane(float3 vector, float3 planeNormal)
    {
        return vector - planeNormal * math.dot(vector, planeNormal);
    }

    /// <summary>
    /// Expands the mesh bounds with one new point.
    /// </summary>
    /// <param name="point">New point to include.</param>
    /// <param name="minimumBounds">Current minimum bounds.</param>
    /// <param name="maximumBounds">Current maximum bounds.</param>
    private static void ExpandBounds(float3 point,
                                     ref float3 minimumBounds,
                                     ref float3 maximumBounds)
    {
        minimumBounds = math.min(minimumBounds, point);
        maximumBounds = math.max(maximumBounds, point);
    }

    /// <summary>
    /// Builds a Unity bounds value from accumulated min and max points.
    /// </summary>
    /// <param name="minimumBounds">Minimum sampled bounds.</param>
    /// <param name="maximumBounds">Maximum sampled bounds.</param>
    /// <returns>Mesh bounds covering the current body.</returns>
    private static Bounds BuildBounds(float3 minimumBounds, float3 maximumBounds)
    {
        Vector3 minimumVector = ToVector3(minimumBounds);
        Vector3 maximumVector = ToVector3(maximumBounds);
        Bounds bounds = new Bounds((minimumVector + maximumVector) * 0.5f,
                                   maximumVector - minimumVector);

        if (bounds.size.sqrMagnitude <= 0f)
            bounds.Expand(0.05f);

        return bounds;
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
    #endregion

    #endregion
}
