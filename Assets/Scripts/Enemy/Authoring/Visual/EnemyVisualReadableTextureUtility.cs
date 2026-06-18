using UnityEngine;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

/// <summary>
/// Builds temporary readable texture copies for bake-time enemy visual color sampling.
/// </summary>
internal static class EnemyVisualReadableTextureUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves a readable Texture2D for color sampling without changing import settings on source assets.
    /// </summary>
    /// <param name="texture">Source texture assigned to the sampled material.</param>
    /// <param name="readableTexture">Readable texture to sample.</param>
    /// <param name="ownsReadableTexture">True when the caller must release the returned copy.</param>
    /// <returns>True when a readable texture is available.</returns>
    public static bool TryResolveReadableTexture(Texture texture, out Texture2D readableTexture, out bool ownsReadableTexture)
    {
        readableTexture = null;
        ownsReadableTexture = false;
        Texture2D texture2D = texture as Texture2D;

        if (texture2D == null)
            return false;

        if (texture2D.isReadable)
        {
            readableTexture = texture2D;
            return true;
        }

#if UNITY_EDITOR
        if (TryLoadEditorReadableTextureCopy(texture2D, out readableTexture))
        {
            ownsReadableTexture = true;
            return true;
        }
#endif

        if (!TryCreateGpuReadableTextureCopy(texture2D, out readableTexture))
            return false;

        ownsReadableTexture = true;
        return true;
    }

    /// <summary>
    /// Releases a temporary readable texture copy created by TryResolveReadableTexture.
    /// </summary>
    /// <param name="readableTexture">Texture returned by TryResolveReadableTexture.</param>
    /// <param name="ownsReadableTexture">True when the texture is a temporary copy owned by the caller.</param>
    public static void ReleaseReadableTexture(Texture2D readableTexture, bool ownsReadableTexture)
    {
        if (!ownsReadableTexture || readableTexture == null)
            return;

        if (Application.isPlaying)
            Object.Destroy(readableTexture);
        else
            Object.DestroyImmediate(readableTexture);
    }
    #endregion

    #region Copy Creation
#if UNITY_EDITOR
    /// <summary>
    /// Loads a readable copy directly from the source asset file when running in the editor.
    /// </summary>
    /// <param name="texture">Unreadable imported texture.</param>
    /// <param name="readableTexture">Readable copy loaded from disk.</param>
    /// <returns>True when the asset file was decoded successfully.</returns>
    private static bool TryLoadEditorReadableTextureCopy(Texture2D texture, out Texture2D readableTexture)
    {
        readableTexture = null;
        string assetPath = AssetDatabase.GetAssetPath(texture);

        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        string fullPath = Path.GetFullPath(assetPath);

        if (!File.Exists(fullPath))
            return false;

        try
        {
            byte[] imageData = File.ReadAllBytes(fullPath);
            Texture2D decodedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);

            if (!ImageConversion.LoadImage(decodedTexture, imageData, false))
            {
                Object.DestroyImmediate(decodedTexture);
                return false;
            }

            readableTexture = decodedTexture;
            return true;
        }
        catch
        {
            if (readableTexture != null)
            {
                Object.DestroyImmediate(readableTexture);
                readableTexture = null;
            }

            return false;
        }
    }
#endif

    /// <summary>
    /// Creates a temporary readable copy through the GPU when no editor asset file copy is available.
    /// </summary>
    /// <param name="texture">Unreadable imported texture.</param>
    /// <param name="readableTexture">Readable copy generated from the source texture.</param>
    /// <returns>True when the GPU copy was read back successfully.</returns>
    private static bool TryCreateGpuReadableTextureCopy(Texture2D texture, out Texture2D readableTexture)
    {
        readableTexture = null;

        if (texture.width <= 0 || texture.height <= 0)
            return false;

        RenderTexture previousActiveTexture = RenderTexture.active;
        RenderTexture temporaryRenderTexture = RenderTexture.GetTemporary(texture.width,
                                                                          texture.height,
                                                                          0,
                                                                          RenderTextureFormat.ARGB32,
                                                                          RenderTextureReadWrite.Default);

        try
        {
            Graphics.Blit(texture, temporaryRenderTexture);
            RenderTexture.active = temporaryRenderTexture;
            Texture2D copiedTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, false);
            copiedTexture.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0, false);
            copiedTexture.Apply(false, false);
            readableTexture = copiedTexture;
            return true;
        }
        catch
        {
            if (readableTexture != null)
            {
                ReleaseReadableTexture(readableTexture, true);
                readableTexture = null;
            }

            return false;
        }
        finally
        {
            RenderTexture.active = previousActiveTexture;
            RenderTexture.ReleaseTemporary(temporaryRenderTexture);
        }
    }
    #endregion

    #endregion
}
