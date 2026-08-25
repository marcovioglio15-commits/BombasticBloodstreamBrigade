using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Resolves menu-button presentation targets from compact baked interaction profiles.
/// </summary>
internal static class GameMenuButtonPresentationUtility
{
    #region Methods

    #region Motion Modes
    /// <summary>
    /// Checks whether one interaction profile applies manual transform motion.
    /// </summary>
    /// <param name="interaction">Baked interaction profile.</param>
    /// <returns>True when manual transform motion is enabled.</returns>
    public static bool UsesManualMotion(in GameUiMenuButtonInteractionElement interaction)
    {
        return interaction.MotionMode == GameUiButtonMotionMode.ManualTransform ||
               interaction.MotionMode == GameUiButtonMotionMode.ManualTransformAndClips;
    }

    /// <summary>
    /// Checks whether one interaction profile samples authored clips.
    /// </summary>
    /// <param name="interaction">Baked interaction profile.</param>
    /// <returns>True when clip sampling is enabled.</returns>
    public static bool UsesClips(in GameUiMenuButtonInteractionElement interaction)
    {
        return interaction.MotionMode == GameUiButtonMotionMode.AnimationClips ||
               interaction.MotionMode == GameUiButtonMotionMode.ManualTransformAndClips;
    }
    #endregion

    #region Transform Targets
    /// <summary>
    /// Resolves target local position for one button state.
    /// </summary>
    /// <param name="interaction">Baked interaction profile.</param>
    /// <param name="state">Current presentation state.</param>
    /// <param name="baseline">Authored local-position baseline.</param>
    /// <returns>Baseline plus the selected state offset.</returns>
    public static Vector3 ResolvePosition(in GameUiMenuButtonInteractionElement interaction,
                                          GameUiButtonPresentationState state,
                                          Vector3 baseline)
    {
        switch (state)
        {
            case GameUiButtonPresentationState.Hovered:
            case GameUiButtonPresentationState.Selected:
                return baseline + ToVector3(interaction.HoverPositionOffset);
            case GameUiButtonPresentationState.Pressed:
                return baseline + ToVector3(interaction.PressedPositionOffset);
            default:
                return baseline;
        }
    }

    /// <summary>
    /// Resolves target local rotation for one button state.
    /// </summary>
    /// <param name="interaction">Baked interaction profile.</param>
    /// <param name="state">Current presentation state.</param>
    /// <param name="baseline">Authored local-rotation baseline.</param>
    /// <returns>Baseline multiplied by the selected state rotation offset.</returns>
    public static Quaternion ResolveRotation(in GameUiMenuButtonInteractionElement interaction,
                                             GameUiButtonPresentationState state,
                                             Quaternion baseline)
    {
        switch (state)
        {
            case GameUiButtonPresentationState.Hovered:
            case GameUiButtonPresentationState.Selected:
                return baseline * Quaternion.Euler(ToVector3(interaction.HoverRotationOffset));
            case GameUiButtonPresentationState.Pressed:
                return baseline * Quaternion.Euler(ToVector3(interaction.PressedRotationOffset));
            default:
                return baseline;
        }
    }

    /// <summary>
    /// Resolves target local scale for one button state.
    /// </summary>
    /// <param name="interaction">Baked interaction profile.</param>
    /// <param name="state">Current presentation state.</param>
    /// <param name="baseline">Authored local-scale baseline.</param>
    /// <returns>Baseline multiplied by the selected state scale.</returns>
    public static Vector3 ResolveScale(in GameUiMenuButtonInteractionElement interaction,
                                       GameUiButtonPresentationState state,
                                       Vector3 baseline)
    {
        switch (state)
        {
            case GameUiButtonPresentationState.Hovered:
            case GameUiButtonPresentationState.Selected:
                return Vector3.Scale(baseline, ToVector3(interaction.HoverScale));
            case GameUiButtonPresentationState.Pressed:
                return Vector3.Scale(baseline, ToVector3(interaction.PressedScale));
            default:
                return baseline;
        }
    }
    #endregion

