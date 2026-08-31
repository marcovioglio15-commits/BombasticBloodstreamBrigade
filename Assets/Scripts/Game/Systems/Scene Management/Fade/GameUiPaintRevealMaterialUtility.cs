using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Selects one shader presentation while retaining numeric compatibility with scene fade modes.
/// </summary>
public enum GameUiPaintRevealMode : byte
{
    Uniform = 0,
    DirectionalGradient = 1,
    UniformCloud = 2,
    DirectionalSweep = 3
}

/// <summary>
/// Selects whether normalized progress deposits pigment or removes existing coverage.
/// </summary>
public enum GameUiPaintRevealOperation : byte
{
    Deposit = 0,
    Remove = 1
}

/// <summary>
/// Captures every runtime-controlled Paint Reveal material value for deterministic Editor restoration.
/// </summary>
public readonly struct GameUiPaintRevealMaterialState
{
    #region Fields
    public readonly float Progress;
    public readonly float Mode;
    public readonly float Direction;
    public readonly float Operation;
    public readonly float GradientSoftness;
    public readonly float GradientVariation;
    public readonly float GradientScale;
    public readonly float DepositSoftness;
    public readonly float DepositVariation;
    public readonly float DepositScale;
    public readonly float MistStrength;
    public readonly float MistScale;
    public readonly float AspectRatio;
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Stores one complete authored shader state before transient presentation values are applied.
    /// </summary>
    /// <param name="progress">Authored reveal progress.</param>
    /// <param name="mode">Authored reveal mode.</param>
    /// <param name="direction">Authored screen-space direction.</param>
    /// <param name="operation">Authored deposit or removal operation.</param>
    /// <param name="gradientSoftness">Authored directional-gradient softness.</param>
    /// <param name="gradientVariation">Authored directional-gradient variation.</param>
    /// <param name="gradientScale">Authored directional-gradient variation scale.</param>
    /// <param name="depositSoftness">Authored deposit edge softness.</param>
    /// <param name="depositVariation">Authored local deposit timing variation.</param>
    /// <param name="depositScale">Authored deposit cluster scale.</param>
    /// <param name="mistStrength">Authored aerosol mist strength.</param>
    /// <param name="mistScale">Authored aerosol mist density.</param>
    /// <param name="aspectRatio">Authored rendered-rectangle aspect ratio.</param>
    public GameUiPaintRevealMaterialState(float progress,
                                          float mode,
                                          float direction,
                                          float operation,
                                          float gradientSoftness,
                                          float gradientVariation,
                                          float gradientScale,
                                          float depositSoftness,
                                          float depositVariation,
                                          float depositScale,
                                          float mistStrength,
                                          float mistScale,
                                          float aspectRatio)
    {
        Progress = progress;
        Mode = mode;
        Direction = direction;
        Operation = operation;
        GradientSoftness = gradientSoftness;
        GradientVariation = gradientVariation;
        GradientScale = gradientScale;
        DepositSoftness = depositSoftness;
        DepositVariation = depositVariation;
        DepositScale = depositScale;
        MistStrength = mistStrength;
        MistScale = mistScale;
        AspectRatio = aspectRatio;
    }
    #endregion

    #endregion
}

/// <summary>
/// Centralizes the material contract shared by room announcements and full-screen scene transitions.
/// </summary>
public static class GameUiPaintRevealMaterialUtility
{
    #region Constants
    private static readonly int depositionTextureProperty = Shader.PropertyToID("_DepositionTex");
    private static readonly int progressProperty = Shader.PropertyToID("_FadeProgress");
    private static readonly int modeProperty = Shader.PropertyToID("_FadeMode");
    private static readonly int directionProperty = Shader.PropertyToID("_FadeDirection");
    private static readonly int operationProperty = Shader.PropertyToID("_PaintOperation");
    private static readonly int gradientSoftnessProperty = Shader.PropertyToID("_EdgeSoftness");
    private static readonly int gradientVariationProperty = Shader.PropertyToID("_NoiseStrength");
    private static readonly int gradientScaleProperty = Shader.PropertyToID("_NoiseScale");
    private static readonly int depositSoftnessProperty = Shader.PropertyToID("_DepositSoftness");
    private static readonly int depositVariationProperty = Shader.PropertyToID("_DepositVariation");
    private static readonly int depositScaleProperty = Shader.PropertyToID("_DepositScale");
    private static readonly int mistStrengthProperty = Shader.PropertyToID("_MistStrength");
    private static readonly int mistScaleProperty = Shader.PropertyToID("_MistScale");
    private static readonly int aspectRatioProperty = Shader.PropertyToID("_AspectRatio");
    #endregion

    #region Methods

