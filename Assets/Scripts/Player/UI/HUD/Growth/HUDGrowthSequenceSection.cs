using System;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the Player HUD growth sequence from ECS visual config and the equipped level-up schedule.
/// </summary>
[Serializable]
public sealed class HUDGrowthSequenceSection
{
    #region Constants
    private const string DefaultContainerName = "GrowthSequence Container";
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Enables the ECS-driven growth sequence HUD section.")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Optional root object for the growth sequence UI pool. When empty, GrowthSequence Container is found under the HUD manager.")]
    [SerializeField] private GameObject rootObject;

    [Tooltip("Optional preauthored TMP labels used as growth sequence slots. When empty, labels are collected from the root object.")]
    [SerializeField] private TMP_Text[] textSlots;

    [Tooltip("Optional preauthored Image slots used by image-mode growth sequence entries. When empty, Image components are collected from the root object.")]
    [SerializeField] private Image[] imageSlots;

    [Tooltip("When enabled, missing references are resolved once from the HUD hierarchy during Initialize.")]
    [SerializeField] private bool autoDiscoverReferences = true;

    [Tooltip("Name of the growth sequence root used by auto discovery.")]
    [SerializeField] private string growthSequenceContainerName = DefaultContainerName;

    [Tooltip("Creates a small runtime-only slot pool when GrowthSequence Container has no authored TMP or Image slots. Disable this once the HUD prefab carries explicit slots.")]
    [SerializeField] private bool createFallbackSlotsWhenMissing = true;

    [Tooltip("Number of fallback slots created under GrowthSequence Container when the authored slot pool is missing.")]
    [SerializeField] private int fallbackSlotCount = HUDGrowthSequenceFallbackSlotUtility.DefaultSlotCount;

    [Tooltip("Width in UI units assigned to each fallback growth sequence slot.")]
    [SerializeField] private float fallbackSlotWidth = HUDGrowthSequenceFallbackSlotUtility.DefaultSlotWidth;

    [Tooltip("Height in UI units assigned to each fallback growth sequence slot.")]
    [SerializeField] private float fallbackSlotHeight = HUDGrowthSequenceFallbackSlotUtility.DefaultSlotHeight;

    [Tooltip("Horizontal spacing in UI units between generated fallback slots.")]
    [SerializeField] private float fallbackSlotSpacing = HUDGrowthSequenceFallbackSlotUtility.DefaultSlotSpacing;

    [Tooltip("TMP font size assigned to generated fallback text slots.")]
    [SerializeField] private float fallbackFontSize = HUDGrowthSequenceFallbackSlotUtility.DefaultFontSize;
    #endregion

    private FixedString64Bytes displayedScheduleId;
    private int displayedNextStepIndex = -1;
    private int displayedVisibleCount = -1;
    private Entity lastConfigEntity;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool loggedMissingPlayerReference;
    private bool loggedMissingConfigEntity;
    private bool loggedMissingProgressionSchedule;
    private bool loggedEmptyStepBuffer;
    private bool loggedEmptySlotPool;
#endif
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

        ApplyInitialVisualState();
    }

    /// <summary>
    /// Applies the initial growth sequence state used before a valid player entity is resolved.
    /// </summary>
    public void ApplyInitialVisualState()
    {
        HideAllSlots();
        displayedScheduleId = default;
        displayedNextStepIndex = -1;
        displayedVisibleCount = -1;
        lastConfigEntity = Entity.Null;
    }

    /// <summary>
    /// Hides all growth sequence slots when the player or config entity is missing.
    /// </summary>
    public void HandleMissingPlayer()
    {
        if (!isEnabled)
            return;

        HideAllSlots();
    }

    /// <summary>
    /// Hides the growth sequence when the player is already at the configured level cap.
    /// </summary>
    public void HandleLevelCapReached()
    {
        if (!isEnabled)
            return;

        HideAllSlots();
    }

    /// <summary>
    /// Updates the growth sequence slots from ECS config and player progression state.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager used to read player and visual config data.</param>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    public void Update(EntityManager runtimeEntityManager, Entity playerEntity)
    {
        if (!isEnabled)
            return;

        if (!runtimeEntityManager.Exists(playerEntity) ||
            !runtimeEntityManager.HasComponent<PlayerGrowthSequenceHudVisualReference>(playerEntity) ||
            !runtimeEntityManager.HasComponent<PlayerProgressionConfig>(playerEntity) ||
            !runtimeEntityManager.HasComponent<PlayerLevel>(playerEntity))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogMissingPlayerReference(runtimeEntityManager, playerEntity);
#endif
            HandleMissingPlayer();
            return;
        }

        PlayerGrowthSequenceHudVisualReference visualReference = runtimeEntityManager.GetComponentData<PlayerGrowthSequenceHudVisualReference>(playerEntity);
        Entity configEntity = visualReference.ConfigEntity;

        if (!runtimeEntityManager.Exists(configEntity) ||
            !runtimeEntityManager.HasComponent<PlayerGrowthSequenceHudVisualConfig>(configEntity) ||
            !runtimeEntityManager.HasBuffer<PlayerGrowthSequenceHudStepVisualElement>(configEntity))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogDiagnosticOnce(ref loggedMissingConfigEntity,
                              "[HUDGrowthSequenceSection] Player growth sequence visual config entity is missing or incomplete. Reimport/rebake the player prefab or owner scene so PlayerAuthoringBaker writes the HUD visual config.");
