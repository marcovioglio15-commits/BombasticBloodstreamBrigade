Shader "Cel Shader/Toon Diffuse Hit Flash"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
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
        [HideInInspector] _ElasticHitDirection("Runtime Elastic Hit Direction", Vector) = (0,0,1,0)
        [HideInInspector] _ElasticHitTiming("Runtime Elastic Hit Timing", Vector) = (0,0,0,0)
        [HideInInspector] _ElasticHitMotion("Runtime Elastic Hit Motion", Vector) = (0,0,0,0)
        _OutlineThickness("Outline Thickness", Range(0,10)) = 1
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "LightMode" = "UniversalForward"
        }

        Pass
        {
            Name "ToonDiffuseHitFlash"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "Assets/3D/Shaders/Includes/SH_EnemyElasticHitDeformation.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
            };

            struct Interpolators
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
            };

            sampler _MainTex;
            sampler _NormalMap;
            float _BumpScale;
            float4 _MainTex_ST;
            float4 _BaseColor;
            float4 _AmbientColor;
            float _AmbientColorIntensity;
            float _ShadowSoftness;
            float _ShadowScatter;
            float _ShadowRangeMin;
            float _ShadowRangeMax;
            float4 _HitFlashColor;
            float _HitFlashBlend;
            float4 _ElasticHitDirection;
            float4 _ElasticHitTiming;
            float4 _ElasticHitMotion;
            float4 _OutlineColor;
            float _OutlineThickness;

            Interpolators ToonNormalMap(v2f inputValue)
            {
                Interpolators outputValue;
                outputValue.uv = TRANSFORM_TEX(inputValue.uv, _MainTex);
                outputValue.position = UnityObjectToClipPos(inputValue.vertex);
                outputValue.normal = inputValue.normal;
                return outputValue;
            }

            v2f vert(appdata inputValue)
            {
                v2f outputValue;
                float3 positionOS = inputValue.vertex.xyz;
                float3 normalOS = inputValue.normal;
                ApplyEnemyElasticHitDeformation(positionOS,
                                                normalOS,
                                                UnityWorldToObjectDir(_ElasticHitDirection.xyz),
                                                _ElasticHitTiming,
                                                _ElasticHitMotion);
                inputValue.vertex.xyz = positionOS;
                inputValue.normal = normalOS;
                outputValue.vertex = UnityObjectToClipPos(inputValue.vertex);
                outputValue.uv = TRANSFORM_TEX(inputValue.uv, _MainTex);
                outputValue.normal = normalize(UnityObjectToWorldNormal(inputValue.normal));
                UNITY_TRANSFER_FOG(outputValue, outputValue.vertex);
                return outputValue;
            }

            void InitializeFragmentNormal(inout Interpolators inputValue)
            {
                inputValue.normal.xy = tex2D(_NormalMap, inputValue.uv).wy * 2 - 1;
                inputValue.normal.xy *= _BumpScale;
                inputValue.normal.z = sqrt(1 - saturate(dot(inputValue.normal.xy, inputValue.normal.xy)));
                inputValue.normal = normalize(inputValue.normal.xzy);
            }

            float InverseLerp(float minValue, float maxValue, float value)
            {
                return (value - minValue) / (maxValue - minValue);
            }

            fixed4 frag(v2f inputValue) : SV_Target
            {
                float shadowScatter = _ShadowScatter / 50;
                float3 lightDirection = _WorldSpaceLightPos0.xyz;
                fixed4 albedoSample = tex2D(_MainTex, inputValue.uv);
                fixed4 albedo = fixed4(albedoSample.rgb * _BaseColor.rgb, albedoSample.a * _BaseColor.a);
                float firstRamp = dot(lightDirection, inputValue.normal);

                Interpolators normalData = ToonNormalMap(inputValue);
                InitializeFragmentNormal(normalData);

                float ramp = dot(firstRamp, normalData.normal);
                float remapOut = InverseLerp(-1, 1, ramp);
                float shadowFloor = floor(remapOut / shadowScatter);
                float shadowRemapIn = InverseLerp(1 / shadowScatter, 0, shadowFloor);
                float shadowRemapOut = lerp(_ShadowRangeMin, _ShadowRangeMax, shadowRemapIn);
                float3 lighting = smoothstep(0, _ShadowSoftness, shadowRemapOut) + (_AmbientColor.rgb * _AmbientColorIntensity);
                float3 texLighting = albedo.rgb + lighting;
                fixed4 finalColor = fixed4(albedo.rgb * texLighting, albedo.a);
                float flashBlend = saturate(_HitFlashBlend * saturate(_HitFlashColor.a));
                finalColor.rgb = lerp(finalColor.rgb, _HitFlashColor.rgb, flashBlend);
                UNITY_APPLY_FOG(inputValue.fogCoord, finalColor);
                return finalColor;
            }
            ENDCG
        }

        Pass
        {
            Name "ToonOutline"
            Tags
            {
                "LightMode" = "OutlineRender"
            }

            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment

            #include "UnityCG.cginc"
            #include "Assets/3D/Shaders/Includes/SH_EnemyElasticHitDeformation.hlsl"

            struct OutlineAppData
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct OutlineVaryings
            {
                float4 vertex : SV_POSITION;
            };

            float4 _OutlineColor;
            float _OutlineThickness;
            float4 _ElasticHitDirection;
            float4 _ElasticHitTiming;
            float4 _ElasticHitMotion;

            OutlineVaryings OutlineVertex(OutlineAppData inputValue)
            {
                OutlineVaryings outputValue;
                float3 positionOS = inputValue.vertex.xyz;
                float3 normalOS = inputValue.normal;
                ApplyEnemyElasticHitDeformation(positionOS,
                                                normalOS,
                                                UnityWorldToObjectDir(_ElasticHitDirection.xyz),
                                                _ElasticHitTiming,
                                                _ElasticHitMotion);
                inputValue.vertex.xyz = positionOS;
                inputValue.normal = normalOS;
                float outlineThickness = _OutlineThickness / 250.0;
                float3 normalDirection = normalize(inputValue.normal);
                float4 extrudedPosition = inputValue.vertex + float4(normalDirection * outlineThickness, 0.0);
                outputValue.vertex = UnityObjectToClipPos(extrudedPosition);
                return outputValue;
            }

            fixed4 OutlineFragment(OutlineVaryings inputValue) : SV_Target
            {
                return fixed4(_OutlineColor.rgb, _OutlineColor.a);
            }
            ENDCG
        }
    }
}
