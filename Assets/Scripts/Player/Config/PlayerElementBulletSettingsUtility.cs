using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Builds and resolves runtime-safe bullet element settings shared by authoring, bake and runtime systems.
/// </summary>
public static class PlayerElementBulletSettingsUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Populates immutable baseline applied-element slots from authored controller data.
    /// </summary>
    /// <param name="appliedElements">Authored element slot list.</param>
    /// <param name="buffer">Destination immutable baseline slot buffer.</param>
    public static void PopulateBaseAppliedElementsBuffer(IReadOnlyList<PlayerProjectileAppliedElement> appliedElements,
                                                         DynamicBuffer<PlayerBaseShootingAppliedElementSlot> buffer)
    {
        buffer.Clear();

        if (appliedElements == null)
            return;

        for (int slotIndex = 0; slotIndex < appliedElements.Count; slotIndex++)
        {
            buffer.Add(new PlayerBaseShootingAppliedElementSlot
            {
                Value = appliedElements[slotIndex]
            });
        }
    }

    /// <summary>
    /// Populates mutable runtime applied-element slots from authored controller data.
    /// </summary>
    /// <param name="appliedElements">Authored element slot list.</param>
    /// <param name="buffer">Destination mutable runtime slot buffer.</param>
    public static void PopulateRuntimeAppliedElementsBuffer(IReadOnlyList<PlayerProjectileAppliedElement> appliedElements,
                                                            DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> buffer)
    {
        buffer.Clear();

        if (appliedElements == null)
            return;

        for (int slotIndex = 0; slotIndex < appliedElements.Count; slotIndex++)
        {
            buffer.Add(new PlayerRuntimeShootingAppliedElementSlot
            {
                Value = appliedElements[slotIndex]
            });
        }
    }

    /// <summary>
    /// Copies immutable baseline applied-element slots into the mutable runtime buffer.
    /// </summary>
    /// <param name="source">Immutable baseline slot buffer.</param>
    /// <param name="destination">Mutable runtime slot buffer rebuilt in place.</param>
    public static void CopyBaseAppliedElementsToRuntime(DynamicBuffer<PlayerBaseShootingAppliedElementSlot> source,
                                                        DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> destination)
    {
        destination.Clear();

        if (!source.IsCreated)
            return;

        for (int slotIndex = 0; slotIndex < source.Length; slotIndex++)
        {
            destination.Add(new PlayerRuntimeShootingAppliedElementSlot
            {
                Value = source[slotIndex].Value
            });
        }
    }

    /// <summary>
    /// Converts authored per-element behaviour blocks into the runtime container used by ECS shooting configs.
    /// </summary>
    /// <param name="sourceSettings">Authored per-element behaviour container.</param>
    /// <returns>Runtime-safe per-element behaviour blob.</returns>
    public static ElementBulletSettingsByElementBlob BuildRuntimeSettingsByElement(ElementBulletSettingsByElement sourceSettings)
    {
        ElementBulletSettingsByElement resolvedSettings = sourceSettings;

        if (resolvedSettings == null)
            resolvedSettings = new ElementBulletSettingsByElement();

        resolvedSettings.Validate();

        return new ElementBulletSettingsByElementBlob
        {
            Fire = BuildRuntimeSettings(resolvedSettings.Fire),
            Ice = BuildRuntimeSettings(resolvedSettings.Ice),
            Poison = BuildRuntimeSettings(resolvedSettings.Poison),
            Custom = BuildRuntimeSettings(resolvedSettings.Custom)
        };
    }

    /// <summary>
    /// Converts one authored per-element behaviour block into the runtime ECS payload format.
    /// </summary>
    /// <param name="sourceSettings">Authored behaviour block.</param>
    /// <returns>Runtime-safe behaviour blob.</returns>
    public static ElementBulletSettingsBlob BuildRuntimeSettings(ElementBulletSettings sourceSettings)
    {
        ElementBulletSettings resolvedSettings = sourceSettings;

        if (resolvedSettings == null)
            resolvedSettings = new ElementBulletSettings();

        resolvedSettings.Validate();

        return new ElementBulletSettingsBlob
        {
            EffectKind = resolvedSettings.EffectKind,
            ProcMode = resolvedSettings.ProcMode,
            ReapplyMode = resolvedSettings.ReapplyMode,
            StacksPerHit = math.max(0f, resolvedSettings.StacksPerHit),
            ProcThresholdStacks = math.max(0.1f, resolvedSettings.ProcThresholdStacks),
            MaximumStacks = math.max(0.1f, resolvedSettings.MaximumStacks),
            StackDecayPerSecond = math.max(0f, resolvedSettings.StackDecayPerSecond),
            ConsumeStacksOnProc = resolvedSettings.ConsumeStacksOnProc ? (byte)1 : (byte)0,
            DotDamagePerTick = math.max(0f, resolvedSettings.DotDamagePerTick),
            DotTickInterval = math.max(0.01f, resolvedSettings.DotTickInterval),
            DotDurationSeconds = math.max(0.05f, resolvedSettings.DotDurationSeconds),
            ImpedimentSlowPercentPerStack = math.clamp(resolvedSettings.ImpedimentSlowPercentPerStack, 0f, 100f),
            ImpedimentProcSlowPercent = math.clamp(resolvedSettings.ImpedimentProcSlowPercent, 0f, 100f),
            ImpedimentMaxSlowPercent = math.clamp(resolvedSettings.ImpedimentMaxSlowPercent, 0f, 100f),
            ImpedimentDurationSeconds = math.max(0.05f, resolvedSettings.ImpedimentDurationSeconds)
        };
    }

    /// <summary>
    /// Builds the shared elemental effect payload consumed by projectile hit systems from one runtime bullet element config.
    /// </summary>
    /// <param name="elementType">Concrete gameplay element emitted by the projectile entry.</param>
    /// <param name="elementBulletSettings">Runtime bullet element behaviour settings.</param>
    /// <returns>Runtime elemental effect configuration.</returns>
    public static ElementalEffectConfig BuildEffectConfig(ElementType elementType,
                                                          in ElementBulletSettingsBlob elementBulletSettings)
    {
        return new ElementalEffectConfig
        {
            ElementType = elementType,
            EffectKind = elementBulletSettings.EffectKind,
            ProcMode = elementBulletSettings.ProcMode,
            ReapplyMode = elementBulletSettings.ReapplyMode,
            ProcThresholdStacks = math.max(0.1f, math.min(elementBulletSettings.ProcThresholdStacks, math.max(0.1f, elementBulletSettings.MaximumStacks))),
            MaximumStacks = math.max(0.1f, elementBulletSettings.MaximumStacks),
            StackDecayPerSecond = math.max(0f, elementBulletSettings.StackDecayPerSecond),
            ConsumeStacksOnProc = elementBulletSettings.ConsumeStacksOnProc,
            DotDamagePerTick = math.max(0f, elementBulletSettings.DotDamagePerTick),
            DotTickInterval = math.max(0.01f, elementBulletSettings.DotTickInterval),
            DotDurationSeconds = math.max(0.05f, elementBulletSettings.DotDurationSeconds),
            ImpedimentSlowPercentPerStack = math.clamp(elementBulletSettings.ImpedimentSlowPercentPerStack, 0f, 100f),
            ImpedimentProcSlowPercent = math.clamp(elementBulletSettings.ImpedimentProcSlowPercent, 0f, 100f),
            ImpedimentMaxSlowPercent = math.clamp(elementBulletSettings.ImpedimentMaxSlowPercent, 0f, 100f),
            ImpedimentDurationSeconds = math.max(0.05f, elementBulletSettings.ImpedimentDurationSeconds)
        };
    }

    /// <summary>
    /// Resolves the per-element runtime behaviour block matching one gameplay element selection.
    /// </summary>
    /// <param name="elementBehaviours">Runtime per-element behaviour container.</param>
    /// <param name="appliedElement">Gameplay element selector.</param>
    /// <param name="settings">Matching runtime behaviour block when available.</param>
    /// <returns>True when the selection resolves to a supported gameplay element.</returns>
    public static bool TryResolveSettings(in ElementBulletSettingsByElementBlob elementBehaviours,
                                          PlayerProjectileAppliedElement appliedElement,
                                          out ElementBulletSettingsBlob settings)
    {
        settings = default;

        switch (appliedElement)
        {
            case PlayerProjectileAppliedElement.Fire:
                settings = elementBehaviours.Fire;
                return true;
            case PlayerProjectileAppliedElement.Ice:
                settings = elementBehaviours.Ice;
                return true;
            case PlayerProjectileAppliedElement.Poison:
                settings = elementBehaviours.Poison;
                return true;
            case PlayerProjectileAppliedElement.Custom:
                settings = elementBehaviours.Custom;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Resolves one runtime applied-element slot value.
    /// </summary>
    /// <param name="appliedElements">Runtime slot buffer.</param>
    /// <param name="slotIndex">Slot index to read.</param>
    /// <returns>Resolved element value, or None when the index is out of range.</returns>
    public static PlayerProjectileAppliedElement ResolveAppliedElementAt(DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> appliedElements,
                                                                        int slotIndex)
    {
        if (!appliedElements.IsCreated || slotIndex < 0 || slotIndex >= appliedElements.Length)
            return PlayerProjectileAppliedElement.None;

        return appliedElements[slotIndex].Value;
    }

    /// <summary>
    /// Writes one runtime applied-element slot, growing the slot buffer when required.
    /// </summary>
    /// <param name="appliedElements">Runtime slot buffer to mutate.</param>
    /// <param name="slotIndex">Slot index to overwrite.</param>
    /// <param name="appliedElement">New element value.</param>
    public static void SetAppliedElementAt(DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> appliedElements,
                                           int slotIndex,
                                           PlayerProjectileAppliedElement appliedElement)
    {
        if (!appliedElements.IsCreated || slotIndex < 0)
            return;

        while (appliedElements.Length <= slotIndex)
        {
            appliedElements.Add(new PlayerRuntimeShootingAppliedElementSlot
            {
                Value = PlayerProjectileAppliedElement.None
            });
        }

        PlayerRuntimeShootingAppliedElementSlot slot = appliedElements[slotIndex];
        slot.Value = appliedElement;
        appliedElements[slotIndex] = slot;
    }

    /// <summary>
    /// Resolves the concrete elemental type emitted by the configured base bullet element selection.
    /// </summary>
    /// <param name="appliedElement">Runtime bullet element selector.</param>
    /// <returns>Matching elemental type, or Fire as a safe fallback.</returns>
    public static ElementType ResolveElementType(PlayerProjectileAppliedElement appliedElement)
    {
        switch (appliedElement)
        {
            case PlayerProjectileAppliedElement.Fire:
                return ElementType.Fire;
            case PlayerProjectileAppliedElement.Ice:
                return ElementType.Ice;
            case PlayerProjectileAppliedElement.Poison:
                return ElementType.Poison;
            case PlayerProjectileAppliedElement.Custom:
                return ElementType.Custom;
            default:
                return ElementType.Fire;
        }
    }
    #endregion

    #endregion
}
