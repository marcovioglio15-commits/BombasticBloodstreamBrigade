using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Owns exact duplicate-capable managed scene operations for transactional procedural room instances.
/// </summary>
internal static class GameProceduralRoomManagedSceneUtility
{
    #region Methods

    #region Load
    /// <summary>
    /// Starts one managed room load with the configured backend and retains its exact instance handle.
    /// </summary>
    /// <param name="instance">Logical room instance receiving the managed scene.</param>
    /// <param name="loadBackend">Configured Scene Manager backend.</param>
    /// <returns>True when the selected backend accepted the asynchronous request.</returns>
    public static bool StartLoad(GameProceduralRoomStreamInstance instance, GameSceneLoadBackend loadBackend)
    {
        if (loadBackend == GameSceneLoadBackend.Addressables)
            return StartAddressablesLoad(instance);

        return StartSceneManagerLoad(instance);
    }

    /// <summary>
    /// Advances callback-driven managed loads when completion was delivered before listener registration or domain reload.
    /// </summary>
    /// <param name="instance">Logical room instance waiting for managed completion.</param>
    public static void TickLoad(GameProceduralRoomStreamInstance instance)
    {
        if (instance.ManagedScene.IsValid())
            instance.State = GameProceduralRoomStreamState.LoadingEntityScenes;
    }

    /// <summary>
    /// Starts one exact Addressables room load and retains its handle for matching unload ownership.
    /// </summary>
    /// <param name="instance">Logical room instance receiving the load handle.</param>
    /// <returns>True when Addressables accepted the request.</returns>
    private static bool StartAddressablesLoad(GameProceduralRoomStreamInstance instance)
    {
        string addressableKey = instance.SceneDefinition.AddressableKey.ToString();

        if (string.IsNullOrWhiteSpace(addressableKey))
            return MarkLoadFailed(instance, "The room scene has no Addressables key.");

        try
        {
            instance.UsesAddressables = true;
            instance.AddressablesLoadOperation = Addressables.LoadSceneAsync(addressableKey, LoadSceneMode.Additive, true);
            instance.AddressablesLoadOperation.Completed += operation => CompleteAddressablesLoad(instance, operation);
            return true;
        }
        catch (Exception exception)
        {
            return MarkLoadFailed(instance, exception.Message);
        }
    }

    /// <summary>
    /// Starts one additive managed scene load while preserving existing handles for duplicate template resolution.
    /// </summary>
    /// <param name="instance">Logical room instance receiving the load operation.</param>
    /// <returns>True when Unity accepted the request.</returns>
    private static bool StartSceneManagerLoad(GameProceduralRoomStreamInstance instance)
    {
        CaptureLoadedSceneHandles(instance);
        string scenePath = instance.SceneDefinition.ScenePath.ToString();

        if (string.IsNullOrWhiteSpace(scenePath))
            return MarkLoadFailed(instance, "The room scene has no project path.");

#if UNITY_EDITOR
        instance.ManagedOperation = EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath,
                                                                                new LoadSceneParameters(LoadSceneMode.Additive));
#else
        instance.ManagedOperation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
#endif

        if (instance.ManagedOperation == null)
            return MarkLoadFailed(instance, "Unity did not create an additive scene operation.");

