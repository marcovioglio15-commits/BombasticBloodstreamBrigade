using System;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Resolves Add Scaling stat keys and runtime field rewrites for stacked projectile elements and per-element bullet behaviours.
/// </summary>
public static class PlayerRuntimeScalingControllerElementFieldUtility
{
    #region Constants
    private const string AppliedElementsPrefix = "shootingSettings.values.appliedElements.Array.data[";
    private const string ElementBehavioursPrefix = "shootingSettings.values.elementBehaviours.";
    private const int ElementBehaviourPropertyCount = 15;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves a shooting-related stacked-element stat key into the matching runtime controller field identifier.
    /// </summary>
    /// <param name="statKey">Normalized Add Scaling stat key.</param>
    /// <param name="fieldId">Resolved field identifier when the key belongs to the stacked-element system.</param>
    /// <param name="slotIndex">Resolved applied-element slot index when the key targets one slot entry.</param>
    /// <returns>True when the key was resolved by this utility.</returns>
    public static bool TryMapFieldId(string statKey,
                                     out PlayerRuntimeControllerFieldId fieldId,
                                     out int slotIndex)
    {
        fieldId = default;
        slotIndex = -1;

        if (string.IsNullOrWhiteSpace(statKey))
            return false;

        if (TryMapAppliedElementSlotFieldId(statKey, out fieldId, out slotIndex))
            return true;

        return TryMapElementBehaviourFieldId(statKey, out fieldId);
    }

