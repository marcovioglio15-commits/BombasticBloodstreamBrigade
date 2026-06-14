using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Resolves authored Laser Beam endpoint visuals and converts gameplay lanes into render-time ribbon data.
/// </summary>
internal static class PlayerLaserBeamPresentationRuntimeGeometryUtility
{
    #region Constants
    private const int MaximumRibbonPointsPerLane = 2048;
    private const float PresentationRibbonDensityMultiplier = 0.72f;
    private const int RibbonSmoothingIterationCount = 2;
    private const float RibbonSmoothingStrength = 0.42f;
    private const float MaximumRibbonSmoothingDeviationFactor = 0.45f;
    private const float MinimumRibbonSmoothingDeviation = 0.015f;
    private const float TurnSmoothingLockDotThreshold = 0.88f;
    #endregion

    #region Fields
    private static readonly List<float3> originalRibbonPositions = new List<float3>(256);
    private static readonly List<float3> smoothedRibbonPositions = new List<float3>(256);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the authored source particle prefab mapped to the requested cap shape.
    /// </summary>
    /// <param name="sourceVariants">Baked source variant buffer.</param>
    /// <param name="shape">Runtime source cap-shape selector.</param>
    /// <returns>Matching prefab when present, otherwise null.</returns>
    public static GameObject ResolveSourcePrefab(DynamicBuffer<PlayerLaserBeamSourceVariantElement> sourceVariants,
                                                 LaserBeamCapShape shape)
    {
        for (int variantIndex = 0; variantIndex < sourceVariants.Length; variantIndex++)
        {
            PlayerLaserBeamSourceVariantElement variant = sourceVariants[variantIndex];

            if (variant.Shape != shape)
                continue;

            return variant.Prefab.Value;
        }

        return null;
    }

    /// <summary>
    /// Resolves the authored impact particle prefab mapped to the requested cap shape.
    /// </summary>
    /// <param name="impactVariants">Baked impact variant buffer.</param>
    /// <param name="shape">Runtime impact cap-shape selector.</param>
    /// <returns>Matching prefab when present, otherwise null.</returns>
    public static GameObject ResolveImpactPrefab(DynamicBuffer<PlayerLaserBeamImpactVariantElement> impactVariants,
                                                 LaserBeamCapShape shape)
    {
        for (int variantIndex = 0; variantIndex < impactVariants.Length; variantIndex++)
        {
            PlayerLaserBeamImpactVariantElement variant = impactVariants[variantIndex];

            if (variant.Shape != shape)
                continue;

            return variant.Prefab.Value;
        }

        return null;
    }

    /// <summary>
    /// Resolves the flow and storm colors used by the current beam from the baked visual preset buffer.
    /// </summary>
    /// <param name="visualPresetId">Runtime visual preset selector.</param>
    /// <param name="visualPresetBuffer">Baked visual preset buffer.</param>
    /// <returns>Resolved colors converted back to managed Unity colors.</returns>
    public static PlayerLaserBeamResolvedPalette ResolvePalette(int visualPresetId,
                                                                DynamicBuffer<PlayerLaserBeamVisualPresetElement> visualPresetBuffer)
    {
        for (int presetIndex = 0; presetIndex < visualPresetBuffer.Length; presetIndex++)
        {
            PlayerLaserBeamVisualPresetElement visualPresetElement = visualPresetBuffer[presetIndex];

            if (visualPresetElement.VisualPresetId != visualPresetId)
                continue;

            return new PlayerLaserBeamResolvedPalette
            {
                CoreColor = ToColor(visualPresetElement.CoreColor),
                FlowColor = ToColor(visualPresetElement.FlowColor),
                StormColor = ToColor(visualPresetElement.StormColor),
                ContactColor = ToColor(visualPresetElement.ContactColor)
            };
        }

        PlayerLaserBeamVisualDefaultsUtility.ResolveDefaultPreset(visualPresetId,
                                                                  out string _,
                                                                  out Color coreColor,
                                                                  out Color flowColor,
                                                                  out Color stormColor,
                                                                  out Color contactColor);
        return new PlayerLaserBeamResolvedPalette
        {
            CoreColor = coreColor,
            FlowColor = flowColor,
            StormColor = stormColor,
            ContactColor = contactColor
        };
    }

