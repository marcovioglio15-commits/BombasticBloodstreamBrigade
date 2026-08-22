using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Stores the project-wide player-build inclusion policy for the runtime enemy spawner test tool.
/// </summary>
[FilePath("ProjectSettings/NashCoreEnemySpawnerRuntimeToolBuildSettings.asset", FilePathAttribute.Location.ProjectFolder)]
internal sealed class EnemySpawnerRuntimeToolBuildSettings : ScriptableSingleton<EnemySpawnerRuntimeToolBuildSettings>
{
    #region Fields

    #region Serialized Fields
    [Tooltip("When enabled, player builds omit the runtime enemy spawner test UI, catalog, ECS override data, and executable logic.")]
    [SerializeField] private bool excludeFromPlayerBuilds = true;
    #endregion

    #endregion

    #region Properties
    public bool ExcludeFromPlayerBuilds
    {
        get
        {
            return excludeFromPlayerBuilds;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Persists the project-wide exclusion policy used by compilation and build-scene stripping.
    /// </summary>
    /// <param name="excluded">True to remove the runtime spawner test tool from player builds.</param>
    public void SetExcludedFromPlayerBuilds(bool excluded)
    {
        if (excludeFromPlayerBuilds == excluded)
            return;

        excludeFromPlayerBuilds = excluded;
        Save(true);
    }
    #endregion

    #endregion
}

/// <summary>
/// Synchronizes the runtime spawner build feature define across Unity build targets.
/// </summary>
[InitializeOnLoad]
internal static class EnemySpawnerRuntimeToolBuildFeatureUtility
{
    #region Constants
    public const string FeatureDefine = "NASHCORE_RUNTIME_SPAWNER_TOOL";
    #endregion

    #region Constructors
    /// <summary>
    /// Defers define synchronization until Unity has completed the current editor initialization pass.
    /// </summary>
    static EnemySpawnerRuntimeToolBuildFeatureUtility()
    {
        EditorApplication.delayCall += SynchronizeAllBuildTargetGroups;
    }
    #endregion

    #region Properties
    public static bool IsExcludedFromPlayerBuilds
    {
        get
        {
            return EnemySpawnerRuntimeToolBuildSettings.instance.ExcludeFromPlayerBuilds;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Changes the project-wide build policy and synchronizes the conditional compilation define immediately.
    /// </summary>
    /// <param name="excluded">True to remove the test tool from every player build target.</param>
    public static void SetExcludedFromPlayerBuilds(bool excluded)
    {
        EnemySpawnerRuntimeToolBuildSettings.instance.SetExcludedFromPlayerBuilds(excluded);
        SynchronizeAllBuildTargetGroups();
    }

    /// <summary>
    /// Synchronizes the feature define for every valid Unity build target group.
    /// </summary>
    public static void SynchronizeAllBuildTargetGroups()
    {
        Array buildTargetGroups = Enum.GetValues(typeof(BuildTargetGroup));
        HashSet<BuildTargetGroup> synchronizedGroups = new HashSet<BuildTargetGroup>();

        // Unity exposes obsolete and aliased enum values; process each supported group once.
        for (int groupIndex = 0; groupIndex < buildTargetGroups.Length; groupIndex++)
        {
            BuildTargetGroup buildTargetGroup = (BuildTargetGroup)buildTargetGroups.GetValue(groupIndex);

            if (buildTargetGroup == BuildTargetGroup.Unknown || !synchronizedGroups.Add(buildTargetGroup))
                continue;

            TrySynchronizeBuildTargetGroup(buildTargetGroup);
        }
    }

    /// <summary>
    /// Reports whether one player build target currently matches the stored feature policy.
    /// </summary>
    /// <param name="buildTargetGroup">Build target group inspected before a player build.</param>
    /// <returns>True when the conditional compilation define matches the project-wide exclusion setting.</returns>
    public static bool IsBuildTargetGroupSynchronized(BuildTargetGroup buildTargetGroup)
    {
        try
        {
            NamedBuildTarget namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            string defineSymbols = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
            bool containsFeatureDefine = ContainsFeatureDefine(defineSymbols);
            return containsFeatureDefine != IsExcludedFromPlayerBuilds;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Synchronizes one supported build target while ignoring Unity enum entries without a named target.
    /// </summary>
    /// <param name="buildTargetGroup">Build target group whose define list is updated.</param>
    private static void TrySynchronizeBuildTargetGroup(BuildTargetGroup buildTargetGroup)
    {
        try
        {
            NamedBuildTarget namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            string currentSymbols = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
            string updatedSymbols = BuildUpdatedDefineSymbols(currentSymbols, !IsExcludedFromPlayerBuilds);

            if (!string.Equals(currentSymbols, updatedSymbols, StringComparison.Ordinal))
                PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, updatedSymbols);
        }
        catch (ArgumentException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    /// <summary>
    /// Adds or removes the runtime-spawner feature define without disturbing unrelated symbols.
    /// </summary>
    /// <param name="currentSymbols">Semicolon-delimited symbols currently assigned to the build target.</param>
    /// <param name="includeFeature">True to include the runtime tool in player compilation.</param>
    /// <returns>Updated semicolon-delimited symbol list.</returns>
    private static string BuildUpdatedDefineSymbols(string currentSymbols, bool includeFeature)
    {
        string[] splitSymbols = (currentSymbols ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        List<string> retainedSymbols = new List<string>(splitSymbols.Length + 1);
        bool featureFound = false;

        // Preserve symbol order while removing duplicate or disabled feature entries.
        for (int symbolIndex = 0; symbolIndex < splitSymbols.Length; symbolIndex++)
        {
            string symbol = splitSymbols[symbolIndex].Trim();

            if (string.IsNullOrWhiteSpace(symbol))
                continue;

            if (string.Equals(symbol, FeatureDefine, StringComparison.Ordinal))
            {
                if (includeFeature && !featureFound)
                {
                    retainedSymbols.Add(FeatureDefine);
                    featureFound = true;
                }

                continue;
            }

            retainedSymbols.Add(symbol);
        }

        if (includeFeature && !featureFound)
            retainedSymbols.Add(FeatureDefine);

        return string.Join(";", retainedSymbols);
    }

    /// <summary>
    /// Checks whether a semicolon-delimited define list contains the exact runtime-spawner symbol.
    /// </summary>
    /// <param name="defineSymbols">Semicolon-delimited symbols to inspect.</param>
    /// <returns>True when the runtime-spawner feature symbol is present.</returns>
    private static bool ContainsFeatureDefine(string defineSymbols)
    {
        string[] splitSymbols = (defineSymbols ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

        for (int symbolIndex = 0; symbolIndex < splitSymbols.Length; symbolIndex++)
        {
            if (string.Equals(splitSymbols[symbolIndex].Trim(), FeatureDefine, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
