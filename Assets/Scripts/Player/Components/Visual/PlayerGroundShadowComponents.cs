using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Links one visual-runtime companion entity to the authoritative player entity whose presentation data it owns.
/// </summary>
public struct PlayerVisualRuntimeDataOwner : IComponentData
{
    public Entity PlayerEntity;
}

/// <summary>
/// Links the authoritative player entity to its presentation-only runtime data companion.
/// </summary>
public struct PlayerPresentationRuntimeReferences : IComponentData
{
    public Entity VisualRuntimeEntity;
    public Entity HealthBarVisualEntity;
    public Entity ActivePowerUpHudVisualEntity;
    public Entity PortraitHudVisualEntity;
    public Entity GrowthSequenceHudVisualEntity;
}

/// <summary>
/// Resolves presentation companion entities and managed Animator data without scanning scene objects.
/// </summary>
public static class PlayerPresentationRuntimeUtility
{
    #region Methods

    #region Resolution
    /// <summary>
    /// Resolves the presentation companion associated with one authoritative player entity.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player and companion entities.</param>
    /// <param name="playerEntity">Authoritative player whose companion is requested.</param>
    /// <param name="visualRuntimeEntity">Resolved presentation companion when available.</param>
    /// <returns>True when the player owns a valid presentation companion reference.</returns>
    public static bool TryResolveVisualRuntimeEntity(EntityManager entityManager,
                                                     Entity playerEntity,
                                                     out Entity visualRuntimeEntity)
    {
        visualRuntimeEntity = Entity.Null;

        if (!entityManager.Exists(playerEntity) ||
            !entityManager.HasComponent<PlayerPresentationRuntimeReferences>(playerEntity))
        {
            return false;
        }

        visualRuntimeEntity = entityManager
            .GetComponentData<PlayerPresentationRuntimeReferences>(playerEntity)
            .VisualRuntimeEntity;
        return entityManager.Exists(visualRuntimeEntity);
    }

    /// <summary>
    /// Resolves the managed Animator stored on one player's presentation companion.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player and companion entities.</param>
    /// <param name="playerEntity">Authoritative player whose Animator is requested.</param>
    /// <param name="animator">Resolved live Animator when available.</param>
    /// <returns>True when the presentation companion owns a live managed Animator.</returns>
    public static bool TryResolveAnimator(EntityManager entityManager,
                                          Entity playerEntity,
                                          out Animator animator)
    {
        animator = null;

        if (!TryResolveVisualRuntimeEntity(entityManager,
                                           playerEntity,
                                           out Entity visualRuntimeEntity) ||
            !entityManager.HasComponent<Animator>(visualRuntimeEntity))
        {
            return false;
        }

        animator = entityManager.GetComponentObject<Animator>(visualRuntimeEntity);
        return animator != null;
    }
    #endregion

    #endregion
}

/// <summary>
/// Runtime player hit-box shadow configuration consumed by world-space presentation.
/// </summary>
public struct PlayerGroundShadowConfig : IComponentData
{
    public float HitAreaMultiplier;
    public float2 PositionOffsetXZ;
    public float HeightOffset;
    public GroundShadowProjectionMode ProjectionMode;
    public float ProjectionMaxDistance;
    public float4 ShadowColor;
    public float ShadowAlpha;
    public float ShadowEdgeSoftness;
    public byte Enabled;
}

/// <summary>
/// Immutable player hit-box shadow baseline used when runtime scaling formulas rebuild visual settings.
/// </summary>
public struct PlayerBaseGroundShadowConfig : IComponentData
{
    public PlayerGroundShadowConfig Config;
}

/// <summary>
/// Tracks the unified runtime scaling hash last applied to player hit-box shadow settings.
/// </summary>
public struct PlayerGroundShadowScalingState : IComponentData
{
    public uint LastScalableStatsHash;
    public byte Initialized;
}

/// <summary>
/// Stores one player ground-shadow scaling entry baked from Visual Preset Add Scaling authoring data.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRuntimeGroundShadowScalingElement : IBufferElementData
{
    public FixedString128Bytes PayloadPath;
    public byte ValueType;
    public float BaseValue;
    public byte BaseBooleanValue;
    public byte IsInteger;
    public FixedString512Bytes Formula;
}
