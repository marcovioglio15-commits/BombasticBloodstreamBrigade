Shader "BombasticBloodstreamBrigade/Enemy Faces Flipbook WebGL"
{
    Properties
    {
        [MainTexture] _MainTex("Idle Face Flipbook Atlas", 2D) = "white" {}
        _FaceAttackTex("Attack Face Flipbook Atlas", 2D) = "white" {}
        _FaceDamageTex("Damage Face Flipbook Atlas", 2D) = "white" {}
        [MainColor] _BaseColor("Face Tint", Color) = (1,1,1,1)
        [Toggle] _FaceFlipbookEnabled("Enable Face Flipbook Playback", Float) = 1
        [HideInInspector] _FaceFlipbookGrid("Legacy Face Flipbook Grid", Vector) = (4,2,8,0)
        _FaceFlipbookState("Runtime Face State", Float) = 0
        _FaceIdleGrid("Idle Face Grid", Vector) = (4,2,8,0)
        _FaceAttackGrid("Attack Face Grid", Vector) = (4,1,4,0)
        _FaceDamageGrid("Damage Face Grid", Vector) = (4,1,4,0)
        _FaceFlipbookPlayback("Face Flipbook Playback", Vector) = (10,0,0,0)
        _FaceFlipbookEdgeInsetPixels("Face Cell Edge Inset", Range(0,8)) = 0.5
        _AlphaClipThreshold("Alpha Clip Threshold", Range(0,1)) = 0.01
        _AmbientColor("Ambient Toon Lighting Color", Color) = (0,0,0,1)
        _AmbientColorIntensity("Ambient Toon Lighting Intensity", Range(0,5)) = 0.5
        _ShadowSoftness("Toon Shadow Transition Softness", Range(0,0.5)) = 0.1
        _ShadowScatter("Toon Shadow Band Count Control", Range(0.01,10)) = 5
        _ShadowRangeMin("Toon Lit Range Minimum", Range(0,1)) = 0.54
        _ShadowRangeMax("Toon Lit Range Maximum", Range(-2,2)) = -0.4
        _HitFlashColor("Runtime Hit Flash Color", Color) = (1,0.15,0.15,1)
        _HitFlashBlend("Runtime Hit Flash Blend", Range(0,1)) = 0
        [HideInInspector] _ElasticHitDirection("Runtime Elastic Hit Direction", Vector) = (0,0,1,0)
        [HideInInspector] _ElasticHitTiming("Runtime Elastic Hit Timing", Vector) = (0,0,0,0)
        [HideInInspector] _ElasticHitMotion("Runtime Elastic Hit Motion", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "EnemyFacesFlipbookWebGL"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ToonPassVertex
            #pragma fragment ToonPassFragment
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/3D/Shaders/Includes/SH_EnemyElasticHitDeformation.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float4 _FaceAttackTex_TexelSize;
                float4 _FaceDamageTex_TexelSize;
                float4 _BaseColor;
                float _FaceFlipbookEnabled;
                float4 _FaceFlipbookGrid;
                float _FaceFlipbookState;
                float4 _FaceIdleGrid;
                float4 _FaceAttackGrid;
                float4 _FaceDamageGrid;
                float4 _FaceFlipbookPlayback;
                float _FaceFlipbookEdgeInsetPixels;
                float _AlphaClipThreshold;
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
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_FaceAttackTex);
            TEXTURE2D(_FaceDamageTex);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
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

            float InverseLerp(float minimumValue, float maximumValue, float value)
            {
                return (value - minimumValue) / (maximumValue - minimumValue);
            }

            float ResolveFaceState()
            {
                return clamp(floor(_FaceFlipbookState + 0.5), 0.0, 2.0);
            }

            float4 ResolveFaceGrid(float faceState)
            {
                float isDamage = step(1.5, faceState);
                float isAttack = step(0.5, faceState) * (1.0 - isDamage);
                return lerp(lerp(_FaceIdleGrid, _FaceAttackGrid, isAttack), _FaceDamageGrid, isDamage);
            }

            float4 ResolveFaceTexelSize(float faceState)
            {
                float isDamage = step(1.5, faceState);
                float isAttack = step(0.5, faceState) * (1.0 - isDamage);
                return lerp(lerp(_MainTex_TexelSize, _FaceAttackTex_TexelSize, isAttack), _FaceDamageTex_TexelSize, isDamage);
            }

            float2 ResolveFaceFlipbookUv(float2 sourceUv, float faceState)
            {
                if (saturate(_FaceFlipbookEnabled) < 0.5)
                    return sourceUv;

                float4 grid = ResolveFaceGrid(faceState);
                float columns = max(1.0, floor(grid.x + 0.5));
                float rows = max(1.0, floor(grid.y + 0.5));
                float frameCount = clamp(floor(grid.z + 0.5), 1.0, columns * rows);
                float framesPerSecond = max(0.0, _FaceFlipbookPlayback.x);
                float startFrame = fmod(max(0.0, floor(_FaceFlipbookPlayback.z + 0.5)), frameCount);
                float playbackSeconds = max(0.0, _Time.y + _FaceFlipbookPlayback.y);
                float frameIndex = fmod(floor(playbackSeconds * framesPerSecond) + startFrame, frameCount);
                float columnIndex = fmod(frameIndex, columns);
                float rowFromBottom = rows - 1.0 - floor(frameIndex / columns);
                float2 cellSize = rcp(float2(columns, rows));
                float2 cellOrigin = float2(columnIndex, rowFromBottom) * cellSize;
                float2 edgeInset = ResolveFaceTexelSize(faceState).xy * max(0.0, _FaceFlipbookEdgeInsetPixels);
                return lerp(cellOrigin + edgeInset, cellOrigin + cellSize - edgeInset, saturate(sourceUv));
            }

            half4 SampleFaceAtlas(float faceState, float2 flipbookUv)
            {
                half4 idleSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, flipbookUv);
                half4 attackSample = SAMPLE_TEXTURE2D(_FaceAttackTex, sampler_MainTex, flipbookUv);
                half4 damageSample = SAMPLE_TEXTURE2D(_FaceDamageTex, sampler_MainTex, flipbookUv);
                half isDamage = step(1.5, faceState);
                half isAttack = step(0.5, faceState) * (1.0 - isDamage);
                return lerp(lerp(idleSample, attackSample, isAttack), damageSample, isDamage);
            }

            Varyings ToonPassVertex(Attributes inputValue)
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

                float faceState = ResolveFaceState();
                float2 flipbookUv = ResolveFaceFlipbookUv(inputValue.uv, faceState);
                half4 albedoSample = SampleFaceAtlas(faceState, flipbookUv);
                clip(albedoSample.a - saturate(_AlphaClipThreshold));
                half4 albedo = half4(albedoSample.rgb * _BaseColor.rgb, albedoSample.a * _BaseColor.a);
                float3 meshNormalWS = NormalizeNormalPerPixel(inputValue.normalWS);
                Light mainLight = GetMainLight();
                float remapOut = InverseLerp(-1.0, 1.0, dot(mainLight.direction, meshNormalWS));
                float shadowScatter = max(0.0001, _ShadowScatter / 50.0);
                float shadowFloor = floor(remapOut / shadowScatter);
                float shadowRemapIn = InverseLerp(1.0 / shadowScatter, 0.0, shadowFloor);
                float shadowRemapOut = lerp(_ShadowRangeMin, _ShadowRangeMax, shadowRemapIn);
                float3 lighting = smoothstep(0.0, max(0.0001, _ShadowSoftness), shadowRemapOut) +
                                  _AmbientColor.rgb * _AmbientColorIntensity;
                float3 finalColor = albedo.rgb * (albedo.rgb + lighting);
                finalColor = lerp(finalColor,
                                  _HitFlashColor.rgb,
                                  saturate(_HitFlashBlend) * saturate(_HitFlashColor.a));
                return half4(MixFog(finalColor, inputValue.fogFactor), albedo.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
