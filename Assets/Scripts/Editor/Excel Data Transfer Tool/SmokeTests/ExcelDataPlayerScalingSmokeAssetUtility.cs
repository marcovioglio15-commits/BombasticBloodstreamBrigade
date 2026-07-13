using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Holds the isolated Player authoring assets used by formula-aware Excel transfer smoke tests.
/// </summary>
internal sealed class ExcelDataPlayerScalingSmokeAssets
{
    #region Properties
    public string FolderPath { get; }
    public PlayerProgressionPreset ProgressionPreset { get; }
    public PlayerControllerPreset ControllerPreset { get; }
    public PlayerMasterPreset PlayerMasterPreset { get; }
    public ExcelDataImportPreset ImportPreset { get; }
    public string NumericStatKey { get; }
    public string BooleanStatKey { get; }
    public string TokenStatKey { get; }
    public string ColorChannelStatKey { get; }
    public string EnumStatKey { get; }
    public string LevelDefaultStatKey { get; }
    public string BonusDefaultStatKey { get; }
    public string MergeTargetStatKey { get; }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one immutable handle for a fully configured temporary Player preset graph.
    /// </summary>
    /// <param name="folderPath">Temporary project folder.</param>
    /// <param name="progressionPreset">Temporary progression preset.</param>
    /// <param name="controllerPreset">Temporary controller preset.</param>
    /// <param name="playerMasterPreset">Temporary Player master linking formula scope.</param>
    /// <param name="importPreset">Temporary Excel import policy.</param>
    /// <param name="numericStatKey">Progression numeric target key.</param>
    /// <param name="booleanStatKey">Progression Boolean target key.</param>
    /// <param name="tokenStatKey">Progression token target key.</param>
    /// <param name="colorChannelStatKey">Progression Color channel target key.</param>
    /// <param name="enumStatKey">Controller enum target key.</param>
    /// <param name="levelDefaultStatKey">Level scalable-stat default target key.</param>
    /// <param name="bonusDefaultStatKey">Bonus scalable-stat default target key.</param>
    /// <param name="mergeTargetStatKey">Unique numeric target used by controlled creation tests.</param>
    public ExcelDataPlayerScalingSmokeAssets(string folderPath,
                                             PlayerProgressionPreset progressionPreset,
                                             PlayerControllerPreset controllerPreset,
                                             PlayerMasterPreset playerMasterPreset,
                                             ExcelDataImportPreset importPreset,
                                             string numericStatKey,
                                             string booleanStatKey,
                                             string tokenStatKey,
                                             string colorChannelStatKey,
                                             string enumStatKey,
                                             string levelDefaultStatKey,
                                             string bonusDefaultStatKey,
                                             string mergeTargetStatKey)
    {
        FolderPath = folderPath;
        ProgressionPreset = progressionPreset;
        ControllerPreset = controllerPreset;
        PlayerMasterPreset = playerMasterPreset;
        ImportPreset = importPreset;
        NumericStatKey = numericStatKey;
        BooleanStatKey = booleanStatKey;
        TokenStatKey = tokenStatKey;
        ColorChannelStatKey = colorChannelStatKey;
        EnumStatKey = enumStatKey;
        LevelDefaultStatKey = levelDefaultStatKey;
        BonusDefaultStatKey = bonusDefaultStatKey;
        MergeTargetStatKey = mergeTargetStatKey;
    }
    #endregion

    #endregion
}

