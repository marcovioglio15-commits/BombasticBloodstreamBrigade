using Unity.Mathematics;

/// <summary>
/// Small bake-time color palette used to tint enemy death debris from the enemy's authored visual colors.
/// </summary>
public struct EnemyDeathDebrisColorPalette
{
    public float4 PrimaryColor;
    public float4 SecondaryColor;
    public byte ColorCount;
}
