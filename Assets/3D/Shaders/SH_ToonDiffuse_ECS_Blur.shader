Shader "Cel Shader/Toon Diffuse ECS Blur"
{
    Properties
    {
        // Added 16/03/2026
        [Header(Main Maps)]
        [MainTexture]
        _MainTex("Texture", 2D) = "white" {}

        [NoScaleOffset]
        _NormalMap("Normal Map", 2D) = "bump" {}

        
        _BumpScale("Bump Scale", Range(0,2)) = 1

        [Header(Toon Lighting)]
        
        _AmbientColor("Ambient Color", Color) = (0,0,0,1)

        
        _AmbientColorIntensity("Ambient Color Intensity", Range(0,5)) = 0.5

        
        _ShadowSoftness("Shadow Softness", Range(0,0.5)) = 0.1

        
        _ShadowScatter("Shadow Scatter", Range(0,10)) = 5

        
        _ShadowRangeMin("Shadow Range Min", Range(0,1)) = 0.54

        
        _ShadowRangeMax("Shadow Range Max", Range(-2,2)) = -0.4

        // Added 16/03/2026: The blur controls stay material-driven so ECS entities can still share
        // the same shader while using different material presets without extra runtime branching.
        [Header(Texture Blur)]
        
        _BlurRadius("Blur Radius", Range(0,16)) = 1

        
        _BlurStrength("Blur Strength", Range(0,1)) = 0.35

        
        _BlurAtlasColumns("Blur Atlas Columns", Range(1,16)) = 1

        
        _BlurAtlasRows("Blur Atlas Rows", Range(1,16)) = 1

        // Added 16/03/2026: Distance separation makes the ECS version visually match the classic shader
        // while still working with DOTS deformation buffers and SRP batcher constraints.
        [Header(Distance Separation)]
        
        _DistanceBlendStart("Distance Blend Start", Float) = 8

        
        _DistanceBlendEnd("Distance Blend End", Float) = 30

        
        _DistanceBlurBoost("Distance Blur Boost", Range(0,1)) = 0.35

        
        _DistanceBlurScale("Distance Blur Scale", Range(1,3)) = 1.6

        
        _DistanceTintColor("Distance Tint Color", Color) = (0.82,0.9,1,1)

        
        _DistanceTintStrength("Distance Tint Strength", Range(0,1)) = 0.2

        
        _DistanceSaturation("Distance Saturation", Range(0,1)) = 0.72

        
        _DistanceContrast("Distance Contrast", Range(0.25,1.25)) = 0.82

        [HideInInspector]
        _ComputeMeshIndex("Compute Mesh Buffer Index Offset", Float) = 0
    }

    SubShader
    {
        Blend SrcAlpha OneMinusSrcAlpha

        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ToonDiffuseECSBlur"

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
                float _BumpScale;
                float4 _AmbientColor;
                float _AmbientColorIntensity;
                float _ShadowSoftness;
                float _ShadowScatter;
                float _ShadowRangeMin;
                float _ShadowRangeMax;
                float _BlurRadius; // Added 16/03/2026
                float _BlurStrength; // Added 16/03/2026
                float _BlurAtlasColumns; // Added 17/03/2026
                float _BlurAtlasRows; // Added 17/03/2026
                float _DistanceBlendStart; // Added 16/03/2026
                float _DistanceBlendEnd; // Added 16/03/2026
                float _DistanceBlurBoost; // Added 16/03/2026
                float _DistanceBlurScale; // Added 16/03/2026
                float4 _DistanceTintColor; // Added 16/03/2026
                float _DistanceTintStrength; // Added 16/03/2026
                float _DistanceSaturation; // Added 16/03/2026
                float _DistanceContrast; // Added 16/03/2026
                float _ComputeMeshIndex;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            float4 _MainTex_TexelSize; // Added 16/03/2026

            // Added 16/03/2026
            struct BlurAccumulator
            {
                float3 colorSum;
                float alphaSum;
                float colorWeightSum;
                float kernelWeightSum;
            };

            float InverseLerpSafe(float minValue, float maxValue, float value) // Added 16/03/2026
            {
                float range = max(abs(maxValue - minValue), 0.0001);
                return saturate((value - minValue) / range);
            }

            float3 DecodeToonNormal(float4 normalSample, float bumpScale) // Added 16/03/2026
            {
                float3 normalTS;
                normalTS.xy = normalSample.wy * 2.0 - 1.0;
                normalTS.xy *= bumpScale;
                normalTS.z = sqrt(saturate(1.0 - dot(normalTS.xy, normalTS.xy)));
                normalTS = normalTS.xzy;
                return normalize(normalTS);
            }

            float2 GetBlurAtlasCellSize() // Added 17/03/2026
            {
                float2 atlasGrid = max(round(float2(_BlurAtlasColumns, _BlurAtlasRows)), 1.0.xx);
                return 1.0 / atlasGrid;
            }

            void GetBlurUvClampBounds(
                float2 uv,
                float2 texelSize,
                out float2 clampMin,
                out float2 clampMax) // Added 17/03/2026
            {
                float2 atlasGrid = max(round(float2(_BlurAtlasColumns, _BlurAtlasRows)), 1.0.xx);
                float2 atlasCellSize = 1.0 / atlasGrid;
                float2 atlasCellIndex = min(floor(saturate(uv) / atlasCellSize), atlasGrid - 1.0.xx);
                float2 atlasCellMin = atlasCellIndex * atlasCellSize;
                float2 atlasCellMax = min(atlasCellMin + atlasCellSize, 1.0.xx);
                float2 halfTexel = texelSize * 0.5;

                clampMin = atlasCellMin + halfTexel;
                clampMax = max(clampMin, atlasCellMax - halfTexel);
            }

            float4 SampleMainTextureForBlur(
                float2 uv,
                float2 texelSize,
                float2 clampMin,
                float2 clampMax) // Added 17/03/2026
            {
                float2 safeTexelSize = max(texelSize, float2(0.0001, 0.0001));
                float2 textureSize = 1.0 / safeTexelSize;
                float2 texelSpace = uv * textureSize - 0.5;
                float2 texelFloor = floor(texelSpace);
                float2 texelFraction = saturate(texelSpace - texelFloor);

                float2 uv00 = clamp((texelFloor + 0.5) * safeTexelSize, clampMin, clampMax);
                float2 uv10 = clamp(uv00 + float2(safeTexelSize.x, 0.0), clampMin, clampMax);
                float2 uv01 = clamp(uv00 + float2(0.0, safeTexelSize.y), clampMin, clampMax);
                float2 uv11 = clamp(uv00 + safeTexelSize, clampMin, clampMax);

                float4 sample00 = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv00, 0);
                float4 sample10 = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv10, 0);
                float4 sample01 = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv01, 0);
                float4 sample11 = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv11, 0);

                return lerp(
                    lerp(sample00, sample10, texelFraction.x),
                    lerp(sample01, sample11, texelFraction.x),
                    texelFraction.y);
            }

            void AccumulateBlurSample(
                float2 uv,
                float2 texelSize,
                float2 clampMin,
                float2 clampMax,
                float kernelWeight,
                inout BlurAccumulator accumulator) // Added 16/03/2026
            {
                float4 sampleColor = SampleMainTextureForBlur(uv, texelSize, clampMin, clampMax);
                float colorWeight = kernelWeight * max(sampleColor.a, 0.0001);
                accumulator.colorSum += sampleColor.rgb * colorWeight;
                accumulator.alphaSum += sampleColor.a * kernelWeight;
                accumulator.colorWeightSum += colorWeight;
                accumulator.kernelWeightSum += kernelWeight;
            }

            float GetBlurRingWeight(float normalizedDistance) // Added 17/03/2026
            {
                float falloff = exp2(-1.5 * normalizedDistance * normalizedDistance);
                return max(falloff * 1.7, 0.15);
            }

            void AccumulateBlurRing(
                float2 uv,
                float2 texelSize,
                float2 clampMin,
                float2 clampMax,
                float ringRadius,
                float kernelWeight,
                inout BlurAccumulator accumulator) // Added 17/03/2026
            {
                float2 axisOffsetX = float2(texelSize.x * ringRadius, 0.0);
                float2 axisOffsetY = float2(0.0, texelSize.y * ringRadius);
                float2 diagonalOffset = texelSize * (ringRadius * 0.70710678);

                AccumulateBlurSample(uv + axisOffsetX, texelSize, clampMin, clampMax, kernelWeight, accumulator);
                AccumulateBlurSample(uv - axisOffsetX, texelSize, clampMin, clampMax, kernelWeight, accumulator);
                AccumulateBlurSample(uv + axisOffsetY, texelSize, clampMin, clampMax, kernelWeight, accumulator);
                AccumulateBlurSample(uv - axisOffsetY, texelSize, clampMin, clampMax, kernelWeight, accumulator);
                AccumulateBlurSample(uv + diagonalOffset, texelSize, clampMin, clampMax, kernelWeight, accumulator);
                AccumulateBlurSample(uv - diagonalOffset, texelSize, clampMin, clampMax, kernelWeight, accumulator);
                AccumulateBlurSample(uv + float2(diagonalOffset.x, -diagonalOffset.y), texelSize, clampMin, clampMax, kernelWeight, accumulator);
                AccumulateBlurSample(uv + float2(-diagonalOffset.x, diagonalOffset.y), texelSize, clampMin, clampMax, kernelWeight, accumulator);
            }

            float4 SampleBlurredMainTexture(float2 uv, float2 texelSize, float blurRadius) // Added 16/03/2026
            {
                float clampedBlurRadius = min(max(blurRadius, 0.0), 16.0);

                if (clampedBlurRadius <= 0.0001)
                {
                    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                }

                float ringCount = min(6.0, max(1.0, ceil(clampedBlurRadius)));
                float ringStep = clampedBlurRadius / ringCount;
                BlurAccumulator accumulator = (BlurAccumulator)0;
                float2 clampMin;
                float2 clampMax;

                GetBlurUvClampBounds(uv, texelSize, clampMin, clampMax);

                AccumulateBlurSample(uv, texelSize, clampMin, clampMax, 4.0, accumulator);
                AccumulateBlurRing(uv, texelSize, clampMin, clampMax, ringStep, GetBlurRingWeight(1.0 / ringCount), accumulator);

                if (ringCount > 1.5)
                {
                    AccumulateBlurRing(uv, texelSize, clampMin, clampMax, ringStep * 2.0, GetBlurRingWeight(2.0 / ringCount), accumulator);
                }

                if (ringCount > 2.5)
                {
                    AccumulateBlurRing(uv, texelSize, clampMin, clampMax, ringStep * 3.0, GetBlurRingWeight(3.0 / ringCount), accumulator);
                }

                if (ringCount > 3.5)
                {
                    AccumulateBlurRing(uv, texelSize, clampMin, clampMax, ringStep * 4.0, GetBlurRingWeight(4.0 / ringCount), accumulator);
                }

                if (ringCount > 4.5)
                {
                    AccumulateBlurRing(uv, texelSize, clampMin, clampMax, ringStep * 5.0, GetBlurRingWeight(5.0 / ringCount), accumulator);
                }

                if (ringCount > 5.5)
                {
                    AccumulateBlurRing(uv, texelSize, clampMin, clampMax, ringStep * 6.0, GetBlurRingWeight(6.0 / ringCount), accumulator);
                }

                return float4(
                    accumulator.colorSum / max(accumulator.colorWeightSum, 0.0001),
                    accumulator.alphaSum / max(accumulator.kernelWeightSum, 0.0001));
            }

            float GetDistanceBlendFactor(float3 worldPosition, float3 cameraPosition, float distanceStart, float distanceEnd) // Added 16/03/2026
            {
                float safeDistanceEnd = max(distanceEnd, distanceStart + 0.0001);
                float distanceToCamera = distance(worldPosition, cameraPosition);
                return InverseLerpSafe(distanceStart, safeDistanceEnd, distanceToCamera);
            }

            float3 ApplyDistanceSeparation(
                float3 color,
                float distanceFactor,
                float3 tintColor,
                float tintStrength,
                float saturationAtMaxDistance,
                float contrastAtMaxDistance) // Added 16/03/2026
            {
                float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
                float saturation = lerp(1.0, saturationAtMaxDistance, distanceFactor);
                float contrast = lerp(1.0, contrastAtMaxDistance, distanceFactor);
                float3 saturatedColor = lerp(luminance.xxx, color, saturation);
                float3 contrastedColor = saturate((saturatedColor - 0.5.xxx) * contrast + 0.5.xxx);
                float tintBlend = saturate(distanceFactor * tintStrength);
                return lerp(contrastedColor, contrastedColor * tintColor, tintBlend);
            }

            float3 EvaluateToonLighting(
                float3 albedo,
                float ramp,
                float3 ambientColor,
                float ambientIntensity,
                float shadowSoftness,
                float shadowScatter,
                float shadowRangeMin,
                float shadowRangeMax) // Added 16/03/2026
            {
                float scatter = max(shadowScatter / 50.0, 0.0001);
                float remap = InverseLerpSafe(-1.0, 1.0, ramp);
                float shadowFloor = floor(remap / scatter);
                float shadowRemap = InverseLerpSafe(1.0 / scatter, 0.0, shadowFloor);
                float shadowRange = lerp(shadowRangeMin, shadowRangeMax, shadowRemap);
                float3 lighting = smoothstep(0.0, shadowSoftness, shadowRange) + ambientColor * ambientIntensity;
                return albedo * (albedo + lighting);
            }

            float4 EvaluateStylizedToonColor(
                float2 uv,
                float3 meshNormal,
                float3 toonNormal,
                float3 lightDirection,
                float2 texelSize,
                float blurRadius,
                float blurStrength,
                float distanceFactor,
                float distanceBlurBoost,
                float distanceBlurScale,
                float3 distanceTintColor,
                float distanceTintStrength,
                float distanceSaturation,
                float distanceContrast,
                float3 ambientColor,
                float ambientIntensity,
                float shadowSoftness,
                float shadowScatter,
                float shadowRangeMin,
                float shadowRangeMax) // Added 16/03/2026
            {
                float effectiveBlurStrength = saturate(blurStrength + distanceFactor * distanceBlurBoost);
                float effectiveBlurRadius = max(blurRadius, 0.0) * lerp(1.0, distanceBlurScale, distanceFactor);
                float4 baseSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                float4 blurredSample = baseSample;

                if (effectiveBlurStrength > 0.0001 && effectiveBlurRadius > 0.0001)
                {
                    blurredSample = SampleBlurredMainTexture(uv, texelSize, effectiveBlurRadius);
                }

                float4 stylizedSample = lerp(baseSample, blurredSample, effectiveBlurStrength);
                stylizedSample.rgb = ApplyDistanceSeparation(
                    stylizedSample.rgb,
                    distanceFactor,
                    distanceTintColor,
                    distanceTintStrength,
                    distanceSaturation,
                    distanceContrast);

                float ramp = dot(dot(lightDirection, meshNormal).xxx, toonNormal);
                float3 finalColor = EvaluateToonLighting(
                    stylizedSample.rgb,
                    ramp,
                    ambientColor,
                    ambientIntensity,
                    shadowSoftness,
                    shadowScatter,
                    shadowRangeMin,
                    shadowRangeMax);

                return float4(finalColor, stylizedSample.a);
            }

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

            // Added 16/03/2026: World position is carried to the fragment stage so distance-based blur
            // and atmospheric separation can be computed per-pixel after DOTS deformation is applied.
            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 ResolveToonNormal(float2 uv)
            {
                float4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv);
                return DecodeToonNormal(normalSample, _BumpScale);
            }

            Varyings ToonPassVertex(Attributes input)
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

                VertexPositionInputs vertexPositionInputs = GetVertexPositionInputs(positionOS);
                VertexNormalInputs vertexNormalInputs = GetVertexNormalInputs(normalOS);

                output.positionCS = vertexPositionInputs.positionCS;
                output.positionWS = vertexPositionInputs.positionWS; // Added 16/03/2026
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.normalWS = NormalizeNormalPerVertex(vertexNormalInputs.normalWS);
                output.fogFactor = ComputeFogFactor(vertexPositionInputs.positionCS.z);

                return output;
            }

            half4 ToonPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 meshNormalWS = NormalizeNormalPerPixel(input.normalWS);
                float3 toonNormal = ResolveToonNormal(input.uv); // Added 16/03/2026
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float distanceFactor = GetDistanceBlendFactor(
                    input.positionWS,
                    _WorldSpaceCameraPos,
                    _DistanceBlendStart,
                    _DistanceBlendEnd); // Added 16/03/2026

                // Added 16/03/2026: The shared evaluator performs the blur, the atmospheric separation
                // and the toon lighting in a single reusable path to avoid logic drift with the original shader.
                float4 finalColor = EvaluateStylizedToonColor(
                    input.uv,
                    meshNormalWS,
                    toonNormal,
                    lightDir,
                    _MainTex_TexelSize.xy,
                    _BlurRadius,
                    _BlurStrength,
                    distanceFactor,
                    _DistanceBlurBoost,
                    _DistanceBlurScale,
                    _DistanceTintColor.rgb,
                    _DistanceTintStrength,
                    _DistanceSaturation,
                    _DistanceContrast,
                    _AmbientColor.rgb,
                    _AmbientColorIntensity,
                    _ShadowSoftness,
                    _ShadowScatter,
                    _ShadowRangeMin,
                    _ShadowRangeMax);

                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);
                return half4(finalColor.rgb, finalColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
