Shader "Cel Shader/Toon Outline ECS"
{
    // ECS/URP port of "Cel Shader/Toon Outline".
    Properties
    {
        _OutlineThickness("Outline Thickness", Range(0,10)) = 1
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        [HideInInspector] _ElasticHitDirection("Runtime Elastic Hit Direction", Vector) = (0,0,1,0)
        [HideInInspector] _ElasticHitTiming("Runtime Elastic Hit Timing", Vector) = (0,0,0,0)
        [HideInInspector] _ElasticHitMotion("Runtime Elastic Hit Motion", Vector) = (0,0,0,0)
        // Hidden property used by DOTS deformation path (ignored for classic GameObjects)
        [HideInInspector] _ComputeMeshIndex("Compute Mesh Buffer Index Offset", Float) = 0
    }

    SubShader
    {
        Blend SrcAlpha OneMinusSrcAlpha //added 07-03-2026

        Tags
        {
            "RenderType" = "Transparent" //added 07-03-2026
            "Queue" = "Transparent+1" //added 07-03-2026
            "IgnoreProjector" = "True" //added 07-03-2026
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ToonOutlineECS"
            Tags
            {
                "LightMode" = "SRPDefaultUnlit"
            }

            Cull Front
            ZWrite Off //added 07-03-2026
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex OutlinePassVertex
            #pragma fragment OutlinePassFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/3D/Shaders/Includes/SH_EnemyElasticHitDeformation.hlsl"

            // SRP Batcher requirement: all material properties must live inside UnityPerMaterial.
            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineThickness;
                float4 _ElasticHitDirection;
                float4 _ElasticHitTiming;
                float4 _ElasticHitMotion;
                float _ComputeMeshIndex;
            CBUFFER_END

            #if defined(DOTS_INSTANCING_ON)
            UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float, _ComputeMeshIndex)
                UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _OutlineColor)
                UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float, _OutlineThickness)
                UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _ElasticHitDirection)
                UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _ElasticHitTiming)
                UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _ElasticHitMotion)
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
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlinePassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionOS = input.positionOS;
                float3 normalOS = input.normalOS;

                #if defined(UNITY_DOTS_INSTANCING_ENABLED)
                    uint meshStartIndex = asuint(UNITY_ACCESS_HYBRID_INSTANCED_PROP(_ComputeMeshIndex, float));

                    if (meshStartIndex > 0u)
                    {
                        DeformedVertexData deformedVertex = _DeformedMeshData[meshStartIndex + input.vertexID];
                        positionOS = deformedVertex.Position;
                        normalOS = deformedVertex.Normal;
                    }
                #endif

                float3 elasticDirectionWS = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_ElasticHitDirection, float4).xyz;
                float3 elasticDirectionOS = TransformWorldToObjectDir(elasticDirectionWS, true);
                ApplyEnemyElasticHitDeformation(positionOS,
                                                normalOS,
                                                elasticDirectionOS,
                                                UNITY_ACCESS_HYBRID_INSTANCED_PROP(_ElasticHitTiming, float4),
                                                UNITY_ACCESS_HYBRID_INSTANCED_PROP(_ElasticHitMotion, float4));
                float outlineThickness = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_OutlineThickness, float) / 250.0;
                float3 normalDirection = SafeNormalize(normalOS);
                float3 extrudedPositionOS = positionOS + normalDirection * outlineThickness;
                VertexPositionInputs vertexPositionInputs = GetVertexPositionInputs(extrudedPositionOS);
                output.positionCS = vertexPositionInputs.positionCS;
                return output;
            }

            half4 OutlinePassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 outlineColor = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_OutlineColor, float4);
                return half4(outlineColor.rgb, outlineColor.a);
            }
            ENDHLSL
        }
    }

    FallBack "Cel Shader/Toon Outline"
}
