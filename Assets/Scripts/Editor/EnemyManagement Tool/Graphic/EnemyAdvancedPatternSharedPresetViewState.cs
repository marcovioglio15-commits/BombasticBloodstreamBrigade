using System.Collections.Generic;

/// <summary>
/// Stores transient filter text used by the shared Modules and Patterns preset authoring views.
/// </summary>
internal sealed class EnemyAdvancedPatternSharedPresetViewState
{
    #region Fields
    private readonly Dictionary<EnemyPatternModuleCatalogSection, string> moduleIdFilterTextBySection = new Dictionary<EnemyPatternModuleCatalogSection, string>();
    private readonly Dictionary<EnemyPatternModuleCatalogSection, string> moduleDisplayNameFilterTextBySection = new Dictionary<EnemyPatternModuleCatalogSection, string>();

    private string patternIdFilterText = string.Empty;
    private string patternDisplayNameFilterText = string.Empty;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Returns the current module-ID filter text for one catalog section.
    /// </summary>
    /// <param name="section">Catalog section whose filter text is requested.</param>
    /// <returns>Current module-ID filter text, or an empty string when unset.</returns>
    public string GetModuleIdFilterText(EnemyPatternModuleCatalogSection section)
    {
        if (moduleIdFilterTextBySection.TryGetValue(section, out string filterText))
            return filterText;

        return string.Empty;
    }

    /// <summary>
    /// Returns the current module display-name filter text for one catalog section.
    /// </summary>
    /// <param name="section">Catalog section whose filter text is requested.</param>
    /// <returns>Current display-name filter text, or an empty string when unset.</returns>
    public string GetModuleDisplayNameFilterText(EnemyPatternModuleCatalogSection section)
    {
        if (moduleDisplayNameFilterTextBySection.TryGetValue(section, out string filterText))
            return filterText;

        return string.Empty;
    }

    /// <summary>
    /// Stores the module-ID filter text for one catalog section.
    /// </summary>
    /// <param name="section">Catalog section whose filter text is being updated.</param>
    /// <param name="filterText">New filter text.</param>
    public void SetModuleIdFilterText(EnemyPatternModuleCatalogSection section, string filterText)
    {
        moduleIdFilterTextBySection[section] = filterText ?? string.Empty;
    }

    /// <summary>
    /// Stores the module display-name filter text for one catalog section.
    /// </summary>
    /// <param name="section">Catalog section whose filter text is being updated.</param>
    /// <param name="filterText">New filter text.</param>
    public void SetModuleDisplayNameFilterText(EnemyPatternModuleCatalogSection section, string filterText)
    {
        moduleDisplayNameFilterTextBySection[section] = filterText ?? string.Empty;
    }

    /// <summary>
    /// Clears both filter texts stored for one catalog section.
    /// </summary>
    /// <param name="section">Catalog section whose filter texts should be cleared.</param>
    public void ClearModuleFilters(EnemyPatternModuleCatalogSection section)
    {
        moduleIdFilterTextBySection[section] = string.Empty;
        moduleDisplayNameFilterTextBySection[section] = string.Empty;
    }

    /// <summary>
    /// Returns the current pattern-ID filter text.
    /// </summary>
    /// <returns>Current pattern-ID filter text.</returns>
    public string GetPatternIdFilterText()
    {
        return patternIdFilterText;
    }

    /// <summary>
    /// Returns the current pattern display-name filter text.
    /// </summary>
    /// <returns>Current pattern display-name filter text.</returns>
    public string GetPatternDisplayNameFilterText()
    {
        return patternDisplayNameFilterText;
    }

    /// <summary>
    /// Stores the current pattern-ID filter text.
    /// </summary>
    /// <param name="filterText">New pattern-ID filter text.</param>
    public void SetPatternIdFilterText(string filterText)
    {
        patternIdFilterText = filterText ?? string.Empty;
    }

    /// <summary>
    /// Stores the current pattern display-name filter text.
    /// </summary>
    /// <param name="filterText">New pattern display-name filter text.</param>
    public void SetPatternDisplayNameFilterText(string filterText)
    {
        patternDisplayNameFilterText = filterText ?? string.Empty;
    }

    /// <summary>
    /// Clears both pattern filter texts.
    /// </summary>
    public void ClearPatternFilters()
    {
        patternIdFilterText = string.Empty;
        patternDisplayNameFilterText = string.Empty;
    }
    #endregion

    #endregion
}
