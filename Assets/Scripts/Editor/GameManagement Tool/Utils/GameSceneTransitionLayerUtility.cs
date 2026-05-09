using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides editor helpers for Scene Manager transition layer validation and setup.
/// /params None.
/// /returns None.
/// </summary>
public static class GameSceneTransitionLayerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves whether a Unity layer with the provided name exists.
    /// /params layerName Unity layer name.
    /// /returns True when the layer exists.
    /// </summary>
    public static bool LayerExists(string layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName))
            return false;

        return LayerMask.NameToLayer(layerName) >= 0;
    }

    /// <summary>
    /// Adds a warning when the configured transition layer is missing.
    /// /params preset Scene manager preset to inspect.
    /// /params warnings Mutable warning output list.
    /// /returns None.
    /// </summary>
    public static void CollectLayerWarnings(GameSceneManagerPreset preset, List<string> warnings)
    {
        if (preset == null || warnings == null || preset.TriggerSettings == null)
            return;

        string layerName = preset.TriggerSettings.TransitionLayerName;

        if (!LayerExists(layerName))
            warnings.Add("Transition layer '" + layerName + "' does not exist in Project Settings.");
    }

    /// <summary>
    /// Creates the configured transition layer in the first available user layer slot.
    /// /params layerName Unity layer name to create.
    /// /returns True when a new layer was created.
    /// </summary>
    public static bool TryCreateLayer(string layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName))
            return false;

        if (LayerExists(layerName))
            return false;

        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layersProperty = tagManager.FindProperty("layers");

        if (layersProperty == null)
            return false;

        for (int index = 8; index < layersProperty.arraySize; index++)
        {
            SerializedProperty layerProperty = layersProperty.GetArrayElementAtIndex(index);

            if (!string.IsNullOrWhiteSpace(layerProperty.stringValue))
                continue;

            tagManager.Update();
            layerProperty.stringValue = layerName.Trim();
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            return true;
        }

        return false;
    }
    #endregion

    #endregion
}
