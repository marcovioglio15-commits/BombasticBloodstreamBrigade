using System;
using TMPro;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Validates fixed-unit syringe label distribution without depending on Play Mode.
/// </summary>
internal static class PlayerHealthBarsLabelDistributionSmokeTestUtility
{
    #region Constants
    private const string PrefabPath = "Assets/Prefabs/UI/PlayerBars VerticalBox.prefab";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates that constrained label pools distribute values across the complete syringe range.
    /// </summary>
    public static void Validate()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = UnityEngine.Object.Instantiate(prefab);

        try
        {
            PlayerSyringeBarLabelPool labelPool = instance.GetComponentInChildren<PlayerSyringeBarLabelPool>(true);

            if (labelPool == null)
                throw new InvalidOperationException("Preauthored syringe label pool is missing.");

            RectTransform ownerRoot = labelPool.transform as RectTransform;

            ValidateRange(labelPool,
                          ownerRoot,
                          5,
                          PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity,
                          260f,
                          46f);
            ValidateRange(labelPool,
                          ownerRoot,
                          10,
                          10,
                          400f,
                          40f);
            ValidateUniformDistribution(labelPool,
                                        ownerRoot,
                                        12f,
                                        5,
                                        400f,
                                        40f);
            ValidateHiddenDistribution(labelPool,
                                       ownerRoot,
                                       12f,
                                       400f,
                                       40f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }
    #endregion

    #region Validation
    /// <summary>
    /// Validates one fixed-unit range and confirms every integer label is active and aligned.
    /// </summary>
    /// <param name="labelPool">Preauthored label pool under test.</param>
    /// <param name="ownerRoot">RectTransform owning the labels used by this pool.</param>
    /// <param name="expectedMaximum">Expected integer maximum value represented by the last label.</param>
    /// <param name="maximumLabelCount">Maximum labels allowed by this test case.</param>
    /// <param name="chamberPixelWidth">Available pixel width used by the label fit calculation.</param>
    /// <param name="minimumLabelSpacing">Minimum horizontal pixel spacing used by the label fit calculation.</param>
    private static void ValidateRange(PlayerSyringeBarLabelPool labelPool,
                                      RectTransform ownerRoot,
                                      int expectedMaximum,
                                      int maximumLabelCount,
                                      float chamberPixelWidth,
                                      float minimumLabelSpacing)
    {
        labelPool.Rebuild(ownerRoot,
                          expectedMaximum,
                          1f,
                          PlayerSyringeGraduationMode.FixedUnits,
                          0,
                          1,
                          maximumLabelCount,
                          chamberPixelWidth,
                          minimumLabelSpacing,
                          PlayerSyringeLabelPlacement.InsideChamber,
                          15f,
                          new float2(0f, 0f),
                          0f,
                          new float4(0f, 0f, 0f, 1f),
                          new float4(1f, 1f, 1f, 1f),
                          0.1f,
                          null,
                          false);

        TMP_Text[] labels = ownerRoot != null
            ? ownerRoot.GetComponentsInChildren<TMP_Text>(true)
            : labelPool.GetComponentsInChildren<TMP_Text>(true);
        bool[] foundValues = new bool[expectedMaximum + 1];
        bool anchorsAligned = true;
        int activeCount = 0;

        for (int index = 0; index < labels.Length; index++)
        {
            if (!labels[index].gameObject.activeSelf)
                continue;

            activeCount++;

            if (!int.TryParse(labels[index].text, out int representedValue) ||
                representedValue <= 0 ||
                representedValue > expectedMaximum)
            {
                throw new InvalidOperationException(string.Format("Unexpected syringe label '{0}' while validating 1-{1}.",
                                                                  labels[index].text,
                                                                  expectedMaximum));
            }

            foundValues[representedValue] = true;

            if (!Mathf.Approximately(labels[index].rectTransform.anchorMin.x, representedValue / (float)expectedMaximum))
                anchorsAligned = false;
        }

        for (int valueIndex = 1; valueIndex <= expectedMaximum; valueIndex++)
        {
            if (!foundValues[valueIndex])
                throw new InvalidOperationException(string.Format("Fixed-unit label pool skipped graduation {0} while validating 1-{1}.",
                                                                  valueIndex,
                                                                  expectedMaximum));
        }

        if (activeCount != expectedMaximum || !anchorsAligned)
        {
            throw new InvalidOperationException(string.Format("Fixed-unit label pool expected {0} aligned labels but produced {1}.",
                                                              expectedMaximum,
                                                              activeCount));
        }
    }

    /// <summary>
    /// Validates that Uniform Labels mode spaces the requested number of labels from zero through the maximum.
    /// </summary>
    /// <param name="labelPool">Preauthored label pool under test.</param>
    /// <param name="ownerRoot">RectTransform owning the labels used by this pool.</param>
    /// <param name="maximumValue">Maximum value represented by the test range.</param>
    /// <param name="uniformLabelCount">Requested uniform label count.</param>
    /// <param name="chamberPixelWidth">Available pixel width used by the label fit calculation.</param>
    /// <param name="minimumLabelSpacing">Minimum horizontal pixel spacing used by the label fit calculation.</param>
    private static void ValidateUniformDistribution(PlayerSyringeBarLabelPool labelPool,
                                                    RectTransform ownerRoot,
                                                    float maximumValue,
                                                    int uniformLabelCount,
                                                    float chamberPixelWidth,
                                                    float minimumLabelSpacing)
    {
        labelPool.Rebuild(ownerRoot,
                          maximumValue,
                          1f,
                          PlayerSyringeGraduationMode.UniformLabels,
                          uniformLabelCount,
                          1,
                          PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity,
                          chamberPixelWidth,
                          minimumLabelSpacing,
                          PlayerSyringeLabelPlacement.InsideChamber,
                          15f,
                          new float2(0f, 0f),
                          0f,
                          new float4(0f, 0f, 0f, 1f),
                          new float4(1f, 1f, 1f, 1f),
                          0.1f,
                          null,
                          false);

        TMP_Text[] labels = ownerRoot != null
            ? ownerRoot.GetComponentsInChildren<TMP_Text>(true)
            : labelPool.GetComponentsInChildren<TMP_Text>(true);
        int activeCount = 0;

        for (int index = 0; index < labels.Length; index++)
        {
            if (!labels[index].gameObject.activeSelf)
                continue;

            float expectedNormalized = activeCount / (float)(uniformLabelCount - 1);

            if (!Mathf.Approximately(labels[index].rectTransform.anchorMin.x, expectedNormalized))
                throw new InvalidOperationException("Uniform label distribution produced a misaligned label.");

            activeCount++;
        }

        if (activeCount != uniformLabelCount)
        {
            throw new InvalidOperationException(string.Format("Uniform label distribution expected {0} labels but produced {1}.",
                                                              uniformLabelCount,
                                                              activeCount));
        }
    }

    /// <summary>
    /// Validates that Hidden mode disables every preauthored label.
    /// </summary>
    /// <param name="labelPool">Preauthored label pool under test.</param>
    /// <param name="ownerRoot">RectTransform owning the labels used by this pool.</param>
    /// <param name="maximumValue">Maximum value represented by the test range.</param>
    /// <param name="chamberPixelWidth">Available pixel width used by the label fit calculation.</param>
    /// <param name="minimumLabelSpacing">Minimum horizontal pixel spacing used by the label fit calculation.</param>
    private static void ValidateHiddenDistribution(PlayerSyringeBarLabelPool labelPool,
                                                   RectTransform ownerRoot,
                                                   float maximumValue,
                                                   float chamberPixelWidth,
                                                   float minimumLabelSpacing)
    {
        labelPool.Rebuild(ownerRoot,
                          maximumValue,
                          1f,
                          PlayerSyringeGraduationMode.Hidden,
                          0,
                          1,
                          PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity,
                          chamberPixelWidth,
                          minimumLabelSpacing,
                          PlayerSyringeLabelPlacement.InsideChamber,
                          15f,
                          new float2(0f, 0f),
                          0f,
                          new float4(0f, 0f, 0f, 1f),
                          new float4(1f, 1f, 1f, 1f),
                          0.1f,
                          null,
                          false);

        TMP_Text[] labels = ownerRoot != null
            ? ownerRoot.GetComponentsInChildren<TMP_Text>(true)
            : labelPool.GetComponentsInChildren<TMP_Text>(true);

        for (int index = 0; index < labels.Length; index++)
        {
            if (labels[index].gameObject.activeSelf)
                throw new InvalidOperationException("Hidden graduation mode left a numeric label active.");
        }
    }
    #endregion

    #endregion
}
