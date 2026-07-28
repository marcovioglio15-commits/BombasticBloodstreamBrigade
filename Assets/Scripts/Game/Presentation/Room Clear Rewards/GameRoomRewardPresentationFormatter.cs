using System.Globalization;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Formats player grant events and portal previews through one shared stat/resource mapping path.
/// </summary>
public static class GameRoomRewardPresentationFormatter
{
    #region Constants
    private const string unavailableValueSummary = "value unavailable";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Formats one actual player grant or future-room schedule using its baked mapping.
    /// </summary>
    /// <param name="rewardEvent">Post-clamp grant event emitted by the authoritative reward system.</param>
    /// <param name="mappings">Baked target presentation mappings.</param>
    /// <returns>Immutable descriptor consumed by the preauthored player log.</returns>
    public static GameRoomRewardPresentationItem FormatPlayerEvent(
        in PlayerRoomRewardPresentationEvent rewardEvent,
        DynamicBuffer<GameRoomRewardPresentationElement> mappings)
    {
        GameRoomRewardPresentationElement mapping =
            ResolveMapping(rewardEvent.PresentationMappingIndex, mappings, out bool hasMapping);
        string fallbackLabel = ResolveFallbackLabel(rewardEvent.TargetDomain,
                                                    rewardEvent.TargetStatName.ToString(),
                                                    rewardEvent.Resource);
        string label = hasMapping && !mapping.DisplayLabel.IsEmpty
            ? mapping.DisplayLabel.ToString()
            : fallbackLabel;
        string valueSummary = rewardEvent.StartsNextRoom != 0
            ? ResolveScheduledValueSummary(in rewardEvent)
            : ResolveAppliedValueSummary(in rewardEvent);
        string durationSummary = ResolveDurationSummary(rewardEvent.StartsNextRoom != 0,
                                                        rewardEvent.IsTemporary != 0,
                                                        rewardEvent.DurationRooms);
        string text = string.Concat(label, " ", valueSummary, durationSummary);
        return BuildItem(text, mapping, hasMapping);
    }

    /// <summary>
    /// Formats one destination module preview from authored flat data or a non-mutating formula result.
    /// </summary>
    /// <param name="module">Flattened atomic reward module.</param>
    /// <param name="quantity">Combined module and room-reward quantity represented by this entry.</param>
    /// <param name="mappings">Baked target presentation mappings.</param>
    /// <param name="formulaBaseValue">Current typed target value used as [this] by a formula module.</param>
    /// <param name="formulaResult">Typed formula result resolved from the current authoritative player snapshot.</param>
    /// <param name="hasFormulaResult">True when a formula module was evaluated successfully.</param>
    /// <returns>Immutable descriptor consumed by a portal log.</returns>
    public static GameRoomRewardPresentationItem FormatPortalModule(
        in GameRoomRewardModuleElement module,
        int quantity,
        DynamicBuffer<GameRoomRewardPresentationElement> mappings,
        in PlayerFormulaValue formulaBaseValue,
        in PlayerFormulaValue formulaResult,
        bool hasFormulaResult)
    {
        GameRoomRewardPresentationElement mapping =
            ResolveMapping(module.PresentationMappingIndex, mappings, out bool hasMapping);
        string fallbackLabel = ResolveFallbackLabel(module.TargetDomain,
                                                    module.TargetStatName.ToString(),
                                                    module.Resource);
        string label = hasMapping && !mapping.DisplayLabel.IsEmpty
            ? mapping.DisplayLabel.ToString()
            : fallbackLabel;
        string valueSummary = ResolveModulePreviewValue(in module,
                                                        in formulaBaseValue,
                                                        in formulaResult,
                                                        hasFormulaResult);
        string durationSummary = module.Duration == GameRoomRewardDuration.Temporary
            ? string.Format(CultureInfo.InvariantCulture,
                            " (next {0} room{1})",
                            module.DurationRooms,
                            module.DurationRooms == 1 ? string.Empty : "s")
            : string.Empty;
        string quantitySummary = quantity > 1
            ? string.Format(CultureInfo.InvariantCulture, " ×{0}", quantity)
            : string.Empty;
        string text = string.Concat(label,
                                    " ",
                                    valueSummary,
                                    durationSummary,
                                    quantitySummary);
        return BuildItem(text, mapping, hasMapping);
    }
    #endregion

