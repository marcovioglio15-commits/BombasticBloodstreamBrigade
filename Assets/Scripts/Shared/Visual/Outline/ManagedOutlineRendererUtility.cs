using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies managed outline property overrides to renderer hierarchies that already expose outline shader properties.
/// </summary>
public static class ManagedOutlineRendererUtility
{
    #region Constants
    private const float ThicknessEpsilon = 0.0001f;
    private const float ColorEpsilon = 0.0001f;
    private static readonly int OutlineThicknessPropertyId = Shader.PropertyToID("_OutlineThickness");
    private static readonly int OutlineColorPropertyId = Shader.PropertyToID("_OutlineColor");
    #endregion

    #region Fields
    private static readonly Dictionary<int, CachedRendererSet> cacheByAnimatorInstanceId = new Dictionary<int, CachedRendererSet>(16);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies the requested outline state to every compatible renderer below the provided animator.
    /// </summary>
    /// <param name="animator">Root animator whose renderer hierarchy should receive outline overrides.</param>
    /// <param name="enabled">When false, outline thickness is forced to zero.</param>
    /// <param name="outlineColor">Outline color written to compatible renderers.</param>
    /// <param name="outlineThickness">Outline thickness written to compatible renderers.</param>
    public static void ApplyToAnimator(Animator animator,
                                       bool enabled,
                                       Color outlineColor,
                                       float outlineThickness)
    {
        if (animator == null)
            return;

        int animatorInstanceId = animator.GetInstanceID();
        CachedRendererSet rendererSet = GetOrCreateRendererSet(animatorInstanceId, animator);
        float effectiveThickness = enabled ? Mathf.Max(0f, outlineThickness) : 0f;

        if (rendererSet.IsAppliedStateUpToDate(enabled, outlineColor, effectiveThickness))
            return;

        for (int rendererIndex = 0; rendererIndex < rendererSet.Entries.Count; rendererIndex++)
        {
            CachedRendererEntry entry = rendererSet.Entries[rendererIndex];

            if (entry.Renderer == null)
                continue;

            ApplyEntry(entry, outlineColor, effectiveThickness);
        }

        rendererSet.SetAppliedState(enabled, outlineColor, effectiveThickness);
    }

    /// <summary>
    /// Clears the cached renderer sets.
    /// </summary>
    public static void ClearCache()
    {
        cacheByAnimatorInstanceId.Clear();
    }
    #endregion

    #region Cache
    /// <summary>
    /// Resolves the cached renderer set associated with one animator, rebuilding it when the hierarchy changed.
    /// </summary>
    /// <param name="animatorInstanceId">Stable animator instance identifier.</param>
    /// <param name="animator">Animator whose hierarchy should be cached.</param>
    /// <returns>The cached renderer set for the animator.</returns>
    private static CachedRendererSet GetOrCreateRendererSet(int animatorInstanceId, Animator animator)
    {
        CachedRendererSet rendererSet;

        if (cacheByAnimatorInstanceId.TryGetValue(animatorInstanceId, out rendererSet))
        {
            if (rendererSet.Animator == animator && rendererSet.IsValid)
                return rendererSet;

            cacheByAnimatorInstanceId.Remove(animatorInstanceId);
        }

        rendererSet = BuildRendererSet(animator);
        cacheByAnimatorInstanceId[animatorInstanceId] = rendererSet;
        return rendererSet;
    }

    /// <summary>
    /// Builds one renderer cache from the supplied animator hierarchy.
    /// </summary>
    /// <param name="animator">Animator whose renderer hierarchy should be scanned.</param>
    /// <returns>The newly built renderer cache.</returns>
    private static CachedRendererSet BuildRendererSet(Animator animator)
    {
        CachedRendererSet rendererSet = new CachedRendererSet();
        rendererSet.Animator = animator;

        if (animator == null)
            return rendererSet;

        Renderer[] renderers = animator.GetComponentsInChildren<Renderer>(true);

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];

            if (renderer == null)
                continue;

            CachedRendererEntry entry = CreateRendererEntry(renderer);

            if (!entry.SupportsOutline)
                continue;

