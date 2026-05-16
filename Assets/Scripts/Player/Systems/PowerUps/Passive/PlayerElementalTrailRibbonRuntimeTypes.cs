using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Stores one sampled point used by the managed Elemental Trail ribbon mesh.
/// /params None.
/// /returns None.
/// </summary>
internal struct PlayerElementalTrailRibbonPoint
{
    public float3 Position;
    public float AgeSeconds;
}

/// <summary>
/// Stores visual settings read from the authored TrailRenderer prefab template.
/// /params None.
/// /returns None.
/// </summary>
internal struct PlayerElementalTrailRibbonTemplate
{
    public Material SourceMaterial;
    public Gradient ColorGradient;
    public AnimationCurve WidthCurve;
    public float LifetimeSeconds;
    public float MinimumSampleDistance;
    public Vector2 TextureScale;
    public int SortingLayerId;
    public int SortingOrder;
    public int Layer;
}

/// <summary>
/// Holds managed objects and reusable buffers for one player-owned Elemental Trail ribbon mesh.
/// /params None.
/// /returns None.
/// </summary>
internal sealed class PlayerElementalTrailRibbonInstance
{
    #region Fields
    public GameObject SourcePrefab;
    public GameObject InstanceObject;
    public Transform InstanceTransform;
    public Mesh Mesh;
    public MeshFilter MeshFilter;
    public MeshRenderer MeshRenderer;
    public Material MaterialInstance;
    public PlayerElementalTrailRibbonTemplate Template;
    public bool WasEmitting;

    public readonly List<PlayerElementalTrailRibbonPoint> Points = new List<PlayerElementalTrailRibbonPoint>(128);
    public readonly List<Vector3> Vertices = new List<Vector3>(256);
    public readonly List<int> Triangles = new List<int>(768);
    public readonly List<Color32> Colors = new List<Color32>(256);
    public readonly List<Vector2> Uvs = new List<Vector2>(256);
    #endregion
}
