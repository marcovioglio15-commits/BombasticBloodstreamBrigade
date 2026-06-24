Shader "BombasticBloodstreamBrigade/Toon Outline WebGL"
{
    Properties
    {
        _OutlineThickness("Outline Thickness", Range(0,10)) = 1
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        [HideInInspector] _ElasticHitDirection("Runtime Elastic Hit Direction", Vector) = (0,0,1,0)
        [HideInInspector] _ElasticHitTiming("Runtime Elastic Hit Timing", Vector) = (0,0,0,0)
        [HideInInspector] _ElasticHitMotion("Runtime Elastic Hit Motion", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+1"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ToonOutlineWebGL"
            Tags
            {
                "LightMode" = "SRPDefaultUnlit"
            }

            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OutlinePassVertex
            #pragma fragment OutlinePassFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/3D/Shaders/Includes/SH_EnemyElasticHitDeformation.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineThickness;
                float4 _ElasticHitDirection;
                float4 _ElasticHitTiming;
                float4 _ElasticHitMotion;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlinePassVertex(Attributes inputValue)
            {
                Varyings outputValue = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(inputValue);
                UNITY_TRANSFER_INSTANCE_ID(inputValue, outputValue);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(outputValue);

                float3 positionOS = inputValue.positionOS;
                float3 normalOS = inputValue.normalOS;
                float3 elasticDirectionOS = TransformWorldToObjectDir(_ElasticHitDirection.xyz, true);
                ApplyEnemyElasticHitDeformation(positionOS,
                                                normalOS,
                                                elasticDirectionOS,
                                                _ElasticHitTiming,
                                                _ElasticHitMotion);

                float outlineThickness = _OutlineThickness / 250.0;
                float3 extrudedPositionOS = positionOS + SafeNormalize(normalOS) * outlineThickness;
                outputValue.positionCS = TransformObjectToHClip(extrudedPositionOS);
                return outputValue;
            }

            half4 OutlinePassFragment(Varyings inputValue) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(inputValue);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(inputValue);
                return half4(_OutlineColor.rgb, _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}
