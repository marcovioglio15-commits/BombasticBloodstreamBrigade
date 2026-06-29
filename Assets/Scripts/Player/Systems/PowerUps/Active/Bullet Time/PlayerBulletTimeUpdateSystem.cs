using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Updates Bullet Time duration and writes global enemy time scale.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpActivationSystem))]
public partial struct PlayerBulletTimeUpdateSystem : ISystem
{
    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerBulletTimeState>();

        EntityQuery timeScaleQuery = state.GetEntityQuery(ComponentType.ReadOnly<EnemyGlobalTimeScale>());

        if (timeScaleQuery.IsEmptyIgnoreFilter)
        {
            Entity singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(singletonEntity, new EnemyGlobalTimeScale
            {
                Scale = 1f,
                PlayerProjectileScale = 1f
            });
        }
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        float maxSlowPercent = 0f;
        float maxPlayerProjectileSlowPercent = 0f;

        foreach (RefRW<PlayerBulletTimeState> bulletTimeState in SystemAPI.Query<RefRW<PlayerBulletTimeState>>())
        {
            float slowPercent = PlayerBulletTimeRuntimeUtility.Tick(ref bulletTimeState.ValueRW,
                                                                    deltaTime,
                                                                    out float playerProjectileSlowPercent);

            if (slowPercent > maxSlowPercent)
                maxSlowPercent = slowPercent;

            if (playerProjectileSlowPercent > maxPlayerProjectileSlowPercent)
                maxPlayerProjectileSlowPercent = playerProjectileSlowPercent;
        }

        float enemyTimeScale = math.saturate(1f - (maxSlowPercent * 0.01f));
        float playerProjectileTimeScale = math.saturate(1f - (maxPlayerProjectileSlowPercent * 0.01f));

        if (SystemAPI.TryGetSingletonRW<EnemyGlobalTimeScale>(out RefRW<EnemyGlobalTimeScale> enemyGlobalTimeScale))
        {
            enemyGlobalTimeScale.ValueRW.Scale = enemyTimeScale;
            enemyGlobalTimeScale.ValueRW.PlayerProjectileScale = playerProjectileTimeScale;
            return;
        }

        Entity singletonEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(singletonEntity, new EnemyGlobalTimeScale
        {
            Scale = enemyTimeScale,
            PlayerProjectileScale = playerProjectileTimeScale
        });
    }
    #endregion

    #endregion
}
