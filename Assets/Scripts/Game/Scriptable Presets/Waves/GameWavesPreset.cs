using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores one weighted enemy master-preset candidate inside a brush category.
/// </summary>
[Serializable]
public sealed class EnemyBrushCategoryEntry
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enemy master preset that can be selected when a painted cell uses the containing brush category.")]
    [SerializeField]
    private EnemyMasterPreset masterPreset;

    [Tooltip("Inclusive minimum coefficient value that makes this enemy candidate eligible.")]
    [SerializeField]
    private float minimumDifficulty;

    [Tooltip("Inclusive maximum coefficient value that makes this enemy candidate eligible.")]
    [SerializeField]
    private float maximumDifficulty = 100f;

    [Tooltip("Relative deterministic selection weight used among all eligible candidates in the category.")]
    [SerializeField]
    private float selectionWeight = 1f;
    #endregion

    #endregion

    #region Properties
    public EnemyMasterPreset MasterPreset => masterPreset;
    public float MinimumDifficulty => minimumDifficulty;
    public float MaximumDifficulty => maximumDifficulty;
    public float SelectionWeight => selectionWeight;
    #endregion
}

/// <summary>
/// Defines one reusable scene-paint brush backed by weighted difficulty-aware enemy candidates.
/// </summary>
[Serializable]
public sealed class EnemyBrushCategoryDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable category identifier stored by painted wave cells instead of a direct enemy preset reference.")]
    [SerializeField]
    private string technicalId;

    [Tooltip("Designer-facing category name displayed by the scene brush toolbar.")]
    [SerializeField]
    private string displayName = "Enemy Category";

    [Tooltip("Short design note describing the combat role represented by this category.")]
    [SerializeField]
    private string description;

    [Tooltip("Color used by the embedded scene overlay and optional scene gizmos for this category.")]
    [SerializeField]
    private Color brushColor = new Color(1f, 0.35f, 0.35f, 0.9f);

    [Tooltip("Difficulty coefficient used to filter and weight candidate enemy presets. Empty uses every candidate range against zero.")]
    [SerializeField]
    private string difficultyCoefficientId;

    [Tooltip("Weighted enemy master-preset candidates resolved for every logical spawn painted with this category.")]
    [SerializeField]
    private List<EnemyBrushCategoryEntry> entries = new List<EnemyBrushCategoryEntry>();
    #endregion

    #endregion

    #region Properties
    public string TechnicalId => technicalId;
    public string DisplayName => displayName;
    public string Description => description;
    public Color BrushColor => brushColor;
    public string DifficultyCoefficientId => difficultyCoefficientId;
    public IReadOnlyList<EnemyBrushCategoryEntry> Entries => entries;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Restores stable identity and candidate storage without changing authored weights or ranges.
    /// </summary>
    public void EnsureInitialized()
    {
        if (string.IsNullOrWhiteSpace(technicalId))
            technicalId = Guid.NewGuid().ToString("N");

        if (entries == null)
            entries = new List<EnemyBrushCategoryEntry>();
    }
    #endregion

    #endregion
}

/// <summary>
/// Links one managed room scene, its single ECS SubScene and the wave preset edited through the embedded preview.
/// </summary>
[Serializable]
public sealed class GameWaveSceneDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Designer-facing room label displayed by the Waves scene selector.")]
    [SerializeField]
    private string displayName = "Room Waves";

    [Tooltip("Project-relative path to the managed main room scene shown in the embedded preview.")]
    [SerializeField]
    private string mainScenePath;

    [Tooltip("Stable Unity asset GUID of the managed main room scene.")]
    [SerializeField]
    private string mainSceneGuid;

    [Tooltip("Project-relative path to the single ECS SubScene containing the room enemy spawner.")]
    [SerializeField]
    private string subScenePath;

    [Tooltip("Stable Unity asset GUID of the ECS SubScene containing the room enemy spawner.")]
    [SerializeField]
    private string subSceneGuid;

    [Tooltip("Wave preset authored for the single enemy spawner owned by this room SubScene.")]
    [SerializeField]
    private EnemyWavePreset wavePreset;

