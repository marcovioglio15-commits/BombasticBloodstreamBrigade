using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Stores one designer-authored Laser Beam visual preset resolved by stable numeric ID at runtime.
/// </summary>
[Serializable]
public sealed class PlayerLaserBeamVisualPresetDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable numeric ID used by gameplay configs and Add Scaling formulas to select this visual preset at runtime.")]
    [SerializeField] private int stableId;

    [Tooltip("Designer-facing name shown in Player Management Tool selectors and helper labels.")]
    [SerializeField] private string displayName = "Electric Azure";

    [Tooltip("White-hot core color used by the innermost plasma filament of the beam.")]
    [SerializeField] private Color coreColor = Color.white;

    [Tooltip("Primary beam flow color used by the body volume and inner energy stream.")]
    [SerializeField] private Color flowColor = Color.white;

    [Tooltip("Electrical storm color used by the detached looped shell rendered around the beam body.")]
    [SerializeField] private Color stormColor = Color.white;

    [Tooltip("Contact color used by the rounded terminal cap highlights and by the separate wall-contact flare.")]
    [SerializeField] private Color contactColor = Color.white;
    #endregion

    #endregion

    #region Properties
    public int StableId
    {
        get
        {
            return stableId;
        }
    }

    public string DisplayName
    {
        get
        {
            return displayName;
        }
    }

    public Color FlowColor
    {
        get
        {
            return flowColor;
        }
    }

    public Color CoreColor
    {
        get
        {
            return coreColor;
        }
    }

    public Color StormColor
    {
        get
        {
            return stormColor;
        }
    }

    public Color ContactColor
    {
        get
        {
            return contactColor;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Overwrites this visual preset definition with one complete authored assignment.
    /// </summary>
    /// <param name="stableIdValue">Stable numeric ID.</param>
    /// <param name="displayNameValue">Designer-facing preset name.</param>
    /// <param name="coreColorValue">White-hot core color.</param>
    /// <param name="flowColorValue">Primary beam flow color.</param>
    /// <param name="stormColorValue">Electrical storm color.</param>
    /// <param name="contactColorValue">Contact highlight color.</param>
    public void Assign(int stableIdValue,
                       string displayNameValue,
                       Color coreColorValue,
                       Color flowColorValue,
                       Color stormColorValue,
                       Color contactColorValue)
    {
        stableId = stableIdValue;
        displayName = displayNameValue;
        coreColor = coreColorValue;
        flowColor = flowColorValue;
        stormColor = stormColorValue;
        contactColor = contactColorValue;
        Validate();
    }

    /// <summary>
    /// Clamps authored color alpha channels to a valid range after inspector edits.
    /// </summary>
    public void Validate()
    {
        coreColor.a = Mathf.Clamp01(coreColor.a);
        flowColor.a = Mathf.Clamp01(flowColor.a);
        stormColor.a = Mathf.Clamp01(stormColor.a);
        contactColor.a = Mathf.Clamp01(contactColor.a);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores editable shared assets and color-preset mappings used by the Laser Beam managed presentation runtime.
/// </summary>
[Serializable]
public sealed class PlayerLaserBeamVisualSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Material asset used by the layered Laser Beam body renderers.")]
    [FormerlySerializedAs("beamMaterial")]
    [SerializeField] private Material bodyMaterial;

    [Tooltip("Material asset used by the source aperture and discharge visuals rendered near the emission origin.")]
    [FormerlySerializedAs("sourceBubbleMaterial")]
    [SerializeField] private Material sourceEffectMaterial;

    [Tooltip("Material asset used by the rounded terminal cap and the separate wall-contact flare rendered at the end of each lane.")]
    [FormerlySerializedAs("impactSplashMaterial")]
    [SerializeField] private Material terminalCapMaterial;

    [Tooltip("Vertical lift in world units applied to Laser Beam visuals to avoid floor z-fighting.")]
    [SerializeField] private float verticalLift = PlayerLaserBeamVisualDefaultsUtility.DefaultVerticalLift;

    [Tooltip("Minimum rendered segment length used by the pooled Laser Beam body visuals.")]
    [SerializeField] private float minimumSegmentLength = PlayerLaserBeamVisualDefaultsUtility.DefaultMinimumSegmentLength;

    [Tooltip("Designer-defined Laser Beam visual presets resolved by stable numeric ID. Each preset owns a core, flow, storm, and contact color.")]
    [SerializeField] private List<PlayerLaserBeamVisualPresetDefinition> visualPresets = new List<PlayerLaserBeamVisualPresetDefinition>
    {
        PlayerLaserBeamVisualDefaultsUtility.CreateDefaultVisualPresetDefinition()
    };
    #endregion

    #endregion

    #region Properties
    public Material BodyMaterial
    {
        get
        {
            return bodyMaterial;
        }
    }

    public float VerticalLift
    {
        get
        {
            return verticalLift;
        }
    }

    public float MinimumSegmentLength
    {
        get
        {
            return minimumSegmentLength;
        }
    }

    public IReadOnlyList<PlayerLaserBeamVisualPresetDefinition> VisualPresets
    {
        get
        {
            return visualPresets;
        }
    }

    public Material SourceEffectMaterial
    {
        get
        {
            return sourceEffectMaterial;
        }
    }

    public Material TerminalCapMaterial
    {
        get
        {
            return terminalCapMaterial;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates material references and authored visual preset entries for Laser Beam visuals.
    /// </summary>
    public void Validate()
    {
        if (visualPresets == null)
            visualPresets = new List<PlayerLaserBeamVisualPresetDefinition>();

#if UNITY_EDITOR
        if (bodyMaterial == null)
            bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerLaserBeamVisualDefaultsUtility.DefaultBodyMaterialPath);

        if (sourceEffectMaterial == null)
            sourceEffectMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerLaserBeamVisualDefaultsUtility.DefaultSourceEffectMaterialPath);

        if (terminalCapMaterial == null)
            terminalCapMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerLaserBeamVisualDefaultsUtility.DefaultTerminalCapMaterialPath);
#endif

        PlayerLaserBeamVisualDefaultsUtility.ValidateVisualPresetDefinitions(visualPresets);
    }
    #endregion

    #endregion
}
