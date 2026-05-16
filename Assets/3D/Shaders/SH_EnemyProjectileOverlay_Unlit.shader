Shader "NashCore/Projectiles/Enemy Projectile Overlay Unlit"
{
    Properties
    {
        [MainTexture] _MainTex("Projectile Texture (white keeps the tint fully visible)", 2D) = "white" {}
        [MainColor] _BaseColor("Projectile Tint (unlit color used for hostile bullet readability)", Color) = (0.7924528, 0, 0.07458379, 1)
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth Test (Always draws hostile bullets above enemy crowds)", Float) = 8
        [HideInInspector] _ZWrite("Depth Write", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Overlay"
            "IgnoreProjector" = "True"
            "UniversalMaterialType" = "Unlit"
        }

        Pass
        {
            Name "EnemyProjectileOverlayUnlit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseColor;
            CBUFFER_END

            #if defined(DOTS_INSTANCING_ON)
                UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _BaseColor)
                UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
                #define UNITY_ACCESS_HYBRID_INSTANCED_PROP(variableName, variableType) UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(variableType, variableName)
            #else
                #define UNITY_ACCESS_HYBRID_INSTANCED_PROP(variableName, variableType) variableName
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

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 textureColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 tintColor = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_BaseColor, float4);
                return half4(textureColor.rgb * tintColor.rgb, textureColor.a * tintColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
