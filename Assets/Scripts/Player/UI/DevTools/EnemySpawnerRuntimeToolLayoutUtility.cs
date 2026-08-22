#if UNITY_EDITOR || NASHCORE_RUNTIME_SPAWNER_TOOL
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Provides layout refresh helpers for the runtime enemy spawner tool.
/// </summary>
public static class EnemySpawnerRuntimeToolLayoutUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Forces the rows scroll content and its parents to rebuild after pooled row activation changes.
    /// </summary>
    /// <param name="rowsContentRoot">Rows content transform controlled by the panel.</param>
    public static void ForceRebuildRows(Transform rowsContentRoot)
    {
        RectTransform contentRect = rowsContentRoot as RectTransform;

        if (contentRect == null)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        RectTransform currentRect = contentRect.parent as RectTransform;

        // Refresh a short parent chain so the scroll viewport receives the new content height immediately.
        for (int depth = 0; depth < 3; depth++)
        {
            if (currentRect == null)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(currentRect);
            currentRect = currentRect.parent as RectTransform;
        }
    }
    #endregion

    #endregion
}
#endif
