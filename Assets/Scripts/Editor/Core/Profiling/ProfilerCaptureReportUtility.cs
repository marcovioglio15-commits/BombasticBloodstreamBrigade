using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Exports profiler capture reports from .data files stored in the ProfilerCaptures folder.
/// </summary>
public static class ProfilerCaptureReportUtility
{
    #region Constants
    private const string CapturesFolderName = "ProfilerCaptures";
    private const string ReportsFolderName = "ProfilerReports";
    private const int TopWorstFrameMarkerLimit = 12;
    private const int TopWorstFrameEntitiesSystemMarkerLimit = 12;
    private const int TopCaptureEntitiesSystemMarkerLimit = 20;
    #endregion

    #region Methods

    #region Types
    private readonly struct MarkerTimeSummary
    {
        #region Fields
        public readonly string MarkerName;
        public readonly float TotalTimeMs;
        #endregion

        #region Constructors
        public MarkerTimeSummary(string markerNameValue, float totalTimeMsValue)
        {
            MarkerName = markerNameValue;
            TotalTimeMs = totalTimeMsValue;
        }
        #endregion
    }
    #endregion

    #region Menu
    //[MenuItem("Tools/Profiling/Export Capture Metadata Report")]
    public static void ExportCaptureMetadataReport()
    {
        string projectRootPath = Directory.GetCurrentDirectory();
        string capturesFolderPath = Path.Combine(projectRootPath, CapturesFolderName);

        if (Directory.Exists(capturesFolderPath) == false)
        {
            Debug.LogError(string.Format(CultureInfo.InvariantCulture,
                                         "[ProfilerCaptureReportUtility] Missing captures folder at '{0}'.",
                                         capturesFolderPath));
            return;
        }

        string[] captureFilePaths = Directory.GetFiles(capturesFolderPath, "*.data", SearchOption.TopDirectoryOnly);

        if (captureFilePaths == null || captureFilePaths.Length == 0)
        {
            Debug.LogWarning("[ProfilerCaptureReportUtility] No .data capture found.");
            return;
        }

        Array.Sort(captureFilePaths, StringComparer.OrdinalIgnoreCase);

        string reportsFolderPath = Path.Combine(projectRootPath, ReportsFolderName);
        Directory.CreateDirectory(reportsFolderPath);

        string reportFilePath = Path.Combine(reportsFolderPath, "ProfilerCaptureMetadataReport.txt");

        using (StreamWriter writer = new StreamWriter(reportFilePath, false))
        {
            writer.WriteLine("Profiler Capture Metadata Report");
            writer.WriteLine("==============================");
            writer.WriteLine();

            for (int captureIndex = 0; captureIndex < captureFilePaths.Length; captureIndex++)
            {
                string captureFilePath = captureFilePaths[captureIndex];
                WriteSingleCaptureMetadata(writer, captureFilePath);
            }
        }

        AssetDatabase.Refresh();
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
                                "[ProfilerCaptureReportUtility] Report generated at '{0}'.",
                                reportFilePath));
    }
    #endregion

    #region Helpers
    private static void WriteSingleCaptureMetadata(StreamWriter writer, string captureFilePath)
    {
        writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "Capture: {0}", captureFilePath));

        bool loadSucceeded = ProfilerDriver.LoadProfile(captureFilePath, false);
        writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "LoadSucceeded: {0}", loadSucceeded));

        int firstFrameIndex = ProfilerDriver.firstFrameIndex;
        int lastFrameIndex = ProfilerDriver.lastFrameIndex;
        writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "FirstFrameIndex: {0}", firstFrameIndex));
        writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "LastFrameIndex: {0}", lastFrameIndex));

        if (loadSucceeded)
        {
            WriteFrameTimingSummary(writer, firstFrameIndex, lastFrameIndex);
        }

        writer.WriteLine();
    }

    private static void WriteFrameTimingSummary(StreamWriter writer, int firstFrameIndex, int lastFrameIndex)
    {
        int frameCount = (lastFrameIndex - firstFrameIndex) + 1;

        if (frameCount <= 0)
        {
            writer.WriteLine("FrameCount: 0");
            return;
        }

        float totalFrameTimeMs = 0f;
        float maxFrameTimeMs = 0f;
        int worstFrameIndex = firstFrameIndex;
        float totalPlayerLoopTimeMs = 0f;
        float maxPlayerLoopTimeMs = 0f;
        int worstPlayerLoopFrameIndex = firstFrameIndex;
        List<float> playerLoopFrameTimes = new List<float>(frameCount);

        for (int frameIndex = firstFrameIndex; frameIndex <= lastFrameIndex; frameIndex++)
        {
            using (RawFrameDataView frameDataView = ProfilerDriver.GetRawFrameDataView(frameIndex, 0))
            {
                if (frameDataView.valid == false)
                {
                    continue;
                }

                float currentFrameTimeMs = frameDataView.frameTimeMs;
                totalFrameTimeMs += currentFrameTimeMs;
                float playerLoopTimeMs = GetFrameMarkerTotalTimeMs(frameDataView, "PlayerLoop");
                totalPlayerLoopTimeMs += playerLoopTimeMs;
                playerLoopFrameTimes.Add(playerLoopTimeMs);

                if (currentFrameTimeMs <= maxFrameTimeMs)
                {
                    if (playerLoopTimeMs > maxPlayerLoopTimeMs)
                    {
                        maxPlayerLoopTimeMs = playerLoopTimeMs;
                        worstPlayerLoopFrameIndex = frameIndex;
                    }

                    continue;
                }

                maxFrameTimeMs = currentFrameTimeMs;
                worstFrameIndex = frameIndex;

                if (playerLoopTimeMs > maxPlayerLoopTimeMs)
                {
                    maxPlayerLoopTimeMs = playerLoopTimeMs;
                    worstPlayerLoopFrameIndex = frameIndex;
                }
            }
        }

        float averageFrameTimeMs = totalFrameTimeMs / frameCount;
        float averageFps = averageFrameTimeMs > 0.0001f ? 1000f / averageFrameTimeMs : 0f;
        float worstFrameFps = maxFrameTimeMs > 0.0001f ? 1000f / maxFrameTimeMs : 0f;
        float averagePlayerLoopTimeMs = totalPlayerLoopTimeMs / frameCount;
        float playerLoopP95Ms = ComputePercentile(playerLoopFrameTimes, 95f);
        float averagePlayerLoopFps = averagePlayerLoopTimeMs > 0.0001f ? 1000f / averagePlayerLoopTimeMs : 0f;
        float worstPlayerLoopFps = maxPlayerLoopTimeMs > 0.0001f ? 1000f / maxPlayerLoopTimeMs : 0f;

        writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "FrameCount: {0}", frameCount));
        writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "AverageFrameMs_MainThread0: {0:0.###}", averageFrameTimeMs));
        writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "AverageFps_MainThread0: {0:0.###}", averageFps));
        writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "WorstFrameIndex_MainThread0: {0}", worstFrameIndex));
        writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "WorstFrameMs_MainThread0: {0:0.###}", maxFrameTimeMs));
        writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "WorstFrameFps_MainThread0: {0:0.###}", worstFrameFps));
        writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "AveragePlayerLoopMs_MainThread0: {0:0.###}", averagePlayerLoopTimeMs));
        writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "AveragePlayerLoopFps_MainThread0: {0:0.###}", averagePlayerLoopFps));
        writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "PlayerLoopP95Ms_MainThread0: {0:0.###}", playerLoopP95Ms));
        writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "WorstPlayerLoopFrameIndex_MainThread0: {0}", worstPlayerLoopFrameIndex));
        writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "WorstPlayerLoopMs_MainThread0: {0:0.###}", maxPlayerLoopTimeMs));
        writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "WorstPlayerLoopFps_MainThread0: {0:0.###}", worstPlayerLoopFps));
        writer.WriteLine("TopMarkersInWorstPlayerLoopFrame_MainThread0:");

        List<MarkerTimeSummary> markerSummaries = BuildFrameMarkerSummaries(worstPlayerLoopFrameIndex, null);
        WriteMarkerSummaries(writer, markerSummaries, TopWorstFrameMarkerLimit);

        writer.WriteLine("TopEntitiesSystemsInWorstPlayerLoopFrame_MainThread0:");
        List<MarkerTimeSummary> worstFrameEntitiesSystemSummaries = BuildFrameMarkerSummaries(worstPlayerLoopFrameIndex, IsEntitiesSystemMarker);
        WriteMarkerSummaries(writer, worstFrameEntitiesSystemSummaries, TopWorstFrameEntitiesSystemMarkerLimit);

        writer.WriteLine("TopEntitiesSystemsAcrossCapture_MainThread0:");
        List<MarkerTimeSummary> captureEntitiesSystemSummaries = BuildCaptureMarkerSummaries(firstFrameIndex,
                                                                                              lastFrameIndex,
                                                                                              IsEntitiesSystemMarker);
        WriteMarkerSummaries(writer, captureEntitiesSystemSummaries, TopCaptureEntitiesSystemMarkerLimit);
    }

    private static List<MarkerTimeSummary> BuildFrameMarkerSummaries(int frameIndex, Predicate<string> markerFilter)
    {
        Dictionary<string, float> timeByMarkerName = new Dictionary<string, float>(StringComparer.Ordinal);

        using (RawFrameDataView frameDataView = ProfilerDriver.GetRawFrameDataView(frameIndex, 0))
        {
            if (frameDataView.valid == false)
            {
                return new List<MarkerTimeSummary>();
            }

            int sampleCount = frameDataView.sampleCount;

            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                string sampleName = frameDataView.GetSampleName(sampleIndex);

                if (string.IsNullOrWhiteSpace(sampleName))
                {
                    continue;
                }

                if (markerFilter != null && markerFilter(sampleName) == false)
                {
                    continue;
                }

                float sampleTimeMs = frameDataView.GetSampleTimeMs(sampleIndex);
                AccumulateMarkerTime(timeByMarkerName, sampleName, sampleTimeMs);
            }
        }

        return BuildSortedMarkerSummaries(timeByMarkerName);
    }

    private static List<MarkerTimeSummary> BuildCaptureMarkerSummaries(int firstFrameIndex,
                                                                       int lastFrameIndex,
                                                                       Predicate<string> markerFilter)
    {
        Dictionary<string, float> timeByMarkerName = new Dictionary<string, float>(StringComparer.Ordinal);

        for (int frameIndex = firstFrameIndex; frameIndex <= lastFrameIndex; frameIndex++)
        {
            using (RawFrameDataView frameDataView = ProfilerDriver.GetRawFrameDataView(frameIndex, 0))
            {
                if (frameDataView.valid == false)
                {
                    continue;
                }

                int sampleCount = frameDataView.sampleCount;

                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    string sampleName = frameDataView.GetSampleName(sampleIndex);

                    if (string.IsNullOrWhiteSpace(sampleName))
                    {
                        continue;
                    }

                    if (markerFilter != null && markerFilter(sampleName) == false)
                    {
                        continue;
                    }

                    float sampleTimeMs = frameDataView.GetSampleTimeMs(sampleIndex);
                    AccumulateMarkerTime(timeByMarkerName, sampleName, sampleTimeMs);
                }
            }
        }

        return BuildSortedMarkerSummaries(timeByMarkerName);
    }

    private static List<MarkerTimeSummary> BuildSortedMarkerSummaries(Dictionary<string, float> timeByMarkerName)
    {
        List<MarkerTimeSummary> markerSummaries = new List<MarkerTimeSummary>(timeByMarkerName.Count);

        foreach (KeyValuePair<string, float> markerEntry in timeByMarkerName)
        {
            markerSummaries.Add(new MarkerTimeSummary(markerEntry.Key, markerEntry.Value));
        }

        markerSummaries.Sort((leftSummary, rightSummary) => rightSummary.TotalTimeMs.CompareTo(leftSummary.TotalTimeMs));
        return markerSummaries;
    }

    private static void AccumulateMarkerTime(Dictionary<string, float> timeByMarkerName, string markerName, float sampleTimeMs)
    {
        float accumulatedTimeMs;

        if (timeByMarkerName.TryGetValue(markerName, out accumulatedTimeMs))
        {
            timeByMarkerName[markerName] = accumulatedTimeMs + sampleTimeMs;
            return;
        }

        timeByMarkerName.Add(markerName, sampleTimeMs);
    }

    private static bool IsEntitiesSystemMarker(string markerName)
    {
        if (string.IsNullOrWhiteSpace(markerName))
        {
            return false;
        }

        if (markerName.IndexOf("Unity.Entities.", StringComparison.Ordinal) >= 0)
        {
            return true;
        }

        if (markerName.EndsWith("SystemGroup", StringComparison.Ordinal))
        {
            return true;
        }

        if (markerName.EndsWith("System", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static void WriteMarkerSummaries(StreamWriter writer, List<MarkerTimeSummary> markerSummaries, int markerLimit)
    {
        if (markerSummaries == null || markerSummaries.Count <= 0 || markerLimit <= 0)
        {
            writer.WriteLine("  none");
            return;
        }

        int resolvedMarkerLimit = markerSummaries.Count < markerLimit ? markerSummaries.Count : markerLimit;

        for (int markerIndex = 0; markerIndex < resolvedMarkerLimit; markerIndex++)
        {
            MarkerTimeSummary markerSummary = markerSummaries[markerIndex];
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                           "  {0}. {1} = {2:0.###}ms",
                                           markerIndex + 1,
                                           markerSummary.MarkerName,
                                           markerSummary.TotalTimeMs));
        }
    }

    private static float GetFrameMarkerTotalTimeMs(RawFrameDataView frameDataView, string markerName)
    {
        if (frameDataView.valid == false || string.IsNullOrWhiteSpace(markerName))
        {
            return 0f;
        }

        float totalTimeMs = 0f;
        int sampleCount = frameDataView.sampleCount;

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            string sampleName = frameDataView.GetSampleName(sampleIndex);

            if (string.Equals(sampleName, markerName, StringComparison.Ordinal) == false)
            {
                continue;
            }

            totalTimeMs += frameDataView.GetSampleTimeMs(sampleIndex);
        }

        return totalTimeMs;
    }

    private static float ComputePercentile(List<float> values, float percentile)
    {
        if (values == null || values.Count == 0)
        {
            return 0f;
        }

        if (percentile <= 0f)
        {
            return 0f;
        }

        if (percentile >= 100f)
        {
            percentile = 100f;
        }

        float[] sortedValues = values.ToArray();
        Array.Sort(sortedValues);
        float scaledIndex = (percentile / 100f) * (sortedValues.Length - 1);
        int lowerIndex = Mathf.FloorToInt(scaledIndex);
        int upperIndex = Mathf.CeilToInt(scaledIndex);

        if (lowerIndex == upperIndex)
        {
            return sortedValues[lowerIndex];
        }

        float interpolation = scaledIndex - lowerIndex;
        return Mathf.Lerp(sortedValues[lowerIndex], sortedValues[upperIndex], interpolation);
    }
    #endregion

    #endregion
}
