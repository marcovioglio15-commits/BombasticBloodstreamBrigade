using System;
using UnityEngine;

/// <summary>
/// Stores direct colors used by one procedural player syringe.
/// </summary>
[Serializable]
public sealed class PlayerSyringePaletteSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Near-black or colored line surrounding the complete syringe silhouette.")]
    [SerializeField] private Color outline = new Color(0.04f, 0.025f, 0.035f, 1f);

    [Tooltip("Primary flat-shaded color used by the syringe body and graduation plate.")]
    [SerializeField] private Color body = new Color(0.36f, 0.16f, 0.19f, 1f);

    [Tooltip("Secondary faceted shade used to separate body planes.")]
    [SerializeField] private Color bodyShadow = new Color(0.17f, 0.075f, 0.105f, 1f);

    [Tooltip("Color used by the internal empty chamber.")]
    [SerializeField] private Color chamber = new Color(0.28f, 0.16f, 0.24f, 0.82f);

    [Tooltip("Primary color of the liquid.")]
    [SerializeField] private Color liquid = new Color(0.72f, 0.16f, 0.2f, 1f);

    [Tooltip("Secondary flat-shaded liquid color used by waves and depth layers.")]
    [SerializeField] private Color liquidHighlight = new Color(0.95f, 0.34f, 0.35f, 1f);

    [Tooltip("Color used by procedural air bubbles.")]
    [SerializeField] private Color bubbles = new Color(1f, 0.58f, 0.56f, 0.72f);

    [Tooltip("Color used by procedural graduation ticks.")]
    [SerializeField] private Color graduation = new Color(0.9f, 0.78f, 0.32f, 1f);

    [Tooltip("Direct TextMeshPro color used by numeric graduation labels.")]
    [SerializeField] private Color label = new Color(0.035f, 0.025f, 0.035f, 1f);

    [Tooltip("Direct TextMeshPro outline color used to keep numeric graduation labels readable.")]
    [SerializeField] private Color labelOutline = new Color(0.95f, 0.78f, 0.32f, 0.9f);

    [Tooltip("Primary color used by the moving-plunger frame; its outer edge uses the outline color.")]
    [SerializeField] private Color plunger = new Color(0.16f, 0.15f, 0.15f, 1f);

    [Tooltip("Semitransparent color used by the readable central window inside the simplified moving plunger.")]
    [SerializeField] private Color plungerWindow = new Color(0.62f, 0.64f, 0.62f, 0.38f);

    [Tooltip("Direct color used by the simplified square syringe termination outline.")]
    [SerializeField] private Color terminationOutline = new Color(0.04f, 0.025f, 0.035f, 1f);

    [Tooltip("Color used inside the simplified square syringe termination.")]
    [SerializeField] private Color terminationInterior = new Color(0.36f, 0.16f, 0.19f, 1f);
    #endregion

    #endregion

    #region Properties
    public Color Outline => outline;
    public Color Body => body;
    public Color BodyShadow => bodyShadow;
    public Color Chamber => chamber;
    public Color Liquid => liquid;
    public Color LiquidHighlight => liquidHighlight;
    public Color Bubbles => bubbles;
    public Color Graduation => graduation;
    public Color Label => label;
    public Color LabelOutline => labelOutline;
    public Color Plunger => plunger;
    public Color PlungerWindow => plungerWindow;
    public Color TerminationOutline => terminationOutline;
    public Color TerminationInterior => terminationInterior;
    #endregion

    #region Methods

    #region Construction
    /// <summary>
    /// Creates the default health-oriented direct palette.
    /// </summary>
    public PlayerSyringePaletteSettings()
    {
    }

    /// <summary>
    /// Creates a direct palette optionally initialized with the default shield-oriented colors.
    /// </summary>
    /// <param name="useShieldPalette">True when the palette should use shield-oriented purple defaults.</param>
    public PlayerSyringePaletteSettings(bool useShieldPalette)
    {
        if (!useShieldPalette)
            return;

        outline = new Color(0.04f, 0.025f, 0.055f, 1f);
        body = new Color(0.28f, 0.13f, 0.38f, 1f);
        bodyShadow = new Color(0.12f, 0.055f, 0.19f, 1f);
        chamber = new Color(0.24f, 0.15f, 0.33f, 0.82f);
        liquid = new Color(0.42f, 0.16f, 0.68f, 1f);
        liquidHighlight = new Color(0.68f, 0.36f, 0.92f, 1f);
        bubbles = new Color(0.78f, 0.58f, 1f, 0.72f);
        terminationOutline = outline;
        terminationInterior = new Color(0.28f, 0.13f, 0.38f, 1f);
    }
    #endregion

    #endregion
}
