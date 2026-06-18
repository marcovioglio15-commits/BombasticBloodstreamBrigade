using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Synchronizes editor preview outline state for player visuals and keeps the legacy outline material asset in sync when available.
/// </summary>
public static class PlayerOutlineRuntimeMaterialSyncUtility
{
    #region Constants
    private const string ReferenceAssetResourcesPath = "PlayerOutlineRuntimeMaterialReference";
    private const float ThicknessEpsilon = 0.0001f;
    private const float ColorEpsilon = 0.0001f;
    private static readonly int OutlineThicknessPropertyId = Shader.PropertyToID("_OutlineThickness");
    private static readonly int OutlineColorPropertyId = Shader.PropertyToID("_OutlineColor");

#if UNITY_EDITOR
    private const string ReferenceAssetPath = "Assets/Resources/PlayerOutlineRuntimeMaterialReference.asset";
    private const string OutlineMaterialAssetPath = "Assets/3D/Materials/M_Outline.mat";
#endif
    #endregion

    #region Fields
    private static PlayerOutlineRuntimeMaterialReference cachedReferenceAsset;
    private static Material cachedOutlineMaterial;
    private static byte appliedEnabled;
    private static float appliedThickness;
    private static Color appliedColor;
    private static bool appliedStateInitialized;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies the outline values authored inside one player visual preset to the runtime outline material used by the player renderer feature.
    /// </summary>
    /// <param name="preset">Player visual preset currently being edited or used at runtime.</param>
    /// <returns>True when the outline material was found and updated.</returns>
    public static bool ApplyFromPreset(PlayerVisualPreset preset)
    {
        if (preset == null || preset.Outline == null)
            return false;

        return ApplyToMaterial(preset.Outline.EnableOutline,
                               preset.Outline.OutlineColor,
                               preset.Outline.OutlineThickness);
    }

    /// <summary>
    /// Applies one ECS outline configuration to the runtime outline material used by the player renderer feature.
    /// </summary>
    /// <param name="outlineConfig">ECS outline config currently active on the player entity.</param>
    /// <returns>True when the outline material was found and updated.</returns>
    public static bool ApplyFromOutlineConfig(in OutlineVisualConfig outlineConfig)
    {
        return ApplyToMaterial(outlineConfig.Enabled != 0,
                               DamageFlashRuntimeUtility.ToManagedColor(outlineConfig.Color),
                               outlineConfig.Thickness);
    }

    /// <summary>
    /// Clears cached material and reference lookups so subsequent calls reload current assets.
    /// </summary>
    public static void ClearCache()
    {
        cachedReferenceAsset = null;
        cachedOutlineMaterial = null;
        appliedEnabled = 0;
        appliedThickness = 0f;
        appliedColor = Color.clear;
        appliedStateInitialized = false;
    }
    #endregion

    #region Private Methods
#if UNITY_EDITOR
    /// <summary>
    /// Ensures the editor-side reference asset exists as soon as scripts reload, so play mode and build-time Resources lookup are both deterministic.
    /// </summary>
    [InitializeOnLoadMethod]
    private static void InitializeEditorReferenceAsset()
    {
        EnsureReferenceAssetExists();
        ClearCache();
    }
#endif