    /// <summary>
    /// Rebuilds render-time ribbon points, lane visuals, and endpoint data from the authoritative gameplay lane buffer.
    /// </summary>
    /// <param name="laserBeamLanes">Gameplay lane buffer generated by simulation.</param>
    /// <param name="visualConfig">Shared visual config used to resample curved paths.</param>
    /// <param name="ribbonPoints">Mutable destination list for the resampled ribbon points.</param>
    /// <param name="laneVisuals">Mutable destination list for per-lane ribbon metadata.</param>
    /// <param name="laneEndpoints">Mutable destination list for per-lane source and impact anchors.</param>
    /// <returns>True when at least one lane visual and endpoint were produced.</returns>
    public static bool BuildLaneVisualData(DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                                           in PlayerLaserBeamVisualConfig visualConfig,
                                           in LaserBeamPassiveConfig laserBeamConfig,
                                           List<PlayerLaserBeamRibbonPoint> ribbonPoints,
                                           List<PlayerLaserBeamLaneVisual> laneVisuals,
                                           List<PlayerLaserBeamLaneEndpoint> laneEndpoints)
    {
        ribbonPoints.Clear();
        laneVisuals.Clear();
        laneEndpoints.Clear();

        if (laserBeamLanes.Length <= 0)
            return false;

        int segmentIndex = 0;

        while (segmentIndex < laserBeamLanes.Length)
        {
            PlayerLaserBeamLaneElement firstSegment = laserBeamLanes[segmentIndex];
            int laneSegmentStartIndex = segmentIndex;
            int laneSegmentEndIndex = ResolveLaneSegmentEndIndex(laserBeamLanes, laneSegmentStartIndex);
            int lanePointStartIndex = ribbonPoints.Count;
            float laneLength = 0f;
            float adaptiveMaximumSegmentLength = ResolveAdaptiveMaximumRibbonSegmentLength(in visualConfig,
                                                                                           laserBeamLanes,
                                                                                           laneSegmentStartIndex,
                                                                                           laneSegmentEndIndex,
                                                                                           laserBeamConfig.SourceOffset);
            PlayerLaserBeamLaneEndpoint endpoint = new PlayerLaserBeamLaneEndpoint
            {
                LaneIndex = firstSegment.LaneIndex,
                MuzzlePoint = LiftPoint(firstSegment.StartPoint, visualConfig.VerticalLift),
                VisibleStartPoint = LiftPoint(firstSegment.StartPoint, visualConfig.VerticalLift),
                StartDirection = firstSegment.Direction,
                StartWidth = firstSegment.VisualWidth,
                EndPoint = LiftPoint(firstSegment.EndPoint, visualConfig.VerticalLift),
                EndDirection = firstSegment.Direction,
                EndWidth = firstSegment.VisualWidth,
                TerminalNormal = firstSegment.TerminalNormal,
                TerminalBlockedByWall = firstSegment.TerminalBlockedByWall
            };
            float remainingSourceTrim = math.max(0f, laserBeamConfig.SourceOffset);
            bool hasVisibleStartPoint = false;
            int laneSegmentIndex = laneSegmentStartIndex;

            while (laneSegmentIndex < laneSegmentEndIndex)
            {
                PlayerLaserBeamLaneElement segment = laserBeamLanes[laneSegmentIndex];
                float segmentLength = math.max(0f, segment.Length);

                if (segmentLength <= 0f)
                {
                    endpoint.EndPoint = LiftPoint(segment.EndPoint, visualConfig.VerticalLift);
                    endpoint.EndDirection = segment.Direction;
                    endpoint.EndWidth = segment.VisualWidth;
                    endpoint.TerminalNormal = segment.TerminalNormal;
                    endpoint.TerminalBlockedByWall = segment.TerminalBlockedByWall;
                    laneSegmentIndex++;
                    continue;
                }

                float trimmedDistance = math.min(segmentLength, remainingSourceTrim);
                remainingSourceTrim -= trimmedDistance;

                if (trimmedDistance < segmentLength)
                {
                    float trimInterpolation = trimmedDistance / segmentLength;
                    PlayerLaserBeamLaneElement presentationSegment = segment;
                    presentationSegment.StartPoint = math.lerp(segment.StartPoint, segment.EndPoint, trimInterpolation);
                    presentationSegment.Length = segmentLength - trimmedDistance;

                    if (!hasVisibleStartPoint)
                    {
                        endpoint.VisibleStartPoint = LiftPoint(presentationSegment.StartPoint, visualConfig.VerticalLift);
                        endpoint.StartDirection = presentationSegment.Direction;
                        endpoint.StartWidth = presentationSegment.VisualWidth;
                        AddLaneStartPoint(presentationSegment.StartPoint,
                                          presentationSegment.VisualWidth,
                                          visualConfig.VerticalLift,
                                          ribbonPoints);
                        hasVisibleStartPoint = true;
                    }

                    bool lockSegmentTerminalPoint = laneSegmentIndex >= laneSegmentEndIndex - 1 ||
                                                    ShouldLockLaneTurn(segment.Direction,
                                                                       laserBeamLanes[laneSegmentIndex + 1].Direction);
                    AppendLaneSegmentPoints(presentationSegment,
                                            adaptiveMaximumSegmentLength,
                                            visualConfig.VerticalLift,
                                            lockSegmentTerminalPoint,
                                            ref laneLength,
                                            ribbonPoints);
                }

                endpoint.EndPoint = LiftPoint(segment.EndPoint, visualConfig.VerticalLift);
                endpoint.EndDirection = segment.Direction;
                endpoint.EndWidth = segment.VisualWidth;
                endpoint.TerminalNormal = segment.TerminalNormal;
                endpoint.TerminalBlockedByWall = segment.TerminalBlockedByWall;
                laneSegmentIndex++;
            }

            segmentIndex = laneSegmentEndIndex;

            int pointCount = ribbonPoints.Count - lanePointStartIndex;

            if (pointCount < 2 || !hasVisibleStartPoint)
            {
                ribbonPoints.RemoveRange(lanePointStartIndex, math.max(0, pointCount));
                continue;
            }

            laneLength = SmoothLaneRibbonPoints(lanePointStartIndex,
                                                pointCount,
                                                ribbonPoints,
                                                ref endpoint);

            laneVisuals.Add(new PlayerLaserBeamLaneVisual
            {
                LaneIndex = endpoint.LaneIndex,
                PointStartIndex = lanePointStartIndex,
                PointCount = pointCount,
                TotalLength = laneLength,
                StartDirection = endpoint.StartDirection,
                StartWidth = endpoint.StartWidth,
                EndDirection = endpoint.EndDirection,
                EndWidth = endpoint.EndWidth,
                TerminalNormal = endpoint.TerminalNormal,
                TerminalBlockedByWall = endpoint.TerminalBlockedByWall
            });
            laneEndpoints.Add(endpoint);
        }

        return laneVisuals.Count > 0 && laneEndpoints.Count > 0;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds the first point of a lane before any segment resampling runs.
    /// </summary>
    /// <param name="segment">First gameplay segment of the lane.</param>
    /// <param name="verticalLift">Shared beam vertical lift.</param>
    /// <param name="ribbonPoints">Mutable destination list for ribbon points.</param>
    private static void AddLaneStartPoint(float3 startPoint,
                                          float startWidth,
                                          float verticalLift,
                                          List<PlayerLaserBeamRibbonPoint> ribbonPoints)
    {
        ribbonPoints.Add(new PlayerLaserBeamRibbonPoint
        {
            Position = LiftPoint(startPoint, verticalLift),
            Distance = 0f,
            Width = math.max(0.02f, startWidth),
            SmoothingLock = 1
        });
    }

    /// <summary>
    /// Resamples one gameplay segment into evenly spaced ribbon points while preserving the authoritative path.
    /// </summary>
    /// <param name="segment">Gameplay segment produced by the simulation path.</param>
    /// <param name="maximumSegmentLength">Maximum spacing allowed between consecutive ribbon points for the current lane.</param>
    /// <param name="verticalLift">Shared beam vertical lift applied to every sampled point.</param>
    /// <param name="laneLength">Mutable accumulated lane length.</param>
    /// <param name="ribbonPoints">Mutable destination list for ribbon points.</param>
    private static void AppendLaneSegmentPoints(PlayerLaserBeamLaneElement segment,
                                                float maximumSegmentLength,
                                                float verticalLift,
                                                bool lockTerminalPoint,
                                                ref float laneLength,
                                                List<PlayerLaserBeamRibbonPoint> ribbonPoints)
    {
        float segmentLength = math.max(0.001f, segment.Length);
        int sampleCount = math.max(1, (int)math.ceil(segmentLength / maximumSegmentLength));
        float startDistance = laneLength;

        for (int sampleIndex = 1; sampleIndex <= sampleCount; sampleIndex++)
        {
            float interpolation = sampleIndex / (float)sampleCount;
            float3 point = math.lerp(segment.StartPoint, segment.EndPoint, interpolation);
            float pointDistance = startDistance + segmentLength * interpolation;
            ribbonPoints.Add(new PlayerLaserBeamRibbonPoint
            {
                Position = LiftPoint(point, verticalLift),
                Distance = pointDistance,
                Width = math.max(0.02f, segment.VisualWidth),
                SmoothingLock = lockTerminalPoint && sampleIndex == sampleCount ? (byte)1 : (byte)0
            });
        }

        laneLength = startDistance + segmentLength;
    }

    /// <summary>
    /// Applies a presentation-only smoothing pass to one lane while preserving locked pivots and the authoritative endpoints.
    /// </summary>
    /// <param name="lanePointStartIndex">First point index belonging to the lane.</param>
    /// <param name="pointCount">Number of points belonging to the lane.</param>
    /// <param name="ribbonPoints">Shared ribbon point list containing the current lane.</param>
    /// <param name="endpoint">Mutable endpoint metadata that receives the smoothed start and end directions.</param>
    /// <returns>Final lane length after smoothing and distance rebuild.</returns>
    private static float SmoothLaneRibbonPoints(int lanePointStartIndex,
                                                int pointCount,
                                                List<PlayerLaserBeamRibbonPoint> ribbonPoints,
                                                ref PlayerLaserBeamLaneEndpoint endpoint)
    {
        CacheOriginalLanePositions(lanePointStartIndex, pointCount, ribbonPoints);

        if (pointCount >= 3)
        {
            for (int iterationIndex = 0; iterationIndex < RibbonSmoothingIterationCount; iterationIndex++)
            {
                ApplyLaneRibbonSmoothingIteration(lanePointStartIndex,
                                                  pointCount,
                                                  ribbonPoints,
                                                  originalRibbonPositions,
                                                  smoothedRibbonPositions);
                CommitSmoothedLanePositions(lanePointStartIndex,
                                            pointCount,
                                            ribbonPoints,
                                            smoothedRibbonPositions);
            }
        }

        endpoint.VisibleStartPoint = ribbonPoints[lanePointStartIndex].Position;
        endpoint.EndPoint = ribbonPoints[lanePointStartIndex + pointCount - 1].Position;
        endpoint.StartDirection = ResolveLaneDirection(lanePointStartIndex,
                                                      pointCount,
                                                      ribbonPoints,
                                                      true,
                                                      endpoint.StartDirection);
        endpoint.EndDirection = ResolveLaneDirection(lanePointStartIndex,
                                                    pointCount,
                                                    ribbonPoints,
                                                    false,
                                                    endpoint.EndDirection);
        return RecalculateLaneRibbonDistances(lanePointStartIndex, pointCount, ribbonPoints);
    }

    /// <summary>
    /// Applies the beam vertical lift to a world-space point.
    /// </summary>
    /// <param name="point">Unlifted world-space point.</param>
    /// <param name="verticalLift">Shared vertical lift amount.</param>
    /// <returns>Lifted world-space point.</returns>
    private static float3 LiftPoint(float3 point, float verticalLift)
    {
        return new float3(point.x, point.y + verticalLift, point.z);
    }

    /// <summary>
    /// Resolves the exclusive segment end index of the current lane.
    /// </summary>
    /// <param name="laserBeamLanes">Gameplay lane buffer generated by simulation.</param>
    /// <param name="laneSegmentStartIndex">First segment index belonging to the lane.</param>
    /// <returns>Exclusive segment end index of the lane.</returns>
    private static int ResolveLaneSegmentEndIndex(DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                                                  int laneSegmentStartIndex)
    {
        int laneSegmentEndIndex = laneSegmentStartIndex + 1;
        int laneIndex = laserBeamLanes[laneSegmentStartIndex].LaneIndex;

        while (laneSegmentEndIndex < laserBeamLanes.Length &&
               laserBeamLanes[laneSegmentEndIndex].LaneIndex == laneIndex)
        {
            laneSegmentEndIndex++;
        }

        return laneSegmentEndIndex;
    }

    /// <summary>
    /// Resolves the adaptive segment length used to keep very long beams inside a stable ribbon-point budget.
    /// </summary>
    /// <param name="visualConfig">Shared visual config used to determine the authored baseline density.</param>
    /// <param name="laserBeamLanes">Gameplay lane buffer generated by simulation.</param>
    /// <param name="laneSegmentStartIndex">First segment index belonging to the lane.</param>
    /// <param name="laneSegmentEndIndex">Exclusive segment end index belonging to the lane.</param>
    /// <param name="sourceOffset">Trim applied at the start of the visible beam.</param>
    /// <returns>Adaptive maximum spacing between consecutive ribbon points for the current lane.</returns>
    private static float ResolveAdaptiveMaximumRibbonSegmentLength(in PlayerLaserBeamVisualConfig visualConfig,
                                                                   DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                                                                   int laneSegmentStartIndex,
                                                                   int laneSegmentEndIndex,
                                                                   float sourceOffset)
    {
        float authoredMaximumSegmentLength = math.max(visualConfig.MinimumSegmentLength,
                                                      visualConfig.MaximumRibbonSegmentLength * PresentationRibbonDensityMultiplier);
        float visibleLaneLength = ResolveVisibleLaneLength(laserBeamLanes,
                                                           laneSegmentStartIndex,
                                                           laneSegmentEndIndex,
                                                           sourceOffset);
        float budgetDrivenSegmentLength = visibleLaneLength / math.max(1, MaximumRibbonPointsPerLane - 1);
        return math.max(authoredMaximumSegmentLength, budgetDrivenSegmentLength);
    }

    /// <summary>
    /// Copies the current lane positions into reusable scratch lists used by the smoothing pass.
    /// </summary>
    /// <param name="lanePointStartIndex">First point index belonging to the lane.</param>
    /// <param name="pointCount">Number of points belonging to the lane.</param>
    /// <param name="ribbonPoints">Shared ribbon point list containing the current lane.</param>
    private static void CacheOriginalLanePositions(int lanePointStartIndex,
                                                   int pointCount,
                                                   List<PlayerLaserBeamRibbonPoint> ribbonPoints)
    {
        originalRibbonPositions.Clear();
        smoothedRibbonPositions.Clear();

        for (int localPointIndex = 0; localPointIndex < pointCount; localPointIndex++)
        {
            float3 pointPosition = ribbonPoints[lanePointStartIndex + localPointIndex].Position;
            originalRibbonPositions.Add(pointPosition);
            smoothedRibbonPositions.Add(pointPosition);
        }
    }

    /// <summary>
    /// Runs one bounded smoothing iteration over the current lane positions.
    /// </summary>
    /// <param name="lanePointStartIndex">First point index belonging to the lane.</param>
    /// <param name="pointCount">Number of points belonging to the lane.</param>
    /// <param name="ribbonPoints">Shared ribbon point list containing the current lane.</param>
    /// <param name="originalPositions">Original unsmoothed lane positions used to clamp drift.</param>
    /// <param name="smoothedPositions">Mutable destination list for smoothed positions.</param>
    private static void ApplyLaneRibbonSmoothingIteration(int lanePointStartIndex,
                                                          int pointCount,
                                                          List<PlayerLaserBeamRibbonPoint> ribbonPoints,
                                                          List<float3> originalPositions,
                                                          List<float3> smoothedPositions)
    {
        for (int localPointIndex = 0; localPointIndex < pointCount; localPointIndex++)
        {
            int absolutePointIndex = lanePointStartIndex + localPointIndex;
            PlayerLaserBeamRibbonPoint currentPoint = ribbonPoints[absolutePointIndex];

            if (currentPoint.SmoothingLock != 0 ||
                localPointIndex <= 0 ||
                localPointIndex >= pointCount - 1)
            {
                smoothedPositions[localPointIndex] = currentPoint.Position;
                continue;
            }

            float3 previousPosition = ribbonPoints[absolutePointIndex - 1].Position;
            float3 currentPosition = currentPoint.Position;
            float3 nextPosition = ribbonPoints[absolutePointIndex + 1].Position;
            float3 neighborAverage = (previousPosition + nextPosition) * 0.5f;
            float smoothingStrength = localPointIndex == 1 || localPointIndex == pointCount - 2
                ? RibbonSmoothingStrength * 0.68f
                : RibbonSmoothingStrength;
            float3 smoothedPosition = math.lerp(currentPosition, neighborAverage, smoothingStrength);
            float3 originalPosition = originalPositions[localPointIndex];
            float maximumDeviation = ResolveMaximumRibbonSmoothingDeviation(previousPosition,
                                                                           currentPosition,
                                                                           nextPosition,
                                                                           currentPoint.Width);
            float3 deviation = smoothedPosition - originalPosition;
            float deviationLength = math.length(deviation);

            if (deviationLength > maximumDeviation && deviationLength > 1e-5f)
                smoothedPosition = originalPosition + deviation * (maximumDeviation / deviationLength);

            smoothedPositions[localPointIndex] = smoothedPosition;
        }
    }

    /// <summary>
    /// Writes the smoothed positions back into the shared lane point list.
    /// </summary>
    /// <param name="lanePointStartIndex">First point index belonging to the lane.</param>
    /// <param name="pointCount">Number of points belonging to the lane.</param>
    /// <param name="ribbonPoints">Shared ribbon point list containing the current lane.</param>
    /// <param name="smoothedPositions">Source list containing the latest smoothed positions.</param>
    private static void CommitSmoothedLanePositions(int lanePointStartIndex,
                                                    int pointCount,
                                                    List<PlayerLaserBeamRibbonPoint> ribbonPoints,
                                                    List<float3> smoothedPositions)
    {
        for (int localPointIndex = 0; localPointIndex < pointCount; localPointIndex++)
        {
            int absolutePointIndex = lanePointStartIndex + localPointIndex;
            PlayerLaserBeamRibbonPoint point = ribbonPoints[absolutePointIndex];
            point.Position = smoothedPositions[localPointIndex];
            ribbonPoints[absolutePointIndex] = point;
        }
    }

    /// <summary>
    /// Rebuilds the cumulative lane distances after presentation-only smoothing changed the point positions.
    /// </summary>
    /// <param name="lanePointStartIndex">First point index belonging to the lane.</param>
    /// <param name="pointCount">Number of points belonging to the lane.</param>
    /// <param name="ribbonPoints">Shared ribbon point list containing the current lane.</param>
    /// <returns>Final lane length after distance rebuild.</returns>
    private static float RecalculateLaneRibbonDistances(int lanePointStartIndex,
                                                        int pointCount,
                                                        List<PlayerLaserBeamRibbonPoint> ribbonPoints)
    {
        float laneLength = 0f;

        for (int localPointIndex = 0; localPointIndex < pointCount; localPointIndex++)
        {
            int absolutePointIndex = lanePointStartIndex + localPointIndex;
            PlayerLaserBeamRibbonPoint point = ribbonPoints[absolutePointIndex];

            if (localPointIndex <= 0)
            {
                point.Distance = 0f;
                ribbonPoints[absolutePointIndex] = point;
                continue;
            }

            float3 previousPosition = ribbonPoints[absolutePointIndex - 1].Position;
            laneLength += math.distance(previousPosition, point.Position);
            point.Distance = laneLength;
            ribbonPoints[absolutePointIndex] = point;
        }

        return laneLength;
    }

    /// <summary>
    /// Resolves the smoothed lane direction at either the start or the end of the point range.
    /// </summary>
    /// <param name="lanePointStartIndex">First point index belonging to the lane.</param>
    /// <param name="pointCount">Number of points belonging to the lane.</param>
    /// <param name="ribbonPoints">Shared ribbon point list containing the current lane.</param>
    /// <param name="resolveStartDirection">True to resolve the start direction, otherwise the end direction.</param>
    /// <param name="fallbackDirection">Direction used when the lane is too short to derive a stable tangent.</param>
    /// <returns>Smoothed lane direction.</returns>
    private static float3 ResolveLaneDirection(int lanePointStartIndex,
                                               int pointCount,
                                               List<PlayerLaserBeamRibbonPoint> ribbonPoints,
                                               bool resolveStartDirection,
                                               float3 fallbackDirection)
    {
        if (pointCount < 2)
            return fallbackDirection;

        int fromIndex = resolveStartDirection
            ? lanePointStartIndex
            : lanePointStartIndex + pointCount - 2;
        int toIndex = resolveStartDirection
            ? lanePointStartIndex + 1
            : lanePointStartIndex + pointCount - 1;
        float3 fromPosition = ribbonPoints[fromIndex].Position;
        float3 toPosition = ribbonPoints[toIndex].Position;
        return math.normalizesafe(toPosition - fromPosition, fallbackDirection);
    }

    /// <summary>
    /// Resolves the maximum presentation-only drift allowed for one smoothed point.
    /// </summary>
    /// <param name="previousPosition">Previous lane point position.</param>
    /// <param name="currentPosition">Current lane point position.</param>
    /// <param name="nextPosition">Next lane point position.</param>
    /// <param name="pointWidth">Raw gameplay width at the current point.</param>
    /// <returns>Maximum allowed deviation from the original point position.</returns>
    private static float ResolveMaximumRibbonSmoothingDeviation(float3 previousPosition,
                                                                float3 currentPosition,
                                                                float3 nextPosition,
                                                                float pointWidth)
    {
        float previousSpan = math.distance(previousPosition, currentPosition);
        float nextSpan = math.distance(currentPosition, nextPosition);
        float localSpan = math.min(previousSpan, nextSpan);
        float widthBudget = 0.03f + math.max(0.02f, pointWidth) * 0.35f;
        float spanBudget = localSpan * MaximumRibbonSmoothingDeviationFactor;
        return math.max(MinimumRibbonSmoothingDeviation, math.min(widthBudget, spanBudget));
    }

    /// <summary>
    /// Resolves the visible lane length after the authored source trim has been removed.
    /// </summary>
    /// <param name="laserBeamLanes">Gameplay lane buffer generated by simulation.</param>
    /// <param name="laneSegmentStartIndex">First segment index belonging to the lane.</param>
    /// <param name="laneSegmentEndIndex">Exclusive segment end index belonging to the lane.</param>
    /// <param name="sourceOffset">Trim applied at the start of the visible beam.</param>
    /// <returns>Visible lane length after source trimming.</returns>
    private static float ResolveVisibleLaneLength(DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                                                  int laneSegmentStartIndex,
                                                  int laneSegmentEndIndex,
                                                  float sourceOffset)
    {
        float remainingSourceTrim = math.max(0f, sourceOffset);
        float visibleLaneLength = 0f;

        for (int laneSegmentIndex = laneSegmentStartIndex; laneSegmentIndex < laneSegmentEndIndex; laneSegmentIndex++)
        {
            float segmentLength = math.max(0f, laserBeamLanes[laneSegmentIndex].Length);
            float trimmedDistance = math.min(segmentLength, remainingSourceTrim);
            remainingSourceTrim -= trimmedDistance;
            visibleLaneLength += math.max(0f, segmentLength - trimmedDistance);
        }

        return math.max(0f, visibleLaneLength);
    }

    /// <summary>
    /// Resolves whether the segment terminal point should remain locked to preserve a deliberate sharp turn.
    /// </summary>
    /// <param name="currentDirection">Current segment direction.</param>
    /// <param name="nextDirection">Following segment direction.</param>
    /// <returns>True when the turn is sharp enough to preserve exactly.</returns>
    private static bool ShouldLockLaneTurn(float3 currentDirection,
                                           float3 nextDirection)
    {
        float3 normalizedCurrentDirection = math.normalizesafe(currentDirection, new float3(0f, 0f, 1f));
        float3 normalizedNextDirection = math.normalizesafe(nextDirection, normalizedCurrentDirection);
        float turnDot = math.dot(normalizedCurrentDirection, normalizedNextDirection);
        return turnDot <= TurnSmoothingLockDotThreshold;
    }

    /// <summary>
    /// Converts one baked linear float4 color into a managed Unity color.
    /// </summary>
    /// <param name="value">Baked float4 color.</param>
    /// <returns>Managed Unity color.</returns>
    private static Color ToColor(float4 value)
    {
        return new Color(value.x, value.y, value.z, value.w);
    }
    #endregion

    #endregion
}
