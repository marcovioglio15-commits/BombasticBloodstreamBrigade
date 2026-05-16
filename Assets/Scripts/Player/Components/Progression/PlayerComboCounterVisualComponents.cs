using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Stores one baked combo-rank HUD visual theme resolved from the progression preset.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerComboRankVisualElement : IBufferElementData
{
    #region Fields
    public UnityObjectRef<Sprite> BadgeSprite;
    public float4 BadgeTint;
    public float4 RankTextColor;
    public float4 ComboValueTextColor;
    public float4 ProgressFillColor;
    public float4 ProgressBackgroundColor;
    #endregion
}
