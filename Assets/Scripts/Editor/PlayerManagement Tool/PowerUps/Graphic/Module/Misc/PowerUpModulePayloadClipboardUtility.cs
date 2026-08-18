using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Stores one in-memory module payload and applies it only to module definitions or bindings with the same runtime kind.
/// </summary>
public static class PowerUpModulePayloadClipboardUtility
{
    #region Fields
    private static string payloadJson;
    private static PowerUpModuleKind payloadKind;
    #endregion

    #region Events
    public static event Action ClipboardChanged;
    #endregion

    #region Methods

    #region Clipboard State
    /// <summary>
    /// Reports whether the clipboard contains a payload compatible with the requested module kind.
    /// </summary>
    /// <param name="moduleKind">Runtime module kind expected by the paste target.</param>
    /// <returns>True when a non-empty payload of the same kind is available.</returns>
    public static bool CanPaste(PowerUpModuleKind moduleKind)
    {
        return !string.IsNullOrEmpty(payloadJson) && payloadKind == moduleKind;
    }

    /// <summary>
    /// Keeps one paste button synchronized with clipboard compatibility while it is attached to a panel.
    /// </summary>
    /// <param name="button">Paste button whose enabled state is updated.</param>
    /// <param name="resolveModuleKind">Callback resolving the button target's current module kind.</param>
    public static void TrackPasteAvailability(Button button, Func<PowerUpModuleKind> resolveModuleKind)
    {
        if (button == null || resolveModuleKind == null)
            return;

        Action refresh = () => button.SetEnabled(CanPaste(resolveModuleKind()));
        button.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            ClipboardChanged += refresh;
            refresh();
        });
        button.RegisterCallback<DetachFromPanelEvent>(evt =>
        {
            ClipboardChanged -= refresh;
        });
        button.RegisterCallback<PointerEnterEvent>(evt => refresh());
        refresh();
    }
    #endregion

    #region Module Definitions
    /// <summary>
    /// Copies the complete data payload owned by one reusable module definition.
    /// </summary>
    /// <param name="preset">Preset containing the source module definition.</param>
    /// <param name="moduleIndex">Source index inside the module catalog.</param>
    /// <returns>True when the payload was copied.</returns>
    public static bool CopyDefinitionPayload(PlayerPowerUpsPreset preset, int moduleIndex)
    {
        if (!TryResolveDefinition(preset, moduleIndex, out PowerUpModuleDefinition definition))
            return false;

        return CopyPayload(definition.ModuleKind, definition.Data);
    }

    /// <summary>
    /// Pastes the clipboard into one reusable module definition when its kind matches.
    /// </summary>
    /// <param name="preset">Preset containing the target module definition.</param>
    /// <param name="moduleIndex">Target index inside the module catalog.</param>
    /// <returns>True when the target payload changed.</returns>
    public static bool PasteDefinitionPayload(PlayerPowerUpsPreset preset, int moduleIndex)
    {
        if (!TryResolveDefinition(preset, moduleIndex, out PowerUpModuleDefinition definition) ||
            !CanPaste(definition.ModuleKind))
            return false;

        Undo.RecordObject(preset, "Paste Module Payload");

        if (!PastePayload(definition.Data))
            return false;

        EditorUtility.SetDirty(preset);
        PlayerManagementDraftSession.MarkDirty();
        return true;
    }
    #endregion

    #region Module Bindings
    /// <summary>
    /// Copies the effective payload of one binding, resolving module defaults when its override is disabled.
    /// </summary>
    /// <param name="powerUpProperty">Serialized modular power-up that owns the binding.</param>
    /// <param name="bindingIndex">Source binding index.</param>
    /// <returns>True when an effective payload was copied.</returns>
    public static bool CopyBindingPayload(SerializedProperty powerUpProperty, int bindingIndex)
    {
        if (!TryResolveBinding(powerUpProperty,
                               bindingIndex,
                               out PlayerPowerUpsPreset preset,
                               out PowerUpModuleBinding binding,
                               out PowerUpModuleDefinition definition))
            return false;

        return CopyPayload(definition.ModuleKind, binding.ResolvePayload(definition));
    }

    /// <summary>
    /// Pastes the clipboard into one binding override and enables that override so the copied values become authoritative.
    /// </summary>
    /// <param name="powerUpProperty">Serialized modular power-up that owns the binding.</param>
    /// <param name="bindingIndex">Target binding index.</param>
    /// <returns>True when the target override changed.</returns>
    public static bool PasteBindingPayload(SerializedProperty powerUpProperty, int bindingIndex)
    {
        if (!TryResolveBinding(powerUpProperty,
                               bindingIndex,
                               out PlayerPowerUpsPreset preset,
                               out PowerUpModuleBinding binding,
                               out PowerUpModuleDefinition definition) ||
            !CanPaste(definition.ModuleKind))
            return false;

        Undo.RecordObject(preset, "Paste Module Binding Payload");

        if (!PastePayload(binding.OverridePayload))
            return false;

        binding.ConfigureOverride(true, binding.OverridePayload);
        EditorUtility.SetDirty(preset);
        PlayerManagementDraftSession.MarkDirty();
        return true;
    }

    /// <summary>
    /// Resolves the runtime module kind referenced by one binding card without allocating a module catalog.
    /// </summary>
    /// <param name="powerUpProperty">Serialized modular power-up that owns the binding.</param>
    /// <param name="bindingIndex">Binding index to inspect.</param>
    /// <returns>Resolved module kind, or the first enum value when the binding cannot be resolved.</returns>
    public static PowerUpModuleKind ResolveBindingKind(SerializedProperty powerUpProperty, int bindingIndex)
    {
        return TryResolveBinding(powerUpProperty,
                                 bindingIndex,
                                 out PlayerPowerUpsPreset preset,
                                 out PowerUpModuleBinding binding,
                                 out PowerUpModuleDefinition definition)
            ? definition.ModuleKind
            : default;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Serializes one payload snapshot and announces the new compatible module kind to attached paste buttons.
    /// </summary>
    /// <param name="moduleKind">Runtime module kind owning the payload schema.</param>
    /// <param name="payload">Payload to copy.</param>
    /// <returns>True when serialization produced a snapshot.</returns>
    private static bool CopyPayload(PowerUpModuleKind moduleKind, PowerUpModuleData payload)
    {
        if (payload == null)
            return false;

        string serializedPayload = JsonUtility.ToJson(payload);

        if (string.IsNullOrEmpty(serializedPayload))
            return false;

        payloadJson = serializedPayload;
        payloadKind = moduleKind;
        ClipboardChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Overwrites one existing payload instance so references held by the serialized preset remain stable.
    /// </summary>
    /// <param name="targetPayload">Existing payload instance receiving clipboard values.</param>
    /// <returns>True when compatible clipboard JSON was applied.</returns>
    private static bool PastePayload(PowerUpModuleData targetPayload)
    {
        if (targetPayload == null || string.IsNullOrEmpty(payloadJson))
            return false;

        JsonUtility.FromJsonOverwrite(payloadJson, targetPayload);
        return true;
    }

    /// <summary>
    /// Resolves one module definition by stable catalog index.
    /// </summary>
    /// <param name="preset">Preset containing the module catalog.</param>
    /// <param name="moduleIndex">Requested module index.</param>
    /// <param name="definition">Resolved definition when available.</param>
    /// <returns>True when the requested definition and its payload exist.</returns>
    private static bool TryResolveDefinition(PlayerPowerUpsPreset preset,
                                             int moduleIndex,
                                             out PowerUpModuleDefinition definition)
    {
        definition = null;

        if (preset == null || preset.ModuleDefinitions == null ||
            moduleIndex < 0 || moduleIndex >= preset.ModuleDefinitions.Count)
            return false;

        definition = preset.ModuleDefinitions[moduleIndex];
        return definition != null && definition.Data != null;
    }

    /// <summary>
    /// Resolves one serialized binding to its typed preset objects and referenced module definition.
    /// </summary>
    /// <param name="powerUpProperty">Serialized modular power-up containing the binding array.</param>
    /// <param name="bindingIndex">Binding index to resolve.</param>
    /// <param name="preset">Owning power-up preset.</param>
    /// <param name="binding">Resolved binding instance.</param>
    /// <param name="definition">Referenced module definition.</param>
    /// <returns>True when the complete binding context is valid.</returns>
    private static bool TryResolveBinding(SerializedProperty powerUpProperty,
                                          int bindingIndex,
                                          out PlayerPowerUpsPreset preset,
                                          out PowerUpModuleBinding binding,
                                          out PowerUpModuleDefinition definition)
    {
        preset = powerUpProperty?.serializedObject?.targetObject as PlayerPowerUpsPreset;
        binding = null;
        definition = null;

        if (preset == null || powerUpProperty == null)
            return false;

        SerializedProperty commonDataProperty = powerUpProperty.FindPropertyRelative("commonData");
        SerializedProperty powerUpIdProperty = commonDataProperty?.FindPropertyRelative("powerUpId");

        if (powerUpIdProperty == null)
            return false;

        ModularPowerUpDefinition powerUp = ResolvePowerUp(preset,
                                                           powerUpProperty.propertyPath,
                                                           powerUpIdProperty.stringValue);

        if (powerUp == null || powerUp.ModuleBindings == null ||
            bindingIndex < 0 || bindingIndex >= powerUp.ModuleBindings.Count)
            return false;

        binding = powerUp.ModuleBindings[bindingIndex];

        if (binding == null)
            return false;

        definition = ResolveDefinitionById(preset.ModuleDefinitions, binding.ModuleId);
        return definition != null && binding.OverridePayload != null;
    }

    /// <summary>
    /// Resolves one active or passive modular power-up by its serialized collection and stable identifier.
    /// </summary>
    /// <param name="preset">Preset containing both modular catalogs.</param>
    /// <param name="propertyPath">Serialized path identifying the active or passive collection.</param>
    /// <param name="powerUpId">Stable power-up identifier to match.</param>
    /// <returns>Matching modular power-up, or null when it cannot be found.</returns>
    private static ModularPowerUpDefinition ResolvePowerUp(PlayerPowerUpsPreset preset,
                                                            string propertyPath,
                                                            string powerUpId)
    {
        IReadOnlyList<ModularPowerUpDefinition> powerUps = propertyPath.StartsWith("activePowerUps", StringComparison.Ordinal)
            ? preset.ActivePowerUps
            : preset.PassivePowerUps;

        if (powerUps == null)
            return null;

        for (int powerUpIndex = 0; powerUpIndex < powerUps.Count; powerUpIndex++)
        {
            ModularPowerUpDefinition powerUp = powerUps[powerUpIndex];

            if (powerUp?.CommonData != null &&
                string.Equals(powerUp.CommonData.PowerUpId, powerUpId, StringComparison.Ordinal))
                return powerUp;
        }

        return null;
    }

    /// <summary>
    /// Resolves a referenced module definition by stable module identifier.
    /// </summary>
    /// <param name="definitions">Module definition catalog to inspect.</param>
    /// <param name="moduleId">Referenced module identifier.</param>
    /// <returns>Matching module definition, or null when unresolved.</returns>
    private static PowerUpModuleDefinition ResolveDefinitionById(IReadOnlyList<PowerUpModuleDefinition> definitions,
                                                                 string moduleId)
    {
        if (definitions == null)
            return null;

        for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
        {
            PowerUpModuleDefinition definition = definitions[definitionIndex];

            if (definition != null && string.Equals(definition.ModuleId, moduleId, StringComparison.Ordinal))
                return definition;
        }

        return null;
    }
    #endregion

    #endregion
}
