using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Represents player input state including movement, looking direction, action triggers, and vector source metadata.
/// </summary>
public struct PlayerInputState : IComponentData
{
    public float2 Move; // Movement input vector (e.g., from joystick or WASD keys).
    public float2 Look; // Look input vector (e.g., right joystick). Mouse-pointer look is resolved separately at runtime.
    public byte MoveUsesAnalogSource; // Non-zero when the current movement vector came from an analog stick-like control.
    public byte LookUsesAnalogSource; // Non-zero when the current look vector came from an analog stick-like control.
    public byte PointerLookBlocked; // Non-zero until mouse-pointer look receives a fresh post-transition movement.
    public float Shoot; // Shooting trigger value (0 = idle, 1 = pressed).
    public float PowerUpPrimary; // Primary active-tool trigger value.
    public float PowerUpSecondary; // Secondary active-tool trigger value.
    public float SwapPowerUpSlots; // Active-slot swap trigger value.
}
