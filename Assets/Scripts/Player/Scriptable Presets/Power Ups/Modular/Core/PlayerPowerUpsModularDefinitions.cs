using System;
using UnityEngine;
using UnityEngine.Serialization;

#region Module Definitions
[Serializable]
public sealed class PowerUpModuleData
{
    #region Fields

    #region Serialized Fields
    [Header("Trigger - Hold Charge")]
    [Tooltip("Hold-charge settings used by TriggerHoldCharge modules.")]
    [SerializeField] private PowerUpHoldChargeModuleData holdCharge = new PowerUpHoldChargeModuleData();

    [Header("Trigger - Event")]
    [Tooltip("Event trigger settings used by TriggerEvent modules.")]
    [SerializeField] private PowerUpTriggerEventModuleData triggerEvent = new PowerUpTriggerEventModuleData();

    [Header("Gate - Resource")]
    [Tooltip("Resource-gate settings used by GateResource modules.")]
    [SerializeField] private PowerUpResourceGateModuleData resourceGate = new PowerUpResourceGateModuleData();

    [Header("State - Suppress Shooting")]
    [Tooltip("Shooting suppression settings used by StateSuppressShooting modules.")]
    [SerializeField] private PowerUpSuppressShootingModuleData suppressShooting = new PowerUpSuppressShootingModuleData();

    [Header("Execute - Projectile Pattern")]
    [Tooltip("Projectile cone settings used by ProjectilesPatternCone modules.")]
    [SerializeField] private PowerUpProjectilePatternConeModuleData projectilePatternCone = new PowerUpProjectilePatternConeModuleData();

    [Header("Post Execute - Character Tuning")]
    [Tooltip("Scalable-stat assignment settings applied on acquisition for standard actives, while owned for passives, temporarily during charge with Trigger Hold Charge, or only while active with toggleable Resource Gate.")]
    [FormerlySerializedAs("projectileTuning")]
    [SerializeField] private PowerUpCharacterTuningModuleData characterTuning = new PowerUpCharacterTuningModuleData();

    [Header("Execute - Spawn Object")]
    [Tooltip("Spawn-object settings used by SpawnObject modules.")]
    [SerializeField] private BombToolData bomb = new BombToolData();

    [Header("Execute - Dash")]
    [Tooltip("Dash settings used by Dash modules.")]
    [SerializeField] private DashToolData dash = new DashToolData();

    [Header("Execute - Time Dilation")]
    [Tooltip("Time dilation settings used by TimeDilationEnemies modules.")]
    [SerializeField] private BulletTimeToolData bulletTime = new BulletTimeToolData();

    [Header("Execute - Impact Frame")]
    [Tooltip("Customizable global time slowdown plus screen filter triggered on power-up activation. Used by ImpactFrame modules.")]
    [SerializeField] private PowerUpImpactFrameModuleData impactFrame = new PowerUpImpactFrameModuleData();

    [Header("Execute - Ghost Trail")]
    [Tooltip("Pooled residual-image trail and optional screen feedback triggered on power-up activation. Used by GhostTrail modules.")]
    [SerializeField] private PowerUpGhostTrailModuleData ghostTrail = new PowerUpGhostTrailModuleData();

    [Header("Execute - Heal")]
    [Tooltip("Healing settings used by Heal modules.")]
    [SerializeField] private PowerUpHealMissingHealthModuleData healMissingHealth = new PowerUpHealMissingHealthModuleData();

    [Header("Execute - Drop Attraction")]
    [Tooltip("Enemy-drop attraction settings used by AttractDrops modules.")]
    [SerializeField]
    private PowerUpDropAttractionModuleData dropAttraction = new PowerUpDropAttractionModuleData();

    [Header("Hook - Death Explosion")]
    [Tooltip("Explosion settings used by DeathExplosion modules.")]
    [SerializeField] private ExplosionPassiveToolData deathExplosion = new ExplosionPassiveToolData();

    [Header("Hook - Orbital Projectiles")]
    [Tooltip("Orbit settings used by OrbitalProjectiles modules.")]
    [SerializeField] private PerfectCirclePassiveToolData projectileOrbitOverride = new PerfectCirclePassiveToolData();

    [Header("Hook - Orbital Projections")]
    [Tooltip("Object-orbital settings used by OrbitalProjections modules.")]
    [SerializeField] private PowerUpOrbitalProjectionsModuleData orbitalProjections = new PowerUpOrbitalProjectionsModuleData();

