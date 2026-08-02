using System;
using System.Collections.Generic;

/// <summary>
/// Exposes a read-only managed projection of authoritative ECS difficulty values to existing formula consumers.
/// </summary>
public static class GameDifficultyRuntimeValueStore
{
    #region Fields
    private static readonly Dictionary<string, float> values =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private static uint version;
    #endregion

    #region Properties
    public static uint Version => version;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Replaces the managed projection after one authoritative ECS difficulty evaluation transaction.
    /// </summary>
    /// <param name="sourceValues">Resolved coefficient values keyed by stable identifier.</param>
    /// <param name="sourceVersion">Authoritative ECS version associated with the value set.</param>
    public static void Replace(IReadOnlyDictionary<string, float> sourceValues, uint sourceVersion)
    {
        values.Clear();

        if (sourceValues != null)
        {
            foreach (KeyValuePair<string, float> entry in sourceValues)
            {
                if (!string.IsNullOrWhiteSpace(entry.Key))
                    values[entry.Key] = entry.Value;
            }
        }

        version = sourceVersion;
    }

    /// <summary>
    /// Clears the managed projection when the authoritative world or run is reset.
    /// </summary>
    public static void Clear()
    {
        values.Clear();
        version = 0u;
    }

    /// <summary>
    /// Resolves one current coefficient value by case-insensitive identifier.
    /// </summary>
    /// <param name="coefficientId">Coefficient identifier requested by a runtime consumer.</param>
    /// <param name="value">Current authoritative projection when found.</param>
    /// <returns>True when the coefficient exists in the latest ECS projection.</returns>
    public static bool TryGetValue(string coefficientId, out float value)
    {
        value = 0f;

        if (string.IsNullOrWhiteSpace(coefficientId))
            return false;

        return values.TryGetValue(coefficientId, out value);
    }

    /// <summary>
    /// Appends all current coefficients as numeric values to a typed formula context.
    /// </summary>
    /// <param name="variableContext">Mutable typed formula context receiving coefficient projections.</param>
    public static void AppendTo(IDictionary<string, PlayerFormulaValue> variableContext)
    {
        if (variableContext == null)
            return;

        foreach (KeyValuePair<string, float> entry in values)
            variableContext[entry.Key] = PlayerFormulaValue.CreateNumber(entry.Value);
    }

    /// <summary>
    /// Appends all current coefficients to a numeric formula context.
    /// </summary>
    /// <param name="variableContext">Mutable numeric formula context receiving coefficient values.</param>
    public static void AppendTo(IDictionary<string, float> variableContext)
    {
        if (variableContext == null)
            return;

        foreach (KeyValuePair<string, float> entry in values)
            variableContext[entry.Key] = entry.Value;
    }
    #endregion

    #endregion
}
