using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Verifies activation-tap ownership, Resource Gate repayment, and stationary endpoint behavior for returning projectiles.
/// </summary>
public static class PlayerReturningProjectileRecallSmokeTest
{
    #region Constants
    private const float PrecisionEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Entry Point
    /// <summary>
    /// Runs deterministic recall checks without creating project assets or persistent runtime entities.
    /// </summary>
    public static void Run()
    {
        ValidateRecallInputAndResourceCost();
        ValidateToggleActiveOwnership();
        ValidateEndpointWaitingAndDirectRecall();
    }
    #endregion

    #region Authoring UI
    /// <summary>
    /// Verifies that activation-only controls appear only for an eligible Active and that Resource Gate repayment remains contextual.
    /// </summary>
    /// <param name="payloadProperty">Serialized Returning Projectiles payload owned by the smoke preset.</param>
    /// <param name="serializedPreset">Serialized preset used to apply the temporary mode selection.</param>
    public static void ValidateAuthoringUi(SerializedProperty payloadProperty, SerializedObject serializedPreset)
    {
        payloadProperty.FindPropertyRelative("returnStartMode").enumValueIndex = (int)ProjectileReturnStartMode.ActivationTap;
        serializedPreset.ApplyModifiedPropertiesWithoutUndo();

        // Passive context keeps timed delay controls and hides every active-input option.
        VisualElement passiveContainer = new VisualElement();
        PowerUpReturningProjectilesPayloadDrawerUtility.Build(passiveContainer, payloadProperty, false);
        ValidateContainerDisplay(passiveContainer,
                                 DisplayStyle.Flex,
                                 DisplayStyle.None,
                                 DisplayStyle.None,
                                 "Passive Returning Projectiles displayed active recall controls.");

        // Active context replaces delay with recall options while hiding cost repayment without Resource Gate.
        VisualElement activeContainer = new VisualElement();
        PowerUpReturningProjectilesPayloadDrawerUtility.Build(activeContainer, payloadProperty, true, false);
        ValidateContainerDisplay(activeContainer,
                                 DisplayStyle.None,
                                 DisplayStyle.Flex,
                                 DisplayStyle.None,
                                 "Active Returning Projectiles did not expose contextual recall controls.");
        ValidateReturnTransitionOrder(activeContainer);

        // Resource Gate context reveals only the extra repayment option.
        VisualElement gatedActiveContainer = new VisualElement();
        PowerUpReturningProjectilesPayloadDrawerUtility.Build(gatedActiveContainer, payloadProperty, true, true);
        ValidateContainerDisplay(gatedActiveContainer,
                                 DisplayStyle.None,
                                 DisplayStyle.Flex,
                                 DisplayStyle.Flex,
                                 "Resource Gate recall repayment remained hidden for an eligible Active.");

        payloadProperty.FindPropertyRelative("returnStartMode").enumValueIndex = (int)ProjectileReturnStartMode.AutomaticDelay;
        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
    }
    #endregion

