using Unity.Entities;
using Unity.Mathematics;

#region Root Blob
/// <summary>
/// Holds progression configuration data used to initialize level-up thresholds and scalable runtime stats.
/// </summary>
public struct PlayerProgressionConfigBlob
{
    public int LevelCap;
    public float ExperiencePickupRadius;
    public float BaseExperiencePickupRadius;
    public float MilestoneTimeScaleResumeDurationSeconds;
    public float MilestoneSkipHoldConfirmationSeconds;
    public float BaseMilestoneSkipHoldConfirmationSeconds;
    public float4 MilestoneSkipHoldFillColor;
    public float4 BaseMilestoneSkipHoldFillColor;
    public BlobString ExperiencePickupRadiusScalingFormula;
    public BlobString MilestoneSkipHoldConfirmationSecondsScalingFormula;
    public BlobString MilestoneSkipHoldFillColorRScalingFormula;
    public BlobString MilestoneSkipHoldFillColorGScalingFormula;
    public BlobString MilestoneSkipHoldFillColorBScalingFormula;
    public BlobString MilestoneSkipHoldFillColorAScalingFormula;
    public BlobArray<PlayerGamePhaseBlob> GamePhases;
    public BlobArray<PlayerScalableStatBlob> ScalableStats;
    public BlobArray<PlayerLevelUpScheduleBlob> Schedules;
    public int EquippedScheduleIndex;
}
#endregion

#region Game Phases
/// <summary>
/// Stores one runtime game phase used to resolve level-up experience requirements.
/// </summary>
public struct PlayerGamePhaseBlob
{
    public BlobString PhaseID;
    public int StartsAtLevel;
    public float StartingRequiredLevelUpExp;
    public float RequiredExperienceGrouth;
    public BlobArray<PlayerLevelUpMilestoneBlob> Milestones;
}
#endregion

#region Milestones
/// <summary>
/// Stores one milestone override for the experience required to reach a specific player level.
/// </summary>
public struct PlayerLevelUpMilestoneBlob
{
    public int MilestoneLevel;
    public float SpecialExpRequirement;
    public byte IsRecurring;
    public int RecurrenceIntervalLevels;
    public BlobArray<PlayerMilestonePowerUpUnlockBlob> PowerUpUnlocks;
    public BlobArray<PlayerMilestoneSkipCompensationBlob> SkipCompensationResources;
}
#endregion

#region Milestone Power-Up Unlocks
/// <summary>
/// Stores one milestone power-up extraction with its own tier-roll candidates.
/// </summary>
public struct PlayerMilestonePowerUpUnlockBlob
{
    public BlobString DropPoolId;
    public BlobArray<PlayerMilestoneTierRollBlob> TierRolls;
}
#endregion

#region Milestone Tier Rolls
/// <summary>
/// Stores one percentage-based tier candidate used by milestone-based power-up extraction rolls.
/// </summary>
public struct PlayerMilestoneTierRollBlob
{
    public BlobString TierId;
    public float SelectionPercentage;
    public float BaseSelectionPercentage;
    public BlobString ScalingFormula;
}
#endregion

#region Milestone Skip Compensation
/// <summary>
/// Stores one resource compensation granted when the player skips a milestone selection.
/// </summary>
public struct PlayerMilestoneSkipCompensationBlob
{
    public byte ResourceType;
    public byte ApplyMode;
    public float Value;
}
#endregion

#region Scalable Stats
/// <summary>
/// Stores one scalable stat entry baked from progression presets.
/// </summary>
public struct PlayerScalableStatBlob
{
    public BlobString Name;
    public byte Type;
    public float DefaultValue;
    public float MinimumValue;
    public float MaximumValue;
    public byte DefaultBooleanValue;
    public BlobString DefaultTokenValue;
}
#endregion

#region Schedules
/// <summary>
/// Stores one repeating level-up schedule made of ordered scalable-stat steps.
/// </summary>
public struct PlayerLevelUpScheduleBlob
{
    public BlobString ScheduleId;
    public BlobArray<PlayerLevelUpScheduleStepBlob> Steps;
}

/// <summary>
/// Stores one schedule step applied on level-up.
/// </summary>
public struct PlayerLevelUpScheduleStepBlob
{
    public BlobString StatName;
    public byte ApplyMode;
    public float Value;
    public float BaseValue;
    public BlobString ScalingFormula;
}
#endregion
