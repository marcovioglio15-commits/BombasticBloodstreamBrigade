Shader "Custom/UI/SceneFadeGradient"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Graphic Tint", Color) = (1, 1, 1, 1)
        _FadeProgress("Fade Progress", Range(0, 1)) = 0
        _FadeMode("Fade Mode", Float) = 1
        _FadeDirection("Fade Direction", Float) = 0
        _EdgeSoftness("Directional Edge Softness", Range(0.001, 0.5)) = 0.16
        _NoiseStrength("Directional Noise Strength", Range(0, 0.25)) = 0.035
        _NoiseScale("Directional Noise Scale", Range(0.25, 24)) = 5.5
        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _ColorMask("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 worldPosition : TEXCOORD0;
                float2 texcoord : TEXCOORD1;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            fixed4 _Color;
            float _FadeProgress;
            float _FadeMode;
            float _FadeDirection;
            float _EdgeSoftness;
            float _NoiseStrength;
            float _NoiseScale;

            /// <summary>
            /// Converts UI vertices to clip space while retaining local coordinates for clipping.
            /// </summary>
            /// <param name="input">UI vertex emitted by the authored full-screen Image.</param>
            /// <returns>Interpolated shader data for one rasterized fragment.</returns>
            v2f Vert(appdata_t input)
            {
                v2f output;
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            /// <summary>
            /// Produces stable value noise without sampling an additional texture.
            /// </summary>
            /// <param name="coordinate">Aspect-corrected procedural noise coordinate.</param>
            /// <returns>Noise value in the 0..1 range.</returns>
            float ValueNoise(float2 coordinate)
            {
                float2 cell = floor(coordinate);
                float2 local = frac(coordinate);
                float2 interpolation = local * local * (3.0 - 2.0 * local);
                float4 corners = frac(sin(float4(dot(cell, float2(127.1, 311.7)),
                                                  dot(cell + float2(1.0, 0.0), float2(127.1, 311.7)),
                                                  dot(cell + float2(0.0, 1.0), float2(127.1, 311.7)),
                                                  dot(cell + 1.0, float2(127.1, 311.7)))) * 43758.5453);
                return lerp(lerp(corners.x, corners.y, interpolation.x),
                            lerp(corners.z, corners.w, interpolation.x),
                            interpolation.y);
            }

            /// <summary>
            /// Combines three low-cost noise octaves into a soft organic transition boundary.
            /// </summary>
            /// <param name="coordinate">Base procedural coordinate.</param>
            /// <returns>Normalized fractal noise in the 0..1 range.</returns>
            float FractalNoise(float2 coordinate)
            {
                float noise = ValueNoise(coordinate) * 0.5714286;
                noise += ValueNoise(coordinate * 2.03 + 13.17) * 0.2857143;
                noise += ValueNoise(coordinate * 4.01 + 37.91) * 0.1428571;
                return noise;
            }

            /// <summary>
            /// Resolves the normalized screen axis selected by the authored fade direction.
            /// </summary>
            /// <param name="uv">Full-screen Image UV coordinate.</param>
            /// <returns>Directional coordinate increasing from the covered origin.</returns>
            float ResolveDirectionalCoordinate(float2 uv)
            {
                if (_FadeDirection < 0.5)
                    return uv.x;

                if (_FadeDirection < 1.5)
                    return 1.0 - uv.x;

                if (_FadeDirection < 2.5)
                    return uv.y;

                return 1.0 - uv.y;
            }

            /// <summary>
            /// Shades uniform opacity or an aspect-corrected noisy directional gradient.
            /// </summary>
            /// <param name="input">Interpolated UI fragment data.</param>
            /// <returns>Premultiplied fade color and coverage.</returns>
            fixed4 Frag(v2f input) : SV_Target
            {
                fixed4 sampled = (tex2D(_MainTex, input.texcoord) + _TextureSampleAdd) * input.color;
                float progress = saturate(_FadeProgress);
                float softness = max(0.001, _EdgeSoftness);
                float aspectRatio = _ScreenParams.x / max(1.0, _ScreenParams.y);
                float2 noiseCoordinate = input.texcoord * float2(aspectRatio, 1.0) * max(0.25, _NoiseScale);
                float distortion = (FractalNoise(noiseCoordinate) - 0.5) * 2.0 * max(0.0, _NoiseStrength);
                float directionalCoordinate = ResolveDirectionalCoordinate(input.texcoord) + distortion;
                float boundary = lerp(-softness - _NoiseStrength,
                                      1.0 + softness + _NoiseStrength,
                                      progress);
                float directionalCoverage = 1.0 - smoothstep(boundary - softness,
                                                             boundary + softness,
                                                             directionalCoordinate);
                float coverage = lerp(progress,
                                      directionalCoverage,
                                      step(0.5, _FadeMode));
                float alpha = sampled.a * coverage;

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                return fixed4(sampled.rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
