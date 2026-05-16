using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

/// <summary>
/// Stores immutable runtime tuning used by short hit-flash feedback presentation.
/// </summary>
public struct DamageFlashConfig : IComponentData
{
    #region Fields
    public float4 FlashColor;
    public float DurationSeconds;
    public float MaximumBlend;
    #endregion
}

/// <summary>
/// Stores mutable runtime hit-flash playback state.
/// </summary>
public struct DamageFlashState : IComponentData
{
    #region Fields
    public float RemainingSeconds;
    public float AppliedBlend;
    #endregion
}

/// <summary>
/// Stores the original material color used to restore per-instance renderer overrides after a hit flash ends.
/// </summary>
public struct DamageFlashBaseColor : IComponentData
{
    #region Fields
    public float4 Value;
    #endregion
}

/// <summary>
/// Stores one renderer entity driven by a root-level hit-flash controller.
/// </summary>
public struct DamageFlashRenderTargetElement : IBufferElementData
{
    #region Fields
    public Entity Value;
    public float4 BaseColor;
    #endregion
}

/// <summary>
/// Custom Entities Graphics override for the shader-side flash tint color.
/// </summary>
[MaterialProperty("_HitFlashColor")]
public struct MaterialHitFlashColor : IComponentData
{
    #region Fields
    public float4 Value;
    #endregion
}

/// <summary>
/// Custom Entities Graphics override for the shader-side flash blend factor.
/// </summary>
[MaterialProperty("_HitFlashBlend")]
public struct MaterialHitFlashBlend : IComponentData
{
    #region Fields
    public float Value;
    #endregion
}
