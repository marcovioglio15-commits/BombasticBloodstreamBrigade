using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores the stable Unity asset and SerializedProperty identity used by one workbook data cell.
/// </summary>
[Serializable]
public sealed class ExcelDataFieldBinding
{
    #region Constants
    private const string UnityListToken = ".Array.data[";
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Stable catalog field identifier used to refresh this binding when project assets change.")]
    [SerializeField] private string fieldId;

    [Tooltip("Management domain that owns the bound authoring field.")]
    [SerializeField] private ExcelDataTransferDomain domain;

    [Tooltip("GUID of the Unity asset that owns the bound serialized property.")]
    [SerializeField] private string ownerAssetGuid;

    [Tooltip("Concrete ScriptableObject type name expected for the owner asset.")]
    [SerializeField] private string ownerAssetTypeName;

    [Tooltip("Project-relative owner asset path retained for readable diagnostics.")]
    [SerializeField] private string ownerAssetPath;

    [Tooltip("Concrete Unity SerializedProperty path, including zero-based list indexes.")]
    [SerializeField] private string serializedPath;

    [Tooltip("Reusable property path where concrete list indexes are represented by empty list tokens.")]
    [SerializeField] private string pathTemplate;

    [Tooltip("Value family expected when this field is read from or written to a workbook cell.")]
    [SerializeField] private ExcelDataBrushDataKind expectedDataKind;

    [Tooltip("Zero-based list indexes extracted from the concrete serialized property path in nesting order.")]
    [SerializeField] private List<int> concreteListIndices = new List<int>();

    [Tooltip("Stable list keys discovered for each nested list scope; empty entries use the fallback index.")]
    [SerializeField] private List<string> stableListKeys = new List<string>();
    #endregion

    #endregion

    #region Properties
    public string FieldId
    {
        get
        {
            return fieldId;
        }
    }

    public ExcelDataTransferDomain Domain
    {
        get
        {
            return domain;
        }
    }

    public string OwnerAssetGuid
    {
        get
        {
            return ownerAssetGuid;
        }
    }

    public string OwnerAssetTypeName
    {
        get
        {
            return ownerAssetTypeName;
        }
    }

    public string OwnerAssetPath
    {
        get
        {
            return ownerAssetPath;
        }
    }

    public string SerializedPath
    {
        get
        {
            return serializedPath;
        }
    }

    public string PathTemplate
    {
        get
        {
            return pathTemplate;
        }
    }

    public ExcelDataBrushDataKind ExpectedDataKind
    {
        get
        {
            return expectedDataKind;
        }
    }

    public IReadOnlyList<int> ConcreteListIndices
    {
        get
        {
            EnsureCollections();
            return concreteListIndices;
        }
    }

