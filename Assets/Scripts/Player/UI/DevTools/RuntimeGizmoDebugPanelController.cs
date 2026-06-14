using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Drives the runtime gizmo debug panel embedded in the gameplay HUD canvas.
/// none.
/// </summary>
[DisallowMultipleComponent]
public sealed class RuntimeGizmoDebugPanelController : MonoBehaviour
{
    #region Constants
    private const string DefaultToggleBindingDisplay = "Ctrl+Shift+G";
    private const string UnsupportedCheckmarkGlyph = "\u2713";
    private const string FallbackCheckmarkGlyph = "V";
    private const string OrbitalProjectionCollisionRadiusTogglePath = "PlayerSection/OrbitalProjectionCollisionRadiusRow/Toggle";
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Panel")]
    [Tooltip("Root object of the runtime gizmo panel. It stays hidden until the bound debug action is pressed.")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("Title text updated with the resolved input binding used to toggle the panel visibility.")]
    [SerializeField] private TMP_Text panelTitleText;

    [Header("General Toggles")]
    [Tooltip("Toggle used to show or hide runtime gizmo labels.")]
    [SerializeField] private Toggle showLabelsToggle;

    [Header("Player Toggles")]
    [Tooltip("Toggle used to show or hide the player pickup radius gizmo.")]
    [SerializeField] private Toggle playerPickupRadiusToggle;

    [Tooltip("Toggle used to show or hide the player movement vector gizmo.")]
    [SerializeField] private Toggle playerMoveVectorToggle;

    [Tooltip("Toggle used to show or hide the player look direction gizmo.")]
    [SerializeField] private Toggle playerLookDirectionToggle;

    [Tooltip("Toggle used to show or hide orbital projection collision radius gizmos.")]
    [SerializeField] private Toggle orbitalProjectionCollisionRadiusToggle;

    [Header("Enemy Toggles")]
    [Tooltip("Toggle used to show or hide enemy body radius gizmos.")]
    [SerializeField] private Toggle enemyBodyRadiusToggle;

    [Tooltip("Toggle used to show or hide enemy contact radius gizmos.")]
    [SerializeField] private Toggle enemyContactRadiusToggle;

    [Tooltip("Toggle used to show or hide enemy area radius gizmos.")]
    [SerializeField] private Toggle enemyAreaRadiusToggle;

    [Tooltip("Toggle used to show or hide enemy separation radius gizmos.")]
    [SerializeField] private Toggle enemySeparationRadiusToggle;

    [Tooltip("Toggle used to show or hide enemy wander target gizmos.")]
    [SerializeField] private Toggle enemyWanderTargetToggle;

    [Header("Spawner Toggles")]
    [Tooltip("Toggle used to show or hide spawner spawn radius gizmos.")]
    [SerializeField] private Toggle spawnerSpawnRadiusToggle;

    [Tooltip("Toggle used to show or hide spawner despawn radius gizmos.")]
    [SerializeField] private Toggle spawnerDespawnRadiusToggle;

    [Header("Bomb Toggles")]
    [Tooltip("Toggle used to show or hide bomb radius gizmos.")]
    [SerializeField] private Toggle bombRadiusToggle;

    [Tooltip("Toggle used to show or hide bomb velocity gizmos.")]
    [SerializeField] private Toggle bombVelocityToggle;

    [Header("Projectile Toggles")]
    [Tooltip("Toggle used to show or hide projectile impact radius gizmos.")]
    [SerializeField] private Toggle projectileImpactRadiusToggle;

    [Tooltip("Toggle used to show or hide projectile velocity gizmos.")]
    [SerializeField] private Toggle projectileVelocityToggle;

    [Tooltip("Toggle used to show or hide projectile remaining range gizmos.")]
    [SerializeField] private Toggle projectileRemainingRangeToggle;
    #endregion

    private InputAction boundPanelToggleAction;
    #endregion

    #region Methods

    #region Unity Methods
    private void Awake()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        if (panelRoot != null)
            panelRoot.SetActive(false);

        enabled = false;
        return;
#endif

        InitializeToggles();
        SanitizeToggleGlyphs();
        RefreshPanelTitle();
        ApplyPanelVisibility(RuntimeGizmoDebugState.PanelVisible, true);
        RefreshAllToggleValues();
    }

    private void OnEnable()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return;
#endif

        RuntimeGizmoDebugState.StateChanged += HandleDebugStateChanged;
        PlayerInputRuntime.RuntimeInitialized += HandleInputRuntimeInitialized;
        PlayerInputRuntime.RuntimeShutdown += HandleInputRuntimeShutdown;
        BindPanelToggleAction();
    }

    private void OnDisable()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return;
