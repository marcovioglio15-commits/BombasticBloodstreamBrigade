using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Updates enemy damage flash presentation and offensive engagement feedback for both managed companion renderers and ECS-rendered visuals.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(EnemyVisualDistanceCullingSystem))]
public partial struct EnemyDamageFlashPresentationSystem : ISystem
{
    #region Constants
    private const float BlendEpsilon = 0.0001f;
    private const float ColorEpsilon = 0.0001f;
    private const float CameraResolveRetryIntervalSeconds = 0.5f;
    #endregion

    #region Fields
    private static Transform cachedMainCameraTransform;
    private static float nextCameraResolveTime;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DamageFlashConfig>();
        state.RequireForUpdate<DamageFlashState>();
        state.RequireForUpdate<EnemyVisualConfig>();
        state.RequireForUpdate<EnemyVisualFlashPresentationState>();
        state.RequireForUpdate<EnemyOffensiveEngagementConfigElement>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        float deltaTime = SystemAPI.Time.DeltaTime;
        Transform cameraTransform = ResolveMainCameraTransform((float)SystemAPI.Time.ElapsedTime);
        BufferLookup<EnemyOffensiveEngagementConfigElement> offensiveConfigLookup = SystemAPI.GetBufferLookup<EnemyOffensiveEngagementConfigElement>(true);
        BufferLookup<EnemyShooterRuntimeElement> shooterRuntimeLookup = SystemAPI.GetBufferLookup<EnemyShooterRuntimeElement>(true);
        BufferLookup<EnemyBossPatternSlotRuntimeElement> bossSlotRuntimeLookup = SystemAPI.GetBufferLookup<EnemyBossPatternSlotRuntimeElement>(true);
        ComponentLookup<EnemyPatternConfig> patternConfigLookup = SystemAPI.GetComponentLookup<EnemyPatternConfig>(true);
        ComponentLookup<EnemyPatternRuntimeState> patternRuntimeStateLookup = SystemAPI.GetComponentLookup<EnemyPatternRuntimeState>(true);
        ComponentLookup<EnemyBossPatternChangeFeedbackConfig> patternChangeConfigLookup = SystemAPI.GetComponentLookup<EnemyBossPatternChangeFeedbackConfig>(true);
        ComponentLookup<EnemyBossPatternChangeFeedbackState> patternChangeStateLookup = SystemAPI.GetComponentLookup<EnemyBossPatternChangeFeedbackState>();

