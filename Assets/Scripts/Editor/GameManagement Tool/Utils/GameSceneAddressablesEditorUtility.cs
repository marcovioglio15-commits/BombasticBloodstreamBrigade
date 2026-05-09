using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

/// <summary>
/// Provides editor-only Addressables setup helpers for managed top-level scenes.
/// /params None.
/// /returns None.
/// </summary>
public static class GameSceneAddressablesEditorUtility
{
    #region Constants
    private const string SceneGroupName = "NashCore Managed Scenes";
    private const string SceneLabel = "ManagedScene";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates Addressables settings and entries for every non-bootstrap scene with an authored key.
    /// /params preset Scene Manager preset containing scene table metadata.
    /// /returns True when settings or entries were updated.
    /// </summary>
    public static bool EnsureSceneEntries(GameSceneManagerPreset preset)
    {
        if (preset == null || preset.SceneDefinitions == null)
            return false;

        AddressableAssetSettings settings = GetOrCreateSettings();

        if (settings == null)
            return false;

        AddressableAssetGroup sceneGroup = GetOrCreateSceneGroup(settings);
        bool changed = EnsureBuildWithPlayer(settings);

        for (int index = 0; index < preset.SceneDefinitions.Count; index++)
        {
            GameSceneDefinition sceneDefinition = preset.SceneDefinitions[index];

            if (sceneDefinition == null)
                continue;

            if (!ShouldRegisterScene(sceneDefinition))
                continue;

            if (EnsureSceneEntry(settings, sceneGroup, sceneDefinition))
                changed = true;
        }

        if (changed)
        {
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true);
            AssetDatabase.SaveAssets();
        }

