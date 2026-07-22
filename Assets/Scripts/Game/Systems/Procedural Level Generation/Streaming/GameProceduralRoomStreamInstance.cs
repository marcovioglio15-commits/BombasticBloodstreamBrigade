using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

/// <summary>
/// Identifies the lifecycle state of one logical room instance independently from its reusable scene asset.
/// </summary>
internal enum GameProceduralRoomStreamState : byte
{
    LoadingManagedScene = 0,
    LoadingEntityScenes = 1,
    Staging = 2,
    Ready = 3,
    Active = 4,
    Retired = 5,
    Unloading = 6,
    Released = 7,
    Failed = 8
}

/// <summary>
/// Stores the authored active-space position of one managed scene root moved into the staging slot.
/// </summary>
internal readonly struct GameProceduralRoomManagedRootPose
{
    #region Fields
    public readonly Transform Root;
    public readonly Vector3 ActivePosition;
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Captures one managed root and its authored active-space position before staging.
    /// </summary>
    /// <param name="root">Managed scene root transform.</param>
    /// <param name="activePosition">Authored active-space position.</param>
    public GameProceduralRoomManagedRootPose(Transform root, Vector3 activePosition)
    {
        Root = root;
        ActivePosition = activePosition;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores the authored active-space position of one root entity owned by an exact DOTS scene section instance.
/// </summary>
internal readonly struct GameProceduralRoomEntityRootPose
{
    #region Fields
    public readonly Entity Root;
    public readonly float3 ActivePosition;
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Captures one entity root and its authored active-space position before staging.
    /// </summary>
    /// <param name="root">Root entity owned by the room instance.</param>
    /// <param name="activePosition">Authored active-space position.</param>
    public GameProceduralRoomEntityRootPose(Entity root, float3 activePosition)
    {
        Root = root;
        ActivePosition = activePosition;
    }
    #endregion

    #endregion
}

/// <summary>
/// Owns exact managed and DOTS scene handles for one generated graph node, including duplicate scene templates.
/// </summary>
internal sealed class GameProceduralRoomStreamInstance
{
    #region Fields
    public readonly int NodeIndex;
    public readonly int StagingSlotIndex;
    public readonly ulong GenerationKey;
    public readonly bool UsesSpatialStaging;
    public readonly GameSceneDefinitionElement SceneDefinition;
    public readonly List<Entity> EntitySceneHandles = new List<Entity>(2);
    public readonly List<Entity> SectionEntities = new List<Entity>(4);
    public readonly List<GameProceduralRoomManagedRootPose> ManagedRootPoses = new List<GameProceduralRoomManagedRootPose>(16);
    public readonly List<GameProceduralRoomEntityRootPose> EntityRootPoses = new List<GameProceduralRoomEntityRootPose>(64);
    public readonly HashSet<int> SceneHandlesBeforeLoad = new HashSet<int>();
    public float3 ActivePlacementOffset;
    public AsyncOperation ManagedOperation;
    public AsyncOperationHandle<SceneInstance> AddressablesLoadOperation;
    public AsyncOperationHandle<SceneInstance> AddressablesUnloadOperation;
    public Scene ManagedScene;
    public GameProceduralRoomStreamState State;
    public double RetiredAtUnscaledTime;
    public bool UsesAddressables;
    public bool EntityLoadStarted;
    public bool EntityUnloadStarted;
    public bool RetireWhenReady;
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates ownership state for one logical node before its managed scene operation starts.
    /// </summary>
    /// <param name="generationKey">Stable run and level generation identity.</param>
    /// <param name="nodeIndex">Generated graph node index.</param>
    /// <param name="stagingSlotIndex">Unique runtime slot used to spatially isolate this exact instance.</param>
    /// <param name="sceneDefinition">Reusable managed room scene definition.</param>
    /// <param name="usesSpatialStaging">True when the optional dual-slot mode must isolate the instance off-world.</param>
    public GameProceduralRoomStreamInstance(ulong generationKey,
                                            int nodeIndex,
                                            int stagingSlotIndex,
                                            GameSceneDefinitionElement sceneDefinition,
                                            bool usesSpatialStaging)
    {
        GenerationKey = generationKey;
        NodeIndex = nodeIndex;
        StagingSlotIndex = stagingSlotIndex;
        SceneDefinition = sceneDefinition;
        UsesSpatialStaging = usesSpatialStaging;
        State = GameProceduralRoomStreamState.LoadingManagedScene;
    }
    #endregion

    #endregion
}
