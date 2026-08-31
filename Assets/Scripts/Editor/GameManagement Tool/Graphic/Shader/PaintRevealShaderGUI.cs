using UnityEditor;
using UnityEngine;

/// <summary>
/// Presents the Paint Reveal material contract with concise guidance for every authorable shader value.
/// </summary>
public sealed class PaintRevealShaderGUI : ShaderGUI
{
    #region Methods

    #region Unity Methods
    /// <summary>
    /// Draws reveal, paint-shape, and rendered-rect settings while UGUI retains stencil ownership.
    /// </summary>
    /// <param name="materialEditor">Unity material editor applying property changes.</param>
    /// <param name="properties">Properties declared by the selected Paint Reveal shader.</param>
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        DrawProperty(materialEditor,
                     properties,
                     "_DepositionTex",
                     "Aerosol Deposition Map",
                     "Stores the normalized arrival time of overlapping spray deposits. Lower values appear earlier during the reveal.");
        DrawProperty(materialEditor,
                     properties,
                     "_Color",
                     "Graphic Tint",
                     "Multiplies the source UI graphic color and alpha.");
        DrawProperty(materialEditor,
                     properties,
                     "_FadeProgress",
                     "Animation Progress",
                     "Advances the selected deposit or removal operation from zero to one. Runtime presentation normally owns this value.");
        DrawProperty(materialEditor,
                     properties,
                     "_FadeMode",
                     "Coverage Mode",
                     "Selects opacity, directional gradient, distributed aerosol accumulation, or directional aerosol progression.");
        DrawProperty(materialEditor,
                     properties,
                     "_FadeDirection",
                     "Coverage Direction",
                     "Selects left-to-right, right-to-left, bottom-to-top, or top-to-bottom frontier movement.");
        DrawProperty(materialEditor,
                     properties,
                     "_PaintOperation",
                     "Coverage Operation",
                     "Deposits pigment behind the moving frontier or removes existing pigment behind it.");
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Gradient", EditorStyles.boldLabel);
        DrawProperty(materialEditor,
                     properties,
                     "_EdgeSoftness",
                     "Gradient Edge Softness",
                     "Sets the normalized half-width of the legacy directional-gradient boundary.");
        DrawProperty(materialEditor,
                     properties,
                     "_NoiseStrength",
                     "Gradient Variation",
                     "Sets the maximum procedural displacement applied to a directional-gradient boundary.");
        DrawProperty(materialEditor,
                     properties,
                     "_NoiseScale",
                     "Gradient Variation Scale",
                     "Sets the spatial frequency of directional-gradient displacement.");
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Aerosol Deposition", EditorStyles.boldLabel);
        DrawProperty(materialEditor,
                     properties,
                     "_DepositSoftness",
                     "Deposit Edge Softness",
                     "Controls antialiasing around newly deposited pigment without blurring the final silhouette.");
        DrawProperty(materialEditor,
                     properties,
                     "_DepositVariation",
                     "Deposit Time Variation",
                     "Offsets local arrival times so wet paint islands overlap instead of forming a continuous wipe edge.");
        DrawProperty(materialEditor,
                     properties,
                     "_DepositScale",
                     "Deposit Cluster Scale",
                     "Controls the repeated deposition-map scale after correcting for the rendered rectangle aspect ratio.");
        DrawProperty(materialEditor,
                     properties,
                     "_MistStrength",
                     "Aerosol Mist Strength",
                     "Controls granular breakup and early satellite droplets around active deposits.");
        DrawProperty(materialEditor,
                     properties,
                     "_MistScale",
                     "Aerosol Mist Density",
                     "Controls the spatial density of fine aerosol particles around active deposit edges.");
        DrawProperty(materialEditor,
                     properties,
                     "_AspectRatio",
                     "Rendered Rect Aspect Ratio",
                     "Keeps procedural paint details proportional on full-screen and banner-shaped UI rectangles.");
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Draws one optional shader property with an explicit tooltip.
    /// </summary>
    /// <param name="materialEditor">Unity material editor receiving the control.</param>
    /// <param name="properties">Complete shader property collection.</param>
    /// <param name="propertyName">Stable shader property name.</param>
    /// <param name="label">Concise field label.</param>
    /// <param name="tooltip">Explanation displayed when the field is hovered.</param>
    private static void DrawProperty(MaterialEditor materialEditor,
                                     MaterialProperty[] properties,
                                     string propertyName,
                                     string label,
                                     string tooltip)
    {
        MaterialProperty property = FindProperty(propertyName, properties, false);

        if (property != null)
            materialEditor.ShaderProperty(property, new GUIContent(label, tooltip));
    }
    #endregion

    #endregion
}