#endif

        UnbindPanelToggleAction();
        PlayerInputRuntime.RuntimeInitialized -= HandleInputRuntimeInitialized;
        PlayerInputRuntime.RuntimeShutdown -= HandleInputRuntimeShutdown;
        RuntimeGizmoDebugState.StateChanged -= HandleDebugStateChanged;
    }
    #endregion

    #region Initialization
    private void InitializeToggles()
    {
        ResolvePrefabToggleReferences();
        BindToggle(showLabelsToggle, RuntimeGizmoDebugState.ShowLabels, HandleShowLabelsChanged);
        BindToggle(playerPickupRadiusToggle, RuntimeGizmoDebugState.PlayerPickupRadiusEnabled, HandlePlayerPickupRadiusChanged);
        BindToggle(playerMoveVectorToggle, RuntimeGizmoDebugState.PlayerMoveVectorEnabled, HandlePlayerMoveVectorChanged);
        BindToggle(playerLookDirectionToggle, RuntimeGizmoDebugState.PlayerLookDirectionEnabled, HandlePlayerLookDirectionChanged);
        BindToggle(orbitalProjectionCollisionRadiusToggle,
                   RuntimeGizmoDebugState.OrbitalProjectionCollisionRadiusEnabled,
                   HandleOrbitalProjectionCollisionRadiusChanged);
        BindToggle(enemyBodyRadiusToggle, RuntimeGizmoDebugState.EnemyBodyRadiusEnabled, HandleEnemyBodyRadiusChanged);
        BindToggle(enemyContactRadiusToggle, RuntimeGizmoDebugState.EnemyContactRadiusEnabled, HandleEnemyContactRadiusChanged);
        BindToggle(enemyAreaRadiusToggle, RuntimeGizmoDebugState.EnemyAreaRadiusEnabled, HandleEnemyAreaRadiusChanged);
        BindToggle(enemySeparationRadiusToggle, RuntimeGizmoDebugState.EnemySeparationRadiusEnabled, HandleEnemySeparationRadiusChanged);
        BindToggle(enemyWanderTargetToggle, RuntimeGizmoDebugState.EnemyWanderTargetEnabled, HandleEnemyWanderTargetChanged);
        BindToggle(spawnerSpawnRadiusToggle, RuntimeGizmoDebugState.SpawnerSpawnRadiusEnabled, HandleSpawnerSpawnRadiusChanged);
        BindToggle(spawnerDespawnRadiusToggle, RuntimeGizmoDebugState.SpawnerDespawnRadiusEnabled, HandleSpawnerDespawnRadiusChanged);
        BindToggle(bombRadiusToggle, RuntimeGizmoDebugState.BombRadiusEnabled, HandleBombRadiusChanged);
        BindToggle(bombVelocityToggle, RuntimeGizmoDebugState.BombVelocityEnabled, HandleBombVelocityChanged);
        BindToggle(projectileImpactRadiusToggle, RuntimeGizmoDebugState.ProjectileImpactRadiusEnabled, HandleProjectileImpactRadiusChanged);
        BindToggle(projectileVelocityToggle, RuntimeGizmoDebugState.ProjectileVelocityEnabled, HandleProjectileVelocityChanged);
        BindToggle(projectileRemainingRangeToggle, RuntimeGizmoDebugState.ProjectileRemainingRangeEnabled, HandleProjectileRemainingRangeChanged);
    }

    /// <summary>
    /// Resolves prefab children added after existing scene controller bindings without creating runtime UI.
    /// </summary>
    private void ResolvePrefabToggleReferences()
    {
        if (orbitalProjectionCollisionRadiusToggle != null || panelRoot == null)
            return;

        Transform orbitalProjectionToggleTransform = panelRoot.transform.Find(OrbitalProjectionCollisionRadiusTogglePath);

        if (orbitalProjectionToggleTransform == null)
            return;

        orbitalProjectionCollisionRadiusToggle = orbitalProjectionToggleTransform.GetComponent<Toggle>();
    }

    /// <summary>
    /// Replaces unsupported checkmark glyphs used by toggle graphics with an ASCII fallback so the panel stays warning-free
    /// even when the assigned TMP font asset lacks the original symbol.
    /// none.
    /// </summary>
    private void SanitizeToggleGlyphs()
    {
        if (panelRoot == null)
            return;

        Toggle[] toggles = panelRoot.GetComponentsInChildren<Toggle>(true);

        // Normalize only the toggle graphics so user-authored panel labels stay untouched.
        for (int toggleIndex = 0; toggleIndex < toggles.Length; toggleIndex++)
        {
            Toggle toggle = toggles[toggleIndex];

            if (toggle == null)
                continue;

            TMP_Text checkmarkText = toggle.graphic as TMP_Text;

            if (checkmarkText == null)
                continue;

            ReplaceUnsupportedCheckmarkGlyph(checkmarkText);
        }
    }

    private static void ReplaceUnsupportedCheckmarkGlyph(TMP_Text targetText)
    {
        if (targetText == null)
            return;

        string currentText = targetText.text;

        if (string.IsNullOrEmpty(currentText))
            return;

        if (!currentText.Contains(UnsupportedCheckmarkGlyph))
            return;

        targetText.text = currentText.Replace(UnsupportedCheckmarkGlyph, FallbackCheckmarkGlyph);
    }

    private static void BindToggle(Toggle toggle, bool initialValue, UnityAction<bool> callback)
    {
        if (toggle == null)
            return;

        toggle.onValueChanged.RemoveAllListeners();
        toggle.SetIsOnWithoutNotify(initialValue);

        if (callback == null)
            return;

        toggle.onValueChanged.AddListener(callback);
    }
    #endregion

    #region Input
    private void BindPanelToggleAction()
    {
        InputAction panelToggleAction = PlayerInputRuntime.RuntimeGizmoPanelToggleAction;

        if (panelToggleAction == boundPanelToggleAction)
            return;

        UnbindPanelToggleAction();

        if (panelToggleAction == null)
        {
            RefreshPanelTitle();
            return;
        }

        boundPanelToggleAction = panelToggleAction;
        boundPanelToggleAction.performed += HandlePanelTogglePerformed;
        RefreshPanelTitle();
    }

    private void UnbindPanelToggleAction()
    {
        if (boundPanelToggleAction == null)
            return;

        boundPanelToggleAction.performed -= HandlePanelTogglePerformed;
        boundPanelToggleAction = null;
    }

    private void HandlePanelTogglePerformed(InputAction.CallbackContext context)
    {
        RuntimeGizmoDebugState.PanelVisible = !RuntimeGizmoDebugState.PanelVisible;
    }

    private void HandleInputRuntimeInitialized()
    {
        BindPanelToggleAction();
    }

    private void HandleInputRuntimeShutdown()
    {
        UnbindPanelToggleAction();
        RefreshPanelTitle();
    }
    #endregion

    #region State Sync
    private void HandleDebugStateChanged()
    {
        ApplyPanelVisibility(RuntimeGizmoDebugState.PanelVisible, false);
        RefreshAllToggleValues();
        RefreshPanelTitle();
    }

    private void ApplyPanelVisibility(bool isVisible, bool forceRefresh)
    {
        if (panelRoot == null)
            return;

        if (!forceRefresh && panelRoot.activeSelf == isVisible)
            return;

        panelRoot.SetActive(isVisible);
    }

    private void RefreshAllToggleValues()
    {
        SetToggleValue(showLabelsToggle, RuntimeGizmoDebugState.ShowLabels);
        SetToggleValue(playerPickupRadiusToggle, RuntimeGizmoDebugState.PlayerPickupRadiusEnabled);
        SetToggleValue(playerMoveVectorToggle, RuntimeGizmoDebugState.PlayerMoveVectorEnabled);
        SetToggleValue(playerLookDirectionToggle, RuntimeGizmoDebugState.PlayerLookDirectionEnabled);
        SetToggleValue(orbitalProjectionCollisionRadiusToggle, RuntimeGizmoDebugState.OrbitalProjectionCollisionRadiusEnabled);
        SetToggleValue(enemyBodyRadiusToggle, RuntimeGizmoDebugState.EnemyBodyRadiusEnabled);
        SetToggleValue(enemyContactRadiusToggle, RuntimeGizmoDebugState.EnemyContactRadiusEnabled);
        SetToggleValue(enemyAreaRadiusToggle, RuntimeGizmoDebugState.EnemyAreaRadiusEnabled);
        SetToggleValue(enemySeparationRadiusToggle, RuntimeGizmoDebugState.EnemySeparationRadiusEnabled);
        SetToggleValue(enemyWanderTargetToggle, RuntimeGizmoDebugState.EnemyWanderTargetEnabled);
        SetToggleValue(spawnerSpawnRadiusToggle, RuntimeGizmoDebugState.SpawnerSpawnRadiusEnabled);
        SetToggleValue(spawnerDespawnRadiusToggle, RuntimeGizmoDebugState.SpawnerDespawnRadiusEnabled);
        SetToggleValue(bombRadiusToggle, RuntimeGizmoDebugState.BombRadiusEnabled);
        SetToggleValue(bombVelocityToggle, RuntimeGizmoDebugState.BombVelocityEnabled);
        SetToggleValue(projectileImpactRadiusToggle, RuntimeGizmoDebugState.ProjectileImpactRadiusEnabled);
        SetToggleValue(projectileVelocityToggle, RuntimeGizmoDebugState.ProjectileVelocityEnabled);
        SetToggleValue(projectileRemainingRangeToggle, RuntimeGizmoDebugState.ProjectileRemainingRangeEnabled);
    }

    private void RefreshPanelTitle()
    {
        if (panelTitleText == null)
            return;

        string bindingDisplay = PlayerInputRuntime.ResolveBindingDisplayString(PlayerInputRuntime.RuntimeGizmoPanelToggleAction,
                                                                               DefaultToggleBindingDisplay);
        panelTitleText.text = string.Format("Runtime Gizmos  [{0}]", bindingDisplay);
    }

    private static void SetToggleValue(Toggle toggle, bool value)
    {
        if (toggle == null)
            return;

        toggle.SetIsOnWithoutNotify(value);
    }
    #endregion

    #region Toggle Callbacks
    private static void HandleShowLabelsChanged(bool isEnabled)
    {
        RuntimeGizmoDebugState.ShowLabels = isEnabled;
    }

    private static void HandlePlayerPickupRadiusChanged(bool isEnabled)
    {
        RuntimeGizmoDebugState.PlayerPickupRadiusEnabled = isEnabled;
    }

    private static void HandlePlayerMoveVectorChanged(bool isEnabled)
    {
        RuntimeGizmoDebugState.PlayerMoveVectorEnabled = isEnabled;
    }

    private static void HandlePlayerLookDirectionChanged(bool isEnabled)
    {
        RuntimeGizmoDebugState.PlayerLookDirectionEnabled = isEnabled;
    }

    /// <summary>
    /// Propagates the orbital projection collision-radius toggle to the shared runtime gizmo debug state.
    /// </summary>
    /// <param name="isEnabled">Current UI toggle value.</param>
    private static void HandleOrbitalProjectionCollisionRadiusChanged(bool isEnabled)
    {
        RuntimeGizmoDebugState.OrbitalProjectionCollisionRadiusEnabled = isEnabled;
    }

    private static void HandleEnemyBodyRadiusChanged(bool isEnabled)
    {
        RuntimeGizmoDebugState.EnemyBodyRadiusEnabled = isEnabled;
    }

    private static void HandleEnemyContactRadiusChanged(bool isEnabled)
    {
        RuntimeGizmoDebugState.EnemyContactRadiusEnabled = isEnabled;
    }

    private static void HandleEnemyAreaRadiusChanged(bool isEnabled)
    {
        RuntimeGizmoDebugState.EnemyAreaRadiusEnabled = isEnabled;
    }

    private static void HandleEnemySeparationRadiusChanged(bool isEnabled)
    {
        RuntimeGizmoDebugState.EnemySeparationRadiusEnabled = isEnabled;
    }

    private static void HandleEnemyWanderTargetChanged(bool isEnabled)
    {
        RuntimeGizmoDebugState.EnemyWanderTargetEnabled = isEnabled;
    }

    private static void HandleSpawnerSpawnRadiusChanged(bool isEnabled)
    {
        RuntimeGizmoDebugState.SpawnerSpawnRadiusEnabled = isEnabled;
    }

    private static void HandleSpawnerDespawnRadiusChanged(bool isEnabled)
    {
        RuntimeGizmoDebugState.SpawnerDespawnRadiusEnabled = isEnabled;
    }

    private static void HandleBombRadiusChanged(bool isEnabled)
    {
        RuntimeGizmoDebugState.BombRadiusEnabled = isEnabled;
    }

    private static void HandleBombVelocityChanged(bool isEnabled)
    {
        RuntimeGizmoDebugState.BombVelocityEnabled = isEnabled;
    }

    private static void HandleProjectileImpactRadiusChanged(bool isEnabled)
    {
        RuntimeGizmoDebugState.ProjectileImpactRadiusEnabled = isEnabled;
    }

    private static void HandleProjectileVelocityChanged(bool isEnabled)
    {
        RuntimeGizmoDebugState.ProjectileVelocityEnabled = isEnabled;
    }

    private static void HandleProjectileRemainingRangeChanged(bool isEnabled)
    {
        RuntimeGizmoDebugState.ProjectileRemainingRangeEnabled = isEnabled;
    }
    #endregion

    #endregion
}
