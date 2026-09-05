using System;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Exercises real look and shooting systems with held/released aim and stale child world transforms.
/// Run with Unity -batchmode -executeMethod PlayerControllerAimSmokeTest.Run -quit.
/// </summary>
public static class PlayerControllerAimSmokeTest
{
    #region Constants
    private const float Tolerance = 0.0001f;
    private const float DeltaTime = 1f / 60f;
    #endregion

    #region Methods

    #region Entry Point
    /// <summary>
    /// Verifies projectile requests without loading a scene or modifying input bindings or authored assets.
    /// </summary>
    public static void Run()
    {
        if (EditorApplication.isPlaying || PlayerInputRuntime.IsReady)
            throw new InvalidOperationException("Run the aim smoke test outside Play Mode with no active input runtime.");

        LookDirectionsMode[] modes =
        {
            LookDirectionsMode.AllDirections,
            LookDirectionsMode.DiscreteCount,
            LookDirectionsMode.Cones,
            LookDirectionsMode.FollowMovementDirection
        };

        foreach (LookDirectionsMode mode in modes)
        {
            ValidateAimSequence(mode, RotationMode.Continuous, true);
            ValidateAimSequence(mode, RotationMode.SnapToAllowedDirections, true);
            ValidateAimSequence(mode, RotationMode.SnapToAllowedDirections, false);
            ValidateAimSequence(mode, RotationMode.Continuous, false);
        }

        ValidateMissingMuzzle();
        PlayerProjectileConePatternSmokeTest.Run();
        Debug.Log("[PlayerControllerAimSmokeTest] PASS: 16 aim sequences, current-frame muzzle origins, missing-muzzle fallback and projectile spread.");
    }
    #endregion

    #region Aim Sequences
    /// <summary>
    /// Fires while turning, releasing, crossing the dead zone, and resuming aim, both stationary and strafing.
    /// </summary>
    private static void ValidateAimSequence(LookDirectionsMode mode, RotationMode rotationMode, bool analog)
    {
        using (World world = new World("PlayerControllerAimSmokeTest"))
        using (BlobBuilder builder = new BlobBuilder(Allocator.Temp))
        {
            builder.ConstructRoot<PlayerControllerConfigBlob>();
            using (BlobAssetReference<PlayerControllerConfigBlob> config = builder.CreateBlobAssetReference<PlayerControllerConfigBlob>(Allocator.Persistent))
            {
                EntityManager manager = world.EntityManager;
                Entity player = CreatePlayer(manager, config);
                PlayerRuntimeLookConfig lookConfig = new PlayerRuntimeLookConfig
                {
                    DirectionsMode = mode,
                    DiscreteDirectionCount = 8,
                    RotationMode = rotationMode,
                    RotationSpeed = 180f,
                    FrontCone = new ConeConfig { Enabled = true, AngleDegrees = 20f },
                    Values = new LookValuesBlob
                    {
                        RotationDeadZone = 0.1f,
                        RotationDamping = 0.1f,
                        RotationMaxSpeed = 180f
                    }
                };
                manager.SetComponentData(player, lookConfig);

                LocalTransform mountLocal = LocalTransform.FromPositionRotationScale(new float3(0.2f, 0.4f, 0.3f), quaternion.RotateY(0.2f), 0.8f);
                LocalTransform muzzleLocal = LocalTransform.FromPositionRotationScale(new float3(0.1f, 0.2f, 0.6f), quaternion.RotateY(-0.1f), 1f);
                Entity mount = CreateChild(manager, player, mountLocal);
                Entity muzzle = CreateChild(manager, mount, muzzleLocal);
                manager.AddComponentData(player, new ShooterMuzzleAnchor { AnchorEntity = muzzle });

                PlayerControllerSystemGroup group = CreateGroup(world);
                float2[] samples = { new float2(0.8f, 0.6f), float2.zero, new float2(0.03f, -0.02f), new float2(-0.8f, 0.6f), float2.zero };

                for (int frame = 0; frame < samples.Length; frame++)
                {
                    LocalTransform before = manager.GetComponentData<LocalTransform>(player);
                    // Model movement already applied this frame; LocalToWorld on both children stays deliberately stale.
                    if (frame > 0)
                        before.Position += new float3(0.35f, 0f, -0.1f);

                    manager.SetComponentData(player, before);
                    manager.SetComponentData(player, new PlayerInputState
                    {
                        Move = frame == 0 ? float2.zero : new float2(1f, 0f),
                        MoveUsesAnalogSource = analog ? (byte)1 : (byte)0,
                        Look = analog ? samples[frame] : (frame % 2 == 0 ? new float2(1f, 1f) : float2.zero),
                        LookUsesAnalogSource = analog ? (byte)1 : (byte)0,
                        Shoot = 1f
                    });
                    manager.SetComponentData(player, default(PlayerShootingState));
                    manager.GetBuffer<ShootRequest>(player).Clear();
                    world.SetTime(new TimeData(1d + frame * DeltaTime, DeltaTime));
                    group.Update();

                    LocalTransform after = manager.GetComponentData<LocalTransform>(player);
                    PlayerLookState look = manager.GetComponentData<PlayerLookState>(player);
                    float3 heading = math.normalizesafe(new float3(math.forward(after.Rotation).x, 0f, math.forward(after.Rotation).z));
                    DynamicBuffer<ShootRequest> requests = manager.GetBuffer<ShootRequest>(player);
                    string context = mode + "/" + rotationMode + "/analog=" + analog + "/frame=" + frame;

                    if (requests.Length != 1)
                        throw new InvalidOperationException(context + ": expected one projectile request, got " + requests.Length);

                    float3 expectedShotDirection = analog ? heading : look.DesiredDirection;
                    AssertDirection(requests[0].Direction, expectedShotDirection, context + ": shot diverged from the input mode's aim direction.");
                    AssertDirection(look.CurrentDirection, heading, context + ": look state lagged behind rotation.");

                    float3 offset = manager.GetComponentData<PlayerRuntimeShootingConfig>(player).ShootOffset;
                    float3 expectedOrigin = after.TransformPoint(mountLocal.TransformPoint(muzzleLocal.Position));
                    quaternion muzzleRotation = math.mul(after.Rotation, math.mul(mountLocal.Rotation, muzzleLocal.Rotation));
                    expectedOrigin += math.rotate(muzzleRotation, offset);
                    AssertPosition(requests[0].Position, expectedOrigin, context + ": shot used stale muzzle world data.");

                    if (analog && rotationMode == RotationMode.Continuous && mode != LookDirectionsMode.FollowMovementDirection && math.lengthsq(samples[frame]) <= 0.01f)
                    {
                        AssertDirection(look.DesiredDirection, heading, context + ": released aim retained an unreachable snapped target.");
                        AssertDirection(heading, math.forward(before.Rotation), context + ": released aim changed the held heading.");
                    }

                    if (mode == LookDirectionsMode.FollowMovementDirection && frame > 0)
                        AssertDirection(heading, new float3(1f, 0f, 0f), context + ": look did not consume this frame's movement direction.");

                    if (requests[0].InheritPlayerSpeed != 0)
                        throw new InvalidOperationException(context + ": unexpected inherited velocity enabled.");
                }
            }
        }
    }
    #endregion