    #region Recall Input
    /// <summary>
    /// Verifies early-recall filtering, version advancement, ready-count consumption, and optional energy repayment.
    /// </summary>
    private static void ValidateRecallInputAndResourceCost()
    {
        PlayerPowerUpSlotConfig slotConfig = new PlayerPowerUpSlotConfig
        {
            HasReturningProjectiles = 1,
            ActivationResource = PowerUpResourceType.Energy,
            MaximumEnergy = 100f,
            ActivationCost = 25f,
            ReturningProjectiles = new ReturningProjectilesConfig
            {
                ReturnStartMode = ProjectileReturnStartMode.ActivationTap,
                ReapplyResourceGateCostOnRecall = 1
            }
        };
        ComponentLookup<PlayerHealth> healthLookup = default;
        ComponentLookup<PlayerShield> shieldLookup = default;
        PlayerHealth updatedHealth = default;
        PlayerShield updatedShield = default;
        bool healthChanged = false;
        bool shieldChanged = false;
        int readyCount = 0;
        uint recallVersion = 5u;
        float energy = 100f;

        // A premature tap is owned by recall mode but cannot spend resources or enqueue a stale recall.
        bool consumedInput = PlayerReturningProjectileRecallActivationUtility.TryProcess(in slotConfig,
                                                                                          1,
                                                                                          true,
                                                                                          ref readyCount,
                                                                                          ref recallVersion,
                                                                                          ref energy,
                                                                                          Entity.Null,
                                                                                          ref healthLookup,
                                                                                          ref updatedHealth,
                                                                                          ref healthChanged,
                                                                                          ref shieldLookup,
                                                                                          ref updatedShield,
                                                                                          ref shieldChanged);

        if (!consumedInput || recallVersion != 5u || math.abs(energy - 100f) > PrecisionEpsilon)
            throw new InvalidOperationException("A disabled early recall changed its version or consumed Resource Gate energy.");

        // Endpoint-ready projectiles accept the same tap and pay the configured activation cost exactly once.
        readyCount = 2;
        consumedInput = PlayerReturningProjectileRecallActivationUtility.TryProcess(in slotConfig,
                                                                                     2,
                                                                                     true,
                                                                                     ref readyCount,
                                                                                     ref recallVersion,
                                                                                     ref energy,
                                                                                     Entity.Null,
                                                                                     ref healthLookup,
                                                                                     ref updatedHealth,
                                                                                     ref healthChanged,
                                                                                     ref shieldLookup,
                                                                                     ref updatedShield,
                                                                                     ref shieldChanged);

        if (!consumedInput || readyCount != 0 || recallVersion != 6u || math.abs(energy - 75f) > PrecisionEpsilon)
            throw new InvalidOperationException("A valid endpoint recall did not advance once and repay the Resource Gate cost.");

        // Insufficient energy keeps waiting projectiles eligible for a later valid tap.
        readyCount = 1;
        energy = 10f;
        consumedInput = PlayerReturningProjectileRecallActivationUtility.TryProcess(in slotConfig,
                                                                                     1,
                                                                                     true,
                                                                                     ref readyCount,
                                                                                     ref recallVersion,
                                                                                     ref energy,
                                                                                     Entity.Null,
                                                                                     ref healthLookup,
                                                                                     ref updatedHealth,
                                                                                     ref healthChanged,
                                                                                     ref shieldLookup,
                                                                                     ref updatedShield,
                                                                                     ref shieldChanged);

        if (!consumedInput || readyCount != 1 || recallVersion != 6u || math.abs(energy - 10f) > PrecisionEpsilon)
            throw new InvalidOperationException("A failed Resource Gate recall consumed readiness, version, or energy.");

        // Optional early recall bypasses endpoint readiness and can skip Resource Gate repayment.
        slotConfig.ReturningProjectiles.AllowEarlyActivationRecall = 1;
        slotConfig.ReturningProjectiles.ReapplyResourceGateCostOnRecall = 0;
        readyCount = 0;
        recallVersion = uint.MaxValue;
        consumedInput = PlayerReturningProjectileRecallActivationUtility.TryProcess(in slotConfig,
                                                                                     1,
                                                                                     true,
                                                                                     ref readyCount,
                                                                                     ref recallVersion,
                                                                                     ref energy,
                                                                                     Entity.Null,
                                                                                     ref healthLookup,
                                                                                     ref updatedHealth,
                                                                                     ref healthChanged,
                                                                                     ref shieldLookup,
                                                                                     ref updatedShield,
                                                                                     ref shieldChanged);

        if (!consumedInput || recallVersion != 1u || math.abs(energy - 10f) > PrecisionEpsilon)
            throw new InvalidOperationException("Early recall did not wrap its version safely or incorrectly charged a disabled cost.");
    }
    #endregion