    [Header("Hook - Bouncing Projectiles")]
    [Tooltip("Bounce settings used by BouncingProjectiles modules.")]
    [SerializeField] private BouncingProjectilesPassiveToolData projectileBounceOnWalls = new BouncingProjectilesPassiveToolData();

    [Header("Execute - Returning Projectiles")]
    [Tooltip("Return trajectory and projectile interaction settings used by ReturningProjectiles modules.")]
    [SerializeField]
    private PowerUpReturningProjectilesModuleData returningProjectiles = new PowerUpReturningProjectilesModuleData();

    [Header("Hook - Projectile Split")]
    [Tooltip("Split settings used by ProjectileSplit modules.")]
    [FormerlySerializedAs("projectileSplitOnDeath")]
    [SerializeField] private SplittingProjectilesPassiveToolData projectileSplit = new SplittingProjectilesPassiveToolData();

    [Header("Hook - Laser Beam")]
    [Tooltip("Continuous beam settings used by LaserBeam modules.")]
    [SerializeField] private PowerUpLaserBeamModuleData laserBeam = new PowerUpLaserBeamModuleData();

    [Header("Hook - Switch Weapon")]
    [Tooltip("Alternate weapon mesh selection shown alongside the always-visible Base Gun while the owning power-up is equipped, replacing the Player Visual Preset optional attachment.")]
    [SerializeField]
    private PowerUpSwitchWeaponModuleData switchWeapon = new PowerUpSwitchWeaponModuleData();

    [Header("Hook - Trail Spawn")]
    [Tooltip("Trail spawn settings used by SpawnTrailSegment modules.")]
    [SerializeField] private PowerUpTrailSpawnModuleData trailSpawn = new PowerUpTrailSpawnModuleData();

    [Header("Hook - Elemental Area Tick")]
    [Tooltip("Area tick elemental settings used by AreaTickApplyElement modules.")]
    [SerializeField] private PowerUpElementalAreaTickModuleData elementalAreaTick = new PowerUpElementalAreaTickModuleData();

    [Header("Post Execute - Stackable")]
    [Tooltip("Stack-count settings used by Stackable modules.")]
    [SerializeField] private PowerUpStackableModuleData stackable = new PowerUpStackableModuleData();
    #endregion

    #endregion

    #region Properties
    public PowerUpHoldChargeModuleData HoldCharge
    {
        get
        {
            return holdCharge;
        }
    }

    public PowerUpResourceGateModuleData ResourceGate
    {
        get
        {
            return resourceGate;
        }
    }

    public PowerUpTriggerEventModuleData TriggerEvent
    {
        get
        {
            return triggerEvent;
        }
    }

    public PowerUpSuppressShootingModuleData SuppressShooting
    {
        get
        {
            return suppressShooting;
        }
    }

    public PowerUpProjectilePatternConeModuleData ProjectilePatternCone
    {
        get
        {
            return projectilePatternCone;
        }
    }

    public PowerUpCharacterTuningModuleData CharacterTuning
    {
        get
        {
            return characterTuning;
        }
    }

    public BombToolData Bomb
    {
        get
        {
            return bomb;
        }
    }

    public DashToolData Dash
    {
        get
        {
            return dash;
        }
    }

    public BulletTimeToolData BulletTime
    {
        get
        {
            return bulletTime;
        }
    }

    public PowerUpImpactFrameModuleData ImpactFrame
    {
        get
        {
            return impactFrame;
        }
    }

    public PowerUpGhostTrailModuleData GhostTrail
    {
        get
        {
            return ghostTrail;
        }
    }

    public PowerUpHealMissingHealthModuleData HealMissingHealth
    {
        get
        {
            return healMissingHealth;
        }
    }

    public PowerUpDropAttractionModuleData DropAttraction
    {
        get
        {
            return dropAttraction;
        }
    }

    public ExplosionPassiveToolData DeathExplosion
    {
        get
        {
            return deathExplosion;
        }
    }

    public PerfectCirclePassiveToolData ProjectileOrbitOverride
    {
        get
        {
            return projectileOrbitOverride;
        }
    }

    public PowerUpOrbitalProjectionsModuleData OrbitalProjections
    {
        get
        {
            return orbitalProjections;
        }
    }

    public BouncingProjectilesPassiveToolData ProjectileBounceOnWalls
    {
        get
        {
            return projectileBounceOnWalls;
        }
    }

    public PowerUpReturningProjectilesModuleData ReturningProjectiles
    {
        get
        {
            return returningProjectiles;
        }
    }

