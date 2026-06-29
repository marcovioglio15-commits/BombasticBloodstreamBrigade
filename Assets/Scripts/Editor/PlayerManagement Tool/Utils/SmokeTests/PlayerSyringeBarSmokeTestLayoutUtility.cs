using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Provides shared smoke-test assertions for authored syringe HUD layout and mirrored-label settings.
/// </summary>
internal static class PlayerSyringeBarSmokeTestLayoutUtility
{
    #region Methods

    #region Boss Layout
    /// <summary>
    /// Validates that boss portrait and bars are controlled by a stable top-right horizontal layout root.
    /// </summary>
    /// <param name="contentRoot">Boss HUD horizontal layout root.</param>
    /// <param name="panelRoot">Boss bars panel child.</param>
    /// <param name="portraitRoot">Boss portrait child.</param>
    public static void ValidateBossHudLayout(RectTransform contentRoot,
                                             RectTransform panelRoot,
                                             RectTransform portraitRoot)
    {
        HorizontalLayoutGroup layoutGroup = contentRoot != null
            ? contentRoot.GetComponent<HorizontalLayoutGroup>()
            : null;

        if (layoutGroup == null)
            throw new InvalidOperationException("Boss HUD content root is missing its HorizontalLayoutGroup.");

        if (panelRoot.parent != contentRoot || portraitRoot.parent != contentRoot)
            throw new InvalidOperationException("Boss HUD panel and portrait must be direct children of BossHudContentRoot.");

        if (panelRoot.GetSiblingIndex() >= portraitRoot.GetSiblingIndex())
            throw new InvalidOperationException("Boss HUD layout order must be Panel then Portrait so the mirrored portrait stays on the right.");

        if (layoutGroup.childAlignment != TextAnchor.UpperRight ||
            layoutGroup.childControlWidth ||
            layoutGroup.childForceExpandWidth ||
            layoutGroup.childControlHeight ||
            layoutGroup.childForceExpandHeight)
        {
            throw new InvalidOperationException("Boss HUD HorizontalLayoutGroup must right-align fixed-size panel and portrait children.");
        }

        if (!IsLocalYRotationApproximately(panelRoot, 0f))
            throw new InvalidOperationException("Boss HUD panel must stay unrotated so horizontal layout remains aligned with the portrait.");

        RectTransform mirrorRoot = FindComponentByName<RectTransform>(panelRoot, "BossBarsMirrorRoot");

        if (mirrorRoot == null || mirrorRoot.parent != panelRoot)
            throw new InvalidOperationException("Boss HUD panel is missing its direct mirrored bars root.");

        if (!IsPivotApproximately(mirrorRoot, new Vector2(0f, 1f)))
            throw new InvalidOperationException("Boss HUD mirrored bars root must pivot from its left edge while positioned at the panel right edge.");

        if (!IsAnchoredPositionApproximately(mirrorRoot, new Vector2(269f, 0f)))
            throw new InvalidOperationException("Boss HUD mirrored bars root must be anchored to the panel right edge.");

        if (!IsLocalYRotationApproximately(mirrorRoot, 190f))
            throw new InvalidOperationException("Boss HUD mirrored bars root must keep its 190-degree Y inclination without using negative scale.");

        ValidateBossSyringeParent(mirrorRoot, "BossHealthSyringe");
        ValidateBossSyringeParent(mirrorRoot, "BossShieldSyringe");

        if (!IsLocalYRotationApproximately(portraitRoot, 180f))
            throw new InvalidOperationException("Boss HUD portrait must keep its mirrored 180-degree Y rotation without using negative scale.");

        TMP_Text bossNameText = FindComponentByName<TMP_Text>(mirrorRoot, "BossName");

        if (bossNameText != null && !IsLocalYRotationApproximately(bossNameText.rectTransform, 180f))
            throw new InvalidOperationException("Boss HUD name text must be counter-rotated so mirrored panel text stays readable.");
    }
    #endregion

    #region Syringe Labels
    /// <summary>
    /// Validates that one boss syringe is directly owned by the visual mirror root.
    /// </summary>
    /// <param name="mirrorRoot">Mirrored boss bars root expected to own the syringe.</param>
    /// <param name="syringeName">Expected syringe GameObject name.</param>
    private static void ValidateBossSyringeParent(RectTransform mirrorRoot, string syringeName)
    {
        PlayerSyringeBarView syringeView = FindComponentByName<PlayerSyringeBarView>(mirrorRoot, syringeName);

        if (syringeView == null || syringeView.transform.parent != mirrorRoot)
            throw new InvalidOperationException(syringeName + " must be a direct child of BossBarsMirrorRoot.");
    }

