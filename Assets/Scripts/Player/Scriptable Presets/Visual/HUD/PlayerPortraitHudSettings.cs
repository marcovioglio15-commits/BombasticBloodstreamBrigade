using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Selects how a HUD portrait animation advances once it reaches the last authored frame.
/// </summary>
public enum PlayerPortraitHudPlaybackMode
{
    Loop = 0,
    Once = 1,
    PingPong = 2
}

/// <summary>
/// Identifies the gameplay condition that can request one authored portrait animation.
/// </summary>
public enum PlayerPortraitHudAnimationRole : byte
{
    Idle = 0,
    Damage = 1,
    ComboRankIdle = 2,
    Death = 3,
    PowerUpAcquired = 4
}

/// <summary>
/// Stores one fully authored portrait frame sequence with playback and arbitration data.
/// </summary>
[Serializable]
public sealed class PlayerPortraitHudAnimationDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable animation ID used by Add Scaling, bake metadata, and runtime selection. Keep it unique inside this Portrait section.")]
    [SerializeField] private string animationId = "PortraitAnimation";

    [Tooltip("Ordered sprites used by this portrait animation. Empty entries are ignored by the baker and warned during validation.")]
    [SerializeField] private List<Sprite> frames = new List<Sprite>();

    [Tooltip("Seconds spent on each frame before the playback speed multiplier is applied.")]
    [SerializeField] private float secondsPerFrame = 0.12f;

    [Tooltip("Runtime multiplier applied to Seconds Per Frame. Values greater than 1 play faster.")]
    [SerializeField] private float playbackSpeedMultiplier = 1f;

    [Tooltip("Playback behavior used when the animation reaches its last valid frame.")]
    [SerializeField] private PlayerPortraitHudPlaybackMode playbackMode = PlayerPortraitHudPlaybackMode.Loop;

    [Tooltip("Higher priority animations can interrupt lower priority portrait states.")]
    [SerializeField] private int priority;

    [Tooltip("When enabled, an equal or higher priority event animation restarts this animation from the first frame.")]
    [SerializeField] private bool restartWhenReentered = true;
    #endregion

    #endregion

    #region Properties
    public string AnimationId
    {
        get
        {
            return animationId;
        }
    }

    public IReadOnlyList<Sprite> Frames
    {
        get
        {
            return frames;
        }
    }

    public float SecondsPerFrame
    {
        get
        {
            return secondsPerFrame;
        }
    }

    public float PlaybackSpeedMultiplier
    {
        get
        {
            return playbackSpeedMultiplier;
        }
    }

    public PlayerPortraitHudPlaybackMode PlaybackMode
    {
        get
        {
            return playbackMode;
        }
    }

    public int Priority
    {
        get
        {
            return priority;
        }
    }

    public bool RestartWhenReentered
    {
        get
        {
            return restartWhenReentered;
        }
    }

    public bool HasAnyFrame
    {
        get
        {
            if (frames == null)
                return false;

            for (int frameIndex = 0; frameIndex < frames.Count; frameIndex++)
            {
                if (frames[frameIndex] != null)
                    return true;
            }

            return false;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates authored portrait animation data and reports unsafe runtime values without mutating the preset.
    /// </summary>
    /// <param name="ownerAssetName">Asset name used in warning messages.</param>
    /// <param name="sectionLabel">Human-readable section path that owns this animation.</param>
    /// <param name="requiresFrames">True when this entry cannot fall back to the authored HUD Image sprite.</param>
    public void Validate(string ownerAssetName, string sectionLabel, bool requiresFrames)
    {
        if (frames == null)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Portrait/{1}: frame list is missing.", ownerAssetName, sectionLabel));
            return;
        }

        bool hasAnyFrame = HasAnyFrame;

        if (requiresFrames && !hasAnyFrame)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Portrait/{1}: no valid frame is assigned.", ownerAssetName, sectionLabel));

        for (int frameIndex = 0; frameIndex < frames.Count; frameIndex++)
        {
            if (frames[frameIndex] != null)
                continue;

            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Portrait/{1}: frame {2} is empty.", ownerAssetName, sectionLabel, frameIndex));
        }

        if (!float.IsFinite(secondsPerFrame) || secondsPerFrame <= 0f)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Portrait/{1}: Seconds Per Frame should be finite and greater than zero.", ownerAssetName, sectionLabel));

        if (!float.IsFinite(playbackSpeedMultiplier) || playbackSpeedMultiplier <= 0f)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Portrait/{1}: Playback Speed Multiplier should be finite and greater than zero.", ownerAssetName, sectionLabel));

        if (!Enum.IsDefined(typeof(PlayerPortraitHudPlaybackMode), playbackMode))
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Portrait/{1}: Playback Mode is unsupported.", ownerAssetName, sectionLabel));
    }
    #endregion

    #endregion
}

