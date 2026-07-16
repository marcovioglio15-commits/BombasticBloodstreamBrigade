using System;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Reports boss mixed-pattern engagement overrides that cannot affect any enabled runtime warning candidate.
/// </summary>
internal static class EnemyBossPatternPresetsPanelEngagementWarningUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds usage warnings for every enabled mixed-pattern behaviour engagement override.
    /// </summary>
    /// <param name="interactionsProperty">Serialized mixed-pattern interactions to inspect.</param>
    /// <param name="sourcePreset">Source module catalog used to resolve each candidate's timing support.</param>
    /// <param name="parent">Visual container receiving consolidated warning boxes.</param>
    public static void AddWarnings(SerializedProperty interactionsProperty,
                                   EnemyModulesAndPatternsPreset sourcePreset,
                                   VisualElement parent)
    {
        if (interactionsProperty == null || sourcePreset == null || parent == null)
            return;

        // Inspect every enabled mixed pattern for ineffective protection flags before override-specific usage checks.
        for (int interactionIndex = 0; interactionIndex < interactionsProperty.arraySize; interactionIndex++)
        {
            SerializedProperty interactionProperty = interactionsProperty.GetArrayElementAtIndex(interactionIndex);

            if (!ReadBoolean(interactionProperty, "enabled"))
                continue;

            AddProtectionWarnings(interactionProperty,
                                  interactionIndex,
                                  sourcePreset,
                                  parent);

            if (!ReadBoolean(interactionProperty, "useEngagementFeedbackOverride"))
                continue;

            int warningCandidateCount = 0;
            int inheritedWarningCandidateCount = 0;
            AccumulateCandidates(interactionProperty.FindPropertyRelative("coreMovementExtraction"),
                                 sourcePreset,
                                 EnemyPatternModuleCatalogSection.CoreMovement,
                                 false,
                                 ref warningCandidateCount,
                                 ref inheritedWarningCandidateCount);
            AccumulateCandidates(interactionProperty.FindPropertyRelative("shortRangeExtraction"),
                                 sourcePreset,
                                 EnemyPatternModuleCatalogSection.ShortRangeInteraction,
                                 true,
                                 ref warningCandidateCount,
                                 ref inheritedWarningCandidateCount);
            AccumulateCandidates(interactionProperty.FindPropertyRelative("weaponExtraction"),
                                 sourcePreset,
                                 EnemyPatternModuleCatalogSection.WeaponInteraction,
                                 true,
                                 ref warningCandidateCount,
                                 ref inheritedWarningCandidateCount);

            if (warningCandidateCount == 0)
            {
                parent.Add(new HelpBox("Mixed Pattern Candidate " + (interactionIndex + 1) + " enables a mixed-pattern warning override, but no enabled module candidate has both a behaviour engagement trigger and a supported runtime timing hook.", HelpBoxMessageType.Warning));
                continue;
            }

            if (inheritedWarningCandidateCount == 0)
                parent.Add(new HelpBox("Mixed Pattern Candidate " + (interactionIndex + 1) + " enables a mixed-pattern warning override, but every supported warning module candidate replaces it with a candidate-specific override.", HelpBoxMessageType.Info));
        }
    }
    #endregion

    #region Candidate Inspection
    /// <summary>
    /// Reports interruption-protection flags that cannot produce a supported runtime warning config.
    /// </summary>
    /// <param name="interactionProperty">Enabled mixed-pattern definition to inspect.</param>
    /// <param name="interactionIndex">Serialized mixed-pattern index used in warning text.</param>
    /// <param name="sourcePreset">Source module catalog used to resolve timing support.</param>
    /// <param name="parent">Visual container receiving warning boxes.</param>
    private static void AddProtectionWarnings(SerializedProperty interactionProperty,
                                              int interactionIndex,
                                              EnemyModulesAndPatternsPreset sourcePreset,
                                              VisualElement parent)
    {
        AddProtectionWarnings(interactionProperty.FindPropertyRelative("coreMovementExtraction"),
                              interactionIndex,
                              sourcePreset,
                              EnemyPatternModuleCatalogSection.CoreMovement,
                              false,
                              parent);
        AddProtectionWarnings(interactionProperty.FindPropertyRelative("shortRangeExtraction"),
                              interactionIndex,
                              sourcePreset,
                              EnemyPatternModuleCatalogSection.ShortRangeInteraction,
                              true,
                              parent);
        AddProtectionWarnings(interactionProperty.FindPropertyRelative("weaponExtraction"),
                              interactionIndex,
                              sourcePreset,
                              EnemyPatternModuleCatalogSection.WeaponInteraction,
                              true,
                              parent);
    }

    /// <summary>
    /// Reports protected candidates in one extraction slot that cannot bake an active warning.
    /// </summary>
    /// <param name="extractionProperty">Serialized extraction slot containing module candidates.</param>
    /// <param name="interactionIndex">Owning mixed-pattern index used in warning text.</param>
    /// <param name="sourcePreset">Source module catalog used to resolve timing support.</param>
    /// <param name="section">Catalog section that owns the candidates.</param>
    /// <param name="usesNestedInteraction">Whether warning fields live under a nested interaction assembly.</param>
    /// <param name="parent">Visual container receiving warning boxes.</param>
    private static void AddProtectionWarnings(SerializedProperty extractionProperty,
                                              int interactionIndex,
                                              EnemyModulesAndPatternsPreset sourcePreset,
                                              EnemyPatternModuleCatalogSection section,
                                              bool usesNestedInteraction,
                                              VisualElement parent)
    {
        SerializedProperty candidatesProperty = extractionProperty != null
            ? extractionProperty.FindPropertyRelative("candidates")
            : null;

        if (candidatesProperty == null)
            return;

        // Preserve candidate ordering so each warning points to the same card the designer sees.
        for (int candidateIndex = 0; candidateIndex < candidatesProperty.arraySize; candidateIndex++)
        {
            SerializedProperty candidateProperty = candidatesProperty.GetArrayElementAtIndex(candidateIndex);
            SerializedProperty warningOwnerProperty = usesNestedInteraction && candidateProperty != null
                ? candidateProperty.FindPropertyRelative("interaction")
                : candidateProperty;

            if (!ReadBoolean(warningOwnerProperty, "preventWarningInterruption") ||
                IsSupportedWarningCandidate(candidateProperty,
                                            sourcePreset,
                                            section,
                                            usesNestedInteraction,
                                            out _))
                continue;

            parent.Add(new HelpBox("Mixed Pattern Candidate " +
                                   (interactionIndex + 1) +
                                   ", " +
                                   ResolveSectionLabel(section) +
                                   " Module Candidate " +
                                   (candidateIndex + 1) +
                                   " enables Prevent Warning Interruption, but it cannot bake a supported enabled Behaviour Engagement Warning, so the protection has no runtime effect.",
                                   HelpBoxMessageType.Warning));
        }
    }

    /// <summary>
    /// Counts supported warning candidates and the subset that inherits its owning mixed-pattern override.
    /// </summary>
    /// <param name="extractionProperty">Serialized extraction slot containing the candidate list.</param>
    /// <param name="sourcePreset">Source module catalog used to resolve module kinds.</param>
    /// <param name="section">Catalog section that determines the supported timing contract.</param>
    /// <param name="usesNestedInteraction">Whether candidate warning fields live inside a nested interaction assembly.</param>
    /// <param name="warningCandidateCount">Running count of supported enabled warning candidates.</param>
    /// <param name="inheritedWarningCandidateCount">Running count of candidates that do not author their own override.</param>
    private static void AccumulateCandidates(SerializedProperty extractionProperty,
                                             EnemyModulesAndPatternsPreset sourcePreset,
                                             EnemyPatternModuleCatalogSection section,
                                             bool usesNestedInteraction,
                                             ref int warningCandidateCount,
                                             ref int inheritedWarningCandidateCount)
    {
        SerializedProperty candidatesProperty = extractionProperty != null
            ? extractionProperty.FindPropertyRelative("candidates")
            : null;

        if (candidatesProperty == null)
            return;

        // Preserve serialized ordering while ignoring entries that cannot compile a warning config.
        for (int candidateIndex = 0; candidateIndex < candidatesProperty.arraySize; candidateIndex++)
        {
            SerializedProperty candidateProperty = candidatesProperty.GetArrayElementAtIndex(candidateIndex);

            if (!IsSupportedWarningCandidate(candidateProperty,
                                              sourcePreset,
                                              section,
                                              usesNestedInteraction,
                                              out bool usesCandidateOverride))
                continue;

            warningCandidateCount++;

            if (!usesCandidateOverride)
                inheritedWarningCandidateCount++;
        }
    }

    /// <summary>
    /// Resolves whether one serialized candidate can bake a boss-owned engagement warning config.
    /// </summary>
    /// <param name="candidateProperty">Serialized module candidate to inspect.</param>
    /// <param name="sourcePreset">Source module catalog used to resolve the selected module kind.</param>
    /// <param name="section">Catalog section that owns the candidate.</param>
    /// <param name="usesNestedInteraction">Whether assembly fields live under the candidate's interaction property.</param>
    /// <param name="usesCandidateOverride">True when the supported candidate replaces its mixed-pattern settings.</param>
    /// <returns>True when the candidate is enabled, non-null, warning-enabled and backed by a supported boss runtime hook.</returns>
    private static bool IsSupportedWarningCandidate(SerializedProperty candidateProperty,
                                                    EnemyModulesAndPatternsPreset sourcePreset,
                                                    EnemyPatternModuleCatalogSection section,
                                                    bool usesNestedInteraction,
                                                    out bool usesCandidateOverride)
    {
        usesCandidateOverride = false;

        if (candidateProperty == null ||
            !ReadBoolean(candidateProperty.FindPropertyRelative("eligibility"), "enabled") ||
            ReadEnum(candidateProperty, "moduleMode") == Convert.ToInt32(EnemyBossPatternModuleMode.NullModule))
            return false;

        SerializedProperty warningOwnerProperty = usesNestedInteraction
            ? candidateProperty.FindPropertyRelative("interaction")
            : candidateProperty;

        if (warningOwnerProperty == null ||
            usesNestedInteraction && !ReadBoolean(warningOwnerProperty, "isEnabled") ||
            !ReadBoolean(warningOwnerProperty, "displayBehaviourEngagementTrigger"))
            return false;

        SerializedProperty bindingProperty = warningOwnerProperty.FindPropertyRelative("binding");

        if (bindingProperty == null || !ReadBoolean(bindingProperty, "isEnabled"))
            return false;

        SerializedProperty moduleIdProperty = bindingProperty.FindPropertyRelative("moduleId");
        EnemyPatternModuleDefinition moduleDefinition = moduleIdProperty != null
            ? sourcePreset.ResolveModuleDefinitionById(moduleIdProperty.stringValue)
            : null;

        if (moduleDefinition == null ||
            !EnemyOffensiveEngagementSupportUtility.SupportsTimingMode(section,
                                                                       moduleDefinition.ModuleKind,
                                                                       EnemyOffensiveEngagementTimingContext.BossMixedPattern))
            return false;

        usesCandidateOverride = ReadBoolean(warningOwnerProperty, "useEngagementFeedbackOverride");
        return true;
    }
    #endregion

    #region Serialized Reads
    /// <summary>
    /// Reads a nested serialized Boolean without treating a missing field as enabled.
    /// </summary>
    /// <param name="parent">Serialized parent that may contain the requested field.</param>
    /// <param name="relativeName">Relative serialized field name.</param>
    /// <returns>The stored Boolean value, or false when the field is unavailable.</returns>
    private static bool ReadBoolean(SerializedProperty parent, string relativeName)
    {
        SerializedProperty property = parent != null
            ? parent.FindPropertyRelative(relativeName)
            : null;
        return property != null && property.boolValue;
    }

    /// <summary>
    /// Reads a nested serialized enum index without allocating a boxed enum value.
    /// </summary>
    /// <param name="parent">Serialized parent that may contain the requested field.</param>
    /// <param name="relativeName">Relative serialized enum field name.</param>
    /// <returns>The stored enum index, or -1 when the field is unavailable.</returns>
    private static int ReadEnum(SerializedProperty parent, string relativeName)
    {
        SerializedProperty property = parent != null
            ? parent.FindPropertyRelative(relativeName)
            : null;
        return property != null ? property.enumValueIndex : -1;
    }

    /// <summary>
    /// Converts a module catalog section into concise designer-facing warning text.
    /// </summary>
    /// <param name="section">Catalog section to format.</param>
    /// <returns>Readable slot label.</returns>
    private static string ResolveSectionLabel(EnemyPatternModuleCatalogSection section)
    {
        switch (section)
        {
            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                return "Short-Range";

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                return "Weapon";

            default:
                return "Core Movement";
        }
    }
    #endregion

    #endregion
}