        foreach ((RefRO<DamageFlashConfig> damageFlashConfig,
                  RefRW<DamageFlashState> damageFlashState,
                  RefRW<EnemyVisualFlashPresentationState> visualFlashPresentationState,
                  RefRO<EnemyVisualConfig> visualConfig,
                  RefRO<EnemyVisualRuntimeState> visualRuntimeState,
                  RefRO<LocalTransform> enemyTransform,
                  Entity enemyEntity)
                 in SystemAPI.Query<RefRO<DamageFlashConfig>,
                                    RefRW<DamageFlashState>,
                                    RefRW<EnemyVisualFlashPresentationState>,
                                    RefRO<EnemyVisualConfig>,
                                    RefRO<EnemyVisualRuntimeState>,
                                    RefRO<LocalTransform>>()
                             .WithAll<EnemyActive>()
                             .WithEntityAccess())
        {
            DynamicBuffer<EnemyOffensiveEngagementConfigElement> offensiveEngagementConfigs = offensiveConfigLookup[enemyEntity];
            DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime = shooterRuntimeLookup[enemyEntity];
            bool hasBossSlotRuntimes = bossSlotRuntimeLookup.HasBuffer(enemyEntity);
            DynamicBuffer<EnemyBossPatternSlotRuntimeElement> bossSlotRuntimes = hasBossSlotRuntimes
                ? bossSlotRuntimeLookup[enemyEntity]
                : default;
            EnemyPatternConfig currentPatternConfig = patternConfigLookup[enemyEntity];
            EnemyPatternRuntimeState currentPatternRuntimeState = patternRuntimeStateLookup[enemyEntity];
            DamageFlashState runtimeState = damageFlashState.ValueRO;
            float damageBlend = DamageFlashRuntimeUtility.Advance(ref runtimeState, in damageFlashConfig.ValueRO, deltaTime);
            EnemyVisualFlashPresentationState currentPresentationState = visualFlashPresentationState.ValueRO;
            EnemyOffensiveEngagementBlendResult offensiveBlendResult = EnemyOffensiveEngagementPresentationUtility.ResolveBlendResult(offensiveEngagementConfigs,
                                                                                                                                     shooterRuntime,
                                                                                                                                     hasBossSlotRuntimes,
                                                                                                                                     bossSlotRuntimes,
                                                                                                                                     in currentPatternConfig,
                                                                                                                                     in currentPatternRuntimeState);
            float offensiveBlend = EnemyOffensiveEngagementPresentationUtility.ResolveDisplayedBlend(currentPresentationState.OffensiveEngagementBlend,
                                                                                                    currentPresentationState.OffensiveEngagementFadeOutSeconds,
                                                                                                    offensiveBlendResult,
                                                                                                    deltaTime,
                                                                                                    out float rememberedFadeOutSeconds);
            float4 offensiveColor = ResolveOffensiveColor(currentPresentationState, offensiveBlendResult);
            EnemyBossPatternChangePresentationResult patternChangeResult = ResolvePatternChangePresentation(enemyEntity,
                                                                                                            patternChangeConfigLookup,
                                                                                                            patternChangeStateLookup,
                                                                                                            deltaTime);
            bool enemyVisible = visualRuntimeState.ValueRO.IsVisible != 0;

            SyncOffensiveBillboard(entityManager,
                                   enemyEntity,
                                   enemyVisible,
                                   enemyTransform.ValueRO.Position,
                                   cameraTransform,
                                   offensiveEngagementConfigs,
                                   shooterRuntime,
                                   hasBossSlotRuntimes,
                                   bossSlotRuntimes,
                                   in currentPatternConfig,
                                   in currentPatternRuntimeState,
                                   patternChangeResult);

            float4 targetColor = damageFlashConfig.ValueRO.FlashColor;
            float targetBlend = damageBlend;

            if (offensiveBlend > targetBlend)
            {
                targetBlend = offensiveBlend;
                targetColor = offensiveColor;
            }

            if (patternChangeResult.Blend > targetBlend)
            {
                targetBlend = patternChangeResult.Blend;
                targetColor = patternChangeResult.Color;
            }

            if (HasUnchangedPresentationState(currentPresentationState,
                                              targetBlend,
                                              targetColor,
                                              offensiveBlend,
                                              offensiveColor,
                                              rememberedFadeOutSeconds))
            {
                damageFlashState.ValueRW = runtimeState;
                continue;
            }

            switch (visualConfig.ValueRO.Mode)
            {
                case EnemyVisualMode.CompanionAnimator:
                    ApplyCompanionFlash(entityManager,
                                        enemyEntity,
                                        DamageFlashRuntimeUtility.ToManagedColor(targetColor),
                                        targetBlend);
                    break;

                default:
                    EnemyDamageFlashRenderUtility.ApplyGpuFlash(entityManager,
                                                                enemyEntity,
                                                                targetColor,
                                                                targetBlend);
                    break;
            }

            runtimeState.AppliedBlend = damageBlend;
            damageFlashState.ValueRW = runtimeState;
            currentPresentationState.AppliedBlend = targetBlend;
            currentPresentationState.AppliedColor = targetColor;
            currentPresentationState.OffensiveEngagementColor = offensiveColor;
            currentPresentationState.OffensiveEngagementBlend = offensiveBlend;
            currentPresentationState.OffensiveEngagementFadeOutSeconds = rememberedFadeOutSeconds;
            visualFlashPresentationState.ValueRW = currentPresentationState;
        }

