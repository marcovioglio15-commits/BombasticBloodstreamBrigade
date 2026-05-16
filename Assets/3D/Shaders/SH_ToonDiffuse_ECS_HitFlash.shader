Shader "Cel Shader/Toon Diffuse ECS Hit Flash"
{
    Properties
    {
        [MainTexture] _MainTex("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [NoScaleOffset] _NormalMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Bump Scale", Range(0,2)) = 1
        _AmbientColor("Ambient Color", Color) = (0,0,0,1)
        _AmbientColorIntensity("Ambient Color Intensity", Range(0,5)) = 0.5
        _ShadowSoftness("Shadow Softness", Range(0,0.5)) = 0.1
        _ShadowScatter("Shadow Scatter", Range(0,10)) = 5
        _ShadowRangeMin("Shadow Range Min", Range(0,1)) = 0.54
        _ShadowRangeMax("Shadow Range Max", Range(-2,2)) = -0.4
        _HitFlashColor("Hit Flash Color", Color) = (1,0.15,0.15,1)
        _HitFlashBlend("Hit Flash Blend", Range(0,1)) = 0
        [HideInInspector] _ComputeMeshIndex("Compute Mesh Buffer Index Offset", Float) = 0
    }

    SubShader
    {
        Tags
        {
            //"Queue" = "Opaque"
            "Queue" = "Transparent" //changed 30/04/2026 because outline was not showing on the enemies (I copied it from the SH_ToonDiffuse_ECS shader)
            "IgnoreProjector" = "True" //added 30/04/2026
            "RenderType" = "Transparent" //added 30/04/2026
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ToonDiffuseECSHitFlash"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ToonPassVertex
            #pragma fragment ToonPassFragment
            #pragma multi_compile_fog

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseColor;
                float _BumpScale;
                float4 _AmbientColor;
                float _AmbientColorIntensity;
                float _ShadowSoftness;
                float _ShadowScatter;
                float _ShadowRangeMin;
                float _ShadowRangeMax;
                float4 _HitFlashColor;
                float _HitFlashBlend;
                float _ComputeMeshIndex;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            #if defined(DOTS_INSTANCING_ON)
            UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float, _ComputeMeshIndex)
                UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _BaseColor)
                UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _HitFlashColor)
                UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float, _HitFlashBlend)
            UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
            #define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(type, var)
            #else
            #define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) var
            #endif

            struct DeformedVertexData
            {
                float3 Position;
                float3 Normal;
                float3 Tangent;
            };

            StructuredBuffer<DeformedVertexData> _DeformedMeshData : register(t1);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float InverseLerp(float minValue, float maxValue, float value)
            {
                return (value - minValue) / (maxValue - minValue);
            }

            float3 ResolveToonNormalFromMap(float2 uv)
            {
                float3 normalValue;
                normalValue.xy = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv).wy * 2.0 - 1.0;
                normalValue.xy *= _BumpScale;
                normalValue.z = sqrt(1.0 - saturate(dot(normalValue.xy, normalValue.xy)));
                normalValue = normalValue.xzy;
                return normalize(normalValue);
            }

            Varyings ToonPassVertex(Attributes inputValue)
            {
                Varyings outputValue = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(inputValue);
                UNITY_TRANSFER_INSTANCE_ID(inputValue, outputValue);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(outputValue);

                float3 positionOS = inputValue.positionOS;
                float3 normalOS = inputValue.normalOS;

                #if defined(UNITY_DOTS_INSTANCING_ENABLED)
                    uint meshStartIndex = asuint(UNITY_ACCESS_HYBRID_INSTANCED_PROP(_ComputeMeshIndex, float));

                    if (meshStartIndex > 0u)
                    {
                        DeformedVertexData deformedVertex = _DeformedMeshData[meshStartIndex + inputValue.vertexID];
                        positionOS = deformedVertex.Position;
                        normalOS = deformedVertex.Normal;
                    }
                #endif

                VertexPositionInputs vertexPositionInputs = GetVertexPositionInputs(positionOS);
                VertexNormalInputs vertexNormalInputs = GetVertexNormalInputs(normalOS);

                outputValue.positionCS = vertexPositionInputs.positionCS;
                outputValue.uv = TRANSFORM_TEX(inputValue.uv, _MainTex);
                outputValue.normalWS = NormalizeNormalPerVertex(vertexNormalInputs.normalWS);
                outputValue.fogFactor = ComputeFogFactor(vertexPositionInputs.positionCS.z);
                return outputValue;
            }

            half4 ToonPassFragment(Varyings inputValue) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(inputValue);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(inputValue);

                float4 baseColor = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_BaseColor, float4);
                half4 albedoSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, inputValue.uv);
                half4 albedo = half4(albedoSample.rgb * baseColor.rgb, albedoSample.a * baseColor.a);
                float3 meshNormalWS = NormalizeNormalPerPixel(inputValue.normalWS);
                float2 toonNormalUv = TRANSFORM_TEX(inputValue.uv, _MainTex);
                float3 toonNormal = ResolveToonNormalFromMap(toonNormalUv);
                Light mainLight = GetMainLight();
                float firstRamp = dot(mainLight.direction, meshNormalWS);
                float ramp = dot(firstRamp.xxx, toonNormal);
                float remapOut = InverseLerp(-1.0, 1.0, ramp);
                float shadowScatter = _ShadowScatter / 50.0;
                float shadowFloor = floor(remapOut / shadowScatter);
                float shadowRemapIn = InverseLerp(1.0 / shadowScatter, 0.0, shadowFloor);
                float shadowRemapOut = lerp(_ShadowRangeMin, _ShadowRangeMax, shadowRemapIn);
                float3 ambientTerm = _AmbientColor.rgb * _AmbientColorIntensity;
                float3 lighting = smoothstep(0.0, _ShadowSoftness, shadowRemapOut) + ambientTerm;
                float3 texLighting = albedo.rgb + lighting;
                float3 finalColor = albedo.rgb * texLighting;
                float4 hitFlashColor = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_HitFlashColor, float4);
                float hitFlashBlend = saturate(UNITY_ACCESS_HYBRID_INSTANCED_PROP(_HitFlashBlend, float));
                finalColor = lerp(finalColor, hitFlashColor.rgb, hitFlashBlend * saturate(hitFlashColor.a));
                finalColor = MixFog(finalColor, inputValue.fogFactor);
                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
