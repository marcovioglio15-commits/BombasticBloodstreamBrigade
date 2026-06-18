using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Selects whether a growth sequence step is rendered as a sprite pair or a TMP text state pair.
/// </summary>
public enum PlayerGrowthSequenceHudPresentationMode : byte
{
    Text = 0,
    Image = 1
}

/// <summary>
/// Stores one text visual state for a growth sequence step.
/// </summary>
[Serializable]
public sealed class PlayerGrowthSequenceHudTextStateSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Optional TMP font asset used by this growth step state. When empty, the scene label keeps its authored font.")]
    [SerializeField] private TMP_FontAsset fontAsset;

    [Tooltip("Font size used by this growth step state. Set 0 to keep the scene label authored size.")]
    [SerializeField] private float fontSize = 28f;

    [Tooltip("Text color used by this growth step state.")]
    [SerializeField] private Color color = Color.white;

    [Tooltip("Outline color used by this growth step state.")]
    [SerializeField] private Color outlineColor = Color.black;

    [Tooltip("TMP outline width used by this growth step state.")]
    [SerializeField] private float outlineWidth = 0.22f;
    #endregion

    #endregion

    #region Properties
    public TMP_FontAsset FontAsset
    {
        get
        {
            return fontAsset;
        }
    }

    public float FontSize
    {
        get
        {
            return fontSize;
        }
    }

    public Color Color
    {
        get
        {
            return color;
        }
    }

    public Color OutlineColor
    {
        get
        {
            return outlineColor;
        }
    }

    public float OutlineWidth
    {
        get
        {
            return outlineWidth;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates one text state and reports numeric/color issues without mutating authored values.
    /// </summary>
    /// <param name="ownerAssetName">Asset name used in warning messages.</param>
    /// <param name="sectionLabel">Human-readable section path that owns this state.</param>
    public void Validate(string ownerAssetName, string sectionLabel)
    {
        if (!float.IsFinite(fontSize) || fontSize < 0f)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Growth Sequence/{1}: Font Size should be finite and zero or greater.", ownerAssetName, sectionLabel));

        if (!float.IsFinite(outlineWidth) || outlineWidth < 0f)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Growth Sequence/{1}: Outline Width should be finite and zero or greater.", ownerAssetName, sectionLabel));

        if (!IsFinite(color) || !IsFinite(outlineColor))
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Growth Sequence/{1}: colors contain invalid numeric values.", ownerAssetName, sectionLabel));
    }
    #endregion

    #region Validation
    /// <summary>
    /// Checks whether every color channel is finite.
    /// </summary>
    /// <param name="value">Color value to inspect.</param>
    /// <returns>True when all channels are finite.</returns>
    private static bool IsFinite(Color value)
    {
        return float.IsFinite(value.r) &&
               float.IsFinite(value.g) &&
               float.IsFinite(value.b) &&
               float.IsFinite(value.a);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores the visual assignment for one step inside one level-up growth schedule.
/// </summary>
[Serializable]
public sealed class PlayerGrowthSequenceHudStepVisualDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Zero-based step index inside the selected level-up schedule. The tool can synchronize these entries from the active Progression Preset.")]
    [SerializeField] private int stepIndex;

    [Tooltip("Optional stat name copied from the matching progression step for readability and stable review.")]
    [SerializeField] private string statName;

    [Tooltip("Text shown when Presentation Mode is Text. Empty text falls back to the progression stat name or step number.")]
    [SerializeField] private string textOverride;

    [Tooltip("Chooses whether this step uses two sprites or two TMP text configurations.")]
    [SerializeField] private PlayerGrowthSequenceHudPresentationMode presentationMode = PlayerGrowthSequenceHudPresentationMode.Text;

    [Tooltip("Sprite used while this step is the next level-up target.")]
    [SerializeField] private Sprite nextSprite;

    [Tooltip("Sprite used while this step is not the next level-up target.")]
    [SerializeField] private Sprite normalSprite;

    [Tooltip("Text state used while this step is the next level-up target.")]
    [SerializeField] private PlayerGrowthSequenceHudTextStateSettings nextText = new PlayerGrowthSequenceHudTextStateSettings();

    [Tooltip("Text state used while this step is not the next level-up target.")]
    [SerializeField] private PlayerGrowthSequenceHudTextStateSettings normalText = new PlayerGrowthSequenceHudTextStateSettings();
    #endregion

    #endregion

    #region Properties
    public int StepIndex
    {
        get
        {
            return stepIndex;
        }
    }

    public string StatName
    {
        get
        {
            return statName;
        }
    }

    public string TextOverride
    {
        get
        {
            return textOverride;
        }
    }

    public PlayerGrowthSequenceHudPresentationMode PresentationMode
    {
        get
        {
            return presentationMode;
        }
    }

    public Sprite NextSprite
    {
        get
        {
            return nextSprite;
        }
    }

    public Sprite NormalSprite
    {
        get
        {
            return normalSprite;
        }
    }

    public PlayerGrowthSequenceHudTextStateSettings NextText
    {
        get
        {
            return nextText;
        }
    }

    public PlayerGrowthSequenceHudTextStateSettings NormalText
    {
        get
        {
            return normalText;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates the step visual data and reports missing assets or invalid values without mutating the preset.
    /// </summary>
    /// <param name="ownerAssetName">Asset name used in warning messages.</param>
    /// <param name="scheduleId">Schedule ID that owns this step entry.</param>
    /// <param name="entryIndex">Index used to identify this visual row in warnings.</param>
    public void Validate(string ownerAssetName, string scheduleId, int entryIndex)
    {
        if (stepIndex < 0)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Growth Sequence/{1}/{2}: Step Index should be zero or greater.", ownerAssetName, scheduleId, entryIndex));

        if (!Enum.IsDefined(typeof(PlayerGrowthSequenceHudPresentationMode), presentationMode))
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Growth Sequence/{1}/{2}: Presentation Mode is unsupported.", ownerAssetName, scheduleId, entryIndex));

        switch (presentationMode)
        {
            case PlayerGrowthSequenceHudPresentationMode.Image:
                if (nextSprite == null || normalSprite == null)
                    Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Growth Sequence/{1}/{2}: Image mode requires both Next and Normal sprites.", ownerAssetName, scheduleId, entryIndex));
                break;
            case PlayerGrowthSequenceHudPresentationMode.Text:
                ValidateTextStates(ownerAssetName, scheduleId, entryIndex);
                break;
        }
    }
    #endregion

    #region Validation
    /// <summary>
    /// Validates the next and normal text states used by a text-mode growth step.
    /// </summary>
    /// <param name="ownerAssetName">Asset name used in warning messages.</param>
    /// <param name="scheduleId">Schedule ID that owns this step entry.</param>
    /// <param name="entryIndex">Index used to identify this visual row in warnings.</param>
    private void ValidateTextStates(string ownerAssetName, string scheduleId, int entryIndex)
    {
        if (nextText == null)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Growth Sequence/{1}/{2}: Next Text settings are missing.", ownerAssetName, scheduleId, entryIndex));
        else
            nextText.Validate(ownerAssetName, string.Format("{0}/{1}/Next Text", scheduleId, entryIndex));

        if (normalText == null)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Growth Sequence/{1}/{2}: Normal Text settings are missing.", ownerAssetName, scheduleId, entryIndex));
        else
            normalText.Validate(ownerAssetName, string.Format("{0}/{1}/Normal Text", scheduleId, entryIndex));
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores all growth-sequence visual entries for one level-up schedule.
/// </summary>
[Serializable]
public sealed class PlayerGrowthSequenceHudScheduleVisualDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Schedule ID selected from the active Level-up & Progression preset.")]
    [SerializeField] private string scheduleId = "Schedule0";

    [Tooltip("Visual entries mapped by step index inside this schedule.")]
    [SerializeField] private List<PlayerGrowthSequenceHudStepVisualDefinition> steps = new List<PlayerGrowthSequenceHudStepVisualDefinition>();
    #endregion

    #endregion

    #region Properties
    public string ScheduleId
    {
        get
        {
            return scheduleId;
        }
    }

    public IReadOnlyList<PlayerGrowthSequenceHudStepVisualDefinition> Steps
    {
        get
        {
            return steps;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates one schedule visual map and warns about missing IDs or duplicate step indexes.
    /// </summary>
    /// <param name="ownerAssetName">Asset name used in warning messages.</param>
    /// <param name="entryIndex">Index used to identify the schedule row in warnings.</param>
    public void Validate(string ownerAssetName, int entryIndex)
    {
        if (string.IsNullOrWhiteSpace(scheduleId))
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Growth Sequence/Schedule Visuals/{1}: Schedule Id is empty.", ownerAssetName, entryIndex));

        if (steps == null)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Growth Sequence/Schedule Visuals/{1}: Steps list is missing.", ownerAssetName, entryIndex));
            return;
        }

        HashSet<int> stepIndexes = new HashSet<int>();

        for (int stepIndex = 0; stepIndex < steps.Count; stepIndex++)
        {
            PlayerGrowthSequenceHudStepVisualDefinition step = steps[stepIndex];

            if (step == null)
            {
                Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Growth Sequence/{1}/{2}: step visual is missing.", ownerAssetName, scheduleId, stepIndex));
                continue;
            }

            step.Validate(ownerAssetName, scheduleId, stepIndex);

            if (!stepIndexes.Add(step.StepIndex))
                Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Growth Sequence/{1}/{2}: duplicate Step Index {3}.", ownerAssetName, scheduleId, stepIndex, step.StepIndex));
        }
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores the complete HUD growth-sequence visual configuration authored by one Player Visual Preset.
/// </summary>
[Serializable]
public sealed class PlayerGrowthSequenceHudSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables the ECS-driven growth sequence HUD under GrowthSequence Container.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Hides the growth sequence while no player entity or progression config can be resolved.")]
    [SerializeField] private bool hideWhenPlayerMissing = true;

    [Tooltip("Maximum number of growth sequence entries rendered from the preauthored UI pool. Set 0 to use the whole matching schedule.")]
    [SerializeField] private int maximumVisibleSteps;

    [Tooltip("Visual entries grouped by level-up schedule ID.")]
    [SerializeField] private List<PlayerGrowthSequenceHudScheduleVisualDefinition> schedules = new List<PlayerGrowthSequenceHudScheduleVisualDefinition>();
    #endregion

    #endregion

    #region Properties
    public bool Enabled
    {
        get
        {
            return enabled;
        }
    }

    public bool HideWhenPlayerMissing
    {
        get
        {
            return hideWhenPlayerMissing;
        }
    }

    public int MaximumVisibleSteps
    {
        get
        {
            return maximumVisibleSteps;
        }
    }

    public IReadOnlyList<PlayerGrowthSequenceHudScheduleVisualDefinition> Schedules
    {
        get
        {
            return schedules;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates all growth-sequence visual maps and reports duplicate schedule IDs.
    /// </summary>
    /// <param name="ownerAssetName">Asset name used in warning messages.</param>
    public void Validate(string ownerAssetName)
    {
        if (maximumVisibleSteps < 0)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Growth Sequence: Maximum Visible Steps should be zero or greater.", ownerAssetName));

        if (schedules == null)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Growth Sequence: Schedules list is missing.", ownerAssetName));
            return;
        }

        HashSet<string> scheduleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int scheduleIndex = 0; scheduleIndex < schedules.Count; scheduleIndex++)
        {
            PlayerGrowthSequenceHudScheduleVisualDefinition schedule = schedules[scheduleIndex];

            if (schedule == null)
            {
                Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Growth Sequence/Schedules/{1}: schedule visual is missing.", ownerAssetName, scheduleIndex));
                continue;
            }

            schedule.Validate(ownerAssetName, scheduleIndex);

            if (string.IsNullOrWhiteSpace(schedule.ScheduleId))
                continue;

            if (!scheduleIds.Add(schedule.ScheduleId.Trim()))
                Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Growth Sequence/Schedules/{1}: duplicate Schedule Id '{2}'.", ownerAssetName, scheduleIndex, schedule.ScheduleId));
        }
    }
    #endregion

    #endregion
}
