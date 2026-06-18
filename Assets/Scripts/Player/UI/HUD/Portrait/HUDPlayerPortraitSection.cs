using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the Player HUD portrait Image from ECS portrait animation config and live player state.
/// </summary>
[Serializable]
public sealed class HUDPlayerPortraitSection
{
    #region Constants
    private const float DamageDeltaEpsilon = 0.001f;
    private const float PowerUpTimeEpsilon = 0.0001f;
    private const string DefaultContainerName = "PlayerPortraitContainer";
    private const string DefaultPortraitImageName = "Portrait";
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Enables the dynamic ECS-driven portrait HUD section.")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Optional root object for the portrait section. When empty, PlayerPortraitContainer is found under the HUD manager.")]
    [SerializeField] private GameObject rootObject;

    [Tooltip("UI Image used to render the selected portrait frame. When empty, the child named Portrait is found under PlayerPortraitContainer.")]
    [SerializeField] private Image portraitImage;

    [Tooltip("When enabled, missing references are resolved once from the HUD hierarchy during Initialize.")]
    [SerializeField] private bool autoDiscoverReferences = true;

    [Tooltip("Name of the portrait section root used by auto discovery.")]
    [SerializeField] private string portraitContainerName = DefaultContainerName;

    [Tooltip("Name of the portrait Image child used by auto discovery.")]
    [SerializeField] private string portraitImageName = DefaultPortraitImageName;
    #endregion

    private readonly Dictionary<string, PowerUpSnapshot> powerUpSnapshots = new Dictionary<string, PowerUpSnapshot>(32, StringComparer.OrdinalIgnoreCase);
    private int activeAnimationId;
    private int activeAnimationBufferIndex = -1;
    private int activeFrameOffset;
    private int playbackDirection = 1;
    private float frameTimer;
    private bool activeAnimationCompleted;
    private bool damageObservationInitialized;
    private float previousHealth;
    private float previousShield;
    private Sprite fallbackSprite;
    private Sprite lastAppliedSprite;
    private Entity lastConfigEntity;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves optional scene references and applies the initial hidden state before ECS data is available.
    /// </summary>
    /// <param name="searchRoot">HUD hierarchy root used for optional reference discovery.</param>
    public void Initialize(Transform searchRoot)
    {
        if (autoDiscoverReferences)
            ResolveReferences(searchRoot);

        fallbackSprite = portraitImage != null ? portraitImage.sprite : null;
        ApplyInitialVisualState();
    }

    /// <summary>
    /// Applies the initial portrait state used before a valid player entity is resolved.
    /// </summary>
    public void ApplyInitialVisualState()
    {
        ResetRuntimeState();
        SetVisible(false);
    }

    /// <summary>
    /// Hides the portrait when the player is missing and the runtime config asks for that behavior.
    /// </summary>
    public void HandleMissingPlayer()
    {
        if (!isEnabled)
            return;

        SetVisible(false);
        ResetRuntimeState();
    }

    /// <summary>
    /// Updates the portrait animation from live ECS state.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager used to read player and portrait config data.</param>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    public void Update(EntityManager runtimeEntityManager, Entity playerEntity)
    {
        if (!isEnabled || portraitImage == null)
            return;

        if (!runtimeEntityManager.Exists(playerEntity) ||
            !runtimeEntityManager.HasComponent<PlayerPortraitHudVisualReference>(playerEntity))
        {
            HandleMissingPlayer();
            return;
        }

        PlayerPortraitHudVisualReference visualReference = runtimeEntityManager.GetComponentData<PlayerPortraitHudVisualReference>(playerEntity);
        Entity configEntity = visualReference.ConfigEntity;

        if (!runtimeEntityManager.Exists(configEntity) ||
            !runtimeEntityManager.HasComponent<PlayerPortraitHudVisualConfig>(configEntity) ||
            !runtimeEntityManager.HasBuffer<PlayerPortraitHudAnimationElement>(configEntity) ||
            !runtimeEntityManager.HasBuffer<PlayerPortraitHudFrameElement>(configEntity))
        {
            HandleMissingPlayer();
            return;
        }

        PlayerPortraitHudVisualConfig config = runtimeEntityManager.GetComponentData<PlayerPortraitHudVisualConfig>(configEntity);

        if (config.Enabled == 0)
        {
            SetVisible(false);
            return;
        }

        DynamicBuffer<PlayerPortraitHudAnimationElement> animations = runtimeEntityManager.GetBuffer<PlayerPortraitHudAnimationElement>(configEntity, true);
        DynamicBuffer<PlayerPortraitHudFrameElement> frames = runtimeEntityManager.GetBuffer<PlayerPortraitHudFrameElement>(configEntity, true);

        if (animations.Length <= 0)
        {
            SetVisible(config.HideWhenPlayerMissing == 0);
            return;
        }

        if (lastConfigEntity != configEntity)
        {
            ResetRuntimeState();
            lastConfigEntity = configEntity;
        }

        int requestedAnimationIndex = ResolveRequestedAnimationIndex(runtimeEntityManager,
                                                                     playerEntity,
                                                                     animations);
        UpdateActiveAnimation(requestedAnimationIndex, animations);
        AdvanceAndApplyFrame(animations, frames);
        SetVisible(true);
    }
    #endregion

    #region Reference Discovery
    /// <summary>
    /// Finds portrait container and Image references from the HUD hierarchy.
    /// </summary>
    /// <param name="searchRoot">HUD hierarchy root used for optional reference discovery.</param>
    private void ResolveReferences(Transform searchRoot)
    {
        if (searchRoot == null)
            return;

        if (rootObject == null)
        {
            Transform container = FindChildByName(searchRoot, string.IsNullOrWhiteSpace(portraitContainerName) ? DefaultContainerName : portraitContainerName);

            if (container != null)
                rootObject = container.gameObject;
        }

        if (portraitImage == null)
        {
            Transform imageRoot = rootObject != null
                ? FindChildByName(rootObject.transform, string.IsNullOrWhiteSpace(portraitImageName) ? DefaultPortraitImageName : portraitImageName)
                : FindChildByName(searchRoot, string.IsNullOrWhiteSpace(portraitImageName) ? DefaultPortraitImageName : portraitImageName);

            if (imageRoot != null)
                portraitImage = imageRoot.GetComponent<Image>();
        }
    }

    /// <summary>
    /// Finds the first child Transform with a matching name.
    /// </summary>
    /// <param name="root">Hierarchy root to scan.</param>
    /// <param name="targetName">Child object name to match.</param>
    /// <returns>Matching Transform, or null when not found.</returns>
    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        for (int childIndex = 0; childIndex < children.Length; childIndex++)
        {
            Transform child = children[childIndex];

            if (child != null && string.Equals(child.name, targetName, StringComparison.Ordinal))
                return child;
        }

        return null;
    }
    #endregion

    #region Animation Selection
    /// <summary>
    /// Resolves the highest-priority portrait animation requested by current player state.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager used to read player state.</param>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    /// <param name="animations">Runtime portrait animation buffer.</param>
    /// <returns>Animation buffer index to play, or -1 when no animation is available.</returns>
    private int ResolveRequestedAnimationIndex(EntityManager runtimeEntityManager,
                                               Entity playerEntity,
                                               DynamicBuffer<PlayerPortraitHudAnimationElement> animations)
    {
        int selectedIndex = FindBestRoleAnimation(animations, PlayerPortraitHudAnimationRole.Idle, default);
        int comboIndex = ResolveComboAnimationIndex(runtimeEntityManager, playerEntity, animations);
        int powerUpIndex = ResolvePowerUpAnimationIndex(runtimeEntityManager, playerEntity, animations);
        int damageIndex = ResolveDamageAnimationIndex(runtimeEntityManager, playerEntity, animations);
        int deathIndex = ResolveDeathAnimationIndex(runtimeEntityManager, playerEntity, animations);

        selectedIndex = SelectHigherPriority(selectedIndex, comboIndex, animations);
        selectedIndex = SelectHigherPriority(selectedIndex, powerUpIndex, animations);
        selectedIndex = SelectHigherPriority(selectedIndex, damageIndex, animations);
        selectedIndex = SelectHigherPriority(selectedIndex, deathIndex, animations);

        if (IsCurrentOneShotStillActive(animations, selectedIndex))
            return activeAnimationBufferIndex;

        return selectedIndex;
    }

    /// <summary>
    /// Resolves combo-rank idle portrait animation for the active combo rank.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager used to read combo state.</param>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    /// <param name="animations">Runtime portrait animation buffer.</param>
    /// <returns>Animation buffer index, or -1 when none matches.</returns>
    private static int ResolveComboAnimationIndex(EntityManager runtimeEntityManager,
                                                  Entity playerEntity,
                                                  DynamicBuffer<PlayerPortraitHudAnimationElement> animations)
    {
        if (!runtimeEntityManager.HasComponent<PlayerComboCounterState>(playerEntity))
            return -1;

        PlayerComboCounterState comboState = runtimeEntityManager.GetComponentData<PlayerComboCounterState>(playerEntity);

        if (comboState.CurrentRankIndex < 0 || comboState.CurrentRankId.IsEmpty)
            return -1;

        return FindBestRoleAnimation(animations,
                                     PlayerPortraitHudAnimationRole.ComboRankIdle,
                                     comboState.CurrentRankId);
    }

    /// <summary>
    /// Resolves the power-up acquisition event portrait animation for newly acquired or stacked power-ups.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager used to read power-up catalog data.</param>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    /// <param name="animations">Runtime portrait animation buffer.</param>
    /// <returns>Animation buffer index, or -1 when no new acquisition is detected.</returns>
    private int ResolvePowerUpAnimationIndex(EntityManager runtimeEntityManager,
                                             Entity playerEntity,
                                             DynamicBuffer<PlayerPortraitHudAnimationElement> animations)
    {
        if (!runtimeEntityManager.HasBuffer<PlayerPowerUpUnlockCatalogElement>(playerEntity))
            return -1;

        DynamicBuffer<PlayerPowerUpUnlockCatalogElement> catalog = runtimeEntityManager.GetBuffer<PlayerPowerUpUnlockCatalogElement>(playerEntity, true);
        int selectedIndex = -1;

        for (int catalogIndex = 0; catalogIndex < catalog.Length; catalogIndex++)
        {
            PlayerPowerUpUnlockCatalogElement powerUp = catalog[catalogIndex];
            string powerUpId = powerUp.PowerUpId.ToString();

            if (string.IsNullOrWhiteSpace(powerUpId))
                continue;

            PowerUpSnapshot previousSnapshot = powerUpSnapshots.TryGetValue(powerUpId, out PowerUpSnapshot snapshot)
                ? snapshot
                : default;
            bool hasNewAcquisition = previousSnapshot.Initialized &&
                                      (powerUp.CurrentUnlockCount > previousSnapshot.UnlockCount ||
                                       powerUp.LastAcquiredTime > previousSnapshot.LastAcquiredTime + PowerUpTimeEpsilon);

            powerUpSnapshots[powerUpId] = new PowerUpSnapshot
            {
                UnlockCount = powerUp.CurrentUnlockCount,
                LastAcquiredTime = powerUp.LastAcquiredTime,
                Initialized = true
            };

            if (!hasNewAcquisition)
                continue;

            int animationIndex = FindBestRoleAnimation(animations,
                                                       PlayerPortraitHudAnimationRole.PowerUpAcquired,
                                                       powerUp.PowerUpId);
            selectedIndex = SelectHigherPriority(selectedIndex, animationIndex, animations);
        }

        return selectedIndex;
    }

    /// <summary>
    /// Resolves the damage event portrait animation by observing health and shield decreases.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager used to read health and shield values.</param>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    /// <param name="animations">Runtime portrait animation buffer.</param>
    /// <returns>Animation buffer index, or -1 when no damage delta is detected.</returns>
    private int ResolveDamageAnimationIndex(EntityManager runtimeEntityManager,
                                            Entity playerEntity,
                                            DynamicBuffer<PlayerPortraitHudAnimationElement> animations)
    {
        float currentHealth = runtimeEntityManager.HasComponent<PlayerHealth>(playerEntity)
            ? runtimeEntityManager.GetComponentData<PlayerHealth>(playerEntity).Current
            : 0f;
        float currentShield = runtimeEntityManager.HasComponent<PlayerShield>(playerEntity)
            ? runtimeEntityManager.GetComponentData<PlayerShield>(playerEntity).Current
            : 0f;

        if (!damageObservationInitialized)
        {
            previousHealth = currentHealth;
            previousShield = currentShield;
            damageObservationInitialized = true;
            return -1;
        }

        bool tookDamage = currentHealth < previousHealth - DamageDeltaEpsilon ||
                          currentShield < previousShield - DamageDeltaEpsilon;
        previousHealth = currentHealth;
        previousShield = currentShield;

        if (!tookDamage)
            return -1;

        return FindBestRoleAnimation(animations, PlayerPortraitHudAnimationRole.Damage, default);
    }

    /// <summary>
    /// Resolves the death portrait animation while defeat is dying or finalized.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager used to read run outcome data.</param>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    /// <param name="animations">Runtime portrait animation buffer.</param>
    /// <returns>Animation buffer index, or -1 when the player is not defeated.</returns>
    private static int ResolveDeathAnimationIndex(EntityManager runtimeEntityManager,
                                                  Entity playerEntity,
                                                  DynamicBuffer<PlayerPortraitHudAnimationElement> animations)
    {
        if (!runtimeEntityManager.HasComponent<PlayerRunOutcomeState>(playerEntity))
            return -1;

        PlayerRunOutcomeState runOutcomeState = runtimeEntityManager.GetComponentData<PlayerRunOutcomeState>(playerEntity);

        if (runOutcomeState.Outcome != PlayerRunOutcome.Defeat && runOutcomeState.IsDying == 0)
            return -1;

        return FindBestRoleAnimation(animations, PlayerPortraitHudAnimationRole.Death, default);
    }

    /// <summary>
    /// Finds the highest-priority animation matching a role and optional trigger key.
    /// </summary>
    /// <param name="animations">Runtime portrait animation buffer.</param>
    /// <param name="role">Animation role to match.</param>
    /// <param name="triggerKey">Optional trigger key; empty matches only unkeyed role animations.</param>
    /// <returns>Best matching animation index, or -1 when none matches.</returns>
    private static int FindBestRoleAnimation(DynamicBuffer<PlayerPortraitHudAnimationElement> animations,
                                             PlayerPortraitHudAnimationRole role,
                                             FixedString64Bytes triggerKey)
    {
        int selectedIndex = -1;

        for (int animationIndex = 0; animationIndex < animations.Length; animationIndex++)
        {
            PlayerPortraitHudAnimationElement animation = animations[animationIndex];

            if (animation.Role != role)
                continue;

            if (!triggerKey.IsEmpty && !animation.TriggerKey.Equals(triggerKey))
                continue;

            if (triggerKey.IsEmpty && !animation.TriggerKey.IsEmpty)
                continue;

            selectedIndex = SelectHigherPriority(selectedIndex, animationIndex, animations);
        }

        return selectedIndex;
    }

    /// <summary>
    /// Selects the higher-priority animation index.
    /// </summary>
    /// <param name="currentIndex">Current selected animation index.</param>
    /// <param name="candidateIndex">Candidate animation index.</param>
    /// <param name="animations">Runtime portrait animation buffer.</param>
    /// <returns>Index of the higher-priority animation.</returns>
    private static int SelectHigherPriority(int currentIndex,
                                            int candidateIndex,
                                            DynamicBuffer<PlayerPortraitHudAnimationElement> animations)
    {
        if (candidateIndex < 0)
            return currentIndex;

        if (currentIndex < 0)
            return candidateIndex;

        return animations[candidateIndex].Priority >= animations[currentIndex].Priority
            ? candidateIndex
            : currentIndex;
    }
    #endregion

    #region Playback
    /// <summary>
    /// Updates the active animation when the requested animation changes or can restart.
    /// </summary>
    /// <param name="requestedAnimationIndex">Requested animation index.</param>
    /// <param name="animations">Runtime portrait animation buffer.</param>
    private void UpdateActiveAnimation(int requestedAnimationIndex,
                                       DynamicBuffer<PlayerPortraitHudAnimationElement> animations)
    {
        if (requestedAnimationIndex < 0 || requestedAnimationIndex >= animations.Length)
            return;

        PlayerPortraitHudAnimationElement requestedAnimation = animations[requestedAnimationIndex];
        bool shouldRestart = requestedAnimation.AnimationId != activeAnimationId ||
                             requestedAnimation.RestartWhenReentered != 0 && requestedAnimationIndex == activeAnimationBufferIndex && activeAnimationCompleted;

        if (!shouldRestart)
            return;

        activeAnimationId = requestedAnimation.AnimationId;
        activeAnimationBufferIndex = requestedAnimationIndex;
        activeFrameOffset = 0;
        playbackDirection = 1;
        frameTimer = 0f;
        activeAnimationCompleted = false;
    }

    /// <summary>
    /// Advances portrait playback and applies the current frame sprite to the Image.
    /// </summary>
    /// <param name="animations">Runtime portrait animation buffer.</param>
    /// <param name="frames">Runtime portrait frame buffer.</param>
    private void AdvanceAndApplyFrame(DynamicBuffer<PlayerPortraitHudAnimationElement> animations,
                                      DynamicBuffer<PlayerPortraitHudFrameElement> frames)
    {
        if (activeAnimationBufferIndex < 0 || activeAnimationBufferIndex >= animations.Length)
            return;

        PlayerPortraitHudAnimationElement animation = animations[activeAnimationBufferIndex];

        if (animation.FrameCount <= 0)
        {
            ApplyFallbackSprite();
            return;
        }

        float secondsPerFrame = Mathf.Max(0.0001f, animation.SecondsPerFrame / Mathf.Max(0.0001f, animation.PlaybackSpeedMultiplier));
        frameTimer += Time.unscaledDeltaTime;

        while (frameTimer >= secondsPerFrame)
        {
            frameTimer -= secondsPerFrame;
            AdvanceFrame(ref animation);
        }

        int frameIndex = animation.FrameStartIndex + Mathf.Clamp(activeFrameOffset, 0, animation.FrameCount - 1);

        if (frameIndex < 0 || frameIndex >= frames.Length)
        {
            ApplyFallbackSprite();
            return;
        }

        Sprite sprite = frames[frameIndex].Sprite.Value;

        if (sprite == null)
        {
            ApplyFallbackSprite();
            return;
        }

        if (sprite == lastAppliedSprite)
            return;

        lastAppliedSprite = sprite;
        portraitImage.sprite = sprite;
        portraitImage.enabled = sprite != null;
    }

    /// <summary>
    /// Advances the active frame offset according to the animation playback mode.
    /// </summary>
    /// <param name="animation">Active portrait animation.</param>
    private void AdvanceFrame(ref PlayerPortraitHudAnimationElement animation)
    {
        switch (animation.PlaybackMode)
        {
            case PlayerPortraitHudPlaybackMode.Once:
                if (activeFrameOffset >= animation.FrameCount - 1)
                {
                    activeAnimationCompleted = true;
                    return;
                }

                activeFrameOffset++;
                return;
            case PlayerPortraitHudPlaybackMode.PingPong:
                activeFrameOffset += playbackDirection;

                if (activeFrameOffset >= animation.FrameCount - 1)
                    playbackDirection = -1;
                else if (activeFrameOffset <= 0)
                    playbackDirection = 1;

                activeFrameOffset = Mathf.Clamp(activeFrameOffset, 0, animation.FrameCount - 1);
                return;
            default:
                activeFrameOffset = (activeFrameOffset + 1) % animation.FrameCount;
                return;
        }
    }

    /// <summary>
    /// Checks whether the current one-shot animation should keep playing before returning to idle.
    /// </summary>
    /// <param name="animations">Runtime portrait animation buffer.</param>
    /// <param name="requestedAnimationIndex">Newly requested animation index.</param>
    /// <returns>True when the current animation should remain active.</returns>
    private bool IsCurrentOneShotStillActive(DynamicBuffer<PlayerPortraitHudAnimationElement> animations,
                                             int requestedAnimationIndex)
    {
        if (activeAnimationBufferIndex < 0 || activeAnimationBufferIndex >= animations.Length)
            return false;

        PlayerPortraitHudAnimationElement activeAnimation = animations[activeAnimationBufferIndex];

        if (activeAnimation.PlaybackMode != PlayerPortraitHudPlaybackMode.Once || activeAnimationCompleted)
            return false;

        if (requestedAnimationIndex < 0 || requestedAnimationIndex >= animations.Length)
            return true;

        return activeAnimation.Priority > animations[requestedAnimationIndex].Priority;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Restores the authored HUD Image sprite when the active portrait animation has no baked frames.
    /// </summary>
    private void ApplyFallbackSprite()
    {
        if (portraitImage == null)
            return;

        if (fallbackSprite == lastAppliedSprite)
            return;

        lastAppliedSprite = fallbackSprite;
        portraitImage.sprite = fallbackSprite;
        portraitImage.enabled = fallbackSprite != null;
    }

    /// <summary>
    /// Shows or hides the portrait root and image.
    /// </summary>
    /// <param name="visible">Whether the portrait should be visible.</param>
    private void SetVisible(bool visible)
    {
        if (rootObject != null && rootObject.activeSelf != visible)
            rootObject.SetActive(visible);

        if (portraitImage != null && portraitImage.enabled != visible && visible)
            portraitImage.enabled = portraitImage.sprite != null;

        if (portraitImage != null && !visible)
            portraitImage.enabled = false;
    }

    /// <summary>
    /// Resets playback and event detection caches.
    /// </summary>
    private void ResetRuntimeState()
    {
        activeAnimationId = 0;
        activeAnimationBufferIndex = -1;
        activeFrameOffset = 0;
        playbackDirection = 1;
        frameTimer = 0f;
        activeAnimationCompleted = false;
        damageObservationInitialized = false;
        previousHealth = 0f;
        previousShield = 0f;
        lastAppliedSprite = null;
        lastConfigEntity = Entity.Null;
        powerUpSnapshots.Clear();
    }
    #endregion

    #endregion

    #region Nested Types
    private struct PowerUpSnapshot
    {
        public int UnlockCount;
        public float LastAcquiredTime;
        public bool Initialized;
    }
    #endregion
}
