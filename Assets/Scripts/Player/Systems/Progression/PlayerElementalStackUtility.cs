using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Shared helper that applies elemental stacks and threshold procs on the player.
/// </summary>
public static class PlayerElementalStackUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies elemental stacks to player runtime buffer and resolves threshold procs.
    /// </summary>
    /// <param name="playerEntity">Target player entity.</param>
    /// <param name="stacksToAdd">Stacks amount to add.</param>
    /// <param name="effectConfig">Elemental effect payload.</param>
    /// <param name="stackLookup">Buffer lookup containing player elemental stacks.</param>
    /// <param name="thresholdProcTriggered">True when threshold proc triggers while applying stacks.</param>
    /// <returns>True when stacks are applied.</returns>
    public static bool TryApplyStacks(Entity playerEntity,
                                      float stacksToAdd,
                                      in ElementalEffectConfig effectConfig,
                                      ref BufferLookup<PlayerElementStackElement> stackLookup,
                                      out bool thresholdProcTriggered)
    {
        thresholdProcTriggered = false;

        if (stacksToAdd <= 0f)
            return false;

        if (!stackLookup.HasBuffer(playerEntity))
            return false;

        DynamicBuffer<PlayerElementStackElement> stackBuffer = stackLookup[playerEntity];
        int stackIndex = FindStackIndex(in stackBuffer, effectConfig.ElementType);
        PlayerElementStackElement stackElement = stackIndex >= 0 ? stackBuffer[stackIndex] : BuildInitialStack(in effectConfig);

        SynchronizeStackDefinition(ref stackElement, in effectConfig);
        bool isProcActive = IsProcActive(in stackElement);

        if (isProcActive)
        {
            switch (stackElement.ReapplyMode)
            {
                case ElementalProcReapplyMode.IgnoreWhileProcActive:
                    return false;

                case ElementalProcReapplyMode.RefreshActiveProc:
                    RefreshActiveProc(ref stackElement);
                    thresholdProcTriggered = false;
                    WriteStack(ref stackBuffer, stackIndex, in stackElement);
                    return true;
            }
        }

        float previousStacks = math.max(0f, stackElement.CurrentStacks);
        float maximumStacks = math.max(0.1f, stackElement.MaximumStacks);
        float thresholdStacks = math.max(0.1f, stackElement.ProcThresholdStacks);
        float nextStacks = previousStacks + stacksToAdd;

        if (nextStacks > maximumStacks)
            nextStacks = maximumStacks;

        stackElement.CurrentStacks = nextStacks;
        bool crossedThreshold = previousStacks < thresholdStacks && nextStacks >= thresholdStacks;

        if (crossedThreshold)
        {
            thresholdProcTriggered = true;
            TriggerProc(ref stackElement);

            if (stackElement.ConsumeStacksOnProc != 0)
                stackElement.CurrentStacks = math.max(0f, stackElement.CurrentStacks - thresholdStacks);
        }

        WriteStack(ref stackBuffer, stackIndex, in stackElement);
        return true;
    }
    #endregion

    #region Helpers
    private static int FindStackIndex(in DynamicBuffer<PlayerElementStackElement> stackBuffer, ElementType elementType)
    {
        for (int index = 0; index < stackBuffer.Length; index++)
        {
            if (stackBuffer[index].ElementType == elementType)
                return index;
        }

        return -1;
    }

    private static PlayerElementStackElement BuildInitialStack(in ElementalEffectConfig effectConfig)
    {
        PlayerElementStackElement stackElement = new PlayerElementStackElement
        {
            ElementType = effectConfig.ElementType,
            EffectKind = effectConfig.EffectKind,
            ProcMode = effectConfig.ProcMode,
            ReapplyMode = effectConfig.ReapplyMode,
            CurrentStacks = 0f,
            DotRemainingSeconds = 0f,
            DotTickTimer = 0f,
            ImpedimentRemainingSeconds = 0f,
            CurrentImpedimentSlowPercent = 0f
        };

        SynchronizeStackDefinition(ref stackElement, in effectConfig);
        return stackElement;
    }

    private static void SynchronizeStackDefinition(ref PlayerElementStackElement stackElement, in ElementalEffectConfig effectConfig)
    {
        float maximumStacks = math.max(0.1f, effectConfig.MaximumStacks);
        float procThresholdStacks = math.max(0.1f, effectConfig.ProcThresholdStacks);

        if (procThresholdStacks > maximumStacks)
            procThresholdStacks = maximumStacks;

        stackElement.ElementType = effectConfig.ElementType;
        stackElement.EffectKind = effectConfig.EffectKind;
        stackElement.ProcMode = effectConfig.ProcMode;
        stackElement.ReapplyMode = effectConfig.ReapplyMode;
        stackElement.MaximumStacks = maximumStacks;
        stackElement.ProcThresholdStacks = procThresholdStacks;
        stackElement.StackDecayPerSecond = math.max(0f, effectConfig.StackDecayPerSecond);
        stackElement.ConsumeStacksOnProc = effectConfig.ConsumeStacksOnProc;
        stackElement.DotDamagePerTick = math.max(0f, effectConfig.DotDamagePerTick);
        stackElement.DotTickInterval = math.max(0.01f, effectConfig.DotTickInterval);
        stackElement.DotDurationSeconds = math.max(0.05f, effectConfig.DotDurationSeconds);
        stackElement.ImpedimentSlowPercentPerStack = math.clamp(effectConfig.ImpedimentSlowPercentPerStack, 0f, 100f);
        stackElement.ImpedimentProcSlowPercent = math.clamp(effectConfig.ImpedimentProcSlowPercent, 0f, 100f);
        stackElement.ImpedimentMaxSlowPercent = math.clamp(effectConfig.ImpedimentMaxSlowPercent, 0f, 100f);
        stackElement.ImpedimentDurationSeconds = math.max(0.05f, effectConfig.ImpedimentDurationSeconds);
    }

    private static void TriggerProc(ref PlayerElementStackElement stackElement)
    {
        switch (stackElement.EffectKind)
        {
            case ElementalEffectKind.Dots:
                float dotTickInterval = math.max(0.01f, stackElement.DotTickInterval);
                float dotDurationSeconds = math.max(0.05f, stackElement.DotDurationSeconds);
                bool wasDotActive = stackElement.DotRemainingSeconds > 0f;
                stackElement.DotRemainingSeconds = math.max(stackElement.DotRemainingSeconds, dotDurationSeconds);

                if (!wasDotActive ||
                    stackElement.DotTickTimer <= 0f ||
                    stackElement.DotTickTimer > dotTickInterval)
                    stackElement.DotTickTimer = dotTickInterval;

                return;

            case ElementalEffectKind.Impediment:
                float procSlowPercent = math.min(stackElement.ImpedimentProcSlowPercent, stackElement.ImpedimentMaxSlowPercent);

                if (procSlowPercent < 0f)
                    procSlowPercent = 0f;

                stackElement.CurrentImpedimentSlowPercent = procSlowPercent;
                stackElement.ImpedimentRemainingSeconds = math.max(stackElement.ImpedimentRemainingSeconds, stackElement.ImpedimentDurationSeconds);
                return;
        }
    }

    private static bool IsProcActive(in PlayerElementStackElement stackElement)
    {
        switch (stackElement.EffectKind)
        {
            case ElementalEffectKind.Dots:
                return stackElement.DotRemainingSeconds > 0f;

            case ElementalEffectKind.Impediment:
                return stackElement.ImpedimentRemainingSeconds > 0f;

            default:
                return false;
        }
    }

    private static bool RefreshActiveProc(ref PlayerElementStackElement stackElement)
    {
        switch (stackElement.EffectKind)
        {
            case ElementalEffectKind.Dots:
                float dotTickInterval = math.max(0.01f, stackElement.DotTickInterval);
                stackElement.DotRemainingSeconds = math.max(0.05f, stackElement.DotDurationSeconds);

                if (stackElement.DotTickTimer <= 0f ||
                    stackElement.DotTickTimer > dotTickInterval)
                    stackElement.DotTickTimer = dotTickInterval;

                return true;

            case ElementalEffectKind.Impediment:
                float procSlowPercent = math.min(stackElement.ImpedimentProcSlowPercent, stackElement.ImpedimentMaxSlowPercent);

                if (procSlowPercent < 0f)
                    procSlowPercent = 0f;

                stackElement.CurrentImpedimentSlowPercent = procSlowPercent;
                stackElement.ImpedimentRemainingSeconds = math.max(0.05f, stackElement.ImpedimentDurationSeconds);
                return true;

            default:
                return false;
        }
    }

    private static void WriteStack(ref DynamicBuffer<PlayerElementStackElement> stackBuffer,
                                   int stackIndex,
                                   in PlayerElementStackElement stackElement)
    {
        if (stackIndex >= 0)
            stackBuffer[stackIndex] = stackElement;
        else
            stackBuffer.Add(stackElement);
    }
    #endregion

    #endregion
}
