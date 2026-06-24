Shader "Cel Shader/Toon Outline"
{
	Properties
	{
		_OutlineThickness("Outline Thickness", Range(0,10)) = 1
		_OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
	}

	SubShader
	{
		Tags
		{
			"RenderType" = "Opaque"
			"RenderPipeline" = "UniversalPipeline"
		}

		Pass
		{
			Name "ToonOutline"

			Cull Front

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"


			float4 _OutlineColor;
			float _OutlineThickness;


			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
			};

			v2f vert(appdata v)
			{
				float outlineThickness = _OutlineThickness / 250; // diving OutlineThickness value to have more precise control over the slider in inspector.
				
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex + outlineThickness * v.normal);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				return _OutlineColor;
			}

			/*struct vertexInput
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
			};

			struct vertexOutput
			{
				float4 pos : SV_POSITION;
				float4 color : COLOR;
			};

			vertexOutput vert(vertexInput input)
			{
				vertexOutput output;

				float4 newPos = input.vertex;

				float outlineThickness = _OutlineThickness / 1000; // diving OutlineThickness value to have more precise control over the slider in inspector.

				// normal extrusion:
				float3 normal = normalize(input.normal);
				newPos += float4(normal, 0.0) * outlineThickness;
								
					// [FAILED] add object position:
						//float3 baseWorldPos = unity_ObjectToWorld._m03_m13_m23;
						//float3 addPosition = newPos + baseWorldPos;
						//float3 addPosition = newPos * input.vertex;

				// convert to world space:
				output.pos = UnityObjectToClipPos(newPos);

				output.color = _OutlineColor;
				return output;
			}

			float4 frag(vertexOutput input) : COLOR
			{
				return input.color;
			}*/

			ENDCG
		}
	}
}