    /// <summary>
    /// Applies one resolved outline state to the player outline material while skipping redundant writes.
    /// </summary>
    /// <param name="enabled">When false, outline thickness is forced to zero.</param>
    /// <param name="outlineColor">Outline color to write.</param>
    /// <param name="outlineThickness">Outline thickness to write.</param>
    /// <returns>True when the runtime outline material exists.</returns>
    private static bool ApplyToMaterial(bool enabled, Color outlineColor, float outlineThickness)
    {
        Material outlineMaterial = ResolveOutlineMaterial();
        float effectiveThickness = enabled ? Mathf.Max(0f, outlineThickness) : 0f;

#if UNITY_EDITOR
        ApplyToLoadedPlayerScenePreview(enabled, outlineColor, effectiveThickness);
#endif

        if (outlineMaterial == null)
            return false;

        if (IsAppliedStateUpToDate(enabled, outlineColor, effectiveThickness))
            return true;

        if (outlineMaterial.HasProperty(OutlineThicknessPropertyId))
            outlineMaterial.SetFloat(OutlineThicknessPropertyId, effectiveThickness);

        if (outlineMaterial.HasProperty(OutlineColorPropertyId))
            outlineMaterial.SetColor(OutlineColorPropertyId, outlineColor);

        appliedEnabled = enabled ? (byte)1 : (byte)0;
        appliedThickness = effectiveThickness;
        appliedColor = outlineColor;
        appliedStateInitialized = true;
        return true;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Applies the current outline state to player authoring instances already loaded in editor scenes so preset editing keeps the scene preview responsive.
    /// </summary>
    /// <param name="enabled">When false, outline thickness is forced to zero.</param>
    /// <param name="outlineColor">Outline color written to loaded player preview renderers.</param>
    /// <param name="outlineThickness">Outline thickness written to loaded player preview renderers.</param>
    private static void ApplyToLoadedPlayerScenePreview(bool enabled, Color outlineColor, float outlineThickness)
    {
        PlayerAuthoring[] playerAuthorings = Object.FindObjectsByType<PlayerAuthoring>(FindObjectsInactive.Include,
                                                                                       FindObjectsSortMode.None);

        for (int playerIndex = 0; playerIndex < playerAuthorings.Length; playerIndex++)
        {
            PlayerAuthoring playerAuthoring = playerAuthorings[playerIndex];

            if (playerAuthoring == null)
                continue;

            Animator animator = playerAuthoring.AnimatorComponent;

            if (animator == null)
                animator = playerAuthoring.GetComponentInChildren<Animator>(true);

            if (animator == null)
                continue;

            ManagedOutlineRendererUtility.ApplyToAnimator(animator,
                                                         enabled,
                                                         outlineColor,
                                                         outlineThickness);
        }

        SceneView.RepaintAll();
    }
#endif

    /// <summary>
    /// Resolves the outline material used by the player RenderObjects feature.
    /// </summary>
    /// <returns>The resolved outline material, or null when no reference asset is available.</returns>
    private static Material ResolveOutlineMaterial()
    {
        if (cachedOutlineMaterial != null)
            return cachedOutlineMaterial;

        PlayerOutlineRuntimeMaterialReference referenceAsset = ResolveReferenceAsset();

        if (referenceAsset == null || referenceAsset.OutlineMaterial == null)
            return null;

        cachedOutlineMaterial = referenceAsset.OutlineMaterial;
        return cachedOutlineMaterial;
    }

    /// <summary>
    /// Resolves the Resources-backed reference asset that points at the player outline material.
    /// </summary>
    /// <returns>The resolved reference asset, or null when it cannot be found.</returns>
    private static PlayerOutlineRuntimeMaterialReference ResolveReferenceAsset()
    {
        if (cachedReferenceAsset != null)
            return cachedReferenceAsset;

#if UNITY_EDITOR
        EnsureReferenceAssetExists();
#endif

        cachedReferenceAsset = Resources.Load<PlayerOutlineRuntimeMaterialReference>(ReferenceAssetResourcesPath);
        return cachedReferenceAsset;
    }

    /// <summary>
    /// Checks whether the currently applied material state already matches the requested state.
    /// </summary>
    /// <param name="enabled">Requested enable flag.</param>
    /// <param name="outlineColor">Requested outline color.</param>
    /// <param name="outlineThickness">Requested outline thickness.</param>
    /// <returns>True when no additional material write is required.</returns>
    private static bool IsAppliedStateUpToDate(bool enabled, Color outlineColor, float outlineThickness)
    {
        if (!appliedStateInitialized)
            return false;

        if (appliedEnabled != (enabled ? (byte)1 : (byte)0))
            return false;

        if (Mathf.Abs(appliedThickness - outlineThickness) > ThicknessEpsilon)
            return false;

        if (Mathf.Abs(appliedColor.r - outlineColor.r) > ColorEpsilon)
            return false;

        if (Mathf.Abs(appliedColor.g - outlineColor.g) > ColorEpsilon)
            return false;

        if (Mathf.Abs(appliedColor.b - outlineColor.b) > ColorEpsilon)
            return false;

        return Mathf.Abs(appliedColor.a - outlineColor.a) <= ColorEpsilon;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Ensures the Resources reference asset exists and points to the player outline material used by the renderer feature.
    /// </summary>
    private static void EnsureReferenceAssetExists()
    {
        PlayerOutlineRuntimeMaterialReference referenceAsset = AssetDatabase.LoadAssetAtPath<PlayerOutlineRuntimeMaterialReference>(ReferenceAssetPath);
        Material outlineMaterial = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialAssetPath);

        if (outlineMaterial == null)
            return;

        if (referenceAsset == null)
        {
            EnsureFolderExists(Path.GetDirectoryName(ReferenceAssetPath));
            referenceAsset = ScriptableObject.CreateInstance<PlayerOutlineRuntimeMaterialReference>();
            referenceAsset.SetOutlineMaterial(outlineMaterial);
            AssetDatabase.CreateAsset(referenceAsset, ReferenceAssetPath);
            AssetDatabase.SaveAssets();
            cachedReferenceAsset = referenceAsset;
            cachedOutlineMaterial = outlineMaterial;
            return;
        }

        if (referenceAsset.OutlineMaterial == outlineMaterial)
            return;

        referenceAsset.SetOutlineMaterial(outlineMaterial);
        EditorUtility.SetDirty(referenceAsset);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Ensures one AssetDatabase folder path exists before creating the reference asset.
    /// </summary>
    /// <param name="folderPath">Folder path that should exist.</param>
    private static void EnsureFolderExists(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parentFolder = Path.GetDirectoryName(folderPath);
        string folderName = Path.GetFileName(folderPath);

        if (!string.IsNullOrWhiteSpace(parentFolder) && !AssetDatabase.IsValidFolder(parentFolder))
            EnsureFolderExists(parentFolder);

        AssetDatabase.CreateFolder(parentFolder, folderName);
    }
#endif
    #endregion

    #endregion
}
