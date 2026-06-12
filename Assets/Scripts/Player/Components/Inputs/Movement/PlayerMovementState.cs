using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// This component represents the movement state of a player entity, 
/// including the desired movement direction, current velocity, and digital input states for movement.
/// It also includes timing information for input presses and release stabilization, 
/// allowing for responsive and smooth player movement based on input.
/// </summary>
public struct PlayerMovementState : IComponentData
{
    public float3 DesiredDirection; // The desired movement direction based on player input.
    public float3 Velocity; // Current velocity of the player.
    public byte PrevMoveMask; // Previous digital move mask.
    public byte CurrMoveMask; // Current digital move mask.
    public float4 MovePressTimes; // Timestamp per digital direction (Up, Down, Left, Right).
    public byte ReleaseHoldMask; // Last diagonal mask held during release stabilization.
    public float ReleaseHoldUntilTime; // Time (seconds) until the release stabilization ends.
}
