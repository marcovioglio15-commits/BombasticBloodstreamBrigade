using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Holds strongly typed authored UI references used by the Synchro Meter editor presentation path.
/// </summary>
internal readonly struct GameHudSynchroMeterSceneBindings
{
    #region Fields
    public readonly GameObject RootObject;
    public readonly Image BackgroundImage;
    public readonly Image CoverImage;
    public readonly Image PrimaryWaveLeadingImage;
    public readonly Image PrimaryWaveTrailingImage;
    public readonly Image SecondaryWaveLeadingImage;
    public readonly Image SecondaryWaveTrailingImage;
    public readonly TMP_Text RankText;
    public readonly TMP_Text ValueText;
    public readonly Image ProgressFillImage;
    public readonly Image ProgressBackgroundImage;
    public readonly TMP_Text ProgressionText;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates an immutable binding collection for one authored Synchro Meter.
    /// </summary>
    /// <param name="rootObject">Root object controlling complete meter visibility.</param>
    /// <param name="backgroundImage">Background reticle layer.</param>
    /// <param name="coverImage">Optional reticle cover layer.</param>
    /// <param name="primaryWaveLeadingImage">Leading primary wave tile.</param>
    /// <param name="primaryWaveTrailingImage">Trailing primary wave tile.</param>
    /// <param name="secondaryWaveLeadingImage">Leading secondary wave tile.</param>
    /// <param name="secondaryWaveTrailingImage">Trailing secondary wave tile.</param>
    /// <param name="rankText">Current rank label.</param>
    /// <param name="valueText">Current numeric value label.</param>
    /// <param name="progressFillImage">Progress bar fill.</param>
    /// <param name="progressBackgroundImage">Progress bar track.</param>
    /// <param name="progressionText">Optional tokenized progression label.</param>
    public GameHudSynchroMeterSceneBindings(GameObject rootObject,
                                           Image backgroundImage,
                                           Image coverImage,
                                           Image primaryWaveLeadingImage,
                                           Image primaryWaveTrailingImage,
                                           Image secondaryWaveLeadingImage,
                                           Image secondaryWaveTrailingImage,
                                           TMP_Text rankText,
                                           TMP_Text valueText,
                                           Image progressFillImage,
                                           Image progressBackgroundImage,
                                           TMP_Text progressionText)
    {
        RootObject = rootObject;
        BackgroundImage = backgroundImage;
        CoverImage = coverImage;
        PrimaryWaveLeadingImage = primaryWaveLeadingImage;
        PrimaryWaveTrailingImage = primaryWaveTrailingImage;
        SecondaryWaveLeadingImage = secondaryWaveLeadingImage;
        SecondaryWaveTrailingImage = secondaryWaveTrailingImage;
        RankText = rankText;
        ValueText = valueText;
        ProgressFillImage = progressFillImage;
        ProgressBackgroundImage = progressBackgroundImage;
        ProgressionText = progressionText;
    }
    #endregion
}

/// <summary>
/// Stores all authored values temporarily replaced on one Scene Synchro Meter.
/// </summary>
internal sealed class GameHudSynchroMeterScenePresentationState
{
    #region Fields
    private readonly GameObject rootObject;
    private readonly bool rootWasActive;
    private readonly CanvasGroup canvasGroup;
    private readonly float canvasAlpha;
    private readonly List<GameHudSynchroMeterGraphicPresentationState> graphicStates;
    private readonly GameHudSynchroMeterTextPresentationState textState;
    public readonly int SceneHandle;
    #endregion

