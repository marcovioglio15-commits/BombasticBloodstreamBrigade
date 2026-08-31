using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Creates one transient ECS movement target from the real player presets while isolating normal gameplay simulation.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameCameraBoundaryFastPlayRig : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Preset Sources")]
    [Tooltip("Player master preset resolved from the project player prefab for movement and camera tuning.")]
    [SerializeField]
    private PlayerMasterPreset playerMasterPreset;

    [Tooltip("Scene Manager preset supplying the Camera Boundary enable toggle, mode, and soft-zone distance.")]
    [SerializeField]
    private GameSceneManagerPreset sceneManagerPreset;

    [Tooltip("Movement speed resolved through the unified Add Scaling formulas before Fast Play starts.")]
    [SerializeField]
    private float movementSpeed;

    [Header("Transient Scene References")]
    [Tooltip("Transient instance of the project player prefab synchronized from the authoritative ECS test entity.")]
    [SerializeField]
    private Transform playerVisualRoot;

    [Tooltip("Transient clone of the persistent gameplay camera rig from the configured bootstrap scene.")]
    [SerializeField]
    private GameObject gameplayCameraRig;

    [Tooltip("World-space player spawn position resolved from the selected Camera Boundary volume.")]
    [SerializeField]
    private Vector3 spawnPosition;
    #endregion

    #region Runtime Fields
    private Entity runtimeEntity = Entity.Null;
    private World runtimeWorld;
    private SimulationSystemGroup simulationSystemGroup;
    private bool simulationGroupWasEnabled;
    private bool initialized;
    #endregion

    #endregion

    #region Methods

    #region Configuration Methods
    /// <summary>
    /// Assigns transient scene references and real project presets before entering Play Mode.
    /// </summary>
    /// <param name="resolvedPlayerMasterPreset">Player master preset resolved from the project player prefab.</param>
    /// <param name="resolvedSceneManagerPreset">Scene Manager preset supplying boundary settings.</param>
    /// <param name="resolvedPlayerVisualRoot">Transient project player prefab instance.</param>
    /// <param name="resolvedGameplayCameraRig">Transient persistent gameplay camera clone.</param>
    /// <param name="resolvedSpawnPosition">World-space position used to create the ECS target.</param>
    /// <param name="resolvedMovementSpeed">Formula-scaled movement speed used by the focused test target.</param>
    public void Configure(PlayerMasterPreset resolvedPlayerMasterPreset,
                          GameSceneManagerPreset resolvedSceneManagerPreset,
                          Transform resolvedPlayerVisualRoot,
                          GameObject resolvedGameplayCameraRig,
                          Vector3 resolvedSpawnPosition,
                          float resolvedMovementSpeed)
    {
        playerMasterPreset = resolvedPlayerMasterPreset;
        sceneManagerPreset = resolvedSceneManagerPreset;
        playerVisualRoot = resolvedPlayerVisualRoot;
        gameplayCameraRig = resolvedGameplayCameraRig;
        spawnPosition = resolvedSpawnPosition;
        movementSpeed = resolvedMovementSpeed;
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Attempts initialization when the Play Mode scene instance becomes active.
    /// </summary>
    private void OnEnable()
    {
        TryInitialize();
    }

    /// <summary>
    /// Retries after the default ECS world and its root groups have completed startup.
    /// </summary>
    private void Start()
    {
        TryInitialize();
    }

    /// <summary>
    /// Synchronizes the real player prefab presentation from the authoritative ECS test transform.
    /// </summary>
    private void LateUpdate()
    {
        if (!initialized || runtimeWorld == null || !runtimeWorld.IsCreated ||
            runtimeEntity == Entity.Null || !runtimeWorld.EntityManager.Exists(runtimeEntity) ||
            playerVisualRoot == null)
        {
            return;
        }

        EntityManager entityManager = runtimeWorld.EntityManager;
        entityManager.CompleteDependencyBeforeRO<LocalTransform>();
        LocalTransform localTransform = entityManager.GetComponentData<LocalTransform>(runtimeEntity);
        playerVisualRoot.SetPositionAndRotation(localTransform.Position, localTransform.Rotation);
    }

    /// <summary>
    /// Removes transient ECS state and restores the simulation root if the rig is disabled before world teardown.
    /// </summary>
    private void OnDisable()
    {
        World simulationWorld = simulationSystemGroup != null
            ? simulationSystemGroup.World
            : null;

        // A managed system wrapper can outlive its native state during world teardown.
        if (simulationWorld != null && simulationWorld.IsCreated)
            simulationSystemGroup.Enabled = simulationGroupWasEnabled;

        if (runtimeWorld != null && runtimeWorld.IsCreated &&
            runtimeEntity != Entity.Null && runtimeWorld.EntityManager.Exists(runtimeEntity))
        {
            runtimeWorld.EntityManager.DestroyEntity(runtimeEntity);
        }

        runtimeEntity = Entity.Null;
        runtimeWorld = null;
        simulationSystemGroup = null;
        initialized = false;
    }
    #endregion

    #region Initialization Methods
    /// <summary>
    /// Enables the cloned gameplay camera, pauses normal simulation and creates the minimal preset-derived ECS target.
    /// </summary>
    private void TryInitialize()
    {
        if (!Application.isPlaying || initialized)
            return;

        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
            return;

        PlayerControllerPreset controllerPreset = playerMasterPreset != null
            ? playerMasterPreset.ControllerPreset
            : null;

        if (controllerPreset == null || controllerPreset.CameraSettings == null ||
            controllerPreset.CameraSettings.Values == null ||
            movementSpeed <= 0f)
        {
            Debug.LogError("[GameCameraBoundaryFastPlayRig] The project player prefab has no valid scaled movement or camera configuration.",
                           this);
            return;
        }

        // Prevent combat, enemies, progression and other Simulation systems from advancing during the focused test.
        simulationSystemGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();

        if (simulationSystemGroup != null)
        {
            simulationGroupWasEnabled = simulationSystemGroup.Enabled;
            simulationSystemGroup.Enabled = false;
        }

        // Give the cloned persistent gameplay rig exclusive ownership before it registers Camera.main.
        DisableExternalCameras();

        if (gameplayCameraRig != null)
            gameplayCameraRig.SetActive(true);

        runtimeWorld = world;
        runtimeEntity = world.EntityManager.CreateEntity(typeof(LocalTransform),
                                                          typeof(PlayerRuntimeCameraConfig),
                                                          typeof(GameCameraBoundaryFastPlayPlayer),
                                                          typeof(GameCameraBoundaryFastPlaySettings));
        world.EntityManager.SetComponentData(runtimeEntity,
                                             LocalTransform.FromPositionRotationScale(spawnPosition,
                                                                                      quaternion.identity,
                                                                                      1f));
        world.EntityManager.SetComponentData(runtimeEntity, BuildCameraConfig(controllerPreset.CameraSettings));
        world.EntityManager.SetComponentData(runtimeEntity, new GameCameraBoundaryFastPlayPlayer
        {
            MoveSpeed = movementSpeed
        });
        world.EntityManager.SetComponentData(runtimeEntity, new GameCameraBoundaryFastPlaySettings
        {
            SoftZoneDistance = sceneManagerPreset != null &&
                               math.isfinite(sceneManagerPreset.CameraBoundarySoftZoneDistance) &&
                               sceneManagerPreset.CameraBoundarySoftZoneDistance >= 0f
                ? sceneManagerPreset.CameraBoundarySoftZoneDistance
                : GameCameraBoundaryDefaults.SoftZoneDistance,
            Mode = sceneManagerPreset != null
                ? sceneManagerPreset.CameraBoundaryMode
                : GameCameraBoundaryMode.ContainmentVolume,
            EnableCameraBoundaries = sceneManagerPreset == null || sceneManagerPreset.EnableCameraBoundaries
                ? (byte)1
                : (byte)0
        });
        initialized = true;
    }

    /// <summary>
    /// Builds the Fast Play camera component from the actual controller preset and replaces Room Fixed with follow mode.
    /// </summary>
    /// <param name="cameraSettings">Actual player camera settings used by normal gameplay.</param>
    /// <returns>Minimal runtime camera configuration consumed by the production follow system.</returns>
    private static PlayerRuntimeCameraConfig BuildCameraConfig(CameraSettings cameraSettings)
    {
        CameraBehavior behavior = cameraSettings.Behavior;

        // A boundary test must follow the moving target even when the gameplay preset normally selects a room anchor.
        switch (behavior)
        {
            case CameraBehavior.RoomFixed:
                behavior = CameraBehavior.FollowWithOffset;
                break;
        }

        return new PlayerRuntimeCameraConfig
        {
            Behavior = behavior,
            FollowOffset = new float3(cameraSettings.FollowOffset.x,
                                      cameraSettings.FollowOffset.y,
                                      cameraSettings.FollowOffset.z),
            Values = new CameraValuesBlob
            {
                SmoothTime = math.max(0f, cameraSettings.Values.SmoothTime),
                MaxFollowDistance = math.max(0f, cameraSettings.Values.MaxFollowDistance),
                DeadZoneRadius = math.max(0f, cameraSettings.Values.DeadZoneRadius)
            },
            Shake = default,
            FireShake = default
        };
    }

    /// <summary>
    /// Disables other active scene cameras and listeners in the Play Mode copy so only the real gameplay rig renders.
    /// </summary>
    private void DisableExternalCameras()
    {
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude,
                                                            FindObjectsSortMode.None);

        // The cloned rig is inactive at this point, so every active camera belongs to the tested scene.
        for (int cameraIndex = 0; cameraIndex < cameras.Length; cameraIndex++)
            cameras[cameraIndex].enabled = false;

        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude,
                                                                            FindObjectsSortMode.None);

        for (int listenerIndex = 0; listenerIndex < listeners.Length; listenerIndex++)
            listeners[listenerIndex].enabled = false;
    }
    #endregion

    #endregion
}

