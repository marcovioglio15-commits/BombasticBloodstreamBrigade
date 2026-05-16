using System.Collections.Generic;

/// <summary>
/// Produces non-mutating validation warnings for GameAudioManagerPreset assets.
/// </summary>
public static class GameAudioManagerPresetValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Collects warnings describing values that may lead to missing or invalid runtime audio playback.
    /// </summary>
    /// <param name="preset">Audio manager preset to inspect.</param>
    /// <param name="warnings">Mutable list that receives warning text.</param>
    public static void CollectWarnings(GameAudioManagerPreset preset, List<string> warnings)
    {
        if (warnings == null)
            return;

        warnings.Clear();

        if (preset == null)
        {
            warnings.Add("Audio Manager preset is missing.");
            return;
        }

        ValidatePlaybackSettings(preset, warnings);
        ValidateRoutingSettings(preset, warnings);
        ValidateBackgroundMusicSettings(preset, warnings);
        ValidateEventBindings(preset, warnings);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Validates global playback settings without modifying the preset.
    /// </summary>
    /// <param name="preset">Preset that owns the playback settings.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidatePlaybackSettings(GameAudioManagerPreset preset, List<string> warnings)
    {
        GameAudioPlaybackSettings playbackSettings = preset.PlaybackSettings;

        if (playbackSettings == null)
        {
            warnings.Add("Playback settings are missing.");
            return;
        }

        if (playbackSettings.MasterVolume < 0f)
            warnings.Add("Master Volume is negative. Runtime playback clamps it to zero.");

        if (playbackSettings.DefaultMinimumDistance < 0f)
            warnings.Add("Default Minimum Distance is negative.");

        if (playbackSettings.DefaultMaximumDistance < playbackSettings.DefaultMinimumDistance)
            warnings.Add("Default Maximum Distance is lower than Default Minimum Distance.");
    }

    /// <summary>
    /// Validates FMOD routing path fields without requiring the FMOD package at edit time.
    /// </summary>
    /// <param name="preset">Preset that owns routing settings.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateRoutingSettings(GameAudioManagerPreset preset, List<string> warnings)
    {
        GameAudioRoutingSettings routingSettings = preset.RoutingSettings;

        if (routingSettings == null)
        {
            warnings.Add("FMOD routing settings are missing.");
            return;
        }

        if (string.IsNullOrWhiteSpace(routingSettings.MasterBusPath))
            warnings.Add("Master Bus Path is empty.");

        if (string.IsNullOrWhiteSpace(routingSettings.SfxBusPath))
            warnings.Add("SFX Bus Path is empty.");

        if (routingSettings.SfxVolume < 0f)
            warnings.Add("SFX Volume is negative.");

        if (routingSettings.MusicVolume < 0f)
            warnings.Add("Music Volume is negative.");
    }

    /// <summary>
    /// Validates background music settings without requiring the FMOD package at edit time.
    /// </summary>
    /// <param name="preset">Preset that owns background music settings.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateBackgroundMusicSettings(GameAudioManagerPreset preset, List<string> warnings)
    {
        GameAudioBackgroundMusicSettings backgroundMusicSettings = preset.BackgroundMusicSettings;

        if (backgroundMusicSettings == null)
        {
            warnings.Add("Background Music settings are missing.");
            return;
        }

        if (!backgroundMusicSettings.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(backgroundMusicSettings.EventPath))
            warnings.Add("Background Music is enabled but FMOD Event Path is empty.");

        if (string.IsNullOrWhiteSpace(backgroundMusicSettings.BankName))
            warnings.Add("Background Music Bank Name is empty. Playback will rely on a bank loaded elsewhere.");

        if (backgroundMusicSettings.Volume < 0f)
            warnings.Add("Background Music Volume is negative.");
    }

    /// <summary>
    /// Validates event map coverage, paths and per-binding playback values.
    /// </summary>
    /// <param name="preset">Preset that owns event bindings.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateEventBindings(GameAudioManagerPreset preset, List<string> warnings)
    {
        IReadOnlyList<GameAudioEventBinding> eventBindings = preset.EventBindings;

        if (eventBindings == null)
        {
            warnings.Add("Event bindings list is missing.");
            return;
        }

        GameAudioDefaultEventDefinition[] definitions = GameAudioDefaultEventDefinitions.Definitions;

        for (int definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++)
        {
            GameAudioDefaultEventDefinition definition = definitions[definitionIndex];

            if (!ContainsEvent(eventBindings, definition.EventId))
                warnings.Add("Missing event binding: " + definition.EventCode + ".");
        }

        for (int bindingIndex = 0; bindingIndex < eventBindings.Count; bindingIndex++)
        {
            GameAudioEventBinding binding = eventBindings[bindingIndex];

            if (binding == null)
            {
                warnings.Add("Event binding entry " + bindingIndex + " is null.");
                continue;
            }

            ValidateEventBinding(binding, bindingIndex, warnings);
        }
    }

    /// <summary>
    /// Validates one event binding and reports warnings scoped to its display name.
    /// </summary>
    /// <param name="binding">Event binding to inspect.</param>
    /// <param name="bindingIndex">Index used when display metadata is missing.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateEventBinding(GameAudioEventBinding binding, int bindingIndex, List<string> warnings)
    {
        string label = string.IsNullOrWhiteSpace(binding.EventCode)
            ? "Binding " + bindingIndex
            : binding.EventCode;

        if (binding.EventId == GameAudioEventId.None)
            warnings.Add(label + " uses the None event ID.");

        if (string.IsNullOrWhiteSpace(binding.EventPath))
            warnings.Add(label + " has no FMOD event path.");

        if (binding.Volume < 0f)
            warnings.Add(label + " has negative Volume.");

        if (binding.Pitch <= 0f)
            warnings.Add(label + " has non-positive Pitch.");

        if (binding.MinimumDistance < 0f)
            warnings.Add(label + " has negative Minimum Distance.");

        if (binding.MaximumDistance < binding.MinimumDistance)
            warnings.Add(label + " has Maximum Distance lower than Minimum Distance.");

        ValidateRateLimit(binding, label, warnings);
    }

    /// <summary>
    /// Validates one binding rate-limit block without changing the authored values.
    /// </summary>
    /// <param name="binding">Event binding that owns the cap settings.</param>
    /// <param name="label">Display label included in warnings.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateRateLimit(GameAudioEventBinding binding, string label, List<string> warnings)
    {
        GameAudioRateLimitSettings rateLimit = binding.RateLimit;

        if (rateLimit == null)
        {
            warnings.Add(label + " has no Rate Limit settings object.");
            return;
        }

        if (!rateLimit.Enabled)
            return;

        if (rateLimit.MaxPlaysPerWindow <= 0)
            warnings.Add(label + " has Rate Limit enabled but Max Plays Per Window is not positive.");

        if (rateLimit.WindowSeconds <= 0f)
            warnings.Add(label + " has Rate Limit enabled but Window Seconds is not positive.");
    }

    /// <summary>
    /// Checks whether a binding list contains one event ID.
    /// </summary>
    /// <param name="bindings">Binding list to search.</param>
    /// <param name="eventId">Event ID to find.</param>
    /// <returns>True when at least one binding uses the event ID.</returns>
    private static bool ContainsEvent(IReadOnlyList<GameAudioEventBinding> bindings, GameAudioEventId eventId)
    {
        for (int index = 0; index < bindings.Count; index++)
        {
            GameAudioEventBinding binding = bindings[index];

            if (binding == null)
                continue;

            if (binding.EventId != eventId)
                continue;

            return true;
        }

        return false;
    }
    #endregion

    #endregion
}
