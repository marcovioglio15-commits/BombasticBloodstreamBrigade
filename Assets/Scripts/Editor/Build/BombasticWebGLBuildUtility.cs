using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

internal static class BombasticWebGLBuildUtility
{
    private const string BootstrapScenePath = "Assets/Scenes/Testing/Main Scenes/Bootstrap/SCN_Bootstrap.unity";
    private const string ReleaseProfilePath = "Assets/Settings/Build Profiles/Bombastic Bloodstream Brigade Prototype WebGL Release.asset";
    private const string DevelopmentProfilePath = "Assets/Settings/Build Profiles/Bombastic Bloodstream Brigade Prototype WebGL Development.asset";
    private const string ReleaseBuildPath = "Builds/WebGL/BombasticBloodstreamBrigade_Prototype_WebGLBuild";
    private const string DevelopmentBuildPath = "Builds/WebGL/BombasticBloodstreamBrigade_Prototype_WebGLBuild_Development";
    private const string UrpGlobalSettingsPath = "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset";
    private const string MobileUrpAssetPath = "Assets/Settings/Mobile_RPAsset.asset";
    private const string MobileRendererPath = "Assets/Settings/Mobile_Renderer.asset";
    private const string GraphicsSettingsPath = "ProjectSettings/GraphicsSettings.asset";
    private const string WebGlTemplatePath = "Assets/WebGLTemplates/BombasticItch/index.html";
    private const string WebGlTemplateName = "PROJECT:BombasticItch";
    private const string FmodDefine = "NASHCORE_FMOD";
    private const string UrpCompatibilityDefine = "URP_COMPATIBILITY_MODE";
    private const int DefaultWebWidth = 1280;
    private const int DefaultWebHeight = 720;
    private const int InitialMemoryMegabytes = 256;
    private const int MaximumMemoryMegabytes = 2048;
    private const int MemoryGrowthCapMegabytes = 256;
    private static readonly string[] WebGlCompressedTexturePaths =
    {
        "Assets/3D/VFX/VFX_BulletTests/vfx_fireGIF_spritesheet3.png",
        "Assets/3D/VFX/VFX_BulletTests/vfx_fireGIF_spritesheet.png",
        "Assets/3D/Textures/textures-enemiesFaces/T_EnemiesFaces_Flipbook_02.png",
        "Assets/3D/Textures/textures-enemiesFaces/T_EnemiesFaces_Flipbook_Damage.png",
        "Assets/3D/Textures/textures-enemiesFaces/T_EnemiesFaces_Flipbook_Attack.png"
    };

    [MenuItem("WebGL/Configure Prototype Settings")]
    public static void ConfigurePrototypeSettings()
    {
        PlayerSettings.defaultWebScreenWidth = DefaultWebWidth;
        PlayerSettings.defaultWebScreenHeight = DefaultWebHeight;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);

        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.initialMemorySize = InitialMemoryMegabytes;
        PlayerSettings.WebGL.maximumMemorySize = MaximumMemoryMegabytes;
        PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
        PlayerSettings.WebGL.memoryGeometricGrowthCap = MemoryGrowthCapMegabytes;
        PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
        PlayerSettings.WebGL.nameFilesAsHashes = false;
        PlayerSettings.WebGL.showDiagnostics = false;
        PlayerSettings.WebGL.template = WebGlTemplateName;
        PlayerSettings.WebGL.threadsSupport = false;
        PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
        PlayerSettings.WebGL.wasm2023 = false;
        PlayerSettings.WebGL.webAssemblyBigInt = false;
        PlayerSettings.WebGL.webAssemblyTable = false;
        PlayerSettings.WebGL.wasmArithmeticExceptions = WebGLWasmArithmeticExceptions.Ignore;
        EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.DXT;

