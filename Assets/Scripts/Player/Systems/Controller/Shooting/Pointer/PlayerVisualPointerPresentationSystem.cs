using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Renders the precision aiming laser pointer straight out of the player weapon for every entity that enabled it in its visual preset.
/// The pointer reuses the Laser Beam body material and palette, adapts its length to the projectile range, and freezes while orbital shots are active.
/// Direction is locked to the player's actual transform forward (same source the Laser Beam uses), so the pointer always matches the
/// visible player heading even under fast rotation or while the Laser Beam optional rotation-speed nerf is active.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct PlayerVisualPointerPresentationSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime data required to render the aiming pointer.
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerVisualPointerConfig>();
        state.RequireForUpdate<PlayerRuntimeShootingConfig>();
        state.RequireForUpdate<PlayerPassiveToolsStateElement>();
        state.RequireForUpdate<LocalTransform>();
    }

    /// <summary>
    /// Submits one steady beam draw per active pointer, recomputing origin, direction and length from the current shot state.
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();
        state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

        Mesh beamMesh = PlayerVisualPointerPresentationUtility.GetOrCreateBeamMesh();

        if (beamMesh == null)
            return;

        ComponentLookup<ShooterMuzzleAnchor> muzzleLookup = SystemAPI.GetComponentLookup<ShooterMuzzleAnchor>(true);
        ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<LocalToWorld> localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);

        foreach ((RefRO<PlayerVisualPointerConfig> pointerConfig,
                  RefRO<PlayerRuntimeShootingConfig> runtimeShootingConfig,
                  RefRO<LocalTransform> localTransform,
                  DynamicBuffer<PlayerPassiveToolsStateElement> passiveToolsStateBuffer,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRO<PlayerVisualPointerConfig>,
                                    RefRO<PlayerRuntimeShootingConfig>,
                                    RefRO<LocalTransform>,
                                    DynamicBuffer<PlayerPassiveToolsStateElement>>()
                             .WithEntityAccess())
        {
            PlayerVisualPointerConfig config = pointerConfig.ValueRO;

            if (config.Enabled == 0)
                continue;

            Material bodyMaterial = config.BodyMaterial.Value;

            if (bodyMaterial == null)
                continue;

            PlayerPassiveToolsState passiveToolsState;
            PlayerPassiveToolsStateBufferUtility.Read(passiveToolsStateBuffer, out passiveToolsState);
            float length = PlayerVisualPointerPresentationUtility.ResolveLength(in config,
                                                                               in passiveToolsState,
                                                                               in runtimeShootingConfig.ValueRO.Values);

            if (length <= PlayerLaserBeamUtility.MinimumTravelDistance)
                continue;

            // Use the same forward resolution the Laser Beam uses so the aiming pointer is always glued to the visible
            // player heading and never lags behind under damped or laser-nerfed rotation.
            float3 direction = PlayerLaserBeamUtility.ResolveCurrentForwardDirection(in localTransform.ValueRO);
            float3 origin = PlayerVisualPointerPresentationUtility.ResolveOrigin(playerEntity,
                                                                                 in localTransform.ValueRO,
                                                                                 in runtimeShootingConfig.ValueRO,
                                                                                 in muzzleLookup,
                                                                                 in transformLookup,
                                                                                 in localToWorldLookup,
                                                                                 config.VerticalLift);
            quaternion rotation = quaternion.LookRotationSafe(direction, math.up());
            RenderPointer(beamMesh, bodyMaterial, in config, origin, direction, rotation, length);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Submits one pointer beam draw for the current frame using the shared mesh and a steady-body property block.
    /// </summary>
    /// <param name="beamMesh">Shared unit-length beam mesh.</param>
    /// <param name="bodyMaterial">Reused Laser Beam body material.</param>
    /// <param name="config">Baked pointer config.</param>
    /// <param name="origin">World-space pointer origin at the muzzle.</param>
    /// <param name="direction">Planar shot direction the pointer follows.</param>
    /// <param name="rotation">World rotation aligning the mesh forward axis with the shot direction.</param>
    /// <param name="length">Final rendered pointer length.</param>
    private static void RenderPointer(Mesh beamMesh,
                                      Material bodyMaterial,
                                      in PlayerVisualPointerConfig config,
                                      float3 origin,
                                      float3 direction,
                                      quaternion rotation,
                                      float length)
    {
        Matrix4x4 objectToWorld = Matrix4x4.TRS(new Vector3(origin.x, origin.y, origin.z),
                                                rotation,
                                                new Vector3(config.Width, config.Width, length));
        float3 boundsCenter = origin + direction * (length * 0.5f);
        RenderParams renderParams = new RenderParams(bodyMaterial)
        {
            worldBounds = new Bounds(new Vector3(boundsCenter.x, boundsCenter.y, boundsCenter.z),
                                     Vector3.one * (length + 2f)),
            matProps = PlayerVisualPointerPresentationUtility.BuildPropertyBlock(in config, length),
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false
        };
        Graphics.RenderMesh(in renderParams, beamMesh, 0, objectToWorld);
    }
    #endregion

    #endregion
}
