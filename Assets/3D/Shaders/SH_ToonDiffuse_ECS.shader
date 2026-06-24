Shader "Cel Shader/Toon Diffuse ECS"
{
    // ECS/URP port of "Cel Shader/Toon Diffuse" with opaque decal receiver support.
    Properties
    {
        // Base toon surface inputs.
        [MainTexture] _MainTex("Texture", 2D) = "white" {}
        [NoScaleOffset] _NormalMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Bump Scale", Range(0,2)) = 1

        // Toon lighting controls.
        _AmbientColor("Ambient Color", Color) = (0,0,0,1)
        _AmbientColorIntensity("Ambient Color Intensity", Range(0,5)) = 0.5
        _ShadowSoftness("Shadow Softness", Range(0,0.5)) = 0.1
        _ShadowScatter("Shadow Scatter", Range(0,10)) = 5
        _ShadowRangeMin("Shadow Range Min", Range(0,1)) = 0.54
        _ShadowRangeMax("Shadow Range Max", Range(-2,2)) = -0.4

        // DOTS deformation metadata. Zero means classic non-deformed mesh data.
        [HideInInspector] _ComputeMeshIndex("Compute Mesh Buffer Index Offset", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
        }

        HLSLINCLUDE
            // Shader Model 4.5 is required for DOTS deformation buffers on desktop APIs.
            #pragma target 4.5

            // DOTS.hlsl injects DOTS instancing variants and keywords.
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

            // Keeping material properties inside UnityPerMaterial preserves SRP Batcher compatibility.
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _BumpScale;
                float4 _AmbientColor;
                float _AmbientColorIntensity;
                float _ShadowSoftness;
                float _ShadowScatter;
                float _ShadowRangeMin;
                float _ShadowRangeMax;
                float _ComputeMeshIndex;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            #if defined(DOTS_INSTANCING_ON)
                UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float, _ComputeMeshIndex)
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

            struct ToonVaryings
            {
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            struct DepthNormalsVaryings
            {
                float3 normalWS : TEXCOORD0;
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
                float3 normal;
                normal.xy = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv).wy * 2.0 - 1.0;
                normal.xy *= _BumpScale;
                normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
                normal = normal.xzy;

                return normalize(normal);
            }

            void ResolveDeformedVertexData(Attributes input, out float3 positionOS, out float3 normalOS)
            {
                positionOS = input.positionOS;
                normalOS = input.normalOS;

                #if defined(UNITY_DOTS_INSTANCING_ENABLED)
                    uint meshStartIndex = asuint(UNITY_ACCESS_HYBRID_INSTANCED_PROP(_ComputeMeshIndex, float));

                    if (meshStartIndex > 0u)
                    {
                        DeformedVertexData deformedVertex = _DeformedMeshData[meshStartIndex + input.vertexID];
                        positionOS = deformedVertex.Position;
                        normalOS = deformedVertex.Normal;
                    }
                #endif
            }
        ENDHLSL

        Pass
        {
            Name "ToonDiffuseECS"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex ToonPassVertex
            #pragma fragment ToonPassFragment
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3

            ToonVaryings ToonPassVertex(Attributes input)
            {
                ToonVaryings output = (ToonVaryings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Resolve classic or DOTS-deformed mesh data before entering URP transform helpers.
                float3 positionOS;
                float3 normalOS;
                ResolveDeformedVertexData(input, positionOS, normalOS);

                VertexPositionInputs vertexPositionInputs = GetVertexPositionInputs(positionOS);
                VertexNormalInputs vertexNormalInputs = GetVertexNormalInputs(normalOS);

                output.positionCS = vertexPositionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.normalWS = NormalizeNormalPerVertex(vertexNormalInputs.normalWS);
                output.fogFactor = ComputeFogFactor(vertexPositionInputs.positionCS.z);

                return output;
            }

            half4 ToonPassFragment(ToonVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Sample albedo first so DBuffer decals can modify the receiver before toon lighting.
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 baseColor = albedo.rgb;
                half3 meshNormalWS = NormalizeNormalPerPixel(input.normalWS);

                #if defined(_DBUFFER)
                    ApplyDecalToBaseColorAndNormal(input.positionCS, baseColor, meshNormalWS);
                #endif

                // Keep the existing toon normal-map remap to preserve the authored look.
                float2 toonNormalUv = TRANSFORM_TEX(input.uv, _MainTex);
                half3 toonNormal = ResolveToonNormalFromMap(toonNormalUv);

                Light mainLight = GetMainLight();
                half firstRamp = dot(mainLight.direction, meshNormalWS);
                half ramp = dot(firstRamp.xxx, toonNormal);

                // Quantize the light ramp, then combine ambient color and texture response.
                half remapOut = InverseLerp(-1.0, 1.0, ramp);
                half shadowScatter = _ShadowScatter / 50.0;
                half shadowFloor = floor(remapOut / shadowScatter);
                half shadowRemapIn = InverseLerp(1.0 / shadowScatter, 0.0, shadowFloor);
                half shadowRemapOut = lerp(_ShadowRangeMin, _ShadowRangeMax, shadowRemapIn);

                half3 ambientTerm = _AmbientColor.rgb * _AmbientColorIntensity;
                half3 lighting = smoothstep(0.0, _ShadowSoftness, shadowRemapOut) + ambientTerm;
                half3 finalColor = baseColor * (baseColor + lighting);

                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex ToonDepthNormalsVertex
            #pragma fragment ToonDepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            DepthNormalsVaryings ToonDepthNormalsVertex(Attributes input)
            {
                DepthNormalsVaryings output = (DepthNormalsVaryings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Match the forward pass deformation path so DBuffer decals use correct receiver normals.
                float3 positionOS;
                float3 normalOS;
                ResolveDeformedVertexData(input, positionOS, normalOS);

                VertexPositionInputs vertexPositionInputs = GetVertexPositionInputs(positionOS);
                VertexNormalInputs vertexNormalInputs = GetVertexNormalInputs(normalOS);

                output.positionCS = vertexPositionInputs.positionCS;
                output.normalWS = NormalizeNormalPerVertex(vertexNormalInputs.normalWS);

                return output;
            }

            void ToonDepthNormalsFragment(
                DepthNormalsVaryings input,
                out half4 outNormalWS : SV_Target0)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Encode normals in the same formats expected by URP DepthNormals consumers.
                #if defined(_GBUFFER_NORMALS_OCT)
                    float3 normalWS = normalize(input.normalWS);
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                    outNormalWS = half4(packedNormalWS, 0.0);
                #else
                    float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                    outNormalWS = half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack Off
}
