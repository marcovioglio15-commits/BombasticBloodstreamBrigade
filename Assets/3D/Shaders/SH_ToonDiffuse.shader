// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Cel Shader/Toon Diffuse"


{

	Properties

	// The shadow ranges (below) are very small because of the minimal values required to get the desired look. 
	// If needed, you can furhter adjust these ranges.

	{
		_MainTex("Texture", 2D) = "white" {}
		[NoScaleOffset] _NormalMap("Normal Map", 2D) = "bump" {} //added 05-03-2026
		_BumpScale ("Bump Scale", Range(0,2)) = 1 //added 05-03-2026
		_AmbientColor("Ambient Color", Color) = (0,0,0,1)
		_AmbientColorIntensity("Ambient Color Intensity", Range(0,5)) = 0.5
		_ShadowSoftness("Shadow Softness", Range(0,0.5)) = 0.1
		_ShadowScatter("Shadow Scatter", Range(0,10)) = 5
		_ShadowRangeMin("Shadow Range Min", Range(0,1)) = 0.54
		_ShadowRangeMax("Shadow Range Max", Range(-2,2)) = -0.4
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
			Name "ToonDiffuse"

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile_fog

			#include "UnityCG.cginc"

			#include "Lighting.cginc"


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

			struct VertexData //added 05-03-2026
			{
				float4 position : POSITION;
				float3 normal : NORMAL;
				float2 uv : TEXCOORD0;
			};

			struct Interpolators  //added 05-03-2026
			{
				float4 position : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 normal : TEXCOORD1;
			};

			sampler _MainTex;
			sampler _NormalMap; //added 05-03-2026
			float _BumpScale; //added 05-03-2026
			float4 _MainTex_ST;
			float4 _AmbientColor;
			float _AmbientColorIntensity;
			float _ShadowSoftness;
			float _ShadowScatter;
			float _ShadowRangeMin;
			float _ShadowRangeMax;

			Interpolators ToonNormalMap (v2f v) //added 05-03-2026
			{
				Interpolators i;
				i.uv = TRANSFORM_TEX(v.uv, _MainTex);
				i.position = UnityObjectToClipPos(v.vertex);
				i.normal = v.normal;
				return i;
			}

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.normal = UnityObjectToWorldNormal(v.normal);
				o.normal = normalize(o.normal);
				UNITY_TRANSFER_FOG(o, o.vertex);
				return o;
			}

			void InitializeFragmentNormal (inout Interpolators i) //added 05-03-2026
				{
					//DISCARDED i.normal = UnpackScaleNormal (tex2D(_NormalMap, i.uv), _BumpScale);

					//calculate normal vectors for normal map (math of UnpackScaleNormals, which doesn't work)
					i.normal.xy = tex2D(_NormalMap, i.uv).wy * 2 - 1;
					i.normal.xy *= _BumpScale;
					i.normal.z = sqrt(1 - saturate(dot(i.normal.xy, i.normal.xy)));

					i.normal = i.normal.xzy;
					i.normal = normalize(i.normal);
				}

			// inverse lerp + lerp = REMAP node in Shader Graph.

			// create inverse lerp function to use for Remap:
			float invLerp(float a, float b, float value)
			{
				return (value - a) / (b - a);
			}


			fixed4 frag(v2f i) : SV_Target
			{

				float shadowScatter = _ShadowScatter / 50; // dividing shadowScatter value to have more precise control over the slider in inspector.
				float3 lightDir = _WorldSpaceLightPos0.xyz; // getting the world space light (Directional Light in scene).

				// sample the Texture
				fixed4 col = tex2D(_MainTex, i.uv); //changed position 05-03-2026

				float firstRamp = dot(lightDir, i.normal); // NEW 06-03-2026
															// dot product between the Directional Light and the object's normal vector.

				i = ToonNormalMap (i); //added 05-03-2026
				InitializeFragmentNormal(i); //added 05-03-2026

				float ramp = dot(firstRamp, i.normal); // dot product between the first dot and the normal map
														//CHANGED 06-03-2026

				
				// REMAP of the dot product:
				float remapIn = invLerp(-1, 1, ramp);
				float remapOut = lerp(0, 1, remapIn);

				float shadowDivide1 = remapOut / shadowScatter;

				float shadowFloor = floor(shadowDivide1); // flooring the remap of the dot product.
				
				// REMAP of the shadowFloor:
				float shadowDivide2 = 1 / shadowScatter;
				float shadowRemapIn = invLerp(shadowDivide2, 0, shadowFloor);
				float shadowRemapOut = lerp(_ShadowRangeMin, _ShadowRangeMax, shadowRemapIn);			

				// smoothstep between the shadow remap and the shadow softness + adding the ambient color:
				float3 lighting = smoothstep(0, _ShadowSoftness, shadowRemapOut) + (_AmbientColor * _AmbientColorIntensity);

				float3 texLighting = col + lighting;

				// multiply the shaded texture with the base texture (to get colored shadows):
				fixed4 final = col * fixed4(texLighting, 1.0);

				// apply fog
				UNITY_APPLY_FOG(i.fogCoord, final);
				return final;
			}

			ENDCG
		}
	}
}