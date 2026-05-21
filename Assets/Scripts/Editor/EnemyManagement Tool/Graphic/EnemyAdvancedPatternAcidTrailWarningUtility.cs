using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Builds editor warnings for Acid Wanderer trail payloads without mutating authored values.
/// </summary>
internal static class EnemyAdvancedPatternAcidTrailWarningUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds Acid Wanderer trail warnings without mutating authored values.
    /// </summary>
    /// <param name="acidProperty">Serialized Acid payload property.</param>
    /// <param name="warningBox">Warning box refreshed in place.</param>
    public static void RefreshAcidTrailWarnings(SerializedProperty acidProperty, HelpBox warningBox)
    {
        if (warningBox == null)
            return;

        List<string> warningLines = new List<string>();

        if (acidProperty != null)
        {
            AddNonPositiveWarning(acidProperty, "trailSegmentLifetimeSeconds", "Segment Lifetime Seconds", "segments expire immediately", warningLines);
            AddNonPositiveWarning(acidProperty, "trailRadius", "Trail Radius", "segments cannot overlap the player", warningLines);
            AddNonPositiveWarning(acidProperty, "damagePerTick", "Damage Per Tick", "acid trail will not damage the player", warningLines);
            AddNonPositiveWarning(acidProperty, "applyIntervalSeconds", "Apply Interval Seconds", "runtime will use the minimum safe tick interval", warningLines);
            AddSegmentCapWarning(acidProperty, warningLines);
            AddDensityWarning(acidProperty, warningLines);
            AddMissingVfxWarning(acidProperty, warningLines);
            AddNonPositiveWarning(acidProperty, "trailSegmentVfxScaleMultiplier", "VFX Scale Multiplier", "runtime will use the minimum safe visual scale", warningLines);
        }

        if (warningLines.Count <= 0)
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        warningBox.text = string.Join("\n", warningLines);
        warningBox.style.display = DisplayStyle.Flex;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Adds one non-positive value warning for an Acid payload field.
    /// </summary>
    /// <param name="parentProperty">Serialized Acid payload parent.</param>
    /// <param name="relativePropertyName">Relative float field name.</param>
    /// <param name="displayName">Display name used in warning text.</param>
    /// <param name="impactText">Concrete runtime impact text.</param>
    /// <param name="warningLines">Mutable warning list.</param>
    private static void AddNonPositiveWarning(SerializedProperty parentProperty,
                                              string relativePropertyName,
                                              string displayName,
                                              string impactText,
                                              List<string> warningLines)
    {
        if (parentProperty == null || warningLines == null)
            return;

        SerializedProperty property = parentProperty.FindPropertyRelative(relativePropertyName);

        if (property != null && property.floatValue <= 0f)
            warningLines.Add(displayName + " is zero or negative; " + impactText + ".");
    }

    /// <summary>
    /// Adds a warning when the active segment cap disables trail retention.
    /// </summary>
    /// <param name="acidProperty">Serialized Acid payload parent.</param>
    /// <param name="warningLines">Mutable warning list.</param>
    private static void AddSegmentCapWarning(SerializedProperty acidProperty, List<string> warningLines)
    {
        SerializedProperty maxSegmentsProperty = acidProperty.FindPropertyRelative("maxActiveSegmentsPerEnemy");

        if (maxSegmentsProperty != null && maxSegmentsProperty.intValue <= 0)
            warningLines.Add("Max Active Segments is zero or negative. This enemy will not retain damaging acid segments.");
    }

    /// <summary>
    /// Adds a warning when spawn timing and distance can create overly dense trail emissions.
    /// </summary>
    /// <param name="acidProperty">Serialized Acid payload parent.</param>
    /// <param name="warningLines">Mutable warning list.</param>
    private static void AddDensityWarning(SerializedProperty acidProperty, List<string> warningLines)
    {
        SerializedProperty spawnDistanceProperty = acidProperty.FindPropertyRelative("trailSpawnDistance");
        SerializedProperty spawnIntervalProperty = acidProperty.FindPropertyRelative("trailSpawnIntervalSeconds");

        if (spawnDistanceProperty != null &&
            spawnIntervalProperty != null &&
            spawnDistanceProperty.floatValue <= 0f &&
            spawnIntervalProperty.floatValue <= 0f)
        {
            warningLines.Add("Spawn Distance and Spawn Interval are both zero or negative. Runtime will clamp timing, but this setup can create dense trails.");
        }
    }

    /// <summary>
    /// Adds a warning when damaging Acid trails have no assigned visual prefab.
    /// </summary>
    /// <param name="acidProperty">Serialized Acid payload parent.</param>
    /// <param name="warningLines">Mutable warning list.</param>
    private static void AddMissingVfxWarning(SerializedProperty acidProperty, List<string> warningLines)
    {
        SerializedProperty vfxPrefabProperty = acidProperty.FindPropertyRelative("trailSegmentVfxPrefab");

        if (vfxPrefabProperty != null && vfxPrefabProperty.objectReferenceValue == null)
            warningLines.Add("Trail Segment VFX Prefab is empty. The acid trail will damage the player but remain invisible outside debug gizmos.");
    }
    #endregion

    #endregion
}