/// <summary>
/// Binds a portrait idle animation to one combo rank ID authored in the progression preset.
/// </summary>
[Serializable]
public sealed class PlayerPortraitHudComboRankAnimationDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Combo rank ID selected from the active Player Progression Preset. Runtime matches this against PlayerComboCounterState.CurrentRankId.")]
    [SerializeField] private string rankId;

    [Tooltip("Portrait animation played while this combo rank is currently held and no higher-priority condition is active.")]
    [SerializeField] private PlayerPortraitHudAnimationDefinition animation = new PlayerPortraitHudAnimationDefinition();
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

    public PlayerPortraitHudAnimationDefinition Animation
    {
        get
        {
            return animation;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates the combo-rank portrait binding and its nested animation.
    /// </summary>
    /// <param name="ownerAssetName">Asset name used in warning messages.</param>
    /// <param name="entryIndex">Index used to identify the entry inside warnings.</param>
    public void Validate(string ownerAssetName, int entryIndex)
    {
        if (string.IsNullOrWhiteSpace(rankId))
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Portrait/Combo Rank Animations/{1}: Rank Id is empty.", ownerAssetName, entryIndex));

        if (animation == null)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Portrait/Combo Rank Animations/{1}: animation is missing.", ownerAssetName, entryIndex));
            return;
        }

        animation.Validate(ownerAssetName, string.Format("Combo Rank Animations/{0}", entryIndex), true);
    }
    #endregion

    #endregion
}

