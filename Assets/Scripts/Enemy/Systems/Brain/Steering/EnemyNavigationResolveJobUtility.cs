using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

/// <summary>
/// Contains the parallel navigation resolver used by the enemy steering pipeline.
/// </summary>
internal static class EnemyNavigationResolveJobUtility
{
    #region Jobs
    /// <summary>
    /// Resolves wall-aware flow-field velocity and direct-path blockage for each evaluated enemy.
    /// </summary>
    [BurstCompile]
    internal struct ResolveJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> EvaluatedEnemyIndices;
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float2> SpeedData;
        [ReadOnly] public NativeArray<float> BodyRadii;
        [ReadOnly] public NativeArray<EnemyData> EnemyDataArray;
        [ReadOnly] public NativeArray<EnemyNavigationCellElement> NavigationCells;
        [ReadOnly] public PhysicsWorldSingleton PhysicsWorld;
        public EnemyNavigationGridState NavigationGridState;
        public float3 PlayerPosition;
        public int WallsLayerMask;
        public NativeArray<float3> NavigationVelocityResults;
        public NativeArray<byte> NavigationDetourResults;

        #region Methods
        /// <summary>
        /// Resolves one compact enemy entry without accessing another job iteration's output.
        /// </summary>
        /// <param name="evaluatedIndex">Index inside the compact evaluation arrays.</param>
        public void Execute(int evaluatedIndex)
        {
            int enemyIndex = EvaluatedEnemyIndices[evaluatedIndex];
            float navigationDesiredSpeed = SpeedData[enemyIndex].y > 0f
                ? SpeedData[enemyIndex].y
                : SpeedData[enemyIndex].x;
            float navigationCollisionRadius = math.max(
                0.01f,
                BodyRadii[enemyIndex] + EnemyDataArray[enemyIndex].MinimumWallDistance);

            // Preserve zero-initialized output when no valid grid route can be resolved.
            if (!EnemyNavigationFlowFieldUtility.TryResolveNavigationVelocity(
                    Positions[enemyIndex],
                    PlayerPosition,
                    navigationCollisionRadius,
                    navigationDesiredSpeed,
                    in PhysicsWorld,
                    WallsLayerMask,
                    in NavigationGridState,
                    NavigationCells,
                    out float3 navigationVelocity,
                    out byte requiresDetour))
            {
                return;
            }

            NavigationVelocityResults[evaluatedIndex] = navigationVelocity;
            NavigationDetourResults[evaluatedIndex] = requiresDetour;
        }
        #endregion
    }
    #endregion
}
