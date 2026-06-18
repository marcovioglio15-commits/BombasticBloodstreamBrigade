using System;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Provides shared helpers to resolve runtime game phases, milestone flags, and level-up experience requirements.
/// </summary>
public static class PlayerProgressionPhaseUtility
{
    #region Constants
    private const int DefaultLevelCap = 100;
    private const float DefaultRequiredExperience = 100f;
    private const string DefaultPhaseID = "Phase0";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves required experience for the next level-up attempt using the current player level.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression configuration component.</param>
    /// <param name="levelValue">Current player level used to resolve the next level-up requirement.</param>
    /// <param name="activeGamePhaseIndex">Resolved index of the active game phase.</param>
    /// <param name="isMilestoneRequirement">True when the next level-up reaches a milestone with a custom requirement.</param>
    /// <param name="milestoneLevel">Target milestone level reached by the next level-up, or 0 when not used.</param>
    /// <returns>Required experience for the next level-up attempt.</returns>
    public static float ResolveRequiredExperienceForLevel(PlayerProgressionConfig progressionConfig,
                                                          int levelValue,
                                                          out int activeGamePhaseIndex,
                                                          out bool isMilestoneRequirement,
                                                          out int milestoneLevel)
    {
        if (levelValue < 0)
            levelValue = 0;

        if (!progressionConfig.Config.IsCreated)
        {
            activeGamePhaseIndex = 0;
            isMilestoneRequirement = false;
            milestoneLevel = 0;
            return DefaultRequiredExperience;
        }

        ref BlobArray<PlayerGamePhaseBlob> gamePhases = ref progressionConfig.Config.Value.GamePhases;
        return ResolveRequiredExperienceForLevel(ref gamePhases,
                                                 levelValue,
                                                 out activeGamePhaseIndex,
                                                 out isMilestoneRequirement,
                                                 out milestoneLevel);
    }

    /// <summary>
    /// Resolves required experience for the next level-up attempt using the current runtime-scaled phase values.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression configuration component.</param>
    /// <param name="runtimeGamePhases">Runtime-scaled phase buffer.</param>
    /// <param name="levelValue">Current player level used to resolve the next level-up requirement.</param>
    /// <param name="activeGamePhaseIndex">Resolved index of the active game phase.</param>
    /// <param name="isMilestoneRequirement">True when the next level-up reaches a milestone with a custom requirement.</param>
    /// <param name="milestoneLevel">Target milestone level reached by the next level-up, or 0 when not used.</param>
    /// <returns>Required experience for the next level-up attempt.</returns>
    public static float ResolveRequiredExperienceForLevel(PlayerProgressionConfig progressionConfig,
                                                          DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases,
                                                          int levelValue,
                                                          out int activeGamePhaseIndex,
                                                          out bool isMilestoneRequirement,
                                                          out int milestoneLevel)
    {
        if (!runtimeGamePhases.IsCreated || runtimeGamePhases.Length <= 0)
        {
            return ResolveRequiredExperienceForLevel(progressionConfig,
                                                    levelValue,
                                                    out activeGamePhaseIndex,
                                                    out isMilestoneRequirement,
                                                    out milestoneLevel);
        }

        return ResolveRequiredExperienceForLevel(progressionConfig,
                                                 runtimeGamePhases,
                                                 levelValue,
                                                 out activeGamePhaseIndex,
                                                 out isMilestoneRequirement,
                                                 out milestoneLevel,
                                                 false);
    }

    /// <summary>
    /// Resolves the configured runtime level cap using a safe fallback when progression data is missing.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression configuration component.</param>
    /// <returns>Maximum reachable player level for this configuration.</returns>
    public static int ResolveLevelCap(PlayerProgressionConfig progressionConfig)
    {
        if (!progressionConfig.Config.IsCreated)
        {
            return DefaultLevelCap;
        }

        int levelCap = progressionConfig.Config.Value.LevelCap;
        return levelCap < 1 ? DefaultLevelCap : levelCap;
    }

    /// <summary>
    /// Checks whether the specified player level has already reached or exceeded the configured level cap.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression configuration component.</param>
    /// <param name="levelValue">Current player level to evaluate.</param>
    /// <returns>True when the level is capped and should stop receiving more experience.</returns>
    public static bool HasReachedLevelCap(PlayerProgressionConfig progressionConfig, int levelValue)
    {
        if (levelValue < 0)
        {
            levelValue = 0;
        }

        return levelValue >= ResolveLevelCap(progressionConfig);
    }

