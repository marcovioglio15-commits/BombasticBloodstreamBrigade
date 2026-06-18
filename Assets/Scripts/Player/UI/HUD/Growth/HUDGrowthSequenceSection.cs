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
    #endregion

    private FixedString64Bytes displayedScheduleId;
    private int displayedNextStepIndex = -1;
    private int displayedVisibleCount = -1;
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
            HandleMissingPlayer();
            return;
        }

        PlayerGrowthSequenceHudVisualReference visualReference = runtimeEntityManager.GetComponentData<PlayerGrowthSequenceHudVisualReference>(playerEntity);
        Entity configEntity = visualReference.ConfigEntity;

        if (!runtimeEntityManager.Exists(configEntity) ||
            !runtimeEntityManager.HasComponent<PlayerGrowthSequenceHudVisualConfig>(configEntity) ||
            !runtimeEntityManager.HasBuffer<PlayerGrowthSequenceHudStepVisualElement>(configEntity))
        {
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

            return;
        }

        DynamicBuffer<PlayerGrowthSequenceHudStepVisualElement> steps = runtimeEntityManager.GetBuffer<PlayerGrowthSequenceHudStepVisualElement>(configEntity, true);
        int slotCount = ResolveSlotCount();
        int maximumVisibleSteps = config.MaximumVisibleSteps > 0
            ? math.min(config.MaximumVisibleSteps, scheduleStepCount)
            : scheduleStepCount;
        int visibleCount = math.min(slotCount, maximumVisibleSteps);

        if (visibleCount <= 0)
        {
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

        if ((imageSlots == null || imageSlots.Length <= 0) && textSlots != null && textSlots.Length > 0)
            imageSlots = BuildImageSlotsFromTextSlots(textSlots);
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

    /// <summary>
    /// Builds image slots on the same preauthored GameObjects used by text slots, avoiding runtime GameObject instantiation.
    /// </summary>
    /// <param name="sourceTextSlots">Text slot pool already present in the scene or prefab.</param>
    /// <returns>Image slot array aligned with the text slot array.</returns>
    private static Image[] BuildImageSlotsFromTextSlots(TMP_Text[] sourceTextSlots)
    {
        if (sourceTextSlots == null || sourceTextSlots.Length <= 0)
            return Array.Empty<Image>();

        Image[] resolvedImageSlots = new Image[sourceTextSlots.Length];

        for (int slotIndex = 0; slotIndex < sourceTextSlots.Length; slotIndex++)
        {
            TMP_Text textSlot = sourceTextSlots[slotIndex];

            if (textSlot == null)
                continue;

            Image imageSlot = textSlot.GetComponent<Image>();

            if (imageSlot == null)
                imageSlot = textSlot.gameObject.AddComponent<Image>();

            imageSlot.enabled = false;
            imageSlot.raycastTarget = false;
            resolvedImageSlots[slotIndex] = imageSlot;
        }

        return resolvedImageSlots;
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

        if (fontSize > 0f)
            textSlot.fontSize = fontSize;

        textSlot.color = ToColor(isNext ? step.NextColor : step.NormalColor);
        textSlot.outlineColor = ToColor(isNext ? step.NextOutlineColor : step.NormalOutlineColor);
        textSlot.outlineWidth = math.saturate(isNext ? step.NextOutlineWidth : step.NormalOutlineWidth);
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
    #endregion

    #endregion
}
