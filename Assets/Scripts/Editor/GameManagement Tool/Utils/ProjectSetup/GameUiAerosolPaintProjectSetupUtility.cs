using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates the deterministic aerosol deposition field and synchronizes default Paint Reveal assets.
/// </summary>
public static class GameUiAerosolPaintProjectSetupUtility
{
    #region Constants
    private const int DepositionTextureSize = 512;
    private const int DepositSeedCount = 56;
    private const int DepositRandomSeed = 196731;
    private const string PaintShaderName = "Custom/UI/PaintReveal";
    private const string EffectsFolderPath = "Assets/2D/UI/Effects";
    public const string DepositionTexturePath = EffectsFolderPath + "/UI_AerosolDepositionMap.png";
    public const string RoomClearSpritePath = EffectsFolderPath + "/UI_AerosolRoomClearMask.png";
    private const string RoomClearMaterialPath = "Assets/2D/Materials/M_UI_PaintRevealRoomClearMask.mat";
    private const string SceneTransitionMaterialPath = "Assets/2D/Materials/M_UI_PaintRevealSceneTransition.mat";
    #endregion

    #region Fields
    private static bool assetsEnsured;
    #endregion

    #region Methods

    #region Entry Points
    /// <summary>
    /// Rebuilds generated aerosol assets without exposing a permanent project menu command.
    /// </summary>
    // [MenuItem("Tools/Game/UI/Rebuild Aerosol Paint Assets")]
    public static void ExecuteBatchSetup()
    {
        EnsureAssets(true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GameUiAerosolPaintProjectSetupUtility] Aerosol paint assets rebuilt.");
    }