    /// <summary>
    /// Resolves how much additional experience the player can still receive before reaching the configured level cap.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression configuration component.</param>
    /// <param name="levelValue">Current player level.</param>
    /// <param name="currentExperience">Current stored progress toward the next level-up.</param>
    /// <returns>Remaining experience capacity until the level cap is reached.</returns>
    public static float ResolveRemainingExperienceUntilLevelCap(PlayerProgressionConfig progressionConfig,
                                                                int levelValue,
                                                                float currentExperience)
    {
        if (levelValue < 0)
        {
            levelValue = 0;
        }

        if (currentExperience < 0f)
        {
            currentExperience = 0f;
        }

        int levelCap = ResolveLevelCap(progressionConfig);

        if (levelValue >= levelCap)
        {
            return 0f;
        }

        float remainingExperience = 0f;
        int simulatedLevel = levelValue;
        float bufferedExperience = currentExperience;

        // Sum the missing thresholds from the current level up to the cap.
        while (simulatedLevel < levelCap)
        {
            float requiredExperienceForNextLevel = ResolveRequiredExperienceForLevel(progressionConfig,
                                                                                     simulatedLevel,
                                                                                     out int _,
                                                                                     out bool _,
                                                                                     out int _);
            float remainingRequirement = requiredExperienceForNextLevel - bufferedExperience;

            if (remainingRequirement > 0f)
            {
                remainingExperience += remainingRequirement;
            }

            bufferedExperience = 0f;
            simulatedLevel += 1;
        }

        return remainingExperience;
    }

    /// <summary>
    /// Resolves how much additional experience the player can still receive before reaching the configured level cap using runtime-scaled phase values.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression configuration component.</param>
    /// <param name="runtimeGamePhases">Runtime-scaled phase buffer.</param>
    /// <param name="levelValue">Current player level.</param>
    /// <param name="currentExperience">Current stored progress toward the next level-up.</param>
    /// <returns>Remaining experience capacity until the level cap is reached.</returns>
    public static float ResolveRemainingExperienceUntilLevelCap(PlayerProgressionConfig progressionConfig,
                                                                DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases,
                                                                int levelValue,
                                                                float currentExperience)
    {
        if (!runtimeGamePhases.IsCreated || runtimeGamePhases.Length <= 0)
            return ResolveRemainingExperienceUntilLevelCap(progressionConfig, levelValue, currentExperience);

        if (levelValue < 0)
            levelValue = 0;

        if (currentExperience < 0f)
            currentExperience = 0f;

        int levelCap = ResolveLevelCap(progressionConfig);

        if (levelValue >= levelCap)
            return 0f;

        float remainingExperience = 0f;
        int simulatedLevel = levelValue;
        float bufferedExperience = currentExperience;

        while (simulatedLevel < levelCap)
        {
            float requiredExperienceForNextLevel = ResolveRequiredExperienceForLevel(progressionConfig,
                                                                                     runtimeGamePhases,
                                                                                     simulatedLevel,
                                                                                     out int _,
                                                                                     out bool _,
                                                                                     out int _);
            float remainingRequirement = requiredExperienceForNextLevel - bufferedExperience;

            if (remainingRequirement > 0f)
                remainingExperience += remainingRequirement;

            bufferedExperience = 0f;
            simulatedLevel += 1;
        }

        return remainingExperience;
    }

    /// <summary>
    /// Resolves the active game phase index for a given level from baked progression config.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression configuration component.</param>
    /// <param name="levelValue">Player level used to select the phase.</param>
    /// <returns>Index of the active game phase, or 0 when data is missing.</returns>
    public static int ResolveActiveGamePhaseIndex(PlayerProgressionConfig progressionConfig, int levelValue)
    {
        if (!progressionConfig.Config.IsCreated)
        {
            return 0;
        }

        ref BlobArray<PlayerGamePhaseBlob> gamePhases = ref progressionConfig.Config.Value.GamePhases;
        return ResolveActiveGamePhaseIndex(ref gamePhases, levelValue);
    }

