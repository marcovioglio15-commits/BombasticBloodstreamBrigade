using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Maintains the default developer-access Input Action reference in Settings Manager presets.
/// </summary>
public static class GameDataCollectionProjectSetupUtility
{
    #region Constants
    public const string DefaultPresetFolder = "Assets/Scriptable Objects/Game/Data Collection";
    public const string DefaultPresetPath =
        "Assets/Scriptable Objects/Game/Data Collection/GameDataCollectionManagerPreset.asset";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads or creates the canonical global Data Collection Manager preset.
    /// </summary>
    /// <returns>Persistent preset used by the default Game Master configuration.</returns>
    public static GameDataCollectionManagerPreset EnsureDefaultPreset()
    {
        GameDataCollectionManagerPreset preset =
            AssetDatabase.LoadAssetAtPath<GameDataCollectionManagerPreset>(DefaultPresetPath);

        if (preset == null)
        {
            GameManagementAssetUtility.EnsureFolder(DefaultPresetFolder);
            preset = ScriptableObject.CreateInstance<GameDataCollectionManagerPreset>();
            preset.name = "GameDataCollectionManagerPreset";
            preset.EnsureInitialized();
            AssetDatabase.CreateAsset(preset, DefaultPresetPath);
        }

        preset.EnsureInitialized();
        EditorUtility.SetDirty(preset);
        return preset;
    }

    /// <summary>
    /// Creates one draft-aware Data Collection Manager preset for assignment from Game Management Tool.
    /// </summary>
    /// <param name="presetName">Requested base filename.</param>
    /// <returns>New initialized preset asset.</returns>
    public static GameDataCollectionManagerPreset CreatePresetAsset(string presetName)
    {
        string normalizedName = GameManagementAssetUtility.NormalizeAssetName(presetName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "GameDataCollectionManagerPreset";

        return GameManagementStandalonePresetAssetUtility.CreateAsset<GameDataCollectionManagerPreset>(
            DefaultPresetFolder,
            normalizedName,
            preset => preset.EnsureInitialized());
    }

    /// <summary>
    /// Assigns the stable Reveal Dev Actions action ID only when the current reference is empty or unresolved.
    /// </summary>
    /// <param name="preset">Settings Manager preset receiving a valid action reference.</param>
    public static void EnsureDefaultInputActionReference(GameSettingsManagerPreset preset)
    {
        if (preset == null)
            return;

        InputActionAsset inputAsset = PlayerInputActionsAssetUtility.LoadOrCreateAsset();

        if (inputAsset == null)
            return;

        SerializedObject serializedPreset = new SerializedObject(preset);
        serializedPreset.Update();
        SerializedProperty actionProperty = serializedPreset.FindProperty(
            "dataCollectionSettings.revealDevActionsActionId");

        if (actionProperty == null)
            return;

        if (!string.IsNullOrWhiteSpace(actionProperty.stringValue) &&
            inputAsset.FindAction(actionProperty.stringValue, false) != null)
            return;

        InputAction action = inputAsset.FindAction(GameDataCollectionSettings.DefaultRevealDevActionsActionName, false);

        if (action == null)
            return;

        actionProperty.stringValue = action.id.ToString();
        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(preset);
    }
    #endregion

    #endregion
}