    #region Contract Methods
    /// <summary>
    /// Verifies that one material exposes the complete deposition contract used by runtime presentation.
    /// </summary>
    /// <param name="material">Authored Paint Reveal material to inspect.</param>
    /// <returns>True when every required texture and scalar property is available.</returns>
    public static bool IsCompatible(Material material)
    {
        return material != null &&
               material.HasProperty(depositionTextureProperty) &&
               material.GetTexture(depositionTextureProperty) != null &&
               material.HasProperty(progressProperty) &&
               material.HasProperty(modeProperty) &&
               material.HasProperty(directionProperty) &&
               material.HasProperty(operationProperty) &&
               material.HasProperty(gradientSoftnessProperty) &&
               material.HasProperty(gradientVariationProperty) &&
               material.HasProperty(gradientScaleProperty) &&
               material.HasProperty(depositSoftnessProperty) &&
               material.HasProperty(depositVariationProperty) &&
               material.HasProperty(depositScaleProperty) &&
               material.HasProperty(mistStrengthProperty) &&
               material.HasProperty(mistScaleProperty) &&
               material.HasProperty(aspectRatioProperty);
    }
    #endregion

    #region Configuration Methods
    /// <summary>
    /// Applies all scene-transition gradient and aerosol parameters through one material update path.
    /// </summary>
    /// <param name="material">Dedicated full-screen transition material.</param>
    /// <param name="mode">Runtime scene fade mode mapped to a shader presentation.</param>
    /// <param name="direction">Screen-space transition direction.</param>
    /// <param name="operation">Deposit while covering or remove while revealing the scene.</param>
    /// <param name="gradientSoftness">Directional-gradient edge softness.</param>
    /// <param name="gradientVariation">Directional-gradient boundary variation.</param>
    /// <param name="gradientScale">Directional-gradient variation scale.</param>
    /// <param name="depositSoftness">Aerosol deposit edge softness.</param>
    /// <param name="depositVariation">Local aerosol arrival-time variation.</param>
    /// <param name="depositScale">Aerosol deposit cluster scale.</param>
    /// <param name="mistStrength">Fine aerosol mist breakup.</param>
    /// <param name="mistScale">Fine aerosol mist density.</param>
    /// <param name="aspectRatio">Rendered transition-surface width-to-height ratio.</param>
    public static void ConfigureSceneTransition(Material material,
                                                GameSceneFadeMode mode,
                                                GameSceneFadeWipeDirection direction,
                                                GameUiPaintRevealOperation operation,
                                                float gradientSoftness,
                                                float gradientVariation,
                                                float gradientScale,
                                                float depositSoftness,
                                                float depositVariation,
                                                float depositScale,
                                                float mistStrength,
                                                float mistScale,
                                                float aspectRatio)
    {
        material.SetFloat(modeProperty, (float)ResolveSceneMode(mode));
        material.SetFloat(directionProperty, (float)direction);
        material.SetFloat(operationProperty, (float)operation);
        material.SetFloat(gradientSoftnessProperty, math.clamp(gradientSoftness, 0.001f, 0.5f));
        material.SetFloat(gradientVariationProperty, math.clamp(gradientVariation, 0f, 0.25f));
        material.SetFloat(gradientScaleProperty, math.clamp(gradientScale, 0.25f, 24f));
        ConfigureAerosol(material,
                         depositSoftness,
                         depositVariation,
                         depositScale,
                         mistStrength,
                         mistScale,
                         aspectRatio);
    }

    /// <summary>
    /// Applies one directional aerosol operation used by stationary room-clear announcements.
    /// </summary>
    /// <param name="material">Base or stencil material receiving room-clear settings.</param>
    /// <param name="config">Baked room-clear configuration supplying direction and aerosol values.</param>
    /// <param name="direction">Independent screen-space direction for the active phase.</param>
    /// <param name="operation">Deposit for entry or remove for exit.</param>
    /// <param name="aspectRatio">Rendered announcement width-to-height ratio.</param>
    public static void ConfigureRoomClear(Material material,
                                          in GameHudWaveClearAnnouncementRuntimeConfig config,
                                          GameHudWaveClearAnnouncementDirection direction,
                                          GameUiPaintRevealOperation operation,
                                          float aspectRatio)
    {
        material.SetFloat(modeProperty, (float)GameUiPaintRevealMode.DirectionalSweep);
        material.SetFloat(directionProperty, (float)direction);
        material.SetFloat(operationProperty, (float)operation);
        ConfigureAerosol(material,
                         config.PaintEdgeSoftness,
                         config.PaintNoiseStrength,
                         config.PaintNoiseScale,
                         config.PaintBristleStrength,
                         config.PaintBristleScale,
                         aspectRatio);
    }

    /// <summary>
    /// Writes the current reveal progress to one compatible base or generated stencil material.
    /// </summary>
    /// <param name="material">Material receiving normalized reveal progress.</param>
    /// <param name="progress">Normalized paint coverage.</param>
    public static void SetProgress(Material material, float progress)
    {
        if (material != null && material.HasProperty(progressProperty))
            material.SetFloat(progressProperty, math.saturate(progress));
    }
    #endregion

