#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds dynamic linked-object and Animator-clip choices from portal anchors available to the editor catalog.
/// </summary>
internal static class GameRoomPortalLinkedObjectEditorCatalogUtility
{
    #region Fields
    private static readonly Dictionary<string, HashSet<string>> cachedNamesByIdentifier =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    private static readonly Dictionary<string, List<GameRoomPortalAnimatorClipCatalogEntry>> cachedClipsByIdentifier =
        new Dictionary<string, List<GameRoomPortalAnimatorClipCatalogEntry>>(StringComparer.Ordinal);
    private static bool cacheValid;
    private static bool buildingCache;
    #endregion

    #region Methods

    #region Initialization
    /// <summary>
    /// Registers editor change notifications that can alter scene bindings or available Animator clips.
    /// </summary>
    static GameRoomPortalLinkedObjectEditorCatalogUtility()
    {
        EditorApplication.projectChanged += Invalidate;
        Undo.undoRedoPerformed += Invalidate;
        Undo.postprocessModifications += InvalidateAfterModifications;
        EditorSceneManager.sceneSaved += InvalidateAfterSceneSaved;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Collects freely authored binding identifiers and readable scene-object labels.
    /// </summary>
    /// <returns>Immutable identifier values and dropdown labels for the current editor context.</returns>
    public static GameRoomPortalLinkedObjectChoiceCatalog Build()
    {
        EnsureProjectCatalog();
        List<string> identifiers = new List<string>(cachedNamesByIdentifier.Keys);
        identifiers.Sort(StringComparer.Ordinal);
        List<string> labels = new List<string>(identifiers.Count);

        for (int identifierIndex = 0;
             identifierIndex < identifiers.Count;
             identifierIndex++)
        {
            string identifier = identifiers[identifierIndex];
            List<string> names = new List<string>(cachedNamesByIdentifier[identifier]);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            labels.Add(string.Join(", ", names) + "  [" + identifier + "]");
        }

        return new GameRoomPortalLinkedObjectChoiceCatalog(identifiers, labels);
    }

    /// <summary>
    /// Collects unique clips exposed by Animators on one linked object or any of its children.
    /// </summary>
    /// <param name="bindingId">Stable linked-object identifier selected by the animation.</param>
    /// <returns>Matching clip assets, relative Animator paths and readable labels.</returns>
    public static GameRoomPortalAnimatorClipChoiceCatalog BuildAnimatorClips(string bindingId)
    {
        EnsureProjectCatalog();
        List<AnimationClip> clips = new List<AnimationClip>();
        List<string> paths = new List<string>();
        List<string> labels = new List<string>();

        if (!string.IsNullOrWhiteSpace(bindingId) &&
            cachedClipsByIdentifier.TryGetValue(bindingId,
                                                 out List<GameRoomPortalAnimatorClipCatalogEntry> entries))
        {
            // Copy immutable catalog entries so dropdown mutation never alters the shared cache.
            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                clips.Add(entries[entryIndex].Clip);
                paths.Add(entries[entryIndex].Path);
                labels.Add(entries[entryIndex].Label);
            }
        }

        return new GameRoomPortalAnimatorClipChoiceCatalog(clips, paths, labels);
    }

    /// <summary>
    /// Invalidates project-scene bindings so the next catalog build reflects saved authoring changes.
    /// </summary>
    public static void InvalidateCache()
    {
        Invalidate();
    }
    #endregion

    #region Catalog Build
    /// <summary>
    /// Builds one cached project-wide catalog from every scene that depends on the portal effect view script.
    /// </summary>
    private static void EnsureProjectCatalog()
    {
        if (cacheValid || buildingCache)
            return;

        buildingCache = true;
        cachedNamesByIdentifier.Clear();
        cachedClipsByIdentifier.Clear();

        try
        {
            HashSet<string> scannedScenePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Include unsaved changes in loaded scenes before consulting project-asset dependencies.
            for (int loadedSceneIndex = 0;
                 loadedSceneIndex < SceneManager.sceneCount;
                 loadedSceneIndex++)
            {
                Scene loadedScene = SceneManager.GetSceneAt(loadedSceneIndex);

                if (!loadedScene.IsValid() ||
                    !loadedScene.isLoaded ||
                    string.IsNullOrWhiteSpace(loadedScene.path))
                {
                    continue;
                }

                scannedScenePaths.Add(loadedScene.path);
                ScanScene(loadedScene.path);
            }

            List<string> candidateScenePaths =
                GameRoomPortalEditorSceneDependencyUtility.FindCandidateScenePaths(
                    scannedScenePaths);

            // Open only scenes with a direct anchor component or a referenced prefab containing one.
            for (int sceneIndex = 0; sceneIndex < candidateScenePaths.Count; sceneIndex++)
                ScanScene(candidateScenePaths[sceneIndex]);

            cacheValid = true;
        }
        finally
        {
            buildingCache = false;
        }
    }