    /// <summary>
    /// Resolves displayable phase ID text from baked progression config and phase index.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression configuration component.</param>
    /// <param name="phaseIndex">Phase index to resolve.</param>
    /// <returns>Phase ID string with safe fallback when index/data is invalid.</returns>
    public static string ResolvePhaseID(PlayerProgressionConfig progressionConfig, int phaseIndex)
    {
        if (!progressionConfig.Config.IsCreated)
        {
            return DefaultPhaseID;
        }

        ref BlobArray<PlayerGamePhaseBlob> gamePhases = ref progressionConfig.Config.Value.GamePhases;

        if (phaseIndex < 0 || phaseIndex >= gamePhases.Length)
        {
            return DefaultPhaseID;
        }

        string resolvedPhaseID = gamePhases[phaseIndex].PhaseID.ToString();

        if (string.IsNullOrWhiteSpace(resolvedPhaseID))
        {
            return DefaultPhaseID;
        }

        return resolvedPhaseID;
    }

    /// <summary>
    /// Resolves one milestone index inside the specified game phase.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression configuration component.</param>
    /// <param name="gamePhaseIndex">Game phase index expected to contain the milestone.</param>
    /// <param name="milestoneLevel">Milestone level to resolve.</param>
    /// <param name="milestoneIndex">Resolved milestone index when found.</param>
    /// <returns>True when the milestone exists in the specified phase; otherwise false.</returns>
    public static bool TryResolveMilestoneIndex(PlayerProgressionConfig progressionConfig,
                                                int gamePhaseIndex,
                                                int milestoneLevel,
                                                out int milestoneIndex)
    {
        milestoneIndex = -1;

        if (!progressionConfig.Config.IsCreated)
            return false;

        ref BlobArray<PlayerGamePhaseBlob> gamePhases = ref progressionConfig.Config.Value.GamePhases;

        if (gamePhaseIndex < 0 || gamePhaseIndex >= gamePhases.Length)
            return false;

        ref PlayerGamePhaseBlob gamePhase = ref gamePhases[gamePhaseIndex];
        return TryResolveMilestoneIndex(ref gamePhase, milestoneLevel, out milestoneIndex);
    }
    #endregion

    #region Private Methods
    private static float ResolveRequiredExperienceForLevel(ref BlobArray<PlayerGamePhaseBlob> gamePhases,
                                                           int levelValue,
                                                           out int activeGamePhaseIndex,
                                                           out bool isMilestoneRequirement,
                                                           out int milestoneLevel)
    {
        if (levelValue < 0)
            levelValue = 0;

        activeGamePhaseIndex = ResolveActiveGamePhaseIndex(ref gamePhases, levelValue);
        isMilestoneRequirement = false;
        milestoneLevel = 0;

        if (gamePhases.Length == 0)
        {
            return DefaultRequiredExperience;
        }

        ref PlayerGamePhaseBlob activeGamePhase = ref gamePhases[activeGamePhaseIndex];
        int targetLevel = levelValue + 1;
        int relativePhaseLevel = levelValue - activeGamePhase.StartsAtLevel;

        if (relativePhaseLevel < 0)
        {
            relativePhaseLevel = 0;
        }

        float resolvedRequirement = activeGamePhase.StartingRequiredLevelUpExp + (activeGamePhase.RequiredExperienceGrouth * relativePhaseLevel);

        if (resolvedRequirement < 1f)
        {
            resolvedRequirement = 1f;
        }

        ref BlobArray<PlayerLevelUpMilestoneBlob> milestones = ref activeGamePhase.Milestones;

        for (int milestoneIndex = 0; milestoneIndex < milestones.Length; milestoneIndex++)
        {
            ref PlayerLevelUpMilestoneBlob milestone = ref milestones[milestoneIndex];

            if (!MatchesMilestoneLevel(ref milestone, targetLevel))
            {
                continue;
            }

            float milestoneRequirement = milestone.SpecialExpRequirement;

            if (milestoneRequirement < 1f)
            {
                milestoneRequirement = 1f;
            }

            resolvedRequirement = milestoneRequirement;
            isMilestoneRequirement = true;
            milestoneLevel = targetLevel;
            break;
        }

        return resolvedRequirement;
    }