#if UNITY_EDITOR
    [Tooltip("Editor-only main scene asset used to synchronize scene path, GUID and preview selection.")]
    [SerializeField]
    private UnityEditor.SceneAsset mainSceneAsset;
#endif
    #endregion

    #endregion

    #region Properties
    public string DisplayName => displayName;
    public string MainScenePath => mainScenePath;
    public string MainSceneGuid => mainSceneGuid;
    public string SubScenePath => subScenePath;
    public string SubSceneGuid => subSceneGuid;
    public EnemyWavePreset WavePreset => wavePreset;
    #endregion
}

/// <summary>
/// Owns reusable enemy brush categories and room-to-wave mappings edited by the Game Management Tool.
/// </summary>
[CreateAssetMenu(fileName = "GameWavesPreset", menuName = "Game/Waves Preset", order = 28)]
public sealed class GameWavesPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Stable preset identifier used by Game Management draft tracking and wave assets.")]
    [SerializeField]
    private string presetId;

    [Tooltip("Designer-facing preset name displayed by the Waves sub-preset panel.")]
    [SerializeField]
    private string presetName = "New Waves Preset";

    [Tooltip("Short description of the rooms and enemy roles configured by this preset.")]
    [SerializeField]
    private string description;

    [Tooltip("Optional semantic version used to communicate wave-layout revisions.")]
    [SerializeField]
    private string version = "1.0.0";

    [Header("Brush Categories")]
    [Tooltip("Reusable weighted enemy categories painted into waves instead of direct master preset references.")]
    [SerializeField]
    private List<EnemyBrushCategoryDefinition> brushCategories = new List<EnemyBrushCategoryDefinition>();

    [Header("Room Waves")]
    [Tooltip("Managed room scenes linked to exactly one editable ECS SubScene and one enemy wave preset.")]
    [SerializeField]
    private List<GameWaveSceneDefinition> sceneMappings = new List<GameWaveSceneDefinition>();
    #endregion

    #endregion

    #region Properties
    public string PresetId => presetId;
    public string PresetName => presetName;
    public string Description => description;
    public string Version => version;
    public IReadOnlyList<EnemyBrushCategoryDefinition> BrushCategories => brushCategories;
    public IReadOnlyList<GameWaveSceneDefinition> SceneMappings => sceneMappings;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Restores stable identity and nested collections without correcting invalid authored tuning.
    /// </summary>
    public void EnsureInitialized()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (brushCategories == null)
            brushCategories = new List<EnemyBrushCategoryDefinition>();

        if (sceneMappings == null)
            sceneMappings = new List<GameWaveSceneDefinition>();

        for (int categoryIndex = 0; categoryIndex < brushCategories.Count; categoryIndex++)
        {
            if (brushCategories[categoryIndex] != null)
                brushCategories[categoryIndex].EnsureInitialized();
        }
    }

    /// <summary>
    /// Resolves one brush category by its stable case-insensitive technical identifier.
    /// </summary>
    /// <param name="technicalId">Category identifier stored by a painted wave cell.</param>
    /// <param name="category">Matching category when found.</param>
    /// <returns>True when a non-null matching category exists.</returns>
    public bool TryFindBrushCategory(string technicalId, out EnemyBrushCategoryDefinition category)
    {
        category = null;

        if (string.IsNullOrWhiteSpace(technicalId) || brushCategories == null)
            return false;

        for (int categoryIndex = 0; categoryIndex < brushCategories.Count; categoryIndex++)
        {
            EnemyBrushCategoryDefinition candidate = brushCategories[categoryIndex];

            if (candidate == null || !string.Equals(candidate.TechnicalId, technicalId, StringComparison.OrdinalIgnoreCase))
                continue;

            category = candidate;
            return true;
        }

        return false;
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Maintains stable identity and required collection storage after editor changes.
    /// </summary>
    private void OnValidate()
    {
        EnsureInitialized();
    }
    #endregion

    #endregion
}
