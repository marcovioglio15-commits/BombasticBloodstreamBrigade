Shader "Custom/Enemy Ground Indicator"
{
    // Unlit URP transparent shader that draws the enemy hit-box shadow plus the two
    // concentric fillable rings (health + shield) on a flat quad lying on the world
    // XZ plane. All per-enemy values are pushed via MaterialPropertyBlock from
    // EnemyGroundIndicatorView so a single material asset feeds every enemy at once.
    Properties
    {
        // Hit-box footprint: world-space half-axes along X and Z used to draw the
        // shadow disc. Outer ring radii are derived from these in the fragment shader.
        _HitFootprint("Hit Footprint (radiusX, radiusZ, edgeSoftness, shadowAlpha)", Vector) = (0.5, 0.5, 0.08, 1)

        // Ring geometry: distance from shadow outer edge, ring thickness, spacing
        // between health and shield, and a 0/1 flag that hides the outer shield ring.
        _RingParams("Ring Params (distance, thickness, spacing, hasShield)", Vector) = (0.05, 0.08, 0.03, 0)

        // Runtime fill state: normalized health, normalized shield, camera facing
        // angle in radians used to orient the depletion gap, and the world-space
        // outer ring radius used to convert UV into local ellipse coordinates.
        _Fill("Fill (healthN, shieldN, cameraAngleRad, outerRadius)", Vector) = (1, 0, 0, 0.5)

        // Softness controls: ring radial AA, angular AA at arc edges, visible
        // ring arc width in radians, and a 0/1 flag that hides every ring layer.
        _Softness("Softness (ringEdge, ringAngular, ringArcRad, ringsVisible)", Vector) = (0.05, 0.02, 6.28318530718, 1)

        // Independent background alpha multipliers (one per ring) applied on top of
        // _HealthBackgroundColor.a / _ShieldBackgroundColor.a so authors can tune
        // background opacity without touching the saved color asset.
        _BackgroundAlphas("Background Alphas (healthBg, shieldBg, _, _)", Vector) = (1, 1, 0, 0)

        _ShadowColor("Shadow Color", Color) = (0, 0, 0, 0.55)
        _HealthFillColor("Health Fill Color", Color) = (0.92, 0.18, 0.16, 0.95)
        _HealthBackgroundColor("Health Background Color", Color) = (0.04, 0.04, 0.04, 0.7)
        _ShieldFillColor("Shield Fill Color", Color) = (0.25, 0.85, 1, 0.95)
        _ShieldBackgroundColor("Shield Background Color", Color) = (0.04, 0.04, 0.04, 0.7)
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
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex GroundIndicatorVertex
            #pragma fragment GroundIndicatorFragment
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Keep the property block in UnityPerMaterial so SRP Batcher remains valid
            // for the small subset of enemies that share authored material values; per-
            // enemy variation comes from MaterialPropertyBlock which intentionally
            // breaks the batcher for those instances.
            CBUFFER_START(UnityPerMaterial)
                float4 _HitFootprint;
                float4 _RingParams;
                float4 _Fill;
                float4 _Softness;
                float4 _BackgroundAlphas;
                float4 _ShadowColor;
                float4 _HealthFillColor;
                float4 _HealthBackgroundColor;
                float4 _ShieldFillColor;
                float4 _ShieldBackgroundColor;
            CBUFFER_END

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

            // The outer ring radius is resolved on the CPU and passed via _Fill.w so the
            // mesh-side scaling and the shader-side UV unpacking always agree.
            float ResolveOuterRadius()
            {
                return max(0.001, _Fill.w);
            }

            // Wraps an angle into [-PI, PI] without producing NaN at the seam.
            float WrapToPi(float angleRadians)
            {
                float wrapped = angleRadians - 6.28318530718 * floor((angleRadians + 3.14159265359) / 6.28318530718);
                return wrapped;
            }

            // Returns a smooth 0..1 mask describing the radial band of a ring whose
            // inner and outer edges are expressed as normalized footprint distances.
            float ResolveRingBandMask(float normalizedRadius, float innerEdge, float outerEdge, float radialSoftness)
            {
                float halfSoftness = max(0.0001, radialSoftness);
                float innerMask = smoothstep(innerEdge - halfSoftness, innerEdge + halfSoftness, normalizedRadius);
                float outerMask = 1.0 - smoothstep(outerEdge - halfSoftness, outerEdge + halfSoftness, normalizedRadius);
                return innerMask * outerMask;
            }

            // Returns a smooth 0..1 mask for a camera-facing ring arc. Full 360-degree
            // arcs bypass the seam test so legacy rings stay visually continuous.
            float ResolveArcAngularMask(float deltaAngleRadians, float arcRadians, float angularSoftness)
            {
                float resolvedArcRadians = clamp(arcRadians, 0.0, 6.28318530718);

                if (resolvedArcRadians >= 6.28308530718)
                    return 1.0;

                if (resolvedArcRadians <= 0.0001)
                    return 0.0;

                float visibleHalfAngle = resolvedArcRadians * 0.5;
                float halfSoftness = max(0.0001, angularSoftness);
                return 1.0 - smoothstep(visibleHalfAngle - halfSoftness, visibleHalfAngle + halfSoftness, abs(deltaAngleRadians));
            }

            // Returns the visible portion of a ring fill as a smooth 0..1 mask based on
            // the authored ring arc and normalized health/shield value. The fill stays
            // centered toward the camera, matching the existing full-ring depletion style.
            float ResolveFillAngularMask(float deltaAngleRadians, float fillNormalized, float ringArcRadians, float angularSoftness)
            {
                float fillArcRadians = clamp(ringArcRadians, 0.0, 6.28318530718) * saturate(fillNormalized);
                return ResolveArcAngularMask(deltaAngleRadians, fillArcRadians, angularSoftness);
            }

            // Standard premultiplied porter-duff over composition operating in the
            // already-shaded color space used by the pass.
            float4 BlendOver(float4 source, float4 destination)
            {
                float outAlpha = source.a + destination.a * (1.0 - source.a);
                float3 outColor = source.rgb * source.a + destination.rgb * destination.a * (1.0 - source.a);
                if (outAlpha > 0.0)
                    outColor /= outAlpha;
                return float4(outColor, outAlpha);
            }

            Varyings GroundIndicatorVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs vertexInputs = GetVertexPositionInputs(input.positionOS);
                output.positionCS = vertexInputs.positionCS;
                output.uv = input.uv;
                return output;
            }

            float4 GroundIndicatorFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                // Convert UV [0,1] into local ellipse coordinates centered at zero.
                // The view scales the transform so the unit quad spans the outer
                // ring diameter on both axes.
                float outerRadius = ResolveOuterRadius();
                float2 centered = (input.uv - 0.5) * 2.0 * outerRadius;

                // Hit-box normalized radius. Values <= 1 are inside the shadow disc.
                float hitRadiusX = max(0.0001, _HitFootprint.x);
                float hitRadiusZ = max(0.0001, _HitFootprint.y);
                float2 shadowNorm = centered / float2(hitRadiusX, hitRadiusZ);
                float normalizedShadowRadius = length(shadowNorm);
                float shadowEdgeSoftness = max(0.0, _HitFootprint.z);
                float shadowAlphaMultiplier = saturate(_HitFootprint.w);
                float shadowMask = 1.0 - smoothstep(1.0 - shadowEdgeSoftness, 1.0, normalizedShadowRadius);
                float4 shadowSample = float4(_ShadowColor.rgb, _ShadowColor.a * shadowAlphaMultiplier * shadowMask);

                // Normalized radius expressed in shadow-radius units so ring edges can
                // be authored as world-space distances from the shadow.
                float averageHitRadius = max(0.0001, 0.5 * (hitRadiusX + hitRadiusZ));
                float radiusForRing = length(centered);
                float ringDistance = max(0.0, _RingParams.x);
                float ringThickness = max(0.0, _RingParams.y);
                float ringSpacing = max(0.0, _RingParams.z);
                float hasShield = saturate(_RingParams.w);
                float ringsVisible = saturate(_Softness.w);

                float healthInnerRadius = averageHitRadius + ringDistance;
                float healthOuterRadius = healthInnerRadius + ringThickness;
                float shieldInnerRadius = healthOuterRadius + ringSpacing;
                float shieldOuterRadius = shieldInnerRadius + ringThickness;

                float ringRadialSoftness = max(0.0001, _Softness.x * ringThickness);
                // Angular delta to camera facing direction projected on the local XZ plane.
                float pixelAngle = atan2(centered.y, centered.x);
                float cameraAngle = _Fill.z;
                float deltaAngle = WrapToPi(pixelAngle - cameraAngle);

                float ringArcMask = ResolveArcAngularMask(deltaAngle, _Softness.z, _Softness.y);
                float healthBand = ResolveRingBandMask(radiusForRing, healthInnerRadius, healthOuterRadius, ringRadialSoftness) * ringArcMask * ringsVisible;
                float shieldBand = ResolveRingBandMask(radiusForRing, shieldInnerRadius, shieldOuterRadius, ringRadialSoftness) * hasShield * ringArcMask * ringsVisible;
                float healthFillMask = ResolveFillAngularMask(deltaAngle, _Fill.x, _Softness.z, _Softness.y);
                float shieldFillMask = ResolveFillAngularMask(deltaAngle, _Fill.y, _Softness.z, _Softness.y);

                // Compose ring colors: full-ring background underneath, fill on top.
                // Independent background alphas are applied as multipliers so authors can
                // tune track opacity without having to edit the saved color asset.
                float healthBackgroundAlphaMultiplier = saturate(_BackgroundAlphas.x);
                float shieldBackgroundAlphaMultiplier = saturate(_BackgroundAlphas.y);
                float4 healthBackground = float4(_HealthBackgroundColor.rgb, _HealthBackgroundColor.a * healthBackgroundAlphaMultiplier * healthBand);
                float4 healthFill = float4(_HealthFillColor.rgb, _HealthFillColor.a * healthBand * healthFillMask);
                float4 shieldBackground = float4(_ShieldBackgroundColor.rgb, _ShieldBackgroundColor.a * shieldBackgroundAlphaMultiplier * shieldBand);
                float4 shieldFill = float4(_ShieldFillColor.rgb, _ShieldFillColor.a * shieldBand * shieldFillMask);

                // Front-to-back composition with shadow at the bottom and shield on top.
                float4 composed = shadowSample;
                composed = BlendOver(healthBackground, composed);
                composed = BlendOver(healthFill, composed);
                composed = BlendOver(shieldBackground, composed);
                composed = BlendOver(shieldFill, composed);
                return composed;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
