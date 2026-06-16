using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides editor-only copy and clip lookup helpers for animation bindings presets.
/// </summary>
public static class PlayerAnimationBindingsPresetTransferUtility
{
    private static readonly string[] SettingsPropertyPaths =
    {
        "animatorController",
        "upperBodyAvatarMask",
        "lowerBodyAvatarMask",
        "disableRootMotion",
        "useFloatDamping",
        "floatDampTime",
        "movingSpeedThreshold",
        "moveXParameter",
        "moveYParameter",
        "moveSpeedParameter",
        "aimXParameter",
        "aimYParameter",
        "isMovingParameter",
        "isShootingParameter",
        "isDashingParameter",
        "shotPulseParameter",
        "proceduralRecoilEnabled",
        "proceduralRecoilKick",
        "proceduralRecoilRecoveryPerSecond",
        "proceduralRecoilParameter",
        "proceduralAimWeightEnabled",
        "proceduralAimWeightSmoothing",
        "proceduralAimWeightParameter",
        "proceduralLeanEnabled",
        "proceduralLeanSmoothing",
        "proceduralLeanParameter"
    };

    private static readonly string[] ClipPropertyPaths =
    {
        "idleClip",
        "upperBodyIdleClip",
        "moveForwardClip",
        "moveBackwardClip",
        "moveLeftClip",
        "moveRightClip",
        "aimForwardClip",
        "aimBackwardClip",
        "aimLeftClip",
        "aimRightClip",
        "upperBodyActionClips.primaryChargeClip",
        "upperBodyActionClips.secondaryChargeClip",
        "upperBodyActionClips.primaryReleaseClip",
        "upperBodyActionClips.secondaryReleaseClip",
        "dashClip"
    };

    public static void CopySettingsFrom(PlayerAnimationBindingsPreset targetPreset, PlayerAnimationBindingsPreset sourcePreset)
    {
        CopyProperties(targetPreset, sourcePreset, SettingsPropertyPaths);
    }

    public static void CopyClipSlotsFrom(PlayerAnimationBindingsPreset targetPreset, PlayerAnimationBindingsPreset sourcePreset)
    {
        CopyProperties(targetPreset, sourcePreset, ClipPropertyPaths);
    }

    public static AnimationClip GetClip(PlayerAnimationBindingsPreset preset, PlayerAnimationClipSlot slot)
    {
        if (preset == null)
            return null;

        switch (slot)
        {
            case PlayerAnimationClipSlot.Idle:
                return preset.IdleClip;
            case PlayerAnimationClipSlot.UpperBodyIdle:
                return preset.UpperBodyIdleClip;
            case PlayerAnimationClipSlot.MoveForward:
                return preset.MoveForwardClip;
            case PlayerAnimationClipSlot.MoveBackward:
                return preset.MoveBackwardClip;
            case PlayerAnimationClipSlot.MoveLeft:
                return preset.MoveLeftClip;
            case PlayerAnimationClipSlot.MoveRight:
                return preset.MoveRightClip;
            case PlayerAnimationClipSlot.AimForward:
                return preset.AimForwardClip;
            case PlayerAnimationClipSlot.AimBackward:
                return preset.AimBackwardClip;
            case PlayerAnimationClipSlot.AimLeft:
                return preset.AimLeftClip;
            case PlayerAnimationClipSlot.AimRight:
                return preset.AimRightClip;
            case PlayerAnimationClipSlot.Dash:
                return preset.DashClip;
        }

        return null;
    }

    private static void CopyProperties(PlayerAnimationBindingsPreset targetPreset,
                                       PlayerAnimationBindingsPreset sourcePreset,
                                       string[] propertyPaths)
    {
        if (targetPreset == null || sourcePreset == null || propertyPaths == null)
            return;

        SerializedObject targetSerializedObject = new SerializedObject(targetPreset);
        SerializedObject sourceSerializedObject = new SerializedObject(sourcePreset);
        sourceSerializedObject.Update();
        targetSerializedObject.Update();

        for (int index = 0; index < propertyPaths.Length; index++)
        {
            string propertyPath = propertyPaths[index];
            SerializedProperty targetProperty = targetSerializedObject.FindProperty(propertyPath);
            SerializedProperty sourceProperty = sourceSerializedObject.FindProperty(propertyPath);

            if (targetProperty == null || sourceProperty == null)
                continue;

            CopyPropertyValue(targetProperty, sourceProperty);
        }

        targetSerializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CopyPropertyValue(SerializedProperty targetProperty, SerializedProperty sourceProperty)
    {
        switch (targetProperty.propertyType)
        {
            case SerializedPropertyType.Boolean:
                targetProperty.boolValue = sourceProperty.boolValue;
                return;
            case SerializedPropertyType.Float:
                targetProperty.floatValue = sourceProperty.floatValue;
                return;
            case SerializedPropertyType.Integer:
                targetProperty.intValue = sourceProperty.intValue;
                return;
            case SerializedPropertyType.String:
                targetProperty.stringValue = sourceProperty.stringValue;
                return;
            case SerializedPropertyType.ObjectReference:
                targetProperty.objectReferenceValue = sourceProperty.objectReferenceValue;
                return;
        }
    }
}
