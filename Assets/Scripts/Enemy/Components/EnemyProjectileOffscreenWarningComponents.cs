using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Stores unmanaged screen-edge warning settings for projectiles fired by one enemy type.
/// </summary>
public struct EnemyProjectileOffscreenWarningConfig : IComponentData
{
    public byte Enabled;
    public float4 IndicatorColor;
    public float IndicatorSizePixels;
    public float EdgePaddingPixels;
}

/// <summary>
/// Stores managed projectile warning assets that cannot be represented in unmanaged ECS components.
/// </summary>
public sealed class EnemyProjectileOffscreenWarningManagedConfig : IComponentData
{
    #region Fields
    public Sprite IndicatorSprite;
    #endregion
}