/// <summary>
/// Moves the transient Fast Play player before boundary selection while the normal Simulation group remains paused.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateBefore(typeof(PlayerCameraBoundarySelectionSystem))]
public partial struct GameCameraBoundaryFastPlayMovementSystem : ISystem
{
    #region Methods

    #region Lifecycle Methods
    /// <summary>
    /// Keeps the system dormant unless a Camera Boundary Fast Play target exists.
    /// </summary>
    /// <param name="state">System state used to register the transient player requirement.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameCameraBoundaryFastPlayPlayer>();
    }

    /// <summary>
    /// Reads keyboard or gamepad movement and advances the transient ECS target on the world XZ plane.
    /// </summary>
    /// <param name="state">System state providing presentation delta time and writable target transforms.</param>
    public void OnUpdate(ref SystemState state)
    {
        float2 movementInput = ReadMovementInput();

        if (math.lengthsq(movementInput) <= 0.0001f)
            return;

        movementInput = math.normalizesafe(movementInput);
        float deltaTime = UnityEngine.Time.unscaledDeltaTime;

        // Fast Play normally owns one target, but the query remains deterministic if duplicate rigs are inspected.
        foreach ((RefRW<LocalTransform> localTransform, RefRO<GameCameraBoundaryFastPlayPlayer> fastPlayPlayer) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRO<GameCameraBoundaryFastPlayPlayer>>())
        {
            localTransform.ValueRW.Position += new float3(movementInput.x, 0f, movementInput.y) *
                                               fastPlayPlayer.ValueRO.MoveSpeed * deltaTime;
        }
    }
    #endregion

    #region Input Methods
    /// <summary>
    /// Resolves WASD, arrow keys and the strongest available left-stick input without initializing gameplay actions.
    /// </summary>
    /// <returns>Unnormalized planar input used by the transient movement target.</returns>
    private static float2 ReadMovementInput()
    {
        float2 movementInput = float2.zero;
        Keyboard keyboard = Keyboard.current;

        // Keyboard input stays available even when the normal InputAuthoring hierarchy is intentionally absent.
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                movementInput.x -= 1f;

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                movementInput.x += 1f;

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                movementInput.y -= 1f;

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                movementInput.y += 1f;
        }

        Gamepad gamepad = Gamepad.current;

        // Prefer the stronger source when keyboard and gamepad input are both active.
        if (gamepad != null)
        {
            Vector2 stickInput = gamepad.leftStick.ReadValue();
            float2 gamepadInput = new float2(stickInput.x, stickInput.y);

            if (math.lengthsq(gamepadInput) > math.lengthsq(movementInput))
                movementInput = gamepadInput;
        }

        return movementInput;
    }
    #endregion

    #endregion
}
