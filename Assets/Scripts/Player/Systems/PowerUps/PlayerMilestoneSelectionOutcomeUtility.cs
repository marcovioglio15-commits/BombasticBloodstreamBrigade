using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Applies milestone-selection outcomes such as skip compensations, state reset, and Time.timeScale resume setup.
/// </summary>
public static class PlayerMilestoneSelectionOutcomeUtility
{
    #region Constants
    private const float MinimumEffectiveDelta = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Clears the active milestone selection state and removes all currently displayed offers.
    /// </summary>
    /// <param name="selectionOffers">Offer buffer cleared when the selection closes.</param>
    /// <param name="selectionState">Selection state reset to its inactive defaults.</param>

    public static void ResetSelection(DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffers,
                                      ref PlayerMilestonePowerUpSelectionState selectionState)
    {
        selectionOffers.Clear();
        selectionState = new PlayerMilestonePowerUpSelectionState
        {
            IsSelectionActive = 0,
            MilestoneLevel = 0,
            GamePhaseIndex = -1,
            MilestoneIndex = -1,
            OfferCount = 0
        };
    }

    /// <summary>
    /// Configures the smooth resume state that restores Time.timeScale after a milestone closes.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression config containing the resume duration setting.</param>
    /// <param name="resumeState">Resume state updated in place.</param>

    public static void BeginTimeScaleResume(PlayerProgressionConfig progressionConfig,
                                            ref PlayerMilestoneTimeScaleResumeState resumeState)
    {
        float durationSeconds = progressionConfig.Config.IsCreated
            ? math.max(0f, progressionConfig.Config.Value.MilestoneTimeScaleResumeDurationSeconds)
            : 0f;
        float startTimeScale = math.clamp(Time.timeScale, 0f, 1f);

        if (durationSeconds <= 0f)
        {
            Time.timeScale = 1f;
            resumeState = CreateInactiveResumeState();
            return;
        }

        resumeState = new PlayerMilestoneTimeScaleResumeState
        {
            IsResuming = 1,
            StartTimeScale = startTimeScale,
            TargetTimeScale = 1f,
            DurationSeconds = durationSeconds,
            ElapsedUnscaledSeconds = 0f
        };
        Time.timeScale = startTimeScale;
    }

    /// <summary>
    /// Applies all configured skip compensations for the currently active milestone.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression config containing baked milestone data.</param>
    /// <param name="selectionState">Active selection state identifying the milestone to resolve.</param>
    /// <param name="playerHealth">Mutable player health state.</param>
    /// <param name="playerShield">Mutable player shield state.</param>
    /// <param name="playerExperience">Mutable player experience state.</param>
    /// <param name="playerLevel">Current player level state used to resolve experience percentages.</param>
    /// <param name="powerUpsConfig">Current active-slot configs used to clamp energy compensation.</param>
    /// <param name="powerUpsState">Mutable active-slot runtime state.</param>
    /// <returns>Number of compensation entries that effectively changed runtime state.</returns>
    public static int ApplySkipCompensations(PlayerProgressionConfig progressionConfig,
                                             DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases,
                                             in PlayerMilestonePowerUpSelectionState selectionState,
                                             ref PlayerHealth playerHealth,
                                             ref PlayerShield playerShield,
                                             ref PlayerExperience playerExperience,
                                             in PlayerLevel playerLevel,
                                             in PlayerPowerUpsConfig powerUpsConfig,
                                             ref PlayerPowerUpsState powerUpsState)
    {
        if (!progressionConfig.Config.IsCreated)
            return 0;

        if (!TryResolveSelectedMilestoneIndex(progressionConfig,
                                              in selectionState,
                                              out int milestoneIndex))
            return 0;

        ref PlayerGamePhaseBlob gamePhase = ref progressionConfig.Config.Value.GamePhases[selectionState.GamePhaseIndex];
        ref PlayerLevelUpMilestoneBlob milestoneBlob = ref gamePhase.Milestones[milestoneIndex];
        int appliedCompensationCount = 0;

        // Apply each configured compensation entry in authoring order.
        for (int compensationIndex = 0; compensationIndex < milestoneBlob.SkipCompensationResources.Length; compensationIndex++)
        {
            ref PlayerMilestoneSkipCompensationBlob compensation = ref milestoneBlob.SkipCompensationResources[compensationIndex];

            if (!TryApplySkipCompensation(progressionConfig,
                                          runtimeGamePhases,
                                          in compensation,
                                          ref playerHealth,
                                          ref playerShield,
                                          ref playerExperience,
                                          in playerLevel,
                                          in powerUpsConfig,
                                          ref powerUpsState))
                continue;

            appliedCompensationCount += 1;
        }

        return appliedCompensationCount;
    }