    public IReadOnlyList<string> StableListKeys
    {
        get
        {
            EnsureCollections();
            return stableListKeys;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Configures a complete field binding from stable catalog and serialized asset metadata.
    /// </summary>
    /// <param name="newFieldId">Stable catalog field identifier.</param>
    /// <param name="newDomain">Management domain that owns the field.</param>
    /// <param name="newOwnerAssetGuid">GUID of the owner asset.</param>
    /// <param name="newOwnerAssetTypeName">Expected owner asset type name.</param>
    /// <param name="newOwnerAssetPath">Project-relative owner asset path.</param>
    /// <param name="newSerializedPath">Concrete SerializedProperty path.</param>
    /// <param name="newPathTemplate">Reusable path template with tokenized list indexes.</param>
    /// <param name="newExpectedDataKind">Expected workbook value family.</param>
    public void Configure(string newFieldId,
                          ExcelDataTransferDomain newDomain,
                          string newOwnerAssetGuid,
                          string newOwnerAssetTypeName,
                          string newOwnerAssetPath,
                          string newSerializedPath,
                          string newPathTemplate,
                          ExcelDataBrushDataKind newExpectedDataKind)
    {
        fieldId = newFieldId;
        domain = newDomain;
        ownerAssetGuid = newOwnerAssetGuid;
        ownerAssetTypeName = newOwnerAssetTypeName;
        ownerAssetPath = newOwnerAssetPath;
        serializedPath = newSerializedPath;
        pathTemplate = newPathTemplate;
        expectedDataKind = newExpectedDataKind;
        RefreshConcreteListIndices();
    }

    /// <summary>
    /// Stores an unresolved legacy field identifier so migration never discards an authored mapping.
    /// </summary>
    /// <param name="legacyFieldId">Legacy catalog identifier that could not be resolved.</param>
    public void ConfigureUnresolved(string legacyFieldId)
    {
        fieldId = legacyFieldId;
        domain = ExcelDataTransferDomain.All;
        ownerAssetGuid = string.Empty;
        ownerAssetTypeName = string.Empty;
        ownerAssetPath = string.Empty;
        serializedPath = string.Empty;
        pathTemplate = string.Empty;
        expectedDataKind = ExcelDataBrushDataKind.Unsupported;
        EnsureCollections();
        concreteListIndices.Clear();
        stableListKeys.Clear();
    }

    /// <summary>
    /// Reports whether the binding retains a stable field identity usable by later resolution stages.
    /// </summary>
    /// <returns>True when a stable field identifier is available.</returns>
    public bool IsUsable()
    {
        return !string.IsNullOrWhiteSpace(fieldId);
    }
    #endregion

    #region Internal Methods
    /// <summary>
    /// Copies one current catalog entry into this serializable binding.
    /// </summary>
    /// <param name="entry">Catalog entry resolved during painting or migration.</param>
    internal void ConfigureFromEntry(ExcelDataFieldCatalogEntry entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        Configure(entry.FieldId,
                  entry.Domain,
                  ExtractAssetGuid(entry.FieldId),
                  entry.AssetTypeName,
                  entry.AssetPath,
                  entry.SerializedPath,
                  entry.PathTemplate,
                  entry.DataKind);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Rebuilds concrete list indexes from the current Unity serialized property path.
    /// </summary>
    private void RefreshConcreteListIndices()
    {
        EnsureCollections();
        concreteListIndices.Clear();
        stableListKeys.Clear();

        if (string.IsNullOrWhiteSpace(serializedPath))
            return;

        // Parse each nested Unity list token without allocating temporary path fragments.
        int tokenIndex = serializedPath.IndexOf(UnityListToken, StringComparison.Ordinal);

        while (tokenIndex >= 0)
        {
            int numberStartIndex = tokenIndex + UnityListToken.Length;
            int numberEndIndex = serializedPath.IndexOf(']', numberStartIndex);

            if (numberEndIndex < 0)
                break;

            int concreteIndex;

            if (int.TryParse(serializedPath.Substring(numberStartIndex, numberEndIndex - numberStartIndex), out concreteIndex))
            {
                concreteListIndices.Add(concreteIndex);
                stableListKeys.Add(string.Empty);
            }

            tokenIndex = serializedPath.IndexOf(UnityListToken, numberEndIndex + 1, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Extracts the owner asset GUID from the stable catalog field identifier.
    /// </summary>
    /// <param name="stableFieldId">Catalog field identifier formatted as domain, GUID, type and path.</param>
    /// <returns>Owner asset GUID, or an empty string when the identifier is malformed.</returns>
    private static string ExtractAssetGuid(string stableFieldId)
    {
        if (string.IsNullOrWhiteSpace(stableFieldId))
            return string.Empty;

        int firstSeparatorIndex = stableFieldId.IndexOf(':');

        if (firstSeparatorIndex < 0 || firstSeparatorIndex >= stableFieldId.Length - 1)
            return string.Empty;

        int secondSeparatorIndex = stableFieldId.IndexOf(':', firstSeparatorIndex + 1);

        if (secondSeparatorIndex <= firstSeparatorIndex + 1)
            return string.Empty;

        return stableFieldId.Substring(firstSeparatorIndex + 1, secondSeparatorIndex - firstSeparatorIndex - 1);
    }

    /// <summary>
    /// Recreates serialized collections that Unity may deserialize as null.
    /// </summary>
    private void EnsureCollections()
    {
        if (concreteListIndices == null)
            concreteListIndices = new List<int>();

        if (stableListKeys == null)
            stableListKeys = new List<string>();
    }
    #endregion

    #endregion
}