    /// <summary>
    /// Validates the authored mirrored-label flag used by positive-scale rotated syringe panels.
    /// </summary>
    /// <param name="syringeView">Syringe view inspected through serialized data.</param>
    /// <param name="expectedValue">Expected flag value for the inspected view.</param>
    /// <param name="label">Human-readable object label used by diagnostics.</param>
    public static void ValidateSyringeLabelCounterRotation(PlayerSyringeBarView syringeView,
                                                           bool expectedValue,
                                                           string label)
    {
        if (syringeView == null)
            throw new InvalidOperationException(label + " is missing while validating mirrored label rotation.");

        SerializedObject serializedObject = new SerializedObject(syringeView);
        SerializedProperty property = serializedObject.FindProperty("counterRotateLabelsForMirroredRotation");

        if (property == null)
            throw new InvalidOperationException(label + " is missing the mirrored label rotation setting.");

        if (property.boolValue == expectedValue)
            return;

        throw new InvalidOperationException(label + " has an invalid mirrored label rotation setting.");
    }

    /// <summary>
    /// Validates that player health, shield, and experience syringes do not counter-rotate labels by default.
    /// </summary>
    /// <param name="playerBarsRoot">Player bars prefab or scene root containing the three authored syringes.</param>
    public static void ValidatePlayerBarsLabelCounterRotation(Transform playerBarsRoot)
    {
        ValidateSyringeLabelCounterRotation(FindComponentByName<PlayerSyringeBarView>(playerBarsRoot,
                                                                                      "PlayerHealthSyringe"),
                                            false,
                                            "Player Health Syringe");
        ValidateSyringeLabelCounterRotation(FindComponentByName<PlayerSyringeBarView>(playerBarsRoot,
                                                                                      "PlayerShieldSyringe"),
                                            false,
                                            "Player Shield Syringe");
        ValidateSyringeLabelCounterRotation(FindComponentByName<PlayerSyringeBarView>(playerBarsRoot,
                                                                                      "PlayerExperienceSyringe"),
                                            false,
                                            "Player Experience Syringe");
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Finds the first component of a given type whose GameObject has the requested name.
    /// </summary>
    /// <param name="root">Hierarchy root used for the search.</param>
    /// <param name="targetName">GameObject name to match.</param>
    /// <typeparam name="T">Component type to resolve.</typeparam>
    /// <returns>The matching component, or null when no matching child exists.</returns>
    private static T FindComponentByName<T>(Transform root, string targetName) where T : Component
    {
        T[] components = root.GetComponentsInChildren<T>(true);

        for (int index = 0; index < components.Length; index++)
        {
            if (components[index].name == targetName)
                return components[index];
        }

        return null;
    }

    /// <summary>
    /// Checks whether a RectTransform keeps the expected local Y rotation.
    /// </summary>
    /// <param name="rectTransform">Transform inspected for authored mirror rotation.</param>
    /// <param name="expectedY">Expected local Y angle in degrees.</param>
    /// <returns>True when the current local Y angle matches within editor serialization tolerance.</returns>
    private static bool IsLocalYRotationApproximately(RectTransform rectTransform, float expectedY)
    {
        return rectTransform != null && Mathf.Abs(Mathf.DeltaAngle(rectTransform.localEulerAngles.y, expectedY)) <= 0.5f;
    }

    /// <summary>
    /// Checks whether a RectTransform pivot matches the expected normalized point.
    /// </summary>
    /// <param name="rectTransform">Transform inspected for authored pivot values.</param>
    /// <param name="expectedPivot">Expected normalized pivot position.</param>
    /// <returns>True when both pivot coordinates match within editor serialization tolerance.</returns>
    private static bool IsPivotApproximately(RectTransform rectTransform, Vector2 expectedPivot)
    {
        return rectTransform != null &&
               Mathf.Abs(rectTransform.pivot.x - expectedPivot.x) <= 0.001f &&
               Mathf.Abs(rectTransform.pivot.y - expectedPivot.y) <= 0.001f;
    }

    /// <summary>
    /// Checks whether a RectTransform anchored position matches the expected UI point.
    /// </summary>
    /// <param name="rectTransform">Transform inspected for authored anchored position.</param>
    /// <param name="expectedPosition">Expected anchored position in UI units.</param>
    /// <returns>True when both coordinates match within editor serialization tolerance.</returns>
    private static bool IsAnchoredPositionApproximately(RectTransform rectTransform, Vector2 expectedPosition)
    {
        return rectTransform != null &&
               Mathf.Abs(rectTransform.anchoredPosition.x - expectedPosition.x) <= 0.001f &&
               Mathf.Abs(rectTransform.anchoredPosition.y - expectedPosition.y) <= 0.001f;
    }
    #endregion

    #endregion
}
