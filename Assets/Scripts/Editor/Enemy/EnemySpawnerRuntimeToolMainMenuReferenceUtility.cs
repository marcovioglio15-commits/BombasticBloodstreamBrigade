using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Resolves generated main-menu runtime spawner tool references without relying on ambiguous object names.
/// </summary>
public static class EnemySpawnerRuntimeToolMainMenuReferenceUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the dedicated row scroll content instead of generic dropdown content objects.
    /// </summary>
    /// <param name="root">Panel root transform to search.</param>
    /// <returns>Rows content transform or null when the scroll view is incomplete.</returns>
    public static Transform ResolveRowsContentRoot(Transform root)
    {
        ScrollRect rowsScroll = FindChildComponent<ScrollRect>(root, "RowsScroll");
        return rowsScroll != null && rowsScroll.content != null ? rowsScroll.content : null;
    }

    /// <summary>
    /// Finds a child transform by name in a hierarchy.
    /// </summary>
    /// <param name="root">Root transform to search.</param>
    /// <param name="name">Child object name.</param>
    /// <returns>Resolved transform or null.</returns>
    public static Transform FindChild(Transform root, string name)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        // Scan the hierarchy once; setup runs in editor only, not during gameplay.
        for (int childIndex = 0; childIndex < children.Length; childIndex++)
        {
            if (children[childIndex].name == name)
                return children[childIndex];
        }

        return null;
    }

    /// <summary>
    /// Finds a child component by GameObject name in a hierarchy.
    /// </summary>
    /// <param name="root">Root transform to search.</param>
    /// <param name="name">Child object name.</param>
    /// <typeparam name="T">Component type to resolve.</typeparam>
    /// <returns>Resolved component or null.</returns>
    public static T FindChildComponent<T>(Transform root, string name) where T : Component
    {
        Transform child = FindChild(root, name);
        return child != null ? child.GetComponent<T>() : null;
    }

    /// <summary>
    /// Assigns a font asset to all TextMeshPro labels in a hierarchy.
    /// </summary>
    /// <param name="root">Root transform.</param>
    /// <param name="fontAsset">Font asset to assign.</param>
    public static void AssignFont(Transform root, TMP_FontAsset fontAsset)
    {
        if (root == null || fontAsset == null)
            return;

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);

        // Normalize generated labels to the existing main-menu font.
        for (int textIndex = 0; textIndex < texts.Length; textIndex++)
            texts[textIndex].font = fontAsset;
    }
    #endregion

    #endregion
}
