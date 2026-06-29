using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static PlayerHudUiAssetSetupSharedUtility;

/// <summary>
/// Builds the authored experience syringe row that keeps the runtime level label outside disabled legacy bars.
/// </summary>
internal static class PlayerHudExperienceLevelLabelSetupUtility
{
    #region Constants
    private const string PlayerExperienceRowName = "PlayerExperienceRow";
    private const string PlayerExperienceSyringeName = "PlayerExperienceSyringe";
    private const string LegacyPlayerExperienceBarName = "PlayerExperienceBar";
    private const string PlayerLevelTextName = "PlayerLevelText";
    private const string PlayerShieldSyringeName = "PlayerShieldSyringe";
    #endregion

    #region Methods

    #region Configuration
    /// <summary>
    /// Ensures the preauthored player experience row contains both the syringe and the level TMP label.
    /// </summary>
    /// <param name="playerBarsRoot">Loaded player bars prefab root or scene instance root.</param>
    /// <param name="sourceExperienceSyringe">Preauthored syringe used as the clone source when no experience syringe exists.</param>
    /// <returns>Resolved experience syringe row references.</returns>
    public static PlayerExperienceSyringeLayout Configure(GameObject playerBarsRoot,
                                                          PlayerSyringeBarView sourceExperienceSyringe)
    {
        if (playerBarsRoot == null || sourceExperienceSyringe == null)
            return default;

        RectTransform rowRoot = EnsureExperienceRow(playerBarsRoot);
        PlayerSyringeBarView experienceSyringe = EnsureExperienceSyringe(playerBarsRoot.transform,
                                                                         rowRoot,
                                                                         sourceExperienceSyringe,
                                                                         playerBarsRoot.layer);
        TMP_Text levelText = EnsureLevelText(playerBarsRoot.transform, rowRoot, playerBarsRoot.layer);

        ConfigureExperienceRow(rowRoot);
        ConfigureExperienceSyringe(experienceSyringe);
        ConfigureLevelText(levelText);
        MoveExperienceRowAfterShield(playerBarsRoot.transform, rowRoot);
        DisableLegacyExperienceBar(playerBarsRoot.transform);
        return new PlayerExperienceSyringeLayout(rowRoot, experienceSyringe, levelText);
    }

