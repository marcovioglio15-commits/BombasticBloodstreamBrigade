using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Shared lookup, prefab validation and VFX bake helpers used by player power-up bake utilities.
/// </summary>
public static class PlayerPowerUpBakeSharedUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds VFX cap settings from player authoring values.
    /// </summary>
    /// <param name="authoring">Source authoring component.</param>
    /// <returns>Sanitized VFX cap config.</returns>
    public static PlayerPowerUpVfxCapConfig BuildPowerUpVfxCapConfig(PlayerAuthoring authoring)
    {
        if (authoring == null)
            return default;

        return new PlayerPowerUpVfxCapConfig
        {
            MaxSamePrefabPerCell = math.max(0, authoring.MaxIdenticalOneShotVfxPerCell),
            CellSize = math.max(0.1f, authoring.OneShotVfxCellSize),
            MaxAttachedSamePrefabPerTarget = math.max(0, authoring.MaxAttachedElementalVfxPerTarget),
            MaxActiveOneShotVfx = math.max(0, authoring.MaxActiveOneShotPowerUpVfx),
            RefreshAttachedLifetimeOnCapHit = authoring.RefreshAttachedElementalVfxLifetimeOnCapHit ? (byte)1 : (byte)0
        };
    }

    /// <summary>
    /// Builds runtime elemental VFX assignments from the resolved authored catalog.
    /// </summary>
    /// <param name="authoring">Owning player authoring component.</param>
    /// <param name="assignments">Source elemental VFX assignments.</param>
    /// <param name="resolveDynamicPrefabEntity">Prefab-to-entity resolver provided by the baker.</param>
    /// <returns>Elemental VFX config for all supported elements.</returns>
    public static PlayerElementalVfxConfig BuildElementalVfxConfig(PlayerAuthoring authoring,
                                                                   IReadOnlyList<ElementalVfxByElementData> assignments,
                                                                   Func<GameObject, Entity> resolveDynamicPrefabEntity)
    {
        return new PlayerElementalVfxConfig
        {
            Fire = BuildElementalVfxDefinitionConfig(authoring,
                                                     ResolveElementalVfxAssignment(assignments, ElementType.Fire),
                                                     "Elemental Fire",
                                                     resolveDynamicPrefabEntity),
            Ice = BuildElementalVfxDefinitionConfig(authoring,
                                                    ResolveElementalVfxAssignment(assignments, ElementType.Ice),
                                                    "Elemental Ice",
                                                    resolveDynamicPrefabEntity),
            Poison = BuildElementalVfxDefinitionConfig(authoring,
                                                       ResolveElementalVfxAssignment(assignments, ElementType.Poison),
                                                       "Elemental Poison",
                                                       resolveDynamicPrefabEntity),
            Custom = BuildElementalVfxDefinitionConfig(authoring,
                                                       ResolveElementalVfxAssignment(assignments, ElementType.Custom),
                                                       "Elemental Custom",
                                                       resolveDynamicPrefabEntity)
        };
    }

    /// <summary>
    /// Resolves a modular active power-up by ID with optional positional fallback.
    /// </summary>
    /// <param name="preset">Source preset.</param>
    /// <param name="powerUpId">Requested power-up ID.</param>
    /// <param name="fallbackIndex">Fallback index inside the active list.</param>
    /// <param name="allowFallback">True when the fallback index can be used after ID lookup fails.</param>
    /// <returns>Resolved modular active power-up or null.</returns>
    public static ModularPowerUpDefinition ResolveLoadoutActivePowerUp(PlayerPowerUpsPreset preset,
                                                                       string powerUpId,
                                                                       int fallbackIndex,
                                                                       bool allowFallback = true)
    {
        if (preset == null)
            return null;

        IReadOnlyList<ModularPowerUpDefinition> activePowerUps = preset.ActivePowerUps;

        if (activePowerUps == null)
            return null;

        if (!string.IsNullOrWhiteSpace(powerUpId))
        {
            for (int index = 0; index < activePowerUps.Count; index++)
            {
                ModularPowerUpDefinition activePowerUp = activePowerUps[index];

                if (activePowerUp == null)
                    continue;

                PowerUpCommonData commonData = activePowerUp.CommonData;

                if (commonData == null)
                    continue;

                if (!string.Equals(commonData.PowerUpId, powerUpId, StringComparison.OrdinalIgnoreCase))
                    continue;

                return activePowerUp;
            }
        }

        if (allowFallback && fallbackIndex >= 0 && fallbackIndex < activePowerUps.Count)
            return activePowerUps[fallbackIndex];

        return null;
    }

    /// <summary>
    /// Resolves a modular passive power-up by ID.
    /// </summary>
    /// <param name="preset">Source preset.</param>
    /// <param name="powerUpId">Requested power-up ID.</param>
    /// <returns>Resolved modular passive power-up or null.</returns>
    public static ModularPowerUpDefinition ResolveLoadoutPassivePowerUp(PlayerPowerUpsPreset preset, string powerUpId)
    {
        if (preset == null)
            return null;

        IReadOnlyList<ModularPowerUpDefinition> passivePowerUps = preset.PassivePowerUps;

        if (passivePowerUps == null)
            return null;

        if (string.IsNullOrWhiteSpace(powerUpId))
            return null;

        for (int index = 0; index < passivePowerUps.Count; index++)
        {
            ModularPowerUpDefinition passivePowerUp = passivePowerUps[index];

            if (passivePowerUp == null)
                continue;

            PowerUpCommonData commonData = passivePowerUp.CommonData;

            if (commonData == null)
                continue;

            if (!string.Equals(commonData.PowerUpId, powerUpId, StringComparison.OrdinalIgnoreCase))
                continue;

            return passivePowerUp;
        }

        return null;
    }

    /// <summary>
    /// Resolves a legacy active tool by ID with positional fallback.
    /// </summary>
    /// <param name="preset">Source preset.</param>
    /// <param name="toolId">Requested tool ID.</param>
    /// <param name="fallbackIndex">Fallback index inside the active tool list.</param>
    /// <returns>Resolved active tool or null.</returns>
    public static ActiveToolDefinition ResolveLoadoutTool(PlayerPowerUpsPreset preset, string toolId, int fallbackIndex)
    {
        if (preset == null)
            return null;

        IReadOnlyList<ActiveToolDefinition> activeTools = preset.ActiveTools;

        if (activeTools == null)
            return null;

        if (!string.IsNullOrWhiteSpace(toolId))
        {
            for (int index = 0; index < activeTools.Count; index++)
            {
                ActiveToolDefinition activeTool = activeTools[index];

                if (activeTool == null)
                    continue;

                PowerUpCommonData commonData = activeTool.CommonData;

                if (commonData == null)
                    continue;

                if (!string.Equals(commonData.PowerUpId, toolId, StringComparison.OrdinalIgnoreCase))
                    continue;

                return activeTool;
            }
        }

        if (fallbackIndex >= 0 && fallbackIndex < activeTools.Count)
            return activeTools[fallbackIndex];

        return null;
    }

    /// <summary>
    /// Resolves a legacy passive tool by ID.
    /// </summary>
    /// <param name="preset">Source preset.</param>
    /// <param name="toolId">Requested tool ID.</param>
    /// <returns>Resolved passive tool or null.</returns>
    public static PassiveToolDefinition ResolveLoadoutPassiveTool(PlayerPowerUpsPreset preset, string toolId)
    {
        if (preset == null)
            return null;

        IReadOnlyList<PassiveToolDefinition> passiveTools = preset.PassiveTools;

        if (passiveTools == null || string.IsNullOrWhiteSpace(toolId))
            return null;

        for (int index = 0; index < passiveTools.Count; index++)
        {
            PassiveToolDefinition passiveTool = passiveTools[index];

            if (passiveTool == null)
                continue;

            PowerUpCommonData commonData = passiveTool.CommonData;

            if (commonData == null)
                continue;

            if (!string.Equals(commonData.PowerUpId, toolId, StringComparison.OrdinalIgnoreCase))
                continue;

            return passiveTool;
        }

        return null;
    }

    /// <summary>
    /// Resolves one module definition by ID from the module catalog.
    /// </summary>
    /// <param name="preset">Source preset.</param>
    /// <param name="moduleId">Requested module ID.</param>
    /// <returns>Resolved module definition or null.</returns>
    public static PowerUpModuleDefinition ResolveModuleDefinitionById(PlayerPowerUpsPreset preset, string moduleId)
    {
        if (preset == null || string.IsNullOrWhiteSpace(moduleId))
            return null;

        IReadOnlyList<PowerUpModuleDefinition> modules = preset.ModuleDefinitions;

        if (modules == null)
            return null;

        for (int index = 0; index < modules.Count; index++)
        {
            PowerUpModuleDefinition module = modules[index];

            if (module == null)
                continue;

            if (string.Equals(module.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
                return module;
        }

        return null;
    }

    /// <summary>
    /// Resolves and validates one optional power-up prefab to an ECS prefab entity.
    /// </summary>
    /// <param name="authoring">Owning player authoring component.</param>
    /// <param name="optionalPrefab">Prefab candidate.</param>
    /// <param name="contextLabel">Label used in validation logs.</param>
    /// <param name="resolveDynamicPrefabEntity">Prefab-to-entity resolver provided by the baker.</param>
    /// <returns>Resolved entity or Entity.Null when validation fails.</returns>
    public static Entity ResolveOptionalPowerUpPrefabEntity(PlayerAuthoring authoring,
                                                            GameObject optionalPrefab,
                                                            string contextLabel,
                                                            Func<GameObject, Entity> resolveDynamicPrefabEntity)
    {
        if (optionalPrefab == null)
            return Entity.Null;

        if (IsInvalidPowerUpPrefab(authoring, optionalPrefab))
        {
#if UNITY_EDITOR
            if (authoring != null)
                Debug.LogError(string.Format("[PlayerAuthoringBaker] Invalid prefab '{0}' in '{1}' on '{2}'. Assign a dedicated prefab without PlayerAuthoring.", optionalPrefab.name, contextLabel, authoring.name), authoring);
#endif
            return Entity.Null;
        }

        return ResolvePrefabEntity(resolveDynamicPrefabEntity, optionalPrefab);
    }

    /// <summary>
    /// Resolves a validated prefab through the baker callback.
    /// </summary>
    /// <param name="resolveDynamicPrefabEntity">Prefab-to-entity resolver provided by the baker.</param>
    /// <param name="prefab">Prefab to resolve.</param>
    /// <returns>Resolved entity or Entity.Null when no resolver is available.</returns>
    public static Entity ResolvePrefabEntity(Func<GameObject, Entity> resolveDynamicPrefabEntity, GameObject prefab)
    {
        if (resolveDynamicPrefabEntity == null || prefab == null)
            return Entity.Null;

        return resolveDynamicPrefabEntity(prefab);
    }

    /// <summary>
    /// Validates bomb prefabs used by active power-ups.
    /// </summary>
    /// <param name="authoring">Owning player authoring component.</param>
    /// <param name="bombPrefab">Bomb prefab candidate.</param>
    /// <returns>True when the prefab is not valid for baking.</returns>
    public static bool IsInvalidBombPrefab(PlayerAuthoring authoring, GameObject bombPrefab)
    {
        if (bombPrefab == null)
            return true;

        if (authoring != null && bombPrefab == authoring.gameObject)
            return true;

        if (bombPrefab.GetComponent<PlayerAuthoring>() != null)
            return true;

        return false;
    }

    /// <summary>
    /// Validates generic VFX prefabs used by power-up bake paths.
    /// </summary>
    /// <param name="authoring">Owning player authoring component.</param>
    /// <param name="optionalPrefab">Prefab candidate.</param>
    /// <returns>True when the prefab is not valid for baking.</returns>
    public static bool IsInvalidPowerUpPrefab(PlayerAuthoring authoring, GameObject optionalPrefab)
    {
        if (optionalPrefab == null)
            return true;

        if (authoring != null && optionalPrefab == authoring.gameObject)
            return true;

        if (optionalPrefab.GetComponent<PlayerAuthoring>() != null)
            return true;

        return false;
    }

    /// <summary>
    /// Converts elemental effect authoring data into runtime-safe values.
    /// </summary>
    /// <param name="effectData">Source elemental effect definition.</param>
    /// <returns>Runtime elemental effect config.</returns>
    public static ElementalEffectConfig BuildElementalEffectConfig(ElementalEffectDefinitionData effectData)
    {
        if (effectData == null)
            return default;

        float procThresholdStacks = math.max(0.1f, effectData.ProcThresholdStacks);
        float maximumStacks = math.max(0.1f, effectData.MaximumStacks);

        if (maximumStacks < procThresholdStacks)
            maximumStacks = procThresholdStacks;

        return new ElementalEffectConfig
        {
            ElementType = effectData.ElementType,
            EffectKind = effectData.EffectKind,
            ProcMode = effectData.ProcMode,
            ReapplyMode = effectData.ReapplyMode,
            ProcThresholdStacks = procThresholdStacks,
            MaximumStacks = maximumStacks,
            StackDecayPerSecond = math.max(0f, effectData.StackDecayPerSecond),
            ConsumeStacksOnProc = effectData.ConsumeStacksOnProc ? (byte)1 : (byte)0,
            DotDamagePerTick = math.max(0f, effectData.DotDamagePerTick),
            DotTickInterval = math.max(0.01f, effectData.DotTickInterval),
            DotDurationSeconds = math.max(0.05f, effectData.DotDurationSeconds),
            ImpedimentSlowPercentPerStack = math.clamp(effectData.ImpedimentSlowPercentPerStack, 0f, 100f),
            ImpedimentProcSlowPercent = math.clamp(effectData.ImpedimentProcSlowPercent, 0f, 100f),
            ImpedimentMaxSlowPercent = math.clamp(effectData.ImpedimentMaxSlowPercent, 0f, 100f),
            ImpedimentDurationSeconds = math.max(0.05f, effectData.ImpedimentDurationSeconds)
        };
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves one elemental VFX assignment from the preset list.
    /// </summary>
    /// <param name="assignments">Source assignment list.</param>
    /// <param name="elementType">Requested element type.</param>
    /// <returns>Matching assignment or null.</returns>
    private static ElementalVfxByElementData ResolveElementalVfxAssignment(IReadOnlyList<ElementalVfxByElementData> assignments, ElementType elementType)
    {
        if (assignments == null)
            return null;

        for (int index = 0; index < assignments.Count; index++)
        {
            ElementalVfxByElementData assignment = assignments[index];

            if (assignment == null || assignment.ElementType != elementType)
                continue;

            return assignment;
        }

        return null;
    }

    /// <summary>
    /// Compiles one elemental VFX assignment into runtime-safe data.
    /// </summary>
    /// <param name="authoring">Owning player authoring component.</param>
    /// <param name="assignment">Source elemental assignment.</param>
    /// <param name="labelPrefix">Label used in validation logs.</param>
    /// <param name="resolveDynamicPrefabEntity">Prefab-to-entity resolver provided by the baker.</param>
    /// <returns>Runtime elemental VFX definition.</returns>
    private static ElementalVfxDefinitionConfig BuildElementalVfxDefinitionConfig(PlayerAuthoring authoring,
                                                                                  ElementalVfxByElementData assignment,
                                                                                  string labelPrefix,
                                                                                  Func<GameObject, Entity> resolveDynamicPrefabEntity)
    {
        if (assignment == null)
            return default;

        Entity stackVfxPrefabEntity = Entity.Null;
        Entity procVfxPrefabEntity = Entity.Null;

        if (authoring != null && authoring.BakePowerUpVfxEntityPrefabs)
        {
            stackVfxPrefabEntity = ResolveOptionalPowerUpPrefabEntity(authoring,
                                                                      assignment.StackVfxPrefab,
                                                                      labelPrefix + " Stack VFX",
                                                                      resolveDynamicPrefabEntity);
            procVfxPrefabEntity = ResolveOptionalPowerUpPrefabEntity(authoring,
                                                                     assignment.ProcVfxPrefab,
                                                                     labelPrefix + " Proc VFX",
                                                                     resolveDynamicPrefabEntity);
        }

        return new ElementalVfxDefinitionConfig
        {
            SpawnStackVfx = assignment.SpawnStackVfx ? (byte)1 : (byte)0,
            StackVfxPrefabEntity = stackVfxPrefabEntity,
            StackVfxScaleMultiplier = math.max(0.01f, assignment.StackVfxScaleMultiplier),
            SpawnProcVfx = assignment.SpawnProcVfx ? (byte)1 : (byte)0,
            ProcVfxPrefabEntity = procVfxPrefabEntity,
            ProcVfxScaleMultiplier = math.max(0.01f, assignment.ProcVfxScaleMultiplier)
        };
    }
    #endregion

    #endregion
}
