Shader "Hidden/BombasticBloodstreamBrigade/PlayerImpactFrame"
{
    Properties
    {
        _BlitTexture ("Source Texture", 2D) = "white" {}
        _ImpactBlend ("Impact Blend", Range(0, 1)) = 0
        _OverlayIntensity ("Overlay Intensity", Range(0, 1)) = 1
        _FilterTint ("Filter Tint", Color) = (0.96, 0.78, 0.55, 0.45)
        _DesaturationAmount ("Desaturation Amount", Range(0, 1)) = 0.65
        _VignetteIntensity ("Screen Border Vignette Intensity", Range(0, 1)) = 0.55
        _VignetteSoftness ("Screen Border Vignette Softness", Range(0, 1)) = 0.6
        _VignetteExtent ("Screen Border Vignette Extent", Range(0, 1)) = 0.35
        _VignetteTint ("Screen Border Vignette Tint", Color) = (0, 0, 0, 1)
        _RadialVignetteIntensity ("Radial Vignette Intensity", Range(0, 1)) = 0
        _RadialVignetteRadius ("Radial Vignette Radius", Range(0, 1)) = 0.55
        _RadialVignetteSoftness ("Radial Vignette Softness", Range(0.001, 1)) = 0.12
        _RadialVignetteTint ("Radial Vignette Tint", Color) = (0.1, 0, 0.2, 0.8)
        _ChromaticAberration ("Chromatic Aberration", Float) = 0.012
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.18
        _ScanlineFrequency ("Scanline Frequency", Float) = 320
        _FlashIntensity ("Flash Intensity", Range(0, 1)) = 0.35
        _RadialDistortion ("Radial Distortion", Range(0, 1)) = 0.22
        _ShockwaveIntensity ("Shockwave Intensity", Range(0, 1)) = 0.35
        _ShockwaveRadius ("Shockwave Radius", Range(0, 1)) = 0.65
        _ShockwaveThickness ("Shockwave Thickness", Range(0.001, 1)) = 0.12
        _ZoomPunchIntensity ("Zoom Punch Intensity", Range(0, 1)) = 0.18
        _InvertIntensity ("Invert Intensity", Range(0, 1)) = 0
        _PosterizeIntensity ("Posterize Intensity", Range(0, 1)) = 0
        _PosterizeSteps ("Posterize Steps", Float) = 6
        _EdgeInkIntensity ("Edge Ink Intensity", Range(0, 1)) = 0.2
        _ScreenTearIntensity ("Screen Tear Intensity", Range(0, 1)) = 0
        _ScreenTearFrequency ("Screen Tear Frequency", Float) = 24
        _PaletteFlashIntensity ("Palette Flash Intensity", Range(0, 1)) = 0.25
        _PaletteFlashTint ("Palette Flash Tint", Color) = (1, 0.9, 0.45, 0.7)
        _LifetimeProgress ("Lifetime Progress", Range(0, 1)) = 0
        _EffectCenter ("Effect Center", Vector) = (0.5, 0.5, 0, 0)
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
                float _VignetteExtent;
                float4 _VignetteTint;
                float _RadialVignetteIntensity;
                float _RadialVignetteRadius;
                float _RadialVignetteSoftness;
                float4 _RadialVignetteTint;
                float _ChromaticAberration;
                float _ScanlineIntensity;
                float _ScanlineFrequency;
                float _FlashIntensity;
                float _RadialDistortion;
                float _ShockwaveIntensity;
                float _ShockwaveRadius;
                float _ShockwaveThickness;
                float _ZoomPunchIntensity;
                float _InvertIntensity;
                float _PosterizeIntensity;
                float _PosterizeSteps;
                float _EdgeInkIntensity;
                float _ScreenTearIntensity;
                float _ScreenTearFrequency;
                float _PaletteFlashIntensity;
                float4 _PaletteFlashTint;
                float _LifetimeProgress;
                float4 _EffectCenter;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float blend = saturate(_ImpactBlend) * saturate(_OverlayIntensity);
                float progress = saturate(_LifetimeProgress);
                float pulse = saturate(1.0 - progress);
                float2 effectCenter = saturate(_EffectCenter.xy);
                float2 screenUv = input.texcoord.xy;
                float tearWave = sin((screenUv.y * max(1.0, _ScreenTearFrequency) + progress * 6.0) * 6.2831853);
                float tearBand = smoothstep(0.68, 1.0, abs(tearWave));
                screenUv.x += tearWave * tearBand * saturate(_ScreenTearIntensity) * blend * pulse * 0.012;

                float zoomPunch = saturate(_ZoomPunchIntensity) * blend * pulse * 0.08;
                screenUv = lerp(screenUv, effectCenter + (screenUv - effectCenter) * (1.0 - zoomPunch), saturate(_ZoomPunchIntensity));

                float2 centeredUv = screenUv - effectCenter;
                float aspect = _ScreenParams.x / max(1.0, _ScreenParams.y);
                float2 aspectCenteredUv = centeredUv * float2(aspect, 1.0);
                float radialDistance = saturate(length(aspectCenteredUv) * 1.41421356);
                float radialMask = saturate(1.0 - radialDistance);
                float sourceDistance = length(centeredUv);
                float2 radialDirection = sourceDistance > 0.0001 ? centeredUv / sourceDistance : float2(0.0, 0.0);
                float shockwaveRadius = saturate(_ShockwaveRadius) * smoothstep(0.0, 1.0, progress);
                float shockwaveThickness = max(0.001, _ShockwaveThickness);
                float shockwaveDistance = abs(radialDistance - shockwaveRadius);
                float shockwaveRing = saturate(1.0 - smoothstep(0.0, shockwaveThickness, shockwaveDistance));
                float distortion = _RadialDistortion * blend * radialMask * radialMask * 0.08;
                distortion += shockwaveRing * saturate(_ShockwaveIntensity) * blend * pulse * 0.04;
                float2 warpedUv = saturate(screenUv + radialDirection * distortion);
                float chromaOffset = max(0.0, _ChromaticAberration) * blend * (1.0 + pulse * 1.5);
                float2 chromaDirection = radialDirection * chromaOffset;
                float3 color;
                color.r = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(warpedUv + chromaDirection), _BlitMipLevel).r;
                color.g = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, warpedUv, _BlitMipLevel).g;
                color.b = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(warpedUv - chromaDirection), _BlitMipLevel).b;
                float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
                float2 texel = _BlitTexture_TexelSize.xy;
                float neighborLuminance = 0.0;
                neighborLuminance += dot(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(warpedUv + float2(texel.x, 0.0)), _BlitMipLevel).rgb, float3(0.2126, 0.7152, 0.0722));
                neighborLuminance += dot(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(warpedUv - float2(texel.x, 0.0)), _BlitMipLevel).rgb, float3(0.2126, 0.7152, 0.0722));
                neighborLuminance += dot(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(warpedUv + float2(0.0, texel.y)), _BlitMipLevel).rgb, float3(0.2126, 0.7152, 0.0722));
                neighborLuminance += dot(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(warpedUv - float2(0.0, texel.y)), _BlitMipLevel).rgb, float3(0.2126, 0.7152, 0.0722));
                float edgeInk = saturate(abs(luminance - neighborLuminance * 0.25) * 6.0 * saturate(_EdgeInkIntensity) * blend);
                color *= 1.0 - edgeInk;
                color = lerp(color, luminance.xxx, saturate(_DesaturationAmount) * blend);
                color = lerp(color, _FilterTint.rgb, saturate(_FilterTint.a) * blend);
                float3 invertedColor = 1.0 - color;
                color = lerp(color, invertedColor, saturate(_InvertIntensity) * blend);
                float posterizeSteps = max(2.0, round(_PosterizeSteps));
                float3 posterizedColor = floor(saturate(color) * (posterizeSteps - 1.0) + 0.5) / max(1.0, posterizeSteps - 1.0);
                color = lerp(color, posterizedColor, saturate(_PosterizeIntensity) * blend);

                float2 borderUv = saturate(input.texcoord.xy);
                float borderDistance = min(min(borderUv.x, 1.0 - borderUv.x), min(borderUv.y, 1.0 - borderUv.y));
                float borderExtent = max(0.0001, saturate(_VignetteExtent) * 0.5);
                float borderFeather = max(0.0001, borderExtent * saturate(_VignetteSoftness));
                float borderVignette = 1.0 - smoothstep(max(0.0, borderExtent - borderFeather), borderExtent, borderDistance);
                float borderVignetteBlend = borderVignette * saturate(_VignetteIntensity) * saturate(_VignetteTint.a) * blend;
                color = lerp(color, _VignetteTint.rgb, borderVignetteBlend);

                float radialVignetteDistance = abs(radialDistance - saturate(_RadialVignetteRadius));
                float radialVignetteRing = 1.0 - smoothstep(0.0, max(0.001, _RadialVignetteSoftness), radialVignetteDistance);
                float radialVignetteBlend = radialVignetteRing * saturate(_RadialVignetteIntensity) * saturate(_RadialVignetteTint.a) * blend;
                color = lerp(color, _RadialVignetteTint.rgb, radialVignetteBlend);

                float scanline = sin(screenUv.y * max(1.0, _ScanlineFrequency) * 6.2831853);
                float scanlineMask = 0.5 + scanline * 0.5;
                color *= 1.0 - scanlineMask * saturate(_ScanlineIntensity) * blend * 0.35;
                color += saturate(_FlashIntensity) * blend * radialMask;
                color += shockwaveRing * saturate(_ShockwaveIntensity) * blend * _FilterTint.rgb * 0.2;
                color = lerp(color, _PaletteFlashTint.rgb, saturate(_PaletteFlashIntensity) * saturate(_PaletteFlashTint.a) * blend * pulse);
                return half4(saturate(color), 1.0);
            }
            ENDHLSL
        }
    }
}
