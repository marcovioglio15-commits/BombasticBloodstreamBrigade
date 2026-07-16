using System;
using UnityEngine;

/// <summary>
/// Stores the Core Movement selection assembled inside one shared enemy pattern.
/// </summary>
[Serializable]
public sealed class EnemyPatternCoreMovementAssembly
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Core Movement module binding resolved from Core Movement definitions.")]
    [SerializeField] private EnemyPatternModuleBinding binding = new EnemyPatternModuleBinding();
    #endregion

    #endregion

    #region Properties
    public EnemyPatternModuleBinding Binding
    {
        get
        {
            return binding;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures the Core Movement assembly always owns one binding instance and keeps it enabled.
    /// </summary>
    public void Validate()
    {
        if (binding == null)
            binding = new EnemyPatternModuleBinding();

        binding.Validate();

        if (!binding.IsEnabled)
            binding.Configure(binding.ModuleId, true);
    }
    #endregion

    #endregion
}
