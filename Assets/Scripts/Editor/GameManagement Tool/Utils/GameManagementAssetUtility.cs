using System;
using System.IO;
using System.Text;
using UnityEditor;

/// <summary>
/// Shared editor helpers for Game Management Tool asset paths and file-safe names.
/// </summary>
public static class GameManagementAssetUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures a Unity asset folder exists, including missing parent folders.
    /// </summary>
    /// <param name="folderPath">Project-relative folder path.</param>
    public static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parentFolder = Path.GetDirectoryName(folderPath);
        string folderName = Path.GetFileName(folderPath);

        if (!string.IsNullOrWhiteSpace(parentFolder) && !AssetDatabase.IsValidFolder(parentFolder))
            EnsureFolder(parentFolder);

        AssetDatabase.CreateFolder(parentFolder, folderName);
    }

    /// <summary>
    /// Converts arbitrary preset display text into a safe Unity asset filename.
    /// </summary>
    /// <param name="rawName">Raw user-authored name.</param>
    /// <returns>Safe filename or an empty string when no valid characters remain.</returns>
    public static string NormalizeAssetName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        string trimmedName = rawName.Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))
            return string.Empty;

        char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
        StringBuilder builder = new StringBuilder(trimmedName.Length);

        for (int charIndex = 0; charIndex < trimmedName.Length; charIndex++)
        {
            char currentChar = trimmedName[charIndex];
            bool isInvalidCharacter = false;

            for (int invalidIndex = 0; invalidIndex < invalidFileNameChars.Length; invalidIndex++)
            {
                if (currentChar != invalidFileNameChars[invalidIndex])
                    continue;

                isInvalidCharacter = true;
                break;
            }

            if (isInvalidCharacter)
            {
                builder.Append('_');
                continue;
            }

            builder.Append(currentChar);
        }

        string normalizedName = builder.ToString().Trim();

        while (normalizedName.EndsWith(".", StringComparison.Ordinal))
            normalizedName = normalizedName.Substring(0, normalizedName.Length - 1).TrimEnd();

        if (string.IsNullOrWhiteSpace(normalizedName))
            return string.Empty;

        return normalizedName;
    }
    #endregion

    #endregion
}