/// <summary>
/// Binds one portrait event animation to one or more power-up IDs selected from the Power-ups preset.
/// </summary>
[Serializable]
public sealed class PlayerPortraitHudPowerUpAnimationDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Power-up IDs selected from the active Player Power-ups Preset. The tool exposes this as a closed selector to avoid free-form IDs.")]
    [SerializeField] private List<string> powerUpIds = new List<string>();

    [Tooltip("Portrait animation played when any selected power-up is acquired or stacked.")]
    [SerializeField] private PlayerPortraitHudAnimationDefinition animation = new PlayerPortraitHudAnimationDefinition();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<string> PowerUpIds
    {
        get
        {
            return powerUpIds;
        }
    }

    public PlayerPortraitHudAnimationDefinition Animation
    {
        get
        {
            return animation;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates the power-up portrait binding and its nested animation.
    /// </summary>
    /// <param name="ownerAssetName">Asset name used in warning messages.</param>
    /// <param name="entryIndex">Index used to identify the entry inside warnings.</param>
    public void Validate(string ownerAssetName, int entryIndex)
    {
        if (powerUpIds == null)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Portrait/Power-up Animations/{1}: Power-up ID list is missing.", ownerAssetName, entryIndex));
            return;
        }

        if (powerUpIds.Count <= 0)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Portrait/Power-up Animations/{1}: no power-ups are selected.", ownerAssetName, entryIndex));

        for (int powerUpIndex = 0; powerUpIndex < powerUpIds.Count; powerUpIndex++)
        {
            if (!string.IsNullOrWhiteSpace(powerUpIds[powerUpIndex]))
                continue;

            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Portrait/Power-up Animations/{1}: Power-up ID {2} is empty.", ownerAssetName, entryIndex, powerUpIndex));
        }

        if (animation == null)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Portrait/Power-up Animations/{1}: animation is missing.", ownerAssetName, entryIndex));
            return;
        }

        animation.Validate(ownerAssetName, string.Format("Power-up Animations/{0}", entryIndex), true);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores all Player HUD portrait animation data authored by one Player Visual Preset.
/// </summary>
[Serializable]
public sealed class PlayerPortraitHudSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables the ECS-driven dynamic portrait on the Player HUD.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Hides the portrait image while no valid player entity can be resolved.")]
    [SerializeField] private bool hideWhenPlayerMissing = true;

    [Tooltip("Fallback portrait animation used when no higher-priority condition applies.")]
    [SerializeField] private PlayerPortraitHudAnimationDefinition idleAnimation = new PlayerPortraitHudAnimationDefinition();

    [Tooltip("Portrait animation played when the player receives health or shield damage.")]
    [SerializeField] private PlayerPortraitHudAnimationDefinition damageAnimation = new PlayerPortraitHudAnimationDefinition();

    [Tooltip("Portrait animation played while the run outcome is in the dying or defeat state.")]
    [SerializeField] private PlayerPortraitHudAnimationDefinition deathAnimation = new PlayerPortraitHudAnimationDefinition();

    [Tooltip("Optional combo-rank idle portrait overrides keyed by rank ID.")]
    [SerializeField] private List<PlayerPortraitHudComboRankAnimationDefinition> comboRankAnimations = new List<PlayerPortraitHudComboRankAnimationDefinition>();

    [Tooltip("Optional portrait event animations keyed by one or more power-up IDs.")]
    [SerializeField] private List<PlayerPortraitHudPowerUpAnimationDefinition> powerUpAnimations = new List<PlayerPortraitHudPowerUpAnimationDefinition>();
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

    public PlayerPortraitHudAnimationDefinition IdleAnimation
    {
        get
        {
            return idleAnimation;
        }
    }

    public PlayerPortraitHudAnimationDefinition DamageAnimation
    {
        get
        {
            return damageAnimation;
        }
    }

    public PlayerPortraitHudAnimationDefinition DeathAnimation
    {
        get
        {
            return deathAnimation;
        }
    }

    public IReadOnlyList<PlayerPortraitHudComboRankAnimationDefinition> ComboRankAnimations
    {
        get
        {
            return comboRankAnimations;
        }
    }

    public IReadOnlyList<PlayerPortraitHudPowerUpAnimationDefinition> PowerUpAnimations
    {
        get
        {
            return powerUpAnimations;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates the complete portrait HUD section and reports duplicate or missing authored data.
    /// </summary>
    /// <param name="ownerAssetName">Asset name used in warning messages.</param>
    public void Validate(string ownerAssetName)
    {
        if (idleAnimation == null)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Portrait: Idle Animation is missing.", ownerAssetName));
        else
            idleAnimation.Validate(ownerAssetName, "Idle Animation", false);

        if (damageAnimation == null)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Portrait: Damage Animation is missing.", ownerAssetName));
        else
            damageAnimation.Validate(ownerAssetName, "Damage Animation", false);

        if (deathAnimation == null)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Portrait: Death Animation is missing.", ownerAssetName));
        else
            deathAnimation.Validate(ownerAssetName, "Death Animation", false);

        ValidateComboRankAnimations(ownerAssetName);
        ValidatePowerUpAnimations(ownerAssetName);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Validates combo-rank portrait animation entries.
    /// </summary>
    /// <param name="ownerAssetName">Asset name used in warning messages.</param>
    private void ValidateComboRankAnimations(string ownerAssetName)
    {
        if (comboRankAnimations == null)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Portrait: Combo Rank Animations list is missing.", ownerAssetName));
            return;
        }

        HashSet<string> rankIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int entryIndex = 0; entryIndex < comboRankAnimations.Count; entryIndex++)
        {
            PlayerPortraitHudComboRankAnimationDefinition entry = comboRankAnimations[entryIndex];

            if (entry == null)
            {
                Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Portrait/Combo Rank Animations/{1}: entry is missing.", ownerAssetName, entryIndex));
                continue;
            }

            entry.Validate(ownerAssetName, entryIndex);

            if (string.IsNullOrWhiteSpace(entry.RankId))
                continue;

            if (!rankIds.Add(entry.RankId.Trim()))
                Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Portrait/Combo Rank Animations/{1}: duplicate Rank Id '{2}'.", ownerAssetName, entryIndex, entry.RankId));
        }
    }

    /// <summary>
    /// Validates power-up portrait animation entries.
    /// </summary>
    /// <param name="ownerAssetName">Asset name used in warning messages.</param>
    private void ValidatePowerUpAnimations(string ownerAssetName)
    {
        if (powerUpAnimations == null)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Portrait: Power-up Animations list is missing.", ownerAssetName));
            return;
        }

        HashSet<string> powerUpIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int entryIndex = 0; entryIndex < powerUpAnimations.Count; entryIndex++)
        {
            PlayerPortraitHudPowerUpAnimationDefinition entry = powerUpAnimations[entryIndex];

            if (entry == null)
            {
                Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Portrait/Power-up Animations/{1}: entry is missing.", ownerAssetName, entryIndex));
                continue;
            }

            entry.Validate(ownerAssetName, entryIndex);

            IReadOnlyList<string> entryPowerUpIds = entry.PowerUpIds;

            if (entryPowerUpIds == null)
                continue;

            for (int powerUpIndex = 0; powerUpIndex < entryPowerUpIds.Count; powerUpIndex++)
            {
                string powerUpId = entryPowerUpIds[powerUpIndex];

                if (string.IsNullOrWhiteSpace(powerUpId))
                    continue;

                if (!powerUpIds.Add(powerUpId.Trim()))
                    Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Portrait/Power-up Animations/{1}: duplicate Power-up Id '{2}'.", ownerAssetName, entryIndex, powerUpId));
            }
        }
    }
    #endregion

    #endregion
}