            rendererSet.Entries.Add(entry);
        }

        return rendererSet;
    }

    /// <summary>
    /// Creates one cached renderer entry and detects whether any shared material exposes outline properties.
    /// </summary>
    /// <param name="renderer">Renderer inspected for outline compatibility.</param>
    /// <returns>The created cached renderer entry.</returns>
    private static CachedRendererEntry CreateRendererEntry(Renderer renderer)
    {
        CachedRendererEntry entry = new CachedRendererEntry();
        entry.Renderer = renderer;
        entry.PropertyBlock = new MaterialPropertyBlock();
        Material[] sharedMaterials = renderer.sharedMaterials;

        for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
        {
            Material sharedMaterial = sharedMaterials[materialIndex];

            if (sharedMaterial == null)
                continue;

            if (!entry.HasOutlineColor && sharedMaterial.HasProperty(OutlineColorPropertyId))
                entry.HasOutlineColor = true;

            if (!entry.HasOutlineThickness && sharedMaterial.HasProperty(OutlineThicknessPropertyId))
                entry.HasOutlineThickness = true;
        }

        return entry;
    }
    #endregion

    #region Apply
    /// <summary>
    /// Applies one outline override to a single cached renderer entry.
    /// </summary>
    /// <param name="entry">Cached renderer entry receiving the property-block update.</param>
    /// <param name="outlineColor">Outline color to apply.</param>
    /// <param name="outlineThickness">Outline thickness to apply.</param>
    private static void ApplyEntry(CachedRendererEntry entry, Color outlineColor, float outlineThickness)
    {
        Renderer renderer = entry.Renderer;

        if (renderer == null)
            return;

        MaterialPropertyBlock propertyBlock = entry.PropertyBlock;
        renderer.GetPropertyBlock(propertyBlock);

        if (entry.HasOutlineColor)
            propertyBlock.SetColor(OutlineColorPropertyId, outlineColor);

        if (entry.HasOutlineThickness)
            propertyBlock.SetFloat(OutlineThicknessPropertyId, outlineThickness);

        renderer.SetPropertyBlock(propertyBlock);
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Cached renderer-set entry list bound to one animator instance.
    /// </summary>
    private sealed class CachedRendererSet
    {
        #region Fields
        public Animator Animator;
        public readonly List<CachedRendererEntry> Entries = new List<CachedRendererEntry>(8);
        public byte AppliedEnabled;
        public float AppliedThickness;
        public Color AppliedColor;
        public bool AppliedStateInitialized;
        #endregion

        #region Properties
        public bool IsValid
        {
            get
            {
                if (Animator == null)
                    return false;

                for (int entryIndex = 0; entryIndex < Entries.Count; entryIndex++)
                {
                    if (Entries[entryIndex].Renderer == null)
                        return false;
                }

                return true;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Checks whether the currently cached outline state already matches the requested state.
        /// </summary>
        /// <param name="enabled">Requested enable flag.</param>
        /// <param name="outlineColor">Requested outline color.</param>
        /// <param name="outlineThickness">Requested outline thickness.</param>
        /// <returns>True when no renderer update is required.</returns>
        public bool IsAppliedStateUpToDate(bool enabled, Color outlineColor, float outlineThickness)
        {
            if (!AppliedStateInitialized)
                return false;

            if (AppliedEnabled != (enabled ? (byte)1 : (byte)0))
                return false;

            if (Mathf.Abs(AppliedThickness - outlineThickness) > ThicknessEpsilon)
                return false;

            return AreColorsEquivalent(AppliedColor, outlineColor);
        }

        /// <summary>
        /// Stores one outline state after a successful renderer update.
        /// </summary>
        /// <param name="enabled">Applied enable flag.</param>
        /// <param name="outlineColor">Applied outline color.</param>
        /// <param name="outlineThickness">Applied outline thickness.</param>
        public void SetAppliedState(bool enabled, Color outlineColor, float outlineThickness)
        {
            AppliedEnabled = enabled ? (byte)1 : (byte)0;
            AppliedThickness = outlineThickness;
            AppliedColor = outlineColor;
            AppliedStateInitialized = true;
        }

        /// <summary>
        /// Compares two colors using a small tolerance suitable for inspector-authored values.
        /// </summary>
        /// <param name="left">First color.</param>
        /// <param name="right">Second color.</param>
        /// <returns>True when all channels are effectively equal.</returns>
        private static bool AreColorsEquivalent(Color left, Color right)
        {
            if (Mathf.Abs(left.r - right.r) > ColorEpsilon)
                return false;

            if (Mathf.Abs(left.g - right.g) > ColorEpsilon)
                return false;

            if (Mathf.Abs(left.b - right.b) > ColorEpsilon)
                return false;

            return Mathf.Abs(left.a - right.a) <= ColorEpsilon;
        }
        #endregion
    }

    /// <summary>
    /// Cached per-renderer outline capabilities and reusable property block.
    /// </summary>
    private sealed class CachedRendererEntry
    {
        #region Fields
        public Renderer Renderer;
        public MaterialPropertyBlock PropertyBlock;
        public bool HasOutlineColor;
        public bool HasOutlineThickness;
        #endregion

        #region Properties
        public bool SupportsOutline
        {
            get
            {
                return HasOutlineColor || HasOutlineThickness;
            }
        }
        #endregion
    }
    #endregion
}
