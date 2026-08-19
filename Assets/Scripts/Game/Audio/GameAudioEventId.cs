/// <summary>
/// Stable identifiers used by ECS gameplay systems to request authored game audio events.
/// </summary>
public enum GameAudioEventId : byte
{
    None = 0,
    PlayerShootProjectile = 1,
    EnemyShootProjectile = 2,
    PlayerShootLaserContinuous = 3,
    PlayerShootLaserTick = 4,
    PlayerShootCannon = 5,
    PlayerHealthDamage = 6,
    PlayerShieldDamage = 7,
    PlayerHealthRecharge = 8,
    PlayerShieldRecharge = 9,
    PlayerLevelUp = 10,
    PlayerLevelUpMilestone = 11,
    ActiveEnergyFull = 12,
    ActiveCharge = 13,
    ActiveRelease = 14,
    ActiveDash = 15,
    ActiveThrow = 16,
    PlayerDeath = 17,
    PlayerSpawn = 18,
    PlayerVictory = 19,
    BulletImpactEnemy = 20,
    BulletImpactPlayer = 21,
    PlayerLaserImpact = 22,
    ExplosionBomb = 23,
    ExplosionPassive = 24,
    ExplosionEnemy = 25,
    PlayerDeathJingle = 26,
    RoomRewardPortalAnimation = 27
}
