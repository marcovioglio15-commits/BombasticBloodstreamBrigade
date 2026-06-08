Shader "BombasticBloodstreamBrigade/Enemy Death Puddle ECS"
{
    Properties
    {
        [HideInInspector] _PuddlePrimaryColor("Runtime Primary Color", Color) = (1,1,1,1)
        [HideInInspector] _PuddleSecondaryColor("Runtime Secondary Color", Color) = (1,1,1,1)
        [HideInInspector] _PuddleTiming("Runtime Timing", Vector) = (0,4,0.2,0.08)
        [HideInInspector] _PuddleShape("Runtime Shape", Vector) = (0.28,0.1,0.04,1)
        [HideInInspector] _PuddleStyle("Runtime Style", Vector) = (0.55,0,0,0)
        [HideInInspector] _PuddleFluid("Runtime Fluid Motion", Vector) = (0.35,0.7,0.08,0.18)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "EnemyDeathPuddle"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _PuddlePrimaryColor;
                float4 _PuddleSecondaryColor;
                float4 _PuddleTiming;
                float4 _PuddleShape;
                float4 _PuddleStyle;
                float4 _PuddleFluid;
            CBUFFER_END

            #if defined(DOTS_INSTANCING_ON)
            UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _PuddlePrimaryColor)
                UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _PuddleSecondaryColor)
                UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _PuddleTiming)
                UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _PuddleShape)
                UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _PuddleStyle)
                UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _PuddleFluid)
            UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
            #define UNITY_ACCESS_HYBRID_INSTANCED_PROP(name, type) UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(type, name)
            #else
            #define UNITY_ACCESS_HYBRID_INSTANCED_PROP(name, type) name
            #endif

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            float ResolveEvaporationProgress(float normalizedTime, float stableFraction, float curveMode)
            {
                float progress = saturate((normalizedTime - stableFraction) / max(0.0001, 1.0 - stableFraction));

                if (curveMode < 0.5)
                    return progress * progress * (3.0 - 2.0 * progress);

                if (curveMode < 1.5)
                    return progress;

                return progress * progress;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 primaryColor = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_PuddlePrimaryColor, float4);
                float4 secondaryColor = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_PuddleSecondaryColor, float4);
                float4 timing = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_PuddleTiming, float4);
                float4 shape = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_PuddleShape, float4);
                float4 style = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_PuddleStyle, float4);
                float4 fluid = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_PuddleFluid, float4);
                float normalizedTime = saturate((_Time.y - timing.x) / max(0.0001, timing.y));
                float evaporation = ResolveEvaporationProgress(normalizedTime, saturate(timing.z), style.y);
                float footprintScale = lerp(1.0, saturate(timing.w), evaporation);
                float2 centeredUv = (input.uv * 2.0 - 1.0) / max(0.001, footprintScale);
                float seed = shape.w * 0.0137;
                float viscosity = saturate(fluid.y);
                float flowTime = (_Time.y - timing.x) * max(0.0, fluid.x) * lerp(1.6, 0.3, viscosity);
                float waveFrequency = lerp(9.0, 4.0, viscosity);
                float2 flowAxis = normalize(float2(frac(seed * 0.75487766),
                                                   frac(seed * 1.32471795)) *
                                            2.0 -
                                            1.0 +
                                            float2(0.013, 0.017));
                float2 crossAxis = float2(-flowAxis.y, flowAxis.x);
                float waveA = sin(dot(centeredUv, flowAxis) * waveFrequency + flowTime + seed);
                float waveB = sin(dot(centeredUv, crossAxis) * waveFrequency * 0.73 - flowTime * 0.63 - seed * 1.7);
                float motionEnvelope = saturate(1.0 - length(centeredUv) * 0.55);
                float2 distortedUv = centeredUv +
                                     (flowAxis * waveA + crossAxis * waveB) *
                                     saturate(fluid.z) *
                                     0.5 *
                                     motionEnvelope;
                float radius = length(distortedUv);
                float angle = atan2(distortedUv.y, distortedUv.x);
                float lobes = sin(angle * 5.0 + seed) * 0.55 + sin(angle * 9.0 - seed * 1.7) * 0.3;
                float noise = Hash21(floor(distortedUv * 5.0 + seed)) - 0.5;
                float irregularRadius = 1.0 + (lobes * 0.12 + noise * 0.09) * saturate(shape.x);
                float edgeFeather = max(0.001, shape.z);
                float outerMask = smoothstep(irregularRadius, irregularRadius - edgeFeather, radius);
                float borderWidth = clamp(shape.y, 0.0, 0.5);
                float innerMask = smoothstep(irregularRadius - borderWidth,
                                             irregularRadius - borderWidth - edgeFeather,
                                             radius);
                float band = step(0.48 + noise * 0.08, radius);
                float3 bodyColor = lerp(primaryColor.rgb,
                                        secondaryColor.rgb,
                                        band * saturate(style.x));
                float3 borderColor = primaryColor.rgb * 0.12;
                float3 outputColor = lerp(borderColor, bodyColor, innerMask);
                float highlightWave = waveA * 0.7 - waveB * 0.3;
                float highlightMask = smoothstep(0.58, 0.96, highlightWave) *
                                      innerMask *
                                      saturate(1.0 - radius);
                outputColor += lerp(bodyColor, 1.0, 0.55) *
                               highlightMask *
                               saturate(fluid.w) *
                               0.35;
                float fade = 1.0 - smoothstep(0.82, 1.0, normalizedTime);
                float alpha = outerMask * fade * primaryColor.a;
                return half4(outputColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
