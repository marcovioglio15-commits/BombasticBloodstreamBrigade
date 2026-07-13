using System;

/// <summary>
/// Declares one ScriptableObject type scanned by the Excel Data Transfer field catalog.
/// </summary>
internal sealed class ExcelDataFieldCatalogSourceDefinition
{
    #region Properties
    public Type AssetType
    {
        get;
    }

    public ExcelDataTransferDomain Domain
    {
        get;
    }

    public string RootFolder
    {
        get;
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one catalog source definition.
    /// </summary>
    /// <param name="assetType">ScriptableObject type scanned through AssetDatabase.</param>
    /// <param name="domain">Management domain owning assets of this type.</param>
    /// <param name="rootFolder">Project-relative root folder searched for assets.</param>
    public ExcelDataFieldCatalogSourceDefinition(Type assetType,
                                                 ExcelDataTransferDomain domain,
                                                 string rootFolder)
    {
        AssetType = assetType;
        Domain = domain;
        RootFolder = rootFolder;
    }
    #endregion

    #endregion
}