    #region Asset And Color Targets
    /// <summary>
    /// Resolves the optional clip assigned to one button state.
    /// </summary>
    /// <param name="interaction">Baked interaction profile.</param>
    /// <param name="state">Current presentation state.</param>
    /// <returns>Assigned clip or null.</returns>
    public static AnimationClip ResolveClip(in GameUiMenuButtonInteractionElement interaction,
                                            GameUiButtonPresentationState state)
    {
        switch (state)
        {
            case GameUiButtonPresentationState.Hovered:
            case GameUiButtonPresentationState.Selected:
                return interaction.HoverClip.Value;
            case GameUiButtonPresentationState.Pressed:
                return interaction.PressedClip.Value;
            case GameUiButtonPresentationState.Disabled:
                return interaction.DisabledClip.Value;
            default:
                return interaction.NormalClip.Value;
        }
    }

    /// <summary>
    /// Resolves the optional sprite assigned to one button state.
    /// </summary>
    /// <param name="interaction">Baked interaction profile.</param>
    /// <param name="state">Current presentation state.</param>
    /// <returns>Assigned sprite or null.</returns>
    public static Sprite ResolveSprite(in GameUiMenuButtonInteractionElement interaction,
                                       GameUiButtonPresentationState state)
    {
        switch (state)
        {
            case GameUiButtonPresentationState.Hovered:
            case GameUiButtonPresentationState.Selected:
                return interaction.HoverSprite.Value;
            case GameUiButtonPresentationState.Pressed:
                return interaction.PressedSprite.Value;
            case GameUiButtonPresentationState.Disabled:
                return interaction.DisabledSprite.Value;
            default:
                return interaction.NormalSprite.Value;
        }
    }

    /// <summary>
    /// Resolves target-graphic color for one button state.
    /// </summary>
    /// <param name="interaction">Baked interaction profile.</param>
    /// <param name="state">Current presentation state.</param>
    /// <returns>Unity color selected from the baked profile.</returns>
    public static Color ResolveGraphicColor(in GameUiMenuButtonInteractionElement interaction,
                                            GameUiButtonPresentationState state)
    {
        switch (state)
        {
            case GameUiButtonPresentationState.Hovered:
            case GameUiButtonPresentationState.Selected:
                return ToColor(interaction.HoverGraphicColor);
            case GameUiButtonPresentationState.Pressed:
                return ToColor(interaction.PressedGraphicColor);
            case GameUiButtonPresentationState.Disabled:
                return ToColor(interaction.DisabledGraphicColor);
            default:
                return ToColor(interaction.NormalGraphicColor);
        }
    }

    /// <summary>
    /// Resolves TMP label color for one button state.
    /// </summary>
    /// <param name="interaction">Baked interaction profile.</param>
    /// <param name="state">Current presentation state.</param>
    /// <returns>Unity color selected from the baked profile.</returns>
    public static Color ResolveTextColor(in GameUiMenuButtonInteractionElement interaction,
                                         GameUiButtonPresentationState state)
    {
        switch (state)
        {
            case GameUiButtonPresentationState.Hovered:
            case GameUiButtonPresentationState.Selected:
                return ToColor(interaction.HoverTextColor);
            case GameUiButtonPresentationState.Pressed:
                return ToColor(interaction.PressedTextColor);
            case GameUiButtonPresentationState.Disabled:
                return ToColor(interaction.DisabledTextColor);
            default:
                return ToColor(interaction.NormalTextColor);
        }
    }
    #endregion

    #region Conversion
    /// <summary>
    /// Converts an ECS float3 to a Unity vector.
    /// </summary>
    /// <param name="value">ECS vector to convert.</param>
    /// <returns>Unity vector with matching components.</returns>
    private static Vector3 ToVector3(float3 value)
    {
        return new Vector3(value.x, value.y, value.z);
    }

    /// <summary>
    /// Converts an ECS color to a Unity color.
    /// </summary>
    /// <param name="value">RGBA value to convert.</param>
    /// <returns>Unity color with matching channels.</returns>
    private static Color ToColor(float4 value)
    {
        return new Color(value.x, value.y, value.z, value.w);
    }
    #endregion

    #endregion
}
