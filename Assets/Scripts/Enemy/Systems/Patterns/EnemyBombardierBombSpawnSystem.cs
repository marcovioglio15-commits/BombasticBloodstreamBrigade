using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Instantiates enemy Bombardier bombs from launch requests and initializes parabolic simulation state.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyBombardierRequestSystem))]
[UpdateBefore(typeof(EnemyBombardierBombSystem))]
public partial struct EnemyBombardierBombSpawnSystem : ISystem
{
    #region Constants
    private static readonly float3 UpAxis = new float3(0f, 1f, 0f);
    private static readonly float3 ForwardAxis = new float3(0f, 0f, 1f);
    private const float MinimumFlightDurationSeconds = 0.05f;
    private const float MinimumGravity = 0.01f;
    private const float MinimumBombScale = 0.0001f;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares Bombardier launch request buffers as update requirements.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyBombardierLaunchRequest>();
    }

    /// <summary>
    /// Converts pending Bombardier launch requests into bomb entities.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

        foreach ((DynamicBuffer<EnemyBombardierLaunchRequest> launchRequests,
                  RefRO<EnemyBombardierBombPrefab> bombPrefab,
                  Entity ownerEntity) in SystemAPI.Query<DynamicBuffer<EnemyBombardierLaunchRequest>,
                                                         RefRO<EnemyBombardierBombPrefab>>()
                                                 .WithEntityAccess())
        {
            if (launchRequests.Length <= 0)
                continue;

            Entity prefabEntity = bombPrefab.ValueRO.PrefabEntity;

            if (prefabEntity == Entity.Null || !entityManager.Exists(prefabEntity))
            {
                launchRequests.Clear();
                continue;
            }

            for (int requestIndex = 0; requestIndex < launchRequests.Length; requestIndex++)
                SpawnBomb(entityManager,
                          commandBuffer,
                          in bombPrefab.ValueRO,
                          ownerEntity,
                          launchRequests[requestIndex],
                          elapsedTime);

            launchRequests.Clear();
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Spawn
    /// <summary>
    /// Instantiates one Bombardier bomb and applies all runtime state needed by simulation and warning presentation.
    /// </summary>
    /// <param name="entityManager">Entity manager used for prefab component checks.</param>
    /// <param name="commandBuffer">Command buffer receiving structural changes.</param>
    /// <param name="bombPrefab">Bomb prefab binding and optional explosion VFX binding.</param>
    /// <param name="ownerEntity">Enemy owner entity.</param>
    /// <param name="request">Launch request to convert.</param>
    /// <param name="elapsedTime">Current elapsed world time.</param>
    private static void SpawnBomb(EntityManager entityManager,
                                  EntityCommandBuffer commandBuffer,
                                  in EnemyBombardierBombPrefab bombPrefab,
                                  Entity ownerEntity,
                                  in EnemyBombardierLaunchRequest request,
                                  float elapsedTime)
    {
        Entity prefabEntity = bombPrefab.PrefabEntity;
        TrajectorySolution trajectory = ResolveTrajectory(in request);
        Entity bombEntity = commandBuffer.Instantiate(prefabEntity);
        LocalTransform bombTransform = BuildTransform(entityManager,
                                                      prefabEntity,
                                                      in request,
                                                      in trajectory);
        EnemyBombardierBomb bomb = new EnemyBombardierBomb
        {
            OwnerEntity = ownerEntity,
            ExplosionVfxPrefabEntity = bombPrefab.ExplosionVfxPrefabEntity,
            ExplosionVfxPrefab = bombPrefab.ExplosionVfxPrefab,
            LaunchPosition = request.LaunchPosition,
            LandingPosition = request.LandingPosition,
            Velocity = trajectory.Velocity,
            Gravity = trajectory.Gravity,
            ElapsedSeconds = 0f,
            FlightDurationSeconds = trajectory.FlightDurationSeconds,
            ImpactExplosionDelaySeconds = math.max(0f, request.ImpactExplosionDelaySeconds),
            ExplosionDelayElapsedSeconds = 0f,
            Damage = math.max(0f, request.Damage),
            DamageRadius = math.max(0f, request.DamageRadius),
            ScaleExplosionVfxToDamageRadius = bombPrefab.ScaleExplosionVfxToDamageRadius,
            ExplosionVfxScaleMultiplier = math.max(0.01f, bombPrefab.ExplosionVfxScaleMultiplier),
            HasImpacted = 0,
            HasExploded = 0,
            WarningFadeOutEndTime = elapsedTime + trajectory.FlightDurationSeconds + math.max(0f, request.WarningFadeOutSeconds)
        };

        if (entityManager.HasComponent<LocalTransform>(prefabEntity))
            commandBuffer.SetComponent(bombEntity, bombTransform);
        else
            commandBuffer.AddComponent(bombEntity, bombTransform);

        if (entityManager.HasComponent<EnemyBombardierBomb>(prefabEntity))
            commandBuffer.SetComponent(bombEntity, bomb);
        else
            commandBuffer.AddComponent(bombEntity, bomb);

        if (request.EnableLandingWarning == 0)
            return;

        EnemyBombardierWarningState warningState = BuildWarningState(in request,
                                                                     trajectory.FlightDurationSeconds,
                                                                     elapsedTime);

        if (entityManager.HasComponent<EnemyBombardierWarningState>(prefabEntity))
            commandBuffer.SetComponent(bombEntity, warningState);
        else
            commandBuffer.AddComponent(bombEntity, warningState);

        commandBuffer.SetComponentEnabled<EnemyBombardierWarningState>(bombEntity, true);
    }

    /// <summary>
    /// Builds the initial bomb transform from prefab scale and launch direction.
    /// </summary>
    /// <param name="entityManager">Entity manager used for prefab transform reads.</param>
    /// <param name="prefabEntity">Bomb prefab entity.</param>
    /// <param name="request">Launch request being spawned.</param>
    /// <param name="trajectory">Resolved trajectory solution.</param>
    /// <returns>Initial bomb transform.</returns>
    private static LocalTransform BuildTransform(EntityManager entityManager,
                                                 Entity prefabEntity,
                                                 in EnemyBombardierLaunchRequest request,
                                                 in TrajectorySolution trajectory)
    {
        float baseScale = 1f;

        if (entityManager.HasComponent<LocalTransform>(prefabEntity))
            baseScale = math.max(MinimumBombScale, entityManager.GetComponentData<LocalTransform>(prefabEntity).Scale);

        float3 forward = trajectory.Velocity;
        forward.y = 0f;

        if (math.lengthsq(forward) <= 1e-6f)
            forward = ForwardAxis;

        return LocalTransform.FromPositionRotationScale(request.LaunchPosition,
                                                       quaternion.LookRotationSafe(math.normalizesafe(forward, ForwardAxis), UpAxis),
                                                       baseScale * math.max(0.01f, request.BombScaleMultiplier));
    }

    /// <summary>
    /// Builds one landing warning state from the launch request and predicted impact time.
    /// </summary>
    /// <param name="request">Launch request containing warning settings.</param>
    /// <param name="flightDurationSeconds">Resolved flight duration.</param>
    /// <param name="elapsedTime">Current elapsed world time.</param>
    /// <returns>Bombardier warning state.</returns>
    private static EnemyBombardierWarningState BuildWarningState(in EnemyBombardierLaunchRequest request,
                                                                 float flightDurationSeconds,
                                                                 float elapsedTime)
    {
        float leadTimeSeconds = math.max(0f, request.WarningLeadTimeSeconds);
        float impactTime = elapsedTime + math.max(0f, flightDurationSeconds);
        float warningStartTime = math.max(elapsedTime, impactTime - leadTimeSeconds);
        float fadeOutSeconds = math.max(0f, request.WarningFadeOutSeconds);

        return new EnemyBombardierWarningState
        {
            WorldPosition = request.LandingPosition,
            WarningStartTime = warningStartTime,
            ImpactTime = impactTime,
            FadeOutEndTime = impactTime + fadeOutSeconds,
            LeadTimeSeconds = math.max(0f, impactTime - warningStartTime),
            FadeOutSeconds = fadeOutSeconds,
            Radius = math.max(0f, request.WarningRadius),
            RingWidth = math.max(0f, request.WarningRingWidth),
            HeightOffset = request.WarningHeightOffset,
            MaximumAlpha = math.saturate(request.WarningMaximumAlpha),
            Color = request.WarningColor
        };
    }
    #endregion

    #region Trajectory
    /// <summary>
    /// Solves initial velocity, gravity and duration for one Bombardier trajectory request.
    /// </summary>
    /// <param name="request">Launch request containing trajectory settings.</param>
    /// <returns>Resolved trajectory solution.</returns>
    private static TrajectorySolution ResolveTrajectory(in EnemyBombardierLaunchRequest request)
    {
        float gravity = math.max(MinimumGravity, request.Gravity);

        switch (request.TrajectoryMode)
        {
            case EnemyBombardierTrajectoryMode.FixedApexHeight:
                return ResolveApexTrajectory(in request, gravity);

            default:
                return ResolveFixedTimeTrajectory(in request, gravity);
        }
    }

    /// <summary>
    /// Solves trajectory velocity from authored fixed flight time and gravity.
    /// </summary>
    /// <param name="request">Launch request containing launch and landing positions.</param>
    /// <param name="gravity">Resolved positive gravity.</param>
    /// <returns>Resolved trajectory solution.</returns>
    private static TrajectorySolution ResolveFixedTimeTrajectory(in EnemyBombardierLaunchRequest request, float gravity)
    {
        float flightDurationSeconds = math.max(MinimumFlightDurationSeconds, request.FlightDurationSeconds);
        float3 displacement = request.LandingPosition - request.LaunchPosition;
        float3 velocity = new float3(displacement.x / flightDurationSeconds,
                                     (displacement.y + 0.5f * gravity * flightDurationSeconds * flightDurationSeconds) / flightDurationSeconds,
                                     displacement.z / flightDurationSeconds);

        return new TrajectorySolution
        {
            Velocity = velocity,
            Gravity = gravity,
            FlightDurationSeconds = flightDurationSeconds
        };
    }

    /// <summary>
    /// Solves trajectory velocity from authored apex height and gravity.
    /// </summary>
    /// <param name="request">Launch request containing launch and landing positions.</param>
    /// <param name="gravity">Resolved positive gravity.</param>
    /// <returns>Resolved trajectory solution.</returns>
    private static TrajectorySolution ResolveApexTrajectory(in EnemyBombardierLaunchRequest request, float gravity)
    {
        float apexWorldY = math.max(request.LaunchPosition.y, request.LandingPosition.y) + math.max(0.05f, request.ApexHeight);
        float launchApexDelta = math.max(0.05f, apexWorldY - request.LaunchPosition.y);
        float landingApexDelta = math.max(0.05f, apexWorldY - request.LandingPosition.y);
        float timeUp = math.sqrt(2f * launchApexDelta / gravity);
        float timeDown = math.sqrt(2f * landingApexDelta / gravity);
        float flightDurationSeconds = math.max(MinimumFlightDurationSeconds, timeUp + timeDown);
        float3 displacement = request.LandingPosition - request.LaunchPosition;
        float3 velocity = new float3(displacement.x / flightDurationSeconds,
                                     gravity * timeUp,
                                     displacement.z / flightDurationSeconds);

        return new TrajectorySolution
        {
            Velocity = velocity,
            Gravity = gravity,
            FlightDurationSeconds = flightDurationSeconds
        };
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Stores resolved trajectory values for a spawned bomb.
    /// </summary>
    private struct TrajectorySolution
    {
        public float3 Velocity;
        public float Gravity;
        public float FlightDurationSeconds;
    }
    #endregion
}
