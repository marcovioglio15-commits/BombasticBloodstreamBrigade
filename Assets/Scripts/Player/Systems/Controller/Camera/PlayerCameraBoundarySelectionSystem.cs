using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Selects one active Camera Boundary from the local player position and publishes immutable state for camera writers.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateBefore(typeof(PlayerCameraFollowSystem))]
public partial struct PlayerCameraBoundarySelectionSystem : ISystem
{
    #region Fields
    private Entity runtimeStateEntity;
    #endregion

    #region Methods

    #region Lifecycle Methods
    /// <summary>
    /// Creates the boundary-selection singleton and requires a camera target while still allowing the final boundary removal to clear state.
    /// </summary>
    /// <param name="state">System state used to create and configure the runtime singleton.</param>
    public void OnCreate(ref SystemState state)
    {
        runtimeStateEntity = state.EntityManager.CreateEntity(typeof(GameCameraBoundaryRuntimeState));
        state.RequireForUpdate<PlayerRuntimeCameraConfig>();
    }

    /// <summary>
    /// Resolves the highest-priority containing boundary without allocations and publishes changes only when needed.
    /// </summary>
    /// <param name="state">System state providing player transforms, boundaries and Scene Manager settings.</param>
    public void OnUpdate(ref SystemState state)
    {
        ResolveSettings(out bool boundariesEnabled,
                        out GameCameraBoundaryMode boundaryMode,
                        out float softZoneDistance);

        if (!boundariesEnabled)
        {
            PublishInactive(state.EntityManager, false, boundaryMode, softZoneDistance);
            return;
        }

        if (boundaryMode == GameCameraBoundaryMode.ImpassableVolume)
        {
            PublishInactive(state.EntityManager, true, boundaryMode, softZoneDistance);
            return;
        }

        state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();
        ComponentLookup<LocalToWorld> localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        float3 focusPosition = default;
        bool hasFocusPosition = false;
        bool hasFastPlayPlayer = SystemAPI.TryGetSingletonEntity<GameCameraBoundaryFastPlayPlayer>(
            out Entity fastPlayPlayerEntity);

        // Resolve the Fast Play target when present; regular gameplay still consumes the first local camera target.
        foreach ((RefRO<LocalTransform> localTransform, Entity entity) in
                 SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerRuntimeCameraConfig>().WithEntityAccess())
        {
            if (hasFastPlayPlayer && entity != fastPlayPlayerEntity)
                continue;

            focusPosition = localToWorldLookup.HasComponent(entity)
                ? localToWorldLookup[entity].Position
                : localTransform.ValueRO.Position;
            hasFocusPosition = true;
            break;
        }

        if (!hasFocusPosition)
        {
            PublishInactive(state.EntityManager, true, boundaryMode, softZoneDistance);
            return;
        }

        Entity selectedEntity = Entity.Null;
        GameCameraBoundary selectedBoundary = default;
        float selectedArea = float.MaxValue;

        // Select the highest priority footprint containing the player; smaller volumes break equal-priority ties.
        foreach ((RefRO<GameCameraBoundary> boundaryReference, Entity entity) in
                 SystemAPI.Query<RefRO<GameCameraBoundary>>().WithEntityAccess())
        {
            GameCameraBoundary boundary = boundaryReference.ValueRO;

            if (!GameCameraBoundaryUtility.Contains(in boundary, focusPosition))
                continue;

            float area = GameCameraBoundaryUtility.CalculatePlanarArea(in boundary);
            bool replacesSelection = selectedEntity == Entity.Null ||
                                     boundary.Priority > selectedBoundary.Priority ||
                                     boundary.Priority == selectedBoundary.Priority && area < selectedArea;

            if (!replacesSelection)
                continue;

            selectedEntity = entity;
            selectedBoundary = boundary;
            selectedArea = area;
        }

        if (selectedEntity == Entity.Null)
        {
            GameCameraBoundaryRuntimeState currentState =
                state.EntityManager.GetComponentData<GameCameraBoundaryRuntimeState>(runtimeStateEntity);

            // Retain the last valid volume while crossing its edge so the camera cannot escape through an uncovered gap.
            if (currentState.HasBoundary == 0 ||
                !state.EntityManager.Exists(currentState.BoundaryEntity) ||
                !state.EntityManager.HasComponent<GameCameraBoundary>(currentState.BoundaryEntity))
            {
                PublishInactive(state.EntityManager, true, boundaryMode, softZoneDistance);
                return;
            }

            selectedEntity = currentState.BoundaryEntity;
            selectedBoundary = state.EntityManager.GetComponentData<GameCameraBoundary>(selectedEntity);
        }

        PublishSelection(state.EntityManager,
                         selectedEntity,
                         in selectedBoundary,
                         boundaryMode,
                         softZoneDistance);
    }

