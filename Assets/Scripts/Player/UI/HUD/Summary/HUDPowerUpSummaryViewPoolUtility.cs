using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Updates the preauthored power-up icon and statistic-row pools without allocating or instantiating UI at runtime.
/// </summary>
public static class HUDPowerUpSummaryViewPoolUtility
{
    #region Methods

    #region Icon Pool
    /// <summary>
    /// Applies the baked shared style to every preauthored icon slot in one category.
    /// </summary>
    /// <param name="views">Icon-slot array receiving the shared visual configuration.</param>
    /// <param name="config">Baked summary configuration containing icon and counter styles.</param>
    public static void ApplyIconStyles(HUDPowerUpSummaryIconView[] views,
                                       in GamePowerUpSummaryRuntimeConfig config)
    {
        if (views == null)
            return;

        // Update each authored slot without changing the pool structure.
        for (int viewIndex = 0; viewIndex < views.Length; viewIndex++)
        {
            HUDPowerUpSummaryIconView view = views[viewIndex];

            if (view != null)
                view.ApplyStyle(in config);
        }
    }

    /// <summary>
    /// Writes one collected power-up category into its preauthored slot pool and hides remaining slots.
    /// </summary>
    /// <param name="catalog">Authoritative player power-up catalog.</param>
    /// <param name="unlockKind">Active or passive category requested for this pool.</param>
    /// <param name="views">Preauthored destination icon slots.</param>
    /// <param name="maximumVisible">Preset-limited visible slot count.</param>
    /// <param name="config">Baked summary configuration containing counter presentation.</param>
    /// <returns>Number of visible entries written to the pool.</returns>
    public static int FillIconViews(DynamicBuffer<PlayerPowerUpUnlockCatalogElement> catalog,
                                    PlayerPowerUpUnlockKind unlockKind,
                                    HUDPowerUpSummaryIconView[] views,
                                    int maximumVisible,
                                    in GamePowerUpSummaryRuntimeConfig config)
    {
        if (views == null)
            return 0;

        int viewIndex = 0;
        int viewLimit = math.min(views.Length, maximumVisible);
        string counterPrefix = config.CounterPrefix.ToString();

        // Populate visible slots from collected entries in stable catalog order.
        for (int catalogIndex = 0; catalogIndex < catalog.Length && viewIndex < viewLimit; catalogIndex++)
        {
            PlayerPowerUpUnlockCatalogElement entry = catalog[catalogIndex];

            if (entry.UnlockKind != unlockKind || entry.CurrentUnlockCount <= 0)
                continue;

            HUDPowerUpSummaryIconView view = views[viewIndex];

            if (view != null)
                view.Show(entry.PowerUpId.ToString(),
                          entry.CurrentUnlockCount,
                          counterPrefix,
                          config.ShowSingleCollectionCount != 0);

            viewIndex += 1;
        }

        // Clear unused slots so stale catalog entries cannot remain visible.
        for (int hiddenIndex = viewIndex; hiddenIndex < views.Length; hiddenIndex++)
        {
            HUDPowerUpSummaryIconView view = views[hiddenIndex];

            if (view != null)
                view.Hide();
        }

        return viewIndex;
    }

    /// <summary>
    /// Hides every icon in one preauthored category pool.
    /// </summary>
    /// <param name="views">Icon-slot array to clear.</param>
    public static void HideAllIconViews(HUDPowerUpSummaryIconView[] views)
    {
        if (views == null)
            return;

        // Clear each authored slot while retaining its reusable view components.
        for (int viewIndex = 0; viewIndex < views.Length; viewIndex++)
        {
            HUDPowerUpSummaryIconView view = views[viewIndex];

            if (view != null)
                view.Hide();
        }
    }
    #endregion

    #region Statistic Pool
    /// <summary>
    /// Hides preauthored statistic rows after the configured definition range.
    /// </summary>
    /// <param name="rows">Preauthored statistic-row pool.</param>
    /// <param name="firstUnusedIndex">First row index not owned by a baked definition.</param>
    public static void HideUnusedStatisticRows(HUDPowerUpSummaryStatisticRowView[] rows,
                                               int firstUnusedIndex)
    {
        if (rows == null)
            return;

        // Disable only the unused tail so configured rows remain untouched.
        for (int rowIndex = math.max(0, firstUnusedIndex); rowIndex < rows.Length; rowIndex++)
        {
            HUDPowerUpSummaryStatisticRowView row = rows[rowIndex];

            if (row != null)
                row.Hide();
        }
    }
    #endregion

    #endregion
}
