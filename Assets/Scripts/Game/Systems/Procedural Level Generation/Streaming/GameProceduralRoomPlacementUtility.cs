using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Scenes;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Moves exact managed and DOTS room roots between authored active coordinates and isolated staging slots.
/// </summary>
internal static class GameProceduralRoomPlacementUtility
{
    #region Constants
    private const float StagingBaseHeight = -100000f;
    private const float StagingNodeStride = 1000f;
    #endregion

    #region Methods

    #region Capture
    /// <summary>
    /// Captures managed root coordinates and moves the room to a unique off-world staging slot.
    /// </summary>
    /// <param name="instance">Logical room instance being staged.</param>
    public static void StageManagedRoots(GameProceduralRoomStreamInstance instance)
    {
        instance.ManagedRootPoses.Clear();

        if (!instance.UsesSpatialStaging)
            return;

        GameObject[] roots = instance.ManagedScene.GetRootGameObjects();
        Vector3 offset = ResolveStagingOffset(instance.StagingSlotIndex);

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Transform root = roots[rootIndex].transform;
            instance.ManagedRootPoses.Add(new GameProceduralRoomManagedRootPose(root, root.position));
            root.position += offset;
        }

    }

    /// <summary>
    /// Captures and stages only root entities filtered through exact SceneTag section handles.
    /// </summary>
    /// <param name="entityManager">Entity manager owning streamed room entities.</param>
    /// <param name="instance">Logical room instance being staged.</param>
    public static void CaptureAndStageEntityRoots(EntityManager entityManager,
                                                  GameProceduralRoomStreamInstance instance)
    {
        instance.EntityRootPoses.Clear();

        if (!instance.UsesSpatialStaging)
            return;

        EntityQuery query = entityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<SceneTag>(),
                ComponentType.ReadWrite<LocalTransform>()
            },
            None = new ComponentType[]
            {
                ComponentType.ReadOnly<Parent>()
            },
            Options = EntityQueryOptions.IncludeDisabledEntities
        });
        float3 offset = ResolveStagingOffset(instance.StagingSlotIndex);

        try
        {
            for (int sectionIndex = 0; sectionIndex < instance.SectionEntities.Count; sectionIndex++)
            {
                query.SetSharedComponentFilter(new SceneTag
                {
                    SceneEntity = instance.SectionEntities[sectionIndex]
                });
                using NativeArray<Entity> roots = query.ToEntityArray(Allocator.Temp);

                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    Entity root = roots[rootIndex];
                    LocalTransform transform = entityManager.GetComponentData<LocalTransform>(root);
                    instance.EntityRootPoses.Add(new GameProceduralRoomEntityRootPose(root, transform.Position));
                    transform.Position += offset;
                    entityManager.SetComponentData(root, transform);
                }
            }
        }
        finally
        {
            query.ResetFilter();
            query.Dispose();
        }
    }
    #endregion

    #region Apply
    /// <summary>
    /// Restores authored room coordinates or applies the node-specific staging offset without scene traversal.
    /// </summary>
    /// <param name="entityManager">Entity manager owning exact room root transforms.</param>
    /// <param name="instance">Logical room instance being placed.</param>
    /// <param name="active">True to restore active-space coordinates; false to isolate the room off-world.</param>
    public static void ApplyPlacement(EntityManager entityManager,
                                      GameProceduralRoomStreamInstance instance,
                                      bool active)
    {
        ApplyPlacement(entityManager,
                       instance,
                       active,
                       instance.ActivePlacementOffset);
    }

    /// <summary>
    /// Promotes one staged room at a world offset that aligns its target arrival with the unchanged persistent player pose.
    /// </summary>
    /// <param name="entityManager">Entity manager owning exact room sections and spatial metadata.</param>
    /// <param name="instance">Logical room instance becoming active.</param>
    /// <param name="activeOffset">World translation applied to authored room coordinates.</param>
    public static void ApplyActivePlacement(EntityManager entityManager,
                                            GameProceduralRoomStreamInstance instance,
                                            float3 activeOffset)
    {
        ApplyPlacement(entityManager, instance, true, activeOffset);
    }

    /// <summary>
    /// Applies either the isolated staging slot or one active world offset to every exact room spatial surface.
    /// </summary>
    /// <param name="entityManager">Entity manager owning exact room sections.</param>
    /// <param name="instance">Logical room instance being placed.</param>
    /// <param name="active">True to use active world placement; false to isolate the room in its staging slot.</param>
    /// <param name="activeOffset">World translation used only for active placement.</param>
    private static void ApplyPlacement(EntityManager entityManager,
                                       GameProceduralRoomStreamInstance instance,
                                       bool active,
                                       float3 activeOffset)
    {
        if (!instance.UsesSpatialStaging)
        {
            instance.ActivePlacementOffset = float3.zero;
            return;
        }

        if (active)
            ApplySpatialMetadataOffset(entityManager, instance, activeOffset);

        float3 resolvedOffset = active
            ? activeOffset
            : (float3)ResolveStagingOffset(instance.StagingSlotIndex);
        Vector3 managedOffset = resolvedOffset;
        float3 entityOffset = resolvedOffset;

        for (int rootIndex = 0; rootIndex < instance.ManagedRootPoses.Count; rootIndex++)
        {
            GameProceduralRoomManagedRootPose pose = instance.ManagedRootPoses[rootIndex];

            if (pose.Root != null)
                pose.Root.position = pose.ActivePosition + managedOffset;
        }

        for (int rootIndex = 0; rootIndex < instance.EntityRootPoses.Count; rootIndex++)
        {
            GameProceduralRoomEntityRootPose pose = instance.EntityRootPoses[rootIndex];

            if (!entityManager.Exists(pose.Root) || !entityManager.HasComponent<LocalTransform>(pose.Root))
                continue;

            LocalTransform transform = entityManager.GetComponentData<LocalTransform>(pose.Root);
            transform.Position = pose.ActivePosition + entityOffset;
            entityManager.SetComponentData(pose.Root, transform);
        }
    }

    /// <summary>
    /// Translates baked portal and center-anchor world data by the delta from the instance's previous active placement.
    /// </summary>
    /// <param name="entityManager">Entity manager owning exact room component data.</param>
    /// <param name="instance">Logical room instance whose world metadata is translated.</param>
    /// <param name="activeOffset">New authoritative active-world translation.</param>
    private static void ApplySpatialMetadataOffset(EntityManager entityManager,
                                                   GameProceduralRoomStreamInstance instance,
                                                   float3 activeOffset)
    {
        float3 offsetDelta = activeOffset - instance.ActivePlacementOffset;

        if (math.lengthsq(offsetDelta) <= 0.000001f)
            return;

        TranslatePortals(entityManager, instance, offsetDelta);
        TranslateCenterAnchors(entityManager, instance, offsetDelta);
        instance.ActivePlacementOffset = activeOffset;
    }

    /// <summary>
    /// Translates portal volumes and arrival positions for one exact room instance.
    /// </summary>
    /// <param name="entityManager">Entity manager owning portal data.</param>
    /// <param name="instance">Exact room instance being translated.</param>
    /// <param name="offsetDelta">World-space translation since its previous active placement.</param>
    private static void TranslatePortals(EntityManager entityManager,
                                         GameProceduralRoomStreamInstance instance,
                                         float3 offsetDelta)
    {
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<GameRoomPortal>(),
                                                            ComponentType.ReadOnly<SceneTag>());
        NativeList<Entity> portals = new NativeList<Entity>(Allocator.Temp);

        try
        {
            GameProceduralRoomInstanceQueryUtility.CollectRoomInstanceEntities(instance, query, ref portals);

            for (int portalIndex = 0; portalIndex < portals.Length; portalIndex++)
            {
                GameRoomPortal portal = entityManager.GetComponentData<GameRoomPortal>(portals[portalIndex]);
                portal.Center += offsetDelta;
                portal.ArrivalPosition += offsetDelta;
                entityManager.SetComponentData(portals[portalIndex], portal);
            }
        }
        finally
        {
            portals.Dispose();
            query.Dispose();
        }
    }

    /// <summary>
    /// Translates fallback center-arrival data for one exact room instance.
    /// </summary>
    /// <param name="entityManager">Entity manager owning center-anchor data.</param>
    /// <param name="instance">Exact room instance being translated.</param>
    /// <param name="offsetDelta">World-space translation since its previous active placement.</param>
    private static void TranslateCenterAnchors(EntityManager entityManager,
                                               GameProceduralRoomStreamInstance instance,
                                               float3 offsetDelta)
    {
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<GameRoomCenterAnchor>(),
                                                            ComponentType.ReadOnly<SceneTag>());
        NativeList<Entity> anchors = new NativeList<Entity>(Allocator.Temp);

        try
        {
            GameProceduralRoomInstanceQueryUtility.CollectRoomInstanceEntities(instance, query, ref anchors);

            for (int anchorIndex = 0; anchorIndex < anchors.Length; anchorIndex++)
            {
                GameRoomCenterAnchor anchor = entityManager.GetComponentData<GameRoomCenterAnchor>(anchors[anchorIndex]);
                anchor.Position += offsetDelta;
                entityManager.SetComponentData(anchors[anchorIndex], anchor);
            }
        }
        finally
        {
            anchors.Dispose();
            query.Dispose();
        }
    }

    /// <summary>
    /// Resolves a deterministic off-world slot that prevents staged node instances from overlapping each other.
    /// </summary>
    /// <param name="stagingSlotIndex">Unique exact-instance staging slot index.</param>
    /// <returns>World-space staging offset for managed and DOTS roots.</returns>
    private static Vector3 ResolveStagingOffset(int stagingSlotIndex)
    {
        return new Vector3(0f, StagingBaseHeight - math.max(0, stagingSlotIndex) * StagingNodeStride, 0f);
    }
    #endregion

    #endregion
}