    #region Value Formatting
    /// <summary>
    /// Formats the actual post-clamp value carried by a player event.
    /// </summary>
    /// <param name="rewardEvent">Applied player reward event.</param>
    /// <returns>Short typed value summary.</returns>
    private static string ResolveAppliedValueSummary(
        in PlayerRoomRewardPresentationEvent rewardEvent)
    {
        if (rewardEvent.TargetDomain == GameRoomRewardTargetDomain.Resource)
            return FormatSignedNumber(rewardEvent.NumericDelta, false);

        switch (rewardEvent.StatType)
        {
            case PlayerScalableStatType.Boolean:
                return rewardEvent.BooleanValue != 0 ? "enabled" : "disabled";
            case PlayerScalableStatType.Token:
                return rewardEvent.TokenValue.IsEmpty
                    ? "updated"
                    : rewardEvent.TokenValue.ToString();
            case PlayerScalableStatType.Integer:
            case PlayerScalableStatType.Unsigned:
                return FormatSignedNumber(rewardEvent.NumericDelta, true);
            default:
                return FormatSignedNumber(rewardEvent.NumericDelta, false);
        }
    }

    /// <summary>
    /// Formats a future-room schedule from its resolved acquisition-time projection.
    /// </summary>
    /// <param name="rewardEvent">Scheduled temporary reward event.</param>
    /// <returns>Short configured-value summary.</returns>
    private static string ResolveScheduledValueSummary(
        in PlayerRoomRewardPresentationEvent rewardEvent)
    {
        if (rewardEvent.ValueSource == GameRoomRewardValueSource.Formula)
            return unavailableValueSummary;

        if (rewardEvent.TargetDomain == GameRoomRewardTargetDomain.Resource)
            return FormatSignedNumber(rewardEvent.NumericDelta, false);

        switch (rewardEvent.StatType)
        {
            case PlayerScalableStatType.Boolean:
                return rewardEvent.BooleanValue != 0 ? "enabled" : "disabled";
            case PlayerScalableStatType.Token:
                return rewardEvent.TokenValue.IsEmpty
                    ? "updated"
                    : rewardEvent.TokenValue.ToString();
            case PlayerScalableStatType.Integer:
            case PlayerScalableStatType.Unsigned:
                return FormatSignedNumber(rewardEvent.NumericDelta, true);
            default:
                return FormatSignedNumber(rewardEvent.NumericDelta, false);
        }
    }

    /// <summary>
    /// Formats a flattened module from its authored flat payload or resolved formula preview.
    /// </summary>
    /// <param name="module">Module whose authored value is summarized.</param>
    /// <param name="formulaBaseValue">Current typed value supplied to the formula as [this].</param>
    /// <param name="formulaResult">Typed formula result resolved against the current player snapshot.</param>
    /// <param name="hasFormulaResult">True when formula evaluation succeeded.</param>
    /// <returns>Resolved formula result or typed flat value.</returns>
    private static string ResolveModulePreviewValue(
        in GameRoomRewardModuleElement module,
        in PlayerFormulaValue formulaBaseValue,
        in PlayerFormulaValue formulaResult,
        bool hasFormulaResult)
    {
        if (module.ValueSource == GameRoomRewardValueSource.Formula)
            return ResolveFormulaPreviewValue(in module,
                                              in formulaBaseValue,
                                              in formulaResult,
                                              hasFormulaResult);

        if (module.TargetDomain == GameRoomRewardTargetDomain.Resource)
            return FormatSignedNumber(module.FlatNumericValue, false);

        switch (module.TargetStatType)
        {
            case PlayerScalableStatType.Boolean:
                return module.FlatBooleanValue != 0 ? "enabled" : "disabled";
            case PlayerScalableStatType.Token:
                return module.FlatTokenValue.IsEmpty
                    ? "updated"
                    : module.FlatTokenValue.ToString();
            case PlayerScalableStatType.Integer:
            case PlayerScalableStatType.Unsigned:
                return FormatSignedNumber(module.FlatNumericValue, true);
            default:
                return FormatSignedNumber(module.FlatNumericValue, false);
        }
    }