    #region Toggle Active Ownership
    /// <summary>
    /// Verifies that a toggle Active preserves slot ownership and that a later passive override clears it.
    /// </summary>
    private static void ValidateToggleActiveOwnership()
    {
        PlayerPassiveToolsAggregationUtility.CreateDefaultState(out PlayerPassiveToolsState passiveToolsState);
        PlayerPassiveToolConfig toggleActiveTool = new PlayerPassiveToolConfig
        {
            IsDefined = 1,
            HasReturningProjectiles = 1,
            ReturningProjectiles = new ReturningProjectilesConfig
            {
                ReturnStartMode = ProjectileReturnStartMode.ActivationTap
            }
        };
        PlayerPassiveToolsAggregationUtility.AccumulateActiveTogglePassiveTool(ref passiveToolsState,
                                                                                in toggleActiveTool,
                                                                                1);

        if (passiveToolsState.HasReturningProjectilesActiveSlotOwner == 0 ||
            passiveToolsState.ReturningProjectilesActiveSlotIndex != 1)
        {
            throw new InvalidOperationException("A toggle Active did not preserve Returning Projectiles slot ownership.");
        }

        // A later ordinary passive override must never inherit the toggle slot input channel.
        PlayerPassiveToolConfig passiveTool = toggleActiveTool;
        passiveTool.ReturningProjectiles.ReturnStartMode = ProjectileReturnStartMode.AutomaticDelay;
        PlayerPassiveToolsAggregationUtility.AccumulatePassiveTool(ref passiveToolsState, in passiveTool);

        if (passiveToolsState.HasReturningProjectilesActiveSlotOwner != 0)
            throw new InvalidOperationException("An ordinary passive Returning Projectiles override inherited Active slot ownership.");
    }
    #endregion

    #region Return Transition
    /// <summary>
    /// Verifies that activation-tap projectiles remain stationary at their endpoint and bypass waiting after an accepted recall.
    /// </summary>
    private static void ValidateEndpointWaitingAndDirectRecall()
    {
        World world = new World("ReturningProjectileRecallSmokeTest");

        try
        {
            Entity projectileEntity = world.EntityManager.CreateEntity();
            DynamicBuffer<ProjectileReturnPathPoint> returnPath = world.EntityManager.AddBuffer<ProjectileReturnPathPoint>(projectileEntity);
            returnPath.Add(new ProjectileReturnPathPoint
            {
                Position = float3.zero
            });
            ReturningProjectilesConfig config = new ReturningProjectilesConfig
            {
                ReturnStartMode = ProjectileReturnStartMode.ActivationTap,
                ReturnSpeedMultiplier = 1f,
                OutboundSizeMultiplier = 1f,
                ReturnSizeMultiplier = 1f,
                SpinDuringFlight = 1,
                SpinSpeedDegreesPerSecond = 360f,
                ReturnHitPolicy = ProjectileReturnHitPolicy.CompleteReturn,
                PathSampleDistance = 0.25f
            };
            ProjectileReturnState returnState = new ProjectileReturnState
            {
                Enabled = 1,
                Phase = ProjectileReturnPhase.Outbound,
                Config = config,
                OriginalDamage = 4f,
                OriginalPenetrationMode = ProjectilePenetrationMode.None
            };
            Projectile projectile = new Projectile
            {
                Velocity = new float3(0f, 0f, 6f),
                Damage = 4f
            };
            ProjectilePerfectCircleState perfectCircleState = default;
            LocalTransform projectileTransform = LocalTransform.FromPosition(new float3(0f, 0f, 8f));

            ProjectileReturnRuntimeUtility.BeginReturn(ref returnState,
                                                        ref projectile,
                                                        ref perfectCircleState,
                                                        ref projectileTransform,
                                                        returnPath,
                                                        false,
                                                        false);
            ProjectileOwner owner = default;
            ComponentLookup<LocalToWorld> ownerTransformLookup = default;
            ProjectileReturnRuntimeUtility.SimulateReturn(ref returnState,
                                                           ref projectile,
                                                           ref projectileTransform,
                                                           in owner,
                                                           returnPath,
                                                           in ownerTransformLookup,
                                                           5f);

            if (returnState.Phase != ProjectileReturnPhase.Delaying ||
                math.lengthsq(projectile.Velocity) > PrecisionEpsilon ||
                math.lengthsq(projectileTransform.Position - new float3(0f, 0f, 8f)) > PrecisionEpsilon)
            {
                throw new InvalidOperationException("Activation Tap did not keep the projectile stationary at its outbound endpoint.");
            }

            // An accepted early recall uses the same transition data but skips endpoint waiting.
            returnState = new ProjectileReturnState
            {
                Enabled = 1,
                Phase = ProjectileReturnPhase.Outbound,
                Config = config,
                OriginalDamage = 4f,
                OriginalPenetrationMode = ProjectilePenetrationMode.None
            };
            projectile.Velocity = new float3(0f, 0f, 6f);
            projectileTransform = LocalTransform.FromPosition(new float3(0f, 0f, 4f));
            ProjectileReturnRuntimeUtility.BeginReturn(ref returnState,
                                                        ref projectile,
                                                        ref perfectCircleState,
                                                        ref projectileTransform,
                                                        returnPath,
                                                        false,
                                                        true);

            if (returnState.Phase != ProjectileReturnPhase.Returning)
                throw new InvalidOperationException("An accepted activation recall remained in endpoint waiting.");
        }
        finally
        {
            world.Dispose();
        }
    }
    #endregion

