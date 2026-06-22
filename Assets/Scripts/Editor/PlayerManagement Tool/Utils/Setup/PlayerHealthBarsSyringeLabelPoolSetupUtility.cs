using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Restores the fixed authored TMP label capacity required by syringe health bar prefabs.
/// </summary>
public static class PlayerHealthBarsSyringeLabelPoolSetupUtility
{
    #region Constants
    private const string PlayerBarsPrefabPath = "Assets/Prefabs/UI/PlayerBars VerticalBox.prefab";
    #endregion

    #region Methods

    #region Entry Point
    /// <summary>
    /// Batchmode entry point used after merges when prefab-authored syringe labels were reduced.
    /// </summary>
    public static void Run()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerBarsPrefabPath);

        try
        {
            PlayerSyringeBarLabelPool[] labelPools = prefabRoot.GetComponentsInChildren<PlayerSyringeBarLabelPool>(true);

            if (labelPools.Length != 2)
                throw new InvalidOperationException("Player bars prefab must contain exactly two syringe label pools.");

            for (int index = 0; index < labelPools.Length; index++)
                EnsureLabelPoolCapacity(labelPools[index]);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerBarsPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PlayerHealthBarsSyringeLabelPoolSetupUtility] Player syringe label pools restored.");
    }
    #endregion

    #region Label Pool
    /// <summary>
    /// Ensures one authored label pool contains the fixed number of reusable TMP labels.
    /// </summary>
    /// <param name="labelPool">Label pool component attached to the GraduationLabels root.</param>
    private static void EnsureLabelPoolCapacity(PlayerSyringeBarLabelPool labelPool)
    {
        if (labelPool == null)
            return;

        RectTransform labelRoot = labelPool.transform as RectTransform;

        if (labelRoot == null)
            throw new InvalidOperationException("Syringe label pool is not hosted by a RectTransform.");

        List<TMP_Text> labels = ResolveExistingLabels(labelRoot);
        TMP_Text template = labels.Count > 0 ? labels[0] : null;

        for (int index = labels.Count; index < PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity; index++)
            labels.Add(CreateLabel(labelRoot, template, index));

        labels.Sort(CompareLabelsByName);
        WriteSerializedLabels(labelPool, labels);
        EditorUtility.SetDirty(labelPool);
    }

    /// <summary>
    /// Resolves the currently authored TMP labels directly under a label root.
    /// </summary>
    /// <param name="labelRoot">RectTransform that owns label children.</param>
    /// <returns>Existing TMP labels sorted later by stable label name.</returns>
    private static List<TMP_Text> ResolveExistingLabels(RectTransform labelRoot)
    {
        List<TMP_Text> labels = new List<TMP_Text>(PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity);

        for (int index = 0; index < labelRoot.childCount; index++)
        {
            TMP_Text label = labelRoot.GetChild(index).GetComponent<TMP_Text>();

            if (label != null)
                labels.Add(label);
        }

        return labels;
    }

    /// <summary>
    /// Creates one inactive-by-runtime TMP label using the first authored label as style reference.
    /// </summary>
    /// <param name="labelRoot">RectTransform parent that owns all label children.</param>
    /// <param name="template">Optional existing label used to copy authoring style.</param>
    /// <param name="index">Zero-based authored label index.</param>
    /// <returns>The newly authored TMP label.</returns>
    private static TMP_Text CreateLabel(RectTransform labelRoot, TMP_Text template, int index)
    {
        GameObject labelObject = new GameObject(BuildLabelName(index),
                                                typeof(RectTransform),
                                                typeof(CanvasRenderer),
                                                typeof(TextMeshProUGUI));
        labelObject.layer = labelRoot.gameObject.layer;
        labelObject.transform.SetParent(labelRoot, false);

        RectTransform rectTransform = labelObject.GetComponent<RectTransform>();
        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        ConfigureLabelRect(rectTransform, template != null ? template.rectTransform : null, index);
        ConfigureLabelStyle(label, template, index);
        EditorUtility.SetDirty(labelObject);
        return label;
    }

    /// <summary>
    /// Writes the fixed label references back into the private serialized pool list.
    /// </summary>
    /// <param name="labelPool">Label pool receiving the ordered references.</param>
    /// <param name="labels">Resolved labels sorted by stable authored name.</param>
    private static void WriteSerializedLabels(PlayerSyringeBarLabelPool labelPool, List<TMP_Text> labels)
    {
        SerializedObject serializedObject = new SerializedObject(labelPool);
        SerializedProperty labelsProperty = serializedObject.FindProperty("labels");

        if (labelsProperty == null)
            throw new InvalidOperationException("PlayerSyringeBarLabelPool.labels serialized field was not found.");

        labelsProperty.arraySize = PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity;

        for (int index = 0; index < PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity; index++)
            labelsProperty.GetArrayElementAtIndex(index).objectReferenceValue = labels[index];

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
    #endregion

    #region Label Configuration
    /// <summary>
    /// Applies deterministic authored placement to a label that will be repositioned by runtime rebuilds.
    /// </summary>
    /// <param name="rectTransform">RectTransform receiving initial authoring values.</param>
    /// <param name="template">Optional template rect used to preserve local sizing.</param>
    /// <param name="index">Zero-based authored label index.</param>
    private static void ConfigureLabelRect(RectTransform rectTransform, RectTransform template, int index)
    {
        float anchorX = PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity <= 1
            ? 0f
            : (float)index / (PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity - 1);
        Vector2 anchor = new Vector2(anchorX, template != null ? template.anchorMin.y : 0.49f);

        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.pivot = template != null ? template.pivot : new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = template != null ? template.sizeDelta : new Vector2(72f, 20f);
        rectTransform.anchoredPosition = template != null ? template.anchoredPosition : new Vector2(0f, -2f);
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// Copies the relevant TMP authoring style while keeping runtime-controlled content deterministic.
    /// </summary>
    /// <param name="label">TMP label receiving the style values.</param>
    /// <param name="template">Optional existing label used as style source.</param>
    /// <param name="index">Zero-based authored label index.</param>
    private static void ConfigureLabelStyle(TMP_Text label, TMP_Text template, int index)
    {
        label.text = index.ToString();
        label.raycastTarget = template != null && template.raycastTarget;
        label.alignment = template != null ? template.alignment : TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;

        if (template == null)
            return;

        label.font = template.font;
        label.fontSharedMaterial = template.fontSharedMaterial;
        label.fontSize = template.fontSize;
        label.fontStyle = template.fontStyle;
        label.color = template.color;
        label.enableAutoSizing = template.enableAutoSizing;
        label.fontSizeMin = template.fontSizeMin;
        label.fontSizeMax = template.fontSizeMax;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Compares authored labels by stable GameObject name.
    /// </summary>
    /// <param name="left">Left label reference.</param>
    /// <param name="right">Right label reference.</param>
    /// <returns>Negative, zero, or positive comparison result for sorting.</returns>
    private static int CompareLabelsByName(TMP_Text left, TMP_Text right)
    {
        return string.CompareOrdinal(left != null ? left.name : string.Empty,
                                     right != null ? right.name : string.Empty);
    }

    /// <summary>
    /// Builds the stable authored label name used by setup and smoke validation.
    /// </summary>
    /// <param name="index">Zero-based authored label index.</param>
    /// <returns>Stable label GameObject name.</returns>
    private static string BuildLabelName(int index)
    {
        return "Label_" + index.ToString("00");
    }
    #endregion

    #endregion
}