    /// <summary>
    /// Applies one resolved runtime scaling value to the stacked-element shooting config fields.
    /// </summary>
    /// <param name="fieldId">Runtime field being updated.</param>
    /// <param name="slotIndex">Resolved applied-element slot index for dynamic slot rewrites.</param>
    /// <param name="resolvedValue">Final numeric value produced by formula evaluation.</param>
    /// <param name="runtimeShooting">Mutable shooting config rebuilt this frame.</param>
    /// <param name="runtimeAppliedElements">Mutable runtime applied-element slot buffer rebuilt this frame.</param>
    /// <returns>True when this utility handled the field.</returns>
    public static bool TryApplyField(PlayerRuntimeControllerFieldId fieldId,
                                     int slotIndex,
                                     float resolvedValue,
                                     ref PlayerRuntimeShootingConfig runtimeShooting,
                                     DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> runtimeAppliedElements)
    {
        if (fieldId == PlayerRuntimeControllerFieldId.ShootingAppliedElementDynamicSlot)
        {
            PlayerProjectileAppliedElement appliedElement = PlayerRuntimeScalingEnumUtility.ResolvePlayerProjectileAppliedElement(resolvedValue);
            PlayerElementBulletSettingsUtility.SetAppliedElementAt(runtimeAppliedElements, slotIndex, appliedElement);
            return true;
        }

        if (!TryResolveElementFieldTarget(fieldId, out int elementIndex, out int propertyIndex))
            return false;

        switch (elementIndex)
        {
            case 0:
                ElementBulletSettingsBlob fireSettings = runtimeShooting.Values.ElementBehaviours.Fire;
                ApplyElementBehaviourProperty(ref fireSettings, propertyIndex, resolvedValue);
                runtimeShooting.Values.ElementBehaviours.Fire = fireSettings;
                return true;
            case 1:
                ElementBulletSettingsBlob iceSettings = runtimeShooting.Values.ElementBehaviours.Ice;
                ApplyElementBehaviourProperty(ref iceSettings, propertyIndex, resolvedValue);
                runtimeShooting.Values.ElementBehaviours.Ice = iceSettings;
                return true;
            case 2:
                ElementBulletSettingsBlob poisonSettings = runtimeShooting.Values.ElementBehaviours.Poison;
                ApplyElementBehaviourProperty(ref poisonSettings, propertyIndex, resolvedValue);
                runtimeShooting.Values.ElementBehaviours.Poison = poisonSettings;
                return true;
            case 3:
                ElementBulletSettingsBlob customSettings = runtimeShooting.Values.ElementBehaviours.Custom;
                ApplyElementBehaviourProperty(ref customSettings, propertyIndex, resolvedValue);
                runtimeShooting.Values.ElementBehaviours.Custom = customSettings;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Applies one resolved boolean scaling value to the stacked-element shooting config fields.
    /// </summary>
    /// <param name="fieldId">Runtime field being updated.</param>
    /// <param name="resolvedValue">Final boolean value produced by formula evaluation.</param>
    /// <param name="runtimeShooting">Mutable shooting config rebuilt this frame.</param>
    /// <returns>True when this utility handled the field.</returns>
    public static bool TryApplyBooleanField(PlayerRuntimeControllerFieldId fieldId,
                                            bool resolvedValue,
                                            ref PlayerRuntimeShootingConfig runtimeShooting)
    {
        if (!TryResolveElementFieldTarget(fieldId, out int elementIndex, out int propertyIndex))
            return false;

        if (propertyIndex != 7)
            return false;

        byte booleanByte = resolvedValue ? (byte)1 : (byte)0;

        switch (elementIndex)
        {
            case 0:
                ElementBulletSettingsBlob fireSettings = runtimeShooting.Values.ElementBehaviours.Fire;
                fireSettings.ConsumeStacksOnProc = booleanByte;
                runtimeShooting.Values.ElementBehaviours.Fire = fireSettings;
                return true;
            case 1:
                ElementBulletSettingsBlob iceSettings = runtimeShooting.Values.ElementBehaviours.Ice;
                iceSettings.ConsumeStacksOnProc = booleanByte;
                runtimeShooting.Values.ElementBehaviours.Ice = iceSettings;
                return true;
            case 2:
                ElementBulletSettingsBlob poisonSettings = runtimeShooting.Values.ElementBehaviours.Poison;
                poisonSettings.ConsumeStacksOnProc = booleanByte;
                runtimeShooting.Values.ElementBehaviours.Poison = poisonSettings;
                return true;
            case 3:
                ElementBulletSettingsBlob customSettings = runtimeShooting.Values.ElementBehaviours.Custom;
                customSettings.ConsumeStacksOnProc = booleanByte;
                runtimeShooting.Values.ElementBehaviours.Custom = customSettings;
                return true;
            default:
                return false;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves one applied-element array stat key into the corresponding fixed slot field.
    /// </summary>
    /// <param name="statKey">Normalized stat key.</param>
    /// <param name="fieldId">Resolved field identifier.</param>
    /// <returns>True when the stat key targets an applied-element slot.</returns>
    private static bool TryMapAppliedElementSlotFieldId(string statKey,
                                                        out PlayerRuntimeControllerFieldId fieldId,
                                                        out int slotIndex)
    {
        fieldId = default;
        slotIndex = -1;

        if (!statKey.StartsWith(AppliedElementsPrefix, StringComparison.Ordinal))
            return false;

        int closingBracketIndex = statKey.IndexOf(']', AppliedElementsPrefix.Length);

        if (closingBracketIndex < 0)
            return false;

        string slotIndexText = statKey.Substring(AppliedElementsPrefix.Length, closingBracketIndex - AppliedElementsPrefix.Length);

        if (!int.TryParse(slotIndexText, out slotIndex))
            return false;

        if (slotIndex < 0)
            return false;

        fieldId = PlayerRuntimeControllerFieldId.ShootingAppliedElementDynamicSlot;
        return true;
    }

    /// <summary>
    /// Resolves one per-element behaviour stat key into the matching runtime field identifier.
    /// </summary>
    /// <param name="statKey">Normalized stat key.</param>
    /// <param name="fieldId">Resolved field identifier.</param>
    /// <returns>True when the stat key targets one per-element behaviour property.</returns>
    private static bool TryMapElementBehaviourFieldId(string statKey, out PlayerRuntimeControllerFieldId fieldId)
    {
        fieldId = default;

        if (!statKey.StartsWith(ElementBehavioursPrefix, StringComparison.Ordinal))
            return false;

        int elementSeparatorIndex = statKey.IndexOf('.', ElementBehavioursPrefix.Length);

        if (elementSeparatorIndex < 0)
            return false;

        string elementKey = statKey.Substring(ElementBehavioursPrefix.Length, elementSeparatorIndex - ElementBehavioursPrefix.Length);
        string propertySuffix = statKey.Substring(elementSeparatorIndex + 1);

        if (!TryResolveElementIndex(elementKey, out int elementIndex))
            return false;

        if (!TryResolveElementPropertyOffset(propertySuffix, out int propertyOffset))
            return false;

        fieldId = (PlayerRuntimeControllerFieldId)(ResolveElementFieldStartValue(elementIndex) + propertyOffset);
        return true;
    }

    /// <summary>
    /// Resolves a runtime field identifier into one element index and one property index inside the per-element behaviour ranges.
    /// </summary>
    /// <param name="fieldId">Runtime controller field identifier.</param>
    /// <param name="elementIndex">Resolved supported element index.</param>
    /// <param name="propertyIndex">Resolved per-element property index.</param>
    /// <returns>True when the identifier belongs to one supported per-element behaviour range.</returns>
    private static bool TryResolveElementFieldTarget(PlayerRuntimeControllerFieldId fieldId,
                                                     out int elementIndex,
                                                     out int propertyIndex)
    {
        int fieldIdValue = (int)fieldId;

        for (int currentElementIndex = 0; currentElementIndex < 4; currentElementIndex++)
        {
            int elementFieldStart = ResolveElementFieldStartValue(currentElementIndex);
            int elementFieldEnd = elementFieldStart + ElementBehaviourPropertyCount - 1;

            if (fieldIdValue < elementFieldStart || fieldIdValue > elementFieldEnd)
                continue;

            elementIndex = currentElementIndex;
            propertyIndex = fieldIdValue - elementFieldStart;
            return true;
        }

        elementIndex = -1;
        propertyIndex = -1;
        return false;
    }

    /// <summary>
    /// Resolves the serialized element field name to the matching behaviour-range index.
    /// </summary>
    /// <param name="elementKey">Lowercase serialized element field name.</param>
    /// <param name="elementIndex">Resolved element range index.</param>
    /// <returns>True when the key belongs to one supported element block.</returns>
    private static bool TryResolveElementIndex(string elementKey, out int elementIndex)
    {
        switch (elementKey)
        {
            case "fire":
                elementIndex = 0;
                return true;
            case "ice":
                elementIndex = 1;
                return true;
            case "poison":
                elementIndex = 2;
                return true;
            case "custom":
                elementIndex = 3;
                return true;
            default:
                elementIndex = -1;
                return false;
        }
    }

    /// <summary>
    /// Resolves a serialized per-element property suffix to the matching property offset inside one behaviour range.
    /// </summary>
    /// <param name="propertySuffix">Serialized property suffix.</param>
    /// <param name="propertyOffset">Resolved property offset inside the range.</param>
    /// <returns>True when the suffix matches one supported property.</returns>
    private static bool TryResolveElementPropertyOffset(string propertySuffix, out int propertyOffset)
    {
        switch (propertySuffix)
        {
            case "effectKind":
                propertyOffset = 0;
                return true;
            case "procMode":
                propertyOffset = 1;
                return true;
            case "reapplyMode":
                propertyOffset = 2;
                return true;
            case "stacksPerHit":
                propertyOffset = 3;
                return true;
            case "procThresholdStacks":
                propertyOffset = 4;
                return true;
            case "maximumStacks":
                propertyOffset = 5;
                return true;
            case "stackDecayPerSecond":
                propertyOffset = 6;
                return true;
            case "consumeStacksOnProc":
                propertyOffset = 7;
                return true;
            case "dotDamagePerTick":
                propertyOffset = 8;
                return true;
            case "dotTickInterval":
                propertyOffset = 9;
                return true;
            case "dotDurationSeconds":
                propertyOffset = 10;
                return true;
            case "impedimentSlowPercentPerStack":
                propertyOffset = 11;
                return true;
            case "impedimentProcSlowPercent":
                propertyOffset = 12;
                return true;
            case "impedimentMaxSlowPercent":
                propertyOffset = 13;
                return true;
            case "impedimentDurationSeconds":
                propertyOffset = 14;
                return true;
            default:
                propertyOffset = -1;
                return false;
        }
    }

    /// <summary>
    /// Resolves the first field identifier value used by one element behaviour range.
    /// </summary>
    /// <param name="elementIndex">Zero-based supported element index.</param>
    /// <returns>First field value used by that element range.</returns>
    private static int ResolveElementFieldStartValue(int elementIndex)
    {
        switch (elementIndex)
        {
            case 0:
                return (int)PlayerRuntimeControllerFieldId.ShootingElementFireEffectKind;
            case 1:
                return (int)PlayerRuntimeControllerFieldId.ShootingElementIceEffectKind;
            case 2:
                return (int)PlayerRuntimeControllerFieldId.ShootingElementPoisonEffectKind;
            default:
                return (int)PlayerRuntimeControllerFieldId.ShootingElementCustomEffectKind;
        }
    }

    /// <summary>
    /// Applies one resolved scaling value to one property inside a per-element behaviour block.
    /// </summary>
    /// <param name="elementSettings">Mutable per-element behaviour block.</param>
    /// <param name="propertyIndex">Offset of the property inside the behaviour range.</param>
    /// <param name="resolvedValue">Final numeric value produced by the scaling formula.</param>
    private static void ApplyElementBehaviourProperty(ref ElementBulletSettingsBlob elementSettings,
                                                      int propertyIndex,
                                                      float resolvedValue)
    {
        switch (propertyIndex)
        {
            case 0:
                elementSettings.EffectKind = PlayerRuntimeScalingEnumUtility.ResolveElementalEffectKind(resolvedValue);
                return;
            case 1:
                elementSettings.ProcMode = PlayerRuntimeScalingEnumUtility.ResolveElementalProcMode(resolvedValue);
                return;
            case 2:
                elementSettings.ReapplyMode = PlayerRuntimeScalingEnumUtility.ResolveElementalProcReapplyMode(resolvedValue);
                return;
            case 3:
                elementSettings.StacksPerHit = math.max(0f, resolvedValue);
                return;
            case 4:
                elementSettings.ProcThresholdStacks = math.max(0.1f, resolvedValue);
                return;
            case 5:
                elementSettings.MaximumStacks = math.max(0.1f, resolvedValue);
                return;
            case 6:
                elementSettings.StackDecayPerSecond = math.max(0f, resolvedValue);
                return;
            case 7:
                elementSettings.ConsumeStacksOnProc = resolvedValue >= 0.5f ? (byte)1 : (byte)0;
                return;
            case 8:
                elementSettings.DotDamagePerTick = math.max(0f, resolvedValue);
                return;
            case 9:
                elementSettings.DotTickInterval = math.max(0.01f, resolvedValue);
                return;
            case 10:
                elementSettings.DotDurationSeconds = math.max(0.05f, resolvedValue);
                return;
            case 11:
                elementSettings.ImpedimentSlowPercentPerStack = math.clamp(resolvedValue, 0f, 100f);
                return;
            case 12:
                elementSettings.ImpedimentProcSlowPercent = math.clamp(resolvedValue, 0f, 100f);
                return;
            case 13:
                elementSettings.ImpedimentMaxSlowPercent = math.clamp(resolvedValue, 0f, 100f);
                return;
            case 14:
                elementSettings.ImpedimentDurationSeconds = math.max(0.05f, resolvedValue);
                return;
        }
    }
    #endregion

    #endregion
}