    /// <summary>
    /// Creates the inactive default state used when no Time.timeScale resume is pending.
    /// </summary>
    /// <returns>Inactive milestone Time.timeScale resume state.</returns>
    public static PlayerMilestoneTimeScaleResumeState CreateInactiveResumeState()
    {
        return new PlayerMilestoneTimeScaleResumeState
        {
            IsResuming = 0,
            StartTimeScale = 1f,
            TargetTimeScale = 1f,
            DurationSeconds = 0f,
            ElapsedUnscaledSeconds = 0f
        };
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies one baked skip-compensation entry to the corresponding runtime resource.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression config used to clamp experience by level cap.</param>
    /// <param name="compensation">Baked compensation entry.</param>
    /// <param name="playerHealth">Mutable player health state.</param>
    /// <param name="playerShield">Mutable player shield state.</param>
    /// <param name="playerExperience">Mutable player experience state.</param>
    /// <param name="playerLevel">Current player level state.</param>
    /// <param name="powerUpsConfig">Current active-slot configs.</param>
    /// <param name="powerUpsState">Mutable active-slot runtime state.</param>
    /// <returns>True when runtime state changed; otherwise false.</returns>
    private static bool TryApplySkipCompensation(PlayerProgressionConfig progressionConfig,
                                                 DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases,
                                                 in PlayerMilestoneSkipCompensationBlob compensation,
                                                 ref PlayerHealth playerHealth,
                                                 ref PlayerShield playerShield,
                                                 ref PlayerExperience playerExperience,
                                                 in PlayerLevel playerLevel,
                                                 in PlayerPowerUpsConfig powerUpsConfig,
                                                 ref PlayerPowerUpsState powerUpsState)
    {
        PlayerMilestoneSkipCompensationResourceType resourceType = (PlayerMilestoneSkipCompensationResourceType)compensation.ResourceType;
        PlayerMilestoneCompensationApplyMode applyMode = compensation.ApplyMode == (byte)PlayerMilestoneCompensationApplyMode.Percent
            ? PlayerMilestoneCompensationApplyMode.Percent
            : PlayerMilestoneCompensationApplyMode.Flat;

        switch (resourceType)
        {
            case PlayerMilestoneSkipCompensationResourceType.Health:
                return TryApplyHealthCompensation(applyMode, compensation.Value, ref playerHealth);
            case PlayerMilestoneSkipCompensationResourceType.Shield:
                return TryApplyShieldCompensation(applyMode, compensation.Value, ref playerShield);
            case PlayerMilestoneSkipCompensationResourceType.PrimaryActiveEnergy:
                return TryApplyEnergyCompensation(applyMode,
                                                  compensation.Value,
                                                  in powerUpsConfig.PrimarySlot,
                                                  ref powerUpsState.PrimaryEnergy);
            case PlayerMilestoneSkipCompensationResourceType.SecondaryActiveEnergy:
                return TryApplyEnergyCompensation(applyMode,
                                                  compensation.Value,
                                                  in powerUpsConfig.SecondarySlot,
                                                  ref powerUpsState.SecondaryEnergy);
            case PlayerMilestoneSkipCompensationResourceType.Experience:
                return TryApplyExperienceCompensation(progressionConfig,
                                                     runtimeGamePhases,
                                                     applyMode,
                                                     compensation.Value,
                                                     ref playerExperience,
                                                     in playerLevel);
            default:
                return false;
        }
    }

    /// <summary>
    /// Applies one health compensation entry while clamping the result to the current maximum health.
    /// </summary>
    /// <param name="applyMode">Value interpretation mode.</param>
    /// <param name="rawValue">Flat amount or percentage value.</param>
    /// <param name="playerHealth">Mutable player health state.</param>
    /// <returns>True when health changed; otherwise false.</returns>
    private static bool TryApplyHealthCompensation(PlayerMilestoneCompensationApplyMode applyMode,
                                                   float rawValue,
                                                   ref PlayerHealth playerHealth)
    {
        float maxHealth = math.max(0f, playerHealth.Max);

        if (maxHealth <= 0f)
            return false;

        float currentHealth = math.clamp(playerHealth.Current, 0f, maxHealth);
        float deltaValue = ResolveResourceCompensationAmount(applyMode, rawValue, currentHealth);

        if (deltaValue <= 0f)
            return false;

        float updatedHealth = math.min(maxHealth, currentHealth + deltaValue);

        if (updatedHealth <= currentHealth + MinimumEffectiveDelta)
            return false;

        playerHealth.Current = updatedHealth;
        return true;
    }

    /// <summary>
    /// Applies one shield compensation entry while clamping the result to the current maximum shield.
    /// </summary>
    /// <param name="applyMode">Value interpretation mode.</param>
    /// <param name="rawValue">Flat amount or percentage value.</param>
    /// <param name="playerShield">Mutable player shield state.</param>
    /// <returns>True when shield changed; otherwise false.</returns>
    private static bool TryApplyShieldCompensation(PlayerMilestoneCompensationApplyMode applyMode,
                                                   float rawValue,
                                                   ref PlayerShield playerShield)
    {
        float maxShield = math.max(0f, playerShield.Max);

        if (maxShield <= 0f)
            return false;

        float currentShield = math.clamp(playerShield.Current, 0f, maxShield);
        float deltaValue = ResolveResourceCompensationAmount(applyMode, rawValue, currentShield);

        if (deltaValue <= 0f)
            return false;

        float updatedShield = math.min(maxShield, currentShield + deltaValue);

        if (updatedShield <= currentShield + MinimumEffectiveDelta)
            return false;

        playerShield.Current = updatedShield;
        return true;
    }

    /// <summary>
    /// Applies one energy compensation entry to the selected active slot.
    /// </summary>
    /// <param name="applyMode">Value interpretation mode.</param>
    /// <param name="rawValue">Flat amount or percentage value.</param>
    /// <param name="slotConfig">Active-slot config used to resolve the maximum energy.</param>
    /// <param name="currentEnergy">Mutable slot energy.</param>
    /// <returns>True when slot energy changed; otherwise false.</returns>
    private static bool TryApplyEnergyCompensation(PlayerMilestoneCompensationApplyMode applyMode,
                                                   float rawValue,
                                                   in PlayerPowerUpSlotConfig slotConfig,
                                                   ref float currentEnergy)
    {
        if (slotConfig.IsDefined == 0)
            return false;

        float maximumEnergy = math.max(0f, slotConfig.MaximumEnergy);

        if (maximumEnergy <= 0f)
            return false;

        float currentEnergyValue = math.clamp(currentEnergy, 0f, maximumEnergy);
        float deltaValue = ResolveResourceCompensationAmount(applyMode, rawValue, currentEnergyValue);

        if (deltaValue <= 0f)
            return false;

        float updatedEnergy = math.min(maximumEnergy, currentEnergyValue + deltaValue);

        if (updatedEnergy <= currentEnergyValue + MinimumEffectiveDelta)
            return false;

        currentEnergy = updatedEnergy;
        return true;
    }

    /// <summary>
    /// Applies one experience compensation entry.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression config used to respect the current level cap.</param>
    /// <param name="applyMode">Value interpretation mode.</param>
    /// <param name="rawValue">Flat amount or percentage value.</param>
    /// <param name="playerExperience">Mutable player experience state.</param>
    /// <param name="playerLevel">Current player level state used to resolve the next required experience.</param>
    /// <returns>True when experience changed; otherwise false.</returns>
    private static bool TryApplyExperienceCompensation(PlayerProgressionConfig progressionConfig,
                                                       DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases,
                                                       PlayerMilestoneCompensationApplyMode applyMode,
                                                       float rawValue,
                                                       ref PlayerExperience playerExperience,
                                                       in PlayerLevel playerLevel)
    {
        float remainingExperienceCapacity = PlayerProgressionPhaseUtility.ResolveRemainingExperienceUntilLevelCap(progressionConfig,
                                                                                                                   runtimeGamePhases,
                                                                                                                   playerLevel.Current,
                                                                                                                   playerExperience.Current);

        if (remainingExperienceCapacity <= 0f)
            return false;

        float currentExperience = math.max(0f, playerExperience.Current);
        float deltaValue = applyMode == PlayerMilestoneCompensationApplyMode.Percent
            ? math.max(0f, rawValue) * 0.01f * math.max(0f, playerLevel.RequiredExperienceForNextLevel)
            : math.max(0f, rawValue);

        if (deltaValue <= 0f)
            return false;

        if (deltaValue > remainingExperienceCapacity)
            deltaValue = remainingExperienceCapacity;

        playerExperience.Current = currentExperience + deltaValue;
        return true;
    }

    /// <summary>
    /// Resolves the additive amount granted by one non-experience resource compensation entry.
    /// </summary>
    /// <param name="applyMode">Value interpretation mode.</param>
    /// <param name="rawValue">Flat amount or percentage value.</param>
    /// <param name="currentValue">Current resource value used as percentage base when needed.</param>
    /// <returns>Resolved additive amount.</returns>
    private static float ResolveResourceCompensationAmount(PlayerMilestoneCompensationApplyMode applyMode,
                                                           float rawValue,
                                                           float currentValue)
    {
        float positiveValue = math.max(0f, rawValue);

        if (applyMode == PlayerMilestoneCompensationApplyMode.Flat)
            return positiveValue;

        return math.max(0f, currentValue) * positiveValue * 0.01f;
    }

    private static bool TryResolveSelectedMilestoneIndex(PlayerProgressionConfig progressionConfig,
                                                         in PlayerMilestonePowerUpSelectionState selectionState,
                                                         out int milestoneIndex)
    {
        milestoneIndex = -1;

        if (!progressionConfig.Config.IsCreated)
            return false;

        ref BlobArray<PlayerGamePhaseBlob> gamePhases = ref progressionConfig.Config.Value.GamePhases;

        if (selectionState.GamePhaseIndex < 0 || selectionState.GamePhaseIndex >= gamePhases.Length)
            return false;

        ref PlayerGamePhaseBlob gamePhase = ref gamePhases[selectionState.GamePhaseIndex];
        milestoneIndex = selectionState.MilestoneIndex;

        if (milestoneIndex < 0 || milestoneIndex >= gamePhase.Milestones.Length)
        {
            if (!PlayerProgressionPhaseUtility.TryResolveMilestoneIndex(progressionConfig,
                                                                        selectionState.GamePhaseIndex,
                                                                        selectionState.MilestoneLevel,
                                                                        out milestoneIndex))
            {
                return false;
            }
        }

        return true;
    }
    #endregion

    #endregion
}