    #region Fallback
    /// <summary>
    /// Verifies the current player pose is also respected without a muzzle, with authored velocity inheritance preserved.
    /// </summary>
    private static void ValidateMissingMuzzle()
    {
        using (World world = new World("PlayerControllerAimFallbackSmokeTest"))
        using (BlobBuilder builder = new BlobBuilder(Allocator.Temp))
        {
            builder.ConstructRoot<PlayerControllerConfigBlob>();
            using (BlobAssetReference<PlayerControllerConfigBlob> config = builder.CreateBlobAssetReference<PlayerControllerConfigBlob>(Allocator.Persistent))
            {
                EntityManager manager = world.EntityManager;
                Entity player = CreatePlayer(manager, config);
                PlayerRuntimeShootingConfig shooting = manager.GetComponentData<PlayerRuntimeShootingConfig>(player);
                shooting.ProjectilesInheritPlayerSpeed = 1;
                manager.SetComponentData(player, shooting);
                manager.SetComponentData(player, new PlayerInputState { Shoot = 1f, LookUsesAnalogSource = 1 });
                world.SetTime(new TimeData(1d, DeltaTime));
                SystemHandle system = world.GetOrCreateSystem<PlayerShootingIntentSystem>();
                system.Update(world.Unmanaged);

                DynamicBuffer<ShootRequest> requests = manager.GetBuffer<ShootRequest>(player);
                if (requests.Length != 1 || requests[0].InheritPlayerSpeed != 1)
                    throw new InvalidOperationException("Missing-muzzle shot or authored velocity inheritance was lost.");

                LocalTransform pose = manager.GetComponentData<LocalTransform>(player);
                AssertPosition(requests[0].Position, pose.Position + math.rotate(pose.Rotation, shooting.ShootOffset), "Missing muzzle did not fall back to the player world pose.");
                AssertDirection(requests[0].Direction, math.forward(pose.Rotation), "Missing muzzle changed the shot direction.");
            }
        }
    }
    #endregion

