using Unity.Entities;
using Unity.Transforms;

/// <summary>
/// This system group serves as a container for all player controller-related systems,
/// including those that handle player input, movement, shooting, and other gameplay mechanics.
/// By grouping these systems together, can manage their update order and dependencies more effectively,
/// ensuring that they run in a cohesive manner during the simulation phase of the game loop.
/// It is updated within the SimulationSystemGroup, which means it will run during the main simulation phase of the game loop,
/// after the fixed-step physics simulation and before the rendering phase.
/// This allows it to process player input and update player-related components in a timely manner,
/// ensuring responsive gameplay.
/// It is explicitly ordered before the TransformSystemGroup so the player movement written into LocalTransform
/// is baked into LocalToWorld within the same frame. This keeps the presentation-side camera follow
/// (which reads LocalToWorld) perfectly aligned with the rendered player visual and removes the one-frame
/// desync that otherwise surfaces as camera/player stutter under variable frame times.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
public sealed partial class PlayerControllerSystemGroup : ComponentSystemGroup
{
}
