using UnityEngine;

/// <summary>
/// References the default liquid-bar materials used by the HUD runtime when no per-bar override is assigned.
/// </summary>
[CreateAssetMenu(fileName = "HUDLiquidBarMaterialReference", menuName = "UI/HUD Liquid Bar Material Reference")]
public sealed class HUDLiquidBarMaterialReference : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Default Materials")]
    [Tooltip("Default material cloned for the player health syringe bar when no explicit override is assigned on the HUD manager.")]
    [SerializeField] private Material healthMaterial;

    [Tooltip("Default material cloned for the player shield syringe bar when no explicit override is assigned on the HUD manager.")]
    [SerializeField] private Material shieldMaterial;

    [Tooltip("Default material cloned for the player experience bar when no explicit override is assigned on the HUD manager.")]
    [SerializeField] private Material experienceMaterial;
    #endregion

    #endregion

    #region Properties
    public Material HealthMaterial
    {
        get
        {
            return healthMaterial;
        }
    }

    public Material ShieldMaterial
    {
        get
        {
            return shieldMaterial;
        }
    }

    public Material ExperienceMaterial
    {
        get
        {
            return experienceMaterial;
        }
    }
    #endregion

    #region Methods

    #if UNITY_EDITOR
    #region Editor Methods
    /// <summary>
    /// Assigns the default health liquid-bar material referenced by the HUD runtime.
    /// </summary>
    /// <param name="materialValue">Material used for health bars.</param>
    public void SetHealthMaterial(Material materialValue)
    {
        healthMaterial = materialValue;
    }

    /// <summary>
    /// Assigns the default shield liquid-bar material referenced by the HUD runtime.
    /// </summary>
    /// <param name="materialValue">Material used for shield bars.</param>
    public void SetShieldMaterial(Material materialValue)
    {
        shieldMaterial = materialValue;
    }

    /// <summary>
    /// Assigns the default experience liquid-bar material referenced by the HUD runtime.
    /// </summary>
    /// <param name="materialValue">Material used for experience bars.</param>
    public void SetExperienceMaterial(Material materialValue)
    {
        experienceMaterial = materialValue;
    }
    #endregion
    #endif

    #endregion
}