    #region Authoring Helpers
    /// <summary>
    /// Verifies mode selection precedes its mutually exclusive delay and activation-recall settings in the same category.
    /// </summary>
    /// <param name="root">Payload drawer root containing the return-transition category.</param>
    private static void ValidateReturnTransitionOrder(VisualElement root)
    {
        VisualElement transitionContainer = root.Q<VisualElement>(PowerUpReturningProjectilesPayloadDrawerUtility.ReturnTransitionContainerName);
        VisualElement delayContainer = root.Q<VisualElement>(PowerUpReturningProjectilesPayloadDrawerUtility.AutomaticReturnDelayContainerName);
        VisualElement recallContainer = root.Q<VisualElement>(PowerUpReturningProjectilesPayloadDrawerUtility.ActivationRecallOptionsContainerName);

        if (transitionContainer == null ||
            delayContainer == null ||
            recallContainer == null ||
            transitionContainer.IndexOf(delayContainer) <= 0 ||
            transitionContainer.IndexOf(recallContainer) <= transitionContainer.IndexOf(delayContainer))
        {
            throw new InvalidOperationException("Return Start Mode did not precede its contextual transition settings in one category.");
        }
    }

    /// <summary>
    /// Verifies the three contextual containers emitted by the Returning Projectiles payload drawer.
    /// </summary>
    /// <param name="root">Payload drawer root containing named contextual containers.</param>
    /// <param name="expectedDelayDisplay">Expected automatic-delay container display.</param>
    /// <param name="expectedRecallDisplay">Expected activation-recall container display.</param>
    /// <param name="expectedResourceDisplay">Expected Resource Gate repayment container display.</param>
    /// <param name="failureMessage">Exception message used when any container is missing or has the wrong display.</param>
    private static void ValidateContainerDisplay(VisualElement root,
                                                 DisplayStyle expectedDelayDisplay,
                                                 DisplayStyle expectedRecallDisplay,
                                                 DisplayStyle expectedResourceDisplay,
                                                 string failureMessage)
    {
        VisualElement delayContainer = root.Q<VisualElement>(PowerUpReturningProjectilesPayloadDrawerUtility.AutomaticReturnDelayContainerName);
        VisualElement recallContainer = root.Q<VisualElement>(PowerUpReturningProjectilesPayloadDrawerUtility.ActivationRecallOptionsContainerName);
        VisualElement resourceContainer = root.Q<VisualElement>(PowerUpReturningProjectilesPayloadDrawerUtility.ActivationRecallResourceGateContainerName);

        if (delayContainer == null ||
            recallContainer == null ||
            resourceContainer == null ||
            delayContainer.style.display.value != expectedDelayDisplay ||
            recallContainer.style.display.value != expectedRecallDisplay ||
            resourceContainer.style.display.value != expectedResourceDisplay)
        {
            throw new InvalidOperationException(failureMessage);
        }
    }
    #endregion

    #endregion
}
