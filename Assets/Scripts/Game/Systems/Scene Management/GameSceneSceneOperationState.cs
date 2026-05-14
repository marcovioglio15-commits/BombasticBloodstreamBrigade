using Unity.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

/// <summary>
/// Identifies the active managed scene operation type owned by the transition executor.
/// /params None.
/// /returns None.
/// </summary>
internal enum GameSceneSceneOperationKind : byte
{
    None = 0,
    SceneManager = 1,
    AddressablesLoad = 2,
    AddressablesUnload = 3
}

/// <summary>
/// Stores one in-flight Unity scene operation, including Addressables handles that must be released correctly.
/// /params None.
/// /returns None.
/// </summary>
internal struct GameSceneSceneOperationState
{
    #region Fields
    public AsyncOperation SceneManagerOperation;
    public AsyncOperationHandle<SceneInstance> AddressablesOperation;
    public FixedString64Bytes AddressablesSceneId;
    public GameSceneSceneOperationKind OperationKind;
    #endregion

    #region Properties
    public bool IsRunning
    {
        get
        {
            return OperationKind != GameSceneSceneOperationKind.None;
        }
    }

    public bool IsDone
    {
        get
        {
            switch (OperationKind)
            {
                case GameSceneSceneOperationKind.SceneManager:
                    return SceneManagerOperation == null || SceneManagerOperation.isDone;
                case GameSceneSceneOperationKind.AddressablesLoad:
                case GameSceneSceneOperationKind.AddressablesUnload:
                    return AddressablesOperation.IsDone;
                default:
                    return true;
            }
        }
    }

    public float Progress
    {
        get
        {
            switch (OperationKind)
            {
                case GameSceneSceneOperationKind.SceneManager:
                    return SceneManagerOperation != null ? Mathf.Clamp01(SceneManagerOperation.progress) : 1f;
                case GameSceneSceneOperationKind.AddressablesLoad:
                case GameSceneSceneOperationKind.AddressablesUnload:
                    return AddressablesOperation.IsValid() ? Mathf.Clamp01(AddressablesOperation.PercentComplete) : 1f;
                default:
                    return 1f;
            }
        }
    }
    #endregion

    #region Methods

    #region Factory
    /// <summary>
    /// Builds operation state for a regular SceneManager async operation.
    /// /params operation Unity async operation returned by SceneManager.
    /// /returns Scene operation state.
    /// </summary>
    public static GameSceneSceneOperationState FromSceneManager(AsyncOperation operation)
    {
        return new GameSceneSceneOperationState
        {
            SceneManagerOperation = operation,
            OperationKind = GameSceneSceneOperationKind.SceneManager
        };
    }

    /// <summary>
    /// Builds operation state for an Addressables scene load handle.
    /// /params sceneId Stable Scene Manager scene ID being loaded.
    /// /params operation Addressables scene load operation.
    /// /returns Scene operation state.
    /// </summary>
    public static GameSceneSceneOperationState FromAddressablesLoad(FixedString64Bytes sceneId,
                                                                    AsyncOperationHandle<SceneInstance> operation)
    {
        return new GameSceneSceneOperationState
        {
            AddressablesSceneId = sceneId,
            AddressablesOperation = operation,
            OperationKind = GameSceneSceneOperationKind.AddressablesLoad
        };
    }

    /// <summary>
    /// Builds operation state for an Addressables scene unload handle.
    /// /params sceneId Stable Scene Manager scene ID being unloaded.
    /// /params operation Addressables scene unload operation.
    /// /returns Scene operation state.
    /// </summary>
    public static GameSceneSceneOperationState FromAddressablesUnload(FixedString64Bytes sceneId,
                                                                      AsyncOperationHandle<SceneInstance> operation)
    {
        return new GameSceneSceneOperationState
        {
            AddressablesSceneId = sceneId,
            AddressablesOperation = operation,
            OperationKind = GameSceneSceneOperationKind.AddressablesUnload
        };
    }
    #endregion

    #region Lifecycle
    /// <summary>
    /// Clears operation references after completion or transition shutdown.
    /// /params None.
    /// /returns None.
    /// </summary>
    public void Clear()
    {
        SceneManagerOperation = null;
        AddressablesOperation = default;
        AddressablesSceneId = default;
        OperationKind = GameSceneSceneOperationKind.None;
    }
    #endregion

    #endregion
}