        return changed;
    }

    /// <summary>
    /// Adds non-mutating warnings for the Addressables settings and entries required by one Scene Manager preset.
    /// /params preset Scene Manager preset to inspect.
    /// /params warnings Mutable warning output list.
    /// /returns None.
    /// </summary>
    public static void CollectWarnings(GameSceneManagerPreset preset, List<string> warnings)
    {
        if (preset == null || warnings == null || preset.LoadBackend != GameSceneLoadBackend.Addressables)
            return;

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

        if (settings == null)
        {
            warnings.Add("Addressables settings are missing. Run Sync Addressable Scenes from the Scene Manager Addressables section.");
            return;
        }

        for (int index = 0; index < preset.SceneDefinitions.Count; index++)
        {
            GameSceneDefinition sceneDefinition = preset.SceneDefinitions[index];

            if (sceneDefinition == null || !ShouldRegisterScene(sceneDefinition))
                continue;

            ValidateSceneEntry(settings, sceneDefinition, warnings);
        }
    }
    #endregion

    #region Settings
    /// <summary>
    /// Loads or creates the project Addressables settings asset.
    /// /params None.
    /// /returns Active Addressables settings asset, or null when creation failed.
    /// </summary>
    private static AddressableAssetSettings GetOrCreateSettings()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

        if (settings != null)
            return settings;

        settings = AddressableAssetSettings.Create(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                                                   AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName,
                                                   true,
                                                   true);
        AddressableAssetSettingsDefaultObject.Settings = settings;
        return settings;
    }

    /// <summary>
    /// Loads or creates the local group used by Scene Manager addressable scenes.
    /// /params settings Addressables settings asset.
    /// /returns Addressables group used for managed scenes.
    /// </summary>
    private static AddressableAssetGroup GetOrCreateSceneGroup(AddressableAssetSettings settings)
    {
        AddressableAssetGroup group = settings.FindGroup(SceneGroupName);

        if (group != null)
            return group;

        return settings.CreateGroup(SceneGroupName,
                                    false,
                                    false,
                                    false,
                                    null,
                                    typeof(BundledAssetGroupSchema),
                                    typeof(ContentUpdateGroupSchema));
    }

    /// <summary>
    /// Enables Addressables content building as part of Player builds to avoid a manual content-build hook.
    /// /params settings Addressables settings asset.
    /// /returns True when the setting changed.
    /// </summary>
    private static bool EnsureBuildWithPlayer(AddressableAssetSettings settings)
    {
        if (settings.BuildAddressablesWithPlayerBuild == AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer)
            return false;

        settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
        EditorUtility.SetDirty(settings);
        return true;
    }
    #endregion

    #region Entries
    /// <summary>
    /// Ensures one scene asset has a matching Addressables entry, address and label.
    /// /params settings Addressables settings asset.
    /// /params sceneGroup Target group for managed scene entries.
    /// /params sceneDefinition Scene definition to register.
    /// /returns True when the entry was created or modified.
    /// </summary>
    private static bool EnsureSceneEntry(AddressableAssetSettings settings,
                                         AddressableAssetGroup sceneGroup,
                                         GameSceneDefinition sceneDefinition)
    {
        string guid = ResolveSceneGuid(sceneDefinition);

        if (string.IsNullOrWhiteSpace(guid))
            return false;

        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, sceneGroup, false, true);
        bool changed = false;

        if (!string.Equals(entry.address, sceneDefinition.AddressableKey, System.StringComparison.Ordinal))
        {
            entry.address = sceneDefinition.AddressableKey;
            changed = true;
        }

        if (entry.labels == null || !entry.labels.Contains(SceneLabel))
        {
            entry.SetLabel(SceneLabel, true, true, true);
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Validates one required scene Addressables entry without changing settings.
    /// /params settings Addressables settings asset.
    /// /params sceneDefinition Scene definition expected to have an entry.
    /// /params warnings Mutable warning output list.
    /// /returns None.
    /// </summary>
    private static void ValidateSceneEntry(AddressableAssetSettings settings,
                                           GameSceneDefinition sceneDefinition,
                                           List<string> warnings)
    {
        string guid = ResolveSceneGuid(sceneDefinition);

        if (string.IsNullOrWhiteSpace(guid))
        {
            warnings.Add(sceneDefinition.SceneId + " has no scene GUID for Addressables registration.");
            return;
        }

        AddressableAssetEntry entry = settings.FindAssetEntry(guid);

        if (entry == null)
        {
            warnings.Add(sceneDefinition.SceneId + " is not registered as an Addressables scene.");
            return;
        }

        if (!string.Equals(entry.address, sceneDefinition.AddressableKey, System.StringComparison.Ordinal))
            warnings.Add(sceneDefinition.SceneId + " Addressables address does not match the preset key.");
    }

    /// <summary>
    /// Resolves whether a scene definition should be registered as Addressable.
    /// /params sceneDefinition Scene definition being inspected.
    /// /returns True when the scene is a top-level non-bootstrap scene with an Addressables key.
    /// </summary>
    private static bool ShouldRegisterScene(GameSceneDefinition sceneDefinition)
    {
        if (sceneDefinition.SceneKind == GameSceneKind.Bootstrap)
            return false;

        if (sceneDefinition.SceneKind == GameSceneKind.SubScene)
            return false;

        return !string.IsNullOrWhiteSpace(sceneDefinition.AddressableKey);
    }

    /// <summary>
    /// Resolves a scene asset GUID from stored scene definition metadata.
    /// /params sceneDefinition Scene definition being registered.
    /// /returns Scene asset GUID or an empty string when unresolved.
    /// </summary>
    private static string ResolveSceneGuid(GameSceneDefinition sceneDefinition)
    {
        if (!string.IsNullOrWhiteSpace(sceneDefinition.SceneGuid))
            return sceneDefinition.SceneGuid;

        if (string.IsNullOrWhiteSpace(sceneDefinition.ScenePath))
            return string.Empty;

        return AssetDatabase.AssetPathToGUID(sceneDefinition.ScenePath);
    }
    #endregion

    #endregion
}
