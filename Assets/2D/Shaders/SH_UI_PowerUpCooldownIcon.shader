Shader "Custom/UI/PowerUpCooldownIcon"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Graphic Tint", Color) = (1, 1, 1, 1)
        _LockedTint("Locked Tint", Color) = (0.38, 0.38, 0.38, 0.92)
        _CooldownProgress("Cooldown Progress", Range(0, 1)) = 1
        _DesaturationStrength("Desaturation Strength", Range(0, 1)) = 0.95
        _RevealFeather("Reveal Feather", Range(0, 0.25)) = 0.025
        _FillDirection("Fill Direction", Float) = 0
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
            "CanUseSpriteAtlas" = "True"
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
            fixed4 _LockedTint;
            float _CooldownProgress;
            float _DesaturationStrength;
            float _RevealFeather;
            float _FillDirection;

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
                fixed4 sampled = (tex2D(_MainTex, input.texcoord) + _TextureSampleAdd) * input.color;
                float luminance = dot(sampled.rgb, float3(0.299, 0.587, 0.114));
                float3 grayscale = lerp(sampled.rgb, luminance.xxx, saturate(_DesaturationStrength));
                float3 lockedColor = grayscale * _LockedTint.rgb;
                float axis = lerp(input.texcoord.y, 1.0 - input.texcoord.y, step(0.5, _FillDirection));
                float reveal = 1.0 - smoothstep(saturate(_CooldownProgress) - _RevealFeather,
                                                saturate(_CooldownProgress) + _RevealFeather,
                                                axis);
                float3 color = lerp(lockedColor, sampled.rgb, reveal);
                float alpha = sampled.a * lerp(_LockedTint.a, 1.0, reveal);

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                return fixed4(color * alpha, alpha);
            }
            ENDCG
        }
    }
}