    /// <summary>
    /// Formats a typed formula result using the same delta semantics shown after an authoritative player grant.
    /// </summary>
    /// <param name="module">Formula module defining target domain and stat type.</param>
    /// <param name="formulaBaseValue">Current typed target value supplied to the formula as [this].</param>
    /// <param name="formulaResult">Typed formula output resolved by the shared runtime evaluator.</param>
    /// <param name="hasFormulaResult">True when the evaluator produced a target-compatible result.</param>
    /// <returns>Short resolved value summary or an actionable unavailable fallback.</returns>
    private static string ResolveFormulaPreviewValue(
        in GameRoomRewardModuleElement module,
        in PlayerFormulaValue formulaBaseValue,
        in PlayerFormulaValue formulaResult,
        bool hasFormulaResult)
    {
        if (!hasFormulaResult || !formulaResult.IsValid)
            return unavailableValueSummary;

        if (module.TargetDomain == GameRoomRewardTargetDomain.Resource)
        {
            return formulaResult.Type == PlayerFormulaValueType.Number
                ? FormatSignedNumber(formulaResult.NumberValue, false)
                : unavailableValueSummary;
        }

        switch (module.TargetStatType)
        {
            case PlayerScalableStatType.Boolean:
                return formulaResult.Type == PlayerFormulaValueType.Boolean
                    ? (formulaResult.BooleanValue ? "enabled" : "disabled")
                    : unavailableValueSummary;
            case PlayerScalableStatType.Token:
                return formulaResult.Type == PlayerFormulaValueType.Token &&
                       !string.IsNullOrWhiteSpace(formulaResult.TokenValue)
                    ? formulaResult.TokenValue
                    : unavailableValueSummary;
            case PlayerScalableStatType.Integer:
            case PlayerScalableStatType.Unsigned:
                return FormatFormulaNumericDelta(in formulaBaseValue,
                                                 in formulaResult,
                                                 true);
            default:
                return FormatFormulaNumericDelta(in formulaBaseValue,
                                                 in formulaResult,
                                                 false);
        }
    }

    /// <summary>
    /// Converts an absolute numeric stat formula result into the signed delta used by reward presentation.
    /// </summary>
    /// <param name="formulaBaseValue">Current numeric stat value supplied to the formula.</param>
    /// <param name="formulaResult">Absolute numeric stat value returned by the formula.</param>
    /// <param name="integral">True when decimal digits must be suppressed for the target stat type.</param>
    /// <returns>Signed numeric delta or the unavailable fallback when either value is not numeric.</returns>
    private static string FormatFormulaNumericDelta(
        in PlayerFormulaValue formulaBaseValue,
        in PlayerFormulaValue formulaResult,
        bool integral)
    {
        if (formulaBaseValue.Type != PlayerFormulaValueType.Number ||
            formulaResult.Type != PlayerFormulaValueType.Number)
        {
            return unavailableValueSummary;
        }

        return FormatSignedNumber(formulaResult.NumberValue -
                                  formulaBaseValue.NumberValue,
                                  integral);
    }

    /// <summary>
    /// Formats future-room or active-temporary duration context.
    /// </summary>
    /// <param name="startsNextRoom">True for a newly acquired future-room schedule.</param>
    /// <param name="temporary">True for temporary effects.</param>
    /// <param name="durationRooms">Configured or currently represented room duration.</param>
    /// <returns>Compact duration suffix.</returns>
    private static string ResolveDurationSummary(bool startsNextRoom,
                                                 bool temporary,
                                                 int durationRooms)
    {
        if (startsNextRoom)
        {
            return string.Format(CultureInfo.InvariantCulture,
                                 " (next {0} room{1})",
                                 durationRooms,
                                 durationRooms == 1 ? string.Empty : "s");
        }

        return temporary ? " (temporary)" : string.Empty;
    }

