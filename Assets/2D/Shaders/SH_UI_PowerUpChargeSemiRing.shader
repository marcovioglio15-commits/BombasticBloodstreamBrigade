Shader "Custom/UI/PowerUpChargeSemiRing"
{
    Properties
    {
        [PerRendererData] _MainTex("UI White Texture", 2D) = "white" {}
        _Color("Graphic Tint", Color) = (1, 1, 1, 1)
        _BackgroundColor("Background Color", Color) = (0.08, 0.075, 0.055, 0.78)
        _FillColor("Fill Color", Color) = (1, 0.86, 0.02, 1)
        _OutlineColor("Outline Color", Color) = (0.035, 0.03, 0.025, 1)
        _FillNormalized("Fill Normalized", Range(0, 1)) = 0
        _Thickness("Ring Thickness", Range(0.02, 0.6)) = 0.18
        _OutlineThickness("Outline Thickness", Range(0, 0.2)) = 0.035
        _StartAngleDegrees("Start Angle Degrees", Range(-360, 360)) = 110
        _ArcDegrees("Arc Degrees", Range(10, 360)) = 140
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
            fixed4 _BackgroundColor;
            fixed4 _FillColor;
            fixed4 _OutlineColor;
            float _FillNormalized;
            float _Thickness;
            float _OutlineThickness;
            float _StartAngleDegrees;
            float _ArcDegrees;

            v2f Vert(appdata_t input)
            {
                v2f output;
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 Frag(v2f input) : SV_Target
            {
                float2 centered = input.texcoord * 2.0 - 1.0;
                float radius = length(centered);
                float angle = degrees(atan2(centered.y, centered.x));
                float normalizedAngle = fmod(angle - _StartAngleDegrees + 720.0, 360.0);
                float arc = clamp(_ArcDegrees, 10.0, 360.0);
                float angleMask = 1.0 - smoothstep(arc - 0.75, arc + 0.75, normalizedAngle);
                float innerRadius = max(0.01, 1.0 - clamp(_Thickness, 0.02, 0.6));
                float outline = clamp(_OutlineThickness, 0.0, 0.2);
                float outerMask = 1.0 - smoothstep(1.0, 1.0 + 0.01, radius);
                float innerMask = smoothstep(innerRadius, innerRadius + 0.01, radius);
                float ringMask = outerMask * innerMask * angleMask;
                float outlineMask = ringMask * (1.0 - smoothstep(innerRadius + outline, innerRadius + outline + 0.01, radius) +
                                                smoothstep(1.0 - outline - 0.01, 1.0 - outline, radius));
                outlineMask = saturate(outlineMask);
                float fillAngle = arc * saturate(_FillNormalized);
                float fillMask = ringMask * (1.0 - smoothstep(fillAngle - 0.75, fillAngle + 0.75, normalizedAngle));
                float3 color = _BackgroundColor.rgb;
                float alpha = ringMask * _BackgroundColor.a;
                color = lerp(color, _FillColor.rgb, fillMask * _FillColor.a);
                alpha = max(alpha, fillMask * _FillColor.a);
                color = lerp(color, _OutlineColor.rgb, outlineMask * _OutlineColor.a);
                alpha = max(alpha, outlineMask * _OutlineColor.a);
                alpha *= input.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                return fixed4(color * input.color.rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
