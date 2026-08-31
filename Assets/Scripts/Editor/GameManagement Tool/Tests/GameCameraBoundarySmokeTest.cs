using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Verifies camera-boundary authoring, Scene Manager propagation and deterministic planar constraint math.
/// </summary>
public static class GameCameraBoundarySmokeTest
{
    #region Constants
    private const float TestTolerance = 0.001f;
    private const string PlayerPrefabPath = "Assets/Prefabs/Player/PF_Player.prefab";
    private const string SceneManagerPresetPath =
        "Assets/Scriptable Objects/Game/Scene Management/GameSceneManagerPreset.asset";
    #endregion

    #region Methods

    #region Test Methods
    // [MenuItem("Tools/Tests/Editor/Camera Boundary Smoke Test")]
    /// <summary>
    /// Runs deterministic checks without entering Play Mode or retaining temporary scene objects.
    /// </summary>
    public static void Run()
    {
        ValidateFastPlayConfiguration();
        ValidateSceneManagerPropagation();
        ValidateAuthoringConversion();
        ValidateConstraintMath();
        ValidateCompoundConstraintMath();
        ValidateTransitionRevealReadiness();
        Debug.Log("[GameCameraBoundarySmokeTest] All deterministic checks passed.");
    }

    /// <summary>
    /// Confirms Fast Play can resolve the real player prefab, its presets and the configured persistent camera hierarchy.
    /// </summary>
    private static void ValidateFastPlayConfiguration()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        PlayerAuthoring playerAuthoring = playerPrefab != null ? playerPrefab.GetComponent<PlayerAuthoring>() : null;
        Assert(playerAuthoring != null && playerAuthoring.MasterPreset != null,
               "Fast Play cannot resolve the project player prefab and master preset.");
        Assert(playerAuthoring.MasterPreset.ControllerPreset != null,
               "Fast Play cannot resolve the player controller preset.");
        float resolvedMovementSpeed =
            GameCameraBoundaryFastPlayEditorUtility.ResolveMovementSpeed(playerAuthoring.MasterPreset);
        Assert(resolvedMovementSpeed > 0f,
               "Fast Play did not resolve a positive movement speed through the unified scaling formulas.");

        GameSceneManagerPreset sceneManagerPreset =
            AssetDatabase.LoadAssetAtPath<GameSceneManagerPreset>(SceneManagerPresetPath);
        Assert(sceneManagerPreset != null,
               "Fast Play cannot resolve the default Scene Manager preset.");
        Assert(sceneManagerPreset.TryFindScene(sceneManagerPreset.BootstrapSceneId,
                                               out GameSceneDefinition bootstrapDefinition),
               "Fast Play cannot resolve the configured bootstrap scene definition.");
        Assert(!string.IsNullOrWhiteSpace(bootstrapDefinition.ScenePath),
               "The configured bootstrap definition has no scene path.");

        Scene bootstrapPreviewScene = EditorSceneManager.OpenPreviewScene(bootstrapDefinition.ScenePath);

        try
        {
            GameObject[] roots = bootstrapPreviewScene.GetRootGameObjects();
            bool foundPersistentCamera = false;

            // Inspect the configured bootstrap hierarchy without opening or modifying its scene asset.
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                if (roots[rootIndex].GetComponentInChildren<GameSceneBootstrapCameraView>(true) == null)
                    continue;

                foundPersistentCamera = true;
                break;
            }

