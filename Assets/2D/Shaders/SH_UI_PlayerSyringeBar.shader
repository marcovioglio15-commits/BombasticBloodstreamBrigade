Shader "Custom/UI/PlayerSyringeBar"
{
    Properties
    {
        [PerRendererData] _MainTex("UI White Texture", 2D) = "white" {}
        _Color("Graphic Tint", Color) = (1, 1, 1, 1)

        [Header(Direct Palette)]
        _OutlineColor("Outline Color", Color) = (0.04, 0.025, 0.035, 1)
        _BodyColor("Body Color", Color) = (0.25, 0.11, 0.14, 1)
        _BodyShadowColor("Body Shadow Color", Color) = (0.12, 0.055, 0.08, 1)
        _ChamberColor("Empty Chamber Color", Color) = (0.28, 0.16, 0.24, 0.82)
        _LiquidColor("Liquid Color", Color) = (0.72, 0.16, 0.2, 1)
        _LiquidHighlightColor("Liquid Highlight Color", Color) = (0.95, 0.34, 0.35, 1)
        _BubbleColor("Bubble Color", Color) = (1, 0.58, 0.56, 0.72)
        _GraduationColor("Graduation Color", Color) = (0.52, 0.48, 0.22, 1)
        _PlungerColor("Plunger Color", Color) = (0.16, 0.15, 0.15, 1)
        _PlungerWindowColor("Plunger Window Color", Color) = (0.62, 0.64, 0.62, 0.38)
        _TerminationOutlineColor("Termination Outline Color", Color) = (0.04, 0.025, 0.035, 1)
        _TerminationInteriorColor("Termination Interior Color", Color) = (0.17, 0.075, 0.105, 1)

        [Header(Geometry)]
        _OutlineThickness("Normalized Outline Thickness", Range(0, 0.2)) = 0.035
        _ChamberInset("Normalized Chamber Inset", Range(0, 0.49)) = 0.16
        _EndCapNormalized("Normalized End Cap Width", Range(0, 0.45)) = 0.08
        _GraduationInsetNormalized("Normalized Graduation Start Inset", Range(0, 0.45)) = 0.053
        _GraduationEndNormalized("Normalized Graduation End Position", Range(0.55, 0.999)) = 0.871
        _TerminationOffsetNormalized("Normalized Termination Offset", Range(0, 0.45)) = 0.024
        _PlungerWidth("Normalized Plunger Width", Range(0, 0.2)) = 0.032
        _BodyStyle("Body Style", Float) = 0
        _LabelPlacement("Label Placement", Float) = 0
        _TerminationStyle("Termination Style", Float) = 1
        _MajorDivisionCount("Major Graduation Interval Count", Float) = 5
        _MinorDivisionsPerMajor("Minor Divisions Per Major Interval", Float) = 1

        [Header(Paint Drips)]
        _PaintDripsEnabled("Paint Drips Enabled", Float) = 0
        _PaintDripDensity("Paint Drip Density", Range(0, 1)) = 0.38
        _PaintDripLength("Paint Drip Length", Range(0, 0.5)) = 0.1
        _PaintDripWidth("Paint Drip Width", Range(0.0001, 0.25)) = 0.018
        _PaintDripIrregularity("Paint Drip Irregularity", Range(0, 1)) = 0.65

        [Header(Runtime Value)]
        _FillNormalized("Normalized Authoritative Fill", Range(0, 1)) = 1
        _Slosh("Normalized Reactive Slosh", Range(-1, 1)) = 0
        _ValueImpulse("Normalized Value Change Impulse", Range(-1, 1)) = 0
        _SurfaceSloshStrength("Surface Slosh Strength", Range(0, 1)) = 0.45
        _HorizontalSloshEnabled("Horizontal Slosh Enabled", Float) = 1
        _HorizontalSloshStrength("Horizontal Slosh Strength", Range(0, 0.5)) = 0.16

        [Header(Fluid)]
        _FlowEnabled("Flow Enabled", Float) = 1
        _FlowSpeed("Flow Speed", Float) = 0.45
        _WaveAmplitude("Wave Amplitude", Float) = 0.035
        _WaveFrequency("Wave Frequency", Float) = 3
        _Viscosity("Viscosity", Range(0, 1)) = 0.72
        _BubblesEnabled("Bubbles Enabled", Float) = 1
        _BubbleDensity("Bubble Density", Range(0, 1)) = 0.35
        _BubbleMinimumSize("Bubble Minimum Size", Float) = 0.025
        _BubbleMaximumSize("Bubble Maximum Size", Float) = 0.07
        _BubbleRiseSpeed("Bubble Rise Speed", Float) = 0.18
        _BubbleDrift("Bubble Drift", Float) = 0.12

        [Header(Unity UI)]
        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _ColorMask("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "False"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 worldPosition : TEXCOORD0;
                float2 texcoord : TEXCOORD1;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            fixed4 _Color;
            fixed4 _OutlineColor;
            fixed4 _BodyColor;
            fixed4 _BodyShadowColor;
            fixed4 _ChamberColor;
            fixed4 _LiquidColor;
            fixed4 _LiquidHighlightColor;
            fixed4 _BubbleColor;
            fixed4 _GraduationColor;
            fixed4 _PlungerColor;
            fixed4 _PlungerWindowColor;
            fixed4 _TerminationOutlineColor;
            fixed4 _TerminationInteriorColor;
            float _OutlineThickness;
            float _ChamberInset;
            float _EndCapNormalized;
            float _GraduationInsetNormalized;
            float _GraduationEndNormalized;
            float _TerminationOffsetNormalized;
            float _PlungerWidth;
            float _BodyStyle;
            float _LabelPlacement;
            float _TerminationStyle;
            float _MajorDivisionCount;
            float _MinorDivisionsPerMajor;
            float _PaintDripsEnabled;
            float _PaintDripDensity;
            float _PaintDripLength;
            float _PaintDripWidth;
            float _PaintDripIrregularity;
            float _FillNormalized;
            float _Slosh;
            float _ValueImpulse;
            float _SurfaceSloshStrength;
            float _HorizontalSloshEnabled;
            float _HorizontalSloshStrength;
            float _FlowEnabled;
            float _FlowSpeed;
            float _WaveAmplitude;
            float _WaveFrequency;
            float _Viscosity;
            float _BubblesEnabled;
            float _BubbleDensity;
            float _BubbleMinimumSize;
            float _BubbleMaximumSize;
            float _BubbleRiseSpeed;
            float _BubbleDrift;

            v2f Vert(appdata_t input)
            {
                v2f output;
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            float BoxMask(float2 uv, float2 minimumValue, float2 maximumValue, float softness)
            {
                float2 lower = smoothstep(minimumValue - softness, minimumValue + softness, uv);
                float2 upper = 1.0 - smoothstep(maximumValue - softness, maximumValue + softness, uv);
                return lower.x * lower.y * upper.x * upper.y;
            }

            float BeveledBoxMask(float2 uv, float2 minimumValue, float2 maximumValue, float bevel, float softness)
            {
                float2 center = (minimumValue + maximumValue) * 0.5;
                float2 halfSize = (maximumValue - minimumValue) * 0.5;
                float2 offset = abs(uv - center);
                float rectangularDistance = max(offset.x - halfSize.x, offset.y - halfSize.y);
                float cornerDistance = max(0.0, offset.x - (halfSize.x - bevel)) +
                                       max(0.0, offset.y - (halfSize.y - bevel)) -
                                       bevel;
                return 1.0 - smoothstep(-softness, softness, max(rectangularDistance, cornerDistance));
            }

            float EllipseMask(float2 uv, float2 center, float2 radius, float softness);

            float SyringeTipMask(float2 uv, float bodyEnd, float endCap, float softness)
            {
                float style = round(clamp(_TerminationStyle, 0.0, 3.0));
                float collar = BeveledBoxMask(uv,
                                              float2(bodyEnd - endCap * 0.1, 0.22),
                                              float2(bodyEnd + endCap * 0.2, 0.78),
                                              0.018,
                                              softness);

                if (style < 0.5)
                    return saturate(collar + BoxMask(uv, float2(bodyEnd + endCap * 0.18, 0.34), float2(0.99, 0.66), softness));

                if (style < 1.5)
                {
                    float progress = saturate((uv.x - bodyEnd) / max(0.0001, 1.0 - bodyEnd));
                    float halfHeight = lerp(0.22, 0.025, progress);
                    float angularTip = BoxMask(uv, float2(bodyEnd, 0.2), float2(0.995, 0.8), softness) *
                                       (1.0 - smoothstep(halfHeight - softness, halfHeight + softness, abs(uv.y - 0.5)));
                    return saturate(collar + angularTip);
                }

                if (style < 2.5)
                {
                    float roundedTip = EllipseMask(uv,
                                                   float2(bodyEnd + endCap * 0.32, 0.5),
                                                   float2(endCap * 0.34, 0.22),
                                                   softness);
                    return saturate(collar + roundedTip);
                }

                float needle = BoxMask(uv,
                                       float2(bodyEnd + endCap * 0.1, 0.475),
                                       float2(0.995, 0.525),
                                       softness);
                return saturate(collar + needle);
            }

            float FlatBodyMask(float2 uv, float softness)
            {
                return BoxMask(uv, float2(0.01, 0.08), float2(0.99, 0.94), softness);
            }

            float AngularBodyMask(float2 uv, float endCap, float softness)
            {
                float centerMask = BoxMask(uv, float2(endCap * 0.55, 0.08), float2(1.0 - endCap * 0.55, 0.94), softness);
                float leftCap = BoxMask(uv, float2(0.01, 0.24), float2(endCap, 0.78), softness);
                float rightCap = BoxMask(uv, float2(1.0 - endCap, 0.24), float2(0.99, 0.78), softness);
                float leftAngular = saturate((uv.x + uv.y * endCap * 0.7) / max(endCap, 0.0001));
                float rightAngular = saturate(((1.0 - uv.x) + uv.y * endCap * 0.7) / max(endCap, 0.0001));
                leftCap *= leftAngular;
                rightCap *= rightAngular;
                return saturate(centerMask + leftCap + rightCap);
            }

            float EllipseMask(float2 uv, float2 center, float2 radius, float softness)
            {
                float ellipseDistance = length((uv - center) / max(radius, float2(0.0001, 0.0001)));
                return 1.0 - smoothstep(1.0 - softness * 12.0, 1.0 + softness * 12.0, ellipseDistance);
            }

            float RoundedBodyMask(float2 uv, float endCap, float softness)
            {
                float centerMask = BoxMask(uv, float2(endCap * 0.5, 0.08), float2(1.0 - endCap * 0.5, 0.94), softness);
                float leftCap = EllipseMask(uv, float2(endCap * 0.52, 0.51), float2(endCap * 0.52, 0.43), softness);
                float rightCap = EllipseMask(uv, float2(1.0 - endCap * 0.52, 0.51), float2(endCap * 0.52, 0.43), softness);
                return saturate(centerMask + leftCap + rightCap);
            }

            float NeedleBodyMask(float2 uv, float endCap, float softness)
            {
                float centerMask = BoxMask(uv, float2(0.01, 0.08), float2(1.0 - endCap * 0.72, 0.94), softness);
                float needleProgress = saturate((uv.x - (1.0 - endCap * 0.72)) / max(endCap * 0.71, 0.0001));
                float needleHalfHeight = lerp(0.43, 0.015, needleProgress);
                float needleHorizontalMask = BoxMask(uv,
                                                     float2(1.0 - endCap * 0.72, 0.08),
                                                     float2(0.99, 0.94),
                                                     softness);
                float needleVerticalMask = 1.0 - smoothstep(needleHalfHeight - softness,
                                                           needleHalfHeight + softness,
                                                           abs(uv.y - 0.51));
                return saturate(centerMask + needleHorizontalMask * needleVerticalMask);
            }

            float SyringeBodyMask(float2 uv, float endCap, float softness)
            {
                float style = round(clamp(_TerminationStyle, 0.0, 3.0));

                if (style < 0.5)
                    return FlatBodyMask(uv, softness);

                if (style < 1.5)
                    return AngularBodyMask(uv, endCap, softness);

                if (style < 2.5)
                    return RoundedBodyMask(uv, endCap, softness);

                return NeedleBodyMask(uv, endCap, softness);
            }

            float GraduationTickMask(float chamberX,
                                     float uvY,
                                     float intervalCount,
                                     float tickBottom,
                                     float tickTop,
                                     float width)
            {
                float repeated = frac(chamberX * max(intervalCount, 0.0001));
                float distanceToTick = min(repeated, 1.0 - repeated);
                float tickLine = smoothstep(width, 0.0, distanceToTick);
                float vertical = BoxMask(float2(chamberX, uvY),
                                         float2(0.0, tickBottom),
                                         float2(1.0, tickTop),
                                         0.003);
                return tickLine * vertical;
            }

            float PaintDripMask(float2 uv,
                                float bodyStart,
                                float bodyEnd,
                                float bodyBottom,
                                float softness)
            {
                float normalizedX = saturate((uv.x - bodyStart) / max(0.0001, bodyEnd - bodyStart));
                float density = saturate(_PaintDripDensity);
                float cellCount = lerp(7.0, 18.0, density);
                float scaledX = normalizedX * cellCount;
                float cellIndex = floor(scaledX);
                float cellCenterX = bodyStart + (cellIndex + 0.5) * (bodyEnd - bodyStart) / cellCount;
                float randomValue = Hash21(float2(cellIndex, 3.71));
                float alternateRandom = Hash21(float2(cellIndex, 9.43));
                float active = step(1.0 - saturate(density * 1.25), randomValue);
                float width = max(0.0001,
                                  _PaintDripWidth * lerp(1.0,
                                                         lerp(0.55, 1.4, alternateRandom),
                                                         saturate(_PaintDripIrregularity)));
                float length = max(0.0,
                                   _PaintDripLength * lerp(1.0,
                                                           lerp(0.34, 1.0, randomValue),
                                                           saturate(_PaintDripIrregularity)));
                length = min(length, max(0.01, bodyBottom - softness * 3.0));
                float verticalDistance = bodyBottom - uv.y;
                float progress = saturate(verticalDistance / max(0.0001, length));
                float stemWidth = width * lerp(0.36, 0.18, progress);
                float stemMask = smoothstep(stemWidth + softness,
                                            max(0.0, stemWidth - softness),
                                            abs(uv.x - cellCenterX));
                float verticalMask = smoothstep(-softness, softness, verticalDistance) *
                                     (1.0 - smoothstep(length * 0.8 - softness,
                                                       length * 0.8 + softness,
                                                       verticalDistance));
                float borderPoolMask = EllipseMask(uv,
                                                   float2(cellCenterX, bodyBottom),
                                                   float2(width * lerp(0.58, 0.82, alternateRandom),
                                                          max(0.004, length * 0.055)),
                                                   softness);
                float dropletMask = EllipseMask(uv,
                                                float2(cellCenterX, bodyBottom - length * 0.82),
                                                float2(width * 0.44, max(0.008, length * 0.1)),
                                                softness);
                float horizontalBounds = BoxMask(uv,
                                                 float2(bodyStart, 0.0),
                                                 float2(bodyEnd, bodyBottom + softness),
                                                 softness);
                return active * horizontalBounds * saturate(borderPoolMask + stemMask * verticalMask + dropletMask);
            }

            float BubbleMask(float2 chamberUv, float timeValue)
            {
                float2 gridScale = float2(18.0, 5.0);
                float2 movingUv = chamberUv * gridScale;
                movingUv.y -= timeValue * _BubbleRiseSpeed * gridScale.y;
                movingUv.x += sin((movingUv.y + timeValue) * 0.7) * _BubbleDrift;
                float2 cell = floor(movingUv);
                float2 local = frac(movingUv) - 0.5;
                float randomValue = Hash21(cell);
                float densityMask = step(1.0 - saturate(_BubbleDensity), randomValue);
                float radius = lerp(_BubbleMinimumSize, _BubbleMaximumSize, Hash21(cell + 4.17));
                float normalizedRadius = radius * gridScale.y;
                float ring = smoothstep(normalizedRadius, normalizedRadius * 0.62, length(local));
                return ring * densityMask;
            }

            fixed4 Frag(v2f input) : SV_Target
            {
                float2 uv = input.texcoord;
                float softness = 0.0025;
                float endCap = clamp(_EndCapNormalized, 0.001, 0.45);
                float outlineThickness = max(0.006, _OutlineThickness * 0.42);
                float detailedStyle = step(0.5, round(clamp(_BodyStyle, 0.0, 1.0)));
                float simplifiedStyle = 1.0 - detailedStyle;
                float externalLabels = step(0.5, round(clamp(_LabelPlacement, 0.0, 1.0)));
                float bodyStart = lerp(endCap * 0.34, endCap * 0.82, detailedStyle);
                float bodyEnd = lerp(1.0 - endCap * 0.34, 1.0 - endCap * 0.72, detailedStyle);
                float bodyBottom = lerp(0.12, 0.045, detailedStyle);
                float bodyTop = lerp(0.88, 0.955, detailedStyle);
                float bodyBevel = lerp(0.008, 0.045, detailedStyle);
                float bodyInteriorBevel = lerp(0.004, 0.035, detailedStyle);
                float graduationStart = clamp(_GraduationInsetNormalized, 0.001, 0.45);
                float graduationEnd = clamp(_GraduationEndNormalized,
                                            graduationStart + 0.05,
                                            0.999);
                float simplifiedTerminationStart = min(graduationEnd + _TerminationOffsetNormalized,
                                                       1.0 - softness);
                float rectangularBodyEnd = lerp(simplifiedTerminationStart, bodyEnd, detailedStyle);
                float bodyMask = BeveledBoxMask(uv,
                                                float2(bodyStart, bodyBottom),
                                                float2(rectangularBodyEnd, bodyTop),
                                                bodyBevel,
                                                softness);
                float bodyInteriorMask = BeveledBoxMask(uv,
                                                        float2(bodyStart + outlineThickness, bodyBottom + outlineThickness),
                                                        float2(rectangularBodyEnd - outlineThickness, bodyTop - outlineThickness),
                                                        bodyInteriorBevel,
                                                        softness);
                float leftStemMask = BoxMask(uv,
                                             float2(0.045, 0.425),
                                             float2(bodyStart + 0.018, 0.575),
                                             softness) * detailedStyle;
                float leftFlangeMask = BeveledBoxMask(uv,
                                                      float2(0.012, 0.18),
                                                      float2(0.07, 0.82),
                                                      0.015,
                                                      softness) * detailedStyle;
                float leftFlangeInteriorMask = BeveledBoxMask(uv,
                                                              float2(0.022, 0.22),
                                                              float2(0.058, 0.78),
                                                              0.009,
                                                              softness);
                float leftFlangeTrimMask = saturate(leftFlangeMask - leftFlangeInteriorMask);
                float leftStemTrimMask = BoxMask(uv,
                                                 float2(0.05, 0.415),
                                                 float2(bodyStart + 0.02, 0.445),
                                                 softness);
                leftStemTrimMask += BoxMask(uv,
                                            float2(0.05, 0.555),
                                            float2(bodyStart + 0.02, 0.585),
                                            softness);
                leftStemTrimMask *= detailedStyle;
                float tipMask = SyringeTipMask(uv, bodyEnd, endCap, softness) * detailedStyle;
                float paintDripMask = 0.0;

                if (_PaintDripsEnabled > 0.5)
                {
                    paintDripMask = PaintDripMask(uv,
                                                   bodyStart,
                                                   rectangularBodyEnd,
                                                   bodyBottom,
                                                   softness);
                }

                float completeBodyMask = saturate(bodyMask + leftStemMask + leftFlangeMask + tipMask + paintDripMask);
                float simplifiedRightBodyEdgeMask = BoxMask(uv,
                                                             float2(simplifiedTerminationStart - outlineThickness * 1.4,
                                                                    bodyBottom + outlineThickness),
                                                             float2(simplifiedTerminationStart + softness * 2.0,
                                                                    bodyTop - outlineThickness),
                                                             softness) * simplifiedStyle;
                float bodyOutlineMask = saturate((bodyMask - bodyInteriorMask) +
                                                 (leftFlangeMask - leftFlangeInteriorMask) +
                                                 paintDripMask -
                                                 simplifiedRightBodyEdgeMask);
                float detailedChamberStart = bodyStart + endCap * 0.18 + _ChamberInset * 0.035;
                float chamberStart = lerp(graduationStart, detailedChamberStart, detailedStyle);
                float chamberEnd = lerp(simplifiedTerminationStart,
                                        bodyEnd - endCap * 0.26 - _ChamberInset * 0.035,
                                        detailedStyle);
                float chamberBottom = lerp(0.18, 0.37, externalLabels);
                float chamberTop = lerp(0.82, 0.79, detailedStyle);
                float chamberFrameEnd = lerp(simplifiedTerminationStart,
                                             chamberEnd + 0.012,
                                             detailedStyle);
                float chamberFrameMask = BoxMask(uv,
                                                 float2(chamberStart - 0.012, chamberBottom - 0.035),
                                                 float2(chamberFrameEnd, chamberTop + 0.035),
                                                 softness);
                float chamberMask = BoxMask(uv,
                                            float2(chamberStart, chamberBottom),
                                            float2(chamberEnd, chamberTop),
                                            softness);
                float chamberInteriorMask = chamberMask * bodyInteriorMask;
                float simplifiedRightChamberEdgeMask = BoxMask(uv,
                                                                float2(simplifiedTerminationStart - outlineThickness * 1.4,
                                                                       chamberBottom),
                                                                float2(simplifiedTerminationStart + softness * 2.0,
                                                                       chamberTop),
                                                                softness) * simplifiedStyle;
                float chamberBorderMask = saturate(chamberFrameMask -
                                                   chamberMask -
                                                   simplifiedRightChamberEdgeMask);
                float majorTickBottom = lerp(chamberBottom + 0.025, 0.205, externalLabels);
                float majorTickTop = lerp(chamberBottom + 0.15, 0.345, externalLabels);
                float simplifiedTerminationEnd = 1.0 - softness;
                float simplifiedTerminationOuterMask = BoxMask(uv,
                                                                float2(simplifiedTerminationStart, bodyBottom),
                                                                float2(simplifiedTerminationEnd, bodyTop),
                                                                softness) * simplifiedStyle;
                float simplifiedTerminationInteriorMask = BoxMask(uv,
                                                                   float2(simplifiedTerminationStart + outlineThickness,
                                                                          bodyBottom + outlineThickness),
                                                                   float2(simplifiedTerminationEnd - outlineThickness,
                                                                          bodyTop - outlineThickness),
                                                                   softness) * simplifiedStyle;
                float simplifiedTerminationOutlineMask = saturate(simplifiedTerminationOuterMask -
                                                                   simplifiedTerminationInteriorMask);
                float graduationPanelMask = BoxMask(uv,
                                                    float2(chamberStart - 0.012, 0.095),
                                                    float2(chamberEnd + 0.012, 0.315),
                                                    softness) * externalLabels;
                float graduationSeparatorMask = BoxMask(uv,
                                                        float2(chamberStart - 0.012, 0.3),
                                                        float2(chamberEnd + 0.012, 0.32),
                                                        softness) * externalLabels;
                float collarMask = BeveledBoxMask(uv,
                                                  float2(chamberEnd + 0.018, 0.25),
                                                  float2(bodyEnd + endCap * 0.06, 0.79),
                                                  0.018,
                                                  softness) * detailedStyle;
                float collarInsetMask = BoxMask(uv,
                                                float2(chamberEnd + 0.032, 0.29),
                                                float2(bodyEnd - 0.004, 0.75),
                                                softness);
                float collarBandMask = saturate(collarMask - collarInsetMask) * detailedStyle;
                float2 chamberUv = float2(saturate((uv.x - chamberStart) / max(0.0001, chamberEnd - chamberStart)),
                                          saturate((uv.y - chamberBottom) / max(0.0001, chamberTop - chamberBottom)));
                float valueTrackX = saturate((uv.x - graduationStart) / max(0.0001, graduationEnd - graduationStart));
                float valueTrackMask = BoxMask(uv,
                                               float2(graduationStart, 0.0),
                                               float2(graduationEnd, 1.0),
                                               softness);
                float slosh = clamp(_Slosh + _ValueImpulse, -1.0, 1.0);
                float horizontalSlosh = slosh *
                                        max(0.0, _HorizontalSloshStrength) *
                                        step(0.5, _HorizontalSloshEnabled);
                float timeValue = _Time.y * _FlowEnabled;
                float wave = sin((valueTrackX * max(0.1, _WaveFrequency) + timeValue * _FlowSpeed) * 6.2831853);
                float secondaryWave = sin((valueTrackX * max(0.1, _WaveFrequency * 0.47) - timeValue * _FlowSpeed * 0.63) * 6.2831853);
                float surface = 0.88 + wave * _WaveAmplitude + secondaryWave * _WaveAmplitude * 0.45;
                surface += slosh *
                           (valueTrackX - 0.5) *
                           max(0.0, _SurfaceSloshStrength) *
                           lerp(1.25, 0.75, saturate(_Viscosity));
                float displacedFill = clamp(_FillNormalized + horizontalSlosh, 0.0, 1.0);
                float liquidHorizontalMask = 1.0 - smoothstep(displacedFill - 0.003,
                                                              displacedFill + 0.003,
                                                              valueTrackX);
                float liquidSurfaceMask = 1.0 - smoothstep(surface - 0.015, surface + 0.015, chamberUv.y);
                float liquidMask = chamberInteriorMask * valueTrackMask * liquidHorizontalMask * liquidSurfaceMask;
                float liquidFacet = step(0.5, frac((valueTrackX - horizontalSlosh) * 7.0 +
                                                   chamberUv.y * 4.0 +
                                                   timeValue * _FlowSpeed * 0.2));
                float surfaceHighlight = smoothstep(surface - 0.06, surface, chamberUv.y) * liquidMask;
                float2 bubbleUv = float2(valueTrackX - horizontalSlosh, chamberUv.y);
                float bubbleMask = BubbleMask(bubbleUv, _Time.y) * liquidMask * _BubblesEnabled;
                float plungerCenter = lerp(graduationStart, graduationEnd, saturate(_FillNormalized));
                float plungerOuterHalfWidth = _PlungerWidth * lerp(0.75, 0.72, detailedStyle);
                float plungerInnerHalfWidth = _PlungerWidth * lerp(0.5, 0.36, detailedStyle);
                float plungerGrooveHalfWidth = _PlungerWidth * lerp(0.1, 0.12, detailedStyle);
                float plungerOuterBottom = lerp(bodyBottom + outlineThickness * 0.4,
                                                chamberBottom - 0.045,
                                                detailedStyle);
                float plungerOuterTop = lerp(bodyTop - outlineThickness * 0.4,
                                             chamberTop + 0.045,
                                             detailedStyle);
                float plungerInnerBottom = lerp(bodyBottom + outlineThickness * 1.05,
                                                chamberBottom - 0.03,
                                                detailedStyle);
                float plungerInnerTop = lerp(bodyTop - outlineThickness * 1.05,
                                             chamberTop + 0.03,
                                             detailedStyle);
                float plungerOutlineMask = BoxMask(uv,
                                                   float2(plungerCenter - plungerOuterHalfWidth, plungerOuterBottom),
                                                   float2(plungerCenter + plungerOuterHalfWidth, plungerOuterTop),
                                                   softness);
                float plungerMask = BoxMask(uv,
                                            float2(plungerCenter - plungerInnerHalfWidth, plungerInnerBottom),
                                            float2(plungerCenter + plungerInnerHalfWidth, plungerInnerTop),
                                            softness);
                float plungerWindowHalfWidth = max(plungerGrooveHalfWidth,
                                                   plungerInnerHalfWidth * 0.88);
                float plungerWindowMask = BoxMask(uv,
                                                  float2(plungerCenter - plungerWindowHalfWidth, plungerInnerBottom),
                                                  float2(plungerCenter + plungerWindowHalfWidth, plungerInnerTop),
                                                  softness) * simplifiedStyle;
                float plungerOutlineFrameMask = saturate(plungerOutlineMask - plungerWindowMask);
                float plungerFrameMask = saturate(plungerMask - plungerWindowMask);
                float graduationX = valueTrackX;
                float majorDivisionCount = max(1.0, _MajorDivisionCount);
                float minorDivisionCount = majorDivisionCount * max(1.0, _MinorDivisionsPerMajor);
                float minorTickBottom = lerp(chamberBottom + 0.025, 0.255, externalLabels);
                float minorTickTop = lerp(chamberBottom + 0.095, 0.345, externalLabels);
                float majorTicks = GraduationTickMask(graduationX,
                                                      uv.y,
                                                      majorDivisionCount,
                                                      majorTickBottom,
                                                      majorTickTop,
                                                      min(0.12, 0.012 * majorDivisionCount));
                majorTicks *= step(0.5 / majorDivisionCount, graduationX);
                float minorTicks = GraduationTickMask(graduationX,
                                                      uv.y,
                                                      minorDivisionCount,
                                                      minorTickBottom,
                                                      minorTickTop,
                                                      min(0.12, 0.004 * minorDivisionCount));
                minorTicks *= step(0.5 / minorDivisionCount, graduationX);
                float endpointTicks = BoxMask(uv,
                                              float2(graduationEnd - 0.004, majorTickBottom),
                                              float2(graduationEnd + 0.004, majorTickTop),
                                              softness);
                float graduationHorizontalMask = valueTrackMask;
                float ticksSurfaceMask = lerp(chamberMask, graduationPanelMask, externalLabels);
                float repeatedTicksMask = ticksSurfaceMask *
                                          graduationHorizontalMask *
                                          saturate(majorTicks + minorTicks * 0.72);
                float endpointTicksMask = ticksSurfaceMask * endpointTicks;
                float ticksMask = saturate(repeatedTicksMask + endpointTicksMask);
                float topTrimMask = BoxMask(uv,
                                            float2(bodyStart + 0.015, 0.88),
                                            float2(bodyEnd - 0.015, 0.915),
                                            softness) * detailedStyle;
                float bottomTrimMask = BoxMask(uv,
                                               float2(bodyStart + 0.015, 0.065),
                                               float2(bodyEnd - 0.015, 0.095),
                                               softness) * detailedStyle;
                float trimMask = saturate(topTrimMask +
                                          bottomTrimMask +
                                           graduationSeparatorMask +
                                           collarBandMask +
                                           leftFlangeTrimMask +
                                          leftStemTrimMask);
                float facetedShade = step(0.53, uv.y + sin(uv.x * 11.0) * 0.02);
                float3 bodyColor = lerp(_BodyShadowColor.rgb, _BodyColor.rgb, facetedShade);
                float3 liquidColor = lerp(_LiquidColor.rgb, _LiquidHighlightColor.rgb, liquidFacet * 0.18 + surfaceHighlight * 0.65);
                float3 finalColor = bodyColor;
                float finalAlpha = completeBodyMask * _BodyColor.a;
                finalColor = lerp(finalColor, _OutlineColor.rgb, bodyOutlineMask * _OutlineColor.a);
                finalAlpha = max(finalAlpha, bodyOutlineMask * _OutlineColor.a);
                finalColor = lerp(finalColor, _BodyShadowColor.rgb, graduationPanelMask * _BodyShadowColor.a);
                finalAlpha = max(finalAlpha, graduationPanelMask * _BodyShadowColor.a);
                finalColor = lerp(finalColor, _OutlineColor.rgb, chamberBorderMask * _OutlineColor.a);
                finalAlpha = max(finalAlpha, chamberBorderMask * _OutlineColor.a);
                finalColor = lerp(finalColor, _ChamberColor.rgb, chamberInteriorMask * _ChamberColor.a);
                finalAlpha = max(finalAlpha, chamberInteriorMask * _ChamberColor.a);
                finalColor = lerp(finalColor, _TerminationInteriorColor.rgb, simplifiedTerminationInteriorMask * _TerminationInteriorColor.a);
                finalAlpha = max(finalAlpha, simplifiedTerminationInteriorMask * _TerminationInteriorColor.a);
                finalColor = lerp(finalColor, _TerminationOutlineColor.rgb, simplifiedTerminationOutlineMask * _TerminationOutlineColor.a);
                finalAlpha = max(finalAlpha, simplifiedTerminationOutlineMask * _TerminationOutlineColor.a);
                finalColor = lerp(finalColor, liquidColor, liquidMask * _LiquidColor.a);
                finalAlpha = max(finalAlpha, liquidMask * _LiquidColor.a);
                finalColor = lerp(finalColor, _BubbleColor.rgb, bubbleMask * _BubbleColor.a);
                finalAlpha = max(finalAlpha, bubbleMask * _BubbleColor.a);
                finalColor = lerp(finalColor, _OutlineColor.rgb, plungerOutlineFrameMask * _OutlineColor.a);
                finalAlpha = max(finalAlpha, plungerOutlineFrameMask * _OutlineColor.a);
                finalColor = lerp(finalColor, _PlungerColor.rgb, plungerFrameMask * _PlungerColor.a);
                finalAlpha = max(finalAlpha, plungerFrameMask * _PlungerColor.a);
                finalColor = lerp(finalColor, _PlungerWindowColor.rgb, plungerWindowMask * _PlungerWindowColor.a);
                finalAlpha = max(finalAlpha, plungerWindowMask * _PlungerWindowColor.a);
                finalColor = lerp(finalColor, _GraduationColor.rgb, ticksMask * _GraduationColor.a);
                finalAlpha = max(finalAlpha, ticksMask * _GraduationColor.a);
                finalColor = lerp(finalColor, _GraduationColor.rgb, trimMask * _GraduationColor.a);
                finalAlpha = max(finalAlpha, trimMask * _GraduationColor.a);
                finalAlpha *= input.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                finalAlpha *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(finalAlpha - 0.001);
                #endif

                return fixed4(finalColor * input.color.rgb * finalAlpha, finalAlpha);
            }
            ENDCG
        }
    }
}