    private static float ResolveRequiredExperienceForLevel(PlayerProgressionConfig progressionConfig,
                                                           DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases,
                                                           int levelValue,
                                                           out int activeGamePhaseIndex,
                                                           out bool isMilestoneRequirement,
                                                           out int milestoneLevel,
                                                           bool unusedFlag)
    {
        if (levelValue < 0)
            levelValue = 0;

        activeGamePhaseIndex = ResolveActiveGamePhaseIndex(progressionConfig, levelValue);
        isMilestoneRequirement = false;
        milestoneLevel = 0;

        if (!runtimeGamePhases.IsCreated || runtimeGamePhases.Length == 0)
            return DefaultRequiredExperience;

        if (activeGamePhaseIndex < 0 || activeGamePhaseIndex >= runtimeGamePhases.Length)
            activeGamePhaseIndex = 0;

        PlayerRuntimeGamePhaseElement activeGamePhase = runtimeGamePhases[activeGamePhaseIndex];
        int targetLevel = levelValue + 1;
        int relativePhaseLevel = levelValue - activeGamePhase.StartsAtLevel;

        if (relativePhaseLevel < 0)
            relativePhaseLevel = 0;

        float resolvedRequirement = activeGamePhase.StartingRequiredLevelUpExp + (activeGamePhase.RequiredExperienceGrouth * relativePhaseLevel);

        if (resolvedRequirement < 1f)
            resolvedRequirement = 1f;

        ref BlobArray<PlayerGamePhaseBlob> bakedGamePhases = ref progressionConfig.Config.Value.GamePhases;

        if (activeGamePhaseIndex < 0 || activeGamePhaseIndex >= bakedGamePhases.Length)
            return resolvedRequirement;

        ref BlobArray<PlayerLevelUpMilestoneBlob> milestones = ref bakedGamePhases[activeGamePhaseIndex].Milestones;

        for (int milestoneIndex = 0; milestoneIndex < milestones.Length; milestoneIndex++)
        {
            ref PlayerLevelUpMilestoneBlob milestone = ref milestones[milestoneIndex];

            if (!MatchesMilestoneLevel(ref milestone, targetLevel))
                continue;

            float milestoneRequirement = milestone.SpecialExpRequirement;

            if (milestoneRequirement < 1f)
                milestoneRequirement = 1f;

            resolvedRequirement = milestoneRequirement;
            isMilestoneRequirement = true;
            milestoneLevel = targetLevel;
            break;
        }

        return resolvedRequirement;
    }

    private static bool TryResolveMilestoneIndex(ref PlayerGamePhaseBlob gamePhase,
                                                 int milestoneLevel,
                                                 out int milestoneIndex)
    {
        milestoneIndex = -1;

        for (int milestoneIndexValue = 0; milestoneIndexValue < gamePhase.Milestones.Length; milestoneIndexValue++)
        {
            ref PlayerLevelUpMilestoneBlob milestone = ref gamePhase.Milestones[milestoneIndexValue];

            if (!MatchesMilestoneLevel(ref milestone, milestoneLevel))
                continue;

            milestoneIndex = milestoneIndexValue;
            return true;
        }

        return false;
    }

    private static int ResolveActiveGamePhaseIndex(ref BlobArray<PlayerGamePhaseBlob> gamePhases, int levelValue)
    {
        if (levelValue < 0)
            levelValue = 0;

        if (gamePhases.Length == 0)
        {
            return 0;
        }

        int resolvedPhaseIndex = 0;
        int resolvedStartLevel = int.MinValue;

        for (int phaseIndex = 0; phaseIndex < gamePhases.Length; phaseIndex++)
        {
            ref PlayerGamePhaseBlob gamePhase = ref gamePhases[phaseIndex];

            if (levelValue < gamePhase.StartsAtLevel)
            {
                continue;
            }

            if (gamePhase.StartsAtLevel < resolvedStartLevel)
            {
                continue;
            }

            if (gamePhase.StartsAtLevel == resolvedStartLevel && phaseIndex < resolvedPhaseIndex)
            {
                continue;
            }

            resolvedPhaseIndex = phaseIndex;
            resolvedStartLevel = gamePhase.StartsAtLevel;
        }

        return resolvedStartLevel == int.MinValue ? 0 : resolvedPhaseIndex;
    }

    private static bool MatchesMilestoneLevel(ref PlayerLevelUpMilestoneBlob milestone, int levelValue)
    {
        if (levelValue < milestone.MilestoneLevel)
            return false;

        if (milestone.IsRecurring == 0)
            return levelValue == milestone.MilestoneLevel;

        int recurrenceIntervalLevels = milestone.RecurrenceIntervalLevels > 0 ? milestone.RecurrenceIntervalLevels : 1;
        return (levelValue - milestone.MilestoneLevel) % recurrenceIntervalLevels == 0;
    }
    #endregion

    #endregion
}