    /// <summary>
    /// Scans one loaded or temporarily opened scene and restores editor scene state afterward.
    /// </summary>
    /// <param name="scenePath">Project-relative scene path to inspect.</param>
    private static void ScanScene(string scenePath)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedForScan = !scene.IsValid() || !scene.isLoaded;

        try
        {
            if (openedForScan)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            if (!scene.IsValid() || !scene.isLoaded)
                return;

            GameObject[] roots = scene.GetRootGameObjects();

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameRoomPortalRewardLogAnchor[] anchors =
                    roots[rootIndex].GetComponentsInChildren<GameRoomPortalRewardLogAnchor>(true);

                for (int anchorIndex = 0; anchorIndex < anchors.Length; anchorIndex++)
                    AddEffectView(anchors[anchorIndex].EffectView, scenePath);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[GameRoomPortalLinkedObjectEditorCatalogUtility] Could not inspect scene '" +
                             scenePath + "': " + exception.Message);
        }
        finally
        {
            if (openedForScan && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    /// <summary>
    /// Adds every valid linked-object binding and its Animator clips to the project cache.
    /// </summary>
    /// <param name="effectView">Scene effect view containing stable object bindings.</param>
    /// <param name="scenePath">Owning scene path used to disambiguate clip labels.</param>
    private static void AddEffectView(GameRoomPortalRewardEffectView effectView,
                                      string scenePath)
    {
        if (effectView == null)
            return;

        IReadOnlyList<GameRoomPortalLinkedObjectBinding> linkedObjects = effectView.LinkedObjects;

        for (int bindingIndex = 0; bindingIndex < linkedObjects.Count; bindingIndex++)
        {
            GameRoomPortalLinkedObjectBinding binding = linkedObjects[bindingIndex];

            if (binding == null ||
                string.IsNullOrWhiteSpace(binding.BindingId) ||
                binding.TargetObject == null)
            {
                continue;
            }

            if (!cachedNamesByIdentifier.TryGetValue(binding.BindingId,
                                                     out HashSet<string> names))
            {
                names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                cachedNamesByIdentifier.Add(binding.BindingId, names);
            }

            names.Add(string.IsNullOrWhiteSpace(binding.DisplayName)
                ? binding.TargetObject.name
                : binding.DisplayName.Trim());
            AddAnimatorClips(binding.BindingId,
                             binding.TargetObject.transform,
                             scenePath);
        }
    }

    /// <summary>
    /// Adds controller clips from every Animator below one linked object without duplicating choices.
    /// </summary>
    /// <param name="bindingId">Stable linked-object identifier owning the clip choices.</param>
    /// <param name="root">Linked scene-object root.</param>
    /// <param name="scenePath">Owning scene path used to disambiguate equivalent hierarchy labels.</param>
    private static void AddAnimatorClips(string bindingId,
                                         Transform root,
                                         string scenePath)
    {
        if (!cachedClipsByIdentifier.TryGetValue(bindingId,
                                                 out List<GameRoomPortalAnimatorClipCatalogEntry> entries))
        {
            entries = new List<GameRoomPortalAnimatorClipCatalogEntry>();
            cachedClipsByIdentifier.Add(bindingId, entries);
        }

        Animator[] animators = root.GetComponentsInChildren<Animator>(true);

        for (int animatorIndex = 0; animatorIndex < animators.Length; animatorIndex++)
        {
            Animator animator = animators[animatorIndex];
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;

            if (controller == null)
                continue;

            string path = ResolveRelativePath(root, animator.transform);
            AnimationClip[] controllerClips = controller.animationClips;

            for (int clipIndex = 0; clipIndex < controllerClips.Length; clipIndex++)
            {
                AnimationClip clip = controllerClips[clipIndex];

                if (clip == null)
                    continue;

                bool duplicate = false;

                for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    if (entries[entryIndex].Clip == clip &&
                        string.Equals(entries[entryIndex].Path, path, StringComparison.Ordinal))
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (duplicate)
                    continue;

                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                string hierarchyLabel = string.IsNullOrEmpty(path) ? "Root" : path;
                entries.Add(new GameRoomPortalAnimatorClipCatalogEntry(clip,
                                                                        path,
                                                                        hierarchyLabel + " — " + clip.name +
                                                                        "  [" + sceneName + "]"));
            }
        }
    }
    #endregion

    #region Cache Invalidation
    /// <summary>
    /// Invalidates the project catalog after a hierarchy, project or Undo change.
    /// </summary>
    private static void Invalidate()
    {
        if (buildingCache)
            return;

        cacheValid = false;
    }

    /// <summary>
    /// Invalidates after serialized editor modifications and preserves the original Undo payload.
    /// </summary>
    /// <param name="modifications">Undo property modifications emitted by the editor.</param>
    /// <returns>The unchanged modification array.</returns>
    private static UndoPropertyModification[] InvalidateAfterModifications(
        UndoPropertyModification[] modifications)
    {
        // Ignore unrelated preset edits so an open Portal Log does not rescan every project scene unnecessarily.
        for (int modificationIndex = 0;
             modificationIndex < modifications.Length;
             modificationIndex++)
        {
            UnityEngine.Object target = modifications[modificationIndex].currentValue.target;

            if (target is GameRoomPortalRewardEffectView ||
                target is GameRoomPortalRewardLogAnchor ||
                target is Animator ||
                target is GameObject ||
                target is Transform)
            {
                Invalidate();
                break;
            }
        }

        return modifications;
    }

    /// <summary>
    /// Invalidates after a scene asset is saved.
    /// </summary>
    /// <param name="scene">Saved scene.</param>
    private static void InvalidateAfterSceneSaved(Scene scene)
    {
        Invalidate();
    }
    #endregion

    #region Hierarchy Paths

    /// <summary>
    /// Builds a slash-delimited Transform path compatible with Transform.Find at runtime.
    /// </summary>
    /// <param name="root">Linked-object hierarchy root.</param>
    /// <param name="target">Animator Transform at or below the root.</param>
    /// <returns>Empty string for the root Animator, otherwise its relative hierarchy path.</returns>
    private static string ResolveRelativePath(Transform root, Transform target)
    {
        if (target == root)
            return string.Empty;

        List<string> names = new List<string>(4);
        Transform current = target;

        while (current != null && current != root)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one immutable project-scene Animator clip option for a linked portal object.
/// </summary>
internal readonly struct GameRoomPortalAnimatorClipCatalogEntry
{
    #region Properties
    public AnimationClip Clip { get; }
    public string Path { get; }
    public string Label { get; }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one exact clip, relative path and project-scene label tuple.
    /// </summary>
    /// <param name="clip">Animation clip asset exposed by the Animator controller.</param>
    /// <param name="path">Relative path from the linked object to the Animator.</param>
    /// <param name="label">Dropdown label including scene context.</param>
    public GameRoomPortalAnimatorClipCatalogEntry(AnimationClip clip,
                                                  string path,
                                                  string label)
    {
        Clip = clip;
        Path = path;
        Label = label;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores matching dynamic identifiers and readable labels for one linked-object dropdown context.
/// </summary>
internal readonly struct GameRoomPortalLinkedObjectChoiceCatalog
{
    #region Fields
    public readonly IReadOnlyList<string> Identifiers;
    public readonly IReadOnlyList<string> Labels;
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one immutable linked-object choice catalog.
    /// </summary>
    /// <param name="identifiers">Stable identifiers represented by dropdown indices.</param>
    /// <param name="labels">Readable labels matching the identifiers.</param>
    public GameRoomPortalLinkedObjectChoiceCatalog(IReadOnlyList<string> identifiers,
                                                   IReadOnlyList<string> labels)
    {
        Identifiers = identifiers;
        Labels = labels;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Finds one stable identifier in this dropdown catalog.
    /// </summary>
    /// <param name="identifier">Serialized binding identifier to resolve.</param>
    /// <returns>Matching dropdown index, or -1 when the catalog does not contain the identifier.</returns>
    public int IndexOf(string identifier)
    {
        for (int index = 0; index < Identifiers.Count; index++)
        {
            if (string.Equals(Identifiers[index], identifier, StringComparison.Ordinal))
                return index;
        }

        return -1;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores exact Animator paths and clip assets represented by one conditional dropdown.
/// </summary>
internal readonly struct GameRoomPortalAnimatorClipChoiceCatalog
{
    #region Fields
    public readonly IReadOnlyList<AnimationClip> Clips;
    public readonly IReadOnlyList<string> Paths;
    public readonly IReadOnlyList<string> Labels;
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one immutable Animator-clip choice catalog.
    /// </summary>
    /// <param name="clips">Clip assets represented by dropdown indices.</param>
    /// <param name="paths">Relative Animator paths matching each clip.</param>
    /// <param name="labels">Readable hierarchy and clip labels.</param>
    public GameRoomPortalAnimatorClipChoiceCatalog(IReadOnlyList<AnimationClip> clips,
                                                   IReadOnlyList<string> paths,
                                                   IReadOnlyList<string> labels)
    {
        Clips = clips;
        Paths = paths;
        Labels = labels;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Finds an exact serialized clip and Animator path pair.
    /// </summary>
    /// <param name="clip">Serialized clip asset.</param>
    /// <param name="path">Serialized relative Animator path.</param>
    /// <returns>Matching dropdown index, or -1 when the catalog does not expose the clip and path pair.</returns>
    public int IndexOf(AnimationClip clip, string path)
    {
        for (int index = 0; index < Clips.Count; index++)
        {
            if (Clips[index] == clip && string.Equals(Paths[index], path, StringComparison.Ordinal))
                return index;
        }

        return -1;
    }
    #endregion

    #endregion
}
#endif