    #region Constructors
    /// <summary>
    /// Captures one authored meter before selected-preset values are reflected in place.
    /// </summary>
    /// <param name="section">Authored section identifying the owning Scene.</param>
    /// <param name="rootObject">Root object whose active state is replaced temporarily.</param>
    /// <param name="canvasGroup">Optional root CanvasGroup receiving a fully visible editor alpha.</param>
    /// <param name="graphicStates">Original states for all assigned graphics.</param>
    /// <param name="progressionText">Progression label whose original content and layout are captured.</param>
    public GameHudSynchroMeterScenePresentationState(HUDComboCounterSection section,
                                                     GameObject rootObject,
                                                     CanvasGroup canvasGroup,
                                                     List<GameHudSynchroMeterGraphicPresentationState> graphicStates,
                                                     TMP_Text progressionText)
    {
        this.rootObject = rootObject;
        rootWasActive = rootObject != null && rootObject.activeSelf;
        this.canvasGroup = canvasGroup;
        canvasAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        this.graphicStates = graphicStates;
        textState = new GameHudSynchroMeterTextPresentationState(progressionText);
        SceneHandle = section.gameObject.scene.handle;
    }
    #endregion

    #region Methods
    /// <summary>
    /// Restores all authored presentation values captured for this Scene meter.
    /// </summary>
    public void Restore()
    {
        textState.Restore();

        // Restore every graphic before the root active state can hide its descendants.
        for (int stateIndex = 0; stateIndex < graphicStates.Count; stateIndex++)
            graphicStates[stateIndex].Restore();

        if (canvasGroup != null)
            canvasGroup.alpha = canvasAlpha;

        if (rootObject != null)
            rootObject.SetActive(rootWasActive);
    }
    #endregion
}

/// <summary>
/// Stores the original enabled and color values for one authored Synchro Meter graphic.
/// </summary>
internal readonly struct GameHudSynchroMeterGraphicPresentationState
{
    #region Fields
    public readonly Graphic Graphic;
    private readonly bool wasEnabled;
    private readonly Color color;
    #endregion

    #region Constructors
    /// <summary>
    /// Captures one authored graphic before editor presentation values are applied.
    /// </summary>
    /// <param name="graphic">Assigned graphic whose original values are stored.</param>
    public GameHudSynchroMeterGraphicPresentationState(Graphic graphic)
    {
        Graphic = graphic;
        wasEnabled = graphic.enabled;
        color = graphic.color;
    }
    #endregion

    #region Methods
    /// <summary>
    /// Restores the captured graphic when its Scene object still exists.
    /// </summary>
    public void Restore()
    {
        if (Graphic == null)
            return;

        Graphic.enabled = wasEnabled;
        Graphic.color = color;
    }
    #endregion
}

/// <summary>
/// Stores original content and layout values for the authored progression TMP label.
/// </summary>
internal readonly struct GameHudSynchroMeterTextPresentationState
{
    #region Fields
    private readonly TMP_Text text;
    private readonly string content;
    private readonly float fontSize;
    private readonly bool autoSizing;
    private readonly TextAlignmentOptions alignment;
    private readonly Vector2 anchoredPosition;
    #endregion

    #region Constructors
    /// <summary>
    /// Captures optional progression-label values before the selected preset is reflected.
    /// </summary>
    /// <param name="text">Assigned progression label, or null when the binding is incomplete.</param>
    public GameHudSynchroMeterTextPresentationState(TMP_Text text)
    {
        this.text = text;
        content = text != null ? text.text : string.Empty;
        fontSize = text != null ? text.fontSize : 0f;
        autoSizing = text != null && text.enableAutoSizing;
        alignment = text != null ? text.alignment : TextAlignmentOptions.Center;
        anchoredPosition = text != null ? text.rectTransform.anchoredPosition : Vector2.zero;
    }
    #endregion

    #region Methods
    /// <summary>
    /// Restores the captured progression-label values when its Scene object still exists.
    /// </summary>
    public void Restore()
    {
        if (text == null)
            return;

        text.text = content;
        text.fontSize = fontSize;
        text.enableAutoSizing = autoSizing;
        text.alignment = alignment;
        text.rectTransform.anchoredPosition = anchoredPosition;
    }
    #endregion
}
