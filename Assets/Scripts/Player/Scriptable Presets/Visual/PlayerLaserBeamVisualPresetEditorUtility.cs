#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Stores one editor-facing Laser Beam visual preset option resolved from authored player visual presets.
/// </summary>
public readonly struct PlayerLaserBeamVisualPresetEditorOption
{
    #region Fields
    public readonly int StableId;
    public readonly string DisplayName;
    public readonly string FormulaToken;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates one editor-facing option entry.
    /// </summary>
    /// <param name="stableId">Stable numeric preset ID.</param>
    /// <param name="displayName">Designer-facing preset label.</param>
    public PlayerLaserBeamVisualPresetEditorOption(int stableId, string displayName)
    {
        StableId = Mathf.Max(0, stableId);
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? string.Format("Visual Preset {0}", StableId)
            : displayName.Trim();
        FormulaToken = BuildFormulaToken(DisplayName, StableId);
    }

    /// <summary>
    /// Builds the popup label shown to designers inside selector fields.
    /// </summary>
    /// <returns>User-facing popup label.</returns>
    public string BuildDisplayLabel()
    {
        return string.Format("{0} [{1}]", DisplayName, StableId);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds one compact formula token from the designer-facing preset name.
    /// </summary>
    /// <param name="displayName">Designer-facing preset name.</param>
    /// <param name="stableId">Stable numeric preset ID.</param>
    /// <returns>Sanitized token suitable for bracket-based formula constants.</returns>
    private static string BuildFormulaToken(string displayName, int stableId)
    {
        StringBuilder builder = new StringBuilder(displayName.Length);

        for (int characterIndex = 0; characterIndex < displayName.Length; characterIndex++)
        {
            char currentCharacter = displayName[characterIndex];

            if (char.IsLetterOrDigit(currentCharacter) || currentCharacter == '_')
                builder.Append(currentCharacter);
        }

        if (builder.Length > 0)
            return builder.ToString();

        return string.Format("VisualPreset{0}", stableId);
    }
    #endregion

    #endregion
}

/// <summary>
/// Resolves editor-facing Laser Beam visual preset selector options and formula constants from authored visual presets.
/// </summary>
public static class PlayerLaserBeamVisualPresetEditorUtility
{
    #region Constants
    private const string SelectorFieldName = "visualPresetId";
    private const string SelectorParentPathToken = "laserBeam";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Checks whether the provided property should behave as a Laser Beam visual preset selector.
    /// </summary>
    /// <param name="property">Serialized property to inspect.</param>
    /// <returns>True when the property is the Laser Beam visual preset selector field.</returns>
    public static bool IsSelectorProperty(SerializedProperty property)
    {
        if (property == null)
            return false;

        if (property.propertyType != SerializedPropertyType.Integer)
            return false;

        if (!string.Equals(property.name, SelectorFieldName, StringComparison.Ordinal))
            return false;

        return property.propertyPath.IndexOf(SelectorParentPathToken, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Builds the merged selector options currently authored across all PlayerVisualPreset assets in the project.
    /// </summary>
    /// <returns>Stable ID-sorted visual preset options.</returns>
    public static List<PlayerLaserBeamVisualPresetEditorOption> BuildOptions()
    {
        Dictionary<int, PlayerLaserBeamVisualPresetEditorOption> optionsById = new Dictionary<int, PlayerLaserBeamVisualPresetEditorOption>();
        string[] assetGuids = AssetDatabase.FindAssets("t:PlayerVisualPreset");

        for (int assetIndex = 0; assetIndex < assetGuids.Length; assetIndex++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[assetIndex]);
            PlayerVisualPreset playerVisualPreset = AssetDatabase.LoadAssetAtPath<PlayerVisualPreset>(assetPath);

            if (playerVisualPreset == null || playerVisualPreset.LaserBeam == null)
                continue;

            IReadOnlyList<PlayerLaserBeamVisualPresetDefinition> visualPresetDefinitions = playerVisualPreset.LaserBeam.VisualPresets;

            if (visualPresetDefinitions == null)
                continue;

            for (int presetIndex = 0; presetIndex < visualPresetDefinitions.Count; presetIndex++)
            {
                PlayerLaserBeamVisualPresetDefinition visualPresetDefinition = visualPresetDefinitions[presetIndex];

                if (visualPresetDefinition == null)
                    continue;

                int stableId = Mathf.Max(0, visualPresetDefinition.StableId);

                if (optionsById.ContainsKey(stableId))
                    continue;

                optionsById.Add(stableId, new PlayerLaserBeamVisualPresetEditorOption(stableId, visualPresetDefinition.DisplayName));
            }
        }

        if (optionsById.Count <= 0)
        {
            for (int defaultId = 0; defaultId <= 3; defaultId++)
            {
                PlayerLaserBeamVisualDefaultsUtility.ResolveDefaultPreset(defaultId,
                                                                         out string displayName,
                                                                         out Color _,
                                                                         out Color _,
                                                                         out Color _,
                                                                         out Color _);
                optionsById.Add(defaultId, new PlayerLaserBeamVisualPresetEditorOption(defaultId, displayName));
            }
        }

        List<PlayerLaserBeamVisualPresetEditorOption> options = new List<PlayerLaserBeamVisualPresetEditorOption>(optionsById.Values);
        options.Sort((left, right) => left.StableId.CompareTo(right.StableId));
        return options;
    }

    /// <summary>
    /// Resolves the current popup index for one stable preset ID.
    /// </summary>
    /// <param name="options">Available selector options.</param>
    /// <param name="stableId">Current stable preset ID.</param>
    /// <returns>Matching option index, or -1 when the ID is not present.</returns>
    public static int ResolveSelectedIndex(IReadOnlyList<PlayerLaserBeamVisualPresetEditorOption> options, int stableId)
    {
        if (options == null)
            return -1;

        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            if (options[optionIndex].StableId != stableId)
                continue;

            return optionIndex;
        }

        return -1;
    }

    /// <summary>
    /// Builds the helper text that exposes formula tokens for Laser Beam visual preset selectors.
    /// </summary>
    /// <param name="options">Available selector options.</param>
    /// <returns>Compact helper text line for Add Scaling.</returns>
    public static string BuildHelperText(IReadOnlyList<PlayerLaserBeamVisualPresetEditorOption> options)
    {
        if (options == null || options.Count <= 0)
            return "Laser Beam Visual Presets: [VisualPreset0]=0";

        StringBuilder builder = new StringBuilder(options.Count * 20);
        builder.Append("Laser Beam Visual Presets: ");

        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            if (optionIndex > 0)
                builder.Append(", ");

            builder.Append('[');
            builder.Append(options[optionIndex].FormulaToken);
            builder.Append("]=");
            builder.Append(options[optionIndex].StableId);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Normalizes bracket tokens such as [ElectricAzure] into stable numeric IDs for Laser Beam visual preset selector formulas.
    /// </summary>
    /// <param name="formula">Raw designer-authored formula.</param>
    /// <param name="property">Serialized selector property.</param>
    /// <param name="allowedVariables">Known scalable-stat variables that must preserve bracket syntax.</param>
    /// <returns>Formula normalized for validation and bake-time/runtime evaluation.</returns>
    public static string NormalizeFormulaTokens(string formula,
                                                SerializedProperty property,
                                                ISet<string> allowedVariables)
    {
        if (!IsSelectorProperty(property))
            return formula;

        if (string.IsNullOrWhiteSpace(formula))
            return string.Empty;

        List<PlayerLaserBeamVisualPresetEditorOption> options = BuildOptions();

        if (options.Count <= 0)
            return formula;

        StringBuilder normalizedFormulaBuilder = new StringBuilder(formula.Length + 16);
        int parseIndex = 0;

        while (parseIndex < formula.Length)
        {
            int openBracketIndex = formula.IndexOf('[', parseIndex);

            if (openBracketIndex < 0)
            {
                normalizedFormulaBuilder.Append(formula.Substring(parseIndex));
                break;
            }

            if (openBracketIndex > parseIndex)
                normalizedFormulaBuilder.Append(formula.Substring(parseIndex, openBracketIndex - parseIndex));

            int closeBracketIndex = formula.IndexOf(']', openBracketIndex + 1);

            if (closeBracketIndex < 0)
            {
                normalizedFormulaBuilder.Append(formula.Substring(openBracketIndex));
                break;
            }

            string token = formula.Substring(openBracketIndex + 1, closeBracketIndex - openBracketIndex - 1).Trim();

            if (string.Equals(token, PlayerScalableStatNameUtility.ReservedThisName, StringComparison.OrdinalIgnoreCase) ||
                allowedVariables != null && allowedVariables.Contains(token) ||
                !TryResolvePresetId(token, options, out int stableId))
            {
                normalizedFormulaBuilder.Append(formula.Substring(openBracketIndex, closeBracketIndex - openBracketIndex + 1));
            }
            else
            {
                normalizedFormulaBuilder.Append(stableId);
            }

            parseIndex = closeBracketIndex + 1;
        }

        return normalizedFormulaBuilder.ToString();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves one bracket token into a stable Laser Beam visual preset ID.
    /// </summary>
    /// <param name="token">Raw bracket token without surrounding brackets.</param>
    /// <param name="options">Available visual preset options.</param>
    /// <param name="stableId">Resolved stable preset ID when successful.</param>
    /// <returns>True when the token maps to a valid visual preset ID.</returns>
    private static bool TryResolvePresetId(string token,
                                           IReadOnlyList<PlayerLaserBeamVisualPresetEditorOption> options,
                                           out int stableId)
    {
        stableId = 0;

        if (string.IsNullOrWhiteSpace(token) || options == null)
            return false;

        if (int.TryParse(token, out int parsedStableId))
        {
            stableId = Mathf.Max(0, parsedStableId);
            return true;
        }

        if (TryResolveLegacyPresetId(token, out stableId))
            return true;

        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            PlayerLaserBeamVisualPresetEditorOption option = options[optionIndex];

            if (string.Equals(option.FormulaToken, token, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(option.DisplayName, token, StringComparison.OrdinalIgnoreCase))
            {
                stableId = option.StableId;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves legacy palette enum names into their compatible stable visual preset IDs.
    /// </summary>
    /// <param name="token">Legacy palette enum token.</param>
    /// <param name="stableId">Resolved stable preset ID when successful.</param>
    /// <returns>True when the token matches one known legacy palette name.</returns>
    private static bool TryResolveLegacyPresetId(string token, out int stableId)
    {
        stableId = 0;

        switch (token)
        {
            case "AntibioticBlue":
                stableId = 0;
                return true;
            case "SterileMint":
                stableId = 1;
                return true;
            case "ToxicLime":
                stableId = 2;
                return true;
            case "PlasmaAmber":
                stableId = 3;
                return true;
            default:
                return false;
        }
    }
    #endregion

    #endregion
}
#endif
