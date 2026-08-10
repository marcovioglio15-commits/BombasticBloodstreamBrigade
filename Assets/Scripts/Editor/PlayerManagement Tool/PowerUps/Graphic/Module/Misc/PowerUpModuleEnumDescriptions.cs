using System.Collections.Generic;

public static class PowerUpModuleEnumDescriptions
{
    #region Fields
    private static readonly List<PowerUpModuleStage> StageValues = new List<PowerUpModuleStage>
    {
        PowerUpModuleStage.Trigger,
        PowerUpModuleStage.PreGate,
        PowerUpModuleStage.Gate,
        PowerUpModuleStage.StateEnter,
        PowerUpModuleStage.Execute,
        PowerUpModuleStage.StateExit,
        PowerUpModuleStage.PostExecute,
        PowerUpModuleStage.Hook
    };

    private static readonly List<PowerUpModuleKind> ModuleKindValues = new List<PowerUpModuleKind>
    {
        PowerUpModuleKind.TriggerPress,
        PowerUpModuleKind.TriggerRelease,
        PowerUpModuleKind.TriggerHoldCharge,
        PowerUpModuleKind.TriggerEvent,
        PowerUpModuleKind.GateResource,
        PowerUpModuleKind.StateSuppressShooting,
        PowerUpModuleKind.ProjectilesPatternCone,
        PowerUpModuleKind.CharacterTuning,
        PowerUpModuleKind.SpawnObject,
        PowerUpModuleKind.Dash,
        PowerUpModuleKind.TimeDilationEnemies,
        PowerUpModuleKind.ImpactFrame,
        PowerUpModuleKind.GhostTrail,
        PowerUpModuleKind.Heal,
        PowerUpModuleKind.SpawnTrailSegment,
        PowerUpModuleKind.AreaTickApplyElement,
        PowerUpModuleKind.DeathExplosion,
        PowerUpModuleKind.OrbitalProjectiles,
        PowerUpModuleKind.OrbitalProjections,
        PowerUpModuleKind.BouncingProjectiles,
        PowerUpModuleKind.ProjectileSplit,
        PowerUpModuleKind.Stackable,
        PowerUpModuleKind.LaserBeam,
        PowerUpModuleKind.SwitchWeapon,
        PowerUpModuleKind.AttractDrops
    };
    #endregion

    #region Properties
    public static IReadOnlyList<PowerUpModuleStage> StageOptions
    {
        get
        {
            return StageValues;
        }
    }

    public static IReadOnlyList<PowerUpModuleKind> ModuleKindOptions
    {
        get
        {
            return ModuleKindValues;
        }
    }
    #endregion

    #region Methods

    #region Public API
    public static string FormatStageOption(PowerUpModuleStage stage)
    {
        return stage.ToString();
    }

    public static string FormatModuleKindOption(PowerUpModuleKind moduleKind)
    {
        return moduleKind.ToString();
    }

    public static string GetStageDescription(PowerUpModuleStage stage)
    {
        switch (stage)
        {
            case PowerUpModuleStage.Trigger:
                return "Input trigger stage.";
            case PowerUpModuleStage.PreGate:
                return "Pre-gate stage used before resource checks.";
            case PowerUpModuleStage.Gate:
                return "Resource and validation stage.";
            case PowerUpModuleStage.StateEnter:
                return "State enter stage for temporary states.";
            case PowerUpModuleStage.Execute:
                return "Direct execution gameplay effects.";
            case PowerUpModuleStage.StateExit:
                return "State exit stage.";
            case PowerUpModuleStage.PostExecute:
                return "Post execution stage.";
            case PowerUpModuleStage.Hook:
                return "Event-driven hook stage.";
            default:
                return "No description available.";
        }
    }