    /// <summary>
    /// Ensures imported textures and dedicated materials satisfy the shared Paint Reveal contract.
    /// </summary>
    /// <param name="rebuildDepositionTexture">True to regenerate the deterministic timing field even when it already exists.</param>
    public static void EnsureAssets(bool rebuildDepositionTexture)
    {
        if (assetsEnsured && !rebuildDepositionTexture)
            return;

        GameManagementAssetUtility.EnsureFolder(EffectsFolderPath);

        if (rebuildDepositionTexture || !File.Exists(DepositionTexturePath))
            GenerateDepositionTexture();

        AssetDatabase.ImportAsset(DepositionTexturePath, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(RoomClearSpritePath, ImportAssetOptions.ForceSynchronousImport);
        ConfigureDepositionImporter();
        ConfigureRoomClearSpriteImporter();

        Texture2D depositionTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(DepositionTexturePath);

        if (depositionTexture == null)
            throw new InvalidOperationException("Aerosol deposition texture could not be imported: " + DepositionTexturePath + ".");

        ConfigureMaterial(RoomClearMaterialPath,
                          depositionTexture,
                          GameUiPaintRevealMode.DirectionalSweep);
        ConfigureMaterial(SceneTransitionMaterialPath,
                          depositionTexture,
                          GameUiPaintRevealMode.UniformCloud);
        assetsEnsured = true;
    }
    #endregion

    #region Texture Generation
    /// <summary>
    /// Builds a tileable grayscale field whose values encode clustered pigment arrival time.
    /// </summary>
    private static void GenerateDepositionTexture()
    {
        DepositSeed[] seeds = CreateDepositSeeds();
        float[] arrivalTimes = new float[DepositionTextureSize * DepositionTextureSize];
        float minimumArrival = float.MaxValue;
        float maximumArrival = float.MinValue;

        // Resolve the earliest expanding deposit at every texel with toroidal distance for seamless repetition.
        for (int y = 0; y < DepositionTextureSize; y++)
        {
            for (int x = 0; x < DepositionTextureSize; x++)
            {
                Vector2 position = new Vector2((x + 0.5f) / DepositionTextureSize,
                                               (y + 0.5f) / DepositionTextureSize);
                float arrival = ResolveEarliestArrival(position, seeds);
                int pixelIndex = y * DepositionTextureSize + x;
                arrivalTimes[pixelIndex] = arrival;
                minimumArrival = Mathf.Min(minimumArrival, arrival);
                maximumArrival = Mathf.Max(maximumArrival, arrival);
            }
        }

        Color32[] pixels = new Color32[arrivalTimes.Length];

        // Normalize the field and add sparse early micro-deposits without altering its large coherent islands.
        for (int pixelIndex = 0; pixelIndex < arrivalTimes.Length; pixelIndex++)
        {
            int x = pixelIndex % DepositionTextureSize;
            int y = pixelIndex / DepositionTextureSize;
            float normalizedArrival = Mathf.InverseLerp(minimumArrival,
                                                        maximumArrival,
                                                        arrivalTimes[pixelIndex]);
            float grain = Hash01(x, y, 0x45d9f3b) - 0.5f;
            float satellite = Hash01(x, y, 0x119de1f3);
            normalizedArrival += grain * 0.055f;

            if (satellite > 0.9875f)
                normalizedArrival -= (satellite - 0.9875f) * 9.5f;

            byte value = (byte)Mathf.RoundToInt(Mathf.Clamp01(normalizedArrival) * 255f);
            pixels[pixelIndex] = new Color32(value, value, value, 255);
        }

        Texture2D texture = new Texture2D(DepositionTextureSize,
                                          DepositionTextureSize,
                                          TextureFormat.RGBA32,
                                          false,
                                          true);
        texture.name = "UI_AerosolDepositionMap";
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(DepositionTexturePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    /// <summary>
    /// Creates deterministic seeds distributed across reveal time and tile space.
    /// </summary>
    /// <returns>Ordered deposit seeds used by the timing-field generator.</returns>
    private static DepositSeed[] CreateDepositSeeds()
    {
        System.Random random = new System.Random(DepositRandomSeed);
        DepositSeed[] seeds = new DepositSeed[DepositSeedCount];

        // Stagger start times while retaining spatial randomness so multiple independent clusters remain visible.
        for (int seedIndex = 0; seedIndex < seeds.Length; seedIndex++)
        {
            float sequence = (seedIndex + (float)random.NextDouble() * 0.8f) / seeds.Length;
            seeds[seedIndex] = new DepositSeed(
                new Vector2((float)random.NextDouble(), (float)random.NextDouble()),
                sequence * 0.72f,
                Mathf.Lerp(0.9f, 1.65f, (float)random.NextDouble()));
        }

        return seeds;
    }

    /// <summary>
    /// Resolves the first expanding deposit that reaches one normalized texture coordinate.
    /// </summary>
    /// <param name="position">Normalized texture coordinate.</param>
    /// <param name="seeds">Deterministic deposit seeds.</param>
    /// <returns>Unnormalized pigment arrival time.</returns>
    private static float ResolveEarliestArrival(Vector2 position, DepositSeed[] seeds)
    {
        float earliestArrival = float.MaxValue;

        // Compare every seed using wrapped distance so opposite texture edges connect without a seam.
        for (int seedIndex = 0; seedIndex < seeds.Length; seedIndex++)
        {
            float horizontalDistance = Mathf.Abs(position.x - seeds[seedIndex].Position.x);
            float verticalDistance = Mathf.Abs(position.y - seeds[seedIndex].Position.y);
            horizontalDistance = Mathf.Min(horizontalDistance, 1f - horizontalDistance);
            verticalDistance = Mathf.Min(verticalDistance, 1f - verticalDistance);
            float distance = Mathf.Sqrt(horizontalDistance * horizontalDistance +
                                        verticalDistance * verticalDistance);
            float arrival = seeds[seedIndex].StartTime + distance * seeds[seedIndex].GrowthCost;
            earliestArrival = Mathf.Min(earliestArrival, arrival);
        }

        return earliestArrival;
    }

    /// <summary>
    /// Produces a stable zero-to-one hash for fine texel variation and sparse satellite deposits.
    /// </summary>
    /// <param name="x">Texel x coordinate.</param>
    /// <param name="y">Texel y coordinate.</param>
    /// <param name="salt">Independent deterministic hash salt.</param>
    /// <returns>Pseudorandom value in the zero-to-one range.</returns>
    private static float Hash01(int x, int y, int salt)
    {
        unchecked
        {
            uint hash = (uint)(x * 374761393 + y * 668265263 + salt);
            hash = (hash ^ (hash >> 13)) * 1274126177u;
            hash ^= hash >> 16;
            return (hash & 0x00ffffffu) / 16777215f;
        }
    }
    #endregion

    #region Import And Material Setup
    /// <summary>
    /// Configures the generated timing field for linear, seamless and uncompressed shader sampling.
    /// </summary>
    private static void ConfigureDepositionImporter()
    {
        TextureImporter importer = AssetImporter.GetAtPath(DepositionTexturePath) as TextureImporter;

        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = DepositionTextureSize;
        importer.SaveAndReimport();
    }

    /// <summary>
    /// Configures the approved transparent room-clear silhouette as an uncompressed UGUI sprite.
    /// </summary>
    private static void ConfigureRoomClearSpriteImporter()
    {
        TextureImporter importer = AssetImporter.GetAtPath(RoomClearSpritePath) as TextureImporter;

        if (importer == null)
            throw new InvalidOperationException("Room-clear aerosol sprite is missing: " + RoomClearSpritePath + ".");

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.spritePixelsPerUnit = 100f;
        importer.SaveAndReimport();
    }

    /// <summary>
    /// Assigns the generated deposition field and balanced aerosol defaults to one dedicated material.
    /// </summary>
    /// <param name="materialPath">Project path of the dedicated Paint Reveal material.</param>
    /// <param name="depositionTexture">Generated clustered arrival-time field.</param>
    /// <param name="mode">Default shader presentation stored in the material.</param>
    private static void ConfigureMaterial(string materialPath,
                                          Texture2D depositionTexture,
                                          GameUiPaintRevealMode mode)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        Shader shader = Shader.Find(PaintShaderName);

        if (material == null || shader == null)
            throw new InvalidOperationException("Paint Reveal material or shader is missing for " + materialPath + ".");

        material.shader = shader;
        material.SetTexture("_DepositionTex", depositionTexture);
        material.SetFloat("_FadeMode", (float)mode);
        material.SetFloat("_PaintOperation", (float)GameUiPaintRevealOperation.Deposit);
        material.SetFloat("_DepositSoftness", 0.025f);
        material.SetFloat("_DepositVariation", 0.22f);
        material.SetFloat("_DepositScale", 2.4f);
        material.SetFloat("_MistStrength", 0.075f);
        material.SetFloat("_MistScale", 48f);
        EditorUtility.SetDirty(material);
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Stores one deterministic expanding spray deposit used only during Editor texture generation.
    /// </summary>
    private readonly struct DepositSeed
    {
        #region Fields
        public readonly Vector2 Position;
        public readonly float StartTime;
        public readonly float GrowthCost;
        #endregion

        #region Methods

        #region Constructors
        /// <summary>
        /// Creates one timed deposit seed.
        /// </summary>
        /// <param name="position">Normalized tileable texture position.</param>
        /// <param name="startTime">Time at which the deposit begins expanding.</param>
        /// <param name="growthCost">Arrival-time cost per normalized distance unit.</param>
        public DepositSeed(Vector2 position, float startTime, float growthCost)
        {
            Position = position;
            StartTime = startTime;
            GrowthCost = growthCost;
        }
        #endregion

        #endregion
    }
    #endregion
}
