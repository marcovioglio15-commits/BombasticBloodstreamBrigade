using System;

#region Enums
public enum PowerUpModuleStage
{
    Trigger = 0,
    PreGate = 1,
    Gate = 2,
    StateEnter = 3,
    Execute = 4,
    StateExit = 5,
    PostExecute = 6,
    Hook = 7
}

public enum PowerUpModuleKind
{
    TriggerPress = 0,
    TriggerRelease = 1,
    TriggerHoldCharge = 2,
    TriggerEvent = 3,
    GateResource = 4,
    StateSuppressShooting = 5,
    ProjectilesPatternCone = 6,
    CharacterTuning = 7,
    SpawnObject = 8,
    Dash = 9,
    TimeDilationEnemies = 10,
    Heal = 11,
    SpawnTrailSegment = 12,
    AreaTickApplyElement = 13,
    DeathExplosion = 14,
    OrbitalProjectiles = 15,
    BouncingProjectiles = 16,
    ProjectileSplit = 17,
    Stackable = 18,
    LaserBeam = 19,
    OrbitalProjections = 20
}

public enum PowerUpTriggerEventType
{
    OnEnemyKilled = 0,
    OnPlayerDamaged = 1,
    OnPlayerMovementStep = 2,
    OnProjectileSpawned = 3,
    OnProjectileWallHit = 4,
    OnProjectileDespawned = 5
}

public enum ProjectilePenetrationMode
{
    None = 0,
    FixedHits = 1,
    Infinite = 2,
    DamageBased = 3
}

public static class PowerUpModuleKindUtility
{
    #region Methods

    #region Public API
    public static PowerUpModuleStage ResolveStageFromKind(PowerUpModuleKind moduleKind)
    {
        switch (moduleKind)
        {
            case PowerUpModuleKind.TriggerPress:
            case PowerUpModuleKind.TriggerRelease:
            case PowerUpModuleKind.TriggerHoldCharge:
                return PowerUpModuleStage.Trigger;
            case PowerUpModuleKind.TriggerEvent:
                return PowerUpModuleStage.Hook;
            case PowerUpModuleKind.GateResource:
                return PowerUpModuleStage.Gate;
            case PowerUpModuleKind.StateSuppressShooting:
                return PowerUpModuleStage.StateEnter;
            case PowerUpModuleKind.ProjectilesPatternCone:
            case PowerUpModuleKind.SpawnObject:
            case PowerUpModuleKind.Dash:
            case PowerUpModuleKind.TimeDilationEnemies:
            case PowerUpModuleKind.Heal:
                return PowerUpModuleStage.Execute;
            case PowerUpModuleKind.CharacterTuning:
            case PowerUpModuleKind.Stackable:
                return PowerUpModuleStage.PostExecute;
            case PowerUpModuleKind.SpawnTrailSegment:
            case PowerUpModuleKind.AreaTickApplyElement:
            case PowerUpModuleKind.DeathExplosion:
            case PowerUpModuleKind.OrbitalProjectiles:
            case PowerUpModuleKind.OrbitalProjections:
            case PowerUpModuleKind.BouncingProjectiles:
            case PowerUpModuleKind.ProjectileSplit:
            case PowerUpModuleKind.LaserBeam:
                return PowerUpModuleStage.Hook;
            default:
                return PowerUpModuleStage.Hook;
        }
    }
    #endregion

    #endregion
}
#endregion
