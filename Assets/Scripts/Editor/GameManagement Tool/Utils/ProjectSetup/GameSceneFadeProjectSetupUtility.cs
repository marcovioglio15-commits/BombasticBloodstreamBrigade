using UnityEditor;
using UnityEngine;
using static GameSceneManagementProjectSetupSerializedUtility;

/// <summary>
/// Synchronizes the default paint and gradient transition settings used by project rebuilds.
/// </summary>
public static class GameSceneFadeProjectSetupUtility
{
    #region Methods

    #region Synchronization Methods
    /// <summary>
    /// Writes default fade timing and shader-shaping values to the serialized Scene Manager preset.
    /// </summary>
    /// <param name="serializedPreset">Serialized Scene Manager preset receiving the base configuration.</param>
    public static void Synchronize(SerializedObject serializedPreset)
    {
        SerializedProperty fadeSettings = serializedPreset.FindProperty("fadeSettings");

        if (fadeSettings == null)
            return;

        SetColor(fadeSettings, "fadeColor", Color.black);
        SetInt(fadeSettings, "visualStyle", (int)GameSceneFadeVisualStyle.Paint);
        SetInt(fadeSettings, "fadeMode", (int)GameSceneFadeMode.DirectionalGradient);
        SetInt(fadeSettings, "wipeDirection", (int)GameSceneFadeWipeDirection.LeftToRight);
        SetFloat(fadeSettings, "directionalEdgeSoftness", 0.16f);
        SetFloat(fadeSettings, "directionalNoiseStrength", 0.035f);
        SetFloat(fadeSettings, "directionalNoiseScale", 5.5f);
        SetFloat(fadeSettings, "paintEdgeSoftness", 0.025f);
        SetFloat(fadeSettings, "paintNoiseStrength", 0.22f);
        SetFloat(fadeSettings, "paintNoiseScale", 2.4f);
        SetFloat(fadeSettings, "paintBristleStrength", 0.075f);
        SetFloat(fadeSettings, "paintBristleScale", 48f);
        SetInt(fadeSettings, "easing", (int)GameSceneFadeEasing.SmoothStep);
        SetFloat(fadeSettings, "fadeOutSeconds", 0.35f);
        SetFloat(fadeSettings, "postLoadReadyExtraSeconds", 0.08f);
        SetFloat(fadeSettings, "fadeInSeconds", 0.35f);
        SetBool(fadeSettings, "lockGameplayInput", true);
        SetBool(fadeSettings, "setTimeScaleDuringTransition", true);
    }
    #endregion

    #endregion
}
