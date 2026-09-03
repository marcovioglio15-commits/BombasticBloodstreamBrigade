using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Verifies recorder-camera authoring conversion, deterministic cycling, look rotation and configurable input wiring.
/// </summary>
public static class GameRecorderCameraSmokeTest
{
    #region Constants
    private const float TestTolerance = 0.001f;
    #endregion

    #region Methods

    #region Test Methods
    // [MenuItem("Tools/Tests/Editor/Recorder Camera Smoke Test")]
    /// <summary>
    /// Runs deterministic recorder-camera checks without entering Play Mode or retaining temporary objects.
    /// </summary>
    public static void Run()
    {
        ValidateAuthoringConversion();
        ValidateCycleOrder();
        ValidateLookRotation();
        ValidateInputAction();
        Debug.Log("[GameRecorderCameraSmokeTest] All deterministic checks passed.");
    }

    /// <summary>
    /// Confirms a scene marker preserves world pose, projection and cycle order in its immutable ECS payload.
    /// </summary>
    private static void ValidateAuthoringConversion()
    {
        GameObject recorderObject = new GameObject("Recorder Camera Smoke Test");

        try
        {
            recorderObject.transform.SetPositionAndRotation(new Vector3(4f, 12f, -7f),
                                                            Quaternion.Euler(24f, 35f, 0f));
            Camera cameraComponent = recorderObject.AddComponent<Camera>();
            cameraComponent.orthographic = true;
            cameraComponent.orthographicSize = 8f;
            cameraComponent.nearClipPlane = 0.25f;
            cameraComponent.farClipPlane = 180f;
            GameRecorderCameraAuthoring authoring = recorderObject.AddComponent<GameRecorderCameraAuthoring>();
            SerializedObject serializedAuthoring = new SerializedObject(authoring);
            SerializedProperty cycleOrderProperty = serializedAuthoring.FindProperty("cycleOrder");
            Assert(cycleOrderProperty != null, "Recorder Camera cycleOrder authoring field is missing.");
            SerializedProperty alignMovementProperty = serializedAuthoring.FindProperty("alignMovementToCamera");
            Assert(alignMovementProperty != null,
                   "Recorder Camera alignMovementToCamera authoring field is missing.");
            cycleOrderProperty.intValue = 7;
            alignMovementProperty.boolValue = true;
            serializedAuthoring.ApplyModifiedPropertiesWithoutUndo();

            Assert(authoring.TryBuildRecorderCamera(out GameRecorderCamera recorderCamera),
                   "Valid Recorder Camera authoring did not produce ECS data.");
            Assert(math.distance(recorderCamera.WorldPosition, (float3)recorderObject.transform.position) <= TestTolerance,
                   "Recorder Camera world position did not reach ECS data unchanged.");
            Assert(recorderCamera.Orthographic != 0,
                   "Recorder Camera orthographic mode did not reach ECS data.");
            Assert(math.abs(recorderCamera.OrthographicSize - 8f) <= TestTolerance,
                   "Recorder Camera orthographic size did not reach ECS data unchanged.");
            Assert(recorderCamera.CycleOrder == 7,
                   "Recorder Camera cycle order did not reach ECS data unchanged.");
            Assert(recorderCamera.AlignMovementToCamera != 0,
                   "Recorder Camera movement-alignment option did not reach ECS data.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(recorderObject);
        }
    }

    /// <summary>
    /// Confirms cycling uses authored order, a deterministic entity tie-breaker and a gameplay-camera return step.
    /// </summary>
    private static void ValidateCycleOrder()
    {
        World world = new World("Recorder Camera Selection Smoke Test");
        NativeArray<Entity> entities = new NativeArray<Entity>(3, Allocator.Temp);
        NativeArray<GameRecorderCamera> cameras = new NativeArray<GameRecorderCamera>(3, Allocator.Temp);

        try
        {
            Entity lastEntity = world.EntityManager.CreateEntity();
            Entity firstEntity = world.EntityManager.CreateEntity();
            Entity secondEntity = world.EntityManager.CreateEntity();
            entities[0] = lastEntity;
            entities[1] = firstEntity;
            entities[2] = secondEntity;
            cameras[0] = new GameRecorderCamera { CycleOrder = 20 };
            cameras[1] = new GameRecorderCamera { CycleOrder = 10 };
            cameras[2] = new GameRecorderCamera { CycleOrder = 10 };

            Assert(GameRecorderCameraUtility.TryResolveNext(entities,
                                                            cameras,
                                                            Entity.Null,
                                                            out Entity selectedEntity) &&
                   selectedEntity == firstEntity,
                   "Recorder Camera cycling did not select the lowest ordered viewpoint first.");
            Assert(GameRecorderCameraUtility.TryResolveNext(entities,
                                                            cameras,
                                                            selectedEntity,
                                                            out selectedEntity) &&
                   selectedEntity == secondEntity,
                   "Recorder Camera cycling did not apply the entity tie-breaker.");
            Assert(GameRecorderCameraUtility.TryResolveNext(entities,
                                                            cameras,
                                                            selectedEntity,
                                                            out selectedEntity) &&
                   selectedEntity == lastEntity,
                   "Recorder Camera cycling did not advance to the next authored order.");
            Assert(!GameRecorderCameraUtility.TryResolveNext(entities,
                                                             cameras,
                                                             selectedEntity,
                                                             out selectedEntity),
                   "Recorder Camera cycling did not return control to the gameplay camera after the last viewpoint.");
        }
        finally
        {
            cameras.Dispose();
            entities.Dispose();
            world.Dispose();
        }
    }

    /// <summary>
    /// Confirms player tracking points at regular and vertical targets without singular or non-finite rotations.
    /// </summary>
    private static void ValidateLookRotation()
    {
        GameRecorderCamera recorderCamera = new GameRecorderCamera
        {
            WorldPosition = float3.zero,
            WorldForward = math.forward(),
            WorldUp = math.up()
        };
        quaternion horizontalRotation = GameRecorderCameraUtility.ResolveLookRotation(in recorderCamera,
                                                                                       math.right());
        float3 horizontalForward = math.rotate(horizontalRotation, math.forward());
        Assert(math.distance(horizontalForward, math.right()) <= TestTolerance,
               "Recorder Camera look rotation did not point toward the horizontal player target.");
        quaternion verticalRotation = GameRecorderCameraUtility.ResolveLookRotation(in recorderCamera, math.up());
        float4 verticalValue = verticalRotation.value;
        Assert(math.all(math.isfinite(verticalValue)),
               "Recorder Camera look rotation became non-finite when the target aligned with the authored up axis.");
    }

    /// <summary>
    /// Confirms the shared input asset exposes the configurable recorder-camera action and default F9 chord part.
    /// </summary>
    private static void ValidateInputAction()
    {
        InputActionAsset inputAsset = PlayerInputActionsAssetUtility.LoadOrCreateAsset();
        InputAction cycleAction = inputAsset != null
            ? inputAsset.FindAction("CycleRecorderCamera", false)
            : null;
        Assert(cycleAction != null,
               "The shared Input Actions asset is missing CycleRecorderCamera.");
        bool foundF9Binding = false;

        // Inspect effective binding paths so user rebind overrides remain valid during this check.
        for (int bindingIndex = 0; bindingIndex < cycleAction.bindings.Count; bindingIndex++)
        {
            InputBinding binding = cycleAction.bindings[bindingIndex];
            string bindingPath = string.IsNullOrWhiteSpace(binding.effectivePath)
                ? binding.path
                : binding.effectivePath;

            if (!string.Equals(bindingPath, "<Keyboard>/f9", StringComparison.OrdinalIgnoreCase))
                continue;

            foundF9Binding = true;
            break;
        }

        Assert(foundF9Binding,
               "CycleRecorderCamera has no default F9 button in its configurable cheat chord.");
    }
    #endregion

    #region Assertion Methods
    /// <summary>
    /// Throws one contextual failure consumed by Unity batch-mode reporting.
    /// </summary>
    /// <param name="condition">Condition that must remain true.</param>
    /// <param name="message">Failure detail written when the condition is false.</param>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("[GameRecorderCameraSmokeTest] " + message);
    }
    #endregion

    #endregion
}
