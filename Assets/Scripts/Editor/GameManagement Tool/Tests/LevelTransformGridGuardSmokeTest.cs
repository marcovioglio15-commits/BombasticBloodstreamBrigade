using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Verifies near-grid audit, tolerance-bounded repair and explicit full snapping in an isolated preview scene.
/// </summary>
public static class LevelTransformGridGuardSmokeTest
{
    #region Methods

    #region Entry Point

    // [MenuItem("Tools/Tests/Editor/Level Transform Grid Guard Smoke Test")]
    /// <summary>
    /// Runs deterministic transform-grid checks and throws when any authoring invariant fails.
    /// </summary>
    public static void Run()
    {
        Vector3 previousMoveIncrement = EditorSnapSettings.move;
        float previousRotationIncrement = EditorSnapSettings.rotate;
        float previousScaleIncrement = EditorSnapSettings.scale;
        Scene previewScene = EditorSceneManager.NewPreviewScene();
        GameObject testObject = new GameObject("TransformGridGuardSmokeTarget");
        SceneManager.MoveGameObjectToScene(testObject, previewScene);

        try
        {
            EditorSnapSettings.move = Vector3.one;
            EditorSnapSettings.rotate = 15f;
            EditorSnapSettings.scale = 0.25f;
            ValidateNearDriftRepair(testObject.transform);
            ValidateExplicitSnap(testObject.transform);
            Debug.Log("Level Transform Grid Guard smoke test passed.");
        }
        finally
        {
            EditorSnapSettings.move = previousMoveIncrement;
            EditorSnapSettings.rotate = previousRotationIncrement;
            EditorSnapSettings.scale = previousScaleIncrement;
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    #endregion

    #region Validation Methods

    /// <summary>
    /// Verifies that only small deviations inside configured tolerances are audited and repaired.
    /// </summary>
    /// <param name="target">Isolated scene transform used by the smoke test.</param>
    private static void ValidateNearDriftRepair(Transform target)
    {
        target.position = new Vector3(1.0005f, 1.9995f, -0.0005f);
        target.localRotation = Quaternion.Euler(0.02f, 14.98f, 0.02f);
        target.localScale = new Vector3(1.0005f, 0.9995f, 1.0005f);
        LevelTransformGridSnapPolicy policy = new LevelTransformGridSnapPolicy(true,
                                                                               true,
                                                                               true,
                                                                               0.01f,
                                                                               1f,
                                                                               0.01f);
        List<LevelTransformGridDrift> drifts = LevelTransformGridSnapUtility.Audit(
            new List<Transform> { target },
            policy);

        if (drifts.Count != 1 || drifts[0].Channels !=
            (LevelTransformGridChannel.Position | LevelTransformGridChannel.Rotation | LevelTransformGridChannel.Scale))
            throw new InvalidOperationException("Grid Guard audit did not identify every near-drift transform channel. Records: " +
                                                drifts.Count + ", channels: " +
                                                (drifts.Count > 0 ? drifts[0].Channels.ToString() : "None") + ".");

        if (LevelTransformGridSnapUtility.RepairNearDrift(drifts, policy) != 1)
            throw new InvalidOperationException("Grid Guard near-drift repair did not change the isolated transform.");

        AssertTransform(target, new Vector3(1f, 2f, 0f), Quaternion.Euler(0f, 15f, 0f), Vector3.one);
    }

    /// <summary>
    /// Verifies that the explicit operation snaps values outside near-drift tolerance on enabled channels.
    /// </summary>
    /// <param name="target">Isolated scene transform used by the smoke test.</param>
    private static void ValidateExplicitSnap(Transform target)
    {
        target.position = new Vector3(1.4f, 2.6f, -1.4f);
        target.localRotation = Quaternion.Euler(0f, 22f, 0f);
        target.localScale = new Vector3(1.3f, 0.7f, 1.3f);
        LevelTransformGridSnapPolicy policy = new LevelTransformGridSnapPolicy(true,
                                                                               true,
                                                                               true,
                                                                               0.001f,
                                                                               0.1f,
                                                                               0.001f);

        if (LevelTransformGridSnapUtility.ForceSnap(new List<Transform> { target }, policy) != 1)
            throw new InvalidOperationException("Grid Guard explicit snap did not change the isolated transform.");

        AssertTransform(target,
                        new Vector3(1f, 3f, -1f),
                        Quaternion.Euler(0f, 15f, 0f),
                        new Vector3(1.25f, 0.75f, 1.25f));
    }

    /// <summary>
    /// Compares one transform with expected position, rotation and scale using serialization-safe tolerances.
    /// </summary>
    /// <param name="target">Transform containing actual values.</param>
    /// <param name="expectedPosition">Expected world position.</param>
    /// <param name="expectedRotation">Expected local rotation.</param>
    /// <param name="expectedScale">Expected local scale.</param>
    private static void AssertTransform(Transform target,
                                        Vector3 expectedPosition,
                                        Quaternion expectedRotation,
                                        Vector3 expectedScale)
    {
        if ((target.position - expectedPosition).sqrMagnitude > 0.0000001f)
            throw new InvalidOperationException("Grid Guard produced an unexpected snapped position: " + target.position + ".");

        if (Quaternion.Angle(target.localRotation, expectedRotation) > 0.001f)
            throw new InvalidOperationException("Grid Guard produced an unexpected snapped rotation: " + target.localEulerAngles + ".");

        if ((target.localScale - expectedScale).sqrMagnitude > 0.0000001f)
            throw new InvalidOperationException("Grid Guard produced an unexpected snapped scale: " + target.localScale + ".");
    }

    #endregion

    #endregion
}
