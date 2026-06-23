using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Drives enemy face flipbook state playback from damage events and offensive engagement timing windows.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(EnemyDamageFlashPresentationSystem))]
public partial struct EnemyFaceFlipbookPresentationSystem : ISystem
{
    #region Constants
    private const float DamageLifetimeEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the face flipbook component set required by the presentation pass.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyFaceFlipbookConfig>();
        state.RequireForUpdate<EnemyFaceFlipbookStateData>();
        state.RequireForUpdate<EnemyRuntimeState>();
        state.RequireForUpdate<EnemyOffensiveEngagementConfigElement>();
    }

    /// <summary>
    /// Updates temporary face state timers and applies changed material overrides to enemy renderers.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        float deltaTime = SystemAPI.Time.DeltaTime;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        BufferLookup<EnemyOffensiveEngagementConfigElement> offensiveConfigLookup = SystemAPI.GetBufferLookup<EnemyOffensiveEngagementConfigElement>(true);
        BufferLookup<EnemyShooterRuntimeElement> shooterRuntimeLookup = SystemAPI.GetBufferLookup<EnemyShooterRuntimeElement>(true);
        BufferLookup<EnemyBombardierRuntimeElement> bombardierRuntimeLookup = SystemAPI.GetBufferLookup<EnemyBombardierRuntimeElement>(true);
        BufferLookup<EnemyBossPatternSlotRuntimeElement> bossSlotRuntimeLookup = SystemAPI.GetBufferLookup<EnemyBossPatternSlotRuntimeElement>(true);

        foreach ((RefRO<EnemyFaceFlipbookConfig> faceConfig,
                  RefRW<EnemyFaceFlipbookStateData> faceState,
                  RefRO<EnemyRuntimeState> enemyRuntime,
                  RefRO<EnemyPatternConfig> patternConfig,
                  RefRO<EnemyPatternRuntimeState> patternRuntimeState,
                  Entity enemyEntity)
                 in SystemAPI.Query<RefRO<EnemyFaceFlipbookConfig>,
                                    RefRW<EnemyFaceFlipbookStateData>,
                                    RefRO<EnemyRuntimeState>,
                                    RefRO<EnemyPatternConfig>,
                                    RefRO<EnemyPatternRuntimeState>>()
                             .WithAll<EnemyActive>()
                             .WithEntityAccess())
        {
            if (!offensiveConfigLookup.HasBuffer(enemyEntity) ||
                !shooterRuntimeLookup.HasBuffer(enemyEntity) ||
                !bombardierRuntimeLookup.HasBuffer(enemyEntity))
            {
                continue;
            }

            EnemyFaceFlipbookConfig config = faceConfig.ValueRO;
            EnemyFaceFlipbookStateData currentState = faceState.ValueRO;

            AdvanceTimers(ref currentState, deltaTime);
            bool forceApplyFace = TriggerDamageFace(ref currentState, in config, in enemyRuntime.ValueRO, elapsedTime);

            bool hasBossSlotRuntimes = bossSlotRuntimeLookup.HasBuffer(enemyEntity);
            DynamicBuffer<EnemyBossPatternSlotRuntimeElement> bossSlotRuntimes = hasBossSlotRuntimes
                ? bossSlotRuntimeLookup[enemyEntity]
                : default;
            bool engagementActive = EnemyOffensiveEngagementPresentationUtility.HasActiveEngagementWindow(offensiveConfigLookup[enemyEntity],
                                                                                                           shooterRuntimeLookup[enemyEntity],
                                                                                                           bombardierRuntimeLookup[enemyEntity],
                                                                                                           hasBossSlotRuntimes,
                                                                                                           bossSlotRuntimes,
                                                                                                           in patternConfig.ValueRO,
                                                                                                           in patternRuntimeState.ValueRO);
            forceApplyFace |= TriggerAttackFace(ref currentState, in config, engagementActive, elapsedTime);

            EnemyFaceFlipbookState selectedState = ResolveSelectedState(in config, in currentState);

            if (forceApplyFace || selectedState != currentState.CurrentState)
            {
                EnemyFaceFlipbookRenderUtility.ApplyGpuFace(entityManager,
                                                            enemyEntity,
                                                            in config,
                                                            selectedState,
                                                            ResolvePlayback(in config, in currentState, selectedState));
                currentState.CurrentState = selectedState;
            }

            faceState.ValueRW = currentState;
        }
    }
    #endregion

    #region State
    /// <summary>
    /// Advances independent temporary face timers.
    /// </summary>
    /// <param name="state">Mutable face state.</param>
    /// <param name="deltaTime">Presentation delta time.</param>
    private static void AdvanceTimers(ref EnemyFaceFlipbookStateData state, float deltaTime)
    {
        float safeDeltaTime = math.max(0f, deltaTime);
        state.AttackRemainingSeconds = math.max(0f, state.AttackRemainingSeconds - safeDeltaTime);
        state.DamageRemainingSeconds = math.max(0f, state.DamageRemainingSeconds - safeDeltaTime);
    }

    /// <summary>
    /// Starts the damage face when EnemyRuntimeState reports a newly observed damage lifetime.
    /// </summary>
    /// <param name="state">Mutable face state.</param>
    /// <param name="config">Baked face flipbook config.</param>
    /// <param name="enemyRuntime">Enemy runtime state carrying damage timing.</param>
    /// <param name="elapsedTime">Current world elapsed time used to restart shader playback.</param>
    /// <returns>True when the material playback should be rewritten this frame.</returns>
    private static bool TriggerDamageFace(ref EnemyFaceFlipbookStateData state,
                                          in EnemyFaceFlipbookConfig config,
                                          in EnemyRuntimeState enemyRuntime,
                                          float elapsedTime)
    {
        if (enemyRuntime.HasTakenDamage == 0)
            return false;

        bool isNewDamage = state.HasObservedDamage == 0 ||
                           math.abs(enemyRuntime.LastDamageLifetimeSeconds - state.LastObservedDamageLifetimeSeconds) > DamageLifetimeEpsilon;
        state.HasObservedDamage = 1;
        state.LastObservedDamageLifetimeSeconds = enemyRuntime.LastDamageLifetimeSeconds;

        if (!isNewDamage)
            return false;

        if (config.Enabled == 0 || config.DamageEnabled == 0)
            return false;

        state.DamageRemainingSeconds = math.max(0f, config.DamageDurationSeconds);
        state.DamagePlaybackPhaseSeconds = -elapsedTime;
        return true;
    }

    /// <summary>
    /// Starts the attack face when an offensive engagement window opens, independent from engagement feedback duration.
    /// </summary>
    /// <param name="state">Mutable face state.</param>
    /// <param name="config">Baked face flipbook config.</param>
    /// <param name="engagementActive">Whether any offensive engagement timing window is currently active.</param>
    /// <param name="elapsedTime">Current world elapsed time used to restart shader playback.</param>
    /// <returns>True when the material playback should be rewritten this frame.</returns>
    private static bool TriggerAttackFace(ref EnemyFaceFlipbookStateData state,
                                          in EnemyFaceFlipbookConfig config,
                                          bool engagementActive,
                                          float elapsedTime)
    {
        if (!engagementActive)
        {
            state.WasEngagementActive = 0;
            return false;
        }

        if (state.WasEngagementActive != 0)
            return false;

        state.WasEngagementActive = 1;

        if (config.Enabled == 0 || config.AttackEnabled == 0)
            return false;

        state.AttackRemainingSeconds = math.max(0f, config.AttackDurationSeconds);
        state.AttackPlaybackPhaseSeconds = -elapsedTime;
        return true;
    }

    /// <summary>
    /// Resolves the face state to display, with damage taking priority over attack and idle as fallback.
    /// </summary>
    /// <param name="config">Baked face flipbook config.</param>
    /// <param name="state">Current face state timers.</param>
    /// <returns>Face state selected for the current frame.</returns>
    private static EnemyFaceFlipbookState ResolveSelectedState(in EnemyFaceFlipbookConfig config,
                                                               in EnemyFaceFlipbookStateData state)
    {
        if (config.Enabled == 0 || config.IdleEnabled == 0)
            return EnemyFaceFlipbookState.Idle;

        if (config.DamageEnabled != 0 && state.DamageRemainingSeconds > 0f)
            return EnemyFaceFlipbookState.Damage;

        if (config.AttackEnabled != 0 && state.AttackRemainingSeconds > 0f)
            return EnemyFaceFlipbookState.Attack;

        return EnemyFaceFlipbookState.Idle;
    }

    /// <summary>
    /// Builds the shader playback vector matching the selected face state.
    /// </summary>
    /// <param name="config">Baked face flipbook config.</param>
    /// <param name="state">Current face state timers and playback phases.</param>
    /// <param name="selectedState">Selected face state for the current frame.</param>
    /// <returns>Playback vector containing frames per second, phase seconds, start frame and reserved data.</returns>
    private static float4 ResolvePlayback(in EnemyFaceFlipbookConfig config,
                                          in EnemyFaceFlipbookStateData state,
                                          EnemyFaceFlipbookState selectedState)
    {
        switch (selectedState)
        {
            case EnemyFaceFlipbookState.Damage:
                return new float4(config.DamageFramesPerSecond,
                                  state.DamagePlaybackPhaseSeconds,
                                  config.DamageStartFrame,
                                  0f);

            case EnemyFaceFlipbookState.Attack:
                return new float4(config.AttackFramesPerSecond,
                                  state.AttackPlaybackPhaseSeconds,
                                  config.AttackStartFrame,
                                  0f);

            default:
                return new float4(config.IdleFramesPerSecond,
                                  state.IdlePlaybackPhaseSeconds,
                                  config.IdleStartFrame,
                                  0f);
        }
    }
    #endregion

    #endregion
}
