#if UNITY_EDITOR
using System;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Verifies that Excel-imported Player scaling authoring reaches the same scaled presets and blobs used by baking.
/// </summary>
public static class PlayerScalingExcelBakeSmokeAssertionUtility
{
    #region Constants
    private const float NumericTolerance = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the real bake scope and configuration blobs, then validates all supported imported result families.
    /// </summary>
    /// <param name="controllerPreset">Imported controller preset containing the enum scaling rule.</param>
    /// <param name="progressionPreset">Imported progression preset containing numeric, Boolean, token and Color rules.</param>
    /// <param name="expectMergedRule">True when the controlled merge rule should also affect milestone timing.</param>
    public static void AssertImportedScaling(PlayerControllerPreset controllerPreset,
                                             PlayerProgressionPreset progressionPreset,
                                             bool expectMergedRule)
    {
        if (controllerPreset == null)
            throw new ArgumentNullException(nameof(controllerPreset));

        if (progressionPreset == null)
            throw new ArgumentNullException(nameof(progressionPreset));

        BlobAssetReference<PlayerProgressionConfigBlob> progressionBlob = default;
        BlobAssetReference<PlayerControllerConfigBlob> controllerBlob = default;

        // Exercise the exact clone-and-formula path consumed by PlayerAuthoring before blob construction.
        using (PlayerScaledPresetScope scope = PlayerPresetScalingBakeUtility.CreateScope(controllerPreset,
                                                                                           progressionPreset,
                                                                                           null,
                                                                                           null,
                                                                                           null,
                                                                                           null))
        {
            try
            {
                progressionBlob = PlayerProgressionBlobBakeUtility.BuildProgressionConfigBlob(
                    scope.ProgressionPreset,
                    null,
                    progressionPreset,
                    null);
                controllerBlob = PlayerControllerConfigBakeUtility.BuildConfigBlob(scope.ControllerPreset);
                ValidateProgressionBlob(progressionBlob, expectMergedRule);
                ValidateControllerBlob(controllerBlob);
            }
            finally
            {
                if (progressionBlob.IsCreated)
                    progressionBlob.Dispose();

                if (controllerBlob.IsCreated)
                    controllerBlob.Dispose();
            }
        }
    }
    #endregion

    #region Assertions
    /// <summary>
    /// Validates scaled values, unscaled bases and runtime formula metadata in the progression blob.
    /// </summary>
    /// <param name="blob">Progression blob built by the production bake utility.</param>
    /// <param name="expectMergedRule">True when milestone timing must include the merged numeric rule.</param>
    private static void ValidateProgressionBlob(BlobAssetReference<PlayerProgressionConfigBlob> blob,
                                                bool expectMergedRule)
    {
        if (!blob.IsCreated)
            throw new InvalidOperationException("Player progression bake did not create a blob.");

        ref PlayerProgressionConfigBlob root = ref blob.Value;
        AssertApproximately(root.ExperiencePickupRadius, 7f, "numeric scaled value");
        AssertApproximately(root.BaseExperiencePickupRadius, 5f, "numeric base value");
        AssertApproximately(root.MilestoneSkipHoldFillColor.x, 0.4f, "Color-channel scaled value");
        AssertApproximately(root.BaseMilestoneSkipHoldFillColor.x, 0.8f, "Color-channel base value");

        if (root.MilestoneSkipOnlyFromExitInput != 1 || root.BaseMilestoneSkipOnlyFromExitInput != 0)
            throw new InvalidOperationException("Boolean scaling did not preserve separate scaled and base blob values.");

        if (!string.Equals(root.ExperiencePickupRadiusScalingFormula.ToString(),
                           "[this] + [Level]",
                           StringComparison.Ordinal))
            throw new InvalidOperationException("Numeric runtime formula metadata was not baked from imported authoring.");

        if (!string.Equals(root.MilestoneSkipOnlyFromExitInputScalingFormula.ToString(),
                           "[Level] > 0",
                           StringComparison.Ordinal))
            throw new InvalidOperationException("Boolean runtime formula metadata was not baked from imported authoring.");

        if (!string.Equals(root.MilestoneSkipHoldFillColorRScalingFormula.ToString(),
                           "[this] * 0.5",
                           StringComparison.Ordinal))
            throw new InvalidOperationException("Color-channel runtime formula metadata was not baked from imported authoring.");

        // Token scaling selects the second authored schedule named by the imported formula result.
        if (root.EquippedScheduleIndex != 1 || root.Schedules.Length != 2)
            throw new InvalidOperationException("Token scaling did not select the expected baked schedule.");

        if (!string.Equals(root.Schedules[root.EquippedScheduleIndex].ScheduleId.ToString(),
                           "Scaled",
                           StringComparison.Ordinal))
            throw new InvalidOperationException("Token scaling selected an unexpected baked schedule ID.");

        AssertApproximately(root.MilestoneTimeScaleResumeDurationSeconds,
                            expectMergedRule ? 2.2f : 0.2f,
                            "controlled merge scaled value");
    }

    /// <summary>
    /// Validates that the imported enum formula reaches the production controller blob.
    /// </summary>
    /// <param name="blob">Controller blob built by the production bake utility.</param>
    private static void ValidateControllerBlob(BlobAssetReference<PlayerControllerConfigBlob> blob)
    {
        if (!blob.IsCreated)
            throw new InvalidOperationException("Player controller bake did not create a blob.");

        if (blob.Value.Movement.DirectionsMode != MovementDirectionsMode.DiscreteCount)
            throw new InvalidOperationException("Enum scaling did not reach the baked movement configuration.");
    }

    /// <summary>
    /// Throws when one floating-point bake result differs from its deterministic expected value.
    /// </summary>
    /// <param name="actualValue">Value emitted by production baking.</param>
    /// <param name="expectedValue">Expected deterministic value.</param>
    /// <param name="scenario">Readable assertion label.</param>
    private static void AssertApproximately(float actualValue,
                                            float expectedValue,
                                            string scenario)
    {
        if (Mathf.Abs(actualValue - expectedValue) <= NumericTolerance)
            return;

        throw new InvalidOperationException(scenario + " mismatch. Expected " + expectedValue +
                                            ", received " + actualValue + ".");
    }
    #endregion

    #endregion
}
#endif