        ConfigureWebGlTextureOverrides();
        EnsureDefine(NamedBuildTarget.WebGL, FmodDefine);
        RemoveDefine(NamedBuildTarget.WebGL, UrpCompatibilityDefine);
        AssetDatabase.SaveAssets();
        Debug.Log("[BombasticWebGL] Applied WebGL prototype settings.");
    }

    [MenuItem("WebGL/Validate Prototype Readiness")]
    public static void ValidatePrototypeReadiness()
    {
        ConfigurePrototypeSettings();

        List<string> failures = new List<string>();
        ValidateBuildTarget(failures);
        ValidateScenes(failures);
        ValidateProfiles(failures);
        ValidatePlayerSettings(failures);
        ValidateUrpWebGL(failures);
        ValidateFmodWebGL(failures);
        ValidateWebGlPlugins(failures);

        if (failures.Count > 0)
        {
            string message = "[BombasticWebGL] Validation failed:\n- " + string.Join("\n- ", failures);
            Debug.LogError(message);
            throw new InvalidOperationException(message);
        }

        Debug.Log("BOMBASTIC_WEBGL_VALIDATE_PASS");
    }

    [MenuItem("WebGL/Build Prototype Release")]
    public static void BuildPrototypeRelease()
    {
        ConfigurePrototypeSettings();
        BuildWebGL(ResolveOutputPath(ReleaseBuildPath), false);
    }

    [MenuItem("WebGL/Build Prototype Development")]
    public static void BuildPrototypeDevelopment()
    {
        ConfigurePrototypeSettings();
        BuildWebGL(ResolveOutputPath(DevelopmentBuildPath), true);
    }

    private static void BuildWebGL(string outputPath, bool development)
    {
        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            throw new InvalidOperationException("[BombasticWebGL] Failed to switch active build target to WebGL.");
        }

        string[] scenes = GetEnabledScenePaths();
        if (scenes.Length == 0)
        {
            throw new InvalidOperationException("[BombasticWebGL] No enabled scenes are configured for build.");
        }

        if (Directory.Exists(outputPath))
        {
            Directory.Delete(outputPath, true);
        }

        Directory.CreateDirectory(outputPath);

        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = scenes;
        options.locationPathName = outputPath;
        options.target = BuildTarget.WebGL;
        options.options = development ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.None;

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        string buildKind = development ? "development" : "release";

        if (summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException("[BombasticWebGL] WebGL " + buildKind + " build failed with result " +
                                                summary.result + ".");
        }

        Debug.Log("BOMBASTIC_WEBGL_BUILD_PASS kind=" + buildKind + " path=" + outputPath +
                  " sizeBytes=" + summary.totalSize);
    }

    private static void ValidateBuildTarget(List<string> failures)
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            failures.Add("Unity WebGL module is not installed or not available to this Editor.");
        }
    }

    private static void ValidateScenes(List<string> failures)
    {
        if (!File.Exists(BootstrapScenePath))
        {
            failures.Add("Bootstrap scene is missing: " + BootstrapScenePath);
        }

        if (!Contains(GetEnabledScenePaths(), BootstrapScenePath))
        {
            failures.Add("Bootstrap scene is not enabled in EditorBuildSettings: " + BootstrapScenePath);
        }
    }

    private static void ValidateProfiles(List<string> failures)
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ReleaseProfilePath) == null)
        {
            failures.Add("Missing WebGL release build profile asset: " + ReleaseProfilePath);
        }

        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(DevelopmentProfilePath) == null)
        {
            failures.Add("Missing WebGL development build profile asset: " + DevelopmentProfilePath);
        }
    }

    private static void ValidatePlayerSettings(List<string> failures)
    {
        if (PlayerSettings.defaultWebScreenWidth != DefaultWebWidth ||
            PlayerSettings.defaultWebScreenHeight != DefaultWebHeight)
        {
            failures.Add("Web default resolution must be " + DefaultWebWidth + "x" + DefaultWebHeight + ".");
        }

        if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.WebGL) != ScriptingImplementation.IL2CPP)
        {
            failures.Add("WebGL scripting backend must be IL2CPP.");
        }

        if (PlayerSettings.WebGL.compressionFormat != WebGLCompressionFormat.Gzip)
        {
            failures.Add("WebGL compression must be Gzip for itch.io CDN compatibility and fast iteration.");
        }

        if (!PlayerSettings.WebGL.dataCaching)
        {
            failures.Add("WebGL Data Caching must be enabled.");
        }

        if (PlayerSettings.WebGL.decompressionFallback)
        {
            failures.Add("WebGL Decompression Fallback must stay disabled for smaller loader and native CDN decompression.");
        }

        if (PlayerSettings.WebGL.threadsSupport)
        {
            failures.Add("WebGL threads support must stay disabled; itch iframe hosting cannot be assumed to provide thread headers.");
        }

        if (!string.Equals(PlayerSettings.WebGL.template, WebGlTemplateName, StringComparison.Ordinal))
        {
            failures.Add("WebGL template must be " + WebGlTemplateName + " so itch fullscreen and WebAudio errors are guarded.");
        }

        if (!File.Exists(WebGlTemplatePath))
        {
            failures.Add("Custom itch WebGL template is missing: " + WebGlTemplatePath);
        }

        if (EditorUserBuildSettings.webGLBuildSubtarget != WebGLTextureSubtarget.DXT)
        {
            failures.Add("WebGL texture subtarget must be DXT to avoid shipping the large VFX and face atlases uncompressed.");
        }

        ValidateWebGlTextureOverrides(failures);

        if (PlayerSettings.WebGL.initialMemorySize < InitialMemoryMegabytes)
        {
            failures.Add("WebGL initial memory is lower than " + InitialMemoryMegabytes + " MB.");
        }

        if (PlayerSettings.WebGL.maximumMemorySize < MaximumMemoryMegabytes)
        {
            failures.Add("WebGL maximum memory is lower than " + MaximumMemoryMegabytes + " MB.");
        }
    }

    private static void ConfigureWebGlTextureOverrides()
    {
        for (int pathIndex = 0; pathIndex < WebGlCompressedTexturePaths.Length; pathIndex++)
        {
            string texturePath = WebGlCompressedTexturePaths[pathIndex];
            TextureImporter textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;

            if (textureImporter == null)
                continue;

            TextureImporterPlatformSettings settings = textureImporter.GetPlatformTextureSettings("WebGL");

            if (settings.overridden &&
                settings.maxTextureSize == 2048 &&
                settings.format == TextureImporterFormat.DXT5 &&
                settings.textureCompression == TextureImporterCompression.Compressed &&
                !settings.crunchedCompression)
            {
                continue;
            }

            settings.name = "WebGL";
            settings.overridden = true;
            settings.maxTextureSize = 2048;
            settings.resizeAlgorithm = TextureResizeAlgorithm.Mitchell;
            settings.format = TextureImporterFormat.DXT5;
            settings.textureCompression = TextureImporterCompression.Compressed;
            settings.compressionQuality = 50;
            settings.crunchedCompression = false;
            textureImporter.SetPlatformTextureSettings(settings);
            textureImporter.SaveAndReimport();
        }
    }

    private static void ValidateWebGlTextureOverrides(List<string> failures)
    {
        for (int pathIndex = 0; pathIndex < WebGlCompressedTexturePaths.Length; pathIndex++)
        {
            string texturePath = WebGlCompressedTexturePaths[pathIndex];
            TextureImporter textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;

            if (textureImporter == null)
            {
                failures.Add("WebGL compressed texture is missing or not importable: " + texturePath);
                continue;
            }

            TextureImporterPlatformSettings settings = textureImporter.GetPlatformTextureSettings("WebGL");

            if (!settings.overridden ||
                settings.maxTextureSize != 2048 ||
                settings.format != TextureImporterFormat.DXT5 ||
                settings.textureCompression != TextureImporterCompression.Compressed ||
                settings.crunchedCompression)
            {
                failures.Add("WebGL texture override must use non-crunched DXT5 at max 2048: " + texturePath);
            }
        }
    }

    private static void ValidateFmodWebGL(List<string> failures)
    {
        if (!DefinesContain(NamedBuildTarget.WebGL, FmodDefine))
        {
            failures.Add("WebGL scripting defines must include " + FmodDefine + ".");
        }

        string fmodLibraryPath = "Assets/Plugins/FMOD/platforms/html5/lib/3.1.39/libfmodstudio.a";
        if (!File.Exists(fmodLibraryPath))
        {
            failures.Add("FMOD WebGL library for Unity 6000 is missing: " + fmodLibraryPath);
        }

        string fmodPlatformPath = "Assets/Plugins/FMOD/platforms/html5/src/PlatformWebGL.cs";
        if (!File.Exists(fmodPlatformPath))
        {
            failures.Add("FMOD WebGL platform implementation is missing: " + fmodPlatformPath);
        }

        string fmodBanksPath = "Assets/BBB_FMOD/Build";
        if (!Directory.Exists(fmodBanksPath))
        {
            failures.Add("FMOD source bank folder is missing: " + fmodBanksPath);
        }

        string fmodSettingsPath = "Assets/Plugins/FMOD/Resources/FMODStudioSettings.asset";

        if (!File.Exists(fmodSettingsPath))
        {
            failures.Add("FMOD settings asset is missing: " + fmodSettingsPath);
            return;
        }

        string fmodSettings = File.ReadAllText(fmodSettingsPath);

        if (!fmodSettings.Contains("BankLoadType: 1") ||
            !fmodSettings.Contains("BanksToLoad:") ||
            !fmodSettings.Contains("- BankSounds.strings") ||
            !fmodSettings.Contains("- BankSounds"))
        {
            failures.Add("FMOD startup banks must preload only BankSounds and its strings bank; BankMusic must remain lazy-loaded.");
        }

        int webGlPlatformIndex = fmodSettings.IndexOf("FMODUnity.PlatformWebGL", StringComparison.Ordinal);
        int defaultPlatformIndex = fmodSettings.IndexOf("FMODUnity.PlatformDefault", StringComparison.Ordinal);

        if (webGlPlatformIndex < 0 || defaultPlatformIndex <= webGlPlatformIndex)
        {
            failures.Add("FMOD WebGL platform settings block could not be resolved.");
            return;
        }

        string webGlSettings = fmodSettings.Substring(webGlPlatformIndex, defaultPlatformIndex - webGlPlatformIndex);
        ValidateFmodWebGlSetting(webGlSettings, "SampleRate:", "Value: 48000", failures);
        ValidateFmodWebGlSetting(webGlSettings, "VirtualChannelCount:", "Value: 128", failures);
        ValidateFmodWebGlSetting(webGlSettings, "RealChannelCount:", "Value: 32", failures);
        ValidateFmodWebGlSetting(webGlSettings, "DSPBufferLength:", "Value: 4096", failures);
        ValidateFmodWebGlSetting(webGlSettings, "DSPBufferCount:", "Value: 4", failures);

        if (!webGlSettings.Contains("OutputTypeName: WEBAUDIO"))
            failures.Add("FMOD WebGL output type must be WEBAUDIO.");
    }

    private static void ValidateFmodWebGlSetting(string webGlSettings,
                                                 string settingName,
                                                 string expectedValue,
                                                 List<string> failures)
    {
        int settingIndex = webGlSettings.IndexOf(settingName, StringComparison.Ordinal);

        if (settingIndex < 0)
        {
            failures.Add("FMOD WebGL setting is missing: " + settingName);
            return;
        }

        int settingBlockLength = Math.Min(160, webGlSettings.Length - settingIndex);
        string settingBlock = webGlSettings.Substring(settingIndex, settingBlockLength);

        if (!settingBlock.Contains(expectedValue) || !settingBlock.Contains("HasValue: 1"))
            failures.Add("FMOD WebGL " + settingName.TrimEnd(':') + " must use " + expectedValue.Replace("Value: ", string.Empty) + ".");
    }

    private static void ValidateWebGlPlugins(List<string> failures)
    {
        ValidateWebGlPlugin("Assets/Plugins/WebGL/BombasticWebGLQuit.jslib",
                            "BombasticWebGLQuit",
                            "WebGL quit bridge",
                            failures);
        ValidateWebGlPlugin("Assets/Plugins/WebGL/BombasticWebGLGamepadRumble.jslib",
                            "BombasticWebGLGamepadSetRumble",
                            "WebGL gamepad rumble bridge",
                            failures);
    }

    private static void ValidateWebGlPlugin(string pluginPath,
                                            string requiredFunction,
                                            string displayName,
                                            List<string> failures)
    {
        if (!File.Exists(pluginPath))
        {
            failures.Add(displayName + " is missing: " + pluginPath);
            return;
        }

        if (!File.ReadAllText(pluginPath).Contains(requiredFunction))
            failures.Add(displayName + " does not export " + requiredFunction + ".");
    }

    private static void ValidateUrpWebGL(List<string> failures)
    {
        if (DefinesContain(NamedBuildTarget.WebGL, UrpCompatibilityDefine))
        {
            failures.Add("WebGL scripting defines must not include deprecated " + UrpCompatibilityDefine + ".");
        }

        if (!File.Exists(UrpGlobalSettingsPath))
        {
            failures.Add("URP global settings asset is missing: " + UrpGlobalSettingsPath);
            return;
        }

        string urpSettings = File.ReadAllText(UrpGlobalSettingsPath);

        if (!urpSettings.Contains("m_EnableRenderCompatibilityMode: 0"))
        {
            failures.Add("URP Compatibility Mode must be disabled for the WebGL renderer path.");
        }

        if (!urpSettings.Contains("m_EnableRenderGraph: 1"))
        {
            failures.Add("URP Render Graph must be enabled for the WebGL renderer path.");
        }

        ValidateSerializedSetting(MobileUrpAssetPath, "m_SupportsHDR: 0", "Mobile URP HDR must be disabled for WebGL.", failures);
        ValidateSerializedSetting(MobileUrpAssetPath, "m_RenderScale: 1", "Mobile URP Render Scale must be 1 for WebGL.", failures);
        ValidateSerializedSetting(MobileRendererPath, "m_UseNativeRenderPass: 0", "Mobile URP Native Render Pass must be disabled for WebGL.", failures);
        ValidateSerializedSetting(GraphicsSettingsPath, "guid: c06d2317c8cda6d49aa62bc6fc1810e9", "WebGL Toon Diffuse fallback shader must be always included.", failures);
        ValidateSerializedSetting(GraphicsSettingsPath, "guid: 1d8626f4f8a44d649d7948db2c5e35a1", "WebGL Toon Hit Flash fallback shader must be always included.", failures);
        ValidateSerializedSetting(GraphicsSettingsPath, "guid: 130cf600495b4c3ca97931ef41f76ba9", "WebGL Toon Blur fallback shader must be always included.", failures);
        ValidateSerializedSetting(GraphicsSettingsPath, "guid: 86a6a7ca39f9418693ca54f0b39be8ad", "WebGL Toon Outline fallback shader must be always included.", failures);
        ValidateSerializedSetting(GraphicsSettingsPath, "guid: 46b23c035ab748698c66aa50c411b82d", "WebGL enemy-face fallback shader must be always included.", failures);
        ValidateSerializedSetting("Assets/3D/Shaders/SH_ToonDiffuse_ECS.shader", "FallBack \"Cel Shader/Toon Diffuse\"", "Toon Diffuse ECS shader must declare its WebGL fallback.", failures);
        ValidateSerializedSetting("Assets/3D/Shaders/SH_ToonDiffuse_ECS_HitFlash.shader", "FallBack \"Cel Shader/Toon Diffuse Hit Flash\"", "Toon Hit Flash ECS shader must declare its WebGL fallback.", failures);
        ValidateSerializedSetting("Assets/3D/Shaders/SH_ToonDiffuse_ECS_Blur.shader", "FallBack \"Cel Shader/Toon Diffuse Blur\"", "Toon Blur ECS shader must declare its WebGL fallback.", failures);
        ValidateSerializedSetting("Assets/Scripts/Core/Hybrid/WebGLEntitiesGraphicsFallbackSystem.cs",
                                  "BombasticBloodstreamBrigade/Toon Outline WebGL",
                                  "Entities Graphics fallback must map outline materials to the WebGL outline shader.",
                                  failures);
        ValidateSerializedSetting("Assets/3D/Shaders/SH_EnemyFacesFlipbook_ECS.shader", "FallBack \"BombasticBloodstreamBrigade/Enemy Faces Flipbook WebGL\"", "Enemy face ECS shader must declare its WebGL fallback.", failures);
        ValidateSerializedSetting(MobileRendererPath, "m_Name: OutlineRenderOpaqueWebGL", "Mobile renderer must contain the opaque WebGL outline pass.", failures);
        ValidateSerializedSetting(MobileRendererPath, "m_Name: OutlineRenderWebGL", "Mobile renderer must contain the transparent WebGL outline pass.", failures);
        ValidateSerializedSetting(MobileRendererPath, "guid: d7cb5dacb2a84780900ad6ccf7425510", "Mobile renderer WebGL outline passes must use the WebGL outline material.", failures);

        string fallbackSystemPath = "Assets/Scripts/Core/Hybrid/WebGLEntitiesGraphicsFallbackSystem.cs";

        if (!File.Exists(fallbackSystemPath))
        {
            failures.Add("WebGL Entities Graphics fallback system is missing: " + fallbackSystemPath);
        }
    }

    private static void ValidateSerializedSetting(string assetPath,
                                                  string requiredSetting,
                                                  string failureMessage,
                                                  List<string> failures)
    {
        if (!File.Exists(assetPath))
        {
            failures.Add("Required WebGL rendering asset is missing: " + assetPath);
            return;
        }

        if (!File.ReadAllText(assetPath).Contains(requiredSetting))
            failures.Add(failureMessage);
    }

    private static string[] GetEnabledScenePaths()
    {
        List<string> scenes = new List<string>();
        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;

        for (int sceneIndex = 0; sceneIndex < buildScenes.Length; sceneIndex++)
        {
            EditorBuildSettingsScene scene = buildScenes[sceneIndex];
            if (scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
            {
                scenes.Add(scene.path);
            }
        }

        return scenes.ToArray();
    }

    private static void EnsureDefine(NamedBuildTarget target, string define)
    {
        if (DefinesContain(target, define))
            return;

        string defines = PlayerSettings.GetScriptingDefineSymbols(target);
        if (string.IsNullOrWhiteSpace(defines))
        {
            PlayerSettings.SetScriptingDefineSymbols(target, define);
            return;
        }

        PlayerSettings.SetScriptingDefineSymbols(target, defines + ";" + define);
    }

    private static void RemoveDefine(NamedBuildTarget target, string define)
    {
        string defines = PlayerSettings.GetScriptingDefineSymbols(target);

        if (string.IsNullOrWhiteSpace(defines))
            return;

        string[] splitDefines = defines.Split(';');
        List<string> retainedDefines = new List<string>(splitDefines.Length);

        for (int defineIndex = 0; defineIndex < splitDefines.Length; defineIndex++)
        {
            string candidate = splitDefines[defineIndex].Trim();

            if (candidate.Length <= 0 || string.Equals(candidate, define, StringComparison.Ordinal))
                continue;

            retainedDefines.Add(candidate);
        }

        PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", retainedDefines));
    }

    private static bool DefinesContain(NamedBuildTarget target, string define)
    {
        string defines = PlayerSettings.GetScriptingDefineSymbols(target);
        string[] splitDefines = defines.Split(';');

        for (int defineIndex = 0; defineIndex < splitDefines.Length; defineIndex++)
        {
            if (string.Equals(splitDefines[defineIndex].Trim(), define, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool Contains(string[] values, string expectedValue)
    {
        for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
        {
            if (string.Equals(values[valueIndex], expectedValue, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string ResolveOutputPath(string fallbackPath)
    {
        string[] args = Environment.GetCommandLineArgs();

        for (int argIndex = 0; argIndex < args.Length - 1; argIndex++)
        {
            if (string.Equals(args[argIndex], "-webglOutputPath", StringComparison.OrdinalIgnoreCase))
                return args[argIndex + 1];
        }

        return fallbackPath;
    }
}
