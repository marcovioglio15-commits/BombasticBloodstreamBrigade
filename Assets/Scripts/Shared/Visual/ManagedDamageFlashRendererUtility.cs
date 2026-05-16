using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies hit-flash material property blocks to managed renderer hierarchies while preserving authored base colors.
/// </summary>
public static class ManagedDamageFlashRendererUtility
{
    #region Constants
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int FlashColorPropertyId = Shader.PropertyToID("_HitFlashColor");
    private static readonly int FlashBlendPropertyId = Shader.PropertyToID("_HitFlashBlend");
    #endregion

    #region Fields
    private static readonly Dictionary<int, CachedRendererSet> cacheByAnimatorInstanceId = new Dictionary<int, CachedRendererSet>(16);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies one hit-flash state to every renderer under the provided animator hierarchy.
    /// </summary>
    /// <param name="animator">Root animator whose child renderers should receive the flash.</param>
    /// <param name="flashColor">Linear-space target flash color.</param>
    /// <param name="blend">Current flash blend in the [0..1] range.</param>
    public static void ApplyToAnimator(Animator animator, Color flashColor, float blend)
    {
        if (animator == null)
            return;

        int animatorInstanceId = animator.GetInstanceID();
        CachedRendererSet rendererSet = GetOrCreateRendererSet(animatorInstanceId, animator);
        float clampedBlend = Mathf.Clamp01(blend);

        for (int rendererIndex = 0; rendererIndex < rendererSet.Entries.Count; rendererIndex++)
        {
            CachedRendererEntry entry = rendererSet.Entries[rendererIndex];

            if (entry.Renderer == null)
                continue;

            ApplyEntry(entry, flashColor, clampedBlend);
        }
    }
    #endregion

    #region Cache
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

    private static CachedRendererSet BuildRendererSet(Animator animator)
    {
        CachedRendererSet rendererSet = new CachedRendererSet
        {
            Animator = animator
        };

        if (animator == null)
            return rendererSet;

        Renderer[] renderers = animator.GetComponentsInChildren<Renderer>(true);

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];

            if (renderer == null)
                continue;

            CachedRendererEntry entry = CreateRendererEntry(renderer);
            rendererSet.Entries.Add(entry);
        }

        return rendererSet;
    }

    private static CachedRendererEntry CreateRendererEntry(Renderer renderer)
    {
        CachedRendererEntry entry = new CachedRendererEntry
        {
            Renderer = renderer,
            PropertyBlock = new MaterialPropertyBlock(),
            BaseColor = Color.white,
            Color = Color.white
        };

        Material[] sharedMaterials = renderer.sharedMaterials;

        for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
        {
            Material sharedMaterial = sharedMaterials[materialIndex];

            if (sharedMaterial == null)
                continue;

            if (!entry.HasBaseColor && sharedMaterial.HasProperty(BaseColorPropertyId))
            {
                entry.HasBaseColor = true;
                entry.BaseColor = sharedMaterial.GetColor(BaseColorPropertyId);
            }

            if (!entry.HasColor && sharedMaterial.HasProperty(ColorPropertyId))
            {
                entry.HasColor = true;
                entry.Color = sharedMaterial.GetColor(ColorPropertyId);
            }

            if (!entry.HasFlashColor && sharedMaterial.HasProperty(FlashColorPropertyId))
                entry.HasFlashColor = true;

            if (!entry.HasFlashBlend && sharedMaterial.HasProperty(FlashBlendPropertyId))
                entry.HasFlashBlend = true;
        }

        return entry;
    }
    #endregion

    #region Apply
    private static void ApplyEntry(CachedRendererEntry entry, Color flashColor, float blend)
    {
        Renderer renderer = entry.Renderer;
        MaterialPropertyBlock propertyBlock = entry.PropertyBlock;
        renderer.GetPropertyBlock(propertyBlock);

        if (entry.HasBaseColor)
            propertyBlock.SetColor(BaseColorPropertyId, Color.Lerp(entry.BaseColor, flashColor, blend));

        if (entry.HasColor)
            propertyBlock.SetColor(ColorPropertyId, Color.Lerp(entry.Color, flashColor, blend));

        if (entry.HasFlashColor)
            propertyBlock.SetColor(FlashColorPropertyId, flashColor);

        if (entry.HasFlashBlend)
            propertyBlock.SetFloat(FlashBlendPropertyId, blend);

        renderer.SetPropertyBlock(propertyBlock);
    }
    #endregion

    #endregion

    #region Nested Types
    private sealed class CachedRendererSet
    {
        #region Fields
        public Animator Animator;
        public readonly List<CachedRendererEntry> Entries = new List<CachedRendererEntry>(8);
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
    }

    private sealed class CachedRendererEntry
    {
        #region Fields
        public Renderer Renderer;
        public MaterialPropertyBlock PropertyBlock;
        public Color BaseColor;
        public Color Color;
        public bool HasBaseColor;
        public bool HasColor;
        public bool HasFlashColor;
        public bool HasFlashBlend;
        #endregion
    }
    #endregion
}
