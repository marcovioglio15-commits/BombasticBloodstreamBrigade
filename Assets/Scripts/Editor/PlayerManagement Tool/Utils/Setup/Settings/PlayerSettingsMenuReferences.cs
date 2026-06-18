using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mutable reference collector used while the Settings menu prefab hierarchy is generated.
/// </summary>
internal sealed class PlayerSettingsMenuReferences
{
    #region Fields
    public GameObject PanelRoot;
    public Button AudioTabButton;
    public Button GameplayTabButton;
    public Button ConfirmButton;
    public Button ResetDefaultsButton;
    public Button CloseButton;
    public GameObject AudioPanelRoot;
    public GameObject GameplayPanelRoot;
    public Slider MasterVolumeSlider;
    public Slider SfxVolumeSlider;
    public Slider MusicVolumeSlider;
    public TMP_Text MasterVolumeValueLabel;
    public TMP_Text SfxVolumeValueLabel;
    public TMP_Text MusicVolumeValueLabel;
    public Toggle VisualPointerToggle;
    public Toggle FullscreenToggle;
    public SettingsFrameRateSelector FrameRateSelector;
    public Slider DamageRumbleMultiplierSlider;
    public Slider FireRumbleMultiplierSlider;
    public TMP_Text DamageRumbleValueLabel;
    public TMP_Text FireRumbleValueLabel;
    #endregion
}