    #region Setup
    private static PlayerControllerSystemGroup CreateGroup(World world)
    {
        PlayerControllerSystemGroup group = world.GetOrCreateSystemManaged<PlayerControllerSystemGroup>();
        // Deliberately add consumers first so ordering attributes, not insertion order, determine the result.
        group.AddSystemToUpdateList(world.GetOrCreateSystem<PlayerShootingIntentSystem>());
        group.AddSystemToUpdateList(world.GetOrCreateSystem<PlayerLookRotationSystem>());
        group.AddSystemToUpdateList(world.GetOrCreateSystem<PlayerLookMultiplierSystem>());
        group.AddSystemToUpdateList(world.GetOrCreateSystem<PlayerLookDirectionSystem>());
        group.AddSystemToUpdateList(world.GetOrCreateSystem<PlayerMovementDirectionSystem>());
        // Input samples and post-movement positions are staged by the fixture, but keep their systems in the
        // group so the production dependency graph is sorted without missing-system warnings.
        AddDisabledSystem<PlayerInputBridgeSystem>(world, group);
        AddDisabledSystem<PlayerMovementSpeedSystem>(world, group);
        AddDisabledSystem<PlayerMovementApplySystem>(world, group);
        group.SortSystems();
        return group;
    }

    private static void AddDisabledSystem<T>(World world, PlayerControllerSystemGroup group) where T : unmanaged, ISystem
    {
        SystemHandle system = world.GetOrCreateSystem<T>();
        world.Unmanaged.ResolveSystemStateRef(system).Enabled = false;
        group.AddSystemToUpdateList(system);
    }

    private static Entity CreatePlayer(EntityManager manager, BlobAssetReference<PlayerControllerConfigBlob> config)
    {
        Entity player = manager.CreateEntity();
        LocalTransform pose = LocalTransform.FromPositionRotationScale(new float3(8f, 0f, -3f), quaternion.RotateY(math.radians(17f)), 1.2f);
        manager.AddComponentData(player, pose);
        manager.AddComponentData(player, new PlayerControllerConfig { Config = config });
        manager.AddComponentData(player, default(PlayerInputState));
        manager.AddComponentData(player, new PlayerLookState { CurrentDirection = math.forward(pose.Rotation), DesiredDirection = new float3(1f, 0f, 0f) });
        manager.AddComponentData(player, default(PlayerMovementState));
        manager.AddComponentData(player, default(PlayerMovementModifiers));
        manager.AddComponentData(player, default(PlayerRunOutcomeState));
        manager.AddComponentData(player, default(PlayerRuntimeLookConfig));
        manager.AddComponentData(player, new PlayerRuntimeMovementConfig { MovementReference = ReferenceFrame.WorldForward });
        manager.AddComponentData(player, new PlayerRuntimeShootingConfig
        {
            TriggerMode = ShootingTriggerMode.ManualContinousShot,
            ShootOffset = new float3(0.05f, 0.1f, 0.2f),
            Values = new ShootingValuesBlob { RateOfFire = 10f, ShootSpeed = 20f, ProjectileSizeMultiplier = 1f }
        });
        manager.AddComponentData(player, default(PlayerShootingState));
        manager.AddComponentData(player, default(PlayerPowerUpsState));
        manager.AddComponentData(player, default(PlayerLaserBeamState));
        manager.AddComponentData(player, default(ShooterProjectilePrefab));
        manager.AddBuffer<ShootRequest>(player);
        manager.AddBuffer<PlayerRuntimeShootingAppliedElementSlot>(player);
        manager.AddBuffer<EquippedPassiveToolElement>(player);
        manager.AddBuffer<PlayerPowerUpsConfigElement>(player);
        manager.AddBuffer<PlayerBombSpawnRequest>(player);
        manager.AddBuffer<PlayerPowerUpUnlockCatalogElement>(player);
        manager.AddBuffer<PlayerPowerUpCharacterTuningFormulaElement>(player);
        manager.AddBuffer<PlayerScalableStatElement>(player);
        manager.AddBuffer<PlayerRuntimeControllerScalingElement>(player);
        return player;
    }

    private static Entity CreateChild(EntityManager manager, Entity parent, LocalTransform pose)
    {
        Entity child = manager.CreateEntity();
        manager.AddComponentData(child, pose);
        manager.AddComponentData(child, new Parent { Value = parent });
        manager.AddComponentData(child, new LocalToWorld { Value = float4x4.Translate(new float3(-100f, 0f, 100f)) });
        return child;
    }
    #endregion

    #region Assertions
    private static void AssertDirection(float3 actual, float3 expected, string message)
    {
        AssertPosition(math.normalizesafe(actual), math.normalizesafe(expected), message);
    }

    private static void AssertPosition(float3 actual, float3 expected, string message)
    {
        if (!math.all(math.isfinite(actual)) || math.distance(actual, expected) > Tolerance)
            throw new InvalidOperationException(message + " Expected " + expected + ", got " + actual);
    }
    #endregion

    #endregion
}
