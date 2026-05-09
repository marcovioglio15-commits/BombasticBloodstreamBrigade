using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns Addressables scene load handles so additive scenes can be unloaded and released through Addressables.
/// /params None.
/// /returns None.
/// </summary>
internal static class GameSceneAddressablesRuntimeUtility
{
    #region Fields
    private static Dictionary<string, AsyncOperationHandle<SceneInstance>> loadedSceneHandlesBySceneId;
    #endregion

    #region Methods

    #region Load
    /// <summary>
    /// Starts loading one scene through Addressables using the authored addressable key.
    /// /params sceneDefinition Scene definition being loaded.
    /// /params operationState Mutable operation state receiving the Addressables handle.
    /// /returns True when an Addressables operation was started.
    /// </summary>
    public static bool TryStartLoad(GameSceneDefinitionElement sceneDefinition, ref GameSceneSceneOperationState operationState)
    {
        string loadKey = ResolveLoadKey(sceneDefinition);

        if (string.IsNullOrWhiteSpace(loadKey))
        {
            Debug.LogWarning("[GameSceneManager] Addressables scene has no key: " + sceneDefinition.SceneId.ToString() + ".");
            return false;
        }

        try
        {
            AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(loadKey, LoadSceneMode.Additive, true);
            operationState = GameSceneSceneOperationState.FromAddressablesLoad(sceneDefinition.SceneId, handle);
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[GameSceneManager] Addressables failed to start scene load " + sceneDefinition.SceneId.ToString() + ": " + exception.Message);
            operationState.Clear();
            return false;
        }
    }

    /// <summary>
    /// Finalizes an Addressables load operation and stores its handle for future unload/release.
    /// /params sceneDefinition Scene definition that completed loading.
    /// /params handle Completed Addressables scene load handle.
    /// /params setActiveScene True when the loaded scene should become Unity's active scene.
    /// /returns True when the scene loaded successfully.
    /// </summary>
    public static bool CompleteLoad(GameSceneDefinitionElement sceneDefinition,
                                    AsyncOperationHandle<SceneInstance> handle,
                                    bool setActiveScene)
    {
        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogWarning("[GameSceneManager] Addressables failed to load scene: " + sceneDefinition.SceneId.ToString() + ".");
            ReleaseFailedLoad(handle);
            return false;
        }

        EnsureHandleDictionary();
        loadedSceneHandlesBySceneId[sceneDefinition.SceneId.ToString()] = handle;

        if (setActiveScene)
            TrySetSceneActive(handle.Result.Scene);

        return true;
    }
    #endregion

    #region Unload
    /// <summary>
    /// Starts unloading a previously Addressables-loaded scene and transfers release ownership to Addressables.
    /// /params sceneDefinition Scene definition being unloaded.
    /// /params operationState Mutable operation state receiving the Addressables unload handle.
    /// /returns True when an Addressables unload operation was started.
    /// </summary>
    public static bool TryStartUnload(GameSceneDefinitionElement sceneDefinition, ref GameSceneSceneOperationState operationState)
    {
        if (!TryGetLoadedHandle(sceneDefinition.SceneId, out AsyncOperationHandle<SceneInstance> handle))
            return false;

        AsyncOperationHandle<SceneInstance> unloadHandle = Addressables.UnloadSceneAsync(handle, false);
        operationState = GameSceneSceneOperationState.FromAddressablesUnload(sceneDefinition.SceneId, unloadHandle);
        return true;
    }

    /// <summary>
    /// Finalizes an Addressables unload operation and clears the cached load handle.
    /// /params sceneId Stable Scene Manager scene ID that was unloaded.
    /// /params handle Completed Addressables scene unload handle.
    /// /returns True when the unload operation completed successfully.
    /// </summary>
    public static bool CompleteUnload(FixedString64Bytes sceneId, AsyncOperationHandle<SceneInstance> handle)
    {
        string sceneIdText = sceneId.ToString();

        if (loadedSceneHandlesBySceneId != null)
            loadedSceneHandlesBySceneId.Remove(sceneIdText);

        bool succeeded = handle.Status == AsyncOperationStatus.Succeeded;

        if (handle.IsValid())
            Addressables.Release(handle);

        if (succeeded)
            return true;

        Debug.LogWarning("[GameSceneManager] Addressables failed to unload scene: " + sceneIdText + ".");
        return false;
    }
    #endregion

    #region Lookup
    /// <summary>
    /// Resolves whether the scene has an active Addressables load handle.
    /// /params sceneId Stable Scene Manager scene ID.
    /// /params handle Cached load handle when available.
    /// /returns True when an active Addressables handle is known for the scene.
    /// </summary>
    public static bool TryGetLoadedHandle(FixedString64Bytes sceneId, out AsyncOperationHandle<SceneInstance> handle)
    {
        handle = default;

        if (sceneId.Length <= 0 || loadedSceneHandlesBySceneId == null)
            return false;

        return loadedSceneHandlesBySceneId.TryGetValue(sceneId.ToString(), out handle) && handle.IsValid();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the Addressables key used for scene loading.
    /// /params sceneDefinition Scene definition being loaded.
    /// /returns Authored Addressables key or an empty string when missing.
    /// </summary>
    private static string ResolveLoadKey(GameSceneDefinitionElement sceneDefinition)
    {
        string addressableKey = sceneDefinition.AddressableKey.ToString();

        if (!string.IsNullOrWhiteSpace(addressableKey))
            return addressableKey;

        return string.Empty;
    }

    /// <summary>
    /// Sets a loaded scene active when Unity reports a valid scene instance.
    /// /params scene Loaded Unity scene.
    /// /returns None.
    /// </summary>
    private static void TrySetSceneActive(Scene scene)
    {
        if (scene.IsValid() && scene.isLoaded)
            SceneManager.SetActiveScene(scene);
    }

    /// <summary>
    /// Releases a failed Addressables load handle when it is still valid.
    /// /params handle Failed Addressables scene load handle.
    /// /returns None.
    /// </summary>
    private static void ReleaseFailedLoad(AsyncOperationHandle<SceneInstance> handle)
    {
        if (handle.IsValid())
            Addressables.Release(handle);
    }

    /// <summary>
    /// Creates the handle dictionary on first use.
    /// /params None.
    /// /returns None.
    /// </summary>
    private static void EnsureHandleDictionary()
    {
        if (loadedSceneHandlesBySceneId != null)
            return;

        loadedSceneHandlesBySceneId = new Dictionary<string, AsyncOperationHandle<SceneInstance>>(8);
    }
    #endregion

    #endregion
}