    /// <summary>
    /// Resolves or creates the horizontal row that sits inside the player bars vertical layout.
    /// </summary>
    /// <param name="playerBarsRoot">Player bars root that owns the row.</param>
    /// <returns>Configured experience row transform.</returns>
    private static RectTransform EnsureExperienceRow(GameObject playerBarsRoot)
    {
        Transform existingRow = FindChild(playerBarsRoot.transform, PlayerExperienceRowName);
        RectTransform rowRoot = existingRow as RectTransform;

        if (rowRoot != null)
        {
            rowRoot.SetParent(playerBarsRoot.transform, false);
            SetLayerRecursively(rowRoot.gameObject, playerBarsRoot.layer);
            return rowRoot;
        }

        GameObject rowObject = new GameObject(PlayerExperienceRowName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowObject.transform.SetParent(playerBarsRoot.transform, false);
        SetLayerRecursively(rowObject, playerBarsRoot.layer);
        return rowObject.GetComponent<RectTransform>();
    }

    /// <summary>
    /// Reuses the existing generated experience syringe or clones a source syringe into the row.
    /// </summary>
    /// <param name="playerBarsRoot">Root searched for existing generated syringe instances.</param>
    /// <param name="rowRoot">Experience row that owns the syringe.</param>
    /// <param name="sourceExperienceSyringe">Source syringe used when a generated one does not exist yet.</param>
    /// <param name="layer">Layer inherited by the experience row hierarchy.</param>
    /// <returns>Configured experience syringe view.</returns>
    private static PlayerSyringeBarView EnsureExperienceSyringe(Transform playerBarsRoot,
                                                                RectTransform rowRoot,
                                                                PlayerSyringeBarView sourceExperienceSyringe,
                                                                int layer)
    {
        PlayerSyringeBarView existingSyringe = FindComponentByName<PlayerSyringeBarView>(playerBarsRoot,
                                                                                         PlayerExperienceSyringeName);

        if (existingSyringe != null)
        {
            existingSyringe.transform.SetParent(rowRoot, false);
            SetLayerRecursively(existingSyringe.gameObject, layer);
            return existingSyringe;
        }

        return EnsureClonedSyringe(sourceExperienceSyringe, rowRoot, PlayerExperienceSyringeName, layer);
    }

    /// <summary>
    /// Reuses the legacy level TMP label before disabling its old parent, or creates one when the asset lost it.
    /// </summary>
    /// <param name="playerBarsRoot">Root searched for the level label.</param>
    /// <param name="rowRoot">Experience row that owns the level label.</param>
    /// <param name="layer">Layer inherited by the level label.</param>
    /// <returns>Configured level TMP label.</returns>
    private static TMP_Text EnsureLevelText(Transform playerBarsRoot, RectTransform rowRoot, int layer)
    {
        TMP_Text levelText = FindComponentByName<TMP_Text>(playerBarsRoot, PlayerLevelTextName);

        if (levelText == null)
        {
            GameObject labelObject = new GameObject(PlayerLevelTextName,
                                                    typeof(RectTransform),
                                                    typeof(CanvasRenderer),
                                                    typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(rowRoot, false);
            levelText = labelObject.GetComponent<TMP_Text>();
        }

        levelText.name = PlayerLevelTextName;
        levelText.transform.SetParent(rowRoot, false);
        SetLayerRecursively(levelText.gameObject, layer);
        return levelText;
    }

    /// <summary>
    /// Applies stable horizontal layout settings to the experience row without relying on runtime instantiation.
    /// </summary>
    /// <param name="rowRoot">Experience row receiving layout settings.</param>
    private static void ConfigureExperienceRow(RectTransform rowRoot)
    {
        if (rowRoot == null)
            return;

        ConfigureRectTransform(rowRoot,
                               new Vector2(440f, 58f),
                               new Vector2(0.5f, 0.5f),
                               new Vector2(0.5f, 0.5f),
                               new Vector2(0.5f, 0.5f),
                               Vector2.zero);
        HorizontalLayoutGroup layoutGroup = EnsureComponent<HorizontalLayoutGroup>(rowRoot.gameObject);
        layoutGroup.padding = new RectOffset(0, 0, 0, 0);
        layoutGroup.spacing = 10f;
        layoutGroup.childAlignment = TextAnchor.MiddleLeft;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childScaleWidth = false;
        layoutGroup.childScaleHeight = false;
        ConfigureLayoutElement(rowRoot.gameObject, 340f, 58f, 440f, 58f);
        EditorUtility.SetDirty(layoutGroup);
        EditorUtility.SetDirty(rowRoot);
    }

    /// <summary>
    /// Applies authored dimensions to the generated experience syringe inside the row.
    /// </summary>
    /// <param name="experienceSyringe">Experience syringe view receiving row-compatible layout values.</param>
    private static void ConfigureExperienceSyringe(PlayerSyringeBarView experienceSyringe)
    {
        if (experienceSyringe == null)
            return;

        RectTransform experienceRoot = experienceSyringe.Root != null
            ? experienceSyringe.Root
            : EnsureRectTransform(experienceSyringe.gameObject);

        ConfigureRectTransform(experienceRoot,
                               new Vector2(340f, 58f),
                               new Vector2(0.5f, 0.5f),
                               new Vector2(0.5f, 0.5f),
                               new Vector2(0.5f, 0.5f),
                               Vector2.zero);
        ConfigureLayoutElement(experienceSyringe.gameObject, 260f, 50f, 340f, 58f);
        experienceSyringe.transform.SetAsFirstSibling();
        experienceSyringe.gameObject.SetActive(true);
        EditorUtility.SetDirty(experienceSyringe);
    }

    /// <summary>
    /// Applies readable authored TMP settings to the level label placed at the right of the experience syringe.
    /// </summary>
    /// <param name="levelText">TMP label that displays the current player level at runtime.</param>
    private static void ConfigureLevelText(TMP_Text levelText)
    {
        if (levelText == null)
            return;

        RectTransform textTransform = EnsureRectTransform(levelText.gameObject);
        ConfigureRectTransform(textTransform,
                               new Vector2(78f, 32f),
                               new Vector2(0.5f, 0.5f),
                               new Vector2(0.5f, 0.5f),
                               new Vector2(0.5f, 0.5f),
                               Vector2.zero);
        ConfigureLayoutElement(levelText.gameObject, 64f, 28f, 78f, 32f);
        levelText.raycastTarget = false;
        levelText.textWrappingMode = TextWrappingModes.NoWrap;
        levelText.overflowMode = TextOverflowModes.Overflow;
        levelText.alignment = TextAlignmentOptions.Center;
        levelText.fontSize = Mathf.Max(1f, levelText.fontSize);

        if (string.IsNullOrWhiteSpace(levelText.text))
            levelText.text = "Lv 1";

        levelText.transform.SetAsLastSibling();
        levelText.gameObject.SetActive(true);
        EditorUtility.SetDirty(levelText);
        EditorUtility.SetDirty(textTransform);
    }

    /// <summary>
    /// Places the whole experience row after shield so the vertical layout ordering remains deterministic.
    /// </summary>
    /// <param name="playerBarsRoot">Root whose direct children are controlled by the vertical layout group.</param>
    /// <param name="rowRoot">Experience row that should appear below the shield bar.</param>
    private static void MoveExperienceRowAfterShield(Transform playerBarsRoot, RectTransform rowRoot)
    {
        Transform shieldSyringe = FindChild(playerBarsRoot, PlayerShieldSyringeName);

        if (shieldSyringe == null || rowRoot == null)
            return;

        rowRoot.SetSiblingIndex(shieldSyringe.GetSiblingIndex() + 1);
        EditorUtility.SetDirty(rowRoot);
    }

    /// <summary>
    /// Disables the old image-based experience bar after its level label has been moved to the new row.
    /// </summary>
    /// <param name="playerBarsRoot">Root searched for the legacy experience bar.</param>
    private static void DisableLegacyExperienceBar(Transform playerBarsRoot)
    {
        Transform legacyExperienceBar = FindChild(playerBarsRoot, LegacyPlayerExperienceBarName);

        if (legacyExperienceBar == null)
            return;

        legacyExperienceBar.gameObject.SetActive(false);
        EditorUtility.SetDirty(legacyExperienceBar.gameObject);
    }
    #endregion

    #endregion
}

/// <summary>
/// Contains the authored references produced while rebuilding the player experience syringe row.
/// </summary>
internal readonly struct PlayerExperienceSyringeLayout
{
    #region Properties
    public RectTransform RowRoot { get; }
    public PlayerSyringeBarView ExperienceSyringe { get; }
    public TMP_Text LevelText { get; }
    #endregion

    #region Methods

    #region Construction
    /// <summary>
    /// Creates an immutable description of the generated player experience row.
    /// </summary>
    /// <param name="rowRoot">Horizontal row that owns the experience syringe and level text.</param>
    /// <param name="experienceSyringe">Preauthored syringe driven by PlayerHealthBarsHudView.</param>
    /// <param name="levelText">Preauthored TMP label driven by HUDManager.</param>
    public PlayerExperienceSyringeLayout(RectTransform rowRoot,
                                         PlayerSyringeBarView experienceSyringe,
                                         TMP_Text levelText)
    {
        RowRoot = rowRoot;
        ExperienceSyringe = experienceSyringe;
        LevelText = levelText;
    }
    #endregion

    #endregion
}