    /// <summary>
    /// Formats a signed numeric delta using invariant compact notation.
    /// </summary>
    /// <param name="value">Numeric delta or authored flat value.</param>
    /// <param name="integral">True when decimal digits must be suppressed.</param>
    /// <returns>Signed invariant numeric string.</returns>
    private static string FormatSignedNumber(float value, bool integral)
    {
        string format = integral ? "+0;-0;0" : "+0.##;-0.##;0";
        return value.ToString(format, CultureInfo.InvariantCulture);
    }
    #endregion

    #region Mapping
    /// <summary>
    /// Resolves one optional baked mapping by flattened index.
    /// </summary>
    /// <param name="mappingIndex">Flattened mapping index stored by a module or event.</param>
    /// <param name="mappings">Baked mapping buffer.</param>
    /// <param name="hasMapping">True when the index resolves to a valid mapping.</param>
    /// <returns>Resolved mapping or its default value.</returns>
    private static GameRoomRewardPresentationElement ResolveMapping(
        int mappingIndex,
        DynamicBuffer<GameRoomRewardPresentationElement> mappings,
        out bool hasMapping)
    {
        hasMapping = mappingIndex >= 0 && mappingIndex < mappings.Length;
        return hasMapping ? mappings[mappingIndex] : default;
    }

    /// <summary>
    /// Builds a mapped descriptor with deterministic white-text fallback.
    /// </summary>
    /// <param name="text">Fully formatted text summary.</param>
    /// <param name="mapping">Resolved mapping when available.</param>
    /// <param name="hasMapping">True when mapping data is valid.</param>
    /// <returns>Immutable text or sprite descriptor.</returns>
    private static GameRoomRewardPresentationItem BuildItem(
        string text,
        in GameRoomRewardPresentationElement mapping,
        bool hasMapping)
    {
        Color color = hasMapping
            ? new Color(mapping.TextColor.x,
                        mapping.TextColor.y,
                        mapping.TextColor.z,
                        mapping.TextColor.w)
            : Color.white;
        Sprite sprite = hasMapping ? mapping.Sprite.Value : null;
        bool useSprite = hasMapping &&
                         mapping.Mode == GameRoomRewardPresentationMode.Sprite &&
                         sprite != null;
        string spriteCaption = useSprite && !mapping.SpriteCaption.IsEmpty
            ? mapping.SpriteCaption.ToString()
            : string.Empty;
        return new GameRoomRewardPresentationItem(text,
                                                   spriteCaption,
                                                   color,
                                                   sprite,
                                                   useSprite);
    }

    /// <summary>
    /// Resolves a readable default label when a target has no optional custom mapping.
    /// </summary>
    /// <param name="targetDomain">Stat or resource domain.</param>
    /// <param name="targetStatName">Scalable stat name when applicable.</param>
    /// <param name="resource">Resource target when applicable.</param>
    /// <returns>Readable nonempty fallback label.</returns>
    private static string ResolveFallbackLabel(GameRoomRewardTargetDomain targetDomain,
                                               string targetStatName,
                                               GameRoomRewardResource resource)
    {
        if (targetDomain == GameRoomRewardTargetDomain.ScalableStat)
            return string.IsNullOrWhiteSpace(targetStatName) ? "Stat" : targetStatName;

        switch (resource)
        {
            case GameRoomRewardResource.Health:
                return "Health";
            case GameRoomRewardResource.PrimaryPowerUpEnergy:
                return "Primary Energy";
            case GameRoomRewardResource.SecondaryPowerUpEnergy:
                return "Secondary Energy";
            case GameRoomRewardResource.Experience:
                return "Experience";
            default:
                return "Resource";
        }
    }
    #endregion

    #endregion
}
