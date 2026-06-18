using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utilities for creating, duplicating, importing and auto-mapping animation bindings presets.
/// </summary>
public static class PlayerAnimationBindingsPresetEditorUtility
{
    #region Constants
    public const string DefaultPresetFolder = "Assets/Scriptable Objects/Player/Animation Bindings";
    #endregion

    #region Fields
    private static readonly PlayerAnimationClipSlot[] SupportedClipSlots =
    {
        PlayerAnimationClipSlot.Idle,
        PlayerAnimationClipSlot.UpperBodyIdle,
        PlayerAnimationClipSlot.MoveForward,
        PlayerAnimationClipSlot.MoveBackward,
        PlayerAnimationClipSlot.MoveLeft,
        PlayerAnimationClipSlot.MoveRight,
        PlayerAnimationClipSlot.AimForward,
        PlayerAnimationClipSlot.AimBackward,
        PlayerAnimationClipSlot.AimLeft,
        PlayerAnimationClipSlot.AimRight,
        PlayerAnimationClipSlot.Dash
    };
    #endregion

    #region Methods
    public static List<PlayerAnimationBindingsPreset> LoadAllPresets()
    {
        List<PlayerAnimationBindingsPreset> presets = new List<PlayerAnimationBindingsPreset>();
        string[] presetGuids = AssetDatabase.FindAssets("t:PlayerAnimationBindingsPreset");

        for (int presetIndex = 0; presetIndex < presetGuids.Length; presetIndex++)
        {
            string presetPath = AssetDatabase.GUIDToAssetPath(presetGuids[presetIndex]);
            PlayerAnimationBindingsPreset preset = AssetDatabase.LoadAssetAtPath<PlayerAnimationBindingsPreset>(presetPath);

            if (preset != null)
                presets.Add(preset);
        }

        presets.Sort(ComparePresets);
        return presets;
    }

    public static PlayerAnimationBindingsPreset CreatePresetAsset(string presetName)
    {
        EnsureFolder(DefaultPresetFolder);

        string normalizedName = string.IsNullOrWhiteSpace(presetName) ? "PlayerAnimationBindingsPreset" : PlayerManagementDraftSession.NormalizeAssetName(presetName);
        string targetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(DefaultPresetFolder, normalizedName + ".asset"));
        string finalName = Path.GetFileNameWithoutExtension(targetPath);
        PlayerAnimationBindingsPreset preset = ScriptableObject.CreateInstance<PlayerAnimationBindingsPreset>();
        preset.name = finalName;
        AssetDatabase.CreateAsset(preset, targetPath);
        SerializedObject serializedPreset = new SerializedObject(preset);
        SerializedProperty presetIdProperty = serializedPreset.FindProperty("presetId");
        SerializedProperty presetNameProperty = serializedPreset.FindProperty("presetName");
        serializedPreset.Update();

        if (presetIdProperty != null)
            presetIdProperty.stringValue = Guid.NewGuid().ToString("N");

        if (presetNameProperty != null)
            presetNameProperty.stringValue = finalName;