            Assert(foundPersistentCamera,
                   "The configured bootstrap scene has no persistent gameplay camera hierarchy.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(bootstrapPreviewScene);
        }
    }

    /// <summary>
    /// Confirms both new Gameplay Camera fields reach the ECS singleton configuration unchanged when valid.
    /// </summary>
    private static void ValidateSceneManagerPropagation()
    {
        GameSceneManagerPreset preset = ScriptableObject.CreateInstance<GameSceneManagerPreset>();

        try
        {
            // Set isolated serialized inputs through Unity's editor serialization path.
            SerializedObject serializedPreset = new SerializedObject(preset);
            SerializedProperty enableProperty = serializedPreset.FindProperty("enableCameraBoundaries");
            SerializedProperty modeProperty = serializedPreset.FindProperty("cameraBoundaryMode");
            SerializedProperty softZoneProperty = serializedPreset.FindProperty("cameraBoundarySoftZoneDistance");
            Assert(enableProperty != null, "The enableCameraBoundaries preset field is missing.");
            Assert(modeProperty != null, "The cameraBoundaryMode preset field is missing.");
            Assert(softZoneProperty != null, "The cameraBoundarySoftZoneDistance preset field is missing.");
            enableProperty.boolValue = false;
            modeProperty.enumValueIndex = (int)GameCameraBoundaryMode.ImpassableVolume;
            softZoneProperty.floatValue = 4.25f;
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();

            // Verify the public preset accessors and baked ECS values together.
            GameSceneManagerConfig config = GameSceneManagementBakeUtility.BuildConfig(preset);
            Assert(!preset.EnableCameraBoundaries, "The boundary enable accessor did not retain the serialized value.");
            Assert(preset.CameraBoundaryMode == GameCameraBoundaryMode.ImpassableVolume,
                   "The boundary mode accessor did not retain the serialized value.");
            Assert(math.abs(preset.CameraBoundarySoftZoneDistance - 4.25f) <= TestTolerance,
                   "The boundary soft-zone accessor did not retain the serialized value.");
            Assert(config.EnableCameraBoundaries == 0, "The boundary enable value did not reach ECS config.");
            Assert(config.CameraBoundaryMode == GameCameraBoundaryMode.ImpassableVolume,
                   "The impassable boundary mode did not reach ECS config.");
            Assert(math.abs(config.CameraBoundarySoftZoneDistance - 4.25f) <= TestTolerance,
                   "The boundary soft-zone value did not reach ECS config.");

            // Verify invalid authoring reports a warning without mutating the preset.
            enableProperty.boolValue = true;
            softZoneProperty.floatValue = -2f;
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
            List<string> warnings = new List<string>();
            GameSceneManagerPresetValidationUtility.CollectWarnings(preset, warnings);
            bool foundBoundaryWarning = false;

            foreach (string warning in warnings)
            {
                if (!warning.Contains("Camera Boundary Soft Zone Distance"))
                    continue;

                foundBoundaryWarning = true;
                break;
            }

            Assert(foundBoundaryWarning, "A negative boundary soft-zone distance did not produce a validation warning.");
            Assert(math.abs(preset.CameraBoundarySoftZoneDistance + 2f) <= TestTolerance,
                   "Validation rewrote the authored boundary soft-zone distance.");

            // Verify defensive defaults used when no preset is available.
            GameSceneManagerConfig defaultConfig = GameSceneManagementBakeUtility.BuildConfig(null);
            Assert(defaultConfig.EnableCameraBoundaries != 0, "Camera boundaries should default to enabled.");
            Assert(defaultConfig.CameraBoundaryMode == GameCameraBoundaryMode.ContainmentVolume,
                   "Camera boundaries should default to containment mode.");
            Assert(math.abs(defaultConfig.CameraBoundarySoftZoneDistance - GameCameraBoundaryDefaults.SoftZoneDistance) <=
                   TestTolerance,
                   "The ECS fallback soft-zone distance differs from the shared default.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(preset);
        }
    }

    /// <summary>
    /// Confirms BoxCollider center, scale, rotation and priority produce stable world-space ECS boundary data.
    /// </summary>
    private static void ValidateAuthoringConversion()
    {
        GameObject boundaryObject = new GameObject("Camera Boundary Smoke Test");

        try
        {
            // Author a scaled and rotated collider without saving it into a scene.
            boundaryObject.transform.SetPositionAndRotation(new Vector3(3f, 7f, -2f),
                                                            Quaternion.Euler(25f, 30f, -12f));
            boundaryObject.transform.localScale = new Vector3(2f, 1f, 0.5f);
            BoxCollider boundaryCollider = boundaryObject.AddComponent<BoxCollider>();
            boundaryCollider.center = new Vector3(1f, 9f, -1f);
            boundaryCollider.size = new Vector3(10f, 40f, 8f);
            GameCameraBoundaryAuthoring authoring = boundaryObject.AddComponent<GameCameraBoundaryAuthoring>();

            // Compare against a yaw-only footprint so height, pitch, roll and collider depth cannot affect ECS data.
            Assert(authoring.TryBuildBoundary(out GameCameraBoundary boundary),
                    "Valid Camera Boundary authoring did not produce ECS data.");
            Quaternion expectedRotation = Quaternion.Euler(0f, boundaryObject.transform.eulerAngles.y, 0f);
            Vector3 expectedCenter = boundaryObject.transform.position +
                                     expectedRotation * new Vector3(2f, 0f, -0.5f);
            Vector3 expectedRight = expectedRotation * Vector3.right;
            Assert(math.distance(boundary.Center, new float2(expectedCenter.x, expectedCenter.z)) <=
                   TestTolerance,
                   "The baked boundary center is not a yaw-only horizontal projection.");
            Assert(math.distance(boundary.HalfExtents, new float2(10f, 2f)) <= TestTolerance,
                   "The baked boundary extents do not contain only scaled XZ dimensions.");
            Assert(math.distance(boundary.PlanarRight, new float2(expectedRight.x, expectedRight.z)) <=
                   TestTolerance,
                   "The baked boundary orientation contains pitch or roll.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(boundaryObject);
        }
    }

    /// <summary>
    /// Confirms soft braking remains inside the footprint and the hard pass cancels only outward velocity.
    /// </summary>
    private static void ValidateConstraintMath()
    {
        GameCameraBoundary boundary = new GameCameraBoundary
        {
            Center = float2.zero,
            HalfExtents = new float2(10f, 8f),
            PlanarRight = new float2(1f, 0f),
            Priority = 0
        };

        // Verify selection and progressive target compression near the positive X edge.
        Assert(GameCameraBoundaryUtility.Contains(in boundary, new float3(9f, 20f, 0f)),
                "Planar containment should ignore world-space height.");
        Assert(GameCameraBoundaryUtility.Contains(in boundary, new float3(9f, -200f, 0f)),
                "Planar containment changed when only world-space height changed.");
        float3 softPosition = GameCameraBoundaryUtility.ResolveSoftConstrainedPosition(in boundary,
                                                                                       new float3(9f, 4f, 0f),
                                                                                       3f);
        Assert(softPosition.x > 7f && softPosition.x < 9f,
               "Soft braking should retain progress while holding the target inside its unconstrained position.");
        Assert(math.abs(softPosition.y - 4f) <= TestTolerance,
               "Planar soft braking should preserve camera height.");

        // Verify the non-negotiable clamp and selective spring-velocity cancellation.
        float3 hardPosition = GameCameraBoundaryUtility.ResolveHardConstrainedPosition(in boundary,
                                                                                       new float3(14f, 4f, -9f));
        Assert(math.distance(hardPosition, new float3(10f, 4f, -8f)) <= TestTolerance,
               "The hard constraint did not clamp both planar axes.");
        float3 velocity = new float3(2f, 1f, -3f);
        GameCameraBoundaryUtility.CancelOutwardVelocity(in boundary, hardPosition, ref velocity);
        Assert(math.distance(velocity, new float3(0f, 1f, 0f)) <= TestTolerance,
               "The hard-edge pass did not cancel outward planar velocity while preserving height velocity.");

        // Verify a zero soft zone retains exact hard-clamp behavior.
        float3 zeroSoftPosition = GameCameraBoundaryUtility.ResolveSoftConstrainedPosition(in boundary,
                                                                                           new float3(12f, 4f, 0f),
                                                                                           0f);
        Assert(math.distance(zeroSoftPosition, new float3(10f, 4f, 0f)) <= TestTolerance,
                "A zero soft zone should resolve directly to the hard limit.");

        // A disjoint boundary hand-off must let the camera spring enter before hard containment becomes authoritative.
        Assert(!GameCameraBoundaryUtility.ShouldApplyHardConstraint(in boundary,
                                                                    new float3(14f, 4f, 0f),
                                                                    new float3(12f, 4f, 0f)),
               "Hard containment should remain inactive while the camera is still outside a new footprint.");
        Assert(GameCameraBoundaryUtility.ShouldApplyHardConstraint(in boundary,
                                                                   new float3(12f, 4f, 0f),
                                                                   new float3(9.5f, 4f, 0f)),
               "Hard containment should activate as soon as the spring enters the new footprint.");

        // Impassable mode brakes outside the approached face and prevents a complete high-speed crossing.
        float3 blockedTarget = GameCameraBoundaryImpassableUtility.ResolveSoftBlockedPosition(
            in boundary,
            new float3(-15f, 4f, 0f),
            new float3(-9f, 4f, 0f),
            3f);
        Assert(blockedTarget.x < -10f && blockedTarget.x > -15f,
               "Impassable soft braking did not retain the camera outside the approached face.");
        float3 crossingCandidate = new float3(15f, 4f, 0f);
        float3 crossingVelocity = new float3(20f, 1f, 0f);
        GameCameraBoundaryImpassableUtility.ApplyHardConstraint(in boundary,
                                                                new float3(-15f, 4f, 0f),
                                                                ref crossingCandidate,
                                                                ref crossingVelocity);
        Assert(crossingCandidate.x < -10f && crossingCandidate.x > -10.01f,
               "Impassable hard constraint did not stop a complete footprint crossing at the entry face.");
        Assert(math.abs(crossingVelocity.x) <= TestTolerance &&
               math.abs(crossingVelocity.y - 1f) <= TestTolerance,
               "Impassable hard constraint did not cancel only inward planar velocity.");

        // Enabling obstacle mode around an already enclosed camera must allow recovery without a direct snap.
        float3 recoveryTarget = GameCameraBoundaryImpassableUtility.ResolveSoftBlockedPosition(
            in boundary,
            new float3(0f, 4f, 0f),
            new float3(12f, 4f, 0f),
            3f);
        Assert(math.distance(recoveryTarget, new float3(12f, 4f, 0f)) <= TestTolerance,
               "An already enclosed camera was prevented from leaving an impassable footprint.");
    }

    /// <summary>
    /// Confirms overlapping equal-priority footprints behave as one continuous L-shaped containment path.
    /// Internal seams preserve target and velocity continuity while external edges remain authoritative.
    /// </summary>
    private static void ValidateCompoundConstraintMath()
    {
        World world = new World("Camera Boundary Compound Constraint Test");

        try
        {
            GameCameraBoundary horizontalBoundary = new GameCameraBoundary
            {
                Center = float2.zero,
                HalfExtents = new float2(6f, 2f),
                PlanarRight = new float2(1f, 0f),
                Priority = 0
            };
            GameCameraBoundary verticalBoundary = new GameCameraBoundary
            {
                Center = new float2(4f, 4f),
                HalfExtents = new float2(2f, 6f),
                PlanarRight = new float2(1f, 0f),
                Priority = 0
            };
            GameCameraBoundary edgeTouchingBoundary = new GameCameraBoundary
            {
                Center = new float2(12f, 0f),
                HalfExtents = new float2(6f, 2f),
                PlanarRight = new float2(1f, 0f),
                Priority = 0
            };
            GameCameraBoundary priorityOverrideBoundary = verticalBoundary;
            priorityOverrideBoundary.Priority = 1;

            // Only same-priority positive-area overlaps form passages; edge contact and overrides remain separate.
            Assert(GameCameraBoundaryUtility.Overlaps(in horizontalBoundary, in verticalBoundary),
                   "Overlapping perpendicular footprints were not recognized as one continuous path.");
            Assert(GameCameraBoundaryUtility.CanShareContainmentGroup(in horizontalBoundary, in verticalBoundary),
                   "Equal-priority overlapping footprints were not accepted into one containment group.");
            Assert(!GameCameraBoundaryUtility.Overlaps(in horizontalBoundary, in edgeTouchingBoundary),
                   "Edge-only footprint contact was incorrectly treated as a stable camera passage.");
            Assert(!GameCameraBoundaryUtility.CanShareContainmentGroup(in horizontalBoundary,
                                                                        in priorityOverrideBoundary),
                   "A higher-priority overlap was incorrectly merged into the active containment group.");

            Entity groupEntity = world.EntityManager.CreateEntity();
            DynamicBuffer<GameCameraBoundaryContainmentElement> containmentBoundaries =
                world.EntityManager.AddBuffer<GameCameraBoundaryContainmentElement>(groupEntity);
            containmentBoundaries.Add(new GameCameraBoundaryContainmentElement
            {
                BoundaryEntity = world.EntityManager.CreateEntity(typeof(GameCameraBoundary)),
                Boundary = horizontalBoundary
            });
            containmentBoundaries.Add(new GameCameraBoundaryContainmentElement
            {
                BoundaryEntity = world.EntityManager.CreateEntity(typeof(GameCameraBoundary)),
                Boundary = verticalBoundary
            });

            // A target inside either branch must pass through unchanged instead of being clamped to the seed volume.
            float3 verticalBranchTarget = new float3(4f, 3f, 5f);
            float3 compoundSoftPosition = GameCameraBoundaryUtility.ResolveSoftConstrainedPosition(
                containmentBoundaries,
                verticalBranchTarget,
                3f);
            Assert(math.distance(compoundSoftPosition, verticalBranchTarget) <= TestTolerance,
                   "Compound soft containment still brakes a target inside the second path branch.");

            // Crossing the seed volume's top seam remains valid because the perpendicular member contains that motion.
            float3 seamVelocity = new float3(0f, 0f, 3f);
            GameCameraBoundaryUtility.CancelOutwardVelocity(containmentBoundaries,
                                                            new float3(4f, 3f, 2f),
                                                            ref seamVelocity);
            Assert(math.distance(seamVelocity, new float3(0f, 0f, 3f)) <= TestTolerance,
                   "An internal overlap seam removed camera velocity allowed by another group member.");

            // A diagonal spring step into the missing quadrant must stop at the concave corner it actually crossed.
            float3 concaveCornerCandidate = new float3(0f, 3f, 4f);
            float3 concaveCornerVelocity = new float3(-3f, 0f, 3f);
            GameCameraBoundaryUtility.ApplyReachableHardConstraint(containmentBoundaries,
                                                                   new float3(3f, 3f, 1f),
                                                                   ref concaveCornerCandidate,
                                                                   ref concaveCornerVelocity);
            Assert(math.distance(concaveCornerCandidate, new float3(2f, 3f, 2f)) <= TestTolerance,
                   "Compound hard containment projected across a concave corner instead of stopping at its first exit.");

            // The nearest external edge still clamps position and removes only its outward velocity component.
            float3 externalCandidate = new float3(7f, 3f, 8f);
            float3 externalVelocity = new float3(4f, 1f, 2f);
            GameCameraBoundaryUtility.ApplyReachableHardConstraint(containmentBoundaries,
                                                                   new float3(5.5f, 3f, 8f),
                                                                   ref externalCandidate,
                                                                   ref externalVelocity);
            Assert(math.distance(externalCandidate, new float3(6f, 3f, 8f)) <= TestTolerance,
                   "Compound hard containment did not select the closest external group edge.");
            Assert(math.distance(externalVelocity, new float3(0f, 1f, 2f)) <= TestTolerance,
                   "Compound hard containment removed tangential or vertical velocity at an external edge.");
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Confirms containment-enabled gameplay transitions remain opaque until camera presentation acknowledges the
    /// destination pose, without delaying scenes or boundary modes that do not require the handshake.
    /// </summary>
    private static void ValidateTransitionRevealReadiness()
    {
        GameSceneManagerConfig config = new GameSceneManagerConfig
        {
            EnableCameraBoundaries = 1,
            CameraBoundaryMode = GameCameraBoundaryMode.ContainmentVolume
        };
        GameSceneDefinitionElement targetScene = new GameSceneDefinitionElement
        {
            SceneKind = GameSceneKind.Gameplay
        };
        GameSceneTransitionState transitionState = new GameSceneTransitionState
        {
            Phase = GameSceneTransitionPhase.HoldBlack,
            IsTransitioning = 1
        };

        // Gameplay containment must block reveal while allowing camera systems to replace traversal framing under black.
        GameSceneTransitionCameraReadinessUtility.InitializeForReveal(ref transitionState, config, targetScene);
        Assert(GameSceneTransitionCameraReadinessUtility.IsPreparationPending(in transitionState),
               "Containment gameplay did not request hidden camera preparation.");
        Assert(GameSceneTransitionCameraReadinessUtility.UsesPreparedFraming(in transitionState),
               "Hidden camera preparation did not release procedural source framing.");
        Assert(!GameSceneTransitionCameraReadinessUtility.UsesPreparedRevealFraming(in transitionState),
               "Player presentation switched to destination framing before camera preparation completed.");
        Assert(!GameSceneTransitionCameraReadinessUtility.CanReveal(in transitionState),
               "Fade-in was allowed before camera preparation completed.");

        // The presentation acknowledgment must open the gate without disabling prepared framing during fade-in.
        GameSceneTransitionCameraReadinessUtility.MarkPrepared(ref transitionState);
        transitionState.Phase = GameSceneTransitionPhase.FadeIn;
        Assert(GameSceneTransitionCameraReadinessUtility.CanReveal(in transitionState),
               "Camera preparation acknowledgment did not release fade-in.");
        Assert(GameSceneTransitionCameraReadinessUtility.UsesPreparedFraming(in transitionState),
               "Prepared destination framing was not retained throughout fade-in.");
        Assert(GameSceneTransitionCameraReadinessUtility.UsesPreparedRevealFraming(in transitionState),
               "Prepared player presentation did not join the destination reveal framing.");

        // Impassable mode and non-gameplay targets preserve the existing reveal path without waiting for a camera writer.
        config.CameraBoundaryMode = GameCameraBoundaryMode.ImpassableVolume;
        GameSceneTransitionCameraReadinessUtility.InitializeForReveal(ref transitionState, config, targetScene);
        Assert(GameSceneTransitionCameraReadinessUtility.CanReveal(in transitionState),
               "Impassable boundaries incorrectly blocked scene reveal.");
        Assert(!GameSceneTransitionCameraReadinessUtility.UsesPreparedRevealFraming(in transitionState),
               "Impassable boundaries incorrectly changed player reveal framing.");
        config.CameraBoundaryMode = GameCameraBoundaryMode.ContainmentVolume;
        targetScene.SceneKind = GameSceneKind.MainMenu;
        GameSceneTransitionCameraReadinessUtility.InitializeForReveal(ref transitionState, config, targetScene);
        Assert(GameSceneTransitionCameraReadinessUtility.CanReveal(in transitionState),
               "A non-gameplay scene incorrectly waited for camera containment.");
    }
    #endregion

    #region Assertion Methods
    /// <summary>
    /// Stops the batch smoke test with a diagnostic when one deterministic condition fails.
    /// </summary>
    /// <param name="condition">Condition required for the implementation contract.</param>
    /// <param name="message">Diagnostic included in the thrown exception.</param>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("[GameCameraBoundarySmokeTest] " + message);
    }
    #endregion

    #endregion
}
