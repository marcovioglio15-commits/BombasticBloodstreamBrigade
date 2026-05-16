using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores one scalable-stat assignment formula applied immediately when the owning power-up is acquired.
/// </summary>
[Serializable]
public sealed class PowerUpCharacterTuningFormulaData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Assignment formula applied on acquisition, for example [damage] = [damage] + 1.")]
    [SerializeField] private string formula = string.Empty;
    #endregion

    #endregion

    #region Properties
    public string Formula
    {
        get
        {
            return formula;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Assigns the serialized acquisition formula.
    /// </summary>
    /// <param name="formulaValue">Assignment formula stored on this entry.</param>
    public void Configure(string formulaValue)
    {
        formula = formulaValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Normalizes the stored formula string to avoid null serialization state.
    /// none.
    /// </summary>
    public void Validate()
    {
        if (formula == null)
            formula = string.Empty;

        formula = formula.Trim();
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores all scalable-stat assignments executed once when the owning power-up is obtained.
/// </summary>
[Serializable]
public sealed class PowerUpCharacterTuningModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Ordered acquisition formulas applied one after another when this power-up is obtained.")]
    [SerializeField] private List<PowerUpCharacterTuningFormulaData> formulas = new List<PowerUpCharacterTuningFormulaData>();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<PowerUpCharacterTuningFormulaData> Formulas
    {
        get
        {
            return formulas;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Replaces the stored acquisition-formula list with the provided entries.
    /// </summary>
    /// <param name="formulasValue">New ordered list of acquisition formulas.</param>
    public void Configure(List<PowerUpCharacterTuningFormulaData> formulasValue)
    {
        formulas = formulasValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Sanitizes the nested acquisition formulas and guarantees a non-null list.
    /// none.
    /// </summary>
    public void Validate()
    {
        if (formulas == null)
            formulas = new List<PowerUpCharacterTuningFormulaData>();

        for (int formulaIndex = 0; formulaIndex < formulas.Count; formulaIndex++)
        {
            PowerUpCharacterTuningFormulaData formulaData = formulas[formulaIndex];

            if (formulaData == null)
            {
                formulaData = new PowerUpCharacterTuningFormulaData();
                formulas[formulaIndex] = formulaData;
            }

            formulaData.Validate();
        }
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores the maximum number of times a Character Tuning power-up can be acquired from milestone rolls.
/// </summary>
[Serializable]
public sealed class PowerUpStackableModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Maximum total acquisitions allowed for this power-up across the run, including the first pickup.")]
    [SerializeField] private int maxAcquisitions = 2;
    #endregion

    #endregion

    #region Properties
    public int MaxAcquisitions
    {
        get
        {
            return maxAcquisitions;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Assigns the total acquisition cap exposed by the Stackable module.
    /// </summary>
    /// <param name="maxAcquisitionsValue">Total number of allowed acquisitions.</param>
    public void Configure(int maxAcquisitionsValue)
    {
        maxAcquisitions = maxAcquisitionsValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Clamps the total acquisition cap to a meaningful stackable range.
    /// none.
    /// </summary>
    public void Validate()
    {
        if (maxAcquisitions < 2)
            maxAcquisitions = 2;
    }
    #endregion

    #endregion
}
