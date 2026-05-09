using UnityEngine;

/// <summary>
/// Defines one Unity scene known to the Game Scene Manager.
/// /params None.
/// /returns None.
/// </summary>
[System.Serializable]
public sealed class GameSceneDefinition
{
    #region Fields

    #region Serialized Fields
    [Header("Identity")]
    [Tooltip("Stable scene ID used by UI commands, transition definitions and trigger volumes.")]
    [SerializeField] private string sceneId;

    [Tooltip("Human-readable scene name shown in tools and used as a fallback load key.")]
    [SerializeField] private string sceneName;

    [Tooltip("Project-relative path to the Unity scene asset.")]
    [SerializeField] private string scenePath;

    [Tooltip("Asset GUID of the Unity scene. The editor tool keeps it synchronized with Scene Path.")]
    [SerializeField] private string sceneGuid;

    [Tooltip("Build Settings index resolved by editor tooling. Runtime uses it when available for fast loading.")]
    [SerializeField] private int buildIndex = -1;

    [Tooltip("High-level scene role used by validation and transition policies.")]
    [SerializeField] private GameSceneKind sceneKind = GameSceneKind.Gameplay;

    [Tooltip("Determines whether this scene is unloaded automatically when another scene becomes active.")]
    [SerializeField] private GameSceneUnloadPolicy unloadPolicy = GameSceneUnloadPolicy.UnloadOnTransition;

    [Tooltip("Optional PersistentUi scene ID loaded additively with this scene and unloaded with it when its policy allows.")]
    [SerializeField] private string companionUiSceneId;

    [Tooltip("Optional tags reserved for future room and level management filtering.")]
    [SerializeField] private string roomTags;

    [Tooltip("Addressables key preferred by the Addressables backend. Build Settings metadata is only required when the Build Settings backend owns the scene.")]
    [SerializeField] private string addressableKey;

#if UNITY_EDITOR
    [Tooltip("Editor-only scene asset used by the Game Management Tool to synchronize path, GUID and name.")]
    [SerializeField] private UnityEditor.SceneAsset sceneAsset;
#endif
    #endregion

    #endregion

    #region Properties
    public string SceneId
    {
        get
        {
            return sceneId;
        }
    }

    public string SceneName
    {
        get
        {
            return sceneName;
        }
    }

    public string ScenePath
    {
        get
        {
            return scenePath;
        }
    }

    public string SceneGuid
    {
        get
        {
            return sceneGuid;
        }
    }

    public int BuildIndex
    {
        get
        {
            return buildIndex;
        }
    }

    public GameSceneKind SceneKind
    {
        get
        {
            return sceneKind;
        }
    }

    public GameSceneUnloadPolicy UnloadPolicy
    {
        get
        {
            return unloadPolicy;
        }
    }

    public string CompanionUiSceneId
    {
        get
        {
            return companionUiSceneId;
        }
    }

    public string RoomTags
    {
        get
        {
            return roomTags;
        }
    }

    public string AddressableKey
    {
        get
        {
            return addressableKey;
        }
    }
    #endregion
}
