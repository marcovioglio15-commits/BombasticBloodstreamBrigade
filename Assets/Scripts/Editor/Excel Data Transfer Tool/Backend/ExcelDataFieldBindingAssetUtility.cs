using System;
using UnityEditor;
using Object = UnityEngine.Object;

/// <summary>
/// Resolves grid field bindings to their current owner asset and stable current SerializedProperty.
/// </summary>
internal static class ExcelDataFieldBindingAssetUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves a binding owner path from its stable GUID before using the readable stored path fallback.
    /// </summary>
    /// <param name="binding">Field binding containing owner identity.</param>
    /// <returns>Current project-relative owner path, or an empty string.</returns>
    public static string ResolveOwnerAssetPath(ExcelDataFieldBinding binding)
    {
        if (binding == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(binding.OwnerAssetGuid))
        {
            string guidPath = AssetDatabase.GUIDToAssetPath(binding.OwnerAssetGuid);

            if (!string.IsNullOrWhiteSpace(guidPath))
                return guidPath;
        }

        return binding.OwnerAssetPath ?? string.Empty;
    }

    /// <summary>
    /// Resolves and validates one binding target without mutating its owner asset.
    /// </summary>
    /// <param name="binding">Grid-authoritative field binding.</param>
    /// <param name="asset">Resolved owner asset.</param>
    /// <param name="serializedObject">Serialized wrapper for the owner asset.</param>
    /// <param name="property">Concrete serialized property.</param>
    /// <param name="warning">Diagnostic generated when resolution fails.</param>
    /// <returns>True when owner, type and property all resolve.</returns>
    public static bool TryResolveTarget(ExcelDataFieldBinding binding,
                                        out Object asset,
                                        out SerializedObject serializedObject,
                                        out SerializedProperty property,
                                        out string warning)
    {
        asset = null;
        serializedObject = null;
        property = null;
        warning = string.Empty;

        if (binding == null || !binding.IsUsable())
        {
            warning = "Missing or unusable field binding.";
            return false;
        }

        string ownerAssetPath = ResolveOwnerAssetPath(binding);

        if (string.IsNullOrWhiteSpace(ownerAssetPath))
        {
            warning = "Owner asset could not be resolved from GUID or stored path.";
            return false;
        }

        asset = AssetDatabase.LoadAssetAtPath<Object>(ownerAssetPath);

        if (asset == null)
        {
            warning = "Missing owner asset at path: " + ownerAssetPath + ".";
            return false;
        }

        if (!MatchesExpectedType(asset, binding.OwnerAssetTypeName))
        {
            warning = "Owner asset type mismatch. Expected " + binding.OwnerAssetTypeName +
                      ", found " + asset.GetType().Name + ".";
            return false;
        }

        serializedObject = new SerializedObject(asset);
        return ExcelDataStableFieldBindingResolver.TryResolveProperty(binding,
                                                                      serializedObject,
                                                                      out property,
                                                                      out string _,
                                                                      out warning);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Compares an owner asset against a stored simple or fully qualified type name.
    /// </summary>
    /// <param name="asset">Resolved owner asset.</param>
    /// <param name="expectedTypeName">Stored expected type name.</param>
    /// <returns>True when no type was stored or the current owner type matches.</returns>
    private static bool MatchesExpectedType(Object asset, string expectedTypeName)
    {
        if (asset == null || string.IsNullOrWhiteSpace(expectedTypeName))
            return true;

        Type assetType = asset.GetType();
        return string.Equals(assetType.Name, expectedTypeName, StringComparison.Ordinal) ||
               string.Equals(assetType.FullName, expectedTypeName, StringComparison.Ordinal) ||
               string.Equals(assetType.AssemblyQualifiedName, expectedTypeName, StringComparison.Ordinal);
    }
    #endregion

    #endregion
}
