using Unity.Entities;

/// <summary>
/// Seeds the <see cref="PlayerUserExperienceSettings"/> ECS singleton from the saved local user settings on the first
/// frame of the world, so presentation systems (visual pointer, controller rumble) honor the player's preferences
/// immediately even before the runtime Settings menu is ever opened. The work runs once and the system then disables
/// itself, so the idle majority of the session costs no per-frame updates. Live menu edits keep the singleton in sync
/// through <see cref="GameUserSettingsRuntimeUtility.Apply"/>.
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct GameUserSettingsEcsBootstrapSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Applies the cached or freshly loaded user settings to the ECS mirror once, then stops further updates.
    /// </summary>
    /// <param name="state">Mutable system state for the owning world.</param>
    public void OnUpdate(ref SystemState state)
    {
        // Idempotent: RefreshEcsMirror reuses the existing singleton when one is already present (e.g. created by the
        // Settings menu), so seeding here never produces a duplicate.
        GameUserSettingsRuntimeUtility.RefreshEcsMirror();
        state.Enabled = false;
    }
    #endregion

    #endregion
}
