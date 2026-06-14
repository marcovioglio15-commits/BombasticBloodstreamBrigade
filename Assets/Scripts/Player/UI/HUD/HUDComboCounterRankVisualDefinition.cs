using System;
using UnityEngine;

/// <summary>
/// Stores one legacy combo-rank HUD visual entry kept only to preserve backward-compatible scene data.
/// none.
/// </summary>
[Serializable]
public sealed class HUDComboCounterRankVisualDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Legacy combo-rank identifier used only to resolve hidden fallback visuals from older scenes.")]
    [SerializeField] private string rankId;

    [Tooltip("Legacy badge sprite fallback used when no preset-owned rank sprite is available.")]
    [SerializeField] private Sprite badgeSprite;

    [Tooltip("Legacy badge tint fallback used when no preset-owned rank tint is available.")]
    [SerializeField] private Color badgeTint = Color.white;

    [Tooltip("Legacy rank-label color fallback used when no preset-owned rank label color is available.")]
    [SerializeField] private Color rankTextColor = Color.white;

    [Tooltip("Legacy combo-value color fallback used when no preset-owned combo value color is available.")]
    [SerializeField] private Color comboValueTextColor = Color.white;

    [Tooltip("Legacy progress-fill color fallback used when no preset-owned progress fill color is available.")]
    [SerializeField] private Color progressFillColor = Color.white;

    [Tooltip("Legacy progress-background color fallback used when no preset-owned progress background color is available.")]
    [SerializeField] private Color progressBackgroundColor = Color.white;
    #endregion

    #endregion

    #region Properties
    public string RankId
    {
        get
        {
            return rankId;
        }
    }

    public Sprite BadgeSprite
    {
        get
        {
            return badgeSprite;
        }
    }

    public Color BadgeTint
    {
        get
        {
            return badgeTint;
        }
    }

    public Color RankTextColor
    {
        get
        {
            return rankTextColor;
        }
    }

    public Color ComboValueTextColor
    {
        get
        {
            return comboValueTextColor;
        }
    }

    public Color ProgressFillColor
    {
        get
        {
            return progressFillColor;
        }
    }

    public Color ProgressBackgroundColor
    {
        get
        {
            return progressBackgroundColor;
        }
    }
    #endregion
}
