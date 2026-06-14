using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Stores the baked configuration of the precision aiming laser pointer rendered straight out of the player weapon.
/// The pointer reuses the Laser Beam body material and palette colors so it stays visually consistent with the Laser Beam power-up.
/// </summary>
public struct PlayerVisualPointerConfig : IComponentData
{
    #region Fields
    public byte Enabled;
    public UnityObjectRef<Material> BodyMaterial;
    public float4 CoreColor;
    public float4 FlowColor;
    public float4 StormColor;
    public float4 ContactColor;
    public float Width;
    public float LengthMultiplier;
    public float MaxLength;
    public float Opacity;
    public float VerticalLift;
    public byte FreezeWithOrbitalProjectiles;
    public float OrbitalFrozenLength;
    public float BaseStraightLength;
    #endregion
}
