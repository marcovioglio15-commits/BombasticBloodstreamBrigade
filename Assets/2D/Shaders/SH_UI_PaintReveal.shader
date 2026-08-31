Shader "Custom/UI/PaintReveal"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _DepositionTex("Aerosol Deposition Map", 2D) = "gray" {}
        _Color("Graphic Tint", Color) = (1, 1, 1, 1)
        _FadeProgress("Animation Progress", Range(0, 1)) = 0
        [Enum(Uniform,0,DirectionalGradient,1,UniformPaint,2,DirectionalPaint,3)] _FadeMode("Coverage Mode", Float) = 3
        [Enum(LeftToRight,0,RightToLeft,1,BottomToTop,2,TopToBottom,3)] _FadeDirection("Coverage Direction", Float) = 0
        [Enum(Deposit,0,Remove,1)] _PaintOperation("Coverage Operation", Float) = 0
        _EdgeSoftness("Gradient Edge Softness", Range(0.001, 0.5)) = 0.16
        _NoiseStrength("Gradient Edge Variation", Range(0, 0.25)) = 0.035
        _NoiseScale("Gradient Variation Scale", Range(0.25, 24)) = 5.5
        _DepositSoftness("Deposit Edge Softness", Range(0.001, 0.25)) = 0.025
        _DepositVariation("Deposit Time Variation", Range(0, 0.5)) = 0.22
        _DepositScale("Deposit Cluster Scale", Range(0.25, 12)) = 2.4
        _MistStrength("Aerosol Mist Strength", Range(0, 0.25)) = 0.075
        _MistScale("Aerosol Mist Density", Range(1, 96)) = 48
        _AspectRatio("Rendered Rect Aspect Ratio", Float) = 1.777778
        [HideInInspector] _StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask("Color Mask", Float) = 15
        [HideInInspector] [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip", Float) = 0
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
            sampler2D _DepositionTex;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            fixed4 _Color;
            float _FadeProgress;
            float _FadeMode;
            float _FadeDirection;
            float _PaintOperation;
            float _EdgeSoftness;
            float _NoiseStrength;
            float _NoiseScale;
            float _DepositSoftness;
            float _DepositVariation;
            float _DepositScale;
            float _MistStrength;
            float _MistScale;
            float _AspectRatio;

            /// <summary>
            /// Converts UI vertices to clip space while retaining coordinates required by UGUI clipping.
            /// </summary>
            /// <param name="input">Vertex emitted by the preauthored UI graphic.</param>
            /// <returns>Interpolated fragment data.</returns>
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
            /// Produces deterministic value noise for fine aerosol breakup without another texture dependency.
            /// </summary>
            /// <param name="coordinate">Continuous aspect-corrected sample coordinate.</param>
            /// <returns>Smoothed noise in the zero-to-one range.</returns>
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
            /// Resolves the progress and cross axes selected by the authored screen-space direction.
            /// </summary>
            /// <param name="uv">Graphic UV coordinate.</param>
            /// <returns>Progress axis in x and perpendicular axis in y.</returns>
            float2 ResolveDirectionalAxes(float2 uv)
            {
                if (_FadeDirection < 0.5)
                    return uv;

                if (_FadeDirection < 1.5)
                    return float2(1.0 - uv.x, uv.y);

                if (_FadeDirection < 2.5)
                    return float2(uv.y, uv.x);

                return float2(1.0 - uv.y, uv.x);
            }

            /// <summary>
            /// Samples two differently oriented copies of the authored arrival field to suppress visible repetition.
            /// </summary>
            /// <param name="uv">Graphic UV coordinate.</param>
            /// <returns>Clustered normalized pigment arrival value.</returns>
            float SampleDeposition(float2 uv)
            {
                float2 centered = (uv - 0.5) * float2(max(0.01, _AspectRatio), 1.0);
                float scale = max(0.25, _DepositScale);
                float2 primaryCoordinate = centered * scale + float2(0.37, 0.61);
                float2 secondaryCoordinate = float2(-centered.y, centered.x) * scale * 0.53 +
                                             float2(0.13, 0.79);
                float primary = tex2D(_DepositionTex, frac(primaryCoordinate)).r;
                float secondary = tex2D(_DepositionTex, frac(secondaryCoordinate)).r;
                return saturate(lerp(primary, secondary, 0.32));
            }

            /// <summary>
            /// Builds fine arrival jitter and rare early satellite droplets around an active spray front.
            /// </summary>
            /// <param name="uv">Graphic UV coordinate.</param>
            /// <returns>Signed arrival-time displacement in normalized animation units.</returns>
            float ResolveMistOffset(float2 uv)
            {
                float2 coordinate = uv * float2(max(0.01, _AspectRatio), 1.0) * max(1.0, _MistScale);
                float fine = ValueNoise(coordinate);
                float breakup = ValueNoise(coordinate * 1.91 + float2(19.7, 7.3));
                float satellite = smoothstep(0.84, 0.985, fine) * step(0.56, breakup);
                float granularOffset = ((fine - 0.5) * 0.7 + (breakup - 0.5) * 0.3) * _MistStrength;
                return granularOffset - satellite * _MistStrength * 1.65;
            }

            /// <summary>
            /// Computes a laterally coherent aerosol arrival field whose irregular head still advances along one axis.
            /// </summary>
            /// <param name="uv">Graphic UV coordinate.</param>
            /// <returns>Normalized pigment arrival time.</returns>
            float ResolveDirectionalArrival(float2 uv)
            {
                float axis = ResolveDirectionalAxes(uv).x;
                float clusteredOffset = (SampleDeposition(uv) - 0.5) * _DepositVariation;
                return axis + clusteredOffset + ResolveMistOffset(uv);
            }

            /// <summary>
            /// Computes distributed arrival timing so independent deposits merge without expanding from screen center.
            /// </summary>
            /// <param name="uv">Graphic UV coordinate.</param>
            /// <returns>Normalized pigment arrival time.</returns>
            float ResolveUniformArrival(float2 uv)
            {
                float deposition = SampleDeposition(uv);
                float remapped = deposition * deposition * (3.0 - 2.0 * deposition);
                return lerp(deposition, remapped, saturate(_DepositVariation * 1.5)) + ResolveMistOffset(uv);
            }

            /// <summary>
            /// Converts phase progress and a local arrival time into accumulated spray coverage.
            /// </summary>
            /// <param name="arrival">Normalized time at which pigment reaches the fragment.</param>
            /// <param name="progress">Normalized progress inside the active operation.</param>
            /// <returns>Accumulated deposited coverage before optional removal inversion.</returns>
            float ResolveDepositCoverage(float arrival, float progress)
            {
                if (progress <= 0.0001)
                    return 0.0;

                if (progress >= 0.9999)
                    return 1.0;

                float softness = max(0.001, _DepositSoftness);
                float extent = softness + _DepositVariation * 0.55 + _MistStrength * 1.65;
                float boundary = lerp(-extent, 1.0 + extent, progress);
                return 1.0 - smoothstep(boundary - softness, boundary + softness, arrival);
            }

            /// <summary>
            /// Computes directional-gradient coverage retained for the non-paint transition family.
            /// </summary>
            /// <param name="uv">Graphic UV coordinate.</param>
            /// <param name="progress">Normalized progress inside the active operation.</param>
            /// <returns>Accumulated gradient coverage before optional removal inversion.</returns>
            float ResolveGradientCoverage(float2 uv, float progress)
            {
                if (progress <= 0.0001)
                    return 0.0;

                if (progress >= 0.9999)
                    return 1.0;

                float2 noiseCoordinate = uv *
                                         float2(max(0.01, _AspectRatio), 1.0) *
                                         max(0.25, _NoiseScale);
                float variation = (ValueNoise(noiseCoordinate) - 0.5) * 2.0 * max(0.0, _NoiseStrength);
                float extent = _EdgeSoftness + _NoiseStrength;
                float boundary = lerp(-extent, 1.0 + extent, progress);
                return 1.0 - smoothstep(boundary - _EdgeSoftness,
                                        boundary + _EdgeSoftness,
                                        ResolveDirectionalAxes(uv).x + variation);
            }

            /// <summary>
            /// Shades one UI fragment with a single pigment while deposit and removal share the same arrival field.
            /// </summary>
            /// <param name="input">Interpolated UI fragment data.</param>
            /// <returns>Premultiplied UI color and animated aerosol coverage.</returns>
            fixed4 Frag(v2f input) : SV_Target
            {
                fixed4 sampled = (tex2D(_MainTex, input.texcoord) + _TextureSampleAdd) * input.color;
                float progress = saturate(_FadeProgress);
                float depositedCoverage = progress;

                if (_FadeMode > 2.5)
                    depositedCoverage = ResolveDepositCoverage(ResolveDirectionalArrival(input.texcoord), progress);
                else if (_FadeMode > 1.5)
                    depositedCoverage = ResolveDepositCoverage(ResolveUniformArrival(input.texcoord), progress);
                else if (_FadeMode > 0.5)
                    depositedCoverage = ResolveGradientCoverage(input.texcoord, progress);

                float coverage = _PaintOperation > 0.5
                    ? 1.0 - depositedCoverage
                    : depositedCoverage;
                float alpha = sampled.a * saturate(coverage);

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
    CustomEditor "PaintRevealShaderGUI"
}
