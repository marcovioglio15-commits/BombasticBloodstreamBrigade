/// <summary>
/// Resolves catalog metadata used by the shared module list UI.
/// </summary>
internal static class EnemyAdvancedPatternSharedPresetModuleCatalogUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the serialized property name that stores one module catalog section.
    /// </summary>
    /// <param name="section">Catalog section being queried.</param>
    /// <returns>Serialized property name for that catalog.</returns>
    public static string ResolveDefinitionsPropertyName(EnemyPatternModuleCatalogSection section)
    {
        switch (section)
        {
            case EnemyPatternModuleCatalogSection.CoreMovement:
                return "coreMovementDefinitions";

            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                return "shortRangeInteractionDefinitions";

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                return "weaponInteractionDefinitions";

            default:
                return "dropItemsDefinitions";
        }
    }

    /// <summary>
    /// Resolves the visible subsection title for one module catalog section.
    /// </summary>
    /// <param name="section">Catalog section being queried.</param>
    /// <returns>Visible subsection title.</returns>
    public static string ResolveSectionTitle(EnemyPatternModuleCatalogSection section)
    {
        switch (section)
        {
            case EnemyPatternModuleCatalogSection.CoreMovement:
                return "Core Movement";

            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                return "Short-Range Interactions";

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                return "Weapons Interactions";

            default:
                return "Drop Items";
        }
    }

    /// <summary>
    /// Resolves the explanatory tooltip used by one module catalog subsection header.
    /// </summary>
    /// <param name="section">Catalog section being queried.</param>
    /// <returns>Tooltip text.</returns>
    public static string ResolveSectionTooltip(EnemyPatternModuleCatalogSection section)
    {
        switch (section)
        {
            case EnemyPatternModuleCatalogSection.CoreMovement:
                return "Reusable movement foundations selected by assembled shared patterns.";

            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                return "Reusable short-range overrides activated only inside the configured proximity gate.";

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                return "Reusable weapon modules referenced by assembled shared patterns.";

            default:
                return "Reusable drop modules preserved for loot-oriented enemy patterns.";
        }
    }

    /// <summary>
    /// Resolves the subdued description label used below one module catalog subsection header.
    /// </summary>
    /// <param name="section">Catalog section being queried.</param>
    /// <returns>Description text.</returns>
    public static string ResolveSectionDescription(EnemyPatternModuleCatalogSection section)
    {
        switch (section)
        {
            case EnemyPatternModuleCatalogSection.CoreMovement:
                return "Baseline movement modules reused by shared assembled patterns while short-range overrides are inactive.";

            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                return "Optional close-range behavior overrides reused by shared assembled patterns.";

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                return "Reusable weapon behaviors combined with the shared weapon-range settings authored in Pattern Assemble.";

            default:
                return "Optional reusable death-drop modules preserved for loot-oriented enemy setups.";
        }
    }

    /// <summary>
    /// Resolves the fallback module ID prefix used for one new module entry.
    /// </summary>
    /// <param name="section">Catalog section receiving the new definition.</param>
    /// <returns>Default module ID prefix.</returns>
    public static string ResolveDefaultModuleIdPrefix(EnemyPatternModuleCatalogSection section)
    {
        switch (section)
        {
            case EnemyPatternModuleCatalogSection.CoreMovement:
                return "Module_Core_Grunt";

            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                return "Module_ShortRange_Grunt";

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                return "Module_Weapon_Shooter";

            default:
                return "Module_DropItems";
        }
    }

    /// <summary>
    /// Resolves the default display name used for one new module entry.
    /// </summary>
    /// <param name="section">Catalog section receiving the new definition.</param>
    /// <returns>Default display name.</returns>
    public static string ResolveDefaultModuleDisplayName(EnemyPatternModuleCatalogSection section)
    {
        switch (section)
        {
            case EnemyPatternModuleCatalogSection.CoreMovement:
                return "New Core Module";

            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                return "New Short-Range Module";

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                return "New Weapon Module";

            default:
                return "New Drop Module";
        }
    }

    /// <summary>
    /// Resolves the default module kind for one new module entry.
    /// </summary>
    /// <param name="section">Catalog section receiving the new definition.</param>
    /// <returns>Default module kind.</returns>
    public static EnemyPatternModuleKind ResolveDefaultModuleKind(EnemyPatternModuleCatalogSection section)
    {
        switch (section)
        {
            case EnemyPatternModuleCatalogSection.CoreMovement:
                return EnemyPatternModuleKind.Grunt;

            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                return EnemyPatternModuleKind.Grunt;

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                return EnemyPatternModuleKind.Shooter;

            default:
                return EnemyPatternModuleKind.DropItems;
        }
    }
    #endregion

    #endregion
}
