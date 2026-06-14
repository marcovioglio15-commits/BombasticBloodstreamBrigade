using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Stores shared asset references and geometric presentation settings used by the Laser Beam managed renderer path.
/// </summary>
public struct PlayerLaserBeamVisualConfig : IComponentData
{
    #region Fields
    public UnityObjectRef<Material> BodyMaterial;
    public UnityObjectRef<Material> SourceEffectMaterial;
    public UnityObjectRef<Material> TerminalCapMaterial;
    public float VerticalLift;
    public float MinimumSegmentLength;
    public float MaximumRibbonSegmentLength;
    public float TerminalSplashLengthMultiplier;
    public float TerminalSplashWidthMultiplier;
    public float SourceForwardOffset;
    public float ImpactForwardOffset;
    #endregion
}

/// <summary>
/// Stores one authored source particle prefab mapped to a Laser Beam cap shape.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerLaserBeamSourceVariantElement : IBufferElementData
{
    #region Fields
    public LaserBeamCapShape Shape;
    public UnityObjectRef<GameObject> Prefab;
    #endregion
}

/// <summary>
/// Stores one authored impact particle prefab mapped to a Laser Beam cap shape.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerLaserBeamImpactVariantElement : IBufferElementData
{
    #region Fields
    public LaserBeamCapShape Shape;
    public UnityObjectRef<GameObject> Prefab;
    #endregion
}

/// <summary>
/// Stores one editable visual preset entry used by the Laser Beam managed renderer path.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerLaserBeamVisualPresetElement : IBufferElementData
{
    #region Fields
    public int VisualPresetId;
    public float4 CoreColor;
    public float4 FlowColor;
    public float4 StormColor;
    public float4 ContactColor;
    #endregion
}
