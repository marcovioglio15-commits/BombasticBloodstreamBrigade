using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Resolves, toggles, and scales the designer-authored Jetpack VFX object inside the active Visual Player hierarchy.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(PlayerManagedVisualAnimatorBridgeSystem))]
public partial struct PlayerJetpackVfxPresentationSystem : ISystem
{
    #region Constants
    private const float MinimumScaleMultiplier = 0.0001f;
    #endregion

    #region Fields
    private static readonly Dictionary<Entity, PlayerJetpackVfxVisualBinding> visualBindings = new Dictionary<Entity, PlayerJetpackVfxVisualBinding>(2);
    private static readonly List<Entity> invalidOwners = new List<Entity>(2);
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Requires Jetpack VFX runtime settings and activity state before resolving Visual Player objects.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerJetpackVfxConfig>();
        state.RequireForUpdate<PlayerJetpackVfxRuntimeState>();
    }

    /// <summary>
    /// Disables resolved Jetpack VFX objects and clears presentation caches during world teardown.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnDestroy(ref SystemState state)
    {
        Dictionary<Entity, PlayerJetpackVfxVisualBinding>.Enumerator enumerator = visualBindings.GetEnumerator();

        while (enumerator.MoveNext())
        {
            RestoreAuthoredScale(enumerator.Current.Value);
            SetVisible(enumerator.Current.Value.TargetObject, false);
        }

        enumerator.Dispose();
        visualBindings.Clear();
        invalidOwners.Clear();
    }

    /// <summary>
    /// Resolves the current managed or companion Visual Player hierarchy and applies only required visibility and scale changes.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();
        EntityManager entityManager = state.EntityManager;
        CleanupInvalidOwners(entityManager);

        foreach ((RefRO<PlayerJetpackVfxConfig> config,
                  RefRO<PlayerJetpackVfxRuntimeState> runtimeState,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRO<PlayerJetpackVfxConfig>,
                                    RefRO<PlayerJetpackVfxRuntimeState>>()
                             .WithEntityAccess())
        {
            if (config.ValueRO.RuntimeReference.Length <= 0)
            {
                HideAndRemoveBinding(playerEntity);
                continue;
            }

            if (!TryResolveVisualRoot(entityManager, playerEntity, out Transform visualRoot))
            {
                HideAndRemoveBinding(playerEntity);
                continue;
            }

            PlayerJetpackVfxVisualBinding binding = ResolveBinding(playerEntity,
                                                                   visualRoot,
                                                                   config.ValueRO.RuntimeReference);

            if (binding == null)
                continue;

            ApplyScale(binding, runtimeState.ValueRO.DesiredScaleMultiplier);
            SetVisible(binding.TargetObject, runtimeState.ValueRO.DesiredVisible != 0);
        }
    }
    #endregion

    #region Resolution
    /// <summary>
    /// Resolves the active Visual Player root from the runtime bridge first, then from the companion Animator hierarchy.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect managed Animator components.</param>
    /// <param name="playerEntity">Player entity whose Visual Player root should be resolved.</param>
    /// <param name="visualRoot">Resolved Visual Player root transform.</param>
    /// <returns>True when a valid managed or companion visual root exists.</returns>
    private static bool TryResolveVisualRoot(EntityManager entityManager,
                                             Entity playerEntity,
                                             out Transform visualRoot)
    {
        if (PlayerManagedVisualAnimatorBridgeSystem.TryGetRuntimeBridgeRoot(playerEntity, out visualRoot))
            return true;

        visualRoot = null;

        if (!entityManager.HasComponent<Animator>(playerEntity))
            return false;

        Animator animator = entityManager.GetComponentObject<Animator>(playerEntity);

        if (animator == null)
            return false;

        PlayerWeaponVisualSet weaponVisualSet = animator.GetComponentInParent<PlayerWeaponVisualSet>(true);
        visualRoot = weaponVisualSet != null ? weaponVisualSet.transform : animator.transform;
        return visualRoot != null;
    }

    /// <summary>
    /// Resolves and caches the Jetpack VFX object for one visual root and scalable prefab-relative reference.
    /// </summary>
    /// <param name="playerEntity">Player entity owning the visual binding.</param>
    /// <param name="visualRoot">Current Visual Player root.</param>
    /// <param name="runtimeReference">Prefab-relative path or unique object name.</param>
    /// <returns>Resolved Jetpack VFX binding, or null when the reference is empty or unresolved.</returns>
    private static PlayerJetpackVfxVisualBinding ResolveBinding(Entity playerEntity,
                                                                Transform visualRoot,
                                                                FixedString128Bytes runtimeReference)
    {
        if (visualBindings.TryGetValue(playerEntity, out PlayerJetpackVfxVisualBinding binding) &&
            binding.RootTransform == visualRoot &&
            binding.RuntimeReference.Equals(runtimeReference) &&
            binding.TargetObject != null)
            return binding;

        HideAndRemoveBinding(playerEntity);

        if (runtimeReference.Length <= 0 ||
            !PlayerWeaponVisualReferenceUtility.TryResolve(visualRoot,
                                                           runtimeReference.ToString(),
                                                           out Transform targetTransform))
            return null;

        PlayerJetpackVfxVisualBinding resolvedBinding = new PlayerJetpackVfxVisualBinding
        {
            RootTransform = visualRoot,
            TargetObject = targetTransform.gameObject,
            RuntimeReference = runtimeReference,
            AuthoredLocalScale = targetTransform.localScale,
            AppliedScaleMultiplier = 1f
        };
        visualBindings[playerEntity] = resolvedBinding;
        return resolvedBinding;
    }
    #endregion

    #region Visibility
    /// <summary>
    /// Applies one active state only when the resolved Jetpack VFX object requires a change.
    /// </summary>
    /// <param name="targetObject">Optional designer-authored Jetpack VFX object.</param>
    /// <param name="visible">Desired active state.</param>
    private static void SetVisible(GameObject targetObject, bool visible)
    {
        if (targetObject != null && targetObject.activeSelf != visible)
            targetObject.SetActive(visible);
    }

    /// <summary>
    /// Applies a bounded multiplier over the cached designer-authored local scale only when it changes.
    /// </summary>
    /// <param name="binding">Resolved Jetpack VFX binding.</param>
    /// <param name="desiredScaleMultiplier">Desired multiplier published by ECS gameplay state.</param>
    private static void ApplyScale(PlayerJetpackVfxVisualBinding binding, float desiredScaleMultiplier)
    {
        if (binding == null || binding.TargetObject == null)
            return;

        float scaleMultiplier = float.IsNaN(desiredScaleMultiplier) ||
                                float.IsInfinity(desiredScaleMultiplier) ||
                                desiredScaleMultiplier <= 0f
            ? 1f
            : Mathf.Max(MinimumScaleMultiplier, desiredScaleMultiplier);

        if (Mathf.Abs(binding.AppliedScaleMultiplier - scaleMultiplier) <= 0.0001f)
            return;

        binding.TargetObject.transform.localScale = binding.AuthoredLocalScale * scaleMultiplier;
        binding.AppliedScaleMultiplier = scaleMultiplier;
    }

    /// <summary>
    /// Restores the designer-authored local scale before a cached binding is discarded.
    /// </summary>
    /// <param name="binding">Optional cached Jetpack VFX binding.</param>
    private static void RestoreAuthoredScale(PlayerJetpackVfxVisualBinding binding)
    {
        if (binding == null || binding.TargetObject == null)
            return;

        binding.TargetObject.transform.localScale = binding.AuthoredLocalScale;
        binding.AppliedScaleMultiplier = 1f;
    }

    /// <summary>
    /// Disables and removes one cached Jetpack VFX binding before its visual root or reference changes.
    /// </summary>
    /// <param name="playerEntity">Player entity whose cached binding should be removed.</param>
    private static void HideAndRemoveBinding(Entity playerEntity)
    {
        if (!visualBindings.TryGetValue(playerEntity, out PlayerJetpackVfxVisualBinding binding))
            return;

        RestoreAuthoredScale(binding);
        SetVisible(binding.TargetObject, false);
        visualBindings.Remove(playerEntity);
    }

    /// <summary>
    /// Removes cached visual bindings whose owner entities no longer exist.
    /// </summary>
    /// <param name="entityManager">Entity manager used to validate cached owner entities.</param>
    private static void CleanupInvalidOwners(EntityManager entityManager)
    {
        invalidOwners.Clear();
        Dictionary<Entity, PlayerJetpackVfxVisualBinding>.Enumerator enumerator = visualBindings.GetEnumerator();

        while (enumerator.MoveNext())
        {
            if (!entityManager.Exists(enumerator.Current.Key))
                invalidOwners.Add(enumerator.Current.Key);
        }

        enumerator.Dispose();

        for (int ownerIndex = 0; ownerIndex < invalidOwners.Count; ownerIndex++)
            HideAndRemoveBinding(invalidOwners[ownerIndex]);

        invalidOwners.Clear();
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Caches one resolved designer-authored Jetpack VFX and its authored local scale while leaving position and rotation untouched.
    /// </summary>
    private sealed class PlayerJetpackVfxVisualBinding
    {
        public Transform RootTransform;
        public GameObject TargetObject;
        public Vector3 AuthoredLocalScale;
        public float AppliedScaleMultiplier;
        public FixedString128Bytes RuntimeReference;
    }
    #endregion
}