    public static string GetModuleKindDescription(PowerUpModuleKind moduleKind)
    {
        switch (moduleKind)
        {
            case PowerUpModuleKind.TriggerPress:
                return "Triggers once when input is pressed.";
            case PowerUpModuleKind.TriggerRelease:
                return "Triggers once when input is released.";
            case PowerUpModuleKind.TriggerHoldCharge:
                return "Accumulates charge while input is held.";
            case PowerUpModuleKind.TriggerEvent:
                return "Triggers execution from configured runtime event.";
            case PowerUpModuleKind.GateResource:
                return "Applies activation resource, recharge and cooldown checks.";
            case PowerUpModuleKind.StateSuppressShooting:
                return "Suppresses base shooting and can interrupt opposite slot.";
            case PowerUpModuleKind.ProjectilesPatternCone:
                return "Sets projectile cone pattern emission.";
            case PowerUpModuleKind.CharacterTuning:
                return "Applies scalable-stat assignment formulas on acquisition for standard actives, while owned for passives, temporarily while charging with Trigger Hold Charge, or only while active with toggleable Resource Gate.";
            case PowerUpModuleKind.SpawnObject:
                return "Spawns configured object payload, optionally with damage.";
            case PowerUpModuleKind.Dash:
                return "Executes dash movement payload.";
            case PowerUpModuleKind.TimeDilationEnemies:
                return "Applies bullet-time slow effect to enemies.";
            case PowerUpModuleKind.ImpactFrame:
                return "Applies a global time slowdown and fullscreen impact filter when an active power-up activation succeeds.";
            case PowerUpModuleKind.GhostTrail:
                return "Emits pooled residual images after activation, with optional orbital, attachment, camera and fullscreen feedback.";
            case PowerUpModuleKind.Heal:
                return "Applies instant or over-time heal payload.";
            case PowerUpModuleKind.SpawnTrailSegment:
                return "Spawns and maintains elemental trail segments.";
            case PowerUpModuleKind.AreaTickApplyElement:
                return "Applies elemental stacks in area ticks.";
            case PowerUpModuleKind.DeathExplosion:
                return "Triggers damage explosion payload.";
            case PowerUpModuleKind.OrbitalProjectiles:
                return "Overrides projectile trajectories to orbital behavior.";
            case PowerUpModuleKind.OrbitalProjections:
                return "Spawns persistent or timed objects orbiting the player with contact damage and interception effects.";
            case PowerUpModuleKind.BouncingProjectiles:
                return "Adds wall bounce logic to projectiles.";
            case PowerUpModuleKind.ProjectileSplit:
                return "Splits projectiles on configured split trigger.";
            case PowerUpModuleKind.Stackable:
                return "Allows a Character Tuning power-up to be rolled multiple times up to a configured cap.";
            case PowerUpModuleKind.LaserBeam:
                return "Replaces base projectile emission with one or more continuous liquid-laser lanes derived from current shooting and projectile passives.";
            case PowerUpModuleKind.SwitchWeapon:
                return "Keeps Base Gun visible and replaces the Player Visual Preset optional attachment with the mountable mesh identified by a Weapon Id while the owning passive or toggleable active power-up is in effect.";
            case PowerUpModuleKind.AttractDrops:
                return "Attracts enemy drops inside a player-centered radius. Passives apply continuously, standard actives emit one request per activation, and toggleable Resource Gates apply while active.";
            default:
                return "No description available.";
        }
    }

    public static PowerUpModuleStage GetRecommendedStage(PowerUpModuleKind moduleKind)
    {
        return PowerUpModuleKindUtility.ResolveStageFromKind(moduleKind);
    }

    public static bool IsStageCoherent(PowerUpModuleKind moduleKind, PowerUpModuleStage stage)
    {
        return stage == GetRecommendedStage(moduleKind);
    }

