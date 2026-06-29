#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Rebuilds the boss HUD Edit Mode preview through the same syringe visual settings used by enemy baking.
/// </summary>
internal static class EnemyBossHudEditorPreviewUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Returns whether the selected boss UI settings should show syringe bars in Edit Mode.
    /// </summary>
    /// <param name="bossUi">Selected boss UI settings, or null when default enabled behavior is used.</param>
    /// <returns>True when the boss syringe bars should be visible in the editor preview.</returns>
    public static bool ShouldShowBars(EnemyBossVisualUiSettings bossUi)
    {
        return bossUi == null || bossUi.Enabled && bossUi.ShowHealthBar;
    }

    /// <summary>
    /// Applies material, layout, labels, and values for the boss HUD Edit Mode preview.
    /// </summary>
    /// <param name="visualPreset">Enemy visual preset selected on the preview component.</param>
    /// <param name="bossUi">Boss UI settings resolved from the selected visual preset.</param>
    /// <param name="healthSyringeBar">Preauthored syringe view representing boss health.</param>
    /// <param name="shieldSyringeBar">Preauthored syringe view representing boss shield.</param>
    /// <param name="portraitRoot">Rect transform hosting the mirrored boss portrait.</param>
    /// <param name="portraitImage">Image used to render the preview portrait sprite.</param>
    /// <param name="offscreenIndicatorRoot">Offscreen indicator hidden during local bar preview.</param>
    /// <param name="bossNameText">Text label showing the preview boss name.</param>
    /// <param name="healthValue">Current health value shown by the preview.</param>
    /// <param name="healthMaximum">Maximum health value used to resolve boss syringe length.</param>
    /// <param name="shieldValue">Current shield value shown by the preview.</param>
    /// <param name="shieldMaximum">Maximum shield value used to resolve boss shield length.</param>
    public static void Refresh(EnemyVisualPreset visualPreset,
                               EnemyBossVisualUiSettings bossUi,
                               PlayerSyringeBarView healthSyringeBar,
                               PlayerSyringeBarView shieldSyringeBar,
                               RectTransform portraitRoot,
                               Image portraitImage,
                               RectTransform offscreenIndicatorRoot,
                               TMP_Text bossNameText,
                               float healthValue,
                               float healthMaximum,
                               float shieldValue,
                               float shieldMaximum)
    {
        EnemyBossHudOffscreenIndicatorUtility.SetVisible(offscreenIndicatorRoot, false);
        ApplyPortraitPreview(bossUi, portraitRoot, portraitImage);

        if (!ShouldShowBars(bossUi))
        {
            HideBars(healthSyringeBar, shieldSyringeBar);
            Repaint();
            return;
        }

        PlayerHealthBarsVisualSettings syringeSettings = ResolveSyringeSettings(bossUi);
        PlayerHealthBarVisualConfig previewConfig = PlayerHealthBarVisualBakeUtility.BuildConfig(syringeSettings);
        TMP_FontAsset font = previewConfig.FontAsset.Value;

        ApplyBossName(bossNameText, ResolveBossName(visualPreset, bossUi));
        ApplyHealthPreview(healthSyringeBar, in previewConfig, font, healthValue, healthMaximum);
        ApplyShieldPreview(shieldSyringeBar, in previewConfig, font, shieldValue, shieldMaximum);
        Repaint();
    }
    #endregion

    #region Preview Application
    /// <summary>
    /// Applies the configured mirrored boss portrait preview using authored preset data.
    /// </summary>
    /// <param name="bossUi">Boss UI settings resolved from the selected visual preset.</param>
    /// <param name="portraitRoot">Rect transform hosting the mirrored boss portrait.</param>
    /// <param name="portraitImage">Image used to render the preview portrait sprite.</param>
    private static void ApplyPortraitPreview(EnemyBossVisualUiSettings bossUi,
                                             RectTransform portraitRoot,
                                             Image portraitImage)
    {
        if (portraitRoot == null || portraitImage == null)
            return;

        bool visible = bossUi == null || bossUi.Enabled && bossUi.ShowPortrait && bossUi.PortraitSprite != null;

        if (portraitRoot.gameObject.activeSelf != visible)
            portraitRoot.gameObject.SetActive(visible);

        if (!visible)
            return;

        portraitRoot.localScale = Vector3.one;
        portraitRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
        float sizePixels = bossUi != null ? Mathf.Max(1f, bossUi.PortraitSizePixels) : 96f;
        portraitRoot.sizeDelta = new Vector2(sizePixels, sizePixels);
        portraitImage.sprite = bossUi != null ? bossUi.PortraitSprite : null;
        portraitImage.color = bossUi != null ? bossUi.PortraitColor : Color.white;
        portraitImage.enabled = portraitImage.sprite != null;
    }

    /// <summary>
    /// Applies the configured boss health syringe preview values.
    /// </summary>
    /// <param name="healthSyringeBar">Preauthored syringe view representing boss health.</param>
    /// <param name="previewConfig">Baked boss syringe visual configuration.</param>
    /// <param name="font">Direct font asset baked from the selected enemy visual preset.</param>
    /// <param name="healthValue">Current health value shown by the preview.</param>
    /// <param name="healthMaximum">Maximum health value used to resolve boss syringe length.</param>
    private static void ApplyHealthPreview(PlayerSyringeBarView healthSyringeBar,
                                           in PlayerHealthBarVisualConfig previewConfig,
                                           TMP_FontAsset font,
                                           float healthValue,
                                           float healthMaximum)
    {
        if (healthSyringeBar == null)
            return;

        healthSyringeBar.ApplyConfiguration(in previewConfig, in previewConfig.Health, font);
        healthSyringeBar.UpdateValue(Mathf.Max(0f, healthValue),
                                     Mathf.Max(0.0001f, healthMaximum),
                                     0f,
                                     true);
    }

    /// <summary>
    /// Applies the configured boss shield syringe preview values or hides it when shield max is unavailable.
    /// </summary>
    /// <param name="shieldSyringeBar">Preauthored syringe view representing boss shield.</param>
    /// <param name="previewConfig">Baked boss syringe visual configuration.</param>
    /// <param name="font">Direct font asset baked from the selected enemy visual preset.</param>
    /// <param name="shieldValue">Current shield value shown by the preview.</param>
    /// <param name="shieldMaximum">Maximum shield value used to resolve boss shield length.</param>
    private static void ApplyShieldPreview(PlayerSyringeBarView shieldSyringeBar,
                                           in PlayerHealthBarVisualConfig previewConfig,
                                           TMP_FontAsset font,
                                           float shieldValue,
                                           float shieldMaximum)
    {
        if (shieldSyringeBar == null)
            return;

        shieldSyringeBar.ApplyConfiguration(in previewConfig, in previewConfig.Shield, font);

        if (shieldMaximum > 0f)
            shieldSyringeBar.UpdateValue(Mathf.Max(0f, shieldValue), shieldMaximum, 0f, true);
        else
            shieldSyringeBar.HandleMissing(true);
    }
    #endregion

    #region Resolution
    /// <summary>
    /// Resolves boss syringe settings from the selected boss UI settings, falling back to the authored boss palettes.
    /// </summary>
    /// <param name="bossUi">Boss UI settings resolved from the selected visual preset.</param>
    /// <returns>Syringe settings used to build the preview configuration.</returns>
    private static PlayerHealthBarsVisualSettings ResolveSyringeSettings(EnemyBossVisualUiSettings bossUi)
    {
        if (bossUi != null && bossUi.SyringeBars != null)
            return bossUi.SyringeBars;

        return new PlayerHealthBarsVisualSettings(PlayerSyringePalettePreset.BossHealth,
                                                  PlayerSyringePalettePreset.BossShield);
    }

    /// <summary>
    /// Resolves the label shown by the Edit Mode boss preview.
    /// </summary>
    /// <param name="visualPreset">Enemy visual preset selected on the preview component.</param>
    /// <param name="bossUi">Boss UI settings resolved from the selected visual preset.</param>
    /// <returns>Display name shown by the preview label.</returns>
    private static string ResolveBossName(EnemyVisualPreset visualPreset, EnemyBossVisualUiSettings bossUi)
    {
        if (bossUi != null && !string.IsNullOrWhiteSpace(bossUi.BossDisplayName))
            return bossUi.BossDisplayName;

        if (visualPreset != null && !string.IsNullOrWhiteSpace(visualPreset.PresetName))
            return visualPreset.PresetName;

        return "Boss";
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Hides both preauthored boss syringe bars when the selected boss UI preset disables health-bar presentation.
    /// </summary>
    /// <param name="healthSyringeBar">Preauthored syringe view representing boss health.</param>
    /// <param name="shieldSyringeBar">Preauthored syringe view representing boss shield.</param>
    private static void HideBars(PlayerSyringeBarView healthSyringeBar, PlayerSyringeBarView shieldSyringeBar)
    {
        if (healthSyringeBar != null)
            healthSyringeBar.HandleMissing(true);

        if (shieldSyringeBar != null)
            shieldSyringeBar.HandleMissing(true);
    }

    /// <summary>
    /// Writes the preview boss name without depending on runtime ECS state.
    /// </summary>
    /// <param name="bossNameText">Text label showing the preview boss name.</param>
    /// <param name="bossName">Resolved preview boss name.</param>
    private static void ApplyBossName(TMP_Text bossNameText, string bossName)
    {
        if (bossNameText == null)
            return;

        bossNameText.text = bossName;
    }

    /// <summary>
    /// Repaints editor views after the procedural syringe material and layout have been rebuilt.
    /// </summary>
    private static void Repaint()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }
    #endregion

    #endregion
}
#endif