    public SplittingProjectilesPassiveToolData ProjectileSplit
    {
        get
        {
            return projectileSplit;
        }
    }

    public PowerUpLaserBeamModuleData LaserBeam
    {
        get
        {
            return laserBeam;
        }
    }

    public PowerUpSwitchWeaponModuleData SwitchWeapon
    {
        get
        {
            return switchWeapon;
        }
    }

    public PowerUpTrailSpawnModuleData TrailSpawn
    {
        get
        {
            return trailSpawn;
        }
    }

    public PowerUpElementalAreaTickModuleData ElementalAreaTick
    {
        get
        {
            return elementalAreaTick;
        }
    }

    public PowerUpStackableModuleData Stackable
    {
        get
        {
            return stackable;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (holdCharge == null)
            holdCharge = new PowerUpHoldChargeModuleData();

        if (resourceGate == null)
            resourceGate = new PowerUpResourceGateModuleData();

        if (triggerEvent == null)
            triggerEvent = new PowerUpTriggerEventModuleData();

        if (suppressShooting == null)
            suppressShooting = new PowerUpSuppressShootingModuleData();

        if (projectilePatternCone == null)
            projectilePatternCone = new PowerUpProjectilePatternConeModuleData();

        if (characterTuning == null)
            characterTuning = new PowerUpCharacterTuningModuleData();

        if (bomb == null)
            bomb = new BombToolData();

        if (dash == null)
            dash = new DashToolData();

        if (bulletTime == null)
            bulletTime = new BulletTimeToolData();

        if (impactFrame == null)
            impactFrame = new PowerUpImpactFrameModuleData();

        if (ghostTrail == null)
            ghostTrail = new PowerUpGhostTrailModuleData();

        if (healMissingHealth == null)
            healMissingHealth = new PowerUpHealMissingHealthModuleData();

        if (dropAttraction == null)
            dropAttraction = new PowerUpDropAttractionModuleData();

        if (deathExplosion == null)
            deathExplosion = new ExplosionPassiveToolData();

        if (projectileOrbitOverride == null)
            projectileOrbitOverride = new PerfectCirclePassiveToolData();

        if (orbitalProjections == null)
            orbitalProjections = new PowerUpOrbitalProjectionsModuleData();

        if (projectileBounceOnWalls == null)
            projectileBounceOnWalls = new BouncingProjectilesPassiveToolData();

        if (returningProjectiles == null)
            returningProjectiles = new PowerUpReturningProjectilesModuleData();

        if (projectileSplit == null)
            projectileSplit = new SplittingProjectilesPassiveToolData();

        if (laserBeam == null)
            laserBeam = new PowerUpLaserBeamModuleData();

        if (switchWeapon == null)
            switchWeapon = new PowerUpSwitchWeaponModuleData();

        if (trailSpawn == null)
            trailSpawn = new PowerUpTrailSpawnModuleData();

        if (elementalAreaTick == null)
            elementalAreaTick = new PowerUpElementalAreaTickModuleData();

        if (stackable == null)
            stackable = new PowerUpStackableModuleData();

        holdCharge.Validate();
        resourceGate.Validate();
        projectilePatternCone.Validate();
        characterTuning.Validate();
        bomb.Validate();
        dash.Validate();
        bulletTime.Validate();
        impactFrame.Validate();
        ghostTrail.Validate();
        healMissingHealth.Validate();
        dropAttraction.Validate();
        deathExplosion.Validate();
        projectileOrbitOverride.Validate();
        orbitalProjections.Validate();
        projectileBounceOnWalls.Validate();
        returningProjectiles.Validate();
        projectileSplit.Validate();
        laserBeam.Validate();
        switchWeapon.Validate();
        trailSpawn.Validate();
        elementalAreaTick.Validate();
        stackable.Validate();
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PowerUpModuleDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable identifier of this module definition.")]
    [SerializeField] private string moduleId;

    [Tooltip("Display name shown in module pickers.")]
    [SerializeField] private string displayName = "New Module";

    [Tooltip("Behavior implemented by this module.")]
    [SerializeField] private PowerUpModuleKind moduleKind;

    [Tooltip("Legacy serialized stage. Stage is now derived from module kind.")]
    [HideInInspector]
    [SerializeField] private PowerUpModuleStage defaultStage = PowerUpModuleStage.Execute;

    [Tooltip("Optional notes for this module.")]
    [SerializeField] private string notes;

    [Tooltip("Payload used by this module kind.")]
    [SerializeField] private PowerUpModuleData data = new PowerUpModuleData();
    #endregion

    #endregion

    #region Properties
    public string ModuleId
    {
        get
        {
            return moduleId;
        }
    }

    public string DisplayName
    {
        get
        {
            return displayName;
        }
    }

    public PowerUpModuleKind ModuleKind
    {
        get
        {
            return moduleKind;
        }
    }

    public PowerUpModuleStage DefaultStage
    {
        get
        {
            return PowerUpModuleKindUtility.ResolveStageFromKind(moduleKind);
        }
    }

    public string Notes
    {
        get
        {
            return notes;
        }
    }

    public PowerUpModuleData Data
    {
        get
        {
            return data;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(string moduleIdValue,
                          string displayNameValue,
                          PowerUpModuleKind moduleKindValue,
                          PowerUpModuleStage defaultStageValue,
                          string notesValue,
                          PowerUpModuleData dataValue)
    {
        moduleId = moduleIdValue;
        displayName = displayNameValue;
        moduleKind = moduleKindValue;
        defaultStage = PowerUpModuleKindUtility.ResolveStageFromKind(moduleKindValue);
        notes = notesValue;
        data = dataValue;
    }

    public void SetModuleKind(PowerUpModuleKind moduleKindValue)
    {
        moduleKind = moduleKindValue;
        defaultStage = PowerUpModuleKindUtility.ResolveStageFromKind(moduleKindValue);
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            moduleId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = "New Module";

        if (data == null)
            data = new PowerUpModuleData();

        defaultStage = PowerUpModuleKindUtility.ResolveStageFromKind(moduleKind);
        data.Validate();
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PowerUpModuleBinding
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable identifier of this module binding instance.")]
    [HideInInspector]
    [SerializeField] private string bindingId;

    [Tooltip("Referenced ModuleId inside Modules Management.")]
    [SerializeField] private string moduleId;

    [Tooltip("Legacy serialized stage. Stage is now derived from module kind.")]
    [HideInInspector]
    [SerializeField] private PowerUpModuleStage stage = PowerUpModuleStage.Execute;

    [Tooltip("When disabled, this module binding is ignored at bake/runtime compile.")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("When enabled, this binding uses the override payload instead of module defaults.")]
    [SerializeField] private bool useOverridePayload;

    [Tooltip("Override payload used when Use Override Payload is enabled.")]
    [SerializeField] private PowerUpModuleData overridePayload = new PowerUpModuleData();
    #endregion

    #endregion

    #region Properties
    public string ModuleId
    {
        get
        {
            return moduleId;
        }
    }

    public string BindingId
    {
        get
        {
            return bindingId;
        }
    }

    public PowerUpModuleStage Stage
    {
        get
        {
            return stage;
        }
    }

    public bool IsEnabled
    {
        get
        {
            return isEnabled;
        }
    }

    public bool UseOverridePayload
    {
        get
        {
            return useOverridePayload;
        }
    }

    public PowerUpModuleData OverridePayload
    {
        get
        {
            return overridePayload;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(string moduleIdValue, bool isEnabledValue)
    {
        if (string.IsNullOrWhiteSpace(bindingId))
            bindingId = Guid.NewGuid().ToString("N");

        moduleId = moduleIdValue;
        stage = PowerUpModuleStage.Execute;
        isEnabled = isEnabledValue;
    }

    public void Configure(string moduleIdValue, PowerUpModuleStage stageValue, bool isEnabledValue)
    {
        if (string.IsNullOrWhiteSpace(bindingId))
            bindingId = Guid.NewGuid().ToString("N");

        moduleId = moduleIdValue;
        stage = stageValue;
        isEnabled = isEnabledValue;
    }

    public void ConfigureOverride(bool useOverridePayloadValue, PowerUpModuleData overridePayloadValue)
    {
        useOverridePayload = useOverridePayloadValue;
        overridePayload = overridePayloadValue;
    }

    public void RegenerateBindingId()
    {
        bindingId = Guid.NewGuid().ToString("N");
    }
    #endregion

    #region Helpers
    public PowerUpModuleData ResolvePayload(PowerUpModuleDefinition moduleDefinition)
    {
        if (useOverridePayload && overridePayload != null)
            return overridePayload;

        if (moduleDefinition != null && moduleDefinition.Data != null)
            return moduleDefinition.Data;

        return null;
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(bindingId))
            bindingId = Guid.NewGuid().ToString("N");

        if (overridePayload == null)
            overridePayload = new PowerUpModuleData();

        overridePayload.Validate();
    }
    #endregion

    #endregion
}

#endregion
