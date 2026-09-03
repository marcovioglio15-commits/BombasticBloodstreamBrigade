using Unity.Entities;
using Unity.Mathematics;

#region Recorder Camera Configuration
/// <summary>
/// Stores one immutable scene-authored recorder viewpoint. The persistent gameplay camera consumes this data so the
/// existing URP stack, UI overlays, listeners and transition bridges remain attached to the render owner.
/// </summary>
public struct GameRecorderCamera : IComponentData
{
    public float3 WorldPosition;
    public float3 WorldForward;
    public float3 WorldUp;
    public float FieldOfView;
    public float OrthographicSize;
    public float NearClipPlane;
    public float FarClipPlane;
    public int CycleOrder;
    public byte Orthographic;
    public byte AlignMovementToCamera;
}
#endregion

#region Recorder Camera Runtime State
/// <summary>
/// Publishes the recorder viewpoint currently overriding normal player-camera movement. Entity.Null means the
/// persistent gameplay camera is controlled by the configured player camera behavior.
/// </summary>
public struct GameRecorderCameraRuntimeState : IComponentData
{
    public Entity ActiveCameraEntity;
}
#endregion