    #region State Methods
    /// <summary>
    /// Captures every runtime-controlled value from a compatible material.
    /// </summary>
    /// <param name="material">Material whose authored values must survive Editor presentation.</param>
    /// <returns>Complete material state, or a zeroed state when the material is incompatible.</returns>
    public static GameUiPaintRevealMaterialState Capture(Material material)
    {
        if (!IsCompatible(material))
            return default;

        return new GameUiPaintRevealMaterialState(material.GetFloat(progressProperty),
                                                   material.GetFloat(modeProperty),
                                                   material.GetFloat(directionProperty),
                                                   material.GetFloat(operationProperty),
                                                   material.GetFloat(gradientSoftnessProperty),
                                                   material.GetFloat(gradientVariationProperty),
                                                   material.GetFloat(gradientScaleProperty),
                                                   material.GetFloat(depositSoftnessProperty),
                                                   material.GetFloat(depositVariationProperty),
                                                   material.GetFloat(depositScaleProperty),
                                                   material.GetFloat(mistStrengthProperty),
                                                   material.GetFloat(mistScaleProperty),
                                                   material.GetFloat(aspectRatioProperty));
    }

    /// <summary>
    /// Restores every runtime-controlled value after Editor presentation releases the material.
    /// </summary>
    /// <param name="material">Compatible material receiving its authored state.</param>
    /// <param name="state">Previously captured authored state.</param>
    public static void Restore(Material material, in GameUiPaintRevealMaterialState state)
    {
        if (!IsCompatible(material))
            return;

        material.SetFloat(progressProperty, state.Progress);
        material.SetFloat(modeProperty, state.Mode);
        material.SetFloat(directionProperty, state.Direction);
        material.SetFloat(operationProperty, state.Operation);
        material.SetFloat(gradientSoftnessProperty, state.GradientSoftness);
        material.SetFloat(gradientVariationProperty, state.GradientVariation);
        material.SetFloat(gradientScaleProperty, state.GradientScale);
        material.SetFloat(depositSoftnessProperty, state.DepositSoftness);
        material.SetFloat(depositVariationProperty, state.DepositVariation);
        material.SetFloat(depositScaleProperty, state.DepositScale);
        material.SetFloat(mistStrengthProperty, state.MistStrength);
        material.SetFloat(mistScaleProperty, state.MistScale);
        material.SetFloat(aspectRatioProperty, state.AspectRatio);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies values shared by every aerosol presentation after caller-specific mode selection.
    /// </summary>
    /// <param name="material">Compatible Paint Reveal material.</param>
    /// <param name="depositSoftness">Aerosol deposit edge softness.</param>
    /// <param name="depositVariation">Local aerosol arrival-time variation.</param>
    /// <param name="depositScale">Aerosol deposit cluster scale.</param>
    /// <param name="mistStrength">Fine aerosol mist breakup.</param>
    /// <param name="mistScale">Fine aerosol mist density.</param>
    /// <param name="aspectRatio">Rendered graphic width-to-height ratio.</param>
    private static void ConfigureAerosol(Material material,
                                         float depositSoftness,
                                         float depositVariation,
                                         float depositScale,
                                         float mistStrength,
                                         float mistScale,
                                         float aspectRatio)
    {
        material.SetFloat(depositSoftnessProperty, math.clamp(depositSoftness, 0.001f, 0.25f));
        material.SetFloat(depositVariationProperty, math.clamp(depositVariation, 0f, 0.5f));
        material.SetFloat(depositScaleProperty, math.clamp(depositScale, 0.25f, 12f));
        material.SetFloat(mistStrengthProperty, math.clamp(mistStrength, 0f, 0.25f));
        material.SetFloat(mistScaleProperty, math.clamp(mistScale, 1f, 96f));
        material.SetFloat(aspectRatioProperty, math.max(0.01f, aspectRatio));
    }

    /// <summary>
    /// Maps persisted scene fade modes to the shader presentation with matching numeric behavior.
    /// </summary>
    /// <param name="mode">Scene fade mode stored in ECS.</param>
    /// <returns>Paint Reveal shader mode.</returns>
    private static GameUiPaintRevealMode ResolveSceneMode(GameSceneFadeMode mode)
    {
        return mode switch
        {
            GameSceneFadeMode.DirectionalGradient => GameUiPaintRevealMode.DirectionalGradient,
            GameSceneFadeMode.UniformPaint => GameUiPaintRevealMode.UniformCloud,
            GameSceneFadeMode.DirectionalPaint => GameUiPaintRevealMode.DirectionalSweep,
            _ => GameUiPaintRevealMode.Uniform
        };
    }
    #endregion

    #endregion
}
