using TMPro;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Handles authored HUD timer configuration, ECS component setup, and TMP clock rendering.
/// none.
/// </summary>
[System.Serializable]
public sealed class HUDRunTimerSection
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables the run timer section and its authoritative ECS timer setup.")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("TMP text used to render the run timer in `00:00` format.")]
    [SerializeField] private TMP_Text timerText;

    [Tooltip("Direction used by the run timer. Backward counts down toward a defeat at zero.")]
    [SerializeField] private PlayerRunTimerDirection direction = PlayerRunTimerDirection.Forward;

    [Tooltip("Initial value in seconds used only when Direction is set to Backward.")]
    [SerializeField] private float initialSeconds = 300f;

    [Tooltip("Hides the timer text while no valid player entity is available.")]
    [SerializeField] private bool hideWhenPlayerMissing = true;
    #endregion

    private Entity configuredPlayerEntity;
    private PlayerRunTimerDirection lastConfiguredDirection;
    private float lastConfiguredInitialSeconds = -1f;
    private int displayedTotalSeconds = int.MinValue;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies the initial visual state before runtime ECS data becomes available.
    /// none.
    /// </summary>
    public void Initialize()
    {
        ApplyInitialVisualState();
    }

    /// <summary>
    /// Applies the initial authored timer text or hides it when the section is not visible without a player.
    /// none.
    /// </summary>
    public void ApplyInitialVisualState()
    {
        displayedTotalSeconds = int.MinValue;

        if (!isEnabled || timerText == null)
        {
            HideTimerImmediate();
            return;
        }

        if (hideWhenPlayerMissing)
        {
            HideTimerImmediate();
            return;
        }

        ApplyDisplaySeconds(HUDRunTimerRuntimeUtility.ResolveInitialDisplaySeconds(direction, initialSeconds));
    }

    /// <summary>
    /// Clears cached runtime bindings and applies the missing-player visual state.
    /// none.
    /// </summary>
    public void HandleMissingPlayer()
    {
        configuredPlayerEntity = Entity.Null;
        displayedTotalSeconds = int.MinValue;

        if (!isEnabled || timerText == null)
        {
            HideTimerImmediate();
            return;
        }

        if (hideWhenPlayerMissing)
        {
            HideTimerImmediate();
            return;
        }

        ApplyDisplaySeconds(HUDRunTimerRuntimeUtility.ResolveInitialDisplaySeconds(direction, initialSeconds));
    }

    /// <summary>
    /// Ensures ECS timer components exist on the current player and refreshes the managed TMP clock.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager used to read and write timer components.</param>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    public void Update(EntityManager runtimeEntityManager, Entity playerEntity)
    {
        if (!isEnabled || timerText == null)
        {
            HideTimerImmediate();
            return;
        }

        if (!runtimeEntityManager.Exists(playerEntity))
        {
            HandleMissingPlayer();
            return;
        }

        EnsureTimerComponents(runtimeEntityManager, playerEntity);

        if (!runtimeEntityManager.HasComponent<PlayerRunTimerConfig>(playerEntity) ||
            !runtimeEntityManager.HasComponent<PlayerRunTimerState>(playerEntity))
        {
            HandleMissingPlayer();
            return;
        }

        PlayerRunTimerConfig timerConfig = runtimeEntityManager.GetComponentData<PlayerRunTimerConfig>(playerEntity);
        PlayerRunTimerState timerState = runtimeEntityManager.GetComponentData<PlayerRunTimerState>(playerEntity);
        ApplyDisplaySeconds(HUDRunTimerRuntimeUtility.ResolveDisplaySeconds(in timerConfig, in timerState));
    }
    #endregion

    #region Setup
    /// <summary>
    /// Ensures the current player entity exposes the timer components that match the authored HUD configuration.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager used to add or update timer components.</param>
    /// <param name="playerEntity">Player entity currently driven by this HUD section.</param>
    private void EnsureTimerComponents(EntityManager runtimeEntityManager, Entity playerEntity)
    {
        float sanitizedInitialSeconds = Mathf.Max(0f, initialSeconds);
        bool requiresReconfiguration = playerEntity != configuredPlayerEntity ||
                                       direction != lastConfiguredDirection ||
                                       !Mathf.Approximately(sanitizedInitialSeconds, lastConfiguredInitialSeconds);
        PlayerRunTimerConfig timerConfig = HUDRunTimerRuntimeUtility.CreateConfig(direction, sanitizedInitialSeconds);
        PlayerRunTimerState timerState = HUDRunTimerRuntimeUtility.CreateState(direction, sanitizedInitialSeconds);

        if (!runtimeEntityManager.HasComponent<PlayerRunTimerConfig>(playerEntity))
            runtimeEntityManager.AddComponentData(playerEntity, timerConfig);
        else if (requiresReconfiguration)
            runtimeEntityManager.SetComponentData(playerEntity, timerConfig);

        if (!runtimeEntityManager.HasComponent<PlayerRunTimerState>(playerEntity))
            runtimeEntityManager.AddComponentData(playerEntity, timerState);
        else if (requiresReconfiguration)
            runtimeEntityManager.SetComponentData(playerEntity, timerState);

        if (!requiresReconfiguration)
            return;

        configuredPlayerEntity = playerEntity;
        lastConfiguredDirection = direction;
        lastConfiguredInitialSeconds = sanitizedInitialSeconds;
        displayedTotalSeconds = int.MinValue;
    }
    #endregion

    #region Presentation
    /// <summary>
    /// Applies one display value to the target TMP text only when it changed from the previous frame.
    /// </summary>
    /// <param name="totalSeconds">Whole seconds value to display.</param>
    private void ApplyDisplaySeconds(int totalSeconds)
    {
        if (timerText == null)
            return;

        if (displayedTotalSeconds == totalSeconds && timerText.enabled)
            return;

        displayedTotalSeconds = totalSeconds;
        HUDRunTimerRuntimeUtility.ApplyClockText(timerText, totalSeconds);
    }

    /// <summary>
    /// Hides the timer text immediately.
    /// none.
    /// </summary>
    private void HideTimerImmediate()
    {
        if (timerText == null)
            return;

        timerText.enabled = false;
    }
    #endregion

    #endregion
}
