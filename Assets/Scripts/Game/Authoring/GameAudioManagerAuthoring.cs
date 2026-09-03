using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Resolves Audio, Settings, and HUD Manager presets for baking or persistent regular-scene bootstrap.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameAudioManagerAuthoring : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Preset")]
    [Tooltip("Game master preset used to resolve the Audio Manager sub-preset.")]
    [SerializeField] private GameMasterPreset masterPreset;

    [Tooltip("Direct Audio Manager preset fallback used when Master Preset is missing or has no Audio Manager assigned.")]
    [SerializeField] private GameAudioManagerPreset audioManagerPreset;

    [Tooltip("Direct Settings Manager preset fallback used when Master Preset is missing or has no Settings Manager assigned.")]
    [SerializeField] private GameSettingsManagerPreset settingsManagerPreset;

    [Tooltip("Direct HUD Manager preset fallback used when Master Preset is missing or has no HUD Manager assigned.")]
    [SerializeField] private GameHudManagerPreset hudManagerPreset;

    [Header("Runtime Bootstrap")]
    [Tooltip("Creates the persistent manager ECS singleton when this component lives in the regular Bootstrap scene instead of a SubScene.")]
    [SerializeField] private bool createRuntimeSingletonWhenNotBaked = true;
    #endregion

    #region Runtime
    private bool runtimeSingletonCreated;
    #endregion

    #endregion

    #region Properties
    public GameMasterPreset MasterPreset
    {
        get
        {
            return masterPreset;
        }
    }

    public GameAudioManagerPreset AudioManagerPreset
    {
        get
        {
            return audioManagerPreset;
        }
    }

    public GameSettingsManagerPreset SettingsManagerPreset
    {
        get
        {
            return settingsManagerPreset;
        }
    }

    public GameHudManagerPreset HudManagerPreset
    {
        get
        {
            return hudManagerPreset;
        }
    }

    public bool CreateRuntimeSingletonWhenNotBaked
    {
        get
        {
            return createRuntimeSingletonWhenNotBaked;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the effective Audio Manager preset used by baking.
    /// </summary>
    /// <returns>Audio Manager preset from MasterPreset or direct fallback.</returns>
    public GameAudioManagerPreset ResolveAudioManagerPreset()
    {
        if (masterPreset != null && masterPreset.AudioManagerPreset != null)
            return masterPreset.AudioManagerPreset;

        return audioManagerPreset;
    }

    /// <summary>
    /// Resolves the effective Settings Manager preset used by baking.
    /// </summary>
    /// <returns>Settings Manager preset from MasterPreset or direct fallback.</returns>
    public GameSettingsManagerPreset ResolveSettingsManagerPreset()
    {
        if (masterPreset != null && masterPreset.SettingsManagerPreset != null)
            return masterPreset.SettingsManagerPreset;

        return settingsManagerPreset;
    }

    /// <summary>
    /// Resolves the effective HUD Manager preset used by baking.
    /// </summary>
    /// <returns>HUD Manager preset from MasterPreset or direct fallback.</returns>
    public GameHudManagerPreset ResolveHudManagerPreset()
    {
        if (masterPreset != null && masterPreset.HudManagerPreset != null)
            return masterPreset.HudManagerPreset;

        return hudManagerPreset;
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Creates the persistent manager singleton for the regular Bootstrap scene before additive menus are loaded.
    /// </summary>
    private void Start()
    {
        if (!Application.isPlaying || !createRuntimeSingletonWhenNotBaked || runtimeSingletonCreated)
            return;

        runtimeSingletonCreated = GameAudioManagerRuntimeBootstrapUtility.TryCreate(this);
    }
    #endregion

    #endregion
}

/// <summary>
/// Baker that converts GameAudioManagerAuthoring into singleton audio config, settings config and event buffers.
/// </summary>
public sealed class GameAudioManagerAuthoringBaker : Baker<GameAudioManagerAuthoring>
{
    #region Methods

    #region Bake
    /// <summary>
    /// Bakes global Settings config, audio config and all event mappings from the selected presets.
    /// </summary>
    /// <param name="authoring">Scene authoring component that chooses the preset.</param>
    public override void Bake(GameAudioManagerAuthoring authoring)
    {
        if (authoring == null)
            return;

        DeclarePresetDependencies(authoring);
        GameAudioManagerPreset audioPreset = authoring.ResolveAudioManagerPreset();
        GameSettingsManagerPreset settingsPreset = authoring.ResolveSettingsManagerPreset();
        GameHudManagerPreset hudPreset = authoring.ResolveHudManagerPreset();

        Entity entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, GameAudioManagerPresetBakeUtility.BuildSettingsRuntimeConfig(settingsPreset));
        AddComponent(entity, GameHudManagerPresetBakeUtility.BuildConfig(hudPreset));
        GameHudPowerUpSummarySettings summarySettings = hudPreset != null ? hudPreset.PowerUpSummarySettings : null;
        AddComponent(entity, GameHudSupplementalPresetBakeUtility.BuildSummaryConfig(summarySettings));
        GameHudWaveClearAnnouncementSettings announcementSettings =
            hudPreset != null ? hudPreset.WaveClearAnnouncementSettings : null;
        AddComponent(entity,
                     GameHudSupplementalPresetBakeUtility.BuildWaveClearAnnouncementConfig(announcementSettings));
        AddComponent(entity, new GameHudWaveClearAnnouncementPresentationState
        {
            NodeIndex = -1
        });
        DynamicBuffer<GamePowerUpSummaryStatisticElement> statisticBuffer = AddBuffer<GamePowerUpSummaryStatisticElement>(entity);
        GameHudSupplementalPresetBakeUtility.PopulateStatisticBuffer(summarySettings, statisticBuffer);

        if (audioPreset == null)
            return;

        AddComponent(entity, GameAudioManagerPresetBakeUtility.BuildAudioRuntimeConfig(audioPreset));
        DynamicBuffer<GameAudioEventBindingElement> bindingBuffer = AddBuffer<GameAudioEventBindingElement>(entity);
        DynamicBuffer<GameAudioEventRequest> requestBuffer = AddBuffer<GameAudioEventRequest>(entity);
        DynamicBuffer<GameAudioRateLimitStateElement> rateLimitStateBuffer = AddBuffer<GameAudioRateLimitStateElement>(entity);
        GameAudioManagerPresetBakeUtility.PopulateBindingBuffer(audioPreset, bindingBuffer);
        requestBuffer.Clear();
        rateLimitStateBuffer.Clear();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Declares asset dependencies so the audio singleton rebakes when presets change.
    /// </summary>
    /// <param name="authoring">Authoring component that contains the preset references.</param>
    private void DeclarePresetDependencies(GameAudioManagerAuthoring authoring)
    {
        if (authoring.MasterPreset != null)
        {
            DependsOn(authoring.MasterPreset);

            if (authoring.MasterPreset.AudioManagerPreset != null)
                DependsOn(authoring.MasterPreset.AudioManagerPreset);

            if (authoring.MasterPreset.SettingsManagerPreset != null)
                DependsOn(authoring.MasterPreset.SettingsManagerPreset);

            if (authoring.MasterPreset.HudManagerPreset != null)
            {
                DependsOn(authoring.MasterPreset.HudManagerPreset);
                DeclareHudPresentationDependencies(authoring.MasterPreset.HudManagerPreset);
            }
        }

        if (authoring.AudioManagerPreset != null)
            DependsOn(authoring.AudioManagerPreset);

        if (authoring.SettingsManagerPreset != null)
            DependsOn(authoring.SettingsManagerPreset);

        if (authoring.HudManagerPreset != null)
        {
            DependsOn(authoring.HudManagerPreset);
            DeclareHudPresentationDependencies(authoring.HudManagerPreset);
        }
    }

    /// <summary>
    /// Declares supplemental HUD presentation assets referenced by one HUD Manager preset.
    /// </summary>
    /// <param name="hudPreset">HUD Manager preset whose nested object references participate in baking.</param>
    private void DeclareHudPresentationDependencies(GameHudManagerPreset hudPreset)
    {
        if (hudPreset == null)
            return;

        GameHudPowerUpSummarySettings summarySettings = hudPreset.PowerUpSummarySettings;
        GameHudWaveClearAnnouncementSettings announcementSettings = hudPreset.WaveClearAnnouncementSettings;

        if (announcementSettings != null && announcementSettings.Font != null)
            DependsOn(announcementSettings.Font);

        if (summarySettings != null)
        {
            if (summarySettings.BackgroundSprite != null)
                DependsOn(summarySettings.BackgroundSprite);

            if (summarySettings.ToggleSprite != null)
                DependsOn(summarySettings.ToggleSprite);

            if (summarySettings.IconBackgroundSprite != null)
                DependsOn(summarySettings.IconBackgroundSprite);

            if (summarySettings.CounterFont != null)
                DependsOn(summarySettings.CounterFont);

            if (summarySettings.TitleFont != null)
                DependsOn(summarySettings.TitleFont);

            IReadOnlyList<GameHudStatisticDisplayDefinition> statistics = summarySettings.Statistics;

            for (int statisticIndex = 0; statisticIndex < statistics.Count; statisticIndex++)
            {
                GameHudStatisticDisplayDefinition statistic = statistics[statisticIndex];

                if (statistic != null && statistic.Font != null)
                    DependsOn(statistic.Font);
            }
        }
    }

    #endregion

    #endregion
}
