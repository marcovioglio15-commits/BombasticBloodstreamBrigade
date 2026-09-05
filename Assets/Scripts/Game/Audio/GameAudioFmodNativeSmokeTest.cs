#if UNITY_EDITOR
using System;
using System.IO;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

/// <summary>
/// Verifies the shipped FMOD banks and moving global voice anchors using an isolated silent FMOD system.
/// </summary>
public static class GameAudioFmodNativeSmokeTest
{
    #region Methods

    #region Entry Point
    /// <summary>
    /// Loads existing banks read-only and checks player lifecycle events without entering gameplay or modifying FMOD assets.
    /// </summary>
    public static void Run()
    {
        Check(FMOD.Studio.System.create(out FMOD.Studio.System studio), "Create isolated FMOD system");

        try
        {
            Check(studio.getCoreSystem(out FMOD.System core), "Get FMOD core");
            Check(core.setOutput(OUTPUTTYPE.NOSOUND), "Select silent output");
            Check(studio.initialize(64, FMOD.Studio.INITFLAGS.NORMAL, FMOD.INITFLAGS.NORMAL, IntPtr.Zero), "Initialize FMOD");

            // Load exported Unity banks only; this test never writes the FMOD project or rebuilds a bank.
            string[] banks = Directory.GetFiles(Application.streamingAssetsPath, "*.bank");

            for (int index = 0; index < banks.Length; index++)
                Check(studio.loadBankFile(banks[index], LOAD_BANK_FLAGS.NORMAL, out Bank _), banks[index]);

            ValidateMusic(studio, "event:/MUSIC/mus_boss");
            ValidateMusic(studio, "event:/MUSIC/mus_menu");
            ValidateGlobalVoice(studio, "event:/SFX/Voices/NASH_SfxMC_SFX_Misc_Spawn");
            ValidateGlobalVoice(studio, "event:/SFX/Voices/NASH_SfxMC_SFX_Misc_DeathCry_01");
            ValidateGlobalVoice(studio, "event:/SFX/Weapon/NASH_SfxMC_SFX_Misc_DeatJIngle");
            ValidateGlobalVoice(studio, "event:/SFX/Voices/NASH_SfxMC_SFX_Misc_Victory_01");
            UnityEngine.Debug.Log("[GameAudioFmodNativeSmokeTest] Shipped music events and lifecycle voice anchors passed.");
        }
        finally
        {
            GameAudioFmodGlobalVoiceRuntimeUtility.StopAll();
            studio.release();
        }
    }
    #endregion

    #region Assertions
    /// <summary>
    /// Confirms the assigned music path exists as a non-spatialized looping event in the shipped banks.
    /// </summary>
    /// <param name="studio">Isolated FMOD system containing the exported banks.</param>
    /// <param name="path">Configured music event path.</param>
    private static void ValidateMusic(FMOD.Studio.System studio, string path)
    {
        Check(studio.getEvent(path, out EventDescription description), path);
        Check(description.is3D(out bool is3D), path);
        Check(description.isOneshot(out bool isOneShot), path);

        if (is3D || isOneShot)
            throw new InvalidOperationException("Music must remain a global looping event: " + path);
    }

    /// <summary>
    /// Moves the listener snapshot far from spawn and checks that the live 3D event follows it.
    /// </summary>
    /// <param name="studio">Isolated FMOD system.</param>
    /// <param name="path">Player lifecycle voice to verify.</param>
    private static void ValidateGlobalVoice(FMOD.Studio.System studio, string path)
    {
        Check(studio.getEvent(path, out EventDescription description), path);
        Check(description.createInstance(out EventInstance instance), path);

        try
        {
            Check(instance.set3DAttributes(RuntimeUtils.To3DAttributes(Vector3.zero)), "Initial voice anchor");
            Check(instance.start(), "Start lifecycle voice");
            GameAudioFmodGlobalVoiceRuntimeUtility.Track(instance);
            Check(studio.update(), "Submit voice start");
            Check(studio.flushCommands(), "Flush voice start");
            ATTRIBUTES_3D movedListener = RuntimeUtils.To3DAttributes(new Vector3(150f, 20f, -80f));
            GameAudioFmodGlobalVoiceRuntimeUtility.UpdateAnchors(in movedListener);
            Check(studio.flushCommands(), "Flush moved anchor");
            Check(instance.get3DAttributes(out ATTRIBUTES_3D actual), "Read moved voice anchor");

            if (Mathf.Abs(actual.position.x - 150f) > 0.001f || Mathf.Abs(actual.position.z + 80f) > 0.001f)
                throw new InvalidOperationException("Global voice stayed at its spawn position: " + path);
        }
        finally
        {
            instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            instance.release();
            studio.flushCommands();
        }
    }

    /// <summary>
    /// Converts native API failures into deterministic batch-test failures.
    /// </summary>
    /// <param name="result">FMOD result returned by the operation.</param>
    /// <param name="operation">Operation or event being checked.</param>
    private static void Check(RESULT result, string operation)
    {
        if (result != RESULT.OK)
            throw new InvalidOperationException(operation + ": " + result);
    }
    #endregion

    #endregion
}
#endif
