using System;
using UnityEngine;

/// <summary>
/// Stores outline presentation settings applied to enemy renderers across companion and GPU-baked paths.
/// </summary>
[Serializable]
public sealed class EnemyVisualOutlineSettings
{
    #region Constants
    private const float MinimumOutlineThickness = 0f;
    private const float MaximumOutlineThickness = 25f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("When enabled, compatible enemy renderers receive outline property overrides from this preset.")]
    [SerializeField] private bool enableOutline = true;

    [Tooltip("Outline thickness written to compatible enemy materials exposing _OutlineThickness. Enemy runtime supports values up to 25 for stronger silhouettes on dense crowds.")]
    [Range(MinimumOutlineThickness, MaximumOutlineThickness)]
    [SerializeField] private float outlineThickness = 1f;

    [Tooltip("Outline color written to compatible enemy materials exposing _OutlineColor.")]
    [SerializeField] private Color outlineColor = Color.black;
    #endregion

    #endregion

    #region Properties
    public bool EnableOutline
    {
        get
        {
            return enableOutline;
        }
    }

    public float OutlineThickness
    {
        get
        {
            return outlineThickness;
        }
    }

    public Color OutlineColor
    {
        get
        {
            return outlineColor;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates outline authored values after inspector edits.
    /// </summary>
    public void Validate()
    {
        outlineColor.a = Mathf.Clamp01(outlineColor.a);
    }
    #endregion

    #endregion
}
