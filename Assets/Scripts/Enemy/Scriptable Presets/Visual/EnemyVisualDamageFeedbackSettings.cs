using System;
using UnityEngine;

/// <summary>
/// Stores short hit-flash presentation tuning used when this enemy receives damage.
/// </summary>
[Serializable]
public sealed class EnemyVisualDamageFeedbackSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Tint color applied during the brief damage flash.")]
    [SerializeField] private Color flashColor = new Color(1f, 0.15f, 0.15f, 1f);

    [Tooltip("Flash duration in seconds. Use small values for a 1-3 frame reaction.")]
    [SerializeField] private float flashDurationSeconds = 0.06f;

    [Tooltip("Maximum overlay strength reached immediately after a valid hit.")]
    [SerializeField] private float flashMaximumBlend = 0.85f;
    #endregion

    #endregion

    #region Properties
    public Color FlashColor
    {
        get
        {
            return flashColor;
        }
    }

    public float FlashDurationSeconds
    {
        get
        {
            return flashDurationSeconds;
        }
    }

    public float FlashMaximumBlend
    {
        get
        {
            return flashMaximumBlend;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Sanitizes damage flash values after asset edits.
    /// </summary>
    public void Validate()
    {
        flashColor.a = Mathf.Clamp01(flashColor.a);

        if (flashDurationSeconds < 0f)
            flashDurationSeconds = 0f;

        if (flashMaximumBlend < 0f)
            flashMaximumBlend = 0f;

        if (flashMaximumBlend > 1f)
            flashMaximumBlend = 1f;
    }
    #endregion

    #endregion
}