        instance.ManagedOperation.completed += operation => CompleteSceneManagerLoad(instance);
        return true;
    }

    /// <summary>
    /// Applies exact Addressables completion data and stages managed roots before the next rendered frame.
    /// </summary>
    /// <param name="instance">Logical room instance that owns the operation.</param>
    /// <param name="operation">Completed Addressables load handle.</param>
    private static void CompleteAddressablesLoad(GameProceduralRoomStreamInstance instance,
                                                 AsyncOperationHandle<SceneInstance> operation)
    {
        if (operation.Status != AsyncOperationStatus.Succeeded)
        {
            MarkLoadFailed(instance, "Addressables failed to load the room instance.");
            return;
        }

        AttachManagedScene(instance, operation.Result.Scene);
    }

    /// <summary>
    /// Resolves the newly created Unity scene handle after a duplicate-capable SceneManager load completes.
    /// </summary>
    /// <param name="instance">Logical room instance that owns the completed operation.</param>
    private static void CompleteSceneManagerLoad(GameProceduralRoomStreamInstance instance)
    {
        string expectedPath = instance.SceneDefinition.ScenePath.ToString();

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene candidate = SceneManager.GetSceneAt(sceneIndex);

            if (instance.SceneHandlesBeforeLoad.Contains(candidate.handle) ||
                !string.Equals(candidate.path, expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AttachManagedScene(instance, candidate);
            return;
        }

        MarkLoadFailed(instance, "The completed additive operation did not expose a new exact scene handle.");
    }

    /// <summary>
    /// Captures an exact managed scene and isolates its roots only for the optional concurrent dual-slot mode.
    /// </summary>
    /// <param name="instance">Logical room instance receiving the scene.</param>
    /// <param name="scene">Exact loaded Unity scene.</param>
    private static void AttachManagedScene(GameProceduralRoomStreamInstance instance, Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            MarkLoadFailed(instance, "Unity returned an invalid managed room scene.");
            return;
        }

        instance.ManagedScene = scene;
        GameProceduralRoomPlacementUtility.StageManagedRoots(instance);
        instance.State = GameProceduralRoomStreamState.LoadingEntityScenes;
    }
    #endregion

    #region Unload
    /// <summary>
    /// Starts unloading the exact managed scene handle owned by one retired room instance.
    /// </summary>
    /// <param name="instance">Retired logical room instance.</param>
    public static void StartUnload(GameProceduralRoomStreamInstance instance)
    {
        if (!instance.ManagedScene.IsValid() || !instance.ManagedScene.isLoaded)
            return;

        if (instance.UsesAddressables && instance.AddressablesLoadOperation.IsValid())
            instance.AddressablesUnloadOperation = Addressables.UnloadSceneAsync(instance.AddressablesLoadOperation, false);
        else
            instance.ManagedOperation = SceneManager.UnloadSceneAsync(instance.ManagedScene);
    }

    /// <summary>
    /// Checks whether the exact managed scene unload operation has completed.
    /// </summary>
    /// <param name="instance">Unloading logical room instance.</param>
    /// <returns>True when its managed scene no longer blocks registry release.</returns>
    public static bool IsUnloadComplete(GameProceduralRoomStreamInstance instance)
    {
        if (!instance.ManagedScene.IsValid() || !instance.ManagedScene.isLoaded)
            return true;

        if (instance.UsesAddressables)
            return instance.AddressablesUnloadOperation.IsValid() && instance.AddressablesUnloadOperation.IsDone;

        return instance.ManagedOperation == null || instance.ManagedOperation.isDone;
    }

    /// <summary>
    /// Releases the completed Addressables unload operation while leaving SceneManager operations to Unity.
    /// </summary>
    /// <param name="instance">Released logical room instance.</param>
    public static void ReleaseUnload(GameProceduralRoomStreamInstance instance)
    {
        if (instance.AddressablesUnloadOperation.IsValid())
            Addressables.Release(instance.AddressablesUnloadOperation);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Captures currently loaded Unity scene handles before a duplicate-capable additive load starts.
    /// </summary>
    /// <param name="instance">Logical room instance receiving existing scene handles.</param>
    private static void CaptureLoadedSceneHandles(GameProceduralRoomStreamInstance instance)
    {
        instance.SceneHandlesBeforeLoad.Clear();

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            instance.SceneHandlesBeforeLoad.Add(SceneManager.GetSceneAt(sceneIndex).handle);
    }

    /// <summary>
    /// Records a failed instance and emits one actionable runtime diagnostic without modifying authored settings.
    /// </summary>
    /// <param name="instance">Logical room instance that failed.</param>
    /// <param name="message">Actionable failure detail.</param>
    /// <returns>Always false for direct propagation by load helpers.</returns>
    private static bool MarkLoadFailed(GameProceduralRoomStreamInstance instance, string message)
    {
        instance.State = GameProceduralRoomStreamState.Failed;
        Debug.LogError("[ProceduralRoomStreaming] Node " + instance.NodeIndex + " failed: " + message);
        return false;
    }
    #endregion

    #endregion
}
