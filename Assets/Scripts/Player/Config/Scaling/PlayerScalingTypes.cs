using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines the scalable stat kinds supported by the unified formula system.
/// </summary>
public enum PlayerScalableStatType : byte
{
    Float = 0,
    Integer = 1,
    Boolean = 2,
    Token = 3,
    Unsigned = 4
}

/// <summary>
/// Stores one scalable stat entry editable by designers in progression presets.
/// </summary>
[Serializable]
public sealed class PlayerScalableStatDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Scalable stat identifier used in formulas with [StatName] syntax.")]
    [SerializeField] private string statName = "newStat";

    [Tooltip("Runtime data type used by this scalable stat.")]
    [SerializeField] private PlayerScalableStatType statType = PlayerScalableStatType.Float;

    [Tooltip("Default numeric value used when the stat type is Float, Integer or Unsigned.")]
    [SerializeField] private float defaultValue;

    [Tooltip("Minimum numeric runtime value allowed when the stat type is Float, Integer or Unsigned.")]
    [SerializeField] private float minimumValue = PlayerScalableStatClampUtility.DefaultMinimumValue;

    [Tooltip("Maximum numeric runtime value allowed when the stat type is Float, Integer or Unsigned.")]
    [SerializeField] private float maximumValue = PlayerScalableStatClampUtility.DefaultMaximumValue;

    [Tooltip("Default boolean value used when the stat type is Boolean.")]
    [SerializeField] private bool defaultBooleanValue;

    [Tooltip("Default token value used when the stat type is Token.")]
    [SerializeField] private string defaultTokenValue = string.Empty;
    #endregion

    #endregion

    #region Properties
    public string StatName
    {
        get
        {
            return statName;
        }
    }

    public PlayerScalableStatType StatType
    {
        get
        {
            return statType;
        }
    }

    public float DefaultValue
    {
        get
        {
            return defaultValue;
        }
    }

    public float MinimumValue
    {
        get
        {
            return minimumValue;
        }
    }

    public float MaximumValue
    {
        get
        {
            return maximumValue;
        }
    }

    public bool DefaultBooleanValue
    {
        get
        {
            return defaultBooleanValue;
        }
    }

    public string DefaultTokenValue
    {
        get
        {
            return defaultTokenValue;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Sanitizes the scalable stat entry while preserving designer-authored values.
    /// </summary>
    /// <param name="fallbackName">Fallback name used when the current stat name is invalid.</param>
    /// <returns>True when at least one field was modified during validation.</returns>
    public bool Validate(string fallbackName)
    {
        bool changed = false;
        string sanitizedName = PlayerScalableStatNameUtility.Sanitize(statName, fallbackName);

        if (!string.Equals(statName, sanitizedName, StringComparison.Ordinal))
        {
            statName = sanitizedName;
            changed = true;
        }

        string sanitizedTokenValue = string.IsNullOrWhiteSpace(defaultTokenValue)
            ? string.Empty
            : defaultTokenValue.Trim();

        if (!string.Equals(defaultTokenValue, sanitizedTokenValue, StringComparison.Ordinal))
        {
            defaultTokenValue = sanitizedTokenValue;
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Resolves the typed runtime default value of this scalable stat.
    /// </summary>
    /// <returns>Runtime-ready typed default value.</returns>
    public PlayerFormulaValue ResolveRuntimeDefaultFormulaValue()
    {
        switch (statType)
        {
            case PlayerScalableStatType.Integer:
            case PlayerScalableStatType.Unsigned:
            case PlayerScalableStatType.Float:
                return PlayerFormulaValue.CreateNumber(PlayerScalableStatClampUtility.ResolveNormalizedValue(statType,
                                                                                                            minimumValue,
                                                                                                            maximumValue,
                                                                                                            defaultValue));
            case PlayerScalableStatType.Boolean:
                return PlayerFormulaValue.CreateBoolean(defaultBooleanValue);
            case PlayerScalableStatType.Token:
                return PlayerFormulaValue.CreateToken(defaultTokenValue);
            default:
                return PlayerFormulaValue.CreateInvalid();
        }
    }

    /// <summary>
    /// Resolves the numeric projection of the default value for legacy numeric-only code paths.
    /// </summary>
    /// <returns>Numeric default projection.</returns>
    public float ResolveRuntimeDefaultValue()
    {
        PlayerFormulaValue defaultFormulaValue = ResolveRuntimeDefaultFormulaValue();
        return PlayerScalableStatTypeUtility.ResolveNumericProjection(statType, defaultFormulaValue);
    }
    #endregion

    #region Setup
    /// <summary>
    /// Assigns the numeric configuration of the scalable stat.
    /// </summary>
    /// <param name="statNameValue">Formula variable name.</param>
    /// <param name="statTypeValue">Runtime scalable stat type.</param>
    /// <param name="defaultValueValue">Default numeric value.</param>
    /// <returns>Void.</returns>
    public void Configure(string statNameValue, PlayerScalableStatType statTypeValue, float defaultValueValue)
    {
        Configure(statNameValue,
                  statTypeValue,
                  defaultValueValue,
                  PlayerScalableStatClampUtility.DefaultMinimumValue,
                  PlayerScalableStatClampUtility.DefaultMaximumValue);
    }

    /// <summary>
    /// Assigns the numeric configuration of the scalable stat including numeric clamp bounds.
    /// </summary>
    /// <param name="statNameValue">Formula variable name.</param>
    /// <param name="statTypeValue">Runtime scalable stat type.</param>
    /// <param name="defaultValueValue">Default numeric value.</param>
    /// <param name="minimumValueValue">Minimum numeric clamp value.</param>
    /// <param name="maximumValueValue">Maximum numeric clamp value.</param>
    /// <returns>Void.</returns>
    public void Configure(string statNameValue,
                          PlayerScalableStatType statTypeValue,
                          float defaultValueValue,
                          float minimumValueValue,
                          float maximumValueValue)
    {
        statName = statNameValue;
        statType = statTypeValue;
        defaultValue = defaultValueValue;
        minimumValue = minimumValueValue;
        maximumValue = maximumValueValue;
        defaultBooleanValue = false;
        defaultTokenValue = string.Empty;
    }

    /// <summary>
    /// Assigns the boolean configuration of the scalable stat.
    /// </summary>
    /// <param name="statNameValue">Formula variable name.</param>
    /// <param name="defaultBooleanValueValue">Default boolean value.</param>
    /// <returns>Void.</returns>
    public void ConfigureBoolean(string statNameValue, bool defaultBooleanValueValue)
    {
        statName = statNameValue;
        statType = PlayerScalableStatType.Boolean;
        defaultValue = defaultBooleanValueValue ? 1f : 0f;
        minimumValue = 0f;
        maximumValue = 1f;
        defaultBooleanValue = defaultBooleanValueValue;
        defaultTokenValue = string.Empty;
    }

    /// <summary>
    /// Assigns the token configuration of the scalable stat.
    /// </summary>
    /// <param name="statNameValue">Formula variable name.</param>
    /// <param name="defaultTokenValueValue">Default token value.</param>
    /// <returns>Void.</returns>
    public void ConfigureToken(string statNameValue, string defaultTokenValueValue)
    {
        statName = statNameValue;
        statType = PlayerScalableStatType.Token;
        defaultValue = 0f;
        minimumValue = 0f;
        maximumValue = 0f;
        defaultBooleanValue = false;
        defaultTokenValue = string.IsNullOrWhiteSpace(defaultTokenValueValue)
            ? string.Empty
            : defaultTokenValueValue.Trim();
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one optional scaling rule bound to a specific numeric stat key.
/// </summary>
[Serializable]
public sealed class PlayerStatScalingRule
{
    #region Constants
    private const float DebugColorDefaultRed = 1f;
    private const float DebugColorDefaultGreen = 0.92156863f;
    private const float DebugColorDefaultBlue = 0.015686275f;
    private const float DebugColorDefaultAlpha = 1f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Stable stat key that identifies which serialized property this scaling rule targets.")]
    [SerializeField] private string statKey;

    [Tooltip("When enabled, the Formula field is evaluated and applied to the target numeric stat.")]
    [SerializeField] private bool addScaling;

    [Tooltip("Unified formula expression using [this], scalable stat variables and typed conditions.")]
    [SerializeField] private string formula;

    [Tooltip("When enabled, editor-only runtime debug logs print this scaling rule when tracked values change.")]
    [SerializeField] private bool debugInConsole;

    [Tooltip("Editor-only debug color used by this specific scaling rule log line.")]
    [SerializeField] private Color debugColor = new Color(DebugColorDefaultRed,
                                                          DebugColorDefaultGreen,
                                                          DebugColorDefaultBlue,
                                                          DebugColorDefaultAlpha);
    #endregion

    #endregion

    #region Properties
    public string StatKey
    {
        get
        {
            return statKey;
        }
    }

    public bool AddScaling
    {
        get
        {
            return addScaling;
        }
    }

    public string Formula
    {
        get
        {
            return formula;
        }
    }

    public bool DebugInConsole
    {
        get
        {
            return debugInConsole;
        }
    }

    public Color DebugColor
    {
        get
        {
            return debugColor;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Returns the default debug color used by scaling rules.
    /// </summary>
    /// <returns>Default opaque yellow debug color.</returns>
    public static Color GetDefaultDebugColor()
    {
        return new Color(DebugColorDefaultRed,
                         DebugColorDefaultGreen,
                         DebugColorDefaultBlue,
                         DebugColorDefaultAlpha);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Normalizes rule storage fields to avoid invalid serialized state.
    /// </summary>
    /// <returns>True when at least one field was modified during validation.</returns>
    public bool Validate()
    {
        bool changed = false;

        if (statKey == null)
        {
            statKey = string.Empty;
            changed = true;
        }

        if (formula == null)
        {
            formula = string.Empty;
            changed = true;
        }

        if (!addScaling && debugInConsole)
        {
            debugInConsole = false;
            changed = true;
        }

        Color sanitizedDebugColor = SanitizeDebugColor(debugColor);

        if (!AreColorsApproximatelyEqual(debugColor, sanitizedDebugColor))
        {
            debugColor = sanitizedDebugColor;
            changed = true;
        }

        return changed;
    }
    #endregion

    #region Setup
    /// <summary>
    /// Assigns all serialized fields for this scaling rule in one call.
    /// </summary>
    /// <param name="statKeyValue">Stable key of the target serialized property.</param>
    /// <param name="addScalingValue">Whether scaling is enabled.</param>
    /// <param name="formulaValue">Formula text evaluated when scaling is enabled.</param>
    /// <returns>Void.</returns>
    public void Configure(string statKeyValue, bool addScalingValue, string formulaValue)
    {
        Configure(statKeyValue, addScalingValue, formulaValue, false, GetDefaultDebugColor());
    }

    /// <summary>
    /// Assigns all serialized fields for this scaling rule in one call.
    /// </summary>
    /// <param name="statKeyValue">Stable key of the target serialized property.</param>
    /// <param name="addScalingValue">Whether scaling is enabled.</param>
    /// <param name="formulaValue">Formula text evaluated when scaling is enabled.</param>
    /// <param name="debugInConsoleValue">Whether editor-only runtime logging is enabled.</param>
    /// <returns>Void.</returns>
    public void Configure(string statKeyValue, bool addScalingValue, string formulaValue, bool debugInConsoleValue)
    {
        Configure(statKeyValue, addScalingValue, formulaValue, debugInConsoleValue, GetDefaultDebugColor());
    }

    /// <summary>
    /// Assigns all serialized fields for this scaling rule in one call.
    /// </summary>
    /// <param name="statKeyValue">Stable key of the target serialized property.</param>
    /// <param name="addScalingValue">Whether scaling is enabled.</param>
    /// <param name="formulaValue">Formula text evaluated when scaling is enabled.</param>
    /// <param name="debugInConsoleValue">Whether editor-only runtime logging is enabled.</param>
    /// <param name="debugColorValue">Editor-only debug color used by this rule.</param>
    /// <returns>Void.</returns>
    public void Configure(string statKeyValue,
                          bool addScalingValue,
                          string formulaValue,
                          bool debugInConsoleValue,
                          Color debugColorValue)
    {
        statKey = statKeyValue;
        addScaling = addScalingValue;
        formula = formulaValue;
        debugInConsole = debugInConsoleValue;
        debugColor = SanitizeDebugColor(debugColorValue);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Sanitizes debug color channels while preserving a visible alpha.
    /// </summary>
    /// <param name="sourceColor">Raw serialized color.</param>
    /// <returns>Sanitized opaque color.</returns>
    private static Color SanitizeDebugColor(Color sourceColor)
    {
        if (sourceColor.a <= 0.0001f)
            return GetDefaultDebugColor();

        return new Color(Mathf.Clamp01(sourceColor.r),
                         Mathf.Clamp01(sourceColor.g),
                         Mathf.Clamp01(sourceColor.b),
                         DebugColorDefaultAlpha);
    }

    /// <summary>
    /// Compares colors using a small tolerance to avoid noisy validation writes.
    /// </summary>
    /// <param name="leftColor">First color.</param>
    /// <param name="rightColor">Second color.</param>
    /// <returns>True when the colors are approximately equal.</returns>
    private static bool AreColorsApproximatelyEqual(Color leftColor, Color rightColor)
    {
        if (Mathf.Abs(leftColor.r - rightColor.r) > 0.0001f)
            return false;

        if (Mathf.Abs(leftColor.g - rightColor.g) > 0.0001f)
            return false;

        if (Mathf.Abs(leftColor.b - rightColor.b) > 0.0001f)
            return false;

        return Mathf.Abs(leftColor.a - rightColor.a) <= 0.0001f;
    }
    #endregion

    #endregion
}

/// <summary>
/// Provides common validation helpers for scalable stat names used in formulas and preset data.
/// </summary>
public static class PlayerScalableStatNameUtility
{
    #region Constants
    public const string ReservedThisName = "this";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Normalizes one scalable stat name using project naming rules.
    /// </summary>
    /// <param name="inputName">Raw user-entered stat name.</param>
    /// <param name="fallbackName">Fallback name used when the input is invalid.</param>
    /// <returns>Sanitized stat name that is never null or whitespace.</returns>
    public static string Sanitize(string inputName, string fallbackName)
    {
        string trimmedName = string.IsNullOrWhiteSpace(inputName) ? string.Empty : inputName.Trim();
        string resolvedFallback = string.IsNullOrWhiteSpace(fallbackName) ? "newStat" : fallbackName.Trim();

        if (IsValid(trimmedName))
            return trimmedName;

        if (IsValid(resolvedFallback))
            return resolvedFallback;

        return "newStat";
    }

    /// <summary>
    /// Checks whether a scalable stat name satisfies allowed syntax and reserved-word restrictions.
    /// </summary>
    /// <param name="name">Candidate name to validate.</param>
    /// <returns>True when the name is valid.</returns>
    public static bool IsValid(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (string.Equals(name, ReservedThisName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!char.IsLetter(name[0]) && name[0] != '_')
            return false;

        for (int index = 0; index < name.Length; index++)
        {
            char currentCharacter = name[index];

            if (char.IsLetterOrDigit(currentCharacter))
                continue;

            if (currentCharacter == '_')
                continue;

            return false;
        }

        return true;
    }

    /// <summary>
    /// Ensures scalable stat names are unique using case-insensitive comparison.
    /// </summary>
    /// <param name="definitions">Definitions to inspect.</param>
    /// <returns>True when all names are unique.</returns>
    public static bool AreUnique(IReadOnlyList<PlayerScalableStatDefinition> definitions)
    {
        if (definitions == null)
            return true;

        HashSet<string> visitedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < definitions.Count; index++)
        {
            PlayerScalableStatDefinition definition = definitions[index];

            if (definition == null || string.IsNullOrWhiteSpace(definition.StatName))
                continue;

            if (!visitedNames.Add(definition.StatName))
                return false;
        }

        return true;
    }
    #endregion

    #endregion
}

/// <summary>
/// Provides type conversion helpers shared by authoring, runtime and editor formula systems.
/// </summary>
public static class PlayerScalableStatTypeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Converts one scalable stat type to the matching formula value type.
    /// </summary>
    /// <param name="statType">Scalable stat type.</param>
    /// <returns>Matching formula value type.</returns>
    public static PlayerFormulaValueType ToFormulaValueType(PlayerScalableStatType statType)
    {
        switch (statType)
        {
            case PlayerScalableStatType.Float:
            case PlayerScalableStatType.Integer:
            case PlayerScalableStatType.Unsigned:
                return PlayerFormulaValueType.Number;
            case PlayerScalableStatType.Boolean:
                return PlayerFormulaValueType.Boolean;
            case PlayerScalableStatType.Token:
                return PlayerFormulaValueType.Token;
            default:
                return PlayerFormulaValueType.Invalid;
        }
    }

    /// <summary>
    /// Resolves the numeric projection of one typed scalable stat value.
    /// </summary>
    /// <param name="statType">Source scalable stat type.</param>
    /// <param name="value">Typed source value.</param>
    /// <returns>Numeric projection used by legacy numeric-only systems.</returns>
    public static float ResolveNumericProjection(PlayerScalableStatType statType, PlayerFormulaValue value)
    {
        switch (statType)
        {
            case PlayerScalableStatType.Integer:
            case PlayerScalableStatType.Unsigned:
            case PlayerScalableStatType.Float:
                return value.Type == PlayerFormulaValueType.Number ? value.NumberValue : 0f;
            case PlayerScalableStatType.Boolean:
                return value.Type == PlayerFormulaValueType.Boolean && value.BooleanValue ? 1f : 0f;
            case PlayerScalableStatType.Token:
                return 0f;
            default:
                return 0f;
        }
    }

    /// <summary>
    /// Builds the compact editor-facing label used to describe one scalable-stat type in formula helpers.
    /// </summary>
    /// <param name="statType">Source scalable-stat type.</param>
    /// <returns>Short human-readable type label.</returns>
    public static string BuildDisplayLabel(PlayerScalableStatType statType)
    {
        switch (statType)
        {
            case PlayerScalableStatType.Float:
                return "Float";
            case PlayerScalableStatType.Integer:
                return "Integer";
            case PlayerScalableStatType.Unsigned:
                return "Unsigned";
            case PlayerScalableStatType.Boolean:
                return "Boolean";
            case PlayerScalableStatType.Token:
                return "Token";
            default:
                return "Invalid";
        }
    }
    #endregion

    #endregion
}
