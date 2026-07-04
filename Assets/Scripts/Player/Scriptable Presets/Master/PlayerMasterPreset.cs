using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "PlayerMasterPreset", menuName = "Player/Master Preset", order = 9)]
public sealed class PlayerMasterPreset : ScriptableObject
{
    #region Serialized Fields
    [Tooltip("Unique ID for this master preset, used for stable references.")]
    [Header("Metadata")]
    [SerializeField] private string m_PresetId;

    [Tooltip("Master preset name.")]
    [SerializeField] private string m_PresetName = "New Player Master Preset";

    [Tooltip("Short description of the master preset use case.")]
    [SerializeField] private string m_Description;

    [Tooltip("Optional semantic version string for this master preset.")]
    [SerializeField] private string m_Version = "1.0.0";

    [Tooltip("Controller preset reference.")]
    [Header("Sub Presets")]
    [SerializeField] private PlayerControllerPreset m_ControllerPreset;

    [Tooltip("Level-up and progression preset reference.")]
    [SerializeField] private PlayerProgressionPreset m_ProgressionPreset;

    [Tooltip("Craftable power-ups preset reference.")]
    [SerializeField] private PlayerPowerUpsPreset m_PowerUpsPreset;

    [Tooltip("Gameplay visual presentation preset reference for weapon visuals, VFX, outline, damage feedback and world presentation.")]
    [SerializeField] private PlayerVisualPreset visualPreset;

    [Tooltip("UI visual presentation preset reference for HUD syringes, active power-up widgets, portrait and growth sequence visuals.")]
    [SerializeField] private PlayerUiVisualPreset uiVisualPreset;

    [Tooltip("Animation bindings preset reference.")]
    [SerializeField] private PlayerAnimationBindingsPreset m_AnimationBindingsPreset;

    [Tooltip("Layer used by gameplay systems to identify solid world walls.")]
    [Header("Layers")]
    [SerializeField] private string wallsLayerName = "Walls";
    #endregion

    #region Properties
    public string PresetId
    {
        get
        {
            return m_PresetId;
        }
    }

    public string PresetName
    {
        get
        {
            return m_PresetName;
        }
    }

    public string Description
    {
        get
        {
            return m_Description;
        }
    }

    public string Version
    {
        get
        {
            return m_Version;
        }
    }

    public PlayerControllerPreset ControllerPreset
    {
        get
        {
            return m_ControllerPreset;
        }
    }

    public PlayerProgressionPreset ProgressionPreset
    {
        get
        {
            return m_ProgressionPreset;
        }
    }

    public PlayerPowerUpsPreset PowerUpsPreset
    {
        get
        {
            return m_PowerUpsPreset;
        }
    }

    public PlayerVisualPreset VisualPreset
    {
        get
        {
            return visualPreset;
        }
    }

    public PlayerUiVisualPreset UiVisualPreset
    {
        get
        {
            return uiVisualPreset;
        }
    }

    public PlayerAnimationBindingsPreset AnimationBindingsPreset
    {
        get
        {
            return m_AnimationBindingsPreset;
        }
    }

    public string WallsLayerName
    {
        get
        {
            return wallsLayerName;
        }
    }
    #endregion

    #region Unity Methods
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(m_PresetId))
            m_PresetId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(wallsLayerName))
            wallsLayerName = "Walls";

        if (uiVisualPreset != null)
            uiVisualPreset.ValidateValues();

        TryMigrateLegacyProgressionHealth();
        TryMigrateLegacyElementalEnemyVfx();
    }
    #endregion

    #region Migration
    private void TryMigrateLegacyProgressionHealth()
    {
        if (m_ControllerPreset == null)
            return;

        if (m_ProgressionPreset == null)
            return;

        if (!m_ProgressionPreset.TryGetLegacyHealth(out float legacyHealth))
            return;

        bool changed = m_ControllerPreset.TryApplyLegacyMaxHealth(legacyHealth);

#if UNITY_EDITOR
        if (changed)
            EditorUtility.SetDirty(m_ControllerPreset);
#else
        _ = changed;
#endif
    }

    private void TryMigrateLegacyElementalEnemyVfx()
    {
        if (visualPreset == null)
            return;

        if (m_PowerUpsPreset == null)
            return;

        if (PlayerElementalVfxAssignmentUtility.HasAnyConfiguredVfx(m_PowerUpsPreset.ElementalVfxByElement) == false)
            return;

        if (!visualPreset.ImportElementalEnemyVfxAssignments(m_PowerUpsPreset.ElementalVfxByElement))
            return;

#if UNITY_EDITOR
        EditorUtility.SetDirty(visualPreset);
#endif
    }
    #endregion
}
