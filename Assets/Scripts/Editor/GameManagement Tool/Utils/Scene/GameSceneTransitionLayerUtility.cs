using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides editor helpers for Scene Manager transition layer validation and setup.
/// </summary>
public static class GameSceneTransitionLayerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves whether a Unity layer with the provided name exists.
    /// </summary>
    /// <param name="layerName">Unity layer name.</param>
    /// <returns>True when the layer exists.</returns>
    public static bool LayerExists(string layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName))
            return false;

        return LayerMask.NameToLayer(layerName) >= 0;
    }

    /// <summary>
    /// Adds a warning when the configured transition layer is missing.
    /// </summary>
    /// <param name="preset">Scene manager preset to inspect.</param>
    /// <param name="warnings">Mutable warning output list.</param>
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
    /// </summary>
    /// <param name="layerName">Unity layer name to create.</param>
    /// <returns>True when a new layer was created.</returns>
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

            layerProperty.stringValue = layerName.Trim();
            tagManager.ApplyModifiedProperties();
            EditorUtility.SetDirty(tagManager.targetObject);
            AssetDatabase.SaveAssets();
            return true;
        }

        return false;
    }
    #endregion

    #endregion
}
