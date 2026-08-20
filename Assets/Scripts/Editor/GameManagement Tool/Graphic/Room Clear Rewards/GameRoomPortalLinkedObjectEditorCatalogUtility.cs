#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds dynamic linked-object and Animator-clip choices from currently loaded portal anchors.
/// </summary>
internal static class GameRoomPortalLinkedObjectEditorCatalogUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Collects freely authored binding identifiers and readable scene-object labels.
    /// </summary>
    /// <returns>Immutable identifier values and dropdown labels for the current editor context.</returns>
    public static GameRoomPortalLinkedObjectChoiceCatalog Build()
    {
        Dictionary<string, HashSet<string>> namesByIdentifier =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        GameRoomPortalRewardLogAnchor[] anchors = FindAnchors();

        // Merge loaded anchors because one rewards preset can serve several room scenes.
        for (int anchorIndex = 0; anchorIndex < anchors.Length; anchorIndex++)
        {
            GameRoomPortalRewardEffectView effectView = anchors[anchorIndex].EffectView;

            if (effectView == null)
                continue;

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

                if (!namesByIdentifier.TryGetValue(binding.BindingId,
                                                    out HashSet<string> names))
                {
                    names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    namesByIdentifier.Add(binding.BindingId, names);
                }

                names.Add(string.IsNullOrWhiteSpace(binding.DisplayName)
                    ? binding.TargetObject.name
                    : binding.DisplayName.Trim());
            }
        }

        List<string> identifiers = new List<string>(namesByIdentifier.Keys);
        identifiers.Sort(StringComparer.Ordinal);
        List<string> labels = new List<string>(identifiers.Count);

        for (int identifierIndex = 0;
             identifierIndex < identifiers.Count;
             identifierIndex++)
        {
            string identifier = identifiers[identifierIndex];
            List<string> names = new List<string>(namesByIdentifier[identifier]);
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
        List<AnimationClip> clips = new List<AnimationClip>();
        List<string> paths = new List<string>();
        List<string> labels = new List<string>();
        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        GameRoomPortalRewardLogAnchor[] anchors = FindAnchors();

        // Collect controller clips from every matching loaded anchor hierarchy.
        for (int anchorIndex = 0; anchorIndex < anchors.Length; anchorIndex++)
        {
            GameRoomPortalRewardEffectView effectView = anchors[anchorIndex].EffectView;

            if (effectView == null)
                continue;

            IReadOnlyList<GameRoomPortalLinkedObjectBinding> linkedObjects = effectView.LinkedObjects;

            for (int bindingIndex = 0; bindingIndex < linkedObjects.Count; bindingIndex++)
            {
                GameRoomPortalLinkedObjectBinding binding = linkedObjects[bindingIndex];

                if (binding == null ||
                    binding.TargetObject == null ||
                    !string.Equals(binding.BindingId, bindingId, StringComparison.Ordinal))
                {
                    continue;
                }

                AddAnimatorClips(binding.TargetObject.transform,
                                 clips,
                                 paths,
                                 labels,
                                 keys);
            }
        }

        return new GameRoomPortalAnimatorClipChoiceCatalog(clips, paths, labels);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Finds loaded anchors, including inactive scene authoring objects.
    /// </summary>
    /// <returns>Loaded portal reward anchors in an unspecified allocation-efficient order.</returns>
    private static GameRoomPortalRewardLogAnchor[] FindAnchors()
    {
        return UnityEngine.Object.FindObjectsByType<GameRoomPortalRewardLogAnchor>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
    }

    /// <summary>
    /// Adds controller clips from every Animator below one linked object without duplicating choices.
    /// </summary>
    /// <param name="root">Linked scene-object root.</param>
    /// <param name="clips">Destination clip assets.</param>
    /// <param name="paths">Destination Animator hierarchy paths.</param>
    /// <param name="labels">Destination dropdown labels.</param>
    /// <param name="keys">Deduplication keys shared by all loaded matching anchors.</param>
    private static void AddAnimatorClips(Transform root,
                                         List<AnimationClip> clips,
                                         List<string> paths,
                                         List<string> labels,
                                         HashSet<string> keys)
    {
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

                string key = path + "\n" + clip.GetInstanceID();

                if (!keys.Add(key))
                    continue;

                clips.Add(clip);
                paths.Add(path);
                labels.Add((string.IsNullOrEmpty(path) ? "Root" : path) + " — " + clip.name);
            }
        }
    }

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
    /// <returns>Matching dropdown index, or -1 when it is not connected in loaded scenes.</returns>
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
    /// <returns>Matching dropdown index, or -1 when no loaded hierarchy exposes it.</returns>
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