    /// <summary>
    /// Destroys the runtime singleton during explicit world teardown.
    /// </summary>
    /// <param name="state">System state owning the selection singleton.</param>
    public void OnDestroy(ref SystemState state)
    {
        if (runtimeStateEntity != Entity.Null && state.EntityManager.Exists(runtimeStateEntity))
            state.EntityManager.DestroyEntity(runtimeStateEntity);
    }
    #endregion

    #region Resolution Methods
    /// <summary>
    /// Resolves enabled state and defensive runtime braking distance from the Scene Manager singleton or defaults.
    /// </summary>
    /// <param name="boundariesEnabled">True when boundary selection and constraints should run.</param>
    /// <param name="boundaryMode">Containment or impassable-volume runtime policy.</param>
    /// <param name="softZoneDistance">Non-negative braking distance used by camera writers.</param>
    private void ResolveSettings(out bool boundariesEnabled,
                                 out GameCameraBoundaryMode boundaryMode,
                                 out float softZoneDistance)
    {
        boundariesEnabled = true;
        boundaryMode = GameCameraBoundaryMode.ContainmentVolume;
        softZoneDistance = GameCameraBoundaryDefaults.SoftZoneDistance;

        if (!SystemAPI.TryGetSingleton(out GameSceneManagerConfig sceneManagerConfig))
        {
            if (!SystemAPI.TryGetSingleton(out GameCameraBoundaryFastPlaySettings fastPlaySettings))
                return;

            boundariesEnabled = fastPlaySettings.EnableCameraBoundaries != 0;
            boundaryMode = fastPlaySettings.Mode;
            softZoneDistance = math.max(0f, fastPlaySettings.SoftZoneDistance);
            return;
        }

        boundariesEnabled = sceneManagerConfig.EnableCameraBoundaries != 0;
        boundaryMode = sceneManagerConfig.CameraBoundaryMode;
        softZoneDistance = math.max(0f, sceneManagerConfig.CameraBoundarySoftZoneDistance);
    }
    #endregion

    #region Publication Methods
    /// <summary>
    /// Clears containment selection while preserving current enable, mode, and soft-zone settings.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the selection singleton.</param>
    /// <param name="enabled">True when any boundary policy remains active.</param>
    /// <param name="boundaryMode">Current containment or impassable-volume policy.</param>
    /// <param name="softZoneDistance">Resolved braking distance retained for diagnostics.</param>
    private void PublishInactive(EntityManager entityManager,
                                 bool enabled,
                                 GameCameraBoundaryMode boundaryMode,
                                 float softZoneDistance)
    {
        GameCameraBoundaryRuntimeState currentState = entityManager.GetComponentData<GameCameraBoundaryRuntimeState>(runtimeStateEntity);

        if (currentState.HasBoundary == 0 &&
            currentState.Enabled == (enabled ? (byte)1 : (byte)0) &&
            currentState.Mode == boundaryMode &&
            math.abs(currentState.SoftZoneDistance - softZoneDistance) <= 0.0001f)
        {
            return;
        }

        entityManager.SetComponentData(runtimeStateEntity, new GameCameraBoundaryRuntimeState
        {
            BoundaryEntity = Entity.Null,
            Boundary = default,
            SoftZoneDistance = softZoneDistance,
            Mode = boundaryMode,
            Enabled = enabled ? (byte)1 : (byte)0,
            HasBoundary = 0
        });
    }

    /// <summary>
    /// Publishes an active selection only when its entity, geometry or braking setting changed.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the selection singleton.</param>
    /// <param name="selectedEntity">Selected boundary entity.</param>
    /// <param name="selectedBoundary">Selected immutable boundary geometry.</param>
    /// <param name="boundaryMode">Current containment policy.</param>
    /// <param name="softZoneDistance">Resolved braking distance.</param>
    private void PublishSelection(EntityManager entityManager,
                                  Entity selectedEntity,
                                  in GameCameraBoundary selectedBoundary,
                                  GameCameraBoundaryMode boundaryMode,
                                  float softZoneDistance)
    {
        GameCameraBoundaryRuntimeState currentState = entityManager.GetComponentData<GameCameraBoundaryRuntimeState>(runtimeStateEntity);
        bool unchanged = currentState.HasBoundary != 0 &&
                         currentState.Enabled != 0 &&
                         currentState.Mode == boundaryMode &&
                         currentState.BoundaryEntity == selectedEntity &&
                         math.abs(currentState.SoftZoneDistance - softZoneDistance) <= 0.0001f &&
                         GameCameraBoundaryUtility.ApproximatelyEquals(in currentState.Boundary, in selectedBoundary);

        if (unchanged)
            return;

        entityManager.SetComponentData(runtimeStateEntity, new GameCameraBoundaryRuntimeState
        {
            BoundaryEntity = selectedEntity,
            Boundary = selectedBoundary,
            SoftZoneDistance = softZoneDistance,
            Mode = boundaryMode,
            Enabled = 1,
            HasBoundary = 1
        });
    }
    #endregion

    #endregion
}
