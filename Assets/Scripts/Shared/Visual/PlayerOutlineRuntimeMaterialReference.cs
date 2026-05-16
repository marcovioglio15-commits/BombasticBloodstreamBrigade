using UnityEngine;

/// <summary>
/// References the material used by the player outline RenderObjects feature so gameplay and editor tools can drive it from presets.
/// </summary>
[CreateAssetMenu(fileName = "PlayerOutlineRuntimeMaterialReference", menuName = "Visual/Player Outline Runtime Material Reference")]
public sealed class PlayerOutlineRuntimeMaterialReference : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Runtime Material")]
    [Tooltip("Material used by the player URP outline RenderObjects feature. This stays an implementation detail and is driven from the player visual preset.")]
    [SerializeField] private Material outlineMaterial;
    #endregion

    #endregion

    #region Properties
    public Material OutlineMaterial
    {
        get
        {
            return outlineMaterial;
        }
    }
    #endregion

    #region Methods

    #if UNITY_EDITOR
    #region Editor Methods
    /// <summary>
    /// Assigns the runtime outline material reference used by the editor/runtime sync utility.
    /// </summary>
    /// <param name="materialValue">Material referenced by the player outline pipeline.</param>
    public void SetOutlineMaterial(Material materialValue)
    {
        outlineMaterial = materialValue;
    }
    #endregion
    #endif

    #endregion
}
