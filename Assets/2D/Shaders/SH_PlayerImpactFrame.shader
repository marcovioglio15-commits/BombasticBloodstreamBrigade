Shader "Hidden/NashCore/PlayerImpactFrame"
{
    Properties
    {
        _BlitTexture ("Source Texture", 2D) = "white" {}
        _ImpactBlend ("Impact Blend", Range(0, 1)) = 0
        _OverlayIntensity ("Overlay Intensity", Range(0, 1)) = 1
        _FilterTint ("Filter Tint", Color) = (0.96, 0.78, 0.55, 0.45)
        _DesaturationAmount ("Desaturation Amount", Range(0, 1)) = 0.65
        _VignetteIntensity ("Vignette Intensity", Range(0, 1)) = 0.55
        _VignetteSoftness ("Vignette Softness", Range(0, 1)) = 0.6
        _ChromaticAberration ("Chromatic Aberration", Float) = 0.012
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.18
        _ScanlineFrequency ("Scanline Frequency", Float) = 320
        _FlashIntensity ("Flash Intensity", Range(0, 1)) = 0.35
        _RadialDistortion ("Radial Distortion", Range(0, 1)) = 0.22
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "ImpactFrame"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _ImpactBlend;
                float _OverlayIntensity;
                float4 _FilterTint;
                float _DesaturationAmount;
                float _VignetteIntensity;
                float _VignetteSoftness;
                float _ChromaticAberration;
                float _ScanlineIntensity;
                float _ScanlineFrequency;
                float _FlashIntensity;
                float _RadialDistortion;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float blend = saturate(_ImpactBlend) * saturate(_OverlayIntensity);
                float2 screenUv = input.texcoord.xy;
                float2 centeredUv = screenUv - 0.5;
                float radialDistance = saturate(length(centeredUv) * 1.41421356);
                float radialMask = saturate(1.0 - radialDistance);
                float2 radialDirection = radialDistance > 0.0001 ? centeredUv / max(radialDistance, 0.0001) : float2(0.0, 0.0);
                float distortion = _RadialDistortion * blend * radialMask * radialMask * 0.08;
                float2 warpedUv = saturate(screenUv + radialDirection * distortion);
                float chromaOffset = max(0.0, _ChromaticAberration) * blend;
                float2 chromaDirection = radialDirection * chromaOffset;
                float3 color;
                color.r = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(warpedUv + chromaDirection), _BlitMipLevel).r;
                color.g = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, warpedUv, _BlitMipLevel).g;
                color.b = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(warpedUv - chromaDirection), _BlitMipLevel).b;
                float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
                color = lerp(color, luminance.xxx, saturate(_DesaturationAmount) * blend);
                color = lerp(color, _FilterTint.rgb, saturate(_FilterTint.a) * blend);

                float softness = lerp(0.02, 0.65, saturate(_VignetteSoftness));
                float vignette = smoothstep(1.0 - softness, 1.0, radialDistance);
                color *= 1.0 - vignette * saturate(_VignetteIntensity) * blend;

                float scanline = sin(screenUv.y * max(1.0, _ScanlineFrequency) * 6.2831853);
                float scanlineMask = 0.5 + scanline * 0.5;
                color *= 1.0 - scanlineMask * saturate(_ScanlineIntensity) * blend * 0.35;
                color += saturate(_FlashIntensity) * blend * radialMask;
                return half4(saturate(color), 1.0);
            }
            ENDHLSL
        }
    }
}
