using System.Collections.Generic;
using Unity.Entities;

/// <summary>
/// Applies baked controller-scaling formulas to mutable runtime controller configurations using one supplied stat context.
/// </summary>
internal static class PlayerRuntimeScalingControllerApplyUtility
{
    #region Methods

    #region Application
    /// <summary>
    /// Evaluates every baked controller field against the supplied scalable-stat context and writes successful results to the matching runtime configuration.
    /// </summary>
    /// <param name="controllerScaling">Baked controller scaling metadata evaluated in authoring order.</param>
    /// <param name="variableContext">Typed scalable-stat values available to unified formulas.</param>
    /// <param name="runtimeMovement">Mutable movement configuration receiving movement results.</param>
    /// <param name="runtimeLook">Mutable look configuration receiving look results.</param>
    /// <param name="runtimeCamera">Mutable camera configuration receiving camera results.</param>
    /// <param name="runtimeShooting">Mutable shooting configuration receiving shooting results.</param>
    /// <param name="runtimeAppliedElementSlots">Mutable applied-element slots, or an unavailable buffer when slot selection must remain unchanged.</param>
    /// <param name="runtimeHealth">Mutable health configuration receiving health results.</param>
    public static void Apply(DynamicBuffer<PlayerRuntimeControllerScalingElement> controllerScaling,
                             IReadOnlyDictionary<string, PlayerFormulaValue> variableContext,
                             ref PlayerRuntimeMovementConfig runtimeMovement,
                             ref PlayerRuntimeLookConfig runtimeLook,
                             ref PlayerRuntimeCameraConfig runtimeCamera,
                             ref PlayerRuntimeShootingConfig runtimeShooting,
                             DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> runtimeAppliedElementSlots,
                             ref PlayerRuntimeHealthStatisticsConfig runtimeHealth)
    {
        if (!controllerScaling.IsCreated || variableContext == null)
            return;

        // Evaluate fields in bake order so controller rebuilding remains deterministic across every consumer.
        for (int scalingIndex = 0; scalingIndex < controllerScaling.Length; scalingIndex++)
        {
            PlayerRuntimeControllerScalingElement scalingElement = controllerScaling[scalingIndex];

            if ((PlayerFormulaValueType)scalingElement.ValueType == PlayerFormulaValueType.Boolean)
            {
                if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateBooleanValue(scalingElement.Formula.ToString(),
                                                                                          scalingElement.BaseBooleanValue != 0,
                                                                                          variableContext,
                                                                                          out bool resolvedBoolean))
                {
                    continue;
                }

                PlayerRuntimeScalingControllerFieldApplyUtility.ApplyBooleanValue(scalingElement.FieldId,
                                                                                  resolvedBoolean,
                                                                                  ref runtimeMovement,
                                                                                  ref runtimeLook,
                                                                                  ref runtimeCamera,
                                                                                  ref runtimeShooting,
                                                                                  ref runtimeHealth);
                continue;
            }

            if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateNumericValue(scalingElement.Formula.ToString(),
                                                                                      scalingElement.BaseValue,
                                                                                      scalingElement.IsInteger != 0,
                                                                                      variableContext,
                                                                                      out float resolvedValue))
            {
                continue;
            }

            PlayerRuntimeScalingControllerFieldApplyUtility.ApplyValue(scalingElement.FieldId,
                                                                       scalingElement.SlotIndex,
                                                                       resolvedValue,
                                                                       ref runtimeMovement,
                                                                       ref runtimeLook,
                                                                       ref runtimeCamera,
                                                                       ref runtimeShooting,
                                                                       runtimeAppliedElementSlots,
                                                                       ref runtimeHealth);
        }
    }
    #endregion

    #endregion
}
