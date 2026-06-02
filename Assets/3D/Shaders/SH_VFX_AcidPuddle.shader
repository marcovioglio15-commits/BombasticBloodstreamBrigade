// =====================================================================================================================
// Stylized posterized acid puddle VFX shader. One shader, four particle roles (puddle disc, rising bubble, popping
// ring, gas mist). Mirrors the posterize energy framework already used by SH_LiquidAntibioticBeam so every layer of
// PF_VFX_AcidPuddle keeps the same cel-shaded 2-3 step look as the rest of the project's art style.
// =====================================================================================================================
Shader "Custom/VFX/AcidPuddle"
{
    Properties
    {
        // ---------------------------------------------------------------------------------------------------- Role
        // 0 = liquid puddle disc, 1 = rising bubble, 2 = bubble pop ring, 3 = gas mist plume.
        [Header(Role)] [Enum(PuddleBody,0,Bubble,1,PopRing,2,GasMist,3)] _PuddleRole("Puddle Role", Float) = 0

        // -------------------------------------------------------------------------------------------------- Palette
        // Dark acid base (deep pool tone, used in puddle valleys and bubble shadows).
        [Header(Palette)] _DeepColor("Deep Color", Color) = (0.08, 0.28, 0.04, 1.0)
        // Mid acid green (puddle body and bubble belly).
        _BodyColor("Body Color", Color) = (0.30, 0.78, 0.16, 1.0)
        // Bright neon green / chartreuse highlight (rim of bubbles, top of swirls, pop core).
        _HighlightColor("Highlight Color", Color) = (0.78, 1.00, 0.36, 1.0)
        // Near-white sheen used for the topmost cel step on bubbles and on pop flashes.
        _RimColor("Rim Color", Color) = (0.96, 1.00, 0.74, 1.0)

        // ----------------------------------------------------------------------------------------------- Posterize
        // Number of discrete cel steps (2-4 keeps the chunky art style).
        [Header(Posterize)] _PosterizeSteps("Posterize Steps", Range(2, 6)) = 3
        // Blend between continuous and posterized output (1 = fully cel, 0 = continuous).
        _PosterizeBlend("Posterize Blend", Range(0, 1)) = 1

        // --------------------------------------------------------------------------------------------------- Shape
        // Edge softness for the puddle disc and mist plume (0 = razor sharp, 1 = very feathered).
        [Header(Shape)] _EdgeFeather("Edge Feather", Range(0.001, 1)) = 0.18
        // Inner bright core extent of the puddle disc (0 = no core, 1 = whole disc is core).
        _CoreSize("Puddle Core Size", Range(0, 1)) = 0.45
        // Width of the bubble rim highlight (in normalized radius units).
        _BubbleRimWidth("Bubble Rim Width", Range(0.01, 0.5)) = 0.18
        // Width of the bubble specular sheen sitting on top of the rim.
        _BubbleSheenSize("Bubble Sheen Size", Range(0.01, 0.5)) = 0.14
        // Pop ring thickness in normalized radius (smaller = thinner expanding ring).
        _PopRingThickness("Pop Ring Thickness", Range(0.02, 0.6)) = 0.16
        // Pop ring droplet count (radial fleck count around the ring).
        _PopDropletFrequency("Pop Droplet Frequency", Range(0, 24)) = 8

        // -------------------------------------------------------------------------------------------------- Motion
        // Speed of the swirling distortion inside the puddle disc.
        [Header(Motion)] _SwirlSpeed("Puddle Swirl Speed", Range(0, 6)) = 0.6
        // Strength of the swirl warp applied to the puddle UV.
        _SwirlStrength("Puddle Swirl Strength", Range(0, 1)) = 0.32
        // Frequency of concentric ripples on the puddle disc.
        _RippleFrequency("Puddle Ripple Frequency", Range(0, 24)) = 6
        // Speed of mist convection (vertical scroll for the gas plume).
        _MistDriftSpeed("Mist Drift Speed", Range(0, 4)) = 0.45

        // ------------------------------------------------------------------------------------------------- Opacity
        // Master opacity multiplier (applied last before posterization clamp).
        [Header(Opacity)] _Opacity("Opacity", Range(0, 1)) = 0.92
        // Emissive boost on the body color (HDR-friendly).
        _Emission("Emission Boost", Range(0, 4)) = 1.2
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ForwardUnlit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_particles
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ----------------------------------------------------------------------------------------------------
            // Vertex inputs include particle color via COLOR semantic, so each layer is multiplicatively tinted by
            // the ParticleSystem's start-color / color-over-lifetime modules without needing a custom data stream.
            // ----------------------------------------------------------------------------------------------------
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 particleColor : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half _PuddleRole;
                half4 _DeepColor;
                half4 _BodyColor;
                half4 _HighlightColor;
                half4 _RimColor;
                half _PosterizeSteps;
                half _PosterizeBlend;
                half _EdgeFeather;
                half _CoreSize;
                half _BubbleRimWidth;
                half _BubbleSheenSize;
                half _PopRingThickness;
                half _PopDropletFrequency;
                half _SwirlSpeed;
                half _SwirlStrength;
                half _RippleFrequency;
                half _MistDriftSpeed;
                half _Opacity;
                half _Emission;
            CBUFFER_END

            // =================================================================================================
            // Hash / noise utilities. Procedural so no texture binding is required and the prefab stays portable.
            // =================================================================================================
            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 345.45));
                value += dot(value, value + 34.345);
                return frac(value.x * value.y);
            }

            float ValueNoise2D(float2 position)
            {
                float2 cellOrigin = floor(position);
                float2 cellFraction = frac(position);
                float2 smoothFraction = cellFraction * cellFraction * (3.0 - 2.0 * cellFraction);
                float cornerA = Hash21(cellOrigin);
                float cornerB = Hash21(cellOrigin + float2(1.0, 0.0));
                float cornerC = Hash21(cellOrigin + float2(0.0, 1.0));
                float cornerD = Hash21(cellOrigin + float2(1.0, 1.0));
                float interpolationX0 = lerp(cornerA, cornerB, smoothFraction.x);
                float interpolationX1 = lerp(cornerC, cornerD, smoothFraction.x);
                return lerp(interpolationX0, interpolationX1, smoothFraction.y);
            }

            // =================================================================================================
            // Posterize energy framework (shared visual language with SH_LiquidAntibioticBeam). Quantizes the
            // perceptual energy of the color so highlights and shadows snap to a small number of cel steps.
            // =================================================================================================
            float EncodePosterizeEnergy(float energy)
            {
                return energy / (1.0 + max(0.0, energy));
            }

            float DecodePosterizeEnergy(float encodedEnergy)
            {
                float clampedEncodedEnergy = min(saturate(encodedEnergy), 0.999);
                return clampedEncodedEnergy / max(0.0001, 1.0 - clampedEncodedEnergy);
            }

            float QuantizePosterizeEnergy(float encodedEnergy, float posterizeSteps)
            {
                float stepCount = max(2.0, posterizeSteps);
                float divisor = max(1.0, stepCount - 1.0);
                return floor(saturate(encodedEnergy) * divisor + 0.5) / divisor;
            }

            half3 ApplyPosterizeColor(half3 colorValue, half posterizeSteps, half posterizeBlend)
            {
                float blendAmount = saturate(posterizeBlend);

                if (blendAmount <= 0.0)
                    return colorValue;

                float luminance = max(0.0, dot(colorValue, float3(0.299, 0.587, 0.114)));
                float encodedLuminance = EncodePosterizeEnergy(luminance);
                float quantizedEncoded = QuantizePosterizeEnergy(encodedLuminance, posterizeSteps);
                float quantizedLuminance = DecodePosterizeEnergy(quantizedEncoded);
                float scale = luminance > 0.0001 ? quantizedLuminance / luminance : 0.0;
                half3 posterizedColor = colorValue * scale;
                return lerp(colorValue, posterizedColor, blendAmount);
            }

            // =================================================================================================
            // Vertex pass-through. Particle system already supplies billboarded vertices in object space so we
            // only need to forward UV, color and world position for screen-stable noise sampling.
            // =================================================================================================
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = input.uv;
                output.particleColor = input.color;
                return output;
            }

            // =================================================================================================
            // Puddle body fragment. Renders a flat radial disc with concentric ripples and a slow domain-warp
            // swirl. Uses three palette stops (deep / body / highlight) plus a softened edge feather.
            // =================================================================================================
            half4 RenderPuddleBody(Varyings input)
            {
                float2 centeredUv = input.uv * 2.0 - 1.0;
                float radius = length(centeredUv);
                float timeValue = _Time.y;

                // Swirl warp: rotate uv by an angle that depends on radius for a slow spiral.
                float swirlAngle = (1.0 - radius) * _SwirlStrength * 3.14159 + timeValue * _SwirlSpeed;
                float cosSwirl = cos(swirlAngle);
                float sinSwirl = sin(swirlAngle);
                float2 swirledUv = float2(centeredUv.x * cosSwirl - centeredUv.y * sinSwirl,
                                          centeredUv.x * sinSwirl + centeredUv.y * cosSwirl);

                // Domain-warped noise gives the chunky blotchy variation across the puddle.
                float2 warp = float2(ValueNoise2D(swirledUv * 3.4 + timeValue * 0.18),
                                     ValueNoise2D(swirledUv * 3.4 - timeValue * 0.21 + 5.7));
                float blotch = ValueNoise2D(swirledUv * 2.2 + warp * 1.6);

                // Concentric ripples scrolling slowly outward.
                float ripplePhase = radius * _RippleFrequency - timeValue * _SwirlSpeed * 1.4;
                float ripple = 0.5 + 0.5 * sin(ripplePhase * 3.14159);

                // Cel weights between deep / body / highlight using two soft thresholds.
                float coreMask = smoothstep(_CoreSize + 0.18, _CoreSize - 0.18, radius);
                float midMask = smoothstep(0.94, 0.04, radius);
                float blotchWeight = saturate(blotch * 0.85 + ripple * 0.4);
                half3 baseColor = lerp(_DeepColor.rgb, _BodyColor.rgb, midMask);
                half3 coreColor = lerp(baseColor, _HighlightColor.rgb, saturate(coreMask + blotchWeight * 0.35) * 0.7);

                // Soft outer edge feather to avoid hard disc boundary.
                float edgeMask = smoothstep(1.0, 1.0 - max(0.001, _EdgeFeather), radius);
                half3 outputColor = coreColor * _Emission;
                half alpha = saturate(edgeMask * _Opacity * input.particleColor.a);
                outputColor = ApplyPosterizeColor(outputColor * input.particleColor.rgb, _PosterizeSteps, _PosterizeBlend);
                return half4(outputColor, alpha);
            }

            // =================================================================================================
            // Rising bubble fragment. Two-tone bubble: dark belly + bright rim ring + tiny specular sheen at
            // the upper-left. All masks are quantized via the posterize pass for cel consistency.
            // =================================================================================================
            half4 RenderBubble(Varyings input)
            {
                float2 centeredUv = input.uv * 2.0 - 1.0;
                float radius = length(centeredUv);
                float bubbleMask = smoothstep(1.0, 0.95, radius);
                float bellyMask = smoothstep(1.0 - _BubbleRimWidth, 0.0, radius);
                float rimMask = saturate(bubbleMask - bellyMask);

                // Specular sheen positioned upper-left for a consistent light direction.
                float2 sheenOffset = centeredUv - float2(-0.32, 0.36);
                float sheenMask = smoothstep(_BubbleSheenSize, _BubbleSheenSize * 0.25, length(sheenOffset));

                half3 outputColor = _DeepColor.rgb * bellyMask;
                outputColor += _BodyColor.rgb * bellyMask * 0.6;
                outputColor += _HighlightColor.rgb * rimMask * 1.1;
                outputColor += _RimColor.rgb * sheenMask * 0.9;
                outputColor *= _Emission;

                half alpha = saturate(bubbleMask * _Opacity * input.particleColor.a);
                outputColor = ApplyPosterizeColor(outputColor * input.particleColor.rgb, _PosterizeSteps, _PosterizeBlend);
                return half4(outputColor, alpha);
            }

            // =================================================================================================
            // Pop ring fragment. Expanding cel ring with radial droplet flecks. Particle UV.y (lifetime when the
            // ParticleSystem custom data is wired through) is used as expansion progress; falls back to radius.
            // =================================================================================================
            half4 RenderPopRing(Varyings input)
            {
                float2 centeredUv = input.uv * 2.0 - 1.0;
                float radius = length(centeredUv);
                float angle = atan2(centeredUv.y, centeredUv.x);
                float halfThickness = max(0.005, _PopRingThickness * 0.5);
                float ringMask = smoothstep(halfThickness, 0.0, abs(radius - (1.0 - halfThickness)));

                // Radial droplets: cel flecks at regular angular positions.
                float dropletPhase = angle * max(1.0, _PopDropletFrequency) * 0.5 / 3.14159;
                float dropletMask = smoothstep(0.6, 1.0, sin(dropletPhase * 6.28318) * 0.5 + 0.5) * ringMask;

                // Inner soft flash for the very first frames of the pop.
                float innerFlash = smoothstep(0.55, 0.0, radius) * 0.6;

                half3 outputColor = _HighlightColor.rgb * ringMask;
                outputColor += _RimColor.rgb * dropletMask * 1.4;
                outputColor += _BodyColor.rgb * innerFlash;
                outputColor *= _Emission;

                half alpha = saturate((ringMask + dropletMask * 0.8 + innerFlash * 0.5) *
                                      _Opacity * input.particleColor.a);
                outputColor = ApplyPosterizeColor(outputColor * input.particleColor.rgb, _PosterizeSteps, _PosterizeBlend);
                return half4(outputColor, alpha);
            }

            // =================================================================================================
            // Gas mist fragment. Soft volumetric-looking blob built from value noise; biased upward to read as
            // rising gas above the puddle. Posterized to 2-3 steps so it does not look like a fuzzy gradient.
            // =================================================================================================
            half4 RenderGasMist(Varyings input)
            {
                float2 centeredUv = input.uv * 2.0 - 1.0;
                float radius = length(centeredUv);
                float timeValue = _Time.y;

                // Two noise layers drifting upward at different speeds for parallax.
                float2 noiseUvA = centeredUv * 1.6 + float2(0.0, -timeValue * _MistDriftSpeed * 1.0);
                float2 noiseUvB = centeredUv * 2.7 + float2(0.12, -timeValue * _MistDriftSpeed * 1.6);
                float noiseValue = saturate(ValueNoise2D(noiseUvA) * 0.65 + ValueNoise2D(noiseUvB) * 0.45);

                // Radial fade plus subtle upward bias so mist reads as rising.
                float verticalBias = saturate(0.55 + centeredUv.y * 0.45);
                float bodyMask = smoothstep(1.0, 0.2, radius) * verticalBias;
                float mistMask = saturate(noiseValue * bodyMask * 1.8);

                half3 outputColor = lerp(_BodyColor.rgb, _HighlightColor.rgb, mistMask);
                outputColor *= _Emission * 0.7;

                half alpha = saturate(mistMask * _Opacity * input.particleColor.a);
                outputColor = ApplyPosterizeColor(outputColor * input.particleColor.rgb, _PosterizeSteps, _PosterizeBlend);
                return half4(outputColor, alpha);
            }

            // =================================================================================================
            // Role dispatcher. Branching on a uniform is free on every GPU we ship to and keeps one .shader file
            // covering every layer of PF_VFX_AcidPuddle, matching the prevailing project pattern.
            // =================================================================================================
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                if (_PuddleRole < 0.5)
                    return RenderPuddleBody(input);

                if (_PuddleRole < 1.5)
                    return RenderBubble(input);

                if (_PuddleRole < 2.5)
                    return RenderPopRing(input);

                return RenderGasMist(input);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
