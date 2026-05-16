using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Stores generic visual feedback authored for offensive enemy behaviour engagements.
/// </summary>
[Serializable]
public sealed class EnemyOffensiveEngagementFeedbackSettings
{
    #region Constants
    private const float DefaultColorBlendLeadTimeSeconds = 0.3f;
    private const float DefaultColorBlendFadeOutSeconds = 0.18f;
    private const float DefaultColorBlendMaximumBlend = 0.7f;
    private const float DefaultBillboardLeadTimeSeconds = 0.3f;
    private const float DefaultBillboardBaseScale = 0.75f;
    private const float DefaultBillboardPulseScaleMultiplier = 1.22f;
    private const float DefaultBillboardPulseExpandDurationSeconds = 0.12f;
    private const float DefaultBillboardPulseContractDurationSeconds = 0.14f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Color Blend")]
    [Tooltip("When enabled, the offensive engagement warning blends enemy renderers toward the configured color before the behaviour commits.")]
    [FormerlySerializedAs("enableAimPulse")]
    [SerializeField] private bool enableColorBlend = true;

    [Tooltip("Tint color applied while the offensive engagement warning blend ramps up.")]
    [FormerlySerializedAs("aimPulseColor")]
    [SerializeField] private Color colorBlendColor = new Color(0.35f, 1f, 0.95f, 1f);

    [Tooltip("Seconds before the behaviour commit when the color blend warning may begin. Shooter idle windows use this directly, while dash telegraphs clamp it to the authored dash aim duration.")]
    [FormerlySerializedAs("aimPulseLeadTimeSeconds")]
    [SerializeField] private float colorBlendLeadTimeSeconds = DefaultColorBlendLeadTimeSeconds;

    [Tooltip("Seconds used to softly fade the color blend after the current offensive warning loses priority.")]
    [FormerlySerializedAs("aimPulseFadeOutSeconds")]
    [SerializeField] private float colorBlendFadeOutSeconds = DefaultColorBlendFadeOutSeconds;

    [Tooltip("Maximum overlay strength reached when the color blend warning finishes its current ramp.")]
    [FormerlySerializedAs("aimPulseMaximumBlend")]
    [SerializeField] private float colorBlendMaximumBlend = DefaultColorBlendMaximumBlend;

    [Header("Billboard")]
    [Tooltip("When enabled, the offensive engagement warning displays a billboard sprite above the enemy before the behaviour commits.")]
    [SerializeField] private bool enableBillboard = true;

    [Tooltip("Sprite rendered by the offensive engagement billboard. Leave empty to suppress the billboard while still allowing color blend feedback.")]
    [SerializeField] private Sprite billboardSprite;

    [Tooltip("Tint multiplied with the billboard sprite color while the offensive engagement warning is active.")]
    [SerializeField] private Color billboardColor = Color.white;

    [Tooltip("World-space offset from the enemy pivot where the offensive engagement billboard is rendered.")]
    [SerializeField] private Vector3 billboardLocalOffset = new Vector3(0f, 1.85f, 0f);

    [Tooltip("Seconds before the behaviour commit when the billboard may appear. Shooter idle windows use this directly, while dash telegraphs clamp it to the authored dash aim duration.")]
    [SerializeField] private float billboardLeadTimeSeconds = DefaultBillboardLeadTimeSeconds;

    [Tooltip("Base uniform world scale applied to the billboard when no pulse is active.")]
    [SerializeField] private float billboardBaseScale = DefaultBillboardBaseScale;

    [Tooltip("Peak scale multiplier reached at the end of each billboard pulse expansion. Use values above 1 for a growth pulse.")]
    [SerializeField] private float billboardPulseScaleMultiplier = DefaultBillboardPulseScaleMultiplier;

    [Tooltip("Seconds spent linearly scaling the billboard from its base scale to the configured pulse peak.")]
    [SerializeField] private float billboardPulseExpandDurationSeconds = DefaultBillboardPulseExpandDurationSeconds;

    [Tooltip("Seconds spent linearly scaling the billboard back from the pulse peak to its base scale.")]
    [SerializeField] private float billboardPulseContractDurationSeconds = DefaultBillboardPulseContractDurationSeconds;
    #endregion

    #endregion

    #region Properties
    public bool EnableColorBlend
    {
        get
        {
            return enableColorBlend;
        }
    }

    public Color ColorBlendColor
    {
        get
        {
            return colorBlendColor;
        }
    }

    public float ColorBlendLeadTimeSeconds
    {
        get
        {
            return colorBlendLeadTimeSeconds;
        }
    }

    public float ColorBlendFadeOutSeconds
    {
        get
        {
            return colorBlendFadeOutSeconds;
        }
    }

    public float ColorBlendMaximumBlend
    {
        get
        {
            return colorBlendMaximumBlend;
        }
    }

    public bool EnableBillboard
    {
        get
        {
            return enableBillboard;
        }
    }

    public Sprite BillboardSprite
    {
        get
        {
            return billboardSprite;
        }
    }

    public Color BillboardColor
    {
        get
        {
            return billboardColor;
        }
    }

    public Vector3 BillboardLocalOffset
    {
        get
        {
            return billboardLocalOffset;
        }
    }

    public float BillboardLeadTimeSeconds
    {
        get
        {
            return billboardLeadTimeSeconds;
        }
    }

    public float BillboardBaseScale
    {
        get
        {
            return billboardBaseScale;
        }
    }

    public float BillboardPulseScaleMultiplier
    {
        get
        {
            return billboardPulseScaleMultiplier;
        }
    }

    public float BillboardPulseExpandDurationSeconds
    {
        get
        {
            return billboardPulseExpandDurationSeconds;
        }
    }

    public float BillboardPulseContractDurationSeconds
    {
        get
        {
            return billboardPulseContractDurationSeconds;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Keeps the serialized feedback block structurally valid after asset edits without mutating authored numeric tuning.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}
