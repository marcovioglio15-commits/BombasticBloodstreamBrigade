using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Shared helper that applies elemental stacks and threshold procs on enemies.
/// </summary>
public static class EnemyElementalStackUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies elemental stacks to one enemy and triggers the configured proc when its threshold is crossed.
    /// Used by projectile and trail hit paths after they resolve an eligible enemy target.
    /// </summary>
    /// <param name="enemyEntity">Enemy entity receiving stacks.</param>
    /// <param name="stacksToAdd">Stack amount to add before threshold evaluation.</param>
    /// <param name="effectConfig">Elemental effect definition baked from the active power-up payload.</param>
    /// <param name="stackLookup">Writable stack-buffer lookup for enemies.</param>
    /// <param name="thresholdProcTriggered">True when this application crossed the proc threshold.</param>
    /// <returns>True when stacks or an active proc refresh were applied; otherwise false.</returns>
    public static bool TryApplyStacks(Entity enemyEntity,
                                      float stacksToAdd,
                                      in ElementalEffectConfig effectConfig,
                                      ref BufferLookup<EnemyElementStackElement> stackLookup,
                                      out bool thresholdProcTriggered)
    {
        thresholdProcTriggered = false;

        if (stacksToAdd <= 0f)
            return false;

        if (!stackLookup.HasBuffer(enemyEntity))
            return false;

        DynamicBuffer<EnemyElementStackElement> stackBuffer = stackLookup[enemyEntity];
        int stackIndex = FindStackIndex(in stackBuffer, effectConfig.ElementType);
        EnemyElementStackElement stackElement = stackIndex >= 0 ? stackBuffer[stackIndex] : BuildInitialStack(in effectConfig);

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
    /// <summary>
    /// Finds the first stack entry matching the requested element type.
    /// </summary>
    /// <param name="stackBuffer">Element stack buffer owned by the target enemy.</param>
    /// <param name="elementType">Element type to resolve.</param>
    /// <returns>Stack index, or -1 when the element is not present yet.</returns>
    private static int FindStackIndex(in DynamicBuffer<EnemyElementStackElement> stackBuffer, ElementType elementType)
    {
        for (int index = 0; index < stackBuffer.Length; index++)
        {
            if (stackBuffer[index].ElementType == elementType)
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Creates a new stack entry initialized from the current effect config.
    /// </summary>
    /// <param name="effectConfig">Elemental effect definition baked from the active power-up payload.</param>
    /// <returns>Initialized stack element with zero current stacks.</returns>
    private static EnemyElementStackElement BuildInitialStack(in ElementalEffectConfig effectConfig)
    {
        EnemyElementStackElement stackElement = new EnemyElementStackElement
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

    /// <summary>
    /// Synchronizes mutable stack definition fields with the newest effect config before stack application.
    /// </summary>
    /// <param name="stackElement">Stack entry that will receive sanitized config values.</param>
    /// <param name="effectConfig">Elemental effect definition baked from the active power-up payload.</param>
    private static void SynchronizeStackDefinition(ref EnemyElementStackElement stackElement, in ElementalEffectConfig effectConfig)
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

    /// <summary>
    /// Starts the elemental proc represented by the current stack element.
    /// </summary>
    /// <param name="stackElement">Stack entry whose proc state should be activated.</param>
    private static void TriggerProc(ref EnemyElementStackElement stackElement)
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

    /// <summary>
    /// Checks whether the stack currently has an active proc window.
    /// </summary>
    /// <param name="stackElement">Stack entry to inspect.</param>
    /// <returns>True when the configured proc kind is active.</returns>
    private static bool IsProcActive(in EnemyElementStackElement stackElement)
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

    /// <summary>
    /// Refreshes the active proc window according to the stack definition.
    /// </summary>
    /// <param name="stackElement">Stack entry whose active proc window should be refreshed.</param>
    /// <returns>True when the configured proc kind was refreshed.</returns>
    private static bool RefreshActiveProc(ref EnemyElementStackElement stackElement)
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

    /// <summary>
    /// Writes the updated stack entry back into the target buffer.
    /// </summary>
    /// <param name="stackBuffer">Element stack buffer owned by the target enemy.</param>
    /// <param name="stackIndex">Existing stack index, or -1 to append a new stack entry.</param>
    /// <param name="stackElement">Stack value to write.</param>
    private static void WriteStack(ref DynamicBuffer<EnemyElementStackElement> stackBuffer,
                                   int stackIndex,
                                   in EnemyElementStackElement stackElement)
    {
        if (stackIndex >= 0)
            stackBuffer[stackIndex] = stackElement;
        else
            stackBuffer.Add(stackElement);
    }
    #endregion

    #endregion
}