        EnemyOffensiveEngagementBillboardRuntimeUtility.ReleaseInactiveViews(entityManager);
    }

    public void OnDestroy(ref SystemState state)
    {
        cachedMainCameraTransform = null;
        nextCameraResolveTime = 0f;
        EnemyOffensiveEngagementBillboardRuntimeUtility.Shutdown();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the current main camera transform with a small retry interval so presentation systems do not repeatedly scan cameras every frame.
    /// </summary>
    /// <param name="elapsedTime">Current world elapsed time.</param>
    /// <returns>The resolved main camera transform, or null when no active camera is currently available.</returns>
    private static Transform ResolveMainCameraTransform(float elapsedTime)
    {
        if (cachedMainCameraTransform != null)
        {
            return cachedMainCameraTransform;
        }

        if (elapsedTime < nextCameraResolveTime)
        {
            return null;
        }

        nextCameraResolveTime = elapsedTime + CameraResolveRetryIntervalSeconds;
        Camera resolvedCamera = Camera.main;

        if (resolvedCamera == null)
        {
            Camera[] allCameras = Camera.allCameras;

            for (int cameraIndex = 0; cameraIndex < allCameras.Length; cameraIndex++)
            {
                Camera candidateCamera = allCameras[cameraIndex];

                if (candidateCamera == null)
                {
                    continue;
                }

                if (!candidateCamera.isActiveAndEnabled)
                {
                    continue;
                }

                resolvedCamera = candidateCamera;
                break;
            }
        }

        if (resolvedCamera == null)
        {
            cachedMainCameraTransform = null;
            return null;
        }

        cachedMainCameraTransform = resolvedCamera.transform;
        return cachedMainCameraTransform;
    }

    /// <summary>
    /// Resolves which offensive color should currently be remembered for fade-out continuity.
    /// </summary>
    /// <param name="currentPresentationState">Current stored presentation state.</param>
    /// <param name="offensiveBlendResult">Strongest active offensive blend result for the current frame.</param>
    /// <returns>Offensive color that should be used for the current frame and stored back into runtime state.</returns>
    private static float4 ResolveOffensiveColor(EnemyVisualFlashPresentationState currentPresentationState,
                                                EnemyOffensiveEngagementBlendResult offensiveBlendResult)
    {
        if (!offensiveBlendResult.IsActive)
        {
            return currentPresentationState.OffensiveEngagementColor;
        }

        if (offensiveBlendResult.Blend >= currentPresentationState.OffensiveEngagementBlend)
        {
            return offensiveBlendResult.Color;
        }

        return currentPresentationState.OffensiveEngagementColor;
    }

    /// <summary>
    /// Advances and resolves the boss pattern-change feedback window for the current enemy.
    /// </summary>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="configLookup">Optional pattern-change feedback config lookup.</param>
    /// <param name="stateLookup">Optional pattern-change feedback state lookup.</param>
    /// <param name="deltaTime">Presentation delta time.</param>
    /// <returns>Resolved pattern-change color blend and billboard data for this frame.</returns>
    private static EnemyBossPatternChangePresentationResult ResolvePatternChangePresentation(Entity enemyEntity,
                                                                                             ComponentLookup<EnemyBossPatternChangeFeedbackConfig> configLookup,
                                                                                             ComponentLookup<EnemyBossPatternChangeFeedbackState> stateLookup,
                                                                                             float deltaTime)
    {
        EnemyBossPatternChangePresentationResult result = default(EnemyBossPatternChangePresentationResult);

        if (!configLookup.HasComponent(enemyEntity) || !stateLookup.HasComponent(enemyEntity))
        {
            return result;
        }

        EnemyBossPatternChangeFeedbackConfig config = configLookup[enemyEntity];
        EnemyBossPatternChangeFeedbackState feedbackState = stateLookup[enemyEntity];

        if (config.Enabled == 0)
        {
            feedbackState.RemainingSeconds = 0f;
            feedbackState.DisplayedBlend = 0f;
            stateLookup[enemyEntity] = feedbackState;
            return result;
        }

        float safeDeltaTime = math.max(0f, deltaTime);
        bool windowWasActive = feedbackState.RemainingSeconds > 0f;

        if (windowWasActive)
        {
            feedbackState.ElapsedSeconds += safeDeltaTime;
            feedbackState.RemainingSeconds = math.max(0f, feedbackState.RemainingSeconds - safeDeltaTime);
        }

        EnemyOffensiveEngagementBlendResult targetBlend = default(EnemyOffensiveEngagementBlendResult);

        if (windowWasActive && config.EnableColorBlend != 0 && config.ColorBlendDurationSeconds > 0f)
        {
            targetBlend.IsActive = true;
            targetBlend.Blend = math.saturate(config.ColorBlendMaximumBlend);
            targetBlend.Color = config.ColorBlendColor;
            targetBlend.FadeOutSeconds = math.max(0f, config.ColorBlendFadeOutSeconds);
        }

        feedbackState.DisplayedBlend = EnemyOffensiveEngagementPresentationUtility.ResolveDisplayedBlend(feedbackState.DisplayedBlend,
                                                                                                        feedbackState.FadeOutSeconds,
                                                                                                        targetBlend,
                                                                                                        safeDeltaTime,
                                                                                                        out float rememberedFadeOutSeconds);
        feedbackState.FadeOutSeconds = rememberedFadeOutSeconds;

        if (targetBlend.IsActive)
        {
            feedbackState.DisplayedColor = targetBlend.Color;
        }

        stateLookup[enemyEntity] = feedbackState;
        result.Blend = feedbackState.DisplayedBlend;
        result.Color = feedbackState.DisplayedColor;
        result.BillboardActive = windowWasActive && config.EnableBillboard != 0 && config.BillboardDurationSeconds > 0f;
        result.BillboardColor = config.BillboardColor;
        result.BillboardOffset = config.BillboardOffset;
        result.BillboardScale = EnemyOffensiveEngagementPresentationUtility.ResolvePulseScale(config.BillboardBaseScale,
                                                                                              config.BillboardPulseScaleMultiplier,
                                                                                              config.BillboardPulseExpandDurationSeconds,
                                                                                              config.BillboardPulseContractDurationSeconds,
                                                                                              feedbackState.ElapsedSeconds);
        return result;
    }

    /// <summary>
    /// Updates the managed offensive billboard view when one is available on the current enemy entity.
    /// </summary>
    /// <param name="entityManager">Entity manager used to resolve the managed billboard companion component.</param>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="enemyVisible">Whether the enemy is currently visible after distance culling.</param>
    /// <param name="enemyPosition">Current enemy world position.</param>
    /// <param name="cameraTransform">Active camera transform used for billboarding.</param>
    /// <param name="offensiveEngagementConfigs">Baked offensive engagement configs for the current enemy.</param>
    /// <param name="shooterRuntime">Current shooter runtime buffer used by weapon timing evaluation.</param>
    /// <param name="hasBossSlotRuntimes">Whether boss slot runtime data is available for activation feedback.</param>
    /// <param name="bossSlotRuntimes">Boss slot runtime buffer used by module activation timing.</param>
    /// <param name="patternConfig">Current compiled pattern config used by short-range timing evaluation.</param>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state used by short-range timing evaluation.</param>
    /// <param name="patternChangeResult">Boss pattern-change billboard result resolved for the current frame.</param>
    private static void SyncOffensiveBillboard(EntityManager entityManager,
                                               Entity enemyEntity,
                                               bool enemyVisible,
                                               float3 enemyPosition,
                                               Transform cameraTransform,
                                               DynamicBuffer<EnemyOffensiveEngagementConfigElement> offensiveEngagementConfigs,
                                               DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                               bool hasBossSlotRuntimes,
                                               DynamicBuffer<EnemyBossPatternSlotRuntimeElement> bossSlotRuntimes,
                                               in EnemyPatternConfig patternConfig,
                                               in EnemyPatternRuntimeState patternRuntimeState,
                                               EnemyBossPatternChangePresentationResult patternChangeResult)
    {
        if (!EnemyOffensiveEngagementBillboardRuntimeUtility.TryResolveRuntimeView(entityManager,
                                                                                  enemyEntity,
                                                                                  out EnemyOffensiveEngagementBillboardView billboardView))
        {
            return;
        }

        if (!enemyVisible || cameraTransform == null)
        {
            billboardView.Hide();
            return;
        }

        EnemyOffensiveEngagementBillboardResult billboardResult = EnemyOffensiveEngagementPresentationUtility.ResolveBillboardResult(offensiveEngagementConfigs,
                                                                                                                                   shooterRuntime,
                                                                                                                                   hasBossSlotRuntimes,
                                                                                                                                   bossSlotRuntimes,
                                                                                                                                   in patternConfig,
                                                                                                                                   in patternRuntimeState);

        if (!billboardResult.IsActive && !patternChangeResult.BillboardActive)
        {
            billboardView.Hide();
            return;
        }

        Vector3 worldPosition = new Vector3(enemyPosition.x, enemyPosition.y, enemyPosition.z);
        bool usePatternChangeBillboard = patternChangeResult.BillboardActive;
        EnemyOffensiveEngagementTriggerSource source = usePatternChangeBillboard
            ? EnemyOffensiveEngagementTriggerSource.BossPatternChange
            : billboardResult.Source;
        int visualSettingsKey = usePatternChangeBillboard ? -1 : billboardResult.VisualSettingsKey;
        bool useOverrideVisualSettings = !usePatternChangeBillboard && billboardResult.UseOverrideVisualSettings;
        float4 color = usePatternChangeBillboard ? patternChangeResult.BillboardColor : billboardResult.Color;
        float3 offsetFloat3 = usePatternChangeBillboard ? patternChangeResult.BillboardOffset : billboardResult.Offset;
        float uniformScale = usePatternChangeBillboard ? patternChangeResult.BillboardScale : billboardResult.UniformScale;
        Vector3 worldOffset = new Vector3(offsetFloat3.x, offsetFloat3.y, offsetFloat3.z);
        billboardView.Render(worldPosition,
                             cameraTransform,
                             source,
                             visualSettingsKey,
                             useOverrideVisualSettings,
                             DamageFlashRuntimeUtility.ToManagedColor(color),
                             worldOffset,
                             uniformScale);
    }

    /// <summary>
    /// Returns whether the currently computed presentation values match the state already applied to renderers.
    /// </summary>
    /// <param name="currentPresentationState">Current stored presentation state.</param>
    /// <param name="targetBlend">Final composed blend that would be applied this frame.</param>
    /// <param name="targetColor">Final composed color that would be applied this frame.</param>
    /// <param name="offensiveBlend">Current offensive-only displayed blend.</param>
    /// <param name="offensiveColor">Current offensive-only remembered color.</param>
    /// <param name="rememberedFadeOutSeconds">Current remembered offensive fade-out duration.</param>
    /// <returns>True when renderers already match the requested frame state.</returns>
    private static bool HasUnchangedPresentationState(EnemyVisualFlashPresentationState currentPresentationState,
                                                      float targetBlend,
                                                      float4 targetColor,
                                                      float offensiveBlend,
                                                      float4 offensiveColor,
                                                      float rememberedFadeOutSeconds)
    {
        if (math.abs(currentPresentationState.AppliedBlend - targetBlend) > BlendEpsilon)
        {
            return false;
        }

        if (!HasApproximatelyEqualColor(currentPresentationState.AppliedColor, targetColor))
        {
            return false;
        }

        if (math.abs(currentPresentationState.OffensiveEngagementBlend - offensiveBlend) > BlendEpsilon)
        {
            return false;
        }

        if (!HasApproximatelyEqualColor(currentPresentationState.OffensiveEngagementColor, offensiveColor))
        {
            return false;
        }

        return math.abs(currentPresentationState.OffensiveEngagementFadeOutSeconds - rememberedFadeOutSeconds) <= BlendEpsilon;
    }

    /// <summary>
    /// Applies the resolved flash values to a managed Animator-based visual companion.
    /// </summary>
    /// <param name="entityManager">Entity manager used to resolve the Animator component object.</param>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="flashColor">Final flash color resolved for this frame.</param>
    /// <param name="targetBlend">Final flash blend resolved for this frame.</param>
    private static void ApplyCompanionFlash(EntityManager entityManager,
                                            Entity enemyEntity,
                                            Color flashColor,
                                            float targetBlend)
    {
        if (!entityManager.HasComponent<Animator>(enemyEntity))
        {
            return;
        }

        Animator animator = entityManager.GetComponentObject<Animator>(enemyEntity);

        if (animator == null)
        {
            return;
        }

        ManagedDamageFlashRendererUtility.ApplyToAnimator(animator, flashColor, targetBlend);
    }

    /// <summary>
    /// Checks whether two linear colors are approximately equal using the shared presentation epsilon.
    /// </summary>
    /// <param name="left">Left-hand color.</param>
    /// <param name="right">Right-hand color.</param>
    /// <returns>True when the largest component delta stays below the configured epsilon.</returns>
    private static bool HasApproximatelyEqualColor(float4 left, float4 right)
    {
        float4 absoluteDelta = math.abs(left - right);
        return math.cmax(absoluteDelta) <= ColorEpsilon;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores resolved boss pattern-change presentation values for one frame.
/// </summary>
internal struct EnemyBossPatternChangePresentationResult
{
    public float Blend;
    public float4 Color;
    public bool BillboardActive;
    public float4 BillboardColor;
    public float3 BillboardOffset;
    public float BillboardScale;
}
