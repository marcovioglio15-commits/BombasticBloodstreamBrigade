using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

#region Utilities
/// <summary>
/// Provides visual-component setup and reset helpers for pooled enemy instances.
/// </summary>
public static class EnemyPoolVisualUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures pooled enemies expose the visual components required by presentation systems.
    /// Called by pool validation before an enemy can be activated or parked.
    /// </summary>
    /// <param name="entityManager">Entity manager used to add missing visual components.</param>
    /// <param name="enemyEntity">Enemy instance that must expose the expected visual runtime surface.</param>
    public static void EnsureVisualComponents(EntityManager entityManager, Entity enemyEntity)
    {
        if (!entityManager.HasComponent<EnemyVisualConfig>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, new EnemyVisualConfig
            {
                Mode = EnemyVisualMode.GpuBaked,
                AnimationSpeed = 1f,
                GpuLoopDuration = 1f,
                MaxVisibleDistance = 55f,
                VisibleDistanceHysteresis = 6f,
                UseDistanceCulling = 1
            });

        if (!entityManager.HasComponent<EnemyVisualRuntimeState>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, new EnemyVisualRuntimeState
            {
                AnimationTime = 0f,
                LastSquaredDistanceToPlayer = 0f,
                IsVisible = 1,
                CompanionInitialized = 0,
                AppliedVisibilityPriorityTier = int.MinValue
            });

        if (!entityManager.HasComponent<EnemyHitVfxConfig>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, new EnemyHitVfxConfig
            {
                PrefabEntity = Entity.Null,
                Prefab = default,
                SpawnOffset = float3.zero,
                LifetimeSeconds = 0.35f,
                ScaleMultiplier = 1f
            });

        if (!entityManager.HasComponent<EnemySpawnVfxConfig>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, new EnemySpawnVfxConfig
            {
                PrefabEntity = Entity.Null,
                Prefab = default,
                Timing = EnemySpawnVfxTiming.OnSpawn,
                SpawnOffset = float3.zero,
                LifetimeSeconds = 0.5f,
                ScaleMultiplier = 1f
            });

        if (!entityManager.HasComponent<EnemyVisualFlashPresentationState>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, CreateDefaultVisualFlashPresentationState());

        if (!entityManager.HasComponent<EnemyFaceFlipbookConfig>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, CreateDefaultFaceFlipbookConfig());

        if (!entityManager.HasComponent<EnemyFaceFlipbookStateData>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, CreateDefaultFaceFlipbookState());

        if (!entityManager.HasComponent<EnemyElasticHitConfig>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, default(EnemyElasticHitConfig));

        if (!entityManager.HasComponent<EnemyDeathPuddleConfig>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, default(EnemyDeathPuddleConfig));

        if (!entityManager.HasComponent<EnemyElasticHitState>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, CreateDefaultElasticHitState());

        if (!entityManager.HasComponent<EnemyElasticHitActive>(enemyEntity))
        {
            entityManager.AddComponent<EnemyElasticHitActive>(enemyEntity);
            entityManager.SetComponentEnabled<EnemyElasticHitActive>(enemyEntity, false);
        }

        if (!entityManager.HasBuffer<DamageFlashRenderTargetElement>(enemyEntity))
            entityManager.AddBuffer<DamageFlashRenderTargetElement>(enemyEntity);
    }

    /// <summary>
    /// Ensures the visual-mode tag set matches the resolved visual configuration.
    /// Called after validation because companion-animator support depends on the final component set.
    /// </summary>
    /// <param name="entityManager">Entity manager used to add or remove visual tags.</param>
    /// <param name="enemyEntity">Enemy instance to inspect.</param>
    public static void EnsureVisualModeTags(EntityManager entityManager, Entity enemyEntity)
    {
        bool hasCompanionVisualTag = entityManager.HasComponent<EnemyVisualCompanionAnimator>(enemyEntity);
        bool hasGpuVisualTag = entityManager.HasComponent<EnemyVisualGpuBaked>(enemyEntity);
        bool hasAnimatorComponent = entityManager.HasComponent<Animator>(enemyEntity);
        EnemyVisualMode visualMode = EnemyVisualMode.GpuBaked;

        if (entityManager.HasComponent<EnemyVisualConfig>(enemyEntity))
            visualMode = entityManager.GetComponentData<EnemyVisualConfig>(enemyEntity).Mode;

        switch (visualMode)
        {
            case EnemyVisualMode.CompanionAnimator:
                if (!hasAnimatorComponent)
                    goto default;

                if (hasGpuVisualTag)
                    entityManager.RemoveComponent<EnemyVisualGpuBaked>(enemyEntity);

                if (!hasCompanionVisualTag)
                    entityManager.AddComponent<EnemyVisualCompanionAnimator>(enemyEntity);
                break;

            default:
                if (hasCompanionVisualTag)
                    entityManager.RemoveComponent<EnemyVisualCompanionAnimator>(enemyEntity);

                if (!hasGpuVisualTag)
                    entityManager.AddComponent<EnemyVisualGpuBaked>(enemyEntity);
                break;
        }
    }

    /// <summary>
    /// Resets presentation runtime state for one enemy instance.
    /// Called when entering or leaving the active simulation set.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate visual runtime state.</param>
    /// <param name="enemyEntity">Enemy instance to reset.</param>
    /// <param name="isVisible">Target visibility flag after reset.</param>
    public static void ResetVisualRuntimeState(EntityManager entityManager, Entity enemyEntity, byte isVisible)
    {
        if (!entityManager.HasComponent<EnemyVisualRuntimeState>(enemyEntity))
            return;

        EnemyVisualRuntimeState visualRuntimeState = entityManager.GetComponentData<EnemyVisualRuntimeState>(enemyEntity);
        visualRuntimeState.AnimationTime = 0f;
        visualRuntimeState.LastSquaredDistanceToPlayer = 0f;
        visualRuntimeState.IsVisible = isVisible;
        visualRuntimeState.CompanionInitialized = 0;
        visualRuntimeState.AppliedVisibilityPriorityTier = int.MinValue;
        entityManager.SetComponentData(enemyEntity, visualRuntimeState);
    }

    /// <summary>
    /// Clears transient visual feedback state when an enemy is recycled.
    /// Called by pooled gameplay reset so flashes and engagement overlays never leak across activations.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate visual presentation state.</param>
    /// <param name="enemyEntity">Enemy instance whose presentation feedback must be reset.</param>
    public static void ResetPresentationRuntimeState(EntityManager entityManager, Entity enemyEntity)
    {
        if (entityManager.HasComponent<DamageFlashState>(enemyEntity))
        {
            DamageFlashState damageFlashState = entityManager.GetComponentData<DamageFlashState>(enemyEntity);
            damageFlashState.RemainingSeconds = 0f;
            damageFlashState.AppliedBlend = 0f;
            entityManager.SetComponentData(enemyEntity, damageFlashState);
        }

        if (entityManager.HasComponent<EnemyVisualFlashPresentationState>(enemyEntity))
            entityManager.SetComponentData(enemyEntity, CreateDefaultVisualFlashPresentationState());

        if (entityManager.HasComponent<EnemyBossTag>(enemyEntity) &&
            entityManager.HasBuffer<EnemyOffensiveEngagementConfigElement>(enemyEntity))
            entityManager.GetBuffer<EnemyOffensiveEngagementConfigElement>(enemyEntity).Clear();

        if (entityManager.HasComponent<EnemyBossPatternChangeFeedbackState>(enemyEntity))
            entityManager.SetComponentData(enemyEntity, CreateDefaultBossPatternChangeFeedbackState());

        if (entityManager.HasComponent<EnemyFaceFlipbookStateData>(enemyEntity))
            entityManager.SetComponentData(enemyEntity, CreateDefaultFaceFlipbookState());

        EnemyDamageFlashRenderUtility.ResetGpuFlash(entityManager, enemyEntity);
        EnemyFaceFlipbookRenderUtility.ResetGpuFace(entityManager, enemyEntity);
        EnemyElasticHitRenderUtility.ResetGpuElasticHit(entityManager, enemyEntity);

        if (entityManager.HasComponent<EnemyElasticHitState>(enemyEntity))
            entityManager.SetComponentData(enemyEntity, CreateDefaultElasticHitState());

        if (entityManager.HasComponent<EnemyElasticHitActive>(enemyEntity))
            entityManager.SetComponentEnabled<EnemyElasticHitActive>(enemyEntity, false);

        if (entityManager.HasComponent<Animator>(enemyEntity))
        {
            Animator animator = entityManager.GetComponentObject<Animator>(enemyEntity);

            if (animator != null)
            {
                ManagedDamageFlashRendererUtility.ApplyToAnimator(animator, Color.white, 0f);
                ManagedDamageFlashRendererUtility.ApplyElasticToAnimator(animator,
                                                                         new float4(0f, 0f, 1f, 0f),
                                                                         float4.zero,
                                                                         float4.zero);
            }
        }

        if (entityManager.HasComponent<EnemyOffensiveEngagementBillboardView>(enemyEntity))
        {
            EnemyOffensiveEngagementBillboardView billboardView = entityManager.GetComponentObject<EnemyOffensiveEngagementBillboardView>(enemyEntity);

            if (billboardView != null)
                billboardView.Hide();
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates the default flash presentation state used by pooled enemies.
    /// </summary>
    /// <returns>Default visual flash presentation state.</returns>
    private static EnemyVisualFlashPresentationState CreateDefaultVisualFlashPresentationState()
    {
        return new EnemyVisualFlashPresentationState
        {
            AppliedBlend = 0f,
            AppliedColor = new float4(1f, 1f, 1f, 1f),
            OffensiveEngagementColor = new float4(1f, 1f, 1f, 1f),
            OffensiveEngagementBlend = 0f,
            OffensiveEngagementFadeOutSeconds = 0f
        };
    }

    /// <summary>
    /// Creates a cleared boss pattern-change feedback state for pooled boss reuse.
    /// </summary>
    /// <returns>Default boss pattern-change feedback state.</returns>
    private static EnemyBossPatternChangeFeedbackState CreateDefaultBossPatternChangeFeedbackState()
    {
        return new EnemyBossPatternChangeFeedbackState
        {
            ElapsedSeconds = 0f,
            RemainingSeconds = 0f,
            DisplayedBlend = 0f,
            DisplayedColor = float4.zero,
            FadeOutSeconds = 0f
        };
    }

    /// <summary>
    /// Creates a conservative default face flipbook config for recovered pooled enemies.
    /// </summary>
    /// <returns>Default face flipbook config matching the shared enemy-face material contract.</returns>
    private static EnemyFaceFlipbookConfig CreateDefaultFaceFlipbookConfig()
    {
        return new EnemyFaceFlipbookConfig
        {
            Enabled = 1,
            IdleEnabled = 1,
            AttackEnabled = 1,
            DamageEnabled = 1,
            IdleGrid = new float4(4f, 2f, 8f, 0f),
            AttackGrid = new float4(4f, 1f, 4f, 0f),
            DamageGrid = new float4(4f, 1f, 4f, 0f),
            IdleFramesPerSecond = 8f,
            AttackFramesPerSecond = 10f,
            DamageFramesPerSecond = 12f,
            IdleStartFrame = 0f,
            AttackStartFrame = 0f,
            DamageStartFrame = 0f,
            AttackDurationSeconds = 0.18f,
            DamageDurationSeconds = 0.14f,
            IdleAtlas = default,
            AttackAtlas = default,
            DamageAtlas = default
        };
    }

    /// <summary>
    /// Creates a cleared face flipbook runtime state for pooled enemy activation and recycle.
    /// </summary>
    /// <returns>Default face flipbook runtime state.</returns>
    private static EnemyFaceFlipbookStateData CreateDefaultFaceFlipbookState()
    {
        return new EnemyFaceFlipbookStateData
        {
            CurrentState = EnemyFaceFlipbookState.Idle,
            AttackRemainingSeconds = 0f,
            DamageRemainingSeconds = 0f,
            IdlePlaybackPhaseSeconds = 0f,
            AttackPlaybackPhaseSeconds = 0f,
            DamagePlaybackPhaseSeconds = 0f,
            LastObservedDamageLifetimeSeconds = 0f,
            HasObservedDamage = 0,
            WasEngagementActive = 0
        };
    }

    /// <summary>
    /// Creates a neutral elastic hit runtime state used by pooled enemy activation and recycle.
    /// </summary>
    /// <returns>Neutral elastic hit runtime state.</returns>
    private static EnemyElasticHitState CreateDefaultElasticHitState()
    {
        return new EnemyElasticHitState
        {
            RemainingSeconds = 0f,
            LastTriggerTime = -1000f,
            DirectionWorld = new float3(0f, 0f, 1f)
        };
    }
    #endregion

    #endregion
}
#endregion