#endif
            HandleMissingPlayer();
            return;
        }

        PlayerGrowthSequenceHudVisualConfig config = runtimeEntityManager.GetComponentData<PlayerGrowthSequenceHudVisualConfig>(configEntity);

        if (config.Enabled == 0)
        {
            HideAllSlots();
            return;
        }

        PlayerProgressionConfig progressionConfig = runtimeEntityManager.GetComponentData<PlayerProgressionConfig>(playerEntity);
        PlayerLevel playerLevel = runtimeEntityManager.GetComponentData<PlayerLevel>(playerEntity);

        if (!TryResolveActiveSchedule(progressionConfig,
                                      playerLevel.Current,
                                      out FixedString64Bytes scheduleId,
                                      out int nextStepIndex,
                                      out int scheduleStepCount))
        {
            if (config.HideWhenPlayerMissing != 0)
                HideAllSlots();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogDiagnosticOnce(ref loggedMissingProgressionSchedule,
                              "[HUDGrowthSequenceSection] Active level-up schedule could not be resolved from PlayerProgressionConfig. Growth sequence HUD has no schedule to render.");
#endif
            return;
        }

        DynamicBuffer<PlayerGrowthSequenceHudStepVisualElement> steps = runtimeEntityManager.GetBuffer<PlayerGrowthSequenceHudStepVisualElement>(configEntity, true);

        if (steps.Length <= 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogDiagnosticOnce(ref loggedEmptyStepBuffer,
                              "[HUDGrowthSequenceSection] Growth sequence visual step buffer is empty. Sync Growth Sequence from Level-up & Progression and rebake the player.");
#endif
            HideAllSlots();
            return;
        }

        int slotCount = ResolveSlotCount();
        int maximumVisibleSteps = config.MaximumVisibleSteps > 0
            ? math.min(config.MaximumVisibleSteps, scheduleStepCount)
            : scheduleStepCount;
        int visibleCount = math.min(slotCount, maximumVisibleSteps);

        if (visibleCount <= 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogDiagnosticOnce(ref loggedEmptySlotPool,
                              "[HUDGrowthSequenceSection] Growth sequence has runtime data but no UI slots. Assign text/image slots or enable fallback slot creation on HUDManager.");
#endif
            HideAllSlots();
            return;
        }

        if (lastConfigEntity != configEntity ||
            !displayedScheduleId.Equals(scheduleId) ||
            displayedNextStepIndex != nextStepIndex ||
            displayedVisibleCount != visibleCount)
        {
            displayedScheduleId = scheduleId;
            displayedNextStepIndex = nextStepIndex;
            displayedVisibleCount = visibleCount;
            lastConfigEntity = configEntity;
        }

        RenderSteps(steps, scheduleId, nextStepIndex, visibleCount);
        HideSlotsFrom(visibleCount);
        SetRootVisible(true);
    }
    #endregion

    #region Reference Discovery
    /// <summary>
    /// Finds growth sequence root, text slots and image slots from the HUD hierarchy.
    /// </summary>
    /// <param name="searchRoot">HUD hierarchy root used for optional reference discovery.</param>
    private void ResolveReferences(Transform searchRoot)
    {
        if (searchRoot == null)
            return;

        if (rootObject == null)
        {
            Transform root = FindChildByName(searchRoot, string.IsNullOrWhiteSpace(growthSequenceContainerName) ? DefaultContainerName : growthSequenceContainerName);

            if (root != null)
                rootObject = root.gameObject;
        }

        Transform slotRoot = rootObject != null ? rootObject.transform : searchRoot;

        if (textSlots == null || textSlots.Length <= 0)
            textSlots = slotRoot.GetComponentsInChildren<TMP_Text>(true);

        if (imageSlots == null || imageSlots.Length <= 0)
            imageSlots = slotRoot.GetComponentsInChildren<Image>(true);

        if (ResolveSlotCount() <= 0 && createFallbackSlotsWhenMissing && rootObject != null)
        {
            HUDGrowthSequenceFallbackSlotPool fallbackSlotPool = HUDGrowthSequenceFallbackSlotUtility.Create(rootObject.transform,
                                                                                                            fallbackSlotCount,
                                                                                                            fallbackSlotWidth,
                                                                                                            fallbackSlotHeight,
                                                                                                            fallbackSlotSpacing,
                                                                                                            fallbackFontSize);
            textSlots = fallbackSlotPool.TextSlots;
            imageSlots = fallbackSlotPool.ImageSlots;
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

    #region Rendering
    /// <summary>
    /// Renders all visible growth steps for the active schedule.
    /// </summary>
    /// <param name="steps">Runtime growth step visual buffer.</param>
    /// <param name="scheduleId">Active equipped schedule ID.</param>
    /// <param name="nextStepIndex">Step index that will be applied on the next level-up.</param>
    /// <param name="visibleCount">Maximum number of slots to fill.</param>
    private void RenderSteps(DynamicBuffer<PlayerGrowthSequenceHudStepVisualElement> steps,
                             FixedString64Bytes scheduleId,
                             int nextStepIndex,
                             int visibleCount)
    {
        int renderedCount = 0;

        for (int searchStepIndex = 0; searchStepIndex < visibleCount; searchStepIndex++)
        {
            if (!TryFindStep(steps, scheduleId, searchStepIndex, out PlayerGrowthSequenceHudStepVisualElement step))
                continue;

            bool isNext = step.StepIndex == nextStepIndex;
            RenderSlot(renderedCount, step, isNext);
            renderedCount++;
        }

        HideSlotsFrom(renderedCount);
    }

    /// <summary>
    /// Renders one growth step into a preauthored slot.
    /// </summary>
    /// <param name="slotIndex">Slot index to write.</param>
    /// <param name="step">Runtime growth step visual data.</param>
    /// <param name="isNext">Whether this step is the next level-up target.</param>
    private void RenderSlot(int slotIndex,
                            PlayerGrowthSequenceHudStepVisualElement step,
                            bool isNext)
    {
        TMP_Text textSlot = textSlots != null && slotIndex < textSlots.Length ? textSlots[slotIndex] : null;
        Image imageSlot = imageSlots != null && slotIndex < imageSlots.Length ? imageSlots[slotIndex] : null;
        Sprite sprite = isNext ? step.NextSprite.Value : step.NormalSprite.Value;
        bool renderImage = step.PresentationMode == PlayerGrowthSequenceHudPresentationMode.Image && sprite != null && imageSlot != null;

        if (textSlot != null)
        {
            textSlot.gameObject.SetActive(true);
            textSlot.enabled = !renderImage;

            if (!renderImage)
                ApplyTextSlot(textSlot, step, isNext);
        }

        if (imageSlot != null)
        {
            imageSlot.gameObject.SetActive(true);
            imageSlot.enabled = renderImage;

            if (renderImage)
                imageSlot.sprite = sprite;
        }
    }

    /// <summary>
    /// Applies text-mode visual state to one TMP slot.
    /// </summary>
    /// <param name="textSlot">TMP label to update.</param>
    /// <param name="step">Runtime growth step visual data.</param>
    /// <param name="isNext">Whether this step is the next level-up target.</param>
    private static void ApplyTextSlot(TMP_Text textSlot,
                                      PlayerGrowthSequenceHudStepVisualElement step,
                                      bool isNext)
    {
        textSlot.text = ResolveSlotText(step);

        TMP_FontAsset fontAsset = isNext ? step.NextFontAsset.Value : step.NormalFontAsset.Value;

        if (fontAsset != null)
            textSlot.font = fontAsset;

        float fontSize = isNext ? step.NextFontSize : step.NormalFontSize;
        bool autoSizeEnabled = isNext ? step.NextAutoSizeEnabled != 0 : step.NormalAutoSizeEnabled != 0;

        ApplyTextSizing(textSlot, fontSize, autoSizeEnabled, step, isNext);

        Color textColor = ToColor(isNext ? step.NextColor : step.NormalColor);
        Color outlineColor = ToColor(isNext ? step.NextOutlineColor : step.NormalOutlineColor);
        ResolveVisibleTextState(ref textColor, ref outlineColor);

        textSlot.color = textColor;
        textSlot.outlineColor = outlineColor;
        textSlot.outlineWidth = math.saturate(isNext ? step.NextOutlineWidth : step.NormalOutlineWidth);
    }

    /// <summary>
    /// Applies fixed or TMP auto-size font sizing to one growth sequence text slot.
    /// </summary>
    /// <param name="textSlot">TMP label to update.</param>
    /// <param name="fontSize">Authored preferred font size.</param>
    /// <param name="autoSizeEnabled">Whether TMP auto-size should be active.</param>
    /// <param name="step">Runtime growth step visual data.</param>
    /// <param name="isNext">Whether this step is the next level-up target.</param>
    private static void ApplyTextSizing(TMP_Text textSlot,
                                        float fontSize,
                                        bool autoSizeEnabled,
                                        PlayerGrowthSequenceHudStepVisualElement step,
                                        bool isNext)
    {
        textSlot.enableAutoSizing = autoSizeEnabled;

        if (!autoSizeEnabled)
        {
            if (fontSize > 0f)
                textSlot.fontSize = fontSize;

            return;
        }

        float autoSizeMin = math.max(0f, isNext ? step.NextAutoSizeMin : step.NormalAutoSizeMin);
        float autoSizeMax = math.max(autoSizeMin, isNext ? step.NextAutoSizeMax : step.NormalAutoSizeMax);
        textSlot.fontSizeMin = autoSizeMin;
        textSlot.fontSizeMax = autoSizeMax;

        if (fontSize > 0f)
            textSlot.fontSize = math.clamp(fontSize, autoSizeMin, autoSizeMax);
    }

    /// <summary>
    /// Finds one growth step by active schedule and step index.
    /// </summary>
    /// <param name="steps">Runtime growth step visual buffer.</param>
    /// <param name="scheduleId">Active equipped schedule ID.</param>
    /// <param name="stepIndex">Step index to find.</param>
    /// <param name="step">Resolved growth step visual data.</param>
    /// <returns>True when a matching step was found.</returns>
    private static bool TryFindStep(DynamicBuffer<PlayerGrowthSequenceHudStepVisualElement> steps,
                                    FixedString64Bytes scheduleId,
                                    int stepIndex,
                                    out PlayerGrowthSequenceHudStepVisualElement step)
    {
        for (int candidateIndex = 0; candidateIndex < steps.Length; candidateIndex++)
        {
            PlayerGrowthSequenceHudStepVisualElement candidate = steps[candidateIndex];

            if (!candidate.ScheduleId.Equals(scheduleId) || candidate.StepIndex != stepIndex)
                continue;

            step = candidate;
            return true;
        }

        step = default;
        return false;
    }
    #endregion

    #region Progression
    /// <summary>
    /// Resolves active schedule ID, next step index and schedule length from the progression blob.
    /// </summary>
    /// <param name="progressionConfig">Player progression config blob reference.</param>
    /// <param name="currentLevel">Current player level.</param>
    /// <param name="scheduleId">Resolved active schedule ID.</param>
    /// <param name="nextStepIndex">Step index applied by the next level-up.</param>
    /// <param name="scheduleStepCount">Number of steps in the active schedule.</param>
    /// <returns>True when the active schedule can be resolved.</returns>
    private static bool TryResolveActiveSchedule(PlayerProgressionConfig progressionConfig,
                                                 int currentLevel,
                                                 out FixedString64Bytes scheduleId,
                                                 out int nextStepIndex,
                                                 out int scheduleStepCount)
    {
        scheduleId = default;
        nextStepIndex = -1;
        scheduleStepCount = 0;

        if (!progressionConfig.Config.IsCreated)
            return false;

        ref PlayerProgressionConfigBlob root = ref progressionConfig.Config.Value;
        int equippedScheduleIndex = root.EquippedScheduleIndex;

        if (equippedScheduleIndex < 0 || equippedScheduleIndex >= root.Schedules.Length)
            return false;

        ref PlayerLevelUpScheduleBlob schedule = ref root.Schedules[equippedScheduleIndex];

        if (schedule.Steps.Length <= 0)
            return false;

        scheduleId = new FixedString64Bytes(schedule.ScheduleId.ToString());
        scheduleStepCount = schedule.Steps.Length;
        nextStepIndex = math.max(0, currentLevel) % scheduleStepCount;
        return true;
    }
    #endregion

    #region Visibility
    /// <summary>
    /// Hides every preauthored growth sequence slot.
    /// </summary>
    private void HideAllSlots()
    {
        HideSlotsFrom(0);
        SetRootVisible(false);
    }

    /// <summary>
    /// Hides every slot after a visible prefix.
    /// </summary>
    /// <param name="startIndex">First slot index to hide.</param>
    private void HideSlotsFrom(int startIndex)
    {
        if (textSlots != null)
        {
            for (int slotIndex = math.max(0, startIndex); slotIndex < textSlots.Length; slotIndex++)
            {
                if (textSlots[slotIndex] == null)
                    continue;

                textSlots[slotIndex].enabled = false;
            }
        }

        if (imageSlots != null)
        {
            for (int slotIndex = math.max(0, startIndex); slotIndex < imageSlots.Length; slotIndex++)
            {
                if (imageSlots[slotIndex] == null)
                    continue;

                imageSlots[slotIndex].enabled = false;
            }
        }
    }

    /// <summary>
    /// Shows or hides the growth sequence root object.
    /// </summary>
    /// <param name="visible">Whether the root should be visible.</param>
    private void SetRootVisible(bool visible)
    {
        if (rootObject != null && rootObject.activeSelf != visible)
            rootObject.SetActive(visible);
    }

    /// <summary>
    /// Resolves how many preauthored slots are available.
    /// </summary>
    /// <returns>Maximum slot count across text and image slot arrays.</returns>
    private int ResolveSlotCount()
    {
        int textSlotCount = textSlots != null ? textSlots.Length : 0;
        int imageSlotCount = imageSlots != null ? imageSlots.Length : 0;
        return math.max(textSlotCount, imageSlotCount);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves display text for one growth step.
    /// </summary>
    /// <param name="step">Runtime growth step visual data.</param>
    /// <returns>Slot text.</returns>
    private static string ResolveSlotText(PlayerGrowthSequenceHudStepVisualElement step)
    {
        string text = step.Text.ToString();

        if (!string.IsNullOrWhiteSpace(text))
            return text;

        string statName = step.StatName.ToString();

        if (!string.IsNullOrWhiteSpace(statName))
            return statName;

        return string.Format("{0}", step.StepIndex + 1);
    }

    /// <summary>
    /// Converts a float4 color into Unity color.
    /// </summary>
    /// <param name="value">Runtime color value.</param>
    /// <returns>Unity color with unchanged channel values.</returns>
    private static Color ToColor(float4 value)
    {
        return new Color(value.x, value.y, value.z, value.w);
    }

    /// <summary>
    /// Prevents text-mode growth steps from becoming fully invisible when both authored alpha channels are zero.
    /// </summary>
    /// <param name="textColor">Mutable TMP face color.</param>
    /// <param name="outlineColor">Mutable TMP outline color.</param>
    private static void ResolveVisibleTextState(ref Color textColor, ref Color outlineColor)
    {
        if (textColor.a > 0f || outlineColor.a > 0f)
            return;

        textColor.a = 1f;
        outlineColor.a = 1f;
    }

    #endregion

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    #region Diagnostics
    /// <summary>
    /// Logs the first missing-reference reason for growth sequence runtime binding.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager used to inspect the player entity.</param>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    private void LogMissingPlayerReference(EntityManager runtimeEntityManager, Entity playerEntity)
    {
        if (loggedMissingPlayerReference)
            return;

        if (!runtimeEntityManager.Exists(playerEntity))
            return;

        if (!runtimeEntityManager.HasComponent<PlayerGrowthSequenceHudVisualReference>(playerEntity))
        {
            LogDiagnosticOnce(ref loggedMissingPlayerReference,
                              "[HUDGrowthSequenceSection] Player entity is missing PlayerGrowthSequenceHudVisualReference. The active player bake does not include the new Growth Sequence HUD config yet; reimport/rebake the player prefab or owner scene.");
            return;
        }

        if (!runtimeEntityManager.HasComponent<PlayerProgressionConfig>(playerEntity))
        {
            LogDiagnosticOnce(ref loggedMissingPlayerReference,
                              "[HUDGrowthSequenceSection] Player entity is missing PlayerProgressionConfig, so the active growth schedule cannot be resolved.");
            return;
        }

        if (!runtimeEntityManager.HasComponent<PlayerLevel>(playerEntity))
        {
            LogDiagnosticOnce(ref loggedMissingPlayerReference,
                              "[HUDGrowthSequenceSection] Player entity is missing PlayerLevel, so the next growth step cannot be resolved.");
        }
    }

    /// <summary>
    /// Logs one diagnostic message once per HUD section instance.
    /// </summary>
    /// <param name="logged">Mutable guard flag for this diagnostic.</param>
    /// <param name="message">Diagnostic message.</param>
    private static void LogDiagnosticOnce(ref bool logged, string message)
    {
        if (logged)
            return;

        logged = true;
        Debug.LogWarning(message);
    }
    #endregion
#endif

    #endregion
}