        serializedPreset.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(preset, "Create Animation Bindings Preset");
        EditorUtility.SetDirty(preset);
        AssetDatabase.SaveAssets();
        PlayerManagementDraftSession.MarkDirty();
        return preset;
    }

    public static PlayerAnimationBindingsPreset DuplicatePresetAsset(PlayerAnimationBindingsPreset sourcePreset)
    {
        if (sourcePreset == null)
            return null;

        EnsureFolder(DefaultPresetFolder);

        string sourcePath = AssetDatabase.GetAssetPath(sourcePreset);

        if (string.IsNullOrWhiteSpace(sourcePath))
            return null;

        string sourceName = string.IsNullOrWhiteSpace(sourcePreset.name) ? "PlayerAnimationBindingsPreset" : sourcePreset.name;
        string duplicatePath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(DefaultPresetFolder, sourceName + "_Copy.asset"));

        if (!AssetDatabase.CopyAsset(sourcePath, duplicatePath))
            return null;

        AssetDatabase.SaveAssets();
        PlayerAnimationBindingsPreset duplicatedPreset = AssetDatabase.LoadAssetAtPath<PlayerAnimationBindingsPreset>(duplicatePath);

        if (duplicatedPreset != null)
        {
            string duplicateName = Path.GetFileNameWithoutExtension(duplicatePath);
            SerializedObject duplicatedSerializedObject = new SerializedObject(duplicatedPreset);
            SerializedProperty presetIdProperty = duplicatedSerializedObject.FindProperty("presetId");
            SerializedProperty presetNameProperty = duplicatedSerializedObject.FindProperty("presetName");
            duplicatedSerializedObject.Update();

            if (presetIdProperty != null)
                presetIdProperty.stringValue = Guid.NewGuid().ToString("N");

            if (presetNameProperty != null)
                presetNameProperty.stringValue = duplicateName;

            duplicatedSerializedObject.ApplyModifiedPropertiesWithoutUndo();
            duplicatedPreset.name = duplicateName;

            EditorUtility.SetDirty(duplicatedPreset);
        }

        PlayerManagementDraftSession.MarkDirty();
        return duplicatedPreset;
    }

    public static bool DeletePresetAsset(PlayerAnimationBindingsPreset preset)
    {
        if (preset == null)
            return false;

        string presetPath = AssetDatabase.GetAssetPath(preset);

        if (string.IsNullOrWhiteSpace(presetPath))
            return false;

        if (!EditorUtility.DisplayDialog("Delete Animation Bindings Preset",
                                        string.Format("Delete '{0}'? This action cannot be undone.", preset.name),
                                        "Delete",
                                        "Cancel"))
            return false;

        bool deleted = AssetDatabase.DeleteAsset(presetPath);

        if (deleted)
        {
            AssetDatabase.SaveAssets();
            PlayerManagementDraftSession.MarkDirty();
        }

        return deleted;
    }

    public static List<AnimationClip> LoadClipsFromFolder(DefaultAsset folderAsset, bool recursive)
    {
        List<AnimationClip> clips = new List<AnimationClip>();

        if (folderAsset == null)
            return clips;

        string folderPath = AssetDatabase.GetAssetPath(folderAsset);

        if (string.IsNullOrWhiteSpace(folderPath))
            return clips;

        if (!AssetDatabase.IsValidFolder(folderPath))
            return clips;

        List<string> searchFolders = new List<string>();
        searchFolders.Add(folderPath);

        if (recursive)
        {
            string[] subFolderGuids = AssetDatabase.FindAssets("t:DefaultAsset", new[] { folderPath });

            for (int guidIndex = 0; guidIndex < subFolderGuids.Length; guidIndex++)
            {
                string candidatePath = AssetDatabase.GUIDToAssetPath(subFolderGuids[guidIndex]);

                if (!AssetDatabase.IsValidFolder(candidatePath))
                    continue;

                if (searchFolders.Contains(candidatePath))
                    continue;

                searchFolders.Add(candidatePath);
            }
        }

        string[] clipGuids = AssetDatabase.FindAssets("t:AnimationClip", searchFolders.ToArray());

        for (int clipIndex = 0; clipIndex < clipGuids.Length; clipIndex++)
        {
            string clipPath = AssetDatabase.GUIDToAssetPath(clipGuids[clipIndex]);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

            if (clip == null)
                continue;

            if (clip.legacy)
                continue;

            if (clips.Contains(clip))
                continue;

            clips.Add(clip);
        }

        return clips;
    }

    public static int AutoMapClipSlots(PlayerAnimationBindingsPreset targetPreset,
                                       IReadOnlyList<AnimationClip> availableClips,
                                       bool overwriteAssignedSlots)
    {
        if (targetPreset == null || availableClips == null || availableClips.Count == 0)
            return 0;

        Undo.RecordObject(targetPreset, "Auto-map Animation Clips");
        int assignedCount = 0;

        for (int slotIndex = 0; slotIndex < SupportedClipSlots.Length; slotIndex++)
        {
            PlayerAnimationClipSlot slot = SupportedClipSlots[slotIndex];
            AnimationClip currentClip = PlayerAnimationBindingsPresetTransferUtility.GetClip(targetPreset, slot);

            if (!overwriteAssignedSlots && currentClip != null)
                continue;

            AnimationClip bestClip = FindBestClip(slot, availableClips);

            if (bestClip == null)
                continue;

            if (currentClip == bestClip)
                continue;

            targetPreset.SetClip(slot, bestClip);
            assignedCount++;
        }

        if (assignedCount > 0)
        {
            EditorUtility.SetDirty(targetPreset);
            PlayerManagementDraftSession.MarkDirty();
        }

        return assignedCount;
    }

    public static void ImportSettingsFromSource(PlayerAnimationBindingsPreset targetPreset,
                                                PlayerAnimationBindingsPreset sourcePreset,
                                                bool includeClipSlots)
    {
        if (targetPreset == null || sourcePreset == null)
            return;

        if (targetPreset == sourcePreset)
            return;

        Undo.RecordObject(targetPreset, "Import Animation Binding Settings");
        PlayerAnimationBindingsPresetTransferUtility.CopySettingsFrom(targetPreset, sourcePreset);

        if (includeClipSlots)
            PlayerAnimationBindingsPresetTransferUtility.CopyClipSlotsFrom(targetPreset, sourcePreset);

        EditorUtility.SetDirty(targetPreset);
        PlayerManagementDraftSession.MarkDirty();
    }
    #endregion

    #region Helpers
    private static int ComparePresets(PlayerAnimationBindingsPreset left, PlayerAnimationBindingsPreset right)
    {
        if (left == null && right == null)
            return 0;

        if (left == null)
            return 1;

        if (right == null)
            return -1;

        string leftName = string.IsNullOrWhiteSpace(left.PresetName) ? left.name : left.PresetName;
        string rightName = string.IsNullOrWhiteSpace(right.PresetName) ? right.name : right.PresetName;
        return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parentPath = Path.GetDirectoryName(folderPath);
        string folderName = Path.GetFileName(folderPath);

        if (string.IsNullOrWhiteSpace(parentPath) || string.IsNullOrWhiteSpace(folderName))
            return;

        EnsureFolder(parentPath);

        if (!AssetDatabase.IsValidFolder(folderPath))
            AssetDatabase.CreateFolder(parentPath, folderName);
    }

    private static AnimationClip FindBestClip(PlayerAnimationClipSlot slot, IReadOnlyList<AnimationClip> clips)
    {
        string[] keywords = GetSlotKeywords(slot);

        if (keywords == null || keywords.Length == 0)
            return null;

        AnimationClip bestClip = null;
        int bestScore = 0;

        for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
        {
            AnimationClip clip = clips[clipIndex];

            if (clip == null)
                continue;

            string clipName = clip.name;

            if (string.IsNullOrWhiteSpace(clipName))
                continue;

            int score = ScoreClip(slot, clipName, keywords);

            if (score <= 0)
                continue;

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestClip = clip;
        }

        return bestClip;
    }

    private static int ScoreClip(PlayerAnimationClipSlot slot, string clipName, string[] keywords)
    {
        string normalized = clipName.ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);
        int score = 0;

        for (int keywordIndex = 0; keywordIndex < keywords.Length; keywordIndex++)
        {
            string keyword = keywords[keywordIndex];

            if (string.IsNullOrWhiteSpace(keyword))
                continue;

            if (!normalized.Contains(keyword))
                continue;

            score += 10;
        }

        switch (slot)
        {
            case PlayerAnimationClipSlot.MoveForward:
            case PlayerAnimationClipSlot.MoveBackward:
            case PlayerAnimationClipSlot.MoveLeft:
            case PlayerAnimationClipSlot.MoveRight:
                if (normalized.Contains("move") || normalized.Contains("movement") || normalized.Contains("run") || normalized.Contains("walk"))
                    score += 5;
                break;
            case PlayerAnimationClipSlot.AimForward:
            case PlayerAnimationClipSlot.AimBackward:
            case PlayerAnimationClipSlot.AimLeft:
            case PlayerAnimationClipSlot.AimRight:
                if (normalized.Contains("aim"))
                    score += 8;
                break;
            case PlayerAnimationClipSlot.Dash:
                if (normalized.Contains("dash") || normalized.Contains("roll") || normalized.Contains("evade"))
                    score += 12;
                break;
            case PlayerAnimationClipSlot.Idle:
                if (IsUpperBodyIdleClipName(normalized))
                    return 0;

                if (normalized.Contains("idle"))
                    score += 12;
                break;
            case PlayerAnimationClipSlot.UpperBodyIdle:
                if (normalized.Contains("idle"))
                    score += 12;

                if (IsUpperBodyIdleClipName(normalized))
                    score += 16;
                break;
        }

        return score;
    }

    /// <summary>
    /// Detects clip-name tokens that identify upper-body or arm-only idle animation variants.
    /// </summary>
    /// <param name="normalizedClipName">Lowercase clip name with separators already removed.</param>
    /// <returns>True when the clip name points to an upper-body idle variant.</returns>
    private static bool IsUpperBodyIdleClipName(string normalizedClipName)
    {
        return !string.IsNullOrWhiteSpace(normalizedClipName) &&
               (normalizedClipName.Contains("arms") ||
                normalizedClipName.Contains("upper"));
    }

    private static string[] GetSlotKeywords(PlayerAnimationClipSlot slot)
    {
        switch (slot)
        {
            case PlayerAnimationClipSlot.Idle:
                return new[] { "idle", "stand" };
            case PlayerAnimationClipSlot.UpperBodyIdle:
                return new[] { "idle", "arms", "upper" };
            case PlayerAnimationClipSlot.MoveForward:
                return new[] { "forward", "fwd" };
            case PlayerAnimationClipSlot.MoveBackward:
                return new[] { "backward", "back", "reverse" };
            case PlayerAnimationClipSlot.MoveLeft:
                return new[] { "left" };
            case PlayerAnimationClipSlot.MoveRight:
                return new[] { "right" };
            case PlayerAnimationClipSlot.AimForward:
                return new[] { "aim", "forward" };
            case PlayerAnimationClipSlot.AimBackward:
                return new[] { "aim", "backward" };
            case PlayerAnimationClipSlot.AimLeft:
                return new[] { "aim", "left" };
            case PlayerAnimationClipSlot.AimRight:
                return new[] { "aim", "right" };
            case PlayerAnimationClipSlot.Dash:
                return new[] { "dash", "roll", "evade" };
            default:
                return Array.Empty<string>();
        }
    }
    #endregion
}
