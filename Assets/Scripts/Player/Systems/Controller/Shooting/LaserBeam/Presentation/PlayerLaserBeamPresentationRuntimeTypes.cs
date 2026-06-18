using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Stores one pooled root instance that owns all managed visuals for a single player beam.
/// </summary>
internal sealed class PlayerLaserBeamManagedInstance
{
    #region Fields
    public GameObject RootObject;
    public Transform RootTransform;
    public float ShutdownTailRemainingSeconds;
    public float ShutdownTailLastFadeNormalized;
    public float3 ShutdownTailAnchorPoint;
    public float3 ShutdownTailDirection;
    public byte ShutdownTailActive;
    public byte ShutdownTailAnchorInitialized;
    public readonly List<PlayerLaserBeamManagedBodyVisual> BodyVisuals = new List<PlayerLaserBeamManagedBodyVisual>(16);
    public readonly List<PlayerLaserBeamManagedParticleVisual> SourceVisuals = new List<PlayerLaserBeamManagedParticleVisual>(8);
    public readonly List<PlayerLaserBeamManagedParticleVisual> TerminalCapVisuals = new List<PlayerLaserBeamManagedParticleVisual>(8);
    public readonly List<PlayerLaserBeamManagedParticleVisual> ContactFlareVisuals = new List<PlayerLaserBeamManagedParticleVisual>(8);
    #endregion
}

/// <summary>
/// Stores one pooled mesh-based body ribbon visual instance.
/// </summary>
internal sealed class PlayerLaserBeamManagedBodyVisual
{
    #region Fields
    public GameObject InstanceObject;
    public Transform RootTransform;
    public Mesh DynamicMesh;
    public readonly List<PlayerLaserBeamManagedBodyLayerVisual> LayerVisuals = new List<PlayerLaserBeamManagedBodyLayerVisual>(3);
    #endregion
}

/// <summary>
/// Stores one pooled mesh-renderer layer that shares the lane body mesh but renders a different visual role.
/// </summary>
internal sealed class PlayerLaserBeamManagedBodyLayerVisual
{
    #region Fields
    public GameObject InstanceObject;
    public Transform RootTransform;
    public MeshFilter MeshFilter;
    public MeshRenderer MeshRenderer;
    public PlayerLaserBeamBodyLayerRole LayerRole;
    #endregion
}

/// <summary>
/// Stores one pooled particle visual instance used for the beam source or impact.
/// </summary>
internal sealed class PlayerLaserBeamManagedParticleVisual
{
    #region Fields
    public GameObject SourcePrefab;
    public GameObject InstanceObject;
    public Transform RootTransform;
    public ParticleSystem[] ParticleSystems;
    public ParticleSystemRenderer[] Renderers;
    #endregion
}

/// <summary>
/// Stores one sampled ribbon point derived from the authoritative gameplay lanes.
/// </summary>
internal struct PlayerLaserBeamRibbonPoint
{
    #region Fields
    public float3 Position;
    public float Distance;
    public float Width;
    public byte SmoothingLock;
    #endregion
}

/// <summary>
/// Stores one render-time ribbon lane built from the authoritative gameplay lanes.
/// </summary>
internal struct PlayerLaserBeamLaneVisual
{
    #region Fields
    public int LaneIndex;
    public int PointStartIndex;
    public int PointCount;
    public float TotalLength;
    public float3 StartDirection;
    public float StartWidth;
    public float3 EndDirection;
    public float EndWidth;
    public float3 TerminalNormal;
    public byte TerminalBlockedByWall;
    #endregion
}

/// <summary>
/// Stores the render-time start and end anchors of one beam lane.
/// </summary>
internal struct PlayerLaserBeamLaneEndpoint
{
    #region Fields
    public int LaneIndex;
    public float3 MuzzlePoint;
    public float3 VisibleStartPoint;
    public float3 StartDirection;
    public float StartWidth;
    public float3 EndPoint;
    public float3 EndDirection;
    public float EndWidth;
    public float3 TerminalNormal;
    public byte TerminalBlockedByWall;
    #endregion
}

/// <summary>
/// Stores the resolved managed flow and storm colors used by body and particle visuals.
/// </summary>
internal struct PlayerLaserBeamResolvedPalette
{
    #region Fields
    public Color CoreColor;
    public Color FlowColor;
    public Color StormColor;
    public Color ContactColor;
    #endregion
}

/// <summary>
/// Identifies the shared mesh-renderer layer used to draw one portion of the beam body.
/// </summary>
internal enum PlayerLaserBeamBodyLayerRole : byte
{
    Core = 0,
    Flow = 1,
    Storm = 2
}

/// <summary>
/// Identifies the endpoint visual role rendered by the pooled particle prefabs.
/// </summary>
internal enum PlayerLaserBeamEndpointVisualRole : byte
{
    Source = 1,
    TerminalCap = 2,
    ContactFlare = 3
}