/// <summary>
/// Creates deterministic persistent Player presets and workbook cells for scaling import smoke coverage.
/// </summary>
internal static class ExcelDataPlayerScalingSmokeAssetUtility
{
    #region Constants
    private const string SheetName = "Player Scaling";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates a temporary Player master graph with typed scalable stats and representative scaling rules.
    /// </summary>
    /// <returns>Configured temporary smoke assets.</returns>
    public static ExcelDataPlayerScalingSmokeAssets Create()
    {
        string folderName = "ExcelDataPlayerScalingSmoke_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", folderName);
        string folderPath = "Assets/" + folderName;
        PlayerProgressionPreset progressionPreset = CreateAsset<PlayerProgressionPreset>(folderPath,
                                                                                         "Progression.asset");
        PlayerControllerPreset controllerPreset = CreateAsset<PlayerControllerPreset>(folderPath,
                                                                                       "Controller.asset");
        PlayerMasterPreset playerMasterPreset = CreateAsset<PlayerMasterPreset>(folderPath,
                                                                                "PlayerMaster.asset");
        ExcelDataImportPreset importPreset = CreateAsset<ExcelDataImportPreset>(folderPath,
                                                                                "Import.asset");
        ConfigureProgressionScalableStats(progressionPreset);
        SerializedObject progressionSerializedObject = new SerializedObject(progressionPreset);
        string numericStatKey = BuildStatKey(progressionSerializedObject, "experiencePickupRadius");
        string booleanStatKey = BuildStatKey(progressionSerializedObject, "milestoneSkipOnlyFromExitInput");
        string tokenStatKey = BuildStatKey(progressionSerializedObject, "equippedScheduleId");
        string colorChannelStatKey = BuildStatKey(progressionSerializedObject,
                                                  "milestoneSkipHoldFillColor.r");
        string levelDefaultStatKey = BuildStatKey(progressionSerializedObject,
                                                  "scalableStats.Array.data[0].defaultValue");
        string bonusDefaultStatKey = BuildStatKey(progressionSerializedObject,
                                                  "scalableStats.Array.data[2].defaultValue");
        string mergeTargetStatKey = BuildStatKey(progressionSerializedObject,
                                                 "milestoneTimeScaleResumeDurationSeconds");
        ConfigureProgressionRules(progressionPreset,
                                  numericStatKey,
                                  booleanStatKey,
                                  tokenStatKey,
                                  colorChannelStatKey,
                                  levelDefaultStatKey,
                                  bonusDefaultStatKey);
        SerializedObject controllerSerializedObject = new SerializedObject(controllerPreset);
        string enumStatKey = BuildStatKey(controllerSerializedObject,
                                          "movementSettings.directionsMode");
        ConfigureControllerRule(controllerPreset, enumStatKey);
        LinkPlayerMaster(playerMasterPreset, progressionPreset, controllerPreset);
        SetImportPolicy(importPreset, ExcelDataScalingRuleImportPolicy.ExistingRulesOnly);
        AssetDatabase.SaveAssets();
        return new ExcelDataPlayerScalingSmokeAssets(folderPath,
                                                     progressionPreset,
                                                     controllerPreset,
                                                     playerMasterPreset,
                                                     importPreset,
                                                     numericStatKey,
                                                     booleanStatKey,
                                                     tokenStatKey,
                                                     colorChannelStatKey,
                                                     enumStatKey,
                                                     levelDefaultStatKey,
                                                     bonusDefaultStatKey,
                                                     mergeTargetStatKey);
    }

    /// <summary>
    /// Deletes every temporary smoke asset in one project-relative folder.
    /// </summary>
    /// <param name="assets">Temporary asset graph to remove.</param>
    public static void Delete(ExcelDataPlayerScalingSmokeAssets assets)
    {
        if (assets == null || string.IsNullOrWhiteSpace(assets.FolderPath))
            return;

        AssetDatabase.DeleteAsset(assets.FolderPath);
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Updates the import preset list policy through serialized authoring data.
    /// </summary>
    /// <param name="importPreset">Temporary import preset.</param>
    /// <param name="policy">Scaling rule policy used by the next planner call.</param>
    public static void SetImportPolicy(ExcelDataImportPreset importPreset,
                                       ExcelDataScalingRuleImportPolicy policy)
    {
        SetSerializedProperty(importPreset,
                              "scalingRuleImportPolicy",
                              property => property.enumValueIndex = (int)policy);
    }

    /// <summary>
    /// Creates one coordinate-aware incoming cell for an existing Player scaling-rule member.
    /// </summary>
    /// <param name="owner">Player preset containing scalingRules.</param>
    /// <param name="ruleIndex">Zero-based source rule index.</param>
    /// <param name="sourceStatKey">Current source rule key used as stable list identity.</param>
    /// <param name="memberName">Direct PlayerStatScalingRule member name.</param>
    /// <param name="incomingValue">Workbook text to stage.</param>
    /// <param name="rowIndex">One-based smoke worksheet row.</param>
    /// <returns>Configured semantic-preflight cell.</returns>
    public static ExcelDataPlayerScalingImportCell CreateScalingCell(Object owner,
                                                                     int ruleIndex,
                                                                     string sourceStatKey,
                                                                     string memberName,
                                                                     string incomingValue,
                                                                     int rowIndex)
    {
        string ownerPath = AssetDatabase.GetAssetPath(owner);
        string serializedPath = ExcelDataPlayerScalingRuleSerializedUtility.BuildMemberPath(
            ExcelDataPlayerScalingRuleSerializedUtility.BuildRulePath("scalingRules", ruleIndex),
            memberName);
        string pathTemplate = "scalingRules.Array.data[]." + memberName;
        ExcelDataFieldBinding binding = new ExcelDataFieldBinding();
        binding.Configure("ScalingSmoke:" + Guid.NewGuid().ToString("N"),
                          ExcelDataTransferDomain.Player,
                          AssetDatabase.AssetPathToGUID(ownerPath),
                          owner.GetType().Name,
                          ownerPath,
                          serializedPath,
                          pathTemplate,
                          ResolveDataKind(memberName));
        binding.ConfigureListIdentity(new List<int> { ruleIndex },
                                      new List<string> { "Stat Key=" + sourceStatKey });
        ExcelDataWorkbookCellDefinition cellDefinition = new ExcelDataWorkbookCellDefinition();
        cellDefinition.ConfigureDataField(SheetName,
                                          rowIndex,
                                          1,
                                          binding,
                                          ExcelDataTransferDirection.Both,
                                          "ScalingSmoke",
                                          string.Empty);
        ExcelDataImportCellValue cellValue = new ExcelDataImportCellValue(incomingValue,
                                                                          string.Empty,
                                                                          string.Empty,
                                                                          string.Empty);
        return new ExcelDataPlayerScalingImportCell(SheetName,
                                                    ExcelDataWorkbookCoordinateUtility.BuildAddress(rowIndex, 1),
                                                    cellDefinition,
                                                    cellValue);
    }

    /// <summary>
    /// Reads the current scalingRules array size without exposing mutable list storage.
    /// </summary>
    /// <param name="owner">Player preset containing scalingRules.</param>
    /// <returns>Current serialized rule count.</returns>
    public static int ReadRuleCount(Object owner)
    {
        SerializedObject serializedObject = new SerializedObject(owner);
        SerializedProperty rulesProperty = serializedObject.FindProperty("scalingRules");
        return rulesProperty == null ? 0 : rulesProperty.arraySize;
    }
    #endregion

    #region Asset Setup
    /// <summary>
    /// Creates one persistent ScriptableObject inside the temporary smoke folder.
    /// </summary>
    /// <typeparam name="T">ScriptableObject type.</typeparam>
    /// <param name="folderPath">Temporary project folder.</param>
    /// <param name="fileName">Asset file name.</param>
    /// <returns>Created persistent asset.</returns>
    private static T CreateAsset<T>(string folderPath, string fileName) where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, folderPath + "/" + fileName);
        return asset;
    }

    /// <summary>
    /// Configures numeric and token formula variables used by typed rule tests.
    /// </summary>
    /// <param name="progressionPreset">Temporary progression preset.</param>
    private static void ConfigureProgressionScalableStats(PlayerProgressionPreset progressionPreset)
    {
        SerializedObject serializedObject = new SerializedObject(progressionPreset);
        SerializedProperty statsProperty = serializedObject.FindProperty("scalableStats");
        statsProperty.arraySize = 3;
        ConfigureScalableStat(statsProperty.GetArrayElementAtIndex(0),
                              "Level",
                              PlayerScalableStatType.Float,
                              2f,
                              string.Empty);
        ConfigureScalableStat(statsProperty.GetArrayElementAtIndex(1),
                              "ModeToken",
                              PlayerScalableStatType.Token,
                              0f,
                              "Scaled");
        ConfigureScalableStat(statsProperty.GetArrayElementAtIndex(2),
                              "Bonus",
                              PlayerScalableStatType.Float,
                              3f,
                              string.Empty);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(progressionPreset);
    }

    /// <summary>
    /// Configures one serialized scalable-stat definition without invoking validation or value snapping.
    /// </summary>
    /// <param name="statProperty">Serialized stat list element.</param>
    /// <param name="statName">Formula variable name.</param>
    /// <param name="statType">Typed formula value family.</param>
    /// <param name="numericValue">Numeric default when applicable.</param>
    /// <param name="tokenValue">Token default when applicable.</param>
    private static void ConfigureScalableStat(SerializedProperty statProperty,
                                              string statName,
                                              PlayerScalableStatType statType,
                                              float numericValue,
                                              string tokenValue)
    {
        statProperty.FindPropertyRelative("statName").stringValue = statName;
        statProperty.FindPropertyRelative("statType").enumValueIndex = (int)statType;
        statProperty.FindPropertyRelative("defaultValue").floatValue = numericValue;
        statProperty.FindPropertyRelative("minimumValue").floatValue = -1000f;
        statProperty.FindPropertyRelative("maximumValue").floatValue = 1000f;
        statProperty.FindPropertyRelative("defaultBooleanValue").boolValue = false;
        statProperty.FindPropertyRelative("defaultTokenValue").stringValue = tokenValue;
    }

    /// <summary>
    /// Configures representative progression scaling rules for every supported result family and dependency tests.
    /// </summary>
    /// <param name="progressionPreset">Temporary progression preset.</param>
    /// <param name="numericStatKey">Numeric target key.</param>
    /// <param name="booleanStatKey">Boolean target key.</param>
    /// <param name="tokenStatKey">Token target key.</param>
    /// <param name="colorChannelStatKey">Color channel target key.</param>
    /// <param name="levelDefaultStatKey">Level default target key.</param>
    /// <param name="bonusDefaultStatKey">Bonus default target key.</param>
    private static void ConfigureProgressionRules(PlayerProgressionPreset progressionPreset,
                                                  string numericStatKey,
                                                  string booleanStatKey,
                                                  string tokenStatKey,
                                                  string colorChannelStatKey,
                                                  string levelDefaultStatKey,
                                                  string bonusDefaultStatKey)
    {
        SerializedObject serializedObject = new SerializedObject(progressionPreset);
        SerializedProperty rulesProperty = serializedObject.FindProperty("scalingRules");
        rulesProperty.arraySize = 6;
        ConfigureRule(rulesProperty.GetArrayElementAtIndex(0), numericStatKey, true, "[this] + [Level]");
        ConfigureRule(rulesProperty.GetArrayElementAtIndex(1), booleanStatKey, true, "[Level] > 0");
        ConfigureRule(rulesProperty.GetArrayElementAtIndex(2), tokenStatKey, true, "[ModeToken]");
        ConfigureRule(rulesProperty.GetArrayElementAtIndex(3), colorChannelStatKey, true, "[this] * 0.5");
        ConfigureRule(rulesProperty.GetArrayElementAtIndex(4), levelDefaultStatKey, false, string.Empty);
        ConfigureRule(rulesProperty.GetArrayElementAtIndex(5), bonusDefaultStatKey, false, string.Empty);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(progressionPreset);
    }

    /// <summary>
    /// Configures one controller enum scaling rule linked to the progression formula scope.
    /// </summary>
    /// <param name="controllerPreset">Temporary controller preset.</param>
    /// <param name="enumStatKey">Controller enum target key.</param>
    private static void ConfigureControllerRule(PlayerControllerPreset controllerPreset, string enumStatKey)
    {
        SerializedObject serializedObject = new SerializedObject(controllerPreset);
        SerializedProperty rulesProperty = serializedObject.FindProperty("scalingRules");
        rulesProperty.arraySize = 1;
        ConfigureRule(rulesProperty.GetArrayElementAtIndex(0), enumStatKey, true, "[this] + 1");
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controllerPreset);
    }

    /// <summary>
    /// Initializes every direct PlayerStatScalingRule member deterministically.
    /// </summary>
    /// <param name="ruleProperty">Serialized rule element.</param>
    /// <param name="statKey">Stable target property key.</param>
    /// <param name="addScaling">Whether formula evaluation is enabled.</param>
    /// <param name="formula">Unified formula text.</param>
    private static void ConfigureRule(SerializedProperty ruleProperty,
                                      string statKey,
                                      bool addScaling,
                                      string formula)
    {
        ruleProperty.FindPropertyRelative("statKey").stringValue = statKey;
        ruleProperty.FindPropertyRelative("addScaling").boolValue = addScaling;
        ruleProperty.FindPropertyRelative("formula").stringValue = formula;
        ruleProperty.FindPropertyRelative("debugInConsole").boolValue = false;
        ruleProperty.FindPropertyRelative("debugColor").colorValue = PlayerStatScalingRule.GetDefaultDebugColor();
    }

    /// <summary>
    /// Links controller and progression presets into one explicit Player master formula scope.
    /// </summary>
    /// <param name="masterPreset">Temporary Player master preset.</param>
    /// <param name="progressionPreset">Temporary progression preset.</param>
    /// <param name="controllerPreset">Temporary controller preset.</param>
    private static void LinkPlayerMaster(PlayerMasterPreset masterPreset,
                                         PlayerProgressionPreset progressionPreset,
                                         PlayerControllerPreset controllerPreset)
    {
        SerializedObject serializedObject = new SerializedObject(masterPreset);
        serializedObject.FindProperty("m_ProgressionPreset").objectReferenceValue = progressionPreset;
        serializedObject.FindProperty("m_ControllerPreset").objectReferenceValue = controllerPreset;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(masterPreset);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Builds the exact Add Scaling stat key for one serialized property path.
    /// </summary>
    /// <param name="serializedObject">Owner preset wrapper.</param>
    /// <param name="propertyPath">Concrete target property path.</param>
    /// <returns>Stable Player scaling stat key.</returns>
    private static string BuildStatKey(SerializedObject serializedObject, string propertyPath)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyPath);

        if (property == null)
            throw new InvalidOperationException("Scaling smoke target property not found: " + propertyPath);

        string statKey = PlayerScalingStatKeyUtility.BuildStatKey(property);

        if (string.IsNullOrWhiteSpace(statKey))
            throw new InvalidOperationException("Scaling smoke stat key could not be generated for: " + propertyPath);

        return statKey;
    }

    /// <summary>
    /// Resolves the workbook data kind expected by one direct rule member.
    /// </summary>
    /// <param name="memberName">Direct PlayerStatScalingRule member.</param>
    /// <returns>Matching import data kind.</returns>
    private static ExcelDataBrushDataKind ResolveDataKind(string memberName)
    {
        switch (memberName)
        {
            case ExcelDataPlayerScalingRuleSerializedUtility.AddScalingMemberName:
            case ExcelDataPlayerScalingRuleSerializedUtility.DebugInConsoleMemberName:
                return ExcelDataBrushDataKind.Boolean;
            case ExcelDataPlayerScalingRuleSerializedUtility.DebugColorMemberName:
                return ExcelDataBrushDataKind.Color;
            default:
                return ExcelDataBrushDataKind.String;
        }
    }

    /// <summary>
    /// Applies one setup value to a serialized asset and marks it dirty.
    /// </summary>
    /// <param name="asset">Target temporary asset.</param>
    /// <param name="propertyPath">Serialized property path.</param>
    /// <param name="setter">Strongly scoped property mutation.</param>
    private static void SetSerializedProperty(Object asset,
                                              string propertyPath,
                                              Action<SerializedProperty> setter)
    {
        SerializedObject serializedObject = new SerializedObject(asset);
        SerializedProperty property = serializedObject.FindProperty(propertyPath);

        if (property == null)
            throw new InvalidOperationException("Scaling smoke setup property not found: " + propertyPath);

        setter(property);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }
    #endregion

    #endregion
}
