using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Verifies that preauthored image button content becomes visible before selection and receives pulse motion.
/// </summary>
[InitializeOnLoad]
public static class GameHudImageButtonPlayModeSmokeTest
{
    #region Constants
    private const string ActiveKey = "NashCore.GameHudImageButtonPlayModeSmokeTest.Active";
    private const string EnteredPlayKey = "NashCore.GameHudImageButtonPlayModeSmokeTest.EnteredPlay";
    private const string FailureKey = "NashCore.GameHudImageButtonPlayModeSmokeTest.Failure";
    private const string StartTicksKey = "NashCore.GameHudImageButtonPlayModeSmokeTest.StartTicks";
    private const string SamplingKey = "NashCore.GameHudImageButtonPlayModeSmokeTest.Sampling";
    private const double TimeoutSeconds = 120d;
    private const float PulseSamplingSeconds = 0.65f;
    private const float MinimumPulseScaleDelta = 0.005f;
    #endregion

    #region Fields
    private static Image targetImage;
    private static Vector3 baselineScale;
    private static float pulseSamplingStartTime;
    private static float maximumScaleDelta;
    #endregion

    #region Constructors
    /// <summary>
    /// Registers Play Mode and update callbacks after editor domain reloads.
    /// </summary>
    static GameHudImageButtonPlayModeSmokeTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }
    #endregion

    #region Methods

    #region Entry Point
    /// <summary>
    /// Starts the bootstrap and validates the Main Menu image-content initialization and pulse animation.
    /// </summary>
    //[MenuItem("Tools/Game/HUD/Run Image Button Play Mode Smoke Test")]
    public static void Run()
    {
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(EnteredPlayKey, false);
        SessionState.SetBool(SamplingKey, false);
        SessionState.SetString(FailureKey, string.Empty);
        SessionState.SetString(StartTicksKey, DateTime.UtcNow.Ticks.ToString());
        GameSceneManagementPlayModeSceneGuard.ClearOneShotBypass();
        EditorSceneManager.OpenScene(GameSceneManagementProjectSetupUtility.BootstrapScenePath,
                                     OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
    #endregion

    #region Callbacks
    /// <summary>
    /// Records Play Mode entry so edit-mode completion cannot race startup.
    /// </summary>
    /// <param name="state">Current editor Play Mode transition.</param>
    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false))
            return;

        if (state == PlayModeStateChange.EnteredPlayMode)
            SessionState.SetBool(EnteredPlayKey, true);
    }

    /// <summary>
    /// Waits for the authored Play button, samples its pulse, and completes after returning to Edit Mode.
    /// </summary>
    private static void Update()
    {
        if (!SessionState.GetBool(ActiveKey, false))
            return;

        if (EditorApplication.isPlaying)
        {
            UpdatePlayModeValidation();
            return;
        }

        if (!EditorApplication.isPlayingOrWillChangePlaymode &&
            SessionState.GetBool(EnteredPlayKey, false))
        {
            string failure = SessionState.GetString(FailureKey, string.Empty);
            Finish(string.IsNullOrWhiteSpace(failure), failure);
        }
    }
    #endregion

    #region Validation Methods
    /// <summary>
    /// Resolves the idle image once, then samples the selected pulse until its scale delta is measurable.
    /// </summary>
    private static void UpdatePlayModeValidation()
    {
        if (ResolveElapsedSeconds() >= TimeoutSeconds)
        {
            FailAndExitPlayMode("The Main Menu image button did not complete validation before timeout.");
            return;
        }

        if (!SessionState.GetBool(SamplingKey, false))
        {
            if (!TryBeginPulseSampling())
                return;

            SessionState.SetBool(SamplingKey, true);
            return;
        }

        if (targetImage == null)
        {
            FailAndExitPlayMode("The sampled Play button image was destroyed during pulse validation.");
            return;
        }

        maximumScaleDelta = Mathf.Max(maximumScaleDelta,
                                      Vector3.Distance(targetImage.rectTransform.localScale, baselineScale));

        if (Time.realtimeSinceStartup - pulseSamplingStartTime < PulseSamplingSeconds)
            return;

        if (maximumScaleDelta < MinimumPulseScaleDelta)
        {
            FailAndExitPlayMode("The Settings button image remained static while the Content Only pulse was active.");
            return;
        }

        EditorApplication.isPlaying = false;
    }

    /// <summary>
    /// Finds the idle Settings button, verifies visibility, and selects it to start pulse sampling.
    /// </summary>
    /// <returns>True when a valid preauthored image target was found and sampling started.</returns>
    private static bool TryBeginPulseSampling()
    {
        MenuSelectableHoverRelay[] relays =
            UnityEngine.Object.FindObjectsByType<MenuSelectableHoverRelay>(FindObjectsInactive.Exclude,
                                                                           FindObjectsSortMode.None);

        for (int relayIndex = 0; relayIndex < relays.Length; relayIndex++)
        {
            MenuSelectableHoverRelay relay = relays[relayIndex];

            if (relay == null || relay.gameObject.name != "SettingsButton")
                continue;

            Transform imageTransform = relay.transform.Find("ImageContent");
            Image image = imageTransform != null ? imageTransform.GetComponent<Image>() : null;

            if (image == null || !image.enabled || image.sprite == null || image.color.a <= 0f)
            {
                FailAndExitPlayMode(
                    "The Settings button image was not visible before the smoke test applied selection.");
                return false;
            }

            relay.OnDeselect(null);
            targetImage = image;
            baselineScale = image.rectTransform.localScale;
            maximumScaleDelta = 0f;
            pulseSamplingStartTime = Time.realtimeSinceStartup;
            relay.OnSelect(null);
            return true;
        }

        return false;
    }
    #endregion

    #region Completion Methods
    /// <summary>
    /// Stores one runtime failure and requests a controlled return to Edit Mode.
    /// </summary>
    /// <param name="failure">Failure detail reported after Play Mode exits.</param>
    private static void FailAndExitPlayMode(string failure)
    {
        SessionState.SetString(FailureKey, failure);
        EditorApplication.isPlaying = false;
    }

    /// <summary>
    /// Resolves elapsed wall-clock seconds across editor domain reloads.
    /// </summary>
    /// <returns>Elapsed seconds since the smoke test started.</returns>
    private static double ResolveElapsedSeconds()
    {
        string startTicksText = SessionState.GetString(StartTicksKey, "0");

        if (!long.TryParse(startTicksText, out long startTicks) || startTicks <= 0)
            return TimeoutSeconds;

        return TimeSpan.FromTicks(DateTime.UtcNow.Ticks - startTicks).TotalSeconds;
    }

    /// <summary>
    /// Clears persistent smoke state, reports the result, and exits the batch editor.
    /// </summary>
    /// <param name="passed">True when idle visibility and image pulse validation completed.</param>
    /// <param name="failure">Failure description when validation did not complete.</param>
    private static void Finish(bool passed, string failure)
    {
        SessionState.SetBool(ActiveKey, false);
        SessionState.SetBool(EnteredPlayKey, false);
        SessionState.SetBool(SamplingKey, false);
        SessionState.SetString(FailureKey, string.Empty);
        SessionState.SetString(StartTicksKey, string.Empty);
        targetImage = null;

        if (passed)
            Debug.Log("[GameHudImageButtonPlayModeSmokeTest] Idle image visibility and Content Only pulse passed.");
        else
            Debug.LogError("[GameHudImageButtonPlayModeSmokeTest] Failed: " + failure);

        EditorApplication.Exit(passed ? 0 : 1);
    }
    #endregion

    #endregion
}