    public static bool TryGetPayloadProperty(PowerUpModuleKind moduleKind, out string relativePropertyPath, out string payloadLabel)
    {
        switch (moduleKind)
        {
            case PowerUpModuleKind.TriggerHoldCharge:
                relativePropertyPath = "holdCharge";
                payloadLabel = "Hold Charge Payload";
                return true;
            case PowerUpModuleKind.TriggerEvent:
                relativePropertyPath = "triggerEvent";
                payloadLabel = "Event Trigger Payload";
                return true;
            case PowerUpModuleKind.GateResource:
                relativePropertyPath = "resourceGate";
                payloadLabel = "Resource Gate Payload";
                return true;
            case PowerUpModuleKind.StateSuppressShooting:
                relativePropertyPath = "suppressShooting";
                payloadLabel = "Suppress Shooting Payload";
                return true;
            case PowerUpModuleKind.ProjectilesPatternCone:
                relativePropertyPath = "projectilePatternCone";
                payloadLabel = "Projectile Pattern Payload";
                return true;
            case PowerUpModuleKind.CharacterTuning:
                relativePropertyPath = "characterTuning";
                payloadLabel = "Character Tuning Payload";
                return true;
            case PowerUpModuleKind.SpawnObject:
                relativePropertyPath = "bomb";
                payloadLabel = "Spawn Object Payload";
                return true;
            case PowerUpModuleKind.Dash:
                relativePropertyPath = "dash";
                payloadLabel = "Dash Payload";
                return true;
            case PowerUpModuleKind.Heal:
                relativePropertyPath = "healMissingHealth";
                payloadLabel = "Heal Payload";
                return true;
            case PowerUpModuleKind.TimeDilationEnemies:
                relativePropertyPath = "bulletTime";
                payloadLabel = "Time Dilation Payload";
                return true;
            case PowerUpModuleKind.ImpactFrame:
                relativePropertyPath = "impactFrame";
                payloadLabel = "Impact Frame Payload";
                return true;
            case PowerUpModuleKind.GhostTrail:
                relativePropertyPath = "ghostTrail";
                payloadLabel = "Ghost Trail Payload";
                return true;
            case PowerUpModuleKind.SpawnTrailSegment:
                relativePropertyPath = "trailSpawn";
                payloadLabel = "Trail Spawn Payload";
                return true;
            case PowerUpModuleKind.AreaTickApplyElement:
                relativePropertyPath = "elementalAreaTick";
                payloadLabel = "Elemental Area Tick Payload";
                return true;
            case PowerUpModuleKind.DeathExplosion:
                relativePropertyPath = "deathExplosion";
                payloadLabel = "Explosion Payload";
                return true;
            case PowerUpModuleKind.OrbitalProjectiles:
                relativePropertyPath = "projectileOrbitOverride";
                payloadLabel = "Orbital Projectiles Payload";
                return true;
            case PowerUpModuleKind.OrbitalProjections:
                relativePropertyPath = "orbitalProjections";
                payloadLabel = "Orbital Projections Payload";
                return true;
            case PowerUpModuleKind.BouncingProjectiles:
                relativePropertyPath = "projectileBounceOnWalls";
                payloadLabel = "Bouncing Projectiles Payload";
                return true;
            case PowerUpModuleKind.ProjectileSplit:
                relativePropertyPath = "projectileSplit";
                payloadLabel = "Projectile Split Payload";
                return true;
            case PowerUpModuleKind.Stackable:
                relativePropertyPath = "stackable";
                payloadLabel = "Stackable Payload";
                return true;
            case PowerUpModuleKind.LaserBeam:
                relativePropertyPath = "laserBeam";
                payloadLabel = "Laser Beam Payload";
                return true;
            case PowerUpModuleKind.SwitchWeapon:
                relativePropertyPath = "switchWeapon";
                payloadLabel = "Switch Weapon Payload";
                return true;
            case PowerUpModuleKind.AttractDrops:
                relativePropertyPath = "dropAttraction";
                payloadLabel = "Drop Attraction Payload";
                return true;
            default:
                relativePropertyPath = string.Empty;
                payloadLabel = string.Empty;
                return false;
        }
    }
    #endregion

    #endregion
}
