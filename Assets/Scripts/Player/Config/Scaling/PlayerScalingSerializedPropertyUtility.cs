#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Centralizes typed Add Scaling reads and writes against SerializedProperty targets during validation and bake-time preset cloning.
/// </summary>
public static class PlayerScalingSerializedPropertyUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Reads the current serialized target value as a typed formula input.
    /// </summary>
    /// <param name="property">Serialized property being scaled.</param>
    /// <param name="thisValue">Typed value bound to the reserved [this] token.</param>
    /// <param name="isIntegerLike">True when numeric outputs should be rounded before assignment.</param>
    /// <returns>True when the property type is supported.</returns>
    public static bool TryReadFormulaInput(SerializedProperty property,
                                           out PlayerFormulaValue thisValue,
                                           out bool isIntegerLike)
    {
        thisValue = PlayerFormulaValue.CreateInvalid();
        isIntegerLike = false;

        if (property == null)
            return false;

        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                thisValue = PlayerFormulaValue.CreateNumber(property.intValue);
                isIntegerLike = true;
                return true;
            case SerializedPropertyType.Float:
                thisValue = PlayerFormulaValue.CreateNumber(property.floatValue);
                return true;
            case SerializedPropertyType.Boolean:
                thisValue = PlayerFormulaValue.CreateBoolean(property.boolValue);
                return true;
            case SerializedPropertyType.String:
                thisValue = PlayerFormulaValue.CreateToken(property.stringValue);
                return true;
            case SerializedPropertyType.Enum:
                thisValue = PlayerFormulaValue.CreateNumber(property.enumValueIndex);
                isIntegerLike = true;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Applies one evaluated formula result back to the serialized target property.
    /// </summary>
    /// <param name="property">Serialized property receiving the evaluated result.</param>
    /// <param name="result">Typed result produced by the formula engine.</param>
    /// <param name="changed">True when the serialized value changed.</param>
    /// <param name="errorMessage">Failure reason when the value type is incompatible.</param>
    /// <returns>True when the value was applied or already matched the target property.</returns>
    public static bool TryApplyFormulaResult(SerializedProperty property,
                                             PlayerFormulaValue result,
                                             out bool changed,
                                             out string errorMessage)
    {
        changed = false;
        errorMessage = string.Empty;

        if (property == null)
        {
            errorMessage = "Target property is missing.";
            return false;
        }

        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                if (result.Type != PlayerFormulaValueType.Number)
                {
                    errorMessage = "Target stat requires a numeric result.";
                    return false;
                }

                int resolvedInteger = Mathf.RoundToInt(result.NumberValue);

                if (property.intValue == resolvedInteger)
                    return true;

                property.intValue = resolvedInteger;
                changed = true;
                return true;
            case SerializedPropertyType.Float:
                if (result.Type != PlayerFormulaValueType.Number)
                {
                    errorMessage = "Target stat requires a numeric result.";
                    return false;
                }

                if (Mathf.Approximately(property.floatValue, result.NumberValue))
                    return true;

                property.floatValue = result.NumberValue;
                changed = true;
                return true;
            case SerializedPropertyType.Boolean:
                if (result.Type != PlayerFormulaValueType.Boolean)
                {
                    errorMessage = "Target stat requires a boolean result.";
                    return false;
                }

                if (property.boolValue == result.BooleanValue)
                    return true;

                property.boolValue = result.BooleanValue;
                changed = true;
                return true;
            case SerializedPropertyType.String:
                if (result.Type != PlayerFormulaValueType.Token)
                {
                    errorMessage = "Target stat requires a token result.";
                    return false;
                }

                if (string.Equals(property.stringValue, result.TokenValue, System.StringComparison.Ordinal))
                    return true;

                property.stringValue = result.TokenValue;
                changed = true;
                return true;
            case SerializedPropertyType.Enum:
                if (result.Type != PlayerFormulaValueType.Number)
                {
                    errorMessage = "Target stat requires an enum-compatible numeric result.";
                    return false;
                }

                string[] enumNames = property.enumNames;

                if (enumNames == null || enumNames.Length <= 0)
                {
                    errorMessage = "Target enum has no available values.";
                    return false;
                }

                int resolvedEnumIndex = Mathf.Clamp(Mathf.RoundToInt(result.NumberValue), 0, enumNames.Length - 1);

                if (property.enumValueIndex == resolvedEnumIndex)
                    return true;

                property.enumValueIndex = resolvedEnumIndex;
                changed = true;
                return true;
            default:
                errorMessage = "Target property type is not supported by Add Scaling.";
                return false;
        }
    }
    #endregion

    #endregion
}
#endif
